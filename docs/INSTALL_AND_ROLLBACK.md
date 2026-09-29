# Installing, upgrading, and rolling back PureDS4

## Upgrading an existing PureDS4 installation

The first public-version target is `5.1.0`. The last full USB/Bluetooth gameplay
baseline was the `5.1.0` candidate from commit `49ba5d2`, not `5.1.0-beta.2`.
The beta-to-stable upgrade first passed with candidate `9c17446`; the later
`a48e467` and `49ba5d2` installers each replaced a same-version installation
successfully.
These are **in-place PureDS4 upgrades**, not the DS4Windows-to-PureDS4
replacement described below. The bundle and MSI share PureDS4's upgrade
codes, and the beta MSI already used numeric version `5.1.0`. These observed
passes are limited to one Windows machine and do not prove the remaining
clean-install or failure/recovery matrix.

The `49ba5d2` installer SHA-256 is
`BB64543954ED38F1EEDC36C3A744C16B6053B5FC629D0EDCF6636482C37A7D49`.
The installed executable matched the candidate's SHA-256, and the owner's
current `Default.xml` profile remained hash-identical to its existing backup.
That backup was created before the `a48e467` installation; no new full-data
backup was made for `49ba5d2` because the owner only required protection of
the current Default profile. The old installer registrations were removed,
and the canonical tasks and backend readiness were verified. The previous
installer was retained for recovery; no rollback was performed.

On 2026-09-29, a later internal UI/startup candidate replaced that installation
with installer SHA-256
`EC23692C0FFB69FE374588ACA9F9DAEABA75191673760F5580E88D53C6885288`
and installed-app SHA-256
`C478BE946670C2E4E7A3F9D83FE4215B9A9FB572564DE1B0B8C3A058F11C6061`.
It was built from the same full source baseline plus uncommitted UI changes;
it is not a new committed release milestone. Setup completed successfully,
the canonical task actions remained under Program Files, and the current
Default profile still matched its existing backup. Burn detected HidHide
1.5.230 and planned no HidHide installation or repair. The owner accepted a
focused Bluetooth startup/reopen and maximized Home check; see
[hardware validation](HARDWARE_VALIDATION.md) for the evidence limits.

The subsequent installer-only HidHide presentation update also completed on
2026-09-29. Its installer SHA-256 was
`62B6009E86822C62A5F07A38E6DF3E512A674473A3020EB06A5AC5D1D6312235`.
The application executable and Default profile were unchanged, both canonical
task actions remained correct, and the infrastructure/API check completed
successfully. Burn again planned no HidHide installation or repair. The
update's UI lists an existing HidHide installation rather than showing its
install checkbox. This is a same-version replacement on the owner's PC,
not evidence for clean-install or failure/recovery behavior.

Use the **exact, source-identified installer intended for distribution**, not
an earlier disposable build. Before running it, record its SHA-256 and the
installed version, verify the outgoing installer is recoverable,
preserve a copy of PureDS4's settings/profiles and ownership journal, inspect
running processes and canonical startup tasks, and check for a pending
infrastructure reboot. Close the app and obtain normal elevation; do not
disable Windows protections. Afterward verify the installed executable and
registration, both task actions, preserved settings/profiles, infrastructure
readiness, controller visibility, virtual and native modes, and a restart.
Exercise repair and failure/recovery scenarios on a separate disposable
Windows installation; do not deliberately break the owner's daily-driver PC.

Do not treat a previous installer as an automatic downgrade: rolling back
may require uninstalling the current version and then reinstalling the
preserved package. That is a separate, owner-approved
host change, followed by restoring only the backed-up PureDS4 data that the
rollback needs. If any identity or recovery prerequisite is missing, stop
before the in-place upgrade rather than experimenting on the current install.

## Migrating from DS4Windows

PureDS4 replaces DS4Windows. It is not a companion to it, and running both
is not a supported configuration: DS4Windows supports everything PureDS4
supports and more, so keeping both installed gains nothing and leaves two products
competing for the same controller.

PureDS4 exists for people who want the narrower tool — DualShock 4 hardware,
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

They cannot both remain active owners. On every launch PureDS4 requires a
running `DS4Windows` process to close, then blocks controller, HidHide, and
VIIPER activation while a predecessor executable, uninstall registration,
or the `RunDS4Windows`/`RunVIIPER` tasks remain. Preserved profiles and an
uninstalled registry tombstone or empty install directory do not block
activation because none can start the old runtime.

## Shared components

The machine-wide infrastructure is shared with other controller tools.
PureDS4 verifies the identities it needs, installs or repairs its pinned
prerequisites when required, and its own uninstall deliberately leaves shared
drivers behind:

- **HidHide** — hides the physical controller so games see only the virtual
  pad.
- **USB-IP** — the transport VIIPER uses to publish a virtual controller.

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

Run the setup executable from the official source. The first public release
may be unsigned: compare its SHA-256 with the published hash before running
it. SmartScreen or Smart App Control may warn or block an unfamiliar download;
the absence of a warning on one PC does not establish trust for other users.
Do not disable Windows protections to install it.
The installer adds PureDS4's own files, registry entry, and shortcuts, and
installs VIIPER, USB-IP, or HidHide only if they are absent.

The installer does **not** touch the predecessor. It has no knowledge of it
at all.

### 4. Bring your profiles across

**Tools → Import from DS4Windows.** This copies game profiles into PureDS4.
It never moves, edits, or deletes the originals, and never overwrites a
PureDS4 profile of the same name. A conflicting profile is imported under a
`(DS4Windows)` suffix; reopening the importer recognizes that copy rather
than duplicating it.

The replacement gate completes before the main Tools page opens. The old
uninstaller leaves `%AppData%\DS4Windows` in place, so the preserved profiles
remain importable after removal.

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
- **Startup**: a standard installation may create the canonical
  `RunPureDS4` logon task and `RunPureDS4VIIPER` elevated on-demand task.
  The VIIPER task has no logon trigger; PureDS4 runs it during its own
  readiness/output flow. An upgrade replaces the old VIIPER logon-task shape with
  this verified launcher while preserving the installer-owned target and
  account. PureDS4's startup preference controls only its own verified task;
  it must not adopt a task belonging to another installation.

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

- HidHide or USB-IP — a predecessor may still need them;
- a FakerInput driver installed by an older PureDS4 or DS4Windows build —
  current PureDS4 neither uses nor distributes it, and removing a shared
  system driver requires a separate, explicit decision;
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
