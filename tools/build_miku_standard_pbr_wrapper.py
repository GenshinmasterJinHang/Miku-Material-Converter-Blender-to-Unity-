#!/usr/bin/env python3
"""Build the Shader Graph 17.4 Standard PBR wrapper deterministically.

The Generic Toon wrapper is the immutable source fixture for the former shared
wrapper. This builder keeps all of its existing objects, IDs, property references,
and Sub Graph input edges, then adds only the Standard PBR authoring layer.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "unity" / "Packages" / "com.miku.shaderconverter"
TEMPLATES = PACKAGE / "Templates"
STANDARD_WRAPPER_SEED = (
    ROOT / "tools" / "fixtures" / "MikuStandardWrapperSeed.shadergraph"
)
STANDARD_WRAPPER = TEMPLATES / "MikuStandardTemplate.shadergraph"
GENERATED_SUBGRAPH = TEMPLATES / "MikuStandardTemplate.generated.shadersubgraph"

PUBLIC_SURFACE_REFS = (
    "_BaseMap",
    "_BaseColor",
    "_MetallicMap",
    "_Metallic",
    "_RoughnessMap",
    "_Roughness",
    "_BumpMap",
    "_NormalStrength",
    "_OcclusionStrength",
)
PUBLIC_EMISSION_REFS = (
    "_EmissionMap",
    "_EmissionColor",
    "_EmissionStrength",
)
PUBLIC_REFS = PUBLIC_SURFACE_REFS + PUBLIC_EMISSION_REFS


def parse_multi_json(path: Path) -> list[dict[str, Any]]:
    text = path.read_text(encoding="utf8")
    decoder = json.JSONDecoder()
    index = 0
    result: list[dict[str, Any]] = []
    while index < len(text):
        while index < len(text) and text[index].isspace():
            index += 1
        if index >= len(text):
            break
        value, index = decoder.raw_decode(text, index)
        if not isinstance(value, dict):
            raise ValueError(f"{path}: expected JSON object")
        result.append(value)
    return result


def serialize_multi_json(objects: list[dict[str, Any]]) -> str:
    return "\n\n".join(
        json.dumps(item, ensure_ascii=False, indent=2) for item in objects
    ) + "\n"


def stable_hex(kind: str, name: str, length: int = 32) -> str:
    return hashlib.sha256(f"miku-standard-pbr:{kind}:{name}".encode("utf8")).hexdigest()[
        :length
    ]


def stable_guid(name: str) -> str:
    value = stable_hex("property-guid", name)
    return f"{value[:8]}-{value[8:12]}-{value[12:16]}-{value[16:20]}-{value[20:]}"


def property_reference(item: dict[str, Any]) -> str:
    return str(
        item.get("m_OverrideReferenceName")
        or item.get("m_DefaultReferenceName")
        or ""
    )


def object_map(objects: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    return {
        str(item["m_ObjectId"]): item
        for item in objects
        if item.get("m_ObjectId")
    }


def add_object(
    objects: list[dict[str, Any]],
    by_id: dict[str, dict[str, Any]],
    item: dict[str, Any],
) -> None:
    object_id = str(item["m_ObjectId"])
    if object_id in by_id:
        raise ValueError(f"duplicate Shader Graph object id: {object_id}")
    objects.append(item)
    by_id[object_id] = item


def set_position(item: dict[str, Any], x: float, y: float) -> None:
    item["m_DrawState"]["m_Position"] = {
        "serializedVersion": "2",
        "x": float(x),
        "y": float(y),
        "width": 200.0,
        "height": 120.0,
    }


def make_float_property(
    template: dict[str, Any],
    key: str,
    display_name: str,
    reference_name: str,
    value: float,
    maximum: float,
) -> dict[str, Any]:
    item = copy.deepcopy(template)
    item["m_ObjectId"] = stable_hex("property", key)
    item["m_Guid"] = {"m_GuidSerialized": stable_guid(key)}
    item["m_Name"] = display_name
    item["m_RefNameGeneratedByDisplayName"] = display_name
    item["m_DefaultReferenceName"] = "_" + display_name.replace(" ", "_")
    item["m_OverrideReferenceName"] = reference_name
    item["m_GeneratePropertyBlock"] = True
    item["overrideHLSLDeclaration"] = False
    item["hlslDeclarationOverride"] = 0
    item["m_Hidden"] = False
    item["m_Value"] = float(value)
    item["m_FloatType"] = 1
    item["m_RangeValues"] = {"x": 0.0, "y": float(maximum)}
    return item


def make_color_property(
    template: dict[str, Any],
    key: str,
    display_name: str,
    reference_name: str,
    hdr: bool,
) -> dict[str, Any]:
    item = copy.deepcopy(template)
    item["m_ObjectId"] = stable_hex("property", key)
    item["m_Guid"] = {"m_GuidSerialized": stable_guid(key)}
    item["m_Name"] = display_name
    item["m_RefNameGeneratedByDisplayName"] = display_name
    item["m_DefaultReferenceName"] = "_" + display_name.replace(" ", "_")
    item["m_OverrideReferenceName"] = reference_name
    item["m_GeneratePropertyBlock"] = True
    item["overrideHLSLDeclaration"] = False
    item["hlslDeclarationOverride"] = 0
    item["m_Hidden"] = False
    item["m_Value"] = {"r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0}
    item["isMainColor"] = False
    item["m_ColorMode"] = 1 if hdr else 0
    return item


def clone_property_node(
    objects: list[dict[str, Any]],
    by_id: dict[str, dict[str, Any]],
    graph: dict[str, Any],
    node_template: dict[str, Any],
    slot_template: dict[str, Any],
    prop: dict[str, Any],
    key: str,
    x: float,
    y: float,
) -> str:
    node = copy.deepcopy(node_template)
    slot = copy.deepcopy(slot_template)
    node_id = stable_hex("property-node", key)
    slot_id = stable_hex("property-slot", key)
    node["m_ObjectId"] = node_id
    node["m_Group"] = {"m_Id": ""}
    node["m_Slots"] = [{"m_Id": slot_id}]
    node["m_Property"] = {"m_Id": prop["m_ObjectId"]}
    set_position(node, x, y)
    slot["m_ObjectId"] = slot_id
    slot["m_DisplayName"] = prop["m_Name"]
    add_object(objects, by_id, node)
    add_object(objects, by_id, slot)
    graph["m_Nodes"].append({"m_Id": node_id})
    return node_id


def clone_math_node(
    objects: list[dict[str, Any]],
    by_id: dict[str, dict[str, Any]],
    graph: dict[str, Any],
    source_by_id: dict[str, dict[str, Any]],
    source_node: dict[str, Any],
    key: str,
    display_name: str,
    x: float,
    y: float,
) -> str:
    node = copy.deepcopy(source_node)
    node_id = stable_hex("node", key)
    node["m_ObjectId"] = node_id
    node["m_Group"] = {"m_Id": ""}
    node["m_Name"] = display_name
    set_position(node, x, y)
    slot_refs = []
    for index, source_ref in enumerate(source_node["m_Slots"]):
        slot = copy.deepcopy(source_by_id[source_ref["m_Id"]])
        slot_id = stable_hex("slot", f"{key}:{index}")
        slot["m_ObjectId"] = slot_id
        add_object(objects, by_id, slot)
        slot_refs.append({"m_Id": slot_id})
    node["m_Slots"] = slot_refs
    add_object(objects, by_id, node)
    graph["m_Nodes"].append({"m_Id": node_id})
    return node_id


def make_normal_strength_node(
    objects: list[dict[str, Any]],
    by_id: dict[str, dict[str, Any]],
    graph: dict[str, Any],
    x: float,
    y: float,
) -> str:
    key = "normal-strength"
    node_id = stable_hex("node", key)
    in_slot = stable_hex("slot", f"{key}:0")
    strength_slot = stable_hex("slot", f"{key}:1")
    out_slot = stable_hex("slot", f"{key}:2")
    node = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.NormalStrengthNode",
        "m_ObjectId": node_id,
        "m_Group": {"m_Id": ""},
        "m_Name": "Normal Strength",
        "m_DrawState": {
            "m_Expanded": True,
            "m_Position": {
                "serializedVersion": "2",
                "x": float(x),
                "y": float(y),
                "width": 200.0,
                "height": 120.0,
            },
        },
        "m_Slots": [
            {"m_Id": in_slot},
            {"m_Id": strength_slot},
            {"m_Id": out_slot},
        ],
        "synonyms": ["intensity"],
        "m_Precision": 0,
        "m_PreviewExpanded": True,
        "m_DismissedVersion": 0,
        "m_PreviewMode": 0,
        "m_CustomColors": {"m_SerializableColors": []},
    }
    slots = [
        {
            "m_SGVersion": 0,
            "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot",
            "m_ObjectId": in_slot,
            "m_Id": 0,
            "m_DisplayName": "In",
            "m_SlotType": 0,
            "m_Hidden": False,
            "m_ShaderOutputName": "In",
            "m_StageCapability": 3,
            "m_Value": {"x": 0.0, "y": 0.0, "z": 1.0},
            "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 1.0},
            "m_Labels": [],
        },
        {
            "m_SGVersion": 0,
            "m_Type": "UnityEditor.ShaderGraph.Vector1MaterialSlot",
            "m_ObjectId": strength_slot,
            "m_Id": 1,
            "m_DisplayName": "Strength",
            "m_SlotType": 0,
            "m_Hidden": False,
            "m_ShaderOutputName": "Strength",
            "m_StageCapability": 3,
            "m_Value": 1.0,
            "m_DefaultValue": 1.0,
            "m_Labels": [],
        },
        {
            "m_SGVersion": 0,
            "m_Type": "UnityEditor.ShaderGraph.Vector3MaterialSlot",
            "m_ObjectId": out_slot,
            "m_Id": 2,
            "m_DisplayName": "Out",
            "m_SlotType": 1,
            "m_Hidden": False,
            "m_ShaderOutputName": "Out",
            "m_StageCapability": 3,
            "m_Value": {"x": 0.0, "y": 0.0, "z": 0.0},
            "m_DefaultValue": {"x": 0.0, "y": 0.0, "z": 0.0},
            "m_Labels": [],
        },
    ]
    add_object(objects, by_id, node)
    for slot in slots:
        add_object(objects, by_id, slot)
    graph["m_Nodes"].append({"m_Id": node_id})
    return node_id


def make_edge(
    output_node: str, output_slot: int, input_node: str, input_slot: int
) -> dict[str, Any]:
    return {
        "m_OutputSlot": {
            "m_Node": {"m_Id": output_node},
            "m_SlotId": output_slot,
        },
        "m_InputSlot": {
            "m_Node": {"m_Id": input_node},
            "m_SlotId": input_slot,
        },
    }


def make_category(
    objects: list[dict[str, Any]],
    by_id: dict[str, dict[str, Any]],
    name: str,
    key: str,
    property_ids: list[str],
) -> dict[str, str]:
    category_id = stable_hex("category", key)
    category = {
        "m_SGVersion": 0,
        "m_Type": "UnityEditor.ShaderGraph.CategoryData",
        "m_ObjectId": category_id,
        "m_Name": name,
        "m_ChildObjectList": [{"m_Id": item} for item in property_ids],
    }
    add_object(objects, by_id, category)
    return {"m_Id": category_id}


def build_standard_wrapper() -> str:
    objects = parse_multi_json(STANDARD_WRAPPER_SEED)
    subgraph_objects = parse_multi_json(GENERATED_SUBGRAPH)
    graph = objects[0]
    if not str(graph.get("m_Type", "")).endswith("GraphData"):
        raise ValueError("standard wrapper seed does not start with GraphData")
    by_id = object_map(objects)
    subgraph_by_id = object_map(subgraph_objects)

    properties = [
        item
        for item in objects
        if "ShaderProperty" in str(item.get("m_Type", ""))
    ]
    props_by_ref = {property_reference(item): item for item in properties}
    if set(PUBLIC_REFS).intersection(props_by_ref) != {
        "_BaseMap",
        "_MetallicMap",
        "_RoughnessMap",
        "_BumpMap",
        "_OcclusionStrength",
        "_EmissionMap",
    }:
        raise ValueError("standard wrapper seed public property baseline changed")

    props_by_ref["_BaseMap"]["m_Name"] = "Base Color Map"
    props_by_ref["_BumpMap"]["m_DefaultType"] = 3
    scalar_template = props_by_ref["_OcclusionStrength"]
    color_template = next(
        item
        for item in properties
        if item.get("m_Type")
        == "UnityEditor.ShaderGraph.Internal.ColorShaderProperty"
    )
    additions = (
        make_color_property(
            color_template, "base-color", "Base Color Tint", "_BaseColor", False
        ),
        make_float_property(
            scalar_template,
            "metallic",
            "Metalness Strength",
            "_Metallic",
            1.0,
            1.0,
        ),
        make_float_property(
            scalar_template,
            "roughness",
            "Roughness Strength",
            "_Roughness",
            1.0,
            1.0,
        ),
        make_float_property(
            scalar_template,
            "normal-strength",
            "Normal Strength",
            "_NormalStrength",
            1.0,
            2.0,
        ),
        make_color_property(
            color_template, "emission-color", "Emission Tint", "_EmissionColor", True
        ),
        make_float_property(
            scalar_template,
            "emission-strength",
            "Emission Strength",
            "_EmissionStrength",
            1.0,
            16.0,
        ),
    )
    for item in additions:
        add_object(objects, by_id, item)
        graph["m_Properties"].append({"m_Id": item["m_ObjectId"]})
        props_by_ref[property_reference(item)] = item

    for item in props_by_ref.values():
        reference = property_reference(item)
        visible = reference in PUBLIC_REFS
        item["m_GeneratePropertyBlock"] = visible
        item["overrideHLSLDeclaration"] = not visible
        item["hlslDeclarationOverride"] = 0 if visible else 2
        item["m_Hidden"] = not visible
        if (
            not visible
            and item.get("m_Type")
            == "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty"
        ):
            item["useTilingAndOffset"] = False
            item["useTexelSize"] = False
            item["isHDR"] = False

    existing_property_nodes = [
        item
        for item in objects
        if item.get("m_Type") == "UnityEditor.ShaderGraph.PropertyNode"
    ]
    scalar_source_node = next(
        item
        for item in existing_property_nodes
        if item.get("m_Property", {}).get("m_Id")
        == props_by_ref["_OcclusionStrength"]["m_ObjectId"]
    )
    color_source_node = next(
        item
        for item in existing_property_nodes
        if by_id[item.get("m_Property", {}).get("m_Id", "")].get("m_Type")
        == "UnityEditor.ShaderGraph.Internal.ColorShaderProperty"
    )
    scalar_source_slot = by_id[scalar_source_node["m_Slots"][0]["m_Id"]]
    color_source_slot = by_id[color_source_node["m_Slots"][0]["m_Id"]]
    property_nodes = {
        "_BaseColor": clone_property_node(
            objects,
            by_id,
            graph,
            color_source_node,
            color_source_slot,
            props_by_ref["_BaseColor"],
            "base-color",
            1060,
            20,
        ),
        "_Metallic": clone_property_node(
            objects,
            by_id,
            graph,
            scalar_source_node,
            scalar_source_slot,
            props_by_ref["_Metallic"],
            "metallic",
            1060,
            220,
        ),
        "_Roughness": clone_property_node(
            objects,
            by_id,
            graph,
            scalar_source_node,
            scalar_source_slot,
            props_by_ref["_Roughness"],
            "roughness",
            1240,
            420,
        ),
        "_NormalStrength": clone_property_node(
            objects,
            by_id,
            graph,
            scalar_source_node,
            scalar_source_slot,
            props_by_ref["_NormalStrength"],
            "normal-strength",
            1120,
            680,
        ),
        "_EmissionColor": clone_property_node(
            objects,
            by_id,
            graph,
            color_source_node,
            color_source_slot,
            props_by_ref["_EmissionColor"],
            "emission-color",
            1080,
            900,
        ),
        "_EmissionStrength": clone_property_node(
            objects,
            by_id,
            graph,
            scalar_source_node,
            scalar_source_slot,
            props_by_ref["_EmissionStrength"],
            "emission-strength",
            1320,
            1040,
        ),
    }
    occlusion_property_node = next(
        item["m_ObjectId"]
        for item in existing_property_nodes
        if item.get("m_Property", {}).get("m_Id")
        == props_by_ref["_OcclusionStrength"]["m_ObjectId"]
    )

    source_nodes = {
        node_type: next(
            item
            for item in subgraph_objects
            if item.get("m_Type") == node_type
        )
        for node_type in (
            "UnityEditor.ShaderGraph.MultiplyNode",
            "UnityEditor.ShaderGraph.OneMinusNode",
            "UnityEditor.ShaderGraph.ClampNode",
        )
    }
    multiply = source_nodes["UnityEditor.ShaderGraph.MultiplyNode"]
    one_minus = source_nodes["UnityEditor.ShaderGraph.OneMinusNode"]
    clamp = source_nodes["UnityEditor.ShaderGraph.ClampNode"]
    nodes = {
        "base-multiply": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            multiply,
            "base-multiply",
            "Base Color Tint",
            1360,
            20,
        ),
        "metal-multiply": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            multiply,
            "metal-multiply",
            "Metalness Strength",
            1320,
            210,
        ),
        "metal-clamp": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            clamp,
            "metal-clamp",
            "Clamp Metalness",
            1560,
            210,
        ),
        "rough-from-smooth": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            one_minus,
            "rough-from-smooth",
            "Smoothness To Roughness",
            1060,
            410,
        ),
        "rough-multiply": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            multiply,
            "rough-multiply",
            "Roughness Strength",
            1480,
            410,
        ),
        "rough-clamp": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            clamp,
            "rough-clamp",
            "Clamp Roughness",
            1720,
            410,
        ),
        "smooth-from-rough": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            one_minus,
            "smooth-from-rough",
            "Roughness To Smoothness",
            1960,
            410,
        ),
        "normal-strength": make_normal_strength_node(
            objects, by_id, graph, 1380, 650
        ),
        "emission-color": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            multiply,
            "emission-color",
            "Emission Tint",
            1360,
            850,
        ),
        "emission-strength": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            multiply,
            "emission-strength",
            "Emission Strength",
            1620,
            850,
        ),
        "occlusion-clamp": clone_math_node(
            objects,
            by_id,
            graph,
            subgraph_by_id,
            clamp,
            "occlusion-clamp",
            "Clamp Occlusion",
            1380,
            1160,
        ),
    }

    subgraph_node = next(
        item
        for item in objects
        if item.get("m_Type") == "UnityEditor.ShaderGraph.SubGraphNode"
    )
    blocks = {
        item["m_Name"]: item["m_ObjectId"]
        for item in objects
        if item.get("m_Type") == "UnityEditor.ShaderGraph.BlockNode"
    }
    target_block_names = {
        "SurfaceDescription.BaseColor",
        "SurfaceDescription.Metallic",
        "SurfaceDescription.Smoothness",
        "SurfaceDescription.NormalTS",
        "SurfaceDescription.Emission",
        "SurfaceDescription.Occlusion",
        "SurfaceDescription.Alpha",
    }
    target_block_ids = {blocks[name] for name in target_block_names}
    graph["m_Edges"] = [
        item
        for item in graph["m_Edges"]
        if not (
            item["m_OutputSlot"]["m_Node"]["m_Id"] == subgraph_node["m_ObjectId"]
            and item["m_InputSlot"]["m_Node"]["m_Id"] in target_block_ids
        )
    ]
    graph["m_Edges"].extend(
        [
            make_edge(subgraph_node["m_ObjectId"], 1, nodes["base-multiply"], 0),
            make_edge(property_nodes["_BaseColor"], 0, nodes["base-multiply"], 1),
            make_edge(
                nodes["base-multiply"], 2, blocks["SurfaceDescription.BaseColor"], 0
            ),
            make_edge(subgraph_node["m_ObjectId"], 2, nodes["metal-multiply"], 0),
            make_edge(property_nodes["_Metallic"], 0, nodes["metal-multiply"], 1),
            make_edge(nodes["metal-multiply"], 2, nodes["metal-clamp"], 0),
            make_edge(
                nodes["metal-clamp"], 3, blocks["SurfaceDescription.Metallic"], 0
            ),
            make_edge(
                subgraph_node["m_ObjectId"], 3, nodes["rough-from-smooth"], 0
            ),
            make_edge(nodes["rough-from-smooth"], 1, nodes["rough-multiply"], 0),
            make_edge(property_nodes["_Roughness"], 0, nodes["rough-multiply"], 1),
            make_edge(nodes["rough-multiply"], 2, nodes["rough-clamp"], 0),
            make_edge(nodes["rough-clamp"], 3, nodes["smooth-from-rough"], 0),
            make_edge(
                nodes["smooth-from-rough"],
                1,
                blocks["SurfaceDescription.Smoothness"],
                0,
            ),
            make_edge(
                subgraph_node["m_ObjectId"], 4, nodes["normal-strength"], 0
            ),
            make_edge(
                property_nodes["_NormalStrength"], 0, nodes["normal-strength"], 1
            ),
            make_edge(
                nodes["normal-strength"], 2, blocks["SurfaceDescription.NormalTS"], 0
            ),
            make_edge(subgraph_node["m_ObjectId"], 5, nodes["emission-color"], 0),
            make_edge(
                property_nodes["_EmissionColor"], 0, nodes["emission-color"], 1
            ),
            make_edge(nodes["emission-color"], 2, nodes["emission-strength"], 0),
            make_edge(
                property_nodes["_EmissionStrength"],
                0,
                nodes["emission-strength"],
                1,
            ),
            make_edge(
                nodes["emission-strength"],
                2,
                blocks["SurfaceDescription.Emission"],
                0,
            ),
            make_edge(occlusion_property_node, 0, nodes["occlusion-clamp"], 0),
            make_edge(
                nodes["occlusion-clamp"],
                3,
                blocks["SurfaceDescription.Occlusion"],
                0,
            ),
        ]
    )

    all_properties = [
        by_id[item["m_Id"]]
        for item in graph["m_Properties"]
    ]
    props_by_ref = {property_reference(item): item for item in all_properties}
    hidden_ids = [
        item["m_ObjectId"]
        for item in all_properties
        if property_reference(item) not in PUBLIC_REFS
    ]
    surface_ids = [props_by_ref[item]["m_ObjectId"] for item in PUBLIC_SURFACE_REFS]
    emission_ids = [props_by_ref[item]["m_ObjectId"] for item in PUBLIC_EMISSION_REFS]
    graph["m_Properties"] = [
        {"m_Id": item}
        for item in surface_ids + emission_ids + hidden_ids
    ]
    graph["m_CategoryData"] = [
        make_category(objects, by_id, "Surface Inputs", "surface", surface_ids),
        make_category(objects, by_id, "Emission", "emission", emission_ids),
        make_category(objects, by_id, "", "internal", hidden_ids),
    ]

    visible = tuple(
        property_reference(item)
        for item in all_properties
        if item.get("m_GeneratePropertyBlock") and not item.get("m_Hidden")
    )
    if set(visible) != set(PUBLIC_REFS) or len(visible) != len(PUBLIC_REFS):
        raise ValueError(f"unexpected Standard PBR visible properties: {visible}")
    if len(graph["m_Properties"]) != 47:
        raise ValueError(
            f"expected 47 Standard PBR properties, got {len(graph['m_Properties'])}"
        )
    return serialize_multi_json(objects)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--check",
        action="store_true",
        help="fail when the checked-in Standard wrapper is not deterministic",
    )
    args = parser.parse_args()
    expected = build_standard_wrapper()
    current = STANDARD_WRAPPER.read_text(encoding="utf8") if STANDARD_WRAPPER.exists() else ""
    if args.check:
        if current != expected:
            raise SystemExit("MikuStandardTemplate.shadergraph is out of date")
        return 0
    if current != expected:
        with STANDARD_WRAPPER.open("w", encoding="utf8", newline="\n") as stream:
            stream.write(expected)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
