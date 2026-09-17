# Visual contract

PureDS4 is a compact Windows desktop tool. Its interface favors
scannable tables, property groups, explicit state, and predictable navigation
over decorative cards or dashboard galleries. Light and dark themes share the
same component geometry and interaction model.

## Product identity

The product mark is **Mapped Pad**: a split white/blue controller on a deep
blue tile, joined by an amber mapping seam. The canonical source is
`branding/mapped-pad/mapped-pad-concept.svg`; generated ICO assets must include
small Windows sizes as well as 256 px. Battery tray variants add a separate
semantic badge and never replace the mark with a number.

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

- Base spacing is 4, 8, 12, 16, 24, and 32 DIP.
- Dense, default, and prominent controls are 24, 28, and 32 DIP high.
- Page, group, body, secondary, and column-header text are 16, 13, 12, 12,
  and 11 DIP.
- The UI font is Segoe UI Variable Text with Segoe UI fallback. Diagnostic
  text uses Cascadia Mono with Consolas fallback.
- Corners are 2 DIP. Circular controls may remain circular when shape conveys
  their function.
- Every keyboard-operable control has a visible focus indication. Icon-only
  controls require an accessible name and tooltip.

Theme colors are semantic resources, not control-local literals:

- surfaces: base, raised, inset, hover, selected;
- text: primary, secondary, disabled;
- actions: accent, hover, pressed, accent foreground, focus;
- status: neutral, success, warning, error, recovery.

Status is never communicated by color alone. Use the shared status chip or
banner with a glyph and plain-language label.

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
Lightbar exposes From profile / Custom using the existing color behavior.
Physical exposure and virtual output type are separate concepts. Do not copy
selected-controller readiness onto other rows or infer it from protection.
Native sessions and recovery remain reachable, with bounded scrolling.

Controller details separates observed runtime state from automatically saved
profile properties. Use aligned label/control rows and shared horizontal sliders,
not summary-card galleries. Keep output/audio selector commit timing intact,
explain unavailable capabilities, and hide connection actions that do not apply.

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
whole release (such as language packs or update checks without a signed channel)
stays bound but hidden until it can work.

Auto Profiles hides the rule editor until a rule is selected, showing a short
explanation instead; options that apply to every rule stay visible in their own
section.

## Navigation and information architecture

The permanent primary navigation is **Home / Profiles / Settings / Tools**.
Specialist workspaces open from Tools or an in-context action and never become
permanent primary tabs. Home owns controller selection, runtime readiness,
game output, exposure actions, and the path to controller details.

Settings keeps a stable 180 DIP textual category list in this order: General,
Controller, Game output, Integrations, Compatibility, Advanced. Profile Editor
uses a stable textual section list and does not resize the main window.

Profile editing keeps name/actions reachable while properties scroll. Mapping
rows offer a keyboard-accessible Edit action; diagram hit targets stay unchanged.
Numeric editors retain native parsing, spin and validation. Macro actions remain
outside scrolling options. The local curve page keeps explicit copy-back semantics
and upstream credits; it does not save a profile automatically.

## User-facing language and state

Normal UI says **game output**. `Managed / Virtual` and `Native Physical` are
controller exposure modes. VIIPER, USB/IP, HidHide, package versions, and
topology belong in Technical details, setup diagnostics, or engineering logs.

Long-running or recoverable operations use an in-window busy surface and
status banner. Ordinary runtime dialogs use the themed application dialog;
native message boxes remain only for fatal failures before WPF can be built.
Degraded mode must preserve Settings, repair, diagnostics, and recovery paths.

## Validation

Build and automated tests validate resource construction, theme contrast,
state mapping, and preserved behavior. Final visual validation is manual from
the exact disposable executable. A virtual-output change also requires the
controller and in-game manual gate described in `AGENTS.md`.

For each migrated surface check light/dark themes, 720×480 and 1024×660 windows,
0/1/2/4 controllers, long names/localized labels, missing output, and service
stopped. Exercise keyboard-only use, selection/focus, popups, and reconnection.
Manual acceptance also covers Windows scaling at 100/125/150/200%, movement
between monitors, and high contrast. Automated layout checks with synthetic
data do not establish hardware, actual DPI, or owner visual acceptance.
