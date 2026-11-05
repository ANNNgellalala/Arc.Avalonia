using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Emoji picker's completeness preview: one panel with an active search and the skin-tone row, and one panel
/// showing the recent-emoji row. Both are pinned open because the picker draws its own inline surface.
/// Declared state keys match Docs/emoji-picker.json so the audit can compare the two.
/// </summary>
public sealed class EmojiPickerItemPage : ArcItemPage
{
    public override string ItemSlug => "emoji-picker";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "state-search", "state-recent", "state-skin-tones"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("搜索 + 肤色", "输入 face 即时过滤；ShowSkinTones 打开时显示六个肤色。", Picker("face", true)),
            Card("最近使用", "没有搜索时，顶部先显示最近用过的 emoji。", Picker(null, false))));

        return panel;
    }

    private static Control Picker(string? query, bool showTones) => new ArcEmojiPicker
    {
        Width = 360,
        IsOpen = true,
        Label = "Emoji",
        SearchText = query,
        ShowSkinTones = showTones,
        SkinTone = ArcSkinTone.MediumLight,
        DefaultRecent = new[] { "😂", "❤", "🔥", "👍", "🎉", "🙏", "✨", "🥳", "😍" }
    };

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
