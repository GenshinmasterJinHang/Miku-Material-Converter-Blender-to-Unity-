# ADR 0007: Retire the Endfield preset and version current schemas

- Status: Accepted
- Date: 2026-07-22

## Context

The Endfield path crossed Blender-specific Goo detection, target-neutral MiGR
documents, Unity ShaderLab templates, copied shader sources with unresolved
redistribution terms, project assets, and character-specific verification data.
Keeping that path active made the current public schema and release package
claim a capability that could not meet MiGR's maintenance, compatibility, and
license requirements.

## Decision

Remove the dedicated implementation and assets. Publish `mgir-2.0`,
`mgir-preset-2.0`, overlay schema version 3, Blender add-on 0.6.0, and Unity
package 0.9.0. Current preset identifiers are limited to the maintained
Genshin, Wuwa, HSR/Honkai, and Generic Toon workflows.

The importer continues to read legacy core and overlay versions for unaffected
workflows. A non-empty unknown or retired preset identifier produces a
structured `unsupported_preset` error and stops generation; it is never
reinterpreted as Standard PBR or another game preset.

Historical plans, audits, schemas, handoffs, and changelog entries remain for
traceability but are excluded from current release inputs.

## Consequences

- Existing Endfield documents require the user to select and author a different
  supported workflow; there is no automatic visual migration.
- Public schema and component versions change because a compatibility surface
  was removed.
- Remaining target-neutral NPR fields and shared Face SDF runtime support stay
  available to maintained workflows.
