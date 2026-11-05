using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Color picker's completeness preview: the closed field in hex and RGB formats, a semi-transparent value, and
/// the open surface shown inline with the same ArcColorEditor the popup hosts. Declared state keys match
/// Docs/color-picker.json so the audit can compare the two.
/// </summary>
public sealed class ColorPickerPage : ArcItemPage
{
    public override string ItemSlug => "color-picker";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-filled", "state-format-rgb", "state-alpha", "state-open"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var brand = Color.FromRgb(0x5B, 0x6B, 0xFF);
        var tint = Color.FromArgb(0x80, 0x5B, 0x6B, 0xFF);

        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("已选颜色", "色块与十六进制值并排显示。", new ArcColorPicker
            {
                Width = 280,
                Label = "Brand color",
                Value = brand,
                DefaultFormat = ArcColorFormat.Hex
            }),
            Card("半透明", "带 alpha 的色块与八位十六进制。", new ArcColorPicker
            {
                Width = 280,
                Label = "Overlay",
                Value = tint,
                DefaultFormat = ArcColorFormat.Hex
            })));

        panel.Children.Add(Row(
            Card("打开的面板", "取色方块、轨道、数值输入与收藏色板；DefaultFormat=Rgb 时数值框先显示 rgb()。", OpenSurface(brand))));

        return panel;
    }

    private static Control OpenSurface(Color brand)
    {
        var editor = new ArcColorEditor
        {
            Value = brand,
            Format = ArcColorFormat.Rgb,
            Swatches = new[]
            {
                new ArcColorSwatch("brand", brand),
                new ArcColorSwatch("ink", Color.FromRgb(0x11, 0x13, 0x18)),
                new ArcColorSwatch("accent", Color.FromRgb(0x3B, 0x82, 0xF6)),
                new ArcColorSwatch("success", Color.FromRgb(0x16, 0xA3, 0x4A)),
                new ArcColorSwatch("warming", Color.FromRgb(0xF5, 0x9E, 0x0B)),
                new ArcColorSwatch("danger", Color.FromRgb(0xDC, 0x26, 0x26))
            }
        };

        return PickerDemo.Surface(editor);
    }

    private static Control Row(params Control[] cards)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var card in cards)
        {
            card.Margin = new Thickness(0, 0, 16, 16);
            row.Children.Add(card);
        }

        return row;
    }

    private static Control Card(string title, string note, Control content) => DemoUi.Card(title, note, content);
}
