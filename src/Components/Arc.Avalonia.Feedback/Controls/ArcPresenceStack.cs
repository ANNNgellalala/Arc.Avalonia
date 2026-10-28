using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

/// <summary>Presence of one person in an <see cref="ArcPresenceStack"/>.</summary>
public enum ArcPresenceStatus
{
    Available,
    Busy,
    Away,
    Typing,
    Idle
}

/// <summary>Avatar diameter for an <see cref="ArcPresenceStack"/>: 28, 36 or 44 px.</summary>
public enum ArcPresenceSize
{
    Sm,
    Md,
    Lg
}

/// <summary>
/// One person in an <see cref="ArcPresenceStack"/>. <see cref="Id"/> is optional but should stay
/// stable while the person is present, so a join or leave keeps everybody else in place.
/// </summary>
public sealed class PresenceMember
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Photo path or avares URI; initials are shown when it cannot be read.</summary>
    public string? Image { get; set; }

    public ArcPresenceStatus Status { get; set; } = ArcPresenceStatus.Available;

    /// <summary>Shows the typing pill and pulse; also implied by <see cref="ArcPresenceStatus.Typing"/>.</summary>
    public bool IsTyping { get; set; }
}

/// <summary>
/// A live presence strip: overlapping faces of the people here now, a rolling +N counter past
/// <see cref="Max"/>, status dots for available, busy, away, typing and idle, a spring reflow when
/// someone joins or leaves, and a fanned-out hover state with a gliding name label.
/// </summary>
/// <remarks>
/// Independent Avalonia implementation built from the public presence-stack documentation
/// (uiarc.dev/components/presence-stack); no Pro source was used. The portable version keeps the
/// faces, overflow counter, status semantics, fan, gliding label, focus and reduced-motion branches.
/// The public component opens a full roster popover on Enter and announces joins through a live
/// region; those are host-application concerns here, so the accessible name and help text carry the
/// roster instead. <see cref="Fanned"/> exposes the hover state for an always-visible header or a
/// still image, where a real pointer hover cannot be simulated.
/// </remarks>
public class ArcPresenceStack : TemplatedControl
{
    public static readonly StyledProperty<IList<PresenceMember>?> MembersProperty =
        AvaloniaProperty.Register<ArcPresenceStack, IList<PresenceMember>?>(nameof(Members));

    public static readonly StyledProperty<int> MaxProperty =
        AvaloniaProperty.Register<ArcPresenceStack, int>(nameof(Max), 4);

    public static readonly StyledProperty<ArcPresenceSize> SizeProperty =
        AvaloniaProperty.Register<ArcPresenceStack, ArcPresenceSize>(nameof(Size));

    public static readonly StyledProperty<bool> ShowNamesProperty =
        AvaloniaProperty.Register<ArcPresenceStack, bool>(nameof(ShowNames), true);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcPresenceStack, string>(nameof(Label), "People here now");

    public static readonly StyledProperty<bool> FannedProperty =
        AvaloniaProperty.Register<ArcPresenceStack, bool>(nameof(Fanned));

    public static readonly StyledProperty<bool> ShowOthersProperty =
        AvaloniaProperty.Register<ArcPresenceStack, bool>(nameof(ShowOthers));

    private sealed class Face
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public Control Root = null!;
        public Control Dot = null!;
        public TranslateTransform Translate = null!;
        public TranslateTransform DotTranslate = null!;
        public double DotXOffset;
        public double DotYOffset;
        public ScaleTransform Scale = null!;
        public TranslateTransform?[] TypingDots = Array.Empty<TranslateTransform?>();
        public Border? TypingPill;
        public int Index;
        public double LayoutX;
    }

    private readonly List<Face> _faces = new();
    private readonly Dictionary<string, ImageBrush> _images = new(StringComparer.Ordinal);
    private Panel? _stage;
    private Canvas? _facesCanvas;
    private Canvas? _dotsCanvas;
    private Canvas? _labelsCanvas;
    private bool _overflowShown;
    private TextBlock? _others;
    private Border? _label;
    private TextBlock? _labelText;
    private TranslateTransform? _labelTranslate;
    private Border? _overflowChip;
    private TextBlock? _overflowText;
    private TranslateTransform? _overflowTranslate;
    private DispatcherTimer? _typingTimer;
    private double _typingPhase;
    private double _displayOverflow = -1;
    private int _activeIndex;
    private int _hoveredIndex = -1;
    private bool _hovered;
    private bool _keyboardFocus;
    private bool _attached;
    private double _restWidth;

    static ArcPresenceStack()
    {
        MembersProperty.Changed.AddClassHandler<ArcPresenceStack>((stack, _) => stack.Rebuild());
        MaxProperty.Changed.AddClassHandler<ArcPresenceStack>((stack, _) => stack.Rebuild());
        SizeProperty.Changed.AddClassHandler<ArcPresenceStack>((stack, _) => stack.Rebuild());
        ShowNamesProperty.Changed.AddClassHandler<ArcPresenceStack>((stack, _) => stack.Rebuild());
        FannedProperty.Changed.AddClassHandler<ArcPresenceStack>((stack, _) =>
        {
            stack.ApplyFan(animate: true);
            if (stack.Fanned)
            {
                stack.ShowLabel(stack._activeIndex);
            }
            else
            {
                stack.HideLabel();
            }
        });
        ShowOthersProperty.Changed.AddClassHandler<ArcPresenceStack>((stack, _) => stack.UpdateOthers());
        LabelProperty.Changed.AddClassHandler<ArcPresenceStack>((stack, _) => stack.UpdateAutomation());
    }

    public ArcPresenceStack()
    {
        Focusable = true;
        ClipToBounds = false;
    }

    /// <summary>
    /// Everyone here now, in display order. Reassign the list, or call <see cref="Refresh"/>, after a
    /// join, leave, reorder or status change so the stack animates to match.
    /// </summary>
    public IList<PresenceMember>? Members
    {
        get => GetValue(MembersProperty);
        set => SetValue(MembersProperty, value);
    }

    /// <summary>Faces shown before the rest collapse into the +N counter.</summary>
    public int Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    /// <summary>Avatar diameter: 28, 36 or 44 px; overlap, dots and label scale with it.</summary>
    public ArcPresenceSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Reveals the gliding name label on hover and keyboard focus.</summary>
    public bool ShowNames
    {
        get => GetValue(ShowNamesProperty);
        set => SetValue(ShowNamesProperty, value);
    }

    /// <summary>Accessible name for the strip; the count is appended.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// Keeps the stack fanned and the name label visible, as hover would. Use it for a pinned header
    /// that is always showing names, or a still. Hover and keyboard focus still update the label.
    /// </summary>
    public bool Fanned
    {
        get => GetValue(FannedProperty);
        set => SetValue(FannedProperty, value);
    }

    /// <summary>Adds an "and 3 others" caption after the rest width of the stack.</summary>
    public bool ShowOthers
    {
        get => GetValue(ShowOthersProperty);
        set => SetValue(ShowOthersProperty, value);
    }

    /// <summary>Re-reads <see cref="Members"/> after the list was mutated in place.</summary>
    public void Refresh() => Rebuild();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stage = e.NameScope.Find<Panel>("PART_Stage");
        _facesCanvas = e.NameScope.Find<Canvas>("PART_Faces");
        _dotsCanvas = e.NameScope.Find<Canvas>("PART_Dots");
        _labelsCanvas = e.NameScope.Find<Canvas>("PART_Labels");
        _others = e.NameScope.Find<TextBlock>("PART_Others");
        _labelTranslate = new TranslateTransform();
        _labelText = FeedbackUi.Text(string.Empty);
        _labelText.FontSize = ArcResources.Double(ArcTokens.TextXs);
        _labelText.FontWeight = FontWeight.Medium;
        _labelText.TextTrimming = TextTrimming.CharacterEllipsis;
        _labelText.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        _label = new Border
        {
            Padding = new Thickness(9, 3, 9, 3),
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusPill),
            BorderThickness = new Thickness(1),
            IsVisible = false,
            IsHitTestVisible = false,
            Opacity = 0,
            Child = _labelText,
            RenderTransform = _labelTranslate,
            ZIndex = 20
        };
        _label.Paint(Border.BackgroundProperty, ArcTokens.SurfaceRaised);
        _label.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        _label.BoxShadow = ArcResources.Shadows(ArcTokens.ShadowResting);
        _labelsCanvas?.Children.Add(_label);
        UpdateAutomation();
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        UpdateTypingTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        UpdateTypingTimer();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = true;
        ApplyFan(animate: true);
        ShowLabel(_activeIndex);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        _hoveredIndex = -1;
        ApplyFan(animate: true);
        HideLabel();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _keyboardFocus = true;
        ApplyFan(animate: true);
        ShowLabel(_activeIndex);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _keyboardFocus = false;
        ApplyFan(animate: true);
        HideLabel();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsFocused)
        {
            Focus();
        }

        _keyboardFocus = false;
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_faces.Count == 0)
        {
            return;
        }

        _keyboardFocus = true;
        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                _activeIndex = Math.Max(0, _activeIndex - 1);
                break;
            case Key.Right:
            case Key.Down:
                _activeIndex = Math.Min(_faces.Count - 1, _activeIndex + 1);
                break;
            case Key.Home:
                _activeIndex = 0;
                break;
            case Key.End:
                _activeIndex = _faces.Count - 1;
                break;
            default:
                return;
        }

        ApplyFan(animate: true);
        ShowLabel(_activeIndex);
        e.Handled = true;
    }

    private double Diameter => Size switch
    {
        ArcPresenceSize.Sm => 28,
        ArcPresenceSize.Md => 36,
        _ => 44
    };

    private bool Engaged => _hovered || Fanned || (IsFocused && _keyboardFocus);

    private static string MemberId(PresenceMember member, int index) =>
        string.IsNullOrWhiteSpace(member.Id)
            ? (string.IsNullOrWhiteSpace(member.Name) ? $"person-{index}" : member.Name)
            : member.Id;

    private void Rebuild()
    {
        if (_facesCanvas is null || _stage is null)
        {
            return;
        }

        var members = Members is null ? new List<PresenceMember>() : Members.ToList();
        var diameter = Diameter;
        var max = Math.Max(1, Max);
        var shown = Math.Min(members.Count, max);
        var overflow = Math.Max(0, members.Count - shown);
        var restStep = Math.Max(1, Math.Round(diameter * 0.32));
        var slots = shown + (overflow > 0 ? 1 : 0);
        _restWidth = slots == 0 ? 0 : (slots - 1) * restStep + diameter;

        var oldFaces = _faces.ToList();
        var oldX = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var face in oldFaces)
        {
            oldX[face.Id] = face.Translate.X;
        }

        var keep = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < shown; i++)
        {
            keep.Add(MemberId(members[i], i));
        }

        _faces.Clear();
        foreach (var face in oldFaces)
        {
            if (keep.Contains(face.Id))
            {
                _facesCanvas.Children.Remove(face.Root);
                _dotsCanvas?.Children.Remove(face.Dot);
                continue;
            }

            // Someone left: shrink and fade their face while the neighbours close the gap.
            var ghost = face;
            var scale = ghost.Scale;
            var root = ghost.Root;
            var dot = ghost.Dot;
            new ArcTween().Run(1, 0, TimeSpan.FromMilliseconds(160), value =>
            {
                root.Opacity = value;
                dot.Opacity = value;
            }, () =>
            {
                _facesCanvas.Children.Remove(root);
                _dotsCanvas?.Children.Remove(dot);
            }, ArcMotion.EaseExit.Ease);
            new ArcTween().Run(scale.ScaleX, 0.72, TimeSpan.FromMilliseconds(160), value =>
            {
                scale.ScaleX = value;
                scale.ScaleY = value;
            });
        }

        if (_overflowChip is not null)
        {
            _dotsCanvas?.Children.Remove(_overflowChip);
        }

        _overflowShown = false;

        var engaged = Engaged;
        var layout = new List<double>();
        for (var i = 0; i < shown; i++)
        {
            layout.Add(i * restStep);
        }

        for (var i = 0; i < shown; i++)
        {
            var member = members[i];
            var id = MemberId(member, i);
            var face = BuildFace(member, i, diameter);
            var faceIndex = _faces.Count;
            face.Index = faceIndex;
            face.LayoutX = layout[i];
            var target = layout[i] + FanOffset(faceIndex, engaged, diameter, restStep);
            var existed = oldX.TryGetValue(id, out var previous);
            face.Translate.X = existed ? previous : target;
            face.Translate.Y = 0;
            face.DotTranslate.X = face.Translate.X + face.DotXOffset;
            face.DotTranslate.Y = face.DotYOffset;
            _facesCanvas.Children.Add(face.Root);
            _dotsCanvas?.Children.Add(face.Dot);
            _faces.Add(face);

            if (!existed)
            {
                face.Root.Opacity = 0;
                face.Dot.Opacity = 0;
                face.Scale.ScaleX = 0.4;
                face.Scale.ScaleY = 0.4;
                new ArcTween().Run(0, 1, TimeSpan.FromMilliseconds(180), value =>
                {
                    face.Root.Opacity = value;
                    face.Dot.Opacity = value;
                });
                new ArcTween().Run(0.4, 1, ArcMotion.SpringSnappyDuration, value =>
                {
                    face.Scale.ScaleX = value;
                    face.Scale.ScaleY = value;
                }, ease: ArcMotion.SpringSnappy.Ease);
            }
            else if (Math.Abs(face.Translate.X - target) > 0.4)
            {
                AnimateTranslate(face, target);
            }
        }

        BuildOverflow(overflow, shown, diameter, restStep, engaged);

        _stage.Width = _restWidth;
        _stage.Height = diameter;
        var fannedWidth = FannedWidth(shown + (overflow > 0 ? 1 : 0), diameter);
        _facesCanvas.Width = Math.Max(_restWidth, fannedWidth);
        _facesCanvas.Height = diameter;
        if (_dotsCanvas is not null)
        {
            _dotsCanvas.Width = _facesCanvas.Width;
            _dotsCanvas.Height = diameter;
        }

        if (_labelsCanvas is not null)
        {
            _labelsCanvas.Width = _facesCanvas.Width;
            _labelsCanvas.Height = diameter + 30;
        }

        _activeIndex = _faces.Count == 0 ? 0 : Math.Clamp(_activeIndex, 0, _faces.Count - 1);
        UpdateOthers();
        UpdateAutomation();
        UpdateTypingTimer();

        if (ShowNames && (Engaged || Fanned) && _faces.Count > 0)
        {
            ShowLabel(_activeIndex);
        }
        else
        {
            HideLabel(clear: true);
        }
    }

    private void BuildOverflow(int overflow, int shown, double diameter, double restStep, bool engaged)
    {
        if (_dotsCanvas is null)
        {
            return;
        }

        if (overflow <= 0)
        {
            if (_overflowChip is not null)
            {
                _dotsCanvas.Children.Remove(_overflowChip);
            }

            _overflowShown = false;
            _displayOverflow = 0;
            return;
        }

        if (_overflowChip is null)
        {
            _overflowTranslate = new TranslateTransform();
            _overflowText = FeedbackUi.Numeric("+0");
            _overflowText.FontWeight = FontWeight.Medium;
            _overflowText.HorizontalAlignment = HorizontalAlignment.Center;
            _overflowText.VerticalAlignment = VerticalAlignment.Center;
            _overflowText.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
            _overflowChip = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(diameter / 2),
                Child = _overflowText,
                RenderTransform = _overflowTranslate,
                ZIndex = 100
            };
            _overflowChip.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            _overflowChip.Paint(Border.BorderBrushProperty, ArcTokens.Border);
            _overflowChip.PointerEntered += (_, _) =>
            {
                _hovered = true;
                _hoveredIndex = -1;
                ApplyFan(animate: true);
                HideLabel();
            };
        }

        _overflowChip.Width = diameter;
        _overflowChip.Height = diameter;
        _overflowText!.FontSize = Math.Clamp(diameter * 0.3, 11, 15);
        var index = shown;
        var target = index * restStep + FanOffset(index, engaged, diameter, restStep);
        _overflowTranslate!.X = target;
        _overflowTranslate.Y = 0;
        Canvas.SetLeft(_overflowChip, 0);
        Canvas.SetTop(_overflowChip, 0);
        _dotsCanvas.Children.Add(_overflowChip);
        _overflowShown = true;
        AutomationProperties.SetName(_overflowChip, $"{overflow} more {(overflow == 1 ? "person" : "people")}");
        ToolTip.SetTip(_overflowChip, $"{overflow} more {(overflow == 1 ? "person" : "people")}");

        var previous = _displayOverflow < 0 ? overflow : _displayOverflow;
        _displayOverflow = overflow;
        if (Math.Abs(previous - overflow) < 0.5)
        {
            _overflowText.Text = $"+{overflow}";
            _overflowText.Opacity = 1;
            return;
        }

        var goingUp = overflow > previous;
        var from = previous;
        var to = overflow;
        _overflowText.Opacity = 1;
        _overflowTranslate.Y = goingUp ? 8 : -8;
        new ArcTween().Run(0, 1, TimeSpan.FromMilliseconds(180), value =>
        {
            _overflowText.Text = $"+{Math.Round(from + (to - from) * value, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)}";
            _overflowTranslate.Y = (goingUp ? 8 : -8) * (1 - value);
        }, ease: ArcMotion.EaseStandard.Ease);
    }

    private Face BuildFace(PresenceMember member, int index, double diameter)
    {
        var translate = new TranslateTransform();
        var scale = new ScaleTransform(1, 1);
        var transform = new TransformGroup { Children = { scale, translate } };
        var panel = new Panel
        {
            Width = diameter,
            Height = diameter,
            RenderTransform = transform,
            RenderTransformOrigin = RelativePoint.Center,
            ClipToBounds = false
        };
        var faceIndex = index;
        panel.PointerEntered += (_, _) => OnFaceEntered(faceIndex);
        panel.PointerExited += (_, _) => OnFaceExited(faceIndex);

        var avatar = new Border
        {
            Width = diameter,
            Height = diameter,
            CornerRadius = new CornerRadius(diameter / 2),
            BorderThickness = new Thickness(2),
            ClipToBounds = true
        };
        avatar.Paint(Border.BorderBrushProperty, ArcTokens.Surface);

        var image = ResolveImage(member.Image);
        if (image is not null)
        {
            avatar.Background = image;
        }
        else
        {
            avatar.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            var initials = FeedbackUi.Text(FeedbackUi.Initials(member.Name));
            initials.FontSize = Math.Clamp(diameter * 0.36, 10, 16);
            initials.FontWeight = FontWeight.Medium;
            initials.HorizontalAlignment = HorizontalAlignment.Center;
            initials.VerticalAlignment = VerticalAlignment.Center;
            initials.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
            avatar.Child = initials;
        }

        if ((member.IsTyping || member.Status == ArcPresenceStatus.Typing))
        {
            avatar.Opacity = 1;
        }
        else if (member.Status == ArcPresenceStatus.Idle)
        {
            avatar.Opacity = 0.68;
        }

        panel.Children.Add(avatar);

        var typing = member.IsTyping || member.Status == ArcPresenceStatus.Typing;
        var dotSize = Math.Round(Math.Clamp(diameter * 0.34, 10, 16));
        var dotTranslate = new TranslateTransform();
        if (typing)
        {
            var pillWidth = Math.Round(Math.Max(dotSize, dotSize * 1.85));
            var dots = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 1.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var dotTransforms = new TranslateTransform?[3];
            for (var i = 0; i < 3; i++)
            {
                var dotTransform = new TranslateTransform();
                dotTransforms[i] = dotTransform;
                var mini = new Ellipse
                {
                    Width = 1.8,
                    Height = 1.8,
                    RenderTransform = dotTransform
                };
                mini.Paint(Shape.FillProperty, ArcTokens.AccentForeground);
                dots.Children.Add(mini);
            }

            var pill = new Border
            {
                Width = pillWidth,
                Height = dotSize,
                CornerRadius = new CornerRadius(dotSize / 2),
                BorderThickness = new Thickness(2),
                Child = dots,
                RenderTransform = dotTranslate,
                IsHitTestVisible = false,
                ZIndex = 3
            };
            pill.Paint(Border.BackgroundProperty, ArcTokens.Accent);
            pill.Paint(Border.BorderBrushProperty, ArcTokens.Surface);
            Canvas.SetLeft(pill, 0);
            Canvas.SetTop(pill, 0);
            return new Face
            {
                Id = MemberId(member, index),
                Name = member.Name,
                Root = panel,
                Dot = pill,
                DotTranslate = dotTranslate,
                DotXOffset = diameter - pillWidth - 1,
                DotYOffset = diameter - dotSize - 1,
                Translate = translate,
                Scale = scale,
                TypingPill = pill,
                TypingDots = dotTransforms,
                Index = index
            };
        }

        var dot = new Ellipse
        {
            Width = dotSize,
            Height = dotSize,
            StrokeThickness = 2,
            RenderTransform = dotTranslate,
            IsHitTestVisible = false,
            ZIndex = 3
        };
        dot.Paint(Shape.FillProperty, member.Status switch
        {
            ArcPresenceStatus.Busy => ArcTokens.Danger,
            ArcPresenceStatus.Away => ArcTokens.Warning,
            ArcPresenceStatus.Idle => ArcTokens.TextMuted,
            _ => ArcTokens.Success
        });
        dot.Paint(Shape.StrokeProperty, ArcTokens.Surface);
        Canvas.SetLeft(dot, 0);
        Canvas.SetTop(dot, 0);

        return new Face
        {
            Id = MemberId(member, index),
            Name = member.Name,
            Root = panel,
            Dot = dot,
            DotTranslate = dotTranslate,
            DotXOffset = diameter - dotSize - 1,
            DotYOffset = diameter - dotSize - 1,
            Translate = translate,
            Scale = scale,
            Index = index
        };
    }

    private double FannedWidth(int slots, double diameter)
    {
        if (slots <= 0)
        {
            return 0;
        }

        return diameter + (slots - 1) * (diameter + 4);
    }

    private double FanOffset(int index, bool engaged, double diameter, double restStep)
    {
        if (!engaged || index <= 0)
        {
            return 0;
        }

        var fannedStep = diameter + 4;
        return index * (fannedStep - restStep);
    }

    private void OnFaceEntered(int index)
    {
        _hovered = true;
        _hoveredIndex = index;
        ApplyFan(animate: true);
        ShowLabel(index);
    }

    private void OnFaceExited(int index)
    {
        if (_hoveredIndex == index)
        {
            _hoveredIndex = -1;
            ApplyFan(animate: true);
        }
    }

    private void ApplyFan(bool animate)
    {
        if (_facesCanvas is null || _faces.Count == 0)
        {
            return;
        }

        var diameter = Diameter;
        var shown = _faces.Count;
        var slots = shown + (_overflowShown ? 1 : 0);
        var restStep = Math.Max(1, Math.Round(diameter * 0.32));
        var engaged = Engaged;
        var fannedWidth = FannedWidth(slots, diameter);

        for (var i = 0; i < _faces.Count; i++)
        {
            var face = _faces[i];
            var target = face.LayoutX + FanOffset(i, engaged, diameter, restStep);
            var lift = engaged && i == _hoveredIndex ? -2 : 0;
            if (!animate)
            {
                face.Translate.X = target;
                face.Translate.Y = lift;
                face.DotTranslate.X = target + face.DotXOffset;
                face.DotTranslate.Y = face.DotYOffset + lift;
                continue;
            }

            AnimateTranslate(face, target);
            if (Math.Abs(face.DotTranslate.Y - (face.DotYOffset + lift)) > 0.1)
            {
                new ArcTween().Run(face.DotTranslate.Y, face.DotYOffset + lift, ArcMotion.SpringSnappyDuration, value =>
                {
                    face.Translate.Y = value - face.DotYOffset;
                    face.DotTranslate.Y = value;
                }, ease: ArcMotion.SpringSnappy.Ease);
            }
        }

        if (_overflowTranslate is not null && _overflowShown)
        {
            var index = shown;
            var target = index * restStep + FanOffset(index, engaged, diameter, restStep);
            if (!animate)
            {
                _overflowTranslate.X = target;
            }
            else if (Math.Abs(_overflowTranslate.X - target) > 0.4)
            {
                new ArcTween().Run(_overflowTranslate.X, target, ArcMotion.SpringSmoothDuration,
                    value => _overflowTranslate.X = value, ease: ArcMotion.SpringSmooth.Ease);
            }
        }

        if (_facesCanvas is not null)
        {
            _facesCanvas.Width = Math.Max(_restWidth, fannedWidth);
            if (_dotsCanvas is not null)
            {
                _dotsCanvas.Width = _facesCanvas.Width;
            }

            if (_labelsCanvas is not null)
            {
                _labelsCanvas.Width = _facesCanvas.Width;
            }
        }

        UpdateOthers();
    }

    private static void AnimateTranslate(Face face, double target)
    {
        var from = face.Translate.X;
        var dotFrom = face.DotTranslate.X;
        new ArcTween().Run(from, target, ArcMotion.SpringSmoothDuration, value =>
        {
            face.Translate.X = value;
            face.DotTranslate.X = dotFrom + (value - from);
        }, ease: ArcMotion.SpringSmooth.Ease);
    }

    private void ShowLabel(int index)
    {
        if (!ShowNames || _label is null || _labelText is null || _labelTranslate is null || _labelsCanvas is null)
        {
            return;
        }

        if (index < 0 || index >= _faces.Count)
        {
            HideLabel();
            return;
        }

        _activeIndex = index;
        var face = _faces[index];
        var name = string.IsNullOrWhiteSpace(face.Name) ? "Someone" : face.Name;
        _labelText.Text = name;
        _labelText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var targetWidth = Math.Ceiling(_labelText.DesiredSize.Width) + 24;
        targetWidth = Math.Clamp(targetWidth, 48, 240);
        var restStep = Math.Max(1, Math.Round(Diameter * 0.32));
        var faceX = face.LayoutX + FanOffset(index, Engaged, Diameter, restStep);
        var targetX = faceX + Diameter / 2 - targetWidth / 2;
        var maxX = Math.Max(0, _labelsCanvas.Width - targetWidth);
        targetX = Math.Clamp(targetX, 0, maxX);
        var targetY = Diameter + 4;
        var firstShow = !_label.IsVisible;

        Canvas.SetLeft(_label, 0);
        Canvas.SetTop(_label, 0);
        _label.IsVisible = true;
        if (firstShow)
        {
            // The label takes its first width at once so it never grows from nothing.
            _label.Width = targetWidth;
            _labelTranslate.X = targetX;
            _labelTranslate.Y = targetY;
            _label.Opacity = 0;
            new ArcTween().Run(0, 1, TimeSpan.FromMilliseconds(120), value => _label.Opacity = value);
            return;
        }

        _label.Opacity = 1;
        new ArcTween().Run(_label.Width, targetWidth, ArcMotion.SpringMorphDuration, value => _label.Width = value,
            ease: ArcMotion.SpringMorph.Ease);
        new ArcTween().Run(_labelTranslate.X, targetX, ArcMotion.SpringMorphDuration, value => _labelTranslate.X = value,
            ease: ArcMotion.SpringMorph.Ease);
        _labelTranslate.Y = targetY;
    }

    private void HideLabel(bool clear = false)
    {
        if (_label is null || !_label.IsVisible)
        {
            return;
        }

        if (clear)
        {
            _label.IsVisible = false;
            _label.Opacity = 0;
            return;
        }

        if (_hovered || Fanned || (IsFocused && _keyboardFocus))
        {
            return;
        }

        var label = _label;
        new ArcTween().Run(label.Opacity, 0, TimeSpan.FromMilliseconds(120), value => label.Opacity = value, () =>
        {
            if (!ShowNames || (!_hovered && !Fanned && !(IsFocused && _keyboardFocus)))
            {
                label.IsVisible = false;
            }
        }, ArcMotion.EaseExit.Ease);
    }

    private void UpdateOthers()
    {
        if (_others is null)
        {
            return;
        }

        var count = Members?.Count ?? 0;
        var shown = Math.Min(count, Math.Max(1, Max));
        var overflow = Math.Max(0, count - shown);
        _others.Text = overflow > 0 ? $"and {overflow} {(overflow == 1 ? "other" : "others")}" : string.Empty;
        _others.IsVisible = ShowOthers && overflow > 0;
        _others.Opacity = ShowOthers && overflow > 0 && !Engaged ? 1 : 0;
        _others.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        _others.Margin = new Thickness(0, Math.Max(0, (Diameter - 16) / 2), 0, 0);
    }

    private void UpdateAutomation()
    {
        var count = Members?.Count ?? 0;
        var people = count == 1 ? "person" : "people";
        AutomationProperties.SetName(this, $"{Label}, {count} {people}");
        var summary = Members is null
            ? string.Empty
            : string.Join(", ", Members.Select(member => $"{member.Name} {StatusWord(member)}"));
        AutomationProperties.SetHelpText(this, summary);
    }

    private static string StatusWord(PresenceMember member)
    {
        if (member.IsTyping || member.Status == ArcPresenceStatus.Typing)
        {
            return "typing";
        }

        return member.Status switch
        {
            ArcPresenceStatus.Busy => "busy",
            ArcPresenceStatus.Away => "away",
            ArcPresenceStatus.Idle => "idle",
            _ => "available"
        };
    }

    private ImageBrush? ResolveImage(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (_images.TryGetValue(source, out var cached))
        {
            return cached;
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

            if (bitmap is null)
            {
                return null;
            }

            var brush = new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            _images[source] = brush;
            return brush;
        }
        catch
        {
            return null;
        }
    }

    private void UpdateTypingTimer()
    {
        var typing = Members is not null && Members.Any(member =>
            member.IsTyping || member.Status == ArcPresenceStatus.Typing);
        var shouldRun = _attached && typing && !ArcThemeManager.ReducedMotion;

        if (!shouldRun)
        {
            _typingTimer?.Stop();
            _typingTimer = null;
            ApplyTypingFrame(0, animate: false);
            return;
        }

        if (_typingTimer is not null)
        {
            return;
        }

        _typingTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            _typingPhase += 0.05;
            ApplyTypingFrame(_typingPhase, animate: true);
        });
        _typingTimer.Start();
    }

    private void ApplyTypingFrame(double phase, bool animate)
    {
        foreach (var face in _faces)
        {
            if (face.TypingPill is null)
            {
                continue;
            }

            var pulse = animate ? 0.78 + 0.22 * (0.5 + 0.5 * Math.Sin(phase * 2.4)) : 1;
            face.TypingPill.Opacity = pulse;
            for (var i = 0; i < face.TypingDots.Length; i++)
            {
                var transform = face.TypingDots[i];
                if (transform is null)
                {
                    continue;
                }

                transform.Y = animate
                    ? -1.6 * Math.Max(0, Math.Sin(phase * 2.4 - i * 0.55))
                    : 0;
            }
        }
    }
}
