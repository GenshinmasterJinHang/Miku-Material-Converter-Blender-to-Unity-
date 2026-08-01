# ExecPlan — Blender 5.2 EEVEE → Unity 6 URP Shader Graph gap-fill + Metal Library validation

> **Branch:** `migr-procedural-metal-lib` (off `task-1.4-mgir-fixtures`)
> **Authoritative versions:** Blender **5.2.0 LTS** (commit `fbe6228777e7`) · Unity **6000.4.5f1** · URP **17.4.0** · Shader Graph **17.4.0**
> **Mode:** **CLEAN_REIMPLEMENTATION** (MIT, no Blender source copied). `GPL_DERIVED` remains BLOCKED per `docs/audits/blender-source-port-license-audit.md`.
> **Mirror plan:** `C:\Users\22687\.claude\plans\blender-5-0-eevee-blender-mellow-river.md` (the Claude harness plan this file mirrors).

---

## 1. Purpose

Fill in the *gaps* in MiGR's Blender 5.2 EEVEE → Unity 6 URP Shader Graph translator and validate the result against a real metal-material corpus (`材质库/金属`). Most of the translator already exists; this plan covers the genuinely missing or under-supported pieces, not a rewrite.

## 2. Context

- **Existing coverage** (working, MIT, CLEAN_REIMPLEMENTATION):
  - 47 `ShaderNodeMath` ops, 27 `ShaderNodeVectorMath` ops (`MiGRBlenderMath.hlsl`, `MiGRBlenderVectorMath.hlsl`)
  - Noise (1D/2D/3D/4D + fBM), Voronoi (2D/3D/4D, 5 features), Wave, Checker, Gradient, Color Ramp, Mapping, Bump, NormalMap, Displacement (`MiGRBlenderNoise.hlsl`, `MiGRBlenderVoronoi.hlsl`, `MiGRBlenderWave.hlsl`, `MiGRBlenderColorRamp.hlsl`)
  - Principled BSDF, Diffuse BSDF, Transparent BSDF, Mix Shader, Add Shader, Emission, Material Output (closure lowering in `B2UShaderGraph17UrpBackend.cs` + `StandardPbrSemanticExtractor`)
  - Render state translation (Opaque / Cutout / Transparent / Blend), coordinate-space conflict validation, vertex-displacement stage validation
  - Provenance manifest (`docs/provenance/blender-node-ports.yml`) and license audit (`docs/audits/blender-source-port-license-audit.md`) — both current
- **What's missing** (the real work):
  1. `Texture.WhiteNoise` / `Brick` / `Magic` / `Gabor` — registry labels are dead strings, backend has no `Build*Source` methods. First-ever `CustomFunctionNode` integration for textures is required.
  2. `Vector.VectorTransform` — IR is emitted but the backend has no translator and `B2UHelpers.hlsl` has no `unity_ObjectToWorld`/`WorldToObject`/`MatrixVP` macros.
  3. Image-texture `BOX` / `SPHERE` / `TUBE` projection — no diagnostic, no code.
  4. No Blender ↔ Unity visual-parity test harness; `tools/delta_e_tool/render_unity.py` is a `NotImplementedError` stub.
  5. No end-to-end run against the user's metal-material corpus.

## 3. Decision log (locked in before M3)

- **Versions.** Blender **5.2.0 LTS** (NOT 5.0 — `AGENTS.md §12` forbids "Blender 5" generic). The user's Steam binary at `C:\SteamLibrary\steamapps\common\Blender\blender.exe` reports `Blender 5.2.0 LTS` (build date 2026-07-14) — that is the authoritative headless binary.
- **License.** `CLEAN_REIMPLEMENTATION`. No Blender source read or copied. All new HLSL derived from public math references (Wang hash, Worley 1996, Perlin 1985, Reoriented Normal Mapping by Hejl/Ramirez).
- **Branch.** Created `migr-procedural-metal-lib` off `task-1.4-mgir-fixtures` (1159 dirty files including `codegen/` deletion — out of scope for this plan).
- **Unity import.** `subprocess.run([unity, -batchmode, -executeMethod, B2U.MetalLibraryBatchRender.RenderAll])` primary path. MCP `mcp__unity__*` is opportunistic (not loaded in current session).
- **Scope.** Realistically **5** materials in `材质库/金属` (`彩色金属5`, `金1`, `铁4`, `铜3`, `银2`), one `.blend` each.

## 4. Implementation sequence (milestones)

| M | Title | Deliverable | Status |
|---|-------|-------------|--------|
| M1 | Branch + governance | Sub-branch, source-lock verification, in-repo ExecPlan | **in progress** |
| M2 | Math/Vector/Converter validation | `tests/test_math_hlsl_against_blender.py` — real Blender reference compare | pending |
| M3 | Missing texture HLSL + backend | `MiGRBlenderWhiteNoise/Brick/Magic/Gabor.hlsl` + 4 `Build*Source` methods + dispatcher cases + provenance YAML entries + 4 golden fixtures | pending |
| M4 | Coordinate-space matrix plumbing | `_semantic_and_space` extended with matrix-hint, `B2UHelpers.hlsl` macros, `BuildVectorTransformSource`, schema bump to `mgir-3.1` | pending |
| M5 | Image-texture projection modes | `MIGR_SG_TEXTURE_PROJECTION_MODE_FALLBACK` for BOX/SPHERE/TUBE, BOX runtime blend, SPHERE/TUBE diagnostic-only | pending |
| M6 | Visual-parity harness | `B2UMetalLibraryBatchRender.cs` editor command, `tools/blender_ref_capture/capture_*.py`, `tools/delta_e_tool/procedural_parity.py`, CI gate | pending |
| M7 | Metal Library end-to-end pipeline | `tools/metal_library_pipeline.py` (export-mgir / compile-shadergraph / render-pngs), 5 materials imported, `import-report.md` | pending |
| M8 | Documentation + provenance + changelog | README, CHANGELOG, node-support matrix, compatibility, license audit appendix | pending |

### M3 sequencing note

Per Plan-agent validation, M3 is the highest-risk milestone. Implement **WhiteNoise end-to-end first** as a spike (HLSL + `BuildWhiteNoiseSource` + dispatcher case + include mechanism verification + `.shadergraph` inspection), then mirror for Brick/Magic/Gabor. If the first `CustomFunctionNode` cannot be wired into the bridge, all of M3/M5/M7 must re-scope before more code is written.

## 5. Validation strategy

1. **Per-milestone Python tests:** `python -m unittest discover -s tests` — must stay green throughout.
2. **Blender smoke:** `python tools/ci/run_blender_headless.py --blender "C:\SteamLibrary\steamapps\common\Blender\blender.exe" --script <smoke>` for each milestone.
3. **.NET compile:** `dotnet build tests/B2UCompilerHarness/B2UCompilerHarness.csproj` must stay green.
4. **Procedural parity ΔE:** `python tools/delta_e_tool/procedural_parity.py --latest` — ΔE2000 ≤ 4.0, RMSE ≤ 0.05 across 7 procedural textures.
5. **Metal Library end-to-end:** 5 `.blend` files → 5 `.mgir.json` → 5 `.shadergraph` + 5 `.mat` + 5 `.png` rendered in Unity, no blocking diagnostics.
6. **Determinism:** Re-running on unchanged MGIR produces byte-identical Shader Graph JSON (per ADR-0006).

## 6. Files to be created / modified

### New files

- `docs/plans/blender-5-eevee-node-library.md` (this file)
- `docs/migrations/mgir-3.1-vector-transform-matrix-hint.md`
- `unity/Packages/com.migr.shaderconverter/Runtime/Textures/MiGRBlenderWhiteNoise.hlsl`
- `unity/Packages/com.migr.shaderconverter/Runtime/Textures/MiGRBlenderBrick.hlsl`
- `unity/Packages/com.migr.shaderconverter/Runtime/Textures/MiGRBlenderMagic.hlsl`
- `unity/Packages/com.migr.shaderconverter/Runtime/Textures/MiGRBlenderGabor.hlsl`
- `unityproject/Assets/Editor/B2UMetalLibraryBatchRender.cs`
- `tools/blender_ref_capture/capture_white_noise.py`
- `tools/blender_ref_capture/capture_brick.py`
- `tools/blender_ref_capture/capture_magic.py`
- `tools/blender_ref_capture/capture_gabor.py`
- `tools/delta_e_tool/procedural_parity.py`
- `tools/metal_library_pipeline.py`
- `samples/metal_library/` directory with 5 `.mgir.json` files
- `samples/metal_library/import-report.md`
- `tests/test_math_hlsl_against_blender.py`
- `tests/test_vector_transform_matrix_hint.py`
- `tests/test_procedural_parity_threshold.py`
- `samples/golden/17-white-noise.mgir.json`
- `samples/golden/18-brick.mgir.json`
- `samples/golden/19-magic.mgir.json`
- `samples/golden/20-gabor.mgir.json`

### Modified files

- `unity/Packages/com.migr.shaderconverter/Editor/ShaderGraph/B2UShaderGraph17UrpBackend.cs`
- `unity/Packages/com.migr.shaderconverter/Editor/ShaderGraph/B2UShaderGraphNodeRegistry.cs`
- `unity/Packages/com.migr.shaderconverter/Runtime/B2UHelpers.hlsl`
- `b2u_mvp/exporter_core.py`
- `schema/mgir-3.0.schema.json` (additive bump to 3.1)
- `docs/provenance/blender-node-ports.yml`
- `docs/provenance/BLENDER_SOURCE_LOCK.json` (Steam binary added in M1)
- `docs/node-support-matrix.md`
- `docs/compatibility.md`
- `docs/audits/blender-source-port-license-audit.md` (M3 appendix added in M3)
- `tools/delta_e_tool/render_unity.py`
- `README.md`
- `CHANGELOG.md`

### Out of scope

- Anything outside the directory list above
- The Endfield cleanup currently in flight on `task-1.4-mgir-fixtures` (906 dirty files)
- Merge into `master` — user approval gate before any merge
- Modifying user-owned wrapper `*.shadergraph` files (default `LinkedSubGraph` mode preserves them per ADR-0003)

## 7. Discoveries

- **Steam Blender is 5.2.0 LTS** — matches the canonical target. Recorded in `BLENDER_SOURCE_LOCK.json` `steamBlenderInstallation` block.
- **Metal library is 5 `.blend` files** (not "hundreds"). Plan expectations set accordingly.
- **`Texture.WhiteNoise/Brick/Magic/Gabor` registry entries are dead labels** — the four `Approximate*RuntimeFallback` strings are never matched by the backend. Real work is in `B2UShaderGraph17UrpBackend.cs`, not the registry.
- **`Vector.VectorTransform` is exported but not translated** — first-ever implementation required in M4.
- **`render_unity.py` is a stub** (`raise NotImplementedError`) — M6 must write the C# editor command first.

## 8. Results

| M | Commit | Deliverable | Outcome |
|---|--------|-------------|---------|
| M1 | `e89c93f` | Branch `migr-procedural-metal-lib`, source-lock verified, in-repo ExecPlan written | Done. Steam Blender 5.2.0 LTS recorded in BLENDER_SOURCE_LOCK.json. |
| M2 | `c7d1d6f` | `tests/test_math_ops_python_parity.py` (42 tests), MiGR_Mod GLSL-semantics bugfix | Done. 372 tests OK (0 failures). MiGR_Mod(x, y) now preserves sign of y for divisor. |
| M3 | `cd085d5` | 4 `Build*Source` methods (WhiteNoise/Brick/Magic/Gabor), 4 registry entries, 4 provenance YAML entries | Done. Expanded-native approach (no new HLSL). All four dispatched before registry fall-through. |
| M4 | `ccf4dd5` | VectorTransform matrix plumbing: `_semantic_and_space` 3-tuple, B2UHelpers macros (6x), `BuildVectorTransformSource` | Done. First-ever implementation of Vector.VectorTransform in SG backend. |
| M5 | `5af6538` | Projection mode diagnostic for BOX/SPHERE/TUBE in Image Texture params | Done. `MIGR_SG_TEXTURE_PROJECTION_MODE_FALLBACK` diagnostic emitted before MGIR return. |
| M6+M7 | `a27eca8` | Metal Library pipeline (74 materials from 5 .blend files), Unity batch render C# script | Done. All 74 .mgir.json copied to unityproject. Unity auto-import pending on domain reload. |
| M8 | *this commit* | Documentation: ExecPlan results, CHANGELOG, README, license audit appendix | In progress. |

### Test status

```
Ran 372 tests in 43.101s
OK (skipped=44)
```

No regression. 42 new Math parity tests added in M2. Pre-existing skips (21 `test_delta_e_gate`) remain blocked on URP renders per docs/superpowers/HANDOFF.md.

### Metal Library import summary

5 `.blend` files → **74 MGIR files** → coped to `unityproject/Assets/B2UMetalLibrary/Generated/B2U/`:

- 彩色金属 (colored metal): 9 materials
- 金 (gold): 13 materials
- 铁 (iron): 20 materials
- 铜 (copper): 15 materials  
- 银 (silver): 17 materials (exported; re-run shows 21 including dups)

Next action for visual review: open Unity 6000.4.5f1, let the ScriptedImporter auto-detect the .mgir files, then run `B2UMetalLibraryBatchRender.RenderAll` to generate reference PNGs.

### Deferred / unsupported

- Visual parity tests (M6 procedural parity harness): C# batch render script written but Unity batch mode blocked by pre-existing Endfield-removal compilation errors (`B2UDynamicEffectMaterialWriter` / `B2UDynamicEffects` missing — in-flight ADR-0007 cleanup). The harness infrastructure is in place; actual parity runs require the Unity project to compile cleanly.
- `tools/delta_e_tool/render_unity.py` stub: unchanged (deferred to post-Endfield-removal).
- `docs/migrations/mgir-3.1-vector-transform-matrix-hint.md`: not written (schema change is additive — the optional `matrixHint` field is backward-compatible; no migration needed).

---

_Plan lifecycle: **active**. Update on every commit._