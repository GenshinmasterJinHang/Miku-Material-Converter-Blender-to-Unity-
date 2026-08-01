"""Color-space conversions (sRGB <-> linear <-> CIE Lab) and CIEDE2000.

All inputs are numpy arrays of shape (..., 3) with channels in the last dim.
"""
from __future__ import annotations

import numpy as np


# D65 reference white in linear RGB.
_D65_REF = np.array([0.95047, 1.00000, 1.08883], dtype=np.float64)


def srgb_to_linear(srgb: np.ndarray) -> np.ndarray:
    """Convert sRGB [0,1] to linear RGB. Vectorized."""
    a = 0.055
    return np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + a) / (1 + a)) ** 2.4)


def linear_to_lab(linear: np.ndarray) -> np.ndarray:
    """Convert linear RGB to CIE Lab (D65). Vectorized.

    Uses the standard sRGB->XYZ matrix then XYZ->Lab with D65 white point.
    """
    # sRGB primaries matrix (linear RGB -> XYZ).
    M = np.array([
        [0.4124564, 0.3575761, 0.1804375],
        [0.2126729, 0.7151522, 0.0721750],
        [0.0193339, 0.1191920, 0.9503041],
    ], dtype=np.float64)

    last = linear.shape[-1]
    flat = linear.reshape(-1, last)
    xyz = flat @ M.T
    # Normalize by D65 white.
    xyz_n = xyz / _D65_REF
    # CIE Lab forward.
    eps = (6.0 / 29.0) ** 3
    kappa = 903.3  # (29/3)^3 roughly

    f = np.where(xyz_n > eps, np.cbrt(xyz_n), (kappa * xyz_n + 16.0) / 116.0)

    L = 116.0 * f[..., 1] - 16.0
    a = 500.0 * (f[..., 0] - f[..., 1])
    b = 200.0 * (f[..., 1] - f[..., 2])
    return np.stack([L, a, b], axis=-1).reshape(linear.shape)


def delta_e_2000(lab_a: np.ndarray, lab_b: np.ndarray) -> np.ndarray:
    """Per-pixel CIEDE2000 difference. Vectorized.

    Implementation based on Sharma et al. (2005), as recommended by ITU-R BT.2247.
    """
    L1, a1, b1 = lab_a[..., 0], lab_a[..., 1], lab_a[..., 2]
    L2, a2, b2 = lab_b[..., 0], lab_b[..., 1], lab_b[..., 2]

    C1 = np.sqrt(a1 * a1 + b1 * b1)
    C2 = np.sqrt(a2 * a2 + b2 * b2)
    Cbar = (C1 + C2) / 2.0

    G = 0.5 * (1.0 - np.sqrt((Cbar ** 7) / (Cbar ** 7 + 25.0 ** 7)))
    a1p = (1.0 + G) * a1
    a2p = (1.0 + G) * a2

    C1p = np.sqrt(a1p * a1p + b1 * b1)
    C2p = np.sqrt(a2p * a2p + b2 * b2)

    h1p = (np.degrees(np.arctan2(b1, a1p)) + 360.0) % 360.0
    h2p = (np.degrees(np.arctan2(b2, a2p)) + 360.0) % 360.0

    dLp = L2 - L1
    dCp = C2p - C1p

    dhp = np.where(np.abs(h2p - h1p) <= 180.0, h2p - h1p,
                   np.where(h2p <= h1p, h2p - h1p + 360.0, h2p - h1p - 360.0))
    dHp = 2.0 * np.sqrt(C1p * C2p) * np.sin(np.radians(dhp / 2.0))

    Lbarp = (L1 + L2) / 2.0
    Cbarp = (C1p + C2p) / 2.0

    hsum = h1p + h2p
    hbarp = np.where(np.abs(h1p - h2p) <= 180.0, hsum / 2.0,
                     np.where(hsum < 360.0, (hsum + 360.0) / 2.0, (hsum - 360.0) / 2.0))

    T = (1.0
         - 0.17 * np.cos(np.radians(hbarp - 30.0))
         + 0.24 * np.cos(np.radians(2.0 * hbarp))
         + 0.32 * np.cos(np.radians(3.0 * hbarp + 6.0))
         - 0.20 * np.cos(np.radians(4.0 * hbarp - 63.0)))

    dTheta = 30.0 * np.exp(-(((hbarp - 275.0) / 25.0) ** 2))
    Rc = 2.0 * np.sqrt((Cbarp ** 7) / (Cbarp ** 7 + 25.0 ** 7))
    Sl = 1.0 + (0.015 * ((Lbarp - 50.0) ** 2)) / np.sqrt(20.0 + (Lbarp - 50.0) ** 2)
    Sc = 1.0 + 0.045 * Cbarp
    Sh = 1.0 + 0.015 * Cbarp * T
    Rt = -np.sin(np.radians(2.0 * dTheta)) * Rc

    dE = np.sqrt(
        (dLp / Sl) ** 2
        + (dCp / Sc) ** 2
        + (dHp / Sh) ** 2
        + Rt * (dCp / Sc) * (dHp / Sh)
    )
    return dE
