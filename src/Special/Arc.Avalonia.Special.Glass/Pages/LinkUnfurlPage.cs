using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Glass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Pages;

/// <summary>The Link unfurl catalog page: a composer that unrolls a pasted link into a preview card.</summary>
public sealed class LinkUnfurlPage : ArcItemPage
{
    public override string ItemSlug => "link-unfurl";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A message or comment composer where pasted links should show what they point to before sending.",
        "Product marketing that shows how a chat or notes app handles links."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Enter sends; Alt and Enter shows or collapses the preview of the link.",
        "Every card has named collapse and remove buttons, and the title is a real link target.",
        "A folded card leaves the tab order, and a polite status line announces adding, loading, opening, collapsing, removal and sending."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The link carries a soft loading sweep, then its width grows to fit the title while the favicon and title roll in.",
        "The card unrolls from the link's own rectangle: width first, height after, with the body fading in after the header.",
        "Collapsing runs the same path backwards on a quicker spring; reduced motion opens and folds instantly with a still tint instead of the sweep."
    };

    public override string? ImplementationNotes =>
        "The card morph animates its height and clip rather than interpolating a clip path from the inline link, and the loading sweep is a translated gradient because there is no CSS background-position animation. Preview artwork is painted from tokens and the resolver is simulated locally. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var view = new ArcLinkUnfurl
        {
            Label = "Message #lisbon-offsite",
            Value = "Found a place for the offsite, take a look",
            ShowSamples = true,
            IsExpanded = true,
            Samples = new List<ArcLinkPreview>
            {
                new()
                {
                    Url = "https://uiarc.dev/components",
                    Site = "uiarc.dev",
                    Title = "The Arc component library",
                    Description = "Quiet, precise components with motion and glass that respect the reader.",
                    Scene = ArcGlassScene.Coast
                },
                new()
                {
                    Url = "https://fieldnotes.example/alfama",
                    Site = "fieldnotes.example",
                    Title = "A slow walk through Alfama",
                    Description = "Steps, shade and the sound of a hundred windows opening at once.",
                    Scene = ArcGlassScene.Garden
                }
            }
        };

        var stage = GlassDemo.Stage(view, ArcGlassScene.Garden, 450, 20, seed: 12);
        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("Click the link to fold the card, drag the card up, or press one of the Try links to watch the loading sweep and unfurl again.")
            }
        };
    }
}


