# Testing

## Core PR profile

```bash
python tools/ci/run_checks.py --profile pr
```

This checks Python AST/Ruff, unit tests, JSON/schema validity, package structure,
notices, and deterministic release packaging. Test projects and large rendered
fixtures are not stored in the repository.

Run the Python suite directly with:

```bash
python -m unittest discover -s tests -p "test_*.py"
```

Golden/snapshot updates require a semantic explanation and review. Never replace
them automatically after failure.

## Unity EditMode

The repository provides `tools/ci/run_unity_editmode.ps1`. It creates a minimal
temporary Unity project from the canonical package and an explicit Unity path,
invokes the NUnit EditMode assembly, prints the result summary to the console,
and removes the complete temporary project, XML, and log in `finally` on success
or failure. Example:

```powershell
./tools/ci/run_unity_editmode.ps1 `
  -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe'
```

Record the result summary, editor version, URP/Shader Graph lock versions, OS,
and exit code. A configured project is not a passing test, and no persistent
Unity test project is required.

## Blender headless

Do not discover Blender from `PATH`. Invoke the pinned executable directly and
make every fixture assert `bpy.app.version == (5, 2, 0)`:

```powershell
& 'C:\SteamLibrary\steamapps\common\Blender\blender.exe' `
  --background --factory-startup `
  --python tests/blender/miku_runtime_inputs_smoke.py
```

The same executable is mandatory for extension installation and installed-copy
smoke tests. If it is missing or reports another version, stop; do not fall back
to `.tools`, Program Files, Steam launcher, or another Blender copy.
