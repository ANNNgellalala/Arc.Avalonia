using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Markdown.Pages;

/// <summary>
/// The rich-block half of the Markdown node matrix: a GFM table with per-column alignment and fenced code in
/// several languages (language label, highlighting, copy). The closing notes state the streaming/outline and
/// fallback contract. Split from <see cref="MarkdownPage"/> so both pages stay under the 1400 px shot cap.
/// </summary>
public sealed class MarkdownExtendedPage : ArcPage
{
    public override string Title => "Markdown Extended";

    public override string Group => "Markdown";

    public override string? Description =>
        "GFM 表格（hairline、muted 表头、:--- / :---: / ---: 列对齐）与多语言围栏代码（语言标签、高亮、复制）。" +
        "未知语言回退纯文本，未注册插件的自定义围栏回退代码块。";

    public override int Order => 1101;

    public override Control Build(ArcDemoContext context)
    {
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "GFM 表格与多语言代码块",
                    "表格按 Markdown 声明对齐；围栏代码按语言高亮并带语言标签，超长代码折叠、复制带反馈，" +
                    "高亮失败回退纯文本。",
                    new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            View(Samples.Table, 960),
                            DemoUi.Row(View(Samples.CSharp, 440), View(Samples.Json, 320), View(Samples.Bash, 236)),
                            Note("流式：稳定边界之前只解析一次，尾部窗口每次 flush 重渲染；CompleteAsync 全量解析，" +
                                 "结果与一次性渲染一致。"),
                            Note("大纲与降级：Outline 返回标题层级、锚点与折叠范围；渲染器失败降级为 warning 文本，" +
                                 "流式完成通过 Announced 事件播报。")
                        }
                    })
            }
        };
    }

    private static ArcMarkdownView View(string markdown, double width) => new()
    {
        Markdown = markdown,
        Width = width,
        HorizontalAlignment = HorizontalAlignment.Left
    };

    private static TextBlock Note(string text) => new()
    {
        Text = "· " + text,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 960,
        Classes = { "arc-body-sm", "arc-secondary" }
    };
}
