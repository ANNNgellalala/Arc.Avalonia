using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Avatar group's completeness preview: the three common sizes, the +N overflow form and a group that shows
/// every member without overflow. Declared keys match Docs/avatar-group.json.
/// </summary>
public sealed class AvatarGroupPage : ArcItemPage
{
    private const string Maya = "avares://Arc.Avalonia.Feedback/Assets/portrait-maya.png";
    private const string Sara = "avares://Arc.Avalonia.Feedback/Assets/portrait-sara.png";

    public override string ItemSlug => "avatar-group";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg", "variant-overflow", "variant-all"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸与溢出",
            Column("Sm · 显示 2 个",
                new ArcAvatarGroup { Members = Team(5), Max = 2, Size = ArcAvatarSize.Sm, Label = "Editors" }),
            Column("Md · 显示全部",
                new ArcAvatarGroup { Members = Team(4), Max = 4, Size = ArcAvatarSize.Md, Label = "Team members" }),
            Column("Lg · 显示 3 个",
                new ArcAvatarGroup { Members = Team(5), Max = 3, Size = ArcAvatarSize.Lg, Label = "Reviewers" })));

        return panel;
    }

    private static ArcAvatarMember[] Team(int count)
    {
        var members = new[]
        {
            new ArcAvatarMember { Name = "Maya Chen", Src = Maya, Status = ArcAvatarStatus.Online },
            new ArcAvatarMember { Name = "Leo Park" },
            new ArcAvatarMember { Name = "Sara Ruiz", Src = Sara },
            new ArcAvatarMember { Name = "Tom Hale" },
            new ArcAvatarMember { Name = "Ines Ma" }
        };

        return members[..count];
    }

    private static Control Column(string label, Control content)
    {
        var column = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        column.Children.Add(content);
        return column;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 10 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 32, 12);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
