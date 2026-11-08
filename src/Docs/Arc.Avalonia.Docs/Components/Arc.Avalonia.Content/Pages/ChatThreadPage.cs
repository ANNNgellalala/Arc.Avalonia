using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Chat;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Content.Pages;

/// <summary>
/// Chat thread's completeness preview on the Chat package: grouped consecutive messages, an own message with
/// an attachment and a Delivered receipt, the typing row and the built-in composer. Declared state keys match
/// Docs/chat-thread.json so the audit can compare the two.
/// </summary>
public sealed class ChatThreadPage : ArcItemPage
{
    public override string ItemSlug => "chat-thread";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-grouped", "variant-attachments", "variant-typing", "state-status-delivered", "variant-composer"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        // Bubble bodies are ArcMarkdownView; refresh the token snapshot for the active palette.
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();

        var now = DateTimeOffset.Now;
        var participants = new List<ArcChatParticipant>
        {
            new() { Id = "me", Name = "Alex Rivera" },
            new() { Id = "sam", Name = "Sam Lee" }
        };

        var chat = new ArcChatView
        {
            Width = 680,
            Height = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
            CurrentUserId = "me",
            Label = "Release team conversation",
            Placeholder = "Message the team",
            Participants = participants,
            ShowComposer = true,
            AllowAttachments = true,
            // Pinned to the top so the preview always shows the first bubble.
            AutoScrollMode = ArcChatAutoScrollMode.Never
        };

        chat.AddMessage(new ArcChatMessage
        {
            Id = "m1",
            Role = ArcChatRole.User,
            Author = participants[1],
            Markdown = "Can you add the new screenshots?",
            CreatedAt = now.AddMinutes(-9)
        });

        chat.AddMessage(new ArcChatMessage
        {
            Id = "m2",
            Role = ArcChatRole.User,
            Author = participants[1],
            Markdown = "The pricing page one is the hero.",
            CreatedAt = now.AddMinutes(-8)
        });

        chat.AddMessage(new ArcChatMessage
        {
            Id = "m3",
            Role = ArcChatRole.Assistant,
            Author = participants[0],
            Markdown = "On it. The release notes draft is attached.",
            CreatedAt = now.AddMinutes(-2),
            Delivery = ArcChatDeliveryState.Delivered,
            Attachments = new List<ArcChatAttachment>
            {
                new() { Name = "release-notes.md", Size = 18_432 }
            }
        });

        chat.AddMessage(new ArcChatMessage
        {
            Id = "m4",
            Role = ArcChatRole.User,
            Author = participants[1],
            Markdown = "Perfect, reviewing now.",
            CreatedAt = now.AddMinutes(-1)
        });

        chat.Typing = new List<string> { "sam" };

        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock
        {
            Text = "分组消息 · 附件 · Delivered 回执 · 正在输入 · 输入框",
            Classes = { "arc-caption" }
        });
        panel.Children.Add(chat);
        return panel;
    }
}
