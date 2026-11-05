using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// Announcement bar's completeness preview: a neutral bar with two messages, controls, a countdown, an action
/// and the dismiss button, plus the inverted tone that cannot be dismissed. Keys match Docs/announcement-bar.json.
/// </summary>
public sealed class AnnouncementBarPage : ArcItemPage
{
    public override string ItemSlug => "announcement-bar";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "tone-neutral", "tone-inverted", "variant-countdown", "variant-controls", "variant-dismissible"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var neutral = new ArcAnnouncementBar
        {
            Id = "docs-announcement-neutral",
            Controls = true,
            AutoPlay = false,
            Tone = ArcAnnouncementTone.Neutral,
            Messages = new[]
            {
                new ArcAnnouncement
                {
                    Id = "sale",
                    Message = "Fall sale: 30% off",
                    Countdown = new ArcAnnouncementCountdown
                    {
                        To = DateTimeOffset.Now.AddDays(3).AddHours(4),
                        Label = "Ends in"
                    },
                    Action = new ArcAnnouncementAction { Label = "See plans" }
                },
                new ArcAnnouncement
                {
                    Id = "beta",
                    Message = "Workflows are now in beta",
                    Action = new ArcAnnouncementAction { Label = "Read more" }
                }
            }
        };

        var inverted = new ArcAnnouncementBar
        {
            Id = "docs-announcement-inverted",
            AutoPlay = false,
            Dismissible = false,
            Tone = ArcAnnouncementTone.Inverted,
            Messages = new[]
            {
                new ArcAnnouncement { Id = "maintenance", Message = "Maintenance on Sunday, 02:00 to 04:00 UTC" }
            }
        };

        var panel = new StackPanel { Spacing = 24, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Labelled("Neutral · 倒计时 · 控件 · 可关闭", neutral));
        panel.Children.Add(Labelled("Inverted · 不可关闭", inverted));
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
