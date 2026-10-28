# Arc.Avalonia 控件库：组件设计与性能审查

> 审查对象：`D:\Code\VibeCoding\Arc.Avalonia`（Arc UI 的 Avalonia 12 / .NET 10 复刻，24 个工程、516 个非 obj `.cs`、约 17.7 万行、112 个 `.axaml`）。
> 审查方式：**只读静态分析**（7 个区域并行细读 + 全仓量化扫描），没有运行时基准；所有"每帧/每秒"数字都由代码路径 × 实例数/帧率推算，`待确认` 项单独标注（见 §8.C）。
> 分片报告：`docs/perf/01-…07-*.md`（7 份，共 1700+ 行，含逐条证据）。

---

## 0. 摘要

### 0.1 一句话结论

**设计层面这是一次高质量的设计系统移植**——token/OKLCH 单一来源、模板化控件、reduced-motion、键盘与 Automation 都有纪律；
**但运行时模型是"自绘 + 逐帧定时器 + 整树重建"**，缺少三件基础设施：**共享帧时钟、绘制资源缓存、数据虚拟化**。
这三点决定了它作为"展示库"很出色，作为"生产级控件库"还有明显性能上限。

### 0.2 关键数字

| 指标 | 值 |
| --- | --- |
| 工程 / 非 obj `.cs` / LOC / `.axaml` | 24 / 516 / 176,581 / 112 |
| 控件实现谱系 | `TemplatedControl` 76、裸 `Control` 33、`Panel` 24、`ContentControl` 14、`Button` 系 12、`ArcChartBase` 20、Special 基类 33 |
| token / 主题 | 82 个常量、`ArcPalette.Build` 写 81 个 key、全仓 1251 处 `{DynamicResource}` |
| 自绘 | 77 个文件 override `Render`；其中 29 个在 Render 内查资源 |
| Render 内每帧分配（77 个 Render 统计） | `new Pen` 98、画刷分配 39、几何创建 10、文本构建约 154、`ArcResources.*` 89 |
| 定时器 | 68 个文件 `new DispatcherTimer`；27 处 16 ms 帧循环、1 处 33 ms；**11 套**独立动画驱动实现，0 处 `CompositionTarget.Rendering` |
| 布局属性动画 | 14+ 处用 `Width/Height/Margin/Padding` 的 `Transitions` 逐帧动画（应为 transform） |
| 虚拟化 | 整库**仅 EmojiPicker** 用 `ListBox` 做行级虚拟化；DataGrid/TreeTable/聊天/Blocks 列表的显式虚拟化使用数为 0（全部手写 `Children.Clear()` + 重建） |
| 可访问性 | 703 处 `AutomationProperties`，键盘路径覆盖高（这是强项） |
| 问题总量 | 区域分片合计 **197 条**（P0 36 / P1 100 / P2 61，区域内相对排序）；统一口径后 **全局 P0 12 条、P1 约 100 条**（§3） |

### 0.3 最该先做的 5 件事（ROI 排序）

1. **让隐藏的 `ArcSpinner` 不再常驻计时**（`ArcButton.axaml:43-51` + `ArcSpinner.cs:53-68`）。当前每个 `ArcButton` 模板都实例化一个 `IsVisible=False` 的 spinner，附加到视觉树即创建 16 ms timer；Gallery 侧栏常驻 ≥59 个按钮，页面里成百上千，等于数千次/秒的无效 tick。改成"`Loading=true` 时才创建/启动 + 共享帧时钟"，一处改动收益最大。
2. **建立主题版本化的绘制缓存层**（Pen/Brush/`FormattedText`/`Typeface`/`Geometry`）。全仓 77 个 `Render` 里每帧新建 98 个 `Pen`、39 个画刷、约 154 份文本布局，并在绘制期做 89 次 `TryFindResource`；`ArcResources.*` 无缓存是公共乘数。
3. **用一套共享帧时钟替换 11 套动画驱动**。目前 7 套实现"每个动画 new 一个 `DispatcherTimer`"，3 套 per-control 帧时钟，1 套 spinner 专用；且 **0 处使用 `CompositionTarget.Rendering`**、**0 处可见性门控**。同一页多个动画 = 多个 60 Hz 定时器线性叠加。
4. **把"布局属性动画"改成变换动画**（14+ 处，`Margin/Width/Height` + `Transitions`）。每次 hover/选中都触发 25–42 帧的布局 pass，牵动整棵子树；改为 `RenderTransform`/自绘后成本降一个数量级。
5. **给数据视图补虚拟化与增量更新**（DataGrid/TreeTable/JsonViewer/ChatThread/Combobox/MultiSelect/ChipGroup）。100×8 的表格一次构建约 800 个控件，选中/排序走 `RebuildContent()` 整表重建；这是"能否上真实数据量"的分水岭。

### 0.4 总体评价（分维度）

| 维度 | 评价 | 说明 |
| --- | --- | --- |
| 设计系统保真 | ★★★★★ | token 零硬编码、OKLCH 单一来源、8 accent、同心圆角、动效语义完整 |
| 组件 API 与组合 | ★★★★☆ | 基类选择准确；缺共享列表/菜单层，18 处手写 `List<Control>` |
| 可访问性 | ★★★★★ | 703 处 Automation、完整键盘路径、焦点陷阱、帮助文本 |
| 主题与换肤 | ★★★★☆ | DynamicResource 全覆盖；代码侧快照缓存缺失导致自绘控件换肤不刷新 |
| 渲染性能 | ★★☆☆☆ | 无绘制缓存、每帧分配、静态层无分层、pointer-move 全量重绘 |
| 动画架构 | ★★☆☆☆ | 11 套实现、无共享时钟、无可见性门控、布局属性动画 |
| 大数据性能 | ★☆☆☆☆ | 几乎 0 虚拟化（仅 EmojiPicker 行级）、重建型 UI |
| 工程护栏 | ★☆☆☆☆ | 无测试/基准/性能预算，性能问题靠人工审查发现 |

---

## 1. 方法与覆盖

### 1.1 方法

* 7 个区域并行审查（Foundation+Charts+Demo/Gallery；Inputs+Actions；Pickers+Feedback+Effects；AdvancedInputs/Actions+Content+Media；Special.Glass/Motion/Media；Special.AIWidgets/Explainers；Blocks×6），每人按统一简报输出"设计特征 + 性能问题清单（分类码/严重度/证据/放大因子/修复方向）"。
* Lead 横向量化扫描：全仓模式计数、逐工程热点分布、`Render` 方法体级分配统计、定时器/生命周期清单、热点文件指数、依赖图与基类谱系。
* 抽查复核：对分片中最关键的 20 余条结论逐条回读源码验证行号与语义，纠正了 3 处过度陈述（见 §8.C）。

### 1.2 严重度口径（统一后）

* **P0**：在持续热路径（≥30 fps 或每次指针/按键事件）上必然发生，成本随**实例数/数据量/帧数**线性放大，常见使用下用户可直接感知（掉帧、输入延迟、内存膨胀）。
* **P1**：交互/切换/构建路径上明显，或条件触发（大列表、深树、频繁换肤、长会话）；可感知但不是持续背景开销。
* **P2**：清洁度、潜在风险、可维护性；当前规模下影响小。
* 各分片内的 P0 是"区域内部排序"，汇总时按上述口径归一化：36 条区域 P0 收敛为 **12 条全局 P0**（§3.2），其余降级并入 P1（§3.3）。这样做是为了避免"Special 展示件每条都是 P0"导致的优先级通胀。

### 1.3 覆盖与局限

* 覆盖：全部 24 个工程的目录结构、依赖、基类、token/主题、动效与绘制基础设施；24 个工程中 22 个有逐控件精读样本，Blocks 以全量 grep + 12 个重文件精读覆盖。
* 局限：仓库无测试工程、无基准、无运行时遥测；本次未运行 profiler。结论是"代码路径 + 放大因子"的静态推断，凡涉及实际帧时间/分配量的量化结论都标记为推测，并给出验证方案（§7）。
* 未审查：`tools/`、`shots/`、`.recon/` 等辅助目录；第三方依赖（只有 Avalonia 官方包）。

---

## 2. 架构与组件设计分析

### 2.1 分层与依赖：干净的"基础层 + 特性层 + 组合层"

24 个工程构成单向依赖图，没有循环引用：

```
Arc.Avalonia                    基础层：OKLCH 色彩引擎 / palette / tokens / 主题 / 动效 / 图标 / 5 个基础控件
   ├─ Arc.Avalonia.Demo         页面契约（ArcPage/ArcItemPage）、ArcCatalog（294 项目录）、DemoUi 助手、反射页面发现
   ├─ Inputs / Actions / Feedback / Charts / Pickers / AdvancedInputs / AdvancedActions /
   │  Effects / Content / Media / Special×5       特性层：只依赖 基础层 + Demo
   └─ Blocks×6                  组合层：依赖多个特性层，把 128 个组件拼成 111 个页面级区块
        └─ Arc.Avalonia.Gallery 展示壳：引用全部程序集，反射发现 221 个页面
```

**设计优点**：特性层之间零耦合，任一组件族可单独发布；Blocks 是真正的"组合层"，没有反向依赖基础层。
**代价**：**没有共享的 `Rendering/Animation` 内部库**。于是每个特性层各自实现了一套绘制/文本/补间助手（§2.5、§2.4），
同名 `ArcText` 出现 3 份、`ArcTween` 2 份 + 5 个变体、11 套动画驱动——这是本库大量重复性能问题的**架构根源**。

### 2.2 控件谱系：模板优先，自绘次之，Special 另成体系

按类声明统计（`class X : Base`，含页面类）：

| 基类 | 数量 | 典型用法 | 评价 |
| --- | --- | --- | --- |
| `ArcPage` / `ArcItemPage` | 54 / 166 | 页面与文档页 | 反射发现，一次性构建 |
| `TemplatedControl` | 76 | 输入、选择、开关、滑杆、菜单、对话框…… | 主要范式；模板 + `ControlTheme` + CSS 风格 class |
| `Button` / `ArcButton` | 9 / 3 | 按钮族 | 继承原生 Button，复用命令/焦点/键盘 |
| `ContentControl` | 14 | Card/Badge/MetricCard 等容器 | 正确：内容容器无需模板重写 |
| `Panel` | 24 | Special widgets（代码搭树 + 内层自绘） | 合理但缺模板化，状态更新靠整树重建 |
| 裸 `Control` | 33 | Icon/Spinner、签名板、色域、日历面、图表…… | 自绘；无模板、无 Automation 钩子 |
| `ArcChartBase` | 20 | 全部图表 | 共享 `MeasureOverride`/`Crisp`/调色板/文本，设计好 |
| `ArcMotionCanvas` / `ArcSpecialCanvas` / `ArcExplainer` | 11 / 11 / 11 | Special 55 项 | 三套"舞台基类"：命中、焦点、键盘、reduced-motion 收敛于此 |
| `ArcFieldBase` / `ArcPickerBase` / `ArcTextFieldBase` | 8 / 4 / 4 | 输入与选择器 | 变体逻辑收敛（好） |

**结论**：基类选择整体准确——有内容的走模板，纯图形的走自绘；变体用 class 而非新控件类型。
真正的设计不一致出现在**三个"世界"之间**：

1. **模板世界**（Inputs/Actions/Feedback/Pickers）：`ControlTheme` + `PART_*` + `DynamicResource`，主题切换自动生效。
2. **自绘世界**（Charts/Pickers 的 surface/AdvancedInputs 的部分控件/Blocks.Data）：代码里 `ArcResources.Brush()` 取快照，
   必须每次 `Render` 重新取（否则换肤不刷新），于是绘制期资源查找成为常态。
3. **Special 世界**：`Panel` 代码搭树 + 舞台基类自绘 + 自有噪声/素材生成器；视觉最复杂，性能模型最"重"（§2.4）。

### 2.3 token 与主题：设计上最干净的一层

* 82 个 `ArcTokens` 常量，`ArcPalette.Build` 每次写 81 个 key；颜色全部以 OKLCH 定义并由 `ArcColor`（OKLab↔sRGB、`color-mix(in oklab)`）转换成 sRGB——**控件代码零硬编码 hex**（分片 grep 证实）。
* 换肤路径：`ArcThemeManager.Apply` 移除并重新加入 Application 级 merged dictionary（`ArcThemeManager.cs:24-38`），
  模板全部 `{DynamicResource}` 自动重解析；Gallery 额外调用 `RebuildAll()` 清缓存并重建当前页。
* **设计缺陷（性能相关）**：代码侧 `ArcResources.Brush/Color/Double/...` 是**无缓存的瞬时查找**
  （`ArcThemeManager.cs:51-70`），而且**只在调用时解析、没有主题版本号**。后果：
  * 自绘控件每次 `Render` 都要重新查资源（全仓 89 次位于 Render 方法体内）；
  * 换肤时已缓存的画刷快照不会失效，多个自绘控件（ElasticSlider、RangeHistogramSlider、BillingPrice、ScrollArea、ImageCompare）需要额外订阅 `ActualThemeVariantChanged` 才能部分修复，且 **accent 切换不触发该事件**——存在换肤后颜色残留的正确性风险（§8.C 待确认项）。
* 另一处成本：全局 `TextBlock` 样式给**每个 TextBlock** 设置 4 个 `DynamicResource`（`ArcTheme.axaml:20-25`），
  控制上万文本节点的样式绑定与换肤重解析；这是"干净但昂贵"的典型 token 用法。

### 2.4 动效体系：11 套实现，3 种哲学

| # | 实现 | 位置 | 模型 | 问题 |
| --- | --- | --- | --- | --- |
| 1 | `ArcSpinner` 专用 timer | `Controls/ArcSpinner.cs:53-68` | 每实例 16 ms | 隐藏也跑；每帧建几何/Pen |
| 2 | `ArcAnim.To/Delay` | `AdvancedInputs/…/ArcAdvancedInternals.cs:22-75` | **每动画一个 timer** | 不可取消、无 detach 清理 |
| 3 | `ArcTween`（Feedback） | `Feedback/Internal/ArcUi.cs:266-309` | 每 tween 一个 timer，可 Stop | 同屏 N 个数值动画 = N 个 60 Hz |
| 4 | `ArcTween`（Effects） | `Effects/…/ArcEffectVisuals.cs:125-184` | 每 tween 一个 timer | 同上；另有 `ArcViewport` 订阅泄漏 |
| 5 | `ArcMediaTween` | `Media/…/ArcMediaUi.cs:193-233` | 每 tween 一个 timer | 同上 |
| 6 | `ArcContentTween` | `Content/…/ArcContentUi.cs:284-330` | 每 tween 一个 timer | 同上 |
| 7 | `SvTween`/`SvClock` | `Special.AIWidgets/…/SpecialVisuals.cs:261-330` | 每 tween 一个 timer + 循环时钟 | 完成态不停（MorphLoader） |
| 8 | `GlassTween`/`ArcFrameLoop` | `Special.Glass/…/GlassMotion.cs:16-175` | 每 tween timer / per-control 帧循环 | 18 个 timer 的直接来源 |
| 9 | `ArcMotionCanvas` | `Special.Motion/…/Special.MotionUi.cs:506-587` | **per-control 帧时钟**（tween 列表 + `NeedsFrame`） | 设计最好的一套；仍无可见性门控 |
| 10 | `ArcSpecialCanvas` | `Special.Media/…/SpecialUi.cs:366-437` | per-control 帧时钟 | 同上 |
| 11 | `ArcExplainer` | `Special.AIExplainers/…/ArcExplainer.cs:25-106` | per-control 33 ms 时钟，相位纯函数 | 每幅图解无条件 30 Hz 重绘 |
| — | XAML `Transitions` | 56 处模板 + C# 若干 | 属性过渡 | **14+ 处动的是布局属性**（§3.2 G-P0-8） |

**评价**：
* **好**：`reduced-motion` 是真正的"降成本"（帧时钟不启动，而非冻结状态）；延迟/意图类动画（MegaMenu 悬停意图、tooltip）
  没有在 pointer-move 里做测量；`ArcMotionCanvas`/`ArcExplainer` 的"状态 + 相位纯函数"模型干净。
* **差**：没有全局帧调度器，`CompositionTarget.Rendering` 使用次数为 0；没有"可见/进入视口才播"的门控（grep 为 0）；
  7 套实现"一个动画一个定时器"，语义（取消、合并、优先级、异常）各不相同，导致同一个 bug 要修 7 次。
* **动画优先级滥用**：大量 timer 使用 `DispatcherPriority.Render`，在输入繁忙时与布局/渲染争抢同一优先级队列。

### 2.5 自绘体系：能力很强，但缺少"缓存协议"

77 个文件 override `Render`，规模上相当于一个迷你 2D 绘图框架：图表（20）、Special（33）、Blocks.Data 内嵌图表（15）、
Pickers surfaces（6）、AdvancedInputs 滑杆/价格（3）。

* **好的部分**：`ChartCore` 把 `Crisp`/`ChartGeometry`/`ChartText`/`ChartFormat`/`ChartPalette` 收敛，22 个图表风格一致；
  `SpecialVisuals.Sv`/`ExplainInk.Ink` 统一了 Special 的取色/文本/卡片/弧线；`ArcIconData` + `ConcurrentDictionary` 缓存图标几何；
  `Sm.Noise` 提供确定性程序化素材（可重复渲染，截图稳定）。
* **结构缺陷**：**没有"静态层 / 动态层"分层**。所有 surface 都是"状态变 → 整块 `Render` 重画"，
  于是静态内容（日历日格、图表骨架、Illustration 背景、Glass 材质）永远陪动画重画。
  典型：GravityWell 每帧 36 次 `SampleArt`、CoverFlow 16 次、PhotoGrid 25 次；日历每帧 49 份文本布局；
  签名板每次 pointer-move 重画全部笔画。
* **没有缓存助手**：`ChartPalette.Pen()` 每次 `new Pen`、`ChartPalette.Alpha()` 每次 `new SolidColorBrush`、
  `ChartText`/`Ink.Text` 每次 `new FormattedText` + 每次 `TryFindResource` 取字体。全仓只有图标几何做了缓存（`ArcIcon`、`Ink.IconCache`）。

### 2.6 组合与数据层：重建型 UI，几乎不用虚拟化

所有列表型 UI（DataGrid、TreeTable、SortableDataTable、TreeView、JsonViewer、CommentThread、ChatThread、Combobox、
MultiSelect、ChipGroup、RadioGroup、PhoneInput 国家选择、EmojiPicker 分类）都是手写 `StackPanel` + `foreach` + `Children.Clear()`。
C# 代码里 `ItemsControl`/`DataTemplate`/`ItemsRepeater`/`VirtualizingStackPanel` 使用次数为 **0**；XAML 侧只有 7 处 `ListBox`
（Select 菜单 ×3、EmojiPicker ×3、TimePicker ×1），其中仅 EmojiPicker 的行级虚拟化真正生效。

后果有三类：
1. **规模上限**：100×8 表格一次构建约 800 个控件（DataGrid），聊天/国家列表/JSON 树同理；
2. **交互成本**：选中、排序、展开、每次按键都"清空 + 重建"，焦点/tab stop 需要 `Dispatcher.Post` 补丁；
3. **状态丢失与闪烁**：滚动位置、动画状态在重建时重置（部分控件用缓存字段绕过，增加复杂度）。

这是一个**设计选择**而非疏忽（CONVENTIONS 明确"变体用 class、模板用 ControlTheme"，但没有规定列表必须数据驱动），
但它是"能否用于真实产品"的关键分界线。

### 2.7 可访问性：强项

* 703 处 `AutomationProperties`；输入控件完整键盘路径（Home/End/Esc/方向键/roving tab stop）；Dialog 有焦点陷阱；
  MegaMenu 有"安全三角形"；日历有键盘模型与帮助文本；图表有键盘导航（但无 Automation 名称）。
* Special 55 项每项一个 focus stop + `Describe(name, help)`；这是移植项目里少见的完整度。
* 不足：图表/图标/Spinner 无自动化名称；部分自绘 surface 只有名称没有角色/值语义。

### 2.8 设计上做得好的地方（应保留）

1. **token 单一来源**：OKLCH → `ArcColor` → immutable brushes；8 套 accent 与明暗由同一套语义角色派生，无硬编码色。
2. **模板化范式统一**：`ControlTheme` + CSS 风格 class + `PART_*`，变体/尺寸/焦点环完全由样式表达；主题切换零重建。
3. **基础控件实现教科书级**：Icon 几何缓存 + 缩放放 transform；Spinner detach 停表；`ArcButton` 键盘焦点环与 loading 状态；
   `ArcCard`/`ArcBadge` 只做内容容器。
4. **图表共享内核**：`ArcChartBase` 的确定性测量 + `Crisp` 像素对齐 + 统一文本/格式/调色板，20 个图表视觉一致。
5. **Special 舞台基类**：`ArcMotionCanvas`/`ArcSpecialCanvas` 把命中、焦点、键盘、pointer capture、reduced-motion 收敛到基站，
   单控件只写状态与绘制——全仓最成熟的自绘组件范式。
6. **动效语义纪律**：断言类数值从不过冲；reduced-motion 在 199 处被引用且多数真的短路；截图 harness 显式设置 reduced-motion 保证确定性。
7. **可访问性覆盖**：703 处 Automation、完整键盘、焦点陷阱、帮助文本。
8. **演示与验收基础设施**：目录 294 项审计（`--audit`）、441 张明暗截图、导航 smoke、`ArcItemPage` 统一文档模板。

### 2.9 结构性设计问题（不是单点 bug）

| # | 问题 | 影响面 | 建议 |
| --- | --- | --- | --- |
| S1 | **无共享帧调度器**：11 套动画驱动、0 处 `CompositionTarget` | 全仓动画 | 在 foundation 提供 `ArcAnimationClock`（单 ticker + Job + reduced/visibility 门控） |
| S2 | **无绘制缓存层**：Pen/Brush/Text/Geometry 每帧新建，资源每帧查找 | 77 个 Render | `ArcDrawingCache`：按 (token, alpha, thickness) / (text, size, brush) 缓存 + 主题版本失效 |
| S3 | **无静态/动态分层**：整块 surface 重画 | 日历/图表/Special/Pickers | `DrawingGroup`/`RenderTargetBitmap` 静态层缓存 + 交互层叠加 |
| S4 | **用布局属性做动画**：Margin/Width/Height/Padding 的 Transitions 14+ 处 | Actions/Glass/Advanced/Media/Effects | 统一改 `RenderTransform`/`ScaleTransform`/自绘；在 `ArcMotion` 提供白名单 |
| S5 | **重建型 UI**：几乎 0 数据驱动列表（C# `ItemsControl` 0、仅 EmojiPicker 行级虚拟化），18 处手写 `Children.Clear` | 表格/列表/聊天/菜单 | 共享 `ArcOptionList`/`ArcVirtualList`（ItemsControl + ControlTheme 行模板 + 选择状态） |
| S6 | **主题快照无失效协议**：代码侧取色是快照，自绘控件无订阅/无版本号 | 自绘控件换肤正确性 | `ArcResources.Get(token)` 返回可观察或带版本号的缓存；`ArcThemedControl` 基类统一 `InvalidateVisual` |
| S7 | **动画生命周期不闭合**：68 个创建 timer 的文件中 32 个未见 detach 处理（抽样确认其中部分是瞬态 tween 或由所属控件停表，但 `ArcEmptyScene`、`ArcLinkUnfurl`、`ArcMorphIllustration`、`ArcToolCall` 等确有长期循环风险）；所有帧时钟均不看可见性 | 导航/多实例页面 | 基类统一 `OnAttached/Detached` 记账；`IsEffectivelyVisible` 门控 |
| S8 | **没有性能护栏**：无测试/基准/预算/lint | 全仓 | 加 `--perf` 采样模式、帧时间/分配预算、CI 回归 |

其中 S1/S2/S5 是"不改架构就只能局部打补丁"的三件事，也是 §6 路线图的主干。

---

## 3. 性能问题：统一分级

### 3.1 归一化说明

7 份分片共记录 **197 条**问题（P0 36 / P1 100 / P2 61，均为区域内相对排序）。
按 §1.2 的统一口径，把"持续热路径 + 线性放大 + 用户可感知"三条同时成立的收敛为 **12 条全局 P0**；
其余区域 P0（如"某个 Special 页面的默认播放"、"某处一次性 timer 未停"）降为 P1，并按主题聚类。
每条都可在对应分片 §3 找到完整证据（分片索引见 §8.B）。

### 3.2 全局 P0（12 条）

| ID | 问题 | 关键位置 | 放大因子 | 量级 / 证据 | 修复方向 |
| --- | --- | --- | --- | --- | --- |
| **G-P0-1** | 每个 `ArcButton` 模板内置**隐藏** `ArcSpinner`，附加即建 16 ms timer（不看 `IsVisible`） | `ArcButton.axaml:43-51`；`ArcSpinner.cs:53-68`；`ArcButton.cs:81-90` | × 按钮数 × 62.5 tick/s | Gallery 侧栏常驻 ≥59 按钮，页面内成百上千；1,000 按钮 ≈ 6.25 万次 tick/s | 懒创建（`Loading=true` 才建/启动）；或共享帧时钟 + 不可见不 tick |
| **G-P0-2** | **环境动画默认永久 60 fps** 且无可见性门控（打字机/语音球/正在播放/变形 loader/沙画/dot grid/头像环） | `ArcTypewriterTerminal.cs:80,150-162`；`ArcVoiceOrb.cs:26,128`；`ArcNowPlaying.cs:31`；`ArcMorphLoader.cs:121-133`；`ArcSandText.cs:88`；`ArcDotGrid.cs:174-224`；`ArcAvatarRing.cs:101`；`ArcExplainer.cs:92-106` | × 实例数 × 60 fps | 这些控件"进页即播、永久不停"；一页多实例线性叠加 | 默认暂停/可见才播；进程级 60 Hz 帧服务 + `IsEffectivelyVisible` 退订 |
| **G-P0-3** | **绘制期分配与资源查找**：`Render` 里 `new Pen/Brush/FormattedText/Typeface/TransformGroup` + `ArcResources.*` 每帧 `TryFindResource` | 全仓 77 个 `Render`：`new Pen` 98、画刷 39、文本约 154、资源查找 89；`ChartCore.cs:106-200`；`ArcCalendarSurface.cs:222-232,327`；`ArcElasticSlider.cs:186-192`；`ArcColorSurfaces.cs`（10 次/帧）；`Special.MotionUi.cs:71-100` | × 图元数 × 帧率 × 控件数 | 自绘控件默认"每次重绘重建一切"；GC 与绘制时间双升 | 主题版本化绘制缓存（Pen/Brush/Text/Typeface）+ `ArcResources` 缓存 |
| **G-P0-4** | 日历每次 `Render` 重建 **49 份 `FormattedText`** + 7 次资源查找 + 42 次 `PushOpacity`；hover 每跨一格整面重绘 | `ArcCalendarSurface.cs:208-332`（`Text` 工厂 `:327-332`） | × 跨格频率（10–30/s）× 双月 × 日期控件数 | 42 日格 + 7 星期 = 49 文本布局/帧 | 文本/日期格缓存为几何或静态层；hover 只重绘交互层 |
| **G-P0-5** | DataGrid / TreeTable / SortableDataTable **零虚拟化**，且选中/排序/编辑走 `RebuildContent()` 整表重建 | `ArcDataGrid.cs:1082-1086,1366`；`ArcTreeTable.cs:1258` | × 行×列（100×8≈800 控件/次）；× 每次点击 | 每次选择重建 800 控件；列宽拖动逐 move 全表列定义重排 | 虚拟化行 + 行控件池；选择/排序增量更新（作者已有 `_cellFrames` 增量路径可复用） |
| **G-P0-6** | RichTextEditor 每次按键**全文解析两遍 + 重建整棵预览控件树**（预览隐藏时仍重建） | `ArcRichTextEditor.cs:762-780,819-827`；`ArcMarkdown.cs:123-147,596` | × 文档长度 × 按键率 × 预览块数 | 预览是多控件 StackPanel，不是单个 TextBlock；工具栏随选区重建 | 解析一次、增量渲染、隐藏跳过；预览块复用 |
| **G-P0-7** | ChatThread 每条消息/typing 变化**整树重建**，头像每次重新解码，无虚拟化 | `ArcChatThread.cs:135-139,453-536,846-873` | × 消息数 × 头像数 × 重建次数 | 长会话线性劣化；滚动/自动滚动触发全量测量 | 消息项复用 + 增量追加 + 虚拟化 + 头像位图缓存 |
| **G-P0-8** | **用布局属性做动画**：`Margin/Width/Height/Padding` 的 `Transitions`，每帧触发布局 pass | `ArcActionVisuals.cs:282-290,302-336`；`ArcAdvancedVisuals.cs:244-248,280-331`；`ArcSlider.cs:321-348`；`ArcMegaMenu.cs:1187-1227`；`ArcGlassTabBar.cs:617-663`；`ArcGlassSegmented.cs:678-706`；`ArcHoldToConfirm.cs:338-357`；`ArcResizablePanels.cs:529,591`；`ArcCarousel.cs:606-629` | × 25–42 帧 × 受影响子树复杂度 × hover/选择频率 | 一次 hover = 25+ 次 measure/arrange；`ArcGlide` 被 Tabs/菜单/ButtonGroup/MegaMenu 共用 | 改 `RenderTransform`/`ScaleTransform`/自绘；`ArcMotion` 禁止布局属性过渡 |
| **G-P0-9** | **pointer-move 全量重绘/重算**：签名板重画全部笔画、折线图重建几何+tooltip、色域/对比滑块重建渐变、图像对比逐线填充 | `ArcSignaturePad.cs:333-346` + `Render :152-202`；`ArcLineChart.cs:127-140,492-501`；`ArcColorSurfaces.cs:61`；`ArcImageCompare.cs:618-628`；`ArcRangeHistogramSlider.cs:417-426,650` | × 已采样点数 / 序列×点数 / 事件率 | 签名 1000 px 挥笔 ≈1600 点，5 笔后单帧 >8000 点；拖动 1 s ≈ 60 次全量重绘 | 已完成层缓存（`DrawingGroup`）；拖动层单独绘制；几何增量 |
| **G-P0-10** | 常驻动画图表：16 ms timer + 每帧全量 min/max 扫描 / 每行重排 + 文本 | `ArcRaceBarChart.cs:694-747`；`ArcRealtimeStream.cs:623-649,263-393` | × series × 点数 × 62.5/s | 每 tick 两次 LINQ 排序 + 每行 2–3 份 `FormattedText`；实时流每帧 O(series×N) | 环形缓冲 + 增量 min/max；`CompositionTarget.Rendering` 合并；文本缓存 |
| **G-P0-11** | Special 展示件**静态图层无缓存**，每帧重画整个复杂场景 | `ArcGravityWell.cs:187-258`（36×SampleArt）；`ArcCoverFlow.cs:243-319`（16×）；`ArcPhotoGrid.cs:404-523`（25×）；`ArcHoloCard.cs:762-799`（172 新 brush/帧）；`ArcInkFluid.cs:404-431`（150 粒子）；`ArcActivityRings.cs:545-552`（168 几何/帧） | × 图元数 × 60 fps × 实例数 | 单帧数百到上千图元；`SampleArt` 每次 ~25–45 图元 + 6 次 OKLCH | 按 (seed,尺寸,主题) 缓存 `RenderTargetBitmap` 静态层；粒子改预渲染贴图 |
| **G-P0-12** | 弹层/建议/选项在**每次打开或每键**清空重建（行构建两次、强制 `Measure`） | `ArcCombobox.cs:231-271`；`ArcMegaMenu.cs:1032-1107,1285-1352`；`ArcShareAccess.cs:736-757,1050-1140`；`ArcPasswordStrength.cs:224-317`；`ArcOnboardingChecklist.cs:225-374` | × 行数 × 打开/按键次数 | 打开延迟与 GC 主因；菜单表面行集合构建两次 | 行模板 + 复用；过滤用增量/虚拟化；去掉回调内 `Measure` |

> 注：G-P0-1 与 G-P0-3 是"乘数关系"——隐藏 spinner 让**数千个**每帧分配点常驻；只修其中一条收益都会减半，应一起修。
> G-P0-2/G-P0-11 只影响 Special 展示页（每页 1–3 个 60 fps 循环），对库消费者可视为"可选重度组件"，优先级可低于前 6 条。

### 3.3 P1 主题聚类（100 条）

| 聚类 | 内容 | 代表位置 | 约计 |
| --- | --- | --- | --- |
| P1-A 动画/定时器生命周期 | 一个动画一个 timer、不可取消、detach 未停、`DispatcherPriority.Render` 滥用、reduced-motion 运行中切换不停表 | `ArcAnimatedCounter.cs:204`；`ArcActionSwap.cs:44-55`；`ArcCopyButton.cs:59-64`；`ArcTooltip.cs:103-113`；`ArcLinkUnfurl.cs:634-672` | 14 |
| P1-B 输入热路径 | 每键重建规则/建议/菜单行、强制 `Measure`、双写 `Text`、Automation 属性逐键写 | `ArcPasswordStrength.cs:224-317`；`ArcTextarea.cs:77-163`；`ArcMoneyInput.cs:201-292`；`ArcMentionInput.cs:462-692`；`ArcPhoneInput.cs:941-1124` | 10 |
| P1-C 弹层与菜单 | 每次打开重建表面、BoxShadow 每帧重栅格、行构建两次、定位 | `ArcMegaMenu.cs:1187-1227`；`ArcActionVisuals.cs:187-198`；`ArcMenuSurface.cs:96-141` | 6 |
| P1-D 图表几何/文本 | 每 Render 全量重建几何与标签、O(N²)、tooltip、treemap/sankey/sunburst 每帧布局 | `ArcLineChart.cs:211-305`；`ArcTreemap.cs:120-210`；`ArcSankeyFlow.cs:306-529`；`ArcSunburstChart.cs:270-403` | 9 |
| P1-E 列表/树/JSON 中等规模 | 树展开重建、每行 `GetRawText()` 复制、虚拟化仅行级 | `ArcJsonViewer.cs:711`；`ArcTreeTable.cs:611`；`ArcEmojiPicker.cs:499,1078` | 6 |
| P1-F 无谓分配 | `TransformOperations.Parse` 字符串、每帧 `new TransformGroup`/`new Typeface`/`new Cursor`、LINQ + List | `ArcAdvancedVisuals.cs:217-228`；`ArcGlassMenu.cs:460-464`；`ArcPagination.cs:82`；`ArcCalendar.cs:423` | 16 |
| P1-G 主题/资源 | `ArcResources` 无缓存、换肤整树重建、自绘控件快照不刷新（accent 不触发） | `ArcThemeManager.cs:51-70`；`ArcElasticSlider.cs:186-192`；`ArcScrollArea.cs:59`；`ArcImageCompare.cs:63` | 8 |
| P1-H Gallery/Demo | 页面缓存无淘汰（访问过的页永久持有控件树）、首次点击 catalog 项同步构建最多 54 页、搜索每键重建侧栏并 8× `ArcPalette.Build`、启动反射 221 页 | `GalleryWindow.cs:28-36,158-162,454-482,559-583,632-666`；`ArcPageRegistry.cs:22-75` | 8 |
| P1-I 布局事件滥用 | 12 处 `LayoutUpdated` 常驻订阅里强制 Measure/定位；布局动画期间 60/s 执行 | `ArcPagination.cs:73-89`；`ArcExpandableText.cs:189-235`；`ArcMegaMenu.cs:314-355` | 6 |
| P1-J Special 中等开销 | Glass 每帧改宽高/每帧 Parse、双面过渡、pointer-move 无条件重绘、LinkUnfurl shimmer 无 detach | `ArcGlassTabBar.cs:617-663`；`ArcBookingPill.cs:158-172`；`ArcDock.cs:202-216`；`ArcKeycapKeyboard.cs:130` | 7 |

> 完整 100 条见分片 §2 表格（ID 可追溯：`FO-*`/`CH-*`/`IN-*`/`AC-*`/`PF-*`/`FB-*`/`EF-*`/`CT-*`/`AI-*`/`MD-*`/`AA-*`/`GM-*`/`MO-*`/`AIW-*`/`AIX-*`/`BL-*`）。

### 3.4 P2 汇总（61 条）

主要是：清洁度与潜在风险（全局 `TextBlock` 样式 4 个 DynamicResource、`Classes.Set` 每次属性变更写 7–10 个 class、
`ArcMotion.Smooth()` 每次 new `Transitions` 的潜在 API、`ArcIcon` 换肤后 fallback 残色）、
一次性 timer 的 GC 压力、无界/未裁剪集合（ContextWindow 文件列表、CardStack 决策列表）、
`MeasureOverride` 忽略 `availableSize`（`SpecialUi.cs:424` 固定 900×520）、
自绘控件零 `AutomationProperties` 等。单条收益小，但累积起来构成"这个库不像模板化控件那样轻"的体感。

### 3.5 分区域计数

| 区域 | 覆盖文件（非 obj `.cs`） | P0 | P1 | P2 | 分片 |
| --- | --- | --- | --- | --- | --- |
| 基础层 + 图表 + Demo/Gallery | 59（+4 axaml） | 5 | 12 | 9 | `docs/perf/01-foundation-charts.md` |
| Inputs + Actions | 62（+3 axaml） | 3 | 12 | 9 | `docs/perf/02-inputs-actions.md` |
| Pickers + Feedback + Effects | 78 | 6 | 16 | 10 | `docs/perf/03-pickers-feedback-effects.md` |
| AdvancedInputs/Actions + Content + Media | 50 | 4 | 21 | 8 | `docs/perf/04-advanced-content-media.md` |
| Special.Glass + Motion + Media | 77 | 9 | 18 | 6 | `docs/perf/05-special-glass-motion-media.md` |
| Special.AIWidgets + Explainers | 47 | 7 | 12 | 6 | `docs/perf/06-special-ai.md` |
| Blocks×6 | 143 | 2 | 9 | 13 | `docs/perf/07-blocks.md` |
| **合计** | **516** | **36** | **100** | **61** | **197 条** |

---

## 4. 横向根因分析

> 分片回答"哪里慢"，本节回答"为什么会系统性地慢"。7 个根因按影响排序，每个都给出全仓量化证据。

### R1 帧时钟碎片化：11 套动画驱动，0 个共享调度器

**现象**：68 个文件 `new DispatcherTimer`；27 处 16 ms 帧循环、1 处 33 ms；`CompositionTarget.Rendering` 使用 0 次；
`IsVisible`/`EffectiveViewport` 门控 0 处。

**机制**：两种模型并存——
* 7 套"每个动画 new 一个 `DispatcherTimer`"（`ArcAnim`、Feedback/Effects 的 `ArcTween`、`ArcMediaTween`、`ArcContentTween`、`SvTween`、`GlassTween`）；
* 3 套 per-control 帧时钟（`ArcMotionCanvas`、`ArcSpecialCanvas`、`ArcExplainer`）+ 1 套 spinner 专用。
每个 timer 都是独立的 native 定时源 + 独立的 dispatcher 队列项，`DispatcherPriority.Render` 下与布局/渲染争抢；
语义（取消/合并/异常/优先级）各不相同，同一个 bug 要修 N 次。

**量化**：Gallery 侧栏常驻 ≥59 个 `ArcButton`，每个带一个隐藏 spinner timer（G-P0-1）→ 约 3.7k tick/s 的**空转**；
若打开一个含 20 个 `ArcButton` 的页面再翻两页，空转即升至 5k+/s。Special 页的单页并发循环 0–3 个（`ArcMotionCanvas` 模型较好），
但 Blocks/Marketing 的 marquee/stat-number 等又各自带 16 ms 循环。

**修复**：foundation 层 `ArcAnimationClock`（单 `DispatcherTimer` 或 `CompositionTarget.Rendering`，Job 列表、
优先级、取消、reduced-motion、可见性门控一次实现），把 11 套实现变为 1 套 + 3 个适配器。

### R2 绘制期分配与资源查找：没有缓存协议

**现象（量化）**：

* 77 个 `Render` 方法体内：`new Pen` **98**、画刷分配 **39**、几何创建 **10**、文本构建约 **154**、`ArcResources.*` **89**。
* 29 个文件的 `Render` 内直接调用 `ArcResources.*`，最高单文件 10 次/帧（`ArcColorSurfaces.cs`）、9 次（`ArcElasticSlider.cs`、`ArcEmptyScene.cs`）、8 次（`ArcRangeHistogramSlider.cs`）。
* `ChartPalette.Pen/Alpha`、`ChartText`、`Ink.Text`、`SvDraw.Text` 等助手全部"调用即分配"；全仓只有图标几何做了缓存。

**机制**：因为代码侧 `ArcResources.*` 是快照（R6），自绘控件必须在每次 `Render` 重新取色以响应换肤；
而取到色之后又立即 `new Pen/new SolidColorBrush/new FormattedText`，于是**每帧的稳定分配量 = 图元数 × 控件数**。
`FormattedText` 还包含字体/整段 shaping 成本，是其中最贵的。

**修复**：`ArcDrawingCache`（Pen/Brush 按 (token, alpha, thickness, dash) 键；FormattedText 按 (text, size, brush, weight, maxWidth) 键；
Typeface/Geometry 静态缓存），配合主题版本号整体失效（R6）。

### R3 用布局属性做动画：每次 hover 一次布局风暴

**现象**：14+ 处 `Transitions` 作用于 `Margin/Width/Height/Padding`：

| 位置 | 动画 | 后果 |
| --- | --- | --- |
| `ArcActionVisuals.cs:282-290`（`ArcGlide`，5 个使用点：Tabs/MenuSurface/ButtonGroup/MegaMenu…） | Margin+Width+Height+Opacity | 每帧整棵父面板 measure/arrange |
| `ArcAdvancedVisuals.cs:244-248` + `Travel()` | Margin+Width+Height | 每次 hover 新建 6 个 Transition |
| `ArcSlider.cs:321-348` | `Fill.Width` | 拖动每 move 一次布局 |
| `ArcMegaMenu.cs:1187-1227` | 面板 Width/Height 弹簧 | 每帧重排整个面板 + BoxShadow 重栅格 |
| `ArcGlassTabBar.cs:617-663`、`ArcGlassSegmented.cs:678-706` | lens 的 Width/Height/Canvas.Left/Top | 每帧布局 + `TransformOperations.Parse` |
| `ArcHoldToConfirm.cs:338-357`、`ArcResizablePanels.cs:591`、`ArcCarousel.cs:606-629`、`ArcMorphSelect.cs` 等 | Width/Height/Margin/InvalidateArrange | 每帧文本重排/全 slide 重排 |

**机制**：Avalonia 的 `Transitions` 对布局属性逐帧写入 → 每帧 `InvalidateMeasure/Arrange` → 子树测量排列。
这是"看起来只是高亮动一下，实际整页在重排"的典型。

**修复**：统一改 `RenderTransform`（translate/scale）、自绘或 `Clip`；在 `ArcMotion` 加"布局属性过渡"黑名单/代码审查规则。

### R4 重建型 UI 与零虚拟化：规模一上来就崩

**现象**：C# 侧 `ItemsControl`/`DataTemplate`/虚拟化使用数为 **0**（XAML 仅 7 处 `ListBox`，其中 EmojiPicker 行级虚拟化）；18 处手写 `List<Control>` + `Children.Clear()`；
DataGrid 一次构建约 800 控件；ChatThread、JsonViewer、Combobox、MultiSelect、ChipGroup、PhoneInput 国家列表、
OnboardingChecklist、RichTextEditor 预览全部"状态变 → 重建子树"。

**机制**：每个新控件意味着 measure/arrange/render + 样式解析 + Automation 属性；重建还会丢失焦点/滚动/动画状态，
于是代码里出现大量 `Dispatcher.Post`、缓存字段、`RefreshActiveCell` 之类的补丁。

**修复**：共享行模板控件（`ArcOptionList`）+ 虚拟化列表（`ItemsRepeater`/`VirtualizingStackPanel`）+ 行控件池 + 增量更新。

### R5 可见性与生命周期门控缺失

**现象**：32 个含 `DispatcherTimer` 的文件没有 `OnDetachedFromVisualTree`（其中部分是瞬态 tween，需要甄别，
但 `ArcMarquee` 以外的 `ArcEmptyScene`、`ArcLinkUnfurl`、`ArcMorphIllustration`、`ArcToolCall` 等确有长期循环风险）；
3 个舞台基类只在 attach/detach 管理时钟，`IsVisible` 不参与；Gallery 缓存所有访问过的页面视图
（`GalleryWindow.cs:28-36,559-583`），意味着**离开页面 ≠ 停止动画**（若控件未 detach 或未停表）。

**机制**：Avalonia 的 `IsVisible=false` 不会停止 `DispatcherTimer`；页面切走时若控件被缓存（未 detach）或 timer 未停，
时钟继续跑，`InvalidateVisual` 命中不可见控件（渲染跳过，但 tick + 属性写入成本仍在）。

**修复**：帧时钟统一检查 `IsEffectivelyVisible`；控件 `OnDetachedFromVisualTree` 必须停表（基类记账）；
Gallery 页面缓存加 LRU/离屏暂停。

### R6 主题快照与缓存失效协议缺失

**现象**：`ArcResources.Brush/Color/...` 每次 `TryFindResource`、无缓存、无版本号（`ArcThemeManager.cs:51-70`）；
自绘控件在 `Render` 里取快照，换肤后需要重新 Render 才能更新；部分控件只订阅 `ActualThemeVariantChanged`，
**accent 切换不触发该事件**，因此换色后可能残留旧色（`ArcScrollArea.cs:59`、`ArcImageCompare.cs:63`；suspected，见 §8.C）。

**机制**：模板侧走 `DynamicResource`（自动），代码侧走快照（手动），两套语义混用；作者用两种补偿：
每次 Render 重新查资源（R2 的根源），或 Gallery `RebuildAll()` 整页重建（换肤卡顿的根源）。

**修复**：foundation 提供"资源版本号 + 缓存"（`ArcResources` 内部维护 token→brush 缓存与 `PaletteVersion`，
`Apply` 时自增并广播 `InvalidateVisual`），把"每次查"变成"版本变了才查"。

### R7 没有性能护栏：最贵的路径不在任何自动化里

**现象**：无测试工程、无基准、无性能预算；`--audit` 只验证目录可达性；截图 harness 在开始时
`ArcThemeManager.ReducedMotion = true`（`ShotRunner.cs:28`），因此**所有动画/定时器路径从未在 CI 中被执行过**。

**机制**：最重的代码路径（spinner 常驻 timer、环境动画、逐帧分配）恰好是 reduced-motion 关闭时才会跑的路径，
所以截图全绿也发现不了"进页即 60 fps"。本次审查中 36 条区域 P0 全部来自人工读码，而不是任何自动告警。

**修复**：`--perf` 采样模式（帧时间/Gen0/每帧控件数）、CI 预算、动画路径的 smoke（非 reduced-motion 渲染一帧并统计 timer 数）。

---

## 5. 分区域结论

> 每节只保留"设计特征 + 该区域最重的 3–5 条 + 亮点 + 分片入口"；完整 197 条证据见分片。

### 5.1 基础层 + 图表 + Demo/Gallery（26 条，P0 5）

**设计特征**：基础层 5 个控件基类选择准确（Button 模板化、Card/Badge 内容容器、Icon/Spinner 自绘）；22 个图表 100% 走
`ArcChartBase : Control` + `override Render`，共享 `Crisp`/几何/文本/格式助手，视觉一致性强；页面契约 `ArcPage` 极薄，
反射发现 221 个页面并缓存实例。

**最重的问题**：
1. **每个 `ArcButton` 模板内置隐藏 spinner**（`ArcButton.axaml:43-51`，P0）——G-P0-1；
2. **spinner/图标每帧重建几何与 Pen**（`ArcSpinner.cs:96-104`、`ArcIcon.cs:96-101`，P0/P1）；
3. **`ArcResources` 无缓存**（全仓 348 行调用，86 处位于 Render，P1）；
4. **RaceBar/Realtime 两个 16 ms 常驻 timer**（`ArcRaceBarChart.cs:694`、`ArcRealtimeStream.cs:623`，P0）；
5. **LineChart pointer-move 全量重绘 + tooltip 文本重建**（`ArcLineChart.cs:127-140`，P0）；
6. **Gallery 层**：`FindComponentSection` 首次点击同步 Build 最多 54 页、搜索每键重建侧栏并 8×`ArcPalette.Build`、页面缓存无淘汰（P1）。

**亮点**：token/OKLCH 单一来源；`ArcIcon` 几何缓存 + 缩放放 transform；spinner detach 停表；ChartCore 的像素对齐与共享数学；
Gallery"视图只属于一个 ScrollViewer"的缓存处理。

### 5.2 Inputs + Actions（24 条，P0 3）

**设计特征**：输入类统一 `TemplatedControl` + 模板内嵌原生 `TextBox`（焦点/选区/IME 零成本继承）；
`ArcFieldBase` 收敛变体 class；**该区域 0 个 `Render` override、0 次 `InvalidateVisual`**——没有"自绘化"过度设计，
热点全在可替换的动画/重建策略上，修复成本最低。Actions 的 34 处 `DispatcherTimer` 字面量实际是 **17 个 timer 对象**，
全部一次性/低频，**没有 16 ms 逐帧 timer**（纠正了一个常见误判）。

**最重的问题**：
1. **`ArcGlide` 用 `Margin/Width/Height` + `Transitions` 做滑动高亮**（`ArcActionVisuals.cs:282-290`，被 Tabs/菜单/ButtonGroup/MegaMenu 共用，P0）——每次 hover 25+ 帧布局；
2. **MegaMenu 面板 Size 弹簧 + 每帧 BoxShadow 重栅格**（`ArcMegaMenu.cs:1187-1227`，P0）；
3. **Slider 拖动每次 pointer-move 写 `Fill.Width`**（`ArcSlider.cs:321-348`，P0）；
4. 弹层/建议/选项每次打开或每键重建（Combobox、MenuSurface、ShareAccess、PasswordStrength，P1）；
5. 12 处 `LayoutUpdated` 常驻订阅里强制 `Measure`（P1）。

**亮点**：`ArcFieldBase`/`ArcPickerBase` 变体收敛；Dialog 焦点陷阱；MegaMenu 悬停意图 + 安全三角形（pointer-move 不测量，正确）；
`ArcDismiss` 配对 Dispose；该区域可访问性覆盖全仓最高之一。

### 5.3 Pickers + Feedback + Effects（32 条，P0 6）

**设计特征**：Pickers 采"自绘 surface + 模板外壳"（日历 42 格不是 42 个 Button，换月只改属性，视觉树零增长——本区域最正确的决定）；
`ArcPickerBase` 收敛弹层语义；Feedback 有 `ArcUi` 复用层（150 处调用）；Effects 的 `ArcTween` 是全仓最规范的补间器（每实例、自停、reduced 短路）。

**最重的问题**：
1. **日历每帧 49 份 `FormattedText` + 7 次资源查找 + 42 次 `PushOpacity`**（`ArcCalendarSurface.cs:208-332`，P0）——G-P0-4；
2. **签名板每次 pointer-move 重画全部笔画**（`ArcSignaturePad.cs:333-346`，P0）——G-P0-9；
3. **DataGrid/TreeTable 零虚拟化 + 整表重建**（`ArcDataGrid.cs:1082,1366`，P0）——G-P0-5；
4. **TextShimmer 常驻 16 ms timer，每帧新渐变刷 + 5 stop + LINQ 排序**（`ArcTextShimmer.cs:174`，P0）；
5. **ArcAvatarRing 不确定态常驻 16 ms timer**（`ArcAvatarRing.cs:101`，P0）；
6. **ArcInViewTitle 的 `ArcViewport.Watch` 返回值被丢弃**→ 订阅永不解除、`Arm()` 叠加 watcher（`ArcInViewTitle.cs:285`，P1，一行可修）。

**亮点**：日历/签名的分层架构；EmojiPicker 行级虚拟化 + 二分查找分类；DataGrid 吸顶表头在 ScrollViewer 之外 + `_cellFrames` 增量边框；
Effects 的 reduced-motion 纪律；179 处 Automation。

### 5.4 AdvancedInputs/Actions + Content + Media（33 条，P0 4）

**设计特征**：四个工程代码零 hex；`CardStack`/`SwipeActions` 的手势是 render-only 正面样板（只改 Translate/Rotate/Opacity）；
`ReorderableList` 的 FLIP 确认没有布局读数（代价是逐行 `TransformOperations.Parse`）；`ArcActionButton` 用 `async Task` 状态机。
结构性问题：**三套重复补间器**（`ArcAnim`/`ArcContentTween`/`ArcMediaTween`）+ 四套重复 UI 助手 + 8 处"动画布局属性"。

**最重的问题**：
1. **RangeHistogramSlider 拖动每 move 起不可取消 16 ms timer，每帧全量 min/max + 分桶**（`ArcRangeHistogramSlider.cs:417,650`，P0）；
2. **RichTextEditor 每键全文解析两遍 + 重建预览树（隐藏时仍重建）**（`ArcRichTextEditor.cs:762-780,819-827`，P0）——G-P0-6；
3. **ChatThread 全量重建 + 头像重解码 + 无虚拟化**（`ArcChatThread.cs:135-139,453-536,846-873`，P0）——G-P0-7；
4. **ResizablePanels 拖动每 move 全 pane Measure+Arrange**（`ArcResizablePanels.cs:529,591`，P0）；
5. ElasticSlider 每次 pointer-move 7 次资源查找 + 2–4 份 `FormattedText`（P1）；ScrollArea 每个滚动事件新建渐变遮罩 + timer（P1）。

**亮点**：token 纪律；reduced-motion 覆盖最完整；手势 render-only 样板；键盘替代路径完整（Carousel/CardStack/ImageCompare/HoldToConfirm）；
`ArcActionButton` 的异步状态机。

### 5.5 Special.Glass + Special.Motion + Special.Media（33 条，P0 9）

**设计特征**：**全仓最成熟的自绘范式**——22 个 Motion/Media 控件共用一个 canvas 基类，命中、焦点、键盘、pointer capture、
reduced-motion 全部收敛；reduced 时帧时钟**根本不启动**（不是冻结状态）；`Sm.Noise` 提供确定性素材；`NeedsFrame` 精确收口
（除 3 个默认持续控件外）。

**最重的问题**（全部是"每帧重画复杂静态场景"）：
1. **GravityWell 每帧 36 次 `SampleArt`**（`ArcGravityWell.cs:187-258`）、**CoverFlow 16 次**（`:243-319`）、**PhotoGrid 25 次**（`:404-523`）；
2. **HoloCard hover 每帧 172 个新 brush + `new TransformGroup`**（`ArcHoloCard.cs:762-799`）；
3. **TypewriterTerminal 默认永久 60 fps + 每字符 `FormattedText`**（`ArcTypewriterTerminal.cs:80,150-162`）；
4. **DotGrid 756 点/帧各建 brush**（`ArcDotGrid.cs:174-224`）；
5. **VoiceOrb 默认 Listening 永久 60 fps + 90 段几何**（`ArcVoiceOrb.cs:26,128`）、NowPlaying 常驻；
6. Glass 用 `Width/Height` 做 lens 动画 + 每帧 `TransformOperations.Parse`（P1）；LinkUnfurl 16 ms shimmer 无 detach（P1）。

**亮点**：统一舞台基类；reduced-motion 真降成本；确定性程序化素材；SandText 用 `WriteableBitmap` + 预光栅化 mask；
有限循环收口；每页主动标注近似实现。

### 5.6 Special.AIWidgets + Explainers（25 条，P0 7）

**设计特征**：两套共享视觉助手（`SpecialVisuals.Sv`/`ExplainInk.Ink`）统一 token 取色、文本、卡片、弧线、图标、缓动；
11 幅图解全部 `ArcExplainer`，画面是"归一化相位的纯函数"，reduced 时钉在 poster phase；
widget 是 `Panel` 组合 + 内层自绘子控件；生命周期配对基本正确。

**最重的问题**：
1. **`ArcExplainer` 33 ms 时钟无条件整场景重绘**，其中 TimeDial/DateReel/MultiRegionFailover 根本不使用 phase（`_ = phase`，30 Hz 纯浪费，`ArcExplainer.cs:92-106`）；
2. **`ExplainInk.Text` 每个标签每帧 `new Typeface`/`FormattedText` + 先 `TryFindResource` 取字体**（DateReel 峰值约 55 标签/帧，`ExplainInk.cs:80-117`）；
3. **MorphLoader 16 ms 时钟完成态不停**（`ArcMorphLoader.cs:121-133`，7 实例页 ≈420 次 `InvalidateVisual`/s）；
4. **InkFluid 150 粒子每帧各 `PushTransform`+`PushOpacity`+渐变椭圆，另每帧 2 个 8-stop `RadialGradientBrush`**（`ArcInkFluid.cs:404-431`）；
5. **ActivityRings 520 ms tween 内每帧 168 个 `StreamGeometry` + 168 个 `Pen`**（`ArcActivityRings.cs:545-552`）；
6. DataFlow/PaymentFlow/ReasoningTree 每帧重建几何与每帧 LINQ 排序（P1）。

**亮点**：`Ink.IconCache`（全区域唯一做对的缓存）；phase 纯函数模型与 `bool` 返回值接口；集中式 reduced-motion；
完整键盘语义（BlackHole 支持 Ctrl+Z，TimeDial 支持 PageUp/Home/End）；有界数据（StretchRefresh 裁剪到 6 条）。

### 5.7 Blocks×6（24 条，P0 2）

**设计特征**：111 个块全部是 `ArcItemPage` + `ArcDocLayout`，文档结构零重复；颜色纪律全仓最好（0 处硬编码 hex）；
reduced-motion 覆盖 9 个动画/播放 timer 中的 8 个；示例数据确定性（mulberry32 固定种子）且无静态集合增长；
15 个 DataStage 全部支持键盘。页面构建规模 120–220 个视觉节点，嵌套 8–10 层。
**结构性缺陷**：**`DemoUi` 已存在但 0 调用**，111 个块长在 **6 套 `*Kit.cs`**（4,930 行 / 300 helper / 30 个重名 helper / 3 套宽度策略）；
半径/间距/字号大量是字面量（242 处 `new CornerRadius(数字)`、625 处 `Thickness(数字)`），token 改动不传播；
`ArcCard` 复用 0 次，手写 `Border`/`StackPanel`/`Grid`/`TextBlock` 共 1,832 个。

**最重的问题**：
1. **CustomerGalaxy 2400 账户：每次指针移动 `Layout()`（2400 次分组 LINQ）+ 拖拽每次 move 重绘 2400 个 ellipse**（`ArcGalaxyStage.cs:98-132,264-297`，P0）——注意注释仍写"1,200"，实际翻倍；
2. **DotGlobe 1620 点：每点一次 `Ink.Alpha`（字典查找 + 新 `SolidColorBrush`）+ 拖拽每 move 全量重绘**（`ArcDotGlobe.cs:206-263`，P0）；
3. Terrain 168 格 × 3 几何/柱 × 4 brush，Candle 120 根逐根 `new Pen`，Horizon 84 个 band 几何（P1）；
4. `DataDraw`/`Ink` 零缓存：139 处 `Ink.<prop>`、59 处文本绘制每次重绘都查资源/建 `FormattedText`（P1）；
5. InboxSidebar 任一交互重建三栏约 150–200 控件（`InboxSidebarPage.cs:562-568`，P1）；MediaPlayer 500 ms timer 随构建启动、每 tick 重建 seek 子树（P1）；
6. MetricsDashboard 每次 Rebuild 生成 30×180 = 5400 个 DateTime + 5400 个字符串（`MetricsDashboardPage.cs:111-255`，P1）。

**亮点**：页面契约与文档骨架统一；颜色纪律；确定性数据；键盘与 polite live region；自绘控件默认静态帧（无 60 fps 常驻）；
17 个 timer 中没有页面构建期启动的动画（除 MediaPlayer 的模拟播放）。

---

## 6. 优化路线图

> 原则：先修"乘数"（隐藏 timer、绘制缓存、资源缓存），再修"单点"；先做低风险局部改动，再做基础设施；
> 每一阶段都能独立验证收益，不要求一次性重构。

### 阶段 0：快速收益（1–3 天，低风险）

| # | 动作 | 覆盖问题 | 预期收益 | 风险 |
| --- | --- | --- | --- | --- |
| 0-1 | `ArcSpinner` 改为"`Loading=true` 才创建/启动 + 共享 ticker"；或至少附加时检查 `IsVisible` | G-P0-1 | 移除 Gallery 侧栏/页面内数千次/秒空转 tick；CPU 空闲占用显著下降 | 低（模板改动 + 状态机微调） |
| 0-2 | `ArcResources` 加 token 缓存 + `PaletteVersion`，`Apply` 时自增并广播 | R2/R6、P1-G | 消除 Render 内 89 次/帧的 `TryFindResource`；换肤正确性统一 | 低 |
| 0-3 | 新增 `ArcDrawingCache`（Pen/Brush/`FormattedText`/`Typeface`/`Geometry`），先接入 ChartCore、日历、ElasticSlider、Special 文本助手 | G-P0-3、P1-D/F | 77 个 Render 中约 291 次/帧分配点可去掉大半；GC 压力下降 | 低–中 |
| 0-4 | 把 5 处最热的布局属性动画改 `RenderTransform`（ArcGlide、AdvancedGlide、Slider fill、GlassTabBar、HoldToConfirm） | G-P0-8 | 每次 hover/拖动少 25–42 帧布局 pass | 中（需回归视觉） |
| 0-5 | 修 3 个确定性泄漏/生命周期：`ArcViewport.Watch` 返回值、ActionSwap/CopyButton/Tooltip detach、LinkUnfurl shimmer | P1-A/M1 | 消除跨页残留订阅与 timer | 低 |
| 0-6 | 去掉 12 处 `LayoutUpdated` 回调里的强制 `Measure`，改 `SizeChanged` + 显式定位 | P1-I | 布局动画期间从 60 次/秒降到 0 | 低–中 |
| 0-7 | DataGrid 选中/排序走 `_cellFrames` 增量（已有基础），先不做虚拟化 | G-P0-5（部分） | 每次点选从 800 控件重建降到局部更新 | 中 |
| 0-8 | Gallery：搜索防抖 + `ArcPalette.Build` 结果缓存 + 页面缓存 LRU | P1-H | 搜索每键从 8×81 资源写降到 0；内存不再随访问页数单调增长 | 低 |

### 阶段 1：基础设施（1–3 周）

| # | 动作 | 覆盖问题 | 说明 |
| --- | --- | --- | --- |
| 1-1 | foundation 新增 `ArcAnimationClock`（单 ticker + Job + reduced/visibility 门控 + `CompositionTarget.Rendering`） | R1、G-P0-1/2/10 | 替换 11 套实现；保留 `ArcMotionCanvas`/`ArcExplainer` 的 API 形态作为适配层 |
| 1-2 | foundation 新增 `ArcThemedControl`/`ArcDrawContext`：主题版本 + 缓存失效 + 静态层 `DrawingGroup` | R2/R6、S3 | 自绘控件统一"何时重画/缓存什么"；日历/图表/Special 静态层受益 |
| 1-3 | 共享列表层 `ArcOptionList`/`ArcVirtualList`（`ItemsControl` + 行 `ControlTheme` + 选择状态 + 虚拟化） | R4、G-P0-5/7/12 | 迁移 Combobox/MultiSelect/ChipGroup/RadioGroup/菜单行/DataGrid/ChatThread/PhoneInput |
| 1-4 | 解析与渲染增量：RichTextEditor 单次解析 + 增量预览；ChatThread 增量追加；RangeHistogramSlider 缓存分桶 | G-P0-6/7、P1-B | 三处"交互一次 = 全量重算"的主干 |
| 1-5 | BlocksKit 收敛：6 套 Kit → 1 套 + 主题化变量；`DataStageBase`（脏区域/相机缓存/文本缓存） | BK-01、BD-04 | 修复成本从 ×6 降为 ×1；Blocks 全部自绘控件受益 |
| 1-6 | Special 静态层位图缓存（SampleArt/粒子/Glass 材质按 seed+尺寸+主题缓存） | G-P0-11 | 单帧图元数降一个数量级 |
| 1-7 | 大数据压力修复：DataGrid 虚拟化、EmojiPicker 全虚拟化、JsonViewer 懒展开 | G-P0-5/7 | 决定"能否上真实数据量" |

### 阶段 2：护栏（持续）

* **性能预算**：每页构建时间、帧时间 P95、每帧分配字节、常驻 timer 数。建议阈值：空闲页面 0 timer、动画页面 P95 < 16.7 ms、每帧分配 < 50 KB。
* **CI**：`--smoke` 打开 reduced-motion（保持截图稳定），另加 `--perf` 模式**关闭** reduced-motion 渲染 N 帧，采集 timer 数与帧时间；对 Top 10 页面做趋势对比。
* **lint/约定**（可先用 Roslyn analyzer 或 code review checklist）：
  * 禁止在 `Render`/`Tick`/pointer handler 中 `new Pen`/`new SolidColorBrush`/`new FormattedText`/`ArcResources.*`（白名单除外）；
  * 禁止 `Transitions` 作用于 `Width/Height/Margin/Padding`；
  * 自绘控件必须有 `AffectsRender` 或明确的失效调用点；
  * 每个 `DispatcherTimer` 必须有 detach 停表与 reduced-motion 分支。
* **回归样例**：DataGrid 1000 行、ChatThread 500 条、CustomerGalaxy 2400 点、日历 100 次 hover/s、页面切换 50 次（验证 timer 归零）。

### 6.1 预期收益（粗估，需实测确认）

| 修复 | 影响面 | 粗估 |
| --- | --- | --- |
| G-P0-1 隐藏 spinner | Gallery 全局 | 移除数千 tick/s；空闲 CPU 从"持续背景负载"降到接近 0 |
| G-P0-3 + ArcResources 缓存 | 77 个 Render | 每帧分配点减少约 60–80%；GC Gen0 频率下降 |
| G-P0-4 日历缓存 | 所有日期控件 | 单帧文本布局 49 → 0（缓存后仅尺寸/主题变化时重建） |
| G-P0-5/7 虚拟化 | 表格/聊天/列表 | 100×8 表格 800 → ~80 控件；1000 行时构建时间近似线性不变 |
| G-P0-8 布局动画改 transform | hover/选择路径 | 每次交互布局 pass 25–42 → 0 |
| 共享帧时钟 | 全仓动画 | 同页 N 个动画从 N 个 60 Hz 定时器 → 1 个；可见性门控后离屏归零 |

---

## 7. 如何验证（测量方案）

> 当前所有结论是静态推断。下面是修复前后都应执行的四个实验，成本低、能直接证伪本报告的关键论断。

### 实验 1：常驻 timer 计数（验证 G-P0-1 / G-P0-2 / R1）

* 反射遍历 `GalleryWindow` 的视觉树，统计 `DispatcherTimer` 字段（`IsEnabled == true`）与每秒 `Tick` 次数；
* 预期（修复前）：打开默认页即有 ≥59 个 enabled timer（侧栏按钮 spinner）；访问含 Special 控件页后 +1–3 个；
* 修复后预期：空闲时 0 个。

### 实验 2：帧时间与分配（验证 G-P0-3 / G-P0-11）

* 给 Gallery 加 `--perf <page>` 模式：关闭 reduced-motion，`RenderTargetBitmap.Render` 连续 120 帧，记录 `Stopwatch` 每帧耗时；
* 同时用 `dotnet-counters monitor --counters System.Runtime` 观察 `gen-0-gc-count` / `alloc-rate`；
* 对比：`--perf calendars`（日历 hover 循环）、`--perf customer-galaxy`（拖拽）、`--perf glass-menu`。

### 实验 3：输入延迟（验证 G-P0-8 / G-P0-9）

* 用 `DispatcherTimer`/`Stopwatch` 在 pointer-move handler 前后打点，统计单次事件到下一帧的延迟；
* 对比 ArcGlide hover 序列、Slider 拖动、签名板连续笔画。

### 实验 4：规模压力（验证 R4）

* DataGrid：100 / 500 / 1000 行；ChatThread：50 / 200 / 500 条；记录构建时间、控件数、内存；
* 预期：修复前随行数线性增长（每行 ~8 控件），修复后虚拟化后近似常数。

### 指标与阈值建议

| 指标 | 采集方式 | 建议阈值 |
| --- | --- | --- |
| 空闲 enabled timer 数 | 反射遍历 | 0（动画页面按需） |
| 帧时间 P95 | Stopwatch per frame | < 16.7 ms（60 fps 动画页） |
| 每帧分配 | `GC.GetAllocatedBytesForCurrentThread` 差分 | < 50 KB |
| 页面构建时间 | `Stopwatch` around `Build` | < 50 ms（120–220 节点） |
| 1000 行 DataGrid 构建 | 同上 | < 200 ms（虚拟化后） |
| 换肤到稳定 | 从 `Apply` 到首帧 | < 100 ms |

---

## 8. 附录

### 8.A 量化扫描

**全仓模式计数**（字面统计，含少量注释，量级参考）：

| 模式 | 次数 | 模式 | 次数 |
| --- | --- | --- | --- |
| `InvalidateVisual` | 402 | `new Pen(` | 80 |
| `DispatcherTimer` | 173（68 个文件） | `new StreamGeometry` | 46 |
| `override void Render` | 80（77 个文件） | `new FormattedText` | 43 |
| `new Transitions` | 30+ | `BoxShadow` | 55 |
| `ArcResources.` | 348 行 | `BlurEffect` | 4 |
| `PropertyChanged` | 69 | `CompositionTarget` | **0** |
| `ItemsControl`/`DataTemplate`（C#） | **0**（XAML 仅 7 处 `ListBox`） | `{DynamicResource}` | 1251 |
| `AutomationProperties` | 703 | `OnDetachedFromVisualTree` | 2 处显式事件 + 若干 override |

**逐工程热点**（`Render` / `InvalidateVisual` / `new Pen` / timer / `ArcResources`）：

| 工程 | Render | Inv | Pen | Timer | Res | 工程 | Render | Inv | Pen | Timer | Res |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Foundation | 2 | 1 | 2 | 2 | 2 | Charts | 20 | 72 | 52 | 6 | 25 |
| Inputs | 1 | 1 | 1 | 6 | 21 | Actions | 0 | 0 | 0 | 34 | 4 |
| Feedback | 3 | 7 | 3 | 17 | 53 | Pickers | 6 | 16 | 13 | 6 | 62 |
| AdvancedInputs | 3 | 26 | 4 | 2 | 45 | AdvancedActions | 0 | 0 | 0 | 6 | 3 |
| Effects | 0 | 0 | 0 | 14 | 8 | Content | 1 | 2 | 2 | 5 | 14 |
| Media | 3 | 3 | 7 | 9 | 9 | Special.Glass | 4 | 10 | 6 | 18 | 4 |
| Special.Motion | 2 | 74 | 59 | 2 | 5 | Special.Media | 1 | 45 | 59 | 2 | 5 |
| Special.AIWidgets | 12 | 45 | 37 | 14 | 9 | Special.AIExplainers | 1 | 16 | 48 | 2 | 4 |
| Blocks.Data | 15 | 72 | 0 | 8 | 16 | Blocks.Team | 2 | 5 | 7 | 2 | 17 |
| Blocks.App | 0 | 0 | 0 | 4 | 3 | Blocks.Commerce | 1 | 2 | 2 | 8 | 14 |
| Blocks.Marketing | 3 | 5 | 1 | 6 | 15 | Blocks.AI | 0 | 0 | 0 | 0 | 6 |
| Gallery | 0 | 0 | 0 | 0 | 4 | Demo | 0 | 0 | 0 | 0 | 0 |

**静态热点文件 Top 15**（启发式评分：Render×20 + Pen/几何/文本×3 + Invalidate + Timer×5 + 资源/LINQ×0.5，仅用于排序，不等于实测帧耗时）：

| # | 文件 | 分数 | Render | Pen | Inv | Timer |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `Pickers/Controls/ArcColorSurfaces.cs` | 74 | 2 | 7 | 8 | 0 |
| 2 | `Special.AIWidgets/Controls/ArcAgentStatus.cs` | 68 | 2 | 8 | 3 | 0 |
| 3 | `Special.AIWidgets/Controls/ArcAgentPlan.cs` | 58 | 2 | 2 | 2 | 1 |
| 4 | `Special.AIWidgets/Controls/ArcAgentTimeline.cs` | 57 | 1 | 8 | 9 | 0 |
| 5 | `Charts/Controls/ArcRaceBarChart.cs` | 56 | 1 | 3 | 16 | 1 |
| 6 | `Special.Motion/Controls/Special.MotionUi.cs` | 54 | 1 | 2 | 9 | 1 |
| 7 | `Charts/Controls/ArcBrushChart.cs` | 52 | 1 | 8 | 5 | 0 |
| 8 | `Feedback/Controls/ArcActivityHeatmap.cs` | 48 | 1 | 3 | 7 | 0 |
| 9 | `Charts/Controls/ArcRealtimeStream.cs` | 48 | 1 | 4 | 11 | 1 |
| 10 | `Special.Media/Controls/ArcPageCurl.cs` | 48 | 0 | 14 | 4 | 0 |
| 11 | `Charts/Controls/ArcSankeyFlow.cs` | 48 | 1 | 2 | 7 | 0 |
| 12 | `Blocks.Team/Controls/ArcCohortChart.cs` | 48 | 1 | 5 | 2 | 0 |
| 13 | `Special.AIWidgets/Controls/ArcContextWindow.cs` | 47 | 1 | 4 | 3 | 1 |
| 14 | `Special.AIExplainers/Controls/ArcExplainer.cs` | 44 | 1 | 2 | 12 | 1 |
| 15 | `Special.AIWidgets/Controls/ArcStretchRefresh.cs` | 44 | 1 | 3 | 5 | 2 |

> 说明：`Blocks.Data` 的单文件 Pen 计数为 0 是因为它通过 `DataDraw.Ink` 间接创建；真实绘制量见 §5.7。
> `Special.MotionUi.cs`/`ArcExplainer.cs`/`SpecialUi.cs` 是基类，分数代表它们服务的 11 个控件。

### 8.B 分片索引与问题总数

| 分片 | 主题 | 问题数 | P0/P1/P2 |
| --- | --- | --- | --- |
| `docs/perf/01-foundation-charts.md` | 基础层 / 图表 / Demo / Gallery | 26 | 5 / 12 / 9 |
| `docs/perf/02-inputs-actions.md` | Inputs / Actions | 24 | 3 / 12 / 9 |
| `docs/perf/03-pickers-feedback-effects.md` | Pickers / Feedback / Effects | 32 | 6 / 16 / 10 |
| `docs/perf/04-advanced-content-media.md` | AdvancedInputs/Actions / Content / Media | 33 | 4 / 21 / 8 |
| `docs/perf/05-special-glass-motion-media.md` | Special.Glass / Motion / Media | 33 | 9 / 18 / 6 |
| `docs/perf/06-special-ai.md` | Special.AIWidgets / Explainers | 25 | 7 / 12 / 6 |
| `docs/perf/07-blocks.md` | Blocks×6 | 24 | 2 / 9 / 13 |
| **合计** | | **197** | **36 / 100 / 61** |

### 8.C 核实状态与纠偏

**已回读源码复核的关键结论**（Lead 抽查）：

* `ArcButton.axaml:43-51` 确实在模板中常驻 `ArcSpinner`（`IsVisible=False`），`ArcSpinner.OnAttachedToVisualTree` 不看可见性即建 16 ms timer —— 结论成立（G-P0-1）。
* `ArcSpinner.Render`、`ArcIcon.Render`、`ArcCalendarSurface.Render`、`ArcElasticSlider.Render` 的每帧分配与资源查找 —— 行号与语义成立。
* `ArcSignaturePad.Render` 遍历全部笔画 + `Text()` 每次 `new FormattedText` —— 成立（G-P0-9）。
* `ArcDataGrid.cs:1082-1086` 逐行 `StackPanel`、`:1366` 整表重建 —— 成立（G-P0-5）。
* `ArcGlide`/`ArcAdvancedGlide` 用 `Margin/Width/Height` + `Transitions` —— 成立；但**纠正**：`ArcActionVisuals` 版本的 `Transitions` 是 `_motion ??=` 缓存实例（每次只是重新赋值），
  真正每次 new 的是 `ArcAdvancedVisuals.Travel()`（`ArcAdvancedVisuals.cs:280-331`）。性能主因是"动布局属性"，不是"分配 Transitions"。
* `ArcTypewriterTerminal` 默认 `_playing=true` + 时间回绕 → 永久 60 fps；`HoloCard` 每帧 `new TransformGroup` + 2 个 foil 重绘 —— 成立。
* Blocks：`ArcGalaxyStage` 2400 账户、`ArcDotGlobe` 1620 点、`DataDraw` 零缓存 —— 成立；页面注释里"1,200"与实际 2400 不一致（注释 bug）。
* **纠正**：Actions 区域"34 处 `DispatcherTimer`"实为 17 个 timer 对象（16 个构造期常驻 + Tooltip 每次 hover 新建），无 16 ms 逐帧 timer；
  BRIEF 中的 6/17/14 是按"动画循环主体"计数，与字面量不同（分片已说明）。
* **纠正**：Pickers/Feedback/Effects 的 timer 字面量为 18 处（3/8/7），不是 33 处；仍然是本区域主要生命周期风险点。

**待确认（suspected，建议实测）**：

1. `IsVisible=false` 的视觉树子控件仍会触发 `OnAttachedToVisualTree`（Avalonia 语义上成立，但本报告未用运行时验证）——影响 G-P0-1 的"隐藏 spinner 仍计时"；
2. 自绘控件在 accent 切换（非明暗切换）后颜色残留：`ActualThemeVariantChanged` 不触发，`ArcScrollArea`/`ArcImageCompare` 等只订阅该事件（`ArcScrollArea.cs:59`、`ArcImageCompare.cs:63`）；
3. 固定宽度（1000 px 外壳、`ArcCohortChart` 860）在窗口 < 1412 px 时的裁切与额外测量轮次（Blocks BP-06/BD-11）；
4. Gallery 首次点击无独立页的 catalog 项时，同步构建页面的真实卡顿时长（静态上限 54 页，未计时）；
5. 各"每帧 N 次"数字均为代码路径推算，未用 profiler 采集真实帧时间/分配。

### 8.D P0 速查修复清单

| ID | 第一动作 | 文件 |
| --- | --- | --- |
| G-P0-1 | spinner 懒创建/共享时钟 | `Arc.Avalonia/Themes/ArcButton.axaml`、`Controls/ArcSpinner.cs` |
| G-P0-2 | 默认暂停 + 可见性门控 | `Special.Motion/…/ArcTypewriterTerminal.cs`、`ArcVoiceOrb.cs`、`ArcNowPlaying.cs`、`ArcMorphLoader.cs`、`ArcSandText.cs`、`ArcDotGrid.cs`、`Pickers/…/ArcAvatarRing.cs` |
| G-P0-3 | `ArcDrawingCache` + `ArcResources` 缓存 | `Theme/ArcThemeManager.cs`、`Charts/Core/ChartCore.cs`、`Pickers/…/ArcCalendarSurface.cs`、`AdvancedInputs/…/ArcElasticSlider.cs` |
| G-P0-4 | 日历文本/格子缓存、hover 分层 | `Pickers/Controls/ArcCalendarSurface.cs` |
| G-P0-5 | 行虚拟化 + 增量选择 | `Feedback/Controls/ArcDataGrid.cs`、`ArcTreeTable.cs` |
| G-P0-6 | 单次解析 + 增量预览 + 隐藏跳过 | `Content/Controls/ArcRichTextEditor.cs`、`ArcMarkdown.cs` |
| G-P0-7 | 消息项复用/增量/虚拟化 | `Content/Controls/ArcChatThread.cs` |
| G-P0-8 | 布局动画改 transform | `Actions/Controls/ArcActionVisuals.cs`、`AdvancedActions/…/ArcAdvancedVisuals.cs`、`Inputs/…/ArcSlider.cs`、`Special.Glass/…/ArcGlassTabBar.cs`、`Media/…/ArcHoldToConfirm.cs` |
| G-P0-9 | 已完成层缓存 + 拖动层分离 | `Pickers/…/ArcSignaturePad.cs`、`Charts/…/ArcLineChart.cs`、`Pickers/…/ArcColorSurfaces.cs`、`Media/…/ArcImageCompare.cs` |
| G-P0-10 | 环形缓冲 + 合并渲染 | `Charts/Controls/ArcRaceBarChart.cs`、`ArcRealtimeStream.cs` |
| G-P0-11 | 静态层位图缓存 | `Special.Media/…/ArcGravityWell.cs`、`ArcCoverFlow.cs`、`ArcPhotoGrid.cs`、`Special.Glass/…/ArcHoloCard.cs`、`Special.AIWidgets/…/ArcInkFluid.cs`、`ArcActivityRings.cs` |
| G-P0-12 | 行构建去重 + 复用 | `Inputs/…/ArcCombobox.cs`、`Actions/…/ArcMegaMenu.cs`、`ArcShareAccess.cs`、`Inputs/…/ArcPasswordStrength.cs`、`Content/…/ArcOnboardingChecklist.cs` |

---

*报告生成：2026-10-25。数据来源：7 份分片 + Lead 全仓扫描；所有行号对应当前工作区文件。*
