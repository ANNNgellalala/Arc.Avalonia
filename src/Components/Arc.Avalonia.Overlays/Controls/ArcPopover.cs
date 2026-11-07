using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Automation;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Overlays.Controls;

/// <summary>
/// A small anchored surface for contextual content. It opens on a click on the trigger and closes on an
/// outside press or Escape, returning focus to where it started. Non-modal by design, so the rest of the
/// page stays usable while it is open.
/// </summary>
[TemplatePart("PART_TriggerHost", typeof(ContentPresenter))]
[TemplatePart("PART_Overlay", typeof(Canvas))]
public sealed class ArcPopover : ContentControl
{
    public static readonly StyledProperty<object?> TriggerProperty =
        AvaloniaProperty.Register<ArcPopover, object?>(nameof(Trigger));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcPopover, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcPopover, string?>(nameof(Label));

    private ContentPresenter? _triggerHost;
    private Canvas? _overlay;
    private Border? _surface;
    private ContentPresenter? _contentHost;
    private IDisposable? _dismiss;
    private bool _opening;
    private Rect _lastPlacement;

    static ArcPopover()
    {
        IsOpenProperty.Changed.AddClassHandler<ArcPopover>((popover, _) => popover.Sync());
        TriggerProperty.Changed.AddClassHandler<ArcPopover>((popover, _) => popover.UpdateTrigger());
        ContentProperty.Changed.AddClassHandler<ArcPopover>((popover, _) => popover.UpdateContent());
    }

    public ArcPopover()
    {
        ClipToBounds = false;
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        LayoutUpdated += (_, _) =>
        {
            if (IsOpen && _surface is null)
            {
                Open();
            }

            PositionSurface();
        };
    }

    /// <summary>The anchor content; a click on it toggles the popover.</summary>
    public object? Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    /// <summary>Whether the surface is showing.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Accessible name for the surface.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _triggerHost = e.NameScope.Find<ContentPresenter>("PART_TriggerHost");
        _overlay = e.NameScope.Find<Canvas>("PART_Overlay");
        UpdateTrigger();
        Sync();
    }

    private void UpdateTrigger()
    {
        if (_triggerHost is null)
        {
            return;
        }

        _triggerHost.Content = Trigger;
        if (Trigger is Button button)
        {
            button.KeyDown += (_, args) =>
            {
                if (args.Key is Key.Down or Key.Enter or Key.Space)
                {
                    SetCurrentValue(IsOpenProperty, true);
                    args.Handled = true;
                }
            };
        }
    }

    private void UpdateContent()
    {
        if (_contentHost is not null)
        {
            _contentHost.Content = Content;
        }
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        if (_opening)
        {
            return;
        }

        if (_surface is not null &&
            _surface.TranslatePoint(new Point(0, 0), this) is { } origin &&
            new Rect(origin, _surface.Bounds.Size).Contains(e.GetPosition(this)))
        {
            return;
        }

        if (_surface is null)
        {
            SetCurrentValue(IsOpenProperty, true);
            e.Handled = true;
        }
    }

    private void Sync()
    {
        if (IsOpen)
        {
            Open();
        }
        else
        {
            CloseSurface();
        }
    }

    private void Open()
    {
        if (_overlay is null || Bounds.Width <= 0 || _surface is not null)
        {
            return;
        }

        _opening = true;
        var surface = new Border
        {
            Classes = { "popover-surface" },
            Focusable = true,
            Child = _contentHost = new ContentPresenter { Content = Content }
        };

        _surface = surface;
        ArcActionVisuals.SetOverlay(_overlay, surface);
        PositionSurface();
        ArcOverlayOrder.SetRaised(this, true);
        if (!string.IsNullOrWhiteSpace(Label))
        {
            AutomationProperties.SetName(surface, Label!);
        }

        _dismiss?.Dispose();
        _dismiss = ArcDismiss.Attach(this, () => SetCurrentValue(IsOpenProperty, false));
        Dispatcher.UIThread.Post(() =>
        {
            _opening = false;
            surface.Focus();
        }, DispatcherPriority.Loaded);
    }

    private void CloseSurface()
    {
        if (_surface is null)
        {
            return;
        }

        _dismiss?.Dispose();
        _dismiss = null;
        ArcActionVisuals.SetOverlay(_overlay, null);
        ArcOverlayOrder.SetRaised(this, false);
        _surface = null;
        _contentHost = null;
        _triggerHost?.Focus();
    }

    private void PositionSurface()
    {
        if (_surface is null || _overlay is null)
        {
            return;
        }

        var size = _surface.Bounds.Width > 0 ? _surface.Bounds.Size : _surface.DesiredSize;
        var x = 0d;
        var y = Bounds.Height + 6;

        if (TopLevel.GetTopLevel(this) is { } top && global::Avalonia.VisualExtensions.TranslatePoint(this, new Point(0, 0), top) is { } origin)
        {
            if (origin.Y + y + size.Height > top.Bounds.Height - 8)
            {
                y = -size.Height - 6;
            }

            if (origin.X + x + size.Width > top.Bounds.Width - 8)
            {
                x = top.Bounds.Width - 8 - size.Width - origin.X;
            }
        }

        var rect = new Rect(x, y, size.Width, size.Height);
        if (rect == _lastPlacement)
        {
            return;
        }

        _lastPlacement = rect;
        ArcActionVisuals.Place(_surface, x, y);
    }
}