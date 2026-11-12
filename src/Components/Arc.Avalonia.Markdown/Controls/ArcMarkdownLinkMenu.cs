using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Markdown.Controls;

/// <summary>
/// The context menu of a rendered Markdown link: tapping the link opens a small Arc surface anchored to the
/// text block that hosts the link with "copy link" and "open in browser" rows. The menu is presentation only;
/// the renderer owns the inline link run, and <see cref="Controls.ArcMarkdownLinkInteraction"/> decides when a
/// tap lands on it.
/// <para>
/// Copy uses <see cref="ArcMarkdownOptions.Clipboard"/> and falls back to the TopLevel clipboard when the
/// default no-op clipboard is installed. Opening prefers a host <see cref="IArcMarkdownLinkHandler"/> (in-app
/// navigation) and otherwise launches an http/https/mailto/tel URL with
/// <see cref="ArcMarkdownPlatform.OpenInBrowserAsync"/>. Rows that cannot navigate (relative
/// <c>#anchor</c>/<c>./path</c> targets or unsupported schemes without a host handler) stay disabled instead
/// of failing silently.
/// </para>
/// </summary>
internal sealed class ArcMarkdownLinkMenu
{
    private const string PresenterClass = "arc-markdown-link-menu";

    private static readonly double RowMinHeight = HitTarget();

    private readonly Control _anchor;
    private readonly string _url;
    private readonly ArcMarkdownRenderContext _context;
    private readonly Action? _onClosed;
    private readonly List<ArcButton> _rows = [];

    private Flyout? _flyout;
    private ArcButton? _copyRow;
    private ArcButton? _openRow;
    private DispatcherTimer? _copiedTimer;

    internal ArcMarkdownLinkMenu(Control anchor, string url, ArcMarkdownRenderContext context, Action? onClosed = null)
    {
        _anchor = anchor;
        _url = url ?? string.Empty;
        _context = context;
        _onClosed = onClosed;
    }

    /// <summary>Link rows of the most recently built menu (copy first, open second).</summary>
    internal IReadOnlyList<ArcButton> Rows => _rows;

    /// <summary>
    /// Whether "open in browser" can do something: a host link handler always can, the platform launcher
    /// needs an absolute URI with a scheme a browser or mail client can own.
    /// </summary>
    internal static bool CanOpenInBrowser(string url, ArcMarkdownOptions options)
        => !ReferenceEquals(options.LinkHandler, ArcMarkdownPlatform.DefaultLinkHandler)
           || IsBrowserTarget(url);

    /// <summary>Builds the menu content without showing it (used by the interaction tests).</summary>
    internal Control CreateContent()
    {
        var localizer = _context.Options.Localizer;
        var panel = new StackPanel { Spacing = 2 };
        AutomationProperties.SetName(panel, localizer.Get(ArcMarkdownStringKeys.LinkMenu));

        _rows.Clear();
        _copyRow = CreateRow(localizer.Get(ArcMarkdownStringKeys.CopyLink));
        _copyRow.Click += (_, _) => _ = CopyLinkAsync();
        _rows.Add(_copyRow);
        panel.Children.Add(_copyRow);

        _openRow = CreateRow(localizer.Get(ArcMarkdownStringKeys.OpenInBrowser));
        _openRow.IsEnabled = CanOpenInBrowser(_url, _context.Options);
        if (!_openRow.IsEnabled)
        {
            ToolTip.SetTip(_openRow, localizer.Get(ArcMarkdownStringKeys.OpenLinkUnavailable));
            ToolTip.SetShowOnDisabled(_openRow, true);
        }

        _openRow.Click += (_, _) => _ = OpenInBrowserAsync();
        _rows.Add(_openRow);
        panel.Children.Add(_openRow);

        // Rows are ordinary Arc buttons: Enter/Space activates them, arrows move, Escape closes. The
        // flyout itself closes on outside taps; keyboard users get the same contract as a native menu.
        panel.KeyDown += OnMenuKeyDown;
        return panel;
    }

    /// <summary>Shows the menu anchored to the link, focusing the first action.</summary>
    internal void Show()
    {
        _flyout?.Hide();
        _copiedTimer?.Stop();

        var flyout = new Flyout
        {
            Content = CreateContent(),
            Placement = PlacementMode.Bottom,
        };
        flyout.FlyoutPresenterClasses.Add(PresenterClass);
        // Focus the first action once the popup owns a focus scope, and give focus back on every close path
        // (Escape, light dismiss, choosing a row) so keyboard readers do not lose their place. The renderer
        // passes a callback that restores the previously focused element; the default keeps the anchor focused.
        flyout.Opened += (_, _) => FocusInitialRow();
        flyout.Closed += (_, _) =>
        {
            if (_onClosed is not null)
            {
                _onClosed();
            }
            else if (_anchor.Focusable)
            {
                _anchor.Focus();
            }
        };
        _flyout = flyout;
        flyout.ShowAt(_anchor);
    }

    /// <summary>Writes the link target to the clipboard and reports the result in the menu row.</summary>
    internal async Task CopyLinkAsync()
    {
        var localizer = _context.Options.Localizer;
        var copied = false;
        try
        {
            if (!ReferenceEquals(_context.Options.Clipboard, ArcMarkdownPlatform.DefaultClipboard))
            {
                await _context.Options.Clipboard.SetTextAsync(_url).ConfigureAwait(true);
                copied = true;
            }
            else if (TopLevel.GetTopLevel(_anchor)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(_url).ConfigureAwait(true);
                copied = true;
            }

            SetCopyRowText(localizer.Get(copied
                ? ArcMarkdownStringKeys.LinkCopied
                : ArcMarkdownStringKeys.CopyFailed));
        }
        catch (Exception ex)
        {
            _context.Warn("ARC-MD-LINK-004", $"复制链接失败：{ex.Message}", nameof(ArcMarkdownLinkMenu));
            SetCopyRowText(localizer.Get(ArcMarkdownStringKeys.CopyFailed));
        }

        ScheduleClose();
    }

    /// <summary>Opens the link: host handler first, otherwise the system browser.</summary>
    internal async Task OpenInBrowserAsync()
    {
        var options = _context.Options;
        var localizer = options.Localizer;

        try
        {
            if (!ReferenceEquals(options.LinkHandler, ArcMarkdownPlatform.DefaultLinkHandler))
            {
                _flyout?.Hide();
                await options.LinkHandler.OpenAsync(_url).ConfigureAwait(true);
                return;
            }

            var opened = await ArcMarkdownPlatform.OpenInBrowserAsync(TopLevel.GetTopLevel(_anchor), _url)
                .ConfigureAwait(true);
            if (opened)
            {
                _flyout?.Hide();
                return;
            }

            // Keep the menu visible and say so: a warning the host never renders is invisible to the reader.
            SetOpenRowText(localizer.Get(ArcMarkdownStringKeys.OpenLinkFailed));
            _context.Warn(
                "ARC-MD-LINK-005",
                $"{localizer.Get(ArcMarkdownStringKeys.OpenLinkFailed)}：{_url}",
                nameof(ArcMarkdownLinkMenu));
            ScheduleClose();
        }
        catch (Exception ex)
        {
            _context.Warn("ARC-MD-LINK-001", $"打开链接失败：{ex.Message}", nameof(ArcMarkdownLinkMenu));
            SetOpenRowText(localizer.Get(ArcMarkdownStringKeys.OpenLinkFailed));
            ScheduleClose();
        }
    }

    private static ArcButton CreateRow(string label)
    {
        var row = new ArcButton
        {
            Content = label,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 0),
            MinHeight = RowMinHeight,
        };
        AutomationProperties.SetName(row, label);
        return row;
    }

    private static bool IsBrowserTarget(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
           uri.Scheme is "http" or "https" or "mailto" or "tel";

    private static double HitTarget()
    {
        var token = ArcResources.Double(ArcTokens.HitTargetMin);
        return token > 0 ? token : 44;
    }

    private void OnMenuKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
                MoveFocus(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                break;
            case Key.Escape:
                _flyout?.Hide();
                e.Handled = true;
                break;
            case Key.Tab:
                // Leaving the menu is the same intent as dismissing it; the Closed handler restores focus.
                _flyout?.Hide();
                break;
        }
    }

    private void FocusInitialRow()
    {
        foreach (var row in _rows)
        {
            if (row.IsEnabled)
            {
                row.Focus();
                return;
            }
        }
    }

    private void MoveFocus(int delta)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var enabled = _rows.Where(row => row.IsEnabled).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        var current = enabled.FindIndex(row => row.IsFocused);
        var next = current < 0 ? 0 : (current + delta + enabled.Count) % enabled.Count;
        enabled[next].Focus();
    }

    private void SetCopyRowText(string text)
    {
        if (_copyRow is null)
        {
            return;
        }

        _copyRow.Content = text;
        AutomationProperties.SetName(_copyRow, text);
    }

    private void SetOpenRowText(string text)
    {
        if (_openRow is null)
        {
            return;
        }

        _openRow.Content = text;
        AutomationProperties.SetName(_openRow, text);
    }

    private void ScheduleClose()
    {
        _copiedTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _copiedTimer.Stop();
        _copiedTimer.Tick -= OnCopiedTimerTick;
        _copiedTimer.Tick += OnCopiedTimerTick;
        _copiedTimer.Start();
    }

    private void OnCopiedTimerTick(object? sender, EventArgs e)
    {
        _copiedTimer?.Stop();
        _flyout?.Hide();
    }
}
