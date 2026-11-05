using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>Demo compositions shared by the feedback gallery pages.</summary>
internal static class FeedbackDemos
{
    private const string Maya = "avares://Arc.Avalonia.Docs/Assets/portrait-maya.png";
    private const string Sara = "avares://Arc.Avalonia.Docs/Assets/portrait-sara.png";

    public static Control Progress()
    {
        var tones = new ArcProgress { Value = 86, Label = "Storage migration", ShowValue = true, Tone = ArcProgressTone.Warning };
        var danger = new ArcProgress { Value = 97, Label = "Sync backlog", ShowValue = true, Tone = ArcProgressTone.Danger };

        var column = new StackPanel { Spacing = 20 };
        column.Children.Add(new ArcProgress { Value = 71, Label = "Uploading report.pdf", ShowValue = true });
        column.Children.Add(new ArcProgress { Value = 100, Label = "Import finished", ShowValue = true });
        column.Children.Add(new ArcProgress { Value = 38, Label = "Processing images", ShowValue = true, Size = ArcProgressSize.Sm });
        column.Children.Add(PageLayout.EqualRow(tones, danger));
        column.Children.Add(new ArcProgress { Label = "Waiting for the server", Indeterminate = true });
        return PageLayout.Narrow(640, column);
    }

    public static Control Skeleton()
    {
        var profile = new ArcSkeleton { Avatar = true, Lines = 3 };
        var block = new ArcSkeleton { Shape = ArcSkeletonShape.Rect, Height = 88 };
        var circle = new ArcSkeleton { Shape = ArcSkeletonShape.Circle, Height = 56 };
        var loaded = new ArcSkeleton
        {
            Loading = false,
            Content = new TextBlock
            {
                Text = "Content has loaded into the reserved space.",
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap
            }
        };

        return PageLayout.EqualRow(
            PageLayout.Narrow(280, PageLayout.Caption("Profile loading"), profile),
            PageLayout.Narrow(280, PageLayout.Caption("Media block and circle"), block, circle),
            PageLayout.Narrow(280, PageLayout.Caption("Loaded content"), loaded));
    }

    public static Control Stepper()
    {
        var steps = new List<ArcStep>
        {
            new() { Id = "cart", Label = "Cart" },
            new() { Id = "shipping", Label = "Shipping", Description = "Address and delivery" },
            new() { Id = "payment", Label = "Payment", Description = "Card and billing address" },
            new() { Id = "review", Label = "Review" }
        };

        var horizontal = new ArcStepper
        {
            Steps = steps,
            Current = 1,
            Label = "Checkout progress"
        };
        horizontal.StepSelected += (_, index) => horizontal.Current = index;

        var advance = new ArcButton
        {
            Content = "Next step",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        advance.Click += (_, _) => horizontal.Current = Math.Min(horizontal.Current + 1, steps.Count);

        var verticalSteps = new List<ArcStep>
        {
            new() { Id = "account", Label = "Account", Description = "Name, email, and password" },
            new() { Id = "workspace", Label = "Workspace", Description = "Team name and region" },
            new() { Id = "billing", Label = "Billing", Error = "No payment method on file" },
            new() { Id = "invite", Label = "Invite people", Description = "Optional" }
        };
        var vertical = new ArcStepper
        {
            Steps = verticalSteps,
            Current = 2,
            Orientation = ArcStepperOrientation.Vertical,
            Details = ArcStepperDetails.All,
            Label = "Account setup"
        };

        var compact = new ArcStepper
        {
            Steps = steps,
            Current = 4,
            Compact = true,
            CompleteLabel = "All steps complete"
        };

        return PageLayout.Stack(
            28,
            PageLayout.Narrow(880, PageLayout.Caption("Horizontal with a back path"), horizontal, advance),
            PageLayout.Narrow(440, PageLayout.Caption("Vertical with an error"), vertical),
            PageLayout.Narrow(680, PageLayout.Caption("Compact, complete"), compact));
    }

    public static Control Countdown()
    {
        var now = DateTimeOffset.Now;
        var large = new ArcCountdown
        {
            Target = now.AddDays(2).AddHours(4).AddMinutes(12).AddSeconds(30),
            Label = "Keynote starts in",
            CompleteLabel = "Live now"
        };
        var compact = new ArcCountdown
        {
            Target = now.AddHours(1).AddMinutes(8).AddSeconds(20),
            Variant = ArcCountdownVariant.Compact,
            Units = new[] { ArcCountdownUnit.Hours, ArcCountdownUnit.Minutes, ArcCountdownUnit.Seconds },
            Label = "Sale ends in"
        };
        var live = new ArcCountdown
        {
            Target = now.AddMinutes(-4),
            Label = "Stream status",
            CompleteLabel = "Live now"
        };

        return PageLayout.Stack(20, large, compact, live);
    }

    public static Control UsageMeters()
    {
        var requests = new ArcUsageMeter
        {
            Label = "Requests",
            Unit = "requests",
            Limit = 10000,
            Decimals = 1,
            Segments = new[]
            {
                new ArcUsageSegment { Id = "api", Label = "API", Value = 8400 }
            }
        };

        var storage = new ArcUsageMeter
        {
            Label = "Workspace storage",
            Unit = "GB",
            Limit = 50,
            Decimals = 1,
            Segments = new[]
            {
                new ArcUsageSegment { Id = "files", Label = "Files", Value = 21.4 },
                new ArcUsageSegment { Id = "media", Label = "Media", Value = 14.2 },
                new ArcUsageSegment { Id = "backups", Label = "Backups", Value = 6.8 }
            }
        };

        var seats = new ArcUsageMeter
        {
            Label = "Team seats",
            Unit = "seats",
            Limit = 100,
            Decimals = 0,
            Segments = new[]
            {
                new ArcUsageSegment { Id = "members", Label = "Members", Value = 96 },
                new ArcUsageSegment { Id = "guests", Label = "Guests", Value = 16 }
            }
        };

        return PageLayout.EqualRow(
            PageLayout.Narrow(300, requests),
            PageLayout.Narrow(320, storage),
            PageLayout.Narrow(280, seats));
    }

    public static Control Avatars()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new ArcAvatar { DisplayName = "Maya Chen", Src = Maya, Size = ArcAvatarSize.Xs });
        row.Children.Add(new ArcAvatar { DisplayName = "Maya Chen", Src = Maya, Size = ArcAvatarSize.Sm });
        row.Children.Add(new ArcAvatar { DisplayName = "Maya Chen", Src = Maya, Size = ArcAvatarSize.Md, Status = ArcAvatarStatus.Online });
        row.Children.Add(new ArcAvatar { DisplayName = "Leo Park", Size = ArcAvatarSize.Lg });
        row.Children.Add(new ArcAvatar { DisplayName = "Sara Ruiz", Src = Sara, Size = ArcAvatarSize.Xl, Status = ArcAvatarStatus.Online });
        row.Children.Add(new ArcAvatar { DisplayName = "Ines Ma", Size = ArcAvatarSize.Md, Status = ArcAvatarStatus.Offline });

        var shapes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
        shapes.Children.Add(new ArcAvatar { DisplayName = "Maya Chen", Src = Maya, Shape = ArcAvatarShape.Square, Size = ArcAvatarSize.Md });
        shapes.Children.Add(new ArcAvatar { DisplayName = "Leo Park", Shape = ArcAvatarShape.Square, Size = ArcAvatarSize.Md });
        shapes.Children.Add(new ArcAvatar { DisplayName = "Tom Hale", Shape = ArcAvatarShape.Square, Size = ArcAvatarSize.Lg, Status = ArcAvatarStatus.Online });
        shapes.Children.Add(new ArcAvatar { DisplayName = "Sara Ruiz", Src = Sara, Shape = ArcAvatarShape.Square, Size = ArcAvatarSize.Xl });

        return PageLayout.Stack(16, row, shapes);
    }

    public static Control AvatarGroup()
    {
        var team = new[]
        {
            new ArcAvatarMember { Name = "Maya Chen", Src = Maya, Status = ArcAvatarStatus.Online },
            new ArcAvatarMember { Name = "Leo Park" },
            new ArcAvatarMember { Name = "Sara Ruiz", Src = Sara },
            new ArcAvatarMember { Name = "Tom Hale" },
            new ArcAvatarMember { Name = "Ines Ma" }
        };

        var small = new ArcAvatarGroup { Members = team, Max = 2, Size = ArcAvatarSize.Sm, Label = "Editors" };
        var medium = new ArcAvatarGroup { Members = team, Max = 3, Size = ArcAvatarSize.Md, Label = "Team members" };
        var large = new ArcAvatarGroup
        {
            Members = team.Take(4).ToArray(),
            Max = 4,
            Size = ArcAvatarSize.Lg,
            Label = "Reviewers",
            FanOnHover = true
        };

        return PageLayout.EqualRow(
            PageLayout.Narrow(220, PageLayout.Caption("Small with overflow"), small),
            PageLayout.Narrow(260, PageLayout.Caption("Medium, 3 shown"), medium),
            PageLayout.Narrow(300, PageLayout.Caption("Large, hover to fan"), large));
    }

    public static Control MetricCards()
    {
        var sparkline = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var values = new[] { 8.0, 12, 10, 15, 13, 18, 16, 22, 20, 25 };
        foreach (var value in values)
        {
            var bar = new Border
            {
                Width = 4,
                Height = value,
                CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            bar.SetValue(Border.BackgroundProperty, DemoUi.Brush(ArcTokens.Series1));
            bar.Opacity = 0.55;
            sparkline.Children.Add(bar);
        }

        var uptime = new ArcMetricCard
        {
            Label = "Uptime",
            Value = 99.9,
            Suffix = "%",
            Context = "Last 30 days",
            Change = "+0.2%",
            Trend = ArcMetricTrend.Up
        };

        var revenue = new ArcMetricCard
        {
            Label = "Monthly revenue",
            Value = 48250,
            Context = "vs last month",
            Change = "-3.1%",
            Trend = ArcMetricTrend.Down
        };

        var sessions = new ArcMetricCard
        {
            Label = "Active sessions",
            Value = 1284,
            Context = "Right now",
            Change = "+12%",
            Trend = ArcMetricTrend.Up,
            Sparkline = sparkline
        };

        return PageLayout.EqualRow(uptime, revenue, sessions);
    }

    public static Control EmptyStates()
    {
        var noResults = new ArcEmptyState
        {
            Icon = "search",
            Title = "No matches",
            Description = "Try a shorter search, or clear the filters to see everything again.",
            Action = new ArcButton { Content = "Clear filters", Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm },
            SecondaryAction = new ArcButton { Content = "Search tips", Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm },
            Label = "No search results"
        };

        var firstRun = new ArcEmptyState
        {
            Icon = "folder",
            Title = "No projects yet",
            Description = "Create the first project, or import one from a CSV file.",
            Action = new ArcButton { Content = "Create project", Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm },
            SecondaryAction = new ArcButton { Content = "Import", Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm }
        };

        return PageLayout.EqualRow(
            new ArcCard { Content = noResults, Padding = new Thickness(8) },
            new ArcCard { Content = firstRun, Padding = new Thickness(8) });
    }

    public static Control Timeline()
    {
        var now = DateTimeOffset.Now;
        var events = new List<ArcTimelineEvent>
        {
            new()
            {
                Id = "e1",
                At = now.AddMinutes(-12),
                Actor = "Maya Chen",
                Title = "merged Checkout redesign into main",
                Meta = "PR #482",
                Tone = ArcTimelineTone.Success
            },
            new()
            {
                Id = "e2",
                At = now.AddMinutes(-41),
                Title = "Deploy failed",
                Meta = "Build step exited with code 1",
                Tone = ArcTimelineTone.Danger,
                Detail = "error: Type 'OrderSummary' is missing the following properties:\n  total, currency",
                Icon = "triangle-alert"
            },
            new()
            {
                Id = "e3",
                At = now.AddHours(-3).AddMinutes(-5),
                Actor = "Leo Park",
                Title = "opened an issue about slow exports",
                Meta = "EXPORT-118"
            },
            new()
            {
                Id = "e4",
                At = now.AddDays(-1).AddHours(-2),
                Actor = "Sara Ruiz",
                Avatar = Sara,
                Title = "commented on Billing settings",
                Meta = "4 replies"
            }
        };

        return PageLayout.Narrow(700, new ArcTimeline
        {
            Events = events,
            Now = now,
            MaxHeight = 460,
            Label = "Project activity",
            DefaultExpanded = new[] { "e2" }
        });
    }

    public static Control CommentThread()
    {
        var maya = new ArcCommentAuthor { Id = "maya", Name = "Maya Chen", Avatar = Maya };
        var leo = new ArcCommentAuthor { Id = "leo", Name = "Leo Park" };
        var sara = new ArcCommentAuthor { Id = "sara", Name = "Sara Ruiz", Avatar = Sara };

        var comments = new List<ArcComment>
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
                Body = "I added the approved copy to the handoff file.",
                Edited = true,
                Reactions = new List<ArcCommentReaction>
                {
                    new() { Emoji = "✅", Users = { "maya" } }
                }
            }
        };

        return PageLayout.Narrow(720, new ArcCommentThread
        {
            Title = "Hero headline",
            CurrentUser = maya,
            Comments = comments,
            Placeholder = "Write a comment",
            MaxDepth = 2
        });
    }

    public static Control Counters()
    {
        var revenue = new ArcAnimatedCounter
        {
            Value = 48250,
            Prefix = "$",
            Label = "Revenue this month",
            AnimateOnView = true
        };

        var add = new ArcButton
        {
            Content = "Add 1,000",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        add.Click += (_, _) => revenue.Value += 1000;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 48,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        row.Children.Add(revenue);
        row.Children.Add(new ArcAnimatedCounter
        {
            Value = 12840,
            Label = "Signups this week",
            AnimateOnView = true
        });
        row.Children.Add(new ArcAnimatedCounter
        {
            Value = 99.9,
            Suffix = "%",
            Decimals = 1,
            Label = "Uptime",
            AnimateOnView = true
        });

        return PageLayout.Stack(16, row, add);
    }
}
