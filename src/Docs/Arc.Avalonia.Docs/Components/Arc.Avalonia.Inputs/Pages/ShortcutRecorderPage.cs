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
/// Shortcut recorder's completeness preview: empty, recorded, recording, hidden label and disabled states.
/// Declared state keys match Docs/shortcut-recorder.json.
/// </summary>
public sealed class ShortcutRecorderPage : ArcItemPage
{
    public override string ItemSlug => "shortcut-recorder";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-empty", "state-recorded", "state-recording", "variant-hide-label", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var recording = new ArcShortcutRecorder
        {
            Width = 300,
            Label = "复制链接",
            Description = "按 Escape 取消录制。"
        };
        Later(recording, recorder => recorder.BeginRecording());

        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("状态",
            Field(new ArcShortcutRecorder
            {
                Width = 300,
                Label = "打开搜索",
                Placeholder = "按下组合键",
                Description = "未录制时显示占位文案。"
            }),
            Field(new ArcShortcutRecorder
            {
                Width = 300,
                Label = "切换侧栏",
                Gesture = "Ctrl+Shift+K",
                Description = "组合渲染为一排键帽。"
            }),
            Field(recording)));

        panel.Children.Add(Row("标签与状态",
            Field(new ArcShortcutRecorder
            {
                Width = 300,
                Label = "不绘制标签",
                HideLabel = true,
                Gesture = "Ctrl+Shift+F",
                Description = "标签只提供给读屏。"
            }),
            Field(new ArcShortcutRecorder
            {
                Width = 300,
                Label = "由管理员锁定",
                Gesture = "Ctrl+Alt+R",
                IsEnabled = false
            })));

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

    private static Control Field(Control control) => control;

    /// <summary>Runs page-side setup once the control is laid out, so the recording state can show.</summary>
    private static T Later<T>(T control, Action<T> action)
        where T : Control
    {
        control.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => action(control), DispatcherPriority.Background);
        return control;
    }
}
