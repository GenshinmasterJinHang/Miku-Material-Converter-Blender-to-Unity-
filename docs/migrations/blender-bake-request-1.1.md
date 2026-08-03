# Blender bake request 1.1 migration

Miku Blender extension 2.1.1 emits `miku-bake-request-1.1`. The only execution
contract change is that `settings.resolution` may be 512, 1024, 2048, or 4096;
the certified Blender build, Cycles CPU device, samples, margin, and random
seed remain fixed.

The 2.1.1 bundled worker accepts both request 1.0 and 1.1. Request 1.0 remains
frozen at 1024 and is never rewritten. An older worker does not know request
1.1 and must fail explicitly; install the unified 2.1.2-or-newer archive so exporter and
worker versions cannot drift.

Bake result 1.0, MaterialIR, Bundle, conversion plan schema, resource metadata,
and Unity imports require no migration. Existing automation that omits the new
`bake_resolution` keyword retains the 1024 default.
