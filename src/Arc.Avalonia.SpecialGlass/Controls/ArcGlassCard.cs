using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

/// <summary>
/// The specular rim of a glass card: a rounded border stroke lit from the pointer, plus a soft sheen just
/// inside the glass. Painted rather than filtered, because Avalonia has no mask compositing for a CSS
/// style masked gradient.
/// </summary>
internal sealed class ArcCardRim : Control
{
    public static readonly StyledProperty<double> HighlightXProperty =
        AvaloniaProperty.Register<ArcCardRim, double>(nameof(HighlightX), 0.35);

    public static readonly StyledProperty<double> HighlightYProperty =
        AvaloniaProperty.Register<ArcCardRim, double>(nameof(HighlightY), 0.2);

    public static readonly StyledProperty<double> CornerProperty =
        AvaloniaProperty.Register<ArcCardRim, double>(nameof(Corner), 26);

    public static readonly StyledProperty<double> StrengthProperty =
        AvaloniaProperty.Register<ArcCardRim, double>(nameof(Strength), 1.0);

    static ArcCardRim()
    {
        AffectsRender<ArcCardRim>(HighlightXProperty, HighlightYProperty, CornerProperty, StrengthProperty);
    }

    public double HighlightX
    {
        get => GetValue(HighlightXProperty);
        set => SetValue(HighlightXProperty, value);
    }

    public double HighlightY
    {
        get => GetValue(HighlightYProperty);
        set => SetValue(HighlightYProperty, value);
    }

    public double Corner
    {
        get => GetValue(CornerProperty);
        set => SetValue(CornerProperty, value);
    }

    public double Strength
    {
        get => GetValue(StrengthProperty);
        set => SetValue(StrengthProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 2 || rect.Height <= 2 || Strength <= 0)
        {
            return;
        }

        using (context.PushClip(new RoundedRect(rect, Corner)))
        {
            var centre = new Point(rect.Width * Math.Clamp(HighlightX, 0, 1), rect.Height * Math.Clamp(HighlightY, 0, 1));
            var shine = ArcGlass.Lift(ArcGlass.Color(ArcTokens.Surface), 0.55);

            var sheen = new RadialGradientBrush
            {
                Center = new RelativePoint(HighlightX, HighlightY, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(HighlightX, HighlightY, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.62, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.62, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ArcColor.WithAlpha(shine, 0.18 * Strength), 0),
                    new GradientStop(ArcColor.WithAlpha(shine, 0.05 * Strength), 0.55),
                    new GradientStop(ArcColor.WithAlpha(shine, 0), 1)
                }
            };
            context.DrawRectangle(sheen, null, rect);

            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(centre.X, centre.Y, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(
                    rect.Width - centre.X + 1,
                    rect.Height - centre.Y + 1,
                    RelativeUnit.Absolute),
                GradientStops =
                {
                    new GradientStop(ArcColor.WithAlpha(shine, 0.62 * Strength), 0),
                    new GradientStop(ArcColor.WithAlpha(shine, 0.12 * Strength), 0.42),
                    new GradientStop(ArcColor.WithAlpha(shine, 0.38 * Strength), 1)
                }
            };
            var pen = new Pen(brush, 1.5);
            context.DrawRectangle(null, pen, rect.Deflate(0.75));
        }
    }
}

/// <summary>
/// A glass info panel over artwork. The rim catches a pointer following highlight, the card leans a few
/// degrees toward the pointer on a spring, and a tap morphs the glass header into a detail sheet that
/// grows out of the same box.
///
/// Platform approximations: the tilt is a Rotate3DTransform with a fixed perspective depth instead of a
/// CSS perspective matrix, and the highlight is a painted gradient because Avalonia cannot mask a
/// gradient to a border. The detail sheet expands within the card rather than becoming a separate sheet,
/// which keeps one element morphing. Pointer tilt is skipped under reduced motion and for touch input.
/// No Pro source was used.
/// </summary>
public sealed class ArcGlassCard : Panel
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcGlassCard, string>(nameof(Title), "Jasmine Brooks");

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ArcGlassCard, string?>(nameof(Subtitle), "Design lead, Lisbon");

    public static readonly StyledProperty<string?> AsideProperty =
        AvaloniaProperty.Register<ArcGlassCard, string?>(nameof(Aside), "4.9");

    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<ArcGlassCard, string?>(nameof(Body));

    public static readonly StyledProperty<string?> ActionLabelProperty =
        AvaloniaProperty.Register<ArcGlassCard, string?>(nameof(ActionLabel), "Follow");

    public static readonly StyledProperty<string?> SecondaryLabelProperty =
        AvaloniaProperty.Register<ArcGlassCard, string?>(nameof(SecondaryLabel), "Message");

    public static readonly StyledProperty<string> ImageAltProperty =
        AvaloniaProperty.Register<ArcGlassCard, string>(nameof(ImageAlt), "Portrait of Jasmine Brooks");

    public static readonly StyledProperty<double> TiltProperty =
        AvaloniaProperty.Register<ArcGlassCard, double>(nameof(Tilt), 6);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcGlassCard, bool>(nameof(IsOpen));

    public static readonly StyledProperty<ArcGlassScene> SceneProperty =
        AvaloniaProperty.Register<ArcGlassCard, ArcGlassScene>(nameof(Scene), ArcGlassScene.Studio);

    private const double Radius = 26;

    private readonly Border _card;
    private readonly ArcGlassBackdrop _photo;
    private readonly Border _shade;
    private readonly ArcCardRim _rim;
    private readonly Border _sheet;
    private readonly Border _header;
    private readonly ArcButton _headerButton;
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;
    private readonly TextBlock _aside;
    private readonly StackPanel _details;
    private readonly GlassSpring _angleX = new(0, 120);
    private readonly GlassSpring _angleY = new(0, 120);
    private readonly GlassSpring _lightX = new(0.3, 90);
    private readonly GlassSpring _lightY = new(0.2, 90);
    private readonly ArcFrameLoop _loop;
    private bool _wasOpen;
    private bool _tiltDriven;
    private GlassTween? _tween;

    static ArcGlassCard()
    {
        TitleProperty.Changed.AddClassHandler<ArcGlassCard>((card, _) => card.UpdateCopy());
        SubtitleProperty.Changed.AddClassHandler<ArcGlassCard>((card, _) => card.UpdateCopy());
        AsideProperty.Changed.AddClassHandler<ArcGlassCard>((card, _) => card.UpdateCopy());
        BodyProperty.Changed.AddClassHandler<ArcGlassCard>((card, _) => card.UpdateCopy());
        ActionLabelProperty.Changed.AddClassHandler<ArcGlassCard>((card, _) => card.UpdateCopy());
        SecondaryLabelProperty.Changed.AddClassHandler<ArcGlassCard>((card, _) => card.UpdateCopy());
        IsOpenProperty.Changed.AddClassHandler<ArcGlassCard>((card, _) => card.SyncOpen());
        SceneProperty.Changed.AddClassHandler<ArcGlassCard>((card, _) => card._photo.Scene = card.Scene);
    }

    public ArcGlassCard()
    {
        ClipToBounds = false;
        Width = 320;
        Height = 400;

        _photo = new ArcGlassBackdrop
        {
            Scene = Scene,
            Corner = Radius,
            Seed = 6,
            Vignette = 0.35
        };

        _shade = new Border
        {
            Background = ArcGlass.PhotoShade(0.35),
            IsHitTestVisible = false
        };

        _rim = new ArcCardRim { Corner = Radius, IsHitTestVisible = false };

        _title = new TextBlock
        {
            Text = Title,
            FontSize = 16,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        };
        _subtitle = new TextBlock
        {
            Text = Subtitle,
            FontSize = 12,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _aside = new TextBlock
        {
            Text = Aside,
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        };

        var identity = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _title, _subtitle }
        };

        var asideRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ArcGlass.Icon("star", 13, ArcGlass.Solid(ArcGlass.Color(ArcTokens.Accent))),
                _aside
            }
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { identity, asideRow }
        };
        Grid.SetColumn(asideRow, 1);

        _headerButton = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(14),
            Content = headerGrid
        };
        _headerButton.Classes.Add("sg-plain");
        _headerButton.Click += (_, _) => Toggle();
        _headerButton.KeyDown += OnHeaderKeyDown;
        AutomationProperties.SetName(_headerButton, Title);

        _header = new Border { Child = _headerButton };

        _details = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(4, 10, 4, 2),
            Opacity = 0,
            IsVisible = false
        };

        _sheet = ArcGlass.Panel(Radius, new Thickness(10), floating: true, fillAlpha: 0.72);
        _sheet.CornerRadius = new CornerRadius(18);
        _sheet.Child = new StackPanel { Spacing = 0, Children = { _header, _details } };
        _sheet.VerticalAlignment = VerticalAlignment.Bottom;
        _sheet.Margin = new Thickness(10);
        _sheet.Height = 78;
        _sheet.ClipToBounds = true;

        _card = new Border
        {
            CornerRadius = new CornerRadius(Radius),
            ClipToBounds = true,
            BorderBrush = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Surface, 0.28)),
            BorderThickness = new Thickness(1),
            BoxShadow = ArcGlass.Shadow(ArcTokens.ShadowFloating),
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Child = new Panel { Children = { _photo, _shade, _sheet, _rim } }
        };

        Children.Add(_card);

        _loop = new ArcFrameLoop(OnFrame);
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        KeyDown += OnKeyDown;

        UpdateCopy();
        BuildDetails();
    }

    public event EventHandler<bool>? OpenChanged;

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

    public string? Aside
    {
        get => GetValue(AsideProperty);
        set => SetValue(AsideProperty, value);
    }

    public string? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public string? ActionLabel
    {
        get => GetValue(ActionLabelProperty);
        set => SetValue(ActionLabelProperty, value);
    }

    public string? SecondaryLabel
    {
        get => GetValue(SecondaryLabelProperty);
        set => SetValue(SecondaryLabelProperty, value);
    }

    public string ImageAlt
    {
        get => GetValue(ImageAltProperty);
        set => SetValue(ImageAltProperty, value);
    }

    /// <summary>Largest lean in degrees; 0 turns tilt off.</summary>
    public double Tilt
    {
        get => GetValue(TiltProperty);
        set => SetValue(TiltProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public ArcGlassScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    private void UpdateCopy()
    {
        _title.Text = Title;
        _subtitle.Text = Subtitle;
        _subtitle.IsVisible = !string.IsNullOrWhiteSpace(Subtitle);
        _aside.Text = Aside;
        AutomationProperties.SetName(_card, ImageAlt);
        AutomationProperties.SetName(_headerButton, Title);
        AutomationProperties.SetHelpText(_headerButton, IsOpen
            ? "Collapses the details"
            : "Expands the details. Escape collapses them.");
        BuildDetails();
    }

    private void BuildDetails()
    {
        _details.Children.Clear();

        if (!string.IsNullOrWhiteSpace(Body))
        {
            _details.Children.Add(new TextBlock
            {
                Text = Body,
                Classes = { "arc-body-sm" },
                TextWrapping = TextWrapping.Wrap,
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
            });
        }

        var stats = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            Margin = new Thickness(0, 2, 0, 0)
        };
        AddStat(stats, 0, "128", "Reviews");
        AddStat(stats, 1, "4.9", "Rating");
        AddStat(stats, 2, "6 yrs", "Hosting");
        _details.Children.Add(stats);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 2, 0, 0)
        };
        var follow = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Content = ActionLabel ?? "Follow",
            Height = 34,
            MinHeight = 34,
            Padding = new Thickness(14, 0)
        };
        AutomationProperties.SetName(follow, ActionLabel ?? "Follow");
        follow.Click += (_, _) => AutomationProperties.SetItemStatus(follow, "Following");
        actions.Children.Add(follow);

        var message = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = SecondaryLabel ?? "Message",
            Height = 34,
            MinHeight = 34,
            Padding = new Thickness(12, 0),
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.14)),
            BorderBrush = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.30)),
            BorderThickness = new Thickness(1)
        };
        message.Classes.Add("sg-plain");
        AutomationProperties.SetName(message, SecondaryLabel ?? "Message");
        actions.Children.Add(message);
        _details.Children.Add(actions);
    }

    private void AddStat(Grid grid, int column, string value, string label)
    {
        var stack = new StackPanel { Spacing = 1 };
        stack.Children.Add(new TextBlock
        {
            Text = value,
            Classes = { "arc-label" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        });
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    private void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);

    private void OnHeaderKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            SetCurrentValue(IsOpenProperty, false);
            _headerButton.Focus();
        }
    }

    private void SyncOpen()
    {
        if (IsOpen == _wasOpen)
        {
            return;
        }

        _wasOpen = IsOpen;
        AutomationProperties.SetHelpText(_headerButton, IsOpen
            ? "Collapses the details"
            : "Expands the details. Escape collapses them.");
        AutomationProperties.SetItemStatus(_headerButton, IsOpen ? "Expanded" : "Collapsed");

        _details.IsVisible = true;
        _details.Measure(new Size(Math.Max(120, (Width > 0 ? Width : 320) - 44), double.PositiveInfinity));
        var detailHeight = _details.DesiredSize.Height + 12;
        var headerHeight = _header.DesiredSize.Height > 0 ? _header.DesiredSize.Height : 56;
        var collapsed = headerHeight + 22;
        var expanded = Math.Min((Height > 0 ? Height : 400) - 26, collapsed + detailHeight + 8);

        _tween?.Stop();
        if (ArcThemeManager.ReducedMotion)
        {
            _sheet.Height = IsOpen ? expanded : collapsed;
            _details.Opacity = IsOpen ? 1 : 0;
            _details.IsVisible = IsOpen;
            OpenChanged?.Invoke(this, IsOpen);
            return;
        }

        var from = _sheet.Height > 0 ? _sheet.Height : collapsed;
        var to = IsOpen ? expanded : collapsed;
        _tween = GlassTween.Run(GlassMotion.Smooth, GlassMotion.SmoothEase, progress =>
        {
            _sheet.Height = from + ((to - from) * progress);
            _details.Opacity = Math.Clamp(IsOpen ? (progress - 0.25) / 0.75 : progress, 0, 1);
        }, () =>
        {
            if (!IsOpen)
            {
                _details.IsVisible = false;
                _details.Opacity = 0;
            }

            OpenChanged?.Invoke(this, IsOpen);
        });
    }

    // ---------------------------------------------------------------- pointer

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is Visual source && IsWithin(source, _sheet))
        {
            return;
        }

        Toggle();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse || ArcThemeManager.ReducedMotion || Tilt <= 0)
        {
            return;
        }

        var point = e.GetPosition(this);
        var width = Math.Max(1, Bounds.Width);
        var height = Math.Max(1, Bounds.Height);
        var nx = ((point.X / width) * 2) - 1;
        var ny = ((point.Y / height) * 2) - 1;
        _angleY.Target = nx * Tilt;
        _angleX.Target = -ny * Tilt;
        _lightX.Target = Math.Clamp(point.X / width, 0, 1);
        _lightY.Target = Math.Clamp(point.Y / height, 0, 1);
        _tiltDriven = true;
        _loop.Start();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _angleX.Target = 0;
        _angleY.Target = 0;
        _lightX.Target = 0.3;
        _lightY.Target = 0.18;
        _tiltDriven = false;
        _loop.Start();
    }

    private void OnFrame(double dt)
    {
        _angleX.Update(dt);
        _angleY.Update(dt);
        _lightX.Update(dt);
        _lightY.Update(dt);

        _card.RenderTransform = new Rotate3DTransform(
            _angleX.Value,
            _angleY.Value,
            0,
            Bounds.Width / 2,
            Bounds.Height / 2,
            0,
            620);
        _rim.HighlightX = _lightX.Value;
        _rim.HighlightY = _lightY.Value;
        _rim.InvalidateVisual();

        if (!_tiltDriven && _angleX.Settled && _angleY.Settled && _lightX.Settled && _lightY.Settled)
        {
            _loop.Stop();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _wasOpen = false;
        if (IsOpen)
        {
            SyncOpen();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _loop.Dispose();
        _tween?.Stop();
        base.OnDetachedFromVisualTree(e);
    }

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
}





