using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Arc.Avalonia.Services;

/// <summary>
/// One place for the file, folder and save dialogs a component needs. The default implementation delegates
/// to Avalonia's <c>TopLevel.StorageProvider</c>; a headless app registers its own fake in
/// <see cref="ArcServiceProvider"/> and the file-picking components use it without any platform window.
/// </summary>
public interface IFilePickerService
{
    /// <summary>Opens the platform open-file dialog.</summary>
    /// <param name="options">Title, filters and multi-select flag.</param>
    /// <param name="owner">The top level that should parent the dialog; defaults to the active window.</param>
    /// <param name="cancellationToken">Cancels the dialog.</param>
    Task<IReadOnlyList<IStorageFile>> OpenFileAsync(
        FilePickerOpenOptions options,
        TopLevel? owner = null,
        CancellationToken cancellationToken = default);

    /// <summary>Opens the platform save-file dialog.</summary>
    Task<IStorageFile?> SaveFileAsync(
        FilePickerSaveOptions options,
        TopLevel? owner = null,
        CancellationToken cancellationToken = default);

    /// <summary>Opens the platform folder picker.</summary>
    Task<IReadOnlyList<IStorageFolder>> PickFolderAsync(
        FolderPickerOpenOptions options,
        TopLevel? owner = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IFilePickerService"/>: Avalonia's <c>StorageProvider</c> on the owner top level,
/// falling back to the desktop main window. Returns an empty result when no platform top level exists, which
/// keeps headless runs safe.
/// </summary>
public sealed class StorageProviderFilePickerService : IFilePickerService
{
    /// <summary>Shared stateless instance; safe to register once at startup.</summary>
    public static StorageProviderFilePickerService Instance { get; } = new();

    /// <inheritdoc />
    public async Task<IReadOnlyList<IStorageFile>> OpenFileAsync(
        FilePickerOpenOptions options,
        TopLevel? owner = null,
        CancellationToken cancellationToken = default)
    {
        var provider = ResolveProvider(owner);
        if (provider is null)
        {
            return System.Array.Empty<IStorageFile>();
        }

        var files = await provider.OpenFilePickerAsync(options);
        if (cancellationToken.IsCancellationRequested)
        {
            return System.Array.Empty<IStorageFile>();
        }

        return files;
    }

    /// <inheritdoc />
    public async Task<IStorageFile?> SaveFileAsync(
        FilePickerSaveOptions options,
        TopLevel? owner = null,
        CancellationToken cancellationToken = default)
    {
        var provider = ResolveProvider(owner);
        if (provider is null)
        {
            return null;
        }

        var file = await provider.SaveFilePickerAsync(options);
        return cancellationToken.IsCancellationRequested ? null : file;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IStorageFolder>> PickFolderAsync(
        FolderPickerOpenOptions options,
        TopLevel? owner = null,
        CancellationToken cancellationToken = default)
    {
        var provider = ResolveProvider(owner);
        if (provider is null)
        {
            return System.Array.Empty<IStorageFolder>();
        }

        var folders = await provider.OpenFolderPickerAsync(options);
        return cancellationToken.IsCancellationRequested ? System.Array.Empty<IStorageFolder>() : folders;
    }

    private static IStorageProvider? ResolveProvider(TopLevel? owner)
    {
        var top = owner ?? ActiveTopLevel();
        return top?.StorageProvider;
    }

    private static TopLevel? ActiveTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
