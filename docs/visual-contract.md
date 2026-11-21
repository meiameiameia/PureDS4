# Visual contract

PureDS4 is a compact Windows desktop tool with a Windows 11 / Fluent-inspired
visual direction implemented in the existing WPF presentation layer. Its
interface favors
scannable tables, property groups, explicit state, and predictable navigation
over decorative cards or dashboard galleries. Light and dark themes share the
same component geometry and interaction model.

The modernization covers the shared palette and controls, primary navigation,
Home, profiles, Settings, Tools, the profile editor and supporting runtime,
setup and migration dialogs. It does not migrate to WinUI, enable WPF's experimental
ThemeMode API, add a UI dependency, or change controller/backend behavior.
Native window chrome remains intact. On Windows 11, its caption background and
text follow the application's semantic palette through documented DWM attributes;
older systems retain their default frame. This applies to the main window and
themed dialogs, including live theme changes. In high contrast, caption colors
return to system defaults. The native activation/resize border is not overridden.
Mica/acrylic and a custom title bar are not part of this slice.
Fluent-inspired styling is not a claim of full WinUI
control parity or completed Windows accessibility/DPI validation.
This modernization targets the planned 26.11.0 release (planned as 5.2.0); the published 5.1.2
artifacts are not replaced by these local presentation changes.

## Product identity

Windows conventions guide interaction, typography, focus and native chrome;
they are not a template for copying Microsoft product surfaces. PureDS4 keeps
its own controller-first identity: the original DS4 silhouette, restrained
blue accents, compact device information and clear physical/game-output states.
Avoid decorative branding on every row or turning specialist tools into a
generic Windows Settings replica.

The product mark ("Brand Pad") is an original, simplified white gamepad (d-pad,
four plain buttons, two sticks, light-blue light bar) on a PureDS4-blue rounded
tile with a soft top sheen and a transparent exterior. No PlayStation symbols.
`branding/brand-pad/generate.ps1` is its only source: it writes the SVG, the
1024 px master `PureDS4/Resources/AppIcon.png`, and the multi-size
`PureDS4/DS4W.ico` and `PureDS4/Resources/DS4W.ico`, drawing every frame from the
vector at its own size. The 16-24 px frames must remain recognizable at native
size on both light and dark Windows surfaces.
The default tray icon uses the mark; the optional battery and monochrome tray
choices remain separate user-selected variants.

Product-facing names use **PureDS4**, maintained by **meiameiameia**. The
executable, storage, IPC, task, shortcut, and installer upgrade identities are
all PureDS4-owned; see [`RELEASE_CONTRACT.md`](RELEASE_CONTRACT.md) for the
full list and for the two names that deliberately stay on the inherited
lineage. Normal UI must not advertise DualSense, DualSense Edge, Switch Pro, or
Joy-Con support, and must not offer controls that exist only on that hardware,
including while a profile is edited with no controller attached. The first
release supports the DualShock 4 only, so ordinary UI must not offer or imply
DualShock 3 support either; that is a planned milestone with its own gate.

## Foundation

`PaletteLight.xaml`, `PaletteDark.xaml` and `PresentationControls.xaml` are
shared by the application and the setup host. `Foundation.xaml` adds only
application-specific converters and document styling. The setup links the
presentation sources, not the controller application or its startup paths.
This includes confirmation, progress, completion, restart and error pages,
as well as the offline license reader and native frame.

Tray and internal context menus use the same implicit menu styles, including
checked/disabled states, separators and nested menus. Native WPF menu routing,
access keys and submenu ownership remain authoritative. Tooltips, disclosures
and progress indicators use the shared palette. Ordinary confirmation/error
dialogs use AppDialog with the original confirmation defaults; fatal startup
failures retain the native fallback. Windows file/folder dialogs, UAC,
SmartScreen, native tray balloons and external vendor interfaces are OS-owned,
not reskinned application surfaces. Dormant updater/language/first-launch
views must not be activated to complete this presentation gate.

Installer failure details scroll independently of the diagnostic/retry/close
actions. The Microsoft agreement remains unchecked and the install/update
action remains unavailable until the existing consent policy permits it.
Offscreen checks exercise actual markup and shared templates with fictitious
state; they do not prove a real tray popup, installer transaction or desktop
keyboard/DPI/high-contrast acceptance.

- Base spacing is 4, 8, 12, 16, 24, and 32 DIP.
- Dense, default, and prominent controls are 24, 28, and 32 DIP high.
- Page, group, body, secondary, and column-header text are 20, 13, 12, 12,
  and 11 DIP.
- The UI font is Segoe UI Variable Text with Segoe UI fallback. Diagnostic
  text uses Cascadia Mono with Consolas fallback.
- Control corners are 4 DIP; bounded Home detail surfaces use 8 DIP. Circular
  controls may remain circular when shape conveys
  their function.
- Navigation uses Segoe Fluent Icons with Segoe MDL2 Assets fallback, alongside
  visible text labels. Font fallback on a real Windows 10 host remains unverified.
- Every keyboard-operable control has a visible focus indication. Icon-only
  controls require an accessible name and tooltip.

Control captions share centered line boxes and a one-DIP upward optical baseline
correction, without changing page/body text or access-key semantics. The top banner
has an 8 DIP gap both below the native frame and above the first navigation header.

Theme colors are semantic resources, not control-local literals:

- surfaces: base, raised, inset, base-hover, raised-hover, pressed, selected, selected-hover;
- text: primary, secondary, disabled;
- actions: accent, hover, pressed, accent foreground, focus;
- status: neutral, success, warning, error, recovery.

Light/dark surfaces use cool slate grays and the product mark's muted blue as
the action accent. Pressed commands have a neutral fill; persistent selection
has a blue fill, with a distinct blue hover state that keeps selection legible.
Default-button borders and keyboard-focus outlines are separate cues. Hover,
press, selection and focus never move or resize a control.
Dialog action rows share a 12 DIP top gap and 12 DIP inset above their buttons;
close-only actions use an 88 DIP minimum width. The editor and Settings use
180 DIP section navigation. Auto-profile search shrinks with its list column,
stays left-aligned, and rule rows share the controller table's selection/focus
treatment. At short editor heights, the controller diagram scrolls within its
panel instead of shrinking its hit targets to icon size; the mapping list and
profile actions remain independently accessible.
Decorative
separators use `SurfaceDividerBrush`; interactive control boundaries keep the
separately contrast-tested `BorderDefaultBrush`. Do not lower interactive
contrast requirements to obtain quieter decorative dividers.

Status is never communicated by color alone. Use the shared status chip or
banner with a glyph and plain-language label.

Home sizes its workspace to the controller rows rather than reserving a fixed
360 DIP panel in maximized windows. Direct-controller and recovery sessions use
the same semantic surfaces and readable copy as managed controllers, but retain
their distinct state and applicable return/recovery action.
The administrator notice uses a compact message with the existing restart and
dismiss actions. It must not replace or suppress game-output or recovery alerts.
Apply/Save explanations live in their tooltips rather than consuming a persistent
line in the profile editor; unavailable live readings explain why they are disabled.

Specialist tool pages keep Tools highlighted as their parent section and include
an explicit Back to Tools action. The selected TabItem still owns the actual
page content; the parent cue must not manufacture a second selected page.

### WPF component contract

`DS4Forms/Themes/Foundation.xaml` owns shared metrics, templates, and interaction
states. Theme dictionaries supply semantic colors; changing theme must not
change geometry. Existing Bridge resources are compatibility names for
unmigrated views, not a second design system. New work uses Foundation styles.
Do not introduce a UI framework, dependency, or duplicate per-screen templates.

- Keep native window chrome and WPF controls, keyboard navigation, access keys,
  selection, and automation peers. Icon-only actions need an accessible name.
- Define normal, hover, pressed, selected, keyboard-focus, and disabled states;
  add validation and busy states where applicable. Focus is not selection.
  State changes must not change padding, borders' thickness, or control size.
- Use the shared control heights as minima, not clipping boxes. Use Grid
  auto/star sizing and constrained scrolling; translated text may grow.
  Trimming is allowed for names with a full-text tooltip, never the only
  presentation of an error or essential action.
- Use dynamic resources for theme brushes. Layout rounding and pixel snapping
  apply at the window boundary. Real per-monitor DPI behavior requires testing,
  including popups and dialogs; an HTML mock is not WPF evidence.
- Menus/popups anchor to their invoking control, remain on-screen, support
  Escape and keyboard use, and return focus predictably. Do not replace
  existing live-preview, cancellation, or persistence semantics during styling.

### Home composition

Use compact selectable controller rows (48 DIP minimum at ordinary text size),
with identity/slot, protection, profile, battery, and lightbar. Header and rows
share column metrics and the same scroll viewport. A child-control interaction
selects its owning row before acting; never derive a hardware slot from a
visual list index. Preserve profile creation and controller details.

Keep selected-controller runtime output/readiness and actions outside the
scrolling rows, with the target identity visible. USB does not offer Disconnect.
Home starts below navigation in restored and maximized windows; it does not
float vertically in unused window space.
Lightbar exposes From profile / Custom using the existing color behavior.
Physical exposure and virtual output type are separate concepts. Do not copy
selected-controller readiness onto other rows or infer it from protection.
Native sessions and recovery remain reachable, with bounded scrolling.

Home uses the original `DualShock4SilhouetteGeometry` vector, not an optional
device bitmap, for its decorative controller icon. The profile column grows only
to 220 DIP (212 DIP usable selector width); long names retain their full tooltip.
Home controller rows use their selected background without an additional side
stripe; keyboard focus retains its separate outline. Selection cues elsewhere
keep their proportional end insets, and primary-tab underlines scale with the
header width rather than staying a fixed tiny mark.

Controller details separates observed runtime state from automatically saved
profile properties. Use aligned label/control rows and shared horizontal sliders,
not summary-card galleries. Keep output/audio selector commit timing intact,
explain unavailable capabilities, and hide connection actions that do not apply.

Controller detection uses the same setting rows and switches as Settings,
with a separately bounded detected-device list and device-specific options.
The DS3 experiment remains hidden in ordinary builds. Preserve the existing
detection bindings and close-to-save behavior; presentation fixtures must
never enumerate real devices or save the owner's settings.

### Settings, Tools, and Auto Profiles composition

Settings, Tools, and Auto Profiles share Home's 16/8 DIP workspace inset. Settings
categories and Tools use one content column capped at 760 DIP, so switching
categories never resizes the page.

Options are setting rows (`FoundationSettingRowStyle`), the same geometry as Tools'
rows: a title and a one-line plain-language description on the left, and the control
on the right. Runs of rows sit under a short section label. A setting that only
applies while another is on uses an indented sub-row directly below it.

On/off options use the shared switch (`FoundationToggleSwitchStyle`). It is a
`CheckBox`, so bindings and handlers are unchanged, and every switch carries an
`AutomationProperties.Name` because it has no visible text of its own.

Do not show permanently disabled controls. A feature that is unavailable for the
whole release (such as language packs)
stays bound but hidden until it can work.

5.1.1 exposes a General switch for read-only release notifications and a manual
Check action in Tools. A newer version uses the existing dismissible neutral
banner with an Open release page action; checking and failures use inline Tools
text, never a modal interruption. Download and installation remain manual.

Auto Profiles hides the rule editor until a rule is selected, showing a short
explanation instead; options that apply to every rule stay visible in their own
section.

Input diagnostics is a controller-first instrument panel, not another settings
form. Each connected controller uses the original DS4 silhouette, identity,
connection and text/glyph verdict, followed by aligned, monospaced measurements.
Long names, explanations and the measured duration wrap without losing data.
The list scrolls independently; Copy report and Start over stay outside it.
Presentation fixtures cover zero, one, two and four controllers, live property
notifications and both themes. They never start sampling or touch real devices.

About keeps the existing product mark and version together, not a promotional
hero. Overview, shortcuts and licenses/credits use native dialog tabs with
independently scrolling content and a fixed Close action. The maintainer and
upstream lineage remain explicitly credited; repository/notices links keep their existing
destinations. Shortcut rows describe the existing behavior, not new features.

Output slots keeps a compact, aligned table and the existing selected-slot
action scope. Reservation choices and their explanation stay readable in the
compact window; selecting another slot must not edit the previous one. The
plug dialog keeps DS4/Xbox 360 and Dynamic/Permanent defaults, with explicit
confirmation and fixed actions below bounded scrolling content.
Runtime log gives search and severity filters their own row. Time and severity
columns leave room for fully wrapped messages; native virtualized pixel
scrolling remains enabled. Copy, export, clear and the sharing/privacy notice
stay outside the scrolling event list. Presentation fixtures never connect
virtual output or read, copy, export or clear the user's real logs.

Full log messages and release-note documents use resizable native dialogs with
bounded, vertically scrolling read-only content and a fixed closing action.
Log paragraphs are left-aligned; formatted text, selection and existing link
commands remain available. The release-note viewer continues to use the manual
release policy and points to the official GitHub releases page rather than
claiming the published channel does not exist. Styling does not enable download
or installation, and presentation fixtures must not execute document links.

## Navigation and information architecture

The permanent primary navigation is **Home / Profiles / Settings / Tools**.
The compact top strip retains native TabControl selection/content/automation
behavior. Headers use text plus an icon, a short accent selection indicator,
and a separate keyboard focus ring. Selection does not change header geometry;
hover applies only to the visible header, not its selected page content.
Textual section navigation uses the same short accent cue and independent focus
ring. Decorative editor/group separators use the quieter surface divider, not
the contrast-tested border reserved for interactive fields.
Specialist workspaces open from Tools or an in-context action and never become
permanent primary tabs. Home owns controller selection, runtime readiness,
game output, exposure actions, and the path to controller details.

Settings keeps a stable 180 DIP textual category list in this order: General,
Controller, Game output, Integrations, Compatibility, Advanced. Profile Editor
uses a stable textual section list and does not resize the main window.

The profile browser keeps its seven actions together, with search on the same
line when space permits and below them at compact widths. Its count is quiet
metadata rather than a badge. Table and loading-surface borders use the decorative
divider; the search field retains its contrast-tested interactive border and
whole-field focus outline. These presentation changes do not alter search,
selection, import/export, or profile commands.

Profile editing keeps name/actions reachable while properties scroll. The name
of an existing profile is a 20 DIP heading with a full-name tooltip, not a disabled
input; a profile still being named retains its editable field. Apply, Save and
Cancel keep their existing commit/close semantics. Mapping
rows offer a keyboard-accessible Edit action; diagram hit targets stay unchanged.
Numeric editors retain native parsing, spin and validation. Macro actions remain
outside scrolling options. The local curve page keeps explicit copy-back semantics
and upstream credits; it does not save a profile automatically.

Lightbar uses setting rows for output mode, battery/charging feedback and effects.
RGB channels share aligned sliders and always-visible numeric values. The existing
battery caption (color or dim by battery) stays visible beside its switch, and
rainbow's action indicates whether it will turn the effect on or off. Preserve
the existing click handlers, preview timing, numeric limits and passthrough mode.
Lightbar properties scroll inside the editor; Apply, Save and Cancel stay outside
that viewport. Whole-window previews use disposable profile fields, not real
controller output or the owner's profile.

Axis Config, Touchpad, Gyro and Advanced are built from one property row
(`FoundationPropertyRowControlStyle`, a `HeaderedContentControl`): the label on the
left without a trailing colon, the control right-aligned in one shared 196 DIP
column so every row of a page has the same right edge, and the unit (`Tag`) in a
fixed narrow column after it. Numeric boxes are 96 DIP wide and choices 196 DIP
(`FoundationPropertyNumericStyle`, `FoundationPropertyComboStyle`); on/off options
use the shared switch. No option sits outside a row. Rows are grouped under short
section headings (stick, trigger, motion axis, mode groups). An option that only
applies while a switch is on is an indented sub-row
(`FoundationPropertySubRowControlStyle`) that is hidden, not greyed, while the
switch is off; the only rows that stay visible but disabled are the program-launch
switch (Browse is what turns it on) and the read-only controller number. Explanatory
tooltips sit on the row; a tooltip that only repeats the label is not added.
`ProfilePropertyPageLayoutTests` enforces the shared edge, the widths, the absence
of bare controls and trailing colons, and the hide-instead-of-grey rule.
They use bounded property columns and the shared
compact property-row surface. Numeric parsing, ranges and LostFocus commits,
output-mode order, nested modes, trigger menus and calibration actions stay
unchanged. Touchpad's mouse-gesture options use named switches beside their
dependent numeric fields. Assigned tilt/swipe actions and long property labels
wrap inside their column without losing the native Label target/access key.
Filter parameters stack vertically at compact widths. All modes remain
reachable without horizontal scrolling; commit/close actions stay outside
the properties viewport. Paired trigger/sensor columns share the available
width; geometry checks use the content viewport, not just the outer window.
Synthetic mode/field checks and full-window previews
do not prove real mouse/gyro/controller behavior or profile persistence.

Advanced uses the same bounded property column, compact rows and named switches.
Keep output selection separate from disabling virtual output, and preserve audio,
idle-disconnect and program-hook dependencies, limits, events and commit timing.
Nested audio/absolute-mouse options must remain reachable when expanded.
Preset and rename dialogs keep confirmation actions below a bounded scroll area;
descriptions wrap and long selector names retain a full-name tooltip. Profile
duplication remains compact and inline in the browser, with its input alongside
secondary/primary actions. Calibration keeps readiness-controlled Save and retry/cancel actions
fixed while status content scrolls. These views share the dialog action-row style,
native theme-aware frame and existing handlers; presentation checks do not apply
presets, copy or rename files, measure sticks, send rumble or open audio endpoints.

Binding selection separates keyboard/mouse, absolute mouse, controller and extra
options into native tab pages. It opens on the current output's page and retains
the existing choose-to-close behavior; regular/shift and Default/Unbound stay in
a fixed footer. Keyboard groups wrap vertically at compact widths, while the
controller hit-target canvas scales uniformly without changing its coordinates.
Macro recording keeps Record/Stop and Save/Cancel outside scrolling step/options
panes. Variable-height steps and trigger lists use pixel scrolling with native
virtualization retained. Special-action editing keeps Save/Cancel fixed, provides
bounded trigger/options panes, and lets the form itself scroll when the inline
editor is too short. Preserve output indices, independent options, numeric limits,
explicit macro-step commits and existing event handlers.
The duplicated section heading is hidden only while that inline action form is
open; switching sections or returning to the action list restores the heading.
Detached fixture checks cover page changes, embedded-editor return,
recording-state presentation and
field edits; they do not record real input, execute actions or save personal data.

Swipe rotation wraps long profile names within a pixel-scrolled native list;
selection checkboxes keep their complete accessible names and two-way binding.
Lightbar sequences retain the existing color/duration text and trigger semantics.
New-step options scroll independently while Add/Delete/Clear and Save remain
reachable. The new-step heading must not imply that selecting a sequence item
edits it. Detached fixtures exercise empty/populated lists and bound field edits,
without emitting lightbar output or saving a profile.

## User-facing language and state

Normal UI says **game output**. `Managed / Virtual` and `Native Physical` are
controller exposure modes. VIIPER, USB/IP, HidHide, package versions, and
topology belong in Technical details, setup diagnostics, or engineering logs.

Long-running or recoverable operations use an in-window busy surface and
status banner. Ordinary runtime dialogs use the themed application dialog;
native message boxes remain only for fatal failures before WPF can be built.
Degraded mode must preserve Settings, repair, diagnostics, and recovery paths.

Game-output setup, setup progress, direct-controller confirmation and generic
app dialogs use the shared native frame and a fixed action/operation-notice row
below vertically scrolling content. Setup keeps its existing choices and
disclosure of Technical details; Enter must not implicitly request a repair.
The shared disclosure retains a native keyboard-operable toggle and expansion
state. Progress disables its native Close command while the existing closing
guard remains authoritative. Completion keeps the existing automatic close,
without inventing a cancellation or retry action.
Detached markup checks cover compact layouts, disclosure, long status/messages
and unchanged confirmation defaults. Full-window previews use fictitious setup
states: they do not install, repair, alter exposure or prove backend recovery.

Legacy import, removal planning and the competing-application notice keep their
existing commands outside bounded scrolling content. Import candidates and
per-file results wrap within the list viewport, with native selection, focus
and pixel-scrolled virtualization retained. Preservation notices and blockers
remain readable rather than being trimmed. Storage selection keeps each choice
beside its description in one bounded scroll area; an unwritable portable
folder remains unavailable. Do not introduce a default Enter confirmation or
change scan, import, uninstall, re-check, exit or storage-activation behavior.
Presentation fixtures use pure copy helpers and fictitious surveys/results,
never the product constructors that inspect real installations or data paths.

## Validation

Build and automated tests validate resource construction, theme contrast,
state mapping, and preserved behavior. Final visual validation is manual from
the exact disposable executable. A virtual-output change also requires the
controller and in-game manual gate described in `HARDWARE_VALIDATION.md`.

`FluentShellPresentationTests` combines the actual Home markup and shell styles
with disposable state, checks theme changes on existing controls and distinct
navigation states, and optionally renders WPF previews. It never starts the
product App/MainWindow or accesses personal profiles/hardware. These previews
are presentation fixtures, not screenshots or acceptance of the installed app.

`NativeWindowFrameTests` checks the shared style without creating a handle, plus
exact caption requests, successful native setters and live theme changes on a
hidden, test-owned native window on Windows 11. It closes that window and
detaches subscriptions. High-contrast
palette fallback is tested without changing host settings; real high-contrast,
inactive-window appearance, Snap and per-monitor DPI still need manual acceptance.
The WPF content previews do not render the OS-owned frame.

Previews shown for visual review should include the entire window: native
caption, all client content and the footer, at compact and normal sizes.
`NativeWindowPreviewTests` lifts the real shell, workspace and dialog markup into a
plain, test-owned Window with fictitious state. Setting
`PUREDS4_NATIVE_PREVIEW_DIRECTORY` to an existing absolute disposable directory
explicitly enables native full-window PNG capture; ordinary runs show nothing.
It never constructs the product App/MainWindow or starts services, hardware,
profile persistence or tray integration. Captures are labelled UI previews,
not screenshots of the installed app or hardware acceptance. Each shown window
is closed after capture; unrelated desktop windows are never captured.
While a fullscreen game or another focus-sensitive activity is in use, do not
run native visible-window captures. With `PUREDS4_NATIVE_PREVIEW_DIRECTORY`
unset, `PUREDS4_VISUAL_PREVIEW_DIRECTORY` enables offscreen WPF content previews
without showing windows or creating their native handles. These previews omit
the OS-owned caption/frame and must not be presented as full native screenshots.
Native-frame acceptance is a separate, explicitly scheduled desktop pass.
Tool previews retain their actual hidden-tab navigation path, parent-section cue
and return action. The Home matrix includes direct-controller and recovery
fixtures as well as managed output; synthetic state is not evidence of an actual
exposure transition or successful recovery.

For each migrated surface check light/dark themes, 720×480 and 1024×660 windows,
0/1/2/4 controllers, long names/localized labels, missing output, and service
stopped. Exercise keyboard-only use, selection/focus, popups, and reconnection.
Manual acceptance also covers Windows scaling at 100/125/150/200%, movement
between monitors, and high contrast. Automated layout checks with synthetic
data do not establish hardware, actual DPI, or owner visual acceptance.
