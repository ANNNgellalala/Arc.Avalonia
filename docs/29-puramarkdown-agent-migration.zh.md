# 29 · 迁移到 PuraMarkdown.Agent：Agent / Chat 不再自研

> 决策（用户指令）：**Agent/Chat 的渲染引擎改用 `PuraMarkdown.Agent`，不再自研**。
> 背景：上游 ADR-0027/0028 把 7 包合并为 3 包并改名，`PuraMarkdown.Agent` = Avalonia 渲染控件 + Chat + Agent 组件 + Testing；版本 `0.1.0-alpha.1`；依赖 Avalonia 12.1.5（Arc 已同步升级）。
> 相关：[ADR 0006](adr/0006-markdown-puramarkdown-dependency.md)、[docs/21](21-markdown-components.zh.md)、[docs/22](22-chat-agent-refactor.zh.md)。
> **0.2 更新（2026-11-13）**：上游 PuraMarkdown 拆为 Core/UI/Agent/Extension 四包；`Arc.Avalonia.Markdown` 也完成迁移（[ADR 0008](adr/0008-markdown-ui-engine-delegation.md)）：`ArcMarkdownView` 委托 `PuraMarkdownView` 渲染引擎（调度/帧预算/流式/虚拟化），Arc 保留自己的 renderer pipeline 与 token skin；`Arc.Avalonia.Chat/Agent` 升级到 `PuraMarkdown.Agent 0.2.0-alpha.1`（渲染仍由 upstream `PuraChatView` 承担）。本文 §1 中"通用 Markdown 不在迁移范围"的口径由 ADR 0008 取代。

---

## 1. 边界（迁移后谁负责什么）

| 能力 | 归属 |
| --- | --- |
| Markdown 解析/诊断/大纲/流式边界 | `PuraMarkdown.Core`（不变） |
| **Markdown 渲染控件**（`PuraMarkdownView`） | `PuraMarkdown.Agent` — **Chat/Agent 用它渲染正文** |
| **Chat 视图与消息模型**（`PuraChatView` / `PuraChatMessage` / `PuraStreamingSession`） | `PuraMarkdown.Agent` — **Arc 不再自研** |
| **pura-agent 组件**（choice / confirm / tool-call 围栏、交互状态、JSON 解析） | `PuraMarkdown.Agent`（`AgentPlugin` / `AgentPipelineExtensions.UseAgentComponents`） |
| **消息内链接点击** | **Arc 补齐**：上游 `PuraChatView` 不转发 `LinkClicked`，`PuraMarkdownOptions.LinkHandler` 默认 `NullLinkHandler`（点了没反应）。Arc 安装 `ArcChatLinkHandler`：先抛 `ArcChatView.LinkClicked`（宿主可 `HandledByHost` 接管），再走宿主 `LinkHandler` / `Options` 携带的 handler，最后 http/https/mailto/tel → `TopLevel.Launcher`；失败降级为 `ARC-CHAT-LINK-001/002` 警告。**外观也在 Arc 侧修正**：上游把链接画成 `InlineUIContainer` 里的 `HyperlinkButton`，其 19px 内容行贴 23px 正文行盒顶部，且带 1px 透明边框/圆角，导致链接整体比正文高约 4px、字体观感偏小；Chat 主题对 `ArcChatView` 内的链接去掉按钮 chrome、前景取 accent，并下移 4px 对齐正文基线（渲染仍由上游负责）。 |
| 主题 token 映射（Arc 色板 → `PuraTheme`） | Arc（适配层，几十行） |
| Agent 表现运行时（事件 reducer / workspace / 审批队列 / artifact） | Arc（保留，这是 Arc 的差异化价值，不是"渲染"） |
| 布局壳、输入框（composer）、审批托盘、注意力收件箱 | Arc（宿主 UI 外壳；输入框是表单控件，不属于 chat 渲染） |
| 通用 Markdown L2 包（`Arc.Avalonia.Markdown`，给文档/业务页面用） | **0.2 起委托 `PuraMarkdown.UI` 引擎**（ADR 0008）：`ArcMarkdownView` 内部宿主 `PuraMarkdownView`；Arc 保留 renderer pipeline（作为 Pura node renderer 适配）、token 主题与公开事件/API |

**结论**：Arc 保留 `Arc.Avalonia.Markdown`（通用 Markdown 渲染，Arc 设计系统风格）；`Arc.Avalonia.Chat` 与 `Arc.Avalonia.Agent` 的聊天/Agent 渲染全部委托给 `PuraMarkdown.Agent`。

> **补齐原则（2026-11-11 用户指令）**：使用 PuraMarkdown.Agent 的 AI 组件是**大前提**；上游实现里缺失的功能由 **Arc 自行补齐**（链接点击即首例），不因此自研同层渲染面。

## 2. 目标 API（迁移后）

```csharp
// Arc.Avalonia.Chat
public sealed class ArcChatView : ContentControl          // 内部 Host = PuraChatView
{
    public ObservableCollection<PuraChatMessage> Messages { get; }   // 上游模型
    public PuraChatView Host { get; }                                  // 上游视图
    public PuraMarkdownOptions? Options { get; set; }                 // 由 ArcChatThemeAdapter 构建
    public bool ShowComposer / Placeholder / AllowAttachments { get; set; }
    public void AddMessage(PuraChatMessage message);
    public void UpdateMessage(PuraChatMessage message);
    public bool RemoveMessage(string messageId);
    public void ClearMessages();
    public PuraStreamingSession BeginStreamingMessage(PuraChatMessage? message = null);
    public void AbortStreaming();
    public void ScrollToEnd();
    public void RestoreSnapshot(PuraChatSnapshot snapshot);
    public PuraChatAutoScrollMode AutoScrollMode { get; set; }
    // Arc 补齐的链接点击面（上游 PuraChatView 没有）：先抛 LinkClicked，再交给 LinkHandler/launcher
    public static readonly RoutedEvent<ArcChatLinkClickedEventArgs> LinkClickedEvent;
    public event EventHandler<ArcChatLinkClickedEventArgs> LinkClicked;   // HandledByHost = true 接管
    public ILinkHandler? LinkHandler { get; set; }                       // 宿主应用内导航/深链
    public event EventHandler<PuraWarningEventArgs>? Warning;            // 上游渲染警告 + ARC-CHAT-LINK-*
}
public static class ArcChatThemeAdapter
{
    public static PuraMarkdownOptions CreateOptions();
    public static PuraMarkdownOptions CreateOptions(ILinkHandler? linkHandler);   // 自建 Options 时注入链接处理
    public static PuraPipeline BuildPipeline();
}

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
* 链接点击（2026-11-11 补齐）为**加法兼容**：新增 `ArcChatLinkClickedEventArgs`、`LinkClicked`/`LinkHandler`/`Warning`、`CreateOptions(ILinkHandler?)` 重载；无参 `CreateOptions()` 保留。行为变化：Chat/Agent 消息内链接从"点击无反应"变为默认用系统 launcher 打开 http/https/mailto/tel（宿主可接管）。

## 6. 验收

1. `build 0 error`；`VERIFY` 断言改为上游 Chat/Agent 行为（消息增删改、流式、工具卡片、choice/confirm 交互、快照）；
2. `--audit` 294/294（删页后 catalog 同步）；
3. `--smoke` 页面全部可渲染；
4. `check-all 11/11`（R3 依赖闭包含 `PuraMarkdown.Agent` 与其传递依赖；R5 包依赖声明正确）；
5. `check-public-api` 更新基线；
6. 真机 Gallery：Chat 页与 Agent 页可交互（发消息、流式、工具卡/choice/confirm）；
7. 链接点击（2026-11-11）：`VERIFY 188/0`（新增 LinkClicked / HandledByHost / 宿主 handler / Options 替换 / no-TopLevel 降级 / 失败警告断言）、`.recon/chat-link-probe` light/dark 各 5/0（真实窗口点击渲染出的 `HyperlinkButton`，Arc handler 注入、事件路由、宿主接管均通过）、`check-public-api -Package Arc.Avalonia.Chat` 已更新基线并 MATCH（api=129，其他包无变化）、`check-all 11/11 GREEN`。
8. 链接外观（2026-11-11）：Chat 主题（`Themes/ArcChatTheme.axaml`）对 `ArcChatView` 内的链接去掉 `HyperlinkButton` 默认 1px 边框/内边距/圆角、前景取 `ArcAccent`、`RenderTransform` 下移 4px 对齐正文基线。浅/深色 8x 像素校验：正文与链接墨迹 top 均为 7.375px、字号/字重一致（链接保留下划线，墨迹 bottom 多 1.5px）；修改后真实窗口点击探针 light/dark 仍 5/0。

