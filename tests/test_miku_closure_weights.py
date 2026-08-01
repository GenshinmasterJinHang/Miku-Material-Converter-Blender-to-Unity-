from __future__ import annotations

import unittest

from miku.closure_ir import (
    AddShaderEnergyPolicy,
    ClosureBudget,
    ClosureGraphBuilder,
    ClosureSimplifier,
    ClosureWeightFlattener,
    FidelityPolicy,
    build_weighted_closure_set,
    evaluate_weight,
)
from miku.socket_conversion import (
    ColorManagementContext,
    ImplicitSocketConversionRegistry,
)
from miku.contracts import canonical_hash, make_document, validate_document
from miku.migrations import migrate_legacy_material_ir
from miku.planner import ConversionPlanner
from miku.surface_models import SurfaceModelKind, build_surface_model_plan
from miku.semantic import build_material_ir


def socket(
    socket_id: str,
    default=None,
    value_type: str = "Float",
    **extra,
) -> dict:
    return {
        "id": socket_id,
        "name": socket_id.replace("_", " "),
        "default": default,
        "valueType": value_type,
        **extra,
    }


def endpoint(node: str, socket_id: str) -> dict[str, str]:
    return {"node": node, "socket": socket_id}


def leaf(node_id: str, op: str) -> dict:
    inputs = {
        "Shader.PrincipledBSDF": [
            socket("Base_Color", [0.8, 0.8, 0.8, 1.0], "Color"),
            socket("Metallic", 0.0),
            socket("Roughness", 0.5),
            socket("IOR", 1.5),
            socket("Alpha", 1.0),
            socket("Weight", 0.0, enabled=False, isUnavailable=True),
        ],
        "Shader.DiffuseBSDF": [
            socket("Color", [0.8, 0.8, 0.8, 1.0], "Color"),
            socket("Roughness", 0.0),
            socket("Normal", [0.0, 0.0, 1.0], "Vector3"),
            socket("Weight", 0.0, enabled=False, isUnavailable=True),
        ],
        "Shader.GlossyBSDF": [
            socket("Color", [0.8, 0.8, 0.8, 1.0], "Color"),
            socket("Roughness", 0.2),
            socket("Anisotropy", 0.0),
            socket("Normal", [0.0, 0.0, 1.0], "Vector3"),
            socket("Weight", 0.0, enabled=False, isUnavailable=True),
        ],
        "Shader.SubsurfaceScattering": [
            socket("Color", [0.8, 0.4, 0.3, 1.0], "Color"),
            socket("Scale", 0.1),
            socket("Radius", [1.0, 0.2, 0.1], "Vector3"),
            socket("IOR", 1.4),
            socket("Anisotropy", 0.0),
            socket("Normal", [0.0, 0.0, 1.0], "Vector3"),
            socket("Weight", 0.0, enabled=False, isUnavailable=True),
        ],
        "Shader.Emission": [
            socket("Color", [1.0, 0.2, 0.1, 1.0], "Color"),
            socket("Strength", 2.0),
            socket("Weight", 0.0, enabled=False, isUnavailable=True),
        ],
        "Shader.TransparentBSDF": [
            socket("Color", [1.0, 1.0, 1.0, 1.0], "Color"),
            socket("Weight", 0.0, enabled=False, isUnavailable=True),
        ],
        "Shader.GlassBSDF": [
            socket("Color", [0.8, 0.9, 1.0, 1.0], "Color"),
            socket("Roughness", 0.1),
            socket("IOR", 1.45),
            socket("Normal", [0.0, 0.0, 1.0], "Vector3"),
            socket("Weight", 0.0, enabled=False, isUnavailable=True),
        ],
    }[op]
    return {
        "id": node_id,
        "op": op,
        "inputs": inputs,
        "outputs": [socket("Closure", None, "Closure")],
        "source": {"groupPath": ["Material"]},
    }


def closure_graph(
    operation: str,
    first_op: str,
    second_op: str,
    *,
    factor: float = 0.5,
) -> dict:
    composite_op = (
        "Shader.Mix" if operation == "Mix" else "Shader.Add"
    )
    nodes = [
        {
            "id": "out",
            "op": "Output.Material",
            "inputs": [socket("Surface", None, "Closure")],
            "params": {"isActiveOutput": True, "target": "EEVEE"},
        },
        {
            "id": "composite",
            "op": composite_op,
            "inputs": [
                socket("Fac", factor),
                socket("Shader", None, "Closure"),
                socket("Shader_001", None, "Closure"),
            ],
            "outputs": [socket("Shader", None, "Closure")],
            "source": {"groupPath": ["Material"]},
        },
        leaf("first", first_op),
        leaf("second", second_op),
    ]
    return {
        "material": {"name": "ClosureFixture"},
        "nodes": nodes,
        "edges": [
            {
                "from": endpoint("first", "Closure"),
                "to": endpoint("composite", "Shader"),
            },
            {
                "from": endpoint("second", "Closure"),
                "to": endpoint("composite", "Shader_001"),
            },
            {
                "from": endpoint("composite", "Shader"),
                "to": endpoint("out", "Surface"),
            },
        ],
    }


def single_leaf_graph(op: str = "Shader.PrincipledBSDF") -> dict:
    return {
        "material": {"name": "SingleClosureFixture"},
        "nodes": [
            {
                "id": "out",
                "op": "Output.Material",
                "inputs": [socket("Surface", None, "Closure")],
                "params": {"isActiveOutput": True, "target": "EEVEE"},
            },
            leaf("surface", op),
        ],
        "edges": [
            {
                "from": endpoint("surface", "Closure"),
                "to": endpoint("out", "Surface"),
            }
        ],
    }


class SocketConversionTests(unittest.TestCase):
    def test_blender_52_scalar_and_color_conversions(self):
        registry = ImplicitSocketConversionRegistry(
            ColorManagementContext(
                (0.2126, 0.7152, 0.0722),
                "test-config",
            )
        )
        self.assertEqual(
            [0.25, 0.25, 0.25, 1.0],
            registry.convert(0.25, "Float", "Color"),
        )
        self.assertAlmostEqual(
            0.2126,
            registry.convert([1.0, 0.0, 0.0, 0.0], "Color", "Float"),
        )
        self.assertAlmostEqual(
            2.0,
            registry.convert([1.0, 2.0, 3.0], "Vector3", "Float"),
        )
        document = registry.resolve("Color", "Float").to_document()
        self.assertEqual(
            "blender-5.2-implicit-v1",
            document["conversionAlgorithmVersion"],
        )
        self.assertEqual(
            [0.2126, 0.7152, 0.0722],
            document["colorManagement"]["luminanceCoefficients"],
        )


class ClosureWeightTests(unittest.TestCase):
    def test_mix_clamps_and_distributes_weight_in_socket_order(self):
        for factor, expected_first, expected_second in (
            (-1.0, 1.0, 0.0),
            (0.0, 1.0, 0.0),
            (0.25, 0.75, 0.25),
            (0.5, 0.5, 0.5),
            (0.75, 0.25, 0.75),
            (1.0, 0.0, 1.0),
            (2.0, 0.0, 1.0),
        ):
            with self.subTest(factor=factor):
                closure, weighted, _ = build_weighted_closure_set(
                    closure_graph(
                        "Mix",
                        "Shader.DiffuseBSDF",
                        "Shader.Emission",
                        factor=factor,
                    )
                )
                terms = {
                    item["source"]["nodeId"]: item
                    for item in weighted["terms"]
                }
                self.assertAlmostEqual(
                    expected_first,
                    evaluate_weight(terms["first"]["finalWeight"]),
                )
                self.assertAlmostEqual(
                    expected_second,
                    evaluate_weight(terms["second"]["finalWeight"]),
                )
                self.assertEqual("Mix", closure["root"]["kind"])
                self.assertEqual(
                    ["Fac", "Shader", "Shader_001"],
                    closure["root"]["sourceSocketOrder"],
                )

    def test_generic_toon_backfacing_mix_weight_is_native_runtime_expression(self):
        graph = closure_graph(
            "Mix",
            "Shader.DiffuseBSDF",
            "Shader.Emission",
        )
        graph["workflow"] = {"kind": "generic_toon"}
        graph["nodes"].append(
            {
                "id": "geometry",
                "op": "Input.Geometry",
                "outputs": [
                    socket(
                        "Backfacing",
                        0.0,
                        "Float",
                        space="None",
                        stage="Fragment",
                        uniformity="Varying",
                    )
                ],
            }
        )
        graph["edges"].append(
            {
                "from": endpoint("geometry", "Backfacing"),
                "to": endpoint("composite", "Fac"),
            }
        )

        ir = build_material_ir(
            graph,
            workflow_kind="generic_toon",
            material_key="generic-toon-backfacing",
        )
        validate_document(ir, "miku-material-ir-1.0")
        errors = [
            item
            for item in ir["diagnostics"]
            if str(item.get("severity") or "").lower() == "error"
        ]
        self.assertEqual([], errors)
        self.assertCountEqual(
            ["Input.IsFrontFace", "Math.OneMinus"],
            [item["op"] for item in ir["expressions"]],
        )

        def source_weight_nodes(value):
            if isinstance(value, dict):
                if (
                    value.get("kind") == "ViewDependent"
                    and isinstance(value.get("source"), dict)
                ):
                    yield value
                for child in value.values():
                    yield from source_weight_nodes(child)
            elif isinstance(value, list):
                for child in value:
                    yield from source_weight_nodes(child)

        weight_sources = list(
            source_weight_nodes(ir["weightedClosures"]["terms"])
        )
        self.assertGreaterEqual(len(weight_sources), 2)
        self.assertTrue(all(item.get("expressionId") for item in weight_sources))
        self.assertTrue(all("requiresBake" not in item for item in weight_sources))
        mix_region = next(
            item
            for item in ir["regions"]
            if item.get("kind") == "SurfaceMix"
        )
        self.assertEqual("Runtime", mix_region["dynamism"])
        plan = ConversionPlanner().plan(ir, mode="Auto")
        self.assertEqual([], plan["bakeJobs"])

    def test_unconnected_mix_branch_is_exact_zero_closure(self):
        graph = closure_graph(
            "Mix",
            "Shader.DiffuseBSDF",
            "Shader.Emission",
            factor=0.25,
        )
        graph["edges"] = [
            edge
            for edge in graph["edges"]
            if edge["to"] != endpoint("composite", "Shader")
        ]

        closure, weighted, diagnostics = build_weighted_closure_set(graph)

        self.assertEqual("Null", closure["root"]["first"]["kind"])
        self.assertEqual(1, len(weighted["terms"]))
        self.assertEqual(
            "second",
            weighted["terms"][0]["source"]["nodeId"],
        )
        self.assertAlmostEqual(
            0.25,
            evaluate_weight(weighted["terms"][0]["finalWeight"]),
        )
        self.assertTrue(
            any(
                item.get("code") == "MIKU_NULL_CLOSURE_IMPLICIT"
                for item in diagnostics
            )
        )

        material_ir = build_material_ir(
            graph,
            source_blend_id="blend",
            material_key="unconnected-mix",
        )
        validate_document(material_ir, "miku-material-ir-1.0")
        self.assertEqual(
            "Null",
            material_ir["closureGraph"]["root"]["first"]["kind"],
        )

    def test_add_copies_parent_weight_without_normalization(self):
        closure, weighted, _ = build_weighted_closure_set(
            closure_graph(
                "Add",
                "Shader.DiffuseBSDF",
                "Shader.TransparentBSDF",
            )
        )
        self.assertEqual("Add", closure["root"]["kind"])
        self.assertEqual(
            [1.0, 1.0],
            sorted(
                evaluate_weight(item["finalWeight"])
                for item in weighted["terms"]
            ),
        )
        plan = build_surface_model_plan(
            "material",
            closure,
            weighted,
        )
        self.assertEqual(
            SurfaceModelKind.TRANSPARENT_LIT.value,
            plan["kind"],
        )
        self.assertEqual(
            "PreserveBlender",
            plan["closureLoweringPlan"]["addShaderEnergyPolicy"],
        )
        self.assertTrue(
            any(
                diagnostic["code"] == "WEIGHT0003"
                for diagnostic in plan["diagnostics"]
            )
        )

    def test_principled_emission_mix_factor_is_an_emission_mask(self):
        graph = closure_graph(
            "Mix",
            "Shader.PrincipledBSDF",
            "Shader.Emission",
        )
        graph["nodes"].append(
            {
                "id": "emission-mask",
                "op": "Texture.Image",
                "inputs": [socket("Vector", [0.0, 0.0, 0.0], "Vector3")],
                "outputs": [
                    socket("Color", [0.0, 0.0, 0.0, 1.0], "Color"),
                    socket("Alpha", 1.0),
                ],
                "params": {
                    "image": {
                        "resourceBaseId": "emission-mask-image",
                        "source": "FILE",
                        "fileFormat": "PNG",
                        "width": 4,
                        "height": 4,
                        "channels": 4,
                        "colorSpaceName": "Non-Color",
                    },
                    "projection": "FLAT",
                    "interpolation": "LINEAR",
                    "extension": "REPEAT",
                },
            }
        )
        graph["edges"].append(
            {
                "from": endpoint("emission-mask", "Color"),
                "to": endpoint("composite", "Fac"),
            }
        )
        ir = build_material_ir(graph, material_key="EmissionMask")
        mask = next(
            expression
            for expression in ir["expressions"]
            if expression["op"] == "Texture.SampleImage2D"
        )
        self.assertEqual("EmissionMask", mask["params"]["semantic"])
        self.assertEqual("_MIKU_EmissionMask", mask["params"]["referenceName"])
        self.assertEqual("R", mask["params"]["channel"])

    def test_nested_mix_add_preserves_every_symbolic_path(self):
        graph = closure_graph(
            "Add",
            "Shader.DiffuseBSDF",
            "Shader.Emission",
        )
        outer = graph["nodes"][1]
        inner = {
            "id": "inner",
            "op": "Shader.Mix",
            "inputs": [
                socket("Fac", 0.25),
                socket("Shader", None, "Closure"),
                socket("Shader_001", None, "Closure"),
            ],
            "outputs": [socket("Shader", None, "Closure")],
            "source": {"groupPath": ["Material"]},
        }
        graph["nodes"].extend(
            [inner, leaf("third", "Shader.TransparentBSDF")]
        )
        graph["edges"] = [
            edge
            for edge in graph["edges"]
            if edge["to"] != endpoint("composite", "Shader")
        ]
        graph["edges"].extend(
            [
                {
                    "from": endpoint("first", "Closure"),
                    "to": endpoint("inner", "Shader"),
                },
                {
                    "from": endpoint("third", "Closure"),
                    "to": endpoint("inner", "Shader_001"),
                },
                {
                    "from": endpoint("inner", "Shader"),
                    "to": endpoint("composite", "Shader"),
                },
            ]
        )
        _, weighted, _ = build_weighted_closure_set(graph)
        weights = {
            item["source"]["nodeId"]: evaluate_weight(item["finalWeight"])
            for item in weighted["terms"]
        }
        self.assertEqual(
            {"first": 0.75, "second": 1.0, "third": 0.25},
            weights,
        )

    def test_dynamic_layer_weight_color_ramp_remains_symbolic(self):
        graph = closure_graph(
            "Mix",
            "Shader.TransparentBSDF",
            "Shader.Emission",
        )
        graph["nodes"].extend(
            [
                {
                    "id": "layer",
                    "op": "Input.LayerWeight",
                    "inputs": [
                        socket("Blend", 0.5),
                        socket("Normal", [0.0, 0.0, 1.0], "Vector3"),
                    ],
                    "outputs": [socket("Facing", None, "Float")],
                },
                {
                    "id": "ramp",
                    "op": "Color.Ramp",
                    "inputs": [socket("Fac", 0.0)],
                    "outputs": [socket("Color", None, "Color")],
                },
            ]
        )
        graph["edges"].extend(
            [
                {
                    "from": endpoint("layer", "Facing"),
                    "to": endpoint("ramp", "Fac"),
                },
                {
                    "from": endpoint("ramp", "Color"),
                    "to": endpoint("composite", "Fac"),
                },
            ]
        )
        closure = ClosureGraphBuilder(graph).build()
        factor = closure["root"]["factor"]
        self.assertEqual("Clamp", factor["kind"])
        conversion = factor["input"]
        self.assertEqual("ImplicitConversion", conversion["kind"])
        self.assertEqual(
            "ColorToFloatLuminance",
            conversion["conversion"]["conversionKind"],
        )
        ramp = conversion["input"]
        self.assertEqual("Math", ramp["kind"])
        self.assertEqual(
            "LayerWeight",
            ramp["inputs"]["Fac"]["kind"],
        )
        terms = ClosureWeightFlattener().flatten(closure["root"])
        self.assertEqual(2, len(terms["terms"]))
        with self.assertRaises(KeyError):
            evaluate_weight(terms["terms"][0]["finalWeight"])

    def test_identical_terms_merge_only_by_summing_weights(self):
        graph = closure_graph(
            "Mix",
            "Shader.DiffuseBSDF",
            "Shader.DiffuseBSDF",
            factor=0.25,
        )
        # Point both Mix inputs at one actual closure leaf to model a DAG.
        graph["edges"][1]["from"] = endpoint("first", "Closure")
        closure = ClosureGraphBuilder(graph).build()
        raw = ClosureWeightFlattener().flatten(closure["root"])
        simplified = ClosureSimplifier().simplify(raw)
        self.assertEqual(1, len(simplified["terms"]))
        self.assertAlmostEqual(
            1.0,
            evaluate_weight(simplified["terms"][0]["finalWeight"]),
        )
        self.assertEqual(
            "IdenticalSemanticTermWeightSum-v1",
            simplified["simplifications"][0]["algorithm"],
        )


class SurfaceModelTests(unittest.TestCase):
    def test_eevee_subsurface_is_an_explicit_diffuse_lobe_approximation(self):
        closure, weighted, _ = build_weighted_closure_set(
            closure_graph(
                "Mix",
                "Shader.SubsurfaceScattering",
                "Shader.GlossyBSDF",
            )
        )
        plan = build_surface_model_plan(
            "material",
            closure,
            weighted,
        )
        self.assertEqual(
            SurfaceModelKind.CUSTOM_MULTI_LOBE.value,
            plan["kind"],
        )
        self.assertTrue(
            any(
                item["code"]
                == "MIKU_EEVEE_DIFFUSE_CLOSURE_APPROXIMATION"
                for item in plan["diagnostics"]
            )
        )

    def test_surface_model_routing(self):
        fixtures = (
            (
                "Mix",
                "Shader.TransparentBSDF",
                "Shader.Emission",
                SurfaceModelKind.TRANSPARENT_EMISSION,
            ),
            (
                "Mix",
                "Shader.TransparentBSDF",
                "Shader.DiffuseBSDF",
                SurfaceModelKind.TRANSPARENT_LIT,
            ),
            (
                "Mix",
                "Shader.TransparentBSDF",
                "Shader.GlassBSDF",
                SurfaceModelKind.REFRACTIVE_GLASS,
            ),
            (
                "Add",
                "Shader.DiffuseBSDF",
                "Shader.DiffuseBSDF",
                SurfaceModelKind.CUSTOM_MULTI_LOBE,
            ),
        )
        for operation, first, second, expected in fixtures:
            with self.subTest(expected=expected.value):
                closure, weighted, _ = build_weighted_closure_set(
                    closure_graph(operation, first, second)
                )
                plan = build_surface_model_plan(
                    "material",
                    closure,
                    weighted,
                )
                self.assertEqual(expected.value, plan["kind"])

    def test_strict_rejects_low_quality_glass_and_energy_approximation(self):
        closure, weighted, _ = build_weighted_closure_set(
            closure_graph(
                "Mix",
                "Shader.TransparentBSDF",
                "Shader.GlassBSDF",
            )
        )
        strict = build_surface_model_plan(
            "material",
            closure,
            weighted,
            fidelity_policy=FidelityPolicy.STRICT,
        )
        self.assertEqual(
            SurfaceModelKind.UNSUPPORTED_SURFACE.value,
            strict["kind"],
        )

        add_closure, add_weighted, _ = build_weighted_closure_set(
            closure_graph(
                "Add",
                "Shader.DiffuseBSDF",
                "Shader.Emission",
            )
        )
        normalized = build_surface_model_plan(
            "material",
            add_closure,
            add_weighted,
            fidelity_policy=FidelityPolicy.STRICT,
            add_energy_policy=(
                AddShaderEnergyPolicy.ENERGY_CONSERVING_APPROXIMATION
            ),
        )
        self.assertEqual(
            SurfaceModelKind.UNSUPPORTED_SURFACE.value,
            normalized["kind"],
        )

    def test_budget_excess_never_silently_drops_lobes(self):
        closure, weighted, _ = build_weighted_closure_set(
            closure_graph(
                "Add",
                "Shader.DiffuseBSDF",
                "Shader.DiffuseBSDF",
            )
        )
        plan = build_surface_model_plan(
            "material",
            closure,
            weighted,
            budget=ClosureBudget(max_lobes=1),
        )
        self.assertEqual(
            SurfaceModelKind.UNSUPPORTED_SURFACE.value,
            plan["kind"],
        )
        self.assertEqual(2, len(weighted["terms"]))
        self.assertTrue(
            any(
                diagnostic["code"] == "WEIGHT0006"
                for diagnostic in plan["diagnostics"]
            )
        )

    def test_custom_lighting_reports_ssao_boundary_and_strict_rejects_it(self):
        closure, weighted, _ = build_weighted_closure_set(
            closure_graph(
                "Add",
                "Shader.DiffuseBSDF",
                "Shader.DiffuseBSDF",
            )
        )
        auto = build_surface_model_plan("material", closure, weighted)
        self.assertEqual(
            SurfaceModelKind.CUSTOM_MULTI_LOBE.value,
            auto["kind"],
        )
        self.assertEqual("Approximate", auto["fidelity"])
        self.assertTrue(
            any(
                diagnostic["code"]
                == "MIKU_CUSTOM_LIGHTING_SSAO_UNAVAILABLE"
                for diagnostic in auto["diagnostics"]
            )
        )

        strict = build_surface_model_plan(
            "material",
            closure,
            weighted,
            fidelity_policy=FidelityPolicy.STRICT,
        )
        self.assertEqual(
            SurfaceModelKind.UNSUPPORTED_SURFACE.value,
            strict["kind"],
        )

    def test_linked_per_lobe_normal_is_an_explicit_realtime_approximation(self):
        closure, weighted, _ = build_weighted_closure_set(
            closure_graph(
                "Add",
                "Shader.DiffuseBSDF",
                "Shader.DiffuseBSDF",
            )
        )
        weighted["terms"][0]["parameters"]["Normal"] = {
            "kind": "ValueExpression",
            "expressionId": "normal-expression",
            "valueType": "Vector3",
        }
        plan = build_surface_model_plan("material", closure, weighted)
        self.assertEqual(
            SurfaceModelKind.CUSTOM_MULTI_LOBE.value,
            plan["kind"],
        )
        self.assertTrue(
            any(
                item.get("feature", "").endswith(":per-lobe-normal")
                for diagnostic in plan["diagnostics"]
                if diagnostic["code"]
                == "MIKU_EEVEE_SURFACE_PARAMETER_APPROXIMATION"
                for item in diagnostic.get("features", [])
            )
        )

    def test_single_principled_linked_normal_is_a_global_surface_normal(self):
        closure, weighted, _ = build_weighted_closure_set(single_leaf_graph())
        weighted["terms"][0]["parameters"]["Normal"] = {
            "kind": "ValueExpression",
            "expressionId": "normal-expression",
            "valueType": "Vector3",
        }
        plan = build_surface_model_plan("material", closure, weighted)
        self.assertEqual(SurfaceModelKind.OPAQUE_PBR.value, plan["kind"])
        self.assertFalse(
            any(
                item.get("feature", "").endswith(":per-lobe-normal")
                for diagnostic in plan["diagnostics"]
                for item in diagnostic.get("features", [])
            )
        )

    def test_shared_normal_across_multiple_lobes_is_not_per_lobe(self):
        closure, weighted, _ = build_weighted_closure_set(
            closure_graph(
                "Add",
                "Shader.DiffuseBSDF",
                "Shader.DiffuseBSDF",
            )
        )
        shared = {
            "kind": "ValueExpression",
            "expressionId": "shared-normal-expression",
            "valueType": "Vector3",
        }
        for term in weighted["terms"]:
            term["parameters"]["Normal"] = dict(shared)
        plan = build_surface_model_plan("material", closure, weighted)
        self.assertEqual(SurfaceModelKind.CUSTOM_MULTI_LOBE.value, plan["kind"])
        self.assertFalse(
            any(
                item.get("feature", "").endswith(":per-lobe-normal")
                for diagnostic in plan["diagnostics"]
                for item in diagnostic.get("features", [])
            )
        )

    def test_principled_coat_maps_to_declared_urp_approximation(self):
        closure, weighted, _ = build_weighted_closure_set(single_leaf_graph())
        parameters = weighted["terms"][0]["parameters"]
        parameters.update(
            {
                "Coat Weight": {"kind": "Constant", "value": 0.25},
                "Coat Roughness": {"kind": "Constant", "value": 0.03},
                "Coat IOR": {"kind": "Constant", "value": 1.5},
                "Coat Tint": {
                    "kind": "Constant",
                    "value": [1.0, 1.0, 1.0, 1.0],
                },
                "Coat Normal": {
                    "kind": "Constant",
                    "value": [0.0, 0.0, 0.0],
                },
            }
        )

        auto = build_surface_model_plan("material", closure, weighted)
        self.assertEqual(SurfaceModelKind.OPAQUE_PBR.value, auto["kind"])
        self.assertEqual("Approximate", auto["fidelity"])
        self.assertTrue(
            auto["closureLoweringPlan"]["standardLitCompatibility"][
                "compatible"
            ]
        )
        self.assertEqual("Urp17ClearCoat", auto["approximations"][0]["kind"])
        self.assertEqual(
            "warning",
            next(
                item["severity"]
                for item in auto["diagnostics"]
                if item["code"] == "MIKU_COAT_URP_APPROXIMATION"
            ),
        )

        strict = build_surface_model_plan(
            "material",
            closure,
            weighted,
            fidelity_policy=FidelityPolicy.STRICT,
        )
        self.assertEqual(
            SurfaceModelKind.UNSUPPORTED_SURFACE.value,
            strict["kind"],
        )
        self.assertEqual(
            "error",
            next(
                item["severity"]
                for item in strict["diagnostics"]
                if item["code"] == "MIKU_COAT_URP_APPROXIMATION"
            ),
        )

    def test_nondefault_principled_coat_extensions_are_declared_approximations(self):
        fixtures = {
            "Coat IOR": {"kind": "Constant", "value": 1.45},
            "Coat Tint": {
                "kind": "Constant",
                "value": [0.8, 0.9, 1.0, 1.0],
            },
            "Coat Normal": {
                "kind": "ValueExpression",
                "expressionId": "coat-normal-expression",
            },
        }
        for parameter_name, value in fixtures.items():
            with self.subTest(parameter=parameter_name):
                closure, weighted, _ = build_weighted_closure_set(
                    single_leaf_graph()
                )
                parameters = weighted["terms"][0]["parameters"]
                parameters["Coat Weight"] = {
                    "kind": "Constant",
                    "value": 0.25,
                }
                parameters[parameter_name] = value
                plan = build_surface_model_plan(
                    "material",
                    closure,
                    weighted,
                )
                self.assertEqual(
                    SurfaceModelKind.OPAQUE_PBR.value,
                    plan["kind"],
                )
                features = [
                    item["feature"]
                    for diagnostic in plan["diagnostics"]
                    for item in diagnostic.get("features", [])
                ]
                self.assertIn(
                    f"Principled:{parameter_name}",
                    features,
                )
                strict = build_surface_model_plan(
                    "material",
                    closure,
                    weighted,
                    fidelity_policy=FidelityPolicy.STRICT,
                )
                self.assertEqual(
                    SurfaceModelKind.UNSUPPORTED_SURFACE.value,
                    strict["kind"],
                )


class MigrationTests(unittest.TestCase):
    def test_ordinary_v1_standard_pbr_migrates_without_inventing_mix_topology(self):
        v1 = make_document(
            "miku-material-ir-1.0",
            {
                "materialKey": "LegacyPbr",
                "workflow": {"kind": "standard_pbr"},
                "regions": [],
                "expressions": [],
                "channels": [
                    {
                        "semantic": "BaseColor",
                        "valueType": "Color",
                        "default": [0.2, 0.3, 0.4, 1.0],
                    },
                    {
                        "semantic": "Roughness",
                        "valueType": "Scalar",
                        "default": 0.25,
                    },
                    {
                        "semantic": "Metalness",
                        "valueType": "Scalar",
                        "default": 0.5,
                    },
                    {
                        "semantic": "Normal",
                        "valueType": "Color",
                        "default": [0.0, 0.0, 1.0],
                    },
                    {
                        "semantic": "Alpha",
                        "valueType": "Scalar",
                        "default": 1.0,
                    },
                ],
                "parameters": [],
                "surfaceContract": {
                    "schema": "miku-surface-1.0",
                    "model": "StandardLit",
                    "renderMethod": "Opaque",
                    "renderFace": "Both",
                    "coverageChannel": "Alpha",
                },
            },
        )
        v1["documentKind"] = "migr-material-ir-1.0"
        v1["canonicalHash"] = canonical_hash(
            {
                key: value
                for key, value in v1.items()
                if key != "canonicalHash"
            }
        )
        migrated = migrate_legacy_material_ir(v1)
        validate_document(migrated, "miku-material-ir-1.0")
        self.assertEqual("Principled", migrated["closureGraph"]["root"]["kind"])
        self.assertEqual("OpaquePBR", migrated["surfaceModelPlan"]["kind"])
        self.assertEqual(
            "miku-legacy-standard-pbr-1.0",
            migrated["migration"]["algorithmVersion"],
        )


if __name__ == "__main__":
    unittest.main()
