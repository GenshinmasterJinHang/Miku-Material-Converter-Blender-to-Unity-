from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "unity" / "Packages" / "com.miku.shaderconverter" / "Runtime"


class FaceSdfLightDirectionTests(unittest.TestCase):
    def test_genshin_face_sdf_uses_body_surface_to_light_convention(self):
        source = (RUNTIME / "Genshin" / "GenshinCommon.hlsl").read_text(encoding="utf-8")
        function = source.split("float Genshin_FaceSDFShadow", 1)[1].split("float3 Genshin_FaceDiffuse", 1)[0]
        self.assertIn("lightDirWS = normalize(lightDirWS);", function)
        self.assertNotIn("lightDirWS = -normalize(lightDirWS);", function)
        self.assertIn("1.0 - (0.5 * FDotL + 0.5)", function)
        self.assertIn("RDotL > 0.0 ? float2(1.0 - uv.x, uv.y) : uv", function)

    def test_hsr_face_sdf_keeps_the_visible_fbx_horizontal_handedness(self):
        source = (RUNTIME / "HSR" / "HSRCommon.hlsl").read_text(encoding="utf-8")
        function = source.split("float HSR_FaceSDFShadow", 1)[1].split("float HSR_FaceAO", 1)[0]
        self.assertNotIn("headRightWS = -headRightWS;", function)
        self.assertIn("1.0 - (dot(fixedLightDirectionWS, headForwardWS)", function)
        self.assertIn("if (lightSide > 0.0)", function)

    def test_wuwa_face_sdf_uses_the_same_surface_to_light_side_test(self):
        source = (RUNTIME / "Wuwa" / "WuwaCommon.hlsl").read_text(encoding="utf-8")
        side_mask = source.split("float Wuwa_FaceSDFSideMask", 1)[1].split(
            "float Wuwa_FaceSDFLight", 1
        )[0]
        function = source.split("float Wuwa_FaceSDFLight", 1)[1].split("float Wuwa_HairShadowMask", 1)[0]
        self.assertEqual(side_mask.count("smoothstep("), 2)
        self.assertNotRegex(side_mask, r"(?<!smooth)step\s*\(")
        self.assertIn("mainMask * lerp(1.0, softMask", side_mask)
        self.assertIn("dot(fixedLightDirectionWS, headRightWS)", function)
        self.assertIn("1.0 - (dot(fixedLightDirectionWS, headForwardWS)", function)
        self.assertIn("float2(1.0 - uv.x, uv.y)", function)
        self.assertIn("smoothstep(-safeMirrorBlendWidth", function)
        self.assertIn(
            "lerp(unmirroredMask, mirroredMask, mirrorWeight)",
            function,
        )
        self.assertNotIn("step(sdfThreshold, sdfValue)", function)
        self.assertNotIn("sign(", function)

    def test_wuwa_eye_keeps_surface_highlights_out_of_iris_parallax(self):
        source = (RUNTIME / "Wuwa" / "Wuwa_Eye.shader").read_text(encoding="utf-8")
        fragment = source.split("half4 WuwaEyeFrag", 1)[1].split("ENDHLSL", 1)[0]
        self.assertIn("float2 surfaceUV = input.uv;", fragment)
        self.assertIn("float2 irisUV = saturate(surfaceUV - parallaxOffset);", fragment)
        self.assertIn("TRANSFORM_TEX(irisUV, _BaseMap)", fragment)
        self.assertIn("sampler_EyeHET, irisUV", fragment)
        self.assertIn("sampler_EyeHDMF, irisUV", fragment)
        self.assertIn("WuwaEyeAffineUV(\n                        surfaceUV,", fragment)
        self.assertNotIn("WuwaEyeAffineUV(\n                        irisUV,", fragment)


if __name__ == "__main__":
    unittest.main()
