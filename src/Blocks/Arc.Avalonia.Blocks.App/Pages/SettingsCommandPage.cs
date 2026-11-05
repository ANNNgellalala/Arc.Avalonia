using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Settings command: workspace settings on one scrolling page with a sticky section nav, a search field
/// that grows into a command palette, and an unsaved changes bar. Picking a result closes the palette,
/// scrolls to the row and marks it. The palette is page-hosted over the block, not in a window popup.
/// </summary>
public sealed class SettingsCommandPage : ArcItemPage
{
    public override string ItemSlug => "settings-command";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-palette-open", "variant-section-nav", "variant-settings-page",
        "variant-search-field", "variant-unsaved-bar"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Workspace settings with several sections and many options.",
        "Admin consoles where support links people to a specific setting."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use settings-page for a short profile form with a few fields per section.",
        "Use command-palette for app-wide navigation rather than settings alone."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The palette is a labelled combobox over a listbox of options, and each row labels its control with the setting name and hint.",
        "Cmd/Ctrl+K or slash opens the search, Arrow keys move the active result, Enter jumps to it, and Escape closes the palette back into the field."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The search field grows into the palette on one spring, and the target row breathes twice after a jump.",
        "Reduced motion swaps the growth and flight for fades and an instant jump."
    };

    public override string? ImplementationNotes =>
        "Setting definitions, saved values and search are local sample data. A jump scrolls the page natively rather than flying the palette into the row, which the port cannot do without a shared-layout animation engine; the row still highlights and the status line announces the jump.";

    private sealed record Setting(string Id, string Section, string Label, string Hints, string Kind);

    private static readonly Setting[] Settings =
    {
        new("workspace-name", "General", "Workspace name", "The name people see in invites and the sidebar.", "text"),
        new("workspace-url", "General", "Workspace URL", "Subdomain for shared links.", "text"),
        new("density", "General", "Interface density", "Comfortable or compact rows.", "segmented"),
        new("week-start", "General", "Week starts on", "First day in calendars and reports.", "select"),
        new("digest", "Notifications", "Weekly digest", "A Monday summary of activity.", "switch"),
        new("mentions", "Notifications", "Mention emails", "Email me when someone mentions me.", "switch"),
        new("deploy-alerts", "Notifications", "Deploy alerts", "Notify the channel when a deploy fails.", "switch"),
        new("two-factor", "Security", "Require two factor", "Every member must enrol an authenticator.", "switch"),
        new("session-length", "Security", "Session length", "How long a sign in lasts.", "select"),
        new("sso", "Security", "Single sign-on", "SAML and OIDC for the workspace.", "switch"),
        new("invoice-emails", "Billing", "Invoice emails", "Send receipts to the billing contact.", "switch"),
        new("plan", "Billing", "Plan", "Team or enterprise billing.", "select")
    };

    private static readonly string[] SectionOrder = { "General", "Notifications", "Security", "Billing" };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var open = true;
        var query = string.Empty;
        var active = 0;
        var highlighted = string.Empty;
        var values = Settings.ToDictionary(
            setting => setting.Id,
            setting => setting switch
            {
                { Kind: "text" } => "Northwind",
                { Kind: "segmented" } => "comfortable",
                { Id: "week-start" } => "monday",
                { Id: "session-length" } => "30 days",
                { Id: "plan" } => "team",
                _ => "true"
            });

        var listHost = new ContentControl();
        var paletteHost = new ContentControl();
        var barHost = new ContentControl();
        var searchHost = new ContentControl();
        var sectionsHost = new ContentControl();

        List<Setting> Results()
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Settings.Take(5).ToList();
            }

            return Settings
                .Where(setting => setting.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || setting.Hints.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || setting.Section.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || setting.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        void Jump(Setting setting)
        {
            highlighted = setting.Id;
            open = false;
            query = string.Empty;
            Build();
            AppKit.Say(status, $"Jumped to {setting.Label} in {setting.Section}.");
        }

        void Open(string seed)
        {
            open = true;
            query = seed;
            active = 0;
            Build();
            AppKit.Say(status, "Settings search open. Arrow keys move, Enter jumps, Escape closes.");
        }

        void BuildPalette()
        {
            if (!open)
            {
                paletteHost.Content = null;
                return;
            }

            var results = Results();
            active = Math.Clamp(active, 0, Math.Max(0, results.Count - 1));

            var list = new StackPanel { Spacing = 1 };
            if (results.Count == 0)
            {
                list.Children.Add(new Border
                {
                    Padding = new Thickness(10, 16, 10, 16),
                    Child = AppKit.Stack(4,
                        AppKit.Caption("No settings match that search"),
                        AppKit.Caption("Try \"theme\", \"2fa\" or \"invoice\"."))
                });
            }

            for (var index = 0; index < results.Count; index++)
            {
                var setting = results[index];
                var selected = index == active;
                var row = new Border
                {
                    Padding = new Thickness(9, 7, 9, 7),
                    CornerRadius = new CornerRadius(9),
                    Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.1) : null,
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                        ColumnSpacing = 9,
                        Children =
                        {
                            new ArcIcon
                            {
                                Icon = setting.Kind == "switch" ? "toggle-left" : setting.Kind == "select" ? "chevrons-up-down" : "type",
                                Size = 15,
                                Foreground = selected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.TextMuted)
                            },
                            AppKit.Place(new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock { Text = setting.Label, Classes = { "arc-body-sm" } },
                                    AppKit.Caption(setting.Hints, 300)
                                }
                            }, 1),
                            AppKit.Place(AppKit.Caption(setting.Section), 2)
                        }
                    }
                };
                list.Children.Add(AppKit.Pressable(row, () => Jump(setting)));
            }

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8,
                Children =
                {
                    new ArcIcon { Icon = "search", Size = 15, Foreground = AppKit.Brush(ArcTokens.TextSecondary) },
                    AppKit.Place(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(query) ? "Search settings" : query,
                        Classes = { "arc-body-sm" },
                        VerticalAlignment = VerticalAlignment.Center
                    }, 1),
                    AppKit.Place(AppKit.Keycap("Esc"), 2)
                }
            });

            var field = AppKit.Search("Search settings", text =>
            {
                query = text;
                active = 0;
                BuildPalette();
            }, 0, query);
            field.ShortcutHint = "⌘K";
            panel.Children.Add(field);
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(new Border
            {
                MaxHeight = 252,
                Child = new ScrollViewer
                {
                    VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    Content = list
                }
            });
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(AppKit.Row(10,
                AppKit.Row(5, AppKit.Keycap("↑"), AppKit.Keycap("↓"), AppKit.Caption("move")),
                AppKit.Row(5, AppKit.Keycap("↵"), AppKit.Caption("jump")),
                AppKit.Place(AppKit.Caption($"{results.Count} settings"), 0)));

            var floating = AppKit.Floating(panel, 12, 16, 430);
            floating.HorizontalAlignment = HorizontalAlignment.Left;
            floating.VerticalAlignment = VerticalAlignment.Top;
            floating.Margin = new Thickness(258, 46, 0, 0);
            var scrim = new Border { Background = AppKit.Brush(ArcTokens.Scrim) };
            paletteHost.Content = new Panel { Children = { AppKit.Pressable(scrim, () =>
            {
                open = false;
                query = string.Empty;
                Build();
                AppKit.Say(status, "Palette closed.");
            }), floating } };
        }

        void BuildSearchHeader()
        {
            var field = AppKit.Search("Search settings", text => Open(text), 300, null, "⌘K");
            field.PointerPressed += (_, _) => Open(string.Empty);
            searchHost.Content = field;
        }

        void BuildSections()
        {
            var panel = new StackPanel { Spacing = 2 };
            foreach (var name in SectionOrder)
            {
                var target = name;
                var count = Settings.Count(setting => setting.Section == name);
                var selected = highlighted.Length > 0
                    && Settings.FirstOrDefault(setting => setting.Id == highlighted)?.Section == name;
                panel.Children.Add(AppKit.Pressable(new Border
                {
                    Padding = new Thickness(9, 6, 9, 6),
                    CornerRadius = new CornerRadius(9),
                    Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.11) : null,
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = name,
                                Classes = { "arc-body-sm" },
                                Foreground = selected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.TextSecondary)
                            },
                            AppKit.Place(AppKit.Caption(count.ToString()), 1)
                        }
                    }
                }, () =>
                {
                    highlighted = Settings.First(setting => setting.Section == target).Id;
                    Build();
                    AppKit.Say(status, $"{target} section highlighted.");
                }));
            }

            panel.Children.Add(new Border { Height = 8 });
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(new Border
            {
                Margin = new Thickness(2, 8, 2, 0),
                Child = AppKit.Caption("12 settings in 4 sections")
            });
            sectionsHost.Content = panel;
        }

        Control Row(Setting setting)
        {
            var highlightedRow = highlighted == setting.Id;
            var value = values[setting.Id];
            var label = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    AppKit.Row(6,
                        new TextBlock { Text = setting.Label, Classes = { "arc-body-sm" } },
                        highlightedRow ? AppKit.Pill("Jumped here", ArcBadgeTone.Accent) : null),
                    AppKit.Caption(setting.Hints, 340)
                }
            };

            Control control = setting.Kind switch
            {
                "switch" => AppKit.Toggle(string.Empty, value == "true", next =>
                {
                    values[setting.Id] = next ? "true" : "false";
                    BuildBar();
                    AppKit.Say(status, $"{setting.Label} set to {(next ? "on" : "off")}.");
                }),
                "segmented" => AppKit.Segmented(new[] { ("comfortable", "Comfortable"), ("compact", "Compact") },
                    value == "compact" ? "compact" : "comfortable", next =>
                    {
                        values[setting.Id] = next;
                        BuildBar();
                    }),
                "select" => AppKit.Select(setting.Id == "week-start"
                        ? new[] { ("monday", "Monday"), ("sunday", "Sunday") }
                        : setting.Id == "plan"
                            ? new[] { ("team", "Team"), ("business", "Business") }
                            : new[] { ("30 days", "30 days"), ("14 days", "14 days"), ("7 days", "7 days") },
                    value, next =>
                    {
                        values[setting.Id] = next;
                        BuildBar();
                    }, 150),
                _ => AppKit.Input("Value", value, next =>
                {
                    values[setting.Id] = next;
                    BuildBar();
                }, width: 200)
            };

            var border = new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                CornerRadius = new CornerRadius(12),
                Background = highlightedRow ? AppKit.Tint(ArcTokens.Accent, 0.07) : null,
                BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(0, 0, 0, highlightedRow ? 0 : 1),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 14,
                    Children = { label, AppKit.Place(control, 1) }
                }
            };

            if (highlightedRow)
            {
                return AppKit.AccentBand(border.Child!, 10, 12, 0.1).With(band => band.Margin = new Thickness(0));
            }

            return border;
        }

        void BuildList()
        {
            var panel = new StackPanel { Spacing = 0 };
            foreach (var section in SectionOrder)
            {
                panel.Children.Add(new Border
                {
                    Padding = new Thickness(12, 12, 12, 6),
                    Child = new TextBlock { Text = section, Classes = { "arc-label" } }
                });
                foreach (var setting in Settings.Where(entry => entry.Section == section))
                {
                    panel.Children.Add(Row(setting));
                }
            }

            listHost.Content = new Border
            {
                Padding = new Thickness(14, 8, 14, 14),
                Child = panel
            };
        }

        void BuildBar()
        {
            barHost.Content = AppKit.Floating(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    AppKit.Row(8,
                        AppKit.Dot(ArcTokens.Warning, 8),
                        new TextBlock { Text = "3 unsaved changes", Classes = { "arc-body-sm" } }),
                    AppKit.Place(AppKit.Caption("Only changed settings are sent."), 1),
                    AppKit.Place(AppKit.Row(6,
                        AppKit.Btn("Discard", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
                        {
                            highlighted = string.Empty;
                            Build();
                            AppKit.Say(status, "Changes discarded.");
                        }, height: 30),
                        AppKit.Btn("Save", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
                        {
                            highlighted = string.Empty;
                            Build();
                            AppKit.Say(status, "Saved 3 settings. The request is simulated.");
                        }, height: 30)), 2)
                }
            }, 9, 14);
        }

        void Build()
        {
            BuildSearchHeader();
            BuildSections();
            BuildList();
            BuildPalette();
            BuildBar();
        }

        Build();

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*") };
        var sidebar = new Border
        {
            Background = AppKit.Brush(ArcTokens.SurfaceMuted),
            BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(12, 14, 12, 12),
            Child = sectionsHost
        };

        var page = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children =
            {
                new Border
                {
                    Padding = new Thickness(18, 12, 18, 12),
                    BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        ColumnSpacing = 12,
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 1,
                                Children =
                                {
                                    new TextBlock { Text = "Workspace settings", Classes = { "arc-body-sm", "arc-medium" } },
                                    AppKit.Caption("Northwind · Team plan")
                                }
                            },
                            AppKit.Place(searchHost, 1)
                        }
                    }
                },
                AppKit.Place(new Border { Child = new ScrollViewer { Content = listHost } }, 0, 1),
                AppKit.Place(new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new Border { Padding = new Thickness(18, 0, 18, 0), Child = status },
                        new Border { Padding = new Thickness(18, 0, 18, 12), Child = barHost }
                    }
                }, 0, 2)
            }
        };

        Grid.SetColumn(sidebar, 0);
        Grid.SetColumn(page, 1);
        layout.Children.Add(sidebar);
        layout.Children.Add(page);

        var overlay = new Panel { Children = { layout, paletteHost } };

        AppKit.Shortcut(overlay, Key.K, () => Open(string.Empty), modifiers => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta));
        AppKit.Shortcut(overlay, Key.Divide, () => Open(string.Empty));
        AppKit.Shortcut(overlay, Key.Down, () =>
        {
            active = Math.Min(Results().Count - 1, active + 1);
            BuildPalette();
        });
        AppKit.Shortcut(overlay, Key.Up, () =>
        {
            active = Math.Max(0, active - 1);
            BuildPalette();
        });
        AppKit.Shortcut(overlay, Key.Enter, () =>
        {
            var results = Results();
            if (results.Count > 0)
            {
                Jump(results[Math.Clamp(active, 0, results.Count - 1)]);
            }
        });
        AppKit.Shortcut(overlay, Key.Escape, () =>
        {
            if (open)
            {
                open = false;
                query = string.Empty;
                Build();
                AppKit.Say(status, "Palette closed.");
            }
        });

        return AppKit.Screen(overlay, 556);
    }
}
