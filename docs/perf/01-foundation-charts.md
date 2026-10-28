# 基础层 + 图表 + Demo/Gallery：组件设计与性能分析

## 0. 范围与覆盖

**覆盖工程与实际文件数**（均为排除 `obj/` 生成文件后的真实源码数；简报中的 14/10/14 含每项目 3 个 obj 生成文件）：

| 工程 | 非 obj `.cs` | 主题文件 | 构成 |
| --- | --- | --- | --- |
| `src/Foundation/Arc.Avalonia` | 11 | 4（`Theme/ArcTheme.axaml` + 3 个 ControlTheme） | 调色板/主题/动效/图标/5 个控件 |
| `src/Components/Arc.Avalonia.Charts` | 30 | — | 22 个自绘控件 + `Core/ChartCore.cs` + 7 个页面 |
| `src/Apps/Arc.Avalonia.Demo` | 7 | — | `ArcPage`/Registry/DemoUi/Catalog/ItemPage/DocLayout/Aliases |
| `src/Apps/Arc.Avalonia.Gallery` | 11 | — | shell、ShotRunner、CatalogAudit、NavigationSmoke、5 个页面 |

**完整精读**：`ArcThemeManager.cs`、`ArcPalette.cs`、`ArcColor.cs`、`ArcSpinner.cs`、`ArcIcon.cs`、`ArcMotion.cs`、`ArcButton.cs`、`ArcCard.cs`、`ArcBadge.cs`、`ArcButton.axaml`、`ChartCore.cs`、`ArcLineChart.cs`、`ArcBarChart.cs`、`ArcTreemap.cs`、`ArcRaceBarChart.cs`、`ArcRealtimeStream.cs`、`ArcAnimatedCounter.cs`、`ArcRadarChart.cs`（Render 段）、`ArcSankeyFlow.cs`（Render 段）、`ArcSunburstChart.cs`（Render 段）、`ArcBrushChart.cs`（Render/Strip 段）、`ArcGauge.cs`、`ArcSparkline.cs`、`ArcActivityHeatmap.cs`、`ArcPage.cs`、`ArcPageRegistry.cs`、`DemoUi.cs`、`IconsPage.cs`、`ShotRunner.cs`、`App.axaml.cs`、`GalleryWindow.cs`、`CatalogAudit.cs`、`ProofPage.cs`（片段）。

**grep 扫描**：`ArcTokens.cs`、`ArcIconData.cs`（161 条 path）、其余 9 个图表控件与 3 个图表页；全仓 `ArcResources.` / `DispatcherTimer` / `new Pen(` / `new FormattedText` / `InvalidateVisual` 的计数与上下文分类。

**方法**：只读静态分析 + 代码路径推理；用一个按大括号归类的脚本统计 `ArcResources` 调用所处方法（Render / Tick / pointer / 普通方法）；**未运行构建、未跑截图**，凡未经运行时验证的量化均标注「推算」或「suspected」。

> 口径说明：简报给的全仓字面量 `ArcResources.` ≈ 420 含注释与 XML 文档；本次按代码行精确计数为 **348 行调用**（本分片四个工程共 31 行：Foundation 2 + Charts 25 + Gallery 4），后文比例以 348 为分母。

## 1. 组件设计特征

### 1.1 控件类型分布与选择

- 基础层 5 个控件：`ArcButton : Button`（1 个 TemplatedControl 系）、`ArcCard`/`ArcBadge : ContentControl`（2 个）、`ArcIcon`/`ArcSpinner : Control` 自绘（2 个）。比例合理：按钮用模板 + CSS 风格 class，卡片/徽章是纯内容容器；图标/转圈只有笔划没有内容，自绘正确。
- Charts 22 个控件**全部**是 `ArcChartBase : Control` + `override Render`（`ChartCore.cs:42`），没有模板/Transitions。对数据图形而言合理（几何绘制不是样式问题），代价是放弃了 Avalonia 的内建缓存与动画设施，全部自己管。
- 变体模式统一：构造器 + `Property.Changed.AddClassHandler` 写 `Classes.Set(...)`（`ArcButton.cs:42-46,116-127`、`ArcCard.cs:19-23`、`ArcBadge.cs:25-29`），与 CONVENTIONS §3 一致。

### 1.2 token、主题与动效约定

- 82 个 `public const string` token（`ArcTokens.cs`），`ArcPalette.Build` 向字典写入 **81 个 key**（43 次 `Set(dictionary,...)` + 38 次 `dictionary[ArcTokens.*]`），其中颜色全部按 OKLCH 值定义再转 sRGB（`ArcPalette.cs:42-55,400-423`）；仅内建灰阶/white 用 `Colors.*`。控件代码里没有硬编码 hex（Charts 用 `ChartPalette.*` 间接取 token）。
- 主题切换只替换 Application 级 merged dictionary（`ArcThemeManager.cs:31-37`），控件全部 `{DynamicResource}`，**换肤不重建可视树**；但代码取值（`ArcResources.*`）是瞬时快照，这一点在 Gallery 里被显式补偿（`ShotRunner.cs:80-82`）。
- 动效：`ArcMotion` 暴露 4 个 SplineEasing + 3 个 `ArcSpringEasing` 单例（`ArcMotion.cs:14-26`），`ArcSpringEasing.Ease` 每次采样做 `Exp/Sin/Cos`（`ArcMotion.cs:57-89`）；56 处模板 `<Transitions>`。`ArcMotion.Smooth()` 设计为“每次调用 new Transitions”，但当前全仓 **0 调用**（latent 问题，见 FO-06）。
- reduced-motion：全仓 199 处引用；基础层的 `ArcSpinner`、`ArcAnimatedCounter`、`ArcRealtimeStream`、`ArcRaceBarChart` 四个 timer 使用者都读了该开关（好）。

### 1.3 交互、可访问性与一致性

- 做得对的地方：图标几何用 `ConcurrentDictionary` 缓存 `Geometry.Parse`（`ArcIcon.cs:17,73-88`），大小缩放放在 Render 的 transform 而不是几何重算（`ArcIcon.cs:95-101`）；spinner 在 `OnDetachedFromVisualTree` 停表并置空（`ArcSpinner.cs:71-76`）；图表基类 `ClipToBounds=true` + 确定性 `MeasureOverride`（`ChartCore.cs:44-57`）；Line/Bar/Treemap/Radar/Sankey/Sunburst 都实现了键盘导航 + `Focusable`（如 `ArcLineChart.cs:173-209`、`ArcBarChart.cs:136-172`）。
- 不足：图表控件没有任何 `AutomationProperties`；`ArcIcon`/`ArcSpinner` 也没有可访问名；图表更接近“键盘可用但屏幕阅读器不可读”。
- 一致性：颜色 100% 走 token；但 Charts 里每个控件都在 Render 里通过 `ChartPalette` 做资源查找（见 CH-10）；阴影用法克制（图表只用 1px Pen，不叠 BoxShadow，好）。

### 1.4 Demo/Gallery 结构

- `ArcPage` 是非常薄的契约：Title/Group/Order/Slug + `Build(context)`（`ArcPage.cs:16-34`）；页面**按需 Build**，Gallery 缓存已构建视图（`GalleryWindow.cs:28-36,522-557`），并有“同一视图只属于一个 ScrollViewer”的注释与实现（好设计）。
- 页面发现靠反射：`ArcPageRegistry.Discover` 扫描 `Arc.Avalonia*.dll`、`GetTypes()`、`Activator.CreateInstance` 每个 page（`ArcPageRegistry.cs:22-59`），结果静态缓存；规格为 221 个 page 类（167 个 `ArcItemPage` + 54 个组合页）。
- 截图工具 `ShotRunner`：先 `ReducedMotion = true`（`ShotRunner.cs:28`），再逐主题 `ArcThemeManager.Apply`、逐页 `page.Build` → `Measure` → `RenderTargetBitmap.Render`（`ShotRunner.cs:77-125`）。

## 2. 性能问题清单

| ID | 分类 | 严重度 | 位置 | 问题一句话 | 放大因子 |
| --- | --- | --- | --- | --- | --- |
| FO-01 | T1/R1 | P0 | `ArcButton.axaml:43-51` + `ArcSpinner.cs:53-68` | 每个 ArcButton 模板内置一个隐藏 ArcSpinner，附加即创建 16 ms timer，不看 IsVisible | × 每个按钮 × 62.5 tick/s；侧栏常驻 ≥59 按钮 |
| FO-02 | R1/R2 | P0 | `ArcSpinner.cs:78-105` | 每帧重建 StreamGeometry + new Pen，Stroke 为空时每帧资源查找 | × spinner 实例 × 60 fps |
| FO-03 | R1/R2 | P1 | `ArcIcon.cs:65-103` | 每次 Render new Pen；Foreground 为空时每次 Render 查资源 | × 图标实例（IconsPage 161 个） |
| FO-04 | R2 | P1 | `ArcThemeManager.cs:51-70`（全仓 348 处） | ArcResources 无缓存，每次 TryFindResource | × 调用点 × 每帧/每次交互 |
| FO-05 | R2/T2 | P1 | `ArcThemeManager.cs:24-38` + `ArcPalette.cs:35-249` | 换肤重建 81 项字典（每次 25–40 次 OKLCH 换算）并 Remove+Add，整树 DynamicResource 重解析 | × 全树绑定数（每按钮 ≥8） |
| FO-06 | T2 | P2 | `ArcMotion.cs:41-42` | Smooth() 每次 new Transitions（当前 0 调用） | 随未来调用点 |
| FO-07 | T1 | P2 | `ArcSpinner.cs:56`、`ArcRealtimeStream.cs:602`、`ArcAnimatedCounter.cs:122`、`ArcRaceBarChart.cs:653` | ReducedMotion 只在启动时读，运行中切开关不停已有 timer | × 存活实例数 |
| FO-08 | R2 | P2 | `ArcIcon.cs:96` | 取色 fallback 只在 Render 内解析、无主题失效通知，换肤后可能残留旧色（suspected） | × 未显式设色的图标 |
| CH-01 | T1/R1/S2 | P0 | `ArcRaceBarChart.cs:694-747` | 16 ms timer；每 tick 两次 BuildRows（LINQ 排序）+ 每行 2–3 个 FormattedText | × 行数 × 62.5/s × entity 数 |
| CH-02 | T1/R1/R3 | P0 | `ArcRealtimeStream.cs:623-649,263-393` | 16 ms timer；每帧全量 min/max 扫描 + 每序列两次几何 + 多段文本 | × series × 样本数 × 62.5/s |
| CH-03 | P1(输入)/R1/S2 | P0 | `ArcLineChart.cs:127-140,492-501` + `ChartCore.cs:459-516` | pointer-move 触发的每次重绘都重建全部几何并重建 tooltip 文本 | × 序列 × 点数 × 指针事件率 |
| CH-04 | R1/R3 | P1 | `ArcLineChart.cs:211-305` | 每 Render：LINQ 过滤、每序列 Point/List、面积+折线几何、切线数组、标签文本全重建 | × 序列数 × 点数 |
| CH-05 | R1 | P1 | `ArcBarChart.cs:174-286,327-340` | 每根柱子一个 StreamGeometry；每根柱子/每个网格线 new Pen/Brush | × 柱数 × 重绘频率 |
| CH-06 | R1/R3 | P1 | `ArcTreemap.cs:120-210` + `ChartCore.cs:519-625` | 每次 Render 重算 treemap 布局（LINQ+列表，WorstRatio 每候选 `row.Append`），ItemIndex 又 O(N²) | × 瓦片数；指针每移动一次重算 |
| CH-07 | R1/R3/S2 | P1 | `ArcSankeyFlow.cs:306-529` | 每 Render 多次 LINQ 扫描 + 循环内 `nodes.First`（O(N²））+ 每条 ribbon 一个几何 + 每节点文本 | × 节点/边数 |
| CH-08 | R1/R3 | P1 | `ArcSunburstChart.cs:270-403` | 逐层扫描全部 arc（O(层数×arc)）+ 每 arc 一个 Pen/几何 | × 弧数 × 层数 |
| CH-09 | R1/R2/S2 | P1 | `ChartCore.cs:106-200` | 每次建文本都 new FormattedText 并查 FontBody；Alpha 每次 new SolidColorBrush | × 文本/着色调用（113/46 处） |
| CH-10 | R2 | P2 | `ChartCore.cs:66-104` | 图表内 245 处 ChartPalette 属性调用每处都做资源查找 | × 每帧绘图调用 |
| CH-11 | R1 | P2 | `ArcGauge.cs:155-178`、`ArcSparkline.cs:261-296` | Gauge 每帧 LINQ 排序阈值；Sparkline 每帧 Min/Max + 点列表 | × 重绘频率 |
| CH-12 | R1 | P2 | `ArcActivityHeatmap.cs:172-204` | hover 时每格 new 半透明 SolidColorBrush（最多 196 格） | × 格数 |
| DG-01 | S1/L2 | P1 | `GalleryWindow.cs:632-666` | 首次点击无独立页的 catalog 项会同步 Build 最多 54 个组合页找标题 | × 页数（一次性） |
| DG-02 | S1/R1 | P1 | `GalleryWindow.cs:158-162,271-277,454-482` | 搜索每敲一键重建 Appearance 段，8 个 accent chip 各调一次 ArcPalette.Build | × 8 × 每键 × 81 项 |
| DG-03 | T1/S1 | P1 | `ShotRunner.cs:28,101-125` | 离屏截图依赖 ReducedMotion，但 18 个文件里的 timer 不读它，页面存活期仍在跑 | × 页面内此类控件数 |
| DG-04 | L2/R1 | P2 | `IconsPage.cs:29-63` | 单页同步创建 161 组（Icon+Border+TextBlock），无虚拟化 | × 161 |
| DG-05 | S1 | P2 | `ArcPageRegistry.cs:22-75` | 反射加载全部程序集 + GetTypes + 221 次 Activator（每个进程一次） | × 程序集/类型数 |
| DG-06 | S1 | P2 | `CatalogAudit.cs:21-43` | `--audit` 同步 Build 全部页面 | × 221 页 |

## 3. 逐条说明

### 3.1 基础层

#### FO-01 每个 ArcButton 都带一个看不见的 16 ms timer（P0）

- 位置：`src/Foundation/Arc.Avalonia/Themes/ArcButton.axaml:43-51`、`src/Foundation/Arc.Avalonia/Controls/ArcSpinner.cs:53-68`、`src/Foundation/Arc.Avalonia/Controls/ArcButton.cs:84-89`
- 证据：
  ```xml
  <c:ArcSpinner x:Name="Spinner" ... IsVisible="False" ... />
  ```
  ```csharp
  _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
  _timer.Tick += (_, _) => { _angle = (_angle + 7.5) % 360; InvalidateVisual(); };
  _timer.Start();
  ```
- 触发/放大：模板实例化 → Spinner 进入视觉树 → `OnAttachedToVisualTree`，**不检查 IsVisible**。Avalonia 的 `IsVisible=false` 只影响布局/渲染，不改变视觉树附着（本仓未跑运行时验证，suspected 但为默认语义）。于是每个按钮 = 一条 16 ms timer：Gallery 侧栏常驻至少 59 个 ArcButton（54 个组合页按钮 + overview 页 + 3 个 tier 按钮 + 2 个主题按钮，`GalleryWindow.cs:175-200,318-333,427-452`），展开分组后更多；全仓 243 个 `new ArcButton` 代码点、页面内按钮成打。按 100 个实例推算：**100 × 62.5 ≈ 6250 次 Tick/s**，每次改角度 + `InvalidateVisual`。隐藏 spinner 不产生实际绘制，但调度与脏标记开销仍在（suspected）。
- 影响：UI 线程被大量事实上无事的 timer 唤醒，耗电、输入延迟抖动；`Loading` 从未使用的按钮也一直付这份钱。
- 修复建议：模板按需创建 spinner（`Loading=true` 时用 ContentTemplate 注入），或 `ArcSpinner` 在 Attached/Detached + `IsVisibleChanged` 时 Start/Stop；更进一步，全仓共用一个 `DispatcherTimer` 或 `CompositionTarget.Rendering` 驱动所有 spinner（角度相位错开即可）。

#### FO-02 ArcSpinner 每帧重建几何与 Pen（P0）

- 位置：`src/Foundation/Arc.Avalonia/Controls/ArcSpinner.cs:78-105`
- 证据：
  ```csharp
  var geometry = new StreamGeometry();
  using (var figure = geometry.Open()) { figure.BeginFigure(start, false); figure.ArcTo(end, ...); }
  context.DrawGeometry(null, new Pen(stroke, thickness, null, PenLineCap.Round, PenLineJoin.Round), geometry);
  ```
- 触发/放大：每个 Tick → `InvalidateVisual()` → Render；每次 Render 一次 `StreamGeometry` + 一条 `Pen`；`Stroke` 为空时还多一次 `ArcResources.Brush`（第 80 行）。× 可见 spinner 数 × 62.5/s。`ProofPage` 有 3 个独立 spinner（`ProofPage.cs:119-121`），加载态按钮各 1 个。
- 影响：每帧 GC 压力（几何对象 + Pen）与 UI 线程几何构建，转圈越多的页面越明显。
- 修复建议：270° 弧的几何每实例只建一次（或 `static readonly`），动画只改 `RenderTransform = RotateTransform(angle)`；Pen 缓存为实例字段并按 (brush, width) 失效重取。

#### FO-03 ArcIcon 每次绘制 new Pen + 资源查找（P1）

- 位置：`src/Foundation/Arc.Avalonia/Controls/ArcIcon.cs:65-103`
- 证据：
  ```csharp
  var brush = Foreground ?? ArcResources.Brush(ArcTokens.TextSecondary);
  var pen = new Pen(brush, StrokeWidth, null, PenLineCap.Round, PenLineJoin.Round);
  ```
- 触发/放大：几何缓存本身做得对（`ArcIcon.cs:17,73-88`），但每次 **Render** 都新建 Pen；未显式设 `Foreground` 的图标再查一次资源。`IconsPage` 一页 161 个 ArcIcon 实例（`IconsPage.cs:29-63`），首次布局 + 每次滚动新暴露/重绘即 161 次 Pen 分配 + 最多 161 次 `TryFindResource`（全仓 `new ArcIcon` 218 个代码点）。
- 影响：单次几微秒，但页面滚动是连续重绘，161 个图标叠加成毫秒级；Pen 全是短命对象。
- 修复建议：按 `(IBrush, double)` 缓存 Pen（`ConditionalWeakTable`/小字典），或在 `Foreground`/`StrokeWidth` 变化时重算 Pen 存字段；fallback brush 在属性变化时解析并缓存。

#### FO-04 ArcResources.* 无缓存（P1）

- 位置：`src/Foundation/Arc.Avalonia/Theme/ArcThemeManager.cs:51-70`；代表调用：`ArcIcon.cs:96`、`ArcSpinner.cs:80`、`ChartCore.cs:96-104`、`ChartCore.cs:120-133`
- 证据：
  ```csharp
  public static IBrush Brush(string key) =>
      Application.Current is { } app && app.TryFindResource(key, out var value) && value is IBrush brush ? brush : Brushes.Transparent;
  ```
- 触发/放大：全仓按行计 **348 处** `ArcResources.`；脚本按方法上下文归类：**86 处（24.7%）直接位于 `Render` 方法体内**，139 处在普通方法（其中相当部分是 Render 调用的工具，如 `ChartCore.cs` 的 18 处、`ChartPalette` 属性、`ChartText.Font`），0 处直接位于 Tick/指针事件体（但这些事件都导致 Render，有效落点更高）。Charts 内的 25 处里有 7 处是“键盘焦点环”每帧取 FocusOutline（`ArcBrushChart.cs:520`、`ArcRaceBarChart.cs:469`、`ArcSankeyFlow.cs:530` 等）。`ChartText.Font` 每次建文本都 `TryFindResource(ArcTokens.FontBody)`（`ChartCore.cs:120-133`）。
- 影响：资源查找要遍历 Application merged dictionaries（81 项 palette + 各 ControlTheme），每次几十纳秒到微秒；乘以每帧调用数后成为图表/图标路径的固定税；也意味着换肤后必须整页重建才是确定的。
- 修复建议：`Apply` 时把 palette 快照到静态字段/`ConcurrentDictionary<string, object>`，`ArcResources` 直接读缓存（或 `ImmutableDictionary` + 版本号）；`ChartText.Font` 缓存 `FontFamily`。

#### FO-05 换肤代价链：81 项重建 + 整树 DynamicResource 重解析（P1）

- 位置：`src/Foundation/Arc.Avalonia/Theme/ArcThemeManager.cs:24-38`、`src/Foundation/Arc.Avalonia/Theme/ArcPalette.cs:35-249`、`GalleryWindow.cs:488-494`
- 证据：
  ```csharp
  var palette = ArcPalette.Build(scheme, accent);
  if (_current is not null) { app.Resources.MergedDictionaries.Remove(_current); }
  app.Resources.MergedDictionaries.Add(_current = palette);
  ```
- 量化：`Build` 每次写入 **81 个 key**（43 次 `Set` + 38 次索引赋值）；文件中 `ArcColor.Oklch` 共 74 处调用点，单次 Build 按 scheme/accent 分支实际执行约 25–40 次（每次都含 OKLab→sRGB 与 3 次 `Math.Pow`），`ArcColor.Hex` 共 29 处调用点（accent 分支执行其中若干），另有 6 处 `WithAlpha`、4 组 `BoxShadows`（`controlThumbShadow` + resting/raised/floating，每组 1–3 个 BoxShadow 对象）与 3 条 LinearGradientBrush。代价链：Remove+Add merged dictionary → Avalonia 对每个 `DynamicResource` 引用重新解析 → 每个模板实例（`ArcButton.axaml` 共 17 处 DynamicResource，其中每个按钮常驻解析的约 8–10 个）× 侧栏 ≥59 按钮 + 当前页数百控件，量级 **千次以上资源解析 + 样式失效/重绘**；Gallery 随后 `RebuildAll` 清空页面缓存、重建侧栏并重 Build 当前页（`GalleryWindow.cs:488-494,496-502`），当前页全部控件重新构造。
- 影响：一次主题/强调色切换是一次整窗重排+重绘峰值，低端机可见卡顿；也是“页面重 Build 后 ArcResources 快照才正确”这一设计负担的根源。
- 修复建议：换肤时复用字典实例、按 key 批量赋值以减少通知次数；保留 scheme 双缓存避免重复 74 次颜色换算；减少模板中 DynamicResource 数量（相邻 token 合并、尽量用继承属性）；Gallery 只重建可见页而不是清空全部缓存。

#### FO-06/07/08（P2）

- **FO-06**（`ArcMotion.cs:41-42`）：`Smooth` 每次调用 `new Transitions{...}`，是典型 T2；当前 0 调用，属潜在风险，落地前改成按属性缓存。
- **FO-07**：`ReducedMotion` 只在 attach/StartAnimation 时读取（`ArcSpinner.cs:56`、`ArcRealtimeStream.cs:602`、`ArcAnimatedCounter.cs:122,187`、`ArcRaceBarChart.cs:653`），运行中切换不会停止已存在的 timer；应订阅主题管理器的变更事件统一 Start/Stop。
- **FO-08**：`ArcIcon` 的 fallback 取色只在 Render 内发生（`ArcIcon.cs:96`），没有资源绑定/失效通知；主题切换后未显式设 `Foreground` 的图标可能保留旧色直到下一次 InvalidateVisual（正确性兼性能，suspected）。建议把 fallback 做成 `DynamicResource` 代理或在 palette 变化时统一 Invalidate。

### 3.2 图表

> **最重的 5 个图表（按每帧/每 tick 成本排序，推算）**：① `ArcBrushChart`（1335 行；文件中 25 处 `ChartPalette.*` 查找、8 处 `new Pen`、9 处 Alpha Brush 调用点，主图 + 缩略条 + 事件轨三层在每次重绘全部重建，拖拽窗口时每 pointer-move 一次）→ ② `ArcRealtimeStream`（16 ms timer 常驻，O(series×N) 扫描 + 每序列 Area+Line 两套几何）→ ③ `ArcRaceBarChart`（16 ms timer，每 tick 两次 BuildRows + 每行 2–3 个 FormattedText）→ ④ `ArcSankeyFlow` / `ArcSunburstChart`（每帧 LINQ/嵌套扫描 + 每边/每弧一个几何）→ ⑤ `ArcLineChart` / `ArcBarChart`（O(序列×点数) 几何重建，pointer-move 触发）。

#### CH-01 ArcRaceBarChart：16 ms timer × 两次布局计算 × 每行文本（P0）

- 位置：`src/Components/Arc.Avalonia.Charts/Controls/ArcRaceBarChart.cs:694-747`、`517-550`、`330-474`
- 证据：
  ```csharp
  private void OnTick()
  {
      UpdateRows(delta);      // → BuildRows(out max, out _)
      InvalidateVisual();
      if (!IsPlaying && Settled()) // → BuildRows(out max, out _) 第二次
          StopTimer();
  }
  ```
  ```csharp
  var rows = BuildRows(out var maximum, out _);              // UpdateRows:574
  var values = entities.Select(...).Where(...).OrderByDescending(...).ToList(); // BuildRows:532-537
  ```
- 触发/放大：播放/悬停动画期间 62.5 tick/s；每 tick 两次 `BuildRows`，每次先 O(E²) 建 entities（`entities.Contains`，第 525 行）再 LINQ `Select/Where/OrderByDescending/ThenBy/ToList`（532-537），`Interpolate` 每 entity 再查两帧；Render 每行 `ChartText.Create`×2、`ChartText.Width`（再建一个 FormattedText）以及 hover 态 tooltip（414-441、452-463）。× TopN 行（默认 TopN 通常 5–10）× entity 数。停止瞬间 `StopTimer` 又 `UpdateRows(1000)`（739）。
- 影响：动画期间每 16 ms 分配大量 List/FormattedText 并排序；即使数据只有 5 个实体、8 帧，文本布局仍是大头（FormattedText 是图表里最贵的单项）。
- 修复建议：`BuildRows` 结果缓存为字段，仅在 `_position`/`Frames`/`TopN` 变化时重算；`Settled` 用布局是否变化的 dirty 标记替代第三次全量重建；行文本按 `(entity, value, width)` 缓存 `FormattedText`；`entities` 用 `HashSet` 去重（O(E)）。

#### CH-02 ArcRealtimeStream：16 ms timer + 每帧全量重采样（P0）

- 位置：`src/Components/Arc.Avalonia.Charts/Controls/ArcRealtimeStream.cs:623-649`、`263-393`、`580-592`
- 证据：
  ```csharp
  for (var series = 0; series < _buffers.Count; series++)
      foreach (var value in _buffers[series]) { low = Math.Min(...); high = Math.Max(...); }
  ...
  var points = new List<Point>(sampleCount);
  context.DrawGeometry(ChartPalette.Alpha(brush, 0.09), null, ChartGeometry.Area(points, _plot.Bottom, true));
  context.DrawGeometry(null, ChartPalette.Pen(brush, 1.75), ChartGeometry.Line(points, true));
  ```
- 触发/放大：Live 模式下 62.5 tick/s；每 tick 追加样本（`AppendSample` 中 `List.RemoveAt(0)` 是 O(N) 搬移，582-590），然后 Render 做：O(series × N) 的 min/max 扫描（294-301）、每序列 `List<Point>`（350）、每序列 **两套 StreamGeometry（Area + Line）+ 两次 `new Pen`/Alpha Brush**（369-373）、4+4 个坐标标签 + reading row 每序列 2 个 FormattedText（320-342、410-411，共约 12 次文本/帧）、一次 `PushClip`、状态 pill 与 hover 分支。× series × 窗口样本数（N 默认量级 10²）× 60 fps。
- 影响：这是本区域唯一“持续 60 fps 自绘”的控件，单实例即可把 CPU 拉满一核的一部分；同页多实例或大区间时明显掉帧。
- 修复建议：优先改 `CompositionTarget.Rendering` 合并 tick 与渲染；min/max 用单调队列/环形缓冲增量维护（O(1) 更新）；`AppendSample` 用固定环形缓冲避免 `RemoveAt(0)`；曲线几何按“新增一段”追加或直接 `StreamGeometry` 每帧复用（可缓存的 `PathGeometry`）；坐标文本缓存（数值量化到步长）。

#### CH-03 pointer-move 触发全量重绘 + tooltip 文本重建（P0）

- 位置：`ArcLineChart.cs:127-140,379-407,492-501`、`ArcBarChart.cs:109-122,363-372`、`ChartCore.cs:459-516`
- 证据：
  ```csharp
  protected override void OnPointerMoved(PointerEventArgs e)
  { ... SetActive(Math.Clamp(index, 0, count - 1)); }     // 只有 index 变化才 InvalidateVisual
  ```
  ```csharp
  var rows = visible.Select(item => (item.Name, FormatValue(ValueAt(item, index)),
      (IBrush?)(item.Brush ?? ChartPalette.Series(SeriesIndex(item))))).ToList();
  var titleText = ChartText.Create(title, 12, ChartPalette.Foreground, FontWeight.Medium);
  var label = ChartText.Create(row.Label, 12, ChartPalette.Muted, maxWidth: 116);
  ```
- 触发/放大：已有 `SetActive` 相等判断做节流（不会每个 pointer 事件都重绘，好），但指针横扫时仍会以“索引变化次数”触发**整幅图表重绘**：LineChart 每次重建 S 条折线/面积几何 + 切线数组 + 全部标签；BarChart 每次重建 N 根柱子几何；tooltip 每次新建 1 个标题 + 每行 2 个 FormattedText + 一个 `List` + `new Pen`（492-497）+ 3 次资源查找。× 序列数 × 点数 × 每秒索引变化数。
- 影响：图表 hover 是用户最常触发的交互；N 大（几百点）时每次移动都有毫秒级重建，形成“跟随卡顿”。
- 修复建议：拆成两层——静态层（网格/系列几何）在数据/尺寸变化时缓存，交互层用轻量 `Control` 叠加只画十字线/tooltip；或缓存 `DrawingGroup`/`RenderTargetBitmap` 作为背景；tooltip 文本与尺寸按内容缓存；`ChartTooltip.Draw` 接受调用方传入的预建文本。

#### CH-04 ArcLineChart 每帧全量几何/分配（P1）

- 位置：`ArcLineChart.cs:211-305,343-377,421-484`
- 证据：
  ```csharp
  var visible = series.Where(item => !_hidden.Contains(item.Name)).ToList();
  var points = new List<Point>(count);                    // 每序列
  var geometry = ChartGeometry.Line(points, Smooth);      // 新 StreamGeometry + 切线 double[]
  context.DrawGeometry(null, ChartPalette.Pen(brush, 1.75, dashed), geometry);
  ```
- 触发/放大：每次 Render：`Where/ToList`（225）、`NiceMaximum` 全量 O(S×N)（463-484）、每序列 `List<Point>` + `StreamGeometry` + `new Pen`（283-291）、面积几何再一套（271-275）+ `Alpha` 新 Brush、每个标签一个 FormattedText（313、370）、`AppendSegments` 平滑时再分配 2 个 `double[]`（`ChartCore.cs:404-455`）。× 序列数 × 点数 × 重绘次数。
- 影响：一页 2–3 个折线图滚动重绘/主题切换时是主要分配源；纯静态图也每次交互全建。
- 修复建议：几何与标签按 `(Series, Labels, Bounds, Smooth, hidden)` 版本缓存，属性变化才失效；Pen/Brush 缓存；`nice maximum` 缓存；平滑切线数组复用（`ArrayPool<double>`）。

#### CH-05 ArcBarChart 每柱一个几何、每元素一个 Pen/Brush（P1）

- 位置：`ArcBarChart.cs:174-286,327-340`
- 证据：
  ```csharp
  for (var i = 0; i < values.Count; i++)
  {
      var fill = i == _hoverIndex || _hoverIndex < 0 ? brush : ChartPalette.Alpha(brush, 0.42);
      context.DrawGeometry(fill, null, RoundedTopBar(bar, Math.Min(6, barWidth / 2)));
  }
  ```
- 触发/放大：每次 Render 每根柱 `new StreamGeometry`（327）+ 非 hover 柱一次 `new SolidColorBrush`（Alpha）；网格 4 条线各一条 `new Pen`（205-208）、均值线一条 Pen + `new DashStyle`（225）、底线 Pen（269）、label 每个 FormattedText（315-323）、`values.Average()` 每帧 LINQ（220）。× 柱数 × 重绘频率。
- 修复建议：柱几何用 `DrawRectangle(brush, pen, Rect, radiusX, radiusY)` 直接画（Avalonia 支持圆角矩形，不需要手工 StreamGeometry）；Pen/Brush 提升为实例缓存；均值缓存。

#### CH-06 ArcTreemap 每帧重算布局 + O(N²) 索引（P1）

- 位置：`ArcTreemap.cs:120-210`、`ChartCore.cs:519-625`
- 证据：
  ```csharp
  var layout = TreemapLayout.Compute(items, bounds);   // 每次 Render
  var index = ItemIndex(item);                          // 每个瓦片再线性找一次
  ...
  var values = items.Where(item => item.Value > 0).OrderByDescending(item => item.Value).ToList();
  var work = values.Select(item => new Entry(item, item.Value / total * bounds.Width * bounds.Height)).ToList();
  ```
- 触发/放大：hover 任意瓦片 → `SetActive` → `InvalidateVisual` → **完整重算**：LINQ `Where/OrderByDescending/ToList/Sum`（524-531）、每行 `List` + `WorstRatio` 里 `row.Append(candidate)` 每个候选分配一次迭代器（`ChartCore.cs:610`），ItemIndex 又是 O(N) → 整体 O(N²)；每瓦片 `Alpha` Brush + `new Pen`（154-155）、每瓦片 `ChartText.Draw`。静置时只在尺寸/数据变化时重绘，一旦 hover 就每移动一次付一次。
- 修复建议：布局按 `(Items 版本, Bounds)` 缓存；`ItemIndex` 用 `Dictionary<ChartValue,int>`/`ReferenceEqualityComparer` 预建；WorstRatio 改用索引循环不分配；Pen 按 alpha 档位缓存。

#### CH-07 ArcSankeyFlow 每帧 LINQ + 循环内 First + 每边几何（P1）

- 位置：`src/Components/Arc.Avalonia.Charts/Controls/ArcSankeyFlow.cs:400-415,424-433,459-463,481-509,551-579,595`
- 证据：
  ```csharp
  var stale = _orders.Count == 0
      || !_orders.Values.SelectMany(ids => ids).All(known.Contains)
      || _orders.Values.Sum(ids => ids.Count) != known.Count
      || !_orders.Keys.OrderBy(column => column).SequenceEqual(columns.Values.Distinct().OrderBy(value => value));
  ...
  var node = nodes.First(item => string.Equals(item.Id, id, StringComparison.Ordinal));
  ```
- 触发/放大：每次 Render：`ToHashSet`+4 组 LINQ + `Distinct/OrderBy`（400-414）、`ids.Sum`（424）、节点循环里 `nodes.First` → **O(N²)**（433）、`links.Where/OrderBy/ThenBy/ToList`（459-463）、每条 band 一个 `Ribbon` StreamGeometry（487→595）、每节点 `new Pen`（497）、每节点 2–3 个 FormattedText（551-552、578-579）；hover/键盘会触发 InvalidateVisual。× 节点数 § 边数。
- 修复建议：列/顺序/布局只在 Nodes/Links 变化时重算；`_byId` 已是字典，循环内改用 `_nodesById`；Ribbon 几何按 `(source,target,thickness)` 缓存或整图 `DrawingGroup` 缓存；文本缓存。

#### CH-08 ArcSunburstChart 逐层扫描 + 每弧一个几何（P1）

- 位置：`src/Components/Arc.Avalonia.Charts/Controls/ArcSunburstChart.cs:270-403`
- 证据：
  ```csharp
  foreach (var level in Enumerable.Range(1, Math.Max(1, levels)))
      foreach (var item in _arcs)
          if (item.Level != level) continue;            // 逐层扫描全部弧：O(levels × arcs)
  ...
  context.DrawGeometry(brush, new Pen(ChartPalette.Surface, 1),
      ChartGeometry.DonutSegment(origin, itemOuter, inner, start - 90, sweep));
  ```
- 触发/放大：每次 Render 每个弧一个 `DonutSegment`（4–8 条 ArcTo 的 StreamGeometry）+ 一条 `new Pen`；被 dim 的弧再 `Alpha` 新 Brush（353）；逐层循环把弧数乘上层数；另有 12 处文本操作（面包屑/中心读数/图例，578-629）。hover/点击逐级下钻都会 InvalidateVisual。× 弧数 × 层数。
- 修复建议：`BuildArcs` 时按 Level 分组，绘制时直接遍历分组；弧几何缓存（数据/焦点/尺寸不变时复用）；Pen 只建一次；dim 用 `Opacity` 而不是新 Brush。

#### CH-09 ChartText / ChartPalette.Alpha：文本与半透明刷子的固定税（P1）

- 位置：`src/Components/Arc.Avalonia.Charts/Core/ChartCore.cs:84-94,106-200`
- 证据：
  ```csharp
  var formatted = new FormattedText(
      text,
      Culture,
      FlowDirection.LeftToRight,
      new Typeface(Font, FontStyle.Normal, weight),
      size,
      brush);
  formatted.SetFontFeatures(Tabular);
  ```
- 触发/放大：`ChartText` 的 3 个入口共 113 个调用点，每次 `Create` 都是新 `FormattedText` + `Font` 属性里一次 `TryFindResource`（120-133）+ `SetFontFeatures`；`Width` 也新建一个再丢弃（199-200）；`Alpha` 有 46 个调用点，每次 hover/透明度档位都新建 Brush；所有图表都没有任何文本去重。放大因子：× 标签数 × 重绘频率 × 图表实例数。
- 修复建议：加一个小的 `FormattedText` 缓存（key = text+size+weight+brush 引用+maxWidth，palette 切换时清空）；`ChartText.Width` 复用 `Create` 的缓存；`Alpha` 用 `(Color, byte)` 键的 `ConcurrentDictionary` 缓存，或改用 `ImmutableSolidColorBrush`。

#### CH-10/11/12（P2）

- **CH-10**：`ChartPalette.*` 共 245 处调用点（`Series/Tone/Alpha` 外，`Foreground/Secondary/Muted/Border*` 都直接调 `ArcResources.Brush`，`ChartCore.cs:96-104`）；每帧每处一次资源查找。随 FO-04 的缓存一起解决即可。
- **CH-11**：`ArcGauge.cs:155-158` 每帧 `Where/OrderBy/ToList` 阈值（阈值几乎不变），`ArcGauge.cs:149,178` 每帧两条 `Arc` 几何 + `new Pen`；`ArcSparkline.cs:261-296` 每帧 `Min/Max` + `List<Point>` + 面积/折线几何。低基数，影响量级小于 CH-01/02。
- **CH-12**：`ArcActivityHeatmap.cs:181-204` 每帧遍历 `_weeks*7` 格（默认约 28×7=196），hover 时每格 `ChartPalette.Alpha` 新建一个 Brush（192），active 格再 new Pen（200）。建议预生成 5 个 level brush 及其 dim 变体，绘制时只选不建。

### 3.3 Demo / Gallery

#### DG-01 首次点击 catalog 项可同步构建最多 54 个页面（P1）

- 位置：`src/Apps/Arc.Avalonia.Gallery/Framework/GalleryWindow.cs:632-666`
- 证据：
  ```csharp
  foreach (var page in _pages.Where(page => page is not ArcItemPage))
  {
      if (!_builtContent.TryGetValue(page.Title, out var content))
          content = page.Build(new ArcDemoContext(ArcThemeManager.Scheme, ArcThemeManager.Accent));
      var heading = FindHeading(content, target);
  }
  ```
- 触发/放大：点击一个没有独立 `ArcItemPage`、也没有 alias 映射的 catalog 项时（`ShowItem:586-630`），会同步遍历所有组合页，未构建的立即 `Build`（最多 54 页，含图表页/Block 页，单页可达数百控件），再递归遍历逻辑树找 `arc-heading`（`FindHeading:668-686`）。× 页数，一次性（构建结果缓存于 `_builtContent`，653）。
- 影响：首点某类条目的交互卡顿可达数百毫秒（suspected，未实测）；随后浏览才顺畅。
- 修复建议：把“页面 → 章节标题”的映射做成构建期索引（页面声明 `Sections` 元数据或静态注册表），避免为找标题而实例化整页；确实要构建时放到后台线程预构建（控件构造需在 UI 线程则分批 Post）。

#### DG-02 搜索每敲一键都重建 8 个 accent chip → 8 次 ArcPalette.Build（P1）

- 位置：`GalleryWindow.cs:158-162,202-285,271-277,454-482`
- 证据：
  ```csharp
  search.TextChanged += (_, _) =>
  {
      _search = search.Text ?? string.Empty;
      RefreshNavigation();
  };
  ...
  foreach (var accent in Enum.GetValues<ArcAccent>())
      accents.Children.Add(AccentChip(accent));
  ...
  var palette = ArcPalette.Build(ArcThemeManager.Scheme, accent);           // 每个 chip 一次
  var color = palette[ArcTokens.Accent] is ISolidColorBrush brush ? brush.Color : Colors.Black;
  ```
- 触发/放大：`RefreshNavigation` 每次都把 Appearance 段（主题按钮 + 8 个 accent chip + 版权文本）重建；每个 chip 调 `ArcPalette.Build` 完整构造 81 项字典（内含颜色换算）。× 8 × 每次按键/每次侧栏刷新 ⇒ 约 **648 次资源写 + 数百次颜色换算/键**，全部只为取一个 `Color`。
- 修复建议：accent chip 只需 8 个 Color：在 `Apply` 后用 `ArcPalette.Build` 的结果缓存一次即可，或给 `ArcPalette` 加 `AccentColor(accent, scheme)` 轻量函数；搜索时只重建结果列表，不动 Appearance 段。

#### DG-03 离屏截图与不受 ReducedMotion 约束的 timer（P1，suspected）

- 位置：`src/Apps/Arc.Avalonia.Gallery/Framework/ShotRunner.cs:25-28,101-125`；代表不受控 timer：`Arc.Avalonia.Actions/Controls/ArcActionSwap.cs:29-50`（4 个 timer）、`Arc.Avalonia.Inputs/Controls/ArcNumberField.cs:284,431`、`Arc.Avalonia.Actions/Controls/ArcToast.cs:52`
- 证据：
  ```csharp
  ArcThemeManager.ReducedMotion = true;          // ShotRunner.cs:28
  host.Child = BuildPage(page, theme);          // 每主题重新 Build 一次
  bitmap.Render(host); bitmap.Save(...);        // 122-124
  ```
- 触发/放大：`--shot` 逐主题、逐页构建并渲染；honor 该开关的 spinner/图表会停（`ArcSpinner.cs:56`、`ArcRealtimeStream.cs:602`、`ArcRaceBarChart.cs:653`、`ArcAnimatedCounter.cs:122`），但全仓有 **18 个文件**含 `DispatcherTimer` 且完全不引用 `ReducedMotion`（另有 Actions/Inputs/Blocks 的若干页面级 timer）。这些页面在 `Task.Delay(60/120)` 与离屏渲染期间 timer 仍在跑并继续 `InvalidateVisual`；页面切走时依赖各自 `OnDetached` 停表（多数实现，未逐一验证）。
- 影响：截图批次 CPU 升高、偶发非确定性（动画中途捕获）；不直接影响运行时用户路径，但让“截图验收”这一唯一验证手段失真。
- 修复建议：ShotRunner 在渲染前遍历视觉树，统一 Stop 所有 `DispatcherTimer`（或提供全局 `ArcThemeManager.AnimationEnabled=false` 由所有控件遵循）；截图脚本按页统计 timer 存活数作为回归指标。

#### DG-04/05/06（P2）

- **DG-04**：`IconsPage.cs:29-63` 同步创建 161 个 `(Border + ArcIcon + TextBlock)`，Grid 无虚拟化；滚动时每个图标各 Render 一次（× FO-03 的 Pen）。图鉴页可以接受，但可改成 `ItemsRepeater`/`VirtualizingStackPanel` 示例。
- **DG-05**：`ArcPageRegistry.Discover`（`ArcPageRegistry.cs:22-59`）对每个 `Arc.Avalonia*.dll` 做 `Assembly.LoadFrom`，对 22 个程序集 `GetTypes()`，再对 221 个 page 类型 `Activator.CreateInstance`；`GalleryWindow` 构造时（`GalleryWindow.cs:25`）执行一次并静态缓存，`--shot/--list/--audit/--smoke` 每个新进程再执行一次。属启动成本（P2），可在需要时再考虑源生成注册表。
- **DG-06**：`CatalogAudit.Run`（`CatalogAudit.cs:21-43`）同步 `Build` 全部页面做标题审计，仅 `--audit` 工具路径，P2。

## 4. 该区域设计评价与修复优先级

**做得好的：**

1. token 与主题架构干净：82 个 token 常量、颜色全部 OKLCH→sRGB 单一来源（`ArcColor`），控件/图表零硬编码色；换肤只换字典、模板全部 DynamicResource，`ArcButton` 的变体/尺寸/焦点环完全由 class + ControlTheme 表达（`ArcButton.axaml:65-136`）。
2. 自绘控件的“共享数学”抽象得好：`ChartCore` 的 `Crisp`/`ChartGeometry`/`ChartText`/`ChartFormat`/`ChartPalette` 让 22 个图表风格一致、轴与像素对齐（`ChartCore.cs:60,251-456`），减少重复 bug。
3. 图标几何缓存（`ConcurrentDictionary` + 缩放放 transform）与 spinner 的 detach 停表是正确范式（`ArcIcon.cs:17`、`ArcSpinner.cs:71-76`）。
4. Gallery 视图缓存 + “视图只属于一个 ScrollViewer”处理、以及 ShotRunner 显式补偿“快照 brush”的问题，说明作者清楚静态解析与 DynamicResource 的边界（`GalleryWindow.cs:28-36,517-557`、`ShotRunner.cs:80-82`）。
5. 图表普遍实现了键盘焦点/方向键/Escape、空数据态、`ClipToBounds`、窄宽度降级；reduced-motion 在 4 个 timer 使用者中均有分支。

**最该先修的 3 件事：**

1. **FO-01 + FO-02**：把“每个 ArcButton 一个隐藏 spinner + 每帧新几何/Pen”改成按需创建 + 共享 timer + `RotateTransform` 画固定弧。这一条在 Gallery shell（常驻侧栏 ≥59 按钮）就足以制造数千次/秒的无效 tick，是全仓性价比最高的修复。
2. **CH-01 + CH-02**：两个 16 ms 常驻 timer 改 `CompositionTarget.Rendering` 合并渲染，布局/文本/几何进缓存；Realtime 的 min/max 与追加改增量/环形缓冲。两台“动画图表”是唯一持续 60 fps 的自绘路径。
3. **FO-04/FO-05 + CH-09/CH-10**：给 `ArcResources`/`ChartPalette`/`ChartText` 加缓存层，并把换肤从“重建 81 项 + 整树重解析 + Gallery 全量重建”降为“原子替换 + 只重建可见页”。这同时修掉资源查找税与换肤卡顿。

**设计层面的结构性问题（不是单点 bug）：**

1. **“timer + InvalidateVisual + 全量重绘”是默认动画范式**（简报 B2；本区域 4 个 timer 使用者都这样），缺少“背景层缓存 + 交互层叠加”和“几何缓存失效协议”。图表即使静态也要在每次 hover 重建全部几何，这是整套 Charts 的共同上限。
2. **资源取值两套语义混用**：模板走 DynamicResource（自动更新），代码走 `ArcResources.*`（快照）。于是每个自绘控件都要在 Render 里重新查资源（R2），而 Gallery 又必须整页重建才能保证换肤后截图正确（DG-02/FO-05）。解决方向是给代码侧一个“palette 版本号 + 缓存”，并让控件在版本变化时统一 `InvalidateVisual`。
3. **自绘与模板体系的边界没有工具化**：22 个图表 100% 自绘、0 个 `AutomationProperties`，而基础控件走模板时又在 ArcButton 模板里塞了隐形 spinner。结果是“模板里的隐形控件”和“自绘里的每帧分配”两类问题同时存在，需要一次统一的渲染/动效基类（共享时钟 + 缓存 + 可访问性钩子）来收敛。

**说明**：以上没有运行时测量；所有“每帧/每秒”数字是由代码路径 × 实例数/帧率推算，`suspected` 项（IsVisible 与视觉树附着、换肤后 ArcIcon 残色、截图期不受控 timer、DG-01 卡顿时长）建议在修复前用一次 profiler/计时埋点确认。
