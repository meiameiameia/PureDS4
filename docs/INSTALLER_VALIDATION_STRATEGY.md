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

## Pinned runtime contract

- VIIPER must match the SHA-256 of the bundled 0.1.0-pureds4.1 executable.
- `usbip.exe` must report version 0.9.7.7 and match the pinned executable
  SHA-256.
- The active `usbip2_ude` and `usbip2_filter` driver files must match the two
  pinned signed-driver SHA-256 values.
- `usbip.exe port` must exit successfully without any known ABI/structure
  mismatch diagnostic.
- The VIIPER API must answer its local readiness probe.
- The machine readiness marker must be
  `VIIPER-0.1.0-pureds4.1+USBIP-0.9.7.7 / Ready` in the 64-bit registry view.

PureDS4 repeats these identity and ABI checks at startup. Missing or
mismatched prerequisites open an offline repair prompt; suppressing a location
recommendation never suppresses a verification failure. The main UI remains
available in degraded mode for Settings and diagnostics, while virtual output
continues to fail closed until a fresh readiness check passes.
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
- Self-contained .NET 8 publication for both installer hosts.
- Real MSI install, repair, and uninstall execution on the Windows CI runner.
- Content-addressed payload manifest and hash validation.
- Fail-fast equality between `PureDS4.release` and the requested package
  identity before WiX or manifest generation can run.
- USB-IP reboot-boundary simulation.
- Installer state-machine simulation covering clean install, update, repair,
  uninstall, downgrade, cancellation, concurrency, failure, and reboot/resume.
- Atomic publication of the completed installer only after every gate passes.
  The verified manifest is committed first and the installer EXE last.
- Authenticode verification when signing is explicitly requested; that path
  fails closed if signing material or a first-party signature is missing. The
  owner-approved unsigned first release instead requires exact artifact
  hashes, a plain unsigned-status notice, and downloaded-candidate checks
  with normal Windows protections enabled.
