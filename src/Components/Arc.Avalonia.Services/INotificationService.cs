using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;

namespace Arc.Avalonia.Services;

/// <summary>The intent of a notification; host surfaces may map it to a tone or an icon.</summary>
public enum ArcNotificationKind
{
    /// <summary>Neutral information.</summary>
    Info,

    /// <summary>A completed action.</summary>
    Success,

    /// <summary>Something needs attention but did not fail.</summary>
    Warning,

    /// <summary>An action failed.</summary>
    Danger
}

/// <summary>One notification request. <see cref="Key"/> is what makes repeated notifications deduplicate.</summary>
public sealed record ArcNotification
{
    /// <summary>Title line; required.</summary>
    public required string Title { get; init; }

    /// <summary>Optional second line.</summary>
    public string? Description { get; init; }

    /// <summary>Intent of the notification.</summary>
    public ArcNotificationKind Kind { get; init; } = ArcNotificationKind.Info;

    /// <summary>Stable identity used for de-duplication; null means every notification is distinct.</summary>
    public string? Key { get; init; }

    /// <summary>How long the host should keep it on screen.</summary>
    public int DurationMs { get; init; } = 4500;

    /// <summary>Optional action button label.</summary>
    public string? ActionLabel { get; init; }

    /// <summary>Optional action invoked when the action button is pressed.</summary>
    public Action? Action { get; init; }
}

/// <summary>The presentation half of <see cref="INotificationService"/>: an overlay host or a fake in tests.</summary>
public interface IArcNotificationSink
{
    /// <summary>Shows the notification and calls <paramref name="dismiss"/> when the user or timer closes it.</summary>
    void Present(ArcNotification notification, Action dismiss);

    /// <summary>Removes every notification the sink is showing.</summary>
    void Clear();
}

/// <summary>
/// Shows transient notifications and keeps the list of currently visible ones. A notification carrying a
/// <see cref="ArcNotification.Key"/> that is already visible is ignored, so a repeated "Saved" toast does not
/// stack; callers that want a fresh toast use a different key or <see cref="Dismiss"/> first.
/// </summary>
public interface INotificationService
{
    /// <summary>Notifications currently on screen.</summary>
    IReadOnlyList<ArcNotification> Active { get; }

    /// <summary>Raised after a notification is accepted and handed to the sink.</summary>
    event EventHandler<ArcNotification>? Shown;

    /// <summary>Raised when a notification is dismissed or expires.</summary>
    event EventHandler<ArcNotification>? Dismissed;

    /// <summary>Shows a notification; returns false when it was deduplicated.</summary>
    bool Show(ArcNotification notification);

    /// <summary>Dismisses the notification with the given key; returns whether one was active.</summary>
    bool Dismiss(string key);

    /// <summary>Dismisses every active notification.</summary>
    void Clear();
}

/// <summary>The default <see cref="INotificationService"/>: thread-safe de-duplication and lifecycle over a sink.</summary>
public sealed class ArcNotificationService : INotificationService
{
    private readonly object _gate = new();
    private readonly IArcNotificationSink _sink;
    private readonly List<ArcNotification> _active = new();

    /// <summary>Creates the service over an explicit sink (headless hosts and tests).</summary>
    public ArcNotificationService(IArcNotificationSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    /// <summary>Creates the service over a hosting panel, for example an <see cref="ArcWindow.OverlayHost"/>.</summary>
    public ArcNotificationService(Panel overlayHost)
        : this(new ToastNotificationSink(overlayHost ?? throw new ArgumentNullException(nameof(overlayHost))))
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<ArcNotification> Active
    {
        get
        {
            lock (_gate)
            {
                return _active.ToList();
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<ArcNotification>? Shown;

    /// <inheritdoc />
    public event EventHandler<ArcNotification>? Dismissed;

    /// <inheritdoc />
    public bool Show(ArcNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        lock (_gate)
        {
            if (notification.Key is { Length: > 0 } key && _active.Any(active => active.Key == key))
            {
                return false;
            }

            _active.Add(notification);
        }

        _sink.Present(notification, () => Remove(notification));
        Shown?.Invoke(this, notification);
        return true;
    }

    /// <inheritdoc />
    public bool Dismiss(string key)
    {
        ArcNotification? match;
        lock (_gate)
        {
            match = _active.FirstOrDefault(active => active.Key == key);
        }

        if (match is null)
        {
            return false;
        }

        Remove(match);
        return true;
    }

    /// <inheritdoc />
    public void Clear()
    {
        List<ArcNotification> removed;
        lock (_gate)
        {
            removed = new List<ArcNotification>(_active);
            _active.Clear();
        }

        _sink.Clear();
        foreach (var notification in removed)
        {
            Dismissed?.Invoke(this, notification);
        }
    }

    private void Remove(ArcNotification notification)
    {
        bool removed;
        lock (_gate)
        {
            removed = _active.Remove(notification);
        }

        if (removed)
        {
            Dismissed?.Invoke(this, notification);
        }
    }
}

/// <summary>
/// The default notification surface: an <see cref="ArcToastHost"/> pinned to the bottom right of an overlay
/// panel. It needs the Arc component themes (they come with <c>ArcTheme</c>).
/// </summary>
public sealed class ToastNotificationSink : IArcNotificationSink
{
    private readonly Panel _overlay;
    private ArcToastHost? _host;

    /// <summary>Creates the sink over an overlay panel.</summary>
    public ToastNotificationSink(Panel overlay)
    {
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
    }

    /// <inheritdoc />
    public void Present(ArcNotification notification, Action dismiss)
    {
        var host = EnsureHost();
        var toast = host.Show(
            notification.Title,
            notification.Description,
            notification.ActionLabel,
            notification.Action,
            notification.DurationMs);
        toast.Closed += (_, _) => dismiss();
    }

    /// <inheritdoc />
    public void Clear() => _host?.Clear();

    private ArcToastHost EnsureHost()
    {
        if (_host is not null)
        {
            return _host;
        }

        _host = new ArcToastHost
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 24, 24)
        };
        _overlay.Children.Add(_host);
        return _host;
    }
}
