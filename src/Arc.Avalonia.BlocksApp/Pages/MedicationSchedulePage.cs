using Avalonia.Controls;
using Arc.Avalonia.BlocksApp.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.BlocksApp.Pages;

/// <summary>
/// Showcase for <see cref="ArcMedicationSchedule"/>. Normal, paused and empty states are switchable; dose
/// check-off, undo, refill and pause are intent events only.
/// </summary>
public sealed class MedicationSchedulePage : ArcPage
{
    public override string Title => "Medication schedule";

    public override string Group => "P0 · Today";

    public override string? Description =>
        "Today's doses by time with check-off, an explicit missed state, weekly adherence, refill supply and a pause switch. Health data stays with the host; the control only displays it and raises intents.";

    public override int Order => 20015;

    public override Control Build(ArcDemoContext context)
    {
        var schedule = new ArcMedicationSchedule();
        var status = TodayPageUi.StatusLine();

        var switcher = new TodayStateSwitch("Normal", "Paused", "Empty");
        switcher.Selected += index =>
        {
            schedule.State = index switch
            {
                0 => ArcMedicationScheduleState.Normal,
                1 => ArcMedicationScheduleState.Paused,
                _ => ArcMedicationScheduleState.Empty
            };
            TodayPageUi.Say(status, $"State: {switcher.Label(index)}");
        };

        schedule.TakeRequested += id => TodayPageUi.Say(status,
            $"{id} marked taken. The host writes the dose log and offers undo.");
        schedule.UndoTakenRequested += id => TodayPageUi.Say(status,
            $"{id} undo requested. The host reverses the dose log entry.");
        schedule.SnoozeRequested += (id, span) => TodayPageUi.Say(status,
            $"{id} delayed by {span.TotalMinutes:0} minutes. The host owns the new reminder time.");
        schedule.SkipRequested += id => TodayPageUi.Say(status,
            $"{id} skipped for today. The host records the skipped dose.");
        schedule.RefillRequested += () => TodayPageUi.Say(status,
            "Refill requested. The host opens the pharmacy flow.");
        schedule.PauseRequested += () => TodayPageUi.Say(status,
            "Pause requested. The host suspends the plan; no doses are expected.");
        schedule.ResumeRequested += () => TodayPageUi.Say(status,
            "Resume requested. The host reactivates the plan.");
        schedule.EditPlanRequested += () => TodayPageUi.Say(status,
            "Edit plan requested. The host opens the plan editor.");
        schedule.AddRequested += () => TodayPageUi.Say(status,
            "Add requested. The host opens the medication form.");

        switcher.Select(0);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(switcher);
        content.Children.Add(schedule);
        content.Children.Add(status);
        content.Children.Add(TodayPageUi.Hint(
            "No medical advice, no dose inference and no reminder delivery live here. Missed doses are shown as missed with an explicit label, and the refill bar warns below the threshold set by the host."));

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section("Medication schedule",
                    "A check-off timetable with a visible missed state, weekly adherence and remaining supply. Pausing the plan removes the per-dose actions instead of hiding the history.",
                    content)
            }
        };
    }
}
