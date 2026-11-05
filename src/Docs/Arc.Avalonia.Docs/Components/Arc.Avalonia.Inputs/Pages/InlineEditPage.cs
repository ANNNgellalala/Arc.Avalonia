using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Inline edit's completeness preview: resting values, both typographic variants, empty, editing,
/// multiline, validation failure, a pending save and the disabled state. Declared state keys match
/// Docs/inline-edit.json.
/// </summary>
public sealed class InlineEditPage : ArcItemPage
{
    public override string ItemSlug => "inline-edit";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-resting", "state-empty", "variant-title", "variant-body",
        "state-editing", "state-multiline", "state-invalid", "state-saving", "state-disabled"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var editing = Later(
            new ArcInlineEdit { Width = 300, Label = "项目名称", Value = "Northwind redesign" },
            edit => edit.BeginEdit());

        var multiline = Later(
            new ArcInlineEdit
            {
                Width = 300,
                Label = "项目描述",
                Variant = ArcInlineEditVariant.Body,
                Multiline = true,
                Value = "为财务团队做的安静仪表盘，三月上线。"
            },
            edit => edit.BeginEdit());

        var invalid = Later(
            new ArcInlineEdit
            {
                Width = 300,
                Label = "团队名称",
                Validate = draft => string.IsNullOrWhiteSpace(draft) ? "名称不能为空" : null
            },
            edit =>
            {
                edit.BeginEdit();
                edit.Commit();
            });

        var saving = new ArcInlineEdit { Width = 300, Label = "看板名称", Value = "Roadmap" };
        var pending = new TaskCompletionSource();
        saving.Save += (_, args) => args.Pending = pending.Task;
        Later(saving, edit =>
        {
            edit.BeginEdit();
            edit.Commit();
        });

        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("静止态与变体",
            Field(new ArcInlineEdit
            {
                Width = 300,
                Label = "标题变体",
                Value = "Northwind redesign",
                Variant = ArcInlineEditVariant.Title
            }, "Title：22 px 标题字号"),
            Field(new ArcInlineEdit
            {
                Width = 300,
                Label = "正文变体",
                Value = "为财务团队做的安静仪表盘，三月上线。",
                Variant = ArcInlineEditVariant.Body
            }, "Body：14 px 正文字号"),
            Field(new ArcInlineEdit
            {
                Width = 300,
                Label = "发布名称",
                Placeholder = "未命名发布"
            }, "Empty：回退到占位文字")));

        panel.Children.Add(Row("编辑与保存",
            Field(editing, "Editing：原位变成字段，自动全选"),
            Field(multiline, "Multiline：换行并增高"),
            Field(invalid, "Invalid：草稿保留，显示错误"),
            Field(saving, "Saving：显示 spinner，暂时只读"),
            Field(new ArcInlineEdit
            {
                Width = 300,
                Label = "由管理员锁定",
                Value = "Roadmap",
                IsEnabled = false
            }, "Disabled")));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var wrap = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 14, 16);
            wrap.Children.Add(child);
        }

        row.Children.Add(wrap);
        return row;
    }

    private static Control Field(Control control, string caption)
    {
        var stack = new StackPanel { Spacing = 6, Width = 300 };
        stack.Children.Add(new TextBlock { Text = caption, Classes = { "arc-caption" }, TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(control);
        return stack;
    }

    /// <summary>Runs page-side setup once the control is laid out, so states that need a template can show.</summary>
    private static T Later<T>(T control, Action<T> action)
        where T : Control
    {
        control.AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => action(control), DispatcherPriority.Background);
        return control;
    }
}
