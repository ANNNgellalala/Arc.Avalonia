using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Controls;

/// <summary>
/// The holographic foil: a conic rainbow band, a deterministic glitter field and a glare that all follow
/// the lean. Drawn in one pass so the layers stay in step with the card's transform.
/// </summary>
internal sealed class ArcHoloFoil : Control
{
    public static readonly StyledProperty<double> TiltXProperty =
        AvaloniaProperty.Register<ArcHoloFoil, double>(nameof(TiltX));

    public static readonly StyledProperty<double> TiltYProperty =
        AvaloniaProperty.Register<ArcHoloFoil, double>(nameof(TiltY));

    public static readonly StyledProperty<double> LightXProperty =
        AvaloniaProperty.Register<ArcHoloFoil, double>(nameof(LightX), 0.28);

    public static readonly StyledProperty<double> LightYProperty =
        AvaloniaProperty.Register<ArcHoloFoil, double>(nameof(LightY), 0.2);

    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<ArcHoloFoil, double>(nameof(Intensity), 1.0);

    public static readonly StyledProperty<double> CornerProperty =
        AvaloniaProperty.Register<ArcHoloFoil, double>(nameof(Corner), 20);

    private readonly Point[] _glitter;

    static ArcHoloFoil()
    {
        AffectsRender<ArcHoloFoil>(
            TiltXProperty, TiltYProperty, LightXProperty, LightYProperty, IntensityProperty, CornerProperty);
    }

    public ArcHoloFoil()
    {
        IsHitTestVisible = false;
        var random = new Random(1979);
        _glitter = new Point[86];
        for (var i = 0; i < _glitter.Length; i++)
        {
            _glitter[i] = new Point(random.NextDouble(), random.NextDouble());
        }
    }

    public double TiltX
    {
        get => GetValue(TiltXProperty);
        set => SetValue(TiltXProperty, value);
    }

    public double TiltY
    {
        get => GetValue(TiltYProperty);
        set => SetValue(TiltYProperty, value);
    }

    public double LightX
    {
        get => GetValue(LightXProperty);
        set => SetValue(LightXProperty, value);
    }

    public double LightY
    {
        get => GetValue(LightYProperty);
        set => SetValue(LightYProperty, value);
    }

    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public double Corner
    {
        get => GetValue(CornerProperty);
        set => SetValue(CornerProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 2 || rect.Height <= 2 || Intensity <= 0.01)
        {
            return;
        }

        using (context.PushClip(new RoundedRect(rect, Corner)))
        {
            var glow = ArcGlass.Color(ArcTokens.Surface);
            var shift = Math.Clamp((TiltY / 20) + 0.5, 0, 1);
            var bands = new[]
            {
                ArcGlass.Color(ArcTokens.Series1),
                ArcGlass.Color(ArcTokens.Series2),
                ArcGlass.Color(ArcTokens.Series3),
                ArcGlass.Color(ArcTokens.Series4),
                ArcGlass.Color(ArcTokens.Accent)
            };

            // Conic foil band: the whole rainbow is rotated by the lean so it sweeps as the card turns.
            var conic = new ConicGradientBrush
            {
                Center = new RelativePoint(LightX, LightY, RelativeUnit.Relative),
                Angle = 30 + (TiltX * 1.6),
                GradientStops = { }
            };
            for (var i = 0; i < 8; i++)
            {
                var color = bands[i % bands.Length];
                conic.GradientStops.Add(new GradientStop(
                    ArcColor.WithAlpha(color, (0.12 + (0.10 * (i % 2))) * Intensity),
                    i / 8.0));
            }

            conic.GradientStops.Add(new GradientStop(ArcColor.WithAlpha(bands[0], 0.12 * Intensity), 1));
            context.DrawRectangle(conic, null, rect);

            // A second, tighter band that travels against the lean, the "foil shift" in the docs.
            var band = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, Math.Clamp(0.2 + (shift * 0.5), 0, 1), RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, Math.Clamp(0.8 - (shift * 0.4), 0, 1), RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ArcColor.WithAlpha(bands[2], 0), 0),
                    new GradientStop(ArcColor.WithAlpha(bands[2], 0.20 * Intensity), 0.38),
                    new GradientStop(ArcColor.WithAlpha(bands[3], 0.24 * Intensity), 0.52),
                    new GradientStop(ArcColor.WithAlpha(bands[0], 0), 1)
                }
            };
            context.DrawRectangle(band, null, rect);

            // Glitter: drifts against the tilt and only sparkles near the glare.
            var light = new Point(rect.Width * LightX, rect.Height * LightY);
            for (var i = 0; i < _glitter.Length; i++)
            {
                var px = ((_glitter[i].X + (TiltY * 0.004)) % 1 + 1) % 1;
                var py = ((_glitter[i].Y - (TiltX * 0.004)) % 1 + 1) % 1;
                var point = new Point(rect.Width * px, rect.Height * py);
                var distance = Distance(point, light) / Math.Max(rect.Width, rect.Height);
                var alpha = Math.Clamp(0.55 - (distance * 0.9), 0.04, 0.55) * Intensity;
                var size = 0.7 + ((i % 3) * 0.45);
                var sparkle = i % 11 == 0 ? 1.9 : size;
                context.DrawEllipse(
                    ArcGlass.Solid(ArcColor.WithAlpha(glow, alpha)),
                    null,
                    point,
                    sparkle,
                    sparkle);
            }

            // Glare: a soft highlight where the light catches the foil.
            var glare = new RadialGradientBrush
            {
                Center = new RelativePoint(LightX, LightY, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(LightX, LightY, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.42, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.42, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ArcColor.WithAlpha(glow, 0.22 * Intensity), 0),
                    new GradientStop(ArcColor.WithAlpha(glow, 0.05 * Intensity), 0.45),
                    new GradientStop(ArcColor.WithAlpha(glow, 0), 1)
                }
            };
            context.DrawRectangle(glare, null, rect);
        }
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

/// <summary>
/// A collectible membership card: it leans toward the pointer with a holographic foil, a glitter layer and
/// a glare that trail the angle, and flips to its back on click, Enter or Space.
///
/// Platform approximations: tilt is a Rotate3DTransform with a fixed perspective depth and the two faces
/// are swapped at the halfway point of the rotation, which is the same trick the web version uses. Device
/// tilt is not available on desktop, so a mouse drag over the card leans it instead. The foil is painted
/// with gradients and a deterministic glitter field because Avalonia has no blend modes. No Pro source
/// was used.
/// </summary>
public sealed class ArcHoloCard : Panel
{
    public static readonly StyledProperty<string> MemberNameProperty =
        AvaloniaProperty.Register<ArcHoloCard, string>(nameof(MemberName), "Jasmine Brooks");

    public static readonly StyledProperty<string> TierProperty =
        AvaloniaProperty.Register<ArcHoloCard, string>(nameof(Tier), "Founding member");

    public static readonly StyledProperty<string> MemberSinceProperty =
        AvaloniaProperty.Register<ArcHoloCard, string>(nameof(MemberSince), "Member since 2024");

    public static readonly StyledProperty<string> NumberProperty =
        AvaloniaProperty.Register<ArcHoloCard, string>(nameof(Number), "2481 0917");

    public static readonly StyledProperty<string?> BackNoteProperty =
        AvaloniaProperty.Register<ArcHoloCard, string?>(nameof(BackNote),
            "Valid through 2029. Tap the card or press Enter to turn it over.");

    public static readonly StyledProperty<double> MaxTiltProperty =
        AvaloniaProperty.Register<ArcHoloCard, double>(nameof(MaxTilt), 14);

    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<ArcHoloCard, double>(nameof(Intensity), 1);

    public static readonly StyledProperty<bool> IsFlippedProperty =
        AvaloniaProperty.Register<ArcHoloCard, bool>(nameof(IsFlipped));

    private const double Radius = 20;

    private readonly Border _frame;
    private readonly Panel _frontFace;
    private readonly Panel _backFace;
    private readonly ArcHoloFoil _frontFoil;
    private readonly ArcHoloFoil _backFoil;
    private readonly Border _ring;
    private readonly GlassSpring _tiltX = new(0, 150);
    private readonly GlassSpring _tiltY = new(0, 150);
    private readonly GlassSpring _lightX = new(0.28, 80);
    private readonly GlassSpring _lightY = new(0.2, 80);
    private readonly ArcFrameLoop _loop;
    private double _flipPhase;
    private bool _pointerFocus;
    private bool _hovering;
    private GlassTween? _flipTween;

    static ArcHoloCard()
    {
        MemberNameProperty.Changed.AddClassHandler<ArcHoloCard>((card, _) => card.RebuildFaces());
        TierProperty.Changed.AddClassHandler<ArcHoloCard>((card, _) => card.RebuildFaces());
        MemberSinceProperty.Changed.AddClassHandler<ArcHoloCard>((card, _) => card.RebuildFaces());
        NumberProperty.Changed.AddClassHandler<ArcHoloCard>((card, _) => card.RebuildFaces());
        BackNoteProperty.Changed.AddClassHandler<ArcHoloCard>((card, _) => card.RebuildFaces());
        IsFlippedProperty.Changed.AddClassHandler<ArcHoloCard>((card, _) => card.SyncFlip());
        IntensityProperty.Changed.AddClassHandler<ArcHoloCard>((card, _) =>
        {
            card._frontFoil.Intensity = card.Intensity;
            card._backFoil.Intensity = card.Intensity;
        });
    }

    public ArcHoloCard()
    {
        ClipToBounds = false;
        Focusable = true;
        IsTabStop = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        Width = 260;
        Height = 364;
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        _frontFoil = new ArcHoloFoil { Corner = Radius, Intensity = Intensity };
        _backFoil = new ArcHoloFoil { Corner = Radius, Intensity = Intensity, Opacity = 0.75 };
        _frontFace = new Panel();
        _backFace = new Panel { IsVisible = false, Opacity = 0 };

        _frame = new Border
        {
            CornerRadius = new CornerRadius(Radius),
            ClipToBounds = true,
            BorderBrush = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Surface), 0.35)),
            BorderThickness = new Thickness(1),
            BoxShadow = ArcGlass.Shadow(ArcTokens.ShadowFloating),
            Child = new Panel { Children = { _frontFace, _backFace } }
        };

        _ring = new Border
        {
            Margin = new Thickness(-5),
            CornerRadius = new CornerRadius(Radius + 5),
            BorderThickness = new Thickness(2),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.FocusOutline)),
            IsVisible = false,
            IsHitTestVisible = false
        };

        Children.Add(_frame);
        Children.Add(_ring);

        _loop = new ArcFrameLoop(OnFrame);
        RebuildFaces();
        BuildChrome();

        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        GotFocus += (_, _) =>
        {
            _ring.IsVisible = !_pointerFocus;
            _pointerFocus = false;
        };
        LostFocus += (_, _) => _ring.IsVisible = false;
        KeyDown += OnKeyDown;
    }

    public event EventHandler<bool>? FlippedChanged;

    public string MemberName
    {
        get => GetValue(MemberNameProperty);
        set => SetValue(MemberNameProperty, value);
    }

    public string Tier
    {
        get => GetValue(TierProperty);
        set => SetValue(TierProperty, value);
    }

    public string MemberSince
    {
        get => GetValue(MemberSinceProperty);
        set => SetValue(MemberSinceProperty, value);
    }

    public string Number
    {
        get => GetValue(NumberProperty);
        set => SetValue(NumberProperty, value);
    }

    public string? BackNote
    {
        get => GetValue(BackNoteProperty);
        set => SetValue(BackNoteProperty, value);
    }

    /// <summary>Largest lean in degrees.</summary>
    public double MaxTilt
    {
        get => GetValue(MaxTiltProperty);
        set => SetValue(MaxTiltProperty, value);
    }

    /// <summary>Strength of the foil, glitter and glare, 0 to 1.</summary>
    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public bool IsFlipped
    {
        get => GetValue(IsFlippedProperty);
        set => SetValue(IsFlippedProperty, value);
    }

    private void BuildChrome()
    {
        AutomationProperties.SetName(this, $"{Name}, member card");
        AutomationProperties.SetHelpText(this, IsFlipped
            ? "Showing the back. Press Enter to turn the card over."
            : "Press Enter to turn the card over.");
        AutomationProperties.SetItemStatus(this, IsFlipped ? "Back" : "Front");
    }

    private void RebuildFaces()
    {
        _frontFace.Children.Clear();
        _backFace.Children.Clear();

        var stock = ArcColor.ReOklch(
            ArcColor.Mix(ArcGlass.Color(ArcTokens.Series2), ArcGlass.Color(ArcTokens.Foreground), 0.70),
            0.26,
            0.10,
            300);

        _frontFace.Children.Add(new Border
        {
            Background = ArcGlass.Gradient(
                145,
                new GradientStop(ArcGlass.Lift(stock, 0.06), 0),
                new GradientStop(ArcGlass.Lift(stock, -0.06), 1))
        });
        _frontFace.Children.Add(_frontFoil);
        _frontFace.Children.Add(FrontContent());

        _backFace.Children.Add(new Border
        {
            Background = ArcGlass.Gradient(
                130,
                new GradientStop(ArcGlass.Lift(stock, -0.02), 0),
                new GradientStop(ArcGlass.Lift(stock, -0.10), 1))
        });
        _backFace.Children.Add(_backFoil);
        _backFace.Children.Add(BackContent());

        BuildChrome();
    }

    private Control FrontContent()
    {
        var ink = ArcGlass.Solid(ArcGlass.PhotoInk);
        var muted = ArcGlass.Solid(ArcGlass.PhotoMuted);

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(18, 16, 18, 0), VerticalAlignment = VerticalAlignment.Top };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(7),
            Background = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Surface), 0.28)),
            BorderBrush = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Surface), 0.4)),
            BorderThickness = new Thickness(1),
            Child = ArcGlass.Icon("layers", 12, ink)
        });
        brand.Children.Add(new TextBlock
        {
            Text = "ARC",
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            LetterSpacing = 1.6,
            Foreground = ink,
            VerticalAlignment = VerticalAlignment.Center
        });
        top.Children.Add(brand);

        var tier = new Border
        {
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(8, 3),
            Background = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Accent), 0.34)),
            BorderBrush = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Surface), 0.35)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = Tier,
                FontSize = 10,
                Foreground = ink
            }
        };
        Grid.SetColumn(tier, 1);
        top.Children.Add(tier);

        var bottom = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(18, 0, 18, 18)
        };
        bottom.Children.Add(new TextBlock
        {
            Text = MemberName,
            FontSize = 21,
            FontWeight = FontWeight.Medium,
            Foreground = ink,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        bottom.Children.Add(new TextBlock
        {
            Text = "Member",
            Classes = { "arc-caption" },
            Foreground = muted
        });

        var numbers = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            Margin = new Thickness(0, 10, 0, 0)
        };
        numbers.Children.Add(new TextBlock
        {
            Text = $"••••  {Number}",
            FontSize = 12,
            LetterSpacing = 1.2,
            Foreground = ink
        });
        numbers.Children.Add(new TextBlock
        {
            Text = MemberSince,
            FontSize = 11,
            Foreground = muted
        });
        bottom.Children.Add(numbers);

        var chips = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 8, 0, 0)
        };
        chips.Children.Add(Sparkle("sparkles", "Holo"));
        chips.Children.Add(Sparkle("award", "2024"));
        bottom.Children.Add(chips);

        return new Panel
        {
            Children = { top, bottom }
        };
    }

    private Control Sparkle(string icon, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(ArcGlass.Icon(icon, 11, ArcGlass.Solid(ArcGlass.PhotoMuted)));
        row.Children.Add(new TextBlock { Text = text, FontSize = 10, Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted) });
        return new Border
        {
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(7, 2),
            Background = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Surface), 0.16)),
            Child = row
        };
    }

    private Control BackContent()
    {
        var ink = ArcGlass.Solid(ArcGlass.PhotoInk);
        var muted = ArcGlass.Solid(ArcGlass.PhotoMuted);

        var stack = new StackPanel { Spacing = 10, Margin = new Thickness(18, 16, 18, 16) };

        stack.Children.Add(new TextBlock
        {
            Text = "MEMBER",
            FontSize = 10,
            LetterSpacing = 2,
            Foreground = muted
        });
        stack.Children.Add(new TextBlock
        {
            Text = MemberName,
            FontSize = 18,
            FontWeight = FontWeight.Medium,
            Foreground = ink
        });

        stack.Children.Add(new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(4),
            Background = ArcGlass.Solid(ArcColor.WithAlpha(ArcGlass.Color(ArcTokens.Background), 0.5)),
            Margin = new Thickness(0, 4)
        });

        var details = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        AddBackDetail(details, 0, 0, "Tier", Tier);
        AddBackDetail(details, 1, 0, "Number", Number);
        AddBackDetail(details, 0, 1, "Issued", "2024");
        AddBackDetail(details, 1, 1, "Valid", "12 / 29");
        stack.Children.Add(details);

        var codeRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, Margin = new Thickness(0, 6, 0, 0) };
        codeRow.Children.Add(BuildCode());

        var benefits = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        benefits.Children.Add(Benefit("Studio sessions, twice a month"));
        benefits.Children.Add(Benefit("Priority critique slots"));
        benefits.Children.Add(Benefit("Archive of every issue"));
        Grid.SetColumn(benefits, 1);
        codeRow.Children.Add(benefits);
        stack.Children.Add(codeRow);

        if (!string.IsNullOrWhiteSpace(BackNote))
        {
            stack.Children.Add(new TextBlock
            {
                Text = BackNote,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = muted
            });
        }

        return stack;
    }

    private static void AddBackDetail(Grid grid, int column, int row, string label, string value)
    {
        var stack = new StackPanel { Spacing = 1, Margin = new Thickness(0, 0, 0, 6) };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9,
            Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted)
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 12,
            Foreground = ArcGlass.Solid(ArcGlass.PhotoInk)
        });
        Grid.SetColumn(stack, column);
        Grid.SetRow(stack, row);
        grid.Children.Add(stack);
    }

    private Control Benefit(string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        row.Children.Add(ArcGlass.Icon("check", 11, ArcGlass.Solid(ArcGlass.PhotoMuted)));
        row.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 130,
            Foreground = ArcGlass.Solid(ArcGlass.PhotoInk)
        });
        return row;
    }

    private Control BuildCode()
    {
        var grid = new Grid
        {
            Width = 58,
            Height = 58,
            RowDefinitions = new RowDefinitions("*,*,*,*,*,*"),
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*")
        };

        var random = new Random(4821);
        for (var row = 0; row < 6; row++)
        {
            for (var column = 0; column < 6; column++)
            {
                var filled = random.NextDouble() > 0.45;
                var cell = new Border
                {
                    Margin = new Thickness(0.6),
                    Background = filled
                        ? ArcGlass.Solid(ArcGlass.Dark ? ArcGlass.Color(ArcTokens.Background) : ArcGlass.Color(ArcTokens.Foreground))
                        : Brushes.Transparent
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
        }

        return new Border
        {
            Padding = new Thickness(5),
            CornerRadius = new CornerRadius(8),
            Background = ArcGlass.Solid(ArcGlass.PhotoInk),
            Child = grid
        };
    }

    // ---------------------------------------------------------------- flip

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pointerFocus = true;
        Focus();
        ToggleFlip();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            ToggleFlip();
        }
    }

    private void ToggleFlip() => SetCurrentValue(IsFlippedProperty, !IsFlipped);

    private void SyncFlip()
    {
        BuildChrome();
        _flipTween?.Stop();

        if (ArcThemeManager.ReducedMotion)
        {
            _flipPhase = IsFlipped ? 1 : 0;
            ApplyFaces(_flipPhase);
            ApplyTransform();
            FlippedChanged?.Invoke(this, IsFlipped);
            return;
        }

        var from = _flipPhase;
        var to = IsFlipped ? 1d : 0d;
        _flipTween = GlassTween.Run(TimeSpan.FromMilliseconds(560), GlassMotion.SnappyEase, progress =>
        {
            _flipPhase = from + ((to - from) * progress);
            ApplyFaces(_flipPhase);
            ApplyTransform();
        }, () =>
        {
            _flipPhase = to;
            ApplyFaces(_flipPhase);
            ApplyTransform();
            FlippedChanged?.Invoke(this, IsFlipped);
        });
    }

    /// <summary>The card is edge on at the halfway point, so the faces swap there and only one is in the tree.</summary>
    private void ApplyFaces(double phase)
    {
        var showingBack = phase >= 0.5;
        _frontFace.IsVisible = !showingBack;
        _backFace.IsVisible = showingBack;
        _frontFace.Opacity = showingBack ? 0 : 1;
        _backFace.Opacity = showingBack ? 1 : 0;
    }

    // ---------------------------------------------------------------- tilt

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse || ArcThemeManager.ReducedMotion || MaxTilt <= 0)
        {
            return;
        }

        var point = e.GetPosition(this);
        var width = Math.Max(1, Bounds.Width);
        var height = Math.Max(1, Bounds.Height);
        var nx = ((point.X / width) * 2) - 1;
        var ny = ((point.Y / height) * 2) - 1;
        _tiltY.Target = nx * MaxTilt;
        _tiltX.Target = -ny * MaxTilt;
        _lightX.Target = Math.Clamp(point.X / width, 0, 1);
        _lightY.Target = Math.Clamp(point.Y / height, 0, 1);
        _hovering = true;
        _loop.Start();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _tiltX.Target = 0;
        _tiltY.Target = 0;
        _lightX.Target = 0.28;
        _lightY.Target = 0.2;
        _hovering = false;
        _loop.Start();
    }

    private void OnFrame(double dt)
    {
        _tiltX.Update(dt);
        _tiltY.Update(dt);
        _lightX.Update(dt);
        _lightY.Update(dt);
        ApplyTransform();

        if (!_hovering && _tiltX.Settled && _tiltY.Settled && _lightX.Settled && _lightY.Settled)
        {
            _loop.Stop();
        }
    }

    private void ApplyTransform()
    {
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        var scaleX = Math.Max(0.001, Math.Abs(Math.Cos(Math.PI * _flipPhase)));
        _frame.RenderTransform = new TransformGroup
        {
            Children =
            {
                new Rotate3DTransform(_tiltX.Value, _tiltY.Value, 0, cx, cy, 0, 900),
                new ScaleTransform(scaleX, 1)
            }
        };
        _frontFoil.TiltX = _tiltX.Value;
        _frontFoil.TiltY = _tiltY.Value;
        _frontFoil.LightX = _lightX.Value;
        _frontFoil.LightY = _lightY.Value;
        _backFoil.TiltX = _tiltX.Value;
        _backFoil.TiltY = _tiltY.Value;
        _backFoil.LightX = _lightX.Value;
        _backFoil.LightY = _lightY.Value;
        _frontFoil.InvalidateVisual();
        _backFoil.InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _flipPhase = IsFlipped ? 1 : 0;
        ApplyFaces(_flipPhase);
        ApplyTransform();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _loop.Dispose();
        _flipTween?.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}



