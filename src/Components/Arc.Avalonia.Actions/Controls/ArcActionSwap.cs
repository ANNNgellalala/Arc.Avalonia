using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;

using Avalonia.Interactivity;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>
/// A pill that steps through related states on each press, morphing its icon, tone and label in place.
/// Follow to Following, Send to Sent, and back again when the states wrap.
/// </summary>
public sealed class ArcActionSwap : ArcButton
{
    public static readonly StyledProperty<IList<ArcActionSwapState>?> StatesProperty =
        AvaloniaProperty.Register<ArcActionSwap, IList<ArcActionSwapState>?>(nameof(States));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcActionSwap, string?>(nameof(Value));

    public static readonly StyledProperty<string?> FailureLabelProperty =
        AvaloniaProperty.Register<ArcActionSwap, string?>(nameof(FailureLabel), "Try again");

    private readonly DispatcherTimer _hold;
    private readonly DispatcherTimer _failure;
    private bool _working;

    protected override Type StyleKeyOverride => typeof(ArcButton);

    static ArcActionSwap()
    {
        StatesProperty.Changed.AddClassHandler<ArcActionSwap>((swap, _) => swap.UpdateFace());
        ValueProperty.Changed.AddClassHandler<ArcActionSwap>((swap, _) => swap.UpdateFace());
    }

    public ArcActionSwap()
    {
        Classes.Add("action-swap");
        _hold = new DispatcherTimer();
        _hold.Tick += (_, _) =>
        {
            _hold.Stop();
            Advance();
        };
        _failure = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        _failure.Tick += (_, _) =>
        {
            _failure.Stop();
            UpdateFace();
        };
        UpdateFace();
    }

    /// <summary>Raised when the control commits to a new state.</summary>
    public event EventHandler<ArcActionSwapState>? ValueChanged;
    /// <summary>Bubbled copy of <see cref="ValueChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcActionSwapValueChangedEventArgs> ValueChangedEvent =
        RoutedEvent.Register<ArcActionSwap, ArcActionSwapValueChangedEventArgs>("ValueChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="ValueChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcActionSwapValueChangedEventArgs>? ValueChangedRouted
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>Ordered states; each press moves to the next and wraps around.</summary>
    public IList<ArcActionSwapState>? States
    {
        get => GetValue(StatesProperty);
        set => SetValue(StatesProperty, value);
    }

    /// <summary>Current state id.</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? FailureLabel
    {
        get => GetValue(FailureLabelProperty);
        set => SetValue(FailureLabelProperty, value);
    }

    private ArcActionSwapState? Current
    {
        get
        {
            var states = States?.ToList();
            if (states is null || states.Count == 0)
            {
                return null;
            }

            return states.FirstOrDefault(state => state.Id == Value) ?? states[0];
        }
    }

    protected override void OnClick()
    {
        if (_working)
        {
            return;
        }

        base.OnClick();
        Advance();
    }

    private void Advance()
    {
        var states = States?.ToList();
        var current = Current;
        if (states is null || current is null || states.Count < 2)
        {
            return;
        }

        var index = states.IndexOf(current);
        var next = states[(index + 1) % states.Count];
        _ = CommitAsync(current, next);
    }

    private async Task CommitAsync(ArcActionSwapState current, ArcActionSwapState next)
    {
        _working = true;
        var pending = next.PendingLabel;
        if (!string.IsNullOrWhiteSpace(pending))
        {
            ShowFace(pending, next.Icon ?? current.Icon, next.Tone, busy: true);
        }
        else
        {
            Loading = true;
            IsEnabled = false;
        }

        try
        {
            if (next.Action is not null)
            {
                await next.Action().ConfigureAwait(true);
            }

            SetCurrentValue(ValueProperty, next.Id);
                    var valueChangedArgs = new ArcActionSwapValueChangedEventArgs(ValueChangedEvent) { State = next };
        ValueChanged?.Invoke(this, next);
        RaiseEvent(valueChangedArgs);
        }
        catch
        {
            ShowFace(FailureLabel ?? "Try again", "circle-alert", ArcActionSwapTone.Warning, busy: false);
            _failure.Stop();
            _failure.Start();
            return;
        }
        finally
        {
            _working = false;
            Loading = false;
            IsEnabled = true;
        }

        UpdateFace();

        if (next.HoldFor > 0)
        {
            _hold.Stop();
            _hold.Interval = TimeSpan.FromMilliseconds(next.HoldFor);
            _hold.Start();
        }
    }

    private void UpdateFace()
    {
        var state = Current;
        if (state is null)
        {
            Content = new TextBlock { Text = "Action" };
            return;
        }

        if (Value != state.Id)
        {
            SetCurrentValue(ValueProperty, state.Id);
        }

        ShowFace(state.Label, state.Icon, state.Tone, busy: false);
    }

    private void ShowFace(string label, string? icon, ArcActionSwapTone tone, bool busy)
    {
        SetTone(tone);

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(icon))
        {
            panel.Children.Add(ArcActionVisuals.InheritIcon(icon, this, 16));
        }

        panel.Children.Add(ArcActionVisuals.InheritText(label, this, medium: true));
        Content = panel;

        AutomationProperties.SetName(this, label);
        if (busy)
        {
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        }
    }

    private void SetTone(ArcActionSwapTone tone)
    {
        Classes.Set("tone-neutral", tone == ArcActionSwapTone.Neutral);
        Classes.Set("tone-accent", tone == ArcActionSwapTone.Accent);
        Classes.Set("tone-success", tone == ArcActionSwapTone.Success);
        Classes.Set("tone-warning", tone == ArcActionSwapTone.Warning);
        Classes.Set("tone-quiet", tone == ArcActionSwapTone.Quiet);

        Variant = tone switch
        {
            ArcActionSwapTone.Accent => ArcButtonVariant.Primary,
            ArcActionSwapTone.Quiet => ArcButtonVariant.Ghost,
            _ => ArcButtonVariant.Secondary
        };
    }
}
