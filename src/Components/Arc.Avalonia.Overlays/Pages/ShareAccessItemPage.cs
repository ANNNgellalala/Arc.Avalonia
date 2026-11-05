using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Overlays.Controls;

namespace Arc.Avalonia.Overlays.Pages;

/// <summary>
/// Share access' completeness preview: one page-hosted panel that presents the invite chips (valid and
/// invalid), the pending member, the open role menu, the link-access row and the Done button. The states are
/// programmed once the template is ready so a still shows them without any clicks. Declared state keys match
/// Docs/share-access.json so the audit can compare the two.
/// </summary>
public sealed class ShareAccessItemPage : ArcItemPage
{
    public override string ItemSlug => "share-access";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "layout-invite", "state-invalid", "state-pending", "state-role-menu", "layout-access", "layout-done"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new ArcShareAccess
        {
            Title = "Q3 launch plan",
            Label = "Share",
            Width = 424,
            ShowDone = true,
            SelfEmail = "maya@acme.com",
            LinkAccess = ArcShareLinkAccess.Anyone,
            LinkRole = "viewer",
            LinkUrl = "https://acme.com/d/q3-launch-plan",
            InviteRole = "editor",
            People = new List<ArcSharePerson>
            {
                new() { DisplayName = "Maya Chen", Email = "maya@acme.com", Role = "owner" },
                new() { DisplayName = "Nathan Cole", Email = "nathan@acme.com", Role = "editor" },
                new() { DisplayName = "Sam Rivera", Email = "sam@acme.com", Role = "commenter", IsPending = true },
                new() { DisplayName = "Owen Park", Email = "owen@acme.com", Role = "viewer" }
            },
            Suggestions = new List<ArcShareSuggestion>
            {
                new("Dana Whitfield", "dana@acme.com", "DW"),
                new("Owen Park", "owen@acme.com", "OP"),
                new("Marcus Bell", "marcus@acme.com", "MB")
            }
        };

        // The chips and the role menu live inside the panel's own overlay canvas, so they are presented once
        // the template exists and then stay put for the screenshot.
        var presented = false;
        panel.AttachedToVisualTree += (_, _) =>
        {
            if (presented)
            {
                return;
            }

            presented = true;
            Dispatcher.UIThread.Post(() =>
            {
                panel.AddChip("dana@acme.com");
                panel.AddChip("mayaatacme");
                panel.OpenRoleMenu("nathan@acme.com");
            }, DispatcherPriority.Loaded);
        };

        var stack = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left };
        stack.Children.Add(panel);
        stack.Children.Add(DemoUi.Caption(
            "合法 chip 与无效 chip 并排；Nathan 的角色菜单展开着，Sam 带着 Pending 徽章；底部是 Anyone 链接访问行与 Done 按钮。"));
        return stack;
    }
}
