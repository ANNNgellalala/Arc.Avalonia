using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Controls;

/// <summary>
/// Text or a number where every character is a reel. On change, digits count through the wheel in the
/// direction the number moved, letters shuffle through a short alphabet, and the reels land left to right
/// without overshoot. Reduced motion swaps the value instantly.
/// </summary>
public class ArcSlotText : ArcMotionCanvas
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcSlotText, string?>(nameof(Value), "0");

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<ArcSlotText, double>(nameof(FontSize), 72);

    public static readonly StyledProperty<double> SpinDurationProperty =
        AvaloniaProperty.Register<ArcSlotText, double>(nameof(SpinDuration), 900);

    public static readonly StyledProperty<double> StaggerProperty =
        AvaloniaProperty.Register<ArcSlotText, double>(nameof(Stagger), 70);

    public static readonly StyledProperty<int> SpinsProperty =
        AvaloniaProperty.Register<ArcSlotText, int>(nameof(Spins), 1);

    private const string Digits = "0123456789";
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private List<Reel>? _reels;
    private double _progress;
    private double _total;

    static ArcSlotText()
    {
        ValueProperty.Changed.AddClassHandler<ArcSlotText>((text, change) =>
            text.StartSpin(change.OldValue as string, change.NewValue as string));
        FontSizeProperty.Changed.AddClassHandler<ArcSlotText>((text, _) => text.InvalidateMeasure());
    }

    public ArcSlotText()
    {
        Focusable = false;
        IsTabStop = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    /// <summary>The value to show. Every character is its own reel.</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Font size the value is drawn at; the display face is used from 28 px up.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Seconds the first reel spins.</summary>
    public double SpinDuration
    {
        get => GetValue(SpinDurationProperty);
        set => SetValue(SpinDurationProperty, value);
    }

    /// <summary>Milliseconds between reels stopping, left to right.</summary>
    public double Stagger
    {
        get => GetValue(StaggerProperty);
        set => SetValue(StaggerProperty, value);
    }

    /// <summary>Extra full turns before landing; 0 rolls straight to the new character.</summary>
    public int Spins
    {
        get => GetValue(SpinsProperty);
        set => SetValue(SpinsProperty, value);
    }

    protected override double DefaultWidth => 240;

    protected override double DefaultHeight => 80;

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Sm.Text(Value ?? string.Empty, FontSize, Brushes.Transparent, FontWeight.Medium, family: Face());
        var width = double.IsInfinity(availableSize.Width) ? Math.Max(24, text.Width) : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? Math.Max(FontSize * 1.35, text.Height + 8) : availableSize.Height;
        return new Size(width, height);
    }

    private FontFamily Face() => FontSize >= 28 ? Sm.Font(ArcTokens.FontDisplay) : Sm.Font(ArcTokens.FontBody);

    private void StartSpin(string? oldValue, string? newValue)
    {
        var from = oldValue ?? string.Empty;
        var to = newValue ?? string.Empty;
        AutomationProperties.SetName(this, to);

        if (Sm.Reduced || from.Length == 0 || from == to)
        {
            _reels = null;
            _progress = 1;
            InvalidateVisual();
            return;
        }

        _reels = BuildReels(from, to);
        if (_reels is null)
        {
            InvalidateVisual();
            return;
        }

        _total = SpinDuration + Stagger * Math.Max(0, _reels.Count - 1);
        _progress = 0;
        Animate(_total, ArcMotion.EaseStandard, progress =>
        {
            _progress = progress;
            InvalidateVisual();
        }, () =>
        {
            _reels = null;
            InvalidateVisual();
        });
    }

    /// <summary>Builds one reel per final character, matched from the end for numbers so separators stay put.</summary>
    private List<Reel>? BuildReels(string from, string to)
    {
        if (string.IsNullOrEmpty(to))
        {
            return null;
        }

        var endAligned = IsNumeric(to);
        var reels = new List<Reel>(to.Length);
        var face = Face();
        var reference = Sm.Text("0", FontSize, Brushes.Transparent, FontWeight.Medium, family: face);
        var digitWidth = reference.Width;

        // A shared leading symbol such as a currency sign stays on its own static reel, so only digits spin.
        var prefix = 0;
        while (prefix < from.Length && prefix < to.Length && from[prefix] == to[prefix] && !char.IsDigit(from[prefix]))
        {
            prefix++;
        }

        var innerFrom = from[prefix..];
        var innerTo = to[prefix..];

        for (var index = 0; index < to.Length; index++)
        {
            var target = to[index];
            var source = '\0';
            if (index < prefix)
            {
                source = target;
            }
            else if (endAligned)
            {
                var fromIndex = innerFrom.Length - (innerTo.Length - (index - prefix));
                if (fromIndex >= 0 && fromIndex < innerFrom.Length)
                {
                    source = innerFrom[fromIndex];
                }
            }
            else if (index < from.Length)
            {
                source = from[index];
            }

            var cells = Cells(source, target);
            var width = char.IsDigit(target) ? digitWidth : Sm.Text(target.ToString(), FontSize, Brushes.Transparent, FontWeight.Medium, family: face).Width;
            reels.Add(new Reel(target, cells, Math.Max(1, width)));
        }

        return reels;
    }

    private char[] Cells(char from, char to)
    {
        if (from == '\0' || from == to)
        {
            return new[] { to };
        }

        var alphabet = char.IsDigit(to) ? Digits : char.IsLower(to) && char.IsLower(from) ? Lower : char.IsUpper(to) && char.IsUpper(from) ? Upper : string.Empty;
        if (alphabet.Length == 0)
        {
            return new[] { from, to };
        }

        var start = alphabet.IndexOf(from);
        var end = alphabet.IndexOf(to);
        if (start < 0 || end < 0)
        {
            return new[] { from, to };
        }

        var steps = ((end - start) % alphabet.Length + alphabet.Length) % alphabet.Length;
        if (steps == 0)
        {
            return new[] { to };
        }

        steps += Math.Max(0, Spins) * alphabet.Length;
        var cells = new char[steps + 1];
        for (var step = 0; step <= steps; step++)
        {
            cells[step] = alphabet[(start + step) % alphabet.Length];
        }

        return cells;
    }

    private static bool IsNumeric(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsDigit(character) && character is not (',' or '.' or '%' or '$' or '€' or '£' or ' ' or '-' or '+'))
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        var value = Value ?? string.Empty;
        if (value.Length == 0)
        {
            return;
        }

        var face = Face();
        if (_reels is null)
        {
            var text = Sm.Text(value, FontSize, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium, family: face);
            Sm.TextCentered(context, text, size.Width / 2, size.Height / 2);
            return;
        }

        var cellHeight = FontSize * 1.16;
        var top = size.Height / 2 - cellHeight / 2;
        var totalWidth = 0.0;
        foreach (var reel in _reels)
        {
            totalWidth += reel.Width;
        }

        var x = size.Width / 2 - totalWidth / 2;
        for (var index = 0; index < _reels.Count; index++)
        {
            var reel = _reels[index];
            var local = _total <= 0 ? 1 : Sm.Clamp01((_progress * _total - Stagger * index) / Math.Max(1, SpinDuration));
            var eased = ArcMotion.EaseEnter.Ease(local);
            var offset = eased * (reel.Cells.Length - 1);
            var low = (int)Math.Floor(offset);
            var fraction = offset - low;

            using (context.PushClip(new Rect(x, top, reel.Width, cellHeight)))
            {
                var lowOpacity = local >= 1 ? 1 : 1 - 0.45 * fraction;
                DrawCell(context, reel, low, x, top, cellHeight, face, -fraction * cellHeight, lowOpacity);
                if (fraction > 0.01 && low + 1 < reel.Cells.Length)
                {
                    DrawCell(context, reel, low + 1, x, top, cellHeight, face, (1 - fraction) * cellHeight, 0.55 + 0.45 * fraction);
                }
            }

            x += reel.Width;
        }
    }

    private void DrawCell(
        DrawingContext context,
        Reel reel,
        int cell,
        double x,
        double top,
        double cellHeight,
        FontFamily face,
        double dy,
        double opacity)
    {
        var character = reel.Cells[Math.Clamp(cell, 0, reel.Cells.Length - 1)].ToString();
        var brush = Sm.Brush(ArcTokens.Foreground);
        if (opacity < 0.999)
        {
            brush = Sm.Solid(ArcColor.WithAlpha(ArcResources.Color(ArcTokens.Foreground), opacity));
        }

        var text = Sm.Text(character, FontSize, brush, FontWeight.Medium, family: face);
        context.DrawText(text, new Point(x + (reel.Width - text.Width) / 2, top + dy + (cellHeight - text.Height) / 2));
    }

    private sealed record Reel(char Target, char[] Cells, double Width);
}
