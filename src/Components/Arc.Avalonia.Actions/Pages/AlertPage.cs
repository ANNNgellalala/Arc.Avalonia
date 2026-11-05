using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Alert's completeness preview: the four tones, an action, the default dismissible form and one alert that
/// cannot be dismissed. Declared keys match Docs/alert.json.
/// </summary>
public sealed class AlertPage : ArcItemPage
{
    public override string ItemSlug => "alert";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "tone-info", "tone-success", "tone-warning", "tone-danger", "variant-action", "state-dismissible"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left, Width = 720 };

        panel.Children.Add(new TextBlock { Text = "色调、行动与关闭", Classes = { "arc-caption" } });

        panel.Children.Add(new ArcAlert
        {
            Title = "A new version is available",
            Tone = ArcAlertTone.Info,
            IsDismissible = false,
            Content = ActionDemos.Caption("Reload to get the latest fixes. Nothing is lost.")
        });

        panel.Children.Add(new ArcAlert
        {
            Title = "Changes saved",
            Tone = ArcAlertTone.Success,
            IsDismissible = false,
            Content = ActionDemos.Caption("The billing email now goes to finance@acme.com.")
        });

        panel.Children.Add(new ArcAlert
        {
            Title = "Your card expires next month",
            Tone = ArcAlertTone.Warning,
            ActionLabel = "Update card",
            Content = ActionDemos.Caption("Invoices after 12 April will fail until a new payment method is added.")
        });

        panel.Children.Add(new ArcAlert
        {
            Title = "Upload failed",
            Tone = ArcAlertTone.Danger,
            ActionLabel = "Try again",
            Content = ActionDemos.Caption("The file is larger than 25 MB. Compress it and upload again.")
        });

        return panel;
    }
}
