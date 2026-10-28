using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.BlocksMarketing.MarketingKit;

namespace Arc.Avalonia.BlocksMarketing.Pages;

/// <summary>
/// Comparison table: Arc against two alternatives with a highlighted column and a switch that hides the rows
/// where every option agrees. The reference also stacks the table into a phone view; the specimen stays at
/// gallery width. Values are sample copy in the page.
/// </summary>
public sealed class ComparisonTablePage : ArcItemPage
{
    public override string ItemSlug => "comparison-table";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "An us versus them page where the differences are the argument.",
        "Replacing a long feature list with a table people can scan in one pass.",
        "Launch pages that need to answer the switching question directly."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The matrix uses header and cell semantics, with the plan names as column headers.",
        "The differences switch is a real toggle and the row count is announced after it changes.",
        "Check and dash glyphs are paired with text in the accessible name, never colour alone."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Rows that only apply to some plans open and close in height as the switch changes.",
        "Reduced motion jumps to the new row set."
    };

    public override string? ImplementationNotes =>
        "Six sample rows compare Arc with two alternatives; the competition is fictional and the values are illustrative. The highlighted column is a single spanning surface under the cells, and the differences switch filters rows in code. The stacked phone layout from the reference is not reproduced at this width.";

    private sealed record Row(string Feature, string Arc, string Sheets, string Legacy, bool Same);

    private static readonly Row[] Rows =
    {
        new("Live roadmap", "check", "x", "x", false),
        new("Two-way sync", "check", "x", "check", false),
        new("Audit history", "12 months", "-", "3 months", false),
        new("SSO and SCIM", "check", "x", "check", false),
        new("Seat price", "$12", "Free", "$85", false),
        new("Time to set up", "1 day", "3 weeks", "6 weeks", false),
        new("Mobile app", "check", "check", "check", true)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var matrixHost = new ContentControl();
        var onlyDifferences = false;

        void Rebuild()
        {
            var rows = onlyDifferences ? new List<Row>(Rows).FindAll(row => !row.Same) : new List<Row>(Rows);
            var definitions = new RowDefinitions();
            definitions.Add(new RowDefinition(GridLength.Auto));
            foreach (var _ in rows)
            {
                definitions.Add(new RowDefinition(GridLength.Auto));
            }

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("1.7*,*,*,*"),
                RowDefinitions = definitions
            };

            var highlight = new Border
            {
                Background = Mk.Tint(ArcTokens.Accent, 0.07),
                BorderBrush = Mk.Tint(ArcTokens.Accent, 0.22),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };
            Grid.SetColumn(highlight, 1);
            Grid.SetRow(highlight, 0);
            Grid.SetRowSpan(highlight, rows.Count + 1);
            highlight.Margin = new Thickness(0, 0, 0, 0);
            grid.Children.Add(highlight);

            AddCell(grid, Mk.Caption("Feature"), 0, 0, HorizontalAlignment.Left);
            AddCell(grid, Header("Arc", true), 1, 0, HorizontalAlignment.Center);
            AddCell(grid, Header("Spreadsheets", false), 2, 0, HorizontalAlignment.Center);
            AddCell(grid, Header("Legacy CRM", false), 3, 0, HorizontalAlignment.Center);

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                AddCell(grid, Mk.Text(row.Feature, "arc-body-sm"), 0, index + 1, HorizontalAlignment.Left);
                AddCell(grid, Value(row.Arc, true), 1, index + 1, HorizontalAlignment.Center);
                AddCell(grid, Value(row.Sheets, false), 2, index + 1, HorizontalAlignment.Center);
                AddCell(grid, Value(row.Legacy, false), 3, index + 1, HorizontalAlignment.Center);

                if (index < rows.Count - 1)
                {
                    var line = new Border
                    {
                        Height = 1,
                        Background = Mk.Brush(ArcTokens.BorderSubtle),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(12, 0, 12, 0)
                    };
                    Grid.SetRow(line, index + 1);
                    Grid.SetColumnSpan(line, 4);
                    grid.Children.Add(line);
                }
            }

            matrixHost.Content = grid;
        }

        var toggle = new ArcSwitch { Label = "Only differences", Checked = false };
        toggle.CheckedChanged += (_, _) =>
        {
            onlyDifferences = toggle.Checked;
            Rebuild();
        };

        Rebuild();

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(Mk.Stack(4,
            Mk.Heading("Why teams switch", 420),
            Mk.Caption("Sample comparison. Alternatives are illustrative.")));
        var toggleHost = toggle;
        toggleHost.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(toggleHost, 1);
        head.Children.Add(toggleHost);

        return Mk.Surface(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 14,
            Margin = new Thickness(24, 22, 24, 18),
            Children = { head, matrixHost }
        });
    }

    private static Control Header(string name, bool accent)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (accent)
        {
            row.Children.Add(new ArcIcon { Icon = "layers", Size = 14, Foreground = Mk.Brush(ArcTokens.Accent), VerticalAlignment = VerticalAlignment.Center });
        }

        row.Children.Add(Mk.Text(name, accent ? "arc-body-sm arc-medium" : "arc-body-sm"));
        return row;
    }

    private static Control Value(string value, bool accent)
    {
        if (value == "check")
        {
            return new ArcIcon
            {
                Icon = "check",
                Size = 16,
                Foreground = accent ? Mk.Brush(ArcTokens.Accent) : Mk.Brush(ArcTokens.TextSecondary)
            };
        }

        if (value == "x")
        {
            return new ArcIcon { Icon = "minus", Size = 16, Foreground = Mk.Brush(ArcTokens.TextMuted) };
        }

        return Mk.Text(value, accent ? "arc-body-sm arc-medium" : "arc-body-sm arc-secondary");
    }

    private static void AddCell(Grid grid, Control content, int column, int row, HorizontalAlignment align)
    {
        var cell = new Border
        {
            Padding = new Thickness(12, 11, 12, 11),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        content.HorizontalAlignment = align;
        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        grid.Children.Add(cell);
    }
}
