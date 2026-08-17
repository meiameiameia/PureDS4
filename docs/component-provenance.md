# Distributed component provenance

Recorded for the Phase 0 safety gate. SHA-256 values identify the repository
artifacts at this milestone; a matching version or tag alone is not proof that
a binary was built from that source revision.

## Bundled installers and native components

| Component and purpose | Version / candidate source revision | Local artifact and SHA-256 | License / notice | Provenance status |
| --- | --- | --- | --- | --- |
| Custom VIIPER virtual-output backend | 0.1.0; `hbashton/VIIPER` tag `v0.1.0`, commit `fd298a04d7d229293be15b2af664405c9e68114c` | `extras/VIIPER-0.1.0-x64.exe` — `AD14F2C9048D61B3447F2F79D7A122EDEA81E5DB52A1AC803D294E5BC9CD2324` | No component-specific notice is packaged; `extras/gplv3.snippet` exists but is not an adequate binary provenance record. | **Unresolved/release blocker:** binary is unsigned and exact binary-to-source mapping and complete notice are not established. Source: <https://github.com/hbashton/VIIPER>. |
| usbip-win2 installer used by VIIPER | 0.9.7.7; `vadimgrn/usbip-win2` tag `v.0.9.7.7`, commit `7c219953101cc5d0ec9a0bcb3eb87259cf72bedd` | `extras/USBip-0.9.7.7-x64.exe` — `51620FA5F9F8BE5932BC9D786DEEE557CE06D5407A99CAB490DCFAC71F185FEA` | `extras/USBip-0.9.7.7-LICENSE.txt` | Version/source candidate and a valid Cloudyne Systems signature are known; exact release-asset-to-tag reproduction is not yet established. Source: <https://github.com/vadimgrn/usbip-win2>. |
| HidHide installer | 1.5.230; `nefarius/HidHide` tag `v1.5.230.0`, commit `722d997ce75db58f5aa36e40ca920f99022c020a` | `extras/HidHide_1.5.230_x64.exe` — `F4BBBCB82E6258641B887C74BC81C4C5F66E4AA811808DFC304347687B7605F6` | `extras/HidHide-1.5.230-LICENSE.txt` | Version/source candidate and a valid Nefarius signature are known; reproducible binary-to-source proof is not recorded. Source: <https://github.com/nefarius/HidHide>. |
| FakerInput driver installers | 0.1.0; `Ryochan7/FakerInput` tag `v0.1.0`, commit `0bf881f603dc40c4a0a2eef3d8d8da6196c405dc` | x64 `extras/FakerInput_0.1.0_x64.msi` — `30CF218B624740A91BE4FCCA3ADFB4550BA8CC8F31AC9625FE39D238E64D13EA`; x86 `extras/FakerInput_0.1.0_x86.msi` — `0C0A01EEF8C57C9B3DB917131995A10ADB3599CC643D2F27AD28D9511B96DEC1` | `extras/FakerInput-0.1.0-LICENSE.txt` | Version/source candidate and valid Travis Nickles signatures are known; reproducible binary-to-source proof is not recorded. Source: <https://github.com/Ryochan7/FakerInput>. |
| RNNoise native audio suppressor | NuGet package `YellowDogMan.RRNoise.NET` 0.1.9; tag/commit `0.1.9` / `73189a685823d2db25a6c94edd7b69265309c0db` | published `rnnoise.dll` — `12E19BF7A18D13E092A5FBE5A7C5B2081F5E7B56F6D77AEAB5837335F44CEEDF` | Package metadata; no RNNoise-specific notice is copied into the publish. | Package version and native hash are pinned in source, but build provenance and publish notice remain incomplete. Source: <https://github.com/Yellow-Dog-Man/RNNoise.Net>. |
| FakerInput managed/native wrapper | Inherited binaries; managed wrapper reports 1.0.3 | x64 wrapper `4792671766A575394D3402A9365AF9908AF94E812EC1969BFE4975C0AB4F5430`; x64 native `7E3D67A3E6B4EF2ABA039A3B1E079ACDE3AD95E0286A87623949AD74607D1A50`; x86 wrapper `1A97250E793E22A7A961142C61BFED930F6A5080AAD739D32D79BE021E13B1A8`; x86 native `1A73F0D2CA7ECB19F00A390A3FD27BA3BA5E21C3363325EBBCC298EE10D10763` under `DS4Windows/libs/<arch>/FakerInputWrapper/` | No wrapper-specific license is packaged. | **Unresolved/release blocker:** exact source revisions and licenses for these inherited binaries are not recorded. |
| SharpOSC managed binary | Inherited binary, assembly version 0.2.0.0 | x64 `DS4Windows/libs/x64/SharpOSC/SharpOSC.dll` — `6419A701CE8EF5BAAD072FF14C232A3557525BF8BB4E3FD6DED09B2D3F22F07E`; x86 equivalent — `2BA7A8C0D6459F16A05725159903A2725955A3AF6D69018CBC1F15DEFF87F1CE` | No SharpOSC-specific license is packaged. | **Unresolved/release blocker:** source revision and license linkage are not recorded. |
| SbcSharp codec source compiled into the app | Vendored source, revision recorded in `NOTICE.md` | `DS4Windows/ThirdParty/SbcSharp/` | `DS4Windows/ThirdParty/SbcSharp/LICENSE.txt` and `NOTICE.md`, both copied to publish | Source and notice are established; compiled bytes are part of the application assembly. |

Authenticode signer validation is useful identity evidence but is not a
substitute for a reproducible source-to-binary chain.

## Managed NuGet runtime dependencies

The project directly pins these runtime package versions in
`DS4Windows/DS4WinWPF.csproj`: `bloomtom.HttpProgress` 2.3.2, `Concentus`
2.2.2, `DotNetProjects.Extended.Wpf.Toolkit` 5.0.106, `H.NotifyIcon.Wpf`
2.0.74, `MdXaml` 1.27.0, `NAudio` 2.2.1, `NLog` 5.1.1,
`Ookii.Dialogs.Wpf` 5.0.1, `System.Management` 7.0.2, `System.Memory` 4.5.5,
`TaskScheduler` 2.10.1, `WPFLocalizeExtension` 3.9.4, and
`WpfScreenHelper` 2.1.0. `Microsoft.Windows.CsWin32` 0.3.106 is a private
build/analyzer dependency. NuGet package metadata is the current source and
license record.

There is no repository package lock or committed package-artifact hash set, so
restored transitive binaries are version-resolved rather than reproducibly
mapped. A release process must capture the resolved dependency graph, package
hashes/signatures, and notices; Phase 0 does not update packages.

## Repository-only and externally supplied items

- `extras/Virtual Bus Driver.zip` has SHA-256
  `3DD36E17242F80AB3D9C5DE8F402BE3FF768A30AD83ADE4DB05CB1563AB614E7`.
  It appears to contain legacy ScpVBus/DIFx artifacts, has no project,
  installer, or post-build reference, and is not distributed by the current
  application project. Exact provenance is unresolved; retain it until an
  explicit repository-cleanup decision.
- `vJoyInterface.dll` is not bundled. The inherited vJoy integration probes an
  external installation by library name; its source and license belong to
  that external installation.
- NVIDIA Audio Effects libraries are not bundled. They are optional vendor
  files loaded only from NVIDIA locations below Windows Program Files.
- `DS4Windows/Resources/ControllerArtwork.NOTICE.txt` records the controller
  artwork notice and is copied into published output.

## Release blockers

Before a public release, establish the exact source/build chain and complete
license notices for the custom VIIPER binary, FakerInput wrapper binaries, and
SharpOSC binaries. Also capture a locked NuGet dependency graph and resolve or
remove the stale virtual-bus archive through an explicit product decision.
