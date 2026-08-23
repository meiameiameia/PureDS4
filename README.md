# DS4Windows Reworked

![DS4Windows Reworked Mapped Pad icon](branding/mapped-pad/mapped-pad-512.png)

This repository is a maintainable DS4Windows derivative focused on dependable
controller input, profile mapping, and Windows virtual-controller output.

## Current validation status

The following has been owner-validated on the canonical dogfood runtime after
the controller-exposure recovery milestone:

- The committed local build launches successfully.
- A DualShock 4 connects over Bluetooth, is detected, and supplies physical
  input.
- **Continue without virtual output** works.
- The application reports Managed/Virtual as Ready, and the transition to
  Native Physical exposure, and back to Managed/Virtual, both complete.
- Forza Horizon 6 recognizes the Managed/Virtual Xbox 360 output and responds
  normally to gameplay input.
- Controller disconnect/reconnect and app close/relaunch both work.

Not yet validated: DS4 over USB and DualSense hardware. Rumble, audio, haptics,
and other advanced output behavior are not claimed unless separately
exercised.

DualShock 4 is the reference hardware. USB support is intended, but only the
Bluetooth path has been owner-validated from the baseline. DualSense and
DualSense Edge compatibility is preserved, but no DualSense hardware has been
validated.

DualShock 3 compatibility is a future candidate. Potential access to genuine
DS3 hardware is available for later owner-assisted validation, but existing
ScpToolkit/ScpTools driver coexistence and migration must be characterized
before changing behavior or claiming support.

## Build and test

```powershell
dotnet restore DS4WindowsWPF.sln
dotnet build DS4WindowsWPF.sln -c Debug -p:Platform=x64
dotnet test DS4WindowsWPF.sln -c Debug -p:Platform=x64
```

## Product and release identity

The public product is **DS4Windows Reworked**, maintained under the
`meiameiameia` alias. The executable and established DS4Windows configuration
paths remain unchanged for compatibility. The first public candidate is
`5.1.0-beta.1`; it is not authorized for publication until the remaining
provenance, signing, installer, and release-validation gates are complete.

See [`docs/RELEASE_CONTRACT.md`](docs/RELEASE_CONTRACT.md) for the version,
upgrade, compatibility, artifact, and release-authority contract.
See [`docs/visual-contract.md`](docs/visual-contract.md) for the interface,
identity, status, accessibility, and validation rules.

## Provenance and license

The annotated `upstream-baseline` tag records the recoverable upstream starting
point. The `main` branch is the derivative working line at
<https://github.com/meiameiameia/ds4windows-reworked>; `AGENTS.md` contains the
canonical project guidance.

The project is licensed under GPLv3. See [`COPYING`](COPYING).
