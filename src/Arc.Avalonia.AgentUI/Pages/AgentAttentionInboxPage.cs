using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.AgentUI.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.AgentUI.Pages;

/// <summary>
/// Gallery page for the attention inbox. The state buttons show the full queue, an approvals-only view and the
/// empty state; opening or dismissing an item updates the local sample list and reports the intent underneath,
/// the way the host would route it to the matching approval or question surface.
/// </summary>
public sealed class AgentAttentionInboxPage : ArcPage
{
    public override string Title => "Agent attention inbox";

    public override string Group => "P0 · Agent UI";

    public override string? Description =>
        "The \"waiting on you\" queue for a desktop agent: approvals, questions, failed runs, finished routines and suggestions in one list, sorted by how much they block.";

    public override int Order => 20005;

    public override Control Build(ArcDemoContext context)
    {
        var inbox = new ArcAgentAttentionInbox
        {
            Width = 840,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var log = DemoUi.Caption("Item actions appear here; the host routes them to the approval sheet, the question card or a recovery flow.");

        var approvalsOnly = false;
        var current = SampleItems();

        void Push()
        {
            var visible = approvalsOnly
                ? current.Where(item => item.Kind == ArcAttentionKind.Approval).ToList()
                : current;
            inbox.Items = visible.ToList();
        }

        inbox.ItemActionRequested += (_, id) =>
        {
            var item = current.FirstOrDefault(candidate => candidate.Id == id);
            if (item is null)
            {
                return;
            }

            log.Text = $"ItemActionRequested · {id} · the host opens the {KindWord(item.Kind)} surface for \"{item.Title}\".";
            current = current
                .Select(candidate => candidate.Id == id
                    ? new ArcAttentionItem
                    {
                        Id = candidate.Id,
                        Kind = candidate.Kind,
                        Title = candidate.Title,
                        Summary = candidate.Summary,
                        Risk = candidate.Risk,
                        Age = candidate.Age,
                        ActionLabel = candidate.ActionLabel,
                        Status = ArcAttentionItemStatus.Resolved
                    }
                    : candidate)
                .ToList();
            Push();
        };

        inbox.Dismissed += (_, id) =>
        {
            current = current.Where(candidate => candidate.Id != id).ToList();
            log.Text = $"Dismissed · {id} · the host removes it from the queue without deciding.";
            Push();
        };

        void Apply(string state)
        {
            approvalsOnly = state == "approvals";
            switch (state)
            {
                case "approvals":
                    current = SampleItems();
                    log.Text = "Approvals only · the same block filtered by kind; counts follow the host data.";
                    break;
                case "empty":
                    current = new List<ArcAttentionItem>();
                    log.Text = "Empty · every item is handled, so the block shows the caught-up state.";
                    break;
                default:
                    current = SampleItems();
                    log.Text = "All items · approvals come first because they block the longest.";
                    break;
            }

            Push();
        }

        var buttons = DemoUi.Row(
            StateButton("Waiting on you", "all", Apply),
            StateButton("Approvals only", "approvals", Apply),
            StateButton("All clear", "empty", Apply));

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                buttons,
                inbox,
                log
            }
        };

        Apply("all");

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Agent attention inbox",
                    "The block can sit in a sidebar, a drawer or a full page. It renders the queue and raises one intent per row; the host keeps the counts, routes the decision and clears the item.",
                    content)
            }
        };
    }

    private static Control StateButton(string label, string state, Action<string> apply)
    {
        var button = new ArcButton
        {
            Content = label,
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        ArcUi.Name(button, $"{label} inbox state");
        button.Click += (_, _) => apply(state);
        return button;
    }

    private static string KindWord(ArcAttentionKind kind) => kind switch
    {
        ArcAttentionKind.Approval => "approval",
        ArcAttentionKind.Ask => "question",
        ArcAttentionKind.Failure => "recovery",
        ArcAttentionKind.Routine => "routine review",
        _ => "suggestion"
    };

    private static List<ArcAttentionItem> SampleItems() => new()
    {
        new ArcAttentionItem
        {
            Id = "inbox-1",
            Kind = ArcAttentionKind.Approval,
            Title = "Approve the electricity payment",
            Summary = "84.20 EUR from Household · checking; the bill is due tomorrow.",
            Risk = ArcAttentionRisk.High,
            Age = "4m",
            ActionLabel = "Review"
        },
        new ArcAttentionItem
        {
            Id = "inbox-2",
            Kind = ArcAttentionKind.Approval,
            Title = "Allow calendar changes for this task",
            Summary = "The agent wants to move two appointments and add a reminder.",
            Risk = ArcAttentionRisk.Medium,
            Age = "12m",
            ActionLabel = "Approve"
        },
        new ArcAttentionItem
        {
            Id = "inbox-3",
            Kind = ArcAttentionKind.Ask,
            Title = "Which account should the budget read?",
            Summary = "Three accounts match the request; the rollup needs one answer.",
            Risk = ArcAttentionRisk.Low,
            Age = "18m",
            ActionLabel = "Answer"
        },
        new ArcAttentionItem
        {
            Id = "inbox-4",
            Kind = ArcAttentionKind.Failure,
            Title = "Routine failed: weekly meal plan",
            Summary = "The grocery site changed its layout; retry after the fix or record the plan by hand.",
            Risk = ArcAttentionRisk.Medium,
            Age = "1h",
            ActionLabel = "Retry"
        },
        new ArcAttentionItem
        {
            Id = "inbox-5",
            Kind = ArcAttentionKind.Routine,
            Title = "Morning brief ran while you were away",
            Summary = "Three reminders moved and two events need a decision.",
            Risk = ArcAttentionRisk.Low,
            Age = "2h",
            ActionLabel = "Review",
            Status = ArcAttentionItemStatus.Seen
        },
        new ArcAttentionItem
        {
            Id = "inbox-6",
            Kind = ArcAttentionKind.Suggestion,
            Title = "Move the dentist reminder to Thursday",
            Summary = "Thursday 15:00 is free; the agent can book it before the next reminder fires.",
            Risk = ArcAttentionRisk.Low,
            Age = "3h",
            ActionLabel = "Adopt"
        }
    };
}
