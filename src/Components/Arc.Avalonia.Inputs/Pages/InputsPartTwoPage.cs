using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>Search, bounded numbers, money and the single-value selection fields.</summary>
public sealed class InputsPartTwoPage : ArcPage
{
    public override string Title => "Inputs, part two";

    public override string Group => "Inputs";

    public override string? Description =>
        "Search, bounded numbers, money, free-form tags and the selection fields.";

    public override int Order => 101;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            InputsSections.SearchNumbersMoney(),
            InputsSections.TagsSelectCombobox()
        }
    };
}
