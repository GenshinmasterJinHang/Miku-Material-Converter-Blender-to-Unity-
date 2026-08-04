#!/usr/bin/env python3
"""Build the reproducible Miku 2.2.9 release candidates."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import tomllib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from tools import build_miku_blender_extensions, build_miku_unity_package  # noqa: E402


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()
    output_dir = args.output_dir.resolve()
    if output_dir == ROOT:
        raise SystemExit("MIKU_RELEASE_OUTPUT_UNSAFE")
    output_dir.mkdir(parents=True, exist_ok=True)

    blender_version = str(
        tomllib.loads(
            (ROOT / "extensions/miku_shader_converter/blender_manifest.toml")
            .read_text(encoding="utf-8")
        )["version"]
    )
    unity_version = str(
        json.loads(
            (ROOT / "unity/Packages/com.miku.shaderconverter/package.json")
            .read_text(encoding="utf-8")
        )["version"]
    )
    if blender_version != "2.2.9" or unity_version != "2.2.9":
        raise SystemExit(
            "MIKU_RELEASE_VERSION_MISMATCH:"
            f"blender={blender_version}:unity={unity_version}"
        )

    artifacts = [
        build_miku_blender_extensions.build(
            output_dir / f"miku_shader_converter-{blender_version}.zip"
        ),
        build_miku_unity_package.build(
            output_dir / f"com.miku.shaderconverter-{unity_version}.tgz"
        ),
    ]
    # The component builders keep local `.sha256` sidecars for their legacy
    # `dist/` workflow. A GitHub Release publishes one canonical manifest only.
    for artifact in artifacts:
        artifact.with_suffix(artifact.suffix + ".sha256").unlink(missing_ok=True)
    manifest = output_dir / "SHA256SUMS.txt"
    lines = [f"{sha256(path)}  {path.name}" for path in sorted(artifacts)]
    manifest.write_text("\n".join(lines) + "\n", encoding="ascii")
    print("Built release artifacts:")
    for path in artifacts:
        print(f"- {path} {sha256(path)}")
    print(f"- {manifest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
