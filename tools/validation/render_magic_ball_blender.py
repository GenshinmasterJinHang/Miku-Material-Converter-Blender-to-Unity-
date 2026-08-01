"""Render the Miku Magic Ball corpus with one fixed Blender 5.2 setup.

Run through the repository-certified Blender executable.  The script renders
the same source mesh for every material, exports that mesh to FBX for the Unity
side, and writes a manifest that points at the retained Miku bundles.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


MAGIC_BALLS = [
    *(f"魔法球{index}" for index in range(1, 11)),
    "魔法球10.001",
]


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--blend", required=True, type=Path)
    parser.add_argument("--bundle-root", required=True, type=Path)
    parser.add_argument("--out-root", required=True, type=Path)
    separator = sys.argv.index("--") if "--" in sys.argv else -1
    return parser.parse_args(
        sys.argv[separator + 1 :] if separator >= 0 else []
    )


def _look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat(
        "-Z",
        "Y",
    ).to_euler()


def _material(name: str, color: tuple[float, float, float, float]):
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Roughness"].default_value = 0.55
    return material


def _center_statistics(image_path: Path) -> dict[str, object]:
    image = bpy.data.images.load(str(image_path), check_existing=False)
    width, height = image.size
    pixels = list(image.pixels)
    rgb_sum = [0.0, 0.0, 0.0]
    luminance_sum = 0.0
    luminance_sq_sum = 0.0
    count = 0
    radius = min(width, height) * 0.205
    radius_sq = radius * radius
    for y in range(height):
        dy = y + 0.5 - height * 0.5
        for x in range(width):
            dx = x + 0.5 - width * 0.5
            if dx * dx + dy * dy > radius_sq:
                continue
            offset = (y * width + x) * 4
            rgb = pixels[offset : offset + 3]
            luminance = (
                0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]
            )
            for channel in range(3):
                rgb_sum[channel] += rgb[channel]
            luminance_sum += luminance
            luminance_sq_sum += luminance * luminance
            count += 1
    mean = luminance_sum / max(count, 1)
    variance = luminance_sq_sum / max(count, 1) - mean * mean
    result = {
        "centerMeanRgbLinear": [
            round(value / max(count, 1), 8) for value in rgb_sum
        ],
        "centerMeanLuminanceLinear": round(mean, 8),
        "centerLuminanceVariance": round(max(variance, 0.0), 8),
    }
    bpy.data.images.remove(image)
    return result


def _bundle_path(bundle_root: Path, name: str) -> Path:
    matches = sorted(bundle_root.glob(f"{name}__*/*.mikubundle"))
    if len(matches) != 1:
        raise RuntimeError(
            f"MIKU_MAGIC_BALL_BUNDLE_MATCH_INVALID:{name}:{len(matches)}"
        )
    return matches[0].resolve()


def main() -> None:
    args = _arguments()
    if tuple(bpy.app.version) != (5, 2, 0):
        raise RuntimeError(
            "MIKU_BLENDER_VERSION_MISMATCH:" + str(bpy.app.version)
        )
    bpy.ops.wm.open_mainfile(filepath=str(args.blend.resolve()))
    source = bpy.data.objects.get("魔法球1")
    if source is None or source.type != "MESH":
        raise RuntimeError("MIKU_MAGIC_BALL_SOURCE_MESH_MISSING")

    retained_mesh = source.data.copy()
    retained_scale = source.scale.copy()
    source_materials = {
        name: bpy.data.materials[name]
        for name in MAGIC_BALLS
    }
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    sphere = bpy.data.objects.new("Miku Magic Ball Validation Sphere", retained_mesh)
    bpy.context.scene.collection.objects.link(sphere)
    sphere.scale = retained_scale
    sphere.location = Vector((0.0, 0.0, 0.0))
    sphere.data.materials.append(source_materials[MAGIC_BALLS[0]])

    shared_root = args.out_root / "shared"
    blender_root = args.out_root / "blender"
    shared_root.mkdir(parents=True, exist_ok=True)
    blender_root.mkdir(parents=True, exist_ok=True)
    mesh_path = (shared_root / "magic-ball-sphere.fbx").resolve()
    bpy.context.view_layer.objects.active = sphere
    sphere.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=str(mesh_path),
        use_selection=True,
        object_types={"MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=False,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=False,
    )

    ground = bpy.data.meshes.new("Miku Validation Ground Mesh")
    ground.from_pydata(
        [
            (-6.0, -6.0, -1.3),
            (6.0, -6.0, -1.3),
            (6.0, 6.0, -1.3),
            (-6.0, 6.0, -1.3),
        ],
        [],
        [(0, 1, 2, 3)],
    )
    ground_object = bpy.data.objects.new("Miku Validation Ground", ground)
    bpy.context.scene.collection.objects.link(ground_object)
    ground.materials.append(_material("Miku Validation Gray", (0.18, 0.18, 0.18, 1.0)))

    camera_data = bpy.data.cameras.new("Miku Validation Camera")
    camera = bpy.data.objects.new("Miku Validation Camera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = Vector((0.0, -4.8, 0.25))
    camera_data.lens = 54.0
    _look_at(camera, Vector((0.0, 0.0, 0.0)))
    bpy.context.scene.camera = camera

    for name, rotation, energy, color in (
        ("Key", (math.radians(35), 0.0, math.radians(-30)), 2.6, (1.0, 0.92, 0.82)),
        ("Fill", (math.radians(55), 0.0, math.radians(145)), 0.8, (0.62, 0.76, 1.0)),
    ):
        light_data = bpy.data.lights.new(f"Miku {name}", "SUN")
        light_data.energy = energy
        light_data.color = color
        light = bpy.data.objects.new(f"Miku {name}", light_data)
        bpy.context.scene.collection.objects.link(light)
        light.rotation_euler = rotation

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.color_management = "FOLLOW_SCENE"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.world.color = (0.055, 0.055, 0.055)

    cases = []
    statistics = {}
    for name in MAGIC_BALLS:
        sphere.data.materials[0] = source_materials[name]
        image_path = (blender_root / f"{name}.png").resolve()
        scene.render.filepath = str(image_path)
        bpy.ops.render.render(write_still=True)
        case_statistics = _center_statistics(image_path)
        statistics[name] = case_statistics
        cases.append(
            {
                "name": name,
                "bundlePath": str(_bundle_path(args.bundle_root, name)),
                "blenderImage": str(image_path),
                "statistics": case_statistics,
            }
        )

    dots_bundle = _bundle_path(args.bundle_root, "Dots Stroke")
    one = statistics["魔法球1"]
    five = statistics["魔法球5"]
    rgb_distance = math.sqrt(
        sum(
            (a - b) ** 2
            for a, b in zip(
                one["centerMeanRgbLinear"],
                five["centerMeanRgbLinear"],
            )
        )
    )
    acceptance = {
        "magicBall1NotBlack": one["centerMeanLuminanceLinear"] > 0.02,
        "magicBall5NotBlack": five["centerMeanLuminanceLinear"] > 0.02,
        "magicBall1HasVariation": one["centerLuminanceVariance"] > 0.0001,
        "magicBall5HasVariation": five["centerLuminanceVariance"] > 0.0001,
        "magicBall1And5MeanRgbDistance": round(rgb_distance, 8),
        "magicBall1And5Distinct": rgb_distance > 0.02,
    }
    if not all(
        value
        for key, value in acceptance.items()
        if key != "magicBall1And5MeanRgbDistance"
    ):
        raise RuntimeError(
            "MIKU_MAGIC_BALL_BLENDER_VISUAL_ACCEPTANCE_FAILED:"
            + json.dumps(acceptance, ensure_ascii=False, sort_keys=True)
        )

    manifest = {
        "schema": "miku-magic-ball-visual-validation-1.0",
        "blenderVersion": ".".join(str(part) for part in bpy.app.version),
        "renderEngine": scene.render.engine,
        "resolution": [512, 512],
        "viewTransform": scene.view_settings.view_transform,
        "look": scene.view_settings.look,
        "exposure": scene.view_settings.exposure,
        "meshPath": str(mesh_path),
        "cases": cases,
        "importOnly": [
            {
                "name": "Dots Stroke",
                "bundlePath": str(dots_bundle),
            }
        ],
        "acceptance": acceptance,
    }
    manifest_path = (args.out_root / "visual-manifest.json").resolve()
    manifest_path.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print("MIKU_MAGIC_BALL_BLENDER_VISUAL_OK", manifest_path)


if __name__ == "__main__":
    main()
