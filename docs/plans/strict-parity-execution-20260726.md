# StrictParity execution record — Blender 5.2 metal library

## Scope

- Blender: 5.2.0 LTS, `fbe6228777e7d9afefcd61a413844e790ae75db7`
- Unity: 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0
- Source set: five `.blend` files under `材质库/金属`
- Assigned materials audited: 73
- Contract: `mgir-5.1` / `b2u-bundle-2.1`, StrictParity

## Completed

1. Added closed route manifests, explicit socket ABI metadata, deterministic
   validation digests, and strict schema validators.
2. Added the Shader Graph 17.4 Custom Function reflection bridge. A procedural
   node is emitted as Custom Function only when its HLSL implementation is
   source-verified; otherwise the backend requires a baked representation.
3. Added a same-volume staging-directory publisher with a sealed manifest and
   last-written `publish.commit.json` marker. The importer rejects unsealed or
   uncommitted StrictParity bundles.
4. Ran Blender read-only audit: all five files opened successfully and all 73
   assigned materials were enumerated. Result: `strictParityCount=0`,
   `blockedCount=73`, `importAllowed=false`.
5. Generated preview bake candidates for all five metal files. Three Blender
   processes reported post-export access violations after complete artifacts
   were written; those artifacts remain uncertified.
6. Unity EditMode suite passed 40/40; no new compiler errors were reported.

## Current gate

Import remains blocked for every material. Baked routes are marked
`pending_visual_gate`, root certification is `pending`, and the Noise/Voronoi
HLSL files are still `unverified-clean-room`. The official source ABI and
license provenance are recorded in
[`docs/provenance/blender-5.2-noise-source.md`](../provenance/blender-5.2-noise-source.md).

## Required before import

- license-reviewed, source-derived pure-function HLSL where claimed;
- Blender/Unity node-output oracle evidence;
- nine required visual scenarios with numeric thresholds;
- deterministic sealed batch manifest and Unity compile/scene/reference checks;
- a final 73/73 pass. Until then no material is written into the Unity review
  project.
