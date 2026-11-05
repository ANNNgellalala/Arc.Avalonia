using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Range histogram slider's completeness preview: a selected price range, the full default range, a collapsed
/// range, a coarser histogram and the custom currency format. Declared keys match
/// Docs/range-histogram-slider.json.
/// </summary>
public sealed class RangeHistogramSliderPage : ArcItemPage
{
    private static readonly List<double> Prices = BuildPrices();

    public override string ItemSlug => "range-histogram-slider";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-range", "state-full-range", "state-collapsed", "variant-bins", "variant-format"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("价格区间",
            new ArcRangeHistogramSlider
            {
                Width = 400,
                Label = "Price per night",
                Data = Prices,
                Min = 0,
                Max = 500,
                Step = 5,
                Bins = 28,
                Value = new ArcRange(80, 320),
                Format = value => "$" + value.ToString("0")
            }));

        panel.Children.Add(Row("整段范围与收窄区间",
            new ArcRangeHistogramSlider
            {
                Width = 400,
                Label = "Full range（未设 Value）",
                Data = Prices,
                Min = 0,
                Max = 500,
                Step = 5,
                Bins = 20
            },
            new ArcRangeHistogramSlider
            {
                Width = 400,
                Label = "Collapsed",
                Data = Prices,
                Min = 0,
                Max = 500,
                Step = 5,
                Bins = 20,
                Value = new ArcRange(240, 240)
            }));

        panel.Children.Add(Row("更粗的柱与自定义格式",
            new ArcRangeHistogramSlider
            {
                Width = 400,
                Label = "Distance",
                Data = Prices,
                Min = 0,
                Max = 500,
                Step = 5,
                Bins = 12,
                SnapToBins = false,
                Value = new ArcRange(40, 220),
                ThumbLabels = new List<string> { "Nearest", "Furthest" },
                Format = value => value.ToString("0") + " km"
            }));

        return panel;
    }

    private static List<double> BuildPrices()
    {
        var random = new Random(7);
        var values = new List<double>();
        for (var i = 0; i < 420; i++)
        {
            var value = 60 + random.NextDouble() * 340;
            if (random.NextDouble() < 0.35)
            {
                value = 120 + random.NextDouble() * 90;
            }

            values.Add(Math.Round(value));
        }

        return values;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 24, 12);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
