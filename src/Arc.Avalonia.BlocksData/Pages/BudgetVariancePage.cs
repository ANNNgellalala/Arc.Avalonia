using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksData.Pages;

/// <summary>
/// Budget variance: budget against actual operating spend by department as aligned bullet bars, with a month,
/// quarter and year to date switch, in-place drill-down into budget lines and a total row. Sample figures
/// only. An independent Avalonia implementation of Arc's public budget-variance block; no Pro source was
/// used.
/// </summary>
public sealed class BudgetVariancePage : ArcItemPage
{
    public override string ItemSlug => "budget-variance";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Finance or operations reviews that compare actual spend with an approved budget by department.",
        "A page where readers must see favourable and unfavourable variance at a glance and then open the lines."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a simple bar chart when there is no budget baseline to compare against.",
        "Use a full data grid when people edit the numbers rather than scan them."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The period switch is a segmented control; each row expands its budget lines with Enter or Space.",
        "Variance is written as favourable or unfavourable plus an amount, and the total row repeats it."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Bars and figures are recalculated per period; the reference springs every bar, while this port swaps them on one short fade.",
        "Reduced motion removes the fade."
    };

    public override string? ImplementationNotes =>
        "Budgets, actuals and the three lines per department are seeded. The bullet is two stacked borders rather than SVG; drill-down expands in place and there is no export.";

    private sealed record Line(string Name, double Budget, double Actual);

    private sealed record Department(string Name, double Budget, double Actual, Line[] Lines);

    private static readonly (string Key, string Label, double Factor, string Caption)[] Periods =
    {
        ("mtd", "Month to date", 1.0, "1–26 November"),
        ("qtd", "Quarter to date", 2.86, "October – November"),
        ("ytd", "Year to date", 8.4, "January – November")
    };

    private static readonly (string Name, double Budget, double Factor)[] BaseDepartments =
    {
        ("Engineering", 642_000, 1.07),
        ("Sales", 308_000, 0.94),
        ("Marketing", 184_000, 1.16),
        ("Support", 121_000, 0.98),
        ("Operations", 96_000, 0.88),
        ("People", 112_000, 1.02)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var period = "mtd";
        var expanded = "Marketing";
        var rowsHost = new ContentControl();
        var statsHost = new ContentControl();
        var caption = DataKit.Caption(string.Empty);
        var status = DataKit.Status();

        List<Department> Build()
        {
            var factor = Periods.First(item => item.Key == period).Factor;
            var random = DataKit.Random(period.Length * 37 + 11);
            return BaseDepartments.Select((department, index) =>
            {
                var budget = Math.Round(department.Budget * factor / 100) * 100;
                var wobble = 1 + (random() - 0.5) * 0.04;
                var actual = Math.Round(budget * department.Factor * wobble / 100) * 100;
                var lines = new[]
                {
                    new Line("Salaries and benefits", Math.Round(budget * 0.62), Math.Round(actual * 0.64)),
                    new Line("Software and tools", Math.Round(budget * 0.23), Math.Round(actual * 0.20)),
                    new Line("Travel and events", Math.Round(budget * 0.15), Math.Round(actual * 0.16))
                };
                return new Department(department.Name, budget, actual, lines);
            }).ToList();
        }

        void Refresh()
        {
            var departments = Build();
            var budgetTotal = departments.Sum(item => item.Budget);
            var actualTotal = departments.Sum(item => item.Actual);
            var variance = actualTotal - budgetTotal;

            var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*,12,*") };
            var tiles = new (string Label, string Value, string Note)[]
            {
                ("Budget", DataKit.Money(budgetTotal), Periods.First(item => item.Key == period).Caption),
                ("Actual", DataKit.Money(actualTotal), $"{DataKit.Percent(actualTotal / budgetTotal, 1)} of budget"),
                ("Variance", DataKit.Signed(variance), variance > 0 ? "unfavourable" : "favourable"),
                ("Largest overrun", departments.OrderByDescending(item => item.Actual - item.Budget).First().Name,
                    DataKit.Signed(departments.Max(item => item.Actual - item.Budget)))
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

            var table = new StackPanel { Spacing = 0 };
            table.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("156,*,92,92,100,64"),
                ColumnSpacing = 10,
                Height = 22,
                Children =
                {
                    DataKit.Caption("Department"),
                    DataKit.AtColumn(DataKit.Caption("Budget vs actual"), 1),
                    DataKit.AtColumn(DataKit.Caption("Budget"), 2),
                    DataKit.AtColumn(DataKit.Caption("Actual"), 3),
                    DataKit.AtColumn(DataKit.Caption("Variance"), 4)
                }
            });
            table.Children.Add(DataKit.Divider());

            foreach (var department in departments)
            {
                var isExpanded = department.Name == expanded;
                var over = department.Actual > department.Budget;
                var tone = over ? ArcTokens.Danger : ArcTokens.Success;
                var bar = Bullet(department, tone);
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("156,*,92,92,100,64"),
                    ColumnSpacing = 10,
                    Height = 30,
                    Children =
                    {
                        DataKit.Row(6,
                            new ArcIcon
                            {
                                Icon = isExpanded ? "chevron-down" : "chevron-right",
                                Size = 13,
                                Foreground = DataKit.Brush(ArcTokens.TextMuted)
                            },
                            DataKit.Text(department.Name, "arc-body-sm")),
                        DataKit.AtColumn(bar, 1),
                        DataKit.AtColumn(DataKit.Text(DataKit.Money(department.Budget), "arc-body-sm"), 2),
                        DataKit.AtColumn(DataKit.Text(DataKit.Money(department.Actual), "arc-body-sm arc-medium"), 3),
                        DataKit.AtColumn(DataKit.Text(DataKit.Signed(department.Actual - department.Budget), "arc-body-sm"),
                            4),
                        DataKit.AtColumn(DataKit.Pill(over ? "Over" : "Under",
                            over ? ArcBadgeTone.Danger : ArcBadgeTone.Success, true), 5)
                    }
                };
                var button = new Border
                {
                    Child = row,
                    Padding = new Thickness(0, 1),
                    Background = isExpanded ? DataKit.Brush(ArcTokens.SurfaceMuted) : null,
                    CornerRadius = new CornerRadius(8),
                    Focusable = true
                };
                button.PointerPressed += (_, args) =>
                {
                    expanded = isExpanded ? string.Empty : department.Name;
                    Refresh();
                    DataKit.Say(status, isExpanded ? "Drill-down closed." : $"{department.Name} budget lines opened.");
                    args.Handled = true;
                };
                button.KeyDown += (_, args) =>
                {
                    if (args.Key is Key.Enter or Key.Space)
                    {
                        expanded = isExpanded ? string.Empty : department.Name;
                        Refresh();
                        args.Handled = true;
                    }
                };
                table.Children.Add(button);

                if (isExpanded)
                {
                    foreach (var line in department.Lines)
                    {
                        table.Children.Add(new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("156,*,92,92,100,64"),
                            ColumnSpacing = 10,
                            Height = 24,
                            Margin = new Thickness(0, 0, 0, 0),
                            Children =
                            {
                                DataKit.Text("   " + line.Name, "arc-caption"),
                                DataKit.AtColumn(Bullet(new Department(line.Name, line.Budget, line.Actual,
                                    Array.Empty<Line>()), line.Actual > line.Budget ? ArcTokens.Danger : ArcTokens.Success, 6), 1),
                                DataKit.AtColumn(DataKit.Caption(DataKit.Money(line.Budget)), 2),
                                DataKit.AtColumn(DataKit.Caption(DataKit.Money(line.Actual)), 3),
                                DataKit.AtColumn(DataKit.Caption(DataKit.Signed(line.Actual - line.Budget)), 4)
                            }
                        });
                    }
                }
            }

            table.Children.Add(DataKit.Divider(4));
            table.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("156,*,92,92,100,64"),
                ColumnSpacing = 10,
                Height = 30,
                Children =
                {
                    DataKit.Strong("Total operating spend"),
                    DataKit.AtColumn(Bullet(new Department("Total", budgetTotal, actualTotal, Array.Empty<Line>()),
                        variance > 0 ? ArcTokens.Danger : ArcTokens.Success), 1),
                    DataKit.AtColumn(DataKit.Strong(DataKit.Money(budgetTotal)), 2),
                    DataKit.AtColumn(DataKit.Strong(DataKit.Money(actualTotal)), 3),
                    DataKit.AtColumn(DataKit.Strong(DataKit.Signed(variance)), 4)
                }
            });

            rowsHost.Content = DataKit.Card(table);

            var overCount = departments.Count(item => item.Actual > item.Budget);
            caption.Text = $"{Periods.First(item => item.Key == period).Label} · {overCount} of {departments.Count} departments over budget";
        }

        var periodSelector = DataKit.Segmented(Periods.Select(item => (item.Key, item.Label)), period, key =>
        {
            period = key;
            Refresh();
            DataKit.Say(status, $"Period set to {Periods.First(item => item.Key == key).Label}.");
        });

        Refresh();

        var header = DataKit.Header("Budget vs actual", "Operating spend · FY 2026 · seed data",
            DataKit.Row(8, periodSelector, DataKit.Pill("Approved", ArcBadgeTone.Neutral)));

        return DataKit.Specimen(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                header,
                statsHost,
                rowsHost,
                DataKit.Caption("Select a department to open its three budget lines · variance is actual minus budget"),
                status
            }
        }, 16);
    }

    /// <summary>A bullet: the budget track, the actual bar and a budget tick, all star sized.</summary>
    private static Control Bullet(Department department, string tone, double height = 8)
    {
        var max = Math.Max(department.Budget, department.Actual) * 1.22;
        var budgetSlot = Math.Clamp(department.Budget / Math.Max(1, max), 0.02, 0.98);
        var actualSlot = Math.Clamp(department.Actual / Math.Max(1, max), 0.02, 0.98);

        var track = new Grid
        {
            Height = height,
            ColumnDefinitions = new ColumnDefinitions($"{actualSlot:0.###}*,{1 - actualSlot:0.###}*")
        };
        track.Children.Add(new Border
        {
            Background = DataKit.Brush(tone),
            CornerRadius = new CornerRadius(height / 2)
        });
        var remainder = new Border
        {
            Background = DataKit.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(height / 2)
        };
        Grid.SetColumn(remainder, 1);
        track.Children.Add(remainder);

        var tickGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{budgetSlot:0.###}*,1,{1 - budgetSlot:0.###}*")
        };
        var tick = new Border
        {
            Width = 2,
            Height = height + 6,
            Background = DataKit.Brush(ArcTokens.BorderStrong),
            CornerRadius = new CornerRadius(1),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(tick, 1);
        tickGrid.Children.Add(new Border());
        tickGrid.Children.Add(tick);

        var panel = new Panel { Height = height + 6, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new Border { Child = track, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(tickGrid);
        return panel;
    }
}
