# Generic Toon anime-lighting design references

> Historical and retired in Miku 2.0. This provenance is retained to document
> the former implementation; it is not a current workflow or support claim.

The Generic Toon shader-family `1.1.0` implementation is original Miku MIT
source. No third-party Shader source, texture, model, or binary asset was copied
into the repository.

The implementation uses the following public material only as design-level
reference:

- Unity Toon Shader 0.8 documentation, "Three Color Map and Control Map
  Settings": the separation of lit, first-shadow, second-shadow, and optional
  shadow-control concepts. Copyright Unity Technologies.
  <https://docs.unity3d.com/ja/Packages/com.unity.toonshader%400.8/manual/Basic.html>
- Unity Toon Shader 0.8 documentation, "Shading Steps and Feather Settings":
  artist-controlled thresholds, boundary feathering, and the separation of
  received system shadows from the geometric cel boundary. Copyright Unity
  Technologies.
  <https://docs.unity3d.com/ja/Packages/com.unity.toonshader%400.8/manual/ShadingStepAndFeather.html>
- Junya Christopher Motomura, Arc System Works, "GuiltyGearXrd's Art Style:
  The X Factor Between 2D and 3D": the importance of deliberate thresholds,
  controllable normals/light vectors, material-specific shade colors, vertex
  data, and inverted-hull outlines. The talk describes production art
  principles; Miku does not reproduce its proprietary shader or assets.
  <https://www.ggxrd.com/Motomura_Junya_GuiltyGearXrd.pdf>

Miku deliberately differs from the locked-light Arc System Works presentation:
Generic Toon remains scene-light-aware for freely staged URP gameplay. Main and
additional lights, Light/Reflection Probes, cookies, light layers, shadow maps,
and Forward+ remain part of the runtime result. Art-directed FaceSDF, HairHM,
MatCap, and control maps are optional refinements rather than prerequisites.
