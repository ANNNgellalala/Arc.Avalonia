using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

public sealed class ArcGlassSegmentedOption
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Optional Lucide icon shown before the label.</summary>
    public string? Icon { get; set; }

    /// <summary>Small accent count after the label.</summary>
    public string? Badge { get; set; }

    public bool Disabled { get; set; }
}

public enum ArcGlassSegmentedSize
{
    Sm,
    Md
}

/// <summary>
/// A glass segmented control with a liquid indicator. The selected segment can be dragged and flung:
/// both indicator edges run on their own spring while dragging, the leading edge races and the trailing
/// edge lags so the drop stretches towards the travel and thins as it does, and the release projects the
/// pointer's velocity to pick the segment it lands on.
///
/// Platform approximations: the web original paints the indicator as an SVG whose attributes update from
/// motion values; here a rounded Border is positioned per frame, which is the same paint-only update. The
/// glass is layered translucency because Avalonia has no backdrop filter. Dragging starts on the selected
/// segment only, matching the public behaviour, and touch drag equivalents are mouse drags on desktop.
/// </summary>
public sealed class ArcGlassSegmented : Panel
{
    public static readonly StyledProperty<IList<ArcGlassSegmentedOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcGlassSegmented, IList<ArcGlassSegmentedOption>?>(nameof(Options));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcGlassSegmented, string?>(nameof(Value));

    public static readonly StyledProperty<ArcGlassSegmentedSize> SizeProperty =
        AvaloniaProperty.Register<ArcGlassSegmented, ArcGlassSegmentedSize>(nameof(Size));

    public static readonly StyledProperty<bool> FullWidthProperty =
        AvaloniaProperty.Register<ArcGlassSegmented, bool>(nameof(FullWidth));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcGlassSegmented, string>(nameof(Label), "View");

    private readonly Border _track;
    private readonly Canvas _indicatorHost;
    private readonly Border _indicator;
    private readonly Grid _buttons;
    private readonly List<ArcButton> _segments = new();
    private readonly List<Border> _badges = new();
    private readonly List<ArcIcon?> _icons = new();
    private GlassTween? _tween;
    private bool _dragging;
    private double _dragStartX;
    private double _dragStartLeft;
    private double _lastX;
    private ulong _lastTime;
    private double _velocity;
    private bool _dragged;
    private double _cell = 96;
    private double _left;
    private double _width;
    private double _height;
    private bool _built;
    private int _segmentsBuilt = -1;

    static ArcGlassSegmented()
    {
        OptionsProperty.Changed.AddClassHandler<ArcGlassSegmented>((control, _) => control.Rebuild());
        ValueProperty.Changed.AddClassHandler<ArcGlassSegmented>((control, _) => control.SyncValue(animate: true));
        SizeProperty.Changed.AddClassHandler<ArcGlassSegmented>((control, _) => control.Rebuild());
        FullWidthProperty.Changed.AddClassHandler<ArcGlassSegmented>((control, _) => control.Rebuild());
        LabelProperty.Changed.AddClassHandler<ArcGlassSegmented>((control, _) => control.UpdateLabel());
    }

    public ArcGlassSegmented()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Left;

        _indicatorHost = new Canvas { ClipToBounds = false, IsHitTestVisible = false };
        _indicator = new Border
        {
            CornerRadius = new CornerRadius(9999),
            Background = ArcGlass.Gradient(
                180,
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.95), 0),
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.AccentStrong, 0.92), 1)),
            BorderBrush = ArcGlass.Gradient(
                180,
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.AccentStrong, 0.55), 0),
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.35), 1)),
            BorderThickness = new Thickness(1),
            BoxShadow = ArcGlass.Shadow(ArcTokens.ShadowRaised)
        };
        AutomationProperties.SetName(_indicator, string.Empty);
        _indicatorHost.Children.Add(_indicator);

        _buttons = new Grid { ClipToBounds = false, Background = null };
        _track = ArcGlass.Panel(22, new Thickness(4), floating: false, fillAlpha: 0.70);
        _track.Child = new Panel
        {
            Children = { _indicatorHost, _buttons }
        };
        _track.ClipToBounds = false;

        Children.Add(_track);
        UpdateLabel();
    }

    /// <summary>Raised with the chosen value when a segment is clicked, keyed, dragged or flung.</summary>
    public event EventHandler<string>? ValueChanged;

    public IList<ArcGlassSegmentedOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public ArcGlassSegmentedSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Stretches every segment to an equal share of the available width.</summary>
    public bool FullWidth
    {
        get => GetValue(FullWidthProperty);
        set => SetValue(FullWidthProperty, value);
    }

    /// <summary>Accessible name of the radio group.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private double Pad => Size == ArcGlassSegmentedSize.Sm ? 3 : 4;

    private double TrackHeight => Size == ArcGlassSegmentedSize.Sm ? 36 : 44;

    private static double FontSize => 13;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _tween?.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateLabel()
    {
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetHelpText(this, "Single choice group. Use the arrow keys to change the selection.");
    }

    private List<ArcGlassSegmentedOption> OptionList() => Options?.ToList() ?? new List<ArcGlassSegmentedOption>();

    private void Rebuild()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }

        var options = OptionList();
        if (options.Count == 0)
        {
            return;
        }

        _built = true;
        _track.Height = TrackHeight;
        _track.CornerRadius = new CornerRadius(22);
        _track.Padding = new Thickness(Pad);
        _track.HorizontalAlignment = FullWidth ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        HorizontalAlignment = FullWidth ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

        var selected = ResolveIndex(Value);
        if (selected < 0)
        {
            selected = options.FindIndex(option => !option.Disabled);
            if (selected < 0)
            {
                selected = 0;
            }
        }

        if (_segmentsBuilt != options.Count)
        {
            _segments.Clear();
            _badges.Clear();
            _icons.Clear();
            _buttons.Children.Clear();
            _buttons.ColumnDefinitions = new ColumnDefinitions(
                string.Join(",", Enumerable.Repeat("*", options.Count)));

            _cell = MeasureCell(options);
            for (var i = 0; i < options.Count; i++)
            {
                BuildSegment(i, options[i]);
            }

            _segmentsBuilt = options.Count;
        }

        if (!FullWidth)
        {
            _track.Width = (_cell * options.Count) + (Pad * 2);
        }
        else
        {
            _track.Width = double.NaN;
        }

        UpdateSegmentFaces(selected);
        UpdateRovingTabStop(selected);
        SetCurrentValue(ValueProperty, options[selected].Value);
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() => SnapIndicator(selected), DispatcherPriority.Loaded);
    }

    private double MeasureCell(List<ArcGlassSegmentedOption> options)
    {
        var widest = 0d;
        foreach (var option in options)
        {
            var probe = new TextBlock
            {
                Text = option.Label,
                FontSize = FontSize,
                FontWeight = FontWeight.Medium
            };
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var extra = 40 + (option.Icon is null ? 0 : 22) + (option.Badge is null ? 0 : 22);
            widest = Math.Max(widest, probe.DesiredSize.Width + extra);
        }

        return Math.Clamp(widest, 84, 168);
    }

    private void BuildSegment(int index, ArcGlassSegmentedOption option)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        ArcIcon? icon = null;
        if (!string.IsNullOrWhiteSpace(option.Icon))
        {
            icon = ArcGlass.Icon(option.Icon!, 15, ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary)));
            content.Children.Add(icon);
        }

        _icons.Add(icon);

        var label = new TextBlock
        {
            Text = option.Label,
            FontSize = FontSize,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        content.Children.Add(label);

        Border? badge = null;
        if (!string.IsNullOrWhiteSpace(option.Badge))
        {
            badge = new Border
            {
                CornerRadius = new CornerRadius(9999),
                Padding = new Thickness(5, 0),
                Height = 16,
                Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.22)),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = option.Badge,
                    FontSize = 10,
                    FontWeight = FontWeight.Medium,
                    Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentStrong)),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            content.Children.Add(badge);
        }

        _badges.Add(badge ?? new Border());

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Height = TrackHeight - (Pad * 2),
            MinHeight = TrackHeight - (Pad * 2),
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(9999),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = content,
            IsEnabled = !option.Disabled,
            FontSize = FontSize
        };
        button.Classes.Add("sg-plain");
        Grid.SetColumn(button, index);
        AutomationProperties.SetName(button, option.Label);
        AutomationProperties.SetHelpText(button, "Segment. Use the arrow keys to change the selection.");
        button.Click += (_, _) =>
        {
            if (_dragged)
            {
                _dragged = false;
                return;
            }

            Select(index, animate: true);
        };
        button.GotFocus += (_, _) => MovePillTo(index);
        button.KeyDown += (_, args) => OnSegmentKeyDown(index, args);
        button.PointerPressed += (_, args) => OnSegmentPressed(index, args);
        _segments.Add(button);
        _buttons.Children.Add(button);
    }

    private void OnSegmentPressed(int index, PointerPressedEventArgs e)
    {
        if (index != SelectedIndex() || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_dragging)
        {
            return;
        }

        _dragging = true;
        _dragged = false;
        _dragStartX = e.GetPosition(this).X;
        _dragStartLeft = _left;
        _lastX = _dragStartX;
        _lastTime = e.Timestamp;
        _velocity = 0;
        _tween?.Stop();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
        {
            return;
        }

        var x = e.GetPosition(this).X;
        if (Math.Abs(x - _dragStartX) > 3)
        {
            _dragged = true;
        }

        if (!_dragged)
        {
            return;
        }

        var width = _cell - 8;
        var left = Math.Clamp(_dragStartLeft + (x - _dragStartX), 0, Math.Max(0, OptionsWidth() - width));
        _left = left;
        _width = width;
        _height = TrackHeight - (Pad * 2) - 8;
        ApplyIndicator();

        var delta = e.Timestamp - _lastTime;
        if (delta > 0)
        {
            _velocity = (x - _lastX) / delta;
        }

        _lastX = x;
        _lastTime = e.Timestamp;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        if (!_dragged)
        {
            return;
        }

        var centre = _left + (_width / 2) + (_velocity * 180);
        var index = (int)Math.Round((centre - (Pad / 2)) / _cell);
        index = Math.Clamp(index, 0, OptionList().Count - 1);
        _dragged = true;
        Select(index, animate: true);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    private double OptionsWidth() => Math.Max(1, Bounds.Width - (Pad * 2));

    private void OnSegmentKeyDown(int index, KeyEventArgs e)
    {
        var options = OptionList();
        if (options.Count == 0)
        {
            return;
        }

        var next = index;
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                next = NextEnabled(index, 1);
                break;
            case Key.Left:
            case Key.Up:
                next = NextEnabled(index, -1);
                break;
            case Key.Home:
                next = options.FindIndex(option => !option.Disabled);
                break;
            case Key.End:
                next = options.FindLastIndex(option => !option.Disabled);
                break;
            default:
                return;
        }

        e.Handled = true;
        if (next < 0 || next >= _segments.Count)
        {
            return;
        }

        Select(next, animate: true);
        _segments[next].Focus();
    }

    private int NextEnabled(int index, int direction)
    {
        var options = OptionList();
        for (var step = 1; step <= options.Count; step++)
        {
            var candidate = (index + (direction * step) + options.Count * 10) % options.Count;
            if (!options[candidate].Disabled)
            {
                return candidate;
            }
        }

        return index;
    }

    private int ResolveIndex(string? value)
    {
        var options = OptionList();
        for (var i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Value, value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private int SelectedIndex()
    {
        var index = ResolveIndex(Value);
        return index < 0 ? 0 : index;
    }

    private void Select(int index, bool animate)
    {
        var options = OptionList();
        if (index < 0 || index >= options.Count || options[index].Disabled)
        {
            return;
        }

        var previous = SelectedIndex();
        UpdateSegmentFaces(index);
        UpdateRovingTabStop(index);
        SetCurrentValue(ValueProperty, options[index].Value);
        MovePillTo(index, animate && previous != index);
        ValueChanged?.Invoke(this, options[index].Value);
    }

    private void SyncValue(bool animate)
    {
        if (!_built)
        {
            return;
        }

        var index = ResolveIndex(Value);
        if (index < 0)
        {
            return;
        }

        UpdateSegmentFaces(index);
        UpdateRovingTabStop(index);
        MovePillTo(index, animate);
    }

    private void UpdateSegmentFaces(int selected)
    {
        var options = OptionList();
        for (var i = 0; i < _segments.Count && i < options.Count; i++)
        {
            var isSelected = i == selected;
            _segments[i].Foreground = ArcGlass.Solid(
                ArcGlass.Color(isSelected ? ArcTokens.AccentForeground : ArcTokens.TextSecondary));
            if (i < _icons.Count && _icons[i] is { } rowIcon)
            {
                rowIcon.Foreground = ArcGlass.Solid(
                    ArcGlass.Color(isSelected ? ArcTokens.AccentForeground : ArcTokens.TextSecondary));
            }

            _segments[i].FontWeight = isSelected ? FontWeight.Medium : FontWeight.Normal;
            AutomationProperties.SetItemStatus(_segments[i], isSelected ? "Selected" : string.Empty);

            if (_badges.Count > i && _badges[i].Child is TextBlock badgeText && options[i].Badge is not null)
            {
                badgeText.Foreground = ArcGlass.Solid(
                    ArcGlass.Color(isSelected ? ArcTokens.AccentForeground : ArcTokens.TextSecondary));
                _badges[i].Background = ArcGlass.Solid(ArcGlass.ArcAlpha(
                    isSelected ? ArcTokens.AccentForeground : ArcTokens.TextSecondary,
                    isSelected ? 0.20 : 0.14));
            }
        }
    }

    private void UpdateRovingTabStop(int selected)
    {
        for (var i = 0; i < _segments.Count; i++)
        {
            _segments[i].IsTabStop = i == selected;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var pad = Pad;
        if (FullWidth && !double.IsInfinity(availableSize.Width) && availableSize.Width > 0)
        {
            _track.Width = Math.Max(0, availableSize.Width);
        }
        else if (!FullWidth && double.IsNaN(_track.Width) && Options?.Count > 0)
        {
            _track.Width = (_cell * Options!.Count) + (pad * 2);
        }

        _track.Height = TrackHeight;
        _track.Measure(availableSize);
        return new Size(
            FullWidth && !double.IsInfinity(availableSize.Width) ? availableSize.Width : _track.DesiredSize.Width,
            TrackHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pad = Pad;
        var width = FullWidth ? Math.Max(0, finalSize.Width - (pad * 2)) : Math.Max(0, finalSize.Width - (pad * 2));
        var count = Math.Max(1, OptionList().Count);
        _cell = width / count;
        _track.Width = finalSize.Width;
        _track.Arrange(new Rect(finalSize));
        if (_built && !_dragging)
        {
            SnapIndicator(SelectedIndex());
        }

        return finalSize;
    }

    private void SnapIndicator(int index)
    {
        var width = Math.Max(0, Bounds.Width - (Pad * 2));
        var count = Math.Max(1, OptionList().Count);
        _cell = width / count;
        var inset = 4.0;
        _left = (index * _cell) + inset;
        _width = Math.Max(24, _cell - (inset * 2));
        _height = TrackHeight - (Pad * 2) - 8;
        ApplyIndicator();
    }

    private void MovePillTo(int index, bool animate = true)
    {
        var width = Math.Max(1, Bounds.Width - (Pad * 2));
        var count = Math.Max(1, OptionList().Count);
        var cell = width / count;
        var inset = 4.0;
        var targetLeft = (index * cell) + inset;
        var targetWidth = Math.Max(24, cell - (inset * 2));
        var targetHeight = TrackHeight - (Pad * 2) - 8;

        _tween?.Stop();
        if (!animate || ArcThemeManager.ReducedMotion || _segments.Count <= 1)
        {
            _left = targetLeft;
            _width = targetWidth;
            _height = targetHeight;
            ApplyIndicator();
            return;
        }

        var fromLeft = _left;
        var fromWidth = _width;
        var fromHeight = _height;
        var direction = targetLeft >= fromLeft ? 1 : -1;
        // Expand towards the travel first: the leading edge reaches ahead, the trailing edge lags.
        var stretchLeft = direction > 0 ? fromLeft : Math.Min(fromLeft, targetLeft);
        var stretchRight = direction > 0
            ? Math.Max(fromLeft + fromWidth, targetLeft + targetWidth)
            : fromLeft + fromWidth;
        var stretchWidth = Math.Max(targetWidth, stretchRight - stretchLeft);
        var stretchedHeight = Math.Max(18, targetHeight - 6);

        _tween = GlassTween.Run(GlassMotion.Morph, GlassMotion.MorphEase, progress =>
        {
            if (progress <= 0.42)
            {
                var local = progress / 0.42;
                _left = fromLeft + ((stretchLeft - fromLeft) * local);
                _width = fromWidth + ((stretchWidth - fromWidth) * local);
                _height = fromHeight + ((stretchedHeight - fromHeight) * local);
            }
            else
            {
                var local = (progress - 0.42) / 0.58;
                _left = stretchLeft + ((targetLeft - stretchLeft) * local);
                _width = stretchWidth + ((targetWidth - stretchWidth) * local);
                _height = stretchedHeight + ((targetHeight - stretchedHeight) * local);
            }

            ApplyIndicator();
        });
    }

    private void ApplyIndicator()
    {
        Canvas.SetLeft(_indicator, _left);
        Canvas.SetTop(_indicator, (TrackHeight - _height) / 2);
        _indicator.Width = Math.Max(1, _width);
        _indicator.Height = Math.Max(1, _height);
        _indicator.CornerRadius = new CornerRadius(9999);
    }
}







