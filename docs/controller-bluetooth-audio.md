# DualShock 4 Bluetooth audio

## Scope

PureDS4 supports DualShock 4 and DualShock 3 only. DualShock 4 speaker and
headset-jack audio are native DS4 features and are kept. The DualSense feedback
paths this document previously described — adaptive triggers, voice-coil
"advanced haptics" over Bluetooth HID `0x32`, and the 48 kHz Opus speaker
stream on HID `0x35` — are out of product scope and are being removed. Do not
treat any DualSense instruction as a supported path.

| Path | Source | Physical controller transport |
| --- | --- | --- |
| Rumble, lightbar, LEDs | DS4 HID output report | normal DS4 HID output (`0x11` on Bluetooth) |
| Controller speaker audio | selected Windows render endpoint, including VIIPER's virtual `Wireless Controller` endpoint | Bluetooth DS4 audio report, 32 kHz SBC |
| Controller microphone | physical DualShock 4 SBC microphone frames | VIIPER virtual DualShock 4 16 kHz mono UAC capture endpoint |

Controller audio requires a **genuine Sony DualShock 4 over Bluetooth**. The
profile editor states this directly rather than offering a control that cannot
work: USB and non-Sony pads report why the microphone toggle is unavailable.
DualShock 3 has no controller audio at all.

## In-game setup

1. Connect a DualShock 4 over Bluetooth.
2. Create a PlayStation game output for it so VIIPER exposes the virtual
   `Wireless Controller` audio interface.
3. In the profile's **Controller audio** section, enable **Stream audio to
   controller**, or **Headset only** to route it exclusively to the 3.5 mm jack.
4. Select the virtual `Wireless Controller` render endpoint as **Audio source**.
5. In the game, select that same endpoint when the game offers a
   controller-audio output choice.

The virtual audio endpoint is created by VIIPER's USB Audio Class function.
PureDS4 does not create a fake Windows audio endpoint in user mode: Windows
audio endpoints require a driver-backed device interface, so creating one
separately would require an installed, signed virtual audio driver.

## Bluetooth speaker processing

The profile can optionally process the physical Bluetooth controller-speaker
stream before SBC encoding:

- **Dynamic range: Balanced** raises quieter detail while restraining loud
  effects.
- **Dynamic range: Strong** applies a narrower range for larger volume
  differences.
- **Bass/body boost** adds 0-6 dB around 200 Hz and filters unusable sub-bass
  below 70 Hz.

The processor is stereo-linked and bufferless, so it adds no look-ahead frame
or transport latency. **Off** and **0 dB** preserve the original PCM path.

## Protocol basis

This is an independent implementation built from public HID descriptors,
documented Sony report formats, and hardware traces captured during
development. The speaker, microphone, state, and lightbar lanes are owned by
one PureDS4 transport so their ordering remains deterministic.

## Diagnostics

If the Windows `Wireless Controller` audio endpoint has an error state, remove
stale VIIPER devices, restart VIIPER, then recreate the output. Windows can
retain an old failed device instance until the virtual device is recreated.

None of the behavior above is claimed as validated on current hardware. See
[`HARDWARE_VALIDATION.md`](HARDWARE_VALIDATION.md) for what a claim requires.
