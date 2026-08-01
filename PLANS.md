# Miku ExecPlans

An ExecPlan is a living implementation record for work that crosses subsystem,
schema, compatibility, security, or public API boundaries. It must let a new
contributor continue the work using only the repository and the plan.

## When an ExecPlan is required

Create or update a plan under `docs/plans/` before implementing any of the
following:

- work spanning more than one of Core, Blender, Unity, schemas, tooling, or docs;
- a compatibility, schema, generated-asset, or public API change;
- a migration, security-sensitive change, or release-process change;
- work whose validation requires more than one runtime or editor.

Small, isolated fixes may document their reasoning in the pull request instead.

## Required structure

Every ExecPlan is self-contained and contains:

1. **Purpose and outcome** — observable behavior and user value.
2. **Context and constraints** — relevant files, ownership rules, compatibility,
   dirty-worktree constraints, and terms a new contributor may not know.
3. **Progress** — timestamped checkboxes reflecting reality.
4. **Discoveries** — facts learned during implementation, with evidence.
5. **Decision log** — decisions, rejected alternatives, and consequences.
6. **Implementation sequence** — concrete edits in dependency order.
7. **Validation** — exact commands, expected results, and environment needs.
8. **Results and follow-up** — completed scope, known limitations, and genuine
   out-of-scope work.

## Maintenance rules

- Update the plan whenever implementation diverges from an earlier assumption.
- Record commands after they actually run; never report an unexecuted test as
  passing.
- Distinguish `passed`, `failed`, `implemented but not executed`, and `blocked`.
- Keep decisions append-only. Correct an earlier statement with a dated note
  instead of erasing the history.
- Use repository-relative paths and describe public/API/schema effects explicitly.
- Do not embed secrets, tokens, private contact details, or unnecessary absolute
  machine paths.
- Finish with enough detail for release review and rollback.

The active foundation plan is
[`docs/plans/open-source-foundation.md`](docs/plans/open-source-foundation.md).
