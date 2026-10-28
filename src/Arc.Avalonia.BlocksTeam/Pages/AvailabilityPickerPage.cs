using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksTeam.Pages;

/// <summary>
/// Availability picker: a day strip and time slot list for a group meeting, with attendee avatars and a
/// confirmation step. Day and slot are real selections; the summary and the confirmation state follow them.
/// Days, slots and attendees are sample data, and times are labelled in the team's Zürich time zone.
/// </summary>
public sealed class AvailabilityPickerPage : ArcItemPage
{
    private sealed record Day(string Label, string Date, string Month, int[] Free);

    private static readonly IReadOnlyList<Day> Days = new[]
    {
        new Day("Mon", "9", "Nov", new[] { 4, 3, 2, 4, 3, 1 }),
        new Day("Tue", "10", "Nov", new[] { 3, 4, 4, 2, 4, 2 }),
        new Day("Wed", "11", "Nov", new[] { 2, 2, 4, 3, 1, 2 }),
        new Day("Thu", "12", "Nov", new[] { 4, 4, 3, 3, 4, 3 }),
        new Day("Fri", "13", "Nov", new[] { 1, 2, 2, 4, 3, 1 })
    };

    private static readonly IReadOnlyList<string> Times = new[] { "09:00", "10:30", "12:00", "13:30", "15:00", "16:30" };

    private static readonly IReadOnlyList<string> Attendees = new[]
    {
        "Maya Chen", "Jonas Weber", "Priya Raman", "Lena Fischer"
    };

    public override string ItemSlug => "availability-picker";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Picking a meeting time that works for several attendees.",
        "Booking flows that show a few days and open slots with a confirmation step."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use calendar or date-picker to pick a single date, and time-picker for a free time input."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Days and times are labelled groups of buttons with a pressed state; the selection is a live region.",
        "The time zone is named in the summary, so a time is never shown without its zone."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "The slot list slides in from the direction of the day change, and the selected highlight moves on a spring.",
        "Reduced motion removes the horizontal offset and the travel, switching slots instantly."
    };

    public override string? ImplementationNotes =>
        "Days, free counts and attendees are sample data in memory; Confirm only simulates booking and sends " +
        "nothing. Times are labelled Zürich time, matching the reference, and the 30/45/60 minute switch changes " +
        "the display only. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var dayIndex = 1;
        var slotIndex = 1;
        var duration = "30 min";
        var booked = false;

        var dayHost = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var slotHost = new StackPanel { Spacing = 6 };
        var summaryHost = new Border();
        var status = TeamKit.Caption("Everyone is in Zürich time.");

        void RenderDays()
        {
            dayHost.Children.Clear();
            for (var index = 0; index < Days.Count; index++)
            {
                var target = index;
                var day = Days[index];
                var selected = index == dayIndex;
                var content = new StackPanel
                {
                    Spacing = 2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = day.Label,
                            Classes = { "arc-caption" },
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = selected
                                ? TeamKit.Brush(ArcTokens.AccentStrong)
                                : TeamKit.Brush(ArcTokens.TextMuted)
                        },
                        new TextBlock
                        {
                            Text = day.Date,
                            FontSize = 20,
                            FontWeight = FontWeight.Medium,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        TeamKit.Caption(day.Month)
                    }
                };
                content.Children[2].HorizontalAlignment = HorizontalAlignment.Center;

                var card = TeamKit.Selectable(content, () =>
                {
                    dayIndex = target;
                    slotIndex = 0;
                    booked = false;
                    RenderDays();
                    RenderSlots();
                    RenderSummary();
                }, selected, $"{day.Label} {day.Date} {day.Month}", 14, new Thickness(6, 8));

                card.Width = 122;
                dayHost.Children.Add(card);
            }
        }

        void RenderSlots()
        {
            slotHost.Children.Clear();
            var day = Days[dayIndex];
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };

            for (var index = 0; index < Times.Count; index++)
            {
                var target = index;
                var free = day.Free[index];
                var selected = target == slotIndex;
                var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                content.Children.Add(new TextBlock
                {
                    Text = Times[index],
                    Classes = { "arc-label" },
                    VerticalAlignment = VerticalAlignment.Center
                });
                var dots = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                    Margin = new Thickness(12, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                for (var person = 0; person < Attendees.Count; person++)
                {
                    dots.Children.Add(new Border
                    {
                        Width = 7,
                        Height = 7,
                        CornerRadius = new CornerRadius(4),
                        Background = person < free
                            ? TeamKit.Brush(ArcTokens.Accent)
                            : TeamKit.Brush(ArcTokens.ControlTrack)
                    });
                }

                Grid.SetColumn(dots, 1);
                content.Children.Add(dots);

                var count = TeamKit.Caption(free == Attendees.Count ? "All free" : $"{free} of {Attendees.Count} free");
                count.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(count, 2);
                content.Children.Add(count);

                var slot = TeamKit.Selectable(content, () =>
                {
                    slotIndex = target;
                    booked = false;
                    RenderSlots();
                    RenderSummary();
                }, selected, $"{Times[index]}, {free} of {Attendees.Count} free", 12, new Thickness(10, 8));

                slot.Margin = new Thickness(index % 2 == 0 ? 0 : 4, 0, index % 2 == 0 ? 4 : 0, 6);
                Grid.SetRow(slot, index / 2);
                Grid.SetColumn(slot, index % 2);
                grid.Children.Add(slot);
            }

            slotHost.Children.Add(grid);
        }

        void RenderSummary()
        {
            var day = Days[dayIndex];
            var time = Times[slotIndex];
            var content = new StackPanel { Spacing = 10 };
            content.Children.Add(TeamKit.Caption("Selected time"));
            content.Children.Add(new TextBlock
            {
                Text = $"{day.Label} {day.Date} {day.Month}",
                Classes = { "arc-heading" }
            });
            content.Children.Add(new TextBlock
            {
                Text = $"{time} – {(slotIndex == Times.Count - 1 ? "17:00" : Times[slotIndex + 1])}",
                Classes = { "arc-subtitle" }
            });
            content.Children.Add(TeamKit.Caption("Zürich time · everyone in CET"));
            content.Children.Add(TeamKit.Divider());

            var people = new StackPanel { Spacing = 7 };
            var free = Days[dayIndex].Free[slotIndex];
            for (var index = 0; index < Attendees.Count; index++)
            {
                var available = index < free;
                people.Children.Add(TeamKit.Row(8,
                    TeamKit.Avatar(Attendees[index], ArcAvatarSize.Xs),
                    TeamKit.Caption(Attendees[index].Split(' ')[0]),
                    TeamKit.Caption(available ? "Free" : "Busy")));
                var row = (StackPanel)people.Children[^1];
                var statusText = (TextBlock)row.Children[2];
                statusText.Foreground = TeamKit.Brush(available ? ArcTokens.Success : ArcTokens.TextMuted);
            }

            content.Children.Add(people);
            content.Children.Add(TeamKit.Divider());

            var confirm = TeamKit.Btn(booked ? "Invite sent" : "Confirm time", ArcButtonVariant.Primary,
                ArcButtonSize.Sm, icon: booked ? "check" : "calendar");
            confirm.HorizontalAlignment = HorizontalAlignment.Stretch;
            confirm.HorizontalContentAlignment = HorizontalAlignment.Center;
            confirm.Click += (_, _) =>
            {
                booked = true;
                status.Text = $"Invite queued for {day.Label} {day.Date} {day.Month} at {time} (Zürich).";
                status.Foreground = TeamKit.Brush(ArcTokens.Success);
                RenderSummary();
            };
            content.Children.Add(confirm);
            content.Children.Add(TeamKit.Caption(booked
                ? "All four attendees will receive the calendar invite."
                : $"The invite covers {duration} plus a 15 minute buffer."));

            summaryHost.Child = content;
        }

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "Find a time", Classes = { "arc-heading" } },
                TeamKit.Caption("Sprint planning · 4 attendees · 30 minutes · Zürich")
            }
        });
        var attendees = TeamKit.Row(8,
            TeamKit.AvatarGroup(Attendees, ArcAvatarSize.Xs, 4, "Attendees"),
            TeamKit.Caption("4 people"));
        Grid.SetColumn(attendees, 1);
        heading.Children.Add(attendees);

        RenderDays();
        RenderSlots();
        RenderSummary();

        summaryHost.Background = TeamKit.Brush(ArcTokens.SurfaceMuted);
        summaryHost.CornerRadius = new CornerRadius(18);
        summaryHost.Padding = new Thickness(16);
        summaryHost.ClipToBounds = true;

        var durations = TeamKit.Segmented(new[] { ("30", "30 min"), ("45", "45 min"), ("60", "60 min") },
            "30", value =>
            {
                duration = $"{value} min";
                status.Text = $"Meeting length set to {duration}.";
                status.Foreground = TeamKit.Brush(ArcTokens.TextMuted);
            }, "Meeting length");

        var zones = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        var zoneNames = new[] { ("Zürich", "CET"), ("San Francisco", "PT"), ("Bengaluru", "IST") };
        for (var index = 0; index < zoneNames.Length; index++)
        {
            var cell = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = zoneNames[index].Item1, Classes = { "arc-label" } },
                    TeamKit.Caption(zoneNames[index].Item2)
                }
            };
            Grid.SetColumn(cell, index);
            zones.Children.Add(cell);
        }

        var zonePanel = new Border
        {
            Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    TeamKit.Label("Attendee time zones"),
                    zones
                }
            }
        };

        var left = TeamKit.Stack(14,
            TeamKit.Stack(8, TeamKit.Label("November 2026"), dayHost),
            TeamKit.Stack(10, TeamKit.Row(10, TeamKit.Label("Open times"), TeamKit.Caption("Click a slot to compare")), slotHost),
            zonePanel);

        var right = TeamKit.Stack(10,
            summaryHost,
            TeamKit.Stack(6, TeamKit.Label("Length"), durations),
            status,
            TeamKit.Caption("Nothing is booked until confirmation."));

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,340") };
        body.Children.Add(left);
        var rightHolder = new Border { Margin = new Thickness(24, 0, 0, 0), Child = right };
        Grid.SetColumn(rightHolder, 1);
        body.Children.Add(rightHolder);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(24),
            Child = TeamKit.Stack(18, heading, body)
        });
    }
}
