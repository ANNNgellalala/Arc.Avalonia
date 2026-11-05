# R2 评审：中间层能力、虚拟化可行性与 WBS 去重（对 v3 主文档）

> **评审对象**：[`docs/13-framework-layering-and-agent-top.zh.md`](../13-framework-layering-and-agent-top.zh.md)（v3，717 行）
> **评审范围**：D6（中间层 C1–C7）、D7（控件契约 10 条）、§2.0/§2.4 职责与准入、WBS 的 G3a–G3f、§4.3 里程碑验收。
> **评审方式**：对抗式复核。**结论以 B 报告为准**（[`13-b-component-mid-layer-audit.zh.md`](13-b-component-mid-layer-audit.zh.md)），逐条回仓库取 `file:line` 证据复核 v3 的可执行性、估算与口径。
> **基线**：工作树（HEAD `5c2513f`，2026-11-05 复核；与 v3 §0.3 声称的 2026-10-31 基线为同一工作树状态）。
> **约束**：未运行 `dotnet build`/`run`/`tools/*.ps1`；静态取证；运行时行为标注「待 POC」。

---

## 0. 评审结论摘要

| 项 | 结论 |
|---|---|
| Findings 总数 | **15 条**（P0 × 5、P1 × 5、P2 × 5） |
| 最大低估项 | **C3 数据驱动列表**：按 v3 字面「列表/树/网格都改为 `ItemsSource`+`DataTemplate`，用 Avalonia 自带虚拟化」不可行且按字面低估 **+20~40 人日**（网格一项即 ≈20–30）；按本文建议降级为「列表/树数据驱动 + 网格官方 DataGrid Feature + 分页」后，C3 = 25（+5） |
| 重复计价 | **2 处确定重复**：G2「API 面收敛」≈10 人日 与 G3a 重复；C1「ItemsSource」与 C3 重复（≈6）。另有 1 处跨表口径（迁移 268 文件 vs 实际 614）。G2 37→27、G3a 重排 44→53，叠加其余重估后 **357→374（净 +17）** |
| WBS 人日建议 | **357 → 374（+17，M3，含 samples +4）**；若 M2 坚持三控件真数据驱动：**+20~40 → 389–409**；可选 M3 网格真虚拟化再 +20~30 |
| 可直接判定的验收 | §4.3 的 **8 条**里程碑门槛中，有 **4 条不可直接判定**（M2 ①②③、M3 ②）。本文 §5 给出可执行清单（含 41 控件候选名单、6 页控件矩阵、度量阈值） |
| 与「渲染层归 Avalonia」口径冲突 | **2 处硬冲突**：① §0.0/§2.0 说「不自研虚拟化」，§4.6 却写「网格/树虚拟化需自研（G3c 20）」；② C6/G3e 的「DPI 标度」属平台渲染层（组件内 0 处 DPI 代码），应改为「尺寸 token 复核」 |
| 证据保真度 | v3 引用的 3 个关键数字在当前树不可复现或口径混用：`ItemsControl/DataTemplate 0 处`（文本命中 6/8/6，集中在 EmojiPicker/TimePicker）、`18 处 Children.Clear`（实为 91 处/65 文件）、`298 Docs`（实为 294 item docs + `docs/` 下 4 个 JSON） |

---

## 1. P0（决定 M1/M2 能否成立，必须先修）

### P0-1 C3「数据驱动列表」按字面范围不可行，20 人日低估 15–30 人日

**问题**：v3 把 C3 描述为「把列表/树/网格改成 `ItemsSource` + `DataTemplate`，用上 Avalonia 自带虚拟化」，并估 20 人日（D6 第 363 行、G3c 第 469 行），同时在 §0.0 第 16 行承诺「不自研虚拟化引擎」、§2.0 第 178 行承诺「Arc 只把控件改成 ItemsSource + DataTemplate，用上该机制」。**但三个数据控件不是 `ItemsControl` 派生，而是「模板 + 手工构建全部子元素」的 `TemplatedControl`**，改成数据驱动等于重写组合与交互层，不是「换成 ItemsSource」：

| 控件 | 基类 / 规模 | 行构建方式（现状） | 自述限制 |
|---|---|---|---|
| `ArcDataGrid` | `TemplatedControl`（`ArcDataGrid.cs:126`），**1,856 行** | `BuildBody()` 先 `_bodyHost.Children.Clear()`（`:1029`），再 `new StackPanel()` **一次性构建 `_view.Count` 全部行**（`:1082-1088`），行内是手工 `Border/Grid` + `Grid.SetColumn`（`:1091` 起、`:1188`） | `:115-125` 明确「Deliberately omitted … **row virtualisation**. There is no row-number gutter and no horizontal scroll」（`:120-122`） |
| `ArcTreeTable` | `TemplatedControl`（`ArcTreeTable.cs:116`），**2,355 行** | `BuildBody()` `Children.Clear()`（`:1236`）后 `foreach (var item in _display)` 全量构建行（`:1258-1266`） | `:106-108`「**no row virtualisation**, no height-spring layout」 |
| `ArcTreeView` | `TemplatedControl`（`ArcTreeView.cs:33`），**424 行** | `Rebuild()` `_host.Children.Clear()`（`:148`）→ 递归 `AddNode` 把**所有展开节点**塞进 `StackPanel`（`:152-176`） | 无虚拟化实现 |

三项合计 **4,635 行**组合/交互代码（含列宽拖拽、列显隐、单元格内联编辑、Shift 区间选择、箭头键盘、排序、footer 汇总等）。改成 `ItemsSource+DataTemplate` 意味着把「每行一个手搭 Grid」换成「行模板 + 列描述符 + 单元格模板 + 容器回收 + 编辑态保持」，**等价于重写一个 DataGrid**；`VirtualizingStackPanel` 只负责「单行/单列列表」的容器虚拟化（Avalonia 包内 XML doc：`VirtualizingStackPanel` = "Arranges and virtualizes content on a single line…"），不提供二维列对齐、表头粘性、编辑覆盖层，这些必须自己补。

**影响**：
- 若照字面做：**网格 20–30 + 树表 10–15 + 树视图 5–8 + 列表/聊天 5–8 ≈ 40–60 人日**（不是 20）。
- 若只做「列表/树」而网格后置（v3 §5 第 560 行的顺序确实如此，但 G3c 与 D6 都写「列表/树/网格」）：**15–23 人日**。
- M2 门槛与 Agent transcript（见 P0-3）都依赖它，低估会直接压垮 M2。

**建议（推荐方案，25 人日，M2）**：
1. **列表（6）**：复用仓库已有 `ArcVirtualList`（`Foundation/Arc.Avalonia/Controls/ArcVirtualList.cs:29-40`，`ListBox` 派生、明确使用 `VirtualizingStackPanel`，296 行）——由 G1 从 Foundation 移到 L2，加增量 `Append` API，先接入聊天/列表类控件；这是 v3 完全没提到的现成基座。
2. **树/树表（10）**：展平可见行 → `ItemsControl` + `VirtualizingStackPanel`（树表保留列定义/表头复用），懒加载子节点（已有 `LoadChildren`），键盘焦点按逻辑行索引维护。真虚拟化 8–14。
3. **网格（9）**：不重写 `ArcDataGrid`，而是 ① 新增分页/服务端数据 API（`PageIndex/PageSize/TotalItems` + `PageRequested`，3–4）；② 采用允许的官方 `Avalonia.Controls.DataGrid` 收进 `Arc.Avalonia.Feature.DataGrid` 并做 Arc 主题覆盖（4–5，含 R7 包结构与 D3 主题键）——这才是「用 Avalonia 自带虚拟化」，且与 §2.0/§4.6 的 tier② 允许口径一致。
4. **把「ArcDataGrid 真虚拟化重写」列为 M3 可选（+20~30）**，不进 M2 门槛。

**替代方案（若坚持不自绘裁剪、也不引官方 DataGrid）**：仅做「可见区间裁剪 + 分页」——两个网格的行高已是常量（`ArcDataGrid.cs:128` `RowHeight=40`；`ArcTreeTable.cs:118` `RowHeight=44`），可保留现有 StackPanel 组合，在 `ScrollViewer.Offset` 变化时重建 `[first-16, last+16]` 窗口并加占位 spacer（每控件 6–10 人日）。可行但交互复杂（选中/编辑跨窗口、焦点保持），不如方案 3 稳。

**人日增量**：C3 20 → **25（+5，推荐口径）**；若保留「三控件真数据驱动 + 全量替换 Children.Clear」的字面范围则 **40–60（+20~40）**；若只做「列表/树」为 15–23（+0~3）；M3 可选再 +20~30。

---

### P0-2 重复计价：G2 含「API 面收敛」与 G3a 重复；C1 的 `ItemsSource` 与 C3 重复

**问题**：两张 WBS 表对同一工作各计一次。

| 重复项 | 证据 | 金额 |
|---|---|---|
| G2「demo 残留清理与 API 面收敛（13）」（v3 `:466`） vs G3a「公开面收敛」（`:467`） | G3a 描述逐字包含「公开面收敛（16 个内部部件）」；G2 的 13 与 B 报告 P0-3（demo 残留 2–3）+ P0-4（API 收敛 8–12）= 10–15 精确吻合，即 13 ≈ 3 + 10 | **≈10** |
| C1（G3a）含「ItemsSource」（`:361`、`:467`） vs C3（G3c）就是「列表/树/网格改 ItemsSource」（`:363`、`:469`） | 两处描述同一迁移；D7-4 又第三次要求（`:380`） | **≈5–8** |
| D7-4「集合型数据用 ItemsSource」的隐含范围 | 组件层集合型 `StyledProperty` **130 个**（含 `Items/Options/Rows/Nodes/Columns/Series/...`，nameof 命中 59 次）；若一刀切，改造面从 3 个控件扩到 ≈45 个控件 | 会放大 C1/C3 2–4 倍 |

**建议**：
- **G2 只保留 demo 残留**：persona 默认值（`ArcUserMenu.cs:70,73`）、`ArcSamplePhoto.cs:14`、3 张 demo PNG（`Feedback.csproj:16`）、演示 helper 随页面迁出 → **3 人日**；G2 由 37 → **27**。
- **API 面收敛全部归 G3a**（内部部件 16 个 internal 化、`ArcAvatarShape` 去重、10 个 `*Size` 枚举、命名/集合类型统一）→ G3a 显式列 10。
- **`ItemsSource` 只留在 C3**，C1 删除该项（C1 描述里去掉）。
- **D7-4 收窄**为「**数据行集合**（`Rows`/`Nodes`/`Messages`/`Events` 等需要虚拟化的集合）用 `ItemsSource`；**配置型集合**（`Columns`/`Options`/`Segments`/`Tabs`/`Swatches`）可保留强类型模型集合」——否则 `Items`/`Options` 类 API 全要重做，与 AtomUI/Semi 的通行做法也不一致。

**调整后总数**：仅这两项去重/重排则 357→356；叠加 §4 的 C3（+5）、G3e（+2）、G3f（+4）、G4 MVP（+3）与 samples（+4）后为 **374**。

---

### P0-3 M2 门槛「10k 事件流式渲染不卡」依赖未分配的 Agent 控件

**问题**：M2 硬门槛 ③（`:488`）要求「10k 事件流式渲染不卡」，但承担该场景的 `ArcAgentTranscript` 是 L4 控件，**不在 C3 的「列表/树/网格」范围，G4 MVP 的「增量 transcript 14」（`:473`）也只描述增量、不含虚拟化/窗口化**，现状是「全量重建 + 全量子元素」：

- `ArcAgentTranscript : Panel`（`src/Agent/Arc.Avalonia.AgentUI/Controls/ArcAgentTranscript.cs:123`），`Events` 为 `IEnumerable<ArcAgentEvent>?`（`:126`）；
- 属性变更即 `Rebuild()`（`:152-159`），`Children.Clear()`（`:217`）后 `foreach (var evt in events) list.Children.Add(BuildRow(evt))`（`:239-243`），外层仅一个 `ScrollViewer`（`:245-250`）；
- 10k 事件 = 10k 个视觉容器 + 每次属性刷新全量重建 → 门槛必然不过。

**影响**：M2 验收不可达成；且容易在 G4/G3c 之间出现「都以为对方做」的空档。

**建议**（二选一，推荐 A）：
- **A**：在 G4 MVP 内显式增加「`ArcAgentTranscript` 窗口化 + 增量追加」**+3 人日**（G4 MVP 46→49），M2 门槛改为「10k 事件、60/s 追加下，视觉行容器 ≤ 视口行数×2，UI 线程批处理 p95 ≤ 8 ms」（见 §5.4）。
- **B**：门槛降级为「10k 事件窗口化渲染（只保留最近 N 条）」，把真虚拟化移出 M2。需同时改 §0.4/§4.3 的门槛文字，避免「不可判定」。

**人日增量**：+3（方案 A）。

---

### P0-4 D7-4 一刀切会让 API 改造面翻 15 倍

**问题**：D7 第 4 条「集合型数据用 `ItemsSource` + `DataTemplate`，不提供『只能 new 出来』的 `Items`」（`:380`）没有区分「数据集合」与「配置集合」。实测组件层集合型 `StyledProperty` **130 个**、约 **45 个控件**受影响（`Items` 15、`Options` 9、`Labels` 6、`Series` 6、`Columns` 4、`Rows` 4、`Segments` 4、`Events/Messages/Nodes/Steps/Swatches` 各 1–2…）。若按字面执行，连 `ArcDataGrid.Columns`（列定义）、`ArcButtonGroup.Items`、`ArcSegments` 这类**设计期配置**也要数据模板化，收益为零、成本巨大。

**建议**：D7-4 改写为：
> 4. **数据行集合**（表格行、树节点、消息流等参与滚动的数据）用 `ItemsSource` + `DataTemplate`；配置型集合（`Columns`/`Options`/`Segments`/`Tabs`/`Swatches`/`Series` 的元数据）保留强类型模型集合。

**影响**：不修则 C1/C3 的范围不可收敛；修后 C1/C3 按本文估算可控。

---

### P0-5 C1「44 人日」范围未定义：按字面（143 事件全转 RoutedEvent + 106 控件补 TemplatePart + ItemsSource + Charts）实为低估

**问题**：C1（`:361`）列出 7 项：RoutedEvent、`[TemplatePart]`、`ItemsSource`、状态枚举、公开面收敛（16 部件）、10 个 `*Size` 枚举、Charts 主题化，合计 44。对照 B 报告 §7/§9 的原始分档：

| C1 子项 | B 报告原始估算 | 按 v3 字面范围重估 | 差异原因 |
|---|---|---|---|
| RoutedEvent | §7-7「事件与模板契约」**M 6–10（仅关键控件）** | **143 个 CLR event 全转**：≈0.2 人日/事件 → **20–35**；限定 Top-40 控件约 60 个交互事件 → **10–12** | v3 未写范围；§1.2 用 143 总数，暗示全量 |
| `[TemplatePart]` + `PART_*` | 同上 M 6–10（与事件合并计） | 106 个有主题控件、200 处 `OnApplyTemplate`（实测）→ 属性声明 + 部件改名（动模板）+ 验证 = **10–14** | 原 B 把两项合并且偏乐观 |
| 公开面收敛 | §9 P0-4 **M 8–12** | **10**（从 G2 迁入） | — |
| 10 个 `*Size` 枚举统一 | 含在 P0-4 内 | **3–4** | — |
| Charts 主题化（20 个自绘图表） | §9 P1-12 **M 6–10** | **8–12**（20 个控件各需可覆盖的样式入口 + 双主题验证） | v3 §4.5 却写「Charts 22 控件」 |
| 状态枚举/伪类规范（D7-5） | B 未单列 | **4–6** | v3 表里有「状态枚举」但未计价 |
| `ItemsSource` | 属 C3 | **0（移出）** | 重复计价 |

**结论**：
- 若 RoutedEvent **全量 143**：C1 ≈ **55–73** → v3 的 44 **低估 11–29**。
- 若 RoutedEvent **限定 Top-40 交互事件**（推荐）：C1 ≈ **46–56（取 53）** → 44 **低估 2–12**。
- 若同时保留 `ItemsSource` 且按字面全量：最坏 **65–80**。

**建议**：C1 拆分并写死范围（推荐合计 **53**）：

| 新子项 | 内容 | 人日 |
|---|---|---:|
| C1a | RoutedEvent：Top-40 控件、约 60 个**交互结果**事件（生命周期事件保留 CLR event，D7-3 同步改「交互结果」限定） | 10 |
| C1b | `[TemplatePart]` + `PART_*` 统一（106 个有主题控件，含 200 处 `OnApplyTemplate` 的部件查找规范化） | 10 |
| C1c | 公开面收敛：16 个内部部件 internal 化、`ArcAvatarShape` 去重、命名/集合类型统一（从 G2 迁入） | 10 |
| C1d | 10 个 `*Size` 枚举 → `ArcSize` + 尺寸 token 复核（吸收 C6 的 DPI 项，见 P1-2） | 4 |
| C1e | Charts 20 控件主题化（StyleKey/可覆盖样式入口） | 8 |
| C1f | 视觉状态枚举/伪类规范（D7-5） | 5 |
| C1g | 键盘/焦点契约文档 + 键位矩阵（见 P1-3 遗漏项） | 6 |
| **合计** | | **53** |

**人日增量**：44 → **53（+9）**；若坚持 143 事件全量另 **+10~20**。

---

## 2. P1（影响可执行性/可信度，M2 前应修）

### P1-1 §4.3 的 M2/M3 验收有 4 条不可判定（详见 §5 的可执行清单）

| 门槛 | v3 原文 | 不可判定的原因 | 建议判定物 |
|---|---|---|---|
| M2 ①（`:488`） | 「示例应用覆盖登录→列表→表单→对话框→文件上传→设置页」 | 未定义哪个示例工程、哪些页面、每页必须用哪些控件、谁验收、证据是什么 | 固定 `samples/Arc.Avalonia.Samples` 工程 + §5.3 的 6 页 × 控件矩阵 + 截图/headless 断言清单 |
| M2 ④（`:488`/`:649`） | 「151 控件中至少 40 个满足 D7 第 1–6 条」 | 40 个怎么选没有规则；D7-4 只对数据集合适用（P0-4）；没有检查方法与证据 | §5.1 抽样规则 + §5.2 逐条检查表（反射 + grep + 键位测试） |
| M2 ③（`:488`） | 「10k 事件流式渲染不卡」 | 「不卡」无指标、无测量方法、无机器口径 | §5.4：容器数、UI 线程批处理 p95/p99、堆增长、重建次数（附 POC 校准说明） |
| M3 ②（`:489`） | 「RTL 与 1.25–2x DPI 通过」 | 无「通过」定义、无范围（哪些控件/页面）、无阈值 | §5.5：41 控件 × 2 主题 × 3 比例的截图/裁剪检查 + 6 示例页人工验收 |

**建议**：直接把 §5 的表挂到 §4.3 作为验收附录；验收人固定为「实现者自证 + Lead 复验」，证据入库（`artifacts/acceptance/`）。**人日增量**：验收 harness 与清单执行已分摊到 G3f（见 §4），不另加。

### P1-2 C6/G3e 的「DPI 标度」属于平台渲染层，且组件内无 DPI 代码

**问题**：C6（`:366`）与 G3e（`:471`）把「DPI 标度」列为 24 人日包的一项，M3 门槛又要求「1.25–2x DPI 通过」（`:489`）。但：
- 组件层对 `RenderScaling/Dpi/DpiScale/ScaleFactor` 的引用 **0 处**（实测）；像素常量均为 DIP（如 `ArcDataGrid.cs:128` `RowHeight=40`、`ArcTreeTable.cs:118-122`），会随 Avalonia 的全局缩放自动放大——**不存在「Arc 做 DPI 标度」这层代码**；
- 用户 v3 口径（`:14`）明确「渲染后端/布局引擎/…全部归 Avalonia」，DPI 缩放属其布局/渲染体系；Arc 的责任只是**验证固定尺寸在 1.25–2.0 下不裁剪**，这是验收项，不是功能包。

**建议**：
- 从 C6/G3e 删除「DPI 标度」；改为 **C1d「尺寸 token 复核」2 人日**（把散落的 40/44/12/24 等常量收回 token/常量表，见 B §5.2.7）＋ **G3f 的 1.25/1.5/2.0 验收**（§5.5）。
- G3e 重排为：DnD 8 + MVVM 8 + 容器 8 + 尺寸复核 2 = **26（24→26，+2）**。

**人日增量**：G3e +2（净；DPI 删除、其余上修）。

### P1-3 遗漏项核对

| 项 | v3 是否覆盖 | 结论/建议 |
|---|---|---|
| 可编译 `samples/` 工程 | D10 提到「提供可编译 samples/」（`:441`），**WBS 无对应行** | **遗漏**。它同时是 M2 门槛①的载体。建议 G2 +4（工程骨架 + 6 页示例 + 接入文档） |
| 键盘/焦点契约文档与测试 | §2.4/§2.1 提「焦点/键盘契约」，D7-6 是行为条款，**WBS 无对应行** | **遗漏**。建议并入 C1g（+6，已含在 P0-5 的 53 内） |
| 控件级性能（非渲染） | G3f 只有「151 控件 × 双主题矩阵、读屏回归」 | **遗漏**。DataGrid 排序 10k、EmojiPicker 1.8k 搜索、Charts 10k 点、Transcript 追加等属控件算法/容器数量，不是渲染层。建议 G3f +4（阈值见 §5.4） |
| D7-5 状态枚举/伪类 | D7 有条款，D6 的 C1 提「状态枚举」但无验收 | 已并入 C1f（+5） |
| D7 第 7–10 条的全量门槛 | M3 只要求「15 个关键控件 peer + RTL/DPI 通过」 | 补一条 M3 门槛：**151 控件满足 D7 1–10，或进入显式例外清单（Charts/内部部件等）**，否则 D7 只是口号 |
| 迁移范围（Blocks/Special/Agent） | G2 只写「268 文件出库」 | 见 P1-4 |

### P1-4 G2「268 文件出库」只覆盖 Components，与 R2/包拓扑/§0.3 数字冲突（跨 WBS，建议 R1 复核）

**问题**：R2 硬规则（`:225`）要求「发布库禁止引用 Demo」，当前引用 Demo 的 csproj 为 **26/29**（25 个 src 库 + tests；v3 写 25/29）。而 G2 的「268 文件出库」（`:466`）恰好等于 Components 的 140 页 + 128 文档（B 报告口径），**未覆盖 Blocks/Special/Agent**：

| 区域 | Pages 文件 | ArcItemPage | Docs JSON | 是否引用 Demo |
|---|---:|---:|---:|---|
| Components | 140 | 128 | 128 | 是（`Inputs.csproj:10` 等 11/11） |
| Blocks ×6 | 111 | 111 | 111 | 是（`Blocks.AI.csproj:14` 等 6/6） |
| Special ×5 | 56 | 55 | 55 | 是（`Special.AIWidgets.csproj:10` 等 5/5） |
| Agent ×2 | 13（`: ArcPage`） | 0 | 0 | 是（`AgentUI.csproj:10`、`LifeToday.csproj:10`） |
| **库内合计** | **320** | **294** | **294** | — |

即真实迁移面是 **320 页 + 294 文档 = 614 个文件**（不含 Gallery 的 8 页），是 G2 的 2.3 倍。D2 写「新建 Docs 工程（引用 11+ 个组件族，含全部 Pages 与 Docs JSON）」（`:310`）本身与 268 不自洽。

**影响**：若只迁 Components，则 L3 Patterns 与 L4 Agent 包仍依赖 Demo，R2 门禁无法对它们生效；M1 的「发布库不含 Demo」门槛会被"选择性通过"。

**建议**：
- 若 M1 只发布 L1+L2：**在 D2/G2 明确写「M1 仅迁 Components（268 文件）；Patterns/Agent 的 346 文件随各自包发布时迁移」**，并在 G5 增加 3–5 人日（Agent 13 页 + Blocks/Special 167 页 + Abstractions 适配）。
- 若 M1 就要「所有发布库无 Demo」：G2 的 4 人日 → **8–10（+4~6）**，回验（audit 294 页/315 smoke/双主题截图）再加 2–3。
- 该条的最终归属建议交 **R1/G2 修订**；本文不计入 +17 的合计（仅在 §4 备注）。

### P1-5 基线数字口径混用，评审与估算会漂移

| v3 数字 | 实际（本次复核） | 说明 |
|---|---|---|
| `137` 个 ControlTheme（`112 Arc + 25 内建`） | **137（112+25）** ✓ 为「**全 src**」口径；B 报告的 **131（106+25）** 是「仅 Components」口径 | 两数都对，但 §0.3 与 §1.2/§4.5 混用不同口径（§4.5 用「Charts 22」） |
| `328 个页面类（294 ArcItemPage）` | `Pages/` 下 `.cs` **328 个**、ArcItemPage **294** ✓ | 但 §1.1 又用 Components 的「140 页/128 文档」→ 同一文档两种 scope |
| `298 份 Docs/*.json` | **294**（Components 128 + Blocks 111 + Special 55）；`298` = 294 + `docs/` 目录下 4 个 JSON（大小写不敏感匹配所致） | 建议统一为 294 |
| `25/29 个项目引用 Demo` | **26/29**（25 个 src 库 + `tests/Arc.Avalonia.Tests`） | 差 1 |
| `774 文件 / 182,621 行` | 763 个 `.cs/.axaml`；物理行 **206,968**、非空行 **181,230** | 疑似「非空行」口径，建议标注 |
| `ItemsControl/DataTemplate 使用 0 处` | EmojiPicker：1 个 `ItemsControl` + 4 个 `DataTemplate` + 3 处 `ItemsSource` 赋值；TimePicker 另 1 处（文本命中 6/8/6） | 见 P2-2 |
| `18 处 Children.Clear()` | Components 非 Pages 代码 **91 处 / 65 文件** | 见 P2-1 |

**建议**：在 §0.3 标注每个数字的 scope（全 src / Components / 仅发布库）与行数口径（物理/非空），并把「0 处」「18 处」改为可复现的 `file:line` 列表。**人日增量**：0.5（文档校准，可并入 G0）。

---

## 3. P2（证据保真与文档一致性，建议修但不影响大数）

### P2-1「18 处 Children.Clear」不可复现，且「替换 Children.Clear」不是虚拟化杠杆

- 当前 Components 非 Pages 代码 `Children.Clear()` **91 处、65 个文件**；`docs/04:215` 的「18 处」是当时对「数据列表重写」子集的计数，v3 把它当成全量并写进 C3（`:363`、`:469`）。
- 更关键：绝大多数 `Children.Clear()` 是**非数据列表**的合理重建（菜单、轮播、图表覆盖层、Cookie 条等）。与虚拟化真正相关的只有 5–6 处数据宿主：`ArcDataGrid.cs:1029`、`ArcTreeTable.cs:1236`、`ArcTreeView.cs:148`、`ArcChatThread.cs:467`、`ArcAgentTranscript.cs:217`（另 EmojiPicker 已是 ItemsSource）。
- **建议**：C3 的文字从「替换 18 处」改为「改造 5 个数据宿主（列举 file:line）」，避免执行者去动 91 处无关代码。

### P2-2「ItemsControl/DataTemplate 使用 0 处」与仓库及自引文档矛盾

- v3 §1.2 第 144 行写「`ItemsControl`/`DataTemplate` 使用 0 处」，但 G3c 的依据又引 `docs/04`（`:469`），而 `docs/04:350` 原文是「**C# 侧** ItemsControl/DataTemplate/虚拟化 0；**XAML 仅 7 处 ListBox**」；`docs/04:28` 还明确「仅 EmojiPicker 用 ListBox 做行级虚拟化」。
- 实测：`ArcEmojiPicker.axaml:378-386` 用 `ListBox + ItemTemplate`（`PART_Grid`，默认 ItemsPanel → 虚拟化）、`:395-424` 用 `ItemsControl + DataTemplate`（Cells）、`ArcEmojiPicker.cs:824` 设置 `_grid.ItemsSource`；`ArcTimePicker.cs:204` 也设 `ItemsSource`；`ArcVirtualList.cs:29-40` 已是 `ListBox`+VSP 的虚拟化列表。
- **建议**：改为「组件层仅 EmojiPicker/TimePicker 使用 `ItemsControl`/`DataTemplate`；DataGrid/TreeTable/ChatThread/AgentTranscript 为手写重建；树/网格/长列表无虚拟化」。这同时说明 **C3 有现成模式可抄**（EmojiPicker 的行模板 + ArcVirtualList 的虚拟化容器），可降低估算。

### P2-3 `ArcVirtualList` 基座未被 v3 提及（C3 的现成起点）

`Foundation/Arc.Avalonia/Controls/ArcVirtualList.cs`（296 行）自述「…uses `VirtualizingStackPanel`, so only the rows inside the viewport are measured and drawn」（`:29-31`），`ItemsSource` 已接（`:59`），依赖 Fluent ListBox 主题（`:71`，与 A 报告的 Fluent 契约一致）。目前**仅 Gallery 示例页使用**（`VirtualListDemoPage.cs:39,63-64`），组件层 0 使用。**建议**：G1 把它移到 L2（v3 D5 已计划），C3 明确「列表基座复用 `ArcVirtualList` + 增量 Append API」，可把列表部分压到 5–6 人日。

### P2-4 §4.4 的交叉引用错误与 §5 顺序 / §4.6 结论不一致

- §4.4（`:506`）写「可先只做列表/树（**见 §4.6**）」，但 §4.6（`:524-532`）实际是「官方附加控件取舍」，没有「先列表/树」的内容；真正的顺序在 §5（`:560`）。
- §4.6 结论「不采用官方 DataGrid → 网格/树虚拟化需**自研**（G3c 20）」与 §0.0/§2.0「**不自研**虚拟化引擎」直接冲突。

**建议**：① §4.4 引用改为 §5；② 在 §0.0/§2.0/§4.6/C3 统一为「不自研虚拟化**机制**；数据驱动改造 + 复用 VSP/官方 DataGrid 属允许范围」；③ 若采纳本文 P0-1 的方案 3，§4.6 的「不推荐官方 DataGrid」需改为「小数据设计表用 ArcDataGrid，大数据用 Feature.DataGrid」。

### P2-5 D7 第 7–10 条没有 M3 门槛（契约清单缺「完成定义」）

D7 是「进 L2 的通行证」（`:373-389`），但 M3 门槛只覆盖 15 个 peer + RTL/DPI（`:489`），D7-9/D7-10（DPI、英文默认文案）从未成为全量门槛。**建议**：M3 增加「151 控件满足 D7 1–10 或列入例外清单（Charts/内部部件/Agent）」，并把例外清单作为发布物之一。

---

## 4. WBS 调整建议（G3a–G3f + 关联项）

| 工作包 | v3 | 建议 | Δ | 依据 |
|---|---:|---:|---:|---|
| G2（关联，去重后） | 37 | **27** | −10 | P0-2（API 收敛迁 G3a）；若加 samples +4 → 31；若加迁移扩面 +4~6 另计 P1-4 |
| **G3a 组件契约** | 44 | **53** | **+9** | P0-5：C1a 10 / C1b 10 / C1c 10 / C1d 4 / C1e 8 / C1f 5 / C1g 6 |
| G3b 服务层+窗口壳 | 24 | 24 | 0 | 与 B 报告 P1-2（10–15）+ P1-1（8–14）区间一致（24 偏中） |
| **G3c 数据驱动列表** | 20 | **25** | **+5** | P0-1 推荐口径：列表 6 + 树 10 + 网格 9（Feature.DataGrid + 分页，不重写）；M3 可选真虚拟化 +20~30 |
| G3d 无障碍+本地化 | 30 | 30 | 0 | B：P1-5 15–20 + P1-6 12–18（30 为低中位） |
| **G3e DnD/MVVM/容器** | 24 | **26** | **+2** | P1-2：删 DPI，DnD 8 + MVVM 8 + 容器 8 + 尺寸复核 2 |
| **G3f 测试基线** | 20 | **24** | **+4** | P1-3：加控件级性能基线（10k 排序/1.8k 搜索/10k 点渲染）与 D7 检查 harness |
| **G4（关联，MVP 部分）** | 46 | **49** | **+3** | P0-3：AgentTranscript 窗口化 + 增量 |
| G5 迁移收尾 | 20 | 20（+3–5 若 P1-4 走扩面） | 0~+5 | P1-4 备注 |

**合计（M3，推荐口径）**：357 + (−10 + 9 + 0 + 5 + 0 + 2 + 4 + 3) = **370**；若把 P1-3 的 samples（+4）计入 G2 → **374**；若 M2 坚持三控件真数据驱动（C3 40–60）→ **389–409**；M3 可选网格真虚拟化 → **+20~30**。

> 说明：G2/G4 不属本评审的 WBS 主责（R1/R3 分别覆盖），此处仅做去重与承接口径；建议 Lead 在合并 R1–R4 时以本表做一次总账。

**M1/M2/M3 重算（推荐口径）**：
- M1 = G0 16 + G1 18 + G2 27 = **61**（v3 71）
- M2 = M1 61 + G3a 53 + G3b 24 + G3c 25 + G4 MVP 49 + G5 20 = **232**（v3 225）
- M3 = M2 232 + G3d 30 + G3e 26 + G3f 24 + G4 完整 58 = **370**（v3 357）
- 含 samples 4 → M1 65 / M2 236 / M3 **374**

---

## 5. 可执行验收清单（补 §4.3 的不可判定项）

### 5.1 「40 个最常用控件」抽样规则与候选名单

**规则（三层取并集，取 41 个）**：
1. **示例应用实际使用**的控件（§5.3 的 6 页并集，34 个）——可执行、可复核；
2. **每个组件族至少 1 个非示例使用控件**（Charts/Pickers 等在外层补充）；
3. **必须含契约最难的类型**：1 个图表（无模板）、1 个自绘控件、1 个数据集合控件、1 个浮层控件。

**候选（41 个）**：ArcInput、ArcPasswordField、ArcPasswordStrength、ArcCheckbox、ArcSwitch、ArcSlider、ArcSelect、ArcCombobox、ArcMultiSelect、ArcTagInput、ArcTextarea、ArcNumberField、ArcMoneyInput、ArcRadioGroup、ArcRadioCards、ArcSegmentedControl、ArcChipGroup、ArcSearchField、ArcDatePicker、ArcTimePicker、ArcCalendar、ArcFileDropzone、ArcAvatar、ArcProgress、ArcEmptyState、ArcAlert、ArcActionButton、ArcUserMenu、ArcThemeSwitcher、ArcTabs、ArcBreadcrumb、ArcPagination、ArcDialog、ArcDrawer、ArcPopover、ArcTooltip、ArcToastHost、ArcDataGrid、ArcTreeTable、ArcTimeline/ArcStepper（二选一）、ArcLineChart（图表代表）。

> 验收人：实现者逐项填表 + Lead 抽查 10 个；证据 `artifacts/acceptance/d7/<Control>.md`（含截图/反射输出/测试名）。

### 5.2 D7 第 1–6 条逐控件检查表（可脚本化）

| D7 条 | 判定方法（可执行） | 通过标准 |
|---|---|---|
| 1. ControlTheme | 在 `src/Components/*/Themes/*.axaml` grep `<ControlTheme … TargetType="{x:Type <C>}"`；或反射模板后 `Control.Theme` 非空 + 改主题键后截图变化 | 有 `x:Key={x:Type}` 的 ControlTheme；自绘图表有可覆盖的样式入口（C1e 后） |
| 2. TemplatePart | 反射 `type.GetCustomAttributes<TemplatePartAttribute>()`；并 grep 模板 `x:Name="PART_*"` | 每个 `GetTemplateChild/NameScope.Find` 的部件都有 `[TemplatePart]` 且名 `PART_*` |
| 3. RoutedEvent / ICommand | 反射 `type.GetFields().Any(f => f.FieldType == typeof(RoutedEvent))`；或控件有 `ICommand` 型 DP | 主要交互结果可冒泡（或走 ICommand）；生命周期事件允许 CLR event（P0-5 修订） |
| 4. 数据集合（仅数据行控件） | 反射有 `ItemsSourceProperty`；且 ItemsSource 变更测试（headless：设置 2 组数据，断言容器数正确） | 数据行控件通过；配置型集合标记 N/A（P0-4） |
| 5. 视觉状态 | grep `PseudoClasses`/`Classes.Set("…")` + 状态枚举；双主题截图中可见 Loading/Empty/Error/Disabled/Hover/Focus | 状态由枚举/伪类表达，无零散 bool 组合爆炸 |
| 6. 键盘路径 | headless 发送键：Tab/Shift+Tab、方向键、Home/End、Esc、Enter/Space；断言焦点/选中/关闭 | 每控件有键位表且断言全绿；`:focus-visible` 截图可见 |

### 5.3 示例应用 6 页 × 控件矩阵（M2 ①）

| 页面 | 必须使用的控件/服务 | 验收断言 |
|---|---|---|
| 登录 | ArcInput、ArcPasswordField、ArcCheckbox、ArcActionButton、ArcAlert、ArcCard | 校验/错误态/Enter 提交/焦点环 |
| 列表 | ArcDataGrid（或 Feature.DataGrid）、ArcPagination、ArcFilterToolbar、ArcEmptyState、ArcSearchField | 1k 行分页可用；排序/过滤/空态；键盘行导航 |
| 表单 | ArcTextarea、ArcNumberField、ArcMoneyInput、ArcSelect、ArcDatePicker、ArcRadioCards、ArcSwitch、ArcSlider | 标签/错误/禁用态；Tab 顺序；ICommand 提交 |
| 对话框 | ArcDialog、ArcDrawer、ArcPopover、ArcTooltip、ArcToastHost、ArcActionButton + `IDialogService`/`INotificationService` | 服务调用可无头测试；Esc/焦点陷阱/还原焦点 |
| 文件上传 | ArcFileDropzone + `IFilePickerService`（替身）、ArcProgress、ArcAlert | 注入替身可测；大小/类型/上限错误态 |
| 设置 | ArcTabs、ArcSwitch、ArcRadioGroup、ArcSegmentedControl、ArcUserMenu、ArcThemeSwitcher、ArcBreadcrumb + `IThemeService` | 主题切换即时生效；tab 键盘；保存/重置 |

> 工程：`samples/Arc.Avalonia.Samples`（WinExe），每页一个 headless 冒烟测试 + 双主题截图；`samples/` 同时也是 R2 门禁的叶子依赖。

### 5.4 「10k 事件流式渲染不卡」的度量（M2 ③，控件级，非渲染层）

**方法**：headless 或离屏窗口加载 `ArcAgentTranscript`，注入 10,000 条 `ArcAgentEvent`，再以 **60 条/秒** 追加 600 条；在 UI 线程处理批次处用 `Stopwatch` 采样，记录视觉树容器数、堆增长与重建次数。

| 指标 | 建议阈值（POC 校准） | 证明什么 |
|---|---|---|
| 视觉行容器数（TextBlock/Border 行级） | ≤ `2 × ceil(viewportHeight/rowHeight) + 8` | 窗口化/虚拟化生效（不是 10k 容器） |
| 单批增量处理 p95 / p99 | ≤ 8 ms / ≤ 16 ms | 流式不阻塞 UI |
| 全量重建次数 | ≤ 2 次 / 1,000 增量 | 增量追加而非全量 Rebuild |
| GC 后托管堆增长 | ≤ 150 MB（10k 事件常在） | 无 O(n²) 分配 |

> 注：不测「帧时间/1000 fps」等渲染指标——那是 Avalonia 渲染层职责（v3 §2.0）；以上均为**控件级**指标，符合用户口径。

### 5.5 M3 验收（RTL / DPI / 无障碍）

| 项 | 范围 | 通过标准 |
|---|---|---|
| RTL | §5.1 的 41 控件 × Light/Dark，`FlowDirection=RightToLeft` | 无文字截断/图标方向错误（方向性图标有镜像表）；6 示例页截图人工抽检 |
| DPI | 41 控件 × 1.25/1.5/2.0；6 示例页 | 固定尺寸不裁剪/不重叠；行高/图标/圆角按比例；人工抽检记录 |
| 无障碍 | 15 个关键控件（ArcInput、ArcSearchField、ArcCheckbox、ArcSwitch、ArcSlider、ArcSelect、ArcCombobox、ArcTabs、ArcDialog、ArcDataGrid、ArcTreeTable、ArcTreeView、ArcCalendar、ArcToast、ArcPagination） | 反射非空 AutomationPeer；角色/值模式断言；读屏抽检记录（每控件 1 条） |
| D7 全量 | 151 控件 | 满足 D7 1–10，或进入显式例外清单（Charts/内部部件/Agent 等，随包发布） |

---

## 6. 证据索引

| 证据 | 位置 |
|---|---|
| C3 原文与 20 人日 | v3 `:16`、`:35`、`:363`；WBS `:469`；§4.4 自述「重写级别」`:506`；§4.6「需自研」`:532` |
| 数据控件非 ItemsControl | `ArcDataGrid.cs:126`、`ArcTreeTable.cs:116`、`ArcTreeView.cs:33`（均 `TemplatedControl`） |
| DataGrid 全量行构建 | `ArcDataGrid.cs:1029`（Clear）、`:1082-1088`（StackPanel + 全行）、`:1091`/`:1188`（手搭行/列） |
| TreeTable 全量行构建 | `ArcTreeTable.cs:1236`（Clear）、`:1258-1266`（foreach 全 `_display`） |
| TreeView 全量行构建 | `ArcTreeView.cs:148`（Clear）、`:152-176`（递归 AddNode 全展开节点） |
| 自述无虚拟化 | `ArcDataGrid.cs:115-125`（`:120` row virtualisation）；`ArcTreeTable.cs:106-108`（`:107`） |
| 行高常量（分页/裁剪可行性） | `ArcDataGrid.cs:128`（40）；`ArcTreeTable.cs:118-122`（44/40/20/44） |
| ChatThread 全量重建 | `ArcChatThread.cs:453-467`（RenderMessages Clear + 全量）、`:948-958`（Send → RenderMessages） |
| Transcript 全量重建 | `ArcAgentTranscript.cs:123`（`: Panel`）、`:126`（`IEnumerable`）、`:152-159`（变更即 Rebuild）、`:217`（Clear）、`:239-245`（全量 + ScrollViewer） |
| 已有虚拟化基座 | `ArcVirtualList.cs:29-40`（ListBox+VSP）、`:59`（ItemsSource）、`:71`（Fluent 主题）；仅 `VirtualListDemoPage.cs:39,63-64` 使用 |
| 已有 ItemsControl/DataTemplate | `ArcEmojiPicker.axaml:378-386`（ListBox 网格）、`:395-424`（ItemsControl/DataTemplate）；`ArcEmojiPicker.cs:824`；`ArcTimePicker.cs:204` |
| `docs/04` 原始口径 | `04:28`（仅 EmojiPicker 行级虚拟化）、`04:215`（18 处）、`04:350`（**C# 侧** 0、XAML 7 处） |
| Children.Clear 实测 | Components 非 Pages：91 处 / 65 文件（本文复核） |
| 契约计数 | 143 public event、0 `RoutedEvent.Register`、0 `[TemplatePart]`、200 `OnApplyTemplate`、10 个 `*Size` enum |
| 151 控件分解 | 106 有 ControlTheme + 20 自绘图表 + 5 继承 + 4 基类 + 16 内部部件（B §3.2） |
| 迁移范围 | `Pages/` 328 文件：Components 140 / Blocks 111 / Special 56 / Agent 13 / Apps 8；Docs 294：128+111+55；Demo 引用 26/29（`AgentUI.csproj:10`、`Blocks.AI.csproj:14`、`Special.AIWidgets.csproj:10` 等） |
| D7-4 影响面 | 组件层集合型 `StyledProperty` 130 个（`Items` 15、`Options` 9、`Labels` 6、`Series` 6、`Columns` 4、`Rows` 4、`Segments` 4…） |
| DPI 无代码 | 组件层 `RenderScaling/Dpi/DpiScale/ScaleFactor` = 0 处 |
| 基线差异 | v3 `:64-68`、`:675-679` vs 本次复核（137/131、328/140+111+56+13、294/298、26/29、763 文件/181,230 非空行） |

---

## 7. 未验证 / 需 POC

1. **Avalonia 层级虚拟化**：Avalonia 是否提供「树/层级虚拟化」**未验证**——本地 12.1.4 包内 XML doc 只描述 `VirtualizingStackPanel` 的「single line」列表语义。若确无，则需把树展平后交给 `VirtualizingStackPanel`；其与展开/折叠、焦点保持、列宽共享组合后的表现待 POC 1（1–2 人日）验证，建议排在 M2 前。
2. **官方 DataGrid 与 Arc 主题**：`Avalonia.Controls.DataGrid`（本地缓存 12.0.0/12.1.0/12.1.2）行/列虚拟化可用，但 Arc 主题覆盖成本与视觉一致性**未验证** → POC 2（2 人日）。
3. **10k 事件指标阈值**：本文 8/16 ms、150 MB 为建议值，需在目标机器上校准 → 归 M2 门槛前的 POC 3。
4. **`ArcVirtualList` 提取**：它当前是 `ListBox` 派生且依赖 Fluent ListBox 主题（`ArcVirtualList.cs:71`）；提到 L2 并在 `ItemsControl` 语义下复用，需验证是否保留 ListBox（含选择/焦点）还是改为 ItemsControl+自定义容器。
5. **§5.1 的 41 控件名单**是按「示例应用需要 + 族覆盖」构造的候选，Lead 可增删；一旦确定应写入 `docs/13` 附录，避免 M2 验收时重新定义。

---

### 附：本评审一句话

**v3 的中间层定位与 WBS 骨架成立，但 C3 把「数据驱动」写成了「改个 ItemsSource 就能白拿虚拟化」，实际是对 4,635 行自绘组合代码的重写；同时 G2/G3a 与 C1/C3 各有一笔重复计价。修完 P0-1~P0-5（C3 降级为 25、C1 重排 53、G2 27、验收可判定），总账从 357 变为 374（+17）；若坚持 M2 内完成三控件真数据驱动，则 389–409。**
