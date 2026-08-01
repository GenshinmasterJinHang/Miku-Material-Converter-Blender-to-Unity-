# Miku semantic IR

Miku is the target-neutral contract between Blender authoring integrations and
versioned Unity backends. The Miku package and Blender extension identifiers are
the only active product compatibility labels.

## Current contracts

- `schema/miku-material-ir-1.0.schema.json` and
  `schema/miku-material-ir-2.0.schema.json` describe target-neutral MaterialIR.
- Versioned bundle, target-profile, conversion-plan, manifest, bake, source-map,
  and import-receipt schemas remain under `schema/`.
- Bundle 2.2 adds static image resources and explicit channel metadata while
  preserving MaterialIR 2.0.

Retired MIKU 2/3/4/5, `.b2ubundle`, and the old `schemas/` directory are not
active compatibility inputs. Unknown schema versions fail explicitly; Unity
does not guess a legacy interpretation.

## Semantic model rules

Nodes express operations such as multiply, roughness, coordinate conversion, or
alpha clip rather than Unity classes. Sockets preserve value type, semantic,
space, stage, uniformity where available, and source identity. The allowed space
vocabulary is `None`, `UV0`, `UV1`, `Object`, `World`, `AbsoluteWorld`, `View`,
`Tangent`, and `Screen`; legacy lower-case values are normalized at boundaries
only when their meaning is unambiguous.

Required output chains fail on unsupported or invalid semantics. Safely pruned
unreachable nodes may warn. Translation quality is structured as Exact,
Equivalent, Approximate, Baked, RequiresProjectSetup, RequiresRuntimeSupport, or
Unsupported.

### Closure normal invariant

An unconnected Blender closure Normal socket reports `[0, 0, 0]` as an
authoring-API sentinel for “use the surface geometry normal.” That value is not
a real tangent-space normal and must not cross the Blender-to-MaterialIR
boundary. Miku normalizes only an unconnected constant zero sentinel to neutral
tangent normal `[0, 0, 1]`. Linked normal expressions, baked normal resources,
and non-zero constant normals retain their original values and provenance.

## Determinism

Normalized objects, lists, diagnostics, and generated resources have stable
ordering. Source-derived IDs remain stable when unrelated nodes are inserted.
Current time, machine paths, dictionary iteration, and random GUIDs are excluded
from default output. See the output migration ADR and changelog.
