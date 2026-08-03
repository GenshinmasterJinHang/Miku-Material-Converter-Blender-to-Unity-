# Miku 2.2.8 GitHub final documentation and material creator

Status: released as `v2.2.8` on 2026-08-03. PR #1 delivered the implementation
and documentation, PR #2 recorded the final artifact hashes, and both merged
with the required CI check passing.

## Baseline

This plan continues the historical 2.2.8 bilingual/localization and Blender
time-input policy plan in
`docs/plans/miku-2.2.8-unity-bilingual-blender-time-policy.md`. Its existing
decisions remain in force: MaterialIR 2.0, Bundle 1.0, Conversion Plan, Bake
Result, and public shader property names are not changed.

## Decisions

- The Blender current-material UI is Standard PBR only. The exporter sets
  `standard_pbr` for that entry point but preserves explicit lower-level legacy
  workflow APIs and historical Bundle readers.
- Unity's public creator is configuration-driven: it enumerates visible public
  2D shader properties in declaration order, excludes `_MainTex` and hidden
  compatibility properties, validates required textures in memory, and only
  then writes a user-owned `.mat`.
- English is canonical public documentation; `docs/zh-CN/` mirrors the same
  sections, links, compatibility claims, and image set.
- Release artifacts are built into an independent output directory and hashed
  by `tools/release/build_release.py`; candidate hashes are never reused.
- Package identity hashes canonicalize text assets to LF, so the immutable
  manifest is stable on Windows checkouts and Linux CI.

## Implemented scope

- Blender frontend and focused regression/smoke coverage.
- Unity menu, part filtering, texture enumeration, required-field rules,
  Wuwa dual binding, localized labels, and EditMode test coverage.
- Root README, Chinese README, English/Chinese Manual, compatibility and
  release notes, Mermaid source/exports, and documentation link regression.
- Reproducible Blender smoke orchestration and release ZIP/TGZ/SHA256 builder.

## Validation record

- Executed: `py -3.13 -m unittest tests.test_miku_blender_frontend` (37 passed),
  then the full Python suite (248 passed) including public documentation
  regression checks.
- Executed: `py_compile` for the new CI/release/capture scripts.
- Executed: two independent final builds in separate output directories with
  byte-identical ZIP/TGZ outputs. Final hashes are recorded in
  `docs/release/miku-2.2.8-sha256.txt`:
  `c3fc830dc3c2c388940355462cd5da071607a73da7eb6cabd3e997dbe2b64a80` for
  the Blender ZIP and
  `0ef9b24e8f051779291b11484f2dd77c662f102426ed01fdcc7fab803730e15b` for
  the Unity TGZ.
- Executed: `tools/ci/run_checks.py --profile pr` and `--profile release`.
- Executed: GitHub Actions `core` checks for PR #1 and PR #2; both passed.
- Executed: fixed-path Blender 5.2.0 public smoke suite (8 self-contained
  scripts passed; private corpus scripts are intentionally excluded).
- Executed: extracted the downloaded ZIP/TGZ in temporary isolated locations
  and compared every packaged file and hash with the canonical source (32 ZIP
  files and 207 Unity package files matched).
- Attempted: Unity 6000.4.5f1 EditMode. The package's cached Shader Graph 17.4
  source currently fails before test discovery on the editor's missing public
  `UnityEngine.GUID` API; the project also reports the existing package compile
  issue in `BuiltInCanvasSubTarget.cs` and `TargetSetupContext.cs`. This remains
  an environment/package blocker, not a claimed passing result.
- The final documentation screenshot renderer and bilingual link regression
  were executed; no private paths or game assets are included in the checked-in
  images.

## Delivery record

- The remote was checked before publishing and had no existing `v2.2.8` tag or
  release. The unsigned annotated tag points to the latest `main` merge
  commit `d6df5f2c029f0441b7fb3be45a4a127741a54db5`.
- Release assets were uploaded as
  `miku_shader_converter-2.2.8.zip`,
  `com.miku.shaderconverter-2.2.8.tgz`, and `SHA256SUMS.txt`.
- All three assets were downloaded again from GitHub; the two package hashes
  matched the checked-in manifest and GitHub's asset digests.

## Post-release documentation amendment: real captures and Game Toon guide

Purpose and outcome: replace the synthetic UI illustrations with maintainer-
provided Blender and Unity captures, document the four bundled Game Toon
Shader/HLSL families and public Unity Editor tools, and add four maintainer-
provided character renders to the bilingual manuals with an explicit non-
commercial documentation-only notice. This amendment changes public GitHub
documentation only; the released 2.2.8 packages, tag, schemas, APIs, and asset
hashes remain immutable.

Context and constraints:

- English remains canonical and `docs/zh-CN/` mirrors its structure and claims.
- Miku-authored source remains MIT. Only the four named character renders are
  excluded from MIT and restricted to non-commercial learning and
  documentation reference.
- The two UI captures and four character renders are copied byte-for-byte. No
  crop, recompression, or blur is applied. Text provenance records source
  basenames only; the maintainer approved the captures' visible content.
- The character renders remain outside both installable packages and all
  existing Release assets.

Progress:

- [x] 2026-08-03: verified the clean canonical repository boundary, GitHub
  authentication, merged predecessor PR #4, and latest `origin/main`.
- [x] 2026-08-03: inspected the current MenuItem implementations, creator
  validation, texture-import mutation, Mesh clone ownership, renderer-feature
  installer, Volume profile rebuild, custom inspectors, and migration tools.
- [x] 2026-08-03: imported the six approved PNG files byte-for-byte and recorded
  their source basenames, display mappings, terms, and SHA-256 hashes.
- [x] 2026-08-03: rewrote the bilingual README/Manual content, documented all
  public tools, and retired the four synthetic localized captures and renderer.
- [x] 2026-08-03: extended public-documentation regression coverage; the focused
  8-test module, full 253-test Python suite, and PR profile passed.
- [ ] Publish, review, and merge the documentation PR without touching v2.2.8.

Discoveries:

- `Miku > Game Toon > Textures > Import Audit` applies recognized Endfield
  TextureImporter settings, reimports changed textures, and writes
  `Assets/Miku/Reports/endfield-texture-import-audit.json`; it is not a read-
  only scanner.
- The Smooth Normal Generator writes UV7 only on a cloned Mesh asset and leaves
  the source Mesh, importer, and Renderer references untouched.
- The Toon Material Recipe inspector exposes synchronization metadata; part
  application occurs through the matching Miku material Inspector workflow.

Decision log:

- Preserve the existing English image filenames for the two real UI captures
  so historical public links remain valid. Remove the two obsolete localized
  synthetic PNGs and their renderer script; both languages use the real UI
  captures.
- Put the four-character 2x2 gallery in both manuals. Keep README concise with
  a preset table and a link to the manual gallery.
- Record the four-image restriction in a dedicated provenance document and in
  `THIRD_PARTY_NOTICES.md`; do not alter the MIT license or package metadata.

Implementation sequence:

1. Import and hash the two UI captures and four character renders.
2. Update English/Chinese README and Manual with mirrored preset, tool, image,
   ownership, and licensing text.
3. Add provenance and third-party notice entries; remove synthetic captures.
4. Extend link, image, hash, menu, version, and license regression coverage.

Validation:

- `py -3.13 -m unittest tests.test_public_docs`
- `py -3.13 -m unittest discover -s tests -p "test_*.py"`
- `py -3.13 tools/ci/run_checks.py --profile pr`
- `git diff --check`
- Visual review of all six PNGs and GitHub Markdown layout.

Results and follow-up:

- The six repository PNG hashes match their maintainer-provided source files.
  Visual review confirmed the two UI captures and all four front-facing renders
  are readable and correctly mapped.
- Passed `py -3.13 -m unittest tests.test_public_docs` (8 tests), the full
  Python suite (253 tests), `tools/ci/run_checks.py --profile pr`, and
  `git diff --check`.
- Blender and Unity runtime suites were not rerun because no package, runtime,
  API, schema, Shader, or menu behavior changed. The PR profile still rebuilt
  both packages and verified their canonical identity.
- GitHub PR/CI/merge results remain to be appended before delivery.
