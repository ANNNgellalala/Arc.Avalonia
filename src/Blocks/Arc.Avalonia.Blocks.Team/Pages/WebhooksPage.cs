using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team.Pages;

/// <summary>
/// Webhooks: an endpoint summary, delivery history with a Failed filter, and a payload and headers inspector
/// with a retry action. Selecting a delivery drives the inspector; a retry resolves the attempt and rolls the
/// success rate. Endpoints, attempts and payloads are sample data in memory.
/// </summary>
public sealed class WebhooksPage : ArcItemPage
{
    private sealed class Delivery
    {
        public string Event { get; init; } = string.Empty;
        public string Status { get; set; } = "200";
        public string Time { get; init; } = string.Empty;
        public string Duration { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
        public bool Failed => Status is not "200";
    }

    public override string ItemSlug => "webhooks";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-endpoint-summary", "variant-delivery-history", "state-failed-delivery", "variant-payload-inspector"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A developer settings page for webhook endpoints, delivery history and retries.",
        "Consoles that need a payload and headers inspector next to the delivery list."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use api-keys for managing tokens, or audit-log for account activity rather than outgoing events."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Delivery and endpoint rows are selectable with the keyboard and mark the current item.",
        "Payload and headers use a two tab switch; the code panel is focusable and the retry result is announced."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The active tab highlight slides, a successful retry swaps to Resolved and the success rate rolls.",
        "Reduced motion uses instant swaps and opacity only."
    };

    public override string? ImplementationNotes =>
        "Endpoints, deliveries and payloads are sample data in memory; Retry marks the attempt resolved without " +
        "calling a service. The signing secret is masked and there is no endpoint creator in this preview. " +
        "No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var deliveries = new List<Delivery>
        {
            new()
            {
                Event = "invoice.paid",
                Status = "200",
                Time = "12:42:08",
                Duration = "184 ms",
                Payload = "{\n  \"id\": \"evt_9f2c\",\n  \"type\": \"invoice.paid\",\n  \"data\": {\n    \"invoice\": \"in_2041\",\n    \"amount\": 2400,\n    \"currency\": \"usd\"\n  }\n}"
            },
            new()
            {
                Event = "seat.created",
                Status = "500",
                Time = "12:40:51",
                Duration = "30 s",
                Payload = "{\n  \"id\": \"evt_9f2b\",\n  \"type\": \"seat.created\",\n  \"data\": {\n    \"workspace\": \"northwind\",\n    \"seats\": 11\n  }\n}"
            },
            new()
            {
                Event = "invoice.paid",
                Status = "200",
                Time = "12:36:17",
                Duration = "201 ms",
                Payload = "{\n  \"id\": \"evt_9f2a\",\n  \"type\": \"invoice.paid\",\n  \"data\": {\n    \"invoice\": \"in_2040\",\n    \"amount\": 890,\n    \"currency\": \"usd\"\n  }\n}"
            },
            new()
            {
                Event = "member.invited",
                Status = "500",
                Time = "12:31:02",
                Duration = "30 s",
                Payload = "{\n  \"id\": \"evt_9f29\",\n  \"type\": \"member.invited\",\n  \"data\": {\n    \"email\": \"sam.okafor@northwind.co\",\n    \"role\": \"viewer\"\n  }\n}"
            },
            new()
            {
                Event = "subscription.updated",
                Status = "200",
                Time = "12:22:45",
                Duration = "166 ms",
                Payload = "{\n  \"id\": \"evt_9f28\",\n  \"type\": \"subscription.updated\",\n  \"data\": {\n    \"plan\": \"scale\",\n    \"status\": \"active\"\n  }\n}"
            }
        };

        var selected = deliveries[0];
        var filter = "all";
        var tab = "payload";
        var success = 99.2;
        var status = TeamKit.Caption("Deliveries are retried with exponential backoff for 24 hours.");

        var historyHost = new StackPanel { Spacing = 4 };
        var inspectorHost = new Border();

        void RenderHistory()
        {
            historyHost.Children.Clear();
            var rows = deliveries.Where(delivery => filter == "all" || delivery.Failed).ToList();
            foreach (var delivery in rows)
            {
                var isSelected = ReferenceEquals(delivery, selected);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
                row.Children.Add(new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock { Text = delivery.Event, Classes = { "arc-label" } },
                        TeamKit.Caption(delivery.Time)
                    }
                });
                var badge = TeamKit.Pill(delivery.Status, delivery.Status switch
                {
                    "200" => ArcBadgeTone.Success,
                    "Retrying" => ArcBadgeTone.Warning,
                    _ => ArcBadgeTone.Danger
                });
                Grid.SetColumn(badge, 1);
                row.Children.Add(badge);
                var duration = TeamKit.Caption(delivery.Duration);
                duration.VerticalAlignment = VerticalAlignment.Center;
                duration.Margin = new Thickness(12, 0);
                Grid.SetColumn(duration, 2);
                row.Children.Add(duration);
                var chevron = new ArcIcon
                {
                    Icon = "chevron-right",
                    Size = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = TeamKit.Brush(isSelected ? ArcTokens.AccentStrong : ArcTokens.TextMuted)
                };
                Grid.SetColumn(chevron, 3);
                row.Children.Add(chevron);

                historyHost.Children.Add(TeamKit.Selectable(row, () =>
                {
                    selected = delivery;
                    RenderHistory();
                    RenderInspector();
                }, isSelected, $"{delivery.Event} at {delivery.Time}", 12, new Thickness(10, 6)));
            }

            if (rows.Count == 0)
            {
                historyHost.Children.Add(TeamKit.Panel(TeamKit.Stack(5,
                    TeamKit.Label("No failed deliveries"),
                    TeamKit.Body("Every recent attempt returned 2xx.")), 16, 14, true));
            }
        }

        void RenderInspector()
        {
            var download = new ArcCopyButton
            {
                Value = selected.Payload,
                Label = "Copy payload",
                CopiedLabel = "Copied",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                IconOnly = true
            };
            AutomationProperties.SetName(download, "Copy delivery payload");

            var tabs = TeamKit.Segmented(new[] { ("payload", "Payload"), ("headers", "Headers") }, tab, value =>
            {
                tab = value;
                RenderInspector();
            }, "Inspector");

            var code = tab == "payload"
                ? selected.Payload
                : "{\n  \"content-type\": \"application/json\",\n  \"x-arc-signature\": \"t=1738,v1=8f3c…\",\n  \"x-arc-delivery\": \"dlv_9f2c\",\n  \"user-agent\": \"Arc-Webhooks/2.4\"\n}";

            var codePanel = new Border
            {
                Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Child = TeamKit.Mono(code)
            };

            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
            header.Children.Add(tabs);
            Grid.SetColumn(download, 1);
            header.Children.Add(download);
            var retry = TeamKit.Btn(selected.Failed ? "Retry delivery" : "Resend", ArcButtonVariant.Secondary,
                ArcButtonSize.Sm, icon: "rotate-cw");
            Grid.SetColumn(retry, 2);
            header.Children.Add(retry);
            retry.Click += (_, _) =>
            {
                if (!selected.Failed)
                {
                    status.Text = "A fresh attempt was queued for this endpoint.";
                    return;
                }

                selected.Status = "200";
                success = Math.Min(100, success + 0.2);
                RenderHistory();
                RenderInspector();
                status.Text = "Delivery resolved on retry. The success rate has been recalculated.";
                status.Foreground = TeamKit.Brush(ArcTokens.Success);
            };

            inspectorHost.Child = TeamKit.Stack(10,
                header,
                TeamKit.Row(10,
                    TeamKit.Meta("clock", $"{selected.Time} · {selected.Duration}"),
                    TeamKit.Meta("server", tab == "payload" ? "Response 200 · 412 bytes" : "Request headers")),
                codePanel);
        }

        var filterSwitch = TeamKit.Segmented(new[]
        {
            ("all", "All deliveries"),
            ("failed", "Failed")
        }, "all", value =>
        {
            filter = value;
            RenderHistory();
        }, "Delivery filter");

        var endpointHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        endpointHeader.Children.Add(TeamKit.Row(8,
            TeamKit.Disc("link-2", 30, 14, true),
            new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = "Northwind production", Classes = { "arc-label" } },
                    TeamKit.Mono("https://api.northwind.co/hooks/arc")
                }
            }));
        var activePill = TeamKit.Pill("Active", ArcBadgeTone.Success, dot: true);
        Grid.SetColumn(activePill, 1);
        endpointHeader.Children.Add(activePill);

        var secretRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        secretRow.Children.Add(new StackPanel
        {
            Spacing = 1,
            Children =
            {
                TeamKit.Caption("Signing secret"),
                TeamKit.Mono("whsec_••••••••••••4f2a")
            }
        });
        var roll = TeamKit.Btn("Roll secret", ArcButtonVariant.Ghost, ArcButtonSize.Sm, icon: "refresh-cw");
        Grid.SetColumn(roll, 1);
        secretRow.Children.Add(roll);

        var endpoint = TeamKit.Panel(TeamKit.Stack(10,
            endpointHeader,
            TeamKit.Row(8,
                TeamKit.Pill("invoice.paid"),
                TeamKit.Pill("seat.created"),
                TeamKit.Pill("member.invited"),
                TeamKit.Pill("+2 more")),
            TeamKit.Divider(),
            secretRow), 14, 16);

        var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        stats.Children.Add(TeamKit.Stat($"{success:0.0}%", "Success rate", "Last 24 hours"));
        var p95 = TeamKit.Stat("240 ms", "p95 latency", "Across all endpoints");
        Grid.SetColumn(p95, 1);
        stats.Children.Add(p95);

        RenderHistory();
        RenderInspector();

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "Webhooks", Classes = { "arc-heading" } },
                TeamKit.Caption("1 endpoint · 5 recent deliveries · signed with a rolling secret")
            }
        });
        var add = TeamKit.Btn("Add endpoint", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "plus");
        Grid.SetColumn(add, 1);
        heading.Children.Add(add);

        var left = TeamKit.Stack(12,
            endpoint,
            TeamKit.Row(10, TeamKit.Label("Delivery history"), filterSwitch),
            historyHost);
        var right = TeamKit.Stack(10,
            TeamKit.Label("Inspector"),
            inspectorHost,
            TeamKit.Divider(),
            stats,
            status);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,420") };
        body.Children.Add(left);
        var rightHolder = new Border
        {
            Margin = new Thickness(24, 0, 0, 0),
            Child = right
        };
        Grid.SetColumn(rightHolder, 1);
        body.Children.Add(rightHolder);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(20),
            Child = TeamKit.Stack(14, heading, TeamKit.Divider(), body)
        });
    }
}
