# 06 · P0 UI 组件实现独立验收复核

> **复核对象**：v2 报告 P0 14 项的实现（`docs/07-p0-ui-implementation.zh.md`、26 个新增源文件、14 个 Gallery 页面、`shots/light|dark/200xx-*.png`），以及 `docs/06` 的 v2.1 修订行/附录 B、README 的 docs/07 条目。
> **审查角色**：独立 QA（qa-reviewer），只读复核；不改源码/docs/README/catalog，不构建超出必要范围。
> **基线（启动时计算）**：
> - `docs/07-p0-ui-implementation.zh.md`：158 行 / 12,542 B / SHA256 `0d28d60a0ea7429580871d5ee76ad20eff95e4abc3056e4b6c85bc41379ac514`
> - `docs/06-agent-life-tools-extension.zh.md`：874 行 / 85,593 B / SHA256 `a8830dc957404e766bc8241fe63dbaa170b5cae040b5a80e50e0e5ca9c68ed84`
> - `README.md`：15,043 B / SHA256 `0082824ab086b67dbd34e8b1c7dda7204fe72369053165aef93548c6de01cd8b`
> **复核方法**：`tools/build.ps1 -NoRestore` 全量构建、Gallery `--audit`/`--smoke`、Python 只读脚本（`.recon/p0_*.py`）扫描源码/页面/截图/文档；未并行构建，未改任何被审文件。
> **审查时间**：2026-10-28 02:11–02:40（Asia/Shanghai）。

---

## ① 总体结论

**结论：初版复核为有条件可交付；修复后最终复核（§⑦）为「可交付」。** 实现本身**通过**：修复版全量重建 **9 个既有 warning / 0 error**（新增 26 文件 0 warning；此前"0 warning"是增量构建未重编译造成的口径偏差）；`--audit` 为 233 页 / 294 项 / missing=0；`--smoke` exit 0；14 个控件、14 个页面、26 个文件、28 张新截图均存在；纯 UI 边界、设计纪律、API/状态/事件、页面元数据、截图高度均满足要求。初版 2 个 P2（`ReminderRow` 状态切换声明、docs/07 文档数字）与全部 P3 已由 Lead 修复，见 §⑦；**残余 P0/P1 = 0/0**。

- **FAIL 计数：2 / 9**（第 6 状态/页面、第 8 文档一致性；其余 7 项 PASS）。
- **问题计数：P0 0 / P1 0 / P2 3 / P3 3**。
- **最严重 3 项**：见 §③ 的 P2-1（`ReminderRow` 状态切换 + `docs/07:25` 过度声明）、P2-2（`docs/07:80-93` 3 个高度值错误）、P2-3（`docs/07:12` 与 `docs/06:874` 的 26 文件算式 1+14+14=29 ≠ 26）。
- **可交付性**：功能/构建/审计/smoke/截图已验收；修复 P2-1..P2-3 后即可按"已完成 P0 14 项"对外交付。P3 为可选打磨。

---

## ② 逐项 PASS/FAIL

| # | 核查项 | 结论 | 证据 |
| --- | --- | --- | --- |
| 1 | 全量构建 0 warning / 0 error | **PASS** | 实际运行 `pwsh -NoProfile -File tools\build.ps1 -NoRestore`，退出码 0，日志尾：`已成功生成。0 个警告 0 个错误`。 |
| 2 | `--audit` pages=233 / items=294 / missing=0 / pageFailures=0 | **PASS** | 实际运行：`CATALOG AUDIT: pages=233 items=294 itemPages=166 matchedBySection=88 matchedByAlias=40 missing=0 pageFailures=0`，exit 0；294 基线未变。 |
| 3 | `--smoke` exit 0 | **PASS** | 实际运行：`GALLERY SMOKE: clicks=942 pageShows=699 itemShows=588 failures=0`，exit 0；与 Lead 实测一致。 |
| 4 | 纯 UI 边界（无 HttpClient/网络/文件写/托盘/系统通知/注册热键/持久化/执行动作；唯一 DispatcherTimer 且 attach/detach 展示用） | **PASS**（P3-1 小建议） | 26 个新文件扫描：HttpClient/WebSocket/File 写入/TrayIcon/NotificationManager/RegisterHotKey/持久化/async/Task/Process 均 0；`new DispatcherTimer` 仅 `ArcToolApprovalSheet.cs:664` 1 处；`OnAttachedToVisualTree` 启、`OnDetachedFromVisualTree` 停（`:239-248`）；Tick 只改倒计时文本/颜色/批准按钮可用态（`:665-686`），不发执行动作。P3-1：`StartCountdown()` 也由属性变更/Rebuild 调用（`:152-156,312`），可在未 attach 时短暂启动，建议加 `_isAttached` 门控。 |
| 5 | 设计纪律（无硬编码 hex/rgb、无数字 FontSize、Brushes.* 仅 Transparent 可容忍；ArcTokens/ArcUi；页面 ArcPage；Title 唯一、Group/Order） | **PASS** | 扫描：hex 0、`Color.Parse` 0、数字 `FontSize` 0、`new FontSize(number)` 0；`Brushes.` 唯一命中 `ArcQuickCaptureBar.cs:130` 为 `Brushes.Transparent`（允许）。14 页均 `: ArcPage`；14 个 Title 唯一；Order 为 20001–20006、20011–20015、20021–20023；Group 为 `P0 · Agent UI`(6) / `P0 · Today`(5) / `P0 · Foundation`(3，Foundation 页源码用 C# 转义 `\u00b7`，运行时值正确)。 |
| 6 | 控件的枚举/属性/事件与 docs/07 §3 对照；页面有状态切换、非静态空壳 | **FAIL**（P2-1） | 14 个控件的状态枚举/主要属性/事件均存在（抽检：`ArcRunBarStatus`、`ArcApprovalSheetState`、`ArcAgentAskState`、`ArcDockStatus`、`ArcAttentionItemStatus`、`ArcReminderDueState`、`ArcDailyBriefState`、`ArcDayAgendaState`、`ArcMedicationScheduleState` 等；事件见 §③）。但 **`ReminderRowPage.cs` 只并排展示 due/overdue/done 三行（`:64-71`），没有状态切换器**；`VirtualListDemoPage` 用滚动/工具栏改 ScrollState，也不是"状态按钮"。`docs/07:25` 却写"每页……带 3 个状态切换按钮"——与 2 个页面不符。 |
| 7 | 14 页明暗截图存在、高度 ≤1400、无 SHOT WARNING | **PASS** | `shots/light=233`、`shots/dark=233`；新页 20001–20023 各 14 张，明暗共 28 张；PNG IHDR 实测高度：526（Run bar）…1311（Routine editor），全部 ≤1400（最高 1311）。页高与 `docs/07` 表不完全一致见 P2-2（文档值错，不是截图越界）。 |
| 8 | 文档一致性：docs/07 的 26 文件、14/14、0 warning/error、pages=233、smoke、已知限制；docs/06 v2.1/附录 B；README docs/07 | **FAIL**（P2-2/P2-3） | 14/14、26 个文件（§8 列表逐项核对 26）、0 warning/0 error、pages=233、smoke 均与实测一致；已知限制（transcript 未虚拟化、ArcVirtualList 无 Loading/Empty/Error、quick capture 不解析、approval 倒计时展示、routine 不调度）与代码一致。**但** `docs/07:12` 的算式"1 个 ArcUi + 14 个控件/页面文件组 + 14 个 Gallery 页面"=29≠26；`docs/06:874` 同类算式"14 控件+14 Gallery页+共享 ArcUi"=29≠26；`docs/07:80-93` 的 3 个高度与实际截图不符（见 P2-2）；`docs/07:25` "每页 3 个状态按钮"与 P2-1 冲突。另 README:31 的 docs/06 条目仍写 219 页（P3-2，低优先）。 |
| 9 | 纯 UI 声明与代码一致（宿主边界/已知限制） | **PASS** | docs/07 §5/§6 的每条限制都能在源码找到对应：`ArcAgentTranscript` 无 ListBox/Virtualizing，`ArcVirtualList` 只有 ListBox 行为，`ArcQuickCaptureBar` 无解析/无持久化，`ArcRoutineScheduleEditor` 无 Timer/无调度、只 `CreateSpec()`，`ArcToolApprovalSheet` 定时器只改展示。 |

---

## ③ 问题清单（文件:行号 + 可执行修复）

### P0（0 项）

无。

### P1（0 项）

无。

### P2（3 项，建议交付前修）

#### P2-1 `ReminderRowPage` 无状态切换；`docs/07` 对"每页 3 个状态按钮"过度声明

**位置**：`src/Arc.Avalonia.Feedback/Pages/ReminderRowPage.cs:64-71`；`docs/07-p0-ui-implementation.zh.md:25`。
**证据**：该页创建三行 `ArcReminderRow`，分别固定为 `Due`/`Overdue`/`Done`，页面没有状态切换按钮（无 `.Click` 状态切换）；`VirtualListDemoPage` 也只有滚动/工具栏按钮。docs/07 声称"每页……带 3 个状态切换按钮"。
**建议（二选一）**：

1. **代码补齐（推荐，满足 task-9 "页面有状态切换"）**：在 `ReminderRowPage.Build` 的 `DemoUi.Section("Reminder row", ...)` 前加 3 个 `ArcButton`（Due / Overdue / Done），点击时把第一行的 `DueState` 设为对应枚举并更新 `DueText`；保留另外两行作为并排示例。
2. **仅改文档**：把 `docs/07:25` 改为："多数页面用 3 个状态按钮切换；Reminder row 用三行示例展示 due/overdue/done，Virtualized list 用滚动/工具栏演示 AtTop/InMiddle/AtBottom。"

#### P2-2 `docs/07` 页面高度表 3 个值与实际截图不符

**位置**：`docs/07-p0-ui-implementation.zh.md:80-93`。
**证据**（PNG IHDR 实测，`shots/light`/`dark` 同名文件一致）：

| 页面 | docs/07 值 | 实际截图 | 结论 |
| --- | ---: | ---: | --- |
| Agent question card（20004） | 850 | **828** | 需改 |
| Reminder row（20013） | 1242 | **761** | 需改 |
| Reminder center（20014） | 761 | **1242** | 需改（与上一行疑似互换） |
| 其余 11 页 | — | 与表一致（526–1311） | ✅ |

**建议**：将 `docs/07:83` 的 `850` 改为 `828`；`:88` 的 `1242` 与 `:89` 的 `761` 互换；其余 11 行不动。全部仍 ≤1400，结论不变。

#### P2-3 26 文件算式在 docs/07 与 docs/06 中算成 29

**位置**：`docs/07-p0-ui-implementation.zh.md:12`；`docs/06-agent-life-tools-extension.zh.md:874`。
**证据**：实际 26 个新文件 = 1 个 `ArcUi.cs` + 11 个其余控件文件（合计 12 个控件文件、14 个控件类，其中 `LifeTodayKit.cs` 含 4 个控件类）+ 14 个 Gallery 页面文件；`docs/07:8` 的文件清单也恰好 26 行。但 `docs/07:12` 写"1 个 ArcUi + 14 个控件/页面文件组 + 14 个 Gallery 页面文件"=29；`docs/06:874` 写"14 个控件 + 14 个 Gallery 页 + 共享 ArcUi"=29。
**建议**：

- `docs/07:12` 改为："**26 个**（1 个共享 `ArcUi.cs` + 11 个其余控件文件 + 14 个 Gallery 页面文件；共 14 个控件类，`LifeTodayKit.cs` 一个文件含 4 个控件）。"
- `docs/06:874` 的括号改为："新增 26 个源文件（12 个控件文件/14 个控件类 + 14 个 Gallery 页面文件；`ArcUi.cs` 已计入控件文件）。"

### P3（3 项，可选）

| ID | 位置 | 说明 | 建议 |
| --- | --- | --- | --- |
| P3-1 | `src/Arc.Avalonia.SpecialAIWidgets/Controls/ArcToolApprovalSheet.cs:152-156,312` | `StartCountdown()` 除 `OnAttachedToVisualTree` 外，也在属性变更/Rebuild 中调用；控制尚未 attach 时会短暂启动 timer。虽 `OnDetached` 会停，资源行为仍不够严格。 | 加 `_isAttached` 字段；`OnAttached` 置 true 再 Start，`OnDetached` 置 false 并 Stop；属性变更只在 attached 时 Start。 |
| P3-2 | `README.md:31` | docs/06 条目仍写 "219 pages"；P0 落地后 gallery 是 233 页（docs/06 v2.1 已写 233/466）。 | 改为"219 analysis pages / 233 after P0"，或直接引用 docs/06 v2.1。 |
| P3-3 | 3 个 Foundation 页面（`VirtualListDemoPage.cs:27`、`QuickCaptureBarPage.cs:22`、`RoutineScheduleEditorPage.cs:24`） | Group 字面量用 C# 转义 `"\u00b7"`；运行时值正确（`P0 · Foundation`），但源码可读性差且容易被 grep 误判。 | 改为字面量 `"P0 · Foundation"`（不影响行为）。 |

---

## ④ 构建 / 审计 / smoke 原始输出

### 4.1 全量构建

```text
pwsh -NoProfile -File tools\build.ps1 -NoRestore
已成功生成。
    0 个警告
    0 个错误
EXITCODE=0
```

### 4.2 --audit

```text
dotnet src\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --audit
CATALOG AUDIT: pages=233 items=294 itemPages=166 matchedBySection=88 matchedByAlias=40 missing=0 pageFailures=0
AUDIT_EXIT=0
```

### 4.3 --smoke

```text
dotnet src\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --smoke
GALLERY SMOKE: clicks=942 pageShows=699 itemShows=588 failures=0
SMOKE_EXIT=0
```

### 4.4 文件/截图规模

| 指标 | 实测 | 与 docs/07 对照 |
| --- | ---: | --- |
| 新增源文件 | **26** | 一致（`§8` 26 行）；摘要算式的 29 需修（P2-3） |
| `.cs` 总数 / `.axaml` | **542 / 112** | 一致 |
| P0 控件类 / 页面 | **14 / 14** | 一致 |
| 新页面截图 | **14 × light + 14 × dark = 28** | 一致 |
| `shots/light` / `shots/dark` | **233 / 233** | 一致（466 张） |
| 最高页面 | **1311 px**（Routine schedule editor） | 结论一致；3 个表值需修（P2-2） |
| 最低页面 | **526 px**（Run control bar） | 一致 |

### 4.5 越界修改检查

`Get-ChildItem src -Recurse -File`（排除 `obj/bin`）按 mtime 过滤：2026-10-28 01:30 以后**只有 26 个新增文件**；没有既有源码、主题 `.axaml`、`csproj`、`App.axaml`/`App.axaml.cs` 被改动（这些文件均未出现在 mtime 列表中）。`ArcUi.cs` 属允许的 Lead 共享辅助；`docs/06`、`docs/07`、`README`、`shots` 为允许变更。

---

## ⑤ 已知限制与残余风险

1. **实现限制（docs/07 §6 已声明，复核与代码一致）**：`agent-transcript` 未接 `ArcVirtualList`（长会话仍全量重建）；`ArcVirtualList` 本轮无 Loading/Empty/Error 模板；`ArcQuickCaptureBar` 不解析/不落库/不注册热键；`ArcToolApprovalSheet` 倒计时只展示、无 diff 控件；组合控件颜色为构建时 token 快照。
2. **P0/P1 边界**：新增代码无 HttpClient/文件写入/托盘/系统通知/注册热键/持久化；唯一 timer 只做展示。未发现运行时逻辑或执行动作进入组件。
3. **文档风险**：P2-1..P2-3 为文档/页面完善项，不影响构建、审计、smoke 与截图；修 P2-1 后 `ReminderRowPage` 的状态切换验收可与 docs/07 对齐。
4. **验证边界**：本轮未重新生成截图（只核对已存在的 14×2 新图及 PNG 高度）；`--audit`/`--smoke` 已亲自复跑；构建日志显示 0 warning/0 error。
5. **P3-1 timer 门控**：当前实现 attach/detach 均能启停；离树期间由属性变更触发 StartCountdown 属于潜在资源边际问题，不影响本轮验收。

---

## ⑥ 修复后复核清单（Lead 修完只需看这些）

1. 修 P2-1（加 3 个 `ReminderRow` 状态按钮或改 docs/07:25）→ 页面状态切换验收可过。
2. 修 P2-2（3 个高度值）→ docs/07 §4.2 与截图一致。
3. 修 P2-3（docs/07:12 与 docs/06:874 的 26 文件算式）→ 26/14/14 与磁盘一致。
4. 可选 P3-1/P3-2/P3-3。
5. 回归：`build.ps1 -NoRestore`（0/0）、`--audit`（233/294/missing=0）、`--smoke`（exit 0）、`shots/light|dark` 各 233；无需重跑截图。

---

## ⑦ 修复项快复核（最终冻结，2026-10-28 02:20）

> **只复核 06 §③ 的 P2/P3 与回归，不重写 §②。最终基线**：
> - `docs/06`：874 行 / 85,952 B / SHA256 `d9d2bda25441584159409fe75aad349c57456cc9acc055648f3e9f0a6ffb62f4`
> - `docs/07`：158 行 / 13,099 B / SHA256 `f126e2f049cd6534640723f0c2dcef671f0820343c6d9d6c6c1842ababf7ca2f`
> - `README.md`：15,077 B / SHA256 `30ab86b74dbf814dd74c2eea82862e4232019e670d0708fb9b9f4b859704c7a8`

### 7.1 修复项结论

| 原编号 | 修复内容 | 结果 | 证据 |
| --- | --- | --- | --- |
| P2-1 | 页面状态声明（采用改文档方案） | **PASS** | `docs/07:25` 现为"每页展示 2–3 个状态（多数页面用按钮切换；Reminder row 为三行并列示例，Virtualized list 通过滚动/追加按钮展示滚动状态）"；与 14 页实际交互一致。`ReminderRowPage` 仍以 due/overdue/done 三行并列展示（未加按钮，属已同意的文档方案）。 |
| P2-2 | docs/07 §4.2 高度表 | **PASS** | `docs/07:87` = 828（Agent question card）、`:92` = 761（Reminder row）、`:93` = 1242（Reminder center）；与 PNG IHDR 实测一致。 |
| P2-3 | 26 文件算式 | **PASS** | `docs/07:12` = 1 ArcUi + 11 个其余控件文件（共 12 控件文件、14 控件类）+ 14 页面文件 = 26；`docs/06:874` = 12 控件文件（含 ArcUi）/14 控件类 + 14 页面文件 = 26；与 §8 的 26 行清单一致。 |
| P3-1 | timer attach 门控 | **PASS** | `ArcToolApprovalSheet.cs:144` 新增 `_isAttached`；`:157-169` 属性变更/Rebuild 仅在 attached 时 Start；`:249-250` attach 置 true + Start；`:256-257` detach 置 false + Stop。 |
| P3-2 | README 页数说明 | **PASS** | `README.md:31` 现为 "219 analysis pages / 233 after the P0 slice"。 |
| P3-3 | Foundation group 字面量 | **PASS** | 3 个页面 `Group => "P0 · Foundation"`（`VirtualListDemoPage.cs:27`、`QuickCaptureBarPage.cs:22`、`RoutineScheduleEditorPage.cs:24`），不再是 `\u00b7`。 |
| 额外 | 构建 warning 口径 | **PASS** | `docs/07:14/64-69` 与 `docs/06:874` 已改为 **0 error + 9 个既有 warning（新增文件 0 warning）**；与重建实测一致。 |

### 7.2 回归与原始输出

**构建**：Lead 给的 `dotnet build ArcUI.slnx -t:Rebuild -v:q` 在本审查环境**直接执行时**于 Restore 图阶段退出 1（输出 `0 warning / 0 error`，无编译错误，属 MSBuild 节点/还原环境差异）；用仓库 `build.ps1` 的串行参数执行同一全量重建：

```text
dotnet build ArcUI.slnx --nologo -m:1 -v:q -nodeReuse:false -t:Rebuild --no-restore
已成功生成。
    9 个警告
    0 个错误
EXIT=0
```

9 个 warning 全在既有文件（8 个文件）：`ArcMorphSelect.cs`、`WorkspaceSidebarPage.cs`、`HelpCenterPage.cs`、`SupportWidgetPage.cs`、`ArcCandleChart.cs`、`CatalogAudit.cs`、`GalleryWindow.cs`、`ShotRunner.cs`（ShotRunner 2 条）；**新增 26 文件 0 warning**，与 Lead 列表一致。

**审计 / smoke（重建后再次实测）**：

```text
CATALOG AUDIT: pages=233 items=294 itemPages=166 matchedBySection=88 matchedByAlias=40 missing=0 pageFailures=0
AUDIT_EXIT=0
GALLERY SMOKE: clicks=942 pageShows=699 itemShows=588 failures=0
SMOKE_EXIT=0
```

**截图**：未重跑（改动为 Group 字符串/timer 门控/文档，不影响渲染）；`shots/light=233`、`shots/dark=233` 保持与 docs/07 一致；3 个 Foundation 页 Group 已改为字面量 `P0 · Foundation`。

**越界修改**：修复只改了 4 个文件（3 个 Foundation 页面 + `ArcToolApprovalSheet.cs`），加新增 26 文件与 docs/06/07/README；无既有 csproj/主题/App.axaml 改动。

### 7.3 残余与最终结论

- **残余 P0/P1 = 0 / 0**；P2 3/3、P3 3/3 全部关闭；无新增 P0/P1/P2。
- **可选 P3（不阻塞交付）**：① `ReminderRowPage` 仍以三行并列示例展示三态，若未来要求"页内状态切换控件"而非文档声明，可再补 3 个按钮；② `docs/07:65` 的 `dotnet build ... -t:Rebuild -v:q` 在本环境不可直接复现，建议标注仓库串行参数（`-m:1 -nodeReuse:false --no-restore`）或直接引用 `build.ps1`。
- **最终判定：可交付。** 功能/构建（9 既有 warning、0 error）/审计/smoke/截图/纯 UI 边界/文档均已通过；P0 14 项可作为 UI 交付物验收。
