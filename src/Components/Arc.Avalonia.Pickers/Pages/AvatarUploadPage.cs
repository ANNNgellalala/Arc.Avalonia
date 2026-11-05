using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>
/// Avatar upload's completeness preview: every state is shown at once (empty, photo, uploading, success,
/// failure, drag-over and the square silhouette) plus the in-place cropper. Declared state keys match
/// Docs/avatar-upload.json so the audit can compare the two. All states share one generated sample portrait,
/// so the preview is deterministic and never touches the file system.
/// </summary>
public sealed class AvatarUploadPage : ArcItemPage
{
    public override string ItemSlug => "avatar-upload";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-ready", "state-photo", "state-uploading", "state-success",
        "state-error", "state-drag-over", "state-cropping", "variant-square"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var photo = ArcSamplePhoto.Create(640);

        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row(
            Card("空态", "没有照片时显示姓名首字母与上传提示", Create(null, "Ada Lovelace")),
            Card("已有照片", "显示照片以及 Replace 与 Remove", Create(photo, "Ada Lovelace"))));

        var uploading = Create(photo, "Ada Lovelace");
        uploading.Status = ArcAvatarUploadStatus.Uploading;
        uploading.Progress = 0.62;

        var success = Create(photo, "Ada Lovelace");
        success.Status = ArcAvatarUploadStatus.Success;

        panel.Children.Add(Row(
            Card("上传中", "Status=Uploading，Progress=0.62", uploading),
            Card("成功", "Status=Success，头像外圈与角标落定", success)));

        var failed = Create(photo, "Ada Lovelace");
        failed.Status = ArcAvatarUploadStatus.Error;
        failed.Progress = 0.62;
        failed.ErrorMessage = "The connection dropped at 62%. Your photo is unchanged.";

        var dragging = Create(photo, "Ada Lovelace");
        dragging.IsDragOver = true;

        panel.Children.Add(Row(
            Card("失败", "Status=Error，保留照片并给出错误与重试", failed),
            Card("拖拽悬停", "IsDragOver=true，头像显示投放提示", dragging)));

        var square = Create(photo, "Ada Lovelace");
        square.Shape = ArcAvatarShape.Square;

        panel.Children.Add(Row(
            Card("方形剪影", "Shape=Square，用于工作区 Logo", square)));

        var crop = Create(photo, "Ada Lovelace");
        crop.BeginCrop(photo);
        crop.CropZoom = 1.35;

        panel.Children.Add(Card("裁剪中", "BeginCrop 后展开 248 px 裁剪台与缩放控制", crop, width: 640));

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

    private static Control Card(string title, string note, ArcAvatarUpload upload, double width = 380)
    {
        upload.Width = width;
        upload.HorizontalAlignment = HorizontalAlignment.Left;
        return DemoUi.Card(title, note, upload);
    }

    private static ArcAvatarUpload Create(IImage? source, string name) => new()
    {
        Source = source,
        DisplayName = name,
        Label = "Profile photo",
        Hint = "PNG, JPG or WebP, up to 8 MB"
    };
}
