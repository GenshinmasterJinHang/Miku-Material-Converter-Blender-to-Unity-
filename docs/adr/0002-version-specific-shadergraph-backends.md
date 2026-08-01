# ADR 0002: Version-specific Shader Graph backends

- Status: Accepted
- Date: 2026-07-22

## Decision

Shader Graph serialization is implemented by exact-version adapters. The first
candidate targets Unity 6000.4.5f1, URP 17.4.0, and Shader Graph 17.4.0 and must
derive from assets created by that editor. No generic Unity 6 writer and no
memory-invented MultiJson fields are permitted.

## Consequences

Unsupported version tuples fail clearly. Fixtures include provenance and are
normalized for review. Business/semantic logic cannot reference Unity Shader
Graph internal class names or slot IDs.
