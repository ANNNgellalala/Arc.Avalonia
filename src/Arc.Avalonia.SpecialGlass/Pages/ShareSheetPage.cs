using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialGlass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Pages;

/// <summary>The Share sheet catalog page: a Share button that unfolds into the glass share panel.</summary>
public sealed class ShareSheetPage : ArcItemPage
{
    public override string ItemSlug => "share-sheet";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A Share button on a document, design or board header where people share often.",
        "Sharing flows that combine copy link, an access level, and sending to a few recent people."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Enter or Space opens the panel from the Share button; Escape folds it back and returns focus to the button.",
        "Arrows move between access options and wrap; Tab moves through copy, access, channels, people and send.",
        "Copy, picks and channel results are announced in a polite status line; picked people become chips."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "One shape springs its size and corners between button, panel and confirmation.",
        "Picked people turn into chips, the send count rolls up, and sending folds into a sent confirmation.",
        "Reduced motion jumps the shape to size and swaps faces with a plain fade."
    };

    public override string? ImplementationNotes =>
        "The panel morph is a scale from the trigger rectangle rather than a CSS auto-height layout animation, and the access list stays open instead of the Escape-closes-list-first detail. The clipboard can be unavailable on a headless run, in which case the row reports a failure. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var sheet = new ArcShareSheet
        {
            Title = "Q3 roadmap",
            Link = "https://example.com/d/q3-roadmap",
            AccessValue = "team",
            People = new List<ArcSharePerson>
            {
                new() { Id = "ana", Name = "Ana Ruiz" },
                new() { Id = "sam", Name = "Sam Lee" },
                new() { Id = "noah", Name = "Noah Park" }
            },
            Access = new List<ArcShareAccessOption>
            {
                new() { Value = "team", Label = "Team", Description = "Anyone on the team can view", Icon = "lock" },
                new() { Value = "public", Label = "Anyone with the link", Description = "Can view and comment", Icon = "globe" }
            },
            Channels = new List<ArcShareChannel>
            {
                new() { Id = "slack", Label = "Slack", Icon = "message-circle", DoneLabel = "Posted" },
                new() { Id = "email", Label = "Email", Icon = "mail", DoneLabel = "Sent" }
            }
        };

        sheet.Pick("ana");
        sheet.HorizontalAlignment = HorizontalAlignment.Right;
        sheet.VerticalAlignment = VerticalAlignment.Top;
        sheet.Margin = new Thickness(0, 18, 18, 0);

        var document = new Border
        {
            Width = 660,
            Height = 380,
            CornerRadius = new CornerRadius(22),
            Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Surface)),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Border)),
            BorderThickness = new Thickness(1),
            BoxShadow = ArcGlass.Shadow(ArcTokens.ShadowRaised),
            Child = new StackPanel
            {
                Spacing = 14,
                Margin = new Thickness(24, 22, 24, 22),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Q3 roadmap",
                        FontSize = 26,
                        FontWeight = FontWeight.Medium,
                        Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
                    },
                    new TextBlock
                    {
                        Text = "Edited by Ana Ruiz · 12 minutes ago",
                        Classes = { "arc-caption" },
                        Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
                    },
                    new Border
                    {
                        Height = 1,
                        Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Border)),
                        Margin = new Thickness(0, 4)
                    },
                    new TextBlock
                    {
                        Text = "Outcome",
                        Classes = { "arc-label" },
                        Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
                    },
                    new TextBlock
                    {
                        Text = "Ship the glass and surfaces special set, then move the catalogue to the new sidebar taxonomy. The team keeps the last week of September free for the migration.",
                        Classes = { "arc-body-sm" },
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
                    }
                }
            }
        };

        var layer = new Grid { Children = { document, sheet } };
        var stage = GlassDemo.Stage(layer, ArcGlassScene.Dusk, 600, 20, seed: 13);
        sheet.IsOpen = true;

        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("The panel opens from the Share button. Copy the link, change access, post to a channel or pick someone and send, then close it with Escape.")
            }
        };
    }
}



