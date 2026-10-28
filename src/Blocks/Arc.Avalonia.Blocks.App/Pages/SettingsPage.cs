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
/// Settings page: account settings with a gliding section nav and a save bar that morphs in as soon as a
/// field changes. Values start from sample data and saving only updates local state.
/// </summary>
public sealed class SettingsPage : ArcItemPage
{
    public override string ItemSlug => "settings-page";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Account or workspace settings split into a few sections.",
        "Forms that should show a save bar only when something changed."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use security-settings for sessions, two factor and passwords.",
        "Use buttons or inputs alone when the form has a single field."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The section nav is a labelled list; fields keep visible labels and hints, and the save bar reports its change count.",
        "Arrow keys move between sections, Home and End jump to the ends, and Cmd or Ctrl+S saves the pending changes."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The active section indicator glides between nav rows and the save bar grows out of the bottom edge as you edit.",
        "Reduced motion makes the glide and the save bar entrance instant."
    };

    public override string? ImplementationNotes =>
        "Profile values and the billing summary are sample data. Saving, discarding and the photo upload only update local state; the form is not validated against a server.";

    private sealed record Section(string Id, string Label, string Icon, string Description);

    private static readonly Section[] Sections =
    {
        new("profile", "Profile", "user", "Your name, photo and contact details."),
        new("notifications", "Notifications", "bell", "What we send and where."),
        new("security", "Security", "shield", "Password and sessions."),
        new("billing", "Billing", "credit-card", "Plan and receipts.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var section = "profile";
        var name = "Maya Chen";
        var email = "maya@northwind.co";
        var timezone = "Europe/Lisbon";
        var density = "comfortable";
        var weekly = true;
        var mentions = true;
        var product = false;
        var changed = 1;

        var bodyHost = new ContentControl();
        var navHost = new ContentControl();
        var saveHost = new ContentControl();

        void Touch(string what)
        {
            changed = Math.Min(9, changed + 1);
            Build();
            AppKit.Say(status, $"{what} updated. {changed} unsaved changes.");
        }

        void Revert()
        {
            name = "Maya Chen";
            email = "maya@northwind.co";
            timezone = "Europe/Lisbon";
            density = "comfortable";
            weekly = true;
            mentions = true;
            product = false;
            changed = 0;
            Build();
            AppKit.Say(status, "Changes discarded.");
        }

        void Save()
        {
            changed = 0;
            Build();
            AppKit.Say(status, "Settings saved. The request is simulated.");
        }

        void BuildNav()
        {
            var panel = new StackPanel { Spacing = 2 };
            foreach (var entry in Sections)
            {
                var target = entry;
                var selected = entry.Id == section;
                var row = new Border
                {
                    Padding = new Thickness(9, 7, 9, 7),
                    CornerRadius = new CornerRadius(10),
                    Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.11) : null,
                    Child = AppKit.Row(9,
                        new ArcIcon
                        {
                            Icon = entry.Icon,
                            Size = 15,
                            Foreground = selected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.TextMuted)
                        },
                        new TextBlock
                        {
                            Text = entry.Label,
                            Classes = { "arc-body-sm" },
                            Foreground = selected ? AppKit.Brush(ArcTokens.Foreground) : AppKit.Brush(ArcTokens.TextSecondary)
                        })
                };
                panel.Children.Add(AppKit.Pressable(row, () =>
                {
                    section = target.Id;
                    Build();
                    AppKit.Say(status, $"{target.Label} section selected.");
                }));
            }

            panel.Children.Add(new Border { Height = 8 });
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(new Border
            {
                Margin = new Thickness(2, 8, 2, 0),
                Child = AppKit.Stack(6,
                    AppKit.Caption("Need a hand?"),
                    AppKit.Link("Read the settings guide", () => AppKit.Say(status, "Docs link is simulated."), trailingIcon: "arrow-right"))
            });

            navHost.Content = panel;
        }

        Control ProfileForm()
        {
            var panel = new StackPanel { Spacing = 14 };
            panel.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    AppKit.Person("Maya Chen", ArcAvatarSize.Lg),
                    AppKit.Place(new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            new TextBlock { Text = "Profile photo", Classes = { "arc-label" } },
                            AppKit.Caption("PNG or JPG, at least 256 px square.")
                        }
                    }, 1),
                    AppKit.Place(AppKit.Row(6,
                        AppKit.Btn("Change photo", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => AppKit.Say(status, "The file picker is simulated."), height: 32),
                        AppKit.Btn("Remove", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () => AppKit.Say(status, "Photo removal is simulated."), height: 32)), 2)
                }
            });

            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(AppKit.Columns("*,*",
                AppKit.Field("Full name", AppKit.Input("Your name", name, value =>
                {
                    name = value;
                    if (changed > 1)
                    {
                        changed = 1;
                    }

                    BuildSaveBar();
                }), "Shown to everyone in the workspace."),
                AppKit.Field("Email", AppKit.Input("you@company.com", email, value =>
                {
                    email = value;
                    if (changed > 1)
                    {
                        changed = 1;
                    }

                    BuildSaveBar();
                }), "Used for sign in and notifications.")));

            panel.Children.Add(AppKit.Columns("*,*",
                AppKit.Field("Time zone", AppKit.Select(new[]
                {
                    ("Europe/Lisbon", "Lisbon (GMT+1)"),
                    ("Europe/Berlin", "Berlin (GMT+2)"),
                    ("America/New_York", "New York (GMT-4)")
                }, timezone, value =>
                {
                    timezone = value;
                    Touch("Time zone");
                })),
                AppKit.Field("Interface density", AppKit.Segmented(new[]
                {
                    ("comfortable", "Comfortable"),
                    ("compact", "Compact")
                }, density, value =>
                {
                    density = value;
                    Touch("Density");
                }), "Compact fits more rows on a laptop screen.")));

            return panel;
        }

        Control NotificationsForm()
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(AppKit.Body("Choose what reaches your inbox. Mentions and assignments always appear in the app.", 460));
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(AppKit.Toggle("Weekly summary email", weekly, value =>
            {
                weekly = value;
                Touch("Weekly summary");
            }));
            panel.Children.Add(AppKit.Toggle("Mentions and replies", mentions, value =>
            {
                mentions = value;
                Touch("Mentions");
            }));
            panel.Children.Add(AppKit.Toggle("Product updates", product, value =>
            {
                product = value;
                Touch("Product updates");
            }));
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(AppKit.Field("Delivery address", AppKit.Input("maya@northwind.co", "maya@northwind.co"), "Notifications are sent from no-reply@northwind.co.", 300));
            return panel;
        }

        Control SecurityForm()
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(AppKit.Body("Passwords and sessions live in the security section, which has its own guided flows.", 460));
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(AppKit.Line("Password", "Last changed 14 days ago"));
            panel.Children.Add(AppKit.Line("Two factor", "Enabled with an authenticator app"));
            panel.Children.Add(AppKit.Line("Active sessions", "3 devices"));
            panel.Children.Add(AppKit.Btn("Open security settings", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => AppKit.Say(status, "The security block is its own catalog item."), icon: "shield", height: 32));
            return panel;
        }

        Control BillingForm()
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(AppKit.Body("You are on the Team plan, billed yearly. The next invoice is on 1 November.", 460));
            panel.Children.Add(AppKit.Divider());
            panel.Children.Add(AppKit.Line("Plan", "Team · 8 seats"));
            panel.Children.Add(AppKit.Line("Renews", "1 November 2026"));
            panel.Children.Add(AppKit.Line("Payment method", "Visa ending 4242"));
            panel.Children.Add(AppKit.Row(6,
                AppKit.Btn("Manage plan", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => AppKit.Say(status, "Billing management is simulated."), height: 32),
                AppKit.Btn("View invoices", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () => AppKit.Say(status, "Invoices are simulated."), height: 32)));
            return panel;
        }

        void BuildBody()
        {
            var entry = Sections.First(item => item.Id == section);
            Control form = section switch
            {
                "notifications" => NotificationsForm(),
                "security" => SecurityForm(),
                "billing" => BillingForm(),
                _ => ProfileForm()
            };

            bodyHost.Content = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            new TextBlock { Text = entry.Label, Classes = { "arc-subtitle" } },
                            AppKit.Body(entry.Description, 460)
                        }
                    },
                    form
                }
            };
        }

        void BuildSaveBar()
        {
            if (changed == 0)
            {
                saveHost.Content = new Border
                {
                    Margin = new Thickness(20, 0, 20, 14),
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = AppKit.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(12),
                    Child = AppKit.Row(7,
                        new ArcIcon { Icon = "check", Size = 14, Foreground = AppKit.Brush(ArcTokens.Success) },
                        AppKit.Caption("All changes saved"))
                };
                return;
            }

            var bar = AppKit.Floating(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    AppKit.Row(8,
                        AppKit.Dot(ArcTokens.Accent, 8),
                        new TextBlock { Text = $"{changed} unsaved change{(changed == 1 ? string.Empty : "s")}", Classes = { "arc-body-sm" } }),
                    AppKit.Place(AppKit.Caption("Saving updates only this preview."), 1),
                    AppKit.Place(AppKit.Row(6,
                        AppKit.Btn("Discard", ArcButtonVariant.Secondary, ArcButtonSize.Sm, Revert, height: 32),
                        AppKit.Btn("Save changes", ArcButtonVariant.Primary, ArcButtonSize.Sm, Save, height: 32)), 2)
                }
            }, 10, 14);
            bar.Margin = new Thickness(20, 0, 20, 14);
            bar.HorizontalAlignment = HorizontalAlignment.Stretch;
            saveHost.Content = bar;
        }

        void Build()
        {
            BuildNav();
            BuildBody();
            BuildSaveBar();
        }

        Build();

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("188,*") };
        var navColumn = new Border
        {
            Background = AppKit.Brush(ArcTokens.SurfaceMuted),
            BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(12, 14, 12, 12),
            Child = navHost
        };
        var main = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children =
            {
                new Border
                {
                    Padding = new Thickness(20, 12, 20, 8),
                    BorderBrush = AppKit.Brush(ArcTokens.BorderSubtle),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new TextBlock { Text = "Settings", Classes = { "arc-body-sm", "arc-medium" } },
                            AppKit.Place(AppKit.Pill("Personal", ArcBadgeTone.Neutral), 1)
                        }
                    }
                },
                AppKit.Place(new Border { Padding = new Thickness(20, 16, 20, 8), Child = bodyHost }, 0, 1),
                AppKit.Place(new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new Border { Padding = new Thickness(20, 0, 20, 0), Child = status },
                        saveHost
                    }
                }, 0, 2)
            }
        };

        Grid.SetColumn(navColumn, 0);
        Grid.SetColumn(main, 1);
        layout.Children.Add(navColumn);
        layout.Children.Add(main);

        AppKit.Shortcut(layout, Key.S, Save, modifiers => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta));

        return AppKit.Screen(layout, 560);
    }
}
