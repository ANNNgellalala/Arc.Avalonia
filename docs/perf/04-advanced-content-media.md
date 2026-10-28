# AdvancedInputs + AdvancedActions + Content + Media：组件设计与性能分析

## 0. 范围与覆盖

- 覆盖工程：`src/Arc.Avalonia.AdvancedInputs`、`src/Arc.Avalonia.AdvancedActions`、`src/Arc.Avalonia.Content`、`src/Arc.Avalonia.Media`。
- 文件数（不含 `obj/`）：31 + 14 + 13 + 23 = 81 个源文件；其中控件/基础设施 `.cs` 36 个、页面 `.cs` 13 个、主题 `.axaml` 31 个。
- 精读（逐段 read，含行号核对）：`ArcRichTextEditor.cs`、`ArcMarkdown.cs`、`ArcReorderableList.cs`、`ArcOnboardingChecklist.cs`（BuildShell + Ring 段）、`ArcContentUi.cs`、`ArcChatThread.cs`（BuildShell→RenderMessages→BuildGroup/Bubble/Avatar→Submit 全段）、`ArcImageCompare.cs`、`ArcCarousel.cs`、`ArcCardStack.cs`（Build/Rebuild/Decide/指针段）、`ArcHoldToConfirm.cs`、`ArcResizablePanels.cs`、`ArcScrollArea.cs`、`ArcReactions.cs`、`ArcSwipeActions.cs`、`ArcMediaUi.cs`、`ArcElasticSlider.cs`、`ArcRangeHistogramSlider.cs`、`ArcScrubInput.cs`（模板与值路径段）、`ArcPhoneInput.cs`（输入与选择器段）、`ArcMentionInput.cs`（文本/建议段）、`ArcMorphSelect.cs`（形变/高亮段）、`ArcExpandingSearch.cs`（展开/结果段）、`ArcAdvancedInternals.cs`、`ArcDraw.cs`、`ArcActionButton.cs`、`ArcAdvancedVisuals.cs`（Glide 段）、`ArcExpandingButtonGroup.cs`（展开段）。
- grep 扫描（未逐行精读，只用于归类/计数）：`ArcInlineEdit.cs`、`ArcShortcutRecorder.cs`、`ArcBillingToggle.cs`、`ArcBillingPrice.cs`、`ArcRadioCards.cs`、`ArcFloatingButtonGroup.cs`、`ArcIconMorph.cs`、`ArcUserMenu.cs`、`ArcMediaModels.cs`、全部 `Pages/*.cs`、全部主题 `.axaml`（抽查 token 用法）。
- 方法：先 `grep` 定位 `DispatcherTimer` / `InvalidateVisual` / `override void Render` / `PointerMoved` / `new FormattedText` / `new SolidColorBrush` / `Transitions = new` / `ArcResources.`，再带行号精读调用链。**没有运行构建、测试或截图**，所有结论是静态代码路径推理；凡未经运行时确认的行为都标了「suspected」。
- 计数与 BRIEF 基线对齐：AdvancedInputs 26 处 `InvalidateVisual`、3 个 `Render` 重写、45 处 `ArcResources`（本文按文件统计恰好 45）；Media 11 处 `new SolidColorBrush`（其中 1 处在 `ArcMediaUi.Alpha` 帮助方法、其余 10 处都在 `Render` 里）；AdvancedActions 6 个 `DispatcherTimer` 字段、0 处 `InvalidateVisual`；Content 5 个计时器/动画点。

## 1. 组件设计特征

### 1.1 基类选择（合理，一处系统性偏差）

- 绝大多数交互控件是 `TemplatedControl`：`ArcScrubInput`、`ArcPhoneInput`、`ArcMentionInput`、`ArcShortcutRecorder`、`ArcExpandingSearch`、`ArcMorphSelect`、`ArcBillingToggle`、`ArcRadioCards`、`ArcInlineEdit`、`ArcRichTextEditor`、`ArcChatThread`、`ArcReorderableList`、`ArcCarousel`、`ArcCardStack`、`ArcReactions`、`ArcResizablePanels`、AdvancedActions 全部。
- 按钮语义复用 foundation：`ArcHoldToConfirm : Button`、`ArcActionButton : Button`、`ArcReactions.ArcReactionPill : Button`、`ArcSwipeActionButton : Button`——保留了 Click、禁用态和指针/键盘语义，比裸 `Control` 正确。
- 自绘点共 7 个，都是"没有文本子元素、纯几何"的场景，选择合理：`ArcElasticSlider`、`ArcRangeHistogramSlider`、`ArcBillingPrice`、`ArcChecklistRing`（Content）、`ArcCompareSurface`、`ArcScrollOverlay`（Media）、`ArcReactionBurst`。
- 偏差：`ArcRichTextEditor` 与 `ArcChatThread` 的 `.axaml` 模板只有 ~680 B（仅一个 `PART_Host`），整棵 UI 在 `BuildShell()` 里用代码建（`ArcRichTextEditor.cs:285-367`、`ArcChatThread.cs:252-301`）。这偏离 CONVENTIONS"模板写在 `ControlTheme`"，直接后果是"界面 = 代码重建"，即 CT-01/CT-03/CT-10 那类全量重建问题。同族代码复用了 `ArcContentUi.Paint/Bind`，所以换肤仍能生效，不算硬伤，但把内容层放回模板/`ItemsControl` 会顺带解决一半问题。

### 1.2 状态、模板与组合

- 变体统一走"enum → CSS 风格 class"：`ArcActionButton.cs:245-256`、`ArcHoldToConfirm.cs:171-177`、`ArcScrubInput.cs:403-408`、`ArcCarousel.cs:557-569`、`ArcExpandingButtonGroup.cs:455-459`；与 CONVENTIONS §3 一致。
- 属性全部是 `StyledProperty` + 静态构造里的 `AddClassHandler`，没有可写普通字段泄漏到公共 API（如 `ArcReorderableList.cs:74-79`、`ArcCarousel.cs:189-204`）。
- `PART_*` 命名规范、`OnApplyTemplate` 取件后立即 `Update*()`：`ArcRichTextEditor.cs:239-245`、`ArcChatThread.cs:237-243`、`ArcReorderableList.cs:103-119`、`ArcCarousel.cs:279-316`、`ArcScrollArea.cs:292-335`、`ArcImageCompare.cs:472-481`、`ArcResizablePanels.cs:310-317`。
- 组合而非派生是主流：`ArcRichTextEditor` 用 `TextBox` + `ContentControl` 预览 + `Canvas` 浮层；`ArcPhoneInput` 用 `TextBox` + 自建列表浮层；`ArcResizablePanels` 用自定义 `ArcPanePanel : Panel` 计算份额。把**主要内容区**建在代码里（而非模板）的是 RichTextEditor/ChatThread/ReorderableList/Reactions/PhoneInput 选择器/Onboarding 六处，见 §4 的结构性问题 1。
- 反模式（结构性）：跨四个工程各有一份几乎相同的 UI 帮助类与补间器——`ArcContentUi` + `ArcContentTween`、`ArcMediaUi` + `ArcMediaTween`、`ArcAnim` + `ArcText`、`ArcAdvancedVisuals`。四份 `Paint`/`Brush`/`Row`/`Column`/`Initials` 逻辑重复，三份 16 ms 补间器重复（详见 AI-03/MD-07），没有沉淀到 foundation。

### 1.3 动效路线与 reduced-motion

三条路线并存，选择大体有章法：

1. **Avalonia `Transitions`（首选）**：AdvancedActions（`ArcActionButton.cs:306-329`、`ArcIconMorph.cs:190`、`ArcUserMenu.cs:697`）、`ArcReorderableList.cs:188-204`、`ArcChatThread.cs:685-704`、`ArcScrollArea.cs:318-329`。
2. **每控件 16 ms `DispatcherTimer` 手写补间**：`ArcAnim.To`（`ArcAdvancedInternals.cs:22-57`）、`ArcContentTween.Run`（`ArcContentUi.cs:304-335`）、`ArcMediaTween.Run`（`ArcMediaUi.cs:193-227`）。三份实现等价，都做了 reduced-motion 短路。
3. **自绘 + 每帧 `InvalidateVisual`**：ElasticSlider / RangeHistogramSlider / Ring / ReactionBurst。

- reduced-motion 覆盖率高：`ArcAnim.To:30`、`ArcContentTween:308`、`ArcMediaTween:197`、`ArcReorderableList:188`、`ArcCarousel:467`、`ArcHoldToConfirm:257`、`ArcChatThread:685`、`ArcScrollArea:320`、`ArcAdvancedVisuals:215/234/253/282`、`ArcRadioCards`/`ArcMorphSelect`/`ArcBillingToggle` 的各个 `ArcAnim.To` 调用点都先判 `ReducedMotion`。**这是本区域做得最好的地方之一。**
- 缺口：`ArcActionButton.SetUpMotion`（`ArcActionButton.cs:314`）只在 `OnApplyTemplate` 判断一次，运行期切换 `ReducedMotion` 不会撤掉已装的 `Transitions`；Media 的 `ArcCompareSurface`（`ArcImageCompare.cs:63`）与 `ArcScrollOverlay`（`ArcScrollArea.cs:59`）只订阅 `ActualThemeVariantChanged`。
- 动作原则有执行，但不彻底：CONVENTIONS 要求"animate transform/opacity rather than layout"，而 MorphSelect/ExpandingSearch/BillingToggle/RadioCards/ExpandButtonGroup/Glide 都在逐帧改 `Width/Height/Margin`（AI-07、AA-01、AA-02、MD-06）。

### 1.4 可访问性

- Automation 覆盖到位：每个控件都有 `AutomationProperties.SetName/HelpText`，实时播报用 `AutomationLiveSetting.Polite`（`ArcChatThread.cs:290`、`ArcReorderableList.cs:114`、`ArcOnboardingChecklist.cs:359`、`ArcActionButton.cs:303`）；`ArcReorderableList` 甚至提供 lift/drop 播报（`:539`、`:553`）。
- 键盘替代路径基本齐全：Carousel ←/→/Home/End（`ArcCarousel.cs:661-683`）、CardStack ←/→ + Ctrl+Z/Backspace（`ArcCardStack.cs:720-744`）、ImageCompare ←/→/PgUp/PgDn/Home/End 且 Shift 10%（`ArcImageCompare.cs:668-705`）、HoldToConfirm Space/Enter/Esc（`:430-463`）、ResizablePanels 分隔条是真实焦点站 + 方向键/Enter 折叠/Home/End（`:813`）、ReorderableList Space 抬起 + 方向键 + Esc（`:500-580`）、ScrubInput 方向键/Shift/Alt 步进 + 表达式输入（`ArcScrubInput.cs:703-759`）、RangeHistogramSlider Tab 切拇指（`:471-487`）、PhoneInput 搜索框方向键 type-ahead（`:1126-1167`）。
- 主要缺口：自绘 slider（ElasticSlider/RangeHistogramSlider）只写了 `AutomationProperties` 名称/帮助文本，没有 `RangeValueProvider`/`AutomationPeer`，读屏器无法用"范围值"模式读出当前值；MentionInput/PhoneInput 建议列表是 `Border` 行而不是 `ListBoxItem`，没有 `Selected` 语义；焦点环只有 ElasticSlider 自绘（`:244-248`）与 ResizablePanels 的 `ring`（`:480-487`），其余自绘控件靠外层模板表达。

### 1.5 token 一致性（做得好）

- 主题文件全部 `DynamicResource`；代码建的树通过 `Paint()`（`host.Bind(property, host.GetResourceObservable(token))`，`ArcContentUi.cs:41-49`、`ArcMediaUi.cs:45-53`）绑定 token observable，因此 accent 切换能自动跟随。
- 四个工程控件代码里**没有任何 hex / `Color.FromRgb` / 语义色 `Brushes.X`**；grep 只命中 `Brushes.Transparent`（结构性透明，允许），透明渐变的白色 alpha 只出现在 ScrollArea 的 OpacityMask（`ArcScrollArea.cs:451-452`，也是结构性遮罩）。
- 唯一系统性例外：自绘 `Render` 里用 `ArcResources.Brush/Color` 取快照（AdvancedInputs 45 处里 20 处在 `Render`；Media 11 个 `new SolidColorBrush` 全在 `Render`），见 AI-08/MD-09。

## 2. 性能问题清单

| ID | 分类 | 严重度 | 位置 | 问题一句话 | 放大因子 |
| --- | --- | --- | --- | --- | --- |
| CT-01 | R2/S2/P1 | P0 | `src/Arc.Avalonia.Content/Controls/ArcRichTextEditor.cs:762-780,819-827` | 每次按键全文 `ToHtml` + 重建整棵预览控件树，Markdown 被解析两次 | ×文档长度 ×按键率 ×预览块数 |
| CT-02 | S2/R1 | P1 | `src/Arc.Avalonia.Content/Controls/ArcMarkdown.cs:123,132,139,147,596` | 逐行未编译正则匹配；`InlineToHtml` 每个普通字符一次单字符字符串 + HtmlEncode | ×文档字符数 ×每次按键 2 次解析 |
| CT-03 | L2/M2/R1 | P0 | `src/Arc.Avalonia.Content/Controls/ArcChatThread.cs:135-139,453-536,846-873` | 每条消息/typing 变化全量清空重建消息树；头像位图每次重新解码 | ×消息数 ×头像数 ×重建次数（无虚拟化） |
| CT-04 | P1/L2 | P1 | `src/Arc.Avalonia.Content/Controls/ArcChatThread.cs:522-535,272-278` | 重建后 `ScrollToEnd`，整棵树被迫重新测量/排列 | ×消息数 ×每次新消息 |
| CT-05 | L1 | P1 | `src/Arc.Avalonia.Content/Controls/ArcRichTextEditor.cs:771,819-827` | 预览隐藏时仍每键重建预览（`force: !_previewVisible` 取反） | ×按键率（纯浪费） |
| CT-06 | L2/P1 | P1 | `src/Arc.Avalonia.Content/Controls/ArcRichTextEditor.cs:907-931,574-646,970-980` | 每次选区/光标变化重建 7 个工具栏按钮，并用 LINQ 遍历视觉树找 `TextPresenter` | ×选区变化次数（拖选≈每 pointer move） |
| CT-07 | L2/R1 | P1 | `src/Arc.Avalonia.Content/Controls/ArcRichTextEditor.cs:1321-1361,688-760` | slash 菜单打开时每个按键重建全部行控件 | ×按键率 ×可见命令数 |
| CT-08 | R1/P1 | P1 | `src/Arc.Avalonia.Content/Controls/ArcReorderableList.cs:603-604,351-387` | FLIP 拖动对每行每 pointer move 做字符串格式化 + `TransformOperations.Parse` | ×行数 ×pointer move 频率 |
| CT-09 | L2/T2 | P2 | `src/Arc.Avalonia.Content/Controls/ArcReorderableList.cs:471-498,137-237` | 每次键盘移序重建整个列表 + 每行 `new Transitions` | ×行数 ×按键次数 |
| CT-10 | L2/T1 | P1 | `src/Arc.Avalonia.Content/Controls/ArcOnboardingChecklist.cs:225-374,624,640,667,680` | 任一勾选/展开都全量 `BuildShell`（含新建进度环与所有行） | ×步骤数 ×交互次数 |
| CT-11 | R1/R2/R3 | P1 | `src/Arc.Avalonia.Content/Controls/ArcOnboardingChecklist.cs:779-820`；`ArcContentUi.cs:317` | 进度环每帧 `new Pen`×2、`new StreamGeometry`、`ArcResources`×2 | ×60 fps ×0.4 s 动画 |
| CT-12 | R1 | P2 | `src/Arc.Avalonia.Content/Controls/ArcRichTextEditor.cs:846-860` | 每次按键 `Split` 全文数词 | ×按键率 ×文档长度 |
| AI-01 | P1/R1 | P0 | `src/Arc.Avalonia.AdvancedInputs/Controls/ArcRangeHistogramSlider.cs:417-426,538-572,611-655,224,266-297` | 拖动每 move 起一个 16 ms 计时器；每帧对全数据集做 Min/Max 并重新分桶重绘 | ×`Data.Count` ×拖动频率 ×存活 timer 数 |
| AI-02 | R1/R2/S2 | P1 | `src/Arc.Avalonia.AdvancedInputs/Controls/ArcElasticSlider.cs:334-350,179-249` | 每次 pointer move 全表面重绘：7 次资源查找 + 2-4 个 `FormattedText` + `new Pen` | ×60 Hz ×实例数 |
| AI-03 | T1/T2 | P1 | `src/Arc.Avalonia.AdvancedInputs/Controls/ArcAdvancedInternals.cs:22-57` | 每次动画新建不可取消的 16 ms `DispatcherTimer` | ×键盘重复率（30/s）×调用点数 |
| AI-04 | L1/P1 | P1 | `src/Arc.Avalonia.AdvancedInputs/Controls/ArcScrubInput.cs:523-546,372-383,454-486` | 拖动每 move 改 `TextBox.Text` + 轨道 `Width` + Automation 文案 | ×pointer move 频率 ×实例数 |
| AI-05 | L2 | P1 | `src/Arc.Avalonia.AdvancedInputs/Controls/ArcPhoneInput.cs:1119-1124,941-990,1022-1090` | 国家选择器每次搜索按键重建全部国家行（每行 3 次 token 查找） | ×国家数（≈200）×按键率 |
| AI-06 | L2/S2 | P1 | `src/Arc.Avalonia.AdvancedInputs/Controls/ArcMentionInput.cs:462-483,500-543,599-635,662-692` | 每按键重建镜像 `Run` 集合与全部建议行，含 LINQ `OrderBy` | ×按键率 ×(提及数 + 候选人数) |
| AI-07 | L1/T2 | P1 | `ArcMorphSelect.cs:568-605,912-918`；`ArcExpandingSearch.cs:540-564`；`ArcBillingToggle.cs:391-415`；`ArcRadioCards.cs:478-503` | 用 `Width/Height/Margin` 逐帧做形变/高亮动画 | ×60 Hz ×每帧布局子树 |
| AI-08 | R2 | P2 | `ArcElasticSlider.cs:186-192`；`ArcRangeHistogramSlider.cs:225-232`；`ArcBillingPrice.cs:100-102` | 自绘控件无主题订阅，`Render` 里的 token 快照换肤后不刷新 | 换肤次数 |
| AI-09 | R1 | P2 | `src/Arc.Avalonia.AdvancedInputs/Controls/ArcDraw.cs:19,57` | 每次绘制 `new Pen`（几何缓存做得好，Pen 没有） | ×每帧图标/描边数 |
| MD-01 | L1/P1 | P0 | `src/Arc.Avalonia.Media/Controls/ArcResizablePanels.cs:529,591,235,63-106,110-162,165-232` | 拖动分隔条每 move 全 pane Measure+Arrange（文本重排）并分配尺寸数组 | ×pane 数 ×pane 内容 ×60 Hz |
| MD-02 | L1/T2 | P1 | `src/Arc.Avalonia.Media/Controls/ArcCarousel.cs:46-58,106-134,606-629` | 拖拽用 `InvalidateArrange` 驱动，每 move 重排全部 slide 并写 Opacity/ZIndex | ×slide 数 ×pointer move 频率 |
| MD-03 | R1/R2/S2 | P1 | `src/Arc.Avalonia.Media/Controls/ArcImageCompare.cs:618-628,152-181,200-238,281-325` | 拖动每 move 全表面重绘：逐线 hatch、2 个 `FormattedText`、6 个 `new SolidColorBrush` | ×pointer move ×控件尺寸 |
| MD-04 | L2/T1/R1 | P1 | `src/Arc.Avalonia.Media/Controls/ArcReactions.cs:437,260-312,386-404,41,72-103` | 每次点击重建所有 pill；每个计数各起 16 ms tween；burst 每帧 7 个新 brush | ×反应类型数 ×存活 tween 数 |
| MD-05 | R1/T1 | P1 | `src/Arc.Avalonia.Media/Controls/ArcScrollArea.cs:301-305,401-446,538-557` | 每个滚动事件新建渐变刷/GradientStops 赋给 OpacityMask，并新建 hide 计时器 | ×scroll 事件频率 |
| MD-06 | L1/T1 | P1 | `src/Arc.Avalonia.Media/Controls/ArcHoldToConfirm.cs:338-357,267-268` | 按住进度每帧改 fill/clip 的 `Width` → 每帧布局 | ×60 Hz ×按住时长 |
| MD-07 | T2 | P2 | `src/Arc.Avalonia.Media/Controls/ArcMediaUi.cs:193-233` | 每次 `Run` new 计时器（有 `Stop`，但无跨 run 取消/合并语义） | 手势频率 |
| MD-08 | L2/M2 | P2 | `src/Arc.Avalonia.Media/Controls/ArcCardStack.cs:271-335,294-298,526-539` | 每次判定重建 ≤4 张卡（含新 `TransformGroup`）；决策列表无界增长 | ×决策次数 |
| MD-09 | R2 | P2 | `ArcScrollArea.cs:59`；`ArcImageCompare.cs:63` | 只订阅 `ActualThemeVariantChanged`，纯 accent 换色不触发重绘 | 换肤次数 |
| AA-01 | L1/T2 | P1 | `src/Arc.Avalonia.AdvancedActions/Controls/ArcAdvancedVisuals.cs:202-249,280-300` | 共享高亮靠 `Margin/Width/Height` 过渡迁移，每次 `ShowAt` 重建 `Transitions` | ×菜单项数 ×hover 频率 |
| AA-02 | L1/T2 | P1 | `src/Arc.Avalonia.AdvancedActions/Controls/ArcExpandingButtonGroup.cs:403-453` | 展开用 `Width` 过渡，每次 `ApplySlot` 为每个槽位分配 `Transitions` | ×槽位数 ×展开次数 |
| AA-03 | T1/R1 | P2 | `ArcActionButton.cs:61,81`；`ArcExpandingButtonGroup.cs:88,109`；`ArcUserMenu.cs:107,156` | 每实例一个计时器 + 每次过渡 `TransformOperations.Parse` 字符串 | 实例数 |

## 3. 逐条说明

先把本轮指定问题的结论放在前面（细节在对应条目）：

- **RichTextEditor**：每次按键**全文重解析两遍**（`ToHtml` 一次、`BuildPreview` 又一次），预览是**多项控件**（`TextBlock`/`Border`/`Grid` 组成的 `StackPanel`，见 `ArcMarkdown.cs:295-402`），不是单个 `TextBlock`，整体替换 `ContentControl.Content`。**没有语法高亮**（源码是纯文本 `TextBox`）。工具栏在**选区变化**时整组重建（不是每次按键，但拖选时等价于每个 pointer move），浮层 toolbar 还会同步 `Measure`；slash 菜单在打开时**每按键重建全部行**（CT-01/CT-05/CT-06/CT-07）。
- **ChatThread**：**无虚拟化**（`ScrollViewer` + `StackPanel`），每条消息/每次 typing 变化全量重建整棵树（O(N)），头像每次重建都重新解码位图；自动滚动在重建后 `Post(ScrollToEnd)`，必然触发整棵树重新测量/排列（CT-03/CT-04）。
- **ReorderableList 的 FLIP**：重排时**没有**对子项做 `Measure/Arrange/TranslatePoint` 强制同步（行高是常量 64），因此**没有 layout thrash**；代价在每行每 pointer move 的字符串 `TransformOperations.Parse`（CT-08）。
- **ScrubInput / MentionInput / ElasticSlider / RangeHistogramSlider 的 pointer 热路径**：ScrubInput 每 move 改 `TextBox.Text` 与轨道 `Width`（布局）+ 播放 Automation 文案；MentionInput 每按键重建镜像 `Run` 集合与全部建议行（含 LINQ）；ElasticSlider 每 move 全表面重绘（7 次 token 查找 + `FormattedText` + `Pen`）；RangeHistogramSlider 最重——每 move 起一个不可取消的 16 ms timer，并且**每帧都对全数据集重算 min/max 并重新分桶所有柱子**，还在拖动中每帧触发 `ValueCommitted`（AI-01/02/04/06）。
- **Media**：Carousel 拖拽用 `InvalidateArrange` 驱动（每 move 全 slide 重排），CardStack 拖拽是 render-only（只有回弹补间，**没有惯性 timer**）；ImageCompare 每 move 重绘整面（两次 `PushClip` + hatch + `FormattedText`）；ResizablePanels 拖动每 move 触发全 pane measure/arrange（文本重排），是 Media 唯一 P0；HoldToConfirm 按住进度每帧改 `Width`（每帧布局）；11 个 `new SolidColorBrush` 里 **10 个在 `Render`/其调用的绘制方法内**（ImageCompare 6、Reactions 2、ScrollArea 2），第 11 个是 `ArcMediaUi.Alpha` 帮助方法本身（MD-01/02/03/04/05/06）。Media 的“9 个 timer”= 4 个 `DispatcherTimer` 创建点 + 5 个 `ArcMediaTween` 字段（每次 `Run` 再建一个 16 ms timer，每个 reaction pill 还有一个）。

### CT-01 编辑按键触发的双解析 + 预览全量重建（P0）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcRichTextEditor.cs:762`
- 证据：
  ```csharp
  _source = _textBox.Text ?? string.Empty;
  SyncOutputs(true);                       // → ArcMarkdown.ToHtml(_source) → Parse
  UpdatePreview(force: !_previewVisible);  // → ArcMarkdown.BuildPreview(_source) → Parse
  UpdateCounts(); UpdateBlockHint(); UpdateSlashFromText(); UpdateSelectionToolbar();
  ```
  预览不是单个 `TextBlock`，而是每个块一个控件的 `StackPanel`（`ArcMarkdown.cs:295-402`：标题/段落/引用/列表/代码各建 `TextBlock`/`Border`/`Grid`，行内再 `Run`），`_previewHost.Content` 整体替换（`ArcRichTextEditor.cs:826`）。
- 触发：`TextBox.TextChanged` → 每个可见字符一次。**放大因子**：×文档长度（两次 O(n) 解析）×按键率（8–12/s）×预览块数（每块 1–3 个控件 + `Run`，整体交给 `ScrollViewer` 重新测量/排列）。
- 影响：长文档（几千字、几十个块）打字时每键一次完整解析 + 整棵预览树重建 + 两个 `StyledProperty` 变更；接近掉帧阈值，且 GC 压力随文档线性增长。
- 修复建议：解析一次复用（`Parse` 结果同时喂 `ToHtml`/`BuildPreview`）；预览改增量/延迟（`DispatcherTimer` 60–120 ms 合并）或只在可见时重建；`BuildPreview` 的 `Run` 尽量合并为单个 `TextLayout`。
- 另：本实现**没有语法高亮**（`TextBox` 纯文本），所以问题集中在"全文重新渲染"，不是着色器。

### CT-02 Markdown 解析的每行正则与逐字符分配（P1）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcMarkdown.cs:123`
- 证据：
  ```csharp
  var heading = Regex.Match(trimmed, @"^(#{1,6})\s+(.+)$");   // 123
  if (Regex.IsMatch(trimmed, @"^(?:---+|\*\*\*+|___+)$")) {   // 132
  var bullet = Regex.Match(line, @"^\s*[-*+]\s+(.+)$");        // 139
  ```
  `InlineToHtml` 里 `builder.Append(Escape(current.ToString()))`（`:596`）为**每个普通字符**建一个单字符 `string` 再 `HtmlEncode`。
- 触发：每次按键 ×2（`ToHtml` 与 `BuildPreview` 各一次 `Parse`）。放大因子：×行数（4 个未缓存正则/行）×字符数（单字符字符串分配）。
- 影响：1000 字段落 ≈ 1000 次单字符分配/次 `ToHtml`，加上每行正则解释执行；是 CT-01 里可独立消除的一半成本。
- 修复建议：预编译 `static readonly Regex` 或用 `ReadOnlySpan<char>` 手写前缀判断（`StartsWith('#')` 等）；`InlineToHtml` 改成"连续普通字符一次 `Escape` 一段"。

### CT-03 ChatThread 每条消息全量重建 + 头像每次解码（P0）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcChatThread.cs:453`
- 证据：
  ```csharp
  _messageHost.Children.Clear();          // 467，之后按组重建全部气泡
  var groups = BuildGroups();             // 488
  foreach (var group in groups) { _messageHost.Children.Add(BuildGroup(group)); }
  ```
  消息容器是 `ScrollViewer` 里一个普通 `StackPanel`（`:272-278`），**无虚拟化**；`BuildAvatar` 每次调用都走 `LoadAvatar`（`:846-873`：`new Bitmap(stream)` / `new Bitmap(source)` + `new ImageBrush`，无缓存、无 `Dispose`）。
- 触发：`Submit()`（`:958`）、`Messages`/`DefaultMessages` 变更（`:137-138` → `ReloadMessages`）、`Typing`/`Participants`/`CurrentUserId`/`GroupWindow` 变更（`:135-136,139,162` → `RenderMessages`）。放大因子：×消息数（第 N 条消息重建 N 个组/气泡/头像）×头像数（磁盘/资源解码）×重建次数（远端 typing 每次变化也全量重建）。
- 影响：长会话每发一条消息 O(N) 重建 + 每头像一次位图解码 → 明显卡顿、内存增长；无虚拟化意味着滚动也始终在全量树上做命中/绘制。
- 修复建议：改 `ItemsControl`/`VirtualizingStackPanel`（或至少"只追加新组、只改 typing 行"）；头像 `ImageBrush` 按 source 缓存并用 `static ConcurrentDictionary`，控件销毁时释放 `Bitmap`。

### CT-04 自动滚动在重建后触发全树测量（P1）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcChatThread.cs:522`
- 证据：
  ```csharp
  if (scrollToEnd || pinned)
      Dispatcher.UIThread.Post(() => _scroller?.ScrollToEnd(), DispatcherPriority.Background);
  ```
  重建刚清空又填满整个 `StackPanel`，`ScrollToEnd` 需要完整 `Extent`。
- 触发/放大：每次新消息、每次 typing 变化（重建 → 布局 → 再滚动）；×消息数。影响：滚动定位与全量布局叠加，是 CT-03 的放大器；预览/聊天滚动位置在重建后也会跳。
- 修复建议：先追加再调用 `BringIntoView` 到末尾哨兵元素；或反转容器（bottom-anchored）避免重新定位；配合虚拟化后此问题自然消失。

### CT-05 预览隐藏时仍在重建（P1）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcRichTextEditor.cs:771`
- 证据：
  ```csharp
  UpdatePreview(force: !_previewVisible);   // 隐藏时 force=true
  ...
  if (_previewHost is null || (!_previewVisible && !force)) return;   // 819-824
  ```
  `force` 的语义是"隐藏也强制构建"，与"隐藏就跳过"正好相反。
- 触发/放大：用户点掉 Preview 后每次按键仍付 CT-01 的解析 + 建树成本；×按键率。影响：可见性开关没有省下任何开销。
- 修复建议：改为 `UpdatePreview()`（force 只在显式 `LoadSource`/`UpdatePreviewVisibility` 时用），或表达式 `UpdatePreview(force: false)`。

### CT-06 选区变化重建工具栏 + 视觉树 LINQ（P1）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcRichTextEditor.cs:907`
- 证据：
  ```csharp
  if (_toolbar.Child is null || _toolbarButtons.Count == 0 && !_linkMode) RebuildToolbar();  // 919
  ...
  if (!_linkMode) RebuildToolbar();                        // 927（首次会连续建两遍）
  Dispatcher.UIThread.Post(PositionToolbar, DispatcherPriority.Loaded);   // 930
  ```
  `RebuildToolbar`（`:574-646`）每次 `new` 7 个 `ArcButton`（各自 `ArcIcon` + ToolTip + Automation）；`UpdateSelectionToolbar` 同时被 `TextChanged`（`:775`）与 `SelectionStart/End/CaretIndex` 的 `PropertyChanged`（`:450-458`）调用。
- 触发：拖选文本/Shift+方向键时每个光标位置一次（≈pointer move 频率）。放大因子：×选区变化次数 ×按钮数（模板应用 7 次）。
- 影响：拖选时持续分配与模板实例化，手感发涩；`PositionToolbar` 里还有一次同步 `_toolbar.Measure(Size.Infinity)`（`:948`）和 `_textBox.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault()`（`:970`，每次遍历整棵 TextBox 视觉树 + LINQ 分配）。
- 修复建议：工具栏只建一次，`IsMarkActive` 只更新 7 个按钮的图标 token/class；`TextPresenter` 在 `Loaded` 后缓存字段；位置改 `RenderTransform` 平移并在滚动时复用。

### CT-07 slash 菜单每按键重建行（P1）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcRichTextEditor.cs:1321`
- 证据：
  ```csharp
  if (shouldOpen) { ... RebuildSlashMenu(); _slashMenu!.IsVisible = true; ... }   // 1344
  else if (_slashOpen) { ... else RebuildSlashMenu(); }                            // 1358
  ```
  `RebuildSlashMenu`（`:688-760`）清空 `_slashRows`，为每个命中命令新建 `Border`+`Grid`+`ArcIcon`+2 个 `TextBlock`+可选 `Enter` 提示，并重挂 `PointerPressed`。
- 触发：菜单打开时每次输入/退格；↑/↓ 导航也重建（`:1024-1033`）。放大因子：×按键率 ×可见命令数（`Commands` 8 条，小，但每条 5–6 控件）。
- 影响：轻量但无谓；也导致每帧重新 `AutomationProperties.SetName`。
- 修复建议：菜单结构单次构建，过滤只 toggle `IsVisible` 与高亮 `Classes`；过滤逻辑本来就只有 8 项。

### CT-08 ReorderableList：FLIP 没有 layout thrash，但每行每 move 解析字符串（P1）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcReorderableList.cs:603`
- 证据：
  ```csharp
  private static TransformOperations Translate(double y) =>
      TransformOperations.Parse(string.Create(CultureInfo.InvariantCulture, $"translateY({y}px)"));
  ...
  _rows[i].Frame.RenderTransform = Translate((display - i) * Step);   // 383，每行每 move
  ```
  好消息：行高是常量 `RowHeight=64`（`:41`），拖动期不做任何 `Measure`/`Arrange`/`TranslatePoint`，开始拖动时还把各行的 `Transitions` 置 null（`:343-346`），无 layout 读数 → **结论：FLIP 重排没有同步布局抖动**。
- 触发/放大：`ContinueDrag` 每次 pointer move × 行数（N 次字符串 `string.Create` + N 次 `TransformOperations.Parse`）。影响：小列表可忽略，长列表/高刷新率鼠标（1000 Hz）下明显；属于热路径分配（R1/P1）。
- 修复建议：缓存 `TransformOperations`（每行两个：`translateY(±Step)`、`translateY(0)`）或直接共享 `TranslateTransform` 并只改 `.Y`。

### CT-10 OnboardingChecklist 每次状态变化全量重建（P1）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcOnboardingChecklist.cs:225`
- 证据：
  ```csharp
  _host.Children.Clear(); _titleButtons.Clear();
  ...
  _ring = new ArcChecklistRing();               // 259，每次新建环形控件
  _ring.SetProgress(previousRing, animate: false);
  _ring.SetProgress(ringValue, animate: true);  // 起 0.4 s 动画
  ```
  `BuildShell` 被勾选/展开/隐藏/取消各状态调用（`:624,640,667,680`）。
- 触发/放大：每次勾选一步 × 步骤数（每步 1 个 `ArcButton` + 3 个文本/图标 + 1 个 chevron），并丢弃上一个 ring 的正在跑的补间。影响：步骤数 10+ 时有可感的重建 + 动画中断。
- 修复建议：`BuildShell` 只建一次，勾选只改对应行的 class/`ArcIcon.Icon` 与进度环 target；ring 复用实例。

### CT-11 进度环每帧两次 Pen + 几何重建（P1）
- 位置：`src/Arc.Avalonia.Content/Controls/ArcOnboardingChecklist.cs:779`
- 证据：
  ```csharp
  var track = new Pen(ArcResources.Brush(ArcTokens.Border), thickness, null, PenLineCap.Round, PenLineJoin.Round);  // 790
  var color = ArcResources.Brush(_progress >= 0.999 ? ArcTokens.Success : ArcTokens.Accent);                        // 799
  var pen = new Pen(color, thickness, null, ...);                                                                   // 800
  var geometry = new StreamGeometry();                                                                              // 812
  ```
  驱动是 `ArcContentTween` 的 16 ms 计时器（`ArcContentUi.cs:317`），每 tick `InvalidateVisual`（`:773`）。
- 触发/放大：每次进度变化后 0.4 s 内 ×60 fps；1 个环/看板。影响：单控件开销不大，但是"每帧建几何"的教科书样本，也是全仓 46 处 `new StreamGeometry` 之一。
- 修复建议：track/pen 只在主题变化时重建；进度弧改用 `Arc` 的 `RenderTransform`/`StrokeDashArray` 表达，或缓存两个 `Pen` + 一个可变 `StreamGeometry`。

### AI-01 RangeHistogramSlider：拖动一帧一次计时器 + 全量重新分桶（P0）
- 位置：`src/Arc.Avalonia.AdvancedInputs/Controls/ArcRangeHistogramSlider.cs:417`
- 证据：
  ```csharp
  protected override void OnPointerMoved(PointerEventArgs e) { ... MoveThumb(_dragging, e.GetPosition(this).X, animate: false); }  // 417-425
  // SetRange → SetCurrentValue(ValueProperty) → OnValueChanged → SyncDisplay(animate: true)
  ArcAnim.To(0, 1, ArcAnim.SnappyDuration, ArcAnim.Snappy, value => { ... InvalidateVisual(); });   // 650
  ```
  `Render` 里两处全量数据扫描：开头无条件 `RecomputeDataBounds()`（`:224` → `data.Min()`/`data.Max()`，`:198-209`），然后 `DrawHistogram` 再 `Histogram(Data, ...)`（`:276`）逐值分桶 + `counts.Max()`（`:282`）；另有 8 次 `ArcResources.Brush`（`:225-232`）与 2 个 `FormattedText`（`:242,247`）。
- 触发/放大：每次 pointer move → 新 `DispatcherTimer`（`ArcAdvancedInternals.cs:37`，不可取消）→ 该 timer 每 16 ms 再 `InvalidateVisual` → 每帧 2×O(Data.Count) + O(Data.Count) 分桶 + 最多 60（`Bins` 上限）个柱子的 `DrawRectangle`。放大因子：×`Data.Count` ×拖动事件频率（60–120/s，同时存活 ~15 个 timer）×柱数。
- 影响：这是本分片最确定会掉帧的一条：数据 1 万点、拖动 2 秒 ≈ 数百次全量扫描。另有语义问题：`MoveThumb` 每 move 传 `announce: true`（`:566,570`），于是 `ValueCommitted` 在拖动过程中每帧触发（`:611-628`），消费方会收到数百次"已提交"。
- 修复建议：`Data` 变化时才缓存 `dataMin/dataMax/counts`；拖动期间只重算拇指几何；`OnValueChanged→SyncDisplay` 加 `if (_dragging)` 分支（ElasticSlider 已有此分支，可照抄 `ArcElasticSlider.cs:439-451`）；commit 只在 `OnPointerReleased`；`ArcAnim` 提供可取消句柄或复用单计时器。

### AI-02 ElasticSlider 每 pointer move 重绘完整控件（P1）
- 位置：`src/Arc.Avalonia.AdvancedInputs/Controls/ArcElasticSlider.cs:334`
- 证据：
  ```csharp
  protected override void OnPointerMoved(PointerEventArgs e) { ... SetFromPointer(x); ... InvalidateVisual(); }  // 334-350
  // Render: 186-192 七次 ArcResources.Brush；196/202/265/275 ArcText.Create；237 new Pen
  var radius = _dragging ? ThumbRadius * 1.28 : ThumbRadius;
  context.DrawEllipse(surface, new Pen(border, 1.5), new Point(thumbX, TrackCenter), radius, radius);
  ```
- 触发/放大：值变化 → `AffectsRender`（`:78-80`），拖动/吸附/键盘动画期间每帧一次；每次 Render 7 次 `TryFindResource` + 2–4 次 `new FormattedText`（`ArcText.Create` 里还 `TryFindResource` 取字体）+ 1 个 `new Pen`。×60 Hz ×实例数（一个表单里 3–4 个滑杆时同时响应 hover 动画）。
- 影响：单实例勉强，但 FormattedText 构造在主线程不便宜，且连同 `DirtyRect` 全表面重绘（含 marks 标签循环 `:251-271`）。
- 修复建议：`ArcText.Create` 结果按 `(text,size,weight)` 缓存；`Pen`/brushes 静态缓存到主题版本（用 `ActualThemeVariantChanged` 失效）；拖动时只画 track+thumb，标签延迟到停顿/释放再画。

### AI-03 `ArcAnim.To` 每次动画新建不可取消计时器（P1）
- 位置：`src/Arc.Avalonia.AdvancedInputs/Controls/ArcAdvancedInternals.cs:22`
- 证据：
  ```csharp
  var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };   // 37
  var watch = Stopwatch.StartNew();
  timer.Tick += (_, _) => { ... apply(from + (to - from) * eased); if (progress < 1) return; timer.Stop(); ... };
  timer.Start();
  ```
  没有返回句柄、没有 `Stop`，调用方无法取消；同一属性的第二次动画会与第一次并发写同一个字段。
- 触发/放大：`ElasticSlider:469,549,565`、`RangeHistogramSlider:650,667`、`MorphSelect:525,593,912`、`ExpandingSearch:509`、`InlineEdit:290,476`、`RadioCards:478`、`BillingToggle:405`、`PhoneInput:897`。键盘按住方向键（系统重复 ~30/s）→ 同时存活 30 个 16 ms timer，全部写同一个 `_display`。×键盘重复率 ×调用点密度。
- 影响：多余的 30 Hz→30×60 Hz tick 与 `InvalidateVisual`，画面抖动；这是全仓"每控件手写 timer"模式的根源（BRIEF §1）。
- 修复建议：抽一个 `ArcTween` 句柄（`Cancel()`），每控件复用一个实例；或统一改 `Transitions`/`Animation`；至少把 timer 存字段并在新动画开始时 `Stop()`。

### AI-04 ScrubInput 拖动每 move 改文本 + 布局宽度（P1）
- 位置：`src/Arc.Avalonia.AdvancedInputs/Controls/ArcScrubInput.cs:523`
- 证据：
  ```csharp
  ApplyScrub(raw);                                  // → SetValueInternal → OnValueChanged
  // OnValueChanged (372-383): UpdateDisplay(); UpdateTrack(); UpdateAutomation();
  _box.Text = FormatValue(Value);                   // 462
  _trackFill.Width = Math.Max(0, width * fraction); // 485
  AutomationProperties.SetHelpText(_box, ArcFormat.Spoken(Value, _activeUnit));  // 496
  ```
- 触发/放大：scrub 时焦点在 handle 而不是输入框，所以 `!_box.IsFocused` 成立 → 每 move 都写 `TextBox.Text`（文本重新排版）与 `Width`（布局失效），并分配 2 个字符串。×pointer move 频率 ×实例数。
- 影响：单个控件可接受，但这是"输入热路径里做布局"的典型（L1/P1）；与 PixelsPerStep=2 的细粒度叠加后几乎每像素一次。
- 修复建议：拖动期间节流到每帧一次（`Dispatcher.Post` 去重）；轨道进度改 `RenderTransform`（ScaleTransform.X）或自绘；Automation 文本只在 commit 时更新。

### AI-05 PhoneInput 选择器搜索每按键重建全部国家行（P1）
- 位置：`src/Arc.Avalonia.AdvancedInputs/Controls/ArcPhoneInput.cs:1119`
- 证据：
  ```csharp
  private void OnSearchTextChanged(...) { BuildRows(_search?.Text ?? string.Empty); _active = 0; Highlight(0, focusSearch: false); }
  // BuildRows (941-990): _rows.Children.Clear(); foreach (var country in _visible) _rows.Children.Add(CreateRow(country));
  ```
  `CreateRow`（`:1022-1090`）每行建 `Grid` + `Border` + 3 个 `TextBlock`，其中 2 次 `ArcResources.Brush`（`:1032,1041`）；`FilterCountries`（`:992-1010`）每按键再 LINQ 过滤 + `ToList`。
- 触发/放大：选择器打开时每个按键 ×≈200 个国家行（≈1000+ 控件分配 + 400 次 token 查找），无虚拟化（`ScrollViewer` + `StackPanel`）。影响：搜索输入明显发涩；`PointerEntered` 的 `Highlight` 还会 O(n) 遍历全部行改背景（`:1102-1111`）。
- 修复建议：`BuildRows` 增量（按 `_visible` 差异 add/remove）或 `ItemsControl` + 虚拟化；行模板在 `ControlTheme` 里定义；token 用 `DynamicResource` 而非逐行 `ArcResources` 查找。

### AI-06 MentionInput 每按键重建镜像与建议（P1）
- 位置：`src/Arc.Avalonia.AdvancedInputs/Controls/ArcMentionInput.cs:462`
- 证据：
  ```csharp
  _text = _box.Text ?? string.Empty;
  ReconcileMentions(); UpdateMirror(); DetectTrigger(); UpdateMessage(); EmitChange();   // 469-474
  ```
  `UpdateMirror`（`:500-543`）每按键重排 `_mentions.OrderBy(...)` 并重建整个 `InlineCollection`（每段 1 次 `ArcResources.Brush`）；`DetectTrigger`（`:547-588`）O(文本) 回扫；`BuildSuggestions`（`:599-635`）线性扫描 + `Sort`；`RenderSuggestions`（`:662-692`）清空重建最多 `MaxSuggestions` 行（每行 6–8 控件 + 3 次 token 查找）；`EmitChange`（`:477-483`）每次 `new ArcMentionValue(_text, _mentions.ToList())`。
- 触发/放大：每次按键 ×(文本长度 + 候选人数 + 建议行数)；hover 高亮也会重建全部行（`:751-759`）。影响：建议面板打开时输入有可感延迟；`Value` 属性每按键新对象也让绑定方频繁刷新。
- 修复建议：镜像只在 `_mentions`/文本变化时 diff 更新；建议行用固定池 + `IsVisible` 过滤；`Value` 用可变对象或延迟提交；hover 只切两行 `Background`。

### AI-07 用 Width/Height/Margin 逐帧做形变动画（P1）
- 位置：`src/Arc.Avalonia.AdvancedInputs/Controls/ArcMorphSelect.cs:568`（其余同类：`ArcExpandingSearch.cs:540`、`ArcBillingToggle.cs:391`、`ArcRadioCards.cs:478`）
- 证据：
  ```csharp
  _surface.Width = fromWidth + (toWidth - fromWidth) * value;      // MorphSelect:600
  _surface.Height = fromHeight + (toHeight - fromHeight) * value;  // 601
  ...
  _highlight.Margin = new Thickness(4, value, 4, 0);               // 916
  ```
  MorphSelect 打开/关闭（0.4 s）与高亮迁移（0.42 s）都逐帧改布局属性；ExpandSearch `ApplySize` 每帧改 `_field.Width`/`_panel.Width`（`:545,551`）；BillingToggle 每帧改 `_thumb.Width`（`:412`）；RadioCards 每帧改 `_ring.Width/Height`（`:487-488`）。
- 触发/放大：每次展开/选择 ×60 fps ×每帧模板布局（MorphSelect 还会连带内部列表 `ScrollViewer` 重新测量）。影响：违反 CONVENTIONS "animate transform/opacity rather than layout"，在低端 GPU/大页面上会掉帧；MorphSelect 的"形变"本可用 `ScaleTransform`+`Clip` 近似。
- 修复建议：容器尺寸一次性设置，形变交给 `RenderTransform`（缩放/平移）与 `Clip`；必须改尺寸时用单个 `Animation` 而非手写 timer。

### MD-01 ResizablePanels 拖动 = 每 move 一次完整布局（P0）
- 位置：`src/Arc.Avalonia.Media/Controls/ArcResizablePanels.cs:558`
- 证据：
  ```csharp
  _slots[index].Share = Math.Max(0, clamped);
  _slots[index + 1].Share = Math.Max(0, total - _slots[index].Share);
  _stage.Refresh();          // 591 → InvalidateMeasure
  UpdateDividerState();      // 592
  ```
  `MeasureOverride`（`:63-106`）对每个 pane 以新宽度 `Measure`（文本按新宽度重排），`ArrangeOverride`（`:110-162`）再 `Arrange` 全部 pane + divider 并写 `Opacity/IsVisible`；`ComputeSizes`（`:165-232`）每次 `new double[count]` + 最多 3 轮 `new List<int>` + `Sum`。
- 触发/放大：每次 pointer move（`:529`）→ 全 pane measure/arrange；×pane 数 ×pane 内文本量 ×60–120 Hz。构造器还给 `LayoutUpdated` 挂了 `UpdateDividerState`（`:274`），于是**每个布局 pass** 都做 `_slots.Sum(...)`、字符串插值与 `AutomationProperties.SetName`（`:751-783`）——拖动时等于把上述成本再乘一次。
- 影响：这是 Media 区域唯一稳定的 P0：三栏带段落文本时拖动分隔条会持续掉帧，且文本重排引起反复 `TextLayout` 重建。
- 修复建议：拖动期间只移动分隔条的可视位置（`RenderTransform`）并画一个"幽灵"列宽；松手才 `Refresh()` 提交真实布局；`LayoutUpdated` 改为 `SizeChanged`/显式调用，`UpdateDividerState` 去掉 LINQ 与逐 pass 的 Automation 文案。

### MD-02 Carousel 拖拽把 arrange 当动画（P1）
- 位置：`src/Arc.Avalonia.Media/Controls/ArcCarousel.cs:46`
- 证据：
  ```csharp
  public double Progress { set { ... _progress = value; InvalidateArrange(); } }   // 46-58
  protected override Size ArrangeOverride(Size finalSize) { ... child.Arrange(...);
      if (child.RenderTransform is not ScaleTransform transform) { ...new ScaleTransform... }
      transform.ScaleX = scale; transform.ScaleY = scale;
      child.Opacity = 1 - 0.45 * distance; child.ZIndex = 100 - (int)Math.Round(distance*10); }   // 106-134
  ```
- 触发/放大：`OnPointerMoved` 每 move 写 `_stage.Progress`（`:627`）；`GoTo` 的 0.4 s 补间同样逐帧写（`:511-525`）。每帧全 slide 重排并写 `Opacity/ZIndex/Transform`（每个属性变更都触发渲染失效）×slide 数 ×60 Hz。影响：比"移动容器"多一次 arrange pass；slide 内若有图片/文本，`ZIndex` 变更还会触发子树的层级重排。
- 修复建议：拖动/补间只改 `ArcCarouselPanel` 的 `RenderTransform`（整行平移），slide 的缩放/透明度只在索引落定时更新；把 `Progress` 改成直接改 transform 并 `InvalidateVisual` 而非 `InvalidateArrange`。

### MD-03 ImageCompare 拖动每 move 重绘整面（P1）
- 位置：`src/Arc.Avalonia.Media/Controls/ArcImageCompare.cs:618`
- 证据：
  ```csharp
  protected override void OnPointerMoved(...) { ... SetPosition(PercentFromPoint(...), settle: false); }  // 618-628
  // Render (152-181): PushClip(RoundedRect) + DrawImage×2 + DrawDivider + DrawCaptions
  // DrawPlaceholder (200-238): 两个 for 循环每 44 px 一条斜线/竖线，:212 new Pen，:228-229/:235 new brush
  // DrawDivider :245 new Pen(new SolidColorBrush(...))
  // DrawCaptions (281-309): 每次 2× ArcMediaUi.Formatted → new FormattedText
  ```
- 触发/放大：`Fraction` 是 `AffectsRender`（`:55-57`），每 move 全表面重绘：2 次 `PushClip`、hatch 线条数 ≈ (W+H)/44 ×2、6 个 `new SolidColorBrush`、3–4 个 `new Pen`、2 个 `FormattedText`；`Announce`（`:550-560`）还每 move 写 `Status` 文本。×pointer move ×控件尺寸（hatch 线性）。影响：无图片（页面默认占位符）时是纯自绘，拖动可见抖动；有图片时两次全幅 `DrawImage` 也偏重。
- 修复建议：图片/占位符分别预渲染到 `RenderTargetBitmap` 缓存，拖动只画裁剪矩形与 divider；笔刷/笔静态化；captions 的 `FormattedText` 缓存（文本只在标签变化时失效）；`Announce` 只在 `settle` 或整百分比变化时写。

### MD-04 Reactions：每次点击全量重建 + 每计数一个 timer + burst 每帧建刷子（P1）
- 位置：`src/Arc.Avalonia.Media/Controls/ArcReactions.cs:260`
- 证据：
  ```csharp
  _host.Children.Clear(); _views.Clear();           // 270-271
  foreach (var reaction in _reactions) { var view = CreatePill(...); _host.Children.Add(view.Pill); }  // 273-285
  ...
  if (animateCounts) foreach (var view in _views) AnimateCount(view);   // 291-297
  ```
  `Toggle`（`:437`）每次点击 → `Sync(animateCounts: true)` 重建所有 pill；`AnimateCount`（`:386-404`）为每个 pill 起 `ArcMediaTween`（16 ms，`ArcMediaUi.cs:207`）逐帧写 `CountText.Text`；`ArcReactionBurst`（`:41`）每次添加新建控件 + 16 ms timer，`Render` 每帧 `new SolidColorBrush`×7（`:84-85,99`）+ `ArcMediaUi.Color` 查找。
- 触发/放大：每次点击 ×反应类型数（重建 ×N + N 个 tween ×60 Hz ×0.4 s）；burst 每次添加 ×7 个 brush/帧 ×26 帧。影响：一次点击产生 N 个并行文本布局动画，pill 越多越慢；burst 的 brush 分配是纯 R1。
- 修复建议：`Sync` 增量（只更新计数变化的 pill 的 `Text` 与 class）；计数动画用单个共享 timer 驱动所有 pill；burst 笔刷按 token 缓存（4 个系列色 + 1 个 ring 笔刷）。

### MD-05 ScrollArea：每个滚动事件重建渐变遮罩与计时器（P1）
- 位置：`src/Arc.Avalonia.Media/Controls/ArcScrollArea.cs:301`
- 证据：
  ```csharp
  _viewport.ScrollChanged += (_, _) => { UpdateEdges(); ShowThumbs(); };   // 301-305
  // UpdateMasks (401-412) → VerticalMask/HorizontalMask (414-446)：每次 new GradientStops + new LinearGradientBrush
  _viewport.OpacityMask = Fade <= 0 ? null : VerticalMask(top, bottom, _viewport.Bounds.Height);
  ...
  _hideTimer = new DispatcherTimer(...); _hideTimer.Start();               // 547-556，每个滚动事件新建
  ```
  overlay 自身 `Render`（`:155-191`）每次滚动/悬停变化也新建 2 个 `SolidColorBrush` + 2 次 `Color(token)`（`:162-165`）。
- 触发/放大：每次 `ScrollChanged`（滚轮、拖动、键盘滚动、程序滚动）×2 个新渐变刷 ×若干 `GradientStop`，并替换 `OpacityMask`（整层遮罩变化）；每个滚动事件还替换 hide 计时器。影响：滚动时持续分配、GC 抖动；虽然遮罩形状通常不变，但对象每次都新建。
- 修复建议：遮罩按 `(top,bottom,length,fade)` 缓存，只在边/尺寸变化时重建；`_hideTimer` 只创建一次并改 `Interval`/`Start`/`Stop`；overlay 笔刷缓存。

### MD-06 HoldToConfirm 按住进度用 Width 驱动（P1）
- 位置：`src/Arc.Avalonia.Media/Controls/ArcHoldToConfirm.cs:338`
- 证据：
  ```csharp
  var filled = width * _fraction;
  if (_fill is not null) _fill.Width = filled;    // 350
  if (_clip is not null) _clip.Width = filled;    // 355
  ```
  驱动是 `_tween.Run(..., ApplyFraction, ...)`（`:268`，16 ms，`ArcMediaUi.cs:207`），即按住期间每帧两次 `Width` 写入 → 每帧触发 fill/clip 的 measure/arrange（其父 `Grid` 含两个完整标签副本 + 图标，`UpdateVisualState:212-220` 还特意让它们同宽）。
- 触发/放大：每次按住 ×60 Hz ×按住时长（默认 2 s → ≈120 帧 ×2 次布局）。影响：单个按钮可接受，但破坏"动画只走合成"的原则；标签文本每帧被重新测量。
- 修复建议：fill 用固定尺寸 + `Clip`/`ScaleTransform.X` 的变化表达进度（render-only）；或 `DrawingContext.DrawRectangle` 自绘填充，避免任何布局。

### AA-01 ArcAdvancedGlide 用 Margin/Width/Height 迁移共享高亮（P1）
- 位置：`src/Arc.Avalonia.AdvancedActions/Controls/ArcAdvancedVisuals.cs:202`
- 证据：
  ```csharp
  Transitions = Travel(includeTravel: true);        // 244
  Margin = new Thickness(bounds.X, bounds.Y, 0, 0); // 245
  Width = Math.Max(0, bounds.Width);                // 246
  Height = Math.Max(0, bounds.Height);              // 247
  ```
  每次 `ShowAt` 都 `Travel(...)` 新建 `Transitions`（`:280-300`）；`Hide`/首次出现同样重建（`:224,262`）。Travel 是布局属性过渡，等于每个 hover 行都跑一段 layout 动画。放大：×菜单项数 ×hover 频率（快速上下移动菜单）。修复：改为单个 `RenderTransform`（`TranslateTransform` + `ScaleTransform`，尺寸用固定基准 + 缩放近似），`Transitions` 只建一次。

### AA-02 ExpandingButtonGroup 展开用 Width 过渡 + 每次重新分配 Transitions（P1）
- 位置：`src/Arc.Avalonia.AdvancedActions/Controls/ArcExpandingButtonGroup.cs:403`
- 证据：
  ```csharp
  var transitions = ArcThemeManager.ReducedMotion ? null : new Transitions { new DoubleTransition { Property = WidthProperty, ... } };
  if (animate) { slot.Button.Transitions = transitions; slot.Label.Transitions = new Transitions { ... }; }
  ...
  slot.Button.Width = targetWidth;   // 445
  ```
- 触发/放大：每次 hover/focus 切换展开项 → 为**每个**槽位（前后都 `ApplySlot`，`:396-400`）分配 2 个 `Transitions`，并对 Width 做过渡（每个槽位每帧布局）。×槽位数 ×切换频率。修复：宽度过渡改为标签 `Opacity`+`ScaleTransform`（图标宽度固定），`Transitions` 在模板加载时一次装好。

## 4. 该区域设计评价与修复优先级

### 做得好的

1. **主题/token 纪律**：四个工程控件代码零 hex、零语义 `Brushes`；代码建树也坚持 `Paint()` 绑 resource observable，accent 切换可自动跟随（`ArcContentUi.cs:41-49`、`ArcMediaUi.cs:45-53`）。
2. **reduced-motion 覆盖**：三套补间器和几乎所有过渡路径都有短路分支，这是全仓最完整的一组区域；断言类数值不看过冲（`ArcElasticSlider` 用 Snappy、`ArcRangeHistogramSlider` 的显示值走 Snappy）。
3. **键盘与可访问性**：Carousel/CardStack/ImageCompare/HoldToConfirm/ResizablePanels/ReorderableList/ScrubInput/RangeHistogramSlider 都有完整键盘替代路径 + 名称/帮助/播报；ReorderableList 的 Space 抬起模型与 ImageCompare 的 Shift 步进细节好于一般实现。
4. **指针手势的"正确姿势"**：CardStack 的拖拽（`ArcCardStack.cs:620-661`：只改 `Translate/Rotate/Opacity`）、SwipeActions 的偏移（`ArcSwipeActions.cs:288-300`：只改 `TranslateTransform`）是 render-only 的正面样板；ReorderableList 的 FLIP 也确认没有布局读数。
5. **Async 状态机**：`ArcActionButton`（`:187-217`）用 `async Task` + 状态枚举 + 计时器复位，错误回调，比手写 bool 状态清晰。

### 最该先修的 3 件事

1. **AI-01 + AI-03**（`ArcRangeHistogramSlider.cs:650`、`ArcAdvancedInternals.cs:37`）：给 `ArcAnim` 加可取消句柄、拖动期禁用 `SyncDisplay` 动画，并把 `Data` 的 min/max/分桶结果缓存到 `Data` 变化时。这是当前最确定会掉帧的一处。
2. **CT-01/CT-03 + MD-01**（`ArcRichTextEditor.cs:762`、`ArcChatThread.cs:453`、`ArcResizablePanels.cs:591`）：三条"交互一次 = 全量重建/全量布局"的主干路径。优先做：编辑预览合并解析 + 隐藏时跳过；聊天改虚拟化 + 增量追加；面板拖动改 render-only。
3. **MD-05/MD-04/CT-11/AI-02 的 per-frame 分配**（`ArcScrollArea.cs:414`、`ArcReactions.cs:270`、`ArcOnboardingChecklist.cs:790`、`ArcElasticSlider.cs:186`）：笔刷/笔/FormattedText/渐变刷按主题版本缓存，是收益/风险比最高的一类清洁修复。

### 设计层面的结构性问题（不是单点 bug）

1. **"模板 + 代码建树"混合模型**：四个工程有十余处 `BuildShell`/`Sync`/`Build` 风格的整树重建（ROI 最高的是 RichTextEditor、ChatThread、Reactions、PhoneInput 选择器、Onboarding）。只要这些内容层留在代码里，"状态变化 → 重建"就很难避免；建议把内容区改为 `ItemsControl`/模板（顺便拿到虚拟化）。
2. **三套重复补间器 + 四套重复 UI 帮助类**（`ArcAnim` / `ArcContentTween` / `ArcMediaTween`；`ArcContentUi` / `ArcMediaUi` / `ArcAdvancedVisuals` / `ArcText`）：每套都有不同的取消语义与分配行为，AI-03 的"不可取消 timer"因此只出现在 AdvancedInputs。建议上收一个 `ArcTween`（可取消、单计时器、支持 reduced-motion）到 foundation。
3. **"动画布局属性"是跨工程惯例**：MorphSelect/ExpandSearch/BillingToggle/RadioCards/ExpandButtonGroup/Glide/HoldToConfirm/Carousel 共 8 处用 `Width/Height/Margin` 逐帧动画（AI-07、AA-01、AA-02、MD-02、MD-06）。CONVENTIONS 已写明应当避免，建议把"尺寸变化"统一成 `ScaleTransform`/`Clip`/自绘，并把这 8 处列入专项。
4. **自绘控件的主题快照**（AI-08、MD-09）：`ArcResources.Brush` 是静态快照，`Render` 型控件又没有资源订阅，唯一兜底是 `ActualThemeVariantChanged`（accent 变化不触发）。建议在 foundation 提供 `ArcResources.BrushObservable(token)` + 一个 `ArcThemedControl` 基类，避免每个自绘控件各写一套缓存失效。
5. **重型控件的虚拟化缺失**：ChatThread（消息）、PhoneInput（≈200 国家）、MentionInput（≤最大建议数）、RichTextEditor 预览（块）都是 `StackPanel` 全量树；前两者是长列表，属于结构性问题，单点优化收益有限。
