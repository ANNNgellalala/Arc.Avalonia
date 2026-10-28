using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIWidgets.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIWidgets.Pages;

/// <summary>
/// Catalog page for skeleton morph. Two profile cards show the same layout loading and loaded; Finish loading
/// resolves the left card block by block so the stagger can be watched.
/// </summary>
public sealed class SkeletonMorphPage : ArcItemPage
{
    public override string ItemSlug => "skeleton-morph";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var loadingCard = BuildCard("Mina Okafor", "Design engineer · Arc", loading: true, out var loadingRegion);
        var loadedCard = BuildCard("Mina Okafor", "Design engineer · Arc", loading: false, out _);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16 };
        grid.Children.Add(loadingCard);
        Grid.SetColumn(loadedCard, 1);
        grid.Children.Add(loadedCard);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var finish = new ArcButton { Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm, Content = "Finish loading" };
        finish.Click += (_, _) => loadingRegion.Loading = false;
        var reload = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Reload" };
        reload.Click += (_, _) => loadingRegion.Loading = true;
        actions.Children.Add(finish);
        actions.Children.Add(reload);

        var hint = Sv.Caption("The left card resolves block by block in reading order, 40 ms apart.");
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        footer.Children.Add(actions);
        footer.Children.Add(hint);

        return new StackPanel { Spacing = 14, Children = { grid, footer } };
    }

    private static Control BuildCard(string name, string role, bool loading, out ArcSkeletonMorph region)
    {
        var blocks = new StackPanel { Spacing = 12 };

        var avatar = new ArcMorphBlock
        {
            Circle = true,
            Width = 48,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(24),
                Child = new TextBlock
                {
                    Text = "MO",
                    FontSize = 15,
                    FontWeight = FontWeight.Medium,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
        ((Border)avatar.Content).Paint(Border.BackgroundProperty, ArcTokens.AccentSubtle);
        blocks.Children.Add(avatar);

        blocks.Children.Add(new ArcMorphBlock
        {
            Lines = 1,
            LineHeight = 22,
            Content = new TextBlock { Text = name, Classes = { "arc-heading" } }
        });

        blocks.Children.Add(new ArcMorphBlock
        {
            Lines = 1,
            LineHeight = 18,
            Content = new TextBlock { Text = role, Classes = { "arc-body-sm", "arc-secondary" } }
        });

        blocks.Children.Add(new ArcMorphBlock
        {
            Lines = 3,
            LineHeight = 19,
            Content = new TextBlock
            {
                Text = "Draws the motion language for Arc's surfaces and writes the specs the rest of the team builds against.",
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap
            }
        });

        var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        chips.Children.Add(new ArcBadge { Content = "Motion", Tone = ArcBadgeTone.Neutral });
        chips.Children.Add(new ArcBadge { Content = "Design systems", Tone = ArcBadgeTone.Neutral });
        blocks.Children.Add(new ArcMorphBlock
        {
            Lines = 1,
            LineHeight = 24,
            Content = chips
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(new ArcButton { Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm, Content = "Follow" });
        buttons.Children.Add(new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Message" });
        blocks.Children.Add(new ArcMorphBlock
        {
            Lines = 1,
            LineHeight = 36,
            Content = buttons
        });

        var card = new Border
        {
            CornerRadius = Sv.Radius(ArcTokens.RadiusPanel),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18),
            Child = blocks
        };
        card.Paint(Border.BackgroundProperty, ArcTokens.Surface);
        card.Paint(Border.BorderBrushProperty, ArcTokens.Border);

        region = new ArcSkeletonMorph { Loading = loading, Stagger = 0.04, Child = card };
        return region;
    }

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Profile cards, feeds and lists that load in under a few seconds.",
        "Dashboards where the layout is known before the data arrives.",
        "Places where a hard swap from grey boxes to content would feel jarring."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "For long lists of hundreds of rows, use a plain skeleton; layout animation there is costly.",
        "When loading takes more than a few seconds, use a progress indicator."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The region is marked busy while loading and carries a status label for screen readers.",
        "Skeleton shapes are hidden from assistive technology and the content is not read until it is ready.",
        "Blocks keep reading order, so focus order is the same before and after loading."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Blocks resolve one after another in reading order, 40 ms apart by default.",
        "Each placeholder fades while the content sharpens in underneath, in place, so nothing travels across the layout.",
        "A calm opacity pulse runs on the skeletons while loading and stops when they resolve; reduced motion removes the pulse and uses a short crossfade."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public skeleton-morph documentation. Blocks keep the loaded content's box, so the placeholder does not stretch its size " +
        "into a measured box; the resolve is a fade with a 1.5 percent scale rather than a measured spring between two boxes, and there is no blur pass. " +
        "Going back to loading is immediate, so a fast reload never leaves a block halfway.";
}
