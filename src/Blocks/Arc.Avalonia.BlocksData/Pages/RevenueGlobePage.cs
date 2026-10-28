using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksData.Pages;

/// <summary>
/// Revenue globe: a stylised dotted globe where today's payments arc home to headquarters, a replay clock
/// that reveals one hour at a time, and region and city breakdowns for the revealed hours. Coordinates,
/// weights and the day are sample data. An independent Avalonia implementation of Arc's public revenue-globe
/// block; no Pro source was used.
/// </summary>
public sealed class RevenueGlobePage : ArcItemPage
{
    public override string ItemSlug => "revenue-globe";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Revenue or operations overviews showing where payments come from right now.",
        "Showcase pages where a live globe with arcs and a day timeline adds impact."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a table or bar-chart when exact regional numbers matter more than the view.",
        "Use customer-galaxy to explore accounts rather than transactions."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The globe is one focusable region: arrows spin it, Up and Down step the hour, and Escape clears the selected city.",
        "The timeline is a slider whose value text reads the UTC time; region and city rows are buttons and each change is announced politely."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Payments draw as arcs to headquarters while the clock advances; totals roll without overshooting.",
        "Reduced motion steps the clock instantly instead of easing the arcs."
    };

    public override string? ImplementationNotes =>
        "The globe is an orthographic dotted sphere drawn in two dimensions, not WebGL: there is no terrain texture, no lighting and no pinch zoom. Only the visible hemisphere is drawn and arcs are quadratic curves lifted from the centre. The replay clock is a local timer over seeded per-city weights; no payments service is queried.";

    private static readonly GlobeCity[] Cities =
    {
        new("San Francisco", "North America", 37.77, -122.42, 0.16),
        new("New York", "North America", 40.71, -74.01, 0.14),
        new("Toronto", "North America", 43.65, -79.38, 0.04),
        new("London", "EMEA", 51.51, -0.13, 0.13),
        new("Berlin", "EMEA", 52.52, 13.40, 0.08),
        new("Paris", "EMEA", 48.86, 2.35, 0.06),
        new("Dubai", "EMEA", 25.20, 55.27, 0.03),
        new("Singapore", "APAC", 1.35, 103.82, 0.09),
        new("Tokyo", "APAC", 35.68, 139.69, 0.08),
        new("Sydney", "APAC", -33.87, 151.21, 0.05),
        new("São Paulo", "LATAM", -23.55, -46.63, 0.07),
        new("Mexico City", "LATAM", 19.43, -99.13, 0.05)
    };

    private const double DayTotal = 186_400;

    private static string ShortRegion(string region) => region switch
    {
        "North America" => "NA",
        _ => region
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var hour = 14;
        var playing = false;
        var selected = (string?)null;
        var globeHost = new ContentControl();
        var panelHost = new ContentControl();
        var clockHost = new ContentControl();
        var status = DataKit.Status();
        var caption = DataKit.Caption(string.Empty);
        ArcDotGlobe? globe = null;
        DispatcherTimer? timer = null;

        bool Revealed(GlobeCity city) => Local(city) is >= 8 and <= 20;

        double LocalHour(GlobeCity city) => (hour + city.Longitude / 15 + 24) % 24;

        double Local(GlobeCity city) => LocalHour(city);

        void Refresh()
        {
            globe ??= new ArcDotGlobe(Cities, hour);
            globe.Hour = hour;
            globeHost.Content = globe;

            var revealed = Cities.Where(Revealed).ToList();
            var captured = revealed.Sum(city => city.Weight * DayTotal);
            var regions = revealed.GroupBy(city => city.Region)
                .Select(group => (Region: group.Key, Total: group.Sum(city => city.Weight * DayTotal)))
                .OrderByDescending(row => row.Total)
                .ToList();

            var rows = new StackPanel { Spacing = 0 };
            foreach (var region in regions)
            {
                var share = region.Total / Math.Max(1, captured);
                var isSelected = region.Region == selected;
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 8,
                    Height = 28,
                    Children =
                    {
                        DataKit.Dot(isSelected ? DataKit.Brush(ArcTokens.Accent) : DataKit.Series(regions.IndexOf(region) % 4)),
                        DataKit.AtColumn(DataKit.Text(region.Region, "arc-body-sm"), 1),
                        DataKit.AtColumn(DataKit.Text(DataKit.Money(region.Total), "arc-body-sm arc-medium"), 2)
                    }
                };
                rows.Children.Add(row);
                rows.Children.Add(new Grid
                {
                    Height = 4,
                    ColumnDefinitions = new ColumnDefinitions($"{Math.Clamp(share, 0.03, 1):0.###}*,{1 - Math.Clamp(share, 0.03, 1):0.###}*"),
                    Children =
                    {
                        new Border
                        {
                            Background = DataKit.Series(regions.IndexOf(region) % 4),
                            CornerRadius = new CornerRadius(2)
                        }
                    }
                });
            }

            var cityRows = new StackPanel { Spacing = 0 };
            foreach (var city in revealed.OrderByDescending(city => city.Weight).Take(5))
            {
                var cityRow = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                    ColumnSpacing = 8,
                    Height = 26,
                    Children =
                    {
                        DataKit.Text(city.Name + " · " + ShortRegion(city.Region), "arc-caption"),
                        DataKit.AtColumn(DataKit.Caption(((int)LocalHour(city)).ToString("00") + ":00 local"), 1),
                        DataKit.AtColumn(DataKit.Text(DataKit.Money(city.Weight * DayTotal), "arc-caption"), 2)
                    }
                };
                cityRows.Children.Add(cityRow);
            }

            panelHost.Content = new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    DataKit.Stat("Captured today", DataKit.Money(captured), $"{revealed.Count} of {Cities.Length} markets open"),
                    DataKit.Divider(),
                    DataKit.Label("Regions"),
                    rows,
                    DataKit.Divider(),
                    DataKit.Label("Markets"),
                    cityRows
                }
            };

            caption.Text = $"{hour:00}:00 UTC · {revealed.Count} markets open";
        }

        void RefreshClock()
        {
            var time = new TextBlock
            {
                Text = $"{hour:00}:00",
                Classes = { "arc-body arc-medium" }
            };
            var play = DataKit.IconBtn(playing ? "pause" : "play", playing ? "Pause replay" : "Play replay", () =>
            {
                playing = !playing;
                if (timer is null)
                {
                    timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                    timer.Tick += (_, _) =>
                    {
                        hour = (hour + 1) % 24;
                        Refresh();
                        RefreshClock();
                    };
                }

                if (playing)
                {
                    timer.Start();
                }
                else
                {
                    timer.Stop();
                }

                RefreshClock();
                DataKit.Say(status, playing ? "Replaying the day." : "Replay paused.");
            }, 30);

            var slider = DataKit.Slider(null, hour, 0, 23, 1, value =>
            {
                if (Math.Abs(value - hour) < 0.01)
                {
                    return;
                }

                hour = (int)Math.Round(value);
                Refresh();
                DataKit.Say(status, $"Clock moved to {hour:00}:00 UTC.");
            });

            var clockGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
                ColumnSpacing = 10
            };
            Grid.SetColumn(play, 0);
            Grid.SetColumn(time, 1);
            Grid.SetColumn(slider, 2);
            var state = DataKit.Pill(playing ? "Replaying" : "Paused",
                playing ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral, playing);
            Grid.SetColumn(state, 3);
            clockGrid.Children.Add(play);
            clockGrid.Children.Add(time);
            clockGrid.Children.Add(slider);
            clockGrid.Children.Add(state);
            clockHost.Content = clockGrid;
        }

        Refresh();
        RefreshClock();
        globe!.SelectionChanged += (_, name) =>
        {
            selected = name;
            Refresh();
            DataKit.Say(status, name is null ? "City selection cleared." : $"{name} selected. {DataKit.Money(Cities.First(city => city.Name == name).Weight * DayTotal)} captured today.");
        };

        var header = DataKit.Header("Revenue globe", "Payments today · headquarters in Austin · replay the day",
            DataKit.Row(8, DataKit.Pill("Live", ArcBadgeTone.Success, true), DataKit.Meta("globe", "12 markets")));

        var globeCard = DataKit.Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Payments in flight"), caption),
                globeHost,
                clockHost,
                DataKit.Caption("Drag the globe to spin · Up and Down step the hour · the clock reveals each market as its day opens")
            }
        });

        var panelCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children = { DataKit.Label("Where it came from"), panelHost }
        }, width: 292);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,292"),
            Children = { globeCard, DataKit.AtColumn(panelCard, 2) }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, body, status }
        }, 16);
    }
}
