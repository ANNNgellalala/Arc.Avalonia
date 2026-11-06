using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>
/// A dense card that opens in place to reveal more of itself: one smooth spring moves the height and,
/// when a wider cap is set, the width, so text never stretches. The details fade in once the box has
/// made room and fade out before it shrinks. The whole header is the toggle, so Enter and Space work.
/// </summary>
[TemplatePart("PART_Frame", typeof(Border))]
[TemplatePart("PART_Header", typeof(ArcButton))]
[TemplatePart("PART_TitleBlock", typeof(TextBlock))]
[TemplatePart("PART_DescriptionBlock", typeof(TextBlock))]
[TemplatePart("PART_Chevron", typeof(ArcIcon))]
[TemplatePart("PART_PanelClip", typeof(Border))]
[TemplatePart("PART_Body", typeof(ContentPresenter))]
public class ArcExpandableCard : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArcExpandableCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcExpandableCard, string?>(nameof(Description));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ArcExpandableCard, bool>(nameof(IsExpanded));

    /// <summary>Collapsed width cap in px; NaN fills the container (Arc's `width`).</summary>
    public static readonly StyledProperty<double> CollapsedWidthProperty =
        AvaloniaProperty.Register<ArcExpandableCard, double>(nameof(CollapsedWidth), double.NaN);

    /// <summary>Expanded width cap in px (Arc's `expandedWidth`); NaN keeps the collapsed width.</summary>
    public static readonly StyledProperty<double> ExpandedWidthProperty =
        AvaloniaProperty.Register<ArcExpandableCard, double>(nameof(ExpandedWidth), double.NaN);

    private Border? _frame;
    private ArcButton? _header;
    private TextBlock? _title;
    private TextBlock? _description;
    private ArcIcon? _chevron;
    private Border? _panel;
    private ContentPresenter? _body;
    private ArcTween? _tween;
    private string? _lastDescription;
    private bool _wasExpanded;

    static ArcExpandableCard()
    {
        TitleProperty.Changed.AddClassHandler<ArcExpandableCard>((card, _) => card.UpdateCopy());
        DescriptionProperty.Changed.AddClassHandler<ArcExpandableCard>((card, _) => card.UpdateDescription());
        IsExpandedProperty.Changed.AddClassHandler<ArcExpandableCard>((card, _) => card.Apply());
        CollapsedWidthProperty.Changed.AddClassHandler<ArcExpandableCard>((card, _) => card.UpdateWidth(animate: false));
        ExpandedWidthProperty.Changed.AddClassHandler<ArcExpandableCard>((card, _) => card.UpdateWidth(animate: false));
    }

    public ArcExpandableCard()
    {
        Focusable = false;
    }

    public event EventHandler<bool>? ExpandedChanged;

    /// <summary>Bubbled copy of <see cref="ExpandedChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcExpandableCardExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<ArcExpandableCard, ArcExpandableCardExpandedChangedEventArgs>("ExpandedChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="ExpandedChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcExpandableCardExpandedChangedEventArgs>? ExpandedChangedRouted
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    private void RaiseExpandedChanged(bool isExpanded)
    {
        ExpandedChanged?.Invoke(this, isExpanded);
        RaiseEvent(new ArcExpandableCardExpandedChangedEventArgs(ExpandedChangedEvent) { IsExpanded = isExpanded });
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Summary line; changed text fades in rather than jumping.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>The details panel state. Arc keeps this internal; the port exposes it so a page can present it.</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public double CollapsedWidth
    {
        get => GetValue(CollapsedWidthProperty);
        set => SetValue(CollapsedWidthProperty, value);
    }

    public double ExpandedWidth
    {
        get => GetValue(ExpandedWidthProperty);
        set => SetValue(ExpandedWidthProperty, value);
    }

    public void Toggle() => SetCurrentValue(IsExpandedProperty, !IsExpanded);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _frame = e.NameScope.Find<Border>("PART_Frame");
        _header = e.NameScope.Find<ArcButton>("PART_Header");
        _title = e.NameScope.Find<TextBlock>("PART_TitleBlock");
        _description = e.NameScope.Find<TextBlock>("PART_DescriptionBlock");
        _chevron = e.NameScope.Find<ArcIcon>("PART_Chevron");
        _panel = e.NameScope.Find<Border>("PART_PanelClip");
        _body = e.NameScope.Find<ContentPresenter>("PART_Body");

        if (_chevron is not null)
        {
            _chevron.Foreground = ArcEffectVisuals.Brush(ArcTokens.TextMuted);
        }

        if (_header is not null)
        {
            _header.Click += (_, _) =>
            {
                SetCurrentValue(IsExpandedProperty, !IsExpanded);
                RaiseExpandedChanged(IsExpanded);
            };
        }

        UpdateCopy();
        UpdateWidth(animate: false);
        _wasExpanded = false;
        Apply(initial: true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && IsExpanded && IsFocusInside())
        {
            SetCurrentValue(IsExpandedProperty, false);
            _header?.Focus();
            e.Handled = true;
        }
    }

    private bool IsFocusInside()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focused is null)
        {
            return false;
        }

        for (Visual? current = focused; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateCopy()
    {
        if (_title is not null)
        {
            _title.Text = Title;
        }

        if (_header is not null)
        {
            AutomationProperties.SetName(_header, Title ?? "Details");
        }

        AutomationProperties.SetName(this, Title ?? "Expandable card");
        UpdateDescription();
        UpdateExpandedHelp();
    }

    private void UpdateDescription()
    {
        if (_description is null)
        {
            return;
        }

        var text = Description ?? string.Empty;
        _description.IsVisible = !string.IsNullOrWhiteSpace(text);
        var changed = _lastDescription is not null && _lastDescription != text;
        _lastDescription = text;
        _description.Text = text;

        if (!changed || ArcThemeManager.ReducedMotion)
        {
            _description.Opacity = 1;
            return;
        }

        // Changed copy rises into place instead of swapping abruptly.
        var move = new TranslateTransform(0, 6);
        _description.RenderTransform = move;
        _description.Opacity = 0;
        ArcTween.Run(ArcMotion.Standard, ArcMotion.EaseEnter, progress =>
        {
            _description.Opacity = progress;
            move.Y = 6 * (1 - progress);
        });
    }

    private void UpdateExpandedHelp()
    {
        if (_header is null)
        {
            return;
        }

        AutomationProperties.SetHelpText(
            _header,
            IsExpanded ? "Expanded. Press to collapse." : "Collapsed. Press to expand.");
        AutomationProperties.SetName(_header, Title ?? "Details");
    }

    private void UpdateWidth(bool animate)
    {
        if (_frame is null)
        {
            return;
        }

        var target = IsExpanded && !double.IsNaN(ExpandedWidth) ? ExpandedWidth : CollapsedWidth;
        if (double.IsNaN(target) || target <= 0)
        {
            _frame.ClearValue(WidthProperty);
            _frame.HorizontalAlignment = HorizontalAlignment.Stretch;
            return;
        }

        _frame.HorizontalAlignment = HorizontalAlignment.Left;
        if (!animate || ArcThemeManager.ReducedMotion)
        {
            _frame.Width = target;
            return;
        }

        var from = _frame.Width is double.NaN || _frame.Width <= 0 ? target : _frame.Width;
        ArcTween.Run(ArcMotion.SpringSmoothDuration, ArcMotion.SpringSmooth, progress =>
        {
            _frame.Width = Math.Max(1, from + (target - from) * progress);
        });
    }

    private void Apply(bool initial = false)
    {
        if (_panel is null || _frame is null)
        {
            return;
        }

        UpdateExpandedHelp();
        if (_chevron is not null)
        {
            ArcEffectVisuals.Rotate(_chevron, IsExpanded);
        }

        var expanded = IsExpanded;
        var animate = !initial && _wasExpanded != expanded && !ArcThemeManager.ReducedMotion;
        _wasExpanded = expanded;

        if (expanded)
        {
            _panel.IsVisible = true;
            var inner = Math.Max(1, _frame.Bounds.Width - 32);
            _panel.InvalidateMeasure();
            if (_body is not null)
            {
                _body.InvalidateMeasure();
                _body.Measure(new Size(inner, double.PositiveInfinity));
            }

            var target = Math.Max(0, _body?.DesiredSize.Height ?? 0);
            UpdateWidth(animate: animate);

            if (!animate)
            {
                _panel.Height = target;
                if (_body is not null)
                {
                    _body.Opacity = 1;
                }

                _panel.IsHitTestVisible = true;
                return;
            }

            var from = _panel.Height > 0 ? _panel.Height : 0;
            if (_body is not null)
            {
                _body.Opacity = 0;
            }

            _tween?.Stop();
            _tween = ArcTween.Run(ArcMotion.SpringSmoothDuration, ArcMotion.SpringSmooth, progress =>
            {
                _panel.Height = Math.Max(0, from + (target - from) * progress);
                if (_body is not null)
                {
                    // Details arrive only after the box has made room for them.
                    _body.Opacity = Math.Clamp((progress - 0.3) / 0.7, 0, 1);
                }
            }, () =>
            {
                _panel.Height = target;
                if (_body is not null)
                {
                    _body.Opacity = 1;
                }

                _panel.IsHitTestVisible = true;
            });

            return;
        }

        if (!animate)
        {
            _panel.Height = 0;
            _panel.IsVisible = false;
            _panel.IsHitTestVisible = false;
            if (_body is not null)
            {
                _body.Opacity = 0;
            }

            UpdateWidth(animate: false);
            return;
        }

        var current = _panel.Height;
        _panel.IsHitTestVisible = false;
        _tween?.Stop();
        _tween = ArcTween.Run(ArcMotion.SpringSmoothDuration, ArcEffectVisuals.Linear, progress =>
        {
            // Details leave first, then the box closes.
            if (_body is not null)
            {
                _body.Opacity = 1 - Math.Clamp(progress / 0.35, 0, 1);
            }

            var close = Math.Clamp((progress - 0.25) / 0.75, 0, 1);
            _panel.Height = Math.Max(0, current * (1 - ArcMotion.SpringSmooth.Ease(close)));
        }, () =>
        {
            _panel.Height = 0;
            _panel.IsVisible = false;
            if (_body is not null)
            {
                _body.Opacity = 0;
            }
        });

        UpdateWidth(animate: true);
    }
}
