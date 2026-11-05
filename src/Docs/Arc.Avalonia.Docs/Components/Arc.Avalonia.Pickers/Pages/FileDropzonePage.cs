using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// File dropzone's completeness preview: the resting target, a target with ready, uploading and failed rows,
/// the drag-over state and a single-file variant. Declared state keys match Docs/file-dropzone.json so the
/// audit can compare the two.
/// </summary>
public sealed class FileDropzonePage : ArcItemPage
{
    public override string ItemSlug => "file-dropzone";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-empty", "variant-files", "state-drag-over", "variant-single"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("空投放区", "只有标题、说明和类型/大小提示", Empty()),
            Card("带文件列表", "同时显示就绪、上传中与失败三种行", Files())));

        panel.Children.Add(Row(
            Card("拖拽悬停", "IsDragOver=true，虚线变成强调色", Dragging()),
            Card("单文件", "Multiple=false、MaxFiles=1", Single())));

        return panel;
    }

    private static Control Row(params Control[] cards)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var card in cards)
        {
            card.Margin = new Thickness(0, 0, 16, 0);
            row.Children.Add(card);
        }

        return row;
    }

    private static Control Card(string title, string note, ArcFileDropzone zone)
    {
        zone.Width = 380;
        return DemoUi.Card(title, note, zone);
    }

    private static ArcFileDropzone Empty() => new()
    {
        Label = "Add attachments",
        Accept = ".pdf,image/*",
        MaxFiles = 3,
        MaxSize = 10 * 1024 * 1024,
        Note = "PDF and images, up to 10 MB each"
    };

    private static ArcFileDropzone Files() => new()
    {
        Label = "Add attachments",
        Accept = ".pdf,image/*",
        MaxFiles = 5,
        MaxSize = 10 * 1024 * 1024,
        Note = "PDF and images, up to 10 MB each",
        DefaultItems = new[]
        {
            new ArcFileDropzoneItem { Id = "guide", Name = "brand-guide.pdf", Size = 2_411_724 },
            new ArcFileDropzoneItem
            {
                Id = "hero", Name = "hero-shot.png", Size = 1_204_224,
                Status = ArcFileDropzoneStatus.Uploading, Progress = 62
            },
            new ArcFileDropzoneItem
            {
                Id = "contract", Name = "contract-final-signed.pdf", Size = 18_400_000,
                Status = ArcFileDropzoneStatus.Failed, Error = "Larger than 10 MB"
            }
        }
    };

    private static ArcFileDropzone Dragging() => new()
    {
        Label = "Drop to attach",
        DropLabel = "Release to add images",
        Accept = "image/*",
        IsDragOver = true,
        Note = "PNG, JPG or WebP"
    };

    private static ArcFileDropzone Single() => new()
    {
        Label = "Add a logo",
        Description = "One file only",
        Accept = ".svg,image/*",
        Multiple = false,
        MaxFiles = 1,
        Note = "SVG or a square image, up to 2 MB"
    };
}
