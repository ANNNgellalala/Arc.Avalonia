using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>
/// A read-only preview of a person or link that opens after a short hover or immediately on keyboard
/// focus, stays open while the pointer travels into it, and closes on Escape or after a short grace
/// delay. The surface is hosted in the control's own overlay canvas so a page can screenshot it open.
/// </summary>
public class ArcHoverCard : ContentControl
{
    public static readonly StyledProperty<object?> TriggerProperty =
        AvaloniaProperty.Register<ArcHoverCard, object?>(nameof(Trigger));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcHoverCard, bool>(nameof(IsOpen));

    public static readonly StyledProperty<ArcHoverCardSide> SideProperty =
        AvaloniaProperty.Register<ArcHoverCard, ArcHoverCardSide>(nameof(Side), ArcHoverCardSide.Bottom);

    public static readonly StyledProperty<ArcHoverCardAlign> AlignProperty =
        AvaloniaProperty.Register<ArcHoverCard, ArcHoverCardAlign>(nameof(Align), ArcHoverCardAlign.Start);

    /// <summary>Milliseconds of hover before the first card opens.</summary>
    public static readonly StyledProperty<int> OpenDelayProperty =
        AvaloniaProperty.Register<ArcHoverCard, int>(nameof(OpenDelay), 500);

    /// <summary>Grace in milliseconds after the pointer leaves.</summary>
    public static readonly StyledProperty<int> CloseDelayProperty =
        AvaloniaProperty.Register<ArcHoverCard, int>(nameof(CloseDelay), 140);

    private static ArcHoverCard? _openCard;

    private Panel? _layer;
    private ContentPresenter? _triggerHost;
    private Canvas? _overlay;
    private Border? _surface;
    private ContentPresenter? _contentHost;
    private DispatcherTimer? _openTimer;
    private DispatcherTimer? _closeTimer;
    private Rect _lastPlacement;
    private bool _hoveringTrigger;
    private bool _hoveringCard;
    private bool _suppressed;

    static ArcHoverCard()
    {
        IsOpenProperty.Changed.AddClassHandler<ArcHoverCard>((card, _) => card.Sync());
        TriggerProperty.Changed.AddClassHandler<ArcHoverCard>((card, _) => card.UpdateTrigger());
        ContentProperty.Changed.AddClassHandler<ArcHoverCard>((card, _) => card.UpdateContent());
        SideProperty.Changed.AddClassHandler<ArcHoverCard>((card, _) => card.Position());
        AlignProperty.Changed.AddClassHandler<ArcHoverCard>((card, _) => card.Position());
    }

    public ArcHoverCard()
    {
        ClipToBounds = false;
        LayoutUpdated += (_, _) => Position();
    }

    /// <summary>The trigger, such as a mention button or a link. It must be focusable.</summary>
    public object? Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    /// <summary>Whether the card is showing; settable so a page can present the open state.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public ArcHoverCardSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    public ArcHoverCardAlign Align
    {
        get => GetValue(AlignProperty);
        set => SetValue(AlignProperty, value);
    }

    public int OpenDelay
    {
        get => GetValue(OpenDelayProperty);
        set => SetValue(OpenDelayProperty, value);
    }

    public int CloseDelay
    {
        get => GetValue(CloseDelayProperty);
        set => SetValue(CloseDelayProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layer = e.NameScope.Find<Panel>("Layer");
        _triggerHost = e.NameScope.Find<ContentPresenter>("TriggerHost");
        _overlay = e.NameScope.Find<Canvas>("Overlay");

        if (_triggerHost is not null)
        {
            _triggerHost.PointerEntered += OnTriggerEntered;
            _triggerHost.PointerExited += OnTriggerExited;
        }

        UpdateTrigger();
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimers();
        if (ReferenceEquals(_openCard, this))
        {
            _openCard = null;
        }
    }

    private void UpdateTrigger()
    {
        if (_triggerHost is not null)
        {
            _triggerHost.Content = Trigger;
        }
    }

    private void UpdateContent()
    {
        if (_contentHost is not null)
        {
            _contentHost.Content = Content is ArcHoverProfile profile ? BuildProfile(profile) : Content;
        }
    }

    private void OnTriggerEntered(object? sender, PointerEventArgs e)
    {
        _hoveringTrigger = true;
        _suppressed = false;
        StartOpenTimer();
    }

    private void OnTriggerExited(object? sender, PointerEventArgs e)
    {
        _hoveringTrigger = false;
        _openTimer?.Stop();
        _openTimer = null;
        if (!_hoveringCard && !HasFocusWithin())
        {
            StartCloseTimer();
        }
    }

    private void OnCardEntered(object? sender, PointerEventArgs e)
    {
        _hoveringCard = true;
        _closeTimer?.Stop();
        _closeTimer = null;
    }

    private void OnCardExited(object? sender, PointerEventArgs e)
    {
        _hoveringCard = false;
        if (!_hoveringTrigger && !HasFocusWithin())
        {
            StartCloseTimer();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (!IsWithinTrigger(e.Source as Visual))
        {
            return;
        }

        _suppressed = false;
        OpenNow();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (HasFocusWithin() || _hoveringTrigger || _hoveringCard)
        {
            return;
        }

        Close();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsWithinTrigger(e.Source as Visual))
        {
            _suppressed = false;
            if (IsOpen)
            {
                Close();
            }
            else
            {
                OpenNow();
            }

            return;
        }

        if (!IsWithinSurface(e.Source as Visual) && IsOpen)
        {
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsOpen)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            _suppressed = true;
            Close();
            e.Handled = true;
        }
    }

    private bool HasFocusWithin()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && IsWithin(focused, this);
    }

    private bool IsWithinTrigger(Visual? source) =>
        _triggerHost is not null && source is not null && IsWithin(source, _triggerHost);

    private bool IsWithinSurface(Visual? source) =>
        _surface is not null && source is not null && IsWithin(source, _surface);

    private static bool IsWithin(Visual source, Visual owner)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, owner))
            {
                return true;
            }
        }

        return false;
    }

    private void StartOpenTimer()
    {
        if (IsOpen || _suppressed)
        {
            return;
        }

        _closeTimer?.Stop();
        _closeTimer = null;
        _openTimer?.Stop();
        _openTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(0, OpenDelay)) };
        _openTimer.Tick += (_, _) =>
        {
            _openTimer?.Stop();
            _openTimer = null;
            if (_hoveringTrigger || HasFocusWithin())
            {
                SetCurrentValue(IsOpenProperty, true);
            }
        };
        _openTimer.Start();
    }

    private void StartCloseTimer()
    {
        if (!IsOpen)
        {
            return;
        }

        _closeTimer?.Stop();
        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(0, CloseDelay)) };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer?.Stop();
            _closeTimer = null;
            if (!_hoveringTrigger && !_hoveringCard && !HasFocusWithin())
            {
                SetCurrentValue(IsOpenProperty, false);
            }
        };
        _closeTimer.Start();
    }

    private void OpenNow()
    {
        _openTimer?.Stop();
        _openTimer = null;
        if (!IsOpen)
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    private void Close()
    {
        _closeTimer?.Stop();
        _closeTimer = null;
        SetCurrentValue(IsOpenProperty, false);
    }

    private void StopTimers()
    {
        _openTimer?.Stop();
        _openTimer = null;
        _closeTimer?.Stop();
        _closeTimer = null;
    }

    private void Sync()
    {
        if (IsOpen)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    private void Show()
    {
        if (_overlay is null || _surface is not null)
        {
            return;
        }

        if (_openCard is { } other && !ReferenceEquals(other, this))
        {
            other.SetCurrentValue(IsOpenProperty, false);
        }

        _openCard = this;

        var surface = new Border
        {
            Classes = { "hover-card-surface" },
            Child = _contentHost = new ContentPresenter()
        };
        surface.PointerEntered += OnCardEntered;
        surface.PointerExited += OnCardExited;

        _surface = surface;
        _overlay.Children.Clear();
        _overlay.Children.Add(surface);
        _lastPlacement = default;
        UpdateContent();
        Position();

        var name = Content is ArcHoverProfile profile && !string.IsNullOrWhiteSpace(profile.Name)
            ? profile.Name
            : "Preview";
        AutomationProperties.SetName(surface, name);

        if (!ArcThemeManager.ReducedMotion)
        {
            surface.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = ArcMotion.Fast,
                    Easing = ArcMotion.EaseStandard
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = ArcMotion.SpringSmoothDuration,
                    Easing = ArcMotion.SpringSmooth
                }
            };
            surface.Opacity = 0;
            surface.RenderTransform = TransformOperations.Parse("translateY(4px) scale(0.96)");
            Dispatcher.UIThread.Post(() =>
            {
                if (_surface is null || !IsOpen)
                {
                    return;
                }

                _surface.Opacity = 1;
                _surface.RenderTransform = TransformOperations.Parse("translateY(0px) scale(1)");
            }, DispatcherPriority.Render);
        }
        else
        {
            surface.Opacity = 1;
        }
    }

    private void Hide()
    {
        if (_surface is null)
        {
            if (ReferenceEquals(_openCard, this))
            {
                _openCard = null;
            }

            return;
        }

        if (_surface is not null)
        {
            _surface.PointerEntered -= OnCardEntered;
            _surface.PointerExited -= OnCardExited;
        }

        _overlay?.Children.Clear();
        _surface = null;
        _contentHost = null;
        _hoveringCard = false;
        if (ReferenceEquals(_openCard, this))
        {
            _openCard = null;
        }
    }

    private void Position()
    {
        if (_surface is null || _overlay is null)
        {
            return;
        }

        var size = _surface.Bounds.Width > 0 ? _surface.Bounds.Size : _surface.DesiredSize;
        if (size.Width <= 0)
        {
            size = new Size(296, size.Height);
        }

        var (x, y) = Place(size, Side, Align);
        var top = TopLevel.GetTopLevel(this);
        if (top is not null && global::Avalonia.VisualExtensions.TranslatePoint(this, new Point(0, 0), top) is { } origin)
        {
            if (Side is ArcHoverCardSide.Bottom && origin.Y + y + size.Height > top.Bounds.Height - 8)
            {
                (x, y) = Place(size, ArcHoverCardSide.Top, Align);
            }
            else if (Side is ArcHoverCardSide.Top && origin.Y + y < 8)
            {
                (x, y) = Place(size, ArcHoverCardSide.Bottom, Align);
            }

            if (origin.X + x + size.Width > top.Bounds.Width - 8)
            {
                x = top.Bounds.Width - 8 - size.Width - origin.X;
            }

            if (origin.X + x < 8)
            {
                x = 8 - origin.X;
            }
        }

        var rect = new Rect(x, y, size.Width, size.Height);
        if (rect == _lastPlacement)
        {
            return;
        }

        _lastPlacement = rect;
        Canvas.SetLeft(_surface, x);
        Canvas.SetTop(_surface, y);
    }

    private (double X, double Y) Place(Size size, ArcHoverCardSide side, ArcHoverCardAlign align)
    {
        const double gap = 8;
        var width = Bounds.Width;
        var height = Bounds.Height;

        double x = align switch
        {
            ArcHoverCardAlign.Center => (width - size.Width) / 2,
            ArcHoverCardAlign.End => width - size.Width,
            _ => 0
        };

        return side switch
        {
            ArcHoverCardSide.Top => (x, -size.Height - gap),
            ArcHoverCardSide.Left => (-size.Width - gap, (height - size.Height) / 2),
            ArcHoverCardSide.Right => (width + gap, (height - size.Height) / 2),
            _ => (x, height + gap)
        };
    }

    /// <summary>The standard person preview: portrait, name, role, bio, stats and a footer line.</summary>
    private static Control BuildProfile(ArcHoverProfile profile)
    {
        var panel = new StackPanel { Spacing = 12 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        var avatar = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(9999),
            Classes = { "hover-card-avatar" },
            Child = new TextBlock
            {
                Text = Initials(profile.Name),
                Classes = { "arc-label" },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        header.Children.Add(avatar);

        var identity = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(new TextBlock { Text = profile.Name, Classes = { "arc-label" }, TextTrimming = TextTrimming.CharacterEllipsis });
        if (!string.IsNullOrWhiteSpace(profile.Role))
        {
            identity.Children.Add(new TextBlock
            {
                Text = profile.Role,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid.SetColumn(identity, 1);
        header.Children.Add(identity);
        panel.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(profile.Bio))
        {
            panel.Children.Add(new TextBlock
            {
                Text = profile.Bio,
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (profile.Stats.Count > 0)
        {
            var stats = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var stat in profile.Stats)
            {
                var cell = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 24, 0) };
                cell.Children.Add(new TextBlock { Text = stat.Value, Classes = { "arc-label" } });
                cell.Children.Add(new TextBlock { Text = stat.Label, Classes = { "arc-caption" } });
                stats.Children.Add(cell);
            }

            panel.Children.Add(stats);
        }

        if (!string.IsNullOrWhiteSpace(profile.Meta))
        {
            panel.Children.Add(new Border
            {
                Classes = { "hover-card-meta" },
                Child = new TextBlock
                {
                    Text = profile.Meta,
                    Classes = { "arc-caption" },
                    TextWrapping = TextWrapping.Wrap
                }
            });
        }

        return panel;
    }

    private static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
        };
    }
}
