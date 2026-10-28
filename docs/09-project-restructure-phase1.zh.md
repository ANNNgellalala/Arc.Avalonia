# 目录重构实施记录（Phase 1）

> 依据：[`08-project-structure-review.zh.md`](08-project-structure-review.zh.md) 的 P0 + 低风险 P1 建议。
> 执行日期：2026-10-29。回滚点：git `baseline` 标签（重构前）与逐步提交。
> 验证结果：`dotnet build` 0 error；`VERIFY: checks=34 failures=0`；`CATALOG AUDIT: pages=233 missing=0`；
> `GALLERY SMOKE: clicks=942 failures=0`。

## 1. 本次完成

### P0：工程卫生

| 项 | 变更 |
| --- | --- |
| 构建残留 | `.gitignore` 增加 `bin-alt/`、`TestResults/`；删除 `src/` 下 12 个 `bin-alt/` 构建目录 |
| 解决方案分组 | `ArcUI.slnx` 从单一 `/src/` 改为 `/Foundation/`、`/Components/`、`/Special/`、`/Blocks/`、`/App/`、`/Agent/`、`/Tests/` |
| 同名类型 | `Feedback.Internal.ArcUi` 重命名为 `FeedbackUi`（文件 `Internal/FeedbackUi.cs`）；基础层 `ArcUi` 保持公共 API 不变 |
| 图表重复 | Feedback 的 `ArcAnimatedCounter` / `ArcActivityHeatmap` 保持为目录项实现；Charts 中同名的渲染型变体重命名为 `ArcChartAnimatedCounter` / `ArcChartActivityHeatmap`（`ChartsMorePage` 已更新） |
| 滑块重复 | 合并进 `Inputs` 后出现的两个 `ArcSliderMark` 合并为 `Inputs/Controls/ArcSliderMark.cs` 单一类型 |
| 验证工程 | 新增 `tests/Arc.Avalonia.Tests`（控制台校验器，34 项断言：OKLCH 色彩、token、弹簧缓动、294 项目录）；加入 slnx |

> 说明：原计划使用 xUnit，但本机沙箱无法启动 vstest host（`Process.GetProcessHandle` 被拒绝），
> 因此验证工程改为无依赖的控制台程序；`dotnet run --project tests/Arc.Avalonia.Tests` 可直接执行。

### P1a：收拢 P0（Agent / Life）能力

新增两个项目，迁入原本散落在 8 个项目里的 14 个 P0 控件与 14 个页面：

* `src/Arc.Avalonia.AgentUI`
  * Controls：`ArcAgentTranscript`、`ArcRunControlBar`、`ArcToolApprovalSheet`、`ArcAgentAskCard`、`ArcBackgroundRunDock`、`ArcAgentAttentionInbox`
  * Pages：对应 6 个 P0 页
  * 依赖：`Arc.Avalonia` + `Arc.Avalonia.Demo`
* `src/Arc.Avalonia.LifeToday`
  * Controls：`ArcReminderRow`、`ArcQuickCaptureBar`、`ArcRoutineScheduleEditor`、`ArcDailyBrief`、`ArcDayAgenda`、`ArcReminderCenter`、`ArcMedicationSchedule`（后四个在 `LifeTodayKit.cs`）
  * Pages：对应 7 个 P0 页
  * 依赖：`Arc.Avalonia` + `Demo` + `Feedback`（`ArcProgress`）+ `Pickers`（`ArcTimePicker`、内部 `ArcDateMath` 通过 `InternalsVisibleTo` 共享）

原项目（Content、Feedback、AdvancedInputs、Pickers、SpecialAIWidgets、BlocksAI、BlocksApp）只删除对应文件，
主题入口无需改动（P0 组合控件没有 XAML ControlTheme）。

### P1b：按上游类别合并 Advanced 项目

| 原项目 | 合并到 | 迁移内容 |
| --- | --- | --- |
| `Arc.Avalonia.AdvancedInputs` | `Arc.Avalonia.Inputs` | 15 个控件、7 个页面、10 个主题文件；命名空间统一为 `Arc.Avalonia.Inputs.*`；主题 include 合并进 `ArcInputsTheme.axaml` |
| `Arc.Avalonia.AdvancedActions` | `Arc.Avalonia.Actions` | 5 个控件 + `ArcAdvancedVisuals`、2 个页面、5 个主题文件；命名空间统一为 `Arc.Avalonia.Actions.*`；主题 include 合并进 `ArcActionsTheme.axaml` |

共享接线同步更新：`ArcUI.slnx`、`Arc.Avalonia.Gallery.csproj`、`App.axaml`、BlocksApp/Commerce/Data 的项目引用，
以及所有 `using Arc.Avalonia.Advanced*` 引用。项目总数从 24 变为 25（含新增的 tests 验证工程）。

## 2. 重构后的项目结构

```
src/
├── Arc.Avalonia                     Foundation
├── Arc.Avalonia.Demo                page contract
├── Arc.Avalonia.AgentUI             P0 Agent UI（新）
├── Arc.Avalonia.LifeToday           P0 Today / 生活域（新）
├── Arc.Avalonia.Inputs              Text fields + special inputs + selects + toggles + sliders（合并 AdvancedInputs）
├── Arc.Avalonia.Actions             buttons + menus + navigation + disclosure + overlays + messages（合并 AdvancedActions）
├── Arc.Avalonia.Feedback
├── Arc.Avalonia.Charts
├── Arc.Avalonia.Pickers
├── Arc.Avalonia.Effects
├── Arc.Avalonia.Content
├── Arc.Avalonia.Media
├── Arc.Avalonia.Special*            5 个项目
├── Arc.Avalonia.Blocks*             6 个项目
└── Arc.Avalonia.Gallery
tests/Arc.Avalonia.Tests              headless 校验器
```

## 3. 本次未做（Phase 2 决策门）

按照 `08` 报告的结论，先完成低风险去重、分组和 AgentUI 收拢，再决定是否“按 catalog 类别彻底重切”。
以下两项存在真实的耦合成本，留到 Phase 2：

1. **拆分大杂烩项目**：`Actions` → `Actions` / `Navigation` / `Overlays`；`Feedback` → `Feedback` / `DataDisplay`。
   阻碍点：
   * `ArcActionVisuals`、`ArcMenuSurface` 被菜单/导航/浮层共用，需要先抽共享原语（原计划 `Arc.Avalonia.Internal`）；
   * `FeedbackUi`、`ArcIconButton`、`ArcTween` 被数据视图控件与反馈控件共用；
   * Actions/Feedback 的主题入口是聚合 Styles 文件（`ArcActions.Navigation/Overlays/Messages`、Feedback 21 个 include），
     拆分需要同步移动/重新挂接主题文件，属于中风险改动。
2. **共享原语项目**：`Arc.Avalonia.Internal`（`ArcUi` / `ArcDraw` / `ArcActionVisuals` / `ArcMenuSurface` /
   `FeedbackUi` / `ArcIconButton` / `ArcTween`），是第 1 项的前置条件。
3. **P2**：`src/` 物理分组、`Special.*` / `Blocks.*` 项目改名、`Charts`/`BlocksData` 命名空间统一。
   均为低成本但高 churn 的改动，等结构稳定后再做。

Phase 2 建议顺序：Internal 抽取 → Actions 拆分 → Feedback 拆分 → 命名空间统一 → 文档/截图回归。

## 4. 验证命令

```powershell
pwsh -NoProfile -File tools/build.ps1
dotnet run --project tests\Arc.Avalonia.Tests\Arc.Avalonia.Tests.csproj
dotnet run --project tests\Arc.Avalonia.Tests\Arc.Avalonia.Tests.csproj   # 输出 VERIFY: checks=34 failures=0
dotnet src\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --audit
dotnet src\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --smoke
```

> 已知：构建存在 10 条既有 warning（未新增 warning 类别）；`--smoke` 942 次点击、`--audit` 233 页 / 294 项全部通过。
