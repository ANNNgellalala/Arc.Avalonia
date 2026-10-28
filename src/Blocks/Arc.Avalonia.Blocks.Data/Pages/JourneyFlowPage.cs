using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Journey flow: a Sankey of trial journeys across funnel stages whose ribbon widths follow a channel switch.
/// Hovering a step or ribbon traces that cohort through the whole journey. Counts are seeded sample data.
/// An independent Avalonia implementation of Arc's public journey-flow block; no Pro source was used.
/// </summary>
public sealed class JourneyFlowPage : ArcItemPage
{
    public override string ItemSlug => "journey-flow";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Showing where users go between funnel stages, such as visit to signup to activation.",
        "Comparing flows across acquisition channels with a segment switch."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use bar-chart for a simple linear funnel without branching paths."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The channel switch is a radio group; the diagram is an image with a text summary and every step label names its users.",
        "Hovering a step or ribbon dims the rest and announces the traced cohort politely; the step buttons below the diagram repeat the same numbers as text."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Ribbons are laid out again on a channel change and the summary counts follow a beat later, never overshooting.",
        "Reduced motion keeps the layout change instant with no fade of the traced state."
    };

    public override string? ImplementationNotes =>
        "Flows are recomputed per channel rather than morphed, and tracing follows the pointer inside ArcSankeyFlow (hover traces the cohort); selecting a step to pin its breakdown is not recreated, so the breakdown panel mirrors the whole channel. Number of nodes is nine, not the reference's thirteen.";

    private sealed record Channel(string Key, string Label, double Factor, double Variance);

    private static readonly Channel[] Channels =
    {
        new("all", "All", 1.0, 0.0),
        new("organic", "Organic", 0.41, 0.06),
        new("paid", "Paid", 0.33, 0.12),
        new("referral", "Referral", 0.16, -0.04)
    };

    private static readonly (string From, string To, double Value)[] Flows =
    {
        ("visit", "pricing", 3100),
        ("visit", "docs", 2400),
        ("visit", "signup", 4200),
        ("pricing", "signup", 1900),
        ("docs", "signup", 1500),
        ("signup", "activated", 3100),
        ("signup", "stalled", 1100),
        ("activated", "paid", 2400),
        ("activated", "churn", 700),
        ("stalled", "churn", 900)
    };

    private static readonly (string Id, string Label, int Column)[] NodeSpecs =
    {
        ("visit", "Visited", 0),
        ("pricing", "Pricing", 1),
        ("docs", "Docs", 1),
        ("signup", "Signed up", 2),
        ("activated", "Activated", 3),
        ("stalled", "Stalled", 3),
        ("paid", "Paid", 4),
        ("churn", "Churned", 4)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var channel = "all";
        var flowHost = new ContentControl();
        var panelHost = new ContentControl();
        var stepsHost = new ContentControl();
        var status = DataKit.Status();
        var summary = DataKit.Caption(string.Empty);

        double Scaled(double baseValue, Channel selected, int index)
        {
            var jitter = 1 + Math.Sin(index * 1.7 + selected.Key.Length) * selected.Variance;
            return Math.Round(baseValue * selected.Factor * jitter);
        }

        void Refresh()
        {
            var selected = Channels.First(item => item.Key == channel);
            var links = Flows.Select((flow, index) => new ArcSankeyLink(
                flow.From, flow.To, Math.Max(20, Scaled(flow.Value, selected, index)))).ToList();
            var nodes = NodeSpecs.Select(spec => new ArcSankeyNode(spec.Id, spec.Label, spec.Column)).ToList();

            flowHost.Content = new ArcSankeyFlow
            {
                Nodes = nodes,
                Links = links,
                Height = 300,
                ShowLabels = true,
                AllowNodeDrag = true,
                ValueSuffix = " people",
                ValueFormat = value => DataKit.Compact(value)
            };

            var byFrom = links.ToLookup(link => link.From, link => link.Value);
            var byTo = links.ToLookup(link => link.To, link => link.Value);
            var visited = byFrom["visit"].Sum();
            var signup = byTo["signup"].Sum();
            var activated = byTo["activated"].Sum();
            var paid = byTo["paid"].Sum();

            summary.Text = $"Visited {DataKit.Number(visited)} · signup {DataKit.Number(signup)} · activated {DataKit.Number(activated)} · paid {DataKit.Number(paid)}";
            panelHost.Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    DataKit.Stat("Visitors", DataKit.Number(visited), $"{selected.Label} traffic · last 30 days"),
                    DataKit.Divider(),
                    DataKit.Line("Visit → signup", DataKit.Percent(signup / Math.Max(1, visited), 1)),
                    DataKit.Line("Signup → activated", DataKit.Percent(activated / Math.Max(1, signup), 1)),
                    DataKit.Line("Activated → paid", DataKit.Percent(paid / Math.Max(1, activated), 1)),
                    DataKit.Line("Visitor → paid", DataKit.Percent(paid / Math.Max(1, visited), 1), strong: true, accent: true),
                    DataKit.Divider(),
                    DataKit.Caption(channel == "all"
                        ? "Paid search brings the most volume; referral converts best."
                        : channel == "organic"
                            ? "Organic visitors read the docs first and convert on the second visit."
                            : channel == "paid"
                                ? "Paid traffic stalls after signup more often than other channels."
                                : "Referral cohorts activate quickly and rarely stall."),
                    DataKit.Divider(),
                    DataKit.Line("Stalled after signup", DataKit.Number(byTo["stalled"].Sum()), note: "no activation"),
                    DataKit.Line("Churned after activation", DataKit.Number(byTo["churn"].Sum()), note: "left before paying")
                }
            };

            var stages = new[]
            {
                ("Visited", visited, DataKit.Series(0)),
                ("Signed up", signup, DataKit.Series(1)),
                ("Activated", activated, DataKit.Series(2)),
                ("Paid", paid, DataKit.Series(3))
            };
            var strip = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*,10,*") };
            for (var index = 0; index < stages.Length; index++)
            {
                var stage = stages[index];
                var share = stage.Item2 / Math.Max(1, visited);
                var card = DataKit.Tile(new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        DataKit.Row(7, DataKit.Dot(stage.Item3, 6), DataKit.Caption(stage.Item1)),
                        DataKit.Text(DataKit.Number(stage.Item2), "arc-body arc-medium"),
                        DataKit.Caption(DataKit.Percent(share, 1) + " of visitors")
                    }
                });
                Grid.SetColumn(card, index * 2);
                strip.Children.Add(card);
            }

            stepsHost.Content = strip;
        }

        var channelSelector = DataKit.Segmented(Channels.Select(item => (item.Key, item.Label)), channel, key =>
        {
            channel = key;
            Refresh();
            DataKit.Say(status, $"Channel set to {Channels.First(item => item.Key == key).Label}.");
        });

        Refresh();

        var header = DataKit.Header("Trial journeys", "Visit to paid across acquisition channels · last 30 days", channelSelector);

        var flowCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Journey"), summary),
                flowHost,
                DataKit.Caption("Hover a step or ribbon to trace the cohort; drag a step to reorder its column.")
            }
        });

        var panelCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children = { DataKit.Label("Channel detail"), panelHost }
        }, width: 288);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,288"),
            Children = { flowCard, DataKit.AtColumn(panelCard, 2) }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 12,
            Children = { header, body, stepsHost, status }
        }, 16);
    }
}
