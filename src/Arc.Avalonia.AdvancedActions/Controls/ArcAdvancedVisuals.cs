using System;
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

namespace Arc.Avalonia.AdvancedActions.Controls;

/// <summary>
/// Shared helpers for the advanced action controls: token lookups, concentric radii, text measurement,
/// the shared highlight, and the outside-press/Escape dismissal used by page-hosted overlays.
/// Everything resolves through <see cref="ArcTokens"/>, so both themes and all eight accents follow.
/// </summary>
internal static class ArcAdvancedVisuals
{
    public static IBrush Brush(string key) => ArcResources.Brush(key);

    public static CornerRadius Radius(string key) => ArcResources.Radius(key);

    public static BoxShadows Shadow(string key) => ArcResources.Shadows(key);

    public static FontFamily Font(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) && value is FontFamily family
            ? family
            : FontFamily.Default;

    /// <summary>Inner radius = outer radius - inset, clamped to 6 so the two arcs stay parallel at small sizes.</summary>
    public static CornerRadius Concentric(CornerRadius outer, double inset)
    {
        inset = Math.Max(0, inset);
        return new CornerRadius(
            Math.Max(6, outer.TopLeft - inset),
            Math.Max(6, outer.TopRight - inset),
            Math.Max(6, outer.BottomRight - inset),
            Math.Max(6, outer.BottomLeft - inset));
    }

    /// <summary>Measures a word with the body face, so animated widths match what layout will render.</summary>
    public static double MeasureText(string? text, double fontSize, FontWeight weight)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var block = new TextBlock
        {
            Text = text,
            FontFamily = Font(ArcTokens.FontBody),
            FontSize = fontSize,
            FontWeight = weight,
            LetterSpacing = -0.01
        };
        block.Measure(Size.Infinity);
        return Math.Ceiling(block.DesiredSize.Width);
    }

    public static void SetName(Control control, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            AutomationProperties.SetName(control, name!);
        }
    }

    /// <summary>Text that follows the foreground of an owning button, including its hover and variant colours.</summary>
    public static TextBlock InheritText(string text, Control owner, double fontSize = 14, bool medium = true)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = medium ? FontWeight.Medium : FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        block.Bind(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = owner });
        return block;
    }

    public static ArcIcon InheritIcon(string? name, Control owner, double size = 16, double strokeWidth = 1.75)
    {
        var icon = new ArcIcon
        {
            Icon = name,
            Size = size,
            StrokeWidth = strokeWidth,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        icon.Bind(ArcIcon.ForegroundProperty, new Binding("Foreground") { Source = owner });
        return icon;
    }

    public static ArcIcon Icon(string? name, double size = 16, double strokeWidth = 1.75) => new()
    {
        Icon = name,
        Size = size,
        StrokeWidth = strokeWidth,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    /// <summary>Turns a chevron over while its surface is open; reduced motion swaps it instantly.</summary>
    public static void Rotate(Control control, bool flipped)
    {
        control.RenderTransformOrigin = RelativePoint.Center;
        if (!ArcThemeManager.ReducedMotion)
        {
            control.Transitions ??= new Transitions
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

    /// <summary>First letters of the first two words, the way the account avatar falls back without a photo.</summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = parts.Length > 0 ? char.ToUpperInvariant(parts[0][0]) : '?';
        var second = parts.Length > 1 ? char.ToUpperInvariant(parts[1][0]) : '\0';
        return second == '\0' ? first.ToString() : string.Concat(first, second);
    }
}

/// <summary>
/// The one moving surface shared by the floating toolbar and the account menu. It is painted as a
/// translucent foreground tint so it reads on a muted page as well as on a floating panel; it travels on
/// the morph spring, settles darker and slightly smaller while pressed, and jumps when motion is reduced.
/// </summary>
public sealed class ArcAdvancedGlide : TemplatedControl
{
    public static readonly StyledProperty<double> TintOpacityProperty =
        AvaloniaProperty.Register<ArcAdvancedGlide, double>(nameof(TintOpacity), 0.08);

    public static readonly StyledProperty<bool> DangerProperty =
        AvaloniaProperty.Register<ArcAdvancedGlide, bool>(nameof(Danger));

    private const double RestTint = 0.08;
    private const double PressedTint = 0.13;
    private bool _pressed;

    public ArcAdvancedGlide()
    {
        IsHitTestVisible = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        RenderTransformOrigin = RelativePoint.Center;
        IsVisible = false;
        Opacity = 0;
    }

    /// <summary>How strongly the foreground tint marks the surface.</summary>
    public double TintOpacity
    {
        get => GetValue(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    /// <summary>Uses the danger tint, for a destructive row such as sign out.</summary>
    public bool Danger
    {
        get => GetValue(DangerProperty);
        set
        {
            SetValue(DangerProperty, value);
            Classes.Set("danger", value);
        }
    }

    static ArcAdvancedGlide()
    {
        DangerProperty.Changed.AddClassHandler<ArcAdvancedGlide>((glide, _) => glide.Classes.Set("danger", glide.Danger));
    }

    /// <summary>Moves (or shows) the highlight over a rectangle in the parent's coordinates.</summary>
    public void ShowAt(Rect bounds, bool grow = false)
    {
        var wasVisible = IsVisible && Opacity > 0.01;
        if (!wasVisible)
        {
            // Appearing from nothing lands in place; only scale and opacity animate.
            Transitions = null;
            Margin = new Thickness(bounds.X, bounds.Y, 0, 0);
            Width = Math.Max(0, bounds.Width);
            Height = Math.Max(0, bounds.Height);
            IsVisible = true;
            TintOpacity = _pressed ? PressedTint : RestTint;

            if (ArcThemeManager.ReducedMotion)
            {
                RenderTransform = TransformOperations.Parse("scale(1)");
                Opacity = 1;
                return;
            }

            Opacity = 0;
            RenderTransform = TransformOperations.Parse(grow ? "scale(0.94)" : "scale(1)");
            Transitions = Travel(includeTravel: false);
            Dispatcher.UIThread.Post(() =>
            {
                Opacity = 1;
                RenderTransform = TransformOperations.Parse("scale(1)");
            }, DispatcherPriority.Loaded);
            return;
        }

        TintOpacity = _pressed ? PressedTint : RestTint;
        if (ArcThemeManager.ReducedMotion)
        {
            Transitions = null;
            Margin = new Thickness(bounds.X, bounds.Y, 0, 0);
            Width = Math.Max(0, bounds.Width);
            Height = Math.Max(0, bounds.Height);
            Opacity = 1;
            return;
        }

        Transitions = Travel(includeTravel: true);
        Margin = new Thickness(bounds.X, bounds.Y, 0, 0);
        Width = Math.Max(0, bounds.Width);
        Height = Math.Max(0, bounds.Height);
        Opacity = 1;
    }

    public void Hide()
    {
        if (ArcThemeManager.ReducedMotion)
        {
            Transitions = null;
            Opacity = 0;
            IsVisible = false;
            return;
        }

        // Fade out in place; the next ShowAt sees Opacity 0 and lands without travelling.
        Transitions = Travel(includeTravel: false);
        Opacity = 0;
    }

    /// <summary>The press answer happens in place: the tint darkens and the surface settles to 96.5 percent.</summary>
    public void SetPressed(bool pressed)
    {
        _pressed = pressed;
        TintOpacity = pressed ? PressedTint : RestTint;
        if (ArcThemeManager.ReducedMotion)
        {
            RenderTransform = TransformOperations.Parse("scale(1)");
            return;
        }

        RenderTransform = TransformOperations.Parse(pressed ? "scale(0.965)" : "scale(1)");
    }

    private Transitions? Travel(bool includeTravel)
    {
        if (ArcThemeManager.ReducedMotion)
        {
            return null;
        }

        var transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = ArcMotion.Fast,
                Easing = ArcMotion.EaseStandard
            },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = ArcMotion.SpringSnappyDuration,
                Easing = ArcMotion.SpringSnappy
            },
            new DoubleTransition
            {
                Property = TintOpacityProperty,
                Duration = ArcMotion.Instant,
                Easing = ArcMotion.EaseStandard
            }
        };

        if (includeTravel)
        {
            transitions.Add(new ThicknessTransition
            {
                Property = MarginProperty,
                Duration = ArcMotion.SpringMorphDuration,
                Easing = ArcMotion.SpringMorph
            });
            transitions.Add(new DoubleTransition
            {
                Property = WidthProperty,
                Duration = ArcMotion.SpringMorphDuration,
                Easing = ArcMotion.SpringMorph
            });
            transitions.Add(new DoubleTransition
            {
                Property = HeightProperty,
                Duration = ArcMotion.SpringMorphDuration,
                Easing = ArcMotion.SpringMorph
            });
        }

        return transitions;
    }
}

/// <summary>Outside-press and Escape dismissal for overlays hosted in the page rather than a popup window.</summary>
internal static class ArcAdvancedDismiss
{
    public static IDisposable Attach(Visual owner, Action dismiss, bool outsidePress = true, Action? escape = null)
    {
        var top = TopLevel.GetTopLevel(owner);
        return top is null ? new Noop() : new Handle(top, owner, dismiss, outsidePress, escape);
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
        private readonly Action _escape;
        private bool _done;

        public Handle(TopLevel top, Visual owner, Action dismiss, bool outsidePress, Action? escape)
        {
            _top = top;
            _owner = owner;
            _dismiss = dismiss;
            _outsidePress = outsidePress;
            _escape = escape ?? dismiss;
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

            Fire(_dismiss);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Fire(_escape);
            }
        }

        private void Fire(Action action)
        {
            if (_done)
            {
                return;
            }

            _done = true;
            action();
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
