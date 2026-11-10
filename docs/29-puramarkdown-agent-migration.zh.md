# 29 · 迁移到 PuraMarkdown.Agent：Agent / Chat 不再自研

> 决策（用户指令）：**Agent/Chat 的渲染引擎改用 `PuraMarkdown.Agent`，不再自研**。
> 背景：上游 ADR-0027/0028 把 7 包合并为 3 包并改名，`PuraMarkdown.Agent` = Avalonia 渲染控件 + Chat + Agent 组件 + Testing；版本 `0.1.0-alpha.1`；依赖 Avalonia 12.1.5（Arc 已同步升级）。
> 相关：[ADR 0006](adr/0006-markdown-puramarkdown-dependency.md)、[docs/21](21-markdown-components.zh.md)、[docs/22](22-chat-agent-refactor.zh.md)。

---

## 1. 边界（迁移后谁负责什么）

| 能力 | 归属 |
| --- | --- |
| Markdown 解析/诊断/大纲/流式边界 | `PuraMarkdown.Core`（不变） |
| **Markdown 渲染控件**（`PuraMarkdownView`） | `PuraMarkdown.Agent` — **Chat/Agent 用它渲染正文** |
| **Chat 视图与消息模型**（`PuraChatView` / `PuraChatMessage` / `PuraStreamingSession`） | `PuraMarkdown.Agent` — **Arc 不再自研** |
| **pura-agent 组件**（choice / confirm / tool-call 围栏、交互状态、JSON 解析） | `PuraMarkdown.Agent`（`AgentPlugin` / `AgentPipelineExtensions.UseAgentComponents`） |
| 主题 token 映射（Arc 色板 → `PuraTheme`） | Arc（适配层，几十行） |
| Agent 表现运行时（事件 reducer / workspace / 审批队列 / artifact） | Arc（保留，这是 Arc 的差异化价值，不是"渲染"） |
| 布局壳、输入框（composer）、审批托盘、注意力收件箱 | Arc（宿主 UI 外壳；输入框是表单控件，不属于 chat 渲染） |
| 通用 Markdown L2 包（`Arc.Avalonia.Markdown`，给文档/业务页面用） | Arc 保留（基于 Core 的 Arc 风格渲染器，不在本次迁移范围） |

**结论**：Arc 保留 `Arc.Avalonia.Markdown`（通用 Markdown 渲染，Arc 设计系统风格）；`Arc.Avalonia.Chat` 与 `Arc.Avalonia.Agent` 的聊天/Agent 渲染全部委托给 `PuraMarkdown.Agent`。

## 2. 目标 API（迁移后）

```csharp
// Arc.Avalonia.Chat
public sealed class ArcChatView : ContentControl          // 内部 Host = PuraChatView
{
    public ObservableCollection<PuraChatMessage> Messages { get; }   // 上游模型
    public PuraMarkdownOptions? Options { get; set; }                 // 由 ArcChatThemeAdapter 构建
    public void AddMessage(PuraChatMessage message);
    public void UpdateMessage(PuraChatMessage message);
    public bool RemoveMessage(string messageId);
    public void ClearMessages();
    public PuraStreamingSession BeginStreamingMessage(PuraChatMessage? message = null);
    public void AbortStreaming();
    public void ScrollToEnd();
    public void RestoreSnapshot(PuraChatSnapshot snapshot);
    public PuraChatAutoScrollMode AutoScrollMode { get; set; }
}
public static class ArcChatThemeAdapter { public static PuraMarkdownOptions Create(PuraPipeline pipeline); }

// Arc.Avalonia.Agent
public sealed class ArcAgentConversation : ContentControl   // ArcChatView + Agent pipeline
// Workspace/Session 的 Messages 改用 PuraChatMessage；工具调用用 PuraToolCall
```

## 3. 删除（不再自研）

* `Arc.Avalonia.Chat`：`ArcChatMessage` / `ArcChatMessageStatus` / `ArcChatParticipant` / `ArcChatAttachment`、`ArcChatBubble`、`ArcChatRow`、`ArcChatUi`、`ArcChatThread`（Obsolete 别名）。
* `Arc.Avalonia.Agent`：`ArcAgentMarkdownPlugin`、`ArcAgentJson`、`ArcAgentModel`（pura-agent 载荷模型）、`ArcAgentChoiceCard`、`ArcAgentConfirmCard`、`ArcAgentFallbackCards`、`ArcAgentToolCallCard` —— 全部由上游 `AgentPlugin` + `PuraChoiceControl`/`PuraConfirmControl`/`PuraToolCallCard` 取代。
* 对应 Docs 页面与 catalog 条目、测试断言同步删除/改写。

## 4. 依赖策略例外（ADR 0007 记录）

* `PuraMarkdown.Agent` 是**第三方 UI 控件库**，与"禁止第三方 UI 组件库"的原则冲突；用户明确要求使用它，因此在 ADR 0007 登记为**有范围例外**：
  * 只允许 `Arc.Avalonia.Chat` 与 `Arc.Avalonia.Agent` 直接引用；
  * `Arc.Avalonia.Markdown` / Foundation / 其它 L2/L3 包仍只允许 `PuraMarkdown.Core`；
  * 传递依赖 `Avalonia.Controls.ItemsRepeater`、`JsonSchema.Net` 进入白名单（仅经此例外路径）。
* `tools/deps-policy.json`、`tools/check-package.ps1`、`THIRD-PARTY-NOTICES.md` 同步。

## 5. 兼容与版本

* Arc 版本仍 `0.9.0-alpha`：`ArcChatMessage` → `PuraChatMessage` 属 **breaking**，在 README/CHANGELOG 时写明；`ArcChatThread` 直接移除（此前已是 Obsolete）。
* Avalonia 固定 12.1.5（`Directory.Packages.props`，commit `bfb12ad`）。

## 6. 验收

1. `build 0 error`；`VERIFY` 断言改为上游 Chat/Agent 行为（消息增删改、流式、工具卡片、choice/confirm 交互、快照）；
2. `--audit` 294/294（删页后 catalog 同步）；
3. `--smoke` 页面全部可渲染；
4. `check-all 11/11`（R3 依赖闭包含 `PuraMarkdown.Agent` 与其传递依赖；R5 包依赖声明正确）；
5. `check-public-api` 更新基线；
6. 真机 Gallery：Chat 页与 Agent 页可交互（发消息、流式、工具卡/choice/confirm）。
