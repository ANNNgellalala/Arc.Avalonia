using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Pickers, part five: the emoji picker. The panel is shown open and inline, the way a popover next to a
/// composer would hold it, with a trigger button that toggles it and a readout of the last pick.
/// </summary>
public sealed class EmojiPickerPage : ArcPage
{
    public override string Title => "Emoji picker";

    public override string Group => "Pickers";

    public override int Order => 505;

    public override string Description =>
        "A full emoji picker with instant search, scroll-synced categories, skin tones, recently used emoji " +
        "and a virtualised, keyboard-navigable grid.";

    public override Control Build(ArcDemoContext context)
    {
        var selectedGlyph = new TextBlock
        {
            Text = "😄",
            FontSize = 40,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var selectedName = new TextBlock
        {
            Text = "grinning face with smiling eyes",
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap
        };

        var picker = new ArcEmojiPicker
        {
            IsOpen = true,
            SearchText = "face",
            ShowSkinTones = true,
            SkinTone = ArcSkinTone.MediumLight,
            DefaultRecent = new[] { "😂", "❤", "🔥", "👍", "🎉", "🙏", "✨", "🥳", "😍" },
            Width = 380,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        picker.Selected += (_, selection) =>
        {
            selectedGlyph.Text = string.IsNullOrEmpty(selection.Emoji) ? "🙂" : selection.Emoji;
            selectedName.Text = selection.Name;
        };

        var trigger = new ArcButton
        {
            Variant = ArcButtonVariant.Secondary,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new ArcIcon { Icon = "smile", Size = 16 },
                    new TextBlock { Text = "Add emoji", VerticalAlignment = VerticalAlignment.Center }
                }
            }
        };

        var closed = new Border
        {
            IsVisible = false,
            Background = ArcResources.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = new TextBlock
            {
                Text = "Panel closed. The trigger sets IsOpen again.",
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap
            }
        };

        trigger.Click += (_, _) =>
        {
            picker.IsOpen = !picker.IsOpen;
            closed.IsVisible = !picker.IsOpen;
        };

        picker.Closed += (_, _) =>
        {
            closed.IsVisible = true;
        };

        var readout = new ArcCard
        {
            Width = 300,
            Padding = new Thickness(20),
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Last pick", Classes = { "arc-label" } },
                    new Border
                    {
                        Background = ArcResources.Brush(ArcTokens.SurfaceMuted),
                        CornerRadius = new CornerRadius(18),
                        Padding = new Thickness(16, 12),
                        Child = new StackPanel
                        {
                            Spacing = 4,
                            Children = { selectedGlyph, selectedName }
                        }
                    },
                    trigger,
                    closed,
                    new TextBlock
                    {
                        Text = "Picking raises Selected and leaves the panel open, so a composer can keep it up " +
                               "for several emoji. Escape clears the search first, then closes.",
                        Classes = { "arc-caption" },
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };

        var side = new StackPanel
        {
            Spacing = 12,
            Width = 300,
            Children =
            {
                readout,
                new TextBlock
                {
                    Text = "Search scans names and keywords. The category bar follows the scroll, arrow keys walk " +
                           "the grid, and Enter or a click inserts the glyph.",
                    Classes = { "arc-caption" },
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        var row = DemoUi.Row(picker, side);
        row.Margin = new Thickness(0, 0, 0, 0);

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Emoji picker",
                    "For composers, comment boxes and anywhere a person expects the full set with search. Recent " +
                    "emoji stay in memory for this session; the port has no per-page storage.",
                    row)
            }
        };
    }
}
