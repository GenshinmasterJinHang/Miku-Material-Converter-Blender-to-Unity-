"""Tests for color-space conversions and CIEDE2000 (converted from pytest to unittest)."""
from __future__ import annotations

import sys
import pathlib
import unittest

import numpy as np

# Make `tools/` importable as a top-level package; the project's `tools.delta_e_tool`
# lives at the repo root, but `tests/` is at the same level, so the parent dir must
# be on sys.path.
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from tools.delta_e_tool import cie_lab


class CieLabTests(unittest.TestCase):
    def test_srgb_to_linear_zero_one(self):
        srgb = np.array([[0.0, 0.5, 1.0]])
        lin = cie_lab.srgb_to_linear(srgb)
        np.testing.assert_allclose(lin[..., 0], [0.0], atol=1e-6)
        np.testing.assert_allclose(lin[..., 1], [0.21404], atol=1e-4)
        np.testing.assert_allclose(lin[..., 2], [1.0], atol=1e-6)

    def test_round_trip_srgb_linear_lab_zero_delta(self):
        """A pixel vs itself must give ΔE2000 = 0."""
        rgb = np.array([[0.5, 0.3, 0.7]])
        lab = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(rgb))
        de = cie_lab.delta_e_2000(lab, lab)
        np.testing.assert_allclose(de, [0.0], atol=1e-9)

    def test_delta_e_known_reference_value(self):
        """Two known Lab pairs from the Sharma (2005) reference table."""
        # Sample from the Sharma 2005 paper, Table 1 row 1:
        # Lab1 = (50.0000, 2.6772, -79.7751)
        # Lab2 = (50.0000, 0.0000, -82.7485) -> expected dE = 2.0425
        lab1 = np.array([[50.0000, 2.6772, -79.7751]])
        lab2 = np.array([[50.0000, 0.0000, -82.7485]])
        de = cie_lab.delta_e_2000(lab1, lab2)
        np.testing.assert_allclose(de, [2.0425], atol=0.05)

    def test_delta_e_image_shape_preserved(self):
        rng = np.random.default_rng(seed=0)
        a = rng.random((32, 32, 3))
        b = rng.random((32, 32, 3))
        lab_a = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(a))
        lab_b = cie_lab.linear_to_lab(cie_lab.srgb_to_linear(b))
        de = cie_lab.delta_e_2000(lab_a, lab_b)
        self.assertEqual(de.shape, (32, 32))
        self.assertTrue((de >= 0).all())


if __name__ == "__main__":
    unittest.main()
