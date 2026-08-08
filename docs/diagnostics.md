# Diagnostic codes

Diagnostic codes are stable machine-readable identifiers. Existing compiler and
exporter codes remain compatible; this table documents core safety expectations
and may be expanded as code is normalized.

| Code | Severity | Meaning / action |
| --- | --- | --- |
| `MIKU_BLENDER_VERSION_UNSUPPORTED` | Error | Blender is outside `>=5.0.0,<5.3.0`; install Blender 5.0, 5.1, or 5.2. |
| `MIKU_BLENDER_VERSION_UNVALIDATED` | Warning | The Blender patch is inside the supported interval but is not a recorded validation runtime; capability preflight still runs. |
| `MIKU_BLENDER_CAPABILITY_MISSING` | Error | A nominally supported Blender runtime lacks a required `bpy` type, property API, or translation API; conversion stops instead of substituting defaults. |
| `MIKU_UNITY_VERSION_UNSUPPORTED` / `MIKU_URP_VERSION_UNSUPPORTED` / `MIKU_SHADERGRAPH_VERSION_UNSUPPORTED` | Error | Unity must be stable 6000.0-6000.5 and both packages must be stable 17.0-17.5. Prereleases and future technical lines are rejected before asset writes. |
| `MIKU_UNITY_PACKAGE_VERSION_MISMATCH` | Error | Unity 6000.N is not paired with URP/Shader Graph 17.N, or the exact URP and Shader Graph package versions differ. Correct the project package lock before importing. |
| `MIKU_SHADERGRAPH_ADAPTER_INCOMPATIBLE` | Error | The selected 17.0-17.5 adapter failed its full property/node/port/connection/Custom Function/output/serialization capability preflight. |
| `MIKU_SHADERGRAPH_TEMPLATE_IDENTITY_MISMATCH` / `MIKU_SHADERGRAPH_TEMPLATE_IMPORT_FAILED` | Error | A package wrapper differs from its fixed identity or cannot be imported by the selected Shader Graph version; reinstall a verified TGZ. |
| `MIKU_SOURCE_MESH_PBR_RESOURCE_SUPERSEDED:<semantic>` | Info | A source texture remains in the Bundle for provenance, but the final Source Mesh PBR channel graph no longer references its Shader property. Miku imports the texture without binding it; reachable properties still fail with `MIKU_SHADER_PROPERTY_MISSING`. |
| `MIKU_SKIN_MASK_TEXTURE_MISSING:<shader>:<property>` | Warning | A Body material was opted into the recommended skin profile without its required authored LightMap/IDMap. Miku leaves Body SSS disabled; bind the named texture and reapply the profile. |
| `unsupported_version` | Error | Miku/schema version is unknown; use a supported exporter/importer pairing |
| `unsupported_node` | Error or Warning | Required node stops the material; safely pruned node may warn |
| `invalid_numeric_value` | Error | NaN or Infinity found; correct source/default data |
| `coordinate_space_conflict` | Error | Connected sockets have incompatible explicit spaces; add a valid transform |
| `shader_stage_conflict` | Error | Fragment-only expression reached a vertex chain |
| `MIKU_RUNTIME_INPUT_PRESERVED` | Info | A supported view, camera, geometry Backfacing, time, Fresnel, or Layer Weight chain was retained as native runtime Shader Graph expressions; no whole-material bake was created |
| `MIKU_RUNTIME_INPUT_UNSUPPORTED` | Error | A required runtime-dependent chain contains an operation the selected backend cannot lower, or a requested Height bake depends on view/camera/time data that cannot be encoded in UVs; simplify it or add backend support |
| `MIKU_TIME_INPUT_UNSUPPORTED` | Error | A new Blender export reaches an effective output through `Input.Time.*`; remove the time dependency or keep it disconnected. Historical time-dependent Bundles remain importable |
| `MIKU_PORTABLE_UV_BAKE_SCHEDULED` | Info | Portable Hybrid scheduled a statically proven UV0 expression island on the canonical 0-1 bake plane; the generated Texture2D has no source-mesh binding |
| `MIKU_PORTABLE_HYBRID_MESH_DEPENDENCY` | Error | A required Portable Hybrid chain still depends on Generated/Object coordinates, surface geometry, topology, AO, Wireframe, or another source-mesh domain; use a UV0 source, simplify the chain, or explicitly choose a mesh-bound mode |
| `MIKU_PORTABLE_RESOURCE_MESH_BOUND` | Error | A `PreferNative` bundle or plan illegally contains SourceMesh, `meshBinding`, or a mesh-bound bake job; export/import is rejected rather than treating it as portable |
| `MIKU_FIXED_WORKFLOW_SOURCE_GRAPH_IGNORED` | Warning | A fixed Toon workflow preserved the Blender graph in Source Map but exported only static images and deterministic minimal Material IR |
| `MIKU_FIXED_WORKFLOW_CONVERSION_MODE_IGNORED` | Info | A fixed Toon workflow ignored conversion mode because it never schedules Shader Graph or bake jobs |
| `MIKU_FIXED_TEXTURE_NOT_EXPORTABLE` | Warning | An Image Texture is missing, dynamic/generated, unreadable, or unsupported and was skipped without failing the material |
| `MIKU_FIXED_TEXTURE_ROLE_AMBIGUOUS` | Warning | Equally authoritative images claim one role, so that role remains unbound |
| `MIKU_FIXED_TEXTURE_INACTIVE_PRIMARY_IGNORED` | Warning | An inactive image claimed BaseMap or EmissionMap, but the unique active Surface-chain image was selected instead |
| `MIKU_FIXED_TEXTURE_UNASSIGNED` | Warning | The image imported with a stable GUID but has no recognized role |
| `MIKU_WUWA_STOCKINGS_ID_SOURCE_MISMATCH` | Error | Wuwa Body received different textures for IDMap and StockingsMap; bind the same authored linear ID resource to both roles |
| `MIKU_TOON_SCREEN_RIM_RENDERER_FEATURE_REQUIRED` | Warning / RequiresProjectSetup | Fixed Toon materials imported, but screen-space depth rim requires explicit installation on the active Renderer Data |
| `MIKU_STANDARD_PBR_SEMANTIC_EXTRACTION_FAILED` | Error | Standard PBR semantic extraction failed; closure-derived slots are retained, but texture binding may be incomplete and the material should be re-exported after fixing the source graph |
| `MIKU_SOCKET_AMBIGUOUS` | Error | More than one active Blender socket still matches after exact-identifier and value-type resolution; update the node mapping instead of relying on socket order |
| `MIKU_REQUIRED_CHANNEL_UNSUPPORTED` | Error | NativeOnly cannot preserve a required channel |
| `MIKU_SOURCE_MESH_FIDELITY_REQUIRED` | Error | The requested portable mode would require a topology/UV-bound Texture2D or a static surface projection. The diagnostic includes the deepest unsupported source and its active consumer path. Select Source Mesh Fidelity or simplify the source; Auto never upgrades itself |
| `MIKU_FULL_PBR_BAKE_REQUIRED` | Error | Source Mesh Fidelity cannot safely split the complete static closure surface into independent channels. Select Full PBR Bake explicitly; neither Auto nor Source Mesh Fidelity upgrades itself |
| `MIKU_SOURCE_MESH_FIDELITY_SCHEDULED` | Info | The caller explicitly selected Source Mesh Fidelity and Miku scheduled deterministic lighting-independent PBR channel baking against the bound source mesh |
| `MIKU_SOURCE_MESH_CLOSURE_FALLBACK` | Warning | A malformed legacy closure branch required a descriptive IR fallback for Full PBR planning; the isolated worker still evaluates the original Blender material |
| `MIKU_SOURCE_MESH_PBR_CHANNEL_APPROXIMATED:<channel>:URP_METALLIC_WORKFLOW_FIXED_F0` | Warning | A baked Source Mesh PBR channel has no editable URP Metallic input. The resource remains sealed for provenance, is not bound to a fabricated property, and the receipt names the fixed-F0 approximation (currently used for per-pixel IOR) |
| `MIKU_NOISE_RUNTIME_APPROXIMATE` | Warning/Error | 3D Blender Noise Factor uses the clean-room runtime HLSL approximation; Strict rejects it |
| `MIKU_NOISE_COLOR_SCALAR_USES_FACTOR` | Info | A Noise Color link entering a scalar socket was normalized to the node's Factor output instead of using the unvalidated pseudo-color route |
| `MIKU_LEGACY_MESH_BOUND_BUNDLE_UNSAFE` | Error | A Bundle 2.0 texture declares `meshBinding` but contains no sealed source mesh; re-export with 2.1.0 |
| `MIKU_MESH_BINDING_MISMATCH` | Error | GLB hash, mesh/vertex/index/UV data, renderer slots, or the selected Renderer fingerprint does not match the sealed source binding; the import or apply operation is rolled back/refused |
| `MIKU_SOURCE_MESH_DEFORM_UNSUPPORTED` | Error | Source Mesh Fidelity encountered an armature, animation, or unsupported runtime-deformed mesh; only static evaluated Mesh is supported |
| `MIKU_SOURCE_MESH_FIDELITY_PREFAB:<path>` | Info | Unity generated the authoritative Prefab for a mesh-bound material; use this Prefab or the fingerprint-checked apply operation |
| `MIKU_NON_AUTHORITATIVE_COMPATIBILITY_RESOURCE_SKIPPED:<semantic>` | Info | A bundle carried a top-level compatibility bake not consumed by its `CustomMultiLobe` graph. Unity skipped only this proven unreachable resource; reachable missing properties still fail |
| `MIKU_COAT_URP_APPROXIMATION` | Warning/Error | Auto maps the safe Principled Coat subset to URP 17.4 Clear Coat and records Approximate; Strict rejects the BRDF mismatch |
| `MIKU_COAT_PROFILE_REEXPORT_REQUIRED_2_0_2` | Error | A bundle claims a 2.0.0/2.0.1 target profile while declaring Clear Coat; re-export with the coordinated 2.0.2 exporter |
| `MIKU_TARGET_PROFILE_2_0_X_COMPATIBILITY` | Info | Unity 2.0.3 imported a known 2.0.2 profile, or a non-Coat 2.0.0/2.0.1 profile, through the bounded compatibility path |
| `MIKU_GENERATED_RESOURCE_UNREFERENCED:<bindingKey>` | Error | A sealed `_MIKU_Baked_*` resource is not reachable from generated output and therefore has no Shader Graph property; fix the backend/data flow rather than silently skipping the texture |
| `MIKU_LIGHT_PATH_UNSUPPORTED:<socket>` | Error | A required Surface chain uses a Light Path output other than Camera Ray or Shadow Ray; those additional ray type/depth semantics are Cycles-only and are not replaced by constants |
| `MIKU_IMAGE_SOURCE_UNSUPPORTED` | Error | The required Image Texture is UDIM/tiled, sequence, movie, generated, or another unsupported source; use a static File/packed PNG, JPEG, or EXR image |
| `MIKU_IMAGE_FORMAT_UNSUPPORTED` | Error | The static image is not PNG, JPEG, or EXR |
| `MIKU_IMAGE_PROJECTION_UNSUPPORTED` / `MIKU_IMAGE_UV_SOURCE_UNSUPPORTED` | Error | Miku 2.2 direct image sampling requires Flat projection and implicit active UV/UV0 |
| `MIKU_IMAGE_INTERPOLATION_UNSUPPORTED` / `MIKU_IMAGE_EXTENSION_UNSUPPORTED` | Error | Miku 2.2 direct image sampling supports Closest/Linear and Repeat/Extend only |
| `MIKU_IMAGE_FILE_MISSING` / `MIKU_ARTIFACT_MISSING` | Error | A required source image or sealed bundle artifact is absent; restore it and re-export/reimport |
| `MIKU_DATA_TEXTURE_COLOR_SPACE_UNSUPPORTED` | Error | Roughness, Metalness, Height, or Normal was not authored as Non-Color/Linear |
| `MIKU_NORMAL_MAP_SPACE_UNSUPPORTED` | Error | The native normal route requires Tangent Space and active UV/UV0; OpenGL positive-Y or DirectX negative-Y must be selected explicitly at material level |
| `MIKU_PACKED_TEXTURE_COLOR_SPACE_CONFLICT` / `MIKU_PACKED_RESOURCE_COLOR_SPACE_CONFLICT` | Error | One physical image is used as both color and scalar data, or a packed scalar resource is not Linear; split the color/data resources |
| `MIKU_PACKED_CHANNEL_MODE_UNSUPPORTED` / `MIKU_PACKED_CHANNEL_OUTPUT_UNSUPPORTED` | Error | Packed semantics were inferred from unsupported topology rather than explicit Separate Color/XYZ or Image Alpha wiring |
| `MIKU_CHANNEL_BINDINGS_INVALID` / `MIKU_CHANNEL_BINDING_SEMANTIC_INVALID` / `MIKU_CHANNEL_BINDING_CHANNEL_INVALID` | Error | Bundle 2.2 packed-channel metadata is malformed or contains an unsupported semantic/channel |
| `MIKU_DISPLACEMENT_SPACE_UNSUPPORTED` | Error | Native height displacement requires the Blender Displacement node to use Object space |
| `MIKU_DISPLACEMENT_NORMAL_INPUT_UNSUPPORTED` | Error | The Displacement node's Normal input is linked; Miku 2.2 supports only the unlinked geometry-normal direction |
| `MIKU_DISPLACEMENT_MIDLEVEL_DYNAMIC_UNSUPPORTED` / `MIKU_DISPLACEMENT_SCALE_DYNAMIC_UNSUPPORTED` | Error | Midlevel or Scale is linked; Miku 2.2 requires finite constants |
| `MIKU_DISPLACEMENT_PARAMETER_NONFINITE` | Error | Midlevel or Scale is NaN/Infinity |
| `MIKU_BUMP_VERTEX_PROMOTION_PARAMETER_UNSUPPORTED` | Warning | `ALWAYS_VERTEX` could not promote Bump to vertex displacement because Strength or Distance is dynamic or non-finite; the normal path remains available but no vertex Height contract is invented |
| `MIKU_MULTIPLE_HEIGHT_SOURCES_NOT_COMBINED` | Warning | Active displacement consumers use different raw Height endpoints. Miku does not guess a primary source or combine them; independent normal bakes remain and no shared Height map is emitted |
| `MIKU_HEIGHT_SOURCE_ENDPOINT_MISSING` / `MIKU_HEIGHT_SOURCE_SOCKET_MISSING` | Error | The isolated worker could not resolve the stable node/socket endpoint requested for the raw Height bake |
| `MIKU_VERTEX_DISPLACEMENT_REQUIRES_SUBDIVIDED_MESH` | Warning / RequiresProjectSetup | The graph writes Vertex Position, but visual displacement requires a sufficiently subdivided model |
| `MIKU_JPEG_REQUIRES_BUNDLE_2_2` / `MIKU_HEIGHT_REQUIRES_BUNDLE_2_2` | Error | A JPEG or Height resource was placed in an older bundle contract; re-export as Bundle 2.2 |
| `MIKU_COLORED_TRANSPARENCY_APPROXIMATE` | Warning | Colored Transparent BSDF cannot preserve background tint exactly, especially with Dithered depth coverage |
| `MIKU_COLOR_RAMP_BSPLINE_APPROXIMATE` | Warning | A multi-stop B-Spline ramp is preserved as a deterministic piecewise Shader Graph approximation |
| `MIKU_SURFACE_SCHEMA_UNKNOWN` | Error | The optional surface companion schema is unknown; install the matching exporter/importer |
| `MIKU_SURFACE_CHANNEL_REFERENCE_MISSING` | Error | A surface contract references an absent MaterialIR channel |
| `MIKU_SURFACE_CONTRACT_PROFILE_UNSUPPORTED` | Error | A transparent/dielectric contract was paired with a pre-1.2 target profile and cannot be silently imported as Opaque |
| `MIKU_TARGET_PROFILE_1_2_0_SURFACE_COMPATIBILITY` | Info | A non-dielectric Miku 1.2.0 surface bundle is being imported by the bounded 1.2.1 compatibility path |
| `MIKU_DIELECTRIC_REEXPORT_REQUIRED_1_2_1` | Error | A Miku 1.2.0 dielectric bundle may contain Blender's unavailable zero Weight socket; re-export with the 1.2.1 semantic exporter |
| `MIKU_SHADERGRAPH_DUPLICATE_NODE_ID:<role>` | Error | Shader Graph rejected a repeated deterministic node role; report the role and source bundle instead of retrying or randomizing IDs |
| `MIKU_SHADERGRAPH_CONNECTION_REJECTED:<context>` | Error | Shader Graph rejected a generated edge; inspect the preserved source role/slot context |
| `MIKU_SURFACE_CONTRACT_PRESERVED` | Info | A constant-only surface contract was emitted through the structured Shader Graph backend |
| `MIKU_SURFACE_MODEL_PRESERVED:<kind>` | Info | A MaterialIR 2.0 surface model was dispatched to its registered Shader Graph generator |
| `MIKU_CLOSURE_BACKEND_FEATURE_UNSUPPORTED` | Error | A required closure feature, including a linked per-lobe normal, has no faithful phase 1-5 backend |
| `MIKU_CUSTOM_LIGHTING_SSAO_UNAVAILABLE` | Warning/Error | Auto custom lighting omits screen-space ambient occlusion and records an approximation; Strict rejects the material |
| `MIKU_GLASS_LOW_QUALITY_APPROXIMATION` | Warning/Error | Auto uses one screen-color refraction sample and probe reflection; Strict rejects the material |
| `WEIGHT0003` | Warning | Add Shader parent weight is copied to both branches exactly and can be non-energy-conserving |
| `WEIGHT0005` | Warning/Error | An explicit Add Shader energy policy changed source energy; Strict rejects it |
| `WEIGHT0006` | Error | The weighted closure set exceeds the configured realtime budget and no lobe was dropped |
| `MIKU_STATIC_CHANNEL_BAKE_SCHEDULED` | Info | One or more linked static channels were proven independent of preserved runtime expressions and scheduled as channel-scoped mesh bakes |
| `MIKU_TIME_DRIVER_EXTERNALIZED` | Warning | A safe scalar driver was too complex for affine frame migration and became a stable exposed material parameter |
| `MIKU_TIME_DRIVER_AST_UNSUPPORTED` | Error | A required driver contains syntax outside finite numbers, `frame`, and `+ - * /`; it was not evaluated or frozen |
| `MIKU_RUNTIME_WRAPPER_PROPERTIES_MISSING` | Warning | A preserved user-owned wrapper predates one or more runtime material properties; defaults still compile, but review wrapper edits and choose Full Regeneration to expose script/Animator/Timeline controls |
| `unsafe_output_path` | Error | Resolved output escapes the selected root or is otherwise invalid |
| `MIKU_OUTPUT_IDENTITY_CONFLICT` | Error | The exact new output directory is already occupied by another or unowned identity; choose another root/name or resolve the listed owner |
| `MIKU_OUTPUT_IDENTITY_DUPLICATE` | Error | More than one existing directory claims the requested Source ID and Material ID; retain one authoritative directory before retrying |
| `MIKU_OUTPUT_IDENTITY_REUSED_OUTSIDE_OUTPUT_ROOT` | Info | The requested identity already owns generated assets elsewhere under `Assets`; Miku reused that authoritative directory and retained its paths and GUIDs |
| `MIKU_ASSET_GUID_COLLISION` | Error | A stable generated-asset GUID is owned by an unrelated project asset; resolve the listed role, existing path, and requested path before retrying |
| `MIKU_BLEND_MODE_UNSUPPORTED` | Error | A surface contract declared a blend mode that the selected Shader Graph backend cannot map; install a compatible package or re-export |
| `MIKU_WRAPPER_PRESENTATION_MIKUATED` | Info | An exact unmodified former Standard wrapper was upgraded to the current semantic PBR presentation while retaining its asset GUID |
| `MIKU_WRAPPER_PRESENTATION_MIKUATION_REQUIRED` | Warning | The Standard wrapper differs from the former/current templates and remains untouched; use Full Regeneration only after reviewing user edits |
| `MIKU_WRAPPER_RENDER_CONTRACT_MISMATCH` | Warning | The user-owned wrapper has a different Surface Type/ZWrite/Alpha Clip/Render Face/SubTarget contract; explicit Full Regeneration is required |
| `MIKU_STANDARD_PBR_AUTHORING_CONTROLS_UNAVAILABLE` | Warning | A preserved legacy or user-modified Standard wrapper does not contain the canonical authoring properties |
| `MIKU_STANDARD_PBR_ALPHA_IGNORED_OPAQUE` | Warning | Alpha data was retained where possible but the current Standard wrapper is fixed Opaque and does not use it |
| `MIKU_STANDARD_PBR_AO_TEXTURE_UNSUPPORTED` | Warning | The Standard wrapper supports a scalar Occlusion value but no AO texture input |
| `MIKU_LEGACY_ZERO_NORMAL_NORMALIZED` | Info | A pre-fix Miku 1.1.1 bundle carried Blender's unconnected closure Normal sentinel `[0, 0, 0]`; Unity used neutral tangent normal `[0, 0, 1]`, left `_BumpMap` unset, and retained `_NormalStrength = 1` |
| `MIKU_LEGACY_CLOSURE_ZERO_NORMAL_NORMALIZED` | Info | A supported Miku 1.0.3 bundle carried an unconnected closure `Normal` or `Coat Normal` as `[0, 0, 0]`; Unity normalized only that constant closure parameter to neutral tangent normal `[0, 0, 1]` in memory before graph generation |
| `MIKU_CLOSURE_NONFINITE_VALUE_SANITIZED` | Warning | A `CustomMultiLobe` material contains zero/near-zero roughness or overlapping coat inputs that require the finite-safe 1.0.5 lighting path. Import remains source-compatible, while validation mode treats any non-finite rendered pixel as a failure |
| `MIKU_STANDARD_PBR_NORMAL_CONSTANT_UNSUPPORTED` | Warning | A non-flat constant tangent normal cannot be represented by the Standard normal-map control |
| `MIKU_TARGET_PROFILE_LEGACY_PRESENTATION_COMPATIBILITY` | Info | The bundle uses the one explicitly supported pre-presentation target profile and is imported through the bounded compatibility path |
| `MIKU_LEGACY_IDENTITY_SOURCE_MISMATCH` | Warning | A legacy root registry belongs to another Source ID; it is ignored and export continues |
| `MIKU_LEGACY_IDENTITY_REGISTRY_INVALID` | Warning | A legacy root registry is malformed or too large; it is ignored and export continues |
| `MIKU_MATERIAL_ID_DUPLICATE_REPAIRED` | Warning | A copied Material shared another Material ID and was assigned a new one |
| `MIKU_SOURCE_ID_COPY_DETECTED` | Warning | This `.blend` carries an identity first seen at another path; use Fork Source Identity only when it is an independent source |
| `MIKU_SOURCE_ID_SESSION_ONLY` / `MIKU_MATERIAL_ID_SESSION_ONLY` | Warning | Unsaved or read-only Blender data could not persist an identity; export succeeded for this session |
| `unsupported_backend` | Error | No exact Unity/URP/Shader Graph backend matches |
| `missing_texture` | Error or Warning | Required resource is missing, or optional resource was omitted |
| `image_export_failed` | Warning/Error | Blender image could not be exported; inspect source/provenance |
| `texture_image_resource_missing` | Error | Active Image/Environment texture has no resource; generation cannot claim support |
| `ies_resource_missing` | Error | IES node has no readable Internal Text or External File |
| `texture_node_baked_parity` | Warning | Node is supported through a concrete baked parity representation; inspect representation/dependencies |
| `texture_resource_bake_completed` | Info | All planned active Texture3D/direction resources exist and are bound to source outputs |
| `texture_resource_bake_failed` | Error | A required node-level Texture3D/direction resource was not produced; coverage is reduced and the active source route is not claimed as supported |
| `texture_resource_4d_snapshot` | Warning | A 4D procedural texture was baked with an unlinked constant W as a frame-specific Texture3D snapshot; re-export after W or the frame changes |
| `texture_requires_runtime_support` | Error | A required bake island depends on View, Light Path, camera, or another runtime-only input |
| `unsupported_hybrid_plan_version` | Error | Unity received an unknown `b2u-hybrid-plan-*` companion |
| `unsupported_bake_version` | Error | Unity received an unknown `b2u-bake-*` companion |
| `unsupported_optical_version` | Error | Unity received an unknown `cycles-optical-*` companion |
| `unsupported_source_resolved_by_bake` | Warning | Source node has no verified live implementation, but a validated baked parity asset is authoritative |
| `missing_edge` | Error | A referenced socket/link could not be reconstructed safely |
| `MIKU_CYCLES_OUTPUT_NOT_FOUND` | Error | No Material Output targets CYCLES or ALL |
| `MIKU_CYCLES_LIGHT_PATH_UNSUPPORTED` | Error | Required optical chain branches by Cycles ray type; simplify or bake explicitly |
| `MIKU_CYCLES_RAY_DEPTH_UNSUPPORTED` | Error | Required optical chain depends on Light Path ray depth; simplify or bake explicitly |
| `MIKU_CYCLES_CAUSTICS_NOT_PRESERVED` | Warning | Glossy/transmission ray classification implies path-traced caustic behavior that URP does not preserve |
| `MIKU_CYCLES_DISPERSION_NOT_PRESERVED` | Error | Wavelength-dependent color reaches the required optical chain; spectral dispersion is outside the subset |
| `MIKU_CYCLES_CLOSURE_COMPOSITE_UNSUPPORTED` | Error | Required closure Mix/Add is not a recognized dielectric pattern |
| `MIKU_CYCLES_VOLUME_SCATTER_UNSUPPORTED` | Error | Volume scattering requires runtime support outside this backend |
| `MIKU_CYCLES_PRINCIPLED_VOLUME_SURFACE_APPROXIMATE` | Warning/Info | Principled Volume is preserved as typed data and rendered as thickness absorption plus surface glow; review against Cycles |
| `MIKU_CYCLES_OPEN_MESH` | Warning | Solid optical material is assigned to open/non-manifold geometry |
| `MIKU_CYCLES_INCONSISTENT_NORMALS` | Warning | Manifold face winding is inconsistent |
| `MIKU_CYCLES_NON_UNIFORM_SCALE` | Warning | Absorption thickness is ambiguous under non-uniform object scale |
| `MIKU_CYCLES_OPTICAL_EXPRESSION_FALLBACK` | Warning | A live source route is outside the 1.0 Shader Graph registry; the typed editable property fallback remains |
| `MIKU_CYCLES_COLOR_TO_FACTOR_APPROXIMATE` | Warning | Blender's implicit color/vector-to-factor conversion was expanded with linear luminance weights |
| `MIKU_CYCLES_BASE_COLOR_FINAL_MODULATOR` | Info | Identity-default `_BaseColor` modulates final optical color while source tint remains on transmission |
| `MIKU_SG_SCALAR_TO_VECTOR_CONVERSION` | Info | Blender's scalar-to-vector conversion was expanded to explicit component replication |
| `MIKU_SG_REROUTE_CYCLE` | Error | A cyclic Blender reroute chain cannot be represented safely |
| `MIKU_SG_DISPLACEMENT_TO_NORMAL` | Info | Optical material displacement was retained as an editable tangent-space height normal |
| `MIKU_SG_ENVIRONMENT_DIRECTION_TEXTURE` | Info | HDR Environment Texture was expanded to Equirectangular or Mirror Ball direction sampling |
| `MIKU_SG_TEXTURE3D_PARITY` | Info | A concrete Blender volume atlas was materialized as a Unity Texture3D and sampled by source coordinate/domain |
| `MIKU_SG_TEXTURE3D_RESOURCE_MISSING` | Error | A baked Texture3D output references absent or invalid resource metadata |
| `MIKU_SG_DIRECTION_LUT_PARITY` | Info | Sky/IES/conditional Environment uses a concrete HDR Equirectangular direction LUT |
| `MIKU_SG_CHECKER_3D_EXACT` | Info | Checker uses Blender-equivalent three-dimensional floor parity, including negative coordinates |
| `MIKU_CYCLES_OPTICAL_PARITY_SLOT` | Info | Optical slot has a baked/live branch controlled by `_B2U_UseBakedParity` |
| `MIKU_CYCLES_OPTICAL_PARITY_RESOURCE_MISSING` | Error | A declared optical parity slot references a missing resource |
| `MIKU_SG_IMPLICIT_DEFAULT_NORMAL` | Info | Blender's unconnected zero Normal socket uses the geometry normal; Unity does not evaluate `normalize(0)` |
| `MIKU_SG_IMPLICIT_NORMAL_TYPE_MISMATCH` | Error | `ImplicitGeometryNormal` reached a non-normal consumer; reject the malformed expression instead of coercing it |
| `MIKU_CRYSTAL_THICKNESS_TEXTURE_CONFIGURED` | Info | Blender exported an explicit Non-Color thickness texture, channel, UV set, and scale |
| `MIKU_CRYSTAL_THICKNESS_TEXTURE_MISSING` | Warning | Texture thickness was requested without a usable resource; `_Thickness` remains the safe fallback |
| `MIKU_CRYSTAL_THICKNESS_CHANNEL_INVALID` | Warning | The imported channel is unknown; red is used and the mismatch is reported |
| `MIKU_CRYSTAL_THICKNESS_INVALID` | Warning | Thickness bounds are invalid; the documented safe range is used |
| `MIKU_CRYSTAL_THIN_SURFACE_CONFIGURED` | Info | Artist selected the ThinSurface shape; closed-volume behavior is not required and the wrapper renders both faces |
| `MIKU_URP_OPAQUE_TEXTURE_REQUIRED` | Warning | Generated Scene Color refraction needs URP Opaque Texture before visual success can be claimed |
| `MIKU_LINEAR_COLOR_SPACE_RECOMMENDED` | Warning | Beer-Lambert composition is authored for Linear color space |
| `MIKU_WUWA_EYE_HET_INHERITED` | Info | The current Eye material had no HET node and inherited the unique HET image from another material on the same mesh |
| `MIKU_WUWA_EYE_UV_MAPPING_UNSUPPORTED` | Warning | An authored Eye highlight uses a linked, animated, non-Point, or non-UV0 coordinate chain; the role is left unbound instead of misaligned |
| `MIKU_WUWA_EYE_2_2_6_REIMPORT_REQUIRED` | Warning | A 2.2.6 Eye recipe now uses HET as emission and must be re-imported to receive HDMF, upper/lower highlights, and UV transforms |
| `MIKU_FIXED_TEXTURE_UV_TRANSFORM_INVALID` | Error | A fixed-workflow material binding contains a malformed UV transform object |
| `MIKU_FIXED_TEXTURE_UV_TRANSFORM_UNSUPPORTED` | Error | A fixed-workflow UV transform is not the supported UV0 Affine2D contract |
| `MIKU_FIXED_TEXTURE_UV_MATRIX_INVALID` | Error | An Affine2D binding does not contain exactly six finite numeric coefficients |

Diagnostics should include severity, material, source node/socket and group path,
translation quality, and remediation where available. Do not parse localized log
sentences; consume the code and structured fields.
