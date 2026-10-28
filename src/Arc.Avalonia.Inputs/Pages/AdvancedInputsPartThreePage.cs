using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>Mentions and the expanding search: writing that reaches for other things.</summary>
public sealed class AdvancedInputsPartThreePage : ArcPage
{
    public override string Title => "Advanced inputs, part three";

    public override string Group => "Advanced inputs";

    public override string? Description =>
        "Composers that grow: mention tokens inside the text, and a search icon that becomes a field.";

    public override int Order => 602;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            AdvancedInputsSections.MentionInput(),
            AdvancedInputsSections.ExpandingSearch()
        }
    };
}
