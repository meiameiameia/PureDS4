# Migrating to PureDS4 from DS4Windows

PureDS4 replaces DS4Windows or an earlier DS4Windows Reworked
installation. It is not a companion to one, and running both is not a
supported configuration: DS4Windows supports everything PureDS4 supports
and more, so keeping both installed gains nothing and leaves two products
competing for the same controller.

PureDS4 exists for people who want the narrower tool — DualShock hardware,
without DualSense features or the complexity that comes with them. If you
need DualSense, DualSense Edge, Switch Pro, or Joy-Con support, use
[DS4Windows](https://github.com/hbashton/DS4Windows) instead; PureDS4 will
not serve you.

This document covers migrating from a predecessor, and how to get back if
you change your mind.

> During a migration both products are briefly present on the machine. That
> is a transitional state on the way to removing the old one, not a
> destination.

## Why the two can be installed at once, briefly

Nothing PureDS4 owns overlaps a predecessor, which is what makes the
transition survivable rather than a leap:

| Resource | PureDS4 | Predecessor |
| --- | --- | --- |
| Program files | `C:\Program Files\PureDS4` | `C:\Program Files\DS4Windows` |
| Registry | `HKLM\Software\PureDS4` | `HKLM\SOFTWARE\DS4Windows` |
| Profiles and settings | `%AppData%\PureDS4` | `%AppData%\DS4Windows` |
| Scheduled tasks | `RunPureDS4`, `RunPureDS4VIIPER` | `RunDS4Windows`, `RunVIIPER` |
| Installer upgrade code | its own | its own |

Because the upgrade codes differ, installing PureDS4 cannot upgrade over or
silently modify the other product's package. That separation exists so a
migration can be *finished* deliberately, and so a failed one can be backed
out — not so the two can live together indefinitely.

They cannot both *run*. PureDS4 refuses to start while a `DS4Windows`
process is running, and checks this on every launch.

## Shared components

Three pieces are machine-wide infrastructure that any DS4Windows-derived
mapper uses. PureDS4 installs them only when missing, and its own uninstall
deliberately leaves them behind:

- **HidHide** — hides the physical controller so games see only the virtual
  pad.
- **USB-IP** — the transport VIIPER uses to publish a virtual controller.
- **FakerInput** — optional, and not installed by default.

**VIIPER** is shared too, and only one copy can own the backend at a time.
See the handover section below.

## Migrating

### 1. Clear any pending reboot

```powershell
Get-ItemProperty 'HKLM:\SOFTWARE\DS4Windows' -ErrorAction SilentlyContinue |
    Select-Object InfrastructureState, InfrastructureStateUtc
```

If `InfrastructureState` is `RebootPending`, reboot before going further.
Installing or validating on top of a pending driver transaction produces
results that cannot be trusted.

### 2. Close the predecessor completely

Including its notification-area icon. PureDS4 will not close it for you,
and will refuse to start while it runs.

### 3. Install PureDS4

Run the setup executable. Unsigned builds will raise a SmartScreen warning.
The installer adds PureDS4's own files, registry entry, and shortcuts, and
installs VIIPER, USB-IP, or HidHide only if they are absent.

The installer does **not** touch the predecessor. It has no knowledge of it
at all.

### 4. Bring your profiles across

**Tools → Import from DS4Windows.** This copies game profiles into PureDS4.
It never moves, edits, or deletes the originals, and never overwrites a
PureDS4 profile of the same name.

Do this before removing the predecessor if you like, but you do not have to:
uninstalling it leaves `%AppData%\DS4Windows` in place, so the profiles
remain importable afterwards.

### 5. Remove the predecessor

**Tools → DS4Windows removal plan.** This lists exactly what removal
involves — the installed program files, registry key, scheduled tasks, and
each Add/Remove Programs entry with its own registered command — and can
start the uninstaller for you.

PureDS4 does not perform the removal. It starts the uninstall command the
other product registered for itself, so Windows Installer owns the
elevation prompt, the progress UI, the transaction, and the rollback. That
is the same code path as removing it from Windows' Apps list by hand.

Where a product registers several entries — a bundle plus the MSI beneath
it — PureDS4 selects the one that genuinely uninstalls, and declines to
start anything it cannot read as an uninstall command rather than guessing.

Your profiles and settings in `%AppData%\DS4Windows` are not deleted by
this. Removing that folder, if you ever want to, stays a separate manual
decision.

### 6. Finish the handover

- **VIIPER**: if a `viiper.exe` from the old path is still running, PureDS4
  stops and offers an elevated prompt to close it so its own pinned copy can
  take over. Note that a predecessor's VIIPER may offer to update itself;
  PureDS4 pins a specific version by hash, so a self-updated shared copy can
  stop being trusted.
- **Startup**: enable *Run at startup* in PureDS4's settings if you want it
  to launch at logon. This is what creates the `RunPureDS4` task — the
  installer does not create it.

### 7. Verify

Confirm which build is actually running before trusting any result:

```powershell
Get-Process PureDS4, DS4Windows -ErrorAction SilentlyContinue |
    Select-Object ProcessName, Id, Path
```

Then work through `docs/HARDWARE_VALIDATION.md` with the controller.

## Rolling back

Three levels, smallest first.

### 1. Before you have removed the predecessor

Close PureDS4 and reopen the other product, approving its own VIIPER prompt
if it asks. Re-enable `RunDS4Windows` if you disabled it. Its profiles were
never touched. This is the fastest route, and the reason the migration is
ordered with removal last.

### 2. Uninstall PureDS4

*Apps and Features* → **PureDS4** → Uninstall. This removes PureDS4's
program files, registry key, scheduled tasks, and shortcut.

It deliberately does **not** remove:

- HidHide, USB-IP, or FakerInput — a predecessor may still need them;
- `%AppData%\PureDS4`, so a later reinstall finds your settings again.

If you have already removed the predecessor, reinstall it from its own
installer; PureDS4 cannot restore it.

### 3. Full recovery

If neither product works — usually a half-finished driver transaction
rather than an application fault:

1. Reboot first. Many apparent failures resolve here.
2. Confirm the drivers survive:

   ```powershell
   pnputil /enum-drivers | Select-String usbip
   ```

3. Repair each product through its own installer. Each repairs only what it
   owns.
4. Collect evidence before changing more: `%AppData%\PureDS4\Logs`,
   including `startup_failure.log` if present.

## What PureDS4 will never do to another product

- Delete its files, registry keys, or scheduled tasks directly. Removal
  always runs the other product's own uninstaller.
- Write to its configuration directory, or adopt that directory as PureDS4's
  own. This is enforced in code.
- Modify or delete its profiles. Import copies; it never writes back.
- Close it automatically. PureDS4 asks you to close it and re-checks.
