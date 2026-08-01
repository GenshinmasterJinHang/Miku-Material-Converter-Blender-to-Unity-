# Adding node support

1. Describe the Blender node/socket behavior, defaults, value type, coordinate
   space, shader stage, uniformity, and texture semantics.
2. Decide whether translation is Exact, Equivalent, Approximate, Baked,
   RequiresProjectSetup, RequiresRuntimeSupport, or Unsupported.
3. Add pure core models/validation first. Blender-specific extraction stays in
   the integration layer; Unity internals stay in a version adapter.
4. Add focused positive and negative tests: unlinked defaults, groups, missing
   resources, space/stage conflicts, unsupported critical/unreachable paths, and
   deterministic IDs/order.
5. Update `docs/node-support.md`, diagnostics, compatibility evidence, and the
   changelog.
6. For Shader Graph, create the minimal asset in the exact target Unity version,
   normalize it as a reviewed fixture, and isolate serialization fields in that
   adapter. Never invent fields from memory.

Roughness support must demonstrate `smoothness = 1 - roughness`. Fragment-only
nodes must never appear in vertex displacement. Approximation requires a clear
visual/semantic warning.
