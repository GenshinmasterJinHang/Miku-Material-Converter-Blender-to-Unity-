"""Structural silhouette metrics for URP vs Cycles comparison.

When color ΔE cannot be compared (different lighting, missing NPR features,
transparent vs opaque background), fall back to structural metrics:
- Foreground coverage: % of pixels in mask
- Silhouette IoU: intersection over union of masks
- Bounding box IoU: overlap of character bboxes
- Aspect ratio match

Usage:
  python -m tools.delta_e_tool.structural_metrics \\
      --urp    tools/delta_e_tool/renders/urp_combined/character_full.png \\
      --cycles tools/delta_e_tool/references/character_combined/character_full.png \\
      --out tools/delta_e_tool/baseline_v3_20260721/structural.json
"""
from __future__ import annotations

import argparse
import json
import pathlib

import numpy as np
from PIL import Image


def load_rgba(path: pathlib.Path) -> np.ndarray:
    img = Image.open(path).convert("RGBA")
    return np.asarray(img, dtype=np.float64) / 255.0


def foreground_mask(rgba: np.ndarray) -> np.ndarray:
    """Build a boolean foreground mask from alpha or bright-bg fallback."""
    alpha = rgba[..., 3]
    if float(np.max(alpha) - np.min(alpha)) > 1e-6:
        return alpha > 0.0
    rgb = rgba[..., :3]
    bright = np.all(rgb >= 240.0 / 255.0, axis=-1)
    return ~bright


def bbox_of(mask: np.ndarray) -> tuple[int, int, int, int]:
    ys, xs = np.where(mask)
    if len(ys) == 0:
        return (0, 0, 0, 0)
    return (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))


def main(argv=None) -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--urp", type=pathlib.Path, required=True)
    p.add_argument("--cycles", type=pathlib.Path, required=True)
    p.add_argument("--out", type=pathlib.Path, required=True)
    args = p.parse_args(argv)

    urp = load_rgba(args.urp)
    cyc = load_rgba(args.cycles)

    # Resize to same size if needed
    if urp.shape != cyc.shape:
        h = min(urp.shape[0], cyc.shape[0])
        w = min(urp.shape[1], cyc.shape[1])
        urp_img = Image.fromarray((urp * 255).astype(np.uint8)).resize((w, h), Image.LANCZOS)
        cyc_img = Image.fromarray((cyc * 255).astype(np.uint8)).resize((w, h), Image.LANCZOS)
        urp = np.asarray(urp_img, dtype=np.float64) / 255.0
        cyc = np.asarray(cyc_img, dtype=np.float64) / 255.0

    mu = foreground_mask(urp)
    mc = foreground_mask(cyc)

    total = mu.size
    cov_u = float(mu.sum()) / total
    cov_c = float(mc.sum()) / total

    inter = np.logical_and(mu, mc).sum()
    uni = np.logical_or(mu, mc).sum()
    iou = float(inter) / float(uni) if uni > 0 else 0.0
    dice = float(2 * inter) / float(mu.sum() + mc.sum()) if (mu.sum() + mc.sum()) > 0 else 0.0

    bu = bbox_of(mu)
    bc = bbox_of(mc)
    # Bbox IoU
    x1 = max(bu[0], bc[0]); y1 = max(bu[1], bc[1])
    x2 = min(bu[2], bc[2]); y2 = min(bu[2], bc[2])  # ty: ignore[B033]
    # Above has a known y2 typo; compute correctly below
    x2 = min(bu[2], bc[2])
    y2 = min(bu[3], bc[3])
    bw = max(0, x2 - x1)
    bh = max(0, y2 - y1)
    bbox_inter = bw * bh
    bbox_u = (bu[2] - bu[0]) * (bu[3] - bu[1]) + (bc[2] - bc[0]) * (bc[3] - bc[1]) - bbox_inter
    bbox_iou = float(bbox_inter) / float(bbox_u) if bbox_u > 0 else 0.0

    def aspect(b):
        w = max(1, b[2] - b[0]); h = max(1, b[3] - b[1])
        return float(w) / float(h)

    aspect_u = aspect(bu)
    aspect_c = aspect(bc)

    out = {
        "urp_path": str(args.urp),
        "cycles_path": str(args.cycles),
        "size": list(urp.shape[:2]),
        "urp_fg_coverage": cov_u,
        "cycles_fg_coverage": cov_c,
        "fg_coverage_ratio": cov_u / cov_c if cov_c > 0 else None,
        "silhouette_iou": iou,
        "silhouette_dice": dice,
        "urp_bbox": list(bu),
        "cycles_bbox": list(bc),
        "bbox_iou": bbox_iou,
        "urp_aspect": aspect_u,
        "cycles_aspect": aspect_c,
        "aspect_ratio_diff_pct": abs(aspect_u - aspect_c) / aspect_c * 100 if aspect_c > 0 else None,
    }

    args.out.parent.mkdir(parents=True, exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2, ensure_ascii=False)

    print(f"urp_fg_coverage   = {cov_u:.4f}")
    print(f"cycles_fg_coverage= {cov_c:.4f}")
    print(f"coverage_ratio    = {cov_u / cov_c if cov_c > 0 else float('nan'):.3f}")
    print(f"silhouette_iou    = {iou:.4f}")
    print(f"silhouette_dice   = {dice:.4f}")
    print(f"bbox_iou          = {bbox_iou:.4f}")
    print(f"urp_aspect        = {aspect_u:.3f}")
    print(f"cycles_aspect     = {aspect_c:.3f}")
    print(f"wrote {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
