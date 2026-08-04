"""Build the single Miku Blender 5.2 extension deterministically."""

from __future__ import annotations

import hashlib
import tomllib
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "extensions" / "miku_shader_converter"
with (SOURCE / "blender_manifest.toml").open("rb") as manifest_file:
    VERSION = str(tomllib.load(manifest_file)["version"])
OUTPUT = ROOT / "dist" / f"miku_shader_converter-{VERSION}.zip"


def _extension_files() -> dict[Path, str]:
    files = {
        SOURCE / "__init__.py": "__init__.py",
        SOURCE / "blender_manifest.toml": "blender_manifest.toml",
        SOURCE / "README.md": "README.md",
        ROOT / "LICENSES" / "GPL-3.0-or-later.txt": "LICENSE.txt",
        ROOT / "LICENSE": "LICENSE-MIT-ORIGIN.txt",
        ROOT / "THIRD_PARTY_NOTICES.md": "THIRD_PARTY_NOTICES.md",
    }
    for source in (SOURCE / "bake_worker").rglob("*.py"):
        files[source] = source.relative_to(SOURCE).as_posix()
    for package_name in ("miku", "miku_blender"):
        package_root = ROOT / package_name
        for source in package_root.rglob("*.py"):
            if "__pycache__" in source.parts:
                continue
            relative = source.relative_to(ROOT).as_posix()
            files[source] = relative
    return files


def _write_deterministic_zip(files: dict[Path, str], output: Path) -> Path:
    missing = [str(path) for path in files if not path.is_file()]
    if missing:
        raise FileNotFoundError("Missing extension sources: " + ", ".join(missing))
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".tmp")
    with zipfile.ZipFile(
        temporary,
        "w",
        compression=zipfile.ZIP_DEFLATED,
        compresslevel=9,
    ) as archive:
        for source, relative in sorted(files.items(), key=lambda item: item[1]):
            info = zipfile.ZipInfo(relative, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, source.read_bytes())
    temporary.replace(output)
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    output.with_suffix(output.suffix + ".sha256").write_text(
        f"{digest}  {output.name}\n",
        encoding="ascii",
    )
    return output


def build(output: Path | None = None) -> Path:
    return _write_deterministic_zip(_extension_files(), output or OUTPUT)


if __name__ == "__main__":
    print(build())
