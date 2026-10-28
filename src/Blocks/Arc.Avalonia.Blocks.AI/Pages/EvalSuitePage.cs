using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Charts;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Eval suite: a candidate version of an LLM feature scored against its baseline, category by category, with the
/// regressed cases listed underneath. Running and filtering change the page state; scoring is sample data.
/// </summary>
public sealed class EvalSuitePage : ArcItemPage
{
    public override string ItemSlug => "eval-suite";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Regression testing an LLM feature before shipping a new prompt, model or agent version.",
        "CI style eval dashboards that need the overall score, the per category picture and the broken cases.",
        "Triage of failing cases with an LLM judge: read both outputs, check the rubric, then file or accept."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use prompt studio to try one case at a time, or model arena for a blind vote on a single prompt."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each category row is one image with a full text summary, so 12 cells never become 12 announcements.",
        "Outcomes never rely on colour: pass, fail and error each have their own glyph.",
        "Case rows with a drilldown are buttons with an expanded state, and the filter is a radio group."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Cells fill as results arrive; the score and per category percentages count to their values on a spring.",
        "Reduced motion fills cells, numbers and bars immediately and drops the counting animation."
    };

    public override string? ImplementationNotes =>
        "Results are sample data. Run resets the summary to the same finished state and labels it simulated, and "
        + "triage only changes the case row. Rubric bars show the new score against a baseline tick.";

    private sealed record Category(string Name, int Passed, int Total, int Baseline, int Delta);

    private static readonly Category[] Categories =
    {
        new("Refunds", 22, 24, 21, 1),
        new("Billing", 23, 24, 24, -1),
        new("Escalation", 20, 24, 19, 1),
        new("Tone", 24, 24, 22, 2)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = K.Caption("Run finished: 88.5%, up 3.1 points on the baseline. 3 regressions. Simulated run.");
        var filter = "Regressions";
        var filterRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var caseHost = new StackPanel { Spacing = 6 };
        var score = K.Text("88.5%", "arc-heading");
        var delta = K.Text("+3.1 pts", "arc-caption");

        void RenderFilters()
        {
            filterRow.Children.Clear();
            foreach (var (label, value, count) in new[]
                     {
                         ("All", "All", 24), ("Failed", "Failed", 3), ("Regressions", "Regressions", 3), ("Improved", "Improved", 6)
                     })
            {
                var match = value;
                var chip = K.Chip($"{label} {count}", selected: filter == match);
                chip.Click += (_, _) =>
                {
                    filter = match;
                    RenderFilters();
                    RenderCases();
                    status.Text = $"Showing {label.ToLowerInvariant()} cases.";
                };
                chip.Margin = new Thickness(0, 0, 5, 4);
                filterRow.Children.Add(chip);
            }
        }

        void RenderCases()
        {
            caseHost.Children.Clear();
            if (filter is "Improved" or "All")
            {
                caseHost.Children.Add(K.Caption("No improved case is open. Regressions are the interesting ones."));
                return;
            }

            foreach (var (title, category, input, baseline, current, scores) in new[]
                     {
                         ("Refund after partial shipment", "Refunds",
                             "Only 2 of my 3 items arrived (order #48213). Can I get my money back for that part?",
                             "I've refunded the whole order of $84.00.",
                             "I've refunded $34.00 for the missing candle set only.",
                             "4 / 5 · grounded, partial")
                     })
            {
                var body = new StackPanel { Spacing = 6 };
                body.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Text(title, "arc-body-sm"),
                        K.Caption(category)
                    }
                });
                if (body.Children[0] is Grid caseHead)
                {
                    Grid.SetColumn(caseHead.Children[1], 1);
                }

                body.Children.Add(K.Caption("Input"));
                body.Children.Add(K.Text(input, "arc-caption", 600));
                body.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        K.Stack(2,
                            K.Caption("Baseline v11"),
                            K.Text(baseline, "arc-caption", 260)),
                        K.Stack(2,
                            K.Caption("Candidate v12"),
                            K.Text(current, "arc-caption", 260))
                    }
                });
                if (body.Children[^1] is Grid outputs)
                {
                    Grid.SetColumn(outputs.Children[1], 1);
                }

                body.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Row(6,
                            new ArcIcon { Icon = "circle-check", Size = 12, Foreground = K.Brush(ArcTokens.Success) },
                            K.Caption("Judge: " + scores)),
                        K.Row(5,
                            K.Chip("Mark as expected", icon: "check"),
                            K.Chip("Open issue", icon: "external-link"))
                    }
                });
                if (body.Children[^1] is Grid triage)
                {
                    Grid.SetColumn(triage.Children[1], 1);
                }

                caseHost.Children.Add(new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    BorderBrush = K.Tint(ArcTokens.Danger, 0.24),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10, 8, 10, 8),
                    Child = body
                });
            }
        }

        var run = K.Btn("Run again", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
        {
            score.Text = "88.5%";
            delta.Text = "+3.1 pts";
            status.Text = "Run finished: 88.5%, 3 regressions. The run is simulated.";
            RenderFilters();
            RenderCases();
        }, icon: "play");

        var grid = new StackPanel { Spacing = 4 };
        foreach (var category in Categories)
        {
            var cells = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            for (var index = 0; index < 12; index++)
            {
                var failed = index < (category.Total - category.Passed);
                cells.Children.Add(new Border
                {
                    Width = 9,
                    Height = 9,
                    CornerRadius = new CornerRadius(2),
                    Background = K.Brush(failed ? ArcTokens.Danger : ArcTokens.Success),
                    Opacity = failed ? 1 : 0.85
                });
            }

            var percent = category.Passed / (double)category.Total * 100;
            var body = new Grid { ColumnDefinitions = new ColumnDefinitions("90,Auto,*,Auto") };
            body.Children.Add(K.Text(category.Name, "arc-caption"));
            cells.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(cells, 1);
            body.Children.Add(cells);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
            bar.Children.Add(K.Bar(percent, 100, ArcProgressSize.Sm));
            Grid.SetColumn(bar, 2);
            body.Children.Add(bar);
            var deltaLabel = K.Caption($"{percent:0}%  {(category.Delta > 0 ? "+" : string.Empty)}{category.Delta}");
            deltaLabel.Foreground = K.Brush(category.Delta >= 0 ? ArcTokens.Success : ArcTokens.Danger);
            Grid.SetColumn(deltaLabel, 3);
            body.Children.Add(deltaLabel);
            AutomationProperties.SetName(body,
                $"{category.Name}: {category.Passed} passed, {category.Total - category.Passed} failed, {percent:0} percent, "
                + $"{(category.Delta > 0 ? "up" : "down")} {Math.Abs(category.Delta)} against the baseline");
            grid.Children.Add(body);
        }

        RenderFilters();
        RenderCases();

        var content = new StackPanel
        {
            Spacing = 9,
            MaxWidth = 800,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Support agent evals", "arc-heading"),
                            K.Caption("v12 candidate · Sonnet 5.5   vs   v11 baseline · Sonnet 5.5   ·   judge Opus 5.5")),
                        K.Row(6, K.Chip("History", icon: "clock"), run)
                    }
                },
                K.Divider(2),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,*,Auto"),
                    ColumnSpacing = 22,
                    Children =
                    {
                        K.Stack(1, K.Caption("Score"), score),
                        K.Stack(1, K.Caption("vs baseline"), delta),
                        K.Stack(1, K.Caption("Regressions"), K.Text("3", "arc-body-sm")),
                        K.Stack(1, K.Caption("Cost and time"), K.Text("$1.42 · 4m 12s", "arc-body-sm")),
                        new ArcSparkline
                        {
                            Values = new[] { 82.0, 84.0, 83.0, 85.0, 86.0, 86.1, 88.3 },
                            Height = 34,
                            ShowArea = true,
                            ShowLastPoint = true,
                            Width = 130
                        }
                    }
                },

                grid,
                K.Divider(2),
                filterRow,
                caseHost,
                status
            }
        };

        if (content.Children[0] is Grid head)
        {
            Grid.SetColumn(head.Children[1], 1);
        }

        if (content.Children[2] is Grid summary)
        {
            for (var index = 1; index < summary.Children.Count; index++)
            {
                Grid.SetColumn(summary.Children[index], index);
            }
        }

        return K.Surface(new Border
        {
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 900
        }, new Thickness(20, 12, 20, 12));
    }
}
