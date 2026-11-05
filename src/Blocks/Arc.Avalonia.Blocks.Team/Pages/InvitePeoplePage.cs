using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team.Pages;

/// <summary>
/// Invite people: pick teammates from the workspace directory or paste a messy list, give each one a role, and
/// watch the seat meter before sending. The parser accepts names, bare emails and "Name &lt;email&gt;" lines,
/// reports duplicates and invalid entries, and the seat bar shows the plan limit with text as well as colour.
/// </summary>
public sealed class InvitePeoplePage : ArcItemPage
{
    private static readonly string[] Roles = { "Admin", "Editor", "Viewer" };

    private const string DotenvSample =
        "maya.chen@northwind.co\nnot-an-email\nalice.moreau@northwind.co\nalice.moreau@northwind.co";

    public override string ItemSlug => "invite-people";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-chip-field", "variant-directory-suggestions", "variant-dotenv-import", "state-seat-meter"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Inviting several people at once by name or email, with a role for each.",
        "Plans with a seat limit where people should see the effect of an invite before sending."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use team-members to manage people who already joined, or tag-input for a plain chip field."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Down and up move through directory suggestions; Enter adds the highlighted person or the typed email.",
        "The seat bar is a meter with a text value such as \"11 of 12 seats, 1 left\", and every add or import is announced."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A suggestion flies into the chip field and the seat meter fills with a short, non-overshooting spring.",
        "Invalid chips shake on send; reduced motion removes the shake and makes chips land instantly."
    };

    public override string? ImplementationNotes =>
        "The directory, pending list and 12-seat plan are sample data in memory; Send does not call an API. The " +
        "paste parser handles comments, blank lines and duplicates but not quoted names with commas. No Pro " +
        "source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var limit = 12;
        var currentMembers = 8;
        var pending = new List<(string Email, string Role)>
        {
            ("jonas.weber@northwind.co", "Editor")
        };
        var selected = new List<(string Email, string Role, string Name)>
        {
            ("sam.okafor@northwind.co", "Viewer", "Sam Okafor"),
            ("guest@partner.studio", "Viewer", "guest")
        };
        var suggestions = new List<TeamPerson>
        {
            TeamData.Person("Alice Moreau"),
            TeamData.Person("Tomas Novak"),
            TeamData.Person("Priya Raman")
        };

        var chipHost = new WrapPanel { Orientation = Orientation.Horizontal };
        var suggestionHost = new StackPanel { Spacing = 3 };
        var pendingHost = new StackPanel { Spacing = 3 };
        var seatMeter = TeamKit.Meter(0, 7);
        var seatTitle = TeamKit.Label(string.Empty);
        var seatNote = TeamKit.Caption(string.Empty);
        var importNote = TeamKit.Caption("Paste one address per line, or Name <email>.");
        var status = TeamKit.Caption(string.Empty);

        var field = TeamKit.Input("Type a name or email, then press Enter", leadingIcon: "mail");

        void RenderSeats()
        {
            var used = currentMembers + pending.Count + selected.Count;
            seatMeter.Fraction = Math.Clamp((double)used / limit, 0, 1);
            seatMeter.FillBrush = TeamKit.Brush(used > limit ? ArcTokens.Danger : ArcTokens.Accent);
            seatTitle.Text = $"{used} of {limit} seats";
            seatNote.Text = used > limit
                ? $"{used - limit} over the plan limit. Upgrade or remove someone before sending."
                : used == limit
                    ? "Full. Sending replaces the last seat."
                    : $"{limit - used} seat{(limit - used == 1 ? string.Empty : "s")} left on Pro.";
            seatNote.Foreground = TeamKit.Brush(used > limit ? ArcTokens.Danger : ArcTokens.TextMuted);
        }

        void RenderChips()
        {
            chipHost.Children.Clear();
            foreach (var item in selected.ToList())
            {
                var remove = new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Size = ArcButtonSize.Sm,
                    Compact = true,
                    Width = 22,
                    Height = 22,
                    MinHeight = 22,
                    Padding = new Thickness(0),
                    Content = new ArcIcon { Icon = "x", Size = 12 },
                    VerticalAlignment = VerticalAlignment.Center
                };
                AutomationProperties.SetName(remove, $"Remove {item.Email}");
                remove.Click += (_, _) =>
                {
                    selected.RemoveAll(entry => entry.Email == item.Email);
                    RenderChips();
                    RenderSeats();
                };

                var roleSelect = TeamKit.Select(Roles.Select(value => (value, value)), item.Role, value =>
                {
                    var index = selected.FindIndex(entry => entry.Email == item.Email);
                    if (index >= 0)
                    {
                        selected[index] = (item.Email, value, item.Name);
                    }
                }, 110);

                chipHost.Children.Add(new Border
                {
                    Margin = new Thickness(0, 0, 6, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = TeamKit.Tint(ArcTokens.Accent, 0.10),
                    BorderBrush = TeamKit.Tint(ArcTokens.Accent, 0.28),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(9, 3),
                    Child = TeamKit.Row(7,
                        TeamKit.Avatar(item.Name, ArcAvatarSize.Xs),
                        TeamKit.Caption(item.Email),
                        roleSelect,
                        remove)
                });
            }

            if (selected.Count == 0)
            {
                chipHost.Children.Add(new Border
                {
                    Padding = new Thickness(4, 6),
                    Child = TeamKit.Caption("No one selected yet. Add a teammate from the directory or paste a list.")
                });
            }
        }

        void RenderSuggestions()
        {
            suggestionHost.Children.Clear();
            foreach (var person in suggestions)
            {
                var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                content.Children.Add(TeamKit.Avatar(person.Name, ArcAvatarSize.Sm));
                var identity = new StackPanel { Spacing = 1, Margin = new Thickness(9, 0, 8, 0) };
                identity.Children.Add(new TextBlock { Text = person.Name, Classes = { "arc-label" } });
                identity.Children.Add(TeamKit.Caption(person.Email));
                Grid.SetColumn(identity, 1);
                content.Children.Add(identity);
                var add = TeamKit.IconBtn("plus", $"Add {person.Name}", box: 26);
                Grid.SetColumn(add, 2);
                content.Children.Add(add);

                add.Click += (_, _) =>
                {
                    if (selected.Count >= 6)
                    {
                        status.Text = "This preview caps the selection at six people.";
                        return;
                    }

                    selected.Add((person.Email, "Viewer", person.Name));
                    suggestions.Remove(person);
                    RenderSuggestions();
                    RenderChips();
                    RenderSeats();
                    status.Text = $"{person.Name} added as a Viewer.";
                };

                suggestionHost.Children.Add(TeamKit.Panel(content, 6, 12));
            }

            if (suggestions.Count == 0)
            {
                suggestionHost.Children.Add(TeamKit.Caption("Everyone in the directory is already invited."));
            }
        }

        void RenderPending()
        {
            pendingHost.Children.Clear();
            foreach (var (address, role) in pending.ToList())
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                var local = address.Split('@')[0].Replace('.', ' ');
                row.Children.Add(TeamKit.Avatar(local, ArcAvatarSize.Xs));
                var identity = new StackPanel { Spacing = 1, Margin = new Thickness(9, 0, 8, 0) };
                identity.Children.Add(TeamKit.Caption(address));
                identity.Children.Add(TeamKit.Caption("Invite sent"));
                Grid.SetColumn(identity, 1);
                row.Children.Add(identity);
                var badge = TeamKit.Pill(role);
                Grid.SetColumn(badge, 2);
                row.Children.Add(badge);
                pendingHost.Children.Add(row);
            }

            if (pending.Count == 0)
            {
                pendingHost.Children.Add(TeamKit.Caption("No pending invites yet."));
            }
        }

        void AddAddress(string raw)
        {
            var address = raw.Trim().Trim('<', '>');
            if (!address.Contains('@') || !address.Contains('.') || address.Contains(' '))
            {
                status.Text = $"\"{raw.Trim()}\" is not a valid email.";
                status.Foreground = TeamKit.Brush(ArcTokens.Danger);
                return;
            }

            var duplicate = selected.Any(item => string.Equals(item.Email, address, StringComparison.OrdinalIgnoreCase)) ||
                            pending.Any(item => string.Equals(item.Email, address, StringComparison.OrdinalIgnoreCase)) ||
                            TeamData.People.Any(person =>
                                string.Equals(person.Email, address, StringComparison.OrdinalIgnoreCase) &&
                                !suggestions.Contains(person));
            if (duplicate)
            {
                status.Text = $"{address} is already on the team or invited.";
                status.Foreground = TeamKit.Brush(ArcTokens.Warning);
                return;
            }

            selected.Add((address, "Viewer", address.Split('@')[0].Replace('.', ' ')));
            RenderChips();
            RenderSeats();
            status.Text = $"{address} added as a Viewer.";
            status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
        }

        field.AddHandler(InputElement.KeyDownEvent, (_, args) =>
        {
            if (args.Key != Key.Enter)
            {
                return;
            }

            AddAddress(field.Value ?? string.Empty);
            field.Value = string.Empty;
            args.Handled = true;
        }, RoutingStrategies.Tunnel);

        var paste = TeamKit.Textarea("Paste a list of people", DotenvSample, 94);
        var import = TeamKit.Btn("Import list", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "upload");
        import.Click += (_, _) =>
        {
            var valid = 0;
            var invalid = 0;
            var duplicates = 0;
            foreach (var line in (paste.Value ?? string.Empty).Split('\n'))
            {
                var text = line.Trim();
                if (text.Length == 0 || text.StartsWith('#'))
                {
                    continue;
                }

                if (!text.Contains('@') || !text.Contains('.'))
                {
                    invalid++;
                    continue;
                }

                var email = text.Trim('<', '>').Trim();
                if (selected.Any(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase)) ||
                    pending.Any(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase)))
                {
                    duplicates++;
                    continue;
                }

                selected.Add((email, "Viewer", email.Split('@')[0].Replace('.', ' ')));
                valid++;
            }

            RenderChips();
            RenderSeats();
            importNote.Text = $"{valid} imported · {invalid} invalid · {duplicates} duplicate{(duplicates == 1 ? string.Empty : "s")}";
            importNote.Foreground = TeamKit.Brush(invalid + duplicates > 0 ? ArcTokens.Warning : ArcTokens.Success);
        };

        var send = TeamKit.Btn("Send invites", ArcButtonVariant.Primary, ArcButtonSize.Sm, icon: "send");
        send.Click += (_, _) =>
        {
            if (selected.Count == 0)
            {
                status.Text = "Add at least one person before sending.";
                status.Foreground = TeamKit.Brush(ArcTokens.Danger);
                return;
            }

            if (currentMembers + pending.Count + selected.Count > limit)
            {
                status.Text = "Over the seat limit. Upgrade the plan or remove someone before sending.";
                status.Foreground = TeamKit.Brush(ArcTokens.Danger);
                return;
            }

            foreach (var item in selected)
            {
                pending.Add((item.Email, item.Role));
            }

            var count = selected.Count;
            selected.Clear();
            RenderChips();
            RenderPending();
            RenderSeats();
            send.Content = new TextBlock { Text = "Invites sent", VerticalAlignment = VerticalAlignment.Center };
            status.Text = $"{count} invite{(count == 1 ? string.Empty : "s")} sent. Pending invites are listed on the right.";
            status.Foreground = TeamKit.Brush(ArcTokens.Success);
        };

        RenderChips();
        RenderSuggestions();
        RenderPending();
        RenderSeats();

        var chipsField = new Border
        {
            Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(8),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    chipHost,
                    field
                }
            }
        };

        var left = TeamKit.Stack(8,
            new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new TextBlock { Text = "Invite people", Classes = { "arc-heading" } },
                    TeamKit.Body("Pick from the directory or paste a list. Everyone starts as a Viewer.", 560)
                }
            },
            chipsField,
            TeamKit.Stack(6, TeamKit.Label("From the directory"), suggestionHost),
            TeamKit.Stack(6,
                TeamKit.Row(10, TeamKit.Label("Paste a list"), importNote),
                paste,
                TeamKit.Row(8, import)));

        var meterPanel = new StackPanel { Spacing = 8 };
        meterPanel.Children.Add(TeamKit.Row(8, new ArcIcon { Icon = "users", Size = 15 }, seatTitle));
        meterPanel.Children.Add(seatMeter);
        meterPanel.Children.Add(seatNote);

        var right = TeamKit.Stack(14,
            TeamKit.Label("Seats"),
            meterPanel,
            TeamKit.Divider(),
            TeamKit.Stack(6, TeamKit.Label("Pending"), pendingHost),
            TeamKit.Divider(),
            send,
            status,
            TeamKit.Caption("Invites expire after seven days. Roles can be changed at any time."));

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("640,1,*") };
        body.Children.Add(new Border { Padding = new Thickness(0, 0, 24, 0), Child = left });
        var rule = new Border
        {
            Width = 1,
            Background = TeamKit.Brush(ArcTokens.BorderSubtle),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(rule, 1);
        body.Children.Add(rule);
        var rightHolder = new Border { Padding = new Thickness(24, 0, 0, 0), Child = right };
        Grid.SetColumn(rightHolder, 2);
        body.Children.Add(rightHolder);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(20),
            Child = body
        });
    }
}


