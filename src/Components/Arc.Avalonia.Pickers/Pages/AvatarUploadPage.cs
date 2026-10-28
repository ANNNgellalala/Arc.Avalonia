using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Pickers, part six: the avatar upload. Every state is shown at once (empty, photo, uploading, success,
/// failure, drag-over and the square silhouette) plus a live cropper, so the in-place crop and the progress
/// ring can be inspected without touching the file system.
/// </summary>
public sealed class AvatarUploadPage : ArcPage
{
    public override string Title => "Avatar upload";

    public override string Group => "Pickers";

    public override int Order => 506;

    public override string Description =>
        "Click or drop a photo onto the avatar to crop it in place with drag and zoom. It uploads through a " +
        "progress ring and settles, with clear errors and retry.";

    public override Control Build(ArcDemoContext context)
    {
        var photo = ArcSamplePhoto.Create(640);

        var empty = CreateControl(null, "Ada Lovelace");
        var filled = CreateControl(photo, "Ada Lovelace");
        var uploading = CreateControl(photo, "Ada Lovelace");
        uploading.Status = ArcAvatarUploadStatus.Uploading;
        uploading.Progress = 0.62;

        var success = CreateControl(photo, "Ada Lovelace");
        success.Status = ArcAvatarUploadStatus.Success;

        var failed = CreateControl(photo, "Ada Lovelace");
        failed.Status = ArcAvatarUploadStatus.Error;
        failed.Progress = 0.62;
        failed.ErrorMessage = "The connection dropped at 62%. Your photo is unchanged.";

        var dragging = CreateControl(photo, "Ada Lovelace");
        dragging.IsDragOver = true;

        var crop = CreateControl(photo, "Ada Lovelace");
        crop.Width = 640;
        crop.BeginCrop(photo);
        crop.CropZoom = 1.35;

        var states = DemoUi.Row(
            State("Empty, initials fallback", empty),
            State("Photo, with Replace and Remove", filled),
            State("Uploading, 62% reported", uploading),
            State("Success, settled", success),
            State("Error, keep the crop and retry", failed),
            State("Drag over, drop to replace", dragging));

        var cropNote = new StackPanel
        {
            Spacing = 8,
            Width = 280,
            Children =
            {
                new TextBlock { Text = "While cropping", Classes = { "arc-label" } },
                Note("The avatar grows into a 248 px stage with a 184 px circular mask; the overflow is dimmed and brightens while you drag."),
                Note("Drag pans the photo. The wheel zooms towards the pointer, and the plus and minus buttons step the zoom."),
                Note("Arrow keys nudge by 2% of the circle, Shift makes it 10%, 0 resets, Enter saves and Escape cancels."),
                Note("Desktop Avalonia has no pinch gesture, so pinch zoom is the wheel and the buttons. The zoom slider of the original is replaced by the same pair."),
                Note("Saving renders the circle to a 512 px square through RenderTargetBitmap, then runs the upload."),
                Note("Shape=\"Square\" swaps the circle for a 26 px panel radius, for workspace logos.")
            }
        };

        var cropBlock = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                crop,
                DemoUi.Caption("Live: press Save photo and the deterministic 14-step upload runs around the avatar.")
            }
        };

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Avatar upload",
                    "For profile and team settings, where a person sets a photo and should see the crop and the upload " +
                    "in place. All states share one generated sample portrait; pick or drop a real PNG, JPG or WebP to " +
                    "crop your own.",
                    states),

                DemoUi.Section(
                    "Crop in place",
                    "The avatar becomes the crop stage, so the photo never leaves the place it will live. Saving crops " +
                    "through a RenderTargetBitmap, then the progress ring fills around the avatar.",
                    DemoUi.Row(cropBlock, cropNote))
            }
        };
    }

    private static ArcAvatarUpload CreateControl(IImage? source, string name) => new()
    {
        Source = source,
        DisplayName = name,
        Width = 300,
        HorizontalAlignment = HorizontalAlignment.Left
    };

    private static Control State(string caption, Control content)
    {
        var panel = new StackPanel { Spacing = 8, Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(DemoUi.Caption(caption));
        panel.Children.Add(content);
        return panel;
    }

    private static Control Note(string text) => new TextBlock
    {
        Text = "\u00b7 " + text,
        Classes = { "arc-caption" },
        TextWrapping = TextWrapping.Wrap
    };
}

