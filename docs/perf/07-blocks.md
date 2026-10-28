# Blocks：页面级组合的组件设计与性能分析

## 0. 范围与覆盖

**覆盖工程与实际文件数**（排除 `obj/` 后的真实源码；简报的 161 含 6 个工程各 3 个 obj 生成文件）：

| 工程 | 非 obj `.cs` | 页面 | 其它（Kit / 自绘控件 / 数据） | Kit 行数 | Kit public helper |
| --- | --- | --- | --- | --- | --- |
| `Arc.Avalonia.BlocksMarketing` | 23 | 18 | `MarketingKit` + `ArcMarquee`/`ArcMeshWash`/`ArcSpotlightCard`/`ArcStatNumber` | 528 | 38 |
| `Arc.Avalonia.BlocksCommerce` | 20 | 18 | `CommerceKit` + `ArcEmptyScene` | 933 | 54 |
| `Arc.Avalonia.BlocksApp` | 23 | 21 | `AppKit` + `OtpField` | 1035 | 64 |
| `Arc.Avalonia.BlocksTeam` | 20 | 16 | `TeamKit` + `ArcCohortChart`/`ArcMorphIllustration`/`TeamData` | 743 | 44 |
| `Arc.Avalonia.BlocksData` | 38 | 20 | `DataKit`/`DataDraw`/`DataSamples` + **15 个 `DataStage` 自绘控件** | 677 | 50 |
| `Arc.Avalonia.BlocksAI` | 19 | 18 | `AiKit` | 1014 | 50 |
| **合计** | **143** | **111** | 23 个非 Kit 控件文件（20 个含 `Render`） | **4930** | **300** |

**完整精读**：`DataKit.cs`、`DataDraw.cs`、`DataSamples.cs`、`ArcTerrainStage.cs`、`ArcGalaxyStage.cs`、`ArcChoroplethMap.cs`、`ArcLayoutMorph.cs`、`ArcDotGlobe.cs`、`ArcHorizonChart.cs`、`ArcSmallMultiples.cs`（含 Render 段）、`ArcCandleChart.cs`（Render 段）、`ArcWaterfallBars.cs`（Render 段）、`ArcReplayLine.cs`（Render 段）、`ArcOverviewBrush.cs`（Render 段）、`ArcMarquee.cs`、`ArcMeshWash.cs`、`ArcEmptyScene.cs`、`ArcStatNumber.cs`（计时段）、`ArcSpotlightCard.cs`、`InboxSidebarPage.cs`、`MetricsDashboardPage.cs`、`AppKit.cs`/`AiKit.cs`（核心段）、`ArcItemPage.cs`、`ArcDocLayout.cs`、`GalleryWindow.cs`（外壳/换肤段）。

**grep 扫描**：全部 143 个文件的 `new Arc*` / `new (StackPanel|Grid|Border|TextBlock|...)` / `DispatcherTimer` / `InvalidateVisual` / `override void Render` / `ArcResources.` / LINQ / `CornerRadius|Thickness|Width|Height` 字面量 / `AutomationProperties` / `ReducedMotion` / `ScrollViewer`；以及 6 个 Kit 的 helper 签名交叉对比。

**方法**：只读静态分析 + 代码路径推理。**未运行构建、未跑截图、未修改 `src/**`**；凡"每帧/每次"的数字均为"代码路径 × 数据规模 × 事件率"推算，未在 profiler 下验证的标注「推算」。口径说明：简报的 BlocksData「8 处 timer」是按字面量计（每个 timer 2 行：声明 + `new`），实际是 **4 个** timer；「15 Render / 72 InvalidateVisual」已逐一核对成立（另有 1 个页面内嵌自绘控件 `ProgressTrack`，见 §1.1 / BP-08）。

## 1. 组件设计特征

### 1.1 组合结构：111 个页面 = 111 个 `ArcItemPage` + 6 套工程内 Kit

- 111 个块**全部**是 `ArcItemPage`（`ArcItemPage.cs:12`），统一走 `ArcDocLayout.Build` 的文档骨架，且全部声明 `DemoOnCanvas => false`（111/111）：块自己就是整屏产品界面，不需要演示画布留白。这是本区域最正确的一个决定——文档结构（何时用/可访问性/动效说明/实现备注）不在 111 个文件里各写一遍。
- 页面的组合方式基本是三种：①**容器换内容**（`ContentControl` + 重建某个 `Host`，如 `MetricsDashboardPage.cs:102-107`）；②**整体重建**（任一状态变化把三栏全树重造，`InboxSidebarPage.cs:562-568`）；③**页面内嵌自绘控件**（`FeatureBentoPage.cs:271-298` 的 `ProgressTrack`、Data 的 15 个 stage）。
- 页面文件的"直接 `new` 控件"数量并不高（每页 20–50 个），因为绝大多数节点由 Kit helper 展开：6 个工程共 **4,886 次 Kit 调用**（App 1309、Commerce 981、AI 791、Data 758、Team 600、Marketing 447），平均每页 24.8（Marketing）到 62.3（App）。按 Kit 展开 + 循环估算，一个代表性块页面构建 **约 120–220 个视觉节点**（见 §1.6 抽样表）。
- 对 128 个基础组件的复用**不均衡**：`ArcIcon` 157 次、`ArcButton` 47 次、`ArcAvatar/ArcBadge/ArcSwitch/ArcInput/ArcSegmentedControl` 各若干；但 `ArcCard` **0 次**——块用 Kit 的 `Border` 包装器自造卡片（`DataKit.Card:263-273`、`AiKit.Panel:225-236`）。手写结构节点的数量远大于复用控件：`Border` 493、`StackPanel` 709、`Grid` 415、`TextBlock` 256。
- 23 个非 Kit 控件文件里，**20 个** `override Render`（BlocksData 15、Marketing 2、Team 2、Commerce 1），App/AI 完全没有自绘控件——App 用 `OtpField`（组合控件）、AI 纯 Kit 组合。20 个自绘控件中**只有 `ArcCohortChart` 用了 `AffectsRender`**（`ArcCohortChart.cs:58-61`），其余 19 个（含 15 个 DataStage 子类）都是"普通字段 + 手工 `InvalidateVisual`"。

### 1.2 Kit：真正的复用，但复用的是"每个工程自己那一套"

- 6 个 Kit 共 4,930 行 / 300 个 public static helper，其中 **30 个 helper 名在 ≥4 个 Kit 中重复**：`Text/Caption/Label/Body/Heading/Stack/Row/Columns/Divider/Panel/Surface/Btn/IconBtn/Pill/Chip/Meta/Segmented/Select/Toggle/Brush/Tint/...`。`AppKit`(1035 行) 与 `DataKit`(677 行) 的 `Text`/`Stack`/`Row`/`Body`/`Heading` 几乎逐字相同（`AppKit.cs:45-113` vs `DataKit.cs:108-185`）。
- 基础层已经有 `DemoUi`（`DemoUi.cs:15-127`，含 Section/Row/Column/Card/Canvas/Field/Brush），但 **111 个块页面里 `DemoUi.` 调用为 0**：每个工程宁可复制一份"更像产品 UI"的 Kit。Kit 之间还存在 API 漂移：`DataKit.Heading` 默认 class 是 `arc-heading`（`DataKit.cs:136`），`AiKit.Heading` 默认 `arc-subtitle`（`AiKit.cs:79`）；`AppKit.Screen` 默认固定宽 1000（`AppKit.cs:192`），`AiKit.Surface` 自适应（`AiKit.cs:210`）。同样是"块标题"，6 个工程的排版类不同。
- Kit 包装 Arc 控件时有**节点通胀**：`AiKit.Btn`（`AiKit.cs:278-326`）把 label 做成 `StackPanel + TextBlock (+ArcIcon)`，并按 variant 自己算图标色，而不是给 `ArcButton.Content` 传字符串/让 `ArcButton` 自己处理前景色。47 次 `ArcButton` 实例化因此多出上百个子节点，也绕过了基础控件的配色逻辑。
- 做得好的地方：Kit 把"块内不该出现的颜色"收敛得很干净（见 §1.3），把数字格式化（`Money/Compact/Signed/Percent`）、确定性随机数（`DataKit.Random:665-676`）、状态播报 live region（`DataKit.Status:601-608`、`AppKit.cs:694,901`）都做成了共用件；`TeamData.cs` 甚至让 16 个团队块共用同一组人物示例数据（避免每页换名字）。

### 1.3 token、尺寸与主题一致性

- **颜色 100% 走 token**：6 个工程 `#[0-9A-Fa-f]{6}` 硬编码 **0 处**，唯一直接用的是 `Brushes.Transparent`（AI 的 10 处透明占位）。`ArcResources.Brush/Color` 是唯一取色入口。
- **半径/间距/字号大量是字面量**：6 个工程 `Radius(ArcTokens.*)` 调用 **0 次**，而 `new CornerRadius(<数字>)` **242 处**；`new Thickness(<数字>…)` **625 处**；`FontSize = <数字>` **33 处**；`Width/Height = <数字>` **579 处**。也就是说"26/18/14"这些与 `ArcRadiusPanel/Control` 同值的数字是**碰巧一致**的约定，不是 token 引用；将来改 token 不会传播到 111 个块。
- **宽度策略有三套**：App/Commerce/Data 的页面外壳固定 `Width = 1000`（`AppKit.cs:192`、`CommerceKit.cs:214`、`DataKit.cs:250`）；Marketing/Team 的 `Surface` 自适应（`MarketingKit.cs:166`、`TeamKit.cs:164`）；AI 自适应但额外有 24 处 Min/MaxWidth 约束。Gallery 内容区在 1440 窗口下是 1440−300(侧栏)−112(内边距) = 1028 px（`GalleryWindow.cs:75,91`），1000 刚好放下；但窗口到 `MinWidth = 1024` 时内容区只有 612 px，而 `HorizontalScrollBarVisibility = Disabled`（`GalleryWindow.cs:88`）⇒ 固定 1000 的外壳会被裁切。`ArcCohortChart.MeasureOverride` 更是直接返回 `new(860, 316)`（`ArcCohortChart.cs:91`），完全忽略可用宽度。
- **换肤**：块在 `Build` 时把 brush 取成快照（`DataKit.Brush` → `ArcResources.Brush`），换肤靠 Gallery 丢弃并重建页面来补偿（`GalleryWindow.cs:488-502` 清空 `_builtContent/_builtViews`）。自绘控件因为在 `Render` 里重新取 token，反而对换肤天然正确（代价见 BD-04 的 R2）。这个"静态页面靠重建、自绘控件靠重查"的双轨是 §4 的结构性问题之一。

### 1.4 动效与 timer（全 Blocks 共 17 个 `DispatcherTimer` 创建点 + 2 个 `RunOnce`）

| 位置 | 间隔 | 用途 | 是否随页面构建启动 | reduced-motion |
| --- | --- | --- | --- | --- |
| `BlocksMarketing/Controls/ArcMarquee.cs:57` | 16 ms | logo 跑马灯，改 `TranslateTransform.X`（不触发布局） | 附加视觉树时启动（`:60`） | 有（`:82`、`:109`） |
| `BlocksMarketing/Controls/ArcMeshWash.cs:39` | 48 ms | 网格渐变drift，每 tick `InvalidateVisual` | 仅 `Drift=true` 且附加时（`:80`） | 有（`:42`、`:122`） |
| `BlocksMarketing/Controls/ArcStatNumber.cs:108` | 16 ms | 数字滚动到目标值，到点自停 | 首次 `Value` 变化（非构建） | 有（`:97`） |
| `BlocksCommerce/Controls/ArcEmptyScene.cs:110` | 16 ms | 空状态图形形变，17 帧后自停 | 场景属性变化时（非构建） | 有（`:101`） |
| `BlocksCommerce/Pages`（6 处） | 700–1300 ms | 假异步：加购/支付/保存/客服回复，一次性 `Stop` | 点击回调内 | 不适用 |
| `BlocksApp/Pages/MediaPlayerPage.cs:88` | 500 ms | 播放进度推进 | **构建时就 `Start()`**（`:432`） | 无（模拟播放，非动效） |
| `BlocksApp/Pages/FileUploadPage.cs:96` | 160 ms | 上传进度 +8%/tick | Retry / Add 回调 | 不适用 |
| `BlocksApp/Pages/SignupFormPage.cs:84,177` | `RunOnce` | 焦点/确认延迟 | 提交后 | 不适用 |
| `BlocksTeam/Controls/ArcMorphIllustration.cs:90` | 15 ms | 场景形变 | 切换场景时（`:123`） | 有（`:114`） |
| `BlocksData/Controls/ArcLayoutMorph.cs:40` | 16 ms | 6 种布局插值，22 帧后自停 | 布局属性变化时（`:84`） | 有（`:76-80`） |
| `BlocksData/Pages`（3 处） | 120/700/900 ms | 回放/播放月份/时区 | Play 按钮 | 2/3 有（`ConnectedScatterPage.cs:154`、`LineReplayPage.cs:155`）；`RevenueGlobePage` 的 900 ms 回放**不读该开关** |

合计：Marketing 3 + Commerce 7（6 页面一次性 + EmptyScene）+ App 2 + Team 1 + Data 4 = **17 个创建点**（另 2 个 `RunOnce`）。

- 回答"8 处 timer"：`BlocksData` 的 8 处 `DispatcherTimer` 字面量 = **4 个 timer**（`ArcLayoutMorph:40`、`ConnectedScatterPage:137`、`LineReplayPage:142`、`RevenueGlobePage:190`），**没有一个在页面构建时启动**——都是用户点 Play 或切布局后才建/启动，且到终态自停或可暂停。这是本区域相对克制的部分。
- 真正"随加载即启动"的只有两处：`ArcMarquee`（跑马灯页，附加即跑，但只改 render transform）与 `MediaPlayerPage`（构建即 `timer.Start()`，见 BP-02）。
- **reduced-motion 覆盖是本区域做得最好的横向能力**：17 个 timer 中属于动画/播放型的 9 个，有 8 个在启动和 tick 里读了 `ArcThemeManager.ReducedMotion`（`ArcMarquee:82,109`、`ArcMeshWash:42,80,122`、`ArcStatNumber:97`、`ArcEmptyScene:101`、`ArcMorphIllustration:114`、`ArcLayoutMorph:76`、`ConnectedScatterPage:154`、`LineReplayPage:155`；缺的一个是 `RevenueGlobePage` 的 900 ms 回放，见 P2），且 `DataStage` 的默认输出就是静态帧（`DataDraw.cs:171-175` 注释）。全 Blocks 只有 2 个页面用 `Transitions`（`NewsletterSignupPage.cs:92`、`StatsBandPage.cs:152`），都带门禁；没有 `Animation`/`KeyFrame`。
- 但 timer 的**生命周期**处理不均：`ArcMarquee`/`ArcMeshWash`/`ArcStatNumber` 有 detach 停表，`MediaPlayerPage` 有（`:483`）；而 Commerce 的 6 个页面级一次性 timer、`ArcLayoutMorph`、`ArcMeshWash` 的实例化 timer 都没有"离开视觉树就停"的统一保障。

### 1.5 可访问性与交互语义

- 15 个 `DataStage` 全部 `Focusable = true`，支持方向键/Home/End/Escape 检查数据（`DataDraw.cs:176-182`；`ArcTerrainStage.cs:104-184`、`ArcGalaxyStage.cs:203-250`、`ArcChoroplethMap.cs:150-192` 等），tooltip 是自绘文本而非 Popup——键盘可用但屏幕阅读器读不到（无 Automation 文本）。
- 状态反馈统一用"礼貌 live region"：`DataKit.Status`（`:601-608`）与 `AppKit.Status`（`:694`）设置 `AutomationLiveSetting.Polite`，每个块的每个动作都会 `Say(...)`，这是很一致的好习惯。
- `AutomationProperties` 分布很不均：**AI 39 处 `SetName`**（`AiKit.cs:324`、`K.IconBtn` 等几乎每个按钮都补了可访问名），Commerce/Data/Team/Marketing 合计 21 处，**App 只有 1 处**（另 3 处 LiveSetting）。页面级自造交互（如 `KpiTile` 被做成 tab 语义）需要手工补 `SetName`（`MetricsDashboardPage.cs:350`），换个工程就没人补。

### 1.6 示例数据：确定性、便宜，但单页规模差异极大

- 确定性：`DataKit.Random` 是自实现的 mulberry32，固定种子（`DataKit.cs:665-676`）；`DataSamples.Daily/Monthly` 用 seed 生成趋势+周节律（`DataSamples.cs:24-56`）。截图与键盘走查在任意机器结果一致——这是块示例数据最正确的做法。
- 代表性规模（均为 `BuildDemo` 内一次性生成）：

| 页面 | 数据对象 | 说明 |
| --- | --- | --- |
| `ActivityTerrainPage` | 7×24 = 168 格 | `ArcTerrainStage`，逐格 3 个多边形 |
| `CustomerGalaxyPage` | **2400** 个账户 | `:64` `BuildAccounts(2400)`；但控件注释仍写 "1,200 of them stay cheap"（`ArcGalaxyStage.cs:10`），页面注释写 "about 1,200"（`:13`）——**注释与实际规模不一致，实际翻倍** |
| `MarketTerminalPage` | 120 根蜡烛/标的 | `:301-309` |
| `LineReplayPage` | 144 点 + 4 事件 | `:57-70` |
| `SmallMultiplesPage` | 12 面板 × 24 天 | `:54-63` |
| `HorizonChartPage` | 14 市场 × 60 天 | `:60-71` |
| `RevenueGlobePage` | 1620 点阵 + 12 城市 | 点阵由纬度/经度步长 6° 决定（`ArcDotGlobe.cs:206-208`） |
| `ChoroplethExplorerPage` | 每国 4–9 个区域 | `:50` 实现备注 |
| `ConnectedScatterPage` | 3 通道 × 18 月 | `:51` |
| `MrrWaterfallPage` | 7 根瀑布柱 | start + 5 项变动 + end（`:56-79,103-116`） |

- 抽样估算（控件节点 / 数据对象）：`MetricsDashboardPage`（369 行）页面内直接 `new` 20 个控件 + 59 次 `DataKit.` 调用 → 展开约 150–180 节点，数据为 4×180 天序列；`InboxSidebarPage`（633 行）50 个直接 + 101 次 `AppKit.` 调用 → **每次 `Build()` 约 150–200 节点**，数据 8 封邮件 + 6 个文件夹；`EnvVariablesPage`（637 行）~30 + 73 次 → 约 180 节点、5 变量×3 环境；`McpServersPage`（351 行）20 + 43 次 → 约 120 节点、3 server/8 tool。**结论：块页面的节点量级在 120–220，属于"整屏一次构建可接受，但做局部状态更新时全部重建就偏贵"**。

### 1.7 布局深度与滚动（直接回答"嵌套层数 / 超大内容 / 固定宽度"）

- **代表性嵌套 8–10 层**：`MetricsDashboardPage` = `Border(Specimen):311` > `StackPanel:311` > `Grid:294` > `Border(Card):288` > `StackPanel:288` > `ContentControl:105` > 图表/行；单个 KPI tile 另有 `Grid > Border:340 > StackPanel:348 > StackPanel:321 > 3×TextBlock` 5 层。`InboxSidebarPage` 的一行邮件是 `Border(Screen):631 > Grid:98 > Border:570 > ContentControl > Grid:391 > StackPanel:297 > Border(Pressable):311 > Grid:317 > StackPanel:324 > Grid:329 > TextBlock` ≈ 10 层。层数本身不算病，但每层都是新的 `Panel`/`Border`，与"整树重建"组合后构建成本直接翻倍。
- **零虚拟化**：全 Blocks `new ListBox` / `new ItemsControl` / `new DataGrid` / `ItemsRepeater` / `Virtualiz*` 均为 **0**。列表一律用 `StackPanel`/`Grid` 循环 `new`（Inbox 8 行、`WeekCalendarPage` 7 天 × 时段格、`SearchResultsPage` 的 matches、`EnvVariablesPage` 5 变量×3 环境）。当前示例规模都在十位数以内，问题被数据量掩盖；但这批块的定位是"可直接迁移的产品界面示例"，模式被复制到真实行数就是线性膨胀（L2）。
- **滚动宿主**：9 个 `ScrollViewer`（8 个页面 + `AiKit.Scroller:264`，后者用固定 `height` 把内容限制住）；`InboxSidebarPage.cs:244` 还把列表滚动条 `Disabled`，与 Gallery 页面级 `ScrollViewer`（`GalleryWindow.cs:86-90`）形成嵌套滚动。块本身不负责页面滚动，这是对的；但"ScrollViewer 里放整棵非虚拟化大内容"的隐患已经在 Inbox / SearchResults / WeekCalendar 三个页面成型。
- **固定宽度会带来额外测量轮次（suspected）**：块里有 579 处 `Width/Height = 数字`，固定外壳 1000、`ArcCohortChart` 860×316，且常与星号列混用（`MetricsDashboardPage.cs:296` 的 `"*,12,286"`）。固定测量值不受 `availableSize` 影响，首帧/窗口缩放时需要一轮额外的 arrange 才能确定最终列宽；由于 Gallery 初始窗口下内容区 ≤1028 且不横向滚动，这些固定值目前"刚好不炸"，但它们是 BP-06 裁切风险的直接来源。

### 1.8 BlocksData 的 15 个 `Render`：谁在重建什么（直接回答"是否逐点重建"）

| 控件 | 数据规模 | 每次 Render 重建 | 主要失效来源 |
| --- | --- | --- | --- |
| `ArcTerrainStage` | 168 格 | 168×3 多边形（504 `StreamGeometry`/`Point[]`）+ 每柱 4 brush | hover 换格、点选、Shift 轨道、缩放、Flattened |
| `ArcGalaxyStage` | **2400** 账户 | `Layout()`+`Clusters()` LINQ + 2400 ellipse（每点新 brush） | hover、**拖拽每 move**、分组 |
| `ArcChoroplethMap` | ≤9 tile | `Layout()`、每 tile 2–4 次 `SortedValues` 排序 + Fill/Pen | hover、选择、缩放、页面手动 `Invalidate` |
| `ArcDotGlobe` | 1620 点 + 12 城市 | 1620 `Ink.Alpha` 新 brush + 每城 1 个 `StreamGeometry` + Clip | hover、**拖拽每 move**、900 ms 回放 |
| `ArcHorizonChart` | 14×60 | `Ordered()` 排序 + 14 行×3 band×2 `List<Point>` + 84 个几何 | crosshair、排序、band 数 |
| `ArcSmallMultiples` | 12×24 | `Panels()` + 12×（ghost list + 折线 list + 面积 + 折线） | crosshair、排序 |
| `ArcSemanticMap` | 每层 ≤9 | `Tiles()` + 每 tile 2–3 个 `FormattedText` | 下钻、hover、键盘 |
| `ArcCandleChart` | 120 蜡烛 | 每根 1 `Pen` + 2 个 rect + brush；MA5/MA20；约 15 文本 | crosshair 每变化（指针） |
| `ArcWaterfallBars` | 7 柱 | `levels` List + min/max 扫描 + 每柱 2–3 brush | 选择、hover |
| `ArcConnectedScatter` | 3×18 | `Select(Map).ToList()` + 折线/虚线/健康区几何 + 逐点文本 | head/crosshair |
| `ArcReplayLine` | 144 点 + 4 事件 | 3 个几何 + 4 事件标注 + 头标签 | 播放 head |
| `ArcForecastCone` | 30 天（18 实测 + 12 预测） | cone/area/实测线几何 + 网格/文本（Render 157 行，最长） | 滑块、hover |
| `ArcOverviewBrush` | 24 月 | Min/Max + 24 点 List + 3 几何 | 窗口拖拽 |
| `ArcExplodedLayers` | 5 层 | 每层 1 个 `StreamGeometry`（`:169`） | 分离滑块、选择 |
| `ArcLayoutMorph` | 24 卡 | 每帧 24 卡矩形 + 2 文本/卡（形变 22 帧） | 布局切换（16 ms timer） |

- 除 `ArcLayoutMorph` 缓存了布局目标矩形（仅尺寸/布局变化时 `Compute`）外，其余 14 个控件都在绘制时现算几何与文本，**没有任何"几何/文本缓存"容器**（现有的 `static readonly` 只是标题/颜色常量数组）。逐点/逐格绘制是常态：地形 168 格、地球 1620 点、银河 2400 点、蜡烛 120 根、horizon 84 个 band 几何。
- 72 次 `InvalidateVisual` 中，**67 次在控件内的交互 handler**，5 次在页面里（`ChoroplethExplorerPage:278`、`HorizonChartPage:102`、`ExplodedViewPage:88,106`、`SmallMultiplesPage:88`）。**没有一个控件在"每帧无条件重绘"**：唯一的 16 ms 自驱路径是 `ArcLayoutMorph` 的形变（22 帧）和 `ArcEmptyScene` 的形变（17 帧）。所以 Blocks 的绘制负载形态不是"60 fps 常驻"，而是"**交互事件 → 一次全量重建**"，这也是 §2 里 P0/P1 的判定依据。

## 2. 性能问题清单

| ID | 分类 | 严重度 | 位置 | 问题一句话 | 放大因子 |
| --- | --- | --- | --- | --- | --- |
| BD-01 | R1/R2/R3 | P1 | `BlocksData/Controls/ArcTerrainStage.cs:278-319,385-397` | 每次重绘 168 柱 × 3 个 `StreamGeometry`+`Point[]`，每柱 4 个 brush、2 次资源查找 | × 168 格 × 每次失效（hover 换格/键盘/拖拽） |
| BD-02 | R1/R2/R3/P1 | **P0** | `BlocksData/Controls/ArcGalaxyStage.cs:98-132,147,264-297` + `CustomerGalaxyPage.cs:64` | 2400 账户；每次指针移动都 `Layout()`（2400 次分组 LINQ），拖拽时每次移动整图重绘 2400 个 ellipse（每个 1–2 次 `Ink.*` 资源查找 + 新 brush） | × 2400 点 × 指针事件率（60–120/s） |
| BD-03 | R1/R2/R3/P1 | **P0** | `BlocksData/Controls/ArcDotGlobe.cs:206-263` | 1620 个点阵点每点 `Ink.Alpha(Ink.Secondary,…)`（=1 次字典查找 + 1 个 `SolidColorBrush`）+ 每城市 1 个 `StreamGeometry`；拖拽时每次移动全量重绘 | × 1620 点 × 拖拽事件率 |
| BD-04 | R2/S2 | P1 | `BlocksData/Controls/DataDraw.cs:14-46,77-136,207-251` | 绘制助手零缓存：`Ink.*` 每属性一次 `ArcResources.Brush`；`DataText` 每次 `new FormattedText` + `new Typeface` + `TryFindResource(FontBody)` | × 139 处 `Ink.<prop>` / 59 处文本绘制点 × 每次重绘 |
| BD-05 | R1/R3 | P1 | `ArcHorizonChart.cs:52-57,73,158-197`；`ArcSmallMultiples.cs:55-63,89,161-213` | 每 band/panel 重建 `List<Point>` + 面积/折线几何；`Ordered()/Panels()` 排序在 `OnPointerMoved` 与 `Render` 各跑一次 | × 14 行 × 3 bands；× 12 面板 × 指针事件 |
| BD-06 | R1/R3/S2 | P1 | `ArcCandleChart.cs:160-243` + `MarketTerminalPage.cs:136-154` | 120 根蜡烛逐根 `new Pen`/新 brush；crosshair 每次变化整图重绘；页面在 `Inspected` 里每次重建 6 个报价控件 | × 120 根 × 指针事件率 |
| BD-07 | L2/R1 | P1 | `ConnectedScatterPage.cs:184-189,75-106` | `HeadChanged`（指针每移动一次就可能触发）重建 4 个统计 tile（约 25 控件）**并重建整个 `ArcSlider`** | × 指针事件率 × 约 25 控件 |
| BD-08 | R2/R3 | P2 | `ArcChoroplethMap.cs:86-100,204-228` | `Step()` 每次调用都 `OrderBy+ToList` 排序全部值，每区域调 2–4 次 | × region² ；n≤9，当前量级小 |
| BD-09 | T1/R1 | P2 | `ArcLayoutMorph.cs:37-56,270-319` | 形变 22 帧内每帧重绘 24 卡，每卡 2 个文本（≈48 `FormattedText`/帧）；timer 每实例新建且不 Dispose | × 24 卡 × 22 帧，reduced-motion 已跳过 |
| BD-10 | R2/S2 | P2 | `BlocksTeam/Controls/ArcCohortChart.cs:136-201` | 每个单元格循环内做 5+ 次 `ArcResources.*` 查找 + `Contrast/Blend` + 每格 `new Pen`；hover 每次移动重绘 | × 8×8 格 × 指针事件率 |
| BD-11 | L1 | P2 | `ArcCohortChart.cs:91`；`DataDraw.cs:192-197` | 自绘控件用固定 `MeasureOverride`（860×316）或"无限宽就 320"，不响应可用宽度 | 窗口 <860 时溢出/裁切 |
| BD-12 | T1 | P2 | `RevenueGlobePage.cs:88,190-206` | 900 ms 回放 timer 不读 `ReducedMotion`，与其他 8 个动画/播放 timer 不一致 | × 播放时长 |
| BP-01 | L2/R1 | P1 | `InboxSidebarPage.cs:562-568,264-396` | 任一交互（点文件夹/星标/打开/换折叠）都 `Build()` 重造三栏全树（约 150–200 控件），列表是 `StackPanel` 逐行 new（无虚拟化） | × 每次交互 × 全树；邮件数增长时线性 |
| BP-02 | T1/R1/L1 | P1 | `MediaPlayerPage.cs:88,90-167,420-432,483` | 500 ms timer 随**页面构建**启动；每 tick 重建 seek 子树（含 `new ColumnDefinitions($"{f}*,…")` 字符串 + `new Cursor`）；拖动时在 `PointerMoved` 里重建正在接收指针的控件 | × 2 次/s（空闲）；× 指针事件率（拖动） |
| BP-03 | T1 | P2 | Commerce 6 处页面 timer；`ArcEmptyScene.cs:110` | "假异步" timer 每次点击 `new` 且不保证 detach 停表；页面在等待期被切走时仍在 tick | × 点击次数 |
| BP-04 | R1/S2 | P1 | `MetricsDashboardPage.cs:111-255,165` | 每次切 KPI/范围/Compare 全量 `Rebuild`；`DateLabels(180)` 被写进 30 元素的 `Select` 里，**每次 Rebuild 生成 30×180 = 5400 个 DateTime + 5400 个字符串**，另有成串 `Skip/Take/Sum` | × 30 × 180 × 每次交互 |
| BP-05 | L2 | P2 | `GalleryWindow.cs:488-502` + 各页 `Build` | 换肤清空所有缓存页面并重建当前页（120–220 节点 × 242 处硬编码半径/625 处硬编码间距，无 token 增量路径） | × 换肤次数 × 每页节点数 |
| BP-06 | L1 | P2 | `AppKit.cs:192`、`CommerceKit.cs:214`、`DataKit.cs:250` + `GalleryWindow.cs:86-91` | 固定 1000 px 外壳 + 内容区水平滚动禁用：窗口宽度 <1412 px 时开始裁切（`MinWidth=1024` 时内容区仅 612 px） | × 每个 App/Commerce/Data 页面 |
| BP-07 | M1 | P2 | `ArcEmptyScene.cs:84-88` | 在**实例构造器**里 `SceneProperty.Changed.AddClassHandler<ArcEmptyScene>(...)`：每 new 一个实例就给类注册一次同类 handler，属性变化时被重复调用 N 次 | × 实例数 |
| BP-08 | R1/R2 | P2 | `FeatureBentoPage.cs:271-298`；`ArcSpotlightCard.cs:158-170,196-200` | 页面内私有 `ProgressTrack : Control` 重复实现 `ArcProgress`（每 Render 3 次 `Mk.Brush`）；spotlight 卡每次指针移动 `InvalidateVisual` 并 `new RadialGradientBrush`（一次页面 6 张卡） | × 指针事件率 × 卡片数 |
| BK-01 | L2/结构性 | P1 | 6 个 `*Kit.cs`（4930 行 / 300 helper，30 个重名） | 6 套近乎重复的组合 helper 取代已有的 `DemoUi`；API 漂移（默认 class/宽度/圆角不同）；`Btn` 用 `StackPanel+TextBlock` 包装 ArcButton 造成每按钮 +2 节点 | × 6 工程 × 111 页；每次修复成本 ×6 |
| BK-02 | 一致性 | P2 | 全 Blocks：`Radius(ArcTokens.*)` 0 处 vs 242 处 `new CornerRadius(字面量)` | 半径/间距/字号是字面量而非 token，换 token 不传播 | × 111 页 |
| BK-03 | 可访问性 | P2 | `AiKit.cs:324` vs `AppKit`（App 仅 1 处 `SetName`） | 块级可访问名覆盖极不均衡；自绘 DataStage 无 Automation 文本 | × 20 个自绘控件 / 111 页 |
| BK-04 | 死能力 | P2 | `ArcMeshWash.cs:39,80` + `MarketingKit.cs:207-211` | `Drift` 动画能力存在但**没有任何调用点传 `drift: true`**，timer 仍按实例创建（不启动） | × 实例数 |

## 3. 逐条说明

### BD-02 银河图：2400 账户 + 指针移动全量重排（P0）

- 位置：`src/Arc.Avalonia.BlocksData/Pages/CustomerGalaxyPage.cs:64`；`src/Arc.Avalonia.BlocksData/Controls/ArcGalaxyStage.cs:98-132,147,264-297`
- 证据：
  ```csharp
  var accounts = BuildAccounts(2400);                          // CustomerGalaxyPage.cs:64
  protected override void OnPointerMoved(PointerEventArgs e)   // ArcGalaxyStage.cs:134
  { ... Layout(); ... }                                        // :147
  Layout();                                                    // Render 内再调一次（:267）
  var keys = _accounts.Select(a => a.GroupKey(_grouping)).Distinct().OrderBy(k => k).ToList(); // :98
  ```
- 触发/放大：①纯 hover：每次 pointer-move 走 `Layout()` → `Clusters()` 对 2400 个账户做 `Select/Distinct/OrderBy/ToList`，再 `ToDictionary` + 2400 次 `Point` 分配（`:110-132`）；②拖拽平移（`_dragging`）：每次 move 直接 `InvalidateVisual()`（`:143`）→ Render 再 `Layout()+Clusters()`，并画 2400 个 `DrawEllipse`，每个点都要 `Ink.Series/Ink.Danger/Ink.Muted`（`ArcResources.Brush`）取色、`Ink.Alpha` 新建 `SolidColorBrush`（`:285-296`）（推算：单帧 ≥2400 次资源查找 + ≥2400 个 brush 分配）。
- 影响：拖拽与分组切换是明显的掉帧点（未实测）。放大因子就是账户数 × 指针事件率，注释里的"1,200 就够便宜"已被实际 2400 突破。
- 修复建议：`Layout()` 结果缓存在"账户集/分组/pan/zoom/Bounds 变化"时才重算（实现 `AffectsRender` + 脏标记）；按 cluster 预计算颜色 brush（≤6 个）与 dim 变体；hover 命中用空间网格或只在 cluster 级判断；拖拽期间用 transform 平移已缓存几何，不重排。

### BD-03 点阵地球：1620 点每点一次资源查找 + 新 brush（P0）

- 位置：`src/Arc.Avalonia.BlocksData/Controls/ArcDotGlobe.cs:206-263`
- 证据：
  ```csharp
  for (var lat = -78.0; lat <= 78; lat += 6)          // 27 行
    for (var lon = -180.0; lon < 180; lon += 6) {     // 60 列 ⇒ 1620 点
        var (point, depth) = Project(lat, lon);
        context.DrawEllipse(Ink.Alpha(Ink.Secondary, alpha), null, point, 1.1, 1.1);
    }
  ```
- 触发/放大：拖拽（`OnPointerMoved:118`，拖拽分支 `:126` 每次 move `InvalidateVisual`）、hover 命中变化、以及 `RevenueGlobePage` 的 900 ms 回放 timer（`RevenueGlobePage.cs:190-201`，每 tick `Hour` 变化 → 重绘）。每次重绘 1620 ×（`Ink.Secondary` 字典查找 + `Ink.Alpha` 新 brush）+ 12 城市各 1 个 `StreamGeometry`（`:250-256`）+ `PushClip`（`:225`）。alpha 只需按 depth 量化——当前每点都是新对象。
- 影响：单次重绘上千次分配与上千次 `TryFindResource`，拖拽旋转地球时是连续负载。属"高频输入路径 + 数据量线性"的 P0 形态（推算）。
- 修复建议：把 alpha 量化成 6–8 档，预生成 6–8 个 brush 字段；点阵几何/位置缓存起来按 yaw 只做投影（或直接画到缓存的 `RenderTargetBitmap` 做旋转）；`Ink.*` 加缓存（同 BD-04）。

### BD-01 地形舞台：168 柱 × 3 几何 + 每柱 Mix（P1）

- 位置：`src/Arc.Avalonia.BlocksData/Controls/ArcTerrainStage.cs:278-319,354-383,385-397`
- 证据：
  ```csharp
  var fill = Mix(Ink.Alpha(Ink.Series(0), 0.35), Ink.Series(0), t);   // :294 每柱
  context.DrawGeometry(Ink.Alpha(fill, 0.55), null, Polygon(side));     // :316
  context.DrawGeometry(fill, ..., Polygon(top));                        // :318
  private static StreamGeometry Polygon(Point[] points) { var g = new StreamGeometry(); ... } // :385
  ```
- 触发/放大：每次 `Render`（hover 换格 `SetSelection:196-213`、点选、Shift+方向键轨道/缩放、`Flattened` 切换）。每次 = 168 柱 ×（3 个 `StreamGeometry` + 3 个 `Point[4]` + `Mix` 里 2 次 `Ink.Series` + `Ink.Alpha` + `new SolidColorBrush` + 两个 55%/45% 透明 brush）≈ **504 几何 / 672 brush / 336 次资源查找**（推算）。另有 `HitTest` 每次 pointer-move 调 `Lattice()`（`:230`），即 168 元组 List + 排序 + 168 次 `sqrt` 每移动一次；`Lattice()` 在 Render 里再算一遍（`:287`）并 `Sort`（`:274`）。
- 影响：不是持续 60 fps（只有失效时绘制），但 hover 走格/轨道旋转时每次失效都重建全部几何；`Flattened` 热力图路径同样每格 `Mix`（`:366`）。
- 修复建议：几何缓存（`Bounds`/`_values`/相机参数变化才重建）；`Mix` 结果按 t 量化到 8 档并缓存 brush；`Lattice()` 结果缓存给 `HitTest`（或改成反投影计算，不必建 List 排序）。

### BD-04 绘制助手无缓存：`Ink.*` 与 `FormattedText`（P1）

- 位置：`src/Arc.Avalonia.BlocksData/Controls/DataDraw.cs:14-46,77-136,207-251`
- 证据：
  ```csharp
  public static IBrush Foreground => ArcResources.Brush(ArcTokens.Foreground);   // :17，每访问一次查一次
  public static Pen Pen(IBrush brush, double thickness = 1, bool dashed = false) => new(...); // :41
  var formatted = new FormattedText(text, Culture, ..., new Typeface(Font, ...), size, brush); // :100
  public static FontFamily Font { get { ... app.TryFindResource(ArcTokens.FontBody, ...) } }  // :86-93
  ```
- 触发/放大：这是 15 个 DataStage 的公共底座。静态调用点：`Ink.<prop>` 139 处、`Ink.Pen(` 57 处、`Ink.Alpha(` 46 处、`Ink.Series(` 40 处、文本绘制 59 处；其中大量位于 per-item 循环内（如 BD-01 的每柱、BD-03 的每点、BD-10 的每格）。每次文本 = `TryFindResource(FontBody)` + `new Typeface` + `new FormattedText`（+`SetFontFeatures`），每次 `Ink.Pen` = `new Pen`，每次 `Alpha` = `new SolidColorBrush`。
- 影响：把"每帧分配"从几何层扩散到颜色/文本层，是整区域 R1+R2+S2 的公共乘数；同时也让换肤后的正确性只能靠每次重绘重查来保证。
- 修复建议：给 `Ink` 加"主题版本 + 静态缓存"（`ArcPalette` 版本号变化时清空）；`Pen` 按 `(brush,thickness,dashed)` 缓存；`DataText` 缓存 `Typeface` 与 `FontFamily`，短标签文本按 `(text,size,weight)` 缓存 `FormattedText`；与基础层 FO-04（`ArcResources` 无缓存）一并解决。

### BD-05 horizon / small multiples：每 band/panel 重建点列与几何（P1）

- 位置：`src/Arc.Avalonia.BlocksData/Controls/ArcHorizonChart.cs:52-57,73,158-197`；`src/Arc.Avalonia.BlocksData/Controls/ArcSmallMultiples.cs:55-63,89,161-213`
- 证据：
  ```csharp
  for (var band = 0; band < Bands; band++) {                     // HorizonChart:173
      var above = BandPoints(...); var below = BandPoints(...);  // 每次 new List<Point>(60)
      context.DrawGeometry(Ink.Alpha(Ink.Series(0), alpha), null, DataStage.AreaGeometry(above, mid));
  }
  private List<HorizonSeries> Ordered() => ... _series.OrderByDescending(...).ToList(); // :52-57
  ```
- 触发/放大：每次重绘（crosshair 每变化一次、排序/band 数切换）→ 14 行 × 3 bands × 2 个 `List<Point>`（每 list 60 点）+ 84 个 `AreaGeometry`；`ArcSmallMultiples` 12 面板 × （ghost 24 点 list + 折线 list + 面积几何 + 折线几何）。同时 `Ordered()`（`:73`）与 `Panels()`（`:89`）在 `OnPointerMoved` 里也各排序一次（14/12 项 LINQ）。
- 影响：中等规模但非常"碎"的分配（每次几百个 list/几何/brush），hover 读取同一天数据时反复分配；两页在 BlocksData 里是"拖动 crosshair"型交互，事件率高。
- 修复建议：按 `Bounds`/数据/排序缓存每个 band/panel 的 `Point[]` 与 `StreamGeometry`（`AffectsRender` 上挂排序与 band 数）；crosshair 变化只重画十字与 tooltip，不重建曲线；hover 命中用缓存的 `Ordered()` 列表。

### BD-06 蜡烛图 + 行情页：每根 new Pen 与每次指针重建报价（P1）

- 位置：`src/Arc.Avalonia.BlocksData/Controls/ArcCandleChart.cs:160-243`；`src/Arc.Avalonia.BlocksData/Pages/MarketTerminalPage.cs:136-154`
- 证据：
  ```csharp
  var brush = up ? Ink.Series(0) : Ink.Series(3);                    // CandleChart:191
  context.DrawLine(Ink.Pen(brush, 1), ...);                          // :192 每根 1 个 Pen
  context.DrawRectangle(Ink.Alpha(brush, 0.42), null, ...);          // :202 每根 1 个 brush
  chart.Inspected += (_, candle) => RefreshQuote(candle);            // MarketTerminalPage:152
  quoteHost.Content = DataKit.Row(14, ...6 个控件...);                // :140-146 每次指针移动重建
  ```
- 触发/放大：120 根蜡烛（`MarketTerminalPage:301`）× （1 次 `Ink.Series` 资源查找 + 1 个 `Pen` + 1 个 alpha brush + 1 个 volume rect）≈ 每帧 **120 Pen / 240 brush / 120 次资源查找**（再加 MA5/MA20 两条几何与 15 个标签）；crosshair 在 `OnPointerMoved` 每次变化即 `InvalidateVisual`（`ArcCandleChart.cs:92`）→ 整图重绘；行情页同时重建 6 个报价控件（含 2 次 `DataKit.Money` 格式化）。
- 影响：行情终端是典型"鼠标在图上移动"场景，指针事件率 ×（整图重绘 + 控件重建）叠加；120 根规模不大，但每次都是新对象。
- 修复建议：涨/跌两色与 volume 透明色预生成 4 个 brush、1 个 Pen；crosshair 只重画 `PushClip` 的覆盖层（或二次绘制层）；报价改为复用固定控件只改 `Text`。

### BD-07 散点页：指针驱动的 stats + slider 重建（P1）

- 位置：`src/Arc.Avalonia.BlocksData/Pages/ConnectedScatterPage.cs:184-189,75-106,108-127`
- 证据：
  ```csharp
  chart.HeadChanged += (_, index) => { head = index; RefreshStats(); RefreshSlider(); };  // :184-189
  var stats = new Grid { ... };  // 4 个 tile，每 tile Tile+StackPanel+3 TextBlock（:80-103）
  sliderHost.Content = DataKit.Slider(null, head, 0, Months.Length - 1, 1, ...);           // :110
  ```
- 触发/放大：`HeadChanged` 由图表内 crosshair/键盘 head 移动触发；鼠标在 250 px 高的散点图上横向移动会连续改变 head。每次 = 新建 4 个 `Border(Tile)+StackPanel+3 TextBlock`（约 20–28 控件）+ 整个 `ArcSlider` 重建并重新挂 value 回调 + 一次全图重绘（3×18 点路径）。× 指针事件率。
- 影响："拖动查看月份"会持续产生控件垃圾与布局/模板实例化；`RefreshSlider` 重建正在交互的 slider 也是交互隐患（拖动中被替换）。
- 修复建议：stats 的 4 个 tile 只创建一次，回调里改 4 个 `TextBlock.Text`；slider 只创建一次，用 `chart` 的 head 单向同步（避免回写触发重建）；`HeadChanged` 做 1 帧合并（`DispatcherTimer.RunOnce` 或 `CompositionTarget.Rendering` 一次）。

### BP-01 Inbox 侧栏：任何交互都重建三栏全树（P1）

- 位置：`src/Arc.Avalonia.BlocksApp/Pages/InboxSidebarPage.cs:562-568,264-396,244`
- 证据：
  ```csharp
  void Build() { BuildFolders(); BuildList(); BuildReader();  // :562-568 三个 Host.Content 全替换
                 shell.ColumnDefinitions[0].Width = new GridLength(collapsed ? 64 : 196); }
  foreach (var message in visible) { var row = new Border { ... }; rows.Children.Add(...); } // :307-389
  ```
- 触发/放大：点文件夹（6 个导航行）、点星标、打开邮件、归档、Compose、折叠侧栏、Send…**每一个动作**都重建三栏 = 约 150–200 个控件（文件夹行 6×NavRow、列表 8 行×约 12 节点、阅读区约 20）+ 每次 8 次 `messages.FirstOrDefault/Count` LINQ。列表是 `StackPanel` 直接放行，外加 `ScrollViewer`（`:244`，且 `VerticalScrollBarVisibility=Disabled`——只能滚不能拖）；行数增长时构建成本线性上升（当前 seed 只有 8 封，掩盖了问题）。
- 影响：交互响应里包含整棵子树构建 + 布局 + 焦点/滚动位置重置（`Build` 后滚动位置丢失，用户感知为"跳回顶部"）；这是 Blocks 里"组合质量"最典型的代价：状态变化没有做局部更新。
- 修复建议：把邮件行做成可复用控件（或 `ItemsControl` + `ItemsSource`），状态变化只重建受影响的行与阅读区；文件夹计数只更新 `TextBlock`；把构建动作包一层 `DispatcherTimer.RunOnce`/批处理以避免同一事件内多次 Build。

### BP-02 媒体播放页：构建即启动的 500 ms timer + 拖拽中重建自身（P1）

- 位置：`src/Arc.Avalonia.BlocksApp/Pages/MediaPlayerPage.cs:88,90-167,420-432,483`
- 证据：
  ```csharp
  var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };   // :88
  timer.Tick += (_, _) => { position += 0.5; ...; BuildSeek(); };                  // :420-431
  timer.Start();                                                                   // :432 构建时启动
  bar.AddHandler(PointerMovedEvent, (_, args) => { ...; BuildSeek(); });           // :123-131
  ```
- 触发/放大：页面一构建就每 500 ms 重建进度条子树：`seekHost.Content = new StackPanel{Border{bar}, Grid{2 Caption}}`（约 8 个控件）+ `new ColumnDefinitions($"{frac}*,…")` 解析（`:105`）+ `new Cursor`（`:100`）+ 2 次 `AppKit.Brush`（`:98,110`）。指针拖动进度条时，`PointerMoved` 里也调 `BuildSeek()`，**把正在接收指针的 `bar` 从树上换掉**，拖动行为与性能都不对。页面切走有 `DetachedFromVisualTree ⇒ timer.Stop()`（`:483`），这点做对了。
- 影响：空闲播放时 2 次/秒的控件分配 + 布局；拖动时按指针事件率重建控件（并且可能导致拖动中断）。属"页面加载即启动动画"的唯一实例。
- 修复建议：进度条只创建一次，用两列 `GridLength` 或 transform 更新（不要把小数拼进 XAML 字符串）；tick 里只改填充宽度/slider value；杆件回调改 `position` + 无效化，不在 pointer-move 里重建控件；播放默认不自动启动（或把自动播放做成显式 prop）。

### BP-04 指标仪表盘：`DateLabels(180)` 在 30 元素 Select 内被调 30 次（P1）

- 位置：`src/Arc.Avalonia.BlocksData/Pages/MetricsDashboardPage.cs:165,111-255`
- 证据：
  ```csharp
  Labels = window.Select((_, index) => DateLabels(180)[values.Length - rangeDays + index]).ToList(),  // :165
  // :72-78 每次调用：Enumerable.Range(0, days).Select(o => start.AddDays(o).ToString("MMM d")).ToArray()
  ```
- 触发/放大：`window` 是 30 天的 `ToArray()`，所以 `DateLabels(180)` 被调用 30 次 = **30×180 = 5400 个 `DateTime.AddDays` + 5400 个 `ToString` + 30 个临时 `string[180]`**，只为了取其中 30 个标签。`Rebuild()` 在切 KPI tab（4 个）、切范围（3 个）、切 Compare 时都会跑；`Rebuild` 里还有多处 `Skip/Take/Sum`（`:115-128`、`:140-141`）和 `metrics.First(...)`。
- 影响：每次交互一次性生成上万对象的垃圾（推算），在 90 天范围下 `window` 90 → 更糟（90×180）；UI 上表现为切换 KPI 的卡顿峰值，而非持续掉帧。
- 修复建议：`DateLabels` 在 `BuildDemo` 里算一次（180 个标签），`window.Select` 只做切片索引；或直接缓存 `string[] labels = DateLabels(180)`。顺带把 `visitors.Skip(...).Sum()` 提到循环外（4 次重复 → 1 次）。

### BK-01 六个 Kit：4930 行近似重复的组合层（P1，结构性）

- 位置：6 个 `*Kit.cs`（`AppKit.cs` 1035 行 / `AiKit.cs` 1014 / `CommerceKit.cs` 933 / `TeamKit.cs` 743 / `DataKit.cs` 677 / `MarketingKit.cs` 528）；对照 `src/Arc.Avalonia.Demo/DemoUi.cs`
- 证据：
  ```csharp
  public static TextBlock Caption(string text, double maxWidth = 0) => Text(text, "arc-caption", maxWidth);
  public static StackPanel Row(double spacing, params Control[] children) { ... }
  // AppKit.cs:67,96-113 与 DataKit.cs:130,171-185 逐字相同
  ```
- 触发/放大：30 个 helper 名在 ≥4 个 Kit 中出现；`DemoUi` 才是基础层提供的同层 API，但 111 个块里 0 次调用。后果有三：①任何组合层修复要做 6 次（并已出现 API 漂移：Heading 默认 class、Screen/Specimen 宽度、Scroller 固定高度）；②`Btn` 类 helper 把 label 塞进 `StackPanel+TextBlock`（`AiKit.cs:282-305`），经 Kit 创建的每个 `ArcButton` 因此多 2 个节点；③"块与 128 个组件的一致性"靠 6 份人工约定维持，而不是靠共享组件。
- 影响：不是单点掉帧，而是把 §2 的每个修复乘 6，并让"测量成本"（节点数/构建时间）在 111 页上不可控。
- 修复建议：抽出 `Arc.Avalonia.BlocksKit`（或扩展 `DemoUi`）——颜色/文本/布局/卡片/按钮/状态行/数字格式/确定性随机；各工程 Kit 退化为"该产品的少量特化 helper"（例如 AiKit 的 Markdown/DiffLine 保留）。先收敛 `Text/Caption/Row/Stack/Surface/Panel/Btn/Divider/Pill` 这 9 个占全部调用 80% 的 helper。

### P2 组（合并说明）

- **BD-08**：`ArcChoroplethMap` 的 `Step()`（`:88-100`）每次 `SortedValues`（`:86`，`OrderBy+ToList`）后再 `Count`，每个区域在 Render 里被调 2–4 次（`:216,220,225`）；region 只有 4–9 个，实际开销小，但 `OnPointerMoved` 里还重跑 `Layout()`（`:117`）+ `FirstOrDefault` 闭包（`:119`）。改为在数据变更时预排序一次 + 命中用矩形表。
- **BD-09**：`ArcLayoutMorph`（`:37-56`）在构造时创建 16 ms timer（每实例一个、不 Dispose），切布局后跑 22 帧、每帧 24 卡（2 文本/卡 ⇒ 约 48 个 `FormattedText`/帧，`Compute` 只在尺寸变化时跑，这部分是好的）；reduced-motion 走 `_progress=1` 直接画终态（`:76-80`）。建议 timer 懒建 + 用 `CompositionTarget.Rendering` 或直接 `Transitions`。
- **BD-10**：`ArcCohortChart`（Team，8×8）在每格循环里做 `ArcResources.Brush/Color`（`:136-201`，每格 5+ 次）+ `Blend/Contrast` 亮度计算 + 每格 `FormattedText`；hover 每次移动重绘（`:394-401`）。它至少声明了 `AffectsRender`（`:58-61`），是 20 个自绘控件里唯一这么做的；建议把 token 颜色、字体、对比色在字段里缓存。
- **BD-11**：`ArcCohortChart.MeasureOverride` 返回固定 `(860,316)`（`:91`）忽略 `availableSize`；`DataStage.MeasureOverride`（`DataDraw.cs:192-197`）在无限宽时退回 320——两者都让自绘页在窄窗口下溢出而非收缩（P2，当前截图宽度 1440 不触发）。
- **BD-12**：`RevenueGlobePage` 的 900 ms 回放 timer（`:88,190-206`）在 Play 时启动、Pause/终态停止，但启动与 tick 都不读 `ArcThemeManager.ReducedMotion`——与其余 8 个动画/播放 timer 不一致；用户开"减弱动效"后地球仍会每 900 ms 跳一小时。加一行门禁即可，或在 `Refresh` 时停表。
- **BP-03**：Commerce 的 6 个"假异步"timer（`CartDrawerPage:301`、`CheckoutLivePage:173`、`CheckoutSummaryPage:184`、`InvoiceStudioPage:200`、`ProductDetailPage:281`、`SupportConversationPage:155`）都是点击后一次性延迟再 `Stop`；量级小，但没有 detach 保护（页面在延迟期被切走仍会 tick 一次并改已脱离的控件）；建议统一封装 `FakeDelay(ms, action)` 并检查 `VisualRoot`。
- **BP-05**：换肤路径 = `GalleryWindow.RebuildAll`（`:488-502`）清空全部缓存页 → 当前页 120–220 节点重建；块页面全部用快照 brush，所以这是"必须重建"的设计后果。配合 BK-02 的 242 处字面量半径，换肤不改变结构尺寸，重建是纯浪费。
- **BP-06**：固定 1000 px（`AppKit.cs:192` 等）+ 内容区水平滚动 Disabled（`GalleryWindow.cs:88`）在 <1412 px 窗口下裁切；`MinWidth` 是 1024，说明作者预期会更窄。建议 `MaxWidth=1000` + `HorizontalAlignment=Stretch` 或按可用宽度的比例列。
- **BP-07**：`ArcEmptyScene` 构造器里注册类 handler（`:87` `SceneProperty.Changed.AddClassHandler<ArcEmptyScene>(...)`）——`AddClassHandler` 是类级注册，每次实例化都会再加一条，N 个实例 → 一次属性变化触发 N 次 `StartMorph()`。应移到 `static ArcEmptyScene()`。
- **BP-08**：`FeatureBentoPage.cs:271-298` 页面内私有 `ProgressTrack : Control` 重复实现 `ArcProgress`（每 Render 3 次 `Mk.Brush`）；`ArcSpotlightCard.cs:196-200` 每次指针移动 `InvalidateVisual`，`:158-170` 每次重绘 `new RadialGradientBrush` + 2 个 `GradientStop`（SpotlightGridPage 6 张卡）。前者删掉用 `ArcProgress`，后者缓存 brush 只改 `Center`。
- **BK-02/03/04**：`Radius(ArcTokens.*)` 0 处 vs 242 处字面量半径（`DataKit.Specimen:256` 写 26、`Card:270` 写 18）；`AutomationProperties` 覆盖极不均（AI 39 / App 1）；`ArcMeshWash` 的 `Drift` 无调用点（`MarketingKit.Wash` 默认 false，`:207-211`），timer 仍随实例创建。

## 4. 该区域设计评价与修复优先级

**做得好的：**

1. **页面契约统一**：111 个块全部是 `ArcItemPage` + `ArcDocLayout`，文档结构（何时用/可访问性/动效/实现备注）零重复；`DemoOnCanvas=false` 让块专注整屏产品界面（`ArcItemPage.cs:12,47`、`ArcDocLayout.cs:21-141`）。
2. **颜色纪律是整仓最好的**：0 处硬编码 hex，`Brushes.Transparent` 是唯一直接 brush；Kit 里所有颜色都经 `ArcResources`/token，不存在"块里随手写个 #fff"。
3. **reduced-motion 覆盖完整**：17 个 timer 里 9 个动画/播放型，8 个有门禁（`ArcMarquee:82,109`、`ArcMeshWash:42,80,122`、`ArcStatNumber:97`、`ArcEmptyScene:101`、`ArcMorphIllustration:114`、`ArcLayoutMorph:76`、`ConnectedScatterPage:154`、`LineReplayPage:155`）；唯一例外是 `RevenueGlobePage` 的 900 ms 回放。自绘图表默认画静态帧。
4. **示例数据确定、便宜**：mulberry32 固定种子（`DataKit.cs:665-676`）+ `DataSamples`（`:24-56`），截图与键盘走查可复现；数据在 `BuildDemo` 内落在局部变量，无静态集合增长（全 Blocks `static readonly Dictionary/List` 为 0）。
5. **自绘控件普遍支持键盘与焦点**：15 个 `DataStage` 全部 `Focusable` 且实现方向键/Home/End/Escape（`DataDraw.cs:176-182`），`ArcStatNumber`/`ArcEmptyScene` 等对 detach 有处理；Team 的 `ArcCohortChart` 还正确使用了 `AffectsRender`。
6. **状态反馈一致**：每个块都用 `Status()` 的 polite live region 播报动作（`DataKit.cs:601-608`、`AppKit.cs:694`），这是可访问性上比 128 个组件本身更完整的一层。

**最该先修的 3 件事：**

1. **BD-02 + BD-03（两个 P0）**：让"指针移动 → 全量重排 + 全量重绘"变成"缓存布局 + 局部失效"。银河图按分组缓存 2400 点、按 cluster 预生成 brush；地球把 1620 点的 alpha 量化成 6–8 档 brush 并缓存投影。这是 Blocks 里唯一符合"高频输入 × 数据量线性"的掉帧源。
2. **BD-04**：给 `Ink`/`DataText` 加缓存（主题版本号 + Pen/Brush/Typeface/FormattedText）。它是 15 个数据块自绘的全部公共乘数，成本低、收益覆盖 72 处 `InvalidateVisual` 的每条路径。
3. **BK-01 + BP-01/BP-02/BP-04**：先抽公共 `BlocksKit`（收敛 9 个占 80% 调用的 helper），再修三个"交互即全量重建"的代表页（Inbox 三栏、媒体播放 seek、指标仪表盘 DateLabels）——它们说明的不是单页 bug，而是块层普遍缺少"增量更新"约定。

**设计层面的结构性问题（不是单点 bug）：**

1. **组合层被复制了 6 次**：`DemoUi` 已经存在，但 111 个块长在 6 套 `internal static` Kit 上（4930 行、30 个重名 helper、3 套宽度策略）。这让"111 个块与 128 个组件的一致性"变成 6 份人工约定，任何修复乘 6，也让"每页多少节点/多少测量"无法统一测量。
2. **自绘数据控件的"重绘协议"没有统一**：15 个 DataStage 都是"普通字段 + 手工 `InvalidateVisual` + Render 内重算一切"，没有 `AffectsRender`（除 Team 的 cohort）、没有几何/布局缓存、pointer 与 Render 各算一遍（`Layout()` 在 `ArcGalaxyStage:147,267`、`ArcChoroplethMap:117,197`、`ArcSemanticMap:160,235`、`ArcSmallMultiples:89,164`、`ArcTerrainStage:230,287` 都是同一模式）。应该有一个 `DataStageBase` 提供"脏区域 + 相机/尺寸缓存 + 主题缓存 + 文本缓存"。
3. **"整树重建"是块层默认的状态更新方式**：Inbox 的 `Build()`、指标盘的 `Rebuild()`、散点页的 `RefreshStats/RefreshSlider`、媒体页的 `BuildSeek` 都是同一思路——把状态变化翻译成"重造控件"。当前 8–2400 条示例数据下还可接受，但块是"可迁移到真实产品界面"的样板，这个模式会被复制到真实数据量上。修复方向不是逐个页面打补丁，而是给块一个轻量 MVU/增量绑定约定（行控件的 `Update(state)` 而不是重建）。
4. **代码取值与 DynamicResource 的双轨**：块页面用 `ArcResources` 快照 + 换肤整页重建（`GalleryWindow.cs:488-502`），自绘控件则每帧重查资源。前者让 111 页在换肤时全部重建，后者把 R2 摊到每一帧。与基础层 FO-04/FO-05 是同一个根因，应在 token 层一次性解决（缓存 + 版本失效通知），块层才能既便宜又正确。

**说明**：本分片全部为静态分析，未运行构建、未跑截图、未做 profiler；"每次 N 个对象/每次 N 次查找"是按代码路径 × 数据规模推算的量级，不构成实测帧时间；`BD-11`、`BP-06` 的溢出现象与 `BD-02/03` 的实际帧率建议在修复前后各做一次计时埋点确认。
