using Avalonia.Media.Imaging;

namespace Arc.Avalonia.Markdown;

/// <summary>Offline-safe platform defaults; hosts replace them through <see cref="ArcMarkdownOptions"/>.</summary>
public static class ArcMarkdownPlatform
{
    /// <summary>Default image loader: refuses every URL (no network I/O in the design system).</summary>
    public static IArcMarkdownImageLoader DefaultImageLoader { get; } = new OfflineImageLoader();

    /// <summary>Default link handler: does nothing; hosts install a browser/file opener.</summary>
    public static IArcMarkdownLinkHandler DefaultLinkHandler { get; } = new NoopLinkHandler();

    /// <summary>Default clipboard: no-op; the view falls back to the Avalonia TopLevel clipboard.</summary>
    public static IArcMarkdownClipboard DefaultClipboard { get; } = new NoopClipboard();

    /// <summary>Creates an image loader from a delegate (return null to refuse).</summary>
    public static IArcMarkdownImageLoader ImageLoader(Func<string, CancellationToken, Task<Bitmap?>> load)
        => new DelegateImageLoader(load);

    /// <summary>Creates a link handler from a delegate.</summary>
    public static IArcMarkdownLinkHandler LinkHandler(Func<string, CancellationToken, Task> open)
        => new DelegateLinkHandler(open);

    /// <summary>Creates a clipboard from a delegate.</summary>
    public static IArcMarkdownClipboard Clipboard(Func<string, CancellationToken, Task> setText)
        => new DelegateClipboard(setText);

    private sealed class OfflineImageLoader : IArcMarkdownImageLoader
    {
        public Task<ArcMarkdownImageResult> LoadAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(ArcMarkdownImageResult.Refused("offline"));
    }

    private sealed class NoopLinkHandler : IArcMarkdownLinkHandler
    {
        public Task OpenAsync(string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopClipboard : IArcMarkdownClipboard
    {
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DelegateImageLoader(Func<string, CancellationToken, Task<Bitmap?>> load) : IArcMarkdownImageLoader
    {
        public async Task<ArcMarkdownImageResult> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                var bitmap = await load(url, cancellationToken).ConfigureAwait(false);
                return bitmap is null ? ArcMarkdownImageResult.Refused("rejected") : ArcMarkdownImageResult.Loaded(bitmap);
            }
            catch (Exception ex)
            {
                return ArcMarkdownImageResult.Failed(ex.Message);
            }
        }
    }

    private sealed class DelegateLinkHandler(Func<string, CancellationToken, Task> open) : IArcMarkdownLinkHandler
    {
        public Task OpenAsync(string url, CancellationToken cancellationToken = default) => open(url, cancellationToken);
    }

    private sealed class DelegateClipboard(Func<string, CancellationToken, Task> setText) : IArcMarkdownClipboard
    {
        public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => setText(text, cancellationToken);
    }
}
