# Miku 2.2.9 GitHub release delivery

Status: in progress, executing on 2026-08-04 from `dev` at `97f644e`. This plan
publishes the 2.2.9 version-range compatibility work already merged on `dev` as
a tagged GitHub Release with the two reproducible package artifacts and a
SHA-256 manifest.

## Baseline

The 2.2.9 implementation plan is
[`docs/plans/miku-2.2.9-version-range-compatibility.md`](miku-2.2.9-version-range-compatibility.md).
Its existing decisions remain in force: MaterialIR 2.0, Bundle 1.0, Bake
request 1.0/1.1, and public shader property names are not changed. The release
delivery follows the 2.2.8 pattern recorded in
[`docs/plans/miku-2.2.8-github-final-docs.md`](miku-2.2.8-github-final-docs.md).

## Decisions

- One commit on a release branch contains the 24 modified files (CHANGELOG,
  bilingual README/Manual, compatibility docs, schema docs, Blender extension
  sources, `miku` core, tests, and `pyproject.toml`).
- The release branch is `codex/miku-2.2.9-release` based on the current
  `dev` HEAD; it merges into `main` via PR and the resulting merge commit
  receives the `v2.2.9` annotated tag.
- Release title is `Miku 2.2.9`; release notes are the `## 2.2.9 - 2026-08-03`
  section copied verbatim from `CHANGELOG.md`.
- Release assets are the three prebuilt files in
  `artifacts/miku-2.2.9-final-a/`:
  `miku_shader_converter-2.2.9.zip`,
  `com.miku.shaderconverter-2.2.9.tgz`,
  and `SHA256SUMS.txt`. The prebuilt `artifacts/miku-2.2.9-final-b/` set is
  retained as the cross-check source.
- Tag signing is intentionally not used; the tag is annotated with the
  release title and date so its identity is inspectable on GitHub.
- The `dev` and `main` branches stay in sync after the merge. No force-push,
  no rewrite of existing history, no alteration of the existing `v2.2.8`
  tag or release.

## Implemented scope

- Bilingual README, Manual, compatibility, and schema documentation updates
  recorded in `CHANGELOG.md`.
- Blender 5.0.0 through 5.2.0 range classification, in-extension runtime
  registration, and Time-node creation diagnostics.
- `miku-bake-request-1.2` schema/validation binding requests to the executing
  Blender numeric version and build hash.
- Unity 6000.0.0f1 through 6000.4.5f1 and URP/Shader Graph 17.0.0 through
  17.4.0 range parsers, explicit 17.0-17.4 adapter selection, and
  in-memory capability preflight before any import transaction.
- New and updated focused tests for the range checks, bake request 1.2, and
  the preflight.

## Progress

- [x] 2026-08-04: Confirmed canonical roots, package identity at 2.2.9, clean
  `dev` HEAD at `97f644e`, existing `v2.2.8` tag/release, and the
  prebuilt 2.2.9 artifacts in `artifacts/miku-2.2.9-final-a/`.
- [ ] 2026-08-04: Create `codex/miku-2.2.9-release` branch from `dev`,
  commit the 24 modified files, and push the branch.
- [ ] 2026-08-04: Open PR from `codex/miku-2.2.9-release` to `main` and
  merge it after the required GitHub `core` check passes.
- [ ] 2026-08-04: Tag the merge commit as `v2.2.9` and push the tag.
- [ ] 2026-08-04: Create the GitHub Release `Miku 2.2.9` with the three
  artifacts and the CHANGELOG-derived notes, then verify the published
  assets and tag.
- [ ] 2026-08-04: Update this plan with the final delivery record and
  follow-up.

## Discoveries

- `origin/main` and `origin/dev` are at the same HEAD (`97f644e`); the
  unmerged 2.2.9 work is local to `dev` and the merge target is effectively a
  fast-forward.
- `artifacts/miku-2.2.9-final-a/` already contains the same three files the
  release builder would produce, with matching `SHA256SUMS.txt`. A rebuild
  is not required for the publish step but the final-a hashes are recorded
  for evidence.
- The 2.2.8 release pattern used a single merge commit followed by a tag and
  asset upload; the same pattern is reused here to keep the public record
  consistent.

## Decision log

- 2026-08-04: Use a single release commit rather than the per-topic split,
  because all 24 files belong to the same delivery and the 2.2.8 plan already
  shipped a single record-keeping commit for its post-release documentation
  amendment.
- 2026-08-04: Publish the GitHub Release with the artifacts from
  `artifacts/miku-2.2.9-final-a/` because they were produced by the canonical
  `tools/release/build_release.py` and verified with
  `artifacts/miku-2.2.9-final-b/`.
- 2026-08-04: Skip tag signing because the repository has no GPG signing
  configuration and the unsigned annotated tag is the existing 2.2.8
  convention.

## Implementation sequence

1. `git checkout -b codex/miku-2.2.9-release` from `dev`.
2. `git add` the 24 modified files; `git commit` with the release message
   recording the 2.2.9 scope and the canonical source boundary.
3. `git push -u origin codex/miku-2.2.9-release`.
4. `gh pr create --base main --head codex/miku-2.2.9-release --title ... --body ...`.
5. Wait for the required `core` check, then `gh pr merge --merge`.
6. `git checkout main && git pull --ff-only`, then
   `git tag -a v2.2.9 -m "Miku 2.2.9 - 2026-08-04" && git push origin v2.2.9`.
7. `gh release create v2.2.9 artifacts/miku-2.2.9-final-a/* --title "Miku 2.2.9" --notes <CHANGELOG 2.2.9 section>`.
8. Verify tag, release, and asset digests with `gh release view v2.2.9`.

## Validation

- Confirm `git log` on `main` shows the merge commit referencing
  `codex/miku-2.2.9-release`.
- Confirm `git ls-remote origin v2.2.9` returns the expected SHA.
- Confirm `gh release view v2.2.9` lists the three assets and matches the
  CHANGELOG-derived notes.
- Compare each uploaded asset digest with `artifacts/miku-2.2.9-final-a/SHA256SUMS.txt`.

## Results and follow-up

(filled in after merge and release)
