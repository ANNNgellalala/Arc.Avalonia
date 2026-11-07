using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Internal;

/// <summary>
/// Small helpers shared by the action components: token lookups, concentric radii, the floating scrim,
/// the gliding highlight, and the outside-press/Escape dismissal used by every overlay.
/// </summary>
internal static class ArcActionVisuals
{
    public static IBrush Brush(string key) => ArcResources.Brush(key);

    public static CornerRadius Radius(string key) => ArcResources.Radius(key);

    public static BoxShadows Shadow(string key) => ArcResources.Shadows(key);

    /// <summary>Inner radius = outer radius - padding, clamped to 6, so the two arcs stay parallel.</summary>
    public static CornerRadius Concentric(CornerRadius outer, double inset)
    {
        inset = Math.Max(0, inset);
        return new CornerRadius(
            Math.Max(6, outer.TopLeft - inset),
            Math.Max(6, outer.TopRight - inset),
            Math.Max(6, outer.BottomRight - inset),
            Math.Max(6, outer.BottomLeft - inset));
    }

    /// <summary>
    /// The modal scrim. Arc keeps this as a translucent ink rather than a token, so derive it from the
    /// palette's own ink in OKLCH (no raw hex) and it reads correctly on both themes.
    /// </summary>
    public static IBrush Scrim(double alpha = 0.42) =>
        new SolidColorBrush(ArcColor.Oklch(0.09, 0.004, 280, alpha));

    public static ArcIcon Icon(string? name, double size = 16) => new()
    {
        Icon = name,
        Size = size,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    /// <summary>Text that inherits the surrounding button's foreground instead of the page default.</summary>
    public static TextBlock InheritText(string text, Button owner, bool medium = false)
    {
        var block = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        if (medium)
        {
            block.FontWeight = FontWeight.Medium;
        }

        block.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Button.Foreground)) { Source = owner });
        return block;
    }

    public static ArcIcon InheritIcon(string? name, Button owner, double size = 16)
    {
        var icon = Icon(name, size);
        icon.Bind(ArcIcon.ForegroundProperty, new Binding(nameof(Button.Foreground)) { Source = owner });
        return icon;
    }

    public static void SetOverlay(Canvas? canvas, Control? content)
    {
        if (canvas is null)
        {
            return;
        }

        canvas.Children.Clear();
        if (content is not null)
        {
            canvas.Children.Add(content);
        }
    }

    public static void Place(Control content, double x, double y)
    {
        Canvas.SetLeft(content, x);
        Canvas.SetTop(content, y);
    }

    public static void SetClass(Control control, string name, bool value) => control.Classes.Set(name, value);

    public static void SetName(Control control, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            AutomationProperties.SetName(control, name!);
        }
    }

    /// <summary>Turns a chevron over while its menu is open; reduced motion swaps it instantly.</summary>
    public static void Rotate(Control control, bool flipped)
    {
        control.RenderTransformOrigin = RelativePoint.Center;
        if (control.Transitions is null && !ArcThemeManager.ReducedMotion)
        {
            control.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = ArcMotion.SpringSnappyDuration,
                    Easing = ArcMotion.SpringSnappy
                }
            };
        }

        control.RenderTransform = TransformOperations.Parse(flipped ? "rotate(180deg)" : "rotate(0deg)");
    }

    /// <summary>Fades and lifts a face in place, used by the swap and confirm states.</summary>
    public static void Fade(Control control, bool visible)
    {
        control.IsVisible = visible;
        control.Opacity = visible ? 1 : 0;
    }
}

/// <summary>
/// Hosts one <see cref="ArcMenuSurface"/> in an owner's overlay canvas and keeps it anchored under (or
/// above) the trigger, flipping away from the window edge the way Arc's collision handling does.
/// </summary>
internal sealed class ArcMenuPresenter
{
    private readonly Control _owner;
    private Canvas? _canvas;
    private ArcMenuSurface? _surface;
    private Rect _lastPlacement;
    private bool _alignEnd = true;
    private double _offset = 6;

    public ArcMenuPresenter(Control owner)
    {
        _owner = owner;
    }

    public bool IsOpen => _surface is not null;

    public event EventHandler? Closed;

    public void Attach(Canvas? canvas)
    {
        _canvas = canvas;
        if (_surface is not null)
        {
            ArcActionVisuals.SetOverlay(canvas, _surface);
            Reposition();
        }
    }

    public void Open(IEnumerable<ArcMenuItem> items, string? label, bool alignEnd = true, double offset = 6, bool focus = true)
    {
        _alignEnd = alignEnd;
        _offset = offset;

        if (_surface is not null)
        {
            return;
        }

        if (_canvas is null)
        {
            return;
        }

        var surface = new ArcMenuSurface
        {
            Items = items,
            Label = label ?? "Menu",
            MinWidth = Math.Max(192, _owner.Bounds.Width)
        };
        surface.Closed += OnSurfaceClosed;
        _surface = surface;
        ArcActionVisuals.SetOverlay(_canvas, surface);
        Reposition();
        surface.Open(focus);
        Reposition();
        // The surface is hosted in the owner's template canvas rather than a popup window, so lift the
        // owner branch above the content that follows it.
        ArcOverlayOrder.SetRaised(_owner, true);
    }

    public void Close()
    {
        if (_surface is null)
        {
            return;
        }

        var surface = _surface;
        _surface = null;
        surface.Closed -= OnSurfaceClosed;
        ArcActionVisuals.SetOverlay(_canvas, null);
        ArcOverlayOrder.SetRaised(_owner, false);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Reposition()
    {
        var surface = _surface;
        if (surface is null || _canvas is null)
        {
            return;
        }

        var size = surface.Bounds.Width > 0 ? surface.Bounds.Size : surface.DesiredSize;
        if (size.Width <= 0)
        {
            size = new Size(Math.Max(192, _owner.Bounds.Width), 0);
        }

        var x = _alignEnd ? _owner.Bounds.Width - size.Width : 0;
        var y = _owner.Bounds.Height + _offset;

        if (TopLevel.GetTopLevel(_owner) is { } top &&
            _owner.TranslatePoint(new Point(0, 0), top) is { } origin)
        {
            if (origin.Y + y + size.Height > top.Bounds.Height - 8)
            {
                y = -size.Height - _offset;
            }

            if (origin.X + x + size.Width > top.Bounds.Width - 8)
            {
                x = top.Bounds.Width - 8 - size.Width - origin.X;
            }

            if (x < 0)
            {
                x = 0;
            }
        }

        var rect = new Rect(x, y, size.Width, size.Height);
        if (rect == _lastPlacement)
        {
            return;
        }

        _lastPlacement = rect;
        ArcActionVisuals.Place(surface, x, y);
    }
    private void OnSurfaceClosed(object? sender, EventArgs e) => Close();
}

/// <summary>
/// Arc's shared highlight: one soft shape that glides between segments on the morph spring, and jumps
/// instantly when motion is reduced. It is always non-interactive and sits behind the content it marks.
/// </summary>
public sealed class ArcGlide : Border
{
    private Transitions? _motion;

    public ArcGlide()
    {
        IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        IsVisible = false;
        Opacity = 0;
        Classes.Add("arc-glide");
    }

    /// <summary>Moves (or shows) the highlight over the given rectangle in the parent's coordinates.</summary>
    public void ShowAt(Rect bounds, bool animate = true)
    {
        Transitions = animate ? Motion() : null;
        Margin = new Thickness(bounds.X, bounds.Y, 0, 0);
        Width = bounds.Width;
        Height = bounds.Height;
        IsVisible = true;
        Opacity = 1;
    }

    public void Hide(bool animate = true)
    {
        Transitions = animate ? Motion() : null;
        Opacity = 0;
        if (!animate || ArcThemeManager.ReducedMotion)
        {
            IsVisible = false;
        }
    }

    private Transitions? Motion()
    {
        if (ArcThemeManager.ReducedMotion)
        {
            return null;
        }

        return _motion ??= new Transitions
        {
            new ThicknessTransition
            {
                Property = MarginProperty,
                Duration = ArcMotion.SpringMorphDuration,
                Easing = ArcMotion.SpringMorph
            },
            new DoubleTransition
            {
                Property = WidthProperty,
                Duration = ArcMotion.SpringMorphDuration,
                Easing = ArcMotion.SpringMorph
            },
            new DoubleTransition
            {
                Property = HeightProperty,
                Duration = ArcMotion.SpringMorphDuration,
                Easing = ArcMotion.SpringMorph
            },
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = ArcMotion.Fast,
                Easing = ArcMotion.EaseStandard
            }
        };
    }
}

/// <summary>Outside-press and Escape dismissal for overlays hosted in the page rather than a popup.</summary>
internal static class ArcDismiss
{
    public static IDisposable Attach(Visual owner, Action dismiss, bool outsidePress = true)
    {
        var top = TopLevel.GetTopLevel(owner);
        return top is null ? new Noop() : new Handle(top, owner, dismiss, outsidePress);
    }

    private sealed class Noop : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class Handle : IDisposable
    {
        private readonly TopLevel _top;
        private readonly Visual _owner;
        private readonly Action _dismiss;
        private readonly bool _outsidePress;

        public Handle(TopLevel top, Visual owner, Action dismiss, bool outsidePress)
        {
            _top = top;
            _owner = owner;
            _dismiss = dismiss;
            _outsidePress = outsidePress;
            if (outsidePress)
            {
                _top.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            }

            _top.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Visual source && IsWithin(source, _owner))
            {
                return;
            }

            _dismiss();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _dismiss();
            }
        }

        public void Dispose()
        {
            if (_outsidePress)
            {
                _top.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            }

            _top.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        }

        private static bool IsWithin(Visual source, Visual owner)
        {
            for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
            {
                if (ReferenceEquals(visual, owner))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

/// <summary>Shared keyboard navigation for vertical menu-like lists.</summary>
internal static class ArcKeyboard
{
    public static int Next(IReadOnlyList<Button> items, int current, int delta, Func<Button, bool> enabled)
    {
        if (items.Count == 0)
        {
            return -1;
        }

        var index = current;
        for (var step = 0; step < items.Count; step++)
        {
            index += delta;
            if (index < 0)
            {
                index = items.Count - 1;
            }
            else if (index >= items.Count)
            {
                index = 0;
            }

            if (enabled(items[index]))
            {
                return index;
            }
        }

        return current;
    }

    public static int Edge(IReadOnlyList<Button> items, int delta, Func<Button, bool> enabled)
    {
        if (delta > 0)
        {
            for (var i = items.Count - 1; i >= 0; i--)
            {
                if (enabled(items[i]))
                {
                    return i;
                }
            }
        }
        else
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (enabled(items[i]))
                {
                    return i;
                }
            }
        }

        return -1;
    }
}
