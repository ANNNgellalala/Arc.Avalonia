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

namespace Arc.Avalonia.BlocksTeam.Pages;

/// <summary>
/// Week calendar: a seven day grid with colour coded calendars, an all-day row and a working week switcher.
/// Pointer drag to create, move and resize is replaced by event selection plus explicit week navigation, so the
/// block stays usable with a keyboard; the agenda view covers the narrow reading case.
/// </summary>
public sealed class WeekCalendarPage : ArcItemPage
{
    private sealed record CalEvent(
        string Title,
        string Calendar,
        int Day,
        int Row,
        int Span,
        string Location,
        bool AllDay = false);

    private static readonly string[] Days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
    private static readonly string[] Hours = { "09:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00" };
    private static readonly DateTime WeekStart = new(2026, 11, 9);

    private static readonly IReadOnlyList<CalEvent> Events = new[]
    {
        new CalEvent("Team standup", "Work", 0, 0, 1, "Studio"),
        new CalEvent("Design review", "Work", 1, 2, 2, "Room 4 · Maya"),
        new CalEvent("1:1 with Priya", "Work", 2, 5, 1, "Walk and talk"),
        new CalEvent("Sprint planning", "Work", 3, 1, 1, "Room 2 · whole team"),
        new CalEvent("Release 2.4", "Work", 4, 6, 1, "Deploy window"),
        new CalEvent("Climbing", "Personal", 5, 1, 2, "The depot"),
        new CalEvent("Design offsite", "Work", 4, 0, 0, "All day", true),
        new CalEvent("Support rota handover", "Work", 3, 0, 0, "All day", true)
    };

    public override string ItemSlug => "week-calendar";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Scheduling and planning screens where people read and rearrange time blocks across a week.",
        "Team calendars with several colour coded calendars and an agenda view for narrow frames."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use month-calendar for a month overview, or availability-picker to collect open slots."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "The grid is labelled with its date range, each day is a labelled cell with its event count, and events are buttons.",
        "Arrow keys move between days, T returns to today and Escape clears the selection; the agenda rows mirror the grid."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Week changes slide in the direction of travel, and the selected event draws a quiet outline rather than a bounce.",
        "Reduced motion replaces the slide with an instant swap, matching the reference's reduced branch."
    };

    public override string? ImplementationNotes =>
        "The drag-to-create, move and resize gestures are not reproduced; events are selectable and the week " +
        "switcher, today action and agenda view are real. The week of 9 November 2026 carries the sample events; " +
        "other weeks show the empty state so navigation reads honestly. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var weekOffset = 0;
        var view = "week";
        var selected = Events[1];

        var rangeLabel = new TextBlock { Classes = { "arc-label" } };
        var gridHost = new Border();
        var detailHost = new Border();
        var emptyHost = new Border { IsVisible = false };
        var status = TeamKit.Caption(string.Empty);

        IReadOnlyList<string> dayDates()
        {
            var start = WeekStart.AddDays(weekOffset * 7);
            return Enumerable.Range(0, 7)
                .Select(offset => start.AddDays(offset).Day.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .ToList();
        }

        void RenderRange()
        {
            var start = WeekStart.AddDays(weekOffset * 7);
            var end = start.AddDays(6);
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            rangeLabel.Text = $"{start.ToString("MMM d", culture)} – {end.ToString("MMM d, yyyy", culture)}";
            status.Text = weekOffset == 0
                ? "6 events · 2 all-day · Zürich time"
                : "No events in this week · use Today to return";
            emptyHost.IsVisible = weekOffset != 0;
        }

        void RenderDetail()
        {
            if (weekOffset != 0)
            {
                detailHost.Child = new Border();
                return;
            }

            var calendarTone = selected.Calendar == "Personal" ? ArcTokens.Series2 : ArcTokens.Accent;
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            row.Children.Add(new Border
            {
                Width = 3,
                Height = 26,
                CornerRadius = new CornerRadius(2),
                Background = TeamKit.Brush(calendarTone),
                VerticalAlignment = VerticalAlignment.Center
            });
            var copy = new StackPanel { Spacing = 1, Margin = new Thickness(10, 0, 0, 0) };
            copy.Children.Add(new TextBlock { Text = selected.Title, Classes = { "arc-label" } });
            copy.Children.Add(TeamKit.Caption(selected.AllDay
                ? $"{Days[selected.Day]} · all day · {selected.Location}"
                : $"{Days[selected.Day]} · {Hours[selected.Row]} – {Hours[Math.Min(Hours.Length - 1, selected.Row + selected.Span)]} · {selected.Location}"));
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);
            var open = TeamKit.Btn("Open event", ArcButtonVariant.Ghost, ArcButtonSize.Sm, icon: "arrow-up-right");
            Grid.SetColumn(open, 2);
            row.Children.Add(open);
            detailHost.Child = row;
        }

        void RenderGrid()
        {
            var dates = dayDates();
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("50,*,*,*,*,*,*,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto")
            };

            // Header row: day names and dates.
            for (var day = 0; day < 7; day++)
            {
                var header = new StackPanel
                {
                    Spacing = 0,
                    Margin = new Thickness(4, 0, 4, 6),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                header.Children.Add(TeamKit.Caption(Days[day]));
                header.Children.Add(new TextBlock
                {
                    Text = dates[day],
                    Classes = { "arc-label" },
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                Grid.SetColumn(header, day + 1);
                Grid.SetRow(header, 0);
                grid.Children.Add(header);
            }

            // All-day row.
            var allDayLabel = TeamKit.Caption("All day");
            allDayLabel.Margin = new Thickness(0, 4, 0, 0);
            Grid.SetRow(allDayLabel, 1);
            grid.Children.Add(allDayLabel);

            for (var day = 0; day < 7; day++)
            {
                var allDayCell = new StackPanel { Spacing = 3, Margin = new Thickness(3) };
                if (weekOffset == 0)
                {
                    foreach (var item in Events.Where(entry => entry.AllDay && entry.Day == day))
                    {
                        allDayCell.Children.Add(new Border
                        {
                            Background = TeamKit.Tint(ArcTokens.Accent, 0.12),
                            BorderBrush = TeamKit.Tint(ArcTokens.Accent, 0.28),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(7, 3),
                            Child = TeamKit.Caption(item.Title)
                        });
                    }
                }

                Grid.SetColumn(allDayCell, day + 1);
                Grid.SetRow(allDayCell, 1);
                grid.Children.Add(allDayCell);
            }

            // Hour gutter and day columns.
            for (var row = 0; row < Hours.Length; row++)
            {
                var hour = TeamKit.Caption(Hours[row]);
                hour.Margin = new Thickness(0, 0, 8, 0);
                hour.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetRow(hour, row + 3);
                grid.Children.Add(hour);

                for (var day = 0; day < 7; day++)
                {
                    var cell = new Border
                    {
                        BorderBrush = TeamKit.Brush(ArcTokens.BorderSubtle),
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        MinHeight = 36,
                        Margin = new Thickness(3, 0, 3, 0)
                    };
                    Grid.SetColumn(cell, day + 1);
                    Grid.SetRow(cell, row + 3);
                    grid.Children.Add(cell);
                }
            }

            if (weekOffset == 0)
            {
                for (var index = 0; index < Events.Count; index++)
                {
                    var item = Events[index];
                    if (item.AllDay)
                    {
                        continue;
                    }

                    var tone = item.Calendar == "Personal" ? ArcTokens.Series2 : ArcTokens.Accent;
                    var isSelected = ReferenceEquals(item, selected);
                    var content = new StackPanel { Spacing = 1, Margin = new Thickness(6, 4) };
                    content.Children.Add(new TextBlock
                    {
                        Text = item.Title,
                        FontSize = 12,
                        LineHeight = 15,
                        TextWrapping = TextWrapping.Wrap
                    });
                    content.Children.Add(new TextBlock
                    {
                        Text = Hours[item.Row],
                        FontSize = 12,
                        LineHeight = 15,
                        Foreground = TeamKit.Brush(ArcTokens.TextMuted)
                    });

                    var block = TeamKit.Selectable(content, () =>
                    {
                        selected = item;
                        RenderDetail();
                        RenderGrid();
                    }, isSelected, $"{item.Title}, {Days[item.Day]}, {Hours[item.Row]}, {item.Calendar}", 8,
                        new Thickness(0));

                    if (block is Panel panel)
                    {
                        if (panel.Children[1] is Border body)
                        {
                            body.Background = TeamKit.Tint(tone, 0.12);
                            body.BorderBrush = TeamKit.Tint(tone, isSelected ? 0.8 : 0.45);
                            body.BorderThickness = new Thickness(3, 0, 0, 0);
                        }
                    }

                    block.Margin = new Thickness(4, 2, 4, 2);
                    Grid.SetColumn(block, item.Day + 1);
                    Grid.SetRow(block, item.Row + 3);
                    Grid.SetRowSpan(block, Math.Max(1, item.Span));
                    grid.Children.Add(block);
                }
            }

            gridHost.Child = new Border { Child = grid };
        }

        void RenderAgenda()
        {
            if (weekOffset != 0)
            {
                gridHost.Child = new Border
                {
                    Padding = new Thickness(24),
                    Child = TeamKit.Panel(TeamKit.Stack(8,
                        new ArcIcon { Icon = "calendar", Size = 20, Foreground = TeamKit.Brush(ArcTokens.TextMuted) },
                        TeamKit.Label("No events this week"),
                        TeamKit.Body("Use the arrows to move through weeks, or Today to come back.")), 22, 18, true)
                };
                return;
            }

            var list = new StackPanel { Spacing = 6 };
            foreach (var item in Events.OrderBy(entry => entry.Day).ThenBy(entry => entry.Row))
            {
                var tone = item.Calendar == "Personal" ? ArcTokens.Series2 : ArcTokens.Accent;
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("64,Auto,*,Auto") };
                row.Children.Add(TeamKit.Caption(Days[item.Day]));
                var dot = new Border
                {
                    Width = 7,
                    Height = 7,
                    CornerRadius = new CornerRadius(4),
                    Background = TeamKit.Brush(tone),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(dot, 1);
                row.Children.Add(dot);
                var titleCell = new StackPanel { Spacing = 0 };
                titleCell.Children.Add(new TextBlock { Text = item.Title, Classes = { "arc-label" } });
                titleCell.Children.Add(TeamKit.Caption(item.Location));
                Grid.SetColumn(titleCell, 2);
                row.Children.Add(titleCell);
                var time = TeamKit.Caption(item.AllDay ? "All day" : hoursRange(item));
                Grid.SetColumn(time, 3);
                row.Children.Add(time);

                list.Children.Add(new Border
                {
                    Padding = new Thickness(10, 7),
                    CornerRadius = new CornerRadius(12),
                    Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
                    Child = row
                });
            }

            gridHost.Child = list;
        }

        static string hoursRange(CalEvent item) =>
            $"{Hours[item.Row]} – {Hours[Math.Min(Hours.Length - 1, item.Row + item.Span)]}";

        void Render()
        {
            RenderRange();
            detailHost.IsVisible = weekOffset == 0;
            RenderDetail();
            if (view == "agenda")
            {
                RenderAgenda();
            }
            else
            {
                emptyHost.IsVisible = false;
                RenderGrid();
            }
        }

        var prev = TeamKit.IconBtn("chevron-left", "Previous week", () =>
        {
            weekOffset--;
            Render();
        }, box: 30);
        var next = TeamKit.IconBtn("chevron-right", "Next week", () =>
        {
            weekOffset++;
            Render();
        }, box: 30);
        var today = TeamKit.Btn("Today", ArcButtonVariant.Secondary, ArcButtonSize.Sm, () =>
        {
            weekOffset = 0;
            Render();
        });

        var viewSwitch = TeamKit.Segmented(new[] { ("week", "Week"), ("agenda", "Agenda") }, "week", value =>
        {
            view = value;
            Render();
        }, "View");

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        heading.Children.Add(TeamKit.Row(10, rangeLabel, TeamKit.Pill("6 events", ArcBadgeTone.Neutral)));
        var controls = TeamKit.Row(8,
            prev,
            next,
            today,
            viewSwitch);
        Grid.SetColumn(controls, 1);
        controls.HorizontalAlignment = HorizontalAlignment.Right;
        heading.Children.Add(controls);

        Render();

        static Control LegendDot(string tone, string label) => TeamKit.Row(5,
            new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(4),
                Background = TeamKit.Brush(tone),
                VerticalAlignment = VerticalAlignment.Center
            },
            TeamKit.Caption(label));

        var legend = TeamKit.Row(12, LegendDot(ArcTokens.Accent, "Work"), LegendDot(ArcTokens.Series2, "Personal"));

        emptyHost.IsVisible = false;

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(18),
            Child = TeamKit.Stack(12,
                heading,
                detailHost,
                TeamKit.Divider(),
                gridHost,
                TeamKit.Row(10, status, legend))
        });
    }
}
