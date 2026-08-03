#!/usr/bin/env python3
"""Capture the real Blender Miku Standard PBR sidebar in a clean session."""

from __future__ import annotations

import argparse
import ctypes
import pathlib
import sys

import bpy

# Keep the helper's logical Blender layout and the captured physical pixels in
# the same coordinate space on Windows high-DPI desktops.
try:
    ctypes.windll.shcore.SetProcessDpiAwareness(2)
except (AttributeError, OSError):
    pass


ROOT = pathlib.Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import miku_blender  # noqa: E402


def signal_ready(path: pathlib.Path):
    report = []
    for area in bpy.context.screen.areas:
        if area.type == "NODE_EDITOR":
            area.spaces.active.show_region_ui = True
            if hasattr(area.spaces.active, "active_panel_category"):
                area.spaces.active.active_panel_category = "Miku"
            report.append({"area": area.type, "width": area.width, "height": area.height,
                           "show_region_ui": area.spaces.active.show_region_ui,
                           "panel_registered": hasattr(bpy.types, "MIKU_PT_export_panel"),
                           "regions": [(region.type, region.width, region.height) for region in area.regions]})
            break
    path.write_text("ready\n" + repr(report), encoding="utf-8")
    shot = path.with_name(path.stem + ".blend-screen.png")
    try:
        bpy.ops.screen.screenshot(filepath=str(shot))
    except Exception as exc:
        path.write_text(path.read_text(encoding="utf-8") + "\nSCREENSHOT_ERROR=" + repr(exc), encoding="utf-8")
    return None


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--language", choices=("en_US", "zh_HANS"), required=True)
    parser.add_argument("--ready", type=pathlib.Path, required=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = parser.parse_args(argv)
    if tuple(bpy.app.version) != (5, 2, 0):
        raise RuntimeError(f"MIKU_BLENDER_VERSION_MISMATCH:{bpy.app.version}")
    args.ready.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.view.language = args.language
    miku_blender.register()
    bpy.ops.mesh.primitive_cube_add()
    obj = bpy.context.object
    obj.name = "Miku Documentation Cube"
    material = bpy.data.materials.new("Documentation Standard PBR")
    material.use_nodes = True
    material.node_tree.nodes.clear()
    principled = material.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    output = material.node_tree.nodes.new("ShaderNodeOutputMaterial")
    principled.inputs["Base Color"].default_value = (0.16, 0.38, 0.72, 1.0)
    material.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    obj.data.materials.append(material)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    area = max(
        bpy.context.screen.areas,
        key=lambda item: item.width * item.height,
    )
    area.type = "NODE_EDITOR"
    area.spaces.active.tree_type = "ShaderNodeTree"
    area.spaces.active.shader_type = "OBJECT"
    area.spaces.active.pin = False
    area.spaces.active.show_region_ui = True
    if hasattr(area.spaces.active, "active_panel_category"):
        area.spaces.active.active_panel_category = "Miku"
    bpy.app.timers.register(lambda: signal_ready(args.ready), first_interval=2.0)


if __name__ == "__main__":
    main()
