"""Small secure CLI for planning and validating JSON source snapshots."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from .contracts import canonical_hash, validate_document
from .planner import plan_graph


def _safe_output(root: Path, relative: str) -> Path:
    root = root.resolve()
    destination = (root / relative).resolve()
    if destination != root and root not in destination.parents:
        raise ValueError("Output path escapes the selected output root")
    return destination


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="miku")
    sub = parser.add_subparsers(dest="command", required=True)
    convert = sub.add_parser("convert")
    convert.add_argument("--source", required=True)
    convert.add_argument("--output", required=True)
    convert.add_argument("--mode", default="Auto")
    convert.add_argument(
        "--fidelity-policy",
        choices=("AllowDeclaredApproximation", "Strict"),
        default="AllowDeclaredApproximation",
    )
    convert.add_argument(
        "--add-shader-energy",
        choices=(
            "PreserveBlender",
            "EnergyConservingApproximation",
            "ClampForRealtimeSafety",
        ),
        default="PreserveBlender",
    )
    verify = sub.add_parser("verify")
    verify.add_argument("document")
    args = parser.parse_args(argv)
    if args.command == "verify":
        document = json.loads(Path(args.document).read_text(encoding="utf-8"))
        validate_document(document)
        print(json.dumps({"valid": True, "canonicalHash": canonical_hash({k: v for k, v in document.items() if k != "canonicalHash"})}, ensure_ascii=False))
        return 0
    source = Path(args.source).resolve()
    if source.suffix.lower() != ".json":
        raise SystemExit("MIKU_SOURCE_REQUIRES_SNAPSHOT: use miku_blender for .blend input")
    graph = json.loads(source.read_text(encoding="utf-8"))
    material_key = str((graph.get("material") or {}).get("name") or source.stem)
    ir, plan = plan_graph(
        graph,
        material_key=material_key,
        mode=args.mode,
        fidelity_policy=args.fidelity_policy,
        add_shader_energy_policy=args.add_shader_energy,
    )
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=True)
    _safe_output(out, f"{material_key}.miku-ir.json").write_text(json.dumps(ir, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    _safe_output(out, f"{material_key}.miku-plan.json").write_text(json.dumps(plan, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"material": material_key, "ir": ir["canonicalHash"], "plan": plan["canonicalHash"]}, ensure_ascii=False))
    return 0
