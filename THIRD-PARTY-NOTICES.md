# Third-party notices

Arc for Avalonia ships only Avalonia's official packages as NuGet dependencies; every control,
theme, chart and surface in this repository is our own XAML/C#. The items below are the third-party
works whose code, assets, fonts or design language reach consumers of these packages.

## Runtime dependencies (NuGet)

| Package | License | Role |
|---|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) (`Avalonia`, `Avalonia.Base`, `Avalonia.Controls`, `Avalonia.Desktop`, `Avalonia.Skia`, `Avalonia.Win32`, `Avalonia.X11`, `Avalonia.Native`) | MIT | UI framework, rendering, layout, input, windowing |
| [Avalonia.Themes.Fluent](https://github.com/AvaloniaUI/Avalonia) | MIT | Official Fluent baseline that `ArcTheme` adapts on top of |
| [Avalonia.Fonts.Inter](https://github.com/AvaloniaUI/Avalonia) | MIT | Inter font package used as the Arc body font |
| [PuraMarkdown.Core](https://github.com/) (local package `0.1.0-alpha.1`) | MIT | Markdown parsing, Markdig AST facade, diagnostics/outline, stable-boundary streaming scanner for `Arc.Avalonia.Markdown` (ADR 0006). No Avalonia dependency and no UI code. |
| [Markdig](https://github.com/xoofx/markdig) (transitive via `PuraMarkdown.Core`) | BSD-2-Clause | CommonMark/GFM parser used by the Markdown syntax layer |
| [SkiaSharp](https://github.com/mono/SkiaSharp) (transitive via `Avalonia.Skia`) | MIT | 2D rendering backend |
| [HarfBuzzSharp](https://github.com/mono/SkiaSharp) (transitive via `Avalonia.Skia`) | MIT | Text shaping backend |

No other third-party NuGet package is referenced by the publishable libraries. The dependency
whitelist is enforced by `tools/check-package.ps1` and `tools/check-deps.ps1`. The only two
non-Avalonia runtime packages are the Markdown syntax layer and its Markdig parser, and both are
confined to `Arc.Avalonia.Markdown` (ADR 0006); no third-party UI component library is used.

## Fonts

* **Inter** — SIL Open Font License 1.1, Copyright (c) 2016-2026 The Inter Project Authors
  (https://rsms.me/inter/), delivered through `Avalonia.Fonts.Inter`.
* Arc uses **Geist** for display type in the reference design; Geist is not vendored here, so the port
  uses Inter for both display and body (documented in `README.md`).

## Icons

* **Lucide** (https://lucide.dev) — ISC License, Copyright (c) 2022 Lucide Contributors.
  The 161 icon paths embedded in `Arc.Avalonia.Foundation` are derived from the public Lucide icon set
  (24 unit grid, stroke 1.75).

## Design language and documentation

* **Arc UI** (https://uiarc.dev) — MIT License for free components, blocks and documentation.
  The Arc design system (tokens, component behaviour, copy) is derived from Arc's public documentation.
  **Pro** components were re-implemented independently from their public descriptions and visual
  documentation; no Pro source code, registry payload (`/r/pro/**`) or paid asset was fetched or copied.
* **AG-UI** (https://github.com/ag-ui-protocol/ag-ui) — referenced only as a semantic model for agent
  event naming in design documents. No AG-UI SDK or code is referenced by any package.

## Tooling used at build time only (not shipped)

* .NET SDK and MSBuild (MIT), NuGet (Apache-2.0), and `System.IO.Compression` for the package gate.

---

If you believe a work is missing from this list, please open an issue before publishing a release.

