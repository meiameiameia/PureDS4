# Distributed component provenance

This inventory is the release authority for third-party binary inputs. The
machine-readable contract is [`installer/release-inputs.json`](../installer/release-inputs.json);
`utils/validate-release-inputs.py` verifies its file hashes, package lock,
license notices, project inventory, and expected Authenticode signers.

A matching version or tag is not sufficient evidence by itself. Release
inputs are accepted only when their exact local bytes match the recorded
official asset or source revision. The normal validator allows a known blocked
component to remain in internal builds; `--require-release-ready` fails closed
while any blocker exists.

## Pinned release inputs

| Component | Source and binary evidence | License / notice |
| --- | --- | --- |
| PureDS4 VIIPER backend 0.1.0-pureds4.1 | [PureDS4-VIIPER](https://github.com/meiameiameia/PureDS4-VIIPER) commit `c472717d2eed950d8aab3eab80edc87400a75ae8`, descended from `hbashton/VIIPER` v0.1.0. Its clean-tree Windows build is `extras/VIIPER-0.1.0-pureds4.1-x64.exe` (SHA-256 `67559205427F18849E16B0A8329E6270D9A42E4AA155ACA9FD5EA8574A8A01C8`), with embedded Go revision and Windows product version. The binary is unsigned; the pinned source revision, build procedure, and local hash are the identity proof. | The build-generated dependency notices are packaged as `extras/VIIPER-0.1.0-pureds4.1-LICENSES.txt`; the GPL program license remains in the source fork and PureDS4's `COPYING`. |
| usbip-win2 0.9.7.7 | `vadimgrn/usbip-win2` tag `v.0.9.7.7`, commit `7c219953101cc5d0ec9a0bcb3eb87259cf72bedd`; the official release asset exactly matches `extras/USBip-0.9.7.7-x64.exe` (`51620FA5F9F8BE5932BC9D786DEEE557CE06D5407A99CAB490DCFAC71F185FEA`) and has the pinned valid Cloudyne Systems signer. | BSD-2-Clause text in `extras/USBip-0.9.7.7-LICENSE.txt`. |
| HidHide 1.5.230 | `nefarius/HidHide` tag `v1.5.230.0`, commit `722d997ce75db58f5aa36e40ca920f99022c020a`; the official release asset exactly matches `extras/HidHide_1.5.230_x64.exe` (`F4BBBCB82E6258641B887C74BC81C4C5F66E4AA811808DFC304347687B7605F6`) and has the pinned valid Nefarius signer. | MIT text in `extras/HidHide-1.5.230-LICENSE.txt`. |
| RNNoise.NET 0.1.9 | NuGet content hash `2lqIr0648oA0qiUlbcTshFe4dm010Lyv+BaP2GGKfN+CI5DOrR7tV3X0kwgYvf/V3DDHllflWiQXhWT7+LTsEg==`; source tag/commit `0.1.9` / `73189a685823d2db25a6c94edd7b69265309c0db`; published `rnnoise.dll` SHA-256 `12E19BF7A18D13E092A5FBE5A7C5B2081F5E7B56F6D77AEAB5837335F44CEEDF`. | MIT text in `PureDS4/ThirdParty/RNNoise.NET-LICENSE.txt`, copied to output and publish. |
| SharpOSC 0.2.0 | Both tracked binaries embed source commit `6cd3eb265e64d62a92679290a44083ebad1ea579`; x64 SHA-256 `6419A701CE8EF5BAAD072FF14C232A3557525BF8BB4E3FD6DED09B2D3F22F07E`, x86 `2BA7A8C0D6459F16A05725159903A2725955A3AF6D69018CBC1F15DEFF87F1CE`. | Upstream MIT text in `PureDS4/ThirdParty/SharpOSC-LICENSE.txt`, copied to output and publish. |
| SbcSharp codec | Vendored source revision and modifications are recorded in `PureDS4/ThirdParty/SbcSharp/NOTICE.md`; compiled bytes are part of the application assembly. | `PureDS4/ThirdParty/SbcSharp/LICENSE.txt` and `NOTICE.md`, both copied to output and publish. |

The previously bundled hbashton VIIPER 0.1.0 bytes remain in the repository
for existing-candidate reproducibility but are no longer referenced by the
project or included in new packages. Its own update channel was incompatible
with PureDS4-owned replacement. The source-pinned fork removes that channel,
the tray UI, inherited configuration loaders, and unrelated virtual devices.
The fork is installed on the owner's machine. Package replacement and normal
exact-artifact USB/Bluetooth gameplay and reconnection passed with the
`49ba5d2` candidate. The release manifest remains blocked because the
transient VIIPER API-unresponsive recovery path has not been exercised on
hardware; normal reconnection did not trigger that failure. The remaining
clean-install and failure/recovery matrix is also not established by this
one-machine pass.

The exact asset URLs, all individual artifact hashes, and signer certificate
thumbprints are kept in the machine-readable contract rather than duplicated
here.

## Removed FakerInput integration

The inherited FakerInput integration has been removed, and with it the only
unresolved licensing question in this manifest. It consisted of a signed driver
MSI for each architecture and two wrapper binaries: `FakerInputWrapper.dll`,
which embeds source commit `a6ba4055d4c53c7b748f182c4d136e433a2400dc` from
`Ryochan7/FakerInputWrapper`, and `FakerInputDll.dll`, which corresponds to
`Ryochan7/FakerInputDll` but embeds no source revision at all. Both wrappers
were inherited through the GPL-licensed DS4Windows lineage, and neither
companion repository states whether the FakerInput driver's MIT license covers
it. That evidence was never enough to prove redistribution scope for a first
public release.

What the integration bought was narrow: a virtual keyboard and mouse device
that kept a real pointer present for profiles mapping a stick to the mouse, and
input that reaches windows which refuse simulated events. PureDS4 now sends all
mapped keyboard and mouse output through SendInput, which needs no driver, no
install step, and no third-party binary. Removing the integration deleted the
two wrapper binaries, both MSIs, the bundled MIT text, the first-run install
step, and the installer's optional package, so `installer/release-inputs.json`
no longer declares a release blocker.

SendInput can inject only into windows at an equal or lower integrity level;
mapped keyboard and mouse input may not reach an elevated game from an
unelevated PureDS4 process. A failed SendInput call does not identify UIPI as
the cause, and this limitation says nothing about VIIPER virtual-gamepad
readiness. The mouse-wheel path reports incomplete injection rather than
silently treating it as success.

This source and package removal does not uninstall a FakerInput driver already
present on a user's machine. Older bundles treated it as shared permanent
infrastructure; removing that system driver remains a separate owner-authorized
operation because another application may still depend on it.

## Managed package graph

Every .NET/WiX project commits `packages.lock.json`. CI restores in locked mode,
and installer composition sets `RestoreLockedMode=true`. NuGet package versions
and content hashes therefore fail closed if the resolved dependency graph
changes without a reviewed lock update.

The application publish additionally checks its actual win-x64 runtime assets
against `PureDS4/ThirdParty/ManagedDependencies.NOTICE.txt`, including package
versions and NuGet license metadata. It copies the .NET Library license,
the .NET SDK/runtime-pack third-party notices, and System.Management's
third-party notices into every portable or MSI payload. These notices cover
the self-contained single-file executable, whose embedded assemblies are not
visible as separate files in the install directory. A new runtime dependency
or changed package license fails publication until the notice is reviewed.

For the `49ba5d2` candidate, the restore assets select .NET and Windows
Desktop runtime packs `8.0.31`. On 2026-09-28,
`dotnet list PureDS4.sln package --vulnerable --include-transitive` reported
no vulnerable packages from NuGet's current advisory source. Microsoft listed
`8.0.31` as the current .NET 8 security patch on its
[download page](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
These are dated checks, not a guarantee against later advisories; repeat them
for the final publication decision.

Microsoft's Windows-specific .NET license breakdown and Windows SDK terms are
linked from the shipped notice. The Windows SDK projection's redistribution
scope still needs a deliberate release-level legal review; a package build
passing does not establish legal clearance.

## Repository cleanup and external components

The unreferenced legacy `extras/Virtual Bus Driver.zip` and the Visual Studio
code snippet `extras/gplv3.snippet` were removed during this gate. Neither was
used by the project, publish scripts, or installer, and the snippet was not a
redistributable license notice.

`vJoyInterface.dll` is not bundled; inherited vJoy support probes an external
installation. NVIDIA Audio Effects libraries are likewise optional files
loaded only from NVIDIA locations under Windows Program Files. Their binaries
and licenses are not release inputs for PureDS4.

`PureDS4/Resources/ControllerArtwork.NOTICE.txt` remains the controller
artwork attribution and is copied into published output.
