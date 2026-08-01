# Delta-E validation toolkit

## Cycles reference capture

1. Open the intended `.blend` source in a compatible Blender build.
2. For each material, position the camera at the relevant body part, render at 512×512, and save the result under `references/character/`.
3. For automation, run `python -m tools.delta_e_tool.capture_cycles_ref --blend <path> --materials Body Hair ...`.

## Comparing URP to Cycles

- Single pair: `python -m tools.delta_e_tool.compare --urp path/to/urp.png --cycles path/to/cycles.png --out-heatmap path/to/heat.png`
- Batch: `python -m tools.delta_e_tool.batch --urp-dir renders/urp --cycles-dir references/character --out-dir gates`
- Structural comparison: `python -m tools.delta_e_tool.structural_metrics --urp path/to/urp.png --cycles path/to/cycles.png`
