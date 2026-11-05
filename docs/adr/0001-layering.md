# ADR 0001 · 分层与依赖方向门禁（R1/R2）

- 状态：Accepted
- 日期：2026-11-05
- 范围：M0-3（docs/14 §4 R1/R2）；实现 `tools/check-architecture.ps1` + `tools/architecture-allow.json`
- 相关：[docs/13 §2.1/§2.2](../13-framework-layering-and-agent-top.zh.md)、[ADR 0002](0002-dependency-policy.md)

## 背景

docs/13 v3.2 把仓库目标形态定为 L0 官方平台 + L1 Foundation + L2 Components + L2.5 Theme 聚合
+ L3 组合层 + L4 Agent（+ L5 宿主）。R1 要求依赖只能沿 L0→L1→L2→L4 单向流动，L2.5 是显式的聚合例外；
R2 要求 Pages/Docs/DemoUi/Samples 只能作为叶子反向依赖库，发布库不得引用它们。

当前树处于 M1 出库之前：24 个发布库 csproj 仍引用 `Arc.Avalonia.Demo`，且都把 `Docs/*.json` 作为
`EmbeddedResource` 打进程序集。docs/14 §4 允许这一状态在 M1 前为 known-red，但门禁必须仍能发现"新增"违规，
否则 M1 之后无法靠它守门。

**M1 进展（2026-11-05 更新）**：
- M1-T1 出库完成：Demo 拆为 `src/Docs/Arc.Avalonia.Docs{,.Abstractions}`，320 页 + 294 文档迁入 Docs 叶子工程，
  发布库 Demo 引用与 EmbeddedResource Docs 均为 0 → **R2 known-red 已清零并转 GREEN**；
- M1-T2a 完成：`src/Foundation/Arc.Avalonia` 的 AssemblyName/PackageId 改为 `Arc.Avalonia.Foundation`，
  csproj 路径/根命名空间仍是 `Arc.Avalonia`；R1 的例外匹配需同时支持"名字"与"工程路径"（见 E1）；
- M1-T2b（解散 `Arc.Avalonia.Internal`）未完成，E1 例外保留。

## 决策

### 1) 层映射（由 `tools/architecture-allow.json` 的 `layerPrefixes` 决定，脚本不硬编码）

| 层 | 路径前缀 | 代表工程 | 允许的 ProjectReference 目标层 |
|---|---|---|---|
| L1 | `src/Foundation/` | `Arc.Avalonia`（PackageId `Arc.Avalonia.Foundation`）、`Arc.Avalonia.Internal` | 无（仅 L0 包；`Arc.Avalonia.Internal` 例外见 E1） |
| L2 | `src/Components/` | 11 个组件族 | L1、L2 |
| L2f | `src/Feature/` | `Arc.Avalonia.Feature.*` | L1、L2（另允许官方 DataGrid/ColorPicker 包） |
| L2.5 | `src/Theme/` | `Arc.Avalonia.Theme`（M1-T3） | L1、L2（docs/13 §2.3 聚合例外，见 E3） |
| L3 | `src/Patterns/`、`src/Special/`、`src/Blocks/` | 页面级组合 | L1、L2（见 E2） |
| L4 | `src/Agent/` | AgentUI、LifeToday（M1 后迁 `Arc.Avalonia.Agent`） | L1、L2、L3 |
| app | `src/Apps/`、`src/Docs/`、`tests/` | Gallery、Docs（叶子）、Tests | 任意层（宿主/叶子/测试） |

### 2) 规则（R1）

- 对每个 csproj 的 `ProjectReference`，解析目标工程 → 目标层；
- `app` 为宿主/测试，跳过出边；
- 目标在 `leafProjects`（Demo/Docs/Samples/Tests）中的边不参与 R1，交给 R2 判（避免同一问题双报）；
- `allowedTargetLayers[源层]` 含 `*` 或含目标层即通过；否则在 `allowedEdges` 里查显式例外；
- 无法解析的引用/无法映射的层 → 直接失败（fail-closed）。

### 3) 例外（必须在本 ADR 与 `architecture-allow.json` 同时登记）

- **E1 `Arc.Avalonia.Internal → Arc.Avalonia.Foundation`（L1 内部边）**：Internal 是 Foundation 的辅助程序集
  （4 个视觉类 + 5 处 InternalsVisibleTo）。M1-T2a 后 Foundation 的 AssemblyName/PackageId = `Arc.Avalonia.Foundation`，
  而 csproj 路径仍是 `src/Foundation/Arc.Avalonia/Arc.Avalonia.csproj`。该例外在
  `architecture-allow.json` 里同时写 `from/to`（名字）与 `fromProject/toProject`（仓库相对路径），
  脚本任一侧可用名字、csproj 文件名或路径匹配，因此改名/改路径都不会产生"误报"或"悄悄失效"。
  M1-T2b 解散 Internal 后删除此例外。
- **E2 L3 允许直接引用 L1**：docs/13 §2.1 的表格写 L3→L2，但 11 个现有 L3 工程都直接使用 Foundation 的
  token/契约。这里的判定轴是**依赖方向而非相邻层**：L3→L2→L1 的下行方向没有改变，故允许 L3→L1。
  M1-T1 文档出库后可收紧为只允许 L2；收紧时改 `architecture-allow.json` 的 `allowedTargetLayers.L3`。
- **E3 L2.5 聚合**：`Arc.Avalonia.Theme` 需要 11 个 L2 包 + L1，属 docs/13 §2.3 显式批准的聚合例外；
  允许的目标仍是 L1/L2，若主题层需要其它边，必须在 `allowedEdges` 逐条登记。

### 4) R2（发布库边界）

发布库（L1/L2/L2f/L2.5/L3/L4）不得：
1. `ProjectReference` 到 `Arc.Avalonia.Demo` / `Arc.Avalonia.Docs*` / `Arc.Avalonia.Samples*`；
2. 含 `EmbeddedResource Include="Docs\*.json"`（或 `Pages/`）把文档/页面打进包。

**M1-T1 出库后 R2 已零命中（GREEN）**：`tools/gates-baseline.json` 的 `R2.counts` 已清零为
`{ r2-demo-ref: 0, r2-embedded-docs: 0 }`、`permittedProjects` 为空、`expected=false`。
判定规则：
- 基线内项目/计数 → `knownred`（不阻塞，exit 0；当前不存在）；
- 任何新引用/新 EmbeddedResource Docs → `new`（RED，exit 1）；
- 历史基线（M0 起点 24+24，仓库口径 25/26 含 Gallery/tests）保留在 `gates-baseline.json` 的 `history`。

## 实现与退出码

```powershell
pwsh -NoProfile tools/check-architecture.ps1          # 0=GREEN/KNOWN-RED，1=新增违规，2=无法检查
pwsh -NoProfile tools/check-architecture.ps1 -Quiet   # check-all 用
```

报告：`.recon/gates/architecture-<timestamp>.md|json` + `architecture-latest.json`（`.recon/` 已 gitignore）。
`check-all.ps1` 把 R1/R2 拆成两行显示；M1-T1 出库完成后 R1/R2 都期望 GREEN。

## 验证结果

2026-11-05 M1-T1/T2a 集成后（本次修复 R1 例外匹配）：

```text
[R1/R2 architecture] GREEN checked=166 new=0 knownRed=0 exempt=0
  R1 ProjectReference 只能向下                 GREEN      checked=140  new=0
  R2 发布库不引用 Demo/Docs/Samples            GREEN      checked=26   new=0
```

- R1 扫描 30 个工程（src 29 + tests 1，含 Docs 叶子工程）、140 条 ProjectReference 边，0 反向依赖、0 层映射未知；
  修复前唯一失败是 `Arc.Avalonia.Internal [L1] -> Arc.Avalonia.Foundation [L1]`（allowedEdges 的
  `to: "Arc.Avalonia"` 未随 M1-T2a 改名），现按名字/工程路径双匹配通过；
- R2：发布库 Demo 引用 0、EmbeddedResource Docs 0（known-red 48 → 0），check-all 中 R2 行为 GREEN；
- 反证：`check-all.ps1 -KnownRed` 严格模式（R5 仍 known-red）exit 1；用 `-BaselinePath` 指向清空基线
  的临时文件时，新增的 leaf 引用会立即变 `new`（R2 清零后无需该开关也直接 RED）。

历史：M0 起点运行结果为 `R1 GREEN(115 边) / R2 KNOWN-RED(48)`，见 git 历史与
`tools/gates-baseline.json` 的 `history` 字段。

## 后果

- 正向：M1 出库/重命名/新增 L2.5 包时，反向依赖与"库引用 Demo"会在本地一步暴露；R2 已无 known-red。
- 代价：R1 的"允许目标层"与例外都在 JSON 里，评审必须同时看脚本与配置；E2 是放宽项，M1 后应重议；
  例外匹配是多字段的（名字/文件名/路径），改名时要同时核对 `allowedEdges` 的两组字段。
- 遗留：循环依赖未单独检测（R1 只查方向）；L2 内部 DAG 由 docs/13 §2.1 约束，M1 后可在本脚本追加 SCC 检查；
  M1-T2b 解散 Internal 后应删除 E1。
