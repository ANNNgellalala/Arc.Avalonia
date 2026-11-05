using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Avatar's completeness preview: all five sizes, photo and initials sources, round and square shapes and
/// the two status dots. Declared keys match Docs/avatar.json.
/// </summary>
public sealed class AvatarPage : ArcItemPage
{
    private const string Maya = "avares://Arc.Avalonia.Docs/Assets/portrait-maya.png";
    private const string Sara = "avares://Arc.Avalonia.Docs/Assets/portrait-sara.png";
    private const string Leo = "avares://Arc.Avalonia.Docs/Assets/portrait-leo.png";

    public override string ItemSlug => "avatar";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-xs", "size-sm", "size-md", "size-lg", "size-xl",
        "variant-photo", "variant-initials", "shape-round", "shape-square",
        "status-online", "status-offline"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Sized(ArcAvatarSize.Xs),
            Sized(ArcAvatarSize.Sm),
            Sized(ArcAvatarSize.Md),
            Sized(ArcAvatarSize.Lg),
            Sized(ArcAvatarSize.Xl)));

        panel.Children.Add(Row("形状与来源",
            new ArcAvatar { DisplayName = "Maya Chen", Src = Maya, Size = ArcAvatarSize.Md, Shape = ArcAvatarShape.Round },
            new ArcAvatar { DisplayName = "Leo Park", Size = ArcAvatarSize.Md, Shape = ArcAvatarShape.Round },
            new ArcAvatar { DisplayName = "Sara Ruiz", Src = Sara, Size = ArcAvatarSize.Md, Shape = ArcAvatarShape.Square },
            new ArcAvatar { DisplayName = "Tom Hale", Size = ArcAvatarSize.Md, Shape = ArcAvatarShape.Square }));

        panel.Children.Add(Row("状态",
            new ArcAvatar { DisplayName = "Sara Ruiz", Src = Sara, Size = ArcAvatarSize.Md, Status = ArcAvatarStatus.Online },
            new ArcAvatar { DisplayName = "Ines Ma", Size = ArcAvatarSize.Md, Status = ArcAvatarStatus.Offline },
            new ArcAvatar
            {
                DisplayName = "Leo Park",
                Src = Leo,
                Size = ArcAvatarSize.Lg,
                Shape = ArcAvatarShape.Square,
                Status = ArcAvatarStatus.Online
            }));

        return panel;
    }

    private static ArcAvatar Sized(ArcAvatarSize size) => new()
    {
        DisplayName = "Maya Chen",
        Src = Maya,
        Size = size
    };

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var child in children)
        {
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
