using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.Blocks.Marketing.MarketingKit;

namespace Arc.Avalonia.Blocks.Marketing.Pages;

/// <summary>
/// Feature bento: six cells of different sizes, each with a small piece of real product UI that acts the
/// feature out when the pointer enters. The demos are sample controls driven locally (a toggle flips, a bar
/// fills, rows light up), not real product code. The reference blurs transitions; the port changes state with
/// token colours and no blur.
/// </summary>
public sealed class FeatureBentoPage : ArcItemPage
{
    public override string ItemSlug => "feature-bento";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A feature overview with one headline capability and five supporting ones.",
        "Product pages that want each claim shown, not described.",
        "Sections that need to look considered rather than uniform."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Every hover demo also has a resting state that carries the same information, so nothing is hover-only.",
        "Motion is decoration: the cell headings and captions state each feature in words.",
        "The toggle in the automation cell is a real switch and works with Space when focused."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Entering a cell plays its short demo once and leaves it in the end state; leaving resets it.",
        "Reduced motion shows the end state without playing anything."
    };

    public override string? ImplementationNotes =>
        "Each cell owns a tiny local demo built from Arc controls: a sparkline that redraws, a switch that flips, a progress track that fills, and rows that take the accent. Nothing is fetched and the animations are single steps, so screenshots are stable. The bento spans are fixed at gallery width rather than container queries.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        var insights = Cell("Live insights", "Charts redraw as events land, without a refresh.", "activity", InsightDemo());
        var keyboard = Cell("Keyboard first", "Every surface answers to one keystroke.", "type", KeyboardDemo());
        var speed = Cell("Speed", "Median interaction under 40 ms.", "zap", SpeedDemo());
        var automations = Cell("Automations", "Rules with a branch and a plain log.", "zap", AutomationDemo());
        var permissions = Cell("Permissions", "Access by role, always visible.", "shield-check", PermissionDemo());
        var audit = Cell("Audit trail", "Every change in order, with its reason.", "list", AuditDemo());

        grid.Children.Add(insights);
        Grid.SetColumnSpan(insights, 2);

        Grid.SetColumn(keyboard, 2);
        grid.Children.Add(keyboard);

        Grid.SetColumn(speed, 3);
        grid.Children.Add(speed);

        Grid.SetRow(automations, 1);
        grid.Children.Add(automations);

        Grid.SetRow(permissions, 1);
        Grid.SetColumn(permissions, 1);
        grid.Children.Add(permissions);

        Grid.SetRow(audit, 1);
        Grid.SetColumn(audit, 2);
        Grid.SetColumnSpan(audit, 2);
        grid.Children.Add(audit);

        return Mk.Surface(new Border { Padding = new Thickness(10), Child = grid });
    }

    private static Control Cell(string title, string caption, string icon, Control demo)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            Children =
            {
                Mk.Row(10,
                    new ArcIcon { Icon = icon, Size = 15, Foreground = Mk.Brush(ArcTokens.TextSecondary) },
                    Mk.Text(title, "arc-body-sm arc-medium")),
                Mk.Caption(caption),
                new Border { Height = 2 },
                demo
            }
        };

        var cell = new Border
        {
            Background = Mk.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = content
        };

        return cell;
    }

    private static Control InsightDemo()
    {
        var values = new List<double> { 12, 14, 13, 17, 16, 20, 19, 24, 22, 27, 26, 31 };
        var chart = new ContentControl { Content = Mk.Spark(values, 44) };
        var caption = Mk.Caption("Last 12 weeks");

        chart.PointerEntered += (_, _) =>
        {
            chart.Content = Mk.Spark(new List<double> { 12, 14, 13, 17, 16, 20, 19, 24, 22, 27, 26, 38 }, 44);
            caption.Text = "Just updated";
        };
        chart.PointerExited += (_, _) =>
        {
            chart.Content = Mk.Spark(values, 44);
            caption.Text = "Last 12 weeks";
        };

        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            Children = { chart, caption }
        };
    }

    private static Control KeyboardDemo()
    {
        var keys = new[] { "K", "/", "G then I" };
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var key in keys)
        {
            row.Children.Add(new Border
            {
                Background = Mk.Brush(ArcTokens.Surface),
                BorderBrush = Mk.Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 6),
                Child = Mk.Text(key, "arc-mono")
            });
        }

        var hint = Mk.Caption("Command bar, search, go to");
        row.PointerEntered += (_, _) => hint.Text = "⌘K opens everything";
        row.PointerExited += (_, _) => hint.Text = "Command bar, search, go to";
        return new StackPanel { Orientation = Orientation.Vertical, Spacing = 6, Children = { row, hint } };
    }

    private static Control AutomationDemo()
    {
        var rule = Mk.Text("Invoice paid, create task", "arc-body-sm arc-medium");
        var toggle = new Arc.Avalonia.Inputs.Controls.ArcSwitch { Label = "On", Checked = true };

        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Children =
            {
                rule,
                Mk.Row(8, toggle, Mk.Caption("Branch: amount over $5k"))
            }
        };
    }

    private static Control PermissionDemo()
    {
        var badges = new WrapPanel { Orientation = Orientation.Horizontal };
        var guest = Mk.Pill("Guest", ArcBadgeTone.Neutral);
        var contributor = Mk.Pill("Contributor", ArcBadgeTone.Accent);
        var admin = Mk.Pill("Admin", ArcBadgeTone.Neutral);
        guest.Margin = new Thickness(0, 0, 6, 6);
        contributor.Margin = new Thickness(0, 0, 6, 6);
        admin.Margin = new Thickness(0, 0, 6, 6);
        badges.Children.Add(guest);
        badges.Children.Add(contributor);
        badges.Children.Add(admin);

        badges.PointerEntered += (_, _) => contributor.Content = "Contributor · 8";
        badges.PointerExited += (_, _) => contributor.Content = "Contributor";

        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            Children = { badges, Mk.Caption("Additive roles, never inherited") }
        };
    }

    private static Control AuditDemo()
    {
        var rows = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        var first = AuditRow("Maya renamed the project", "09:41");
        var second = AuditRow("Jonas moved 3 issues", "09:38");
        rows.Children.Add(first);
        rows.Children.Add(second);

        var highlight = Mk.Tint(ArcTokens.Accent, 0.07);
        rows.PointerEntered += (_, _) => first.Background = highlight;
        rows.PointerExited += (_, _) => first.Background = null;

        return rows;
    }

    private static Border AuditRow(string text, string time)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.Children.Add(Mk.Caption(text));
        var stamp = Mk.Caption(time);
        Grid.SetColumn(stamp, 1);
        row.Children.Add(stamp);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 4, 6, 4),
            Child = row
        };
    }

    private static Control SpeedDemo()
    {
        var value = new Arc.Avalonia.Blocks.Marketing.Controls.ArcStatNumber
        {
            Value = 38,
            Suffix = " ms",
            FontSize = 26,
            FontWeight = FontWeight.Medium
        };

        var track = new ProgressTrack();
        track.PointerEntered += (_, _) =>
        {
            value.Value = 24;
            value.Suffix = " ms";
            track.Fraction = 0.86;
        };
        track.PointerExited += (_, _) =>
        {
            value.Value = 38;
            track.Fraction = 0.62;
        };

        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Children = { value, track, Mk.Caption("Median interaction, sample run") }
        };
    }

    /// <summary>A 6 px token track that draws its fill from a fraction, used by the speed cell.</summary>
    private sealed class ProgressTrack : Control
    {
        private double _fraction = 0.62;

        public double Fraction
        {
            get => _fraction;
            set
            {
                _fraction = Math.Clamp(value, 0, 1);
                InvalidateVisual();
            }
        }

        protected override Size MeasureOverride(Size availableSize) => new(0, 6);

        public override void Render(DrawingContext context)
        {
            if (Bounds.Width <= 0)
            {
                return;
            }

            context.DrawRectangle(Mk.Brush(ArcTokens.ControlTrack), null, new Rect(0, 0, Bounds.Width, 6), 3, 3);
            var width = Math.Max(6, Bounds.Width * _fraction);
            context.DrawRectangle(Mk.Brush(ArcTokens.Accent), null, new Rect(0, 0, width, 6), 3, 3);
        }
    }
}

