using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>View switching, approximate values and scores.</summary>
public sealed class InputsPartFourPage : ArcPage
{
    public override string Title => "Inputs, part four";

    public override string Group => "Inputs";

    public override string? Description =>
        "Switch between views, pick an approximate value, and collect or display a score.";

    public override int Order => 103;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            InputsSections.SegmentedSliderRating()
        }
    };
}
