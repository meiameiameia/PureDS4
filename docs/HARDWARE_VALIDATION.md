# DualShock 4 hardware validation

A manual pass over everything the application claims to do with a physical
controller. It exists because build success and a passing test suite say
nothing about hardware: every claim below has to be seen.

Most of it needs no game. The application's own **Controller Readings** page
and two built-in Windows tools cover sticks, triggers, gyro, touchpad, rumble,
lightbar and the virtual pad. Only the controller speaker and the headset
microphone need a trick, and both are described at the end.

## Current internal candidate evidence

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
bounded retry handles the original transient failure in vivo. Isolated tests
cover temporary binding failure, later success, cancellation and exhaustion;
they do not replace an actual backend interruption. Controlled
backend loss, sleep/wake, two simultaneous DS4 controllers, and the broader
installation/recovery matrix are not covered by this evidence. Do not force
a backend outage on the owner's daily-driver PC merely to fill that gap.

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
