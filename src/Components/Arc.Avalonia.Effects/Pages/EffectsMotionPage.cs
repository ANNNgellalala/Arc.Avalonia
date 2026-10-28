using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>The four text effects: reveal, in-view title, morph and shimmer.</summary>
public sealed class EffectsMotionPage : ArcPage
{
    public override string Title => "Text effects";

    public override string Group => "Effects";

    public override string? Description =>
        "Small entrances for copy that explain where it came from. Under reduced motion every one of " +
        "them shows its final state with no transform, blur or mask.";

    public override int Order => 801;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            EffectDemos.Section(
                "Text reveal",
                "Above-the-fold headlines that animate once on first paint, word by word or character by character.",
                RevealDemo()),
            EffectDemos.Section(
                "In-view title",
                "Section headings that bring themselves in when they scroll into view, in five reveal styles.",
                InViewDemo()),
            EffectDemos.Section(
                "Text morph",
                "A short status label that morphs into its next state instead of jumping.",
                MorphDemo()),
            EffectDemos.Section(
                "Text shimmer",
                "A calm light across a status line while work is ongoing; it settles to solid when done.",
                ShimmerDemo())
        }
    };

    private static Control RevealDemo()
    {
        var hero = new ArcTextReveal
        {
            Text = "Ship interfaces\nthat feel precise",
            Level = ArcTextLevel.H1,
            Delay = 0.1
        };

        var characters = new ArcTextReveal
        {
            Text = "Quietly confident",
            Level = ArcTextLevel.H3,
            Unit = ArcTextUnit.Character,
            Stagger = 18
        };

        var words = new ArcTextReveal
        {
            Text = "Restrained motion for copy that matters",
            Level = ArcTextLevel.Paragraph,
            Stagger = 28
        };

        var replay = new ArcButton
        {
            Content = "Replay",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        replay.Click += (_, _) =>
        {
            hero.Replay();
            characters.Replay();
            words.Replay();
        };

        replay.VerticalAlignment = VerticalAlignment.Top;

        var heroRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heroRow.Children.Add(hero);
        Grid.SetColumn(replay, 1);
        heroRow.Children.Add(replay);

        return new StackPanel
        {
            Spacing = 18,
            Children =
            {
                heroRow,
                DemoUi.Row(EffectDemos.Labelled("By character", characters), EffectDemos.Labelled("By paragraph", words))
            }
        };
    }

    private static Control InViewDemo()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 24,
            RowSpacing = 26
        };

        var blur = EffectDemos.Labelled(
            "Blur, the default",
            new ArcInViewTitle
            {
                Text = "Everything your team needs",
                Variant = ArcInViewVariant.Blur,
                Level = ArcTextLevel.H3
            });
        grid.Children.Add(blur);

        var line = EffectDemos.Labelled(
            "Line, explicit breaks",
            new ArcInViewTitle
            {
                Text = "Everything your team needs to ship",
                Variant = ArcInViewVariant.Line,
                Lines = new[] { "Everything your team", "needs to ship" },
                Level = ArcTextLevel.H3
            });
        Grid.SetColumn(line, 1);
        grid.Children.Add(line);

        var wipe = EffectDemos.Labelled(
            "Wipe, left to right",
            new ArcInViewTitle
            {
                Text = "One motion per section",
                Variant = ArcInViewVariant.Wipe,
                Level = ArcTextLevel.H3
            });
        Grid.SetRow(wipe, 1);
        grid.Children.Add(wipe);

        var tracking = EffectDemos.Labelled(
            "Tracking, letters settle",
            new ArcInViewTitle
            {
                Text = "Quiet confidence",
                Variant = ArcInViewVariant.Tracking,
                Level = ArcTextLevel.H3
            });
        Grid.SetRow(tracking, 1);
        Grid.SetColumn(tracking, 1);
        grid.Children.Add(tracking);

        return grid;
    }

    private static Control MorphDemo()
    {
        var label = new ArcTextMorph
        {
            Text = "Publish",
            Level = ArcTextLevel.H3
        };

        var idle = new ArcButton { Content = "Publish", Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm };
        var busy = new ArcButton { Content = "Publishing", Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm };
        var done = new ArcButton { Content = "Published", Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm };
        idle.Click += (_, _) => label.Text = "Publish";
        busy.Click += (_, _) => label.Text = "Publishing";
        done.Click += (_, _) => label.Text = "Published";

        var frame = new Border
        {
            CornerRadius = new CornerRadius(26),
            Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
            Padding = new Thickness(24, 16),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = label
        };

        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                frame,
                DemoUi.Row(idle, busy, done)
            }
        };
    }

    private static Control ShimmerDemo()
    {
        var generating = new ArcTextShimmer
        {
            Text = "Generating summary",
            Active = true,
            Level = ArcTextLevel.Paragraph
        };

        var ready = new ArcTextShimmer
        {
            Text = "Summary ready",
            Active = false,
            Level = ArcTextLevel.Paragraph
        };

        var finish = new ArcButton
        {
            Content = "Finish",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        finish.Click += (_, _) =>
        {
            generating.Active = false;
            generating.Text = "Summary ready";
        };

        var again = new ArcButton
        {
            Content = "Generate again",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm
        };
        again.Click += (_, _) =>
        {
            generating.Active = true;
            generating.Text = "Generating summary";
        };

        return new StackPanel
        {
            Spacing = 14,
            Children =
            {
                DemoUi.Row(EffectDemos.Labelled("Active, a light sweeps", generating), finish),
                DemoUi.Row(EffectDemos.Labelled("Settled, solid text", ready), again)
            }
        };
    }
}

