# AGENTS.md — Miku Engineering Constitution

## 1. Project Identity

Miku is a production-quality, public open-source project.

Its primary purpose is:

Blender 5.2 Shader Nodes
→ target-neutral Miku semantic IR
→ Unity 6 URP Shader Graph
→ editable `.shadergraph` and `.shadersubgraph` assets

Miku is not a disposable prototype, code-generation experiment, or demo.

Every contribution must prioritize:

1. Correctness
2. Deterministic behavior
3. Data integrity
4. Maintainability
5. Compatibility
6. Clear diagnostics
7. Performance
8. Feature coverage

Do not trade correctness or asset safety for faster implementation.

The current primary target is:

- Blender 5.2
- Unity 6
- URP
- Shader Graph
- Editor-time conversion
- Editable Shader Graph assets

Do not generate ShaderLab unless a future approved architecture decision explicitly reintroduces that backend.

---

## 1.1 Canonical Miku 1.x Source Boundary

Miku 1.x feature work must use only these canonical source roots:

- `miku/`
- `miku_blender/`
- `extensions/miku_shader_converter/`
- `unity/Packages/com.miku.shaderconverter/`

The active Blender extension IDs are `miku_semantic_exporter` and
`miku_gpl_bake_worker`. The active Unity package ID is
`com.miku.shaderconverter`.

Before changing Miku 1.x source, preflight the repository root and stop if any
of these markers is missing:

- `miku/`
- `extensions/miku_shader_converter/`
- `unity/Packages/com.miku.shaderconverter/package.json` whose `name` is
  `com.miku.shaderconverter`

Passing this marker check establishes the canonical repository boundary. A
retired B2U checkout, a Unity validation project's embedded
`Packages/com.miku.shaderconverter/`, an installed Blender extension, or a
`dist/` archive must never be selected as the implementation root.

The following paths and identities belong to the retired B2U architecture and
must not receive new Miku features unless a task explicitly authorizes legacy
migration or removal work:

- `b2u_mvp/`
- `b2u_mvp_blender/`
- `addons/b2u_mvp_blender/`
- `unity/Packages/com.b2u.shaderconverter/`

Installed Blender extensions and `dist/` archives are build outputs, not source
of truth. Never patch an installed copy as the implementation. Change the
canonical Miku source, build deterministic packages, install those packages,
and verify the installed module paths and hashes. Unity validation projects and
installed Blender extension copies may only be populated from deterministic
canonical-source builds. Compare their file manifests and SHA-256 hashes with
the canonical build before treating validation results as evidence.

For this repository's Windows validation environment, the Blender installation
root is fixed to:

`C:\SteamLibrary\steamapps\common\Blender`

All Blender launches, headless tests, extension installation, and export
validation must call this executable directly:

`C:\SteamLibrary\steamapps\common\Blender\blender.exe`

Do not use `PATH`, `blender-launcher.exe`, `.tools`, Program Files, or another
Blender installation as a fallback. Before validation, assert
`bpy.app.version == (5, 2, 0)` and fail clearly on any mismatch. Do not overwrite
installed extensions while a Blender GUI process is running or contains
unsaved work; save and close Blender first.

---

## 2. Required Working Process

Before modifying code:

1. Read this file completely.
2. Read the nearest nested `AGENTS.md`, if one exists.
3. Inspect the repository structure.
4. Read the relevant architecture and compatibility documents.
5. Search for existing implementations before creating new abstractions.
6. Inspect existing tests and fixtures.
7. Check the current Git diff.
8. Identify the exact supported Blender, Unity, URP, and Shader Graph versions.

For a complex feature, cross-module refactor, schema change, compatibility change, or task expected to affect more than one subsystem:

- Create or update an ExecPlan under `docs/plans/`.
- Follow `PLANS.md`.
- Keep the plan updated while implementing.
- Record discoveries, decisions, rejected alternatives, tests, and remaining work.
- Do not let implementation silently diverge from the plan.

Do not stop after producing a plan unless the task explicitly requests planning only. Continue through implementation and validation.

---

## 3. Repository Scope and Ownership

Respect existing repository structure.

Do not create parallel implementations of functionality that already exists.

The intended architectural boundaries are:

- Blender integration:
  Reads Blender data through supported Blender APIs.
  Converts Blender-specific nodes into target-neutral Miku data.

- Core:
  Owns semantic IR, type checking, graph validation, normalization,
  effect recognition, diagnostics, deterministic IDs, and schema handling.
  Core must not depend on UnityEditor or bpy.

- Unity integration:
  Reads target-neutral Miku IR.
  Selects a version-specific Shader Graph backend.
  Generates Unity assets and reports Unity-specific requirements.

- Schemas:
  Owns versioned interchange formats.
  Schema changes require compatibility documentation and tests.

- Tests:
  Owns unit, integration, snapshot, fixture, and compatibility tests.

- Documentation:
  Owns architecture, contributor, compatibility, schema, and user documentation.

Do not place Blender-specific or Unity-specific types in the target-neutral core model.

Do not expose Unity Shader Graph internal class names in Miku interchange files.

---

## 4. Architectural Rules

### 4.1 Target-neutral IR

The Miku IR must express semantics rather than target implementation details.

Good:

- multiply
- gradient_noise
- alpha_clip
- material_blend
- object_position
- fragment_stage
- dissolve
- roughness
- coordinate_space

Bad:

- UnityEditor.ShaderGraph.MultiplyNode
- Unity internal slot numbers
- Unity object IDs as semantic node identities
- Blender Python object references
- serialized Unity Target internals

All IR documents must have an explicit schema version.

Unknown schema versions must fail with a clear diagnostic.

Do not silently reinterpret unknown fields or versions.

### 4.2 Strong typing

Expressions and ports must preserve relevant type information, including:

- scalar/vector/color/texture type
- coordinate space
- shader stage
- uniformity
- source node identity
- source socket identity

Coordinate spaces must not be treated as interchangeable.

At minimum, distinguish:

- None
- UV0
- UV1
- Object
- World
- Absolute World
- View
- Tangent
- Screen

Shader stages must distinguish:

- Vertex
- Fragment
- Both

A fragment-only operation must never be emitted into a vertex-stage chain.

### 4.3 Version-specific backends

Do not implement a generic writer that assumes every Unity 6 or Shader Graph 17 version uses the same internal format.

Use version adapters.

Example:

- ShaderGraph17UrpBackend
- ShaderGraph17_0UrpBackend
- ShaderGraph17_6UrpBackend

All access to Shader Graph internal serialization details must be isolated in the backend or serialization adapter.

Business logic must not directly depend on Unity internal Shader Graph types.

### 4.4 Template-based generation

Prefer:

versioned wrapper graph template
+ generated Sub Graph
+ version-specific serializer

over constructing every Target and Master Stack object from assumptions.

Never invent undocumented Shader Graph serialization fields from memory.

Verify internal fields against:

- the installed Shader Graph package source
- real assets created by the target Unity version
- checked-in versioned fixtures

### 4.5 Asset ownership

Generated assets must have explicit ownership.

Default model:

- `*.generated.shadersubgraph` is owned by Miku and may be regenerated.
- Wrapper `*.shadergraph` is user-owned after initial creation.
- Sidecar mapping and report files are owned by Miku.

Do not overwrite a user-modified wrapper graph unless Full Regeneration was explicitly selected.

### 4.6 Determinism

The same normalized input and backend version must produce byte-stable or semantically stable output.

Do not create random IDs during ordinary regeneration.

Use deterministic IDs derived from stable source identities.

Unrelated nodes must retain their IDs when another node changes.

Output ordering must be deterministic.

Timestamps, machine paths, random GUIDs, and unstable dictionary ordering must not cause meaningless diffs.

### 4.7 Failure behavior

Never silently produce a graph that is known to be semantically wrong.

Use explicit translation qualities:

- Exact
- Equivalent
- Approximate
- Baked
- RequiresProjectSetup
- RequiresRuntimeSupport
- Unsupported

Rules:

- Unsupported node outside an active output chain:
  emit a warning and continue when safe.

- Unsupported node on a required output chain:
  emit an error and stop generation for that material.

- Approximation:
  emit a warning that explains the visual or semantic difference.

- Missing project configuration:
  generate the graph only when safe and report the required setup.

Do not replace an unsupported operation with zero, black, white, or a pass-through value without a diagnostic.

---

## 5. Shader Translation Invariants

The following are correctness invariants.

### Roughness

Blender roughness maps to Unity smoothness as:

smoothness = 1 - roughness

Never connect Blender roughness directly to Unity Smoothness.

### Alpha clipping

Dissolve and hard cutout coverage must preserve:

- continuous mask
- threshold
- inversion direction
- edge band semantics
- applicable shadow/depth behavior provided by the target graph

Do not reduce all Transparent BSDF mixes to ordinary color interpolation.

### Coordinate space

Object-space procedural textures must not silently become world-space textures.

Position, direction, and normal transformations are different operations.

Normal transformations must account for non-uniform scaling where required.

### Shader stage

Vertex displacement must be emitted into Vertex Position.

Scene Color, Scene Depth, screen derivatives, and fragment-only operations must not be moved into the vertex stage.

### Texture semantics

Preserve, when available:

- color versus non-color data
- normal-map semantics
- UV source
- projection
- wrapping intent
- interpolation intent
- missing texture diagnostics

### Color ramps

Preserve:

- element order
- position
- color
- alpha
- interpolation mode

Do not assume every Color Ramp is a two-point linear interpolation.

---

## 6. Public API and Schema Compatibility

Treat the following as public compatibility surfaces:

- Miku JSON schemas
- generated property reference names
- package identifiers
- documented CLI options
- public C# APIs
- Blender add-on operators and public settings
- generated mapping file structure

Breaking changes require:

1. An explicit architecture decision.
2. A schema or major version change where applicable.
3. Migration documentation.
4. Compatibility tests.
5. Changelog entry.
6. Release notes.

Do not rename an exposed shader property merely for style.

Property reference names can be used by materials, animations, scripts, and user tooling.

---

## 7. Code Quality

Follow the repository’s configured formatter, analyzer, linter, and naming conventions.

Do not disable a warning, analyzer, lint rule, or test merely to make CI pass.

Suppressions require:

- a narrow scope
- an explanatory comment
- a documented reason
- evidence that the suppression is safe

Prefer:

- small cohesive types
- explicit data models
- immutable value objects where practical
- dependency injection at external boundaries
- result types for expected failures
- structured diagnostics
- pure functions in transformation stages

Avoid:

- global mutable state
- giant manager classes
- stringly typed node logic
- unbounded reflection
- regex as the primary Shader Graph parser
- catch-all exception swallowing
- hidden fallback behavior
- duplicated semantic mappings
- direct file writes without atomic replacement

Public APIs require documentation.

Complex code requires explanation of why it exists, not line-by-line narration.

Do not leave unexplained TODO or FIXME comments.

When work must be deferred, create a documented issue reference or add it to the active plan.

---

## 8. File and Data Safety

Treat imported Miku JSON, Blender paths, texture paths, and generated asset names as untrusted input.

Validate:

- schema
- input size
- path normalization
- allowed output roots
- node counts
- edge counts
- duplicate IDs
- cycles where prohibited
- illegal port connections
- missing references
- invalid numeric values
- NaN and Infinity
- malformed texture paths

Prevent:

- path traversal
- writing outside the selected project directory
- arbitrary code evaluation
- command injection
- accidental overwrite of unrelated files
- partial file corruption

Use temporary files and atomic replacement for generated assets.

Do not log secrets, tokens, or unnecessary absolute user paths.

---

## 9. Dependency and License Rules

Do not add a production dependency without:

- demonstrating that existing dependencies or standard libraries are insufficient
- documenting why it is needed
- checking maintenance status
- checking license compatibility
- checking security implications
- considering package size and runtime/editor impact

Do not copy source code from third-party projects unless its license and required attribution are explicitly verified and recorded.

Maintain third-party attribution in the repository’s designated notice file.

Do not invent or change the project license without an explicit maintainer decision.

Generated code, templates, fixtures, and copied algorithms must have documented provenance.

---

## 10. Testing Requirements

Every behavior change must have tests at the lowest practical layer.

Required test categories include:

### Core unit tests

Test:

- IR validation
- type propagation
- coordinate-space propagation
- shader-stage validation
- normalization
- effect recognition
- deterministic IDs
- diagnostics
- schema migration
- roughness-to-smoothness conversion

### Blender exporter tests

Test:

- node extraction
- sockets and links
- node groups
- default values
- Color Ramp elements
- texture metadata
- stable source IDs
- malformed and unsupported graphs

Prefer small programmatically constructed Blender test graphs where possible.

### Unity EditMode tests

Test:

- backend selection
- template compatibility
- node creation
- port resolution
- property creation
- generated asset import
- generated report creation
- wrapper ownership behavior

### Golden and snapshot tests

Use normalized semantic snapshots where full serialized files are too version-sensitive.

Golden tests must explain how fixtures are updated and reviewed.

Do not update snapshots automatically merely because a test failed.

### Determinism tests

Generate the same asset more than once and verify stable output.

Changing one source value must not change unrelated object IDs.

### Negative tests

Test:

- unsupported critical nodes
- missing textures
- invalid ports
- invalid coordinate-space conversions
- schema mismatch
- incompatible Shader Graph template
- malformed MultiJson
- unsafe output paths

Do not claim a test passed unless it was actually executed.

When an environment prevents execution, clearly distinguish:

- executed and passed
- implemented but not executed
- blocked, with reason

---

## 11. Documentation Requirements

Public behavior must be documented.

Maintain, as appropriate:

- README.md
- CONTRIBUTING.md
- CODE_OF_CONDUCT.md
- SECURITY.md
- SUPPORT.md
- GOVERNANCE.md
- CHANGELOG.md
- ROADMAP.md
- LICENSE
- third-party notices
- compatibility matrix
- architecture overview
- schema documentation
- node support matrix
- diagnostic code reference
- release process
- local development instructions
- Blender add-on installation instructions
- Unity package installation instructions

Use English as the canonical public project documentation language.

A `docs/zh-CN/` translation may be maintained, but it must not contradict the canonical documentation.

Documentation examples must be tested or derived from tested fixtures where practical.

---

## 12. Compatibility Documentation

Never describe support only as “Unity 6” or “Blender 5”.

Record exact validated versions:

- Blender version
- Unity Editor version
- URP package version
- Shader Graph package version
- operating system when relevant

Maintain a compatibility matrix with statuses such as:

- Supported
- Experimental
- Community-tested
- Unsupported
- Unknown

Do not claim compatibility without a reproducible test or documented validation.

Unsupported versions must fail clearly rather than attempting unsafe generation.

---

## 13. Open-source Contribution Standards

Changes should be reviewable by external contributors.

Prefer small, coherent pull requests.

Each pull request should explain:

- problem
- proposed solution
- alternatives considered
- compatibility impact
- schema impact
- security impact
- tests
- documentation changes
- screenshots or generated graph evidence when relevant

Avoid mixing:

- unrelated formatting
- broad renames
- architecture refactors
- new features
- generated fixture updates

in one pull request unless they are inseparable.

Use the repository’s commit convention.

Do not rewrite contributor history or force-push shared branches.

---

## 14. Code Review Rules

When reviewing changes, prioritize consequential correctness issues over formatting.

Flag:

- silent semantic degradation
- unstable generated IDs
- accidental overwrite of user-owned assets
- graph corruption
- schema changes without versioning
- public property renames
- roughness connected directly to smoothness
- incorrect coordinate-space conversion
- fragment-only operations in vertex paths
- unsupported nodes silently replaced by constants
- unsafe file paths
- use of undocumented Shader Graph internals outside a backend
- unlicensed copied source
- tests that do not test the claimed behavior
- documentation claiming versions that were not validated

Formatting and deterministic mechanical checks belong in automated tooling.

---

## 15. Definition of Done

A task is not complete merely because code compiles.

It is complete only when all applicable conditions are met:

- requested behavior is implemented
- architecture boundaries are preserved
- input validation is present
- errors produce structured diagnostics
- tests are added or updated
- relevant tests were executed
- formatting, linting, and analyzers pass
- documentation is updated
- compatibility impact is documented
- schema impact is documented
- changelog is updated when user-visible behavior changes
- generated output is deterministic
- no unrelated files were modified
- the final diff was self-reviewed
- known limitations are reported honestly

The final response must include:

1. Summary of the implementation.
2. Important design decisions.
3. Files changed.
4. Tests and commands executed.
5. Tests not executed and why.
6. Compatibility impact.
7. Schema or public API impact.
8. Known limitations.
9. Any follow-up work that is genuinely outside the task scope.
