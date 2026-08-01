# ADR 0004: Independent component SemVer

- Status: Accepted
- Date: 2026-07-22

## Decision

The Blender add-on, Unity UPM package, and MiGR schemas version independently
using SemVer. Compatibility is a tested matrix, not equality of version numbers.

## Consequences

Release tooling reads `bl_info` and `package.json` dynamically. Schema versions
change only with their contract and migration. Changelog/release notes identify
the component and supported pairings.
