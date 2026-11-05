using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Media.Controls;

namespace Arc.Avalonia.Media.Pages;

/// <summary>
/// Hold to confirm's completeness preview: both tones, a custom icon, the confirmed face and the disabled
/// state, each rendered as a still frame. Declared state keys match Docs/hold-to-confirm.json so the audit
/// can compare the two lists.
/// </summary>
public sealed class HoldToConfirmPage : ArcItemPage
{
    public override string ItemSlug => "hold-to-confirm";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-danger", "variant-neutral", "variant-icon", "state-confirmed", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("色调",
            Danger(),
            Neutral()));

        panel.Children.Add(Row("状态",
            Confirmed(),
            Disabled()));

        panel.Children.Add(Row("图标",
            CustomIcon()));

        return panel;
    }

    private static Control Danger() => new ArcHoldToConfirm
    {
        Label = "Hold to delete project",
        ConfirmedLabel = "Project deleted",
        Icon = "trash-2",
        Tone = ArcHoldToConfirmTone.Danger
    };

    private static Control Neutral() => new ArcHoldToConfirm
    {
        Label = "Hold to publish release",
        ConfirmedLabel = "Published",
        Icon = "upload",
        Tone = ArcHoldToConfirmTone.Neutral
    };

    /// <summary>The done face after a completed hold: success wash, check icon and the confirmed label.</summary>
    private static Control Confirmed() => new ArcHoldToConfirm
    {
        Label = "Hold to delete project",
        ConfirmedLabel = "Project deleted",
        Icon = "trash-2",
        IsConfirmed = true
    };

    private static Control Disabled() => new ArcHoldToConfirm
    {
        Label = "Hold to archive workspace",
        ConfirmedLabel = "Archived",
        Icon = "archive",
        Tone = ArcHoldToConfirmTone.Neutral,
        IsEnabled = false
    };

    /// <summary>A custom leading icon keeps its identity through the hold and becomes a check when done.</summary>
    private static Control CustomIcon() => new ArcHoldToConfirm
    {
        Label = "Hold to sign out everywhere",
        ConfirmedLabel = "Signed out",
        Icon = "log-out",
        Tone = ArcHoldToConfirmTone.Neutral
    };

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 12, 0);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
