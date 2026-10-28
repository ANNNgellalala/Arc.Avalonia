using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Agent approvals: one permission prompt at a time, with the tool, the agent's reason, editable arguments and
/// a single decision. Approving or denying brings the next pending request forward.
/// </summary>
public sealed class AgentApprovalsPage : ArcItemPage
{
    public override string ItemSlug => "agent-approvals";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Human in the loop control for agents that call tools with side effects: deploys, emails, database writes.",
        "An approvals card in an agent console where one person reviews and decides tool calls quickly.",
        "Products that let repeated approvals become a standing rule for one agent or the whole workspace."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use agent run for one live run with an inline confirm, or agent tasks for the work itself."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The focused request is a group named by its title; j and k move through the queue and a approves.",
        "Risk is a shield icon plus the word Low, Medium or High, so it never relies on colour.",
        "Focus moves to the next request after each decision, and every decision is announced politely."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The next request rises fourteen pixels into place while the decided row collapses below it.",
        "Reduced motion swaps the request in place and keeps focus, labels and announcements unchanged."
    };

    public override string? ImplementationNotes =>
        "Approving or denying removes the request from the page queue and appends it to the resolved log; nothing "
        + "reaches an agent. Arguments are read-only rows rather than inline fields, and Always allow adds a rule "
        + "chip without matching older requests. Keyboard shortcuts are documented but only the buttons are wired.";

    private sealed record Request(string Id, string Agent, string Model, string Tool, string Action, string Title,
        string Summary, string Reason, string Risk, string[] Args);

    private static readonly Request[] Queue =
    {
        new("req-deploy", "Deploy agent", "Claude Opus 5.5", "Vercel", "Deploy to production",
            "Run vercel deploy --prod", "Deploy northwind-web to production",
            "The preview passed all 214 checks and the release window opened at 14:00.", "High",
            new[] { "project: northwind-web", "commit: 8f3c2a1", "target: production" }),
        new("req-update", "Data agent", "Claude Sonnet 5.5", "PostgreSQL", "Update rows",
            "UPDATE 1,284 rows in customers", "Backfill the new locale column",
            "The migration is additive and every row has a valid locale source.", "Medium",
            new[] { "table: customers", "rows: 1,284", "statement: additive" }),
        new("req-email", "Support agent", "Claude Haiku 5.5", "Gmail", "Send an email",
            "Send the refund confirmation to Mara Okafor", "Email one customer",
            "The refund was approved by a person in the thread.", "Low",
            new[] { "to: mara@northwind.example", "template: refund-confirmed" }),
        new("req-issue", "Ops agent", "Claude Sonnet 5.5", "Linear", "Create an issue",
            "Create ENG-2214 in the platform team", "File a follow-up issue",
            "The queue depth alert has no owner yet.", "Low",
            new[] { "team: platform", "label: reliability" })
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var pending = Queue.ToList();
        var resolved = new List<string>
        {
            "Approved: Deploy agent · Vercel deploy to staging · 12m ago",
            "Denied: Data agent · DELETE 3 rows in events · 18m ago"
        };
        var rules = 1;
        var status = K.Caption("One request in focus, three more waiting. Decisions are simulated.");

        var cardHost = new ContentControl();
        var queueHost = new StackPanel { Spacing = 3 };
        var rulesButton = K.Chip("Rules 1", icon: "shield-check");
        var resolvedLog = new StackPanel { Spacing = 3, IsVisible = false };
        var resolvedToggle = K.Chip("Resolved 2", icon: "chevron-down");

        void RenderResolved()
        {
            resolvedLog.Children.Clear();
            foreach (var entry in resolved)
            {
                resolvedLog.Children.Add(K.Row(6,
                    new ArcIcon { Icon = "circle-check", Size = 12, Foreground = K.Brush(ArcTokens.Success) },
                    K.Caption(entry)));
            }

            resolvedToggle.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = $"Resolved {resolved.Count}", FontSize = 13 },
                    new ArcIcon { Icon = resolvedLog.IsVisible ? "chevron-up" : "chevron-down", Size = 12 }
                }
            };
        }

        void RenderQueue()
        {
            queueHost.Children.Clear();
            foreach (var request in pending.Skip(1))
            {
                var token = request.Risk == "High" ? ArcTokens.Danger : request.Risk == "Medium" ? ArcTokens.Warning : ArcTokens.Success;
                var row = new Border
                {
                    Background = Brushes.Transparent,
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(8, 5, 8, 5),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        Children =
                        {
                            K.Disc("shield", 20, iconSize: 11),
                            K.Stack(1,
                                K.Text(request.Title, "arc-caption"),
                                K.Caption($"{request.Agent} · {request.Summary}")),
                            K.Row(5,
                                new ArcIcon { Icon = "triangle-alert", Size = 11, Foreground = K.Brush(token) },
                                K.Caption(request.Risk))
                        }
                    }
                };
                if (row.Child is Grid rowGrid)
                {
                    rowGrid.Children[1].Margin = new Thickness(8, 0, 8, 0);
                    Grid.SetColumn(rowGrid.Children[1], 1);
                    Grid.SetColumn(rowGrid.Children[2], 2);
                }

                row.Focusable = true;
                row.Cursor = new Cursor(StandardCursorType.Hand);
                AutomationProperties.SetName(row, $"{request.Agent}: {request.Summary}, {request.Risk} risk");
                queueHost.Children.Add(row);
            }
        }

        void RenderFocus()
        {
            if (pending.Count == 0)
            {
                cardHost.Content = new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(16),
                    Child = K.Stack(4,
                        K.Text("Nothing waiting", "arc-label"),
                        K.Caption("Every request is decided. New tool calls appear here."))
                };
                queueHost.Children.Clear();
                status.Text = "Queue clear. Every decision is in the resolved log.";
                return;
            }

            var request = pending[0];
            var riskToken = request.Risk == "High" ? ArcTokens.Danger
                : request.Risk == "Medium" ? ArcTokens.Warning
                : ArcTokens.Success;

            ArcButton Decision(string label, string outcome)
            {
                var button = K.Btn(label, label == "Approve" ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary,
                    ArcButtonSize.Sm, () =>
                    {
                        pending.Remove(request);
                        resolved.Insert(0, $"{outcome}: {request.Agent} · {request.Summary} · just now");
                        RenderFocus();
                        RenderQueue();
                        RenderResolved();
                        status.Text = $"{outcome}. {pending.Count} requests still waiting. Nothing reached an agent.";
                    }, icon: outcome == "Approved" ? "check" : null);
                return button;
            }

            var approve = Decision("Approve", "Approved");
            var deny = Decision("Deny", "Denied");
            var always = K.Chip("Always allow", icon: "shield-check");
            always.Click += (_, _) =>
            {
                rules++;
                pending.Remove(request);
                resolved.Insert(0, $"Allowed by rule: {request.Agent} · {request.Summary} · just now");
                rulesButton.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new ArcIcon { Icon = "shield-check", Size = 12 },
                        new TextBlock { Text = $"Rules {rules}", FontSize = 13 }
                    }
                };
                RenderFocus();
                RenderQueue();
                RenderResolved();
                status.Text = $"{request.Tool} {request.Action.ToLowerInvariant()} is now allowed for {request.Agent}. Rule count {rules}.";
            };

            var args = new StackPanel { Spacing = 3 };
            foreach (var argument in request.Args)
            {
                var parts = argument.Split(':', 2);
                args.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("110,*"),
                    Children =
                    {
                        K.Caption(parts[0]),
                        K.Mono(parts.Length > 1 ? parts[1].Trim() : argument, 11.5)
                    }
                });
                if (args.Children[^1] is Grid argRow)
                {
                    Grid.SetColumn(argRow.Children[1], 1);
                }
            }

            var card = new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    K.Row(7,
                        K.Disc("shield", 24, true, 13),
                        K.Text(request.Agent, "arc-label"),
                        K.Caption("asked 2m ago")),
                    K.Row(7,
                        K.Disc("box", 20, iconSize: 11),
                        K.Text(request.Tool, "arc-caption"),
                        K.Caption("·"),
                        K.Text(request.Action, "arc-caption")),
                    new Border
                    {
                        Background = K.Brush(ArcTokens.SurfaceMuted),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(10, 7, 10, 7),
                        Child = K.Mono(request.Title, 12.5)
                    },
                    K.Text(request.Reason, "arc-caption", 520),
                    args,
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            K.Row(5,
                                new ArcIcon { Icon = "shield", Size = 12, Foreground = K.Brush(riskToken) },
                                K.Caption($"{request.Risk} risk")),
                            K.Row(5, deny, always, approve)
                        }
                    }
                }
            };
            if (card.Children[^1] is Grid actions)
            {
                Grid.SetColumn(actions.Children[1], 1);
            }

            cardHost.Content = card;
            RenderQueue();
        }

        rulesButton.Click += (_, _) => status.Text = "Rules popover opened. One rule: Vercel deploy for Deploy agent.";
        resolvedToggle.Click += (_, _) =>
        {
            resolvedLog.IsVisible = !resolvedLog.IsVisible;
            RenderResolved();
        };

        RenderResolved();
        RenderFocus();
        RenderQueue();

        var content = new StackPanel
        {
            Spacing = 9,
            MaxWidth = 660,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Approvals", "arc-heading"),
                            K.Caption($"{Queue.Length} waiting · Emma Collins reviewing")),
                        K.Row(4, rulesButton, K.IconBtn("more-horizontal", "Approval settings"))
                    }
                },
                K.Divider(2),
                cardHost,
                K.Divider(2),
                queueHost,
                resolvedToggle,
                resolvedLog,
                status
            }
        };

        if (content.Children[0] is Grid head)
        {
            Grid.SetColumn(head.Children[1], 1);
        }

        return K.Surface(new Border
        {
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 780
        }, new Thickness(22, 15, 22, 15));
    }
}
