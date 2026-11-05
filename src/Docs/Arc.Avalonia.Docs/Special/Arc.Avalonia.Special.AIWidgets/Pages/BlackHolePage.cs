using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.AIWidgets.Controls;

namespace Arc.Avalonia.Special.AIWidgets.Pages;

/// <summary>
/// Catalog page for the black hole delete transition. Three instances show an idle list, two selected rows and the
/// settled result right after a delete (with Undo); the delete itself runs from code so the "after" frame is static.
/// </summary>
public sealed class BlackHolePage : ArcItemPage
{
    public override string ItemSlug => "black-hole";

    public override bool DemoOnCanvas => true;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-idle", "state-selected", "state-deleted"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var selected = new ArcBlackHole
        {
            Title = "Inbox",
            Items = Inbox()
        };
        selected.Select("2", "5");

        var idle = new ArcBlackHole
        {
            Title = "Inbox",
            Items = Inbox()
        };

        var deleted = new ArcBlackHole
        {
            Title = "Inbox",
            Items = Inbox()
        };

        // Run the real transition once the list is attached and loaded: with reduced motion (the screenshot
        // baseline) it settles in the same frame, and with motion on it plays the spiral and then rests deleted.
        deleted.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(
            () =>
            {
                deleted.Select("2", "5");
                deleted.Delete();
            },
            DispatcherPriority.Loaded);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 16,
            RowSpacing = 16
        };

        var top = WithCaption("选中两行：复选框取强调色，按钮变成 Delete 2", selected);
        Grid.SetColumnSpan(top, 2);
        grid.Children.Add(top);

        var bottomLeft = WithCaption("未选中：Delete 禁用，状态行提示先选行", idle);
        Grid.SetRow(bottomLeft, 1);
        grid.Children.Add(bottomLeft);

        var bottomRight = WithCaption("删除完成：行消失、状态行变强调色，出现 Undo", deleted);
        Grid.SetColumn(bottomRight, 1);
        Grid.SetRow(bottomRight, 1);
        grid.Children.Add(bottomRight);

        return grid;
    }

    private static List<ArcBlackHoleItem> Inbox() => new()
    {
        new() { Id = "1", Subject = "Weekly metrics digest", From = "Arc analytics", Time = "09:41" },
        new() { Id = "2", Subject = "Re: motion review notes", From = "Rowan Pike", Time = "09:12" },
        new() { Id = "3", Subject = "Invoice 2041 is ready", From = "Billing", Time = "08:50" },
        new() { Id = "4", Subject = "Deploy finished on staging", From = "Pipeline", Time = "08:30" },
        new() { Id = "5", Subject = "Old thread: pricing page copy", From = "Mina Okafor", Time = "08:02" },
        new() { Id = "6", Subject = "Your weekly calendar", From = "Calendar", Time = "07:58" }
    };

    private static Control WithCaption(string caption, Control content)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(Sv.Caption(caption));
        stack.Children.Add(content);
        return stack;
    }

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Deleting or archiving a handful of selected rows, cards or files, with an Undo right after.",
        "Emptying a trash, clearing notifications or dismissing a batch where the removal should feel final.",
        "A demo or changelog where the delete itself is worth showing once."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "High frequency deletes such as clearing a single chip; use a quick height collapse.",
        "Irreversible actions without Undo: the spectacle must not stand in for a confirmation."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Up and Down move between rows, Space toggles the selection, Delete runs the transition and Control Z undoes it.",
        "The effect is visual only: announce the result yourself, as the demo does with a polite status line and an Undo button.",
        "The delete control disables itself while a transition runs, so a double press cannot delete twice."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Rows fall along a spiral: points nearer the hole start earlier, so the inner edge falls faster and the row stretches into an arm.",
        "The hole opens with a slight overshoot, grows a little as it eats, then collapses and sends out one soft ring.",
        "Undo plays the same path backwards; reduced motion fades the rows out in 200 ms and fades them back in."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public black-hole documentation. Nothing is painted to a texture and there is no gravitational lensing shader: the rows are " +
        "drawn by hand, so the bend is a short vertical and horizontal displacement near the hole, the fall is a logarithmic spiral on one clock, and the " +
        "shockwave is a stroked ring. Only the visible list is animated, as the reference also recommends.";
}
