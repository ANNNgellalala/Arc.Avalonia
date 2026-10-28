using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Pickers, part four: the file dropzone and the signature pad. Both show their resting and filled states,
/// plus the dropzone's drag-over styling.
/// </summary>
public sealed class FilesAndInkPage : ArcPage
{
    public override string Title => "Files and ink";

    public override string Group => "Pickers";

    public override int Order => 503;

    public override string Description =>
        "A generous dashed drop target with a file list, and a signature pad with speed-thinned ink, undo, " +
        "clear and a transparent PNG export.";

    public override Control Build(ArcDemoContext context)
    {
        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "File dropzone",
                    "Drop files or press the target to use the system picker. Type and size limits are enforced per row, and the dash turns accent while files hover.",
                    BuildDropzones()),

                DemoUi.Section(
                    "Signature pad",
                    "Ink thins as the pen speeds up. Control Z undoes, Control Shift Z redoes, Delete clears, and SavePng writes a transparent PNG at three times the pad resolution.",
                    BuildSignaturePad())
            }
        };
    }

    private static Control BuildDropzones()
    {
        var empty = new ArcFileDropzone
        {
            Width = 380,
            Label = "Add attachments",
            Accept = ".pdf,image/*",
            MaxFiles = 3,
            MaxSize = 10 * 1024 * 1024
        };

        var files = new ArcFileDropzone
        {
            Width = 380,
            Label = "Add attachments",
            Accept = ".pdf,image/*",
            MaxFiles = 5,
            MaxSize = 10 * 1024 * 1024,
            Note = "PDF and images, up to 10 MB each",
            DefaultItems = new[]
            {
                new ArcFileDropzoneItem { Id = "guide", Name = "brand-guide.pdf", Size = 2_411_724 },
                new ArcFileDropzoneItem { Id = "hero", Name = "hero-shot.png", Size = 1_204_224, Status = ArcFileDropzoneStatus.Uploading, Progress = 62 },
                new ArcFileDropzoneItem { Id = "contract", Name = "contract-final-signed.pdf", Size = 18_400_000, Status = ArcFileDropzoneStatus.Failed, Error = "Larger than 10 MB" }
            }
        };

        var dragging = new ArcFileDropzone
        {
            Width = 260,
            Label = "Drop to attach",
            Accept = "image/*",
            IsDragOver = true,
            Note = "Release to add images"
        };

        return DemoUi.Row(empty, files, dragging);
    }

    private static Control BuildSignaturePad()
    {
        var pad = new ArcSignaturePad
        {
            Width = 600,
            HorizontalAlignment = HorizontalAlignment.Left,
            Signer = "Ada Lovelace",
            InkColor = ArcInkColor.Violet,
            InkWidth = ArcInkWidth.Medium,
            FileName = "ada-signature"
        };
        pad.LoadStrokes(PickerDemo.SampleSignature());

        var column = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(pad);
        column.Children.Add(DemoUi.Caption(
            "Sample ink restored through LoadStrokes, the same data a form would persist and replay."));
        return column;
    }
}
