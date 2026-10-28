# Special.AIWidgets + Special.AIExplainers：组件设计与性能分析

## 0. 范围与覆盖

- 覆盖两个工程，共 53 个 `.cs`；其中 6 个是 `obj/Debug/net10.0/` 下生成文件（AssemblyInfo / AssemblyAttributes / GlobalUsings），人工源码 **47 个**：
  - `src/Special/Arc.Avalonia.Special.AIWidgets`：11 个 Special 控件 + 共享绘制助手 `Controls/SpecialVisuals.cs` + 11 个演示页（`Pages/`）。
  - `src/Special/Arc.Avalonia.Special.AIExplainers`：12 个 Special 控件（抽象基类 `ArcExplainer` + 11 幅图解）+ 共享绘制助手 `Controls/ExplainInk.cs` + 11 个演示页。
  - 即任务口径的「22 个 AI 主题 Special 控件」（11 widget + 11 explainer），另有 2 个共享视觉助手、22 个页面。
- 本分片实测（grep 计数，与 BRIEF §2 的区域基线一致）：`DispatcherTimer` 14 / 2，`InvalidateVisual` 45 / 16，`new Pen(` 37 / 48，`override void Render` 12 / 1，`new StreamGeometry` 2 / 3（工厂方法内还有更多，见 AIW-03、AIX-03），`new FormattedText` 字面 0 / 1（但全部文本经 `SvDraw.Text`/`Ink.Text` 工厂每帧新建，见 AIX-02），`CompositionTarget` 0，`EffectiveViewport` 0，`Color.Parse`/`Colors.`/`#"` 硬编码色 0，`BoxShadow`/`BlurEffect` 1/0。
- 方法：先按文件建立热点矩阵（timer / invalidate / Pen / Geometry / FormattedText / LINQ / 资源查找 / ReducedMotion）；再**完整精读**两个共享助手与 20 个最重文件；其余 6 个图形场景（AgentLoop、MixtureOfExperts、PromptCache、SpeculativeDecoding、MultiRegionFailover 的绘制段）用 grep 定位 + 抽样精读；页面只做实例计数（并发 timer 数）与初始状态核对。
- 完整精读：`SpecialVisuals.cs`、`ExplainInk.cs`、`ArcExplainer.cs`、`ArcInkFluid.cs`、`ArcMorphLoader.cs`、`ArcBlackHole.cs`、`ArcActivityRings.cs`（绘制段）、`ArcContextWindow.cs`（绘制/Flash/状态段）、`ArcAgentTimeline.cs`（绘制/时钟段）、`ArcAgentStatus.cs`（时钟/两种自绘段）、`ArcAgentPlan.cs`（状态机/两种自绘段）、`ArcSkeletonMorph.cs`、`ArcStretchRefresh.cs`（交互/指示器段）、`ArcToolCall.cs`（timer 段）、`ArcTimeDial.cs`、`ArcDateReel.cs`、`ArcDataFlow.cs`、`ArcPaymentFlow.cs`、`ArcReasoningTree.cs`、`ArcVectorSearch.cs`（绘制段）。

## 1. 组件设计特征

### 1.1 Widgets：`Panel` 组合 + 内层一个自绘 `Control`

所有 widget 都是 `Panel`（`ArcSkeletonMorph` 是 `Decorator`），内部按需 new 一个专用自绘子控件，子控件持有动画状态。**没有 TemplatedControl / ControlTheme / PART_\***，全部视觉树在 `Rebuild()` 里用代码搭；颜色/尺寸一律 `Sv.Paint(property, token)`（绑定 `GetResourceObservable`）或绘制期 `Sv.Brush(token)`，因此换主题/换 accent 仍能响应（`SpecialVisuals.cs:39-47`）。

| 控件 | 形态 | 动画时钟（频率） | 自绘 Render | 每帧分配（峰值） | 是否只在可见时运行 |
| --- | --- | --- | --- | --- | --- |
| ArcActivityRings | Panel + `ArcRingsView` | `SvTween` 16 ms，仅数值 tween 的 520 ms | 是（1 个，`ArcRingsView`） | 3 环 × 56 tick = 168 `StreamGeometry` + 168 `Pen` | 不适用（tween 结束自停） |
| ArcAgentPlan | Panel + `ArcAgentMark`/`ArcStepMark` | `DispatcherTimer` 1100 ms 循环（Running）+ `ArcStepMark` 16 ms | 是（2 个） | 每 1.1 s 整树 `Rebuild()`；StepMark 每帧 5 次 `Sv.Brush` | **否** |
| ArcAgentStatus | Panel + `ArcMiniBar`/`ArcAgentLiveRing` | **2 个** `SvClock` 16 ms（时长文本 + 转环） | 是（2 个） | 每帧 5 `Sv.Brush` + 3–4 `Pen` + 1 `Arc` 几何 | **否**（Working 时永久） |
| ArcAgentTimeline | Panel + `ArcTimelinePlot` | `SvClock` 16 ms，仅 `IsPlaying`（默认 false） | 是（1 个） | 播放期 ~10 `FormattedText` + ~20 `Pen` + 每 lane 一次 LINQ | **否** |
| ArcBlackHole | Panel + `ArcBlackHoleList` | `SvTween` 16 ms，仅删除/撤销 1.2 s | 是（1 个） | 每行 3 `FormattedText` + 2–3 `Pen`（动画期） | 无定时器常驻 |
| ArcContextWindow | Panel + `ArcContextCells` | `DispatcherTimer` 16 ms × 420 ms（Flash） | 是（1 个） | 200 rect + ~200 个新建 `AlphaBrush` | **否** |
| ArcInkFluid | Panel + `ArcInkCanvas` | `SvClock` **16 ms 持续** | 是（1 个） | 150 粒子 ×（2 次 Push + 1 椭圆）+ 2 个 `RadialGradientBrush` + 5 次资源查找 | **否** |
| ArcMorphLoader | 裸 `Control` | `SvClock` **16 ms 持续，状态完成也不停** | 是（1 个） | 4–8 图元 + 1–2 `Pen`（Ring 变体每帧 4 个 `StreamGeometry`） | **否** |
| ArcSkeletonMorph | Decorator + `ArcMorphBlock` + `ArcSkeletonShape` | `SvClock` 40 ms 持续 + 每块一个一次性 stagger timer | 是（骨架 1 个） | 1 `AlphaBrush` + ≤6 rect | **否**（`IsVisible` 只用于切换，不门控时钟） |
| ArcStretchRefresh | Panel + `ArcPullIndicator` | 一次性 900 / 1800 ms + 每次 `Settle()` 新建 `SvTween` 16 ms | 是（1 个） | 下拉时每行一个 `new TranslateTransform` | **否** |
| ArcToolCall | Panel，**0 自绘** | 一次性 1200 / 1400 ms（Approve / Flash） | 否（0 次 `InvalidateVisual`） | 展开/审批时整树 `Rebuild()` | 无定时器常驻 |

### 1.2 Explainers：一个基类、纯自绘、单一相位

11 幅图解全部 `: ArcExplainer`（唯一的 `Control` 基类），每实例一个 **33 ms** `DispatcherTimer`，在 `OnAttachedToVisualTree` 启动、`OnDetachedFromVisualTree` 停止（`ArcExplainer.cs:39,80-90`）。画面被声明为「归一化相位 phase 的纯函数」：`Phase` 由 `_time / CycleSeconds` 得出（`ArcExplainer.cs:61-71`），因此循环无缝；reduced-motion 时 phase 被钉在 `PosterPhase`，并隐藏播放按钮（`ArcExplainer.cs:52,65,169-173`）。交互（hover/press/wheel/key）通过基类钩子 `OnHover/OnPress/OnRelease/OnWheel/OnKey/OnTick` 回流成 `bool`，返回 true 才 `InvalidateVisual`（`ArcExplainer.cs:195-294`）——这层抽象是全仓最干净的一处设计。

| 控件 | 场景内容（每帧） | 每帧主要分配 | phase 是否被使用 |
| --- | --- | --- | --- |
| ArcAgentLoop | 8+4+3 段循环图、3 站、箭头 | 14 `Ink.Label` + 6 `Pen` + 6 `Icon` + 1 `Chip` | 是 |
| ArcDataFlow | 3×2 节点 + 6 连线 + 6 记录粒子 | 9–21 `Label` + 6 几何 + 6 `Pen` + 12 `Ink.A` | 是 |
| ArcDateReel | 4 滚筒 × 11 行 + 摘要面板 | **~40–55 `Label`** + `PushTransform`/`PushClip` + 4 `Pen` | **否**（`_ = phase`） |
| ArcMixtureOfExperts | 8 行 token + 专家矩阵 | 16 `Label` + 1 `Paragraph` + 5 `Icon` + 2 `Pen` | 是 |
| ArcMultiRegionFailover | 负载均衡、策略、包 | 16 `Label` + 4 `Pen` + Chip/Icon | **否**（`_ = phase`） |
| ArcPaymentFlow | 4 节点链 + 回执道 + 结算轨 | 20 `Label` + 2 `List<Point>` + 4 几何 + 5 `Pen` | 是 |
| ArcPromptCache | 3 请求 × 段条 | **21 `Label`** + 5 `Pen` + 1 几何 | 是 |
| ArcReasoningTree | 15 节点树 + beam 搜索 | 12 `Label` + 4 次 LINQ 排序 + 每边 1 `Cubic` 几何 + 2 `Pen` | 是 |
| ArcSpeculativeDecoding | 轮次、lane、chips | 17 `Label` + 4 `Pen` + 4 `Icon` | 是 |
| ArcTimeDial | 24 时刻度 + 3 城市 + 城市行 | 12 `Label` + **24 `Pen`（小时刻度循环内）** + 3 `Chip` | **否**（`_ = phase`） |
| ArcVectorSearch | 倾斜平面 + 18 文档 + 近邻 | 12 `Label` + 3 `Poly` 环几何 + 18×18 距离 + `OrderBy` | 是 |

### 1.3 状态与组合

- 状态机分布不均：`ArcMorphLoader` 有 `_blend`/`_settle` 两条独立 tween 通道（`ArcMorphLoader.cs:126-127`）、`ArcDateReel` 有 4 个独立 `Reel`（速度/拖拽/吸附，`ArcDateReel.cs:24-35`）、`ArcMultiRegionFailover` 有 6 段 `Stages` 自动推进（`ArcMultiRegionFailover.cs:83-107`）；而 `ArcDataFlow`/`ArcPaymentFlow`/`ArcPromptCache`/`ArcSpeculativeDecoding` 等只是「phase → 开关量」的纯函数，没有独立状态机。缓动统一走 `Ink.Smooth/EaseOut/Span/Wave`（`ExplainInk.cs:406-439`）与 `ArcMotion.EaseStandard`（`SpecialVisuals.cs:273`），**没有一条用 Avalonia `Transitions`/`Animation`**。
- 组合控件用法正确：`ArcAgentTimeline`、`ArcContextWindow`、`ArcAgentPlan`、`ArcAgentStatus`、`ArcToolCall` 把按钮/文本/图例交给 `ArcButton`/`TextBlock`/`ArcIcon`，只把无法模板化的图形自绘；`ArcBlackHole` 把行、复选框、焦点环全部自绘（`ArcBlackHoleList.cs:287-370`），是自绘比例最高的 widget。
- 属性更新方式是本区域最一致的短板：widget 几乎不用 `AffectsRender`，而是在 `Changed.AddClassHandler` 里 **`Rebuild()` 整棵子树**（`ArcToolCall.cs:104-107`、`ArcAgentPlan.cs:89-93`、`ArcAgentStatus.cs:99-106`），只有 `ArcMorphLoader`/`ArcStepMark`/`ArcMiniBar`/`ArcAgentLiveRing` 用了 `AffectsRender`（`ArcMorphLoader.cs:66`、`ArcAgentPlan.cs:791`、`ArcAgentStatus.cs:612,655`）。

### 1.4 token / 主题 / 可访问性

- 零硬编码色（grep `Color.Parse`/`Colors.`/`#"` 全为 0），全部走 `ArcTokens`；主题切换经 `DynamicResource` 或绘制期资源查找生效。代价是绘制期查找没有缓存（BRIEF B1），见 AIX-02。
- 可访问性是这个区域的亮点：11 幅图解每幅都在构造函数里 `Describe(name, help)`（`ArcExplainer.cs:310-314`），widget 普遍设置 `AutomationProperties.SetName/HelpText`（如 `ArcBlackHole.cs:156,657-658`）并实现键盘语义（`ArcBlackHoleList.cs:606-639` 方向键/Space/Delete/Ctrl+Z；`ArcTimeDial.cs:182-208` 方向键/PageUp/Home/End）。
- reduced-motion 有**集中实现**：`SvTween.Run` 直接跳到终点、`SvClock.Start` 只回调一次 0、`ArcExplainer` 钉住 poster phase（`SpecialVisuals.cs:261-265,301-305`、`ArcExplainer.cs:52,65`）。不足是 timer 自身不断：见 AIX-09、AIW-10。

## 2. 性能问题清单

| ID | 分类 | 严重度 | 位置 | 问题一句话 | 放大因子 |
| --- | --- | --- | --- | --- | --- |
| AIW-01 | T1/R1 | P0 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcMorphLoader.cs:121-133` | 16 ms 时钟在 attach 时无条件启动，完成态也不 Stop，每帧 `InvalidateVisual` | × 实例数（演示页 7 个）× 62.5 Hz |
| AIW-02 | R1/R3/G1 | P0 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcInkFluid.cs:404-431` | 150 个粒子每帧各一次 `PushTransform`+`PushOpacity`+渐变椭圆；每帧新建 2 个 `RadialGradientBrush`（8 个 GradientStop） | × 150 粒子 × 62.5 Hz；× 实例数 |
| AIW-03 | R1/R3 | P0 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcActivityRings.cs:545-552` | 每环每 tick 新建 `Pen` 并调用 `SvDraw.Arc`（内部 `new StreamGeometry`）→ 168 个几何/帧 | × 环数 × 56 tick × 31 帧（520 ms） |
| AIW-04 | T1/R1 | P1 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcAgentStatus.cs:189-193,696-700` | 一个实例两个 16 ms 时钟（时长文本 + 转环），Working 时永久运行 | × 实例数 × 2 × 62.5 Hz |
| AIW-05 | R1/S2/R3 | P1 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcAgentTimeline.cs:510-661` | 播放期每帧重排全部文本、重建全部 Pen、并对每个 lane 做 LINQ 过滤 | × 60 Hz × 14.4 s × lane/span 数 |
| AIW-06 | R1/R3 | P1 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcContextWindow.cs:598-608,670-705` | Flash 期 200 个格子逐格新建 `ImmutableSolidColorBrush`，部分格子再加 `Pen` | × 200 格 × 26 帧（420 ms） |
| AIW-07 | L2 | P1 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcToolCall.cs:104-107,166-168` | 展开/审批/每次状态变化都 `Children.Clear()` 重建整棵树 | × 行数 × 交互次数 |
| AIW-08 | L1/P1 | P1 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcStretchRefresh.cs:239-290` | pointer-move 每次 `ApplyPull`：每行 new `TranslateTransform` + 指示器 `InvalidateVisual` | × 行数 × 指针事件频率 |
| AIW-09 | R2/R1 | P2 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcAgentPlan.cs:842-846` | `ArcStepMark.Render` 无条件先取 5 个 token 画刷（多数状态只用 2 个） | × 60 Hz × Active 步数 |
| AIW-10 | T1/R2 | P1 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcInkFluid.cs:207` | 构造函数里 `WarmUp(80)` 且所有时钟不看 `IsVisible`/有效视口 | × 实例数 × 构造次数 |
| AIW-11 | T2 | P2 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/SpecialVisuals.cs:270,308` | `SvTween`/`SvClock` 每次调用都 new 一个 `DispatcherTimer` | × 交互次数 |
| AIW-12 | M1 | P2 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcToolCall.cs:616-622,649-658` | 一次性 timer 不随 detach 停止，闭包持有按钮/数据 | × 连续操作次数 |
| AIW-13 | T1 | P2 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcSkeletonMorph.cs:346-353` | 每个骨架块一个一次性 timer（List 管理） | × 块数 |
| AIW-14 | R1 | P2 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcBlackHole.cs:303,327` | 删除动画期每次 Render 做 LINQ 计数，行循环内 `Take(index).Count` 为 O(n²) | × 行数² × 63 Hz |
| AIW-15 | M2 | P2 | `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcContextWindow.cs:531-547,551` | 只有 Messages 会被压缩，重复 Attach 的 Files 项无限累积 | × 点击次数 |
| AIX-01 | T1/R3 | P0 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcExplainer.cs:92-106` | 33 ms 时钟令**每个**图解无条件整场景重绘；静态场景（TimeDial/DateReel/MultiRegionFailover）也照重绘 | × 实例数 × 30 Hz × 全场景图元 |
| AIX-02 | S2/R2/R1 | P0 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ExplainInk.cs:80-117` | 每个文本都新建 `Typeface`+`FormattedText`，且每次先 `TryFindResource` 取字体；`Pen` 也无缓存 | × 每帧文本数（5–55）× 30 Hz |
| AIX-03 | R1/R3 | P0 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcDataFlow.cs:140,144` | 连线/曲线每帧重建 `StreamGeometry` 与 `Pen`；节点 Rect 每帧重算 | × 边数 × 30 Hz |
| AIX-04 | R1/S2/L1 | P0 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcReasoningTree.cs:179-182,262` | 静态树每帧重跑 beam-search 排序（4 次 LINQ）+ `Select().ToList()`，并逐节点 `Contains` | × 30 Hz × 节点数² |
| AIX-05 | R1/P1 | P1 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcVectorSearch.cs:218-242,279` | 每帧 O(18²) 近邻距离 + 每帧 `OrderBy`/`Max` 排序 | × 30 Hz × 节点数² |
| AIX-06 | P1/R1 | P1 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcTimeDial.cs:213,239-251` | 静态表盘 30 Hz 空转，且 24 个时刻度每帧各建一个 `Pen` | × 30 Hz × 24 + 拖动时 × pointer 事件 |
| AIX-07 | R1/S2 | P1 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcDateReel.cs:405-444` | 每个可见滚筒行一个 `Label`（带 maxWidth 截断），一帧 40+ 次文本排版；速度衰减按 tick 而非 dt | × 30 Hz × 4 滚筒 × 11 行 |
| AIX-08 | S2/R1 | P1 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcPromptCache.cs:130-131,170` | 21 个标签/帧全部重新排版，无任何文本缓存 | × 30 Hz × 标签数 |
| AIX-09 | T1 | P1 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcExplainer.cs:95-105` | 暂停/ReducedMotion 时 33 ms timer 仍空转并调用 `OnTick` | × 实例数 × 30 Hz（空回调） |
| AIX-10 | R1 | P2 | `src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcPaymentFlow.cs:102-111` | 每帧 new `List<Point>`/数组（lane、rail、presets、rows） | × 30 Hz × 列表数 |

## 3. 逐条说明

### AIW-01 MorphLoader：16 ms 时钟常开，7 个实例并发（P0）

- 位置：`src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcMorphLoader.cs:121-133`
- 证据：
  ```csharp
  _clock.Start(seconds =>
  {
      _time = seconds;
      InvalidateVisual();
  });
  ```
- 触发/放大：`OnAttachedToVisualTree` 无条件启动，`OnDetachedFromVisualTree` 才停；`Status` 变为 Success/Error 后时钟仍在跑（`_settle` 的 260 ms tween 早已结束）。加载环/方块的静止画面与 `_time` 无关，因此完成态的每帧重绘是纯浪费。演示页同时放 7 个实例（`Pages/MorphLoaderPage.cs:23,24,25,26,28,31,34`），其中 2 个是 Success/Error → **7 个 16 ms timer，约 420 次 InvalidateVisual/s**，即使 2 个已无动画。
- 影响：每帧 7 个小控件重画（每个 4–8 图元 + 1–2 Pen；Ring 变体每帧 4 个 `SvDraw.Arc` 几何，`ArcMorphLoader.cs:276-281`）。单帧成本小，但 420 次/秒的重绘请求 + 7 个 timer 回调会持续占用 UI 线程，并让 Avalonia 无法进入空闲节流。
- 修复建议：状态非 `Loading` 时 `_clock.Stop()`，在 `OnStatusChanged` 里按需重开；或改为 `CompositionTarget.Rendering` + 全局动画注册表（同一帧只驱动一次）；再加 `if (!IsVisible || !IsEffectivelyVisible) return;` 门控。

### AIW-02 InkFluid：150 个粒子 × `PushOpacity` + 每帧 2 个渐变刷（P0）

- 位置：`src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcInkFluid.cs:404-431`
- 证据：
  ```csharp
  var accentBrush = SoftBrush(accentColor);   // new RadialGradientBrush + 4 GradientStop
  var inkBrush = SoftBrush(inkColor);         // 每帧再来一次
  foreach (var particle in _particles)        // Count = 150（:192）
  using (context.PushTransform(...))
  using (context.PushOpacity(fade))
      context.DrawEllipse(brush, null, new Point(x, y), stampRadius * 1.9, stampRadius * 1.15);
  ```
- 触发/放大：`SvClock` 16 ms 持续驱动（`:283-289`），每帧 `Advance(150 particles)` + 整场景重绘。粒子数组是**预分配**的 `Particle[150]`（`:194`），`Random` 只在重生时用（`:238-254`）→ 这部分做得好；真正的开销在绘制：150 次 `PushOpacity`（每粒子一层半透明合成）+ 150 次带旋转的 `PushTransform` + 150 个径向渐变椭圆；另有 5 次绘制期资源查找（`:386,387,393,402,403`）、每帧 1 个 `new Pen` + 1 个 `AlphaBrush`（`:431`）。
- 影响：`PushOpacity` 会把每个粒子的绘制拆成独立合成层，这是典型的 GPU 填充率/合成开销，在 4K 或高 DPI 下最明显；每帧 2 个渐变刷 × 4 个 stop = 8 个 `GradientStop` + 2 个 `RadialGradientBrush` 的纯 GC 压力。页面滚动离开视口后仍然全速运行（无 `IsVisible` 检查）。
- 修复建议：① 把粒子戳预渲染成 2 张 `RenderTargetBitmap`（accent/ink 各一张），每帧用 `DrawImage` + 单一 `PushOpacity`（或直接把 fade 烘进顶点 alpha），去掉 150 次 `PushOpacity`；② `SoftBrush` 结果按颜色缓存为 `static readonly`/字典；③ 粒子数降到 40–60 或改用一次 `DrawGeometry` 合并同色粒子；④ 时钟加可见性门控。

### AIW-03 ActivityRings：168 段弧 = 168 个几何 + 168 个 Pen/帧（P0）

- 位置：`src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcActivityRings.cs:545-552`
- 证据：
  ```csharp
  for (var tick = 0; tick < tickCount; tick++)          // tickCount = 56（:519）
  {
      var on = sweep > 0 && delta <= sweep - step * 0.4;
      var pen = new Pen(on ? filled : track, thickness * 0.92, null, PenLineCap.Flat);
      SvDraw.Arc(context, center, radius, start, step * 0.64, pen);   // 内部 new StreamGeometry（SpecialVisuals.cs:350）
  }
  ```
- 触发/放大：切天/切换指标触发 `SyncValues(animate: true)`（`ArcActivityRings.cs:157-196`），520 ms tween 每帧调用 `_view.SetValues` → `InvalidateVisual`（`:498-503`）→ 3 个环（`_metrics` 3 条）× 56 tick = **168 次 `new Pen` + 168 个 `StreamGeometry` + 168 次 `DrawGeometry`/帧，持续约 31 帧**；另有 3 次 `PushOpacity`、每环 1 个 `Sv.Brush` + 1 个 `AlphaBrush`（`:541-542`）与超 100% 时的第 4 条内弧（`:555-559`）。
- 影响：这不是持续 60 fps，而是**每次交互 31 帧的重度几何洪泛**（单帧 >300 个短期对象），在中低端设备上可直接看到掉帧；同时 tick 环的 56 段几何每帧形状不变（只有颜色 on/off 变），属于可完全缓存的部分。
- 修复建议：① 把 56 段的「轨道」一次画成一条虚线圆弧或一张缓存位图；② 值弧只用 1 条 `StreamGeometry`（把 56 段 tick 合并成一个 `StreamGeometry` 的 56 个 figure，一次 `DrawGeometry`），而不是 56 次独立几何；③ `track` 画刷/`filled` 画刷按 token 缓存为字段；④ tween 回调里只更新数值，`Render` 内不分配。

### AIW-04 AgentStatus：一个控件两个 16 ms 时钟（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcAgentStatus.cs:189-193`、`:696-700`
- 证据：
  ```csharp
  _clock.Start(seconds => { _displaySeconds = ElapsedSeconds + seconds; UpdateTimerText(); }); // 外层
  _clock.Start(seconds => { _angle = (seconds * 150) % 360; InvalidateVisual(); });            // ArcAgentLiveRing
  ```
- 触发/放大：`Status == Working` 时两个时钟同时启动（演示页默认 Working，`Pages/AgentStatusPage.cs:24`），永久运行；每个 tick 更新一次 `TextBlock.Text`（触发文本排版）或重画 30×30 转环。放大因子 = 实例数 × 2 × 62.5 Hz。
- 影响：`UpdateTimerText` 每帧拼字符串并让 Avalonia 重排一行文本（60 次/秒），而时长显示只需 10 Hz 即可；转环每帧 5 次 `Sv.Brush` + 3–4 个 `Pen` + 1 个 `Arc` 几何（`:715-726`），绘制面积很小但分配率不低。
- 修复建议：时长文本降到 100–200 ms 一次（或复用 `DispatcherTimer` 的 interval=100）；转环改 `CompositionTarget.Rendering` 或 33 ms；`Render` 里的 5 个画刷提到字段（token 变了再刷）。

### AIW-05 AgentTimeline：播放期 60 fps 全量文本 + LINQ（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcAgentTimeline.cs:510-661`
- 证据：
  ```csharp
  foreach (var span in spans.Where(item => item.AgentId == agents[i].Id))   // :564，每个 lane 一次
  context.DrawLine(new Pen(borderSubtle, 1), ...);                          // :535，每 lane 一个新 Pen
  var name = SvDraw.Text(agents[i].Name, 12, ..., FontWeight.Medium);        // :539，每帧新 FormattedText
  ```
- 触发/放大：用户点 Play 后 `SyncClock()` 启动 `SvClock` 16 ms（`:233-249`），`Advance` 每帧 `_plot.InvalidateVisual()`（`:265`），持续 `Duration`（演示 14.4 s，默认属性 26.4 s）。每帧重排：5 个 lane 名 + 5 个模型名 + 7 个轴刻度 + 13 个 span 行内文本 ≈ 10–20 个 `FormattedText`；~20 个 `Pen`（lane 线、轴线、tick、retry 弧、交界线、playhead）；每 lane 一次 LINQ `Where`；`series` 画刷在循环内 `Sv.Brush`（`:537,607,612`）。
- 影响：播放期间是标准 60 fps 重绘路径，文本排版是 UI 线程上最贵的一类工作；另外 `OnPointerMoved` 在 scrub 时每 move 一次 `ScrubTo` → `InvalidateVisual`（`:739-745`），与播放叠加。
- 修复建议：轴刻度/ lane 标签/模型名是静态的 → 缓存为 `FormattedText` 字段（`Duration` 变了才重建）；`spans` 预分组为 `Dictionary<string, List<ArcTimelineSpan>>`（Rebuild 时一次）；`Pen`/画刷按 token 缓存；播放时钟改 `CompositionTarget.Rendering` 以对齐渲染帧。

### AIW-06 ContextWindow：Flash 期 200 个格子逐格建刷（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcContextWindow.cs:598-608`、`:670-705`
- 证据：
  ```csharp
  _flash = Sv.Clamp01((DateTime.UtcNow - started).TotalMilliseconds / 420);   // :600
  _cells.Flash = _flash;
  _cells.InvalidateVisual();                                                  // :602
  context.DrawRectangle(Sv.AlphaBrush(CategoryToken((ArcContextCategory)category), alpha), ...);  // :694
  ```
- 触发/放大：每次发送/附件/压缩调用 `Flash()`，16 ms timer 跑 420 ms（≈26 帧）；`Render` 遍历 20×10=200 格（`:77-78`），每个已用格子新建 1 个 `ImmutableSolidColorBrush`（`:694`），预留格加 1 个 `Pen`（`:683`），高亮格再加 1 个 `Pen`（`:697`），Flash 区间每格再加 1 个 `Pen`（`:703`）。峰值 ≈ 200 刷 + 数十 Pen/帧。
- 影响：单帧 <200 个小矩形，光栅化不贵，但 GC 与 `TryFindResource`（`CategoryToken`→`Sv.AlphaBrush`→`ArcResources.Color` 内部 `Brush` 查找两次）在 26 帧内密集发生。
- 修复建议：把「类别 → 不透明画刷」预计算成 6 个字段（主题切换时重算）；Flash 只改一个全局 alpha uniform（或在 `Render` 开头 `PushOpacity` 一次），而不是每格新建刷；格子仍是 200 个小矩形时可直接缓存为一张 `WriteableBitmap`/`RenderTargetBitmap`，Flash 只画覆盖层。

### AIW-07 组合控件每次状态变化都重建整树（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcToolCall.cs:104-107,166-168`；同类：`ArcAgentPlan.cs:89-93,222,225-227`；`ArcAgentStatus.cs:99-106`
- 证据：
  ```csharp
  IsOpenProperty.Changed.AddClassHandler<ArcToolCall>((call, _) => call.Rebuild());
  private void Rebuild()
  {
      Children.Clear();          // 展开一行就重建所有行 + header + footer
  ```
- 触发/放大：`ArcToolCall` 每次展开/折叠、Approve、Deny、Retry、复制反馈都会 `Rebuild()`；`ArcAgentPlan` 每 1100 ms 推进一个步骤也 `Rebuild()`（`:222`）——一次完整运行会重建 3–5 次整棵树，每次都重新 new `ArcButton`/`ArcIcon`/`TextBlock`、重新绑定资源。放大因子 = 行数 × 交互次数。
- 影响：不是持续开销，但会造成点击瞬间的布局抖动和托管分配抖动（L2/L1）。`ArcAgentPlan` 的 1.1 s 周期重建尤其浪费：状态变化只影响单个 step 的图标与文字。
- 修复建议：为每个 step/row 保留控件实例，只改 `Classes`/图标/文本；或把行做成带 `AffectsRender` 的轻量 `Control`，状态改变只 `InvalidateVisual` 该行。至少把 `Rebuild()` 合并到 `Dispatcher.UIThread.Post(..., DispatcherPriority.Background)` 做一次去重。

### AIW-08 StretchRefresh：pointer-move 里逐行换 RenderTransform（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcStretchRefresh.cs:239-254,276-291`
- 证据：
  ```csharp
  ApplyPull(Math.Min(150, dy * 0.55));                                  // :254，每次 move
  row.RenderTransform = new TranslateTransform(0, _pull * (0.72 + i * 0.03));  // :288，每行每帧
  ```
- 触发/放大：按住下拉时每个 pointer-move 事件都执行 `ApplyPull`：重建每行的 `TranslateTransform`（分配 + 渲染变换失效），并 `_indicator.InvalidateVisual()`（`:282`）。放大因子 = 行数（演示 4–6 行）× 指针事件频率（可达 120+ Hz）。
- 影响：输入热路径上的分配与变换失效，与 AIW-05 的播放叠加时更明显。`Settle()` 还每次 new 一个 `SvTween` 局部对象（`:269-274`），其 timer 无人引用、只能靠自然结束。
- 修复建议：`TranslateTransform` 每行只建一次并改 `Y` 值（`TranslateTransform.Y` 是 `AffectsRender` 的）；或整个列表用一个外层 `TranslateTransform` + 每行不同系数的 `ScaleTransform`；指针路径里用 `e.GetPosition` 的增量而非重设。

### AIW-09/10/11/12/13/14/15（P2 组）

- **AIW-09** `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcAgentPlan.cs:842-846`：`Render` 开头无条件取 5 个 token 画刷（`muted/track/accent/success/danger`），Done/Failed 状态各只用 2 个；改为 switch 内按需取。
- **AIW-10** `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcInkFluid.cs:207`：`WarmUp(ArcThemeManager.ReducedMotion ? 150 : 80)` 在构造期跑 80×150=12000 次粒子积分；页面切换/重建即重复。建议惰性化（首次 Render 前一次）或把 warmup 步数降到 20。全区域 16 个 timer 无一检查 `IsVisible`/`EffectiveViewport`（grep 为 0），滚动出视口后照常运行。
- **AIW-11** `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/SpecialVisuals.cs:270,308`：`SvTween.Run`/`SvClock.Start` 每次 new `DispatcherTimer`；建议一个共享的 `DispatcherTimer`（单例队列）驱动所有 tween/clock，或直接用 `CompositionTarget.Rendering`。
- **AIW-12** `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcToolCall.cs:616-622,649-658`：一次性 timer 未随 `OnDetachedFromVisualTree` 取消，闭包持有 `ArcButton`/`ArcToolCallData`；控件被移出树后仍会执行 `Rebuild()`。建议存字段并在 detach 时 `Stop()`。
- **AIW-13** `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcSkeletonMorph.cs:346-353`：每个骨架块一个 `DispatcherTimer`（`_timers` 列表管理，detach 时统一停 —— 生命周期是对的），但 N 块即 N 个 timer；建议用一个 timer + 到期索引队列。
- **AIW-14** `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcBlackHole.cs:303,327`：`Render` 里 `_items.Count(item => IsGhostItem(item.Id))`；行循环内 `_items.Take(index).Count(...)` 是 O(n²) + 每行一个迭代器。演示 6 行时无感，行数上百会明显。建议在 `Advance` 里预计算 ghost 计数前缀和。
- **AIW-15** `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcContextWindow.cs:531-547,551`：`Attach()` 每次 `_items.Add` 一个 Files 项，而 `Compact` 只折叠 `Messages`（`:551`）→ 无 Messages 时「Attach 到底」会让 `_items` 无限增长（视觉上被 200 格截断，但 legend/自动化文本会变长）。建议对 Files 也做上限/合并。

### AIX-01 所有 Explainer 以 33 ms 时钟无条件全场景重绘（P0）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcExplainer.cs:39,92-106,133-151`
- 证据：
  ```csharp
  _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };   // :39
  if (!Reduced && _playing) { _time += _timer.Interval.TotalSeconds; clockMoved = true; }
  var contentMoved = OnTick(_timer.Interval.TotalSeconds);
  if (clockMoved || contentMoved) InvalidateVisual();                         // :102-105
  ```
- 触发/放大：只要控件 attach 到视觉树就每 33 ms 走一次；默认 `_playing = true` → `clockMoved` 恒为 true → **每个实例无条件 30 fps 重绘整幅 960×470 场景**（`Render` → `DrawBackdrop` + `DrawScene` + `DrawTransport`，`:142-144`）。其中 `ArcTimeDial`、`ArcDateReel`、`ArcMultiRegionFailover` 明确写着 `_ = phase;`（`ArcTimeDial.cs:213`、`ArcDateReel.cs:381`、`ArcMultiRegionFailover.cs:175`），TimeDial 的画面甚至完全由 `_value` 决定——重绘 100% 冗余。放大因子 = 实例数 × 30 Hz × 每场景图元数（12–55 个文本 + 10–24 个 Pen）。
- 影响：这是本区域最大的结构性开销：即使画面静止，UI 线程也被 30 Hz 的全场景绘制占满；再叠加 AIX-02 的文本排版，单幅图每秒约 1500–3000 次托管分配。多图同页（图解区块）时线性放大。
- 修复建议：① 给基类加「场景是否依赖 phase」的脏标记（`protected virtual bool Animated => true`），TimeDial 等覆写为 false；`clockMoved && Animated` 才重绘；② 帧率降到 20 fps（`Interval = 50`）或改 `CompositionTarget.Rendering`（与渲染帧对齐，避免空转）；③ 增加 `IsEffectivelyVisible` 门控；④ 静态层（背景、节点、标签）缓存为 `RenderTargetBitmap`，每帧只画动态层。

### AIX-02 每帧新建 FormattedText/Typeface，且每次都查字体资源（P0）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ExplainInk.cs:80-117`
- 证据：
  ```csharp
  public static FontFamily Font { get { if (Application.Current is { } app &&
      app.TryFindResource(ArcTokens.FontBody, out var value) ...) return family; ... } }   // :80-93
  var typeface = new Typeface(Font, FontStyle.Normal, weight ?? FontWeight.Normal);        // :103
  var formatted = new FormattedText(text, Culture, ..., typeface, size, brush);            // :104
  formatted.SetFontFeatures(Tabular);
  ```
- 触发/放大：`Ink.Label`（`:120-133`）/`Ink.Text`/`Ink.Paragraph`/`Ink.Chip`/`Ink.Width` 全部走这里，被每一幅图解的每一行文字每帧调用：DateReel ~40–55 次/帧、PromptCache 21 次、SpeculativeDecoding 17 次、MoE 16 次、MultiRegionFailover 16 次、AgentLoop 14 次、TimeDial/VectorSearch/ReasoningTree 各 12 次。`Font` 是**属性**，每次访问都 `TryFindResource`（BRIEF B1 的放大版）；`Ink.Chip` 还会先 `Width()` 再 `Label()`，同一文本排版两次（`ExplainInk.cs:218,228`）。`Pen` 同样无缓存：`Ink.Stroke`（`:53-54`）、`Ink.Dashed`（`:75-76`，每次还 new 一个 `double[]` + `DashStyle`）。
- 影响：按 30 Hz、单图 20 条文本估算 ≈ 600 次 `FormattedText` + 600 次 `Typeface` + 600 次 `TryFindResource`/秒；文本排版是 Skia 中最贵的一类操作，`SetFontFeatures` 每次还重新解析 feature。这是 AIX-01 的乘数，两者必须一起修。
- 修复建议：① `Font` 改为 `static` 惰性字段（并在 `ArcThemeManager.Apply` 时失效）；② 加 `static readonly` 的 token→画刷、`(token,thickness)`→Pen 缓存；③ 文本缓存：键 `(text,size,weight,maxWidth)`，值 `FormattedText`（每幅图解的标签大多不变，只有少数字符串随时钟变）；④ `Chip` 内部复用一次 `Width` 的测量结果；⑤ 尺寸/字重固定的标题直接预生成。

### AIX-03 连线/曲线每帧重建几何，节点布局每帧重算（P0）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcDataFlow.cs:140,144`；同类 `ArcPaymentFlow.cs:102-117,133,155`；`ArcReasoningTree.cs:227`
- 证据：
  ```csharp
  context.DrawGeometry(null, Ink.Stroke(sourceBrush, sourceHot ? 2 : 1.2), Ink.Flow(_sources[i].Anchor, coreLeft));      // DataFlow:140
  context.DrawGeometry(null, Ink.Stroke(destinationBrush, ...), Ink.Flow(coreRight, _destinations[i].Anchor));            // DataFlow:144
  var lane = new List<Point> { ... };                             // PaymentFlow:102
  context.DrawGeometry(null, Ink.Dashed(..., 1.2, 5, 4), Polyline(lane));   // PaymentFlow:109
  ```
- 触发/放大：`Ink.Flow`/`Ink.Cubic`/`Ink.Poly`/`Ink.Arc` 每次调用都 `new StreamGeometry()` + `Open()/BeginFigure/LineTo/EndFigure`（`ExplainInk.cs:279-359`）。DataFlow 每帧 6 条 wire → 6 几何；PaymentFlow 每帧 2 个 `List<Point>` + 4 几何（lane、rail、两段 `PartialPath`）；ReasoningTree 每帧每条边 1 个 `Cubic` + 1 个 `Pen`（15 节点 → ~15 几何）；VectorSearch 每帧 3 个 `List<Point>`（40 点圆环）+ `ToArray` + 3 几何；节点 Rect 也每帧重算（`ArcDataFlow.cs:117-127`、`ArcPaymentFlow.cs:82-91`、`ArcDateReel.cs:392-397`、`ArcReasoningTree.cs:207-212`）。放大因子 = 边数/节点数 × 30 Hz。
- 影响：几何构建在托管侧（点数组、命令流）与 SKPath 构建两侧都耗 CPU；这些路径在数据不变时形状完全不变（只有颜色变化），属于可缓存却被每帧重建的典型。
- 修复建议：① 每幅图解在 `DrawScene` 首帧把静态几何缓存到字段，`OnDetached` 时清空；颜色变化改为「缓存几何 + 每次画不同 Pen」；② 数据（节点位置）只在 `width/height` 或数据变化时重算布局，而不是每帧；③ `Pen` 按 (token, thickness, cap) 缓存；④ 对仅透明度变化的连线，使用一个基几何 + `PushOpacity`。

### AIX-04 ReasoningTree：静态树每帧重跑 beam-search 排序（P0）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcReasoningTree.cs:179-182,262`
- 证据：
  ```csharp
  var beam1 = _roots.OrderByDescending(t => t.Score).Take(_beam).ToList();
  var beam2 = beam1.SelectMany(t => t.Children).OrderByDescending(t => t.Score).Take(_beam).ToList();
  var beam3 = beam2.SelectMany(t => t.Children).OrderByDescending(t => t.Score).Take(_beam).ToList();
  var answer = beam3.OrderByDescending(t => t.Score).FirstOrDefault();
  ```
- 触发/放大：树是构造期固定的静态数据，只有 `_beam`（用户点 pill）与 hover 会变；但这段搜索在 `DrawScene` 里，**每 33 ms 执行一次**：4 次 `OrderByDescending`（各自分配比较器/缓冲）+ 2 次 `SelectMany` + 4 次 `ToList`，再对 15 个节点做 `_winners.Contains`/`_path.Contains` 线性查找（`ArcReasoningTree.cs:221,240`，O(n²)），另有 `_path.Select(...).ToList()`（`:262`）。放大因子 = 30 Hz × 节点数 × log 节点数。
- 影响：这是「静态数据每帧重算」的教科书案例：排序结果与当前帧无关，只在 `_beam` 变化时需要重算。GC 压力（比较器 + 列表）与 CPU 都在 30 Hz 上持续发生。
- 修复建议：在 `_beam` 变化/构造时算一次 beam 的结果，缓存 `_winners`/`_path`/`answer`；`Render` 只消费缓存；把 `Contains` 换成 `HashSet<Thought>` 或给节点加 `IsWinner/IsOnPath` 布尔字段（一次遍历标记）。

### AIX-05 VectorSearch：每帧 O(n²) 近邻 + 排序（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcVectorSearch.cs:218-242,279`
- 证据：
  ```csharp
  for (var j = 0; j < _documents.Count; j++)          // 18 个文档 → 18×18
      var distance = Distance(doc.Point, _documents[j].Point);
  _matches.AddRange(_documents.OrderBy(doc => Distance(doc.Point, queryPoint)).Take(K));   // :279
  ```
- 触发/放大：18 个文档（3 topic × 6）→ 每帧 324 次 `Math.Sqrt` 的近邻搜索（只为在**同一簇内**画一条连接线），加一次 18 元素的 `OrderBy`（含 lambda/比较器分配）+ `_matches.Max(...)`（`:283`）；每帧还重算 18 次投影（`:213-216`）与 3×40 点的簇环（`:191-198`）。数据与 `_query` 固定，`stage` 只在用户按键时变。
- 影响：30 Hz 下约 1 万次距离计算/秒 + 每秒 30 次小排序；单看不大，但它是 AIX-01 之外的纯浪费，且随文档数平方放大（真实组件若接 1000 文档会直接卡死）。
- 修复建议：文档投影与近邻在数据/`_query` 变化时预计算；`OrderBy` 改 `PartialSort`（Top-K 用插入排序/堆）；投影只在 `stage`/尺寸变化时重算。

### AIX-06 TimeDial：静态表盘 30 Hz 空转 + 24 个 Pen/帧（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcTimeDial.cs:213,239-251`
- 证据：
  ```csharp
  _ = phase;                                                    // :213，明确忽略相位
  for (var hour = 0; hour < 24; hour++)
  {
      context.DrawLine(Ink.Stroke(major ? Ink.A(Ink.B(ArcTokens.TextSecondary), 0.55) : ..., major ? 1.2 : 1), inner, outer);  // :245
      if (major) Ink.Label(context, $"{hour:00}", at.X, at.Y, 9.5, muted, ...);                                                 // :249
  }
  ```
- 触发/放大：画面只依赖 `_value`（拖拽/键盘才变），但基类 33 ms 时钟仍在推进 `_time` → 每帧全场景重绘（AIX-01）；每帧 24 个时刻度各建 1 个 `Pen`（`Ink.Stroke`）+ `Ink.A` 刷，另有 3 个 `Ink.Arc` 几何、4 个城市 Chip（每个 Chip = 2 次文本排版）、12 个标签。拖拽时 `SetValue` 还会在每次 pointer-move 上再 `InvalidateVisual`（`:76-86`）。
- 影响：纯浪费：一帧 24 个 Pen + ~20 次文本排版 × 30 Hz，画面却完全静止。
- 修复建议：这是 AIX-01 的最佳验证案例——把该图覆写为 `Animated => false`，只在 `_value`/hover/尺寸变化时重绘；24 个刻度的 Pen 合并成 2 个（major/minor 各一个缓存 Pen）；表盘（盘面、刻度、城市环）可整体缓存为 `RenderTargetBitmap`，拖拽时只重画指针/城市点。

### AIX-07 DateReel：一帧 40+ 次文本排版 + 速度衰减与帧率耦合（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcDateReel.cs:405-444,159-160`
- 证据：
  ```csharp
  for (var row = -5; row <= 5; row++)                       // 4 滚筒 × 11 行
      using (context.PushTransform(...))
          Ink.Label(context, reel.Items[index], x + ColumnWidth / 2, 0, 13.5,
              Ink.A(Ink.B(ArcTokens.TextSecondary), alpha), FontWeight.Medium, Align.Center, ColumnWidth - 8);  // :434-443
  reel.Offset = Math.Clamp(reel.Offset + reel.Velocity, ...);
  reel.Velocity *= 0.90;                                    // :160，按 tick 衰减，与 dt 无关
  ```
- 触发/放大：4 滚筒 × 最多 10 行可见 = 40 次 `Label`/帧（每行还带 `maxWidth` → `MaxTextWidth/MaxLineCount/Trimming` 的完整排版），加标题/摘要/预设/确认按钮 ≈ 55 次；每行一个 `PushTransform`、每列一个 `PushClip`。flick 惯性用「每 tick ×0.90」而不是 `1 - k·dt`，在 30 fps 与 120 Hz 显示器上手感/滑行距离不同（相位虽是 33 ms 固定，但 `OnPointerMoved` 会额外改速度，见 `:232`）。
- 影响：这是本区域单帧文本量最大的一幅（≈55 次排版 × 30 Hz ≈ 1650 次/秒）；惯性衰减与 tick 绑定是正确性问题（同一手势在不同刷新率下不同）。
- 修复建议：可见行降到 ±3（7 行）并把每行渲染缓存为位图/`FormattedText`（文本只在吸附后变化，滚动中只需变换）；速度衰减改成 `v *= Math.Exp(-k * seconds)`；把「滚动中」单独标记，只重绘 reel 区域（`InvalidateVisual(rect)`）。

### AIX-08 PromptCache / MoE / SpecDec / AgentLoop：每帧 16–21 条标签，无文本缓存（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcPromptCache.cs:130-131,170`（代表）；`ArcMixtureOfExperts.cs:167` 起 16 条；`ArcSpeculativeDecoding.cs:115` 起 17 条；`ArcAgentLoop.cs:64` 起 14 条
- 证据：
  ```csharp
  Ink.Label(context, $"Request {i + 1}", row.X + 14, row.Y + 16, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);  // PromptCache:130
  Ink.Label(context, segment.Label, barX + offset + 6, barY + 5.5, 8.5, ..., maxWidth: w - 10);                            // PromptCache:170
  ```
- 触发/放大：四幅图解的文本量 14–21 条/帧，全部走 AIX-02 的无缓存路径；其中 PromptCache 的段标签、MoE 的 token 串、SpecDec 的 chips 在整段动画中字符串不变。放大因子 = 30 Hz × 标签数 × 实例数。
- 影响：与 AIX-02 同源，单独列出是因为这四幅的标签大多**静态**（不像 DataFlow 有递增计数），缓存命中率可达 90% 以上。
- 修复建议：在 `ArcExplainer` 基类提供 `protected FormattedText Cached(string key, Func<FormattedText> make)` 的小缓存（每实例、按 key），静态标签一律走它；只对真会变的数字/进度走每帧排版。

### AIX-09 暂停/ReducedMotion 下 timer 仍 30 Hz 空转（P1）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcExplainer.cs:95-105`
- 证据：
  ```csharp
  if (!Reduced && _playing) { _time += ...; clockMoved = true; }
  var contentMoved = OnTick(_timer.Interval.TotalSeconds);
  if (clockMoved || contentMoved) InvalidateVisual();
  ```
- 触发/放大：暂停（transport 按钮/Space）后 `_playing=false` → 不再重绘，但 `DispatcherTimer` 仍每 33 ms 回调（只做两次判断）；reduced-motion 下同样空转。放大因子 = 实例数 × 30 Hz。
- 影响：空回调本身很便宜（每实例每秒 30 次），但它阻止了 UI 线程进入低功耗空闲，也让「真的停了」这件事在 trace/profile 上不可见。真正的问题是 §AIX-01 的「无条件重绘」，而不是这个回调。
- 修复建议：暂停时 `_timer.Stop()`，恢复时 `_timer.Start()`（并重置 `_lastTick`）；或者把 timer 改成 `CompositionTarget.Rendering` 的订阅，暂停即退订。

### AIX-10 每帧 new List/数组（P2）

- 位置：`src/Special/Arc.Avalonia.Special.AIExplainers/Controls/ArcPaymentFlow.cs:102-117`；`ArcDataFlow.cs:114`；`ArcDateReel.cs:483`
- 证据：
  ```csharp
  var lane = new List<Point> { new(centers[3], rowTop + nodeHeight), ... };   // PaymentFlow:102
  var rail = new List<Point> { ... };                                        // PaymentFlow:111
  var rows = new[] { 64.0, 152.0, 240.0 };                                   // DataFlow:114
  var presets = new[] { "This evening", "Tomorrow", "Monday" };              // DateReel:483
  ```
- 触发/放大：这些数组/列表是常量形状，却每帧重新分配并按 30 Hz 进入 Gen0；放大因子 = 30 Hz × 列表数 × 实例数。
- 修复建议：提为 `static readonly` / 实例只读字段；`Point` 列表可直接写成预计算的 `StreamGeometry`（见 AIX-03）。

## 4. 该区域设计评价与修复优先级

### 做得好的

1. **两套共享视觉助手是本仓库最成熟的抽象之一**：`SpecialVisuals.cs` 的 `Sv`/`SvDraw`（widgets）与 `ExplainInk.cs` 的 `Ink`（explainers）统一了 token 取色、文本、圆角卡片、弧线、图标与缓动；零硬编码颜色（grep 证实 0 处 `Color.Parse`/`Colors.`/`#"`）。`Ink.IconCache` 用 `ConcurrentDictionary` 缓存 `Geometry.Parse`（`ExplainInk.cs:31,247-262`）——这正是全区域其它资源（Pen/Brush/FormattedText/StreamGeometry）**没有**做对的事。
2. **Explainer 的「phase 纯函数」模型**（`ArcExplainer.cs:13-24,61-71`）让循环无缝、reduced-motion 有诚实的静止帧（poster phase），并且把 hover/press/wheel/key 的「是否需要重绘」收敛成 `bool` 返回值——这是性能友好的接口设计，只是没被基类自己的时钟利用。
3. **reduced-motion 集中实现**：`SvTween`/`SvClock`/`ArcExplainer` 三处统一处理（`SpecialVisuals.cs:261-265,301-305`、`ArcExplainer.cs:52,65`），而不是每个控件各写一遍。
4. **生命周期配对基本正确**：除 AIW-12 的一次性 timer 外，所有时钟都在 `OnDetachedFromVisualTree` 停止（`ArcExplainer.cs:86-90`、`ArcInkFluid.cs:269-273`、`ArcMorphLoader.cs:136-142` 等），未发现静态事件订阅泄漏。
5. **可访问性与键盘语义完整**：每幅图解 `Describe(...)`；`ArcBlackHoleList` 支持方向键/Space/Delete/Ctrl+Z（`:606-639`），`ArcTimeDial` 支持方向键/PageUp/Home/End（`:182-209`），widget 都有 `AutomationProperties.Name/HelpText`。
6. **有界数据**：`ArcStretchRefresh` 刷新后裁剪到 6 条（`:334-338`）、`ArcBlackHole` 删除/撤销用显式列表、`ArcSkeletonMorph` 的 stagger timer 在 detach 时统一停（`:366-374`）——没有明显的无界缓存。

### 最该先修的 3 件事

1. **给 Explainer 加「是否需要重绘」的脏标记，并把 33 ms 降到 20 Hz 或改用 `CompositionTarget.Rendering`**（AIX-01/AIX-09）。这一步同时消灭 TimeDial 的静态空转、DateReel/MultiRegionFailover 的冗余重绘，是全区域性价比最高的改动。
2. **在 `SvDraw`/`Ink` 里补上 Pen/Brush/FormattedText 缓存，并把字体查找提到 static 字段**（AIX-02）。当前 `Font` 属性每次 `TryFindResource`，`FormattedText` 每次新建——这是所有 11 幅图解 30 Hz 开销的公共乘数。
3. **MorphLoader/InkFluid 加可见性门控并让时钟在静止态停止**（AIW-01/AIW-02/AIW-10）：MorphLoader 完成态 Stop、InkFluid 用预渲染粒子贴图替代 150 次 `PushOpacity` + 每帧 2 个渐变刷。

### 设计层面的结构性问题（不是单点 bug）

- **「每帧整场景重绘」与「图解大多是静态的」这一事实不匹配**：说明式图形（TimeDial、ReasoningTree、PromptCache、DataFlow 的骨架）本质是 SVG，应当按「静态层缓存 + 动态层重绘」分层；现在所有 11 幅都用同一种「全量重画」策略。
- **时钟与可见性解耦**：16 个 timer 全部只在 attach/detach 上管理，从不检查 `IsVisible`/`EffectiveViewport`（grep 为 0），因此「一页多实例」是线性叠加；同时缺一个全仓共享的帧时钟（`CompositionTarget` 使用 0 次）。
- **共享助手只封装「怎么画」，没封装「何时重画/缓存什么」**：于是每个控件自行 `InvalidateVisual`、自行在 Render 里取色/建 Pen，重复模式散布在 20+ 文件里；`Sv` 与 `Ink` 两个助手本身也重复（两套文本/弧/缓动实现，`SvDraw.Text` vs `Ink.Text`、`SvDraw.Arc` vs `Ink.Arc`），可合并为一层。
- **widget 的更新策略是「重建整棵树」**（AIW-07），而 explainer 是「全量重绘」——两种极端都绕开了「最小失效」：前者浪费布局，后者浪费绘制。中间路线（保留实例、只改绑定属性/局部 Invalidate）在本区域尚未使用。
