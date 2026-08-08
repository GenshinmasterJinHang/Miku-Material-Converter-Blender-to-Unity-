"""Blender runtime compatibility policy for the Miku extension."""

from __future__ import annotations

from io import BytesIO
from pathlib import Path
from tempfile import TemporaryDirectory
from dataclasses import dataclass
from typing import Any, Iterable


MIN_BLENDER_VERSION = (5, 0, 0)
MAX_BLENDER_VERSION_EXCLUSIVE = (5, 3, 0)
CERTIFIED_BLENDER_VERSION = (5, 2, 0)
CERTIFIED_BLENDER_VERSION_STRING = "5.2.0 LTS"
CERTIFIED_BLENDER_COMMIT = "fbe6228777e7d9afefcd61a413844e790ae75db7"
TECHNICAL_ADAPTERS = {
    (5, 0): "Blender50Adapter",
    (5, 1): "Blender51Adapter",
    (5, 2): "Blender52Adapter",
}
TARGA_PNG_STRATEGIES = {
    (5, 0): "ImageDatablock",
    (5, 1): "ImageDatablock",
    (5, 2): "MemoryBuffer",
}
_REQUIRED_BPY_TYPES = (
    "Material",
    "NodeSocketColor",
    "NodeSocketFloat",
    "NodeSocketVector",
    "NodeTreeInterface",
    "ShaderNodeBsdfPrincipled",
    "ShaderNodeBump",
    "ShaderNodeDisplacement",
    "ShaderNodeNormalMap",
    "ShaderNodeOutputMaterial",
    "ShaderNodeTexImage",
    "ShaderNodeTree",
)
_REQUIRED_BPY_PROPERTIES = (
    "BoolProperty",
    "EnumProperty",
    "IntProperty",
    "StringProperty",
)


@dataclass(frozen=True)
class BlenderCompatibility:
    version: tuple[int, int, int]
    supported: bool
    certified: bool

    @property
    def adapter_name(self) -> str:
        return TECHNICAL_ADAPTERS.get(
            self.version[:2],
            "UnsupportedBlenderAdapter",
        )

    @property
    def diagnostic(self) -> str | None:
        actual = ".".join(str(part) for part in self.version)
        if not self.supported:
            return (
                "MIKU_BLENDER_VERSION_UNSUPPORTED:"
                f"actual={actual}:supported=>=5.0.0,<5.3.0"
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
            "actual=<unknown>:supported=>=5.0.0,<5.3.0"
        ) from exc
    if len(parts) < 3 or any(part < 0 for part in parts[:3]):
        raise RuntimeError(
            "MIKU_BLENDER_VERSION_UNSUPPORTED:"
            "actual=<unknown>:supported=>=5.0.0,<5.3.0"
        )
    return parts[:3]


def classify_blender_version(value: Iterable[Any]) -> BlenderCompatibility:
    version = normalize_blender_version(value)
    supported = MIN_BLENDER_VERSION <= version < MAX_BLENDER_VERSION_EXCLUSIVE
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


def require_blender_capabilities(bpy_module: Any) -> BlenderCompatibility:
    """Fail before export when a nominally supported runtime lacks required APIs."""

    compatibility = require_supported_blender(
        getattr(getattr(bpy_module, "app", None), "version", ())
    )
    missing: list[str] = []
    bpy_types = getattr(bpy_module, "types", None)
    for name in _REQUIRED_BPY_TYPES:
        if bpy_types is None or not hasattr(bpy_types, name):
            missing.append("bpy.types." + name)
    bpy_props = getattr(bpy_module, "props", None)
    for name in _REQUIRED_BPY_PROPERTIES:
        if bpy_props is None or not hasattr(bpy_props, name):
            missing.append("bpy.props." + name)
    translations = getattr(getattr(bpy_module, "app", None), "translations", None)
    for name in ("register", "unregister"):
        if translations is None or not hasattr(translations, name):
            missing.append("bpy.app.translations." + name)
    if missing:
        actual = ".".join(str(part) for part in compatibility.version)
        raise RuntimeError(
            "MIKU_BLENDER_CAPABILITY_MISSING:"
            f"version={actual}:adapter={compatibility.adapter_name}:"
            f"capabilities={','.join(sorted(missing))}"
        )
    return compatibility


def blender_targa_png_strategy(value: Iterable[Any]) -> str:
    compatibility = require_supported_blender(value)
    strategy = TARGA_PNG_STRATEGIES.get(compatibility.version[:2])
    if strategy is None:
        raise RuntimeError(
            "MIKU_BLENDER_CAPABILITY_MISSING:"
            f"version={'.'.join(map(str, compatibility.version))}:"
            f"adapter={compatibility.adapter_name}:capabilities=imbuf.png"
        )
    return strategy


def encode_imbuf_png(
    imbuf_module: Any,
    buffer: Any,
    blender_version: Iterable[Any],
) -> bytes:
    """Encode PNG through the explicit API available on each Blender line."""

    compatibility = require_supported_blender(blender_version)
    strategy = blender_targa_png_strategy(compatibility.version)
    if strategy != "MemoryBuffer" or (
        not hasattr(buffer, "file_type")
        or not hasattr(buffer, "compress")
        or not hasattr(imbuf_module, "write_to_buffer")
    ):
        raise RuntimeError(
            "MIKU_BLENDER_CAPABILITY_MISSING:"
            f"version={'.'.join(map(str, compatibility.version))}:"
            f"adapter={compatibility.adapter_name}:"
            "capabilities=imbuf.write_to_buffer"
        )
    buffer.file_type = "PNG"
    buffer.compress = 15
    destination = BytesIO()
    imbuf_module.write_to_buffer(buffer, destination)
    return destination.getvalue()


def encode_targa_png(
    bpy_module: Any,
    imbuf_module: Any,
    source_data: bytes,
    blender_version: Iterable[Any],
) -> bytes:
    """Transcode TARGA bytes without mutating the source image datablock."""

    compatibility = require_supported_blender(blender_version)
    strategy = blender_targa_png_strategy(compatibility.version)
    if strategy == "MemoryBuffer":
        if not hasattr(imbuf_module, "load_from_buffer"):
            raise RuntimeError(
                "MIKU_BLENDER_CAPABILITY_MISSING:"
                f"version={'.'.join(map(str, compatibility.version))}:"
                f"adapter={compatibility.adapter_name}:"
                "capabilities=imbuf.load_from_buffer"
            )
        buffer = imbuf_module.load_from_buffer(source_data)
        try:
            return encode_imbuf_png(
                imbuf_module,
                buffer,
                compatibility.version,
            )
        finally:
            buffer.free()

    images = getattr(getattr(bpy_module, "data", None), "images", None)
    if images is None or not hasattr(images, "load") or not hasattr(images, "remove"):
        raise RuntimeError(
            "MIKU_BLENDER_CAPABILITY_MISSING:"
            f"version={'.'.join(map(str, compatibility.version))}:"
            f"adapter={compatibility.adapter_name}:"
            "capabilities=bpy.data.images.load,bpy.data.images.remove"
        )
    temporary_image = None
    with TemporaryDirectory(prefix="miku-image-png-") as temporary:
        source = Path(temporary) / "source.tga"
        destination = Path(temporary) / "transcoded.png"
        source.write_bytes(source_data)
        try:
            temporary_image = images.load(str(source), check_existing=False)
            # Blender 5.0/5.1 loads file pixels lazily. Force the TARGA data
            # into memory before repointing the temporary datablock at PNG.
            _ = temporary_image.pixels[0]
            temporary_image.filepath_raw = str(destination)
            temporary_image.file_format = "PNG"
            temporary_image.save()
            if not destination.is_file():
                return b""
            return destination.read_bytes()
        finally:
            if temporary_image is not None:
                images.remove(temporary_image)


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
