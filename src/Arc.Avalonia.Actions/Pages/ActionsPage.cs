using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>Buttons and joined action groups: the action surface of the system.</summary>
public sealed class ActionsPage : ArcPage
{
    public override string Title => "Actions";

    public override string Group => "Actions";

    public override string? Description =>
        "Joined action groups, split actions and in-place confirmation. Each control keeps a quiet resting face.";

    public override int Order => 200;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            ActionDemos.Section(
                "Button group",
                "Two to five related actions on one object, joined on one surface with hairline dividers and one highlight gliding between segments.",
                ButtonGroupDemo()),
            ActionDemos.Section(
                "Split button",
                "One default action plus close variants behind the chevron, for when a menu would hide the usual choice.",
                SplitButtonDemo()),
            ActionDemos.Section(
                "Copy button",
                "Copies a value and confirms in place; the label returns to Copy after about two seconds.",
                CopyButtonDemo()),
            ActionDemos.Section(
                "Action swap",
                "One control that cycles through related states such as Follow and Following, morphing its label, icon and tone together.",
                ActionSwapDemo())
        }
    };

    private static Control ButtonGroupDemo()
    {
        var withMenu = new ArcButtonGroup
        {
            VerticalAlignment = VerticalAlignment.Top,
            Label = "Zoom controls",
            Size = ArcButtonGroupSize.Sm,
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Zoom out", Icon = "zoom-out", IconOnly = true },
                new ArcButtonGroupItem
                {
                    Label = "Zoom level",
                    Content = new TextBlock { Text = "100%", Classes = { "arc-body-sm" } }
                },
                new ArcButtonGroupItem { Label = "Zoom in", Icon = "zoom-in", IconOnly = true }
            },
            Menu = new ArcMenuDefinition
            {
                Label = "More actions",
                Items =
                {
                    new ArcMenuItem { Label = "Fit to width", Icon = "maximize" },
                    new ArcMenuItem { Label = "Actual size", Shortcut = "0" },
                    new ArcMenuItem
                    {
                        Label = "Delete layer",
                        Icon = "trash-2",
                        IsDestructive = true,
                        SeparatorBefore = true
                    }
                }
            },
            IsOpen = true
        };

        var vertical = new ArcButtonGroup
        {
            VerticalAlignment = VerticalAlignment.Top,
            Label = "Canvas controls",
            Orientation = ArcButtonGroupOrientation.Vertical,
            Size = ArcButtonGroupSize.Sm,
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Zoom in", Icon = "plus", IconOnly = true },
                new ArcButtonGroupItem { Label = "Zoom out", Icon = "minus", IconOnly = true },
                new ArcButtonGroupItem { Label = "Fit", Icon = "maximize", IconOnly = true }
            }
        };

        var share = new ArcButtonGroup
        {
            Label = "Document actions",
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Share", Icon = "link" },
                new ArcButtonGroupItem { Label = "Duplicate", Icon = "copy" },
                new ArcButtonGroupItem { Label = "Archive", Icon = "archive" }
            }
        };

        var solid = new ArcButtonGroup
        {
            Label = "Review actions",
            Variant = ArcButtonGroupVariant.Solid,
            Items = new[]
            {
                new ArcButtonGroupItem { Label = "Approve" },
                new ArcButtonGroupItem { Label = "Request changes" },
                new ArcButtonGroupItem { Label = "Draft" }
            }
        };

        return ActionDemos.Row(
            ActionDemos.Reserved(215, 186, withMenu),
            vertical,
            new StackPanel { Spacing = 12, Children = { share, solid } });
    }

    private static Control SplitButtonDemo()
    {
        var primary = new ArcSplitButton
        {
            VerticalAlignment = VerticalAlignment.Top,
            Label = "Publish",
            Icon = "upload",
            Actions = new[]
            {
                new ArcMenuItem { Label = "Publish and notify", Icon = "bell" },
                new ArcMenuItem { Label = "Publish as draft", Icon = "file" },
                new ArcMenuItem
                {
                    Label = "Discard changes",
                    Icon = "trash-2",
                    IsDestructive = true,
                    SeparatorBefore = true
                }
            },
            IsOpen = true
        };

        var secondary = new ArcSplitButton
        {
            VerticalAlignment = VerticalAlignment.Top,
            Label = "Export",
            Icon = "download",
            Variant = ArcSplitButtonVariant.Secondary,
            Actions = new[]
            {
                new ArcMenuItem { Label = "Export as CSV", Shortcut = "⌘E" },
                new ArcMenuItem { Label = "Export as JSON" },
                new ArcMenuItem { Label = "Export as PDF" }
            }
        };

        return ActionDemos.Row(ActionDemos.Reserved(240, 196, primary), secondary);
    }

    private static Control CopyButtonDemo()
    {
        const string key = "arc_live_51N8kQ2Lm4X";

        return ActionDemos.Row(
            new ArcCopyButton { Value = key, Label = "Copy key" },
            new ArcCopyButton { Value = key, Label = "Copy key", IconOnly = true },
            new ArcCopyButton { Value = key, Label = "Copy install command", State = ArcCopyState.Copied },
            new ArcCopyButton { Value = key, Label = "Copy link", Variant = ArcButtonVariant.Ghost });
    }

    private static Control ActionSwapDemo()
    {
        var follow = new ArcActionSwap
        {
            Value = "following",
            States = new[]
            {
                new ArcActionSwapState { Id = "follow", Label = "Follow", Icon = "plus", Tone = ArcActionSwapTone.Accent },
                new ArcActionSwapState { Id = "following", Label = "Following", Icon = "check", Tone = ArcActionSwapTone.Quiet }
            }
        };

        var invite = new ArcActionSwap
        {
            States = new[]
            {
                new ArcActionSwapState { Id = "send", Label = "Send invite", Icon = "send", Tone = ArcActionSwapTone.Accent },
                new ArcActionSwapState
                {
                    Id = "sent",
                    Label = "Sent",
                    Icon = "check",
                    Tone = ArcActionSwapTone.Success,
                    PendingLabel = "Sending",
                    HoldFor = 2000
                }
            }
        };

        var save = new ArcActionSwap
        {
            Value = "saved",
            States = new[]
            {
                new ArcActionSwapState { Id = "save", Label = "Save draft", Icon = "save", Tone = ArcActionSwapTone.Neutral },
                new ArcActionSwapState { Id = "saved", Label = "Saved", Icon = "check", Tone = ArcActionSwapTone.Success }
            }
        };

        return ActionDemos.Row(follow, invite, save);
    }
}

