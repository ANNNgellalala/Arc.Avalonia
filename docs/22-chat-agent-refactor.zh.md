# 22 · Chat / Agent 组件重构记录

> 状态：已落地（0.10 波次）。目标：把此前“气泡在 Content、Markdown 流式在 Markdown 包、Agent 事件在 AgentUI”的三套聊天收敛为
> **一套消息模型 + 一个气泡表面 + 一个输入组件 + Agent 事件卡组合**。用户已确认：新建 L2 `Arc.Avalonia.Chat`；
> 公共名 `ArcChatView` + `ArcChatThread` 作为 Obsolete 别名；Agent 对话默认气泡、无 Rows 模式开关；`ArcAgentAskCard` 合并进 ChoiceCard；
> 四套消息模型统一为一套；删除除 `ArcMarkdownView` 以外的重复 Markdown 渲染。

> **后续**：本文记录的是迁移前（0.10 波次）的自研 Chat 设计。之后按 ADR 0007 把 Chat/Agent 渲染整体委托给
> `PuraMarkdown.Agent`，现状见 [docs/29](29-puramarkdown-agent-migration.zh.md) 与 [docs/20](20-component-inventory.zh.md) §13；
> 其中上游缺失的消息内链接点击由 Arc 在 Chat 侧补齐（`LinkClicked` / `LinkHandler` / launcher）。

---

## 1. 最终结构

```text
Foundation (L1)
  └─ Markdown (L2)      ArcMarkdownView / DocumentView / CodeBlock / 渲染器 / 插件 / 流式会话
       └─ Chat (L2)     ArcChatMessage（唯一消息模型）
                        ArcChatBubble（气泡，正文 = ArcMarkdownView）
                        ArcChatView（虚拟化消息列表 + 打字/空状态 + 内嵌 composer）
                        ArcChatComposer（输入/附件/停止）
                        ArcChatThread（[Obsolete] ArcChatView 别名）
            ├─ Content (L2)      富文本编辑器（预览用 ArcMarkdownView；不再有聊天）
            ├─ Blocks.AI (L3)    AI 页面组合 Chat/Markdown（不引用 L4 Agent）
            └─ AgentUI (L4)      审批/提问/运行条/后台 dock 等叶子控件
                 └─ Agent (L4)   runtime + ArcAgentConversation（气泡 + 工具/推理/审批事件卡）+ Surface 布局壳
```

依赖方向没有反转：Chat 只依赖 Foundation/Services/Markdown；AgentUI 不再依赖 Markdown；Agent 依赖 Chat/AgentUI。
`Arc.Avalonia.Chat` 是第 32/33 个发布包中的 L2 家族之一（主题聚合、Meta、check-package 组件清单、API 基线同步）。

## 2. 唯一消息模型

`Arc.Avalonia.Chat.ArcChatMessage` 同时用于人类聊天、AI 回答与 Agent 运行时：

| 成员 | 说明 |
|---|---|
| `Id/Role/Author` | User / Assistant / System；作者用于头像与分组 |
| `Markdown/ContentVersion` | 正文（唯一渲染入口是 ArcMarkdownView）；版本号用于增量 |
| `Status` | Draft / Streaming / Complete / Failed / Aborted |
| `Delivery` | None / Sent / Delivered / Read（人类聊天回执，与生成状态正交） |
| `Attachments/Metadata/Error` | 附件、宿主元数据、失败原因 |
| `Append/Complete/Changed` | 流式追加、完成、变更通知 |

删除：`ArcMarkdownChatMessage`（Markdown 包）、Content 旧 `ArcChatMessage/ArcChatMessageStatus/ArcChatParticipant/ArcChatAttachment`、
Agent runtime `ArcAgentMessage/ArcMessageStatus`。Agent 事件协议（Started/Delta/Ended）保留，`Messages` 直接投影为 `ArcChatMessage`；
推理移出 Messages，作为 Timeline 事件由 Conversation 渲染成推理卡。

## 3. 组件职责

### Chat
- `ArcChatBubble`：气泡视觉（own 右 / assistant 左、非对称圆角、头像/名字/时间/状态/附件、系统消息居中、reduced-motion 动画）；
  Body 惰性创建，模板未应用也可 `BeginStreaming()`。
- `ArcChatView`：复用 L1 `ArcVirtualList` 消息级窗口化（1000 条 realized ≈10）、日期分隔与分组、Never/NearBottom/Always 自动滚动 +
  上滚暂停、列表外流式槽、Complete/Abort upsert、typing/空状态、LiveSetting；`ArcChatThread` 为 `[Obsolete]` 别名。
- `ArcChatComposer`：Enter/Shift+Enter、运行中停止、附件 tray/上限、LeadingContent、焦点环与 Automation；Agent 不再自建输入框。

### Agent
- `ArcAgentConversation`：单一滚动列表，按 Timeline 顺序交织：消息 → `ArcChatBubble`；工具 → `ArcAgentToolCallCard`；
  推理 → 折叠卡；审批 → Approve/Deny 事件卡；question → `ArcAgentChoiceCard`；步骤/artifact/attention → 紧凑事件卡。
  订阅 Workspace/Session.Changed 增量处理，流式 delta 就地更新气泡正文。
- `ArcAgentSurface`：布局壳（run bar 上、conversation 中、右侧 320px rail、底部 composer），去掉固定高度；保留既有命令事件与 StartTurnAsync/SendAsync。
- `ArcAgentChoiceCard` 吸收 `ArcAgentAskCard`（AllowFreeText/AllowDefer/DeferredRequested/FreeText），`pura-agent` 协议不变。
- 删除：`ArcAgentAskCard`、`ArcAgentTranscript`、`ArcAgentComposer`、`ArcAgentStreamingText`。

## 4. Markdown 去重

唯一 Markdown 渲染实现 = `ArcMarkdownView`（解析层仍是 PuraMarkdown.Core + Markdig，只被 Markdown 包引用）：
- Blocks.AI `AiKit.Markdown` 子集渲染删除，保留薄包装（内部创建 ArcMarkdownView）；
- Content `ArcMarkdown.BuildPreview` 等控件渲染删除，编辑器预览改为 ArcMarkdownView；`FromHtml/ToHtml/NormalizeUrl` 序列化保留；
- Markdown 包的 Chat 控件全部移出/删除。

## 5. 兼容与迁移

- `ArcChatThread`（旧 Content 类型）改为 `Arc.Avalonia.Chat.ArcChatThread : ArcChatView`，`[Obsolete]`，一个版本后删除。
- `ArcChatMessage.Text` 不再存在：改用 `Markdown`；旧 `Status`（Sending/Sent…）拆成 `Status` + `Delivery`。
- `ArcAgentSession.Messages` 元素类型改为 `ArcChatMessage`；`ArcAgentTranscript`/`ArcAgentAskCard`/`ArcAgentComposer` 已删除，
  迁移到 `ArcAgentConversation`/`ArcAgentChoiceCard`/`ArcChatComposer`。
- 文档页面：`chat-thread` 仍指向 Chat 气泡演示；`markdown-chat` 迁移为 Chat 文档页；`agent-transcript` → `agent-conversation`；
  `agent-ask-card` 删除并由 ChoiceCard 页面覆盖。

## 6. 验证

- 全量 build 0 error；Chat/Markdown/Content/Agent/AgentUI 各自 0 warning；
- Verify 覆盖消息/气泡/分组/虚拟化/流式/Composer/AgentConversation/统一模型；
- gallery `--audit` 294/294、`--smoke` 无失败；Chat/Agent/AI 页浅深色截图；
- `check-all` 9/9、`check-package` 33/33（新增 Chat 包）、Public API 基线更新。

## 7. 已知限制

- 气泡正文统一使用语义 Foreground，own 气泡用 `ArcAccentSubtle` + `ArcAccent` 描边而非实心 Accent，以保证 Markdown 对比度；
  若未来坚持实心 Accent，需要给 Markdown 正文增加 on-accent 着色契约。
- 消息操作按钮（复制/重生成）、Chat 快照序列化不在本波次。
- Blocks.AI（L3）不能引用 Agent（L4），因此 AI block 页只用 Chat + 演示数据，Agent 对话在 Agent 示例页展示。

