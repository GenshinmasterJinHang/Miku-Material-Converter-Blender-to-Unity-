# Contributing to B2U/Miku

Thank you for helping build a reliable Blender-to-Unity material pipeline.
Before changing code, read `AGENTS.md`, the nearest nested instruction file, the
relevant architecture documents, and the current Git diff.

## Development setup

Use Python 3.11 and .NET 8. Install pinned development dependencies:

```bash
python -m pip install -r requirements-dev.txt
python tools/ci/run_checks.py --profile pr
```

Unity tests cover the 6000.0-6000.5 / 17.0-17.5 Windows matrix and retain the
6000.4.5f1 / 17.4.0 regression channel. Blender tests cover 5.0.1, 5.1.2, and
5.2.0 and must record the exact binary/version used. See
[`docs/development/local-development.md`](docs/development/local-development.md).

## Change process

1. Search for an existing implementation and tests before adding an abstraction.
2. Use an ExecPlan for cross-module, schema, compatibility, security, or release
   work as required by `PLANS.md`.
3. Add behavior tests at the lowest practical layer.
4. Update compatibility, schema, diagnostic, and changelog documentation when
   applicable.
5. Run the PR profile and any editor-specific checks the change affects.
6. Self-review the scoped diff; do not mix unrelated formatting or generated
   asset changes.

Do not automatically replace golden data after a failure. Explain why the new
fixture is correct and what changed semantically.

## Pull requests

Pull requests must describe the problem, solution, alternatives, compatibility,
schema/API/security impact, tests actually run, documentation, and generated
asset evidence where relevant. Keep B2U package IDs, public namespaces, Blender
operators/settings, property references, and schema paths stable unless a
versioned migration has been approved.

Use focused imperative commit subjects, for example:
`Validate Miku coordinate spaces during export`. No formal commit-prefix scheme
is imposed until maintainers adopt one.

## Licensing and provenance

By submitting original work, you agree that it may be distributed under the
repository MIT license. Do not submit models, textures, archives, copied code,
templates, or fixtures without verified redistribution rights and recorded
provenance. Third-party notices must be updated with any accepted dependency.

## Conduct and security

Participation is governed by [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md). Do not
put sensitive vulnerability details in a public issue; follow [`SECURITY.md`](SECURITY.md).
