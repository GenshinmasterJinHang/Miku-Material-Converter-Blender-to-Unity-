# Shared output identity migration

Miku supports several Blender sources in one Blender export root and one Unity
generated-assets root. This changes directory ownership behavior without
changing `miku-bundle-1.0`, `persistentSourceId`, `persistentMaterialId`, public
property references, or Unity's stable GUID algorithm.

## Existing Blender exports

- Keep existing Bundle directories. Miku finds them by the Source ID and
  Material ID inside `*.migrbundle`, even when the directory is only the old
  material name.
- Keep or delete `.migr-identities.json` as desired. It is read-only migration
  input. When its Source ID matches, a missing Material custom property adopts
  the old ID. A foreign or malformed file produces a warning and is ignored.
- If the `.blend` itself has no Source ID, choose **Advanced > Migrate Legacy
  Identities** before deleting the registry. This explicit operation adopts the
  registry Source ID and matching Material IDs without modifying the registry.
- New directories use `<safe-material-name>__<12-character-material-id>`.
  Existing directories are not renamed merely to adopt the convention.
- A Material rename retains `miku_material_id` and therefore reuses its existing
  directory. A copied Material data-block with a duplicate ID is assigned a new
  ID automatically.

## Copied Blender files

A copied `.blend` deliberately retains the original source and material
identities, which means it continues the same generated assets. Miku reports
`MIKU_SOURCE_ID_COPY_DETECTED` when it can recognize the path change.

If the copy is a new independent source, open **Advanced** in the Miku Shader
Editor panel and choose **Fork Source Identity**, confirm, then save the
`.blend`. The operation assigns a new Source ID and new Material IDs. Its next
Unity import creates new GUIDs and does not replace the original source.

## Existing Unity assets

Do not move existing generated directories for this migration. Unity searches
under `Assets` for `*.miku-assets.json` documents with the same Source ID and
Material ID, even when a copied or re-exported bundle requests another output
root. A single match remains authoritative. Unity reuses the recorded Wrapper
Graph, generated Sub Graph, generated base Material, and user Material Variant
paths. Their `.meta` GUIDs remain unchanged, so Scene and Prefab references
continue to resolve.

Miku blocks an exact candidate directory occupied by another identity,
multiple directories claiming the requested identity, or a stable GUID already
owned by an unrelated asset. Identity discovery is bounded by directory count,
identity-document count and size, project-root containment, and reparse-point
checks. Diagnostics list project-relative paths; Miku does not guess which data
to overwrite.
