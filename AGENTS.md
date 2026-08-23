# DS4Windows Reworked

## Working agreement

This is a maintainable DS4Windows derivative for a solo product owner. Lead with outcomes in plain language; explain material tradeoffs, risks, validation limits, and decisions without assuming the owner reads code. Work autonomously on safe, bounded tasks, preserve unrelated work, and distinguish verified facts from hypotheses and product choices.

Controller correctness and reliability come first, followed by latency consistency, maintainability, understandable architecture, testability, setup experience, and reversibility. Architectural elegance alone is not a reason to change working code.

## Milestone execution

- Execute validated roadmap milestones autonomously; do not require ChatGPT Web between ordinary engineering milestones.
- Stop at genuine owner, visual, hardware, product, or architectural gates. Before a materially different next milestone, recommend the suitable Codex model and reasoning level; recommend a switch if investigation materially changes the complexity.
- Do not create AI-specific task, context, plan, result, handoff, roadmap, or metadata files/directories, and do not duplicate transient conversation context in the repository. Repository documentation must be independently useful product or engineering documentation.

## Product direction and validation truth

- **The product is PureDS4: a deliberately narrow DualShock 4 and DualShock 3 controller tool for Windows.** Its reason to exist is that `hbashton/DS4Windows` is a catch-all supporting DualSense, DualSense Edge, Switch Pro and Joy-Con, and a user who only owns a DS4 or DS3 has to carry all of it. PureDS4 covers exactly two controllers and aims to make every feature of those two work as it would on a console. It is a complement to DS4Windows, not a replacement, and it credits that lineage openly.
- Publication is intended but not scheduled, and no date or readiness claim should be assumed. Build the product well first. `hbashton/DS4Windows` does not sign its own binaries either, so code signing is a later quality improvement rather than a precondition for release.
- Scope: DS4 leads because the owner has the hardware. DS3 follows, gated on borrowed hardware from a friend who will also be a user. DualSense, DualSense Edge, Switch Pro and Joy-Con are removed along with the subsystems that exist only to serve them. The DualSense implementations alone are roughly sixteen times the size of the DS3 support being kept, and drag an audio, haptics and adaptive-trigger surface plus native Opus and noise-suppression dependencies.
- Removing DualSense does **not** mean removing controller audio. The DualShock 4 speaker and headset jack are native DS4 features with a dedicated implementation in this tree, and they stay. What goes is Audio Haptics, the DualSense voice-coil feature, and every DualSense-specific speaker, microphone and haptics path.
- This tool has a second user. The friend who owns the DS3 will run it on a machine the owner cannot reach, so the installer, first-run experience and DS3 path must work without the owner being able to hot-fix anything. DS3 requires the DsHidMini driver, which the installer does not yet provision.

### Identity separation

PureDS4 must be safely installable **alongside** DS4Windows. Anything that both products could own is a collision, and a collision will look like data loss to the user. Before publication, every one of these must be on a PureDS4 identity rather than the inherited `DS4Windows` lineage:

- install root and registry root;
- configuration and profile storage paths;
- scheduled task names, currently `RunDS4Windows` and `RunVIIPER`;
- IPC identities and single-instance mutexes;
- installer upgrade codes, which today would make a PureDS4 MSI upgrade over and remove a DS4Windows install;
- executable and package names, and the product version line, which need not follow DS4Windows versioning.

There are no existing users, so no migration is owed. A first-run import from an existing DS4Windows configuration is a courtesy for people switching, not an obligation, and must never write back to it.

### Relationship to upstream

- Upstream contribution is no longer the primary route for this work. `hbashton/DS4Windows` has a standing queue of unaddressed pull requests, so building against that queue is a poor use of effort. Report user-harming defects found here as issues, which costs little and helps users regardless of whether they are acted on, and do not invest in preparing pull requests unless something changes.
- Because contributions are no longer the goal, the previous constraint to keep `Mapping`, `ControlService`, the device protocol layers and the serialization close to upstream in shape is **withdrawn**. Diverge where it makes the product better. Preserve upstream history and GPL notices regardless; that obligation does not depend on the contribution route.
- Keep the `upstream` remote and the `upstream-baseline` tag. They remain useful for understanding inherited behaviour and for picking up upstream bug fixes in shared code.

- DualShock 4 is the reference hardware. Bluetooth and USB are both supported paths and both have now been owner-validated (detection, physical input, Managed/Virtual readiness, and Native Physical mode transitions); see the validation records below for exactly which build each was exercised on.
- DualSense and DualSense Edge are out of scope and are being removed. Do not add, repair or defend that code; do not claim it works.
- DS3 is an intended supported target for the pivoted tool, not merely an inherited family. Genuine hardware is available for manual validation through the owner's friend. Existing ScpToolkit/ScpTools driver state and coexistence must be characterized before changing setup or runtime behavior; do not claim DS3 support until the real hardware path is validated.
- Joy-Con, Switch Pro and other inherited controller families are out of scope and are being removed. Removal is staged and verified rather than done in one pass, because DS4, DS3 and DualSense share a device class hierarchy and the seams are not all obvious.
- The first dependable product journey is a normal Windows user reaching working in-game controller output without learning VIIPER, USB-IP, HidHide internals, hashes, or installation modes. Do not weaken integrity or identity checks to simplify setup.
- Owner-validated, on the promoted canonical `Dev\current` runtime after the controller-exposure recovery milestone: the app launches through `RunDS4Windows`; a DS4 connects over Bluetooth, is detected, and supplies physical input; **Continue without virtual output** works; the application reports Managed/Virtual as Ready; the transition to Native Physical exposure, and back to Managed/Virtual, both complete; Forza Horizon 6 recognizes the Managed/Virtual Xbox 360 output and responds normally to gameplay input; controller disconnect/reconnect works; and app close/relaunch through `RunDS4Windows` works.
- Owner-validated on the single-file build installed by the reworked installer, from a fully clean machine with no prior DS4Windows, VIIPER, usbip-win2 or HidHide present: the installer provisions VIIPER, usbip-win2 and HidHide and completes without error; the install root contains only the intended six files and four content directories; a DS4 connects over **USB**, is detected, supplies physical input, reports Managed/Virtual as Ready, and the transition to Native Physical exposure works. A DS4 over Bluetooth also reaches Ready on this build.
- Not yet owner-validated: **uninstall**. The fix that caches the uninstall-only Burn packages is committed but has not been exercised end to end, so no claim is made that an install produced by this installer can be removed by it. DualSense hardware remains unvalidated, as does the upgrade path from the previous multi-file layout, which may leave orphaned assemblies and language folders behind. Rumble, audio, haptics, and other advanced output behavior have not been claimed unless separately exercised.

Never turn build success, protocol tests, a component health check, or test count into a hardware/runtime claim. Record exactly what was exercised.

## Visual validation and dogfood runtime

- Computer-use or GUI launcher automation is not authoritative for DS4Windows visual validation because it may launch the installed legacy executable from `C:\Program Files\DS4Windows` instead of the requested disposable build.
- UI tasks must report the exact disposable validation executable. Automated visual evidence is valid only when the running process path is explicitly verified to match that executable. Final visual validation is performed manually by the owner.
- `Dev\current` is the latest owner-approved and committed milestone. `Dev\rollback` is the immediately previous owner-approved milestone. Disposable validation publishes never become the daily driver.
- Promotion sequence: build/tests → disposable publish → owner validation → commit/push → promote.
- `RunDS4Windows` is persistent host-level infrastructure and remains pointed at `Dev\current`. Normal build, validation, promotion, rollback, cleanup, and release work must never recreate or retarget it, including to a disposable or versioned publish. Promote a validated build by replacing `Dev\current`, then manually smoke-test that canonical runtime.
- `RunVIIPER` is likewise host infrastructure and must not be modified by normal development or promotion work.

## Repository and stack

- Baseline: annotated tag `upstream-baseline`, commit `3579450dc8f50a74d9532e711249589c732c460b`. That commit is also `upstream/main` and upstream tag `VIIPERRC4.3`, so this fork currently sits exactly on top of the maintained project's main branch rather than behind it. Re-check that before assuming it still holds; upstream's newer work (for example the native UdeCx backend) lives on branches and draft pull requests.
- `main` tracks derivative remote `origin/main`; `upstream` is `https://github.com/hbashton/DS4Windows.git`. Preserve upstream history; do not rebase it away.
- `DS4WindowsWPF.sln` contains the .NET 8 Windows WPF application (`DS4Windows/DS4WinWPF.csproj`) and MSTest project (`DS4WindowsTests/DS4WindowsTests.csproj`). Installer projects under `installer/` are outside the solution.
- `DS4Windows/App.xaml.cs` is application startup/composition. `DS4Windows/DS4Control/ControlService.cs` owns broad runtime orchestration. `DS4Windows/DS4Control/Mapping.cs` is the input-mapping hot path. `DS4Windows/DS4Library/` and `HidLibrary/` implement controller/HID protocols. `DS4Windows/DS4Control/Viiper/` implements the virtual-output backend. `DS4Windows/DS4Control/DTOXml/` and `BackingStore` in `ScpUtil.cs` own persisted settings/profile formats. `DS4Windows/DS4Forms/` contains WPF views and view models.
- Generated resource/settings designer files should be regenerated through their source `.resx`/`.settings` workflow, not hand-edited without a specific reason.

## Build and checks

Use native Windows/PowerShell tooling and repository-local configuration. No `global.json` is currently present.

```powershell
dotnet restore DS4WindowsWPF.sln
dotnet build DS4WindowsWPF.sln -c Debug -p:Platform=x64
dotnet test DS4WindowsWPF.sln -c Debug -p:Platform=x64
dotnet build DS4WindowsWPF.sln -c Release -p:Platform=x64
```

Fresh baseline on `pt-BR`: Debug x64 builds with 0 errors and 15 warnings; 760 tests discover, with 755 passed, 2 failed, and 3 skipped. `CheckSettingsRead` and `CheckSettingsSave` fail because `AppSettingsDTO.LastCheckString` writes `MM/dd/yyyy HH:mm:ss` but parses with the current culture. Fix serialization/parsing, not assertions; validate explicitly under `en-US`, `pt-BR`, and another non-US culture. The three skipped `LiveProcessCapture*` tests become inconclusive when a suitable live Windows audio session is unavailable.

For a change, run the narrowest relevant test first, then the full x64 suite and build. Report existing and introduced failures separately. Run Release/publish/installer checks when the changed surface reaches packaging. Do not treat the CI test filter as permission to ignore a local failure.

## Architecture and change discipline

- Keep .NET 8 and WPF unless measured repository evidence shows a material product problem they cannot solve.
- Start with logical seams inside the existing production project. Add projects only for a demonstrated dependency-enforcement, compilation, ownership, or testing benefit.
- Make dependencies and ownership explicit before considering a DI container. Do not add DI, MVVM, or other framework packages by default.
- Prefer existing/native solutions. Ask before adding any package; consider maintenance, security, licensing, binary size, and lock-in.
- Do not rewrite or broadly decompose `Global`, `ControlService`, `Mapping`, device protocols, or VIIPER in one step. Characterize the exact behavior and threading first, then make one small reversible change with targeted tests.
- Do not call something a performance problem or set a latency budget without measurement. Before changing report, mapping, or output timing, establish comparable report-rate, processing, output, allocation, GC, and p50/p95/p99 evidence where practical.
- Preserve profile/settings backward compatibility. Serialization changes require round-trip and legacy-fixture tests.
- Review/planning tasks may inspect and document but must not change production behavior. Implementation tasks include the requested code, relevant tests, diff review, and honest remaining-risk report.

## High-risk boundaries

Use stronger characterization and validation for:

- physical HID, Bluetooth/USB protocol, calibration, report parsing/writing, and hotplug;
- DS3 and legacy ScpToolkit/ScpTools driver coexistence, ownership, and migration;
- `ControlService` lifecycle, synchronization, profile switching, and disconnect/reconnect;
- `Mapping` and `DS4StateFieldMapping` input translation;
- VIIPER/USB-IP output, feedback, audio, and output-slot ownership;
- HidHide, FakerInput, scheduled tasks, elevation, installers, and system/driver state;
- profile/settings serialization and migration;
- controller audio, microphone, haptics, rumble, and adaptive triggers.

When virtual output could be affected, automated checks are insufficient. The manual gate is: launch the local build; connect DS4 by the target transport; verify detection and physical readings; create the intended VIIPER output; verify the virtual controller; play in the target game; disconnect/reconnect; restart/reconnect. Claim only the completed steps.

## Safety and Git

- Preserve pre-existing user work and inspect `git status` before edits. Do not discard ambiguous files.
- Do not commit, push, publish, release, deploy, or alter production/shared data without explicit authorization.
- Do not install, repair, remove, replace, start/stop, or bypass VIIPER, USB-IP, HidHide, FakerInput, scheduled tasks, services, drivers, or the owner's other DS4Windows installation unless the owner explicitly authorizes that exact operation. Read-only inspection is allowed when relevant.
- Do not remove inherited capabilities during baseline/characterization work.
- Keep changes small, coherent, reviewable, and reversible. Finish implementation work by reviewing the diff and reporting commands and manual checks as passed, failed, skipped, or not run.
