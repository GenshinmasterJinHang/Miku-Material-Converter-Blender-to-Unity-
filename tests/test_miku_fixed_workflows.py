import json
import re
import unittest
from pathlib import Path

from miku.contracts import validate_document
from miku.fixed_workflows import (
    FIXED_TEXTURE_ROLES,
    FIXED_WORKFLOWS,
    allowed_texture_role,
    genshin_required_texture_roles,
    infer_filename_texture_role,
    infer_wuwa_filename_texture_role,
    normalize_genshin_filename_role,
    normalize_texture_role,
    texture_role_color_space,
)
from miku.planner import ConversionPlanner
from miku.semantic import build_material_ir


ROOT = Path(__file__).resolve().parents[1]


class FixedWorkflowTests(unittest.TestCase):
    @staticmethod
    def _graph(workflow):
        return {
            "material": {"name": "FixedFixture"},
            "workflow": {
                "kind": workflow,
                **({"part": "Body"} if workflow != "standard_pbr" else {}),
            },
            "nodes": [],
            "edges": [],
        }

    def test_four_game_workflows_emit_material_ir_2(self):
        self.assertEqual(
            {"genshin_toon", "wuwa_toon", "hsr_toon", "endfield_toon"},
            FIXED_WORKFLOWS,
        )
        for workflow in sorted(FIXED_WORKFLOWS):
            with self.subTest(workflow=workflow):
                ir = build_material_ir(self._graph(workflow))
                self.assertEqual("miku-material-ir-2.0", ir["documentKind"])
                validate_document(ir, "miku-material-ir-2.0")
                plan = ConversionPlanner().plan(ir, mode="FullPBRBake")
                self.assertEqual("FixedWorkflowTextureBinding", plan["routePolicy"])
                self.assertEqual([], plan["bakeJobs"])

    def test_generic_workflow_is_rejected_without_fallback(self):
        with self.assertRaisesRegex(ValueError, r"MIKU_WORKFLOW_RETIRED:generic_toon"):
            build_material_ir(self._graph("generic_toon"), workflow_kind="generic_toon")

    def test_ir_1_generic_is_structurally_valid_but_planning_is_retired(self):
        ir = build_material_ir(
            {
                "material": {"name": "HistoricalGeneric"},
                "workflow": {"kind": "standard_pbr"},
                "nodes": [
                    {"id": "out", "op": "Output.Material"},
                    {"id": "surface", "op": "Shader.PrincipledBSDF"},
                ],
                "edges": [
                    {
                        "from": {"node": "surface", "socket": "Closure"},
                        "to": {"node": "out", "socket": "Surface"},
                    }
                ],
            }
        )
        ir["documentKind"] = "miku-material-ir-1.0"
        ir["schemaVersion"] = "1.0"
        ir["workflow"] = {"kind": "generic_toon"}
        # Rebuild the canonical hash after making the historical fixture.
        from miku.contracts import canonical_hash

        ir["canonicalHash"] = canonical_hash(
            {key: value for key, value in ir.items() if key != "canonicalHash"}
        )
        validate_document(ir, "miku-material-ir-1.0")
        with self.assertRaisesRegex(ValueError, r"MIKU_WORKFLOW_RETIRED:generic_toon"):
            ConversionPlanner().plan(ir)

    def test_role_aliases_are_strict_and_suffix_bounded(self):
        self.assertEqual("BaseMap", normalize_texture_role("MIKU:Albedo"))
        self.assertEqual("FaceSDF", normalize_texture_role("_FaceSDFMap"))
        self.assertEqual(
            "BodyCoolRamp",
            infer_filename_texture_role("Avatar_Body_Cool_Ramp.png"),
        )
        self.assertEqual("", infer_filename_texture_role("abnormalish.png"))
        self.assertTrue(allowed_texture_role("genshin_toon", "ShadowRampMap"))
        self.assertTrue(allowed_texture_role("genshin_toon", "NormalMap"))
        self.assertFalse(allowed_texture_role("genshin_toon", "SkinRamp"))
        self.assertEqual(
            "FaceSDF",
            infer_filename_texture_role("Avatar_FaceLightmap.png"),
        )
        self.assertEqual(
            "ShadowRampMap",
            infer_filename_texture_role("Avatar_Body_Shadow_Ramp.png"),
        )
        self.assertEqual(
            "HairRampMap",
            infer_filename_texture_role("Avatar_Hair_Shadow_Ramp.png"),
        )
        self.assertEqual(
            "NormalMap",
            infer_filename_texture_role("Avatar_Body_Normalmap.png"),
        )

    def test_genshin_parts_normalize_ramps_and_require_complete_inputs(self):
        self.assertEqual(
            "HairRampMap",
            normalize_genshin_filename_role("Hair", "ShadowRampMap"),
        )
        self.assertEqual(
            "ShadowRampMap",
            normalize_genshin_filename_role("Face", "HairRampMap"),
        )
        self.assertEqual(
            {"BaseMap", "LightMap", "ShadowRampMap"},
            set(genshin_required_texture_roles("Body")),
        )
        self.assertEqual(
            {"BaseMap", "FaceSDF", "ShadowRampMap"},
            set(genshin_required_texture_roles("Face")),
        )
        self.assertFalse(allowed_texture_role("generic_toon", "BaseMap"))
        self.assertTrue(
            allowed_texture_role("endfield_toon", "HairShadowMap")
        )
        self.assertTrue(
            allowed_texture_role("endfield_toon", "SpecularRefineF0")
        )
        self.assertTrue(
            allowed_texture_role("endfield_toon", "SpecularRefineColor")
        )
        self.assertIn("SpecularRefineF0", FIXED_TEXTURE_ROLES)
        self.assertIn("SpecularRefineColor", FIXED_TEXTURE_ROLES)
        self.assertEqual(
            "SpecularRefineF0",
            normalize_texture_role("MIKU:Specular Refine F0"),
        )
        self.assertEqual(
            "SpecularRefineColor",
            normalize_texture_role("specular_refine_color"),
        )
        self.assertEqual(
            "Linear",
            texture_role_color_space(["SpecularRefineF0"]),
        )
        self.assertEqual(
            "sRGB",
            texture_role_color_space(["SpecularRefineColor"]),
        )
        self.assertTrue(allowed_texture_role("wuwa_toon", "StockingsMap"))
        self.assertIn("EyeHET", FIXED_TEXTURE_ROLES)
        self.assertIn("EyeHDMF", FIXED_TEXTURE_ROLES)
        self.assertIn("EyeUpperHighlight", FIXED_TEXTURE_ROLES)
        self.assertIn("EyeLowerHighlight", FIXED_TEXTURE_ROLES)
        self.assertTrue(allowed_texture_role("wuwa_toon", "EyeHDMF"))
        self.assertEqual(
            "EyeHET",
            infer_filename_texture_role(
                "T_R2T1FeiBiMd10011Eye_HET.tga"
            ),
        )
        self.assertEqual(
            "MaterialParamMap",
            infer_filename_texture_role("Avatar_Material_Param_Map.png"),
        )
        self.assertEqual(
            "HairShadowMap",
            infer_filename_texture_role(
                "T_actor_common_hairshadow_01_M.png"
            ),
        )
        self.assertEqual(
            "FaceAreaMap",
            infer_filename_texture_role(
                "T_actor_common_female_face_01_cm_M.png"
            ),
        )
        self.assertEqual(
            "ColorLut",
            infer_filename_texture_role(
                "T_actor_common_female_skin_01_LUT_D.png"
            ),
        )
        self.assertEqual(
            "FaceRefineMap",
            infer_filename_texture_role(
                "T_actor_common_female_face_01_ST.png"
            ),
        )
        self.assertEqual(
            "HairRefineMap",
            infer_filename_texture_role("T_actor_aglina_hair_01_ST.png"),
        )
        self.assertEqual(
            "HairShiftMap",
            infer_filename_texture_role("T_actor_aglina_hairst_01_ST.png"),
        )
        self.assertEqual(
            "HairLineMap",
            infer_filename_texture_role("T_actor_common_hairline_01_M.png"),
        )
        self.assertEqual(
            "SplitNormalMap",
            infer_filename_texture_role("T_actor_aglina_hair_01_HN.png"),
        )
        self.assertEqual("", infer_filename_texture_role("unknown_M.png"))

    def test_game_shader_parts_use_shared_depth_rim_mask_contract(self):
        runtime = ROOT / "unity" / "Packages" / "com.miku.shaderconverter" / "Runtime"
        for family in ("Genshin", "Wuwa", "HSR"):
            for part in ("Body", "Hair", "Face"):
                path = runtime / family / f"{family}_{part}.shader"
                source = path.read_text(encoding="utf-8")
                self.assertEqual(1, source.count('Name "MikuToonCharacterMask"'), path)
                self.assertIn("Runtime/GameToon/MikuGameToonScreenRimPass.hlsl", source)
                if family == "Wuwa":
                    self.assertIn(
                        '_FresnelPower ("Fresnel Rim Power"',
                        source,
                    )
                    self.assertNotIn(
                        "[HideInInspector] _FresnelPower",
                        source,
                    )
                else:
                    self.assertIn("[HideInInspector] _FresnelPower", source)
                self.assertIn("[HideInInspector] _FresnelClamp", source)
        endfield = runtime / "Endfield"
        for part in ("Body", "Skin", "Hair", "Face"):
            source = (endfield / f"Endfield_{part}.shader").read_text(
                encoding="utf-8"
            )
            self.assertIn(
                'UsePass "Hidden/MIKU/Endfield/PassLibrary/MikuToonCharacterMask"',
                source,
            )
            self.assertIn(
                'UsePass "Hidden/MIKU/Endfield/PassLibrary/Outline"',
                source,
            )
        hair_shadow = (endfield / "Endfield_HairShadow.shader").read_text(
            encoding="utf-8"
        )
        self.assertIn("1.0 - mask", hair_shadow)
        self.assertNotIn("MikuToonCharacterMask", hair_shadow)
        self.assertNotIn('Name "Outline"', hair_shadow)
        game_toon = runtime / "GameToon"
        feature = (game_toon / "MikuToonScreenRimRendererFeature.cs").read_text(
            encoding="utf-8"
        )
        self.assertEqual(2, feature.count("SetGlobalTextureAfterPass("))
        self.assertNotIn("cmd.SetGlobalTexture(", feature)
        composite = (game_toon / "MikuToonScreenRimComposite.shader").read_text(
            encoding="utf-8"
        )
        self.assertIn('Hidden/Miku/GameToon/ScreenRimComposite', composite)
        self.assertIn("SampleSceneDepth", composite)

    def test_endfield_22_uses_renderer_object_space_and_packed_maps(self):
        common = (
            ROOT
            / "unity"
            / "Packages"
            / "com.miku.shaderconverter"
            / "Runtime"
            / "Endfield"
            / "EndfieldCommon.hlsl"
        ).read_text(encoding="utf-8")
        self.assertIn("GetObjectToWorldMatrix()", common)
        self.assertIn("float3(1.0, 0.0, 0.0)", common)
        self.assertIn("float3(0.0, -1.0, 0.0)", common)
        self.assertIn("float3(0.0, 0.0, 1.0)", common)
        self.assertIn("basis.backWS = -forward", common)
        self.assertIn("_HeadCenterOS", common)
        self.assertIn("_UseHeadSphereNormal", common)
        self.assertIn("const float size = 32.0", common)
        self.assertIn("size / width", common)
        self.assertIn("texelG / size", common)
        self.assertNotIn("1.0 - texelG / size", common)
        self.assertIn("baseSample.a", common)
        self.assertIn("material.r", common)
        self.assertIn("material.g", common)
        self.assertIn("material.b", common)
        self.assertIn("material.a", common)
        self.assertIn("EndfieldKajiyaKayLobe", common)
        self.assertIn("1.0 - tangentDotHalf * tangentDotHalf", common)
        self.assertIn("projectedRight", common)
        self.assertIn("scleraMode", common)
        self.assertIn("legacyMatcapSpecular", common)
        self.assertIn("tutorialMatcapBrdf", common)
        self.assertIn("primaryColor", common)
        self.assertIn("accessorySpecular", common)
        self.assertIn("_OverlayUseTintOnly", common)
        self.assertIn("complexion", common)
        self.assertNotIn("baseColor *= hairLine", common)

    def test_wuwa_authored_eye_face_basis_and_id_stockings_contract(self):
        runtime = (
            ROOT
            / "unity"
            / "Packages"
            / "com.miku.shaderconverter"
            / "Runtime"
            / "Wuwa"
        )
        eye = (runtime / "Wuwa_Eye.shader").read_text(encoding="utf-8")
        self.assertEqual(1, eye.count("_EyeHET, sampler_EyeHET"))
        self.assertEqual(1, eye.count("_EyeHDMF, sampler_EyeHDMF"))
        self.assertEqual(
            1,
            eye.count("SAMPLE_TEXTURE2D(\n                        _EyeUpperHighlight,"),
        )
        self.assertEqual(
            1,
            eye.count("SAMPLE_TEXTURE2D(\n                        _EyeLowerHighlight,"),
        )
        self.assertEqual(1, eye.count("_EyeEG, sampler_EyeEG"))
        self.assertIn("_EyeUpperHighlightOffset", eye)
        self.assertIn("_EyeLowerHighlightOffset", eye)
        self.assertIn("_EyeUpperHighlightScale", eye)
        self.assertIn("_EyeLowerHighlightScale", eye)
        self.assertIn("_EyeBaseEmissionStrength", eye)
        self.assertIn("float pupilMask = saturate(1.0 - hdmf.a)", eye)
        self.assertIn("baseColor * hetMask * lerp", eye)
        self.assertIn("const float gate = 0.0400000215", eye)
        self.assertIn("const float rampStart = 0.0803109035", eye)
        self.assertIn("const float rampEnd = 0.9041451216", eye)
        self.assertEqual(1, eye.count("hdmf.bbb"))
        self.assertNotIn("SAMPLE_TEXTURE2D(_EmissionMap", eye)

        body = (runtime / "Wuwa_Body.shader").read_text(encoding="utf-8")
        forward = body.split('Name "MikuToonCharacterMask"', 1)[0]
        self.assertEqual(1, forward.count("SAMPLE_TEXTURE2D(_IDMap"))
        self.assertIn("float3(0.2126, 0.7152, 0.0722)", forward)
        self.assertIn("idLuminance > 0.5", forward)
        self.assertIn("_WUWA_STOCKINGS_ON", forward)

        face = (runtime / "Wuwa_Face.shader").read_text(encoding="utf-8")
        self.assertIn("_FaceRight", face)
        self.assertIn("_FaceUp", face)
        self.assertIn("_FaceForward", face)
        self.assertIn("_FaceFlatness", face)
        self.assertIn("GetObjectToWorldMatrix()", face)
        self.assertIn("_MikuHeadAxesValid", face)

    def test_wuwa_packed_nrm_and_outline_roles_are_scoped(self):
        self.assertIn(
            "WuwaPackedNormalRoughnessMetallic",
            FIXED_TEXTURE_ROLES,
        )
        self.assertIn("OutlineColorMap", FIXED_TEXTURE_ROLES)
        self.assertTrue(
            allowed_texture_role(
                "wuwa_toon",
                "WuwaPackedNormalRoughnessMetallic",
            )
        )
        self.assertTrue(
            allowed_texture_role("wuwa_toon", "OutlineColorMap")
        )
        self.assertFalse(
            allowed_texture_role(
                "genshin_toon",
                "WuwaPackedNormalRoughnessMetallic",
            )
        )
        self.assertEqual(
            "WuwaPackedNormalRoughnessMetallic",
            infer_wuwa_filename_texture_role("Phoebe_Up_N.tga", "Body"),
        )
        self.assertEqual(
            "OutlineColorMap",
            infer_wuwa_filename_texture_role("Phoebe_Down_LD.tga", "Body"),
        )
        self.assertEqual(
            "FaceHET",
            infer_wuwa_filename_texture_role("Phoebe_Face_HET.tga", "Face"),
        )
        self.assertEqual(
            "",
            infer_wuwa_filename_texture_role("Phoebe_Up_Switch_D.tga", "Body"),
        )
        self.assertEqual(
            "Linear",
            texture_role_color_space(
                ["WuwaPackedNormalRoughnessMetallic"]
            ),
        )


if __name__ == "__main__":
    unittest.main()
