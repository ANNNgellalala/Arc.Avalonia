# Third-party notices

Arc for Avalonia ships Avalonia's official packages as its baseline; every control, theme, chart and
surface in this repository is our own XAML/C# **except** the Agent/Chat rendering surface, which
delegates to `PuraMarkdown.Agent` under ADR 0007's scoped exception. The items below are the
third-party works whose code, assets, fonts or design language reach consumers of these packages.

## Runtime dependencies (NuGet)

| Package | License | Role |
|---|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) (`Avalonia`, `Avalonia.Base`, `Avalonia.Controls`, `Avalonia.Desktop`, `Avalonia.Skia`, `Avalonia.Win32`, `Avalonia.X11`, `Avalonia.Native`) | MIT | UI framework, rendering, layout, input, windowing |
| [Avalonia.Themes.Fluent](https://github.com/AvaloniaUI/Avalonia) | MIT | Official Fluent baseline that `ArcTheme` adapts on top of |
| [Avalonia.Fonts.Inter](https://github.com/AvaloniaUI/Avalonia) | MIT | Inter font package used as the Arc body font |
| [PuraMarkdown.Core](https://github.com/) (local package `0.1.0-alpha.1`) | MIT | Markdown parsing, Markdig AST facade, diagnostics/outline, stable-boundary streaming scanner for `Arc.Avalonia.Markdown` (ADR 0006). No Avalonia dependency and no UI code. |
| [PuraMarkdown.Agent](https://github.com/) (local package `0.1.0-alpha.1`) | MIT | Avalonia rendering controls + Chat + Agent components + Testing. **Scoped exception (ADR 0007)**: only `Arc.Avalonia.Chat` / `Arc.Avalonia.Agent` may reference it; `Arc.Avalonia.Markdown` and other packages may not. |
| `Avalonia.Controls.ItemsRepeater` (`12.0.0`, transitive via `PuraMarkdown.Agent`) | MIT | Items repeater/virtualization primitive used by the upstream Chat/Agent rendering controls |
| `JsonSchema.Net` (`9.6.0`, transitive via `PuraMarkdown.Agent`) | MIT | JSON Schema validation for upstream `pura-agent` fence payloads; pulls `JsonPointer.Net` → `Json.More.Net` and `Humanizer.Core` |
| [Markdig](https://github.com/xoofx/markdig) (transitive via `PuraMarkdown.Core`) | BSD-2-Clause | CommonMark/GFM parser used by the Markdown syntax layer |
| [SkiaSharp](https://github.com/mono/SkiaSharp) (transitive via `Avalonia.Skia`) | MIT | 2D rendering backend |
| [HarfBuzzSharp](https://github.com/mono/SkiaSharp) (transitive via `Avalonia.Skia`) | MIT | Text shaping backend |

No other third-party NuGet package is referenced by the publishable libraries. The dependency
whitelist is enforced by `tools/check-package.ps1` and `tools/check-deps.ps1`: `PuraMarkdown.Core`
and `Markdig` are confined to `Arc.Avalonia.Markdown` (ADR 0006), while `PuraMarkdown.Agent` and its
transitive chain are confined to `Arc.Avalonia.Chat` / `Arc.Avalonia.Agent` (ADR 0007). No other
third-party UI component library is used. The Agent/Chat scoped exception was requested by the user
and is auditable in `tools/deps-policy.json` (`agentChatBridge`) and `tools/check-package.ps1`.

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

