# Local development

## Prerequisites

- Python 3.11
- .NET SDK 8
- Git
- Exact Unity 6000.4.5f1 for ephemeral Unity EditMode suites
- Exact Blender 5.2.0 LTS at
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe`

Install pinned development tools without adding them to production runtime:

```bash
python -m venv .venv
.venv/Scripts/python -m pip install -r requirements-dev.txt
.venv/Scripts/python tools/ci/run_checks.py --profile pr
```

On POSIX systems activate/use `.venv/bin/python` instead. This repository's
Blender validation is intentionally pinned to the Windows path above; it must
not discover Blender from `PATH` or fall back to another installation.

## Boundaries

- Pure semantic/export code lives in `miku/` and is testable without Blender.
- Blender API/UI/file export belongs in `miku_blender/`; installable extension
  entry points live under `extensions/miku_semantic_exporter/` and
  `extensions/miku_gpl_bake_worker/`.
- Unity Editor/runtime code lives in
  `unity/Packages/com.miku.shaderconverter/`.
- Public schemas remain under `schema/`.
- `b2u_mvp*`, `addons/b2u_mvp_blender`, and
  `com.b2u.shaderconverter` are retired and must not receive new features.

`dist/` and Blender's installed extension directories are build outputs. Never
patch either as source. Before overwriting an installed extension, save and
close every Blender GUI session.

Unity EditMode validation creates a minimal temporary project from the canonical
package and deletes it in a `finally` block. No populated Unity validation
project is part of the repository. New fixtures need provenance, minimal size,
and redistribution review.

## Dirty worktrees

Never reset, clean, delete, or broadly format a contributor's worktree. Scope
format/diff checks to the files changed for the task. Build archives in a fresh
temporary directory and use allowlists.
