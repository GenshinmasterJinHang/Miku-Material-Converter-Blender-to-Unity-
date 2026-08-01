# Governance

B2U/Miku is currently in bootstrap governance. Technical decisions follow the
engineering constitution, reviewed ADRs, compatibility evidence, and tests.

## Roles

- **Maintainers** approve releases, security handling, breaking changes, licenses,
  and governance changes.
- **Contributors** propose and implement reviewable changes under the project
  contribution and conduct policies.

No maintainer account or team is yet configured in the repository. The canonical
placeholder is `.github/project-maintainers.yml`; it must not be interpreted as
an assignment of ownership. Releases fail closed until at least one real owner
and a verified private security channel are configured.

## Decisions and releases

Breaking public API/schema changes, licensing changes, and target-backend
architecture require an ADR plus maintainer approval. Component versions follow
independent SemVer. Release candidates must pass the release profile, be built
deterministically from allowlists, and be published manually by an authorized
maintainer.

If maintainers disagree, preserve the safer compatible behavior and record the
open decision. Governance changes are made through normal review and documented
in the changelog.
