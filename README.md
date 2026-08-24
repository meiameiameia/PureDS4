# PureDS4

![PureDS4 Mapped Pad icon](branding/mapped-pad/mapped-pad-512.png)

PureDS4 is a deliberately narrow DualShock 4 and DualShock 3 tool for Windows.
It exists because [`hbashton/DS4Windows`](https://github.com/hbashton/DS4Windows)
is a catch-all that also carries DualSense, DualSense Edge, Switch Pro and
Joy-Con, and someone who owns only a DS4 or a DS3 has to carry all of it.
PureDS4 covers exactly two controllers and aims to make every feature of those
two work the way it would on a console. It is a complement to DS4Windows, not a
replacement.

Support for DualSense, DualSense Edge, Switch Pro and Joy-Con has been removed,
along with the subsystems that existed only to serve them. DualShock 4 speaker
and headset-jack audio are native DS4 features and remain.

The application still identifies itself as DS4Windows Reworked and still uses
the inherited DS4Windows install, registry, configuration and scheduled-task
identities. Until that separation is done, **do not install this alongside
DS4Windows** — the two would contend for the same locations.

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

Both records above predate the removal of the other controller families. They
describe a build that no longer exists, and nothing in them has been re-checked
against the current tree by hand.

Not yet validated: **uninstall**. The fix that caches the uninstall-only
installer packages is committed but has not been exercised end to end, so no
claim is made that an install produced by this installer can be removed by it.
The upgrade path from the previous multi-file layout is also unvalidated.
Rumble, audio, haptics, and other advanced output behavior are not claimed
unless separately exercised, and none of them have been re-exercised since the
removals.

DualShock 4 is the reference hardware, and both the Bluetooth and USB paths
were owner-validated before the removals.

DualShock 3 compatibility is a future candidate. Potential access to genuine
DS3 hardware is available for later owner-assisted validation, but existing
ScpToolkit/ScpTools driver coexistence and migration must be characterized
before changing behavior or claiming support.

## Build and test

```powershell
dotnet restore PureDS4.sln
dotnet build PureDS4.sln -c Debug -p:Platform=x64
dotnet test PureDS4.sln -c Debug -p:Platform=x64
```

## Relationship to DS4Windows

[`hbashton/DS4Windows`](https://github.com/hbashton/DS4Windows) is the
maintained DS4Windows and keeps that identity. PureDS4 is a derivative of it,
narrowed to two controllers, and credits that lineage openly.

Publication is intended but not scheduled, and no readiness claim should be
inferred from this repository. The version identifiers in the tree
(`5.1.0-beta.1` and friends) record identity only, and the executable and
configuration paths are still the inherited DS4Windows ones. If you want
DS4Windows to install and use today, go upstream.

See [`docs/HARDWARE_VALIDATION.md`](docs/HARDWARE_VALIDATION.md) for the
manual pass that checks the controller paths against real hardware.
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
