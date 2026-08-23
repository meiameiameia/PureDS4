# DS4Windows Reworked

## Working agreement

This is a maintainable DS4Windows derivative for a solo product owner. Lead with outcomes in plain language; explain material tradeoffs, risks, validation limits, and decisions without assuming the owner reads code. Work autonomously on safe, bounded tasks, preserve unrelated work, and distinguish verified facts from hypotheses and product choices.

Controller correctness and reliability come first, followed by latency consistency, maintainability, understandable architecture, testability, setup experience, and reversibility. Architectural elegance alone is not a reason to change working code.

## Milestone execution

- Execute validated roadmap milestones autonomously; do not require ChatGPT Web between ordinary engineering milestones.
- Stop at genuine owner, visual, hardware, product, or architectural gates. Before a materially different next milestone, recommend the suitable Codex model and reasoning level; recommend a switch if investigation materially changes the complexity.
- Do not create AI-specific task, context, plan, result, handoff, roadmap, or metadata files/directories, and do not duplicate transient conversation context in the repository. Repository documentation must be independently useful product or engineering documentation.

## Product direction and validation truth

- **This repository is not heading for a public release.** `hbashton/DS4Windows` is the maintained public DS4Windows and keeps that identity. This derivative exists so the owner can run the controller setup he wants, and as a place to develop work that is offered upstream by pull request. Recognition for that work is sought through upstream contribution, not through a competing distribution.
- The work proceeds in two phases, in order:
  1. **Stabilize the current application, unstripped.** Nothing is removed yet. The value of the tree in this phase is that `upstream/main` is the exact commit this fork started from, so `git diff upstream/main..HEAD` is a clean statement of everything that changed and is the surface used to decide what is worth offering upstream. Stripping before that comparison is finished destroys it.
  2. **Then pivot to a private, DualShock 4 only personal tool.** Remove what the owner does not use, and stop carrying device families that cannot be validated here.
- Contribution portability governs where divergence is acceptable. Diverge freely in the shell, views, tools, dialogs, installer presentation, and branding: none of that is intended to travel upstream. Keep `Mapping`, `ControlService`, `DS4Library`, `HidLibrary`, and the profile/settings serialization close to upstream in shape, because that is where portable fixes live and gratuitous divergence there makes every future patch a manual re-implementation. Prefer developing an intended contribution on a branch tracking `upstream/main` and merging it down, rather than extracting it later from a diverged tree.
- Public product identity, when the product names itself: **DS4Windows Reworked**, from `https://github.com/meiameiameia/ds4windows-reworked`. Preserve upstream attribution and GPL notices. The executable, configuration/storage paths, serialized formats, task names, IPC identities, install root, registry root, and existing installer upgrade codes remain on the established `DS4Windows` lineage until an explicit migration contract says otherwise.
- Version identifiers in the tree (display/Burn `5.1.0-beta.1`, MSI `5.1.0`, assembly/file `5.1.0.0`) record identity only. There is no planned tag, release, or publication, and no release readiness work should be started on the assumption that there is.

- DualShock 4 is the reference hardware. Bluetooth and USB are both supported paths and both have now been owner-validated (detection, physical input, Managed/Virtual readiness, and Native Physical mode transitions); see the validation records below for exactly which build each was exercised on.
- Preserve DualSense and DualSense Edge compatibility, but do not claim hardware validation: the owner has no DualSense hardware.
- DS3 is a future compatibility candidate, with potential access to a genuine controller through the owner's friend for later manual validation. Existing ScpToolkit/ScpTools driver state and coexistence must be characterized before changing setup or runtime behavior; do not claim DS3 support until the real hardware path is validated.
- Joy-Con, Switch Pro, third-party controllers, and other inherited families are not initial priorities. Preserve them until their value, coupling, maintenance cost, and validation feasibility support an explicit product decision.
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
