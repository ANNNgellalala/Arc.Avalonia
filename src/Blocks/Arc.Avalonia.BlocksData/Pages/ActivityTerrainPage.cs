using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksData.Pages;

/// <summary>
/// Activity terrain: a stylised 3D terrain of hourly activity across a week, with a metric switch, an orbit
/// and zoom camera, a heatmap flatten toggle and peak hours that fly to a cell. The grid is seeded sample
/// data. An independent Avalonia implementation of Arc's public activity-terrain block; no Pro source was
/// used.
/// </summary>
public sealed class ActivityTerrainPage : ArcItemPage
{
    public override string ItemSlug => "activity-terrain";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Analytics or operations pages showing when a product is busiest across a week.",
        "Showcase dashboards where a 3D view of a 7 by 24 grid adds impact, with a flat heatmap fallback."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use activity-heatmap for a plain 2D grid that is easier to read."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The stage is one focusable group: arrows move the highlighted hour, Shift with the arrows orbits and scales, plus and minus zoom, Enter flattens, Escape resets, and a text summary names the peak.",
        "Peak hour buttons fly to the hour and announce its value politely; heights and the camera are also readable as text in the stats row."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Columns rise in a wave on load and the camera follows drags; flattening springs to a top view. The shot build renders with reduced motion, so it paints the settled week.",
        "Reduced motion snaps the camera and the heights with no wave."
    };

    public override string? ImplementationNotes =>
        "This is a 2D isometric projection drawn with DrawingContext, not WebGL: there is no perspective divide, no depth buffer and no pinch zoom. Orbit is a lattice rotation driven by Shift with the arrow keys, the camera is fixed otherwise, and flattening swaps the columns for a heatmap.";

    private static readonly string[] Days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var sessions = BuildGrid(28.0, 210.0, 118.0, 31);
        var signups = BuildGrid(1.1, 8.4, 4.2, 32);
        var errors = BuildGrid(0.4, 3.8, -2.1, 33);
        var metrics = new List<(string Key, string Label, double[,] Grid, string Unit, Func<double, string> Format)>
        {
            ("sessions", "Sessions", sessions, "sessions", value => DataKit.Number(value)),
            ("signups", "Signups", signups, "signups", value => DataKit.Number(value, 1)),
            ("errors", "Errors", errors, "errors", value => DataKit.Number(value, 1))
        };

        var activeKey = "sessions";
        var flattened = false;
        var stageHost = new ContentControl();
        var statsHost = new ContentControl();
        var peakHost = new ContentControl();
        var readout = DataKit.Caption(string.Empty);
        var status = DataKit.Status();
        var flattenToggle = new ContentControl();
        ArcTerrainStage? stage = null;

        (string Key, string Label, double[,] Grid, string Unit, Func<double, string> Format) Active()
            => metrics.First(item => item.Key == activeKey);

        void RefreshStats()
        {
            var active = Active();
            var best = (Day: 0, Hour: 0, Value: double.MinValue);
            var total = 0.0;
            var busiestDay = 0;
            var dayTotals = new double[7];
            for (var day = 0; day < 7; day++)
            {
                for (var hour = 0; hour < 24; hour++)
                {
                    var value = active.Grid[day, hour];
                    total += value;
                    dayTotals[day] += value;
                    if (value > best.Value)
                    {
                        best = (day, hour, value);
                    }
                }
            }

            busiestDay = Array.IndexOf(dayTotals, dayTotals.Max());
            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*") };
            var tiles = new (string Label, string Value, string Note)[]
            {
                ("Peak hour", Days[best.Day] + " " + best.Hour.ToString("00") + ":00", $"{active.Format(best.Value)} {active.Unit}"),
                ("Busiest day", Days[busiestDay], DataKit.Number(dayTotals[busiestDay]) + " " + active.Unit),
                ("Week total", DataKit.Compact(total), "sum of all 168 hours")
            };
            for (var index = 0; index < tiles.Length; index++)
            {
                var info = tiles[index];
                var tile = DataKit.Tile(new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        DataKit.Caption(info.Label),
                        DataKit.Text(info.Value, "arc-body arc-medium"),
                        DataKit.Caption(info.Note)
                    }
                });
                Grid.SetColumn(tile, index * 2);
                stats.Children.Add(tile);
            }

            statsHost.Content = stats;

            var peaks = Enumerable.Range(0, 24)
                .Select(hour => (Hour: hour, Value: Enumerable.Range(0, 7).Average(day => active.Grid[day, hour])))
                .OrderByDescending(item => item.Value)
                .Take(3)
                .ToList();
            var rows = new StackPanel { Spacing = 6 };
            foreach (var peak in peaks)
            {
                var button = DataKit.Chip($"{peak.Hour:00}:00", false, () =>
                {
                    stage?.Highlight(busiestDay, peak.Hour);
                    DataKit.Say(status, $"Flew to {Days[busiestDay]} {peak.Hour:00}:00 · {active.Format(active.Grid[busiestDay, peak.Hour])} {active.Unit}.");
                }, DataKit.Compact(peak.Value));
                rows.Children.Add(button);
            }

            peakHost.Content = rows;
        }

        void Refresh()
        {
            var active = Active();
            stage = new ArcTerrainStage(active.Grid, active.Unit) { Flattened = flattened };
            stage.SelectionChanged += (_, cell) =>
            {
                readout.Text = $"{Days[cell.Day]} {cell.Hour:00}:00 · {active.Format(active.Grid[cell.Day, cell.Hour])} {active.Unit}";
            };
            readout.Text = $"{Days[stage.Day]} {stage.Hour:00}:00 · {active.Format(active.Grid[stage.Day, stage.Hour])} {active.Unit}";
            stageHost.Content = stage;
            flattenToggle.Content = DataKit.Toggle("Heatmap", flattened, value =>
            {
                flattened = value;
                if (stage is not null)
                {
                    stage.Flattened = value;
                }

                DataKit.Say(status, value ? "Flattened to a heatmap." : "Columns raised to the terrain view.");
            });
            RefreshStats();
        }

        var metricSelector = DataKit.Segmented(metrics.Select(item => (item.Key, item.Label)), activeKey, key =>
        {
            activeKey = key;
            Refresh();
            DataKit.Say(status, $"Metric set to {Active().Label}.");
        });

        Refresh();

        var header = DataKit.Header("Activity terrain", "Hourly traffic across a week · week of 12 October",
            DataKit.Row(10, metricSelector, flattenToggle));

        var stageCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Hour of day"), readout),
                stageHost,
                DataKit.Caption("Arrows move the highlight · Shift + arrows orbit and scale · + / − zoom · Enter flattens · Escape resets")
            }
        });

        var sideCard = DataKit.Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                DataKit.Label("Peak hours"),
                DataKit.Caption("Average across the week"),
                peakHost,
                DataKit.Divider(),
                DataKit.Label("Camera"),
                DataKit.Caption("Shift + arrows orbit and tilt, + / − zoom. The projection is isometric: far columns shrink by the lattice angle, not by perspective."),
                DataKit.Link("Reset camera", () =>
                {
                    stage?.ResetCamera();
                    DataKit.Say(status, "Camera reset to the whole week.");
                })
            }
        }, width: 268);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,268"),
            Children = { stageCard, DataKit.AtColumn(sideCard, 2) }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, statsHost, body, status }
        }, 16);
    }

    /// <summary>A seeded 7 by 24 grid: office-hours shape, weekday weight and a lunch bump.</summary>
    private static double[,] BuildGrid(double floor, double peak, double evening, int seed)
    {
        var random = DataKit.Random(seed);
        var grid = new double[7, 24];
        for (var day = 0; day < 7; day++)
        {
            var weekday = day < 5 ? 1.0 : 0.62;
            for (var hour = 0; hour < 24; hour++)
            {
                var curve = Math.Exp(-Math.Pow((hour - 14) / 3.6, 2)) * peak +
                            Math.Exp(-Math.Pow((hour - 20) / 2.4, 2)) * evening +
                            floor;
                var noise = (random() - 0.5) * 0.12;
                grid[day, hour] = Math.Max(0, Math.Round(curve * weekday * (1 + noise), 1));
            }
        }

        return grid;
    }
}
