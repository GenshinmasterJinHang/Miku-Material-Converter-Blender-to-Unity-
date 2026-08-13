# ADR 0018: HSR tutorial lighting masks and texture-neutral face detail

- Status: Accepted
- Date: 2026-08-12

## Decision

HSR Body and Hair use the literal tutorial Shadow AO calculation. Given
`HL = 0.5 * NdotL + 0.5`, LightMap green produces `shadowAO = 2 * G`, and the
two-component dot produces `signal = saturate(4 * HL * G)`. The Toon ramp is
sampled at the fixed coordinate `0.85 * signal + 0.15`.

LightMap blue is inverted into a bounded smoothstep threshold for the
Blinn-Phong response. Metal and non-metal branches share that thresholded mask
and differ only in their final color/strength response. Existing Body/Hair
threshold-center, threshold-softness, and ramp-offset properties remain
declared so old materials deserialize, but they no longer control the tutorial
equations.

HSR Face does not acquire a LightMap. Its new parameterized Blinn-Phong Toon
highlight uses existing geometry and material inputs and is gated to the
existing skin region. FaceMap blue remains the authored nose-line mask; its
response uses surface `NdotV`, a configurable power, strength, and color so the
line remains view-dependent but can be made clearly visible.

This correction is limited to those lighting/detail equations. It does not
restore the tutorial's two-pass layout; the HSR preset remains single-pass.

## Consequences

- Existing Body/Hair materials can render differently because three retained
  compatibility properties no longer alter the corrected tutorial path.
- Face gains additive material controls but no new required or optional texture
  binding.
- MaterialIR, Bundle, bake, fixed-workflow texture roles, shader names, material
  parts, and public C# schemas do not change.
- HSR remains Experimental on Unity 6000.4.5f1 with URP/Shader Graph 17.4.0;
  the decision does not claim pixel-exact game parity.
- Tutorial descriptions and local character assets remain behavioral
  references only. Miku distributes only its independent MIT-licensed
  implementation.
