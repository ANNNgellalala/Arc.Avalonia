using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Services;

namespace Arc.Avalonia.Gallery.Pages;

/// <summary>
/// The M2-G3b service layer, driven from a normal page: confirm dialogs, deduplicated notifications and a
/// fake file picker injected through <see cref="ArcServiceProvider"/>. The dialogs and toasts land in the
/// <see cref="ArcWindow"/> overlay, not in this page's visual tree.
/// </summary>
public sealed class ServicesPage : ArcPage
{
    private TextBlock? _dialogResult;
    private TextBlock? _notificationStatus;
    private TextBlock? _pickerStatus;

    public override string Title => "Services";

    public override string Group => "P0 · Services";

    public override string? Description =>
        "Dialog, notification, navigation, file-picker and theme services behind an opt-in static registry. " +
        "No DI container: an unregistered service keeps the historical behaviour. This page runs the real " +
        "services from the ArcWindow overlay plus a fake file picker injected on demand.";

    public override int Order => 20040;

    public override Control Build(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 0 };

        panel.Children.Add(DemoUi.Section(
            "Registered services",
            "The ArcWindow shell registers defaults on construction; the app registers the file picker and " +
            "theme facade. Components resolve them through ArcServiceProvider.TryGet and fall back to their " +
            "legacy behaviour when nothing is registered.",
            RegisteredServicesList()));

        panel.Children.Add(DemoUi.Section(
            "Dialog service",
            "IDialogService serialises one dialog at a time and returns the chosen action's value. The dialog " +
            "traps focus, closes on Escape or backdrop press and restores focus when it closes.",
            DialogDemo()));

        panel.Children.Add(DemoUi.Section(
            "Notification service",
            "INotificationService deduplicates notifications that share a Key, so repeated status messages do " +
            "not stack; the toast host dismisses each one after its duration.",
            NotificationDemo()));

        panel.Children.Add(DemoUi.Section(
            "File picker service",
            "IFilePickerService is the seam the file controls use. The button below temporarily registers a " +
            "fake picker and calls it; a headless test does exactly the same.",
            FilePickerDemo()));

        return panel;
    }

    private Control RegisteredServicesList()
    {
        var types = ArcServiceProvider.RegisteredTypes;
        var lines = types.Count == 0
            ? "No service registered."
            : string.Join(Environment.NewLine, types.Select(type => "• " + type.Name));
        return DemoUi.Canvas(new TextBlock
        {
            Text = lines,
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap
        });
    }

    private Control DialogDemo()
    {
        _dialogResult = DemoUi.Caption("No dialog has been shown yet.");

        var confirm = new ArcButton
        {
            Content = "Delete project (destructive confirm)",
            Variant = ArcButtonVariant.Danger
        };
        confirm.Click += async (_, _) => await ShowConfirmAsync();

        var custom = new ArcButton
        {
            Content = "Rename dialog (string result)"
        };
        custom.Click += async (_, _) => await ShowRenameAsync();

        return DemoUi.Column(
            DemoUi.Row(confirm, custom),
            _dialogResult);
    }

    private async Task ShowConfirmAsync()
    {
        var dialogs = ArcServiceProvider.Get<IDialogService>();
        if (dialogs is null)
        {
            SetText(_dialogResult, "No IDialogService registered.");
            return;
        }

        var confirmed = await dialogs.ConfirmAsync(new ArcConfirmRequest
        {
            Title = "Delete this project?",
            Message = "Everyone with access loses the workspace. This cannot be undone.",
            ConfirmText = "Delete project",
            IsDestructive = true
        });
        SetText(_dialogResult, confirmed ? "ConfirmAsync → true (deleted)" : "ConfirmAsync → false (cancelled)");
    }

    private async Task ShowRenameAsync()
    {
        var dialogs = ArcServiceProvider.Get<IDialogService>();
        if (dialogs is null)
        {
            SetText(_dialogResult, "No IDialogService registered.");
            return;
        }

        var name = await dialogs.ShowAsync(new ArcDialogRequest<string>
        {
            Title = "Rename project",
            Description = "The new name is visible to everyone on the team.",
            PrimaryText = "Rename",
            SecondaryText = "Keep current",
            SecondaryResult = "unchanged",
            CancelResult = "cancelled",
            PrimaryResult = "Renamed to “Atlas”"
        });
        SetText(_dialogResult, "ShowAsync<string> → " + name);
    }

    private Control NotificationDemo()
    {
        _notificationStatus = DemoUi.Caption("No notification has been shown yet.");

        var toast = new ArcButton { Content = "Show toast (key = save)", Variant = ArcButtonVariant.Primary };
        toast.Click += (_, _) => ShowToast();

        var duplicate = new ArcButton { Content = "Show same key again (deduplicated)" };
        duplicate.Click += (_, _) => ShowToast();

        var clear = new ArcButton { Content = "Clear notifications", Variant = ArcButtonVariant.Ghost };
        clear.Click += (_, _) => ClearNotifications();

        return DemoUi.Column(
            DemoUi.Row(toast, duplicate, clear),
            _notificationStatus);
    }

    private void ShowToast()
    {
        var notifications = ArcServiceProvider.Get<INotificationService>();
        if (notifications is null)
        {
            SetText(_notificationStatus, "No INotificationService registered.");
            return;
        }

        var shown = notifications.Show(new ArcNotification
        {
            Key = "save",
            Title = "Changes saved",
            Description = "The workspace is up to date.",
            Kind = ArcNotificationKind.Success
        });
        SetText(
            _notificationStatus,
            (shown ? "Shown." : "Deduplicated (key \"save\" is already visible).") +
            $" Active: {notifications.Active.Count}");
    }

    private void ClearNotifications()
    {
        var notifications = ArcServiceProvider.Get<INotificationService>();
        notifications?.Clear();
        SetText(_notificationStatus, "Cleared. Active: " + (notifications?.Active.Count ?? 0));
    }

    private Control FilePickerDemo()
    {
        _pickerStatus = DemoUi.Caption("No picker call yet.");

        var pick = new ArcButton { Content = "Pick files with a fake picker", Variant = ArcButtonVariant.Secondary };
        pick.Click += async (_, _) => await PickWithFakeAsync();

        return DemoUi.Column(DemoUi.Row(pick), _pickerStatus);
    }

    private async Task PickWithFakeAsync()
    {
        ArcServiceProvider.TryGet<IFilePickerService>(out var previous);
        var fake = new CountingFilePickerService();
        ArcServiceProvider.Register<IFilePickerService>(fake);
        try
        {
            var files = await fake.OpenFileAsync(new FilePickerOpenOptions
            {
                Title = "Fake picker",
                AllowMultiple = true
            });
            SetText(
                _pickerStatus,
                $"IFilePickerService fake called {fake.Calls} time(s) and returned {files.Count} file(s).");
        }
        finally
        {
            if (previous is not null)
            {
                ArcServiceProvider.Register<IFilePickerService>(previous);
            }
            else
            {
                ArcServiceProvider.Unregister<IFilePickerService>();
            }
        }
    }

    private static void SetText(TextBlock? target, string text)
    {
        if (target is not null)
        {
            target.Text = text;
        }
    }

    /// <summary>A headless stand-in: counts calls and returns no files, the same shape a test uses.</summary>
    private sealed class CountingFilePickerService : IFilePickerService
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<IStorageFile>> OpenFileAsync(
            FilePickerOpenOptions options,
            TopLevel? owner = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<IStorageFile>>(Array.Empty<IStorageFile>());
        }

        public Task<IStorageFile?> SaveFileAsync(
            FilePickerSaveOptions options,
            TopLevel? owner = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IStorageFile?>(null);
        }

        public Task<IReadOnlyList<IStorageFolder>> PickFolderAsync(
            FolderPickerOpenOptions options,
            TopLevel? owner = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<IStorageFolder>>(Array.Empty<IStorageFolder>());
        }
    }
}
