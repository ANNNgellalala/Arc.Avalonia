using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Icons;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery.Pages;

/// <summary>The icon set: every Lucide name available to ArcIcon, rendered on the shared 24 unit grid.</summary>
public sealed class IconsPage : ArcPage
{
    public override string Title => "Icons";

    public override string Group => "Overview";

    public override string? Description =>
        "161 Lucide icons drawn on the 24 unit grid at stroke 1.75 with round caps. Use ArcIcon with the kebab-case name; the geometry scales to the requested size.";

    public override int Order => 2;

    public override Control Build(ArcDemoContext context)
    {
        var names = ArcIconData.Paths.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList();
        var columns = 12;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns)))
        };

        var rowCount = (int)Math.Ceiling(names.Count / (double)columns);
        for (var i = 0; i < rowCount; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var index = 0; index < names.Count; index++)
        {
            var cell = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(0, 0, 4, 12),
                Children =
                {
                    new Border
                    {
                        Width = 38,
                        Height = 38,
                        CornerRadius = new CornerRadius(12),
                        Background = DemoUi.Brush(ArcTokens.SurfaceMuted),
                        Child = new ArcIcon
                        {
                            Icon = names[index],
                            Size = 20,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    },
                    new TextBlock
                    {
                        Text = names[index],
                        Classes = { "arc-caption" },
                        FontSize = 10,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 90
                    }
                }
            };
            Grid.SetRow(cell, index / columns);
            Grid.SetColumn(cell, index % columns);
            grid.Children.Add(cell);
        }

        return DemoUi.Section($"{names.Count} icons", "Names are kebab-case and match ArcIconData.Paths.", grid);
    }
}

