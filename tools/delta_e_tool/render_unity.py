"""Drive Unity Editor over MCP to render each material to a PNG.

This is a placeholder/stub. The actual Unity MCP tool names are owned by the
`unity-mcp-skill` companion plugin once installed; until then this script emits
instructions for a manual hand-off.
"""
from __future__ import annotations

import argparse
import asyncio
import pathlib
import sys

try:
    from mcp import ClientSession  # type: ignore
    HAS_MCP = True
except Exception:
    HAS_MCP = False


async def _render_one(session, material: str, out_path: pathlib.Path, size=(512, 512)) -> None:
    raise NotImplementedError(
        "Hook this up to the actual unity-mcp-skill tools. "
        "See docs/superpowers/plans/2026-07-20-b2u-npr-shader-architecture.md for the contract."
    )


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--materials", nargs="+", required=True)
    parser.add_argument("--out-dir", type=pathlib.Path, required=True)
    parser.add_argument("--size", default="512x512")
    args = parser.parse_args(argv)

    if not HAS_MCP:
        print(
            "Unity MCP Python client not available. Render via unity-mcp-skill manually:\n"
            "  1. Spawn a sphere with the material\n"
            "  2. Camera -> RenderTexture\n"
            "  3. Save PNG to " + str(args.out_dir),
            file=sys.stderr,
        )
        return 2

    args.out_dir.mkdir(parents=True, exist_ok=True)
    size = tuple(map(int, args.size.split("x")))

    async def _run():
        async with ClientSession() as session:
            for mat in args.materials:
                out = args.out_dir / f"{mat}.png"
                await _render_one(session, mat, out, size=size)

    asyncio.run(_run())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
