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

Additionally validated on the single-file build installed by the reworked
installer, starting from a machine with no prior DS4Windows, VIIPER, usbip-win2
or HidHide present:

- The installer provisions VIIPER, usbip-win2 and HidHide and completes without
  error.
- The install root contains only the intended six files and four content
  directories.
- A DualShock 4 over **USB** is detected, supplies physical input, reports
  Managed/Virtual as Ready, and completes the transition to Native Physical
  exposure.

Not yet validated: **uninstall**. The fix that caches the uninstall-only
installer packages is committed but has not been exercised end to end, so no
claim is made that an install produced by this installer can be removed by it.
DualSense hardware is unvalidated, as is the upgrade path from the previous
multi-file layout. Rumble, audio, haptics, and other advanced output behavior
are not claimed unless separately exercised.

DualShock 4 is the reference hardware, and both the Bluetooth and USB paths
have now been owner-validated. DualSense and DualSense Edge compatibility is
preserved, but no DualSense hardware has been validated.

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

## Relationship to DS4Windows

[`hbashton/DS4Windows`](https://github.com/hbashton/DS4Windows) is the
maintained DS4Windows and keeps that identity. This repository is a personal
derivative: it exists so its owner can run the controller setup he wants, and
as a place to develop work that is offered back upstream by pull request.

There is no planned release. The version identifiers in the tree
(`5.1.0-beta.1` and friends) record identity only, and the executable and
established DS4Windows configuration paths are unchanged for compatibility.
If you are looking for DS4Windows to install and use, go upstream.

See [`docs/RELEASE_CONTRACT.md`](docs/RELEASE_CONTRACT.md) for the version,
upgrade, compatibility, and artifact contract those identifiers follow.
See [`docs/visual-contract.md`](docs/visual-contract.md) for the interface,
identity, status, accessibility, and validation rules.

## Provenance and license

The annotated `upstream-baseline` tag records the recoverable upstream starting
point. The `main` branch is the derivative working line at
<https://github.com/meiameiameia/ds4windows-reworked>; `AGENTS.md` contains the
canonical project guidance.

The project is licensed under GPLv3. See [`COPYING`](COPYING).
