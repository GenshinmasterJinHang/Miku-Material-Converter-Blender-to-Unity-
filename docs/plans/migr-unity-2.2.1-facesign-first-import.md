# MiGR Unity 2.2.1 FaceSign First-Import Fix

## Purpose and outcome

Release a Unity-only `2.2.1` patch that makes a newly generated Shader Graph
compile on its first import when the reachable MaterialIR expression graph
contains `Input.IsFrontFace`. The importer must preserve the native,
Fragment-only Shader Graph `IsFrontFaceNode`, wait until the generated Sub
Graph has its stable GUID and a loadable imported main asset, and only then
create or import the Wrapper that references it. When MaterialIR has no
runtime Displacement expression, the Wrapper must also leave Vertex Position
at the URP Master Stack default instead of invoking the fragment-dependent Sub
Graph from the vertex stage for an identity position result.

The observable acceptance result is that the existing Magic Ball 1, 4, and 5
Bundle 2.2 assets import without `FaceSign` shader errors, repeat imports are
stable, Magic Ball 9 does not regress, and existing Bundle 2.0/2.1/2.2 files
remain compatible.

## Context and constraints

The canonical implementation root is
`unity/Packages/com.migr.shaderconverter/`. Installed PackageCache copies,
validation-project package copies, retired B2U roots, Blender installations,
and `dist/` archives are build outputs and must not be edited as source.

The failure occurs only during Unity's first generated-asset import. The
Blender exporter has already produced valid MaterialIR 2.0. A reachable
`Input.IsFrontFace` lowers to Shader Graph's native `IsFrontFaceNode`, whose
Sub Graph requirement must propagate into the Wrapper before Wrapper shader
compilation. Previously the importer let Unity create a random `.meta` GUID,
rewrote it to the MiGR stable GUID, and proceeded without a verified,
force-updated Sub Graph dependency barrier. The Wrapper could therefore
compile against a stale Sub Graph importer artifact whose requirements omitted
`FaceSign`.

Generated Sub Graphs are MiGR-owned. Wrapper graphs become user-owned after
initial creation and may only be rewritten by explicit Full Regeneration.
Stable asset IDs, templates, target profile hashes, public Shader properties,
Bundle 2.2, and MaterialIR 2.0 are compatibility surfaces and remain
unchanged. The generated Wrapper MultiJson changes only for newly created or
explicitly regenerated materials with no runtime displacement: its redundant
Sub Graph-to-Vertex Position edge is omitted. Existing user-owned Wrappers are
not rewritten.

The repository has unrelated dirty and untracked work. This patch must touch
only the importer, its focused EditMode regression, Unity package version
surfaces, release documentation, and this plan.

Validated environment:

- Windows / D3D11
- Unity Editor `6000.4.5f1`
- URP `17.4.0`
- Shader Graph `17.4.0`
- Blender exporter remains `2.2.0`
- Unity package becomes `2.2.1`

## Progress

- [x] 2026-07-30: Confirmed all canonical source-boundary markers and the
  active Unity package identity.
- [x] 2026-07-30: Reproduced the semantic distinction: Magic Ball 1, 4, and 5
  retain reachable `Input.IsFrontFace`; Magic Ball 9's equivalent expression
  is unreachable and pruned.
- [x] 2026-07-30: Confirmed failed transactions were first imports and rolled
  back with no pre-existing material root.
- [x] 2026-07-30: Confirmed installed `2.2.0` importer/backend files match
  canonical source hashes; installation corruption is not the cause.
- [x] 2026-07-30: Implemented the Sub Graph GUID/import synchronization
  barrier and force-updated final graph imports.
- [x] 2026-07-30: Added a first-import and deterministic reimport EditMode
  regression with a reachable Layer Weight Fresnel `IsFrontFace` chain.
- [x] 2026-07-30: Repository checks and the full EditMode suite passed for the
  initial GUID-barrier implementation, but live Magic Ball validation proved
  that implementation incomplete.
- [x] 2026-07-30: Built the initial `2.2.1` TGZ twice; both archives had
  identical manifests and SHA-256
  `81e66b961aecda53adaef6a6ebfec1f9b1ca6fac7b0e13be163ae8287a676eea`.
- [x] 2026-07-30: Reflected Shader Graph 17.4 and captured the generated
  ShadowCaster vertex HLSL. The real failure is the redundant Vertex Position
  edge invoking a fragment-dependent Sub Graph from
  `VertexDescriptionFunction`.
- [x] 2026-07-30: Implemented conditional Vertex Position connection based on
  the presence of a MaterialIR Displacement expression.
- [x] 2026-07-30: Re-ran repository checks, the full Unity EditMode suite, and
  deterministic packaging after the corrected implementation.
- [x] 2026-07-30: Installed the deterministic TGZ into the validation project,
  verified installed hashes, and reimported Magic Ball 1, 4, 5, and 9.
- [x] 2026-07-30: Recorded final results, limitations, and release evidence.

## Discoveries

- Shader Graph's `IsFrontFaceNode` implements `IMayRequireFaceSign`; its
  generated code reads `IN.FaceSign`. The parent Wrapper receives that field
  only when the imported Sub Graph asset reports `requiresFaceSign`.
- The existing `EnsureMetaGuid` refreshes after rewriting a generated
  `.meta`, but a refresh alone does not guarantee that the path-to-GUID mapping
  and Sub Graph importer artifact are current before Wrapper creation.
- Existing package tests already use
  `ForceSynchronousImport | ForceUpdate` when importing a Sub Graph dependency
  before its Wrapper. The production importer did not use the same barrier.
- Globally changing `EnsureMetaGuid` would affect textures, materials, meshes,
  prefabs, and live object references. The fix therefore uses a targeted
  generated-Sub-Graph synchronization step.
- Live validation disproved the original conclusion that stale Sub Graph
  requirements were the sole cause. After the dependency barrier, the exact
  Magic Ball 1 Sub Graph imported with `requirements.requiresFaceSign == true`
  and a loadable native `IsFrontFaceNode`, yet the Wrapper still failed.
- Shader Graph 17.4 emits one monolithic Sub Graph function containing all
  outputs. MiGR had always connected the Sub Graph's identity `Vertex Position`
  output even when MaterialIR contained no Displacement value. ShadowCaster
  therefore invoked the fragment-dependent function from
  `VertexDescriptionFunction`; generated transfer code attempted
  `bindings.FaceSign = IN.FaceSign`, but `VertexDescriptionInputs` correctly
  has no fragment face-sign field.
- Removing only that redundant identity Vertex Position edge from a temporary
  exact Magic Ball 1 Wrapper changed the shader from
  `invalid subscript 'FaceSign'` to no compile error. Magic Ball 1, 4, and 5
  all have a Vertex-stage Displacement channel with no value and therefore do
  not require a runtime vertex connection.

## Decision log

- 2026-07-30: Keep `Input.IsFrontFace` as the native Fragment-only Shader
  Graph node. Replacing it with a constant was rejected because it silently
  changes Layer Weight/Fresnel semantics.
- 2026-07-30: Place the synchronization barrier after either structured Sub
  Graph generation or template copying, and before any Wrapper existence or
  ownership decision. This gives both first-created and regenerated wrappers
  the same dependency guarantee without changing Wrapper ownership.
- 2026-07-30: Validate both `AssetDatabase.AssetPathToGUID` and
  `LoadMainAssetAtPath`. GUID mismatch reports
  `MIGR_SUBGRAPH_GUID_SYNC_FAILED`; failed or missing imported assets report
  `MIGR_SUBGRAPH_IMPORT_FAILED`. Existing transaction rollback remains the
  recovery mechanism.
- 2026-07-30: Use `ForceUpdate | ForceSynchronousImport` for the final Sub
  Graph and Wrapper imports to prevent reuse of a stale first-import Shader
  importer artifact.
- 2026-07-30: Amend the original import-order-only plan based on live compiler
  evidence. Keep the dependency barrier, but connect the generated Sub Graph
  to `VertexDescription.Position` only when the MaterialIR Displacement
  channel contains an Expression value. The omitted edge represented
  `Vertex Position = Object Position`, so leaving the URP Master Stack default
  is semantically equivalent and avoids evaluating fragment-only nodes in a
  vertex pass.
- 2026-07-30: Preserve Wrapper ownership. This MultiJson change applies only
  while a Wrapper is first created or when Full Regeneration is explicitly
  requested; an existing user-owned Wrapper remains byte-for-byte untouched.
- 2026-07-30: Ship a Unity-only patch. Blender exporter, GPL bake worker,
  schemas, target profile hash, public C# API, Shader properties, stable IDs,
  and generated Sub Graph serialization remain unchanged.

## Implementation sequence

1. Add a targeted Sub Graph synchronization helper to
   `MiGRBundleImporter.cs`.
2. Call it immediately after the generated Sub Graph content and stable
   `.meta` GUID exist, before Wrapper creation/import.
3. Force-update the final Sub Graph and Wrapper imports.
4. Add an EditMode regression starting with no output directory or `.meta`,
   build a reachable
   `Input.IsFrontFace -> Math.LayerWeightFresnel -> Roughness` chain, compile
   the Wrapper, and verify repeat-import byte/GUID stability and Wrapper
   preservation.
5. Connect Wrapper Vertex Position only for a real MaterialIR Displacement
   expression; retain the generated identity output for Sub Graph
   compatibility and deterministic IDs.
6. Update the package manifest/import receipt version lock and release
   documentation for Unity package `2.2.1`.
7. Run repository checks, exact Unity EditMode tests, deterministic packaging,
   installed-package hash verification, and live Magic Ball reimports.

## Validation

Required commands:

```powershell
python tools/ci/run_checks.py
powershell -File tools/ci/run_unity_editmode.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe"
python tools/build_migr_unity_package.py
```

Build the TGZ twice from unchanged source state. Compare archive member paths,
member SHA-256 values, complete package asset GUID manifests, and whole-archive
SHA-256 values.

Install `com.migr.shaderconverter-2.2.1.tgz` into the existing validation
project. Compare the installed package's complete file manifest and SHA-256
values with the deterministic canonical build. Force-reimport Magic Ball 1, 4,
and 5 and require generated Wrapper, Sub Graph, and Material assets with no
`MIGR_AUTO_IMPORT_FAILED`, `MIGR_SHADER_COMPILE_FAILED`, or `FaceSign` console
error. Reimport again and require stable generated-tree hashes and GUID
mappings. Preserve and revalidate Magic Ball 9.

## Results and follow-up

Implementation and requested validation are complete.

- `py -3.13 tools/ci/run_checks.py` passed: canonical boundary, 73 Python
  files, 16 schemas, 179 Python tests, package identity, Blender exporter
  2.2.0, GPL worker 1.2.0, and Unity package 2.2.1.
- `tools/ci/run_unity_editmode.ps1` passed under Unity 6000.4.5f1:
  `total=102`, `passed=101`, `failed=0`, `skipped=1`.
- Two unchanged-source package builds each contained 144 files and 2,677,412
  unpacked bytes. Their complete member path/length/SHA-256 manifests matched,
  and both TGZ files had SHA-256
  `79c23c57840d68913a333d06948a344ce5f84c7d7cefe87d8912060b8b23daa8`.
- The installed package resolved to
  `Library/PackageCache/com.migr.shaderconverter@73dd559c2f5f`; its complete
  package identity manifest matched canonical source. The installed and
  canonical importer SHA-256 both equal
  `c59ee09d286863989d4ec97c5a8815faa655223608095a85e21f1601ede947f6`.
- Magic Ball 1, 4, and 5 first imports committed with one Wrapper, one
  generated Sub Graph, one generated base Material, and one user Material
  Variant each. All three Wrappers loaded as supported shaders with zero
  compile errors, retained native `IsFrontFaceNode`, and referenced the stable
  imported Sub Graph GUID.
- Their complete 15-file generated-tree hashes were respectively
  `0c3c07d0f35f1f8fd9b78c2a441dcf36b7ef4a5a52aa0e1095697bc48a878705`,
  `2918aa62d3c999897333eef27c57c28b76f26a02bed753370b802691a01667f2`,
  and
  `3008f055113c6fa091b092502ae6bcfc8f56c195f4cf3725467f5c95312baa5c`;
  all three were identical after a second import.
- Magic Ball 9 retained its pre-upgrade Wrapper, Sub Graph, generated Material,
  and user Material hashes. Its 2.2.1 generated-tree hash
  `3993b98f35003bcee7b48960307776bb8290fba733d5ae5c69a3aa0b6bd2bcb4`
  was identical on a second 2.2.1 import. The receipt changed once from the
  previous package version as expected.
- After the acceptance imports and repeat imports, the Unity Console contained
  zero errors; in particular there was no `MIGR_AUTO_IMPORT_FAILED`,
  `MIGR_SHADER_COMPILE_FAILED`, or `FaceSign` error.

No schema, target profile hash, public C# API, public Shader property,
MaterialIR, Bundle, stable ID, or generated Sub Graph format changed. The only
generated Wrapper difference is omission of a redundant identity Vertex
Position edge for newly created or explicitly regenerated no-displacement
materials. Existing user-owned Wrappers remain untouched.

The exact tuple remains `Experimental` because this task executed import and
compile validation, not the separate final human render review. Blender 5.2.0
was not launched because the patch is Unity-only and Blender exporter 2.2.0
bytes/version were intentionally unchanged.
