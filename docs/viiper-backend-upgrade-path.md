# VIIPER backend architecture

VIIPER is the only virtual-controller backend PureDS4 uses. It exposes the two
supported game outputs, Xbox 360 and DualShock 4, through usbip-win2 as
complete USB devices, including the DualShock 4 audio interfaces.

## User setup

PureDS4 checks VIIPER and usbip-win2 at startup. When either component is
missing, the app offers its bundled self-elevating setup. Setup installs both
components and registers an exact, enabled, highest-privilege
`RunPureDS4VIIPER` on-demand launcher (without a logon trigger) and the
`RunPureDS4` app logon task when app startup is enabled. Once
the USBIP ABI is ready, setup starts the server and verifies its local API.
Settings also provides Install / Repair and Refresh actions.

The elevated tasks are rooted explicitly at `\RunPureDS4VIIPER` and
`\RunPureDS4`. Windows never starts VIIPER independently at login; PureDS4
invokes its verified launcher when game output needs the backend. The VIIPER
executable and managed PureDS4 recovery copy
live in the dedicated, protected `%ProgramFiles%\PureDS4` tree; the
installer never rewrites access controls on an arbitrary ZIP extraction
directory.

Install / Repair copies a managed recovery build into Program Files, but its
`RunPureDS4` task points to the exact executable that launched setup. If the
user later opens a different portable copy, PureDS4 asks once for
administrator confirmation and retargets that task to the newly chosen
portable executable. Installing VIIPER remains independent of where that
portable package was extracted.

## Profile migration

The retired serialized values `X360` and `DS4` remain readable solely for
backward compatibility. They normalize immediately to `ViiperX360` and
`ViiperDS4`; new saves never write the retired values.

## Runtime containment

PureDS4 records locally created VIIPER Sony interfaces before normal HID
enumeration and rejects them as physical inputs. Moonlight/Sunshine virtual
controllers use a separate opt-in admission policy, so accepting streamed
controllers cannot make PureDS4 recursively ingest its own output.

## Feedback and audio

VIIPER feedback is read by `ViiperOutDevice` and routed to the currently bound
physical controller. Xbox/standard rumble, Sony lightbar output, adaptive
triggers, advanced haptics, speaker playback, and microphone capture are
translated according to the physical controller's capabilities.
