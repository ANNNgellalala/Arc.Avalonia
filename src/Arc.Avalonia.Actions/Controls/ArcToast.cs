using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>
/// A transient confirmation after a background action: title, optional description, one action and a
/// close button. It leaves on its own after a few seconds; hover does not pause the timer, so keep the
/// copy short.
/// </summary>
public sealed class ArcToast : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArcToast, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcToast, string?>(nameof(Description));

    public static readonly StyledProperty<string?> ActionLabelProperty =
        AvaloniaProperty.Register<ArcToast, string?>(nameof(ActionLabel));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcToast, bool>(nameof(IsOpen), true);

    public static readonly StyledProperty<int> DurationProperty =
        AvaloniaProperty.Register<ArcToast, int>(nameof(Duration), 4500);

    private readonly DispatcherTimer _timer;
    private TextBlock? _title;
    private TextBlock? _description;
    private ArcButton? _action;
    private ArcButton? _close;
    private Border? _surface;

    static ArcToast()
    {
        TitleProperty.Changed.AddClassHandler<ArcToast>((toast, _) => toast.UpdateCopy());
        DescriptionProperty.Changed.AddClassHandler<ArcToast>((toast, _) => toast.UpdateCopy());
        ActionLabelProperty.Changed.AddClassHandler<ArcToast>((toast, _) => toast.UpdateCopy());
        IsOpenProperty.Changed.AddClassHandler<ArcToast>((toast, _) => toast.Sync());
        DurationProperty.Changed.AddClassHandler<ArcToast>((toast, _) => toast.RestartTimer());
    }

    public ArcToast()
    {
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => Close();
    }

    /// <summary>Raised when the toast closes, by timeout, the close button or the action.</summary>
    public event EventHandler? Closed;

    /// <summary>Runs when the action button is pressed.</summary>
    public Action? Action { get; set; }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Label for the optional action; no label means no action button.</summary>
    public string? ActionLabel
    {
        get => GetValue(ActionLabelProperty);
        set => SetValue(ActionLabelProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Milliseconds before it dismisses itself; 0 keeps it until it is closed by hand.</summary>
    public int Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scene(e);
        UpdateCopy();
        Sync();
    }

    private void _scene(TemplateAppliedEventArgs e)
    {
        _surface = e.NameScope.Find<Border>("Surface");
        _title = e.NameScope.Find<TextBlock>("Title");
        _description = e.NameScope.Find<TextBlock>("Description");
        _action = e.NameScope.Find<ArcButton>("Action");
        _close = e.NameScope.Find<ArcButton>("Close");

        if (_close is not null)
        {
            _close.Content = new ArcIcon { Icon = "x", Size = 15 };
            AutomationProperties.SetName(_close, "Dismiss notification");
            _close.Click += (_, _) => Close();
        }

        if (_action is not null)
        {
            _action.Click += (_, _) =>
            {
                Action?.Invoke();
                Close();
            };
        }
    }

    private void UpdateCopy()
    {
        if (_title is not null)
        {
            _title.Text = Title;
        }

        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrWhiteSpace(Description);
        }

        if (_action is not null)
        {
            _action.Content = ActionLabel;
            _action.IsVisible = !string.IsNullOrWhiteSpace(ActionLabel);
        }

        AutomationProperties.SetName(this, Title ?? "Notification");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private void Sync()
    {
        if (IsOpen)
        {
            IsVisible = true;
            if (_surface is not null)
            {
                _surface.Opacity = 1;
            }

            RestartTimer();
        }
        else
        {
            if (!IsVisible)
            {
                return;
            }

            IsVisible = false;
            _timer.Stop();
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RestartTimer()
    {
        _timer.Stop();
        if (!IsOpen || Duration <= 0)
        {
            return;
        }

        _timer.Interval = TimeSpan.FromMilliseconds(Duration);
        _timer.Start();
    }

    /// <summary>Closes the toast; the host removes it from its stack.</summary>
    public void Close()
    {
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}
