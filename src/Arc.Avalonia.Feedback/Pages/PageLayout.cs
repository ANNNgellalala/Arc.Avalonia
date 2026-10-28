using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Small layout helpers for the feedback gallery pages: equal columns and fixed slots.</summary>
public static class PageLayout
{
    /// <summary>Equal-width columns with a shared 24 px gutter, matching the page rhythm.</summary>
    public static Control EqualRow(params Control[] children)
    {
        var grid = new Grid();
        for (var i = 0; i < children.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var child = children[i];
            child.Margin = new Thickness(0, 0, i == children.Length - 1 ? 0 : 24, 0);
            Grid.SetColumn(child, i);
            grid.Children.Add(child);
        }

        return grid;
    }

    public static Control Stack(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Spacing = spacing };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>A capped column so long-form demos keep a readable measure.</summary>
    public static Control Narrow(double width, params Control[] children)
    {
        var panel = new StackPanel { Width = width, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    public static TextBlock Caption(string text) => new()
    {
        Text = text,
        Classes = { "arc-caption" },
        TextWrapping = TextWrapping.Wrap
    };

    public static Control MutedCanvas(Control content) => new Border
    {
        Background = Arc.Avalonia.Demo.DemoUi.Brush(ArcTokens.SurfaceMuted),
        CornerRadius = new CornerRadius(26),
        Padding = new Thickness(24),
        Child = content
    };
}
