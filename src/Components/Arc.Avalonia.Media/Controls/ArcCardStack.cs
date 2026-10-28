using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Controls;

/// <summary>
/// Arc's card stack: one card at a time, thrown left or right by a drag or decided with the buttons or
/// arrow keys. The cards underneath rise as the deck shrinks, a decision can be undone, and the end of
/// the deck offers a reset.
/// </summary>
public class ArcCardStack : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcCardStackItem>?> CardsProperty =
        AvaloniaProperty.Register<ArcCardStack, IList<ArcCardStackItem>?>(nameof(Cards));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcCardStack, string>(nameof(Label), "Cards");

    public static readonly StyledProperty<string> LeftLabelProperty =
        AvaloniaProperty.Register<ArcCardStack, string>(nameof(LeftLabel), "Pass");

    public static readonly StyledProperty<string> RightLabelProperty =
        AvaloniaProperty.Register<ArcCardStack, string>(nameof(RightLabel), "Keep");

    public static readonly StyledProperty<string> LeftOutcomeProperty =
        AvaloniaProperty.Register<ArcCardStack, string>(nameof(LeftOutcome), "passed");

    public static readonly StyledProperty<string> RightOutcomeProperty =
        AvaloniaProperty.Register<ArcCardStack, string>(nameof(RightOutcome), "kept");

    public static readonly StyledProperty<double> CardWidthProperty =
        AvaloniaProperty.Register<ArcCardStack, double>(nameof(CardWidth), 420d);

    public static readonly DirectProperty<ArcCardStack, int> IndexProperty =
        AvaloniaProperty.RegisterDirect<ArcCardStack, int>(nameof(Index), stack => stack.Index);

    private readonly ArcMediaTween _tween = new();
    private readonly List<(ArcCardStackItem Item, ArcCardDecision Decision)> _decisions = new();
    private readonly List<CardView> _cards = new();
    private Grid? _layer;
    private Border? _stage;
    private StackPanel? _empty;
    private ArcButton? _undo;
    private ArcButton? _left;
    private ArcButton? _right;
    private ArcButton? _reset;
    private TextBlock? _status;
    private int _index;
    private bool _pointerDown;
    private bool _dragging;
    private Point _pressPoint;
    private double _dragX;
    private bool _animating;

    private sealed class CardView
    {
        public Border Card { get; init; } = null!;

        public TranslateTransform Translate { get; init; } = null!;

        public RotateTransform Rotate { get; init; } = null!;

        public ScaleTransform Scale { get; init; } = null!;

        public Border? LeftStamp { get; init; }

        public Border? RightStamp { get; init; }
    }

    static ArcCardStack()
    {
        CardsProperty.Changed.AddClassHandler<ArcCardStack>((stack, _) =>
        {
            stack.Index = Math.Clamp(stack.Index, 0, stack.Cards?.Count ?? 0);
            stack.Rebuild(animateIn: false);
        });
        LabelProperty.Changed.AddClassHandler<ArcCardStack>((stack, _) => stack.UpdateAutomation());
        LeftLabelProperty.Changed.AddClassHandler<ArcCardStack>((stack, _) => stack.UpdateButtons());
        RightLabelProperty.Changed.AddClassHandler<ArcCardStack>((stack, _) => stack.UpdateButtons());
        CardWidthProperty.Changed.AddClassHandler<ArcCardStack>((stack, _) => stack.ApplyWidth());
    }

    public ArcCardStack()
    {
        Focusable = true;
        IsTabStop = true;
    }

    /// <summary>Cards in order.</summary>
    public IList<ArcCardStackItem>? Cards
    {
        get => GetValue(CardsProperty);
        set => SetValue(CardsProperty, value);
    }

    /// <summary>Accessible name for the stack.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Name of the decision that throws a card left.</summary>
    public string LeftLabel
    {
        get => GetValue(LeftLabelProperty);
        set => SetValue(LeftLabelProperty, value);
    }

    /// <summary>Name of the decision that throws a card right.</summary>
    public string RightLabel
    {
        get => GetValue(RightLabelProperty);
        set => SetValue(RightLabelProperty, value);
    }

    /// <summary>Past tense used in announcements for the left decision.</summary>
    public string LeftOutcome
    {
        get => GetValue(LeftOutcomeProperty);
        set => SetValue(LeftOutcomeProperty, value);
    }

    /// <summary>Past tense used in announcements for the right decision.</summary>
    public string RightOutcome
    {
        get => GetValue(RightOutcomeProperty);
        set => SetValue(RightOutcomeProperty, value);
    }

    /// <summary>Width of the deck stage in px.</summary>
    public double CardWidth
    {
        get => GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }

    /// <summary>Index of the card on top; equal to the card count when the deck is finished.</summary>
    public int Index
    {
        get => _index;
        private set => SetAndRaise(IndexProperty, ref _index, value);
    }

    /// <summary>Raised when a card leaves the deck, by drag, button or key.</summary>
    public event EventHandler<ArcCardDecisionEventArgs>? CardDismissed;

    /// <summary>Raised when the last decision is undone.</summary>
    public event EventHandler<ArcCardDecisionEventArgs>? CardUndone;

    /// <summary>Raised when every card comes back.</summary>
    public event EventHandler? StackReset;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layer = e.NameScope.Find<Grid>("PART_CardLayer");
        _stage = e.NameScope.Find<Border>("Stage");
        _empty = e.NameScope.Find<StackPanel>("PART_Empty");
        _undo = e.NameScope.Find<ArcButton>("PART_Undo");
        _left = e.NameScope.Find<ArcButton>("PART_Left");
        _right = e.NameScope.Find<ArcButton>("PART_Right");
        _reset = e.NameScope.Find<ArcButton>("PART_Reset");
        _status = e.NameScope.Find<TextBlock>("Status");

        if (_undo is not null)
        {
            _undo.Content = "Undo";
            AutomationProperties.SetName(_undo, "Undo last decision");
            _undo.Click += (_, _) => Undo();
        }

        if (_left is not null)
        {
            _left.Click += (_, _) => Decide(ArcCardDecision.Left, animate: true);
        }

        if (_right is not null)
        {
            _right.Click += (_, _) => Decide(ArcCardDecision.Right, animate: true);
        }

        if (_reset is not null)
        {
            _reset.Content = "Start over";
            _reset.Click += (_, _) => Reset();
        }

        ApplyWidth();
        UpdateAutomation();
        Rebuild(animateIn: false);
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Cards" : Label);
        AutomationProperties.SetHelpText(this,
            "Drag the top card, or press Left or Right to decide. Backspace undoes the last decision.");
    }

    private void ApplyWidth()
    {
        if (_stage is not null)
        {
            _stage.MaxWidth = Math.Max(240, CardWidth);
        }
    }

    private void UpdateButtons()
    {
        if (_left is not null)
        {
            _left.Content = LeftLabel;
        }

        if (_right is not null)
        {
            _right.Content = RightLabel;
        }

        var hasCards = CurrentItem() is not null;
        if (_left is not null)
        {
            _left.IsEnabled = hasCards;
        }

        if (_right is not null)
        {
            _right.IsEnabled = hasCards;
        }

        if (_undo is not null)
        {
            _undo.IsEnabled = _decisions.Count > 0;
        }

        var item = CurrentItem();
        if (item is not null && _left is not null)
        {
            AutomationProperties.SetName(_left, $"{LeftLabel} {item.Label}");
        }

        if (item is not null && _right is not null)
        {
            AutomationProperties.SetName(_right, $"{RightLabel} {item.Label}");
        }
    }

    private ArcCardStackItem? CurrentItem()
    {
        if (Cards is null || _index < 0 || _index >= Cards.Count)
        {
            return null;
        }

        return Cards[_index];
    }

    private void Rebuild(bool animateIn, ArcCardDecision entrance = ArcCardDecision.None)
    {
        if (_layer is null)
        {
            return;
        }

        _layer.Children.Clear();
        _cards.Clear();
        var items = Cards ?? Array.Empty<ArcCardStackItem>();

        if (_index >= items.Count)
        {
            ShowEmpty();
            return;
        }

        if (_empty is not null)
        {
            _empty.IsVisible = false;
        }

        var visible = Math.Min(4, items.Count - _index);
        for (var offset = visible - 1; offset >= 0; offset--)
        {
            var item = items[_index + offset];
            _cards.Insert(0, BuildCard(item, offset));
        }

        // Add back-to-front so the top card paints over the ones it covers.
        for (var offset = _cards.Count - 1; offset >= 0; offset--)
        {
            _layer.Children.Add(_cards[offset].Card);
        }

        for (var offset = 0; offset < _cards.Count; offset++)
        {
            ApplyDepth(_cards[offset], offset);
        }

        UpdateButtons();
        AnnounceCurrent();

        if (animateIn && _cards.Count > 0)
        {
            var top = _cards[0];
            var startX = entrance switch
            {
                ArcCardDecision.Left => -160,
                ArcCardDecision.Right => 160,
                _ => 0
            };
            top.Translate.X = startX;
            top.Card.Opacity = 0;
            _tween.Run(0, 1, ArcMediaUi.Duration(ArcTokens.DurationStandard), value =>
            {
                top.Translate.X = startX * (1 - value);
                top.Card.Opacity = value;
            }, () =>
            {
                top.Translate.X = 0;
                top.Card.Opacity = 1;
            });
        }
    }

    private void ShowEmpty()
    {
        if (_empty is not null)
        {
            _empty.IsVisible = true;
        }

        UpdateButtons();
        if (_left is not null)
        {
            _left.IsEnabled = false;
        }

        if (_right is not null)
        {
            _right.IsEnabled = false;
        }

        if (_status is not null)
        {
            _status.Text = "Every card has been reviewed";
        }
    }

    private CardView BuildCard(ArcCardStackItem item, int depth)
    {
        var translate = new TranslateTransform();
        var rotate = new RotateTransform();
        var scale = new ScaleTransform(1, 1);
        var group = new TransformGroup { Children = { scale, rotate, translate } };

        var card = new Border
        {
            Background = ArcMediaUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcMediaUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcMediaUi.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(20),
            RenderTransform = group,
            RenderTransformOrigin = RelativePoint.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        card.Paint(Border.BackgroundProperty, ArcTokens.Surface);
        card.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        card.Bind(Border.CornerRadiusProperty, card.GetResourceObservable(ArcTokens.RadiusPanel));

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(new TextBlock
        {
            Text = item.Label,
            Classes = { "arc-caption", "arc-secondary" }
        });
        body.Children.Add(new TextBlock
        {
            Text = item.Title,
            Classes = { "arc-heading" },
            TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            body.Children.Add(new TextBlock
            {
                Text = item.Description,
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 3,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        if (item.Content is Control custom)
        {
            body.Children.Add(custom);
        }

        var leftStamp = BuildStamp(LeftLabel, HorizontalAlignment.Left, -12);
        var rightStamp = BuildStamp(RightLabel, HorizontalAlignment.Right, 12);
        var overlay = new Panel { IsHitTestVisible = false };
        overlay.Children.Add(leftStamp);
        overlay.Children.Add(rightStamp);

        card.Child = new Panel
        {
            Children = { body, overlay }
        };

        var count = Cards?.Count ?? 0;
        AutomationProperties.SetName(card, $"{item.Label}, {_index + depth + 1} of {count}");

        return new CardView
        {
            Card = card,
            Translate = translate,
            Rotate = rotate,
            Scale = scale,
            LeftStamp = leftStamp,
            RightStamp = rightStamp
        };
    }

    private static Border BuildStamp(string text, HorizontalAlignment alignment, double angle)
    {
        var stamp = new Border
        {
            Background = ArcMediaUi.Brush(ArcTokens.AccentSubtle),
            BorderBrush = ArcMediaUi.Brush(ArcTokens.Accent),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 4),
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
            Opacity = 0,
            RenderTransform = new RotateTransform(angle),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = text,
                FontSize = ArcResources.Double(ArcTokens.TextXs),
                FontWeight = FontWeight.Medium,
                Foreground = ArcMediaUi.Brush(ArcTokens.Accent)
            }
        };
        return stamp;
    }

    private void ApplyDepth(CardView view, int depth)
    {
        view.Translate.X = 0;
        view.Translate.Y = depth * 14;
        view.Rotate.Angle = 0;
        var scale = Math.Max(0.7, 1 - depth * 0.06);
        view.Scale.ScaleX = scale;
        view.Scale.ScaleY = scale;
        view.Card.Opacity = depth == 0 ? 1 : Math.Max(0.35, 1 - depth * 0.22);
        view.Card.ZIndex = 100 - depth;
        view.Card.IsHitTestVisible = depth == 0;
        if (view.LeftStamp is not null)
        {
            view.LeftStamp.Opacity = 0;
        }

        if (view.RightStamp is not null)
        {
            view.RightStamp.Opacity = 0;
        }
    }

    private void Decide(ArcCardDecision decision, bool animate)
    {
        var item = CurrentItem();
        if (item is null || _animating)
        {
            return;
        }

        if (!animate || _cards.Count == 0)
        {
            Commit(item, decision);
            return;
        }

        var top = _cards[0];
        var width = Math.Max(320, top.Card.Bounds.Width);
        var target = decision == ArcCardDecision.Left ? -width - 80 : width + 80;
        _animating = true;
        if (top.LeftStamp is not null)
        {
            top.LeftStamp.Opacity = decision == ArcCardDecision.Left ? 1 : 0;
        }

        if (top.RightStamp is not null)
        {
            top.RightStamp.Opacity = decision == ArcCardDecision.Right ? 1 : 0;
        }

        _tween.Run(top.Translate.X, target, ArcMediaUi.Duration(ArcTokens.DurationStandard), value =>
        {
            top.Translate.X = value;
            top.Rotate.Angle = Math.Clamp(value / 24, -14, 14);
            top.Card.Opacity = Math.Clamp(1 - Math.Abs(value) / (width * 1.6), 0, 1);
        }, () =>
        {
            _animating = false;
            Commit(item, decision);
        }, ArcMotion.EaseExit.Ease);
    }

    private void Commit(ArcCardStackItem item, ArcCardDecision decision)
    {
        _decisions.Add((item, decision));
        Index = Math.Min(Index + 1, Cards?.Count ?? Index + 1);
        if (_status is not null)
        {
            _status.Text = decision == ArcCardDecision.Left
                ? $"{item.Label} {LeftOutcome}"
                : $"{item.Label} {RightOutcome}";
        }

        Rebuild(animateIn: false);
        CardDismissed?.Invoke(this, new ArcCardDecisionEventArgs(item, decision));
    }

    /// <summary>Reverses the last decision and brings that card back.</summary>
    public void Undo()
    {
        if (_decisions.Count == 0)
        {
            return;
        }

        var last = _decisions[^1];
        if (_animating)
        {
            return;
        }

        _decisions.RemoveAt(_decisions.Count - 1);
        Index = Math.Max(0, Index - 1);
        if (_status is not null)
        {
            _status.Text = $"Undid {last.Item.Label}";
        }

        Rebuild(animateIn: true, entrance: last.Decision);
        CardUndone?.Invoke(this, new ArcCardDecisionEventArgs(last.Item, last.Decision));
    }

    /// <summary>Brings every card back and clears the decisions.</summary>
    public void Reset()
    {
        if (_decisions.Count == 0 && Index == 0)
        {
            return;
        }

        _decisions.Clear();
        Index = 0;
        if (_status is not null)
        {
            _status.Text = "Deck reset";
        }

        Rebuild(animateIn: true, entrance: ArcCardDecision.None);
        StackReset?.Invoke(this, EventArgs.Empty);
    }

    private void AnnounceCurrent()
    {
        if (_status is null)
        {
            return;
        }

        var item = CurrentItem();
        var count = Cards?.Count ?? 0;
        _status.Text = item is null
            ? "Every card has been reviewed"
            : $"Card {_index + 1} of {count}: {item.Label}";
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _animating || CurrentItem() is null)
        {
            return;
        }

        if (ArcMediaUi.InsideButton(e.Source) || _cards.Count == 0)
        {
            return;
        }

        Focus(NavigationMethod.Pointer);
        _pointerDown = true;
        _dragging = false;
        _pressPoint = e.GetPosition(this);
        _dragX = 0;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_pointerDown || e.Pointer.Captured != this || _cards.Count == 0)
        {
            return;
        }

        var point = e.GetPosition(this);
        var dx = point.X - _pressPoint.X;
        var dy = point.Y - _pressPoint.Y;
        if (!_dragging && Math.Abs(dx) < 6)
        {
            return;
        }

        if (!_dragging && Math.Abs(dy) > Math.Abs(dx))
        {
            _pointerDown = false;
            e.Pointer.Capture(null);
            return;
        }

        _dragging = true;
        _dragX = dx;
        var top = _cards[0];
        top.Translate.X = dx;
        top.Translate.Y = dy * 0.12;
        top.Rotate.Angle = Math.Clamp(dx / 24, -14, 14);
        var strength = Math.Clamp(Math.Abs(dx) / 110.0, 0, 1);
        if (top.LeftStamp is not null)
        {
            top.LeftStamp.Opacity = dx < 0 ? strength : 0;
        }

        if (top.RightStamp is not null)
        {
            top.RightStamp.Opacity = dx > 0 ? strength : 0;
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pointerDown)
        {
            return;
        }

        _pointerDown = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        if (!_dragging || _cards.Count == 0)
        {
            _dragging = false;
            return;
        }

        _dragging = false;
        var top = _cards[0];
        var threshold = Math.Max(72, top.Card.Bounds.Width * 0.28);
        if (Math.Abs(_dragX) > threshold)
        {
            Decide(_dragX < 0 ? ArcCardDecision.Left : ArcCardDecision.Right, animate: true);
        }
        else
        {
            _tween.Run(top.Translate.X, 0, ArcMediaUi.Duration(ArcTokens.DurationFast), value =>
            {
                top.Translate.X = value;
                top.Rotate.Angle = Math.Clamp(value / 24, -14, 14);
                if (top.LeftStamp is not null)
                {
                    top.LeftStamp.Opacity = Math.Clamp(-value / 110.0, 0, 1);
                }

                if (top.RightStamp is not null)
                {
                    top.RightStamp.Opacity = Math.Clamp(value / 110.0, 0, 1);
                }
            }, () =>
            {
                ApplyDepth(top, 0);
            });
        }

        e.Handled = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tween.Stop();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (ArcMediaUi.InsideButton(e.Source) && e.Key is Key.Left or Key.Right)
        {
            return;
        }
        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        if (e.Key == Key.Left)
        {
            Decide(ArcCardDecision.Left, animate: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            Decide(ArcCardDecision.Right, animate: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Back || (control && e.Key == Key.Z))
        {
            Undo();
            e.Handled = true;
        }
    }
}


