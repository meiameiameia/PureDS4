# Visual contract

DS4Windows Reworked is a compact Windows desktop tool. Its interface favors
scannable tables, property groups, explicit state, and predictable navigation
over decorative cards or dashboard galleries. Light and dark themes share the
same component geometry and interaction model.

## Product identity

The product mark is **Mapped Pad**: a split white/blue controller on a deep
blue tile, joined by an amber mapping seam. The canonical source is
`branding/mapped-pad/mapped-pad-concept.svg`; generated ICO assets must include
small Windows sizes as well as 256 px. Battery tray variants add a separate
semantic badge and never replace the mark with a number.

Product-facing names use **DS4Windows Reworked**, maintained by
**meiameiameia**. Compatibility identifiers such as the executable, storage,
IPC, and installer upgrade codes retain the established DS4Windows lineage.

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

## Navigation and information architecture

The permanent primary navigation is **Home / Profiles / Settings / Tools**.
Specialist workspaces open from Tools or an in-context action and never become
permanent primary tabs. Home owns controller selection, runtime readiness,
game output, exposure actions, and the path to controller details.

Settings keeps a stable 180 DIP textual category list in this order: General,
Controller, Game output, Integrations, Compatibility, Advanced. Profile Editor
uses a stable textual section list and does not resize the main window.

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
