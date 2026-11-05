using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Arc.Avalonia.Services;

/// <summary>The pieces a caller supplies for a dialog: copy, optional body content and the returned values.</summary>
/// <typeparam name="TResult">The value type the dialog returns for each outcome.</typeparam>
public sealed record ArcDialogRequest<TResult>
{
    /// <summary>Dialog heading.</summary>
    public string? Title { get; init; }

    /// <summary>One or two lines under the heading.</summary>
    public string? Description { get; init; }

    /// <summary>Optional body content shown between the copy and the actions.</summary>
    public Control? Content { get; init; }

    /// <summary>Label of the affirmative action.</summary>
    public string PrimaryText { get; init; } = "OK";

    /// <summary>Optional second action between the affirmative and the cancel action.</summary>
    public string? SecondaryText { get; init; }

    /// <summary>Label of the cancel action; set to null to hide it.</summary>
    public string? CancelText { get; init; } = "Cancel";

    /// <summary>Renders the affirmative action in the danger tone.</summary>
    public bool IsDestructive { get; init; }

    /// <summary>Whether a backdrop press closes the dialog with the cancel result.</summary>
    public bool CloseOnBackdropPress { get; init; } = true;

    /// <summary>Value returned by the affirmative action.</summary>
    public required TResult PrimaryResult { get; init; }

    /// <summary>Value returned by the secondary action; defaults to <see cref="PrimaryResult"/>'s type default.</summary>
    public TResult? SecondaryResult { get; init; }

    /// <summary>Value returned when the user cancels or presses Escape.</summary>
    public TResult? CancelResult { get; init; }
}

/// <summary>The copy of a yes/no confirmation, the common case of <see cref="IDialogService.ShowAsync{TResult}"/>.</summary>
public sealed record ArcConfirmRequest
{
    /// <summary>Dialog heading.</summary>
    public string? Title { get; init; }

    /// <summary>The question shown under the heading.</summary>
    public string? Message { get; init; }

    /// <summary>Label of the confirm action.</summary>
    public string ConfirmText { get; init; } = "Confirm";

    /// <summary>Label of the cancel action; set to null to hide it.</summary>
    public string? CancelText { get; init; } = "Cancel";

    /// <summary>Renders the confirm action in the danger tone.</summary>
    public bool IsDestructive { get; init; }

    /// <summary>Whether a backdrop press counts as a cancel.</summary>
    public bool CloseOnBackdropPress { get; init; } = true;
}

/// <summary>
/// Shows a dialog above the current surface and returns its result. The default implementation uses the
/// <see cref="ArcWindow"/> overlay and serialises one dialog at a time; a headless host can implement
/// <see cref="IArcDialogPresenter"/> directly.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a dialog and completes with the value of the chosen action.</summary>
    Task<TResult> ShowAsync<TResult>(ArcDialogRequest<TResult> request, CancellationToken cancellationToken = default);

    /// <summary>Shows a yes/no confirmation; true for confirm, false for cancel or dismiss.</summary>
    Task<bool> ConfirmAsync(ArcConfirmRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The presentation half of a dialog service, so an overlay host (or a headless test) can drive it.</summary>
public interface IArcDialogPresenter
{
    /// <summary>Presents the dialog and completes when the user picks an action or dismisses it.</summary>
    Task<TResult> ShowAsync<TResult>(ArcDialogRequest<TResult> request, CancellationToken cancellationToken);
}

/// <summary>
/// The default <see cref="IDialogService"/>: queues dialog requests so only one is open at a time and
/// delegates presentation to an <see cref="IArcDialogPresenter"/>.
/// </summary>
public sealed class ArcDialogService : IDialogService
{
    private readonly IArcDialogPresenter _presenter;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the service over an explicit presenter (headless hosts and tests).</summary>
    public ArcDialogService(IArcDialogPresenter presenter)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
    }

    /// <summary>Creates the service over a hosting panel, for example an <see cref="ArcWindow.OverlayHost"/>.</summary>
    public ArcDialogService(Panel overlayHost)
        : this(new OverlayDialogPresenter(overlayHost ?? throw new ArgumentNullException(nameof(overlayHost))))
    {
    }

    /// <inheritdoc />
    public async Task<TResult> ShowAsync<TResult>(ArcDialogRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            return await _presenter.ShowAsync(request, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(ArcConfirmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ShowAsync(
            new ArcDialogRequest<bool>
            {
                Title = request.Title,
                Description = request.Message,
                PrimaryText = request.ConfirmText,
                CancelText = request.CancelText,
                IsDestructive = request.IsDestructive,
                CloseOnBackdropPress = request.CloseOnBackdropPress,
                PrimaryResult = true,
                CancelResult = false
            },
            cancellationToken);
    }
}
