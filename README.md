# PureDS4

![PureDS4 controller icon](PureDS4/Resources/AppIcon.png)

PureDS4 is a deliberately narrow DualShock 4 tool for Windows. It exists
because [`hbashton/DS4Windows`](https://github.com/hbashton/DS4Windows) is a
catch-all that also carries DualSense, DualSense Edge, Switch Pro and Joy-Con,
and someone who owns only a DS4 has to carry all of it. PureDS4 covers one
controller and aims to make every feature of it work the way it would on a
console.

**The first release supports the DualShock 4 only. If you use a DualSense,
DualSense Edge, Switch Pro, Joy-Con or any other controller family, use
[`hbashton/DS4Windows`](https://github.com/hbashton/DS4Windows) instead.**

DualShock 3 is a **planned future milestone with no schedule**, and this
release neither claims nor offers it. The DS3 implementation is kept dormant in
the tree rather than deleted, because the work will be picked up when genuine
hardware can be validated — but nothing in the first release turns it on.

PureDS4 is meant to **replace** DS4Windows on a machine, not to run beside it.
Running both is not a supported outcome. PureDS4 nevertheless owns its own
executable, install and registry roots, configuration and log paths, scheduled
tasks, Startup shortcut, IPC identities, and installer upgrade codes, so that
a package can never silently upgrade over another product and so that what
PureDS4 owns can be
audited and removed cleanly. The replacement flow detects an older
installation and gates activation while it can compete with PureDS4. It can
hand off removal to the predecessor's own uninstaller after confirmation; it
does not silently remove another product. The complete real-machine upgrade
and recovery matrix is still pending.

Removal of DualSense, DualSense Edge, Switch Pro and Joy-Con support is
**in progress, not finished**. Physical detection, the profile output selector,
the manual output picker and the mapping list no longer expose those families,
and the Switch 2 Pro game output has been retired along with its packet writer
and artwork. What remains is deliberate: retired members stay in the profile
and output-slot formats so an existing file still loads, and each one is
normalized to a supported output when it does. Some inherited code, strings and
assets are still being characterized before deletion, so do not read this as a
finished removal. DualShock 4 speaker and headset-jack audio are native DS4
features and remain.

The two game outputs PureDS4 offers are **Xbox 360** and **DualShock 4**.

## Validation status

An internal installed `5.1.0` candidate from commit `49ba5d2` has passed an
owner-observed, one-machine DualShock 4 USB and Bluetooth pass: virtual output,
gameplay, disconnect/reconnect, and app restart. Managed/direct round trips
passed on the preceding candidate and were not repeated on this build.
The exact artifact and limits of that evidence are recorded in
[`docs/HARDWARE_VALIDATION.md`](docs/HARDWARE_VALIDATION.md). This is not a
public-release or broad hardware-compatibility claim.

- DualShock 3 is **not supported and not claimed**. It is a planned milestone
  with its own gate: DsHidMini setup, existing ScpToolkit/ScpTools driver
  state, genuine hardware, installation, runtime, gameplay and uninstall all
  have to be exercised on the target machine before any release mentions it as
  working. That gate does not block the first release.
- The beta-to-`5.1.0` upgrade and later same-version replacements passed on
  the owner's machine. Clean installation, failure/recovery, reboot, uninstall,
  and the exact downloaded unsigned-candidate check remain separate gates.
- `installer/release-inputs.json` calculates `releaseReady: true` for its
  pinned component inputs, not for the complete product. The owner-approved
  [backend-failure criterion](docs/HARDWARE_VALIDATION.md#backend-failure-acceptance-criterion)
  uses deterministic software checks; physical interruption remains unobserved.
  Overall release gates remain open. The [component review](docs/component-provenance.md)
  records the separately presented offline Microsoft terms and the limits of
  that licensing assessment. This field never authorizes publication.

[`docs/HARDWARE_VALIDATION.md`](docs/HARDWARE_VALIDATION.md) describes the
manual pass that a hardware claim requires.

## Build and test

```powershell
dotnet restore PureDS4.sln --locked-mode
dotnet build PureDS4.sln -c Debug -p:Platform=x64
dotnet test PureDS4.sln -c Debug -p:Platform=x64 --filter "FullyQualifiedName!~LiveProcessCapture"
```

## Relationship to DS4Windows

[`hbashton/DS4Windows`](https://github.com/hbashton/DS4Windows) is the
maintained DS4Windows and keeps that identity. PureDS4 is a derivative of it,
focused publicly on the DualShock 4, and credits that lineage openly.

Upstream history, the `upstream` remote, the annotated `upstream-baseline` tag,
the original copyright notices, and the GPL notices are all preserved.

Publication is intended but not scheduled, and no readiness claim should be
inferred from this repository. The owner selected `5.1.0` as the first public
version target, subject to the remaining hardware, installer, and release
gates. No release or tag has been authorized. If you want a controller mapper
to install and use today, go upstream.

See [`docs/RELEASE_CONTRACT.md`](docs/RELEASE_CONTRACT.md) for the version,
upgrade, compatibility, and artifact contract those identifiers follow.
See [`docs/visual-contract.md`](docs/visual-contract.md) for the interface,
identity, status, accessibility, and validation rules.

## Provenance and license

The annotated `upstream-baseline` tag records the recoverable upstream starting
point. The `main` branch is the derivative working line at
<https://github.com/meiameiameia/pureds4>; `AGENTS.md` contains the
canonical project guidance.

The project is licensed under GPL-3.0-or-later. See [`COPYING`](COPYING).
Included Microsoft runtime/Windows SDK components retain their separate
terms. Standard setup presents the complete terms offline and requires a
Microsoft-only acceptance before install/update/repair. Portable users must
read `PORTABLE-TERMS.txt` and the full agreements under `ThirdParty/` before
using those components. These terms do not add restrictions to PureDS4's
GPL-covered code. See [component provenance](docs/component-provenance.md).
