using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Media.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.BlocksCommerce.CommerceKit;

namespace Arc.Avalonia.BlocksCommerce.Pages;

/// <summary>
/// Integrations: a directory with search, category filters that recount as connections change, Connect
/// buttons that become Connected, and an inline settings strip for sync frequency and a held disconnect.
/// Providers and connections are sample data; no OAuth flow is started.
/// </summary>
public sealed class IntegrationsPage : ArcItemPage
{
    public override string ItemSlug => "integrations";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Settings or marketplace pages for third party connections.",
        "Directories with search, category filters and a safe disconnect."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Outgoing event endpoints or developer credentials; use webhooks or api-keys."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Filters are a single-select group with pressed state, and results and connection changes are announced politely.",
        "Disconnect uses a hold so it cannot fire from a stray click; the sync select is a labelled field."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Connect swaps to Connected in place and the filter counts recount as you go.",
        "Reduced motion replaces the morph and the card reflow with instant changes."
    };

    public override string? ImplementationNotes =>
        "Providers, descriptions and connection state are sample data. Connecting does not start an OAuth flow and disconnecting does not revoke a token. The reference opens settings in a drawer; the port shows an inline settings strip under the grid so the page stays within the shot budget.";

    private sealed record Provider(string Id, string Name, string Description, string Category, string Icon, bool Connected);

    private static readonly Provider[] Seed =
    {
        new("slack", "Slack", "Post release notes into a channel", "Communication", "hash", true),
        new("notion", "Notion", "Sync specs and meeting notes", "Productivity", "file", true),
        new("stripe", "Stripe", "Send invoices and reconcile payouts", "Payments", "credit-card", false),
        new("linear", "Linear", "Link issues to releases automatically", "Productivity", "layers", false),
        new("github", "GitHub", "Connect pull requests to changelog entries", "Developer", "code", true),
        new("hubspot", "HubSpot", "Push billing events to the CRM", "Payments", "target", false)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var providers = Seed.ToList();
        var category = "All";
        var query = string.Empty;
        var selected = providers.First(provider => provider.Connected);
        var status = Ck.Status();

        var gridHost = new ContentControl();
        var countHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var settingsHost = new ContentControl();

        void BuildSettings()
        {
            if (!selected.Connected)
            {
                settingsHost.Content = null;
                return;
            }

            var sync = Ck.Select(new[]
            {
                ("realtime", "Sync in real time"),
                ("hourly", "Sync every hour"),
                ("daily", "Sync once a day")
            }, "hourly", value => Ck.Say(status, $"{selected.Name} sync set to {value}."), 210);

            var disconnect = new ArcHoldToConfirm
            {
                Label = "Hold to disconnect",
                ConfirmedLabel = "Disconnected",
                Tone = ArcHoldToConfirmTone.Danger,
                Duration = 900,
                VerticalAlignment = VerticalAlignment.Center
            };
            var target = selected;
            disconnect.Confirmed += (_, _) =>
            {
                providers[providers.IndexOf(target)] = target with { Connected = false };
                selected = providers.FirstOrDefault(provider => provider.Connected) ?? target with { Connected = false };
                BuildGrid();
                BuildSettings();
                Ck.Say(status, $"{target.Name} disconnected. Tokens are not revoked in this preview.");
            };

            settingsHost.Content = Ck.Tile(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            Ck.Text($"{selected.Name} settings", "arc-body-sm arc-medium"),
                            Ck.Caption("Connected by you · tokens stored securely")
                        }
                    },
                    Place(Ck.Field("Sync frequency", sync), 1),
                    Place(disconnect, 2)
                }
            }, 12);
        }

        void BuildGrid()
        {
            var matches = providers
                .Where(provider => category == "All" || provider.Category == category)
                .Where(provider => string.IsNullOrWhiteSpace(query) ||
                                   provider.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   provider.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            countHost.Children.Clear();
            countHost.Children.Add(Ck.Caption($"{matches.Count} of {providers.Count} integrations"));
            countHost.Children.Add(Ck.Caption("·"));
            countHost.Children.Add(Ck.Caption($"{providers.Count(provider => provider.Connected)} connected"));

            if (matches.Count == 0)
            {
                gridHost.Content = Ck.Tile(new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        Ck.Disc("search", 32, 15),
                        Ck.Text("No integrations match that search", "arc-body-sm arc-medium"),
                        Ck.Link("Clear the search", () =>
                        {
                            query = string.Empty;
                            BuildGrid();
                        })
                    }
                }, 14);
                return;
            }

            var cards = new List<Control>();
            foreach (var provider in matches)
            {
                var target = provider;
                var connected = provider.Connected;
                var connect = Ck.Btn(connected ? "Connected" : "Connect",
                    connected ? ArcButtonVariant.Ghost : ArcButtonVariant.Secondary,
                    ArcButtonSize.Sm,
                    connected ? null : () =>
                    {
                        providers[providers.IndexOf(target)] = target with { Connected = true };
                        selected = target with { Connected = true };
                        BuildGrid();
                        BuildSettings();
                        Ck.Say(status, $"{target.Name} connected. Tokens are simulated.");
                    },
                    icon: connected ? "check" : "plus",
                    height: 32);
                if (connected)
                {
                    connect.Click += (_, _) =>
                    {
                        selected = target;
                        BuildSettings();
                        Ck.Say(status, $"{target.Name} settings opened.");
                    };
                }

                cards.Add(new Border
                {
                    Background = Ck.Brush(ArcTokens.SurfaceRaised),
                    BorderBrush = Ck.Brush(ArcTokens.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Grid
                            {
                                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                                ColumnSpacing = 8,
                                Children =
                                {
                                    Ck.Mark(provider.Icon, 34, 16),
                                    Place(new StackPanel
                                    {
                                        Spacing = 0,
                                        Children =
                                        {
                                            Ck.Text(provider.Name, "arc-body-sm arc-medium"),
                                            Ck.Caption(provider.Category)
                                        }
                                    }, 1),
                                    Place(connected
                                        ? new Border
                                        {
                                            Width = 7,
                                            Height = 7,
                                            CornerRadius = new CornerRadius(4),
                                            Background = Ck.Brush(ArcTokens.Success),
                                            VerticalAlignment = VerticalAlignment.Center
                                        }
                                        : new Border { Width = 0 }, 2)
                                }
                            },
                            Ck.Caption(provider.Description),
                            connect
                        }
                    }
                });
            }

            gridHost.Content = Ck.GridOf(3, 10, cards);
        }

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var option in new[] { "All", "Communication", "Productivity", "Payments", "Developer" })
        {
            var value = option;
            var count = option == "All"
                ? providers.Count
                : providers.Count(provider => provider.Category == option);
            chips.Children.Add(Ck.Chip(option, category == option, () =>
            {
                category = value;
                BuildGrid();
                Ck.Say(status, $"{value} selected, {count} integrations.");
            }, count.ToString()));
        }

        BuildGrid();
        BuildSettings();

        var search = Ck.Search("Search integrations", text =>
        {
            query = text;
            BuildGrid();
        }, 240);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ck.Heading("Integrations", 380, "arc-subtitle"),
                Ck.Caption("Connect the tools your team already uses")
            }
        });
        search.VerticalAlignment = VerticalAlignment.Top;
        header.Children.Add(Place(search, 1));

        var filterRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        filterRow.Children.Add(chips);
        countHost.HorizontalAlignment = HorizontalAlignment.Right;
        filterRow.Children.Add(Place(countHost, 1));

        return Ck.Specimen(new StackPanel
        {
            Spacing = 12,
            Children = { header, filterRow, gridHost, settingsHost, status }
        }, 16);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
