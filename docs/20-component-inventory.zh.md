# 20 · 组件清单（当前实现清点）

> **口径**：本文件由仓库实际产物统计（`tools/api-baselines/*.txt` 的公开类型与基类链 + `src/**` 源码 + `src/Docs/**` 页面与文档），不是设计规划。
> **快照**：M2 第三波之后（commit `ff9ad27`），M3 Markdown 波次后增补（见 §10）。
> **一句话**：**32 个 NuGet 包**、**262 个公开 UI 控件**、973 个公开类型、144 个路由事件、164 个 ControlTheme、323 个文档页面（294 篇目录文档）、111 个页面级 block。

---

## 1. 概览

| 维度 | 数量 |
|---|---|
| 可发布包 | **31**（L1 ×1、L2.5 ×1、L2 ×13、L3 ×11、L4 ×3、元包 ×2） |
| 公开 UI 控件（派生自 Avalonia 控件体系） | **256** |
| 公开类型（class/record/struct/enum/interface） | **907** |
| 属性注册（`StyledProperty`/`DirectProperty` 字段） | ≈**1213** |
| 路由事件（`RoutedEvent.Register`） | **139**（全部带 CLR 包装，100%） |
| ControlTheme（主题项） | **158** |
| 设计 token | **82** 个常量 / 47 个 Fluent 覆盖键 |
| 内置图标 | **264**（Lucide，可运行时注册扩展） |
| 文档页面 / 目录文档 | **320** / **294** |
| 页面级 block（L3 组合） | **111** |
| 源码行数（src 下 31 个发布库） | ≈**125,500** |

## 2. 分层与包清单

```text
L0 平台（官方 Avalonia 12.1.4：Avalonia / Themes.Fluent / Fonts.Inter；DataGrid 仅 Feature 包）
└─ L1 Arc.Avalonia.Foundation                    基础层：token/主题引擎/OKLCH/图标/动效 + 6 个原子控件
└─ L2 通用 UI（11 个组件族 + 服务 + Feature）
   ├─ Arc.Avalonia.Inputs / Actions / Navigation / Overlays / Feedback / DataDisplay
   ├─ Arc.Avalonia.Charts / Pickers / Effects / Content / Media
   ├─ Arc.Avalonia.Services                     对话框/通知/导航/文件选择 + ArcWindow 窗口壳
   └─ Arc.Avalonia.Feature.DataGrid             官方 DataGrid 封装（大数据，唯一允许的官方附加包）
└─ L2.5 Arc.Avalonia.Theme                       一行 <arc:ArcTheme/> 聚合入口（聚合例外层）
└─ L3 组合层
   ├─ Special.{Glass,Motion,Media,AIWidgets,AIExplainers}      55 个特殊组件
   └─ Blocks.{Marketing,Commerce,App,Team,Data,AI}             111 个页面级 block
└─ L4 Agent 顶层
   ├─ Arc.Avalonia.Agent                        表现运行时（reducer/workspace/流式合并/审批队列）+ 5 个组合控件
   ├─ Arc.Avalonia.AgentUI                      6 个 Agent 叶子控件（transcript/run bar/审批/提问/后台 dock/收件箱）
   └─ Arc.Avalonia.LifeToday                    7 个生活/日程控件
└─ 元包：Arc.Avalonia（Theme + 11 组件 + Agent + AgentUI + LifeToday） / Arc.Avalonia.All（+Special +Blocks）
```

## 3. L1 基础层（6 控件）

`src/Foundation/Arc.Avalonia`（程序集 `Arc.Avalonia.Foundation`，2,263 行）

| 能力 | 内容 |
|---|---|
| 颜色 | OKLCH 色彩引擎 `ArcColor`、`ArcPalette`（82 token × 明暗 × 8 accent，支持自定义种子色派生）、`ArcColor.ContrastRatio` |
| 主题 | `ArcThemeManager`（`Apply(app, options)` / `SetAccent` / `Applied`）、`ArcThemeOptions`（Scheme/Accent/AccentColor/Density/CornerRadiusScale）、`ArcResources`（变体感知、可注入 host）、`ArcBaseStyles` |
| 图标 | `ArcIconData` 264 个 path、`ArcIconRegistry`（运行时注册 / `IArcIconProvider`）、`ArcIcon` 几何缓存 |
| 动效 | Spring/Snappy/Morph 曲线与时长 token |
| 控件 | `ArcButton`、`ArcBadge`、`ArcCard`、`ArcSpinner`、`ArcIcon`、`ArcVirtualList`（数据驱动列表原语，10k 行窗口化） |

## 4. L2 通用 UI 组件（11 族 / 155 控件 / 131 页）

| 族（包） | 控件 | 代表控件 | 页面 | DP | 路由 | 主题 | LOC |
|---|---:|---|---:|---:|---:|---:|---:|
| `Inputs` | 32 | TextField/Textarea/SearchField/Combobox/Select/MultiSelect/MorphSelect/Checkbox/Switch/RadioGroup/RadioCards/Slider/ElasticSlider/RangeHistogramSlider/NumberField/MoneyInput/PhoneInput/MentionInput/TagInput/ScrubInput/Rating/SegmentedControl/ChipGroup/BillingToggle/BillingPrice/InlineEdit/PasswordField/PasswordStrength/ExpandingSearch/ShortcutRecorder/FieldBase | 29 | 196 | 16 | 40 | 13,132 |
| `Actions` | 23 | ActionButton/SplitButton/ButtonGroup/FloatingButtonGroup/ExpandingButtonGroup/IconButton/FlatButton/CopyButton/ActionSwap/ConfirmMorph/IconMorph/Glide/AdvancedGlide/Accordion/Alert/Toast/ToastHost/ContextMenu/DropdownMenu/UserMenu/ThemeSwitcher/ExpandableText/MenuSurface | 19 | 118 | 11 | 19 | 7,240 |
| `Navigation` | 4 | Tabs / Breadcrumb / Pagination / MegaMenu | 4 | 22 | 4 | 4 | 2,813 |
| `Overlays` | 5 | Dialog / Drawer / Popover / Tooltip / ShareAccess | 5 | 29 | 9 | 5 | 2,941 |
| `Feedback` | 17 | Progress / ProgressTrack / Skeleton / Spinner 家族 / Avatar / AvatarGroup / PresenceStack / EmptyState / Stepper / Timeline / CommentThread / Countdown / MetricCard / ActivityHeatmap / HeatmapGrid / AnimatedCounter / UsageBar / UsageMeter | 14 | 101 | 7 | 13 | 5,830 |
| `DataDisplay` | 7 | SortableDataTable / TreeTable（10k 窗口化）/ TreeView / DataGrid（自有，小数据）/ FilterToolbar / CodeBlock / JsonViewer | 10 | 66 | 16 | 7 | 7,213 |
| `Charts` | 22 | Line/Bar/Donut/Funnel/Radar/Waffle/SplitBar/Streamgraph/Treemap/Sankey/RaceBar/Slope/Ridgeline/Sparkline/Gauge/Brush/RealtimeStream/Sunburst/Treemap/ChartCard/ActivityHeatmap/AnimatedCounter | 20 | 125 | 5 | 20 | 9,876 |
| `Pickers` | 17 | Calendar/DatePicker/DateRangePicker/TimePicker/ColorPicker/ColorEditor/ColorArea/ColorRamp/EmojiPicker/FileDropzone/AvatarUpload/AvatarCrop/AvatarRing/SignaturePad/SignatureSurface/PickerBase | 9 | 118 | 14 | 22 | 8,330 |
| `Effects` | 9 | AnnouncementBar / CookieConsent / BottomSheet / HoverCard / ExpandableCard / InViewTitle / TextMorph / TextReveal / TextShimmer | 9 | 54 | 9 | 9 | 4,464 |
| `Content` | 4 | RichTextEditor / ChatThread / ReorderableList / OnboardingChecklist | 4 | 28 | 7 | 4 | 4,445 |
| `Media` | 15 | Carousel/CarouselDot/CarouselPanel / CardStack / ImageCompare/CompareSurface / Reactions/ReactionPill / HoldToConfirm / SwipeActions/SwipeActionButton / ScrollArea/ScrollOverlay / ResizablePanels/PanePanel | 8 | 69 | 10 | 11 | 5,337 |
| **合计** | **155** | — | **131** | — | — | — | — |

### 4.1 L2 服务层（1 包）

`Arc.Avalonia.Services`（1,010 行）：`ArcWindow`（PageHost + OverlayHost + 可选自绘标题栏）、`ArcServiceProvider`、`IDialogService`（串行对话框 + 焦点陷阱）、`INotificationService`（去重 + 生命周期）、`INavigationService`（栈/替换/回退）、`IFilePickerService`（StorageProvider 默认实现 + fake 注入）、`IArcThemeService`。

### 4.2 Feature 包（1 包）

`Arc.Avalonia.Feature.DataGrid`（375 行）：`ArcDataGridHost` 包装官方 `Avalonia.Controls.DataGrid`（分页/大数据/Arc 皮肤），与自有 `ArcDataGrid`（小数据）分工。

## 5. L2.5 主题聚合（1 包）

`Arc.Avalonia.Theme`（300 行）：`<arc:ArcTheme/>` 一行接入 = 官方 Fluent 基线 + 47 个 Fluent 键覆盖 + 82 token 的明暗/8 accent 字典 + 11 个组件族主题 `StyleInclude`；`ArcThemePalettes` 驱动 Fluent `SystemAccentColor`，`ArcThemeOptions` 支持自定义品牌色/密度/圆角。

## 6. L3 Special 特殊组件（5 包 / 68 控件 / 55 篇文档）

| 包 | 控件数 | 组件 |
|---|---:|---|
| `Special.Glass` | 14 | GlassCard / GlassBackdrop / GlassMenu / GlassSegmented / GlassTabBar / HoloCard / CardToArticle / ControlCenter / DurationDial / LinkUnfurl / RubberMeter / ShareSheet / SheetStack / WalletStack |
| `Special.Motion` | 12 | ActionMorph / BookingPill / Dock / KeycapKeyboard / LiquidTabBar / MorphNav / MotionCanvas / OrbitLogos / OrbitMenu / SandText / SlotText / TypewriterTerminal |
| `Special.Media` | 12 | CoverFlow / DotGrid / GravityWell / LightboxGallery / NowPlaying / PageCurl / PhotoGrid / ProductGallery / SpecialCanvas / StickerPeel / VoiceOrb / VoiceRecorder |
| `Special.AIWidgets` | 18 | AgentLiveRing / AgentMark / AgentPlan / AgentStatus / AgentTimeline / ActivityRings / RingsView / ToolCall / ContextWindow / ContextCells / MiniBar / StepMark / MorphBlock / MorphLoader / SkeletonMorph / StretchRefresh / InkFluid / BlackHole |
| `Special.AIExplainers` | 12 | AgentLoop / PromptCache / ReasoningTree / VectorSearch / SpeculativeDecoding / MixtureOfExperts / MultiRegionFailover / DataFlow / PaymentFlow / Explainer / DateReel / TimeDial |

## 7. L3 Blocks 页面级组合（6 包 / 111 块）

| 包 | 块数 | 内容 |
|---|---:|---|
| `Blocks.App` | 21 | Login×3、Signup、Otp、Settings×3、Sidebar×3、Inbox×2、Search、CommandPalette、NotificationCenter、FileUpload、SelectionToolbar、MediaPlayer、MultiStepForm、SecuritySettings、DocsSidebar |
| `Blocks.Commerce` | 18 | ProductListing/Detail、Cart、Checkout×2、Billing×2、Usage×2、Invoice、Wallet、CancelFlow、Delivery、HelpCenter、Support×2、Integrations、Changelog、EmptyStates |
| `Blocks.Marketing` | 18 | Hero×2、SiteHeader/Footer、FeatureBento、Spotlight、StatsBand、LogoMarquee、Pricing×2、Comparison、FAQ、Contact、CTA、Newsletter、Blog、ScrollStory、PageHeader |
| `Blocks.Data` | 20 | MetricsDashboard、MetricExplorer、KpiDrilldown、Revenue×2、MRR Waterfall、BudgetVariance、HorizonChart、LineReplay、SmallMultiples、SemanticZoom、Choropleth、ConnectedScatter、JourneyFlow、Budget/Usage forecast、MarketTerminal、CustomerGalaxy、ActivityTerrain、ExplodedView、LayoutMorph |
| `Blocks.Team` | 16 | TeamDirectory/Members/Showcase、Invite、Roles、ApiKeys、Webhooks、EnvVars、ProjectBoard、Kanban、ReleaseReadiness、CohortRetention、Availability、WeekCalendar、OnboardingIllustration、TestimonialStage |
| `Blocks.AI` | 18 | AiChat/Answer/Composer/SidePanel、AgentRun/Tasks/Routines/Approvals/Memory/Orchestrator、ResearchRun、PromptStudio、KnowledgeBase、McpServers、EvalSuite、ModelArena、ModelPicker、DocumentExtract |

## 8. L4 Agent 顶层（3 包 / 18 控件）

| 包 | 控件/内容 |
|---|---|
| `Arc.Avalonia.Agent`（2,746 行） | **5 个组合控件**：`ArcAgentSurface`（组合根）、`ArcAgentComposer`、`ArcAgentStreamingText`、`ArcAgentToolCallCard`、`ArcAgentApprovalTray`。**表现运行时**：`IArcAgentWorkspace`/`IArcAgentSession` 纯 reducer、`ArcAgentEvent` 事件模型、`ArcDeltaCoalescer`（Dispatcher 批处理，无定时器）、`ArcApprovalQueue`/`ArcAttentionQueue`/`ArcArtifactIndex`、`ArcAgentItemViewRegistry`、宿主接缝 `IArcAgentTransport`/`IArcAgentCommandSink`/`IArcAgentServices`。边界由 BannedApiAnalyzers（RS0030→error）+ R9 门禁守住 |
| `Arc.Avalonia.AgentUI`（3,176 行） | **6 个叶子控件**：`ArcAgentTranscript`（增量追加 + 有界窗口）、`ArcRunControlBar`、`ArcToolApprovalSheet`、`ArcAgentAskCard`、`ArcBackgroundRunDock`、`ArcAgentAttentionInbox` |
| `Arc.Avalonia.LifeToday`（3,423 行） | **7 个控件**：`ArcDailyBrief`、`ArcDayAgenda`、`ArcMedicationSchedule`、`ArcQuickCaptureBar`、`ArcReminderCenter`、`ArcReminderRow`、`ArcRoutineScheduleEditor` |

## 9. 按"用途"再分类（交叉视图，不是分区；同一控件可能出现在多行）

| 用途 | 主要组件 | 主包 |
|---|---|---|
| 文本与表单输入 | TextField/Textarea/SearchField/Combobox/Select/MultiSelect/MorphSelect/Checkbox/Switch/RadioGroup/RadioCards/Slider 系列/NumberField/MoneyInput/PhoneInput/MentionInput/TagInput/ScrubInput/Rating/SegmentedControl/ChipGroup/Billing*/InlineEdit/Password*/ExpandingSearch/ShortcutRecorder ＋ RichTextEditor | `Inputs`（+ `Content`） |
| 动作与菜单 | ActionButton/SplitButton/ButtonGroup/Floating+ExpandingButtonGroup/IconButton/FlatButton/CopyButton/ActionSwap/ConfirmMorph/IconMorph/Glide*/Accordion/Alert/Toast*/ContextMenu/DropdownMenu/UserMenu/ThemeSwitcher/ExpandableText/MenuSurface ＋ Foundation `ArcButton` | `Actions`（+ `Foundation`） |
| 导航与容器 | Tabs/Breadcrumb/Pagination/MegaMenu；窗口壳 `ArcWindow`；面板容器 ResizablePanels/PanePanel/ScrollArea/ScrollOverlay/CarouselPanel | `Navigation`、`Services`、`Media` |
| 浮层与反馈 | Dialog/Drawer/Popover/Tooltip/ShareAccess；Progress/Skeleton/Avatar*/Presence/EmptyState/Stepper/Timeline/CommentThread/Countdown/MetricCard/Heatmap/Usage* | `Overlays`、`Feedback` |
| 数据展示与图表 | SortableDataTable/TreeTable/TreeView/ArcDataGrid/FilterToolbar/CodeBlock/JsonViewer；Line/Bar/Donut/Funnel/Radar/Waffle/… 22 个图表；官方 DataGrid 封装 | `DataDisplay`、`Charts`、`Feature.DataGrid` |
| 选择器与上传 | Calendar/DatePicker/DateRangePicker/TimePicker/ColorPicker 系列/EmojiPicker/FileDropzone/AvatarUpload/Crop/Ring/Signature* | `Pickers` |
| 内容与媒体 | RichTextEditor/ChatThread/ReorderableList/Onboarding；Carousel/CardStack/ImageCompare/Reactions/HoldToConfirm/Swipe/Resizable/Scroll | `Content`、`Media` |
| 特殊视觉 | 玻璃/动效/媒体/AI 装饰共 68 个（见 §6） | `Special.*` |
| Agent 会话与生活流 | AgentSurface/Composer/StreamingText/ToolCallCard/ApprovalTray + Transcript/RunBar/审批/提问/后台 dock/收件箱 + 7 个生活控件 | `Agent`、`AgentUI`、`LifeToday` |
| 页面级组合 | 登录/设置/收件箱/电商/营销/数据大屏/团队/AI 产品页共 111 个 block | `Blocks.*` |

## 10. 质量与契约现状

| 维度 | 状态 |
|---|---|
| 模板契约 | **108/108** 模板控件声明 `[TemplatePart]`（511 条属性），`PART_*` 命名统一，主题 `x:Name`/`#选择器` 同步 |
| 事件契约 | **139** 个 `RoutedEvent`，全部带 CLR 包装；可路由 legacy 事件 **99/99** 有路由对应物；6 个非 `Interactive` 类的 CLR 事件按设计不路由 |
| 主题 | 158 个 ControlTheme；47/47 Fluent 键覆盖生效；token-only 门禁（R6）0 硬编码色；一行 `<arc:ArcTheme/>` |
| 依赖边界 | 依赖闭包仅官方 Avalonia 包（R3/R5）；Feature.DataGrid 是唯一 `Avalonia.Controls.DataGrid` 使用者 |
| 渲染边界 | R8 无渲染层信号（帧调度/绘制缓存/主题版本协议）；流式合并走 `Dispatcher` |
| 文档 | 320 页面全部通过 smoke；294 篇目录文档通过 audit；双主题截图基线 |
| 缺口 | 0 个 `AutomationPeer`/`AutomationId`/`TabIndex`（M3）；本地化 0 `.resx`（M2 尾/ M3）；30 个可路由事件已补齐但**无运行时冒泡断言**（M3 测试基线）；`*Size` 枚举未统一、`ItemsSource` 迁移未完成 |

## 11. 统计口径

* 控件 = `tools/api-baselines/*.txt` 中基类链最终落到 `Avalonia.Controls.*` 或 `Interactive/StyledElement/Visual/Layoutable` 的公开类型（含 base/panel 帮助类型，如 `ArcFieldBase`、`ArcPickerBase`、`ArcCarouselPanel`）。
* 属性注册 = 基线中 `field static … StyledProperty/DirectProperty` 行数（含内部使用的附加属性 holder）。
* 页面/文档 = `src/Docs/Arc.Avalonia.Docs/**/Pages/*Page.cs` 与 `Docs/*.json`；block 数 = Blocks 分组下页面数（111）。
* LOC = `src/**` 下 31 个发布工程（排除 Docs/Gallery/samples/POC）的 C# 行数。
* 可复算：`.recon/component-controls.json`（控件清单）、`.recon/component-stats.json`（每包统计）。

---

## 10. M3 Markdown 波次增量（2026-11-08）

新增 `Arc.Avalonia.Markdown`（L2，32 个包）与 `pura-agent` 组件；PuraMarkdown 的解析层作为唯一外部依赖特例
（`PuraMarkdown.Syntax` + `Markdig`，仅该包，ADR 0006）。

| 维度 | 增量 | 现在 |
|---|---|---|
| 可发布包 | +1 `Arc.Avalonia.Markdown` | **32** |
| 公开 UI 控件 | +4 Markdown（View/DocumentView/ChatView/CodeBlock）+2 Agent（ChoiceCard/ConfirmCard） | **262** |
| 公开类型（API baseline 统计） | +66 | **973** |
| 路由事件 | +5 | **144** |
| ControlTheme | +6 | **164** |
| 文档页面 | +3（Markdown / Markdown Extended / Markdown Chat） | **323**（gallery smoke pageShows） |
| Public API baseline | 32/32（新增 Markdown，Agent/AgentUI 更新） | GREEN |
| 仓库门禁 | 9/9 | GREEN（R5 32/32 包） |

Markdown 能力：CommonMark+GFM 全节点矩阵、代码高亮（17 语言族、无第三方库）、GFM 表格、图片/链接/剪贴板
平台契约、稳定边界流式（committed/tail 两窗口 + 增量控件复用）、消息级虚拟化（1000 条消息约 12 个 realized 行）、
`pura-agent` choice/confirm 与交互状态恢复、中文默认本地化、主题/浅深色 token 化。**块级虚拟化不在本波次范围**
（单篇超长文档请拆页或使用 Chat 的消息级虚拟化）。

