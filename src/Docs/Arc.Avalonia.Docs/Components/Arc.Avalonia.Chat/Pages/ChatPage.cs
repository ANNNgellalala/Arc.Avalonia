using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Chat;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Chat.Pages;

/// <summary>
/// Chat family showcase: <see cref="ArcChatView"/> bubbles (grouping, avatars, timestamps, attachments,
/// delivery/status lines, system messages, typing row, built-in composer), the standalone
/// <see cref="ArcChatComposer"/>, and the streaming slot with Append/Complete/Abort. Every bubble body is
/// rendered by <c>ArcMarkdownView</c>, so the same surface serves human chat and AI answers.
/// </summary>
public sealed class ChatPage : ArcPage
{
    public override string Title => "Chat";

    public override string Group => "Chat";

    public override string? Description =>
        "气泡会话：用户靠右、助手靠左，同作者连续消息合并成组；正文用 ArcMarkdownView 渲染，支持附件、" +
        "送达与生成状态、系统消息、正在输入、内置输入框与流式槽。1000 条消息只实例化视口内的气泡。";

    public override int Order => 1100;

    public override Control Build(ArcDemoContext context)
    {
        // ArcMarkdownStyle.Current is a token snapshot used by the bubble/composer chrome; rebuild it on every
        // page build so a page opened after a light/dark swap renders with the active palette.
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();

        return new StackPanel
        {
            Children =
            {
                BubbleSection(),
                ComposerSection(),
                StreamingSection()
            }
        };
    }

    private static Control BubbleSection()
    {
        var now = DateTimeOffset.Now;
        var participants = new List<ArcChatParticipant>
        {
            new() { Id = "me", Name = "Alex Rivera" },
            new() { Id = "sam", Name = "Sam Lee" }
        };

        var chat = new ArcChatView
        {
            Width = 960,
            Height = 365,
            HorizontalAlignment = HorizontalAlignment.Left,
            CurrentUserId = "me",
            Participants = participants,
            Label = "发布团队会话",
            Placeholder = "输入消息…",
            // Pinned to the top so the screenshot always starts at the first bubble.
            AutoScrollMode = ArcChatAutoScrollMode.Never
        };

        chat.AddMessage(new ArcChatMessage
        {
            Id = "m1",
            Role = ArcChatRole.User,
            Author = participants[0],
            Markdown = "把发布说明的截图和变更日志补上，顺便给我一个 Markdown 版本。",
            CreatedAt = now.AddMinutes(-6),
            Delivery = ArcChatDeliveryState.Read
        });

        chat.AddMessage(new ArcChatMessage
        {
            Id = "m2",
            Role = ArcChatRole.Assistant,
            Author = participants[1],
            CreatedAt = now.AddMinutes(-5),
            Markdown =
                "### 发布说明草稿已更新\n\n" +
                "- 新增定价页截图与空状态文案\n" +
                "- 气泡正文统一交给 `ArcMarkdownView` 渲染\n\n" +
                "```csharp\nchat.AddMessage(new ArcChatMessage\n{\n    Role = ArcChatRole.Assistant,\n    Markdown = \"**Done**\",\n});\n```\n\n" +
                "| 状态 | 含义 |\n| :--- | :--- |\n| Streaming | token 仍在到达 |\n| Complete | 生成完成 |\n| Failed | 生成失败 |"
        });

        chat.AddMessage(new ArcChatMessage
        {
            Id = "m3",
            Role = ArcChatRole.Assistant,
            Status = ArcChatMessageStatus.Failed,
            Error = "流式连接中断：已保留收到的内容，宿主可以重试。",
            Markdown = "连接中断前收到的最后一段正文仍然可读。",
            CreatedAt = now.AddMinutes(-1)
        });

        chat.AddMessage(new ArcChatMessage
        {
            Id = "m4",
            Role = ArcChatRole.System,
            Markdown = "Sam 加入了会话",
            CreatedAt = now
        });

        var caption = DemoUi.Caption(
            "气泡分组 · 生成状态与错误行 · 系统消息 · 内置 composer；Markdown 正文含代码块与表格。");

        return DemoUi.Section(
            "会话气泡",
            "ArcChatView 按作者与 GroupWindow 合并连续消息，只在一组开头显示头像与时间；自己的消息靠右。" +
            "气泡正文是完整 Markdown，附件、送达回执与生成状态分行展示。",
            new StackPanel { Spacing = 10, Children = { chat, caption } });
    }

    private static Control ComposerSection()
    {
        var status = DemoUi.Caption("Enter 发送 · Shift+Enter 换行 · IsRunning 时发送键切换成停止按钮。");

        var composer = new ArcChatComposer
        {
            Width = 960,
            HorizontalAlignment = HorizontalAlignment.Left,
            Placeholder = "Message the team",
            AllowAttachments = true,
            MaxAttachments = 3
        };

        composer.Send += (_, draft) =>
            status.Text = $"Send · {draft.Text.Length} 字符 · {draft.Attachments.Count} 个附件 · 宿主追加消息";
        composer.StopRequested += (_, _) => status.Text = "StopRequested · 宿主中断当前生成并保留部分内容";

        var running = new ArcButton
        {
            Content = "切换 IsRunning",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        running.Click += (_, _) =>
        {
            composer.IsRunning = !composer.IsRunning;
            status.Text = composer.IsRunning
                ? "IsRunning = true · 发送键变成停止键。"
                : "IsRunning = false · 恢复正常发送。";
        };

        var attach = new ArcButton
        {
            Content = "预置附件",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm
        };
        attach.Click += (_, _) =>
        {
            composer.AddAttachment(new ArcChatAttachment { Name = "screenshot.png", Size = 245_760, IsImage = true });
            status.Text = $"AddAttachment · 待发附件 {composer.PendingAttachments.Count} / {composer.MaxAttachments}";
        };

        var submit = new ArcButton
        {
            Content = "提交草稿",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm
        };
        submit.Click += (_, _) => composer.Submit();

        return DemoUi.Section(
            "输入框",
            "ArcChatComposer 是独立的输入控件：多行输入、Enter 发送、Shift+Enter 换行、附件 tray、IsRunning " +
            "停止态与 LeadingContent 插槽。Agent 层复用同一个输入框，不再维护第二套。",
            new StackPanel
            {
                Spacing = 10,
                Children = { composer, DemoUi.Row(running, attach, submit, status) }
            });
    }

    private static Control StreamingSection()
    {
        var now = DateTimeOffset.Now;
        var chat = new ArcChatView
        {
            Width = 560,
            Height = 165,
            HorizontalAlignment = HorizontalAlignment.Left,
            ShowComposer = false,
            AutoScrollMode = ArcChatAutoScrollMode.Never,
            Label = "流式回答演示"
        };
        chat.AddMessage(new ArcChatMessage
        {
            Id = "s1",
            Role = ArcChatRole.User,
            Markdown = "把变更日志生成一下。",
            CreatedAt = now.AddMinutes(-1)
        });

        var status = DemoUi.Caption("会话未开始 · 点击按钮演示 Append / Complete / Abort。");
        ArcMarkdownStreamingSession? session = null;
        ArcChatMessage? streamingMessage = null;

        void EnsureSession()
        {
            if (session is not null)
            {
                return;
            }

            streamingMessage = new ArcChatMessage
            {
                Id = "s2",
                Role = ArcChatRole.Assistant,
                Status = ArcChatMessageStatus.Streaming,
                Markdown = string.Empty
            };
            session = chat.BeginStreamingMessage(streamingMessage);
        }

        var append = new ArcButton
        {
            Content = "Append 一段",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        append.Click += (_, _) =>
        {
            EnsureSession();
            session!.Append("\n\n- 新增 `ArcChatView` 气泡\n- 统一 `ArcChatMessage` 模型");
            status.Text = $"Streaming · {session.CurrentText.Length} 字符 · 状态 {streamingMessage!.Status}";
        };

        var complete = new ArcButton
        {
            Content = "Complete",
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm
        };
        complete.Click += async (_, _) =>
        {
            if (session is null)
            {
                return;
            }

            await session.CompleteAsync();
            status.Text = "Completed · 全量 Markdown 解析，与一次性渲染一致。";
        };

        var abort = new ArcButton
        {
            Content = "Abort（保留部分）",
            Variant = ArcButtonVariant.Danger,
            Size = ArcButtonSize.Sm
        };
        abort.Click += async (_, _) =>
        {
            if (session is null)
            {
                return;
            }

            await session.AbortAsync();
            status.Text = "Aborted · 已收到的部分内容保留并写回消息。";
        };

        var side = new StackPanel
        {
            Spacing = 8,
            Width = 380,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                DemoUi.Label("流式会话"),
                DemoUi.Body("流式消息放在列表外的流式槽，完成后带最终 Markdown 与状态 upsert 回 Messages；" +
                            "自动滚动支持 Never / NearBottom / Always。"),
                DemoUi.Row(append, complete, abort),
                status,
                DemoUi.Body("消息级虚拟化：1000 条历史消息只实例化视口内的气泡。")
            }
        };

        return DemoUi.Section(
            "流式与虚拟化",
            "ArcChatView 的流式槽与 Markdown 流式会话共用稳定边界策略；完成时全量解析一次，结果与一次性渲染一致。",
            DemoUi.Row(chat, side));
    }
}
