using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;

using Avalonia.Interactivity;

namespace Arc.Avalonia.Actions.Controls;

public enum ArcCopyState
{
    Idle,
    Copied,
    Failed
}

/// <summary>
/// Copies a value to the clipboard and confirms in place: the icon becomes a check and the label
/// becomes "Copied" for about two seconds, then the button quietly returns to its resting face.
/// </summary>
public sealed class ArcCopyButton : ArcButton
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcCopyButton, string?>(nameof(Value));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcCopyButton, string?>(nameof(Label), "Copy");

    public static readonly StyledProperty<bool> IconOnlyProperty =
        AvaloniaProperty.Register<ArcCopyButton, bool>(nameof(IconOnly));

    public static readonly StyledProperty<ArcCopyState> StateProperty =
        AvaloniaProperty.Register<ArcCopyButton, ArcCopyState>(nameof(State));

    public static readonly StyledProperty<string?> CopiedLabelProperty =
        AvaloniaProperty.Register<ArcCopyButton, string?>(nameof(CopiedLabel), "Copied");

    public static readonly StyledProperty<string?> FailedLabelProperty =
        AvaloniaProperty.Register<ArcCopyButton, string?>(nameof(FailedLabel), "Could not copy");

    private readonly DispatcherTimer _reset;

    protected override Type StyleKeyOverride => typeof(ArcButton);

    static ArcCopyButton()
    {
        LabelProperty.Changed.AddClassHandler<ArcCopyButton>((button, _) => button.UpdateFace());
        IconOnlyProperty.Changed.AddClassHandler<ArcCopyButton>((button, _) => button.UpdateFace());
        StateProperty.Changed.AddClassHandler<ArcCopyButton>((button, _) => button.UpdateFace());
        CopiedLabelProperty.Changed.AddClassHandler<ArcCopyButton>((button, _) => button.UpdateFace());
    }

    public ArcCopyButton()
    {
        Variant = ArcButtonVariant.Secondary;
        Size = ArcButtonSize.Sm;
        _reset = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        _reset.Tick += (_, _) =>
        {
            _reset.Stop();
            SetCurrentValue(StateProperty, ArcCopyState.Idle);
        };
        UpdateFace();
    }

    /// <summary>Raised after a successful copy.</summary>
    public event EventHandler? Copied;
    /// <summary>Bubbled copy of <see cref="Copied"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcCopyButtonCopiedEventArgs> CopiedEvent =
        RoutedEvent.Register<ArcCopyButton, ArcCopyButtonCopiedEventArgs>("Copied", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="Copied"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcCopyButtonCopiedEventArgs>? CopiedRouted
    {
        add => AddHandler(CopiedEvent, value);
        remove => RemoveHandler(CopiedEvent, value);
    }

    /// <summary>The text placed on the clipboard.</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Idle label and accessible name.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Shows only the icon; the label stays as the accessible name.</summary>
    public bool IconOnly
    {
        get => GetValue(IconOnlyProperty);
        set => SetValue(IconOnlyProperty, value);
    }

    /// <summary>The current confirmation face; settable so a demo can show the Copied state.</summary>
    public ArcCopyState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string? CopiedLabel
    {
        get => GetValue(CopiedLabelProperty);
        set => SetValue(CopiedLabelProperty, value);
    }

    public string? FailedLabel
    {
        get => GetValue(FailedLabelProperty);
        set => SetValue(FailedLabelProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        _ = CopyAsync();
    }

    /// <summary>Copies the value; failures show the Failed face instead of throwing.</summary>
    public async System.Threading.Tasks.Task CopyAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        var clipboard = top?.Clipboard;
        if (clipboard is null || string.IsNullOrEmpty(Value))
        {
            SetCurrentValue(StateProperty, ArcCopyState.Failed);
            return;
        }

        try
        {
            var item = new DataTransferItem();
            item.SetText(Value);
            var data = new DataTransfer();
            data.Add(item);
            await clipboard.SetDataAsync(data).ConfigureAwait(true);
            SetCurrentValue(StateProperty, ArcCopyState.Copied);
                    var copiedArgs = new ArcCopyButtonCopiedEventArgs(CopiedEvent);
        Copied?.Invoke(this, EventArgs.Empty);
        RaiseEvent(copiedArgs);
        }
        catch
        {
            SetCurrentValue(StateProperty, ArcCopyState.Failed);
        }
    }

    private void UpdateFace()
    {
        Compact = IconOnly;
        Size = ArcButtonSize.Sm;
        var icon = State switch
        {
            ArcCopyState.Copied => "check",
            ArcCopyState.Failed => "circle-alert",
            _ => "copy"
        };

        if (IconOnly)
        {
            Content = new ArcIcon { Icon = icon, Size = 16 };
        }
        else
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(new ArcIcon { Icon = icon, Size = 15, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(ArcActionVisuals.InheritText(State switch
            {
                ArcCopyState.Copied => CopiedLabel ?? "Copied",
                ArcCopyState.Failed => FailedLabel ?? "Could not copy",
                _ => Label ?? "Copy"
            }, this));
            Content = panel;
            Classes.Set("copied", State == ArcCopyState.Copied);
            Classes.Set("failed", State == ArcCopyState.Failed);
        }

        var name = State switch
        {
            ArcCopyState.Copied => CopiedLabel ?? "Copied",
            ArcCopyState.Failed => FailedLabel ?? "Could not copy",
            _ => Label ?? "Copy"
        };
        AutomationProperties.SetName(this, name);
        if (State != ArcCopyState.Idle)
        {
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        }

        if (State == ArcCopyState.Idle)
        {
            _reset.Stop();
        }
        else
        {
            _reset.Stop();
            _reset.Start();
        }
    }
}
