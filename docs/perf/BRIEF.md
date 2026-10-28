# 性能审查任务简报（供分析小组共用）

本目录是一次**只读静态分析**的产物。最终由 Lead 汇总为 `docs/04-design-and-performance-review.zh.md`。
每位分析员只写自己那一个分片文件，**绝不修改 `src/**`、不修改别人的分片、不运行构建/截图**。

## 1. 被分析对象

`D:\Code\VibeCoding\Arc.Avalonia`：Arc UI（React 设计系统）的 Avalonia 12 / .NET 10 复刻。
588 个 `.cs`（约 5.9 MB）+ 112 个 `.axaml` 主题文件，24 个工程。没有测试工程，也没有基准测试——
因此本次结论是**静态分析 + 代码路径推理**，必须写清触发条件与放大因子，禁止把推测写成实测。

关键约定（`CONVENTIONS.md`）：

* 交互控件以 `TemplatedControl` / `ContentControl` / `Button` 为基类，变体用 CSS 风格 class，模板在
  `ControlTheme` 里，颜色一律 `{DynamicResource ...}`；自绘控件继承 `Control` 并 override `Render`。
* token 常量在 `ArcTokens`，代码取值走 `ArcResources.Brush(key)`；`ArcThemeManager.Apply` 整体替换
  merged dictionary 来换肤。
* 动效有两个流派：Avalonia `Transitions`/`Animation`，以及**每控件一个 `DispatcherTimer` 手动插值 +
  `InvalidateVisual()`**。后者是本仓库数量最多的模式，也是性能审查重点。

## 2. 已核实的基线事实（可直接引用，不必重复验证）

| # | 事实 | 位置 |
| --- | --- | --- |
| B1 | `ArcResources.Brush/Color/Double/...` 每次都 `Application.Current.TryFindResource`，无缓存 | `src/Foundation/Arc.Avalonia/Theme/ArcThemeManager.cs:51-70` |
| B2 | `ArcSpinner` 每个实例在附加到视觉树时创建一个 16 ms `DispatcherTimer`，每帧重建 `StreamGeometry` 和 `Pen` | `src/Foundation/Arc.Avalonia/Controls/ArcSpinner.cs:53-105` |
| B3 | `ArcIcon` 用 `ConcurrentDictionary` 缓存 `Geometry.Parse` 结果（好），但每次 `Render` 都 `new Pen`，且 `Foreground` 为空时每帧调用 `ArcResources.Brush` | `src/Foundation/Arc.Avalonia/Controls/ArcIcon.cs:17,73-102` |
| B4 | `ArcThemeManager.Apply` 先 Remove 再 Add 整个 palette 字典 → 全树 DynamicResource 重新解析 | `src/Foundation/Arc.Avalonia/Theme/ArcThemeManager.cs:24-38` |
| B5 | `ArcMotion.Smooth(property)` 每次调用都 `new Transitions { new DoubleTransition ... }` | `src/Foundation/Arc.Avalonia/Motion/ArcMotion.cs:41-42` |

全仓模式计数（`rg` 风格字面统计，含注释，量级参考）：
`InvalidateVisual` 402、`DispatcherTimer` 173、`override void Render` 80、`new Pen(` 80、
`new StreamGeometry` 46、`new FormattedText` 43、`BoxShadow` 55、`BlurEffect` 4、
`new SolidColorBrush` 48、`ArcResources.` 约 420、`PropertyChanged` 69、`Task.Run` 5、`async void` 6。

各工程热点分布（`Timers` / `Invalidate` / `RenderOverrides` / `newPen`）：
Charts 6/72/20/52；Special.Motion 2/74/2/59；Special.Media 2/45/1/59；
Special.AIWidgets 14/45/12/37；Special.AIExplainers 2/16/1/48；Blocks.Data 8/72/15/0；
Pickers 6/16/6/13；Feedback 17/7/3/3；Actions 34/0/0/0；
AdvancedInputs 2/26/3/4；AdvancedActions 6/0/0/0；Media 9/3/3/7；Content 5/2/1/2；
Inputs 6/1/1/1；Effects 14/0/0/0；Special.Glass 18/10/4/6。
（Blocks 其它组、Foundation、Gallery/Demo 见各自分片。）

## 3. 要回答的两个问题

### Q1 组件设计分析（每个区域都要写）

* 控件类型分布：`TemplatedControl` / `ContentControl` / `Button` / 裸 `Control` 自绘的比例与选择是否合理。
* 状态与组合：属性 + `Classes` 变体、模板部件（`OnApplyTemplate`/`PART_*`）、`StyledProperty` vs 普通字段、
  派生控件 vs 组合控件、是否复用 foundation 控件。
* 动效与交互：用的哪条路径（Transitions / Animation / 手写 timer / 弹簧采样）、是否有 reduced-motion 分支、
  指针/键盘/焦点语义。
* 可访问性：AutomationProperties、键盘可达、焦点可视化。
* 一致性：是否都走 token（没有硬编码 hex）、主题切换是否真的响应。
* 设计上做得好的地方（要写，别只挑毛病）。

### Q2 性能问题（重点）

按下面的分类法逐类检查，给每个问题一个分类码：

| 码 | 含义 | 典型信号 |
| --- | --- | --- |
| R1 | 每帧/每次绘制分配 | `Render`、`OnRender`、`Tick`、pointer-move 里 `new Pen/Brush/StreamGeometry/FormattedText/List/LINQ` |
| R2 | 绘制期资源查找 | `Render`/`Tick` 里 `ArcResources.*`、`TryFindResource`、`FindResource` |
| R3 | 自绘开销 | 大量 `DrawGeometry`/`DrawText`、复杂 path、`PushClip`/`PushOpacity` 嵌套、逐点画线 |
| L1 | 布局抖动 | pointer-move 里改 `Width/Height/Margin/Padding`、`InvalidateMeasure` 循环、Measure 中分配 |
| L2 | 视觉树膨胀 | 大列表/表格逐项 new 控件（无虚拟化）、重复模板层、每项一个 timer |
| T1 | 定时器泛滥 | 每实例一个 16 ms `DispatcherTimer`、不 Stop、在 Tick 里做重活、N 个实例叠加 |
| T2 | 动画实现代价 | 每次调用 new `Transitions`/`Animation`、`KeyFrame` 动画跑在不必要属性上、手写弹簧采样 |
| M1 | 事件/订阅泄漏 | `PropertyChanged +=` / `event +=` 无解绑、静态事件、attach/detach 不配对、闭包捕获控件 |
| M2 | 无界缓存/集合 | `static Dictionary/List` 无清理、日志/历史列表无限增长 |
| S1 | 启动/反射 | 反射发现页面、`Activator`、`GetTypes`、启动时构建全部页面/图标 |
| S2 | 文本排版 | 每帧 `new FormattedText`/`TextLayout`、每帧测量文本 |
| G1 | 合成/特效 | `BoxShadow`、`BlurEffect`、多层半透明、每帧改 `Opacity` 触发重绘 |
| P1 | 输入热路径 | `PointerMoved`/`PointerWheelChanged`/`ScrollChanged` 里做布局、绘制、分配、资源查找 |

严重度：

* **P0**：在 60 fps 或高频输入路径上必然发生，且成本随实例数/数据量线性放大（典型：每控件 16 ms timer、每帧
  重建几何、pointer-move 里全量重算）。会直接掉帧。
* **P1**：交互/切换路径上明显，或条件触发（大列表、深树、频繁换肤、长会话）。用户可感知但非持续。
* **P2**：清洁度、潜在风险、可维护性；当前规模下影响小。

## 4. 证据规则（硬性）

1. 每条结论必须给 `相对路径:行号`（如 `src/Components/Arc.Avalonia.Charts/Controls/ArcLineChart.cs:88`），
   行号必须是你用 read/grep 实际看到的当前文件行号。
2. 引用代码 ≤ 8 行，只引关键行；不要整段粘贴。
3. 写清三件事：**触发条件**（什么时候执行：每次 Render？每次 pointer move？每次 Tick？）、
   **放大因子**（× 控件实例数？× 数据点数？× 每秒 60 次？）、**修复方向**（具体到 API：缓存 Pen 为
   static readonly、共享 `DispatcherTimer`/用 `CompositionTarget.Rendering`、`ArcResources` 加缓存、
   `AffectsRender` 增加属性、改虚拟化、缓存 `FormattedText` 等）。
4. 没验证的写「待确认（suspected）」，不要编造行号或行为。
5. 同一个问题在多文件重复出现时，合并为一条，列出 2–4 个代表位置 + "另有 N 处同类"。

## 5. 分片文件模板

写入 `docs/perf/<你的文件名>.md`，严格用这个结构：

```markdown
# <区域名>：组件设计与性能分析

## 0. 范围与覆盖
- 覆盖工程 / 文件数 / 实际完整阅读的文件清单
- 方法：哪些文件精读，哪些 grep 扫描

## 1. 组件设计特征
（按 Q1 的要点写，分小节；给代表文件:行）

## 2. 性能问题清单
| ID | 分类 | 严重度 | 位置 | 问题一句话 | 放大因子 |
| --- | --- | --- | --- | --- | --- |
（ID 形如 CH-01、IN-01，前缀自定但本分片内唯一）

## 3. 逐条说明
### CH-01 <标题>（P0）
- 位置：`path:line`
- 证据：
  ```csharp
  ...关键行...
  ```
- 触发/放大：
- 影响：
- 修复建议：

## 4. 该区域设计评价与修复优先级
- 做得好的：
- 最该先修的 3 件事：
- 设计层面的结构性问题（不是单点 bug）：
```

## 6. 交付

1. 把分片文件写完整（目标 250–500 行，宁精不滥；P0/P1 必须逐条有证据）。
2. 用 `send_message` 给 `lead` 一段 ≤ 300 字总结：分片路径、覆盖了多少文件、P0/P1 各几条、
   最关键的 5 条（文件:行 + 一句话）。不要粘贴全文。
3. 只写自己的分片文件，不碰 `src/**`，不碰 `docs/perf/` 下别人的文件，不跑 `tools/build.ps1` 或截图。
