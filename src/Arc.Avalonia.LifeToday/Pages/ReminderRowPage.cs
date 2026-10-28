using System;
using Avalonia.Automation;
using Avalonia.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.LifeToday.Controls;

// The Feedback assembly contains an internal FeedbackUi helper too, so the shared foundation helpers are
// aliased explicitly in this assembly.
using Ui = Arc.Avalonia.Controls.ArcUi;

namespace Arc.Avalonia.LifeToday.Pages;

/// <summary>
/// Showcase for <see cref="ArcReminderRow"/>: the atomic life action row used by the reminder center,
/// the day agenda and the medication schedule. The page renders four representative rows and reports
/// every intent event in a polite live region instead of changing any data.
/// </summary>
public sealed class ReminderRowPage : ArcPage
{
    public override string Title => "Reminder row";

    public override string Group => "P0 · Today";

    public override string? Description =>
        "The atomic life action: a due badge with relative copy, repeat and source markers, a completion control and an inline strip for delay, reschedule and skip. Actions are intent events only.";

    public override int Order => 20013;

    public override Control Build(ArcDemoContext context)
    {
        var status = Ui.Caption(string.Empty);
        status.IsVisible = false;
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);

        ArcReminderRow Row(string title, string due, ArcReminderDueState state, string repeat, string source,
            string? note = null, bool expanded = false, bool done = false)
        {
            var row = new ArcReminderRow
            {
                Title = title,
                DueText = due,
                DueState = state,
                RepeatLabel = repeat,
                SourceLabel = source,
                Note = note,
                IsExpanded = expanded,
                IsChecked = done
            };

            row.CompletedChanged += value => Say(status, value
                ? $"{title} marked done. The host would write the completion time and offer undo."
                : $"{title} restored to open.");
            row.SnoozeRequested += span => Say(status,
                $"{title} delay requested ({FormatSpan(span)}). The host owns the new due time.");
            row.RescheduleRequested += () => Say(status,
                $"Reschedule requested for {title}. The host opens its date and time picker.");
            row.SkipRequested += () => Say(status,
                $"Skip requested for {title}. The host records the skipped occurrence.");
            return row;
        }

        var rows = new StackPanel { Spacing = 10 };
        rows.Children.Add(Row("Take the recycling out", "Today · 18:00", ArcReminderDueState.Due,
            "Every Wednesday", "Home",
            "Comes from the household routine. Delay options are local intents only.", expanded: true));
        rows.Children.Add(Row("Pay the credit card", "2 days overdue · ¥1,240", ArcReminderDueState.Overdue,
            "Monthly", "Bills"));
        rows.Children.Add(Row("Metformin 500 mg", "Taken 08:02", ArcReminderDueState.Done, "Daily", "Health",
            done: true));

        var note = Ui.Caption(
            "States shown: due, overdue and done. The first row starts expanded; the trailing control or the M key toggles the action strip. Upcoming, snoozed and missed are the other values of the same enum.",
            760);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(rows);
        content.Children.Add(status);
        content.Children.Add(note);

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section("Reminder row",
                    "One row for a reminder, bill, dose, chore or renewal. The row never schedules or notifies: it renders the host's state and raises completion, delay, reschedule and skip intents.",
                    content)
            }
        };
    }

    private static void Say(TextBlock line, string text)
    {
        line.Text = text;
        line.IsVisible = !string.IsNullOrWhiteSpace(text);
    }

    private static string FormatSpan(TimeSpan span) => span.TotalDays >= 1
        ? "tomorrow"
        : span.TotalHours >= 1
            ? "1 hour"
            : "10 minutes";
}
