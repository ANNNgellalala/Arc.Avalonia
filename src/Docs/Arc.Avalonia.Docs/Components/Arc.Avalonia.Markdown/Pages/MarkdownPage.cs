using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Markdown.Pages;

/// <summary>
/// Markdown typography and prose: heading levels, paragraph with inline emphasis/strike/inline code/link,
/// image placeholder, thematic break, unordered/ordered/nested/task lists and a block quote. Kept to one
/// screenshot-height page (≤1400 px); the table/code half of the node matrix lives on
/// <see cref="MarkdownExtendedPage"/> and the chat surfaces on <see cref="MarkdownChatPage"/>.
/// </summary>
public sealed class MarkdownPage : ArcPage
{
    public override string Title => "Markdown";

    public override string Group => "Markdown";

    public override string? Description =>
        "ArcMarkdownView 把 PuraMarkdown 解析出的 Markdig AST 渲染成 Arc 控件。本页覆盖排版与行内样式、" +
        "列表/任务列表/引用与图片占位；颜色、字号和间距全部来自 Arc token，浅色与深色自适应。" +
        "行内链接可点击：展开「复制链接 / 在浏览器中打开」上下文菜单，#锚点链接在当前文档内滚动。";

    public override int Order => 1100;

    public override Control Build(ArcDemoContext context)
    {
        // ArcMarkdownStyle.Current is a token snapshot: rebuild it on every page build so a page opened after
        // a light/dark swap renders with the active palette (pages are rebuilt on theme change).
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "排版与行内样式",
                    "标题 28/22/18/16、正文 14；粗体 500 字重，删除线、行内代码与链接走语义 token。图片默认" +
                    "离线拒绝并显示安全占位，宿主注入 ImageLoader 后才加载。点击左侧示例里的 uiarc.dev 链接会展开" +
                    "「复制链接 / 在浏览器中打开」菜单；宿主可订阅 LinkClicked 并置 HandledByHost 接管。",
                    DemoUi.Row(View(Samples.Typography, 500), View(Samples.Lists, 500)))
            }
        };
    }

    private static ArcMarkdownView View(string markdown, double width) => new()
    {
        Markdown = markdown,
        Width = width,
        HorizontalAlignment = HorizontalAlignment.Left
    };
}
