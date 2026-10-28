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
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

/// <summary>One person in an <see cref="ArcAvatarGroup"/>.</summary>
public sealed class ArcAvatarMember
{
    public string Name { get; set; } = string.Empty;

    public string? Src { get; set; }

    public ArcAvatarStatus Status { get; set; }
}

/// <summary>
/// An overlapping stack of avatars with a +N overflow chip. Hover fans the stack apart;
/// reduced motion keeps it still.
/// </summary>
public class ArcAvatarGroup : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<ArcAvatarMember>?> MembersProperty =
        AvaloniaProperty.Register<ArcAvatarGroup, IEnumerable<ArcAvatarMember>?>(nameof(Members));

    public static readonly StyledProperty<int> MaxProperty =
        AvaloniaProperty.Register<ArcAvatarGroup, int>(nameof(Max), 4);

    public static readonly StyledProperty<ArcAvatarSize> SizeProperty =
        AvaloniaProperty.Register<ArcAvatarGroup, ArcAvatarSize>(nameof(Size));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcAvatarGroup, string>(nameof(Label), "Team members");

    public static readonly StyledProperty<bool> FanOnHoverProperty =
        AvaloniaProperty.Register<ArcAvatarGroup, bool>(nameof(FanOnHover), true);

    private readonly List<(ArcAvatar Avatar, TranslateTransform Transform, int Index)> _items = new();
    private readonly List<ArcTween> _tweens = new();
    private Panel? _host;
    private bool _hovered;

    static ArcAvatarGroup()
    {
        MembersProperty.Changed.AddClassHandler<ArcAvatarGroup>((group, _) => group.Rebuild());
        MaxProperty.Changed.AddClassHandler<ArcAvatarGroup>((group, _) => group.Rebuild());
        SizeProperty.Changed.AddClassHandler<ArcAvatarGroup>((group, _) => group.Rebuild());
        LabelProperty.Changed.AddClassHandler<ArcAvatarGroup>((group, _) => group.UpdateAutomation());
        FanOnHoverProperty.Changed.AddClassHandler<ArcAvatarGroup>((group, _) => group.ApplyFan(animate: false));
    }

    public ArcAvatarGroup()
    {
        ClipToBounds = false;
    }

    /// <summary>People in display order.</summary>
    public IEnumerable<ArcAvatarMember>? Members
    {
        get => GetValue(MembersProperty);
        set => SetValue(MembersProperty, value);
    }

    /// <summary>Avatars shown before the rest collapse into the overflow count.</summary>
    public int Max
    {
        get => GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public ArcAvatarSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Accessible name for the group and the overflow label.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Spreads the stack on hover for fine pointers.</summary>
    public bool FanOnHover
    {
        get => GetValue(FanOnHoverProperty);
        set => SetValue(FanOnHoverProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        Rebuild();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hovered = true;
        ApplyFan(animate: true);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hovered = false;
        ApplyFan(animate: true);
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label);
    }

    private double Diameter => Size switch
    {
        ArcAvatarSize.Xs => 24,
        ArcAvatarSize.Sm => 28,
        ArcAvatarSize.Md => 40,
        ArcAvatarSize.Lg => 56,
        _ => 88
    };

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _items.Clear();
        foreach (var tween in _tweens)
        {
            tween.Stop();
        }

        _tweens.Clear();

        var members = Members?.ToList() ?? new List<ArcAvatarMember>();
        if (members.Count == 0)
        {
            return;
        }

        var diameter = Diameter;
        var max = Math.Max(1, Max);
        var shown = Math.Min(members.Count, max);
        var overflow = members.Count - shown;
        var overlap = Math.Round(diameter * 0.28);
        var step = diameter - overlap;
        var total = (shown - 1 + (overflow > 0 ? 1 : 0)) * step + diameter;

        var canvas = new Canvas { Width = total, Height = diameter, HorizontalAlignment = HorizontalAlignment.Left };
        for (var i = 0; i < shown; i++)
        {
            var member = members[i];
            var transform = new TranslateTransform();
            var avatar = new ArcAvatar
            {
                DisplayName = member.Name,
                Src = member.Src,
                Status = member.Status,
                Size = Size,
                RenderTransform = transform
            };
            Canvas.SetLeft(avatar, i * step);
            Canvas.SetTop(avatar, 0);
            canvas.Children.Add(avatar);
            _items.Add((avatar, transform, i));
        }

        if (overflow > 0)
        {
            var index = shown;
            var transform = new TranslateTransform();
            var chip = new Border
            {
                Width = diameter,
                Height = diameter,
                CornerRadius = Size == ArcAvatarSize.Xl ? new CornerRadius(diameter / 2) : new CornerRadius(diameter / 2),
                BorderThickness = new Thickness(1),
                RenderTransform = transform,
                Child = new TextBlock
                {
                    Text = $"+{overflow}",
                    FontSize = Math.Clamp(diameter * 0.3, 10, 18),
                    FontWeight = FontWeight.Medium,
                    FontFeatures = FeedbackUi.Tabular,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            chip.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            chip.Paint(Border.BorderBrushProperty, ArcTokens.Border);
            if (chip.Child is TextBlock label)
            {
                label.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
            }

            AutomationProperties.SetName(chip, $"{overflow} more {Label.ToLowerInvariant()}");
            ToolTip.SetTip(chip, $"{overflow} more {Label.ToLowerInvariant()}");
            Canvas.SetLeft(chip, index * step);
            Canvas.SetTop(chip, 0);
            canvas.Children.Add(chip);
            _items.Add((null!, transform, index));
        }

        _host.Children.Add(canvas);
        UpdateAutomation();
        ApplyFan(animate: false);
    }

    private void ApplyFan(bool animate)
    {
        if (!FanOnHover || _items.Count <= 1)
        {
            foreach (var (_, transform, _) in _items)
            {
                transform.X = 0;
            }

            return;
        }

        var spread = _hovered ? Math.Clamp(Diameter * 0.14, 3, 9) : 0;
        for (var i = 0; i < _items.Count; i++)
        {
            var target = spread * i;
            var transform = _items[i].Transform;
            if (!animate)
            {
                transform.X = target;
                continue;
            }

            var tween = new ArcTween();
            var start = transform.X;
            tween.Run(start, target, TimeSpan.FromMilliseconds(180), value => transform.X = value);
            _tweens.Add(tween);
        }
    }
}
