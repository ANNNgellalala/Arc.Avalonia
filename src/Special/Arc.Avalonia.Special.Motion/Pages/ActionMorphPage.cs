using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Motion.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Pages;

/// <summary>Action morph: a floating create button that becomes a menu, then an inline composer.</summary>
public sealed class ActionMorphPage : ArcItemPage
{
    public override string ItemSlug => "action-morph";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-button", "state-menu", "state-composer", "variant-corner-start"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Quick capture of tasks, notes or comments without leaving the page.",
        "Apps with two or three frequent create actions that deserve one persistent button.",
        "Compact layouts where a corner button should expand into a small form."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use a dialog or sheet for longer forms with several fields.",
        "Use a dropdown menu when actions run immediately and need no input."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Enter or Space opens the menu from the button and focuses the first action; arrows wrap, Home and End jump.",
        "T and N open the task or note composer while the menu is open, with the shortcut shown on each row.",
        "Enter submits a composer; Escape goes back one face or closes the menu and returns focus to the button."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "One surface springs its width, height and corner radius: growing carries a little bounce, folding settles.",
        "Going deeper slides the new face in from the right while the menu rows stagger; a tint marks the hovered row.",
        "Reduced motion jumps the size and cross fades the faces without travel or stagger."
    };

    public override string? ImplementationNotes =>
        "Platform approximations: the button does not position itself; the demo hosts it in a page surface at the " +
        "end corner, as the docs recommend. Drafts are kept per action while the page is alive, and a submit is a " +
        "short simulated save rather than a promise from a backend. The drawn rows share one focus stop, so the " +
        "control announces its name and keys rather than one automation peer per row.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var morph = new ArcActionMorph
        {
            Label = "Create",
            Face = ArcActionMorphFace.Menu,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var notes = new StackPanel
        {
            Margin = new Thickness(32, 28, 32, 28),
            Spacing = 12,
            Width = 400,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                new TextBlock { Text = "Today", Classes = { "arc-caption" } },
                new TextBlock { Text = "Launch checklist", Classes = { "arc-heading" } },
                TaskRow("Draft the release notes", done: true),
                TaskRow("Invite the beta cohort", done: true),
                TaskRow("Ship the motion pass", done: false),
                TaskRow("Write the changelog", done: false)
            }
        };

        var stage = SmPage.Stage(
            new Grid { Children = { notes, morph } },
            height: 340,
            background: ArcTokens.SurfaceMuted);

        var composer = SmPage.Toggle("Compose", () => morph.OpenComposer("task"));
        var success = SmPage.Toggle("Confirm", morph.ShowSuccess);
        var reset = SmPage.Toggle("Reset", () => morph.Face = ArcActionMorphFace.Button);

        // The interactive stage above rests on the menu face; the row below pins the other faces statically.
        var faces = new WrapPanel { Orientation = Orientation.Horizontal };
        faces.Children.Add(FaceStage(120, 110, new ArcActionMorph { Face = ArcActionMorphFace.Button }));
        faces.Children.Add(FaceStage(300, 220, new ArcActionMorph { Face = ArcActionMorphFace.Menu, CornerStart = true }));
        faces.Children.Add(FaceStage(380, 290, new ArcActionMorph { Face = ArcActionMorphFace.Composer }));

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                stage,
                faces,
                SmPage.Hint(
                    "Pick a quick action, type, and add it without leaving the page. Try the button, or T and N for a composer.",
                    composer,
                    success,
                    reset)
            }
        };
    }

    private static Control FaceStage(double width, double height, ArcActionMorph morph)
    {
        morph.HorizontalAlignment = HorizontalAlignment.Right;
        morph.VerticalAlignment = VerticalAlignment.Bottom;

        return new Border
        {
            Width = width,
            Height = height,
            Margin = new Thickness(0, 0, 12, 12),
            Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(20),
            Child = morph
        };
    }

    private static Control TaskRow(string text, bool done)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Height = 34, VerticalAlignment = VerticalAlignment.Center };
        var icon = new ArcIcon
        {
            Icon = done ? "circle-check" : "circle",
            Size = 16,
            Foreground = done ? DemoUi.Brush(ArcTokens.Success) : DemoUi.Brush(ArcTokens.BorderStrong),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        row.Children.Add(icon);
        var label = new TextBlock
        {
            Text = text,
            Classes = { "arc-body-sm" },
            Foreground = done ? DemoUi.Brush(ArcTokens.TextMuted) : DemoUi.Brush(ArcTokens.Foreground),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        return row;
    }
}
