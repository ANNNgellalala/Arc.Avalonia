using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;
using LayoutOrientation = Avalonia.Layout.Orientation;

namespace Arc.Avalonia.Actions.Controls;

public enum ArcExpandingButtonSize
{
    Sm,
    Md
}

public enum ArcExpandingButtonTone
{
    Neutral,
    Danger
}

/// <summary>One action in an expanding group.</summary>
public sealed class ArcExpandingButtonItem
{
    public string? Id { get; set; }

    /// <summary>Visible once expanded; always the accessible name. Use a short verb.</summary>
    public string? Label { get; set; }

    /// <summary>Lucide icon name shown while collapsed.</summary>
    public string? Icon { get; set; }

    /// <summary>Past tense word shown after a successful action. Omit for actions that open something.</summary>
    public string? DoneLabel { get; set; }

    /// <summary>Danger colours the expanded state, for a destructive action such as Delete.</summary>
    public ArcExpandingButtonTone Tone { get; set; }

    public bool IsDisabled { get; set; }

    /// <summary>Runs the action. Return false to skip the confirmation; a long task keeps the button busy.</summary>
    public Func<Task<bool>>? OnSelect { get; set; }
}

/// <summary>
/// A compact pill of icon buttons for one object's actions. One action always shows its label; the one the
/// pointer or keyboard moves to takes the label over while its neighbours slide aside. Actions with a
/// done label confirm in place with a check and a past tense word, then return to their resting face.
/// The group is a toolbar with one roving tab stop and wraps arrow-key navigation at the ends. Touch's
/// first tap to reveal and second to run is not ported; this control targets pointer and keyboard.
/// </summary>
public sealed class ArcExpandingButtonGroup : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcExpandingButtonItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcExpandingButtonGroup, IList<ArcExpandingButtonItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcExpandingButtonGroup, string?>(nameof(Label));

    public static readonly StyledProperty<ArcExpandingButtonSize> SizeProperty =
        AvaloniaProperty.Register<ArcExpandingButtonGroup, ArcExpandingButtonSize>(nameof(Size), ArcExpandingButtonSize.Md);

    public static readonly StyledProperty<string?> DefaultExpandedProperty =
        AvaloniaProperty.Register<ArcExpandingButtonGroup, string?>(nameof(DefaultExpanded));

    public static readonly StyledProperty<int> ConfirmDurationProperty =
        AvaloniaProperty.Register<ArcExpandingButtonGroup, int>(nameof(ConfirmDuration), 1400);

    /// <summary>Pixels the pointer must travel into a neighbouring action before the expansion moves there.</summary>
    private const double Hysteresis = 4;

    private readonly List<Slot> _slots = new();
    private readonly DispatcherTimer _confirm;
    private StackPanel? _host;
    private string? _restId;
    private string? _keyboardId;
    private string? _pointerId;
    private bool _pointerInside;

    static ArcExpandingButtonGroup()
    {
        ItemsProperty.Changed.AddClassHandler<ArcExpandingButtonGroup>((group, _) => group.Rebuild());
        SizeProperty.Changed.AddClassHandler<ArcExpandingButtonGroup>((group, _) => group.Rebuild());
        DefaultExpandedProperty.Changed.AddClassHandler<ArcExpandingButtonGroup>((group, _) => group.Rebuild());
        LabelProperty.Changed.AddClassHandler<ArcExpandingButtonGroup>((group, _) =>
        {
            ArcAdvancedVisuals.SetName(group, group.Label);
            AutomationProperties.SetHelpText(group, "Toolbar");
        });
    }

    public ArcExpandingButtonGroup()
    {
        _confirm = new DispatcherTimer();
        _confirm.Tick += (_, _) => EndConfirmation();
        AddHandler(PointerPressedEvent, OnTunnelPress, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Raised with the item id whenever an action runs, before its own callback.</summary>
    public event EventHandler<string?>? ActionInvoked;

    public IList<ArcExpandingButtonItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Accessible name of the toolbar, such as "Message actions".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public ArcExpandingButtonSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Id of the action whose label shows at rest. Defaults to the first enabled action.</summary>
    public string? DefaultExpanded
    {
        get => GetValue(DefaultExpandedProperty);
        set => SetValue(DefaultExpandedProperty, value);
    }

    /// <summary>How long a confirmation stays, in milliseconds.</summary>
    public int ConfirmDuration
    {
        get => GetValue(ConfirmDurationProperty);
        set => SetValue(ConfirmDurationProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<StackPanel>("ItemsHost");
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Actions" : Label!);
        AutomationProperties.SetHelpText(this, "Toolbar");
        Rebuild();
    }

    private void OnTunnelPress(object? sender, PointerPressedEventArgs e) => _pointerInside = true;

    private void Rebuild()
    {
        _confirm.Stop();
        _keyboardId = null;
        _pointerId = null;

        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _slots.Clear();

        var small = Size == ArcExpandingButtonSize.Sm;
        var metrics = MetricsFor(small);
        var items = Items?.ToList() ?? new List<ArcExpandingButtonItem>();
        _host.Spacing = 2;

        foreach (var item in items)
        {
            var slot = BuildSlot(item, metrics, small);
            _slots.Add(slot);
            _host.Children.Add(slot.Button);
        }

        _restId = ResolveRest();
        if (_restId is null && _slots.Count > 0)
        {
            _restId = _slots[0].Item.Id;
        }

        var tabStop = _slots.FirstOrDefault(slot => slot.Item.Id == _restId);
        foreach (var slot in _slots)
        {
            slot.Button.IsTabStop = ReferenceEquals(slot, tabStop);
        }

        if (_restId is not null)
        {
            Expand(_restId, animate: false);
        }
    }

    private string? ResolveRest()
    {
        if (!string.IsNullOrWhiteSpace(DefaultExpanded) &&
            _slots.Any(slot => slot.Item.Id == DefaultExpanded))
        {
            return DefaultExpanded;
        }

        return _slots.FirstOrDefault(slot => !slot.Item.IsDisabled)?.Item.Id
               ?? _slots.FirstOrDefault()?.Item.Id;
    }

    private sealed class Metrics
    {
        public double Height { get; init; }
        public double Icon { get; init; }
        public double Start { get; init; }
        public double Gap { get; init; }
        public double End { get; init; }
        public double FontSize { get; init; }
    }

    private static Metrics MetricsFor(bool small) => small
        ? new Metrics { Height = 28, Icon = 14, Start = 7, Gap = 5, End = 11, FontSize = 12 }
        : new Metrics { Height = 36, Icon = 16, Start = 10, Gap = 7, End = 14, FontSize = 14 };

    private sealed class Slot
    {
        public required ArcExpandingButtonItem Item { get; init; }
        public required ArcButton Button { get; init; }
        public required ArcIcon Icon { get; init; }
        public required ArcSpinner Spinner { get; init; }
        public required TextBlock Label { get; init; }
        public required double LabelWidth { get; init; }
        public required Metrics Metrics { get; init; }
        public bool IsExpanded { get; set; }
        public bool IsDone { get; set; }
        public bool IsBusy { get; set; }
    }

    private Slot BuildSlot(ArcExpandingButtonItem item, Metrics metrics, bool small)
    {
        var labelWidth = Math.Max(
            ArcAdvancedVisuals.MeasureText(item.Label, metrics.FontSize, FontWeight.Medium),
            ArcAdvancedVisuals.MeasureText(item.DoneLabel, metrics.FontSize, FontWeight.Medium)) + 4;

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = small ? ArcButtonSize.Sm : ArcButtonSize.Md,
            Height = metrics.Height,
            MinHeight = metrics.Height,
            Width = metrics.Height,
            Padding = new Thickness(metrics.Start, 0, 0, 0),
            CornerRadius = ArcAdvancedVisuals.Radius(ArcTokens.RadiusPill),
            FontSize = metrics.FontSize,
            FontWeight = FontWeight.Medium,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
            IsTabStop = false
        };
        button.Classes.Add("expanding-item");

        var icon = ArcAdvancedVisuals.Icon(item.Icon, metrics.Icon);
        icon.Classes.Add("expand-icon");
        icon.RenderTransformOrigin = RelativePoint.Center;
        if (!ArcThemeManager.ReducedMotion)
        {
            icon.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = ArcMotion.Standard,
                    Easing = ArcMotion.EaseStandard
                }
            };
        }

        var spinner = new ArcSpinner
        {
            Width = metrics.Icon,
            Height = metrics.Icon,
            StrokeWidth = 2,
            IsVisible = false
        };
        spinner.Bind(ArcSpinner.StrokeProperty, new Binding("Foreground") { Source = button });

        var iconHost = new Panel
        {
            Width = metrics.Icon,
            Height = metrics.Icon,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconHost.Children.Add(icon);
        iconHost.Children.Add(spinner);

        var label = new TextBlock
        {
            Text = item.Label ?? string.Empty,
            Width = labelWidth,
            FontSize = metrics.FontSize,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = 0,
            RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative)
        };
        label.Bind(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = button });

        var content = new StackPanel
        {
            Orientation = LayoutOrientation.Horizontal,
            Spacing = metrics.Gap,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(iconHost);
        content.Children.Add(label);
        button.Content = content;

        var slot = new Slot
        {
            Item = item,
            Button = button,
            Icon = icon,
            Spinner = spinner,
            Label = label,
            LabelWidth = labelWidth,
            Metrics = metrics
        };

        ArcAdvancedVisuals.SetName(button, item.Label);
        if (item.IsDisabled)
        {
            button.Classes.Add("disabled-item");
            AutomationProperties.SetHelpText(button, "Unavailable");
            AutomationProperties.SetItemStatus(button, "Unavailable");
        }

        button.Click += (_, _) => _ = ActivateAsync(slot);
        button.GotFocus += (_, _) => OnSlotFocused(slot);
        button.PointerPressed += (_, args) =>
        {
            if (item.IsDisabled)
            {
                return;
            }

            PressIcon(slot, true);
        };
        button.PointerReleased += (_, _) => PressIcon(slot, false);
        button.PointerCaptureLost += (_, _) => PressIcon(slot, false);

        return slot;
    }

    private void PressIcon(Slot slot, bool pressed)
    {
        if (ArcThemeManager.ReducedMotion)
        {
            slot.Icon.RenderTransform = TransformOperations.Parse("scale(1)");
            return;
        }

        slot.Icon.RenderTransform = TransformOperations.Parse(pressed ? "scale(0.86)" : "scale(1)");
    }

    private void OnSlotFocused(Slot slot)
    {
        foreach (var other in _slots)
        {
            other.Button.IsTabStop = ReferenceEquals(other, slot);
        }

        // A pointer click keeps its own path (the expansion already followed the pointer); only a
        // keyboard or programmatic focus, with the pointer outside the group, moves the label.
        if (_pointerInside)
        {
            return;
        }

        _keyboardId = slot.Item.Id;
        Expand(slot.Item.Id, animate: true);
    }

    private void Expand(string? id, bool animate)
    {
        var target = _slots.FirstOrDefault(slot => slot.Item.Id == id);

        foreach (var slot in _slots)
        {
            slot.IsExpanded = ReferenceEquals(slot, target);
            ApplySlot(slot, animate);
        }
    }

    private void ApplySlot(Slot slot, bool animate)
    {
        var expandedWidth = slot.Metrics.Start + slot.Metrics.Icon + slot.Metrics.Gap + slot.LabelWidth + slot.Metrics.End;
        var targetWidth = slot.IsExpanded ? expandedWidth : slot.Metrics.Height;

        var transitions = ArcThemeManager.ReducedMotion ? null : new Transitions
        {
            new DoubleTransition
            {
                Property = WidthProperty,
                Duration = ArcMotion.SpringMorphDuration,
                Easing = ArcMotion.SpringMorph
            }
        };

        if (animate)
        {
            slot.Button.Transitions = transitions;
            slot.Label.Transitions = ArcThemeManager.ReducedMotion
                ? null
                : new Transitions
                {
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = slot.IsExpanded ? ArcMotion.Standard : ArcMotion.Fast,
                        Easing = slot.IsExpanded ? ArcMotion.EaseEnter : ArcMotion.EaseStandard
                    },
                    new TransformOperationsTransition
                    {
                        Property = Visual.RenderTransformProperty,
                        Duration = slot.IsExpanded ? ArcMotion.Standard : ArcMotion.Fast,
                        Easing = slot.IsExpanded ? ArcMotion.EaseEnter : ArcMotion.EaseStandard
                    }
                };
        }
        else
        {
            slot.Button.Transitions = null;
            slot.Label.Transitions = null;
        }

        slot.Button.Width = targetWidth;
        if (!animate || ArcThemeManager.ReducedMotion)
        {
            slot.Button.Transitions = null;
            slot.Label.Transitions = null;
        }

        slot.Label.Opacity = slot.IsExpanded ? 1 : 0;
        slot.Label.RenderTransform = TransformOperations.Parse(slot.IsExpanded ? "translate(0px, 0px)" : "translate(-6px, 0px)");

        slot.Button.Classes.Set("expanded", slot.IsExpanded);
        slot.Button.Classes.Set("danger-item", slot.Item.Tone == ArcExpandingButtonTone.Danger);
        slot.Button.Classes.Set("done-item", slot.IsDone);
        slot.Button.Classes.Set("busy-item", slot.IsBusy);
        slot.Button.Classes.Set("disabled-item", slot.Item.IsDisabled);

        slot.Icon.Icon = slot.IsDone ? "check" : (slot.Item.Icon ?? "circle");
        slot.Icon.IsVisible = !slot.IsBusy;
        slot.Spinner.IsVisible = slot.IsBusy;
        slot.Label.Text = slot.IsDone ? (slot.Item.DoneLabel ?? slot.Item.Label ?? string.Empty) : (slot.Item.Label ?? string.Empty);

        var name = slot.Item.Label ?? slot.Item.Icon ?? "Action";
        ArcAdvancedVisuals.SetName(slot.Button, name);
        AutomationProperties.SetItemStatus(slot.Button, slot.IsBusy ? "Busy" : slot.IsDone ? (slot.Item.DoneLabel ?? name) : string.Empty);
        if (slot.IsDone)
        {
            AutomationProperties.SetLiveSetting(slot.Button, AutomationLiveSetting.Polite);
        }
    }

    private async Task ActivateAsync(Slot slot)
    {
        if (slot.Item.IsDisabled || slot.IsBusy || slot.IsDone)
        {
            return;
        }

        Expand(slot.Item.Id, animate: true);
        ActionInvoked?.Invoke(this, slot.Item.Id);

        var result = true;
        if (slot.Item.OnSelect is not null)
        {
            slot.IsBusy = true;
            ApplySlot(slot, animate: false);
            try
            {
                result = await slot.Item.OnSelect().ConfigureAwait(true);
            }
            catch
            {
                result = false;
            }
            finally
            {
                slot.IsBusy = false;
            }
        }

        if (!result || string.IsNullOrWhiteSpace(slot.Item.DoneLabel))
        {
            ApplySlot(slot, animate: false);
            RestoreAfterAction(slot);
            return;
        }

        EndConfirmation();
        slot.IsDone = true;
        Expand(slot.Item.Id, animate: true);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);

        if (ConfirmDuration > 0)
        {
            _confirm.Interval = TimeSpan.FromMilliseconds(ConfirmDuration);
            _confirm.Start();
        }
    }

    private void EndConfirmation()
    {
        _confirm.Stop();
        foreach (var slot in _slots)
        {
            if (!slot.IsDone)
            {
                continue;
            }

            slot.IsDone = false;
            ApplySlot(slot, animate: true);
        }

        if (!_pointerInside && _keyboardId is null)
        {
            Expand(_restId, animate: true);
        }
    }

    private void RestoreAfterAction(Slot slot)
    {
        if (_pointerInside || ReferenceEquals(_slots.FirstOrDefault(s => s.Item.Id == _keyboardId), slot))
        {
            return;
        }

        Expand(_restId, animate: true);
    }

    private Slot? SlotAt(Visual source)
    {
        for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ArcButton button)
            {
                return _slots.FirstOrDefault(slot => ReferenceEquals(slot.Button, button));
            }
        }

        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerInside = true;

        if (_slots.Count == 0)
        {
            return;
        }

        var slot = e.Source is Visual source ? SlotAt(source) : null;
        if (slot is null)
        {
            return;
        }

        var position = e.GetPosition(slot.Button);
        if (position.X < Hysteresis || position.X > slot.Button.Bounds.Width - Hysteresis)
        {
            return;
        }

        if (slot.Item.Id == _pointerId && slot.IsExpanded)
        {
            return;
        }

        _pointerId = slot.Item.Id;
        Expand(slot.Item.Id, animate: true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerInside = false;
        _pointerId = null;

        if (IsKeyboardFocusWithin)
        {
            return;
        }

        Expand(_keyboardId ?? _restId, animate: true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _slots.Count == 0)
        {
            return;
        }

        var index = _slots.FindIndex(slot => slot.Button.IsFocused);
        if (index < 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Right:
                FocusSlot((index + 1) % _slots.Count);
                e.Handled = true;
                break;
            case Key.Left:
                FocusSlot((index - 1 + _slots.Count) % _slots.Count);
                e.Handled = true;
                break;
            case Key.Home:
                FocusSlot(0);
                e.Handled = true;
                break;
            case Key.End:
                FocusSlot(_slots.Count - 1);
                e.Handled = true;
                break;
        }
    }

    private void FocusSlot(int index)
    {
        var slot = _slots[index];
        slot.Button.Focus();
        _keyboardId = slot.Item.Id;
        Expand(slot.Item.Id, animate: true);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Dispatcher.UIThread.Post(() =>
        {
            if (IsKeyboardFocusWithin)
            {
                return;
            }

            _keyboardId = null;
            if (!_pointerInside)
            {
                Expand(_restId, animate: true);
            }
        }, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _confirm.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}
