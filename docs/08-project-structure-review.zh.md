# Arc for Avalonia 目录结构与项目划分分析

> **执行状态（2026-10-29）：** 本报告的 P0 与 P1 已全部实施（含 `Arc.Avalonia.Internal`、AgentUI/LifeToday、
> Advanced 合并、Actions→Navigation/Overlays、Feedback→DataDisplay），见
> [`09-project-restructure-phase1.zh.md`](09-project-restructure-phase1.zh.md)。仅 P2（物理目录分组 / 项目改名 /
> 命名空间统一）按计划暂缓。
>
> 分析基线：2026-10-28，工作区 `D:\Code\VibeCoding\Arc.Avalonia`。
> 范围：根目录 5 个文件夹、`src/` 下全部 24 个 .NET 项目（约 172k 行 C#/XAML、235 个页面类、233 个 Gallery 页面）、
> `docs/`、`tools/`、`shots/`、`.recon/`。
> 方法：读取 `README.md`、`CONVENTIONS.md`、`docs/03-implementation.zh.md`、`docs/07-p0-ui-implementation.zh.md`；
> 解析全部 `.csproj` 的 `ProjectReference`；对每个项目的 `Controls/`、`Pages/`、`Themes/` 做文件级清点；
> 用 `docs/arc-catalog.json`（23 类 / 128 组件 + 5 组 / 55 Special + 6 组 / 111 Blocks）反查组件归属。

---

## 1. 顶层目录

```
Arc.Avalonia/
├── src/                    24 个项目：1 基础层 + 1 页面契约 + 10 组件 + 5 Special + 6 Blocks + 1 Gallery
├── docs/                   7 篇中文分析报告 + 2 份 catalog JSON + images/perf/reference/agent-life-tools 素材
├── tools/                  3 个 PowerShell 脚本：build.ps1 / shots.ps1 / build-catalog.ps1
├── shots/                  light / dark 各 233 张离屏截图（.gitignore 忽略 PNG）
├── .recon/                 临时侦察目录（日志、探针工程、Python 校验脚本；已在 .gitignore 中）
├── ArcUI.slnx              24 个项目的解决方案
├── Directory.Build.props   统一 net10.0 / Avalonia 12.1.4 / Nullable / LangVersion
├── Directory.Build.targets 关闭 Avalonia 遥测写入
├── NuGet.config            离线源配置
├── CONVENTIONS.md          构建约定、所有权、token/组件规范
└── README.md               项目说明、组件总表、运行与截图方式
```

顶层划分是清楚的：`src`（源码）、`docs`（设计与审查文档）、`tools`（构建/截图脚本）、`shots`（渲染产物）、
`.recon`（一次性侦察草稿），根目录只放解决方案级配置与约定文件。

---

## 2. 分层与依赖关系

从 24 个 `.csproj` 实际解析出的 `ProjectReference` 形成一个严格单向的四层结构：

```
┌──────────────────────────────────────────────────────────────────────┐
│ Arc.Avalonia.Gallery（WinExe：外壳、截图、--audit、--smoke）          │
│   引用其余全部 23 个项目                                              │
└───────────────▲──────────────────────────────────────────────────────┘
                │
┌───────────────┴───────────┐   ┌─────────────────────────────────────┐
│ 6 个 Blocks 项目           │──▶│ 4–10 个组件项目（按区块需要引用）     │
│ Marketing/Commerce/App/    │   └─────────────────────────────────────┘
│ Team/Data/AI               │
└───────────────┬───────────┘
                │
┌───────────────▼───────────┐   ┌─────────────────────────────────────┐
│ 10 个组件项目 + 5 个       │──▶│ Arc.Avalonia.Demo                   │
│ Special 项目               │   │ ArcPage / ArcItemPage / DemoUi /     │
│ （同一层，互不引用）        │   │ ArcCatalog / ArcDocLayout / 反射发现 │
└───────────────┬───────────┘   └───────────────┬─────────────────────┘
                │                               │
                └───────────────┬───────────────┘
                                ▼
                ┌─────────────────────────────────────┐
                │ Arc.Avalonia（基础层）               │
                │ Theme / Motion / Icons / 5 个基础控件│
                └─────────────────────────────────────┘
```

实测要点：

* 无循环依赖，方向单一：基础层 → Demo → 组件/Special → Blocks → Gallery。
* 10 个组件项目与 5 个 Special 项目之间**没有任何互相引用**（每个只引用 `Arc.Avalonia` + `Arc.Avalonia.Demo`），
  并行开发时不会互相编译污染，这是当前划分最健康的部分。
* Blocks 项目按需引用 4–10 个组件项目，Gallery 引用全部 23 个；因此任何新增项目都必须同时改
  `ArcUI.slnx` 与 `src/Arc.Avalonia.Gallery/Arc.Avalonia.Gallery.csproj`（以及 `App.axaml` 的 `StyleInclude`）。
* 每个组件/Special/Blocks 项目都自带 `Controls/`、`Pages/`、`Themes/` 三件套，页面由
  `Arc.Avalonia.Demo.ArcPageRegistry` 反射发现，不需要中央注册表。这套“项目即插件”的模式是
  `CONVENTIONS.md` 里“一个 agent 一个项目”的所有权模型的直接产物。

---

## 3. 各项目组件清单

### 3.1 基础与展示层（3 个项目）

#### `src/Arc.Avalonia` — 基础层（13 个 .cs、4 个 .axaml，约 2.0k 行）

| 子目录 | 内容 |
| --- | --- |
| `Theme/` | `ArcColor`（OKLCH/OKLab ↔ sRGB、`color-mix(in oklab)`）、`ArcPalette`（明暗 + 8 accent 的完整 token 表）、`ArcThemeManager`（`Apply` / `ReducedMotion`）、`ArcTokens`（token 键常量）、`ArcTheme.axaml`（基础样式与排版类） |
| `Motion/` | `ArcMotion`：`SpringSmooth` / `SpringSnappy` / `SpringMorph`、120/160/240/480ms tween、`ArcSpringEasing` 阻尼振子采样 |
| `Icons/` | `ArcIconData`：161 个 Lucide 图标的路径数据（stroke 1.75，24 单位网格） |
| `Controls/` | `ArcButton`、`ArcCard`、`ArcBadge`、`ArcIcon`、`ArcSpinner`；以及 P0 阶段新增的 `ArcUi`（组合控件辅助：Brush/Text/Stack/Icon/Badge/Pill…）与 `ArcVirtualList`（`ListBox` 行级虚拟化，`StickToBottom` / `ScrollState`） |
| `Themes/` | `ArcButton.axaml`、`ArcCard.axaml`、`ArcBadge.axaml`（ControlTheme） |

注意基础层有两个近义目录：`Theme/`（token、调色板、主题管理器）与 `Themes/`（控件 ControlTheme）。
其他 23 个项目只有一个 `Themes/`，命名上容易混淆。

#### `src/Arc.Avalonia.Demo` — 页面契约（7 个 .cs + 1 个 JSON，约 0.6k 行）

| 文件 | 作用 |
| --- | --- |
| `ArcPage.cs` | Gallery 页面抽象：`Title` / `Group` / `Description` / `Order` / `Build(context)` |
| `ArcItemPage.cs` | 对应 catalog 单项的页面基类，从 catalog 取标题、分类、tier、order |
| `ArcDocLayout.cs` | 统一的文档版式（面包屑、标题、When to use、Preview、Keyboard、Motion、Implementation notes） |
| `DemoUi.cs` | `Section` / `Row` / `Column` / `Field` / `Card` / `Canvas` / `Brush` 等布局助手 |
| `ArcCatalog.cs` | 目录模型与内嵌 `Catalog/arc-catalog.json` 的加载 |
| `ArcCatalogAliases.cs` | 共享页与 catalog 条目的别名映射，保证 294 项全部可达 |
| `ArcPageRegistry.cs` | 反射发现已加载 Arc 程序集中的所有 `ArcPage` |

**这是全仓库最重要的结构性事实：所有组件/Special/Blocks 项目都引用 Demo，并把页面编译进自己的库程序集。**
`Arc.Avalonia.Inputs.csproj:9-10` 即典型：引用基础层 + Demo。Demo 的定位因此同时是“页面契约”
和“每个库的展示层依赖”。

#### `src/Arc.Avalonia.Gallery` — 展示与验收应用（13 个 .cs/.axaml，约 2.0k 行）

| 部分 | 内容 |
| --- | --- |
| 入口 | `Program.cs`、`App.axaml(.cs)`（22 个 `StyleInclude` 按项目装配主题，`App.axaml:8-28`） |
| `Framework/` | `GalleryWindow`（侧边栏 + 页面宿主）、`ShotRunner`（真实离屏窗口 + Skia 渲染）、`CatalogAudit`（`--audit` 294/294 检查）、`NavigationSmoke`（`--smoke` 点击全部页面） |
| `Pages/` | `FoundationsPage`、`TokensPage`、`IconsPage`、`MotionPage`、`ProofPage`、`VirtualListDemoPage`（基础层与 P0 的演示） |

### 3.2 组件层（10 个项目，对应 catalog 的 128 个常规组件）

> 下表“控件”列出 `Controls/` 中的主要公开类型；`Internal/` 或 `*Visuals/*Models/*Kit` 为内部辅助，不计入组件。

#### `Arc.Avalonia.Inputs` — 18 个控件，4 个页面

`ArcInput`、`ArcTextarea`、`ArcPasswordField`、`ArcPasswordStrength`、`ArcSearchField`、`ArcNumberField`、
`ArcMoneyInput`、`ArcTagInput`、`ArcSelect`、`ArcCombobox`、`ArcMultiSelect`、`ArcChipGroup`、`ArcCheckbox`、
`ArcRadioGroup`、`ArcSwitch`、`ArcSegmentedControl`、`ArcSlider`、`ArcRating`；
辅助：`ArcFieldBase`、`ArcTextFieldBase`、`ArcStar`、`CapsLockState`、`ArcMenuHelper`。
页面：`Inputs`、`InputsPartTwo`、`InputsPartThree`、`InputsPartFour`（`InputsSections` 为共享分节辅助）。

#### `Arc.Avalonia.Actions` — 约 22 个目录项，8 个页面

按钮：`ArcSplitButton`、`ArcButtonGroup`、`ArcCopyButton`、`ArcActionSwap`、`ArcConfirmMorph`、`ArcThemeSwitcher`；
菜单：`ArcDropdownMenu`、`ArcContextMenu`；导航：`ArcTabs`、`ArcBreadcrumb`、`ArcPagination`、`ArcMegaMenu`(Pro)；
折叠：`ArcAccordion`、`ArcExpandableText`；浮层：`ArcDialog`、`ArcDrawer`、`ArcPopover`、`ArcTooltip`、`ArcShareAccess`(Pro)；
消息：`ArcAlert`、`ArcToast`、`ArcToastHost`。辅助：`ArcActionModels`、`ArcActionVisuals`、`ArcMenuSurface`。
页面：`Actions`、`ActionsPartTwo`、`Disclosure`、`Navigation`、`Overlays`、`OverlaysPartTwo`、`MegaMenu`、`ShareAccess`。

#### `Arc.Avalonia.AdvancedActions` — 5 个控件，2 个页面

`ArcActionButton`、`ArcFloatingButtonGroup`、`ArcExpandingButtonGroup`、`ArcIconMorph`、`ArcUserMenu`；
辅助 `ArcAdvancedVisuals`。页面：`AdvancedActions`、`AdvancedActionsPartTwo`。

#### `Arc.Avalonia.AdvancedInputs` — 13 个控件，6 个页面

`ArcInlineEdit`、`ArcScrubInput`、`ArcPhoneInput`、`ArcMentionInput`、`ArcShortcutRecorder`、`ArcExpandingSearch`、
`ArcMorphSelect`、`ArcElasticSlider`、`ArcRangeHistogramSlider`、`ArcBillingToggle`、`ArcBillingPrice`、`ArcRadioCards`；
P0 新增 `ArcQuickCaptureBar`；辅助 `ArcDraw`、`ArcAdvancedInternals`。
页面：`AdvancedInputs`、`…PartTwo` 至 `…PartFive`、`QuickCaptureBar`（`AdvancedInputsSections` 为分节辅助）。

#### `Arc.Avalonia.Feedback` — 23 个控件，12 个页面

进度/状态：`ArcProgress`、`ArcSkeleton`、`ArcStepper`、`ArcCountdown`、`ArcUsageMeter`；
头像/身份：`ArcAvatar`、`ArcAvatarGroup`、`ArcPresenceStack`(Pro)；
卡片/空态：`ArcMetricCard`、`ArcEmptyState`；
数据视图：`ArcSortableDataTable`、`ArcTreeTable`、`ArcTreeView`、`ArcFilterToolbar`、`ArcCodeBlock`、`ArcJsonViewer`、`ArcDataGrid`(Pro)；
活动：`ArcTimeline`、`ArcCommentThread`；图表类：`ArcAnimatedCounter`、`ArcActivityHeatmap`（与 Charts 重复）；
行内动作：`ArcIconButton`；P0 新增 `ArcReminderRow`。辅助：`Internal/ArcUi`。
页面：`Feedback`、`Steps`、`Time`、`Activity`、`Identity`、`Data`、`DataViews`、`Explorer`、`TreeTable`、`Presence`、`DataGrid`、`ReminderRow`（`FeedbackDemos`、`DataDemos`、`ProDemos`、`PageLayout` 为演示/版式辅助）。

#### `Arc.Avalonia.Charts` — 20 个控件，7 个页面

常规图：`ArcLineChart`、`ArcBarChart`、`ArcDonutChart`、`ArcSparkline`、`ArcGauge`、`ArcWaffleChart`、
`ArcSlopeChart`、`ArcTreemap`、`ArcRidgeline`、`ArcStreamgraph`、`ArcActivityHeatmap`、`ArcAnimatedCounter`、`ArcSplitBar`；
Pro 图：`ArcBrushChart`、`ArcSunburstChart`、`ArcSankeyFlow`、`ArcFunnelChart`、`ArcRadarChart`、`ArcRealtimeStream`、`ArcRaceBarChart`；
容器：`ArcChartCard`；辅助 `ArcAdvancedChartModels`。
页面：`Charts`、`ChartsMore`、`ChartsBrush`、`AdvancedCharts`、`AdvancedChartsPartTwo`（`ChartsData`、`AdvancedChartsData` 为示例数据辅助）。

#### `Arc.Avalonia.Pickers` — 10 个控件，7 个页面

`ArcCalendar`、`ArcDatePicker`、`ArcDateRangePicker`、`ArcTimePicker`、`ArcColorPicker`、`ArcFileDropzone`、
`ArcSignaturePad`、`ArcEmojiPicker`(Pro)、`ArcAvatarUpload`(Pro)；P0 新增 `ArcRoutineScheduleEditor`。
辅助：`ArcCalendarSurface`、`ArcColorSurfaces`、`ArcColorEditor`、`ArcColorMath`、`ArcDateMath`、`ArcPickerBase`、
`ArcAvatarCropSurface`、`ArcAvatarRing`、`ArcEmojiData`、`ArcSamplePhoto`。
页面：`Pickers`、`Color`、`FilesAndInk`、`RangesAndTime`、`EmojiPicker`、`AvatarUpload`、`RoutineScheduleEditor`（`PickerDemo` 为共享演示辅助）。

#### `Arc.Avalonia.Effects` — 9 个控件，2 个页面

`ArcTextReveal`、`ArcInViewTitle`、`ArcTextMorph`、`ArcTextShimmer`、`ArcAnnouncementBar`、`ArcCookieConsent`、
`ArcHoverCard`、`ArcBottomSheet`、`ArcExpandableCard`；辅助 `ArcEffectModels`、`ArcEffectVisuals`。
页面：`Effects`、`EffectsMotion`（`EffectDemos` 为演示辅助）。

#### `Arc.Avalonia.Content` — 5 个控件，3 个页面

`ArcRichTextEditor`、`ArcReorderableList`、`ArcChatThread`、`ArcOnboardingChecklist`；
P0 新增 `ArcAgentTranscript`；辅助 `ArcMarkdown`、`ArcContentUi`。
页面：`Content`、`ContentFlows`、`AgentTranscript`。

#### `Arc.Avalonia.Media` — 8 个控件，3 个页面

`ArcHoldToConfirm`、`ArcSwipeActions`、`ArcReactions`、`ArcImageCompare`、`ArcCarousel`、`ArcCardStack`、
`ArcScrollArea`、`ArcResizablePanels`；辅助 `ArcMediaModels`、`ArcMediaUi`。
页面：`Media`、`MediaLayout`、`Gestures`（`MediaDemos` 为演示辅助）。

### 3.3 Special components（5 个项目，55 项，每项一页）

| 项目 | 11 个目录项 | 额外控件/辅助 |
| --- | --- | --- |
| `Arc.Avalonia.Special.Glass` | Glass menu / segmented / card / tab bar、Holo card、Wallet stack、Sheet stack、Control center、Card to article、Link unfurl、Share sheet | `ArcGlassBackdrop`、`ArcGlass`、`ArcDurationDial`、`ArcRubberMeter`、`GlassMotion`、`GlassDemo` |
| `Arc.Avalonia.Special.Motion` | Action morph、Morph nav、Dock、Liquid tab bar、Orbit menu、Orbit logos、Booking pill、Keycap keyboard、Slot text、Sand text、Typewriter terminal | `SpecialMotionUi` |
| `Arc.Avalonia.Special.Media` | Product gallery、Lightbox gallery、Photo grid、Cover flow、Voice orb、Now playing、Voice recorder、Page curl、Sticker peel、Dot grid、Gravity well | `SampleArt`、`SpecialIcons`、`SpecialUi` |
| `Arc.Avalonia.Special.AIWidgets` | Agent status、Tool call、Agent plan、Agent timeline、Context window、Ink fluid、Activity rings、Stretch refresh、Morph loader、Skeleton morph、Black hole | P0：`ArcAgentAskCard`、`ArcRunControlBar`、`ArcToolApprovalSheet`、`ArcBackgroundRunDock`；`SpecialVisuals` |
| `Arc.Avalonia.Special.AIExplainers` | Data flow、Payment flow、Multi-region failover、Vector search、Agent loop、Mixture of experts、Speculative decoding、Prompt cache、Reasoning tree、Time dial、Date reel | `ExplainInk` |

Special 五个项目与 `arc-catalog.json` 的 `specialGroups` **一一对应**（每组 11 项），
每项都继承 `ArcItemPage`，是当前划分中最整齐的一组。

### 3.4 Blocks（6 个项目，111 项）

| 项目 | 目录项 | 页面数 | 项目内控件/辅助 |
| --- | --- | --- | --- |
| `Arc.Avalonia.Blocks.Marketing` | 18（Marketing and site） | 18 | `ArcMarquee`、`ArcMeshWash`、`ArcSpotlightCard`、`ArcStatNumber`、`MarketingKit` |
| `Arc.Avalonia.Blocks.Commerce` | 18（Commerce） | 18 | `ArcEmptyScene`、`CommerceKit` |
| `Arc.Avalonia.Blocks.App` | 21（App shell and settings） | 25 | `OtpField`、`AppKit`、`LifeTodayKit`（含 `ArcDailyBrief`、`ArcDayAgenda`、`ArcReminderCenter`、`ArcMedicationSchedule` 四个 P0 控件） |
| `Arc.Avalonia.Blocks.Team` | 16（Team and collaboration） | 16 | `ArcCohortChart`、`ArcMorphIllustration`、`TeamData`、`TeamKit` |
| `Arc.Avalonia.Blocks.Data` | 20（Data and charts） | 20 | `ArcCandleChart`、`ArcChoroplethMap`、`ArcConnectedScatter`、`ArcDotGlobe`、`ArcExplodedLayers`、`ArcForecastCone`、`ArcGalaxyStage`、`ArcHorizonChart`、`ArcLayoutMorph`、`ArcOverviewBrush`、`ArcReplayLine`、`ArcSemanticMap`、`ArcSmallMultiples`、`ArcTerrainStage`、`ArcWaterfallBars`、`DataDraw`、`DataKit`、`DataSamples` |
| `Arc.Avalonia.Blocks.AI` | 18（AI） | 19 | `AiKit`、`ArcAgentAttentionInbox`（P0） |

Blocks 六个项目与 `blockGroups` 的六组一一对应。`BlocksApp` 25 页 = 21 个 catalog 区块 + 4 个 P0 Today 页；
`BlocksAI` 19 页 = 18 + 1 个 P0 Agent 页。

### 3.5 文档、工具与产物

* `docs/`：`01`–`07` 七篇分析/实现/性能/扩展文档 + `arc-catalog.json`（294 项目录）+ `arc-extension-catalog.json`
  （57 项扩展提案）+ `images/`（62 张 README 截图）+ `perf/`、`reference/`、`agent-life-tools/` 素材。
* `tools/`：`build.ps1`（-m:1 串行构建 + 文件锁）、`shots.ps1`（全页明暗截图）、`build-catalog.ps1`（从公开
  llms 数据生成并校验 294 项目录）。
* `shots/`：light/dark 各 233 张，共 466 张（PNG 被 `.gitignore` 忽略）。
* `.recon/`：被忽略的临时目录，含 `Probe`、`NavPerf` 两个不在解决方案中的探针工程与大量日志/校验脚本。

---

## 4. 与上游 Arc 目录（23 类 / 128 项）的映射

这是判断“划分是否合理”的关键。把 `arc-catalog.json` 的 23 个类别逐一对应到实际项目，结果如下
（✅ 完整落在一个项目；⚠️ 被拆到多个项目）：

| 上游类别（项数） | 落在哪些项目 | 评价 |
| --- | --- | --- |
| Buttons (11) | `Arc.Avalonia`(button)、`Actions`(split/group/copy/action-swap/confirm-morph/theme-switch)、`AdvancedActions`(action-button/floating/expanding/icon-morph) | ⚠️ 3 个项目 |
| Menus (3) | `Actions`(dropdown/context)、`AdvancedActions`(user-menu) | ⚠️ 2 个项目 |
| Text fields (7) | `Inputs`(input/textarea/password/password-strength/search)、`AdvancedInputs`(expanding-search/inline-edit) | ⚠️ 2 个项目 |
| Special inputs (7) | `Inputs`(number/money/tag)、`AdvancedInputs`(phone/scrub/mention/shortcut) | ⚠️ 2 个项目 |
| Selects (5) | `Inputs`(select/combobox/multi-select/chip-group)、`AdvancedInputs`(morph-select) | ⚠️ 2 个项目 |
| Toggles (7) | `Inputs`(checkbox/radio/switch/segmented/rating)、`AdvancedInputs`(radio-cards/billing-toggle) | ⚠️ 2 个项目 |
| Sliders (3) | `Inputs`(slider)、`AdvancedInputs`(elastic/range-histogram) | ⚠️ 2 个项目 |
| Pickers (6) | `Pickers` | ✅ |
| Editors (4) | `Content`(rich-text-editor)、`Pickers`(signature/file-dropzone/avatar-upload) | ⚠️ 2 个项目 |
| Navigation (4) | `Actions` | ✅ |
| Expand (5) | `Actions`(accordion/expandable-text)、`Effects`(expandable-card)、`Media`(scroll-area/resizable-panels) | ⚠️ 3 个项目 |
| Overlays (7) | `Actions`(dialog/drawer/popover/tooltip/share-access)、`Effects`(bottom-sheet/hover-card) | ⚠️ 2 个项目 |
| Messages (5) | `Actions`(alert/toast/toast-stack)、`Effects`(announcement-bar/cookie-consent) | ⚠️ 2 个项目 |
| Progress (6) | `Feedback`(progress/skeleton/stepper/countdown/usage-meter)、`Content`(onboarding-checklist) | ⚠️ 2 个项目 |
| Avatars (4) | `Feedback`(avatar/avatar-group/presence-stack)、`Arc.Avalonia`(badge) | ⚠️ 2 个项目 |
| Cards (3) | `Arc.Avalonia`(card)、`Feedback`(metric-card/empty-state) | ⚠️ 2 个项目 |
| Charts (14) | `Charts` | ✅ |
| Advanced charts (7) | `Charts`(6 个 Pro 图)、`Feedback`(data-grid) | ⚠️ 2 个项目 |
| Tables (7) | `Feedback`(data-table/tree-table/tree-view/filter/code/json)、`Content`(reorderable-list) | ⚠️ 2 个项目 |
| Activity (3) | `Feedback`(timeline/comment-thread)、`Content`(chat-thread) | ⚠️ 2 个项目 |
| Media (3) | `Media` | ✅ |
| Text effects (4) | `Effects` | ✅ |
| Gestures (3) | `Media` | ✅ |

**23 类中只有 6 类完整落在一个项目里，17 类被拆分到 2–3 个项目。**
拆分的主要原因不是领域，而是实现难度/开发轮次：`AdvancedInputs` / `AdvancedActions` 是“进阶版输入/动作”，
`Effects` 收纳了“动画 + 浮层 + 消息”的混合体，`Content` 收纳了编辑器/列表/聊天/清单。

相比之下，Special（5 组 × 11）与 Blocks（6 组）是**严格按上游导航组 1:1 划分**的，
这也是它们阅读起来最直观的原因。

---

## 5. 合理性评估

### 5.1 结论先行

**方向合理、粒度不统一。**
作为“多智能体并行开发的 Arc 复刻/展示仓库”，这套划分是合理且高效的工程妥协；
作为“可被长期复用、按领域查找的组件库”，它不合理，需要重组。

一句话概括：**分层（layer）切得对，家族（Special/Blocks）切得对，只有 128 个常规组件这一层的切分轴混用了
“上游类别 / 实现难度 / 开发轮次”三种标准。**

### 5.2 合理之处

1. **依赖方向单一、无环。** 基础层 → Demo → 组件/Special → Blocks → Gallery，24 个项目实测无环，
   任何叶子项目都不能反向依赖 Gallery。
2. **Special / Blocks 划分与上游目录 1:1。** 5 个 Special 项目对应 `specialGroups` 的 5 组 × 11，
   6 个 Blocks 项目对应 `blockGroups` 的 18/18/21/16/20/18，页面数可直接核对，迁移和审计都简单。
3. **组件项目之间零耦合。** 10 个组件项目互不引用，可以独立编译、独立截图、独立分配 owner，
   这是 `CONVENTIONS.md` “一个 agent 一个项目”能成立的前提。
4. **物理目录与命名空间基本一致。** `src/Arc.Avalonia.Inputs` → `Arc.Avalonia.Inputs.Controls/.Pages`，
   按项目名就能定位类型。
5. **页面与控件同项目、反射发现。** 不需要中央注册表，新增项目只改 slnx/Gallery 引用/`App.axaml` 三处，
   适合大量并行提交。
6. **验收链路完整。** `tools/build.ps1`（串行构建）、`tools/shots.ps1`（明暗截图）、
   `Gallery --audit`（294/294 可达）、`--smoke`（942 次点击）构成可执行的验收闭环。
7. **根目录职责清晰。** `src/docs/tools/shots` 分离，`.recon` 隔离草稿，`Directory.Build.*` 集中公共配置。

### 5.3 不合理 / 风险之处

#### ① 组件层的切分轴不统一（核心问题）

同一上游类别被拆到 2–3 个项目，用户找“所有输入类”要同时看 `Inputs` + `AdvancedInputs`；
找“所有浮层”要看 `Actions` + `Effects`；找“所有表格/列表”要看 `Feedback` + `Content`。
`Advanced*` 是难度标签而不是领域标签，`Effects` / `Content` / `Feedback` 的名字也不足以预告内容
（例如 hover-card、cookie-consent、onboarding-checklist 都在名字里看不出来）。

#### ② 两个“大杂烩”项目体量最大

| 项目 | 文件数 | 行数 | 内容 |
| --- | --- | --- | --- |
| `Feedback` | 62 | ~15.1k | 进度 + 头像 + 卡片 + 表格 + 活动 + 图表 + P0 提醒行 |
| `Actions` | 42 | ~12.0k | 按钮 + 菜单 + 导航 + 折叠 + 浮层 + 消息 |

两者同时是“最不内聚”和“最大”的项目，天然是合并冲突与 review 压力最高的地方；
而 `AdvancedActions` 只有 14 个文件 / ~4.1k 行，负载明显不均。

#### ③ 缺少共享的“内部原语”项目，导致工具类复制

每个组件项目都自带一份 `*Visuals` / `*Ui` / `*Kit` / `*Internals` 辅助文件：
`ArcActionVisuals`、`ArcAdvancedVisuals`、`ArcAdvancedInternals`、`ArcEffectVisuals`、`ArcMediaUi`、
`ArcContentUi`、`ArcMenuHelper`、`SpecialVisuals`、`SpecialUi`、`SpecialMotionUi`、`GlassDemo` 等。
后果已经出现——**三个同名类型**：

* `ArcAnimatedCounter`：`Charts/Controls/ArcAnimatedCounter.cs`（204 行）与
  `Feedback/Controls/ArcAnimatedCounter.cs`（186 行）两份实现；
* `ArcActivityHeatmap`：`Charts/Controls/ArcActivityHeatmap.cs`（279 行）与
  `Feedback/Controls/ArcActivityHeatmap.cs`（607 行）两份实现，行数差一倍，已经漂移；
* `ArcUi`：基础层 `public static class ArcUi` 与 Feedback `internal static class ArcUi` 同名不同物。

#### ④ Demo 反向依赖：展示层被编进库

所有 10 个组件项目、5 个 Special 项目都引用 `Arc.Avalonia.Demo` 并把自己的 `Pages/` 编进同一个程序集。
如果目标是“可引用的组件库”，这意味着消费 `Arc.Avalonia.Inputs` 会连带拉入页面契约、catalog JSON 和内嵌演示；
如果目标只是“复刻/展示仓库”，这是有意的设计（`ArcPage.cs` 注释明确说明“each library ships with its own
showcase”）。当前 README/CONVENTIONS 的定位更接近后者，所以问题不是“错”，而是**定位需要写清楚**，
否则后续想抽库时会发现展示层已经和组件层焊死。

#### ⑤ 新的产品能力（P0 Agent/Life 工具）没有归属，被撒进 8 个项目

`docs/07` 的 14 个 P0 控件按“就近匹配”散落：

| P0 控件 | 被放进 |
| --- | --- |
| `ArcAgentTranscript` | `Content` |
| `ArcRunControlBar` / `ArcToolApprovalSheet` / `ArcAgentAskCard` / `ArcBackgroundRunDock` | `SpecialAIWidgets` |
| `ArcAgentAttentionInbox` | `BlocksAI` |
| `ArcReminderRow` | `Feedback` |
| `ArcDailyBrief` / `ArcDayAgenda` / `ArcReminderCenter` / `ArcMedicationSchedule` | `BlocksApp/LifeTodayKit` |
| `ArcVirtualList` / `ArcUi` | `Arc.Avalonia`（基础层） |
| `ArcQuickCaptureBar` | `AdvancedInputs` |
| `ArcRoutineScheduleEditor` | `Pickers` |

这说明当前目录只有“按控件类型”一个轴，没有“按产品/场景”的轴；`docs/06` 还规划了 57 项扩展，
如果继续按这个方式落位，这些项目会进一步变成杂物间。

#### ⑥ 解决方案与 `src/` 是扁平的

`ArcUI.slnx:2-27` 把 24 个项目全部塞进一个 `/src/` 解决方案文件夹；`src/` 下也没有
`Components/`、`Special/`、`Blocks/` 分组。24 个项目尚可，按 `docs/06` 的路线扩到 40+ 个项目后可读性会明显下降。
另外 `.csproj` 之间重复 4 条 `PackageReference`，只靠 `Directory.Build.props` 的 `AvaloniaVersion` 统一版本。

#### ⑦ 命名与命名空间不一致

* 命名空间：绝大多数是 `<Project>.Controls` / `<Project>.Pages`，但 `Charts` 用单一
  `Arc.Avalonia.Charts`，`BlocksData` 用 `Arc.Avalonia.Blocks.Data`（控件）与 `.Pages`（页面）混合；
* 项目名：`SpecialAIExplainers`、`BlocksAI` 缺少分隔符，读起来是 `SpecialAIExplainers` 而不是
  `Special.AIExplainers`；
* 基础层 `Theme/` 与 `Themes/` 并存，只有基础层有这种双目录；
* `Arc.Avalonia.Feedback.Internal.ArcUi` 与基础层 `ArcUi` 同名，检索时会产生歧义。

#### ⑧ 工程卫生与验证缺口

* 12 个项目下存在 `bin-alt/` 构建输出，而 `.gitignore` 只忽略了 `bin/`，没有 `bin-alt/`；
* 没有 `tests/`、没有测试工程，质量保障完全依赖 Gallery 的 `--audit` / `--smoke` / 截图；
* 没有 CI 配置、没有 `global.json`、没有 `.editorconfig`；
* 页面按 `PartTwo`/`PartThree`/`PartFour`/`PartFive` 和单页 `Sm` 命名，是 1400px 高度限制下的补丁式拆分，
  页面层缺少与 catalog 一一对应的组织方式（组件层是共享页，Special/Blocks 才是单项页）。

---

## 6. 改进建议

### P0：低风险、立刻可做

1. `.gitignore` 增加 `bin-alt/`，清理 12 个残留构建目录；`.recon/` 保持忽略。
2. 去重 `ArcAnimatedCounter` / `ArcActivityHeatmap`：保留一份（建议放 `Charts` 或新的共享 `DataDisplay`），
   另一个项目改为引用；`ArcUi` 二选一改名（如 Feedback 的改为 `FeedbackUi`）。
3. 统一命名空间后缀（`Charts` 补 `.Controls`/`.Pages`，`BlocksData` 统一），统一 `Theme/` 与 `Themes/`。
4. `ArcUI.slnx` 用解决方案文件夹分组：`/Foundation`、`/Components`、`/Special`、`/Blocks`、`/App`，
   不动文件路径，只提升导航。
5. 增加一个最小测试工程（token 转换、`ArcSpringEasing`、catalog 计数校验），把 `--audit` 的断言搬进 `dotnet test`。

### P1：下一轮功能开发前完成

6. **把 P0/Agent 能力收拢成独立项目**：`src/Arc.Avalonia.AgentUI`（Agent transcript / run bar / approval /
   ask card / attention inbox / background dock）与 `src/Arc.Avalonia.LifeToday`（reminder / brief / agenda /
   medication / routine editor / quick capture），从 `Content`、`Feedback`、`SpecialAIWidgets`、`BlocksAI`、
   `BlocksApp`、`Pickers`、`AdvancedInputs` 中迁出。这样“按类型”和“按产品”两个轴分离，后续 57 项扩展有处可放。
7. **抽共享内部原语**：把 `ArcUi`、`ArcDraw`、`*Visuals`、布局/文字/画笔辅助合并到一个内部项目
   （如 `Arc.Avalonia.Internal`，`internal` 可见性或 `InternalsVisibleTo`），结束每个项目各造一份 `*Kit` 的局面。
8. **拆分两个大杂烩**（按 catalog 类别，而不是按难度）：
   * `Actions` → `Actions`（按钮/菜单）+ `Navigation` + `Overlays` + `Messages`；
   * `Feedback` → `Feedback`（进度/头像/卡片）+ `DataDisplay`（表格/代码/JSON/filter）；
   * `Content` 的 activity（chat-thread）与 tables（reorderable-list）可并入对应项目。
   同时把 `AdvancedInputs` 并回 `Inputs`、`AdvancedActions` 并回 `Actions`，让“Text fields / Selects / Toggles /
   Sliders / Buttons”各自只出现在一个项目里。
9. **明确展示层边界**：若坚持“仓库即展示”，在 README 写清“组件程序集包含 Pages，不作为可复用库发布”；
   若要发布组件库，把 `Pages/` 拆到 `samples/` 或每项目的 `*.Demo` 程序集，组件项目只引用基础层。

### P2：规模再上一个量级时

10. `src/` 物理分组为 `src/Foundation`、`src/Components`、`src/Special`、`src/Blocks`、`src/Apps`，
    或至少保持解决方案文件夹分组；新增项目时同步 `slnx`、Gallery 引用、`App.axaml` 三处。
11. 页面生成化：Special/Blocks 的“每项一页”可由 catalog 驱动生成骨架，减少 200+ 个手写页面类；
    组件层的 `PartTwo…PartFive` 改为按 catalog 类别分页。
12. 统一项目命名分隔符（`Special.AIExplainers` / `Blocks.AI`），并在 `Directory.Packages.props` 集中包版本。

---

## 7. 建议的目标结构（渐进式，不必一次搬完）

```
src/
├── Foundation/
│   ├── Arc.Avalonia                    # tokens/theme/motion/icons/5 基础控件
│   └── Arc.Avalonia.Internal           # 共享组合原语（ArcUi/ArcDraw/Visuals）
├── Components/                         # 按 catalog 类别，而不是难度
│   ├── Arc.Avalonia.Inputs             # Text fields + Special inputs
│   ├── Arc.Avalonia.Selects            # Selects + Toggles + Sliders
│   ├── Arc.Avalonia.Actions            # Buttons + Menus
│   ├── Arc.Avalonia.Navigation
│   ├── Arc.Avalonia.Overlays
│   ├── Arc.Avalonia.Feedback           # Progress + Avatars + Cards
│   ├── Arc.Avalonia.DataDisplay        # Tables + code/json + data-grid
│   ├── Arc.Avalonia.Pickers
│   ├── Arc.Avalonia.Effects            # Text effects + messages
│   ├── Arc.Avalonia.Media
│   └── Arc.Avalonia.Content            # Editors + Activity
├── Agent/                              # 新的产品能力轴（P0/后续扩展）
│   ├── Arc.Avalonia.AgentUI
│   └── Arc.Avalonia.LifeToday
├── Special/                            # 保持现状：5 组 × 11
│   ├── Arc.Avalonia.Special.Glass
│   ├── Arc.Avalonia.Special.Motion
│   ├── Arc.Avalonia.Special.Media
│   ├── Arc.Avalonia.Special.AIWidgets
│   └── Arc.Avalonia.Special.AIExplainers
├── Blocks/                             # 保持现状：6 组
│   ├── Arc.Avalonia.Blocks.Marketing
│   ├── Arc.Avalonia.Blocks.Commerce
│   ├── Arc.Avalonia.Blocks.App
│   ├── Arc.Avalonia.Blocks.Team
│   ├── Arc.Avalonia.Blocks.Data
│   └── Arc.Avalonia.Blocks.AI
└── Apps/
    ├── Arc.Avalonia.Demo               # 页面契约（或拆到 samples/）
    └── Arc.Avalonia.Gallery
```

---

## 8. 结论

| 维度 | 评价 |
| --- | --- |
| 分层与依赖 | ✅ 优：四层单向、无环，叶子项目零互相依赖 |
| Special / Blocks 家族划分 | ✅ 优：与上游 5 组 / 6 组严格 1:1 |
| 128 个常规组件的划分 | ⚠️ 差：混合了“上游类别 / 实现难度 / 开发轮次”三种轴，17/23 类被拆散 |
| 项目内聚性 | ⚠️ 中：`Actions`、`Feedback` 过大且混杂；其余项目内聚良好 |
| 复用与去重 | ⚠️ 差：无共享原语层，已出现 3 组同名类型、2 组重复实现 |
| 可发现性 | ⚠️ 中：按项目名只能猜到大类，具体组件需查 README/docs/03 |
| 可扩展性 | ⚠️ 中：P0 的 14 项已被证明无处安放；扩到 57 项前需要新的产品轴 |
| 工程卫生 | ⚠️ 中：`bin-alt/` 残留、无测试/CI、命名空间不一致 |
| 文档与验收 | ✅ 优：README/CONVENTIONS/01–07 文档齐全，audit/smoke/截图闭环 |

**最终判断：**

* 如果这是一个**以“复刻 + 展示 + 多智能体并行开发”为目标的仓库**——当前划分合理，甚至是最省协调成本的方案：
  一人一项目、页面随库走、依赖单向、Special/Blocks 与上游一一对应。
* 如果这是一个**要长期演进、被业务项目引用的组件库**——当前划分不合理：`Demo` 反向依赖、类别被拆散、
  共享原语缺失、Agent/Life 新能力没有家。建议按第 6 节的 P0 → P1 顺序渐进重组，
  先做“低风险去重 + 分组 + 收拢 AgentUI”，再决定是否按 catalog 类别彻底重切。
