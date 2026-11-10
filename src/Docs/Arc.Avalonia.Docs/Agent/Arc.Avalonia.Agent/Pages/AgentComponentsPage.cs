using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Agent;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Agent.Pages;

/// <summary>
/// Upstream <c>pura-agent</c> component showcase: an <see cref="ArcAgentConversation"/> projects one assistant
/// message whose Markdown contains <c>choice</c> / <c>confirm</c> fences. The fences are parsed and rendered by
/// PuraMarkdown.Agent's <c>AgentPlugin</c> (choice / confirm controls); Arc ships no choice/confirm cards and no
/// Markdown plugin of its own (ADR 0007). Tool calls use the upstream <c>PuraToolCall</c> model and render as
/// upstream tool-call cards. This page replaces the retired <c>agent-choice-card</c> page.
/// </summary>
public sealed class AgentComponentsPage : ArcPage
{
    public override string Title => "Agent components";

    public override string Group => "P0 · Agent";

    public override string? Description =>
        "pura-agent 围栏演示：choice / confirm / tool-call 全部由 PuraMarkdown.Agent 的 AgentPlugin 解析与渲染，" +
        "交互状态通过 InteractionOccurred 上报；Arc 只负责把会话时间线投影到 ArcAgentConversation。";

    public override int Order => 20004;

    public override Control Build(ArcDemoContext context)
    {
        var workspace = CreateFenceWorkspace();
        var conversation = new ArcAgentConversation
        {
            Width = 760,
            Height = 640,
            HorizontalAlignment = HorizontalAlignment.Left,
            Workspace = workspace,
            Title = "pura-agent 组件",
            ShowHeader = true
        };

        var log = DemoUi.Caption("选择/确认后显示在这里；上游控件不会直接调用 agent，只上报交互意图。");
        conversation.InteractionOccurred += (_, args) =>
            log.Text = args.ComponentType switch
            {
                "choice" => $"choice · id={args.ComponentId} · selected=[{string.Join(", ", args.SelectedIds)}]",
                "confirm" => $"confirm · id={args.ComponentId} · confirmed={args.Confirmed}",
                _ => $"InteractionOccurred · {args.ComponentType} · id={args.ComponentId}"
            };

        var reset = new ArcButton
        {
            Content = "重新投影",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm
        };
        reset.Click += (_, _) =>
        {
            conversation.Refresh();
            log.Text = "Refresh · 从 session.Timeline 重新生成 pura-agent 围栏。";
        };

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "Agent components",
                    "文本答案容易含糊时使用：choice 单选/多选、confirm 确认/取消、tool-call 展示参数与结果。" +
                    "这些控件不再由 Arc 实现——围栏 JSON 由 PuraMarkdown.Agent 解析，渲染与交互状态都由上游负责，" +
                    "Arc 只是把会话时间线投影到 ArcAgentConversation。",
                    new StackPanel
                    {
                        Spacing = 12,
                        Children = { conversation, DemoUi.Row(reset, log) }
                    }),
                DemoUi.Section(
                    "PuraMarkdown.Agent 提供渲染",
                    "迁移后 Arc.Avalonia.Agent 不再包含 choice / confirm / fallback / tool-call 卡片，也没有自研 " +
                    "Markdown 插件：choice 与 confirm 是上游 AgentPlugin 的组件，tool-call 走 PuraToolCall + 上游工具卡；" +
                    "审批与提问由 ArcAgentConversation 合成同样的围栏后交给上游渲染。",
                    DemoUi.Caption("依赖例外：只有 Arc.Avalonia.Chat 与 Arc.Avalonia.Agent 直接引用 PuraMarkdown.Agent（ADR 0007）。"))
            }
        };
    }

    /// <summary>One assistant message carrying both fences; the transcript renders them as live upstream cards.</summary>
    private static ArcAgentWorkspace CreateFenceWorkspace()
    {
        var workspace = new ArcAgentWorkspace();
        var now = DateTimeOffset.Now;
        var run = new ArcAgentRunRef("run-fence", "session-fence", ArcAgentRunKind.Foreground, Label: "发布评审");
        var sequence = 0L;
        ArcEventStamp Stamp() => ArcEventStamp.Create(run.RunId, ++sequence);
        void Apply(ArcAgentEvent @event) => workspace.Apply(@event);

        Apply(new ArcRunStartedEvent(Stamp(), now, run));
        Apply(new ArcMessageStartedEvent(Stamp(), now, "msg-user", ArcAgentRole.User));
        Apply(new ArcMessageDeltaEvent(Stamp(), now, "msg-user", "准备发布，先把两个待确认项发我。"));
        Apply(new ArcMessageEndedEvent(Stamp(), now, "msg-user"));

        Apply(new ArcMessageStartedEvent(Stamp(), now, "msg-assistant", ArcAgentRole.Assistant));
        Apply(new ArcMessageDeltaEvent(Stamp(), now, "msg-assistant", FenceMarkdown));
        Apply(new ArcMessageEndedEvent(Stamp(), now, "msg-assistant"));

        Apply(new ArcToolCallStartedEvent(Stamp(), now, "call-1", "repo.search",
            "确认发布说明里引用的迁移文档链接", ArcRiskLevel.Low));
        Apply(new ArcToolCallEndedEvent(Stamp(), now, "call-1", ArcToolCallStatus.Succeeded, "1 处引用，指向 docs/29"));

        return workspace;
    }

    private const string FenceMarkdown = """
        发布前需要你确认两件事，选完我就继续：

        ```pura-agent
        {
          "type": "choice",
          "version": "1.0",
          "id": "release-channel",
          "question": "发布到哪个通道？",
          "options": [
            { "id": "stable", "label": "stable", "description": "全量用户" },
            { "id": "beta", "label": "beta", "description": "提前一周" },
            { "id": "canary", "label": "canary", "description": "只放 5% 流量", "disabled": true }
          ]
        }
        ```

        渠道确认后还要写发布说明：

        ```pura-agent
        {
          "type": "confirm",
          "version": "1.0",
          "id": "publish-confirm",
          "question": "确认写入发布说明？",
          "description": "会追加到 CHANGELOG 并同步 release notes。",
          "confirmLabel": "确认发布",
          "cancelLabel": "再等等"
        }
        ```
        """;
}
