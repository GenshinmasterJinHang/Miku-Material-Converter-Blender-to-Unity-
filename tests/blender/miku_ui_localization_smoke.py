"""Blender 5.0-5.2 smoke coverage for UI localization and bake quality."""

from __future__ import annotations

import sys
import inspect
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import miku_blender  # noqa: E402
from miku_blender.versioning import require_blender_capabilities  # noqa: E402


def assert_ui_localization() -> None:
    require_blender_capabilities(bpy)
    view = bpy.context.preferences.view
    original_language = view.language
    original_interface = view.use_translate_interface
    miku_blender.register()
    try:
        miku_blender.register()
        settings = bpy.context.scene.miku_settings
        assert settings.bake_texture_quality == "STANDARD_1024"
        assert miku_blender.bake_resolution_for_quality(settings.bake_texture_quality) == 1024

        view.use_translate_interface = True
        expected = {
            "en_US": {
                "Advanced": "Advanced",
                "Bake Texture Quality": "Bake Texture Quality",
                "Ultra (4096 × 4096)": "Ultra (4096 × 4096)",
            },
            "zh_HANS": {
                "Advanced": "高级",
                "Bake Texture Quality": "烘焙贴图质量",
                "Ultra (4096 × 4096)": "超高（4096 × 4096）",
            },
        }
        for locale, messages in expected.items():
            view.language = locale
            for source, translated in messages.items():
                actual = bpy.app.translations.pgettext_iface(source)
                assert actual == translated, (locale, source, actual, translated)
            diagnostic = miku_blender._translate_diagnostic(
                "MIKU_TIME_INPUT_UNSUPPORTED:Input.Time.Sine"
            )
            assert diagnostic.startswith("MIKU_TIME_INPUT_UNSUPPORTED:")
            if locale == "zh_HANS":
                assert "不支持" in diagnostic
            else:
                assert "not supported" in diagnostic
        draw_source = inspect.getsource(miku_blender.register).split(
            "def draw(self, context):", 1
        )[1].split("_REGISTERED_CLASSES", 1)[0]
        assert "MIKU_OT_add_time_node.bl_idname" not in draw_source
        assert "MIKU_OT_migrate_legacy_identities.bl_idname" not in draw_source
    finally:
        view.language = original_language
        view.use_translate_interface = original_interface
        miku_blender.unregister()
        miku_blender.unregister()


if __name__ == "__main__":
    assert_ui_localization()
    print("MIKU_UI_LOCALIZATION_SMOKE_OK")
