# Phase 0 privilege and native-code boundary

This inventory describes the current boundary after the Phase 0 safety work. It
does not grant the application ownership of host tasks, drivers, or another
program's configuration.

## Privilege inventory

| Operation | Class | Current boundary |
| --- | --- | --- |
| Controller discovery, input, mapping, profiles, logs, and ordinary UI | Normal runtime | Runs with the process token; the application manifest is `asInvoker` and these operations do not require elevation. |
| Read-only inspection or starting of the existing `RunDS4Windows` / `RunVIIPER` tasks | Normal runtime | Inspection is allowed. Ordinary startup contains no task registration, deletion, repair, or retargeting path. The tasks remain host-owned infrastructure. |
| HidHide allowlist, blacklist, and active-state configuration | Normal runtime | HidHide's configuration device is designed for least-privilege clients. Reworked now journals only deltas it creates, never adopts matching pre-existing state, and preserves uncertain state for manual recovery. Driver installation is separate. |
| VIIPER output and usbip attach/detach | Normal runtime | Uses the installed backend and canonical Program Files usbip location. Whether every usbip operation works from an unelevated process still needs a clean-host experiment. |
| FakerInput output | Normal runtime | Connecting to an installed driver does not request elevation. Its installer is a setup operation. |
| HidHide, FakerInput, usbip, and VIIPER installation or repair | Setup/install or repair | Explicit setup UI verifies bundled artifacts and launches the installer host with `runas`. Program Files, driver-store, service, and setup-task changes remain confined to that explicit flow. |
| Device disable/re-enable after an exclusive-access failure | Exceptional recovery | The same executable is relaunched with the narrow `re-enabledevice` argument and `runas` to use SetupAPI. |
| `pnputil /restart-device` Steam-device reclaim | Exceptional recovery | Currently depends on the whole process already being elevated. This coupling is documented for a later ownership/privilege phase; Phase 0 does not create a helper or ownership mode. |
| Terminating a foreign VIIPER process that blocks repair | Exceptional recovery | Explicit setup recovery invokes a narrow elevated helper. It is not an ordinary startup action. |
| Protected Program Files mutation | Setup/install or repair | Performed only by explicit elevated setup/repair or the installer engine. Normal runtime must not write there. |
| Inherited updater download, replacement, or elevated copy | No longer required | Product update checks and updater launch are disabled until Reworked has an independent signed channel. The elevated updater-copy path was removed. |
| Runtime creation, repair, deletion, or retargeting of scheduled tasks | No longer required | Removed. Installation tooling may still create tasks as part of an explicitly invoked install/repair, but the application runtime cannot maintain host tasks. |

The dogfood `RunDS4Windows` task may still launch the whole application with a
high-privilege token. That host configuration is outside this source change.
Moving normal runtime to an unelevated token requires the usbip and exceptional
recovery experiments above and is intentionally deferred.

## Native-code trust inventory

| Dependency | Resolution boundary | Phase 0 disposition |
| --- | --- | --- |
| Windows APIs (`kernel32`, `user32`, `hid`, `setupapi`, `cfgmgr32`, `shell32`, `winmm`, `avrt`, `powrprof`, `Mmdevapi`, `ntdll`, `msi`, and `bthprops.cpl`) | Windows system DLL search | OS-owned framework dependencies; unchanged. |
| `rnnoise.dll` | Application directory | A resolver now constructs an absolute path below `AppContext.BaseDirectory` and verifies the pinned SHA-256 before loading. Missing or mismatched files disable the optional suppressor. |
| `FakerInputDll.dll` | Application directory | The wrapper assembly now receives a resolver that uses an absolute application path and a platform-specific pinned SHA-256. Failure falls back to the existing SendInput path. |
| NVIDIA Audio Effects | NVIDIA subdirectories under Windows Program Files locations | User environment variables and application-directory probing were removed. The optional feature fails closed when the protected vendor installation is absent. Signer verification is a possible later defense-in-depth improvement. |
| `usbip.exe` | Canonical USBip installation below Windows Program Files | PATH and environment-derived fallback discovery were removed. Existing setup health checks continue to verify expected installed artifacts. |
| VIIPER executable | Explicit verified installed or bundled path | Existing hash/sidecar verification is retained. No name-only or PATH launch was introduced. |
| `vJoyInterface.dll` | Legacy name-only P/Invoke | The repository does not distribute this DLL; the dormant inherited vJoy integration depends on an external installation. If vJoy is retained for release, an explicit trusted resolver is still required. |

No dynamic managed-assembly loading was found. Current-working-directory and
PATH probing must not be added for application-owned native code.

## Validation limits

Unit tests cover task-mutation API absence, HidHide baseline/delta and crash
semantics, updater disablement, trusted path construction and hashes, and
canonical usbip discovery. They do not install drivers, modify tasks, mutate
the dogfood deployment, or exercise physical hardware. A later owner check
must confirm normal controller input, intended virtual output, reconnect, and
restart behavior from the exact candidate executable.
