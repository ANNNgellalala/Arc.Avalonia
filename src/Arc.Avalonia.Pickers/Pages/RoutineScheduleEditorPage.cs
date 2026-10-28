using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// The routine schedule editor. The page plays the host: the state row drives the badge, and Save raises the
/// structured spec shown below. Nothing is scheduled here, which is the point of the contract.
/// </summary>
public sealed class RoutineScheduleEditorPage : ArcPage
{
    public override string Title => "Routine schedule editor";

    public override string Group => "P0 · Foundation";

    public override string? Description =>
        "Repeat, days, time, time zone and the missed-run and overlap policies, with a live next-run preview " +
        "and the RRULE the host will store. The editor validates and reports; scheduling stays with the host.";

    public override int Order => 20023;

    public override Control Build(ArcDemoContext context)
    {
        var editor = new ArcRoutineScheduleEditor
        {
            Width = 720,
            Repeat = ArcRoutineRepeat.Weekly,
            SelectedDays = new List<DayOfWeek>
            {
                DayOfWeek.Monday,
                DayOfWeek.Wednesday,
                DayOfWeek.Friday
            },
            TimeText = "08:00",
            MisfirePolicy = ArcRoutineMisfirePolicy.RunOnce,
            OverlapPolicy = ArcRoutineOverlapPolicy.Skip
        };

        var saved = ArcUi.Text("No save yet. Press Save schedule to raise SaveRequested with the spec.", "arc-mono");
        saved.TextWrapping = TextWrapping.Wrap;
        saved.MaxWidth = 720;
        var cancelled = ArcUi.Caption("Cancel has not been used.");

        editor.SaveRequested += spec =>
        {
            saved.Text = Describe(spec);
            cancelled.Text = "Saved. The host would now persist the rule and schedule it.";
        };
        editor.CancelRequested += (_, _) =>
            cancelled.Text = "CancelRequested \u00b7 the host would close the editor and keep the previous rule.";

        var outputCard = new ArcCard
        {
            Padding = new Thickness(16),
            Content = ArcUi.Stack(8, ArcUi.Label("Event output"), saved, cancelled)
        };

        var stateRow = new WrapPanel { Orientation = Orientation.Horizontal };
        var hint = ArcUi.Caption("Display state:");
        hint.Margin = new Thickness(0, 0, 10, 0);
        hint.VerticalAlignment = VerticalAlignment.Center;
        stateRow.Children.Add(hint);
        foreach (var (label, state) in new (string, ArcRoutineEditorState)[]
                 {
                     ("Viewing", ArcRoutineEditorState.Viewing),
                     ("Editing", ArcRoutineEditorState.Editing),
                     ("Validating", ArcRoutineEditorState.Validating),
                     ("Saved", ArcRoutineEditorState.Saved),
                     ("Error", ArcRoutineEditorState.Error)
                 })
        {
            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Content = label,
                Margin = new Thickness(0, 0, 6, 6)
            };
            ArcUi.Name(button, $"Set editor state to {label}");
            button.Click += (_, _) =>
            {
                editor.ValidationMessage = state == ArcRoutineEditorState.Error
                    ? "The host could not resolve \"Europe/Berlin\" for this rule. Pick a zone again."
                    : string.Empty;
                editor.State = state;
            };
            stateRow.Children.Add(button);
        }

        var live = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { editor, stateRow }
        };

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Routine schedule editor",
                    "The repeat buttons change the rule shape, the weekday row appears for weekly and custom " +
                    "rules, and the preview recomputes from the static sample data. The time picker and the " +
                    "fields stay editable; the host owns the time zone and the actual scheduler.",
                    live),

                DemoUi.Section(
                    "What the host receives",
                    "The control only produces a display contract: repeat, time, time zone, days, the two " +
                    "policies and a readable RRULE. Daylight saving, persistence and missed-run recovery are " +
                    "the host's responsibility.",
                    outputCard)
            }
        };
    }

    private static string Describe(ArcScheduleSpec spec)
    {
        var days = spec.Days.Count == 0
            ? "no explicit days"
            : string.Join(", ", spec.Days.Select(day => day.ToString()));
        var time = new DateTime(2000, 1, 1).Add(ParseTime(spec.TimeText))
            .ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));

        return $"SaveRequested \u00b7 {spec.Repeat} \u00b7 {time}\n" +
               $"RRULE {spec.Rrule} \u00b7 {days} \u00b7 monthly day {spec.MonthlyDay}\n" +
               $"Misfire {spec.MisfirePolicy} \u00b7 Overlap {spec.OverlapPolicy} \u00b7 {spec.TimeZone}";
    }

    private static TimeSpan ParseTime(string text) =>
        TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var time) ? time : TimeSpan.Zero;
}
