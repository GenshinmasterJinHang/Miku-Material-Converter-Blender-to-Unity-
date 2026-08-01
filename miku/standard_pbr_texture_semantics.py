"""Texture purpose inference for exported Blender shader resources."""

from __future__ import annotations

import os
import re
from typing import Any, Dict, Optional


STANDARD_PBR_TEXTURE_SEMANTICS = {
    "BaseColor": [
        "diffuse",
        "albedo",
        "basecolor",
        "base_color",
        "color",
        "base map",
        "颜色",
        "颜色贴图",
        "固有色",
        "漫反射",
    ],
    "AmbientOcclusion": [
        "ao",
        "ambientocclusion",
        "ambient_occlusion",
        "occlusion",
        "occ",
        "环境光遮蔽",
        "遮蔽",
    ],
    "Metalness": ["metalness", "metallic", "metal", "金属度", "金属"],
    "Roughness": ["roughness", "rough", "粗糙度", "粗糙"],
    "Bump": ["bump", "bumpmap", "凹凸"],
    "Normal": ["normal", "normalmap", "nrm", "法线", "法向"],
    "Height": ["height", "heightmap", "高度", "高度图"],
    "Displacement": ["displacement", "disp", "displace", "置换", "置换图"],
    "Reflection": ["reflection", "reflect", "refl", "反射"],
    "Specular": ["specular", "spec", "镜面", "高光", "反射"],
    "Glossiness": ["glossiness", "gloss", "smoothness", "光泽度", "平滑度"],
    "Emission": ["emission", "emissive", "glow", "自发光", "发光"],
    "Alpha": ["alpha", "opacity", "transparent", "透明", "透明度"],
}


STANDARD_PBR_TEXTURE_SEMANTIC_VALUES = [
    "BaseColor",
    "AmbientOcclusion",
    "Metalness",
    "Roughness",
    "Bump",
    "Normal",
    "Height",
    "Displacement",
    "Reflection",
    "Specular",
    "Glossiness",
    "Emission",
    "Alpha",
    "Unknown",
]


def infer_standard_pbr_texture_semantic(name: str, connection_hint: Optional[str] = None) -> Dict[str, Any]:
    """Infer advisory Standard PBR texture semantics from names.

    This is lower priority than node/socket graph semantics. Callers should use
    it only for loose, ambiguous, or unknown textures.
    """

    original = os.path.basename(name or "")
    text = f"{connection_hint or ''} {original}".lower()
    compact = _compact(text)
    original_compact = _compact(original)

    packing = infer_channel_packing_config(original)
    if packing:
        return {
            "semantic": "Unknown",
            "packing": packing,
            "confidence": 0.55,
        }

    best_semantic = ""
    best_needle = ""
    best_score = 0
    for semantic, needles in STANDARD_PBR_TEXTURE_SEMANTICS.items():
        for needle in needles:
            lower = needle.lower()
            needle_compact = _compact(lower)
            score = 0
            if lower and lower in text:
                score = max(score, 60 + len(lower))
            if needle_compact and (needle_compact in compact or needle_compact in original_compact):
                score = max(score, 70 + len(needle_compact))
            if score > best_score:
                best_score = score
                best_semantic = semantic
                best_needle = needle
    if not best_semantic:
        return {"semantic": "Unknown", "confidence": 0.0}
    return {
        "semantic": best_semantic,
        "matched": best_needle,
        "confidence": min(0.75, 0.35 + best_score / 200.0),
    }


def infer_channel_packing_config(name: str) -> Optional[Dict[str, Any]]:
    text = os.path.basename(name or "").lower()
    compact = _compact(text)
    stem = os.path.splitext(text)[0]
    tokens = {token for token in re.split(r"[^a-z0-9]+", stem) if token}
    if "maskmap" in compact or "mask_map" in text:
        return {
            "name": "MaskMap",
            "channels": {"R": "Metalness", "G": "AmbientOcclusion", "B": "Unused", "A": "Smoothness"},
        }
    if "orm" in tokens or compact.endswith("orm"):
        return {
            "name": "ORM",
            "channels": {"R": "AmbientOcclusion", "G": "Roughness", "B": "Metalness", "A": "Unused"},
        }
    if "rma" in tokens or compact.endswith("rma"):
        return {
            "name": "RMA",
            "channels": {"R": "Roughness", "G": "Metalness", "B": "AmbientOcclusion", "A": "Unused"},
        }
    if "mra" in tokens or compact.endswith("mra"):
        return {
            "name": "MRA",
            "channels": {"R": "Metalness", "G": "Roughness", "B": "AmbientOcclusion", "A": "Unused"},
        }
    return None


def infer_texture_semantic(name: str, connection_hint: Optional[str] = None) -> Dict[str, Any]:
    """Infer non-authoritative texture metadata from graph context and file names.

    The result is advisory. Exported Blender settings stay intact; consumers can
    use these fields for import defaults, variable names, and diagnostics.
    """

    text = f"{connection_hint or ''} {os.path.basename(name or '')}".lower()
    original = os.path.basename(name or "")

    if _contains_any(text, original, ["lightmap", "light_map", "光照图"]):
        return {
            "semantic": "LightMapTexture",
            "recommendedColorSpace": "Linear",
            "channels": {
                "r": "AO",
                "g": "ShadowDetail",
                "b": "SpecThreshold",
                "a": "RampOrMaterialId",
            },
        }
    if _contains_any(text, original, ["facemap", "face_map", "face map", "face-map", "面部图", "面部贴图"]):
        return {
            "semantic": "FaceMapTexture",
            "recommendedColorSpace": "sRGB",
            "channels": {
                "r": "Mask",
                "g": "AO",
                "b": "NoseLine",
                "a": "SDF",
            },
        }
    ramp_semantic = _infer_ramp_semantic(text, original)
    if ramp_semantic:
        return {
            "semantic": ramp_semantic,
            "baseSemantic": "RampTexture",
            "recommendedColorSpace": "sRGB",
            "recommendedFilter": "Point",
            "recommendedWrap": "Clamp",
            "recommendedMipmap": "DisabledOrWarn",
            "requirePointSampling": True,
            "requireNoBilinear": True,
        }
    if _contains_any(text, original, ["sdf", "faceshadow", "脸部阴影", "阴影sdf"]):
        return {
            "semantic": "FaceSDFTexture",
            "recommendedColorSpace": "Linear",
        }
    if _contains_any(text, original, ["matcap"]):
        return {
            "semantic": "MatCapTexture",
            "recommendedColorSpace": "sRGB",
            "recommendedWrap": "Clamp",
        }
    stocking_semantic = _infer_stocking_semantic(text, original)
    if stocking_semantic:
        return {
            "semantic": stocking_semantic,
            "recommendedColorSpace": "Linear",
        }
    if _contains_any(
        text,
        original,
        [
            "basecolor",
            "albedo",
            "diffuse",
            "color",
            "base map",
            "上衣颜色",
            "下衣颜色",
            "头发颜色",
            "脸部颜色",
            "基础色",
            "基础色与高光",
        ],
    ):
        return {
            "semantic": "BaseColorTexture",
            "recommendedColorSpace": "sRGB",
        }
    if _contains_any(text, original, ["mask", "遮罩", "_id", "-id"]):
        return {
            "semantic": "MaskTexture",
            "recommendedColorSpace": "Linear",
        }
    if _contains_any(text, original, ["emission", "emissive", "glow", "eye_emission", "自发光", "发光"]):
        return {
            "semantic": "EmissionTexture",
            "recommendedColorSpace": "sRGB",
        }
    return {}


def _contains_any(lower_text: str, original_text: str, needles: list[str]) -> bool:
    for needle in needles:
        if needle in lower_text or needle in original_text:
            return True
    return False


def _infer_ramp_semantic(lower_text: str, original_text: str) -> str:
    compact = _compact(lower_text)
    original_compact = _compact(original_text)
    if _contains_any(
        lower_text,
        original_text,
        ["身体冷色调ramp", "身体冷色调Ramp", "body cool ramp", "body_cool_ramp"],
    ) or "bodycoolramp" in compact or "bodycoolramp" in original_compact:
        return "BodyCoolRampTexture"
    if _contains_any(
        lower_text,
        original_text,
        ["身体暖色调ramp", "身体暖色调Ramp", "body warm ramp", "body_warm_ramp"],
    ) or "bodywarmramp" in compact or "bodywarmramp" in original_compact:
        return "BodyWarmRampTexture"
    if _contains_any(
        lower_text,
        original_text,
        ["头发冷色调ramp", "头发冷色调Ramp", "hair cool ramp", "hair_cool_ramp"],
    ) or "haircoolramp" in compact or "haircoolramp" in original_compact:
        return "HairCoolRampTexture"
    if _contains_any(
        lower_text,
        original_text,
        ["头发暖色调ramp", "头发暖色调Ramp", "hair warm ramp", "hair_warm_ramp"],
    ) or "hairwarmramp" in compact or "hairwarmramp" in original_compact:
        return "HairWarmRampTexture"
    if _contains_any(lower_text, original_text, ["ramp", "色调ramp", "色调Ramp"]):
        return "UnknownRampTexture"
    return ""


def _infer_stocking_semantic(lower_text: str, original_text: str) -> str:
    compact = _compact(f"{lower_text} {original_text}")
    if (
        "左丝袜" in original_text
        or "左黑丝" in original_text
        or "leftstocking" in compact
        or "leftsocks" in compact
        or "lefttights" in compact
    ):
        return "UpperStockingMaskTexture"
    if (
        "右丝袜" in original_text
        or "右黑丝" in original_text
        or "rightstocking" in compact
        or "rightsocks" in compact
        or "righttights" in compact
    ):
        return "LowerStockingMaskTexture"
    if (
        "上衣黑丝图" in original_text
        or "上半身黑丝" in original_text
        or "upperstocking" in compact
        or "upperpantyhose" in compact
        or "uppertights" in compact
    ):
        return "UpperStockingMaskTexture"
    if (
        "下衣黑丝图" in original_text
        or "下半身黑丝" in original_text
        or "lowerstocking" in compact
        or "lowerpantyhose" in compact
        or "lowertights" in compact
    ):
        return "LowerStockingMaskTexture"
    if _contains_any(
        lower_text,
        original_text,
        ["黑丝", "丝袜", "pantyhose", "stocking", "stockings", "tights", "silk stocking"],
    ):
        return "StockingMaskTexture"
    return ""


def _compact(value: str) -> str:
    return "".join(ch.lower() for ch in value if ch.isalnum())
