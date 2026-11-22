# DualShock 4 hardware validation

A manual pass over everything the application claims to do with a physical
controller. It exists because build success and a passing test suite say
nothing about hardware: every claim below has to be seen.

Most of it needs no game. The application's own **Controller Readings** page
and two built-in Windows tools cover sticks, triggers, gyro, touchpad, rumble,
lightbar and the virtual pad. Only the controller speaker and the headset
microphone need a trick, and both are described at the end.

## Release hardware evidence

### 26.11.0 candidate acceptance — 2026-11-22

The owner reported "All tested and good to go" for the 26.11.0 candidate after
installing it as an update: profile editor Cancel and Save, Controller Readings
with the DS4 over USB, the new icon in setup, About, taskbar, Start and tray,
and About showing 26.11.0. Read-only checks confirmed that installed
`C:\Program Files\PureDS4\PureDS4.exe` reports 26.11.0 and matches SHA-256
`0EFF3D6DD840C1367C8CFB55643D733A73D4402AC5543AAB5CB24CA8BEC420C7`, the
application built from clean source `5ca896f5e7cef5d1c42b7177bec35c51bbf71210`.
Controller: DS4 v2.1 (owner's only controller), USB for this check.

The full hardware batch ran on the earlier 5.2.0 candidate from `2430483`
(installed 2026-11-08; app SHA-256
`CA1E0ABFF0F9F52C68C0DF845D8451B58BF8380E85FBC94AB5C4C5BC6D6D2947`):

- 2026-11-15, Bluetooth, Xbox 360 output, audio off, game rumble active: a
  session of about 86 minutes (19:24-20:50, battery 55%) with no involuntary
  disconnect; it ended with the controller switched off and the app closed.
  The periodic read stalls recurred on the 15-minute cadence (19:47:03 0.84 s,
  20:02:04 a 3.0 s read timeout with input paused and resumed, 20:17:03 0.75 s,
  20:47:03 0.71 s). The new file-only diagnostics placed every stall in the HID
  read stage; processing, dispatch and feedback stayed near 0 ms and no effect
  write failed. The owner did not notice the 3-second pause in game. An earlier
  session that day at 5% battery (startup timeout, stalls, CRC failures and
  disconnects at 16:48-16:49) is confounded by low battery and is not counted
  either way.
- 2026-11-19: the owner reported the visual pass (light and dark), USB checks,
  managed -> native -> managed routing and profile editing all passed, except
  that the profile editor property pages looked unfinished. The page polish in
  `cfa9690`, `6e51ffe` and `462b049` addresses that; the owner accepted the
  26.11.0 candidate containing it. Its acceptance list above names the editor's
  Cancel and Save, not a recorded page-by-page light/dark review.

The commits from `2430483` to `5ca896f` change only XAML pages, the Controller
Readings code-behind and a converter, one tooltip string, the icon, version
numbers, tests and documentation. No device, mapping, control-service,
protocol or VIIPER source changed, so the hardware results above carry over;
they were not repeated on 26.11.0. Bluetooth was not rechecked on 26.11.0.
Current DS4 speaker/headset audio was not separately confirmed. The 15-minute
Bluetooth stall is not worse than in the preview builds, but its cause is
unidentified and it is unknown whether 5.1.2 shows it. No latency claim is made.

### 5.2.0 owner decision on the no-audio effect experiment — 2026-11-08

The owner chose to revert only the unvalidated experiment (byte 2 of ordinary
Bluetooth effect reports `0x00` instead of `0xA0` when speaker and microphone
are off). 5.2.0 sends the 5.1.2 value again: `0xA1` with the microphone armed,
otherwise `0xA0`. The HID read-safety fix and the file-only pipeline diagnostics
ship as their own changes. The 15-minute Bluetooth input stall stays a known
open investigation: it recurred in preview.4 and preview.6 before the byte
change, so the byte is not its cause, and it is unknown whether 5.1.2 shows it.
No latency or fix claim is made for 5.2.0.

### 5.2.0 private preview.7 Forza Bluetooth result — 2026-10-19

The owner reported a Forza Bluetooth session on the installed preview.7 with
speaker and microphone disabled. The controller did not power off or
disconnect, but input froze, and the virtual pad kept the pressed state
(for example, a held R2) until input resumed. This does not pass the stability
gate. The runtime log, preserved under the ignored
`artifacts/disposable/bt-no-audio-20261015-01/evidence/freeze-20261019/`
(SHA-256 `64E7727E193E9F59CD05477F3EACC80C7BAF7A869667697BCC556080E025B8F6`),
shows active game feedback from about 20:21 to 20:50 (about 28 minutes, short
of the 90-minute target). It records two Bluetooth read stalls, starting at
about 20:30:07.6 (a 3,008 ms read timeout, then retry) and 20:45:07.6
(1,200 ms). Processing, dispatch and effect writes stayed fast, and no effect
write failed.

The stalls recur on a 15-minute cadence, to the same second within a session.
That pattern also appears in the earlier logs: 2026-10-11 at 21:09:15, 21:39:15,
21:54:15, 22:09:15 and 22:24:15 (preview.4), and the 2026-10-13 power-off stall
at 20:33:34 (preview.6). This points to a periodic host-side trigger, not the
removed `0xA0` byte. That trigger is not identified yet. The radio is a USB
`CSR8510 A10` (`VID_0A12&PID_0001`) on Microsoft's generic driver. No Windows
event and no scheduled task with a 15-minute repetition matched the 2026-10-19
stalls. During a stall, the inherited retry policy (`ShouldRetryBluetoothInputAfterTimeout`,
from upstream RC4.2) keeps the last state on the virtual pad for up to the
3-second read timeout per attempt and a 9-second grace. This explains the
held input without explaining the stall itself.

### 5.2.0 private preview.7 installation — 2026-10-15

After separately authorizing the in-place preview.6 -> preview.7 update, the
owner completed setup and left **Launch PureDS4** unchecked. Read-only checks
confirmed all 29 installed payloads against the package manifest below.
`C:\Program Files\PureDS4\PureDS4.exe` reports `5.2.0-ui-preview.7` and matches
SHA-256 `48777392F59C042FF1696289C1D37AD82D4B53CA9C01677DC08C7F5032E0A7A1`.
All eight pre-update Roaming files, including profiles, settings and the
HidHide ownership journal, remain hash-identical to the fresh private backup.

Canonical tasks, the Ready marker, pinned backend/USB-IP executable and driver
hashes, the USB-IP runtime probe and a read-only VIIPER API ping passed. Setup
completed with exit code 0 and no requested restart; see the
[installer checkpoint](INSTALLER_VALIDATION_STRATEGY.md#520-private-preview7-installed-checkpoint--2026-10-15)
for the backup, logs and limits. PureDS4 was not running during this check.

Installation is verified, not Bluetooth stability. Exact-preview.7 controller
readings, virtual/native routing, USB/Bluetooth gameplay, audio transitions,
reconnect/restart and at least 90 minutes of comparable Forza Bluetooth use
remain pending. Keep the no-audio comparison's speaker and microphone modes
disabled, as in the previous failing session, without changing other settings.
Any recurrence needs its approximate time and whether only input stopped or
the physical controller powered off. No commit, push or publication occurred.
The package-only and experiment entries below describe earlier checkpoints.

### 5.2.0 private preview.7 package — 2026-10-15

The owner authorized private package generation only. The installer was built
without launching it or the controller app. No installation, activation, commit,
push or publication occurred. The no-audio experiment below remains unproven
on hardware.

Candidate root: `artifacts/disposable/bt-no-audio-20261015-01/`. Its
`candidate-identity.json` records the dirty source baseline and composition
fingerprint, runtime packs, tests and file identities; `SHA256SUMS.txt` covers
the app, installer and incidental portable archive.

- Installer: `installer/PureDS4_5.2.0-ui-preview.7_Setup_x64.exe`,
  190,076,666 bytes, SHA-256
  `ABC6F97C499917E15E7AACD8C5BEF4EEA20D7FF381F0C24417156D5392794019`.
- App: `x64/Release/PureDS4/PureDS4.exe`, SHA-256
  `48777392F59C042FF1696289C1D37AD82D4B53CA9C01677DC08C7F5032E0A7A1`.
- App and bundle report `5.2.0-ui-preview.7`; the app's numeric file version
  remains `5.2.0.0`. The installer is unsigned.

MSI/Burn composition, release-input/signature checks, managed notices and
isolated installer/launcher/reboot-boundary simulations passed. All 29 portable
payloads match the staged installer manifest; only the app and release marker
differ from preview.6, with the other 27 files byte-identical. Core, Desktop and
apphost remain pinned to .NET 10.0.12 for the app and both setup hosts. The
generated WiX source was restored byte-for-byte after composition.

The final stamped preview.7 managed build passed 162 focused Bluetooth/HID
tests. The 1,172-test standard-version Release source pass below is reused;
three live-process audio capture tests remain excluded. The installed preview.6
and all eight existing non-log Roaming data files remained hash-identical.
Installer execution/layout, real installation/repair/rollback and exact-preview.7
USB/Bluetooth, audio-mode, native-mode and sustained Forza gameplay are still
unperformed. The Bluetooth stability gate remains open.

### 5.2.0 private no-audio effect experiment — 2026-10-15

The owner reported that the DS4 powered off during Forza Bluetooth gameplay on
2026-10-13, on the identified preview.6 below. At 20:33 the input observer
recorded a blocked physical read, a three-second timeout and then Win32 error
1167 (device not connected). Windows recorded an unresponsive Bluetooth HID
device at the same time. The owner manually powered the controller on again;
its appearance at 20:34 was not automatic recovery. This fails the Bluetooth
stability gate and does not establish whether the cause is in PureDS4, the
controller, the radio or the Windows Bluetooth stack. A later removal after
the logged app shutdown at 21:03 is not another spontaneous disconnection.

The authorized local experiment changes only byte 2 of ordinary Bluetooth
effect reports when both audio lanes are disabled: `0xA0` becomes `0x00`.
Speaker-only remains `0xA0`; microphone and duplex remain `0xA1`. Rumble,
lightbar, poll interval, CRC framing, buffer size, handle lifetime, read timeout,
retry grace and diagnostics are unchanged. This isolates one difference from
[schmaldeo 3.9.9](https://github.com/schmaldeo/DS4Windows/blob/722c561c3d7aa65dcf063e716c04ca69e4ca33bd/DS4Windows/DS4Library/DS4Device.cs);
the owner's exact predecessor version and a controlled comparison remain
unconfirmed. The other transport differences are not reverted in this pass.

Local tests use a recording writer without opening HID devices or personal
profiles. They check ordinary, speaker, microphone and duplex effect reports,
audio-to-ordinary transitions, rumble/lightbar preservation, an independent CRC
reference, the four-second keepalive and unchanged USB serialization. This is a
diagnostic hypothesis, not a demonstrated fix. A hardware candidate still needs
packaging and identification, followed by separately authorized installation,
USB/Bluetooth, audio-mode and sustained gameplay checks before the 5.2.0
stability gate can close.

The standard-version Release/x64 build passed 1,172 tests, with the three
opt-in live-process audio capture tests excluded. The focused Bluetooth/HID
pass passed 162 tests. An earlier run stamped as preview.7 failed only the
unchanged public-version assertion; the standard-version rerun above passed.
No installer was produced or activated in this pass.

### 5.2.0 private preview.6 installation — 2026-10-13

The owner completed the authorized preview.4 -> preview.6 update after a
controlled retry using the installer's default log location. Read-only checks
confirmed all 29 installed payload files against the identified preview.6
manifest and all eight existing Roaming data files against the private backup.
The installed `C:\Program Files\PureDS4\PureDS4.exe` reports
`5.2.0-ui-preview.6`, SHA-256
`ADDE80041AA3D42C8A2292E06930AFF5AAE91C2294FEE0DE33D968A9585D0CE0`.
Backend identity, canonical enabled tasks, the Ready marker and a read-only
VIIPER API ping passed. See the [installer checkpoint](INSTALLER_VALIDATION_STRATEGY.md#520-private-preview6-installed-checkpoint--2026-10-13)
for the earlier MSI logging failure, secondary restore error and evidence limits.

PureDS4 was not running at that post-install check. USB and Bluetooth readings,
virtual-pad input, gameplay/rumble, disconnect/reconnect, app restart, managed
-> native -> managed routing and sustained Bluetooth use have not yet been
accepted on this candidate. Installation success does not close the original
Bluetooth-freeze investigation or authorize publication.

### 5.2.0 private Bluetooth diagnostic follow-up — 2026-10-12

An independent 250 ms diagnostic observer samples the current Bluetooth input
stage and actual VIIPER state-write progress. An activity still in flight after
one second produces a file-only incident without waiting for that activity to
return; observed progress produces one paired recovery record. Repeated
incidents in the same stage are limited to one per 30 seconds. Recovery timing
is sampled, not an exact freeze duration or proof that a game received input.
Input incident context includes speaker/microphone mode and effect counters,
but no input values or controller identifiers. The observer takes none of the
input/output/audio/UI locks and performs no recovery action.

VIIPER observes pending or in-flight work without successful write progress,
not equality of submitted/written counts: superseded states are intentionally
coalesced. Idle output, native-mode retirement and old worker generations must
not be diagnosed as stalls. This instrumentation changes no HID timeout,
transport, output packet, profile, grace period, UI or neutral-state policy.

The original hardware freeze remains unexplained. Candidate preparation and
disposable tests are not installed-app or physical-controller acceptance.
After authorized installation of the identified candidate, batch the existing
USB/Bluetooth input/output/gameplay/reconnect/restart/native round trips, then
observe at least 90 minutes of comparable Bluetooth use. Record the game,
audio mode, duration, any input loss/stuck output and relevant incident stages.
No input loss, stuck output or unexplained pipeline incident is the practical
acceptance target; this duration is not a statistical confidence claim or proof
of broader compatibility. Any recurrence keeps the stability gate open and
must be correlated with the exact artifact before changing behavior.

### 5.2.0 private UI dogfood — Bluetooth input freeze, 2026-10-11

The owner accepted the UI and reported that the requested initial interactions
worked, but subsequently observed intermittent Bluetooth input loss while the
controller remained connected in Windows and PureDS4. This is not a passing
Bluetooth stability gate. The owner reported no equivalent interruption with
their previous schmaldeo DS4Windows installation; no controlled comparison or
exact predecessor version was supplied.

The installed file at `C:\Program Files\PureDS4\PureDS4.exe` reports
`5.2.0-ui-preview.4`, SHA-256
`2091E393DDAA6C2111A107A8EAA919F9F17F80D059719CEFD92DAEA741E973A3`.
Its startup log identifies that preview; the elevated running process's path
was unavailable to the read-only query. Logs on 2026-10-11 show two 3-second
input-read pauses at 21:09:15 and 21:39:15 without controller removal. A Windows
Bluetooth-adapter command timeout was also recorded earlier, not at either
pause. Neither observation establishes the underlying cause.

Local changes preserve a successful read that completes during cancellation,
distinguish a failed completion from a recoverable timeout, and drain pending
reads before releasing their native buffers. Incident-only diagnostics measure
HID reading, processing, report dispatch and feedback separately, without input
values or controller identifiers. They do not change the read timeout, removal
grace, feedback transport, profiles or UI. Automated native byte-pipe and
diagnostic tests do not demonstrate recovery of the reported hardware freeze.
New-candidate USB/Bluetooth acceptance and sustained Bluetooth gameplay remain
pending; this pass does not qualify 5.2.0 for release.

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
These unchanged packages were published as
[PureDS4 5.1.2](https://github.com/meiameiameia/PureDS4/releases/tag/v5.1.2);
unauthenticated downloads matched the frozen hashes.

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
Home capture showed DS4 v2 over Bluetooth, Protected, Default, Xbox 360 output,
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
