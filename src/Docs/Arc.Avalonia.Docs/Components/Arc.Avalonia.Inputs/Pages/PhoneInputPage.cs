using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Phone input's completeness preview: empty / filling / valid / invalid states, the open country picker,
/// preferred countries and the disabled state. Declared state keys match Docs/phone-input.json.
/// </summary>
public sealed class PhoneInputPage : ArcItemPage
{
    public override string ItemSlug => "phone-input";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-empty", "state-filling", "state-valid", "state-invalid",
        "variant-country-picker", "variant-preferred", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var picker = new ArcPhoneInput
        {
            Width = 320,
            Label = "国家列表",
            DefaultCountry = "US",
            PreferredCountries = new List<string> { "GB", "IE", "US" },
            Description = "列表从国家按钮下方展开，常用国家固定在 Suggested。"
        };
        var pickerShown = false;
        Later(picker, input =>
        {
            if (pickerShown)
            {
                return;
            }

            pickerShown = true;
            input.OpenCountryPicker();
        });

        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("状态",
            Field(new ArcPhoneInput
            {
                Label = "手机号",
                DefaultCountry = "US",
                Required = true,
                Description = "仅用于订单通知。"
            }),
            Field(new ArcPhoneInput
            {
                Label = "输入中",
                DefaultCountry = "GB",
                Value = "+44791",
                Description = "边输边按国家分组。"
            }),
            Field(new ArcPhoneInput
            {
                Label = "有效",
                DefaultCountry = "US",
                Value = "+14155550132",
                Description = "位数正确，右侧显示对勾。"
            }),
            Field(new ArcPhoneInput
            {
                Label = "无效",
                DefaultCountry = "DE",
                Value = "+491511",
                Error = "这个号码太短了。"
            }),
            Field(new ArcPhoneInput
            {
                Label = "常用国家",
                DefaultCountry = "GB",
                Value = "+447911123456",
                PreferredCountries = new List<string> { "GB", "IE", "US" },
                Description = "常用国家固定在 Suggested 分组。"
            }),
            Field(new ArcPhoneInput
            {
                Label = "禁用",
                DefaultCountry = "GB",
                Value = "+447911123456",
                IsEnabled = false
            })));

        var stage = new StackPanel { Spacing = 6, Width = 340 };
        stage.Children.Add(new TextBlock { Text = "Country picker：展开的国家列表，并在 Suggested 固定常用国家", Classes = { "arc-caption" } });
        stage.Children.Add(new Border { Height = 430, Child = picker });
        panel.Children.Add(stage);

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var wrap = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 12, 16);
            wrap.Children.Add(child);
        }

        row.Children.Add(wrap);
        return row;
    }

    private static T Field<T>(T control, double width = 300)
        where T : Control
    {
        control.Width = width;
        return control;
    }

    /// <summary>Runs page-side setup once the control is laid out, so the picker can be shown open.</summary>
    private static T Later<T>(T control, Action<T> action)
        where T : Control
    {
        control.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => action(control), DispatcherPriority.Background);
        return control;
    }
}
