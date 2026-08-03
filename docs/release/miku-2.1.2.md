# Miku Blender extension 2.1.2 release notes

Miku Blender extension 2.1.2 targets Blender 5.2.0. Fixed-workflow texture
selection now distinguishes the active Material Surface chain from disconnected
authoring nodes. A unique active unlabelled primary image can therefore become
BaseMap without a disconnected filename/label candidate replacing it.

For Wuwa Body, the exporter recognizes the authored linear
`Image Color -> Greater Than 0.5` chain and assigns the same sealed resource to
both `IDMap` and `StockingsMap`. Wuwa Eye continues to use the existing
`EyeHET` role. Fixed-workflow TARGA inputs, including official Wuwa HET maps,
are deterministically transcoded to sealed PNG resources. No
`EyeBottomHighlight` role or schema version was added.

The deterministic archive is `miku_shader_converter-2.1.2.zip`. Validation is
performed only with
`C:\SteamLibrary\steamapps\common\Blender\blender.exe` reporting 5.2.0.
