using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIWidgets.Controls;

/// <summary>One row in the demo feed.</summary>
public sealed class ArcFeedItem
{
    public string Id { get; set; } = string.Empty;

    public string Icon { get; set; } = "bell";

    public string Title { get; set; } = string.Empty;

    public string Meta { get; set; } = string.Empty;

    public string Time { get; set; } = string.Empty;
}

/// <summary>
/// A feed that refreshes when pulled past its top edge. The rows rubber-band apart, a line in the gap stretches to
/// show when to let go, then it runs while the work happens and gives way to the result as new rows settle in. A
/// refresh button gives keyboard and switch users the same path as the gesture.
///
/// Platform approximations: there is no scroll container inside the demo feed, so the pull is a pointer drag rather
/// than a scroll-over-the-top handoff, and the stretch line is drawn rather than a DOM element that gains height.
/// </summary>
public class ArcStretchRefresh : Panel
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcStretchRefresh, string>(nameof(Title), "Inbox");

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ArcStretchRefresh, string?>(nameof(Subtitle), "Updated 14 min ago");

    public static readonly StyledProperty<IEnumerable<ArcFeedItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcStretchRefresh, IEnumerable<ArcFeedItem>?>(nameof(Items));

    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<ArcStretchRefresh, double>(nameof(Threshold), 72d);

    private readonly List<ArcFeedItem> _items = new();
    private readonly StackPanel _rows = new() { Spacing = 0 };
    private readonly ArcPullIndicator _indicator = new();
    private TextBlock? _subtitleBlock;
    private bool _refreshing;

    private double _pull;
    private double _startY;


    static ArcStretchRefresh()
    {
        ItemsProperty.Changed.AddClassHandler<ArcStretchRefresh>((feed, _) => feed.BuildRows());
        TitleProperty.Changed.AddClassHandler<ArcStretchRefresh>((feed, _) => feed.Rebuild());
        SubtitleProperty.Changed.AddClassHandler<ArcStretchRefresh>((feed, _) => feed.Rebuild());
        ThresholdProperty.Changed.AddClassHandler<ArcStretchRefresh>((feed, _) => feed.ApplyPull(feed._pull));
    }

    public ArcStretchRefresh()
    {
        ClipToBounds = false;
        Rebuild();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public IEnumerable<ArcFeedItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Pull distance, after resistance, that arms a refresh.</summary>
    public double Threshold
    {
        get => GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    /// <summary>Holds the feed at a fixed pull distance; used by the documentation demo for a still frame.</summary>
    public void HoldPreview(double fraction)
    {
        ApplyPull(Math.Max(0, Threshold) * Math.Clamp(fraction, 0, 1.2));
        _indicator.Refreshing = false;
        _indicator.Result = string.Empty;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Items is not null)
        {
            _items.Clear();
            _items.AddRange(Items);
        }

        BuildRows();
        ApplyPull(_pull);
    }

    private void Rebuild()
    {
        Children.Clear();
        var root = new StackPanel { Spacing = 12 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = Title, Classes = { "arc-label" } });
        _subtitleBlock = Sv.Caption(Subtitle ?? string.Empty);
        titles.Children.Add(_subtitleBlock);
        header.Children.Add(titles);

        var refresh = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { Sv.Icon(_refreshing ? "loader-circle" : "refresh-cw", 14), new TextBlock { Text = "Refresh", FontSize = 13 } }
            }
        };
        refresh.Click += (_, _) => BeginRefresh();
        AutomationProperties.SetName(refresh, _refreshing ? "Refreshing, please wait" : "Refresh the feed");
        Grid.SetColumn(refresh, 1);
        header.Children.Add(refresh);
        root.Children.Add(header);

        Sv.Detach(_indicator);
        Sv.Detach(_rows);
        var viewport = new Panel();
        viewport.Children.Add(_indicator);
        viewport.Children.Add(_rows);
        var feed = new Border
        {
            CornerRadius = Sv.Radius(ArcTokens.RadiusPanel),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Height = 320,
            Child = viewport
        };
        feed.Paint(Border.BackgroundProperty, ArcTokens.Surface);
        feed.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        feed.PointerPressed += OnFeedPointerPressed;
        feed.PointerMoved += OnFeedPointerMoved;
        feed.PointerReleased += OnFeedPointerReleased;
        root.Children.Add(feed);

        Children.Add(root);

        _rows.Margin = new Thickness(6, 6, 6, 6);
        BuildRows();
        ApplyPull(_pull);
        AutomationProperties.SetName(this, $"{Title} feed");
        AutomationProperties.SetHelpText(this, "Pull down to refresh, or use the refresh button. The list is marked busy while refreshing.");
    }

    private void BuildRows()
    {
        if (Items is not null && _items.Count == 0)
        {
            _items.AddRange(Items);
        }

        _rows.Children.Clear();
        if (_items.Count == 0 && Items is not null)
        {
            _items.AddRange(Items);
        }

        foreach (var item in _items)
        {
            _rows.Children.Add(BuildRow(item));
        }
    }

    private Control BuildRow(ArcFeedItem item)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Height = 56 };
        var plate = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(10),
            Child = Sv.Icon(item.Icon, 15, ArcTokens.TextSecondary),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 10, 0)
        };
        plate.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        grid.Children.Add(plate);

        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { Text = item.Title, FontSize = Sv.Size(ArcTokens.TextSm), FontWeight = FontWeight.Medium, TextTrimming = TextTrimming.CharacterEllipsis };
        title.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
        text.Children.Add(title);
        text.Children.Add(Sv.Caption(item.Meta));
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var time = Sv.Numeric(item.Time, "arc-caption");
        time.VerticalAlignment = VerticalAlignment.Center;
        time.Margin = new Thickness(10, 0, 12, 0);
        Grid.SetColumn(time, 2);
        grid.Children.Add(time);

        var row = new Border { Child = grid, CornerRadius = new CornerRadius(14) };
        return row;
    }

    private void OnFeedPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _startY = e.GetPosition(this).Y;
        e.Pointer.Capture((IInputElement?)sender);
        e.Handled = true;
    }

    private void OnFeedPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _refreshing)
        {
            return;
        }

        var dy = e.GetPosition(this).Y - _startY;
        if (dy <= 0)
        {
            ApplyPull(0);
            return;
        }

        // Growing resistance: the sheet follows the finger less the further it goes.
        ApplyPull(Math.Min(150, dy * 0.55));
    }

    private void OnFeedPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        if (_pull >= Threshold && !_refreshing)
        {
            BeginRefresh();
            return;
        }

        Settle();
    }

    private void Settle()
    {
        var from = _pull;
        var tween = new SvTween();
        tween.Run(from, 0, 260, value => ApplyPull(value), () => ApplyPull(0));
    }

    private void ApplyPull(double pull)
    {
        _pull = Math.Max(0, pull);
        _indicator.Pull = _pull;
        _indicator.Threshold = Threshold;
        _indicator.Refreshing = _refreshing;
        _indicator.InvalidateVisual();

        for (var i = 0; i < _rows.Children.Count; i++)
        {
            if (_rows.Children[i] is Control row)
            {
                row.RenderTransform = new TranslateTransform(0, _pull * (0.72 + i * 0.03));
            }
        }
    }

    private void BeginRefresh()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        _indicator.Refreshing = true;
        _indicator.Result = string.Empty;
        _indicator.InvalidateVisual();
        AutomationProperties.SetHelpText(this, "Refreshing");

        if (ArcThemeManager.ReducedMotion)
        {
            CompleteRefresh();
            return;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            CompleteRefresh();
        };
        timer.Start();
    }

    private void CompleteRefresh()
    {
        _refreshing = false;
        _indicator.Refreshing = false;
        _indicator.Result = "3 new updates";
        _indicator.InvalidateVisual();


        var fresh = new List<ArcFeedItem>
        {
            new() { Id = "new-1", Icon = "sparkles", Title = "Arc 4.2 is available on npm", Meta = "Release", Time = "now" },
            new() { Id = "new-2", Icon = "message-circle", Title = "Design review moved to Thursday", Meta = "Team", Time = "2m" }
        };
        _items.InsertRange(0, fresh);
        if (_items.Count > 6)
        {
            _items.RemoveRange(6, _items.Count - 6);
        }

        BuildRows();
        if (_subtitleBlock is not null)
        {
            _subtitleBlock.Text = "Updated just now";
        }

        SetCurrentValue(ItemsProperty, _items.ToList());
        ApplyPull(Threshold * 0.35);
        Settle();

        if (!ArcThemeManager.ReducedMotion)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _indicator.Result = string.Empty;
                _indicator.InvalidateVisual();
            };
            timer.Start();
        }
    }
}

/// <summary>The stretch line in the gap: it reaches full length exactly at the threshold and runs while refreshing.</summary>
internal sealed class ArcPullIndicator : Control
{
    public double Pull { get; set; }

    public double Threshold { get; set; } = 72;

    public bool Refreshing { get; set; }

    public string Result { get; set; } = string.Empty;

    private readonly SvClock _clock = new();
    private double _spin;

    public ArcPullIndicator()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clock.Start(seconds =>
        {
            _spin = seconds;
            InvalidateVisual();
        }, 33);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock.Stop();
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        var centerX = Bounds.Width / 2;
        var armed = Pull >= Threshold;
        var lineBrush = armed || Refreshing ? Sv.Brush(ArcTokens.Accent) : Sv.AlphaBrush(ArcTokens.TextMuted, 0.55);
        var gap = Math.Max(0, Pull) * 0.72;
        var bottom = Math.Max(12, gap - 26);

        if (Result.Length > 0)
        {
            var result = SvDraw.Text(Result, 12, Sv.Brush(ArcTokens.TextSecondary), FontWeight.Medium);
            SvDraw.DrawTextCentered(context, result, centerX, Math.Max(6, bottom - 4));
            return;
        }

        if (gap > 8)
        {
            var pen = new Pen(lineBrush, 2, null, PenLineCap.Round);
            context.DrawLine(pen, new Point(centerX, 6), new Point(centerX, bottom));

            if (Refreshing)
            {
                var sweep = 80 + 120 * (0.5 + 0.5 * Math.Sin(_spin * 3));
                SvDraw.Arc(context, new Point(centerX, bottom), 7, _spin * 220 - 90, sweep, new Pen(lineBrush, 2, null, PenLineCap.Round));
            }
            else
            {
                context.DrawEllipse(armed ? lineBrush : Sv.Brush(ArcTokens.Surface), new Pen(lineBrush, 2), new Point(centerX, bottom), 4.5, 4.5);
            }

            var label = Refreshing
                ? "Refreshing"
                : armed
                    ? "Release to refresh"
                    : Pull > 6
                        ? "Pull to refresh"
                        : string.Empty;
            if (label.Length > 0)
            {
                var text = SvDraw.Text(label, 11.5, armed || Refreshing ? Sv.Brush(ArcTokens.Accent) : Sv.Brush(ArcTokens.TextMuted), FontWeight.Medium);
                SvDraw.DrawTextCentered(context, text, centerX, bottom + 9);
            }
        }
    }
}
