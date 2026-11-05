using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Presence stack's completeness preview: the three sizes, the rested stack, the fanned stack with names and
/// the overflow caption. Declared keys match Docs/presence-stack.json.
/// </summary>
public sealed class PresenceStackPage : ArcItemPage
{
    private const string Maya = "avares://Arc.Avalonia.Feedback/Assets/portrait-maya.png";
    private const string Sara = "avares://Arc.Avalonia.Feedback/Assets/portrait-sara.png";
    private const string Leo = "avares://Arc.Avalonia.Feedback/Assets/portrait-leo.png";

    public override string ItemSlug => "presence-stack";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "variant-rested", "variant-fanned", "variant-overflow"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 26, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("尺寸",
            Column("Sm", new ArcPresenceStack
            {
                Members = People(3),
                Max = 3,
                Size = ArcPresenceSize.Sm,
                Label = "Reviewers here now"
            }),
            Column("Md", new ArcPresenceStack
            {
                Members = People(4),
                Max = 4,
                Size = ArcPresenceSize.Md,
                Label = "People here now"
            }),
            Column("Lg", new ArcPresenceStack
            {
                Members = People(4),
                Max = 4,
                Size = ArcPresenceSize.Lg,
                Label = "Call roster"
            })));

        panel.Children.Add(Row("形态",
            Column("叠放", new ArcPresenceStack
            {
                Members = People(4),
                Max = 4,
                Size = ArcPresenceSize.Md,
                Label = "People here now"
            }),
            Column("展开并显示姓名", new ArcPresenceStack
            {
                Members = People(4),
                Max = 4,
                Size = ArcPresenceSize.Md,
                ShowNames = true,
                Fanned = true,
                Label = "People here now"
            }),
            Column("溢出计数", new ArcPresenceStack
            {
                Members = People(6),
                Max = 2,
                Size = ArcPresenceSize.Md,
                ShowOthers = true,
                Label = "People here now"
            })));

        return panel;
    }

    private static List<PresenceMember> People(int count)
    {
        var people = new List<PresenceMember>
        {
            new() { Id = "maya", Name = "Maya Chen", Image = Maya, Status = ArcPresenceStatus.Available },
            new() { Id = "leo", Name = "Leo Park", Image = Leo, Status = ArcPresenceStatus.Busy },
            new() { Id = "jon", Name = "Jon Bell", Status = ArcPresenceStatus.Typing, IsTyping = true },
            new() { Id = "sara", Name = "Sara Ruiz", Image = Sara, Status = ArcPresenceStatus.Away },
            new() { Id = "tom", Name = "Tom Hale", Status = ArcPresenceStatus.Idle },
            new() { Id = "ines", Name = "Ines Ma", Status = ArcPresenceStatus.Available }
        };

        return people.GetRange(0, count);
    }

    private static Control Column(string label, Control content)
    {
        var column = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Left };
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
            child.Margin = new Thickness(0, 0, 36, 16);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
