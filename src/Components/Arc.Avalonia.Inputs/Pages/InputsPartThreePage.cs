using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>Multi-value selection, filter chips and the checkbox, radio and switch family.</summary>
public sealed class InputsPartThreePage : ArcPage
{
    public override string Title => "Inputs, part three";

    public override string Group => "Inputs";

    public override string? Description =>
        "Several values at once, filter chips, and the toggle family for forms and settings.";

    public override int Order => 102;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            InputsSections.MultiSelectChips(),
            InputsSections.Toggles()
        }
    };
}
