using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>The async action button, the floating toolbar and the morphing icon family.</summary>
public sealed class AdvancedActionsPage : ArcPage
{
    public override string Title => "Advanced actions";

    public override string Group => "Advanced actions";

    public override string? Description =>
        "Actions that report their own progress: an async button that confirms in place, a floating toolbar with one shared highlight, and icons that morph between two states.";

    public override int Order => 700;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            DemoUi.Section(
                "Action button",
                "A single async commit such as Publish or Save where the result belongs on the button: pending keeps focus, success shows a check, then the button returns to idle on its own.",
                ActionButtonDemo()),
            DemoUi.Section(
                "Floating button group",
                "A floating editor or canvas toolbar of separate soft buttons. One highlight morphs from button to button as the pointer or keyboard moves, the tray never resizes, and a mode action keeps a quiet tint.",
                FloatingGroupDemo()),
            DemoUi.Section(
                "Icon morph",
                "A two-state icon inside the control whose meaning flips, such as menu and close or play and pause. Pick a pair, then drive it with State; the button around it carries the name.",
                IconMorphDemo())
        }
    };

    private static Control ActionButtonDemo()
    {
        var idle = new ArcActionButton
        {
            Label = "Publish",
            PendingLabel = "Publishing",
            SuccessLabel = "Published",
            OnAction = async () => await Task.Delay(900)
        };

        var pending = new ArcActionButton
        {
            Label = "Publish",
            PendingLabel = "Publishing",
            State = ArcActionButtonState.Pending
        };

        var success = new ArcActionButton
        {
            Label = "Publish",
            SuccessLabel = "Published",
            State = ArcActionButtonState.Success,
            ResetAfterMs = 0
        };

        var secondary = new ArcActionButton
        {
            Label = "Save draft",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };

        return new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                DemoUi.Row(idle, pending, success, secondary),
                Note("Pending shows a spinner and keeps keyboard focus; success draws a check and returns to idle after ResetAfterMs. The secondary button reuses ArcButton's quieter look.")
            }
        };
    }

    private static Control FloatingGroupDemo()
    {
        var status = DemoUi.Caption("Last action: none");

        var board = new ArcFloatingButtonGroup
        {
            Label = "Board actions",
            VerticalAlignment = VerticalAlignment.Top,
            Items = new[]
            {
                new ArcFloatingButtonItem
                {
                    Id = "undo",
                    Label = "Undo",
                    Icon = "undo-2",
                    IconOnly = true,
                    Shortcut = "⌘Z",
                    IsDisabled = true
                },
                new ArcFloatingButtonItem
                {
                    Id = "redo",
                    Label = "Redo",
                    Icon = "redo-2",
                    IconOnly = true,
                    Shortcut = "⇧⌘Z"
                },
                new ArcFloatingButtonItem { IsSeparator = true },
                new ArcFloatingButtonItem
                {
                    Id = "comment",
                    Label = "Comment",
                    Icon = "message-circle",
                    IconOnly = true,
                    Shortcut = "C"
                },
                new ArcFloatingButtonItem
                {
                    Id = "share",
                    Label = "Share",
                    Icon = "link"
                },
                new ArcFloatingButtonItem { IsSeparator = true },
                new ArcFloatingButtonItem
                {
                    Id = "present",
                    Label = "Present",
                    Icon = "play",
                    IconOnly = true,
                    IsPressed = true
                }
            }
        };
        board.ActionInvoked += (_, id) => status.Text = $"Last action: {id ?? "none"}";
        board.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            var share = board.GetVisualDescendants()
                .OfType<ArcButton>()
                .FirstOrDefault(button => AutomationProperties.GetName(button) == "Share");
            share?.Focus();
        }, DispatcherPriority.Loaded);

        var selection = new ArcFloatingButtonGroup
        {
            Label = "Selection actions",
            Variant = ArcFloatingButtonVariant.Floating,
            IconOnly = true,
            VerticalAlignment = VerticalAlignment.Top,
            Items = new[]
            {
                new ArcFloatingButtonItem { Id = "bold", Label = "Bold", Icon = "pencil", Tooltip = "Bold", Shortcut = "⌘B" },
                new ArcFloatingButtonItem { Id = "copy", Label = "Copy", Icon = "copy", Tooltip = "Copy", Shortcut = "⌘C" },
                new ArcFloatingButtonItem
                {
                    Id = "link",
                    Label = "Copy link",
                    Icon = "link",
                    Tooltip = "Copy link",
                    Shortcut = "⌘L"
                }
            }
        };
        selection.ActionInvoked += (_, id) => status.Text = $"Last action: {id ?? "none"}";

        var rail = new ArcFloatingButtonGroup
        {
            Label = "Canvas tools",
            Orientation = ArcFloatingButtonOrientation.Vertical,
            Size = ArcFloatingButtonSize.Sm,
            VerticalAlignment = VerticalAlignment.Top,
            Items = new[]
            {
                new ArcFloatingButtonItem { Id = "zoom-in", Label = "Zoom in", Icon = "zoom-in", IconOnly = true },
                new ArcFloatingButtonItem { Id = "zoom-out", Label = "Zoom out", Icon = "zoom-out", IconOnly = true },
                new ArcFloatingButtonItem { Id = "fit", Label = "Zoom to fit", Icon = "maximize", IconOnly = true }
            }
        };
        rail.ActionInvoked += (_, id) => status.Text = $"Last action: {id ?? "none"}";

        var note = Note("Hover or focus a button to see the shared highlight; Present shows the pressed tint. Icon-only actions carry a tooltip with their label and shortcut.");

        return new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                DemoUi.Row(board, selection, rail),
                note,
                status
            }
        };
    }

    private static Control IconMorphDemo()
    {
        var row = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var pairs = new (string Caption, ArcIconMorphName Name, bool State)[]
        {
            ("Menu", ArcIconMorphName.Menu, false),
            ("Play", ArcIconMorphName.Play, true),
            ("Plus", ArcIconMorphName.Plus, true),
            ("Copy", ArcIconMorphName.Copy, true),
            ("Theme", ArcIconMorphName.Theme, true),
            ("Chevron", ArcIconMorphName.Chevron, false)
        };

        foreach (var pair in pairs)
        {
            var morph = new ArcIconMorph
            {
                Icon = pair.Name,
                State = pair.State,
                Size = 20,
                Foreground = DemoUi.Brush(ArcTokens.Foreground)
            };

            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Secondary,
                Compact = true,
                Width = 44,
                Height = 44,
                MinWidth = 44,
                MinHeight = 44,
                Padding = new Thickness(0),
                Content = morph,
                Foreground = DemoUi.Brush(ArcTokens.Foreground)
            };
            ArcAdvancedVisuals.SetName(button, $"{pair.Caption} icon");
            button.Click += (_, _) => morph.State = !morph.State;

            var column = new StackPanel
            {
                Width = 64,
                Spacing = 8,
                Children =
                {
                    button,
                    DemoUi.Caption(pair.Caption)
                }
            };
            column.Margin = new Thickness(0, 0, 12, 12);
            row.Children.Add(column);
        }

        return new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                row,
                Note("Click any icon to toggle it. The two layers cross-fade, turn and scale on the snappy spring; true SVG path interpolation is approximated. The chevron turns 180° by default.")
            }
        };
    }

    private static TextBlock Note(string text)
    {
        var block = DemoUi.Body(text);
        block.HorizontalAlignment = HorizontalAlignment.Left;
        return block;
    }
}
