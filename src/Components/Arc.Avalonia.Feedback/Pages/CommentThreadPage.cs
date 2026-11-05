using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Comment thread's completeness preview: nested replies, reaction chips, an edited comment, a deleted comment
/// and a resolved thread. Declared keys match Docs/comment-thread.json.
/// </summary>
public sealed class CommentThreadPage : ArcItemPage
{
    private const string Maya = "avares://Arc.Avalonia.Feedback/Assets/portrait-maya.png";
    private const string Sara = "avares://Arc.Avalonia.Feedback/Assets/portrait-sara.png";

    public override string ItemSlug => "comment-thread";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-nested", "variant-reactions", "variant-edited", "variant-deleted", "state-resolved"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var maya = new ArcCommentAuthor { Id = "maya", Name = "Maya Chen", Avatar = Maya };
        var leo = new ArcCommentAuthor { Id = "leo", Name = "Leo Park" };
        var sara = new ArcCommentAuthor { Id = "sara", Name = "Sara Ruiz", Avatar = Sara };
        var tom = new ArcCommentAuthor { Id = "tom", Name = "Tom Hale" };

        var active = new ArcCommentThread
        {
            Width = 720,
            Title = "Hero headline",
            CurrentUser = maya,
            Placeholder = "Write a comment",
            MaxDepth = 2,
            Comments = new List<ArcComment>
            {
                new()
                {
                    Id = "c1",
                    Author = maya,
                    CreatedAt = "2h",
                    Body = "The headline lands well. Could we try a shorter version for the mobile breakpoint?",
                    Reactions = new List<ArcCommentReaction>
                    {
                        new() { Emoji = "👍", Users = { "leo", "sara" } },
                        new() { Emoji = "🎉", Users = { "sara" } }
                    },
                    Replies = new List<ArcComment>
                    {
                        new()
                        {
                            Id = "c2",
                            Author = leo,
                            CreatedAt = "1h",
                            Body = "Agreed. I will test a two-line version at 390 and post the screenshots.",
                            Replies = new List<ArcComment>
                            {
                                new()
                                {
                                    Id = "c3",
                                    Author = sara,
                                    CreatedAt = "48m",
                                    Body = "The two-line version reads better on the compact card too."
                                }
                            }
                        }
                    }
                },
                new()
                {
                    Id = "c4",
                    Author = sara,
                    CreatedAt = "25m",
                    Body = "Added the approved copy to the handoff file.",
                    Edited = true
                },
                new()
                {
                    Id = "c5",
                    Author = tom,
                    CreatedAt = "10m",
                    Deleted = true
                }
            }
        };

        var resolved = new ArcCommentThread
        {
            Width = 720,
            Title = "Pricing copy",
            Resolved = true,
            Comments = new List<ArcComment>
            {
                new() { Id = "c6", Author = sara, CreatedAt = "3d", Body = "Copy signed off." }
            }
        };

        var panel = new StackPanel { Spacing = 26, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Labelled("回复、回应、编辑与删除", active));
        panel.Children.Add(Labelled("已解决", resolved));
        return panel;
    }

    private static Control Labelled(string label, Control content)
    {
        var column = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        column.Children.Add(content);
        return column;
    }
}
