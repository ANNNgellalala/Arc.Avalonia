using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using K = Arc.Avalonia.Blocks.AI.AiKit;

namespace Arc.Avalonia.Blocks.AI.Pages;

/// <summary>
/// Agent run: one coding agent on one task. Steps settle as the run proceeds, the live step waits for a person,
/// and approving the diff writes it and shows the result. The run and the pull request are simulated.
/// </summary>
public sealed class AgentRunPage : ArcItemPage
{
    public override string ItemSlug => "agent-run";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Coding agents and background jobs that read, search, edit and test, where people approve the change first.",
        "Product surfaces that hand an agent a ticket and return a pull request, such as an issue tracker.",
        "Demos that explain how an agent works step by step, with a real approval gate."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use research run for a research agent, or agent approvals for an inbox of pending tool calls."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The card is a section named by the task; steps are an ordered list and expandable steps are buttons.",
        "Diff lines carry hidden added and removed prefixes, and file tabs are a tablist with tabpanels.",
        "Status is never colour alone: finished steps change to past tense with a check, and sent back reads in words."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "New steps open their height on a spring and the live verb shimmers until the step settles.",
        "Reduced motion drops the travel, the fade and the shimmer, and keeps every state and announcement."
    };

    public override string? ImplementationNotes =>
        "The run is a snapshot at its approval gate. Approve writes the sample change on the page and opens the "
        + "result; Reject shows the note field and marks the step sent back. The file tab content, tool output and "
        + "pull request number are sample values, and the diff uses tokens rather than a syntax highlighter.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = K.Caption("Waiting for you: the agent will write two files once you approve.");
        var state = K.Pill("Waiting for you", ArcBadgeTone.Warning);
        var timer = K.Text("01:12", "arc-caption");

        var steps = new StackPanel { Spacing = 5 };
        void AddStep(string text, string meta, bool done)
        {
            steps.Children.Add(K.Row(7,
                done
                    ? new ArcIcon { Icon = "circle-check", Size = 13, Foreground = K.Brush(ArcTokens.Success) }
                    : new ArcIcon { Icon = "clock", Size = 13, Foreground = K.Brush(ArcTokens.Warning) },
                K.Text(text, done ? "arc-caption" : "arc-body-sm"),
                K.Caption(meta)));
        }

        AddStep("Read the rate limiter and its tests", "6s", true);
        AddStep("Searched for an existing middleware", "4s", true);
        AddStep("Edited src/api/rate-limit.ts", "12s", true);
        AddStep("Wrote a test for the bucket", "9s", true);
        AddStep("Review changes to 2 files", "waiting", false);

        var folded = K.Caption("Ran 5 steps in 1m 12s");
        folded.IsVisible = false;

        var fileTabs = new WrapPanel { Orientation = Orientation.Horizontal };
        var diffHost = new StackPanel { Spacing = 1 };
        void ShowDiff(bool testFile)
        {
            fileTabs.Children.Clear();
            var sourceTab = K.Chip("src/api/rate-limit.ts", selected: !testFile);
            sourceTab.Click += (_, _) => ShowDiff(false);
            fileTabs.Children.Add(sourceTab);
            var testTab = K.Chip("src/api/rate-limit.test.ts", selected: testFile);
            testTab.Click += (_, _) => ShowDiff(true);
            fileTabs.Children.Add(testTab);

            diffHost.Children.Clear();
            if (testFile)
            {
                diffHost.Children.Add(K.DiffLine("ctx", "describe('rate limit', () => {"));
                diffHost.Children.Add(K.DiffLine("add", "  it('rejects the 11th request in a minute', () => {"));
                diffHost.Children.Add(K.DiffLine("add", "    expect(limiter.take('ip_1')).toBe(false);"));
                diffHost.Children.Add(K.DiffLine("add", "  });"));
                diffHost.Children.Add(K.DiffLine("ctx", "});"));
            }
            else
            {
                diffHost.Children.Add(K.DiffLine("ctx", "export function take(key: string) {"));
                diffHost.Children.Add(K.DiffLine("del", "  return true;"));
                diffHost.Children.Add(K.DiffLine("add", "  const bucket = buckets.get(key) ?? newBucket();"));
                diffHost.Children.Add(K.DiffLine("add", "  if (bucket.spent >= bucket.limit) return false;"));
                diffHost.Children.Add(K.DiffLine("add", "  bucket.spent += 1;"));
                diffHost.Children.Add(K.DiffLine("ctx", "}"));
            }
        }

        ShowDiff(false);

        var noteField = new ArcTextarea
        {
            Placeholder = "Why are you sending this back?",
            Rows = 2,
            Height = 60,
            IsVisible = false
        };
        AutomationProperties.SetName(noteField, "Rejection note");

        var approvalHost = new Panel();
        var resultHost = new Panel { IsVisible = false };

        void BuildApproval()
        {
            var actions = K.Row(6,
                K.Btn("Reject", ArcButtonVariant.Ghost, ArcButtonSize.Sm, () =>
                {
                    noteField.IsVisible = true;
                    status.Text = "Add a note and send the change back. The agent revises once.";
                }, icon: "x"),
                K.Btn("Approve", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
                {
                    approvalHost.IsVisible = false;
                    steps.IsVisible = false;
                    folded.IsVisible = true;
                    resultHost.IsVisible = true;
                    state.Content = "Done";
                    state.Tone = ArcBadgeTone.Success;
                    timer.Text = "01:24";
                    status.Text = "Change approved and written. Open a pull request from the result.";
                }, icon: "check"));

            var card = new Border
            {
                Background = K.Brush(ArcTokens.SurfaceMuted),
                BorderBrush = K.Tint(ArcTokens.Warning, 0.3),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(11, 9, 11, 9),
                Child = K.Stack(7,
                    K.Row(7,
                        new ArcIcon { Icon = "pencil", Size = 13, Foreground = K.Brush(ArcTokens.Warning) },
                        K.Label("Review changes to 2 files"),
                        K.Caption("+38 −6")),
                    fileTabs,
                    new Border
                    {
                        Background = K.Brush(ArcTokens.Surface),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(6, 4, 6, 4),
                        Child = diffHost
                    },
                    noteField,
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            K.Caption("Nothing is written until you approve."),
                            actions
                        }
                    })
            };
            if (card.Child is StackPanel cardStack && cardStack.Children[^1] is Grid footer)
            {
                Grid.SetColumn(footer.Children[1], 1);
            }

            approvalHost.Children.Clear();
            approvalHost.Children.Add(card);
        }

        BuildApproval();

        var content = new StackPanel
        {
            Spacing = 9,
            MaxWidth = 780,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        K.Stack(2,
                            K.Text("Add rate limiting to the public API", "arc-heading", 520),
                            K.Row(6,
                                K.Mark("anthropic", 15),
                                K.Caption("Claude Sonnet 5.5"),
                                K.Dot(ArcTokens.BorderStrong, 3),
                                K.Caption("arc-ui/api"),
                                K.Dot(ArcTokens.BorderStrong, 3),
                                timer)),
                        K.Row(6, state, K.IconBtn("refresh-cw", "Restart run"))
                    }
                },
                K.Divider(2),
                K.Caption("Steps"),
                steps,
                folded,
                approvalHost,
                resultHost,
                status
            }
        };

        if (content.Children[0] is Grid head)
        {
            Grid.SetColumn(head.Children[1], 1);
        }

        // The result panel is built after the content so it can reuse the same controls.
        var openPr = K.Btn("Open pull request", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
        {
            status.Text = "Pull request #482 opened from the result. Simulated.";
        }, icon: "arrow-up-right");
        var resultBody = new StackPanel { Spacing = 7 };
        resultBody.Children.Add(K.Text("Added a token bucket limiter to the public API and covered it with a "
            + "test that rejects the eleventh request in a minute.", "arc-body-sm", 600));
        resultBody.Children.Add(K.Row(6,
            new ArcIcon { Icon = "code", Size = 12 },
            K.Caption("2 files changed"),
            K.Caption("+38 −6")));
        resultBody.Children.Add(K.Row(6,
            new ArcIcon { Icon = "circle-check", Size = 12, Foreground = K.Brush(ArcTokens.Success) },
            K.Caption("12 checks passed"),
            K.Dot(ArcTokens.BorderStrong, 3),
            K.Caption("4m 02s")));
        resultBody.Children.Add(K.Row(6, openPr, K.Chip("View files", icon: "file")));
        resultHost.Children.Clear();
        resultHost.Children.Add(new Border
        {
            Background = K.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(11, 9, 11, 9),
            Child = resultBody
        });

        return K.Surface(new Border
        {
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 880
        }, new Thickness(22, 15, 22, 15));
    }
}
