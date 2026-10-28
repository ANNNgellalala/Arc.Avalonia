# 目录重构实施记录（P0 + P1 + P2）

> 依据：[`08-project-structure-review.zh.md`](08-project-structure-review.zh.md) 的分析与建议。
> 执行日期：2026-10-29。回滚点：git `baseline` 标签（重构前）与分阶段提交。
> 最终验证：`dotnet build` 0 error；`VERIFY: checks=34 failures=0`；
> `CATALOG AUDIT: pages=233 items=294 missing=0 pageFailures=0`；
> `GALLERY SMOKE: clicks=942 pageShows=699 itemShows=588 failures=0`。

## 1. 结果概览

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| P0 | `.gitignore` / `bin-alt` 清理、slnx 分组、重复类型处理、无头验证工程 | ✅ |
| P1a | 新增 `Arc.Avalonia.AgentUI`、`Arc.Avalonia.LifeToday`，收拢 14 个 P0 控件与页面 | ✅ |
| P1b | `AdvancedInputs` → `Inputs`；`AdvancedActions` → `Actions` | ✅ |
| P1c | 新增 `Arc.Avalonia.Internal` 共享原语 | ✅ |
| P1d | `Actions` → `Actions` + `Navigation` + `Overlays` | ✅ |
| P1e | `Feedback` → `Feedback` + `DataDisplay` | ✅ |
| P2a | `src/` 物理分组为 Foundation / Components / Agent / Special / Blocks / Apps | ✅ |
| P2b | `Special*` / `Blocks*` 项目改名 `Special.*` / `Blocks.*`，同步 AssemblyName / 命名空间 / avares URI | ✅ |
| P2c | 统一 `Charts` / `Blocks.Data` 命名空间为 `.Controls` / `.Pages` | ✅ |

重构后：`src/` 下 **28 个源项目**，加上 `tests/Arc.Avalonia.Tests` 共 **29 个解决方案项目**
（重构前 24 个源项目）。依赖保持单向：Foundation → Demo → Components / Agent / Special → Blocks → Gallery。

## 2. 最终目录结构

```
src/
├── Foundation/
│   ├── Arc.Avalonia                     tokens/主题/动效/图标/5 基础控件
│   └── Arc.Avalonia.Internal            共享内部原语（menu/glide/icon button/feedback helpers）
├── Components/
│   ├── Arc.Avalonia.Inputs              Text fields + special inputs + selects + toggles + sliders
│   ├── Arc.Avalonia.Actions             Buttons + menus + disclosure + messages
│   ├── Arc.Avalonia.Navigation          Tabs + breadcrumb + pagination + mega menu
│   ├── Arc.Avalonia.Overlays            Dialog + drawer + popover + tooltip + share access
│   ├── Arc.Avalonia.Feedback            Progress + avatars + cards + presence + activity
│   ├── Arc.Avalonia.DataDisplay         Tables + tree + code/json + filter toolbar + data grid
│   ├── Arc.Avalonia.Charts
│   ├── Arc.Avalonia.Pickers
│   ├── Arc.Avalonia.Effects
│   ├── Arc.Avalonia.Content
│   └── Arc.Avalonia.Media
├── Agent/
│   ├── Arc.Avalonia.AgentUI             P0 Agent UI（6 控件 / 6 页）
│   └── Arc.Avalonia.LifeToday           P0 Today / 生活域（7 控件 / 7 页）
├── Special/
│   ├── Arc.Avalonia.Special.Glass
│   ├── Arc.Avalonia.Special.Motion
│   ├── Arc.Avalonia.Special.Media
│   ├── Arc.Avalonia.Special.AIWidgets
│   └── Arc.Avalonia.Special.AIExplainers
├── Blocks/
│   ├── Arc.Avalonia.Blocks.Marketing
│   ├── Arc.Avalonia.Blocks.Commerce
│   ├── Arc.Avalonia.Blocks.App
│   ├── Arc.Avalonia.Blocks.Team
│   ├── Arc.Avalonia.Blocks.Data
│   └── Arc.Avalonia.Blocks.AI
└── Apps/
    ├── Arc.Avalonia.Demo                页面契约、DemoUi、目录、反射发现
    └── Arc.Avalonia.Gallery             展示应用、截图、--audit / --smoke
tests/Arc.Avalonia.Tests                 无头验证工程
```

## 3. P2 详细变更

### P2a：物理分组

* 28 个项目目录移入 6 个分组目录；`ArcUI.slnx` 路径同步重写。
* 所有 `.csproj` 的 `ProjectReference` 按新相对路径重写；`tools/shots.ps1` 的 Gallery 路径同步更新。
* 解决方案文件夹保持 `/Foundation`、`/Components`、`/Agent`、`/Special`、`/Blocks`、`/App`、`/Tests`。

### P2b：项目改名

| 旧项目 | 新项目 |
| --- | --- |
| `Arc.Avalonia.Special.Glass` | `Arc.Avalonia.Special.Glass` |
| `Arc.Avalonia.Special.Motion` | `Arc.Avalonia.Special.Motion` |
| `Arc.Avalonia.Special.Media` | `Arc.Avalonia.Special.Media` |
| `Arc.Avalonia.Special.AIWidgets` | `Arc.Avalonia.Special.AIWidgets` |
| `Arc.Avalonia.Special.AIExplainers` | `Arc.Avalonia.Special.AIExplainers` |
| `Arc.Avalonia.Blocks.Marketing` | `Arc.Avalonia.Blocks.Marketing` |
| `Arc.Avalonia.Blocks.Commerce` | `Arc.Avalonia.Blocks.Commerce` |
| `Arc.Avalonia.Blocks.App` | `Arc.Avalonia.Blocks.App` |
| `Arc.Avalonia.Blocks.Team` | `Arc.Avalonia.Blocks.Team` |
| `Arc.Avalonia.Blocks.Data` | `Arc.Avalonia.Blocks.Data` |
| `Arc.Avalonia.Blocks.AI` | `Arc.Avalonia.Blocks.AI` |

目录名、`.csproj` 文件名、`RootNamespace`、`AssemblyName`、C# 命名空间、XAML `xmlns`、
`avares://` 资源 URI、csproj 引用、slnx、文档全部同步（共更新 297 个文件）。

### P2c：命名空间统一

* `Arc.Avalonia.Charts` 拆为 `Arc.Avalonia.Charts.Controls`（`Controls/`、`Core/`）与
  `Arc.Avalonia.Charts.Pages`（`Pages/`）；Charts 项目增加 `global using Arc.Avalonia.Charts.Controls;`。
* `Arc.Avalonia.Blocks.Data` 的控件改为 `Arc.Avalonia.Blocks.Data.Controls`，页面保持 `.Pages`，
  项目内增加 `global using Arc.Avalonia.Blocks.Data.Controls;`。
* 所有外部引用（Blocks 页面、`ChartTone` alias 等）同步更新。

## 4. 验证

```text
dotnet build ArcUI.slnx                                              → 0 error
dotnet tests\Arc.Avalonia.Tests\bin\Debug\net10.0\Arc.Avalonia.Verify.dll
                                                                     → VERIFY: checks=34 failures=0
dotnet src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --audit
                                                                     → CATALOG AUDIT: pages=233 items=294 missing=0 pageFailures=0
dotnet src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --smoke
                                                                     → GALLERY SMOKE: clicks=942 failures=0
```

构建 warning 均为重构前既有的 analyzer 提示（未新增错误类别）。已抽查
`navigation`、`data-grid`、`charts`、`glass-menu` 页面的明暗截图，确认主题与 avares 资源在改名/分组后仍正确装配；
全量 466 张截图未重跑。

## 5. 提交历史

```
60c3377 baseline
ecb6f8b P0: solution folders / renames / bin-alt
b6dcd31 P0: headless verification project
744773e P1: AgentUI + LifeToday
0924fa1 P1: Arc.Avalonia.Internal
34d9c90 P1: Actions → Actions/Navigation/Overlays
f2fc693 P1: Feedback → Feedback/DataDisplay
e7f3b2e docs: Phase 1+2 record
ae6df34 P2a: group src projects into Foundation/Components/Agent/Special/Blocks/Apps
e32ea30 P2b: rename Special/Blocks projects to dotted names and sync avares/namespaces
bfc99bf P2c: unify Charts and Blocks.Data namespaces into .Controls/.Pages
```

## 6. 后续可选项

* 全量截图回归（`pwsh -NoProfile tools/shots.ps1`，约 466 张）与视觉对比；
* 为 `Arc.Avalonia.Gallery` 增加 CI 入口（build + verify + audit + smoke）；
* 若后续继续扩展，可按 catalog 23 类进一步评估 `Inputs` / `Actions` 的内部子目录组织。
