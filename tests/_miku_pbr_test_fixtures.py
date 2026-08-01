"""Minimal duck-typed Blender node-tree fixtures for Standard PBR tests.

This avoids depending on the retired B2U exporter core while still
covering the behaviour of ``miku.standard_pbr_semantics``.

The fixture emulates only the attributes the extractor actually touches:

* ``Node.name`` and ``Node.bl_idname``
* ``Node.inputs`` / ``Node.outputs`` lists of :class:`Socket`
* ``Socket.name`` and ``Socket.type`` (used to look up standard PBR
  semantics for the connected socket)
* ``Socket.default_value`` (used as the parameter value for the slot)
* ``Socket.links`` (populated by :meth:`NodeTree.link`)
* ``Node.image`` (optional; used by the texture-name inference)
* ``NodeTree.nodes`` and ``NodeTree.links``
"""

from __future__ import annotations

from typing import Any, Iterable, List, Optional


# Mapping from Blender ``bl_idname`` to the Miku op token consumed by
# ``miku.standard_pbr_semantics``.  Mirrors the legacy B2U
# ``SUPPORTED_NODE_OPS`` mapping in b2u_mvp/exporter_core.py.
_BL_IDNAME_TO_OP: dict[str, str] = {
    "ShaderNodeOutputMaterial": "Output.Material",
    "ShaderNodeOutputAOV": "Output.AOV",
    "ShaderNodeOutputWorld": "Output.World",
    "ShaderNodeOutputLight": "Output.Light",
    "ShaderNodeBsdfPrincipled": "Shader.PrincipledBSDF",
    "ShaderNodeAddShader": "Shader.Add",
    "ShaderNodeBsdfDiffuse": "Shader.DiffuseBSDF",
    "ShaderNodeEmission": "Shader.Emission",
    "ShaderNodeBsdfGlossy": "Shader.GlossyBSDF",
    "ShaderNodeBsdfMetallic": "Shader.MetallicBSDF",
    "ShaderNodeMixShader": "Shader.Mix",
    "ShaderNodeBsdfSheen": "Shader.SheenBSDF",
    "ShaderNodeBsdfSpecular": "Shader.SpecularBSDF",
    "ShaderNodeEeveeSpecular": "Shader.SpecularBSDF",
    "ShaderNodeSubsurfaceScattering": "Shader.SubsurfaceScattering",
    "ShaderNodeBsdfTranslucent": "Shader.TranslucentBSDF",
    "ShaderNodeBsdfTransparent": "Shader.TransparentBSDF",
    "ShaderNodeShaderToRGB": "Shader.ToRGB",
    "ShaderNodeRGB": "Input.RGB",
    "ShaderNodeValue": "Input.Value",
    "B2UNodeVector": "Input.Vector",
    "ShaderNodeTexCoord": "Input.TextureCoordinate",
    "ShaderNodeUVMap": "Input.UVMap",
    "ShaderNodeNewGeometry": "Input.Geometry",
    "ShaderNodeAttribute": "Input.Attribute",
    "ShaderNodeAmbientOcclusion": "Input.AmbientOcclusion",
    "ShaderNodeCameraData": "Input.CameraData",
    "ShaderNodeVertexColor": "Input.ColorAttribute",
    "ShaderNodeFresnel": "Input.Fresnel",
    "ShaderNodeLayerWeight": "Input.LayerWeight",
    "ShaderNodeObjectInfo": "Input.ObjectInfo",
    "ShaderNodeTangent": "Input.Tangent",
    "ShaderNodeWireframe": "Input.Wireframe",
    "ShaderNodeTexImage": "Texture.Image",
    "ShaderNodeTexBrick": "Texture.Brick",
    "ShaderNodeTexChecker": "Texture.Checker",
    "ShaderNodeTexEnvironment": "Texture.Environment",
    "ShaderNodeTexGradient": "Texture.Gradient",
    "ShaderNodeTexMagic": "Texture.Magic",
    "ShaderNodeTexMusgrave": "Texture.Musgrave",
    "ShaderNodeTexNoise": "Texture.Noise",
    "ShaderNodeTexSky": "Texture.Sky",
    "ShaderNodeTexVoronoi": "Texture.Voronoi",
    "ShaderNodeTexWave": "Texture.Wave",
    "ShaderNodeTexWhiteNoise": "Texture.WhiteNoise",
    "ShaderNodeMapping": "Vector.Mapping",
    "ShaderNodeNormalMap": "Vector.NormalMap",
    "ShaderNodeTangent": "Vector.Tangent",
    "ShaderNodeBump": "Vector.Bump",
    "ShaderNodeDisplacement": "Vector.Displacement",
    "ShaderNodeSeparateColor": "Converter.SeparateColor",
    "ShaderNodeCombineColor": "Converter.CombineColor",
    "ShaderNodeSeparateXYZ": "Converter.SeparateXYZ",
    "ShaderNodeCombineXYZ": "Converter.CombineXYZ",
    "ShaderNodeRGBToBW": "Converter.RGBToBW",
    "ShaderNodeMath": "Math",
    "ShaderNodeVectorMath": "VectorMath",
    "ShaderNodeMixRGB": "Color.Mix",
    "ShaderNodeInvert": "Color.Invert",
    "ShaderNodeHueSaturation": "Color.HueSaturationValue",
    "ShaderNodeBrightContrast": "Color.BrightContrast",
    "ShaderNodeGamma": "Color.Gamma",
    "ShaderNodeCurves": "Color.RGBCurves",
    "ShaderNodeMapRange": "Math.MapRange",
    "ShaderNodeClamp": "Math.Clamp",
    "ShaderNodeValToRGB": "Color.Ramp",
    "ShaderNodeNodeReroute": "Utility.Reroute",
    "ShaderNodeGroup": "ShaderNodeGroup",
}


class Socket:
    def __init__(
        self,
        name: str,
        socket_type: str = "VALUE",
        default_value: Any = None,
    ) -> None:
        self.name = name
        self.type = socket_type
        self.default_value = default_value
        self.links: List["Link"] = []


class Link:
    def __init__(
        self,
        from_node: "Node",
        from_socket: Socket,
        to_node: "Node",
        to_socket: Socket,
    ) -> None:
        self.from_node = from_node
        self.from_socket = from_socket
        self.to_node = to_node
        self.to_socket = to_socket
        to_socket.links.append(self)


class Node:
    def __init__(
        self,
        name: str,
        bl_idname: str,
        inputs: Optional[Iterable[Socket]] = None,
        outputs: Optional[Iterable[Socket]] = None,
        **attrs: Any,
    ) -> None:
        self.name = name
        self.bl_idname = bl_idname
        self.inputs: List[Socket] = list(inputs or [])
        self.outputs: List[Socket] = list(outputs or [])
        for key, value in attrs.items():
            setattr(self, key, value)


class Image:
    def __init__(self, filepath: str, name: Optional[str] = None) -> None:
        self.filepath = filepath
        self.name = name or filepath.rsplit("/", 1)[-1]


class Material:
    def __init__(self, name: str, node_tree: "NodeTree") -> None:
        self.name = name
        self.node_tree = node_tree


class NodeTree:
    def __init__(self, nodes: Optional[Iterable[Node]] = None) -> None:
        self.nodes: List[Node] = list(nodes or [])
        self.links: List[Link] = []

    def link(
        self,
        from_node: Node,
        from_socket_index: int,
        to_node: Node,
        to_socket_index: int,
    ) -> Link:
        link = Link(
            from_node,
            from_node.outputs[from_socket_index],
            to_node,
            to_node.inputs[to_socket_index],
        )
        self.links.append(link)
        return link


# Mapping from the legacy exporter's ``Socket.type`` strings to the
# Miku Material IR ``valueType`` values the new graph builder expects.
_TYPE_TO_VALUE_TYPE = {
    "RGBA": "color4",
    "RGB": "color3",
    "VECTOR": "float3",
    "VALUE": "float",
    "SHADER": "closure_bsdf",
}


def socket_value_type(socket: Socket) -> str:
    """Translate the legacy socket type token to a Miku valueType string."""

    return _TYPE_TO_VALUE_TYPE.get(socket.type.upper(), "float")


def op_for(bl_idname: str) -> str:
    return _BL_IDNAME_TO_OP.get(bl_idname, bl_idname)


def build_graph_dict(material: Material) -> dict:
    """Convert the fixture Material/NodeTree into a Miku-style graph dict.

    The resulting dict matches the shape produced by
    ``miku_blender.snapshot_material`` (list of nodes, list of edges, plus
    a top-level ``standardPbrSemantic`` placeholder for the extractor to
    fill in).  Only the fields the Standard PBR extractor actually reads
    are populated; the rest can be left empty.
    """

    nodes: List[dict] = []
    resources: dict[str, dict] = {}

    # Sockets whose default value the legacy exporter lifted into
    # ``params`` so the Standard PBR extractor can read them off the
    # node.  Mirrors the legacy B2U helper.  Each entry maps the
    # Blender bl_idname to a list of (param_key, socket_name) pairs.
    _PARAMS_LIFTED_SOCKETS: dict[str, list[tuple[str, str]]] = {
        "ShaderNodeNormalMap": [("strength", "Strength")],
        "ShaderNodeBump": [
            ("strength", "Strength"),
            ("distance", "Distance"),
        ],
        "ShaderNodeDisplacement": [
            ("scale", "Scale"),
            ("midlevel", "Midlevel"),
        ],
    }

    for node in material.node_tree.nodes:
        node_id = f"node::{material.name}::{node.name}"
        params: dict = {"bl_idname": node.bl_idname}
        for key, value in node.__dict__.items():
            if key in {"name", "bl_idname", "inputs", "outputs"}:
                continue
            params[key] = value
        for param_key, socket_name in _PARAMS_LIFTED_SOCKETS.get(
            node.bl_idname, []
        ):
            for sock in node.inputs:
                if sock.name == socket_name and sock.default_value is not None:
                    params[param_key] = sock.default_value
        serialized_inputs: List[dict] = []
        for socket in node.inputs:
            value_type = socket_value_type(socket)
            entry = {
                "id": socket.name,
                "name": socket.name,
                "dir": "in",
                "valueType": value_type,
                "default": socket.default_value,
            }
            serialized_inputs.append(entry)
        serialized_outputs: List[dict] = []
        for socket in node.outputs:
            value_type = socket_value_type(socket)
            entry = {
                "id": socket.name,
                "name": socket.name,
                "dir": "out",
                "valueType": value_type,
            }
            serialized_outputs.append(entry)
        image = getattr(node, "image", None)
        if image is not None:
            filepath = getattr(image, "filepath", "") or getattr(
                image, "name", ""
            )
            params["image"] = filepath
            # Texture.Image nodes need a registered resource so the
            # Standard PBR extractor can resolve ``trace.resource`` and
            # produce slot entries.  Mirror the legacy B2U shape.
            resource_id = f"image::{filepath}"
            params["resource"] = resource_id
            resources[resource_id] = {
                "id": resource_id,
                "path": filepath,
                "uri": filepath,
                "exportFileName": filepath.rsplit("/", 1)[-1],
                "blenderImageName": filepath.rsplit("/", 1)[-1],
                "name": filepath.rsplit("/", 1)[-1],
            }
        nodes.append(
            {
                "id": node_id,
                "name": node.name,
                "op": op_for(node.bl_idname),
                "params": params,
                "inputs": serialized_inputs,
                "outputs": serialized_outputs,
            }
        )

    name_to_id = {
        node.name: f"node::{material.name}::{node.name}"
        for node in material.node_tree.nodes
    }
    edges: List[dict] = []
    for link in material.node_tree.links:
        from_id = name_to_id[link.from_node.name]
        to_id = name_to_id[link.to_node.name]
        edges.append(
            {
                "from": {"node": from_id, "socket": link.from_socket.name},
                "to": {"node": to_id, "socket": link.to_socket.name},
            }
        )

    # The Standard PBR extractor looks at ``entry.surface`` to find the
    # active Material Output and its Surface input socket.  Match the
    # legacy B2U exporter shape: ``entry.surface.node`` is the
    # ``ShaderNodeOutputMaterial`` node ID, and ``entry.surface.socket``
    # is the Surface input identifier.
    entry: dict = {}
    active_output = next(
        (
            node
            for node in material.node_tree.nodes
            if node.bl_idname == "ShaderNodeOutputMaterial"
            and getattr(node, "is_active_output", True)
        ),
        None,
    )
    if active_output is not None:
        surface_input = next(
            (
                sock
                for sock in active_output.inputs
                if sock.name == "Surface"
            ),
            None,
        )
        if surface_input is not None:
            entry["surface"] = {
                "node": name_to_id[active_output.name],
                "socket": surface_input.name,
            }

    return {
        "material": {"name": material.name},
        "nodes": nodes,
        "edges": edges,
        "resources": resources,
        "entry": entry,
        "standardPbrSemantic": {
            "workflow": "Metallic",
            "source": {"shader": "", "blenderMaterial": material.name, "confidence": 0.0},
            "slots": {},
            "packedTextures": {},
            "diagnostics": [],
        },
    }
