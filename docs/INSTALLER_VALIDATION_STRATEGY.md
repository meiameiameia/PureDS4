# Installer validation and recovery strategy

The PureDS4 installer is treated as a transaction with one owner and one
commit point. A package is not considered installed merely because its child
process returned zero; the complete PureDS4, VIIPER, USB-IP, task, ABI, and
API contract must be verified first.

## Transaction rules

1. A global Burn mutex rejects overlapping install, update, repair, and
   uninstall plans with Windows Installer error 1618.
2. A second global infrastructure mutex serializes VIIPER and USB-IP mutation,
   including the built-in repair flow.
3. Install/repair and uninstall have separate process preflights at opposite
   ends of the Burn chain. This respects Burn's forward installation and
   reverse uninstallation order.
4. The original interactive user's SID and profile folders are captured before
   elevation and persisted by Burn across a reboot resume.
5. The readiness marker is cleared before mutation. It is restored only after
   all pinned identities and runtime probes pass.
6. A failed transaction stops VIIPER, disables only launcher/startup tasks whose full
   executable/action/principal/trigger contract still belongs to this package, and
   records a failed state.
7. A newer related bundle blocks an older installer with error 1638 before any
   package is planned.
8. One interlocked Plan gate rejects duplicate UI, passive, resume, or recovery
   requests until the active Apply pass completes or Retry explicitly resets
   the gate.
9. Process preflight acquires the same infrastructure mutex as in-app repair,
   preventing one transaction from stopping executables while another is
   replacing or probing them.
10. Burn creates one persisted correlation ID and passes it through every
    preflight, elevated helper, PowerShell phase, and reboot resume. In-app
    repair creates the same kind of transaction ID. Bounded append retries
    preserve diagnostics when a log reader briefly owns the file.
11. The backend always lives under Program Files. "Keep PureDS4 portable"
    changes only the UI/package location; it never creates an elevated task for
    a LocalAppData VIIPER executable.
12. Start-menu and optional desktop shortcuts are all-users shell integration
    owned by the infrastructure host, not per-user MSI components.
13. The VIIPER task has no logon trigger. It is an elevated on-demand launcher
    invoked by PureDS4 when game output needs the backend; only the PureDS4
    application task may start at login when the owner enables that option.
14. Program Files ACL normalization is a required safety gate for both the
    managed application and VIIPER backend; setup never registers an elevated
    task against a directory it could not protect.

### Installer-to-runtime task compatibility

The development candidate's application and VIIPER task readers accept the
installer's default `BelowNormal` process priority, while retaining existing
`High`-priority definitions. Required `Highest` elevation remains a separate
check; executable/action/principal/trigger ownership is not inferred from
process priority. No task is rewritten by this compatibility correction.

`OwnedStartupTaskContractTests` invokes `utils/test-viiper-launch-task.ps1` with
`-EmitInstallerTasks` through the existing PowerShell 7 verification shell.
The script imports ScheduledTasks before installing its interceptors, asserts
that host task commands are intercepted, then runs the actual installer
registration functions and verifier with real Windows `New-*` constructors.
The emitted definitions are tested by the same runtime predicates used for
live tasks, including XML reload and incompatible target/account/shape cases.
Only unregistered in-memory task definitions are created. This is not evidence
of an actual reboot, successful task execution or resolution on a reporter's PC.

On 2026-10-05 the two installer-to-runtime acceptance cases failed with the old
High-only rule, while the other seven new cases passed. With the compatibility
correction, all 90 focused tests and the 1,102-test Release suite passed locally;
live-process audio capture was excluded. Release compilation also passed.
The TRX evidence is under `artifacts/disposable/launcher-fix-20261005-01/`.
No installation, real task registration/execution or reboot was performed.

## Pinned runtime contract

### 26.11.0 installed candidate checkpoint — 2026-11-22

The owner ran the unsigned candidate
`%LOCALAPPDATA%\dev-scratch\pure-ds4\20261121-candidate-26.11.0\installer\PureDS4_26.11.0_Setup_x64.exe`
(189,091,352 bytes, SHA-256
`D17FEFCA058FBA5BAB4C51475D516291FC11A61AF9C2EDEE9500B4275B013AF9`, built
from clean source `5ca896f5e7cef5d1c42b7177bec35c51bbf71210`) as an update on
2026-11-21 and accepted it on 2026-11-22. Read-only checks on 2026-11-22:

- Bundle log `%TEMP%\PureDS4_20261121012354.log`: Burn ran from the candidate
  path above, detected the related **5.2.0** bundle (Upgrade) and MSI
  (MajorUpgrade), installed the 26.11.0 MSI, removed the 5.2.0 bundle, and
  finished with Apply result `0x0`, restart None, exit code `0x0`. VIIPER/USB-IP
  and HidHide were detected Present and not reinstalled.
- Installed `C:\Program Files\PureDS4\PureDS4.exe` reports 26.11.0 and matches
  SHA-256 `0EFF3D6DD840C1367C8CFB55643D733A73D4402AC5543AAB5CB24CA8BEC420C7`,
  the candidate application. The candidate installer still matches its hash.
- Both PureDS4 uninstall entries report 26.11.0; no 5.x registration remains.
- `HKLM\SOFTWARE\PureDS4` is `Ready` for `VIIPER-0.1.0-pureds4.2+USBIP-0.9.7.7`,
  version 26.11.0. `RunPureDS4` is Ready and `RunPureDS4VIIPER` was Running.

The previous step was the 5.2.0 candidate from `2430483` (installer SHA-256
`8C8C36A62070C5D3E083428547B8B88CB784D66E315F07621442EE5EE19717DD`), installed
by the owner on 2026-11-08 over private **5.2.0-ui-preview.7**: Apply result
`0x0`, no restart, installed app matched its candidate, both tasks present, and
Roaming user data untouched. That 5.2.0 candidate was never published.

Gaps: this PC went preview.7 -> 5.2.0 candidate -> 26.11.0, so the public
in-place update **from 5.1.2 to 26.11.0** was not exercised on hardware; the
version jump from 5.x to 26.x is covered by the installer state-machine model,
the shared UpgradeCodes and `ReleaseChannelPolicyTests`, not by a real install.
No user-data backup comparison, task-definition detail check or Windows
restart was recorded for the 26.11.0 update. Clean full-bundle installation,
real failure rollback and interrupted-install reboot/resume remain unobserved.

### 5.2.0 private preview.7 installed checkpoint — 2026-10-15

The owner authorized and completed the private preview.6 -> preview.7 update,
leaving **Launch PureDS4** unchecked. The exact installer is
`artifacts/disposable/bt-no-audio-20261015-01/installer/PureDS4_5.2.0-ui-preview.7_Setup_x64.exe`,
SHA-256 `ABC6F97C499917E15E7AACD8C5BEF4EEA20D7FF381F0C24417156D5392794019`.
This is an unsigned private diagnostic candidate, not a committed release.

Before activation, all 29 installed files matched preview.6. A fresh private
backup under `%LocalAppData%\PureDS4\ValidationBackups\preview7-20261015-01`
preserves eight non-log Roaming files, three ProgramData recovery files, the
verified original preview.6 installer and identity/manifest/checksums, owned
task definitions and the PureDS4 registry export. Copied data/recovery payloads
were hash-verified. There was a general Windows Update/CBS restart pending,
but no controller-related pending file rename or infrastructure RebootPending
marker; the USB-IP probe passed. No Windows restart was performed.

Setup used default logging with no custom log argument. The MSI replacement,
older-bundle removal and deferred infrastructure pass returned 0; Burn reported
successful Apply and exit 0 with restart None. HidHide was already present and
was not installed or repaired. Seven setup logs were copied and hash-verified
in the private backup's `InstallerLogs` directory, not in the repository.

Read-only post-install checks confirmed:

- All 29 installed application files match the preview.7 manifest. Installed
  `C:\Program Files\PureDS4\PureDS4.exe` reports `5.2.0-ui-preview.7`, SHA-256
  `48777392F59C042FF1696289C1D37AD82D4B53CA9C01677DC08C7F5032E0A7A1`.
- All eight backed-up Roaming files remain hash-identical. The registered
  bundle is preview.7, the managed MSI remains numeric 5.2.0 and the preview.6
  bundle registration is no longer listed.
- Both tasks remain enabled for the current account, Highest/Interactive,
  priority 7, with canonical Program Files actions. Only the app task has a
  logon trigger; the backend action is `viiper.exe server` in its canonical
  VIIPER directory, without a logon trigger.
- Infrastructure is Ready for `VIIPER-0.1.0-pureds4.2+USBIP-0.9.7.7`. The
  backend, USB-IP executable and both USB-IP driver hashes match their pins.
  HidHide and both USB-IP drivers are running. The USB-IP runtime probe and
  read-only VIIPER API ping passed. Ports 3241/3242 listen only on 127.0.0.1
  under the same VIIPER PID. The elevated process path was unavailable to the
  read-only process query; its live executable path was not independently proven.

PureDS4 was not launched during these checks. Exact-candidate hardware and
sustained Bluetooth validation remain pending, as do clean-install, rollback
and reboot/resume evidence. This success does not resolve the preview.6 custom
logging failure or prove the Bluetooth hypothesis. Nothing was published.

### 5.2.0 private preview.6 installed checkpoint — 2026-10-13

The owner authorized the in-place preview.4 -> preview.6 update for private
USB/Bluetooth dogfood, not publication. The unchanged installer is
`artifacts/disposable/bt-dogfood-20261012-01/installer/PureDS4_5.2.0-ui-preview.6_Setup_x64.exe`,
SHA-256 `A13EFB4B961A703F92A6E74F524D8D3B939A890BBD22830F99C83DDC54AAD1F6`.
Its candidate identity retains the dirty source baseline
`82bb1ccbe0c91c7403d0ecc0281b14f8f71db928`, composition fingerprint and
the 1,166-test Release evidence; this is not a committed release milestone.

Before activation, all 29 installed files matched preview.4. Eight non-log
Roaming data files and three ProgramData recovery files were copied and
hash-verified in a private local backup, together with the identified original
preview.4 installer, package manifest, product registry export and owned-task
definitions. No backup or personal data belongs in distribution artifacts.

The first attempt used an explicit log path inside that private backup. The
MSI initialization returned 1622 (`0x80070656`, failure opening the installation
log); the older related bundle's automatic restore attempt also returned 1638
(`0x80070666`). Subsequent checks found all 29 preview.4 files and all eight
Roaming data files unchanged, with the original registrations and enabled
canonical tasks retained. This failed attempt is not proof of successful
rollback after file replacement. The logging failure and secondary restore
behavior remain observations requiring investigation, not defects declared fixed.

With separate owner authorization, the exact same preview.6 installer was
reopened without the custom log argument. The MSI, older-bundle removal and
deferred infrastructure pass completed with exit code 0 and no requested
restart. The owner reported successful completion. Changing the log invocation
was sufficient for this retry; it does not establish why the first attempt failed.
Both attempts' logs are preserved privately, not uploaded.

Read-only post-install checks confirmed:

- All 29 installed application payload files match the preview.6 manifest.
  `C:\Program Files\PureDS4\PureDS4.exe` reports `5.2.0-ui-preview.6`, SHA-256
  `ADDE80041AA3D42C8A2292E06930AFF5AAE91C2294FEE0DE33D968A9585D0CE0`.
- All eight pre-update Roaming data files, including profiles/settings and
  the HidHide ownership journal, remain hash-identical to the backup.
- The registered bundle is preview.6 and the managed MSI remains numeric
  5.2.0; the preview.4 bundle registration is no longer listed.
- Both tasks are enabled for the current account with Highest elevation and
  canonical Program Files actions. Only the app task has a logon trigger.
- Infrastructure is marked Ready for `VIIPER-0.1.0-pureds4.2+USBIP-0.9.7.7`;
  the installed backend matches the pinned candidate payload and answers its
  read-only API ping. API/USB-IP listeners are only on `127.0.0.1` ports
  3242/3241. The elevated process's path was unavailable to the read-only query.

PureDS4 was not running at that check. No new-candidate hardware result,
Windows restart, sustained Bluetooth stability, clean installation, mid-update
failure recovery or public-release acceptance is claimed. Those gates remain
separate; the owner has not authorized publication.

### 5.1.2 installed candidate checkpoint — 2026-10-05

The owner completed the authorized in-place 5.1.1 -> 5.1.2 update using the
clean-source candidate from `42ab4041d5d7ef40b17b63134cce331e7b2fee53`.
The source worktree remains `pure-ds4-hotfix-5.1.2`, isolated from the dirty
5.2.0 UI. Package paths are relative to
`artifacts/candidate-5.1.2-42ab404/`:

- `installer/PureDS4_5.1.2_Setup_x64.exe`: 190,059,022 bytes; SHA-256
  `FD6A49D0BB20FFAE96FBBBC210C8C1A1F2855988B9FFB6211351CEA3EF32E936`.
- `portable/x64/Release/PureDS4_5.1.2_x64.zip`: 122,850,107 bytes; SHA-256
  `50E01FB87F88824CD0873C0D710C3A73588629A09E1D21E9481C47B713B4BA3B`.
- `installer/candidate-identity.json` and `installer/SHA256SUMS.txt` bind
  these bytes to the source and the unchanged pinned backend/runtime inputs.
  The installer is unsigned; no hosted-CI artifact is claimed.

Read-only checks after the owner's completion confirmed all 29 installed
application files against the frozen package manifest. Installed
`C:\Program Files\PureDS4\PureDS4.exe` reports 5.1.2.0 and matches
`A997671DAE7F0CB18BEF69969B8998D75761FE2078134087ABEDA8FC7A0B26A0`,
byte-identical to the application that passed the 976-test regression. Both
PureDS4 uninstall entries report 5.1.2; no older PureDS4 registration remained.
The eight non-log user-data files, including profiles/configuration/cache and
the HidHide ownership journal, were hash-identical to the pre-update backup
at that post-update checkpoint.
That private backup also preserves the infrastructure recovery files and the
previous verified 5.1.1 installer; it is not part of any public artifact.

The registry marker is Ready for `VIIPER-0.1.0-pureds4.2+USBIP-0.9.7.7`.
The installed backend matches its pinned hash and answered a live local
readiness ping. Both enabled tasks target the canonical Program Files
executables for the current account with Highest/Interactive and priority 7;
only `RunPureDS4` has a logon trigger. The infrastructure transaction completed
with exit code 0. No separate manual repair or task rewrite was performed by
the agent.

The owner subsequently reported passing the requested exact-candidate checks:
Windows restart without another repair, and separately over USB and Bluetooth,
readings, gameplay, disconnect/reconnect and app close/reopen without renewed
repair. The new boot was independently observed at `2026-10-06T00:05:25.500Z`
(2026-10-05 21:05:25 in America/Sao_Paulo), compared with the previous boot
`2026-10-03T23:47:19.500Z`. All 29 installed payload files still match the frozen
manifest. Both tasks retain their canonical actions, priority 7 and expected
trigger shapes; resolving their account names to SIDs confirms the current
account. The Ready registry marker and live backend ping also passed after
restart. The app was closed at the read-only process query.

This closes the authorized in-place update/startup/reboot and owner-reported
USB/Bluetooth gates, allowing the previously approved GitHub publication.
It does not establish clean installation, real failure rollback, interrupted
installer reboot/resume, another Windows PC or the reporting user's result.
Retain the frozen source/tag identity if a later documentation-only commit
records acceptance; do not rebuild or relabel the accepted application bytes.

### Installer/runtime launcher compatibility (5.1.2)

`New-ScheduledTaskSettingsSet` emits Windows priority 7 (`BelowNormal`).
Runtime validation accepts that installer-owned value and the legacy `High`
value for both `RunPureDS4` and `RunPureDS4VIIPER`. Priority does not replace
the required `Highest` run level, current-account identity, exact executable,
arguments, working directory, enabled state or trigger contract. Validation
does not rewrite, register, enable or execute a Windows task.

`OwnedStartupTaskContractTests` obtains definitions from the actual installer
registration functions. `test-viiper-launch-task.ps1 -EmitInstallerTasks`
imports the Windows task module before intercepting host-mutation commands,
asserts that interception, and calls the real in-memory constructors only.
The fixture then feeds those properties to the same runtime predicates used
for live tasks, including XML reload into a fresh unregistered definition.
Negative cases retain rejection of foreign paths/accounts, lower elevation,
disabled tasks and malformed action/trigger/settings contracts.

This proves definition compatibility without changing the host. It does not
prove a real logon/reboot, installer update or the reporter's complete setup.
The affected update -> Windows restart -> game-output-without-repair flow was
accepted on the owner's PC as recorded above; it has not been reproduced on
the reporting user's machine.

- VIIPER must match the SHA-256 of the bundled 0.1.0-pureds4.2 executable
  for 5.1.1/5.1.2; historical 5.1.0 evidence below retains its pureds4.1 identity.
- `usbip.exe` must report version 0.9.7.7 and match the pinned executable
  SHA-256.
- The active `usbip2_ude` and `usbip2_filter` driver files must match the two
  pinned signed-driver SHA-256 values.
- `usbip.exe port` must exit successfully without any known ABI/structure
  mismatch diagnostic.
- The VIIPER API must answer its local readiness probe.
- The machine readiness marker must be
  `VIIPER-0.1.0-pureds4.2+USBIP-0.9.7.7 / Ready` in the 64-bit registry view.

PureDS4 repeats these identity and ABI checks at startup. Missing or
mismatched prerequisites remain visible as unavailable output with offline
repair available. In the development candidate, automatic setup dialogs respect
the saved preference and appear at most once per session; manual setup remains
available. Suppressing a dialog never suppresses the verification failure or
permits output to start. The main UI remains available in degraded mode for
Settings and diagnostics, while virtual output continues to fail closed until a
fresh readiness check passes. Released 5.1.0/5.1.1 builds still force the modal
repair prompt when prerequisites are unhealthy.
Installer detection accepts a stopped but exact backend because VIIPER no
longer starts independently at login. The post-install check still requires
setup to start VIIPER and receive its live API response before reporting a
completed transaction.

## USB-IP 0.9.7.8 downgrade

The downgrade is intentionally split across boots:

1. Verify the exact 0.9.7.8 uninstall record, quiesce PureDS4/VIIPER, detach
   imports, remove 0.9.7.8, and persist the source/target versions plus the
   current boot identity.
2. Leave 0.9.7.7 uninstalled in that boot, disable the verified VIIPER
   on-demand launcher and PureDS4 startup task, and return 3010.
3. After reboot, prove the boot identity changed and the old root device,
   running services, and DriverStore packages are gone.
4. Install the bundled 0.9.7.7 package, validate executable and driver hashes,
   validate ABI and VIIPER API, enable the owned launcher/startup tasks, then atomically
   publish Ready.

The release gate runs a no-driver-mutation simulation of same-boot rejection,
next-boot continuation, failed-uninstaller rollback, and task suspension. The
test deliberately avoids installing a known-incompatible kernel driver on the
build host.

## Release gates

The hosted `windows-2022` runner is
[Windows Server 2022](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md).
Its MSI lifecycle pass proves the application package's install/repair/uninstall
behavior on that runner; it does not prove the complete driver bundle on a
supported consumer OS. HidHide declares support for
[Windows 10/11](https://docs.nefarius.at/projects/HidHide/FAQ/).
The owner's in-place upgrade and hardware checks remain separate evidence.
Clean full-bundle installation, real failure/recovery and reboot/resume on
Windows 10/11 are still unobserved. Do not manufacture that evidence by
deliberately damaging the daily-driver installation or by counting a model
simulation as a driver installation.

### 5.1.1 preparation — 2026-10-04

The maintenance candidate uses .NET/Core/Desktop `10.0.12` and backend
`0.1.0-pureds4.2` from clean source
`04c10b1e0b5439b649730389673a64918a43578e`. The component-source gate is
complete. Final local composition and all 29 payload hashes passed for clean
application commit `5a14defb05992250b73cb6f2e6bf0b9ffe438952`; exact identities
are recorded in the [frozen candidate checkpoint](RELEASE_CONTRACT.md#frozen-511-candidate--2026-10-04).
The owner completed the exact-build in-place upgrade on 2026-10-04. All 29
installed payload hashes, preservation of Default, canonical backend/tasks,
USB-IP executable/driver hashes and ABI probe, backend API response and
loopback-only listening passed read-only checks. See the
[5.1.1 upgrade evidence](HARDWARE_VALIDATION.md#511-in-place-upgrade--2026-10-04).
The owner subsequently passed the requested USB/Bluetooth input, gameplay,
rumble, reconnect/reopen and routing checks on that candidate. The separate
delayed-macro cancellation check also passed, with observer evidence and
preservation/restoration of Default recorded in the hardware checkpoint.
Hosted [CI 37253102643](https://github.com/meiameiameia/PureDS4/actions/runs/37253102643)
passed for that exact application source: tests, locked dependencies/advisories,
component/notice gates, build/package, offline layout and application MSI
install/repair/uninstall on Windows Server 2022. The pinned backend also passed
[CI 37253100965](https://github.com/meiameiameia/PureDS4-VIIPER/actions/runs/37253100965).
No downloadable CI artifact was retained; the accepted local packages remain
unchanged. The hosted lifecycle is not consumer-Windows full-bundle proof.
On 2026-10-04 the owner accepted unsigned distribution and the bounded risk of
unobserved clean full-bundle installation, actual failure rollback, and
reboot/resume on consumer Windows for **5.1.1**, conditional on final hardware
acceptance. Installation failure can require manual repair. This is a fresh
risk decision, not inheritance of the 5.1.0 exception or a claim that these
scenarios passed. See the [release contract](RELEASE_CONTRACT.md#511-maintenance-scope-2026-10-04).
No real installation or driver mutation is authorized by preparation alone.

### 5.1.1 public distribution verification — 2026-10-04

[PureDS4 5.1.1](https://github.com/meiameiameia/PureDS4/releases/tag/v5.1.1)
was published as latest stable from release `403308929` at
`2026-10-05T02:05:07Z` (2026-10-04 local). Its annotated tag resolves to
`5a14defb05992250b73cb6f2e6bf0b9ffe438952`; backend source
`04c10b1e0b5439b649730389673a64918a43578e` is publicly available.
All four asset sizes and GitHub SHA-256 digests matched their accepted local
files before publication. After publication, all four were downloaded without
authentication and matched again byte-for-byte by size/SHA-256. No public
download was executed and no accepted binary was rebuilt or replaced.

The package hashes are in the frozen candidate checkpoint. The accompanying
`SHA256SUMS.txt` is 182 bytes, SHA-256
`E0740B08C9EA82DA851D0AAD857E663C351783A7D509E6388F31D4039F6716DF`;
`candidate-identity.json` is 1,950 bytes, SHA-256
`9B293D0CE6D73CA2F8681CF3992EA04CBD54EB792B945FB34DFDAFB2AC0D06B5`.
It identifies a local clean-source build, the successful hosted source-validation
run, .NET 10.0.12, the pinned backend, and unsigned setup; it does not claim a
downloadable hosted artifact. Published 5.1.0 assets/tag were left untouched.

### Evidence checkpoint — 2026-10-03

The current distribution candidate is `5.1.0` from the clean source commit
`8999108763aa09c5478c8713f7fd62d99cf7ebb5`. It was composed on local Windows,
not downloaded from CI: run `36663057406` passed its test job and packaging/MSI
checks, but the build job failed at artifact upload because of the Actions
storage quota. The local candidate's identity records `ciArtifactAvailable: false`
and .NET/Windows Desktop runtime packs `8.0.31` for the application and both
installer hosts. Do not rebuild it and treat different bytes as accepted.

| Exact candidate file | Bytes | SHA-256 |
| --- | ---: | --- |
| `PureDS4_5.1.0_Setup_x64.exe` | 191,378,182 | `75B3F8516870A9473AF8E1684C00AFE1446067327AA642497C22EBBB68AE58F5` |
| `PureDS4_5.1.0_x64.zip` | 119,209,741 | `FBD02D7EE9FFE0A184BC0E10AB7CCF2670E1B2ED2DE916ACDAB1BAC5F0A98AEE` |

The files are retained under
`artifacts/disposable/candidate-ci-36663057406/`, alongside the installer
manifest, `candidate-identity.json` and `SHA256SUMS.txt`. On 2026-10-02 the owner
authorized uploading these exact files to GitHub draft release `402225081` in
`meiameiameia/PureDS4`. The remote asset sizes and SHA-256 digests match.
Before the final publication sequence on 2026-10-03, the release had
`draft: true`, `published_at: null` and
target commit `8999108763aa09c5478c8713f7fd62d99cf7ebb5`; no public `v5.1.0`
Git tag existed. Draft creation/upload is not publication authority.

| Scenario | Evidence and remaining scope |
| --- | --- |
| Existing-install upgrade / same-version replacement | The owner completed the exact candidate's update on 2026-09-30 after the Microsoft terms presentation. All 29 installed application payload files matched its manifest; Default was preserved. Installed backend/API and canonical task observations passed. See [hardware validation](HARDWARE_VALIDATION.md#current-internal-candidate-evidence). This is not a clean install. |
| Application MSI install / repair / uninstall | Windows Server 2022 CI lifecycle passed in run `36663057406` at the candidate source SHA; artifact upload failed on storage quota after build/validation completed. This was not the full driver bundle on consumer Windows. |
| Setup planning / cancellation / concurrency / core and optional failures | `utils/test-installer-state-machine.py` passed locally on the current source. This is a model with source-contract checks, not actual Burn/driver failure injection. |
| USB-IP reboot boundary / failed removal | `utils/test-viiper-reboot-boundary.ps1` passed locally using extracted backend functions and fake boot/task/process operations. No real reboot or driver mutation. |
| Backend on-demand launch / task ownership | `utils/test-viiper-launch-task.ps1` passed locally with fake tasks. Existing installed-task observations remain separate. |
| Microsoft terms presentation / planning gate | Seven actual policy/resource tests passed. Offscreen WPF checks rendered full .NET/SDK/GPL documents and exercised checked/unchecked/withdrawn consent in all setup modes. The actual `StartPlan` method, with a fake engine, rejected missing/stale consent and permitted accepted install/repair plus consent-free uninstall/layout. Those software checks did not execute a real Burn transaction; the later owner-observed update is recorded separately above. Real reboot consent resume remains unobserved. |
| Local terms-enabled candidate composition | Release publish, MSI ICE/Burn compilation and package integrity checks passed. The composed setup completed actual `/layout /quiet` with exit 0 and no restart or package execution; this proves extraction without acceptance, not installation. Exact current artifact identities are listed above; [provenance](component-provenance.md#distribution-footprint--2026-09-29) retains the earlier footprint comparison rather than identifying this candidate. The full x64 software suite passed 902 tests; live-process audio capture remained excluded. |
| Clean full bundle / real failure rollback / reboot-resume on Windows 10/11 | Unobserved. On 2026-10-03 the owner approved the bounded first-5.1.0 exception below. This is accepted residual risk, not a synthetic or real-machine pass. Do not uninstall or damage the daily-driver stack to substitute for an isolated test. |
| Exact downloaded unsigned final installer | Browser download from the draft on 2026-10-03 retained its Internet-zone marker (`ZoneId=3`) and matched the candidate installer hash/size. Authenticode status was `NotSigned`. Owner screenshots showed SmartScreen's "Windows protected your PC", app `PureDS4_5.1.0_Setup_x64.exe`, and "Unknown publisher" before setup launched. Integrity passed; the security warning was observed. This is not warning-free launch acceptance or proof that every Windows policy permits execution. No bypass, security-setting change or second installation was performed by the agent. |

The SmartScreen result is consistent with the owner-approved unsigned first
release, not evidence of malware detection or an assurance of safety. Microsoft's
[application reputation guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)
explains that publisher and file reputation influence these warnings. Release
instructions must disclose the unverified publisher and possible warnings/blocks;
never advise disabling protections or promise acceptance under Smart App Control
or enterprise policy.

### Public distribution verification — 2026-10-04

The owner made `meiameiameia/PureDS4` public. Anonymous checks returned HTTP
200 for the repository, the exact application and VIIPER source revisions,
the migration/recovery guide and the issues page. Release `402225081` was
published at `2026-10-04T14:07:03Z` as
[v5.1.0](https://github.com/meiameiameia/PureDS4/releases/tag/v5.1.0), with
`draft: false`, `prerelease: false`, and latest-release status. Its annotated
tag resolves to `8999108763aa09c5478c8713f7fd62d99cf7ebb5`; the documentation
commit does not relabel the binary source.

All four existing assets retained their IDs, bytes and SHA-256 digests.
Unauthenticated downloads of the installer, portable ZIP, `SHA256SUMS.txt`
and `candidate-identity.json` matched their accepted hashes and sizes. The
downloaded identity still records version `5.1.0`, the exact application source
and a local Windows build. The publicly rendered README preserves the icon's
explicit 80-by-80 dimensions. No executable was run, installed or rebuilt
for this distribution verification. It does not fill the hardware or
installation-evidence gaps listed above.

### Owner-approved first-release exception — 2026-10-03

The owner approved proceeding with preparation of the first `5.1.0` while
accepting the unobserved clean full-bundle installation, real failure/rollback
and reboot/resume scenarios on Windows 10/11. The stated risk is that an
installation failure can require manual repair. This applies only to this
first release and the exact candidate identified above; it is not a standing
waiver for later installers or a claim of complete Windows 10/11 validation.
Source/package checks and the successful existing-install update remain valid
but do not fill the missing evidence.

The exception does not authorize public release/tag creation, repository
visibility changes, another installation, weakening Windows protections, or
ignoring a known defect. If a concrete safety or integrity defect is found,
hold distribution and investigate it. New runtime/installer changes require
reassessing affected evidence; documentation-only updates do not require
replaying unaffected gameplay checks.

On 2026-10-03 the release-input gate reverified all five component inputs and
six artifacts, including the required third-party signatures. NuGet advisory
queries for the solution, bootstrapper and setup-actions projects completed
successfully against NuGet.org and reported no known vulnerable packages.
Microsoft's official .NET 8 metadata still listed runtime `8.0.31`, SDK
`8.0.425` and end of support `2026-11-10`. These are dated results, not future
security guarantees. See the prepared publication text and remaining authority
requirements in [release contract](RELEASE_CONTRACT.md).

Before the final publication sequence on 2026-10-03, PureDS4 was private and
PureDS4-VIIPER was public. The owner separately authorized making PureDS4's
tracked code/history public and publishing the exact draft assets; see the
[release contract](RELEASE_CONTRACT.md#version-contract). Public distribution
still requires accessible corresponding source and downloads. The bounded
installation-risk exception alone does not grant that authority.

These are distinct gates. The backend-failure exception in
[hardware validation](HARDWARE_VALIDATION.md#backend-failure-acceptance-criterion)
does not itself waive the full-bundle installation matrix; the distinct,
limited first-release exception is recorded above. Reuse unaffected hardware
evidence; do not request the entire USB/Bluetooth gameplay pass for notice-only
or test-only changes.

- One offline setup includes the app, pinned VIIPER/USB-IP components, and
  HidHide by default; interactive setup must not require separate downloads.
  Windows elevation or a driver-related restart may still be required.
- Install/update/repair UI must name the PureDS4 package and show its actual
  detected state. Declining or failing HidHide must not report protected
  virtual output as ready.
- PowerShell parser validation for the backend installer.
- WPF clean-configuration construction tests, including mandatory repair UI.
- Full unit/regression suite.
- WiX MSI ICE validation.
- Burn/bootstrapper and setup-action compilation.
- Self-contained .NET 10 publication for both installer hosts (historical
  5.1.0 records above used .NET 8).
- Real MSI install, repair, and uninstall execution on the Windows CI runner.
- Content-addressed payload manifest and hash validation.
- Fail-fast equality between `PureDS4.release` and the requested package
  identity before WiX or manifest generation can run.
- USB-IP reboot-boundary simulation.
- Installer state-machine simulation covering clean install, update, repair,
  uninstall, downgrade, cancellation, concurrency, failure, and reboot/resume.
  This is a specification model plus source-anchor checks, not execution of
  those production transitions. `InstallerPlanningPolicyTests` separately
  compiles the same ownership decisions used by Burn (upgrade deferral,
  parent-owned uninstall and direct-uninstall cleanup); it does not exercise
  Burn callbacks, mutexes, driver mutation or real reboot/resume.
- Atomic publication of the completed installer only after every gate passes.
  The verified manifest is committed first and the installer EXE last.
- Authenticode verification when signing is explicitly requested; that path
  fails closed if signing material or a first-party signature is missing. The
  owner-approved unsigned first release instead requires exact artifact
  hashes, a plain unsigned-status notice, and downloaded-candidate checks
  with normal Windows protections enabled.
