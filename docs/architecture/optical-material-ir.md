# Optical MaterialIR 1.0

Miku 1.2 represents transparent and dielectric intent with an optional strict
`surfaceContract` object inside `miku-material-ir-1.0`. It remains
target-neutral and contains no Blender objects, Unity class names, or Shader
Graph slot numbers.

The required companion fields are:

- `schema: "miku-surface-1.0"`;
- `model: StandardLit | DielectricScreenRefraction`;
- `renderMethod: Opaque | AlphaBlend | Dithered`;
- `renderFace: Front | Back | Both`;
- `coverageChannel`, referencing a Scalar MaterialIR channel.

`DielectricScreenRefraction` additionally references
`TransmissionColor`, `TransmissionWeight`, `IOR`, `Thickness`, `Roughness`,
and `Normal` channels. Emission remains an independent ordinary MaterialIR
channel. Unknown fields, companion schemas, missing references, or non-scalar
coverage fail validation.

Documents without `surfaceContract` retain the Miku 1.0/1.1 Opaque behavior.
Documents with it require the Miku 1.2 target profile so an old Unity package
cannot silently discard transparency.

Unity 1.2.1 accepts 1.2.0 non-dielectric surface contracts. A 1.2.0
`DielectricScreenRefraction` document is rejected with
`MIKU_DIELECTRIC_REEXPORT_REQUIRED_1_2_1` because Blender 5.2's unavailable
Glass Weight compatibility socket could have been serialized as an unintended
zero. The importer does not reinterpret an explicit zero.

## Unity 6 URP 17.4 lowering

`StandardLit` selects a versioned Opaque, Alpha Blend, or Dithered Lit wrapper.
Dithered output is Opaque, writes depth, applies the Shader Graph screen Dither,
and clips the result.

`DielectricScreenRefraction` selects a transparent Unlit wrapper. The generated
Sub Graph uses:

- Scene Color with a normal/IOR/thickness screen offset;
- Schlick Fresnel derived from IOR;
- Reflection Probe with roughness-derived LOD;
- `Smoothness = 1 - Roughness`;
- independent Emission and Coverage.

The importer reports `RequiresProjectSetup` when Camera Opaque Texture is
disabled or the project is not Linear. It never changes URP assets or project
color space automatically.

## Deliberate limits

Alpha Blend retains Unity transparent sorting, ZWrite, shadow, and intersecting
surface limitations. Dithered coverage is a 4×4 screen-space approximation and
may pattern or shimmer in motion. Colored Transparent BSDF cannot preserve
background tint and opaque depth dithering simultaneously.

Screen refraction does not model nested media, repeated refraction, internal
reflection, caustics, dispersion, transparent objects behind glass, or
physically exact rough transmission. Required Light Path ray depth remains
unsupported and blocks generation.
