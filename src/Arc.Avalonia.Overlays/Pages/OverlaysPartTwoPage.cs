using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Overlays.Controls;
using Arc.Avalonia.Actions.Pages;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Overlays.Pages;

/// <summary>Popovers, toasts and alerts: the small surfaces and the messages that follow an action.</summary>
public sealed class OverlaysPartTwoPage : ArcPage
{
    public override string Title => "Overlays, part two";

    public override string Group => "Actions";

    public override string? Description =>
        "A small anchored surface, transient confirmations in a stack, and the persistent in-flow message.";

    public override int Order => 204;

    public override Control Build(ArcDemoContext context)
    {
        var toastHost = new ArcToastHost();
        toastHost.Show("Changes saved", "Your billing email is updated.", "Undo", null, 0);
        toastHost.Show("Invite sent to Sam", null, null, null, 0);

        var saveButton = new ArcButton
        {
            Content = "Save changes",
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm
        };
        saveButton.Click += (_, _) => toastHost.Show("Changes saved", "Your billing email is updated.", "Undo");

        return new StackPanel
        {
            Children =
            {
                ActionDemos.Section(
                    "Popover",
                    "A small anchored surface for contextual content; clicking the trigger opens it, an outside press or Escape closes it.",
                    PopoverDemo()),
                ActionDemos.Section(
                    "Toast",
                    "Transient confirmation after a background action, stacked at the bottom right. The host is a control the page places once.",
                    ToastDemo(toastHost, saveButton)),
                ActionDemos.Section(
                    "Alert",
                    "A persistent message in the page flow with a tone, one action and an optional close. Use it when the message must stay.",
                    AlertDemo())
            }
        };
    }

    private static Control PopoverDemo()
    {
        var stage = ActionDemos.Stage(640, 210, mockPage: false);

        var trigger = new ArcButton
        {
            Content = "Share",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };

        var popover = new ArcPopover
        {
            Label = "Share settings",
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Trigger = trigger,
            IsOpen = true,
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Share with the team", Classes = { "arc-label" } },
                    new TextBlock
                    {
                        Text = "Anyone in Acme Studio with the link can view this board.",
                        Classes = { "arc-caption" },
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 260
                    },
                    new ArcButton
                    {
                        Content = "Copy link",
                        Variant = ArcButtonVariant.Secondary,
                        Size = ArcButtonSize.Sm,
                        HorizontalAlignment = HorizontalAlignment.Left
                    }
                }
            }
        };

        popover.Margin = new Thickness(24);
        stage.Children.Add(popover);
        return stage;
    }

    private static Control ToastDemo(ArcToastHost host, ArcButton save)
    {
        var stage = ActionDemos.Stage(880, 160);
        stage.Children.Add(host);

        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                ActionDemos.Row(save),
                stage
            }
        };
    }

    private static Control AlertDemo()
    {
        var dismissible = new ArcAlert
        {
            Title = "Your card expires next month",
            Tone = ArcAlertTone.Warning,
            ActionLabel = "Update card",
            Content = ActionDemos.Caption("Invoices after 12 April will fail until a new payment method is added.")
        };

        return new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                new ArcAlert
                {
                    Title = "Changes saved",
                    Tone = ArcAlertTone.Success,
                    IsDismissible = false,
                    Content = ActionDemos.Caption("The billing email now goes to finance@acme.com.")
                },
                new ArcAlert
                {
                    Title = "Upload failed",
                    Tone = ArcAlertTone.Danger,
                    ActionLabel = "Try again",
                    Content = ActionDemos.Caption("The file is larger than 25 MB. Compress it and upload again.")
                },
                new ArcAlert
                {
                    Title = "A new version is available",
                    Tone = ArcAlertTone.Info,
                    IsDismissible = false,
                    Content = ActionDemos.Caption("Reload to get the latest fixes. Nothing is lost.")
                },
                dismissible
            }
        };
    }
}
