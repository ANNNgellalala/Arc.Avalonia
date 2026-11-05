# 13 评审汇总（v3 → v3.1）：4 份对抗式评审的结论、去重与采纳记录

> **评审对象**：`docs/13-framework-layering-and-agent-top.zh.md`（v3，2026-11-05，717 行）
> **评审分工**：R1 基础层/主题/依赖边界与包拓扑；R2 组件能力/虚拟化/WBS 去重；R3 Agent API/口径/里程碑；R4 竞品可替代性（AtomUI/Semi/Ursa）。
> **评审方式**：4 名评审员 = 原审计员（各自带审计上下文）+ 只读静态核对；未改源码、未构建。
> **产出**：本汇总 + 主文档 v3.1（§10 评审记录）+ 4 份评审原文。
> **总判定**：**方向通过，估算偏乐观，三处架构缺陷需修**；修后总量 **357 → 442 人日（288–594）**。
> **决策状态（2026-11-05 已确认，v3.2）**：D-1 定位=同生态位、不承诺 drop-in；D-2 Agent=M 档；D-3 网格=自有+官方 Feature 分工；D-4 Top 15–20 清单认可。

---

## 1. 评审结论速览

| 维度 | v3 | 评审发现 | v3.1 |
|---|---|---|---|
| "无其他 UI 依赖" | 已满足，门禁 1 人日 | 只看直接依赖会被间接包绕过（R4） | 门禁升级为**传递闭包**检查 |
| 主题 | 10 人日"定向覆盖" | 只覆盖 6 类，**不能支撑"替代 AtomUI"**；真实中性 15 人日（R1）；AtomUI 全量换肤（R4） | **30 人日**：基础适配 15 + **Top 15–20 内建控件** 15 |
| 主题包分层 | L1 独立包聚合 L2 主题 | **依赖方向倒置**，一行接入只在元包成立（R1） | **L2.5 聚合层**（显式 allowlist） |
| 基础层 | 纯度 5 人日 | `Internal` 被 **12 个 csproj** 引用，解散未立项，会卡 `pack`（R1） | +5 人日，进 M1 |
| 文档出库 | 268 文件 | 库内真实迁移面 **614 文件**（320 页 + 294 文档）（R2） | 9 人日 |
| 数据驱动列表 | 20 人日"用 Avalonia 虚拟化" | 三控件是 TemplatedControl 手工建行（4,635 行），字面 **40–60**（R2） | 降级方案 25（列表复用 + 树展平 + 网格官方 DataGrid Feature） |
| 组件契约 C1 | 44 | 范围未定义；定性后 **53**（Top-40 事件 + 106 TemplatePart + Charts）（R2） | 53 |
| Agent | 单会话 `IArcAgentSession`；"帧级 16ms 合并" | 缺多会话 Workspace（3 个跨 run 控件驱动不了）；帧级=渲染层，违反口径；门槛不可判定（R3） | Workspace API、Dispatcher 批处理、量化门槛、BannedApi |
| 竞品差距 | 未评估 | 图标 161 vs 843、本地化 0、Theme API 无（R4） | 图标扩展 +4、Theme API +4、本地化最小集前移 M2 |

---

## 2. 发现登记册（合并去重后，按严重度）

### P0（必须修，已全部采纳）

| ID | 来源 | 问题 | 证据 | 修复 | 人日 |
|---|---|---|---|---|---:|
| P0-1 | R1 | `Arc.Avalonia.Theme` 在 L1 却聚合 11 个 L2 主题，依赖倒置；单独装 Theme 时组件主题 URI 解析失败 | 主文档 v3 §2.3；`Gallery/App.axaml:8-32` 共 26 个 StyleInclude | Theme 改 **L2.5 聚合层**，元包依赖 Theme；架构测试 allowlist | +2 |
| P0-2 | R1/R4 | 10 人日主题适配是乐观下界；只覆盖 6 类，裸控件仍是 Fluent，不能声称替代 AtomUI | A 报告主题分档；AtomUI 全量换肤 [R4] | 主题 **30 人日**：基础适配 15 + Top 15–20 覆盖 15；POC 先行 | +20 |
| P0-3 | R1 | `Arc.Avalonia.Internal` 解散未进 WBS，却阻塞 M1 `dotnet pack` | 12 个 csproj 引用 + 6 处 `InternalsVisibleTo` | 解散 + 类迁移进 G1 | +5 |
| P0-4 | R2 | C3"数据驱动列表"把自绘网格当 ItemsControl，20 人日不可行 | `ArcDataGrid.cs:1029/1082`、`ArcTreeTable.cs:1236/1258` 手工建行；自述无虚拟化 | 降级方案 25；真重写列为 M3 可选 +20–30 | +5 |
| P0-5 | R2 | G2 的"API 收敛 13"与 G3a 重复；C1 与 C3 的 ItemsSource 重复 | WBS v3 G2/G3a | 拆分：G2 留 demo 3；API 收敛归 G3a；C1 剔除重复 | −10 |
| P0-6 | R2 | C1 44 人日"范围未定义"，实际按 Top-40 事件 + 106 控件 TemplatePart 需 53 | B 报告 §7 原估 M/S/L | C1 = 53 | +9 |
| P0-7 | R3 | 缺多会话/多 run Workspace；3 个跨 run 控件（后台 dock/注意力收件箱/run 控制条）驱动不了 | AgentUI 6 控件职责；C 报告 §4.3 亦只有单会话 | 新增 `IArcAgentWorkspace`/`ArcAgentRunRef`/跨 run 队列 | +8 |
| P0-8 | R3 | "帧级 16 ms 合并"违反"不做渲染层"；M2/M3 门槛不可判定；Host 边界未 CI 化 | 主文档 v3 D8/§4.3/§6 | 改 `Dispatcher` 批处理；headless 断言 + 桌面 p95；BannedApi CI | +1.5+3+2 |
| P0-9 | R4 | 图标 161 且无扩展点；Theme API 缺失（自定义 accent/token）；本地化在 M3 太晚 | `ArcIconData.Paths` 只读；8 固定 accent；0 `.resx` | 图标 Provider + 100 个高频；`ArcThemeOptions`；本地化最小集前移 M2 | +8 |

### P1（发布/验收前修，已采纳）

| ID | 来源 | 问题 | 处理 |
|---|---|---|---|
| P1-1 | R1 | `ArcTheme` 类/资源文件/命名空间/包名四重同名 | 资源文件改名 `ArcBaseStyles.axaml`；类保留 `ArcTheme` |
| P1-2 | R1 | 接入现状是 26 条 StyleInclude，不是 13 条 | 文档修正；目标是 1 行 |
| P1-3 | R1 | Fluent 覆盖必须用 `ThemeDictionaries` + `FluentTheme.Palettes` | 写入 D3 机制与 POC |
| P1-4 | R1 | `PackageId≠AssemblyName` 不必要 | 采纳"程序集也改名"（仅 6 处 avares） |
| P1-5 | R2 | 614 文件出库（非 268） | G2 计 9 人日 |
| P1-6 | R2 | DPI 标度属渲染层 | 改为"尺寸 token 复核"（C6） |
| P1-7 | R2 | 验收不可判定（40 控件/10k 事件） | 给出 41 控件名单、6 页矩阵、headless 阈值、桌面 p95 |
| P1-8 | R2 | samples / 键盘焦点契约 / 控件级性能缺失 | samples 4、契约并入 C1、控件级性能并入 C7 |
| P1-9 | R3 | "渲染器注册表"易误读为渲染层 | 改"条目视图注册"（`IArcAgentItemViewRegistry`） |
| P1-10 | R3 | 事件模型缺 `EventId`/run 类型/parentRunId/乱序策略 | 并入 Workspace 设计 |
| P1-11 | R3 | G4/G5 漏 AgentUI 合并、LifeToday 迁移、13 场景页归属 | 并入 G5（+4） |
| P1-12 | R3 | 元包含 Agent 与"Agent 独立回滚"承诺矛盾 | 元包拆分说明 + 回滚以包为单位 |
| P1-13 | R3 | 许可未覆盖 AG-UI 语义参考 | D10 补声明 |
| P1-14 | R4 | 黑/白名单补漏 | 黑名单加 AtomUI 全族/Material.Icons/AntDesign/IconPacks/Svg.*/DialogHost 等；白名单加 Remote.Protocol/BuildServices/平台包/SkiaSharp 等 |

### P2（收尾，已采纳或标注）

| ID | 问题 | 处理 |
|---|---|---|
| P2-1 | 基线数字口径混用（774/182,621 含 `.recon`；328 vs 316；298 vs 294；131 vs 137；"23 断言" vs checks=34） | 已改为：764 文件 / 181,358 行（src+tests）；328 页文件（294 条目页+21 页类）；294 Docs；112 key/137 元素；统一标口径 |
| P2-2 | §7"不做"未含"不自建合并节奏/帧时钟""不做多会话持久化" | 已补 |
| P2-3 | 主题类命名/包名易混 | 已在 D3/§2.3 说明 |

---

## 3. 去重后的净增量（357 → 442）

| 变更 | 人日 |
|---|---:|
| 主题：10 → 30（基础 15 + Top 15–20 覆盖 15） | +20 |
| `Internal` 解散 | +5 |
| Theme 聚合层/POC/命名 | +2 |
| 门禁（传递依赖、无渲染扫描、BannedApi、术语） | +4 |
| G2 净变化（去重 −10、614 文件 +5、samples/API/LICENSE +9） | +4 |
| 组件契约 C1 44 → 53 | +9 |
| 数据驱动列表 20 → 25（降级方案） | +5 |
| 尺寸 token / 测试 / 键盘契约（G3e/G3f） | +6 |
| Agent Workspace/批处理/门槛/迁移/边界 CI（G4 +20，G5 +3） | +23 |
| 图标扩展 + Theme API | +8 |
| **合计** | **+86** |
| **v3.1 总量** | **442（288–594）** |

---

## 4. 评审后的 WBS 与里程碑（摘要）

| 包 | 人日 |
|---|---:|
| G0 治理与门禁 | 20 |
| G1 基础层/主题/聚合入口 | 44 |
| G2 文档/示例解耦与发布 | 41 |
| G3a 组件契约与 API 收敛 | 53 |
| G3b 服务层 + 窗口壳 | 24 |
| G3c 数据驱动列表（降级） | 25 |
| G3d 无障碍 + 本地化/RTL | 30 |
| G3e DnD/MVVM/容器/尺寸 token | 26 |
| G3f 测试基线 | 24 |
| G3g 图标体系 | 4 |
| G3h Theme API | 4 |
| G4 Agent 顶层（MVP 54 / 完整 124） | 124 |
| G5 迁移收尾 | 23 |
| **合计** | **442（288–594）** |
| 可选：网格真数据驱动重写 | +20–30 |
| 可选：Agent L 档（06 长尾 7 项） | +31 |

| 里程碑 | 人日 | 含义 |
|---|---:|---|
| M1 α | ≈90（62–118） | 分层 + 可发布（库形态） |
| M2 β | ≈298（225–384） | 通用 UI + Top 皮肤 + Agent MVP（与 AtomUI 同生态位） |
| M3 1.0 | ≈442（288–594） | 全量成熟 |

---

## 5. 决策确认（2026-11-05，v3.2）

| # | 问题 | 决策 | 对文档的影响 |
|---|---|---|---|
| D-1 | 是否承诺 drop-in 替代 AtomUI | **不承诺**：与 AtomUI/Semi/Ursa **同一生态位**，以设计一致性/组件覆盖/文档对标 | 删除"替代"表述与替代验收；改为"生态位对齐"；不追求 78 全量换肤/843 图标 |
| D-2 | Agent "完整"口径 | **C 报告 M 档（124 人日）** | L 档（+31，06 长尾 7 项）列 post-1.0 |
| D-3 | 网格策略 | **自有 `ArcDataGrid`（小数据）+ `Arc.Avalonia.Feature.DataGrid`（大数据）分工** | 取消"自研真虚拟化 +20–30"可选项 |
| D-4 | Top 15–20 皮肤清单 | **认可** | 按清单实施；未覆盖控件文档标注 Fluent 外观 |

> 结论：WBS 维持 **442（288–594）**；M1 ≈90 / M2 ≈298 / M3 ≈442；剩余可选项仅 Agent L 档（post-1.0）与图标继续扩充（按需）。

---

## 6. 评审原文索引

| 编号 | 文件 | 行数 | 结论 |
|---|---|---:|---|
| R1 | [`13-review-a-foundation-deps.zh.md`](13-review-a-foundation-deps.zh.md) | 459 | 3 P0 / 7 P1 / 8 P2；方向认同，估算与分层需修 |
| R2 | [`13-review-b-components-wbs.zh.md`](13-review-b-components-wbs.zh.md) | 370 | 15 findings；C3 最大低估；两处重复计价；验收需可判定 |
| R3 | [`13-review-c-agent-gates.zh.md`](13-review-c-agent-gates.zh.md) | 686 | 16 findings；多会话 Workspace 是最大 API 缺口；口径/门槛需修 |
| R4 | [`13-review-d-competitor-parity.zh.md`](13-review-d-competitor-parity.zh.md) | 322 | v3 不能 drop-in 替代 AtomUI；建议 +21–33；名单补漏 |
