using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Signature pad's completeness preview: a signed pad, the same ink in blue and black and the empty pad with
/// its hint. Declared state keys match Docs/signature-pad.json so the audit can compare the two. The ink is
/// generated from a fixed curve, never at random.
/// </summary>
public sealed class SignaturePadPage : ArcItemPage
{
    public override string ItemSlug => "signature-pad";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-signed", "variant-blue-ink", "variant-black-ink", "state-empty"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("已签（紫色）", "紫色墨迹 + 签名人姓名，Medium 笔宽", Signed(ArcInkColor.Violet)),
            Card("蓝色墨迹", "InkColor=Blue，同一份笔画换色", Signed(ArcInkColor.Blue))));

        panel.Children.Add(Row(
            Card("黑色墨迹", "InkColor=Black，最接近纸质签字", Signed(ArcInkColor.Black)),
            Card("待签", "没有笔画时显示基线、Hint 与工具栏", Empty())));

        return panel;
    }

    private static Control Row(params Control[] cards)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var card in cards)
        {
            card.Margin = new Thickness(0, 0, 16, 0);
            row.Children.Add(card);
        }

        return row;
    }

    private static Control Card(string title, string note, ArcSignaturePad pad)
    {
        pad.Width = 340;
        return DemoUi.Card(title, note, pad);
    }

    private static ArcSignaturePad Signed(ArcInkColor color)
    {
        var pad = new ArcSignaturePad
        {
            Signer = "Ada Lovelace",
            InkColor = color,
            InkWidth = ArcInkWidth.Medium,
            FileName = "ada-signature"
        };
        pad.LoadStrokes(SampleInk(color));
        return pad;
    }

    private static ArcSignaturePad Empty() => new()
    {
        Signer = "Ada Lovelace",
        Hint = "Sign here",
        FileName = "ada-signature"
    };

    /// <summary>A deterministic signature-like curve in the pad's 600 by 260 coordinate space.</summary>
    private static IReadOnlyList<ArcInkStroke> SampleInk(ArcInkColor color)
    {
        var first = new List<ArcInkPoint>();
        var second = new List<ArcInkPoint>();
        for (var index = 0; index <= 48; index++)
        {
            var t = index / 48.0;
            first.Add(new ArcInkPoint(
                80 + t * 200,
                150 - Math.Sin(t * Math.PI * 2.2) * 38 - t * 12,
                (float)(2.1 + 0.9 * Math.Sin(t * Math.PI))));
            second.Add(new ArcInkPoint(
                300 + t * 200,
                148 + Math.Sin(t * Math.PI * 1.6) * 30 - t * 8,
                2.2));
        }

        return new[]
        {
            new ArcInkStroke(color, first),
            new ArcInkStroke(color, second),
            new ArcInkStroke(color, new List<ArcInkPoint>
            {
                new(90, 208, 1.9),
                new(240, 210, 2.1),
                new(390, 208, 2.1),
                new(520, 200, 1.9)
            })
        };
    }
}
