# Product identity and release contract

> **Publication is intended but not scheduled.** Nothing in this document is a
> readiness claim or a date. It defines the identity and compatibility rules
> the tree follows so that versioning, upgrade paths, and installer behaviour
> stay coherent, and so a public release can later be judged against a written
> contract rather than against whatever the tree happened to contain.

PureDS4 is an independently maintained DS4Windows derivative by
`meiameiameia`, narrowed to the DualShock 4. Its source is
<https://github.com/meiameiameia/pureds4>.

**The first public release supports the DualShock 4 only.** DualShock 3 is a
planned milestone with no schedule and its own gate in `AGENTS.md`; it does not
block this release, is not claimed by it, and is not offered by its UI. Users
who need any other controller family use
[`hbashton/DS4Windows`](https://github.com/hbashton/DS4Windows).

## Identity and compatibility

The public product name is **PureDS4**. Application metadata, window titles,
installer presentation, package names, release artifacts, and first-party links
use that identity. Historical copyright, GPL notices, upstream contributors,
and third-party attribution remain visible.

PureDS4 owns every name it claims on the host. `PureDS4/ProductIdentity.cs` is
the single definition for the application-side names, and
`PureDS4.Tests/ProductIdentityContractTests.cs` holds them to it:

- executable `PureDS4.exe` and assembly `PureDS4`;
- install root `%ProgramFiles%\PureDS4` and registry root
  `HKLM\Software\PureDS4`;
- `%AppData%`, `%LocalAppData%`, `%ProgramData%`, and `%Temp%` directories
  named `PureDS4`, including the installer log root
  `%ProgramData%\PureDS4\Installer`;
- scheduled tasks `RunPureDS4` and `RunPureDS4VIIPER`;
- the per-user Startup shortcut `PureDS4.lnk`;
- IPC, mutex, single-instance, and window-discovery identities, all prefixed
  `PureDS4`;
- MSI `UpgradeCode` `FE00C21E-C0E6-4509-81EA-2D4B34D377A1` and Burn
  `UpgradeCode` `E0DC119F-B6A6-4425-8FEB-FAA6232FE346`, both distinct from the
  inherited lineage so a PureDS4 package can never upgrade over, or remove,
  another product.

Two names are deliberately *not* on the PureDS4 identity, and must not be
changed without a migration:

- the `DS4Windows` XML roots and element names inside profile and settings
  files, and the internal C# namespaces, which are format and source-level
  compatibility rather than owned host resources;
- `Global\DS4Windows-Reworked-HidHide-Blacklist`, the machine-wide lock that
  serialises HidHide blacklist mutation. The blacklist is one shared resource,
  so a per-product lock would let PureDS4 and a not-yet-removed predecessor
  corrupt it concurrently.

## Replacement, not coexistence

PureDS4 replaces DS4Windows or an earlier DS4Windows Reworked installation on a
machine. Simultaneous installation or runtime coexistence is not a supported
outcome, and PureDS4 does not claim side-by-side operation with another
DS4Windows mapper.

Unique identities are still required, for three reasons that survive the
absence of coexistence: they stop a Windows Installer package from silently
upgrading over a different product, they make it possible to audit and remove
exactly what PureDS4 owns, and they let a failed transition be recovered
without two products competing for the same files and handles.

Ordinary preference changes must never adopt, rename, retarget, or delete
another product's shortcuts, tasks, configuration, or backend. Detecting the
old product, requiring it to close, preserving or archiving its configuration,
importing profiles read-only, and removing it belong to an explicit,
owner-visible replacement flow. Read-only detection and a gate that blocks
while the old application's process is running are implemented,
test-covered, and run on every launch, not only the first one; PureDS4's own
configuration path is enforced never to resolve to the old product's
configuration directory; and read-only import of game profiles is available
from the Tools tab. Separately, `ViiperSetupManager` already refuses to
start PureDS4's own VIIPER against a foreign viiper.exe running from a
different path (including an old installation's own copy) on every launch —
this predates the replacement flow's naming but satisfies the same
ownership requirement for virtual-output activation. Auto Profiles/Actions/
Controller Configs import and uninstall/rollback/recovery are not
implemented yet.

## Version contract

The version line the tree currently carries is `5.1.0-beta.1`. It records
identity only. The owner has not chosen the first public PureDS4 version or a
publication policy, and this table must not be read as one:

| Surface | Value |
| --- | --- |
| Public/display version | `5.1.0-beta.1` |
| Burn bundle version | `5.1.0-beta.1` |
| MSI product version | `5.1.0` |
| Assembly and file version | `5.1.0.0` |

MSI ordering uses the three numeric fields. Burn carries the prerelease label
so a future `5.1.0` stable bundle orders above `5.1.0-beta.1`. CI builds use a
`5.1.0-ci.<run>` Burn version and a separate human-readable artifact label.

No release tag is defined. Choosing the first public version, and the tag that
names it, is an owner decision that has not been made.

Public artifacts use the `PureDS4_<version>_<kind>_x64` naming family, and the
installed executable is `PureDS4.exe`. The publish layout carries a
`PureDS4.release` marker that `utils/validate-installer.py` checks against the
package manifest.

## Upgrade and host infrastructure

The PureDS4 upgrade codes are distinct from the inherited lineage, so a PureDS4
package upgrades only over an earlier PureDS4 package. Removing a predecessor
product is a separate, explicitly authorized step, never a side effect of
installing PureDS4. A real-machine upgrade is destructive to the outgoing
installed application and therefore requires explicit owner authorization
during validation.

`RunPureDS4` and `RunPureDS4VIIPER` are persistent host infrastructure.
Runtime, portable packages, dogfood promotion, cleanup, and release automation
must not create or retarget them. A fresh, explicitly initiated standard
installation may create canonical installer-owned tasks. An upgrade must
preserve already canonical tasks. The installer gate must enforce and validate
those rules before a public package is authorized.

## Update and release authority

The inherited hbashton release feed is not an update or release-notes authority
for PureDS4. `UpdateAuthorityPolicy` keeps automatic product updates and live
release-note retrieval disabled, and no upstream release feed is queried, until
an independent signed channel, rollback design, and prerelease ordering
implementation are complete. Inherited DS4Windows release notes are not
presented as PureDS4 notes. The application does not solicit donations for an
upstream maintainer under the PureDS4 identity.

Creating a tag or GitHub Release does not authorize publication by itself.
Public release automation must fail closed without complete provenance, tests,
package validation, and explicit owner approval.

**Code signing is an open owner decision.** The build supports Authenticode
signing and `installer/build-installer.ps1 -RequireSigning` fails closed when
signing material is missing, but whether a public PureDS4 artifact must be
signed has not been decided. Nothing in this repository should be read as
committing to signed releases, or as ruling them out.

## Release blockers outside this identity gate

Component hashes, source revisions, notices, signers, and NuGet locks are now
enforced by the release-input contract described in
[`component-provenance.md`](component-provenance.md). Public composition still
fails closed because the inherited FakerInput wrappers have unconfirmed
license scope. The evidence supports their relationship to the MIT-licensed
FakerInput family and GPL DS4Windows lineage, but it does not establish their
precise standalone terms; that decision must be recorded before distribution.

The remaining release program must also implement the explicit replacement
flow described above, enforce task ownership in installer and portable flows,
settle the signing policy and implement whatever release workflow it implies,
and validate an authorized in-place upgrade on a disposable or owner-approved
Windows installation.
