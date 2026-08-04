"""Build a deterministic Unity Package Manager .tgz for Miku."""

from __future__ import annotations

import gzip
import io
import json
import tarfile
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "unity" / "Packages" / "com.miku.shaderconverter"
VERSION = str(
    json.loads((PACKAGE / "package.json").read_text(encoding="utf-8"))["version"]
)
OUTPUT = ROOT / "dist" / f"com.miku.shaderconverter-{VERSION}.tgz"


def validate_declared_samples(package: Path = PACKAGE) -> tuple[Path, ...]:
    """Return package sample files after rejecting missing or empty declarations."""
    manifest = json.loads((package / "package.json").read_text(encoding="utf-8"))
    samples = manifest.get("samples", [])
    if not isinstance(samples, list):
        raise ValueError("package.json 'samples' must be an array")

    declared_files: list[Path] = []
    for index, sample in enumerate(samples):
        if not isinstance(sample, dict):
            raise ValueError(f"package.json samples[{index}] must be an object")
        declared_path = sample.get("path")
        if not isinstance(declared_path, str) or not declared_path:
            raise ValueError(f"package.json samples[{index}].path must be a string")
        if (
            "\\" in declared_path
            or declared_path.startswith("/")
            or any(part in {"", ".", ".."} for part in declared_path.split("/"))
        ):
            raise ValueError(
                f"package.json samples[{index}].path is not a safe package-relative path: "
                f"{declared_path!r}"
            )

        relative = PurePosixPath(declared_path)
        sample_root = package.joinpath(*relative.parts)
        if not sample_root.is_dir():
            raise ValueError(
                f"declared Unity sample directory does not exist: {declared_path}"
            )
        files = sorted(
            (item for item in sample_root.rglob("*") if item.is_file()),
            key=lambda item: item.relative_to(package).as_posix(),
        )
        if not files:
            raise ValueError(f"declared Unity sample directory is empty: {declared_path}")
        declared_files.extend(files)
    return tuple(declared_files)


def build(output: Path | None = None) -> Path:
    validate_declared_samples()
    output = output or OUTPUT
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = io.BytesIO()
    with tarfile.open(fileobj=payload, mode="w", format=tarfile.PAX_FORMAT) as archive:
        for path in sorted(
            (item for item in PACKAGE.rglob("*") if item.is_file()),
            key=lambda item: item.relative_to(PACKAGE).as_posix(),
        ):
            relative = "package/" + path.relative_to(PACKAGE).as_posix()
            data = path.read_bytes()
            if path.suffix == ".meta":
                # Keep deterministic Unity metadata readable by tools whose
                # anchored YAML patterns do not treat CRLF as a line ending.
                data = data.replace(b"\r\n", b"\n")
            info = tarfile.TarInfo(relative)
            info.size = len(data)
            info.mode = 0o644
            info.mtime = 0
            info.uid = 0
            info.gid = 0
            info.uname = ""
            info.gname = ""
            archive.addfile(info, io.BytesIO(data))
    temporary = output.with_suffix(output.suffix + ".tmp")
    with temporary.open("wb") as raw:
        with gzip.GzipFile(
            filename="",
            mode="wb",
            fileobj=raw,
            mtime=0,
            compresslevel=9,
        ) as compressed:
            compressed.write(payload.getvalue())
    temporary.replace(output)
    return output


if __name__ == "__main__":
    print(build())
