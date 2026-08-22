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

## Verified release inputs

| Component | Source and binary evidence | License / notice |
| --- | --- | --- |
| Custom VIIPER backend 0.1.0 | `hbashton/VIIPER` tag `v0.1.0`, commit `fd298a04d7d229293be15b2af664405c9e68114c`; the official ZIP is SHA-256 `07BEB53FDB6856AFA6E0F31EDF210A8618CBC2304AB59AC23FB017B11082ADD7`, and its `viiper.exe` exactly matches `extras/VIIPER-0.1.0-x64.exe` (`AD14F2C9048D61B3447F2F79D7A122EDEA81E5DB52A1AC803D294E5BC9CD2324`). The executable is upstream-unsigned, so the archive and inner-file hashes are the identity proof. | The official release's complete `licenses.txt` is packaged as `extras/VIIPER-0.1.0-LICENSES.txt`. |
| usbip-win2 0.9.7.7 | `vadimgrn/usbip-win2` tag `v.0.9.7.7`, commit `7c219953101cc5d0ec9a0bcb3eb87259cf72bedd`; the official release asset exactly matches `extras/USBip-0.9.7.7-x64.exe` (`51620FA5F9F8BE5932BC9D786DEEE557CE06D5407A99CAB490DCFAC71F185FEA`) and has the pinned valid Cloudyne Systems signer. | BSD-2-Clause text in `extras/USBip-0.9.7.7-LICENSE.txt`. |
| HidHide 1.5.230 | `nefarius/HidHide` tag `v1.5.230.0`, commit `722d997ce75db58f5aa36e40ca920f99022c020a`; the official release asset exactly matches `extras/HidHide_1.5.230_x64.exe` (`F4BBBCB82E6258641B887C74BC81C4C5F66E4AA811808DFC304347687B7605F6`) and has the pinned valid Nefarius signer. | MIT text in `extras/HidHide-1.5.230-LICENSE.txt`. |
| FakerInput driver 0.1.0 | `Ryochan7/FakerInput` tag `v0.1.0`, commit `0bf881f603dc40c4a0a2eef3d8d8da6196c405dc`; both official MSI assets exactly match the bundled x64 and x86 files and have the pinned valid Travis Nickles signer. | MIT text in `extras/FakerInput-0.1.0-LICENSE.txt`. |
| RNNoise.NET 0.1.9 | NuGet content hash `2lqIr0648oA0qiUlbcTshFe4dm010Lyv+BaP2GGKfN+CI5DOrR7tV3X0kwgYvf/V3DDHllflWiQXhWT7+LTsEg==`; source tag/commit `0.1.9` / `73189a685823d2db25a6c94edd7b69265309c0db`; published `rnnoise.dll` SHA-256 `12E19BF7A18D13E092A5FBE5A7C5B2081F5E7B56F6D77AEAB5837335F44CEEDF`. | MIT text in `DS4Windows/ThirdParty/RNNoise.NET-LICENSE.txt`, copied to output and publish. |
| SharpOSC 0.2.0 | Both tracked binaries embed source commit `6cd3eb265e64d62a92679290a44083ebad1ea579`; x64 SHA-256 `6419A701CE8EF5BAAD072FF14C232A3557525BF8BB4E3FD6DED09B2D3F22F07E`, x86 `2BA7A8C0D6459F16A05725159903A2725955A3AF6D69018CBC1F15DEFF87F1CE`. | Upstream MIT text in `DS4Windows/ThirdParty/SharpOSC-LICENSE.txt`, copied to output and publish. |
| SbcSharp codec | Vendored source revision and modifications are recorded in `DS4Windows/ThirdParty/SbcSharp/NOTICE.md`; compiled bytes are part of the application assembly. | `DS4Windows/ThirdParty/SbcSharp/LICENSE.txt` and `NOTICE.md`, both copied to output and publish. |

The exact asset URLs, all individual artifact hashes, and signer certificate
thumbprints are kept in the machine-readable contract rather than duplicated
here.

## Remaining public-release licensing decision

The inherited FakerInput integration uses two additional binary repositories:

- `FakerInputWrapper.dll` embeds exact source commit
  `a6ba4055d4c53c7b748f182c4d136e433a2400dc` from
  `Ryochan7/FakerInputWrapper`.
- `FakerInputDll.dll` corresponds to `Ryochan7/FakerInputDll`, but the binary
  embeds no source revision.

The three repositories are companion projects from the same author and period,
and the wrappers were inherited through the GPL-licensed DS4Windows lineage.
That is meaningful redistribution evidence. The wrapper repositories, however,
do not explicitly say whether the FakerInput driver's MIT license covers them.
This gate does **not** conclude that they are outside the MIT license or that
redistribution is prohibited; it records that the license scope is not proven
well enough for a first public release.

The four x64/x86 binary hashes are pinned in the manifest, so byte substitution
is detected. Public release composition remains intentionally blocked until one
of these evidence-based resolutions is recorded:

1. the author confirms that the existing FakerInput MIT license covers both
   wrapper repositories, preferably by adding their `LICENSE` files;
2. the author confirms a GPL grant for the wrappers and supplies or identifies
   the exact corresponding native source revision; or
3. a later owner-approved engineering decision replaces or removes the
   integration after real FakerInput validation.

The smallest capability-preserving route is written confirmation or explicit
license files from the author. No runtime behavior was changed during this
provenance gate.

## Managed package graph

Every .NET/WiX project commits `packages.lock.json`. CI restores in locked mode,
and installer composition sets `RestoreLockedMode=true`. NuGet package versions
and content hashes therefore fail closed if the resolved dependency graph
changes without a reviewed lock update.

## Repository cleanup and external components

The unreferenced legacy `extras/Virtual Bus Driver.zip` and the Visual Studio
code snippet `extras/gplv3.snippet` were removed during this gate. Neither was
used by the project, publish scripts, or installer, and the snippet was not a
redistributable license notice.

`vJoyInterface.dll` is not bundled; inherited vJoy support probes an external
installation. NVIDIA Audio Effects libraries are likewise optional files
loaded only from NVIDIA locations under Windows Program Files. Their binaries
and licenses are not release inputs for DS4Windows Reworked.

`DS4Windows/Resources/ControllerArtwork.NOTICE.txt` remains the controller
artwork attribution and is copied into published output.
