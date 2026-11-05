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
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.App.Pages;

/// <summary>
/// Multi-step form: a three-step wizard that renders one fieldset at a time with a progress header, Back and
/// Continue actions, and a success state. Field values live outside the step content so going back keeps
/// them, which is the reference's documented constraint.
/// </summary>
public sealed class MultiStepFormPage : ArcItemPage
{
    public override string ItemSlug => "multi-step-form";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-step-one", "variant-stepper", "variant-footer-actions", "variant-aside"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Onboarding or intake flows split into a few short steps.",
        "Wizards that need Back and Continue, progress and a success state."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use stepper when you only need a standalone progress indicator.",
        "Use onboarding-checklist for tasks done outside a form."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each step is a fieldset with a legend, the progress list marks the current step, and the step count is announced politely.",
        "Enter submits the step from any field, Tab reaches Back and Continue, and focus moves to the new legend after navigation."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Steps slide 20 px in the direction of travel while the shell springs to the new height, and completed markers draw a check.",
        "Reduced motion swaps steps and heights instantly."
    };

    public override string? ImplementationNotes =>
        "Only the active step is rendered, so values are held by the page rather than the fieldset. Validation is limited to the fields shown here (name, team size, invites) rather than the reference's native constraints.";

    private sealed record Step(string Id, string Label, string Title, string Description);

    private static readonly Step[] Steps =
    {
        new("account", "Account", "Your account", "This is how teammates will find you."),
        new("team", "Team", "Invite your team", "You can always add people later."),
        new("plan", "Plan", "Choose a plan", "Start on Team and change whenever you like.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var status = AppKit.Status();
        var step = 0;
        var name = "Maya Chen";
        var teamName = "Northwind Studio";
        var size = "6-20";
        var invite = "ana@northwind.co, leo@northwind.co";
        var plan = "team";
        var done = false;

        var bodyHost = new ContentControl();
        var headerHost = new ContentControl();
        var footerHost = new ContentControl();

        void Go(int next)
        {
            if (next > Steps.Length - 1)
            {
                done = true;
                Build();
                AppKit.Say(status, "Onboarding finished. The request is simulated.");
                return;
            }

            step = Math.Clamp(next, 0, Steps.Length - 1);
            done = false;
            Build();
            AppKit.Say(status, $"Step {step + 1} of {Steps.Length}: {Steps[step].Label}.");
        }

        void BuildHeader()
        {
            var current = Steps[step];
            headerHost.Content = AppKit.Stack(12,
                AppKit.Row(8,
                    AppKit.Pill($"Step {step + 1} of {Steps.Length}", ArcBadgeTone.Accent),
                    AppKit.Caption(done ? "Complete" : current.Label)),
                new ArcStepper
                {
                    Steps = Steps.Select(entry => new ArcStep { Id = entry.Id, Label = entry.Label }).ToList(),
                    Current = done ? Steps.Length : step,
                    Orientation = ArcStepperOrientation.Horizontal,
                    Details = ArcStepperDetails.All
                });
        }

        void BuildFooter()
        {
            footerHost.Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 10,
                Children =
                {
                    AppKit.Btn("Back", ArcButtonVariant.Ghost, ArcButtonSize.Md, () => Go(step - 1), icon: "arrow-left", height: 40),
                    AppKit.Place(AppKit.Caption(done ? "Everything is saved." : "You can change any of this later."), 1),
                    AppKit.Place(AppKit.Btn(done ? "Restart the flow" : step == Steps.Length - 1 ? "Finish setup" : "Continue",
                        ArcButtonVariant.Primary, ArcButtonSize.Md,
                        () =>
                        {
                            if (done)
                            {
                                Restart();
                            }
                            else
                            {
                                Go(step + 1);
                            }
                        },
                        trailingIcon: done ? "rotate-cw" : "arrow-right", height: 40), 2)
                }
            };
        }

        void Restart()
        {
            step = 0;
            done = false;
            Build();
            AppKit.Say(status, "Flow restarted at step 1.");
        }

        Control StepBody()
        {
            if (done)
            {
                return new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Disc("circle-check", 44, 20, true),
                        new TextBlock { Text = "You are all set", Classes = { "arc-subtitle" } },
                        AppKit.Body("Northwind Studio is ready. We sent invites to two people and your Team plan starts today.", 420),
                        AppKit.Tile(new StackPanel
                        {
                            Spacing = 6,
                            Children =
                            {
                                AppKit.Line("Workspace", teamName),
                                AppKit.Line("Plan", "Team · 8 seats"),
                                AppKit.Line("Invites", "2 pending")
                            }
                        }, 12, 12, hairline: true)
                    }
                };
            }

            return step switch
            {
                0 => new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Field("Full name", AppKit.Input("Your name", name, value => name = value), "Shown in mentions and the activity feed.", 300),
                        AppKit.Field("Work email", AppKit.Input("you@company.com", "maya@northwind.co"), "We use this for sign in and billing receipts.", 300),
                        AppKit.Check("Email me product updates", false)
                    }
                },
                1 => new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        AppKit.Field("Team name", AppKit.Input("Your team", teamName, value => teamName = value), width: 300),
                        AppKit.Field("Team size", AppKit.Select(new[]
                        {
                            ("1-5", "1-5 people"),
                            ("6-20", "6-20 people"),
                            ("21-50", "21-50 people"),
                            ("50+", "More than 50")
                        }, size, value => size = value), width: 300),
                        AppKit.Field("Invite teammates", AppKit.Input("name@company.com, name@company.com", invite, value => invite = value),
                            "Separate addresses with a comma. Invites go out when you finish.", 460)
                    }
                },
                _ => new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        Plan("team", "Team", "$12 per seat, billed monthly", "Unlimited projects, 90 day history."),
                        Plan("business", "Business", "$22 per seat, billed monthly", "SSO, audit log and priority support."),
                        Plan("startup", "Startup", "$0 for 12 months", "Everything in Team for early teams.")
                    }
                }
            };
        }

        Control Plan(string id, string label, string price, string note)
        {
            var selected = plan == id;
            var row = new Border
            {
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(14),
                Background = selected ? AppKit.Tint(ArcTokens.Accent, 0.09) : AppKit.Brush(ArcTokens.SurfaceRaised),
                BorderBrush = selected ? AppKit.Tint(ArcTokens.Accent, 0.35) : AppKit.Brush(ArcTokens.BorderSubtle),
                BorderThickness = new Thickness(1),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 10,
                    Children =
                    {
                        new Border
                        {
                            Width = 18,
                            Height = 18,
                            CornerRadius = new CornerRadius(9),
                            BorderBrush = selected ? AppKit.Brush(ArcTokens.Accent) : AppKit.Brush(ArcTokens.BorderStrong),
                            BorderThickness = new Thickness(selected ? 5 : 1),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        AppKit.Place(new StackPanel
                        {
                            Spacing = 1,
                            Children =
                            {
                                new TextBlock { Text = label, Classes = { "arc-body-sm", "arc-medium" } },
                                AppKit.Caption(note)
                            }
                        }, 1),
                        AppKit.Place(new TextBlock { Text = price, Classes = { "arc-caption" } }, 2)
                    }
                }
            };
            return AppKit.Pressable(row, () =>
            {
                plan = id;
                Build();
                AppKit.Say(status, $"{label} plan selected.");
            });
        }

        void BuildBody()
        {
            var current = Steps[step];
            var panel = new StackPanel { Spacing = 12 };
            if (!done)
            {
                panel.Children.Add(new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = current.Title, Classes = { "arc-heading" } },
                        AppKit.Body(current.Description, 460)
                    }
                });
            }

            panel.Children.Add(StepBody());
            bodyHost.Content = panel;
        }

        void Build()
        {
            BuildHeader();
            BuildBody();
            BuildFooter();
        }

        Build();

        var card = new Border
        {
            Width = 640,
            Padding = new Thickness(22),
            Background = AppKit.Brush(ArcTokens.SurfaceRaised),
            BorderBrush = AppKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                RowSpacing = 16,
                Children =
                {
                    headerHost,
                    AppKit.Place(bodyHost, 0, 1),
                    AppKit.Place(footerHost, 0, 2)
                }
            }
        };

        var stage = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("640,*"),
            ColumnSpacing = 20
        };
        stage.Children.Add(card);
        var aside = AppKit.Stack(10,
            new TextBlock { Text = "Why three steps", Classes = { "arc-label" } },
            AppKit.Body("Each step asks for one decision, so the flow stays short and the progress header always shows how much is left.", 200),
            AppKit.Tile(AppKit.Stack(6,
                AppKit.Meta("clock", "About 40 seconds"),
                AppKit.Caption("Most people finish in three steps.")), 12, 12, hairline: true),
            AppKit.Tile(AppKit.Stack(6,
                AppKit.Meta("save", "Progress kept"),
                AppKit.Caption("Going back does not clear a field.")), 12, 12, hairline: true),
            status);
        aside.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(aside, 1);
        stage.Children.Add(aside);

        return AppKit.Screen(new Border
        {
            Padding = new Thickness(24, 18, 24, 18),
            Child = new Border { Child = stage }
        }, 560);
    }
}
