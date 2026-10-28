# Inputs + Actions：组件设计与性能分析

## 0. 范围与覆盖

- **工程**：`src/Components/Arc.Avalonia.Inputs`（31 个 `.cs`）与 `src/Components/Arc.Avalonia.Actions`（37 个 `.cs`）。
- **Inputs**：`Controls/` 下 23 个文件全部完整阅读（18 个公开控件 + `ArcFieldBase` / `ArcTextFieldBase` / `ArcMenuHelper` / `CapsLockState`，外加自绘 `ArcStar`）；`Themes/` 中完整读 `ArcSegmentedControl.axaml`、`ArcSlider.axaml`，`ArcSelect.axaml` 读关键段，其余主题用 grep 扫 `Transitions`/`Width`/`Height`。5 个 `Pages/*.cs`（演示组合代码）未逐行读；3 个 `obj/` 生成文件忽略。
- **Actions**：`Controls/` 下 25 个文件；其中 23 个（含 `ArcActionModels` 的数据类部分）完整或近完整阅读，`ArcMegaMenu.cs`（2454 行）读约 68%、`ArcShareAccess.cs`（2040 行）读约 49%，其余行用 grep 覆盖 `DispatcherTimer`/`LayoutUpdated`/`Children.Clear`/`Measure(`/`PointerMoved`/`Split(`。9 个 `Pages/*.cs` 未读；`obj/` 忽略。
- **方法**：先 `grep` 定位热点再精读关键窗口；所有行号均为当前文件的真实行号。构建/截图未执行（按 BRIEF §6）。严重度按 BRIEF §3 定义，P0 仅给"必然发生在高频路径、且按实例/数据/帧数线性放大"的三条。
- **基线事实引用**：B1（`ArcResources` 无缓存）、B3（`ArcIcon` 每帧 `new Pen`）直接引用，不重复验证。

## 1. 组件设计特征

### 1.1 控件类型分布

| 区域 | TemplatedControl | ContentControl | Button 派生 | 裸 Control/自绘 |
| --- | --- | --- | --- | --- |
| Inputs（18 个公开控件） | 18（11 个经 `ArcFieldBase`，其中 4 个纯文本变体经 `ArcTextFieldBase`） | 0 | 0 | 1（`ArcStar : Control`） |
| Actions（22 个公开控件 + 1 辅助视觉） | 11 | 6（Dialog/Drawer/Popover/Tooltip/ContextMenu/Alert） | 4（ActionSwap/CopyButton/ConfirmMorph/ThemeSwitcher） | 1（`ArcGlide : Border`） |

- Inputs 的选择是合理的：`TemplatedControl` + 模板里嵌**原生 `TextBox`**（`ArcTextFieldBase.cs:115`）把焦点、选区、IME、密码掩码都交给平台；`ArcTextFieldBase` 只做外壳与 `Value` 双向同步。
- Actions 侧 `override void Render` / `InvalidateVisual` 均为 **0**（与 BRIEF 的 Actions 34/0/0/0 一致），说明该区域的"动效"全靠 `Transitions` 与"timer + 重建内容"两种手段——这正是问题所在（见 §2）。
- 自绘只占 2/41，符合"交互控件走模板"的约定；但两个自绘点都踩了 R1/R2（`ArcStar.cs:46-48`：每次 Render 两次 `ArcResources.Brush` + `new Pen`）。

### 1.2 状态与组合

- `StyledProperty` 覆盖齐全，但**派生布局值用普通字段缓存**：`ArcMegaMenu.cs:222-240` 一次性放 12 个互相依赖的字段（`_panelX/_panelTargetWidth/_panelTargetHeight/_panelScale/_appliedWidth/_availableWidth/...`），状态机全靠这些字段 + `_syncing` 布尔，不可观察、难同步；`ArcShareAccess`、`ArcSlider` 同型但规模小。
- **没有一处用 `ItemsControl` + `DataTemplate`/`ItemsSource`**：列表型控件全部手写 `List<Control>` + `Children.Clear()` 重建。`grep` 命中 Actions 12 处、Inputs 9 处 `Children.Clear()`。这是 L2 的结构性来源，也是 IN-08 / AC-03 / AC-07 的共同根因。
- `Classes` 变体映射重复：只有 `ArcFieldBase.UpdateFieldClasses()`（`:85-92`）做成了唯一入口；其余每个控件在构造函数 + 每个 `Changed` handler 里各写一遍，例如 `ArcAccordion.cs:299-302`、`ArcAlert.cs:122-125`、`ArcConfirmMorph.cs:299-302`、`ArcSwitch.cs:108-109`。没有共享的 variant→class 助手。
- **PART_ 规范不一致**：只有文本框用 `PART_Input`（`ArcTextFieldBase.cs:115` 等 6 处）；Actions 的 40+ 个模板部件全部无前缀（`Trigger`/`Overlay`/`Panel`/`Backdrop`/`FooterHost`/`GlideLayer`…，如 `ArcDialog.cs:102-106`、`ArcTabs.cs:91-94`）。`OnApplyTemplate` 大多"先解绑再绑定"（`ArcTextFieldBase.cs:102-138`、`ArcSelect.cs:118-157`、`ArcSlider.cs:159-186`、`ArcRating.cs:168-188` 做得规范），但有 5 处只加不解绑：`ArcSegmentedControl.cs:102`（`SizeChanged +=`）、`ArcTextarea.cs:77`（`InputBox.SizeChanged +=`）、`ArcPasswordStrength.cs:173`、`ArcPasswordField.cs:84,94-95`、`ArcNumberField.cs:259-277`。由于模板重应用会换掉控件实例，这些不构成"活泄漏"，属清洁度问题。

### 1.3 动效与交互路径

三条动效流派都在用，分工大体清楚：

1. **XAML `Transitions`**：主题里大量 `BrushTransition`（颜色/边框，正确用法），但夹了 3 处布局属性过渡：`ArcSegmentedControl.axaml:22-28`（`Width` 0.42s）、`ArcSelect.axaml:88-105`（`Height` 0.42s）。这两处都无法被 `ArcThemeManager.ReducedMotion` 关闭——违反 `CONVENTIONS.md:138` 的"每个动效都要有 reduced-motion 分支"。
2. **C# `new Transitions`**：Actions 13 处、每次触发新建（AC-10 汇总）。
3. **timer + 重建子树**：Actions 5 个控件（ActionSwap/CopyButton/ConfirmMorph/MegaMenu/ShareAccess）、Inputs 3 处 hint/repeat。

`CONVENTIONS.md:140` 要求"animate transform/opacity rather than layout"，实际有 **7 处**在动画布局属性：`ArcGlide`（Margin/Width/Height）、Segmented pill（Width）、Select highlight（Height）、Accordion body（Height）、MegaMenu panel/sheet（Width/Height）、ShareAccess 行/撤销条/角色按钮（Height/Width）、ExpandableText clip（Height）。

指针语义上值得肯定：**Actions 全区域 0 个 `PointerMoved` 订阅**，浮层定位走"打开时 + LayoutUpdated"而不是 pointer-move；Inputs 只有 2 处 pointer-move（`ArcSlider.cs:183`、`ArcRating.cs:184`）。

### 1.4 可访问性 / 键盘 / 焦点

- Automation 覆盖率显著高于一般移植项目：几乎每个控件在模板应用或状态变化时 `SetName`/`SetHelpText`/`SetItemStatus`/`SetLiveSetting`（如 `ArcSlider.cs:390-394`、`ArcRating.cs:239-240`、`ArcSegmentedControl.cs:195`、`ArcToast.cs:146-147`）。
- Dialog 有完整焦点陷阱（`ArcDialog.cs:252-290` 全 Tab 环 + 关闭后恢复）；Drawer 只恢复焦点、**没有 Tab 陷阱**（`ArcDrawer.cs:259` 只 `_panel.Focus()`），两者不一致。
- 键盘语义齐全：Slider/Rating/Segmented/Radio/Select/Combobox/MultiSelect/Tabs/Accordion/MegaMenu/MenuSurface 均有方向键 + Home/End + Esc；roving tab stop 落在 `ArcSegmentedControl.cs:194`、`ArcRadioGroup.cs:176-180`、`ArcChipGroup.cs:245-256`。
- 小缺陷：`ArcRating.ApplyReadOnly()` 每次调用 `new Cursor(...)`（`:283`）；`ArcPopover` 在自身范围内任意 pointer press 就打开并吞掉事件（`:121-140`）；`ArcContextMenu` 左键也开（`:117`，XML doc 已声明）。

### 1.5 token 与主题一致性

- 颜色基本走 `{DynamicResource}`/`ArcResources`；代码里的 `ArcResources.Brush` 出现在**构建期**（建行、建 chip）而不是绘制期，比 B3 式每帧查找轻，但仍是"每次重建 N 次资源查找"（AC-03/IN-07/IN-08 的放大项）。
- 尺寸/字号硬编码散布在代码里，与 `ArcTokens` 的 Sizing/Type 约定不一致：`ArcSegmentedControl.cs:124-140`（13/11）、`ArcMenuHelper.cs:31-39`（16/2）、`ArcMultiSelect.cs:258-262`（12）、`ArcRating.cs:157-162`（28/36/44）、`ArcTagInput.cs:264`（12）。

### 1.6 "34 处 DispatcherTimer" 全量清点（逐个回答）

先纠正量级：`grep DispatcherTimer src/Components/Arc.Avalonia.Actions` 的 **34 条命中 = 17 个 timer 对象**（每个对象同时有"字段声明 + 构造函数 `new`"两条）。Inputs 侧 6 条命中 = 3 个对象。**合计 20 个 timer 对象，其中 0 个是 16 ms / 逐帧 timer**，全部是一次性延迟或低频重复；`ArcSpinner`（B2）那种"每实例 16 ms 逐帧"在本区域**不存在**。

| # | 位置 | 用途 | 间隔 | 生命周期 | Detach 时 Stop | Tick 里做什么 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `ArcActionSwap.cs:44` `_hold` | 成功后停留再自动前进 | `state.HoldFor` 自定义 | 每实例（ctor） | ✗ 无 override | `Advance()` → `CommitAsync` → 重建 face（新 StackPanel/ArcIcon/TextBlock） |
| 2 | `ArcActionSwap.cs:50` `_failure` | 失败文案 1.8s 后恢复 | 1800 ms | 每实例 | ✗ | `UpdateFace()` → 重建 face |
| 3 | `ArcConfirmMorph.cs:130` `_confirmTimer` | 未回答时自动取消 | 6000 ms | 每实例 | ✓ `:685-693` | `Cancel()` → 状态切换 + 重建 face |
| 4 | `ArcConfirmMorph.cs:132` `_resultTimer` | 结果面停留后回 Idle | 5000 ms | 每实例 | ✓ | 状态切换 + 重建 face |
| 5 | `ArcConfirmMorph.cs:134` `_countdownTimer` | 确认倒计时 | 1000 ms | 每实例 | ✓ | **重建 face + `Measure(∞)` + `new Transitions`**（`:619-630`） |
| 6 | `ArcCopyButton.cs:59` `_reset` | "Copied" 2s 后回 Idle | 2000 ms | 每实例 | ✗ 无 override | `UpdateFace()` → 重建 icon/TextBlock |
| 7 | `ArcMegaMenu.cs:260` `_openTimer` | 悬停意图：首次打开 | `OpenDelay` 90 ms | 每实例 | ✓ `:504-514` | `RequestOpen` → 建整个面板 body |
| 8 | `ArcMegaMenu.cs:270` `_closeTimer` | 悬停离开宽限关闭 | `CloseDelay` 240 ms | 每实例 | ✓ | `IsOpen=false` → `ClosePanel`（含 Transitions 清理） |
| 9 | `ArcMegaMenu.cs:280` `_hideTimer` | 收起动画后再隐藏 | 220 ms | 每实例 | ✓ | `IsVisible=false`（布局） |
| 10 | `ArcMegaMenu.cs:290` `_outgoingTimer` | 滑出体移除 | 480 ms | 每实例 | ✓ | `Children.Remove`（布局） |
| 11 | `ArcMegaMenu.cs:302` `_sheetHideTimer` | 折叠面板延迟隐藏 | 200 ms | 每实例 | ✓ | `IsVisible=false`（布局） |
| 12 | `ArcShareAccess.cs:227` `_undoTimer` | 5s 撤销窗口 | 5000 ms | 每实例 | ✓ `:450-459` | 丢弃 pending person、隐藏撤销条 |
| 13 | `ArcShareAccess.cs:237` `_copyTimer` | 链接复制确认 | 1800 ms | 每实例 | ✓ | 文案回 Idle |
| 14 | `ArcShareAccess.cs:244` `_commitTimer` | 折叠行移除 | 300 ms | 每实例 | ✓ | `Children.Remove` |
| 15 | `ArcShareAccess.cs:256` `_settleTimer` | 角色按钮宽度动画收尾 | 520 ms | 每实例 | ✓ | 把所有行按钮 `Width=NaN`（布局） |
| 16 | `ArcToast.cs:52` `_timer` | 自动消失 | `Duration` 4500 ms | 每实例 | ✓ `:196-200` | `Close()` |
| 17 | `ArcTooltip.cs:104` `_delay` | 悬停延迟显示 | `Delay` 250 ms | **每次 enter 新建** | ✗ | `IsOpen=true` → 建 Border+TextBlock |
| 18 | `ArcNumberField.cs:284` `_repeatTimer` | 按住步进 | 400→80 ms | **每次按下新建** | ✗ | `StepBy` → 写 `TextBox.Text` + Classes + 事件 |
| 19 | `ArcNumberField.cs:431` `_hintTimer` | 限值提示隐藏 | 1600 ms | 每次提示新建 | ✗ | `IsVisible=false` |
| 20 | `ArcMoneyInput.cs:447` `_hintTimer` | 范围提示隐藏 | 1800 ms | 每次提示新建 | ✗ | `IsVisible=false` |

结论：定时器数量本身**不是**本区域的 P0（Avalonia 的 `DispatcherTimer` 停止时不占 CPU；16 个 per-instance 常驻 timer 只花内存）。真正的 T1 成本在第 5、18 两条的 Tick 内容（重建子树/写文本），以及第 1/2/6/17 条的"detach 后仍会触发"。

最危险的 5 个 timer：

1. `src/Components/Arc.Avalonia.Actions/Controls/ArcConfirmMorph.cs:134` + `:619-630` —— 每秒重建整个 face + 强制 Measure + 新建 `Transitions`。
2. `src/Components/Arc.Avalonia.Inputs/Controls/ArcNumberField.cs:284-294` —— 按住时每 80 ms 写 `TextBox.Text`（文本布局）+ `Classes.Set` + 事件。
3. `src/Components/Arc.Avalonia.Actions/Controls/ArcMegaMenu.cs:290-300` —— 与 panel 的 Width/Height 动画同帧叠加，出栈时移除子树。
4. `src/Components/Arc.Avalonia.Actions/Controls/ArcShareAccess.cs:256-264` —— 520 ms 后把所有角色按钮 `Width` 重置，触发布局。
5. `src/Components/Arc.Avalonia.Actions/Controls/ArcActionSwap.cs:44-55` / `ArcCopyButton.cs:59-64` —— 无 Detach stop，且 Tick 内重建 face 控件。

### 1.7 四类弹出层实现与代价

| 机制 | 使用者 | 打开时是否重建 | 关闭后残留 |
| --- | --- | --- | --- |
| Avalonia `Popup` | Select/Combobox/MultiSelect（`ArcMenuHelper.ConfigurePopup:57-64`） | 每次打开 `BuildMenu`/`ApplyFilter` 全量重建 `ListBoxItem` | 无 timer；Popup 关闭即脱离 |
| 页面内 `Canvas` overlay | DropdownMenu/SplitButton/ButtonGroup/ContextMenu/Popover/Tooltip/MegaMenu/ShareAccess | 每次 `new ArcMenuSurface`/`new Border`/`new Border+"ContentPresenter"` | `ArcDismiss` 已配对 Dispose（`ArcActionVisuals.cs:342-417`）；`LayoutUpdated` 常驻订阅不随关闭解除 |
| 面板内填满 | Dialog/Drawer（ContentControl 自填） | 不重建（模板常驻） | 仅首次模板应用建 `Transitions`（`:120-137`） |
| StackPanel 宿主 | Toast/ToastHost | 每个 toast `new ArcToast` + 新 `Transitions`（`ArcToastHost.cs:69-83`） | Toast 自身 timer 在 detach 停 |

## 2. 性能问题清单

| ID | 分类 | 严重度 | 位置 | 问题一句话 | 放大因子 |
| --- | --- | --- | --- | --- | --- |
| AC-01 | T2+L1 | **P0** | `ArcActionVisuals.cs:282-290,302-336` | 共享滑动高亮 `ArcGlide` 用 `Margin/Width/Height` + `Transitions` 做动画 → 每帧布局 | × 每次 hover/选择 × 25–40 帧 × 使用点 5 处 × 行数 |
| AC-02 | L1+T2+G1 | **P0** | `ArcMegaMenu.cs:1187-1227,1285-1352`；`ArcActionsTheme.axaml:372-377` | 面板弹簧动画跑在 `Width/Height/RenderTransform` 上，+`BoxShadow` 每帧重栅格 | × 每次切换 section × 25 帧 × body 子控件数 |
| IN-01 | L1+P1 | **P0** | `ArcSlider.cs:254-260,276-288,321-348` | 拖动每次 pointer-move 改 `Fill.Width` → 布局 pass；同时 3 次数值格式化 + Automation 写 | × 拖动帧数（60/s）× 页面祖先链/兄弟数 |
| AC-03 | L2 | P1 | `ArcActionVisuals.cs:187-198`；`ArcMenuSurface.cs:96-100,114-141` | 每次打开重建整个菜单表面，且行集合被构建两次，无缓存 | × 打开次数 × 行数 |
| AC-04 | L1+R1 | P1 | `ArcPagination.cs:40,73-89`；`ArcExpandableText.cs:57-64,189-217,219-235`；`ArcMegaMenu.cs:314-355,843-872`；另 9 处 | 12 处 `LayoutUpdated` 常驻订阅：强制 Measure / 每次分配字符串 / 每 pass 定位 | × 布局 pass 数（任何布局动画期间 = 60/s）× 实例数 |
| AC-05 | L1 | P1 | `ArcAccordion.cs:194-221,237-269` | 展开动画跑在 `Height` 上 + 每次打开强制 `Measure(∞)` | × 行数 × 25 帧 × 内容复杂度 |
| AC-06 | R1+S2 | P1 | `ArcShareAccess.cs:736-757,1050-1140` | 邮箱输入每键重建 5 行建议 + 每项 `Split` 分配 + 强制 `Measure` | × 按键数 × directory 条目数 |
| AC-07 | L2 | P1 | `ArcMegaMenu.cs:1032-1107,1285-1352` | 每次切换 section 销毁并重建整个面板 body（Grid/列/链接/feature 卡） | × section 数 × 每组链接数 × hover 次数 |
| IN-02 | T2+L1 | P1 | `ArcSegmentedControl.axaml:22-28`；`ArcSegmentedControl.cs:205-237` | 选中 pill 的 `Width` 0.42s 过渡；无 reduced-motion 分支 | × 选择次数（方向键连按 = 连续动画）× 帧数 |
| IN-03 | L1+T2 | P1 | `ArcSelect.axaml:88-105`；`ArcSelect.cs:322-344` | 菜单高亮 `Height` 0.42s 过渡，每次选中/方向键都跑 | × 行数导航次数 × 25 帧 |
| IN-04 | R1+S2+L2 | P1 | `ArcPasswordStrength.cs:224-268,270-309,311-317` | 每次按键 new 4 个 rule 对象 + 4 个 lambda + 重建 4 行规则 | × 按键数 × 规则数 |
| IN-05 | S2+L1 | P1 | `ArcTextarea.cs:77,84-88,127-163` | AutoGrow 每键 `new FormattedText` + 写 `Height`，SizeChanged 回环导致重复测量 | × 按键数 × 2（回环） |
| IN-06 | R1+S2 | P1 | `ArcMoneyInput.cs:201-234,238,279-292` | 每键 LINQ `Where().ToArray()` + 两次写 `TextBox.Text` + Automation 属性 | × 按键数 |
| IN-07 | L2+R2 | P1 | `ArcCombobox.cs:231-271` | 每次过滤清空并重建全部菜单项，每项一次 `FindTheme` | × 按键数 × 选项数（>50 时升 P0） |
| IN-08 | L2 | P1 | `ArcMultiSelect.cs:195-245,266-293`；`ArcChipGroup.cs:111-174`；`ArcTagInput.cs:240-258`；`ArcRadioGroup.cs:92-161` | 值变化即全量重建子控件，无 `ItemsControl`/虚拟化 | × 选项数 × 每次交互 |
| IN-09 | R1+R2 | P2 | `ArcStar.cs:34-62` | 每颗星每次 Render `new Pen` + 2 次 `ArcResources.Brush` | × 星数 × 评级实例 × 重绘次数 |
| AC-08 | T2 | P2 | `ArcConfirmMorph.cs:378-425,619-630` | 每次换脸 `new Transitions` + `Children.Clear` + 强制 `Measure(∞)` | × 状态切换次数 |
| AC-09 | T1/M1 | P2 | `ArcActionSwap.cs:44-55`；`ArcCopyButton.cs:59-64`；`ArcTooltip.cs:103-113` | Detach 时不停表，timer 会在控件离开树后继续触发 | × 快速导航/换页 |
| AC-10 | T2 | P2 | `ArcMegaMenu.cs:1200,1304,2088,2263`；`ArcShareAccess.cs:519,1340,1538,1659,1864`；`ArcToastHost.cs:69`；`ArcAccordion.cs:205` 等 | 每次触发 `new Transitions` + `TransformOperations.Parse("...")` 字符串解析 | × 交互次数 |
| AC-11 | M1 | P2 | `ArcSegmentedControl.cs:102`；`ArcTextarea.cs:77`；`ArcPasswordStrength.cs:173`；`ArcPasswordField.cs:84,94-95`；`ArcNumberField.cs:259-277` | 模板重应用时重复挂接事件（旧控件被替换故不双触达） | × 主题切换次数 |
| AC-12 | R1 | P2 | `ArcPagination.cs:82`；`ArcRating.cs:283`；`ArcMenuHelper.cs:31-39` | 每个布局 pass 分配 signature 字符串；每次 ReadOnly 变更 `new Cursor`；硬编码尺寸不进 token | × 布局 pass / 配置变更 |
| IN-10 | L1 | P2 | `ArcSlider.cs:315-319,396-428` | 每次 SizeChanged（含窗口拖动每帧）清空并重建 marks 的 Canvas 子项 | × marks 数 × resize 帧 |
| IN-11 | L1 | P2 | `ArcTextFieldBase.cs:281-296,340-369` | 每键刷新图标/清除按钮的 `IsVisible/Opacity/IsHitTestVisible/IsTabStop` | × 按键数 × 模板子项 |
| IN-12 | T1 | P2 | `ArcNumberField.cs:284,431`；`ArcMoneyInput.cs:447` | hint/repeat timer 每次交互新建且从不 Dispose | × 交互次数（GC 压力） |

## 3. 逐条说明

### AC-01 ArcGlide 用布局属性做滑动高亮（P0）

- 位置：`src/Components/Arc.Avalonia.Actions/Controls/ArcActionVisuals.cs:282-290`（`ShowAt`），`:302-336`（`Motion()`）；使用点 `ArcTabs.cs:302`、`ArcMenuSurface.cs:260`、`ArcButtonGroup.cs:344`、`ArcMegaMenu.cs:1706,1745`。
- 证据（`ArcActionVisuals.cs:282-290`，`:309-320`）：

  ```csharp
  public void ShowAt(Rect bounds, bool animate = true)
  {
      Transitions = animate ? Motion() : null;
      Margin = new Thickness(bounds.X, bounds.Y, 0, 0);
      Width = bounds.Width;
      Height = bounds.Height;
  ```
  `Motion()` 里是 `ThicknessTransition{MarginProperty}` + 两个 `DoubleTransition{WidthProperty/HeightProperty}`（`:311-328`）。
- 触发/放大：任何"高亮移动"都会启动 0.42 s 动画 → 每帧改 Margin/Width/Height → 每帧让 `ArcGlide` 及其父 Panel 的 measure 失效 → **每帧一次布局 pass**，约 25 帧/次。使用点 5 处、每个菜单/标签栏/按钮组至少 1 个 ArcGlide；鼠标在 8 个菜单行上扫过 = 8 段 × 25 帧 = 200 次布局 pass。`Transitions` 本身有 `_motion ??=`（`:309`，好），成本全在动画属性选择上。
- 影响：菜单/标签/按钮组 hover 是最高频交互，直接表现为 hover 卡顿；S 面板里多个使用点叠加时更明显。
- 修复建议：只动 `RenderTransform`（`TranslateTransform.X/Y` + `ScaleTransform`），不碰 Margin/Width/Height；或用 `Canvas.SetLeft/Top` + `Bounds` 固定的 Border（Canvas 定位不触发 measure）。若必须变换尺寸，考虑 `ScaleTransform` 或自绘一个 `Control` 在 `Render` 里画圆角矩形（亚像素即可）。

### AC-02 MegaMenu 面板动画跑在 Width/Height 上，阴影每帧重栅格（P0）

- 位置：`src/Components/Arc.Avalonia.Actions/Controls/ArcMegaMenu.cs:1187-1227`（`ApplyPanelMotion`），`:1285-1352`（`BuildBody`）；阴影 `src/Components/Arc.Avalonia.Actions/Themes/ArcActionsTheme.axaml:372-377`（`Border.mm-panel` → `ArcShadowFloating`）。
- 证据：

  ```csharp
  _panel.Transitions = new Transitions
  {
      new DoubleTransition { Property = WidthProperty, ... },
      new DoubleTransition { Property = HeightProperty, ... },
      new TransformOperationsTransition { Property = RenderTransformProperty, ... },
  ```
  随后 `BuildBody` 里 `_panel.Width = width; _panel.Height = height;`（`:1333-1337`），面板带 `BoxShadow`（主题 `:377`）。
- 触发/放大：每次打开/切换 section 触发 `OpenPanel(animate:true)` → `ApplyPanelMotion` 新建 Transitions（每次 4 个对象）→ 0.42 s 内每帧同时改 Width + Height + RenderTransform → 每帧 measure/arrange + **每帧重画 BoxShadow（模糊半径大、面板面积大）**。放大因子：每次 hover 切换 × 25 帧 × 面板子树规模；窄屏 sheet 同样（`:2088-2104` 的 Height 过渡 + `:2415-2440` 的高度回写）。
- 影响：MegaMenu 是本区域最贵的单次交互；Width/Height 动画 + 阴影重绘在集成显卡上很容易掉到 30 fps 以下。
- 修复建议：面板宽高用一次 `Measure` 定稿后固定，移动/缩放只用 `RenderTransform`（`ScaleTransform` + `TranslateTransform`，`RenderTransformOrigin` 已在 `:494` 设好）；过渡里的 Width/Height 换成 Transform；阴影在动画期间切到无阴影或缓存为 9-patch/位图。

### IN-01 Slider 拖动在 pointer-move 里写 Width（P0）

- 位置：`src/Components/Arc.Avalonia.Inputs/Controls/ArcSlider.cs:254-260`（`OnTrackMoved`），`:276-288`（`SetFromPointer`），`:321-348`（`UpdateVisuals`）。
- 证据：

  ```csharp
  // UpdateVisuals
  _thumbTransform.X = x;
  if (_fill is not null) { _fill.Width = x + 10; }   // :337 —— 布局属性
  ...
  _readout.Text = ShowValue ? FormatValue(Value) : null;   // :354 每 move 一次格式化
  ```
  模板里 `Fill` 是 `HorizontalAlignment="Left" Width="0"` 的 Border（`ArcSlider.axaml:38-44`），缩略图用的是 `TranslateTransform`（正确）。
- 触发/放大：拖动期间每次 `PointerMoved` → `Value` 变 → `UpdateVisuals` → `Fill.Width` 写值 → 祖先链 measure 失效（每帧合并成 1 次布局 pass，但拖动 = 每秒 60 次）；同一函数还做 3 次 `ToString("0.##")`（Readout/BubbleText/Automation HelpText）、两次写 `TextBlock.Text`、`AutomationProperties.SetHelpText`（`:390-394`）。放大因子 = 拖动帧数 × 页面祖先链/兄弟数（布局 pass 会沿根路径重跑 Measure/Arrange）。
- 影响：单滑块自身很便宜，但"每帧布局"与页面其它布局动画叠加时无法合并；设置面板常见"多个滑块 + 说明文字"，同页多个滑块各自拖动仍是一次共享布局 pass，主要风险是深树/密集页。
- 修复建议：`Fill` 改成固定宽度满轨 + `ScaleTransform.ScaleX`（或自绘轨道的 `Control.Render`）；`Readout` 文本只在整数变化时更新；`AutomationProperties` 只在提交（`ValueCommitted`）时写；把 `UpdateVisuals` 拆成"几何（transform）"与"文本"两条路径。

### AC-03 弹出层每次打开重建视觉树，且行集合被构建两次（P1）

- 位置：`src/Components/Arc.Avalonia.Actions/Controls/ArcActionVisuals.cs:187-198`（`ArcMenuPresenter.Open`）；`src/Components/Arc.Avalonia.Actions/Controls/ArcMenuSurface.cs:96-100`（`Open`）与 `:114-141`（`BuildRows`）、`:143-168`（`BuildRow`）。
- 证据：

  ```csharp
  // ArcMenuSurface
  public void Open(bool focus = true) { BuildRows(); SetActive(FirstEnabled(), focus); }  // :96-100
  ```
  而 `OnApplyTemplate` 已经 `BuildRows()`（`:72-79`），`Items` 变更也 `BuildRows()`（`:41`）—— 所以一次打开至少构建 2 次行集合，第二次把第一次的行全部丢弃。
- 触发/放大：`ArcMenuPresenter.Open`（`ArcActionVisuals.cs:187-198`）每次 `new ArcMenuSurface`，关闭时 `SetOverlay(canvas, null)` 直接丢弃（`:211`），**没有任何缓存/复用**。每行成本 = `ArcButton` + `Grid` + 可选 `ArcIcon` + `TextBlock` + 1–2 个 `new Binding`（`ArcActionVisuals.cs:58-80`）。放大因子 = 打开次数 × 行数 × 2（重复构建）。
- 影响：菜单/上下文菜单/拆分按钮下拉的打开延迟随行数线性增长；页面里多个菜单轮换打开时 GC 压力明显。
- 修复建议：表面只创建一次，行用 `ItemsControl` + `DataTemplate` 或 `ListBox` 复用容器；`Open` 不调 `BuildRows`（只 `SetActive`）；关闭时 `IsVisible=false` 而非移出 Canvas（配合 `ArcDismiss` 已有配对清理）；Binding 换成 `{DynamicResource}` 样式 setter 或一次性绑定。

### AC-04 12 处 `LayoutUpdated` 常驻订阅，其中含强制 Measure 与分配（P1）

- 位置：`ArcPagination.cs:40`（`LayoutUpdated += (_, _) => Build()`）、`ArcExpandableText.cs:57-64`、`ArcMegaMenu.cs:314-355`、`ArcTabs.cs:56`、`ArcTooltip.cs:58`、`ArcDropdownMenu.cs:47-55`、`ArcContextMenu.cs:47-55`、`ArcSplitButton.cs:59-67`、`ArcButtonGroup.cs:90-98`、`ArcAccordion.cs:62-68`、`ArcShareAccess.cs:266-277`、`ArcPopover.cs:51-59`。
- 证据（Pagination 每次布局 pass 都分配一个字符串，`:82-88`）：

  ```csharp
  var signature = $"{page}:{count}";
  if (signature == _built && _buttons.Children.Count > 0) { return; }
  _built = signature;
  _buttons.Children.Clear();
  ```
  证据（ExpandableText 在宽度变化时强制测量两次并新建 TextBlock，`:206-210`、`:227`）：`_body.Measure(...)` ×2；`_toggle.Content = new TextBlock {...}`。
  证据（MegaMenu 在宽度变化时对 brand + 每个 trigger + 每个 action 强制 `Measure(Size.Infinity)`，`:850-868`）。
- 触发/放大：`LayoutUpdated` 在每次布局 pass 后回调（Avalonia 语义，具体"仅受影响控件"的窄化范围待确认 suspected）。关键放大来自**布局动画**：只要页面上有 AC-01/AC-02/AC-05/IN-02/IN-03 这类布局动画在跑，布局 pass 就是 60/s，这 12 处订阅就变成 60/s 的强制 Measure / 定位 / 字符串分配。放大因子 = 布局 pass 频率 × 实例数。
- 影响：单个订阅多为微秒级，但"动画 × 订阅数"叠加会造成持续 CPU 占用与 Gen0 分配；MegaMenu 的强制测量会在大页面上放大成毫秒级。
- 修复建议：改为"尺寸变化才跑"——用 `SizeChanged`/`AffectsMeasure` 属性回调或缓存 `Bounds` 比较（MegaMenu/Accordion/ExpandableText 已有宽度比较，Pagination 没有）；Pagination 把 signature 换成分量比较 `(page, count)`；绝不在 `LayoutUpdated` 里调 `Measure`；`ArcGlide` 不再动布局后，Tabs/ButtonGroup/MenuSurface 的定位订阅可以只在打开/选择时跑一次。

### AC-05 Accordion 展开动画跑在 Height 上（P1）

- 位置：`src/Components/Arc.Avalonia.Actions/Controls/ArcAccordion.cs:194-221`（`body.Transitions`）、`:237-269`（`ApplyOpen`）。
- 证据：

  ```csharp
  body.Transitions = new Transitions {
      new DoubleTransition { Property = HeightProperty, Duration = ArcMotion.SpringSmoothDuration, ... },
      new DoubleTransition { Property = OpacityProperty, ... } };
  ...
  row.Presenter.Measure(new Size(Math.Max(0, Bounds.Width - 16), double.PositiveInfinity));  // :253
  row.Body.Height = height;                                                                 // :260
  ```
- 触发/放大：每次展开/收起 → 0.4 s 内每帧改行高 → `StackPanel` 每帧重排所有行（行数 × 子元素），并让 `ContentPresenter` 里的 `TextBlock` 每帧重新断行（S2/L1 叠加）。放大因子 = 行数 × 25 帧 × 内容长度。
- 影响：展开单行在长文案（`MaxWidth=640` 的 wrap 文本）上可见掉帧；整页 FAQ 常见 8–12 行。
- 修复建议：用 `RenderTransform` 的 `ScaleTransform.ScaleY` + `ClipToBounds` 做展开视觉（内容不做真实高度动画），或用 `Animation` 只动 `Opacity`+`Clip` 矩形；至少要缓存 `MeasureHeights` 结果，避免每次都强制 `Measure`。

### AC-06 ShareAccess 邮箱输入每键重建建议列表（P1）

- 位置：`src/Components/Arc.Avalonia.Actions/Controls/ArcShareAccess.cs:736-757`（`OnFieldTextChanged`）、`:1050-1140`（`UpdateSuggestions`）。
- 证据：

  ```csharp
  var matches = directory.Where(item =>
      item.DisplayName!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
          .Any(part => part.StartsWith(query, ...)) ...)      // :1062-1070 每键、每项都 Split
      .Take(5).ToList();
  ...
  _suggestionRows!.Children.Clear();                          // :1086
  // 每个 match：new ArcButton + StackPanel + 2×TextBlock + Avatar(Border+TextBlock)
  _suggestions.Measure(new Size(..., double.PositiveInfinity));   // :1135 强制测量
  ```
- 触发/放大：每次 `TextChanged` → LINQ（每项一次 `Split` 数组分配）+ 最多 5 行 × ~6 个控件重建 + 强制 Measure + 写 `Width/Height`。放大因子 = 按键数 × directory 条目数（Split 是 O(directory)）+ 5 行控件重建。
- 影响：输入邮箱时每个字符都产生数十个分配和一次强制布局，长会话/大目录下 GC 抖动明显。
- 修复建议：预计算一次 `DisplayName` 分词（`ArcShareSuggestion` 上缓存 `Parts`）；行控件复用（固定 5 行，只改文本/`IsVisible`）；去掉 `Measure`，用 `DesiredSize` 或固定高度。

### AC-07 MegaMenu 每次切换 section 全量重建面板 body（P1）

- 位置：`src/Components/Arc.Avalonia.Actions/Controls/ArcMegaMenu.cs:1032-1107`（`OpenPanel`）、`:1285-1352`（`BuildBody`）、`:1370-1434`（`BuildSectionBody`）。
- 证据：

  ```csharp
  ApplyPanelMotion(animate && !first);
  BuildBody(section!, direction, animate && !first);      // :1053-1054 每次切换
  ...
  body.Measure(new Size(width, double.PositiveInfinity));  // :1328
  body.Height = height;                                    // :1330
  ```
  `BuildSectionBody` 每次新建 `StackPanel` + `Grid` + 每组 `StackPanel` + 每个链接 `ArcButton`+`Grid`+`Border`+`TextBlock`，`BuildLink`/`BuildFeature` 另有图标与文案控件。
- 触发/放大：鼠标在 trigger 之间移动（每次 `OnTriggerEnter` → `RequestOpen`，`:1775-1801`）→ 整棵 body 重建；放大因子 = section 切换次数 × 每组链接数。
- 影响：MegaMenu 的"光标下滑"体验（`docs/03-implementation.zh.md:126`）依赖频繁切 section，每次切换既重建树又跑 Width/Height 动画，是本区域最重的交互。
- 修复建议：每个 section 的 body 首次构建后缓存，切换只切 `IsVisible`/`TranslateTransform`；把"缓存 body"限制在最近 2–3 个 section 以控内存；面板尺寸只在首次确定，之后固定。

### IN-02 SegmentedControl 的 pill 用 Width 过渡（P1）

- 位置：`src/Components/Arc.Avalonia.Inputs/Themes/ArcSegmentedControl.axaml:22-28`；代码 `src/Components/Arc.Avalonia.Inputs/Controls/ArcSegmentedControl.cs:205-237`。
- 证据：

  ```xml
  <Border.Transitions>
    <Transitions>
      <DoubleTransition Property="Width" Duration="0:0:0.42"
                        Easing="{x:Static motion:ArcMotion.SpringMorph}" />
  ```
  代码里 `_pill.Width = selected.Bounds.Width; _pillTransform.X = selected.Bounds.X;`（`:225-226`/`:232-233`）。
- 触发/放大：每次选择（点击或方向键）→ 0.42 s 每帧改宽 → `Panel`/`Track` 每帧 measure+arrange；方向键连按会让动画不断重启 → 连续布局动画。**XAML 里的过渡无法被 `ArcThemeManager.ReducedMotion` 关闭**（`UpdatePill` 只处理 `animate` 参数，主题过渡仍在）。
- 影响：分段控件通常出现在页头/工具栏，持续布局抖动会扩散到整页（祖先链重排）。
- 修复建议：pill 宽度固定为 max 段宽并用 `ScaleTransform`/`Clip` 表达，或给 pill 一个固定槽位；过渡改 `TranslateTransform.X`（已经有一个）；在 `UpdatePill` 里根据 `ReducedMotion` 设置 `_pill.Transitions = null`（代码路径已有此逻辑，`:221-228`，需覆盖主题默认）。

### IN-03 Select 高亮用 Height 过渡（P1）

- 位置：`src/Components/Arc.Avalonia.Inputs/Themes/ArcSelect.axaml:88-105`；代码 `src/Components/Arc.Avalonia.Inputs/Controls/ArcSelect.cs:322-344`。
- 证据：`DoubleTransition Property="Height"` 0.42 s（`:90-92`）+ `TranslateTransform.Y`（`:99-102`）；代码每次选中写 `_highlight.Height = container.Bounds.Height; _highlightTransform.Y = point.Value.Y;`（`:341-343`）。
- 触发/放大：每次 `SelectionChanged`（`ArcSelect.cs:148`），包括按住方向键连续移动 → 动画反复重启 → 连续布局动画；放大因子 = 导航步数 × 25 帧。同型问题在 `ArcMultiSelect`（复用 `ArcSelectMenuItemTheme`）也会遇到。
- 修复建议：高亮高度改为固定行高（行高一致时 Height 不需要动画），只动 Y；或把高亮做成 `RenderTransform` 缩放的固定高度条。

### IN-04 PasswordStrength 每次按键重建规则行与规则对象（P1）

- 位置：`src/Components/Arc.Avalonia.Inputs/Controls/ArcPasswordStrength.cs:224-268`（`UpdateStrength`）、`:270-309`（`UpdateRuleRows`）、`:311-317`（`BuildDefaultRules`）。
- 证据：

  ```csharp
  var rules = Rules ?? BuildDefaultRules();          // :227 每键 new 4 个 ArcPasswordRule + 4 个 lambda
  var met = rules.Count(rule => rule.Test(password)); // :228 每次调用枚举
  ...
  _rulesList.Children.Clear();                        // :277
  // 每条规则：new StackPanel + new ArcIcon + new TextBlock + 2 次 ArcResources.Brush
  ```
- 触发/放大：`TextChanged` → `Value` → `OnValueChanged` → `UpdateStrength`（每次按键至少 1 次，`:202-213`）。放大因子 = 按键数 × 规则数（默认 4）；每个默认规则内部还有 1–3 次 LINQ（`Any(IsUpper)`、`Any(IsLower)`、`Any(IsDigit)`、`Any(!IsLetterOrDigit)`）。
- 影响：密码输入是典型高频路径，每键 ~12 个控件 + ~10 次资源查找 + ~8 个 lambda/数组分配，长密码下 `Any` 还会多遍扫描。
- 修复建议：默认规则做成 `static readonly`（lambda 捕获 `ShortLength` 的改为传参或缓存按长度）；规则行只建一次，之后只切 `IsVisible`/`Classes`；`ValueChanged` 的 args 分配可改为复用（或在无订阅者时跳过）。

### IN-05 Textarea AutoGrow 每键 FormattedText + 写 Height（P1）

- 位置：`src/Components/Arc.Avalonia.Inputs/Controls/ArcTextarea.cs:84-88`（`OnInputChanged`）、`:127-163`（`UpdateAutoGrow`）、`:77`（`InputBox.SizeChanged += (_, _) => UpdateAutoGrow();`）。
- 证据：

  ```csharp
  var formatted = new FormattedText(text.Length == 0 ? " " : text, ..., typeface, InputBox.FontSize, Brushes.Black)
      { MaxTextWidth = Math.Max(40, width) };                 // :150 每键 new
  var lines = Math.Max(Rows, (int)Math.Ceiling(formatted.Height / lineHeight));
  InputBox.Height = lines * lineHeight + 4;                   // :162 写布局属性
  ```
- 触发/放大：每键 → `UpdateAutoGrow` → `Height` 写值 → `SizeChanged` → 再 `UpdateAutoGrow`（第二次值相同，但 `FormattedText` 已再建一次）。放大因子 = 按键数 × 2 次文本排版；含换行/长文本时 `FormattedText` 成本随文本长度增长。
- 修复建议：用 `TextLayout`/`TextBlock` 的 `Measure` 结果缓存按 (text长度, width) 键；只在行数真正变化时写 `Height`；把 `SizeChanged` 解绑并在模板重应用时重挂（见 AC-11）；或用 `TextBox` 自带的 `AcceptsReturn` + 外层 `ScrollViewer` 的 `VerticalScrollBarVisibility=Auto` 自适应。

### IN-06 MoneyInput 每键 LINQ 解析 + 两次写 Text（P1）

- 位置：`src/Components/Arc.Avalonia.Inputs/Controls/ArcMoneyInput.cs:201-234`（`OnTextChanged`）、`:236-277`（`ParseDraft`）、`:279-292`（`SyncInput`）。
- 证据：

  ```csharp
  var filtered = new string(text.Where(character => char.IsDigit(character) || character == '.').ToArray());  // :238
  ...
  SetCurrentValue(ValueProperty, draft.Amount);              // :224 → OnValueUpdated → SyncInput
  _input.Text = draft.Display;                               // :230
  ```
  `SyncInput` 再写一次 `_input.Text = FormatMinor(...)`（`:287`）并设置 `AutomationProperties.SetName/SetHelpText`（`:290-291`）。
- 触发/放大：每键 1 次 `Where().ToArray()`（字符数组 + 枚举器 + 新字符串）、2–4 次子串、1 次 `ToString("#,0")`、**两次 `TextBox.Text` 赋值**（每次触发文本布局 + caret 处理）、2 次 Automation 写。放大因子 = 按键数 × 金额位数。
- 修复建议：`ParseDraft` 用 `Span<char>`/单遍循环去掉 LINQ；`OnTextChanged` 只做格式重写一次，`SyncInput` 在 `_syncing` 期间跳过（避免双写）；Automation 属性改为失焦时写。

### IN-07 Combobox 每次过滤重建整个菜单（P1）

- 位置：`src/Components/Arc.Avalonia.Inputs/Controls/ArcCombobox.cs:231-271`（`ApplyFilter`）。
- 证据：

  ```csharp
  var matches = options.Where(option => Matches(option, query)).ToList();   // :239-241
  var theme = ArcMenuHelper.FindTheme(this, "ArcMenuItemTheme");            // :243 每键资源查找
  _menu.Items.Clear();                                                       // :245
  foreach (var option in matches) { var item = ArcMenuHelper.CreateItem(...); ... }  // :247-256
  ```
- 触发/放大：输入每键 → 全量 clear + 每个命中项 new `ListBoxItem`+`Grid`+`TextBlock`+`ArcIcon`+`ArcResources.Brush`（`ArcMenuHelper.cs:19-55`）。放大因子 = 按键数 × 命中项数；选项 >50 时（国家/时区选择）每次按键创建 50+ 控件，属 P0 级（本仓当前 demo 选项少，故记 P1）。
- 修复建议：`ListBox` 绑定 `ItemsSource` + `DataTemplate` + `CollectionView` 过滤（容器虚拟化）；主题在 `OnApplyTemplate` 取一次缓存到字段；`Matches` 的 `Keywords?.Any` 在选项上预小写缓存。

### IN-08 列表型输入控件"值变化即全量重建"（P1）

- 位置（4 个代表 + 另有 3 处同类）：`ArcMultiSelect.cs:195-245,266-293`、`ArcChipGroup.cs:111-174`、`ArcTagInput.cs:240-258`、`ArcRadioGroup.cs:92-161`；同类还有 `ArcSegmentedControl.cs:106-169`、`ArcRating.cs:196-221`、`ArcPasswordStrength`（IN-04 已列）。
- 证据：

  ```csharp
  // ArcMultiSelect.RebuildChips :205-208
  var labels = options.Where(option => selected.Contains(option.Value, StringComparer.Ordinal))
      .Select(option => option.Label).ToList();
  _chipPanel.Children.Clear();   // :202
  ```
  `Toggle()` 里同时 `RebuildChips()` + `RebuildMenu()`（`:388-389`）；`RebuildChips` 末尾 `AutomationProperties.SetHelpText(this, string.Join(", ", labels))`（`:244`）每次重建一次全量字符串。
- 触发/放大：一次勾选 = 全量重建 chip（每 chip 一个 Border+TextBlock）+ 全量重建菜单行（每行 Grid+Border+ArcIcon+TextBlock+3 次 `ArcResources.Brush`，`:295-345`）；放大因子 = 选项数 × 每次勾选。`ArcChipGroup` 的 `Rebuild` 更彻底：`Options`/`Value`/`Multiple`/`MaxVisible` 任一变化全量重建（`:111-174`），每次点击都是一次全量重建 + 焦点恢复 `Dispatcher.Post`（`:166-173`）。
- 影响：多选/标签/chip 是"每次点击都要用"的控件，全量重建造成点击后 ~10–50 ms 的卡顿（选项越多越明显），并丢失滚动/焦点状态（靠 Post 补）。
- 修复建议：改用 `ItemsControl`（`ItemsSource` = 选项 + `Selected` 状态对象）+ `DataTemplate`；只更新受影响容器的 class（`Classes.Set("selected", ...)`）；`AutomationProperties` 的 `HelpText` 只在打开/提交时写。

### IN-09 / AC-08 / AC-09 / AC-10 / AC-11 / AC-12 / IN-10 / IN-11 / IN-12（P2，合并说明）

- **IN-09 每帧分配/资源查找（`ArcStar.cs:34-62`）**：`_geometry` 有静态缓存（好），但 `new Pen(...)` 与 2 次 `ArcResources.Brush` 在每次 `Render` 里。悬停预览只在跨星时重绘（`ArcRating.cs:300-309` 的 `Fill` 同值不触发 `AffectsRender`），所以频率可控；修复：`Pen` 按 (fill>0) 做两个 `static readonly`，颜色缓存到资源变更时刷新（`OnResourcesChanged` 已有，`:82`）。
- **AC-08 每次换脸新建 Transitions + 强制 Measure（`ArcConfirmMorph.cs:378-425`）**：`_faces.Children.Clear()` + `face.Measure(Size.Infinity)` + `new Transitions{DoubleTransition}`（`:414-422`）。一次确认交互 ~7 次换脸 → ~7 个 Transitions + 7 次强制测量；倒计时模式每秒一次（`:619-630`）。修复：Transitions 只建一次并复用；宽度差值用 `RenderTransform.ScaleX` 表达。
- **AC-09 Detach 不停表（`ArcActionSwap.cs:44-55`、`ArcCopyButton.cs:59-64`、`ArcTooltip.cs:103-113`）**：这些 timer 是字段、Tick 闭包捕获控件；控件离开树后 timer 仍会在 0.25–2 s 后触发并 `SetCurrentValue`/重建 face。修复：三处补 `OnDetachedFromVisualTree` → `Stop()` + 清 `_delay`（参照 `ArcToast.cs:196-200`、`ArcMegaMenu.cs:504-514` 的写法）。
- **AC-10 每次触发 `new Transitions` + `TransformOperations.Parse`（P2）**：`ArcMegaMenu.cs:1200,1304,2088,2263`、`ArcShareAccess.cs:519,1340,1538,1659,1864`、`ArcToastHost.cs:69-83`、`Accordion.cs:205`、`ExpandableText.cs:109,133`、`Dialog.cs:122`、`Drawer.cs:142`、`ArcConfirmMorph.cs:414`。`TransformOperations.Parse("translateX(0px)...")` 每次都解析字符串（`:308-309`/`:1863` 等），应缓存为 `static readonly` 的 `TransformOperations` 或直接 `new MatrixTransform`。
- **AC-11 模板重应用重复挂接（P2）**：见 §1.2 的 5 处；补解绑即可（`SizeChanged`/`Click`/`KeyDown`）。
- **AC-12 每 pass 分配与硬编码（P2）**：`ArcPagination.cs:82` 每 pass 一个插值字符串；`ArcRating.cs:283` 每次 `new Cursor`；`ArcMenuHelper.cs:31-39`、`ArcSegmentedControl.cs:124-140`、`ArcMultiSelect.cs:252-262` 的尺寸/字号散落（违反 token 约定，也让换尺寸/换主题不跟随）。
- **IN-10 Slider marks 每次 resize 重建（`ArcSlider.cs:315-319,396-428`）**：`SizeChanged` → `RebuildMarks` → `Canvas.Children.Clear()` + 每 mark 一个 Border；窗口拖动时每帧重建。修复：marks 只建一次，resize 时只改 `Canvas.SetLeft`。
- **IN-11 每键刷新 affordance（`ArcTextFieldBase.cs:281-296,340-369`）**：每键设 2 个图标 `Icon/IsVisible`、清除按钮 `IsVisible/Opacity/IsHitTestVisible/IsTabStop`；值未变时属性系统会短路，但空↔非空时触发一次模板重排。修复：按 `hasText` 布尔变化才更新。
- **IN-12 hint/repeat timer 每次新建（`ArcNumberField.cs:284,431`、`ArcMoneyInput.cs:447`）**：交互频率低，属 GC 压力与清洁度；同时两处 hint timer 在 detach 时不停（与 AC-09 同类）。

## 4. 该区域设计评价与修复优先级

**做得好的地方**

1. **控件基类选择准确**：输入类统一 `TemplatedControl` + 模板内嵌原生 `TextBox`，焦点/选区/IME/掩码零成本继承平台能力；`ArcFieldBase`（`:85-92`）把 size/invalid/disabled 的 class 映射收敛到一处，是全区唯一被正确抽象的变体逻辑。
2. **可访问性覆盖率高**：几乎每个控件都有 `AutomationProperties` 与完整的键盘路径（Home/End/Esc/方向键/roving tab stop），Dialog 的焦点陷阱（`ArcDialog.cs:252-290`）在移植项目里少见。
3. **全区域 0 个 `Render` override、0 个 `InvalidateVisual`**（Actions），说明没有"自绘图表化"的过度设计；热点都集中在可替换的动画/重建策略上，修复成本低、收益大。
4. **悬停意图与安全三角形**（`ArcMegaMenu.cs:1775-1873`）实现得干净：只用 Enter/Exit + 90/240 ms 两个一次性 timer + 一次三角形判定，没有在 pointer-move 里做测量——这是本区域最正确的交互性能设计。
5. **`ArcGlide` 的 Transitions 与 `ArcDismiss` 的生命周期**分别做了缓存（`:309`）与配对 Dispose（`:394-402`），说明作者知道订阅要收敛；问题只出在"动的是布局属性"。

**最该先修的 3 件事**

1. **把 7 处布局属性动画改成变换动画**（AC-01、AC-02、IN-02、IN-03、AC-05、ShareAccess 三处）：一次性消灭本区域大部分"每帧布局 pass"，收益最大、改动局部。
2. **给弹出层加缓存/减少重建次数**（AC-03、AC-07、IN-07、IN-08）：菜单行构建两次 + 每次打开重建 + 每个命中项重建，是打开延迟与 GC 的主因。
3. **输入热路径去掉"每键重建子树/每键强制测量"**（IN-04、IN-05、IN-06、AC-06）：默认规则静态化、建议行复用、去掉 `Measure`、避免双写 `Text`。

**结构性问题（不是单点 bug）**

- **缺少数据驱动的列表层**：41 个控件里 0 个用 `ItemsControl`/`DataTemplate`/虚拟化，18 处手写 `List<Control>` + `Children.Clear()`。这同时造成 L2 性能问题和状态丢失（焦点/tab stop 靠 `Dispatcher.Post` 打补丁）。建议在 `Arc.Avalonia.Inputs` 里先做一个共享的 `ArcOptionList`（`ItemsControl` + `ControlTheme` 行模板 + 选择状态），让 Select/Combobox/MultiSelect/ChipGroup/SegmentedControl/RadioGroup/MenuSurface 七个控件共用。
- **动画策略没有统一出口**：XAML Transitions、C# `new Transitions`、timer 重建三条路径并存，reduced-motion 只在 C# 路径生效。建议在 `ArcMotion` 加一个 `ArcMotion.LayoutTransitions(prop)` 白名单/黑名单并让主题过渡走 `DynamicResource` 布尔开关，禁止在动画里使用 `Width/Height/Margin/Padding`。
- **`LayoutUpdated` 被当成"布局变化通知"通用钩子**（12 处）：一旦有布局动画，它就以 60/s 的频率执行强制测量与定位。应统一改成 `SizeChanged` + 显式定位调用（打开/选择时各一次），并把 `Measure` 从事件回调里彻底移除。
- **定时器本身不是问题，Tick 的内容是**：20 个 timer 全部低频一次性，说明作者没有重蹈 `ArcSpinner` 式逐帧覆辙；但 ConfirmMorph 倒计时/NumberField 按住重复这类"Tick 里重建控件/写文本"的写法应改成"Tick 只改一个标量，渲染交给绑定/变换"。
