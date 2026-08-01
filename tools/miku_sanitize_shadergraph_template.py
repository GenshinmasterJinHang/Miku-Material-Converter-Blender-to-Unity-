"""Remove project-local/GPL file dependencies from the MIT Sub Graph template."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TEMPLATE = (
    ROOT
    / "unity"
    / "Packages"
    / "com.miku.shaderconverter"
    / "Templates"
    / "MikuStandardTemplate.generated.shadersubgraph"
)
LEGACY_SOURCE_GUID = "0e9f9e7c9e2d4c15a8e8f0d11b4ac11d"
INLINE_BODY = "Factor = 0.0; Color = float4(0.0, 0.0, 0.0, 1.0);"


def transform(text: str) -> tuple[str, int]:
    decoder = json.JSONDecoder()
    offset = 0
    replacements = []
    while offset < len(text):
        while offset < len(text) and text[offset].isspace():
            offset += 1
        if offset >= len(text):
            break
        value, end = decoder.raw_decode(text, offset)
        if (
            isinstance(value, dict)
            and value.get("m_Type") == "UnityEditor.ShaderGraph.CustomFunctionNode"
            and value.get("m_FunctionSource") == LEGACY_SOURCE_GUID
        ):
            block = text[offset:end]
            block = block.replace('"m_SourceType": 0', '"m_SourceType": 1', 1)
            block = block.replace(
                f'"m_FunctionSource": "{LEGACY_SOURCE_GUID}"',
                '"m_FunctionSource": ""',
                1,
            )
            block = block.replace(
                '"m_FunctionBody": "Enter function body here..."',
                '"m_FunctionBody": "' + INLINE_BODY + '"',
                1,
            )
            replacements.append((offset, end, block))
        offset = end
    for start, end, block in reversed(replacements):
        text = text[:start] + block + text[end:]
    return text, len(replacements)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    before = TEMPLATE.read_text(encoding="utf-8")
    after, count = transform(before)
    if args.check:
        if LEGACY_SOURCE_GUID in before or count:
            raise SystemExit("MIKU_TEMPLATE_EXTERNAL_FUNCTION_SOURCE_REMAINS")
        return 0
    if count != 4:
        raise SystemExit(f"MIKU_TEMPLATE_EXPECTED_FOUR_CUSTOM_FUNCTIONS:{count}")
    with TEMPLATE.open("w", encoding="utf-8", newline="\n") as stream:
        stream.write(after)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
