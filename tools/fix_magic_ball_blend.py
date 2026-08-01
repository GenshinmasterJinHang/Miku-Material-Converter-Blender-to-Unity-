"""Create the approved, non-destructive Magic Ball Light Path repair."""

from __future__ import annotations

import hashlib
from pathlib import Path

import bpy


SOURCE = Path(r"C:\Users\22687\Desktop\项目4\材质库\魔法球\魔法球.blend")
TARGET = SOURCE.with_name("魔法球.miku-fixed.blend")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def only_link(socket: object, diagnostic: str) -> object:
    links = list(getattr(socket, "links", ()) or ())
    if len(links) != 1:
        raise RuntimeError(f"{diagnostic}:expected-one-link:got={len(links)}")
    return links[0]


def main() -> None:
    if tuple(bpy.app.version) != (5, 2, 0):
        raise RuntimeError(
            "MIKU_BLENDER_VERSION_MISMATCH:"
            f"expected=(5, 2, 0):got={tuple(bpy.app.version)}"
        )
    source_hash = sha256(SOURCE)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    material = bpy.data.materials.get("魔法球10")
    if material is None or material.node_tree is None:
        raise RuntimeError("MIKU_MAGIC_BALL10_MATERIAL_MISSING")
    tree = material.node_tree
    output = next(
        (
            node
            for node in tree.nodes
            if node.bl_idname == "ShaderNodeOutputMaterial"
            and bool(node.is_active_output)
        ),
        None,
    )
    if output is None:
        raise RuntimeError("MIKU_MAGIC_BALL10_ACTIVE_OUTPUT_MISSING")
    surface = output.inputs.get("Surface")
    surface_link = only_link(surface, "MIKU_MAGIC_BALL10_SURFACE")
    final_mix = surface_link.from_node
    if final_mix.bl_idname != "ShaderNodeMixShader":
        raise RuntimeError(
            "MIKU_MAGIC_BALL10_FINAL_MIX_MISMATCH:"
            f"{final_mix.bl_idname}"
        )
    factor_link = only_link(
        final_mix.inputs[0],
        "MIKU_MAGIC_BALL10_FINAL_FACTOR",
    )
    if (
        factor_link.from_node.bl_idname != "ShaderNodeLightPath"
        or factor_link.from_socket.name != "Transparent Depth"
    ):
        raise RuntimeError(
            "MIKU_MAGIC_BALL10_LIGHT_PATH_MISMATCH:"
            f"{factor_link.from_node.bl_idname}:{factor_link.from_socket.name}"
        )
    primary_link = only_link(
        final_mix.inputs[1],
        "MIKU_MAGIC_BALL10_PRIMARY_SURFACE",
    )
    transparent_link = only_link(
        final_mix.inputs[2],
        "MIKU_MAGIC_BALL10_RECURSIVE_TRANSPARENT",
    )
    recursive_transparent = transparent_link.from_node
    if recursive_transparent.bl_idname != "ShaderNodeBsdfTransparent":
        raise RuntimeError(
            "MIKU_MAGIC_BALL10_RECURSIVE_BRANCH_MISMATCH:"
            f"{recursive_transparent.bl_idname}"
        )
    light_path = factor_link.from_node
    tree.links.remove(surface_link)
    tree.links.new(primary_link.from_socket, surface)
    for node in (final_mix, light_path, recursive_transparent):
        tree.nodes.remove(node)
    bpy.ops.wm.save_as_mainfile(filepath=str(TARGET), check_existing=False)
    if sha256(SOURCE) != source_hash:
        raise RuntimeError("MIKU_MAGIC_BALL_SOURCE_WAS_MODIFIED")
    print(
        "MIKU_MAGIC_BALL_FIXED",
        TARGET,
        f"sourceSha256={source_hash}",
        f"targetSha256={sha256(TARGET)}",
    )


if __name__ == "__main__":
    main()
