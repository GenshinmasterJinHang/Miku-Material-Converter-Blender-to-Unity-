import unittest
from types import SimpleNamespace

from miku_blender.versioning import (
    blender_targa_png_strategy,
    classify_blender_version,
    require_blender_capabilities,
    require_supported_blender,
)


class MikuBlenderVersionTests(unittest.TestCase):
    def test_closed_supported_range_and_certified_version(self):
        cases = (
            ((5, 0, 0), True, False),
            ((5, 0, 99), True, False),
            ((5, 1, 7), True, False),
            ((5, 2, 0), True, True),
            ((5, 2, 1), True, False),
            ((5, 3, 0), False, False),
            ((4, 5, 8), False, False),
            ((6, 0, 0), False, False),
        )
        for version, supported, certified in cases:
            with self.subTest(version=version):
                compatibility = classify_blender_version(version)
                self.assertEqual(supported, compatibility.supported)
                self.assertEqual(certified, compatibility.certified)

    def test_each_supported_technical_line_has_an_explicit_adapter(self):
        self.assertEqual(
            "Blender50Adapter",
            classify_blender_version((5, 0, 1)).adapter_name,
        )
        self.assertEqual(
            "Blender51Adapter",
            classify_blender_version((5, 1, 2)).adapter_name,
        )
        self.assertEqual(
            "Blender52Adapter",
            classify_blender_version((5, 2, 0)).adapter_name,
        )
        self.assertEqual(
            "ImageDatablock",
            blender_targa_png_strategy((5, 0, 1)),
        )
        self.assertEqual(
            "ImageDatablock",
            blender_targa_png_strategy((5, 1, 2)),
        )
        self.assertEqual(
            "MemoryBuffer",
            blender_targa_png_strategy((5, 2, 0)),
        )

    def test_missing_runtime_capability_fails_with_structured_diagnostic(self):
        fake = SimpleNamespace(
            app=SimpleNamespace(
                version=(5, 1, 2),
                translations=SimpleNamespace(register=lambda: None),
            ),
            types=SimpleNamespace(),
            props=SimpleNamespace(),
        )
        with self.assertRaisesRegex(
            RuntimeError,
            "MIKU_BLENDER_CAPABILITY_MISSING.*Blender51Adapter",
        ):
            require_blender_capabilities(fake)

    def test_in_range_lower_version_has_unvalidated_diagnostic(self):
        compatibility = require_supported_blender((5, 1, 2))
        self.assertEqual(
            "MIKU_BLENDER_VERSION_UNVALIDATED:"
            "actual=5.1.2:certified=5.2.0",
            compatibility.diagnostic,
        )
        compatibility = require_supported_blender((5, 2, 1))
        self.assertEqual(
            "MIKU_BLENDER_VERSION_UNVALIDATED:"
            "actual=5.2.1:certified=5.2.0",
            compatibility.diagnostic,
        )

    def test_wrong_major_versions_fail(self):
        for version in ((4, 99, 99), (5, 3, 0), (6, 0, 0)):
            with self.subTest(version=version):
                with self.assertRaisesRegex(
                    RuntimeError,
                    "MIKU_BLENDER_VERSION_UNSUPPORTED",
                ):
                    require_supported_blender(version)


if __name__ == "__main__":
    unittest.main()
