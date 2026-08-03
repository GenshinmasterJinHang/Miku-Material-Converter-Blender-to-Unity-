import math
import unittest

from miku_blender import _active_surface_node_ids, _fixed_image_uv_transform


def _socket(name, default=None):
    value = {
        "id": name,
        "name": name,
        "enabled": True,
        "isUnavailable": False,
    }
    if default is not None:
        value["default"] = default
    return value


class WuwaEyeBlenderGraphTests(unittest.TestCase):
    def _mix_graph(self, factor):
        return {
            "nodes": [
                {"id": "a", "op": "Texture.Image"},
                {"id": "b", "op": "Texture.Image"},
                {
                    "id": "mix",
                    "op": "Color.Mix",
                    "params": {"blend_type": "MIX"},
                    "inputs": [
                        {
                            **_socket("Factor", factor),
                            "valueType": "FLOAT",
                        },
                        _socket("A"),
                        _socket("B"),
                    ],
                },
                {
                    "id": "output",
                    "op": "Output.Material",
                    "params": {"isActiveOutput": True},
                },
            ],
            "edges": [
                {
                    "from": {"node": "a", "socket": "Color"},
                    "to": {"node": "mix", "socket": "A"},
                },
                {
                    "from": {"node": "b", "socket": "Color"},
                    "to": {"node": "mix", "socket": "B"},
                },
                {
                    "from": {"node": "mix", "socket": "Result"},
                    "to": {"node": "output", "socket": "Surface"},
                },
            ],
        }

    def test_constant_mix_uses_only_the_effective_endpoint(self):
        factor_zero = _active_surface_node_ids(self._mix_graph(0.0))
        self.assertIn("a", factor_zero)
        self.assertNotIn("b", factor_zero)

        factor_one = _active_surface_node_ids(self._mix_graph(1.0))
        self.assertNotIn("a", factor_one)
        self.assertIn("b", factor_one)

    def test_intermediate_mix_keeps_both_inputs(self):
        active = _active_surface_node_ids(self._mix_graph(0.5))
        self.assertIn("a", active)
        self.assertIn("b", active)

    def _mapping_graph(self, location, rotation, scale):
        return {
            "nodes": [
                {"id": "uv", "op": "Input.TextureCoordinate"},
                {
                    "id": "mapping",
                    "op": "Vector.Mapping",
                    "params": {"vectorType": "POINT"},
                    "inputs": [
                        _socket("Vector"),
                        _socket("Location", location),
                        _socket("Rotation", rotation),
                        _socket("Scale", scale),
                    ],
                },
                {"id": "image", "op": "Texture.Image"},
            ],
            "edges": [
                {
                    "from": {"node": "uv", "socket": "UV"},
                    "to": {"node": "mapping", "socket": "Vector"},
                },
                {
                    "from": {"node": "mapping", "socket": "Vector"},
                    "to": {"node": "image", "socket": "Vector"},
                },
            ],
        }

    def test_point_mapping_exports_scale_rotate_translate_affine(self):
        location = (0.13, -0.05, 0.2)
        rotation = (0.2, -0.3, 0.4)
        scale = (0.68, 1.27, 1.06)
        transform, reason = _fixed_image_uv_transform(
            self._mapping_graph(location, rotation, scale),
            "image",
        )
        self.assertEqual("", reason)
        self.assertEqual("UV0", transform["coordinateSpace"])
        self.assertEqual("Affine2D", transform["operation"])

        matrix = transform["matrix"]
        cx, sx = math.cos(rotation[0]), math.sin(rotation[0])
        cy, sy = math.cos(rotation[1]), math.sin(rotation[1])
        cz, sz = math.cos(rotation[2]), math.sin(rotation[2])
        expected = (
            cz * cy * scale[0],
            (cz * sy * sx - sz * cx) * scale[1],
            location[0],
            sz * cy * scale[0],
            (sz * sy * sx + cz * cx) * scale[1],
            location[1],
        )
        for actual, wanted in zip(matrix, expected):
            self.assertAlmostEqual(wanted, actual, places=7)

        for uv in ((0.0, 0.0), (0.5, 0.5), (0.91, 0.17)):
            actual = (
                matrix[0] * uv[0] + matrix[1] * uv[1] + matrix[2],
                matrix[3] * uv[0] + matrix[4] * uv[1] + matrix[5],
            )
            wanted = (
                expected[0] * uv[0] + expected[1] * uv[1] + expected[2],
                expected[3] * uv[0] + expected[4] * uv[1] + expected[5],
            )
            self.assertAlmostEqual(wanted[0], actual[0], places=7)
            self.assertAlmostEqual(wanted[1], actual[1], places=7)

    def test_point_mapping_rejects_non_finite_defaults(self):
        transform, reason = _fixed_image_uv_transform(
            self._mapping_graph(
                (float("nan"), 0.0, 0.0),
                (0.0, 0.0, 0.0),
                (1.0, 1.0, 1.0),
            ),
            "image",
        )
        self.assertIsNone(transform)
        self.assertIn("non-finite", reason)


if __name__ == "__main__":
    unittest.main()
