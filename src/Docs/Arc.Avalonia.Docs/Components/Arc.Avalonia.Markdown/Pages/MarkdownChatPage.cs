using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Agent;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Markdown.Pages;

/// <summary>
/// Chat and streaming showcase for the Markdown family: <see cref="ArcMarkdownChatView"/> (message list with
/// per-message Markdown, pura-agent choice/confirm fences, status rows and auto-scroll policy) and the
/// append-only streaming session driven by the page buttons. Split from the node-matrix page so both stay
/// inside the 1400 px screenshot cap.
/// </summary>
public sealed class MarkdownChatPage : ArcPage
{
    public override string Title => "Markdown Chat";

    public override string Group => "Markdown";

    public override string? Description =>
        "AI 回答的聊天面：每条消息用完整 Markdown 渲染，pura-agent choice/confirm 围栏由 Agent 插件渲染成" +
        "交互卡片；消息级窗口化、流式状态槽（Streaming / Complete / Failed / Aborted）与自动滚动策略。";

    public override int Order => 1102;

    public override Control Build(ArcDemoContext context)
    {
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();

        var chat = BuildChatSection();
        var streaming = BuildStreamingSection();

        return new StackPanel
        {
            Children = { chat, streaming }
        };
    }

    private static Control BuildChatSection()
    {
        var chat = new ArcMarkdownChatView
        {
            Width = 960,
            Height = 470,
            HorizontalAlignment = HorizontalAlignment.Left,
            // The agent plugin registers the pura-agent fence renderer; without it the same fences fall back to
            // code blocks (the documented degradation path).
            Options = ArcAgentMarkdown.DefaultOptions,
            // The page pins the list to the top so the screenshot always starts at the first message; the
            // default NearBottom policy pauses following as soon as the reader scrolls away from the bottom.
            AutoScrollMode = ArcMarkdownChatAutoScrollMode.Never
        };

        chat.AddMessage(new ArcMarkdownChatMessage
        {
            Role = ArcMarkdownChatRole.User,
            Markdown = "给发布流程加上 choice 与 confirm 两个 pura-agent 交互组件。"
        });

        chat.AddMessage(new ArcMarkdownChatMessage
        {
            Role = ArcMarkdownChatRole.Assistant,
            Markdown = "pura-agent 围栏由 Arc.Avalonia.Agent 的插件接管，渲染成可交互的 Arc 卡片：\n\n" +
                       Samples.AgentChoice + "\n\n" + Samples.AgentConfirm
        });

        chat.AddMessage(new ArcMarkdownChatMessage
        {
            Role = ArcMarkdownChatRole.Assistant,
            Markdown =
                "交互事件通过 `InteractionOccurred` 上报，状态可由 `InteractionStates` 恢复：\n\n" +
                "```csharp\nview.InteractionOccurred += (_, e) =>\n    Debug.WriteLine($\"{e.Interaction.ComponentType}:{e.Interaction.SelectedIds.Count}\");\n```\n\n" +
                "| 状态 | 含义 |\n| :--- | :--- |\n| Streaming | 增量提交中 |\n| Complete | 全量解析完成 |\n| Aborted | 已中断，保留部分内容 |"
        });

        chat.AddMessage(new ArcMarkdownChatMessage
        {
            Role = ArcMarkdownChatRole.Assistant,
            Status = ArcMarkdownChatMessageStatus.Failed,
            Error = "流式连接中断：已保留收到的内容；宿主可以重试或展示错误行。",
            Markdown = "连接中断前收到的最后一段正文仍然可读。"
        });

        var status = DemoUi.Caption("消息数 4 · 状态行与错误行由 Chat 视图绘制。");
        void Refresh(string action)
        {
            status.Text = $"{action} · 消息数 {chat.Messages.Count}";
        }

        var append = new ArcButton
        {
            Content = "追加助手消息",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        append.Click += (_, _) =>
        {
            chat.AddMessage(new ArcMarkdownChatMessage
            {
                Role = ArcMarkdownChatRole.Assistant,
                Markdown = "新的回复追加到列表末尾，`AutoScrollMode` 决定是否跟随到底部。"
            });
            Refresh("MessageAdded");
        };

        var insert = new ArcButton
        {
            Content = "插入系统消息",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm
        };
        insert.Click += (_, _) =>
        {
            chat.InsertMessage(0, new ArcMarkdownChatMessage
            {
                Role = ArcMarkdownChatRole.System,
                Markdown = "系统：本次会话使用 neutral accent。"
            });
            Refresh("InsertMessage(0)");
        };

        var clear = new ArcButton
        {
            Content = "清空",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm
        };
        clear.Click += (_, _) =>
        {
            chat.ClearMessages();
            Refresh("ClearMessages");
        };

        var controls = DemoUi.Row(append, insert, clear, status);
        var content = new StackPanel { Spacing = 10, Children = { chat, controls } };

        return DemoUi.Section(
            "Chat 消息列表",
            "ArcMarkdownChatView 用 ArcMarkdownView 渲染每条消息，并把角色、状态与错误行包在消息外壳里；" +
            "1000 条消息按视口窗口化，宿主只负责增删改消息集合。",
            content);
    }

    private static Control BuildStreamingSection()
    {
        var view = new ArcMarkdownView
        {
            Width = 560,
            HorizontalAlignment = HorizontalAlignment.Left,
            Markdown =
                "## 流式输出\n\n" +
                "已经提交的块会立即出现；未闭合的围栏与表格留在尾部窗口，完成时再用一次全量解析替换文档，" +
                "因此最终结果与一次性渲染一致。"
        };

        var status = DemoUi.Caption("会话未开始 · 点击按钮演示 Append / Complete / Abort。");
        ArcMarkdownStreamingSession? session = null;

        ArcMarkdownStreamingSession Ensure()
        {
            session ??= view.BeginStreaming(new ArcMarkdownStreamingOptions { Synchronous = true });
            return session;
        }

        var append = new ArcButton { Content = "Append 一段", Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm };
        append.Click += (_, _) =>
        {
            var active = Ensure();
            active.Append("\n\n- 新增列表项：`Append(delta)` 可跨线程调用。\n- 稳定边界之前的内容只解析一次。");
            var stats = active.Statistics;
            status.Text = $"Streaming · {stats.TotalChars} 字符 · 提交 {stats.CommittedChars} · 尾部 {stats.TailChars}";
        };

        var complete = new ArcButton { Content = "Complete", Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm };
        complete.Click += async (_, _) =>
        {
            if (session is null)
            {
                return;
            }

            await session.CompleteAsync();
            status.Text = "Completed · 全量解析替换文档，大纲与一次性渲染一致。";
        };

        var abort = new ArcButton { Content = "Abort（保留部分）", Variant = ArcButtonVariant.Danger, Size = ArcButtonSize.Sm };
        abort.Click += async (_, _) =>
        {
            if (session is null)
            {
                return;
            }

            await session.AbortAsync();
            status.Text = "Aborted · 已收到的部分内容保留并重新渲染。";
        };

        var side = new StackPanel
        {
            Spacing = 8,
            Width = 380,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                DemoUi.Label("流式会话"),
                DemoUi.Body("ArcMarkdownStreamingSession 只提交稳定块，尾部窗口每次 flush 重渲染；" +
                            "CompleteAsync 幂等，AbortAsync 保留部分文本。"),
                DemoUi.Row(append, complete, abort),
                status,
                DemoUi.Body("骨架屏、错误行与自动滚动由 Chat 视图负责；单条超长文档可以按块虚拟化。")
            }
        };

        return DemoUi.Section(
            "流式渲染",
            "流式不是每 token 全量重排：稳定窗口解析一次、尾部窗口重渲染，完成时再做一次全量解析。",
            DemoUi.Row(view, side));
    }
}
