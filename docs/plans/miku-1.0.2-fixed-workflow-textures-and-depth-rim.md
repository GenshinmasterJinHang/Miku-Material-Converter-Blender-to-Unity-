# Miku 1.0.2 fixed-workflow textures and depth rim

## Purpose and outcome

Ship coordinated Blender and Unity 1.0.2 packages in which Generic Toon and
the Genshin, WuWa, and HSR workflows export without translating the complete
Blender closure graph. Static Blender images are sealed deterministically and
bound through explicit target-neutral roles. Unity users select the Generic
semantic or game part without reimport overwriting that choice. Genshin, WuWa,
and HSR Body/Hair/Face use the shared RenderGraph screen-space depth rim rather
than inline Fresnel lighting, and the RenderGraph pass no longer mutates global
state from a raster callback.

## Context and constraints

- Canonical sources are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`.
- The working tree already contains unrelated edits in the planner, Unity
  importer/backend/tests, and changelogs. Preserve and review them rather than
  reverting or replacing whole files.
- Blender validation is fixed to Blender 5.2.0 at
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe`.
- Unity validation is fixed to 6000.4.5f1 with URP/Shader Graph 17.4.0.
- Blender and Unity GUIs are currently open. Do not overwrite installed
  extensions or the validation project's package until users have saved and
  closed them.
- Standard PBR remains strict. Fixed workflows must not schedule bake work.
- Existing public Shader property names and schema versions remain stable.

## Progress

- [x] 2026-08-01: Confirmed canonical source markers, package identity, exact
  validation versions, dirty-worktree overlap, and running editor processes.
- [x] Implement fixed-workflow IR/planning and deterministic image-role export.
- [x] Implement Unity role binding, recipes, selection UI, and migration.
- [x] Implement shared two-target mask and depth composite for game Body/Hair/Face.
- [x] Add/update tests and documentation.
- [x] Run Python, Blender 5.2, Unity 6000.4.5f1, release, and deterministic-build validation.

## Discoveries

- Generic Toon currently reaches the full closure compiler, so unsupported
  closure-weight expressions fail before bundle sealing.
- Static image sealing currently only collects `Texture.SampleImage2D`
  expressions referenced by semantic channels; fixed workflows need an
  independent image collector.
- Static game binding currently guesses a short list of PBR semantics and can
  map Roughness/AO incorrectly to `_LightMap`.
- Existing imported Generic recipes reset semantic to GenericOpaque and can
  throw when Restore Source Values runs with no Unity source material.
- Genshin and WuWa currently sample scene depth inside their opaque forward
  passes and multiply the result by Fresnel. HSR uses Fresnel only.
- The shared composite currently derives an edge from mask morphology rather
  than scene depth, and calls `SetGlobalTexture` illegally inside its raster
  callback even though the producer already uses `SetGlobalTextureAfterPass`.
- Bundle validation has both JSON Schema and imperative safety layers; both had
  to accept `FixedWorkflowTexture` and validate optional `materialBindings`.
- Fixed workflows must bypass final required-channel materialization as well as
  closure planning; otherwise a valid texture-only export still fails on the
  synthetic BaseColor channel.

## Decision log

- Four fixed workflows bypass source closure/value compilation; the original
  graph remains authoritative provenance in Source Map.
- Texture roles are target-neutral names. Explicit node role wins over a
  `MIKU:<Role>` label, strict aliases, and filename suffix aliases. Equal-rank
  ambiguity leaves the role unbound rather than guessing.
- Unknown or unexportable fixed-workflow images warn and continue. Security,
  identity, size, and path failures remain fatal.
- Unity selection is recipe-owned user intent. Blender supplies only the
  initial Generic semantic/game part.
- The shared screen rim applies to Genshin/WuWa/HSR Body, Hair, and Face only.
  Eye and transparent WuWa Effect remain outside the rim path.
- Preserve `_FresnelPower` and `_FresnelClamp` as hidden compatibility
  properties, but do not consume them after 1.0.2.
- Preserve the renderer-feature class, namespace, and script GUID. Publish both
  mask textures with `SetGlobalTextureAfterPass`; never grant broad global-state
  permission to the composite callback.

## Implementation sequence

1. Add fixed-workflow contracts, minimal valid IR construction, Native-only
   planning, deterministic role inference, image collection, diagnostics, and
   Blender role UI.
2. Extend bundle resource records with optional material bindings and update
   Unity import/resource identity without affecting Standard PBR resources.
3. Extend recipes and both Shader GUIs with persistent semantic/part selection,
   migration, rebind, and source-value restoration.
4. Add the shared mask ABI, game mask passes, two RenderGraph attachments, and
   depth-based composite; remove inline game rim consumers and fix global state.
5. Bump coordinated versions to 1.0.2 and update English canonical docs,
   translated user docs, diagnostics, compatibility, changelogs, and release notes.
6. Run targeted tests, full checks, deterministic builds, safe install, exact
   Blender/Unity validation, final diff review, and record results here.

## Validation

- `py -3.13 -m unittest discover -s tests -v`
- `py -3.13 tools/ci/run_checks.py --profile pr`
- `py -3.13 tools/ci/run_checks.py --profile release`
- Blender headless smoke tests must call the fixed Blender 5.2 executable and
  assert `bpy.app.version == (5, 2, 0)`.
- Unity EditMode and render tests must run against the deterministic canonical
  1.0.2 TGZ under Unity 6000.4.5f1/URP 17.4.0.
- Build Blender ZIP and Unity TGZ twice and compare manifests and SHA-256 hashes.

## Results and follow-up

Implementation and isolated validation are complete. Blender 5.2 fixed-workflow
smoke coverage passes for all four workflows, including FullPBRBake mode
producing zero bake jobs. Python validation passes as 198 project-environment
tests plus 19 NumPy-dependent tests under the bundled Python runtime. Ruff and
the Unity package identity gate pass.

Unity 6000.4.5f1/URP 17.4.0/D3D11 passes 126 of 126 EditMode tests both from
canonical source and the final 1.0.2 TGZ. The suite includes real-camera pixel comparisons for Generic,
Genshin Body, WuWa Body, and HSR Body, proving that depth-edge pixels change,
flat centers do not receive Fresnel glow, pixels outside the visible character
remain untouched, and the illegal global-state exception is absent. A clean
reimport contains no C# compilation, Shader compilation, RenderGraph, or
global-state error. On each fresh project,
Unity's built-in Shader Graph package
briefly reported its own `UnityEngine.GUID` resolution error during the first
package refresh, then rebuilt successfully before the test run; stable reimport
did not reproduce it.

Two consecutive canonical builds are byte-stable and have identical manifests:

- `miku_shader_converter-1.0.2.zip`: 31 entries,
  SHA-256 `e2ddf678a2bbf2b5024232ecf1af379896c360fba67d30d89a7b0ae2adf8e5c8`.
- `com.miku.shaderconverter-1.0.2.tgz`: 183 entries,
  SHA-256 `bea2c494dfaca2053241a93d804d151732a7a2799f56375f0da40df50a518224`.

The monolithic PR/release Python command cannot run under a single local
interpreter because the Python 3.13 project environment lacks NumPy while the
bundled Python 3.12 environment lacks the compiled `jsonschema` dependency.
Its constituent gates were therefore executed with the compatible interpreter
for each dependency and all passed. Installation into the user's currently
open Blender/Unity projects remains intentionally deferred until both GUI
applications are saved and closed; no installed copy or project Renderer Data
was modified.
