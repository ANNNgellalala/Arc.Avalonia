# 13-review-c · Agent 顶层 v3 对抗式评审（API / 口径 / 里程碑）

> **评审对象**：`docs/13-framework-layering-and-agent-top.zh.md`（v3，2026-11-05，717 行 / 61,222 B，SHA256 `66C187E3B6F684C563751B0ED0BC71E9D8A10A4B26276D50DDBDE5B5A52A82DC`）。
> **评审范围**：D8（Agent 顶层）、D9（Patterns/LifeToday）、G4/G5、§4.3 里程碑门槛、§6 风险登记册、§7 不做清单；对应 Lead 的 5 个必查项。
> **评审基准**：结论仍以 `docs/analysis/13-c-agent-top-layer-audit.zh.md`（下称 **C 报告**）为准；本评审同时修正 C 报告自身未覆盖的「多会话 host」缺口（C 报告同样只有单会话 `IArcAgentSession`，见 §2.4）。
> **评审方式**：只读 `read/grep/glob` + 只读 PowerShell 静态核对；**未运行 `dotnet build` / `dotnet run` / `tools/*.ps1`**（由 Lead 统一验证）；**未修改主文档与任何源码**，唯一写入为本文件。
> **基线时间**：2026-11-05（工作树未提交改动仍在，主文档数字与源码实测的差异见 P2-01）。

---

## 0. 结论摘要

### 0.1 总判定

**有条件通过：v3 的职责口径（Arc=UI 组件库、渲染层归 Avalonia）是正确的，D8 的「表现层运行时」方向也与 C 报告一致；但 D8 的 API 形状只覆盖单会话、D8 的「帧级 16 ms 合并器」与 v3 §2.0 的「不做渲染层」自相矛盾，M2/M3 的门槛不可判定，G4/G5 漏了多会话/迁移/边界 CI 三块工作。**

| 级别 | 数量 | 含义 |
| --- | --- | --- |
| **P0** | 4 | 不修则 M2 验收无法成立，或会违反 v3 自己定的口径；必须在 M0–M2 之间关闭 |
| **P1** | 9 | 不修则 M3「完整顶层」不可验收 / 发布形态有缺口；M3 前关闭 |
| **P2** | 3 | 口径/数字/术语的收尾项，可与主文档下一版同批 |

### 0.2 Finding 速览

| ID | 级别 | 一句话 | 主要位置 | 人日增量 |
| --- | --- | --- | --- | --- |
| C-R-01 | **P0** | 缺多会话/多 run 的 Workspace（Host）API；单会话 `IArcAgentSession` 驱动不了后台 dock / 注意力收件箱 / 并行子 agent | D8 API 块 `L408-419`、G4 `L473` | **+8**（MVP 4 / 完整 +4） |
| C-R-02 | **P0** | 「帧级 16 ms 合并 token delta」= 自建刷新节奏，违反「不做渲染层」；R3 已写 Dispatcher，D8 未改 | D8 `L400` vs `L14/L31/L179/L287/L584/L643` | **+1.5** |
| C-R-03 | **P0** | M2 门槛「10k 事件流式渲染不卡」不可判定；无压测夹具/阈值/基线机 | §4.3 `L488` | **+3** |
| C-R-04 | **P0** | Host 边界只靠「评审拦截」，未 CI 化；R2 的缓解不可执行 | §6 R2 `L583`、§8 `L628-634` | **+2** |
| C-R-05 | P1 | 「渲染器注册表」术语与「渲染层不做」冲突，易被误读为 Arc 管渲染 | D8 `L402`、L4 `L199`、`L273`、G4 `L473` | +0.5 |
| C-R-06 | P1 | `IArcAgentSession` 用 Messages/ToolCalls/PendingApprovals 分离列表，丢失 run 内顺序；`Apply` 无返回值；命令接口归属/命名不一致（`IArcAgentCommandSink` 写成宿主实现、`IArcAgentServices` 未定义） | D8 `L408-419`、`L648` | +2 |
| C-R-07 | P1 | M3「Agent 完整顶层」无验收清单（控件/页面/测试/文档） | §4.3 `L489`、M3 路线图 `L567-572` | +2 |
| C-R-08 | P1 | G4/G5 未列 AgentUI 6 控件合并/改名、LifeToday 迁移、13 个场景页出库归属；G2 的 268 文件是否含这 13 页未说明 | G2 `L466`、G4 `L473`、G5 `L474`、D9 `L430-431` | +4 |
| C-R-09 | P1 | 许可/第三方声明未覆盖 Agent 层与 AG-UI 语义参考（AG-UI 为 MIT） | D10 `L439`、§6 R9 `L590` | +0.5 |
| C-R-10 | P1 | G4「完整 104」= C 报告 M 档，不是 C 的 L 档 135；W9 的 7 项（23 人日）未计价，M3 却称「完整」 | `L49`、`L473`、`L489`、C §5.4 | 0（重定口径）或 +31（若纳入） |
| C-R-11 | P1 | D9「Blocks.AI 6 页重写为顶层示例」与 D2/R5「页面/示例必须出库」冲突；重写目标工程未定 | D9 `L430` vs D2 `L307-315`、R5 `L228` | +0.5 |
| C-R-12 | P1 | 风险登记册缺 C 报告 R3「审批/中断并发（前后台 run 同时请求）」；只保留 R2/R3 两条 Agent 风险 | §6 `L578-591` vs C §8.1 | 0（补文档） |
| C-R-13 | P1 | 元包 `Arc.Avalonia` 包含 Agent，与 M2「Agent 独立成包、不通过不影响 L1–L2 发版」的回滚承诺矛盾 | §2.3 `L244` vs M2 回滚点 `L565` | +0.5 |
| P2-01 | P2 | 基线数字内部不一致（`L66` 328 页/298 docs vs `L71` pages=294/smoke=315；`L61-62` 774 文件/182,621 行与当前树复算不符） | §0.3 `L57-72` | 0（M0 重算） |
| P2-02 | P2 | §7「不做」清单未显式包含「不自建合并节奏/帧时钟」「不做多会话持久化/事件日志存储」 | §7 `L595-604` | 0 |
| P2-03 | P2 | 事件模型缺 `EventId`、run 类型（前台/后台/子 agent）与 parentRunId、乱序/孤儿事件策略；多 run 路由无依据 | D8 `L398`、`L408-414`、C §4.3.1 | +0.5（并入 C-R-01） |

### 0.2.1 Finding 登记册（问题 / 证据 / 影响 / 建议 / 人日）

| ID | 级别 | 问题 | 证据 | 影响 | 建议 | 人日 |
| --- | --- | --- | --- | --- | --- | ---: |
| C-R-01 | P0 | 缺多会话/多 run 容器；单会话 `IArcAgentSession` 驱动不了后台 dock/收件箱/并行子 agent | v3 `L398`、`L408-414`；C `L394-414`、`L699-713`、`L954`；`ArcBackgroundRunDock.cs:40-48`、`ArcAgentAttentionInbox.cs:66-92` | M2「Agent MVP」只能用单 run 假数据；3 个 P0 控件无法真实接入 | 新增 `IArcAgentWorkspace`（多会话路由 + 跨 run 审批/注意力/artifact 队列 + `ArcAgentRunRef`）；`ArcAgentSurface` 改绑 Workspace | 8 |
| C-R-02 | P0 | 「帧级 16 ms 合并器」= 自建刷新节拍，违反「不做渲染层」 | v3 `L400` vs `L14`、`L287`、`L584`、`L643` | 口径自相矛盾；timer 生命周期/泄漏；headless 不可测 | 改「UI 批次合并（Avalonia `Dispatcher` + 合并标志）」，去掉帧常量/定时器；`IUiDispatcher` 便于单测 | 1.5 |
| C-R-03 | P0 | M2「10k 事件流式渲染不卡」不可判定 | v3 `L488`；G3f 在 M3 `L567-572` | M2 无法客观验收；易演变为「演示截图通过」 | 预载 10k + 60/s×60s；headless 断言 0 全量重建/可见行/分配/GC/内存；桌面 p95/p99 帧时间与 CPU | 3 |
| C-R-04 | P0 | Host 边界只有「评审拦截」，无 CI | v3 `L583`、`L628-634` | D8「无 I/O」随实现漂移，宿主锁定风险 | BannedApiAnalyzers + 引用白名单 + 无宿主渲染 smoke + PR 检查项 | 2 |
| C-R-05 | P1 | 「渲染器注册表」易误读为渲染层 | v3 `L93`、`L148`、`L199`、`L273`、`L402`、`L473`、`L562` | 对外沟通/评审误解 Arc 管渲染 | 改「条目视图注册表 / `ArcAgentItemViewRegistry` + `DataTemplateSelector`」 | 0.5 |
| C-R-06 | P1 | 分离列表丢顺序；`Apply` 无返回值；命令接口归属写反、命名不一致 | v3 `L408-419`、`L417`、`L648`；C `L394-414`、`L640-650` | transcript 无法按 run 内顺序渲染；重复/乱序无法拒绝；宿主接入文档会写错 | 统一有序 `Timeline` + `ArcApplyResult`；`IArcAgentCommandSink` 归 UI；宿主只实现 Transport/UiServices | 2 |
| C-R-07 | P1 | M3「Agent 完整顶层」无控件/页面/测试/文档清单 | v3 `L489`、`L567-572` | M3 验收靠主观判断 | 采用 §5.4 的 A–E 清单（API/控件/页面/测试/文档） | 2 |
| C-R-08 | P1 | G4/G5 未列 AgentUI 合并、LifeToday 迁移、13 场景页出库归属 | v3 `L243`、`L430-431`、`L466`、`L473`、`L474`；源码 `AgentUI` 12 .cs + 6 页、`LifeToday` 7+7、`Gallery/App.axaml:21-22`、`Gallery.csproj:31-32` | 包拓扑不一致；Agent 包可能带 Pages（R5 失败）；迁移漏项 | G4 增加 AgentUI→Agent 合并（2.5）；G5 增加 LifeToday 迁移（1.5）；G2 明确 13 页出库 | 4 |
| C-R-09 | P1 | 许可/第三方声明未覆盖 Avalonia/AG-UI | v3 `L439`、`L590`、`L421` | 发布合规缺口 | `THIRD-PARTY-NOTICES.md`（Avalonia MIT / SkiaSharp MIT / Inter OFL / Lucide ISC / AG-UI MIT 语义参考）+ pack 断言 | 0.5 |
| C-R-10 | P1 | G4「完整 104」= C 的 M 档，非 L 档 135；W9 7 项未计价 | v3 `L49`、`L473`、`L489` vs C `L836` | M3 虚报「完整」，预算/范围失控 | 二选一：按 135 重估（M3≈388）或把「完整」改「P0+P1 完整」并把 W9 后置 | 0 / +31 |
| C-R-11 | P1 | Blocks.AI 6 页「重写为顶层示例」与 D2/R5「页面出库」冲突；目标工程未定 | v3 `L430` vs `L307-315`、`L228` | 示例可能进 Agent 发布包，违反发布形态 | 重写目标放 `Arc.Avalonia.Docs`/`samples`；Blocks.AI 18 条保留示例 | 1 |
| C-R-12 | P1 | 风险登记册缺多 run 审批并发 | v3 `L578-591` vs C `L954` | 最危险的并发缺陷无跟踪/测试 | 新增 R11，缓解指向 Workspace 全局串行审批 + 交错测试 | 0 |
| C-R-13 | P1 | 元包含 Agent，与 M2 回滚承诺矛盾 | v3 `L244` vs `L565` | Agent 失败会拖住 L1–L2 元包发版 | 元包不含 Agent；Agent 独立包（推荐）或删回滚承诺 | 0.5 |
| P2-01 | P2 | 基线数字内部不一致 | v3 `L66`（328 页/298 docs）vs `L71`（294/315）；当前树 315 页/294 docs | 评审基线漂移 | M0 冻结时用 `--audit/--smoke` 实测 + 注口径 | 0 |
| P2-02 | P2 | §7 未显式列「不自建合并节拍」「不做多会话持久化」 | v3 `L595-604` | 口径留缝 | 补两条 | 0 |
| P2-03 | P2 | 事件缺 `EventId`/run 类型/parentRunId/孤儿策略 | v3 `L398`、`L408-414`；C §4.3.1 | 多 run 路由/幂等/子 agent 关系无依据 | 并入 C-R-01 的 `ArcAgentRunRef` + `ArcApplyResult` | +0.5 |

### 0.3 最大 API 缺口

**`IArcAgentSession` 是「一个 run 的视图投影」，但 v3 要交付的 6 个 AgentUI 控件里，有 3 个天然需要「跨 run 身份/聚合」：`ArcBackgroundRunDock`（后台 run）、`ArcAgentAttentionInbox`（跨 run 聚合）、`ArcRunControlBar`（前台 run 需与后台 run 共存）。** 当前 API 没有 `RunId → 会话` 的路由、没有跨 run 的审批/注意力队列归属、没有「后台 run 列表」；C 报告设计的 `ArcAgentSurface` 也只接一个 `Session`。因此 **C-R-01 是 v3 最大的 API 缺口**：不是「多一个接口」，而是**会话级 API 的容器缺失**。修复后 C 报告 46（MVP）/104（完整）需变为 **≈54 / ≈118**（见 §7）。

### 0.4 建议新增人日（给排期）

- **净新增工作**：**≈25 人日**（P0 14.5 + P1 ≈10 + P2 ≈0.5；P2-03 的实现并入 C-R-01，但人日单列）。
- **建议分配**：G0 +3（边界 CI/术语/元包决策）、G4 +19（Workspace/API/批处理/压测/迁移/Blocks 目标）、G5 +3（LifeToday/许可/压测基线）、G2 并入 G5；取整 3/19/3。
- **估算口径修正（非新增工作）**：若 M3 的「完整」按 C 报告 L 档（135）定义，G4 需 +31 → **M3 从 357 改为 ≈413（范围 320–540）**；若维持 357，必须把 M3 的「完整」明确改为「P0+P1 完整（不含 06 长尾 7 项）」，并把 W9 列入 post-1.0（见 C-R-10）。

---

## 1. 评审基线与方法

### 1.1 评审对象与基线

| 项 | 值 |
| --- | --- |
| 主文档 | `docs/13-framework-layering-and-agent-top.zh.md`（717 行 / 61,222 B / SHA256 `66C187E3…82DC`） |
| C 报告 | `docs/analysis/13-c-agent-top-layer-audit.zh.md`（1,061 行；本评审的结论基准） |
| 源码基线 | 工作树当前状态（有未提交改动；AgentUI/LifeToday csproj 已带 `EmbeddedResource Docs\*.json`） |
| 现有 AgentUI | 6 控件 + 6 页面（`src/Agent/Arc.Avalonia.AgentUI`，`namespace Arc.Avalonia.AgentUI.*` 共 12 个 .cs 文件） |
| 现有 LifeToday | 7 控件 + 7 页面（`src/Agent/Arc.Avalonia.LifeToday`） |
| 现有 Blocks.AI | 18 个 `ArcItemPage`，0 可复用控件（C §2.6） |

### 1.2 证据来源

1. 主文档 v3 的行号（本文全部用 `Lxxx` 指主文档）；
2. C 报告的 §2（资产）、§3.3（表现层运行时判断）、§4.3（API 概形）、§5.4（S/M/L 人日）、§8.1（风险）；
3. 源码：6 个 AgentUI 控件的公开 API、`ArcAttentionItem`/`ArcBackgroundRunDock` 的数据形状、`ArcToolApprovalSheet` 的展示定时器；
4. `docs/06` §7（UI 契约 vs 宿主边界）、`docs/07`（P0 实现与限制）。

### 1.3 判定口径

| 级别 | 定义 |
| --- | --- |
| **P0** | 阻断 M2 验收，或使 v3 违反自己定义的职责/口径；必须在 M0–M2 之间修复 |
| **P1** | 不阻断 M2，但不修则 M3「完整顶层」无法验收、发布形态有缺口或迁移会漏项 |
| **P2** | 数字/术语/文档收尾，可与下一版主文档同批；不影响里程碑 |

### 1.4 与 C 报告的关系（重要）

v3 D8 标注「C 报告 §3.3/§4.3，采纳」。但**C 报告的 §4.3 也只有单会话 `IArcAgentSession` + 会话级 `ArcApprovalQueue`/`ArcAttentionQueue`**（C 报告 `L394-414`、`L699-712`），同样缺多会话容器。C 报告的风险 R3 已经明确写了「后台 run 与前台 run 同时请求」（C `L954`），但 API 没有对应形状。**因此 C-R-01 不是「v3 没抄全 C」，而是 C 与 v3 共同遗漏，本评审一并补上**（见 §2.3/§2.4）。

### 1.5 本评审不做

- 不修改主文档、C 报告或源码（唯一写入本文件）；
- 不运行构建/截图/smoke/pack（由 Lead 统一验证）；
- 不重新评估 A/B/D 报告与 L2 的 162 人日（只评价 Agent 顶层的接口、口径与门槛）；
- 不评价 v3 的 M1/G1–G3 估算（不在本任务范围）。

---

## 2. Q1 · 多会话/多 run：是否需要 `IArcAgentHost`？

### 2.1 结论

**需要，且必须是 P0。** 建议命名为 `IArcAgentWorkspace`（而不是 `IArcAgentHost`，理由见 §2.5 的「命名建议」），并把它放进 G4 的 MVP；没有它，现有 6 个 AgentUI 控件中至少 3 个无法被正确驱动，D8 的「组合根」也不成立。

### 2.2 现状证据（v3 只有单会话）

| # | 事实 | 证据 |
| --- | --- | --- |
| 1 | 事件带 `RunId` + 单调 `Sequence` | v3 `L398`：`事件模型（ArcAgentEvent + RunId + 单调 Sequence）` |
| 2 | 会话接口只有一个，且没有 `RunId`/`SessionId` 路由；`Apply` 接收任意事件 | v3 `L408-414`：`IArcAgentSession { Run; Messages; ToolCalls; PendingApprovals; Apply(e); Changed }` |
| 3 | 主文档全文没有「多会话/多 run/后台 run/会话管理」；grep 无 `IArcAgentHost`/`SessionManager`/`多会话` | v3 全文（本次只读 grep） |
| 4 | 组合根只接一个 `Session` | C 报告 `L699-712`（`SessionProperty`、`Approvals`/`Attention` 在 Surface 上）；v3 `L418` 只有 `IArcAgentRendererRegistry` |
| 5 | 审批/注意力/artifact 队列在 C 报告里是**会话级**（Surface 属性），不是跨 run 的 | C 报告 `L699-713`（`Approvals`、`Attention`、`Artifacts` 为 Surface 实例属性） |
| 6 | 现有 6 个 P0 控件的数据形状本身就跨 run：dock 只有单 run 的 `Status/TaskLabel/Progress`，inbox 的 `ArcAttentionItem` 没有 `RunId` 字段 | `src/Agent/Arc.Avalonia.AgentUI/Controls/ArcBackgroundRunDock.cs:40-48`；`ArcAgentAttentionInbox.cs:66-92` |
| 7 | C 报告已识别「后台 run 与前台 run 同时请求」是并发风险，但 API 无承载 | C 报告 `L954`（R3） |

### 2.3 场景矩阵：为什么单会话接口不够

| 场景 | 需要同时存在的 run | 受影响的现有控件 | 单会话接口的失败方式 |
| --- | --- | --- | --- |
| 前台对话 + 后台研究 | 2 个（foreground + background） | `ArcRunControlBar`、`ArcBackgroundRunDock` | dock 没有数据来源；Surface 的 `Run` 只能是前台 |
| 前台对话 + 并行子 agent | 1 + N（lead + lanes） | `ArcAgentSubAgentBoard`（待建）、`ArcAgentTranscript`（子 agent 事件流） | 无法把 lane 事件路由到子会话；transcript 会把它们混进主 run |
| 注意力收件箱 | 跨 run（审批/追问/失败/例行） | `ArcAgentAttentionInbox` | 只能聚合当前会话；后台 run 的审批不会出现在收件箱 |
| 审批队列 | 跨 run，需串行弹一张表 | `ArcToolApprovalSheet`/`ArcAgentApprovalTray` | 每个会话各弹一张，用户被多张表打断；无法去重/排序 |
| 后台 run 完成通知 | 后台 run 结束，前台不变 | `ArcBackgroundRunDock`、toast | 无「run 完成」信号源 |
| 切换会话（历史） | N 个已结束 run + 当前 | `ArcAgentSurface` | Surface 只能持有一个 Session，切换即重建 |

> 结论：**跨 run 的一致性（审批串行、注意力聚合、dock 列表、子 agent 父子关系）只能由 UI 侧统一管理**；交给每个宿主自行实现，等于把 6 个控件的编排成本乘以宿主数量——这正是 D8「表现层运行时」要避免的（v3 `L393`、C `L234`）。

### 2.4 与 C 报告 §4.3 的差异（需要同步修正）

| 项 | C 报告 §4.3 | v3 D8 | 问题 | 建议 |
| --- | --- | --- | --- | --- |
| 会话容器 | 无（只有 `IArcAgentSession`） | 无 | 多 run 无归属 | 新增 `IArcAgentWorkspace`（§2.5） |
| 会话内容 | `Items: IReadOnlyList<ArcAgentItem>`（统一有序条目，含 Message/ToolCall/Approval/Artifact 子类） | `Messages`/`ToolCalls`/`PendingApprovals` **三个分离列表** | 分离列表丢失 run 内交错顺序，transcript 无法按发生顺序渲染 message→tool→message；且三套集合的变更通知要分别处理 | 改为统一有序 `Timeline`（或 `Items`），按 `Sequence` 排序；保留 `Messages`/`ToolCalls` 作为投影视图（只读派生） |
| `Apply` 返回值 | `bool Apply(...)`（C `L404`） | `void Apply(...)` | 无法拒绝重复/乱序/未知 run 事件，无法向宿主回报接受结果 | 改为 `ArcApplyResult Apply(ArcAgentEvent e)`（`Accepted/Duplicate/OutOfOrder/UnknownRun`） |
| 审批/注意力归属 | Surface 实例属性（会话级） | 未写 | 多 run 时审批必须跨会话串行；注意力必须跨 run 聚合 | 提升到 Workspace 级；Session 保留只读投影 |
| 命令方向 | `IArcAgentCommandSink` 是 UI 侧入口，宿主实现 `IArcAgentTransport.SendCommandAsync`（C `L640-650`、`L703`） | `IArcAgentCommandSink { ... } // 宿主实现：执行 stop/steer/approve/takeover`（`L417`） | 归属写反：宿主同时实现 Transport 和 CommandSink，两个都「执行命令」，UI 侧没有命令出口 | 明确：`IArcAgentCommandSink` 由 Surface/Workspace 实现（UI 侧）；宿主只实现 `IArcAgentTransport`；若保留宿主命令处理器，命名为 `IArcAgentCommandHandler` |
| 宿主接口名 | `IArcAgentUiServices`（C `L673`） | §8 写 `IArcAgentServices`（`L648`），D8 未定义 | 三个名字（Transport/CommandSink/Services）对应不清，宿主接入文档会写错 | 统一为 `IArcAgentUiServices`（或 `IArcAgentServices` 全局改名），并在 D8 API 块列出 |

### 2.5 必须补的接口（签名级草案）

```csharp
namespace Arc.Avalonia.Agent;

/// <summary>run 的稳定引用：收件箱、审批、artifact、dock、子 agent lane 都指向它。</summary>
public readonly record struct ArcAgentRunRef(
    string RunId,
    string SessionId,
    ArcAgentRunKind Kind,          // Foreground / Background / SubAgent
    string? ParentRunId = null,    // 子 agent 的父 run
    string? Label = null,          // dock / lane 的展示名
    int Depth = 0);

public enum ArcAgentRunKind { Foreground, Background, SubAgent }

/// <summary>
/// UI 侧多会话协调器：创建/激活/关闭会话，按 RunId 路由事件，持有跨 run 的
/// 审批/注意力/artifact 队列；不执行 I/O，不持久化，不调用模型。
/// 命名用 Workspace 而不是 Host，避免与 L5「宿主应用」混淆（见 §2.5「命名建议」）。
/// </summary>
public interface IArcAgentWorkspace
{
    IReadOnlyList<IArcAgentSession> Sessions { get; }
    IArcAgentSession? Active { get; }

    IArcAgentSession Create(ArcAgentSessionDescriptor descriptor);
    bool TryGet(string sessionId, out IArcAgentSession session);
    void Activate(string sessionId);                 // 前台切换
    void Close(string sessionId);                    // 只关闭视图，宿主决定是否停止执行

    // 唯一事件入口：按 e.RunId 找到会话并 Apply；未知 RunId 进入 orphan 缓冲
    ArcApplyResult Dispatch(ArcAgentEvent e);
    int DispatchRange(IEnumerable<ArcAgentEvent> events);   // 返回被接受事件数

    IReadOnlyList<ArcAgentRunRef> Runs { get; }       // 含前台/后台/子 agent，供 dock/board
    IReadOnlyList<ArcAgentRunRef> BackgroundRuns { get; }

    // 跨 run 聚合（从会话级提升）
    ArcApprovalQueue Approvals { get; }               // 全局串行：同一时刻最多一张审批
    ArcAttentionQueue Attention { get; }              // 跨 run 去重/排序
    ArcArtifactIndex Artifacts { get; }               // 按 RunRef 归属

    event EventHandler<IArcAgentSession>? SessionCreated;
    event EventHandler<IArcAgentSession>? ActiveChanged;
    event EventHandler<ArcAgentRunRef>? RunStarted;    // dock 订阅「后台 run 出现」
    event EventHandler<ArcAgentRunRef>? RunFinished;
}

public sealed record ArcAgentSessionDescriptor(
    string SessionId,
    string Title,
    ArcAgentRunKind Kind,
    string? ParentRunId = null,
    ArcAgentSessionOptions? Options = null);

public enum ArcApplyResult { Accepted, Duplicate, OutOfOrder, UnknownRun, Orphaned }
```

**对现有 API 的最小改动（v3 D8 之外）**：

```csharp
public interface IArcAgentSession
{
    string SessionId { get; }
    ArcAgentRunRef RunRef { get; }                    // 新增：身份，不只是 Run 状态
    ArcAgentRunState Run { get; }
    IReadOnlyList<ArcAgentItem> Timeline { get; }      // 统一有序（替代三个分离列表）
    IReadOnlyList<ArcAgentMessage> Messages { get; }   // 投影视图（派生）
    IReadOnlyList<ArcToolCall> ToolCalls { get; }      // 投影视图（派生）
    IReadOnlyList<ArcApprovalRequest> PendingApprovals { get; }
    ArcApplyResult Apply(ArcAgentEvent e);
    event EventHandler<ArcSessionChangedEventArgs>? Changed;
}

public class ArcAgentSurface : ContentControl
{
    public static readonly StyledProperty<IArcAgentWorkspace?> WorkspaceProperty;  // 替代单一 Session
    public static readonly StyledProperty<string?> SessionIdProperty;             // 绑定到其中一个会话
    // BackgroundRunDock/AttentionInbox 绑定 Workspace.Runs / Workspace.Attention
}
```

#### 命名建议：`Workspace` 而不是 `Host`

v3 全文用「宿主」指 L5 应用（`L92`、`L199-200`、`L456`）。若 UI 侧再叫 `IArcAgentHost`，会和「宿主应用」冲突；建议：

| 候选名 | 评价 |
| --- | --- |
| `IArcAgentWorkspace`（推荐） | 明确是「UI 侧多会话工作区」；与 L5 宿主无歧义 |
| `IArcAgentSessionHub` | 也可，但 Hub 暗示网络/总线 |
| `IArcAgentCoordinator` | 过于泛化 |
| `IArcAgentHost` | 与 L5 宿主撞名；若坚持用，必须在术语表写「UI Host ≠ 宿主应用」 |

### 2.6 是否已含在 G4 的 46/104 人日？

**结论：没有。** G4 的 MVP 明细是「事件模型 8 + 增量 transcript 14 + 审批/工具 8 + 渲染器/队列 5 + Gallery/护栏 6 + 迁移 5」（`L473`）；完整档再加「子 agent/artifact/diff 10 + 治理 UI 35 + 重写 14」。逐项对照：

| G4 已有条目 | 能覆盖本 finding 的部分 | 缺口 |
| --- | --- | --- |
| 事件模型 8 | `RunId + Sequence`（`L398`） | 不覆盖：会话容器、RunId→Session 路由、run 类型/parentRunId、orphan 策略 |
| 审批/工具 8 | 单 run 的审批/工具卡片 | 不覆盖：**跨 run 审批串行队列**（多张表/去重/顺序） |
| 渲染器/队列 5 | 会话级 Attention/Approval 队列 | 不覆盖：跨 run 聚合、Workspace 级所有权 |
| 子 agent/artifact/diff 10 | 子 agent 面板/artifact 展示 | 不覆盖：lane→子会话的映射与父子 run 引用 |
| 迁移 5 / 重写 14 | AgentUI/Blocks.AI 迁移的一部分 | 不覆盖：见 C-R-08 |

**建议拆分（合计 +8）**：

| 子项 | 内容 | 人日 | 档位 |
| --- | --- | --- | --- |
| 1 | `IArcAgentWorkspace` + `ArcAgentRunRef` + 会话路由/orphan 缓冲 + 单会话兼容层 | 4 | MVP（M2） |
| 2 | 跨 run 审批串行、注意力聚合、artifact 索引所有权；dock/子 agent 绑定 | 2 | 完整（M3） |
| 3 | 多 run 交错测试（审批/停止/过期/切换/孤儿事件）+ 双会话/后台示例页 | 2 | 完整（M3） |

> 若预算紧张：MVP 至少做子项 1（4 人日），否则 M2 的「Agent 顶层 MVP」无法渲染 `ArcAgentBackgroundRunDock`/`ArcAgentAttentionInbox` 的真实多 run 数据，只能继续用单 run 假数据，等于 MVP 没兑现。

### 2.7 建议修订点

| 位置 | 修订 |
| --- | --- |
| D8 表（`L396-403`） | 「纯投影 reducer」一行补「Workspace 多会话路由」；「审批队列/注意力队列」改为「跨 run 队列」 |
| D8 API 块（`L405-419`） | 增加 `IArcAgentWorkspace`/`ArcAgentRunRef`/`ArcApplyResult`；`IArcAgentSession` 改为 `Timeline` + `ApplyResult`；修正 `IArcAgentCommandSink` 归属 |
| §2.1 L4（`L199`） | 「表现层运行时」补「多会话协调」 |
| §8 度量（`L648`） | 宿主实现接口数改为「Transport（+可选 UiServices）」；UI 实现 Workspace/Session/CommandSink/ViewRegistry |
| G4（`L473`） | 本 finding 使 G4 +8：MVP 46→50、完整 104→112；G4 全部修订见 §7.1 |
| M2/M3 验收（`L488-489`） | M2 增加「双会话 + 后台 dock + 收件箱同屏」示例页；M3 增加多 run 交错测试 |

---

## 3. Q2 · 「帧级 16 ms 合并器」是否违反「不做渲染层」？

### 3.1 结论

**「批量合并」是 UI 层可做的；「帧级 16 ms + 自建合并节拍」不是。** 应改为 **基于 Avalonia `Dispatcher` 的批次/优先级合并（不自建帧时钟）**，并把术语从「帧级」改为「UI 批次」。当前 v3 文本自相矛盾：D8 写「帧级 16 ms」（`L400`），R3 却写「用 Avalonia `Dispatcher` 合并，不自建帧时钟」（`L584`）；§0.0/§2.0/§7/§8 又反复承诺「不做帧调度器/动画时钟/渲染层」（`L14`、`L31`、`L179`、`L287`、`L456`、`L522`、`L643`）。

### 3.2 冲突证据

| # | 文本 | 行号 | 冲突点 |
| --- | --- | --- | --- |
| 1 | 「不承担渲染层职责：帧调度器、绘制缓存……动画时钟……全部归 Avalonia」 | `L14` | 16 ms 合并器 = 自建刷新节拍 |
| 2 | 「中间层是否包含帧调度器/绘制缓存？**否（v3 明确不做）**」 | `L287` | D8 是 L4，但同一职责原则适用于 L4 |
| 3 | 「渲染层自研代码 **0**（不新增帧时钟/绘制缓存/主题失效协议）」 | `L643` | 「帧级 16 ms」= 新增帧时钟 |
| 4 | 「增量合并器（帧级 16 ms 合并 token delta）」 | `L400` | 与 1/2/3 直接矛盾 |
| 5 | R3 缓解：「用 Avalonia `Dispatcher` 合并，**不自建帧时钟**」 | `L584` | 同一文档已给出正确做法，D8 未同步 |
| 6 | C 报告亦用「16 ms/帧」措辞（`ArcDeltaCoalescer(TimeSpan? frameInterval…)`） | C `L431-437`、C `L953` | C 也需同步改名/改口径 |

### 3.3 建议实现（不自建帧时钟）

```csharp
namespace Arc.Avalonia.Agent;

/// <summary>UI 批次合并：消费 Avalonia 既有 Dispatcher 的调度，不持有定时器/帧常量。</summary>
public sealed class ArcAgentUiUpdateBatch
{
    public ArcAgentUiUpdateBatch(IUiDispatcher dispatcher, int maxBatchSize = 256);

    /// <summary>跨线程可调用：入队后若未挂起调度，则 Post 一次 Flush。</summary>
    public void Enqueue(ArcAgentEvent e);      // O(1)，不立即应用

    /// <summary>由 Dispatcher 在 Background 优先级回调；每次把队列内事件一次应用。</summary>
    public event EventHandler<ArcAgentEventBatch>? Flush;

    // 可测性：测试注入同步 dispatcher，断言「N 次 Enqueue 只产生 1 次 Flush」
}

public interface IUiDispatcher
{
    bool CheckAccess();
    void Post(Action action, UiDispatchPriority priority);  // 包装 Dispatcher.UIThread.Post
}
```

实现要点：

1. **不写 `TimeSpan.FromMilliseconds(16)`**，不 `new DispatcherTimer`；`Post` 的优先级用 `DispatcherPriority.Background`（token 追加让位于输入/渲染），消息结束时用 `Render`/`Loaded` 做最终 flush。
2. **合并标志**：`_flushScheduled` 布尔 + 有界队列（`MaxBatchSize`），避免每个 delta 都 Post 一次（否则 60 chunks/s 会变成 60 次 `Post`/s，UI 线程队列被挤爆）。
3. **文本最新值合并、工具参数按序追加**：文本 delta 可只保留最新拼接结果；JSON 参数增量必须保序（不可丢），队列满时对文本做合并、对参数做背压（丢弃最老的非语义事件并通知宿主）。
4. **UI 未挂载时缓冲**：`Transport` 在窗口 Attach 前推事件时先入模型队列，`AttachedToVisualTree`/`Loaded` 时统一 flush；不启动任何后台时钟。
5. **测试替身**：`IUiDispatcher` 使 reducer/batch 可无 UI 单测（headless 不需要真实渲染）。

### 3.4 实现差异与性能风险（60 chunks/s）

| 方案 | 刷新节拍 | 延迟 | 60 chunks/s 的行为 | 风险 |
| --- | --- | --- | --- | --- |
| **自建 16 ms 定时器**（v3 D8 现文） | 固定 60 Hz | 0–16 ms | 最多 60 次 flush/s；如果 UI 忙，timer 事件堆积，可能一次 flush 处理 2–3 个 tick | 违反口径；timer 生命周期/泄漏；headless 不可测 |
| **Dispatcher Post + 合并标志**（建议） | 由 Dispatcher 消息泵决定（通常每 UI pass 1 次） | 取决于消息泵，通常 ≤1 帧 | 每 pass 最多 1 次 flush；空闲时可能比 60 Hz 更频繁（2–3 次/帧），但每次只处理队列内事件 | 若忘记合并标志 → 每次 delta 都 Post，队列洪泛；优先级选错（`Input`）会抢输入 |
| **不合并** | 每 delta 即应用 | 最低 | 60 次全量重建/s × N 行 = 每秒 O(60N) 行构建（N=10k 即 60 万行/s） | 长会话必卡；这是 R3 的原始风险 |

**关键判断：合并器只解决“每次 delta 都重建”的常数问题，不解决 O(n²)。** 即使按 16 ms 或 Dispatcher 合并，如果 transcript 仍然 `Children.Clear()` 全量重建（C §2.2，`ArcAgentTranscript.cs:150-160,199-290`），60 次/s × 10,000 行仍然是每秒 60 万行构建——不可用。**R3 的缓解顺序必须写成：G3c 增量集合 + 虚拟化（主）→ 批次合并（辅）→ 文本 O(1) 追加**；只做批处理不能过 M2 门槛。

### 3.5 对 M2 门槛的连带影响

- M2 门槛应断言「**全量重建次数 = 0**」「可见行 ≤ 2×视口」「分配/内存阈值」，而不是「不卡」（见 C-R-03）。
- `Dispatcher` 合并不算渲染层自研代码；把它写进 §4.1「不含渲染层优化」的例外说明：「Arc 只使用 Avalonia 既有 Dispatcher 做批次合并，不新增帧时钟/帧常量/定时器」。
- 现有 `ArcToolApprovalSheet` 的展示倒计时 `DispatcherTimer`（`ArcToolApprovalSheet.cs:141,676`）若随 AgentUI 进入 Agent 包，会与 C-R-04 的 CI 禁令冲突；建议改为「宿主发 `ExpiresAt`/tick 事件，UI 只显示」，或在 CI 中留一个**仅展示时钟**的显式豁免（见 C-R-04）。

### 3.6 建议修订点 + 人日

| 位置 | 修订 |
| --- | --- |
| D8 表 `L400` | 改为「UI 批次合并（基于 Avalonia `Dispatcher`，不自建帧时钟）」 |
| R3 `L584` | 保持，补「与 D8 同步」 |
| §7 `L595-604` | 增一条「不自建帧时钟/合并节拍；只用 Avalonia Dispatcher」 |
| §8 度量 `L643` | 「渲染层自研代码 0」保留，补注「Dispatcher 批次合并不计入」 |
| C 报告同步（供后续修订） | `ArcDeltaCoalescer` 改名 `ArcAgentUiUpdateBatch`，去掉 `frameInterval` |

**人日：+1.5**（批处理实现与原有 16ms 方案工作量相近，主要增加 `IUiDispatcher` 可测性抽象、headless 测试、文档口径同步）。

---

## 4. Q3 · 「渲染器注册表」术语

### 4.1 结论

**术语会被误读，建议改名。** v3 一边说「不承担渲染层职责」（`L14`、`L179`），一边在 D8 表写「渲染器注册表（宿主可覆盖消息/工具/artifact 渲染）」（`L402`），并在 L4（`L199`）、L4 准入（`L273`）、G4（`L473`）、M2 路线图（`L562`）重复「渲染器」。对熟悉 Avalonia 的读者，`Renderer` 直接指向 Avalonia 的 rendering/compositor；对外部使用者，容易误解为 Arc 拥有一层渲染管线。

### 4.2 改名方案

| 旧（v3/C） | 新（建议） | 说明 |
| --- | --- | --- |
| 渲染器注册表 | **条目视图注册表**（`ArcAgentItemViewRegistry`） | 明确是「条目 → DataTemplate/视图」的映射 |
| 消息/工具/artifact 渲染 | **消息/工具/artifact 视图**（Views） | 只做视图选择与覆盖，不做绘制 |
| `IArcAgentRendererRegistry`（C `L605-624`） | `IArcAgentItemViewRegistry` + `ArcAgentItemTemplateSelector : DataTemplateSelector` | 与 Avalonia 原生模板机制对齐 |
| 「渲染器」在 L4/§2.1/§2.4/G4/§5 的所有出现 | 统一替换 | 一次全文替换 + 术语表新增词条 |

说明：Avalonia 的 `DataTemplateSelector` + `DataTemplate` 就是「条目 → 视图」的原生机制；注册表只是「按 kind 查模板/工厂」的薄封装，**不含任何绘制/渲染实现**。

### 4.3 人日

**+0.5**（纯文档/命名；代码尚未实现，改名无迁移成本；若 C 报告后续修订，同批替换）。

---

## 5. Q4 · 里程碑可判定性

### 5.1 M2 门槛的问题

原文（`L488`）：「③ 10k 事件流式渲染不卡（护栏）」。问题：

1. 没有**测法**（帧时间？分配？内存？CPU？）、没有**阈值**、没有**基线机**、没有**测试入口**；
2. 没有说明在 headless 还是桌面跑（headless 没有真实帧时间）；
3. 没有说明「10k 事件」是预载还是追加、chunk 速率多少、持续多久；
4. 没有与 G3c（数据驱动列表）的依赖对齐：如果 C3 延期，M2 门槛不可达。

### 5.2 建议的可执行度量（M2 gate 替换文本）

| 指标 | 阈值 | 测量方式 | CI / 手工 |
| --- | --- | --- | --- |
| **全量重建次数** | `== 0`（流式期间） | `ArcAgentTranscript` 暴露 `internal int FullRebuildCount`（`InternalsVisibleTo` 测试）；每 delta 断言不增 | ✅ Headless CI |
| **可见行数** | ≤ `ceil(viewportHeight / rowHeight) + 2`（如 ≤ 27 行 @ 24px 行高/600px 视口） | 反射/visual tree 计数 `ItemsControl` 已实现容器数；断言远离 10k | ✅ Headless CI |
| **分配** | 每 1,000 delta ≤ 2 MB；10,000 事件全程 ≤ 20 MB | `GC.GetAllocatedBytesForCurrentThread()` 在 UI 线程 delta 循环前后取差 | ✅ Headless CI |
| **GC** | gen2 收集 `== 0`；gen0 ≤ 基线 + 50 | `GC.CollectionCount(0/2)` | ✅ Headless CI |
| **内存** | 10k 事件 + 强制 GC 后，托管堆增量 ≤ 50 MB；两次运行差异 ≤ 5% | `GC.GetTotalMemory(true)`；基线在 M0 记录 | ✅ Headless CI |
| **帧时间** | p95 ≤ 16.7 ms，p99 ≤ 33 ms；单帧 > 50 ms 记失败 | 桌面 `TopLevel.RequestAnimationFrame` 时间戳差；录制 60 s | ⚠️ 桌面手工（记录机型） |
| **UI 线程占用** | ≤ 单核 25%（60 s 平均） | `Process.TotalProcessorTime` 差 / 时长 | ⚠️ 桌面手工 |
| **输入响应** | 流式期间滚动/点击无 >50 ms 卡顿；贴底不抖 | 每 100 ms 注入一次滚动，记录 Dispatcher 回调延迟 | ⚠️ 桌面手工 |
| **事件速率** | 预载 10,000 + 追加 60 chunks/s × 60 s（≥3,600 delta） | 压测夹具（见 §5.3） | 两者共用夹具 |

> 建议 M2 门槛改写为：
> **「Agent 压测页：预载 10,000 事件 + 60 chunks/s 追加 60 s；headless 断言 0 次全量重建、可见行 ≤ 2×视口、分配/GC/内存达上表阈值；桌面记录 p95/p99 帧时间，p95 ≤16.7 ms、p99 ≤33 ms。」**

### 5.3 压测夹具设计（新增，不依赖 L3 页面）

| 组件 | 位置建议 | 说明 |
| --- | --- | --- |
| `AgentStressPage`（场景页，非 catalog 条目） | `Arc.Avalonia.Docs`（D2 后）或 Gallery `Pages/` | 10,000 事件预载 + 60/s 模拟推进；按钮切换「空闲/流式/停止」 |
| `AgentStressRunner`（无 UI） | `tests/Arc.Avalonia.Tests` 或 `Arc.Avalonia.Agent.Tests` | 纯模型 + headless UI 的自动化 runner，输出 JSON 指标 |
| `--stress-agent` CLI 开关 | Gallery | 桌面手工跑 + 输出 `artifacts/agent-stress.json`（帧时间/CPU） |
| 基线机记录 | `docs/perf/agent-baseline.md` | CPU/GPU/OS/Avalonia 版本，M0 冻结 |

> 依赖提醒：M2 gate ③ 需要「headless 测试夹具」；但 G3f（测试基线）在 M3。建议把「Agent 压测夹具的最小版本」提前到 G4/G0（+3 人日，已计入 C-R-03），否则 M2 只能手工截图，不能过门槛。

### 5.4 M3「Agent 完整顶层」验收清单（替换 `L489` 第④条）

**A. API（必须存在，编译 + 公共 API 快照）**

| # | 接口/类型 | 验收 |
| --- | --- | --- |
| A1 | `IArcAgentWorkspace`（多会话路由 + 跨 run 队列） | 单测：2 会话并存、事件按 RunId 路由、orphan 缓冲 |
| A2 | `IArcAgentSession`（`Timeline` + `ArcApplyResult` + `RunRef`） | 单测：乱序/重复/未知 run 事件被正确拒绝/缓冲 |
| A3 | 事件模型（message/reasoning/tool/approval/question/artifact/subagent/lifecycle/custom；含 `RunKind`/`ParentRunId`） | 每类事件有 replay 测试；`Sequence` 单调校验 |
| A4 | `IArcAgentTransport` + 命令出口（`IArcAgentCommandSink` 归属 UI；宿主命令处理器独立命名） | 宿主适配示例（可编译 sample，不在库内） |
| A5 | `IArcAgentItemViewRegistry` + `ArcAgentItemTemplateSelector` | 注册自定义 kind → 渲染自定义控件；未注册回退内置 |
| A6 | `ArcApprovalQueue`/`ArcAttentionQueue`/`ArcArtifactIndex`（Workspace 级） | 跨 run 串行/去重/排序/过期测试 |
| A7 | 无宿主 SampleSession/SampleWorkspace | 无 `Transport` 时四态可渲染 |

**B. 控件（必须 public、有 XML 文档、有 Theme/模板策略）**

| 档 | 控件 | 数量 |
| --- | --- | --- |
| 复用现有 P0 叶子 | `ArcAgentTranscript`、`ArcRunControlBar`、`ArcToolApprovalSheet`、`ArcAgentAskCard`、`ArcBackgroundRunDock`、`ArcAgentAttentionInbox` | 6 |
| 顶层 MVP（M2） | `ArcAgentSurface`、`ArcAgentComposer`、`ArcAgentStreamingText`、`ArcAgentToolCallCard`、`ArcAgentApprovalTray` | 5 |
| 完整（M3） | `ArcAgentSubAgentBoard`、`ArcAgentArtifactPreview`、`ArcAgentDiffReview`、`ArcAgentRunHistory`、`ArcAgentTraceViewer` | 5 |
| 治理 UI（M3，06 的 14 项 P1） | permission-scope-picker、diff-review、agent-handoff-banner、agent-budget-bar、checkpoint-restore、local-model-status、memory-review-queue、desktop-agent-settings、notification-routing、agent-audit-log、connector-health、offline-action-queue、trust-rules-manager、voice-session-bar | 14 项（component/special/block 混合；`diff-review` 与核心 `ArcAgentDiffReview` 同一交付，计一次） |

**C. 页面（Gallery/Docs，场景页不占 294 目录）**

| 页面 | 阶段 | 必须展示的状态 |
| --- | --- | --- |
| Agent surface（宿主无关，单会话） | M2 | 空/加载/错误/内容四态 |
| Streaming & interruption | M2 | 流式/停止/暂停/改向/接管 |
| Approval queue | M2 | 3 条跨 run 审批排队/过期/拒绝/范围 |
| Multi-session & background runs | M3 | 前台 + 后台 dock + 跨 run 收件箱同屏 |
| Sub-agent board | M3 | lead + ≥3 lanes + handoff |
| Artifact & diff | M3 | artifact 索引 + diff 审查 + 打开/复制 |
| Run history & audit | M3 | 历史列表/重放/审计（06 P1/P2） |

> 预期页面数：M2 `315 + 3 = 318`；M3 `315 + 7 = 322`（294 目录不变，audit items/docs/pages 仍为 294/294/294）；新增页必须过 `--audit`、`--smoke`、双主题 `--shot`。

**D. 测试与门禁（必须可执行）**

1. reducer replay golden tests（事件序列 → 视图状态，逐字段）；
2. 多 run 交错矩阵（approve→stop、approve→expire、deny→retry、前台/后台同时审批、重复事件重放）；
3. 压测门禁（§5.2 表）；
4. Host 边界 analyzer/引用扫描（C-R-04）；
5. 公共 API 快照（PublicApiAnalyzers）；
6. `--audit` 294/294、`--smoke` failures=0、双主题 shots 无裁切；
7. `dotnet pack` 包内无 Pages/Docs/Demo；Agent 包依赖白名单通过。

**E. 文档/示例（必须存在）**

1. 每个新控件的中文页 + When/When not/Keyboard/Motion/Implementation notes；
2. 宿主接入指南（实现 `IArcAgentTransport` 的最小示例，放 `samples/` 且可编译）；
3. Agent 事件 schema 文档（版本 + `ArcCustomEvent` 扩展）；
4. `THIRD-PARTY-NOTICES.md`（见 C-R-09）。

### 5.5 人日

**+2**（M2 压测夹具已计 C-R-03 +3；此处为 M3 验收清单的自动化检查脚本 + 页面验收清单 + golden/交错测试的最小骨架）。

---

## 6. Q5 · 遗漏核对

### 6.1 Host 边界如何 CI 化（C-R-04）

**问题**：v3 §6 R2 说「评审拦截 `HttpClient`/`File`/`Dispatcher` 业务逻辑」（`L583`），§8 的 6 类门禁（`L628-634`）没有 Agent 边界项；「评审」不是可执行门禁。D8 的「无 I/O」只靠人。

**建议 CI 方案（可直接落 G0/G4）**：

| 门禁 | 实现 | 失败条件 |
| --- | --- | --- |
| **Banned API** | `Arc.Avalonia.Agent.csproj` 引 `Microsoft.CodeAnalysis.BannedApiAnalyzers` + `BannedSymbols.txt`，`WarningsAsErrors`；analyzer 包标记 `PrivateAssets=all`，作为 build-only 白名单，**不计入** R3 的运行时 `PackageReference` 检查 | 出现禁止符号即构建失败 |
| 禁止符号（模型/运行时层） | `T:System.Net.Http.HttpClient`、`T:System.Net.Http.HttpRequestMessage`、`T:System.IO.File`、`T:System.IO.Directory`、`T:System.IO.FileStream`、`T:System.Diagnostics.Process`、`T:System.Timers.Timer`、`T:System.Threading.Timer`、`T:Avalonia.Threading.DispatcherTimer`、`T:Avalonia.Controls.TrayIcon`、`T:Avalonia.Platform.Storage.IStorageProvider`、`T:Avalonia.Platform.Storage.IStorageFile` 等（按需列举） | 同上 |
| **允许例外** | 推荐不设例外：倒计时由宿主 `ExpiresAt`/tick 驱动；若确需控件内展示时钟，必须 `[SuppressMessage]` + 注释理由 + 只读 `ExpiresAt`，并在 PR 中单列 | 未豁免即失败 |
| **引用白名单** | 架构测试解析 `project.assets.json` + `Assembly.GetReferencedAssemblies()`：只允许 `Avalonia*`、`System.*`、`Arc.Avalonia.Foundation`、`Arc.Avalonia.Controls.*`（显式 allowlist）；禁止 `Arc.Avalonia.Demo`/`Gallery`/`Docs` | 出现非白名单引用即失败 |
| **无宿主渲染** | `--shot --page agent` / `--smoke` 断言 `Transport == null` 仍可渲染；测试断言 Agent 包不引用宿主 sample | 失败即失败 |
| **PR 模板** | 「D8 边界检查」勾选项：reducer 无 I/O、事件 schema 变更记录、无持久化 | 人工但不漏 |

> 建议落到 `tools/check-agent-boundary.ps1` + `BannedSymbols.txt`，并在 G0 的 16 人日里**明确列出 Agent 项**（当前 G0 只写「依赖白名单/分层门禁」）。**人日 +2**。

### 6.2 License / 第三方声明（C-R-09）

| 项 | 现状 | 建议 |
| --- | --- | --- |
| D10 许可清单（`L439`） | 覆盖 Arc UI MIT / Lucide ISC / Inter OFL | 补 `Avalonia`（MIT，含 Fluent/Fonts 包）、`SkiaSharp`/`HarfBuzzSharp`（MIT，传递依赖）、`.NET`（MIT） |
| AG-UI | v3 D8/`L421` 只作为业界对照，C 报告 §3.4 引用协议语义 | 明确「**只做事件形状/语义对齐，不引入 `@ag-ui/*` 或 .NET AG-UI SDK**」；AG-UI 仓库为 **MIT**（本评审已核对 `ag-ui-protocol/ag-ui` LICENSE）；如事件名/状态机与规范一致，在 `THIRD-PARTY-NOTICES.md` 注「参考 AG-UI 1.0 Schema（MIT）」 |
| 交付物 | 无 `THIRD-PARTY-NOTICES.md`/`NOTICE` 计划（D10 只说随包 LICENSE） | G2/G0 增加 `THIRD-PARTY-NOTICES.md` + `dotnet pack` 内容断言（NOTICE 在包内） |
| 风险登记册 | R9 只提「Arc UI/Lucide/Inter」 | 补 Avalonia/SkiaSharp/AG-UI 项 |

**人日 +0.5**。

### 6.3 G4/G5 迁移遗漏（C-R-08）

| # | 遗漏 | 证据 | 影响 | 建议 |
| --- | --- | --- | --- | --- |
| 1 | **AgentUI 6 控件的改名/合并未列 WBS** | v3 `§2.3` 只列 `Arc.Avalonia.Agent`（`L243`），未列 `Arc.Avalonia.AgentUI`；源码仍有 12 个 `namespace Arc.Avalonia.AgentUI.*` 文件、6 个页面；Gallery 引用 `App.axaml:21`、`Gallery.csproj:31`、`ArcUI.slnx` | 若合并，命名空间/avares URI/工程引用/文档路径变更；若保留，包拓扑与 §2.3 不一致 | G4 明确「AgentUI → Agent 合并/改名」子项：12 文件命名空间 + 主题 avares + App.axaml + Gallery csproj + slnx + 6 页归属 + 回归（**2.5 人日**）；或保留为叶子包并补进 §2.3（0.5 人日） |
| 2 | **LifeToday 迁移未进 WBS** | D9 `L431` 说移 `src/Scenarios/`，但 §2.1 L3/§2.3 包拓扑/G 表均无 Scenarios；Gallery 引用 `App.axaml:22`、`Gallery.csproj:32` | 迁移后无包归属、无验收 | 增加 `Arc.Avalonia.Scenarios.LifeToday`（可选包，不进元包）子项：工程移动 + 7 控件/7 页归属 + 引用更新 + 回归（**1.5 人日**） |
| 3 | **13 个场景页出库归属** | 当前 P0 场景页 = 6 AgentUI + 7 LifeToday（`src/Agent/**/Pages`，均 `: ArcPage`）；D2 要求所有 Pages 出库（`L307-315`），G2 的「268 文件」未说明是否含这 13 页 | 若含：G2 已计价；若不含：Agent 包会带 Pages（R5 失败） | G2 文件清单显式补 13 页（或说明已含）；Agent 包只留控件/模型 |
| 4 | **Blocks.AI 6 页重写已含但目标未定** | G4 `L473`「重写 14」；M3 `L572`「6 页 Blocks.AI 重写」；D9 `L430` 说「重写为顶层示例」 | 若重写进 Agent 包 → 违反 D2/R5；若留 Blocks.AI → 与 D9 描述不符 | 明确重写在 `Arc.Avalonia.Docs`（场景页）或 `samples/`；Blocks.AI 原 18 个 catalog 条目保留为示例；在 G4 子项中写清（**1 人日**） |
| 5 | **AgentUI 6 页/6 控件的文档与截图基线** | `docs/07` 文件索引已过期；迁移后 audit/smoke/shots 需重跑 6 页×2 主题 | 基线漂移 | G5 明确回归 6 页（含在 8 人日 Gallery 自举内，建议命名） |

**人日：+4**（AgentUI 合并 2.5 + LifeToday 1.5；Blocks 重写目标 0.5 单列在 C-R-11；G2 13 页若已含则不额外计）。

### 6.4 G4「完整 104」与 C 报告 135 的口径问题（C-R-10）

| 项 | 值 | 证据 |
| --- | --- | --- |
| C 报告 S/M/L | 46 / 104 / **135** | C §5.4 `L46`、`L836` |
| v3 §0.2 | 「46（MVP）–**135（全量）**」 | v3 `L49` |
| v3 G4 | 「完整 **104**」（范围 46–135） | v3 `L473` |
| v3 M3 验收 | 「Agent **完整顶层**（子 agent/artifact/治理 UI）」 | v3 `L489` |
| 差异来源 | G4 的 104 = C 的 **M 档**（含 W8 35、W4 10、W7 14），缺 C 的 **L 档** W9 7 项（23）+ W7 差 8 | C `L826-836` |

**两种修法（二选一，不能含糊）**：

- **A. 按 C 的 L 档（135）**：G4 104→135，M3 357→388，再把 C-R-01/02/03/04 的 ≈25 加上 → **M3 ≈413**；这与「M3 完整」的字面一致，但必须确认预算。
- **B. 明确 M3 = P0+P1 完整（不含 06 长尾 7 项）**：保留 104，把 `L489` 的「完整」改为「P0+P1 完整」；W9 7 项（run trace/model routing/run history/autonomy/guardrail/hotkey/spoken）明确列入 post-1.0，同时 M3 的验收清单（§5.4）不再声称覆盖它们。

> 推荐 **B + 在 §7 不做/后置清单列出 W9**：既不虚报 M3，也避免 357 变 413 的排期冲击；但若产品目标要求「完整 Agent 顶层」包含长尾，则应按 A 重估。

### 6.5 其它遗漏

| # | 项 | 证据 | 建议 | 人日 |
| --- | --- | --- | --- | --- |
| 1 | 风险登记册缺「多 run 审批并发」 | C `L954`（R3）明确「后台 run 与前台 run 同时请求」；v3 §6 只有 R2/R3 两条 Agent 风险 | 新增风险 R11，缓解指向 `IArcAgentWorkspace.Approvals` 全局串行 | 0 |
| 2 | 风险登记册缺「AgentUI 迁移导致 315 页基线漂移」 | R8 只讲文档平台，未讲 6 页迁移 | 新增或扩写 R8 | 0 |
| 3 | 元包包含 Agent 与 M2 回滚承诺矛盾 | §2.3 `L244`：`Arc.Avalonia` 元包 = Foundation+Theme+11 Controls+**Agent**；M2 回滚点 `L565`：Agent 独立成包、不通过不影响 L1–L2 发版 | 二选一：元包不含 Agent（推荐，Agent 单独包）；或删掉回滚承诺 | +0.5 |
| 4 | §7「不做」未含「不做合并节拍/多会话持久化」 | `L595-604` | 补两条，与 Q2/Q1 呼应 | 0 |
| 5 | 基线数字内部不一致 | `L66` 328 页/298 docs vs `L71` pages=294/smoke=315；当前树静态复算 315 页/294 docs；`L61-62` 774 文件/182,621 行与当前树（763/206,968 行）口径不一致 | M0 冻结基线时统一用 `--audit`/`--smoke` 实测数字 + 注明口径（是否含 Pages/Docs/axaml） | 0 |

---

## 7. 人日增量与里程碑修订

### 7.1 增量子账

| Finding | 子项 | 人日 | 归属工作包 |
| --- | --- | ---: | --- |
| C-R-01 | Workspace + 跨 run 队列 + 多 run 测试 | 8 | G4（MVP 4 / 完整 4） |
| C-R-02 | Dispatcher 批次合并 + `IUiDispatcher` + 文档 | 1.5 | G4 |
| C-R-03 | 压测夹具 + headless 结构断言 + 桌面基线 | 3 | G4（2）/ G0（1） |
| C-R-04 | BannedApi + 引用白名单 + 无宿主渲染检查 | 2 | G0 |
| C-R-05 | 术语改名（文档/API 名） | 0.5 | G0/G4 |
| C-R-06 | Timeline/ApplyResult/命令归属 API 修正 | 2 | G4 |
| C-R-07 | M3 验收清单 + 检查脚本 + golden/交错测试骨架 | 2 | G4/G5 |
| C-R-08 | AgentUI 合并 + LifeToday 迁移 + 13 页归属 | 4 | G4（2.5）/ G5（1.5） |
| C-R-09 | THIRD-PARTY-NOTICES + pack 断言 | 0.5 | G2 |
| C-R-11 | Blocks.AI 6 页重写目标与验收 | 0.5 | G4（与 C-R-08 不同对象：C-R-08 管 AgentUI/LifeToday 迁移，C-R-11 管 Blocks 重写目标） |
| C-R-13 | 元包/回滚决策 | 0.5 | G0 |
| P2-03 | 事件 `RunKind`/`ParentRunId`/`EventId` | 0.5 | 并入 C-R-01 |
| **合计** | | **25** | G0 +3、G4 +19、G5 +3 |

### 7.2 修订后的估算（中性）

| 里程碑 | 原 | 修订（不含 C-R-10 的 W9） | 说明 |
| --- | ---: | ---: | --- |
| M1 | 71 | 71 | 不涉及 Agent |
| M2 | 225 | **≈237**（+12：Workspace MVP 4 + 批处理 1.5 + 压测 2 + API 2 + 术语 0.5 + 迁移 1 + 元包/边界 1） | 若 M2 只做最小集，可压到 +9 |
| M3 | 357 | **≈382**（+25） | 不含 C-R-10 的 W9 |
| M3（若按 C 的 L 档 135 计） | — | **≈413**（+31） | 需 Lead 明确 M3 范围 |

> 区间仍可沿用 ±30%：M3 约 **382（300–500）**；若纳入 W9 则 **413（325–540）**。

### 7.3 若预算不变（维持 357）的建议裁剪

1. **C-R-01 的完整档（+4）延后到 post-1.0**，MVP 保留 +4（Workspace + 单会话兼容），M2 先交付「一前台 + 一后台」双 run；
2. **C-R-07 的 M3 清单只做核心 11 控件 + 7 页**，治理 UI 的 14 项中只做 6 个高频项（diff/permission/handoff/budget/audit/memory）；
3. **C-R-08 的 LifeToday 迁移延后**（保留 `src/Agent/` 仅改文档定位），省 1.5；
4. **W9 明确 post-1.0**（C-R-10-B）。
   按此裁剪，M3 约 **374**（357 + 保留的 P0 ≈14.5 + 必需的 P1 ≈2，延后 P1 ≈8.5）；要真正维持 357，还需同时砍 G3e（24）或治理 UI 6 项（≈12），并同步改 §0.2/M3 的「完整」措辞。

### 7.4 给 Lead 的一句话

> **建议新增 ≈25 人日（P0 14.5），并把 M2 从 225 调到 ≈237、M3 从 357 调到 ≈382；若坚持 357，则把 M3 的「完整」改名为「P0+P1 完整」并把 06 长尾 7 项列入 post-1.0。**

---

## 8. 主文档修订清单（逐节）

| 节/行 | 现状 | 建议修订 | 优先级 |
| --- | --- | --- | --- |
| `L14` | 「不做渲染层」 | 保留；在 §4.1 例外中说明「Dispatcher 批次合并不算渲染层」 | 0（保留） |
| `L49` | 「46（MVP）–135（全量）」 | 保留；补「M3 采用哪一档」 | P1 |
| `L93` / `L148` / `L199` / `L273` | 「渲染器」 | 改「条目视图注册表/视图」 | P1 |
| `L244` | 元包含 Agent | 改为「元包不含 Agent；Agent 独立包」（推荐）或删 M2 回滚承诺 | P1 |
| `L307-315`（D2） | 页面出库 | 补 13 个 Agent/Life 场景页的归属 | P1 |
| `L396-403`（D8 表） | 单会话 reducer / 16 ms 合并 / 渲染器 | 补 Workspace；改 UI 批次合并；改视图注册表 | **P0** |
| `L405-419`（D8 API） | 单会话、分离列表、命令归属写反 | 替换为 §2.5 的 API；修正 `IArcAgentCommandSink`/`IArcAgentServices` | **P0** |
| `L421` | 业界对齐 | 保留；补「AG-UI 仅语义参考、不引 SDK」 | P1 |
| `L430-431`（D9） | Blocks 6 页「顶层示例」/LifeToday 迁移 | 明确重写目标为 Docs/samples；LifeToday 包名与 WBS | P1 |
| `L456` / §7 `L595-604` | 不做清单 | 增「不自建合并节拍/帧时钟」「不做多会话持久化/事件日志存储」 | P2 |
| `L473`（G4） | MVP 46 / 完整 104 | 本行只列 C-R-01：MVP 50 / 完整 112；G4 全部修订（+19）见 §7.1 | **P0/P1** |
| `L474`（G5） | 迁移 20 | 补 LifeToday/许可/压测基线（+3）；AgentUI 合并放 G4 | P1 |
| `L488`（M2 门槛） | 「10k 不卡」 | 替换为 §5.2 的可执行度量 | **P0** |
| `L489`（M3 门槛） | 「Agent 完整顶层」 | 替换为 §5.4 清单 + 判定方式 | P1 |
| `L578-591`（§6） | 风险 R1–R10 | 补 R11 多 run 审批并发、R12 迁移基线漂移；R2 缓解改 CI；扩 R9 许可 | P1 |
| `L628-634`（§8 门禁） | 6 类门禁 | 增「Agent 边界 BannedApi/引用白名单/无宿主渲染」 | **P0** |
| `L643` | 「渲染层自研代码 0」 | 保留，补 Dispatcher 合并例外 | P2 |
| `L648` | 宿主 3 接口 | 改为「Transport（+可选 UiServices）；UI 实现 Workspace/Session/CommandSink/ViewRegistry」 | **P0** |
| `§0.3` `L57-72` | 328/298/774/182,621 | M0 重新冻结，注明统计口径 | P2 |

---

## 9. 复核与回归建议（供 Lead 执行，本评审不运行）

1. **M0 基线重算**：`--audit`/`--smoke`/`--list` + 静态复算（文件/行/页面/docs），把 §0.3 的数字改为实测并记录口径；
2. **主文档修订后复核**：检查 D8 API 块能被 C 报告 §4.3 支撑（或在 C 报告下一版同步 `Workspace`）；检查「渲染」/「帧」关键词全文一致；
3. **实现前的接口评审**：`IArcAgentWorkspace`/`IArcAgentSession`/`IArcAgentTransport`/`IArcAgentCommandSink` 的归属表（UI vs 宿主）需一次定稿，避免实现期反复；
4. **门禁先红后绿**：先让 `BannedApi`/引用白名单在空 Agent 工程上报红，再加入允许代码；
5. **压测夹具先于 transcript 实现**：否则 M2 门槛无法客观判定；
6. **不动 294 目录**：新增 Agent 页面用 `ArcPage`（非 `ArcItemPage`），避免 `--audit` 基线变化；若新增 catalog 条目需同批补 Docs JSON；
7. **回归门槛**：`--audit` 294/294、`--smoke` pageShows=315（M2 预计 318）、双主题 shots、`dotnet pack` 无 Pages/Docs/Demo。

---

## 附录 A · 证据索引

| Finding | 证据（主文档行号 / 源码 / C 报告） |
| --- | --- |
| C-R-01 | v3 `L398`（RunId+Sequence）、`L408-414`（单会话接口）、`L418`（视图注册表）、`L473`（G4 明细）、C `L394-414`、C `L699-713`、C `L954`；`ArcBackgroundRunDock.cs:40-48`、`ArcAgentAttentionInbox.cs:66-92` |
| C-R-02 | v3 `L400` vs `L14`、`L31`、`L179`、`L287`、`L456`、`L522`、`L584`、`L643`；C `L431-437`、`L953` |
| C-R-03 | v3 `L488`（M2 门槛）、`L567-572`（G3f 在 M3）、C `L953`（压力页要求） |
| C-R-04 | v3 `L583`（R2「评审拦截」）、`L628-634`（门禁列表）、D8 `L396-403`（无 I/O） |
| C-R-05 | v3 `L93`、`L148`、`L199`、`L273`、`L402`、`L473`、`L562` vs `L14`、`L179` |
| C-R-06 | v3 `L408-419` vs C `L394-414`（统一 Items）、C `L640-650`（命令方向）、v3 `L648`（`IArcAgentServices`） |
| C-R-07 | v3 `L489`、`L567-572`；C §5.2/§5.3（控件/页面清单） |
| C-R-08 | v3 `L466`（G2 268 文件）、`L473`、`L474`、`L430-431`；源码 `src/Agent/Arc.Avalonia.AgentUI/Controls/*.cs`（12 文件命名空间）、`Pages/*.cs`（6 页）、`src/Agent/Arc.Avalonia.LifeToday`（7+7）、`Gallery/App.axaml:21-22`、`Gallery.csproj:31-32`、`ArcUI.slnx` |
| C-R-09 | v3 `L439`（许可清单）、`L590`（R9）、`L421`（AG-UI 对照）；AG-UI LICENSE（MIT） |
| C-R-10 | v3 `L49`、`L473`、`L489` vs C `L46`、`L826-836` |
| C-R-11 | v3 `L430` vs `L307-315`（D2）、`L228`（R5）、`L466`（G2） |
| C-R-12 | v3 `L578-591` vs C `L954` |
| C-R-13 | v3 `L244` vs `L565` |
| P2-01 | v3 `L57-72`（328/298/774/182,621）vs v3 `L71`（pages=294/smoke=315）；当前树静态复算 315 页/294 docs |
| P2-02 | v3 `L595-604` |
| P2-03 | v3 `L398`、`L408-414`；C `L305-363`（事件模型） |

## 附录 B · 评审局限

1. 未运行构建/audit/smoke/shots/pack，所有「通过/失败」结论均来自主文档与既有报告；
2. 未实现任何接口草案，签名仅用于表达职责边界；
3. 主文档基线数字与当前工作树存在差异（P2-01），本评审未重算全部基线，只标注并要求 M0 重算；
4. 未评估 L2（G3a–G3f）与 A/B/D 报告；若 L2 的 G3c 延期，C-R-02/C-R-03 的 M2 门槛需顺延；
5. 外部对标（AG-UI/Vercel/assistant-ui/CopilotKit）只用于判断「表现层运行时」边界，不构成规范；
6. 本文件不改主文档/源码，C 报告的「16 ms」措辞也需在主文档修订时同步（见 C-R-02）。

## 附录 C · 外部参考

| 来源 | 用途 | License / 备注 |
| --- | --- | --- |
| `ag-ui-protocol/ag-ui` LICENSE | 确认 AG-UI 为 **MIT**；只做语义参考不引 SDK | MIT（2025） |
| `docs.ag-ui.com/spec/1.0/schema` | 事件族划分（Text/Tool/State/Reasoning/Subagent/Activity） | 协议文档 |
| Vercel AI SDK UI stream protocol | 前端状态 + 流式协议分层 | 参考 |
| assistant-ui AG-UI runtime | `ExternalStoreRuntime` 承担事件解析/消息重建 | 参考 |
| CopilotKit architecture | frontend / runtime / agent 三层 | 参考 |

> 抓取时间：2026-11-05（Asia/Shanghai）。外部内容仅作对标参考，不代表本项目规范；结论可由 v3 行号 + C 报告 + 源码独立支撑。
