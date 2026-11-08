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
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Agent.Pages;

/// <summary>
/// Agent conversation preview: one scrollable timeline where user/assistant messages render as
/// <c>ArcChatBubble</c> (user right, assistant left) and tool calls, reasoning, approvals, questions, steps and
/// artifacts render as full-width event cards in run order. There is no Rows/mode switch; the bubble surface
/// and the event cards always interleave. The fake transport is not attached, so the sample workspace renders
/// statically; the buttons apply one host-side approval and refresh the timeline.
/// </summary>
public sealed class AgentConversationPage : ArcPage
{
    public override string Title => "Agent conversation";

    public override string Group => "P0 · Agent UI";

    public override string? Description =>
        "Agent 运行的默认对话形态：用户/助手消息为左右气泡（Markdown 正文），工具调用、推理、审批、提问、" +
        "步骤与 artifact 按 Timeline 顺序作为整宽事件卡插在气泡之间；无 Rows 模式切换。";

    public override int Order => 20001;

    public override Control Build(ArcDemoContext context)
    {
        // Bubbles and event cards render Markdown; refresh the token snapshot for the active palette.
        ArcMarkdownStyle.Current = new ArcMarkdownStyle();

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
        conversation.RetryRequested += (_, args) =>
            log.Text = $"RetryRequested · {args.CallId} · 宿主会重新执行这次工具调用。";
        conversation.AnswerRequested += (_, args) =>
            log.Text = $"AnswerRequested · {args.RequestId} · options={args.SelectedOptions.Count} · freeText={args.FreeText ?? "none"}";

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
            log.Text = "ArcApprovalResolvedEvent · 宿主应用了审批，事件卡状态更新。";
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
            log.Text = "Refresh · 从 session.Timeline 重新构建对话。";
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
                    "Agent conversation",
                    "消息气泡使用 Chat 包（用户右、助手左、Markdown 正文）；工具/推理/审批/提问/步骤/artifact 是" +
                    "整宽事件卡。输入框由宿主放在 Surface 上，独立页面可直接并排放 ArcChatComposer。",
                    new StackPanel
                    {
                        Spacing = 12,
                        Children = { conversation, DemoUi.Row(approve, refresh), composer, log }
                    })
            }
        };
    }

    /// <summary>
    /// Compact, deterministic timeline for the page: two message bubbles, one reasoning card, one tool call and
    /// one pending approval. It fits the 560 px stage from the top, so the screenshot shows the bubbles first
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
            "计划分三步：\n\n1. 先改 **ArcAgentSurface** 的组合\n2. 再统一消息模型为 `ArcChatMessage`\n3. 最后跑 294 条目审计"));
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
