"""Headless structural acceptance for the locked colorful-coating corpus."""

from __future__ import annotations

import hashlib
import json
import os
import pathlib
import sys

import bpy
from mathutils import Color


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
MATERIAL_LIBRARY_ROOT = pathlib.Path(
    os.environ.get("MIKU_MATERIAL_LIBRARY_ROOT")
    or REPOSITORY_ROOT / "材质库"
)
CORPUS_PATH = (
    MATERIAL_LIBRARY_ROOT / "石头" / "彩色镀层" / "彩色镀层.blend"
)
CORPUS_SHA256 = (
    "b02d5d317af2787023a71993d90ceaceb2066917637338fefd95157f9abd7942"
)

if str(REPOSITORY_ROOT) not in sys.path:
    sys.path.insert(0, str(REPOSITORY_ROOT))

import miku_blender  # noqa: E402
from miku.planner import ConversionPlanner  # noqa: E402
from miku.runtime_math import (  # noqa: E402
    hue_saturation_value,
    two_element_bspline,
)
from miku.semantic import build_material_ir  # noqa: E402


def iter_nodes(tree, seen=None):
    if tree is None:
        return
    if seen is None:
        seen = set()
    key = int(tree.as_pointer())
    if key in seen:
        return
    seen.add(key)
    for node in tree.nodes:
        yield node
        if node.bl_idname == "ShaderNodeGroup":
            yield from iter_nodes(node.node_tree, seen)


def main() -> None:
    if tuple(bpy.app.version) != (5, 2, 0):
        raise RuntimeError(
            f"MIKU_BLENDER_VERSION_MISMATCH:{tuple(bpy.app.version)}"
        )
    digest = hashlib.sha256(CORPUS_PATH.read_bytes()).hexdigest()
    if digest != CORPUS_SHA256:
        raise RuntimeError(
            f"MIKU_COLORFUL_COATING_CORPUS_HASH_MISMATCH:{digest}"
        )
    bpy.ops.wm.open_mainfile(filepath=str(CORPUS_PATH))
    source_color = (0.8, 0.2, 0.1)
    hue, saturation, value, factor = (0.75, 2.0, 0.5, 0.25)
    blender_color = Color(source_color)
    source_hue, source_saturation, source_value = blender_color.hsv
    blender_color.hsv = (
        (source_hue + hue - 0.5) % 1.0,
        min(max(source_saturation * saturation, 0.0), 1.0),
        source_value * value,
    )
    expected_hsv = tuple(
        original + (converted - original) * factor
        for original, converted in zip(source_color, blender_color)
    )
    actual_hsv = hue_saturation_value(
        source_color, hue, saturation, value, factor
    )
    if any(
        abs(actual - expected) > 1.0e-4
        for actual, expected in zip(actual_hsv, expected_hsv)
    ):
        raise RuntimeError(
            f"MIKU_HSV_ORACLE_MISMATCH:{actual_hsv}:{expected_hsv}"
        )
    bspline_nodes = [
        node
        for material in bpy.data.materials
        for node in iter_nodes(material.node_tree)
        if node.bl_idname == "ShaderNodeValToRGB"
        and node.color_ramp.interpolation == "B_SPLINE"
        and len(node.color_ramp.elements) == 2
    ]
    if not bspline_nodes:
        raise RuntimeError("MIKU_BSPLINE_CORPUS_NODE_MISSING")
    for node in bspline_nodes:
        elements = sorted(
            node.color_ramp.elements,
            key=lambda item: item.position,
        )
        width = elements[1].position - elements[0].position
        for normalized in (0.0, 0.25, 0.5, 0.75, 1.0):
            position = elements[0].position + width * normalized
            actual = node.color_ramp.evaluate(position)
            for channel in range(4):
                expected = two_element_bspline(
                    elements[0].color[channel],
                    elements[1].color[channel],
                    normalized,
                )
                if abs(actual[channel] - expected) > 1.0e-4:
                    raise RuntimeError(
                        "MIKU_BSPLINE_ORACLE_MISMATCH:"
                        f"{node.name}:{normalized}:{channel}:"
                        f"{actual[channel]}:{expected}"
                    )
    summaries = []
    for material in sorted(bpy.data.materials, key=lambda item: item.name):
        snapshot = miku_blender.snapshot_material(material)
        material_ir = build_material_ir(
            snapshot,
            source_blend_id=CORPUS_SHA256,
            material_key=material.name,
        )
        if material.name in {"彩色镀层1", "彩色镀层3", "彩色镀层10"}:
            normal = next(
                item
                for item in material_ir["channels"]
                if item["semantic"] == "Normal"
            )
            if normal.get("default") != [0.0, 0.0, 1.0]:
                raise RuntimeError(
                    "MIKU_COLORFUL_COATING_NEUTRAL_NORMAL_MISSING:"
                    f"{material.name}:{normal.get('default')}"
                )
            expressions = {
                item["op"]: item
                for item in material_ir["expressions"]
                if item["op"] in {
                    "Input.Normal",
                    "Input.ViewDirection",
                    "Math.LayerWeightFacing",
                }
            }
            missing = {
                "Input.Normal",
                "Input.ViewDirection",
                "Math.LayerWeightFacing",
            } - set(expressions)
            if missing:
                raise RuntimeError(
                    "MIKU_COLORFUL_COATING_RUNTIME_EXPRESSION_MISSING:"
                    f"{material.name}:{sorted(missing)}"
                )
            for op in ("Input.Normal", "Input.ViewDirection"):
                expression = expressions[op]
                if (
                    expression.get("space") != "World"
                    or expression.get("stage") != "Fragment"
                ):
                    raise RuntimeError(
                        "MIKU_COLORFUL_COATING_RUNTIME_INPUT_TYPE_INVALID:"
                        f"{material.name}:{op}:"
                        f"{expression.get('space')}:{expression.get('stage')}"
                    )
            second_ir = build_material_ir(
                snapshot,
                source_blend_id=CORPUS_SHA256,
                material_key=material.name,
            )
            if sorted(
                item["id"] for item in material_ir["expressions"]
            ) != sorted(item["id"] for item in second_ir["expressions"]):
                raise RuntimeError(
                    "MIKU_COLORFUL_COATING_EXPRESSION_IDS_UNSTABLE:"
                    f"{material.name}"
                )
        plan = ConversionPlanner().plan(material_ir)
        summaries.append(
            {
                "material": material.name,
                "channels": {
                    item["semantic"]: (
                        (item.get("value") or {}).get("kind")
                        or ("Bake" if item.get("requiresBake") else "Constant")
                    )
                    for item in material_ir["channels"]
                },
                "expressionOps": sorted(
                    {item["op"] for item in material_ir["expressions"]}
                ),
                "jobs": [
                    {
                        "scope": item.get("scope", "Region"),
                        "usage": item.get("usage"),
                        "semantics": item.get("semantics"),
                    }
                    for item in plan["bakeJobs"]
                ],
                "errors": [
                    item
                    for item in plan["diagnostics"]
                    if str(item.get("severity") or "").lower() == "error"
                ],
            }
        )
    print("MIKU_COLORFUL_COATING_SCAN=" + json.dumps(
        summaries,
        ensure_ascii=False,
        sort_keys=True,
    ))


if __name__ == "__main__":
    main()
