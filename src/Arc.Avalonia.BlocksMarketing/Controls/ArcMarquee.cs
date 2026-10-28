using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksMarketing.Controls;

/// <summary>
/// A quiet, continuously moving row of marks with a pause control, used by the logo marquee block. Two
/// identical copies of the row travel left on a linear loop, so the seam is never visible; the animation is
/// a render transform, never a layout pass. Under reduced motion the row holds still and the pause control
/// reports the still frame. The web original loops with CSS; this uses one timer and a translate.
/// </summary>
public sealed class ArcMarquee : Border
{
    private readonly StackPanel _track;
    private readonly DispatcherTimer _timer;
    private readonly double _speed;
    private readonly int _count;
    private double _offset;
    private double _copyWidth;

    public ArcMarquee(int count, Func<int, Control> item, double speed = 26, double gap = 44, double height = 64)
    {
        _count = count;
        _speed = speed;

        ClipToBounds = true;
        Height = height;
        Background = Brushes.Transparent;

        _track = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        _track.RenderTransform = new TranslateTransform();

        for (var copy = 0; copy < 2; copy++)
        {
            for (var index = 0; index < count; index++)
            {
                var child = item(index);
                child.Margin = new Thickness(0, 0, gap, 0);
                child.VerticalAlignment = VerticalAlignment.Center;
                _track.Children.Add(child);
            }
        }

        Child = _track;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        LayoutUpdated += (_, _) => MeasureCopy();
        AttachedToVisualTree += (_, _) => Start();
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    /// <summary>Pauses the loop; the marks stay where they are.</summary>
    public bool Paused { get; private set; }

    /// <summary>True when the loop cannot move at all, because reduced motion is on.</summary>
    public bool IsStill => ArcThemeManager.ReducedMotion;

    public bool IsMoving => !Paused && !ArcThemeManager.ReducedMotion;

    public event EventHandler? PauseChanged;

    public void TogglePause()
    {
        Paused = !Paused;
        PauseChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Start()
    {
        if (!ArcThemeManager.ReducedMotion)
        {
            _timer.Start();
        }
    }

    private void MeasureCopy()
    {
        if (_count <= 0 || _track.Children.Count < _count * 2)
        {
            return;
        }

        var width = 0d;
        for (var index = 0; index < _count; index++)
        {
            width += _track.Children[index].Bounds.Width + _track.Children[index].Margin.Right;
        }

        if (width > 1)
        {
            _copyWidth = width;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (Paused || _copyWidth <= 0 || ArcThemeManager.ReducedMotion)
        {
            return;
        }

        // The timer runs at a fixed 16 ms cadence, so the step is a constant 1/62.5 of a second of travel.
        _offset += _speed * 0.016;
        if (_offset >= _copyWidth)
        {
            _offset -= _copyWidth;
        }

        if (_track.RenderTransform is TranslateTransform transform)
        {
            transform.X = -_offset;
        }
    }
}

