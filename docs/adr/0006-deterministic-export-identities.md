# ADR 0006: Deterministic export identities and metadata

- Status: Accepted
- Date: 2026-07-22

## Decision

MiGR node IDs are derived from stable group-instance path, source node type/name,
and a collision digest, rather than ordinal position. Default export omits
`metadata.exportedAt`; callers may supply a timestamp explicitly when provenance
requires it.

## Consequences

The first export after this change may update every node/edge reference once.
Subsequent insertion of an unrelated node does not churn existing IDs. Consumers
must treat IDs as opaque strings. The MiGR schema version and file paths do not
change.
