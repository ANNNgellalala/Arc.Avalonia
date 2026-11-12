# Swipe actions danger-colour leak verification

Generated: 2026-11-10 21:59:45 +08:00

Page: `swipe-actions` (documentation page in `src/Docs/Arc.Avalonia.Docs`)

Verdict: **PASS**

## Assertion

The last demo row of the `swipe-actions` page is at rest (closed) while its hidden trailing actions are `Archive` (neutral) and `Delete` (`ArcSwipeActionTone.Danger`). `ArcSwipeActions` hides both action hosts while the row is closed, so this row must not paint any danger colour on its surface, including on the antialiased 1 px border and the rounded corners where a leak would show first.

The scan targets the outer 6 px band of the row rectangle (all four edges; the rounded corners are inside it) with the predicate `r >= 60 && r - g >= 25 && r - b >= 25 (outer 6 px band)`. The opened trailing row above it is the positive control: its right-hand 168 px must contain more than 1000 pixels of the red Delete fill (`r >= 150 && r - g >= 60 && r - b >= 55 (danger fill)`), which proves the scan can see ArcDanger in the rendered theme at all.

### Why the outer band and not the whole row rectangle

The offscreen Skia renderer draws text with LCD subpixel antialiasing. Warm fringe pixels on the outside of glyphs (observed examples: `#D0A019` = 208,160,25; `#BB7719` = 187,119,25; `#A01919` = 160,25,25; `#F2D0A0` = 242,208,160) satisfy any red predicate, so scanning the full row rectangle reports ~50-180 false "danger" pixels on the title and time label of every row, including rows that carry no danger action at all. Text sits at least one row padding (16 px) inside the row surface, while a colour leak can only appear on the 1-3 px border/corner ring, so the outer band isolates the regression target. Whole-row counts are kept as diagnostics only (r >= 120 and r - g >= 50 and r - b >= 50).

## Commands

```powershell
pwsh -NoProfile tools/build.ps1 -Project src/Apps/Arc.Avalonia.Gallery/Arc.Avalonia.Gallery.csproj
pwsh -NoProfile tools/shots.ps1 -Page swipe-actions -Theme both -Width 1180 -SkipBuild
```

Per-theme offset slice (tools/shots.ps1 only captures the top 1400 px; the demo canvas starts near page y=900):

```powershell
dotnet D:\Code\VibeCoding\Arc.Avalonia\src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --shot D:\Code\VibeCoding\Arc.Avalonia\docs\analysis\swipe-leak\render --page swipe-actions --theme dark --width 1180 --offset 300 --timeout 240
dotnet D:\Code\VibeCoding\Arc.Avalonia\src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --shot D:\Code\VibeCoding\Arc.Avalonia\docs\analysis\swipe-leak\render --page swipe-actions --theme light --width 1180 --offset 300 --timeout 240
```

## Results

| theme | assertion band rect (page px) | assertion band danger px | sanity row 0 band danger px | control danger fill px | verdict |
| --- | --- | --- | --- | --- | --- |
| dark | x=77, y=1492, w=646, h=75 | 0 | 0 | 5251 | PASS |
| light | x=77, y=1492, w=646, h=75 | 0 | 0 | 5246 | PASS |

### dark

- slice: `docs/analysis/swipe-leak/render/dark/10012-swipe-actions-o300.png` (1180x1400)
- annotated: `docs/analysis/swipe-leak/swipe-leak-dark-annotated.png`
- canvas: x=58..913, y=813..1299 (page y=1113..1599), surface #191919

| row | state | closed | rect (slice px) | band danger px | band danger bbox (slice px) | full-row diagnostics px | control fill px | asserted | expected | result |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | closed neutral row | True | x=77, y=821, w=646, h=75 | 0 | none | 98 | n/a | True | sanity-ring-zero | ok |
| 1 | leading-open row (accent action) | False | x=77, y=968, w=646, h=75 | 0 | none | 50 | n/a | False | diagnostic-ring | ok |
| 2 | trailing-open row (Archive + danger Delete) | False | x=77, y=1080, w=646, h=75 | 352 | x=635, y=1084, w=84, h=67 | 5285 | 5251 | True | control-fill | ok |
| 3 | closed row with hidden danger actions (ASSERTION) | True | x=77, y=1192, w=646, h=75 | 0 | none | 62 | n/a | True | assertion-ring-zero | ok |

### light

- slice: `docs/analysis/swipe-leak/render/light/10012-swipe-actions-o300.png` (1180x1400)
- annotated: `docs/analysis/swipe-leak/swipe-leak-light-annotated.png`
- canvas: x=58..913, y=812..1300 (page y=1112..1600), surface #FFFFFF

| row | state | closed | rect (slice px) | band danger px | band danger bbox (slice px) | full-row diagnostics px | control fill px | asserted | expected | result |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | closed neutral row | True | x=77, y=821, w=646, h=75 | 0 | none | 131 | n/a | True | sanity-ring-zero | ok |
| 1 | leading-open row (accent action) | False | x=77, y=968, w=646, h=75 | 0 | none | 147 | n/a | False | diagnostic-ring | ok |
| 2 | trailing-open row (Archive + danger Delete) | False | x=77, y=1080, w=646, h=75 | 356 | x=635, y=1084, w=84, h=67 | 5276 | 5246 | True | control-fill | ok |
| 3 | closed row with hidden danger actions (ASSERTION) | True | x=77, y=1192, w=646, h=75 | 0 | none | 181 | n/a | True | assertion-ring-zero | ok |

## Evidence

- slice: `docs/analysis/swipe-leak/render/dark/10012-swipe-actions-o300.png`
- annotated: `docs/analysis/swipe-leak/swipe-leak-dark-annotated.png`
- slice: `docs/analysis/swipe-leak/render/light/10012-swipe-actions-o300.png`
- annotated: `docs/analysis/swipe-leak/swipe-leak-light-annotated.png`
- gallery screenshot: `shots/dark/10012-swipe-actions.png`
- gallery screenshot: `shots/light/10012-swipe-actions.png`

## Conclusion

In both themes the closed assertion row reports 0 danger pixels in its outer band, while the opened control row reports danger pixels in the corresponding band (dark=352, light=356) because its visible Delete fill reaches the row edge, and more than 1000 danger-fill pixels in its right-hand action stack. The band predicate therefore fires when danger colour sits on a row edge and stays at 0 on the resting row. ArcSwipeActions itself does not leak the hidden action colour; the DSHRemote symptom comes from a host surface that paints its hidden (or underlay) action background through an antialiased, semi-transparent rounded edge, not from this control.
