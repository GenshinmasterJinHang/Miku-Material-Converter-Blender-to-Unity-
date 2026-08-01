import json
import tempfile
import unittest
from pathlib import Path

from miku.bundle import (
    compute_sealed_digest,
    make_file_reference,
    normalize_relative_path,
    stage_bundle_artifacts,
    validate_bundle_document,
)
from miku.contracts import DocumentValidationError, canonical_json, make_document
from miku.planner import default_target_profile


class MikuBundleSecurityTests(unittest.TestCase):
    def test_canonical_json_normalizes_negative_zero_for_cross_language_hashes(self):
        self.assertEqual(canonical_json({"value": -0.0}), '{"value":0.0}')

    def _bundle(
        self,
        root: Path,
        resources=None,
        *,
        kind: str = "miku-bundle-1.0",
    ):
        root.mkdir(parents=True, exist_ok=True)
        references = {}
        kinds = {
            "ir": "miku-material-ir-1.0",
            "plan": "miku-conversion-plan-1.0",
            "manifest": "miku-conversion-manifest-1.0",
            "sourceMap": "miku-blender-source-map-1.0",
        }
        for role, document_kind in kinds.items():
            document = make_document(document_kind, {"role": role})
            path = root / f"material.{role}.json"
            path.write_text(json.dumps(document, ensure_ascii=False), encoding="utf-8")
            references[role] = make_file_reference(root, path, media_type="application/json")
        references["sourceMap"]["editorOnly"] = True
        payload = {
            "materialKey": "Metal",
            "sourceName": "Metal",
            "persistentSourceId": "source-1",
            "persistentMaterialId": "material-1",
            "targetProfileHash": default_target_profile()["canonicalHash"],
            **references,
            "resources": list(resources or []),
        }
        payload["sealedDigest"] = compute_sealed_digest(payload)
        return make_document(kind, payload)

    def test_valid_document_and_staged_bytes_round_trip(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "bundle"
            bundle = self._bundle(root)
            path = root / "material.mikubundle"
            path.write_text(json.dumps(bundle, ensure_ascii=False), encoding="utf-8")
            validate_bundle_document(bundle)
            staged, staged_root = stage_bundle_artifacts(path, Path(temp) / "stage")
            self.assertEqual(bundle["canonicalHash"], staged["canonicalHash"])
            self.assertTrue((staged_root / bundle["ir"]["relativePath"]).is_file())

    def test_legacy_hash_only_reference_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            bundle = self._bundle(Path(temp))
            bundle["ir"] = {"documentHash": "0" * 64}
            bundle["canonicalHash"] = "0" * 64
            with self.assertRaises(DocumentValidationError):
                validate_bundle_document(bundle)

    def test_pending_target_profile_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            bundle = self._bundle(Path(temp))
            bundle["targetProfileHash"] = "pending-unity-17.4-profile"
            bundle["sealedDigest"] = compute_sealed_digest(bundle)
            bundle = make_document(
                "miku-bundle-1.0",
                {key: value for key, value in bundle.items() if key not in {"documentKind", "schemaVersion", "toolVersion", "id", "canonicalHash"}},
            )
            with self.assertRaises(DocumentValidationError) as raised:
                validate_bundle_document(bundle)
            self.assertEqual("MIKU_TARGET_PROFILE_INVALID", raised.exception.code)

    def test_path_traversal_and_reserved_names_are_rejected(self):
        for value in ("../secret.json", "C:/secret.json", "/secret.json", "AUX/data.json", "folder./data.json"):
            with self.subTest(value=value), self.assertRaises(DocumentValidationError):
                normalize_relative_path(value)

    def test_casefold_colliding_artifact_paths_are_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            bundle = self._bundle(root)
            bundle["plan"]["relativePath"] = bundle["ir"]["relativePath"].upper()
            bundle["sealedDigest"] = compute_sealed_digest(bundle)
            bundle = make_document(
                "miku-bundle-1.0",
                {key: value for key, value in bundle.items() if key not in {"documentKind", "schemaVersion", "toolVersion", "id", "canonicalHash"}},
            )
            with self.assertRaises(DocumentValidationError) as raised:
                validate_bundle_document(bundle)
            self.assertEqual("MIKU_ARTIFACT_PATH_DUPLICATE", raised.exception.code)

    def test_tampered_source_is_rejected_while_streaming_to_stage(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "bundle"
            bundle = self._bundle(root)
            path = root / "material.mikubundle"
            path.write_text(json.dumps(bundle, ensure_ascii=False), encoding="utf-8")
            (root / bundle["ir"]["relativePath"]).write_text("tampered", encoding="utf-8")
            with self.assertRaises(DocumentValidationError) as raised:
                stage_bundle_artifacts(path, Path(temp) / "stage")
            self.assertIn(raised.exception.code, {"MIKU_ARTIFACT_SIZE_MISMATCH", "MIKU_ARTIFACT_HASH_MISMATCH"})

    def test_missing_texture_is_rejected_while_streaming_to_stage(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "bundle"
            image = root / "height.jpg"
            root.mkdir(parents=True, exist_ok=True)
            image.write_bytes(b"sealed-jpeg-bytes")
            resource = make_file_reference(
                root,
                image,
                media_type="image/jpeg",
            )
            resource.update(
                {
                    "id": "height",
                    "semantic": "Height",
                    "bindingKey": "Height",
                    "usage": "Scalar",
                    "channel": "R",
                    "colorSpace": "Linear",
                    "width": 2,
                    "height": 2,
                    "channelCount": 3,
                    "componentBytes": 1,
                    "uvSet": "UV0",
                    "projection": "FLAT",
                    "interpolation": "LINEAR",
                    "extension": "REPEAT",
                }
            )
            bundle = self._bundle(
                root,
                [resource],
                kind="miku-bundle-1.0",
            )
            path = root / "material.mikubundle"
            path.write_text(
                json.dumps(bundle, ensure_ascii=False),
                encoding="utf-8",
            )
            image.unlink()
            with self.assertRaises(DocumentValidationError) as raised:
                stage_bundle_artifacts(path, Path(temp) / "stage")
            self.assertEqual("MIKU_ARTIFACT_MISSING", raised.exception.code)

    def test_normal_resource_requires_explicit_convention(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "normal.png"
            image.write_bytes(b"not-an-image-but-hashed")
            resource = make_file_reference(root, image, media_type="image/png")
            resource.update(
                {
                    "id": "normal",
                    "semantic": "Normal",
                    "channel": "RGB",
                    "colorSpace": "Linear",
                    "width": 1,
                    "height": 1,
                    "channelCount": 3,
                    "componentBytes": 1,
                }
            )
            bundle = self._bundle(root, [resource])
            with self.assertRaises(DocumentValidationError) as raised:
                validate_bundle_document(bundle)
            self.assertEqual("MIKU_NORMAL_CONVENTION_INVALID", raised.exception.code)

    def test_linear_ior_resource_is_valid_in_bundle_1_0(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "ior.png"
            image.write_bytes(b"sealed-ior")
            resource = make_file_reference(root, image, media_type="image/png")
            resource.update(
                {
                    "id": "ior",
                    "semantic": "IOR",
                    "bindingKey": "_MIKU_Baked_IOR",
                    "usage": "Scalar",
                    "channel": "R",
                    "colorSpace": "Linear",
                    "width": 1,
                    "height": 1,
                    "channelCount": 1,
                    "componentBytes": 1,
                    "decodeScale": 9.0,
                    "decodeBias": 1.0,
                }
            )
            validate_bundle_document(self._bundle(root, [resource]))

    def test_bundle_2_2_accepts_directx_normal_convention(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "normal.png"
            image.write_bytes(b"sealed-normal")
            resource = make_file_reference(root, image, media_type="image/png")
            resource.update(
                {
                    "id": "normal",
                    "semantic": "Normal",
                    "bindingKey": "Normal",
                    "usage": "Normal",
                    "channel": "RGB",
                    "colorSpace": "Linear",
                    "normalConvention": "TangentDirectXNegativeY",
                    "width": 1,
                    "height": 1,
                    "channelCount": 3,
                    "componentBytes": 1,
                }
            )
            validate_bundle_document(
                self._bundle(
                    root,
                    [resource],
                    kind="miku-bundle-1.0",
                )
            )

    def test_bundle_2_2_accepts_arbitrary_linear_scalar_channel_bindings(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "packed.png"
            image.write_bytes(b"sealed-packed")
            resource = make_file_reference(root, image, media_type="image/png")
            resource.update(
                {
                    "id": "packed",
                    "semantic": "AmbientOcclusion",
                    "bindingKey": "_MIKU_Packed_0123456789abcdef0123",
                    "usage": "Scalar",
                    "channel": "B",
                    "channelBindings": [
                        {"semantic": "AmbientOcclusion", "channel": "B"},
                        {"semantic": "Metalness", "channel": "R"},
                        {"semantic": "Roughness", "channel": "G"},
                        {"semantic": "Alpha", "channel": "A"},
                    ],
                    "colorSpace": "Linear",
                    "width": 1,
                    "height": 1,
                    "channelCount": 4,
                    "componentBytes": 1,
                }
            )
            validate_bundle_document(
                self._bundle(
                    root,
                    [resource],
                    kind="miku-bundle-1.0",
                )
            )

    def test_packed_channel_bindings_reject_srgb_and_invalid_channels(self):
        for color_space, channel, expected in (
            (
                "sRGB",
                "G",
                "MIKU_PACKED_RESOURCE_COLOR_SPACE_CONFLICT",
            ),
            (
                "Linear",
                "RGB",
                "MIKU_CHANNEL_BINDING_CHANNEL_INVALID",
            ),
        ):
            with self.subTest(color_space=color_space, channel=channel):
                with tempfile.TemporaryDirectory() as temp:
                    root = Path(temp)
                    image = root / "packed.png"
                    image.write_bytes(b"sealed-packed")
                    resource = make_file_reference(
                        root,
                        image,
                        media_type="image/png",
                    )
                    resource.update(
                        {
                            "id": "packed",
                            "semantic": "Metalness",
                            "bindingKey": "_MIKU_Packed_invalid",
                            "usage": "Scalar",
                            "channel": "R",
                            "channelBindings": [
                                {"semantic": "Metalness", "channel": "R"},
                                {"semantic": "Roughness", "channel": channel},
                            ],
                            "colorSpace": color_space,
                            "width": 1,
                            "height": 1,
                            "channelCount": 4,
                            "componentBytes": 1,
                        }
                    )
                    with self.assertRaises(DocumentValidationError) as raised:
                        validate_bundle_document(
                            self._bundle(
                                root,
                                [resource],
                                kind="miku-bundle-1.0",
                            )
                        )
                    self.assertEqual(expected, raised.exception.code)

    def test_expression_island_resource_round_trip(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "island.exr"
            image.write_bytes(b"not-an-image-but-hashed")
            resource = make_file_reference(root, image, media_type="image/x-exr")
            resource.update(
                {
                    "id": "island-color",
                    "semantic": "ExpressionIsland",
                    "bindingKey": "_MIKU_Baked_0123456789abcdef",
                    "expressionId": "expression-1",
                    "usage": "Color",
                    "channel": "RGB",
                    "colorSpace": "Linear",
                    "width": 1,
                    "height": 1,
                    "channelCount": 3,
                    "componentBytes": 2,
                }
            )
            validate_bundle_document(self._bundle(root, [resource]))

    def test_expression_island_normal_requires_explicit_convention(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "island-normal.png"
            image.write_bytes(b"not-an-image-but-hashed")
            resource = make_file_reference(root, image, media_type="image/png")
            resource.update(
                {
                    "id": "island-normal",
                    "semantic": "ExpressionIsland",
                    "bindingKey": "_MIKU_Baked_fedcba9876543210",
                    "expressionId": "expression-normal",
                    "usage": "Normal",
                    "channel": "RGB",
                    "colorSpace": "Linear",
                    "width": 1,
                    "height": 1,
                    "channelCount": 3,
                    "componentBytes": 1,
                }
            )
            bundle = self._bundle(root, [resource])
            with self.assertRaises(DocumentValidationError) as raised:
                validate_bundle_document(bundle)
            self.assertEqual("MIKU_NORMAL_CONVENTION_INVALID", raised.exception.code)

    def test_bundle_2_2_accepts_jpeg_height_resource_without_source_mesh(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "height.jpg"
            image.write_bytes(b"sealed-jpeg-bytes")
            resource = make_file_reference(
                root,
                image,
                media_type="image/jpeg",
            )
            resource.update(
                {
                    "id": "height",
                    "semantic": "Height",
                    "bindingKey": "Height",
                    "usage": "Scalar",
                    "channel": "R",
                    "colorSpace": "Linear",
                    "width": 2,
                    "height": 2,
                    "channelCount": 3,
                    "componentBytes": 1,
                    "uvSet": "UV0",
                    "projection": "FLAT",
                    "interpolation": "LINEAR",
                    "extension": "REPEAT",
                }
            )
            bundle = self._bundle(
                root,
                [resource],
                kind="miku-bundle-1.0",
            )
            validate_bundle_document(bundle)

    def test_miku_1_bundle_accepts_jpeg_height_resource(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            image = root / "height.jpg"
            image.write_bytes(b"sealed-jpeg-bytes")
            resource = make_file_reference(
                root,
                image,
                media_type="image/jpeg",
            )
            resource.update(
                {
                    "id": "height",
                    "semantic": "Height",
                    "channel": "R",
                    "colorSpace": "Linear",
                    "width": 2,
                    "height": 2,
                    "channelCount": 3,
                    "componentBytes": 1,
                }
            )
            bundle = self._bundle(
                root,
                [resource],
                kind="miku-bundle-1.0",
            )
            self.assertEqual(bundle, validate_bundle_document(bundle))


if __name__ == "__main__":
    unittest.main()
