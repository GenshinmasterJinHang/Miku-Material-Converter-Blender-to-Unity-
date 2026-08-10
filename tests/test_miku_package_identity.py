import json
import hashlib
import re
import tarfile
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "unity" / "Packages" / "com.miku.shaderconverter"
IDENTITY = ROOT / "docs" / "provenance" / "miku-unity-package-asset-identity.json"
GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})$", re.MULTILINE)


class MikuPackageIdentityTests(unittest.TestCase):
    def test_package_is_mit_and_versioned(self):
        package = json.loads((PACKAGE / "package.json").read_text(encoding="utf8"))
        self.assertEqual(package["name"], "com.miku.shaderconverter")
        self.assertEqual(package["version"], "2.3.0")
        self.assertEqual(package["license"], "MIT")
        self.assertEqual(package["unity"], "6000.0")
        self.assertEqual(
            package["dependencies"]["com.unity.render-pipelines.universal"],
            "17.0.0",
        )

    def test_public_miku_importer_and_binding_exist(self):
        self.assertTrue((PACKAGE / "Editor" / "MikuBundleImporter.cs").is_file())
        self.assertTrue((PACKAGE / "Runtime" / "MikuMaterialBinding.cs").is_file())
        self.assertIn("namespace Miku.ShaderConverter", (PACKAGE / "Editor" / "MikuBundleImporter.cs").read_text(encoding="utf8"))

    def test_only_miku_bundle_and_diagnostic_miku_importers_are_registered(self):
        bundle = (PACKAGE / "Editor" / "MikuBundleScriptedImporter.cs").read_text(
            encoding="utf8"
        )
        legacy = (PACKAGE / "Editor" / "MikuLegacyMgirImporter.cs").read_text(
            encoding="utf8"
        )
        self.assertIn('[ScriptedImporter(1, "migrbundle")]', bundle)
        self.assertIn('[ScriptedImporter(1, "mgir")]', legacy)
        self.assertIn("re-export", legacy.lower())
        self.assertFalse(any((PACKAGE / "Editor").glob("B2U*.cs")))

    def test_every_package_meta_has_one_unique_unity_guid(self):
        records = {}
        for meta in PACKAGE.rglob("*.meta"):
            match = GUID_RE.search(meta.read_text(encoding="utf8"))
            self.assertIsNotNone(match, meta.relative_to(PACKAGE).as_posix())
            guid = match.group(1)
            self.assertNotIn(guid, records, f"{guid}: {records.get(guid)} and {meta}")
            records[guid] = meta.relative_to(PACKAGE).as_posix()
        manifest = json.loads(IDENTITY.read_text(encoding="utf8"))
        self.assertEqual(len(manifest["assets"]), len(records))

    def test_every_imported_package_folder_and_asset_has_meta(self):
        for item in PACKAGE.rglob("*"):
            relative = item.relative_to(PACKAGE)
            if any(part.endswith("~") for part in relative.parts):
                continue
            if item.is_dir():
                self.assertTrue(Path(str(item) + ".meta").is_file(), relative.as_posix())
            elif item.is_file() and item.suffix != ".meta":
                self.assertTrue(Path(str(item) + ".meta").is_file(), relative.as_posix())

    def test_declared_unity_samples_are_present_and_nonempty(self):
        from tools.build_miku_unity_package import validate_declared_samples

        declared_files = validate_declared_samples()
        package = json.loads((PACKAGE / "package.json").read_text(encoding="utf8"))
        if package.get("samples"):
            self.assertTrue(declared_files)

    def test_sample_validation_rejects_missing_and_empty_directories(self):
        from tools.build_miku_unity_package import validate_declared_samples

        with tempfile.TemporaryDirectory() as temporary:
            package = Path(temporary)
            (package / "package.json").write_text(
                json.dumps({"samples": [{"path": "Samples~/Example"}]}),
                encoding="utf-8",
            )
            with self.assertRaisesRegex(ValueError, "does not exist"):
                validate_declared_samples(package)

            sample = package / "Samples~" / "Example"
            sample.mkdir(parents=True)
            with self.assertRaisesRegex(ValueError, "is empty"):
                validate_declared_samples(package)

            readme = sample / "README.md"
            readme.write_text("# Example\n", encoding="utf-8")
            self.assertEqual((readme,), validate_declared_samples(package))

    def test_checked_in_identity_manifest_matches_package(self):
        manifest = json.loads(IDENTITY.read_text(encoding="utf8"))
        self.assertEqual("miku-package-asset-identity-1.0", manifest["schema"])
        expected = {
            item["metaPath"]: item["guid"]
            for item in manifest["assets"]
        }
        actual = {}
        for meta in PACKAGE.rglob("*.meta"):
            match = GUID_RE.search(meta.read_text(encoding="utf8"))
            self.assertIsNotNone(match)
            actual[meta.relative_to(PACKAGE).as_posix()] = match.group(1)
        self.assertEqual(expected, actual)

    def test_semantic_lit_shader_keeps_pre_cutover_guid(self):
        meta = PACKAGE / "Runtime" / "StandardPBR" / "Miku_StandardPBR_SemanticLit.shader.meta"
        self.assertIn("guid: 10e4314ce3554d94a3d70d965890cbbc", meta.read_text(encoding="utf8"))

    def test_deterministic_tgz_preserves_complete_identity_manifest(self):
        from tools.build_miku_unity_package import build

        artifact = build()
        self.assertEqual("com.miku.shaderconverter-2.3.0.tgz", artifact.name)
        first = artifact.read_bytes()
        second = build().read_bytes()
        self.assertEqual(first, second)
        manifest = json.loads(IDENTITY.read_text(encoding="utf8"))
        expected = {
            "package/" + item["metaPath"]: item["guid"]
            for item in manifest["assets"]
        }
        with tarfile.open(build(), "r:gz") as archive:
            archive_names = {member.name for member in archive.getmembers()}
            from tools.build_miku_unity_package import validate_declared_samples

            for sample_file in validate_declared_samples():
                expected_name = "package/" + sample_file.relative_to(PACKAGE).as_posix()
                self.assertIn(expected_name, archive_names)
            actual = {}
            for member in archive.getmembers():
                if not member.name.endswith(".meta"):
                    continue
                stream = archive.extractfile(member)
                self.assertIsNotNone(stream)
                match = GUID_RE.search(stream.read().decode("utf8"))
                self.assertIsNotNone(match, member.name)
                actual[member.name] = match.group(1)
        self.assertEqual(expected, actual)

    def test_template_profile_has_no_project_local_or_gpl_hlsl_dependency(self):
        template_root = PACKAGE / "Templates"
        graph = template_root / "MikuStandardTemplate.shadergraph"
        clear_coat = template_root / "MikuClearCoatTemplate.shadergraph"
        alpha_blend = template_root / "MikuAlphaBlendTemplate.shadergraph"
        dithered = template_root / "MikuDitheredTemplate.shadergraph"
        dielectric = template_root / "MikuDielectricTemplate.shadergraph"
        subgraph = template_root / "MikuStandardTemplate.generated.shadersubgraph"
        subgraph_text = subgraph.read_text(encoding="utf8")
        self.assertNotIn("Assets/MikuReview", subgraph_text)
        self.assertNotIn("0e9f9e7c9e2d4c15a8e8f0d11b4ac11d", subgraph_text)
        from miku.planner import default_target_profile

        hashes = default_target_profile()["implementationHashes"]
        self.assertEqual(hashlib.sha256(graph.read_bytes()).hexdigest(), hashes["shaderGraphWrapper"])
        self.assertEqual(
            hashlib.sha256(clear_coat.read_bytes()).hexdigest(),
            hashes["clearCoatWrapper"],
        )
        self.assertEqual(
            hashlib.sha256(alpha_blend.read_bytes()).hexdigest(),
            hashes["alphaBlendWrapper"],
        )
        self.assertEqual(
            hashlib.sha256(dithered.read_bytes()).hexdigest(),
            hashes["ditheredWrapper"],
        )
        self.assertEqual(
            hashlib.sha256(dielectric.read_bytes()).hexdigest(),
            hashes["dielectricWrapper"],
        )
        toon_root = PACKAGE / "Runtime" / "GameToon"
        family = hashlib.sha256()
        for path in sorted(
            toon_root.iterdir(), key=lambda item: item.name
        ):
            if path.suffix == ".meta":
                continue
            relative = "Runtime/GameToon/" + path.name
            family.update(relative.encode("utf-8"))
            family.update(b"\0")
            family.update(path.read_bytes())
        self.assertEqual(
            family.hexdigest(),
            hashes["gameToonScreenRim"],
        )
        self.assertEqual(hashlib.sha256(subgraph.read_bytes()).hexdigest(), hashes["generatedSubGraph"])
        runtime = PACKAGE / "Editor" / "MikuShaderGraph17RuntimeBackend.cs"
        registry = PACKAGE / "Editor" / "MikuSurfaceModelBackends.cs"
        for backend in (runtime, registry):
            source = backend.read_text(encoding="utf8")
            self.assertNotIn("Assets/", source)
            self.assertNotIn("GPL", source)
        self.assertEqual(
            hashlib.sha256(runtime.read_bytes()).hexdigest(),
            hashes["runtimeStructuredBackend"],
        )
        workflow_registry = PACKAGE / "Editor" / "MikuWorkflowBackends.cs"
        self.assertEqual(
            hashlib.sha256(workflow_registry.read_bytes()).hexdigest(),
            hashes["workflowBackendRegistry"],
        )
        self.assertEqual(
            hashlib.sha256(registry.read_bytes()).hexdigest(),
            hashes["surfaceModelRegistry"],
        )
        multi_lobe = (
            PACKAGE
            / "Runtime"
            / "SurfaceModels"
            / "MikuMultiLobeLighting.hlsl"
        )
        self.assertEqual(
            hashlib.sha256(multi_lobe.read_bytes()).hexdigest(),
            hashes["multiLobeLighting"],
        )
        importer = (PACKAGE / "Editor" / "MikuBundleImporter.cs").read_text(encoding="utf8")
        self.assertIn(default_target_profile()["canonicalHash"], importer)

    def test_standard_pbr_wrapper_has_only_canonical_visible_controls(self):
        from tools.build_miku_standard_pbr_wrapper import (
            PUBLIC_EMISSION_REFS,
            PUBLIC_REFS,
            PUBLIC_SURFACE_REFS,
            STANDARD_WRAPPER,
            build_standard_wrapper,
            parse_multi_json,
            property_reference,
        )

        self.assertEqual(
            STANDARD_WRAPPER.read_text(encoding="utf8"),
            build_standard_wrapper(),
        )
        objects = parse_multi_json(STANDARD_WRAPPER)
        graph = objects[0]
        by_id = {
            item["m_ObjectId"]: item
            for item in objects
            if item.get("m_ObjectId")
        }
        properties = [
            item
            for item in objects
            if "ShaderProperty" in item.get("m_Type", "")
        ]
        visible = [
            property_reference(item)
            for item in properties
            if item["m_GeneratePropertyBlock"] and not item["m_Hidden"]
        ]
        self.assertCountEqual(PUBLIC_REFS, visible)
        self.assertEqual(47, len(properties))
        for item in properties:
            if property_reference(item) in PUBLIC_REFS:
                continue
            self.assertFalse(item["m_GeneratePropertyBlock"])
            self.assertTrue(item["overrideHLSLDeclaration"])
            self.assertEqual(2, item["hlslDeclarationOverride"])
            self.assertTrue(item["m_Hidden"])
            if item["m_Type"].endswith("Texture2DShaderProperty"):
                self.assertFalse(item["useTilingAndOffset"])
                self.assertFalse(item["useTexelSize"])
                self.assertFalse(item["isHDR"])

        categories = [by_id[item["m_Id"]] for item in graph["m_CategoryData"]]
        self.assertEqual(["Surface Inputs", "Emission", ""], [
            item["m_Name"] for item in categories
        ])
        category_refs = [
            [property_reference(by_id[child["m_Id"]]) for child in item["m_ChildObjectList"]]
            for item in categories
        ]
        self.assertEqual(list(PUBLIC_SURFACE_REFS), category_refs[0])
        self.assertEqual(list(PUBLIC_EMISSION_REFS), category_refs[1])

        nodes = {
            item.get("m_Name"): item
            for item in objects
            if item.get("m_Type", "").endswith("Node")
        }
        for expected in (
            "Base Color Tint",
            "Metalness Strength",
            "Clamp Metalness",
            "Smoothness To Roughness",
            "Roughness Strength",
            "Clamp Roughness",
            "Roughness To Smoothness",
            "Normal Strength",
            "Emission Tint",
            "Emission Strength",
            "Clamp Occlusion",
        ):
            self.assertIn(expected, nodes)
        subgraph_node = next(
            item
            for item in objects
            if item.get("m_Type") == "UnityEditor.ShaderGraph.SubGraphNode"
        )
        block_ids = {
            item["m_ObjectId"]
            for item in objects
            if item.get("m_Type") == "UnityEditor.ShaderGraph.BlockNode"
            and item["m_Name"].startswith("SurfaceDescription.")
        }
        direct_output_edges = [
            edge
            for edge in graph["m_Edges"]
            if edge["m_OutputSlot"]["m_Node"]["m_Id"] == subgraph_node["m_ObjectId"]
            and edge["m_InputSlot"]["m_Node"]["m_Id"] in block_ids
        ]
        self.assertEqual([], direct_output_edges)

    def test_generic_toon_assets_and_entries_are_absent(self):
        retired_root = PACKAGE / "Runtime" / "GenericToon"
        self.assertFalse(retired_root.exists() and any(retired_root.rglob("*")))
        for path in PACKAGE.rglob("*"):
            if not path.is_file() or path.suffix == ".meta":
                continue
            text = path.read_text(encoding="utf8", errors="ignore")
            if "MIKU_WORKFLOW_RETIRED:generic_toon" in text:
                continue
            self.assertNotIn("Miku/GenericToon/", text, path)


if __name__ == "__main__":
    unittest.main()
