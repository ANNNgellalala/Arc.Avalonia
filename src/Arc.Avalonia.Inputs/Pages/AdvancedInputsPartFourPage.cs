using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>The morphing select and the two tactile sliders.</summary>
public sealed class AdvancedInputsPartFourPage : ArcPage
{
    public override string Title => "Advanced inputs, part four";

    public override string Group => "Advanced inputs";

    public override string? Description =>
        "Pickers and sliders with a physical feel: a trigger that grows into its list, rubber-band travel and a live histogram.";

    public override int Order => 603;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            AdvancedInputsSections.MorphSelect(),
            AdvancedInputsSections.ElasticSlider(),
            AdvancedInputsSections.RangeHistogramSlider()
        }
    };
}
