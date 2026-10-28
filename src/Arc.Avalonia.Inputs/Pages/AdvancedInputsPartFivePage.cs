using Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>Pricing and plan choice: the billing switch with rolling prices, and radio cards.</summary>
public sealed class AdvancedInputsPartFivePage : ArcPage
{
    public override string Title => "Advanced inputs, part five";

    public override string Group => "Advanced inputs";

    public override string? Description =>
        "Choosing a price: a period switch whose prices roll, and option cards with a sliding ring.";

    public override int Order => 604;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            AdvancedInputsSections.BillingToggle(),
            AdvancedInputsSections.RadioCards()
        }
    };
}
