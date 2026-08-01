"""Generate and verify the immutable Unity package asset identity manifest."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path


GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})$", re.MULTILINE)


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _content_sha256(path: Path, relative_asset: str) -> str:
    if relative_asset == "package.json":
        # Unity Package Manager injects a cache-local `_fingerprint` field
        # into an installed tarball. It is not package-authored identity.
        payload = json.loads(path.read_text(encoding="utf-8"))
        payload.pop("_fingerprint", None)
        canonical = json.dumps(
            payload,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        return hashlib.sha256(canonical.encode("utf-8")).hexdigest()
    return _sha256(path)


def _asset_role(path: Path) -> str:
    if path.suffix == "":
        return "Folder"
    suffix = path.suffix.lower()
    return {
        ".asmdef": "AssemblyDefinition",
        ".cs": "CSharp",
        ".hlsl": "Hlsl",
        ".json": "Json",
        ".md": "Documentation",
        ".shader": "Shader",
        ".shadergraph": "ShaderGraph",
        ".shadersubgraph": "ShaderSubGraph",
    }.get(suffix, "Asset")


def build_manifest(package_root: Path) -> dict:
    package_root = package_root.resolve()
    for directory in package_root.rglob("*"):
        if not directory.is_dir() or any(part.endswith("~") for part in directory.relative_to(package_root).parts):
            continue
        meta = Path(str(directory) + ".meta")
        if not meta.is_file():
            raise ValueError(
                "MIKU_PACKAGE_FOLDER_META_MISSING: "
                + directory.relative_to(package_root).as_posix()
            )
    for asset in package_root.rglob("*"):
        if (
            not asset.is_file()
            or asset.suffix == ".meta"
            or any(part.endswith("~") for part in asset.relative_to(package_root).parts)
        ):
            continue
        meta = Path(str(asset) + ".meta")
        if not meta.is_file():
            raise ValueError(
                "MIKU_PACKAGE_ASSET_META_MISSING: "
                + asset.relative_to(package_root).as_posix()
            )
    assets = []
    seen: dict[str, str] = {}
    for meta in sorted(package_root.rglob("*.meta"), key=lambda item: item.as_posix().casefold()):
        relative_meta = meta.relative_to(package_root).as_posix()
        match = GUID_RE.search(meta.read_text(encoding="utf-8"))
        if match is None:
            raise ValueError(f"MIKU_PACKAGE_GUID_INVALID: {relative_meta}")
        guid = match.group(1)
        previous = seen.get(guid)
        if previous is not None:
            raise ValueError(f"MIKU_PACKAGE_GUID_DUPLICATE: {guid}: {previous}, {relative_meta}")
        seen[guid] = relative_meta
        asset = meta.with_suffix("")
        relative_asset = asset.relative_to(package_root).as_posix()
        record = {
            "path": relative_asset,
            "metaPath": relative_meta,
            "guid": guid,
            "role": _asset_role(asset),
        }
        if asset.is_file():
            record["contentSha256"] = _content_sha256(asset, relative_asset)
        elif not asset.is_dir():
            raise ValueError(f"MIKU_PACKAGE_ASSET_MISSING: {relative_asset}")
        assets.append(record)
    if not assets:
        raise ValueError("MIKU_PACKAGE_IDENTITY_EMPTY")
    payload = {
        "schema": "miku-package-asset-identity-1.0",
        "package": "com.miku.shaderconverter",
        "assets": assets,
    }
    canonical = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    payload["canonicalHash"] = hashlib.sha256(canonical.encode("utf-8")).hexdigest()
    return payload


def main() -> int:
    parser = argparse.ArgumentParser()
    repo = Path(__file__).resolve().parents[1]
    parser.add_argument(
        "--package-root",
        type=Path,
        default=repo / "unity" / "Packages" / "com.miku.shaderconverter",
    )
    parser.add_argument(
        "--manifest",
        type=Path,
        default=repo / "docs" / "provenance" / "miku-unity-package-asset-identity.json",
    )
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    actual = build_manifest(args.package_root)
    rendered = json.dumps(actual, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if args.check:
        if not args.manifest.is_file() or args.manifest.read_text(encoding="utf-8") != rendered:
            raise SystemExit("MIKU_PACKAGE_IDENTITY_DRIFT")
        return 0
    args.manifest.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.manifest.with_suffix(args.manifest.suffix + ".tmp")
    temporary.write_text(rendered, encoding="utf-8")
    temporary.replace(args.manifest)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
