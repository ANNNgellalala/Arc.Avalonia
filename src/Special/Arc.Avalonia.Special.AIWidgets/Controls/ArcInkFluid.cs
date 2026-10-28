using System;
using System.Collections.Generic;
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

/// <summary>
/// A hero surface with live ink behind it: soft ink tinted with the accent is stirred by the pointer, swirls and
/// settles to neutral as it spreads, and keeps drifting on its own while nobody interacts.
///
/// Platform approximations: this is a seeded particle and curl-noise composition rather than a Navier-Stokes
/// simulation on the GPU, so the ink is a field of soft stamped blobs with smoothed trails instead of a solved
/// velocity and pressure grid. There is no backdrop blur; the ink sits on the surface's own faint gradient.
/// With reduced motion the field is warmed up once and drawn as a still frame.
/// </summary>
public class ArcInkFluid : Panel
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcInkFluid, string>(nameof(Title), "Ship interfaces that feel alive");

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ArcInkFluid, string?>(nameof(Subtitle),
            "Components with motion that explains every change, drawn with the same tokens in light and dark.");

    public static readonly StyledProperty<string?> PrimaryLabelProperty =
        AvaloniaProperty.Register<ArcInkFluid, string?>(nameof(PrimaryLabel), "Get started");

    public static readonly StyledProperty<string?> SecondaryLabelProperty =
        AvaloniaProperty.Register<ArcInkFluid, string?>(nameof(SecondaryLabel), "Read the docs");

    /// <summary>How dark the ink gets, from 0 to 1.</summary>
    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<ArcInkFluid, double>(nameof(Intensity), 0.55);

    /// <summary>How much fresh ink takes the accent colour, from 0 to 1.</summary>
    public static readonly StyledProperty<double> TintProperty =
        AvaloniaProperty.Register<ArcInkFluid, double>(nameof(Tint), 0.85);

    /// <summary>How hard the pointer stirs, from 0 to 1.</summary>
    public static readonly StyledProperty<double> ForceProperty =
        AvaloniaProperty.Register<ArcInkFluid, double>(nameof(Force), 0.55);

    /// <summary>A slow stir while nobody interacts.</summary>
    public static readonly StyledProperty<bool> AmbientProperty =
        AvaloniaProperty.Register<ArcInkFluid, bool>(nameof(Ambient), true);

    private readonly ArcInkCanvas _ink = new();

    public ArcInkFluid()
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

    public string? PrimaryLabel
    {
        get => GetValue(PrimaryLabelProperty);
        set => SetValue(PrimaryLabelProperty, value);
    }

    public string? SecondaryLabel
    {
        get => GetValue(SecondaryLabelProperty);
        set => SetValue(SecondaryLabelProperty, value);
    }

    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public double Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    public double Force
    {
        get => GetValue(ForceProperty);
        set => SetValue(ForceProperty, value);
    }

    public bool Ambient
    {
        get => GetValue(AmbientProperty);
        set => SetValue(AmbientProperty, value);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _ink.Pause();
    }

    private void Rebuild()
    {
        Children.Clear();
        Sv.Detach(_ink);
        _ink.MinHeight = 340;
        _ink.Intensity = Intensity;
        _ink.Tint = Tint;
        _ink.Force = Force;
        _ink.Ambient = Ambient;
        Children.Add(_ink);

        var content = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 520,
            Margin = new Thickness(40, 34, 40, 34),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var heading = new TextBlock
        {
            Text = Title,
            Classes = { "arc-subtitle" },
            TextWrapping = TextWrapping.Wrap
        };
        content.Children.Add(heading);

        if (!string.IsNullOrWhiteSpace(Subtitle))
        {
            var body = new TextBlock
            {
                Text = Subtitle,
                Classes = { "arc-body", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap
            };
            content.Children.Add(body);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (!string.IsNullOrWhiteSpace(PrimaryLabel))
        {
            actions.Children.Add(new ArcButton { Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm, Content = PrimaryLabel });
        }

        if (!string.IsNullOrWhiteSpace(SecondaryLabel))
        {
            actions.Children.Add(new ArcButton { Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm, Content = SecondaryLabel });
        }

        content.Children.Add(actions);

        var hint = new TextBlock
        {
            Text = "Move the pointer across the surface to stir the ink",
            FontSize = Sv.Size(ArcTokens.TextXs)
        };
        hint.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        content.Children.Add(hint);

        Children.Add(content);
        AutomationProperties.SetName(this, Title);
        AutomationProperties.SetHelpText(this, "Decorative ink background; the pointer stirs it. Reduced motion shows a still frame.");
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _ink.PointerStir(e.GetPosition(this));
    }
}

/// <summary>The ink field itself: particles advected by a curl field with seeded positions and soft stamps.</summary>
internal sealed class ArcInkCanvas : Control
{
    private const int Count = 150;

    private readonly Particle[] _particles = new Particle[Count];
    private readonly SvClock _clock = new();
    private readonly Random _random = new(77_031);
    private double _time;
    private Point? _lastPointer;
    private double _pointerImpulse;
    private double _pointerX;
    private double _pointerY;

    public ArcInkCanvas()
    {
        ClipToBounds = true;
        Seed();
        WarmUp(ArcThemeManager.ReducedMotion ? 150 : 80);
    }

    public double Intensity { get; set; } = 0.55;

    public double Tint { get; set; } = 0.85;

    public double Force { get; set; } = 0.55;

    public bool Ambient { get; set; } = true;

    private struct Particle
    {
        public double X;
        public double Y;
        public double VX;
        public double VY;
        public double Life;
        public double Radius;
        public double Drift;
    }

    private void Seed()
    {
        var random = new Random(20_240_517);
        for (var i = 0; i < Count; i++)
        {
            _particles[i] = Spawn(random, i);
        }
    }

    private static Particle Spawn(Random random, int index)
    {
        // Two broad bands plus scattered wisps, so the still frame already has composition.
        var band = index % 3;
        var x = band == 0 ? 0.22 + random.NextDouble() * 0.24 : band == 1 ? 0.56 + random.NextDouble() * 0.3 : random.NextDouble();
        var y = band == 0 ? random.NextDouble() * 0.7 + 0.15 : random.NextDouble();
        return new Particle
        {
            X = x,
            Y = y,
            VX = (random.NextDouble() - 0.5) * 0.02,
            VY = (random.NextDouble() - 0.5) * 0.02,
            Life = 0.35 + random.NextDouble() * 0.65,
            Radius = 12 + random.NextDouble() * 26,
            Drift = random.NextDouble() * Math.PI * 2
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 900 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 340 : Math.Max(320, availableSize.Height);
        return new Size(width, height);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Resume();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Pause();
    }

    private void Resume()
    {
        if (ArcThemeManager.ReducedMotion)
        {
            InvalidateVisual();
            return;
        }

        _clock.Start(seconds =>
        {
            var delta = Math.Min(0.05, Math.Max(0.001, seconds - _time));
            _time = seconds;
            Advance(delta);
            InvalidateVisual();
        });
    }

    public void Pause() => _clock.Stop();

    public void PointerStir(Point position)
    {
        if (_lastPointer is { } last)
        {
            var dx = (position.X - last.X) / Math.Max(1, Bounds.Width);
            var dy = (position.Y - last.Y) / Math.Max(1, Bounds.Height);
            var speed = Math.Min(1, Math.Sqrt(dx * dx + dy * dy) * 14);
            _pointerImpulse = Math.Min(1.6, _pointerImpulse + speed);
            _pointerX = position.X / Math.Max(1, Bounds.Width);
            _pointerY = position.Y / Math.Max(1, Bounds.Height);
        }

        _lastPointer = position;
    }

    private void WarmUp(int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            _time += 1.0 / 60;
            Advance(1.0 / 60);
        }
    }

    private void Advance(double delta)
    {
        var ambient = Ambient ? 1.0 : 0.45;
        var force = Math.Clamp(Force, 0, 1);
        for (var i = 0; i < Count; i++)
        {
            ref var p = ref _particles[i];
            var t = _time * 0.32;
            var fx = Math.Sin(p.Y * 5.1 + t) + 0.6 * Math.Sin(p.Y * 9.3 - t * 1.4 + p.X * 3.1);
            var fy = Math.Cos(p.X * 5.6 - t * 0.8) + 0.6 * Math.Cos(p.X * 8.4 + t * 1.2 + p.Y * 2.7);
            p.VX += -fy * 0.028 * delta * ambient;
            p.VY += fx * 0.028 * delta * ambient;

            if (_pointerImpulse > 0.001)
            {
                var dx = p.X - _pointerX;
                var dy = p.Y - _pointerY;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < 0.42)
                {
                    var falloff = (1 - distance / 0.42) * _pointerImpulse * force;
                    var angle = Math.Atan2(dy, dx) + 1.1;
                    p.VX += Math.Cos(angle) * falloff * 0.55 * delta * 60;
                    p.VY += Math.Sin(angle) * falloff * 0.55 * delta * 60;
                }
            }

            p.VX *= 0.982;
            p.VY *= 0.982;
            p.X += p.VX * delta;
            p.Y += p.VY * delta;
            p.Life -= delta * 0.16;

            if (p.Life <= 0 || p.X < -0.12 || p.X > 1.12 || p.Y < -0.12 || p.Y > 1.12)
            {
                p = Spawn(_random, i);
            }
        }

        _pointerImpulse *= 0.9;
    }

    /// <summary>A soft radial stamp: solid at the centre, gone at the edge, so stamped ink has no hard rim.</summary>
    private static IBrush SoftBrush(Color color)
    {
        var alpha = color.A / 255.0;
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(new GradientStop(ArcColor.WithAlpha(color, alpha), 0));
        brush.GradientStops.Add(new GradientStop(ArcColor.WithAlpha(color, alpha * 0.55), 0.45));
        brush.GradientStops.Add(new GradientStop(ArcColor.WithAlpha(color, alpha * 0.18), 0.75));
        brush.GradientStops.Add(new GradientStop(ArcColor.WithAlpha(color, 0), 1));
        return brush;
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var radius = ArcResources.Radius(ArcTokens.RadiusSurface).TopLeft;
        var surface = Sv.Brush(ArcTokens.SurfaceMuted);
        context.DrawRectangle(surface, null, bounds, radius, radius);

        using (context.PushClip(new RoundedRect(bounds.Deflate(1), radius - 1)))
        {
            // A faint brand wash under the ink, so the surface is not flat.
            var wash = Sv.Brush(ArcTokens.GradientSoft);
            using (context.PushOpacity(0.9))
            {
                context.DrawRectangle(wash, null, bounds);
            }

            var intensity = Math.Clamp(Intensity, 0, 1);
            var tint = Math.Clamp(Tint, 0, 1);
            var alpha = 0.05 + 0.07 * intensity;
            var accentColor = ArcColor.WithAlpha(Sv.Color(ArcTokens.Accent), alpha);
            var inkColor = ArcColor.WithAlpha(Sv.Color(ArcTokens.Foreground), alpha * 0.9);
            var accentBrush = SoftBrush(accentColor);
            var inkBrush = SoftBrush(inkColor);

            foreach (var particle in _particles)
            {
                var age = 1 - Math.Clamp(particle.Life, 0, 1);
                var fade = Math.Sin(age * Math.PI);
                if (fade <= 0.03)
                {
                    continue;
                }

                var x = particle.X * Bounds.Width;
                var y = particle.Y * Bounds.Height;
                var wobble = 0.8 + 0.3 * Math.Sin(particle.Drift + _time * 0.55);
                var stampRadius = particle.Radius * wobble * (1 + age * 0.8);
                var fresh = Math.Clamp((0.58 - age) / 0.58, 0, 1) * tint;
                var brush = fresh > 0.45 ? accentBrush : inkBrush;
                var angle = Math.Atan2(particle.VY * Bounds.Height, particle.VX * Bounds.Width);

                using (context.PushTransform(Matrix.CreateTranslation(-x, -y) * Matrix.CreateRotation(angle) * Matrix.CreateTranslation(x, y)))
                using (context.PushOpacity(fade))
                {
                    context.DrawEllipse(brush, null, new Point(x, y), stampRadius * 1.9, stampRadius * 1.15);
                }
            }
            // Hairline frame reads as a surface on the page.
            context.DrawRectangle(null, new Pen(Sv.AlphaBrush(ArcTokens.Border, 0.9), 1), bounds.Deflate(0.5), radius, radius);
        }
    }
}
