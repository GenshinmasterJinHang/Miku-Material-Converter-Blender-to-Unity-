# Remove distributable validation tools and deduplicate the renderer installer

## Purpose and outcome

Remove local/manual validation utilities from the public Unity package while
retaining production validation and automated EditMode/D3D12 acceptance. Expose
one accurately named Game Toon renderer-feature installer instead of two menu
items opening the same window.

## Context and constraints

- The canonical Miku roots and `com.miku.shaderconverter` package marker passed
  preflight on 2026-08-13. Installed packages, validation projects, `dist/`, and
  retired B2U paths are not implementation roots.
- The worktree already contains extensive user-owned 2.4.0/3.0.0 work. Four of
  the validation files are untracked, while the Endfield diagnostic and its
  tests are tracked. This task uses exact-path edits only; no reset, checkout,
  clean, or broad deletion is permitted.
- The package remains 3.0.0. Remote inspection found no `v2.4.0` or `v3.0.0`
  tag or release, so final local 3.0.0 artifacts may be rebuilt and rehashed.
- The Windows validation tuple is Unity 6000.4.5f1, URP 17.4.0, Shader Graph
  17.4.0, Direct3D 12. GPU evidence must use `-force-d3d12` without
  `-nographics`.

## Progress

- [x] 2026-08-13: Located all validation-only types, menu registrations,
  callers, tests, public documentation, and package-identity entries.
- [x] 2026-08-13: Confirmed the duplicate Screen Rim and Game Toon menu items
  both call the same `OpenWindow()` and the window now installs Geometry plus
  Screen Rim features.
- [x] 2026-08-13: Deleted the five validation-only source groups and adapted
  the retained automated tests.
- [x] 2026-08-13: Kept one combined renderer installer entry and updated
  localization plus menu/WuWa regression tests.
- [x] 2026-08-13: Reconciled active documentation, changelogs, release notes,
  historical supersession notes, and package identity.
- [x] 2026-08-13: Ran Python, Unity EditMode, D3D12, deterministic-build, and
  diff/static checks against canonical source and the final TGZ.
- [x] 2026-08-13: Completed the final targeted diff/archive review and handoff
  record without resetting, cleaning, staging, or modifying installed copies.

## Discoveries

- `MikuFurinaLocalValidation` is the only source of the two
  `Miku/Game Toon/Validation` menu entries. `MikuWuwaTutorialValidationBuilder`
  has no menu but registers an `InitializeOnLoadMethod` and consumes private
  requests from `Library`, so it is also distributable validation tooling.
- `MikuDx12Validation` is needed only by the Furina helper and the retained GPU
  tests; the tests can check `SystemInfo.graphicsDeviceType` directly.
- `MikuGenshinUv1Diagnostics` is needed only by the Furina helper and one
  helper-specific unit test. The shader's geometric fallback is independent.
- The checked-in package identity already reports drift before this task. It
  must be generated to a temporary file and reviewed before replacement.
- The first Python gate exposed an unrelated pre-existing profile-hash drift:
  `default_target_profile()` produced `cc442352...` while the Unity importer
  still named `443efd49...` as current. The current hash is now authoritative,
  and both earlier 3.0 hashes remain accepted for Bundle compatibility.

## Decision log

- Delete all five validation-only classes rather than merely hiding their menu
  items. Keep importer/schema/path/material validation and Texture Import Audit.
- Delete the dedicated Endfield diagnostic test and the single Genshin helper
  test; retain and decouple all four D3D12 GPU tests.
- Keep `Miku/Game Toon/Rendering/Game Toon Renderer Feature Installer` as the
  canonical menu and remove the released `Screen Rim Installer` alias. This is
  an intentional 3.0 Editor-menu compatibility change; runtime feature APIs and
  serialized assets are unchanged.
- Historical plans, audits, and executed evidence remain historical records.
  Active public documentation must not claim that the removed tools ship.
- Updating the canonical target-profile hash must not invalidate earlier 3.0
  bundles; the old values stay in `SupportedProfileHashes` and have EditMode
  compatibility coverage.

## Implementation sequence

1. Delete the five Editor source/meta pairs and the dedicated Endfield
   diagnostic test/meta; remove the one Genshin helper test.
2. Make D3D12 GPU tests self-contained, then remove all remaining compiled
   references to the deleted types.
3. Remove the old Screen Rim menu entry, rename the inspector shortcut/window,
   update Chinese localization, and tighten menu/localization tests.
4. Update active manuals, guides, diagnostics, provenance, changelogs,
   compatibility/release records, and package identity without rewriting
   historical validation evidence.
5. Build and test only from canonical source, using isolated temporary Unity
   projects and deterministic release output directories.

## Validation

Executed commands and results:

- `python tools/miku_package_identity.py --check`: passed. The temporary
  candidate audit removed exactly six assets, added none, and changed no GUID.
- `python -m unittest tests.test_public_docs tests.test_miku_package_identity`:
  21 passed.
- `python tools/ci/run_checks.py`: 270 passed; identity and both package builds
  also passed.
- `tools/ci/run_unity_editmode.ps1`: final TGZ on Unity 6000.4.5f1 / URP and
  Shader Graph 17.4.0 passed 316/322, with zero failures and six documented
  skips under `-nographics`.
- `tools/ci/run_unity_dx12_gpu.ps1`: the same TGZ in an isolated ASCII-path
  project passed all four required tests with zero skips under `-force-d3d12`
  and without `-nographics`.
- Two independent `tools/release/build_release.py` outputs were byte-identical
  for the TGZ, ZIP, and `SHA256SUMS.txt`.
- Static menu/type scans and `git diff --check`: passed.

Blender tests were not repeated because this cleanup changed no Blender source.
The rebuilt ZIP was byte-identical across the two current-source release builds;
older local candidate hashes were inconsistent and were not used as evidence.

## Results and follow-up

The final TGZ is 497,774 bytes with SHA-256
`ea5549fa6f8d21c8abfce629deded9feca6a265a497453a2770ecab29e0ed417`.
The final ZIP is 199,614 bytes with SHA-256
`b5826ea19a75399a7f6ecbac8d7cde5570939a54aeade54caf4b68006223f1c2`.

Known limitations are explicit: two full-suite tests still require external
regression/visual inputs, the four GPU tests intentionally skip in the
`-nographics` full run and are proven separately, Blender tests were not rerun,
and the earlier private live-scene measurement was not repeated for this final
cleanup TGZ. No schema, MaterialIR, shader-property, or runtime Screen Rim
contract changed.

Final self-review confirmed that the six deleted source/test assets are absent
from both the canonical tree and TGZ, retired Editor menu registrations occur
zero times, the combined installer registration occurs exactly once, active
3.0 release records use the new hashes/counts, and unrelated dirty-worktree
changes were left in place.

### 2026-08-13 post-plan release correction

The counts and hashes above remain the historical cleanup-candidate record.
The later main-light/Face-SDF repair changed package bytes and added three
graphics tests. The current final TGZ is
`760dc9b365f7a1329483e63ca34ff23f88e5f0a3da7827ab774d7df6146bcb75`;
its isolated full EditMode run discovered 333 tests (324 passed, zero failed,
nine skipped), and its D3D12 lane passed all seven required tests with zero
skips. The removal and installer-deduplication conclusions of this plan are
unchanged.
