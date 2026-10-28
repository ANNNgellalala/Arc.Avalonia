# 04 · 独立事实核查报告（QA Review）

> **范围说明**：本报告审查的是 **v1（UI + 运行时）** 版本（2026-10-26 基线）。2026-10-28 已按用户方向收敛为**纯 UI（v2）**；v2 复核见 [`05-ui-scope-review.zh.md`](05-ui-scope-review.zh.md)。本文件保留为历史基线，其中的"运行时服务/参考应用"等结论不适用于 v2。

> **审查对象**：`docs/06-agent-life-tools-extension.zh.md`、`docs/arc-extension-catalog.json`、`docs/agent-life-tools/01~03` 与 README 的 docs/06 条目。
> **审查角色**：独立 QA（qa-reviewer），只读核查，不改主报告/目录/附录/代码/README。
> **审查基线（已与 Lead 确认冻结）**：
> - `docs/06-agent-life-tools-extension.zh.md`：866 行 / 76,999 B / mtime 2026-10-26 23:02:47 / **SHA256 `2023963b2cae566ef760db839defa29b1a95dfd023f10ffd5da2a2994b7fa54d`**
> - `docs/arc-extension-catalog.json`：94,536 B / mtime 2026-10-26 22:50:14 / **SHA256 `721e3e7b01a042a80d3bc16b6e48d9c2fdcbd20e2d16c7f63006f5daecd23574`**
> - 本文所有行号均以该基线为准；Lead 修复后数字/行号可能变化，修复项复核时以"当前文件 + 本报告的问题编号"定位。
> - **复核基线（2026-10-26 23:10 修复后）**：`06` 883 行 / 80,616 B / SHA256 `342035fe4a8afd81d76d932fab63ad79d40679b83d08de1b2a8fc4cd79a3f937`；`arc-extension-catalog.json` 94,960 B / SHA256 `7f78553e52b3d9492cf2f50191b7226e9427ab955d76e3988adb85f7bd2ba787`；`README.md` SHA256 `81b881f231a3305fbc3dc69271874b001f3bc69600e4b1de2e7883da8c524b54`。复核结果见 §⑦。
> **审查方式**：只读 PowerShell/Python 静态复算（脚本见 `.recon/qa_*.py`），**未构建、未运行 `--audit`/`--smoke`**；`--audit` 结论引用工作树内既有日志 `.recon/audit-final4.log`（2026-10-25 01:59）并做了同算法静态复算。
> **审查时间**：2026-10-26 23:00–23:15（Asia/Shanghai）。

---

## ① 总体结论

**结论：有条件可交付。** 扩展目录（57 项 / 39 场景 / 10 服务）与三份附录的**数据层是可信的**：核心数字全部复算通过，slug/场景/composes/分类/项目/优先级无一致性错误；主报告 §0.2–§4 的事实基础也基本可靠。当前版本有 **3 个 P1 问题**（排期承诺、§4.5 去重覆盖面、场景交叉引用），建议按本报告修复后再对外交付；修复后无需重跑构建，只需按 §⑥ 清单做一轮快速复核。**第 2 轮复核（§⑦）结果：P1 全部关闭、回归通过、最终结论改为「可交付」；仅余 1 个 P2 文档级残留（§4.5 两处矩阵引用）与 2 个 P3 建议。**

- **FAIL 计数**：9 个核查项中 **3 项 FAIL**（第 4、7、8 项，见 §②）；其余 6 项 PASS（含 4 个带非阻塞 caveat）。
- **问题计数**：**P0 0 / P1 3 / P2 14 / P3 2**。
- **最严重 3 项**：
  1. **P1-1 排期不可行**：290 人日（235 UI + 55 服务）按 2–3 人、14–17 周无法完成（2–3 人容量仅 140–255 人日；即使只算 UI，235 人日也需要约 3.4 FTE / ≥19 周）。这是本报告唯一会直接误导投资与排期的错误。
  2. **P1-2 §4.5 声称"每一项都给出边界判定"，实际只有 19/57 行**，且表后进一步断言其余 38 项"没有对应物、不存在换名重复风险"——这与 02 §9 / 03 §12.2+§9 的复用矩阵直接矛盾；38 项（含 2 项源分析明确建议"变体/扩展优先"的项）的去重说明没有进入主报告。
  3. **P1-3 §2.3 场景交叉引用错误**：声称 Agent 域 15 个场景的完整描述见 `03-agent-gaps` §4，但 03 §4 是"桌面平台约束"，且 03 全文没有 15 个场景的逐场景描述（只有 JSON 摘要）。
- **可交付边界**：把 `arc-extension-catalog.json` 作为机器可读扩展目录、把 01/02/03 作为分析底稿，**现在即可用**；把 §6 路线图作为"排期承诺"、把 §4.5 作为"完整去重证明"，**需修复后使用**。

---

## ② 逐项 PASS/FAIL 表

| # | 核查项 | 结论 | 证据（复算/原文） |
| --- | --- | --- | --- |
| 1 | 数字一致性（294/128/55/111/219/438/236/411/166+88+40/57/14/34/9/16-9-32/25-28-4/235-72-130-33） | **PASS**（1 处非阻塞：§3.2/§4.1 的"12 项"见 P2-1） | 脚本 `.recon/qa_numbers.py`、`.recon/qa_source.py`：catalog `items=128 / special=55 / block=111`，唯一 slug 294；`shots/light=219`、`shots/dark=219`；`ItemSlug` 166 + 章节标题 88 + 别名 40 = 294，missing 0；公共类 411/407、控件 236/234、internal 100；扩展 57 项，kind 16/9/32、priority 14/34/9、asset 25/28/4、effort S8/M33/L16，人日 235（P0 72 / P1 130 / P2 33）。全部与报告一致。 |
| 2 | 引用真实性（扩展 slug ⊆ 目录；现有 slug ⊆ 294；composes 有效；相对链接可解析） | **PASS** | 57 个扩展 slug 零冲突、零重复；报告出现的 57 个扩展 slug 全部命中目录（`qa_cover.py`：`ext slugs not mentioned in report: 0`）；294 个现有 slug 全部可在 01-inventory 中找到（`catalog items not mentioned: 0`）；composes 逐项 ∈ 现有 294 ∪ 扩展 57（0 越界、0 自引用）；文档内相对链接全部解析（04 自引用在本文创建后成立），README 链接全部解析。`calendar-heatmap` 已修为 `activity-heatmap`。 |
| 3 | 目录完整性（零冲突/零重复/category/project/枚举/reviewNote/source） | **PASS** | 57 个 slug 与现有 294 零交集；内部零重复。kind/category 全部合法（component ∈ 23 类+Foundation=24；special ∈ 5 组；block ∈ 6 组+Daily life=7）；project 全部 ∈ `src/` 下 15 个真实工程；priority/effort/asset 枚举合法。`reviewNote` 恰 4 条且为 daily-brief、bill-due-list、document-vault、grocery-list；`source=lead` 恰 5 项（quick-capture-bar、virtualized-list、tray-menu、habit-tracker、task-board）。`grouping` 覆盖 57 项各一次；`summary` 各字段与逐项复算完全相等。 |
| 4 | 无重复现有能力（§4.5 边界表是否成立） | **FAIL** | §4.5 表只有 **19 行**（`06:461-481`），导语 `06:459` 写"每一项都给出最接近的现有项与关系判定"，表后 `06:483` 又写"其余 38 项……**没有对应物**……**不存在'换名重复'风险**"——两处都与实际不符（脚本 `qa_numbers.py` → `ext items NOT covered by 4.5 (38)`；这 38 项在 `02-life-gaps` §9 或 `03-agent-gaps` §12.2+§9 中都有"最接近的现有项"与分工说明，例如 `bill-due-list` vs `billing-overview`、`agent-budget-bar` vs `usage-meter`、`desktop-agent-settings` vs `settings-page`）。另 02 §9 对 `habit-streak-strip`、`reference-band-chart` 明确写"变体优先/扩展优先"（`02-life-gaps.zh.md:736-737`），但两者仍作为新目录项进入合并目录，主报告与 catalog 均无 reviewNote 解释（见 P1-2、P2-9）。 |
| 5 | docs/04 引用（EmojiPicker / 11 套动画 / 77 Render / 703 AutomationProperties） | **PASS** | 四项均与 `docs/04-design-and-performance-review.zh.md` 原文一致：§2.6（第 176-177 行）"C# 代码里 ItemsControl/DataTemplate/ItemsRepeater/VirtualizingStackPanel 使用次数为 0；XAML 侧只有 7 处 ListBox，其中仅 EmojiPicker 的行级虚拟化真正生效"；§0.2（第 26 行）"11 套独立动画驱动，0 处 CompositionTarget.Rendering"；§0.2（第 24 行）"77 个文件 override Render"；§2.7（第 189 行）"703 处 AutomationProperties"。报告 §0.2 `06:30` 已补注"审查时点 703、当前实测 722"（我方复算：.cs 703 + .xaml 19 = 722），附录 A `06:834` 同步标注，处理正确。§3.4 的一处 docs/04 转述不准确另见 P2-3。 |
| 6 | 场景一致性（24+15=39；提案数；§9.2 五链路） | **PASS** | 39 个场景 id、标题与 §2.3 表逐行一致；每个场景 `proposalSlugs` 长度与表中"提案数"完全相等（生活 98 + Agent 77 = 175，与 items 的 `scenarios` 成员总数 175 相等）；39 个场景全部至少 1 个提案；§9.2 五条链路均能映射到目录项：晨间简报（`daily-brief`/`day-agenda`/`weather-forecast`）、快速捕捉（`quick-capture-bar`）、提醒触达（`reminder-row`/`reminder-center`+`notifications`/`scheduler`）、预算预警（`budget-board`/`expense-ledger`）、计划审批（`tool-approval-sheet`/`agent-attention-inbox`/`run-control-bar`）。 |
| 7 | 路线图自洽（M0–M4 项数/人日；P0/P1 拆分） | **FAIL** | **逐项加法全部正确**：M0 1 项 8 人日；M1 15 项 50（P0 7+P1 8）；M2 7 项 31（P0 6+P1 1）；M3 17 项 89（P1 17）；M4 17 项 57（P1 8+P2 9）；合计 235；P0 14=M0 1+M1 7+M2 6；P1 34=M1 8+M2 1+M3 17+M4 8；P2 9。**但团队/周期与工作量不可行**：290 人日 vs "2–3 人 × 14–17 周"（容量 140–255 人日），见 P1-1；另有跨里程碑依赖倒置见 P2-8。 |
| 8 | 文档结构/可读性（摘要/定义/路线图/验收/表格/前后矛盾） | **FAIL** | 结构齐全（§0 摘要、§0.4/§6.1/附录 C 优先级与工作量定义、§6 路线图、§8 风险、§9 验收）；33 张表列数全部一致、4 个代码围栏成对，无渲染问题。但存在前后矛盾：§3.2/§4.1 的"12 项"与 §3.3 的 14 行不符（P2-1）；§1.1 `06:75` "0 处虚拟化"与 §0.2 `06:31` "EmojiPicker 1 处生效"自相矛盾（P2-2）；§6.1 M3/M4 的"行程/日记"归类错误（P2-7）；§1.6 `06:147` 称"本次分析实际运行了审计"与 01 §6.6 "本次未运行"矛盾（P2-12）；附录 A `06:839` 称"双列呈现"但实际未实现（P2-11）。 |
| 9 | 可执行性（P0 项目/composes/验收/新建项目说明） | **PASS** | 14 个 P0 全部有真实 `project`、非空 `composes`、≥1 场景；§5.2 给出 11 个旗舰项、§5.3 给出其余 3 个的验收要点，14/14 覆盖；Daily life 新建 `Arc.Avalonia.BlocksLife` 在 §4.7 `06:511` 说明，`virtualized-list` 落 Core 在 §4.7 `06:512` 说明，4 个 P0 Special 落 Special.AIWidgets 在 §4.7 `06:513`，参考应用 `Arc.Avalonia.ReferenceApp` 在 §7.9 `06:748` 说明；不存在"需要新建项目但正文未说明"的情况。 |

---

## ③ 问题清单（可直接执行）

> 格式：`文件:行号`｜现状 → 建议替换文本。P0 = 阻塞交付；P1 = 交付前必修；P2 = 建议同批修；P3 = 可选。

### P0（0 项）

无。

### P1（3 项，交付前必修）

#### P1-1 路线图人数/周期与 290 人日不可行

**位置**：`docs/06-agent-life-tools-extension.zh.md:52`、`:645`、`:669`、`:677`；联动 `README.md:33-34`。
**证据（复算）**：

| 里程碑 | 人日 | 周期 | 5 天/周所需 FTE |
| --- | ---: | ---: | ---: |
| M0 | 8 | 1 周 | 1.6 |
| M1 | 50 | 2.5 周 | 4.0 |
| M2 | 31 | 2 周 | 3.1 |
| M3 | 89 | 4 周 | 4.45 |
| M4 | 57 | 3 周 | 3.8 |
| UI 合计 | 235 | 14 周 | 3.4 |
| + 10 个运行时服务 | 55 | — | — |
| **总计** | **290** | **14–17 周** | **3.4–4.1（峰值 4.45）** |

2–3 人 × 14–17 周 = 140–255 人日 < 290；即使只算 235 UI，也需要约 3.4 FTE，2–3 人在 14 周内最多完成 210 人日。当前"2–3 人 / 14–17 周"的承诺不成立（若固定 2–3 人，实际约需 19–29 周）。

**建议替换文本**：

- `06:52`：把"对应 §6 的 14–17 周、2–3 人团队"改为：
  > 对应 §6 的 **3–5 人团队（平均约 3.7 FTE、峰值 5）14–17 周**；若固定 2–3 人，则约需 **19–29 周**。
- `06:645`：把"周期按 2–3 人团队估算，总计 **14–17 周**"改为：
  > 周期按 **3–5 人团队（平均约 3.7 FTE、峰值 5）**估算，总计 **14–17 周**；若固定 2–3 人，需约 **19–29 周**。
- `06:669`：把"**2–3 人**：1 人基础/Agent 交互（M0/M1 + 服务），1–2 人生活域（M2–M4），设计/验证共享"改为：
  > **3–5 人（平均约 3.7 FTE、峰值 5）**：1 人基础/Agent 交互（M0/M1 + 服务），2 人生活域（M2–M4），1 人 Agent 扩展/自动化（M4），0.5 人设计/验证（截图、a11y、场景走查）。
- `06:677`：把"2–3 人 **6–8 周**"改为"**3 人约 7 周 / 2 人约 10 周**"。
- `README.md:33-34`：把"a 14–17 week roadmap"改为"a 14–17 week roadmap for a 3–5 person team"（或按上面选定的口径同步）。

> 备选方案：若不改团队/周期，则把 §6.1 的人日按"2–3 人 × 14–17 周 = 140–255 人日"重排（需削减约 35–150 人日范围），但这会破坏 57 项/P0 14 的既定范围，不推荐。

#### P1-2 §4.5 去重声明与覆盖不符（19/57，且对未覆盖 38 项作了错误断言）

**位置**：`docs/06-agent-life-tools-extension.zh.md:459`（导语）、`:461-481`（表）、`:483`（表后断言）；关联 `02-life-gaps.zh.md:736-737,725-750`、`03-agent-gaps.zh.md:788-816`。
**证据**：
- §4.5 表 **19 行**；目录 57 项；未覆盖 **38 项**，含 `habit-streak-strip`、`reference-band-chart`、`global-hotkey-field`、`desktop-agent-settings`、`agent-budget-bar`、`notification-routing`、`connector-health`、`task-board`、`run-history-list`、`voice-session-bar`、`agent-audit-log`、`permission-scope-picker`、`diff-review` 等。
- `06:483` 写"未列入本表的其余 38 项属于现有 294 项中**没有对应物**的纯新增域……**不存在"换名重复"风险**"——该断言可被源文档直接证伪：02 §9（`02-life-gaps.zh.md:725-750`）对全部 24 个生活提案逐项列出"最相近的现有项"（如 `bill-due-list` vs `billing-overview`/`usage-billing`/`invoice-studio`、`budget-board` vs `usage-meter`/`budget-variance`、`expense-ledger` vs `wallet-card`/`budget-variance`/`sortable-data-table`）；03 §12.2（`03-agent-gaps.zh.md:788-816`，25 行）+ §9（`03-agent-gaps.zh.md:664-688`，覆盖 03 §12.2 未列的 `autonomy-dial`/`guardrail-alert`/`connector-health`）合计覆盖 28 个 Agent 提案，逐项列出"明确不重复的现有项"（如 `agent-audit-log` vs `timeline`/`data-grid`、`permission-scope-picker` vs `radio-cards`/`tag-input`、`desktop-agent-settings` vs `settings-page`、`global-hotkey-field` vs `shortcut-recorder`）。"38 项没有对应物"与这两份文件直接矛盾。
- 另 02 §9 还明确把 `habit-streak-strip` 判为"变体优先"、`reference-band-chart` 判为"扩展优先"，但两者仍作为新项进入目录且无 reviewNote（见 P2-9）。

**建议**：

1. 把 `06:459` 的"每一项都给出最接近的现有项与关系判定"改为：
   > 下表只列 **19 个最容易与现有能力重叠的项**；其余 38 项的去重依据见 `02-life-gaps` §9（24 项）与 `03-agent-gaps` §12.2（25 项）+ §9（补 3 项：`autonomy-dial`/`guardrail-alert`/`connector-health`）的复用矩阵——合计覆盖 52 个提案项，Lead 补充的 5 项见下表的 `quick-capture-bar`/`virtualized-list`/`tray-menu`/`habit-tracker`/`task-board`。
2. 把 `06:483` 整段改为：
   > 未列入上表的 38 项，其"最接近现有项/不重复理由"见 `02-life-gaps` §9 与 `03-agent-gaps` §12.2+§9 的复用矩阵；其中 `habit-streak-strip`、`reference-band-chart` 按"变体/扩展优先"处理（见 §4.5 增补行）。"不存在换名重复风险"这一结论不成立，已改为逐项对照。
3. 在表末追加以下 12 行（可直接粘贴，依据 02 §9 / 03 §12.2）：

```markdown
| `habit-streak-strip` | `activity-heatmap` / `activity-rings` | **变体优先**：先给 heatmap 加 7/30 天窗口；独立成项仅因需要独立标题/点击回填契约（02 §9） |
| `reference-band-chart` | `line-chart` / `gauge` | **扩展优先**：作为 `ArcLineChart` 的 Band/Target 属性；不新增绘制引擎，目录项只对应公开 API 文档页（02 §9） |
| `global-hotkey-field` | `shortcut-recorder` | 包裹：全局注册/冲突/平台能力探测，普通组合键录制已由 `shortcut-recorder` 覆盖（03 §12.2） |
| `desktop-agent-settings` | `settings-page` | 包裹：桌面运行时设置分区，复用 `settings-page` 骨架（03 §12.2） |
| `agent-budget-bar` | `usage-meter` / `progress` | 组合：运行时阈值与降级动作；视觉复用 `usage-meter`，新增的是 run 级预算语义（03 §12.2） |
| `notification-routing` | `notification-center` / `settings-page` | 不同对象：投递策略/打扰控制 vs 收件箱/设置页（03 §12.2） |
| `connector-health` | `mcp-servers` | 互补：MCP 管工具，本项管账号/同步/重授权（03 §12.2） |
| `task-board` | `kanban-board` / `project-board` | 组合 + 去团队化（今天/逾期/无日期维度），复用看板交互（02 §9） |
| `voice-session-bar` | `voice-orb` / `voice-recorder` | 组合：会话控制条；不是全屏 orb、也不是录音控件（03 §12.2） |
| `run-history-list` | `agent-run` / `agent-tasks` | 补"历史层"：单次 run 卡与任务队列都不提供可搜索/重放（03 §12.2） |
| `model-routing-policy` | `model-picker` | 不同对象：单次模型选择 vs 条件→档位路由策略（03 §12.2） |
| `trust-rules-manager` | `security-settings` / `agent-approvals` | 不同对象：账号安全设置 vs Always-allow 规则台账（03 §12.2） |
```

3. 可选：在 `arc-extension-catalog.json` 给 `habit-streak-strip`、`reference-band-chart` 补 `reviewNote`（Lead 需同步更新目录 SHA256 并重跑校验）：
   - `habit-streak-strip`："02 §9 判为'变体优先'；保留为目录项是为独立页面/截图与点击回填契约，实现先在 activity-heatmap 上加窗口参数。"
   - `reference-band-chart`："02 §9 判为'扩展优先'；保留为目录项是为公开 API（ArcLineChart Band/Target）提供文档页，不新增绘制引擎。"

#### P1-3 Agent 域场景描述交叉引用错误

**位置**：`docs/06-agent-life-tools-extension.zh.md:199`。
**现状**："每个场景的完整描述（用户目标、Agent 参与方式、界面元素、可复用项、缺口）见 `02-life-gaps` §3 与 `03-agent-gaps` §4。"
**核查**：02 §3 确实有 24 个生活场景的完整描述；但 `03-agent-gaps.zh.md:195` 的 §4 是"桌面平台（Avalonia 12 / .NET 10）特有的约束"，03 全文没有 15 个 Agent 场景的逐场景描述（仅 `focus-hours` 在 `03:376` 边界表出现一次）；15 个场景目前只有 `docs/agent-life-tools/03-agent-proposals.json` 的 `scenarios` 摘要（id/title/summary）。
**建议替换 `06:199`**：

> 场景是本次扩展的"验收单元"：每个新增项都必须挂到至少一个场景，场景 id 写入 `arc-extension-catalog.json` 的 `scenarios` 字段；生活域 24 个场景的完整描述（用户目标、Agent 参与方式、界面元素、可复用项、缺口）见 `02-life-gaps` §3；Agent 域 15 个场景的 `id/title/summary` 见 `03-agent-proposals.json` 的 `scenarios`，对应的生命周期/桌面约束/缺口分析见 `03-agent-gaps` §3、§4、§5（03 正文暂未展开逐场景的"界面元素/可复用项"）。

> 若希望保留"完整描述"承诺，需请 agent-ux-analyst 在 03 §4 前新增"15 个场景矩阵"（每个场景一行：用户目标/Agent 参与/界面元素/复用 slug/缺口），否则应改小措辞。

### P2（14 项，建议同批修）

| ID | 位置 | 现状 | 建议替换/动作 |
| --- | --- | --- | --- |
| P2-1 | `06:296`、`:365`、`:771` | §3.2/§4.1 写"12 项（§3.3）/12 项见 §3.3"，但 §3.3 实际 14 行、涉及 17 个现有项；§8 `:771` 又以"12 个已有组件"为校准基准。 | `:296` → "**14 组扩展现有项**（§3.3）"；`:365` → "**14 组扩展现有项**见 §3.3"；`:771` → "以 §3.3 的 14 组扩展现有项为基准校准"（若确指另一组 12 项基准，请在括号内列出清单）。 |
| P2-2 | `06:75` | "全仓 0 处 `TrayIcon` / 系统通知 / `HttpClient` / 调度 / 音频采集 / 虚拟化"与 §0.2 `06:31` "EmojiPicker 1 处生效的行级虚拟化"自相矛盾；`DispatcherTimer` 也存在 92 处（动画计时器）。 | 改为："全仓 0 处 `TrayIcon` / 平台系统通知 / `HttpClient` / 任务调度器 / 音频采集 / **显式列表虚拟化**（仅 EmojiPicker 的 `ListBox` 行级虚拟化生效；另有 92 处动画 `DispatcherTimer`）。" |
| P2-3 | `06:323` | "`docs/04` §2.6 实测全库 `ItemsControl/ItemsRepeater/VirtualizingStackPanel` 使用次数为 0"——docs/04 原文限定为"C# 代码里"，且 XAML 侧有 6 处 `ItemsControl`、7 处 `ListBox`。 | 改为："`docs/04` §2.6 实测 **C# 代码里** `ItemsControl/DataTemplate/ItemsRepeater/VirtualizingStackPanel` 使用次数为 0；XAML 侧仅 7 处 `ListBox`（其中 EmojiPicker 行级虚拟化生效）。" |
| P2-4 | `06:326` | "没有 `ItemsSource`"是绝对表述；`ArcEmojiPicker.cs:700/729/824/1078`、`ArcTimePicker.cs:204`、`ArcEmojiPicker.axaml:395` 均使用 `ItemsSource/ItemsControl`。 | 改为："所有 block 使用代码内确定性示例数据；**没有对外的 `ItemsSource` 数据契约**（仅 EmojiPicker/TimePicker 内部使用 `ItemsSource`）、持久化、迁移、同步概念。" |
| P2-5 | `06:322` | `ArcCalendar` 属性清单漏了 `DisabledDates`（实际还有 `Func<DateTime,bool>? DisabledDates`，源码 `ArcCalendar.cs:38-39,115`；01-inventory §4.1 也已列出）。 | 在清单中加入 `/DisabledDates`（并把"只暴露"改为"公开属性包括"）。 |
| P2-6 | `06:81` | Buttons 行代表项含 `hold-to-confirm`，但它在 catalog 中属 **Gestures** 分类（Gestures 行已正确列出）；Buttons 分类实际 11 项含 `copy-button`/`confirm-morph`。 | 把 `hold-to-confirm` 替换为 `copy-button`（或 `confirm-morph`）。 |
| P2-7 | `06:652-653` | M3 表写"生活 P1 16 项…（任务/习惯/账单/预算/台账/订阅/文档/购物/餐食/维护/专注/家居/天气）"漏了 `trip-itinerary`、`daily-journal`；M4 括号却写"行程/日记"。按 person-days，这两个 M 级/L 级项在 M3（M3 16 项=86+3=89；M4 57=12+24+21），不在 M4。 | `06:652` 括号改为"（任务/习惯/账单/预算/台账/订阅/文档/购物/餐食/维护/专注/家居/天气/行程/日记）"；`06:653` 删除"行程/日记..."中的这两项（保留语音/记忆/审计/连接器/离线/信任规则/桌面设置）。 |
| P2-8 | `06:601`、`:651-652`、`:502`、`:653` | 跨里程碑软依赖未在 §6.2 说明：`daily-brief`（M2）复用 `weather-forecast`（M3，P1）；`tool-approval-sheet`（M1）依赖 `trust-rules-manager`（M4，P1）；catalog 中 `tray-menu`（M1）composes `desktop-agent-settings`（M4）。 | 二选一：① 把 `weather-forecast` 挪到 M2（M2 变 34 人日、M3 变 86，总数不变，并同步 §4 的 M 备注）；② 在 §6.2 关键路径加一条"软依赖"说明："daily-brief 无天气数据时降级为无天气卡；tool-approval-sheet 的 trust-rules-manager 先接口预留；tray-menu 的设置入口先跳 settings-page。" |
| P2-9 | `02-life-gaps.zh.md:736-737`（对照 `arc-extension-catalog.json` 对应项） | 02 §9 明确写 habit-streak-strip"变体优先"、reference-band-chart"扩展优先"，但合并目录仍作为新项，且无 reviewNote/§4.5 行解释（见 P1-2 修复）。 | 已在 P1-2 给出 catalog reviewNote 文本；建议与 §4.5 两行一起补。 |
| P2-10 | `06:145` | "`ArcCatalog` 读取 `docs/arc-catalog.json`"——运行时实际读取内嵌资源 `src/Apps/Arc.Avalonia.Demo/Catalog/arc-catalog.json`（`ArcCatalog.cs:61-67,88-91`，csproj `EmbeddedResource`；两份文件 SHA-256 相同）。 | 改为："`ArcCatalog` 读取内嵌的 catalog 副本（`src/Apps/Arc.Avalonia.Demo/Catalog/arc-catalog.json`，与 `docs/arc-catalog.json` 字节级一致）"。 |
| P2-11 | `06:839` | "报告一律以'实测 + 文档快照'双列呈现"——全文没有双列表格；实际做法是正文个别位置加括注、附录 A 标"审查时点"。 | 改为："关键数字在正文/附录标注'审查时点'与'当前实测'；其余数字以 docs/04 快照为准，扩展后重新统计。" |
| P2-12 | `06:147` 与 `01-inventory.zh.md:1103` | 主报告称"本次分析实际运行了现有构建的审计模式"，但 01 §6.6 明确"本次未构建/未运行（任务约束）"；报告引用的审计输出与 `.recon/audit-final4.log`（2026-10-25 01:59）逐字一致，而最新源码 `GalleryWindow.cs` 修改于 2026-10-25 23:35（晚于该日志）；QA 未发现本轮新的审计日志。 | 二选一：① 本轮确实重跑过 → 把新输出保存为 `.recon/audit-<date>.log` 并在 `06:147` 引用；② 未重跑 → 改为"引用 2026-10-25 的 `--audit` 记录（294/294、missing=0），并按同一算法静态复算一致；本轮未重新构建"。 |
| P2-13 | `02-life-gaps.zh.md:455,563,629,658`（§7 标题） | 02 §7 仍把 `document-vault`（7.4）、`bill-due-list`（7.6）、`grocery-list`（7.7）列为"P0 设计说明（7 项）"，而合并目录已按 reviewNote 降为 P1（daily-brief 升 P0）。 | 在 02 §7 开头加一行注："合并后优先级以 `arc-extension-catalog.json` 的 `reviewNote` 为准：7.4/7.6/7.7 已降为 P1，daily-brief 升为 P0。" |
| P2-14 | `arc-extension-catalog.json` → `projectSuggestions[0].recommendation` | 文本重复："Blocks.App/Blocks.Commerce/Blocks.Data/Blocks.App"（Blocks.App 出现两次）。 | 改为："22 个生活域 block 建议新建 `Arc.Avalonia.BlocksLife`；扩展目录暂把 `project` 指向现有 `Blocks.App`/`Blocks.Commerce`/`Blocks.Data`（15/5/2）以保证可构建，落地时一次性迁移。" |

### P3（2 项，可选）

| ID | 位置 | 说明 | 建议 |
| --- | --- | --- | --- |
| P3-1 | `03-agent-gaps.zh.md:430` | 03 的粗算用 L=7（28 项 ≈90 人日），合并目录用 L=8（28 项 = 95 人日；与 §7.1 的 S/M/L 定义一致）。 | 03 §7.1 末尾注明"合并目录按 L=8 计，与本文件 L=7 粗算的差异见 catalog"；主报告 §0.4 已统一 L=8，无需改。 |
| P3-2 | `06:677` | "72 + 30 = 102"中 5 个关键服务按 catalog 实际 effort 为 3+3+8+3+8=**25**，合计 97；"约 102"是取整。 | 若修 P1-1 时一并改为"约 72+25≈97 人日"或保留"约 100"并注明取整。 |

---

## ④ 独立复算关键数字表

> 复算脚本：`.recon/qa_numbers.py`、`.recon/qa_source.py`、`.recon/qa_pages.py`、`.recon/qa_misc.py`、`.recon/qa_final.py`（全部只读，输出在 `.recon/qa-report*.txt`）。

| 指标 | 报告值 | 独立复算 | 结论 |
| --- | --- | --- | --- |
| catalog 总数 | 294 | 128 + 55 + 111 = 294，唯一 slug 294 | ✅ |
| 分类/分组 | 23 类 / 5 组 / 6 组 | `componentCategories=23`、`specialGroups=5`、`blockGroups=6`，逐类项数全部匹配报告 §1.2–§1.4 | ✅ |
| 294 项路由 | 166 + 88 + 40 = 294，missing 0 | 静态复算：`ItemSlug` 166、`Section()` 标题匹配 88、`ArcCatalogAliases` 40，覆盖 294，未覆盖 0 | ✅ |
| 页面/截图 | 219 页 / 438 图 | `shots/light=219`、`shots/dark=219`；219 个具体 `ArcPage` = 166 ItemPage + 53 共享页（含 5 个 Gallery 页）；audit 日志 `pages=219` | ✅（顶层另有 13 个 shell/crop PNG，非 catalog 页面截图） |
| 公共类/控件类 | 411 / 407；236 / 234；internal 100 | 非 Pages 顶层 `public class` 411（唯一 407）；继承链启发式 236（唯一 234，重复名 `ArcActivityHeatmap`/`ArcAnimatedCounter`）；internal 100（含嵌套；仅顶层 99，含 record 101） | ✅（internal 100 依赖"含嵌套"口径，见 §⑤） |
| 工程/源码规模 | 24 项目 / 516 `.cs` / 17.7 万行 / 112 `.axaml` | 24 目录+24 csproj；516 `.cs`、112 `.axaml`；当前全行数 176,685（docs/04 快照 176,581，差 0.06%，为行数口径差异） | ✅ |
| token/图标 | 82 常量 / 81 key / 8 accent / 161 图标 | docs/04 §0.2 原文一致；`ArcIconData.cs` 字典键 161 | ✅ |
| AutomationProperties | 703（报告已注"审查时点；当前实测 722"） | `.cs` 703 + `.axaml` 19 = 722；与报告括注一致 | ✅（口径已标注） |
| 虚拟化/动画/Render | EmojiPicker 唯一生效；11 套驱动；77 Render | docs/04 §2.6/§0.2 原文一致；源码 `VirtualizingStackPanel/ItemsRepeater/CompositionTarget.Rendering` 均为 0 | ✅ |
| 扩展总量 | 57（16/9/32；P0 14 / P1 34 / P2 9；life 25 / agent 28 / foundation 4） | 逐项计数全部相等；无重复、与 294 零冲突 | ✅ |
| 工作量 | 235（72/130/33；S8/M33/L16） | 按 S=1/M=3/L=8 逐项求和：总计 235，P0 72、P1 130、P2 33 | ✅ |
| 运行时服务 | 10 项 / 约 55 人日 | 10 项：life-store 3、scheduler 8、notifications 3、desktop-presence 8、permissions 3、virtualization 3、i18n 8、observability 3、mcp-runtime 8、voice-pipeline 8 = **55** | ✅ |
| 合计 | 290 人日 | 235 + 55 = 290 | ✅（P1-1 已修复：现行文字为 3–5 人 14–17 周 / 2–3 人 19–29 周，见 §⑦） |
| 场景 | 39（24 life + 15 agent） | JSON `scenarios=39`；场景 id/标题/提案数与 §2.3 表逐行一致；成员总数 175 = items 场景引用总数 175 | ✅ |
| §4 表行数/并集 | 14 / 34 / 9，并集 57 | 脚本解析行数 14/34/9，并集 57，无遗漏/重复；类型列与 JSON `kind` 完全一致 | ✅ |
| 里程碑 | M0 8、M1 50、M2 31、M3 89、M4 57，合计 235 | 按 §4 清单+目录 effort/priority 复算完全相等；P0 14=M0 1+M1 7+M2 6；P1 34=M1 8+M2 1+M3 17+M4 8 | ✅（逐项）；团队容量已在 P1-1 修复（见 §⑦） |
| 合并来源 | 24 + 28 + 5 = 57；4 项优先级调整 | 02=24、03=28、source=lead=5；slug 零重叠；priority 变更恰 4 项：daily-brief P1→P0、bill-due-list/document-vault/grocery-list P0→P1；除优先级外 kind/category/project/asset/title 零变更 | ✅ |
| reviewNote | 4 条（daily-brief/document-vault/bill-due-list/grocery-list） | 恰好这 4 个 slug；内容与"4 项优先级调整"一致 | ✅ |
| README docs/06 条目 | 294/236/219/57/39/14 P0/14–17 周 | 前 6 个数字一致；"14–17 周"需随 P1-1 同步 | ⚠️（联动态，见 P1-1） |

---

## ⑤ 未解决/不确定项（不影响交付，但需诚实标注）

1. **internal 类 100 的可复现性**：按"非 Pages 顶层 `internal class`"为 99，含嵌套为 100，含 `record` 为 101。报告采用 100，与 01-inventory §3 一致，且来源可解释；建议保留 100 并在附录 A 补一句口径（"含嵌套 internal class，不含 record"）。不构成数字错误。
2. **516 `.cs` / 176,581 LOC 是 docs/04 审查时点快照**：当前工作树复算 516/112，LOC 176,685（差 104 行 / 0.06%，为换行/口径差异）；报告用"约 17.7 万行"没问题。若要求逐字精确，需用 docs/04 的统计脚本重跑。
3. **审计证据**：QA 未运行 `--audit`（Lead 负责）。`.recon/audit-final4.log` 是 2026-10-25 01:59 的记录，晚于它的源码修改（GalleryWindow.cs 2026-10-25 23:35）意味着"当前 HEAD 的 --audit 输出"严格说未被本轮验证；但我们的**同算法静态复算**给出 166+88+40=294、missing 0，与日志一致，且通过 `CatalogAdmin` 算法复刻。建议 Lead 在 P2-12 中二选一处理（重跑保存日志 / 改措辞）。
4. **`--smoke`**：引用 `.recon/smoke-final4.log`（10/25，clicks=836, failures=0），同样未在本轮重跑；报告未给出 smoke 数字，风险低。
5. **02/03 附录的"P0/P1"口径滞后**：02 §7 的 7 个 P0 设计仍含 3 个已降 P1 的项（P2-13）；03 §7 的 28 项与合并目录一致，仅工作量 L=7 vs L=8 差约 5 人日（P3-1）。主报告已在 §4.1 说明"4 项优先级调整"，因此不算主报告错误，但三份文档同读时会困惑，建议按 P2-13 加一行注。
6. **docs/04 的"221 页"历史数字**：docs/04 §2.8 提到"启动反射 221 页"，当前源码 219 个具体页面、审计输出 `pages=219`、截图 219/219。主报告采用 219 正确；这是 docs/04 的旧口径，不需要改主报告。
7. **主观判断项**：§1.7 星级、§0.3 的"90% 界面"等定性结论无法用脚本证伪，未列入 PASS/FAIL，只做了表述一致性检查。
8. **行号易变性**：Lead 在 QA 期间有过两次编辑（22:58、23:02）；本报告问题均以冻结版 866 行/哈希 `2023…` 定位。修复后若行号变化，请按问题 ID 搜索原文定位。

---

## ⑥ 修复后快速复核清单（Lead 修完只需跑这些）

1. 重跑 `.recon/qa_numbers.py` → `MISMATCH`/`invalid`/`unknown`/`NOT covered` 全部为空；`4.2 rows=14 4.3 rows=34 4.4 rows=9; union=57`、`P0/P1/P2 = 14/34/9`、`235` 不变。
2. 重跑 `.recon/qa_final.py` → `proposal->catalog field changes` 为空；`2.3 mismatches` 为空；`4.2 scenario-column vs JSON mismatches` 为空。
3. `Select-String docs\06-... -Pattern '12 项（§3.3）','12 项见 §3.3','每一项都给出最接近','0 处 `TrayIcon`` 应无命中；`14 组`/`14 组扩展现有项` 命中。
4. `Select-String docs\06-... -Pattern '03-agent-gaps`）§4'` 应无命中；`03-agent-proposals.json` 的 `scenarios` 出现。
5. 重新核对 `§0.4`/`§6.1`/`§6.3`/`§6.4`/README 的"人日-人数-周数"三处口径一致；用下式自查：`总人日 ÷ (人数 × 5) ≈ 周数`，且每个里程碑 `人日 ÷ (人数 × 5) ≤ 周期`。
6. `docs/06` 与 `README.md` 的 docs/06 条目不再出现"14–17 周、2–3 人"与 290 人日并存的表述。
7. 链接检查：`agent-life-tools/04-qa-review.zh.md` 能解析（本文创建即成立）；其余相对链接不变。
8. 若修了 `arc-extension-catalog.json`（P1-2 的 reviewNote / P2-14 文本），重跑一次 JSON schema/枚举/composes/场景校验；目录 SHA256 会变，需在交付说明里更新。

---

## 附：核查方法与脚本索引

| 脚本 | 用途 | 输出 |
| --- | --- | --- |
| `.recon/qa_numbers.py` | catalog/扩展目录计数、§1.2–§1.4/§2.3/§4 表解析、composes/场景/枚举/分组校验、§4.5 覆盖、里程碑加法 | `.recon/qa-report.txt` |
| `.recon/qa_source.py` | 类声明/控件类/internal 计数、AutomationProperties、图标、报告 slug 的现有项命中、页面类/页面文件、inventory 附录核对 | `.recon/qa-report2.txt` |
| `.recon/qa_pages.py` | 219 页面 vs 源码页面类、截图文件名与 9 个不一致对、文件写 API、通知/托盘/虚拟化扫描、`ArcCalendar` API | `.recon/qa-report3.txt` |
| `.recon/qa_misc.py` | 附录 A 9 对截图、§4.6 与 JSON 交叉、`ArcToolCall.Approve`、签名导出、§5 旗舰项计数 | `.recon/qa-report4.txt` |
| `.recon/qa_final.py` | 合并保真度、必填字段/slug 格式、§4.2 场景列与 JSON、§2.3 标题、代表项分类、哈希 | `.recon/qa-final.txt` |
| `.recon/qa_cover.py` | 294 项在 01 的覆盖、57 slug 在报告中的覆盖、场景 id/标题覆盖 | 控制台 |

> 附注：`shots/` 顶层还有 13 个 `shell-*.png`、`crop-*.png`、`contact-*.png`（非 catalog 页面截图）；`shots/light` 与 `shots/dark` 各 219 个；448/451 的总文件数不影响"438 张截图"口径。

---

## ⑦ 修复复核（第 2 轮，2026-10-26 23:10 基线）

> **复核范围**：只复核 §③ 的修复项与回归，不重写 §② 全套 PASS/FAIL；未变更段落的结论沿用第 1 轮。
> **复核方式**：只读脚本（`.recon/qa_recheck.py`、`.recon/qa_residual.py`、`.recon/qa_numbers.py`、`.recon/qa_final.py`、`.recon/qa_md.py`、`.recon/qa_selfcheck.py`），未改 06/catalog/README/代码。
> **最终结论：可交付。** 3 个 P1 全部关闭，回归通过；仅余 1 个 P2 文档级残留（R-1）与 2 个 P3 建议。**第 2 轮未发现新的 P0/P1。**

### 7.1 新基线（Lead 已冻结）

| 文件 | 字节 | SHA256 |
| --- | ---: | --- |
| `docs/06-agent-life-tools-extension.zh.md` | 80,616 | `342035fe4a8afd81d76d932fab63ad79d40679b83d08de1b2a8fc4cd79a3f937` |
| `docs/arc-extension-catalog.json` | 94,960 | `7f78553e52b3d9492cf2f50191b7226e9427ab955d76e3988adb85f7bd2ba787` |
| `README.md` | 14,564 | `81b881f231a3305fbc3dc69271874b001f3bc69600e4b1de2e7883da8c524b54` |
| `docs/agent-life-tools/01-inventory.zh.md` | 108,395 | `60d72f83260f77cf0041419ce018b2be7b0d1227724646ede3cb2db5ca97ae9a` |
| `docs/agent-life-tools/02-life-gaps.zh.md` | 95,599 | `b1342aa6944cd421e8862dd7db9eebe710704f4347c1e37b5c675ae48eb66f78` |
| `docs/agent-life-tools/03-agent-gaps.zh.md` | 79,843 | `20efe5ed646278923c2e62af94e64a6c2f0f55e50013570a859ed312574ebcce` |
| `.recon/audit-2026-10-26.log` | 113 | `9b63c11141f90c71523048c9c2ae9db070fa3923da803a236baab9a82b880473` |

> 06 行数 883（原 866，+17 行：§4.5 增 11 行表格、§6.2 增 6 行软依赖区块）；catalog `dedupeNote` 2 条、`reviewNote` 4 条、57 项与 14/34/9 优先级未变。

### 7.2 修复项逐项复核

| ID | 修复内容 | 复核结果 | 证据 |
| --- | --- | --- | --- |
| P1-1 | 团队/周期/README 联动 | **PASS（有 P3 备注 R-2）** | `06:52`/`:656`/`:686` 均为"3–5 人（平均约 3.7 FTE、峰值 5）14–17 周；固定 2–3 人 19–29 周"；`06:694` 改为"72+25=97 人日，3 人约 7 周 / 2 人约 10 周"；README `:33-34` 已加"for a 3–5 person team"。按里程碑 FTE 复算：M1 4.0、M2 3.1、M3 4.45、M4 3.8，峰值 5 可覆盖；290 人日 ÷ (3.7×5) ≈ 15.7 周，落在 14–17 内。 |
| P1-2 | §4.5 补表/改导语/尾注 + catalog dedupeNote | **PASS（残留 R-1）** | §4.5 由 19 行扩到 **30 行**（去重后未重复 task-board），导语 `06:459` 改为"30 个 + 剩余 27 项见复用矩阵"；尾注 `06:494` 改为逐项对照，删除"38 项没有对应物"断言；catalog 新增 `dedupeNote`：`habit-streak-strip`（变体优先）、`reference-band-chart`（扩展优先）。脚本：4.5 行 30、unique 30、无重复；57 = 30 + 27；27 项均能在 02 §9 / 03 §12.2+§9 找到对照（但正文引用有 2 项瑕疵 → R-1）。 |
| P1-3 | §2.3 场景交叉引用 | **PASS** | `06:199` 已改为：生活域 24 场景见 02 §3；Agent 域 15 场景的 `id/title/summary` 见 **`03-agent-proposals.json` 的 `scenarios`**；生命周期/桌面约束/缺口见 03 §3/§4/§5，并明确"03 正文暂未展开逐场景的界面元素/可复用项"。不再声称"03 §4 有完整描述"。 |
| P2-1 | "12 项" → "14 组" | **PASS** | `06:296` "14 组（§3.3）"、`:365` "14 组扩展现有项见 §3.3"、`:788` "以 §3.3 的 14 组扩展现有项为基准校准"；旧串"12 项（§3.3）/12 项见 §3.3" 0 命中；§3.3 表实际 14 行。 |
| P2-2 | §1.1 运行时为零 | **PASS** | `06:75` 改为"0 处 TrayIcon / 平台系统通知 / HttpClient / 任务调度器 / 音频采集 / **显式列表虚拟化**（仅 EmojiPicker ListBox 行级虚拟化生效；另有 92 处动画 DispatcherTimer）"；与 §0.2 不再矛盾。 |
| P2-3 | docs/04 转述 | **PASS** | `06:323` 改为"C# 代码里 ItemsControl/DataTemplate/ItemsRepeater/VirtualizingStackPanel 使用次数为 0；XAML 侧仅 7 处 ListBox（其中 EmojiPicker 行级虚拟化生效）"，与 docs/04 §2.6 原文一致。 |
| P2-4 | "没有 ItemsSource" 绝对表述 | **PASS** | `06:326` 改为"没有**对外**的 ItemsSource 数据契约（仅 EmojiPicker / TimePicker 内部使用 ItemsSource）"；`:343` 的"没有 ItemsSource 契约"指对外契约，语义正确。 |
| P2-5 | ArcCalendar 属性清单 | **PASS** | `06:322` 改为"公开属性包括 …ShowToday/WeekStartsOn/DisabledDates"，并改为"没有事件集合或事件模板"；源码 `ArcCalendar.cs:38-39,115` 佐证。 |
| P2-6 | Buttons 代表项 | **PASS** | `06:81` 已由 hold-to-confirm 换为 `copy-button`；hold-to-confirm 仅在 Gestures 行出现。 |
| P2-7 | M3/M4 行程/日记 | **PASS** | `06:663` M3 括号补"行程/日记"；`:664` M4 删除这两项；M3=16 生活 P1+ref-band=89、M4=57 的算术未变。 |
| P2-8 | 跨里程碑依赖 | **PASS** | `06:678-682` 新增"软依赖（先接口、后实现，不阻塞里程碑）"三条：daily-brief 天气卡降级、tool-approval-sheet 先两个 scope、tray-menu 设置入口先跳 settings-page；未挪项，M2/M3 人日不变。 |
| P2-9 | 变体/扩展优先项的说明 | **PASS（并随 P1-2）** | catalog 2 条 `dedupeNote` 落地；§4.5 新增 habit-streak-strip / reference-band-chart 两行，分别标注"变体优先/扩展优先"并引用 02 §9。 |
| P2-10 | ArcCatalog 读取路径 | **PASS** | `06:145` 改为"读取内嵌的 catalog 副本（`src/Apps/Arc.Avalonia.Demo/Catalog/arc-catalog.json`，与 `docs/arc-catalog.json` 字节级一致）"；`ArcCatalog.cs:61-67,88-91` + csproj `EmbeddedResource` 佐证。 |
| P2-11 | "双列呈现" | **PASS** | `06:856` 改为"正文与附录对关键数字标注'审查时点'与'当前实测'；其余数字以 docs/04 快照为准"；全文旧串"双列" 0 命中。 |
| P2-12 | 审计证据/01 口径 | **PASS** | `06:147` 引用 2026-10-26 重跑日志 `.recon/audit-2026-10-26.log`；日志存在且输出 `pages=219 items=294 itemPages=166 matchedBySection=88 matchedByAlias=40 missing=0 pageFailures=0`；01 §6.6 `:1103` 已补"Lead 于 2026-10-26 23:09 重跑"说明，不再冲突。 |
| P2-13 | 02 §7 P0/P1 口径 | **PASS** | `02-life-gaps.zh.md:457-458` 新增注："合并后优先级以 catalog `reviewNote` 为准：document-vault/bill-due-list/grocery-list 降 P1；daily-brief 升 P0"，与目录 reviewNote 一致。 |
| P2-14 | catalog projectSuggestions | **PASS** | 文本改为"Blocks.App/Blocks.Commerce/Blocks.Data（15/5/2）"；脚本核对 Daily life 22 项的项目分布恰为 15/5/2，无重复 Blocks.App。 |
| P3-1 | 03 的 L=7/L=8 口径 | **PASS** | `03:432` 新增注：合并目录统一 L=8（28 项≈95 人日），L=7 仅本文件排序用；与主报告 §0.4 一致。 |
| P3-2 | 5 个关键服务人日 | **PASS** | `06:694` 改为"72 + 25 = 97 人日（3+3+8+3+8），3 人约 7 周 / 2 人约 10 周"；旧"102/30"已消失。 |

### 7.3 回归检查（全部通过）

| 检查 | 结果 |
| --- | --- |
| catalog 57 项唯一、零冲突、kind 16/9/32、priority 14/34/9、asset 25/28/4、effort S8/M33/L16、`summary` 各字段与逐项复算相等 | ✅ |
| `reviewNote` = 4（daily-brief/document-vault/bill-due-list/grocery-list）、`source=lead` = 5、`dedupeNote` = 2；优先级未变 | ✅ |
| `composes` / 场景引用 / crossDomain / grouping（57 项各一次）全部有效 | ✅ |
| 报告 §4.2/§4.3/§4.4 行数 14/34/9，并集 = 57，kind/priority 列与目录一致 | ✅ |
| §2.3 场景表 39 行（24+15），id/标题/提案数与 JSON 逐行一致；成员总数 175 | ✅ |
| §6.1 里程碑：M0 8 / M1 50 / M2 31 / M3 89 / M4 57 = 235；P0 14 = 1+7+6；P1 34 = 8+1+17+8；P2 9 | ✅ |
| 相对链接（06、README）全部可解析；04 自引用存在；33 张表列数全部一致、4 个代码围栏标记成对 | ✅ |
| `--audit` 日志输出 219 pages / 294 items / missing=0 / pageFailures=0；与静态复算 166+88+40=294 一致 | ✅ |
| README docs/06 条目数字（294/236/219/57/39/14 P0/3–5 人 14–17 周）与正文一致 | ✅ |
| 01/02/03 仅新增说明注，未改数字；三份文件哈希见 7.1 | ✅ |

### 7.4 残留问题（不阻塞交付）

| ID | 级别 | 位置 | 说明与建议（可直接执行） |
| --- | --- | --- | --- |
| R-1 | **P2** | `06:459`、`06:494` | **两处矩阵引用/计数不精确**：正文写"两表合计覆盖 52 个提案项"，但被引用的 02 §9（24 行）+ 03 §12.2（25 行）= **49**；剩余 27 项中的 `autonomy-dial`、`guardrail-alert` 只在 `03-agent-gaps` §9（不在 §12.2）有对照，按现引用有 2 项查不到。建议改：`06:459` → "……见 02 §9（24 项）与 03 §12.2（25 项）的复用矩阵，另 3 项 `autonomy-dial`/`guardrail-alert`/`connector-health` 见 03 §9，合计覆盖 52 个提案项"；`06:494` → "……见 02 §9 与 03 §12.2+§9 的复用矩阵（`autonomy-dial`/`guardrail-alert` 见 03 §9）"。若不想再改 06，也可接受——但这是唯一会让读者"按引用找不到项"的残留。 |
| R-2 | P3 | `06:52`/`:656` | 3.7 FTE × 14 周 × 5 = 259 人日 < 290；"14–17 周"的低端需要平均约 **4.1 FTE**。建议写成"约 15–17 周（3–5 人）"或"平均约 4.1 FTE"，避免 14 周端点被严格审计挑战。 |
| R-3 | P3 | `arc-extension-catalog.json` `conventions`/schema | 新增 `dedupeNote` 字段未在 `conventions`（kind/priority/effort/asset）或 schema 说明中登记；若目录会被外部工具消费，建议加一行字段说明（不阻塞交付）。 |

### 7.5 复核结论

- **P1 关闭数：3/3**（P1-2 主体通过，留 R-1 一个文档级 P2 残留）；**P2 通过 14/14**（P2-9 随 P1-2）；**P3 通过 2/2**；**新增 P0/P1：0**。
- 第 1 轮 §② 的第 4、7、8 项 FAIL 已分别由 P1-2、P1-1、P1-3 及 P2 修复关闭；其中第 4 项保留 R-1 一个文档级 P2。
- **最终判定：可交付。** 建议发布前顺手修 R-1（两行文本，约 5 分钟）；R-2/R-3 可选。若修 R-1，请回我新 SHA256，我只复核这两行与哈希后即可确认最终交付。

### 7.6 最终冻结确认（2026-10-26 23:15 基线，R-1/R-2/R-3 已修）

| 项目 | 最终值 |
| --- | --- |
| `docs/06-agent-life-tools-extension.zh.md` | 80,731 B / 883 行 / SHA256 `800d69ba8ca2c2fdc7858a9b91c3e032d0bfdf08d10afdf3c8de7bca538f91aa` |
| `docs/arc-extension-catalog.json` | 95,213 B / SHA256 `361b936f24b309b96563704269be9b00226841f2944d074cc6f03c72af68277d` |
| `README.md` | 14,564 B / SHA256 `81b881f231a3305fbc3dc69271874b001f3bc69600e4b1de2e7883da8c524b54`（未变） |

- **R-1 PASS**：`06:459` 现为"02 §9（24）+ 03 §12.2（25）+ 03 §9 补 `autonomy-dial`/`guardrail-alert`/`connector-health`（3）= 52 个提案项"；`06:494` 现为"见 02 §9 与 03 §12.2+§9（`autonomy-dial`/`guardrail-alert` 见 03 §9）"。数值与引用均自洽。
- **R-2 PASS**：`06:52`/`:656`/`:686` 改为"3–5 人（3.4–4.1 FTE、峰值 5）14–17 周"；端点复核：290 ÷ (4.1×5) = 14.15 周、290 ÷ (3.4×5) = 17.06 周，与 14–17 周区间自洽；固定 2–3 人的 19–29 周保留不变。
- **R-3 PASS**：catalog `conventions.itemFields` 已登记 `reviewNote`（4 项）与 `dedupeNote`（2 条，habit-streak-strip/reference-band-chart）；`summary`、57 项、14/34/9、dedupeNote 2、reviewNote 4 均未变。
- **回归复跑（23:15 最终版）**：catalog 57 unique / kind 16-9-32 / priority 14-34-9 / asset 25-28-4 / effort S8-M33-L16 / `effortPersonDays` 235 / `p0PersonDays` 72；§4.2/§4.3/§4.4 = 14/34/9、并集 57、kind/priority 列一致；§2.3 = 39 行、id/标题/提案数与 JSON 完全一致；§6.1 = M0 8/M1 50/M2 31/M3 89/M4 57（合计 235），P0 14、P1 34、P2 9；§4.5 = 30 行 + 其余 27 项（共 57）；06/README 相对链接全部可解析；06 围栏 4 个成对、33 张表列数一致；审计日志 219 pages / 294 items / missing=0。
- **最终判定：确认可交付。** R-1/R-2/R-3 全部关闭；无新增 P0/P1/P2；第 1 轮 3 个 P1 与 14 个 P2、2 个 P3 均有闭环证据。此后 06 与 catalog 由 Lead 冻结，不再变更。

> 备注：§② 的"FAIL 计数 3"与 §⑦.5 的"P1-2 留 R-1"是**第 1/第 2 轮时点结论**；以本节 7.6 的最终冻结结论为准（3 个 FAIL 项均已关闭，最终 FAIL = 0）。
