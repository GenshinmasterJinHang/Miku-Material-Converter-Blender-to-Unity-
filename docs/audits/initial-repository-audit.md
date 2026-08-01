# Initial repository audit

- Audit date: 2026-07-22
- Branch: `task-1.4-miku-fixtures`
- Scope: read-only pre-implementation inspection plus foundation baseline

This audit separates evidence from inference and unverified claims. It is not a
legal opinion or a compatibility certification.

## Confirmed facts

### Repository state

- The pre-task inspection counted 383 modified, 2 deleted, and 16,292 untracked
  worktree entries. At implementation start, Git counted 383 modified, 2 deleted,
  and 16,300 untracked entries including the newly added constitution and files
  created by earlier test reconnaissance. The repository was already highly
  dirty; these entries are treated as user-owned.
- There is no configured Git remote and no tag.
- At audit start there was no root `AGENTS.md`, `PLANS.md`, `.github/` CI,
  `LICENSE`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`,
  `GOVERNANCE.md`, `CHANGELOG.md`, or `ROADMAP.md`.
- The repository includes large models, textures, archives, Unity-generated
  outputs/logs, release archives, and binary presentation/reference material.

### Modules and public surfaces

- `b2u_mvp` contains largely pure-Python exporter/semantic logic.
- `b2u_mvp_blender` imports `bpy` and provides Blender UI/integration. The
  `addons/b2u_mvp_blender` package is a discovery wrapper.
- `unity/Packages/com.miku.shaderconverter` contains Unity Editor/runtime code,
  public C# types, scripted importers, ShaderLab/HLSL templates, and package
  metadata.
- Package ID, C# namespaces/public types, Blender operators/settings, generated
  shader property references, `.miku`/`.b2ubundle` formats, and schema paths are
  compatibility surfaces.
- The current editable shader utility writes ShaderLab `.shader` text and
  preserves an existing editable file unless explicit regeneration is requested.
  No `.shadergraph`/`.shadersubgraph` serializer exists in production code.

### Versions and dependencies

- Both Blender add-on entry points declare version 0.5.0 and minimum Blender
  4.0.0.
- No Blender executable was found on `PATH`; Blender 4.x and 5.0 were not run.
- The Unity project records Editor 6000.4.5f1.
- The Unity project/package resolves URP 17.4.0 and Shader Graph 17.4.0.
- Unity package source is 0.8.1. The newest checked-in Unity package tarball
  observed in `unity/dist` is 0.7.0. Blender release ZIPs exist through 0.5.0.
- The Unity package declares glTFast 6.19.0, URP 17.4.0, and Newtonsoft JSON
  3.2.1; the verification lock resolves Newtonsoft JSON 3.2.2.
- The Unity verification project referenced MCP for Unity from `#main`; its lock
  recorded commit `c14de1e6dc01ab42d2bb358730cff954bce0ce6b`.

### Schemas and generated data

- `schema/miku-1.0.schema.json` requires `version: "miku-1.0"` and allows
  companion `schema: "miku-preset-1.0"`.
- `schema/` contains the main/companion schemas while `schemas/miku_v2.json`
  describes a separate strict overlay with numeric `schemaVersion: 2`.
- Blender integration currently injects `schemaVersion = 2` into newly assembled
  graphs for NPR features while the same document retains `version: miku-1.0`.
- The exporter previously generated ordinal node IDs such as
  `node_002_output` and inserted the current UTC timestamp into metadata by
  default, causing unrelated identity churn and time-dependent diffs.

### Tests and tooling

- There are 37 Python test files and 318 discovered test methods in the baseline.
- A pre-implementation run produced 4 failures and 21 skips. The failures were
  stale expectations for bundle `objects`, Endfield FaceSDF channels, package
  version 0.7.0, and Unlit Shader output.
- Two .NET 8 harness projects exist.
- Six Unity verification C# files existed as static `Run()` routines, not a
  discoverable NUnit EditMode assembly.
- Blender headless scripts exist under `tests/blender`.
- All inspected JSON schemas parsed as JSON; 64 inspected Python files parsed as
  AST in the baseline.
- Repository-wide `git diff --check` found pre-existing trailing whitespace in a
  dirty Unity scene; new checks must be scoped away from generated/user YAML.

## Code inferences

- The current dual version fields can confuse consumers because a Miku core
  version and an NPR overlay version are not clearly named as separate layers.
  This warrants a future versioned schema design, not an in-place rewrite.
- Broad public C# models/compiler/emitter types increase compatibility risk;
  foundation work should add tests without renaming them.
- Direct writes in Blender and Unity can leave partial assets after interruption.
  Atomic replacement at file boundaries is needed.
- Unconstrained socket `space` strings and limited stage information allow
  semantically invalid graphs unless validation is strengthened.
- The worktree content and explicit non-commercial terms make repository-wide
  release archives unsafe. An allowlist is required.

## Unverified claims

- Blender 4.0+ operation: declared, not executed during this audit.
- Blender 5.0 operation: target, not executed.
- Unity 6000.4.5f1 end-to-end conversion: project configured, EditMode validation
  pending at audit time.
- Any Unity/URP/Shader Graph tuple other than 6000.4.5f1/17.4.0/17.4.0: Unknown.
- Visual equivalence of specialized game shaders: not established by the
  foundation test suite.
- Redistribution rights for models, textures, reference projects, and archives:
  not established; one package is explicitly restricted.
- Shader Graph editable asset support: not implemented and must not be claimed.

## Risks and disposition

| Risk | Foundation disposition |
| --- | --- |
| Dirty user worktree | Touch only scoped foundation/source/test files; record counts and final diff |
| Restricted assets | Do not delete; exclude by release allowlist; block public release pending rights review |
| No maintainer/security contact | Comment-only CODEOWNERS; release check fails closed |
| Version drift | Read versions from source; compatibility matrix; deterministic builder |
| ShaderLab mistaken for Shader Graph | Label legacy in README/architecture/roadmap |
| Dual schema concepts | Map/document and validate; no wire upgrade in this task |
| Editor tests not discoverable | Add real NUnit EditMode assembly and batch-mode entry point |
