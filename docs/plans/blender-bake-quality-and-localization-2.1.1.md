# Blender Bake Quality and Localization 2.1.1

## Purpose and outcome

Release one Blender 5.2 extension that follows Blender's `en_US` or
`zh_HANS` interface language and exposes four deterministic 2D bake
resolutions under Advanced. The selected resolution must agree across the
conversion plan, bake request, cache identity, and emitted image metadata.

## Context and constraints

- Canonical source roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and the unchanged Unity package.
- The worktree already contains substantial unrelated maintainer changes,
  including edits in several files touched by this feature. Preserve them and
  review only task-specific hunks.
- Blender validation must use
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe` and assert 5.2.0.
- Do not replace an installed extension while a Blender GUI is running.
- Bake request 1.0 remains frozen; request 1.1 adds selectable resolution.
  Result 1.0 and Unity-facing interchange contracts remain unchanged.

## Progress

- [x] 2026-08-03: Confirmed canonical repository markers and Blender 5.2.0.
- [x] 2026-08-03: Confirmed no nested `AGENTS.md` and recorded dirty-worktree
  overlap.
- [x] 2026-08-03: Implemented protocol 1.1 and backward-compatible worker
  consumption.
- [x] 2026-08-03: Threaded bake resolution through public Blender export APIs
  and deterministic plans.
- [x] 2026-08-03: Added English-source/Simplified-Chinese UI translations and
  repeat-safe registration.
- [x] 2026-08-03: Added unit, Blender, package, compatibility, and determinism
  coverage.
- [x] 2026-08-03: Built, installed, and verified the 2.1.1 extension archive.

## Discoveries

- The worker already includes resolution, samples, and margin in its cache key,
  but `make_bake_request` currently locks every request to 1024.
- Planner jobs also record 1024, so changing only worker settings would make
  the public conversion plan disagree with actual output.
- Blender 5.2.0 reports `zh_HANS` as the Simplified Chinese locale and exposes
  `bpy.app.translations.register(module_name, translations_dict)`.
- Specialized Texture3D and direction-resource dimensions use separate worker
  settings and are outside this 2D quality control.
- Bumping the global core `TOOL_VERSION` would change the target profile hash
  and require an unrelated Unity compatibility update. The core remains 2.1.0
  while the independently versioned Blender extension release is 2.1.1.

## Decision log

- 2026-08-03: Use one archive that follows Blender language; no plugin-specific
  language selector and no separate Chinese build.
- 2026-08-03: Use Low 512, Standard 1024, High 2048, and Ultra 4096; keep CPU,
  16 samples, 16 px margin, and random seed 0 fixed.
- 2026-08-03: Always show the quality control under Advanced and state that it
  applies only when a bake job is scheduled. Do not plan inside `Panel.draw()`.
- 2026-08-03: Translate visible Blender UI and operator-result templates only;
  exported diagnostics, codes, identifiers, and JSON remain English.
- 2026-08-03: Emit bake request 1.1 from new exporters, accept 1.0 and 1.1 in
  the new worker, and retain bake result 1.0.

## Implementation sequence

1. Add request 1.1 to the contract registry and JSON schemas. Validate the
   four supported resolutions before document construction.
2. Add additive `bake_resolution=1024` keywords to request, client, and Blender
   export APIs. Rewrite every scheduled 2D bake job and rebuild the plan hash.
3. Add a stable scene quality enum and English-source translation catalog,
   register/unregister it safely, and localize every visible panel/operator
   string in scope.
4. Update focused unit and Blender tests, then bump the Blender extension
   artifact version to 2.1.1 and update public documentation.
5. Run focused tests, full PR checks, Blender 5.2 headless checks, deterministic
   double builds, and installed-tree verification when safe.

## Validation

- `py -3.13 -m unittest tests.test_miku_bake_protocol tests.test_miku_blender_frontend tests.test_miku_workflows`
- `py -3.13 -m unittest discover -s tests -p "test_*.py"`
- `py -3.13 tools/ci/run_checks.py --profile pr`
- Fixed Blender 5.2 headless translation and 512 bake smoke scripts.
- Build `miku_shader_converter-2.1.1.zip` twice and compare bytes/SHA-256.

Expected results: all tests pass; both locale lookups resolve correctly;
request, plan, and baked image report the selected resolution; repeated builds
are byte-identical.

## Results and follow-up

Implemented all planned UI, protocol, export, documentation, and test changes.
The real Blender smoke generated six 512x512 images and confirmed matching
plan/request metadata. Source and installed extension localization both
resolved `Bake Texture Quality` to `烘焙贴图质量` under `zh_HANS`.

Validation results:

- Focused Python suite: 50 passed.
- Full Python suite invoked by PR checks: 236 passed.
- Ruff correctness checks and `git diff --check`: passed.
- Blender 5.2 localization smoke: passed.
- Blender 5.2 real 512 bake smoke: passed.
- Two canonical builds: byte-identical, 189118 bytes, SHA-256
  `de6e681934c13e82e79069757e8dc63b080b6b84fc28b398c45973fd390bdf0d`.
- Installed archive/file manifest comparison: passed at
  `portable/extensions/user_default/miku_shader_converter`; installed tree
  SHA-256 `8348e3b82244f461a174d0cc426803ffea12487105106ff8d64d52d4ab3358e7`.
- `tools/ci/run_checks.py --profile pr`: Python/schema/identity-boundary checks
  passed, then the run stopped at `MIKU_PACKAGE_IDENTITY_DRIFT`. The existing
  worktree already contains unrelated Unity package and provenance edits; this
  task did not regenerate or overwrite their identity manifest.

No Unity tests were required because Unity code, Unity schemas, Shader
properties, and bake result/resource wire shapes are unchanged.
