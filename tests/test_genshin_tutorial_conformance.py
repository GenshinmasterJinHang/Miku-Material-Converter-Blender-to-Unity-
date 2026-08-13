from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "unity" / "Packages" / "com.miku.shaderconverter" / "Runtime"


class GenshinTutorialConformanceTests(unittest.TestCase):
    def test_common_hlsl_exposes_tutorial_contracts(self):
        source = (RUNTIME / "Genshin" / "GenshinCommon.hlsl").read_text(encoding="utf-8")
        for contract in (
            "Genshin_TutorialRampRow",
            "Genshin_OutlineVertexMask",
            "saturate(vertexColor.g)",
            "Genshin_OutlineRegionColor",
            "step(0.25, lightMap.a)",
            "smoothstep(0.2, 0.3, saturate(lightMapGreen))",
            "Genshin_TutorialSpecular",
            "Genshin_TutorialMetalMask",
            "Genshin_DiffuseAlphaEmission",
            "smoothstep(0.0, 1.0, saturate(baseAlpha))",
            "sin(_Time.w * max(flicker, 0.0))",
            "Genshin_DecodeNormalMap",
            "Genshin_DiffuseAlphaClip",
            "smoothstep(0.05, 0.7, saturate(baseAlpha))",
        ):
            self.assertIn(contract, source)

    def test_body_and_hair_use_explicit_backface_pass_with_uv1(self):
        for name in ("Genshin_Body.shader", "Genshin_Hair.shader"):
            source = (RUNTIME / "Genshin" / name).read_text(encoding="utf-8")
            self.assertIn("_GENSHIN_DOUBLE_SIDED", source)
            self.assertIn('Name "MikuGenshinBackface"', source)
            self.assertIn('Tags { "LightMode"="MikuGenshinBackface" }', source)
            self.assertIn("Cull Front", source)
            self.assertIn("Cull Back", source)
            self.assertIn("float2 uv1 : TEXCOORD1", source)
            self.assertNotIn("SV_IsFrontFace", source)
            self.assertIn("_BackUV1", source)
            self.assertIn("_UseUv1Backface", source)
            self.assertIn("Genshin_DerivativeTangentFrame", source)
            self.assertIn("Genshin_OutlineVertexMask(input.vertexColor)", source)

    def test_body_and_hair_support_optional_normal_map(self):
        for name in ("Genshin_Body.shader", "Genshin_Hair.shader"):
            source = (RUNTIME / "Genshin" / name).read_text(encoding="utf-8")
            self.assertIn('_NormalMap ("Normal Map", 2D) = "bump"', source)
            self.assertIn("_BumpScale", source)
            self.assertIn("_GENSHIN_NORMALMAP_ON", source)
            self.assertIn("tangentWS : TEXCOORD6", source)
            self.assertIn("bitangentWS : TEXCOORD7", source)
            self.assertIn("Genshin_DecodeNormalMap", source)
            self.assertIn("TransformTangentToWorld", source)

    def test_body_skin_tone_is_gated_by_skin_mask(self):
        source = (RUNTIME / "Genshin" / "Genshin_Body.shader").read_text(encoding="utf-8")
        self.assertIn(
            "lerp(diffuse, Genshin_ReferenceSkinTone(diffuse, _HighlightCompression), skinMask)",
            source,
        )

    def test_face_keeps_cull_back_and_gets_alpha_modes(self):
        source = (RUNTIME / "Genshin" / "Genshin_Face.shader").read_text(encoding="utf-8")
        self.assertIn("Cull Back", source)
        self.assertIn("_DiffuseA", source)
        self.assertIn("Genshin_ApplyBaseAlphaCoverage", source)
        self.assertIn("_OutlineColor0", source)
        self.assertNotIn("_GENSHIN_DOUBLE_SIDED", source)

    def test_all_three_shaders_expose_diffuse_alpha_and_ramp_rows(self):
        for name in ("Genshin_Body.shader", "Genshin_Hair.shader", "Genshin_Face.shader"):
            source = (RUNTIME / "Genshin" / name).read_text(encoding="utf-8")
            for prop in (
                "_DiffuseA",
                "_Cutoff",
                "_Glow",
                "_Flicker",
                "_LightmapA0",
                "_LightmapA4",
                "_OutlineColorMode",
                "_OutlineColor4",
            ):
                self.assertIn(prop, source, name)


if __name__ == "__main__":
    unittest.main()
