# ADR 0004 · Top 内建控件皮肤：Fluent 12.1.4 资源键覆盖可行，47 键覆盖、0 键需模板替换

> 状态：**Accepted**（M0-5 结论，2026-11-05）
> 对应：`docs/14-m0-baseline-and-gates.zh.md` §6（M0-5 POC-B）、`docs/13-framework-layering-and-agent-top.zh.md` v3.2 §2.5 ① / D3
> 证据工程：`poc/TopControlSkinPoc/`（不进 `ArcUI.slnx`；只引用 Foundation 取 token 值，不引用任何 Arc 组件包）

## 1. 问题

v3.2 §2.5 的假设：Fluent 12.1.4 的裸内建控件外观由**资源键**驱动；在 Arc 的 `ThemeDictionaries` 里覆盖这些键，能否在**不改模板**的前提下让裸控件呈 Arc 外观？这决定 M2 主题 15 人日的置信度。

## 2. 方法（可复算）

1. 从运行时把 Fluent 的真实资源清单转储出来（`FluentTheme.Resources` 递归 + 主题字典），确认键名而不是猜（**清单规模**：基础资源 69 项；`Default` 主题字典 732 键；`Dark` 731 键；另有 2×29 项的合并字典）；
2. `MainWindow.axaml` 左右两栏：左 = 原生 Fluent 12.1.4（无覆盖），右 = 同一批裸控件 + `Border.Resources` 内的 `ResourceDictionary.ThemeDictionaries`（Light/Dark 两套、值直接取 Arc token）；
3. **双证**：① 资源断言（`Border.TryFindResource(key, variant)` 命中且值等于 Arc token）＋ ② 控件属性回读（`TextBox.Background/BorderBrush/Padding/MinHeight/CornerRadius`、`ComboBox.*`、`TabItem.*`、`PART_SelectedPipe.*`、`Thumb.Background`、轨道 `Rectangle.Fill`、`Bounds` 宽度）＋ ③ 截图（`docs/images/poc/skin-before-after-{light,dark}.png`）；
4. 键名不确定时先 `TryFindResource` 探测；仍不确定的，做**单键注入 sweep**（一次只往 `Application.Resources` 写一个候选键，回读控件属性，再撤销），用"哪个键能改动属性"定位真正驱动键（ScrollBar 静止 thumb、TabItem 内边距/高度就是这样定位的）。

刷新（POC 的 `--probe` 模式）会打印：Arc token 值、Fluent 资源清单、候选键 before/after 解析值、每个控件的可视树属性、键 sweep 结果。

## 3. 结论：分类表

图例：**纯键**＝只用 `ThemeDictionaries` 覆盖即可生效；**局部 ControlTheme**＝需要 `ControlTheme TargetType=… BasedOn=Fluent` 的局部覆盖；**模板替换**＝需重写模板。

### 3.1 `TextBox` — 纯键覆盖（11 键），余 1 项字号需局部 ControlTheme

| 覆盖键 | 验证点（双主题） | 生效 | 归类 |
|---|---|---|---|
| `TextControlBackground` / `PointerOver` / `Focused` | `TextBox.Background`：`#66ffffff`→ ArcSurface / `#66000000`→ `#191919` | ✅ | 纯键 |
| `TextControlBorderBrush` / `PointerOver` / `Focused` | `TextBox.BorderBrush`：`#99000000`→ ArcBorder | ✅ | 纯键 |
| `TextControlForeground` / `PlaceholderForeground` | 文本/占位色 | ✅（资源解析 + 渲染） | 纯键 |
| `TextControlSelectionHighlightColor` | 选区高亮 | ✅（资源解析） | 纯键 |
| `TextControlThemeMinHeight` | `TextBox.MinHeight` 32 → **44**（Arc `ControlHeightMd`） | ✅ | 纯键 |
| `TextControlThemePadding` | `TextBox.Padding` `10,6,6,5` → `16,10,16,10` | ✅ | 纯键 |
| `ControlCornerRadius` | `TextBox.CornerRadius` 3 → **18**（Arc `RadiusControl`） | ✅ | 纯键 |
| `ControlContentThemeFontSize` | `TextBox.FontSize` 仍 14 → **无效果** | ❌ | **局部 ControlTheme** |

- 圆角是**全局**键：`ControlCornerRadius` 同时驱动 TextBox/ComboBox/ComboBoxItem/TabItem 选中下划线，是"Arc 一眼可辨"的最大杠杆（Fluent 12.1.4 默认 3，不是 11.x 的 4）。
- 焦点边框：`TextControlBorderBrushFocused` 键解析正确（Arc 用 `ArcForeground`，而非 Fluent 蓝 `#0078d4`），但 Fluent 的焦点是 2px 边框 + `UseSystemFocusVisuals=True` 的系统焦点框；Arc 若要自绘 `:focus-visible` 焦点环需额外样式（M2 决策项，不是键问题）。

### 3.2 `ComboBox` — 纯键覆盖（16 键），余 1 项字号需局部 ControlTheme

| 覆盖键 | 验证点 | 生效 | 归类 |
|---|---|---|---|
| `ComboBoxBackground` / `PointerOver` / `Focused` | `ComboBox.Background` | ✅ | 纯键 |
| `ComboBoxBorderBrush` / `PointerOver` | `ComboBox.BorderBrush` | ✅ | 纯键 |
| `ComboBoxForeground`、`ComboBoxPlaceHolderForeground` | 前景/占位 | ✅ | 纯键 |
| `ComboBoxDropDownBackground`、`ComboBoxDropDownBorderBrush` | 下拉面板 | ✅（资源解析） | 纯键 |
| `ComboBoxItemBackgroundPointerOver` / `Selected`、`ComboBoxItemForeground(Selected)` | 列表项 | ✅（资源解析） | 纯键 |
| `ComboBoxPadding` | `ComboBox.Padding` `12,5,0,7` → `16,10,12,10` | ✅ | 纯键 |
| `ComboBoxThemeMinWidth` | 渲染宽度 90 → **140**（注意：控件 `MinWidth` 属性仍 0，键作用于模板） | ✅ | 纯键 |
| `ComboBoxMinHeight` | `ComboBox.MinHeight` 32 → **44** | ✅ | 纯键 |
| `ControlCornerRadius` | `ComboBox.CornerRadius` 3 → 18 | ✅ | 纯键 |
| `ControlContentThemeFontSize` | `ComboBox.FontSize` 仍 14 | ❌ | **局部 ControlTheme** |

### 3.3 `TabControl` / `TabItem` — 纯键覆盖（11 键，含字号）

| 覆盖键 | 验证点 | 生效 | 归类 |
|---|---|---|---|
| `TabItemHeaderFontSize` | `TabItem.FontSize` **24 → 14** | ✅ | 纯键 |
| `TabItemHeaderMargin` | `TabItem.Padding` `12,0,12,0` → `0,0,16,0` | ✅ | 纯键 |
| `TabItemMargin` | `TabItem.Margin` | ✅ | 纯键 |
| `TabItemMinHeight` | `TabItem.MinHeight` 48 → **40** | ✅ | 纯键 |
| `TabItemHeaderForegroundUnselected` / `Selected` | `TabItem.Foreground`（`#99000000`→ArcSecondary / `Black`→ArcForeground） | ✅ | 纯键 |
| `TabItemHeaderBackgroundUnselected` / `Selected` / `*PointerOver` | 选中/悬停底色 | ✅（资源解析） | 纯键 |
| `TabItemHeaderSelectedPipeFill` | `PART_SelectedPipe.Background`（Fluent 蓝 → ArcForeground），且 `CornerRadius` 随 `ControlCornerRadius`=18 | ✅ | 纯键 |

> 这条最出乎意料：Fluent 12.1.4 的 TabItem 标题字号是 **24px**（桌面版 WinUI 风格），但它**是键驱动的**（`TabItemHeaderFontSize`），Arc 的 14px 标签不需要改模板。

### 3.4 `ScrollBar`（V/H）— 纯键覆盖（7 键）；"自动收缩"行为需样式

| 覆盖键 | 验证点 | 生效 | 归类 |
|---|---|---|---|
| `ScrollBarSize` | 垂直 `Bounds.Width` 16 → **10**；水平 `Bounds.Height` 16 → **10** | ✅ | 纯键 |
| `ScrollBarPanningThumbBackground` | **静止态 thumb 填充** `#7a7a7a` → ArcBorderStrong | ✅ | 纯键（键名反直觉，见 §4.1） |
| `ScrollBarThumbFillPointerOver` / `Pressed` | hover/pressed 填充 | ✅（资源解析；截图未覆盖 hover） | 纯键 |
| `ScrollBarTrackFill` / `ScrollBarTrackStroke` / `ScrollBarBackground` | 轨道/背景（`Rectangle.Fill` `#e6e6e6` → Transparent） | ✅ | 纯键 |
| 自动收缩（非 hover 时 thumb 缩到 ≈2px，由 `VerticalSmallScrollThumbScaleTransform` / `HorizontalSmallScrollThumbScaleTransform` + 伪类样式驱动） | 资源键无法"按状态"改变；POC 为截图把两个 transform 置 `scale(1)` | — | **需局部样式/行为覆盖**（非键值，非模板） |
| 上下/左右箭头按钮 | `ScrollBarButton*` 系列 28 键在清单中（Arc 若要隐藏箭头需改模板或样式） | 未测 | 需评估（可能模板替换） |

## 4. 关键坑（升级时会踩）

### 4.1 Fluent 11.x 的键名在 12.1.4 已改名/消失（抽样约 30%）

| 11.x 常见写法 | 12.1.4 实际 | 证据 |
|---|---|---|
| `TextControlPadding` | `TextControlThemePadding` | 旧名 `<missing>`；覆盖新名后 `TextBox.Padding` 变化 |
| `ContentControlThemeFontSize` | `ControlContentThemeFontSize` | 旧名 `<missing>`；新名 14 |
| `ScrollBarThumbFill`（基础态） | 基础态不存在；静止 thumb 用 `ScrollBarPanningThumbBackground` | 单键 sweep：只有后者能改 `Thumb.Background` |
| `TabItemFontSize` | `TabItemHeaderFontSize` | 旧名 `<missing>`；新名默认 24 |
| `TabItemHeaderPadding` | `TabItemHeaderMargin`（值**当作 Padding** 用） | sweep：改它 → `TabItem.Padding` 变 |
| `ComboBoxPlaceholderForeground` | `ComboBoxPlaceHolderForeground`（大写 H） | 旧名 `<missing>` |
| `ComboBoxThemeMinWidth` 作用于控件 `MinWidth` 的假设 | 作用于**模板**（控件 `MinWidth` 仍 0，渲染宽度变） | 断言前后 `MinWidth=0`，`Bounds.Width` 90→140 |

**因此 M2 必须交付**：① 键常量表（`ArcFluentKeys`）；② 一条自动化校验（对每个键在 Light/Dark 各 `TryFindResource`，缺失即失败）；③ Fluent 升级核对清单。

### 4.2 Fluent 12.1.4 的资源规模

| 组 | 键数（单主题） |
|---|---:|
| `SystemControl*` | 109 |
| `ComboBox*` | 53 |
| `ScrollBar*` | 28 |
| `TextControl*` | 27 |
| `TabItem*` | 19 |
| `Button*` | 13 |
| 主题字典合计 | 732（Default）/ 731（Dark） |

即"全量覆盖"一个控件表面上看要 20–50 键，但**Arc 只需要其中 7–16 键**就能达到目标视觉（本 POC 实测），剩余键属于 hover/disabled/高对比等边缘态。

### 4.3 覆盖作用域

POC 用 `Border.Resources` 承载 AFTER 的 `ThemeDictionaries`（为了一张图里放 before/after）。机制与"应用级 `ThemeDictionaries`"完全相同（都是 `ResourceDictionary.ThemeDictionaries` + `DynamicResource` 的层级解析），但**真实实现必须放在应用级/主题字典**；`BEFORE` 栏验证了"最近可见 provider 优先"——同一窗口内两个作用域互不污染。

## 5. "纯键覆盖"占比与 M2 15 人日

按本 POC 的 4 个控件、**47 个键**（同一批键 Light/Dark 各写一套 → 94 项；另有 2 个 `ThemeDictionaries` 的 `x:Key="Light"/"Dark"`）：

| 分类 | 键数 | 说明 |
|---|---:|---|
| 纯键覆盖（资源断言 + 控件属性回读**双证**） | **19** | 截图可辨的主视觉：底色/边框/圆角/高度/内边距/宽度/字号/前景色/thumb/轨道 |
| 纯键覆盖（资源解析确认；hover/pressed/dropdown/选中态未逐键做属性断言） | 27 | 机制与上一行相同，只是截图只覆盖静止态 |
| 覆盖后对样本控件无视觉效果 → 需局部 `ControlTheme` | **1** | `ControlContentThemeFontSize`（TextBox/ComboBox 字号在模板里是固定值） |
| 需**模板替换** | **0** | — |

* 纯键覆盖：**46/47 = 98%**；模板替换：**0**。
* 按**控件**计：4/4 控件的主视觉（底色/边框/圆角/尺寸/内边距/字号/强调色元素）都能纯键覆盖，**0/4 需要模板替换**。
* 断言规模：POC-B 输出 **72 条断言（Light 36 + Dark 36）全部 PASS**，exit 0；POC-A 51 条全 PASS。
* 真正需要"非键"工作的地方只有 3 处：① TextBox/ComboBox **字号**（局部 ControlTheme，1 个补丁可同时覆盖两个控件）；② ScrollBar **自动收缩**行为（样式/行为覆盖，非键值）；③ 焦点视觉（`:focus-visible` 自绘焦点环 vs 接受 Fluent 2px 边框，属 M2 决策项）。

### 工作量结论（回答："M2 主题 15 人日够不够"）

* 纯键覆盖阶段：每控件约 2–4 h（探键 + 双主题取值 + 断言）＋ 1–2 h 截图/回归 ⇒ **15–20 控件 ≈ 10–14 人日**（与 v3.2 的 15 人日基本吻合）；
* 但 v3.2 的 15 人日**没有计入**：局部 `ControlTheme` 补丁（字号、焦点环、ScrollBar 收缩行为、箭头按钮）≈ **3–5 人日**；键常量表 + 升级检测 + 升级清单 ≈ **2–3 人日**（12.1.4 已把约 30% 的常见键改名，这条不可省）。

**建议**：
1. M2 主题块从 **15 → 18–20 人日**（含 3 人日升级防护）；若硬保 15 人日，则把 Top 清单从 20 收敛到 **15 个控件**，并把"焦点视觉/自动收缩"降级为 M3；
2. 先做**全局键**（`ControlCornerRadius`、`ControlContentThemeFontSize`、`SystemControl*` 子集、`ScrollBarSize`），它们一次性改变全部控件观感，性价比最高（本 POC 只用了 2 个全局键就让 4 个控件全部圆角化）；
3. 每控件只维护"7–16 个 Arc 需要的键"，其余键不覆盖（Fluent 默认在 Arc 里也基本可接受），避免 732 键全量维护；
4. 交付物里必须包含"未覆盖控件/状态清单"（Menu、ToolTip/Flyout/Popup、TreeView、Expander、Slider 等尚未 POC 验证）。

## 6. 未覆盖（M2 待补）

* hover / pressed / disabled / 键盘焦点状态的**截图**（本 POC 只截图静止态；键已在资源层验证）；
* `Menu/MenuItem/ContextMenu`、`ToolTip/Flyout/Popup`、`TreeView`、`Expander`、`Slider`、`CheckBox/RadioButton/ToggleSwitch`、`ProgressBar`、`Window` 背景；
* `ScrollBar` 箭头按钮的隐藏（可能需模板）；
* 高对比度主题（`HighContrast`）、DPI 缩放；
* 8 accent × 4 控件的组合矩阵（POC-A 已证明 accent 可经 `FluentTheme.Palettes` 进入 Fluent，POC-B 只出 Neutral）。

## 7. 复现

```powershell
dotnet build poc\TopControlSkinPoc\TopControlSkinPoc.csproj -m:1 -nodeReuse:false
dotnet poc\TopControlSkinPoc\bin\Debug\net10.0\TopControlSkinPoc.dll            # 2 张 PNG + 断言，exit 0
dotnet poc\TopControlSkinPoc\bin\Debug\net10.0\TopControlSkinPoc.dll --probe    # 资源清单 + 候选键 + 可视树 + 键 sweep
```

## 8. 截图（`docs/images/poc/`，均 ≤1200 长边、≤300KB）

| 文件 | 内容 |
|---|---|
| `skin-before-after-light.png` | Light：左 Fluent 默认 / 右 Arc token 键覆盖（TextBox、ComboBox、TabControl、ScrollBar V+H） |
| `skin-before-after-dark.png` | Dark：同上 |
