# Arc for Avalonia: build conventions

The source of truth is the React library at **https://uiarc.dev** (per-component markdown lives at
`https://uiarc.dev/components/<name>/markdown`, design rules in `https://uiarc.dev/r/skills/arc/*.md`).
We are porting its design system to Avalonia 12 / .NET 10. Fidelity of *design* (tokens, spacing, radii,
type, colour, motion intent) matters more than API-name parity.

## 1. Layout and ownership

| Path | Owner | Contents |
| --- | --- | --- |
| `src/Foundation/Arc.Avalonia` | Lead | tokens/palette/theme manager, motion, icons, `ArcButton`, `ArcCard`, `ArcBadge`, `ArcIcon`, `ArcSpinner`, `ArcVirtualList`, typography styles |
| `src/Foundation/Arc.Avalonia.Internal` | Lead | shared internal primitives: menu surface/glide, icon button, feedback composition helpers, action models; `InternalsVisibleTo` the consuming projects |
| `src/Apps/Arc.Avalonia.Demo` | Lead | `ArcPage` contract, `DemoUi` layout helpers, page discovery |
| `src/Agent/Arc.Avalonia.AgentUI` | agent-ui agent | P0 agent transcript / run bar / approval sheet / question card / background dock / attention inbox |
| `src/Agent/Arc.Avalonia.LifeToday` | life-today agent | P0 reminder row/center, daily brief, day agenda, medication schedule, quick capture, routine editor |
| `src/Apps/Arc.Avalonia.Gallery` | Lead | shell window, shot harness, foundations page |
| `src/Components/Arc.Avalonia.Inputs` | input agent | text fields, selects, toggles, sliders, inline edit, scrub/phone/mention/shortcut input, expanding search, morph select, elastic + histogram sliders, billing toggle, radio cards |
| `src/Components/Arc.Avalonia.Actions` | action agent | buttons/menus, disclosure, messages, action button, floating/expanding button groups, icon morph, user menu |
| `src/Components/Arc.Avalonia.Navigation` | navigation agent | tabs, breadcrumb, pagination, mega menu |
| `src/Components/Arc.Avalonia.Overlays` | overlay agent | dialog, drawer, popover, tooltip, share access |
| `src/Components/Arc.Avalonia.Feedback` | feedback agent | feedback/progress, avatars/cards, presence, activity |
| `src/Components/Arc.Avalonia.DataDisplay` | data agent | sortable table, tree table, tree view, filter toolbar, code block, JSON viewer, data grid |
| `src/Components/Arc.Avalonia.Charts` | chart agent | data visualisation controls |
| `src/Components/Arc.Avalonia.Pickers` | picker agent | calendar/date/time/colour, file dropzone, signature |
| `src/Components/Arc.Avalonia.Effects` | effects agent | text reveal/in-view/morph/shimmer, announcement bar, cookie consent, hover card, bottom sheet, expandable card |
| `src/Components/Arc.Avalonia.Content` | content agent | rich text editor, reorderable list, chat thread, onboarding checklist |
| `src/Components/Arc.Avalonia.Media` | media agent | hold to confirm, swipe actions, reactions, image compare, carousel, card stack, scroll area, resizable panels |

**Never edit files outside your project** (except adding your `ResourceInclude` lines inside your own
project theme file). In particular do not touch `Directory.Build.*`, `ArcUI.slnx`, `src/Foundation/Arc.Avalonia/**`,
`src/Apps/Arc.Avalonia.Demo/**`, `src/Apps/Arc.Avalonia.Gallery/**`, `tools/**` or `docs/**`. If a change to the
foundation is genuinely needed, message the Lead with the exact file and reason.

## 2. Build and verify

Everything is offline (NuGet packages come from a local global-packages cache). Two quirks matter:

* Build **single-node** with `-m:1`. Parallel MSBuild restore is unstable on this machine.
* Never launch the built `.exe` directly (the sandbox blocks unsigned executables). Run the `.dll`
  through `dotnet`, which `tools/shots.ps1` does for you.

```powershell
# compile your project (serialised behind a workspace-wide build lock)
pwsh -NoProfile tools/build.ps1 -Project src/Components/Arc.Avalonia.Inputs/Arc.Avalonia.Inputs.csproj

# render screenshots of every page in light and dark (builds the gallery first)
pwsh -NoProfile tools/shots.ps1
# only your page / only one theme
pwsh -NoProfile tools/shots.ps1 -Page input -Theme both
# shell screenshot (the whole gallery window)
pwsh -NoProfile tools/shots.ps1 -Shell
```

Screenshots land in `shots/light/<order>-<slug>.png` and `shots/dark/...`. Read them with the image
tooling and iterate until both themes look correct. Pages are capped at 1400 px (screen work area): split a
long showcase page into numbered sibling pages instead of scrolling.

**Definition of done** for every component: project builds with 0 errors; the component appears on its
page; the page renders in light and dark; no raw hex/rgb in component code; keyboard focus and Enter/Space
work where the Arc component supports them; only documented props exist (do not invent APIs); Pro-only
components are not recreated.

## 3. Adding a component

1. `src/<project>/Controls/ArcXxx.cs`: a `TemplatedControl`/`ContentControl`/`Control` subclass. Variants
   as enums mapped to CSS-like classes in the constructor and in property-change handlers, exactly like
   `ArcButton` (`Classes.Set("primary", Variant == ArcButtonVariant.Primary)`).
2. `src/<project>/Themes/ArcXxx.axaml`: a `ResourceDictionary` holding the `ControlTheme`
   (`x:Key="{x:Type controls:ArcXxx}"`). Style the template with `{DynamicResource ...}` tokens only.
   Nested variant styles use `^.primary`, `^:pressed`, `^:focus-visible`, `/template/ ...`.
3. Merge the theme file in your project's single theme entry (`Themes/ArcInputsTheme.axaml` etc.):

   ```xml
   <ResourceDictionary.MergedDictionaries>
     <ResourceInclude Source="avares://Arc.Avalonia.Inputs/Themes/ArcInput.axaml" />
   </ResourceDictionary.MergedDictionaries>
   ```

4. `src/<project>/Pages/XxxPage.cs`: a public sealed `ArcPage` subclass. The gallery discovers pages by
   reflection, so **no registration is needed**. One page per project area (not per component) is enough;
   use `DemoUi.Section` for each component with a one-line "when to use" description.

```csharp
public sealed class InputsPage : ArcPage
{
    public override string Title => "Inputs";          // sidebar label, also screenshot slug
    public override string Group => "Inputs";          // sidebar section
    public override string? Description => "Text entry ...";
    public override int Order => 100;                  // Inputs 100s, Actions 200s, Feedback 300s, Charts 400s, Pickers 500s
    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section("Input", "Single line field ...", new ArcInput { Placeholder = "you@acme.com" })
        }
    };
}
```

Pages are rebuilt on theme change, so resolving a brush once in `Build` with `DemoUi.Brush(key)` is fine.
Live controls still use DynamicResource in their templates and react to theme swaps by themselves.

## 4. Design tokens: use these keys, nothing else

`using Arc.Avalonia.Theme;` then `{DynamicResource ArcBackground}` in XAML or
`ArcResources.Brush(ArcTokens.Background)` from code. Keys are constants on `ArcTokens`.

| Family | Keys |
| --- | --- |
| Surfaces | `ArcBackground`, `ArcSurface`, `ArcSurfaceRaised`, `ArcSurfaceMuted` |
| Text | `ArcForeground`, `ArcTextSecondary`, `ArcTextMuted` |
| Lines | `ArcBorder`, `ArcBorderSubtle`, `ArcBorderStrong` |
| Accent | `ArcAccent`, `ArcAccentStrong`, `ArcAccentSubtle`, `ArcAccentForeground` |
| Status | `ArcSuccess`, `ArcWarning`, `ArcDanger` (+ `…Subtle` tints) |
| Selection | `ArcControlOn`, `ArcControlGlyph`, `ArcControlTrack`, `ArcControlTrackHover`, `ArcControlThumb`, `ArcControlThumbOn`, `ArcControlThumbShadow`, `ArcControlOnSubtle`, `ArcControlFill` |
| Elevation | `ArcShadowResting`, `ArcShadowRaised`, `ArcShadowFloating` (BoxShadows) |
| Data | `ArcSeries1`…`ArcSeries4` |
| Brand wash | `ArcGradientFrom`, `ArcGradientTo`, `ArcGradientSoft`, `ArcGradientInk`, `ArcGradientForeground` |
| Radii | `ArcRadiusControl` (18), `ArcRadiusPanel` (26), `ArcRadiusSurface` (34), `ArcRadiusPill` |
| Sizing | `ArcControlHeightSm/Md/Lg` (36/44/50), `ArcSpace1/2/3/4/5/6/8/10/12/16/20/24` |
| Type | `ArcFontDisplay`, `ArcFontBody`, `ArcTextXs…ArcText5xl`, `ArcTrackingDisplay/Body`, `ArcLeadingDisplay/Body` |
| Motion | `ArcDurationInstant/Fast/Standard/Considered` (TimeSpans) |

Rules (from Arc's design skill):

* **Semantic roles only.** No hex, no `Brushes.*` for anything user-facing; status colours never decorate.
* **Radii are concentric.** Inner radius = outer radius − padding, clamped to ≥6. Control 18, panel 26,
  surface 34, pill 9999. Cards nest a panel (34 − 8 = 26) inside, never 34 inside 34.
* **Type**: 400/500 only, never bold. Sizes 12/14/16/18/22/28/36/52/72. Use the `arc-*` TextBlock classes
  (`arc-heading`, `arc-label`, `arc-body-sm`, `arc-caption`, `arc-secondary`…). Sentence case, no eyebrow
  labels above headings, no em dashes.
* **Spacing** on the 4 px grid. Product UI: 16 inside groups, 24 between regions, 24–32 page padding.
* **Shadows** only on floating layers (menus, popovers, dialogs, sheets). Cards use a 1 px `ArcBorder`.
* **Accent with intent**: active nav item, selected row/option, one important series, progress, links.
* **Icons**: `new ArcIcon { Icon = "chevron-down", Size = 16 }` (Lucide names, stroke 1.75). Icon-only
  buttons need a label or tooltip.
* **Focus**: pointer focus draws nothing; `:focus-visible` gets a 2 px `ArcFocusOutline` ring offset 2 px.
  `ArcButton` already does this; copy the pattern.
* **Motion**: use `ArcMotion.SpringSmooth` (0.4 s, no overshoot) for size/state, `SpringSnappy` (0.26 s)
  for presses, `SpringMorph` (0.42 s) for shared highlights; 120/160/180/240/480 ms tweens for colour and
  opacity; exits faster than entrances; every animation must have a reduced-motion branch
  (`ArcThemeManager.ReducedMotion`). State that reports a value must never overshoot. Use Avalonia
  `Transitions` inside templates for hover/press, and animate transform/opacity rather than layout.

## 5. Foundation API cheat sheet

```csharp
using Arc.Avalonia.Controls;   // ArcButton, ArcCard, ArcBadge, ArcIcon, ArcSpinner
using Arc.Avalonia.Theme;      // tokens, palette, theme manager, ArcResources
using Arc.Avalonia.Motion;     // ArcMotion presets, ArcSpringEasing
using Arc.Avalonia.Demo;       // ArcPage, DemoUi, ArcDemoContext
```

* `ArcButton`: `Variant` (Primary/Secondary/Ghost/Danger), `Size` (Sm/Md/Lg), `Loading`, `Compact`, `Content`.
* `ArcCard`: `Content`, `Interactive`, `Muted`, `Padding`, `CornerRadius` (defaults to surface radius).
* `ArcBadge`: `Content`, `Tone` (Neutral/Accent/Success/Warning/Danger), `Dot`.
* `ArcIcon`: `Icon` (Lucide kebab-case name), `Size` (default 20), `StrokeWidth` (default 1.75),
  `Foreground` (defaults to text-secondary). 161 icons in `Arc.Avalonia.Icons.ArcIconData.Paths`.
* `ArcSpinner`: `Size`, `StrokeWidth`, `Stroke`; renders the shared 270° arc.
* `ArcThemeManager`: `Scheme`, `Accent`, `ReducedMotion`, `Apply(app, scheme, accent)`.
* `DemoUi`: `Section(title, description, content)`, `Row(...)`, `Column(...)`, `Field(label, control, hint)`,
  `Card(title, description, content)`, `Canvas(content)`, `Caption`, `Label`, `Body`, `Brush(key)`.
  Widths: a form field is 280; a demo row wraps with 12 px gaps; section copy wraps at 720.

Overlay layers: for dialogs/drawers/toasts/popovers, add a `Panel` host on the gallery window (there is an
`OverlayLayer` in Avalonia: `OverlayLayer.GetOverlayLayer(window)`), or use Avalonia `Popup`/`Flyout` with
an `ArcSurface` + `ArcShadowFloating` + panel radius. Document which approach you chose in your page.

## 6. Working with the rest of the team

* You share one filesystem with other agents. Only edit your own project's files.
* Builds are serialised by `tools/build.ps1`; a build that fails with a file-lock style error should be
  retried once after a few seconds.
* Keep the page order ranges: Inputs 100–199, Actions 200–299, Feedback 300–399, Charts 400–499,
  Pickers 500–599, Advanced inputs 600–699, Advanced actions 700–799, Effects 800–899,
  Content 900–999, Media 1000–1099.
* When you finish, report: components implemented, files added, pages added, screenshot paths, anything
  deliberately skipped and why.
## 7. Avalonia 12 gotchas the foundation learned

* **ClipToBounds defaults to true.** Any template that hosts an overlay `Canvas` (menus, popovers, tooltips,
  page-hosted dialogs) must set `ClipToBounds="False"` on the template root and the canvas, or children
  outside the control bounds are clipped away without an error.
* **Implicit styles match the style key, not the concrete type.** A control using `StyleKeyOverride`
  (for example an `ArcCopyButton` overriding to `ArcButton`) cannot be targeted by its own type selector,
  and a selector on a base type (`Border.x`) does not match a derived control. Use `type + class` selectors.
* **Overlay scrim**: use `ArcScrim` (a dimming brush tuned per theme) behind dialogs, drawers and sheets;
  do not hard-code black alpha.
* **Danger buttons**: use `ArcDangerForeground` for text on `ArcDanger`; plain white loses contrast on the
  lighter danger tone in dark mode.
* **Content alignment**: `ArcButton` honours `HorizontalContentAlignment` (after the template fix), but a
  custom `ContentPresenter` may still need a `/template/` stretch override for full-width menu rows.
* **Page height**: pages are capped at 1400 px because the offscreen window cannot exceed the screen work
  area. Split a long showcase into numbered sibling pages; the shot output prints a `SHOT WARNING` if a
  page was clipped.
* **Shot runs terminate via a watchdog** (`--timeout`, default 300 s); `tools/shots.ps1` passes 240 s and
  `tools/build.ps1` builds with `-nodeReuse:false` so the PowerShell wrapper returns.
* **Run the scripts with `-NoProfile`.** The machine's PowerShell profile calls `Set-PSReadLineOption`
  with a redirected console, which can leave a nested `pwsh` waiting forever even after the build or
  screenshot child exits. That is what made one background build task show as "running" for hours while
  its work was already finished. Use `pwsh -NoProfile tools/build.ps1` (or `-File`), and remember the
  gallery shot app has its own `--timeout` watchdog.
## 8. Pro components (round 3 policy)

The team may not use external commercial UI component libraries. The port uses only Avalonia (MIT),
Avalonia.Themes.Fluent, Avalonia.Fonts.Inter and SkiaSharp; everything visual is our own XAML/C#.

Arc Pro items **are now in scope**, but implemented independently:

* Use only **public documentation and visual description** (the component page, its public markdown,
  llms.txt one-liners, and the screenshots you can infer from the docs). Do **not** fetch
  `https://uiarc.dev/r/pro/**.json`, do not ask for Pro source, and do not copy any Pro implementation
  code. Match the style, layout, states, motion intent and keyboard behaviour described publicly.
* Note approximations honestly in the class XML docs and in the page description where a platform limit
  matters (no pinch on desktop, no browser eyedropper, no virtualised DOM, etc.).
* Everything else in this document still applies: semantic tokens only, light and dark, all accents,
  reduced-motion branches, keyboard support, page-hosted overlays, pages ≤1400 px.

Page orders for this round (spread across the existing projects, one writer per project):
`Advanced charts` 403–404 (Charts), `Pickers` 505–506 (Pickers), `Navigation` 206 and `Overlays` 207
(Actions), `Avatars` 308 and `Tables` 309 (Feedback).
## 9. Round 4: catalog parity (Special components and Blocks)

The gallery navigation is now **catalog-driven** and mirrors https://uiarc.dev/docs/introduction:
`ArcCatalog` (in `Arc.Avalonia.Demo`) loads `Catalog/arc-catalog.json` (generated from the public
llms-small.txt + the introduction sidebar by `tools/build-catalog.ps1`) and the sidebar renders
Components (23 categories, 128 items), Blocks (6 implementation groups, 111 items) and Special components
(5 groups, 55 items), with an All/Free/Pro tier filter and search.

Every new item gets **one page** built from `ArcItemPage`:

```csharp
public sealed class ShareSheetPage : ArcItemPage
{
    public override string ItemSlug => "share-sheet";
    protected override Control BuildDemo(ArcDemoContext context) { /* the demo */ }
    public override IReadOnlyList<string>? WhenToUse => new[] { "...", "..." };
    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[] { "..." };
    public override IReadOnlyList<string>? MotionNotes => new[] { "..." };
    public override string? ImplementationNotes => "Platform approximations: ...";
    public override bool DemoOnCanvas => false; // blocks usually
}
```

`ArcItemPage` derives Title, Category, tier badge and order from the catalog, and `ArcDocLayout` renders
the same sections on every page (breadcrumb, title + tier + docs link, description, When to use, When not
to use, Preview, Keyboard and accessibility, Motion, Implementation notes, docs footer). Keep that
structure: it is what makes the port easy to navigate for people who know the React docs. The docs URL is
public; **never** fetch `/r/pro/**.json` or copy Pro source.

Rules for item pages:

* One catalog item per page is the default; a page that covers several items must use the exact catalog
  title as its `DemoUi.Section` heading so sidebar deep links resolve.
* Total page height ≤1400 px including the doc chrome (~350 px): keep the demo itself under ~950 px.
* The doc sections are optional; fill the ones the public markdown supports. If a detail is unknown, omit
  it rather than inventing it. Mark simplifications in `ImplementationNotes`.
* Blocks compose existing controls (ArcCard, ArcButton, charts, inputs, tables…). They should read as a
  real product surface at gallery width (~1000 px), not a wireframe; sample data in code is fine.
* Special components are mostly self-drawn/animated illustrations. Use `DrawingContext`, `DispatcherTimer`
  and tokens; no external libraries; deterministic frame under `ArcThemeManager.ReducedMotion`.

New projects: `Arc.Avalonia.Special.Glass`, `.Special.Motion`, `.Special.Media`, `.Special.AIWidgets`,
`.Special.AIExplainers`, `Arc.Avalonia.Blocks.Marketing`, `.Blocks.Commerce`, `.Blocks.App`, `.Blocks.Team`,
`.Blocks.Data`, `.Blocks.AI`. They are already referenced by the Gallery and wired into `App.axaml`; each has
a `Themes/Arc<Name>Theme.axaml` entry and `Controls/`, `Pages/` folders.
Catalog data is generated from public docs only:

```powershell
pwsh -NoProfile -File tools/build-catalog.ps1     # writes docs/arc-catalog.json and validates 55 specials / 111 blocks
Copy-Item docs/arc-catalog.json src/Apps/Arc.Avalonia.Demo/Catalog/arc-catalog.json -Force
```

The Demo project embeds that JSON (`ArcCatalog`), the gallery sidebar renders it, and
`dotnet src/Apps/Arc.Avalonia.Gallery/bin/Debug/net10.0/Arc.Avalonia.Gallery.dll --audit` must report
`missing=0 pageFailures=0`. When adding a page, give it the exact catalog `ItemSlug`; when a component is
demonstrated on a shared page without its own heading, add it to `ArcCatalogAliases` in
`src/Apps/Arc.Avalonia.Demo/ArcCatalogAliases.cs` so the sidebar still reaches it.

Note: `tools/shots.ps1` builds the gallery with `-p:BuildProjectReferences=false`, so a freshly built
component DLL is not copied into the gallery output. Run a normal `tools/build.ps1` / `dotnet build
ArcUI.slnx` first, or copy the project DLL into `src/Apps/Arc.Avalonia.Gallery/bin/Debug/net10.0/`, before
shooting; otherwise screenshots render the previous assembly.
