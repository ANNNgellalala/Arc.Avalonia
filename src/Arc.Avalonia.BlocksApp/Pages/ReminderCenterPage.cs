using Avalonia.Controls;
using Arc.Avalonia.BlocksApp.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.BlocksApp.Pages;

/// <summary>
/// Showcase for <see cref="ArcReminderCenter"/>. Normal, empty and error states are switchable; every
/// row action and the bulk delay are reported in a polite live region.
/// </summary>
public sealed class ReminderCenterPage : ArcPage
{
    public override string Title => "Reminder center";

    public override string Group => "P0 · Today";

    public override string? Description =>
        "The life action queue: overdue, today, upcoming and snoozed reminders with group counts, filters and the shared reminder row actions for complete, delay, reschedule and skip.";

    public override int Order => 20014;

    public override Control Build(ArcDemoContext context)
    {
        var center = new ArcReminderCenter();
        var status = TodayPageUi.StatusLine();

        var switcher = new TodayStateSwitch("Normal", "Empty", "Error");
        switcher.Selected += index =>
        {
            center.State = index switch
            {
                0 => ArcReminderCenterState.Normal,
                1 => ArcReminderCenterState.Empty,
                _ => ArcReminderCenterState.Error
            };
            TodayPageUi.Say(status, $"State: {switcher.Label(index)}");
        };

        center.CompleteRequested += (id, completed) => TodayPageUi.Say(status, completed
            ? $"{id} completed. The host writes the completion time and offers undo."
            : $"{id} restored to open. The host removes the completion time.");
        center.SnoozeRequested += (id, span) => TodayPageUi.Say(status,
            $"{id} delayed by {span.TotalMinutes:0} minutes. The host owns the new due time.");
        center.RescheduleRequested += id => TodayPageUi.Say(status,
            $"{id} reschedule requested. The host opens its date and time picker.");
        center.SkipRequested += id => TodayPageUi.Say(status,
            $"{id} skip requested. The host records the skipped occurrence.");
        center.SnoozeAllRequested += span => TodayPageUi.Say(status,
            $"Every overdue reminder delay requested ({span.TotalHours:0} hour). The host applies it in bulk.");
        center.AddRequested += () => TodayPageUi.Say(status,
            "Add requested. The host opens the capture sheet.");
        center.RetryRequested += () =>
        {
            center.State = ArcReminderCenterState.Normal;
            TodayPageUi.Say(status, "Retry requested. The demo shows the grouped reminders again.");
        };

        switcher.Select(0);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(switcher);
        content.Children.Add(center);
        content.Children.Add(status);
        content.Children.Add(TodayPageUi.Hint(
            "Grouping follows the host's local day. Snooze options are intents with an explicit span, not a built-in scheduler; the quiet-hours line names the policy owner."));

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section("Reminder center",
                    "A workbench rather than a notification history: every group has a count, every row can be delayed to a known time, and the empty and error states still give a next step.",
                    content)
            }
        };
    }
}
