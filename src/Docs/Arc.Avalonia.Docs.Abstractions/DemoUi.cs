using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Demo;

/// <summary>
/// Layout helpers that keep every demo page on the same grid: 24px section rhythm, a 720px copy column,
/// 16px inside groups and 12px gaps inside demo rows.
/// </summary>
public static class DemoUi
{
    /// <summary>
    /// Resolves a token from the current application palette. Delegates to
    /// <see cref="ArcResources.TryFind(string, out object?)"/> because the tokens live in
    /// <c>ThemeDictionaries</c>: an untargeted <c>Application.FindResource</c> returns
    /// <c>UnsetValue</c> for every token, which silently turned page and window backgrounds into
    /// <see cref="Brushes.Transparent"/> (transparent Gallery window).
    /// </summary>
    public static object? Resource(string key) => ArcResources.TryFind(key, out var value) ? value : null;

    /// <summary>Resolves a colour token, falling back to transparent only for genuinely missing keys.</summary>
    public static IBrush Brush(string key) => Resource(key) as IBrush ?? Brushes.Transparent;

    /// <summary>A page section: heading, optional description, content, then a 48px break.</summary>
    public static Control Section(string title, string? description, Control content, double gap = 16)
    {
        var panel = new StackPanel { Spacing = gap };
        panel.Children.Add(new TextBlock { Text = title, Classes = { "arc-heading" } });
        if (!string.IsNullOrWhiteSpace(description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = description,
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 720
            });
        }

        panel.Children.Add(content);
        return new Border { Child = panel, Margin = new Thickness(0, 0, 0, 48) };
    }

    /// <summary>A demo row that wraps on narrow widths.</summary>
    public static Control Row(params Control[] children)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 12, 12);
            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>A vertical stack with the standard 12px gap.</summary>
    public static Control Column(params Control[] children)
    {
        var panel = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    /// <summary>A fixed-width column for label/value pairs.</summary>
    public static Control FixedColumn(double width, params Control[] children)
    {
        var panel = new StackPanel { Width = width, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    public static TextBlock Caption(string text) => new() { Text = text, Classes = { "arc-caption" } };

    public static TextBlock Label(string text) => new() { Text = text, Classes = { "arc-label" } };

    public static TextBlock Body(string text, double maxWidth = 720) => new()
    {
        Text = text,
        Classes = { "arc-body-sm", "arc-secondary" },
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = maxWidth
    };

    /// <summary>Puts demo content on a muted canvas so surfaces and borders read against it.</summary>
    public static Control Canvas(Control content, double padding = 24) => new Border
    {
        Background = Brush(ArcTokens.SurfaceMuted),
        CornerRadius = new CornerRadius(26),
        Padding = new Thickness(padding),
        Child = content
    };

    /// <summary>A card with a title and a one-line note, used for dense showcase pages.</summary>
    public static Control Card(string title, string? description, Control content)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = title, Classes = { "arc-label" } });
        if (!string.IsNullOrWhiteSpace(description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = description,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap
            });
        }

        panel.Children.Add(new Border { Child = content, Margin = new Thickness(0, 12, 0, 0) });
        return new ArcCard { Content = panel, Padding = new Thickness(20) };
    }

    /// <summary>A label plus a control, the standard field layout.</summary>
    public static Control Field(string label, Control control, string? hint = null)
    {
        var panel = new StackPanel { Spacing = 6, Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = label, Classes = { "arc-label" } });
        panel.Children.Add(control);
        if (!string.IsNullOrWhiteSpace(hint))
        {
            panel.Children.Add(new TextBlock
            {
                Text = hint,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap
            });
        }

        return panel;
    }
}


