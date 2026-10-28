using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery.Pages;

/// <summary>Foundation sanity page: buttons, badges, cards, icons and the spinner on the token layer.</summary>
public sealed class ProofPage : ArcPage
{
    public override string Title => "Kit proof";

    public override string Group => "Overview";

    public override string? Description => "A quick read of the base controls every component builds on: buttons at three sizes and four weights, badges, cards, icons and the spinner.";

    public override int Order => 1;

    public override Control Build(ArcDemoContext context)
    {
        var stack = new StackPanel();

        stack.Children.Add(DemoUi.Section(
            "Buttons",
            "One primary action per surface. Secondary is a quiet neutral, ghost is for toolbars, danger only for destructive commits.",
            new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    DemoUi.Row(
                        new ArcButton { Content = "Primary", Variant = ArcButtonVariant.Primary },
                        new ArcButton { Content = "Secondary", Variant = ArcButtonVariant.Secondary },
                        new ArcButton { Content = "Ghost", Variant = ArcButtonVariant.Ghost },
                        new ArcButton { Content = "Delete", Variant = ArcButtonVariant.Danger }),
                    DemoUi.Row(
                        new ArcButton { Content = "Small", Size = ArcButtonSize.Sm },
                        new ArcButton { Content = "Medium", Size = ArcButtonSize.Md },
                        new ArcButton { Content = "Large", Size = ArcButtonSize.Lg },
                        new ArcButton { Content = "Saving", Loading = true }),
                    DemoUi.Row(
                        new ArcButton { Content = new ArcIcon { Icon = "plus", Size = 16 }, Compact = true, Variant = ArcButtonVariant.Secondary },
                        new ArcButton { Content = new ArcIcon { Icon = "settings", Size = 18 }, Compact = true, Variant = ArcButtonVariant.Ghost },
                        new ArcButton { Content = "Publish", Loading = true },
                        new ArcButton { Content = "Disabled", IsEnabled = false })
                }
            }));

        stack.Children.Add(DemoUi.Section(
            "Badges",
            "Short status labels: neutral for metadata, accent for emphasis, status tones only when something succeeded, needs attention, or failed.",
            DemoUi.Row(
                new ArcBadge { Content = "Draft" },
                new ArcBadge { Content = "Recommended", Tone = ArcBadgeTone.Accent },
                new ArcBadge { Content = "Active", Tone = ArcBadgeTone.Success, Dot = true },
                new ArcBadge { Content = "Trial ends soon", Tone = ArcBadgeTone.Warning },
                new ArcBadge { Content = "Failed", Tone = ArcBadgeTone.Danger, Dot = true })));

        stack.Children.Add(DemoUi.Section(
            "Cards",
            "Cards rest on a hairline border with the surface radius; shadows stay for floating layers.",
            new WrapPanel
            {
                Children =
                {
                    new ArcCard
                    {
                        Width = 300,
                        Content = new StackPanel
                        {
                            Spacing = 8,
                            Children =
                            {
                                new TextBlock { Text = "Project brief", Classes = { "arc-label" } },
                                new TextBlock
                                {
                                    Text = "A contained group of related content and actions, with a real boundary.",
                                    Classes = { "arc-body-sm", "arc-secondary" },
                                    TextWrapping = TextWrapping.Wrap
                                },
                                new ArcButton { Content = "Open", Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm, HorizontalAlignment = HorizontalAlignment.Left }
                            }
                        }
                    },
                    new ArcCard
                    {
                        Width = 300,
                        Muted = true,
                        Content = new TextBlock
                        {
                            Text = "Muted card for nested groups.",
                            Classes = { "arc-body-sm", "arc-secondary" }
                        }
                    }
                }
            }));

        stack.Children.Add(DemoUi.Section(
            "Icons",
            "Lucide on the 24 unit grid at stroke 1.75, round caps, currentColor. Size 16, 20, or 24.",
            new WrapPanel
            {
                Children =
                {
                    Icon("check"), Icon("chevron-down"), Icon("search"), Icon("settings"), Icon("calendar"),
                    Icon("bell"), Icon("copy"), Icon("trash-2"), Icon("plus"), Icon("x"), Icon("arrow-right"),
                    Icon("sparkles"), Icon("chart-line"), Icon("users"), Icon("lock")
                }
            }));

        stack.Children.Add(DemoUi.Section(
            "Loading",
            "The spinner is the shared Arc arc: 270 degrees, round caps, sized by the caller.",
            DemoUi.Row(
                new ArcSpinner { Size = 16, Width = 16, Height = 16, StrokeWidth = 2 },
                new ArcSpinner { Size = 24, Width = 24, Height = 24, StrokeWidth = 2.5 },
                new ArcSpinner { Size = 32, Width = 32, Height = 32, StrokeWidth = 3 })));

        // The last section does not need the full inter-section gap; trimming it keeps the page under 1400 px.
        if (stack.Children.Count > 0)
        {
            stack.Children[^1].Margin = new Thickness(0, 0, 0, 30);
        }

        return stack;
    }

    private static Control Icon(string name) => new Border
    {
        Width = 56,
        Height = 56,
        CornerRadius = new CornerRadius(18),
        Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
        Child = new ArcIcon
        {
            Icon = name,
            Size = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }
    };
}


