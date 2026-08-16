# DS4Windows Reworked

This repository is a maintainable DS4Windows derivative focused on dependable
controller input, profile mapping, and Windows virtual-controller output.

## Current validation status

- The application builds and launches locally.
- A DualShock 4 has been detected over Bluetooth and physical input has been
  observed.
- “Continue without virtual output” has been owner-validated.
- VIIPER output creation, complete virtual-controller behavior, game
  compatibility, and reconnect/restart cycles are not yet fully validated.

DualShock 4 is the reference hardware. USB support is intended, but only the
Bluetooth path has been owner-validated from the baseline. DualSense and
DualSense Edge compatibility is preserved, but no DualSense hardware has been
validated.

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
