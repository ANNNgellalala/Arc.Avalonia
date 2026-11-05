using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Effects.Controls;

namespace Arc.Avalonia.Effects.Pages;

/// <summary>
/// Cookie consent's completeness preview: the bar before a choice, the preferences panel and the small reopen
/// button after a saved choice. Each face is hosted in its own stage. Keys match Docs/cookie-consent.json.
/// </summary>
public sealed class CookieConsentPage : ArcItemPage
{
    public override string ItemSlug => "cookie-consent";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-bar", "variant-panel", "variant-reopen"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var bar = Consent();
        var preferences = Consent();
        preferences.PreferencesOpen = true;

        var reopened = Consent();
        reopened.Value = new ArcConsentValue(new Dictionary<string, bool>
        {
            ["necessary"] = true,
            ["analytics"] = true,
            ["marketing"] = false
        });

        var barStage = EffectDemos.Stage(600, 150, clip: false);
        barStage.Children.Add(bar);

        var panelStage = EffectDemos.Stage(600, 360, clip: false);
        panelStage.Children.Add(preferences);

        var reopenStage = EffectDemos.Stage(600, 96, clip: false);
        reopenStage.Children.Add(reopened);

        var panel = new StackPanel { Spacing = 24, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(Labelled("横条 · 还没做选择", barStage));
        panel.Children.Add(Labelled("偏好面板", panelStage));
        panel.Children.Add(Labelled("重新打开按钮", reopenStage));
        return panel;
    }

    private static ArcCookieConsent Consent() => new()
    {
        Position = ArcCookieConsentPosition.Bottom,
        Policy = new ArcCookiePolicy { Label = "Cookie 政策", OnClick = () => { } },
        Labels = new ArcCookieConsentLabels
        {
            Message = "我们使用 Cookie 来记住你的偏好并改进体验。",
            Customize = "自定义",
            AcceptAll = "全部接受",
            RejectAll = "全部拒绝",
            Save = "保存选择",
            PreferencesTitle = "Cookie 偏好",
            PreferencesDescription = "逐项选择允许的用途；必要项始终开启。",
            Reopen = "Cookie 设置",
            AlwaysOn = "始终开启",
            Close = "关闭"
        },
        Categories = new[]
        {
            new ArcCookieCategory
            {
                Id = "necessary",
                Label = "必要",
                Description = "登录与安全所必需。",
                Required = true
            },
            new ArcCookieCategory
            {
                Id = "analytics",
                Label = "分析",
                Description = "帮助我们了解哪些页面被使用。",
                DefaultEnabled = true
            },
            new ArcCookieCategory
            {
                Id = "marketing",
                Label = "营销",
                Description = "用于衡量活动效果。"
            }
        }
    };

    private static Control Labelled(string label, Control content)
    {
        var column = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        column.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        column.Children.Add(content);
        return column;
    }
}
