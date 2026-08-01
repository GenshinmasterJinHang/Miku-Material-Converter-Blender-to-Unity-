# Release rollback

Releases are manual and immutable. If a candidate is wrong, do not replace an
archive under the same version.

1. Stop publication or mark the affected release as withdrawn/deprecated.
2. Preserve artifacts, SHA-256 manifests, test results, and the failure report
   for audit.
3. Identify compatibility, schema, security, licensing, or data-integrity impact.
4. Revert through a reviewed change; do not rewrite shared history.
5. Add regression tests and changelog/release-note guidance.
6. Increment the affected component version and build a new deterministic
   candidate.

Generated user-owned `.shader`/wrapper assets are not rollback targets. Restore
them from user version control/backups; ordinary regeneration must not overwrite
them.
