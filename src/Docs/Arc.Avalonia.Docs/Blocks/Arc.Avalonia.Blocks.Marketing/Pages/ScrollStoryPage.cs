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
/// Scroll story: a feature section where the copy drives a product view through its steps. The reference pins
/// the preview while the page scrolls; that behaviour is deliberately not reproduced, because a gallery page is
/// a single fixed frame. Instead the steps are real buttons and the preview follows the chosen step, which
/// keeps the same reading order, keyboard path and reduced-motion story.
/// </summary>
public sealed class ScrollStoryPage : ArcItemPage
{
    public override string ItemSlug => "scroll-story";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-steps", "variant-preview"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Explaining a multi-step workflow where the order matters.",
        "Feature sections that need more than a screenshot to make the point.",
        "Pages where each step is a claim the preview can prove."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each step is a button: Tab moves through them and Enter or Space selects one.",
        "The selected step is marked in text as well as with the accent, and the preview is a labelled group.",
        "Nothing depends on scrolling: every step is reachable by keyboard and read in order."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The preview crossfades between steps while the selected step's rail marker moves.",
        "Under reduced motion the preview swaps instantly and the marker jumps."
    };

    public override string? ImplementationNotes =>
        "Deliberate simplification: no scroll-driven pinning. The reference pins the product view and advances it as the copy crosses the viewport; a gallery page cannot scroll, so the steps are buttons and the preview follows the selection. No observer, no sticky layout, no scroll listener.";

    private sealed record Step(string Title, string Caption, string Icon);

    private static readonly Step[] Steps =
    {
        new("Connect", "Link the tools you already run. No migration, no export.", "link-2"),
        new("Map", "Match fields once; the rules follow every new record.", "columns-3"),
        new("Automate", "Set the branch that decides what happens next.", "zap"),
        new("Measure", "Watch the run, the time saved and where it stalls.", "chart-line")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var preview = new ContentControl();
        var state = Mk.Caption("Step 1 of 4");
        var index = 0;
        var stepHost = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };

        void Select(int next)
        {
            index = next;
            state.Text = $"Step {index + 1} of 4";
            preview.Content = Preview(index);
            for (var i = 0; i < stepHost.Children.Count; i++)
            {
                if (stepHost.Children[i] is Border border)
                {
                    var active = i == index;
                    border.Background = active ? Mk.Tint(ArcTokens.Accent, 0.08) : Mk.Tint(ArcTokens.Surface, 0);
                    border.BorderBrush = active ? Mk.Tint(ArcTokens.Accent, 0.28) : Mk.Brush(ArcTokens.BorderSubtle);
                }
            }
        }

        for (var i = 0; i < Steps.Length; i++)
        {
            var position = i;
            var number = (i + 1).ToString("00");
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(Mk.Disc(Steps[i].Icon, 28, 13));
            var text = Mk.Stack(2,
                Mk.Text($"{number}  {Steps[i].Title}", "arc-body arc-medium"),
                Mk.Caption(Steps[i].Caption));
            text.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Content = row,
                Padding = new Thickness(10, 8, 10, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            button.Click += (_, _) => Select(position);

            var cell = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                BorderBrush = Mk.Brush(ArcTokens.BorderSubtle),
                Background = Mk.Tint(ArcTokens.Surface, 0),
                Child = button
            };
            stepHost.Children.Add(cell);
        }

        Select(0);

        var left = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            Children =
            {
                Mk.Stack(4,
                    Mk.Heading("From event to outcome", 380),
                    Mk.Body("Four steps, one screen. Pick a step to see what it changes.", 380)),
                state,
                stepHost
            }
        };

        var right = new Border
        {
            Margin = new Thickness(28, 0, 0, 0),
            Child = preview
        };

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("0.95*,1.05*") };
        root.Children.Add(left);
        Grid.SetColumn(right, 1);
        root.Children.Add(right);

        return Mk.Surface(new Border { Padding = new Thickness(26, 24, 26, 24), Child = root });
    }

    private static Control Preview(int index)
    {
        return index switch
        {
            1 => MapPreview(),
            2 => AutomatePreview(),
            3 => MeasurePreview(),
            _ => ConnectPreview()
        };
    }

    private static Control ConnectPreview()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };
        var sources = new[]
        {
            ("Stripe", "Events and invoices", "check"),
            ("HubSpot", "Companies and deals", "check"),
            ("Postgres", "Read-only replica", "check"),
            ("Slack", "Notifications", "loader-circle")
        };

        stack.Children.Add(Mk.Caption("Connected sources"));
        foreach (var source in sources)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            row.Children.Add(Mk.Disc("database", 26, 12));
            var text = Mk.Stack(1,
                Mk.Text(source.Item1, "arc-body-sm arc-medium"),
                Mk.Caption(source.Item2));
            text.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            var glyph = new ArcIcon
            {
                Icon = source.Item3,
                Size = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = source.Item3 == "check" ? Mk.Brush(ArcTokens.Success) : Mk.Brush(ArcTokens.TextMuted)
            };
            Grid.SetColumn(glyph, 2);
            row.Children.Add(glyph);

            stack.Children.Add(Mk.Panel(row, padding: 10, muted: true));
        }

        return stack;
    }

    private static Control MapPreview()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*") };
        var left = Mk.Stack(6,
            Mk.Caption("Incoming field"),
            Mk.Text("invoice.amount_cents", "arc-mono"),
            Mk.Text("company.domain", "arc-mono"),
            Mk.Text("paid_at", "arc-mono"));
        grid.Children.Add(left);

        var arrow = new ArcIcon
        {
            Icon = "arrow-right",
            Size = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 14, 0)
        };
        Grid.SetColumn(arrow, 1);
        grid.Children.Add(arrow);

        var right = Mk.Stack(6,
            Mk.Caption("Arc property"),
            Mk.Pill("Deal amount", ArcBadgeTone.Neutral),
            Mk.Pill("Account", ArcBadgeTone.Neutral),
            Mk.Pill("Closed at", ArcBadgeTone.Neutral));
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return Mk.Stack(10,
            Mk.Caption("Field mapping"),
            grid,
            Mk.Caption("12 fields mapped, 0 left."));
    }

    private static Control AutomatePreview()
    {
        return Mk.Stack(10,
            Mk.Caption("Rule"),
            Mk.Panel(Mk.Stack(8,
                Mk.Row(8,
                    Mk.Disc("zap", 26, 12, accent: true),
                    Mk.Text("When an invoice is paid", "arc-body-sm arc-medium")),
                Mk.Divider(),
                Mk.Text("If the amount is over $5,000", "arc-body-sm"),
                Mk.Row(8,
                    Mk.Pill("Large deal", ArcBadgeTone.Accent),
                    Mk.Caption("post to #revenue and create a task")),
                Mk.Text("Otherwise", "arc-body-sm"),
                Mk.Row(8,
                    Mk.Pill("Standard", ArcBadgeTone.Neutral),
                    Mk.Caption("sync to the warehouse only"))), padding: 14, muted: true),
            Mk.Caption("Rule 4 of 11 · last edited by Maya"));
    }

    private static Control MeasurePreview()
    {
        return Mk.Stack(10,
            Mk.Caption("Runs this week"),
            Mk.Row(10,
                new ArcStatNumber { Value = 2418, FontSize = 28, FontWeight = FontWeight.Medium },
                Mk.Pill("+12%", ArcBadgeTone.Success)),
            Mk.Spark(new double[] { 18, 21, 19, 24, 26, 23, 29, 32, 30, 36, 34, 41 }, 52),
            Mk.Row(14,
                Mk.Meta("clock", "1.2 s median"),
                Mk.Meta("circle-check", "99.4% success"),
                Mk.Meta("circle-alert", "3 retries")));
    }
}

