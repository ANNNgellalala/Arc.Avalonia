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
/// Connected scatter: monthly CAC against LTV for three channels, joined in time order with a scrubber that
/// moves a head along the curve and a channel switch that swaps the path against a shaded 3x target. Months
/// and values are seeded sample data. An independent Avalonia implementation of Arc's public connected-scatter
/// block; no Pro source was used.
/// </summary>
public sealed class ConnectedScatterPage : ArcItemPage
{
    public override string ItemSlug => "connected-scatter";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A growth or finance screen that shows whether unit economics are moving toward or away from a target ratio.",
        "Comparing how a few channels travelled through the same two metrics over the same months."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use line-chart or metric-explorer when time itself is the axis and one metric is enough.",
        "Use a plain scatter or beeswarm when points have no time order to connect."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The scrubber is a month slider; the chart takes the arrow keys and Home and End.",
        "The readout repeats month, CAC, LTV and ratio, and says above or below target in words."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Playback walks the head from the first month to the last; a channel change swaps every point at once. The reference morphs the path point by point and draws it in on first view.",
        "Reduced motion steps playback month by month and never animates the head between points."
    };

    public override string? ImplementationNotes =>
        "Three channels share eighteen monthly points, and the path is a straight polyline rather than a Catmull-Rom curve. Playback is a timer over the month index; the healthy zone is the area above y = 3x.";

    private static readonly string[] Months =
    {
        "Jan 25", "Feb 25", "Mar 25", "Apr 25", "May 25", "Jun 25", "Jul 25", "Aug 25", "Sep 25",
        "Oct 25", "Nov 25", "Dec 25", "Jan 26", "Feb 26", "Mar 26", "Apr 26", "May 26", "Jun 26"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var channels = BuildSeries();
        var channel = "all";
        var head = Months.Length - 1;
        var playing = false;
        var chartHost = new ContentControl();
        var statsHost = new ContentControl();
        var sliderHost = new ContentControl();
        var status = DataKit.Status();
        var playHost = new ContentControl();
        ArcConnectedScatter? chart = null;
        DispatcherTimer? timer = null;

        ScatterSeries Active() => channels.First(item => item.Name == channel);

        void RefreshStats()
        {
            var series = Active();
            var point = series.Points[head];
            var ratio = point.Y / Math.Max(1, point.X);
            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*,12,*") };
            var tiles = new (string Label, string Value, string Note)[]
            {
                ("CAC", DataKit.Money(point.X), point.Label),
                ("LTV, 12 month", DataKit.Money(point.Y), "per account"),
                ("LTV / CAC", ratio.ToString("0.00") + "x", ratio >= 3 ? "above the 3x target" : "below the 3x target"),
                ("Path length", DataKit.Number(series.Points.Count), "months joined")
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
        }

        void RefreshSlider()
        {
            sliderHost.Content = DataKit.Slider(null, head, 0, Months.Length - 1, 1, value =>
            {
                var index = (int)Math.Round(value);
                if (index == head)
                {
                    return;
                }

                head = index;
                if (chart is not null)
                {
                    chart.Head = index;
                }

                RefreshStats();
                DataKit.Say(status, $"{Months[index]} selected.");
            }, showValue: true);
        }

        void RefreshPlay()
        {
            playHost.Content = DataKit.Btn(playing ? "Pause" : "Play months",
                ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                {
                    playing = !playing;
                    if (timer is null)
                    {
                        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                        timer.Tick += (_, _) =>
                        {
                            var next = (chart?.Head ?? head) + 1;
                            if (next >= Months.Length)
                            {
                                next = 0;
                            }

                            head = next;
                            if (chart is not null)
                            {
                                chart.Head = next;
                            }

                            RefreshStats();
                            RefreshSlider();
                            if (ArcThemeManager.ReducedMotion)
                            {
                                playing = false;
                                timer.Stop();
                                RefreshPlay();
                            }
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

                    RefreshPlay();
                    DataKit.Say(status, playing ? "Walking the head through every month." : "Playback paused.");
                }, icon: playing ? "pause" : "play");
        }

        void RefreshChart()
        {
            chart = new ArcConnectedScatter(channels, 500, 740, 1200, 2900, 3, head)
            {
                SeriesKey = channel,
                Height = 250
            };
            chart.HeadChanged += (_, index) =>
            {
                head = index;
                RefreshStats();
                RefreshSlider();
            };
            chartHost.Content = chart;
        }

        var channelSelector = DataKit.Segmented(
            channels.Select(item => (item.Name, LabelFor(item.Name))), channel, key =>
            {
                channel = key;
                RefreshChart();
                RefreshStats();
                DataKit.Say(status, $"Channel set to {LabelFor(Active().Name)}.");
            });

        RefreshChart();
        RefreshStats();
        RefreshSlider();
        RefreshPlay();

        var header = DataKit.Header("CAC and LTV by channel", "January 2025 to June 2026 · healthy above 3x",
            DataKit.Row(10, channelSelector, playHost));

        var chartCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                chartHost,
                sliderHost,
                DataKit.Caption("Click a month or use the arrows · the shaded side is healthy")
            }
        });

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, chartCard, statsHost, status }
        }, 16);
    }

    private static string LabelFor(string name) => name switch
    {
        "all" => "All channels",
        "partners" => "Partners",
        _ => "Paid search"
    };

    private static List<ScatterSeries> BuildSeries()
    {
        var result = new List<ScatterSeries>();
        var definitions = new (string Name, double Cac, double Ltv, double CacDrift, double LtvDrift, double Noise)[]
        {
            ("all", 700, 1500, -9.0, 55.0, 34),
            ("partners", 460, 1600, -5.0, 62.0, 24),
            ("paid", 880, 1500, 10.0, 22.0, 40)
        };

        for (var channelIndex = 0; channelIndex < definitions.Length; channelIndex++)
        {
            var definition = definitions[channelIndex];
            var random = DataKit.Random(4400 + channelIndex * 113);
            var points = new List<ScatterPoint>();
            var cac = definition.Cac;
            var ltv = definition.Ltv;
            for (var month = 0; month < Months.Length; month++)
            {
                cac += definition.CacDrift + (random() - 0.5) * definition.Noise;
                ltv += definition.LtvDrift + (random() - 0.5) * definition.Noise * 1.6;
                points.Add(new ScatterPoint(Months[month], Math.Round(cac), Math.Round(ltv)));
            }

            result.Add(new ScatterSeries(definition.Name, points, DataKit.Series(channelIndex)));
        }

        return result;
    }
}
