# Deterministic export identity migration

Foundation hardening changes generated node IDs from collection ordinals to
stable source-derived identities and omits `metadata.exportedAt` unless a caller
supplies it explicitly.

## Expected one-time diff

The first re-export can change node IDs and every edge/entry reference to those
IDs. Semantic operations, public schema version, package IDs, Blender operators,
and shader property reference names do not change. Subsequent insertion of an
unrelated source node should not change existing node IDs.

Consumers must treat node IDs as opaque keys. If a tool persisted an ordinal ID
outside a Miku document, regenerate that association from the new document and
source metadata. Pass an explicit timestamp only when provenance needs it and
when byte reproducibility is not required.
