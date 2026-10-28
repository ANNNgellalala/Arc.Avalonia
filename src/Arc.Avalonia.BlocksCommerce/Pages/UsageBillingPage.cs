using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Charts;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.BlocksCommerce.CommerceKit;
using ChartTone = Arc.Avalonia.Charts.ChartTone;

namespace Arc.Avalonia.BlocksCommerce.Pages;

/// <summary>
/// Usage billing: spend for the current period with a projection and a budget, meters against plan limits,
/// spend alert rules, invoice history and a plan card that opens an upgrade confirmation in place. Every
/// charge, limit and invoice is sample data; the upgrade only changes local state.
/// </summary>
public sealed class UsageBillingPage : ArcItemPage
{
    public override string ItemSlug => "usage-billing";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A billing page for usage priced SaaS or infrastructure products.",
        "Admin dashboards where finance needs spend alerts and invoice history in one place."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Flat seat based plans without meters; use billing-overview."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Meters pair a value and a limit in text, and warnings add an icon and words rather than colour alone.",
        "Invoice downloads swap their icon for a spinner, then a check, without changing width; alerts and upgrades are announced politely."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The spend line draws in once, meters fill with a short stagger, and the plan card grows into the confirm state on one spring.",
        "Reduced motion removes the draw in and the flight, keeping short fades."
    };

    public override string? ImplementationNotes =>
        "Daily spend, meters, invoices and alerts are sample data in the page. The projection is a seven day average carried to the end of the period, drawn as a dashed second series rather than the reference's shaded cone; the upgrade confirm is an in-place swap rather than a modal sheet, and no billing provider is called.";

    private static readonly double[] DailySpend =
    {
        42, 58, 51, 66, 78, 61, 49, 72, 83, 91, 74, 68, 88, 96, 81, 77, 64, 58
    };

    private sealed record InvoiceRow(string Number, string Period, string Amount, string Status);

    private static readonly InvoiceRow[] Invoices =
    {
        new("INV-2410", "1–30 Sep 2026", "$1,932.40", "Paid"),
        new("INV-2409", "1–31 Aug 2026", "$1,764.10", "Paid"),
        new("INV-2408", "1–31 Jul 2026", "$1,508.75", "Paid")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var spent = DailySpend.Sum();
        var average = DailySpend.Skip(DailySpend.Length - 7).Average();
        var projected = Math.Round(spent + average * 13);
        const double budget = 2000;
        var upgraded = false;
        var alertCount = 2;
        var status = Ck.Status();

        var planHost = new ContentControl();
        var alertHost = new StackPanel { Spacing = 6 };

        void BuildAlerts()
        {
            alertHost.Children.Clear();
            alertHost.Children.Add(Alert("80% of budget", "Email · finance@acme.com"));
            alertHost.Children.Add(Alert("100% of budget", "Slack · #billing"));
            for (var extra = 2; extra < alertCount; extra++)
            {
                alertHost.Children.Add(Alert(extra == 2 ? "120% of budget" : "150% of budget", "Email · on-call@acme.com"));
            }

            alertHost.Children.Add(Ck.Link("Add alert rule", () =>
            {
                alertCount++;
                BuildAlerts();
                Ck.Say(status, $"Alert rule added. {alertCount} rules now notify the billing channel.");
            }, "plus"));
        }

        Control Alert(string title, string detail)
        {
            return new Border
            {
                Background = Ck.Brush(ArcTokens.SurfaceMuted),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 4, 9, 4),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new StackPanel
                        {
                            Spacing = 0,
                            Children = { Ck.Text(title, "arc-body-sm arc-medium"), Ck.Caption(detail) }
                        },
                        Place(new ArcIcon
                        {
                            Icon = "bell",
                            Size = 13,
                            Foreground = Ck.Brush(ArcTokens.TextMuted),
                            VerticalAlignment = VerticalAlignment.Center
                        }, 1)
                    }
                }
            };
        }

        void BuildPlan()
        {
            var plan = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    Ck.Row(8, Ck.Pill(upgraded ? "Enterprise" : "Scale", ArcBadgeTone.Accent), Ck.Caption(upgraded ? "Upgraded" : "Current plan")),
                    Ck.Row(8,
                        Ck.Text(upgraded ? "$1,299" : "$499", "arc-heading"),
                        Ck.Caption("/ month")),
                    Ck.Divider(),
                    Meter("API requests", upgraded ? 6.4 : 6.4, upgraded ? 20 : 10, "M"),
                    Meter("Storage", 1.2, upgraded ? 4 : 2, "TB"),
                    Ck.Divider(),
                    upgraded
                        ? Ck.Pill("Every limit doubled", ArcBadgeTone.Success, true)
                        : Ck.Btn("Upgrade to Enterprise", ArcButtonVariant.Primary, ArcButtonSize.Md,
                            ConfirmUpgrade, trailingIcon: "arrow-right")
                }
            };

            planHost.Content = Ck.Card(plan, 14);
        }

        Control Meter(string label, double used, double limit, string unit)
        {
            var fraction = used / limit;
            return new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    Ck.Row(8,
                        Ck.Text(label, "arc-body-sm"),
                        Ck.Text($"{used:0.0}{unit} of {limit:0}{unit} · {fraction * 100:0}%", "arc-caption")),
                    Ck.Progress(used, limit, fraction >= 0.8 ? ArcProgressTone.Warning : ArcProgressTone.Accent)
                }
            };
        }

        void ConfirmUpgrade()
        {
            planHost.Content = Ck.Card(new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    Ck.Caption("Confirm upgrade"),
                    Ck.Text("Enterprise", "arc-subtitle"),
                    Ck.Body("Every limit doubles, overage drops to $0.008 and the card on file is charged the prorated difference today.", 280),
                    Ck.Stack(6,
                        Ck.Line("Now", "$499"),
                        Ck.Line("Enterprise", "$1,299"),
                        Ck.Line("Due today", "$412.90", strong: true)),
                    Ck.Btn("Confirm upgrade", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                    {
                        upgraded = true;
                        BuildPlan();
                        Ck.Say(status, "Upgraded to Enterprise. Limits doubled and the card ending 4242 was charged $412.90.");
                    }, icon: "check"),
                    Ck.Link("Not now", () =>
                    {
                        BuildPlan();
                        Ck.Say(status, "Upgrade cancelled. Nothing was charged.");
                    })
                }
            }, 14);
        }

        BuildAlerts();
        BuildPlan();

        var chart = new ArcLineChart
        {
            Series = new List<ChartSeries>
            {
                new("Daily spend", DailySpend),
                new("7 day average", DailySpend.Select((_, index) =>
                {
                    var start = Math.Max(0, index - 6);
                    var window = DailySpend.Skip(start).Take(index - start + 1).ToList();
                    return Math.Round(window.Average(), 1);
                }).ToList(), Ck.Brush(ArcTokens.Series2), true)
            },
            Labels = Enumerable.Range(1, DailySpend.Length).Select(day => day.ToString()).ToList(),
            Height = 124,
            ShowArea = false,
            ShowGrid = true,
            Smooth = true
        };

        var budgetBar = Ck.Progress(Math.Min(spent + average * 13, budget), budget, ArcProgressTone.Accent);

        var chartCard = Ck.Card(new StackPanel
        {
            Spacing = 7,
            Children =
            {
                Ck.Row(10,
                    Ck.Text("Spend so far", "arc-body-sm arc-medium"),
                    Ck.Pill(projected > budget ? "Over budget" : "On track",
                        projected > budget ? ArcBadgeTone.Warning : ArcBadgeTone.Success, true)),
                Ck.Row(8,
                    Ck.Text(Ck.Money(spent), "arc-heading"),
                    Ck.Caption($"projected {Ck.Money(projected)} of {Ck.Money(budget)}")),
                chart,
                budgetBar,
                Ck.Row(8,
                    Ck.Caption($"Day {DailySpend.Length} of 31"),
                    Ck.Caption("·"),
                    Ck.Caption($"7 day average {Ck.Money(Math.Round(average))}"))
            }
        }, 14);

        var invoiceRows = new StackPanel { Spacing = 4 };
        foreach (var invoice in Invoices)
        {
            var icon = Ck.IconBtn("download", $"Download invoice {invoice.Number}", null, 28);
            icon.Click += (_, _) =>
            {
                icon.Icon = "check";
                Ck.Say(status, $"Invoice {invoice.Number} downloaded. Sample file only.");
            };

            invoiceRows.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
                ColumnSpacing = 10,
                Children =
                {
                    Ck.Pill(invoice.Status, ArcBadgeTone.Success, true),
                    Place(Ck.Text($"{invoice.Number} · {invoice.Period}", "arc-body-sm"), 1),
                    Place(Ck.Text(invoice.Amount, "arc-body-sm arc-medium"), 2),
                    Place(icon, 3)
                }
            });
        }

        var invoicesCard = Ck.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Ck.Row(8, Ck.Label("Invoices"), Ck.Link("View all", () => Ck.Say(status, "The reference opens the invoice list; this specimen keeps three."))),
                invoiceRows
            }
        }, 14);

        var alertsCard = Ck.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Ck.Row(8, Ck.Label("Spend alerts"), Ck.Caption("Notify before the budget is spent")),
                alertHost
            }
        }, 14);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ck.Heading("Usage", 420, "arc-subtitle"),
                Ck.Caption("1–31 October · seats 24 of 30 · billed to Acme Inc.")
            }
        });
        var headerRight = Ck.Row(8, Ck.Pill("Metered plan", ArcBadgeTone.Neutral), Ck.Meta("calendar", "Next invoice 1 Nov"));
        headerRight.HorizontalAlignment = HorizontalAlignment.Right;
        headerRight.VerticalAlignment = VerticalAlignment.Top;
        header.Children.Add(Place(headerRight, 1));

        var top = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,24,330"),
            Children = { chartCard, Place(planHost, 2) }
        };

        var bottom = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,24,*"),
            Children = { alertsCard, Place(invoicesCard, 2) }
        };

        return Ck.Specimen(new StackPanel
        {
            Spacing = 12,
            Children = { header, top, bottom, status }
        }, 16);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
