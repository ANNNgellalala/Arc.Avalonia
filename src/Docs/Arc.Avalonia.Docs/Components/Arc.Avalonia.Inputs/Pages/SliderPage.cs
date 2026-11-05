using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Slider's completeness preview: the plain track, marks, the formatted readout, the description line and the
/// form without a readout. Declared state keys match Docs/slider.json.
/// </summary>
public sealed class SliderPage : ArcItemPage
{
    private static readonly ArcSliderMark[] Marks =
    {
        new(0), new(25), new(50), new(75), new(100)
    };

    public override string ItemSlug => "slider";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-default", "state-with-marks", "variant-format", "variant-description", "variant-without-value"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("基础",
            Field(new ArcSlider
            {
                Label = "Volume",
                Value = 70,
                Min = 0,
                Max = 100
            })));

        panel.Children.Add(Row("刻度与步进",
            Field(new ArcSlider
            {
                Label = "Opacity",
                Value = 45,
                Min = 0,
                Max = 100,
                Step = 5,
                Marks = Marks,
                Description = "方向键每次 5，刻度只作参考、不吸附。"
            })));

        panel.Children.Add(Row("格式化读数",
            Field(new ArcSlider
            {
                Label = "Monthly budget",
                Value = 40,
                Min = 0,
                Max = 100,
                Step = 5,
                Format = value => "$" + value.ToString("0"),
                Marks = new[] { new ArcSliderMark(0), new ArcSliderMark(50), new ArcSliderMark(100) }
            })));

        panel.Children.Add(Row("隐藏读数",
            Field(new ArcSlider
            {
                Label = "Zoom",
                Value = 80,
                Min = 50,
                Max = 200,
                ShowValue = false,
                Description = "只留拖动气泡，轨道本身不显示读数。"
            })));

        return panel;
    }

    private static ArcSlider Field(ArcSlider slider)
    {
        slider.Width = 320;
        return slider;
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
