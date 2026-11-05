using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;
using Ck = Arc.Avalonia.Blocks.Commerce.CommerceKit;

namespace Arc.Avalonia.Blocks.Commerce.Pages;

/// <summary>
/// Delivery tracker: an order status surface with a route stepper, a rolling arrival window, the courier and
/// quick replies, plus the update history and the order summary. The order, courier and events are simulated
/// sample data; replies never leave the page.
/// </summary>
public sealed class DeliveryTrackerPage : ArcItemPage
{
    public override string ItemSlug => "delivery-tracker";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-tracker", "variant-stepper", "variant-arrival", "variant-updates", "variant-order-summary"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Order status pages that follow a delivery from dispatch to the door.",
        "Tracking screens where people can message the courier about the drop off."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "A static list of past events; use timeline."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Stage changes and arrival updates are announced in a polite live region.",
        "Quick replies are a labelled group of buttons, and the courier's typing state is reported as status text."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The route marker glides between stops as the stage changes, and the arrival label swaps in place.",
        "Reduced motion makes the marker and the contact panel instant."
    };

    public override string? ImplementationNotes =>
        "The order, courier, events and replies are sample data. The reference morphs the contact button into a panel through a shared layout id and supports masked calling; the port swaps the panel in place and simulates the call with status text. Live carrier webhooks are out of scope.";

    private sealed record Update(string Time, string Label, string Detail, bool Done);

    private static readonly Update[] Updates =
    {
        new("14:05", "On the way", "Diego picked up the parcel at the depot", true),
        new("12:40", "Sorted", "Scanned at the Lisbon hub", true),
        new("09:15", "Packed", "Order packed and labelled", true)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var messages = new List<string>();
        var panelOpen = false;
        var called = false;
        var status = Ck.Status();

        var arrival = Ck.Text("4:20 pm", "arc-title-sm");
        var arrivalNote = Ck.Caption("Arriving today · 2 stops away");
        var replyHost = new ContentControl();
        var stepperHost = new ContentControl();

        var steps = new List<ArcStep>
        {
            new() { Id = "packed", Label = "Packed" },
            new() { Id = "picked", Label = "Picked up" },
            new() { Id = "way", Label = "On the way", Description = "2 stops away" },
            new() { Id = "done", Label = "Delivered" }
        };

        void BuildStepper()
        {
            stepperHost.Content = new ArcStepper
            {
                Steps = steps,
                Current = 2,
                Compact = true,
                Orientation = ArcStepperOrientation.Horizontal,
                Label = "Delivery progress"
            };
        }

        void SendReply(string text)
        {
            messages.Add(text);
            Ck.Say(status, $"Sent to Diego: {text}");
            arrivalNote.Text = "Diego is reading your message";
        }

        void BuildReplies()
        {
            if (!panelOpen)
            {
                replyHost.Content = null;
                return;
            }

            var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var reply in new[] { "Leave at the door", "Call on arrival", "Change the time" })
            {
                var text = reply;
                chips.Children.Add(Ck.Btn(text, ArcButtonVariant.Secondary, ArcButtonSize.Sm, () => SendReply(text), height: 32));
            }

            replyHost.Content = Ck.Tile(new StackPanel
            {
                Spacing = 7,
                Children =
                {
                    Ck.Caption("Quick replies"),
                    chips,
                    messages.Count == 0
                        ? Ck.Caption("Replies reach the courier in the reference; here they stay on the page.")
                        : Ck.Caption($"Last message: {messages[^1]}")
                }
            }, 10);
        }

        var messageButton = Ck.Btn("Message", ArcButtonVariant.Primary, ArcButtonSize.Sm, () =>
        {
            panelOpen = !panelOpen;
            BuildReplies();
            Ck.Say(status, panelOpen ? "Courier replies opened." : "Courier replies closed.");
        }, "message-circle", height: 34);

        var callButton = Ck.Btn("Call", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
        {
            called = !called;
            Ck.Say(status, called
                ? "Calling Diego through a masked number. Sample call, no audio."
                : "Call ended after 0:07.");
            arrivalNote.Text = called ? "On a masked call with Diego" : "Arriving today · 2 stops away";
        }, "phone", height: 34);

        BuildStepper();
        BuildReplies();

        var courier = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        courier.Children.Add(Ck.Person("Diego Alvarez", ArcAvatarSize.Md));
        courier.Children.Add(Place(new StackPanel
        {
            Spacing = 1,
            Children =
            {
                Ck.Text("Diego Alvarez", "arc-body-sm arc-medium"),
                Ck.Caption("Courier · white van · 4.9 stars")
            }
        }, 1));
        courier.Children.Add(Place(Ck.Row(6, messageButton, callButton), 2));

        var leftCard = Ck.Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        Ck.Caption("Order ARC-2481 · 2 items"),
                        Place(Ck.Pill("On the way", ArcBadgeTone.Accent, true), 1)
                    }
                },
                new StackPanel { Spacing = 2, Children = { arrival, arrivalNote } },
                stepperHost,
                Ck.Divider(),
                courier,
                replyHost,
                Ck.Divider(),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    ColumnSpacing = 8,
                    Children =
                    {
                        new ArcIcon { Icon = "map-pin", Size = 15, VerticalAlignment = VerticalAlignment.Center },
                        Place(new StackPanel
                        {
                            Spacing = 0,
                            Children = { Ck.Text("22 Harbour Road, Porto", "arc-body-sm"), Ck.Caption("Leave with reception, 2nd floor") }
                        }, 1),
                        Place(Ck.Link("Change", () => Ck.Say(status, "Delivery instructions open a form in the reference.")), 2)
                    }
                },
                status
            }
        }, 16);
        leftCard.VerticalAlignment = VerticalAlignment.Top;

        var updates = new StackPanel { Spacing = 10 };
        foreach (var update in Updates)
        {
            updates.Children.Add(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 10,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Border
                            {
                                Width = 9,
                                Height = 9,
                                CornerRadius = new CornerRadius(5),
                                Background = Ck.Brush(ArcTokens.Accent),
                                HorizontalAlignment = HorizontalAlignment.Center
                            },
                            new Border
                            {
                                Width = 1,
                                Height = 26,
                                Background = Ck.Brush(ArcTokens.BorderSubtle),
                                HorizontalAlignment = HorizontalAlignment.Center
                            }
                        }
                    },
                    Place(new StackPanel
                    {
                        Spacing = 1,
                        Children =
                        {
                            Ck.Row(8,
                                Ck.Text(update.Label, "arc-body-sm arc-medium"),
                                Ck.Caption(update.Time)),
                            Ck.Caption(update.Detail)
                        }
                    }, 1)
                }
            });
        }

        var updatesCard = Ck.Card(new StackPanel
        {
            Spacing = 10,
            Children = { Ck.Label("Updates"), updates }
        }, 14);

        var orderRows = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                OrderLine("Halo table lamp", "Brass · Medium", 189, "circle-dot"),
                OrderLine("Stoneware mug", "Mist · set of two", 38, "circle")
            }
        };

        var orderCard = Ck.Card(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Ck.Label("In this delivery"),
                orderRows,
                Ck.Divider(),
                Ck.Line("Total paid", Ck.Money(227), strong: true)
            }
        }, 14);

        var right = new StackPanel { Spacing = 12, Children = { updatesCard, orderCard } };

        var stage = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,24,330"),
            Children = { leftCard, Place(right, 2) }
        };

        return Ck.Specimen(stage, 16);

        Control OrderLine(string name, string detail, double price, string icon)
        {
            return new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8,
                Children =
                {
                    Ck.Photo(icon, 34, 30, 12),
                    Place(new StackPanel
                    {
                        Spacing = 0,
                        Children = { Ck.Text(name, "arc-body-sm"), Ck.Caption(detail) }
                    }, 1),
                    Place(Ck.Text(Ck.Money(price), "arc-body-sm"), 2)
                }
            };
        }
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
