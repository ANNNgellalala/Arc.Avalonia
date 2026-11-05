using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Scrub input's completeness preview: both sizes, single-unit and cycling-unit variants, wrap, fixed
/// precision and the disabled state. Declared state keys match Docs/scrub-input.json.
/// </summary>
public sealed class ScrubInputPage : ArcItemPage
{
    public override string ItemSlug => "scrub-input";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "variant-unit", "variant-units",
        "variant-wrap", "state-precision", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Field(new ArcScrubInput
            {
                Label = "W",
                Value = 240,
                Min = 0,
                Max = 1000,
                Unit = "px",
                ResetValue = 240,
                Size = ArcScrubInputSize.Sm
            }),
            Field(new ArcScrubInput
            {
                Label = "H",
                Value = 160,
                Min = 0,
                Max = 1000,
                Unit = "px",
                Size = ArcScrubInputSize.Md
            })));

        panel.Children.Add(Row("单位与环绕",
            Field(new ArcScrubInput
            {
                Label = "Opacity",
                Value = 100,
                Min = 0,
                Max = 100,
                Unit = "%",
            }),
            Field(new ArcScrubInput
            {
                Label = "Gap",
                Value = 12,
                Min = 0,
                Max = 200,
                Unit = "em",
                Units = new List<string> { "px", "%", "em" },
            }),
            Field(new ArcScrubInput
            {
                Label = "R",
                Value = 0,
                Min = 0,
                Max = 360,
                Wrap = true,
                FineStep = 0.1,
                Unit = "°",
                Units = new List<string> { "°" },
                ResetValue = 0,
                Size = ArcScrubInputSize.Sm,
            })));

        panel.Children.Add(Row("精度与禁用",
            Field(new ArcScrubInput
            {
                Label = "Ratio",
                Value = 1.25,
                Min = 0,
                Max = 4,
                Step = 0.05,
                Precision = 2,
                Unit = "x",
            }),
            Field(new ArcScrubInput
            {
                Label = "Locked",
                Value = 32,
                Min = 0,
                Max = 100,
                Unit = "px",
                IsEnabled = false
            })));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var wrap = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 10, 14);
            wrap.Children.Add(child);
        }

        row.Children.Add(wrap);
        return row;
    }

    private static T Field<T>(T control, double width = 196)
        where T : Control
    {
        control.Width = width;
        return control;
    }
}
