using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Chat;
using Arc.Avalonia.Demo;
using PuraMarkdown.Agent;
using PuraMarkdown.Agent.Chat;

namespace Arc.Avalonia.Content.Pages;

/// <summary>
/// Chat thread's completeness preview on the Chat package. The surface is Arc's <see cref="ArcChatView"/>
/// wrapper around the upstream <c>PuraChatView</c>; the message model is <see cref="PuraChatMessage"/> and the
/// Markdown bodies, tool-call cards, pura-agent fences and streaming lifecycle are rendered by
/// PuraMarkdown.Agent (ADR 0007). Declared state keys match Docs/chat-thread.json so the audit can compare them.
/// </summary>
public sealed class ChatThreadPage : ArcItemPage
{
    public override string ItemSlug => "chat-thread";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-conversation", "variant-markdown", "variant-tool-call", "state-status-failed", "state-streaming"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var now = DateTimeOffset.Now;

        var chat = new ArcChatView
        {
            Width = 680,
            Height = 470,
            HorizontalAlignment = HorizontalAlignment.Left,
            ShowComposer = true,
            Placeholder = "Message the team",
            AllowAttachments = true,
            // Pinned to the top so the preview always shows the first message.
            AutoScrollMode = ArcChatAutoScrollMode.Never
        };

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m1",
            Role = PuraChatRole.User,
            Markdown = "Can you add the new screenshots?",
            CreatedAt = now.AddMinutes(-9)
        });

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m2",
            Role = PuraChatRole.Assistant,
            Markdown =
                "On it — two screenshots and a **Markdown** copy of the notes are coming.\n\n" +
                "| File | Size |\n| :--- | ---: |\n| pricing-page.png | 245 KB |\n| release-notes.md | 18 KB |",
            CreatedAt = now.AddMinutes(-8)
        });

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m3",
            Role = PuraChatRole.Assistant,
            ToolCalls = new[]
            {
                new PuraToolCall
                {
                    Id = "call-1",
                    Name = "repo.write",
                    Status = PuraToolCallStatus.Succeeded,
                    Arguments = "{ \"path\": \"release-notes.md\" }",
                    Result = "written · 18,432 bytes",
                    Duration = TimeSpan.FromMilliseconds(1260)
                }
            },
            Markdown = "Release notes saved next to the pricing screenshot.",
            CreatedAt = now.AddMinutes(-6)
        });

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m4",
            Role = PuraChatRole.Assistant,
            Status = PuraChatMessageStatus.Failed,
            Error = "Upload interrupted: 2 of 3 files were uploaded; retrying keeps the received parts.",
            Markdown = "The pricing screenshot was uploaded before the connection dropped.",
            CreatedAt = now.AddMinutes(-3)
        });

        chat.AddMessage(new PuraChatMessage
        {
            Id = "m5",
            Role = PuraChatRole.Assistant,
            Status = PuraChatMessageStatus.Streaming,
            Markdown = "### Generating the changelog\n\n- Added the pricing page screenshot\n- Unified every bubble body on",
            CreatedAt = now.AddMinutes(-1)
        });

        return chat;
    }
}
