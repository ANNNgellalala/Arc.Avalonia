using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialGlass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Pages;

/// <summary>The Glass tab bar catalog page: a floating Liquid Glass bar over a scrolling feed.</summary>
public sealed class GlassTabBarPage : ArcItemPage
{
    public override string ItemSlug => "glass-tabbar";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Mobile web apps and PWAs with three to five top level sections and one global action such as search.",
        "Content feeds where the bar should step aside while reading and come back on scroll up."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tab moves focus to the selected tab; the tab list is one stop.",
        "Left and Right move to and select the previous or next tab, including the search action, and wrap; Home and End jump.",
        "Enter or Space selects the focused tab and grows the bar when it is compact.",
        "Every tab keeps its name when the bar is compact, badges carry a count, and selection is shown by the lens, the tint and the label."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The lens glides on the smooth spring; its speed lifts and stretches it along the travel and it magnifies the icon beneath it before settling.",
        "Each tab's tint and icon scale follow the lens position, so the accent glides instead of switching.",
        "Dragging the selected tab moves the lens across the bar, and releasing snaps it to the nearest tab. One spring drives the compact morph."
    };

    public override string? ImplementationNotes =>
        "There is no backdrop filter in Avalonia, so the bar refracts the feed as layered translucency; magnification is an icon scale driven by the lens position. Dragging is a mouse drag on desktop, and scroll compacting is wired by the page through the bar's Compact property. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var feed = new StackPanel { Spacing = 12, Margin = new Thickness(14, 14, 14, 96) };
        feed.Children.Add(FeedCard("Weekend guide", "Twelve places to swim near Lisbon", ArcGlassScene.Coast, 0));
        feed.Children.Add(FeedCard("New in the studio", "The critique wall got a rehang", ArcGlassScene.Studio, 1));
        feed.Children.Add(FeedCard("Field notes", "A slow walk through Alfama", ArcGlassScene.Villa, 2));
        feed.Children.Add(FeedCard("Saved for later", "The light in October", ArcGlassScene.Garden, 3));

        var scroller = new ScrollViewer
        {
            Height = 430,
            Content = feed,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var bar = new ArcGlassTabBar
        {
            Label = "Sections",
            Value = "home",
            Items = new List<ArcGlassTabBarItem>
            {
                new() { Value = "home", Label = "Home", Icon = "layout-dashboard" },
                new() { Value = "explore", Label = "Explore", Icon = "compass" },
                new() { Value = "saved", Label = "Saved", Icon = "bookmark", Badge = "3" },
                new() { Value = "profile", Label = "Profile", Icon = "user" }
            },
            Action = new ArcGlassTabBarItem { Value = "search", Label = "Search", Icon = "search" }
        };
        bar.HorizontalAlignment = HorizontalAlignment.Center;
        bar.VerticalAlignment = VerticalAlignment.Bottom;
        bar.Margin = new Thickness(0, 0, 0, 20);

        var lastOffset = 0d;
        scroller.ScrollChanged += (_, _) =>
        {
            var offset = scroller.Offset.Y;
            if (offset > lastOffset + 6)
            {
                bar.Compact = true;
            }
            else if (offset < lastOffset - 6)
            {
                bar.Compact = false;
            }

            lastOffset = offset;
        };

        var layout = new Grid { Height = 430, ClipToBounds = true };
        layout.Children.Add(scroller);
        layout.Children.Add(new Panel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 18),
            Children = { bar }
        });

        var stage = GlassDemo.Stage(layout, ArcGlassScene.Sand, 430, 0, seed: 4);

        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("Scroll the feed down and the bar compacts to icons; scroll up and it grows back. Drag the selected tab to glide the lens, or use the arrow keys.")
            }
        };
    }

    private static Control FeedCard(string title, string subtitle, ArcGlassScene scene, int seed)
    {
        var backdrop = new ArcGlassBackdrop { Scene = scene, Corner = 18, Seed = seed, Vignette = 0.4 };
        var shade = new Border { Background = ArcGlass.PhotoShade(0.5), IsHitTestVisible = false };
        var wash = new Border
        {
            Background = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Background), 0.14)),
            IsHitTestVisible = false
        };
        var text = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(16, 0, 16, 14)
        };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.Medium,
            Foreground = ArcGlass.Solid(ArcGlass.PhotoInk)
        });
        text.Children.Add(new TextBlock
        {
            Text = subtitle,
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted)
        });

        return new Border
        {
            Height = 150,
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            Child = new Panel { Children = { backdrop, shade, wash, text } }
        };
    }
}




