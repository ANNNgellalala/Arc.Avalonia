using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using PuraMarkdown.UI.Platform;

namespace Arc.Avalonia.Chat;

/// <summary>
/// Arc's fill-in for the link handling that PuraMarkdown.UI leaves to the host (ADR 0007 keeps chat
/// rendering upstream, but the upstream chat view defaults to <see cref="NullLinkHandler"/> and exposes no
/// link-click event, so a message link would otherwise do nothing). This handler raises
/// <see cref="ArcChatView.LinkClicked"/>, then lets the host take over (the <see cref="ArcChatView.LinkHandler"/>
/// property, or a handler carried by <see cref="ArcChatView.Options"/>), and otherwise opens
/// http/https/mailto/tel with the platform launcher.
/// </summary>
internal sealed class ArcChatLinkHandler(ArcChatView owner) : ILinkHandler
{
    /// <summary>A host link handler threw while opening the link.</summary>
    internal const string OpenFailedCode = "ARC-CHAT-LINK-001";

    /// <summary>The host has no launcher, or the launcher refused the URI.</summary>
    internal const string NoLauncherCode = "ARC-CHAT-LINK-002";

    /// <summary>
    /// Schemes the platform launcher may own. Same policy as the Arc Markdown link menu: browser/mail/tel
    /// targets open externally, everything else (relative paths, <c>#anchor</c>, <c>ftp</c>/<c>file</c>) needs
    /// a host link handler.
    /// </summary>
    internal static bool IsBrowserTarget(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" or "tel";

    /// <inheritdoc />
    public async Task OpenAsync(string url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var args = new ArcChatLinkClickedEventArgs { Url = url };
        owner.RaiseLinkClicked(args);
        if (args.HandledByHost)
        {
            return;
        }

        if (owner.LinkHandler is { } hostHandler)
        {
            await OpenWithAsync(hostHandler, url, cancellationToken).ConfigureAwait(true);
            return;
        }

        if (owner.OptionsLinkHandler is { } optionsHandler)
        {
            await OpenWithAsync(optionsHandler, url, cancellationToken).ConfigureAwait(true);
            return;
        }

        if (!IsBrowserTarget(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        var launcher = TopLevel.GetTopLevel(owner)?.Launcher;
        if (launcher is null)
        {
            owner.RaiseWarning(NoLauncherCode, $"无法打开链接（当前宿主没有 launcher）：{url}");
            return;
        }

        try
        {
            if (!await launcher.LaunchUriAsync(uri).ConfigureAwait(true))
            {
                owner.RaiseWarning(NoLauncherCode, $"无法打开链接（launcher 拒绝）：{url}");
            }
        }
        catch (Exception ex)
        {
            owner.RaiseWarning(NoLauncherCode, $"打开链接失败：{ex.Message}");
        }
    }

    private async Task OpenWithAsync(ILinkHandler handler, string url, CancellationToken cancellationToken)
    {
        try
        {
            await handler.OpenAsync(url, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            owner.RaiseWarning(OpenFailedCode, $"链接处理器打开失败：{ex.Message}");
        }
    }
}
