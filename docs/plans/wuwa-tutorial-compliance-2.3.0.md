# Miku 2.3.0 Wuwa tutorial-compliance repair (Unity side)

## Purpose and outcome

Align the existing `MIKU/Wuwa/*` ShaderLab backends with the user-supplied
IcePaper Wuwa tutorial: simplified CookTorrance direct specular and
reflection-probe GI, MatCap added onto the albedo at low saturation, UV3
vertical gradient, Fresnel-step rim, enabled hair-shadow sampling, official
face SDF with the soft channel, eye main-light response with official eye
textures, and vertex-color outline width with the tutorial's two-segment
distance formula. Scope is Unity-only; Blender export, schemas, and texture
roles are unchanged.

## Context and constraints

- Canonical source roots only: `unity/Packages/com.miku.shaderconverter/`.
  Installed package caches and `dist/` are outputs.
- The worktree already contains extensive uncommitted 2.3.0 work. Preserve it;
  edits are narrowly additive.
- Validation uses the local `Desktop/unity/test` Unity project through the
  port-8080 Unity MCP server, Unity 6000.4.5f1, URP/Shader Graph 17.4.0.
- Private Feibi model/textures are validation inputs only and must not enter
  the public repository.
- No schema version, package ID, workflow ID, or existing public texture role
  changes. New shader properties are additive public surface and require
  changelog/compatibility documentation.

## Implementation

1. WuwaCommon: add `Wuwa_DirectBRDFSpecular`, `Wuwa_IndirectSpecular`,
   `Wuwa_MatcapAlbedo`, `Wuwa_VerticalGradient`, `Wuwa_FresnelStepRim`,
   `Wuwa_GradientValue`, and `Wuwa_TutorialOutlineWidth` with finite guards.
2. Wuwa_Body: pre-light MatCap at 10% saturation, simplified CookTorrance
   specular, reflection-probe GI, UV3 gradient (default channel 3), Fresnel
   rim, vertex-color outline mask, tutorial distance mode.
3. Wuwa_Hair: gradient, Fresnel rim, step-gated narrow highlight, vertex-color
   outline mask, tutorial distance mode.
4. Wuwa_Face: official SDF channel semantics (A step / B soft) with soft
   channel enabled, hair shadow enabled by default, gradient present but off,
   vertex-color outline mask, tutorial distance mode.
5. Wuwa_Eye: main-light response via `_EyeShadowTint`/`_EyeLitTint`; bind
   official HDMF/EG textures in the validation materials.
6. Editor: recommended-profile defaults; idempotent hair-shadow renderer
   feature installer with tests.
7. Tests: CPU math mirrors, shader property contracts, profile application,
   installer idempotency, outline pass source contract.
8. Docs: plan, audit, changelogs, compatibility matrix, provenance.

## Validation

- Unity EditMode tests and `ShaderUtil` compile checks for all five Wuwa
  shaders.
- `python tools/ci/run_checks.py --profile pr` plus regenerated package
  identity manifest.
- Deterministic double build of the Unity TGZ with matching SHA-256.
- Reinstall into `Desktop/unity/test`, install the hair-shadow renderer
  feature, create versioned `菲比_2.3.0` materials and a validation scene,
  and capture front/profile/light-yaw screenshots for each tutorial effect.

## Known deviations

- No metallic map exists in the supplied resources; `_Metallic` is a material
  float and MatCap/body masks use `idMap.r` instead of `step(0.8, metallic)`.
- Face SDF uses the official `T_FemaleMFace01_SDF` A/B channels; the previous
  project PNG had R zeroed and a non-official alpha mix.
- Outline width applies the tutorial formula as a multiplier on `_OutlineWidth`
  (the tutorial's absolute constants are scaled by the material width).
