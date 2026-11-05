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

## 决策

### 1) 层映射（由 `tools/architecture-allow.json` 的 `layerPrefixes` 决定，脚本不硬编码）

| 层 | 路径前缀 | 代表工程 | 允许的 ProjectReference 目标层 |
|---|---|---|---|
| L1 | `src/Foundation/` | `Arc.Avalonia`、`Arc.Avalonia.Internal` | 无（仅 L0 包；`Arc.Avalonia.Internal` 例外见 E1） |
| L2 | `src/Components/` | 11 个组件族 | L1、L2 |
| L2f | `src/Feature/` | `Arc.Avalonia.Feature.*` | L1、L2（另允许官方 DataGrid/ColorPicker 包） |
| L2.5 | `src/Theme/` | `Arc.Avalonia.Theme`（M1-T3） | L1、L2（docs/13 §2.3 聚合例外，见 E3） |
| L3 | `src/Patterns/`、`src/Special/`、`src/Blocks/` | 页面级组合 | L1、L2（见 E2） |
| L4 | `src/Agent/` | AgentUI、LifeToday（M1 后迁 `Arc.Avalonia.Agent`） | L1、L2、L3 |
| app | `src/Apps/`、`src/Docs/`、`tests/` | Gallery、Demo、Docs、Tests | 任意层（宿主/叶子/测试） |

### 2) 规则（R1）

- 对每个 csproj 的 `ProjectReference`，解析目标工程 → 目标层；
- `app` 为宿主/测试，跳过出边；
- 目标在 `leafProjects`（Demo/Docs/Samples/Tests）中的边不参与 R1，交给 R2 判（避免同一问题双报）；
- `allowedTargetLayers[源层]` 含 `*` 或含目标层即通过；否则在 `allowedEdges` 里查显式例外；
- 无法解析的引用/无法映射的层 → 直接失败（fail-closed）。

### 3) 例外（必须在本 ADR 与 `architecture-allow.json` 同时登记）

- **E1 `Arc.Avalonia.Internal → Arc.Avalonia`（L1 内部边）**：Internal 是 Foundation 的辅助程序集
  （4 个视觉类 + 5 处 InternalsVisibleTo），M1-T2 解散后删除此例外。
- **E2 L3 允许直接引用 L1**：docs/13 §2.1 的表格写 L3→L2，但 11 个现有 L3 工程都直接使用 Foundation 的
  token/契约。这里的判定轴是**依赖方向而非相邻层**：L3→L2→L1 的下行方向没有改变，故允许 L3→L1。
  M1-T1 文档出库后可收紧为只允许 L2；收紧时改 `architecture-allow.json` 的 `allowedTargetLayers.L3`。
- **E3 L2.5 聚合**：`Arc.Avalonia.Theme` 需要 11 个 L2 包 + L1，属 docs/13 §2.3 显式批准的聚合例外；
  允许的目标仍是 L1/L2，若主题层需要其它边，必须在 `allowedEdges` 逐条登记。

### 4) R2（发布库边界）

发布库（L1/L2/L2f/L2.5/L3/L4）不得：
1. `ProjectReference` 到 `Arc.Avalonia.Demo` / `Arc.Avalonia.Docs*` / `Arc.Avalonia.Samples*`；
2. 含 `EmbeddedResource Include="Docs\*.json"`（或 `Pages/`）把文档/页面打进包。

已知红基线在 `tools/gates-baseline.json`：`R2.counts = { r2-demo-ref: 24, r2-embedded-docs: 24 }`，
`permittedProjects` 列出 24 个发布库。判定规则：
- 项目在 `permittedProjects` 内 **且** 同类计数不超过基线 → `knownred`（不阻塞，exit 0）；
- 新项目/超出计数 → `new`（RED，exit 1）；
- 基线计数下降 → 提示清理基线（M1 清完 R2 变 GREEN）。

> 仓库口径：引用 Demo 的 src 项目共 25 个（含 Gallery），连 tests 共 26 个 csproj；Gallery/tests 属 app，
> 不在发布库范围，所以 R2 只报 24 个。25 与 24 的差异已写进 `gates-baseline.json` 的 `context`。

## 实现与退出码

```powershell
pwsh -NoProfile tools/check-architecture.ps1          # 0=GREEN/KNOWN-RED，1=新增违规，2=无法检查
pwsh -NoProfile tools/check-architecture.ps1 -Quiet   # check-all 用
```

报告：`.recon/gates/architecture-<timestamp>.md|json` + `architecture-latest.json`（`.recon/` 已 gitignore）。
`check-all.ps1` 把 R1/R2 拆成两行显示，R1 期望 GREEN、R2 期望 KNOWN-RED。

## 验证结果（2026-11-05，工作树 M0 起点）

```text
[R1/R2 architecture] KNOWN-RED checked=141 new=0 knownRed=48 exempt=0
  R1 ProjectReference 只能向下                 GREEN      checked=115  new=0
  R2 发布库不引用 Demo/Docs/Samples            KNOWN-RED  knownRed=48（24 demo + 24 embedded docs）
```

- R1 扫描 27 个工程、115 条 ProjectReference 边，0 反向依赖、0 层映射未知；
- 反证：用 `-BaselinePath` 指向“permittedProjects 为空、计数清零”的临时基线时，R2 的 48 条全部变 `new`
  且 exit 1（已实测），说明 known-red 基线不会掩盖新违规；`check-all.ps1 -KnownRed` 严格模式同样 exit 1；
- R3/R4/R6/R8 的当前状态见 ADR 0002 与 `pwsh -NoProfile tools/check-all.ps1`。

## 后果

- 正向：M1 出库/重命名/新增 L2.5 包时，反向依赖与"库引用 Demo"会在本地一步暴露；known-red 不阻塞 M0。
- 代价：R1 的"允许目标层"与例外都在 JSON 里，评审必须同时看脚本与配置；E2 是放宽项，M1 后应重议。
- 遗留：循环依赖未单独检测（R1 只查方向）；L2 内部 DAG 由 docs/13 §2.1 约束，M1 后可在本脚本追加 SCC 检查。
