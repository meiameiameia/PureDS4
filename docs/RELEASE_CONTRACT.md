# Product identity and release contract

> **First publication: PureDS4 5.1.0.** The owner authorized publication of
> the exact accepted candidate on 2026-10-03, including making the repository
> public. The evidence and bounded first-release exceptions remain recorded
> below; authorization is not a claim that unobserved scenarios passed.

PureDS4 is an independently maintained DS4Windows derivative by
`meiameiameia`, narrowed to the DualShock 4. Its source is
<https://github.com/meiameiameia/pureds4>.

**The first public release supports the DualShock 4 only.** DualShock 3 is a
planned milestone with no schedule and separate implementation/hardware gates; it does not
block this release, is not claimed by it, and is not offered by its UI. Users
who need any other controller family use
[`hbashton/DS4Windows`](https://github.com/hbashton/DS4Windows).

## Identity and compatibility

### GitHub publishing account

The owner requires public releases and release-asset uploads to use the
authenticated GitHub account `meiameiameia`. Verify the API account before
publication; the SSH identity and Git author/committer do not establish it.
An unexpected API account blocks new publication or uploads. Existing release
authorship is separate from editable release text; changing it must not silently
delete releases, replace assets or alter accepted source/tag identities.

### 5.1.2 frozen candidate acceptance — 2026-10-05

The owner accepted the unchanged clean-source candidate from
`42ab4041d5d7ef40b17b63134cce331e7b2fee53` after the authorized in-place update
from 5.1.1, a real Windows restart without renewed repair, and the requested
USB/Bluetooth readings, gameplay, disconnect/reconnect and app close/reopen
checks. These are owner-reported hardware results on one PC; read-only checks
independently confirmed the changed boot, all 29 installed payload files,
canonical task definitions/current account, Ready marker and live backend ping.
See [hardware evidence](HARDWARE_VALIDATION.md#512-launcher-hotfix--2026-10-05)
and [installer evidence](INSTALLER_VALIDATION_STRATEGY.md#512-installed-candidate-checkpoint--2026-10-05).

The frozen public assets are under `artifacts/candidate-5.1.2-42ab404/` in the
isolated worktree. These replace the preliminary dirty-source packages below:

| Public package | Bytes | SHA-256 |
| --- | --- | --- |
| `PureDS4_5.1.2_Setup_x64.exe` | 190,059,022 | `FD6A49D0BB20FFAE96FBBBC210C8C1A1F2855988B9FFB6211351CEA3EF32E936` |
| `PureDS4_5.1.2_x64.zip` | 122,850,107 | `50E01FB87F88824CD0873C0D710C3A73588629A09E1D21E9481C47B713B4BA3B` |

The published assets are these two packages plus their unchanged
`candidate-identity.json` and `SHA256SUMS.txt`. Tag `v5.1.2` identifies the
frozen source above, not a later documentation-only acceptance commit. The accepted application remains
byte-identical to the application that passed 976 Release tests. No rebuild
or UI changes are part of publication. Clean installation, actual rollback,
interrupted-installer reboot/resume and the reporting user's result remain
unobserved; the specific owner-approved risk exception remains bounded.

The unchanged assets were published as stable/latest
[PureDS4 5.1.2](https://github.com/meiameiameia/PureDS4/releases/tag/v5.1.2)
at `2026-10-06T00:32:43Z` (2026-10-05 21:32:43 in America/Sao_Paulo).
The annotated remote tag was resolved and matches the frozen source. The
acceptance/README commit `d25712173175db318e20ca7adbc94bd1dc2e0551` differs
from that source only in four Markdown documentation files. Its
[CI run](https://github.com/meiameiameia/PureDS4/actions/runs/37393809047)
passed **976 tests**, package-security/input/notice gates, x64 application/ZIP
and installer composition, offline layout planning and the MSI install/repair/
uninstall lifecycle on the disposable Windows runner. That MSI-only lifecycle
does not establish a consumer full-bundle clean installation or failure rollback.
Existing unused-member warnings and hosted-action Node runtime deprecation
warnings remain; neither job failed. Push CI retained no downloadable artifacts.

Unauthenticated requests independently confirmed that the public latest-release
API exposes stable `v5.1.2`, and downloaded all four release assets. Every
download matched its unchanged local SHA-256, including both identity/checksum
sidecars. The version checker's existing latest-release endpoint can discover
this newer stable version; no personal settings were changed to force a check.
The 5.2.0 working files remain separate and untouched. No Reddit message or
announcement was posted by the agent.

#### 5.1.2 public release notes

This is a small launcher fix, not the new UI release.

Setup creates a launcher task with Windows' default priority, but PureDS4
5.1.0 and 5.1.1 expected a different value. That mismatch could make the app
ask to repair game output again after restarting Windows. 5.1.2 accepts the
task setup actually creates, while keeping the path, account and elevation
checks. It also keeps compatibility with the earlier high-priority task.

I checked an in-place update from 5.1.1, a Windows restart without another
repair, and USB/Bluetooth readings, gameplay, reconnecting and app restart
on my DS4 setup. The original reporter's PC hasn't been retested yet, so this
isn't a promise that every setup problem is fixed.

The installer is still unsigned. Clean full-bundle installation, rollback
after a real installation failure and resuming an interrupted installation
after reboot still need validation. Back up your profiles before updating;
don't disable Windows protections to install it.

Download `PureDS4_5.1.2_Setup_x64.exe` for standard setup, or
`PureDS4_5.1.2_x64.zip` for portable use. The portable package still needs the
machine-wide drivers. Updates remain manual; 5.1.1's version checker can notify
you about this release. Controller support, drivers and the bundled .NET
10.0.12 runtime are unchanged.

The packages were built locally from clean source
`42ab4041d5d7ef40b17b63134cce331e7b2fee53`. `candidate-identity.json` records
the build inputs; `SHA256SUMS.txt` lets you check the two package downloads.
See the [installation and recovery guide](https://github.com/meiameiameia/PureDS4/blob/main/docs/INSTALL_AND_ROLLBACK.md)
and [validation record](https://github.com/meiameiameia/PureDS4/blob/main/docs/HARDWARE_VALIDATION.md).

### 5.1.2 launcher hotfix preparation (2026-10-05)

The owner approved preparing a separate **5.1.2** local candidate from the
published `v5.1.1` source, commit `5a14defb05992250b73cb6f2e6bf0b9ffe438952`.
Assembly/file version is **5.1.2.0**; intended distribution names are
`PureDS4_5.1.2_Setup_x64.exe` and `PureDS4_5.1.2_x64.zip`.
On 2026-10-05 the owner additionally authorized committing this hotfix,
updating the existing 5.1.1 installation under `C:\Program Files\PureDS4`
while preserving profiles, and publishing 5.1.2 on GitHub (including the
necessary source push/tag) only after final USB/Bluetooth and Windows-restart
acceptance. The owner specifically accepted unsigned distribution and the
remaining unobserved clean-install/real-rollback risk for 5.1.2, including
possible manual repair. This does not waive known safety defects or the
required exact-candidate acceptance. Published packages remain unchanged.
The source line retains documentation-only commit
`10d058b9b3c5e124a9c397fb96ecadc827567506` after the published 5.1.1 base;
no uncommitted 5.2.0 UI work is included.

Scope is the installer/runtime scheduled-task contract mismatch only: the
installer creates Windows priority 7 (`BelowNormal`), but 5.1.0/5.1.1 runtime
validation required `High`. Accept the installer's default and the already
supported legacy `High` value for both owned launcher definitions. Keep the
executable, arguments, working directory, current-account, enabled-state,
elevation and trigger checks. No existing task is rewritten by this change.
Installer/runtime contract regression tests accompany the fix.

The recurring repair report is consistent with this defect, but the reporter's
machine has not been reproduced. Synthetic task-definition reload is not a
real Windows reboot test. The Windows-style UI and setup-prompt preference
changes remain outside this hotfix, planned separately for 5.2.0.

The backend source/hash, drivers, locked dependencies, SDK `10.0.302` and
bundled Core/Desktop runtime `10.0.12` stay unchanged. Microsoft's
[.NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
was rechecked on 2026-10-05 and still lists runtime `10.0.12` as the latest
servicing patch. The newer SDK feature band is not bundled with this product.

Before publication: freeze clean source and package hashes, complete package
checks, obtain exact-candidate installation and final acceptance, and verify
the affected update/startup/reboot flow. The specific 5.1.2 unsigned/clean-install/
rollback risk decision above does not waive the required real Windows-restart
check. Keep all remaining evidence gaps explicit. Component
`releaseReady` alone does not establish release acceptance.

Local verification on 2026-10-05 passed **976 Release tests**, zero failures
or skips, after updating the exact-version identity assertion to 5.1.2. Live
process audio capture remained explicitly excluded. The nine new launcher
contract cases passed. The first run had 975 passes and one obsolete 5.1.1
identity expectation; its corrected final TRX is
`artifacts/disposable/launcher-hotfix-20261005-01/tests/release-suite-final.trx`.
An initial solution-only restore omitted the test project's assets; an explicit
locked test-project restore without overriding its two locked RIDs resolved
that setup issue without changing dependencies or lockfiles.

The Release solution build succeeded with the existing four unused-member
warnings (eight emissions across WPF compilation). The application, MSI and
Burn bundle composed successfully; both installer builds had zero warnings
and errors. Release-input/signature, managed-notice, launcher-contract,
reboot-boundary and installer-state-machine gates passed. NuGet advisory
queries for the application, tests and both managed setup hosts reported zero
affected dependencies. All three shipped managed components resolve Core and
Windows Desktop runtime packs to 10.0.12. Each of the portable ZIP's 29 files
matches the installer staging manifest by size and SHA-256.

The first preparation produced **dirty-source disposable packages**, not a clean-SHA frozen public
candidate or an accepted daily driver. Paths below are relative to
`artifacts/disposable/launcher-hotfix-20261005-01/` in the isolated
`pure-ds4-hotfix-5.1.2` worktree; the base SHA above does not identify the
complete uncommitted source. At that preliminary checkpoint no app/installer was launched, no live task,
driver or profile was changed, and no commit/push/publication had occurred.

| Preliminary artifact | Bytes | SHA-256 |
| --- | --- | --- |
| `installer/PureDS4_5.1.2_Setup_x64.exe` | 190,074,566 | `72016631BBD41095EB141B9CB711B29C6D9CCC63838C9C2BB5F3704D082A7FAB` |
| `portable/x64/Release/PureDS4_5.1.2_x64.zip` | 122,850,107 | `0D3599F3AAE01E01A2F9D7E259956D125DCD8A3CE193BBF7E8DDD90729B412E6` |

The installer and application are unsigned. Application SHA-256 is
`A997671DAE7F0CB18BEF69969B8998D75761FE2078134087ABEDA8FC7A0B26A0`.
At that preliminary checkpoint, exact-package UI/hardware, full installer
lifecycle and real Windows reboot acceptance were unperformed; deterministic
simulations were not substitutes. Final frozen-candidate acceptance is recorded
above, without changing these historical preliminary hashes.

### 5.1.1 maintenance scope (2026-10-04)

The maintenance release is **5.1.1**, with assembly/file version **5.1.1.0**.
The distribution names are `PureDS4_5.1.1_Setup_x64.exe` and
`PureDS4_5.1.1_x64.zip`. On 2026-10-04 the owner authorized finishing 5.1.1
and publishing it on GitHub, including the source commits/pushes needed for
traceability, conditional on the required final acceptance. Installation and
new release-specific risk exceptions still require their own approval.
Published 5.1.0 artifacts and their evidence remain unchanged.

This is a reliability/security update, not a controller-support expansion:

- Reject malformed USB/IP imports without terminating the backend; restrict
  the API and USB/IP listeners, including overrides, to local IPv4.
- Cancel, drain, and release controller-owned macros before entering native
  physical mode, stopping, or recycling a disconnected input slot. Failed
  cleanup must remain retryable, and must not admit a replacement macro session.
- Preserve virtual-device, bus, port, and output-slot identities until their
  retirement is confirmed. Failed creation cleanup is retained for retry;
  a failed retirement must not announce a successful native/stop transition.
- Write profiles atomically, keep the original on failed saves/renames, and
  report persistence failure instead of emitting success events.
- Migrate the application, tests, and both managed installer hosts to .NET 10
  LTS. Pin SDK `10.0.302` separately from Core/Desktop runtime `10.0.12`;
  use locked dependencies and retain the reviewed Microsoft terms/notices.
- Notify users about newer stable PureDS4 releases through a read-only GitHub
  metadata check. Check on startup at most once per 24 hours, with an opt-out
  and a manual Check action. Never download/install assets or start an updater.

The owner approved local implementation of this notifier on 2026-10-04.
A GitHub publication and a Reddit announcement remain separate decisions;
the owner does not want a second Reddit announcement on the same day.

On 2026-10-04 the owner separately accepted an unsigned 5.1.1 and the limited
residual risk of unobserved consumer-Windows clean installation, real rollback,
and reboot/resume, conditional on final USB/Bluetooth acceptance. A failed
installation may require manual repair. Keep those gaps explicit; do not count
them as passing tests, weaken Windows protections, or deliberately damage the
daily-driver stack. This decision does not authorize installation of the
candidate on the owner's PC. Known safety/integrity defects still hold release.

DS3, DualSense, and per-shortcut absolute mouse destinations for RTS games are
out of this update. The community examples (Dota 2, Red Alert 2, and AoE2)
inform later exploration; no implementation date is promised.

Before release-ready: complete composition checks using the clean-source backend
pin, freeze one source/hash-identified installer and portable candidate,
then batch USB/Bluetooth hardware acceptance on that candidate. That pass must
exercise readings, virtual pad/gameplay, reconnect/restart, and managed → native
→ managed routing. Include cancellation of a delayed held-input macro once;
persistence failures use disposable automated fixtures, never the owner's profile.
Document clean-install, rollback, and reboot/resume evidence or obtain a specific
5.1.1 risk decision; the bounded first-release exceptions are not inherited.
`releaseReady` in the component manifest is necessary, not overall acceptance.

Local builds in `artifacts/validation-5.1.1/` are disposable, dirty-source
verification outputs, not accepted publication candidates or daily-driver installs.
The final backend input was rebuilt from clean commit
`04c10b1e0b5439b649730389673a64918a43578e`; its source, binary and notice
identities are recorded in [provenance](component-provenance.md#511-pinned-inputs-2026-10-04).
This closes the component-source blocker, not the remaining package and owner gates.

Before the release-notification change, local verification on 2026-10-04 passed **934 Release tests** in self-contained
.NET 10.0.12 (live process audio capture explicitly excluded), backend Go tests
and vet, locked restores, release-input/managed-notice regression gates, and
installer/portable composition. Both Core/Desktop packs were verified as
10.0.12 for the application, tests, bootstrapper, and setup-action host. NuGet
advisory queries reported no known vulnerable packages for the application,
tests, or either managed setup host. Installer state-machine, on-demand task,
and USB-IP reboot-boundary simulations passed; these are not real Windows
installation/rollback/reboot observations. The public-input gate correctly
rejects the blocked backend. Hardware/GUI acceptance of these new bytes has
not been performed; no application, installer, driver, or task was activated.

After the notifier implementation, the final application regression on
2026-10-04 passed **967 Release tests**, with zero failures or skips, using
the explicit test project, locked restore, and self-contained .NET 10.0.12.
Live process audio capture remained excluded. New isolated tests cover stable
numeric version comparisons, opt-out/manual checking, persistent daily
throttling, malformed/oversized metadata, HTTP failures, timeout/cancellation,
concurrent checks, cache failures, and detached WPF preference/banner behavior.
The HTTP tests use fake responses; they do not contact GitHub or activate
controllers. This proves neither the final packaged application's live GUI
workflow nor a real installation. No replacement package was composed in this
pass; final-candidate and hardware acceptance remain pending as described above.

### Frozen 5.1.1 candidate — 2026-10-04

The final local candidate was composed from clean application source
`5a14defb05992250b73cb6f2e6bf0b9ffe438952`, with the clean backend pin above.
The complete application regression was repeated with that backend input:
**967 tests passed**, zero failures/skips, with live process capture excluded.
Final advisory queries for the application, tests, and both managed installer
hosts reported zero known vulnerable packages. This is a local Windows build;
no hosted CI validation run or artifact is claimed for this source yet.

Paths below are relative to `artifacts/candidate-5.1.1-5a14def/`:

| Frozen candidate | Bytes | SHA-256 |
| --- | --- | --- |
| `installer/PureDS4_5.1.1_Setup_x64.exe` | 190,015,404 | `0B83EC6A74D1E119D621AAF426D68816C6A732C4D86DBD8D06A5DBB8411F059D` |
| `portable/x64/Release/PureDS4_5.1.1_x64.zip` | 122,849,936 | `87A9E9961E8F33AD5939513EB1E49C0E548825CDCA45655FD1C62C8BD87A6D78` |

`installer/candidate-identity.json` records source, backend, runtime-pack,
signature and file identities; `installer/SHA256SUMS.txt` accompanies it.
Core/Desktop/apphost packs were verified as `10.0.12` for all three managed
components. Both executable product versions are `5.1.1`; setup is unsigned.
Composition passed release-input/signature gates, managed-notice checks, MSI
ICE/Burn builds, package validation, and the existing isolated installer,
on-demand-task and USB-IP reboot-boundary simulations. These simulations are
not real host installation or driver-failure evidence. The portable archive
and installer manifest match all 29 application payload files byte-for-byte,
with no unexpected archive files.

At the initial freeze, this candidate had **not been installed or accepted on
hardware**, pushed, tagged, or published; the installed app was then `5.1.0`.
Do not substitute a rebuilt package or an earlier verification artifact for
these frozen bytes. Next: obtain specific in-place upgrade authority, verify
the installed payload and preservation of Default, then perform the batched
USB/Bluetooth and routing/macro acceptance above. Public documentation updates
may follow separately without relabelling the package's source commit.

The owner subsequently authorized this exact in-place `5.1.0` → `5.1.1`
upgrade on 2026-10-04, preserving the current Default profile and completing
normal Windows elevation personally. A hash-verified copy of Default was
preserved outside the repository; no full-data backup was created. The exact
setup above was opened for the owner to complete. Installation completion,
installed payload identity and the hardware pass are not yet observed.
No deliberate driver-failure injection, rollback or shared-driver removal
is authorized by this upgrade approval.

The owner then reported completion. Read-only verification confirmed `5.1.1`,
all 29 installed payload hashes, unchanged Default, the new canonical backend,
task actions, USB-IP executable/driver identities and ABI, backend API response,
and loopback-only listening. The [installation checkpoint](HARDWARE_VALIDATION.md#511-in-place-upgrade--2026-10-04)
records the evidence and its process-query limitation. Hardware acceptance
and hosted CI remain pending; nothing has been pushed, tagged or published.

The owner subsequently reported passing the requested grouped USB/Bluetooth
readings, virtual-pad/gameplay/rumble, disconnect/reconnect, app restart and
managed/native round-trip checks on these installed bytes. The owner subsequently
authorized a temporary macro profile, which was prepared without changing
Default. The observation script was refused by the existing Windows script
policy; no security settings were changed and no macro result is claimed.
The owner then authorized an executable observation panel; it was prepared
and opened without installation, new dependencies or policy changes.
The owner then passed the normal/cancelled macro check. The observer recorded
a normal 10.009-second F24 hold and one F23 pulse, followed by a cancelled
4.029-second hold and no later F23 pulse. The owner restored Default and closed
the app; saved profile selection and Default hash were checked, the observer
was closed, and the test profile was recoverably archived. See the hardware
checkpoint above. All requested frozen-candidate hardware acceptance is now
complete; no fault-injection scenario is inferred from those passes.

Both clean source commits were pushed under the owner's publication approval.
Hosted validation passed for exact application source in
[CI 37253102643](https://github.com/meiameiameia/PureDS4/actions/runs/37253102643)
and backend source in
[CI 37253100965](https://github.com/meiameiameia/PureDS4-VIIPER/actions/runs/37253100965).
The application run passed tests, advisories, input/notice gates, build/package,
offline layout, and application MSI install/repair/uninstall on Windows Server
2022. The backend run passed tests, vet, build and notices. CI retained no
downloadable artifact, so the accepted local package hashes remain the release
identity. `candidate-identity.json` now links the successful source-validation
run without claiming that CI produced those bytes. Compilation reported
unused-member/local warnings, and Actions reported its Node 20 deprecation;
this is not a warning-free-build claim.

[PureDS4 5.1.1](https://github.com/meiameiameia/PureDS4/releases/tag/v5.1.1)
was then published as latest stable from release `403308929` at
`2026-10-05T02:05:07Z` (2026-10-04 local). The annotated tag `v5.1.1` resolves
to the exact application source above, and both source repositories are public.
All four uploaded assets matched accepted size/hash identities, then passed
anonymous public download verification. No accepted binary was rebuilt or
replaced; 5.1.0 remained untouched. See the
[public distribution checkpoint](INSTALLER_VALIDATION_STRATEGY.md#511-public-distribution-verification--2026-10-04).
The owner-approved unsigned/untested-case limits remain explicit in the public
notes below. No Reddit post, UI-framework migration or upstream contribution
was started by this publication.

Verification outputs before the clean backend source pin:

At that earlier verification point, application changes were uncommitted on source base
`5c890ddcb4d13951b0ca2d21155a2cd81110768a`; backend changes are uncommitted on
`c472717d2eed950d8aab3eab80edc87400a75ae8`. Neither base SHA identifies the
complete changed source as a public candidate.

| Local artifact | Bytes | SHA-256 |
| --- | --- | --- |
| `installer/PureDS4_5.1.1_Setup_x64.exe` | 189,995,918 | `9AB74942751820090FEEC97BD7026D875224A492C35B6C4A3112D9A9FC407297` |
| `portable/x64/Release/PureDS4_5.1.1_x64.zip` | 122,854,090 | `8DCCE06341D2B3950DCC0045EC366FD0EBFAE58FDEE363DDBC1E094FC0C064D2` |

Paths are relative to `artifacts/validation-5.1.1/`. Both layouts contain the
same application bytes. These pre-notifier packages are now stale relative to
the changed application source; replace them after notification verification
and the clean-source backend rebuild, not as an accepted final candidate.

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

On 2026-10-04 the owner completed the repository visibility change, and
[PureDS4 5.1.0](https://github.com/meiameiameia/PureDS4/releases/tag/v5.1.0)
was published from release `402225081` at `2026-10-04T14:07:03Z` as the latest
stable release. The annotated tag resolves to the application source above;
the editorial documentation was committed separately as
`4305d0b35e40a88fdf3223d33c7e85b58cafa7f1`. All four public downloads were
verified without authentication against the accepted sizes and SHA-256 hashes.
No binary was rebuilt or replaced. See the
[publication verification](INSTALLER_VALIDATION_STRATEGY.md#public-distribution-verification--2026-10-04).

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
implementation are complete. The 5.1.1 read-only release notifier is separately
authorized: it reads only public `/releases/latest` metadata from the PureDS4
repository over HTTPS, accepts strictly numeric stable version tags, and opens
only the fixed official Releases page when the user clicks. It neither requires
nor establishes authority to install an unsigned binary. No remote release
Markdown or asset URLs are executed or rendered. Network operations have a
10-second deadline, cancellation, bounded response size, and no redirects.
There is no telemetry or transmission of profiles/controller data; GitHub still
receives ordinary request metadata such as the connection's IP address.

The General setting reuses `CheckWhen`: zero disables automatic checks and a
fresh configuration defaults to 24 hours. Existing opt-outs are preserved;
legacy shorter intervals are clamped to 24 hours (longer intervals up to 30 days
are retained). The tiny `release-check.json` cache in the active PureDS4 data
folder atomically stores the UTC attempt time and last confirmed stable tag;
both successes and failures are throttled across normal restarts. If the cache
is unwritable, automatic requests are suppressed while manual checking remains
available. Offline, invalid, rate-limited, or failed manual checks say they could
not check, never falsely claim the installed build is current. Neither checking
nor dismissing a notice changes controller, driver, profile, or backend state.
5.1.0 installations do not gain notification retroactively and need manual
communication to discover 5.1.1. Inherited DS4Windows release notes are not
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

The self-contained runtime makes security servicing PureDS4's responsibility.
Version 5.1.1 moves the app and managed installer hosts to .NET 10 LTS, with
Core/Desktop/apphost `10.0.12`. Microsoft's
[official .NET 10 metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)
was rechecked on 2026-10-04 and still listed `10.0.12` as the current runtime.
SDK `10.0.302` remains a deliberate reproducible build pin, not a claim to be
the latest SDK. Record the actual .NET and Windows Desktop runtime-pack patch
versions from the candidate's restore assets, and check package advisories
before approval. Microsoft's [.NET 8 support](https://dotnet.microsoft.com/en-us/platform/support/policy)
ends November 10, 2026. The original October 27, 2026 migration checkpoint for
the .NET 8 release is superseded by this accepted .NET 10 candidate; publication
still depends on the remaining gates above. Recheck the current
servicing patch and advisories for every candidate; the historical 8.0.31
minimum in the former CI records the September 2026 baseline, not an evergreen
latest-version claim.
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
Public notes and support/recovery/withdrawal guidance follow below
and in [install/rollback](INSTALL_AND_ROLLBACK.md#support-and-distribution-recovery).
The separate distribution/visibility decision was approved on 2026-10-03:
commit/push the final documentation, expose the tracked repository/history,
and publish the unchanged accepted assets under the source-accurate tag.
Those actions are not authorized by the installation-risk exception alone.

## 5.1.1 publication text

The following text was published with the frozen accepted candidate after
successful hosted source validation and the integrity gates above.
The release tag must point to application source
`5a14defb05992250b73cb6f2e6bf0b9ffe438952`, not a later documentation commit.

### PureDS4 5.1.1

5.1.1 is a maintenance release for the DS4-only Windows x64 fork. It focuses
on safe cleanup, profile saving and runtime maintenance; controller support
and Xbox 360 / DS4 output choices are unchanged.

#### Changes

- Reject malformed USB-IP imports without stopping the backend, and keep its
  API and USB-IP listeners on local IPv4 only.
- Cancel controller-owned macros and release held inputs when switching to
  direct/native use, stopping, or disconnecting a controller.
- Keep track of virtual outputs when cleanup fails, rather than reporting a
  successful removal or reusing the slot prematurely.
- Save profiles atomically and report failed saves/renames without claiming
  success or overwriting the original with an incomplete file.
- Move the app and managed installer components to bundled **.NET 10.0.12 LTS**.
- Add a read-only GitHub release checker, with automatic checks at most once
  per 24 hours, an opt-out in Settings, and a manual **Tools → Updates → Check**.
  Downloads and installation stay manual; there is no automatic updater.

#### Updating and downloads

Use `PureDS4_5.1.1_Setup_x64.exe` to update an existing PureDS4 installation.
Back up your profiles/settings and keep your previous trusted package first.
The portable package is `PureDS4_5.1.1_x64.zip`; it includes the runtime, but
still needs the machine-wide game-output components and drivers. Read its
included terms before use. Compare either download with `SHA256SUMS.txt`;
`candidate-identity.json` records the build and component identities.

**5.1.0 users must download 5.1.1 manually to get the release checker.**
PureDS4 does not use the upstream DS4Windows update channel. Do not run both
apps together; see the [migration and recovery guide](https://github.com/meiameiameia/PureDS4/blob/main/docs/INSTALL_AND_ROLLBACK.md).

#### Validation and limits

The exact candidate completed an in-place 5.1.0 update on one Windows PC,
preserved Default, and matched all 29 installed application files. A DS4 v2
passed USB/Bluetooth input, Xbox 360 output, gameplay/rumble, reconnect/restart
and managed/native round trips. A separate macro check confirmed early release
of a held key and cancellation of the later step when entering native mode.

The installer remains **unsigned, with no verified publisher**. Windows may
warn or block it. A matching checksum establishes identity, not safety;
do not disable Windows protections to install it.

Clean full-bundle installation, real installation-failure rollback,
reboot/resume, physical backend interruption, multiple controllers and
current DS4 speaker/headset audio remain unvalidated. A failed installation
may need manual repair. Automated tests do not replace those hardware checks.
Report problems through [PureDS4 issues](https://github.com/meiameiameia/PureDS4/issues)
with the version, Windows version, controller/connection type and reproduction
steps; remove personal details from any logs you share.

Source: [PureDS4](https://github.com/meiameiameia/PureDS4/tree/5a14defb05992250b73cb6f2e6bf0b9ffe438952)
and [PureDS4-VIIPER](https://github.com/meiameiameia/PureDS4-VIIPER/tree/04c10b1e0b5439b649730389673a64918a43578e).
Upstream credits and GPL-3.0-or-later licensing are unchanged.

## 5.1.0 publication text

The text below was published on 2026-10-04 with the exact accepted candidate.
Preserve its source and artifact identities. Do not rebuild, replace assets or
silently retarget the release to a documentation-only commit.

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
