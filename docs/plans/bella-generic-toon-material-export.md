# 贝拉 Generic Toon 材质导出与 Unity 重映射

## Purpose and outcome

Export the 24 materials used by `BeiLa_mesh` from the supplied Blender file as
Miku `generic_toon` BaseMap-only bundles, import them into the Unity validation
project under `Assets/卡通`, classify the resulting Toon recipes, and remap all
24 material slots of `Assets/卡通/贝拉.fbx` to the generated user materials.

## Constraints

- Blender executable: `C:\SteamLibrary\steamapps\common\Blender\blender.exe`;
  required version `(5, 2, 0)`.
- Unity tuple: 6000.4.5f1, URP/Shader Graph 17.4.0, Miku 1.0.5.
- Only `BeiLa_mesh` material slots are in scope; `mmd_tools_rigid_*` materials
  are excluded.
- Original Blender nodes are preserved; only Miku workflow, texture-role, and
  persistent identity metadata are saved after a recoverable backup.
- No Unity scene is saved or changed; only generated assets and the FBX importer
  material remaps are modified.

## Progress

- [x] Build and verify deterministic Blender/Unity packages.
- [x] Back up and configure the Blender source.
- [x] Export and validate 24 deterministic BaseMap-only bundles.
- [x] Import bundles and apply semantic recipes in Unity.
- [x] Remap and validate all 24 FBX material slots.

## Decisions

- Semantic mapping: Face (脸, cheek), BodySkin (体, 臂指), Hair (发, 睫眉),
  Eye (瞳, 瞳-外, 目), Mouth (口, 牙, 舌), Cloth (上衣1, 上衣2, 裤, 裙子,
  丝袜, 鞋, 皮带, 皮带1), MetalAccessory (金属, 发卡, 发饰, 饰品).
- Basecolor-only means only the original `mmd_base_tex` is bound as `BaseMap`;
  non-base image nodes are removed only from the in-memory export snapshot.
- `牙` has no source Base Texture and therefore retains the exporter’s solid
  Base Color fallback without fabricating a texture.

## Validation and rollback

- Compare two export tree hashes and verify 24 bundles, no bake jobs, and no
  blocking diagnostics.
- Verify 24 Unity recipes/user materials, expected shader names, BaseMap-only
  bindings, stable GUIDs after a repeat import, and 24 FBX external remaps.
- Capture a temporary unsaved PreviewScene render and inspect it; the offscreen
  editor render produced a black frame, so visual appearance remains unverified
  even though the model had one renderer and all 24 material slots resolved.
- Unity Console was empty after the operation; no new Miku, compile, or Shader
  errors were reported.
- Restore the Blender backup if source metadata persistence must be undone;
  remove only the generated `Assets/卡通` bundle directories and revert the FBX
  remap through the importer if a validation gate fails before completion.

## Results

- Blender 5.2.0 exported 24 bundles twice with identical 143-file trees.
- The source now contains 24 persistent material IDs and BaseMap roles; the
  original 32 non-base image nodes remain in the saved file.
- Unity generated 24 recipes and 48 material assets (base plus user material),
  and all 24 FBX material slots resolve to `Assets/卡通` user materials.
- Repeat imports preserved user-material and recipe GUIDs.
- The offscreen PreviewScene produced a black frame, so visual appearance is
  not claimed as validated; structural/material validation and Console checks
  passed.
