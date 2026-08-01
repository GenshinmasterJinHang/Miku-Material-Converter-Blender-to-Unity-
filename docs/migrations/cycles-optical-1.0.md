# Migrating to the Cycles optical 1.0 companion

`miku-2.0` documents may now contain additive `opticalMaterial`,
`cyclesFeatureReport`, and `meshRequirements` members. Existing documents
without those members remain valid. Consumers that generate target assets must
validate `opticalMaterial.schema`; the only supported value is
`cycles-optical-1.0`. Unknown values fail rather than falling back to ordinary
PBR color channels.

Unity model consumers should add the three optional JSON objects and the
optional `entry.volume` socket. Diagnostic readers should accept
`translationQuality`, `sourceNodeId`, `sourceSocketId`, `targetAssetPath`, and
`suggestedFix`.

The generated property references `_BaseColor`, `_IOR`, `_Roughness`,
`_TransmissionWeight`, `_Opacity`, `_RefractionStrength`,
`_ReflectionStrength`, and `_Thickness` are public material bindings. Volume
materials additionally expose `_AbsorptionColor` and `_AbsorptionDensity`;
emissive optical materials expose `_EmissionColor` and `_EmissionStrength`.
Texture-authored thickness additionally exposes `_ThicknessMap` and
`_ThicknessMapScale`; its IR records a typed texture resource, R/G/B/A channel,
UV0/UV1 selection, and scale. Constant documents remain valid and unchanged.
Do not rename these references in a patch release.

The Blender material property `b2u_optical_shape_model` is additive and defaults
to `SOLID`, preserving prior exports. `THIN_SURFACE` writes `shapeModel` as
`ThinSurface`, sets `surface.thinWalled`, and removes the closed-mesh
requirement. Existing documents that already contain a valid shape model remain
wire-compatible.

No automatic migration is applied to unknown optical versions. Re-export from
a compatible Blender add-on or retain the prior generated assets until the
consumer is upgraded.

`ImplicitGeometryNormal` is an additive expression kind within
`cycles-optical-1.0`. It replaces the ambiguous constant `[0, 0, 0]` previously
emitted for an unconnected built-in shader Normal socket. Updated consumers
must resolve it to a varying geometry normal in the requested coordinate space;
they must not treat it as a uniform vector or serialize a `constant` payload.
The Unity 17.4 backend retains read compatibility for legacy constant-zero
Normal expressions and for Lerps whose two branches are both legacy defaults.
An explicitly connected zero vector remains a normal Socket expression and is
not migrated implicitly. Synthetic zero vectors created while flattening an
unconnected node-group Normal input are recognized through the existing
`groupInterfaceMappings` `input_default` provenance.
