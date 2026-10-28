using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.AdvancedInputs.Pages;

/// <summary>Inline edit and the scrub input, the two controls that edit a value in place.</summary>
public sealed class AdvancedInputsPage : ArcPage
{
    public override string Title => "Advanced inputs";

    public override string Group => "Advanced inputs";

    public override string? Description =>
        "Editing that happens in place: rename a title, then scrub or type an inspector value.";

    public override int Order => 600;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            AdvancedInputsSections.InlineEdit(),
            AdvancedInputsSections.ScrubInput()
        }
    };
}
