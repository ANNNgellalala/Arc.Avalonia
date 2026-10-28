using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;
using Arc.Avalonia.Blocks.Marketing.Controls;
using Mk = Arc.Avalonia.Blocks.Marketing.MarketingKit;

namespace Arc.Avalonia.Blocks.Marketing.Pages;

/// <summary>
/// Hero section: the three landing page designs (a live dashboard rising from the bottom edge over a drifting
/// wash, a workflow that routes a sample event node by node, and editorial type over a mesh). The mesh is
/// approximated with token gradient brushes instead of a WebGL renderer; the workflow advances on a local
/// button instead of an on-screen timer, which keeps the specimen deterministic. No scroll-driven pinning and
/// no network calls.
/// </summary>
public sealed class HeroSectionPage : ArcItemPage
{
    public override string ItemSlug => "hero-section";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The first section of a SaaS landing page, when the product screen is the pitch.",
        "Developer tools, automation and infrastructure products, where showing the product working is the pitch.",
        "A launch page that wants one confident line and a sense of atmosphere."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each design has one heading, and every call to action is a real button with visible text.",
        "The dashboard is a labelled group of sample data, and its range control is a segmented control.",
        "Workflow steps report state in words (done, running, waiting), never by colour alone."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The dashboard runs off the bottom edge into the fade, and workflow steps turn to the accent while they run.",
        "The wash drifts on a slow loop; reduced motion draws one still frame and removes every transform."
    };

    public override string? ImplementationNotes =>
        "The mesh is an approximation: a token base wash with three soft radial blooms and a slow drift that is disabled under reduced motion. There is no WebGL, no grain pass and no scroll-driven pinning. The workflow is a step stack whose states advance from a local Send test event button, and the dashboard is built from real Arc controls over sample data.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var host = new ContentControl();

        Control Dashboard() => Mk.Wash(DashboardContent(), drift: true, intensity: 0.8);
        Control Workflow() => Mk.Wash(WorkflowContent(), drift: false);
        Control Editorial() => Mk.Wash(EditorialContent(), drift: true, intensity: 0.9);

        void Show(string value)
        {
            host.Content = value switch
            {
                "workflow" => Workflow(),
                "editorial" => Editorial(),
                _ => Dashboard()
            };
        }

        var switchRow = Mk.Row(14,
            Mk.Segmented(new[] { ("screenshot", "Screenshot"), ("workflow", "Workflow"), ("mesh", "Mesh") }, "screenshot",
                Show, "Hero design"),
            Mk.Caption("Each design is one screen in the reference; the specimen crops it to gallery height."));

        Show("screenshot");

        return Mk.Stack(14, switchRow, host);
    }

    private static Control DashboardContent()
    {
        var title = Mk.Text("Revenue you can explain.", "arc-title-sm", 560);
        title.TextAlignment = TextAlignment.Center;
        title.HorizontalAlignment = HorizontalAlignment.Center;

        var body = Mk.Body("Every number in Lumen traces back to an event, a customer and a decision. No spreadsheet archaeology.", 470);
        body.TextAlignment = TextAlignment.Center;
        body.HorizontalAlignment = HorizontalAlignment.Center;

        var actions = Mk.Row(10,
            Mk.Btn("Start free trial"),
            Mk.Btn("Book a demo", ArcButtonVariant.Secondary));
        actions.HorizontalAlignment = HorizontalAlignment.Center;

        var copy = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            Children =
            {
                new ArcBadge { Content = "Arc 3.0 is out", Tone = ArcBadgeTone.Accent, HorizontalAlignment = HorizontalAlignment.Center },
                title,
                body,
                actions
            }
        };

        var ranges = Mk.Row(4,
            Mk.Btn("7d", ArcButtonVariant.Ghost, ArcButtonSize.Sm),
            Mk.Btn("30d", ArcButtonVariant.Secondary, ArcButtonSize.Sm),
            Mk.Btn("90d", ArcButtonVariant.Ghost, ArcButtonSize.Sm));

        var cardHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        cardHeader.Children.Add(Mk.Row(10,
            Mk.Logo("Lumen", "chart-line", 24, 14),
            Mk.Caption("Revenue overview"),
            Mk.Avatar("Maya Chen", ArcAvatarSize.Xs),
            Mk.Avatar("Jonas Weber", ArcAvatarSize.Xs)));
        Grid.SetColumn(ranges, 2);
        cardHeader.Children.Add(ranges);

        var figure = Mk.Row(12,
            new ArcStatNumber
            {
                Value = 23480,
                Prefix = "$",
                Classes = { "arc-title-sm" }
            },
            Mk.Pill("+18.2%", ArcBadgeTone.Success));

        var chart = Mk.Spark(
            new List<double> { 18, 22, 19, 26, 24, 31, 29, 34, 32, 38, 36, 43, 41, 47, 44, 52, 50, 57, 55, 62 },
            54);

        var card = new Border
        {
            Background = Mk.Brush(ArcTokens.Surface),
            BorderBrush = Mk.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(20, 16, 20, 18),
            Margin = new Thickness(34, 22, 34, 0),
            Child = Mk.Stack(14,
                cardHeader,
                figure,
                chart)
        };

        var stage = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            Margin = new Thickness(0, 30, 0, 0),
            Children = { copy, card }
        };

        return new Grid { Height = 392, Children = { stage } };
    }

    private static Control WorkflowContent()
    {
        var title = Mk.Text("Every event lands where it should.", "arc-subtitle", 330);
        var body = Mk.Body("Relay watches your systems, decides at the branch, and writes the result back where the team already looks.", 320);

        var actions = Mk.Row(10,
            Mk.Btn("Start building"),
            Mk.Btn("Read the docs", ArcButtonVariant.Secondary));

        var cover = Mk.Caption("Stripe invoice paid · Acme, $6,400");

        var copy = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { title, body, actions, cover }
        };

        var steps = new[]
        {
            ("Trigger", "Stripe invoice paid", "0.2s"),
            ("Lookup", "HubSpot company", "0.4s"),
            ("Branch", "Amount over $5,000", "0.1s"),
            ("Action", "Post to #revenue", "0.3s"),
            ("Action", "Warehouse sync", "0.6s")
        };

        var statuses = new List<ArcBadge>();
        var times = new List<TextBlock>();
        var stepHost = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };

        for (var index = 0; index < steps.Length; index++)
        {
            var position = index;
            var badge = new ArcBadge { Content = "Waiting", Tone = ArcBadgeTone.Neutral, VerticalAlignment = VerticalAlignment.Center };
            var time = Mk.Caption(steps[index].Item3);
            statuses.Add(badge);
            times.Add(time);

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
            row.Children.Add(Mk.Disc(steps[index].Item1 == "Branch" ? "move-3d" : "zap", 26, 13));

            var text = Mk.Stack(1,
                Mk.Text(steps[index].Item2, "arc-body-sm arc-medium"),
                Mk.Caption(steps[index].Item1));
            text.Margin = new Thickness(10, 0, 10, 0);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            Grid.SetColumn(badge, 2);
            row.Children.Add(badge);

            time.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(time, 3);
            row.Children.Add(time);

            var cell = new Border { Padding = new Thickness(6, 6, 6, 6), Child = row };
            stepHost.Children.Add(cell);

            if (index < steps.Length - 1)
            {
                stepHost.Children.Add(new Border
                {
                    Width = 1,
                    Height = 10,
                    Background = Mk.Brush(ArcTokens.BorderSubtle),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(19, 0, 0, 0)
                });
            }
        }

        var run = 1;
        void ApplyRun()
        {
            for (var index = 0; index < statuses.Count; index++)
            {
                if (index < run)
                {
                    statuses[index].Content = "Done";
                    statuses[index].Tone = ArcBadgeTone.Success;
                }
                else if (index == run)
                {
                    statuses[index].Content = "Running";
                    statuses[index].Tone = ArcBadgeTone.Accent;
                }
                else
                {
                    statuses[index].Content = "Waiting";
                    statuses[index].Tone = ArcBadgeTone.Neutral;
                }
            }
        }

        var graph = new Border
        {
            Background = Mk.Tint(ArcTokens.Surface, 0),
            BorderBrush = Mk.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(14, 10, 14, 12),
            Child = Mk.Stack(8,
                Mk.Row(8,
                    new ArcIcon { Icon = "play", Size = 13, Foreground = Mk.Brush(ArcTokens.Accent) },
                    Mk.Text("Run #2,418", "arc-caption arc-medium"),
                    new Border { HorizontalAlignment = HorizontalAlignment.Right },
                    Mk.Btn("Send test event", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
                    {
                        run = run >= steps.Length ? 1 : run + 1;
                        ApplyRun();
                    })),
                Mk.Divider(),
                stepHost)
        };

        ApplyRun();

        var grid = new Grid { Height = 392,
            ColumnDefinitions = new ColumnDefinitions("*,1.35*"),
            Margin = new Thickness(30, 34, 30, 0)
        };
        grid.Children.Add(new Border { Child = copy, Padding = new Thickness(0, 20, 0, 0) });
        Grid.SetColumn(graph, 1);
        graph.Margin = new Thickness(18, 0, 0, 0);
        grid.Children.Add(graph);
        return grid;
    }

    private static Control EditorialContent()
    {
        var title = Mk.Text("Ship less.\nShip better.", "arc-title", 620);
        title.TextAlignment = TextAlignment.Left;

        var body = Mk.Text("Cadence is the release notebook for teams who would rather finish three things than start twelve. Notes, decisions and a ship date you can keep.",
            "arc-body-lg arc-secondary", 480);

        var actions = Mk.Row(10,
            Mk.Btn("Download for Mac", ArcButtonVariant.Primary, ArcButtonSize.Lg, icon: "download"),
            Mk.Btn("What's new", ArcButtonVariant.Ghost, ArcButtonSize.Lg));

        var marks = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
        var names = new[] { "Northwind", "Cobalt", "Fieldkit", "Meridian" };
        for (var index = 0; index < names.Length; index++)
        {
            var mark = new TextBlock
            {
                Text = names[index],
                FontSize = 15,
                FontWeight = FontWeight.Medium,
                Opacity = 0.55,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(mark, index);
            marks.Children.Add(mark);
        }

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 18,
            Margin = new Thickness(40, 40, 40, 30),
            Children = { title, body, actions }
        };

        var root = new Grid { Height = 392 };
        root.Children.Add(stack);

        var marksRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        marksRow.Children.Add(Mk.Caption("Sample customer marks"));
        Grid.SetColumn(marks, 1);
        marks.Margin = new Thickness(24, 0, 0, 0);
        marksRow.Children.Add(marks);

        var footer = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Padding = new Thickness(40, 16, 40, 22),
            Child = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 12,
                Children = { Mk.Divider(), marksRow }
            }
        };

        root.Children.Add(footer);
        return root;
    }
}


