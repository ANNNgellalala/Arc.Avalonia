using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Blocks.Team.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team.Pages;

/// <summary>
/// Onboarding illustration: a compact first-run card whose line illustration morphs through folder, files, team
/// and badge scenes. Back, next and the step dots are real controls, arrow keys move between steps, and the
/// illustration respects reduced motion by switching scenes instantly. The copy is sample content.
/// </summary>
public sealed class OnboardingIllustrationPage : ArcItemPage
{
    private sealed record OnboardingStep(string Title, string Body, string SceneLabel);

    private static readonly IReadOnlyList<OnboardingStep> Steps = new[]
    {
        new OnboardingStep("Bring your files together",
            "Import docs, sheets and links so the whole team starts from the same folder.",
            "Illustration: a folder with a tab"),
        new OnboardingStep("Turn files into shared work",
            "Group the files that belong together, then give the collection one owner and a due date.",
            "Illustration: a stack of documents with text lines"),
        new OnboardingStep("Invite the people around it",
            "Share the folder with a view or edit role, and mention anyone who needs to know.",
            "Illustration: three people and a shared bar"),
        new OnboardingStep("Ship with a badge",
            "Close the folder out with a short summary and a status the whole workspace can see.",
            "Illustration: a badge with a check mark")
    };

    public override string ItemSlug => "onboarding-illustration";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-step-card", "variant-illustration", "variant-step-dots", "state-step-progress"
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A short first-run dialog in a document or workspace product.",
        "Onboarding with three or four steps that each need a simple picture."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use onboarding-checklist for tasks people complete over time, or feature-illustration on a marketing page."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Left and right arrows go to the previous or next step; the dots and both buttons are real controls.",
        "The illustration is an image with a label describing the current scene, and step copy is a polite live region."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Seven shared line shapes interpolate between scenes on a 420 ms smooth spring, so nothing swaps abruptly.",
        "Reduced motion jumps to each scene without the morph, which is the branch this port ships."
    };

    public override string? ImplementationNotes =>
        "The illustration is drawn with Avalonia geometry rather than an SVG; seven shared shapes interpolate between " +
        "the four scenes. Copy, scene labels and the Get started action are sample content and create nothing. " +
        "No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var step = 1;
        var finished = false;

        var illustration = new ArcMorphIllustration
        {
            Width = 330,
            Height = 196,
            Step = step
        };
        AutomationProperties.SetName(illustration, Steps[step].SceneLabel);

        var title = new TextBlock
        {
            Text = Steps[step].Title,
            Classes = { "arc-subtitle" },
            MaxWidth = 440,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var body = TeamKit.Body(Steps[step].Body, 440);
        var stepLabel = TeamKit.Caption($"Step {step + 1} of {Steps.Count}");
        var tourPill = TeamKit.Pill("Guided tour", ArcBadgeTone.Accent, dot: true);
        var progress = TeamKit.Meter(0, 4);
        progress.Width = 150;
        progress.HorizontalAlignment = HorizontalAlignment.Left;
        var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var actionsHost = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var status = TeamKit.Caption("Use the dots or the arrow keys to look through the tour.");

        void RenderDots()
        {
            dots.Children.Clear();
            for (var index = 0; index < Steps.Count; index++)
            {
                var target = index;
                var active = index == step;
                var dot = TeamKit.Selectable(new Border
                {
                    Width = active ? 20 : 7,
                    Height = 7,
                    CornerRadius = new CornerRadius(4),
                    Background = active ? TeamKit.Brush(ArcTokens.Accent) : TeamKit.Brush(ArcTokens.ControlTrack)
                }, () =>
                {
                    step = target;
                    finished = false;
                    Render();
                }, false, $"Step {index + 1}: {Steps[index].Title}", 999, new Thickness(0));
                dots.Children.Add(dot);
            }
        }

        void RenderActions()
        {
            var back = TeamKit.Btn("Back", ArcButtonVariant.Ghost, ArcButtonSize.Sm, icon: "chevron-left");
            back.IsEnabled = step > 0;
            back.Click += (_, _) =>
            {
                if (step == 0)
                {
                    return;
                }

                step--;
                finished = false;
                Render();
            };

            var next = TeamKit.Btn(step == Steps.Count - 1 ? "Get started" : "Next",
                ArcButtonVariant.Primary, ArcButtonSize.Sm,
                icon: step == Steps.Count - 1 ? "sparkles" : null,
                trailingIcon: step == Steps.Count - 1 ? null : "chevron-right");
            next.Click += (_, _) =>
            {
                if (step < Steps.Count - 1)
                {
                    step++;
                    Render();
                    return;
                }

                finished = true;
                RenderFinished();
            };

            actionsHost.Children.Clear();
            actionsHost.Children.Add(dots);
            var buttons = TeamKit.Row(8, back, next);
            Grid.SetColumn(buttons, 1);
            actionsHost.Children.Add(buttons);
        }

        void Render()
        {
            illustration.Step = step;
            AutomationProperties.SetName(illustration, Steps[step].SceneLabel);
            title.Text = Steps[step].Title;
            body.Text = Steps[step].Body;
            stepLabel.Text = $"Step {step + 1} of {Steps.Count}";
            progress.Fraction = (step + 1) / (double)Steps.Count;
            RenderDots();
            RenderActions();
        }

        void RenderFinished()
        {
            RenderDots();
            stepLabel.Text = "Setup complete";
            progress.Fraction = 1;
            title.Text = "You're set up";
            body.Text = "Your first folder is ready. Import files any time, or invite the rest of the team from settings.";
            status.Text = "The tour is complete. Reopen it any time from Help.";
            status.Foreground = TeamKit.Brush(ArcTokens.Success);

            var reopen = TeamKit.Btn("Reopen the tour", ArcButtonVariant.Secondary, ArcButtonSize.Sm, icon: "rotate-cw");
            reopen.Click += (_, _) =>
            {
                finished = false;
                step = 0;
                Render();
            };
            var open = TeamKit.Btn("Go to the folder", ArcButtonVariant.Primary, ArcButtonSize.Sm, icon: "folder");

            actionsHost.Children.Clear();
            actionsHost.Children.Add(dots);
            var buttons = TeamKit.Row(8, reopen, open);
            Grid.SetColumn(buttons, 1);
            actionsHost.Children.Add(buttons);
        }

        Render();

        var header = TeamKit.Row(9,
            new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(9),
                Background = TeamKit.Brush(ArcTokens.Accent),
                Child = new ArcIcon
                {
                    Icon = "layers",
                    Size = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = TeamKit.Brush(ArcTokens.AccentForeground)
                }
            },
            new StackPanel
            {
                Spacing = 0,
                Children =
                {
                    new TextBlock { Text = "Welcome to Northwind", Classes = { "arc-label" } },
                    TeamKit.Caption("A first run tour · about a minute")
                }
            });

        var copy = new StackPanel { Spacing = 6 };
        copy.Children.Add(stepLabel);
        copy.Children.Add(TeamKit.Row(10, tourPill, progress));
        copy.Children.Add(title);
        copy.Children.Add(body);

        var card = new Border
        {
            Width = 560,
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(22),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    header,
                    new Border
                    {
                        Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
                        CornerRadius = new CornerRadius(18),
                        Padding = new Thickness(10),
                        Child = new Panel { Children = { illustration } }
                    },
                    copy,
                    actionsHost
                }
            }
        };

        var backdrop = new Border
        {
            Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(26),
            Padding = new Thickness(28, 22),
            Child = card
        };

        var demo = new StackPanel { Spacing = 10, Children = { backdrop, status } };

        void FocusPrimaryAction()
        {
            if (actionsHost.Children.Count > 1 &&
                actionsHost.Children[1] is StackPanel row &&
                row.Children.Count > 1 &&
                row.Children[1] is Control primary)
            {
                primary.Focus();
            }
        }

        demo.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Left)
            {
                step = Math.Max(0, step - 1);
                finished = false;
                Render();
                FocusPrimaryAction();
                args.Handled = true;
            }
            else if (args.Key == Key.Right && !finished)
            {
                step = Math.Min(Steps.Count - 1, step + 1);
                Render();
                FocusPrimaryAction();
                args.Handled = true;
            }
        };

        return TeamKit.Root(demo);
    }
}


