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
runtime activation while the old application's process, executable,
uninstall registration, or scheduled tasks can still compete are implemented,
test-covered, and run on every launch, not only the first one. Preserved
configuration, empty install directories, and historical registry residue
remain visible without being misclassified as an active runtime. PureDS4's
own configuration path is enforced never to resolve to the old product's
configuration directory or a descendant; read-only import of game profiles
is available from the Tools tab, and name conflicts receive deterministic
DS4Windows-suffixed copies rather than losing the predecessor's profile.
Separately, `ViiperSetupManager` already refuses to
start PureDS4's own VIIPER against a foreign viiper.exe running from a
different path (including an old installation's own copy) on every launch —
this predates the replacement flow's naming but satisfies the same
ownership requirement for virtual-output activation. A removal plan — listing
exactly what a full removal would involve, built from the same detection
survey — is available from the Tools tab, and can start the predecessor's own
registered uninstaller after an explicit confirmation. PureDS4 never removes
another product's files, registry keys, or tasks itself: Windows Installer
owns the elevation, transaction, and rollback, and PureDS4 declines to start
any command it cannot read as an uninstall rather than guessing. Recognized
MSI removal commands require the exact `/x` verb and a product code or MSI
path; arbitrary `/x...` prefixes are rejected. Auto
Profiles/Actions/Controller Configs import is not implemented yet. The owner
observed the uninstall hand-off complete against the local beta.2 dogfood
installation on 2026-08-30; that one-machine observation does not replace the
clean-install, rollback, failure, and recovery matrix required for release.

## Version contract

The owner selected **`5.1.0` as the target for the first public release**.
This is a version decision, not a readiness claim or permission to publish.
The exact installer bearing this version must pass the remaining gates.
An internal `5.1.0` candidate has already upgraded the owner's former
`5.1.0-beta.2` dogfood installation, and a later candidate replaced it at
the same version. Neither observation authorizes publication.

| Surface | Value |
| --- | --- |
| Public/display version | `5.1.0` |
| Burn bundle version | `5.1.0` |
| MSI product version | `5.1.0` |
| Assembly and file version | `5.1.0.0` |

Future public versions use `MAJOR.MINOR.PATCH`: increment PATCH for compatible
fixes, MINOR for compatible features, and MAJOR for intentional incompatible
changes. Public prereleases, if deliberately offered, append `-beta.N` or
`-rc.N` to the target version; the stable version follows those prereleases.
This is a product-release convention, not a claim of a stable programming API.
Release-candidate validation builds retain the target version and are
distinguished by their complete source SHA and artifact SHA-256, not by an
invented public version or a filename such as `beta.3`.

MSI compares only the first three numeric version fields. The former
`5.1.0-beta.2` MSI already used `5.1.0`; its upgrade to an internal stable
candidate and the later same-version replacement both passed on the owner's
machine. The eventual published installer must be the exact bytes finally
accepted. The Burn bundle uses the prerelease-aware version for
upgrade ordering; a separate test or CI label must not silently alter that
ordering. Later public PATCH/MINOR/MAJOR releases must advance the MSI numeric
version too. CI builds may use a `5.1.0-ci.<run>` Burn version and a separate
human-readable artifact label, but CI labels are never public releases.

The manual `CI Build` input `release_candidate` selects the target-version
packaging path. It retains `5.1.0` for the first release's display, Burn,
and MSI identity, and records `kind: release-candidate` alongside the full
source SHA and file hashes in `candidate-identity.json`. Ordinary runs retain
CI labels. A manual candidate is for validation; it neither clears
`releaseReady: false` nor creates or authorizes a public release/tag.

No release tag has been created or authorized. The tag spelling and creation
remain a separate owner-approved publication action.

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
The latter is an elevated, triggerless launcher: Windows must not start VIIPER
independently at login. PureDS4 runs the verified task during its own
readiness/output flow. `RunPureDS4` alone may have a logon trigger when the owner
enables app startup.
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

The owner accepts an **unsigned first public release**. Authenticode signing
remains supported for a later release; `installer/build-installer.ps1
-RequireSigning` fails closed when signing material is missing. An unsigned
artifact must be identified by its exact version and published SHA-256, and
its download instructions must say plainly that it has no verified publisher.
This is not a promise that SmartScreen or Smart App Control will allow it:
the exact downloaded candidate must be exercised with normal Windows
protections enabled. Users must not be told to disable security controls.

The self-contained .NET 8 payload makes runtime security servicing PureDS4's
responsibility. Record the actual .NET and Windows Desktop runtime-pack patch
versions from the candidate's restore assets, and check package advisories
before approval. Microsoft's [.NET 8 support](https://dotnet.microsoft.com/en-us/platform/support/policy)
ends November 10, 2026. The maintenance checkpoint is October 27, 2026:
by then, validate a supported-runtime migration candidate or plan to stop
distributing the .NET 8 build before end of support. Recheck the current
servicing patch and advisories for every candidate; the 8.0.31 minimum in CI
records the September 2026 baseline, not an evergreen latest-version claim.
A pinned SDK version alone is not evidence of the candidate's embedded
runtime patch.

## Release blockers outside this identity gate

Component hashes, source revisions, notices, signers, and NuGet locks are now
enforced by the release-input contract described in
[`component-provenance.md`](component-provenance.md). Every component that
contract still declares is verified. The one unresolved case, the inherited
FakerInput wrapper binaries, was settled by removing that integration rather
than by shipping binaries whose redistribution scope could not be established.

The remaining release program must also implement the explicit replacement
flow described above, enforce task ownership in installer and portable flows,
publish exact artifact hashes and implement the unsigned-release checks above,
and validate an authorized in-place upgrade on a disposable or owner-approved
Windows installation.
