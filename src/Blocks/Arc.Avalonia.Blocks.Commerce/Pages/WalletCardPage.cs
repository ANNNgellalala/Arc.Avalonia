using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Charts.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.Blocks.Commerce.CommerceKit;

namespace Arc.Avalonia.Blocks.Commerce.Pages;

/// <summary>
/// Wallet card: a compact account view with a scrubbable balance chart, hide balance, focused money actions
/// and searchable recent activity. Balances and actions are local sample state; nothing moves real money.
/// </summary>
public sealed class WalletCardPage : ArcItemPage
{
    public override string ItemSlug => "wallet-card";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A personal or business balance summary with quick send, deposit, swap and buy actions.",
        "Dashboards that pair a balance with a small history chart and recent activity."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Subscription billing and invoices; use billing-overview."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The balance is a live region and hidden balances read as Balance hidden rather than showing dashes.",
        "Action buttons keep a pressed state, and amount errors are tied to the field with an alert line."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The balance counts to a new value and the chart frame stays fixed so the card does not jump.",
        "Panels swap with a short fade; reduced motion sets values instantly."
    };

    public override string? ImplementationNotes =>
        "Balances, activity and transfers are sample data. The reference scrubs the chart by pointer; here the chart is a static ArcSparkline and the actions are plain local state. No ledger, card network or currency conversion is involved.";

    private sealed record Entry(string Name, string Detail, double Amount, string Icon);

    private static readonly Entry[] SeedEntries =
    {
        new("Coffee Lab", "Card · today, 09:12", -4.80, "circle-dot"),
        new("Acme payroll", "Transfer · yesterday", 3200, "arrow-down"),
        new("Figma", "Subscription · 21 Oct", -15.00, "layers"),
        new("Mira Chen", "Split dinner · 19 Oct", -32.40, "users")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var main = 12480.32;
        var reserve = 2850.00;
        var hidden = false;
        var account = "main";
        var action = "send";
        var status = Ck.Status();

        var activity = SeedEntries.ToList();
        var activityHost = new StackPanel { Spacing = 2 };
        var balanceText = Ck.Text(Ck.Money(main, 2), "arc-title-sm");
        var reserveText = Ck.Text(Ck.Money(reserve, 2), "arc-subtitle");
        var accountLabel = Ck.Caption("Main account");
        var amountHost = new ContentControl();

        var sendAmount = new ArcMoneyInput
        {
            Value = 25000,
            Currency = "$",
            Min = 0,
            Max = 500000,
            Step = 2500,
            QuickAdd = new List<int> { 50, 100, 250 }
        };

        void BuildActivity(string filter = "")
        {
            activityHost.Children.Clear();
            var matches = activity
                .Where(entry => string.IsNullOrWhiteSpace(filter) ||
                                entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();
            if (matches.Count == 0)
            {
                activityHost.Children.Add(Ck.Caption("No activity matches that search."));
                return;
            }

            foreach (var entry in matches)
            {
                var positive = entry.Amount > 0;
                activityHost.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 10,
                    Margin = new Thickness(0, 2, 0, 2),
                    Children =
                    {
                        Ck.Disc(entry.Icon, 30, 14),
                        Place(new StackPanel
                        {
                            Spacing = 1,
                            Children = { Ck.Text(entry.Name, "arc-body-sm"), Ck.Caption(entry.Detail) }
                        }, 1),
                        Place(Ck.Text(
                            (positive ? "+" : "−") + Ck.Money(Math.Abs(entry.Amount), 2),
                            positive ? "arc-body-sm arc-medium" : "arc-body-sm"), 2)
                    }
                });
            }
        }

        void BuildAmountPanel()
        {
            if (action == "deposit")
            {
                amountHost.Content = Ck.Stack(8,
                    Ck.Caption("Add money from the account ending 4242. Arrives instantly, sample only."),
                    Ck.Row(8,
                        Ck.Btn("Add $250", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                        {
                            main += 250;
                            activity.Insert(0, new Entry("From card 4242", "Deposit · just now", 250, "arrow-down"));
                            Refresh();
                            Ck.Say(status, "Deposited $250.00 from the account ending 4242.");
                        }),
                        Ck.Btn("Add $1,000", ArcButtonVariant.Secondary, ArcButtonSize.Md, () =>
                        {
                            main += 1000;
                            activity.Insert(0, new Entry("From card 4242", "Deposit · just now", 1000, "arrow-down"));
                            Refresh();
                            Ck.Say(status, "Deposited $1,000.00 from the account ending 4242.");
                        })));
                return;
            }

            amountHost.Content = Ck.Stack(8,
                Ck.Field("Amount", sendAmount),
                Ck.Row(8,
                    Ck.Btn("Send money", ArcButtonVariant.Primary, ArcButtonSize.Md, () =>
                    {
                        var cents = sendAmount.Value ?? 0;
                        var amount = cents / 100.0;
                        if (amount <= 0)
                        {
                            sendAmount.Error = "Enter an amount above zero.";
                            return;
                        }

                        if (amount > main)
                        {
                            sendAmount.Error = "That is more than the balance.";
                            return;
                        }

                        sendAmount.Error = null;
                        main -= amount;
                        activity.Insert(0, new Entry("Mira Chen", "Sent · just now", -amount, "send"));
                        Refresh();
                        Ck.Say(status, $"Sent {Ck.Money(amount, 2)} to Mira Chen.");
                    }, trailingIcon: "arrow-up-right"),
                    Ck.Caption("Arrives in seconds. No fee on the first transfer each month.")));
        }

        void Refresh()
        {
            balanceText.Text = hidden ? "Balance hidden" : Ck.Money(main, 2);
            reserveText.Text = Ck.Money(reserve, 2);
            accountLabel.Text = account == "main" ? "Main account" : "Reserve account";
            BuildActivity();
        }

        var eye = Ck.IconBtn("eye", "Hide balance", null, 32);
        eye.Click += (_, _) =>
        {
            hidden = !hidden;
            eye.Icon = hidden ? "eye-off" : "eye";
            balanceText.Text = hidden ? "Balance hidden" : Ck.Money(main, 2);
            Ck.Say(status, hidden ? "Balance hidden." : "Balance shown.");
        };

        var switcher = Ck.Segmented(
            new[] { ("main", "Main"), ("reserve", "Reserve") },
            account,
            value =>
            {
                account = value;
                Refresh();
            },
            "Account");

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (id, label, icon) in new[]
                 {
                     ("send", "Send", "send"), ("deposit", "Deposit", "arrow-down"), ("swap", "Swap", "refresh-cw"), ("buy", "Buy", "credit-card")
                 })
        {
            var selected = action == id;
            var button = new ArcButton
            {
                Variant = selected ? ArcButtonVariant.Secondary : ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Height = 34,
                MinHeight = 34,
                CornerRadius = new CornerRadius(999),
                Content = Ck.Row(6,
                    new ArcIcon { Icon = icon, Size = 14 },
                    new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center })
            };
            if (selected)
            {
                button.BorderBrush = Ck.Brush(ArcTokens.Accent);
                button.BorderThickness = new Thickness(1);
                button.Background = Ck.Brush(ArcTokens.ControlOnSubtle);
            }

            var target = id;
            button.Click += (_, _) =>
            {
                action = target;
                BuildAmountPanel();
                Ck.Say(status, $"{label} selected.");
            };
            actions.Children.Add(button);
        }

        var chart = new ArcSparkline
        {
            Values = new List<double> { 11.2, 11.6, 11.4, 11.9, 12.1, 11.8, 12.3, 12.2, 12.5, 12.4, 12.5 },
            Height = 36,
            ShowArea = true,
            ShowLastPoint = true,
            Tone = ChartTone.Accent
        };

        var search = Ck.Search("Search activity", text => BuildActivity(text), 190);
        BuildActivity();
        BuildAmountPanel();

        var wallet = Ck.Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children = { accountLabel, Place(Ck.Row(6, switcher, eye), 1) }
                },
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        balanceText,
                        Ck.Row(8,
                            Ck.Caption("+2.4% this month"),
                            Ck.Caption("·"),
                            Ck.Caption("Available to spend"))
                    }
                },
                chart,
                actions,
                Ck.Tile(amountHost, 10),
                Ck.Divider(),
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                            Children =
                            {
                                Ck.Label("Recent activity"),
                                Place(Ck.Link("View all", () => Ck.Say(status, "The reference opens the full activity list.")), 1)
                            }
                        },
                        search,
                        activityHost
                    }
                },
                status
            }
        }, 16);

        var reserveCard = Ck.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Ck.Caption("Reserve account"),
                reserveText,
                Ck.Caption("Held for taxes, released on the 1st."),
                Ck.Divider(),
                Ck.Link("Move to main", () =>
                {
                    var moved = reserve;
                    reserve = 0;
                    main += moved;
                    activity.Insert(0, new Entry("From reserve", "Transfer · just now", moved, "refresh-cw"));
                    Refresh();
                    Ck.Say(status, $"Moved {Ck.Money(moved, 2)} from reserve to main.");
                }, "arrow-right"),
                Ck.Caption("Instant, no fee.")
            }
        }, 16);

        var rows = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Statement("September 2026", "PDF"),
                Statement("August 2026", "PDF")
            }
        };

        var statementsCard = Ck.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Ck.Label("Statements"),
                rows
            }
        }, 16);

        var side = new StackPanel { Spacing = 12, Children = { reserveCard, statementsCard } };

        var stage = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("640,24,*"),
            Children = { wallet, Place(side, 2) }
        };

        return Ck.Specimen(stage, 16);

        Control Statement(string label, string format)
        {
            return new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 1,
                        Children = { Ck.Text(label, "arc-body-sm"), Ck.Caption($"Statement · {format}") }
                    },
                    Place(Ck.IconBtn("download", $"Download {label} statement",
                        () => Ck.Say(status, $"{label} statement downloaded. Sample file only."), 28), 1)
                }
            };
        }
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
