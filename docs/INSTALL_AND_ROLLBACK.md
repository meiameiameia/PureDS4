# Installing PureDS4, and getting back out

PureDS4 is intended to replace DS4Windows or an earlier DS4Windows Reworked
installation rather than run beside one. That is the destination, not a
precondition: the two products own entirely separate files, registry keys,
configuration, and scheduled tasks, so both can be *installed* at once
while you decide. What they cannot do is *run* at once.

This document covers installing PureDS4 while a predecessor is still
present, and exactly how to get back to it if you decide not to keep
PureDS4.

## What PureDS4 owns

Nothing in this column overlaps a DS4Windows or DS4Windows Reworked
installation, which is what makes installing alongside one safe.

| Resource | PureDS4 | Predecessor |
| --- | --- | --- |
| Program files | `C:\Program Files\PureDS4` | `C:\Program Files\DS4Windows` |
| Registry | `HKLM\Software\PureDS4` | `HKLM\SOFTWARE\DS4Windows` |
| Profiles and settings | `%AppData%\PureDS4` | `%AppData%\DS4Windows` |
| Scheduled tasks | `RunPureDS4`, `RunPureDS4VIIPER` | `RunDS4Windows`, `RunVIIPER` |
| Installer upgrade code | its own | its own |

Because the Windows Installer upgrade codes differ, installing PureDS4
cannot upgrade over, modify, or remove the other product's package. It is
registered in *Apps and Features* as its own entry.

## What PureDS4 shares and will not remove

Three pieces are machine-wide infrastructure that any DS4Windows-derived
mapper uses. PureDS4 installs them only when they are missing, and its own
uninstall deliberately leaves them behind, because a predecessor may still
depend on them:

- **HidHide** — hides the physical controller from games so they see only
  the virtual pad.
- **USB-IP** — the transport VIIPER uses to publish a virtual controller.
- **FakerInput** — optional, and **not** installed by default.

If your machine already has these at the versions PureDS4 expects, the
installer detects them and skips them. Nothing is downgraded or replaced.

**VIIPER** is the one shared component that needs care, and it has its own
section below.

## Before installing

1. **Clear any pending reboot.** Driver-level infrastructure can leave a
   machine mid-transaction. Check:

   ```powershell
   Get-ItemProperty 'HKLM:\SOFTWARE\DS4Windows' -ErrorAction SilentlyContinue |
       Select-Object InfrastructureState, InfrastructureStateUtc
   ```

   If `InfrastructureState` is `RebootPending`, reboot before continuing.
   Installing or validating on top of a pending reboot produces results
   that cannot be trusted.

2. **Close the predecessor completely**, including its notification-area
   icon. PureDS4 checks for this at every launch and will refuse to start
   while a `DS4Windows` process is running — it will not close it for you.

3. **Decide about its scheduled tasks.** If `RunDS4Windows` is enabled, the
   old application starts itself at logon and will collide with PureDS4
   every day. Check with:

   ```powershell
   Get-ScheduledTask -TaskName RunDS4Windows, RunVIIPER -ErrorAction SilentlyContinue |
       Select-Object TaskName, State
   ```

   Disabling a predecessor's task is a change to *its* installation, so
   PureDS4 never does it for you. Disable it yourself from Task Scheduler
   if you want PureDS4 to be the one that starts at logon, and re-enable it
   if you roll back.

4. **Keep a note of your fallback.** Write down where the working
   predecessor lives before you change anything.

## Installing

Run `PureDS4_<version>_Setup_x64.exe`. It is currently unsigned, so Windows
SmartScreen will warn; that is expected for a build that has not been
through a signing decision.

The installer will:

1. Quiesce any running PureDS4 and VIIPER (preflight).
2. Install PureDS4's own files, registry entry, and scheduled tasks.
3. Install VIIPER and USB-IP **only if** the expected versions are absent.
4. Install HidHide **only if** absent.
5. Optionally create a desktop shortcut.

## The VIIPER handover

VIIPER is the backend that publishes the virtual controller, and only one
copy can own that role at a time. PureDS4 ships its own pinned copy and
verifies it by SHA-256.

If a `viiper.exe` from a different path — typically a predecessor's copy —
is running when PureDS4 starts, PureDS4 stops and tells you. It will offer
an elevated prompt to close that process so it can start its own. This is a
runtime handover, not a removal: the other product's VIIPER files are left
untouched, and going back to that product simply reverses the handover.

To see which copy is running:

```powershell
Get-Process viiper -ErrorAction SilentlyContinue |
    Select-Object Id, Path, StartTime
```

Note that a predecessor's VIIPER may offer to update itself. PureDS4 pins a
specific VIIPER version by hash, so letting a shared copy self-update can
cause PureDS4 to stop trusting it and install its own instead.

## Confirming which build is running

This matters more than it sounds. With two products installed, it is easy
to validate the wrong one:

```powershell
Get-Process PureDS4, DS4Windows -ErrorAction SilentlyContinue |
    Select-Object ProcessName, Id, Path
```

The path must be the build you intend to test. See
`docs/HARDWARE_VALIDATION.md` for the full controller checklist to run once
you are certain.

## Rolling back

Three levels, smallest first. Try them in order.

### 1. Just go back, keep both installed

Nothing to uninstall. Close PureDS4, reopen the predecessor, and approve
its own VIIPER prompt if it asks. Re-enable `RunDS4Windows` in Task
Scheduler if you disabled it. Your predecessor's profiles were never
touched — PureDS4 reads them at most to import copies, and refuses by
design to use its configuration directory as its own.

This is the fastest path and the one to reach for first.

### 2. Uninstall PureDS4

*Apps and Features* → **PureDS4** → Uninstall, or re-run the setup
executable and choose Uninstall. This removes PureDS4's program files,
registry key, scheduled tasks, and shortcut.

It deliberately **does not** remove:

- HidHide, USB-IP, or FakerInput — a predecessor may still need them;
- `%AppData%\PureDS4`, your PureDS4 profiles and settings, so a later
  reinstall finds them again. Delete that folder yourself if you want it
  gone.

A reboot may be requested if shared infrastructure was mid-transition.

### 3. Full recovery

If the machine ends up in a state where neither product works — usually a
half-finished driver transaction rather than an application fault:

1. Reboot first. A pending driver transaction resolves on reboot, and many
   apparent failures disappear here.
2. Check the drivers are still present:

   ```powershell
   pnputil /enum-drivers | Select-String usbip
   ```

3. Repair the predecessor through its own installer, not PureDS4's. Each
   product only repairs what it owns.
4. Collect evidence before changing more: `%AppData%\PureDS4\Logs`
   (including `startup_failure.log` if present) and the equivalent folder
   for the predecessor.

## Preserving your data

PureDS4 never deletes another product's configuration, and never uses that
configuration directory as its own — this is enforced in code, not just by
convention. Importing profiles from a predecessor copies them; it never
moves, edits, or removes the originals, and never overwrites a PureDS4
profile that already exists under the same name.

The reverse is also worth knowing: uninstalling a predecessor through its
own uninstaller will not remove `%AppData%\PureDS4`.
