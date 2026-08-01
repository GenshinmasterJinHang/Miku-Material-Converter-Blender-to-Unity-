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
        function = source.split("float Wuwa_FaceSDFLight", 1)[1].split("float Wuwa_HairShadowMask", 1)[0]
        self.assertIn("dot(fixedLightDirectionWS, headRightWS) > 0.0", function)
        self.assertIn("1.0 - (dot(fixedLightDirectionWS, headForwardWS)", function)


if __name__ == "__main__":
    unittest.main()
