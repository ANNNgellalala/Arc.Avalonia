using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Arc.Avalonia.Blocks.Marketing.Controls;
using Mk = Arc.Avalonia.Blocks.Marketing.MarketingKit;

namespace Arc.Avalonia.Blocks.Marketing.Pages;

/// <summary>
/// Plan comparison: two tiers and an enterprise column, a monthly or yearly switch, prices that roll, and a
/// feature matrix that can hide the rows where plans agree. The selected plan is highlighted in one spanning
/// surface; clicking a plan header selects it. Sample prices and features in the page.
/// </summary>
public sealed class PlanComparisonPage : ArcItemPage
{
    public override string ItemSlug => "plan-comparison";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Comparing two or three tiers side by side with a feature matrix.",
        "Pricing pages that switch between monthly and yearly prices.",
        "Long feature lists where people want to see only the differences."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use the pricing calculator when price depends on seats or volume.",
        "Use a billing toggle alone when there is only one plan to price."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The matrix reads as a table with plan names as column headers and one row header per feature.",
        "Prices are announced as a whole value after the billing switch changes.",
        "The chosen plan is written in the footer status line, not only highlighted."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Prices roll between billing periods and the selection highlight moves to the chosen plan.",
        "Rows that differ open and close in height; reduced motion jumps to the final values."
    };

    public override string? ImplementationNotes =>
        "Plans, prices and the matrix are sample data in the page. Prices roll with the local number control, which lands exactly on the value; the highlight is one spanning surface under the selected column. The stacked narrow layout from the reference is not reproduced at gallery width.";

    private sealed record Plan(string Name, string Monthly, string Yearly, string Note, string Cta);

    private static readonly Plan[] Plans =
    {
        new("Starter", "0", "0", "Free forever, one project", "Start free"),
        new("Team", "18", "14", "Per seat, billed monthly", "Start free"),
        new("Business", "34", "27", "Per seat, billed monthly", "Start free")
    };

    private sealed record Feature(string Name, string Starter, string Team, string Business);

    private static readonly Feature[] Features =
    {
        new("Projects", "1", "Unlimited", "Unlimited"),
        new("Audit history", "-", "12 months", "36 months"),
        new("SSO and SCIM", "-", "Included", "Included"),
        new("API access", "Read only", "Full", "Full"),
        new("Support", "Community", "Email", "Priority"),
        new("Custom fields", "-", "check", "check"),
        new("Data region", "US", "US or EU", "US or EU")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var yearly = false;
        var onlyDifferences = false;
        var selected = 1;
        var matrixHost = new ContentControl();
        var status = Mk.Caption("Team selected");

        void Rebuild()
        {
            var features = onlyDifferences
                ? Features.Where(feature => feature.Starter != feature.Team || feature.Team != feature.Business).ToList()
                : new List<Feature>(Features);

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("1.5*,*,*,*"),
                RowDefinitions = RowDefinitionsFor(features.Count + 1)
            };

            var highlight = new Border
            {
                Background = Mk.Tint(ArcTokens.Accent, 0.06),
                BorderBrush = Mk.Tint(ArcTokens.Accent, 0.22),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14)
            };
            Grid.SetColumn(highlight, selected + 1);
            Grid.SetColumnSpan(highlight, 1);
            Grid.SetRowSpan(highlight, features.Count + 1);
            grid.Children.Add(highlight);

            AddCell(grid, 0, 0, Mk.Caption("Plan"));
            for (var index = 0; index < Plans.Length; index++)
            {
                var plan = Plans[index];
                var position = index;
                var price = yearly ? plan.Yearly : plan.Monthly;
                var header = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 4,
                    Children =
                    {
                        Mk.Text(plan.Name, "arc-body-sm arc-medium"),
                        Mk.Row(4,
                            new ArcStatNumber { Value = double.Parse(price), Prefix = "$", FontSize = 22, FontWeight = FontWeight.Medium },
                            Mk.Caption(yearly ? "/ seat / mo, yearly" : "/ seat / mo")),
                        Mk.Caption(index == 0 ? "Free forever" : plan.Note.Replace("billed monthly", yearly ? "billed yearly" : "billed monthly"))
                    }
                };

                var button = new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Padding = new Thickness(6),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Top,
                    Content = header
                };
                button.Click += (_, _) =>
                {
                    selected = position;
                    status.Text = $"{Plans[selected].Name} selected";
                    Rebuild();
                };
                AddCell(grid, position + 1, 0, button);
            }

            for (var row = 0; row < features.Count; row++)
            {
                var feature = features[row];
                AddCell(grid, 0, row + 1, Mk.Text(feature.Name, "arc-body-sm"));
                AddCell(grid, 1, row + 1, ValueCell(feature.Starter));
                AddCell(grid, 2, row + 1, ValueCell(feature.Team));
                AddCell(grid, 3, row + 1, ValueCell(feature.Business));

                if (row < features.Count - 1)
                {
                    var line = new Border
                    {
                        Height = 1,
                        Background = Mk.Brush(ArcTokens.BorderSubtle),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(12, 0, 12, 0)
                    };
                    Grid.SetRow(line, row + 1);
                    Grid.SetColumnSpan(line, 4);
                    grid.Children.Add(line);
                }
            }

            matrixHost.Content = grid;
        }

        Rebuild();

        var billing = Mk.Segmented(
            new[] { ("monthly", "Monthly"), ("yearly", "Yearly, save 22%") },
            "monthly",
            value =>
            {
                yearly = value == "yearly";
                Rebuild();
            },
            "Billing period");

        var toggle = new ArcSwitch { Label = "Only differences", Checked = false };
        toggle.CheckedChanged += (_, _) =>
        {
            onlyDifferences = toggle.Checked;
            Rebuild();
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        header.Children.Add(Mk.Stack(3,
            Mk.Heading("Compare plans", 420),
            Mk.Caption("Sample pricing. Seat counts are illustrative.")));
        Grid.SetColumn(billing, 1);
        billing.VerticalAlignment = VerticalAlignment.Center;
        billing.Margin = new Thickness(16, 0, 18, 0);
        header.Children.Add(billing);
        Grid.SetColumn(toggle, 2);
        toggle.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(toggle);

        return Mk.Surface(new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 12,
            Margin = new Thickness(24, 20, 24, 18),
            Children = { header, matrixHost, status }
        });
    }

    private static RowDefinitions RowDefinitionsFor(int count)
    {
        var definitions = new RowDefinitions();
        for (var index = 0; index < count; index++)
        {
            definitions.Add(new RowDefinition(GridLength.Auto));
        }

        return definitions;
    }

    private static Control ValueCell(string value)
    {
        if (value == "check")
        {
            return new ArcIcon { Icon = "check", Size = 15, Foreground = Mk.Brush(ArcTokens.Accent) };
        }

        if (value == "-")
        {
            return new ArcIcon { Icon = "minus", Size = 15, Foreground = Mk.Brush(ArcTokens.TextMuted) };
        }

        return Mk.Text(value, "arc-body-sm arc-secondary");
    }

    private static void AddCell(Grid grid, int column, int row, Control content)
    {
        var cell = new Border
        {
            Padding = new Thickness(12, 9, 12, 9),
            Child = content
        };

        if (column == 0)
        {
            content.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            content.HorizontalAlignment = HorizontalAlignment.Center;
        }

        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        grid.Children.Add(cell);
    }
}

