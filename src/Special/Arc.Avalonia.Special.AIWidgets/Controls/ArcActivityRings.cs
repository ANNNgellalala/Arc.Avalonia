using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIWidgets.Controls;

/// <summary>One daily goal drawn as a ring.</summary>
public sealed class ArcRingMetric
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public double Goal { get; set; }

    /// <summary>Token key; defaults to the series tokens in ring order.</summary>
    public string? ColorToken { get; set; }
}

/// <summary>One day of values keyed by metric id.</summary>
public sealed class ArcRingDay
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Full date for assistive technology.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Two or three letters under the day.</summary>
    public string Short { get; set; } = string.Empty;

    public Dictionary<string, double> Values { get; set; } = new();
}

/// <summary>
/// Daily goals as concentric tick rings. Arcs sweep to each day's value, a second lap traces the inside edge past
/// 100%, and the numbers count to their new amounts. A week picker changes the day and the legend isolates one
/// goal so the rest quiet down.
///
/// Platform approximations: the rings are drawn in one control per frame instead of SVG arcs driven by motion
/// values, so a value change sweeps the whole set together rather than each ring on its own spring.
/// </summary>
public class ArcActivityRings : Panel
{
    public static readonly StyledProperty<IEnumerable<ArcRingMetric>?> MetricsProperty =
        AvaloniaProperty.Register<ArcActivityRings, IEnumerable<ArcRingMetric>?>(nameof(Metrics));

    public static readonly StyledProperty<IEnumerable<ArcRingDay>?> DaysProperty =
        AvaloniaProperty.Register<ArcActivityRings, IEnumerable<ArcRingDay>?>(nameof(Days));

    public static readonly StyledProperty<string?> DayProperty =
        AvaloniaProperty.Register<ArcActivityRings, string?>(nameof(Day));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcActivityRings, string>(nameof(Label), "Activity");

    private readonly List<ArcRingMetric> _metrics = new();
    private readonly List<ArcRingDay> _days = new();
    private readonly ArcRingsView _view = new();
    private readonly SvTween _tween = new();
    private TextBlock? _centerValue;
    private TextBlock? _centerCaption;
    private int _isolated = -1;
    private string? _currentDay;
    private double[] _from = Array.Empty<double>();
    private double[] _to = Array.Empty<double>();
    private double[] _display = Array.Empty<double>();

    static ArcActivityRings()
    {
        MetricsProperty.Changed.AddClassHandler<ArcActivityRings>((rings, _) => rings.Load());
        DaysProperty.Changed.AddClassHandler<ArcActivityRings>((rings, _) => rings.Load());
        DayProperty.Changed.AddClassHandler<ArcActivityRings>((rings, _) => rings.ChangeDay());
    }

    public ArcActivityRings()
    {
        ClipToBounds = false;
        Rebuild();
    }

    public IEnumerable<ArcRingMetric>? Metrics
    {
        get => GetValue(MetricsProperty);
        set => SetValue(MetricsProperty, value);
    }

    public IEnumerable<ArcRingDay>? Days
    {
        get => GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    public string? Day
    {
        get => GetValue(DayProperty);
        set => SetValue(DayProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private ArcRingDay? Selected =>
        _days.FirstOrDefault(day => day.Id == (_currentDay ?? Day)) ?? _days.LastOrDefault();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Load();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tween.Stop();
    }

    private void Load()
    {
        if (Metrics is not null)
        {
            _metrics.Clear();
            _metrics.AddRange(Metrics);
        }

        if (Days is not null)
        {
            _days.Clear();
            _days.AddRange(Days);
        }

        _currentDay ??= Day ?? _days.LastOrDefault()?.Id;
        SyncValues(animate: false);
        Rebuild();
    }

    private void ChangeDay()
    {
        _currentDay = Day;
        SyncValues(animate: true);
        Rebuild();
    }

    private void SyncValues(bool animate)
    {
        var day = Selected;
        _from = _display.Length == _metrics.Count ? _display.ToArray() : new double[_metrics.Count];
        _to = new double[_metrics.Count];
        _display = new double[_metrics.Count];

        for (var i = 0; i < _metrics.Count; i++)
        {
            _to[i] = day is not null && day.Values.TryGetValue(_metrics[i].Id, out var value) ? value : 0;
            _display[i] = animate && _from.Length == _metrics.Count ? _from[i] : _to[i];
        }

        if (!animate)
        {
            Array.Copy(_to, _display, _to.Length);
            _view.SetValues(_metrics, _display);
            return;
        }

        var from = _from.ToArray();
        var to = _to.ToArray();
        _tween.Run(0, 1, 520, t =>
        {
            for (var i = 0; i < _display.Length; i++)
            {
                _display[i] = from[i] + (to[i] - from[i]) * t;
            }

            _view.SetValues(_metrics, _display);
            UpdateCenter();
            UpdateLegendValues();
        }, () =>
        {
            Array.Copy(to, _display, to.Length);
            _view.SetValues(_metrics, _display);
            UpdateCenter();
            UpdateLegendValues();
        });
    }

    private void Rebuild()
    {
        Children.Clear();
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = 26 };

        var left = new StackPanel { Spacing = 10 };
        var ringHost = new Panel { Width = 236, Height = 236, HorizontalAlignment = HorizontalAlignment.Left };

        Sv.Detach(_view);
        _view.Width = 236;
        _view.Height = 236;
        _view.SetValues(_metrics, _display);
        _view.Isolated = _isolated;
        _view.Highlight = _isolated;
        ringHost.Children.Add(_view);

        var center = new StackPanel
        {
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsHitTestVisible = false
        };
        _centerCaption = Sv.Caption(Selected?.Short ?? "Today");
        _centerValue = new TextBlock
        {
            Text = "0%",
            FontSize = Sv.Size(ArcTokens.Text2xl),
            FontWeight = FontWeight.Medium,
            FontFeatures = Sv.Tabular,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        center.Children.Add(_centerCaption);
        center.Children.Add(_centerValue);
        ringHost.Children.Add(center);
        UpdateCenter();
        left.Children.Add(ringHost);
        left.Children.Add(BuildWeekPicker());
        root.Children.Add(left);

        var legend = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Top, MinWidth = 258 };
        legend.Children.Add(Sv.Caption(Label));
        _legendRows.Clear();
        _legendValues.Clear();
        for (var i = 0; i < _metrics.Count; i++)
        {
            var row = BuildLegendRow(i);
            _legendRows.Add(row);
            legend.Children.Add(row);
        }

        legend.Children.Add(BuildTotals());
        Grid.SetColumn(legend, 1);
        root.Children.Add(legend);

        Children.Add(root);
        UpdateLegendValues();
        UpdateAutomation();
    }

    private readonly List<Border> _legendRows = new();
    private readonly List<TextBlock> _legendValues = new();

    private Control BuildWeekPicker()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var day in _days)
        {
            var selected = day.Id == Selected?.Id;
            var label = new TextBlock
            {
                Text = day.Short,
                FontSize = Sv.Size(ArcTokens.TextXs),
                FontWeight = selected ? FontWeight.Medium : FontWeight.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            label.Paint(TextBlock.ForegroundProperty, selected ? ArcTokens.Accent : ArcTokens.TextMuted);

            var cell = new Border
            {
                Width = 30,
                Height = 28,
                CornerRadius = new CornerRadius(9),
                Child = label,
                Cursor = new Cursor(StandardCursorType.Hand),
                Background = selected ? Sv.Brush(ArcTokens.AccentSubtle) : Brushes.Transparent,
                Focusable = true
            };
            var id = day.Id;
            cell.PointerPressed += (_, args) =>
            {
                _currentDay = id;
                SetCurrentValue(DayProperty, id);
                ChangeDay();
                args.Handled = true;
            };
            cell.KeyDown += (_, args) =>
            {
                if (args.Key is Key.Left or Key.Right or Key.Home or Key.End)
                {
                    var index = _days.IndexOf(day);
                    index = args.Key switch
                    {
                        Key.Home => 0,
                        Key.End => _days.Count - 1,
                        Key.Left => Math.Max(0, index - 1),
                        _ => Math.Min(_days.Count - 1, index + 1)
                    };
                    var target = _days[index];
                    SetCurrentValue(DayProperty, target.Id);
                    _currentDay = target.Id;
                    ChangeDay();
                    args.Handled = true;
                }
                else if (args.Key is Key.Enter or Key.Space)
                {
                    SetCurrentValue(DayProperty, id);
                    _currentDay = id;
                    ChangeDay();
                    args.Handled = true;
                }
            };
            cell.GotFocus += (_, _) => cell.Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
            cell.LostFocus += (_, _) => cell.BorderBrush = Brushes.Transparent;
            cell.BorderThickness = new Thickness(2);
            AutomationProperties.SetName(cell, day.Label);
            row.Children.Add(cell);
        }

        return row;
    }

    private Border BuildLegendRow(int index)
    {
        var metric = _metrics[index];
        var token = TokenFor(metric, index);
        var dot = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center };
        dot.Paint(Border.BackgroundProperty, token);

        var name = new TextBlock { Text = metric.Label, FontSize = Sv.Size(ArcTokens.TextSm), FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
        var value = Sv.Numeric("0", "arc-body-sm");
        _legendValues.Add(value);
        var unit = Sv.Caption($"/ {metric.Goal:0} {metric.Unit}");

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
        grid.Children.Add(dot);
        name.Margin = new Thickness(8, 0, 8, 0);
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        unit.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(unit, 3);
        grid.Children.Add(unit);

        var row = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 7, 10, 7),
            Child = grid,
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = _isolated == index ? Sv.Brush(ArcTokens.SurfaceMuted) : Brushes.Transparent,
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Focusable = true
        };
        row.PointerEntered += (_, _) => SetIsolated(index);
        row.PointerExited += (_, _) => SetIsolated(-1);
        row.PointerPressed += (_, args) =>
        {
            row.Focus();
            SetIsolated(_isolated == index ? -1 : index);
            args.Handled = true;
        };
        row.KeyDown += (_, args) =>
        {
            if (args.Key is Key.Enter or Key.Space)
            {
                SetIsolated(_isolated == index ? -1 : index);
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                SetIsolated(-1);
                args.Handled = true;
            }
        };
        row.GotFocus += (_, _) => row.Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
        row.LostFocus += (_, _) => row.BorderBrush = Brushes.Transparent;
        AutomationProperties.SetName(row, $"{metric.Label}, toggle to isolate this goal");
        return row;
    }

    private Control BuildTotals()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(8, 8, 10, 0) };
        var label = Sv.Caption("Goals reached");
        grid.Children.Add(label);
        var value = Sv.Numeric($"{Closed()} of {_metrics.Count}", "arc-caption");
        Grid.SetColumn(value, 1);
        grid.Children.Add(value);
        var border = new Border { Child = grid, Padding = new Thickness(0, 4, 0, 0) };
        border.BorderThickness = new Thickness(0, 1, 0, 0);
        border.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        return border;
    }

    private int Closed()
    {
        var count = 0;
        for (var i = 0; i < _metrics.Count; i++)
        {
            if (_display.Length > i && _metrics[i].Goal > 0 && _display[i] >= _metrics[i].Goal)
            {
                count++;
            }
        }

        return count;
    }

    private void SetIsolated(int index)
    {
        if (_isolated == index)
        {
            return;
        }

        _isolated = index;
        _view.Isolated = index;
        _view.InvalidateVisual();
        for (var i = 0; i < _legendRows.Count; i++)
        {
            _legendRows[i].Background = _isolated == i ? Sv.Brush(ArcTokens.SurfaceMuted) : Brushes.Transparent;
            _legendRows[i].Opacity = _isolated < 0 || _isolated == i ? 1 : 0.5;
        }
    }

    private void UpdateCenter()
    {
        if (_centerValue is null || _metrics.Count == 0)
        {
            return;
        }

        var index = _isolated >= 0 && _isolated < _metrics.Count ? _isolated : 0;
        var metric = _metrics[index];
        var value = _display.Length > index ? _display[index] : 0;
        var percent = metric.Goal <= 0 ? 0 : value / metric.Goal * 100;
        _centerValue.Text = $"{Math.Round(percent)}%";
        if (_centerCaption is not null)
        {
            _centerCaption.Text = $"{(Selected?.Short ?? "Today")} · {metric.Label}";
        }
    }

    private void UpdateLegendValues()
    {
        for (var i = 0; i < _legendValues.Count && i < _metrics.Count; i++)
        {
            var value = _display.Length > i ? _display[i] : 0;
            _legendValues[i].Text = Math.Round(value).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string TokenFor(ArcRingMetric metric, int index) =>
        metric.ColorToken ?? (index switch
        {
            0 => ArcTokens.Series1,
            1 => ArcTokens.Series2,
            2 => ArcTokens.Series3,
            _ => ArcTokens.Series4
        });

    private void UpdateAutomation()
    {
        var day = Selected;
        var parts = _metrics.Select((metric, index) =>
        {
            var value = day is not null && day.Values.TryGetValue(metric.Id, out var v) ? v : 0;
            var percent = metric.Goal <= 0 ? 0 : Math.Round(value / metric.Goal * 100);
            return $"{metric.Label} {Math.Round(value)} of {metric.Goal:0} {metric.Unit}, {percent} percent";
        });
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetHelpText(this, $"{day?.Label ?? "Today"}. {string.Join(". ", parts)}.");
    }
}

/// <summary>The drawn tick rings: a segmented track, a swept value arc and a second lap inside past 100%.</summary>
public sealed class ArcRingsView : Control
{
    private IReadOnlyList<ArcRingMetric> _metrics = Array.Empty<ArcRingMetric>();
    private double[] _values = Array.Empty<double>();

    public int Isolated { get; set; } = -1;

    public int Highlight { get; set; } = -1;

    public void SetValues(IReadOnlyList<ArcRingMetric> metrics, double[] values)
    {
        _metrics = metrics;
        _values = values;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(236, 236);

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0 || _metrics.Count == 0)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var scale = size / 236.0;
        var thickness = 14 * scale;
        var outer = 102 * scale;
        const int tickCount = 56;
        var step = 360.0 / tickCount;

        for (var i = 0; i < _metrics.Count; i++)
        {
            var radius = outer - i * (thickness + 8 * scale);
            var metric = _metrics[i];
            var token = metric.ColorToken ?? (i switch
            {
                0 => ArcTokens.Series1,
                1 => ArcTokens.Series2,
                2 => ArcTokens.Series3,
                _ => ArcTokens.Series4
            });
            var dim = Isolated >= 0 && Isolated != i;

            using (context.PushOpacity(dim ? 0.2 : 1))
            {
                var value = _values.Length > i ? Math.Max(0, _values[i]) : 0;
                var goal = Math.Max(0.0001, metric.Goal);
                var share = value / goal;
                var sweep = Math.Min(1, share) * 360;
                var filled = Sv.Brush(token);
                var track = Sv.AlphaBrush(ArcTokens.BorderStrong, 0.85);

                // The ring is a ring of ticks: the track stays quiet, the filled share takes the goal colour.
                for (var tick = 0; tick < tickCount; tick++)
                {
                    var start = -90 + tick * step + step * 0.18;
                    var delta = tick * step;
                    var on = sweep > 0 && delta <= sweep - step * 0.4;
                    var pen = new Pen(on ? filled : track, thickness * 0.92, null, PenLineCap.Flat);
                    SvDraw.Arc(context, center, radius, start, step * 0.64, pen);
                }

                // Past 100% a second lap traces the inside edge, so exceeding a goal reads differently.
                if (share > 1)
                {
                    var lap = Math.Min(1, share - 1) * 360;
                    var innerRadius = radius - thickness * 0.5 - 4 * scale;
                    SvDraw.Arc(context, center, innerRadius, -90, lap, new Pen(Sv.AlphaBrush(token, 0.95), Math.Max(2, thickness * 0.46), null, PenLineCap.Flat));
                }
            }
        }
    }
}
