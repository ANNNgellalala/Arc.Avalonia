using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Glass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Pages;

/// <summary>The Glass card catalog page: a photo card with a pointer lit rim and a morphing detail sheet.</summary>
public sealed class GlassCardPage : ArcItemPage
{
    public override string ItemSlug => "glass-card";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "variant-rim", "variant-rating", "variant-actions"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Profile, host, or creator cards on a landing page or in a discovery grid.",
        "A featured product or stay where the photo sells and the details can wait for a tap."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tab moves to the glass header, then into the detail controls while the card is open.",
        "Enter or Space expands or collapses; Escape collapses and keeps focus on the header.",
        "The header is a real button with an expanded state, and detail controls leave the tree while collapsed.",
        "The artwork is named by its alt text; keyboard focus draws Arc's two pixel outline."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Tilt and the rim highlight follow the pointer on springs, so the card eases in and settles when the pointer leaves.",
        "Expanding springs the glass panel while the header stays put, the details fade up and the photo scales slightly.",
        "Reduced motion removes the tilt and the morph: the panel switches size and the highlight stays centred."
    };

    public override string? ImplementationNotes =>
        "Tilt uses an Avalonia Rotate3DTransform with a fixed perspective depth, and the rim highlight is a painted gradient because a border cannot be masked. The glass is layered translucency, and the portrait is painted from tokens because the project ships no photography. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var card = new ArcGlassCard
        {
            Title = "Jasmine Brooks",
            Subtitle = "Design lead, Lisbon",
            Aside = "4.9",
            Body = "Leads the design system at Meridian. Hosts the Thursday critique and keeps the Lisbon studio full of plants.",
            ActionLabel = "Follow",
            SecondaryLabel = "Message",
            IsOpen = true,
            Scene = ArcGlassScene.Villa
        };
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;

        var notes = BuildNotes();
        notes.HorizontalAlignment = HorizontalAlignment.Right;
        notes.VerticalAlignment = VerticalAlignment.Center;

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 40,
            Children = { card, notes }
        };
        Grid.SetColumn(notes, 1);

        var stage = GlassDemo.Stage(body, ArcGlassScene.Dusk, 440, 20, seed: 5);
        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("Move the pointer over the card to light its rim; tap the artwork or press Enter on the header to morph the glass into the detail sheet.")
            }
        };
    }

    private static Control BuildNotes()
    {
        var panel = ArcGlass.Panel(22, new Thickness(20), floating: true, fillAlpha: 0.55);
        panel.Width = 300;

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = "One featured person",
            Classes = { "arc-label" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        });
        stack.Children.Add(new TextBlock
        {
            Text = "The photo carries the identity; the glass panel holds the name, rating and the details that wait for a tap.",
            Classes = { "arc-body-sm" },
            TextWrapping = TextWrapping.Wrap,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
        });

        stack.Children.Add(NoteRow("sparkles", "Rim highlight follows the pointer"));
        stack.Children.Add(NoteRow("move", "Tap or Enter expands the sheet"));
        stack.Children.Add(NoteRow("corner-down-left", "Escape collapses it"));

        panel.Child = stack;
        return panel;
    }

    private static Control NoteRow(string icon, string text)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(ArcGlass.Icon(icon, 15, ArcGlass.Solid(ArcGlass.Color(ArcTokens.Accent))));
        row.Children.Add(new TextBlock
        {
            Text = text,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 220,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
        });
        return row;
    }
}
