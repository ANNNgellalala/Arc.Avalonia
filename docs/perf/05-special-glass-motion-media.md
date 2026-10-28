# Special.Glass + Special.Motion + Special.Media：组件设计与性能分析

## 0. 范围与覆盖

- 工程：`src/Special/Arc.Avalonia.Special.Glass`（31 个 `.cs`，10 783 行）、`Special.Motion`（27 个，6 170 行）、
  `Special.Media`（28 个，6 146 行）。共 **33 个控件**（11 + 11 + 11）、33 个 item 页，其余是共享绘制辅助。
- 全量精读：两个基类 `Special.MotionUi.cs` / `SpecialUi.cs`、`ArcGlass.cs`、`GlassMotion.cs`、`ArcGlassBackdrop.cs`，
  Motion 的 11 个控件（Dock、OrbitMenu、OrbitLogos、LiquidTabBar、SandText、SlotText、TypewriterTerminal、
  KeycapKeyboard 精读，ActionMorph/MorphNav/BookingPill 略读），Media 的 11 个控件（DotGrid、GravityWell、VoiceOrb、
  PageCurl、PhotoGrid、CoverFlow、ProductGallery、Lightbox、StickerPeel、VoiceRecorder 精读，NowPlaying 略读），
  Glass 的 11 个控件（Menu、Segmented、Card、TabBar、HoloCard、WalletStack、ShareSheet、LinkUnfurl、ControlCenter 精读，
  SheetStack、CardToArticle 略读）。另查证 gallery 的页面生命周期（`GalleryWindow.cs:28,504-513,559-583`）。
- grep 统计（与 BRIEF §2 一致并细化）：`DispatcherTimer` 18 / 2 / 2，`InvalidateVisual` 10 / 74 / 45，
  `new Pen` 6 / 59 / 59，`override Render` 4 / 2 / 1。两个"2"均为基类懒创建的 timer。
- 方法：静态分析 + 代码路径推理；未构建、未截图、未实测。所有"单帧成本"是按绘制调用数**静态计数/估算**，
  均标注触发条件与放大因子；代码无法确定的写「待确认（suspected）」。

## 1. 组件设计特征

### 1.1 控件类型与分层

- **Motion / Media：22 个控件全是裸 `Control` 自绘**，继承 `ArcMotionCanvas`（`Special.MotionUi.cs:283`）或
  `ArcSpecialCanvas`（`SpecialUi.cs:366`），`Render` 被 sealed（`Special.MotionUi.cs:591`、`SpecialUi.cs:483`），
  每帧子类 `OnRender` 整块重画。符合 `CONVENTIONS.md` 的自绘约定，但**没有子控件级重绘粒度、没有模板部件**。
- **Glass：11 个控件是组合控件**（Panel/Canvas/Border + `ArcIcon`/`TextBlock`），仅 4 个自绘辅助：
  `ArcGlassBackdrop`（`:91`）、卡片高光（`ArcGlassCard.cs:62`）、全息 foil（`ArcHoloCard.cs:19,94`）、刻度盘
  （`ArcDurationDial.cs:114`）。材质靠 `ArcGlass.Panel`（`ArcGlass.cs:104-121`）：双停渐变填充 + 三段 rim 渐变边框 +
  `BoxShadow`（`:118`）近似 backdrop-filter。

### 1.2 状态、变体与主题

- 状态多为普通字段（`ArcDock._selected`、`ArcVoiceOrb._state`），少数 `StyledProperty`（`ArcSandText.Text`、
  `ArcSlotText.Value`、`ArcGlassCard.IsOpen`）；变体用枚举/构造参数而非 CSS class（`ArcGlassSegmentedSize`、`ArcBookingStep`）。
- Glass 部分复用 foundation（`ArcButton`/`ArcIcon`），Motion/Media 完全不复用 foundation。
- 全部颜色走 token/`ArcResources`，无硬编码 hex；但每次取色都是 `TryFindResource`（BRIEF B1），而取色点密布在
  Render/OnFrame 上（SYS-02）。

### 1.3 三条动效路径并存

1. **共享帧时钟 + tween/spring（Motion/Media）**：`Animate()` 收集闭包，`EnsureFrame()` 懒建 16 ms `DispatcherTimer`
   （`Special.MotionUi.cs:483-547`、`SpecialUi.cs:427-438`），`Frame()` 推进后 `InvalidateVisual()`（`:549-576`），
   `NeedsFrame` 为假即 `StopFrame`，detach 兜底（`Special.MotionUi.cs:583-587`、`SpecialUi.cs:471-475`）；Media 另有值积分
   临界阻尼 `Spring`（`SpecialUi.cs:315-358`）。
2. **GlassTween：每个动画 new 一个 16 ms timer + Stopwatch**（`GlassMotion.cs:32,46-48`）；
   **ArcFrameLoop：每个控件一个 16 ms timer**（`:144`）。Glass 的 18 个 timer 主要来自这两类 + 业务 timer。
3. **Avalonia `Transitions`（Glass 局部）**：`ArcShareSheet.cs:1025-1042`、`ArcControlCenter.cs:539-556`、
   `ArcSheetStack.cs:364,846-847`、`ArcWalletStack.cs:652-653,762`。本身不占 timer，但每次交互 new `Transitions`（T2）。

### 1.4 reduced-motion：真降工作量，只有一个漏洞

- Motion/Media：`EnsureFrame` 在 `Reduced` 时直接 return（`Special.MotionUi.cs:523-528`、`SpecialUi.cs:429-431`），
  `Animate` 直接 apply 终值（`:485-490`），`Spring.Step` 直接 jump（`SpecialUi.cs:339-344`）→ **帧循环根本不启动**，
  不是"冻结状态但仍在 60fps InvalidateVisual"。
- Glass：`GlassTween.Run` 立即完成（`GlassMotion.cs:38-44`），`ArcFrameLoop.Start` no-op（`:151-153`），
  HoloCard/WalletStack/GlassCard 的 pointer 处理器提前 return（`ArcHoloCard.cs:734-737`、`ArcWalletStack.cs:938-941`、
  `ArcGlassCard.cs:562-567`）。降得最彻底。
- **漏洞**：`ArcMotionCanvas.OnPointerMoved` 无条件 `InvalidateVisual()`（`Special.MotionUi.cs:400-411`），既无 reduced 判断，
  也不判断命中/拖拽是否变化 → reduced 下在 Dock 上移动指针仍每帧重画 306 个背景点（见 MO-06）。Media 基类不在
  pointer-move 里 invalidate（`SpecialUi.cs:477-481`），故只有 Motion 受影响。

### 1.5 可访问性（优点）

- 每个自绘控件一个 focus stop（`Focusable/IsTabStop`）、`AutomationProperties.Name/HelpText`、2 px 焦点环
  （`Special.MotionUi.cs:130-144,293-299`）；键盘语义普遍完整（方向键/Home/End/Enter/Space/Escape，如
  `ArcDotGrid.cs:94-127`、`ArcGravityWell.cs:95-136`）；纯装饰项显式 `Focusable=false`（`ArcSandText.cs:57-59`）。
- 每个 item 页都写 When to use / Keyboard / Motion / Implementation notes，并主动交代 2D 近似
  （如 `GlassSegmentedPage.cs:45-46`）。

### 1.6 一致性：三套弹簧、两套助手、两个孪生基类

- 弹簧三份：`Spring`（`SpecialUi.cs:315-358`）、`GlassSpring`（`GlassMotion.cs:89-131`）、`SmTween+ArcMotion easings`
  （`Special.MotionUi.cs:266-275`）；dt 钳制语义不同（`GlassSpring.Update` 钳 1/30，基类 Frame 钳 0.05）。
- `Sm` 与 `SpecialUi` 大量同构：`Lift`（`Special.MotionUi.cs:59-69` vs `SpecialUi.cs:105-115`）、`Text`（`:71-100` vs
  `SpecialUi.cs:50-78`）、`FocusRing`、`Polygon/ArcSegment`、`Noise`、`Time`。两个基类约 95% 相同，差异仅在命中模型、
  tween 列表、`MeasureOverride`（`SpecialUi.cs:424` 固定尺寸 vs `Special.MotionUi.cs:350-352` 尊重无限约束）。
- 结论：高层"分层 + 状态机 + 缓动"是统一的；底层原语重复分叉，同一 bug 要修 3 处。

### 1.7 逐控件统计

图例：帧源 T=一次性 tween、S=spring 循环、L=条件/永久 60 fps 循环、A=Avalonia Transitions；detach=摘出视觉树是否停时钟。

| 控件 | 自绘 | 帧源 | 每帧主要重建 | detach | 默认持续 |
| --- | --- | --- | --- | --- | --- |
| ArcActionMorph | 是 | T | ~10 FormattedText、2 Lift、~8 Pen | 是 | 否 |
| ArcMorphNav | 是 | T | ~12 FormattedText、6 Pen | 是 | 否 |
| ArcDock | 是 | T | **306 DrawEllipse**、~12 文本、5 Icon、Lift×2 | 是 | 否（hover 全量重绘） |
| ArcLiquidTabBar | 是 | T | 3×Layout()（数组+MeasureWidth）、~6 文本、4 Icon | 是 | 否 |
| ArcOrbitMenu | 是 | T | ~20 文本、Lift×6（18 半透明矩形）、~10 Pen | 是 | 否 |
| ArcOrbitLogos | 是 | L | ~20 文本、Lift×12（36 矩形）、12 Icon | 是 | 默认不转（`_selected=0`） |
| ArcBookingPill | 是 | T | 过渡双面：~42+12+chrome 文本 | 是 | 否 |
| ArcKeycapKeyboard | 是 | L | **60 键 ×2 rect + 60 文本** | 是 | 聚焦后永久 |
| ArcSlotText | 是 | T | 每 reel 1–2 FormattedText + brush | 是 | 否 |
| ArcSandText | 是+位图 | L | **400×133≈53k 像素写** + ≤520 brush | 是 | 悬停即永久 |
| ArcTypewriterTerminal | 是 | L | 每字符 1 FormattedText+1 子串（~30/帧）+ rows List | 是 | **是** |
| ArcProductGallery | 是 | T | ~7 SampleArt、~18 文本、每帧 OKLCH 色块 | 是 | 否 |
| ArcLightboxGallery | 是 | T | 13×SampleArt、多个渐变 | 是 | 否 |
| ArcPhotoGrid | 是 | T | 动画时 **25×SampleArt** | 是 | 否 |
| ArcCoverFlow | 是 | T | **16×SampleArt** + 16 mask + List/Sort | 是 | 否 |
| ArcVoiceOrb | 是 | L | **90 段 StreamGeometry** + RadialGradient + 3 文本 | 是 | **是** |
| ArcNowPlaying | 是 | L | SampleArt + 波形 + 若干文本 | 是 | **是** |
| ArcVoiceRecorder | 是 | L | 44 bars（逐条 brush）+ 每消息 34 bars | 是 | 否 |
| ArcPageCurl | 是 | L | 2–3 SampleArt + 4 次 List/Select + 多边形裁剪 | 是 | 否 |
| ArcStickerPeel | 是 | S | 每次 Render 都 LINQ 排序 + 多次 Polygon/Clip | 是 | 否 |
| ArcDotGrid | 是 | L | **756×(new brush + Mix + DrawEllipse)** | 是 | 否 |
| ArcGravityWell | 是 | L | **36×SampleArt** + List/Sort + 36 Pen + 36 PushOpacity | 是 | 否 |
| ArcGlassMenu | 组合 | T | 每帧 `TransformOperations.Parse` 字符串 | 是 | 否 |
| ArcGlassSegmented | 组合 | T | 每帧改 Border Width/Height + Canvas 定位 | 是 | 否 |
| ArcGlassCard | 组合+自绘高光 | L | 每帧 new `Rotate3DTransform` + 高光 2 渐变 + Pen | 是 | hover 期间 |
| ArcGlassTabBar | 组合 | L | 每帧改 lens 尺寸/位置 + 每 tab Parse scale | 是 | 否 |
| ArcHoloCard | 组合+2 foil | L | 2×86 brush/Ellipse + conic/linear/radial + new TransformGroup | 是 | hover 期间 |
| ArcWalletStack | 组合 | L | 每卡 Parse translate + Opacity | 是 | 否 |
| ArcSheetStack | 组合 | A | 交互时 new Transitions | **否** | 否 |
| ArcControlCenter | 组合 | A | 每次开卡 new Transitions + Parse | **否** | 否 |
| ArcCardToArticle | 组合 | A+T | 拖动每 move Parse translate | **否** | 否 |
| ArcLinkUnfurl | 组合 | 3×timer | 16 ms shimmer 每 tick Parse translate | **否** | 加载期间 |
| ArcShareSheet | 组合 | A+3×timer | 交互时 new Transitions | 是（停 timer） | 否 |
| ArcGlassBackdrop | 是 | 无 | 静态：渐变 + 3 radial blob + Geometry.Parse | — | 否 |

## 2. 性能问题清单

### 2.1 单帧成本估算与排序（最重的 12 个）

估算口径：按 OnRender/OnFrame 静态数绘制调用（DrawRectangle/Ellipse/Text/Geometry/Line/渐变/mask）。
`SampleArt.Draw` 一次 ≈25–45 图元（`SampleArt.cs:55-136`：天空渐变 + 2–3 山体多边形 + 3–4 渐变 + 12–34 grain 椭圆 +
vignette），并做 6 次 OKLCH 转换（`:28-52`）。以下为静态估算，非实测。

| 排名 | 控件 | 触发 | 单帧图元估算 | 单帧分配估算 |
| --- | --- | --- | --- | --- |
| 1 | ArcGravityWell | 指针/脉冲，60fps | 36×30≈900–1500 | 216 次 OKLCH、List+Sort、36 Pen、36 Brush |
| 2 | ArcDotGrid | 指针/涟漪，60fps | 756 | 756 SolidColorBrush、756 次 ArcColor.Mix |
| 3 | ArcCoverFlow | spring 移动，60fps | 16×30≈480–700 | 16 SampleArt、8 渐变 mask、List+Sort |
| 4 | ArcPhotoGrid | viewer 开合，60fps | 25×30≈500–800 | 25 SampleArt、25 clip |
| 5 | ArcHoloCard | hover，60fps | 172（仅 foil）+2 渐变面板 | 172 ImmutableBrush、3 渐变、new TransformGroup |
| 6 | ArcTypewriterTerminal | **永久 60fps** | ~60–80 | ~30 FormattedText + 30 子串 + List/Row |
| 7 | ArcKeycapKeyboard | 聚焦后永久 60fps | ~180 | 60 FormattedText、60 Pen、60 Brush |
| 8 | ArcBookingPill | 步骤切换 320 ms | ~60–100 文本 | 60–100 FormattedText、2 Lift |
| 9 | ArcSandText | 悬停常驻 60fps | 1 张位图 | 213 KB/帧上传 + 53k 像素写 + ≤520 Brush |
| 10 | ArcVoiceOrb | **常驻**（默认 Listening） | ~120 | 90 段 geometry、RadialGradient、~12 Ellipse/Text |
| 11 | ArcDock | 每次 pointer move | ~330 | 306 DrawEllipse + 12 文本 + 5 Pen |
| 12 | ArcOrbitMenu | 展开/收拢 60fps | ~120 | 20 文本、18 半透明矩形、10 Pen |

### 2.2 问题表

| ID | 分类 | 严重度 | 位置 | 问题一句话 | 放大因子 |
| --- | --- | --- | --- | --- | --- |
| GM-01 | R1/R3/G1 | P0 | `src/Special/Arc.Avalonia.Special.Glass/Controls/ArcHoloCard.cs:762-799` | hover 每帧重画 2 个 foil：172 新 brush + conic 渐变 + new TransformGroup | ×2 foil ×60fps |
| MO-01 | S2/R1 | P0 | `src/Special/Arc.Avalonia.Special.Motion/Controls/ArcTypewriterTerminal.cs:80,150-162,394-418` | 默认 `_playing=true` 且时间回绕，永久 60fps；每帧每字符 1 FormattedText+1 子串 | ×~30 字符 × 永久 |
| MO-02 | R1/R3 | P0 | `src/Special/Arc.Avalonia.Special.Motion/Controls/ArcSandText.cs:88-89,274-318` | 悬停即持续 60fps；每帧软件重写 53k 像素位图并整帧上传 | ×400×133 像素 ×60fps |
| MD-01 | R1/R3 | P0 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcDotGrid.cs:174-224` | 每个点每帧 new SolidColorBrush + Mix + DrawEllipse | ×756 点 ×60fps |
| MD-02 | R3/G1 | P0 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcGravityWell.cs:187-258` | 36 个 tile 每帧各画一次完整 SampleArt + PushOpacity | ×36 ×60fps ×~30 图元 |
| MD-03 | R3/G1 | P0 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcCoverFlow.cs:243-319` | 每帧 16 次 SampleArt（图+镜像反射）+ 每卡 2 opacity mask | ×8 卡 ×60fps |
| MD-04 | R3/S2 | P0 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcVoiceOrb.cs:26,128,297-314,409-433` | 默认 Listening → 永久 60fps；每帧重建 90 段 blob + radial 渐变 | ×90 段 × 永久 |
| MD-05 | R3 | P0 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcNowPlaying.cs:31,126-140,350` | `_playing=true` 常驻 60fps，每帧重画 SampleArt 与波形 | × 永久 |
| MD-06 | R3 | P0 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcPhotoGrid.cs:404-445,455-523` | viewer 动画每帧重画 12 网格图 + 大图 + 12 胶片缩略图 | ×25 SampleArt ×60fps |
| GM-02 | L1/T2 | P1 | `src/Special/Arc.Avalonia.Special.Glass/Controls/ArcGlassTabBar.cs:617-663` | 每帧改 lens Width/Height/Canvas 定位触发布局；每 tab Parse scale | ×4–5 tab ×60fps |
| GM-03 | L1/T2 | P1 | `src/Special/Arc.Avalonia.Special.Glass/Controls/ArcGlassSegmented.cs:678-706` | 每帧改 Border 宽高与 Canvas 位置（布局而非 transform） | ×3 实例/页 ×60fps |
| GM-04 | R1/T2 | P1 | `ArcGlassMenu.cs:460-464` / `ArcWalletStack.cs:963-969` / `ArcLinkUnfurl.cs:656-662` | 每帧字符串拼 translate/scale 再 Parse | ×帧数 × 卡片数 |
| GM-05 | T2 | P1 | `src/Special/Arc.Avalonia.Special.Glass/Controls/GlassMotion.cs:32,46-48` | 每个动画 new timer+Stopwatch，不复用共享时钟 | ×同时动画数（可达 3） |
| GM-06 | T1/M1 | P1 | `src/Special/Arc.Avalonia.Special.Glass/Controls/ArcLinkUnfurl.cs:634-672` | 自建 16 ms shimmer timer（双 Tick 处理器），文件无 detach 清理 | ×60fps × 加载时长 |
| GM-07 | G1 | P1 | `src/Special/Arc.Avalonia.Special.Glass/Controls/ArcGlass.cs:78-121` | 每块玻璃=渐变填充+rim 渐变+BoxShadow；一页 6–12 个半透明/阴影层 | ×面板/inset 层数 |
| MO-03 | S2/T2 | P1 | `src/Special/Arc.Avalonia.Special.Motion/Controls/ArcBookingPill.cs:158-172,285-311` | 步骤过渡同时画 from/to 两面，日期面 21 卡×2 文本 | ×2 面 ×60fps ×320ms |
| MO-04 | T1/R3 | P1 | `src/Special/Arc.Avalonia.Special.Motion/Controls/ArcKeycapKeyboard.cs:130,260-319` | `IsFocused` 进入 NeedsFrame → 聚焦即永久 60fps，每帧 60 键 | ×60 键 × 永久 |
| MO-05 | L1/S2 | P1 | `src/Special/Arc.Avalonia.Special.Motion/Controls/ArcLiquidTabBar.cs:77-125` | 每次渲染 3 次 Layout()：分配数组 + 每 tab FormattedText 量宽 | ×3 Layout ×60fps |
| MO-06 | P1/R2 | P1 | `src/Special/Arc.Avalonia.Special.Motion/Controls/Special.MotionUi.cs:400-411` + `ArcDock.cs:202-216` | pointer-move 无条件 InvalidateVisual（reduced 也是），Dock 每次重画 306 点 | ×pointer 频率 |
| MD-07 | R1/R3 | P1 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcPageCurl.cs:456-536` | 拖动/翻页每帧 2–3 SampleArt + 4 次 Select/ToList + 半平面裁剪 | ×pointer 频率 |
| MD-08 | R3/S2 | P1 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcProductGallery.cs:237-248,476-514,613-643` | hover 透镜每帧重画主图+4 缩略图+透镜（7 SampleArt）+~18 文本 | ×pointer 频率 |
| MD-09 | R1/L1 | P1 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcStickerPeel.cs:398-400,456-505` | 每次 Render 都 LINQ 排序；每贴纸重复构造多边形 | ×每次重绘 × 贴纸数 |
| MD-10 | R1 | P1 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcVoiceRecorder.cs:416-431,516-524` | 录音时 44 根波形条逐条 new brush；回放每条消息再 34 根 | ×bars ×60fps |
| MD-11 | R3 | P1 | `src/Special/Arc.Avalonia.Special.Media/Controls/ArcLightboxGallery.cs:345-356,394-433` | viewer 动画每帧重画 12 张静态缩略图 + 大图 | ×13 SampleArt ×60fps |
| SYS-01 | T1 | P1 | `Special.MotionUi.cs:522-547` / `SpecialUi.cs:427-438` | 每控件一个 16 ms timer，无可见性/最小化门控，不与合成器对齐 | ×控件数 ×62.5Hz |
| SYS-02 | R2 | P1 | `Special.MotionUi.cs:26-31` / `SpecialUi.cs:23-31` / `ArcDock.cs:204,213,253` | Render 路径每次取 token 都 TryFindResource，无缓存 | ×每帧取色次数（数十） |
| SYS-03 | S2/R1 | P1 | `Special.MotionUi.cs:71-100` / `SpecialUi.cs:50-78` | 所有文本每帧重建 FormattedText/Typeface，无缓存 | ×每帧文本数 |
| SYS-04 | M2 | P2 | `GalleryWindow.cs:28,559-583` | 页面 content 缓存无淘汰，长会话内存线性增长 | ×访问页数 |
| SYS-05 | M1/正确性 | P2 | `ArcSandText.cs:126-130,474-479` | detach 释放位图，但重挂时 Build 不执行 → 回页可能空白 | 每次切页往返 |
| SYS-06 | T2 | P2 | `Special.MotionUi.cs:561,570` | tween 回调再 InvalidateVisual，Frame() 末尾已 invalidate | 每 tween 帧（无害） |
| GM-08 | R2 | P2 | `ArcGlassBackdrop.cs:159-186` | 每次 Render 都 Geometry.Parse(motif)，无缓存 | ×每次失效重绘 |
| MO-07 | 设计 | P2 | `ArcOrbitLogos.cs:57,65,436-452` | 构造即 `_selected=0` → 环默认不转；`RunLoop` 无人使用 | 每页首次 |
| MD-12 | L1 | P2 | `SpecialUi.cs:424` | MeasureOverride 固定 900×520 忽略 availableSize，易溢出 | ×每个实例 |

## 3. 逐条说明

### GM-01 ArcHoloCard 的 foil 每帧全量重绘（P0）

- 位置：`src/Special/Arc.Avalonia.Special.Glass/Controls/ArcHoloCard.cs:762-799`、foil 本体 `:94-183`、86 个闪耀点 `:39-54`。
- 证据：
  ```csharp
  _frontFoil.InvalidateVisual();   // :797
  _backFoil.InvalidateVisual();    // :798
  _frame.RenderTransform = new TransformGroup { new Rotate3DTransform(...), new ScaleTransform(...) }; // :781-788
  ```
- 触发/放大：每次 pointer move 启动 `_loop`（`:732-750`）→ 每 16 ms 两张 foil 各画一次：conic 9 stop + linear band +
  **86 个 `ArcGlass.Solid(...)`（每颗一个新 ImmutableSolidColorBrush）+ 86 个 DrawEllipse** + radial glare；再 new 一整套
  TransformGroup 让整卡 3D 重合成。HoloCard 页有 2 张卡（`HoloCardPage.cs:44,56`）。
- 影响：hover 期间单帧 ≈172 椭圆 + 180 brush + 3 渐变 + 1 次 3D 子树合成，是 Glass 最重单帧。
- 修复：glitter 位置 static 预计算；颜色按 alpha 分档缓存 brush；foil 缓存为 `RenderTargetBitmap` 只在 tween 结束更新；
  3D 变换每 2 帧更新一次（或复用变换对象改字段）。

### MO-01 TypewriterTerminal 永久播放 + 逐字符排版（P0）

- 位置：`src/Special/Arc.Avalonia.Special.Motion/Controls/ArcTypewriterTerminal.cs:80,142-147,150-162,394-418`。
- 证据：
  ```csharp
  _playing = !Sm.Reduced;                        // :143
  if (_time >= _total + 1.4) { _time = 0; }      // :158-161  永久回绕
  var formatted = Sm.MonoText(character.ToString(), 12, brush); // :411
  var token = text[start..end];                  // :434  每字符两次扫描 + 子串
  ```
- 触发/放大：首次 Render 里 `Build()` 后 `_playing=true`（`:179,143`），`NeedsFrame` 含 `_playing`（`:80`）→ 页面停留期间
  恒久 60fps。每帧 `BuildRows()` 新建 List<Row>+Row（`:240-261`），`DrawSyntax` 对每个已打字新建 FormattedText（~30/帧）。
- 影响：常驻 60fps + 每秒约 1 800 次 FormattedText 与子串分配。
- 修复：按 (char, tokenKind) 缓存文本；语法着色按 token 批量 DrawText；播完 `_playing=false` + StopFrame，改手动 replay。

### MO-02 SandText 悬停常驻 + 全位图重写（P0）

- 位置：`src/Special/Arc.Avalonia.Special.Motion/Controls/ArcSandText.cs:88-89,135-164,274-318`。
- 证据：
  ```csharp
  base.NeedsFrame || (!Sm.Reduced && (_pour < 1.2 || _grains.Count > 0 || _dugCount > 0 || _pointerInside)); // :88-89
  using var buffer = _bitmap.Lock();                                   // :281
  for (var y = 0; y < _rows; y++) for (var x = 0; x < _cols; x++) { ... } // :283-316
  ```
- 触发/放大：`_pointerInside` 一旦为真（`:460`），指针不移出就永久 60fps。`_cols=960/2.4=400`、`_rows=320/2.4≈133`
  （`:173-174`）→ 每帧 53 200 次像素写 + 纹理上传（`:150`），另加 ≤520 个粒子 brush（`:152-157`）。
- 影响：悬停即单核满载 + 每帧 213 KB 上传，量级高于"多画几个图元"。
- 修复：`_pointerInside` 不单独驱动帧（仅当 Dig 真正改像素时启动）；Paint 只写脏行；隐藏/reduced 停帧；
  粒子改批量单 brush。

### MD-01 DotGrid 每点新建笔刷（P0）

- 位置：`src/Special/Arc.Avalonia.Special.Media/Controls/ArcDotGrid.cs:174-224`（尤其 `:221-222`）。
- 证据：
  ```csharp
  var color = tint <= 0.01 ? baseDot : ArcColor.Mix(baseDot, accent, Math.Clamp(tint, 0, 1)); // :221
  context.DrawEllipse(SpecialUi.Solid(color), null, point, radiusAtPoint, radiusAtPoint);      // :222
  ```
- 触发/放大：960×440、Gap 22、inset 14 → 42 列 ×18 行 = **756 点/帧**（`:169-172`）；每点 sqrt+Pow+Sin，镜头内再 Mix；
  每点 new 一个 SolidColorBrush。spring 未收敛或存在涟漪即 60fps（`:149-152`），pointer-move 也会启动（`:57-62`）。
- 影响：单帧 756 次 brush 分配 + 756 次椭圆，随窗口面积线性放大，GC 与绘制调用双高。
- 修复：位置/方向预计算进数组；颜色量化 8–12 档缓存 `ImmutableSolidColorBrush`；或把点场画进缓存位图，
  lens 位移超阈值才重画。

### MD-02 GravityWell 每帧 36 次 SampleArt（P0）

- 位置：`src/Special/Arc.Avalonia.Special.Media/Controls/ArcGravityWell.cs:182-258,57-62,177-180`。
- 证据：
  ```csharp
  var layers = new List<(...)>(count);             // :187 每帧分配
  layers.Sort((a, b) => b.Pull.CompareTo(a.Pull)); // :248 每帧分配比较委托
  SampleArt.Draw(context, ..., layer.Index + 2, 12, null, false); // :255
  using (context.PushOpacity(layer.Opacity))       // :252 36 个合成层
  ```
- 触发/放大：9×4=36 tile（`:18-19`）。每次 SampleArt = PaletteFor（6 次 `ArcColor.Oklch`）+ 天空渐变 + 2–3 山体多边形 +
  12–34 grain 椭圆 + vignette（`SampleArt.cs:28-136`）→ 单帧约 36×30≈1 000 图元、216 次 OKLCH、40 个渐变、36 个 PushOpacity；
  指针/脉冲期间 60fps。
- 影响：Media 最重的一帧，OKLCH 是纯 CPU 运算且逐帧发生。
- 修复：SampleArt 结果按 (seed, 尺寸, 主题) 缓存位图；调色板与几何预计算；按 pull 档位分批避免每帧 Sort。

### MD-03 CoverFlow 每帧 16 次 SampleArt（P0）

- 位置：`src/Special/Arc.Avalonia.Special.Media/Controls/ArcCoverFlow.cs:243-319`。
- 证据：
  ```csharp
  var order = new List<(int Index, Rect Card, double Delta)>(); // :243 每帧
  order.Sort(...);                                              // :255
  SampleArt.Draw(context, art, Covers[index].Seed, 0, null, false);              // :293 图像
  SampleArt.Draw(context, new Rect(card.X, card.Y, card.Width, card.Height), ...); // :318 镜像反射
  ```
- 触发/放大：8 张卡全在 `|delta|<4.2` 内（`:247`），每卡图+反射 → **16 次/帧**；另每卡 2 个 `PushOpacityMask`
  （`:268,311`）、阴影椭圆、2 个新 LinearGradientBrush。spring 未收敛即 60fps。
- 影响：单帧 500–700 图元 + 16 次调色板生成；拖动/惯性期间持续。
- 修复：卡面与反射缓存位图；拖动中降频，惯性结束补一帧高清；mask 复用。

### MD-04 VoiceOrb 默认永久 60fps + 每帧重建 blob（P0）

- 位置：`src/Special/Arc.Avalonia.Special.Media/Controls/ArcVoiceOrb.cs:26,103,128,297-314,409-433`。
- 证据：
  ```csharp
  private ArcVoiceOrbState _state = ArcVoiceOrbState.Listening;  // :26
  protected override bool NeedsFrame => Active || _reveal < 1;   // :128
  for (var index = 0; index <= steps; index++) { ... }           // :414-430  steps = 90
  ```
- 触发/放大：默认 Listening → `Active` 恒真（`:103`），加载即 60fps 直到用户结束。每帧新建 90 段 StreamGeometry
  （`:409-433`）、RadialGradientBrush（`:300-313`）、2 层 halo、2 个 ripple ring、3–4 个 FormattedText、pill 的 Lift（3 层）。
- 影响：常驻 60fps + 每秒 5 400 个线段；单页即可让集显掉帧。
- 修复：blob 用固定 geometry 每帧改点或隔帧更新；radial 渐变按 tint 分档缓存；非 Speaking 降到 30fps；不可见 StopFrame。

### MD-05 NowPlaying 默认播放常驻 60fps（P0）

- 位置：`src/Special/Arc.Avalonia.Special.Media/Controls/ArcNowPlaying.cs:31,120-140,350`。
- 证据：
  ```csharp
  private bool _playing = true;                              // :31
  _time += dt; if (_playing) { _progress += dt; ... }        // :125-134
  ... || _playing                                             // :137-140
  ```
- 触发/放大：`_playing` 为真即每帧推进进度并重画 SampleArt 封面（`:350`）+ 波形 + 控件；曲目结束自动下一首
  （`:129-133`）→ 不自行停止。
- 修复：封面缓存位图；进度条只在百分比变化时 invalidate（1 Hz 足够）；暂停时 StopFrame。

### MD-06 PhotoGrid viewer 动画重画全部静态内容（P0）

- 位置：`src/Special/Arc.Avalonia.Special.Media/Controls/ArcPhotoGrid.cs:404-445,455-523`。
- 证据：
  ```csharp
  for (var index = 0; index < Photos.Length; index++) { ... SampleArt.Draw(context, rect, ...); } // :404-414
  SampleArt.Draw(context, rect, Photos[...].Seed, 18);                                            // :483
  for (var index = 0; index < _film.Length; index++) { SampleArt.Draw(context, thumb, ...); }      // :511-516
  ```
- 触发/放大：打开/收起 viewer 时 `_viewer` spring 以 60fps 运动（`:166-168`），每帧重画 12 网格图 + 大图 + 12 胶片
  = **25 次 SampleArt**；静态网格与动画无关却逐帧重画。
- 修复：静态网格层缓存位图（尺寸/主题不变即复用），viewer 只覆盖动画矩形；胶片缩略图缓存；动画期间隔帧重绘。

### GM-02 / GM-03 以布局属性做动画（P1）

- 位置：`src/Special/Arc.Avalonia.Special.Glass/Controls/ArcGlassTabBar.cs:617-663`、`ArcGlassSegmented.cs:678-706`。
- 证据：
  ```csharp
  _lens.Width = _activeLensWidth + stretch; _lens.Height = height;      // TabBar :635-636
  Canvas.SetLeft(_lens, _lensX.Value - (stretch / 2));                  // :638
  Canvas.SetLeft(_indicator, _left); _indicator.Width = Math.Max(1, _width); // Segmented :701-703
  ```
- 触发/放大：两处都在 60fps 帧回调里改 Width/Height/Canvas 定位 → 每帧 measure/arrange；TabBar 还每帧为 4–5 个 tab
  Parse `scale(...)` 字符串（`:657`）。Segmented 页有 3 个实例（`GlassSegmentedPage.cs:50-87`），动画 0.42 s ≈26 帧。
- 修复：改固定尺寸 + `TranslateTransform`/`ScaleTransform`（复用实例改字段）；magnification 档位预生成变换缓存。

### GM-04 / GM-05 / GM-06 Glass 的 transform 与 timer 模型（P1）

- 位置：`ArcGlassMenu.cs:460-464`、`ArcWalletStack.cs:963-969`、`ArcLinkUnfurl.cs:634-672`、`GlassMotion.cs:16-83`。
- 证据：
  ```csharp
  _panel.RenderTransform = TransformOperations.Parse($"translate({dx:0.##}px, {dy:0.##}px) scale(...)"); // GlassMenu :462-463
  _shimmerTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => { }); // LinkUnfurl :647
  _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);              // GlassMotion :32
  ```
- 触发/放大：菜单开合每帧 1 次 Parse（`:443-451`）；WalletStack hover 每帧 ×卡数；LinkUnfurl shimmer 每 16 ms 1 次
  （且 timer 先带空处理器再追加 Tick）；`GlassTween` 每次动画 new timer+Stopwatch，同页可同时 2–3 个。
  字符串插值+解析+新对象是纯分配；LinkUnfurl 没有 detach 清理，timer 能活到加载结束。
- 修复：直接复用 `TranslateTransform/ScaleTransform` 对象；shimmer 改用 `Transitions`；Glass 统一走"每控件一个帧时钟"；
  在 `OnDetachedFromVisualTree` 停掉全部 timer。

### GM-07 Glass 材质的合成层数（P1）

- 位置：`src/Special/Arc.Avalonia.Special.Glass/Controls/ArcGlass.cs:78-121,124-134`；背景 `ArcGlassBackdrop.cs:91-157`。
- 证据：
  ```csharp
  Background = GlassFill(tintKey, fillAlpha);   // 135° 双停渐变
  BorderBrush = RimBrush(tintKey);              // 三段光源渐变
  BoxShadow = ArcResources.Shadows(floating ? ArcTokens.ShadowFloating : ArcTokens.ShadowRaised); // :118
  ```
- 触发/放大：一页 = 1 层静态 backdrop（渐变 + 3 个 radial blob + 光轴 + vignette）+ 1–3 块 `Panel`（填充+rim+阴影 3 层）
  + 若干 `Inset`（半透明底+亮边）；嵌套面板见 `ArcGlassMenu.cs:173,203`、`ArcShareSheet.cs:172,499`、
  `ArcControlCenter.cs:182,203,408` → 典型 6–12 个半透明/阴影合成层。面板 transform/opacity 动画时是重合成（较便宜），
  但 GlassCard/HoloCard 每帧 invalidate 自绘高光层会使子树重新栅格化。
- 修复：静态背景/阴影缓存位图（或 composition surface）；阴影一次性栅格化；非 hover/reduced 完全不动画。

### MO-03 / MO-04 / MO-05 Motion 的三处高成本路径（P1）

- 位置：`ArcBookingPill.cs:158-172,285-311`、`ArcKeycapKeyboard.cs:130,260-319`、`ArcLiquidTabBar.cs:77-125`。
- 证据：
  ```csharp
  using (context.PushOpacity(...)) { DrawStep(context, rect, _from); } // BookingPill :164-167 过渡双面
  protected override bool NeedsFrame => base.NeedsFrame || (!Sm.Reduced && (AnyLit() || IsFocused)); // Keycap :130
  private (double[] X, double[] W, ...) Layout(int selected) { var widths = new double[Tabs.Length]; ... } // LiquidTabBar :77-79
  ```
- 触发/放大：BookingPill 320 ms morph 内同时画 from/to，日期面 21 卡×2 文本 + 12 时槽 → 约 60–100 FormattedText/帧；
  Keycap 聚焦后 `IsFocused` 使帧循环永久，每帧 60 键（14+14+13+12+7，`:18-25`）×2 矩形+1 文本；LiquidTabBar morph 每帧
  做 3 次 Layout → 6 个数组 + 选中 tab 的 `Sm.MeasureWidth`（`:82`，new FormattedText）。
- 修复：Booking 两面缓存位图只做缩放/淡入；Keycap 改为"无 lit/down 变化不需要帧"，文本按状态缓存；
  LiquidTabBar 宽度缓存，渲染只做插值。

### MO-06 pointer-move 无条件全量重绘（P1）

- 位置：`src/Special/Arc.Avalonia.Special.Motion/Controls/Special.MotionUi.cs:400-411`；示例 `ArcDock.cs:202-216`。
- 证据：
  ```csharp
  base.OnPointerMoved(e); Pointer = e.GetPosition(this); UpdateHover();
  if (PressId >= 0) { OnDrag(PressId, Pointer); }
  InvalidateVisual();   // :410 无 reduced 判断、无"状态是否变化"判断
  ```
- 触发/放大：高回报率鼠标在 Dock 上移动：每次 move 重画 `DrawCanvas` 双层 for（`:205-211`，960×380 下 34×9=**306 个
  DrawEllipse**）+ 5 个 Icon + 标签 pill + 文本。11 个 Motion 控件全部继承该行为。
- 修复：仅 `UpdateHover` 返回 true 或拖拽中才 invalidate；reduced 下 hover 不重画；静态画布缓存位图图层。

### MD-07 / MD-09 折页/贴纸的几何与 LINQ（P1）

- 位置：`src/Special/Arc.Avalonia.Special.Media/Controls/ArcPageCurl.cs:456-536`、`ArcStickerPeel.cs:398-505`。
- 证据：
  ```csharp
  var flap = folded.Select(point => SpecialUi.Reflect(point, normal, offset)).ToList();        // PageCurl :486
  var order = Enumerable.Range(0, Specs.Length).OrderBy(index => index == _selected ? 1 : 0).ToList(); // StickerPeel :398-400
  ```
- 触发/放大：PageCurl 拖动每次 pointer-move 都 invalidate（`:252-266`），每帧 2–3 次 SampleArt（`:596,604,622,630`）+
  4 次 List 分配 + 2 次 LINQ ToList + 3 个 StreamGeometry + `PushGeometryClip`；StickerPeel 的排序在**每次重绘**都执行
  （含静态帧），peel 期间每帧对 5–6 个贴纸各构造 4–6 个 List + 6–10 个几何。
- 修复：预分配数组 + 手写反射；绘制顺序只在 `_selected` 变化时算；当前跨页/贴纸内容缓存位图；拖动期间隔帧重绘。

### MD-08 / MD-11 每帧重画静态画面（P1）

- 位置：`ArcProductGallery.cs:476-514,613-643`、`ArcLightboxGallery.cs:345-356,394-433`。
- 证据：
  ```csharp
  SampleArt.Draw(context, art, Seeds[_photo], 26, Current.Hue, true);  // ProductGallery :634 透镜再画一遍
  for (var index = 0; index < Shots.Length; index++) { SampleArt.Draw(context, rect, ...); } // Lightbox :350-356
  ```
- 触发/放大：ProductGallery 指针在主图上时（`:237-248`）每帧 1–2 次主图 + 4 缩略图 + 1 透镜 ≈7 SampleArt + ~18 文本 +
  每帧 3 次 `ArcColor.Oklch`（`:528`）；Lightbox 打开 viewer 时每帧 12 张 masonry + 1 大图 = 13 SampleArt。
- 修复：主图/缩略图/网格缓存位图；透镜只重采样缓存位图的一块；Oklch 色块结果缓存。

### MD-10 VoiceRecorder 波形逐条建 brush（P1）

- 位置：`src/Special/Arc.Avalonia.Special.Media/Controls/ArcVoiceRecorder.cs:416-431,516-524`。
- 证据：
  ```csharp
  var value = 0.2 + SpecialUi.Noise((int)(_recorded * 12) * 53 + index * 11) * 0.8;   // :427
  context.DrawRectangle(SpecialUi.Solid(ArcColor.WithAlpha(accent, 0.55 + 0.45 * value)), ...); // :430
  ```
- 触发/放大：录音时 44 根/帧（每根 new brush + Alpha 计算），录音期间 60fps；回放每条消息再 34 根（`:516-524`）。
- 修复：`Noise` 已按 `(int)(_recorded*12)` 分档，波形数组可按 12 Hz 预计算；brush 分 8 档缓存；单 brush + 渐变绘制。

### SYS-01 / SYS-02 / SYS-03 平台级：timer、资源查找、文本（P1）

- 位置：`Special.MotionUi.cs:522-547,26-31,71-100`、`SpecialUi.cs:427-457,23-31,50-78`。
- 证据：
  ```csharp
  _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Frame()); // :538
  if (!NeedsFrame) { StopFrame(); }                                                                           // :572-575
  public static IBrush Brush(string token) => ArcResources.Brush(token);   // 每次 TryFindResource（BRIEF B1）
  ```
- 触发/放大：①每控件独立 Stopwatch+timer，只在 `NeedsFrame` 为假时停，**不看 IsVisible/最小化**，16 ms 与 60 Hz 不同步；
  ②渲染路径上每个 `Sm.Brush/Color/Lift` 都查资源字典（Dock 一帧 30+ 次、OrbitMenu 60–90 次）；
  ③`FormattedText`+`Typeface` 每帧重建（Dock 12、OrbitMenu 20、Keycap 60、BookingPill 60–100、PageCurl 10+）。
- 影响：正常交互可接受，但隐藏页/离屏滚动区/最小化仍唤醒 UI 线程；配合永久循环控件（MO-01/MD-04/MD-05）等于常驻 60fps。
- 修复：共享帧服务（一个 timer 驱动所有订阅者）+ `IsEffectivelyVisible` 退订；`ArcResources` 加 (scheme, accent, token)
  缓存；FormattedText 按 (text, size, weight, brushKey, maxWidth) 缓存，Typeface/CultureInfo 提为 static。

### P2（清洁度/正确性）

- `SYS-04` gallery `_builtContent` 永不淘汰（`GalleryWindow.cs:28,559-583`）：每访问一页保留整棵树与位图，长会话内存线性增长。
- `SYS-05` `ArcSandText` detach 时 dispose 位图（`:474-479`），重挂时 Build 因尺寸/文本未变不执行（`:126-130`）→ 切回该页
  可能空白（正确性问题，非性能）。
- `SYS-06` `Animate` 回调内再 `InvalidateVisual()`（如 `ArcOrbitMenu.cs:252,260`），而 `Frame()` 末尾已 invalidate
  （`Special.MotionUi.cs:570`），多一次无效请求（Avalonia 会合并，无实害）。
- `GM-08` `ArcGlassBackdrop` 每次 Render 都 `Geometry.Parse`（`:170`），可按 `ArcIcon` 模式缓存。
- `MO-07` `ArcOrbitLogos` 构造即 `_selected=0`（`:57`），`UpdateSpeed` 仅在 hover 变化时调用（`:436-452`）→ 初始环不转
  （与"缓慢旋转"文案不符）；`RunLoop()`（`Special.MotionUi.cs:511-515`）无人使用。
- `MD-12` `ArcSpecialCanvas.MeasureOverride` 固定 900×520 忽略 availableSize（`SpecialUi.cs:424`），与 Motion 版本
  （`Special.MotionUi.cs:350-352`）行为不一致，窄容器会溢出。

## 4. 该区域设计评价与修复优先级

### 做得好的

- **统一舞台架构**：22 个 Motion/Media 控件共用一个 canvas 基类，命中模型、focus stop、键盘、Automation、pointer capture、
  reduced-motion 全部收敛在基站；单控件文件只写状态与绘制——这是全仓最成熟的自绘组件范式。
- **reduced-motion 是真降成本**：帧时钟在 reduced 下根本不启动（不是"冻结状态但仍 60fps"），截图 harness 因此确定性渲染
  （`Special.MotionUi.cs:277-281` 的注释与实现一致）。
- **确定性程序化素材**：`Sm.Noise`/`SpecialUi.Noise`（`Special.MotionUi.cs:202-209`）让 SampleArt/粒子可重复，零外部资源。
- **有限循环**：除 3 个默认持续控件外，其余都以 `NeedsFrame` 精确收口；SandText 用 WriteableBitmap + 预光栅化 mask
  （`:173-209`）代替逐字符绘制，是区域里最有性能意识的实现。
- 可访问性与文档：每控件一个 focus stop、完整键盘路径、Automation 名称随状态更新；每页 Implementation notes 主动交代
  2D/定时器近似（如 `GlassSegmentedPage.cs:45-46`）；Glass 材质在明暗与 8 套 accent 下都能跟随。

### 最该先修的 3 件事

1. **给静态图像/图层加缓存**：`SampleArt.Draw` ≈25–45 图元 + 6 次 OKLCH，却被每帧调用 16–36 次（GravityWell、CoverFlow、
   PhotoGrid、PageCurl、ProductGallery、Lightbox）。按 (seed, 尺寸, 主题) 缓存成 `RenderTargetBitmap`，只做变换/透明度。
   收益：MD-02/03/06/07/08/11 一起消除，这几页单帧开销预计降一个数量级。
2. **每帧分配清零**：Pen/Brush/FormattedText/TransformOperations 全部缓存或预建（SYS-02/03、GM-04、MD-01 的 756 个 brush）。
   优先级：DotGrid 颜色分档、Typewriter 字符 FormattedText、Glass transform 对象复用。
3. **统一帧时钟 + 可见性门控**：进程级 60 Hz 帧服务驱动所有 `NeedsFrame` 控件，`IsEffectivelyVisible=false` 时退订；
   三个默认永久播放控件（TypewriterTerminal、NowPlaying、VoiceOrb）改为"可见才播/默认暂停"。

### 设计层面的结构性问题

- **三条动效路径、三套弹簧、两个孪生基类**（§1.6）：dt 钳制、reduced 分支、detach 清理要改多处；Glass 的 18 个 timer
  正是"没有共享帧服务"的直接后果。
- **没有 retained/composition 分层**：所有 surface 都是"状态变 → 整块 OnRender"，静态内容永远陪动画重画；不引入
  "静态层位图 + 动态层"约定，交互越多越慢。
- **以布局属性做动画**（GlassTabBar/GlassSegmented）与**以字符串表达 transform**（5 个文件、每帧 Parse）是两类可被
  lint/helper 统一消灭的模式。
- **缺少性能预算与自动化基准**：没有测试工程/基准（BRIEF §1），每帧图元数无人守门；建议给 Special 页加 `--perf` 采样
  （帧时间 + 分配），至少对前 3 名页面做回归。
- **默认状态即最贵状态**：VoiceOrb/NowPlaying/Typewriter 一进页就 60fps 且不停，SandText/Keycap 一悬停/聚焦就常驻；
  把"默认静止、交互才动"写成区域约定，可从根上避免"整页掉帧"。
