# PureDS4

<img src="PureDS4/Resources/AppIcon.png" alt="PureDS4 controller icon" width="80" height="80">

PureDS4 is a Windows controller mapper for the DualShock 4, maintained by
[meiameiameia](https://github.com/meiameiameia). It is a DS4-focused fork of
[hbashton/DS4Windows](https://github.com/hbashton/DS4Windows).

Connect a DS4 over USB or Bluetooth, create profiles, and choose **Xbox 360**
or **DualShock 4** game output. The app includes button remapping, stick and
trigger adjustments, gyro and touchpad controls, rumble, lightbar settings,
and live controller readings for checking input and mapped output.

**This release supports the DualShock 4 only.** DualSense, DualSense Edge,
Switch Pro and Joy-Con are outside its scope. DualShock 3 support is planned,
with no release date. For other controller families, see
[DS4Windows](https://github.com/hbashton/DS4Windows).

## Download and install

The first public release is **5.1.0**. Get the packages from
[PureDS4 Releases](https://github.com/meiameiameia/PureDS4/releases).

PureDS4 targets **Windows 10/11 x64**. Testing so far is limited to one Windows
PC; see [what has been tested](#what-has-been-tested) before installing.

- **Standard setup:** `PureDS4_5.1.0_Setup_x64.exe`
- **Portable package:** `PureDS4_5.1.0_x64.zip`
- **Checksums and build details:** `SHA256SUMS.txt` and `candidate-identity.json`

The first installer is **unsigned and has no verified publisher**. The tested
browser download produced a SmartScreen warning, and Windows policy may block
it. Download from this repository's Releases page and compare the file's
SHA-256 with `SHA256SUMS.txt`:

```powershell
Get-FileHash .\PureDS4_5.1.0_Setup_x64.exe -Algorithm SHA256
```

A matching hash confirms that the file matches the published checksum; it
does not prove the file is harmless. Do not disable Windows protections to
install PureDS4.

### Standard setup

If you already use DS4Windows, read [switching from DS4Windows](#switching-from-ds4windows)
first.

1. Run the setup executable and review the included component terms.
2. Keep the recommended HidHide option unless you have a reason to leave it
   out. Setup detects an existing HidHide installation.
3. Allow setup to finish, including any requested restart, then open PureDS4
   and connect your DS4.
4. Select a profile and game output. Check the controller's protection and
   output status on Home before starting a game.

Setup includes the app, its .NET runtime, and the components needed for
offline installation:

- **PureDS4-VIIPER** creates the virtual Xbox 360 or DS4 controller.
- **USB-IP** provides the virtual controller's transport.
- **HidHide** hides the physical controller from games to avoid double input.
  It is optional, but recommended.

Driver installation or repair can require administrator approval and a
restart. The bundled VIIPER backend starts when PureDS4 needs game output;
it has no separate tray icon, updater, or Windows logon trigger.

### Portable package

Extract the ZIP and read `PORTABLE-TERMS.txt` and the agreements in
`ThirdParty/` before running `PureDS4.exe`. The runtime is included, but
portable use still needs the machine-wide game-output components and drivers.
Extracting the archive does not install or configure them. PureDS4 checks
these components and offers setup or repair when needed.

Updates are manual. Use PureDS4's Releases page; the app does not use the
DS4Windows update channel. Version 5.1.0 bundles .NET 8.0.31, and .NET 8
[reaches end of support on November 10, 2026](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/). Runtime security fixes require
an updated PureDS4 package; see the
[runtime maintenance plan](docs/RELEASE_CONTRACT.md#update-and-release-authority).

## Switching from DS4Windows

PureDS4 replaces DS4Windows. Keeping both installed for ongoing use, or
running them together, is unsupported.

Before switching, back up your DS4Windows profiles and settings and keep a
copy of your previous trusted installer or portable package. Close DS4Windows
completely, including its notification-area icon. For an installed version,
use its own uninstaller. Portable versions, including older Ryochan7 and
schmaldeo builds, may have no uninstaller: preserve profiles and settings
stored alongside the app, then remove only the old application files and
startup entries. Leave shared drivers in place. PureDS4 blocks controller
activation while a detected predecessor installation can still compete for
the controller.

Once PureDS4 is running, use **Tools → Import from DS4Windows** to copy your
saved game profiles. The importer leaves the originals untouched and gives
name conflicts a `(DS4Windows)` suffix. It does **not** import Auto Profiles,
special actions, controller configurations, or every application setting.

See the [installation and recovery guide](docs/INSTALL_AND_ROLLBACK.md) for
replacement, repair and rollback details. Uninstalling PureDS4 leaves its
settings and shared HidHide/USB-IP drivers in place. Removing shared drivers
is not a general troubleshooting step, and an older installer is not a
guaranteed downgrade.

## What has been tested

Testing has covered a **DS4 v2.1 on one Windows PC**, using USB and Bluetooth.
Earlier 5.1.0 candidates passed input, Xbox 360 game output, gameplay,
disconnect/reconnect and app restart checks. The current candidate completed
an in-place update, preserved the existing Default profile, and matched its
installed-file manifest. A full gameplay pass was not repeated on that exact
build.

The remaining limits matter:

- Clean full-bundle installation, actual installation-failure rollback, and
  reboot/resume on Windows 10/11 have not been observed. A failed installation
  may need manual repair.
- Physical backend interruption, sleep/wake, and two simultaneous controllers
  have not been validated. Automated recovery tests do not cover those
  hardware scenarios.
- DS4 speaker and headset audio are implemented, but have not had current
  physical validation.
- Keyboard and mouse mappings use SendInput. They may not reach an elevated
  game when PureDS4 is running without elevation.

The [hardware validation record](docs/HARDWARE_VALIDATION.md) and
[installer checkpoint](docs/INSTALLER_VALIDATION_STRATEGY.md#evidence-checkpoint--2026-10-03)
separate observed results from procedures and untested cases.

## Troubleshooting and support

If input is missing or behaves unexpectedly, open **Profiles → edit a profile
→ Controller Readings**. Compare the input and output values to check whether
the problem starts at the controller or after mapping. Run `joy.cpl` to check
whether Windows sees the virtual game controller.

For setup or update failures, save the error and logs before trying another
installer. Follow any restart request. Use the intended PureDS4 installer's
repair flow rather than deleting shared drivers.

Report problems through
[PureDS4 issues](https://github.com/meiameiameia/PureDS4/issues). Include:

- PureDS4 and Windows versions, and whether you used setup or the portable ZIP
- DS4 model, USB or Bluetooth connection, and selected game output
- The error text, steps to reproduce it, and what you expected to happen

Runtime logs are in `%AppData%\PureDS4\Logs`. Setup diagnostics are in
`%ProgramData%\PureDS4\Installer`; include a transaction ID if one is shown.
Before posting excerpts, remove usernames, personal paths, controller
identifiers, MAC addresses and credentials. Keep complete logs locally and
do not post personal profiles or entire data folders publicly.

## Build from source

From a Windows development environment with the project's .NET SDK:

```powershell
dotnet restore PureDS4.sln --locked-mode
dotnet build PureDS4.sln -c Debug -p:Platform=x64
dotnet test PureDS4.sln -c Debug -p:Platform=x64 --filter "FullyQualifiedName!~LiveProcessCapture"
```

These commands build and test the application; they do not validate physical
hardware or the full installer. See the [installer documentation](installer/README.md)
for packaging and the [release contract](docs/RELEASE_CONTRACT.md) for build
identity, runtime maintenance and release details.

## Credits and license

PureDS4 builds on [hbashton/DS4Windows](https://github.com/hbashton/DS4Windows)
and the DS4Windows contributors before it. The upstream history, original
copyright notices and GPL notices are preserved. The `upstream-baseline` tag
records the fork's starting point.

PureDS4 is licensed under **GPL-3.0-or-later**. See [COPYING](COPYING).
Bundled third-party components retain their own licenses. Standard setup
presents the separate Microsoft runtime and Windows SDK terms; portable users
must read the included agreements before use. These are not an additional
PureDS4 EULA.

See [component provenance](docs/component-provenance.md) for sources and
licenses, [managed dependency notices](PureDS4/ThirdParty/ManagedDependencies.NOTICE.txt)
for the .NET package inventory, and
[controller artwork attribution](PureDS4/Resources/ControllerArtwork.NOTICE.txt)
for the artwork credits. The bundled backend's source is available at
[PureDS4-VIIPER](https://github.com/meiameiameia/PureDS4-VIIPER).
