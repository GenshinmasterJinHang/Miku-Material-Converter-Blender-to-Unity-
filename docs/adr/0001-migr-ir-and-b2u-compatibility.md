# ADR 0001: MiGR IR and B2U compatibility labels

- Status: Accepted
- Date: 2026-07-22

## Decision

MiGR names the target-neutral semantic IR and the project's target architecture.
Existing B2U package IDs, namespaces, Blender operators/settings, file extensions,
and documented integration labels remain compatibility surfaces.

## Consequences

New semantic/core documentation uses MiGR. Integration UI may continue to say
B2U. No broad rename is part of foundation work. A future rename requires a
versioned migration and compatibility tests.
