# P0 UI 组件实现报告

> 实现依据：[`06-agent-life-tools-extension.zh.md`](06-agent-life-tools-extension.zh.md) 的 **P0 14 项**（纯 UI；Agent 逻辑/数据/运行时由宿主应用负责）。
> 实现范围：14 个 P0 UI 组件的**可运行代码**（C# 组合控件 + Gallery 展示页），不含 P1/P2 的 43 项、不含任何运行时/数据层。
> 数据基线：2026-10-28，`Arc.Avalonia` 工作区；542 个 `.cs` 源文件、112 个 `.axaml`（本次新增 26 个 `.cs`）。

## 1. 结果摘要

| 指标 | 结果 |
| --- | --- |
| P0 UI 组件 | **14 / 14 已实现**（6 Agent UI + 5 Today/生活 + 3 Foundation/Input） |
| 新增源文件 | **26 个**（1 个共享 `ArcUi` + 11 个其余控件文件，共 12 个控件文件、14 个控件类，其中 `LifeTodayKit.cs` 含 4 个控件 + 14 个 Gallery 页面文件） |
| 新增 Gallery 页面 | **14 页**（Group：`P0 · Agent UI` / `P0 · Today` / `P0 · Foundation`） |
| 全量构建 | **0 error**；9 个既有 warning（全部在既有文件，新增 26 个文件 0 warning） |
| 目录审计 | `pages=233 items=294 itemPages=166 matchedBySection=88 matchedByAlias=40 missing=0 pageFailures=0`（294 项基线未受影响） |
| 导航 smoke | `clicks=942 pageShows=699 itemShows=588 failures=0`（退出码 0） |
| 明暗截图 | 新增页各明暗 1 张；全库实时截图 233×2 = **466 张** |
| 纯 UI 边界 | 新代码 0 处 `HttpClient`/文件写入/托盘/系统通知/数据持久化；仅审批单内一个**展示用**倒计时 `DispatcherTimer`（随视觉树 attach/detach 启停） |

## 2. 共同实现约定

1. **组合控件**：P0 控件均为 C# 组合控件（`Panel` / `ListBox` 子类），不新增 XAML `ControlTheme`，因此**不修改任何项目主题入口**，也不会与既有 294 项的主题冲突。
2. **共享辅助**：新增 `src/Arc.Avalonia/Controls/ArcUi.cs`（Lead 提供），统一 `Brush/Text/Stack/HStack/Wrap/Icon/Surface/Divider/Badge/Button/Pill/Dot/Name`；全部颜色/圆角/阴影/字体走 `ArcTokens` / `ArcResources`，无硬编码色值。
3. **纯 UI 契约**：控件只接收展示数据、渲染状态、发出意图事件（如 `StopRequested`、`ApproveRequested`、`CompleteRequested`、`SaveRequested`）；不读写数据、不发起网络/模型调用、不注册系统能力。
4. **页面**：均继承 `ArcPage`（非 `ArcItemPage`，不占用 294 项目录），Group 分别为 `P0 · Agent UI` / `P0 · Today` / `P0 · Foundation`，Order 20001–20023；每页用 `DemoUi.Section` 组织并展示 2–3 个状态（多数页面用按钮切换；`Reminder row` 为三行并列示例，`Virtualized list` 通过滚动/追加按钮展示滚动状态），页面高度 526–1311 px（均 ≤1400）。
5. **无障碍**：控件通过 `ArcUi.Name` 设置 Automation 名称；交互走 `ArcButton`（Tab/Enter/Space 可达）；行级操作提供键盘路径与可见焦点。

## 3. 组件清单

### 3.1 Agent UI（6 项，Group `P0 · Agent UI`）

| P0 slug | 控件 | 文件（项目） | 页面 / Order | 关键状态 | 意图事件 |
| --- | --- | --- | --- | --- | --- |
| `agent-transcript` | `ArcAgentTranscript` | `Arc.Avalonia.Content/Controls/ArcAgentTranscript.cs` | Agent transcript / 20001 | Idle / Running / AwaitingInput / Done / Failed / Stopped；事件级 Pending/Running/Done/Failed/Skipped；可展开详情 | `RetryRequested(event)` |
| `run-control-bar` | `ArcRunControlBar` | `Arc.Avalonia.SpecialAIWidgets/Controls/ArcRunControlBar.cs` | Run control bar / 20002 | Running / Paused / Stopped / TakenOver；当前步骤/耗时 | `StopRequested`、`StopAfterStepRequested`、`PauseRequested`、`ResumeRequested`、`SteerRequested(string)`、`TakeoverRequested` |
| `tool-approval-sheet` | `ArcToolApprovalSheet` | `.../Controls/ArcToolApprovalSheet.cs` | Tool approval sheet / 20003 | Checkout / Editing / Submitting / Decided / Expired / Cancelled；Risk Low/Medium/High；Scope Once/Task/Hour/Always | `ApproveRequested(decision)`、`DenyRequested(reason)`、`DelegateRequested` |
| `agent-ask-card` | `ArcAgentAskCard` | `.../Controls/ArcAgentAskCard.cs` | Agent question card / 20004 | Question / Answered / Skipped / Deferred / Expired；单选/多选/自由文本 | `Submitted(answer)`、`DeferredRequested` |
| `background-run-dock` | `ArcBackgroundRunDock` | `.../Controls/ArcBackgroundRunDock.cs` | Background run dock / 20006 | Idle / Running / NeedsAttention / Paused；进度线 | `PauseRequested`、`StopRequested`、`ExpandRequested` |
| `agent-attention-inbox` | `ArcAgentAttentionInbox` | `Arc.Avalonia.BlocksAI/Controls/ArcAgentAttentionInbox.cs` | Agent attention inbox / 20005 | 分组（Approval/Ask/Failure/Routine/Suggestion）+ New/Seen/InProgress/Resolved/Expired；空态 | `ItemActionRequested(id)`、`Dismissed(id)` |

### 3.2 Today / 生活域（5 项，Group `P0 · Today`）

| P0 slug | 控件 | 文件（项目） | 页面 / Order | 关键状态 | 意图事件 |
| --- | --- | --- | --- | --- | --- |
| `reminder-row` | `ArcReminderRow` | `Arc.Avalonia.Feedback/Controls/ArcReminderRow.cs` | Reminder row / 20013 | Upcoming / Due / Overdue / Snoozed / Done / Missed；重复/来源；展开动作 | `CompletedChanged(bool)`、`SnoozeRequested(TimeSpan)`、`RescheduleRequested`、`SkipRequested` |
| `daily-brief` | `ArcDailyBrief` | `Arc.Avalonia.BlocksApp/Controls/LifeTodayKit.cs` | Daily brief / 20011 | Loading / Ready / PartiallyLoading / Error；建议 Pending/Accepted/Dismissed/Snoozed | `AcceptSuggestionRequested`、`IgnoreSuggestionRequested`、`SnoozeSuggestionRequested`、`OpenAgendaRequested`、`OpenRemindersRequested`、`RetryRequested` |
| `day-agenda` | `ArcDayAgenda` | `.../LifeTodayKit.cs` | Day agenda / 20012 | Loading / Empty / Populated / Conflict / Error；全天条 + 小时网格 + Now 线 | `PreviousDayRequested`、`NextDayRequested`、`TodayRequested`、`ItemActivated(id)`、`AddRequested`、`RetryRequested` |
| `reminder-center` | `ArcReminderCenter` | `.../LifeTodayKit.cs` | Reminder center / 20014 | Normal / Empty / Loading / Error；分组计数 + 过滤 + 跨项目复用 `ArcReminderRow` | `CompleteRequested`、`SnoozeRequested`、`RescheduleRequested`、`SkipRequested`、`SnoozeAllRequested`、`AddRequested`、`RetryRequested` |
| `medication-schedule` | `ArcMedicationSchedule` | `.../LifeTodayKit.cs` | Medication schedule / 20015 | Normal / Empty / Paused；Pending/Taken/Missed/Snoozed；库存/续方 | `TakeRequested`、`SnoozeRequested`、`SkipRequested`、`UndoTakenRequested`、`RefillRequested`、`PauseRequested`、`ResumeRequested`、`EditPlanRequested`、`AddRequested` |

### 3.3 Foundation / Input（3 项，Group `P0 · Foundation`）

| P0 slug | 控件 | 文件（项目） | 页面 / Order | 关键能力 |
| --- | --- | --- | --- | --- |
| `virtualized-list` | `ArcVirtualList : ListBox` | `Arc.Avalonia/Controls/ArcVirtualList.cs` | Virtualized list / 20021（Gallery） | `VirtualizingStackPanel` 行级虚拟化；`StickToBottom`、`ScrollState`（AtTop/InMiddle/AtBottom）、`ScrollToEnd()`、`ScrollToIndex(int)`、集合变化粘底 |
| `quick-capture-bar` | `ArcQuickCaptureBar` | `Arc.Avalonia.AdvancedInputs/Controls/ArcQuickCaptureBar.cs` | Quick capture bar / 20022 | 状态 Idle/Parsing/Parsed/Ambiguous/Error；`Text`、`Chips`（Kind：Time/Person/Amount/List/Note）、`Hint`；Enter 提交 / Esc 取消 / chip 行内删除 |
| `routine-schedule-editor` | `ArcRoutineScheduleEditor` | `Arc.Avalonia.Pickers/Controls/ArcRoutineScheduleEditor.cs` | Routine schedule editor / 20023 | Repeat（Daily/Weekdays/Weekly/Monthly/Custom）、周几、时间/时区、Misfire（RunOnce/Skip/Ask）、Overlap（Skip/Queue/Allow）、next-run 预览、`CreateSpec()`；只产出展示契约 |

## 4. 验证

### 4.1 构建与审计

```text
pwsh -NoProfile tools/build.ps1 -NoRestore        # 全量解决方案（增量）
dotnet build ArcUI.slnx --nologo -m:1 -v:q -nodeReuse:false -t:Rebuild --no-restore   # 全量重建（验证命令）
Build succeeded. 9 Warning(s) / 0 Error(s)
（9 个 warning 全部位于既有文件：ArcMorphSelect、WorkspaceSidebarPage、HelpCenterPage、
  SupportWidgetPage、ArcCandleChart、CatalogAudit、GalleryWindow、ShotRunner；
  本次新增的 26 个文件 0 warning、0 error）

dotnet src/Arc.Avalonia.Gallery/bin/Debug/net10.0/Arc.Avalonia.Gallery.dll --audit
CATALOG AUDIT: pages=233 items=294 itemPages=166 matchedBySection=88 matchedByAlias=40 missing=0 pageFailures=0
```

- 新增 14 页使 gallery 页面数 219 → 233；294 项目录的审计结果保持 `missing=0 / pageFailures=0`。
- 导航 smoke 结果见 §4.3。

### 4.2 截图

每个新增页在 light/dark 各渲染一张，输出 `shots/light|dark/200xx-<slug>.png`；页面高度实测：

| 页面 | 高度(px) |
| --- | --- |
| Agent transcript | 918 |
| Run control bar | 526 |
| Tool approval sheet | 1242 |
| Agent question card | 828 |
| Agent attention inbox | 1224 |
| Background run dock | 808 |
| Daily brief | 905 |
| Day agenda | 1257 |
| Reminder row | 761 |
| Reminder center | 1242 |
| Medication schedule | 1190 |
| Virtualized list | 1199 |
| Quick capture bar | 893 |
| Routine schedule editor | 1311 |

全部 ≤1400 px，无 `SHOT WARNING`。

### 4.3 导航 smoke

```text
dotnet src/Arc.Avalonia.Gallery/bin/Debug/net10.0/Arc.Avalonia.Gallery.dll --smoke
GALLERY SMOKE: clicks=942 pageShows=699 itemShows=588 failures=0
exit code: 0
```

smoke 会遍历全部 233 个页面（含 14 个新页）并反复切换，验证页面内容可重复挂载、无 reparent 回归；`failures=0` 说明新增页面没有破坏导航。

## 5. 宿主边界（与 06 报告 §7 一致）

- 组件只做展示与意图事件：数据读写、Agent 执行、调度、系统通知、托盘/热键、权限、模型/MCP 调用、持久化、语音管线均由宿主应用负责。
- `ArcVirtualList` 只虚拟化宿主提供的 `ItemsSource`；`ArcQuickCaptureBar` 不解析文本；`ArcRoutineScheduleEditor` 不计算时区/DST、不调度；`ArcToolApprovalSheet` 的倒计时只做展示，过期强制由宿主执行。

## 6. 已知限制

| 项 | 限制 | 后续 |
| --- | --- | --- |
| `agent-transcript` | 未接 `ArcVirtualList`，长会话仍是全量重建 | P1 接入虚拟化（`ArcVirtualList` 已就绪） |
| `ArcVirtualList` | 未内建 Loading/Empty/Error 模板（本轮不加主题文件），行高沿用 Fluent `ListBoxItem` | 后续加 Arc 主题容器 |
| `ArcQuickCaptureBar` | 不解析、不落库、不注册热键；chips 由宿主提供 | 宿主接入解析服务 |
| `ArcToolApprovalSheet` | 倒计时只展示；参数 diff 为文本预览，非通用 diff 控件 | P1 `diff-review` |
| 组合控件主题 | 颜色在构建时取 token 快照，运行时换主题需宿主重建页面（与 `ArcUi` 现状一致） | 后续统一 `DynamicResource` 化 |
| 状态覆盖 | 页面每页展示 3 个状态；控件枚举含全部状态 | P1 补状态矩阵截图 |

## 7. 未实现（按 06 报告，不在本次 P0 范围）

- **P1 34 项**：权限范围选择、diff、接管、护栏、预算、语音条、检查点、记忆周检、审计、连接器、离线队列、信任规则、桌面设置，以及生活域 16 项组件/block 与参考带图表。
- **P2 9 项**：语音确认、全局热键字段、trace、模型路由、运行历史、家务板、能耗、包裹、宠物。
- 上述项保持 `arc-extension-catalog.json` 中的待实现状态；本次不新增运行时服务。

## 8. 文件索引

```text
src/Arc.Avalonia/Controls/ArcUi.cs                         （共享组合辅助，新增）
src/Arc.Avalonia/Controls/ArcVirtualList.cs
src/Arc.Avalonia.Gallery/Pages/VirtualListDemoPage.cs
src/Arc.Avalonia.AdvancedInputs/Controls/ArcQuickCaptureBar.cs
src/Arc.Avalonia.AdvancedInputs/Pages/QuickCaptureBarPage.cs
src/Arc.Avalonia.Pickers/Controls/ArcRoutineScheduleEditor.cs
src/Arc.Avalonia.Pickers/Pages/RoutineScheduleEditorPage.cs
src/Arc.Avalonia.Content/Controls/ArcAgentTranscript.cs
src/Arc.Avalonia.Content/Pages/AgentTranscriptPage.cs
src/Arc.Avalonia.SpecialAIWidgets/Controls/ArcRunControlBar.cs
src/Arc.Avalonia.SpecialAIWidgets/Controls/ArcToolApprovalSheet.cs
src/Arc.Avalonia.SpecialAIWidgets/Controls/ArcAgentAskCard.cs
src/Arc.Avalonia.SpecialAIWidgets/Controls/ArcBackgroundRunDock.cs
src/Arc.Avalonia.SpecialAIWidgets/Pages/RunControlBarPage.cs
src/Arc.Avalonia.SpecialAIWidgets/Pages/ToolApprovalSheetPage.cs
src/Arc.Avalonia.SpecialAIWidgets/Pages/AgentAskCardPage.cs
src/Arc.Avalonia.SpecialAIWidgets/Pages/BackgroundRunDockPage.cs
src/Arc.Avalonia.BlocksAI/Controls/ArcAgentAttentionInbox.cs
src/Arc.Avalonia.BlocksAI/Pages/AgentAttentionInboxPage.cs
src/Arc.Avalonia.Feedback/Controls/ArcReminderRow.cs
src/Arc.Avalonia.Feedback/Pages/ReminderRowPage.cs
src/Arc.Avalonia.BlocksApp/Controls/LifeTodayKit.cs
src/Arc.Avalonia.BlocksApp/Pages/DailyBriefPage.cs
src/Arc.Avalonia.BlocksApp/Pages/DayAgendaPage.cs
src/Arc.Avalonia.BlocksApp/Pages/ReminderCenterPage.cs
src/Arc.Avalonia.BlocksApp/Pages/MedicationSchedulePage.cs
```
