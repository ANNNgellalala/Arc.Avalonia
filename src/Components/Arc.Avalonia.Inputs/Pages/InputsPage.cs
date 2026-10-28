using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>Text entry: the single-line and multi-line families, plus the password fields.</summary>
public sealed class InputsPage : ArcPage
{
    public override string Title => "Inputs";

    public override string Group => "Inputs";

    public override string? Description =>
        "Single-line and multi-line text entry, with helper copy, errors and password reveal.";

    public override int Order => 100;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            InputsSections.Input(),
            InputsSections.TextareaPassword()
        }
    };
}
