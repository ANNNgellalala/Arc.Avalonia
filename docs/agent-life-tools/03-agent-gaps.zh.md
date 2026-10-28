# 03 · Agent 交互与运行时 UI 缺口分析

> 分析对象：`D:\Code\VibeCoding\Arc.Avalonia`（Arc UI 的 Avalonia 12.1.4 / .NET 10 移植，24 个工程，catalog 294 项）。
> 产品假设：用这套组件库构建**基于 Agent 的日常生活工具**（桌面个人助手：主动规划、长期记忆、多步工具调用、用户审批、定时例行任务、语音与后台常驻）。
> 本文件分工：**只负责 Agent 交互与运行时 UI**；健康/财务/出行/饮食/家庭等生活内容域由 `02-life-gaps.zh.md` 负责。
> 本文件只读代码与文档，不修改 `src/`、`docs/01~05` 与 `README.md`；配套提案见 `03-agent-proposals.json`。
>
> **范围说明（v2，2026-10-28）**：本库定位为**纯 UI 组件库**。本文关于托盘/系统通知/调度/离线等"运行时"的分析，只用于说明 **UI 需要展示的状态与接口**；组件库不实现这些能力（由宿主应用负责）。28 项提案全部是 UI 组件/页面。

---

## 0. 结论摘要

### 0.1 一句话结论

**这套库已经画出了 Agent 产品几乎全部的"界面表情"，但还没有给出任何"运行时接口"。** 40 个 AI 命名资产（11 AI widgets + 11 AI explainers + 18 AI blocks）在视觉、动效、可访问性上都能直接支撑演示；但其中没有一个向外暴露 Agent 生命周期事件（Approve / Deny / Stop / Retry / Ask / Handoff），18 个 AI block 全部是 `ArcItemPage` 页面级样例（`src/Blocks/Arc.Avalonia.Blocks.AI` 只有一个 `Controls/AiKit.cs` 布局助手 + 18 个 `Pages/*.cs`），不能被业务页面当控件嵌入。要把它变成可用的桌面助手，缺的不是"更多漂亮面板"，而是**一条可驱动的事件流、一套可执行的审批/中断契约，以及桌面运行时（托盘、系统通知、调度、离线）的骨架**。

### 0.2 关键数字

| 指标 | 值 | 出处 |
| --- | --- | --- |
| catalog 总量 | 294（128 components / 55 special / 111 blocks） | `docs/arc-catalog.json`；`README.md:210` |
| 与 Agent 直接相关的命名资产 | 40（11 AI widgets + 11 AI explainers + 18 AI blocks） | `docs/arc-catalog.json` 的 `specialGroups`/`blockGroups` |
| 提供 Agent 运行时事件契约（approve / stop / retry / ask / handoff）的 AI 资产 | **0** | 见 §2；40 项中唯一的 `public event` 来自通用氛围件 `ArcBlackHole`（SelectionChanged/Deleted），与 Agent 无关 |
| AI block 中的可复用控件类 | **0**（仅 `AiKit` 内部静态助手） | `src/Blocks/Arc.Avalonia.Blocks.AI/Controls/AiKit.cs` |
| 托盘 / 系统通知 / 全局热键 API 使用 | 各 **0 处** | 全仓 `src/**/*.cs,*.axaml` 精确 grep |
| 桌面运行时存在性 | `TrayIcon`=0、`WindowNotificationManager`=0、`RegisterHotKey`=0、`NativeMenu`=0 | 同上 |
| 现有展示页 / 截图 | 219 页 / 438 张 | `README.md:194-195`；`docs/03-implementation.zh.md:74-75` |
| 本次新增提案 | 28 项（7 P0 / 16 P1 / 5 P2） | `03-agent-proposals.json` |
| 提案覆盖场景 | 15 个 Agent 运行场景 | 同上 |

### 0.3 五个最重要的判断（Top 5 Insights）

1. **"能看"与"能接"之间差一个事件层。** `ArcAgentStatus` 的 Pause/Stop 只是 `SetCurrentValue(StatusProperty, …)`（`ArcAgentStatus.cs:515`、`:533`），`Open run` 按钮甚至没有挂处理器（`ArcAgentStatus.cs:486-488`）；`ArcToolCall` 的 Allow/Deny/Retry 直接修改传入的数据对象并自导自演跑 1.2 秒（`ArcToolCall.cs:303-324`、`:637-678`）。接入真实 Agent 时，控件会与运行时状态互相覆盖——这不是修补，而是要补一份**受控/非受控 + 事件回传**的契约。
2. **18 个 AI block 是"截图级产品页"，不是积木。** `agent-approvals` 的参数是只读行、`Always allow` 只是一枚 chip、审批不触达 agent（`AgentApprovalsPage.cs:52-55`）；`agent-routines` 的排期编辑只改句子和倒计时字符串（`AgentRoutinesPage.cs:52-54`）；`ai-composer` 的 Stop 与进度环"documented but not animated"（`AiComposerPage.cs:54-57`）。这些页面非常适合做验收基线，但复制它们不等于得到可复用组件。
3. **桌面助手的三个决定性能力在本仓库完全空白：后台常驻（托盘）、系统通知触达、定时调度。** 全仓没有任何 `TrayIcon`/原生菜单/系统通知封装；`ArcToastHost` 只是"页面里放一个 host"（`ArcToastHost.cs:15-19`），`ArcDialog`/`ArcDrawer` 是 `ContentControl` 页内浮层（`ArcDialog.cs:25`、`ArcDrawer.cs:28`）。关掉主窗口后，助手既不能继续"被看见"，也不能把审批送到用户面前。
4. **生活助手最贵的失败模式是"Agent 做了不可逆的事"，而当前只有外观级的批准。** 仓库有 `hold-to-confirm`、`confirm-morph`、`ai-side-panel` 的一键 undo（`AiSidePanelPage.cs:51-55`），但没有可复用的 diff、检查点/回滚、审批范围与过期、信任规则治理。**审批的表面在，审批的语义不在。**
5. **性能约束会直接转化为交互约束。** docs/04 明确整库几乎零虚拟化（`docs/04-design-and-performance-review.zh.md:176-177`、`:348-357`），ChatThread 每条消息变化整树重建（同文 `:243`，G-P0-7），68 个文件 `new DispatcherTimer`（`:26`、`:299`）。Agent 会话天然是"长列表 + 高频增量"——不先补增量/虚拟化契约，流式输出和长跑 trace 会在真实数据量下崩掉。

---

## 1. 方法与证据分级

### 1.1 读了什么

- 目录与元数据：`docs/arc-catalog.json` 全量 294 项（slug/title/kind/category/tier/description）。
- 实现说明：`README.md`、`docs/03-implementation.zh.md`、`docs/04-design-and-performance-review.zh.md`（重点 R4/R5/G-P0-7）。
- 源码：`src/Special/Arc.Avalonia.Special.AIWidgets/Controls`（11 项）、`src/Special/Arc.Avalonia.Special.AIExplainers/Controls`（11 项）、`src/Blocks/Arc.Avalonia.Blocks.AI`（`Controls/AiKit.cs` + 18 个 Pages）、`src/Components/Arc.Avalonia.Actions`（toast/overlay/dialog）、`src/Components/Arc.Avalonia.Feedback`（timeline/usage-meter/countdown/table）、`src/Components/Arc.Avalonia.Content`（chat-thread）、`src/Special/Arc.Avalonia.Special.Media`（voice-orb/voice-recorder）、`src/Blocks/Arc.Avalonia.Blocks.App`/`Blocks.Team`（notification-center/settings/security/week-calendar）、`src/Apps/Arc.Avalonia.Gallery`（页面契约与截图链路）。
- 交叉校验：对全仓 `src/**/*.cs,*.axaml` 做精确 grep（`TrayIcon`、`WindowNotificationManager`、`RegisterHotKey`、`NativeMenu`、`DiffView`、`public event`），避免只看名字。

### 1.2 支持度分级（全文统一）

| 记号 | 含义 |
| --- | --- |
| ● | **可复用组件**：有数据属性与公开事件/命令契约，能直接接运行时。 |
| ◐ | **可复用显示件或页面级样例**：有数据属性（Special）或完整交互外观（Blocks），但缺事件契约 / 不可嵌入 / 交互自导自演。 |
| ○ | **纯插图**：解释性视觉，不承载业务状态。 |
| — | 组件库中不存在。 |

> 结论先说：Agent 相关 40 项里，● 为 0：5 个"可复用显示件"（agent-status / tool-call / agent-plan / agent-timeline / context-window）与 18 个 AI block 页面是 ◐；11 个 AI explainer 与 6 个氛围件（ink-fluid / activity-rings / stretch-refresh / morph-loader / skeleton-morph / black-hole）是 ○。通用基础件（toast、dialog、timeline、usage-meter 等）里有若干 ●，但都不懂"Agent"。

---

## 2. 现状盘点（按 Agent 生命周期）

生命周期定义：**理解 → 计划 → 执行 → 审批 → 结果 → 记忆 → 复盘**，外加 5 条横切能力（通知触达 / 成本 / 权限安全 / 语音 / 模型选择）。

### 2.1 理解（把用户意图变成可执行请求）

| 项 | 位置 | 实际支持 | 支持度 | 关键证据 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| `ai-composer` | Blocks.AI | 附件 chip、模型 chip、斜杠与 @ 的**视觉**占位、发送/停止按钮外观 | ◐ | `AiComposerPage.cs:54-57`（"/ 命令列表、@ 提及、听写与上传都是 chip 表示；Stop 与进度环只有文档，没有动画"） | 无输入事件/命令菜单数据契约；无语音、无中断语义、无"上下文来源"选择 |
| `ai-chat` | Blocks.AI | 整页会话：markdown 子集、折叠推理、工具 chip、引用；**成品态** | ◐ | `AiChatPage.cs:53-57`（"编辑成版本、停止流、hover 操作只写在 notes，样例是稳定会话"） | 无流式追加、无消息版本、无中断、无多轮工具事件、无虚拟化 |
| `ai-side-panel` | Blocks.AI | 贴着"页面"读选中段、word diff、Apply/Undo | ◐ | `AiSidePanelPage.cs:51-55`（word diff 预计算；Apply/Undo 只改页面文案；固定在页面内） | 只读"页面"而非桌面应用/窗口/剪贴板；无选中捕获真实通路；无跨应用上下文 |
| `model-picker` | Blocks.AI | 模型 pill、推理档位、按 provider 浏览 | ◐ | `ModelPickerPage.cs:51-54`（菜单直接渲染成打开态，价格为例示值） | 无模型能力/成本/离线状态数据；无路由策略 |
| `voice-orb` | Special.Media | Idle/Listening/Thinking/Speaking 四态、转写两行、计时 | ◐ | `ArcVoiceOrb.cs:9-16`、`:50-101`（属性有，事件无；麦克风电平为模拟） | 无事件、无 barge-in、无真实设备/权限、无"说完即发"语义 |
| `voice-recorder` | Special.Media | 波形、回放、发送 take | ◐ | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcVoiceRecorder.cs` | 无转写、无 VAD、无语音命令 |

**阶段判断**：理解阶段"输入表面"齐全，"输入通道"（结构化追问、桌面上下文、语音事件）全缺。

### 2.2 计划（把请求拆成步骤并获得用户认可）

| 项 | 位置 | 实际支持 | 支持度 | 关键证据 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| `agent-plan` | Special.AIWidgets | Draft 状态可改名/拖动排序/增删步骤/Keep planning；Approve plan 后进入 Running 清单；失败步 Retry/Skip | ◐ | `ArcAgentPlan.cs:17-33`（状态枚举）、`:395-412`、`:675-690`；`:62-63`（"Keep planning 追加固定建议；运行按固定时钟推进，而不是你的 agent 事件"） | 无 Approve/Edit/Retry 事件；无预计耗时/成本/风险；无与日历的可行性校验；无"批准后计划被 Agent 改写"的版本 diff |
| `agent-orchestrator` | Blocks.AI | lead 拆解 + 并行 lane + 结果合并的**快照** | ◐ | `AgentOrchestratorPage.cs:53-56`（"run 是快照；lane 已返回；lead 与 lane 的连线因静态区块不画；并发只改标签与队列顺序"） | 无运行中拓扑、无 lane 间依赖/取消、无重试分支、无交互式编排 |
| `reasoning-tree` | Special.AIExplainers | 思维树/beam 的**插图**（beam 1–3 是真实控件） | ○ | `ArcReasoningTree.cs:14-27`（固定样例分数） | 不承载真实检索/推理状态 |
| `agent-loop` | Special.AIExplainers | 三段循环的**插图**（固定 3 次样例） | ○ | `ArcAgentLoop.cs:19-27` | 同上 |

**阶段判断**：计划"画得完整、批不了、改不动、估不出"。

### 2.3 执行（运行中的可见性与控制）

| 项 | 位置 | 实际支持 | 支持度 | 关键证据 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| `agent-status` | Special.AIWidgets | 胶囊态：运行环、当前活动、计数、计时；展开步骤、进度线；Pause/Stop/Open run | ◐ | `ArcAgentStatus.cs:18-27`（7 态）、`:454-466`（NeedsInput 的 Approve/Deny/Retry 只改自身状态）、`:500-534`（Pause/Stop 只改属性）、`:486-488`（Open run 无处理器） | **无事件**；状态由控件自持；无"停止范围"；无转交/接管；无多 run 队列 |
| `tool-call` | Special.AIWidgets | 一行工具调用：状态、输入/输出展开、Allow/Deny、Retry | ◐ | `ArcToolCall.cs:17-24`、`:303-324`、`:637-678`（批准后直接把数据改成 Running→Success） | 无事件；参数只读；无 dry-run；无副作用/diff；无审批过期；无工具结果的结构化查看器 |
| `agent-timeline` | Special.AIWidgets | 多 agent lane、thinking/tool/waiting span、handoff、playhead 回放、tokens/cost 头 | ◐ | `ArcAgentTimeline.cs:78-95`、`:198-208`、`:235-295`（内部时钟驱动回放） | 只读回放；无事件；不能从 span 触发重试/取消；无真实 trace 导入 |
| `context-window` | Special.AIWidgets | 200 格上下文预算、分类图例、阈值线、Send/Attach/Compact | ◐ | `ArcContextWindow.cs:64-75`、`:508-583` | 演示型操作；无真实 tokenizer/压缩事件；无"哪些内容可被丢弃"选择 |
| `agent-run` | Blocks.AI | 单次运行的步骤流 + 停在两文件 diff 上等人批准 | ◐ | `AgentRunPage.cs:51-54`（"snapshot at its approval gate；Approve 只写样例变更"） | 无事件、无真实 diff 组件、无重跑/分支 |
| `agent-tasks` | Blocks.AI | 后台任务卡（queued/running/review）+ 打开 PR | ◐ | `AgentTasksPage.cs:53-55`（"全部样例数据"） | 无并发队列、无优先级/取消、无资源占用 |
| `ai-chat` / `chat-thread` | Blocks.AI / Content | 流式外观 / 消息分组、typing、附件、自动滚动 | ◐ | `docs/04:243`（G-P0-7 整树重建）；`ArcChatThread.cs:467`（`_messageHost.Children.Clear()`） | 无增量 append 契约；无工具/审批事件类型；无虚拟化 |
| `morph-loader` / `ink-fluid` 等 | Special.AIWidgets | 运行中的氛围动画 | ○ | `Special.AIWidgets/Controls/*` | 与运行状态无绑定 |

**阶段判断**：执行阶段"可视化最丰富、可控性最弱"——所有按钮都在改自己的数据，而不是告诉宿主"用户想停"。

### 2.4 审批（把风险交给人）

| 项 | 位置 | 实际支持 | 支持度 | 关键证据 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| `agent-approvals` | Blocks.AI | 审批卡：工具、理由、参数只读行、Approve/Deny/Always allow、已解决日志、j/k/a 快捷键 | ◐ | `AgentApprovalsPage.cs:52-55`（"nothing reaches an agent；参数是只读行而不是内联字段；Always allow 只加规则 chip 不匹配旧请求；快捷键只文档化"）；`:182-220` | 无事件、无队列/批量、无过期、无改参、无预览、无范围/约束、无审计写入 |
| `tool-call`（Approval 态） | Special.AIWidgets | 行内 Allow/Deny | ◐ | `ArcToolCall.cs:303-324` | 同上（粒度太粗，只适合低风险） |
| `hold-to-confirm` / `confirm-morph` | Media / Actions | 按住确认、确认形变（**可复用交互**） | ● | `src/Components/Arc.Avalonia.Media/Controls/ArcHoldToConfirm.cs`；`src/Components/Arc.Avalonia.Actions/Controls/ArcConfirmMorph.cs:138-146`（有 StateChanged/Cancelled） | 不懂审批语义：无风险说明、无范围、无过期、无审计 |
| `dialog` / `drawer` | Actions | 页内模态与抽屉，Esc/外点关闭 | ● | `ArcDialog.cs:25-27`、`:65`；`ArcDrawer.cs:28-30` | 页内宿主，不是窗口级/系统级；无"审批专用"结构 |

### 2.5 结果（把答案/产物交回用户）

| 项 | 位置 | 实际支持 | 支持度 | 关键证据 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| `ai-answer` | Blocks.AI | 成品答案 + 来源卡 + 引用点亮 | ◐ | `AiAnswerPage.cs`（Implementation notes：样例数据；引用只点亮段落与来源卡） | 无"结果对象"（文件/事件/订单）打开与保存；无未读/已读结果流 |
| `research-run` | Blocks.AI | 深研究步骤、来源 mark、引用答案 | ◐ | `ResearchRunPage.cs:53-55`（"答案预先写好、阅读百分比静态"） | 无真实流式、无取消、无结果导出 |
| `ai-side-panel` | Blocks.AI | Apply/Undo 的**局部**撤销 | ◐ | `AiSidePanelPage.cs:51-55` | 撤销只覆盖当前页面文案，不覆盖外部工具副作用 |
| `toast` / `toast-stack` | Actions | 短确认、动作按钮、自动消失 | ● | `ArcToast.cs`、`ArcToastHost.cs:43-92` | 只能页内；无行动型通知/持久通知/通知中心投递 |
| `diff-review` | — | **不存在** | — | 全仓 grep 无 diff 控件；`AiSidePanelPage` 的 word diff 预计算，`AgentRunPage` 的 diff 用 token | 结果批准缺最核心的"改动预览" |
| `empty-state` | Feedback | 空态引导 | ● | `ArcEmptyState.cs` | 无"Agent 无事可做/等待授权"的专用空态语义 |

### 2.6 记忆（长期偏好、事实与关系）

| 项 | 位置 | 实际支持 | 支持度 | 关键证据 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| `agent-memory` | Blocks.AI | 记忆列表、筛选搜索、就地编辑、忘记、保存记忆/聊天历史开关 | ◐ | `AgentMemoryPage.cs:53-55`（"Forget 立即删除，没有 5 秒撤销；capture pill 只存一条样例"） | 无来源/置信度/作用域；无批量治理；无冲突合并；无导出/全删审计；无加密本地库契约 |
| `context-window` | Special.AIWidgets | 上下文中 memory 占比的**可视化** | ◐ | `ArcContextWindow.cs:17-27` | 只展示，不管理 |
| `knowledge-base` | Blocks.AI | 文档摄取/切块/检索排序样例 | ◐ | `KnowledgeBasePage.cs`（ingestion 快照、检索返回固定 chunk） | 生活助手更需要"个人记忆"而非文档库；两者未打通 |
| `json-viewer` / `tree-view` | Feedback | 结构化数据浏览 | ● | `ArcJsonViewer.cs`、`ArcTreeView.cs` | 可作为记忆来源/证据的查看器，但无领域模型 |

### 2.7 复盘（回看、评估、改进）

| 项 | 位置 | 实际支持 | 支持度 | 关键证据 | 缺口 |
| --- | --- | --- | --- | --- | --- |
| `agent-timeline` | Special.AIWidgets | playhead 回放与 tokens/cost 头 | ◐ | `ArcAgentTimeline.cs:78-95` | 只有一条 run 的回放；无历史检索、无对比 |
| `eval-suite` | Blocks.AI | 版本 vs 基线的用例结果、回归标注 | ◐ | `EvalSuitePage.cs:53-55`（"结果样例；Run 重置回同一完成态并标注 simulated；triage 只改行"） | 无"把一次真实失败变成 eval 用例"的通路 |
| `usage-meter` | Feedback | 用量条（分类/上限/阈值） | ● | `ArcUsageMeter.cs:31-54` | 不懂"单次 run 预算"与"硬停" |
| `usage-billing` / `usage-forecast` | Blocks.Commerce/Data | 账单投影、预算拖拽 | ◐ | `usage-billing`、`usage-forecast` catalog 项 | 账单视角，不是运行中护栏 |
| `activity-heatmap` | Feedback | 全年活动热图 | ● | `ArcActivityHeatmap.cs` | 可复用为"Agent 每周活动/失败"汇总，但无 Agent 语义 |
| `agent-audit-log` / `run-history-list` | — | **不存在** | — | — | 复盘缺台账与历史入口 |

### 2.8 横切能力盘点

| 能力 | 现有项 | 支持度 | 关键证据 | 缺口 |
| --- | --- | --- | --- | --- |
| 通知触达 | `toast`、`toast-stack`、`notification-center` | ◐ | `ArcToastHost.cs:15-19`（"页面放一个 host 并调用 Show"）；`NotificationCenterPage.cs:50-51`（"样例数据、read state 本地、参考实现用 portal，移植版放在 block 内"） | 无 OS 通知、无托盘红点、无投递策略、无静默时段、无升级规则 |
| 成本 | `usage-meter`、`usage-billing`、`usage-forecast`、`agent-timeline` 头 | ◐ | 同上 | 无运行中预算、无硬停、无按任务归集 |
| 权限与安全 | `security-settings`、`env-variables`、`roles-permissions`、`api-keys` | ◐ | `SecuritySettingsPage.cs:52-54`（样例数据、本地验证码）；`EnvVariablesPage.cs:64-66`（内存模拟） | 无工具级能力矩阵、无路径/域授权、无信任规则治理、无提示注入防护 |
| 语音 | `voice-orb`、`voice-recorder` | ◐ | `ArcVoiceOrb.cs:18-23` | 无双向会话控制条、无语音确认、无设备/权限 |
| 模型 | `model-picker`、`model-arena`、`prompt-cache`(○)、`prompt-studio` | ◐ | `ModelPickerPage.cs:51-54`；`ArcPromptCache.cs:14-24`（固定样例价格） | 无本地模型状态、无路由策略、无离线回退 |
| 后台常驻 | — | — | 全仓 grep：`TrayIcon`=0、`NativeMenu`=0、`ShutdownMode` 仅用于截图/冒烟（`Gallery/App.axaml.cs:69,105`） | 完全空白 |
| 审计 | — | — | 无 audit 控件；`timeline` 是展示型 | 完全空白 |

---

## 3. 生命周期覆盖矩阵

### 3.1 现状（● 完整 / ◐ 半覆盖 / ○ 空白）

| 阶段 | 现有主力项 | 覆盖度 | 具体缺口 | 桌面端（Avalonia）特有问题 |
| --- | --- | --- | --- | --- |
| **理解** | ai-composer ◐、ai-chat ◐、ai-side-panel ◐、model-picker ◐、voice-orb ◐ | ◐ | 结构化追问、歧义澄清、桌面上下文获取、语音事件 | 无法读取"用户正在看的窗口/选中文本"（ai-side-panel 只读自绘页面）；全局唤起缺失 |
| **计划** | agent-plan ◐、agent-orchestrator ◐、reasoning-tree ○、agent-loop ○ | ◐ | 计划审批契约、耗时/成本/风险预估、日历可行性、重规划 diff | 计划要落到系统日历/本地文件时缺授权与冲突处理；无"批准后后台执行"承载 |
| **执行** | agent-status ◐、tool-call ◐、agent-timeline ◐、agent-run ◐、agent-tasks ◐、context-window ◐ | ◐ | 事件、中断/改向、并发队列、流式增量、trace 导入 | 关窗即停；无托盘/后台；休眠唤醒后时钟/任务漂移 |
| **审批** | agent-approvals ◐、tool-call(Approval) ◐、hold-to-confirm ●、confirm-morph ●、dialog/drawer ● | ◐ | 富审批（预览/改参/范围/过期/批量）、审计留痕、失焦触达 | 审批在应用内，窗口失焦时用户看不到；系统通知权限不可控 |
| **结果** | ai-answer ◐、research-run ◐、ai-side-panel ◐、toast ●、empty-state ● | ◐ | diff/产物对象、撤销窗口、结果归档与再打开 | 打开/保存本地文件、分享到系统；无"结果通知" |
| **记忆** | agent-memory ◐、context-window ◐、knowledge-base ◐ | ◐ | 来源/置信度/冲突/作用域/批量治理/加密与导出 | 本地文件权限、加密密钥（DPAPI/Keychain/libsecret）、多设备同步 |
| **复盘** | agent-timeline ◐、eval-suite ◐、usage-meter ●、activity-heatmap ● | ◐ | run 历史、审计台账、成本归集、失败→eval 回流 | 日志落盘位置/轮转/隐私；崩溃后恢复上次 run |
| 通知触达（横切） | toast ●、notification-center ◐ | ◐ | OS 通知/托盘/路由/静默/摘要 | Avalonia 只有窗口内 `WindowNotificationManager`，无 OS 通知 API；三平台实现与权限不同 |
| 成本（横切） | usage-meter ●、usage-billing ◐、usage-forecast ◐ | ◐ | 运行中预算、硬停、降级、按 run/routine 归集 | 后台 run 超预算的触达；休眠期间计费/暂停 |
| 权限安全（横切） | security-settings ◐、env-variables ◐、roles-permissions ◐、api-keys ◐ | ◐ | 工具能力矩阵、路径/域授权、信任规则、注入防护 | 桌面进程无沙箱；OS 权限（TCC/ACL/portal）与 UI 表达不一致 |
| 语音（横切） | voice-orb ◐、voice-recorder ◐ | ◐ | 双向会话控制、打断、语音确认、设备/权限 | 麦克风权限、设备热插拔、回声消除需平台能力 |
| 模型（横切） | model-picker ◐、model-arena ◐、prompt-studio ◐ | ◐ | 本地模型、离线、路由、能力/价格数据 | 本地推理资源（RAM/VRAM/CPU）、下载与校验、隐私开关 |
| 后台常驻（横切） | — | ○ | 托盘、开机自启、关窗行为、单实例、休眠策略 | 全平台差异最大的一块 |
| 审计（横切） | — | ○ | 台账、导出、不可篡改、隐私清空 | 本地存储与加密；用户数据删除权 |

### 3.2 补齐提案后的覆盖变化

| 阶段 | 提案补强 | 预期覆盖度 |
| --- | --- | --- |
| 理解 | agent-ask-card、voice-session-bar、global-hotkey-field | ◐ → ●（结构化追问 + 语音输入 + 唤起） |
| 计划 | routine-schedule-editor（把"计划"延伸到"例行"）、agent-budget-bar（成本预估）、autonomy-dial（计划执行权限） | ◐ → ●（可批、可估、可授权） |
| 执行 | agent-transcript、run-control-bar、background-run-dock、agent-handoff-banner | ◐ → ●（事件流 + 中断/接管 + 后台可见） |
| 审批 | tool-approval-sheet、permission-scope-picker、diff-review、trust-rules-manager | ◐ → ●（富审批 + 范围 + 规则治理） |
| 结果 | diff-review（预览/逐 hunk）、checkpoint-restore（撤销） | ◐ → ● |
| 记忆 | memory-review-queue、agent-audit-log（来源证据链） | ◐ → ●（可治理） |
| 复盘 | agent-audit-log、run-trace-viewer、run-history-list | ○ → ● |
| 通知/成本/权限/语音/模型/离线 | notification-routing、agent-budget-bar、guardrail-alert、voice-session-bar、local-model-status、offline-action-queue、model-routing-policy、desktop-agent-settings | 各 ◐/○ → ● |

---

## 4. 桌面平台（Avalonia 12 / .NET 10）特有的约束

> 这些约束说明 **UI 需要展示/适配什么**；系统能力本身由宿主实现，不属于组件库交付物（边界见 `docs/06-agent-life-tools-extension.zh.md` §7）。

> 这一节是提案的硬约束来源：同一套 UI 在浏览器里可以靠 Notification API / Service Worker / localStorage 解决的事，在桌面端必须由应用自己实现。

### 4.1 没有浏览器通知，也没有内置系统通知

- Avalonia 12 只在 `Avalonia.Controls.Notifications` 里提供 `WindowNotificationManager`（**窗口内**通知条，不是系统通知），没有跨平台 OS 通知 API；仓库也没有使用（`WindowNotificationManager`=0）。`notification-center` 只是页内样例（`NotificationCenterPage.cs:50-51`），`toast` 是页面里放一个 host（`ArcToastHost.cs:15-19`）。
- 三平台 OS 通知实现不同：Windows 需要 App SDK 的 `AppNotification`（且要求打包/快捷方式/AUMID，否则静默失败）；macOS 需要 `UNUserNotificationCenter` 并请求授权、处理前台呈现策略；Linux 走 `org.freedesktop.Notifications`（DBus），不同桌面环境能力不一致。
- **UI 影响**：必须有"通知通道能力检测 + 降级"界面；审批类通知要带"立即处理"动作，并在点击后聚焦窗口与具体审批项；系统通知不可靠时，托盘红点/应用内横幅是兜底。提案：`notification-routing`（P1）、`desktop-agent-settings`（P1）、`background-run-dock`（P0）。

### 4.2 系统托盘 / 后台常驻

- Avalonia `TrayIcon` 提供图标与原生菜单，但**不支持富 popover**；要做状态面板只能创建无边框窗口贴到托盘位置（多显示器/DPI 缩放要换算）。仓库当前 0 处使用。
- 关窗行为需要显式设计：`ShutdownMode` 默认在最后一个窗口关闭时退出；后台常驻必须改为 `OnExplicitShutdown` 或隐藏主窗口 + 托盘存活，并处理开机自启、单实例、第二次启动唤起。
- **UI 影响**：`background-run-dock` 要同时设计"原生菜单版"和"窗口版"；菜单项是文字化的最小集（Pause all / Open / Quit），状态细节放窗口。Linux 某些桌面无托盘（GNOME 默认），必须降级为任务栏窗口/通知。

### 4.3 全局热键与唤起

- Avalonia 无全局热键 API；Windows 需 `RegisterHotKey` P/Invoke；macOS 可用 Carbon `RegisterEventHotKey`，若走事件 tap 则需辅助功能授权；Linux 需处理 X11/Wayland 差异（Wayland 通常不可用）。
- **UI 影响**：录制 UI（`shortcut-recorder` 已有）之外，还需要"注册结果/冲突/平台不支持"的反馈与替代唤起方式（托盘菜单、菜单栏项）。提案：`global-hotkey-field`（P2）。

### 4.4 本地文件与权限

- 桌面进程以用户身份运行，**没有应用级沙箱**：一旦授予"读写文件"能力，Agent 可以触达用户全部文件。Avalonia 不提供权限代理。
- OS 层还有额外闸门：macOS TCC（桌面/文稿/下载/全盘访问）、Windows Defender Controlled Folder Access、Linux Flatpak/Snap portal。这些权限的系统弹窗与产品内授权 UI 必须对齐，否则用户会看到"产品里已允许但系统仍拒绝"。
- **UI 影响**：权限授予要按"目录/文件类型/动作"表达（`permission-scope-picker`），拒绝后 Agent 要有降级路径与解释（`guardrail-alert`）；审计要记录路径摘要而不是完整敏感路径。

### 4.5 休眠 / 唤醒 / 时钟漂移

- `ArcCountdown` 只是显示（`ArcCountdown.cs:34-51`），`DispatcherTimer` 在系统休眠期间不走。定时 routine 必须在唤醒后**补算 misfire**、避免"睡醒后连跑五次"。
- 依赖外部系统时钟的 "2 分钟后提醒" 在跨时区/夏令时切换时要重算（`TimeZoneInfo`）。
- **UI 影响**：`routine-schedule-editor` 必须显式提供 missed/overlap 策略；`countdown` 显示要标注"基于本机时钟"；超时审批应过期而不是自动通过。

### 4.6 窗口、多显示器与浮层

- 现有 dialog/drawer/toast 都是页内（`ArcDialog.cs:25`、`ArcToastHost.cs:15-19`），没有窗口级 Host；审批/追问如果需要"浮在其他应用之上"，必须设计 `Topmost` 无边框窗口或系统通知，而不是页内浮层。
- 多显示器下托盘 popover 的定位、DPI 缩放、任务栏位置（上/下/左/右）都会影响布局。
- **UI 影响**：P0 审批单优先做"应用内 + 系统通知"，窗口级浮层作为增强；不要假设能从页内 overlay 直接跨窗口。

### 4.7 语音与设备

- `voice-orb` 麦克风电平是模拟（`ArcVoiceOrb.cs:18-23`），`voice-recorder` 没有 VAD/转写。真实语音需要：设备枚举与热插拔、麦克风权限、回声消除、TTS 播放与 barge-in（说话时打断）、离线 ASR/TTS。
- **UI 影响**：语音条必须是"事件驱动 + 宿主注入电平/转写"，控件不做音频 IO；打断要有明确的 `TtsStop` 语义。

### 4.8 离线与本地模型

- 本地推理需要下载/校验/量化/内存与显存评估；Avalonia 只负责 UI。断网时工具调用要排队/幂等；恢复后处理冲突。
- **UI 影响**：`local-model-status` 显示"能不能跑、跑哪个、数据出不出本机"；`offline-action-queue` 显示"哪些动作在等、重放会不会重复"。

### 4.9 跨平台差异速查表

| 能力 | Windows | macOS | Linux | 建议的 UI 降级 |
| --- | --- | --- | --- | --- |
| 托盘 | 通知区域，支持原生菜单 | 菜单栏项 | AppIndicator（GNOME 需扩展） | 无托盘→任务栏窗口 + 应用内横幅 |
| 系统通知 | App SDK `AppNotification`（需打包/快捷方式） | `UNUserNotificationCenter`（需授权） | DBus Notifications（能力不一） | 无系统通知→托盘红点 + 应用内审批队列 |
| 全局热键 | `RegisterHotKey` | Carbon `RegisterEventHotKey`；事件 tap 需辅助功能授权 | X11 可、Wayland 多不可 | 注册失败→提供托盘/菜单项 |
| 本地密钥 | DPAPI | Keychain | libsecret | 无密钥环→提示"本地加密不可用" |
| 文件权限 | ACL/Controlled Folder Access | TCC | portal/权限位 | 系统拒绝→引导到系统设置并解释 |

### 4.10 图标与资源依赖

`ArcIconData` 目前 161 个 Lucide 名（`src/Foundation/Arc.Avalonia/Icons/ArcIconData.cs:10-11`）。本次 28 项提案需要但**当前缺失**的图标（按语义需要）：`bot`、`brain`、`history`、`rotate-ccw`、`timer`、`gauge`、`wallet`、`coins`、`git-branch`、`git-merge`、`workflow`、`stop-circle`、`circle-pause`、`calendar-clock`、`hard-drive`、`wifi-off`、`cloud-off`、`shield-alert`、`list-checks`、`hand`、`user-check`、`volume-2`、`file-text`、`ghost` 等。建议作为 foundation 附属任务（S，≤1 人日）补齐，并纳入 icon 审计，避免实现阶段才卡住。

---

## 5. 关键缺口专题（14 个）

> 每个专题：现象 → 证据 → 为什么现成组件不够 → 对应提案 → 验收要点。

### 专题 1：流式输出与打断（P0）

- **现象**：`ai-chat` 的流式是"成品态"（`AiChatPage.cs:53-57`），`ai-composer` 的 Stop 是注释（`AiComposerPage.cs:54-57`），`chat-thread` 每次变化整树重建（`docs/04:243`；`ArcChatThread.cs:467`）。
- **为什么现有不够**：流式不是"逐字改一个 TextBlock"，而是需要增量事件（append/update/done）、滚动锚定（用户上翻时不抢滚动）、停止/重生成/编辑分支、以及性能上的增量与虚拟化。现有 `ChatThread` 只有简单消息模型（`ArcChatThread.cs:26-111`），无法容纳 tool/plan/approval 事件。
- **提案**：`agent-transcript`（P0）、`run-control-bar`（P0）。
- **验收**：① 追加第 1001 条事件不触发既有事件重建（以控件构建计数断言）；② 用户上翻时新事件不抢滚动，出现"跳到最新"pill；③ 停止支持 Immediate / Finish step 两种语义；④ 停止后消息标"已停止"且可重生成；⑤ 键盘：Esc 停止、`↑` 编辑上一条、`Ctrl+Enter` 重生成。

### 专题 2：工具调用预演 / 审批（P0）

- **现象**：`ArcToolCall` 内置 Allow/Deny/Retry 直接改数据（`ArcToolCall.cs:637-678`）；`agent-approvals` 参数只读、`Always allow` 只加 chip、不触达 agent（`AgentApprovalsPage.cs:52-55`）。
- **为什么现有不够**：生活助手的审批需要"将要发生什么"的证据：参数、目标对象、金额、影响的日历事件、文件 diff、可逆性、有效期。只读参数行 + 两个按钮无法支撑"用户真的看懂并负责"。而且审批必须能被宿主拒绝/超时/撤销，不能由控件自嗨。
- **提案**：`tool-approval-sheet`（P0）、`permission-scope-picker`（P1）、`diff-review`（P1）、`trust-rules-manager`（P1）。
- **验收**：① 审批单显示意图、参数（可改）、副作用预览、风险与不可逆标记、过期倒计时；② Decision 四态（Approve / Deny / Approve with edits / Delegate）都有事件；③ 改参后以新参数重放，原调用标 Superseded；④ 过期自动 Deny 并通知；⑤ 审批结果写入审计（谁、何时、基于什么证据）。

### 专题 3：定时与后台例行任务（P0）

- **现象**：`agent-routines` 的排期编辑器只改句子与倒计时字符串（`AgentRoutinesPage.cs:52-54`、`:195-211`），Run now 立即完成、无流式报告；`ArcCountdown` 是纯展示；全仓没有调度器/托盘。
- **为什么现有不够**：例行任务是桌面助手的"心跳"：需要 RRULE、时区、下一次运行预览、错过/重叠策略、后台执行、结果投递、失败重试、与通知/成本/审批联动。现在连"下次什么时候跑"都是一个写死的字符串。
- **提案**：`routine-schedule-editor`（P0）、`background-run-dock`（P0）、`desktop-agent-settings`（P1）、`notification-routing`（P1）。
- **验收**：① 人类句子与 RRULE 双向同步；② 预览 next 5 runs，跨时区/夏令时正确；③ 休眠 2 小时后只补跑一次并说明；④ 关窗后 routine 仍执行，托盘可见、可暂停；⑤ 每次运行结果进入 `agent-attention-inbox` 待复核。

### 专题 4：Agent 记忆浏览 / 编辑 / 遗忘（P1）

- **现象**：`agent-memory` 支持筛选/搜索/编辑/忘记，但 Forget 立即删除、无撤销（`AgentMemoryPage.cs:53-55`），没有来源、置信度、冲突与批量治理。
- **为什么现有不够**：长期记忆是生活助手的信任资产，也是最容易"记错"的地方。用户需要知道"为什么记住这条、从哪句话记住的、和哪条冲突、作用域是个人还是家庭"，并且能低成本纠正。现有页面是"清单编辑器"，不是"记忆治理"。
- **提案**：`memory-review-queue`（P1）；来源证据可复用 `json-viewer`/`tree-view`，审计走 `agent-audit-log`。
- **验收**：① 每条记忆可追溯到 ≤3 条来源对话/文档；② 冲突项并排显示并可选保留/合并；③ 删除 5 秒撤销；④ 支持作用域（个人/家庭/项目）与过期；⑤ 一键导出/全删，并记录删除动作本身。

### 专题 5：多步工作流可视化与重试（P0/P1）

- **现象**：`agent-timeline` 是只读回放（`ArcAgentTimeline.cs:78-95`），`agent-orchestrator` 是静态快照且不画连线（`AgentOrchestratorPage.cs:53-56`），`agent-plan` 的 Retry/Skip 只改自己的 step 状态（`ArcAgentPlan.cs:395-412`）。
- **为什么现有不够**：用户需要在运行中看到"哪条分支在跑、为什么慢、失败卡在哪、重试会影响谁"，并能从失败节点重跑而不是整轮重来。现有可视化不能点击、不能取消、不能从 span 重试。
- **提案**：`agent-transcript`（P0，事件流）、`run-trace-viewer`（P2，span 瀑布）、`run-history-list`（P2）、`checkpoint-restore`（P1）。
- **验收**：① 失败节点可"仅重跑此步/重跑下游"；② 跳过的分支在时间轴上可辨认；③ 并行 lane 的取消只影响自己的下游；④ 每一步显示重试次数与原因；⑤ 运行结束后可从历史列表重放。

### 专题 6：失败恢复与回滚（P1）

- **现象**：`agent-run` 停在 diff 上等人批准（`AgentRunPage.cs:51-54`），但批准后的"撤销"没有机制；`ai-side-panel` 的 Undo 只改页面样例（`AiSidePanelPage.cs:51-55`）；状态枚举里有 Failed/Stopped，但没有宿主事件。
- **为什么现有不够**：生活助手的动作分三类——可逆（改日历）、可补偿（发邮件后补发更正）、不可逆（付款/提交表单）。没有检查点与影响范围预览，用户只能"事后发现"；而不可逆动作必须在执行前就标出来（与专题 2 联动）。
- **提案**：`checkpoint-restore`（P1）、`guardrail-alert`（P1）、`offline-action-queue`（P1，断网重放）。
- **验收**：① 每个动作前自动建检查点（含受影响对象 ID）；② 恢复前列出将撤销/无法撤销的副作用；③ 部分成功标 Partial 并给人工补救指引；④ 已发出邮件等不可逆项只允许"发更正"而不假装能撤回。

### 专题 7：权限与沙箱提示（P1 信任基础）

- **现象**：`security-settings` 只覆盖账号安全（`SecuritySettingsPage.cs:52-54`），`env-variables` 只是密钥样例（`EnvVariablesPage.cs:64-66`），没有工具能力矩阵、路径授权、信任规则；桌面进程本身无沙箱。
- **为什么现有不够**：Agent 生活助手的权限是**按能力与范围**的（读哪个目录、能否联网、能否支付、能否发消息），而不是"账号安全"。用户需要在首次授权、单次审批、长期规则三个层面被尊重，并且能随时收紧。
- **提案**：`permission-scope-picker`（P1）、`trust-rules-manager`（P1）、`autonomy-dial`（P1）、`guardrail-alert`（P1）、`desktop-agent-settings`（P1）。
- **验收**：① 首次授权按域列出能力与后果；② 每次文件访问显示"哪个目录、读/写、一次/长期"；③ 拒绝后 Agent 有降级路径（询问替代方案）；④ 长期规则可审查/撤销/过期；⑤ 敏感动作默认不信任外部内容（提示注入隔离标记）。

### 专题 8：成本 / 延迟预算（P1）

- **现象**：`agent-timeline` 只在头部汇总 tokens/cost（`ArcAgentTimeline.cs:78-95`），`usage-meter` 是账单视角（`ArcUsageMeter.cs:31-54`），没有运行中预算条与硬停；`usage-billing`/`usage-forecast` 是商业账单页样例。
- **为什么现有不够**：常驻 routine + 长任务会让成本失控；用户需要"这条 run 最多花多少、超了怎么办"，以及"本地/便宜模型能不能顶替"。预算不是账单，是**运行时策略**。
- **提案**：`agent-budget-bar`（P1）、`model-routing-policy`（P2）、`local-model-status`（P1）。
- **验收**：① 预算快照按 run/routine 展示已用/预算/预测；② 70/90/100% 三档文案与动作（暂停/降级/继续）；③ 超限事件即使窗口在后台也能触达；④ 每次 run 的 cost 可归因到 routine 与模型；⑤ 路由规则可预估月成本。

### 专题 9：通知与打扰控制（P1）

- **现象**：`notification-center` 是页内样例（`NotificationCenterPage.cs:50-51`），`toast` 只能页内（`ArcToastHost.cs:15-19`），无 DND/静默/路由/升级。
- **为什么现有不够**：桌面助手 7×24 运行，打扰控制是生死线：审批要打扰、例行结果不该打扰、失败要升级、夜间要静默、摘要要合并。没有投递策略，用户两天就会关掉通知，然后错过审批。
- **提案**：`notification-routing`（P1）、`agent-attention-inbox`（P0）、`background-run-dock`（P0）。
- **验收**：① 事件类型 × 通道矩阵可配置；② 静默时段内仅"紧急审批"可升级，且升级需二次确认；③ 每日摘要合并非紧急项；④ 托盘红点与未读一致；⑤ 每个通道显示可用性（权限/平台）。

### 专题 10：双向语音（P1）

- **现象**：`voice-orb` 有 4 态但无事件、麦克风模拟（`ArcVoiceOrb.cs:9-23`、`:50-101`）；`voice-recorder` 只录音。没有常驻控制条、打断、语音确认。
- **为什么现有不够**：生活助手的语音是"免手 + 随时打断"：说"停"要停 TTS，说"不对"要改方向，敏感动作用语音复述确认。全屏 orb 适合展示，不适合常驻。
- **提案**：`voice-session-bar`（P1）、`spoken-confirmation`（P2）。
- **验收**：① 收到 barge-in 后 UI 一帧内进入 Listening 并发出 `TtsStop`；② 实时转写单行 + 可纠错；③ 免提/Push-to-talk 两态；④ 敏感动作读回 + 复述匹配；⑤ 设备/权限错误有明确文案。

### 专题 11：离线 / 本地模型（P1）

- **现象**：`model-picker` 只选模型名（`ModelPickerPage.cs:51-54`），无本地模型状态、无离线队列、无隐私标识。
- **为什么现有不够**：桌面助手的差异化是"数据不出本机 + 断网可用"；这需要模型运行时状态、任务可离线性判断、动作排队与恢复冲突解决，三者都是 UI 契约。
- **提案**：`local-model-status`（P1）、`offline-action-queue`（P1）、`model-routing-policy`（P2）。
- **验收**：① 本地模型显示下载/校验/内存适配/隐私；② 断网时明确哪些任务可用、哪些排队；③ 恢复后重放幂等（不重复下单）；④ 冲突项让用户选择保留版本；⑤ 回退云端必须再次提示隐私。

### 专题 12：日志与审计（P1）

- **现象**：`timeline` 是展示组件，`agent-tasks`/`eval-suite` 全是样例；没有任何 audit/run history。
- **为什么现有不够**：家庭共享设备上，"Agent 为什么给这个号码发了短信"必须可回答；这也是出问题后唯一能复盘与追责的入口。审计要求 append-only、可筛选、可导出、可删除但删除留痕。
- **提案**：`agent-audit-log`（P1）、`run-trace-viewer`（P2）、`run-history-list`（P2）、`eval-suite` 作为质量回流（把失败变成用例）。
- **验收**：① 每条记录含 actor/意图/工具/参数摘要/策略与审批引用/结果/成本/时间；② 可导出 JSONL/CSV；③ 支持按域/风险/结果筛选；④ 删除数据后保留"删除动作"记录；⑤ 失败可一键加入 eval 用例。

### 专题 13：桌面通知触达（P0 配套）

- **现象**：见 §4.1；现有 `toast`/`notification-center` 都不能在窗口失焦时触达。
- **为什么现有不够**：审批如果只能"打开应用才看到"，那桌面助手就退化成"另一个需要盯着的窗口"。P0 的 `tool-approval-sheet`、`agent-attention-inbox` 都必须配一条系统通知链路。
- **提案**：`notification-routing`（P1）+ `background-run-dock`（P0）+ 平台通知封装（作为 foundation 运行时能力，非 UI 项；在 `desktop-agent-settings` 里做能力自检）。
- **验收**：① 窗口最小化/关窗时审批仍能触达；② 点击通知直达具体审批项；③ 权限未授予时明确引导；④ 不允许"静默失败"（发送失败要有应用内替代）。

### 专题 14：统一注意力队列与多 Agent 并发（P0）

- **现象**：审批（agent-approvals）、追问（无）、失败（agent-run/tasks 样例）、routine 到期（agent-routines 样例）分散在不同页面；`agent-orchestrator` 是静态快照。
- **为什么现有不够**：真实使用中用户面对的是"同时在跑的 3 个 agent + 2 个 routine + 1 个等你审批的付款"，需要一个按阻塞度排序的队列，而不是在多个页面间找红点。多 agent 并发还要求每个 run 可独立暂停/接管，而不是只显示一个 agent-status。
- **提案**：`agent-attention-inbox`（P0）、`run-control-bar`（P0）、`agent-handoff-banner`（P1）、`background-run-dock`（P0）。
- **验收**：① 5 个并发 run 下队列稳定、阻塞项置顶；② 批量批准只对同 tool+scope 生效；③ 每个 run 可单独暂停/接管；④ 处理完自动从队列移除并可撤销；⑤ 托盘图标反映"有阻塞"状态。

---

## 6. 与生活内容域的分工边界

- 本文件**不**提出健康/财务/出行/饮食/家庭/记录等生活内容组件（由 `02-life-gaps.zh.md` 负责）。凡是"某个生活场景需要的 Agent 运行表面"，以本文件的提案为底座，由 02 按场景组合。
- 需要协同的共用件：`week-calendar`/`calendar`/`time-picker`（排期）、`notification-center`（收件箱）、`settings-page`（设置）、`data-grid`/`filter-toolbar`（列表与筛选）、`timeline`（事件）、`hold-to-confirm`（确认手势）。本文件的提案尽量只引用这些现有 slug，不重复造轮子。
- 命名与分类：本文件 28 项全部沿用现有 34 个分类（23 component + 5 special group + 6 block group），不新增分类，便于与 01/02 合并进同一份扩展目录。
- 与 `02-life-proposals.json`（24 项）的**概念边界**（最终校验：slug 零重叠，见 §11.3）：

| 本文件提案/场景 | 02 的邻近项 | 边界 |
| --- | --- | --- |
| `agent-attention-inbox`（Agent 待处理动作） | `reminder-center`（生活提醒内容） | 前者是"审批/追问/失败"等 Agent 阻塞项，后者是"买药/缴费/取件"等生活事项；分组与批量交互共用模式，数据模型不合并 |
| `routine-schedule-editor`（Agent 例行任务排期） | `medication-schedule`、`reminder-row`（生活提醒排期） | 前者产出 `ScheduleSpec` 驱动 Agent 执行，后者是提醒条目的时间与重复标记 |
| `notification-routing`（投递策略） | `reminder-center`（提醒收件箱） | 前者决定"走哪个通道、是否静默"，后者是提醒内容的呈现与延后 |
| `focus-hours` 场景（Agent 打扰策略） | `focus-session`（专注会话） | 前者是系统级 DND/升级规则，后者是用户主动开始的专注计时与会话记录 |
| `background-run-dock`（跨窗口常驻） | `control-center`（页内快捷设置） | 前者是关闭主窗口后的托盘/菜单栏存在，后者是页面内快捷控制 |

两份提案可以同批合并：本文件的 `agent-*` 运行时组件为 02 的生活场景提供"Agent 在跑、在等、在问"的底座；02 的生活内容块负责"跑什么、提醒什么"。

---

## 7. 提案总览（28 项）

> 完整字段见 `03-agent-proposals.json`。P0=不补就无法做出可用的桌面 Agent 生活助手；P1=显著提升；P2=锦上添花。工作量按仓库"控件 + ControlTheme + 页面 + 明暗截图"的完成定义：S≤1 人日、M=2~4 人日、L=1~2 周。

### 7.1 P0（7 项，核心可用性）

| # | slug | 类型 | 分类 | 落地项目 | 工作量 | 补哪个阶段 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `agent-transcript` | component | Activity | Arc.Avalonia.Content | M | 执行/结果/复盘（事件流底座） |
| 2 | `run-control-bar` | special | AI widgets | Arc.Avalonia.Special.AIWidgets | M | 执行（中断/改向/接管） |
| 3 | `tool-approval-sheet` | special | AI widgets | Arc.Avalonia.Special.AIWidgets | L | 审批（高风险动作） |
| 4 | `agent-ask-card` | special | AI widgets | Arc.Avalonia.Special.AIWidgets | M | 理解（结构化追问） |
| 5 | `agent-attention-inbox` | block | AI | Arc.Avalonia.Blocks.AI | M | 审批/复盘（统一注意力） |
| 6 | `routine-schedule-editor` | component | Pickers | Arc.Avalonia.Pickers | L | 计划（例行排期） |
| 7 | `background-run-dock` | special | AI widgets | Arc.Avalonia.Special.AIWidgets | L | 执行（后台常驻） |

### 7.2 P1（16 项，体验与信任）

| # | slug | 类型 | 分类 | 落地项目 | 工作量 | 补哪个阶段 |
| --- | --- | --- | --- | --- | --- | --- |
| 8 | `permission-scope-picker` | component | Selects | Arc.Avalonia.AdvancedInputs | S | 审批/权限 |
| 9 | `agent-budget-bar` | component | Progress | Arc.Avalonia.Feedback | S | 成本 |
| 10 | `diff-review` | component | Editors | Arc.Avalonia.Content | M | 审批/结果 |
| 11 | `checkpoint-restore` | special | AI widgets | Arc.Avalonia.Special.AIWidgets | M | 失败恢复 |
| 12 | `guardrail-alert` | component | Messages | Arc.Avalonia.Actions | S | 安全 |
| 13 | `connector-health` | block | AI | Arc.Avalonia.Blocks.AI | M | 执行/复盘 |
| 14 | `memory-review-queue` | special | AI widgets | Arc.Avalonia.Special.AIWidgets | M | 记忆 |
| 15 | `autonomy-dial` | component | Toggles | Arc.Avalonia.Inputs | S | 权限 |
| 16 | `notification-routing` | block | App shell and settings | Arc.Avalonia.Blocks.App | M | 通知 |
| 17 | `agent-audit-log` | block | AI | Arc.Avalonia.Blocks.AI | M | 复盘/审计 |
| 18 | `voice-session-bar` | special | Media and input | Arc.Avalonia.Special.Media | M | 语音 |
| 19 | `local-model-status` | special | AI widgets | Arc.Avalonia.Special.AIWidgets | M | 离线/模型 |
| 20 | `agent-handoff-banner` | component | Messages | Arc.Avalonia.Actions | S | 执行（人工接管） |
| 21 | `offline-action-queue` | block | AI | Arc.Avalonia.Blocks.AI | M | 离线 |
| 22 | `trust-rules-manager` | block | AI | Arc.Avalonia.Blocks.AI | M | 权限 |
| 23 | `desktop-agent-settings` | block | App shell and settings | Arc.Avalonia.Blocks.App | M | 后台常驻 |

### 7.3 P2（5 项，锦上添花）

| # | slug | 类型 | 分类 | 落地项目 | 工作量 |
| --- | --- | --- | --- | --- | --- |
| 24 | `run-trace-viewer` | special | AI widgets | Arc.Avalonia.Special.AIWidgets | L |
| 25 | `model-routing-policy` | block | AI | Arc.Avalonia.Blocks.AI | L |
| 26 | `global-hotkey-field` | component | Special inputs | Arc.Avalonia.AdvancedInputs | S |
| 27 | `spoken-confirmation` | component | Gestures | Arc.Avalonia.Media | S |
| 28 | `run-history-list` | block | AI | Arc.Avalonia.Blocks.AI | M |

**分布**：kind = component 10 / special 9 / block 9；priority = P0 7 / P1 16 / P2 5；effort = S 7 / M 16 / L 5。粗略工作量（S=1、M=3、L=7 人日）≈ **90 人日**，其中 P0 ≈ **33 人日**（不含 foundation 的通知/托盘/调度运行时，非 UI 工作另计）。

> 口径说明：合并目录 `docs/arc-extension-catalog.json` 统一按 **L=8 人日** 计（与主报告 §0.4 / §7.1 定义一致），因此本表的 28 项在目录中合计约 **95 人日**；L=7 只用于本文件内部的粗算排序，不影响主报告数字。

**为什么 P0 只覆盖"理解/计划/执行/审批"**：这四段是"Agent 能不能被信任地跑起来"的地基——没有事件流、没有中断、没有追问、没有审批、没有排期、没有后台存在感，产品无法成立。**结果/记忆/复盘暂不列 P0**，因为 MVP 可以先用现有 `timeline`/`agent-memory` 样例 + 手工流程兜底（能跑，但不好用）；一旦目标包含"家庭共享、多用户、合规审计、长期陪伴"，`agent-audit-log`、`memory-review-queue`、`diff-review` 应同步升为 P0。

---

## 8. P0 提案详解（Anatomy / 状态枚举 / 验收）

### 8.1 `agent-transcript`（component · Activity · Arc.Avalonia.Content · M）

**设计意图**：给 Agent 一个可嵌入、可增量、可虚拟化的统一事件流。它是所有运行表面的底座：`agent-status` 展开的步骤、`tool-call`、计划、审批、追问、结果都作为事件渲染在同一个列表里。

**为什么现成组件不够**：`chat-thread` 只有参与者/消息/typing/附件模型（`ArcChatThread.cs:26-111`），没有工具/计划/审批/追问事件；`ai-chat` 是整页 block；两者都按"状态变→重建"实现（`docs/04:243`、`:348-357`）。

**Anatomy**

```
AgentTranscript (TemplatedControl)
├─ ScrollViewer (竖向，stick-to-bottom 受控)
│  ├─ TranscriptEventView × N        // 按事件类型选择模板
│  │  ├─ EventHeader                 // 角色/图标/时间/折叠
│  │  ├─ MessageBody                 // markdown 子集（复用 ai-chat 渲染思路）
│  │  ├─ ThinkingBody                // 折叠推理，默认收起
│  │  ├─ ToolEventHost               // 复用 tool-call 行
│  │  ├─ PlanEventHost               // 复用 agent-plan 摘要
│  │  ├─ ApprovalEventHost           // 内联审批摘要 → 打开审批单
│  │  ├─ QuestionEventHost           // 内联追问摘要 → 打开 ask-card
│  │  ├─ ResultEventHost             // 结果对象卡（文件/事件/订单）
│  │  └─ ErrorEventHost              // 错误 + Retry/Skip
│  └─ NewOutputPill (浮在底部)        // "3 条新输出 ↓"
├─ TranscriptFooter                  // 输入区插槽（复用 ai-composer）
└─ JumpToLatest (右上/底部)
```

**状态枚举**

| 枚举 | 值 |
| --- | --- |
| `ArcTranscriptEventKind` | `Message` / `Thinking` / `Tool` / `Plan` / `Approval` / `Question` / `Result` / `Error` / `Checkpoint` / `Handoff` |
| `ArcTranscriptEventState` | `Pending` / `Streaming` / `WaitingApproval` / `WaitingInput` / `Done` / `Failed` / `Skipped` / `Cancelled` |
| `ArcTranscriptFollowMode` | `Following` / `Detached`（用户上翻）/ `Jumping` |

**交互与可访问性**：事件按 roving tab index 遍历；`Ctrl+End` 回到底部；新事件到达用 polite live region 仅播报状态变化，不逐字播报；每条事件有唯一 `AutomationProperties.Name`；流式文本只在停止后整体播报。

**验收**：见专题 1 的 5 条；另加：① 1000 条事件下滚动不卡（复用虚拟化，遵守 docs/04 R4 的结论）；② 事件模板可扩展（宿主能注册自定义 kind）；③ 卸载时停掉所有定时器（docs/04 R5）。

---

### 8.2 `run-control-bar`（special · AI widgets · Arc.Avalonia.Special.AIWidgets · M）

**设计意图**：把"停止/暂停/改向/接管"从控件内部状态提升为**运行指令**，让用户随时能控制。

**为什么现成组件不够**：`agent-status` 的 Pause/Stop 改自己的属性（`ArcAgentStatus.cs:500-534`），`ai-composer` 的 Stop 只有外观（`AiComposerPage.cs:54-57`）；没有"安全点停止"和"插入新指令"的概念。

**Anatomy**

```
RunControlBar (special / 自绘 + 组合)
├─ Left: AgentMark + 当前步骤/耗时/成本（复用 agent-status 的紧凑信息）
├─ Center: 进度细线与当前 step 名
├─ Right 主按钮: Stop ▾（Immediate / After this step）
├─ Right: Pause / Resume
├─ Right: Steer（展开一行输入：插队新指令）
├─ Right: Take over（切换人工接管）
└─ Overflow: Open run / Open transcript
```

**状态枚举**

| 枚举 | 值 |
| --- | --- |
| `ArcRunControlState` | `Running` / `FinishingStep` / `Pausing` / `Paused` / `Stopping` / `Stopped` / `HandedOver` / `Resuming` |
| `ArcStopScope` | `Immediate` / `AfterStep` |
| `ArcSteerScope` | `NextStep` / `ReplaceRemaining` |

**事件契约**：`StopRequested(StopScope)`、`PauseRequested`、`ResumeRequested`、`SteerRequested(text, SteerScope)`、`TakeoverRequested`、`OpenRunRequested`。控件不自行把状态改成 Stopped——由运行时确认后回写。

**桌面约束**：Esc 绑定停止（需二次确认，避免误触）；停止不可逆操作时接 `hold-to-confirm`；窗口失焦时该条不占焦点。

**验收**：① 两种停止范围（Immediate / After step）都有事件且可取消；② 停止过程中按钮显示 Stopping 且不可重复触发；③ 改向输入 200 字内可提交并有"将替换剩余步骤"提示；④ 接管后 Agent 状态转 HandedOver 并在 transcript 留痕；⑤ 键盘全路径可达。

---

### 8.3 `tool-approval-sheet`（special · AI widgets · Arc.Avalonia.Special.AIWidgets · L）

**设计意图**：把"要不要允许这个工具调用"升级为"看清将要发生什么、可改参、可限定范围、可过期"的审批 surface，是生活助手信任的核心。

**为什么现成组件不够**：`agent-approvals` 参数只读、无预览、无过期、`Always allow` 只是 chip 且不触达 agent（`AgentApprovalsPage.cs:52-55`）；`tool-call` 只适合低风险行内 Allow/Deny。

**Anatomy**

```
ToolApprovalSheet (从右侧或底部升起；页内 + 可选窗口级)
├─ Header: 工具 mark + 工具名 + 风险等级 + 有效期倒计时
├─ Intent: 一句话"Agent 想做什么、为什么"（来自模型的 reason）
├─ ArgumentsEditor: 参数表（可编辑字段 + 校验 + 恢复原始值）
├─ EffectPreview:
│  ├─ 目标对象摘要（账户/联系人/事件/文件）
│  ├─ DiffPreview（只读片段；P1 升级为 diff-review）
│  ├─ 金额/时间/收件人等高亮关键字段
│  └─ 可逆性标记（可撤销/可补偿/不可逆）
├─ Scope: permission-scope-picker（Once / Task / 1h / Always + 约束）
├─ DecisionBar: Deny / Approve with edits / Approve / Delegate
└─ Footnote: 本次决定会写入审计；过期自动拒绝
```

**状态枚举**

| 枚举 | 值 |
| --- | --- |
| `ArcApprovalState` | `Pending` / `EditingArgs` / `Submitting` / `Approved` / `Denied` / `Expired` / `Delegated` / `Superseded` |
| `ArcApprovalRisk` | `Low` / `Medium` / `High` / `Blocked` |
| `ArcApprovalScope` | `Once` / `Task` / `Session`（1 小时）/ `Always` |

**验收**：见专题 2 的 5 条；另加：① 参数编辑后"批准"发送的是新参数且原调用标 Superseded；② 高风险（付款/删除/发送）默认聚焦 Deny，需 hold-to-confirm；③ 过期后 Sheet 保留只读快照并可"重新请求"；④ 审批结果、耗时、范围全部进入审计；⑤ 键盘：`a` 批准、`d` 拒绝、`Esc` 关闭（关闭≠拒绝，需明确文案）。

---

### 8.4 `agent-ask-card`（special · AI widgets · Arc.Avalonia.Special.AIWidgets · M）

**设计意图**：当 Agent 需要用户补充信息（选日期、选卡、上传单据、二选一）时，用结构化卡片替代"再发一条聊天消息"。

**为什么现成组件不够**：`ai-chat` 只有文本输入；`dialog` 是通用模态，没有"问题/字段/延后/跳过"语义；审批单解决"授权"，不解决"补信息"。

**Anatomy**

```
AgentAskCard (页内卡片；可由 transcript 内联或独立呈现)
├─ Header: 问题标题 + Agent mark + 为什么需要 + 有效期
├─ Fields: 依据 field type 渲染
│  ├─ Choice / MultiChoice（radio-cards / checkbox）
│  ├─ Date / DateRange（calendar / date-range-picker）
│  ├─ File（file-dropzone）
│  ├─ Amount（money-input）
│  ├─ Confirm（confirm-morph）
│  └─ Text（input / textarea）
├─ ValidationHint（字段级）
└─ Actions: Answer / Skip / Ask me later / Answer in chat
```

**状态枚举**

| 枚举 | 值 |
| --- | --- |
| `ArcAskState` | `Asking` / `Validating` / `Answered` / `Skipped` / `Deferred` / `Expired` / `Cancelled` |
| `ArcAskFieldType` | `Text` / `Choice` / `MultiChoice` / `Date` / `DateRange` / `File` / `Amount` / `Confirm` |

**验收**：① 每种字段类型有校验与键盘路径；② 跳过/延后都要回传事件并让 Agent 有降级策略；③ 延后到期自动 Expired 并进 attention inbox；④ 回答作为结构化 payload（不是拼字符串）；⑤ 同一问题不重复轰炸（合并显示）。

---

### 8.5 `agent-attention-inbox`（block · AI · Arc.Avalonia.Blocks.AI · M）

**设计意图**：一个常驻的"等你处理"队列，将审批、追问、失败、到期 routine、待复核结果统一排序，替代在多个页面之间找红点。

**为什么现成组件不够**：`notification-center` 是通知收件箱（已读/未读、展开、批量已读，`NotificationCenterPage.cs:50-51`），不区分"必须处理"和"知会"；`inbox-triage` 是邮件归档/延后；`agent-approvals` 只是单页队列样例。

**Anatomy**

```
AgentAttentionInbox (block 页面或侧栏)
├─ Header: 未处理计数 + 批量操作（全部延后 / 静默 1h）
├─ FilterChips: 全部 / 阻塞 / 需授权 / 需补充信息 / 失败 / 待复核
├─ GroupedList (按 run/routine 分组)
│  └─ AttentionRow
│     ├─ 类型图标 + 标题 + 来源（run/routine）
│     ├─ 为什么需要你 + 已等待时长 + 到期
│     ├─ 预计人工耗时
│     └─ 行内动作（Approve / Answer / Retry / Open）
└─ EmptyState: "没有等你处理的事" + 最近自动完成
```

**状态枚举**：`ArcAttentionKind` = `Blocked` / `Approval` / `Question` / `Failure` / `Review` / `Info`；`ArcAttentionState` = `New` / `Seen` / `Acting` / `Resolved` / `Snoozed` / `Expired`。

**验收**：见专题 14；另加：① 批量批准只合并同 tool+scope，不同 scope 弹出差异确认；② 处理后 5 秒内可撤销；③ 托盘红点数与队列一致；④ 空态有"最近自动完成"以避免"Agent 是不是没工作"的焦虑。

---

### 8.6 `routine-schedule-editor`（component · Pickers · Arc.Avalonia.Pickers · L）

**设计意图**：把"每个工作日 9 点"从字符串变成可执行、可预览、可处理异常的排期。

**为什么现成组件不够**：`agent-routines` 只改句子和倒计时（`AgentRoutinesPage.cs:52-54`、`:195-211`）；`countdown` 是展示（`ArcCountdown.cs:34-51`）；`calendar`/`time-picker` 只能选单个时间点。

**Anatomy**

```
RoutineScheduleEditor (TemplatedControl，可放进 dialog/drawer)
├─ SentencePreview: "每个工作日 09:00，在 30 分钟内完成"（实时更新）
├─ Mode: 每天 / 每周几 / 每月几号 / 每 N 小时 / 自定义 RRULE
├─ Time & Timezone: time-picker + 时区选择（默认本机）
├─ NextRuns: 未来 5 次运行（含跨时区/DST 提示）
├─ Policy:
│  ├─ MissedRun: 补跑一次 / 跳过 / 询问我
│  ├─ Overlap: 跳过 / 排队 / 并行（展示风险）
│  ├─ RequirePresence: 需要我在场
│  └─ QuietHours: 命中静默时段则延后
└─ Advanced: RRULE 文本（双向同步）+ 校验
```

**状态枚举**：`ArcScheduleEditorState` = `Editing` / `Valid` / `Invalid` / `Previewing` / `Saving`；`ArcMisfirePolicy` = `RunOnce` / `Skip` / `Ask`；`ArcOverlapPolicy` = `Skip` / `Queue` / `Parallel`。

**验收**：① 5 次预览在跨时区与 DST 边界正确；② 休眠唤醒后 misfire 按策略处理并提示；③ RRULE 与句子双向同步无漂移；④ 非法组合（如"每 5 分钟且需要我在场"）给出解释而不是禁用；⑤ 输出 `ScheduleSpec` 供运行时消费。

---

### 8.7 `background-run-dock`（special · AI widgets · Arc.Avalonia.Special.AIWidgets · L）

**设计意图**：让关闭主窗口后的 Agent 仍可被看见、被暂停、被唤回——桌面助手的"存在感"。

**为什么现成组件不够**：全仓没有 `TrayIcon`（grep=0）；`agent-status` 只活在页面里；`control-center` 是页内快捷设置（Special.Glass），不能跨窗口常驻。

**Anatomy**

```
BackgroundRunDock
├─ 托盘图标（平台原生）：状态点（运行/需要注意/暂停/错误）
├─ 原生菜单（最小集）：Open / Pause all / Quiet 1h / Quit
└─ 富 popover（自建无边框窗口贴托盘）
   ├─ 当前 run 列表（agent-status 紧凑态 ×N）
   ├─ 队列与预计时间
   ├─ 今日成本/预算（复用 agent-budget-bar 摘要）
   ├─ 一键 Pause all / Resume
   └─ Open last result / Open attention inbox
```

**状态枚举**：`ArcDockState` = `Idle` / `Running` / `NeedsAttention` / `Paused` / `Error` / `Syncing`；`ArcTrayAvailability` = `NativeTray` / `RichWindow` / `FallbackWindow` / `Unavailable`。

**桌面约束**：见 §4.1/4.2；Linux 无托盘时降级为任务栏窗口；多显示器/DPI 缩放换算；托盘菜单不可含富文本。

**验收**：① 关窗后进程仍活、托盘可见、可从菜单 Open 回主窗口；② 需要注意时图标与红点变化；③ Pause all 停止新任务但不打断正在写盘的动作（安全点）；④ 托盘不可用时自动降级并说明；⑤ 退出前确认"还有 N 个 run 在跑"。

---

## 9. P1 / P2 提案要点（节选）

| slug | 关键设计点 | 与现有项的关系 |
| --- | --- | --- |
| `permission-scope-picker` | Scope（一次/任务/1h/永久）+ 约束 chips（路径/金额/账户/时段）+ 可读规则句；返回 `PermissionGrant` | 被 `tool-approval-sheet` 内嵌；复用 `radio-cards`/`tag-input`/`segmented-control` |
| `agent-budget-bar` | 已用/预算/预测三段；70/90/100% 阈值动作（暂停/降级/继续）；按 run 归集 | 数据来自运行时；视觉复用 `usage-meter`/`progress`/`gauge` |
| `diff-review` | unified/split、文件树、逐 hunk Accept/Reject、只读/可编辑；事件 `HunkDecision` | 与 `ai-side-panel` 的 word diff、`agent-run` 的 token diff 明确分工（本项数据驱动、可复用） |
| `checkpoint-restore` | 动作前后快照；恢复前列副作用；`Partial` 状态与人工补救 | 复用 `timeline` + `diff-review` + `agent-run` |
| `guardrail-alert` | 规则/证据片段/风险/覆盖请求；不可信来源隔离标记 | 复用 `alert`；与 `security-settings` 联动；写审计 |
| `connector-health` | 每个连接器状态/scope/最后同步/重新授权；依赖它的 routine 列表 | 与 `mcp-servers` 互补：MCP 管工具，本项管账号与同步 |
| `memory-review-queue` | 新记忆/低置信/冲突三组；来源与置信度；合并/遗忘/永不再记 | `agent-memory` 的治理层，不是替代 |
| `autonomy-dial` | 每域四档 + 例外；显示"过去 7 天需要你批准多少次" | 与 `agent-routines`、`permission-scope-picker` 联动 |
| `notification-routing` | 事件 × 通道矩阵 + 静默时段 + 升级 + 能力检测 | 与 `notification-center` 互补（投递策略 vs 收件箱） |
| `agent-audit-log` | append-only 台账；筛选/导出/删除留痕 | 复用 `timeline`/`data-grid`/`filter-toolbar`/`json-viewer` |
| `voice-session-bar` | Push-to-talk/免提、barge-in、实时转写纠错；不采集音频 | 与 `voice-orb`（视觉主体）、`voice-recorder`（录音）分工 |
| `local-model-status` | 下载/校验/内存适配/隐私徽章/离线任务/回退链 | 补 `model-picker` 的运行时状态 |
| `agent-handoff-banner` | AgentWorking/HumanTakeover/Paused/Returning + 计时 + 交还 | 复用 `presence-stack`/`avatar`，明确"非真人" |
| `offline-action-queue` | 排队/重放/冲突/幂等；`NeedsReview` 让用户选版本 | 与 `checkpoint-restore` 配合 |
| `trust-rules-manager` | 规则表（来源/最近使用/命中/过期）+ 撤销/收紧 | `agent-approvals` 生成的规则在此治理 |
| `desktop-agent-settings` | 启动与常驻/通知自检/全局键/资源上限/本地数据/权限总览 | 包裹 `settings-page`，按平台标注可用性 |
| `run-trace-viewer`（P2） | span 瀑布 + 详情 + 与基线对比；大 trace 虚拟化 | 补 `agent-timeline` 的调试视角 |
| `model-routing-policy`（P2） | 条件 → 模型档位；命中率/月成本预估；可测试 | 与 `model-picker`/`model-arena`/`eval-suite` 组合 |
| `global-hotkey-field`（P2） | 全局注册/冲突/平台支持/替代唤起 | 包裹 `shortcut-recorder` |
| `spoken-confirmation`（P2） | 读回 + 复述匹配 + 三次失败改键入 | 与 `hold-to-confirm`/`confirm-morph` 组合 |
| `run-history-list`（P2） | 可搜索历史、结果/成本/时长、重放、加入 eval | 补 `agent-run` 与 `agent-tasks` 之间缺失的历史层 |

---

## 10. 路线图与工作量

| 阶段 | 内容 | 依赖 | 估算 |
| --- | --- | --- | --- |
| Phase 0（基础） | 补齐 24 个图标（当前清单，见 §4.10）；系统通知/托盘/调度运行时接口（非 UI，foundation）；`agent-transcript` 的事件模型与虚拟化契约 | 无 | ~8 人日（约 2 周） |
| Phase 1（P0 核心） | `agent-transcript`、`run-control-bar`、`agent-ask-card`、`agent-attention-inbox` | Phase 0 事件与通知 | ~12 人日 + 集成缓冲 3（约 3 周） |
| Phase 2（高风险审批） | `tool-approval-sheet`、`routine-schedule-editor`、`background-run-dock` | Phase 0 托盘/调度；`permission-scope-picker` 可并行 | ~21 人日（3 项 L，约 4 周） |
| Phase 3（P1 信任与体验） | 16 项 P1（审批范围、diff、检查点、审计、通知路由、语音、本地模型、离线、设置等） | 各 P0 契约稳定后 | ~38 人日（约 7~8 周） |
| Phase 4（P2） | 5 项 P2 | 数据与 trace 格式确定后 | ~19 人日（约 4 周） |

**总量**：P0 完整落地（Phase 0~2）≈ **44 人日**（单人约 9 周；若 foundation 与 UI 双线并行约 5~6 周）；加上 P1/P2 全部约 **101 人日**（单人约 20 周）。以上不含"把现有 AI block 抽成控件"的工程重构成本（那属于运行时接入，另计）。

**关键依赖**：`tool-approval-sheet` 的"改动预览"在 P0 阶段先用只读 `code-block` 片段，`diff-review`（P1）落地后替换为可交互 hunk；`background-run-dock` 依赖平台通知/托盘封装；`agent-attention-inbox` 依赖审批/追问的事件契约。

**验收方式（每项一致）**：控件 + ControlTheme（如适用）+ `ArcItemPage` 展示页（分类/描述/When to use/键盘/动效/实现说明）+ 明暗两张离屏截图 + `--audit` 可达 + 关键交互的冒烟（至少非 reduced-motion 路径一帧）。

---

## 11. 校验结果与风险

### 11.1 JSON 校验（脚本，见运行结果）

- 文件：`docs/agent-life-tools/03-agent-proposals.json`；schema `arc-extension-proposals/v1`；domain `agent`。
- 项数：**28**（要求 20~30）✅。
- slug 唯一性：28 个内部无重复；与 `docs/arc-catalog.json` 294 个 slug **无冲突**（catalog 294 个 slug 也自校验唯一）✅。
- `composes` 引用：全部落在"现有 294 slug ∪ 本 JSON 28 slug"内，无越界、无自引用 ✅。
- 场景引用：15 个场景，所有 `scenarios` 引用均存在，无未使用场景 ✅。
- 分类：全部落在现有 34 个分类名内（23 component 分类 + 5 special 组 + 6 block 组），**未新增分类** ✅。
- 落地项目：全部为现有 24 个项目之一 ✅。
- 与 `02-life-proposals.json` 的 slug 冲突：**最终校验通过**——02 已生成 24 项，与本文件 28 个 slug **零重叠**；两者的 `composes` 均只引用"现有 294 slug ∪ 各自 JSON 内 slug"。
- 优先级/工作量分布：P0 7 / P1 16 / P2 5；S 7 / M 16 / L 5。

### 11.2 风险

| 风险 | 影响 | 缓解 |
| --- | --- | --- |
| 平台通知/托盘 API 不在 Avalonia 内，需要平台特定实现 | P0 的"失焦触达"可能延期 | Phase 0 先做能力探测与降级（应用内队列 + 托盘红点），系统通知后补 |
| 仓库零虚拟化（docs/04 R4）与逐帧 timer 债务 | 长会话/大 trace 性能不达标 | `agent-transcript`/`diff-review`/`run-trace-viewer` 的验收必须含增量与虚拟化；复用 docs/04 建议的共享列表层 |
| 图标缺失 24 个 | 控件实现被卡住或图标临时拼凑 | Phase 0 一次性补全并做 icon 审计 |
| 审批/记忆涉及敏感数据（支付、健康、家庭） | 隐私事故 | 本地加密存储 + 最小化日志 + 删除留痕；审批默认 Deny 焦点；不可逆动作 hold-to-confirm |
| 与生活域提案的 slug/概念重叠 | 合并目录时重复 | 已校验 28 vs 24 零 slug 重叠；合并时保留 §6 的 5 组语义边界说明，共用交互模式但不合并数据模型 |
| 控件事件契约被"演示逻辑"污染（照抄 `tool-call` 的自行改数据） | 无法接真实运行时 | 所有 P0/P1 控件必须"受控优先 + 事件回传"，在实现说明中明确"控件不持有真相" |

### 11.3 与 02 的最终交叉校验（已执行）

- `docs/agent-life-tools/02-life-proposals.json` 在交付前已生成：24 个 items、24 个 scenarios、domain `life`。
- 比对结果：本文件 28 个 slug 与 02 的 24 个 slug **零重叠**；本文件 `composes` 中未引用 02 的任何新 slug（生活域内容不进 Agent 运行时组件），避免跨文件耦合。
- 语义边界见 §6 的对照表（attention-inbox / reminder-center 等 5 组），不存在"同一能力两个提案"的情况。

---

## 12. 附录

### 12.1 现有 AI 相关项与证据索引

| catalog slug | 实现位置 | 证据锚点 |
| --- | --- | --- |
| agent-status | `Special.AIWidgets/Controls/ArcAgentStatus.cs` | 枚举 `:18-38`；按钮 `:454-534` |
| tool-call | `Special.AIWidgets/Controls/ArcToolCall.cs` | 枚举 `:17-24`；审批 `:303-324`；改数据 `:637-678` |
| agent-plan | `Special.AIWidgets/Controls/ArcAgentPlan.cs` | 枚举 `:17-33`；编辑/批准 `:395-412`、`:675-690`；固定时钟说明 `:62-63` |
| agent-timeline | `Special.AIWidgets/Controls/ArcAgentTimeline.cs` | 属性 `:78-95`；回放 `:198-208`、`:235-295` |
| context-window | `Special.AIWidgets/Controls/ArcContextWindow.cs` | 属性 `:64-75`；演示操作 `:508-583` |
| agent-loop | `Special.AIExplainers/Controls/ArcAgentLoop.cs` | remarks `:19-27`（固定 3 次样例） |
| reasoning-tree | `Special.AIExplainers/Controls/ArcReasoningTree.cs` | remarks `:19-27`（固定分数） |
| prompt-cache | `Special.AIExplainers/Controls/ArcPromptCache.cs` | remarks `:18-25`（固定价格/压缩时钟） |
| ai-composer | `Blocks.AI/Pages/AiComposerPage.cs` | 实现说明 `:54-57` |
| ai-chat | `Blocks.AI/Pages/AiChatPage.cs` | 实现说明 `:53-57` |
| ai-side-panel | `Blocks.AI/Pages/AiSidePanelPage.cs` | 实现说明 `:51-55` |
| research-run | `Blocks.AI/Pages/ResearchRunPage.cs` | 实现说明 `:53-55` |
| ai-answer | `Blocks.AI/Pages/AiAnswerPage.cs` | 实现说明（样例/引用点亮） |
| agent-memory | `Blocks.AI/Pages/AgentMemoryPage.cs` | 实现说明 `:53-55` |
| agent-tasks | `Blocks.AI/Pages/AgentTasksPage.cs` | 实现说明 `:53-55` |
| agent-approvals | `Blocks.AI/Pages/AgentApprovalsPage.cs` | 实现说明 `:52-55`；决策 `:182-220` |
| agent-routines | `Blocks.AI/Pages/AgentRoutinesPage.cs` | 实现说明 `:52-54`；排期 `:195-211` |
| agent-orchestrator | `Blocks.AI/Pages/AgentOrchestratorPage.cs` | 实现说明 `:53-56` |
| mcp-servers | `Blocks.AI/Pages/McpServersPage.cs` | 实现说明 `:53-55` |
| eval-suite | `Blocks.AI/Pages/EvalSuitePage.cs` | 实现说明 `:53-55` |
| agent-run | `Blocks.AI/Pages/AgentRunPage.cs` | 实现说明 `:51-54` |
| model-picker | `Blocks.AI/Pages/ModelPickerPage.cs` | 实现说明 `:51-54` |
| model-arena / prompt-studio / document-extract / knowledge-base | `Blocks.AI/Pages/…` | 各自实现说明（样例数据） |
| voice-orb | `Special.Media/Controls/ArcVoiceOrb.cs` | 枚举 `:9-16`；属性 `:50-101`；模拟说明 `:18-23` |
| voice-recorder | `Special.Media/Controls/ArcVoiceRecorder.cs` | 无转写/无事件 |
| toast / toast-stack | `Actions/Controls/ArcToast.cs`、`ArcToastHost.cs` | Host 说明 `:15-19`；Show `:43-92` |
| notification-center | `Blocks.App/Pages/NotificationCenterPage.cs` | 实现说明 `:50-51` |
| security-settings | `Blocks.App/Pages/SecuritySettingsPage.cs` | 实现说明 `:52-54` |
| env-variables | `Blocks.Team/Pages/EnvVariablesPage.cs` | 实现说明 `:64-66` |
| week-calendar | `Blocks.Team/Pages/WeekCalendarPage.cs` | 实现说明 `:74-76`（拖拽未复刻） |
| inbox-triage | `Blocks.App/Pages/InboxTriagePage.cs` | 实现说明 `:50-52`（snooze 不可编辑） |
| chat-thread | `Content/Controls/ArcChatThread.cs` | 模型 `:26-111`；重建 `:467`；docs/04 `:243` |
| timeline | `Feedback/Controls/ArcTimeline.cs` | 事件模型 `:25-45`；属性 `:52-63` |
| usage-meter | `Feedback/Controls/ArcUsageMeter.cs` | 属性 `:31-54` |
| countdown | `Feedback/Controls/ArcCountdown.cs` | 属性 `:34-51`；Completed `:112` |
| dialog / drawer | `Actions/Controls/ArcDialog.cs`、`ArcDrawer.cs` | `ContentControl` + IsOpen（页内） |
| hold-to-confirm / confirm-morph | `Media/Controls/ArcHoldToConfirm.cs`、`Actions/Controls/ArcConfirmMorph.cs` | 确认手势可复用；`ArcConfirmMorph.cs:138-146` 有事件 |
| gallery 页面契约 | `Demo/ArcItemPage.cs`、`ArcPageRegistry.cs` | 新项页面接入方式 |

### 12.2 新增提案与现有项的复用矩阵（摘要）

| 新提案 | 主要复用 | 明确不重复的现有项 |
| --- | --- | --- |
| agent-transcript | ai-chat、chat-thread、tool-call、agent-plan、empty-state | 不是 ai-chat（整页 block）也不是 chat-thread（仅消息） |
| run-control-bar | agent-status、hold-to-confirm、toast | 不是 agent-status（它只显示状态） |
| tool-approval-sheet | agent-approvals、tool-call、drawer、hold-to-confirm | 不是 agent-approvals（无预览/改参/范围/过期） |
| agent-ask-card | dialog、calendar、file-dropzone、radio-cards | 不是 ai-chat 的文本追问 |
| agent-attention-inbox | agent-approvals、notification-center、inbox-triage、filter-toolbar | 不是 notification-center（知会 ≠ 必须处理） |
| routine-schedule-editor | agent-routines、calendar、time-picker、select、countdown | 不是 agent-routines 的句子替换 |
| background-run-dock | agent-status、control-center、notification-center | 仓库无托盘/常驻，非重复 |
| permission-scope-picker | radio-cards、tag-input、segmented-control | 不是 Always allow chip |
| agent-budget-bar | usage-meter、progress、gauge、agent-timeline | 不是账单视角的 usage-billing |
| diff-review | code-block、tree-view、json-viewer | 不是 ai-side-panel 的预计算 word diff |
| checkpoint-restore | timeline、diff-review、agent-run | 不是 ai-side-panel 的局部 Undo |
| memory-review-queue | agent-memory、tree-view、tag-input | 不是 agent-memory 的浏览清单 |
| notification-routing | notification-center、settings-page、select、time-picker | 不是收件箱本身 |
| agent-audit-log | timeline、data-grid、filter-toolbar、json-viewer | 无现有审计组件 |
| voice-session-bar | voice-orb、voice-recorder、hold-to-confirm | 不是全屏 orb |
| local-model-status | model-picker、progress、usage-meter | 不是模型选择菜单 |
| agent-handoff-banner | presence-stack、avatar、dialog | 无现有人工接管表达 |
| offline-action-queue | alert、timeline、toast | 无现有离线队列 |
| trust-rules-manager | agent-approvals、roles-permissions、data-grid | 不是账号安全 settings |
| desktop-agent-settings | settings-page、switch、shortcut-recorder | 无现有桌面运行时设置 |
| run-trace-viewer | agent-timeline、code-block、json-viewer、brush-chart | 不是回放动画 |
| model-routing-policy | model-picker、model-arena、eval-suite | 不是单次模型选择 |
| global-hotkey-field | shortcut-recorder、settings-command | 不是组合键录制 |
| spoken-confirmation | hold-to-confirm、confirm-morph、voice-recorder | 不是按住确认 |
| run-history-list | agent-run、agent-tasks、filter-toolbar、data-grid | 不是单次 run 卡 |

### 12.3 本文件没有做的事

- 没有提出任何生活内容域组件（健康/财务/出行/饮食/家庭等），避免与 02 重复。
- 没有改动 `src/`、`docs/01~05`、`README.md`；没有构建或运行截图。
- 没有把"把现有 block 抽成控件"当成新提案——那是工程重构（见 §2 的评估），本文件只提出**新增交互能力**。
- 没有给出具体的调度器/通知/加密实现代码；这些属于运行时工程，提案中只定义 UI 契约与验收。
