using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIWidgets.Controls;

namespace Arc.Avalonia.Special.AIWidgets.Pages;

/// <summary>
/// Catalog page for activity rings. The demo is one week of three daily goals; picking a day sweeps the rings and
/// counts the numbers, and pointing at a goal in the legend isolates its ring.
/// </summary>
public sealed class ActivityRingsPage : ArcItemPage
{
    public override string ItemSlug => "activity-rings";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-below-goal", "state-mixed", "state-exceeded",
        "variant-week-picker", "variant-legend"
    };

    private static List<ArcRingMetric> Metrics() => new()
    {
        new() { Id = "move", Label = "Move", Unit = "kcal", Goal = 500 },
        new() { Id = "exercise", Label = "Exercise", Unit = "min", Goal = 30 },
        new() { Id = "stand", Label = "Stand", Unit = "hr", Goal = 12 }
    };

    private static List<ArcRingDay> Days() => new()
    {
        Day("mon", "Monday 12 May", "Mon", 420, 26, 9),
        Day("tue", "Tuesday 13 May", "Tue", 510, 34, 12),
        Day("wed", "Wednesday 14 May", "Wed", 380, 21, 8),
        Day("thu", "Thursday 15 May", "Thu", 545, 41, 13),
        Day("fri", "Friday 16 May", "Fri", 470, 30, 11),
        Day("sat", "Saturday 17 May", "Sat", 610, 48, 14),
        Day("sun", "Sunday 18 May", "Sun", 485, 33, 11)
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var mixed = new ArcActivityRings
        {
            Day = "sun",
            Label = "Move, exercise and stand",
            Metrics = Metrics(),
            Days = Days()
        };

        var below = new ArcActivityRings
        {
            Day = "wed",
            Label = "Move, exercise and stand",
            Metrics = Metrics(),
            Days = Days()
        };

        var exceeded = new ArcActivityRings
        {
            Day = "sat",
            Label = "Move, exercise and stand",
            Metrics = Metrics(),
            Days = Days()
        };

        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(WithCaption("部分达标：Exercise 与 Stand 已满，Move 还差一点", mixed));
        panel.Children.Add(WithCaption("全部未达标：三条环都有缺口，Goals reached 0 of 3", below));
        panel.Children.Add(WithCaption("全部超额：第二圈贴内缘再画，Goals reached 3 of 3", exceeded));
        return panel;
    }

    private static Control WithCaption(string caption, Control content)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(Sv.Caption(caption));
        stack.Children.Add(content);
        return stack;
    }

    private static ArcRingDay Day(string id, string label, string shortLabel, double move, double exercise, double stand) => new()
    {
        Id = id,
        Label = label,
        Short = shortLabel,
        Values = new Dictionary<string, double>
        {
            ["move"] = move,
            ["exercise"] = exercise,
            ["stand"] = stand
        }
    };

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Daily goals fill up and can be exceeded, like move, exercise and stand.",
        "A fitness or habit dashboard has a week picker to compare days.",
        "A summary card should show three related goals at once without three separate charts."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "For a single value against a range, use progress or a gauge.",
        "For quotas and plan limits, use a usage meter; for streaks, use an activity heatmap."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Arrow keys in the week picker pick the previous or next day; Home and End jump to the first or last day.",
        "Enter or Space on a legend row isolates that goal, and pressing again clears it.",
        "The rings read as one image whose label lists each goal's percent, and the legend rows are toggle buttons that speak the value and percent."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Every change sweeps from what is on screen to the new values, so a quick second change redirects mid flight.",
        "Past 100% a second lap traces the inside edge of the ring, so exceeding a goal reads differently from reaching it.",
        "Hovering or selecting a goal dims the others while the numbers count; reduced motion jumps straight to the values."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public activity-rings documentation. The rings are drawn in one control rather than SVG arcs driven by motion values, so a new " +
        "day sweeps the whole set together instead of each ring on its own spring. Ring colours come from the series tokens, so they follow the accent.";
}
