"""Build a deterministic Unity Package Manager .tgz for Miku."""

from __future__ import annotations

import gzip
import io
import json
import tarfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "unity" / "Packages" / "com.miku.shaderconverter"
VERSION = str(
    json.loads((PACKAGE / "package.json").read_text(encoding="utf-8"))["version"]
)
OUTPUT = ROOT / "dist" / f"com.miku.shaderconverter-{VERSION}.tgz"


def build() -> Path:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    payload = io.BytesIO()
    with tarfile.open(fileobj=payload, mode="w", format=tarfile.PAX_FORMAT) as archive:
        for path in sorted(
            (item for item in PACKAGE.rglob("*") if item.is_file()),
            key=lambda item: item.relative_to(PACKAGE).as_posix(),
        ):
            relative = "package/" + path.relative_to(PACKAGE).as_posix()
            data = path.read_bytes()
            info = tarfile.TarInfo(relative)
            info.size = len(data)
            info.mode = 0o644
            info.mtime = 0
            info.uid = 0
            info.gid = 0
            info.uname = ""
            info.gname = ""
            archive.addfile(info, io.BytesIO(data))
    temporary = OUTPUT.with_suffix(OUTPUT.suffix + ".tmp")
    with temporary.open("wb") as raw:
        with gzip.GzipFile(
            filename="",
            mode="wb",
            fileobj=raw,
            mtime=0,
            compresslevel=9,
        ) as compressed:
            compressed.write(payload.getvalue())
    temporary.replace(OUTPUT)
    return OUTPUT


if __name__ == "__main__":
    print(build())
