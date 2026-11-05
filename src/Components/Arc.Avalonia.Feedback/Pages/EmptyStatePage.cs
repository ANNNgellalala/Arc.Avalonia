using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Empty state's completeness preview: icon with primary and secondary actions, icon with one action and the
/// minimal form without icon or buttons. Declared keys match Docs/empty-state.json.
/// </summary>
public sealed class EmptyStatePage : ArcItemPage
{
    public override string ItemSlug => "empty-state";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-icon", "variant-actions", "variant-secondary-action", "variant-minimal"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(new TextBlock { Text = "图标与出口", Classes = { "arc-caption" } });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Top
        };

        row.Children.Add(Wrap(new ArcEmptyState
        {
            Icon = "search",
            Title = "No matches",
            Description = "Try a shorter search, or clear the filters to see everything again.",
            Action = new ArcButton { Content = "Clear filters", Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm },
            SecondaryAction = new ArcButton { Content = "Search tips", Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm },
            Label = "No search results"
        }));

        row.Children.Add(Wrap(new ArcEmptyState
        {
            Icon = "folder",
            Title = "No projects yet",
            Description = "Create the first project to get started.",
            Action = new ArcButton { Content = "Create project", Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm }
        }));

        row.Children.Add(Wrap(new ArcEmptyState
        {
            Icon = null,
            Title = "Nothing to show",
            Description = "This board has no cards yet."
        }));

        panel.Children.Add(row);
        return panel;
    }

    private static Control Wrap(ArcEmptyState empty) => new ArcCard
    {
        Width = 300,
        Padding = new Thickness(4),
        Content = empty
    };
}
