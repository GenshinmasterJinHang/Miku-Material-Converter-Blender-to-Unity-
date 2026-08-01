# Miku Certified GPL Bake Worker

This Blender Extension is licensed under `GPL-2.0-or-later`. It consumes
`miku-bake-request-1.0` JSON artifacts and writes
`miku-bake-result-1.0` JSON plus hashed image resources. Worker 1.2.0 also
writes a deterministic evaluated static GLB and renderer bindings whenever a
Source Mesh Fidelity bake is requested. The MIT Miku core and
Unity package do not contain or link this worker's GPL implementation.
The bundled `miku/` artifact-protocol helpers retain their MIT terms, included
as `LICENSE-MIT.txt`.

The certified execution profile is Blender 5.2.0 LTS build
`fbe6228777e7d9afefcd61a413844e790ae75db7`, Cycles CPU, 1024×1024,
16 samples, 16 px margin, and random seed 0.
