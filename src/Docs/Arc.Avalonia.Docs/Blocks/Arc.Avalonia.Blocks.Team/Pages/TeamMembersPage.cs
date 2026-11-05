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
/// Team members: a workspace member list with pending invites, per-person roles and a seat count. Typed emails
/// become chips, Send invite moves them into the pending list, and the seat meter rolls with every change.
/// Members, invitations and the seat limit are simulated in memory.
/// </summary>
public sealed class TeamMembersPage : ArcItemPage
{
    private static readonly string[] Roles = { "Admin", "Editor", "Viewer" };

    public override string ItemSlug => "team-members";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-member-rows", "variant-pending-invites", "variant-invite-composer", "state-seat-limit"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "The team or workspace settings page of a SaaS app.",
        "Inviting several people at once, with a role for each and a visible seat limit."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use team-directory for a browsable people directory, or roles-permissions to define what roles can do."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Enter turns the typed email into a chip; Backspace on an empty field removes the last one.",
        "Role selects are keyboard operable, and invite results are announced in a polite status region."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Chips settle into the pending list and the seat count rolls; rows enter and leave with a short fade.",
        "Reduced motion makes the flight and reflow instant."
    };

    public override string? ImplementationNotes =>
        "Members, pending invites and the 12-seat limit are sample data in memory; nothing is sent. The email " +
        "field accepts Enter only, and role changes are local. No actual email validation beyond a simple shape " +
        "check happens, matching the reference's simulated send. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var members = new List<TeamPerson>
        {
            TeamData.Person("Maya Chen"),
            TeamData.Person("Jonas Weber"),
            TeamData.Person("Priya Raman"),
            TeamData.Person("Lena Fischer")
        };
        var roles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Maya Chen"] = "Admin",
            ["Jonas Weber"] = "Editor",
            ["Priya Raman"] = "Editor",
            ["Lena Fischer"] = "Viewer"
        };
        var pending = new List<(string Email, string Role)>
        {
            ("sam.okafor@northwind.co", "Viewer"),
            ("diego.alvarez@northwind.co", "Editor")
        };
        var staged = new List<(string Email, string Role)>();
        const int seatLimit = 12;

        var pendingHost = new StackPanel { Spacing = 4 };
        var membersHost = new StackPanel { Spacing = 4 };
        var stagedHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var seatLabel = TeamKit.Caption(string.Empty);
        var pendingCount = TeamKit.Caption(string.Empty);
        var memberCount = TeamKit.Caption(string.Empty);
        var status = TeamKit.Caption(string.Empty);
        var email = TeamKit.Input("name@company.com", leadingIcon: "mail", width: 280);
        var roleValue = "Viewer";

        int Seats() => members.Count + pending.Count + staged.Count;

        void RenderStatus()
        {
            seatLabel.Text = $"{Seats()} of {seatLimit} seats used";
            pendingCount.Text = pending.Count.ToString();
            memberCount.Text = members.Count.ToString();
            if (Seats() > seatLimit)
            {
                seatLabel.Text += " · over the plan limit";
                seatLabel.Foreground = TeamKit.Brush(ArcTokens.Danger);
            }
            else
            {
                seatLabel.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
            }
        }

        void RenderStaged()
        {
            stagedHost.Children.Clear();
            foreach (var (address, role) in staged.ToList())
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
                AutomationPropertiesName(remove, $"Remove {address}");
                remove.Click += (_, _) =>
                {
                    staged.RemoveAll(item => item.Email == address);
                    RenderStaged();
                    RenderStatus();
                    status.Text = $"{address} removed from this invite.";
                };

                var chip = new Border
                {
                    Background = TeamKit.Tint(ArcTokens.Accent, 0.10),
                    BorderBrush = TeamKit.Tint(ArcTokens.Accent, 0.28),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(10, 4, 5, 4),
                    Child = TeamKit.Row(6,
                        TeamKit.Caption(address),
                        TeamKit.Caption(role),
                        remove)
                };
                stagedHost.Children.Add(chip);
            }
        }

        void RenderPending()
        {
            pendingHost.Children.Clear();
            foreach (var (address, role) in pending.ToList())
            {
                var name = Display(address.Split('@')[0].Replace('.', ' '));
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto") };
                row.Children.Add(TeamKit.Avatar(name, ArcAvatarSize.Sm));
                var identity = new StackPanel { Spacing = 1, Margin = new Thickness(10, 0, 8, 0) };
                identity.Children.Add(new TextBlock { Text = name, Classes = { "arc-label" } });
                identity.Children.Add(TeamKit.Caption(address));
                Grid.SetColumn(identity, 1);
                row.Children.Add(identity);

                var roleSelect = TeamKit.Select(Roles.Select(value => (value, value)), role, value =>
                {
                    var index = pending.FindIndex(item => item.Email == address);
                    if (index >= 0)
                    {
                        pending[index] = (address, value);
                    }
                }, 108);
                Grid.SetColumn(roleSelect, 2);
                row.Children.Add(roleSelect);

                var sent = TeamKit.Btn("Resend", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
                sent.Click += (_, _) =>
                {
                    sent.Content = new TextBlock { Text = "Sent", VerticalAlignment = VerticalAlignment.Center };
                    status.Text = $"Invite resent to {address}.";
                };
                Grid.SetColumn(sent, 3);
                row.Children.Add(sent);

                var revoke = TeamKit.Btn("Revoke", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
                revoke.Click += (_, _) =>
                {
                    pending.RemoveAll(item => item.Email == address);
                    RenderPending();
                    RenderStatus();
                    status.Text = $"Invite to {address} revoked.";
                };
                Grid.SetColumn(revoke, 4);
                row.Children.Add(revoke);

                pendingHost.Children.Add(new Border
                {
                    Padding = new Thickness(10, 5),
                    CornerRadius = new CornerRadius(14),
                    Child = row
                });
            }

            if (pending.Count == 0)
            {
                pendingHost.Children.Add(TeamKit.Caption("No pending invites."));
            }
        }

        void RenderMembers()
        {
            membersHost.Children.Clear();
            foreach (var person in members)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
                row.Children.Add(TeamKit.Avatar(person.Name, ArcAvatarSize.Sm, ArcAvatarStatus.Online));
                var identity = new StackPanel { Spacing = 1, Margin = new Thickness(10, 0, 8, 0) };
                identity.Children.Add(new TextBlock { Text = person.Name, Classes = { "arc-label" } });
                identity.Children.Add(TeamKit.Caption(person.Email));
                Grid.SetColumn(identity, 1);
                row.Children.Add(identity);

                var roleSelect = TeamKit.Select(Roles.Select(value => (value, value)),
                    roles[person.Name], value => roles[person.Name] = value, 108);
                Grid.SetColumn(roleSelect, 2);
                row.Children.Add(roleSelect);

                var remove = TeamKit.IconBtn("trash-2", $"Remove {person.Name}", () =>
                {
                    if (members.Count <= 1)
                    {
                        status.Text = "A workspace needs at least one member.";
                        return;
                    }

                    members.Remove(person);
                    RenderMembers();
                    RenderStatus();
                    status.Text = $"{person.Name} removed from the workspace.";
                }, box: 28);
                Grid.SetColumn(remove, 3);
                row.Children.Add(remove);

                membersHost.Children.Add(new Border
                {
                    Padding = new Thickness(10, 5),
                    CornerRadius = new CornerRadius(14),
                    Child = row
                });
            }
        }

        void AddStaged(string address)
        {
            var trimmed = address.Trim();
            if (trimmed.Length == 0)
            {
                return;
            }

            if (!trimmed.Contains('@') || !trimmed.Contains('.'))
            {
                status.Text = $"\"{trimmed}\" is not a valid email address.";
                status.Foreground = TeamKit.Brush(ArcTokens.Danger);
                return;
            }

            if (staged.Any(item => string.Equals(item.Email, trimmed, StringComparison.OrdinalIgnoreCase)) ||
                pending.Any(item => string.Equals(item.Email, trimmed, StringComparison.OrdinalIgnoreCase)) ||
                members.Any(person => string.Equals(person.Email, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                status.Text = $"{trimmed} is already a member or invited.";
                status.Foreground = TeamKit.Brush(ArcTokens.Warning);
                return;
            }

            staged.Add((trimmed, roleValue));
            RenderStaged();
            RenderStatus();
            status.Text = $"{trimmed} ready to invite as {roleValue}.";
            status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
        }

        var add = TeamKit.Btn("Add", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
        {
            AddStaged(email.Value ?? string.Empty);
            email.Value = string.Empty;
        });

        email.AddHandler(InputElement.KeyDownEvent, (_, args) =>
        {
            if (args.Key != Key.Enter)
            {
                return;
            }

            AddStaged(email.Value ?? string.Empty);
            email.Value = string.Empty;
            args.Handled = true;
        }, RoutingStrategies.Tunnel);

        var rolePicker = TeamKit.Select(Roles.Select(value => (value, value)), roleValue, value => roleValue = value, 118);

        var send = TeamKit.Btn("Send invites", ArcButtonVariant.Primary, ArcButtonSize.Sm, icon: "send");
        send.Click += (_, _) =>
        {
            foreach (var item in staged.ToList())
            {
                pending.Add(item);
            }

            staged.Clear();
            RenderStaged();
            RenderPending();
            RenderStatus();
            status.Text = "Invitations sent. Also fine: one click here would call your invite endpoint.";
            status.Foreground = TeamKit.Brush(ArcTokens.Success);
        };

        RenderPending();
        RenderMembers();
        RenderStatus();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "Members", Classes = { "arc-heading" } },
                TeamKit.Caption("Northwind Studio · SAML enforced · 4 roles")
            }
        });
        var seat = new Border
        {
            Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(12, 6),
            Child = TeamKit.Row(8,
                new ArcIcon { Icon = "users", Size = 14 },
                seatLabel)
        };
        Grid.SetColumn(seat, 1);
        header.Children.Add(seat);

        var inviteRow = TeamKit.Stack(8,
            TeamKit.Row(8, email, rolePicker, add, send),
            stagedHost,
            status);

        var pendingSection = TeamKit.Stack(8,
            TeamKit.Row(8, TeamKit.Label("Pending invites"), pendingCount),
            pendingHost);

        var membersSection = TeamKit.Stack(8,
            TeamKit.Row(8, TeamKit.Label("Current members"), memberCount),
            membersHost);

        var content = TeamKit.Stack(12,
            header,
            TeamKit.Divider(),
            inviteRow,
            pendingSection,
            membersSection);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(24),
            Child = content
        });
    }

    private static void AutomationPropertiesName(Control control, string name) =>
        AutomationProperties.SetName(control, name);

    private static string Display(string localPart)
    {
        var words = localPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', Array.ConvertAll(words,
            word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
