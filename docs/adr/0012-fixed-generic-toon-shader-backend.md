# ADR 0012: Use a fixed URP backend for Generic Toon

## Status

Accepted — 2026-07-30

## Context

Generic Toon requires semantic passes that Shader Graph templates cannot safely
express as editable, version-neutral graphs: original-material inverted-hull
outline, a character mask pass, deterministic shared per-material layout, and
version-specific RenderGraph composition.

## Decision

Generic Toon is the approved exception to the project's normal Shader Graph
backend rule. It uses eight fixed ShaderLab assets backed by shared HLSL and
URP 17.4 adapters. Every semantic shader owns its outline and mask Pass; the
renderer feature draws those passes with original materials and never uses an
outline override material.

New `generic_toon` imports produce materials and a recipe but no wrapper or
subgraph. Existing wrapper/subgraph assets are user-owned legacy assets and are
never deleted.

## Consequences

Generic Toon shaders are editable through Material properties and the Miku
Shader GUI, not by editing generated Shader Graph topology. Shader compilation,
pass layout, CBUFFER identity, alpha clipping, and RenderGraph behavior become
explicit compatibility tests for Unity 6000.4.5f1/URP 17.4.0.
