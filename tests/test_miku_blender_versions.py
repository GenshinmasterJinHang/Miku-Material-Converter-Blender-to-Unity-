import unittest

from miku_blender.versioning import (
    classify_blender_version,
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
            ((5, 3, 0), True, False),
            ((4, 5, 8), False, False),
            ((6, 0, 0), False, False),
        )
        for version, supported, certified in cases:
            with self.subTest(version=version):
                compatibility = classify_blender_version(version)
                self.assertEqual(supported, compatibility.supported)
                self.assertEqual(certified, compatibility.certified)

    def test_in_range_lower_version_has_unvalidated_diagnostic(self):
        compatibility = require_supported_blender((5, 1, 2))
        self.assertEqual(
            "MIKU_BLENDER_VERSION_UNVALIDATED:"
            "actual=5.1.2:certified=5.2.0",
            compatibility.diagnostic,
        )
        above_ceiling = require_supported_blender((5, 3, 0))
        self.assertEqual(
            "MIKU_BLENDER_VERSION_UNVALIDATED:"
            "actual=5.3.0:certified=5.2.0",
            above_ceiling.diagnostic,
        )

    def test_wrong_major_versions_fail(self):
        for version in ((4, 99, 99), (6, 0, 0)):
            with self.subTest(version=version):
                with self.assertRaisesRegex(
                    RuntimeError,
                    "MIKU_BLENDER_VERSION_UNSUPPORTED",
                ):
                    require_supported_blender(version)


if __name__ == "__main__":
    unittest.main()
