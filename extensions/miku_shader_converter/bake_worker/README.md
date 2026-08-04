# Miku Certified GPL Bake Worker

This Blender Extension is licensed under `GPL-2.0-or-later`. It consumes
frozen `miku-bake-request-1.0` / `1.1` and current
`miku-bake-request-1.2` JSON artifacts
and writes
`miku-bake-result-1.0` JSON plus hashed image resources. The current worker also
writes a deterministic evaluated static GLB and renderer bindings whenever a
Source Mesh Fidelity bake is requested. The MIT Miku core and
Unity package do not contain or link this worker's GPL implementation.
The bundled `miku/` artifact-protocol helpers retain their MIT terms, included
as `LICENSE-MIT.txt`.

Request 1.2 binds execution to the exact Blender numeric version and build hash
recorded by its exporter and allows Blender 5.0.0 through 5.2.0. Versions other
than 5.2.0 are Allowed / Unvalidated and retain a structured warning in the
bake result. Frozen request 1.0/1.1 artifacts continue to require the certified
Blender 5.2.0 LTS build
`fbe6228777e7d9afefcd61a413844e790ae75db7`, Cycles CPU, a selected 512,
1024, 2048, or 4096 square 2D bake resolution, 16 samples, 16 px margin, and
random seed 0. Request 1.0 remains fixed at 1024.
