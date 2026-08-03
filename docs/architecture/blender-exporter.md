# Blender exporter

## Current implementation

`extensions/miku_semantic_exporter` is the installable MIT Blender extension;
the implementation is in `miku_blender`. Blender data access, active-slot
selection, material workflow settings, hidden source and material identities, semantic
snapshotting, bundle export, and operator registration stay at that boundary.
Target-neutral validation, semantic IR, planning, and bundle sealing remain in
the `miku` package.

Miku 1.1 snapshots output-level type, coordinate space, shader stage, and
uniformity for Camera Data, Geometry Incoming/Backfacing, Fresnel, Layer
Weight, and the versioned Miku Time group before ordinary node-group
expansion. Geometry Backfacing remains a runtime scalar; the semantic compiler
lowers it to `1 - Input.IsFrontFace` rather than baking it. The Time group
is identified by `miku.semantic = "Input.Time"` and
`miku.semanticVersion = 1`; its serialized parameters record
`contract = "miku_time_v1"`, source FPS, and scene start frame.

Only root Material NodeTree scalar FCurves are migrated in 1.1. Expressions
containing `frame`, finite numeric literals, and `+ - * /` are parsed with a
strict AST whitelist and lowered into Time Frame plus arithmetic. Safe complex
scalar drivers become stable exposed material parameters and report
`MIKU_TIME_DRIVER_EXTERNALIZED`. Unsafe, vector, object-targeted, or otherwise
unrepresentable required drivers fail explicitly. No expression is evaluated
as Python code.

The panel exports exactly
`context.object.material_slots[context.object.active_material_index].material`.
It never scans other objects or slots. The public batch function
`export_selected_materials` remains available for automation and is not used by
the panel operator.

Each material stores one concrete workflow. Legacy `Inherit Scene` data is
resolved once against the legacy scene default and persisted into the explicit
material workflow. `Game Part` is relevant only to Genshin, WuWa, and HSR.
Standard PBR omits that field from the semantic workflow. A saved Generic Toon
workflow is reported as retired and is never normalized to Standard PBR.

Persistent identity remains part of the Miku 1.0 contract but is hidden from
the ordinary UI. Blender `BlendData` does not accept ID properties, so the
exporter stores `miku_source_id` on one canonical Scene and
`miku_material_id` on each exported Material. Existing IDs always win. A saved
blend without identity uses UUIDv5 over its normalized absolute path; an
unsaved blend receives one session-stable UUID. Material IDs are UUIDv4 values,
survive material renames, and are repaired when copied Material data-blocks
carry the same ID. If a Scene or Material is read-only, export continues with a
session-only ID and a warning.

The Scene also records a private source-origin fingerprint input. If a copied
or Save-As `.blend` still carries the original Source ID, Miku warns without
blocking. **Fork Source Identity** explicitly assigns a new Source ID and new
Material IDs to declare the copy independent; future Unity GUIDs consequently
change.

An export root is a shared container, not source-owned state. Miku never creates
or rewrites `.migr-identities.json`. A matching legacy registry can seed a
missing Material custom property; malformed or foreign registries only warn.
Before writing, the exporter scans immediate child Bundle directories for the
exact `(persistentSourceId, persistentMaterialId)` pair. One match is reused,
including an old name-only directory. Otherwise a new directory uses
`<safe-material-name>__<first-12-material-id-characters>`. Multiple matches or
an occupied candidate owned by another identity fail with the conflicting
directory and both identity pairs.

File-system writes occur at the Blender integration boundary and must be
constrained to the selected export root, finite-number validated,
deterministic, staged, and atomically committed.

Blender extension 2.1.2 registers one English-source translation catalog for
the `zh_HANS` locale. UI labels, tooltips, empty-state messages, and operator
result templates follow Blender's interface language; interchange keys,
diagnostic codes, and serialized diagnostic messages remain canonical English.

Advanced bake quality is a user execution preference, not a semantic material
property. Low, Standard, High, and Ultra map to 512, 1024, 2048, and 4096 for
generated 2D bake images. The exporter rewrites every scheduled 2D bake job and
rebuilds the plan hash before emitting bake request 1.1. The worker continues
to accept frozen request 1.0 at 1024. Samples, margin, CPU device, random seed,
Texture3D volume resolution, and direction-texture width remain fixed.

## Target constraints

- Access Blender only through APIs supported by Blender 5.2.0.
- Derive stable IDs from group-instance path plus source node/socket identity,
  never collection order or transient object pointers.
- Preserve value type, semantic, coordinate space, shader stage, source identity,
  ramp stops, image metadata, and diagnostic context.
- Reject unknown schema targets, non-finite numbers, unsafe paths, and stage/space
  conflicts before writing a bundle.
- Keep baking an explicit `Baked` translation with provenance; do not use it to
  hide unsupported required semantics.
- Preserve supported runtime expressions as `Native`; never submit a required
  view-, camera-, or time-dependent chain to the UV bake worker.
- In Portable Hybrid (`PreferNative`), classify every required expression as
  Runtime, Uniform, UV0, or MeshSurface. Bake only maximal Uniform/UV0 islands
  on the worker's canonical plane and never inspect or export a source mesh.
- Reject Portable Hybrid requests that contain any MeshSurface dependency or
  mix reusable and mesh-bound jobs; no fixed-view snapshot fallback is allowed.
- When a runtime material also contains an independent linked static channel,
  emit a channel-scoped `MeshBake` job for only that semantic. The worker must
  prove the selected channel is runtime-independent before baking it.
- Traverse active weighted-closure parameters recursively. Static unsupported
  endpoints become traceable expression-island bakes, while the surrounding
  runtime View/Camera/Time chain remains native.
- Treat Height as the raw pre-Midlevel/pre-Scale scalar source. Export at most
  one shared source as Linear R half-float EXR; never choose or combine distinct
  active sources heuristically. Material policy controls whether the Height is
  Blender-following, promoted to Vertex Position, or map-only.

Local Blender validation is intentionally machine-specific for this worktree:
every launch and headless test uses
`C:\SteamLibrary\steamapps\common\Blender\blender.exe` and asserts
`bpy.app.version == (5, 2, 0)`.

The separately installed GPL Bake Worker communicates with the MIT exporter
only through versioned request/result JSON and baked artifacts. The MIT
extension does not import the worker implementation.
