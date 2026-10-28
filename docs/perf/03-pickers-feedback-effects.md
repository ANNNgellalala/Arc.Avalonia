# Pickers + Feedback + Effects：组件设计与性能分析

## 0. 范围与覆盖

- 工程：`src/Arc.Avalonia.Pickers`（19 个控件 `.cs`/13 个主题）、`src/Arc.Avalonia.Feedback`
  （22 个控件 + `Internal/ArcUi.cs`）、`src/Arc.Avalonia.Effects`（11 个文件）。
- 逐行精读：Pickers 的 `ArcCalendarSurface`、`ArcCalendar`、`ArcDateMath`、`ArcPickerBase`、`ArcEmojiPicker`(480–1452)、
  `ArcEmojiData`(648–747)、`ArcSignaturePad`(1–400)、`ArcColorSurfaces`(50–410)、`ArcAvatarCropSurface`(120–401)、
  `ArcAvatarRing`(80–165)、`ArcAvatarUpload`(940–987)，主题 `ArcCalendar.axaml`、`ArcEmojiPicker.axaml`(376–456)、
  `ArcEmojiPickerStyles.axaml`；Feedback 的 `ArcDataGrid`(377–1250,1660–1720)、`ArcTreeTable`(581–700,1229–1288,1720–1799)、
  `ArcSortableDataTable`(215–273,540–590)、`ArcJsonViewer`(428–560,690–771)、`ArcAnimatedCounter`(全文)、
  `ArcCountdown`(120–240)、`ArcProgress`(300–422)、`ArcSkeleton`(200–254)、`ArcPresenceStack`(1020–1084)、
  `ArcActivityHeatmap`(339–450,530–549)、`ArcCodeBlock`(102–270)、`Internal/ArcUi.cs`(240–309)、`ArcDataGrid.axaml`；
  Effects 的 `ArcEffectVisuals`(全文，含 `ArcTween`/`ArcViewport`/`ArcDismiss`)、`ArcTextShimmer`(全文)、
  `ArcTextReveal`(全文)、`ArcInViewTitle`(90–438)、`ArcAnnouncementBar`(200–243,620–767)、`ArcBottomSheet`(240–369)、
  `ArcExpandableCard`(180–300)。
- grep 全量扫描：`DispatcherTimer`、`InvalidateVisual`、`override void Render`、`new SolidColorBrush`、`new FormattedText`、
  `new Pen(`、`ArcResources.`、`OnAttached/OnDetached`、`ReducedMotion`、`AutomationProperties`、`ItemsPanel`/`Virtualiz`。
- **timer 口径修正**：BRIEF §2 的 Pickers 6 / Feedback 17 / Effects 14 按“动画循环主体”计；按 `new DispatcherTimer`
  字面实测为 **Pickers 3 / Feedback 8 / Effects 7**（共 18 处创建点）。差异来自 `ArcTween` 这类“一个循环类、
  每实例一个 timer”的助手——实例数远大于创建点数，量级判断成立，但本文计数以字面为准。

## 1. 组件设计特征

### 1.1 控件类型分布

| 区域 | 公开控件 | 基类分布 | 自绘裸 `Control` |
| --- | --- | --- | --- |
| Pickers | 10 | `TemplatedControl` 10（4 个派生自 `ArcPickerBase`） | 6（CalendarSurface、ColorArea、ColorRamp、SignatureSurface、AvatarCropSurface、AvatarRing） |
| Feedback | 23 | `TemplatedControl` 19、`ContentControl` 1、`Button` 2、`ArcCard` 派生 1 | 3（ProgressTrack、UsageBar、HeatmapGrid） |
| Effects | 9 | `TemplatedControl` 6、`ContentControl` 3（BottomSheet/HoverCard/ExpandableCard） | 0 |

选择基本正确：像素多、语义少的（日格、色域、签名、热量图）自绘；要变体/焦点/换肤的走模板控件。
`ArcCalendar` 把网格拆成 `ArcCalendarSurface` 一个自绘面，模板里只放 1 个面 + 3 个 Button
（`Themes/ArcCalendar.axaml:38`），**没有 42 个日格按钮**。

### 1.2 状态与组合

- 变体走 CSS class：`Classes.Set("dragover", IsDragOver)`（`ArcFileDropzone.cs:267`）、
  `Classes.Set("indeterminate", …)`（`ArcProgress.cs:305`）。
- 共享基类做得好：`ArcPickerBase`（`ArcPickerBase.cs:15`）收敛 trigger/Popup、外部点击关闭、Esc 回焦、automation 命名，
  4 个 picker 派生；`OnApplyTemplate` 先 `-=` 再 `+=`（:77–109），模板重套不重复订阅。
- 模板部件统一 `PART_*`，在 `OnApplyTemplate` 一次解析并缓存字段。
- StyledProperty 密度：Pickers 118 / Feedback 175 / Effects 54。Effects 把“每帧值”放普通字段
  （`_wipeWidth`、`_angle`）避免动画写属性；`ArcAvatarRing` 相反（字段 + 每帧 `InvalidateVisual`，见 PF-03）。

### 1.3 动效路线与 reduced-motion

三条路线并存：① 模板内 `Transitions`（`ArcEmojiPickerStyles.axaml:9-13` 的 `BrushTransition`）；
② 共享 tween（Feedback `Internal/ArcUi.cs:266-308`、Effects `ArcEffectVisuals.cs:125-184`，都是
“每实例一个 16 ms timer + Stopwatch、结束自停、不常驻”）；③ 手写常驻 timer（`ArcTextShimmer`、`ArcAvatarRing`、
`ArcSkeleton`、`ArcProgress`、`ArcPresenceStack`）。

reduced-motion 覆盖差异极大（`ReducedMotion` 出现：Pickers 3 / Feedback 8 / Effects 25）。Effects 9 个控件全覆盖，
且 `ArcTween.Run` 在 `ReducedMotion` 时直接 `apply(1)`（`ArcEffectVisuals.cs:146-151`），是全仓最规范的一处。
Pickers 只有 3 处，且 `ArcAvatarRing.cs:104` 把 reduced-motion 实现成“6°/帧降到 1.5°/帧”——**语义错了，
应当是停止**。`ArcCalendar` 月份切换硬切无动画，可接受。

### 1.4 可访问性

`AutomationProperties` 共 179 处（Pickers 49 / Feedback 87 / Effects 43）。日历单 tab stop + 方向键/Home/End/PageUp/Down
+ 完整键位写进 `HelpText`（`ArcCalendar.cs:501-504`）；表格有 `"Sort by X, currently descending"`
（`ArcDataGrid.cs:798-803`）、`"Shift-click checks the whole range"`（:1105）；树表每行拼 `", expanded"/", leaf"`
（`ArcTreeTable.cs:1437-1441`）；表情格每格 `ToolTip.Tip` + `AutomationProperties.Name`
（`ArcEmojiPicker.axaml:408-409`）。缺口：自绘面本身没有逐格语义（只有容器名），读屏无法逐日浏览——属平台近似，
应在文档承认。

### 1.5 一致性与复用

零硬编码 hex（三工程 grep `"#RRGGBB"` 均为 0）。Feedback 有强复用层 `ArcUi`（20 个控件引用 150 次：
`Text/Icon/Pill/Input/Row/Caption/Numeric/AddRun` + `ArcTween`）；Effects 有 `ArcEffectVisuals`。
**Pickers 没有视觉复用层**，只有 `ArcDateMath`/`ArcColorMath`/`ArcEmojiData` 三个数据助手，TextBlock/Border 组装
在控件内重复（`ArcColorEditor.cs:308`、`ArcFileDropzone.cs:499-558`、`ArcSignaturePad.cs:753-780`），
`ArcResources.*` 也因此散落 62 处。

## 2. 性能问题清单

| ID | 分类 | 严重度 | 位置 | 问题一句话 | 放大因子 |
| --- | --- | --- | --- | --- | --- |
| PF-01 | R1+R3+P1 | P0 | `Pickers/Controls/ArcSignaturePad.cs:346` | 每次 pointer move 重画全部笔画，逐笔重建数组 + `StreamGeometry` | × 已采样点数（0.6 px/点，1000 px 挥笔≈1600）× 笔数 |
| PF-02 | S2+R2+R3 | P0 | `Pickers/Controls/ArcCalendarSurface.cs:305` | 每次 Render 重建 49 个 `FormattedText`+7 次资源查找+42 次 `PushOpacity`；hover 每跨格整面重绘 | × 每秒跨格数（10–30）× 双月日历 ×2 |
| PF-03 | T1+R1 | P0 | `Pickers/Controls/ArcAvatarRing.cs:101` | 不确定态常驻 16 ms timer，每帧 2 个 `new Pen` + `StreamGeometry` + 资源查找 | × 实例数 × 60 fps |
| PF-04 | S2+R2 | P1 | `Pickers/Controls/ArcDateRangePicker.cs:359` | hover 预览一次移动写 2×12 属性 → 2 次整面重绘 + 页脚/预设重算 | × 2 个日历 × 跨格 |
| PF-05 | R1+S1 | P1 | `Pickers/Controls/ArcEmojiPicker.cs:499` | 每个键入字符重建 418 个 cell + 行对象 + 换 `ItemsSource`；`Filter` 内层逐项小写化 | × 418/键 × 7 个分类重复扫描 |
| PF-06 | R1+R2+G1 | P1 | `Pickers/Controls/ArcColorSurfaces.cs:61` | 色域拖动每次 move 重建 3 个画刷（2 组 GradientStop）+3 Pen+4 次资源查找 | × 每次 move（拖动 1 s≈60 次） |
| PF-07 | R1+R3 | P1 | `Pickers/Controls/ArcAvatarCropSurface.cs:357` | 裁剪拖动每次 move 重建 `SolidColorBrush`+`GeometryGroup`+2 几何+全图重绘 | × pointer move |
| PF-08 | P1 | P1 | `Pickers/Controls/ArcCalendar.cs:423` | 每次 move 无条件 `new Cursor` 并写属性（在 hover 早退之前） | × 每次 move，即使日期未变 |
| PF-09 | L2 | P2 | `Pickers/Themes/ArcEmojiPicker.axaml:402` | 虚拟化只有行级；每格 4 控件+ToolTip+绑定，且每个格子都带 `BrushTransition` | × 实现行数（≈10）× ≤14 格 |
| PF-10 | R1 | P2 | `Pickers/Controls/ArcEmojiPicker.cs:1078` | 滚动同步分类每次 `Cast().ToArray()`（即使分类没变） | × ScrollChanged 事件数 |
| PF-11 | R1 | P2 | `Pickers/Controls/ArcAvatarUpload.cs:978` | settle 动效每帧 `new ScaleTransform` | × 30 帧 |
| FB-01 | L2 | P0 | `Feedback/Controls/ArcDataGrid.cs:1082` | 表格/树表/可排序表零虚拟化：StackPanel+每行 Grid+每格 Border/TextBlock | × 行×列（100×8≈800 控件/构建） |
| FB-02 | L1+L2 | P0 | `Feedback/Controls/ArcDataGrid.cs:1366` | 选中/排序/编辑提交都 `RebuildContent()` 重建整表 | × 行×列×每次点选 |
| FB-03 | R2+S2 | P1 | `Feedback/Controls/ArcDataGrid.cs:1149` | 每格一次 `ArcResources.Double`（无缓存）+ automation 插值；每行重算 `VisibleColumns()` | × 行×列 |
| FB-04 | L1+P1 | P1 | `Feedback/Controls/ArcDataGrid.cs:882` | 列宽拖动每次 move 遍历所有行的 `ColumnDefinition` → 全表重排 | × 行数 × move |
| FB-05 | L2+R3 | P1 | `Feedback/Controls/ArcJsonViewer.cs:711` | 展开/折叠重建整棵树；每行 `ArcIconButton`+6 列 Grid；每行 `GetRawText()` 复制子树 | × 可见行；× 子树字节 |
| FB-06 | S2+R2+L1 | P1 | `Feedback/Controls/ArcAnimatedCounter.cs:210` | 滚动每帧 `new FormattedText`+`new Typeface`+`FindResource`+写 `MinWidth` | × 42 帧/次 × 可见计数器数 |
| FB-07 | T1 | P1 | `Feedback/Internal/ArcUi.cs:284` | 每个数值动效一个 16 ms timer；Progress 16/ Skeleton 40/ Presence 16/ Countdown 250 各自独立 | × 动画控件数（同屏 5 个=5 个 60 Hz） |
| FB-08 | T1+G1 | P1 | `Feedback/Controls/ArcSkeleton.cs:242` | 骨架 25 fps 逐块改 `Opacity`；打字指示 60 fps 改透明度+位移；无离屏暂停 | × 块数（≈20）× 25 fps；× 实例 |
| FB-09 | R3+P1 | P1 | `Feedback/Controls/ArcActivityHeatmap.cs:339` | hover 每跨一格整面重绘（≈364 格）+ tooltip `FormattedText`+资源查找 | × 格数 × 跨格数 |
| FB-10 | M1+S2 | P2 | `Feedback/Controls/ArcCodeBlock.cs:257` | 每 token 一个 `Run` + 一个 dynamic-resource observable 订阅 | × token 数（40 行≈300–400） |
| FB-11 | R1+S2 | P2 | `Feedback/Controls/ArcCountdown.cs:153` | 250 ms 无条件 `Render()`，`Children.Clear()` 后重建时间块 | × 4 次/秒 × 实例 |
| FB-12 | R2+R3 | P2 | `Feedback/Controls/ArcUsageMeter.cs:398` | 每段一次资源查找 + 一次 `PushOpacity` | × 段数 |
| FB-13 | R2 | P2 | `Feedback/Controls/ArcDataGrid.cs:1674` | 键盘移格遍历全部 cell frame 重置边框 | × 行×列/方向键 |
| FB-14 | R1 | P2 | `Feedback/Controls/ArcTreeTable.cs:611` | 每次 Flatten 递归重建全树索引（含折叠子树） | × 全部节点数/次展开 |
| EF-01 | T1+R1+R2+S2 | P0 | `Effects/Controls/ArcTextShimmer.cs:174` | 常驻 16 ms timer，每帧 2 次资源查找+List+2 次 Mix+新渐变画刷+5 个 stop+LINQ 排序 | × 实例数 × 60 fps |
| EF-02 | M1+P1 | P1 | `Effects/Controls/ArcInViewTitle.cs:285` | `ArcViewport.Watch` 返回值被丢弃 → `ScrollChanged`/`LayoutUpdated` 订阅永不解除，`Arm()` 会叠加 watcher | × 每次属性变更/重套模板 |
| EF-03 | L1 | P1 | `Effects/Controls/ArcBottomSheet.cs:358` | 抽屉/折叠卡逐帧改 `Height`/`Width` → 每帧整棵内容重新 Measure | × 内容复杂度 × 24–42 帧 |
| EF-04 | R1+T2 | P1 | `Effects/Controls/ArcTextReveal.cs:270` | 每帧逐 word/char 写 `Opacity`+`TranslateTransform.Y`；字符模式每字符一个 TextBlock | × 单元数（8–60）× 60 fps |
| EF-05 | L1 | P1 | `Effects/Controls/ArcAnnouncementBar.cs:719` | 进度条 50 ms timer 逐次写 `_progressFill.Width` → 20 次/秒布局 | × 20/s × 可见公告条 |
| EF-06 | T2 | P2 | `Effects/Controls/ArcEffectVisuals.cs:84` | 每个新控件 `new Transitions`+`TransformOperations.Parse` 字符串 | × 旋转控件数 × 状态切换 |
| EF-07 | M1 | P2 | `Effects/Controls/ArcEffectVisuals.cs:301` | `ArcViewport` 用全局 `LayoutUpdated` 兜底，未播放实例每次布局都 `Evaluate` | × 未进视口实例 × 布局次数 |

## 3. 逐条说明

### PF-01 SignaturePad 每次 move 重画全部笔画并重建几何（P0）

- 位置：`src/Arc.Avalonia.Pickers/Controls/ArcSignaturePad.cs:346`（触发）、`:225-303`（代价）
- 证据：
  ```csharp
  _drawing.Points.Add(new ArcInkPoint(point.X, point.Y, BaseWidth() * factor));  // :344 采样（阈值 0.6 px，:337）
  InvalidateVisual();                                                            // :346
  var widths = new double[points.Count];                                         // :242
  var left = new Point[points.Count]; var right = new Point[points.Count];       // :255-256
  var outline = new StreamGeometry();                                            // :278
  ```
- 触发/放大：捕获期间每次 `PointerMoved` → 一次 `Render`，而 `Render` 会重画**已完成**的笔画（:176-187）。
  每次分配 3 个数组 + 1 个 `StreamGeometry` + 1 个 `SolidColorBrush`（:233）+ 每笔一次 `Ink()`
  （Black 分支走到 `ArcResources.Color`，:221，即每笔每帧一次资源查找）。5 笔签满一屏时单帧 >8000 个点。
- 影响：签名手感随笔画数线性变差，是 Pickers 最严重的单点。
- 修复：`ArcInkStroke` 缓存 `Geometry`（完成时建一次）；live 笔画只重建尾部；`Pen`/`SolidColorBrush` 按颜色缓存为字段。
- 补充：没有无界增长（`_history` 只存引用，:404）；**没有曲线平滑**——只有宽度 ±2 点均值（:243-252）+ 法线偏移折线，
  所以“平滑”是速度变宽，不是线形平滑。

### PF-02 日历：无逐日控件（好），但一次 Render 分配 49 个 FormattedText，hover 每跨格整面重绘（P0）

- 位置：`ArcCalendarSurface.cs:208-325`、`:327-332`；触发在 `ArcCalendar.cs:414-432`
- 证据：
  ```csharp
  var text = Text(date.Day.ToString(ArcDateMath.Culture), 14, brush, face, cellWidth);  // :305
  context.DrawText(text, new Point(cell.X, cell.Y + (cellHeight - text.Height) / 2));  // :306
  ```
- 回答任务问题：**切换月份不重建任何子控件**（模板只有 1 个 surface + 3 个 Button）；**没有“每天一个 Button”**，
  42 格是一次 `Render` 循环；**日期比较不用 LINQ**，每格 4 次 `SameDay`（:268-272）+ 1 次 `IsSelectable`
  （:267，即用户 `DisabledDates` 委托每格调一次）。这是正确选择。
- 放大：`AffectsRender` 含 `HoverDateProperty`（:63-76），`OnSurfaceMoved` 每跨格写 `HoverDate`（`ArcCalendar.cs:430`）
  → 每次跨格整面重绘：49 个 `FormattedText`、7 次 `ArcResources.Brush`（:222-228）、每格一次 `PushOpacity`（:296）、
  每格一次 `DateTime.Today`（:272）、42 次 `date.Day.ToString` 分配（:305）。扫过 6 周≈40 次全量重绘，
  双月 `DateRangePicker` 翻倍（PF-04）。
- 修复：① 42 格文本按 (day, brush, typeface) 预生成/缓存（只有 1–31 + 7 个字头）；② 7 个画刷缓存为字段，
  主题变更失效（对应 B1/B4）；③ 用预先乘 alpha 的 3 个 muted 画刷替换每格 `PushOpacity`（只有 0.45/0.35/1.0 三档）；
  ④ `DateTime.Today` 提到循环外；⑤ `Weeks`/`FirstVisible`（:218-219）各算一次 `MonthGrid`，合并。

### PF-03 AvatarRing：每实例常驻 16 ms timer，每帧新 Pen+新几何（P0）

- 位置：`ArcAvatarRing.cs:101-107`、`:110-160`
- 证据：
  ```csharp
  _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };            // :101
  var track = new Pen(ArcResources.Brush(ArcTokens.Border), thickness);                 // :121
  var stroke = Stroke ?? ArcResources.Brush(ArcTokens.Accent);                          // :124
  ```
- 放大：不确定态一直转，每帧 2 个 `Pen`+1–2 次资源查找+1 个 `StreamGeometry`（:155），而除 `_angle` 外全部不变。
  × 实例数 × 60 fps（同页 4 个上传头像=4 个 60 Hz timer）。
- 影响：与 foundation `ArcSpinner`（BRIEF B2）是同一实现的两份拷贝。
- 修复：复用 `ArcSpinner`，或把 timer 换成静态共享 ticker；`Pen`/画刷缓存；用旋转变换代替重画弧。
  reduced-motion 分支应按“停止”写（`ArcAvatarRing.cs:104` 现在是慢放）。

### PF-04 DateRangePicker 把日历 hover 成本 ×2 并附带字符串格式化（P1）

- 位置：`ArcDateRangePicker.cs:359-377`
- 证据：
  ```csharp
  foreach (var calendar in new[] { _left, _right })   // :367 每次调用分配数组
  { calendar.RangeStart = _draftStart; calendar.RangeEnd = previewEnd; calendar.SelectedDate = null; }  // :374-376
  ```
- 放大：`OnCalendarHover`（:206-214）→ `UpdateDraft()` → 两个日历各 `UpdateSurface()`（`ArcCalendar.cs:460-505`
  写 12 个属性）→ 2 次整面重绘。另有 `_footer.Text` 每跨格 `ArcDateMath.Range` 重拼（:381-388）、
  `preset.Create(DateTime.Today)` 每项新建（:397）。
- 修复：hover 只改 `RangeEnd`；footer/预设刷新挪到选中变化时；`new[] {…}` 换两个 if。

### PF-05 EmojiPicker：搜索每键全量重建 418 项，`Filter` 内层逐项小写化（P1）

- 位置：`ArcEmojiPicker.cs:499-503`、`:757-824`；`ArcEmojiData.cs:666-714`
- 证据：
  ```csharp
  private void OnSearchChanged() { SyncSearchBox(); RebuildRows(resetActive: true, keepCategory: true); }  // :499-503
  _grid.ItemsSource = _rows.ToArray();                     // :824
  var needle = word.ToLowerInvariant();                    // ArcEmojiData.cs:684 ← 在逐 emoji 循环内
  ```
- 回答任务问题：**虚拟化是真的，但只是行级**——`PART_Grid` 是 `ListBox` 且未覆盖 `ItemsPanel`
  （`ArcEmojiPicker.axaml:378-385`），走默认 `VirtualizingStackPanel`；每行内是**非虚拟化水平 StackPanel**
  （:395-400）。行数≈42（418/10 列），可视 8–10 行 → 实际实例化≈100 格，尚可。
  **搜索每键全量重建**：`RebuildRows` 为每个命中项 `new ArcEmojiCell`（:796）再换 `ItemsSource`，无 debounce；
  `Filter` 按分类调用 7 次（:774-781）、每次 `query.Split`；`ToLowerInvariant` 在最内层 → 单键 418 次字符串分配。
- 修复：`words/needle` 在 `Filter` 外算一次传入；重建加 60–120 ms 防抖；命中集未变时跳过；纯搜索只换 `Cells`。
- 滚动同步相反**开销很低**：`CategoryAtOffset`（:1164-1189）二分查找 `_rowTops`，`_jumping` 抑制跳转期间同步（:1198）；
  小瑕疵是 `SetActiveCategory` 每次先 `Cast().ToArray()`（:1078）再判断索引。

### PF-06 取色器拖动：每次 move 重建 3 画刷+3 Pen+4 次资源查找（P1）

- 位置：`ArcColorSurfaces.cs:61-115`、`:217-235`（触发）、`:308-385`（ramp）
- 证据：
  ```csharp
  var hue = new SolidColorBrush(new ArcHsv(Hue, 1, 1, 1).ToColor());                    // :71
  var white = new LinearGradientBrush { … new GradientStop(…), new GradientStop(…) };   // :77-86
  context.DrawRectangle(null, new Pen(ArcResources.Brush(ArcTokens.Border), 1), rounded);  // :102
  ```
- 放大：拖动期间每次 `PointerMoved` → `UpdateFromPoint` → `InvalidateVisual`（:231-234）→ 整面重绘。
  **拖 SV 面时 `Hue` 是常量**，但 3 个画刷每帧重建；拖 hue/alpha ramp 时 7 段渐变（:323-334）与棋盘格
  （:341-358，≈40 次 `DrawRectangle`）每帧重画，而它们只依赖 `Mode`。
- 修复：不依赖拖动值的绘制缓存为 `DrawingGroup`/字段画刷，`Hue`/`Mode`/主题变化时失效；`Pen` 全静态缓存。

### PF-07 Avatar 裁剪拖动：每次 move 重建画刷+几何组+全图重绘（P1）

- 位置：`ArcAvatarCropSurface.cs:148-165`、`:357-399`
- 证据：
  ```csharp
  var dimBrush = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(scrim.A * alpha, 0, 255), …));  // :386-390
  var mask = new GeometryGroup { FillRule = FillRule.EvenOdd };   // :392
  ```
- 放大：每 move 一次 640 px 图缩放重绘 + 全尺寸 EvenOdd 路径填充 + 4 次分配 + 2 次资源查找；`alpha` 只有 0.62/1.0
  两档，`mask` 只依赖 `Bounds`/直径（拖动期间不变）。
- 修复：`mask` 缓存为字段；两个 alpha 画刷预建；拖动只改平移。

### PF-08 日历 pointer move 无条件 `new Cursor`（P1）

- 位置：`ArcCalendar.cs:414-432`、`:446-447`
- 证据：
  ```csharp
  SetCursorShape(next.HasValue && IsSelectable(next.Value));  // :423 ← 在 hover 早退之前
  if (_hover == next) { return; }                             // :424
  private void SetCursorShape(bool hand) => Cursor = hand ? new Cursor(StandardCursorType.Hand) : Cursor.Default;  // :446
  ```
- 放大：网格内每次 move 分配一个 `Cursor` 并写一次属性（引用不同→属性变更→顶层光标更新）。横扫 6 周≈200 次，
  绝大多数与上一次相同。修复：两个静态 readonly `Cursor` 缓存 + 仅在形状变化时赋值。

### FB-01 DataGrid / TreeTable / SortableDataTable 零虚拟化（P0）

- 位置：`ArcDataGrid.cs:1082-1240`、`ArcTreeTable.cs:1258-1266`、`ArcSortableDataTable.cs:259-273`
- 证据：
  ```csharp
  var rows = new StackPanel();                                     // ArcDataGrid.cs:1082
  for (var index = 0; index < _view.Count; index++) rows.Children.Add(BuildRow(_view[index], index));  // :1083-1086
  ```
- 回答任务问题：**逐行 new 控件，无模板化、无虚拟化**。每行 = `Grid`(N 个 ColumnDefinition) + 每列
  `Border`+`TextBlock`+`AutomationProperties.Name` 插值，可选中时再加复选框（`BuildCheck`，:1241+）。
  100×8≈800+ 控件、800 次资源查找、800 次插值，一次构建全做完。`TreeTable` 相同（每行还有 chevron `ArcIconButton`）。
  行高固定 40（:128），完全具备虚拟化条件。
- 修复：`ItemsControl`+`VirtualizingStackPanel`（固定行高可开 snap points），行模板进 `DataTemplate`；
  `TreeTable` 的 `_display` 已经是扁平行集合，直接喂虚拟化列表即可。
- 做得好的一点：表头是独立 `PART_HeaderHost`（`ArcDataGrid.axaml:31-36`，在 ScrollViewer 之外），
  **吸顶表头不需要滚动同步**，结构选择正确。

### FB-02 交互即整表重建（P0）

- 位置：`ArcDataGrid.cs:1366`（点选）、`:985`（排序）、`:1607`（提交编辑）、`:1327`（全选）、`:388-405`（RebuildContent）；
  `ArcSortableDataTable.cs:549/565/588`；`ArcTreeTable.cs:1742/1777/1833`
- 证据：
  ```csharp
  private void ToggleRow(int viewIndex, bool shift) { … RebuildContent(); }   // :1366
  private void RebuildContent() { BuildView(); _layoutGrids.Clear(); … BuildHeader(); BuildBody(); BuildFooter(); }  // :388-405
  ```
- 放大：勾选一行 = 100 行 800 个控件重建 + 完整布局；树表展开一个节点 = 全可见行重建（再叠加 FB-14 全树递归）。
- 对比：键盘游标走 `RefreshActiveCell`（:1672-1684）只用 `_cellFrames` 改边框、不重建——作者知道增量写法，
  只是点选/排序/编辑没走。修复：选择变化只更新受影响行（`_cellFrames` 可反查）；排序只重建 body；
  编辑提交只刷新被编辑行。

### FB-03 DataGrid 每格资源查找 + 字符串插值，每行重算列集合（P1）

- 位置：`ArcDataGrid.cs:1093`、`:1123`、`:1138`、`:1149`
- 证据：
  ```csharp
  var visible = VisibleColumns();                                  // :1093 ← BuildRow 内，每行一次 Where().ToList()
  var text = CellText(value, column);                              // :1123
  AutomationProperties.SetName(cell, $"{column.Header}: {text}");   // :1138
  FontSize = ArcResources.Double(ArcTokens.TextSm),                 // :1149  ← 每格一次 TryFindResource
  ```
- 放大：`VisibleColumns()` 每次分配新 `List`（:407-408），`BuildBody` 每行调一次 → O(行) 个 List；
  `ArcResources.Double` 无缓存（B1）→ O(行×列) 次 `TryFindResource`。
- 修复：列集合算一次传参；token 值提为字段并在主题变化时刷；automation 名称按需更新。

### FB-04 列宽拖动逐 move 全表重排（P1）

- 位置：`ArcDataGrid.cs:882-892`、`:941-958`
- 证据：
  ```csharp
  handle.PointerMoved += (_, args) => { … ApplyColumnWidth(column, width); };  // :882-891
  foreach (var grid in _layoutGrids) { … grid.ColumnDefinitions[definitionIndex].Width = new GridLength(column.Width, …); }  // :951-957
  ```
- 放大：每次 move 写 O(行) 个 `GridLength` → 每个 `Grid` 重新 Measure/Arrange。50 行×60 次/s=3000 次行布局/s。
- 修复：拖动期间只动表头 + 一条幽灵线，`PointerReleased` 一次性写全部行；或 `SharedSizeGroup` 让列宽自然传播。

### FB-05 JsonViewer：展开一个节点重建整棵树，且每行复制子树文本（P1）

- 位置：`ArcJsonViewer.cs:711-725`（Toggle）、`:428-453`（Refresh）、`:508-560`（RenderRows）、`:455-506`（AddRow）
- 证据：
  ```csharp
  if (!_expanded.Remove(row.Path)) { _expanded.Add(row.Path); }  // :718-721
  Refresh();                                                     // :723 → _tree.Children.Clear() + 重建所有行
  CopyText = element.GetRawText()                                // :472 ← branch 行等于整个子树文本
  ```
- 回答任务问题：**解析**是一次性的（`JsonDocument.Parse` + `RootElement.Clone()`，:759-763，克隆正确避免了
  `using` 释放后的悬垂）；**树构建**是递归 `AddRow`，只加展开节点并按 `PageSize` 截断（:480-505）→ 默认深度 1 的
  大 JSON 初始化成本可控。但**折叠/展开不增量**：重建所有可见行（`Border`+6 列 `Grid`+`ArcIconButton`，:517-560），
  一行≈10 控件。`GetRawText()` 使深树的内存/分配为 O(Σ 子树大小)。
- 修复：展开只插入/移除该节点子树的行控件（保留 `JsonRow.Frame` 引用）；`CopyText` 懒加载；行模板化+虚拟化。

### FB-06 AnimatedCounter 数字滚动每帧排版（P1）

- 位置：`ArcAnimatedCounter.cs:166-217`
- 证据：
  ```csharp
  _valueText.Text = BuildText(value);          // :174
  var width = MeasureWidth(_valueText.Text);   // :175  → 每帧写 MinWidth（布局失效）
  var formatted = new FormattedText(text, CultureInfo.InvariantCulture, …, typeface, size, Brushes.White);  // :210
  var family = Application.Current?.FindResource(ArcTokens.FontDisplay) as FontFamily ?? FontFamily.Default;  // :207
  ```
- 放大：每次数值变化 700 ms（`ArcUi.ArcTween` 16 ms）≈42 帧；每帧 `BuildText`（`new string('0', decimals)`+
  `ToString`）+ `new Typeface` + `new FormattedText` + `FindResource` + 写 `MinWidth`（L1）。× 同屏计数器数（4–8）。
- 修复：`MinWidth` 只在动画结束时算一次；格式串按 `decimals` 缓存；`FormattedText` 用 `TextLayout` 缓存或直接读
  `_valueText.Bounds.Width`；`Typeface`/`FontFamily` 提字段。

### FB-07 / FB-08 Feedback 的定时器组合：每实例 16 ms + 常驻 25–60 fps 动画（P1）

- 位置：`Internal/ArcUi.cs:284`、`ArcProgress.cs:327`、`ArcSkeleton.cs:242`、`ArcPresenceStack.cs:1051`、
  `ArcCountdown.cs:153/362`、`ArcCodeBlock.cs:390`、`ArcJsonViewer.cs:868`
- 证据：
  ```csharp
  timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => { … });  // ArcUi.cs:284
  _pulse = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Render, (_, _) => { … }); // ArcSkeleton.cs:242
  ```
- 好消息：`ArcTween` 设计正确（Run 开头 `Stop()`、结束自停、reduced-motion 短路，:270-302），
  `ArcProgress`/`ArcSkeleton`/`ArcCountdown` 都在 `OnDetachedFromVisualTree` 里 Stop（`ArcProgress.cs:179-183`、
  `ArcSkeleton.cs:113-116`、`ArcCountdown.cs:127-134`）——**没有泄漏**。
- 问题在“一控件一 timer”：同屏 5 个进行中动效 = 5 个 60 Hz 定时器；`ArcSkeleton` 25 fps 逐块写 `Opacity`
  （一屏 20 块→500 次/s，且 `<1` 的不透明度每块走独立合成层），`ArcPresenceStack` 60 fps 改 pill 透明度+逐点位移；
  两者都没有可见性门控（离屏/隐藏照跑）。
- 修复：`ArcTween` 改全局单例 ticker（一个 DispatcherTimer + Job 列表，空则停）；Skeleton 用 `Animation`；
  两处加 `IsEffectivelyVisible`/`Bounds` 门控。

### FB-09 热量图 hover 全量重绘（P1）

- 位置：`ArcActivityHeatmap.cs:339-450`、`:530-549`
- 证据：
  ```csharp
  for (var week = 0; week < _weeks; week++) for (var row = 0; row < 7; row++) { … }   // :395-397
  if (index != _hover) { _hover = index; InvalidateVisual(); }                        // :535-538
  var formatted = new FormattedText(text, …, 10.5, ArcResources.Brush(ArcTokens.Foreground));  // :480
  ```
- 放大：默认 52 周×7=364 格，每次跨格重画全部格子 + tooltip 文本 + `new Pen`/资源查找（:425-429、:480、:494）。
  横扫一行 52 格 ≈ 1.9 万次矩形绘制。
- 修复：静态格子合成 `DrawingGroup`/合并几何缓存，hover 只画高亮与边框层；tooltip 文本按 (day,count) 缓存。

### EF-01 TextShimmer：常驻 16 ms timer，每帧新建渐变画刷 + 5 个 stop + LINQ（P0）

- 位置：`ArcTextShimmer.cs:174`、`:185-227`
- 证据：
  ```csharp
  _sweep = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => ApplyBrush());  // :174
  var baseColor = ArcResources.Color(ArcTokens.TextMuted);   // :202
  var bandColor = ArcResources.Color(ArcTokens.Foreground);  // :203
  var stops = new List<(double Offset, Color Colour)> { … };  // :206-213
  foreach (var stop in stops.OrderBy(stop => stop.Offset)) …  // :221
  _current.Foreground = brush;                               // :226
  ```
- 放大：`Active` 且非 reduced-motion 就 60 fps 不停（:164-177）。每帧 2 次资源查找（R2）+ List/5 tuple/5 stop/
  1 个 `LinearGradientBrush`（R1）+ 2 次 `ArcColor.Mix` + 1 次 5 项 LINQ 排序（顺序本来固定）+ 一次 Foreground 写入。
  × 实例数（状态行 1–3）× 60/s。
- 修复：不用手写 timer——把 phase 映射到画刷 `StartPoint`/`EndPoint`（`StyledProperty`，可用 `Animation` 关键帧驱动）
  或复用同一 `LinearGradientBrush` 只改 stop offset；删 `OrderBy`；颜色在 `Active`/主题变化时取一次；30 fps 足够。

### EF-02 InViewTitle 的 viewport watcher 从未保存 → 订阅泄漏且可叠加（P1）

- 位置：`ArcInViewTitle.cs:270-291`；`ArcEffectVisuals.cs:288-312`
- 证据：
  ```csharp
  ArcViewport.Watch(this, Play, once: Once, threshold: 0.45, onHidden: …);  // ArcInViewTitle.cs:285 ← 返回值丢弃
  _watch?.Dispose(); _watch = null;                                        // :272-273 永远是 null
  _scroll.ScrollChanged += OnScrollChanged;                                // ArcEffectVisuals.cs:298
  control.LayoutUpdated += OnLayoutUpdated;                                // :301
  ```
- 放大：`_watch`（:43）从未赋值 → `Arm()` 每次调用都新增一个 watcher（`Build()` 在 Text/Level/Stops 等属性变化时
  都会 `Arm()`，:147-150）；主题切换/页面重建后 `ScrollViewer` 的 `ScrollChanged` 仍持有已脱离树的控件，
  `Evaluate`（:314）每次滚动/布局跑 N 份。对比 `ArcTextReveal.cs:206` 写的是 `_watch = ArcViewport.Watch(...)`
  ——**同一助手两个调用点一对一错**。
- 修复：`_watch = ArcViewport.Watch(...)`（一行）；`LayoutUpdated` 兜底建议首帧后降级为 `ScrollChanged`+`SizeChanged`。

### EF-03 BottomSheet / ExpandableCard 逐帧改尺寸=每帧布局（P1）

- 位置：`ArcBottomSheet.cs:341-368`；`ArcExpandableCard.cs:223-250`、`:269-290`
- 证据：
  ```csharp
  var height = from + (target - from) * progress;
  _sheet.Height = Math.Max(0, height);        // ArcBottomSheet.cs:361 ← 每帧改布局属性
  UpdateScrim(height);
  ```
- 放大：0.4 s/16 ms≈25 帧，每帧抽屉内整棵内容 Measure+Arrange；`ExpandableCard` 同时逐帧改 `_frame.Width`
  并在 `Apply()` 手 `Measure`（:273-280）。与 `CONVENTIONS.md` §4“animate transform/opacity rather than layout”冲突。
- 修复：抽屉用 `RenderTransform` 平移（高度一次设好，`ClipToBounds` 遮罩）；展开卡动画 `ScaleTransform`/`Clip`。

### EF-04 TextReveal 每帧逐单元写属性（P1）

- 位置：`ArcTextReveal.cs:270-280`、`:165-178`
- 证据：
  ```csharp
  var eased = ArcMotion.EaseEnter.Ease(local);
  _units[i].Opacity = eased;  _moves[i].Y = (1 - eased) * rise;   // :273-279
  ```
- 放大：每 word/char 一个 `TextBlock`+一个 `TranslateTransform`，每帧 2×单元数次写入。`Unit=Character`
  时（`ArcEffectVisuals.cs:429-431` 每个字符拆成一个字符串）一句 60 字符 → 60 个 TextBlock，一次入场≈3600 次写入 + 60 个独立文本层重绘。
- 修复：字符模式用单个 `TextLayout` 自绘按段裁剪 opacity，或限制字符模式 ≤20 字符；入场改为行级容器整体
  `Opacity`+平移（stagger 用 3–5 组近似）。

### EF-05 AnnouncementBar 50 ms 改 `Width` 驱动进度条（P1）

- 位置：`ArcAnnouncementBar.cs:711-739`
- 证据：
  ```csharp
  _progress = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, _) => UpdateProgress());  // :719
  _progressFill.Width = width * Math.Clamp(elapsed, 0, 1);   // :738
  ```
- 放大：20 次/秒布局失效，且与旋转 timer（:711）+倒计时 timer（:639）并存。相对好的是三者关闭/脱离时都 Stop
  （:239-240、:366-380），生命周期正确。
- 修复：进度用 `ArcProgress` 或自绘 1 行 `Render` 按 fraction 画，彻底避开布局；timer 降到 10 Hz 也够。

### 其余 P2 速览

- **PF-09** 表情格每格 Border>Panel>(Border+TextBlock) 4 控件 + `ToolTip.Tip` + 两绑定，且每个格子都吃到样式里
  那条 `BrushTransition`（`ArcEmojiPicker.axaml:402-422`、`ArcEmojiPickerStyles.axaml:5-18`），可视≈100 格→400 控件
  +100 个 ToolTip 服务。建议 ToolTip 提到行级、hover 用自绘。
- **PF-10** `SetActiveCategory` 每次 `Cast().ToArray()`（`ArcEmojiPicker.cs:1078`），改为缓存 options 数组。
- **PF-11** settle 每帧 `new ScaleTransform`（`ArcAvatarUpload.cs:978`），应建一次再改 `ScaleX/ScaleY`。
- **FB-10** 每 token 一个 `Run` + `GetResourceObservable` 绑定（`ArcUi.cs:241-255`、`ArcCodeBlock.cs:257`），
  40 行≈300–400 个 dynamic-resource 订阅，主题切换（B4）逐个重解析；建议按 token 类别缓存 `IBrush` 直接赋 `Run.Foreground`。
- **FB-11** Countdown 250 ms 无条件 `Render()` + `Children.Clear()`（:153、:202-203）；秒级差异用 1 s 或对齐秒边界更好。
- **FB-12** `ArcUsageMeter.Render` 每段一次资源查找+`PushOpacity`（:398-421），段数少（≤5），可预建画刷数组。
- **FB-13** `RefreshActiveCell` 遍历全部 `_cellFrames`（:1674-1677），记住上一个 active frame 即可。
- **FB-14** `RebuildIndex` 每次 `Flatten()` 递归全树（`ArcTreeTable.cs:611-635`，含折叠子树），可改惰性索引。
- **EF-06** `Rotate` 每次状态切换 `TransformOperations.Parse` 字符串（`ArcEffectVisuals.cs:97`），可缓存两个常量。
- **EF-07** `ArcEffectDismiss` 在 TopLevel 挂 Tunnel 的 PointerPressed/KeyDown 且关闭时 Dispose（:246-254），配对正确；
  多浮层同开时每层一份。影响小。

## 4. 该区域设计评价与修复优先级

### 做得好的

1. **日历/签名/色域的“自绘 + 模板外壳”分层**：日格不是 42 个 Button，换月份只改属性（`ArcCalendar.cs:358-368`），
   视觉树零增长——本区域最正确的架构决定。
2. **`ArcPickerBase` 把弹层语义收敛到一处**，4 个 picker 复用，`OnApplyTemplate` 先退订再订阅。
3. **Effects 的动效纪律**：`ArcTween`（每实例、自停、reduced-motion 短路）+25 处 `ReducedMotion` 分支 + 零硬编码颜色，
   是本仓最规范的一组。
4. **Feedback 的 `ArcUi` 复用层**（150 处调用）让 23 个控件排版一致；`ArcMetricCard : ArcCard` 说明“派生复用”也可用。
5. **表格的增量路径已存在**：`RefreshActiveCell` 用 `_cellFrames` 只改游标边框、吸顶表头在 ScrollViewer 之外
   （`ArcDataGrid.axaml:31`）——作者知道正确做法，只是没在选中/排序上贯彻。
6. **生命周期基本干净**：18 处 timer 创建点除 EF-02 的 watcher 外全部在脱树/关闭时 `Stop()`
   （`ArcAnnouncementBar.cs:236-243`、`ArcCountdown.cs:127-134`、`ArcProgress.cs:179-183`）；未发现 static 事件
   或无解绑的 `PropertyChanged +=`。
7. **可访问性覆盖高**：179 处 `AutomationProperties`，日历键盘模型+帮助文本、表格排序/勾选提示、表情格命名都写了。

### 最该先修的 3 件事（按 ROI）

1. **签名板缓存已完成笔画的几何**（PF-01）——一处改动，从 O(总点数)/次 move 降到 O(1)，手感立竿见影。
2. **日历 Render 预生成资源/文本 + 去掉每格 `PushOpacity`**（PF-02，连带 PF-04）——所有日期控件共享收益。
3. **表格/树表/JSON 树改虚拟化 + 选择增量更新**（FB-01/FB-02/FB-05）——工作量最大，但决定数据视图能否上真实数据量；
   `_display`/`_cellFrames` 数据结构已就绪。

其次：TextShimmer 改画刷关键帧（EF-01）、`_watch = ArcViewport.Watch(...)` 一行修泄漏（EF-02）、
`ArcTween` 改全局 ticker（FB-07）。

### 设计层面的结构性问题

1. **“一个控件一个 16 ms timer”的动效模型**：Effects 一份 `ArcTween`、Feedback 一份 `ArcTween`、Pickers 手写三处
   （`ArcAvatarRing.cs:101`、`ArcEmojiPicker.cs:1143`、`ArcAvatarUpload.cs:967`）。应收敛为 foundation 层
   `ArcAnimationClock`（单 ticker + Job 列表），顺带消掉 `ArcSpinner`（B2）与 `ArcAvatarRing` 的重复。
2. **“重建整棵树”是默认交互实现**：DataGrid/TreeTable/SortableDataTable/JsonViewer/CommentThread/TreeView 的
   选中、展开、排序全部 `Children.Clear()`+重建。缺少“行控件池/增量更新”中间层，每个新控件重复同一模式。
3. **Pickers 缺视觉复用层**：Feedback 有 `ArcUi`、Effects 有 `ArcEffectVisuals`，Pickers 没有等价物，
   于是 `ArcResources.*` 散落 62 处、TextBlock 组装重复 3–4 份。补一个 `ArcPickerUi` 是低风险高收益重构。
4. **自绘面没有分开“静态层 vs 交互层”**：`ArcCalendarSurface`/`ArcColorArea`/`ArcColorRamp`/`ArcAvatarCropSurface`/
   `ArcHeatmapGrid` 的 `Render` 都是“重画一切”；正确形态是静态层缓存为 `DrawingGroup`/`Geometry`，
   交互层（光标、hover、tooltip）单独画。这是 PF-02/PF-06/PF-07/FB-09 的共同根因。
5. **reduced-motion 语义在执行层被误读**：`ArcAvatarRing.cs:104` 用“慢放”代替“停止”。应在 `ArcMotion`/
   `ArcTween` 层提供唯一判定入口，禁止控件自行解释。
