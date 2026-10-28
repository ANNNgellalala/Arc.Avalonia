using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>
/// A modal sheet that rises from the bottom edge and rests at one or more detents. The header follows the
/// pointer one-to-one, rubber-bands past the tallest detent, and releases to the nearest detent or to a
/// dismissal. The grabber is a button: arrows move one detent, Home and End jump to the ends, Enter or
/// Space toggles. The scrim dims with the sheet, so a drag reads as one gesture.
/// </summary>
public class ArcBottomSheet : ContentControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcBottomSheet, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArcBottomSheet, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcBottomSheet, string?>(nameof(Description));

    public static readonly StyledProperty<IReadOnlyList<double>> DetentsProperty =
        AvaloniaProperty.Register<ArcBottomSheet, IReadOnlyList<double>>(
            nameof(Detents),
            new[] { 0.45, 0.92 });

    public static readonly StyledProperty<int> InitialDetentProperty =
        AvaloniaProperty.Register<ArcBottomSheet, int>(nameof(InitialDetent));

    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<ArcBottomSheet, string>(nameof(CloseLabel), "Close");

    private Panel? _layer;
    private Border? _scrim;
    private Border? _sheet;
    private StackPanel? _header;
    private ArcButton? _grabber;
    private ArcButton? _close;
    private TextBlock? _title;
    private TextBlock? _description;
    private ContentPresenter? _body;
    private IDisposable? _dismiss;
    private ArcTween? _tween;
    private Control? _restoreFocus;
    private bool _wasOpen;
    private bool _dragging;
    private double _dragStartY;
    private double _dragStartHeight;
    private double _lastY;
    private ulong _lastTime;
    private double _velocity;
    private int _detentIndex;

    static ArcBottomSheet()
    {
        IsOpenProperty.Changed.AddClassHandler<ArcBottomSheet>((sheet, _) => sheet.Sync());
        TitleProperty.Changed.AddClassHandler<ArcBottomSheet>((sheet, _) => sheet.UpdateCopy());
        DescriptionProperty.Changed.AddClassHandler<ArcBottomSheet>((sheet, _) => sheet.UpdateCopy());
        DetentsProperty.Changed.AddClassHandler<ArcBottomSheet>((sheet, _) => sheet.Sync());
        InitialDetentProperty.Changed.AddClassHandler<ArcBottomSheet>((sheet, _) => sheet.Sync());
        CloseLabelProperty.Changed.AddClassHandler<ArcBottomSheet>((sheet, _) => sheet.UpdateCopy());
    }

    public ArcBottomSheet()
    {
        Focusable = true;
    }

    public event EventHandler<bool>? OpenChanged;

    public event EventHandler<int>? DetentChanged;

    /// <summary>Whether the sheet is showing; settable so a page can present the open state.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Resting heights as fractions of the host height, smallest first.</summary>
    public IReadOnlyList<double> Detents
    {
        get => GetValue(DetentsProperty);
        set => SetValue(DetentsProperty, value);
    }

    public int InitialDetent
    {
        get => GetValue(InitialDetentProperty);
        set => SetValue(InitialDetentProperty, value);
    }

    public string CloseLabel
    {
        get => GetValue(CloseLabelProperty);
        set => SetValue(CloseLabelProperty, value);
    }

    /// <summary>The detent the sheet is resting at.</summary>
    public int Detent => _detentIndex;

    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Moves to a detent without a drag, used by the grabber keys and a page.</summary>
    public void GoToDetent(int index)
    {
        if (!IsOpen)
        {
            SetCurrentValue(InitialDetentProperty, index);
            return;
        }

        SetDetent(index, animate: true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layer = e.NameScope.Find<Panel>("Layer");
        _scrim = e.NameScope.Find<Border>("Scrim");
        _sheet = e.NameScope.Find<Border>("Sheet");
        _header = e.NameScope.Find<StackPanel>("Header");
        _grabber = e.NameScope.Find<ArcButton>("Grabber");
        _close = e.NameScope.Find<ArcButton>("CloseButton");
        _title = e.NameScope.Find<TextBlock>("Title");
        _description = e.NameScope.Find<TextBlock>("Description");
        _body = e.NameScope.Find<ContentPresenter>("Body");

        if (_grabber is not null)
        {
            _grabber.Click += (_, _) => ToggleDetent();
            _grabber.KeyDown += OnGrabberKeyDown;
        }

        if (_close is not null)
        {
            _close.Content = ArcEffectVisuals.Icon("x", 16);
            _close.Click += (_, _) => Close();
        }

        if (_sheet is not null)
        {
            _sheet.PointerPressed += OnSheetPressed;
            _sheet.PointerMoved += OnSheetMoved;
            _sheet.PointerReleased += OnSheetReleased;
            _sheet.PointerCaptureLost += (_, _) => _dragging = false;
        }

        if (_sheet is not null)
        {
            var radius = ArcResources.Radius(ArcTokens.RadiusPanel);
            _sheet.CornerRadius = new CornerRadius(radius.TopLeft, radius.TopRight, 0, 0);
        }

        UpdateCopy();
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && IsOpen && !_dragging && _sheet is not null)
        {
            var target = HeightFor(_detentIndex);
            if (target > 0 && Math.Abs(_sheet.Height - target) > 1)
            {
                _sheet.Height = target;
                UpdateScrim(target);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _dismiss?.Dispose();
        _dismiss = null;
        _tween?.Stop();
        _tween = null;
    }

    private void UpdateCopy()
    {
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrWhiteSpace(Title);
        }

        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrWhiteSpace(Description);
        }

        if (_close is not null)
        {
            AutomationProperties.SetName(_close, CloseLabel);
        }

        if (_grabber is not null)
        {
            AutomationProperties.SetName(_grabber, "Sheet grabber");
            AutomationProperties.SetHelpText(_grabber, "Drag, or use the arrow keys to move between heights");
        }

        AutomationProperties.SetName(this, Title ?? "Bottom sheet");
        if (_sheet is not null)
        {
            AutomationProperties.SetName(_sheet, Title ?? "Bottom sheet");
            AutomationProperties.SetLiveSetting(_sheet, AutomationLiveSetting.Polite);
        }
    }

    private void Sync()
    {
        if (_layer is null || _sheet is null)
        {
            return;
        }

        if (IsOpen && !_wasOpen)
        {
            _wasOpen = true;
            _layer.IsVisible = true;
            _layer.Opacity = 1;
            _detentIndex = ClampInitial();
            _restoreFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            _dismiss?.Dispose();
            _dismiss = ArcEffectDismiss.Attach(this, Close, () => IsOpen);
            UpdateScrim(ArcThemeManager.ReducedMotion ? HeightFor(_detentIndex) : 0);
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsOpen || _sheet is null)
                {
                    return;
                }

                var target = HeightFor(_detentIndex);
                _sheet.Height = ArcThemeManager.ReducedMotion ? target : 0;
                if (ArcThemeManager.ReducedMotion)
                {
                    UpdateScrim(target);
                }
                else
                {
                    AnimateHeight(target, ArcMotion.SpringSmoothDuration, ArcMotion.SpringSmooth, () => { });
                }

                _sheet.Focus();
                AnnounceDetent();
            }, DispatcherPriority.Loaded);

            OpenChanged?.Invoke(this, true);
        }
        else if (!IsOpen && _wasOpen)
        {
            _wasOpen = false;
            _dismiss?.Dispose();
            _dismiss = null;
            AnimateHeight(0, ArcMotion.Exit, ArcMotion.EaseExit, () =>
            {
                if (_layer is not null)
                {
                    _layer.IsVisible = false;
                }
            });
            OpenChanged?.Invoke(this, false);

            if (_restoreFocus is { } restore)
            {
                Dispatcher.UIThread.Post(() => restore.Focus(), DispatcherPriority.Loaded);
            }
        }
        else if (!IsOpen && _layer is not null)
        {
            _layer.IsVisible = false;
        }
    }

    private int ClampInitial()
    {
        var count = Detents?.Count ?? 0;
        return count == 0 ? 0 : Math.Clamp(InitialDetent, 0, count - 1);
    }

    private double TallestHeight() => HeightFor((Detents?.Count ?? 1) - 1);

    private double HeightFor(int index)
    {
        var detents = Detents;
        if (detents is null || detents.Count == 0)
        {
            return 0;
        }

        var fraction = detents[Math.Clamp(index, 0, detents.Count - 1)];
        return Math.Max(0, Bounds.Height * Math.Clamp(fraction, 0.05, 1));
    }

    private void UpdateScrim(double height)
    {
        if (_scrim is null)
        {
            return;
        }

        var max = TallestHeight();
        _scrim.Opacity = max <= 0 ? 0 : Math.Clamp(height / max, 0, 1);
    }

    private void AnimateHeight(double target, TimeSpan duration, Easing easing, Action completed)
    {
        if (_sheet is null)
        {
            return;
        }

        _tween?.Stop();
        if (ArcThemeManager.ReducedMotion || duration <= TimeSpan.Zero)
        {
            _sheet.Height = target;
            UpdateScrim(target);
            completed();
            return;
        }

        var from = _sheet.Height > 0 ? _sheet.Height : 0;
        _tween = ArcTween.Run(duration, easing, progress =>
        {
            var height = from + (target - from) * progress;
            _sheet.Height = Math.Max(0, height);
            UpdateScrim(height);
        }, () =>
        {
            _sheet.Height = target;
            UpdateScrim(target);
            completed();
        });
    }

    private void SetDetent(int index, bool animate)
    {
        var count = Detents?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        _detentIndex = Math.Clamp(index, 0, count - 1);
        var target = HeightFor(_detentIndex);
        if (animate)
        {
            AnimateHeight(target, ArcMotion.SpringSmoothDuration, ArcMotion.SpringSmooth, () => { });
        }
        else if (_sheet is not null)
        {
            _sheet.Height = target;
            UpdateScrim(target);
        }

        AnnounceDetent();
        DetentChanged?.Invoke(this, _detentIndex);
    }

    private void AnnounceDetent()
    {
        if (_sheet is null)
        {
            return;
        }

        var count = Detents?.Count ?? 0;
        AutomationProperties.SetHelpText(_sheet, count <= 1 ? "Sheet" : $"Height {_detentIndex + 1} of {count}");
        if (_grabber is not null)
        {
            var tallest = count - 1;
            AutomationProperties.SetName(_grabber, _detentIndex >= tallest ? "Collapse sheet" : "Expand sheet");
        }
    }

    private void ToggleDetent()
    {
        var count = Detents?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var tallest = count - 1;
        SetDetent(_detentIndex >= tallest ? 0 : tallest, animate: true);
    }

    private void OnGrabberKeyDown(object? sender, KeyEventArgs e)
    {
        var count = Detents?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Up:
                SetDetent(_detentIndex + 1, animate: true);
                break;
            case Key.Down:
                SetDetent(_detentIndex - 1, animate: true);
                break;
            case Key.Home:
                SetDetent(0, animate: true);
                break;
            case Key.End:
                SetDetent(count - 1, animate: true);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private bool PressInHeader(object? source)
    {
        if (_header is null || source is not Visual visual)
        {
            return false;
        }

        for (Visual? current = visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, _header))
            {
                return true;
            }

            if (ReferenceEquals(current, _sheet))
            {
                return false;
            }
        }

        return false;
    }

    private void OnSheetPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_sheet is null || !e.GetCurrentPoint(_sheet).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_detentIndex > 0 && !PressInHeader(e.Source))
        {
            return;
        }

        StopTween();
        _dragging = true;
        _dragStartY = e.GetPosition(this).Y;
        _dragStartHeight = _sheet.Bounds.Height;
        _lastY = _dragStartY;
        _lastTime = e.Timestamp;
        _velocity = 0;
        e.Pointer.Capture(_sheet);
        e.Handled = true;
    }

    private void OnSheetMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || _sheet is null)
        {
            return;
        }

        var point = e.GetPosition(this);
        var max = TallestHeight();
        var height = _dragStartHeight + (_dragStartY - point.Y);

        if (height > max && max > 0)
        {
            height = max + (height - max) * 0.22;
        }

        if (height < 0)
        {
            height *= 0.35;
        }

        _sheet.Height = Math.Max(0, height);
        UpdateScrim(_sheet.Height);

        var delta = e.Timestamp - _lastTime;
        if (delta > 0)
        {
            _velocity = (point.Y - _lastY) / delta;
        }

        _lastY = point.Y;
        _lastTime = e.Timestamp;
        e.Handled = true;
    }

    private void OnSheetReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging || _sheet is null)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);

        var max = TallestHeight();
        var height = _sheet.Bounds.Height;
        var detents = Detents;
        if (max <= 0 || detents is null || detents.Count == 0)
        {
            return;
        }

        var dismiss = height < max * 0.28 || _velocity > 1.1;
        if (dismiss)
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }

        var target = NearestDetent(height);
        if (Math.Abs(_velocity) > 0.45)
        {
            var direction = _velocity < 0 ? 1 : -1;
            var index = _detentIndex + direction;
            if (index >= 0 && index < detents.Count)
            {
                target = index;
            }
        }

        SetDetent(target, animate: true);
    }

    private int NearestDetent(double height)
    {
        var count = Detents?.Count ?? 0;
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < count; i++)
        {
            var distance = Math.Abs(HeightFor(i) - height);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private void StopTween()
    {
        _tween?.Stop();
        _tween = null;
    }
}
