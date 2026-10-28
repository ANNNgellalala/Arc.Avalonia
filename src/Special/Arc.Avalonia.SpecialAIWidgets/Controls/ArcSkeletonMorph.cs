using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIWidgets.Controls;

/// <summary>One piece of a loaded layout: a shape-matched skeleton while loading, then the real content in place.</summary>
public class ArcMorphBlock : Panel
{
    public static readonly StyledProperty<bool> LoadingProperty =
        AvaloniaProperty.Register<ArcMorphBlock, bool>(nameof(Loading), true);

    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<ArcMorphBlock, Control?>(nameof(Content));

    /// <summary>Text skeleton lines; 0 draws a single block.</summary>
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<ArcMorphBlock, int>(nameof(Lines));

    /// <summary>Line box height, so the skeleton is exactly as tall as the loaded text.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ArcMorphBlock, double>(nameof(LineHeight), 20d);

    public static readonly StyledProperty<bool> CircleProperty =
        AvaloniaProperty.Register<ArcMorphBlock, bool>(nameof(Circle));

    private readonly ArcSkeletonShape _skeleton = new();
    private Control? _content;
    private readonly SvTween _tween = new();
    private double _progress;

    static ArcMorphBlock()
    {
        LoadingProperty.Changed.AddClassHandler<ArcMorphBlock>((block, _) => block.Sync(animate: true));
        ContentProperty.Changed.AddClassHandler<ArcMorphBlock>((block, _) => block.Rebuild());
        LinesProperty.Changed.AddClassHandler<ArcMorphBlock>((block, _) => block.Rebuild());
        LineHeightProperty.Changed.AddClassHandler<ArcMorphBlock>((block, _) => block.Rebuild());
        CircleProperty.Changed.AddClassHandler<ArcMorphBlock>((block, _) => block.Rebuild());
    }

    public ArcMorphBlock()
    {
        ClipToBounds = false;
        Rebuild();
    }

    public bool Loading
    {
        get => GetValue(LoadingProperty);
        set => SetValue(LoadingProperty, value);
    }

    /// <summary>The loaded content; not shown while loading.</summary>
    public Control? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public int Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public bool Circle
    {
        get => GetValue(CircleProperty);
        set => SetValue(CircleProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tween.Stop();
    }

    private void Rebuild()
    {
        Children.Clear();
        Sv.Detach(_skeleton);
        Sv.Detach(_content);

        _skeleton.Lines = Lines;
        _skeleton.LineHeight = LineHeight;
        _skeleton.Circle = Circle;
        _skeleton.Width = double.NaN;

        if (Content is not null)
        {
            _content = Content;
            _content.Opacity = 0;
            Children.Add(_content);
        }

        Children.Add(_skeleton);
        Sync(animate: true);
    }

    private void Sync(bool animate)
    {
        var target = Loading ? 0 : 1;
        if (!animate || ArcThemeManager.ReducedMotion)
        {
            _progress = target;
            Apply();
            return;
        }

        _tween.Run(_progress, target, 320, value =>
        {
            _progress = value;
            Apply();
        }, () =>
        {
            _progress = target;
            Apply();
        });
    }

    private void Apply()
    {
        if (_content is not null)
        {
            _content.Opacity = _progress;
            _content.RenderTransform = new ScaleTransform(0.985 + 0.015 * _progress, 0.985 + 0.015 * _progress);
        }

        _skeleton.Opacity = 1 - _progress;
        _skeleton.IsVisible = _progress < 0.999;
        _skeleton.InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The block takes the loaded content's size, so the skeleton occupies exactly the same box.
        if (_content is null)
        {
            return base.MeasureOverride(availableSize);
        }

        _content.Measure(availableSize);
        return _content.DesiredSize;
    }
}

/// <summary>The skeleton itself: bars centred in their line boxes, a single block, or a circle.</summary>
internal sealed class ArcSkeletonShape : Control
{
    private readonly SvClock _clock = new();
    private double _pulse = 1;

    public ArcSkeletonShape()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    public int Lines { get; set; }

    public double LineHeight { get; set; } = 20;

    public bool Circle { get; set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _clock.Start(seconds =>
        {
            _pulse = 0.62 + 0.32 * (0.5 + 0.5 * Math.Sin(seconds * 3.4));
            InvalidateVisual();
        }, 40);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock.Stop();
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var brush = Sv.AlphaBrush(ArcTokens.ControlTrack, _pulse);
        if (Circle)
        {
            var size = Math.Min(Bounds.Width, Bounds.Height);
            context.DrawEllipse(brush, null, new Point(size / 2, size / 2), size / 2, size / 2);
            return;
        }

        if (Lines <= 0)
        {
            SvDraw.Fill(context, new Rect(0, 0, Bounds.Width, Bounds.Height), 8, brush);
            return;
        }

        var count = Math.Clamp(Lines, 1, 6);
        var fractions = new[] { 1.0, 0.94, 0.82, 0.9, 0.72, 0.6 };
        for (var i = 0; i < count; i++)
        {
            var fraction = count == 1 ? 0.68 : fractions[Math.Min(i, fractions.Length - 1)];
            var height = Math.Min(12, LineHeight * 0.58);
            var y = i * LineHeight + (LineHeight - height) / 2;
            SvDraw.Fill(context, new Rect(0, y, Math.Max(6, Bounds.Width * fraction), height), height / 2, brush);
        }
    }
}

/// <summary>
/// A loading region whose skeleton blocks become the real content, block by block in reading order. It marks itself
/// busy while loading, and the blocks resolve one after another with a short stagger.
///
/// Platform approximations: blocks keep the loaded content's box, so the placeholder does not stretch its size; the
/// resolve is a fade and a 1.5 percent scale instead of a measured spring between two boxes, and there is no blur.
/// </summary>
public class ArcSkeletonMorph : Decorator
{
    public static readonly StyledProperty<bool> LoadingProperty =
        AvaloniaProperty.Register<ArcSkeletonMorph, bool>(nameof(Loading), true);

    /// <summary>Seconds between blocks as they resolve in reading order.</summary>
    public static readonly StyledProperty<double> StaggerProperty =
        AvaloniaProperty.Register<ArcSkeletonMorph, double>(nameof(Stagger), 0.04);

    public static readonly StyledProperty<string> LoadingLabelProperty =
        AvaloniaProperty.Register<ArcSkeletonMorph, string>(nameof(LoadingLabel), "Loading");

    private readonly List<ArcMorphBlock> _blocks = new();
    private List<DispatcherTimer> _timers = new();

    static ArcSkeletonMorph()
    {
        LoadingProperty.Changed.AddClassHandler<ArcSkeletonMorph>((region, _) => region.Apply());
        StaggerProperty.Changed.AddClassHandler<ArcSkeletonMorph>((region, _) => region.Apply());
        LoadingLabelProperty.Changed.AddClassHandler<ArcSkeletonMorph>((region, _) => region.UpdateAutomation());
    }

    public bool Loading
    {
        get => GetValue(LoadingProperty);
        set => SetValue(LoadingProperty, value);
    }

    public double Stagger
    {
        get => GetValue(StaggerProperty);
        set => SetValue(StaggerProperty, value);
    }

    public string LoadingLabel
    {
        get => GetValue(LoadingLabelProperty);
        set => SetValue(LoadingLabelProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Collect();
        Apply();
        UpdateAutomation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimers();
    }

    private void Collect()
    {
        _blocks.Clear();
        if (Child is null)
        {
            return;
        }

        Gather(Child);
    }

    private void Gather(Control control)
    {
        if (control is ArcMorphBlock block)
        {
            _blocks.Add(block);
            return;
        }

        if (control is ILogical logical)
        {
            foreach (var child in logical.LogicalChildren.OfType<Control>())
            {
                Gather(child);
            }
        }
    }

    private void Apply()
    {
        if (_blocks.Count == 0)
        {
            Collect();
        }

        StopTimers();
        AutomationProperties.SetHelpText(this, Loading ? LoadingLabel : "Loaded");
        AutomationProperties.SetLiveSetting(this, Loading ? AutomationLiveSetting.Off : AutomationLiveSetting.Polite);

        if (!Loading)
        {
            for (var i = 0; i < _blocks.Count; i++)
            {
                var block = _blocks[i];
                var delay = ArcThemeManager.ReducedMotion ? 0 : i * Math.Max(0, Stagger) * 1000;
                if (delay <= 1)
                {
                    block.Loading = false;
                    continue;
                }

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    block.Loading = false;
                };
                timer.Start();
                _timers.Add(timer);
            }

            return;
        }

        // Going back to loading is immediate, so a fast reload never leaves a block halfway.
        foreach (var block in _blocks)
        {
            block.Loading = true;
        }
    }

    private void StopTimers()
    {
        foreach (var timer in _timers)
        {
            timer.Stop();
        }

        _timers.Clear();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Loading ? LoadingLabel : "Content");
        AutomationProperties.SetHelpText(this, Loading ? LoadingLabel : "Loaded");
    }
}
