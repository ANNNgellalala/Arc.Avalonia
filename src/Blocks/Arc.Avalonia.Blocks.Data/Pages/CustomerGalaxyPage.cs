using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Customer galaxy: about 1,200 accounts drawn as a drifting galaxy that clusters by plan, region or health,
/// sized by MRR, with a totals panel and pan and zoom. Accounts are seeded sample data. An independent
/// Avalonia implementation of Arc's public customer-galaxy block; no Pro source was used.
/// </summary>
public sealed class CustomerGalaxyPage : ArcItemPage
{
    public override string ItemSlug => "customer-galaxy";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-galaxy", "variant-clusters", "variant-grouping", "variant-book-panel", "variant-legend"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Customer success or revenue pages to explore where MRR and risk concentrate.",
        "Showcase views that cluster thousands of accounts by plan, region or health."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use customers-table when people need to find and act on specific accounts.",
        "Use semantic-zoom for hierarchical data like teams and projects."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The stage is one focusable application region: arrows fly between clusters, plus and minus zoom, Escape clears the selection and Home fits all accounts.",
        "Grouping is a tab group, and the cluster rows repeat every total as text with MRR and account counts."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Accounts drift slowly at rest and spiral into clusters when the grouping changes; flying to a cluster eases the camera.",
        "Reduced motion snaps the layout and stops the drift, which is how the shot build renders it."
    };

    public override string? ImplementationNotes =>
        "A 2D canvas scatter, not a WebGL particle system: points are seeded once, the drift is a slow rotation of the cloud, and clusters are placed on a ring rather than solved by a force simulation. Pan, zoom and cluster selection work; there is no pinch gesture on desktop.";

    private static readonly string[] Prefixes =
    {
        "Northwind", "Kestrel", "Bramble", "Halcyon", "Meridian", "Okapi", "Pinewood", "Quartz",
        "Sable", "Tidewater", "Arbor", "Cobalt", "Drift", "Ember", "Fathom", "Grove"
    };

    private static readonly string[] Suffixes = { "Labs", "Health", "Studio", "Freight", "Works", "Retail", "AI", "Bank", "& Co", "Group" };

    private static readonly string[] RegionNames = { "North America", "EMEA", "APAC", "LATAM" };
    private static readonly string[] PlanNames = { "Starter", "Growth", "Scale", "Enterprise" };
    private static readonly string[] HealthNames = { "Healthy", "Watch", "At risk" };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var accounts = BuildAccounts(2400);
        var grouping = "plan";
        var stageHost = new ContentControl();
        var panelHost = new ContentControl();
        var status = DataKit.Status();
        var selection = DataKit.Caption("All clusters");
        ArcGalaxyStage? stage = null;

        void RefreshPanel()
        {
            var groups = accounts.GroupBy(account => grouping switch
                {
                    "region" => account.Region,
                    "health" => account.Health,
                    _ => account.Plan
                })
                .Select(group => (
                    Name: group.Key,
                    Accounts: group.Count(),
                    Mrr: group.Sum(account => account.Mrr),
                    Risk: group.Count(account => account.Health == "At risk")))
                .OrderByDescending(group => group.Mrr)
                .ToList();
            var totalMrr = accounts.Sum(account => account.Mrr);
            var atRisk = accounts.Count(account => account.Health == "At risk");
            var rows = new StackPanel { Spacing = 8 };
            foreach (var group in groups)
            {
                var share = group.Mrr / Math.Max(1, totalMrr);
                rows.Children.Add(new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                            ColumnSpacing = 8,
                            Children =
                            {
                                DataKit.Dot(DataKit.Series(groups.IndexOf(group)), 7),
                                DataKit.AtColumn(DataKit.Text(group.Name, "arc-body-sm"), 1),
                                DataKit.AtColumn(DataKit.Text(DataKit.MoneyCompact(group.Mrr), "arc-body-sm arc-medium"), 2)
                            }
                        },
                        new Grid
                        {
                            Height = 4,
                            ColumnDefinitions = new ColumnDefinitions($"{Math.Clamp(share, 0.03, 1):0.###}*,{1 - Math.Clamp(share, 0.03, 1):0.###}*"),
                            Children =
                            {
                                new Border
                                {
                                    Background = DataKit.Series(groups.IndexOf(group) % 4),
                                    CornerRadius = new CornerRadius(2)
                                }
                            }
                        },
                        DataKit.Caption($"{DataKit.Number(group.Accounts)} accounts · {group.Risk} at risk")
                    }
                });
            }

            panelHost.Content = new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    DataKit.Stat("Book MRR", DataKit.Money(totalMrr), $"{DataKit.Number(accounts.Count)} accounts · {DataKit.Number(atRisk)} at risk"),
                    DataKit.Divider(),
                    DataKit.Label("Clusters"),
                    rows,
                    DataKit.Caption("Click a cluster or use the arrow keys on the stage to dim the rest.")
                }
            };
        }

        void RefreshStage()
        {
            if (stage is null)
            {
                stage = new ArcGalaxyStage(accounts);
                stage.ClusterSelected += (_, name) =>
                {
                    selection.Text = name is null ? "All clusters" : name;
                    DataKit.Say(status, name is null
                        ? "Cluster selection cleared; all accounts shown."
                        : $"{name} selected. {DataKit.Number(accounts.Count(account => account.GroupKey(grouping) == name))} accounts.");
                };
                stageHost.Content = stage;
            }

            stage.Grouping = grouping;
            RefreshPanel();
        }

        var groupingSelector = DataKit.Segmented(new[] { ("plan", "Plan"), ("region", "Region"), ("health", "Health") },
            grouping, key =>
            {
                grouping = key;
                RefreshStage();
                DataKit.Say(status, $"Galaxy regrouped by {key}. Clusters are laid out again.");
            });

        RefreshStage();

        var header = DataKit.Header("Customer galaxy", $"{DataKit.Number(accounts.Count)} accounts · {DataKit.MoneyCompact(accounts.Sum(account => account.Mrr))} book · grouping decides the clusters",
            DataKit.Row(8, groupingSelector));

        var stageCard = DataKit.Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                DataKit.Row(8, DataKit.Label("Accounts by " + grouping), selection),
                stageHost,
                DataKit.Caption("Drag to pan · +/- zoom · arrows between clusters · Home fits all")
            }
        });

        var panelCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children = { DataKit.Label("Book"), panelHost }
        }, width: 292);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,292"),
            Children = { stageCard, DataKit.AtColumn(panelCard, 2) }
        };

        var legend = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            Children =
            {
                Legend(DataKit.Brush(ArcTokens.Series1), "Healthy"),
                Legend(DataKit.Brush(ArcTokens.Warning), "Watch"),
                Legend(DataKit.Brush(ArcTokens.Danger), "At risk"),
                DataKit.Caption("Point size is MRR; the orbits are a visual frame, not a scale.")
            }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, body, legend, status }
        }, 16);
    }

    private static Control Legend(global::Avalonia.Media.IBrush brush, string label) => DataKit.Row(6,
        new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = brush, VerticalAlignment = VerticalAlignment.Center },
        DataKit.Caption(label));

    private static List<GalaxyAccount> BuildAccounts(int count)
    {
        var random = DataKit.Random(90210);
        var accounts = new List<GalaxyAccount>(count);
        for (var index = 0; index < count; index++)
        {
            var name = Prefixes[index % Prefixes.Length] + " " + Suffixes[(index / Prefixes.Length) % Suffixes.Length];
            var plan = PlanNames[(int)(Math.Pow(random(), 1.8) * PlanNames.Length) % PlanNames.Length];
            var region = RegionNames[(int)(random() * RegionNames.Length) % RegionNames.Length];
            var roll = random();
            var health = roll > 0.88 ? "At risk" : roll > 0.72 ? "Watch" : "Healthy";
            var baseMrr = plan switch
            {
                "Enterprise" => 6400,
                "Scale" => 2400,
                "Growth" => 880,
                _ => 220
            };
            var mrr = Math.Round(baseMrr * (0.7 + random() * 0.8));
            accounts.Add(new GalaxyAccount($"{name} {index / 40 + 1}", plan, region, health, mrr, random()));
        }

        return accounts;
    }
}
