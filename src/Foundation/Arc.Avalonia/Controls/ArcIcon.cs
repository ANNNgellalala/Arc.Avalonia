using System;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Icons;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Controls;

/// <summary>
/// A Lucide stroke icon on the 24 unit grid, drawn with the Arc stroke weight (1.75) and round joins.
/// The geometry scales to the control bounds so one data set serves every size.
/// </summary>
public class ArcIcon : Control
{
    private static readonly ConcurrentDictionary<string, Geometry?> Cache = new(StringComparer.Ordinal);

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<ArcIcon, string?>(nameof(Icon));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ArcIcon, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<ArcIcon, double>(nameof(StrokeWidth), 1.75);

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ArcIcon, double>(nameof(Size), 20);

    static ArcIcon()
    {
        AffectsRender<ArcIcon>(IconProperty, ForegroundProperty, StrokeWidthProperty);
        AffectsMeasure<ArcIcon>(SizeProperty);
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Stroke colour; defaults to the secondary text token.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    /// <summary>Layout size in px; bounds win when a Width or Height is set.</summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    /// <summary>
    /// Registers or overrides a single icon path at runtime (M2-G3g). Thread-safe; the parsed-geometry
    /// cache for that name is dropped so the next render picks up the new data. <see cref="Icons.ArcIconData.Paths"/>
    /// itself stays read-only.
    /// </summary>
    public static void Register(string name, string pathData)
    {
        ArcIconRegistry.Register(name, pathData);
        Cache.TryRemove(name, out _);
    }

    /// <summary>Unregisters a runtime override; the built-in icon (if any) takes over again.</summary>
    public static bool Unregister(string name)
    {
        Cache.TryRemove(name, out _);
        return ArcIconRegistry.Unregister(name);
    }

    /// <summary>
    /// Registers an <see cref="IArcIconProvider"/>: consulted after runtime registrations and before the
    /// built-in table, so a host can plug in a whole icon set without patching Arc.
    /// </summary>
    public static void Register(IArcIconProvider provider)
    {
        ArcIconRegistry.Register(provider);
        Cache.Clear();
    }

    /// <summary>Resolves an icon name through the runtime registry (registrations → providers → built-ins).</summary>
    public static bool TryGetPath(string name, out string? pathData) => ArcIconRegistry.TryGetPath(name, out pathData);

    public override void Render(DrawingContext context)
    {
        var name = Icon;
        if (string.IsNullOrEmpty(name) || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var geometry = Cache.GetOrAdd(name, static key =>
        {
            if (!ArcIconRegistry.TryGetPath(key, out var data) || string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            try
            {
                return Geometry.Parse(data);
            }
            catch
            {
                return null;
            }
        });

        if (geometry is null)
        {
            return;
        }

        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var brush = Foreground ?? ArcResources.Brush(ArcTokens.TextSecondary);
        var pen = new Pen(brush, StrokeWidth, null, PenLineCap.Round, PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }
}

