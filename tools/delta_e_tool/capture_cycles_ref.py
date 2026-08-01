"""Run the repository-certified Blender 5.2 binary for Cycles references."""
from __future__ import annotations

import argparse
import os
import pathlib
import subprocess
import sys
import tempfile

CERTIFIED_BLENDER = (
    r"C:\SteamLibrary\steamapps\common\Blender\blender.exe"
)


def build_render_script(
    out_png: pathlib.Path,
    material_names=None,
    combined_character: bool = False,
    camera_name=None,
) -> str:
    """Build the Blender script, optionally isolating a combined character."""
    out_png_posix = str(pathlib.Path(out_png).resolve()).replace(chr(92), "/")
    lines = [
        "import bpy",
        "if tuple(bpy.app.version) != (5, 2, 0):",
        "    raise RuntimeError('MIKU_BLENDER_VERSION_MISMATCH:' + str(bpy.app.version))",
        "bpy.context.scene.render.engine = 'CYCLES'",
        "bpy.context.scene.render.resolution_x = 512",
        "bpy.context.scene.render.resolution_y = 512",
        "bpy.context.scene.render.image_settings.file_format = 'PNG'",
        "bpy.context.scene.render.filepath = {!r}".format(out_png_posix),
    ]
    if camera_name is not None:
        missing_message = "Camera not found or not a CAMERA: {}".format(camera_name)
        lines.extend([
            "scene = bpy.context.scene",
            "_camera = scene.objects.get({!r})".format(camera_name),
            "if _camera is None or _camera.type != 'CAMERA':",
            "    raise RuntimeError({!r})".format(missing_message),
            "scene.camera = _camera",
        ])
    if combined_character:
        names = sorted(set(material_names or []))
        lines.extend([
            "bpy.context.scene.render.image_settings.color_mode = 'RGBA'",
            "bpy.context.scene.render.film_transparent = True",
            "_keep_materials = set({!r})".format(names),
            "for obj in bpy.context.scene.objects:",
            "    if obj.type == 'MESH':",
            "        _used = {slot.material.name for slot in obj.material_slots if slot.material}",
            "        obj.hide_render = not bool(_used & _keep_materials)",
        ])
    lines.append("bpy.ops.render.render(write_still=True)")
    return "\n".join(lines) + "\n"


def _render_one(
    blender_exe: str,
    blend_path: pathlib.Path,
    out_png: pathlib.Path,
    material_names=None,
    combined_character: bool = False,
    camera_name=None,
) -> int:
    if not blend_path.exists():
        print(f"[capture_cycles_ref] missing blend: {blend_path}", file=sys.stderr)
        return 1

    # Force an absolute path so Blender never resolves it against an unrelated
    # process working directory.
    script_text = build_render_script(
        out_png,
        material_names=material_names,
        combined_character=combined_character,
        camera_name=camera_name,
    )

    # Write the script to a temp file so --python <file> works. --python-text
    # expects the script to be the NAME of a Text block in the .blend; we'd
    # have to embed a Text block in the scene which is invasive.
    with tempfile.NamedTemporaryFile("w", suffix=".py", delete=False, encoding="utf-8") as f:
        f.write(script_text)
        script_path = f.name

    try:
        return subprocess.call(
            [
                blender_exe,
                "-b",
                str(blend_path),
                "--python-exit-code",
                "1",
                "--python",
                script_path,
            ],
        )
    finally:
        try:
            os.unlink(script_path)
        except OSError:
            pass


def main(argv=None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blender-exe", default=CERTIFIED_BLENDER)
    parser.add_argument("--blend", required=True, type=pathlib.Path,
                        help="Path to the Blender source for the character")
    parser.add_argument("--materials", nargs="+", required=True)
    parser.add_argument("--out-dir", type=pathlib.Path, required=True)
    parser.add_argument(
        "--camera",
        default=None,
        help="Name of the scene camera to use; Blender fails if it is missing or not a camera",
    )
    parser.add_argument(
        "--combined-name",
        default=None,
        help="Render the combined character once to this PNG name instead of one file per material",
    )
    args = parser.parse_args(argv)

    selected_blender = pathlib.Path(args.blender_exe).resolve()
    certified_blender = pathlib.Path(CERTIFIED_BLENDER).resolve()
    if selected_blender != certified_blender:
        print(
            "MIKU_BLENDER_EXECUTABLE_MISMATCH:"
            f"expected={certified_blender}:got={selected_blender}",
            file=sys.stderr,
        )
        return 2
    if not certified_blender.is_file():
        print(f"Blender not found at {certified_blender}", file=sys.stderr)
        return 2

    args.out_dir.mkdir(parents=True, exist_ok=True)
    if args.combined_name:
        filename = args.combined_name
        if not filename.lower().endswith(".png"):
            filename += ".png"
        out = args.out_dir / filename
        render_kwargs = {
            "material_names": args.materials,
            "combined_character": True,
        }
        if args.camera is not None:
            render_kwargs["camera_name"] = args.camera
        ret = _render_one(
            str(certified_blender),
            args.blend,
            out,
            **render_kwargs,
        )
        if ret != 0:
            print(f"[capture_cycles_ref] combined -> blender exit {ret}", file=sys.stderr)
        return ret

    rc = 0
    for mat in args.materials:
        out = args.out_dir / f"{mat}.png"
        if args.camera is None:
            ret = _render_one(str(certified_blender), args.blend, out)
        else:
            ret = _render_one(
                str(certified_blender),
                args.blend,
                out,
                camera_name=args.camera,
            )
        if ret != 0:
            print(f"[capture_cycles_ref] {mat} -> blender exit {ret}", file=sys.stderr)
            rc = ret
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
