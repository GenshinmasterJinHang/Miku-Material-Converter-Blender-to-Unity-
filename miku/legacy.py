"""Strict compatibility island for Genshin, Wuwa, and HSR presets."""

from __future__ import annotations

from typing import Any, Mapping


class LegacyPresetError(ValueError):
    def __init__(self, code: str, message: str) -> None:
        self.code = code
        super().__init__(f"{code}: {message}")


_VERSIONS = {"genshin": "1.0", "wuwa": "1.0", "hsr": "1.1"}


def project_game_preset(document: Mapping[str, Any]) -> dict[str, Any] | None:
    if str(document.get("schema") or "") != "migr-preset-2.0":
        return None
    preset = document.get("preset")
    if not isinstance(preset, Mapping):
        raise LegacyPresetError("MIKU_LEGACY_PRESET_INVALID", "preset must be an object")
    family = str(preset.get("id") or "")
    if family not in _VERSIONS:
        raise LegacyPresetError("MIKU_LEGACY_PRESET_FAMILY", "Only genshin, wuwa, and hsr are accepted")
    if str(preset.get("version") or "") != _VERSIONS[family]:
        raise LegacyPresetError("MIKU_LEGACY_PRESET_VERSION", f"{family} requires version {_VERSIONS[family]}")
    companion = document.get(f"{family}ToonPreset")
    if family == "hsr":
        if not isinstance(companion, Mapping) or companion.get("schema") != "hsr-toon-1.1":
            raise LegacyPresetError("MIKU_HSR_COMPANION_SCHEMA", "HSR requires hsrToonPreset.schema=hsr-toon-1.1")
    elif companion is not None and not isinstance(companion, Mapping):
        raise LegacyPresetError("MIKU_LEGACY_COMPANION_INVALID", "Family companion must be an object")
    return {
        "family": family,
        "version": _VERSIONS[family],
        "materials": document.get("materials") or [],
        "globalTextures": document.get("globalTextures") or {},
        "resources": document.get("resources") or {},
        "companion": companion or {},
        "shaderLabBackend": f"{family.title()}LegacyShaderLabBackend",
    }
