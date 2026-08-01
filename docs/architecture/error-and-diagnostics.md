# Errors and diagnostics

Diagnostics are data, not log strings. Each diagnostic should include a stable
code, severity, summary, source material/node/socket context where available,
translation quality, and a concrete remediation. Absolute local paths and secret
values are omitted unless essential and explicitly requested.

## Failure model

- **Error**: required semantics are invalid/unsupported, schema is unknown,
  output is unsafe, or generated data cannot be trusted. Stop generation for the
  affected material.
- **Warning**: output is safe but approximate, requires setup, or an unreachable
  unsupported node was safely pruned.
- **Info**: non-actionable provenance or conversion detail.

The compiler/importer may continue with other independent materials after an
error, but cannot substitute black/white/zero/pass-through for an unsupported
required operation. File-write failures preserve the previous owned asset.

Stable codes and their present coverage are listed in `docs/diagnostics.md`.
