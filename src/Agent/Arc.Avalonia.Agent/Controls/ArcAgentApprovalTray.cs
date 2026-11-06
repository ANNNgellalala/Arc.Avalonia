using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Arc.Avalonia.AgentUI.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;
using AgentApprovalArgument = Arc.Avalonia.AgentUI.Controls.ArcApprovalArgument;
using AgentApprovalDecision = Arc.Avalonia.AgentUI.Controls.ArcApprovalDecision;
using AgentApprovalRisk = Arc.Avalonia.AgentUI.Controls.ArcApprovalRisk;
using AgentApprovalScope = Arc.Avalonia.AgentUI.Controls.ArcApprovalScope;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Shows the current approval in the workspace-level <see cref="ArcApprovalQueue"/>. It reuses the existing
/// leaf <c>ArcToolApprovalSheet</c> and translates its intents into the top layer's decision model; the queue
/// serialises requests so only one sheet is ever visible.
/// </summary>
public class ArcAgentApprovalTray : Panel
{
    private readonly StackPanel _root = ArcUi.Stack(8);
    private readonly TextBlock _heading = ArcUi.Strong("Waiting for your decision");
    private ArcApprovalQueue? _queue;
    private ArcToolApprovalSheet? _sheet;

    /// <summary>Creates the tray.</summary>
    public ArcAgentApprovalTray()
    {
        Children.Add(_root);
        IsVisible = false;
    }

    /// <summary>The queue to render.</summary>
    public ArcApprovalQueue? Queue
    {
        get => _queue;
        set
        {
            if (ReferenceEquals(_queue, value))
            {
                return;
            }

            if (_queue is not null)
            {
                _queue.Changed -= OnQueueChanged;
            }

            _queue = value;
            if (_queue is not null)
            {
                _queue.Changed += OnQueueChanged;
            }

            Refresh();
        }
    }

    /// <summary>Raised when the person makes a decision; the host enforces it.</summary>
    public static readonly RoutedEvent<ArcAgentDecisionRoutedEventArgs> DecisionRequestedEvent =
        RoutedEvent.Register<ArcAgentApprovalTray, ArcAgentDecisionRoutedEventArgs>("DecisionRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="DecisionRequested"/>.</summary>
    public event EventHandler<ArcAgentDecisionRoutedEventArgs>? DecisionRequestedRouted
    {
        add => AddHandler(DecisionRequestedEvent, value);
        remove => RemoveHandler(DecisionRequestedEvent, value);
    }

    public event EventHandler<Arc.Avalonia.Agent.ArcApprovalDecision>? DecisionRequested;

    private void OnQueueChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var current = _queue?.Current;
        IsVisible = current is not null;
        if (current is null)
        {
            _sheet = null;
            _root.Children.Clear();
            return;
        }

        _root.Children.Clear();
        _root.Children.Add(_heading);
        if (_queue is not null && _queue.Count > 1)
        {
            _root.Children.Add(ArcUi.Caption($"{_queue.Count - 1} more request(s) queued; the host approves them one at a time."));
        }

        var request = current;
        _sheet = new ArcToolApprovalSheet
        {
            ToolName = request.ToolName,
            Intent = request.Intent,
            Risk = MapRisk(request.Risk),
            Arguments = request.Arguments.Select(argument =>
                new AgentApprovalArgument { Name = argument.Name, Value = argument.Value, Editable = argument.Editable }).ToArray(),
            PreviewLines = request.SideEffects,
            ExpiresInSeconds = request.ExpiresAt is { } expires
                ? Math.Max(0, (expires - DateTimeOffset.UtcNow).TotalSeconds)
                : 0,
            Scope = AgentApprovalScope.Once,
            State = _queue?.IsSubmitting(request.RequestId) == true
                ? ArcApprovalSheetState.Submitting
                : ArcApprovalSheetState.Checkout
        };
        _sheet.ApproveRequested += (_, decision) =>
        {
            _queue?.MarkSubmitting(request.RequestId);
            RaiseDecision(new Arc.Avalonia.Agent.ArcApprovalDecision(
                request.RequestId,
                Approved: true,
                Scope: MapScope(decision.Scope),
                ModifiedArguments: decision.ModifiedArguments
                    ?.Select(argument => new ArcApprovalArgument(argument.Name, argument.Value, argument.Editable))
                    .ToArray()));
        };
        _sheet.DenyRequested += (_, reason) =>
        {
            _queue?.MarkSubmitting(request.RequestId);
            RaiseDecision(new Arc.Avalonia.Agent.ArcApprovalDecision(request.RequestId, Approved: false, DenyReason: reason));
        };
        _sheet.DelegateRequested += (_, _) =>
        {
            _queue?.MarkSubmitting(request.RequestId);
            RaiseDecision(new Arc.Avalonia.Agent.ArcApprovalDecision(request.RequestId, Approved: false, DelegateTo: "host"));
        };

        _root.Children.Add(_sheet);
    }

    private void RaiseDecision(Arc.Avalonia.Agent.ArcApprovalDecision decision)
    {
        DecisionRequested?.Invoke(this, decision);
        RaiseEvent(new ArcAgentDecisionRoutedEventArgs(DecisionRequestedEvent, decision));
    }

    private static AgentApprovalRisk MapRisk(ArcRiskLevel risk) => risk switch
    {
        ArcRiskLevel.High => AgentApprovalRisk.High,
        ArcRiskLevel.Medium => AgentApprovalRisk.Medium,
        _ => AgentApprovalRisk.Low
    };

    private static Arc.Avalonia.Agent.ArcApprovalScope MapScope(AgentApprovalScope scope) => scope switch
    {
        AgentApprovalScope.Task => ArcApprovalScope.Task,
        AgentApprovalScope.Hour => ArcApprovalScope.Hour,
        AgentApprovalScope.Always => ArcApprovalScope.Always,
        _ => ArcApprovalScope.Once
    };
}



