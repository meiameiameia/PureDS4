# PureDS4 standard installer

`build-installer.ps1` composes the standard x64 distribution as a WiX 5 Burn
bundle with a custom WPF interface. It contains the managed PureDS4 MSI,
PureDS4 VIIPER 0.1.0-pureds4.1, USB-IP 0.9.7.7, and an optional HidHide package.

The installer intentionally has no portable mode or destination selector. The
portable ZIP remains a separate CI artifact. The standard installer places
VIIPER under protected `%ProgramFiles%\PureDS4\VIIPER`. A portable package
may use a VIIPER executable only when it exactly matches the SHA-256 identity
pinned by that PureDS4 build. Portable and release artifacts must never
create or retarget the persistent `RunPureDS4` or `RunPureDS4VIIPER` tasks;
installer gate must enforce that boundary before public distribution.
The installed VIIPER task is an elevated on-demand launcher without a Windows
logon trigger. If PureDS4 is configured to start with Windows, its own task
starts the app, which launches VIIPER during its own readiness/output flow. The fork
has no tray icon or independent update/autostart channel.

```powershell
.\installer\build-installer.ps1 `
  -PublishRoot .\bin\x64\Release\output `
  -ProductVersion 5.1.0 `
  -BundleVersion 5.1.0 `
  -DisplayVersion 5.1.0 `
  -SkipApplicationPublish
```

Installer logs are written by Burn and by the elevated helpers. Process
preflight diagnostics are stored in
`%ProgramData%\PureDS4\Installer\setup-actions.log`; VIIPER, USB-IP, startup
task, and runtime verification is stored in
`%ProgramData%\PureDS4\Installer\infrastructure-actions.log`. The in-app
repair host records failures that occur before its helper starts in
`%ProgramData%\PureDS4\Installer\viiper-setup-host.log`.
One transaction ID is preserved across Burn, setup actions, the infrastructure
backend, and reboot resume so those logs can be correlated without timestamp
guesswork.

The output is named `PureDS4_5.1.0_Setup_x64.exe` and the installed
executable is `PureDS4.exe`. `5.1.0` is the owner-selected first public version
target, not a release-readiness or publication claim. An internal `5.1.0`
bundle upgraded the owner's former `5.1.0-beta.2` installation, and a later
bundle passed a same-version replacement. The eventual published file must
match the exact accepted candidate; clean-install and failure/recovery checks
remain before publication can be considered.

The owner accepts an unsigned first public release. Its official download
must state that there is no verified publisher and include the exact artifact
version and SHA-256. SmartScreen or Smart App Control may warn or block it;
users should verify the download source and hash, not disable Windows
protections. A test of the exact downloaded candidate with normal protections
enabled is still required before public distribution.

Set `DS4W_SIGN_CERT_PATH`, `DS4W_SIGN_CERT_PASSWORD`, and optionally
`DS4W_SIGN_TIMESTAMP_URL` to sign the application, setup hosts, MSI, and final
EXE in a protected release environment. Passing `-RequireSigning` makes the
build fail closed unless PureDS4 and the packaged VIIPER binary both have
valid signatures; a release job that signs also needs the
`DS4W_SIGN_CERT_BASE64` and `DS4W_SIGN_CERT_PASSWORD` secrets. The approved
unsigned first-release path does not pass `-RequireSigning`.

For a manually dispatched CI candidate, the portable and installer artifacts
also contain `candidate-identity.json` and `SHA256SUMS.txt`. They bind the
source commit, artifact version, SHA-256 of the inner ZIP/EXE, installer
signature status, and .NET/Windows Desktop runtime-pack versions selected by
that restore. Compare the hashes with the downloaded inner files; GitHub's
artifact wrapper has its own identity. These records do not replace the
downloaded-candidate security check or owner approval to publish.

To prepare the exact target-version validation artifact, manually dispatch
`CI Build` with `release_candidate` checked. `utils/get-build-identity.ps1`
then keeps display, Burn, and MSI versions at the application's target
(`5.1.0` for the first release), with no CI suffix. Normal push/PR and manual
CI builds keep their CI labels. The candidate identity also records whether
the artifact is a `release-candidate` or `ci` build. This option does not
create a release/tag, remove release blockers, or authorize publication.

The PowerShell infrastructure backend is the sole VIIPER/USB-IP mutation
engine. Burn and the in-app repair surface only validate, stage, elevate, and
report that same engine. HidHide is an optional non-vital package: its failure
is reported without rolling back a healthy PureDS4 + VIIPER installation.

VIIPER's legacy Windows network installer is developer-only and fail-closed by
default. It cannot silently create a second LocalAppData/HKCU owner beside this
managed infrastructure transaction.

Setup lists HidHide as `Installed` when Burn detects its installation and
offers the recommended install checkbox only when it is absent. This detected
package state does not prove that a particular controller is protected; the
application checks protection when the controller connects. Existing HidHide
installations and the package-planning policy remain unchanged.

## Offline terms and acceptance

Standard setup includes the full .NET Library and Windows SDK agreements,
plus PureDS4's GPL, in its executable. They can be read without a browser,
Office, or a network connection. The Microsoft checkbox starts unchecked;
install/update/repair cannot plan any changes until it is accepted. The GPL
is presented separately, not as a PureDS4 EULA or an additional restriction
on GPL-covered code. Uninstall and `/layout /quiet` do not require agreement.

An unattended install/repair requires the exact reviewed terms argument
`--accept-microsoft-terms=microsoft-20260929` in addition to its normal action
and display arguments. Pass it only after reading and accepting the shipped
texts. Missing, stale, duplicate or conflicting acceptance is rejected before
planning. A reboot resume retains the current transaction's accepted terms
identifier; a new setup invocation does not inherit an earlier run's consent.
The internal infrastructure recovery pass uses the same consent gate.

The portable ZIP contains `PORTABLE-TERMS.txt`, `COPYING`, and both full
Microsoft agreements under `ThirdParty/`. Its read-before-use notice points
to the Microsoft agreements' acceptance-by-use provisions; it does **not**
record explicit checkbox consent or introduce a new first-run controller
startup gate. The MSI is an internal bundle payload, not a separately offered
public distribution. Builds fail if vendor terms differ from reviewed hashes
or the installed .NET build license differs from the text presented by setup.
No driver/runtime binary was modified by this terms change.

The transaction state machine, failure containment, pinned identities, reboot
boundary, and release gates are documented in
[`docs/INSTALLER_VALIDATION_STRATEGY.md`](../docs/INSTALLER_VALIDATION_STRATEGY.md).
