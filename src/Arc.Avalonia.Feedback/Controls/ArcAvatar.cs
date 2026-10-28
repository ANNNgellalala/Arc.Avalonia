using System;
using System.IO;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

public enum ArcAvatarSize
{
    Xs,
    Sm,
    Md,
    Lg,
    Xl
}

public enum ArcAvatarStatus
{
    None,
    Online,
    Offline
}

public enum ArcAvatarShape
{
    Round,
    Square
}

/// <summary>
/// A compact identity marker. A photo is loaded from a file path or an avares URI and falls back
/// to the person's initials; the status dot carries presence in addition to its colour.
/// </summary>
public class ArcAvatar : TemplatedControl
{
    public static readonly StyledProperty<string> DisplayNameProperty =
        AvaloniaProperty.Register<ArcAvatar, string>(nameof(DisplayName), string.Empty);

    public static readonly StyledProperty<string?> SrcProperty =
        AvaloniaProperty.Register<ArcAvatar, string?>(nameof(Src));

    public static readonly StyledProperty<ArcAvatarSize> SizeProperty =
        AvaloniaProperty.Register<ArcAvatar, ArcAvatarSize>(nameof(Size));

    public static readonly StyledProperty<ArcAvatarStatus> StatusProperty =
        AvaloniaProperty.Register<ArcAvatar, ArcAvatarStatus>(nameof(Status));

    public static readonly StyledProperty<ArcAvatarShape> ShapeProperty =
        AvaloniaProperty.Register<ArcAvatar, ArcAvatarShape>(nameof(Shape));

    private Border? _surface;
    private TextBlock? _initials;
    private Ellipse? _dot;
    private ImageBrush? _image;
    private string? _loadedSrc;

    static ArcAvatar()
    {
        DisplayNameProperty.Changed.AddClassHandler<ArcAvatar>((avatar, _) => avatar.UpdateContent());
        SrcProperty.Changed.AddClassHandler<ArcAvatar>((avatar, _) => avatar.UpdateContent());
        SizeProperty.Changed.AddClassHandler<ArcAvatar>((avatar, _) => avatar.UpdateSize());
        StatusProperty.Changed.AddClassHandler<ArcAvatar>((avatar, _) => avatar.UpdateStatus());
        ShapeProperty.Changed.AddClassHandler<ArcAvatar>((avatar, _) => avatar.UpdateSize());
    }

    /// <summary>Full name; drives the accessible label and the first two initials.</summary>
    public string DisplayName
    {
        get => GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    /// <summary>Photo path or avares URI. Falls back to initials when it cannot be read.</summary>
    public string? Src
    {
        get => GetValue(SrcProperty);
        set => SetValue(SrcProperty, value);
    }

    public ArcAvatarSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public ArcAvatarStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public ArcAvatarShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _surface = e.NameScope.Find<Border>("Surface");
        _initials = e.NameScope.Find<TextBlock>("Initials");
        _dot = e.NameScope.Find<Ellipse>("Dot");
        UpdateSize();
        UpdateContent();
        UpdateStatus();
    }

    private double Diameter => Size switch
    {
        ArcAvatarSize.Xs => 24,
        ArcAvatarSize.Sm => 28,
        ArcAvatarSize.Md => 40,
        ArcAvatarSize.Lg => 56,
        _ => 88
    };

    private void UpdateSize()
    {
        if (_surface is null)
        {
            return;
        }

        var diameter = Diameter;
        Width = diameter;
        Height = diameter;
        _surface.Width = diameter;
        _surface.Height = diameter;
        _surface.CornerRadius = Shape == ArcAvatarShape.Round
            ? new CornerRadius(diameter / 2)
            : new CornerRadius(Math.Clamp(diameter * 0.3, 6, 24));

        if (_initials is not null)
        {
            _initials.FontSize = Size switch
            {
                ArcAvatarSize.Xs => 10,
                ArcAvatarSize.Sm => 11,
                ArcAvatarSize.Md => 14,
                ArcAvatarSize.Lg => 19,
                _ => 28
            };
        }

        if (_dot is not null)
        {
            var dot = Math.Clamp(diameter * 0.28, 8, 18);
            _dot.Width = dot;
            _dot.Height = dot;
            _dot.StrokeThickness = Math.Max(2, dot * 0.14);
        }
    }

    private void UpdateStatus()
    {
        if (_dot is null)
        {
            return;
        }

        _dot.IsVisible = Status != ArcAvatarStatus.None;
        _dot.Paint(global::Avalonia.Controls.Shapes.Shape.FillProperty, Status == ArcAvatarStatus.Online ? ArcTokens.Success : ArcTokens.TextMuted);
        _dot.Paint(global::Avalonia.Controls.Shapes.Shape.StrokeProperty, ArcTokens.Surface);

        var label = DisplayName;
        if (Status == ArcAvatarStatus.Online)
        {
            label += ", online";
        }
        else if (Status == ArcAvatarStatus.Offline)
        {
            label += ", offline";
        }

        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(label) ? "Avatar" : label);
    }

    private void UpdateContent()
    {
        if (_surface is null || _initials is null)
        {
            return;
        }

        var (image, succeeded) = LoadImage();
        if (succeeded && image is not null)
        {
            _surface.Background = image;
            _initials.IsVisible = false;
        }
        else
        {
            _surface.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            _initials.IsVisible = true;
            _initials.Text = FeedbackUi.Initials(DisplayName);
            _initials.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        }

        UpdateStatus();
    }

    private (ImageBrush? Brush, bool Ok) LoadImage()
    {
        if (string.IsNullOrWhiteSpace(Src))
        {
            _image = null;
            _loadedSrc = null;
            return (null, false);
        }

        if (_image is not null && string.Equals(_loadedSrc, Src, StringComparison.Ordinal))
        {
            return (_image, true);
        }

        try
        {
            Bitmap? bitmap = null;
            if (Src.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = AssetLoader.Open(new Uri(Src));
                bitmap = new Bitmap(stream);
            }
            else if (File.Exists(Src))
            {
                bitmap = new Bitmap(Src);
            }

            if (bitmap is null)
            {
                _image = null;
                _loadedSrc = null;
                return (null, false);
            }

            _image = new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            _loadedSrc = Src;
            return (_image, true);
        }
        catch
        {
            _image = null;
            _loadedSrc = null;
            return (null, false);
        }
    }
}
