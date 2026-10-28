using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksTeam.Pages;

/// <summary>
/// Studio perspectives: portrait-led editorial stories with a tab list of people and a focused reading state.
/// Arrow keys move through the tabs, the previous and next buttons wrap, and the quote panel is announced as a
/// polite live region. Quotes are sample editorial copy paired with the shared sample team.
/// </summary>
public sealed class TestimonialStagePage : ArcItemPage
{
    private sealed record Perspective(TeamPerson Person, string Company, string Quote, string Note);

    private static readonly IReadOnlyList<Perspective> Perspectives = new[]
    {
        new Perspective(TeamData.Person("Maya Chen"), "Northwind Studio",
            "We removed three status meetings in the first month. The work is visible now, so we talk about the work instead of asking where it is.",
            "Maya leads the design system and the editor surface."),
        new Perspective(TeamData.Person("Jonas Weber"), "Northwind Studio",
            "The board finally matches how a release actually feels: drafts pile up, review is the bottleneck, then everything lands in a quiet week.",
            "Jonas owns sync, offline drafts and the performance budget."),
        new Perspective(TeamData.Person("Priya Raman"), "Northwind Studio",
            "Permission reviews used to be a spreadsheet ritual. Now the matrix shows exactly what changed and who is affected before we save.",
            "Priya runs the API platform and the release pipeline."),
        new Perspective(TeamData.Person("Alice Moreau"), "Northwind Studio",
            "We interviewed teams for six months. The pattern was always the same: people do not want more tools, they want one place that tells the truth.",
            "Alice leads research and retention studies.")
    };

    public override string ItemSlug => "testimonial-stage";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Testimonials or editorial stories where the person's portrait matters.",
        "Landing sections that rotate through a few quotes on demand, with no autoplay."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use logo-marquee when logos are enough social proof, or carousel for many short testimonials."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "People form a tab list: left and right arrows move and focus the next person, wrapping at the ends.",
        "The quote panel is a polite live region, and each portrait has a descriptive label."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Portrait and quote crossfade with a small directional shift when the active person changes.",
        "Reduced motion removes the shift and switches instantly rather than sliding."
    };

    public override string? ImplementationNotes =>
        "Quotes and companies are sample editorial copy on the shared sample team; no testimonial is attributed to " +
        "a real customer. Portraits are the brand gradient with initials because the port ships no licensed " +
        "photography. There is no autoplay timer, matching the documented behaviour. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var index = 0;
        var portraitHost = new Border();
        var quoteHost = new Border();
        var tabHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var counter = TeamKit.Caption("01 / 04");
        var restoreTabFocus = false;

        void Render()
        {
            var perspective = Perspectives[index];
            var person = perspective.Person;

            var identity = new StackPanel { Spacing = 2 };
            identity.Children.Add(new TextBlock { Text = person.Name, Classes = { "arc-heading" } });
            identity.Children.Add(TeamKit.Body($"{person.Role} · {perspective.Company}"));

            var left = new StackPanel { Spacing = 14 };
            left.Children.Add(TeamKit.Portrait(person, 358, 316, 20, 64));
            left.Children.Add(identity);
            left.Children.Add(TeamKit.Caption(perspective.Note, 358));
            portraitHost.Child = left;

            var quote = new StackPanel { Spacing = 14 };
            var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            top.Children.Add(new ArcIcon
            {
                Icon = "quote",
                Size = 22,
                Foreground = TeamKit.Brush(ArcTokens.Accent)
            });
            Grid.SetColumn(counter, 1);
            top.Children.Add(counter);
            quote.Children.Add(top);
            quote.Children.Add(new TextBlock
            {
                Text = perspective.Quote,
                FontSize = 22,
                LineHeight = 32,
                LetterSpacing = -0.3,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 520
            });
            quote.Children.Add(TeamKit.Divider());
            quote.Children.Add(TeamKit.Row(10,
                TeamKit.Avatar(person.Name, ArcAvatarSize.Sm, ArcAvatarStatus.Online),
                new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock { Text = person.Name, Classes = { "arc-label" } },
                        TeamKit.Caption($"{person.Role}, {perspective.Company}")
                    }
                }));
            quoteHost.Child = quote;

            tabHost.Children.Clear();
            for (var tabIndex = 0; tabIndex < Perspectives.Count; tabIndex++)
            {
                var target = tabIndex;
                var personTab = Perspectives[tabIndex].Person;
                var tab = TeamKit.Selectable(TeamKit.Row(7,
                    TeamKit.Avatar(personTab.Name, ArcAvatarSize.Xs),
                    new TextBlock { Text = personTab.FirstName, FontSize = 12 }), () =>
                {
                    index = target;
                    Render();
                }, tabIndex == index, $"{personTab.Name} story", 999, new Thickness(8, 5));

                tab.KeyDown += (_, args) =>
                {
                    if (args.Key == Key.Right)
                    {
                        index = (index + 1) % Perspectives.Count;
                        restoreTabFocus = true;
                        Render();
                        args.Handled = true;
                    }
                    else if (args.Key == Key.Left)
                    {
                        index = (index - 1 + Perspectives.Count) % Perspectives.Count;
                        restoreTabFocus = true;
                        Render();
                        args.Handled = true;
                    }
                };
                tabHost.Children.Add(tab);
            }

            counter.Text = $"{index + 1:00} / {Perspectives.Count:00}";

            if (restoreTabFocus &&
                index < tabHost.Children.Count &&
                tabHost.Children[index] is Panel tabPanel &&
                tabPanel.Children.Count > 1 &&
                tabPanel.Children[1] is Control tabBody)
            {
                restoreTabFocus = false;
                tabBody.Focus();
            }
        }

        var prev = TeamKit.IconBtn("chevron-left", "Previous story", () =>
        {
            index = (index - 1 + Perspectives.Count) % Perspectives.Count;
            Render();
        }, box: 32);
        var next = TeamKit.IconBtn("chevron-right", "Next story", () =>
        {
            index = (index + 1) % Perspectives.Count;
            Render();
        }, box: 32);

        Render();

        var footer = new StackPanel { Spacing = 12 };
        footer.Children.Add(TeamKit.Divider());
        var footerNav = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        footerNav.Children.Add(tabHost);
        var arrows = TeamKit.Row(4, prev, next);
        Grid.SetColumn(arrows, 1);
        footerNav.Children.Add(arrows);
        footer.Children.Add(footerNav);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("382,1,*") };
        body.Children.Add(new Border { Padding = new Thickness(0, 0, 26, 0), Child = portraitHost });

        var rule = new Border
        {
            Width = 1,
            Background = TeamKit.Brush(ArcTokens.BorderSubtle),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(rule, 1);
        body.Children.Add(rule);

        var right = new StackPanel { Spacing = 16 };
        right.Children.Add(quoteHost);
        right.Children.Add(footer);
        var rightBorder = new Border { Padding = new Thickness(28, 0, 0, 0), Child = right };
        Grid.SetColumn(rightBorder, 2);
        body.Children.Add(rightBorder);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 18) };
        header.Children.Add(TeamKit.Caption("Studio perspectives · Northwind Studio"));
        var live = TeamKit.Pill("Reading", ArcBadgeTone.Accent, dot: true);
        Grid.SetColumn(live, 1);
        header.Children.Add(live);

        var content = new StackPanel { Spacing = 0 };
        content.Children.Add(header);
        content.Children.Add(body);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(26),
            Child = content
        });
    }
}

