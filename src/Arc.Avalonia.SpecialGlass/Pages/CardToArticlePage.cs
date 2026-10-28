using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialGlass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Pages;

/// <summary>The Card to article catalog page: a story grid that morphs into one article.</summary>
public sealed class CardToArticlePage : ArcItemPage
{
    public override string ItemSlug => "card-to-article";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Blogs, journals and changelogs where each entry has a strong image.",
        "Editorial landing sections that preview stories without leaving the page."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Enter or Space opens the focused card; Escape closes the article and returns focus to its card.",
        "Tab stays inside the open article, and the close button is the first stop.",
        "The card is named by its tag, title and reading time; the article is a named dialog labelled by its title."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The card itself becomes the article: its box and corners spring to the sheet while the hero scales as one piece.",
        "The body rises in after the morph and a reading progress bar tracks the scroll.",
        "Dragging the sheet down shrinks it and closes past a threshold; reduced motion opens and closes without travel."
    };

    public override string? ImplementationNotes =>
        "The morph animates the article rectangle between the measured card slot and the sheet target instead of a shared layout animation, so text reflows during the flight. Story artwork is painted from tokens, and the drag gesture runs on the hero. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var view = new ArcCardToArticle
        {
            AspectRatio = 16.0 / 9.0,
            OpenId = "glass-house",
            Items = new List<ArcArticleItem>
            {
                new()
                {
                    Id = "glass-house",
                    Tag = "Case study",
                    Title = "A glass house that stays cool in August",
                    Meta = "5 min read",
                    Scene = ArcGlassScene.Villa,
                    Body =
                        "The brief was simple and a little unreasonable: a house that feels like it is outdoors, in a city where August does not forgive glass.\nWe started with the section, not the view. Deep eaves, a hollow core and one long pool that pulls air through the plan do most of the work before any machine switches on.\nThe result is a house that behaves like a courtyard: bright at noon, cool by four, and quiet enough to hear the water."
                },
                new()
                {
                    Id = "light",
                    Tag = "Journal",
                    Title = "The light in October",
                    Meta = "3 min read",
                    Scene = ArcGlassScene.Coast,
                    Body =
                        "October light arrives sideways. It finds the back of the room first and makes the kitchen look borrowed from someone else's holiday.\nWe spent the month photographing the same three corners at the same time, which is a slow way to learn that a room is mostly a schedule."
                }
            }
        };

        var stage = GlassDemo.Stage(view, ArcGlassScene.Studio, 500, 16, seed: 11);
        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("The first story is open. Press Escape or drag its hero down to shrink it back into its slot, then open either card.")
            }
        };
    }
}
