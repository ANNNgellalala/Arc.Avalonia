using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.AgentUI.Controls;

namespace Arc.Avalonia.AgentUI.Pages;

/// <summary>
/// Gallery page for the structured agent question. Three cards show a single-choice date question, a
/// multi-select account question with free text, and a deferred question; submissions are reported in the
/// line underneath.
/// </summary>
public sealed class AgentAskCardPage : ArcPage
{
    public override string Title => "Agent question card";

    public override string Group => "P0 · Agent UI";

    public override string? Description =>
        "A structured question from an agent: one question, selectable options, optional free text and an answer-later path, with a submitted answer returned as a payload.";

    public override int Order => 20004;

    public override Control Build(ArcDemoContext context)
    {
        var card = new ArcAgentAskCard { Width = 640, HorizontalAlignment = HorizontalAlignment.Left };
        var log = DemoUi.Caption("Submitted answers appear here; the host writes them back to the run.");

        card.Submitted += (_, answer) =>
        {
            card.State = ArcAgentAskState.Answered;
            var ids = answer.SelectedIds.Count == 0 ? "none" : string.Join(", ", answer.SelectedIds);
            log.Text = $"Submitted · options [{ids}] · free text: {answer.FreeText ?? "none"} · State switches to Answered.";
        };
        card.DeferredRequested += (_, _) =>
        {
            card.State = ArcAgentAskState.Deferred;
            log.Text = "DeferredRequested · the host keeps the run blocked and asks again later.";
        };

        void Apply(string state)
        {
            switch (state)
            {
                case "accounts":
                    card.Question = "Which accounts should the budget rollup read this month?";
                    card.Options = AccountOptions();
                    card.AllowMultiple = true;
                    card.AllowFreeText = true;
                    card.State = ArcAgentAskState.Question;
                    log.Text = "Multiple selection · choose any number of accounts, or type your own.";
                    break;
                case "deferred":
                    card.Question = "Which day should I move the dentist appointment to?";
                    card.Options = DayOptions();
                    card.AllowMultiple = false;
                    card.AllowFreeText = true;
                    card.State = ArcAgentAskState.Deferred;
                    log.Text = "Deferred · the card keeps the question but stops asking until the host reopens it.";
                    break;
                default:
                    card.Question = "Which day should I move the dentist appointment to?";
                    card.Options = DayOptions();
                    card.AllowMultiple = false;
                    card.AllowFreeText = true;
                    card.State = ArcAgentAskState.Question;
                    log.Text = "Single choice · one option, or a free text answer if none of the slots fit.";
                    break;
            }
        }

        var buttons = DemoUi.Row(
            StateButton("Choose a day", "day", Apply),
            StateButton("Pick accounts", "accounts", Apply),
            StateButton("Deferred", "deferred", Apply));

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                buttons,
                card,
                log
            }
        };

        Apply("day");

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Agent question card",
                    "Use when a text answer would be ambiguous: pick a date, an account, a file or one of two paths. Submitting raises the structured answer; the card never calls the agent and never changes its own state.",
                    content)
            }
        };
    }

    private static Control StateButton(string label, string state, System.Action<string> apply)
    {
        var button = new ArcButton
        {
            Content = label,
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        ArcUi.Name(button, $"{label} question state");
        button.Click += (_, _) => apply(state);
        return button;
    }

    private static IReadOnlyList<ArcAgentAskOption> DayOptions() => new[]
    {
        new ArcAgentAskOption
        {
            Id = "thu",
            Label = "Thursday at 15:00",
            Description = "Keeps the morning free for the budget review.",
            Icon = "calendar"
        },
        new ArcAgentAskOption
        {
            Id = "fri",
            Label = "Friday at 9:30",
            Description = "Earliest slot after the dentist's next opening.",
            Icon = "calendar"
        },
        new ArcAgentAskOption
        {
            Id = "monday",
            Label = "Next Monday at 11:00",
            Description = "Pushes the reminder out by four days.",
            Icon = "calendar"
        }
    };

    private static IReadOnlyList<ArcAgentAskOption> AccountOptions() => new[]
    {
        new ArcAgentAskOption
        {
            Id = "household",
            Label = "Household · checking (··4021)",
            Description = "Groceries, utilities and the mortgage.",
            Icon = "credit-card"
        },
        new ArcAgentAskOption
        {
            Id = "savings",
            Label = "Household · savings (··8890)",
            Description = "Read only; the rollup does not need it for the totals.",
            Icon = "credit-card"
        },
        new ArcAgentAskOption
        {
            Id = "business",
            Label = "Studio · business (··1174)",
            Description = "Separate ledger, usually excluded from the household budget.",
            Icon = "credit-card"
        }
    };
}
