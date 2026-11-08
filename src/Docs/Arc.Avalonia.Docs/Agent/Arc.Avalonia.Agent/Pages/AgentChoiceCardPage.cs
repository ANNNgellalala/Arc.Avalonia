using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Agent;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Agent.Pages;

/// <summary>
/// Choice card preview: the single interactive question control of the Agent layer. One card switches between
/// a single-choice date question, a multi-select account question with free-text input and a deferred
/// question; submissions and deferrals are reported in the line underneath. The same control renders the
/// <c>pura-agent</c> choice fence, so the AskCard page is retired.
/// </summary>
public sealed class AgentChoiceCardPage : ArcPage
{
    public override string Title => "Agent choice card";

    public override string Group => "P0 · Agent";

    public override string? Description =>
        "结构化提问卡：单选/多选、选项说明与图标、禁用项、可选自由输入与「稍后再答」；提交通过 " +
        "Submitted 事件返回 SelectedIds + FreeText，pura-agent 的 choice fence 复用同一控件。";

    public override int Order => 20004;

    public override Control Build(ArcDemoContext context)
    {
        // The pura-agent fence demo renders through ArcMarkdownView; refresh the token snapshot.
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();

        var card = new ArcAgentChoiceCard
        {
            Width = 640,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsInteractive = true
        };

        var log = DemoUi.Caption("提交与延后意图会显示在这里；卡片从不直接调用 agent。");
        card.Submitted += (_, args) =>
            log.Text = $"Submitted · [{string.Join(", ", args.SelectedIds)}] · freeText: {args.FreeText ?? "none"}";
        card.DeferredRequested += (_, _) =>
            log.Text = "DeferredRequested · 宿主保留问题并稍后重开。";

        void Apply(string mode)
        {
            card.Model = mode switch
            {
                "multi" => MultiModel(),
                "defer" => DeferModel(),
                _ => SingleModel()
            };
            card.AllowFreeText = mode == "multi";
            card.AllowDefer = mode == "defer";
            card.IsInteractive = true;
            log.Text = mode switch
            {
                "multi" => "多选 · min/max 校验，可附加自由输入。",
                "defer" => "可延后 · DeferredRequested 让宿主保留问题。",
                _ => "单选 · 选中即提交 SelectedIds。"
            };
        }

        var buttons = DemoUi.Row(
            StateButton("单选（日期）", "single", Apply),
            StateButton("多选 + 自由输入", "multi", Apply),
            StateButton("可延后", "defer", Apply));

        Apply("single");

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Agent choice card",
                    "文本答案容易含糊时使用：选日期、账户、文件或两条路径之一。选项支持说明、禁用与图标，" +
                    "多选有 min/max，另有自由输入与 defer 两条补充路径；范围与 pura-agent choice 协议兼容。",
                    new StackPanel { Spacing = 12, Children = { buttons, card, log } }),
                DemoUi.Section(
                    "pura-agent 围栏",
                    "助手回答里的 pura-agent 围栏由 ArcAgentMarkdownPlugin 接管，渲染成同一套 choice / confirm " +
                    "控件；未注册插件时围栏回退为代码块。",
                    FenceDemo())
            }
        };
    }

    private static Control FenceDemo()
    {
        var log = DemoUi.Caption("交互事件通过 view.InteractionOccurred 上报，可用 InteractionStates 恢复。");
        var view = new ArcMarkdownView
        {
            Width = 640,
            HorizontalAlignment = HorizontalAlignment.Left,
            Options = ArcAgentMarkdown.DefaultOptions,
            Markdown = AgentChoiceFence + "\n\n" + AgentConfirmFence
        };
        view.InteractionOccurred += (_, args) =>
            log.Text = $"InteractionOccurred · {args.ComponentType} · id={args.ComponentId} · confirmed={args.Confirmed}";

        return new StackPanel { Spacing = 10, Children = { view, log } };
    }

    private const string AgentChoiceFence = """
        ```pura-agent
        {
          "type": "choice",
          "version": "1.0",
          "id": "release-channel",
          "question": "发布到哪个通道？",
          "options": [
            { "id": "stable", "label": "stable", "description": "全量用户" },
            { "id": "beta", "label": "beta", "description": "提前一周" }
          ]
        }
        ```
        """;

    private const string AgentConfirmFence = """
        ```pura-agent
        { "type": "confirm", "version": "1.0", "id": "publish-confirm", "question": "确认写入发布说明？", "confirmLabel": "确认发布" }
        ```
        """;

    private static Control StateButton(string label, string mode, System.Action<string> apply)
    {
        var button = new ArcButton
        {
            Content = label,
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        ArcUi.Name(button, $"{label} choice state");
        button.Click += (_, _) => apply(mode);
        return button;
    }

    private static ArcAgentChoiceModel SingleModel()
    {
        var options = new List<ArcAgentChoiceOption>
        {
            new("thu", "周四 15:00", "保留上午的预算评审"),
            new("fri", "周五 9:30", "牙医下一个最早档期"),
            new("monday", "下周一 11:00", "提醒顺延四天")
        };

        return new ArcAgentChoiceModel
        {
            Type = "choice",
            Version = "1.0",
            Id = "dentist-slot",
            Question = "把牙医预约改到哪一天？",
            Options = options
        };
    }

    private static ArcAgentChoiceModel MultiModel()
    {
        var options = new List<ArcAgentChoiceOption>
        {
            new("household", "家庭 · 活期（··4021）", "日用、水电与房贷"),
            new("savings", "家庭 · 储蓄（··8890）", "只读，汇总不需要"),
            new("business", "工作室 · 对公（··1174）", "独立账本，通常不计入")
        };

        return new ArcAgentChoiceModel
        {
            Type = "choice",
            Version = "1.0",
            Id = "budget-accounts",
            Question = "这周的预算汇总要读哪些账户？",
            Multi = true,
            Min = 1,
            Max = 3,
            Options = options
        };
    }

    private static ArcAgentChoiceModel DeferModel()
    {
        var options = new List<ArcAgentChoiceOption>
        {
            new("now", "现在发布", "文档与截图已就绪"),
            new("tomorrow", "明天发布", "等评审意见回来"),
            new("later", "本周内再定", "先处理阻塞项")
        };

        return new ArcAgentChoiceModel
        {
            Type = "choice",
            Version = "1.0",
            Id = "release-window",
            Question = "发布窗口定在什么时候？",
            Options = options
        };
    }
}
