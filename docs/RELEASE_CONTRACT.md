# Product identity and release contract

> **First publication: PureDS4 5.1.0.** The owner authorized publication of
> the exact accepted candidate on 2026-10-03, including making the repository
> public. The evidence and bounded first-release exceptions remain recorded
> below; authorization is not a claim that unobserved scenarios passed.

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
The exact installer bearing this version must satisfy the remaining gates
under the explicitly recorded first-release acceptance decisions.
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
CI labels. A manual candidate is for validation; it neither changes the
component-input readiness decision nor creates or authorizes a public release/tag.

`releaseReady` in `installer/release-inputs.json` covers only the pinned
component inputs. It is necessary, not sufficient, for publication. Runtime
backend-failure acceptance follows the owner-approved deterministic criterion
in [hardware validation](HARDWARE_VALIDATION.md#backend-failure-acceptance-criterion);
physical interruption remains an explicit evidence gap. The installation/recovery
matrix, licensing review, exact final-artifact acceptance and publication
authority remain separate requirements even when the input manifest is ready.

The owner authorized annotated tag `v5.1.0` at the exact application source
`8999108763aa09c5478c8713f7fd62d99cf7ebb5`, not at a later documentation-only
commit. Future tag creation remains a separately authorized publication action.

The owner authorized a validation-only GitHub draft and exact-file upload on
2026-10-02. Draft `402225081` records tag name `v5.1.0`; before the final
publication sequence on 2026-10-03 it was unpublished with no Git tag. Its source,
asset hashes, installed-candidate acceptance and downloaded unsigned-installer
observation are recorded in the
[installer checkpoint](INSTALLER_VALIDATION_STRATEGY.md#evidence-checkpoint--2026-10-03).
Draft/upload authorization alone did not permit publication. On 2026-10-03
the owner separately approved committing/pushing the final documentation,
making `meiameiameia/PureDS4` public with its tracked history, creating the
source-accurate tag and publishing this draft with its four existing assets,
conditional on exposure and integrity checks. Publication was paused for the
owner's editorial README review and then explicitly resumed. Later
documentation commits must not relabel these binaries as built from a
different source revision.

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

The exact draft download was checked on 2026-10-03: its hash matched, its
Internet-zone marker was preserved, and SmartScreen warned before launch,
identifying the app and "Unknown publisher". Record that as observed warning
behavior, not malware detection, a warning-free launch, or a promise of
compatibility with stricter Windows policy. The accepted local in-place update
is separate evidence; there is no need to reinstall merely to repeat it.

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

The replacement flow, task-ownership checks, exact candidate hashes and
owner-observed in-place update have evidence in the linked contracts. The
downloaded unsigned candidate produced the SmartScreen warning documented
above. Those observations do not complete the consumer-Windows clean full-bundle,
failure/rollback and reboot/resume matrix. On 2026-10-03 the owner accepted
that specific residual risk for preparation of the first `5.1.0`; the bounded
exception is recorded in the installer strategy, not as passed testing.
Current runtime/advisory and component-input checks completed on that date.
Prepared public notes and support/recovery/withdrawal guidance follow below
and in [install/rollback](INSTALL_AND_ROLLBACK.md#support-and-distribution-recovery).
The separate distribution/visibility decision was approved on 2026-10-03:
commit/push the final documentation, expose the tracked repository/history,
and publish the unchanged accepted assets under the source-accurate tag.
Those actions are not authorized by the installation-risk exception alone.

## Prepared 5.1.0 publication text

This is proposed publication content, not an announcement that publication has
occurred. Preserve the exact candidate files and their source SHA when transferring
it to the GitHub release. Do not rebuild, replace assets or silently retarget the
release to a documentation-only commit.

### PureDS4 5.1.0

PureDS4 is a Windows x64 controller tool focused on the DualShock 4, derived
from DS4Windows. It offers Xbox 360 and DualShock 4 game output. DualShock 3
is planned, but is not supported in this release. Other controller families
are not offered.

The standard installer includes the application, a pinned minimal
PureDS4-VIIPER backend, USB-IP and the recommended HidHide package for offline
setup. VIIPER has no tray icon, independent updater or Windows logon trigger;
PureDS4 requests its on-demand launcher when game output needs it. Existing
HidHide installations are detected; controller protection is checked when a
controller connects. Installing drivers can require Windows elevation or a
restart.

Use `PureDS4_5.1.0_Setup_x64.exe` for standard installation. The portable ZIP is
a separate application distribution, not a way to avoid machine-wide driver
requirements. Read `PORTABLE-TERMS.txt` and the included agreements before
using it. Setup presents the separate Microsoft component terms offline;
PureDS4 itself remains GPL-3.0-or-later, with its upstream credits preserved.

PureDS4 replaces DS4Windows; simultaneous operation is not supported. Close
the predecessor and follow the
[migration and recovery guide](https://github.com/meiameiameia/PureDS4/blob/main/docs/INSTALL_AND_ROLLBACK.md).
Profile import copies game profiles without modifying their originals; it
does not import every predecessor setting or special action. Back up your
profiles/settings and retain your previous trusted installer before changing
an existing installation. Updates to PureDS4 are manual; this release does
not use the upstream DS4Windows update channel.

#### Security and validation limits

This installer is **unsigned and has no verified publisher**. The exact browser
download produced a SmartScreen "Windows protected your PC" / "Unknown
publisher" warning. Check the source and SHA-256; a matching hash proves
identity, not that a file is harmless. Windows policy can warn or block it.
Do not disable Windows protections to install PureDS4.

Owner testing covered DS4 USB/Bluetooth input, virtual output, gameplay and
normal reconnection on earlier runtime-equivalent candidates. The exact
distributed installer completed an in-place update on the owner's PC,
preserved Default, and matched all 29 installed application payload files.
CI covered the application MSI lifecycle on Windows Server 2022. This is
one-machine hardware evidence, not universal compatibility certification.

Clean full-bundle installation, real installation-failure rollback and
reboot/resume on Windows 10/11 remain **unobserved**. The owner accepted this
limited first-release risk, including possible manual repair after a failed
installation. Deterministic recovery tests are not real driver/reboot tests;
physical backend interruption also remains unobserved. Report failures through
[PureDS4 issues](https://github.com/meiameiameia/PureDS4/issues), with sanitized
details as described in the migration/recovery guide. Do not post personal
profiles, controller identifiers or unsanitized logs publicly.

The included self-contained .NET runtime is `8.0.31`; Microsoft ends .NET 8
support on November 10, 2026. PureDS4 must ship its own runtime updates. The
maintenance checkpoint is October 27, 2026: validate migration to a supported
runtime or stop distributing this .NET 8 build before support ends. This is
not a promise of an automatically scheduled update or monitoring service.

#### Exact files and source

- `PureDS4_5.1.0_Setup_x64.exe`: 191,378,182 bytes; SHA-256
  `75B3F8516870A9473AF8E1684C00AFE1446067327AA642497C22EBBB68AE58F5`.
- `PureDS4_5.1.0_x64.zip`: 119,209,741 bytes; SHA-256
  `FBD02D7EE9FFE0A184BC0E10AB7CCF2670E1B2ED2DE916ACDAB1BAC5F0A98AEE`.
- `SHA256SUMS.txt` and `candidate-identity.json` accompany both files. The
  candidate identity correctly records a local Windows build and unavailable
  CI artifacts; it is not a claim that the download came from hosted CI.
- Application source:
  [8999108763aa09c5478c8713f7fd62d99cf7ebb5](https://github.com/meiameiameia/PureDS4/tree/8999108763aa09c5478c8713f7fd62d99cf7ebb5).
- Included VIIPER source:
  [c472717d2eed950d8aab3eab80edc87400a75ae8](https://github.com/meiameiameia/PureDS4-VIIPER/tree/c472717d2eed950d8aab3eab80edc87400a75ae8),
  version `0.1.0-pureds4.1`. This backend ships with PureDS4 releases, not a
  separate end-user update channel.

Confirm public access to corresponding source and guidance before publishing,
and verify the public downloads immediately afterward. Publication/tag authority
must cover the exact candidate and distribution actions described above.
