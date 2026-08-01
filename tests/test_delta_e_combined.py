"""Tests for combined-character capture and alpha-masked comparison."""
from __future__ import annotations

import json
import io
import pathlib
import sys
import tempfile
import unittest
from unittest import mock

import numpy as np
from PIL import Image

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from tools.delta_e_tool import batch, capture_cycles_ref, compare


class CombinedCaptureTests(unittest.TestCase):
    def test_blender_argv_turns_python_exceptions_into_failure_exit_code(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            blend = root / "character.blend"
            blend.touch()
            with mock.patch.object(
                capture_cycles_ref.subprocess,
                "call",
                return_value=0,
            ) as call:
                rc = capture_cycles_ref._render_one(
                    "goo",
                    blend,
                    root / "combined.png",
                )

        argv = call.call_args[0][0]
        self.assertEqual(rc, 0)
        self.assertEqual(argv[:6], [
            "goo", "-b", str(blend),
            "--python-exit-code", "1", "--python",
        ])
        self.assertEqual(len(argv), 7)
        self.assertTrue(argv[-1].endswith(".py"))

    def test_default_cli_keeps_legacy_per_material_renders(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            blend = root / "character.blend"
            blend.touch()
            blender = root / "blender.exe"
            blender.touch()
            out_dir = root / "out"
            with mock.patch.object(
                    capture_cycles_ref, "CERTIFIED_BLENDER", str(blender)), \
                    mock.patch.object(capture_cycles_ref, "_render_one", return_value=0) as render:
                rc = capture_cycles_ref.main([
                    "--blender-exe", str(blender),
                    "--blend", str(blend),
                    "--materials", "Body", "Hair",
                    "--out-dir", str(out_dir),
                ])

        self.assertEqual(rc, 0)
        self.assertEqual(render.call_count, 2)
        render.assert_has_calls([
            mock.call(str(blender.resolve()), blend, out_dir / "Body.png"),
            mock.call(str(blender.resolve()), blend, out_dir / "Hair.png"),
        ])

    def test_combined_script_keeps_matching_meshes_and_uses_transparent_film(self):
        script = capture_cycles_ref.build_render_script(
            pathlib.Path("combined.png"),
            material_names=["Body", "Hair"],
            combined_character=True,
        )

        self.assertIn("film_transparent = True", script)
        self.assertIn("image_settings.color_mode = 'RGBA'", script)
        self.assertIn("obj.type == 'MESH'", script)
        self.assertIn("Body", script)
        self.assertIn("Hair", script)
        self.assertIn("hide_render", script)

    def test_render_script_selects_named_scene_camera_and_fails_if_missing(self):
        script = capture_cycles_ref.build_render_script(
            pathlib.Path("combined.png"),
            camera_name="摄像机",
        )

        self.assertIn("scene.objects.get('摄像机')", script)
        self.assertIn("scene.camera = _camera", script)
        self.assertIn("raise RuntimeError", script)
        self.assertIn("Camera not found or not a CAMERA", script)

    def test_combined_cli_renders_once_with_requested_name(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            blend = root / "character.blend"
            blend.touch()
            blender = root / "blender.exe"
            blender.touch()
            out_dir = root / "out"
            with mock.patch.object(
                    capture_cycles_ref, "CERTIFIED_BLENDER", str(blender)), \
                    mock.patch.object(capture_cycles_ref, "_render_one", return_value=0) as render:
                rc = capture_cycles_ref.main([
                    "--blender-exe", str(blender),
                    "--blend", str(blend),
                    "--materials", "Body", "Hair",
                    "--out-dir", str(out_dir),
                    "--combined-name", "character.png",
                ])

        self.assertEqual(rc, 0)
        render.assert_called_once_with(
            str(blender.resolve()), blend, out_dir / "character.png",
            material_names=["Body", "Hair"], combined_character=True,
        )

    def test_combined_cli_forwards_named_camera(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            blend = root / "character.blend"
            blend.touch()
            blender = root / "blender.exe"
            blender.touch()
            out_dir = root / "out"
            with mock.patch.object(
                    capture_cycles_ref, "CERTIFIED_BLENDER", str(blender)), \
                    mock.patch.object(capture_cycles_ref, "_render_one", return_value=0) as render:
                rc = capture_cycles_ref.main([
                    "--blender-exe", str(blender),
                    "--blend", str(blend),
                    "--materials", "Body", "Hair",
                    "--out-dir", str(out_dir),
                    "--combined-name", "character.png",
                    "--camera", "摄像机",
                ])

        self.assertEqual(rc, 0)
        render.assert_called_once_with(
            str(blender.resolve()), blend, out_dir / "character.png",
            material_names=["Body", "Hair"], combined_character=True,
            camera_name="摄像机",
        )


    def test_cli_rejects_non_certified_blender(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            blend = root / "character.blend"
            blend.touch()
            certified = root / "certified" / "blender.exe"
            certified.parent.mkdir()
            certified.touch()
            other = root / "other" / "blender.exe"
            other.parent.mkdir()
            other.touch()

            with mock.patch.object(
                    capture_cycles_ref, "CERTIFIED_BLENDER", str(certified)), \
                    mock.patch.object(capture_cycles_ref, "_render_one") as render:
                rc = capture_cycles_ref.main([
                    "--blender-exe", str(other),
                    "--blend", str(blend),
                    "--materials", "Body",
                    "--out-dir", str(root / "out"),
                ])

        self.assertEqual(rc, 2)
        render.assert_not_called()


class AlphaMaskCompareTests(unittest.TestCase):
    @staticmethod
    def _save_rgba(path, pixels):
        Image.fromarray(np.asarray(pixels, dtype=np.uint8), mode="RGBA").save(path)

    def test_alpha_mask_excludes_reference_background_and_makes_heatmap_transparent(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            cycles = root / "cycles.png"
            urp = root / "urp.png"
            heatmap = root / "heatmap.png"
            summary_path = root / "summary.json"
            self._save_rgba(cycles, [[[255, 0, 0, 255], [0, 0, 0, 0]]])
            self._save_rgba(urp, [[[255, 0, 0, 255], [255, 255, 255, 255]]])

            rc = compare.main([
                "--urp", str(urp),
                "--cycles", str(cycles),
                "--alpha-mask",
                "--out-heatmap", str(heatmap),
                "--out-json", str(summary_path),
            ])

            summary = json.loads(summary_path.read_text(encoding="utf-8"))
            heatmap_rgba = np.asarray(Image.open(heatmap).convert("RGBA"))

        self.assertEqual(rc, 0)
        self.assertEqual(summary["num_pixels"], 1)
        self.assertEqual(summary["mask"], "cycles_alpha")
        self.assertAlmostEqual(summary["mean"], 0.0)
        self.assertEqual(heatmap_rgba[0, 1, 3], 0)

    def test_alpha_mask_penalizes_missing_urp_alpha_even_when_rgb_matches(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            cycles = root / "cycles.png"
            urp = root / "urp.png"
            summary_path = root / "summary.json"
            self._save_rgba(cycles, [[[0, 0, 0, 255]]])
            self._save_rgba(urp, [[[0, 0, 0, 0]]])

            rc = compare.main([
                "--urp", str(urp),
                "--cycles", str(cycles),
                "--alpha-mask",
                "--out-json", str(summary_path),
            ])
            summary = json.loads(summary_path.read_text(encoding="utf-8"))

        self.assertEqual(rc, 0)
        self.assertEqual(summary["mean"], 100.0)
        self.assertEqual(summary["max"], 100.0)

    def test_align_scales_different_resolutions_onto_reference_canvas(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            cycles = root / "cycles.png"
            urp = root / "urp.png"
            summary_path = root / "summary.json"
            heatmap_path = root / "nested" / "heatmaps" / "pair.png"
            # 4x4 opaque white background + red 2x2 character
            cycles_px = np.full((4, 4, 4), 255, dtype=np.uint8)
            cycles_px[1:3, 1:3] = [255, 0, 0, 255]
            # 8x8 URP with same red character (larger canvas)
            urp_px = np.full((8, 8, 4), 255, dtype=np.uint8)
            urp_px[2:6, 2:6] = [255, 0, 0, 255]
            self._save_rgba(cycles, cycles_px)
            self._save_rgba(urp, urp_px)

            rc = compare.main([
                "--urp", str(urp),
                "--cycles", str(cycles),
                "--align",
                "--out-heatmap", str(heatmap_path),
                "--out-json", str(summary_path),
            ])
            summary = json.loads(summary_path.read_text(encoding="utf-8"))
            heatmap_exists = heatmap_path.exists()

        self.assertEqual(rc, 0)
        self.assertEqual(summary["mask"], "aligned_foreground")
        self.assertLess(summary["mean"], 1.0)
        self.assertTrue(heatmap_exists)

    def test_foreground_mask_uses_bright_background_when_alpha_is_opaque(self):
        rgba = np.zeros((3, 3, 4), dtype=np.float64)
        rgba[..., 3] = 1.0
        rgba[0, :, :3] = 1.0  # bright top row = background
        rgba[1:, :, 0] = 1.0  # red character
        mask = compare.foreground_mask(rgba)
        self.assertFalse(bool(mask[0, 1]))
        self.assertTrue(bool(mask[1, 1]))

    def test_foreground_mask_floods_opaque_grey_gradient_but_keeps_white_subject(self):
        rgba = np.ones((9, 9, 4), dtype=np.float64)
        for y in range(9):
            rgba[y, :, :3] = 0.70 + y * 0.01
        rgba[2:7, 2:7, :3] = [0.85, 0.2, 0.4]  # coloured silhouette border
        rgba[3:6, 3:6, :3] = 0.95              # disconnected white clothing
        mask = compare.foreground_mask(rgba)
        self.assertFalse(bool(mask[0, 4]))
        self.assertTrue(bool(mask[4, 4]))


class SinglePairBatchTests(unittest.TestCase):
    def test_directory_mode_missing_urp_warns_and_still_succeeds(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            cycles_dir = root / "cycles"
            urp_dir = root / "urp"
            out_dir = root / "out"
            cycles_dir.mkdir()
            urp_dir.mkdir()
            pixel = np.array([[[128, 64, 32, 255]]], dtype=np.uint8)
            Image.fromarray(pixel, mode="RGBA").save(cycles_dir / "Body.png")

            stderr = io.StringIO()
            with mock.patch.object(batch.sys, "stderr", stderr):
                rc = batch.main([
                    "--urp-dir", str(urp_dir),
                    "--cycles-dir", str(cycles_dir),
                    "--out-dir", str(out_dir),
                ])

        self.assertEqual(rc, 0)
        self.assertIn("missing URP render for Body, skipping", stderr.getvalue())

    def test_batch_accepts_single_pair_and_forwards_alpha_mask(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            cycles = root / "character.png"
            urp = root / "urp.png"
            out_dir = root / "out"
            pixel = np.array([[[128, 64, 32, 255]]], dtype=np.uint8)
            Image.fromarray(pixel, mode="RGBA").save(cycles)
            Image.fromarray(pixel, mode="RGBA").save(urp)

            rc = batch.main([
                "--urp", str(urp),
                "--cycles", str(cycles),
                "--out-dir", str(out_dir),
                "--alpha-mask",
            ])

            rows = (out_dir / "summary.csv").read_text(encoding="utf-8").splitlines()

        self.assertEqual(rc, 0)
        self.assertEqual(len(rows), 2)
        self.assertIn("character", rows[1])

    def test_single_pair_missing_urp_returns_nonzero(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            cycles = root / "character.png"
            missing_urp = root / "missing.png"
            out_dir = root / "out"
            pixel = np.array([[[128, 64, 32, 255]]], dtype=np.uint8)
            Image.fromarray(pixel, mode="RGBA").save(cycles)

            rc = batch.main([
                "--urp", str(missing_urp),
                "--cycles", str(cycles),
                "--out-dir", str(out_dir),
            ])

        self.assertNotEqual(rc, 0)


if __name__ == "__main__":
    unittest.main()
