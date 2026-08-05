"""Blender runtime compatibility policy for the Miku extension."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Iterable


MIN_BLENDER_VERSION = (5, 0, 0)
BLENDER_MAJOR_VERSION = 5
CERTIFIED_BLENDER_VERSION = (5, 2, 0)
CERTIFIED_BLENDER_VERSION_STRING = "5.2.0 LTS"
CERTIFIED_BLENDER_COMMIT = "fbe6228777e7d9afefcd61a413844e790ae75db7"


@dataclass(frozen=True)
class BlenderCompatibility:
    version: tuple[int, int, int]
    supported: bool
    certified: bool

    @property
    def diagnostic(self) -> str | None:
        actual = ".".join(str(part) for part in self.version)
        if not self.supported:
            return (
                "MIKU_BLENDER_VERSION_UNSUPPORTED:"
                f"actual={actual}:supported=5.x"
            )
        if not self.certified:
            return (
                "MIKU_BLENDER_VERSION_UNVALIDATED:"
                f"actual={actual}:certified=5.2.0"
            )
        return None


def normalize_blender_version(value: Iterable[Any]) -> tuple[int, int, int]:
    try:
        parts = tuple(int(part) for part in value)
    except (TypeError, ValueError) as exc:
        raise RuntimeError(
            "MIKU_BLENDER_VERSION_UNSUPPORTED:"
            "actual=<unknown>:supported=5.x"
        ) from exc
    if len(parts) < 3 or any(part < 0 for part in parts[:3]):
        raise RuntimeError(
            "MIKU_BLENDER_VERSION_UNSUPPORTED:"
            "actual=<unknown>:supported=5.x"
        )
    return parts[:3]


def classify_blender_version(value: Iterable[Any]) -> BlenderCompatibility:
    version = normalize_blender_version(value)
    supported = version[0] == BLENDER_MAJOR_VERSION
    return BlenderCompatibility(
        version=version,
        supported=supported,
        certified=version == CERTIFIED_BLENDER_VERSION,
    )


def require_supported_blender(value: Iterable[Any]) -> BlenderCompatibility:
    compatibility = classify_blender_version(value)
    if not compatibility.supported:
        raise RuntimeError(str(compatibility.diagnostic))
    return compatibility


def blender_build_hash(bpy_module: Any) -> str:
    value = getattr(getattr(bpy_module, "app", None), "build_hash", b"")
    if isinstance(value, bytes):
        value = value.decode("ascii", errors="replace")
    return str(value or "").strip().lower()


def blender_version_string(bpy_module: Any) -> str:
    version = normalize_blender_version(
        getattr(getattr(bpy_module, "app", None), "version", ())
    )
    return ".".join(str(part) for part in version)
