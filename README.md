# DS4Windows Reworked

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

Derivative release packaging and the final installation path are not finalized
yet.

## Provenance and license

The annotated `upstream-baseline` tag records the recoverable upstream starting
point. The `main` branch is the derivative working line; `AGENTS.md` contains
the canonical project guidance.

The project is licensed under GPLv3. See [`COPYING`](COPYING).
