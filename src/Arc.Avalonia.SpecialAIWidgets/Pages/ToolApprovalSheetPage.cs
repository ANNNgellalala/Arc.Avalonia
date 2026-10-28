using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialAIWidgets.Controls;

namespace Arc.Avalonia.SpecialAIWidgets.Pages;

/// <summary>
/// Gallery page for the tool approval sheet. Three requests show a high-risk payment, a low-risk calendar
/// change and an expired request; approve, deny and delegate report their decision in the line underneath.
/// </summary>
public sealed class ToolApprovalSheetPage : ArcPage
{
    public override string Title => "Tool approval sheet";

    public override string Group => "P0 · Agent UI";

    public override string? Description =>
        "A pre-execution approval surface: intent, editable arguments, side-effect preview, risk note, expiry countdown, scope choice and the approve, deny and delegate decisions.";

    public override int Order => 20003;

    public override Control Build(ArcDemoContext context)
    {
        var sheet = new ArcToolApprovalSheet { Width = 720, HorizontalAlignment = HorizontalAlignment.Left };
        var log = DemoUi.Caption("Decisions appear here; the host applies the call and writes the audit entry.");

        sheet.ApproveRequested += (_, decision) =>
        {
            sheet.State = ArcApprovalSheetState.Decided;
            log.Text = $"ApproveRequested · scope {decision.Scope} · {decision.ModifiedArguments.Count} arguments. The host applies the call and audits it.";
        };
        sheet.DenyRequested += (_, reason) =>
        {
            sheet.State = ArcApprovalSheetState.Cancelled;
            log.Text = $"DenyRequested · \"{reason}\" · nothing is executed and the agent can offer another way.";
        };
        sheet.DelegateRequested += (_, _) =>
            log.Text = "DelegateRequested · the host routes the decision to another person and keeps the run blocked.";

        void Apply(string state)
        {
            switch (state)
            {
                case "low":
                    sheet.ToolName = "calendar.create_event";
                    sheet.Intent = "Book the dentist appointment for Thursday at 15:00 and invite Sam.";
                    sheet.Risk = ArcApprovalRisk.Low;
                    sheet.Arguments = CalendarArguments();
                    sheet.PreviewLines = CalendarPreview();
                    sheet.ExpiresInSeconds = 300;
                    sheet.Scope = ArcApprovalScope.Once;
                    sheet.State = ArcApprovalSheetState.Checkout;
                    log.Text = "Low risk · the change is a single calendar event and can be undone from the run diff.";
                    break;
                case "expired":
                    sheet.ToolName = "mail.send";
                    sheet.Intent = "Send the signed lease to the landlord and copy the agency.";
                    sheet.Risk = ArcApprovalRisk.Medium;
                    sheet.Arguments = MailArguments();
                    sheet.PreviewLines = MailPreview();
                    sheet.ExpiresInSeconds = 0;
                    sheet.State = ArcApprovalSheetState.Expired;
                    log.Text = "Expired · the countdown reached zero, so Approve stays disabled; the agent must ask again.";
                    break;
                default:
                    sheet.ToolName = "payments.transfer";
                    sheet.Intent = "Pay the electricity bill of 84.20 EUR from the household account.";
                    sheet.Risk = ArcApprovalRisk.High;
                    sheet.Arguments = PaymentArguments();
                    sheet.PreviewLines = PaymentPreview();
                    sheet.ExpiresInSeconds = 144;
                    sheet.Scope = ArcApprovalScope.Once;
                    sheet.State = ArcApprovalSheetState.Checkout;
                    log.Text = "High risk · a payment cannot be undone automatically, so the whole preview stays visible.";
                    break;
            }
        }

        var buttons = DemoUi.Row(
            StateButton("High-risk payment", "high", Apply),
            StateButton("Low-risk calendar", "low", Apply),
            StateButton("Expired request", "expired", Apply));

        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                buttons,
                sheet,
                log
            }
        };

        Apply("high");

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Tool approval sheet",
                    "Approving raises the decision with the edited arguments and the chosen scope; the sheet never runs the tool itself. Use a page host or a drawer, and let the host add a system notification when the window is not focused.",
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
        ArcUi.Name(button, $"{label} approval state");
        button.Click += (_, _) => apply(state);
        return button;
    }

    private static IReadOnlyList<ArcApprovalArgument> PaymentArguments() => new List<ArcApprovalArgument>
    {
        new() { Name = "from", Value = "Household · checking (·· 4021)", Editable = false },
        new() { Name = "to", Value = "Northwind Power", Editable = false },
        new() { Name = "amount", Value = "84.20 EUR", Editable = true },
        new() { Name = "reference", Value = "Invoice 2026-1042", Editable = true }
    };

    private static IReadOnlyList<string> PaymentPreview() => new[]
    {
        "Withdraws 84.20 EUR from Household · checking",
        "Sends one payment to Northwind Power today",
        "Marks invoice 2026-1042 as paid in the ledger"
    };

    private static IReadOnlyList<ArcApprovalArgument> CalendarArguments() => new List<ArcApprovalArgument>
    {
        new() { Name = "title", Value = "Dentist", Editable = true },
        new() { Name = "start", Value = "Thu 15:00 (Europe/Berlin)", Editable = true },
        new() { Name = "duration", Value = "45 minutes", Editable = true },
        new() { Name = "attendee", Value = "sam@northwind.example", Editable = false }
    };

    private static IReadOnlyList<string> CalendarPreview() => new[]
    {
        "Creates Calendar event \"Dentist\" on Thu 15:00-15:45",
        "Sends an invite to sam@northwind.example"
    };

    private static IReadOnlyList<ArcApprovalArgument> MailArguments() => new List<ArcApprovalArgument>
    {
        new() { Name = "to", Value = "landlord@harbour.example", Editable = true },
        new() { Name = "cc", Value = "agency@harbour.example", Editable = true },
        new() { Name = "subject", Value = "Signed lease · 14 Harbour Street", Editable = true },
        new() { Name = "attachment", Value = "lease-signed.pdf (1.2 MB)", Editable = false }
    };

    private static IReadOnlyList<string> MailPreview() => new[]
    {
        "Sends one email to landlord@harbour.example",
        "Copies agency@harbour.example and attaches lease-signed.pdf",
        "Keeps a copy in the Documents folder and the run audit"
    };
}
