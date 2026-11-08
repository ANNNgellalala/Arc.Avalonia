namespace Arc.Avalonia.Markdown.Pages;

/// <summary>
/// Markdown specimens shared by the Markdown documentation pages. Each constant isolates one part of the
/// node matrix so a failing block is obvious in the screenshot; the pages render them through the public
/// <see cref="Arc.Avalonia.Markdown.ArcMarkdownView"/> only.
/// </summary>
internal static class Samples
{
    internal const string Typography = """
        # Markdown 视图

        ## 二级标题与正文

        正文段落支持 **粗体**、*斜体*、~~删除线~~、行内代码 `ArcMarkdownView` 与 [链接](https://uiarc.dev)。
        软换行展开为空格。

        ### 三级标题

        ![发布架构图](https://example.com/architecture.png)

        ---

        #### 四级标题
        """;

    internal const string Lists = """
        ### 列表与引用

        - 无序项：解析 PuraMarkdown AST
        - 无序项：嵌套列表
          - 二级嵌套项

        1. 有序项：ParseDocument
        2. 有序项：节点渲染器

        - [x] 标题与段落
        - [ ] GFM 表格

        > 引用块用 3px 竖线标识层级。
        """;

    internal const string Table = """
        | 组件 | 关键 token | 状态 | 说明 |
        | :--- | :---: | ---: | :--- |
        | `ArcMarkdownView` | `ArcForeground` | 稳定 | 文档渲染宿主 |
        | `ArcMarkdownChatView` | `ArcSurfaceMuted` | 稳定 | 消息级虚拟化 |
        | `ArcMarkdownCodeBlock` | `ArcBorder` | 稳定 | 围栏代码与高亮 |
        | `pura-agent` 围栏 | `ArcAccent` | 插件 | choice / confirm |
        """;

    internal const string CSharp = """
        ```csharp
        var pipeline = new ArcMarkdownPipelineBuilder()
            .UseCommonMark()
            .UseGfm()
            .Build();

        var view = new ArcMarkdownView
        {
            Options = new ArcMarkdownOptions
            {
                Pipeline = pipeline,
            },
            Markdown = "# 发布说明",
        };
        ```
        """;

    internal const string Json = """
        ```json
        {
          "pipeline": "commonmark+gfm",
          "streaming": true,
          "agent": ["choice", "confirm"]
        }
        ```
        """;

    internal const string Bash = """
        ```bash
        dotnet build ArcUI.slnx
        ```
        """;

    internal const string AgentChoice = """
        ```pura-agent
        {
          "type": "choice",
          "version": "1.0",
          "id": "release-channel",
          "question": "发布到哪个通道？",
          "options": [
            { "id": "stable", "label": "stable", "description": "全量用户" },
            { "id": "beta", "label": "beta", "description": "提前一周" },
            { "id": "internal", "label": "internal", "disabled": true }
          ]
        }
        ```
        """;

    internal const string AgentConfirm = """
        ```pura-agent
        { "type": "confirm", "version": "1.0", "id": "publish-confirm", "question": "确认写入发布说明？", "confirmLabel": "确认发布" }
        ```
        """;
}
