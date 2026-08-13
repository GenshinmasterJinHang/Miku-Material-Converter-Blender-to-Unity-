"""Regression checks for the public bilingual GitHub documentation."""

from __future__ import annotations

import hashlib
import json
import re
import struct
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PUBLIC_DOCUMENTS = (
    ROOT / "README.md",
    ROOT / "docs/zh-CN/README.md",
    ROOT / "docs/manual.md",
    ROOT / "docs/zh-CN/manual.md",
)
UI_IMAGES = (
    "blender-standard-pbr-en.png",
    "unity-game-material-wizard-en.png",
)
CHARACTER_IMAGE_HASHES = {
    "preset-genshin-hu-tao.png":
        "95a7812d501d27557cafd0ab7a15052ad67eec29d3422996d85b86e448e7e022",
    "preset-genshin-furina.png":
        "1abfde9f6bf844de2503c8694df5c423e6650366d2d8e2bac0af6dca6b36f5d9",
    "preset-hsr-bronya.png":
        "85464c9fdce286b3c51bcf341901642019e95248cbdf66c7b786eef74ed6fcfe",
    "preset-wuwa-phoebe.png":
        "f178220fddc059886db4cb3bd4af67b633590b4ad48144fe664fc47b0c74de3e",
    "preset-endfield-jierpeta.png":
        "7200e0f50d905370d977db3981d166f09bb51fcf4d29483973e3f8c922ee185f",
}
UI_IMAGE_HASHES = {
    "blender-standard-pbr-en.png":
        "24aa37f677bf9e51d1d35259b8e284e144c0b08ae040edc510a575609a48915c",
    "unity-game-material-wizard-en.png":
        "c82e3325b05626c1a8772b59aa6a099397baa4336ea07af697e7b6fc40e3f118",
}
UNITY_MENU_PATHS = (
    "Miku > Settings",
    "Miku > Game Toon > Materials > Create Material",
    "Miku > Game Toon > Materials > Apply Recommended Skin & Highlight Profile",
    "Miku > Game Toon > Textures > Import Audit",
    "Miku > Game Toon > Mesh > Smooth Normal Generator",
    "Miku > Game Toon > Rendering > Game Toon Renderer Feature Installer",
    "Miku > Game Toon > Rendering > Rebuild Anime Global Volume Profile",
    "Miku > Migration > Dry Run Selected MiGR Assets",
    "Miku > Migration > Upgrade Selected MiGR Assets",
)


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _png_size(path: Path) -> tuple[int, int]:
    data = path.read_bytes()[:24]
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise AssertionError(f"not a PNG: {path}")
    return struct.unpack(">II", data[16:24])


def _normalized(text: str) -> str:
    return " ".join(text.split())


class PublicDocumentationTests(unittest.TestCase):
    def test_bilingual_entry_points_version_and_active_branding(self) -> None:
        readme, chinese, manual, chinese_manual = map(_read, PUBLIC_DOCUMENTS)
        for document in (readme, chinese, manual, chinese_manual):
            self.assertIn("3.0.0", document)
            self.assertNotIn("0.11.0", document)
            self.assertNotIn("B2U", document)
        self.assertIn("docs/zh-CN/README.md", readme)
        self.assertIn("docs/manual.md", readme)
        self.assertIn("Standard PBR", readme)
        self.assertIn("standard_pbr", manual)
        self.assertIn("standard_pbr", chinese_manual)
        self.assertNotIn("MiGR", readme)
        self.assertNotIn("MiGR", chinese)

    def test_relative_links_and_images_exist_in_every_public_document(self) -> None:
        link_pattern = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")
        html_target_pattern = re.compile(
            r'<(?:a|img)\b[^>]*\b(?:href|src)="([^"]+)"'
        )
        for document_path in PUBLIC_DOCUMENTS:
            document = _read(document_path)
            targets = (
                link_pattern.findall(document)
                + html_target_pattern.findall(document)
            )
            for raw_target in targets:
                target = raw_target.strip()
                if target.startswith(("http://", "https://", "mailto:", "#")):
                    continue
                target = target.split("#", 1)[0]
                self.assertTrue(
                    (document_path.parent / target).is_file(),
                    f"{document_path.relative_to(ROOT)} -> {raw_target}",
                )

    def test_real_ui_and_character_images_are_referenced(self) -> None:
        readme, chinese, manual, chinese_manual = map(_read, PUBLIC_DOCUMENTS)
        for image in UI_IMAGES[:1]:
            for document in (readme, chinese, manual, chinese_manual):
                self.assertIn(image, document)
        for document in (manual, chinese_manual):
            self.assertIn(UI_IMAGES[1], document)
            for image in CHARACTER_IMAGE_HASHES:
                self.assertIn(image, document)

        self.assertFalse(
            (ROOT / "docs/images/blender-standard-pbr-zh-cn.png").exists()
        )
        self.assertFalse(
            (ROOT / "docs/images/unity-game-material-wizard-zh-cn.png").exists()
        )
        self.assertFalse(
            (ROOT / "tools/docs/render_documentation_screenshots.py").exists()
        )

    def test_documentation_images_match_approved_source_hashes(self) -> None:
        for filename, expected in {**UI_IMAGE_HASHES,
                                   **CHARACTER_IMAGE_HASHES}.items():
            self.assertEqual(
                expected,
                _sha256(ROOT / "docs/images" / filename),
                filename,
            )

    def test_character_renders_are_exact_1080p_pngs(self) -> None:
        for filename in CHARACTER_IMAGE_HASHES:
            self.assertEqual(
                (1920, 1080),
                _png_size(ROOT / "docs/images" / filename),
                filename,
            )

    def test_four_shader_families_and_22_parts_are_documented(self) -> None:
        readme, chinese, manual, chinese_manual = map(_read, PUBLIC_DOCUMENTS)
        for document in (readme, chinese, manual, chinese_manual):
            self.assertIn("Shader/HLSL", document)
            for workflow in ("Genshin", "HSR", "Wuwa", "Endfield"):
                self.assertIn(workflow, document)
        self.assertIn("22 valid material parts", readme)
        self.assertIn("22 valid material parts", manual)
        self.assertIn("22 个有效材质部位", chinese)
        self.assertIn("22 个有效材质部位", chinese_manual)
        for character in ("Hu Tao", "Furina", "Bronya", "Phoebe", "洁尔佩塔"):
            self.assertIn(character, manual)
        for character in ("胡桃", "芙宁娜", "布洛妮娅", "菲比", "洁尔佩塔"):
            self.assertIn(character, chinese_manual)

    def test_public_unity_tools_are_in_both_manuals(self) -> None:
        manual = _normalized(_read(ROOT / "docs/manual.md"))
        chinese_manual = _normalized(_read(ROOT / "docs/zh-CN/manual.md"))
        for menu_path in UNITY_MENU_PATHS:
            self.assertIn(menu_path, manual)
            self.assertIn(menu_path, chinese_manual)
        for inspector in (
            "Miku Material Inspector",
            "Mesh Binding Description Inspector",
            "Toon Material Recipe Inspector",
        ):
            self.assertIn(inspector, manual)

    def test_code_and_character_image_license_boundaries_are_explicit(self) -> None:
        readme, chinese, manual, chinese_manual = map(_read, PUBLIC_DOCUMENTS)
        provenance = _read(ROOT / "docs/provenance/documentation-images.md")
        notices = _read(ROOT / "THIRD_PARTY_NOTICES.md")
        self.assertIn("MIT License", readme)
        self.assertIn("MIT License", chinese)
        self.assertIn("Commercial use is prohibited", manual)
        self.assertIn("禁止用于任何商业用途", chinese_manual)
        self.assertIn("excluded from Miku's MIT", provenance)
        self.assertIn("commercial use is prohibited", _normalized(notices))
        for filename in CHARACTER_IMAGE_HASHES:
            self.assertIn(filename, provenance)
        package = json.loads(
            _read(ROOT / "unity/Packages/com.miku.shaderconverter/package.json")
        )
        self.assertEqual("MIT", package["license"])
        self.assertIn("MIT License", _read(ROOT / "LICENSE"))

    def test_release_asset_names_and_workflow_menu_are_documented(self) -> None:
        release = _read(ROOT / "docs/release/miku-2.2.9.md")
        self.assertIn("miku_shader_converter-2.2.9.zip", release)
        self.assertIn("com.miku.shaderconverter-2.2.9.tgz", release)
        self.assertIn("SHA256SUMS.txt", release)
        self.assertIn("Miku > Game Toon > Materials > Create Material", release)
        self.assertNotIn("Create Material Template", release)


if __name__ == "__main__":
    unittest.main()
