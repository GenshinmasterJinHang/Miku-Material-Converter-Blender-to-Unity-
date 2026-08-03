# ADR 0013: Retire the Generic Toon workflow in Miku 2.0

## Status

Accepted (2026-08-01). Supersedes ADR 0012.

## Context

The Generic Toon fixed ShaderLab/HLSL backend requires a large amount of
character-independent lighting, texture, bounds, and RenderGraph maintenance.
Its optimization cost is no longer justified. Three game-specific fixed
workflows still depend on a small shared Screen Rim implementation that must
remain supported.

## Decision

Remove the Generic Toon workflow from current Core, Blender, CLI, Unity editor,
runtime, schema 2.0, and package outputs. New MaterialIR 2.0 documents may use
only Standard PBR, Genshin, WuWa, and HSR. A `generic_toon` document from a
frozen MaterialIR 1.0 schema is accepted only far enough to produce the
structured error `MIKU_WORKFLOW_RETIRED:generic_toon`; it is never converted to
another workflow.

Move the shared Game Toon Screen Rim implementation out of the retired
directory, preserve its Unity `.meta` GUIDs, and keep existing game shader names
and public material properties. Release the coordinated Python, Blender, and
Unity components as 2.0.0.

## Consequences

- Existing Generic Toon bundle imports stop before writing assets.
- Existing user materials and wrappers are not deleted, but materials that
  reference removed Generic Toon shaders require manual reassignment.
- MaterialIR 1.0, historical release records, and provenance remain available
  for traceability and unaffected-workflow compatibility.
- This is a deliberate public/API breaking change and requires the 2.0.0
  migration guide and release notes.
