using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Share access: email chips with an error state, people rows with a role menu, the undo bar after a
/// removal, link access and the in-place copy confirmation, all in one page-hosted panel.
/// </summary>
public sealed class ShareAccessPage : ArcPage
{
    public override string Title => "Share access";

    public override string Group => "Overlays";

    public override string? Description =>
        "A share panel for a document, board or project: invite with validated email chips, change roles " +
        "from a compact menu, remove someone with an undo, switch link access and copy the link with an " +
        "in-place confirmation. The panel draws its own floating surface inside the page.";

    public override int Order => 207;

    public override Control Build(ArcDemoContext context)
    {
        var panel = new ArcShareAccess
        {
            Title = "Q3 launch plan",
            Label = "Share",
            Width = 424,
            ShowDone = true,
            SelfEmail = "maya@acme.com",
            LinkAccess = ArcShareLinkAccess.Anyone,
            LinkRole = "viewer",
            LinkUrl = "https://acme.com/d/q3-launch-plan",
            InviteRole = "editor",
            People = new List<ArcSharePerson>
            {
                new() { DisplayName = "Maya Chen", Email = "maya@acme.com", Role = "owner" },
                new() { DisplayName = "Nathan Cole", Email = "nathan@acme.com", Role = "editor" },
                new() { DisplayName = "Sam Rivera", Email = "sam@acme.com", Role = "commenter", IsPending = true },
                new() { DisplayName = "Owen Park", Email = "owen@acme.com", Role = "viewer" },
                new() { DisplayName = "Lena Ortiz", Email = "lena@acme.com", Role = "viewer" },
                new() { DisplayName = "Priya Shah", Email = "priya@acme.com", Role = "viewer" }
            },
            Suggestions = new List<ArcShareSuggestion>
            {
                new("Dana Whitfield", "dana@acme.com", "DW"),
                new("Owen Park", "owen@acme.com", "OP"),
                new("Lena Ortiz", "lena@acme.com", "LO"),
                new("Marcus Bell", "marcus@acme.com", "MB")
            }
        };

        // Present the states a person produces while sharing: a valid chip beside an invalid one, a removal
        // with its undo bar, an open role menu and the copy confirmation.
        panel.AttachedToVisualTree += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                panel.AddChip("dana@acme.com");
                panel.AddChip("mayaatacme");
                panel.RemovePerson("priya@acme.com");
                panel.OpenRoleMenu("nathan@acme.com");
                _ = panel.CopyLinkAsync();
            }, DispatcherPriority.Loaded);
        };

        var notes = new StackPanel
        {
            Spacing = 14,
            Width = 300,
            Margin = new Thickness(36, 4, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        notes.Children.Add(new TextBlock
        {
            Text = "What the panel shows",
            Classes = { "arc-label" }
        });
        notes.Children.Add(Note(
            "Email chips",
            "An address that does not parse keeps its danger tint and a message explains the form. Enter or a " +
            "comma turns text into a chip; Backspace in an empty field marks the last chip, then removes it."));
        notes.Children.Add(Note(
            "Roles land in place",
            "Each row's role opens a compact menu and the label morphs to its new width, so the chevron glides " +
            "instead of jumping. Owner rows cannot be changed or removed."));
        notes.Children.Add(Note(
            "Removal with undo",
            "The row folds away and the undo bar slides in beneath it for five seconds. Undo puts the person " +
            "back where they were."));
        notes.Children.Add(Note(
            "Link access",
            "The lock turns into a globe, the link role menu slides in under the access line, and Copy link " +
            "confirms in place without changing width."));

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0),
            Children = { panel, notes }
        };

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Share access",
                    "Use it as the content of a share popover or dialog for anything with per-person access. " +
                    "Every change lands in place: chips, roles, removals with undo, link access and the copy " +
                    "confirmation never move the rest of the panel.",
                    new Border
                    {
                        Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
                        CornerRadius = new CornerRadius(26),
                        Padding = new Thickness(28, 24),
                        Child = row,
                        ClipToBounds = false,
                        HorizontalAlignment = HorizontalAlignment.Left
                    })
            }
        };
    }

    private static Control Note(string title, string copy)
    {
        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(new TextBlock { Text = title, Classes = { "arc-body-sm", "arc-medium" } });
        stack.Children.Add(new TextBlock
        {
            Text = copy,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300
        });
        return stack;
    }
}


