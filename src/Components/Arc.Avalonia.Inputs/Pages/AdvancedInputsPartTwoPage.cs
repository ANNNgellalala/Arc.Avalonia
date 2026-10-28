using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>Phone input and the shortcut recorder, the two fields that build a structured value.</summary>
public sealed class AdvancedInputsPartTwoPage : ArcPage
{
    public override string Title => "Advanced inputs, part two";

    public override string Group => "Advanced inputs";

    public override string? Description =>
        "Structured entry: a phone number with its country, and a key chord recorded into caps.";

    public override int Order => 601;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            AdvancedInputsSections.PhoneInput(),
            AdvancedInputsSections.ShortcutRecorder()
        }
    };
}
