"""Safe migration helpers for Blender frame-driven scalar sockets."""

from __future__ import annotations

import ast
import math
from dataclasses import dataclass


class TimeDriverError(ValueError):
    """A driver cannot be represented by the Miku time contract."""


@dataclass(frozen=True)
class AffineFrame:
    scale: float
    offset: float


def parse_affine_frame(expression: str) -> AffineFrame:
    """Parse a scalar ``a * frame + b`` expression without evaluating code."""

    try:
        tree = ast.parse(str(expression), mode="eval")
    except SyntaxError as exc:
        raise TimeDriverError("MIKU_TIME_DRIVER_SYNTAX_INVALID") from exc

    def visit(node: ast.AST) -> tuple[float, float]:
        if isinstance(node, ast.Expression):
            return visit(node.body)
        if isinstance(node, ast.Name):
            if node.id != "frame":
                raise TimeDriverError(
                    f"MIKU_TIME_DRIVER_NAME_UNSUPPORTED:{node.id}"
                )
            return 1.0, 0.0
        if isinstance(node, ast.Constant):
            if isinstance(node.value, bool) or not isinstance(
                node.value,
                (int, float),
            ):
                raise TimeDriverError("MIKU_TIME_DRIVER_LITERAL_INVALID")
            value = float(node.value)
            if not math.isfinite(value):
                raise TimeDriverError("MIKU_TIME_DRIVER_NUMBER_NONFINITE")
            return 0.0, value
        if isinstance(node, ast.UnaryOp) and isinstance(
            node.op,
            (ast.UAdd, ast.USub),
        ):
            scale, offset = visit(node.operand)
            if isinstance(node.op, ast.USub):
                return -scale, -offset
            return scale, offset
        if isinstance(node, ast.BinOp) and isinstance(
            node.op,
            (ast.Add, ast.Sub, ast.Mult, ast.Div),
        ):
            left_scale, left_offset = visit(node.left)
            right_scale, right_offset = visit(node.right)
            if isinstance(node.op, ast.Add):
                return left_scale + right_scale, left_offset + right_offset
            if isinstance(node.op, ast.Sub):
                return left_scale - right_scale, left_offset - right_offset
            if isinstance(node.op, ast.Mult):
                if left_scale and right_scale:
                    raise TimeDriverError("MIKU_TIME_DRIVER_NON_AFFINE")
                return (
                    left_scale * right_offset + right_scale * left_offset,
                    left_offset * right_offset,
                )
            if right_scale:
                raise TimeDriverError("MIKU_TIME_DRIVER_NON_AFFINE")
            if right_offset == 0.0:
                raise TimeDriverError("MIKU_TIME_DRIVER_DIVIDE_BY_ZERO")
            return left_scale / right_offset, left_offset / right_offset
        raise TimeDriverError(
            f"MIKU_TIME_DRIVER_AST_UNSUPPORTED:{type(node).__name__}"
        )

    scale, offset = visit(tree)
    if not math.isfinite(scale) or not math.isfinite(offset):
        raise TimeDriverError("MIKU_TIME_DRIVER_NUMBER_NONFINITE")
    return AffineFrame(scale, offset)
