"""Contracts shared by the fixed Miku shader workflows.

These names describe author intent.  Unity property names stay in the Unity
backend so the interchange format does not expose target implementation data.
"""

from __future__ import annotations

import os
import re
from typing import Iterable


FIXED_WORKFLOWS = frozenset(
    {"genshin_toon", "wuwa_toon", "hsr_toon", "endfield_toon"}
)

FIXED_TEXTURE_ROLES = (
    "BaseMap",
    "LightMap",
    "ShadowRampMap",
    "MetalMap",
    "EmissionMap",
    "FaceSDF",
    "HairRampMap",
    "HairSpecMap",
    "NormalMap",
    "WuwaPackedNormalRoughnessMetallic",
    "IDMap",
    "MatCap",
    "OutlineColorMap",
    "FaceID",
    "FaceHET",
    "SkinRamp",
    "HairHM",
    "EyeHET",
    "EyeHDMF",
    "EyeUpperHighlight",
    "EyeLowerHighlight",
    "EyeEG",
    "BodyCoolRamp",
    "BodyWarmRamp",
    "StockingsMap",
    "FaceMap",
    "HairCoolRamp",
    "HairWarmRamp",
    "MaterialParamMap",
    "DiffRampMap",
    "SpecRampMap",
    "ShadowLut",
    "SplitNormalMap",
    "OutlineMask",
    "SpecularMask",
    "LineMap",
    "StrokeMap",
    "FaceSDFMask",
    "EmotionMap",
    "HighlightMap",
    "HairShadowMap",
    "EyeShadowMap",
    "EffectMask",
    "ColorLut",
    "FaceAreaMap",
    "FaceRefineMap",
    "HairRefineMap",
    "HairShiftMap",
    "HairLineMap",
    "SpecularRefineF0",
    "SpecularRefineColor",
)

WORKFLOW_TEXTURE_ROLES = {
    "genshin_toon": frozenset(
        {
            "BaseMap",
            "LightMap",
            "ShadowRampMap",
            "MetalMap",
            "EmissionMap",
            "FaceSDF",
            "HairRampMap",
            "HairSpecMap",
            "NormalMap",
        }
    ),
    "wuwa_toon": frozenset(
        {
            "BaseMap",
            "NormalMap",
            "WuwaPackedNormalRoughnessMetallic",
            "IDMap",
            "MatCap",
            "OutlineColorMap",
            "EmissionMap",
            "FaceSDF",
            "FaceID",
            "FaceHET",
            "SkinRamp",
            "HairHM",
            "EyeHET",
            "EyeHDMF",
            "EyeUpperHighlight",
            "EyeLowerHighlight",
            "EyeEG",
            "StockingsMap",
        }
    ),
    "hsr_toon": frozenset(
        {
            "BaseMap",
            "LightMap",
            "BodyCoolRamp",
            "BodyWarmRamp",
            "StockingsMap",
            "EmissionMap",
            "FaceMap",
            "HairCoolRamp",
            "HairWarmRamp",
        }
    ),
    "endfield_toon": frozenset(
        {
            "BaseMap",
            "NormalMap",
            "MaterialParamMap",
            "DiffRampMap",
            "SpecRampMap",
            "ShadowLut",
            "EmissionMap",
            "MatCap",
            "SplitNormalMap",
            "OutlineMask",
            "SpecularMask",
            "LineMap",
            "StrokeMap",
            "FaceSDF",
            "FaceSDFMask",
            "EmotionMap",
            "HighlightMap",
            "HairShadowMap",
            "EyeShadowMap",
            "EffectMask",
            "ColorLut",
            "FaceAreaMap",
            "FaceRefineMap",
            "HairRefineMap",
            "HairShiftMap",
            "HairLineMap",
            "SpecularRefineF0",
            "SpecularRefineColor",
        }
    ),
}

_ALIASES = {
    "basecolor": "BaseMap",
    "albedo": "BaseMap",
    "diffuse": "BaseMap",
    "emission": "EmissionMap",
    "emissive": "EmissionMap",
    "normal": "NormalMap",
    "facesdfmap": "FaceSDF",
    "facelightmap": "FaceSDF",
    "bodyshadowramp": "ShadowRampMap",
    "shadowramp": "ShadowRampMap",
    "hairshadowramp": "HairRampMap",
}
_ALIASES.update(
    {
        re.sub(r"[^0-9a-z]+", "", role.casefold()): role
        for role in FIXED_TEXTURE_ROLES
    }
)

LINEAR_TEXTURE_ROLES = frozenset(
    {
        "LightMap",
        "MetalMap",
        "FaceSDF",
        "NormalMap",
        "WuwaPackedNormalRoughnessMetallic",
        "IDMap",
        "OutlineColorMap",
        "FaceID",
        "FaceHET",
        "HairHM",
        "EyeHET",
        "EyeHDMF",
        "EyeUpperHighlight",
        "EyeLowerHighlight",
        "EyeEG",
        "StockingsMap",
        "FaceMap",
        "MaterialParamMap",
        "SplitNormalMap",
        "OutlineMask",
        "SpecularMask",
        "LineMap",
        "StrokeMap",
        "FaceSDFMask",
        "HairShadowMap",
        "EyeShadowMap",
        "EffectMask",
        "FaceAreaMap",
        "FaceRefineMap",
        "HairRefineMap",
        "HairShiftMap",
        "HairLineMap",
        "SpecularRefineF0",
    }
)


def _key(value: str) -> str:
    return re.sub(r"[^0-9a-z]+", "", (value or "").casefold())


def normalize_texture_role(value: str) -> str:
    """Return a canonical role only for a complete, controlled alias."""

    candidate = (value or "").strip()
    if candidate.casefold().startswith("miku:"):
        candidate = candidate.split(":", 1)[1].strip()
    return _ALIASES.get(_key(candidate), "")


def infer_filename_texture_role(value: str) -> str:
    """Infer from a whole stem or one delimiter-bounded terminal suffix."""

    stem = os.path.splitext(os.path.basename(value or ""))[0]
    if re.search(r"eye[\s_.-]+het$", stem, flags=re.IGNORECASE):
        return "EyeHET"
    endfield = _infer_endfield_filename_texture_role(stem)
    if endfield:
        return endfield
    exact = normalize_texture_role(stem)
    if exact:
        return exact
    tokens = [item for item in re.split(r"[\s_.-]+", stem) if item]
    # Permit compound canonical suffixes (for example Body_Cool_Ramp) without
    # allowing arbitrary substring matches such as "normalish".
    for count in range(min(4, len(tokens)), 0, -1):
        role = normalize_texture_role("".join(tokens[-count:]))
        if role:
            return role
    return ""


def infer_wuwa_filename_texture_role(value: str, part: str) -> str:
    """Infer source-game roles only inside an explicit WuWa workflow part."""

    stem = os.path.splitext(os.path.basename(value or ""))[0]
    normalized = re.sub(r"[\s.-]+", "_", stem.casefold()).strip("_")
    normalized_part = (part or "Body").strip().title()
    if normalized.endswith("_switch_d"):
        return ""
    if normalized_part == "Body" and normalized.endswith("_ld"):
        return "OutlineColorMap"
    if normalized_part == "Body" and normalized.endswith("_n"):
        return "WuwaPackedNormalRoughnessMetallic"
    if normalized_part == "Face" and normalized.endswith("face_het"):
        return "FaceHET"
    if normalized_part == "Eye" and normalized.endswith("eye_het"):
        return "EyeHET"
    return ""


def _infer_endfield_filename_texture_role(stem: str) -> str:
    """Recognize only complete Endfield-style resource filename patterns."""

    value = (stem or "").casefold()
    if not value.startswith(("t_actor_", "t_fx_", "t_wpn_")):
        return ""
    if "hairshadow" in value and value.endswith("_m"):
        return "HairShadowMap"
    if "eyeshadow" in value and value.endswith("_m"):
        return "EyeShadowMap"
    if value.endswith("_cm_m"):
        return "FaceAreaMap"
    if value.endswith("_hl_m"):
        return "HighlightMap"
    if value.endswith("_sw_m"):
        return "SpecularMask"
    if "hairline" in value and value.endswith("_m"):
        return "HairLineMap"
    if "hairst" in value and value.endswith("_st"):
        return "HairShiftMap"
    if "emotion" in value and value.endswith("_d"):
        return "EmotionMap"
    if "matcap" in value and value.endswith("_d"):
        return "MatCap"
    if "lut" in value and value.endswith("_d"):
        return "ColorLut"
    if value.endswith("_sdf"):
        return "FaceSDF"
    if value.startswith("t_fx_") and value.endswith("_m"):
        return "EffectMask"
    if value.endswith("_hn"):
        return "SplitNormalMap"
    if value.endswith("_rd"):
        return "DiffRampMap"
    if value.endswith("_rs"):
        return "SpecRampMap"
    if value.endswith("_st") and "_face_" in value:
        return "FaceRefineMap"
    if value.endswith("_st") and "_hair_" in value:
        return "HairRefineMap"
    if value.endswith("_st"):
        return "OutlineMask"
    if value.endswith("_n"):
        return "NormalMap"
    if value.endswith("_p") or value.endswith("_cloth_01_m"):
        return "MaterialParamMap"
    if value.endswith("_e"):
        return "EmissionMap"
    if value.endswith("_d"):
        return "BaseMap"
    return ""


def allowed_texture_role(workflow: str, role: str) -> bool:
    return role in WORKFLOW_TEXTURE_ROLES.get(workflow, frozenset())


GENSHIN_REQUIRED_TEXTURE_ROLES = {
    "Body": frozenset({"BaseMap", "LightMap", "ShadowRampMap"}),
    "Hair": frozenset({"BaseMap", "LightMap", "HairRampMap"}),
    "Face": frozenset({"BaseMap", "FaceSDF", "ShadowRampMap"}),
    "Eye": frozenset({"BaseMap"}),
}


def normalize_genshin_filename_role(part: str, role: str) -> str:
    """Resolve ambiguous Genshin ramp filenames using the material part."""

    normalized_part = (part or "Body").strip().title()
    if role in {"ShadowRampMap", "HairRampMap"}:
        return "HairRampMap" if normalized_part == "Hair" else "ShadowRampMap"
    return role


def genshin_required_texture_roles(part: str) -> frozenset[str]:
    """Return the blocking texture contract for one Genshin material part."""

    return GENSHIN_REQUIRED_TEXTURE_ROLES.get(
        (part or "Body").strip().title(),
        frozenset(),
    )


def texture_role_color_space(roles: Iterable[str]) -> str:
    values = {role for role in roles if role}
    return "Linear" if values & LINEAR_TEXTURE_ROLES else "sRGB"
