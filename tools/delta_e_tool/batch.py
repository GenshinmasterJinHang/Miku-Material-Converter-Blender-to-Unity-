"""Batch ΔE2000 comparison over a directory of paired URP / Cycles PNGs."""
from __future__ import annotations

import argparse
import csv
import json
import pathlib
import subprocess
import sys


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--urp-dir",    type=pathlib.Path)
    parser.add_argument("--cycles-dir", type=pathlib.Path)
    parser.add_argument("--urp",        type=pathlib.Path, help="Single URP PNG")
    parser.add_argument("--cycles",     type=pathlib.Path, help="Single combined Cycles PNG")
    parser.add_argument("--out-dir",    required=True, type=pathlib.Path)
    parser.add_argument("--alpha-mask", action="store_true",
                        help="Forward reference-alpha masking to compare")
    parser.add_argument("--align", action="store_true",
                        help="Align URP silhouette onto the reference canvas before comparison")
    parser.add_argument("--pass-mean", type=float, default=3.0,
                        help="Mean ΔE2000 threshold (pass < threshold). Default 3.0.")
    parser.add_argument("--pass-p99",  type=float, default=8.0,
                        help="p99 ΔE2000 threshold (pass < threshold). Default 8.0.")
    args = parser.parse_args(argv)

    directory_mode = args.urp_dir is not None or args.cycles_dir is not None
    single_mode = args.urp is not None or args.cycles is not None
    if directory_mode == single_mode:
        parser.error("provide either --urp-dir/--cycles-dir or --urp/--cycles")
    if directory_mode and (args.urp_dir is None or args.cycles_dir is None):
        parser.error("--urp-dir and --cycles-dir must be provided together")
    if single_mode and (args.urp is None or args.cycles is None):
        parser.error("--urp and --cycles must be provided together")

    args.out_dir.mkdir(parents=True, exist_ok=True)
    rows = []
    failures = []

    if single_mode:
        pairs = [(args.cycles.stem, args.urp, args.cycles)]
    else:
        pairs = [
            (cycles_png.stem, args.urp_dir / cycles_png.name, cycles_png)
            for cycles_png in sorted(args.cycles_dir.glob("*.png"))
        ]

    for mat, urp_png, cycles_png in pairs:
        if not urp_png.exists():
            print(f"[batch] missing URP render for {mat}, skipping", file=sys.stderr)
            if single_mode:
                failures.append((mat, 1))
            continue

        cmd = [
            sys.executable, "-m", "tools.delta_e_tool.compare",
            "--urp",    str(urp_png),
            "--cycles", str(cycles_png),
            "--out-heatmap", str(args.out_dir / f"{mat}_heatmap.png"),
            "--out-json",    str(args.out_dir / f"{mat}.json"),
        ]
        if args.alpha_mask:
            cmd.append("--alpha-mask")
        if args.align:
            cmd.append("--align")
        ret = subprocess.call(cmd, stdout=subprocess.PIPE)
        if ret != 0:
            failures.append((mat, ret))
            continue

        summary = json.loads((args.out_dir / f"{mat}.json").read_text(encoding="utf-8"))
        rows.append({
            "material":   mat,
            "mean":       summary["mean"],
            "p99":        summary["p99"],
            "max":        summary["max"],
            "pass_mean":  summary["mean"] < args.pass_mean,
            "pass_p99":   summary["p99"]  < args.pass_p99,
        })

    csv_path = args.out_dir / "summary.csv"
    with csv_path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=["material", "mean", "p99", "max", "pass_mean", "pass_p99"])
        writer.writeheader()
        for r in rows:
            writer.writerow(r)

    md_path = args.out_dir / "summary.md"
    md_lines = ["| Material | mean ΔE2000 | p99 | max | Pass (mean<%.1f, p99<%.1f) |" % (args.pass_mean, args.pass_p99),
                "|---|---|---|---|---|"]
    for r in rows:
        ok = "✅" if (r["pass_mean"] and r["pass_p99"]) else "❌"
        md_lines.append(f"| {r['material']} | {r['mean']:.2f} | {r['p99']:.2f} | {r['max']:.2f} | {ok} |")
    md_path.write_text("\n".join(md_lines) + "\n", encoding="utf-8")

    try:
        print(md_path.read_text(encoding="utf-8"))
    except UnicodeEncodeError:
        # Windows console fallback: print an ASCII-only summary.
        ascii_lines = [line.replace("✅", "OK").replace("❌", "FAIL") for line in md_lines]
        for line in ascii_lines:
            print(line.encode("ascii", "replace").decode("ascii"))
    return 1 if failures else (0 if all(r["pass_mean"] and r["pass_p99"] for r in rows) else 1)


if __name__ == "__main__":
    raise SystemExit(main())
