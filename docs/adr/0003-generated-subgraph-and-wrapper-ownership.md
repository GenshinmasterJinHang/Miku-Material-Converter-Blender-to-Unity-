# ADR 0003: Generated subgraph and wrapper ownership

- Status: Accepted
- Date: 2026-07-22

## Decision

Future `*.generated.shadersubgraph` files are MiGR-owned and replaceable. Wrapper
`.shadergraph` files become user-owned after initial creation. Sidecar maps and
reports are MiGR-owned. Existing editable `.shader` files retain their current
preserve-by-default user ownership.

## Consequences

Ordinary regeneration cannot overwrite a user wrapper. Full Regeneration is the
only explicit override. All owned replacements are atomic and output-root
constrained.
