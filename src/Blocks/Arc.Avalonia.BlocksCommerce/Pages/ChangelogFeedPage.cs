using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.BlocksCommerce.CommerceKit;

namespace Arc.Avalonia.BlocksCommerce.Pages;

/// <summary>
/// Changelog feed: release notes grouped by month with type filters that recount, entries that open in place,
/// and a subscribe control that turns into a check. Entries and the webhook snippet are sample data.
/// </summary>
public sealed class ChangelogFeedPage : ArcItemPage
{
    public override string ItemSlug => "changelog-feed";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A product changelog or release notes page grouped by month.",
        "Release notes with type filters such as features, fixes and improvements."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A short vertical list of events without filters; use timeline."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each entry is a button with expanded state, and Escape cancels the subscribe form and returns focus to the button.",
        "Filter chips are toggle buttons with pressed state, and the visible count is announced politely."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Entries open in place while the chevron rotates; the subscribe control morphs from button to form to a check.",
        "Reduced motion makes those changes instant."
    };

    public override string? ImplementationNotes =>
        "Entries, dates and the webhook snippet are sample data. The feed is not virtualised and shows one month; the reference scrolls a fixed height area and tracks the month bar. Subscribing only sets local state.";

    private sealed record Entry(string Id, string Date, string Type, string Title, string Body, string? Code);

    private static readonly Entry[] Entries =
    {
        new("filters", "24 Oct", "Feature", "Saved filters on every table",
            "Every table now remembers its filters per person. Sort, search and column choices survive a reload, and a saved view can be shared with the workspace.",
            null),
        new("hooks", "18 Oct", "Feature", "Signed webhooks with a retry console",
            "Webhook deliveries are signed with a per endpoint secret, and failed deliveries can be retried from the console with the original payload.",
            "POST /api/webhooks/releases\nX-Arc-Signature: t=1729,v1=8f2c...\n{\n  \"event\": \"release.published\",\n  \"id\": \"rel_2481\"\n}"),
        new("dates", "11 Oct", "Fix", "Timezone drift in weekly reports",
            "Reports generated near midnight could land in the previous day. The aggregation now uses the workspace timezone for both the window and the label.",
            null),
        new("density", "3 Oct", "Improvement", "A calmer dense table",
            "Row height in dense mode went from 32 to 36 px, headers stick while scrolling, and the hover row no longer moves the selection ring.",
            null)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var filter = "All";
        var expanded = "hooks";
        var subscribed = false;
        var subscribeOpen = false;
        var status = Ck.Status();

        var listHost = new ContentControl();
        var subscribeHost = new ContentControl();

        void BuildSubscribe()
        {
            if (subscribed)
            {
                subscribeHost.Content = Ck.Row(6,
                    new ArcIcon { Icon = "check", Size = 14, Foreground = Ck.Brush(ArcTokens.Success) },
                    Ck.Caption("Subscribed, check your inbox"));
                return;
            }

            if (!subscribeOpen)
            {
                subscribeHost.Content = Ck.Btn("Subscribe", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
                {
                    subscribeOpen = true;
                    BuildSubscribe();
                    Ck.Say(status, "Subscribe form opened.");
                }, icon: "mail", height: 34);
                return;
            }

            var email = Ck.Input("you@company.com", null, null, null);
            email.Width = 220;
            var form = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            form.Children.Add(email);
            form.Children.Add(Ck.Btn("Join", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
            {
                subscribed = true;
                subscribeOpen = false;
                BuildSubscribe();
                Ck.Say(status, "Subscribed to release notes. Sample list only.");
            }, height: 34));
            form.Children.Add(Ck.Link("Cancel", () =>
            {
                subscribeOpen = false;
                BuildSubscribe();
                Ck.Say(status, "Subscribe cancelled.");
            }));
            subscribeHost.Content = form;
        }

        void BuildList()
        {
            var matches = filter == "All"
                ? Entries
                : Entries.Where(entry => entry.Type == filter).ToArray();

            var rows = new StackPanel { Spacing = 4 };
            foreach (var entry in matches)
            {
                var target = entry;
                var isOpen = expanded == entry.Id;
                var chevron = new ArcIcon
                {
                    Icon = isOpen ? "chevron-up" : "chevron-down",
                    Size = 15,
                    Foreground = Ck.Brush(ArcTokens.TextMuted),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var row = new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Padding = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    CornerRadius = new CornerRadius(10),
                    Content = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
                        ColumnSpacing = 10,
                        Children =
                        {
                            Ck.Caption(entry.Date),
                            Place(Ck.Pill(entry.Type, entry.Type == "Fix" ? ArcBadgeTone.Warning : ArcBadgeTone.Accent), 1),
                            Place(Ck.Text(entry.Title, "arc-body-sm arc-medium"), 2),
                            Place(chevron, 3)
                        }
                    }
                };
                if (isOpen)
                {
                    row.Background = Ck.Brush(ArcTokens.SurfaceMuted);
                }

                row.Click += (_, _) =>
                {
                    expanded = isOpen ? string.Empty : target.Id;
                    BuildList();
                    Ck.Say(status, isOpen ? $"{target.Title} closed." : $"{target.Title} opened.");
                };
                rows.Children.Add(row);

                if (!isOpen)
                {
                    continue;
                }

                var detail = new StackPanel { Spacing = 8 };
                detail.Children.Add(Ck.Body(entry.Body, 640));
                if (entry.Code is { } code)
                {
                    detail.Children.Add(new ArcCodeBlock { Code = code, Language = "http", MaxLines = 4 });
                    detail.Children.Add(Ck.Caption("Signed with the endpoint secret"));
                }

                rows.Children.Add(new Border
                {
                    Padding = new Thickness(12, 0, 12, 8),
                    Child = detail
                });
            }
            listHost.Content = rows;
        }

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        void BuildFilters()
        {
            // Chips carry their counts from the full set; rebuilding keeps the selected fill correct.
            chips.Children.Clear();
            foreach (var option in new[] { "All", "Feature", "Fix", "Improvement" })
            {
                var value = option;
                var count = option == "All" ? Entries.Length : Entries.Count(entry => entry.Type == option);
                chips.Children.Add(Ck.Chip(option, filter == option, () =>
                {
                    filter = value;
                    BuildList();
                    BuildFilters();
                    Ck.Say(status, $"{value} selected, {count} entries.");
                }, count.ToString()));
            }
        }

        BuildSubscribe();
        BuildFilters();
        BuildList();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                Ck.Heading("Changelog", 380, "arc-subtitle"),
                Ck.Caption("Release notes, two or three times a month")
            }
        });
        subscribeHost.VerticalAlignment = VerticalAlignment.Top;
        header.Children.Add(Place(subscribeHost, 1));

        var monthBar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        monthBar.Children.Add(Ck.Text("October 2026", "arc-body-sm arc-medium"));
        var monthLinks = Ck.Row(10,
            Ck.Link("September", () => Ck.Say(status, "The reference scrolls back a month; this specimen keeps October.")),
            Ck.Link("November", () => Ck.Say(status, "November has no published entries yet.")));
        monthLinks.HorizontalAlignment = HorizontalAlignment.Right;
        monthBar.Children.Add(Place(monthLinks, 2));

        return Ck.Specimen(new StackPanel
        {
            Spacing = 10,
            Children = { header, Ck.Divider(), chips, monthBar, listHost, status }
        }, 12);
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
