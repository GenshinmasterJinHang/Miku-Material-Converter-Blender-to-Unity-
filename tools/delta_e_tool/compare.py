"""Compare a URP render to a Cycles / Blender reference PNG via ΔE2000."""
from __future__ import annotations

import argparse
from collections import deque
import json
import pathlib

import numpy as np
from PIL import Image

from tools.delta_e_tool import cie_lab


_THRESHOLDS = (1.0, 3.0, 5.0, 8.0)
_MISSING_ALPHA_PENALTY = 100.0
_BRIGHT_BG_THRESHOLD = 240.0 / 255.0


def load_rgb(path: pathlib.Path) -> np.ndarray:
    img = Image.open(path).convert("RGB")
    arr = np.asarray(img, dtype=np.float64) / 255.0
    return arr


def load_rgba(path: pathlib.Path) -> np.ndarray:
    img = Image.open(path).convert("RGBA")
    return np.asarray(img, dtype=np.float64) / 255.0


def foreground_mask(rgba: np.ndarray, alpha_threshold: float = 0.0) -> np.ndarray:
    """Build a foreground mask from alpha or an edge-connected neutral backdrop.

    Blender viewport references in this project use an opaque grey gradient,
    so a fixed near-white cutoff treats the complete canvas as foreground.
    Flooding only neutral, sufficiently bright pixels connected to the image
    border removes that gradient while retaining disconnected white clothing.
    """
    alpha = rgba[..., 3]
    if float(np.max(alpha) - np.min(alpha)) > 1e-6:
        return alpha > alpha_threshold
    rgb = rgba[..., :3]
    chroma = np.max(rgb, axis=-1) - np.min(rgb, axis=-1)
    luminance = np.sum(rgb * np.array([0.2126, 0.7152, 0.0722]), axis=-1)
    candidate = (chroma < 0.045) & (luminance > 0.35)
    height, width = candidate.shape
    background = np.zeros_like(candidate)
    queue = deque()

    def seed(y: int, x: int) -> None:
        if candidate[y, x] and not background[y, x]:
            background[y, x] = True
            queue.append((y, x))

    for x in range(width):
        seed(0, x)
        seed(height - 1, x)
    for y in range(height):
        seed(y, 0)
        seed(y, width - 1)

    while queue:
        y, x = queue.popleft()
        if y > 0:
            seed(y - 1, x)
        if y + 1 < height:
            seed(y + 1, x)
        if x > 0:
            seed(y, x - 1)
        if x + 1 < width:
            seed(y, x + 1)

    if np.any(background):
        return ~background

    # Legacy fallback for small synthetic or pure-white images.
    bright = np.all(rgb >= _BRIGHT_BG_THRESHOLD, axis=-1)
    return ~bright


def bbox_from_mask(mask: np.ndarray, pad: int = 2):
    ys, xs = np.where(mask)
    if ys.size == 0:
        raise ValueError("foreground mask is empty")
    y0 = max(int(ys.min()) - pad, 0)
    y1 = min(int(ys.max()) + pad, mask.shape[0] - 1)
    x0 = max(int(xs.min()) - pad, 0)
    x1 = min(int(xs.max()) + pad, mask.shape[1] - 1)
    return y0, y1, x0, x1


def align_rgba_to_reference(
    urp_rgba: np.ndarray,
    cycles_rgba: np.ndarray,
    alpha_threshold: float = 0.0,
) -> tuple:
    """Crop + scale URP foreground onto the reference canvas by silhouette bbox.

    Returns (aligned_urp_rgba, cycles_rgba, compare_mask) all HxWx4 / HxW.
    Missing URP coverage inside the reference silhouette remains transparent so
    alpha-deficit penalties still apply.
    """
    ref_mask = foreground_mask(cycles_rgba, alpha_threshold)
    urp_mask = foreground_mask(urp_rgba, alpha_threshold)
    if not np.any(ref_mask):
        raise ValueError("reference foreground mask is empty")
    if not np.any(urp_mask):
        raise ValueError("URP foreground mask is empty")

    ry0, ry1, rx0, rx1 = bbox_from_mask(ref_mask)
    uy0, uy1, ux0, ux1 = bbox_from_mask(urp_mask)

    ref_h = ry1 - ry0 + 1
    ref_w = rx1 - rx0 + 1
    urp_crop = (urp_rgba[uy0 : uy1 + 1, ux0 : ux1 + 1] * 255.0).astype(np.uint8)
    urp_img = Image.fromarray(urp_crop, mode="RGBA")
    # Preserve aspect: fit inside reference bbox.
    scale = min(ref_w / urp_img.width, ref_h / urp_img.height)
    new_w = max(1, int(round(urp_img.width * scale)))
    new_h = max(1, int(round(urp_img.height * scale)))
    urp_resized = urp_img.resize((new_w, new_h), Image.NEAREST)

    canvas = Image.new("RGBA", (cycles_rgba.shape[1], cycles_rgba.shape[0]), (0, 0, 0, 0))
    paste_x = rx0 + (ref_w - new_w) // 2
    paste_y = ry0 + (ref_h - new_h) // 2
    canvas.paste(urp_resized, (paste_x, paste_y), urp_resized)
    aligned = np.asarray(canvas, dtype=np.float64) / 255.0
    return aligned, cycles_rgba, ref_mask


def percentile_summary(de: np.ndarray) -> dict:
    flat = de.reshape(-1)
    out = {
        "num_pixels": int(flat.size),
        "mean": float(np.mean(flat)),
        "p50":  float(np.percentile(flat, 50)),
        "p95":  float(np.percentile(flat, 95)),
        "p99":  float(np.percentile(flat, 99)),
        "max":  float(np.max(flat)),
    }
    for thr in _THRESHOLDS:
        out[f"pixels_above_{thr:.1f}"] = int(np.sum(flat > thr))
        out[f"pct_above_{thr:.1f}"] = float(out[f"pixels_above_{thr:.1f}"]) / flat.size
    return out


def make_heatmap(de: np.ndarray) -> np.ndarray:
    """Map ΔE values to a 3-channel RGB heatmap (0..1)."""
    norm = np.clip(de / 8.0, 0.0, 1.0)  # 0..8 -> 0..1
    # Yellow (low) -> red (high): lerp yellow to red.
    yellow = np.array([1.0, 1.0, 0.0])
    red    = np.array([1.0, 0.0, 0.0])
    return (yellow * (1.0 - norm[..., None]) + red * norm[..., None])


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--urp",    required=True, type=pathlib.Path, help="URP-rendered PNG path")
    parser.add_argument("--cycles", required=True, type=pathlib.Path, help="Cycles reference PNG path")
    parser.add_argument("--out-heatmap", type=pathlib.Path, default=None, help="Where to save the heatmap PNG")
    parser.add_argument("--out-json",    type=pathlib.Path, default=None, help="Where to save the JSON summary")
    parser.add_argument(
        "--alpha-mask",
        action="store_true",
        help="Only compare pixels where the reference foreground mask is nonzero",
    )
    parser.add_argument(
        "--align",
        action="store_true",
        help="Align URP silhouette onto the reference canvas before comparison",
    )
    parser.add_argument(
        "--alpha-threshold",
        type=float,
        default=0.0,
        help="Minimum Cycles alpha included by --alpha-mask (0..1, default 0)",
    )
    args = parser.parse_args(argv)

    if not 0.0 <= args.alpha_threshold <= 1.0:
        parser.error("--alpha-threshold must be between 0 and 1")

    urp_rgba = load_rgba(args.urp)
    cycles_rgba = load_rgba(args.cycles)

    mask = None
    if args.align:
        urp_rgba, cycles_rgba, mask = align_rgba_to_reference(
            urp_rgba, cycles_rgba, alpha_threshold=args.alpha_threshold
        )
        # Align implies foreground-only comparison.
        args.alpha_mask = True
    elif args.alpha_mask:
        mask = foreground_mask(cycles_rgba, args.alpha_threshold)

    a = urp_rgba[..., :3]
    b = cycles_rgba[..., :3]
    if a.shape != b.shape:
        raise SystemExit(f"size mismatch: URP={a.shape} vs Cycles={b.shape}")

    la = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(a))
    lb = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(b))
    de = cie_lab.delta_e_2000(la, lb)

    compared = de
    if args.alpha_mask:
        if mask is None:
            mask = foreground_mask(cycles_rgba, args.alpha_threshold)
        if not np.any(mask):
            raise SystemExit("alpha mask contains no comparable pixels")
        # Penalize missing URP coverage inside the reference silhouette.
        # When the reference is opaque (viewport shot), synthesize coverage from
        # the bright-background inverted mask as alpha=1.
        ref_alpha = cycles_rgba[..., 3]
        if float(np.max(ref_alpha) - np.min(ref_alpha)) <= 1e-6:
            ref_alpha = mask.astype(np.float64)
        alpha_deficit = np.clip(ref_alpha - urp_rgba[..., 3], 0.0, 1.0)
        de = np.maximum(de, alpha_deficit * _MISSING_ALPHA_PENALTY)
        compared = de[mask]

    summary = percentile_summary(compared)
    if args.align:
        summary["mask"] = "aligned_foreground"
    elif args.alpha_mask:
        summary["mask"] = "cycles_alpha"
    else:
        summary["mask"] = "none"
    summary["urp_path"]    = str(args.urp)
    summary["cycles_path"] = str(args.cycles)

    if args.out_heatmap:
        args.out_heatmap.parent.mkdir(parents=True, exist_ok=True)
        heatmap_rgb = (make_heatmap(de) * 255.0).astype(np.uint8)
        if mask is None:
            Image.fromarray(heatmap_rgb, mode="RGB").save(args.out_heatmap)
        else:
            heatmap_alpha = (mask.astype(np.uint8) * 255)[..., None]
            heatmap = np.concatenate((heatmap_rgb, heatmap_alpha), axis=2)
            Image.fromarray(heatmap, mode="RGBA").save(args.out_heatmap)

    out_json = json.dumps(summary, indent=2, ensure_ascii=False)
    if args.out_json:
        args.out_json.parent.mkdir(parents=True, exist_ok=True)
        args.out_json.write_text(out_json + "\n", encoding="utf-8")
    print(out_json)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
