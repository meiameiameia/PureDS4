# "The Bridge" icon family

Original icon family for PureDS4. It replaces the inherited DS4W
mark, which combined a DualShock 4 silhouette with the Windows logo — a
combination that reproduces PlayStation trade dress and a Microsoft trademark,
and whose monochrome variant is a solid white fill that is invisible in a light
Windows tray.

Nothing in this directory is wired into the application, the installer, or the
build. These are source assets plus a reproducible generator.

## The mark

Three elements on a horizontal axis:

| Element | Meaning |
| --- | --- |
| Left: solid block, chamfered top-left | The physical device. Solid = real. |
| Centre: tall pillar crossing the deck | The application — the point of intervention. |
| Right: outlined block | The virtual device. Hollow = emulated. |

A one-unit gap sits on each side of the pillar. It is load-bearing, not
decoration: it says the crossing is an assembled piece, so removing it leaves
both sides intact. That is the product's reversibility promise stated as
geometry.

The solid-versus-hollow contrast is the element that survives every reduction,
which is why it carries the primary meaning rather than any silhouette detail.

There is no controller silhouette, no PlayStation face-button symbol, no Xbox
reference, no Windows logo, and no lettering.

## Geometry contract

The master is a 24-unit grid, half-open `[x0,x1) x [y0,y1)`, centred on (12,12):

```
solid anchor    ( 1,  9,  7, 15)     chamfer 2 units, top-left
gap             (  x = 7  )
left span       ( 8, 11, 11, 13)
pillar          (11,  6, 13, 18)
right span      (13, 11, 16, 13)
gap             (  x = 16 )
hollow anchor   (17,  9, 23, 15)     2-unit stroke, 2x2 hole
```

Every edge is axis-aligned, so every raster size is exact — no resampling and
no antialiasing anywhere in the family.

### Progressive detail

16, 20, 24 and 32 are hand-tuned rather than scaled. Sizes 40 and above are
derived from the master by rounding edges to whole pixels.

| Size | Chamfer | Deck spans | Gaps | Hollow stroke |
| --- | --- | --- | --- | --- |
| 16 | dropped | dropped | kept | 1 (keeps a 3x4 hole) |
| 20 | dropped | dropped | kept | 2 |
| 24 | 2 | kept | kept | 2 |
| 32 | 3 | kept | kept | 2 |
| 40+ | scaled | kept | kept | scaled |

At 16 and 20 the deck spans are dropped because they only add mud at that size.
The gaps are kept at every size: without them the pillar fuses into the solid
anchor in monochrome and the mark reads as one blob.

## Palette

| Role | Value |
| --- | --- |
| App tile background | `#22303D` |
| App tile mark | `#E9F0F6` |
| App tile pillar | `#E0A43C` |
| Flat mark | `#2E5F80` |
| Flat pillar | `#C8842A` |
| Tray, light Windows theme | `#20272C` |
| Tray, dark Windows theme | `#F4F7FB` |
| Gauge: Good / Full | `#3F9D5C` |
| Gauge: Half / Low | `#D08A2A` |
| Gauge: Critical | `#C4483F` |

## Files

`svg/` — vector source. `bridge-24.svg` is the master; `bridge-16/20/32.svg`
are the hand-tuned grids. Colour comes from `currentColor`, with the pillar
overridable through the `--bridge-pillar` custom property.

`ico/` — 30 Windows icon files. Each contains 16, 20, 24, 32, 40, 48 and, for
the application icons, also 64, 96, 128 and 256. Entries up to 128 are BMP for
legacy compatibility; 256 is PNG.

| File | Use |
| --- | --- |
| `Bridge.ico` | Executable, window, installer, shortcuts. Tile form. |
| `Bridge-Flat.ico` | Transparent-background colour mark for documents. |
| `Bridge-Light.ico` | Tray, light Windows theme. |
| `Bridge-Dark.ico` | Tray, dark Windows theme. |
| `Bridge-Degraded-{Light,Dark}.ico` | Virtual output unavailable. |
| `Bridge-Battery-{Critical,Low,Half,Good,Full}-{Light,Dark}.ico` | Battery, gauge in state colour. |
| `Bridge-Battery-…-{Light,Dark}-Mono.ico` | Battery, strict monochrome, for high-contrast trays. |
| `Bridge-Battery-Charging-{Light,Dark}[-Mono].ico` | Charging. |

`preview/` — flat PNG exports for README and release pages.

### Degraded state

The pillar breaks into two segments with a visible gap. It reads as "the link
is broken" at 16px and depends on shape alone, so it survives monochrome and
high-contrast rendering.

### Battery encoding

The right-hand anchor becomes a gauge that fills from the bottom, across five
levels, with charging adding a terminal nub on top. Level is carried by fill
height, so the icon still works in grayscale and for colour-blind users; colour
is a redundant second channel.

Five levels rather than ten: the hollow anchor's interior is four rows tall at
tray sizes, so ten steps are not distinguishable at 16px. The exact percentage
belongs in the tooltip, which already carries it. This replaces the inherited
set, which rendered two-digit numbers — those were unreadable at 16px, showed
red for everything below 100%, and displayed `99` at full charge.

## Regenerating

```bash
python branding/bridge/generate.py
```

Pure standard library; writes PNG and ICO directly. Edit the geometry tables at
the top of `generate.py` — `M` for the master and `HAND` for the hand-tuned
grids — and rerun. The SVGs in `svg/` are maintained by hand and must be kept
in step with those tables.

## Adopting the family

Not yet done, and each item is a separate change:

1. `PureDS4/PureDS4.csproj` — `ApplicationIcon`.
2. `DS4Windows/DS4Control/ScpUtil.cs` — `iconChoiceResources`.
3. `DS4Windows/DS4Forms/ViewModels/TrayIconViewModel.cs` — the percentage
   mapping, from eleven buckets to five plus charging.
4. Automatic light/dark tray selection. `Util.SystemAppsUsingDarkTheme()`
   already exists; a `WM_SETTINGCHANGE` handler does not.
5. `installer/PureDS4.Bootstrapper/InstallerWindow.xaml` and
   `installer/PureDS4.Bundle/Bundle.wxs` — installer and bundle icons.
6. Shortcut icons in `installer/PureDS4.SetupActions/Program.cs`.
7. `README.md` — repository and release presentation.

Item 3 changes what the tray communicates and item 4 changes runtime behaviour,
so both need owner validation against real hardware over a full discharge.
