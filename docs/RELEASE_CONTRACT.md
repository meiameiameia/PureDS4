# Product identity and release contract

> **No release is planned.** `hbashton/DS4Windows` is the maintained public
> DS4Windows. This repository is a personal derivative whose work reaches
> users through upstream pull requests, not through its own distribution.
> This document defines the identity and compatibility rules the tree follows
> so that versioning, upgrade paths, and installer behaviour stay coherent for
> the owner's own installs — it does not describe a publication plan.

DS4Windows Reworked is an independently maintained derivative by
`meiameiameia`. Its source is
<https://github.com/meiameiameia/ds4windows-reworked>.

## Identity and compatibility

The public product name is **DS4Windows Reworked**. Application metadata,
window titles, installer presentation, package names, release artifacts, and
first-party links use that identity. Historical copyright, GPL notices,
upstream contributors, and third-party attribution remain visible.

The first release remains an in-place successor to the inherited hbashton
installer lineage. These compatibility identifiers intentionally remain
unchanged for the first release:

- `DS4Windows.exe`, the application assembly, and code namespaces;
- profile and settings formats, including their `DS4Windows` XML roots;
- `%AppData%`, `%LocalAppData%`, `%ProgramData%`, and `%ProgramFiles%`
  directories named `DS4Windows`;
- `HKLM\Software\DS4Windows`;
- `RunDS4Windows` and `RunVIIPER` task names;
- IPC, mutex, and window-discovery identities;
- the existing MSI and Burn `UpgradeCode` values.

Changing any preserved identity requires a separately designed migration and
coexistence contract. Reworked does not claim side-by-side operation with
another DS4Windows mapper.

## Version contract

The first public candidate is `5.1.0-beta.1`:

| Surface | Value |
| --- | --- |
| Public/display version | `5.1.0-beta.1` |
| Burn bundle version | `5.1.0-beta.1` |
| MSI product version | `5.1.0` |
| Assembly and file version | `5.1.0.0` |
| Future release tag | `v5.1.0-beta.1` |

MSI ordering uses the three numeric fields. Burn carries the prerelease label
so a future `5.1.0` stable bundle orders above `5.1.0-beta.1`. CI builds use a
`5.1.0-ci.<run>` Burn version and a separate human-readable artifact label.

Public artifacts use the `DS4Windows-Reworked_<version>_<kind>_x64` naming
family. The executable remains `DS4Windows.exe` for compatibility.

## Upgrade and host infrastructure

Keeping the existing upgrade codes means a standard Reworked installation can
replace an installed package from the inherited lineage. A real-machine
upgrade is destructive to the outgoing installed application and therefore
requires explicit owner authorization during validation.

`RunDS4Windows` and `RunVIIPER` are persistent host infrastructure. Runtime,
portable packages, dogfood promotion, cleanup, and release automation must not
create or retarget them. A fresh, explicitly initiated standard installation
may create canonical installer-owned tasks. An upgrade must preserve already
canonical tasks. The installer gate must enforce and validate those rules
before a public package is authorized.

## Update and release authority

The inherited hbashton release feed is not an update or release-notes authority
for Reworked. Automatic product updates and live release-note retrieval remain
disabled until an independent signed channel, rollback design, and prerelease
ordering implementation are complete. The application does not solicit
donations for an upstream maintainer under the Reworked identity.

Creating a tag or GitHub Release does not authorize publication by itself.
Public release automation must fail closed without required signing material,
complete provenance, tests, package validation, and explicit owner approval.

## Release blockers outside this identity gate

Component hashes, source revisions, notices, signers, and NuGet locks are now
enforced by the release-input contract described in
[`component-provenance.md`](component-provenance.md). Public composition still
fails closed because the inherited FakerInput wrappers have unconfirmed
license scope. The evidence supports their relationship to the MIT-licensed
FakerInput family and GPL DS4Windows lineage, but it does not establish their
precise standalone terms; that decision must be recorded before distribution.

The remaining release program must also enforce task ownership in installer
and portable flows, implement the signed release workflow, and validate an
authorized in-place upgrade on a disposable or owner-approved Windows
installation.
