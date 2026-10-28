# 在 Avalonia 中复刻 Arc：实现说明

本文说明这套 Avalonia 复刻的结构、token 移植方式、控件实现模式、验收流程，以及已完成/未完成的组件范围。
上游依据见 [01-arc-analysis.zh.md](01-arc-analysis.zh.md) 与 [02-component-specs.zh.md](02-component-specs.zh.md)。

## 1. 技术栈与解决方案结构

Avalonia 12.1.4 / .NET 10，纯 C# 与 XAML，不依赖任何第三方 UI 或图表库；NuGet 完全离线还原。

| 项目 | 职责 |
| --- | --- |
| `Arc.Avalonia` | 基础层：OKLCH 色彩引擎、调色板与主题管理、字体与排版、动效、161 个 Lucide 图标、`ArcButton` / `ArcCard` / `ArcBadge` / `ArcIcon` / `ArcSpinner` / `ArcVirtualList` |
| `Arc.Avalonia.Internal` | 共享内部原语：menu surface / glide、icon button、feedback 组合助手与 tween、action models（不是公开组件层） |
| `Arc.Avalonia.Demo` | `ArcPage` 页面契约、`DemoUi` 布局助手、基于反射的页面发现 |
| `Arc.Avalonia.AgentUI` | P0 Agent UI：transcript、run control bar、tool approval sheet、question card、background dock、attention inbox |
| `Arc.Avalonia.LifeToday` | P0 Today：reminder row/center、daily brief、day agenda、medication schedule、quick capture、routine editor |
| `Arc.Avalonia.Inputs` | 输入框、特殊输入、选择器、开关、滑杆，以及 inline edit、scrub/phone/mention/shortcut 输入、expanding search、morph select、弹性/直方图滑杆、billing toggle、radio cards（原 AdvancedInputs 已并入） |
| `Arc.Avalonia.Actions` | 按钮组、菜单、折叠、消息，以及 action button、floating/expanding 按钮组、icon morph、user menu（原 AdvancedActions 已并入） |
| `Arc.Avalonia.Navigation` | Tabs、Breadcrumb、Pagination、Mega menu |
| `Arc.Avalonia.Overlays` | Dialog、Drawer、Popover、Tooltip、Share access |
| `Arc.Avalonia.Feedback` | 反馈、进度、头像、卡片、在线状态、活动流、计数器、activity heatmap |
| `Arc.Avalonia.DataDisplay` | 可排序表格、树形表格、树视图、筛选工具栏、代码块、JSON 查看器、Data grid |
| `Arc.Avalonia.Charts` | 基于系列色板的自绘图表 |
| `Arc.Avalonia.Pickers` | 日历、日期/范围/时间、颜色、文件拖放、签名 |
| `Arc.Avalonia.Effects` | 文字 reveal / in-view / morph / shimmer、announcement bar、cookie consent、hover card、bottom sheet、expandable card |
| `Arc.Avalonia.Content` | markdown 富文本编辑器、可拖拽排序列表、聊天线程、onboarding checklist |
| `Arc.Avalonia.Media` | hold to confirm、swipe actions、reactions、image compare、carousel、card stack、scroll area、resizable panels |
| `Arc.Avalonia.Special.Glass` | Glass menu / segmented / card / tab bar、holo card、wallet stack、sheet stack、control center、card to article、link unfurl、share sheet |
| `Arc.Avalonia.Special.Motion` | Action morph、morph nav、dock、liquid tab bar、orbit menu/logos、booking pill、keycap keyboard、slot text、sand text、typewriter terminal |
| `Arc.Avalonia.Special.Media` | Product gallery、lightbox、photo grid、cover flow、voice orb、now playing、voice recorder、page curl、sticker peel、dot grid、gravity well |
| `Arc.Avalonia.Special.AIWidgets` | Agent status、tool call、agent plan/timeline、context window、ink fluid、activity rings、stretch refresh、morph loader、skeleton morph、black hole |
| `Arc.Avalonia.Special.AIExplainers` | Data/payment flow、multi-region failover、vector search、agent loop、MoE、speculative decoding、prompt cache、reasoning tree、time dial、date reel |
| `Arc.Avalonia.Blocks.Marketing` | 18 个营销/站点区块（header、hero、FAQ、blog、pricing、bento…） |
| `Arc.Avalonia.Blocks.Commerce` | 18 个电商/计费区块（listing、detail、cart、checkout、wallet、invoice…） |
| `Arc.Avalonia.Blocks.App` | 21 个应用壳层区块（sidebar、settings、inbox、command palette、OTP、auth…） |
| `Arc.Avalonia.Blocks.Team` | 16 个团队协作区块（directory、board、calendar、API keys、RBAC、cohort…） |
| `Arc.Avalonia.Blocks.Data` | 20 个数据可视化区块（dashboard、revenue、terrain、globe、galaxy、choropleth、candle…） |
| `Arc.Avalonia.Blocks.AI` | 18 个 AI 产品区块（composer、chat、research、arena、agents、MCP、eval…） |
| `Arc.Avalonia.Gallery` | 展示应用 + 离屏截图工具 + 分类审计（`--audit`） |

Gallery 侧边栏现在由 `ArcCatalog` 驱动（数据来自 `docs/arc-catalog.json`，由公开的 llms-small.txt 与
introduction 侧边栏生成），按 uiarc.dev 的分类呈现 **Components（23 类 / 128 项）、Blocks（6 组 / 111 项）、
Special components（5 组 / 55 项）**，并提供 All / Free / Pro 过滤、搜索与目录审计；每个目录项要么有独立的
`ArcItemPage`，要么通过章节标题/别名映射到共享的演示页。

## 2. Token 移植

`foundation.css` 的每个 token 都按原值用 OKLCH 写在 `ArcPalette.Build(scheme, accent)` 中，由
`ArcColor`（OKLab ↔ sRGB、CSS `color-mix(in oklab)`）换算为 Avalonia 颜色：

* 明/暗两套中性色阶，8 个 accent（neutral、violet、blue、green、amber、orange、coral、rose），
  accent 专属的 `--control-on/glyph/thumb` 表（保证白色滑块在每个色相上都可读）。
* 只暴露语义角色：`ArcBackground`、`ArcSurface`、`ArcSurfaceMuted`、`ArcForeground`、
  `ArcTextSecondary`、`ArcBorder(Submit/Strong)`、`ArcAccent(+Strong/Subtle/Foreground)`、
  `ArcSuccess/Warning/Danger`、`ArcControl*`、`ArcSeries1..4`、`ArcGradient*`。
* 圆角 18 / 26 / 34 / pill，控件高度 36 / 44 / 50，4px 间距网格，12–72 字号，400/500 字重，
  浮动层专属阴影。
* 图表系列色按 foundation.css 中的相对 OKLCH 公式从 accent 推导；accent 为中性时回落到蓝色系列。
* 主题切换：`ArcThemeManager.Apply(app, scheme, accent)` 替换 Application 级资源字典，组件全部通过
  `DynamicResource` 取值，因此换主题/换 accent 无需重建可视化树。

## 3. 控件实现模式

* 交互控件继承 `TemplatedControl` / `ContentControl` / `Button`，变体用枚举到 CSS 风格 class 的映射
  （`Classes.Set("primary", …)`），模板写在 `ControlTheme` 中，只允许 `{DynamicResource}` token。
* 浮层使用 Avalonia `Popup` / `Flyout` / 页面内 overlay panel，面板圆角 26、`ArcShadowFloating`、
  1px `ArcBorder`；Esc 与外部点击关闭。
* 焦点：`ArcButton` 系列的键盘焦点显示 2px `ArcFocusOutline`（offset 2），指针焦点不显示；输入类控件
  用边框与光标表达焦点。
* 动效：`ArcSpringEasing` 用阻尼振子采样，`SpringSmooth`（0.4s，无过冲）用于状态/尺寸，
  `SpringSnappy`（0.26s）用于按压，`SpringMorph`（0.42s）用于共享高亮；`ArcThemeManager.ReducedMotion`
  为真时跳过位移。断言类动效（数值、进度、开关）永不回弹。
* 文本：`arc-*` 样式类（`arc-display`、`arc-title`、`arc-heading`、`arc-body-sm`、`arc-caption`…）统一
  字体、字号、行高与字距。

## 4. 已完成范围（Arc 目录 294 项全部落地）

基础层：Button、Card、Badge、Icon、Spinner。Gallery 共 **219 个展示页**：常规组件 53 页（含 Tree table）、
Special components 55 页（每项一页）、Blocks 111 页（每项一页），明暗两套合计 **438 张**离屏截图。
`--audit` 模式会核对 294 个目录项是否都能从侧边栏到达（独立 item page / 章节标题匹配 / 共享页别名），
当前结果 **294/294，missing=0，pageFailures=0**。

### 输入与选择（Inputs，18）
Input、Textarea、PasswordField、PasswordStrength、SearchField、NumberField、MoneyInput、TagInput、
Select、Combobox、MultiSelect、ChipGroup、Checkbox、RadioGroup、Switch、SegmentedControl、Slider、Rating。

### 动作、菜单与浮层（Actions，19）
ButtonGroup、SplitButton、CopyButton、ActionSwap、ConfirmMorph、ThemeSwitcher、DropdownMenu、ContextMenu、
Tabs、Breadcrumb、Pagination、Accordion、ExpandableText、Dialog、Drawer、Popover、Tooltip、Toast/ToastHost、
Alert。

### 反馈、数据与活动（Feedback，18）
Progress、Skeleton、Stepper、Countdown、UsageMeter、Avatar、AvatarGroup、MetricCard、EmptyState、Timeline、
CommentThread、CodeBlock、JsonViewer、SortableDataTable、TreeView、FilterToolbar、AnimatedCounter。

### 图表（Charts，13 + 容器）
LineChart、BarChart、DonutChart、Sparkline、Gauge、WaffleChart、SlopeChart、Treemap、Ridgeline、Streamgraph、
ActivityHeatmap、AnimatedCounter、SplitBar，以及 `ArcChartCard` 图表容器。

### 选择器（Pickers，7）
Calendar、DatePicker、DateRangePicker、TimePicker、ColorPicker、FileDropzone、SignaturePad。

### 高级输入（AdvancedInputs，11）
InlineEdit、ScrubInput、PhoneInput、MentionInput、ShortcutRecorder、ExpandingSearch、MorphSelect、
ElasticSlider、RangeHistogramSlider、BillingToggle/ArcBillingPrice、RadioCards。

### 高级动作（AdvancedActions，5）
ActionButton、FloatingButtonGroup、ExpandingButtonGroup、IconMorph、UserMenu。

### 效果与浮层（Effects，9）
TextReveal、InViewTitle、TextMorph、TextShimmer、AnnouncementBar、CookieConsent、HoverCard、BottomSheet、
ExpandableCard。

### 内容（Content，4）
RichTextEditor（markdown 标记 + 实时预览 + 浮动工具栏 + slash 菜单 + HTML/Markdown 输出）、
ReorderableList（FLIP 重排 + 键盘拖放）、ChatThread（分组气泡、输入指示、附件、自动滚动）、
OnboardingChecklist（进度环、步骤状态、可展开详情）。

### 媒体与手势（Media，8）
HoldToConfirm、SwipeActions、Reactions、ImageCompare、Carousel、CardStack、ScrollArea、ResizablePanels。

### 图表补充（Charts，Brush chart + 6 个 Pro 图表）
BrushChart（密集时间序列 + 概览条 + 可拖动/缩放窗口 + 事件标记）；Sunburst（同心环层级 + 钻取 + 面包屑）、
SankeyFlow（分层流图 + 悬浮追踪 + 节点拖动）、FunnelChart（漏斗 + 阶段转化）、RadarChart（多轴雷达 +
图例切换）、RealtimeStream（滚动实时流 + 暂停）、RaceBarChart（排名动画 + 拖动进度条）。

### Pro 组件（13 个，全部按公开文档与视觉描述独立实现）
* **Pickers**：EmojiPicker（418 个离线表情、8 分类、即时搜索、滚动同步分类、肤色、最近使用、虚拟化网格、
  全键盘导航）、AvatarUpload（就地裁剪：拖拽平移、滚轮缩放、进度环上传、成功/失败/重试/移除）。
* **Actions**：MegaMenu（共享浮层面板在区块间变形、方向性滑入、光标下滑、悬停意图与安全三角、
  窄屏折叠手风琴）、ShareAccess（邮箱 chip 校验与建议、角色菜单、移除 + 5 秒撤销、链接权限、复制确认）。
* **Feedback**：PresenceStack（重叠头像 + 滚动溢出计数 + 状态点 + 输入中脉冲 + 悬停展开姓名）、
  DataGrid（吸顶表头、排序/列宽拖动/列可见性、多选与 Shift 范围、单元格键盘导航、行内编辑、汇总行、
  空状态）。
* **Charts**：Sunburst、SankeyFlow、FunnelChart、RadarChart、RealtimeStream、RaceBarChart。
* **Charts 既有**：SplitBar（在上一轮按公开描述实现）。
* **补充**：Tree table（层级表格：三态选择、按层排序、懒加载子行、树形键盘导航）。

### Special components（55 个，全部按公开文档独立实现）

按 Arc 站点的五组划分，每项一个 `ArcItemPage`，演示是可交互/可播放的自绘或组合控件：

* **Glass and surfaces（11）**：Glass menu、Glass segmented、Glass card、Glass tab bar、Holo card、
  Wallet stack、Sheet stack、Control center、Card to article、Link unfurl、Share sheet。
* **Motion and interaction（11）**：Action morph、Morph nav、Dock、Liquid tab bar、Orbit menu、
  Orbit logos、Booking pill、Keycap keyboard、Slot text、Sand text、Typewriter terminal。
* **Media and input（11）**：Product gallery、Lightbox gallery、Photo grid、Cover flow、Voice orb、
  Now playing、Voice recorder、Page curl、Sticker peel、Dot grid、Gravity well。
* **AI widgets（11）**：Agent status、Tool call、Agent plan、Agent timeline、Context window、Ink fluid、
  Activity rings、Stretch refresh、Morph loader、Skeleton morph、Black hole。
* **AI explainers（11）**：Data flow、Payment flow、Multi-region failover、Vector search、Agent loop、
  Mixture of experts、Speculative decoding、Prompt cache、Reasoning tree、Time dial、Date reel。

### Blocks（111 个，页面级组合）

按 6 个实现/导航组划分，每组是可直接迁移的产品界面示例：

* **Marketing and site（18）**：site header/footer、hero、FAQ、contact、blog grid、comparison table、
  stats band、CTA、newsletter、logo marquee、page header、hero signup、spotlight grid、scroll story、
  feature bento、plan comparison、pricing calculator。
* **Commerce（18）**：product listing/detail、cart drawer、checkout summary、usage pricing/billing、
  wallet card、invoice studio、delivery tracker、cancel flow、integrations、help center、support
  widget/conversation、changelog feed、empty states、billing overview、live checkout。
* **App shell and settings（21）**：workspace sidebar、sidebar rail、settings page/command、docs sidebar、
  inbox sidebar/triage、command palette、notification center、selection toolbar、file upload、OTP、
  multi-step form、signup、sign-in、login centered/immersive/split、security settings、search results、
  media player。
* **Team and collaboration（16）**：team directory/showcase/members、testimonial stage、invite people、
  availability picker、project board、kanban、week calendar、API keys、webhooks、roles & permissions、
  env variables、release readiness、onboarding illustration、cohort retention。
* **Data and charts（20）**：metrics dashboard、revenue explorer、journey flow、usage forecast、
  MRR waterfall、metric explorer、activity terrain、revenue globe、customer galaxy、semantic zoom、
  layout morph、budget variance、choropleth explorer、small multiples、connected scatter、horizon chart、
  line replay、exploded view、KPI drilldown、market terminal。
* **AI（18）**：ai composer/chat/side panel、research run、ai answer、model picker/arena、prompt studio、
  document extract、agent memory/tasks/approvals/routines/orchestrator、MCP servers、knowledge base、
  eval suite、agent run。

## 5. 范围与取舍

* **目录已全覆盖**：Arc 站点当前的 294 个目录项（128 个常规组件 + 55 个 Special components +
  111 个 Blocks）在本仓库均有 Avalonia 实现；每项都有与站点一致的标题、一句话描述、tier、分类与
  “When to use / Keyboard and accessibility / Motion / Implementation notes” 文档结构。
* **Pro 项独立实现**：不抓取、不参考 `/r/pro/**` 或 Pro 源码；只依据公开组件页、公开 markdown 与
  视觉描述，在 Avalonia 中重写，并在每个页面的 Implementation notes 中列出近似点。
* **无外部商业 UI 组件库**：整个解决方案只引用 Avalonia / Avalonia.Desktop / Avalonia.Themes.Fluent /
  Avalonia.Fonts.Inter（均为 MIT）；图表、网格、编辑器、插图全部自绘或由基础控件组合。
* **保真度分层**：常规组件与 Pro 组件按视觉/交互规则对齐；Special components 是独立实现的动画/插图，
  用 2D 绘制、分层透明材质、弹簧与定时器近似原站的 CSS 3D、backdrop-filter、WebGL 与流体效果；
  Blocks 复用 128 个组件搭建真实产品界面，数据为代码内确定的示例，交互可操作但省略网络、路由与
  后端行为。具体偏差记录在每页的 Implementation notes 与代码注释中。
* **字体替换**：Arc 的 display 字体 Geist 离线不可得，display 与 body 统一使用内置 Inter，并在文档与
  README 中注明。字距、行高按 Arc 的 tracking/leading 规则换算为像素值。
* **平台原语**：Radix 菜单/对话框语义、Motion 共享布局动画由 Avalonia Popup/Transitions 与采样弹簧近似；
  桌面端无 pinch/陀螺仪（用滚轮/按钮/键盘/指针倾斜替代）；声音、摄像头、网络均为模拟。

## 6. 验收流程（离屏截图）

设计审查不依赖可见桌面，全部通过 PNG 进行：

```powershell
pwsh -NoProfile -File tools/build.ps1              # 单节点构建（本机并行 MSBuild 不稳定）
pwsh -NoProfile -File tools/shots.ps1              # 所有页面，明暗两套 -> shots/light、shots/dark
pwsh -NoProfile -File tools/shots.ps1 -Page inputs -Theme dark
dotnet src/Apps/Arc.Avalonia.Gallery/bin/Debug/net10.0/Arc.Avalonia.Gallery.dll --audit   # 294/294 可达性审计
```

`ShotRunner` 在真实（离屏）窗口中构建每个页面，让样式与资源正常解析，再用 Skia 渲染为自然高度的
`RenderTargetBitmap`。由于窗口高度受屏幕工作区限制，展示页拆分为多个 `ArcPage`，而不是让截图滚动。
`ArcItemPage` 页面统一由 `ArcDocLayout` 输出与站点一致的文档结构（面包屑、tier、描述、When to use、
Preview、Keyboard and accessibility、Motion、Implementation notes、公开文档链接）。

在验收过程中发现并修复的典型问题：

* 截图工具的页面背景在切换暗色主题后仍是浅色的快照（改为每个主题重新解析 `ArcBackground`）。
* 图标数据中有多路径 `d` 拼接错误（例如 `x` 的第二个子路径缺少绝对起点），导致清除按钮渲染成折线；
  重新按 Lucide 原始 SVG 修正了 `x`、`code`、`chart-line`、`expand`、`pencil`、`move`、`eye-off`、
  `user`、`users`、`maximize`、`minimize` 等图标。
* 在 `RenderTargetBitmap` 中直接渲染 `ScrollViewer` 子树会挂起，改为渲染页面内容本身并限制页面高度。





