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
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// MCP servers: the tool servers an agent can call, what their enabled tools cost in context, and a switch per
/// tool. Turning tools on or off moves the context bar and the totals.
/// </summary>
public sealed class McpServersPage : ArcItemPage
{
    public override string ItemSlug => "mcp-servers";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-server-row",
        "variant-tools",
        "variant-context-meter",
        "variant-directory"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Settings for an agent that connects to MCP servers: which servers it has, which tools are on and their cost.",
        "Onboarding a new integration with a directory, sign in and tool discovery in one place.",
        "Keeping tool definitions inside a context budget by switching off tools the agent does not need."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use agent approvals to decide individual tool calls, or knowledge base for retrieved documents."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each server row is a button with an expanded state named with its status, transport, tools and token cost.",
        "Every tool switch is named with the tool, its read or write access and its token cost.",
        "The context bar is a meter with a value text, and going over budget adds how far over it is."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Toggling a tool rolls the counts and resizes its segment on a critically damped spring.",
        "Reduced motion places the new numbers and segments immediately and keeps every announcement."
    };

    public override string? ImplementationNotes =>
        "Token counts, endpoints and the directory are sample values. The Add server sheet, authorize step and "
        + "discovery flow are represented by the status rows; context is a simple segmented meter.";

    private sealed class Tool
    {
        public string Name { get; init; } = string.Empty;
        public string Access { get; init; } = "read";
        public int Tokens { get; init; }
        public bool Enabled { get; set; } = true;
    }

    private sealed class Server
    {
        public string Name { get; init; } = string.Empty;
        public string Icon { get; init; } = "server";
        public string Transport { get; init; } = "remote";
        public string Endpoint { get; init; } = string.Empty;
        public string Status { get; init; } = "connected";
        public string? Error { get; init; }
        public List<Tool> Tools { get; init; } = new();
        public string Permission { get; set; } = "ask";
    }

    protected override Control BuildDemo(ArcDemoContext context)
    {
        const int budget = 4000;
        var servers = new List<Server>
        {
            new()
            {
                Name = "GitHub", Icon = "code", Endpoint = "https://api.githubcopilot.com/mcp/",
                Tools = new List<Tool>
                {
                    new() { Name = "search_code", Access = "read", Tokens = 420 },
                    new() { Name = "create_pull_request", Access = "write", Tokens = 640 },
                    new() { Name = "merge_pull_request", Access = "write", Tokens = 360, Enabled = false }
                }
            },
            new()
            {
                Name = "Linear", Icon = "list", Endpoint = "https://mcp.linear.app/sse",
                Tools = new List<Tool>
                {
                    new() { Name = "list_issues", Access = "read", Tokens = 380 },
                    new() { Name = "create_issue", Access = "write", Tokens = 520 }
                }
            },
            new()
            {
                Name = "PostgreSQL", Icon = "database", Transport = "local", Status = "error",
                Error = "tools/list timed out after 20s", Endpoint = "stdio: pg-mcp",
                Tools = new List<Tool>
                {
                    new() { Name = "query", Access = "write", Tokens = 720 },
                    new() { Name = "list_tables", Access = "read", Tokens = 260 }
                }
            }
        };

        var expanded = "GitHub";
        var status = K.Caption("GitHub and Linear are connected. Postgres needs a retry. Values are simulated.");

        var list = new StackPanel { Spacing = 3 };
        var meterHost = new Panel();
        var meterLabel = K.Text(string.Empty, "arc-body-sm");
        var meterBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Height = 8 };

        void RefreshMeter()
        {
            var connected = servers.Where(server => server.Status == "connected").ToList();
            var enabled = connected.SelectMany(server => server.Tools).Where(tool => tool.Enabled).ToList();
            var used = enabled.Sum(tool => tool.Tokens);
            meterLabel.Text = $"{enabled.Count} tools · {used / 1000.0:0.00}k of {budget / 1000.0:0.0}k tokens";

            meterBar.Children.Clear();
            meterBar.Children.Add(new Border { Width = 2 });
            for (var index = 0; index < connected.Count; index++)
            {
                var server = connected[index];
                var tokens = server.Tools.Where(tool => tool.Enabled).Sum(tool => tool.Tokens);
                var width = Math.Max(0, Math.Round(tokens / (double)budget * 620));
                meterBar.Children.Add(new Border
                {
                    Width = width,
                    Height = 8,
                    CornerRadius = new CornerRadius(2),
                    Background = K.Brush(K.SeriesToken(index))
                });
            }

            var remainder = Math.Max(4, 620 - meterBar.Children.OfType<Border>().Sum(border => border.Width));
            meterBar.Children.Add(new Border
            {
                Width = remainder,
                Height = 8,
                CornerRadius = new CornerRadius(2),
                Background = K.Brush(ArcTokens.Border)
            });
            meterHost.Children.Clear();
            meterHost.Children.Add(meterBar);
        }

        void Render()
        {
            list.Children.Clear();
            foreach (var server in servers)
            {
                var item = server;
                var isOpen = expanded == item.Name;
                var connected = item.Status == "connected";
                var enabledTools = item.Tools.Count(tool => tool.Enabled);
                var tokens = item.Tools.Where(tool => tool.Enabled).Sum(tool => tool.Tokens);
                var statusToken = item.Status switch
                {
                    "connected" => ArcTokens.Success,
                    "needs-auth" => ArcTokens.Warning,
                    _ => ArcTokens.Danger
                };
                var statusWord = item.Status switch
                {
                    "connected" => "Connected",
                    "needs-auth" => "Needs sign in",
                    _ => "Error"
                };

                var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                head.Children.Add(K.Disc(item.Icon, 24, false, 13));
                var title = K.Stack(1,
                    K.Row(6,
                        K.Text(item.Name, "arc-body-sm"),
                        new ArcIcon { Icon = item.Status == "connected" ? "circle-check" : item.Status == "error" ? "circle-x" : "lock",
                            Size = 11, Foreground = K.Brush(statusToken) },
                        K.Caption(statusWord),
                        K.Caption(item.Transport)),
                    K.Caption(connected ? $"{enabledTools} of {item.Tools.Count} tools · {tokens / 1000.0:0.0}k tokens" : item.Error ?? "Signed in account required"));
                title.Margin = new Thickness(9, 0, 9, 0);
                Grid.SetColumn(title, 1);
                head.Children.Add(title);
                var right = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                if (item.Status == "needs-auth")
                {
                    right.Children.Add(K.Chip("Sign in", icon: "lock"));
                }
                else if (item.Status == "error")
                {
                    right.Children.Add(K.Chip("Retry", icon: "refresh-cw"));
                }

                right.Children.Add(new ArcIcon
                {
                    Icon = isOpen ? "chevron-up" : "chevron-down",
                    Size = 12,
                    Foreground = K.Brush(ArcTokens.TextMuted)
                });
                Grid.SetColumn(right, 2);
                head.Children.Add(right);

                var body = new StackPanel { Spacing = 6, Children = { head } };
                if (isOpen)
                {
                    var tools = new StackPanel { Spacing = 2 };
                    foreach (var tool in item.Tools)
                    {
                        var toolItem = tool;
                        var accessIcon = tool.Access == "read" ? "eye" : "pencil";
                        var rowSwitch = K.Toggle(string.Empty, tool.Enabled, on =>
                        {
                            toolItem.Enabled = on;
                            RefreshMeter();
                            Render();
                            status.Text = $"{toolItem.Name} {(on ? "on" : "off")}.";
                        });
                        AutomationProperties.SetName(rowSwitch, $"{tool.Name}, {tool.Access}, {tool.Tokens} tokens");

                        var toolRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                        var toolInfo = K.Row(8,
                            new ArcIcon { Icon = accessIcon, Size = 11, Foreground = K.Brush(ArcTokens.TextMuted) },
                            K.Caption(tool.Access),
                            K.Mono(tool.Name, 11.5),
                            K.Caption($"{tool.Tokens} tok"));
                        toolInfo.VerticalAlignment = VerticalAlignment.Center;
                        toolRow.Children.Add(toolInfo);
                        Grid.SetColumn(rowSwitch, 1);
                        rowSwitch.VerticalAlignment = VerticalAlignment.Center;
                        toolRow.Children.Add(rowSwitch);
                        tools.Children.Add(toolRow);
                    }

                    body.Children.Add(K.Divider(4));
                    body.Children.Add(tools);
                    body.Children.Add(K.Divider(4));
                    body.Children.Add(K.Row(8,
                        K.Caption("Tool calls"),
                        K.Segmented(new[] { ("ask", "Ask every time"), ("reads", "Always allow reads") },
                            item.Permission == "ask" ? "ask" : "reads",
                            value =>
                            {
                                item.Permission = value;
                                status.Text = value == "ask"
                                    ? $"{item.Name} asks before every tool call."
                                    : $"{item.Name} allows read tools without asking.";
                            },
                            "Tool calls")));
                    body.Children.Add(K.Row(8,
                        K.Mono(item.Endpoint, 10.5),
                        K.Chip("Remove", icon: "trash-2")));
                }

                var card = new Border
                {
                    Background = isOpen ? K.Tint(ArcTokens.Accent, 0.04) : K.Brush(ArcTokens.Surface),
                    BorderBrush = isOpen ? K.Tint(ArcTokens.Accent, 0.26) : K.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10, 7, 10, 7),
                    Child = body
                };
                card.Focusable = true;
                card.Cursor = new Cursor(StandardCursorType.Hand);
                AutomationProperties.SetName(card, $"{item.Name}, {statusWord}, {item.Transport}, {enabledTools} tools, {tokens} tokens");
                var nameKey = item.Name;
                card.PointerPressed += (_, _) =>
                {
                    expanded = isOpen ? string.Empty : nameKey;
                    Render();
                };

                list.Children.Add(card);
            }
        }

        var addServer = K.Btn("Add server", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
        {
            status.Text = "Add server sheet opened. The directory and authorize step are described in the notes.";
        }, icon: "plus");

        Render();
        RefreshMeter();

        var content = new StackPanel
        {
            Spacing = 8,
            MaxWidth = 760,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(3,
                            K.Text("MCP servers", "arc-heading"),
                            K.Caption("Tool definitions count against the agent's context")),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 4,
                            Children = { addServer, K.IconBtn("settings", "MCP settings") }
                        }
                    }
                },
                new Border
                {
                    Background = K.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(11, 8, 11, 9),
                    Child = K.Stack(6, new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { meterLabel, K.Caption($"Budget {budget / 1000.0:0.0}k") } }, meterHost)
                },
                list,
                status
            }
        };

        if (content.Children[0] is Grid head)
        {
            Grid.SetColumn(head.Children[1], 1);
        }

        if (content.Children[1] is Border meterFrame && meterFrame.Child is StackPanel meterStack
            && meterStack.Children[0] is Grid meterHead)
        {
            Grid.SetColumn(meterHead.Children[1], 1);
        }

        return K.Surface(new Border
        {
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 860
        }, new Thickness(18, 10, 18, 10));
    }
}
