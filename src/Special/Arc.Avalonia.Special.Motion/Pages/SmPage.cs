using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Pages;

/// <summary>
/// Shared demo chrome for the Special motion pages: one clipped stage at the gallery width, a hint row with
/// optional demo toggles, and small token bound builders. The stage is the only container the drawn
/// components receive, so every page shows the same surface at the same height.
/// </summary>
internal static class SmPage
{
    /// <summary>A rounded stage that clips the drawn surface, on the given token background.</summary>
    public static Border Stage(Control content, double height = 400, string background = ArcTokens.Surface, double padding = 0)
    {
        var stage = new Border
        {
            Height = height,
            Padding = new Thickness(padding),
            Background = DemoUi.Brush(background),
            Child = content
        };
        stage.Classes.Add("sp-stage");
        return stage;
    }

    /// <summary>Centres content inside the stage without stretching it.</summary>
    public static Control Center(Control content, double padding = 24)
    {
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        return new Border { Padding = new Thickness(padding), Child = content };
    }

    /// <summary>The hint row under a stage: guidance on the left, demo toggles on the right.</summary>
    public static Control Hint(string text, params Control[] actions)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 0) };
        var caption = new TextBlock
        {
            Text = text,
            Classes = { "arc-caption", "sp-hint" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 620,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0)
        };
        row.Children.Add(caption);

        if (actions.Length > 0)
        {
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (var action in actions)
            {
                buttons.Children.Add(action);
            }

            Grid.SetColumn(buttons, 1);
            row.Children.Add(buttons);
        }

        return row;
    }

    /// <summary>A small ghost button used for the demo toggles beside the hint.</summary>
    public static ArcButton Toggle(string text, Action click, bool active = false)
    {
        var button = new ArcButton
        {
            Content = text,
            Variant = active ? ArcButtonVariant.Primary : ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Height = 32,
            MinHeight = 32,
            FontSize = 12,
            Padding = new Thickness(12, 0)
        };
        button.Click += (_, _) => click();
        return button;
    }

    public static TextBlock Caption(string text) => new() { Text = text, Classes = { "arc-caption" } };

    public static TextBlock Label(string text) => new() { Text = text, Classes = { "arc-label" } };

    /// <summary>A card on the stage surface, with the panel radius and a hairline border.</summary>
    public static Border Card(Control child, double padding = 22)
    {
        return new Border
        {
            Background = DemoUi.Brush(ArcTokens.Surface),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(padding),
            Child = child
        };
    }

    /// <summary>A quiet pill used to show state inside demo chrome.</summary>
    public static Border Pill(string text, string backgroundToken, string foregroundToken)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = DemoUi.Brush(foregroundToken)
        };
        return new Border
        {
            Background = DemoUi.Brush(backgroundToken),
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(9, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = label
        };
    }

    /// <summary>A vertical stack for the demo body.</summary>
    public static StackPanel Stack(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Spacing = spacing };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }
}
