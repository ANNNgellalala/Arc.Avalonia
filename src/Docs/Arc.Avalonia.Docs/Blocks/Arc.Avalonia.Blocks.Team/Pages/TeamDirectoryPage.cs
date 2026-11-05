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
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team.Pages;

/// <summary>
/// Team directory: a searchable people list with team filters, availability and a focused profile pane.
/// The sample workspace lives in <see cref="TeamData"/>; portraits are gradient stand-ins because the port
/// ships no licensed photography. Selection is a real in-memory filter, not a static mock.
/// </summary>
public sealed class TeamDirectoryPage : ArcItemPage
{
    public override string ItemSlug => "team-directory";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-search-filters", "variant-people-list", "variant-profile-pane"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "An internal directory where people search colleagues and copy contact details.",
        "Team pages with department filters and a profile pane that stays in context."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use team-members to manage roles and invitations, or team-showcase for a public team section."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Filter chips are a labelled group with a pressed state; people are buttons reached with Tab and chosen with Enter or Space.",
        "Escape in the search field clears the query, and the profile pane updates as polite live content."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The selected row and filtered rows settle in place; the profile pane fades up 4 px when the person changes.",
        "Reduced motion removes the offset and the fade, so selection changes land at once."
    };

    public override string? ImplementationNotes =>
        "Sample people and contact details live in TeamData.cs. Portraits are the brand gradient with initials " +
        "because the port ships no licensed photography. Search and filters run in memory, not against a " +
        "paginated server. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var query = string.Empty;
        var team = "All";
        var selected = TeamData.People[0];

        var listHost = new StackPanel { Spacing = 2 };
        var profileHost = new Border();

        void RenderList()
        {
            listHost.Children.Clear();
            var matches = TeamData.People.Where(person =>
                (team == "All" || person.Team == team) &&
                (query.Length == 0 ||
                 person.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 person.Role.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 person.Team.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();

            foreach (var person in matches)
            {
                listHost.Children.Add(BuildRow(person, person == selected, () =>
                {
                    selected = person;
                    RenderList();
                    RenderProfile();
                }));
            }

            if (matches.Count == 0)
            {
                listHost.Children.Add(TeamKit.Panel(TeamKit.Stack(6,
                    TeamKit.Label("No one matches that search"),
                    TeamKit.Body("Try a name, a role, or one of the team filters.")), 18, 18, true));
            }
        }

        void RenderProfile()
        {
            var identity = TeamKit.Row(10,
                TeamKit.Avatar(selected.Name, ArcAvatarSize.Lg,
                    selected.Name is "Maya Chen" or "Jonas Weber" or "Sam Okafor"
                        ? ArcAvatarStatus.Online
                        : ArcAvatarStatus.None),
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = selected.Name, Classes = { "arc-heading" } },
                        TeamKit.Body(selected.Role)
                    }
                });

            var facts = new StackPanel { Spacing = 9 };
            facts.Children.Add(identity);
            facts.Children.Add(TeamKit.TeamPill(selected.Team));
            facts.Children.Add(TeamKit.Divider());
            facts.Children.Add(TeamKit.Meta("target", selected.Focus));
            facts.Children.Add(TeamKit.Meta("map-pin", $"{selected.Location} · {selected.Timezone}"));
            facts.Children.Add(TeamKit.Meta("clock", "Usually replies within two hours"));

            var copy = new ArcCopyButton
            {
                Value = selected.Email,
                Label = "Copy email",
                CopiedLabel = "Copied",
                Variant = ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm
            };

            var emailRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            emailRow.Children.Add(new TextBlock
            {
                Text = selected.Email,
                Classes = { "arc-body-sm" },
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(copy, 1);
            emailRow.Children.Add(copy);

            profileHost.Child = TeamKit.Stack(10,
                TeamKit.Portrait(selected, 326, 112, 18, 38),
                facts,
                TeamKit.Divider(),
                emailRow,
                TeamKit.Row(8,
                    TeamKit.Btn("Message", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "message-circle"),
                    TeamKit.Btn("View profile", ArcButtonVariant.Ghost, ArcButtonSize.Sm, icon: "arrow-up-right")));
        }

        var search = TeamKit.Search("Search people, roles, teams", width: 268);
        search.ValueChanged += (_, _) =>
        {
            query = search.Value ?? string.Empty;
            RenderList();
        };

        var chips = TeamKit.Chips(new[]
        {
            ("All", $"All {TeamData.People.Count}"),
            ("Design", $"Design {TeamData.CountIn("Design")}"),
            ("Engineering", $"Engineering {TeamData.CountIn("Engineering")}"),
            ("Operations", $"Operations {TeamData.CountIn("Operations")}")
        }, "All", value =>
        {
            team = value;
            RenderList();
        }, "Team");
        chips.MaxVisible = 4;

        var toolbar = TeamKit.Stack(12,
            TeamKit.Row(10,
                search,
                TeamKit.Btn("Invite people", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "plus")),
            chips);

        var left = TeamKit.Stack(10, toolbar, listHost);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("604,1,357") };
        body.Children.Add(new Border { Padding = new Thickness(14), Child = left });

        var rule = new Border
        {
            Background = TeamKit.Brush(ArcTokens.BorderSubtle),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Width = 1
        };
        Grid.SetColumn(rule, 1);
        body.Children.Add(rule);

        var profile = new Border { Padding = new Thickness(14), Child = profileHost };
        Grid.SetColumn(profile, 2);
        body.Children.Add(profile);

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(20, 9, 20, 9)
        };
        header.Children.Add(TeamKit.Row(10,
            TeamKit.Disc("users", 32, 15, true),
            new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = "Northwind Studio", Classes = { "arc-label" } },
                    TeamKit.Caption("People directory · 8 teammates")
                }
            }));
        var presence = TeamKit.Row(8,
            TeamKit.AvatarGroup(TeamData.People.Select(person => person.Name), ArcAvatarSize.Xs, 4, "Online now"),
            TeamKit.Caption("5 available"));
        Grid.SetColumn(presence, 2);
        header.Children.Add(presence);

        RenderList();
        RenderProfile();

        var surface = new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Child = TeamKit.Stack(0,
                header,
                TeamKit.Divider(),
                body)
        };

        return TeamKit.Root(surface);
    }

    private static Control BuildRow(TeamPerson person, bool selected, Action onSelect)
    {
        var online = person.Name is "Maya Chen" or "Jonas Weber" or "Sam Okafor" or "Lena Fischer" or "Alice Moreau";
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        content.Children.Add(TeamKit.Avatar(person.Name, ArcAvatarSize.Sm,
            online ? ArcAvatarStatus.Online : ArcAvatarStatus.Offline));
        var identity = new StackPanel { Spacing = 2, Margin = new Thickness(10, 0, 8, 0) };
        identity.Children.Add(new TextBlock { Text = person.Name, Classes = { "arc-label" } });
        identity.Children.Add(TeamKit.Body(person.Role, 290));
        Grid.SetColumn(identity, 1);
        content.Children.Add(identity);

        var meta = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                TeamKit.TeamPill(person.Team),
                TeamKit.Caption(online ? "Available now" : "Offline")
            }
        };
        Grid.SetColumn(meta, 2);
        content.Children.Add(meta);

        return TeamKit.Selectable(content, onSelect, selected, $"{person.Name}, {person.Role}", 14,
            new Thickness(8, 3));
    }
}



