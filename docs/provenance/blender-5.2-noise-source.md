# Blender 5.2 procedural reference

Miku uses Blender 5.2.0 LTS as a behavioral oracle for the acceptance corpus.
The exporter reads supported `bpy` data and emits target-neutral semantic
regions; it does not copy Blender implementation files or translate Blender
shader source into HLSL.

Noise, Voronoi, Wave, Brick, Magic, Gabor, White Noise, ramps, and bump data
are therefore represented as semantic regions. A route may be native,
reusable bake, mesh bake, or Unsupported according to the target profile. The
route and its fidelity are recorded in `miku-conversion-plan-1.0`.

Public Blender manuals and black-box renders are specification references only.
The Unity implementation is an independent MIT clean-room implementation and
must not be advertised as a source port.
