"""Blender-compatible implicit socket conversions.

The registry is target-neutral.  It records the exact conversion selected at
an edge so Unity generation never relies on Shader Graph's implicit slot
coercion rules.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from typing import Any, Iterable, Mapping, Sequence


ALGORITHM_VERSION = "blender-5.2-implicit-v1"
DEFAULT_LUMA_COEFFICIENTS = (0.2126, 0.7152, 0.0722)


class SocketConversionError(ValueError):
    """Raised when Blender has no registered conversion for an edge."""


@dataclass(frozen=True)
class ColorManagementContext:
    """Color-management data that affects Blender's Color-to-Float coercion."""

    luminance_coefficients: tuple[float, float, float] = (
        DEFAULT_LUMA_COEFFICIENTS
    )
    config_fingerprint: str = "blender-5.2-bundled-ocio"

    def __post_init__(self) -> None:
        if len(self.luminance_coefficients) != 3:
            raise ValueError("luminance_coefficients must contain three values")

    def to_document(self) -> dict[str, Any]:
        return {
            "luminanceCoefficients": [
                float(value) for value in self.luminance_coefficients
            ],
            "configFingerprint": self.config_fingerprint,
        }


@dataclass(frozen=True)
class SocketConversion:
    source_type: str
    target_type: str
    conversion_kind: str
    algorithm_version: str = ALGORITHM_VERSION
    color_management: ColorManagementContext | None = None

    def to_document(self) -> dict[str, Any]:
        result: dict[str, Any] = {
            "sourceType": self.source_type,
            "targetType": self.target_type,
            "conversionKind": self.conversion_kind,
            "conversionAlgorithmVersion": self.algorithm_version,
        }
        if self.color_management is not None:
            result["colorManagement"] = self.color_management.to_document()
        return result


_ALIASES = {
    "VALUE": "Float",
    "SCALAR": "Float",
    "FLOAT": "Float",
    "INT": "Int",
    "INTEGER": "Int",
    "BOOL": "Bool",
    "BOOLEAN": "Bool",
    "VECTOR": "Vector3",
    "FLOAT2": "Vector2",
    "VECTOR2": "Vector2",
    "FLOAT3": "Vector3",
    "VECTOR3": "Vector3",
    "FLOAT4": "Vector4",
    "VECTOR4": "Vector4",
    "RGBA": "Color",
    "COLOR": "Color",
    "SHADER": "Closure",
    "CLOSURE": "Closure",
}


def canonical_socket_type(value: Any) -> str:
    raw = str(value or "Float")
    return _ALIASES.get(raw.upper(), raw)


class ImplicitSocketConversionRegistry:
    """Select and evaluate the Blender 5.2 conversion for a socket edge."""

    def __init__(
        self,
        color_management: ColorManagementContext | None = None,
    ) -> None:
        self.color_management = color_management or ColorManagementContext()

    def resolve(self, source_type: Any, target_type: Any) -> SocketConversion:
        source = canonical_socket_type(source_type)
        target = canonical_socket_type(target_type)
        if source == target:
            return SocketConversion(source, target, "Identity")
        kinds = {
            ("Float", "Color"): "FloatToColor",
            ("Float", "Vector2"): "FloatToVector",
            ("Float", "Vector3"): "FloatToVector",
            ("Float", "Vector4"): "FloatToVector",
            ("Color", "Float"): "ColorToFloatLuminance",
            ("Vector2", "Float"): "VectorToFloatAverage",
            ("Vector3", "Float"): "VectorToFloatAverage",
            ("Vector4", "Float"): "VectorToFloatAverage",
            ("Color", "Vector3"): "ColorToVectorRgb",
            ("Vector3", "Color"): "VectorToColorOpaque",
            ("Bool", "Float"): "BoolToFloat",
            ("Int", "Float"): "IntToFloat",
        }
        kind = kinds.get((source, target))
        if kind is None:
            raise SocketConversionError(
                f"MIKU_IMPLICIT_CONVERSION_UNSUPPORTED:{source}->{target}"
            )
        return SocketConversion(
            source,
            target,
            kind,
            color_management=(
                self.color_management
                if kind == "ColorToFloatLuminance"
                else None
            ),
        )

    def convert(
        self,
        value: Any,
        source_type: Any,
        target_type: Any,
    ) -> Any:
        conversion = self.resolve(source_type, target_type)
        kind = conversion.conversion_kind
        if kind == "Identity":
            return value
        if kind == "FloatToColor":
            scalar = float(value)
            return [scalar, scalar, scalar, 1.0]
        if kind == "FloatToVector":
            scalar = float(value)
            dimensions = int(conversion.target_type[-1])
            return [scalar] * dimensions
        if kind == "ColorToFloatLuminance":
            components = _components(value, 3)
            return sum(
                components[index]
                * self.color_management.luminance_coefficients[index]
                for index in range(3)
            )
        if kind == "VectorToFloatAverage":
            dimensions = int(conversion.source_type[-1])
            components = _components(value, dimensions)
            return sum(components) / dimensions
        if kind == "ColorToVectorRgb":
            return _components(value, 3)
        if kind == "VectorToColorOpaque":
            return [*_components(value, 3), 1.0]
        if kind == "BoolToFloat":
            return 1.0 if bool(value) else 0.0
        if kind == "IntToFloat":
            return float(int(value))
        raise AssertionError(f"Unhandled conversion kind: {kind}")


def color_config_fingerprint(config: Mapping[str, Any] | str) -> str:
    """Build a stable fingerprint without leaking an absolute OCIO path."""

    if isinstance(config, Mapping):
        payload = json.dumps(
            dict(config),
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
    else:
        payload = str(config)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def _components(value: Any, count: int) -> list[float]:
    if not isinstance(value, Sequence) or isinstance(value, (str, bytes)):
        raise SocketConversionError(
            f"MIKU_IMPLICIT_CONVERSION_VALUE_INVALID:expected-{count}-components"
        )
    if len(value) < count:
        raise SocketConversionError(
            f"MIKU_IMPLICIT_CONVERSION_VALUE_INVALID:expected-{count}-components"
        )
    return [float(value[index]) for index in range(count)]


def conversion_document(
    source_type: Any,
    target_type: Any,
    *,
    color_management: ColorManagementContext | None = None,
) -> dict[str, Any]:
    return ImplicitSocketConversionRegistry(
        color_management
    ).resolve(source_type, target_type).to_document()

