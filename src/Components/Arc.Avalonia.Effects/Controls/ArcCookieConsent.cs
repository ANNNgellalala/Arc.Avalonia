using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>Which shape the consent surface is showing.</summary>
public enum ArcConsentFace
{
    Bar,
    Panel,
    Reopen
}

/// <summary>
/// A consent bar that morphs into a preferences panel and folds into a small reopen button. One surface
/// springs its width, height and radius between the three shapes, growing on the slightly bouncy morph
/// spring and shrinking on the flat smooth one so it never overshoots. Choices live in memory for the
/// session; nothing is sent anywhere.
/// </summary>
[TemplatePart("PART_Layer", typeof(Panel))]
[TemplatePart("PART_Surface", typeof(Border))]
[TemplatePart("PART_Faces", typeof(Panel))]
[TemplatePart("PART_FaceBar", typeof(StackPanel))]
[TemplatePart("PART_FacePanel", typeof(StackPanel))]
[TemplatePart("PART_ReopenButton", typeof(ArcButton))]
[TemplatePart("PART_BarPolicy", typeof(ArcButton))]
[TemplatePart("PART_CustomizeButton", typeof(ArcButton))]
[TemplatePart("PART_AcceptButton", typeof(ArcButton))]
[TemplatePart("PART_RejectButton", typeof(ArcButton))]
[TemplatePart("PART_PanelReject", typeof(ArcButton))]
[TemplatePart("PART_PanelSave", typeof(ArcButton))]
[TemplatePart("PART_PanelPolicy", typeof(ArcButton))]
[TemplatePart("PART_BarMessage", typeof(TextBlock))]
[TemplatePart("PART_PanelTitle", typeof(TextBlock))]
[TemplatePart("PART_PanelDescription", typeof(TextBlock))]
[TemplatePart("PART_CategoryHost", typeof(StackPanel))]
public class ArcCookieConsent : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<ArcCookieCategory>?> CategoriesProperty =
        AvaloniaProperty.Register<ArcCookieConsent, IReadOnlyList<ArcCookieCategory>?>(
            nameof(Categories),
            ArcDefaultCookieCategories.All);

    public static readonly StyledProperty<ArcConsentValue?> ValueProperty =
        AvaloniaProperty.Register<ArcCookieConsent, ArcConsentValue?>(nameof(Value));

    public static readonly StyledProperty<string?> StorageKeyProperty =
        AvaloniaProperty.Register<ArcCookieConsent, string?>(nameof(StorageKey));

    public static readonly StyledProperty<string> VersionProperty =
        AvaloniaProperty.Register<ArcCookieConsent, string>(nameof(Version), "1");

    public static readonly StyledProperty<bool> PreferencesOpenProperty =
        AvaloniaProperty.Register<ArcCookieConsent, bool>(nameof(PreferencesOpen));

    public static readonly StyledProperty<ArcCookiePolicy?> PolicyProperty =
        AvaloniaProperty.Register<ArcCookieConsent, ArcCookiePolicy?>(nameof(Policy));

    public static readonly StyledProperty<ArcCookieConsentPosition> PositionProperty =
        AvaloniaProperty.Register<ArcCookieConsent, ArcCookieConsentPosition>(nameof(Position));

    public static readonly StyledProperty<bool> ShowReopenButtonProperty =
        AvaloniaProperty.Register<ArcCookieConsent, bool>(nameof(ShowReopenButton), true);

    public static readonly StyledProperty<ArcCookieConsentReopenSide> ReopenSideProperty =
        AvaloniaProperty.Register<ArcCookieConsent, ArcCookieConsentReopenSide>(
            nameof(ReopenSide),
            ArcCookieConsentReopenSide.Left);

    public static readonly StyledProperty<ArcCookieConsentLabels> LabelsProperty =
        AvaloniaProperty.Register<ArcCookieConsent, ArcCookieConsentLabels>(
            nameof(Labels),
            ArcCookieConsentLabels.Default);

    private static readonly Dictionary<string, (ArcConsentValue Value, string Version, DateTimeOffset UpdatedAt)> Store =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, bool> _draft = new(StringComparer.Ordinal);
    private Panel? _layer;
    private Border? _surface;
    private Panel? _faces;
    private StackPanel? _faceBar;
    private StackPanel? _facePanel;
    private ArcButton? _reopen;
    private ArcButton? _barPolicy;
    private ArcButton? _customize;
    private ArcButton? _accept;
    private ArcButton? _reject;
    private ArcButton? _panelReject;
    private ArcButton? _panelSave;
    private ArcButton? _panelPolicy;
    private TextBlock? _barMessage;
    private TextBlock? _panelTitle;
    private TextBlock? _panelDescription;
    private StackPanel? _categoryHost;
    private IDisposable? _dismiss;
    private ArcTween? _tween;
    private ArcConsentFace? _face;
    private bool _applied;

    static ArcCookieConsent()
    {
        CategoriesProperty.Changed.AddClassHandler<ArcCookieConsent>((consent, _) => consent.BuildRows());
        ValueProperty.Changed.AddClassHandler<ArcCookieConsent>((consent, _) => consent.AdoptValue());
        PreferencesOpenProperty.Changed.AddClassHandler<ArcCookieConsent>((consent, _) =>
        {
            consent.Sync();
            consent.PreferencesOpenChanged?.Invoke(consent, consent.PreferencesOpen);
        });
        PolicyProperty.Changed.AddClassHandler<ArcCookieConsent>((consent, _) => consent.UpdateCopy());
        PositionProperty.Changed.AddClassHandler<ArcCookieConsent>((consent, _) => consent.UpdatePosition());
        ShowReopenButtonProperty.Changed.AddClassHandler<ArcCookieConsent>((consent, _) => consent.Sync());
        ReopenSideProperty.Changed.AddClassHandler<ArcCookieConsent>((consent, _) => consent.UpdatePosition());
        LabelsProperty.Changed.AddClassHandler<ArcCookieConsent>((consent, _) => consent.UpdateCopy());
        VersionProperty.Changed.AddClassHandler<ArcCookieConsent>((consent, _) => consent.AdoptValue());
    }

    public ArcCookieConsent()
    {
        Focusable = false;
        ClipToBounds = false;
    }

    public event EventHandler<ArcConsentChangedEventArgs>? Changed;

    /// <summary>Bubbled copy of <see cref="Changed"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcConsentChangedEventArgs> ChangedEvent =
        RoutedEvent.Register<ArcCookieConsent, ArcConsentChangedEventArgs>("Changed", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="Changed"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcConsentChangedEventArgs>? ChangedRouted
    {
        add => AddHandler(ChangedEvent, value);
        remove => RemoveHandler(ChangedEvent, value);
    }

    public event EventHandler<bool>? PreferencesOpenChanged;

    public IReadOnlyList<ArcCookieCategory>? Categories
    {
        get => GetValue(CategoriesProperty);
        set => SetValue(CategoriesProperty, value);
    }

    /// <summary>The saved choice; null means the visitor has not decided yet.</summary>
    public ArcConsentValue? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Remembers the choice for this session under this key, the analogue of localStorage.</summary>
    public string? StorageKey
    {
        get => GetValue(StorageKeyProperty);
        set => SetValue(StorageKeyProperty, value);
    }

    /// <summary>Bump when categories or the policy change so a stored choice is ignored.</summary>
    public string Version
    {
        get => GetValue(VersionProperty);
        set => SetValue(VersionProperty, value);
    }

    public bool PreferencesOpen
    {
        get => GetValue(PreferencesOpenProperty);
        set => SetValue(PreferencesOpenProperty, value);
    }

    public ArcCookiePolicy? Policy
    {
        get => GetValue(PolicyProperty);
        set => SetValue(PolicyProperty, value);
    }

    public ArcCookieConsentPosition Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public bool ShowReopenButton
    {
        get => GetValue(ShowReopenButtonProperty);
        set => SetValue(ShowReopenButtonProperty, value);
    }

    public ArcCookieConsentReopenSide ReopenSide
    {
        get => GetValue(ReopenSideProperty);
        set => SetValue(ReopenSideProperty, value);
    }

    public ArcCookieConsentLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <summary>Reads a saved choice for this session, or null when there is none.</summary>
    public static ArcConsentValue? Read(string storageKey, string version = "1") =>
        Store.TryGetValue(storageKey, out var saved) && saved.Version == version ? saved.Value : null;

    /// <summary>Opens the preferences panel, the analogue of the imperative handle.</summary>
    public void OpenPreferences() => SetCurrentValue(PreferencesOpenProperty, true);

    /// <summary>Closes the panel without saving and returns to the bar.</summary>
    public void ClosePreferences() => SetCurrentValue(PreferencesOpenProperty, false);

    /// <summary>Forgets the saved choice and asks again.</summary>
    public void Reset()
    {
        if (!string.IsNullOrWhiteSpace(StorageKey))
        {
            Store.Remove(StorageKey!);
        }

        _draft.Clear();
        SetCurrentValue(ValueProperty, null);
        SetCurrentValue(PreferencesOpenProperty, false);
        var resetArgs = new ArcConsentChangedEventArgs(null, ArcConsentAction.Reset, DateTimeOffset.UtcNow) { RoutedEvent = ChangedEvent };
        Changed?.Invoke(this, resetArgs);
        RaiseEvent(resetArgs);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layer = e.NameScope.Find<Panel>("PART_Layer");
        _surface = e.NameScope.Find<Border>("PART_Surface");
        _faces = e.NameScope.Find<Panel>("PART_Faces");
        _faceBar = e.NameScope.Find<StackPanel>("PART_FaceBar");
        _facePanel = e.NameScope.Find<StackPanel>("PART_FacePanel");
        _reopen = e.NameScope.Find<ArcButton>("PART_ReopenButton");
        _barPolicy = e.NameScope.Find<ArcButton>("PART_BarPolicy");
        _customize = e.NameScope.Find<ArcButton>("PART_CustomizeButton");
        _accept = e.NameScope.Find<ArcButton>("PART_AcceptButton");
        _reject = e.NameScope.Find<ArcButton>("PART_RejectButton");
        _panelReject = e.NameScope.Find<ArcButton>("PART_PanelReject");
        _panelSave = e.NameScope.Find<ArcButton>("PART_PanelSave");
        _panelPolicy = e.NameScope.Find<ArcButton>("PART_PanelPolicy");
        _barMessage = e.NameScope.Find<TextBlock>("PART_BarMessage");
        _panelTitle = e.NameScope.Find<TextBlock>("PART_PanelTitle");
        _panelDescription = e.NameScope.Find<TextBlock>("PART_PanelDescription");
        _categoryHost = e.NameScope.Find<StackPanel>("PART_CategoryHost");

        if (_customize is not null)
        {
            _customize.Click += (_, _) => SetCurrentValue(PreferencesOpenProperty, true);
        }

        if (_accept is not null)
        {
            _accept.Click += (_, _) => Decide(ArcConsentAction.AcceptAll);
        }

        if (_reject is not null)
        {
            _reject.Click += (_, _) => Decide(ArcConsentAction.RejectAll);
        }

        if (_panelSave is not null)
        {
            _panelSave.Click += (_, _) => Decide(ArcConsentAction.Save);
        }

        if (_panelReject is not null)
        {
            _panelReject.Click += (_, _) => Decide(ArcConsentAction.RejectAll);
        }

        if (_reopen is not null)
        {
            _reopen.Content = ArcEffectVisuals.Icon("sliders-horizontal", 18);
            _reopen.Classes.Add("consent-reopen");
            _reopen.Click += (_, _) => SetCurrentValue(PreferencesOpenProperty, true);
        }

        if (_barPolicy is not null)
        {
            _barPolicy.Click += (_, _) => InvokePolicy();
        }

        if (_panelPolicy is not null)
        {
            _panelPolicy.Click += (_, _) => InvokePolicy();
        }

        _applied = false;
        AdoptValue();
        UpdateCopy();
        UpdatePosition();
        BuildRows();
        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _dismiss?.Dispose();
        _dismiss = null;
        _tween?.Stop();
        _tween = null;
    }

    private void InvokePolicy() => Policy?.OnClick?.Invoke();

    private void AdoptValue()
    {
        if (!_applied)
        {
            _applied = true;
            if (Value is null && !string.IsNullOrWhiteSpace(StorageKey))
            {
                var stored = Read(StorageKey!, Version);
                if (stored is not null)
                {
                    SetCurrentValue(ValueProperty, stored);
                }
            }
        }

        SeedDraft(Value);
        Sync();
    }

    private void SeedDraft(ArcConsentValue? value)
    {
        _draft.Clear();
        foreach (var category in Categories ?? Array.Empty<ArcCookieCategory>())
        {
            var enabled = category.Required
                || (value is not null ? value.IsAllowed(category.Id) : category.DefaultEnabled);
            _draft[category.Id] = enabled;
        }
    }

    private void UpdateCopy()
    {
        var labels = Labels ?? ArcCookieConsentLabels.Default;
        if (_barMessage is not null)
        {
            _barMessage.Text = labels.Message;
        }

        if (_panelTitle is not null)
        {
            _panelTitle.Text = labels.PreferencesTitle;
        }

        if (_panelDescription is not null)
        {
            _panelDescription.Text = labels.PreferencesDescription;
        }

        if (_customize is not null)
        {
            _customize.Content = labels.Customize;
            AutomationProperties.SetName(_customize, labels.Customize);
        }

        if (_accept is not null)
        {
            _accept.Content = labels.AcceptAll;
            AutomationProperties.SetName(_accept, labels.AcceptAll);
        }

        if (_reject is not null)
        {
            _reject.Content = labels.RejectAll;
            AutomationProperties.SetName(_reject, labels.RejectAll);
        }

        if (_panelSave is not null)
        {
            _panelSave.Content = labels.Save;
        }

        if (_panelReject is not null)
        {
            _panelReject.Content = labels.RejectAll;
            AutomationProperties.SetName(_panelReject, labels.RejectAll);
        }

        if (_reopen is not null)
        {
            AutomationProperties.SetName(_reopen, labels.Reopen);
            AutomationProperties.SetHelpText(_reopen, "Opens cookie preferences");
        }

        var hasPolicy = Policy is not null;
        if (_barPolicy is not null)
        {
            _barPolicy.Content = Policy?.Label ?? "Cookie policy";
            _barPolicy.IsVisible = hasPolicy;
        }

        if (_panelPolicy is not null)
        {
            _panelPolicy.Content = Policy?.Label ?? "Cookie policy";
            _panelPolicy.IsVisible = hasPolicy;
        }

        AutomationProperties.SetName(this, labels.Message);
    }

    private void UpdatePosition()
    {
        if (_surface is null)
        {
            return;
        }

        _surface.HorizontalAlignment = _face == ArcConsentFace.Reopen
            ? (ReopenSide == ArcCookieConsentReopenSide.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left)
            : Position switch
            {
                ArcCookieConsentPosition.BottomLeft => HorizontalAlignment.Left,
                ArcCookieConsentPosition.BottomRight => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center
            };
    }

    private void BuildRows()
    {
        if (_categoryHost is null)
        {
            return;
        }

        _categoryHost.Children.Clear();
        if (_draft.Count == 0)
        {
            SeedDraft(Value);
        }

        foreach (var category in Categories ?? Array.Empty<ArcCookieCategory>())
        {
            _categoryHost.Children.Add(BuildRow(category));
        }

        UpdateSaveHelp();
    }

    private Control BuildRow(ArcCookieCategory category)
    {
        var labels = Labels ?? ArcCookieConsentLabels.Default;
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { Text = category.Label };
        title.Classes.Add("arc-label");
        text.Children.Add(title);
        if (!string.IsNullOrWhiteSpace(category.Description))
        {
            var hint = new TextBlock { Text = category.Description, TextWrapping = TextWrapping.Wrap };
            hint.Classes.Add("arc-caption");
            text.Children.Add(hint);
        }

        stack.Children.Add(text);

        if (category.Required)
        {
            var always = new TextBlock { Text = labels.AlwaysOn, VerticalAlignment = VerticalAlignment.Center };
            always.Classes.Add("arc-caption");
            AutomationProperties.SetName(always, $"{category.Label}: {labels.AlwaysOn}");
            stack.Children.Add(always);
            return new Border { Child = stack, Padding = new Thickness(12, 10) };
        }

        var check = new Border { Classes = { "pref-check" } };
        var glyph = ArcEffectVisuals.Icon("check", 14);
        glyph.Foreground = ArcEffectVisuals.Brush(ArcTokens.AccentForeground);
        check.Child = glyph;
        var initial = _draft.TryGetValue(category.Id, out var on) && on;
        UpdateCheck(check, glyph, initial);

        var row = new ArcButton
        {
            Classes = { "pref-row" },
            Variant = ArcButtonVariant.Ghost,
            Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    stack,
                    check
                }
            }
        };
        Grid.SetColumn(check, 1);

        AutomationProperties.SetName(row, category.Label);
        AutomationProperties.SetHelpText(row, StateHelp(category, _draft.TryGetValue(category.Id, out var enabled) && enabled));

        row.Click += (_, _) =>
        {
            var next = !(_draft.TryGetValue(category.Id, out var current) && current);
            _draft[category.Id] = next;
            UpdateCheck(check, glyph, next);
            AutomationProperties.SetHelpText(row, StateHelp(category, next));
            UpdateSaveHelp();
        };

        return row;
    }

    private static string StateHelp(ArcCookieCategory category, bool enabled) =>
        enabled ? $"{category.Label}: allowed. Press to block." : $"{category.Label}: blocked. Press to allow.";

    private static void UpdateCheck(Border check, ArcIcon glyph, bool on)
    {
        check.Classes.Set("on", on);
        glyph.IsVisible = on;
    }

    private void UpdateSaveHelp()
    {
        if (_panelSave is null)
        {
            return;
        }

        var categories = Categories ?? Array.Empty<ArcCookieCategory>();
        var optional = categories.Count(category => !category.Required);
        var allowed = categories.Count(category => !category.Required && _draft.TryGetValue(category.Id, out var on) && on);
        AutomationProperties.SetHelpText(_panelSave, $"{allowed} of {optional} optional categories allowed");
    }

    private ArcConsentFace DesiredFace()
    {
        if (PreferencesOpen)
        {
            return ArcConsentFace.Panel;
        }

        return Value is not null ? ArcConsentFace.Reopen : ArcConsentFace.Bar;
    }

    private void Decide(ArcConsentAction action)
    {
        var categories = Categories ?? Array.Empty<ArcCookieCategory>();
        var allowed = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var category in categories)
        {
            allowed[category.Id] = action == ArcConsentAction.AcceptAll
                || (action != ArcConsentAction.RejectAll
                    && (category.Required || (_draft.TryGetValue(category.Id, out var on) && on)));
            if (category.Required)
            {
                allowed[category.Id] = true;
            }

            _draft[category.Id] = allowed[category.Id];
        }

        var value = new ArcConsentValue(allowed);
        var updatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(StorageKey))
        {
            Store[StorageKey!] = (value, Version, updatedAt);
        }

        SetCurrentValue(ValueProperty, value);
        SetCurrentValue(PreferencesOpenProperty, false);
        var changedArgs = new ArcConsentChangedEventArgs(value, action, updatedAt) { RoutedEvent = ChangedEvent };
        Changed?.Invoke(this, changedArgs);
        RaiseEvent(changedArgs);
    }

    private void Sync()
    {
        if (_surface is null || _faces is null)
        {
            return;
        }

        var hidden = Value is not null && !ShowReopenButton && !PreferencesOpen;
        _surface.IsVisible = !hidden;
        if (hidden)
        {
            _dismiss?.Dispose();
            _dismiss = null;
            return;
        }

        var face = DesiredFace();
        var animate = _face is not null && _face != face && !ArcThemeManager.ReducedMotion;
        _face = face;
        UpdatePosition();

        if (_faceBar is not null)
        {
            _faceBar.IsVisible = face == ArcConsentFace.Bar;
        }

        if (_facePanel is not null)
        {
            _facePanel.IsVisible = face == ArcConsentFace.Panel;
        }

        if (_reopen is not null)
        {
            _reopen.IsVisible = face == ArcConsentFace.Reopen;
        }

        _dismiss?.Dispose();
        _dismiss = null;
        if (face == ArcConsentFace.Panel)
        {
            _dismiss = ArcEffectDismiss.Attach(this, ClosePreferences);
        }

        // A face change is a resize of the one surface; let layout settle first so the target face can
        // be measured at its real width.
        Dispatcher.UIThread.Post(() => Resize(face, animate), DispatcherPriority.Loaded);
    }

    private void Resize(ArcConsentFace face, bool animate)
    {
        if (_surface is null || _layer is null)
        {
            return;
        }

        var layerWidth = Bounds.Width > 0 ? Bounds.Width - 32 : 0;
        if (layerWidth <= 0)
        {
            _surface.ClearValue(WidthProperty);
            _surface.ClearValue(HeightProperty);
            return;
        }

        var targetWidth = face switch
        {
            ArcConsentFace.Panel => Math.Min(420, layerWidth),
            ArcConsentFace.Reopen => 48,
            _ => Math.Min(660, layerWidth)
        };

        var faceControl = face switch
        {
            ArcConsentFace.Panel => (Control?)_facePanel,
            ArcConsentFace.Reopen => _reopen,
            _ => _faceBar
        };

        var targetHeight = 48.0;
        if (faceControl is not null && face != ArcConsentFace.Reopen)
        {
            faceControl.InvalidateMeasure();
            faceControl.Measure(new Size(targetWidth, double.PositiveInfinity));
            targetHeight = Math.Ceiling(faceControl.DesiredSize.Height);
        }

        var radius = ArcResources.Radius(ArcTokens.RadiusPanel);
        var pill = 9999.0;
        var fromWidth = _surface.Width is double.NaN || _surface.Width <= 0 ? targetWidth : _surface.Width;
        var fromHeight = _surface.Height is double.NaN || _surface.Height <= 0 ? targetHeight : _surface.Height;
        var currentRadius = _surface.CornerRadius.TopLeft;
        var fromRadius = currentRadius > 0 ? currentRadius : radius.TopLeft;
        var toRadius = face == ArcConsentFace.Reopen ? pill : radius.TopLeft;

        _tween?.Stop();
        if (!animate)
        {
            _surface.Width = targetWidth;
            _surface.Height = targetHeight;
            _surface.CornerRadius = new CornerRadius(toRadius);
            ResetFaceOpacity(face);
            return;
        }

        var grow = targetWidth * targetHeight > fromWidth * fromHeight;
        var easing = grow ? ArcMotion.SpringMorph : ArcMotion.SpringSmooth;
        var duration = grow ? ArcMotion.SpringMorphDuration : ArcMotion.SpringSmoothDuration;

        FadeFaces(face, 0);

        // Shrinking must not overshoot: the smooth spring is critically damped.
        _tween = ArcTween.Run(duration, easing, progress =>
        {
            _surface.Width = Math.Max(1, fromWidth + (targetWidth - fromWidth) * progress);
            _surface.Height = Math.Max(1, fromHeight + (targetHeight - fromHeight) * progress);
            _surface.CornerRadius = new CornerRadius(fromRadius + (toRadius - fromRadius) * Math.Clamp(progress, 0, 1));
            FadeFaces(face, progress);
        }, () =>
        {
            _surface.Width = targetWidth;
            _surface.Height = targetHeight;
            _surface.CornerRadius = new CornerRadius(toRadius);
            ResetFaceOpacity(face);
        });
    }

    private void FadeFaces(ArcConsentFace face, double progress)
    {
        if (_faceBar is null || _facePanel is null || _reopen is null)
        {
            return;
        }

        // The incoming face rises out of a short fade once the surface has made room for it.
        var incoming = Math.Clamp((progress - 0.25) / 0.75, 0, 1);
        _faceBar.Opacity = face == ArcConsentFace.Bar ? incoming : 0;
        _facePanel.Opacity = face == ArcConsentFace.Panel ? incoming : 0;
        _reopen.Opacity = face == ArcConsentFace.Reopen ? incoming : 0;
    }

    private void ResetFaceOpacity(ArcConsentFace face)
    {
        if (_faceBar is not null)
        {
            _faceBar.Opacity = face == ArcConsentFace.Bar ? 1 : 0;
            _faceBar.IsVisible = face == ArcConsentFace.Bar;
        }

        if (_facePanel is not null)
        {
            _facePanel.Opacity = face == ArcConsentFace.Panel ? 1 : 0;
            _facePanel.IsVisible = face == ArcConsentFace.Panel;
        }

        if (_reopen is not null)
        {
            _reopen.Opacity = face == ArcConsentFace.Reopen ? 1 : 0;
            _reopen.IsVisible = face == ArcConsentFace.Reopen;
        }
    }
}
