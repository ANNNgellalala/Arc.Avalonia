# 目录重构实施记录（Phase 1 + Phase 2）

> 依据：[`08-project-structure-review.zh.md`](08-project-structure-review.zh.md) 的 P0 + P1 建议。
> 执行日期：2026-10-29。回滚点：git `baseline` 标签（重构前）与分阶段提交。
> 最终验证：`dotnet build` 0 error；`VERIFY: checks=34 failures=0`；`CATALOG AUDIT: pages=233 missing=0`；
> `GALLERY SMOKE: clicks=942 failures=0`。

## 1. 结果概览

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| P0 | `.gitignore` / `bin-alt` 清理、slnx 分组、重复类型处理、无头验证工程 | ✅ |
| P1a | 新增 `Arc.Avalonia.AgentUI`、`Arc.Avalonia.LifeToday`，收拢 14 个 P0 控件与页面 | ✅ |
| P1b | `AdvancedInputs` → `Inputs`；`AdvancedActions` → `Actions` | ✅ |
| P1c | 新增 `Arc.Avalonia.Internal` 共享原语（MenuSurface / ArcGlide / IconButton / FeedbackUi / ArcTween / ArcActionModels） | ✅ |
| P1d | `Actions` → `Actions` + `Navigation` + `Overlays` | ✅ |
| P1e | `Feedback` → `Feedback` + `DataDisplay` | ✅ |
| P2 | `src/` 物理分组、`Special.*` / `Blocks.*` 改名、命名空间统一 | 暂缓（高 churn） |

重构后：`src/` 下 **28 个源项目**，加上 `tests/Arc.Avalonia.Tests` 共 **29 个解决方案项目**
（重构前 24 个源项目）。依赖保持单向：Foundation → Demo → Components / Special / Agent / Life → Blocks → Gallery。

## 2. 目录结构（重构后）

```
src/
├── Arc.Avalonia                     Foundation：token/主题/动效/图标/5 基础控件
├── Arc.Avalonia.Internal            共享内部原语：ArcMenuSurface/ArcGlide/ArcIconButton/
│                                    FeedbackUi/ArcTween/ArcActionModels（非公开组件层）
├── Arc.Avalonia.Demo                页面契约、DemoUi、目录、反射发现
├── Arc.Avalonia.AgentUI             P0 Agent UI（6 控件 / 6 页）
├── Arc.Avalonia.LifeToday           P0 Today / 生活域（7 控件 / 7 页）
├── Arc.Avalonia.Inputs              Text fields + special inputs + selects + toggles + sliders
├── Arc.Avalonia.Actions             Buttons + menus + disclosure + messages
├── Arc.Avalonia.Navigation          Tabs + breadcrumb + pagination + mega menu
├── Arc.Avalonia.Overlays            Dialog + drawer + popover + tooltip + share access
├── Arc.Avalonia.Feedback            Progress + skeleton + avatars + cards + activity
├── Arc.Avalonia.DataDisplay         Tables + tree + code/json + filter toolbar + data grid
├── Arc.Avalonia.Charts
├── Arc.Avalonia.Pickers
├── Arc.Avalonia.Effects
├── Arc.Avalonia.Content
├── Arc.Avalonia.Media
├── Arc.Avalonia.Special*            5 个项目
├── Arc.Avalonia.Blocks*             6 个项目
└── Arc.Avalonia.Gallery
tests/Arc.Avalonia.Tests              无头验证工程
```

## 3. 详细变更

### P0：工程卫生

* `.gitignore` 增加 `bin-alt/`、`TestResults/`；删除 `src/` 下 12 个 `bin-alt/`。
* `ArcUI.slnx` 从单一 `/src/` 改为 `/Foundation`、`/Components`、`/Special`、`/Blocks`、`/Agent`、`/App`、`/Tests`。
* `Feedback.Internal.ArcUi` → `FeedbackUi`（后续迁入 Internal）；Charts 渲染型
  `ArcAnimatedCounter`/`ArcActivityHeatmap` → `ArcChartAnimatedCounter`/`ArcChartActivityHeatmap`；
  合并重复的 `ArcSliderMark`。
* 新增 `tests/Arc.Avalonia.Tests`：控制台校验器，34 项断言（OKLCH 色彩、token、弹簧缓动、294 项目录）。
  原计划 xUnit，但本机沙箱无法启动 vstest host（`Process.GetProcessHandle` 被拒绝），因此改为无依赖控制台程序。

### P1：Shared primitives（Internal）

`Arc.Avalonia.Internal` 承载跨组件项目复用的内部原语：

| 文件 | 内容 | 消费者 |
| --- | --- | --- |
| `Controls/ArcActionModels.cs` | `ArcMenuItem` / `ArcAccordionItem` / `ArcGlide` 等模型 | Actions、Navigation、Overlays、Blocks |
| `Controls/ArcMenuSurface.cs` + theme | 菜单表面 ControlTheme | Actions、Overlays |
| `Controls/ArcIconButton.cs` + theme | 行内图标按钮 / flat 按钮 | Feedback、DataDisplay、Blocks |
| `Composition/ArcActionVisuals.cs` | `ArcActionVisuals`、`ArcGlide`、`ArcMenuPresenter`、`ArcDismiss`、`ArcKeyboard` | Actions、Navigation、Overlays |
| `Composition/FeedbackUi.cs` | `FeedbackUi` 组合助手与 `ArcTween` | Feedback、DataDisplay |

内部类型通过 `InternalsVisibleTo` 暴露给 Actions / Navigation / Overlays / Feedback / DataDisplay；
`ArcMenuSurface`、`ArcGlide`、`ArcIconButton`、`ArcFlatButton` 保持 public。

### P1：AgentUI / LifeToday

* `Arc.Avalonia.AgentUI`：`ArcAgentTranscript`、`ArcRunControlBar`、`ArcToolApprovalSheet`、
  `ArcAgentAskCard`、`ArcBackgroundRunDock`、`ArcAgentAttentionInbox` + 6 个 P0 页。
* `Arc.Avalonia.LifeToday`：`ArcReminderRow`、`ArcQuickCaptureBar`、`ArcRoutineScheduleEditor`、
  `ArcDailyBrief`、`ArcDayAgenda`、`ArcReminderCenter`、`ArcMedicationSchedule` + 7 个 P0 页。
* 原项目（Content、Feedback、AdvancedInputs、Pickers、SpecialAIWidgets、BlocksAI、BlocksApp）只删除文件；
  LifeToday 依赖 Feedback（`ArcProgress`）与 Pickers（`ArcTimePicker`、`ArcDateMath` 经 `InternalsVisibleTo`）。

### P1：Advanced 合并

| 原项目 | 合并到 | 内容 |
| --- | --- | --- |
| `Arc.Avalonia.AdvancedInputs` | `Arc.Avalonia.Inputs` | 15 控件、7 页面、10 主题；命名空间 `Arc.Avalonia.Inputs.*` |
| `Arc.Avalonia.AdvancedActions` | `Arc.Avalonia.Actions` | 5 控件 + `ArcAdvancedVisuals`、2 页面、5 主题 |

### P1：Actions 拆分

| 项目 | 控件 | 页面 | 主题 |
| --- | --- | --- | --- |
| `Actions` | Accordion、ActionSwap、Alert、ButtonGroup、ConfirmMorph、ContextMenu、CopyButton、DropdownMenu、ExpandableText、SplitButton、ThemeSwitcher、Toast/ToastHost + AdvancedActions 5 控件 | Actions、ActionsPartTwo、Disclosure + AdvancedActions 2 页 | Buttons / Menus / Messages / Disclosure / AdvancedActions 主题 |
| `Navigation` | Tabs、Breadcrumb、Pagination、MegaMenu | Navigation、MegaMenu | Navigation + MegaMenu |
| `Overlays` | Dialog、Drawer、Popover、Tooltip、ShareAccess | Overlays、OverlaysPartTwo、ShareAccess | Overlays + ShareAccess |

`ArcActionsTheme.axaml` 中按注释分节的内联样式被精确切分：Tabs/Breadcrumb/Pagination/MegaMenu → Navigation，
Popover/Tooltip/ShareAccess → Overlays，其余保留在 Actions；`ArcAccordion`/`ArcExpandableText` 的 ControlTheme
保留在 Actions 的 `ArcActions.Disclosure.axaml`。Navigation/Overlays 仅为页面 demo 引用 Actions
（`ArcAccordion`、`ArcToastHost`、`ArcAlert`、`ActionDemos`），控件层只依赖 Foundation/Internal。

### P1：Feedback 拆分

| 项目 | 控件 | 页面 | 主题 |
| --- | --- | --- | --- |
| `Feedback` | Progress、Skeleton、Stepper、Countdown、UsageMeter、Avatar、AvatarGroup、PresenceStack、MetricCard、EmptyState、Timeline、CommentThread、AnimatedCounter、ActivityHeatmap | Feedback、Steps、Time、Activity、Identity、Presence、ProDemos | 14 个 |
| `DataDisplay` | SortableDataTable、TreeTable、TreeView、FilterToolbar、CodeBlock、JsonViewer、DataGrid | Data、DataViews、Explorer、TreeTable、DataGrid、DataDemos/DataGridDemos | 7 个 |

`ProDemos` 中的 DataGrid/EmptyGrid demo 拆到 `DataDisplay/Pages/DataGridDemos.cs`，Presence demo 留在 Feedback；
共享版式 `PageLayout` 改为 public 供 DataDisplay 复用；DataDisplay 依赖 Feedback（`ArcEmptyState`、
`ArcActivityHeatmap`、`PageLayout`），因此 Feedback 不反向依赖 DataDisplay。

## 4. 验证

```text
dotnet build ArcUI.slnx                                              → 0 error
dotnet tests\Arc.Avalonia.Tests\bin\Debug\net10.0\Arc.Avalonia.Verify.dll
                                                                     → VERIFY: checks=34 failures=0
dotnet src\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --audit
                                                                     → CATALOG AUDIT: pages=233 items=294 missing=0 pageFailures=0
dotnet src\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --smoke
                                                                     → GALLERY SMOKE: clicks=942 failures=0
```

构建 warning 均为重构前既有的 analyzer 提示（未新增错误类别）。已对 `navigation` 与 `data-grid` 页面做明暗截图抽查，
确认拆分后的 Navigation/DataDisplay 主题仍正确装配；全量 466 张截图未重跑。

## 5. 回滚点

```
60c3377 baseline: Arc.Avalonia before P0/P1 restructure
ecb6f8b P0: group solution folders; rename duplicate chart types and Feedback Ui helper; ignore bin-alt
b6dcd31 P0: add headless verification project (colour, motion, catalog)
744773e P1: extract AgentUI and LifeToday projects from component/blocks projects
0924fa1 P1: add Arc.Avalonia.Internal shared primitives
34d9c90 P1: split Actions into Actions, Navigation and Overlays
f2fc693 P1: split Feedback into Feedback and DataDisplay
```

## 6. 暂缓项（P2）

* `src/` 物理分组（`src/Foundation`、`src/Components`、`src/Special`、`src/Blocks`、`src/Apps`）；
* 项目改名 `Special.*` / `Blocks.*`（需同步 `avares://` URI、命名空间、slnx、文档）；
* `Charts` / `BlocksData` 的命名空间统一（`Arc.Avalonia.Charts` 与 `Arc.Avalonia.Blocks.Data` 目前为根命名空间）。

这些改动价值主要是导航与一致性，但 churn 高、收益低，留到结构稳定后的独立提交。
