using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Data.Pages;

/// <summary>
/// Semantic zoom: an infinite-zoom map of a company drawn as nested tiles, with breadcrumbs and a side panel
/// that follows the level. Teams, projects and tasks are sample data. An independent Avalonia implementation
/// of Arc's public semantic-zoom block; no Pro source was used.
/// </summary>
public sealed class SemanticZoomPage : ArcItemPage
{
    public override string ItemSlug => "semantic-zoom";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-zoom", "variant-tiles", "variant-breadcrumb", "variant-level-panel"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Workspace or portfolio overviews where people zoom from the whole company to one task.",
        "Hierarchical data where each zoom level should show different detail."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use tree-view for a plain hierarchy people scan and expand.",
        "Use customer-galaxy for flat data clustered by attributes."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The map is one focusable application region: arrows move the cursor, Enter flies in, Escape or Backspace flies out, and Home fits the company.",
        "Breadcrumbs are navigation buttons with the current level marked, and the side panel repeats the level's totals as text."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The camera flies between levels and detail fades in as a level gets large enough; reduced motion replaces the flight with an instant level swap."
    };

    public override string? ImplementationNotes =>
        "Levels are drawn as nested flat tiles rather than an infinitely scalable canvas: the camera snaps between four levels instead of easing through a continuous zoom, and there is no blur or depth of field. Breadcrumbs, the side panel and the person avatars are real controls.";

    private static readonly string[] People =
    {
        "Mara Chen", "Ivo Petrov", "Ada Okonkwo", "Ren Takahashi", "Lena Fischer", "Sam Ortiz"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var root = BuildTree();
        var mapHost = new ContentControl();
        var breadcrumbHost = new ContentControl();
        var panelHost = new ContentControl();
        var status = DataKit.Status();
        var map = new ArcSemanticMap(root);

        void RefreshChrome()
        {
            var crumbs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            var path = map.Path;
            for (var depth = 0; depth < path.Count; depth++)
            {
                var atDepth = depth;
                crumbs.Children.Add(DataKit.Chip(path[depth].Name, depth == path.Count - 1, () =>
                {
                    map.GoToDepth(atDepth);
                }));
            }

            breadcrumbHost.Content = crumbs;

            var current = map.Current;
            var children = current.Children;
            var rows = new StackPanel { Spacing = 4 };
            foreach (var child in children.OrderByDescending(child => child.Rollup).Take(5))
            {
                rows.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 8,
                    Height = 24,
                    Children =
                    {
                        DataKit.Text(child.Name, "arc-body-sm"),
                        DataKit.AtColumn(DataKit.Text(DataKit.Compact(child.Rollup), "arc-body-sm arc-medium"), 1)
                    }
                });
            }

            var avatars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = -8 };
            for (var index = 0; index < 5; index++)
            {
                avatars.Children.Add(new ArcAvatar
                {
                    DisplayName = People[(current.Name.Length + index) % People.Length],
                    Size = ArcAvatarSize.Sm
                });
            }

            panelHost.Content = new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    DataKit.Pill(current.Kind, ArcBadgeTone.Neutral),
                    DataKit.Text(current.Name, "arc-subtitle"),
                    DataKit.Caption($"{DataKit.Compact(current.Rollup)} points · {children.Count} {(children.Count == 1 ? "child" : "children")}"),
                    DataKit.Divider(),
                    DataKit.Label("Largest " + (children.Count > 0 ? children[0].Kind.ToLowerInvariant() + "s" : "items")),
                    rows,
                    DataKit.Divider(),
                    DataKit.Label("People"),
                    avatars,
                    DataKit.Link("Back to the company", () => map.Reset(), "corner-up-left")
                }
            };
        }

        map.Navigated += (_, _) =>
        {
            RefreshChrome();
            DataKit.Say(status, $"Flew to {map.Current.Name} ({map.Current.Kind.ToLowerInvariant()}).");
        };
        map.SelectionChanged += (_, node) =>
        {
            if (node is not null)
            {
                DataKit.Say(status, $"{node.Name} · {node.Kind.ToLowerInvariant()} · {DataKit.Compact(node.Rollup)} points.");
            }
        };

        mapHost.Content = map;
        RefreshChrome();

        var header = DataKit.Header("Acme company map",
            "Teams, projects and tasks · 5 teams · 15 projects",
            DataKit.Row(8, DataKit.Meta("mouse-pointer-click", "Double-click a tile to fly in")));

        var mapCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                breadcrumbHost,
                mapHost,
                DataKit.Caption("Enter flies into the selected tile · Escape flies out · Home fits the company")
            }
        });

        var panelCard = DataKit.Card(new StackPanel
        {
            Spacing = 8,
            Children = { DataKit.Label("This level"), panelHost }
        }, width: 268);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,268"),
            Children = { mapCard, DataKit.AtColumn(panelCard, 2) }
        };

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, body, status }
        }, 16);
    }

    private static MapNode BuildTree()
    {
        static MapNode Task(string name, double points) => new(name, "Task", points);

        static MapNode Project(string name, params MapNode[] tasks) => new(name, "Project", 0, tasks);

        static MapNode Team(string name, params MapNode[] projects) => new(name, "Team", 0, projects);

        var teams = new[]
        {
            Team("Product",
                Project("Editor", Task("Slash menu", 8), Task("Collaborative cursors", 13), Task("Version history", 5)),
                Project("Insights", Task("Funnel builder", 13), Task("Cohort compare", 8), Task("Saved views", 3)),
                Project("Mobile", Task("Offline drafts", 8), Task("Push digest", 5))),
            Team("Engineering",
                Project("Platform", Task("Event pipeline", 21), Task("Rate limits", 8), Task("Audit log", 5)),
                Project("Data", Task("Warehouse sync", 13), Task("Backfill tool", 8)),
                Project("Infra", Task("Blue green deploys", 13), Task("Cost guardrails", 5))),
            Team("Design",
                Project("System", Task("Token audit", 8), Task("Dark theme pass", 5)),
                Project("Brand", Task("Launch kit", 8), Task("Illustration set", 13))),
            Team("Growth",
                Project("Lifecycle", Task("Trial nudges", 8), Task("Winback", 5)),
                Project("SEO", Task("Comparison pages", 13), Task("Docs revamp", 8))),
            Team("Support",
                Project("Help center", Task("Search rewrite", 8), Task("Article feedback", 3)),
                Project("Bots", Task("Triage assistant", 13)))
        };

        return new MapNode("Acme Inc", "Company", 0, teams);
    }
}
