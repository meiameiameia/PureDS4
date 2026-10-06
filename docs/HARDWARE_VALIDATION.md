# DualShock 4 hardware validation

A manual pass over everything the application claims to do with a physical
controller. It exists because build success and a passing test suite say
nothing about hardware: every claim below has to be seen.

Most of it needs no game. The application's own **Controller Readings** page
and two built-in Windows tools cover sticks, triggers, gyro, touchpad, rumble,
lightbar and the virtual pad. Only the controller speaker and the headset
microphone need a trick, and both are described at the end.

## Release hardware evidence

### 5.1.2 launcher hotfix — 2026-10-05

The owner completed the in-place 5.1.1 -> 5.1.2 update and reported passing
Windows restart without another repair, followed by the requested readings,
gameplay, disconnect/reconnect and app close/reopen checks separately over
USB and Bluetooth. This is owner-reported acceptance on the same one-PC DS4
setup, not an automated gameplay result or validation on the reporter's PC.
No new game names, controller models or screenshots were supplied.

Frozen source: `42ab4041d5d7ef40b17b63134cce331e7b2fee53`.
Installer SHA-256:
`FD6A49D0BB20FFAE96FBBBC210C8C1A1F2855988B9FFB6211351CEA3EF32E936`.
Installed app version 5.1.2.0 and SHA-256:
`A997671DAE7F0CB18BEF69969B8998D75761FE2078134087ABEDA8FC7A0B26A0`.
All 29 installed payload files matched the frozen manifest after the independently
observed Windows restart. The Ready infrastructure marker, canonical task
definitions/current-account identities and live VIIPER readiness ping passed.
The app was closed when the later read-only process query ran.

The [installer checkpoint](INSTALLER_VALIDATION_STRATEGY.md#512-installed-candidate-checkpoint--2026-10-05)
records package hashes and update/data-preservation checks. Clean full-bundle
installation, actual failure rollback, interrupted-install reboot/resume,
sleep/wake and physical backend interruption remain unobserved. Prior 5.1.1
macro/native-mode results below are retained as earlier evidence, not relabelled
as another 5.1.2 hardware pass. The hotfix adds no new controller or output type.

### 5.1.1 in-place upgrade — 2026-10-04

The owner completed the authorized `5.1.0` → `5.1.1` update using the frozen
candidate from application source `5a14defb05992250b73cb6f2e6bf0b9ffe438952`.
Installer SHA-256:
`0B83EC6A74D1E119D621AAF426D68816C6A732C4D86DBD8D06A5DBB8411F059D`.
All 29 installed application payload files matched the candidate manifest.
The installed file at `C:\Program Files\PureDS4\PureDS4.exe` reports `5.1.1`
and SHA-256 `3A742C470678C25480DE98D3F710397BBDF65C381F1F551CC011EA258077EC50`.
Default remained byte-identical to its recovery copy.

Read-only post-install checks confirmed the new canonical backend hash/version,
`Ready` infrastructure registration, canonical Program Files task actions,
USB-IP `0.9.7.7` executable and active driver hashes, a successful USB-IP port
ABI probe, and a successful backend API ping. Both API/USB-IP listeners were
observed only on `127.0.0.1` (ports 3242/3241). No real failure was injected.
The elevated backend process's executable path was unavailable to the read-only
process query; this does not claim direct process-path verification.

The owner subsequently reported that the requested grouped checks passed,
separately over USB and Bluetooth: controller readings and virtual-pad input
in `joy.cpl`, familiar-game gameplay/rumble, disconnect/reconnect with the
game open, app close/reopen, and managed → native → managed routing. No game
names or new screenshots were supplied for this pass. This is owner-reported
one-PC acceptance of this frozen candidate, not broader compatibility proof.
The installed file hash was rechecked and remained the candidate hash above;
the app was not running at that later process query.

The owner authorized one temporary profile for the delayed held-input macro
cancellation check. `Teste-macro-5.1.1.xml` was created from the public test
fixture in `PureDS4.Tests/ProfileTests.cs`, not from personal Default. Triangle
holds F24 for 10 seconds, releases it, waits 2 seconds, then pulses F23 for
1 second (`135/10300/135/2300/134/1300/134`). Program launch and special actions
are empty; gyro/touchpad output is disabled. XML structure and the supported
enum values were checked; Default and the installed app hashes are unchanged.
At preparation time, the profile had not yet been activated or exercised with
the controller; the completed result follows below.

A disposable observation panel was prepared to poll only F23/F24, without
input injection or other-key recording. Its PowerShell syntax check passed,
but launching the script was refused by the existing Windows script policy.
No policy, permission or security setting was changed, no observer process
was started, and no hardware result was claimed at that point. The planned check:
first demonstrate normal execution, then trigger a second macro and switch
to native mode during the held F24. F24 must release before its normal delay
ends, and no additional F23 pulse may follow. Restore Default afterwards.
No physical backend failure was forced or observed.
Clean full-bundle installation, actual rollback and reboot/resume remain the
explicit evidence gaps accepted separately for 5.1.1.

The owner then authorized a temporary executable observation panel, without
installation or a security-policy change. A 17,920-byte WinForms helper was
compiled with the existing Windows .NET Framework compiler and opened from
`artifacts/disposable/macro-acceptance-5.1.1/Observe-TestKeys.exe`. Its five
fixture checks passed (normal counters, cancelled-sequence counters, bounded
event retention, polling-gap detection and snapshot serialization); these
check the observer, not production macro cancellation. The panel queries only
F23/F24 current state and records only those events and elapsed times in the
same disposable directory. It does not inject input, capture other keys, or
control another application's UI. Default and installed app hashes remain
unchanged. The panel was left open for the authorized owner-assisted check;
its closure and the completed acceptance are recorded below.

The owner subsequently reported passing the normal/cancelled macro check.
The observer's recorded normal sequence held F24 for 10.009 seconds, waited
2.004 seconds, and pulsed F23 for 1.002 seconds. The second sequence released
F24 after 4.029 seconds during the owner-reported native transition; no second
F23 pulse occurred through the remaining 32.076-second observation window.
Both keys were released, counters were 2/2 for F24 and 1/1 for F23, the maximum
polling gap was 132 ms, and no observer error was recorded. The running
application path was the canonical installed `PureDS4.exe` identified above.
The owner then restored Default and closed PureDS4. Saved Controller1 selected
Default and no longer referenced the test profile; its file hash stayed unchanged.
The observer was already closed. Only the task-created test profile was moved
to the disposable directory, preserving its bytes for recoverable cleanup.
The frozen candidate's requested USB/Bluetooth and macro acceptance is complete;
this remains one-PC evidence, not a physical backend-interruption or broader
installation/audio/controller compatibility pass.
These unchanged candidate bytes were subsequently published as
[PureDS4 5.1.1](https://github.com/meiameiameia/PureDS4/releases/tag/v5.1.1).

### Published 5.1.0 candidate

The published `5.1.0` candidate was built from clean commit
`8999108763aa09c5478c8713f7fd62d99cf7ebb5`. On 2026-09-30 the owner completed
an in-place same-version update using installer SHA-256
`75B3F8516870A9473AF8E1684C00AFE1446067327AA642497C22EBBB68AE58F5`.
All 29 installed application payload files matched its manifest; the installed
`C:\Program Files\PureDS4\PureDS4.exe` SHA-256 was
`605592D61617E0D6A1276823120F4C781997C5AE4DFF46D7D691B0B263B4016D`.
The owner's Default profile remained byte-identical, and installed infrastructure,
VIIPER API response and canonical task actions were verified. The submitted
Home capture showed DS4 v2.1 over Bluetooth, Protected, Default, Xbox 360 output,
Ready and Service running. That capture does not independently prove gameplay.

The earlier USB/Bluetooth gameplay evidence below is retained for the unchanged
controller/output behavior; it is not relabelled as a new full hardware pass.
The later changes concerned setup terms/notices, test coverage and previously
accepted UI/startup status, not a new backend binary or mapping implementation.
The installer/download identities and SmartScreen observation are recorded in
the [installer evidence checkpoint](INSTALLER_VALIDATION_STRATEGY.md#evidence-checkpoint--2026-10-03).
On 2026-10-03 the installed app and Default hashes still matched those recorded
above. No new hardware test or installation was performed for that check.

### Earlier UI and setup observations

On 2026-09-29, the owner accepted the installed internal UI candidate built
from `49ba5d244ad975d823299489114216fb02cfb7b3` plus the uncommitted
Home/readings layout and startup-status changes. The installer SHA-256 was
`EC23692C0FFB69FE374588ACA9F9DAEABA75191673760F5580E88D53C6885288`;
the installed executable at `C:\Program Files\PureDS4\PureDS4.exe` matched
`C478BE946670C2E4E7A3F9D83FE4215B9A9FB572564DE1B0B8C3A058F11C6061`.
The owner passed the focused checks for Ready Xbox 360 output, maximized Home
placement, and closing/reopening with the DS4 still connected. The submitted
capture showed Bluetooth, Protected, Ready, and no game-output warning;
logs confirmed Bluetooth virtual-output association after reopening.
The Default profile remained hash-identical to its preserved backup. This
UI/startup pass did not repeat gameplay or establish new USB gameplay proof.
The later installer-only HidHide presentation candidate was also installed on
2026-09-29, with installer SHA-256
`62B6009E86822C62A5F07A38E6DF3E512A674473A3020EB06A5AC5D1D6312235`.
Its application executable remained identical to the accepted UI/startup
candidate. Setup logs confirmed successful completion and no HidHide
installation or repair; the owner then authorized commit/push of the changes.
This subsequent package replacement adds no new gameplay or hardware proof.

### Earlier USB and Bluetooth gameplay baseline

On the owner's Windows machine, the installed `5.1.0` candidate from commit
`49ba5d244ad975d823299489114216fb02cfb7b3` was identified by its
installer SHA-256
`BB64543954ED38F1EEDC36C3A744C16B6053B5FC629D0EDCF6636482C37A7D49`
and installed-app SHA-256
`ACD84DBDA1EAE2AA31D97A4DFD47EDB4FB76196420BAD44454761CF3F4B39B10`.
The running process path was `C:\Program Files\PureDS4\PureDS4.exe`.

The owner reported a Ready Xbox 360 output, responsive virtual-pad buttons
and axes in `joy.cpl`, gameplay, disconnect/reconnect with the game open, and
continued gameplay after restarting PureDS4, separately over USB and
Bluetooth. The games used for this candidate were not named. Application
logs confirmed virtual-output association after the observed reconnections
and app restarts. This is one-machine acceptance of those specific flows, not
a claim that every controller or Windows configuration works. Managed/direct
round trips and additional DS4 features were exercised on the preceding
`a48e467` candidate, not repeated on `49ba5d2`.

No VIIPER API timeout or output-binding failure occurred during this pass.
Therefore it confirms normal reconnection, but does not prove that the
bounded retry handles the original transient failure in vivo. Deterministic
tests cover temporary binding failure, later success, cancellation and exhaustion;
they do not establish an actual backend interruption. Controlled
backend loss, sleep/wake, two simultaneous DS4 controllers, and the broader
installation/recovery matrix are not covered by this evidence. Do not force
a backend outage on the owner's daily-driver PC merely to fill that gap.

### Backend-failure acceptance criterion

On 2026-09-29 the owner approved replacing mandatory physical reproduction of
the intermittent VIIPER failure with deterministic software checks, while
retaining the missing physical evidence explicitly. This is a bounded exception
for that runtime failure, not a waiver of normal exact-artifact USB/Bluetooth
acceptance or the installer's Windows installation/recovery matrix.

The required software checks are:

- Transient backend failure followed by success publishes one connected output,
  never a failed or partially attached slot.
- Persistent failure stops after the configured retry schedule. With a known
  binding failure and no primary output, the direct-mode entry policy permits
  releasing the physical controller in the required teardown order.
- A pending retry is cancelled before switching to direct mode and cannot
  recreate a virtual output afterwards. If returning to managed mode fails,
  successful rollback preserves direct mode; later backend availability allows
  managed output to be established again.
- If transition and rollback both fail, readiness is withheld and another
  transition is refused until recovery. Cleanup failure prevents restart;
  recovery success requires the original controller and its connected requested
  output, not merely successful service startup.

`PureDS4.Tests/VirtualOutputRecoveryScenarioTests.cs` composes the production
retry sequence, output-slot manager, exposure coordinator, entry policy and
recovery postcondition with simulated backend and physical operations. Its four
scenarios and the existing retry, exposure/recovery and backend-probe tests
passed locally (85 focused tests). No runtime code or backend binary was changed.
These checks do not run the complete `ControlService` with Windows drivers,
prove HidHide permissions, or guarantee Bluetooth/Steam re-enumeration. The
physical interruption remains **not observed**, rather than passed or an
indefinite component-input blocker. A future observed failure must still be
investigated; this exception never excuses a known unresolved safety defect.

## Before starting

1. Confirm which build is running. This matters more than it sounds: an
   installed copy under `C:\Program Files` — of PureDS4, or of a DS4Windows or
   DS4Windows Reworked install that has not been removed yet — will be launched
   instead of the one you meant to test.

   ```powershell
   Get-Process PureDS4, DS4Windows -ErrorAction SilentlyContinue |
       Select-Object ProcessName, Id, Path
   ```

   The path must be the build under test.

2. Record the transport. Bluetooth and USB are separate paths and both need a
   pass. The Home row and the controller detail view both show which is in use.

3. Note the profile. Gyro, touchpad and lightbar behaviour all depend on the
   active profile, so a "failure" that is really a profile setting is the most
   common false alarm.

## Part 1 — no game required

### Sticks, triggers, gyro, touchpad

Open **Profiles → edit a profile → Controller Readings**. The page shows live
input and output values side by side.

| Move | Watch | Expected |
|---|---|---|
| Left stick | `LX (I)` / `LY (I)` | swings to the extremes and returns near centre |
| Right stick | `RX (I)` / `RY (I)` | same |
| L2 / R2 | `L2` / `R2` | 0 at rest, full at bottom of travel |
| Tilt and rotate the controller | `X` / `Y` / `Z` | values change smoothly with motion |
| Drag a finger on the touchpad | `TX` / `TY` | coordinates track the finger |

The `(I)` column is what the controller reports; `(O)` is what the profile
produces after dead zones and curves. If input moves and output does not, the
profile is responsible, not the device.

Gyro is worth a moment: the axes should respond to rotation, not only to
shaking, and should settle when the controller is still. Drift while
stationary is a calibration problem, not a mapping one.

### Rumble

In the profile editor, use **Test Heavy** and **Test Light**. They drive the
two motors independently. Both should be felt distinctly: heavy is the
low-frequency motor, light is the faster one.

Rumble is the most likely casualty of the controller-family removals, because
the feedback path was rewritten when the DualSense output was dropped. Test it
before assuming a game is at fault.

### Lightbar

Open the **Lightbar** section and set a colour. The change should appear on the
controller immediately, without saving or reconnecting. Try two distinct
colours so a stuck value is obvious.

### The virtual controller

Run the Windows game controller panel:

```powershell
joy.cpl
```

The virtual pad created by the application appears in the list. Open its
properties and the live view shows axes and buttons. Every physical input
should move the corresponding virtual control. This is the honest test of the
whole chain — physical HID in, mapping, virtual pad out — and it needs no game.

If the virtual pad is missing here, nothing in a game will work, and the
problem is output creation rather than mapping.

## Part 2 — in a game

The panel above proves the pad exists and responds. A game proves the timing
and the feel. Use a game you know well, so wrong behaviour is obvious.

- Sticks and triggers behave as they did before.
- Rumble fires on the events you expect, at a sensible strength.
- No input lag or stutter that was not there before.

Then, still with the game running:

- Disconnect the controller and reconnect it. It should return to working
  without restarting the application.
- Close and relaunch the application. The controller should come back.

For the installed candidate, also check the VIIPER process lifecycle: with
PureDS4 closed before login and app startup disabled, Windows must not launch
VIIPER on its own; opening PureDS4 and requesting game output must start the
backend and restore the virtual pad. No VIIPER tray icon should appear. If app
startup is enabled, PureDS4 may start at login and then request the backend;
the backend still must not have its own logon trigger.

## Part 3 — the awkward two

### Headset microphone

Plug a headset into the controller's 3.5 mm jack, then enable
**Enable headset microphone input** in the controller detail view.

Open Windows sound settings:

```powershell
mmsys.cpl
```

On the **Recording** tab, find the controller's microphone device and speak.
The level meter beside it should move. That is the whole test: if the meter
responds, capture, transport and the noise-suppression stage are all working.

If the device is missing entirely, the microphone was never armed; if it is
present but the meter is dead, the capture path is at fault.

### Controller speaker

The DualShock 4 speaker is small and quiet, which makes "I hear nothing" an
ambiguous result. Split it into two steps so the answer is never ambiguous.

**Step 1 — prove the audio chain with the headset.** Plug an earphone into the
controller jack. Enable **Stream audio to controller**, pick an audio source,
tick **Headset only (mute controller speaker)**, and play something.

If you hear it in the earphone, the entire chain works: capture, encoding,
Bluetooth transport, and the controller's audio hardware. That is almost all of
it.

**Step 2 — move the same audio to the speaker.** Leave everything playing and
untick **Headset only**. The audio now routes to the controller's built-in
speaker instead of the jack. Hold the controller near your ear; it is quiet but
clearly audible in a quiet room.

Because step 1 already proved the chain, a failure at step 2 is narrow: the
routing bit that chooses speaker over jack, not the audio pipeline.

Unplugging the earphone is a useful third check — the controller should fall
back to its speaker on its own.

> The controller speaker only carries what the application streams to it. It is
> not a Windows output device, so nothing appears for it in `mmsys.cpl`, and
> system audio does not reach it unless **Stream audio to controller** is on.

## Reporting a failure

Record, for anything that fails:

- the executable path from the check at the top;
- the transport, Bluetooth or USB;
- the profile in use and the emulated controller it selects;
- what you saw, against what this document says you should have seen.

The distinction that saves the most time is **input versus output**: Controller
Readings shows both, and knowing which side stopped tells you whether the
device, the mapping, or the virtual pad is at fault.
