using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

/// <summary>One used category in an <see cref="ArcUsageMeter"/>.</summary>
public sealed class ArcUsageSegment
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public double Value { get; set; }
}

/// <summary>
/// An allowance bar: used categories against a fixed limit, the free remainder, a percentage,
/// threshold tones, and a caption such as "8.4k of 10k requests".
/// </summary>
public class ArcUsageMeter : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcUsageMeter, string>(nameof(Label), "Usage");

    public static readonly StyledProperty<IEnumerable<ArcUsageSegment>?> SegmentsProperty =
        AvaloniaProperty.Register<ArcUsageMeter, IEnumerable<ArcUsageSegment>?>(nameof(Segments));

    public static readonly StyledProperty<double> LimitProperty =
        AvaloniaProperty.Register<ArcUsageMeter, double>(nameof(Limit), 100d);

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<ArcUsageMeter, string>(nameof(Unit), string.Empty);

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<ArcUsageMeter, int>(nameof(Decimals), 1);

    public static readonly StyledProperty<string> FreeLabelProperty =
        AvaloniaProperty.Register<ArcUsageMeter, string>(nameof(FreeLabel), "Free");

    public static readonly StyledProperty<string> OverLabelProperty =
        AvaloniaProperty.Register<ArcUsageMeter, string>(nameof(OverLabel), "Over limit");

    public static readonly StyledProperty<double> WarnAtProperty =
        AvaloniaProperty.Register<ArcUsageMeter, double>(nameof(WarnAt), 0.9);

    private Panel? _host;
    private ArcUsageBar? _bar;
    private readonly List<Border> _legendItems = new();
    private int _pinned = -1;
    private int _hover = -1;

    static ArcUsageMeter()
    {
        LabelProperty.Changed.AddClassHandler<ArcUsageMeter>((meter, _) => meter.Rebuild());
        SegmentsProperty.Changed.AddClassHandler<ArcUsageMeter>((meter, _) => meter.Rebuild());
        LimitProperty.Changed.AddClassHandler<ArcUsageMeter>((meter, _) => meter.Rebuild());
        UnitProperty.Changed.AddClassHandler<ArcUsageMeter>((meter, _) => meter.Rebuild());
        DecimalsProperty.Changed.AddClassHandler<ArcUsageMeter>((meter, _) => meter.Rebuild());
        FreeLabelProperty.Changed.AddClassHandler<ArcUsageMeter>((meter, _) => meter.Rebuild());
        OverLabelProperty.Changed.AddClassHandler<ArcUsageMeter>((meter, _) => meter.Rebuild());
        WarnAtProperty.Changed.AddClassHandler<ArcUsageMeter>((meter, _) => meter.Rebuild());
    }

    public ArcUsageMeter()
    {
        Focusable = true;
    }

    /// <summary>What is measured, such as "Workspace storage".</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Used amounts in display order; up to four read clearly.</summary>
    public IEnumerable<ArcUsageSegment>? Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <summary>The plan limit, in the same unit as the segments.</summary>
    public double Limit
    {
        get => GetValue(LimitProperty);
        set => SetValue(LimitProperty, value);
    }

    /// <summary>Unit shown after numbers, such as "GB".</summary>
    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public int Decimals
    {
        get => GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    public string FreeLabel
    {
        get => GetValue(FreeLabelProperty);
        set => SetValue(FreeLabelProperty, value);
    }

    public string OverLabel
    {
        get => GetValue(OverLabelProperty);
        set => SetValue(OverLabelProperty, value);
    }

    /// <summary>Share of the limit at which the badge warns that it is almost full.</summary>
    public double WarnAt
    {
        get => GetValue(WarnAtProperty);
        set => SetValue(WarnAtProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        Rebuild();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = _legendItems.Count;
        if (count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                SetPinned((_pinned <= 0 ? count : _pinned) - 1);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Down:
                SetPinned((_pinned + 1) % count);
                e.Handled = true;
                break;
            case Key.Home:
                SetPinned(0);
                e.Handled = true;
                break;
            case Key.End:
                SetPinned(count - 1);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                SetPinned(_pinned >= 0 ? -1 : Math.Max(0, _hover));
                e.Handled = true;
                break;
            case Key.Escape:
                SetPinned(-1);
                e.Handled = true;
                break;
        }
    }

    private void SetPinned(int index)
    {
        _pinned = index;
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        var highlight = _pinned >= 0 ? _pinned : _hover;
        if (_bar is not null)
        {
            _bar.Highlight = highlight;
        }

        for (var i = 0; i < _legendItems.Count; i++)
        {
            var item = _legendItems[i];
            item.Opacity = highlight < 0 || highlight == i ? 1 : 0.45;
            if (highlight == i)
            {
                item.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            }
            else
            {
                item.Background = Brushes.Transparent;
            }
        }
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _legendItems.Clear();
        _pinned = -1;
        _hover = -1;

        var segments = (Segments ?? Array.Empty<ArcUsageSegment>()).ToList();
        var used = segments.Sum(segment => Math.Max(0, segment.Value));
        var limit = Math.Max(0, Limit);
        var share = limit <= 0 ? 0 : used / limit;
        var over = used > limit;
        var warn = !over && share >= Math.Clamp(WarnAt, 0, 1);

        var root = new StackPanel { Spacing = 12 };

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        header.Children.Add(FeedbackUi.Text(Label, "arc-label"));
        var badge = BuildBadge(share, over, warn);
        Grid.SetColumn(badge, 1);
        header.Children.Add(badge);
        root.Children.Add(header);

        var fractions = segments.Select(segment => limit <= 0 ? 0 : Math.Max(0, segment.Value) / limit).ToList();
        _bar = new ArcUsageBar { Fractions = fractions, Height = 10, MinHeight = 10 };
        root.Children.Add(_bar);

        var noun = string.IsNullOrWhiteSpace(Unit) ? Label.ToLowerInvariant() : Unit;
        var caption = FeedbackUi.Text(
            $"{FeedbackUi.CompactNumber(used)} of {FeedbackUi.CompactNumber(limit)} {noun}",
            "arc-caption");
        caption.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        root.Children.Add(caption);

        var legend = new WrapPanel { Orientation = Orientation.Horizontal };
        var unitText = string.IsNullOrWhiteSpace(Unit) ? string.Empty : " " + Unit;
        for (var i = 0; i < segments.Count; i++)
        {
            var index = i;
            var item = BuildLegendItem(
                FeedbackUi.SeriesKey(i),
                segments[i].Label,
                $"{segments[i].Value.ToString("0." + new string('#', Math.Max(0, Decimals)), CultureInfo.InvariantCulture)}{unitText}",
                index);
            item.PointerEntered += (_, _) =>
            {
                _hover = index;
                UpdateHighlight();
            };
            item.PointerExited += (_, _) =>
            {
                if (_hover == index)
                {
                    _hover = -1;
                    UpdateHighlight();
                }
            };
            item.PointerPressed += (_, args) =>
            {
                Focus();
                SetPinned(_pinned == index ? -1 : index);
                args.Handled = true;
            };
            _legendItems.Add(item);
            legend.Children.Add(item);
        }

        var remainder = limit - used;
        var remainderItem = BuildLegendItem(
            over ? ArcTokens.Danger : ArcTokens.ControlTrack,
            over ? OverLabel : FreeLabel,
            over
                ? $"{FeedbackUi.CompactNumber(Math.Abs(remainder))}{unitText}"
                : $"{remainder.ToString("0." + new string('#', Math.Max(0, Decimals)), CultureInfo.InvariantCulture)}{unitText}",
            segments.Count);
        _legendItems.Add(remainderItem);
        legend.Children.Add(remainderItem);

        root.Children.Add(legend);
        _host.Children.Add(root);

        UpdateHighlight();
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetHelpText(this, $"{FeedbackUi.CompactNumber(used)} of {FeedbackUi.CompactNumber(limit)} {noun}, {Math.Round(share * 100)} percent");
    }

    private Control BuildBadge(double share, bool over, bool warn)
    {
        var percent = Math.Round(share * 100);
        var text = over ? OverLabel : warn ? "Almost full" : $"{percent}% used";
        var background = over ? ArcTokens.DangerSubtle : warn ? ArcTokens.WarningSubtle : ArcTokens.SurfaceMuted;
        var foreground = over ? ArcTokens.Danger : warn ? ArcTokens.Warning : ArcTokens.TextSecondary;

        var block = FeedbackUi.Text(text);
        block.FontSize = ArcResources.Double(ArcTokens.TextXs);
        block.FontWeight = FontWeight.Medium;
        var pill = FeedbackUi.Pill(block, background, foreground);
        if (over || warn)
        {
            block.FontFeatures = FeedbackUi.Tabular;
        }

        return pill;
    }

    private Border BuildLegendItem(string token, string name, string value, int index)
    {
        var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
        dot.Paint(Border.BackgroundProperty, token);

        var label = FeedbackUi.Text(name, "arc-caption");
        label.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        var amount = FeedbackUi.Numeric(value, "arc-caption");
        amount.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);

        var row = FeedbackUi.Row(6, dot, label, amount);
        var item = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 3, 8, 3),
            Margin = new Thickness(0, 4, 16, 4),
            Background = Brushes.Transparent,
            Child = row
        };
        item.Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetName(item, $"{name}, {value}");
        return item;
    }
}

/// <summary>The segmented fill; segment colours come from the Arc series tokens.</summary>
public sealed class ArcUsageBar : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> FractionsProperty =
        AvaloniaProperty.Register<ArcUsageBar, IReadOnlyList<double>?>(nameof(Fractions));

    public static readonly StyledProperty<int> HighlightProperty =
        AvaloniaProperty.Register<ArcUsageBar, int>(nameof(Highlight), -1);

    static ArcUsageBar()
    {
        AffectsRender<ArcUsageBar>(FractionsProperty, HighlightProperty);
    }

    public IReadOnlyList<double>? Fractions
    {
        get => GetValue(FractionsProperty);
        set => SetValue(FractionsProperty, value);
    }

    public int Highlight
    {
        get => GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, 10);

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var height = Math.Max(4, Math.Min(Bounds.Height, 14));
        var radius = height / 2;
        var top = (Bounds.Height - height) / 2;
        var track = new Rect(0, top, Bounds.Width, height);
        context.DrawRectangle(ArcResources.Brush(ArcTokens.ControlTrack), null, track, radius, radius);

        var fractions = Fractions;
        if (fractions is null || fractions.Count == 0)
        {
            return;
        }

        var cursor = 0.0;
        var gap = 2.0;
        using (context.PushClip(new RoundedRect(track, radius)))
        {
            for (var i = 0; i < fractions.Count; i++)
            {
                var width = Math.Max(0, fractions[i] * Bounds.Width);
                if (width <= 0.2)
                {
                    continue;
                }

                var left = cursor;
                var drawWidth = Math.Min(width, Bounds.Width - left);
                if (drawWidth <= 0.2)
                {
                    break;
                }

                var opacity = Highlight < 0 || Highlight == i ? 1 : 0.42;
                using (context.PushOpacity(opacity))
                {
                    context.DrawRectangle(ArcResources.Brush(FeedbackUi.SeriesKey(i)), null, new Rect(left, top, drawWidth, height), radius, radius);
                }

                cursor += drawWidth + gap;
            }
        }
    }
}
