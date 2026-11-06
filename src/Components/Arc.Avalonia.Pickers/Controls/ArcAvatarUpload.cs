using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>Avatar silhouette. Round is the default; square keeps the panel radius.</summary>
public enum ArcAvatarShape
{
    Round,
    Square
}

/// <summary>The states an avatar upload moves through.</summary>
public enum ArcAvatarUploadStatus
{
    Ready,
    Cropping,
    Uploading,
    Success,
    Error
}

/// <summary>Carries the square image produced by the cropper.</summary>
public sealed class ArcAvatarCropEventArgs : RoutedEventArgs
{
    public ArcAvatarCropEventArgs()
    {
    }

    public ArcAvatarCropEventArgs(RoutedEvent routedEvent) : base(routedEvent)
    {
    }

    public ArcAvatarCropEventArgs(IImage image) => Image = image;

    /// <summary>The cropped square at <see cref="ArcAvatarUpload.OutputSize"/>.</summary>
    public IImage Image { get; init; } = null!;
}

/// <summary>
/// Arc's avatar upload, reimplemented from the public documentation: click or drop a photo and the avatar
/// becomes a circular crop stage with the overflow dimmed; drag pans, the wheel, the plus/minus buttons and
/// the keys zoom, and saving crops through a <see cref="RenderTargetBitmap"/> into the avatar while a progress
/// ring fills around it. Failures keep the pending photo inset with a retry action.
///
/// Platform notes: desktop Avalonia has no pinch gesture, so pinch zoom is replaced by the wheel, the zoom
/// buttons and the + / - keys; the zoom slider in the original is replaced by that pair of buttons. Recents,
/// progress and errors are in-memory, and the cropper works on a square stage rather than a free crop.
/// </summary>
[TemplatePart("PART_AvatarButton", typeof(Button))]
[TemplatePart("PART_AvatarBox", typeof(Grid))]
[TemplatePart("PART_PhotoClip", typeof(Border))]
[TemplatePart("PART_Photo", typeof(Image))]
[TemplatePart("PART_InitialsHost", typeof(Border))]
[TemplatePart("PART_Initials", typeof(TextBlock))]
[TemplatePart("PART_Hover", typeof(Border))]
[TemplatePart("PART_Drop", typeof(Border))]
[TemplatePart("PART_Pending", typeof(Border))]
[TemplatePart("PART_PendingPhoto", typeof(Image))]
[TemplatePart("PART_Ring", typeof(ArcAvatarRing))]
[TemplatePart("PART_Settle", typeof(Border))]
[TemplatePart("PART_Check", typeof(Border))]
[TemplatePart("PART_Label", typeof(TextBlock))]
[TemplatePart("PART_Name", typeof(TextBlock))]
[TemplatePart("PART_Hint", typeof(TextBlock))]
[TemplatePart("PART_Status", typeof(TextBlock))]
[TemplatePart("PART_UploadButton", typeof(ArcButton))]
[TemplatePart("PART_RemoveButton", typeof(ArcButton))]
[TemplatePart("PART_RetryButton", typeof(ArcButton))]
[TemplatePart("PART_CropPanel", typeof(Border))]
[TemplatePart("PART_Crop", typeof(ArcAvatarCropSurface))]
[TemplatePart("PART_ZoomValue", typeof(TextBlock))]
[TemplatePart("PART_ZoomOut", typeof(Button))]
[TemplatePart("PART_ZoomIn", typeof(Button))]
[TemplatePart("PART_Save", typeof(ArcButton))]
[TemplatePart("PART_Cancel", typeof(ArcButton))]
public class ArcAvatarUpload : TemplatedControl
{
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, IImage?>(nameof(Source));

    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, string?>(nameof(DisplayName));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, string?>(nameof(Label), "Profile photo");

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, string?>(nameof(Hint));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, double>(nameof(Size), 88d);

    public static readonly StyledProperty<ArcAvatarShape> ShapeProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, ArcAvatarShape>(nameof(Shape));

    public static readonly StyledProperty<long> MaxSizeBytesProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, long>(nameof(MaxSizeBytes), 5L * 1024 * 1024);

    public static readonly StyledProperty<int> OutputSizeProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, int>(nameof(OutputSize), 512);

    public static readonly StyledProperty<Func<Stream, IProgress<double>, CancellationToken, Task<string>>?> UploadAsyncProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, Func<Stream, IProgress<double>, CancellationToken, Task<string>>?>(nameof(UploadAsync));

    public static readonly StyledProperty<ArcAvatarUploadStatus> StatusProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, ArcAvatarUploadStatus>(nameof(Status));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, double>(nameof(Progress));

    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, string?>(nameof(ErrorMessage));

    public static readonly StyledProperty<bool> IsDragOverProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, bool>(nameof(IsDragOver));

    public static readonly StyledProperty<double> CropZoomProperty =
        AvaloniaProperty.Register<ArcAvatarUpload, double>(nameof(CropZoom), 1d);


    private Button? _avatar;
    private Grid? _avatarBox;
    private Border? _photoClip;
    private Image? _photo;
    private Border? _initialsHost;
    private TextBlock? _initials;
    private Border? _hover;
    private Border? _drop;
    private Border? _pendingLayer;
    private Image? _pendingPhoto;
    private ArcAvatarRing? _ring;
    private Border? _settle;
    private Border? _check;
    private TextBlock? _label;
    private TextBlock? _name;
    private TextBlock? _hint;
    private TextBlock? _status;
    private ArcButton? _uploadButton;
    private ArcButton? _removeButton;
    private ArcButton? _retryButton;
    private Border? _cropPanel;
    private ArcAvatarCropSurface? _crop;
    private TextBlock? _zoomValue;
    private Button? _zoomOut;
    private Button? _zoomIn;
    private ArcButton? _save;
    private ArcButton? _cancel;

    private Bitmap? _pending;
    private IImage? _cropOrigin;
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _settleTimer;
    private bool _pointerOver;
    private bool _syncingZoom;

    static ArcAvatarUpload()
    {
        SourceProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateState());
        DisplayNameProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateState());
        LabelProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateState());
        HintProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateState());
        SizeProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateGeometry());
        ShapeProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateGeometry());
        MaxSizeBytesProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateState());
        StatusProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateState());
        ProgressProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateState());
        ErrorMessageProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateState());
        IsDragOverProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.UpdateState());
        CropZoomProperty.Changed.AddClassHandler<ArcAvatarUpload>((control, _) => control.SyncCropZoom());
    }

    /// <summary>Current photo; null falls back to initials.</summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The person's name, used for the initials fallback.</summary>
    public string? DisplayName
    {
        get => GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    /// <summary>Heading beside the avatar and the group's accessible name.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Helper copy; defaults to the accepted types and the size limit.</summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>Avatar diameter in px while not cropping. Defaults to 88.</summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Round or square silhouette.</summary>
    public ArcAvatarShape Shape
    {
        get => GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    /// <summary>Largest accepted file in bytes; checked before decoding.</summary>
    public long MaxSizeBytes
    {
        get => GetValue(MaxSizeBytesProperty);
        set => SetValue(MaxSizeBytesProperty, value);
    }

    /// <summary>Side of the square image passed to <see cref="UploadAsync"/>. Defaults to 512.</summary>
    public int OutputSize
    {
        get => GetValue(OutputSizeProperty);
        set => SetValue(OutputSizeProperty, value);
    }

    /// <summary>
    /// The upload. Call <c>report</c> with 0 to 1 as bytes go out; resolve with the stored URL, or nothing to
    /// keep the local crop. Leave it null to run the deterministic simulated upload the demo uses.
    /// </summary>
    public Func<Stream, IProgress<double>, CancellationToken, Task<string>>? UploadAsync
    {
        get => GetValue(UploadAsyncProperty);
        set => SetValue(UploadAsyncProperty, value);
    }

    /// <summary>
    /// Current state. The control sets it while a person crops and uploads; it is writable so a host can
    /// restore a persisted state, and so the demo page can show every state side by side.
    /// </summary>
    public ArcAvatarUploadStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>Upload progress, 0 to 1.</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Message shown on the error state.</summary>
    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    /// <summary>True while a file hovers the avatar. Also driven by real drag events.</summary>
    public bool IsDragOver
    {
        get => GetValue(IsDragOverProperty);
        set => SetValue(IsDragOverProperty, value);
    }

    /// <summary>Crop scale while cropping; 1 fits the circle.</summary>
    public double CropZoom
    {
        get => GetValue(CropZoomProperty);
        set => SetValue(CropZoomProperty, Math.Clamp(value, 1, 4));
    }

    /// <summary>The most recent cropped square, before or after upload.</summary>
    public Bitmap? CroppedImage => _pending;

    /// <summary>Initials shown when there is no photo.</summary>
    public string Initials
    {
        get
        {
            var name = DisplayName?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return parts[0][..1].ToUpperInvariant();
            }

            return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
        }
    }

    /// <summary>Raised when the cropper resolves a square image, before the upload starts.</summary>
    public event EventHandler<ArcAvatarCropEventArgs>? CropConfirmed;

    /// <summary>Bubbled copy of <see cref="CropConfirmed"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcAvatarCropEventArgs> CropConfirmedEvent =
        RoutedEvent.Register<ArcAvatarUpload, ArcAvatarCropEventArgs>("CropConfirmed", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="CropConfirmed"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcAvatarCropEventArgs>? CropConfirmedRouted
    {
        add => AddHandler(CropConfirmedEvent, value);
        remove => RemoveHandler(CropConfirmedEvent, value);
    }

    private void RaiseCropConfirmed(IImage image)
    {
        var args = new ArcAvatarCropEventArgs(CropConfirmedEvent) { Image = image };
        CropConfirmed?.Invoke(this, args);
        RaiseEvent(args);
    }

    /// <summary>Raised when the upload resolves, with the stored URL when the host returned one.</summary>
    public event EventHandler<string?>? Uploaded;

    /// <summary>Raised with the message shown on the error state.</summary>
    public event EventHandler<string>? Failed;

    /// <summary>Raised after the photo is removed.</summary>
    public event EventHandler? Removed;

    /// <summary>Opens the system image picker. Same path as pressing the avatar.</summary>
    public async void PickPhoto()
    {
        if (Status == ArcAvatarUploadStatus.Cropping)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        var options = new FilePickerOpenOptions
        {
            Title = string.IsNullOrWhiteSpace(Label) ? "Profile photo" : Label,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" },
                    MimeTypes = new[] { "image/png", "image/jpeg", "image/webp" }
                }
            }
        };

        // An app may register IFilePickerService (a headless picker or a custom image portal); without one
        // the control keeps using the platform storage provider directly.
        if (ArcServiceProvider.TryGet<IFilePickerService>(out var picker) && picker is not null)
        {
            var picked = await picker.OpenFileAsync(options, top);
            if (picked.Count > 0)
            {
                await LoadFileAsync(picked[0]);
            }

            return;
        }

        if (top?.StorageProvider is null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(options);

        if (files.Count > 0)
        {
            await LoadFileAsync(files[0]);
        }
    }

    /// <summary>Opens the cropper for an image a host already has, for paste or camera capture paths.</summary>
    public void BeginCrop(IImage image, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        _pending = null;
        _cropOrigin = image;

        if (_crop is not null)
        {
            _crop.Image = image;
            _crop.ResetView();
        }

        SetCurrentValue(CropZoomProperty, 1d);
        SetCurrentValue(ErrorMessageProperty, null);
        SetCurrentValue(StatusProperty, ArcAvatarUploadStatus.Cropping);
        FocusCrop();
    }

    /// <summary>Runs the last cropped image through the upload again after a failure.</summary>
    public void RetryUpload()
    {
        _pending ??= Source as Bitmap;
        if (_pending is null)
        {
            return;
        }

        _ = StartUploadAsync();
    }

    /// <summary>Clears the photo and returns to initials.</summary>
    public void Remove()
    {
        _cts?.Cancel();
        _pending = null;
        SetCurrentValue(SourceProperty, null);
        SetCurrentValue(ErrorMessageProperty, null);
        SetCurrentValue(ProgressProperty, 0d);
        SetCurrentValue(StatusProperty, ArcAvatarUploadStatus.Ready);
        Removed?.Invoke(this, EventArgs.Empty);
        FocusUploadButton();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Detach();
        _avatar = e.NameScope.Find<Button>("PART_AvatarButton");
        _avatarBox = e.NameScope.Find<Grid>("PART_AvatarBox");
        _photoClip = e.NameScope.Find<Border>("PART_PhotoClip");
        _photo = e.NameScope.Find<Image>("PART_Photo");
        _initialsHost = e.NameScope.Find<Border>("PART_InitialsHost");
        _initials = e.NameScope.Find<TextBlock>("PART_Initials");
        _hover = e.NameScope.Find<Border>("PART_Hover");
        _drop = e.NameScope.Find<Border>("PART_Drop");
        _pendingLayer = e.NameScope.Find<Border>("PART_Pending");
        _pendingPhoto = e.NameScope.Find<Image>("PART_PendingPhoto");
        _ring = e.NameScope.Find<ArcAvatarRing>("PART_Ring");
        _settle = e.NameScope.Find<Border>("PART_Settle");
        _check = e.NameScope.Find<Border>("PART_Check");
        _label = e.NameScope.Find<TextBlock>("PART_Label");
        _name = e.NameScope.Find<TextBlock>("PART_Name");
        _hint = e.NameScope.Find<TextBlock>("PART_Hint");
        _status = e.NameScope.Find<TextBlock>("PART_Status");
        _uploadButton = e.NameScope.Find<ArcButton>("PART_UploadButton");
        _removeButton = e.NameScope.Find<ArcButton>("PART_RemoveButton");
        _retryButton = e.NameScope.Find<ArcButton>("PART_RetryButton");
        _cropPanel = e.NameScope.Find<Border>("PART_CropPanel");
        _crop = e.NameScope.Find<ArcAvatarCropSurface>("PART_Crop");
        _zoomValue = e.NameScope.Find<TextBlock>("PART_ZoomValue");
        _zoomOut = e.NameScope.Find<Button>("PART_ZoomOut");
        _zoomIn = e.NameScope.Find<Button>("PART_ZoomIn");
        _save = e.NameScope.Find<ArcButton>("PART_Save");
        _cancel = e.NameScope.Find<ArcButton>("PART_Cancel");

        if (_avatar is not null)
        {
            _avatar.Click += OnAvatarClick;
            _avatar.PointerEntered += OnAvatarPointerEntered;
            _avatar.PointerExited += OnAvatarPointerExited;
            _avatar.GotFocus += OnAvatarFocusChanged;
            _avatar.LostFocus += OnAvatarFocusChanged;
            DragDrop.SetAllowDrop(_avatar, true);
            _avatar.AddHandler(DragDrop.DragEnterEvent, OnDragOver);
            _avatar.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            _avatar.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _avatar.AddHandler(DragDrop.DropEvent, OnDrop);
            AutomationProperties.SetHelpText(_avatar, "Press to choose an image, or drop one here.");
        }

        if (_uploadButton is not null)
        {
            _uploadButton.Click += OnUploadClick;
        }

        if (_removeButton is not null)
        {
            _removeButton.Click += OnRemoveClick;
        }

        if (_retryButton is not null)
        {
            _retryButton.Click += OnRetryClick;
        }

        if (_crop is not null)
        {
            _crop.ConfirmRequested += OnCropConfirmed;
            _crop.CancelRequested += OnCropCancelled;
            _crop.ViewChanged += OnCropViewChanged;
        }

        if (_save is not null)
        {
            _save.Click += OnSaveClick;
        }

        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }

        if (_zoomOut is not null)
        {
            _zoomOut.Click += OnZoomOutClick;
        }

        if (_zoomIn is not null)
        {
            _zoomIn.Click += OnZoomInClick;
        }

        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }

        UpdateGeometry();
        UpdateState();
        SyncCropZoom();

        if (_crop is not null && _cropOrigin is not null)
        {
            _crop.Image = _cropOrigin;
            _crop.Zoom = Math.Clamp(CropZoom, 1, 4);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _cts?.Cancel();
        _settleTimer?.Stop();
        _settleTimer = null;
        Detach();
    }

    private void Detach()
    {
        if (_avatar is not null)
        {
            _avatar.Click -= OnAvatarClick;
            _avatar.PointerEntered -= OnAvatarPointerEntered;
            _avatar.PointerExited -= OnAvatarPointerExited;
            _avatar.GotFocus -= OnAvatarFocusChanged;
            _avatar.LostFocus -= OnAvatarFocusChanged;
            _avatar.RemoveHandler(DragDrop.DragEnterEvent, OnDragOver);
            _avatar.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            _avatar.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            _avatar.RemoveHandler(DragDrop.DropEvent, OnDrop);
        }

        if (_uploadButton is not null)
        {
            _uploadButton.Click -= OnUploadClick;
        }

        if (_removeButton is not null)
        {
            _removeButton.Click -= OnRemoveClick;
        }

        if (_retryButton is not null)
        {
            _retryButton.Click -= OnRetryClick;
        }

        if (_crop is not null)
        {
            _crop.ConfirmRequested -= OnCropConfirmed;
            _crop.CancelRequested -= OnCropCancelled;
            _crop.ViewChanged -= OnCropViewChanged;
        }

        if (_save is not null)
        {
            _save.Click -= OnSaveClick;
        }

        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }

        if (_zoomOut is not null)
        {
            _zoomOut.Click -= OnZoomOutClick;
        }

        if (_zoomIn is not null)
        {
            _zoomIn.Click -= OnZoomInClick;
        }
    }

    private void UpdateGeometry()
    {
        var size = Math.Max(24, Size);
        var radius = Shape == ArcAvatarShape.Square ? Math.Min(26, size / 2) : size / 2;
        var corner = new CornerRadius(radius);

        if (_avatarBox is not null)
        {
            _avatarBox.Width = size;
            _avatarBox.Height = size;
        }

        foreach (var border in new[] { _photoClip, _initialsHost, _pendingLayer })
        {
            if (border is not null)
            {
                border.CornerRadius = corner;
            }
        }

        if (_hover is not null)
        {
            _hover.CornerRadius = corner;
        }

        if (_drop is not null)
        {
            _drop.CornerRadius = corner;
        }

        if (_avatar is not null)
        {
            _avatar.CornerRadius = corner;
        }

        if (_ring is not null)
        {
            _ring.Width = size + 14;
            _ring.Height = size + 14;
        }

        if (_initials is not null)
        {
            _initials.FontSize = Math.Round(Math.Max(12, size * 0.34), 1);
        }

        if (_pendingPhoto is not null)
        {
            _pendingPhoto.Margin = new Thickness(Math.Round(size * 0.05));
        }

        if (_check is not null)
        {
            _check.CornerRadius = new CornerRadius(size * 0.16);
        }
    }

    private void UpdateState()
    {
        var hasPhoto = Source is not null;
        var pending = (IImage?)_pending ?? Source;
        var busy = Status is ArcAvatarUploadStatus.Uploading or ArcAvatarUploadStatus.Error;

        if (_photo is not null)
        {
            _photo.Source = Source;
            _photo.IsVisible = hasPhoto;
        }

        if (_photoClip is not null)
        {
            _photoClip.IsVisible = hasPhoto;
        }

        if (_initialsHost is not null)
        {
            _initialsHost.IsVisible = !hasPhoto;
        }

        if (_initials is not null)
        {
            _initials.Text = string.IsNullOrEmpty(Initials) ? "?" : Initials;
        }

        if (_label is not null)
        {
            _label.Text = string.IsNullOrWhiteSpace(Label) ? "Profile photo" : Label;
        }

        if (_name is not null)
        {
            _name.Text = DisplayName ?? string.Empty;
            _name.IsVisible = !string.IsNullOrWhiteSpace(DisplayName);
        }

        if (_hint is not null)
        {
            _hint.Text = string.IsNullOrWhiteSpace(Hint) ? BuildHint() : Hint;
        }

        if (_drop is not null)
        {
            _drop.IsVisible = IsDragOver;
        }

        if (_pendingLayer is not null)
        {
            _pendingLayer.IsVisible = busy && pending is not null;
        }

        if (_pendingPhoto is not null && pending is not null)
        {
            _pendingPhoto.Source = pending;
        }

        if (_ring is not null)
        {
            _ring.IsVisible = Status is ArcAvatarUploadStatus.Uploading or ArcAvatarUploadStatus.Error;
            _ring.Progress = Progress;
            _ring.IsIndeterminate = Status == ArcAvatarUploadStatus.Uploading && Progress <= 0.001;
            _ring.Stroke = ArcResources.Brush(Status == ArcAvatarUploadStatus.Error ? ArcTokens.Danger : ArcTokens.Accent);
            AutomationProperties.SetName(
                _ring,
                Status == ArcAvatarUploadStatus.Error
                    ? "Upload failed"
                    : $"Upload {Math.Round(Progress * 100)} percent");
        }

        if (_check is not null)
        {
            _check.IsVisible = Status == ArcAvatarUploadStatus.Success;
        }

        if (_settle is not null && Status != ArcAvatarUploadStatus.Success)
        {
            _settle.IsVisible = false;
            _settleTimer?.Stop();
            _settleTimer = null;
        }

        if (_status is not null)
        {
            var message = Status switch
            {
                ArcAvatarUploadStatus.Uploading => Progress > 0.001
                    ? $"Uploading {Math.Round(Progress * 100)}%"
                    : "Uploading",
                ArcAvatarUploadStatus.Success => "Photo updated",
                ArcAvatarUploadStatus.Error => ErrorMessage ?? "Upload failed. Try again.",
                _ => string.Empty
            };
            _status.Text = message;
            _status.IsVisible = message.Length > 0;
            _status.Classes.Set("danger", Status == ArcAvatarUploadStatus.Error);
        }

        if (_uploadButton is not null)
        {
            _uploadButton.Content = hasPhoto ? "Replace photo" : "Upload photo";
        }

        if (_retryButton is not null)
        {
            _retryButton.IsVisible = Status == ArcAvatarUploadStatus.Error;
        }

        if (_removeButton is not null)
        {
            _removeButton.IsVisible = hasPhoto;
        }

        if (_cropPanel is not null)
        {
            _cropPanel.IsVisible = Status == ArcAvatarUploadStatus.Cropping;
        }

        if (_avatar is not null)
        {
            AutomationProperties.SetName(_avatar, hasPhoto ? "Change profile photo" : "Upload profile photo");
        }

        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Profile photo" : Label!);

        var showHover = _pointerOver || (_avatar?.IsFocused ?? false);
        if (_hover is not null)
        {
            _hover.IsVisible = showHover && Status == ArcAvatarUploadStatus.Ready && !IsDragOver;
        }
    }

    private string BuildHint()
    {
        var limit = MaxSizeBytes > 0 ? ArcFileDropzone.FormatFileSize(MaxSizeBytes) : null;
        return limit is null ? "PNG, JPG or WebP." : $"PNG, JPG or WebP, up to {limit}.";
    }

    private void SyncCropZoom()
    {
        if (_syncingZoom || _crop is null)
        {
            return;
        }

        var zoom = Math.Clamp(CropZoom, 1, 4);
        if (Math.Abs(_crop.Zoom - zoom) > 0.001)
        {
            _crop.Zoom = zoom;
        }

        if (_zoomValue is not null)
        {
            _zoomValue.Text = $"{zoom:0.0}\u00d7";
        }
    }

    private void OnCropViewChanged(object? sender, EventArgs e)
    {
        if (_crop is null)
        {
            return;
        }

        _syncingZoom = true;
        try
        {
            SetCurrentValue(CropZoomProperty, _crop.Zoom);
        }
        finally
        {
            _syncingZoom = false;
        }

        if (_zoomValue is not null)
        {
            _zoomValue.Text = $"{_crop.Zoom:0.0}\u00d7";
        }
    }

    private void OnAvatarClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (Status == ArcAvatarUploadStatus.Cropping)
        {
            return;
        }

        PickPhoto();
    }

    private void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        PickPhoto();
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Remove();
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RetryUpload();
    }

    private void OnCropConfirmed(object? sender, EventArgs e) => ConfirmCrop();

    private void OnCropCancelled(object? sender, EventArgs e) => CancelCrop();

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ConfirmCrop();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CancelCrop();
    }

    private void OnZoomOutClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _crop?.ZoomTo((_crop.Zoom) / 1.15, null);
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _crop?.ZoomTo((_crop.Zoom) * 1.15, null);
    }

    private void OnAvatarPointerEntered(object? sender, PointerEventArgs e)
    {
        _pointerOver = true;
        UpdateState();
    }

    private void OnAvatarPointerExited(object? sender, PointerEventArgs e)
    {
        _pointerOver = false;
        UpdateState();
    }

    private void OnAvatarFocusChanged(object? sender, EventArgs e) => UpdateState();

    private void ConfirmCrop()
    {
        if (_crop is null || _crop.Image is null)
        {
            CancelCrop();
            return;
        }

        var cropped = _crop.RenderCrop(OutputSize);
        if (cropped is null)
        {
            CancelCrop();
            return;
        }

        _pending = cropped;
        _cropOrigin = null;
        _crop.Image = null;
        RaiseCropConfirmed(cropped);
        _ = StartUploadAsync();
    }

    private void CancelCrop()
    {
        _pending = null;
        _cropOrigin = null;
        if (_crop is not null)
        {
            _crop.Image = null;
        }

        SetCurrentValue(StatusProperty, ArcAvatarUploadStatus.Ready);
        SetCurrentValue(ErrorMessageProperty, null);
        FocusUploadButton();
    }

    private async Task StartUploadAsync()
    {
        if (_pending is null)
        {
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        SetCurrentValue(ErrorMessageProperty, null);
        SetCurrentValue(ProgressProperty, 0d);
        SetCurrentValue(StatusProperty, ArcAvatarUploadStatus.Uploading);

        try
        {
            string? url = null;
            using var stream = new MemoryStream();
#pragma warning disable CS0618
            _pending.Save(stream);
#pragma warning restore CS0618
            stream.Position = 0;

            if (UploadAsync is { } upload)
            {
                var reporter = new Progress<double>(value =>
                    SetCurrentValue(ProgressProperty, Math.Clamp(value, 0, 1)));
                url = await upload(stream, reporter, token);
            }
            else
            {
                // Deterministic simulation: fourteen steps, 90 ms apart, so the demo page is repeatable.
                const int steps = 14;
                for (var step = 1; step <= steps; step++)
                {
                    await Task.Delay(90, token);
                    SetCurrentValue(ProgressProperty, step / (double)steps);
                }
            }

            token.ThrowIfCancellationRequested();
            SetCurrentValue(SourceProperty, (IImage?)_pending);
            SetCurrentValue(StatusProperty, ArcAvatarUploadStatus.Success);
            Uploaded?.Invoke(this, url);
            PlaySettle();
        }
        catch (OperationCanceledException)
        {
            SetCurrentValue(StatusProperty, ArcAvatarUploadStatus.Ready);
        }
        catch (Exception exception)
        {
            var message = string.IsNullOrWhiteSpace(exception.Message)
                ? "Upload failed. The photo is unchanged. Try again."
                : exception.Message;
            SetCurrentValue(ErrorMessageProperty, message);
            SetCurrentValue(StatusProperty, ArcAvatarUploadStatus.Error);
            Failed?.Invoke(this, message);
        }
    }

    private void PlaySettle()
    {
        if (_settle is null || ArcThemeManager.ReducedMotion)
        {
            return;
        }

        _settle.IsVisible = true;
        _settle.Opacity = 1;
        _settle.RenderTransform = new ScaleTransform(1, 1);
        _settle.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        var started = DateTime.UtcNow;
        _settleTimer?.Stop();
        _settleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _settleTimer.Tick += (_, _) =>
        {
            var progress = Math.Clamp((DateTime.UtcNow - started).TotalMilliseconds / 480, 0, 1);
            if (_settle is null)
            {
                _settleTimer?.Stop();
                return;
            }

            _settle.Opacity = 1 - progress;
            _settle.RenderTransform = new ScaleTransform(1 + 0.12 * progress, 1 + 0.12 * progress);
            if (progress >= 1)
            {
                _settleTimer?.Stop();
                _settleTimer = null;
                _settle.IsVisible = false;
            }
        };
        _settleTimer.Start();
    }

    private void FocusUploadButton()
    {
        Dispatcher.UIThread.Post(() => _uploadButton?.Focus(), DispatcherPriority.Loaded);
    }

    private void FocusCrop()
    {
        Dispatcher.UIThread.Post(() => _crop?.Focus(), DispatcherPriority.Loaded);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (Status == ArcAvatarUploadStatus.Cropping)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.DataTransfer.TryGetFiles() is { Length: > 0 })
        {
            e.DragEffects = DragDropEffects.Copy;
            SetCurrentValue(IsDragOverProperty, true);
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        SetCurrentValue(IsDragOverProperty, false);
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        SetCurrentValue(IsDragOverProperty, false);
        var files = e.DataTransfer.TryGetFiles();
        e.Handled = true;
        var file = files?.OfType<IStorageFile>().FirstOrDefault();
        if (file is not null)
        {
            await LoadFileAsync(file);
        }
    }

    private async Task LoadFileAsync(IStorageFile file)
    {
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
        {
            ShowError("That file is not an image. Use a PNG, JPG or WebP.");
            return;
        }

        long size = 0;
        try
        {
            var properties = await file.GetBasicPropertiesAsync();
            size = (long)(properties.Size ?? 0);
        }
        catch
        {
            size = 0;
        }

        if (MaxSizeBytes > 0 && size > MaxSizeBytes)
        {
            ShowError(
                $"That image is {ArcFileDropzone.FormatFileSize(size)}. The limit is " +
                $"{ArcFileDropzone.FormatFileSize(MaxSizeBytes)}.");
            return;
        }

        try
        {
            await using var stream = await file.OpenReadAsync();
            var bitmap = new Bitmap(stream);
            BeginCrop(bitmap, file.Name);
        }
        catch
        {
            ShowError("That image could not be opened. Try another file.");
        }
    }

    private void ShowError(string message)
    {
        SetCurrentValue(ErrorMessageProperty, message);
        SetCurrentValue(StatusProperty, ArcAvatarUploadStatus.Error);
        Failed?.Invoke(this, message);
    }
}





