using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// User menu's completeness preview: the panel opened with its identity header, plan badge, destinations,
/// inline theme switch and sign out; a closed avatar trigger; and a closed trigger with the name and the
/// busy presence mark. Declared state keys match Docs/user-menu.json so the audit can compare the two lists.
/// </summary>
public sealed class UserMenuPage : ArcItemPage
{
    public override string ItemSlug => "user-menu";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "state-closed", "variant-name", "variant-presence-busy", "variant-plan-badge"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var open = BuildMenu(context, showName: false, status: ArcUserMenuStatus.Available, plan: "Pro", isOpen: true);
        var closed = BuildMenu(context, showName: false, status: ArcUserMenuStatus.None, plan: null, isOpen: false);
        var named = BuildMenu(context, showName: true, status: ArcUserMenuStatus.Busy, plan: null, isOpen: false);

        var right = new StackPanel { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Left };
        right.Children.Add(Captioned(TopBar(closed, 300, 108), "收起态：只有圆形头像入口，没有姓名。"));
        right.Children.Add(Captioned(TopBar(named, 300, 132), "带姓名：头像旁显示姓名与 chevron；忙碌状态用横杠标记，不只靠颜色。"));

        var row = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        row.Children.Add(Captioned(TopBar(open, 380, 370), "打开态：身份头部带 Plan 徽章，下方是目的地、内联主题开关与危险色退出登录。"));
        row.Children.Add(right);
        return row;
    }

    private static ArcUserMenu BuildMenu(
        ArcDemoContext context, bool showName, ArcUserMenuStatus status, string? plan, bool isOpen) => new()
    {
        UserName = "Maya Chen",
        Email = "maya@acme.com",
        Plan = plan,
        Status = status,
        ShowName = showName,
        ThemePreference = context.Dark ? ArcUserMenuTheme.Dark : ArcUserMenuTheme.Light,
        IsOpen = isOpen,
        SignOutShortcut = "⇧⌘Q",
        Items = new List<ArcUserMenuItem>
        {
            new() { Label = "Profile", Icon = "user" },
            new() { Label = "Settings", Icon = "settings", Shortcut = "⌘," },
            new() { Label = "Billing", Icon = "credit-card" }
        },
        OnSignOut = async () => await Task.Delay(600)
    };

    /// <summary>A top bar surface that gives the page-hosted panel a realistic anchor.</summary>
    private static Control TopBar(ArcUserMenu menu, double width, double height) => new Panel
    {
        Width = width,
        Height = height,
        HorizontalAlignment = HorizontalAlignment.Left,
        ClipToBounds = false,
        Children =
        {
            new Border
            {
                Width = width,
                Height = 56,
                Background = DemoUi.Brush(ArcTokens.Surface),
                BorderBrush = DemoUi.Brush(ArcTokens.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(16, 0, 10, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Acme Studio",
                            Classes = { "arc-label" },
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        WithColumn(menu, 1)
                    }
                }
            }
        }
    };

    private static Control WithColumn(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static Control Captioned(Control content, string caption)
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 16, 16)
        };
        panel.Children.Add(content);
        panel.Children.Add(new TextBlock
        {
            Text = caption,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 380,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        return panel;
    }
}
