"""Miku target-neutral semantic material conversion core."""

from .contracts import (
    DOCUMENT_KINDS,
    Fidelity,
    ParameterMutability,
    ParameterScope,
    ParameterUpdateAction,
    Route,
    canonical_hash,
    validate_document,
)
from .closure_ir import (
    AddShaderEnergyPolicy,
    ClosureBudget,
    ClosureDomain,
    ClosureGraphBuilder,
    ClosureKind,
    ClosureSimplifier,
    ClosureWeightFlattener,
    FidelityPolicy,
)
from .migrations import (
    migrate_legacy_manifest,
    migrate_legacy_material_ir,
    normalize_legacy_document,
)
from .planner import ConversionPlanner, default_target_profile
from .runtime_math import dielectric_fresnel, layer_weight
from .semantic import build_material_ir, build_source_map
from .socket_conversion import ImplicitSocketConversionRegistry
from .surface_models import SurfaceModelKind, build_surface_model_plan
from .time_driver import AffineFrame, TimeDriverError, parse_affine_frame

__all__ = [
    "ConversionPlanner",
    "AddShaderEnergyPolicy",
    "ClosureBudget",
    "ClosureDomain",
    "ClosureGraphBuilder",
    "ClosureKind",
    "ClosureSimplifier",
    "ClosureWeightFlattener",
    "DOCUMENT_KINDS",
    "Fidelity",
    "FidelityPolicy",
    "ImplicitSocketConversionRegistry",
    "ParameterMutability",
    "ParameterScope",
    "ParameterUpdateAction",
    "Route",
    "SurfaceModelKind",
    "build_material_ir",
    "build_source_map",
    "build_surface_model_plan",
    "AffineFrame",
    "TimeDriverError",
    "parse_affine_frame",
    "canonical_hash",
    "default_target_profile",
    "dielectric_fresnel",
    "layer_weight",
    "migrate_legacy_manifest",
    "migrate_legacy_material_ir",
    "normalize_legacy_document",
    "validate_document",
]
