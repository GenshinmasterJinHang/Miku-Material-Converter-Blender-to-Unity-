# Release process

Components use independent SemVer. A release identifies Blender add-on, Unity
package, and schema versions plus the exact compatibility tuple.

1. Configure real maintainers/CODEOWNERS and a verified private security contact
   in `.github/project-maintainers.yml` and public policy docs.
2. Complete the third-party rights review and update notices.
3. Update `CHANGELOG.md`, compatibility evidence, and component versions.
4. Run `python tools/ci/run_checks.py --profile pr`.
5. Run required Unity/Blender exact-version suites and retain results.
6. Run `python tools/ci/run_checks.py --profile release`.
7. Build twice into separate empty directories with
   `tools/release/build_release.py`; compare the emitted SHA-256 manifest.
8. Inspect archive file lists and confirm models, textures, project outputs,
   caches, logs, and restricted content are absent.
9. Create a signed/tagged candidate and have an authorized maintainer manually
   publish it. GitHub Actions only uploads candidates; it does not publish.

The builder reads versions dynamically from `bl_info` and `package.json`. Do not
rename an archive to simulate another version.
