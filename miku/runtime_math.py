"""Reference math for Miku runtime-input conformance tests.

These functions are an independently written, target-neutral description of
the public Blender node behavior. Production Shader Graph generation expands
the same equations into native nodes rather than calling this module.
"""

from __future__ import annotations

import math
import colorsys


def overlay(
    base: float,
    blend: float,
    factor: float = 1.0,
    *,
    clamp_factor: bool = True,
) -> float:
    """Evaluate Blender 5.2 Overlay followed by the Mix node factor."""

    a = float(base)
    b = float(blend)
    t = float(factor)
    if clamp_factor:
        t = min(max(t, 0.0), 1.0)
    combined = 2.0 * a * b if a < 0.5 else 1.0 - 2.0 * (1.0 - a) * (1.0 - b)
    return a + (combined - a) * t


def dielectric_fresnel(
    cosine: float,
    eta: float,
    *,
    front_facing: bool = True,
) -> float:
    """Return the unpolarized dielectric Fresnel reflectance."""

    c = abs(float(cosine))
    ratio = max(float(eta), 1.0e-5)
    if not front_facing:
        ratio = 1.0 / ratio
    g_squared = ratio * ratio - 1.0 + c * c
    if g_squared <= 0.0:
        return 1.0
    g = math.sqrt(g_squared)
    a = (g - c) / (g + c)
    b = (c * (g + c) - 1.0) / (c * (g - c) + 1.0)
    return 0.5 * a * a * (1.0 + b * b)


def layer_weight(
    blend: float,
    cosine: float,
    *,
    front_facing: bool = True,
) -> tuple[float, float]:
    """Return Blender-compatible ``(Fresnel, Facing)`` Layer Weight outputs."""

    blend_value = float(blend)
    eta = max(1.0 - blend_value, 1.0e-5)
    fresnel = dielectric_fresnel(
        cosine,
        1.0 / eta if front_facing else eta,
        front_facing=True,
    )
    facing = abs(float(cosine))
    if blend_value != 0.5:
        clamped = min(max(blend_value, 0.0), 0.99999)
        exponent = (
            2.0 * clamped
            if clamped < 0.5
            else 0.5 / (1.0 - clamped)
        )
        facing = math.pow(facing, exponent)
    return fresnel, 1.0 - facing


def hue_saturation_value(
    color: tuple[float, float, float],
    hue: float,
    saturation: float,
    value: float,
    factor: float,
) -> tuple[float, float, float]:
    """Return Blender ShaderNodeHueSaturation RGB behavior."""

    red, green, blue = (float(component) for component in color)
    source_hue, source_saturation, source_value = colorsys.rgb_to_hsv(
        red, green, blue
    )
    shifted_hue = (source_hue + float(hue) - 0.5) % 1.0
    adjusted = colorsys.hsv_to_rgb(
        shifted_hue,
        min(max(source_saturation * float(saturation), 0.0), 1.0),
        source_value * float(value),
    )
    blend = float(factor)
    return tuple(
        original + (converted - original) * blend
        for original, converted in zip((red, green, blue), adjusted)
    )


def two_element_bspline(
    first: float,
    second: float,
    factor: float,
) -> float:
    """Evaluate a two-control-point cubic B-spline with endpoint replication."""

    t = min(max(float(factor), 0.0), 1.0)
    weight_second = (
        1.0 + 3.0 * t + 3.0 * t * t - 2.0 * t * t * t
    ) / 6.0
    return float(first) * (1.0 - weight_second) + float(second) * weight_second
