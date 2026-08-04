# Blender bake request 1.2 migration

Miku Blender extension 2.2.9 emits `miku-bake-request-1.2`. Its settings retain
the Cycles CPU device, deterministic random seed, samples, margin, and the four
supported bake resolutions from request 1.1. The version and commit fields now
record the executing Blender build instead of a single constant.

The 2.2.9 worker accepts Blender 5.0.0 through 5.2.0 and verifies that the
request version and build hash match the current process. Blender 5.0 and 5.1
continue with `MIKU_BLENDER_VERSION_UNVALIDATED`; 5.2.1 and later fail before
bake resources are written.

Request 1.0 and 1.1 remain frozen to Blender 5.2.0 and its certified commit.
The 2.2.9 worker continues to accept those documents without rewriting them.
Older workers do not know request 1.2 and must fail explicitly, so install the
2.2.9 unified extension archive to keep exporter and worker paired.

Bake result 1.0, MaterialIR, Bundle, conversion plan, resource metadata, and
Unity imports do not change schema.
