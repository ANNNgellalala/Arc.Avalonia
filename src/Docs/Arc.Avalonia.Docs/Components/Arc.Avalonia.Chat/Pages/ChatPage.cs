using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Chat;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using PuraMarkdown.Agent;
using PuraMarkdown.UI;
using PuraMarkdown.Agent.Chat;

namespace Arc.Avalonia.Chat.Pages;

/// <summary>
/// Chat family showcase built on the upstream engine: <see cref="ArcChatView"/> wraps
/// <c>PuraChatView</c> (messages, Markdown rendering, streaming lifecycle, pura-agent components all come from
/// PuraMarkdown.Agent), while Arc keeps the composer, the design-system wrapper and the token-derived theme
/// mapping. The page shows grouped multi-turn messages, a tool call rendered above the assistant body, a
/// failed message, a system notice, the standalone composer and the Append / Complete / Abort streaming slot.
/// </summary>
public sealed class ChatPage : ArcPage
{
    public override string Title => "Chat";

    public override string Group => "Chat";

    public override string? Description =>
        "对话视图：渲染引擎整体委托给 PuraMarkdown.Agent（PuraChatView / PuraChatMessage / " +
        "PuraStreamingSession），Arc 只保留设计系统包装、输入框与主题 token 映射；pura-agent 围栏" +
        "（choice / confirm / tool-call）由上游直接渲染。";

    public override int Order => 1100;

    public override Control Build(ArcDemoContext context)
    {
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
        var chat = new ArcChatView
        {
            Width = 960,
            Height = 380,
            HorizontalAlignment = HorizontalAlignment.Left,
            ShowComposer = true,
            Placeholder = "输入消息…",
            AllowAttachments = true,
            // Pinned to the top so the screenshot always starts at the first message.
            AutoScrollMode = ArcChatAutoScrollMode.Never
        };

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m1",
            Role = PuraChatRole.User,
            Markdown = "把发布说明的截图和变更日志补上，顺便给我一个 Markdown 版本。",
            CreatedAt = now.AddMinutes(-6)
        });

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m2",
            Role = PuraChatRole.Assistant,
            CreatedAt = now.AddMinutes(-5),
            ToolCalls = new[]
            {
                new PuraToolCall
                {
                    Id = "call-1",
                    Name = "repo.search",
                    Status = PuraToolCallStatus.Succeeded,
                    Arguments = "{ \"query\": \"release notes\" }",
                    Result = "12 files matched · docs/29-puramarkdown-agent-migration.zh.md",
                    Duration = TimeSpan.FromMilliseconds(840)
                }
            },
            Markdown =
                "### 发布说明草稿已更新\n\n" +
                "- 新增定价页截图与空状态文案\n" +
                "- 正文渲染全部交给 `PuraMarkdown.Agent`（`PuraChatView` + `PuraMarkdownView`）\n\n" +
                "```csharp\nchat.AddMessage(new PuraChatMessage\n{\n    Role = PuraChatRole.Assistant,\n    Markdown = \"**Done**\",\n});\n```\n\n" +
                "| 状态 | 含义 |\n| :--- | :--- |\n| Streaming | token 仍在到达 |\n| Complete | 生成完成 |\n| Failed | 生成失败 |" +
                "\n\n参考 [Arc 文档](https://uiarc.dev) 与 [PuraMarkdown 迁移说明](https://uiarc.dev/docs/migration)。"
        });

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m3",
            Role = PuraChatRole.User,
            Markdown = "把附件也补上。",
            CreatedAt = now.AddMinutes(-3)
        });

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m4",
            Role = PuraChatRole.Assistant,
            Status = PuraChatMessageStatus.Failed,
            Error = "流式连接中断：已保留收到的内容，宿主可以重试。",
            Markdown = "连接中断前收到的最后一段正文仍然可读。",
            CreatedAt = now.AddMinutes(-1)
        });

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m5",
            Role = PuraChatRole.System,
            Markdown = "Sam 加入了会话",
            CreatedAt = now
        });

        var caption = DemoUi.Caption(
            "消息模型 = PuraChatMessage · 正文与工具卡 = PuraMarkdown.Agent 渲染 · 生成状态与错误行 · 系统消息 · 内置 composer。" +
            "正文里的链接可直接点击：默认用系统 launcher 打开，宿主可经 LinkClicked / LinkHandler 接管。");

        return DemoUi.Section(
            "会话视图",
            "ArcChatView 包装上游 PuraChatView：消息、Markdown 渲染、代码块/表格、pura-agent 围栏与流式生命周期" +
            "都由 PuraMarkdown.Agent 提供；Arc 额外派生 token 主题、内置 composer、无障碍名称，并补齐上游缺失的" +
            "消息内链接点击（LinkClicked / LinkHandler）。",
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
            status.Text = $"Send · {draft.Text.Length} 字符 · {draft.Attachments.Count} 个附件 · 宿主追加 PuraChatMessage";
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
            "ArcChatComposer 是 Arc 保留的宿主输入控件（属于表单，不是聊天渲染）：多行输入、Enter 发送、" +
            "Shift+Enter 换行、附件 tray、IsRunning 停止态与 LeadingContent 插槽。Agent 层复用同一个输入框。",
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
            Height = 190,
            HorizontalAlignment = HorizontalAlignment.Left,
            ShowComposer = false,
            AutoScrollMode = ArcChatAutoScrollMode.Never
        };
        chat.AddMessage(new PuraChatMessage
        {
            Id = "s1",
            Role = PuraChatRole.User,
            Markdown = "把变更日志生成一下。",
            CreatedAt = now.AddMinutes(-1)
        });

        var status = DemoUi.Caption("会话未开始 · 点击按钮演示 Append / Complete / Abort。");
        PuraStreamingSession? session = null;
        var streamingMessage = new PuraChatMessage { Id = "s2", Role = PuraChatRole.Assistant };

        void EnsureSession()
        {
            if (session is not null)
            {
                return;
            }

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
            session!.Append("\n\n- 新增 `ArcChatView` 包装\n- 消息模型换成 `PuraChatMessage`");
            status.Text = $"Streaming · {session.State} · {session.CurrentText.Length} 字符 · 待处理 {session.PendingChars}";
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
            status.Text = "Completed · PuraMarkdown.Agent 做一次全量校正，结果与一次性渲染一致。";
            session = null;
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
            status.Text = "Aborted · 保留已生成内容并写入历史（状态 Aborted）。";
            session = null;
        };

        return DemoUi.Section(
            "流式槽",
            "BeginStreamingMessage 返回上游 PuraStreamingSession；Append 增量、Complete 全量校正、Abort 保留" +
            "部分内容。Arc 不再自研流式会话与 Markdown 增量边界。",
            new StackPanel
            {
                Spacing = 10,
                Children = { chat, DemoUi.Row(append, complete, abort, status) }
            });
    }
}
