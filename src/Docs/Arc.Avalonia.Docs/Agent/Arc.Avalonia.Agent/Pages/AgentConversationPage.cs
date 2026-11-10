using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Agent;
using Arc.Avalonia.Chat;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Agent.Pages;

/// <summary>
/// Agent conversation preview: the transcript surface (<see cref="ArcAgentConversation"/>) projects a session
/// into the upstream chat surface. Messages are <c>PuraChatMessage</c>, Markdown bodies and tool-call cards are
/// rendered by PuraMarkdown.Agent, and approval / question cards are synthesized as <c>pura-agent</c> fences
/// through the upstream <c>AgentPlugin</c> pipeline. Arc keeps only the presentation runtime (event reducer,
/// workspace, approval/attention/artifact queues) and this projection. The fake workspace renders statically;
/// the buttons apply one host-side approval and refresh the timeline.
/// </summary>
public sealed class AgentConversationPage : ArcPage
{
    public override string Title => "Agent conversation";

    public override string Group => "P0 · Agent UI";

    public override string? Description =>
        "Agent 运行的默认对话形态：消息是上游 PuraChatMessage，Markdown 正文与工具调用卡由 " +
        "PuraMarkdown.Agent 渲染；审批/提问以 pura-agent 围栏合成整宽卡片，Arc 只保留事件 reducer、" +
        "workspace 与审批队列等表现运行时。";

    public override int Order => 20001;

    public override Control Build(ArcDemoContext context)
    {
        var workspace = CreateDemoWorkspace();
        var conversation = new ArcAgentConversation
        {
            Width = 940,
            Height = 820,
            HorizontalAlignment = HorizontalAlignment.Left,
            Workspace = workspace,
            Title = "迁移计划评审",
            ShowHeader = true
        };

        var log = DemoUi.Caption("审批、重试与提问提交会在这里显示宿主意图；Agent 库自身不执行 I/O。");

        conversation.ApprovalDecisionRequested += (_, args) =>
            log.Text = $"ApprovalDecisionRequested · {args.RequestId} · approved={args.Approved}";
        conversation.AnswerRequested += (_, args) =>
            log.Text = $"AnswerRequested · {args.RequestId} · options={args.SelectedOptions.Count} · freeText={args.FreeText ?? "none"}";
        conversation.InteractionOccurred += (_, args) =>
            log.Text = $"InteractionOccurred · {args.ComponentType} · id={args.ComponentId} · confirmed={args.Confirmed}";

        var approve = new ArcButton
        {
            Content = "批准示例审批",
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm
        };
        approve.Click += (_, _) =>
        {
            var session = workspace.GetSession("session-demo");
            var sequence = (session?.Timeline.LastOrDefault()?.Stamp.Sequence ?? 0) + 1;
            workspace.Apply(new ArcApprovalResolvedEvent(
                ArcEventStamp.Create("run-demo", sequence, $"approval-resolved-{sequence}"),
                DateTimeOffset.UtcNow,
                "approval-1",
                ArcApprovalState.Approved));
            log.Text = "ArcApprovalResolvedEvent · 宿主应用了审批，pura-agent confirm 卡从时间线移除。";
        };

        var refresh = new ArcButton
        {
            Content = "Refresh",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm
        };
        refresh.Click += (_, _) =>
        {
            conversation.Refresh();
            log.Text = "Refresh · 从 session.Timeline 重新向上游 chat surface 投影。";
        };

        var composer = new ArcChatComposer
        {
            Width = 940,
            HorizontalAlignment = HorizontalAlignment.Left,
            Placeholder = "给 agent 发消息…"
        };
        composer.Send += (_, draft) =>
            log.Text = $"Composer.Send · “{draft.Text}” · 宿主会调用 ArcAgentSurface.StartTurnAsync。";

        return new StackPanel
        {
            Children =
            {
                DemoUi.Section(
                    "PuraMarkdown.Agent 提供渲染",
                    "迁移后 Agent 不再自研聊天与卡片渲染：消息模型是上游 PuraChatMessage，正文由 PuraMarkdownView " +
                    "渲染；工具调用使用 PuraToolCall，在正文上方渲染成上游工具卡；审批与提问由 ArcAgentConversation " +
                    "合成为 pura-agent 围栏（confirm / choice），交给上游 AgentPlugin 渲染并上报 InteractionOccurred。" +
                    "Arc 保留的是事件 reducer、workspace/session 投影、审批/注意力/artifact 队列与宿主事件。",
                    DemoUi.Caption("依赖例外：只有 Arc.Avalonia.Chat 与 Arc.Avalonia.Agent 直接引用 PuraMarkdown.Agent（ADR 0007）。")),
                DemoUi.Section(
                    "Agent conversation",
                    "消息气泡与整宽事件卡在同一条时间线上按 Timeline 顺序排列；输入框由宿主放在 Surface 上，" +
                    "独立页面可直接并排放 ArcChatComposer。",
                    new StackPanel
                    {
                        Spacing = 12,
                        Children = { conversation, DemoUi.Row(approve, refresh), composer, log }
                    })
            }
        };
    }

    /// <summary>
    /// Compact, deterministic timeline for the page: two messages, one reasoning delta, one tool call and one
    /// pending approval. It fits the 820 px stage from the top, so the screenshot shows the messages first
    /// instead of starting mid-timeline (the full sample workspace is still available through CreateSample()).
    /// </summary>
    private static ArcAgentWorkspace CreateDemoWorkspace()
    {
        var workspace = new ArcAgentWorkspace();
        var now = DateTimeOffset.Now;
        var run = new ArcAgentRunRef("run-demo", "session-demo", ArcAgentRunKind.Foreground, Label: "迁移计划评审");
        var sequence = 0L;
        ArcEventStamp Stamp() => ArcEventStamp.Create(run.RunId, ++sequence);
        void Apply(ArcAgentEvent @event) => workspace.Apply(@event);

        Apply(new ArcRunStartedEvent(Stamp(), now, run));
        Apply(new ArcMessageStartedEvent(Stamp(), now, "msg-user", ArcAgentRole.User));
        Apply(new ArcMessageDeltaEvent(Stamp(), now, "msg-user", "把迁移计划整理成 PR，并检查风险。"));
        Apply(new ArcMessageEndedEvent(Stamp(), now, "msg-user"));

        Apply(new ArcMessageStartedEvent(Stamp(), now, "msg-assistant", ArcAgentRole.Assistant));
        Apply(new ArcMessageDeltaEvent(Stamp(), now, "msg-assistant",
            "计划分三步：\n\n1. 先改 **ArcAgentSurface** 的组合\n2. 再统一消息模型为 `PuraChatMessage`\n3. 最后跑 294 条目审计"));
        Apply(new ArcMessageEndedEvent(Stamp(), now, "msg-assistant"));

        Apply(new ArcReasoningDeltaEvent(Stamp(), now, "reasoning-1",
            "先比对两个 surface 的公共 API，避免破坏宿主事件；transcript 的用例迁到 conversation。"));
        Apply(new ArcToolCallStartedEvent(Stamp(), now, "call-1", "repo.search",
            "搜索 ArcAgentTranscript 的调用点", ArcRiskLevel.Low));
        Apply(new ArcToolCallEndedEvent(Stamp(), now, "call-1", ArcToolCallStatus.Succeeded, "8 处引用，3 个页面"));
        Apply(new ArcApprovalRequestedEvent(Stamp(), now, new ArcApprovalRequest
        {
            RequestId = "approval-1",
            RunId = run.RunId,
            CallId = "call-1",
            ToolName = "shell.exec",
            Risk = ArcRiskLevel.High
        }));
        Apply(new ArcRunPhaseChangedEvent(Stamp(), now, ArcRunPhase.AwaitingApproval));

        return workspace;
    }
}
