using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Chat;

/// <summary>
/// Token-bound building blocks shared by the chat controls: semantic brush bindings (so accent/theme
/// switches restyle live), the small type scale, avatars, dates and byte formatting. Colours always come
/// from <see cref="ArcTokens"/>; nothing here holds a raw colour.
/// </summary>
internal static class ArcChatUi
{
    /// <summary>Binds a property to a semantic token resource so light/dark and accents update live.</summary>
    public static T Paint<T>(this T control, AvaloniaProperty property, string token) where T : AvaloniaObject
    {
        if (control is Control host)
        {
            host.Bind(property, host.GetResourceObservable(token));
        }
        else
        {
            control.SetValue(property, ArcResources.Brush(token));
        }

        return control;
    }

    public static IBrush Brush(string token) => ArcResources.Brush(token);

    public static CornerRadius Radius(string token) => ArcResources.Radius(token);

    public static double Space(string token) => ArcResources.Double(token);

    public static double TextSize(string token, double fallback)
    {
        var value = ArcResources.Double(token);
        return value > 0 ? value : fallback;
    }

    public static TextBlock Text(string text, string sizeToken, string brushToken)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        block.FontSize = TextSize(sizeToken, 14);
        block.Paint(TextBlock.ForegroundProperty, brushToken);
        return block;
    }

    public static TextBlock Caption(string text) => Text(text, ArcTokens.TextXs, ArcTokens.TextMuted);

    public static ArcIcon Icon(string name, double size = 16, string? token = ArcTokens.TextSecondary)
    {
        var icon = new ArcIcon { Icon = name, Size = size, VerticalAlignment = VerticalAlignment.Center };
        if (token is not null)
        {
            icon.Paint(ArcIcon.ForegroundProperty, token);
        }

        return icon;
    }

    /// <summary>Round initials avatar; loads an image when the participant has one.</summary>
    public static Border Avatar(ArcChatParticipant? participant, bool own, double diameter = 28)
    {
        var border = new Border
        {
            Width = diameter,
            Height = diameter,
            CornerRadius = new CornerRadius(diameter / 2),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top,
        };

        border.Paint(Border.BorderBrushProperty, own ? ArcTokens.Accent : ArcTokens.BorderSubtle);
        var image = LoadAvatar(participant?.Avatar);
        if (image is not null)
        {
            border.Background = image;
        }
        else
        {
            border.Paint(Border.BackgroundProperty, own ? ArcTokens.AccentSubtle : ArcTokens.SurfaceMuted);
            var initials = new TextBlock
            {
                Text = Initials(participant?.Name),
                FontSize = Math.Max(10, diameter * 0.36),
                FontWeight = FontWeight.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            initials.Paint(TextBlock.ForegroundProperty, own ? ArcTokens.AccentStrong : ArcTokens.TextSecondary);
            border.Child = initials;
        }

        AutomationProperties.SetName(border, participant?.Name ?? "Avatar");
        return border;
    }

    private static ImageBrush? LoadAvatar(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        try
        {
            Bitmap? bitmap = null;
            if (source.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = AssetLoader.Open(new Uri(source));
                bitmap = new Bitmap(stream);
            }
            else if (File.Exists(source))
            {
                bitmap = new Bitmap(source);
            }

            return bitmap is null
                ? null
                : new ImageBrush(bitmap)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                };
        }
        catch
        {
            return null;
        }
    }

    public static string Initials(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "?";
        }

        var first = char.ToUpperInvariant(trimmed[0]);
        var space = trimmed.IndexOf(' ');
        return space > 0 && space + 1 < trimmed.Length
            ? string.Concat(first, char.ToUpperInvariant(trimmed[space + 1]))
            : first.ToString();
    }

    public static string FormatTime(DateTimeOffset time)
        => time.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);

    public static string FormatDay(DateTimeOffset time)
    {
        var local = time.ToLocalTime().Date;
        var today = DateTimeOffset.Now.ToLocalTime().Date;
        if (local == today)
        {
            return "今天";
        }

        if (local == today.AddDays(-1))
        {
            return "昨天";
        }

        return local.Year == today.Year
            ? local.ToString("M月d日", CultureInfo.CurrentCulture)
            : local.ToString("yyyy年M月d日", CultureInfo.CurrentCulture);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return string.Create(CultureInfo.CurrentCulture, $"{bytes / 1024.0:0.#} KB");
        }

        return string.Create(CultureInfo.CurrentCulture, $"{bytes / (1024.0 * 1024.0):0.#} MB");
    }

    /// <summary>Rounded pill used for day separators and system notices.</summary>
    public static Border Pill(Control content, string backgroundToken, string borderToken)
    {
        var pill = new Border
        {
            CornerRadius = Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(10, 3, 10, 3),
            Child = content,
        };
        pill.Paint(Border.BackgroundProperty, backgroundToken);
        pill.Paint(Border.BorderBrushProperty, borderToken);
        pill.BorderThickness = new Thickness(1);
        return pill;
    }
}
