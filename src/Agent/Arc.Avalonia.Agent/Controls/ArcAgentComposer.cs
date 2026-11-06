using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Agent;

/// <summary>What the composer sends when the person submits a turn.</summary>
public sealed record ArcComposerSubmit(string Text, string? Model = null);

/// <summary>
/// The input half of the AgentSurface: a multi-line message field, an optional model label and the send/stop
/// actions. It emits intents only; the host transport starts the turn and the run bar owns the run state.
/// </summary>
public class ArcAgentComposer : Panel
{
    private readonly ArcInput _input = new()
    {
        Placeholder = "Message the agent…",
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private readonly ArcButton _send;
    private readonly ArcButton _stop;
    private readonly TextBlock _hint;
    private bool _isRunning;

    /// <summary>Creates the composer.</summary>
    public ArcAgentComposer()
    {
        _send = ArcUi.Button("Send", ArcButtonVariant.Primary, ArcButtonSize.Sm, "arrow-up");
        _stop = ArcUi.Button("Stop", ArcButtonVariant.Secondary, ArcButtonSize.Sm, "square");
        _hint = ArcUi.Caption("Enter sends · Shift+Enter adds a line · the host owns execution.");
        _send.Click += (_, _) => Submit();
        _stop.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
        _input.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        ArcUi.Name(_input, "Agent message", "Type a message for the agent. Enter sends, Shift+Enter inserts a new line.");
        ArcUi.Name(_send, "Send message", "Sends the draft to the host transport.");
        ArcUi.Name(_stop, "Stop run", "Asks the host to stop the current run.");

        var actions = ArcUi.HStack(8);
        actions.Children.Add(_send);
        actions.Children.Add(_stop);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        row.Children.Add(_input);
        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);

        var stack = ArcUi.Stack(6);
        stack.Children.Add(row);
        stack.Children.Add(_hint);

        Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(12),
            Child = stack
        });

        SetRunning(false);
    }

    /// <summary>Raised when the person submits a non-empty draft.</summary>
    public static readonly RoutedEvent<ArcComposerSubmitRoutedEventArgs> SubmitRequestedEvent =
        RoutedEvent.Register<ArcAgentComposer, ArcComposerSubmitRoutedEventArgs>("SubmitRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="SubmitRequested"/>.</summary>
    public event EventHandler<ArcComposerSubmitRoutedEventArgs>? SubmitRequestedRouted
    {
        add => AddHandler(SubmitRequestedEvent, value);
        remove => RemoveHandler(SubmitRequestedEvent, value);
    }

    public event EventHandler<ArcComposerSubmit>? SubmitRequested;

    /// <summary>Raised when the person asks to stop the current run.</summary>
    public event EventHandler? StopRequested;

    /// <summary>True while a run is active; toggles the send/stop emphasis.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            SetRunning(value);
        }
    }

    /// <summary>The current draft.</summary>
    public string Text => _input.Value ?? string.Empty;

    /// <summary>Clears the draft after a successful send.</summary>
    public void Clear() => _input.Value = string.Empty;

    /// <summary>Focuses the input field.</summary>
    public void FocusInput() => _input.Focus();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        e.Handled = true;
        Submit();
    }

    private void Submit()
    {
        var text = (Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return;
        }

        var submit = new ArcComposerSubmit(text);
        SubmitRequested?.Invoke(this, submit);
        RaiseEvent(new ArcComposerSubmitRoutedEventArgs(SubmitRequestedEvent, submit));
        Clear();
    }

    private void SetRunning(bool running)
    {
        _send.IsVisible = !running;
        _stop.IsVisible = running;
        _hint.Text = running ? "The host is streaming events; stop asks it to end the run." : "Enter sends · Shift+Enter adds a line · the host owns execution.";
    }
}



