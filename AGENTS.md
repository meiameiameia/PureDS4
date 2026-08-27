# PureDS4

## Working agreement

PureDS4 is a narrowed DS4Windows derivative for a solo product owner who does not review code. Lead with outcomes in plain language. Explain material tradeoffs, risks, validation limits, and owner decisions; distinguish verified facts from hypotheses and product choices. Work autonomously on safe, bounded tasks, preserve unrelated work, and trust current behavior, code, configuration, tests, command output, and authoritative sources over memory.

Controller correctness and reliability come first, followed by latency consistency, maintainability, understandable architecture, testability, setup experience, professional UI/UX, and reversibility. Architectural elegance alone is not a reason to change working code.

## Task mode, risk, and authority

- Explain, research, review, diagnose, and plan without changing product behavior. A read-only audit changes nothing, ranks findings by severity with evidence and location, separates confirmed defects from missing evidence and owner decisions, recommends action, and states what was not examined.
- Build, fix, or change requests include the implementation, relevant tests, full diff review, and an honest remaining-risk report.
- Treat narrow, local, reversible work as low risk. Treat behavior, architecture, durable interfaces, compatibility, packaging, dependencies, or operations as material. Treat drivers, elevation, destructive replacement, security boundaries, and consequential real-system changes as high risk until evidence supports lowering the class.
- For material work, state the intended outcome, acceptance examples, affected systems, important assumptions, and out of scope before implementation. For high-risk work, require explicit authority, recovery evidence, and an independent review method suited to the likely failure before activation.
- Ask only when missing information changes a material outcome or requires new authority. Prefer the smallest complete reversible solution and keep optional ideas outside committed scope.
- Do not commit, push, publish, release, deploy, expose a system, or alter production/shared data without explicit authorization for that action.

Use only the highest status proven by evidence: **Implemented**, **Machine-verified**, **Owner-accepted**, **Ready**, **Active**, or **Observed**. Build success is not owner acceptance; activation is not observation.

## Milestone execution

- Execute validated roadmap milestones autonomously. Stop at genuine owner, visual, hardware, product, architectural, or consequential-action gates.
- Do not interrupt routine work with model advice. Recommend stronger reasoning only before a materially different architecture, difficult debugging, migration, security, or high-risk review gate.
- Do not create AI-specific task, context, plan, result, handoff, roadmap, or metadata files/directories, and do not duplicate transient conversation context in the repository. Repository documentation must be independently useful product or engineering documentation.

## Product contract

- **PureDS4 is a deliberately narrow DualShock 4 tool for Windows.** The first public release supports the DualShock 4 only. It is the focused alternative for a user who needs nothing else. A user who needs DualSense, DualSense Edge, Switch Pro, Joy-Con, or any other controller family should use `hbashton/DS4Windows` instead.
- PureDS4 is intended to replace DS4Windows or the earlier DS4Windows Reworked installation on a machine, not run beside either one. It credits and preserves its DS4Windows lineage and GPL notices openly.
- DS4 leads because the owner has the hardware. Bluetooth and USB are supported paths and are separate claims.
- **DualShock 3 is a planned future milestone with no schedule, and is out of scope for the first release.** It must never block that release, because validation depends on genuine hardware owned by the owner's friend. The DS3 implementation, its device option, and its tests are retained dormant: postponed, not deleted. Ordinary UI must not offer DS3 or read as though it works, and `ProductScope.DualShock3Offered` keeps a stored or imported setting from activating it. Flipping that constant is not by itself enough to ship DS3; see the DS3 milestone gate below.
- DualSense, DualSense Edge, Switch Pro, and Joy-Con are out of product scope. Their physical support, exclusive UI, assets, dependencies, and subsystems should not ship unless a specific retained compatibility need is documented and tested. Removal is still in progress; do not describe it as complete merely because normal detection or output selection no longer exposes a family.
- Removing DualSense does **not** remove DualShock 4 audio. The DS4 speaker and headset jack are native DS4 features and stay. Audio Haptics, DualSense voice-coil behavior, adaptive triggers, and DualSense-specific speaker, microphone, and haptics paths go.
- The normal user journey is install → first run → connect a DS4 → obtain truthful physical/virtual readiness → play, without learning VIIPER, USB-IP, HidHide, hashes, or installation modes. Normal UI should expose product concepts; implementation topology belongs in technical details and diagnostics.
- Publication is intended but not scheduled. Do not infer a date or readiness claim. Code signing is an explicit release-policy decision, not something to assume from upstream practice.

## Replacement and identity ownership

Unique PureDS4 identities are required even though coexistence is not a supported outcome. They prevent accidental Windows Installer upgrades over another product, make ownership auditable, and allow a failed transition to be recovered safely.

Before publication, PureDS4 must own its executable/package names, install and registry roots, configuration/profile paths, scheduled tasks, IPC and single-instance objects, installer upgrade codes, logs, version line, URLs, and public product strings.

The replacement flow must:

1. detect DS4Windows and DS4Windows Reworked installations, processes, tasks, services, and relevant configuration;
2. require the old application to close and use an explicit owner-visible uninstall or replacement step rather than silently mutating it;
3. preserve or archive user configuration unless deletion was specifically authorized;
4. optionally import old profiles read-only, never write back to the old product's storage;
5. activate PureDS4 only after old runtime ownership can no longer compete; and
6. provide tested uninstall, rollback, reboot, and recovery behavior.

Do not adopt, rename, delete, or retarget another product's shortcuts, tasks, configuration, or backend as a side effect of an ordinary PureDS4 preference. Shared drivers may be preserved intentionally, but ownership and removal rules must be explicit.

## Relationship to upstream

- Upstream contribution is not the primary route. Report user-harming inherited defects as upstream issues when useful, but do not prepare pull requests unless the owner asks or upstream responsiveness materially changes.
- Diverge where it makes PureDS4 better. Preserve upstream history, `upstream` remote, annotated `upstream-baseline` tag, copyright, and GPL notices. Re-check current upstream state before relying on remembered branch or tag positions.
- Broadly renaming internal C# namespaces or legacy serialization roots is not a release goal unless they create an owned-resource collision or a measured engineering problem. Host identities and user-visible branding are different: they must be PureDS4.

## Validation truth

- Never turn build success, protocol tests, a component health check, CI status, or test count into a hardware/runtime claim. Record the exact commit/artifact, executable path, transport, controller, and steps exercised.
- Validation from an older build does not transfer to the current HEAD or release artifact. DS4 and DS3 require separate claims; Bluetooth and USB require separate claims.
- When virtual output could be affected, automated checks are insufficient. The manual gate is: launch the exact local build; verify its running process path; connect the target controller and transport; verify detection and physical readings; create the intended VIIPER output; verify the virtual controller; play in the target game; disconnect/reconnect; and restart/reconnect. Claim only completed steps.
- Inspect test intent. A test that asserts inherited or wrong product behavior is evidence of the defect, not validation.
- For observable changes, provide numbered owner-verification steps and expected results. For high-risk work, the builder's reread is not independent review.

## Visual validation and dogfood runtime

- Computer-use or GUI launcher automation is not authoritative for visual validation because it can launch a different installed executable. UI work must report the exact disposable validation executable, and automated visual evidence is valid only when the running process path matches it. Final visual acceptance is manual by the owner.
- `Dev\current` is the latest owner-approved and committed milestone. `Dev\rollback` is the immediately previous owner-approved milestone. Disposable validation publishes never become the daily driver.
- Promotion sequence: build/tests → disposable publish → owner validation → commit/push → promote to canonical `Dev\current` → smoke-test `Dev\current`.
- Scheduled tasks are host-level infrastructure. Normal builds, validation, promotion, rollback, cleanup, and release work must not recreate or retarget them. Change a task only in an explicitly authorized installation, replacement, or host-repair operation, and verify its complete action afterward. Never point a persistent task at a disposable or versioned build.

## Repository and stack

- Baseline lineage is preserved by annotated tag `upstream-baseline`, commit `3579450dc8f50a74d9532e711249589c732c460b`. `main` tracks derivative `origin/main`; `upstream` is `https://github.com/hbashton/DS4Windows.git`. Do not rebase away inherited history.
- `PureDS4.sln` contains the .NET 8 Windows WPF application (`PureDS4/PureDS4.csproj`) and MSTest project (`PureDS4.Tests/PureDS4.Tests.csproj`). Installer projects under `installer/` are outside the solution.
- `PureDS4/App.xaml.cs` is application startup/composition. `PureDS4/DS4Control/ControlService.cs` owns broad runtime orchestration. `PureDS4/DS4Control/Mapping.cs` is the input-mapping hot path. `PureDS4/DS4Library/` and `HidLibrary/` implement controller/HID protocols. `PureDS4/DS4Control/Viiper/` implements the virtual-output backend. `PureDS4/DS4Control/DTOXml/` and `BackingStore` in `ScpUtil.cs` own persisted settings/profile formats. `PureDS4/DS4Forms/` contains WPF views and view models.
- Generated resource/settings designer files should be regenerated through their source `.resx`/`.settings` workflow, not hand-edited without a specific reason.

## Build and checks

Use native Windows/PowerShell tooling and repository-local configuration. `global.json` pins SDK `8.0.421` with roll-forward disabled.

```powershell
dotnet restore PureDS4.sln
dotnet build PureDS4.sln -c Debug -p:Platform=x64
dotnet test PureDS4.sln -c Debug -p:Platform=x64
dotnet build PureDS4.sln -c Release -p:Platform=x64
```

Run the narrowest relevant test first, then the full x64 suite and build. Use `--no-incremental` when comparing warning counts. Some `LiveProcessCapture*` tests can be inconclusive without a suitable live Windows audio session; report them as skipped, not passed. Report every check as passed, failed, skipped, or not run, and separate introduced from pre-existing failures when evidence allows.

Run Release, publish, release-input, and installer checks whenever packaging or publish shape changes; `utils/post-build.py` depends on the published layout. `installer/release-inputs.json` is the release-input authority: a public artifact must not be produced while its calculated `releaseReady` is false.

## Publication gate

Before public release, verify all applicable evidence rather than inferring readiness from CI:

- reviewed source and artifact traceability to the full commit SHA;
- secret scan and locked, sourced, licensed, integrity-checked, and vulnerability-checked dependencies;
- no unresolved redistribution blocker in `installer/release-inputs.json`;
- coherent PureDS4 version, metadata, URLs, notices, release notes, checksums, and source availability;
- clean install, explicit replacement of old products, repair, reboot boundaries, upgrade where applicable, full uninstall, reinstall, rollback, and failed-install recovery;
- DS4 USB and Bluetooth hardware gates, each claimed separately;
- truthful professional UI/UX, accessibility checks appropriate to WPF, and owner visual acceptance of the exact artifact;
- logs, diagnostics, support instructions, backup/recovery steps, and a defined release withdrawal path;
- owner acceptance and an independent final review appropriate to installer, driver, licensing, and release risk.

After any authorized activation, smoke-test the activated artifact, verify version and behavior, inspect available diagnostics, and state the observation window. On failure, stop further use, preserve evidence, and follow the authorized recovery path.

## DualShock 3 milestone gate

This gate is separate from, and later than, publication. It is not part of the first release and must not gate it. Before any release claims DualShock 3:

- exercise DsHidMini installation, driver state, and removal on the target machine;
- characterize whatever ScpToolkit or ScpTools state that machine already carries, including coexistence and cleanup;
- validate detection, physical readings, mapping, and virtual output with genuine DS3 hardware over each supported transport, claimed separately;
- validate real gameplay, disconnect/reconnect, and application restart;
- validate installation, upgrade, uninstall, and recovery with DS3 support enabled;
- restore the presentation this release hides and set `ProductScope.DualShock3Offered`, then re-run the ordinary product-boundary tests against the new claim;
- obtain owner acceptance on the exact artifact.

The friend who owns the hardware runs it on a machine the owner cannot hot-fix, so a self-sufficient setup and recovery path is part of this gate, not an optional extra.

## Architecture and change discipline

- Keep .NET 8 and WPF unless measured repository evidence shows a material product problem they cannot solve.
- Start with logical seams inside the existing production project. Add projects only for a demonstrated dependency-enforcement, compilation, ownership, or testing benefit.
- Make dependencies and ownership explicit before considering a DI container. Do not add DI, MVVM, or other framework packages by default.
- Prefer native or existing solutions. Ask before adding a production package and consider maintenance, security, licensing, binary size, cost, and lock-in.
- Do not rewrite or broadly decompose `Global`, `ControlService`, `Mapping`, device protocols, or VIIPER in one step. Characterize behavior and threading first, then make one small reversible change with targeted tests.
- Do not call something a performance problem or set a latency budget without measurement. Before changing report, mapping, or output timing, establish comparable report-rate, processing, output, allocation, GC, and p50/p95/p99 evidence where practical.
- Preserve profile/settings compatibility when intentionally supported. Serialization changes require round-trip and relevant legacy-fixture tests.
- Remove out-of-scope inherited behavior only in characterized, reviewable slices. Do not preserve unsupported UI or payload merely because deletion is difficult, and do not perform a broad purge without proving shared DS4/DS3 paths.

## High-risk boundaries and host safety

Use stronger characterization, failure tests, recovery evidence, and independent review for:

- physical HID, Bluetooth/USB protocol, calibration, report parsing/writing, and hotplug;
- DS3, DsHidMini, and legacy ScpToolkit/ScpTools ownership and replacement;
- `ControlService` lifecycle, synchronization, profile switching, and disconnect/reconnect;
- `Mapping` and `DS4StateFieldMapping` input translation;
- VIIPER/USB-IP output, feedback, audio, and output-slot ownership;
- HidHide, FakerInput, scheduled tasks, elevation, installers, services, drivers, and system state;
- profile/settings serialization, import, and migration;
- controller audio, microphone, haptics, rumble, and related timing.

Read-only inspection is allowed when relevant. Do not install, repair, remove, replace, start/stop, or bypass VIIPER, USB-IP, HidHide, FakerInput, scheduled tasks, services, drivers, or another controller application without explicit authorization for that exact operation. Before a destructive or consequential host mutation, resolve exact targets, record applicable state/backups, prove a recovery path, and stop for owner action when elevation is required; never bypass UAC.

## Completion and handoff

- Inspect `git status` before edits, preserve ambiguous work, keep changes coherent and reversible, and review the full diff.
- Report product changes separately from guidance/documentation changes. Include material decisions, commands and concise results, owner-verification steps, checks not run, remaining risks, and the next gate.
- A task is complete only to its highest demonstrated status. Never claim release readiness, activation, recovery, hardware support, or observation without the corresponding evidence.
