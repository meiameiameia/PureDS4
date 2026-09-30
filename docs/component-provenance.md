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
`49ba5d2` candidate. Its source, binary hash and packaged notices are verified.
On 2026-09-29 the owner approved deterministic backend-failure acceptance,
documented with its passing tests and limitations in
[hardware validation](HARDWARE_VALIDATION.md#backend-failure-acceptance-criterion).
Physical reproduction of the intermittent failure remains unobserved, not a
claimed pass or a component-input blocker. The manifest therefore calculates
`releaseReady: true` for its five component inputs. This does not establish
overall product readiness, legal clearance for the separate Windows SDK
projection, the remaining clean-install/failure/recovery matrix, final artifact
acceptance, or permission to publish.

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

### Windows SDK distribution review — 2026-09-29

The actual Release win-x64 dependency graph contains
`runtimepack.Microsoft.Windows.SDK.NET.Ref/10.0.19041.56`, supplying
`Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll`. It also contains WPF's
`D3DCompiler_47_cor3.dll`; these are embedded in the self-contained executable,
not merely build-time references. `GameBarIntegration.cs` uses Windows WinRT APIs.

Microsoft's [redistribution list](https://learn.microsoft.com/en-us/legal/windows-sdk/redist#microsoftwindowssdknetref)
explicitly includes both targeting-pack assemblies for unmodified distribution
with applications calling WinRT APIs. Its classic-Windows list also includes
the D3D compiler. The [.NET Windows license breakdown](https://github.com/dotnet/core/blob/main/license-information-windows.md)
identifies the WPF compiler's SDK terms. This resolves the file-list uncertainty;
it does not mean all Microsoft components are MIT-licensed.

The [package's license link](https://aka.ms/WinSDKLicenseURL) resolves to
Microsoft's SDK agreement, which conditions distribution on preserving notices,
keeping the code unmodified, adding application functionality and obtaining
protective terms from distributors and end users. The shipped .NET Library
agreement also requires protective terms. Keeping URLs/notices alone does not
prove that requirement is met. The bootstrapper now presents both complete
agreements offline, separately from the GPL. Install/update/repair require an
unchecked-by-default Microsoft-only checkbox or the exact unattended terms
argument. Its planning gate also protects internal recovery and reboot resume;
uninstall/layout are exempt. The portable package includes a read-before-use
notice and the full agreements, relying on their acceptance-by-use language,
not a claimed recorded click. No new startup gate or GPL restriction is added.

PureDS4 remains GPL-3.0-or-later. Microsoft's binaries retain their separate
terms, not a PureDS4 relicensing grant. The [GPLv3 system-library explanation](https://www.gnu.org/licenses/quick-guide-gplv3.html)
is relevant to bundled runtime libraries; applying it to this exact combination
is a licensing assessment, not something a test or owner approval proves.
No copyright-holder exception, license change or unconditional legal clearance
is inferred here.

The unmodified SDK agreement was downloaded from Microsoft's official
`https://download.microsoft.com/download/0/F/F/0FF2B061-47DD-4F55-89B6-FD1D8C44F14D/sdk_license.rtf`
on 2026-09-29 into `PureDS4/ThirdParty/WindowsSdk-LICENSE.rtf` (SHA-256
`DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62`).
The .NET Library text was copied unmodified from the pinned build installation's
`C:\Program Files\dotnet\LICENSE.txt` into `PureDS4/ThirdParty/DotNet-LICENSE.txt`
(SHA-256 `7F6839A61CE892B79C6549E2DC5A81FDBD240A0B260F8881216B45B7FDA8B45D`).
Setup's `microsoft-20260929` identifier refers to those exact texts, not the
PureDS4 release version. Both setup resources and packaged documents are
hash-checked. A terms update requires reviewing the new texts and changing
the consent identifier. These are presentation/integrity safeguards, not a
legal opinion about GPL compatibility or proof of an end user's understanding.

`WindowsSdk-LICENSE.txt` is the complete plain-text extraction of that RTF
using WPF `TextRange` (SHA-256
`5D27A78A64D3CB74DDB7556B625E119B05D12ABEFF5909A9EE8B171A0BE7DC75`).
It lets portable users read the SDK terms offline in Notepad without Office;
the original RTF remains authoritative for formatting and is also shipped.
Git must preserve all pinned vendor documents without line-ending conversion.

`utils/validate-managed-notices.py` now checks the targeting-pack version,
runtime assembly inventory, package license URL and shipped notice against the
actual dependency graph. Its fixture tests reject missing/replaced packs,
unexpected assemblies and changed/missing terms references. Those checks and
the real Release graph passed locally. The notice now uses the working
Microsoft license/redistribution links. No Microsoft runtime bytes were changed.

## Distribution footprint — 2026-09-29

Logical file sizes, in decimal MB (1 MB = 1,000,000 bytes); not allocated disk
space or memory use. ZIP entry lengths measure the exact unpacked package
without launching any software. The local legacy directory was enumerated
read-only, without reading personal profiles or logs.

| Distribution | ZIP download | Unpacked portable files |
| --- | ---: | ---: |
| Locally composed PureDS4 5.1.0 candidate | 119.210 MB | 247.359 MB |
| [hbashton VIIPERRC4.6.6 / application 5.0.12.0](https://github.com/hbashton/DS4Windows/releases/tag/VIIPERRC4.6.6) | 134.621 MB | 279.389 MB |
| Owner's schmaldeo 3.9.9 portable directory | Not measured; existing directory | 11.750 MB total; 11.718 MB excluding personal settings/profiles/logs |

The PureDS4 ZIP was composed from the dirty candidate based on
`f9d3b5e9aed55ff4cbfe34c09d87e6d5a882247e`, not a committed or published
release. It is `artifacts/disposable/terms-final-20260929/x64/Release/PureDS4_5.1.0_x64.zip`,
SHA-256 `6781F7EA899AB87748F1AE2046D65A52CBB9D1765DA5393C6B778DC3F766D398`.
It contains 29 files including the runtime, pinned backend/driver installers,
and licenses. Its unpacked size is 11.46% smaller than the hbashton ZIP.
The matching local standard setup is 191,372,404 bytes (191.372 MB), SHA-256
`50B038623602EAF73D3AC323D2A0E079C16E6E7A299EC375F15E7C94DF2F5CF6`,
at `artifacts/disposable/terms-final-20260929/installer/PureDS4_5.1.0_Setup_x64.exe`.
The hbashton release lists its setup at 201,242,471 bytes (201.242 MB); that
setup was not downloaded or executed for this comparison.

The hbashton ZIP was downloaded from that release and matched its published
checksum `B97DAA4C605C778AFAA64FCC8283E2218597A1B8BCF6B55CD2443302346BA4AF`.
Its 555 files total 279,389,324 bytes. It includes VIIPER twice (root and
`extras`, 9,445,376 bytes each). PureDS4 includes one pinned VIIPER executable
(5,919,232 bytes, 37.33% smaller than one upstream copy); this does not establish
performance, latency or memory improvements.

The legacy directory `E:\70_Programs\DS4Windows` contains 71 files totaling
11,750,260 bytes, of which 32,540 bytes are personal settings/profiles/logs.
`DS4Windows.dll` identifies itself as
`3.9.9+722c561c3d7aa65dcf063e716c04ca69e4ca33bd`. Its runtime configuration
requires external Microsoft.NETCore.App and Microsoft.WindowsDesktop.App 8.0;
its ViGEm driver is also external. Those installed dependencies are not part
of the 11.750 MB directory. PureDS4 and hbashton's package include the runtime
and offline driver installers, so their much larger totals are not a like-for-like
application-only regression. Future release changes require a fresh measurement.

The owner accepted this footprint for 5.1.0 on 2026-09-29: retain the offline,
self-contained distribution without additional size optimization for this
release. This size acceptance does not waive installation/security validation
or authorize a public binary release.

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
