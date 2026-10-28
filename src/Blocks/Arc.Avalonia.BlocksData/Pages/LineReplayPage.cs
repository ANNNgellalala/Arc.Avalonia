using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Charts;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksData.Pages;

/// <summary>
/// Line replay: a day of traffic with a moving playhead, the value at the tip, a scrubber, speed control and
/// key moments annotated as the playhead passes them. Readings are seeded sample data. An independent
/// Avalonia implementation of Arc's public line-replay block; no Pro source was used.
/// </summary>
public sealed class LineReplayPage : ArcItemPage
{
    public override string ItemSlug => "line-replay";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A day of traffic, signups or latency that reads better as a replay than as a static line.",
        "Incident reviews where a few annotated moments need the reader to arrive at them in order."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use line-chart when the shape matters more than the moment; playback takes time.",
        "Use realtime-stream for data that is still arriving."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The chart is a day slider: arrows step one interval, Home and End jump, and the playhead can be dragged.",
        "The head's time and value are printed on the label and repeated in the live readout."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Playback advances the playhead one interval per tick; the line ahead of the head is drawn quietly so the whole day is still visible.",
        "Reduced motion pauses after one step rather than easing the head, and the label never overshoots."
    };

    public override string? ImplementationNotes =>
        "The line is drawn in full from the first frame and the head moves rather than the path. Playback is a timer over 144 readings; annotations are seeded index markers.";

    private static readonly string[] Speeds = { "1x", "2x", "4x" };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var count = 144;
        var labels = Enumerable.Range(0, count)
            .Select(index => (index / 6).ToString("00") + ":" + (index % 6 * 10).ToString("00"))
            .ToList();
        var values = DataSamples.Daily(count, 320, 0.9, 46, 26, 41, 0)
            .Select((value, index) => Math.Round(value * (0.7 + 0.3 * Math.Sin(index / 20.0)), 0))
            .ToArray();
        var events = new List<ReplayEvent>
        {
            new(24, "Newsletter"),
            new(62, "Launch post"),
            new(96, "Incident"),
            new(124, "Fix shipped")
        };

        var head = 60;
        var playing = false;
        var speed = 2;
        var chartHost = new ContentControl();
        var status = DataKit.Status();
        var readout = DataKit.Caption(string.Empty);
        var statsHost = new ContentControl();
        var sliderHost = new ContentControl();
        var playHost = new ContentControl();
        ArcReplayLine? chart = null;
        DispatcherTimer? timer = null;

        void RefreshStats()
        {
            var current = values[head];
            var peak = values.Max();
            var peakIndex = Array.IndexOf(values, peak);
            var total = values.Take(head + 1).Sum() * 10;
            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*") };
            var tiles = new (string Label, string Value, string Note)[]
            {
                ("At the head", DataKit.Number(current), labels[head] + " · visitors per 10 min"),
                ("Day peak", DataKit.Number(peak), labels[peakIndex]),
                ("Visitors so far", DataKit.Compact(total), $"of {DataKit.Compact(values.Sum() * 10)} projected")
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
            readout.Text = $"{labels[head]} · {DataKit.Number(current)} visitors · {(head >= 24 ? "after the newsletter" : "morning ramp")}";
        }

        void RefreshSlider()
        {
            sliderHost.Content = DataKit.Slider(null, head, 0, count - 1, 1, value =>
            {
                var index = (int)Math.Round(value);
                if (index == head)
                {
                    return;
                }

                head = index;
                chart?.SetHead(index);
                RefreshStats();
            }, showValue: true);
        }

        void RefreshPlay()
        {
            playHost.Content = DataKit.Row(8,
                DataKit.IconBtn(playing ? "pause" : "play", playing ? "Pause replay" : "Play replay", () =>
                {
                    playing = !playing;
                    if (timer is null)
                    {
                        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
                        timer.Tick += (_, _) =>
                        {
                            var next = head + 1;
                            if (next >= count)
                            {
                                next = 0;
                            }

                            head = next;
                            chart?.SetHead(next);
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

                    timer.Interval = TimeSpan.FromMilliseconds(260 / Math.Max(1, speed));
                    if (playing)
                    {
                        timer.Start();
                    }
                    else
                    {
                        timer.Stop();
                    }

                    RefreshPlay();
                    DataKit.Say(status, playing ? $"Replaying at {speed}x." : "Replay paused.");
                }, 30),
                DataKit.Segmented(Speeds.Select(item => (item, item)), speed + "x", key =>
                {
                    speed = int.Parse(key.TrimEnd('x'));
                    if (timer is not null)
                    {
                        timer.Interval = TimeSpan.FromMilliseconds(260 / Math.Max(1, speed));
                    }

                    DataKit.Say(status, $"Speed set to {speed}x.");
                }));
        }

        chart = new ArcReplayLine(values, labels, events, "visitors", head) { Height = 240 };
        chart.HeadChanged += (_, index) =>
        {
            head = index;
            RefreshStats();
            RefreshSlider();
        };
        chartHost.Content = chart;

        RefreshStats();
        RefreshSlider();
        RefreshPlay();

        var header = DataKit.Header("Traffic replay", "18 November 2026 · 144 ten-minute readings · four annotated moments",
            playHost);

        var card = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Visitors"), readout),
                chartHost,
                sliderHost,
                DataKit.Caption("Click or drag the chart to scrub · arrows step one reading · Home and End jump to the ends")
            }
        });

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, card, statsHost, status }
        }, 16);
    }
}
