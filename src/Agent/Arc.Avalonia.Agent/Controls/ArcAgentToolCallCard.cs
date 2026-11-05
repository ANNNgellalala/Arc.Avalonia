using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.AgentUI.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Agent;

/// <summary>
/// One tool call's card: tool name, intent, risk, streamed argument preview and the approve/deny/retry intents.
/// It renders a projection; the host owns execution and the approval policy.
/// </summary>
public class ArcAgentToolCallCard : Panel
{
    private readonly StackPanel _body = ArcUi.Stack(6);
    private ArcToolCall? _call;

    /// <summary>Creates the card.</summary>
    public ArcAgentToolCallCard()
    {
        Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(12),
            Child = _body
        });
        Rebuild();
    }

    /// <summary>The call being rendered.</summary>
    public ArcToolCall? Call
    {
        get => _call;
        set
        {
            _call = value;
            Rebuild();
        }
    }

    /// <summary>Raised when the person approves the call (the host still performs it).</summary>
    public event EventHandler<string>? ApproveRequested;

    /// <summary>Raised when the person denies the call.</summary>
    public event EventHandler<string>? DenyRequested;

    /// <summary>Raised when the person retries a failed call.</summary>
    public event EventHandler<string>? RetryRequested;

    /// <summary>Raised when the person asks to open the call details.</summary>
    public event EventHandler<string>? DetailsRequested;

    private void Rebuild()
    {
        _body.Children.Clear();
        if (_call is null)
        {
            _body.Children.Add(ArcUi.Caption("No tool call selected."));
            return;
        }

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
        header.Children.Add(new ArcIcon
        {
            Icon = "wrench",
            Size = 15,
            Foreground = ArcUi.Brush(ArcTokens.TextSecondary),
            VerticalAlignment = VerticalAlignment.Center
        });
        var title = ArcUi.Strong(_call.ToolName);
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        var badge = StatusBadge(_call.Status);
        Grid.SetColumn(badge, 2);
        header.Children.Add(badge);
        _body.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(_call.Intent))
        {
            _body.Children.Add(ArcUi.Body(_call.Intent, 620));
        }

        if (!string.IsNullOrWhiteSpace(_call.Arguments))
        {
            _body.Children.Add(ArcUi.Mono(_call.Arguments.Length > 320 ? _call.Arguments[..320] + "…" : _call.Arguments));
        }

        if (!string.IsNullOrWhiteSpace(_call.ResultSummary))
        {
            _body.Children.Add(ArcUi.Caption(_call.ResultSummary));
        }

        var actions = ArcUi.HStack(6);
        if (_call.Status == ArcToolCallStatus.AwaitingApproval)
        {
            var approve = ArcUi.Button("Approve", ArcButtonVariant.Primary, ArcButtonSize.Sm, "check");
            var deny = ArcUi.Button("Deny", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "x");
            var callId = _call.Id;
            approve.Click += (_, _) => ApproveRequested?.Invoke(this, callId);
            deny.Click += (_, _) => DenyRequested?.Invoke(this, callId);
            ArcUi.Name(approve, $"Approve {_call.ToolName}");
            ArcUi.Name(deny, $"Deny {_call.ToolName}");
            actions.Children.Add(approve);
            actions.Children.Add(deny);
        }
        else if (_call.Status == ArcToolCallStatus.Failed)
        {
            var retry = ArcUi.Button("Retry", ArcButtonVariant.Secondary, ArcButtonSize.Sm, "refresh-cw");
            var callId = _call.Id;
            retry.Click += (_, _) => RetryRequested?.Invoke(this, callId);
            ArcUi.Name(retry, $"Retry {_call.ToolName}");
            actions.Children.Add(retry);
        }

        var details = ArcUi.Button("Details", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "code");
        var currentId = _call.Id;
        details.Click += (_, _) => DetailsRequested?.Invoke(this, currentId);
        actions.Children.Add(details);

        if (actions.Children.Count > 0)
        {
            _body.Children.Add(actions);
        }

        AutomationProperties.SetName(this,
            $"{_call.ToolName} tool call, {_call.Status}. {_call.Intent}");
    }

    private static ArcBadge StatusBadge(ArcToolCallStatus status) => status switch
    {
        ArcToolCallStatus.Succeeded => ArcUi.Badge("Succeeded", ArcBadgeTone.Success),
        ArcToolCallStatus.Failed => ArcUi.Badge("Failed", ArcBadgeTone.Danger),
        ArcToolCallStatus.Denied => ArcUi.Badge("Denied", ArcBadgeTone.Neutral),
        ArcToolCallStatus.AwaitingApproval => ArcUi.Badge("Needs approval", ArcBadgeTone.Warning),
        ArcToolCallStatus.Running => ArcUi.Badge("Running", ArcBadgeTone.Accent),
        _ => ArcUi.Badge("Pending", ArcBadgeTone.Neutral)
    };
}
