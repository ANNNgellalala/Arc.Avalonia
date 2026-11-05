using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Content.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Content.Pages;

/// <summary>
/// Chat thread's completeness preview: grouped consecutive messages, an outgoing message with an attachment
/// and a delivered status, the typing indicator and the composer. Keys match Docs/chat-thread.json.
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
        var now = DateTimeOffset.Now;
        var chat = new ArcChatThread
        {
            Width = 680,
            Height = 420,
            CurrentUserId = "me",
            Label = "Release team conversation",
            Placeholder = "Message the team",
            Participants = new List<ArcChatParticipant>
            {
                new() { Id = "me", Name = "Alex Rivera" },
                new() { Id = "sam", Name = "Sam Lee" }
            },
            Messages = new List<ArcChatMessage>
            {
                new()
                {
                    Id = "m1",
                    AuthorId = "sam",
                    Text = "Can you add the new screenshots?",
                    CreatedAt = now.AddMinutes(-9)
                },
                new()
                {
                    Id = "m2",
                    AuthorId = "sam",
                    Text = "The pricing page one is the hero.",
                    CreatedAt = now.AddMinutes(-8)
                },
                new()
                {
                    Id = "m3",
                    AuthorId = "me",
                    Text = "On it. The release notes draft is attached.",
                    CreatedAt = now.AddMinutes(-2),
                    Status = ArcChatMessageStatus.Delivered,
                    Attachments = new List<ArcChatAttachment>
                    {
                        new() { Name = "release-notes.md", Size = 18_432 }
                    }
                },
                new()
                {
                    Id = "m4",
                    AuthorId = "sam",
                    Text = "Perfect, reviewing now.",
                    CreatedAt = now.AddMinutes(-1)
                }
            },
            Typing = new List<string> { "sam" }
        };

        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock
        {
            Text = "分组消息 · 附件 · 送达状态 · 输入提示 · 输入框",
            Classes = { "arc-caption" }
        });
        panel.Children.Add(chat);
        return panel;
    }
}
