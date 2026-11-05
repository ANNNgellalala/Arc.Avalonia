using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Elastic slider's completeness preview: the plain elastic track, labelled detents with end icons, the
/// formatted readout, the hidden readout and the disabled form. Declared keys match Docs/elastic-slider.json.
/// </summary>
public sealed class ElasticSliderPage : ArcItemPage
{
    public override string ItemSlug => "elastic-slider";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-default", "variant-marks", "variant-icons", "variant-without-value", "variant-format", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("基础",
            new ArcElasticSlider
            {
                Width = 320,
                Label = "Volume",
                Value = 62,
                Min = 0,
                Max = 100,
                Format = value => value.ToString("0") + "%"
            }));

        panel.Children.Add(Row("刻度吸附与两端图标",
            new ArcElasticSlider
            {
                Width = 320,
                Label = "Playback speed",
                Value = 1.5,
                Min = 0.5,
                Max = 2,
                Step = 0.25,
                StartIcon = "minus",
                EndIcon = "plus",
                Marks = new List<ArcSliderMark>
                {
                    new(1, "1x"),
                    new(1.5, "1.5x"),
                    new(2, "2x")
                },
                Format = value => value.ToString("0.##") + "x"
            }));

        panel.Children.Add(Row("隐藏读数与禁用",
            new ArcElasticSlider
            {
                Width = 320,
                Label = "Brightness",
                Value = 30,
                ShowValue = false,
                Marks = new List<ArcSliderMark> { new(0), new(50), new(100) }
            },
            new ArcElasticSlider
            {
                Width = 320,
                Label = "Disabled",
                Value = 45,
                IsEnabled = false,
                Marks = new List<ArcSliderMark> { new(0), new(50), new(100) }
            }));

        return panel;
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
