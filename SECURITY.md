# Security policy

## Supported versions

The project is pre-1.0. Security fixes are developed against the current default
development line; no older version is presently promised security support.

## Reporting a vulnerability

A private security reporting address or hosting-platform advisory contact has
not yet been configured. **Do not publish exploit details, secrets, private asset
paths, or personal data in a public issue.** If the hosting platform exposes a
private vulnerability-reporting feature, use it. Otherwise, wait for the
repository maintainers to publish a verified private channel.

Release validation fails while this contact is unconfigured. Maintainers must
replace the placeholder in `.github/project-maintainers.yml`, enable private
vulnerability reporting, and update this document before a public release.

Non-sensitive hardening suggestions may use the bug issue template and must be
clearly marked as non-sensitive.

## Security boundaries

Miku JSON, Blender paths, texture paths, bundle manifests, and output names are
untrusted input. Reports involving path traversal, writes outside the selected
project root, unsafe deserialization, command execution, archive traversal,
partial-write corruption, or denial of service are in scope.

The project never requests passwords, API tokens, or proprietary source assets
as part of a report.
