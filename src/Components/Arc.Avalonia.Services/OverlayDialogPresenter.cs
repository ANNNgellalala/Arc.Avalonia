using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Overlays.Controls;

namespace Arc.Avalonia.Services;

/// <summary>
/// Presents <see cref="ArcDialogRequest{TResult}"/> in an <see cref="ArcDialog"/> on an overlay panel. The
/// dialog already traps focus, closes on Escape or backdrop press and returns focus to the previous element.
/// </summary>
internal sealed class OverlayDialogPresenter : IArcDialogPresenter
{
    private readonly Panel _overlay;

    public OverlayDialogPresenter(Panel overlay)
    {
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
    }

    public async Task<TResult> ShowAsync<TResult>(ArcDialogRequest<TResult> request, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog = new ArcDialog
        {
            Title = request.Title,
            Description = request.Description,
            Content = request.Content,
            CloseOnBackdropPress = request.CloseOnBackdropPress
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        if (!string.IsNullOrWhiteSpace(request.CancelText))
        {
            var cancel = new ArcButton
            {
                Content = request.CancelText,
                Variant = ArcButtonVariant.Ghost
            };
            cancel.Click += (_, _) => Complete(request.CancelResult);
            actions.Children.Add(cancel);
        }

        if (!string.IsNullOrWhiteSpace(request.SecondaryText))
        {
            var secondary = new ArcButton
            {
                Content = request.SecondaryText,
                Variant = ArcButtonVariant.Secondary
            };
            secondary.Click += (_, _) => Complete(request.SecondaryResult);
            actions.Children.Add(secondary);
        }

        var primary = new ArcButton
        {
            Content = request.PrimaryText,
            Variant = request.IsDestructive ? ArcButtonVariant.Danger : ArcButtonVariant.Primary
        };
        primary.Click += (_, _) => Complete(request.PrimaryResult);
        actions.Children.Add(primary);

        dialog.Footer = actions;
        dialog.Closed += (_, _) =>
        {
            // A backdrop press or Escape closes the dialog without a button click: treat it as cancel.
            completion.TrySetResult(request.CancelResult ?? default!);
            _overlay.Children.Remove(dialog);
        };

        _overlay.Children.Add(dialog);
        dialog.IsOpen = true;

        using var registration = cancellationToken.Register(() =>
        {
            completion.TrySetCanceled(cancellationToken);
            Dispatcher.UIThread.Post(() =>
            {
                dialog.IsOpen = false;
                _overlay.Children.Remove(dialog);
            });
        });

        try
        {
            return await completion.Task.ConfigureAwait(true);
        }
        finally
        {
            _overlay.Children.Remove(dialog);
        }

        void Complete(TResult? result)
        {
            completion.TrySetResult(result ?? default!);
            dialog.IsOpen = false;
        }
    }
}
