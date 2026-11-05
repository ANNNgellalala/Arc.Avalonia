# 13-C · Agent 顶层组合层现状与缺口审计

> **审计对象**：Arc.Avalonia（Avalonia 12 / .NET 10 移植，`D:\Code\VibeCoding\Arc.Avalonia`）。
> **审计问题**：把现有 AI Agent 相关资产提升为「顶层组合 UI 组件层」需要什么、差多少工作量。
> **审计方式**：只读 `read/grep/glob` + 只读 PowerShell 静态统计；**未执行 `dotnet build` / `dotnet run` / `tools/*.ps1`**（由 Lead 统一验证）。本文件是本次审计唯一写入的文件。
> **基线**：工作树当前状态（2026-10-31）。工作树存在未提交改动（文档平台迁移等，`git status` 可见 248 个文件），本报告的事实、行号与文件位置均以**工作树当前内容**为准；与 `docs/06`、`docs/07` 的旧数字/旧路径冲突处，以源码为准并在 §6.5 列出漂移清单。
> **证据标注**：正文 `file:line` 均为源码/文件系统可直接复现的事实；引用既有设计文档处标 `docs/...`（可能过期）；推断处注明「推断」。

---

## 0. 一页结论

### 0.1 一句话结论

**这套库已经有「Agent 界面的叶子」（14 个 P0 契约控件 + 40 个 AI 命名 catalog 资产），但没有「Agent 界面的主干」：没有会话/事件投影模型、没有增量流式契约、没有审批队列与并行子 agent 的组合根，也没有可插拔渲染器。顶层的正确形态是「表现层运行时（presentation runtime）+ 组合控件 + 宿主注入点」：内生事件→视图的状态机，但不内生 Agent 执行运行时。**

### 0.2 关键数字（全部为本审计静态复算）

| 指标 | 数值 | 口径 / 证据 |
| --- | --- | --- |
| 仓库工程数 | **29** | `ArcUI.slnx`（Foundation 2 + Components 11 + Agent 2 + Special 5 + Blocks 6 + Apps 2 + Tests 1）；文档 06/07 的「24 个项目」已过期 |
| catalog 总量 / 文档 / 条目页 | **294 / 294 / 294** | `docs/arc-catalog.json`（128 components + 55 special + 111 blocks）；`src/**/Docs/*.json` 实测 294 个；`ArcItemPage` 子类实测 294 个 |
| Gallery 页面总数 | **315** | `README.md:232-233`（294 文档页 + 5 guides + home/status + 14 个 agent/life 场景页）；静态复算：294 item page + 21 个具体 `ArcPage` |
| AI 命名 catalog 资产 | **40** | 11 AI widgets + 11 AI explainers + 18 AI blocks（`docs/arc-catalog.json` 的 `specialGroups`/`blockGroups`） |
| Agent/生活域 P0 场景页 | **14** | AgentUI 6 + LifeToday 7 + Gallery 的 Virtualized list 1（均 `: ArcPage` 非 catalog 条目） |
| 本审计范围内控件（catalog-facing） | **36** | AgentUI 6 + LifeToday 7 + AIWidgets 11 + AIExplainers 11 + Content `ArcChatThread` 1；另有 AIWidgets 7 个 public 辅助子控件、1 个抽象基类 `ArcExplainer` |
| 本审计范围内页面 | **54** | 6 + 7 + 11 + 11 + 18（Blocks.AI）+ 1（chat-thread） |
| 源码行数（控件 + 页面 + AiKit） | **30,353 行** | AgentUI 4,236 / LifeToday 4,546 / AIWidgets 8,520 / AIExplainers 5,390 / Blocks.AI 6,494 / Content 1,167 |
| 现有 Agent UI 公开意图事件 | **AgentUI 17 个**（6 控件）；LifeToday 38 个（含内部辅助类）；AIWidgets **2 个**（且与 Agent 无关）；AIExplainers **0**；Blocks.AI **0** | `public event` 逐文件计数；AIWidgets 的 2 个事件来自 `ArcBlackHole`（`ArcBlackHole.cs:219,221`） |
| 现有「会话/流式/审批状态机」类型 | **0** | 全仓 grep 无 `Session`/`Conversation`/`ApprovalQueue`/`INotifyCollectionChanged` 于 Agent 域（唯一的集合订阅在 `ArcVirtualList.cs:53,158-175`） |
| 现有「增量更新」控件 | **0** | 6 个 AgentUI 控件与 4 个 LifeToday 复合控件全部 `Children.Clear()` 全量重建（见 §2.2/§3.2） |
| 06 扩展目录中 Agent 项 | **28 项 / 95 人日**（P0 7 项 36 人日；P1 16 项 38；P2 5 项 21） | `docs/arc-extension-catalog.json` 逐项 S=1/M=3/L=8 复算；但 7 个 P0 已实现却未回写状态（catalog 无 `status` 字段） |

### 0.3 三个判断（详见 §3.3、§9）

| # | 问题 | 判断 | 一句话理由 |
| --- | --- | --- | --- |
| 1 | 顶层只做「UI 契约 + 宿主注入」还是「内生会话/流式/审批状态机」？ | **内生「表现层状态机」，不内生「Agent 运行时」** | 纯契约已证明不够：每个宿主都要自己把事件流映射成 6 套控件状态；但执行/传输/持久化仍必须留宿主（06 §7.4 的三条理由仍然成立）。业界（AG-UI / Vercel AI SDK UI / assistant-ui / CopilotKit）都把「事件解析 + 消息重建 + 状态投影」放在前端 runtime 层，把执行留在后端——Arc 应对齐这一刀口。 |
| 2 | Blocks.AI 应被顶层吸收，还是保留为「示例块」？ | **保留为示例块（catalog 条目页），顶层只吸收它的「组合配方」与共享 kit；不整包吸收代码** | 18 个页面是 `ArcItemPage` + `BuildDemo()` 的页面级样例（`AiChatPage.cs:22`），是 294 项目录、文档与截图的验收基线；把它改成控件会破坏目录与演示价值，而复制其视觉又会造成双份维护。正确做法是「页面改写为组合顶层组件」（§5.5、§6.4）。 |
| 3 | LifeToday 属于 Agent 顶层还是横向场景层？ | **横向场景层（生活域），不属于 Agent 顶层；但可以依赖 Agent 顶层** | 7 个控件的数据模型是提醒/用药/议程/捕获（`LifeTodayKit.cs:140-213`），没有任何 run/approval/streaming 语义；且 06 §4.6 已确立「生活域复用 Agent 域 UI」的依赖方向（life → agent）。建议从 `src/Agent/` 移出到 `src/Scenarios/`（或保留目录但按「横向场景层」发布）。 |

### 0.4 人日分档（详见 §5.4）

| 档 | 范围 | 人日 | 2–3 人周期 |
| --- | --- | --- | --- |
| **S 最小可交付** | 会话/事件投影模型 + 增量流式 transcript + 审批队列 + 组合根 `ArcAgentSurface` + 渲染器注册 + 3 个宿主无关 Gallery 页 + a11y/性能护栏 | **≈ 46** | 2 人 ≈ 5 周 / 3 人 ≈ 3 周 |
| **M 完整顶层 P0/P1** | S + 并行子 agent 面板 + artifact 预览 + diff/权限范围/接管/预算/检查点等治理 UI（含 06 的 14 个 Agent P1 项）+ Blocks.AI 关键页重写 | **≈ 104** | 3 人 ≈ 7 周 / 4 人 ≈ 5 周 |
| **L 全量** | M + 06 剩余 7 个非重叠 Agent 项（审计/连接器/离线/本地模型/路由/历史/trace）+ Blocks.AI 全量重写 + 迁移收尾 | **≈ 135** | 3 人 ≈ 9–10 周 / 4 人 ≈ 7 周 |

> 06 已估的 P0 36 人日（7 个 Agent 项）视为**已投入**；上述分档是「在已有 P0 之上把能力提升为顶层」的**净新增**，其中 M/L 已扣除与 06 目录的重复计价（§5.5）。

### 0.5 三大风险（详见 §8）

1. **UI 库内生运行时导致宿主锁定 / 版本漂移**：状态机一旦越界（发起模型调用、持有持久化、自动重试），就与 06「无副作用、只发意图事件」的边界冲突（`docs/06:752-765`）。缓解：把状态机定义为**纯函数 reducer + 只读投影**，无 I/O、无定时器（显示时钟除外）、可重放；执行全部经 `IArcAgentCommandSink` 交宿主。
2. **流式高频更新性能**：现有 transcript 每次属性变化 `Children.Clear()` 全量重建（`ArcAgentTranscript.cs:150-160,199-290`），且未接 `ArcVirtualList`（`docs/07:120`）；60 chunks/s 的 token 流会变成 O(n²)。缓解：集合增量（Insert/Update/Remove）+ 帧级合并器（16 ms）+ 虚拟化 + O(1) 尾部追加；设 10k 事件 / 60 fps 压力护栏。
3. **审批/中断语义的并发正确性**：approve/deny/expire/stop/takeover/steer 在同一 run 上交错；现有审批单只负责展示与倒计时（`ArcToolApprovalSheet.cs:95-99,141,676`），没有队列、去重、顺序与幂等键。缓解：显式串行审批队列 + 请求 id 幂等 + 宿主确认前「Submitting」态 + 交错场景状态测试。

---

## 1. 审计范围、口径与方法

### 1.1 范围

| 范围块 | 路径 | 说明 |
| --- | --- | --- |
| Agent UI（P0） | `src/Agent/Arc.Avalonia.AgentUI` | 6 控件 / 6 页 / 0 catalog 条目 |
| 生活域（P0） | `src/Agent/Arc.Avalonia.LifeToday` | 7 控件 / 7 页 / 0 catalog 条目 |
| AI Special | `src/Special/Arc.Avalonia.Special.AIWidgets`、`.AIExplainers` | 22 个 catalog 条目（11+11） |
| AI Blocks | `src/Blocks/Arc.Avalonia.Blocks.AI` | 18 个 catalog 条目页 + 1 个 internal kit |
| 通用对话表面 | `src/Components/Arc.Avalonia.Content`（`ArcChatThread`） | 1 个 catalog 条目，AI 语义最接近的中间层控件 |
| 横向检索 | `src/Components/**`、`src/Special/**` 的 agent/timeline/context-window/reasoning/prompt-cache/vector-search 等关键字 | 见 §2.7 |

### 1.2 统计口径

| 口径 | 规则 |
| --- | --- |
| 控件 | `public class X : Panel/Control/TemplatedControl/Decorator/ListBox/ContentControl`，且文件在对应 `Controls/` 目录；抽象基类与 public 辅助子控件单独标注 |
| 页面 | `class XPage : ArcPage`（场景页）或 `: ArcItemPage`（catalog 条目页） |
| LOC | `Get-Content <file>` + `Measure-Object -Line`；含注释/空行/XML 文档 |
| 事件 | 各控件源码中 `public event` 声明数；`EventHandler`（AgentUI）与 `Action`（LifeToday）混用已在 §3.2 标注 |
| 工作量 | 沿用 06 报告的分档：S=1 / M=3 / L=8 人日（`docs/06:373`）；顶层新增项按分解到「模型 / 组合 / 控件 / 页面 / 验收」的工作包估 |

### 1.3 与既有文档的关系

- `docs/06-agent-life-tools-extension.zh.md`：四层 UI 能力模型（§2.2）、扩展方案（§4，57 项 / 235 人日）、宿主边界（§7）——本审计的**原则来源**。
- `docs/07-p0-ui-implementation.zh.md`：P0 14 项落地报告——本审计的**资产基线**，但其文件索引与部分数字已过期（§6.5）。
- `docs/agent-life-tools/05-ui-scope-review.zh.md`：v2 纯 UI 范围复核，结论「可交付、P0 0 项」——本审计沿用「UI-only」口径，但指出「纯 UI 契约」需要升级为「表现层状态机」才够用。
- `docs/agent-life-tools/03-agent-gaps.zh.md`（2026-10-28）：其结论「40 个 AI 资产 / 0 个运行时事件契约」在 P0 落地后**对 6 个 AgentUI 控件已不再成立**（这 6 个现在有 17 个意图事件），但对 AIWidgets/Explainers/Blocks.AI 仍然成立。本审计以源码为准更新该结论。

---

## 2. 现有资产清点

### 2.1 总表

| 项目 | catalog 条目 | 控件（catalog-facing / 含辅助） | 页面 | Docs JSON | 控件 LOC | 页面 LOC | 关键公共事件数 | 直接依赖（csproj） |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `AgentUI` | 0 | **6 / 6** | 6 | 0 | 3,192 | 1,044 | 17 | Foundation + Demo |
| `LifeToday` | 0 | **7 / 7** | 7 | 0 | 3,832 | 714 | 38 | Foundation + Demo + Feedback + Pickers |
| `Special.AIWidgets` | 11 | **11 / 18** | 11 | 11 | 7,164 | 1,356 | 2（均无关 Agent） | Foundation + Demo |
| `Special.AIExplainers` | 11 | **11 / 12**（含抽象基类） | 11 | 11 | 4,727 | 663 | 0 | Foundation + Demo |
| `Blocks.AI` | 18 | **0 / 0**（`AiKit` 为 internal） | 18 | 18 | 1,014（AiKit） | 5,480 | 0 | Foundation + Internal + DataDisplay + Navigation + Overlays + Inputs + Feedback + Charts + Demo |
| `Content`（`ArcChatThread`） | 1 | **1 / 1** | 1 | 1 | 1,080 | 87 | 1（`Send`） | Foundation + Demo |
| **合计** | **41**（40 + chat-thread） | **36 / 44**（含 7 辅助 + 1 基类） | **54** | **41** | **21,009** | **9,344** | **58** | — |

> 口径：`catalog 条目` 指 294 项目录中的 AI 命名项；AgentUI/LifeToday 是 P0 场景页，不在 294 目录内（`docs/07:25` 明确 `ArcPage` 非 `ArcItemPage`，不占目录）。LOC 合计 30,353 行 = 21,009（控件/AiKit）+ 9,344（页面脚手架）。

### 2.2 AgentUI（6 控件 / 6 页）——契约清洁度最高，但仍是「叶子视图」

| 控件 | 文件 | 页 | 状态枚举 | 公开事件 | 模型/契约要点 | 展示 or 交互 |
| --- | --- | --- | --- | --- | --- | --- |
| `ArcAgentTranscript` | `Controls/ArcAgentTranscript.cs`（601 行） | Agent transcript（Order 20001） | Run: Idle/Running/AwaitingInput/Done/Failed/Stopped；Event: Message/Thinking/Tool/Plan/Approval/Question/Result/Error；Row: Pending/Running/Done/Failed/Skipped | `RetryRequested(ArcAgentEvent)`（`:197`） | `Events: IEnumerable<ArcAgentEvent>`（`:126`）、`Status`、`IsStreaming`、`EmptyText`；扁平事件模型（`:91-110`） | 只读；默认 `SampleEvents()`（`:127,551`），可独立渲染 |
| `ArcRunControlBar` | `Controls/ArcRunControlBar.cs`（486） | Run control bar（20002） | Running/Paused/Stopped/TakenOver | `StopRequested`、`StopAfterStepRequested`、`PauseRequested`、`ResumeRequested`、`SteerRequested(string)`、`TakeoverRequested`（`:112-128`） | `Status`、`CurrentStep`、`StepLabel`、`ElapsedSeconds`（宿主供数，`:45-58`） | 只发意图，不改状态 |
| `ArcToolApprovalSheet` | `Controls/ArcToolApprovalSheet.cs`（778） | Tool approval sheet（20003） | Checkout/Editing/Submitting/Decided/Expired/Cancelled；Risk Low/Medium/High；Scope Once/Task/Hour/Always | `ApproveRequested(ArcApprovalDecision)`、`DenyRequested(string)`、`DelegateRequested`（`:237-244`） | `ToolName`、`Intent`、`Risk`、`Arguments`（`ArcApprovalArgument` 可编辑）、`ExpiresInSeconds`、`Scope`、`State` | 展示 + 编辑参数；**唯一展示用 `DispatcherTimer`**（`:141,676`），到期只禁用按钮，不强制 |
| `ArcAgentAskCard` | `Controls/ArcAgentAskCard.cs`（537） | Agent question card（20004） | Question/Answered/Skipped/Deferred/Expired | `Submitted(ArcAgentAskAnswer)`、`DeferredRequested`（`:160-163`） | `Question`、`Options`、`AllowMultiple`、`AllowFreeText`、`State`；`ArcAgentAskAnswer` 回传选项 id + 自由文本 | 只发意图；默认 `SampleOptions()`（`:78`） |
| `ArcAgentAttentionInbox` | `Controls/ArcAgentAttentionInbox.cs`（510） | Agent attention inbox（20005） | Kind: Approval/Ask/Failure/Routine/Suggestion；Attention: Info/Important/Blocking；State: New/Seen/InProgress/Resolved/Expired | `ItemActionRequested(id)`、`Dismissed(id)`（`:152-155`） | `Items: IEnumerable<ArcAttentionItem>`（`Id/Kind/Title/Detail/Attention/Age/ActionLabel/State`，`:66-92`）；按 Kind 分组 | 只发意图；默认 `SampleItems()`（`:105`） |
| `ArcBackgroundRunDock` | `Controls/ArcBackgroundRunDock.cs`（280） | Background run dock（20006） | Idle/Running/NeedsAttention/Paused | `PauseRequested`、`StopRequested`、`ExpandRequested`（`:86-92`） | `Status`、`TaskLabel`、`Progress`（宿主供，`:40-48`） | 只发意图 |

**AgentUI 的三个结构性事实：**

1. **契约清洁**：6 个控件都不写回业务状态；唯一 `SetCurrentValue` 是审批单里切换 Scope 的 UI 选择（`ArcToolApprovalSheet.cs:531`）。`03-agent-gaps` 对旧 AIWidgets 的「自导自演」批评在 AgentUI 上不成立——这是 P0 的最大成果。
2. **没有增量**：`EventsProperty.Changed` → `Rebuild()` → `Children.Clear()`（`:150-160,199-290`）；`Events` 是 `IEnumerable` 快照，没有 `Append/Update` 方法，也不订阅集合变化。docstring 写「host can append or update events without the control knowing where they come from」（`:88`），但实现层面只能整表替换。流式必须每 chunk 重设整个列表 → 全量重建。
3. **没有虚拟化**：内部是 `ScrollViewer + StackPanel`（`:239-251`），未接 `ArcVirtualList`（`docs/07:120` 已自认「未接，长会话仍是全量重建」）。

### 2.3 LifeToday（7 控件 / 7 页）——生活域，不是 Agent 语义

| 控件 | 文件 | 页 | 数据模型 | 事件风格 | 依赖 |
| --- | --- | --- | --- | --- | --- |
| `ArcReminderRow` | `Controls/ArcReminderRow.cs`（384） | Reminder row | `ArcReminderDueState` + 标题/到期文案/重复/来源/备注 | `Action<bool>`、`Action<TimeSpan>`、`Action` | — |
| `ArcQuickCaptureBar` | `Controls/ArcQuickCaptureBar.cs`（456） | Quick capture bar | `ArcQuickCaptureChip`（Time/Person/Amount/List/Note）+ `ArcQuickCaptureState` | `Action<string>`、`Action<string>`、`EventHandler` | — |
| `ArcRoutineScheduleEditor` | `Controls/ArcRoutineScheduleEditor.cs`（765） | Routine schedule editor | `ArcScheduleSpec`（时间/时区/周几/月度日/RRULE 子集）+ Misfire/Overlap 策略 | `Action<ArcScheduleSpec>`、`EventHandler` | Pickers |
| `ArcDailyBrief` | `Controls/LifeTodayKit.cs:229`（2,227 行文件含 4 控件） | Daily brief | 问候/天气/日程/健康/预算/单条建议 + 聚合状态 | 6 × `Action` | Feedback |
| `ArcDayAgenda` | `LifeTodayKit.cs:637` | Day agenda | `ArcAgendaItem`（显示字符串时间、冲突标记、全天条） | 6 × `Action` | — |
| `ArcReminderCenter` | `LifeTodayKit.cs:1326` | Reminder center | `ArcReminderItem` 分组 + 批量延后 | 7 × `Action` | Feedback |
| `ArcMedicationSchedule` | `LifeTodayKit.cs:1681` | Medication schedule | `ArcMedicationItem` + 疗程/库存/依从性 | 9 × `Action` | — |

**判断**：7 个控件全部是「生活域展示 + 意图事件」，没有 run/approval/streaming/tool 语义；事件委托混用 `Action` 与 `EventHandler`（`ArcQuickCaptureBar.cs:176-183` vs `:183`；LifeTodayKit 全为 `Action`）。它与 Agent 顶层的正确关系是 **life 场景层 → 依赖 Agent 顶层**（06 §4.6：`reminder-center` composes `background-run-dock`、`daily-brief` composes `agent-attention-inbox`，`docs/06:511-529`）。

### 2.4 Special.AIWidgets（11 catalog 控件 + 7 public 辅助子控件）

| 控件 | 文件 | 公开数据 API | 公开事件 | 自驱行为（关键证据） | 复用判定 |
| --- | --- | --- | --- | --- | --- |
| `ArcAgentStatus` | `ArcAgentStatus.cs`（757） | `Status`、`AgentName`、`ActivityLabel/Icon/Count`、`ElapsedSeconds`、`Steps`、`IsExpanded` | **无** | Approve/Deny/Retry/Pause/Stop 直接 `SetCurrentValue(StatusProperty, …)`（`:457-534`）；`Open run` 无处理器（`:486`）；内部时钟 | 改签名 |
| `ArcToolCall` | `ArcToolCall.cs`（678） | `Calls: IEnumerable<ArcToolCallData>`（含 `ArcToolArgument`） | **无** | Allow/Deny/Retry 直接改 `call.Status` 并启动 1.2–1.4 s 演示定时器（`:616,637-678`） | 改签名（或由 `ArcToolApprovalSheet` 取代） |
| `ArcAgentPlan` | `ArcAgentPlan.cs`（870） | `Steps: IEnumerable<ArcAgentPlanStep>`、`Status`、`Goal`、`ModelName`、`Summary` | **无** | 草稿可改名/排序/增删；运行时按内部时钟推进（`:103-127` + 内部 tick） | 改签名 |
| `ArcAgentTimeline` | `ArcAgentTimeline.cs`（846） | `Title`、`Agents`、`Spans`、`Handoffs`、`Duration`、`IsPlaying` | **无** | 内部时钟驱动回放播放头（`IsPlaying` 自驱） | 改签名（并行子 agent 的底图可复用） |
| `ArcContextWindow` | `ArcContextWindow.cs`（718） | `Items`、`Models`、`ModelId`、`Threshold`（`Cells` 在辅助子控件 `ArcContextCells.cs:617`） | **无** | 演示型 Send/Attach/Compact 操作；无 tokenizer | 可保留为展示件，接只读数据 |
| `ArcActivityRings` | `ArcActivityRings.cs`（564） | `Metrics`、`Days`、`Day`、`Label` | 无 | 绘制动画 | 保留（非 Agent 语义） |
| `ArcBlackHole` | `ArcBlackHole.cs`（666） | `Items`、`Title` | `SelectionChanged`、`Deleted`（`:219-221`） | 删除动画 | 保留（氛围件） |
| `ArcInkFluid` / `ArcMorphLoader` / `ArcSkeletonMorph` / `ArcStretchRefresh` | 434 / 342 / 381 / 449 | 视觉参数 | 无 | 动画/计时 | 保留（氛围件） |
| 7 个 public 辅助子控件 | `ArcRingsView`、`ArcAgentMark`、`ArcStepMark`、`ArcMiniBar`、`ArcAgentLiveRing`、`ArcContextCells`、`ArcMorphBlock`（合计约 459 行，散在上列文件中） | — | — | — | 顶层化时应下沉为 internal |

**结论**：11 项中只有 5 项（status/tool-call/plan/timeline/context-window）与 Agent 运行时语义相关，且全部是「自驱演示件」；另外 6 项是通用氛围/可视化件，不应进入 Agent 顶层。

### 2.5 Special.AIExplainers（11 控件 + 抽象基类）

| 项 | 文件 | 类型 | 公共 API | 事件 |
| --- | --- | --- | --- | --- |
| `ArcExplainer`（抽象基类） | `ArcExplainer.cs:25` | `Control` | 相位/动画基类 | 无 |
| `ArcAgentLoop` | `ArcAgentLoop.cs:20` | `ArcExplainer` | 固定样例（3 次循环） | 无 |
| `ArcDataFlow` / `ArcPaymentFlow` / `ArcMultiRegionFailover` / `ArcVectorSearch` / `ArcMixtureOfExperts` / `ArcSpeculativeDecoding` / `ArcPromptCache` / `ArcReasoningTree` / `ArcTimeDial` / `ArcDateReel` | 各 `Controls/*.cs`（合计 4,727 行） | `ArcExplainer` | 固定样例/演示相位 | **0** |

**结论**：11 项全部是「原理插图」，不是 Agent 界面控件；`vector-search` / `prompt-cache` / `reasoning-tree` 不应被误读为「AI 语义控件」。它们保留在 Special 层，顶层不引用。

### 2.6 Blocks.AI（0 控件 / 18 页 / 1 internal kit）

| 事实 | 证据 |
| --- | --- |
| 18 个页面全部 `: ArcItemPage`，走 `BuildDemo()`，是页面级「产品界面样例」 | `AiChatPage.cs:22`；`AgentRunPage.cs:18,62`（「run 与 PR 是模拟的、diff 用 token 不是语法高亮」）；18 个 `Pages/*.cs` 对应 18 个 Docs JSON |
| 唯一共享代码是 `internal static class AiKit`（1,014 行布局/文本/Markdown/diff 助手） | `Controls/AiKit.cs:26` |
| 页面确实组合了中间层组件（这是全仓 AI 区唯一真正「组合 L2」的项目） | `Arc.Avalonia.Blocks.AI.csproj` 引用 DataDisplay / Navigation / Overlays / Inputs / Feedback / Charts / Internal |
| 页面内部状态是私有方法 + 局部变量，不能当控件嵌入 | 例如 `AiChatPage.cs:67`（`private sealed record Turn`）、`AiChatPage.cs:69`（`BuildDemo`） |
| 18 项覆盖了 Agent 产品的「界面表情」：composer/chat/side-panel/research-run/answer/model-picker/arena/prompt-studio/document-extract/memory/tasks/approvals/routines/orchestrator/MCP/knowledge/eval/run | `docs/arc-catalog.json` blockGroups.AI |

**结论**：Blocks.AI 是「示例块 + 验收基线」，不是可复用组件层；但它是顶层组件最重要的**组合配方来源**（§5.5）。

### 2.7 散落的 AI 语义资产（横向检索结果）

| 资产 | 位置 | 与 Agent 的关系 | 顶层处理 |
| --- | --- | --- | --- |
| `ArcChatThread` | `src/Components/Arc.Avalonia.Content/Controls/ArcChatThread.cs`（1,080 行；`: ArcChatThread.cs` = `TemplatedControl :82`） | 通用对话表面：气泡/分组/输入框/附件/typing 指示（`:96,235`）；**无流式**、**无增量**（属性变化 → `RenderMessages` 整体重建 `:139,259,466-467`）、无虚拟化 | L2 保留；顶层组合它，并补 `IArcAgentMessageSink` 增量适配 |
| `ArcMarkdown` | `Content/Controls/ArcMarkdown.cs:37`（internal static） | `AiKit.Markdown` 与 ChatThread 的 Markdown 子集实现分散在 Blocks.AI 与 Content | 顶层需要统一 Markdown 渲染器（候选：提升为 L1/L2 公共件） |
| `ArcVirtualList` | `Foundation/Arc.Avalonia/Controls/ArcVirtualList.cs:40`（`: ListBox`） | 唯一支持 `INotifyCollectionChanged` + 粘底的列表原语（`:53,158-175`） | L1 保留；Agent transcript 必须接它 |
| 语音件 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcVoiceOrb.cs:24`、`ArcVoiceRecorder.cs:15`（`ArcSpecialCanvas`） | 视觉件，不是语音会话控件；`voice-session-bar` 仍是 06 P1 缺项 | 暂不进入顶层 |
| 通用反馈/浮层件 | Actions（toast/dialog/drawer）、Feedback（timeline/usage-meter）、Overlays（popover/tooltip） | 顶层审批/通知/预算的底层积木 | 顶层声明依赖 L2，不自造 |

### 2.8 汇总

- **控件总数**：36 个 catalog-facing（AgentUI 6 + LifeToday 7 + AIWidgets 11 + AIExplainers 11 + chat-thread 1）；含 7 个 public 辅助子控件与 `ArcExplainer` 基类则 44 个「公开类型」。
- **页面数**：54（14 个 P0 场景页 + 40 个 AI catalog 条目页；其中 Blocks.AI 18）。
- **可复用率**见 §5.1（按源码行数 30,353 行：直接可用 10.5%、改签名 16.3%、保留/下沉 26.4%、示例拆解 21.4%、页面脚手架 12.7%、LifeToday 12.6% 单列为横向场景层）。
- **能力缺口**：会话模型 0、增量流式 0、审批队列 0、并行子 agent 组合 0、artifact 预览 0、渲染器注册 0、事件总线/宿主注入 0。

---

## 3. 已确立的原则与顶层化升级

### 3.1 已确立的原则（06/07/05 提炼）

| # | 原则 | 出处 | 对顶层的含义 |
| --- | --- | --- | --- |
| P1 | 组件只「展示状态 + 发意图事件」，不替宿主做决定 | `docs/06:200`、`docs/06:737-742` | 顶层控件仍只发意图；状态投影可以内置，执行不可以 |
| P2 | 四层 UI 能力模型：L1 基础 / L2 领域组件 / L3 页面组合 / L4 Agent 状态与交互；L5 运行时不在库内 | `docs/06:202-211` | 顶层 = 把 L4 提升为「组合层」，并吸收 L3 的 Agent 页面组合 |
| P3 | 统一状态机：Idle → Running → Paused → NeedsInput → Done/Failed/Stopped；写操作 Preview → AwaitingApproval → Executing → Done/Undone/Failed | `docs/06:548` | 顶层 reducer 直接把这条状态机作为契约实现 |
| P4 | 无副作用：不写文件/库、不发网络/模型、不注册系统能力、不持全局单例 | `docs/06:740`、`docs/07:24,113-114` | 顶层「表现层运行时」必须证明自己无 I/O（可静态审查） |
| P5 | 可嵌入：不假设路由/全局快捷键/固定窗口尺寸 | `docs/06:741` | `ArcAgentSurface` 必须能进侧栏/浮层/整页/移动壳层 |
| P6 | 组件内部状态可完整展示（Loading/Empty/Error/Partial/Running/…），宿主只切换数据 | `docs/06:739` | 顶层状态投影要覆盖全部枚举，不能用「转圈」兜底 |
| P7 | 示例数据确定性；Gallery 页展示空/加载/错误/内容四态；双主题、8 accent、reduced-motion | `docs/06:742,776,806`、`docs/07:25-26` | 顶层交付必须带宿主无关 SampleSession 与 4 态页面 |
| P8 | 长列表必须虚拟化或声明数据规模上限 | `docs/06:777` | transcript 接 `ArcVirtualList`；并行 lane 面板声明上限 |
| P9 | 验收：`--audit` missing=0/pageFailures=0；`--smoke` failures=0；每页明暗截图无裁切 | `docs/06:803-805`、`docs/07:71-109` | 顶层新增页面进入同一门槛 |
| P10 | 范围收敛：不把 Agent 逻辑写进控件（范围外溢是最大风险） | `docs/06:752-765` | 顶层的「运行时」必须限定为 presentation runtime，并在文档中显式定义边界 |

### 3.2 现有实现的符合度矩阵（升级点）

| 能力 | 现状 | 证据 | 顶层升级点 |
| --- | --- | --- | --- |
| 状态枚举完整性 | AgentUI 6 控件均完整 | §2.2 | 保留枚举，补 `Suspended/Resuming/Reconnecting` 等宿主态 |
| 意图事件 | AgentUI 17 个、LifeToday 38 个 | §2.2/2.3 | 统一为 `IArcAgentCommandSink`/命令对象；事件委托统一（`EventHandler<T>`） |
| 增量更新 | **0**：全部 `Children.Clear()` 重建 | `ArcAgentTranscript.cs:150-160,199-290`、`ArcAgentAttentionInbox.cs:157-160`、`ArcAgentAskCard.cs:165-167`、`LifeTodayKit.cs:392,726,1430,1858` | 集合增量契约 + 模板选择器 + 虚拟化 |
| 集合通知 | **0**：`IEnumerable` 快照 | AgentUI 全部属性；唯一 `INotifyCollectionChanged` 在 `ArcVirtualList.cs:53` | 顶层模型用 `ObservableCollection` 或自定义增量 sink |
| 流式 | 只有 `IsStreaming` 徽标 + `Status`，无 delta API | `ArcAgentTranscript.cs:135,303-306` | 事件模型 `MessageDelta/ReasoningDelta/ArgsDelta` + 合并器 |
| 审批并发 | 单张表；无队列/去重/顺序 | `ArcToolApprovalSheet.cs:95-99` | `ArcApprovalQueue` + 幂等键 + 宿主确认态 |
| 生命周期归属 | P0 已做到「宿主拥有状态」 | `docs/06:200`、`docs/07:24` | reducer 内生态，但只到「视图状态」；执行仍宿主 |
| 主题切换 | 组合控件构建时快照 token，运行时切换需重建页面 | `docs/07:124`；`ArcUi.cs:15-18`（docstring 与实现张力） | 顶层页面级双主题验收，不承诺同一控件实例热切换 |
| 分层依赖 | AgentUI/AIWidgets 只依赖 Foundation，**绕过 L2** | csproj + usings（`Arc.Avalonia.Controls`、`Arc.Avalonia.Theme`） | 顶层应显式依赖 L2（Content/Inputs/Overlays/Feedback），复用 ChatThread/Markdown/Toast |
| 与 Demo 解耦 | 所有组件项目 `ProjectReference Demo`（为 `ArcPage`/`ArcDoc`） | 6 个 csproj | 顶层发布前需把页面/文档契约从组件库剥离（B 任务同源问题） |

### 3.3 关键判断：顶层的「运行时」边界

**结论：顶层内生「表现层状态机（presentation state machine）」，不内生「Agent 运行时（agent runtime）」。**

支持「内生状态机」的四条理由：

1. **纯契约已被证明不够**：P0 的 6 个控件各自只认一个切片（事件表 / run 状态 / 审批 / 追问 / 收件箱 / 后台坞），把「事件流 → 6 个切片」的映射留给宿主，等于每个宿主重写一遍 06 §5.1 的状态机；这正是顶层组合层存在的理由。
2. **语义已经在 UI 层**：审批范围/风险分级、run 的阶段、收件箱的 attention 分级、事件的 row 状态，都是「界面语义」而不是执行语义；把它们做成 reducer 不越过 §7 边界。
3. **可测试性/可复用性**：纯 reducer（事件进、视图状态出）可以脱离 UI 单测、可重放、可录制；宿主替换模型/后端不影响 UI 行为。这是 06 §7.4「复用性/稳定性」的直接延伸。
4. **业界一致**：AG-UI 把前端职责定义为「消费事件流、重建消息、投影状态」；assistant-ui 用 `ExternalStoreRuntime` 承担事件解析与消息重建；Vercel AI SDK UI 的 `useChat` 在前端维护消息状态与流式增量；CopilotKit 明确「frontend / runtime / agent」三层，事件协议只做传输。Arc 的顶层对应「runtime 层」，宿主对应「agent + transport」。

同时**不内生**的四件事（必须留宿主）：

1. 模型/工具执行、重试/回退、并发调度；
2. 传输（HTTP/WS/IPC/本地进程）与鉴权；
3. 持久化/同步/迁移、审计落库；
4. 系统能力：托盘、通知投放、热键、调度、权限沙箱、MCP/语音管线。

**为什么不是「纯宿主注入」**：如果只保留现在的 6 个控件 + 宿主注入，`agent-attention-inbox` 与 `tool-approval-sheet` 无法知道「这个审批是否已被同一个 run 的 stop 作废」；`agent-transcript` 无法在不重建的情况下追加 token；`background-run-dock` 与 `run-control-bar` 的状态会各说各话。这三种「跨控件一致性」只能由 UI 侧共享的会话投影来保证——它就是表现层状态机。

### 3.4 业界对照

| 方案 | 前端职责 | 后端/宿主职责 | 对 Arc 顶层的启示 |
| --- | --- | --- | --- |
| **AG-UI 协议**（[schema](https://docs.ag-ui.com/spec/1.0/schema)、[events](https://docs.ag-ui.com/concepts/events)） | 消费事件流：`TEXT_MESSAGE_START/CONTENT/END`、`TOOL_CALL_START/ARGS/END/RESULT`、`STATE_SNAPSHOT/DELTA`、`MESSAGES_SNAPSHOT`、lifecycle/reasoning/subagent/activity 事件；重建消息与状态 | 产生事件、执行工具、维护 agent 状态 | Arc 的事件模型应覆盖同一语义族（Arc 可做「AG-UI 兼容的事件形状」，但不绑定传输） |
| **Vercel AI SDK UI**（[stream protocol](https://ai-sdk.dev/docs/ai-sdk-ui/stream-protocol)、[transport](https://ai-sdk.dev/docs/ai-sdk-ui/transport)） | `useChat` 维护消息状态、流式合并、工具调用 UI 状态；支持 text stream 与 data stream 两种协议 | 服务端生成流；transport 可替换 | 「UI 状态由前端 hook/模型持有、协议可换」正是 Arc `ArcAgentSession` 的定位 |
| **assistant-ui**（[AG-UI runtime](https://www.assistant-ui.com/docs/runtimes/ag-ui/overview.md)） | `ExternalStoreRuntime` 承担事件解析与消息重建；`Thread`/`Composer` 等原语只读 runtime | `HttpAgent`/后端 | 顶层 = runtime + primitives；控件只做渲染，状态在 runtime——与「ArcAgentSession + 叶子控件」同构 |
| **CopilotKit**（[architecture](https://docs.copilotkit.ai/concepts/architecture)） | Frontend SDK + 预置聊天组件 | Runtime（应用服务器内）+ Agent；AG-UI 作为 wire format（16 类事件、传输无关） | 明确「UI 与运行时之间只隔一层可检查的 wire format」；Arc 的 `IArcAgentTransport` 就是这层 wire format 的 .NET 接口化 |

> 外部内容为参考实现/协议文档，非本项目规范；抓取时间 2026-10-31。

---

## 4. 顶层组合层目标形态

### 4.1 分层

```text
┌────────────────────────────────────────────────────────────────────┐
│ 宿主应用（L5，不在本库）：模型/MCP/语音/调度/权限/持久化/托盘/通知      │
│   └─ 实现 IArcAgentTransport / IArcAgentUiServices                     │
├────────────────────────────────────────────────────────────────────┤
│ L4 Agent 顶层组合层（本审计建议新增；当前缺失）                        │
│   Arc.Avalonia.Agent（组合控件 + 表现层运行时 + 渲染器注册 + 治理 UI）  │
│   依赖：L1 + L2（Content/Inputs/Overlays/Feedback/Actions）            │
├────────────────────────────────────────────────────────────────────┤
│ L3 横向场景层（生活域）：LifeToday（reminder/brief/agenda/medication…）│
│   可选依赖 L4 的 Agent UI（06 §4.6 的 life → agent 方向）               │
├────────────────────────────────────────────────────────────────────┤
│ L2 中间层（通用 UI 框架）：Components 11 项目                          │
│   Content（ChatThread/Markdown）、Inputs、Overlays、Feedback、          │
│   Actions、Navigation、DataDisplay、Charts、Pickers、Effects、Media     │
├────────────────────────────────────────────────────────────────────┤
│ L1 基础层：Arc.Avalonia（token/theme/motion/icons/ArcUi/ArcVirtualList）│
│           + Arc.Avalonia.Internal（共享组合原语）                      │
└────────────────────────────────────────────────────────────────────┘
```

现状偏差：AgentUI / AIWidgets / AIExplainers **只依赖 L1，绕过 L2**（csproj + usings）；Blocks.AI 反而正确组合 L2。顶层化时必须把依赖方向修正为 L4 → L2 → L1。

### 4.2 项目/包形态（三个候选）

| 方案 | 结构 | 优点 | 缺点 | 建议 |
| --- | --- | --- | --- | --- |
| **A（推荐）** | `src/Agent/Arc.Avalonia.Agent`：会话模型 + 组合控件 + 渲染器 + 治理 UI；`AgentUI` 保留为叶子视图包（或并入） | 一次到位、依赖方向正确；可独立 `Arc.Avalonia.Agent` 包发布 | 新增 1 个工程与主题入口 | ✅ 顶层 P0 采用 |
| B | 在 `AgentUI` 内原地扩展 | 迁移成本最低 | 包名/职责混合（叶子+组合根），后续拆分成本高 | 仅适合时间极紧的临时方案 |
| C | `Arc.Avalonia.Agent.Abstractions`（纯模型，无 Avalonia）+ `Arc.Avalonia.Agent`（控件） | 模型可被宿主/测试直接引用 | 两包边界与版本管理更重；Arc 是 UI 库，纯模型收益有限 | P1 之后按需拆分 |

> 命名一致性：仓库既有 `Special.AIWidgets`/`Blocks.AI` 缺分隔符的问题（`docs/08:371-372`）；新顶层建议用 **`Arc.Avalonia.Agent`**（不叫 `AgentUI`，避免「只是 UI 叶子」的语义），内部命名空间 `Arc.Avalonia.Agent`。

### 4.3 候选 API 概形（签名级草案，不是实现）

> 下列代码只定义**公开契约的形状**，用来说明顶层该暴露什么、谁拥有状态；不包含实现、不改变任何源码。所有类型默认 `public`；事件回调一律 `EventHandler<T>`。

#### 4.3.1 事件模型（宿主 → UI 的唯一输入）

```csharp
namespace Arc.Avalonia.Agent;

/// <summary>单调序号 + run 标识：投影层用它做排序、幂等与去重。</summary>
public readonly record struct ArcEventStamp(string RunId, long Sequence);

public abstract record ArcAgentEvent(ArcEventStamp Stamp, DateTimeOffset At);

// —— 生命周期（对齐 AG-UI 的 Run/Step 语义） ——
public sealed record ArcRunStartedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string UserTurnId, ArcRunOptions Options) : ArcAgentEvent(Stamp, At);
public sealed record ArcRunPhaseChangedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    ArcRunPhase Phase) : ArcAgentEvent(Stamp, At);   // Running/Paused/AwaitingApproval/AwaitingInput/TakenOver/Stopped
public sealed record ArcStepStartedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string StepId, string Title) : ArcAgentEvent(Stamp, At);
public sealed record ArcStepFinishedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string StepId, ArcStepOutcome Outcome) : ArcAgentEvent(Stamp, At);

// —— 文本/推理增量（增量，不传全量） ——
public sealed record ArcMessageStartedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string MessageId, ArcAgentRole Role) : ArcAgentEvent(Stamp, At);
public sealed record ArcMessageDeltaEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string MessageId, string Delta) : ArcAgentEvent(Stamp, At);
public sealed record ArcMessageEndedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string MessageId, ArcMessageStopReason Reason) : ArcAgentEvent(Stamp, At);
public sealed record ArcReasoningDeltaEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string ReasoningId, string Delta) : ArcAgentEvent(Stamp, At);

// —— 工具调用（参数增量 + 结果） ——
public sealed record ArcToolCallStartedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string CallId, string ToolName, ArcRiskLevel Risk, string Intent) : ArcAgentEvent(Stamp, At);
public sealed record ArcToolArgsDeltaEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string CallId, string DeltaJson) : ArcAgentEvent(Stamp, At);
public sealed record ArcToolCallEndedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string CallId, ArcToolCallStatus Status, string? ResultSummary) : ArcAgentEvent(Stamp, At);

// —— 人机协同（审批/追问） ——
public sealed record ArcApprovalRequestedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    ArcApprovalRequest Request) : ArcAgentEvent(Stamp, At);
public sealed record ArcApprovalResolvedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string RequestId, ArcApprovalOutcome Outcome) : ArcAgentEvent(Stamp, At);
public sealed record ArcQuestionRequestedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    ArcQuestionRequest Request) : ArcAgentEvent(Stamp, At);

// —— 结果与编排 ——
public sealed record ArcArtifactProducedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    ArcArtifact Artifact) : ArcAgentEvent(Stamp, At);
public sealed record ArcSubAgentStartedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    ArcSubAgentLane Lane) : ArcAgentEvent(Stamp, At);
public sealed record ArcSubAgentUpdatedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    ArcSubAgentLane Lane) : ArcAgentEvent(Stamp, At);
public sealed record ArcSubAgentFinishedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string LaneId, ArcStepOutcome Outcome) : ArcAgentEvent(Stamp, At);
public sealed record ArcRunEndedEvent(ArcEventStamp Stamp, DateTimeOffset At,
    ArcRunOutcome Outcome, ArcTokenUsage? Usage) : ArcAgentEvent(Stamp, At);

// —— 逃生门：宿主私有事件/协议扩展 ——
public sealed record ArcCustomEvent(ArcEventStamp Stamp, DateTimeOffset At,
    string Kind, string PayloadJson) : ArcAgentEvent(Stamp, At);
```

#### 4.3.2 会话与消息模型（UI 侧投影，不是执行状态）

```csharp
namespace Arc.Avalonia.Agent;

public enum ArcAgentRole { User, Assistant, Tool, System, SubAgent }
public enum ArcRunPhase { Idle, Planning, Running, Paused, AwaitingApproval, AwaitingInput, Done, Failed, Stopped, TakenOver }
public enum ArcAgentItemKind { Message, Reasoning, ToolCall, Plan, Approval, Question, Artifact, SubAgent, Failure, Custom }

/// <summary>会话中的一条投影条目；Id 稳定，用于增量更新与展开态记忆。</summary>
public abstract class ArcAgentItem
{
    public required string Id { get; init; }
    public required ArcAgentItemKind Kind { get; init; }
    public ArcAgentItemStatus Status { get; set; } = ArcAgentItemStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ArcMessageItem : ArcAgentItem
{
    public ArcAgentRole Role { get; init; }
    public string Text { get; set; } = string.Empty;      // 由 delta 合并写入
    public bool IsStreaming { get; set; }
    public IReadOnlyList<ArcCitation> Citations { get; init; } = Array.Empty<ArcCitation>();
    public IReadOnlyList<ArcAttachment> Attachments { get; init; } = Array.Empty<ArcAttachment>();
    public int Version { get; set; }                       // 编辑/重生成
}

/// <summary>会话投影：只读视图状态 + 增量应用；由 UI 持有，不执行 I/O。</summary>
public interface IArcAgentSession
{
    string ConversationId { get; }
    ArcRunPhase Phase { get; }
    IReadOnlyList<ArcAgentItem> Items { get; }             // 或 INotifyCollectionChanged
    ArcAgentRunSummary Summary { get; }                     // 步骤/耗时/成本/模型
    ArcArtifactIndex Artifacts { get; }
    ArcAttentionQueue Attention { get; }

    // 唯一的写入口：宿主事件流经这里进入 UI；返回是否被接受（幂等/序号校验）
    bool Apply(ArcAgentEvent @event);

    // 宿主可批量回放（恢复会话/测试）
    void ApplyRange(IEnumerable<ArcAgentEvent> events);

    event EventHandler<ArcAgentItem>? ItemAdded;
    event EventHandler<ArcAgentItem>? ItemChanged;
    event EventHandler<string>? ItemRemoved;
    event EventHandler<ArcRunPhase>? PhaseChanged;
    event EventHandler? Reset;
}
```

#### 4.3.3 流式增量（高频更新契约）

```csharp
namespace Arc.Avalonia.Agent;

/// <summary>宿主把事件推给 UI 的入口；实现必须允许跨线程调用。</summary>
public interface IArcAgentEventSink
{
    void Push(ArcAgentEvent @event);          // 由宿主 transport 调用
    void Complete(ArcRunOutcome outcome);
    void Fail(Exception error);
}

/// <summary>帧级合并器：把毫秒级 delta 合并为 ≤1 次/帧的 UI 更新（默认 16 ms）。</summary>
public sealed class ArcDeltaCoalescer
{
    public ArcDeltaCoalescer(TimeSpan? frameInterval = null, IDispatcher? dispatcher = null);
    public void Enqueue(ArcAgentEvent @event);              // O(1)
    public event EventHandler<ArcAgentEventBatch>? Flush;   // 每帧一次
    public TimeSpan FrameInterval { get; init; }             // 默认 16 ms
    public int MaxBatchSize { get; init; }                   // 保护：默认 256
}
```

> 契约要求：`ArcMessageItem.Text` 追加必须 O(1)（StringBuilder/rope 或只在渲染时拼接）；transcript 的 item 容器必须支持「只更新最后一行」，禁止每次 delta 重建整棵树。

#### 4.3.4 工具调用与审批（含队列/串行/幂等）

```csharp
namespace Arc.Avalonia.Agent;

public sealed class ArcToolCallItem : ArcAgentItem
{
    public string ToolName { get; init; } = string.Empty;
    public ArcRiskLevel Risk { get; init; }
    public string Intent { get; init; } = string.Empty;
    public IReadOnlyList<ArcToolArgument> Arguments { get; set; } = Array.Empty<ArcToolArgument>();
    public string? ResultSummary { get; set; }
    public ArcApprovalRequest? Approval { get; set; }
}

public sealed class ArcApprovalRequest
{
    public required string RequestId { get; init; }
    public required string RunId { get; init; }
    public required string CallId { get; init; }
    public required string ToolName { get; init; }
    public ArcRiskLevel Risk { get; init; }
    public string Intent { get; init; } = string.Empty;
    public IReadOnlyList<ArcApprovalArgument> Arguments { get; init; } = Array.Empty<ArcApprovalArgument>();
    public IReadOnlyList<string> SideEffects { get; init; } = Array.Empty<string>();
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>审批队列：UI 侧串行化决策；同一 run 的多个请求按到达顺序呈现，不并发弹多张表。</summary>
public sealed class ArcApprovalQueue
{
    public ArcApprovalRequest? Current { get; }
    public IReadOnlyList<ArcApprovalRequest> Pending { get; }

    public bool Enqueue(ArcApprovalRequest request);          // 按 RequestId 去重
    public bool TryDequeue(string requestId, out ArcApprovalRequest request);
    public void MarkSubmitting(string requestId);             // 等待宿主确认
    public void MarkResolved(string requestId, ArcApprovalOutcome outcome);
    public void Expire(string requestId);                     // 由宿主时钟驱动
    public event EventHandler<ArcApprovalRequest>? CurrentChanged;
}

/// <summary>用户决策 → 宿主命令；UI 永不直接执行工具。</summary>
public sealed class ArcApprovalDecision
{
    public required string RequestId { get; init; }
    public required ArcApprovalOutcome Outcome { get; init; }  // Approve/Deny/Delegate/Defer
    public IReadOnlyList<ArcApprovalArgument> ModifiedArguments { get; init; } = Array.Empty<ArcApprovalArgument>();
    public ArcApprovalScope Scope { get; init; }               // Once/Task/Hour/Always（展示语义，策略由宿主判定）
    public string? DenyReason { get; init; }
    public string? DelegateTo { get; init; }
}
```

#### 4.3.5 并行子 agent

```csharp
namespace Arc.Avalonia.Agent;

public sealed class ArcSubAgentLane
{
    public required string LaneId { get; init; }
    public required string Name { get; init; }
    public string? ParentLaneId { get; init; }                 // 主 agent 拆分出的层级
    public ArcSubAgentRole Role { get; init; }                  // Lead/Worker/Reviewer...
    public ArcRunPhase Phase { get; set; }
    public double Progress { get; init; }                      // 0..1，宿主供数
    public string? CurrentStep { get; set; }
    public string? ModelName { get; init; }
    public IReadOnlyList<ArcTimelineSpan> Spans { get; init; } = Array.Empty<ArcTimelineSpan>();
    public IReadOnlyList<ArcHandoff> Handoffs { get; init; } = Array.Empty<ArcHandoff>();
}

/// <summary>并行面板的只读拓扑；取消/重试/合并都是宿主命令。</summary>
public sealed class ArcAgentTopology
{
    public IReadOnlyList<ArcSubAgentLane> Lanes { get; init; } = Array.Empty<ArcSubAgentLane>();
    public string? LeadLaneId { get; init; }
    public ArcRunPhase Phase { get; init; }
    public int MaxLaneCount { get; init; } = 8;                // 面板声明上限（06 §6 的虚拟化纪律）
}
```

#### 4.3.6 Artifact

```csharp
namespace Arc.Avalonia.Agent;

public enum ArcArtifactKind { File, Diff, Url, Json, Text, Image, Table }

public sealed class ArcArtifact
{
    public required string ArtifactId { get; init; }
    public required ArcArtifactKind Kind { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? MimeType { get; init; }
    public long? SizeBytes { get; init; }
    public string? DisplayText { get; init; }                  // 宿主已读好的展示文本（UI 不读文件）
    public object? Payload { get; init; }                      // 宿主自定义（不透明）
    public IReadOnlyList<ArcDiffLine>? DiffLines { get; init; }
}

public interface IArcArtifactPreviewFactory
{
    Control? Create(ArcArtifact artifact);                     // 宿主可注册图片/表格/自定义预览
}

public sealed class ArcArtifactIndex
{
    public IReadOnlyList<ArcArtifact> Items { get; }
    public ArcArtifact? Selected { get; set; }
    public event EventHandler<ArcArtifact>? ArtifactAdded;
    public event EventHandler? SelectionChanged;
}
```

#### 4.3.7 注意力收件箱（聚合，不是又一个列表）

```csharp
namespace Arc.Avalonia.Agent;

public enum ArcAttentionSource { Approval, Question, Failure, Routine, Suggestion, Tool }

public sealed class ArcAttentionEntry
{
    public required string EntryId { get; init; }              // 由源对象 id 派生，用于去重
    public required ArcAttentionSource Source { get; init; }
    public required string RunId { get; init; }
    public required string Title { get; init; }
    public string Detail { get; init; } = string.Empty;
    public ArcAttentionLevel Level { get; init; }              // Info/Important/Blocking
    public ArcAttentionState State { get; set; }               // New/Seen/InProgress/Resolved/Expired
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>从会话投影/宿主事件聚合「等你处理」；Top 排序：Blocking > Important > Info，再按时间。</summary>
public sealed class ArcAttentionQueue
{
    public IReadOnlyList<ArcAttentionEntry> Entries { get; }
    public int BlockingCount { get; }
    public void Upsert(ArcAttentionEntry entry);               // 按 EntryId 去重/合并
    public void Resolve(string entryId);
    public void Expire(string runId);
    public event EventHandler? Changed;
}

/// <summary>宿主可扩展的注意力来源（例如 routine 到期、离线队列冲突）。</summary>
public interface IArcAttentionSource
{
    string SourceId { get; }
    IReadOnlyList<ArcAttentionEntry> Snapshot();
    event EventHandler? Changed;
}
```

#### 4.3.8 可插拔渲染器（template selector / registry）

```csharp
namespace Arc.Avalonia.Agent;

/// <summary>一条 AgentItem → 控件的渲染契约；宿主/产品可覆盖任意 kind，未覆盖时回退到内置。</summary>
public interface IArcAgentItemRenderer
{
    bool CanRender(ArcAgentItem item);
    Control Render(ArcAgentItem item, IArcAgentRendererContext context);
}

public interface IArcAgentRendererContext
{
    IArcAgentSession Session { get; }
    IArcAgentCommandSink Commands { get; }
    IArcAgentUiServices Ui { get; }                            // 打开链接/复制/确认框（宿主可空实现）
}

public sealed class ArcAgentRendererRegistry
{
    public void Register(ArcAgentItemKind kind, IArcAgentItemRenderer renderer);
    public void Register(ArcAgentItemKind kind, Func<ArcAgentItem, IArcAgentRendererContext, Control?> factory);
    public IReadOnlyList<IArcAgentItemRenderer> Fallbacks { get; }   // 内置渲染器链
    public Control Render(ArcAgentItem item, IArcAgentRendererContext context);
}

/// <summary>Avalonia 原生路线：模板选择器 + 注册表二选一或组合使用。</summary>
public sealed class ArcAgentTemplateSelector : DataTemplateSelector
{
    public required ArcAgentRendererRegistry Registry { get; init; }
    protected override IDataTemplate? SelectTemplateCore(object item, Control container);
}
```

#### 4.3.9 宿主注入点与事件总线

```csharp
namespace Arc.Avalonia.Agent;

/// <summary>UI → 宿主的命令出口；ArcAgentSurface 实现它，叶子控件只依赖它。</summary>
public interface IArcAgentCommandSink
{
    Task SendAsync(ArcAgentCommand command, CancellationToken ct = default);
    event EventHandler<ArcAgentCommand>? CommandDispatched;   // 供日志/调试/无 Transport 的宿主订阅
}

/// <summary>宿主运行时（Agent 执行 + 传输 + 持久化）的唯一入口；UI 只认这个接口。</summary>
public interface IArcAgentTransport
{
    /// <summary>提交一轮用户输入；事件通过 sink 回流（允许跨线程）。</summary>
    Task<string> StartTurnAsync(ArcAgentTurnRequest request, IArcAgentEventSink sink, CancellationToken ct);

    /// <summary>发送控制命令：Stop/Pause/Resume/Steer/Approve/Deny/Defer/Retry/Takeover。</summary>
    Task SendCommandAsync(string runId, ArcAgentCommand command, CancellationToken ct);

    /// <summary>恢复历史会话（可返回快照事件序列）。可选能力。</summary>
    Task<IReadOnlyList<ArcAgentEvent>?> LoadHistoryAsync(string conversationId, CancellationToken ct);
}

/// <summary>UI 侧主动发出的命令；每个命令都对应一个现有控件的意图事件。</summary>
public abstract record ArcAgentCommand(string RunId);
public sealed record ArcSendMessageCommand(string RunId, string Text, IReadOnlyList<ArcAttachment> Attachments) : ArcAgentCommand(RunId);
public sealed record ArcStopCommand(string RunId, ArcStopMode Mode) : ArcAgentCommand(RunId);       // Immediate/AfterStep
public sealed record ArcPauseCommand(string RunId) : ArcAgentCommand(RunId);
public sealed record ArcResumeCommand(string RunId) : ArcAgentCommand(RunId);
public sealed record ArcSteerCommand(string RunId, string Instruction) : ArcAgentCommand(RunId);
public sealed record ArcTakeoverCommand(string RunId) : ArcAgentCommand(RunId);
public sealed record ArcApprovalCommand(string RunId, ArcApprovalDecision Decision) : ArcAgentCommand(RunId);
public sealed record ArcAnswerCommand(string RunId, ArcQuestionAnswer Answer) : ArcAgentCommand(RunId);
public sealed record ArcRetryEventCommand(string RunId, string EventId) : ArcAgentCommand(RunId);
public sealed record ArcOpenItemCommand(string RunId, string ItemId) : ArcAgentCommand(RunId);     // 收件箱/artifact 打开

/// <summary>UI 需要宿主提供的环境能力；全部可选，默认无操作。</summary>
public interface IArcAgentUiServices
{
    Task OpenExternalAsync(Uri uri, CancellationToken ct = default);
    Task CopyTextAsync(string text, CancellationToken ct = default);
    Task<bool> ConfirmAsync(string title, string message, CancellationToken ct = default);
    void BringToFront();                                       // 后台坞「打开」时由宿主实现，可空
}

/// <summary>组合根的装配：宿主在启动时注入一次；之后不可替换（会话级）。</summary>
public sealed class ArcAgentHostAdapter
{
    public required IArcAgentTransport Transport { get; init; }
    public IArcAgentUiServices Ui { get; init; } = new ArcAgentNullUiServices();
    public IArcAgentEventSink? Sink { get; init; }             // 由 ArcAgentSurface 内部创建时留空
}
```

#### 4.3.10 组合根与叶子控件

```csharp
namespace Arc.Avalonia.Agent.Controls;

/// <summary>
/// 顶层组合根：把会话投影、叶子控件、审批队列、收件箱、并行面板装成任意宿主可嵌入的表面。
/// 不假设路由/窗口/快捷键；不发起任何 I/O。
/// </summary>
public class ArcAgentSurface : ContentControl
{
    // 宿主注入（唯一必需项）：Transport 为 null 时进入「Sample 会话」模式，可独立渲染
    public static readonly StyledProperty<IArcAgentSession?> SessionProperty;
    public static readonly StyledProperty<IArcAgentTransport?> TransportProperty;
    public static readonly StyledProperty<IArcAgentRendererRegistry?> RenderersProperty;
    public static readonly StyledProperty<ArcAgentSurfaceLayout> LayoutProperty;   // Chat/SidePanel/Dock/FullPage
    public static readonly StyledProperty<bool> ShowAttentionInboxProperty;
    public static readonly StyledProperty<bool> ShowSubAgentBoardProperty;
    public static readonly StyledProperty<bool> ShowArtifactsProperty;

    public IArcAgentCommandSink Commands { get; }                 // 由 Surface 实现并暴露给叶子控件
    public ArcApprovalQueue Approvals { get; }
    public ArcAttentionQueue Attention { get; }
    public ArcArtifactIndex Artifacts { get; }

    public event EventHandler<ArcAgentCommand>? CommandRequested; // 可选：宿主也可直接订阅而不实现 Transport
}

/// <summary>输入条：文本、附件、/ 命令、@ 提及、模型/思考强度、发送/停止。</summary>
public class ArcAgentComposer : Panel
{
    public static readonly StyledProperty<string> TextProperty;
    public static readonly StyledProperty<bool> IsRunningProperty;       // 控制「发送↔停止」
    public static readonly StyledProperty<IEnumerable<ArcComposerCommand>?> CommandsProperty;
    public static readonly StyledProperty<IEnumerable<ArcModelOption>?> ModelsProperty;
    public event EventHandler<ArcComposerSubmit>? SubmitRequested;      // 文本 + 附件 + 模型 + 思考强度
    public event EventHandler? StopRequested;
}

/// <summary>流式文本块：增量追加、光标、reduced-motion、引用角标。</summary>
public class ArcAgentStreamingText : Panel
{
    public static readonly StyledProperty<string> TextProperty;          // 由合并器更新
    public static readonly StyledProperty<bool> IsStreamingProperty;
    public static readonly StyledProperty<IEnumerable<ArcCitation>?> CitationsProperty;
    public event EventHandler<ArcCitation>? CitationActivated;
}

/// <summary>工具调用卡片：生命周期 + 参数（脱敏） + 结果摘要 + 打开详情/审批入口。</summary>
public class ArcAgentToolCallCard : Panel
{
    public static readonly StyledProperty<ArcToolCallItem?> CallProperty;
    public event EventHandler<string>? ApproveRequested;
    public event EventHandler<string>? DenyRequested;
    public event EventHandler<string>? RetryRequested;
    public event EventHandler<string>? DetailsRequested;
}

/// <summary>审批托盘：把 ArcApprovalQueue 的 Current 交给 ArcToolApprovalSheet 渲染。</summary>
public class ArcAgentApprovalTray : Panel
{
    public static readonly StyledProperty<ArcApprovalQueue?> QueueProperty;
    // 复用现有 ArcToolApprovalSheet 作为内部视图
}

/// <summary>并行子 agent 面板：lead + lanes + handoff 连线；只读拓扑。</summary>
public class ArcAgentSubAgentBoard : Panel
{
    public static readonly StyledProperty<ArcAgentTopology?> TopologyProperty;
    public event EventHandler<string>? LaneActivated;
    public event EventHandler<string>? LaneCancelRequested;
    public event EventHandler<string>? LaneRetryRequested;
}

/// <summary>artifact 预览：按 ArcArtifactKind 走渲染器，宿主可覆盖。</summary>
public class ArcAgentArtifactPreview : Panel
{
    public static readonly StyledProperty<ArcArtifact?> ArtifactProperty;
    public static readonly StyledProperty<IArcArtifactPreviewFactory?> PreviewFactoryProperty;
    public event EventHandler<ArcArtifact>? OpenRequested;
    public event EventHandler<ArcArtifact>? CopyRequested;
}
```

### 4.4 归属表：「UI 层可做 / 必须留给宿主 / 暂不做」

| 能力 | UI 层可做（顶层交付） | 必须留给宿主 | 暂不做 |
| --- | --- | --- | --- |
| 事件→视图投影 | ✅ 纯 reducer、事件排序/幂等/重放、消息增量合并 | 产生事件、保证序号 | — |
| 会话/消息模型 | ✅ 视图模型、消息版本、引用/附件展示模型 | 持久化、同步、跨设备 | 服务端会话/多用户 |
| 流式 | ✅ delta 合并、帧级刷新、贴底/jump-to-latest、光标/思考态 | 生成 token、背压策略 | 自研 tokenizer |
| 工具调用 | ✅ 生命周期卡片、参数展示（脱敏）、结果摘要、重试入口 | 执行工具、参数校验、重试策略 | 工具 SDK |
| 审批 | ✅ 审批队列、范围选择 UI、过期展示、拒绝理由、撤销入口 | 策略判定、权限校验、审计落库、过期强制 | 自动批准策略引擎 |
| 并行子 agent | ✅ 拓扑/lane/进度/移交展示、取消/重试入口 | 编排、并发调度、资源分配 | 分布式 agent 调度 |
| artifact | ✅ 索引/预览/打开/复制；宿主供给展示数据 | 读写文件、生成 diff、存储 | 云存储/版本库 |
| 注意力收件箱 | ✅ 聚合/去重/排序/已读态 | 通知投放、升级策略、跨应用来源 | 系统通知中心本体 |
| 渲染器 | ✅ registry/template selector/内置渲染器 | 业务自定义控件 | 动态插件热加载 |
| 事件总线 | ✅ 会话级 `IArcAgentEventSink`/`IArcAgentTransport` 接口 | 传输、鉴权、重试、断线恢复 | AG-UI wire server |
| 系统能力 | ❌（只提供入口/状态位） | 托盘、热键、通知、调度、开机自启 | — |
| 模型/MCP/语音 | ❌（只提供展示位与设置 UI） | 全部 | — |

---

## 5. 复用率与工作量

### 5.1 复用分档（按源码行数，基线 30,353 行）

| 档 | 内容 | 控件数 | LOC | 占比 | 处理方式 |
| --- | --- | --- | --- | --- | --- |
| **A 直接可用**（契约清洁、无需改签名） | AgentUI 6 控件 | 6 | 3,192 | **10.5%** | 保留为顶层叶子视图；补 `ItemsSource` 增量适配与主题化 |
| **B 需改签名**（补事件/增量/重写交互） | AIWidgets 5 个 Agent 语义件（status/tool-call/plan/timeline/context-window，3,869）+ `ArcChatThread`（1,080） | 6 | 4,949 | **16.3%** | 契约化改造；status/tool-call 优先，plan/timeline 次之 |
| **C 示例拆解**（页面 → 组件配方） | Blocks.AI 18 页（5,480）+ `AiKit`（1,014） | 0 | 6,494 | **21.4%** | 保留为示例块；抽 6 个关键页（chat/composer/side-panel/research-run/orchestrator/approvals）重写为顶层组件 |
| **D 保留/下沉**（不进 Agent 顶层） | AIWidgets 6 个氛围件 + 7 个 public 辅助子控件 + AIExplainers 11 控件 + `ArcExplainer` | 25 | 8,022（7,563 + 459） | **26.4%** | 保留在 Special；辅助子控件下沉 internal |
| **L 横向场景层**（不属顶层） | LifeToday 7 控件 | 7 | 3,832 | **12.6%** | 移出 `src/Agent/` 或标记为场景层；可依赖顶层 |
| **P 页面脚手架**（不可复用，需重写） | 各项目 `Pages/*.cs`（AgentUI 1,044 + LifeToday 714 + AIWidgets 1,356 + AIExplainers 663 + chat 87 + Blocks.AI 5,480 已计入 C） | — | 3,864（不含 Blocks 页） | **12.7%** | 迁移为顶层组合页；不直接搬运 |

### 5.2 顶层 P0 新增组件与页面

| 类型 | 交付物 | 说明 |
| --- | --- | --- |
| 运行时模型（非控件） | `IArcAgentSession` + 事件模型 + reducer + `ArcDeltaCoalescer` + `ArcApprovalQueue` + `ArcAttentionQueue` + `ArcArtifactIndex` + `ArcAgentRendererRegistry` | 顶层核心，无 Avalonia 控件依赖（但可引用 Avalonia 的 dispatcher 类型，或抽象 `IDispatcher`） |
| 组合控件 | `ArcAgentSurface`、`ArcAgentComposer`、`ArcAgentStreamingText`、`ArcAgentToolCallCard`、`ArcAgentApprovalTray` | 5 个新控件（复用 transcript/inbox/run-bar/dock/ask-card/approval-sheet 6 个叶子） |
| 页面（新增） | Agent surface（组合根）、Streaming & interruption、Approval queue | **3 个新页**；另有 1 个改造（Agent transcript 增量演示） |
| 改造 | `ArcAgentTranscript` 接 `ArcVirtualList` + 增量 item sink；`ArcChatThread` 增 `IArcAgentMessageSink` 适配 | 2 个改签名 |

### 5.3 顶层 P1 新增组件与页面

| 类型 | 交付物 | 数量 |
| --- | --- | --- |
| 控件 | `ArcAgentSubAgentBoard`、`ArcAgentArtifactPreview`、`ArcAgentDiffReview`、`ArcAgentRunHistory`、`ArcAgentTraceViewer`、`ArcAgentHandoffBanner`、`ArcAgentBudgetBar`、`ArcAgentGuardrailAlert`、`ArcAgentMemoryReview`、`ArcAgentConnectorHealth` | 10 |
| 页面 | 并行子 agent、artifact/diff、运行历史/审计、记忆周检、连接器健康、桌面设置 | 6 |
| 复用 | 06 的 P1 项中已有设计的（permission-scope-picker、diff-review、handoff、budget、checkpoint、voice-session、memory、audit、connector、offline、local-model、desktop-settings、notification-routing、trust-rules）按顶层契约落地 | 14 项（计 35 人日） |

### 5.4 人日分档

| 工作包 | S | M | L |
| --- | --- | --- | --- |
| W1 事件/会话模型 + reducer + 幂等/重放 | 8 | 8 | 8 |
| W2 增量 transcript + 流式文本 + 合并器 + 虚拟化接入 | 14 | 14 | 14 |
| W3 工具卡片 + 审批队列 + 范围/过期/拒绝语义 | 8 | 8 | 8 |
| W4 并行子 agent 面板 + artifact 预览 + diff 审查 | — | 10 | 10 |
| W5 渲染器注册 + 注意力聚合 + 宿主注入/命令总线 | 5 | 5 | 5 |
| W6 Gallery 页/文档/明暗截图/a11y/性能护栏 | 6 | 10 | 10 |
| W7 迁移与返工（transcript 改造、Blocks.AI 6 页重写、catalog/docs/截图基线） | 5 | 14 | 22 |
| W8 06 的 14 个 Agent P1 项（治理/审计/记忆等 UI） | — | 35 | 35 |
| W9 06 剩余 7 个非重叠 Agent 项（autonomy-dial/guardrail/hotkey/trace/路由/历史/语音确认） | — | — | 23 |
| **合计** | **46** | **104** | **135** |
| 2–3 人周期（5 天/周，含评审/联调 20%） | 2 人 ≈ 5 周 / 3 人 ≈ 3 周 | 3 人 ≈ 7 周 / 4 人 ≈ 5.5 周 | 3 人 ≈ 9–10 周 / 4 人 ≈ 7 周 |

> 说明：W8 与 06 的 95 人日 Agent 目录部分**不重复计价**（06 已含 P0 36 人日、P1 38、P2 21；W8 只覆盖与顶层强耦合、需要按新契约重做的 14 项）。L 档约等于「06 全量 Agent 95 + 顶层净新增 69」再扣与 W8 重叠的 35 人日，与 §0.4 的 135 量级一致。

### 5.5 与 06 扩展目录的计价关系

| 集合 | 项数 | 人日（06 口径） | 与顶层关系 |
| --- | --- | --- | --- |
| 06 Agent P0（7 项） | 7 | 36 | **已实现**（P0 报告），顶层直接复用 |
| 06 Agent P1（16 项） | 16 | 38 | 其中 14 项与顶层治理 UI 强相关 → W8；2 项（autonomy-dial/guardrail-alert）→ W9 |
| 06 Agent P2（5 项） | 5 | 21 | 3 项（trace/model-routing/run-history）→ W9；2 项（spoken-confirmation/global-hotkey）→ W9 |
| 总计 | 28 | 95 | 顶层化后仍需要这些 UI，但**必须按新契约重做**，不能把旧实现原样上提 |

> **注意**：`docs/arc-extension-catalog.json` 没有 `status` 字段，7 个已实现的 P0 仍显示为未完成；且 `project` 字段仍指向旧项目（`agent-transcript` → `Arc.Avalonia.Content`、`agent-ask-card` → `Arc.Avalonia.Special.AIWidgets`、`agent-attention-inbox` → `Arc.Avalonia.Blocks.AI`、`routine-schedule-editor` → `Arc.Avalonia.Pickers` 等），与工作树实际位置不一致——顶层迁移时应同批修正（§6.5）。

---

## 6. Gallery 迁移影响面

### 6.1 工程接线

| 位置 | 现状 | 顶层化改动 | 影响 |
| --- | --- | --- | --- |
| `src/Apps/Arc.Avalonia.Gallery/App.axaml:21` | `StyleInclude avares://Arc.Avalonia.AgentUI/Themes/ArcAgentUITheme.axaml` | 若新增 `Arc.Avalonia.Agent`：加 1 个 `StyleInclude`；若合并/弃用 AgentUI：删 1 个 | 1 行级 |
| `App.axaml:21-22` | AgentUI 与 LifeToday 两个空主题；LifeToday 的 include 与 Glass 同行（格式化瑕疵） | 新顶层主题应真正承载 `ControlTheme`（如果新增 templated 控件） | 1–2 行 |
| `Gallery.csproj` | 引用 27 个工程（不含 Tests，含 Agent 2 个） | 新增 1 个 `ProjectReference`（若合并则改路径） | 1 行 |
| `ArcUI.slnx` | `/Agent/` 文件夹只有 AgentUI、LifeToday | 新增 `Arc.Avalonia.Agent`（可选把 LifeToday 移到 `/Scenarios/`） | 1–2 行 |
| `ArcPageRegistry.cs:21-45` | 反射扫描输出目录中所有 `Arc.Avalonia*.dll` 的 `ArcPage` 子类 | 无需改代码（新工程名以 `Arc.Avalonia` 开头即可被发现） | 0 行 |
| `ArcDocStore.cs:25-27,97-113` | 扫描所有 `Arc.Avalonia*` 程序集的 `*.Docs.*.json` | 新工程若进入 294 目录：csproj 加 `<EmbeddedResource Include="Docs\*.json" />` | 0–1 行 |

> 结论：**页面发现与文档加载是「约定优于注册」的**，新增顶层工程不需要改 Gallery 代码；成本集中在主题入口、工程引用、目录条目与页面搬迁。

### 6.2 catalog 与文档

| 项 | 现状 | 顶层化影响 |
| --- | --- | --- |
| `docs/arc-catalog.json` = `src/Apps/Arc.Avalonia.Demo/Catalog/arc-catalog.json` | 两份文件 113,204 B，SHA256 相同（`8EBA6084…`） | 任何目录条目增删都要**同步两份**（否则 Demo 内嵌 catalog 与 docs 不一致） |
| 294 项 / 294 Docs JSON | 一 item 一页一文档（工作树已完成迁移） | 顶层若新增 catalog 条目：+1 catalog 项 + 1 `Docs/<slug>.json` + 1 `ArcItemPage` + 截图；否则 audit 报 missing |
| 40 个 AI 命名条目 | 11+11+18 | 顶层吸收 Blocks.AI 的关键页**不应删除**这些条目（否则 294 目录少项）；应保留为示例块并在文档页链接到新的顶层组件 |
| Agent P0/LifeToday 页面 | 14 个 P0 场景页（6 AgentUI + 7 LifeToday + Virtualized list，不进目录） | 顶层新增页面同样用 `ArcPage`，避免扰动 294 基线 |
| 06 扩展目录 | 57 项（28 Agent） | 顶层新组件若要对使用者可见：加入 extension catalog（新增条目，不动 294）；否则仅文档说明 |

### 6.3 audit / smoke / shots 门槛的数字变化

| 工具 | 当前口径/门槛 | 新增顶层 P0 后 | 证据 |
| --- | --- | --- | --- |
| `--audit` | 294 items / 294 docs / 294 item pages；`errors=0 pageFailures=0`（`README.md:208-209`） | 若顶层只加 `ArcPage`：items/docs/itemPages 不变；若加 catalog 条目：同步 +N，audit 仍须 0 error | `CatalogAudit.cs:18-40` |
| `--smoke` | 遍历 294 slug + 全部 `ArcPage` + 主题/accent 切换 + 搜索；`failures=0` | 315 → 315+3（S 档 3 页）或 +6（M 档）页；smoke 时间线性增加 | `NavigationSmoke.cs:29-49` |
| `--shot` | 所有页面 × 明暗 → 当前 315×2 = 630 张；每页自然高度，harness 上限 5200 px（设计目标 ≤1400，`docs/07:99`） | +3 页 → 636 张；+6 页 → 642 张；新增页必须无 `SHOT WARNING` | `ShotRunner.cs:23,47-49` |
| 页面高度 | P0 页面实测 526–1311 px | 组合根/并行面板易超高 → 拆页或切片（`--offset` 已支持） | `docs/07:80-99`、`README.md:221-229` |

### 6.4 页面归属迁移的影响

| 迁移 | 影响 | 风险 |
| --- | --- | --- |
| AgentUI 6 页 → 新 `Arc.Avalonia.Agent` | 页面类可整体搬迁，`Group="P0 · Agent UI"`、Order 20001–20006 不变；Gallery 自动发现 | 若两个程序集同时存在旧/新页面 → 导航中出现两份同 Title/Slug 的页面，smoke/截图重复；必须先删旧再加新 |
| Blocks.AI 关键页重写为顶层组件示例 | 页面保留在 Blocks.AI（catalog 条目不变），内部改为组合顶层控件 | 重写期间页面视觉可能回归；需逐页截图对照 |
| LifeToday 移出 `src/Agent/` | 若只改目录不改命名空间：无功能影响；若改命名空间：7 页 + 7 控件 + 引用方（Gallery、App.axaml）同步改 | 与 `docs/08` 的分组文档不一致 |
| 新顶层工程的 `Docs/*.json` | 场景页不需要 Docs JSON（`ArcPage` 无 `ArcItemPage.Doc`）；若加目录条目才需要 | 照抄 csproj 的 `EmbeddedResource` 但目录不存在不会报错（本工作树 AgentUI/LifeToday csproj 已有此情况） |

### 6.5 文档漂移清单（顶层迁移时应同批修）

| # | 漂移 | 证据 | 建议 |
| --- | --- | --- | --- |
| 1 | `docs/07` §8 文件索引仍指向旧位置（`Content/Pages/AgentTranscriptPage.cs`、`Special.AIWidgets/Pages/RunControlBarPage.cs` 等） | `docs/07:143-154`；实际在 `src/Agent/Arc.Avalonia.AgentUI/Pages/` | 改为当前路径，并标注 P1 迁移 |
| 2 | `docs/08` 把 P0 控件归在 `Special.AIWidgets`/`Blocks.AI` | `docs/08:350-353` | 改为 AgentUI |
| 3 | extension catalog 的 `project`/`byProject` 仍是旧项目 | `docs/arc-extension-catalog.json`（`agent-transcript.project = Arc.Avalonia.Content` 等） | 按实际项目重算 |
| 4 | extension catalog 无 `status`，已实现的 7 个 P0 未标完成 | JSON 无 status 字段；`docs/07:11` 称已实现 | 增加 `status`/`implementedIn` 或移除该维度 |
| 5 | `docs/06` 的「24 个项目」 | `docs/06:826`；实际 29（`ArcUI.slnx`） | 更新数字 |
| 6 | 文档称 `ArcVirtualList` 已就绪但 transcript 未接 | `docs/07:120` | 顶层 W2 关闭此限制并更新文档 |
| 7 | `docs/03`/`docs/07` 的页面/截图数字（219/233/438/466） | `docs/07:15-17`；`README.md:232-233` 已到 315 页 | 以 README 315 为当前基线 |

---

## 7. 顶层化验收标准

### 7.1 契约与功能（P0 门槛）

| # | 验收项 | 判定 |
| --- | --- | --- |
| 1 | 事件模型覆盖 message/reasoning/tool/approval/question/artifact/subagent/lifecycle 八族；未知事件走 `ArcCustomEvent` | §4.3.1 全部类型有单测（应用/排序/幂等/重放） |
| 2 | reducer 纯函数：同输入事件序列 → 同视图状态；无 I/O、无定时器（显示时钟除外） | 代码审查 + 单测；静态扫描无 `HttpClient/File./Directory./Process.` |
| 3 | 流式增量：10,000 事件的 transcript 在 60 chunks/s 下追加 O(1)，无整树重建 | 压力页 + 帧时间探针；`ArcAgentTranscript` 不再 `Children.Clear()` |
| 4 | 审批串行/幂等/过期：同一 run 多请求按序；重复/过期决策被忽略；UI 不执行工具 | 交错场景单测（approve→stop、approve→expire、deny→retry） |
| 5 | 并行 lane：拓扑/进度/移交展示；取消是命令不是本地状态 | 3 lane 演示页 + 命令断言 |
| 6 | 渲染器可替换：注册自定义 kind 后 transcript/tool card 渲染自定义控件；未注册回退内置 | 测试用 renderer registry 覆盖 1 个 kind |
| 7 | 无宿主可渲染：`ArcAgentSurface` 不注入 Transport 时使用 SampleSession，四态（空/加载/错误/内容）可见 | Gallery 页 + 单测 |
| 8 | 主题/密度：8 accent × 明暗 × 紧凑/舒适不破版（受 `docs/07:124` 快照限制的页面级验收） | 截图矩阵抽查 |
| 9 | a11y：每个新控件有 Automation 名称/角色；流式用 polite live region；审批焦点陷阱；键盘主路径 | 人工走查 + AutomationProperties 统计 |
| 10 | reduced-motion：流式光标/思考动画在 reduced-motion 下保留语义、去掉运动 | 设置 `ArcThemeManager.ReducedMotion` 后截图对比 |

### 7.2 Gallery 门槛

| 门槛 | 目标 |
| --- | --- |
| `--audit` | `missing=0 / pageFailures=0 / errors=0`；若未新增 catalog 条目，294 基线不变 |
| `--smoke` | `failures=0`；新增页面全部可达、可重复挂载；itemShows=294 |
| `--shot` | 新增页明暗各 1 张，无 `SHOT WARNING`；单页高度 ≤1400 px（超长拆分） |
| Gallery 页面 | P0 新增 3 页（组合根 / 流式与打断 / 审批队列），每页 ≥3 个状态 |
| 文档 | 除本审计文件外，至少更新 06/07 的「顶层化」章节；每个新控件有 When/When not/Keyboard/Motion/Implementation notes |

### 7.3 无宿主依赖的独立可渲染性

- `ArcAgentSurface` 的 `Transport == null` 时进入 **SampleSession**（内置确定性事件序列），支持空/加载/错误/内容四态；
- 叶子控件保留现有 sample 默认值（`ArcAgentTranscript.cs:127`、`ArcAgentAskCard.cs:78`、`ArcAgentAttentionInbox.cs:105`、`ArcToolApprovalSheet.cs:118`）；
- 验收：`--shot` 在**不启动任何宿主**的情况下渲染全部新增页；页面内无 `HttpClient`/文件/系统调用。

---

## 8. 风险与缓解

### 8.1 三大风险（必读）

| # | 风险 | 触发条件 | 影响 | 缓解 | 验收探针 |
| --- | --- | --- | --- | --- | --- |
| R1 | **UI 库做运行时状态机 → 宿主锁定 / 版本漂移** | 顶层 reducer 越界持有模型调用、持久化、重试、定时器；或命令接口绑死某家 SDK | 宿主无法替换后端；UI 升级带行为变化；与 06 §7 冲突 | (a) 边界写进 XML 文档与 API 审查清单：reducer 无 I/O、无系统调用；(b) 传输/执行全部在 `IArcAgentTransport` 后；(c) `ArcAgentEvent` 只做结构，不做语义推断（重试/取消由宿主决定）；(d) 版本化事件 schema + `ArcCustomEvent` 逃生门 | 静态扫描 + 边界单测；公开 API 里不出现 `HttpClient`/`Stream`/`File`/`Process`/`DispatcherTimer`（显示时钟除外） |
| R2 | **流式高频更新性能** | 每 token/chunk 重设 `Items`/`Text` 触发全量重建；长会话（>2k 事件）无虚拟化 | 界面卡顿/掉帧；CPU 飙升；长会话不可用 | (a) `ArcDeltaCoalescer` 16 ms 批处理；(b) 集合增量 + 稳定 Id + `ArcVirtualList`；(c) 文本 O(1) 追加、只刷新最后一行；(d) 压力页 10k 事件 / 60 chunks/s 门禁 | `--shot` 页 + 压力探针：追加 10k 事件内存稳定、帧时间 ≤16.7 ms、无全量重建（计数 `Children.Clear` 调用=0） |
| R3 | **审批/中断语义的并发正确性** | approve/deny/expire/stop/takeover/steer 交错；后台 run 与前台 run 同时请求；宿主重复投递事件 | 用户看到已作废的审批；重复执行敏感操作；状态错乱 | (a) `RequestId` 幂等 + `Sequence` 单调校验（乱序丢弃/缓冲）；(b) 审批队列串行、`Submitting` 态等待宿主确认；(c) 过期由宿主时钟驱动，UI 只禁用+显示；(d) 交错场景单测（§7.1 #4）；(e) 决策事件带 runId/callId，宿主可拒绝迟到的决策 | 交错状态测试矩阵；重复事件重放测试；乐观 UI 回滚测试 |

### 8.2 其它风险（次优先）

| 风险 | 说明 | 缓解 |
| --- | --- | --- |
| R4 主题快照 | 组合控件构建时取 token，运行时换主题需重建页面（`docs/07:124`；`ArcUi.cs:15-18` 的 docstring 与实现有张力） | 顶层页面对主题切换做「重建」策略并写入文档；新增 templated 控件用 `ControlTheme` |
| R5 Demo 耦合 | 所有 Agent 项目引用 Demo（页面/文档契约），顶层无法作为干净 NuGet 包发布 | 顶层 P0 先完成「页面/文档契约外置」或明确「Gallery 内发布」；与 B 审计联动 |
| R6 API 风格分裂 | `EventHandler<T>`（AgentUI）vs `Action<T>`（LifeToday）；`IEnumerable` vs `IReadOnlyList` | 顶层统一 `EventHandler<T>` + `IReadOnlyList`；旧控件保留兼容适配器 |
| R7 Blocks.AI 双维护 | 顶层组件与 Blocks.AI 页面视觉重复 | Blocks.AI 页面改为组合顶层控件（一次重写，长期单源） |
| R8 目录/文档漂移 | 见 §6.5，审计/文档与代码不一致会误导后续实现 | 顶层迁移 PR 同批修正 |

---

## 9. 结论与建议路线

### 9.1 回答「顶层 Agent 组合层该长什么样」

**一句话**：一个**只做事件投影与组合**的 `Arc.Avalonia.Agent` 层——`IArcAgentSession`（纯 reducer 投影）+ `ArcAgentSurface`（组合根）+ 5 个组合控件 + 渲染器注册表 + 审批/注意力/artifact 三个会话级队列，宿主通过 `IArcAgentTransport`/`IArcAgentCommandSink`/`IArcAgentUiServices` 三处注入；执行、传输、持久化、系统能力全部留在宿主。

### 9.2 建议路线

| 阶段 | 内容 | 人日 | 退出标准 |
| --- | --- | --- | --- |
| 0（已完成） | P0 14 控件 / 14 页 | 已在库 | `docs/07` 报告 |
| 1 | W1+W2+W3+W5：模型/流式/审批/渲染器 + 改造 transcript | 35 | reducer/合并器单测通过；transcript 10k 事件不重建 |
| 2 | 组合根 `ArcAgentSurface` + composer + 审批托盘 + 3 页 + a11y/性能 | 11 | 3 页 `--shot` 明暗通过；无宿主可渲染 |
| 3 | W4：并行 board + artifact + diff | 10 | 并行/artifact 页通过；命令断言 |
| 4 | W8：治理 UI（06 的 14 项 Agent P1） | 35 | 06 §9.2 的 5 条状态走查全部通过 |
| 5 | W7+W9：迁移收尾 + 06 剩余 7 项 + Blocks.AI 全量重写 | 44 | 294 目录 audit 0 错；文档漂移清零 |
| **合计** | | **≈ 135** | — |

### 9.3 判断回填

- **Blocks.AI**：保留为示例块（不整包吸收）；顶层吸收其组合配方，关键 6 页改写为组合顶层的示例。
- **LifeToday**：横向生活场景层，不属于 Agent 顶层；可依赖顶层 Agent UI（06 §4.6 方向）。
- **内生运行时**：内生「表现层状态机」，不内生「执行运行时」；判定标准见 §3.3/§4.4。

---

## 10. 证据索引

| # | 结论 | 证据（file:line） |
| --- | --- | --- |
| 1 | AgentUI 6 控件均有意图事件、不写回业务状态 | `src/Agent/Arc.Avalonia.AgentUI/Controls/ArcAgentTranscript.cs:197`、`ArcRunControlBar.cs:112-128`、`ArcToolApprovalSheet.cs:238-244`、`ArcAgentAskCard.cs:160-163`、`ArcAgentAttentionInbox.cs:152-155`、`ArcBackgroundRunDock.cs:86-92` |
| 2 | transcript 每次属性变化全量重建、无增量 API、未接虚拟化 | `ArcAgentTranscript.cs:126-160,199-290`；`docs/07:120,124` |
| 3 | 审批单只展示、倒计时仅展示、无队列 | `ArcToolApprovalSheet.cs:95-99,141,676` |
| 4 | 全部 AgentUI 属性的数据形态是 `IEnumerable` 快照 | `ArcAgentTranscript.cs:126`、`ArcAgentAttentionInbox.cs:105`、`ArcToolApprovalSheet.cs:116` |
| 5 | 唯一 `INotifyCollectionChanged` 支持在 L1 `ArcVirtualList` | `src/Foundation/Arc.Avalonia/Controls/ArcVirtualList.cs:40,53,158-175` |
| 6 | AIWidgets 自驱演示（status 自改、tool-call 自改+定时器） | `ArcAgentStatus.cs:457-534,616`；`ArcToolCall.cs:616,637-678` |
| 7 | AIWidgets/Explainers 的公开事件几乎为 0 | `ArcBlackHole.cs:219-221` 是 AIWidgets 唯一事件；AIExplainers 0 |
| 8 | Blocks.AI 无可复用控件，仅 internal kit | `src/Blocks/Arc.Avalonia.Blocks.AI/Controls/AiKit.cs:26`；18 个 `Pages/*.cs` 均 `: ArcItemPage`（如 `AiChatPage.cs:22`） |
| 9 | Blocks.AI 页面是模拟/样例 | `AgentRunPage.cs:18,62`；`AiChatPage.cs:62-65` |
| 10 | ChatThread 无流式/增量、整树重建、typing 定时器 | `ArcChatThread.cs:82,96,135-139,235,453,467,799` |
| 11 | AgentUI/AIWidgets 只依赖 Foundation（绕过 L2） | `Arc.Avalonia.AgentUI.csproj`（仅 Foundation+Demo）、`Special.AIWidgets.csproj`（仅 Foundation+Demo）；AgentUI usings 仅 Controls/Theme，AIWidgets 另用 Motion |
| 12 | Blocks.AI 正确组合 L2 | `src/Blocks/Arc.Avalonia.Blocks.AI/Arc.Avalonia.Blocks.AI.csproj`（DataDisplay/Navigation/Overlays/Inputs/Feedback/Charts/Internal） |
| 13 | 四层能力模型与 L5 边界 | `docs/06:202-211` |
| 14 | 扩展方案：57 项 / 235 人日 / Agent 28 项 | `docs/06:365-376,384-406`；`docs/arc-extension-catalog.json` summary |
| 15 | 宿主边界：只展示+事件、无副作用 | `docs/06:342-358,713-749,752-765,769-807` |
| 16 | 跨域依赖方向（life → agent） | `docs/06:511-529` |
| 17 | P0 已实现 14 控件 / 14 页，页面用 `ArcPage` 不占目录 | `docs/07:11-25,30-57` |
| 18 | P0 自认限制：transcript 未虚拟化、主题快照 | `docs/07:116-125` |
| 19 | 05 复核结论：纯 UI 范围可交付、P0 0 | `docs/agent-life-tools/05-ui-scope-review.zh.md:17,133-175` |
| 20 | 03-agent-gaps 的「40 资产 / 0 事件」判断（对 AIWidgets/Blocks 仍成立，对 AgentUI 已过时） | `docs/agent-life-tools/03-agent-gaps.zh.md:16,24,34-38,60` |
| 21 | 29 个工程 | `ArcUI.slnx` |
| 22 | 294 目录 / 294 Docs / 294 item page / 315 页 | `docs/arc-catalog.json`；`src/**/Docs/*.json`（294）；`README.md:8,208-209,232-233` |
| 23 | 页面发现与文档加载是约定式（无需注册） | `src/Apps/Arc.Avalonia.Demo/ArcPageRegistry.cs:21-45`；`Docs/ArcDocStore.cs:25-27,97-113` |
| 24 | audit/smoke/shots 机制与数字 | `src/Apps/Arc.Avalonia.Gallery/Framework/CatalogAudit.cs:18-40`；`NavigationSmoke.cs:29-49`；`ShotRunner.cs:23,47-49` |
| 25 | App.axaml 主题入口 | `src/Apps/Arc.Avalonia.Gallery/App.axaml:8-32`（AgentUI `:21`、LifeToday `:22`） |
| 26 | catalog 两份同源同哈希 | `docs/arc-catalog.json` 与 `src/Apps/Arc.Avalonia.Demo/Catalog/arc-catalog.json` SHA256 `8EBA6084…` |
| 27 | 文档漂移：07 文件索引/08 归属/extension project/status/24 项目 | `docs/07:143-154`；`docs/08:350-353`；`docs/06:826`；`docs/arc-extension-catalog.json` items |
| 28 | AgentUI/LifeToday csproj 已加 `EmbeddedResource Docs\*.json` 但无 Docs 目录（工作树状态） | `src/Agent/Arc.Avalonia.AgentUI/Arc.Avalonia.AgentUI.csproj`、`...LifeToday.csproj`（未提交改动） |

---

## 附录 A · 统计方法与命令（只读）

| 统计 | 方法 |
| --- | --- |
| 文件/LOC | `Get-ChildItem <dir>/*.cs`；`(Get-Content $f).Count` 求和 |
| 控件/事件 | `Select-String` 匹配 `public (sealed )?class … : Panel/Control/TemplatedControl/Decorator/ListBox/ContentControl/ArcExplainer`；`public event` 计数 |
| 页面 | `class \w+Page : ArcPage` / `: ArcItemPage` 计数 |
| Docs | `src/**/Docs/*.json` 文件计数；extension catalog 用 `ConvertFrom-Json` 分组求和（S=1/M=3/L=8） |
| catalog 分组 | `docs/arc-catalog.json` 的 `items/specialItems/blockItems` 与 `specialGroups/blockGroups` |
| 哈希 | `Get-FileHash -Algorithm SHA256` |

> 本审计**未运行** `dotnet build`、`dotnet run`、`tools/build.ps1`、`tools/shots.ps1`；所有「构建/审计/截图通过」类结论均引自既有 `docs/07` 报告，不由本审计重新验证。

## 附录 B · 外部对标来源

| 来源 | 用途 | URL |
| --- | --- | --- |
| AG-UI 规范 · Schema | 事件族（TextMessage/ToolCall/State/Reasoning/Subagent/Activity） | https://docs.ag-ui.com/spec/1.0/schema |
| AG-UI · Events | 事件语义与生命周期 | https://docs.ag-ui.com/concepts/events |
| Vercel AI SDK UI · Stream Protocols | 前端状态 + 流式协议分层 | https://ai-sdk.dev/docs/ai-sdk-ui/stream-protocol |
| Vercel AI SDK UI · Transport | transport 可替换 | https://ai-sdk.dev/docs/ai-sdk-ui/transport |
| assistant-ui · AG-UI Runtime | `ExternalStoreRuntime` 承担事件解析与消息重建 | https://www.assistant-ui.com/docs/runtimes/ag-ui/overview.md |
| CopilotKit · Architecture | frontend / runtime / agent 三层 + AG-UI wire format | https://docs.copilotkit.ai/concepts/architecture |

> 抓取时间：2026-10-31（Asia/Shanghai）。外部内容仅作对标参考，不代表本项目规范；如离线复现，结论仍可由 §3.3 的源码事实与 06 §7 原则独立支撑。

## 附录 C · 本审计不做的事

1. 不实现任何 API 草案（§4.3 仅为签名级设计）；
2. 不修改任何源码/csproj/其它文档（唯一写入为本文件）；
3. 不运行构建/截图/smoke（由 Lead 统一验证）；
4. 不评估宿主应用（模型、调度、MCP、语音）的选型；
5. 不重估 06 的 57 项 UI 人日，只给出「顶层化新增」与「与 06 重叠」的拆分口径。
