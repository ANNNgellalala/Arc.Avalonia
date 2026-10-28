using Avalonia.Controls;
using Arc.Avalonia.BlocksApp.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.BlocksApp.Pages;

/// <summary>
/// Showcase for <see cref="ArcDayAgenda"/>. The page switches between the populated, conflict and empty
/// states; navigation and item activation are reported as intents.
/// </summary>
public sealed class DayAgendaPage : ArcPage
{
    public override string Title => "Day agenda";

    public override string Group => "P0 · Today";

    public override string? Description =>
        "Events, reminders, tasks and medication on one hour grid: an all-day band, a thin now line and overlap badges, with navigation and activation as intent events.";

    public override int Order => 20012;

    public override Control Build(ArcDemoContext context)
    {
        var agenda = new ArcDayAgenda();
        var status = TodayPageUi.StatusLine();

        var switcher = new TodayStateSwitch("Populated", "Conflict", "Empty");
        switcher.Selected += index =>
        {
            agenda.State = index switch
            {
                0 => ArcDayAgendaState.Populated,
                1 => ArcDayAgendaState.Conflict,
                _ => ArcDayAgendaState.Empty
            };
            TodayPageUi.Say(status, $"State: {switcher.Label(index)}");
        };

        agenda.PreviousDayRequested += () => TodayPageUi.Say(status,
            "Previous day requested. The host resolves the date and supplies new items.");
        agenda.NextDayRequested += () => TodayPageUi.Say(status,
            "Next day requested. The host resolves the date and supplies new items.");
        agenda.TodayRequested += () => TodayPageUi.Say(status, "Jump to today requested.");
        agenda.ItemActivated += id => TodayPageUi.Say(status,
            $"Item {id} activated. The host opens its detail sheet.");
        agenda.AddRequested += () => TodayPageUi.Say(status,
            "Add requested. The host opens the capture sheet.");
        agenda.RetryRequested += () =>
        {
            agenda.State = ArcDayAgendaState.Populated;
            TodayPageUi.Say(status, "Retry requested. The demo shows the populated day again.");
        };

        switcher.Select(0);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(switcher);
        content.Children.Add(agenda);
        content.Children.Add(status);
        content.Children.Add(TodayPageUi.Hint(
            "The now line is a static display line, not a timer. Date navigation, item activation and retry are intents; the control never reorders overlapping items."));

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section("Day agenda",
                    "One timeline for the four things a day contains. Kind is encoded by a dot plus a word, so colour is never the only signal; conflicts are flagged, never moved.",
                    content)
            }
        };
    }
}
