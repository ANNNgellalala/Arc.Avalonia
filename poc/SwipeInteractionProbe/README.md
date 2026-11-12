# SwipeInteractionProbe

Headless behaviour probe for the tappable `ArcSwipeActions` row (task-3). It answers one question with
real routed pointer events: does a tap reach the row content while a horizontal drag still reveals the
trailing actions, and does an already open row swallow a tap instead of activating the content?

The probe is an offline POC project. It is deliberately **not** part of `ArcUI.slnx`.

## What it covers

| case | gesture | assertion |
| --- | --- | --- |
| T1 | tap on a pure-content row | `row.Tapped == 1`, `row.IsOpen == false` |
| T2 | horizontal drag `dx = -120` on a pure-content row | `row.IsOpen == true`, `row.Tapped == 0` |
| T3 | tap on a row whose `Content` is a `Button` | `Button.Click == 1`, `row.IsOpen == false` |
| T4 | horizontal drag starting on that `Button` | `row.IsOpen == true`, `Button.Click == 0` |
| T5 | tap on an already open row | row closes (`IsOpen == false`), `Button.Click == 0` |
| T6 | vertical drag `dy = 80` | `row.IsOpen == false` (the row must not hijack scrolling) |
| T7 | exploratory: row wrapped in an outer `Button` | observations only, no pass/fail |

T1-T6 drive the exit code. T7 prints `T7 INFO` lines; the conclusion is in
`docs/analysis/swipe-leak/interaction-probe.md`.

## Build and run (offline)

The repository pins Avalonia **12.1.4**, but the local NuGet cache only carries `Avalonia.Headless` for
**12.1.5**. `SwipeInteractionProbe.csproj` therefore sets `<AvaloniaVersion>12.1.5</AvaloniaVersion>`
and references Avalonia / Avalonia.Headless / Avalonia.Themes.Fluent / Avalonia.Fonts.Inter /
Avalonia.HarfBuzz explicitly (`poc/Directory.Packages.props` already opts POC projects out of central
package management). The referenced Arc assemblies are still built against 12.1.4; the 12.1.x patch
lines are binary compatible and the probe resolves them against the 12.1.5 runtime.

```powershell
# build (single node: the machine's parallel MSBuild restore is unstable)
dotnet build poc/SwipeInteractionProbe/SwipeInteractionProbe.csproj -m:1

# run the managed dll through the dotnet host; never launch the unsigned exe directly
dotnet poc/SwipeInteractionProbe/bin/Debug/net10.0/SwipeInteractionProbe.dll
```

Exit codes: `0` = T1-T6 all matched, `1` = at least one mandatory case failed, `2` = probe crash.

Capture the raw output for the evidence document with:

```powershell
dotnet poc/SwipeInteractionProbe/bin/Debug/net10.0/SwipeInteractionProbe.dll |
    Tee-Object docs/analysis/swipe-leak/interaction-probe-output.txt
```

## How the harness works

* `AppBuilder.Configure<ProbeApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })`
  then `SetupWithoutStarting()`: no desktop window is created, but the real Avalonia input pipeline,
  routed events, pointer capture and control templates are used.
* `ProbeTheme` installs a Fluent baseline (for the plain `Button` content) plus the Arc Foundation base
  styles and the Media theme family (for the `ArcSwipeActions` `ControlTheme`). It mirrors the aggregate
  `ArcTheme` load order but skips the Markdown family, which would pull the local PuraMarkdown feed.
* `ArcThemeManager.Apply(app, ArcColorScheme.Dark, ArcAccent.Violet)` installs the token dictionaries.
* `ArcThemeManager.ReducedMotion = true` makes the row tween apply `Open`/`Close` immediately, so every
  assertion is synchronous and deterministic (no 400 ms animation pump).
* Pointer input is injected with `window.MouseDown/MouseMove/MouseUp` from `Avalonia.Headless`, then
  `Dispatcher.UIThread.RunJobs()` + `AvaloniaHeadlessPlatform.ForceRenderTimerTick(1)` pump the queue.

## Gotchas found while building this probe

* **A drag move must carry `RawInputModifiers.LeftMouseButton`.** The headless platform derives
  `PointerPointProperties.IsLeftButtonPressed` from the raw modifiers, not from an internal button
  state. `MouseMove(point, RawInputModifiers.None)` makes every handler see the button as released, so
  `ArcSwipeActions` correctly aborts the gesture and the drag never starts (the first version of this
  probe hit exactly that and reported T2/T4 as failures).
* A tap gesture (`Tapped`) **does** fire under headless input; T1 reaches `row.Tapped` normally, no
  fallback to pointer-only assertions was needed.
* Window coordinates are computed with `TranslatePoint`; `CenterOf` in `Program.cs` maps a control
  centre into window space, so the probe works at any window size.
