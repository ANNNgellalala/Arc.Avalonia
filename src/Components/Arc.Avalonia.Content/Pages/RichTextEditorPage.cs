using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Content.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Content.Pages;

/// <summary>
/// Rich text editor's completeness preview: a Markdown document, an empty editor with its placeholder and
/// slash hint, an HTML-initialised document and the read-only form. Declared state keys match
/// Docs/rich-text-editor.json so the audit can compare the two.
/// </summary>
public sealed class RichTextEditorPage : ArcItemPage
{
    public override string ItemSlug => "rich-text-editor";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-markdown", "variant-empty", "variant-html", "state-readonly"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("Markdown 正文", "默认文档：标题、粗体、列表、引用与代码", Markdown()),
            Card("空编辑器", "只有 Placeholder 与斜杠提示", Empty())));

        panel.Children.Add(Row(
            Card("HTML 初始化", "用 DefaultHtml 传入初稿并渲染版式", Html()),
            Card("只读", "ReadOnly=true，工具栏与输入都不可用", ReadOnly())));

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

    private static Control Card(string title, string note, ArcRichTextEditor editor)
    {
        editor.Width = 380;
        editor.Height = 360;
        return DemoUi.Card(title, note, editor);
    }

    private static ArcRichTextEditor Markdown() => new()
    {
        Label = "Release notes editor",
        Placeholder = "Write your update",
        DefaultMarkdown =
            "# Release notes\n\n" +
            "Search is now **twice as fast**, with a calmer *dark* theme and a new " +
            "[command palette](https://uiarc.dev).\n\n" +
            "- Fuzzy file search\n" +
            "- New **dark** theme\n" +
            "- `ArcButton` focus fix\n\n" +
            "> Small touches add up."
    };

    private static ArcRichTextEditor Empty() => new()
    {
        Label = "Release notes editor",
        Placeholder = "Write your update",
        BlockHint = "Type / for blocks"
    };

    private static ArcRichTextEditor Html() => new()
    {
        Label = "Release notes editor",
        DefaultHtml =
            "<h1>Release notes</h1>" +
            "<p>Search is now <strong>twice as fast</strong>, with a calmer <em>dark</em> theme.</p>" +
            "<ul><li>Fuzzy file search</li><li>New dark theme</li></ul>"
    };

    private static ArcRichTextEditor ReadOnly() => new()
    {
        Label = "Release notes, read only",
        ReadOnly = true,
        DefaultMarkdown =
            "# Release notes\n\n" +
            "Search is now **twice as fast**.\n\n" +
            "- Fuzzy file search\n" +
            "- New **dark** theme\n"
    };
}
