using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// Bottom sheet's completeness preview: one sheet resting at the peek detent and one at the full detent, each
/// hosted in a fixed stage so the open state and the scrim are static. Declared state keys match
/// Docs/bottom-sheet.json so the audit can compare the two.
/// </summary>
public sealed class BottomSheetPage : ArcItemPage
{
    public override string ItemSlug => "bottom-sheet";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "detent-peek", "detent-full", "layout-header"
    };

    public override bool DemoOnCanvas => false;

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Row(
            Captioned(Sheet(0, 1), "Peek：InitialDetent=0，停在较低的 68% 一档，背后页面仍可见。"),
            Captioned(Sheet(1, 3), "Full：InitialDetent=1，停在最高的 94% 一档。")));
        return panel;
    }

    private static Control Sheet(int initialDetent, int detailRows)
    {
        var stage = EffectDemos.Stage(410, 330, mockPage: true);

        var body = new StackPanel { Spacing = 8 };
        var rows = new[]
        {
            ("Flight", "TP 1042 · 09:10"),
            ("Hotel", "Alfama Loft · 3 nights"),
            ("Guests", "2 adults")
        };
        for (var i = 0; i < detailRows; i++)
        {
            body.Children.Add(EffectDemos.DetailRow(rows[i].Item1, rows[i].Item2));
        }

        var sheet = new ArcBottomSheet
        {
            Title = "Lisbon, 3 nights",
            Description = "Oct 12 to Oct 15",
            IsOpen = true,
            Detents = new[] { 0.68, 0.94 },
            InitialDetent = initialDetent,
            Content = body
        };

        stage.Children.Add(sheet);
        return stage;
    }

    private static Control Row(params Control[] columns)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var column in columns)
        {
            column.Margin = new Thickness(0, 0, 16, 16);
            row.Children.Add(column);
        }

        return row;
    }

    private static Control Captioned(Control stage, string caption)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(stage);
        panel.Children.Add(DemoUi.Caption(caption));
        return panel;
    }
}
