using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Threading;
using Arc.Avalonia.Content.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Content.Pages;

/// <summary>Conversation and setup flows built from the content primitives.</summary>
public sealed class ContentFlowsPage : ArcPage
{
    public override string Title => "Flows";

    public override string Group => "Content";

    public override string? Description =>
        "A live conversation, then a setup card that keeps progress visible.";

    public override int Order => 901;

    public override Control Build(ArcDemoContext context)
    {
        var now = DateTimeOffset.Now;
        var chat = new ArcChatThread
        {
            Width = 680,
            Height = 480,
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
                    Id = "m5", AuthorId = "sam", Text = "Can you add the new screenshots?",
                    CreatedAt = now.AddMinutes(-9)
                },
                new()
                {
                    Id = "m6", AuthorId = "sam", Text = "The pricing page one is the hero.",
                    CreatedAt = now.AddMinutes(-8)
                },
                new()
                {
                    Id = "m7", AuthorId = "me",
                    Text = "On it. The release notes draft is attached.",
                    CreatedAt = now.AddMinutes(-2), Status = ArcChatMessageStatus.Delivered,
                    Attachments = new List<ArcChatAttachment>
                    {
                        new() { Name = "release-notes.md", Size = 18_432 }
                    }
                }
            },
            Typing = new List<string> { "sam" }
        };

        var checklist = new ArcOnboardingChecklist
        {
            Width = 660,
            Title = "Set up your workspace",
            DoneTitle = "Workspace ready",
            DefaultCompleted = new[] { "profile", "invite" },
            Steps = new List<ArcChecklistStep>
            {
                new()
                {
                    Id = "profile", Title = "Add your profile photo",
                    Description = "A face makes comments and mentions easier to scan."
                },
                new()
                {
                    Id = "invite", Title = "Invite your team",
                    Description = "Bring the people who will review, build, and ship with you.",
                    ActionLabel = "Invite"
                },
                new()
                {
                    Id = "connect", Title = "Connect GitHub",
                    Description = "Sync issues and pull requests so releases stay in one place.",
                    ActionLabel = "Connect"
                },
                new()
                {
                    Id = "domain", Title = "Verify your domain",
                    Description = "Use your own address for links and notification emails.",
                    ActionLabel = "Verify"
                },
                new()
                {
                    Id = "plan", Title = "Choose a plan",
                    Description = "Pick the plan that matches your team size. You can change it later.",
                    ActionLabel = "View plans"
                }
            }
        };
        checklist.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => checklist.ExpandForDemo("connect"), DispatcherPriority.Background);

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Chat thread",
                    "Use for support, DMs, and team threads with grouped messages, attachments, and a typing state.",
                    chat),
                DemoUi.Section(
                    "Onboarding checklist",
                    "Use for a short, loosely ordered setup flow that should stay visible until it is done.",
                    checklist)
            }
        };
    }
}
