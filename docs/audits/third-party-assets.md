# Third-party and restricted-content audit

- Audit date: 2026-07-22
- Status: preliminary; public release rights review remains blocked

This inventory records obvious classes of non-original or unverified content. It
does not transfer rights and is not exhaustive. Files remain untouched in the
working tree but are excluded from B2U/Miku release archives.

## Confirmed restricted content

The directory/archive named for the `陈_佩丽卡_莱万汀_伊冯` character package
contains an RM/readme dated 2025-10-29. Its terms were read as allowing free,
non-commercial/fan use with attribution to `新杨XIYAG` while prohibiting sale.
Those terms are incompatible with treating the asset as MIT project content or
including it in a general-purpose open-source release.

Disposition: retain only as user-owned local material; exclude the directory,
ZIP, extracted models/textures, and derivatives from all release artifacts.

## Unreviewed content classes

| Repository examples | Rights evidence | Disposition |
| --- | --- | --- |
| `Zanni_...` directory and ZIP | Not established | Exclude |
| `【ZGabriel学习存档】...` directory, RAR, and FBX | Not established | Exclude |
| `材质库/` and `材质库.zip` | Not established | Exclude |
| `菲比-角色渲染...` directory and ZIP | Not established | Exclude |
| Root and nested `.blend`, `.blend1`, `.fbx` | Mixed/unknown | Exclude unless separately reviewed |
| Game/reference textures and model exports under samples/outputs/Generated/nodeoutput | Mixed/unknown | Exclude from code releases |
| `unityproject/`, `unityverify053/`, `unityverify060/` generated Assets/Library/logs | Verification data plus unknown asset rights | Exclude from release packages |
| Existing archives under `dist/` and `unity/dist/` | Historical build provenance not fully verified | Do not republish; build new candidates from allowlists |
| PPTX and research/reference documents | Mixed/unknown | Exclude from software packages |

## Package/dependency terms

- glTFast 6.19.0 declares Apache-2.0.
- URP 17.4.0 and Shader Graph 17.4.0 use Unity Companion License terms; URP
  includes its own third-party notices.
- Unity's Newtonsoft JSON package is distributed under Unity package terms and
  carries MIT notices for Json.NET components.
- MCP for Unity is a development-only Git dependency pinned to an inspected
  commit; its upstream terms must be reviewed before redistributing it.
- Python development dependencies and observed optional libraries are listed in
  the root notice file.

The Unity package also contains `Runtime/Endfield/MyZmdSource`, whose README says
the HLSL/shader files are verbatim copies from `qiudashu233/MyZmdShaders` commit
`650745732f8251db150744eab60eb01613d1ecc8`. The local directory has provenance
but no license text. It and related formula adaptations require explicit license
review; the verbatim directory is excluded from release candidates.

Unity dependencies are references resolved by Package Manager and are not copied
into the deterministic B2U UPM archive.

## Release policy

The release builder accepts only explicit project-code roots and known metadata
files. It rejects symlinks, traversal, absolute archive paths, and restricted
binary/model/texture extensions. It never packages the repository root wholesale.

Before public release, maintainers must:

1. identify the owner and provenance of every intended fixture/template;
2. document license/attribution and redistribution rights;
3. remove or segregate any content without suitable rights (requires explicit
   maintainer authorization because current files are user-owned);
4. rerun release validation and inspect archive manifests; and
5. retain required upstream license/notice files.
