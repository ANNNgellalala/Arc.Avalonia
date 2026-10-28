# Arc for Avalonia

A faithful Avalonia 12 / .NET 10 port of **[Arc UI](https://uiarc.dev)** — the React component and block
library for AI-assisted frontend development. The goal is design fidelity, not API parity: the same OKLCH
token set, eight accents, concentric radii, type scale, motion intent and accessibility rules, expressed
as Avalonia `ControlTheme`s and controls.

The port now covers **all 294 entries of Arc's catalog**: 128 components, 55 Special components and 111
blocks. Pro items are implemented independently in Avalonia from public documentation and visual
descriptions (no Pro source, no commercial UI library). The gallery mirrors the uiarc.dev sidebar taxonomy
and renders 219 pages in light and dark; every catalog entry is reachable (`--audit` reports 294/294).

Read the analysis first:

* [`docs/01-arc-analysis.zh.md`](docs/01-arc-analysis.zh.md) — what Arc is, the full component inventory,
  and a detailed analysis of its design thinking (tokens, motion, accessibility, copy, architecture).
* [`docs/02-component-specs.zh.md`](docs/02-component-specs.zh.md) — per-component specs distilled from the
  official markdown for 25 representative components.
* [`docs/03-implementation.zh.md`](docs/03-implementation.zh.md) — how the port is structured, what is
  implemented, and the deliberate trade-offs.
* [`docs/04-design-and-performance-review.zh.md`](docs/04-design-and-performance-review.zh.md) — a
  component-design and performance review: architecture, 12 global P0 hot spots, root causes (frame-clock
  fragmentation, per-frame drawing allocations, layout-property animation, no virtualisation) and a
  phased remediation plan. Per-area evidence lives in [`docs/perf/`](docs/perf/).
* [`docs/05-gallery-page-switch-lag.zh.md`](docs/05-gallery-page-switch-lag.zh.md) — measured root-cause
  analysis of the gallery's page-switch stutter (the sidebar was fully rebuilt on every click: 270–572 ms,
  28–108 MB; `ArcButton` costs ~1 ms to attach; 390 hidden spinner timers burn ~24% of a core while idle)
  plus the implemented P0 fix: incremental selection + lazy category expansion, measured at
  **366 → 15.8 ms** per item click.
* [`docs/06-agent-life-tools-extension.zh.md`](docs/06-agent-life-tools-extension.zh.md) — inventory of what
  the gallery contains today (294 catalog items, 236 control classes, 219 analysis pages / 233 after the P0
  slice) and what to extend it with for **AI Agent interfaces**: 57 proposed components/specials/blocks
  across 39 life and agent UI scenarios, 14 P0 designs, and a **UI-only scope** (agent logic, data and
  runtime stay in the host app) with a 14–17 week roadmap for a 3–4 person team (data in
  [`docs/arc-extension-catalog.json`](docs/arc-extension-catalog.json)).
* [`docs/07-p0-ui-implementation.zh.md`](docs/07-p0-ui-implementation.zh.md) — the P0 slice of that plan,
  implemented: 14 UI controls (agent transcript/run bar/approval sheet/question card/attention inbox/
  background dock, reminder row, daily brief, day agenda, reminder center, medication schedule,
  virtualized list, quick capture, routine editor) with 14 gallery pages, build/audit verification and
  known limitations.
* [`docs/08-project-structure-review.zh.md`](docs/08-project-structure-review.zh.md) — directory and project
  division review: what lives where, how the 23 catalog categories map onto the projects, and which
  boundaries need to change.
* [`docs/09-project-restructure-phase1.zh.md`](docs/09-project-restructure-phase1.zh.md) — the Phase 1
  restructure record: P0 hygiene, AgentUI/LifeToday extraction, AdvancedInputs/AdvancedActions merge,
  verification, and the explicitly deferred Phase 2 plan.

## Gallery

Every screenshot is rendered from the real Avalonia controls in the gallery, not mockups.

| Foundations | Tokens |
| --- | --- |
| ![Foundations](docs/images/foundations-light.png) | ![Tokens](docs/images/tokens-light.png) |

| Inputs | Actions |
| --- | --- |
| ![Inputs](docs/images/inputs-light.png) | ![Actions](docs/images/actions-light.png) |

| Charts | Charts, dark |
| --- | --- |
| ![Charts](docs/images/charts-light.png) | ![Charts dark](docs/images/charts-dark.png) |

| Advanced inputs | Effects |
| --- | --- |
| ![Advanced inputs](docs/images/advanced-inputs-light.png) | ![Effects](docs/images/effects-light.png) |

| Media | Content |
| --- | --- |
| ![Media](docs/images/media-light.png) | ![Content](docs/images/content-light.png) |

| Advanced charts (Pro) | Data grid (Pro) |
| --- | --- |
| ![Advanced charts](docs/images/advanced-charts-light.png) | ![Data grid](docs/images/data-grid-light.png) |

| Mega menu (Pro) | Share access (Pro) |
| --- | --- |
| ![Mega menu](docs/images/mega-menu-light.png) | ![Share access](docs/images/share-access-light.png) |

| Special components | Special components |
| --- | --- |
| ![Glass menu](docs/images/round4-glass-menu.png) | ![Orbit menu](docs/images/round4-orbit-menu.png) |

| Special components | Special components |
| --- | --- |
| ![Product gallery](docs/images/round4-product-gallery.png) | ![Vector search](docs/images/round4-vector-search.png) |

| Blocks | Blocks |
| --- | --- |
| ![Site header](docs/images/round4-site-header.png) | ![Metrics dashboard](docs/images/round4-metrics-dashboard.png) |

| Blocks | Blocks |
| --- | --- |
| ![Command palette](docs/images/round4-command-palette.png) | ![AI composer](docs/images/round4-ai-composer.png) |

More: [components](docs/images/components-light.png), [icons](docs/images/icons-light.png),
[motion](docs/images/motion-light.png), [navigation](docs/images/navigation-light.png),
[overlays](docs/images/overlays-light.png), [disclosure](docs/images/disclosure-light.png), [feedback](docs/images/feedback-light.png),
[identity](docs/images/identity-light.png), [data table](docs/images/data-table-light.png),
[charts part two](docs/images/charts-two-light.png), [brush chart](docs/images/brush-chart-light.png),
[advanced actions](docs/images/advanced-actions-light.png),
[text effects](docs/images/text-effects-light.png), [gestures](docs/images/gestures-light.png),
[media layout](docs/images/media-layout-light.png), [pickers](docs/images/pickers-light.png),
[colour picker](docs/images/colour-light.png), [files and ink](docs/images/files-and-ink-light.png),
[foundations dark](docs/images/foundations-dark.png),
[emoji picker](docs/images/emoji-picker-light.png), [avatar upload](docs/images/avatar-upload-light.png),
[presence](docs/images/presence-light.png), [advanced charts part two](docs/images/advanced-charts-two-light.png),
[agent timeline](docs/images/round4-agent-timeline.png), [kanban board](docs/images/round4-kanban-board.png).

## What is inside

| Project | Contents |
| --- | --- |
| `src/Arc.Avalonia` | Foundation: OKLCH colour engine, palette/theme manager, typography, motion, 161 Lucide icons, `ArcButton`, `ArcCard`, `ArcBadge`, `ArcIcon`, `ArcSpinner`, `ArcVirtualList` |
| `src/Arc.Avalonia.Demo` | `ArcPage` contract, `DemoUi` layout helpers, reflection page discovery |
| `src/Arc.Avalonia.AgentUI` | P0 Agent UI: agent transcript, run control bar, tool approval sheet, question card, background dock, attention inbox |
| `src/Arc.Avalonia.LifeToday` | P0 Today: reminder row and center, daily brief, day agenda, medication schedule, quick capture, routine schedule editor |
| `src/Arc.Avalonia.Inputs` | Text fields, special inputs, selects, toggles, sliders, plus inline edit, scrub/phone/mention/shortcut input, expanding search, morph select, elastic + histogram sliders, billing toggle, radio cards |
| `src/Arc.Avalonia.Actions` | Buttons, menus, navigation, disclosure, overlays, messages, plus action button, floating/expanding button groups, icon morph, user menu |
| `src/Arc.Avalonia.Feedback` | Feedback, progress, avatars, cards, tables, activity |
| `src/Arc.Avalonia.Charts` | Custom-drawn data visualisation on the Arc series palette |
| `src/Arc.Avalonia.Pickers` | Calendar, date/time range, colour, file dropzone, signature |
| `src/Arc.Avalonia.Effects` | Text reveal/in-view/morph/shimmer, announcement bar, cookie consent, hover card, bottom sheet, expandable card |
| `src/Arc.Avalonia.Content` | Markdown rich text editor, reorderable list, chat thread, onboarding checklist |
| `src/Arc.Avalonia.Media` | Hold to confirm, swipe actions, reactions, image compare, carousel, card stack, scroll area, resizable panels |
| `src/Arc.Avalonia.Special*` | 55 Special components in five projects: glass and surfaces, motion and interaction, media and input, AI widgets, AI explainers |
| `src/Arc.Avalonia.Blocks*` | 111 page-level blocks in six projects: marketing, commerce, app shell, team and collaboration, data and charts, AI |
| `src/Arc.Avalonia.Gallery` | The showcase application and offscreen screenshot harness |

The gallery discovers every `ArcPage` in the referenced assemblies, so each project ships its own showcase
pages without a central registry.

## Design tokens

Every token from Arc's `foundation.css` is defined in OKLCH in
[`ArcPalette.cs`](src/Arc.Avalonia/Theme/ArcPalette.cs) and converted to sRGB by the small
[`ArcColor.cs`](src/Arc.Avalonia/Theme/ArcColor.cs) engine (OKLab maths, CSS `color-mix(in oklab)`).

* Light and dark palettes, neutral plus eight accents (neutral, violet, blue, green, amber, orange, coral,
  rose), accent-tuned control colours.
* Semantic roles only: `ArcBackground`, `ArcSurface`, `ArcSurfaceMuted`, `ArcForeground`, `ArcTextSecondary`,
  `ArcBorder`, `ArcAccent`, `ArcSuccess`, `ArcDanger`, `ArcControlOn`, `ArcSeries1..4`, …
* Radii 18 / 26 / 34 / pill with concentric nesting; control heights 36 / 44 / 50; 4 px spacing grid;
  type scale 12…72 with 400/500 weights; floating-only shadows.
* Motion presets: `SpringSmooth` (0.40 s, no overshoot), `SpringSnappy` (0.26 s), `SpringMorph` (0.42 s),
  plus 120/160/240/480 ms tweens — `ArcSpringEasing` samples a damped oscillator, `ArcThemeManager.ReducedMotion`
  removes travel for reduced-motion branches.
* Lucide icons at stroke 1.75 on the 24 unit grid, embedded in `ArcIconData` (161 names).

Theming at runtime:

```csharp
ArcThemeManager.Apply(Application.Current!, ArcColorScheme.Dark, ArcAccent.Violet);
```

Every component resolves colours through `DynamicResource`, so a scheme or accent swap restyles the whole
tree without rebuilding it.

## Controls

The port covers actions, inputs, selects, toggles, sliders, pickers, navigation, disclosure, overlays,
feedback, progress, avatars, cards, tables, activity and charts. The full list, grouped like the Arc
documentation, is in [`docs/03-implementation.zh.md`](docs/03-implementation.zh.md).

Highlights:

* **Buttons** — primary/secondary/ghost/danger at three sizes, loading spinner, press spring, keyboard-only
  focus ring, in-place label states.
* **Inputs** — labelled fields with helper/error states, password reveal, password strength, search with
  shortcut hint, number, money, tag entry.
* **Selects** — select, searchable combobox, multi-select with chips, chip group, segmented control.
* **Overlays** — dialog, drawer, popover, tooltip, dropdown/context menus, toasts.
* **Data** — line/bar/donut/sparkline/gauge/waffle/slope/treemap/ridgeline/streamgraph/heatmap/split bar
  charts drawn on the Arc series palette; sortable table, tree view, JSON viewer, code block.
* **Pickers** — calendar with range highlight, date and range pickers with presets, time picker, colour
  picker with contrast readout, drag-and-drop file zone, signature pad with PNG export.
* **Special components** — glass menu/tab bar/card, holo and wallet stacks, sheet stacks, dock, liquid tab
  bar, orbit menu, action morph, booking pill, keycap keyboard, product/lightbox galleries, voice orb,
  now playing, page curl, agent timeline, context window, ink fluid, activity rings, data/payment flows,
  failover stage, vector search, MoE, speculative decoding, prompt cache, reasoning tree, time dial and
  date reel (55 items across five projects).
* **Blocks** — 111 working page compositions: marketing site, commerce and checkout, app shell and settings,
  team collaboration, data dashboards/explorers, and AI product surfaces (composer, research run, agents,
  MCP servers, knowledge base, eval suite).

## Run the gallery

```powershell
# build everything (single node; this machine needs -m:1)
pwsh -NoProfile tools/build.ps1

# launch the interactive gallery (use the dotnet host: direct .exe launch is blocked in this sandbox)
dotnet src/Arc.Avalonia.Gallery/bin/Debug/net10.0/Arc.Avalonia.Gallery.dll

# navigation regression check: clicks the real sidebar and shows every page, exits non-zero on failure
dotnet src/Arc.Avalonia.Gallery/bin/Debug/net10.0/Arc.Avalonia.Gallery.dll --smoke
```

## Screenshot harness

Design review happens on rendered PNGs (light and dark), not on a live desktop:

```powershell
pwsh -NoProfile tools/shots.ps1                      # every page, both themes -> shots/light and shots/dark
pwsh -NoProfile tools/shots.ps1 -Page inputs         # one page
pwsh -NoProfile tools/shots.ps1 -Theme dark
```

`ShotRunner` builds each page in a real (offscreen) window so styles and resources resolve, then renders it
with Skia at its natural height. Pages are capped at 1400 px, so showcase pages are split instead of scrolling.
The catalog currently renders **219 pages / 438 screenshots** (light and dark); `--audit` verifies that every
catalog entry (294 items) is reachable, and item pages share one documentation layout.

## Notes on fidelity

* **Fonts** — Arc uses Geist for display and Inter for body. Geist is not available offline, so the port
  uses the bundled Inter for both and documents the substitution.
* **Platform primitives** — Radix and Motion have no direct equivalents; menus, popovers, dialogs and
  toasts use Avalonia `Popup`/`Flyout`/overlay surfaces, and springs are reimplemented as sampled damped
  oscillators with the same intent (state changes never overshoot).
* **Pro, Special and Blocks** — all 13 Pro components, 55 Special components and 111 blocks are
  implemented independently from their public documentation and visual descriptions. No Pro source was
  fetched or copied; no commercial UI library is used. Special components approximate the original CSS 3D,
  backdrop-filter, WebGL and fluid effects with layered 2D drawing, translucent materials, springs and
  timers; blocks compose the 128 components into working product surfaces with deterministic sample data.
  Each item page records its specific approximations under "Implementation notes".
* **Scope** — all 294 catalog entries are covered (128 components + 55 Special components + 111 blocks),
  rendered as 219 gallery pages and 438 light/dark screenshots. The goal is the design system and the
  component/block set, not a 1:1 API clone.

## Dependencies

No commercial or third-party UI/component library is used. The only NuGet packages across the solution are
**Avalonia**, **Avalonia.Desktop**, **Avalonia.Themes.Fluent** and **Avalonia.Fonts.Inter** (all MIT); every
control, chart and surface in this repository is our own XAML/C#. Pro components are implemented independently
from Arc's public documentation and visual descriptions, without using or copying Pro source code.

## Attribution

* Design system, tokens, component behaviour and copy are derived from **Arc UI** (https://uiarc.dev),
  MIT licensed for free items. This repository is an independent Avalonia port for study and internal use;
  Pro components are re-implemented from public documentation without using Pro source code.
* Icons are from **Lucide** (https://lucide.dev), ISC licensed, embedded as path data on the 24 unit grid.
* Fonts: **Inter** (SIL Open Font License) via `Avalonia.Fonts.Inter`.










