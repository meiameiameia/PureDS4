# PureDS4 standard installer

`build-installer.ps1` composes the standard x64 distribution as a WiX 5 Burn
bundle with a custom WPF interface. It contains the managed PureDS4 MSI,
VIIPER 0.1.0, USB-IP 0.9.7.7, and optional HidHide/FakerInput packages.

The installer intentionally has no portable mode or destination selector. The
portable ZIP remains a separate CI artifact. The standard installer places
VIIPER under protected `%ProgramFiles%\PureDS4\VIIPER`. A portable package
may use a VIIPER executable only when it exactly matches the SHA-256 identity
pinned by that PureDS4 build. Portable and release artifacts must never
create or retarget the persistent `RunPureDS4` or `RunPureDS4VIIPER` tasks;
installer gate must enforce that boundary before public distribution.

```powershell
.\installer\build-installer.ps1 `
  -PublishRoot .\bin\x64\Release\output `
  -ProductVersion 5.1.0 `
  -BundleVersion 5.1.0-beta.1 `
  -DisplayVersion 5.1.0-beta.1 `
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

The output is named `PureDS4_5.1.0-beta.1_Setup_x64.exe` and the installed
executable is `PureDS4.exe`. That version line records identity only; no first
public PureDS4 version has been chosen.

Authenticode signing is supported but **not decided as release policy**.
Whether a public PureDS4 artifact must be signed is an open owner decision;
this section describes the mechanism only.

Set `DS4W_SIGN_CERT_PATH`, `DS4W_SIGN_CERT_PASSWORD`, and optionally
`DS4W_SIGN_TIMESTAMP_URL` to sign the application, setup hosts, MSI, and final
EXE in a protected release environment. Passing `-RequireSigning` makes the
build fail closed unless PureDS4 and the packaged VIIPER binary both have
valid signatures; a release job that signs also needs the
`DS4W_SIGN_CERT_BASE64` and `DS4W_SIGN_CERT_PASSWORD` secrets. If the owner
settles on unsigned releases, `-RequireSigning` is simply not passed.

The PowerShell infrastructure backend is the sole VIIPER/USB-IP mutation
engine. Burn and the in-app repair surface only validate, stage, elevate, and
report that same engine. HidHide and FakerInput are optional non-vital packages:
their failure is reported without rolling back a healthy PureDS4 + VIIPER
installation.

VIIPER's legacy Windows network installer is developer-only and fail-closed by
default. It cannot silently create a second LocalAppData/HKCU owner beside this
managed infrastructure transaction.

The transaction state machine, failure containment, pinned identities, reboot
boundary, and release gates are documented in
[`docs/INSTALLER_VALIDATION_STRATEGY.md`](../docs/INSTALLER_VALIDATION_STRATEGY.md).
