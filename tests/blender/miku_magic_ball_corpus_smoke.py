"""Blender 5.2 smoke coverage for the real Magic Ball material corpus."""

from __future__ import annotations

import contextlib
import json
import hashlib
import os
import tempfile
import sys
import uuid
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import miku_blender  # noqa: E402
from miku.semantic import build_material_ir  # noqa: E402
from miku.planner import ConversionPlanner  # noqa: E402
from extensions.miku_shader_converter import bake_worker  # noqa: E402


SOURCE = Path(r"C:\Users\22687\Desktop\项目4\材质库\魔法球\魔法球.blend")
FIXED = SOURCE.with_name("魔法球.miku-fixed.blend")


def load(path: Path) -> None:
    bpy.ops.wm.open_mainfile(filepath=str(path))
    assert tuple(bpy.app.version) == (5, 2, 0), tuple(bpy.app.version)


def snapshot_all(path: Path) -> dict[str, dict]:
    load(path)
    result = {}
    for material in sorted(bpy.data.materials, key=lambda item: item.name):
        graph = miku_blender.snapshot_material(material)
        result[material.name] = build_material_ir(
            graph,
            source_blend_id="magic-ball-corpus",
            material_key=material.name,
        )
    return result


def error_codes(document: dict) -> set[str]:
    return {
        str(item.get("code") or "")
        for item in document.get("diagnostics", [])
        if str(item.get("severity") or "").lower() == "error"
    }


def tree_hashes(root: Path) -> dict[str, str]:
    return {
        path.relative_to(root).as_posix(): hashlib.sha256(
            path.read_bytes()
        ).hexdigest()
        for path in sorted(root.rglob("*"))
        if path.is_file()
    }


def expression_index(document: dict) -> dict[str, dict]:
    return {
        str(item["id"]): item
        for item in document.get("expressions", [])
    }


def channel_expression(document: dict, semantic: str) -> dict:
    channel = next(
        item
        for item in document["channels"]
        if item["semantic"] == semantic
    )
    expression_id = channel["value"]["expressionId"]
    return expression_index(document)[expression_id]


def referenced_expressions(
    document: dict,
    root_expression_id: str,
) -> list[dict]:
    expressions = expression_index(document)
    found = []
    pending = [root_expression_id]
    visited = set()
    while pending:
        expression_id = pending.pop()
        if expression_id in visited:
            continue
        visited.add(expression_id)
        expression = expressions[expression_id]
        found.append(expression)
        for value in expression.get("inputs", {}).values():
            dependency = value.get("expressionId")
            if dependency:
                pending.append(dependency)
    return found


def nested_expression_ids(value: object) -> set[str]:
    if isinstance(value, dict):
        result = {
            str(value["expressionId"])
            for key in ("expressionId",)
            if value.get(key)
        }
        for child in value.values():
            result.update(nested_expression_ids(child))
        return result
    if isinstance(value, list):
        result = set()
        for child in value:
            result.update(nested_expression_ids(child))
        return result
    return set()


def assert_baked_resource_is_sealed(
    document: dict,
    bundle: dict,
    expression: dict,
) -> None:
    parameters = expression["params"]
    resource_id = parameters["resourceId"]
    binding_key = parameters["referenceName"]
    assert binding_key.startswith("_MIKU_Baked_"), parameters
    ir_resource = next(
        item
        for item in document["resources"]
        if item["id"] == resource_id
    )
    assert ir_resource["bindingKey"] == binding_key, ir_resource
    bundle_resource = next(
        item
        for item in bundle["resources"]
        if item["id"] == resource_id
    )
    assert bundle_resource["bindingKey"] == binding_key, bundle_resource


def _assert_corpus_203_legacy() -> None:
    source_documents = snapshot_all(SOURCE)
    assert len(source_documents) == 11, sorted(source_documents)
    source_magic_10 = source_documents["魔法球10"]
    light_path = [
        item
        for item in source_magic_10["diagnostics"]
        if item.get("code") == "MIKU_LIGHT_PATH_UNSUPPORTED"
    ]
    assert len(light_path) == 1, source_magic_10["diagnostics"]
    assert (
        light_path[0]["message"]
        == "MIKU_LIGHT_PATH_UNSUPPORTED:Transparent Depth"
    ), light_path[0]
    assert "MIKU_REQUIRED_CHANNEL_UNRESOLVED" not in error_codes(source_magic_10)

    fixed_documents = snapshot_all(FIXED)
    expected_fixed_names = {
        "Dots Stroke",
        *(f"魔法球{index}" for index in range(1, 11)),
    }
    assert expected_fixed_names.issubset(fixed_documents), (
        expected_fixed_names - set(fixed_documents),
        sorted(fixed_documents),
    )
    for name, document in fixed_documents.items():
        errors = error_codes(document)
        assert not errors, (name, document["diagnostics"])
        assert "MIKU_REQUIRED_CHANNEL_UNRESOLVED" not in errors

    overlay_roots = {}
    for name in ("魔法球1", "魔法球5"):
        document = fixed_documents[name]
        expressions = expression_index(document)
        emission = channel_expression(document, "Emission")
        assert emission["op"] == "Math.Multiply", (name, emission)
        emission_inputs = [
            expressions[value["expressionId"]]
            for value in emission["inputs"].values()
        ]
        strength = next(
            item
            for item in emission_inputs
            if item["op"] == "Constant"
        )
        assert abs(strength["params"]["value"] - 12.8) < 0.00001, (
            name,
            strength,
        )
        overlay = next(
            item
            for item in emission_inputs
            if item["op"] == "Color.Overlay"
        )
        overlay_inputs = {
            key: expressions[value["expressionId"]]["op"]
            for key, value in overlay["inputs"].items()
        }
        assert overlay_inputs == {
            "A": "Color.HueSaturationValue",
            "B": "Texture.SampleBaked2D",
            "T": "Math.Lerp",
        }, (name, overlay_inputs, overlay)
        overlay_roots[name] = overlay
    assert (
        overlay_roots["魔法球1"]["id"]
        != overlay_roots["魔法球5"]["id"]
    ), overlay_roots
    magic_1_baked = expression_index(fixed_documents["魔法球1"])[
        overlay_roots["魔法球1"]["inputs"]["B"]["expressionId"]
    ]
    magic_5_baked = expression_index(fixed_documents["魔法球5"])[
        overlay_roots["魔法球5"]["inputs"]["B"]["expressionId"]
    ]
    assert (
        magic_1_baked["params"]["referenceName"]
        != magic_5_baked["params"]["referenceName"]
    ), (magic_1_baked, magic_5_baked)

    for index in range(1, 6):
        name = f"魔法球{index}"
        document = fixed_documents[name]
        assert document["surfaceModelPlan"]["kind"] == "OpaquePBR", (
            name,
            document["surfaceModelPlan"],
        )
        assert any(
            item.get("code") == "MIKU_COAT_URP_APPROXIMATION"
            and item.get("severity") == "warning"
            for item in document["diagnostics"]
        ), (name, document["diagnostics"])
        normal = next(
            item
            for item in document["channels"]
            if item["semantic"] == "Normal"
        )
        assert normal.get("requiresBake"), (name, normal)
        plan = ConversionPlanner().plan(document)
        normal_jobs = [
            item
            for item in plan["bakeJobs"]
            if item.get("scope") == "Channels"
            and item.get("semantics") == ["Normal"]
        ]
        assert len(normal_jobs) == 1, (name, plan["bakeJobs"])

    expected_methods = {
        "魔法球6": "AlphaBlend",
        "魔法球7": "AlphaBlend",
        "魔法球9": "Dithered",
        "魔法球10": "Dithered",
        "魔法球10.001": "Dithered",
    }
    for name, document in fixed_documents.items():
        expected = expected_methods.get(name, "Opaque")
        assert document["surfaceContract"]["renderMethod"] == expected, (
            name,
            document["surfaceContract"],
        )
    magic_9 = fixed_documents["魔法球9"]
    assert any(
        expression["op"] == "Vector.NormalFromHeight"
        and expression["stage"] == "Fragment"
        for expression in magic_9["expressions"]
    ), magic_9["expressions"]
    magic_10 = fixed_documents["魔法球10"]
    assert any(
        item.get("code") == "MIKU_COLORED_TRANSPARENCY_APPROXIMATE"
        for item in magic_10["diagnostics"]
    ), magic_10["diagnostics"]
    magic_10_plan = ConversionPlanner().plan(magic_10)
    wireframe_jobs = [
        item
        for item in magic_10_plan["bakeJobs"]
        if item.get("scope") == "ExpressionIsland"
        and item.get("usage") == "Scalar"
    ]
    assert wireframe_jobs, magic_10_plan["bakeJobs"]
    for name in ("魔法球7", "魔法球10"):
        emission = next(
            item
            for item in fixed_documents[name]["channels"]
            if item["semantic"] == "Emission"
        )
        assert not emission.get("requiresBake"), (name, emission)
        plan = ConversionPlanner().plan(fixed_documents[name])
        assert not any(
            item.get("scope") == "Channels"
            and "Emission" in item.get("semantics", [])
            for item in plan["bakeJobs"]
        ), (name, plan["bakeJobs"])
    load(FIXED)
    magic_10_material = bpy.data.materials["魔法球10"]
    assert not any(
        node.bl_idname == "ShaderNodeLightPath"
        for node in magic_10_material.node_tree.nodes
    )
    bake_worker.register()
    try:
        retained_output = os.environ.get("MIKU_MAGIC_BALL_OUTPUT")
        output_context = (
            contextlib.nullcontext(Path(retained_output).resolve())
            if retained_output
            else tempfile.TemporaryDirectory(
                prefix="miku-magic-ball-2.0.3-"
            )
        )
        with output_context as output:
            first_root = Path(output) / "first"
            second_root = Path(output) / "second"
            first_root.mkdir(parents=True, exist_ok=True)
            second_root.mkdir(parents=True, exist_ok=True)
            exported = {}
            for material in sorted(
                bpy.data.materials,
                key=lambda item: item.name,
            ):
                print("MIKU_MAGIC_BALL_EXPORT", material.name)
                persistent_id = str(
                    uuid.uuid5(
                        uuid.NAMESPACE_URL,
                        "miku-magic-ball:" + material.name,
                    )
                )
                exported[material.name] = miku_blender.export_material_bundle(
                    material,
                    first_root,
                    source_blend_id="magic-ball-corpus-fixed",
                    persistent_material_id=persistent_id,
                    allow_appearance_approximation=False,
                )
            assert set(exported) == set(fixed_documents)
            for index in range(1, 6):
                name = f"魔法球{index}"
                bundle_directory = Path(exported[name]["bundlePath"]).parent
                ir_path = (
                    bundle_directory
                    / exported[name]["bundle"]["ir"]["relativePath"]
                )
                exported_ir = json.loads(ir_path.read_text(encoding="utf-8"))
                normal = next(
                    item
                    for item in exported_ir["channels"]
                    if item["semantic"] == "Normal"
                )
                assert normal["value"]["kind"] == "TextureResource", (
                    name,
                    normal,
                )
                assert not normal.get("requiresBake"), (name, normal)
            magic_9_directory = Path(
                exported["魔法球9"]["bundlePath"]
            ).parent
            magic_9_ir = json.loads(
                (
                    magic_9_directory
                    / exported["魔法球9"]["bundle"]["ir"]["relativePath"]
                ).read_text(encoding="utf-8")
            )
            magic_9_normal = next(
                item
                for item in magic_9_ir["channels"]
                if item["semantic"] == "Normal"
            )
            magic_9_runtime = referenced_expressions(
                magic_9_ir,
                magic_9_normal["value"]["expressionId"],
            )
            magic_9_baked = next(
                item
                for item in magic_9_runtime
                if item["op"] == "Texture.SampleBaked2D"
            )
            assert_baked_resource_is_sealed(
                magic_9_ir,
                exported["魔法球9"]["bundle"],
                magic_9_baked,
            )

            magic_10_directory = Path(
                exported["魔法球10"]["bundlePath"]
            ).parent
            magic_10_ir = json.loads(
                (
                    magic_10_directory
                    / exported["魔法球10"]["bundle"]["ir"]["relativePath"]
                ).read_text(encoding="utf-8")
            )
            magic_10_expression_ids = nested_expression_ids(
                magic_10_ir["weightedClosures"]["terms"]
            )
            magic_10_expressions = expression_index(magic_10_ir)
            magic_10_baked = next(
                magic_10_expressions[expression_id]
                for expression_id in magic_10_expression_ids
                if expression_id in magic_10_expressions
                if magic_10_expressions[expression_id]["op"]
                == "Texture.SampleBaked2D"
                and magic_10_expressions[expression_id]["params"]["usage"]
                == "Scalar"
            )
            assert_baked_resource_is_sealed(
                magic_10_ir,
                exported["魔法球10"]["bundle"],
                magic_10_baked,
            )
            repeated = {}
            for material in sorted(
                bpy.data.materials,
                key=lambda item: item.name,
            ):
                name = material.name
                repeated[name] = miku_blender.export_material_bundle(
                    material,
                    second_root,
                    source_blend_id="magic-ball-corpus-fixed",
                    persistent_material_id=str(
                        uuid.uuid5(
                            uuid.NAMESPACE_URL,
                            "miku-magic-ball:" + material.name,
                        )
                    ),
                    allow_appearance_approximation=False,
                )
                first_bundle_root = Path(
                    exported[name]["bundlePath"]
                ).parent
                second_bundle_root = Path(
                    repeated[name]["bundlePath"]
                ).parent
                assert tree_hashes(first_bundle_root) == tree_hashes(
                    second_bundle_root
                ), name
            dots_bundle = Path(exported["Dots Stroke"]["bundlePath"])
            assert dots_bundle.is_file(), dots_bundle
            magic_10_manifest = exported["魔法球10"]["manifest"]
            artifacts = magic_10_manifest["completion"]["artifacts"]
            assert artifacts, magic_10_manifest
            for artifact in artifacts:
                assert (
                    Path(exported["魔法球10"]["bundlePath"]).parent
                    / artifact["relativePath"]
                ).is_file(), artifact
            load(SOURCE)
            try:
                miku_blender.export_material_bundle(
                    bpy.data.materials["魔法球10"],
                    first_root,
                    source_blend_id="magic-ball-corpus-original",
                    persistent_material_id=str(
                        uuid.uuid5(
                            uuid.NAMESPACE_URL,
                            "miku-magic-ball-original:魔法球10",
                        )
                    ),
                    allow_appearance_approximation=False,
                )
            except RuntimeError as exc:
                assert str(exc) == (
                    "MIKU_LIGHT_PATH_UNSUPPORTED:Transparent Depth"
                ), str(exc)
            else:
                raise AssertionError(
                    "Original 魔法球10 must reject required Light Path"
                )
    finally:
        bake_worker.unregister()


def assert_corpus() -> None:
    """Validate the 2.1 portable/source-mesh split on the real corpus."""

    load(FIXED)
    materials = {
        material.name: material
        for material in sorted(
            bpy.data.materials,
            key=lambda item: item.name,
        )
    }
    expected = {
        "Dots Stroke",
        *(f"魔法球{index}" for index in range(1, 11)),
        "魔法球10.001",
    }
    assert expected.issubset(materials), (
        expected - set(materials),
        sorted(materials),
    )
    graphs = {
        name: miku_blender.snapshot_material(material)
        for name, material in materials.items()
    }
    portable = {
        name: build_material_ir(
            graph,
            source_blend_id="magic-ball-corpus",
            material_key=name,
            conversion_mode="Auto",
        )
        for name, graph in graphs.items()
    }

    for name in ("魔法球1", "魔法球4", "魔法球9"):
        document = portable[name]
        assert not error_codes(document), (
            name,
            document["diagnostics"],
        )
        ops = {
            expression["op"]
            for expression in document["expressions"]
        }
        assert "Input.TextureCoordinate.Object" in ops, (name, ops)
        assert "Texture.Noise.Factor" in ops, (name, ops)
        assert "Texture.SampleBaked2D" not in ops, (name, ops)
        if name in {"魔法球1", "魔法球4"}:
            assert "Vector.Mapping" in ops, (name, ops)
        if name == "魔法球9":
            assert "Vector.NormalFromHeight" in ops, (name, ops)
        auto_plan = ConversionPlanner().plan(document, mode="Auto")
        assert not auto_plan["bakeJobs"], (
            name,
            auto_plan["bakeJobs"],
        )

    magic_10_auto = portable["魔法球10"]
    assert "MIKU_SOURCE_MESH_FIDELITY_REQUIRED" in error_codes(
        magic_10_auto
    ), magic_10_auto["diagnostics"]
    assert not any(
        expression["op"] == "Texture.SampleBaked2D"
        for expression in magic_10_auto["expressions"]
    ), magic_10_auto["expressions"]

    modes = {
        name: (
            "AllowMeshBake"
            if any(
                str(item.get("severity") or "").lower() == "error"
                for item in document.get("diagnostics", []) or []
                if isinstance(item, dict)
            )
            else "Auto"
        )
        for name, document in portable.items()
    }
    modes["魔法球10"] = "AllowMeshBake"
    bake_worker.register()
    try:
        retained_output = os.environ.get("MIKU_MAGIC_BALL_OUTPUT")
        output_context = (
            contextlib.nullcontext(Path(retained_output).resolve())
            if retained_output
            else tempfile.TemporaryDirectory(
                prefix="miku-magic-ball-2.1.0-"
            )
        )
        with output_context as output:
            first_root = Path(output) / "first"
            second_root = Path(output) / "second"
            first_root.mkdir(parents=True, exist_ok=True)
            second_root.mkdir(parents=True, exist_ok=True)

            def export_all(root: Path) -> dict[str, dict]:
                exported = {}
                for name, material in materials.items():
                    print(
                        "MIKU_MAGIC_BALL_EXPORT",
                        name,
                        modes[name],
                    )
                    exported[name] = (
                        miku_blender.export_material_bundle(
                            material,
                            root,
                            source_blend_id=(
                                "magic-ball-corpus-fixed"
                            ),
                            persistent_material_id=str(
                                uuid.uuid5(
                                    uuid.NAMESPACE_URL,
                                    "miku-magic-ball:" + name,
                                )
                            ),
                            mode=modes[name],
                            allow_appearance_approximation=False,
                        )
                    )
                return exported

            first = export_all(first_root)
            second = export_all(second_root)
            for name in sorted(materials):
                first_bundle_root = Path(
                    first[name]["bundlePath"]
                ).parent
                second_bundle_root = Path(
                    second[name]["bundlePath"]
                ).parent
                assert tree_hashes(first_bundle_root) == tree_hashes(
                    second_bundle_root
                ), name

            for name in ("魔法球1", "魔法球4", "魔法球9"):
                bundle = first[name]["bundle"]
                assert bundle["documentKind"] == "miku-bundle-1.0", (
                    name,
                    bundle["documentKind"],
                )
                assert not any(
                    resource.get("semantic") == "ExpressionIsland"
                    or resource.get("meshBinding")
                    for resource in bundle["resources"]
                ), (name, bundle["resources"])

            magic_10 = first["魔法球10"]
            bundle = magic_10["bundle"]
            assert bundle["documentKind"] == "miku-bundle-1.0", bundle
            source_meshes = [
                resource
                for resource in bundle["resources"]
                if resource.get("semantic") == "SourceMesh"
            ]
            assert len(source_meshes) == 1, bundle["resources"]
            source_mesh = source_meshes[0]
            assert source_mesh["mediaType"] == "model/gltf-binary"
            assert source_mesh["meshCount"] > 0
            assert source_mesh["vertexCount"] > 0
            assert source_mesh["indexCount"] > 0
            assert source_mesh["hasUv0"] is True
            assert source_mesh["rendererBindings"]
            bundle_root = Path(magic_10["bundlePath"]).parent
            glb_path = (
                bundle_root / source_mesh["relativePath"]
            )
            assert glb_path.is_file(), glb_path
            assert (
                hashlib.sha256(glb_path.read_bytes()).hexdigest()
                == source_mesh["sha256"]
            )
            mesh_hash = source_mesh["meshBinding"]["sha256"]
            bound_textures = [
                resource
                for resource in bundle["resources"]
                if resource.get("meshBinding")
                and resource.get("semantic") != "SourceMesh"
            ]
            assert bound_textures, bundle["resources"]
            assert all(
                resource["meshBinding"]["sha256"] == mesh_hash
                for resource in bound_textures
            )
    finally:
        bake_worker.unregister()


if __name__ == "__main__":
    assert_corpus()
    documents = snapshot_all(FIXED)
    for name, document in documents.items():
        diagnostics = [
            (
                item.get("code"),
                item.get("severity"),
                item.get("message"),
            )
            for item in document.get("diagnostics", [])
            if item.get("severity") in {"error", "warning"}
        ]
        print(
            "MIKU_MAGIC_BALL_SNAPSHOT",
            name,
            json.dumps(document.get("surfaceContract"), ensure_ascii=False),
            json.dumps(diagnostics, ensure_ascii=False),
        )
    print("MIKU_MAGIC_BALL_CORPUS_SMOKE_OK")
