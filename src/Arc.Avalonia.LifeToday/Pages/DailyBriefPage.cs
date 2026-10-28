using Avalonia.Controls;
using Arc.Avalonia.LifeToday.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.LifeToday.Pages;

/// <summary>
/// Showcase for <see cref="ArcDailyBrief"/>. One instance is switched through its four display states;
/// every suggestion action reports its intent in a polite live region.
/// </summary>
public sealed class DailyBriefPage : ArcPage
{
    public override string Title => "Daily brief";

    public override string Group => "P0 · Today";

    public override string? Description =>
        "The morning screen: greeting and weather, the next appointment, due reminders, health and money snapshots, and exactly one suggestion the host can accept, ignore or snooze.";

    public override int Order => 20011;

    public override Control Build(ArcDemoContext context)
    {
        var brief = new ArcDailyBrief();
        var status = TodayPageUi.StatusLine();

        var switcher = new TodayStateSwitch("Ready", "Partly loading", "Error");
        switcher.Selected += index =>
        {
            brief.State = index switch
            {
                0 => ArcDailyBriefState.Ready,
                1 => ArcDailyBriefState.PartiallyLoading,
                _ => ArcDailyBriefState.Error
            };
            TodayPageUi.Say(status, $"State: {switcher.Label(index)}");
        };

        brief.AcceptSuggestionRequested += () =>
        {
            brief.SuggestionState = ArcSuggestionState.Accepted;
            TodayPageUi.Say(status, "Suggestion accepted. The host would add it to today's plan.");
        };
        brief.IgnoreSuggestionRequested += () =>
        {
            brief.SuggestionState = ArcSuggestionState.Dismissed;
            TodayPageUi.Say(status, "Suggestion dismissed for today. No data was changed.");
        };
        brief.SnoozeSuggestionRequested += () =>
        {
            brief.SuggestionState = ArcSuggestionState.Snoozed;
            TodayPageUi.Say(status, "Suggestion snoozed to 18:00. The host schedules the reminder.");
        };
        brief.OpenAgendaRequested += () => TodayPageUi.Say(status, "Open agenda requested.");
        brief.OpenRemindersRequested += () => TodayPageUi.Say(status, "Open reminders requested.");
        brief.RetryRequested += () =>
        {
            brief.State = ArcDailyBriefState.Ready;
            TodayPageUi.Say(status, "Retry requested. The demo shows the last copy.");
        };

        switcher.Select(0);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(switcher);
        content.Children.Add(brief);
        content.Children.Add(status);
        content.Children.Add(TodayPageUi.Hint(
            "Display only: the brief never aggregates data, fetches weather or calls the agent. Accept, Ignore and Snooze are intent events, and the deterministic sample means screenshots do not depend on the clock."));

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section("Daily brief",
                    "Open the app and know what today needs in a few seconds. The single suggestion is marked with its source and can be refused, which keeps the agent accountable.",
                    content)
            }
        };
    }
}
