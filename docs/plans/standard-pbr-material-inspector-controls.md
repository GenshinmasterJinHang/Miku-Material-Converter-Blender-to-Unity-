# Standard PBR material inspector controls

## Purpose and outcome

MiGR Standard PBR exports must present a small, honest material-authoring surface
instead of exposing generated Blender node defaults. The generated Shader Graph
keeps all technical properties for parity and regeneration, while the Unity
material inspector exposes only twelve documented PBR controls. Identity defaults
must preserve the existing rendered result.

## Context and constraints

- The implementation source is the in-progress MiGR 1.0 staging worktree. It
  contains unrelated migration edits and deletions that must remain untouched.
- `MiGRStandardTemplate.shadergraph` is the current shared wrapper for
  `standard_pbr` and `generic_toon`; the workflows must be split before changing
  presentation.
- `MiGRStandardTemplate.generated.shadersubgraph` is MiGR-owned generated output.
  Its bytes, input/output signature, property identities, and graph behavior must
  not change in this work.
- Wrapper graphs and material variants are user-owned after creation. An existing
  wrapper may be presentation-migrated only when it byte-matches the former
  template after deterministic Sub Graph GUID substitution, or when Full
  Regeneration is explicit.
- Existing shader property reference names are public compatibility surfaces.
  Six new authoring references are additive; no existing reference may be
  renamed.
- Validation targets Unity 6000.4.5f1, URP 17.4.0, and Shader Graph 17.4.0.

## Progress

- [x] 2026-07-28: Confirmed the active Unity version and current generated asset
  identities.
- [x] 2026-07-28: Audited wrapper properties, Sub Graph outputs, importer binding,
  workflow selection, ownership behavior, target profile hashes, and tests.
- [x] 2026-07-28: Generated the Standard PBR wrapper and preserved the old
  wrapper byte-for-byte as the Generic Toon template.
- [x] 2026-07-28: Added workflow-specific template selection and exact-match safe
  wrapper migration.
- [x] 2026-07-28: Implemented canonical Standard PBR texture/constant binding and
  explicit unsupported-channel diagnostics.
- [x] 2026-07-28: Updated profile/asset identities, tests, public documentation,
  diagnostics, and changelogs.
- [x] 2026-07-28: Passed Python, package identity, Unity EditMode, compilation,
  hidden-property persistence, deterministic packaging, and live material-panel
  validation.

## Discoveries

- The former wrapper exposes 41 properties: 35 generated/internal properties and
  six meaningful PBR controls.
- The wrapper directly connects the generated Sub Graph's Base Color, Metallic,
  Smoothness, Normal TS, Emission, Occlusion, and Alpha outputs to the URP Lit
  Master Stack.
- The target is fixed Opaque with Alpha Clip disabled and material override
  disabled. Alpha Map and Alpha Clip Threshold therefore have no honest authoring
  effect in this template.
- Constant editable-graph channels currently target nonexistent
  `_MIGR_<Semantic>Value` properties. Roughness is additionally pre-inverted, so
  constant binding is silently ineffective and violates the intended conversion
  boundary.
- Shader Graph 17.4 can keep an unexposed property in the ShaderLab property block
  and `UnityPerMaterial` declaration with
  `m_GeneratePropertyBlock=false`, `overrideHLSLDeclaration=true`,
  `hlslDeclarationOverride=2`, and `m_Hidden=true`.
- Texture2D helper declarations do not honor that override. The hidden textures in
  this template do not consume tiling/offset, texel-size, or HDR helper outputs;
  tests must keep that constraint explicit and prove per-material texture
  persistence.
- Shader Graph import preserves per-material hidden Texture2D values when
  `useTilingAndOffset`, `useTexelSize`, and `isHDR` are disabled for those
  otherwise-unused hidden helpers.
- An early targeted Unity fixture encoded integral-looking channel values as
  integer JSON tokens, which changed the canonical bundle hash. The fixture was
  corrected to emit floating-point tokens like the real Blender exporter, and
  the full EditMode suite then passed.
- The active Unity package was synchronized from the validated staging package
  only after making a recoverable backup under
  `Library/MiGR/PackageBackups/standard-pbr-controls-20260728`.
- Windows Git was configured with `core.autocrlf=true`, while Shader Graph asset
  extensions had no explicit line-ending rule. `.gitattributes` now pins
  `.shadergraph` and `.shadersubgraph` to LF so checkout cannot invalidate the
  recorded deterministic hashes.

## Decision log

- 2026-07-28: Put authoring nodes in the wrapper after generated Sub Graph outputs.
  This applies to both live and baked parity paths without changing the generated
  Sub Graph contract.
- 2026-07-28: Keep the current template bytes as the Generic Toon wrapper and make
  the named Standard template the new PBR authoring wrapper.
- 2026-07-28: Use Shader Graph's native property metadata instead of replacing
  URP's Shader Graph inspector with a custom `ShaderGUI`. This preserves future
  user-added exposed properties and avoids taking ownership of URP material
  validation.
- 2026-07-28: Keep Standard fixed Opaque. Alpha authoring and AO textures remain
  explicit future workflow/schema work rather than inferred behavior.
- 2026-07-28: Accept only the former and current exact target profile hashes.
  Arbitrary profile hashes remain errors.

## Implementation sequence

1. Add a deterministic wrapper builder that preserves all existing object IDs,
   properties, and direct Sub Graph inputs; add six public properties, property
   nodes, PBR math nodes, categories, and replacement Master Stack edges.
2. Copy the unchanged former wrapper to a Generic Toon template with a stable
   `.meta` GUID, then generate the Standard wrapper from that source.
3. Extend workflow backend descriptors with the wrapper template path and resolve
   workflow before copying templates.
4. Add exact-match presentation migration for old Standard wrappers and a
   diagnostic for modified wrappers. Preserve asset paths and `.meta` GUIDs.
5. Replace Standard constant binding with canonical public properties, neutralize
   map/factor state when changing channel kind, and report unsupported normal/AO/
   alpha cases explicitly.
6. Update target profile implementation hashes, the two-hash importer
   compatibility window, package asset identity, tests, documentation, and
   changelogs.
7. Synchronize the validated package to the active Unity test project and migrate
   the unmodified `玻璃雾岩` sample.

## Validation

- `python -m unittest discover -s tests -p "test_*.py"` must pass.
- `python tools/migr_package_identity.py --check` must pass after regenerating the
  checked-in identity manifest.
- `python tools/build_migr_unity_package.py` must produce deterministic package
  bytes.
- Unity EditMode tests must pass on Unity 6000.4.5f1 and assert:
  - the Standard visible property set is exactly the documented twelve;
  - hidden properties retain `Material.HasProperty` and independent saved values;
  - representative hidden Texture2D bindings survive alternating-material
    save/reload without cross-material leakage;
  - the Shader Graph compiles without errors and remains fixed Opaque;
  - roughness is converted exactly once;
  - Generic Toon keeps the former wrapper behavior;
  - ordinary import preserves modified wrappers, exact-match migration is safe,
    and Full Regeneration keeps GUID/reference identity;
  - repeated generation is deterministic and both known profile hashes are
    accepted while unknown hashes are rejected.
- Live validation must compare the current sample at identity defaults, adjust
  Roughness Strength, Normal Strength, and Emission Strength, inspect the material
  panel, and confirm no Unity console errors.

## Results and follow-up

Implementation and validation completed on 2026-07-28.

- The Standard wrapper exposes exactly the twelve documented controls in
  `Surface Inputs` and `Emission`. Thirty-five technical properties remain in the
  graph and material property block but are hidden from the inspector.
- The Generic Toon wrapper is the exact former shared wrapper
  (`430427de4cac421dd16ccc5a273339f17f500a2e368e6cb98f2277bce8023780`).
  The generated Sub Graph remains byte-identical
  (`af28776210ca64bac9fb5a82e8da3b9fd15e175c9c8b8addf74802f0c6fc92cb`).
- The new Standard wrapper hash is
  `88ca621a082ecab466302b90b7ccafe48660240d7100d3022f4cc08786d71974`.
  The importer accepts only the former and current exact target profile hashes.
- `python -m unittest discover -s tests -p "test_*.py"` passed 89 tests.
- `python tools/migr_package_identity.py --check` and
  `python tools/build_migr_standard_pbr_wrapper.py --check` passed.
- `python tools/build_migr_unity_package.py` was run twice and produced the same
  package SHA-256:
  `06da9057fa6f2cc4b4b002e6448ee0e25e494a8f043060ed0b0bee6c87bad2a5`.
- Unity EditMode assembly `MiGR.ShaderConverter.Editor.Tests` passed 26/26 tests
  in 24.449 seconds on Unity 6000.4.5f1, URP 17.4.0, and Shader Graph 17.4.0.
  Final Unity console inspection reported zero errors and zero warnings.
- Safe live migration of `玻璃雾岩` preserved the Wrapper, base Material, and
  Material Variant GUIDs. The Material Variant serialized SHA-256 stayed exactly
  `9e41aeedb43f106682004563d6c97d1289d28a3dd3f17e100e87ad48ccc23d27`.
  In-memory Unity previews for Roughness Strength, Normal Strength, and Emission
  Strength all produced distinct image hashes from the neutral baseline without
  writing changes to the material assets.
- The final live inspector evidence is stored at
  `docs/images/standard-pbr-material-inspector.png`.

There is no MiGR IR or bundle schema change. Public Shader API impact is additive:
`_BaseColor`, `_Metallic`, `_Roughness`, `_NormalStrength`, `_EmissionColor`, and
`_EmissionStrength` are new references; existing references and identities remain
unchanged. AO Map support and explicit Opaque/AlphaClip/Transparent wrapper
selection remain intentionally out of scope.
