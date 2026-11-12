# Avalonia 12.1.4 文本/布局运行时判定 —— 用于 DSHRemote「Markdown 文字重叠」

> 角色：Avalonia 文本运行时分析。范围：只回答「Avalonia 12.1.4 的哪条语义能/不能解释截图症状」，
> 不改 `src`。本文所有结论分为 **已证实 / 推论 / 无法证实** 三档，并给出 `file:line` 或 URL。
> 根因闭环（DSHRemote 把 `ArcMarkdownView` 的 `PART_Host` 从 `StackPanel` 换成裸 `Panel`）由集成层
> 归因；本文只提供 Avalonia 侧的源码级判定与脚注引用。

---

## 0. 结论摘要（先看这里）

| # | 结论 | 档位 |
|---|------|------|
| 1 | Avalonia 12.1.4 的 `Panel`（`Avalonia.Controls`）**没有自己的 `MeasureOverride`/`ArrangeOverride`**，继承 `Layoutable` 的默认实现；默认 `ArrangeOverride` 把**每个**子控件都 `Arrange(new Rect(finalSize))`，即所有子控件占同一个 `(0,0,finalSize)` 矩形 → 裸 `Panel` **不堆叠**子控件 | **已证实**（源码 + 本机实验） |
| 2 | 默认 `MeasureOverride` 用同一个 `availableSize` 量所有子控件，返回 **max(宽)/max(高)**（不是求和），所以裸 `Panel` 的 `DesiredSize` 只等于「最高的那个子块」，不随子块数量增长 | **已证实**（源码 + 实验） |
| 3 | `StackPanel` 自己覆写了这两个方法，按排列方向累加坐标 → 段落、列表、代码块才会顺序堆叠 | **已证实**（源码 + 实验） |
| 4 | 因此「同一个 `ArcMarkdownView` 内多个顶层块（段落 + 紧随的列表）画在同一基线、列表首行被段落压住、列表其余行正常」**完全可以由集成层的 `Panel` 模板解释**；Avalonia 布局层对此负「语义正确」责任，不是缺陷 | **已证实**（语义）+ **推论**（与截图的对应关系） |
| 5 | `TextBlock` 显式 `LineHeight` 的语义：行盒**被固定**为 `LineHeight`，字体度量更大时**不扩张行盒**，而是把基线顶到 `-ascent`（`baseline = -ascent`），字形/内联控件向下溢出到相邻行 | **已证实**（源码 + 实验） |
| 6 | 上述第 5 条**不是**本次截图重叠的成因：它造成的是「相邻行竖向压字 / 基线不齐 / 行内控件压住下一行」，与截图里「同一基线上两段文字水平叠压」不是同一现象 | **已证实**（语义）+ **推论**（排除） |
| 7 | 本次截图里 `Panel` 重叠与 x86_64/arm64 **不是**因果关系：`Panel` 的 arrange 行为是确定性的、与架构无关；模拟器与真机表现不同只能由「两边的构建/模板状态或消息片段的空行结构不同」解释 | **推论** |
| 8 | Android 上 `FontFamily.Default` 走系统默认字体，用户可替换系统字体（真机截图里键盘/状态栏也是手写体可佐证）；Avalonia 不读取 `fontScale` | **推论**（见 §5） |

---

## 1. 权威判定：`Panel` 在 Avalonia 12.1.4 里不堆叠子控件

### 1.1 源码（Avalonia 12.1.4 tag）

`Panel` 本身只负责 `Children` 集合、背景绘制和视觉子节点挂接，**没有覆写布局方法**：

- <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Controls/Panel.cs>
  - `public class Panel : Control, IChildIndexProvider`
  - 该文件内声明的成员只有：`BackgroundProperty`、`Children`、`IsItemsHost`、`Render(...)`、
    `AffectsParentArrange/AffectsParentMeasure`、`ChildrenChanged(...)`、`InvalidateMeasureOnChildrenChanged()`；
    **没有任何 `MeasureOverride` / `ArrangeOverride`**。
  - 注意类注释写着 “All children are layed out to fill the panel.”——这句话就是默认实现的行为：
    每个子控件都铺满整个 Panel 矩形，而不是依次堆叠。

默认实现来自 `Layoutable`：

- <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Base/Layout/Layoutable.cs>
  - `protected virtual Size MeasureOverride(Size availableSize)`：遍历 `VisualChildren`，
    对每个 `Layoutable` 调 `Measure(availableSize)`（**同一个可用尺寸**），返回
    `new Size(max(子宽), max(子高))` —— 取最大值，**不是累加**。
  - `protected virtual void ArrangeOverride(Size finalSize)`：
    ```csharp
    var arrangeRect = new Rect(finalSize);      // 左上角恒为 (0,0)
    foreach (var visual in VisualChildren)
        if (visual is Layoutable layoutable)
            layoutable.Arrange(arrangeRect);    // 每个子控件都拿同一个矩形
    return finalSize;
    ```
    即：**所有子控件被 arrange 到同一个 `(0,0,finalSize)`**，视觉上完全重叠、都从左上角开始画。

`StackPanel` 覆写了这两个方法（`src/Avalonia.Controls/StackPanel.cs`），按 `Orientation` 累加偏移
并在 `MeasureOverride` 里累加高度/宽度，因此才有堆叠效果。

### 1.2 本机实验复核（`.recon/PanelProbe`，Avalonia.Headless 12.1.5 + Skia 软件渲染）

> 说明：本机 NuGet 缓存只有 `Avalonia.Headless 12.1.5`（`D:\.nuget\packages\avalonia.headless\`），
> 无 12.1.4 headless；`Panel.cs` / `Layoutable.cs` 在 12.1.4 与 12.1.5 两个 tag 上逐行一致
> （两版 raw 源码比对），所以该实验对 12.1.4 语义有效。

反射确认覆写归属（`dotnet run` 输出）：

```
=== LAYOUT METADATA (Avalonia 12.1.5.0) ===
  Panel.MeasureOverride   -> declared by Avalonia.Layout.Layoutable
  Panel.ArrangeOverride   -> declared by Avalonia.Layout.Layoutable
  StackPanel.MeasureOverride -> declared by Avalonia.Controls.StackPanel
  StackPanel.ArrangeOverride -> declared by Avalonia.Controls.StackPanel
```

两个 `TextBlock`（每个 `LineHeight=22`）分别放进 `Panel` 与 `StackPanel`：

```
=== T1: bare Panel with two TextBlocks ===
  Panel.Bounds=0,0,420,160  DesiredSize=102,22          <-- max，不是 44
    child[0] Bounds=(0,0,420x160)                        <-- 同一 origin
    child[1] Bounds=(0,0,420x160)                        <-- 同一 origin
=== T2: StackPanel with two TextBlocks ===
  StackPanel.Bounds=0,0,420,160  DesiredSize=102,44
    child[0] Bounds=(0,0,420x22)
    child[1] Bounds=(0,22,420x22)                        <-- 顺序堆叠
```

Markdown 形态（段落块 + 列表块 + 尾块）放进 `ScrollViewer > Panel(PART_Host)`：

```
=== T3: ScrollViewer > bare Panel host ===
  child[0] (段落 "最新 APK：")  Bounds=(0,0,420x320)  DesiredSize=81,22
  child[1] (列表容器)           Bounds=(0,0,420x320)  DesiredSize=407,68
  child[2] (尾块)               Bounds=(0,0,420x320)  DesiredSize=212,22
=== T4: 同样内容换成 StackPanel host ===
  child[0] Bounds=(0,0,420x22)
  child[1] Bounds=(0,22,420x68)
  child[2] Bounds=(0,90,420x22)
```

截图与产物：`.recon/overlap/probe_panel.png`、`probe_stackpanel.png`、`probe_panel_host.png`、
`probe_stackpanel_host.png`；工程在 `.recon/PanelProbe/`（可 `dotnet run -c Debug` 复跑）。

### 1.3 脚注引用（可直接放进报告）

> Avalonia 12.1.4 中 `Panel` 不提供堆叠布局：`Panel` 未覆写 `MeasureOverride`/`ArrangeOverride`
> （`src/Avalonia.Controls/Panel.cs`，<https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Controls/Panel.cs>），
> 实际使用 `Layoutable` 的默认实现——所有子控件都 `Arrange` 到同一个 `new Rect(finalSize)`
> （`src/Avalonia.Base/Layout/Layoutable.cs` 的 `ArrangeOverride`，
> <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Base/Layout/Layoutable.cs>）。
> 上游 Arc 的 `ArcMarkdownView` 模板本身就要求宿主是堆叠容器：
> `src/Components/Arc.Avalonia.Markdown/Themes/ArcMarkdownView.axaml:22` 用
> `<StackPanel x:Name="PART_Host" .../>`，而 `ArcMarkdownView.cs:290` 只按 `[TemplatePart("PART_Host", typeof(Panel))]`
> 取宿主（`StackPanel` 派生自 `Panel`，所以类型检查通过、语义却被换掉）。

---

## 2. 为什么这个语义能解释截图的「段落 + 列表首行叠压」

链路上每一步都在本仓库/DSHRemote 仓库里可查：

1. **DSHRemote 按空行切块**：`MarkdownBodyView.SplitTopLevelBlocks` 只在空行处 flush
   （`D:\Code\VibeCoding\DSHRemote\app\src\DshRemote.App\Controls\MarkdownBodyView.cs:210-248`），
   多块时每块包一个 `ArcMarkdownView` 放进自己的 `StackPanel`（同文件 `:110-130`）。
2. 因此 `**最新 APK**：` 后面紧跟 `- <url>` / `- 直链：…` / `- 1.2.5 (18)…` 这一组（`_dev/final-msg.md:47-50`）
   **没有空行 → 是同一个 chunk → 进同一个 `ArcMarkdownView`**。
3. `ArcMarkdownView` 把 chunk 里的**每个顶层块**各建一个控件后加到宿主：
   `ArcMarkdownView.cs:499` `_host.Children.Clear()`、`:529` / `:597` `_host!.Children.Add(control)`。
4. 宿主若被 re-template 成裸 `Panel`（DSHRemote `AppStyles.axaml` 修复前的写法），
   由 §1 的语义 → 段落控件与列表控件都拿到 `(0,0,W,H)`：
   - 段落的第 1 行与列表的**第 1 行**落在同一基线、同一左起点 → 视觉上「一行里两段文字水平叠压」；
   - 列表内部的 `StackPanel` 仍然正常堆叠 → 列表第 2、3 行看起来是好的；
   - 一个 chunk 只有一个顶层块时完全看不出问题（大部分 chunk 属于这种）→ 「只有个别行坏」。
5. 已证实部分：§1（`Panel`/`StackPanel` 语义）+ 第 1–3 步的代码位置。
   推论部分：截图里那一处重叠「就是」这条链路（我无法从 PNG 反查运行时可视树；但
   `.recon/overlap/shot1-overlap.png` 的量测与「同基线两段文字」一致：坏行基线间距与相邻行一致、
   行盒高度只比相邻行高 6 px，说明不是两行行盒重叠，而是两个块共用同一行位置）。

### 2.1 关于「一行内水平叠压」的像素量测（`.recon/overlap/shot1-overlap.png`）

- 坏行（y≈334–391）与相邻行的行距一致（≈72–74 px），坏行墨迹带高 58 px、相邻行 52 px；
  **没有出现两行行盒叠在一起的竖向膨胀** → 症状不是 `LineHeight` 造成的行间压字。
- 坏行里 URL 的 `https://` 段与 `APK：` 段起点几乎重合（`https://` 宽 137 px 与量测到的
  合并墨迹段 205–341 完全吻合），其后 `69.8.132.141…` 继续按正常字距排到行尾 →
  同一基线上有**两个独立的绘制起点**，这与「两个兄弟块被 arrange 到同一 origin」一致，
  与「单个 `TextLayout` 内部 run 位置算错」不一致（后者不会让后半段字距恢复正常）。

> 无法证实：`https://` 起点为什么会落在 `APK` 起点的精确像素上（取决于列表行内部 bullet/缩进宽度），
> 截图无法反推列表行的内部布局，故这里只保留「同基线双绘制起点」这一可证实结论。

---

## 3. `TextBlock.LineHeight` 显式设置与字形溢出（已证实，但与本次根因无关）

### 3.1 源码语义

1. `LineHeight` 是**继承的附加属性**，默认 `NaN`：
   `TextBlock.cs`：`LineHeightProperty = AvaloniaProperty.RegisterAttached<TextBlock, Control, double>(nameof(LineHeight), double.NaN, validate: IsValidLineHeight, inherits: true)`
   （<https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Controls/TextBlock.cs>）。
2. 该值被直接塞进段落属性：`CreateTextLayout(...)` →
   `new GenericTextParagraphProperties(..., TextWrapping, LineHeight, 0, LetterSpacing)`（同文件）。
3. 行盒高度与基线的计算在 `TextLineImpl.CreateLineMetrics()`：
   <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Base/Media/TextFormatting/TextLineImpl.cs>
   ```csharp
   var naturalHeight = descent - ascent + lineGap;
   var baseline = -ascent + halfLineGap;
   var height = naturalHeight;
   if (!double.IsNaN(lineHeight) && !MathUtilities.IsZero(lineHeight))
   {
       if (lineHeight <= naturalHeight)
       {
           //Clamp to the specified line height
           height = lineHeight;       // 行盒被压到 LineHeight
           baseline = -ascent;        // 基线顶到字形上沿，字形/内嵌控件向下溢出
       }
       else
       {
           // Center the text vertically within the specified line height
           height = lineHeight;
           var extra = lineHeight - (descent - ascent);
           baseline = -ascent + extra / 2;
       }
   }
   ```
   其中 `ascent/descent/lineGap` 是**所有 run 的并集**：`ShapedTextRun` 取 max(ascent)/max(descent)，
   `DrawableTextRun`（= `InlineUIContainer` 的内嵌控件）用 `bottom = Size.Height - Baseline` 撑大 descent
   （同一方法内的 `foreach (var run in _textRuns)` 段）。

**结论（已证实）**：显式 `LineHeight` 一旦 ≤ 字体（含内嵌控件）算出的自然行高，Avalonia **固定行盒、
不扩张行盒**，把基线改成 `-ascent`，字形/控件**向下溢出**到后续行；`LineHeight` > 自然行高时才做垂直居中。
它不会把同一行的多个 run 在水平方向挤到一起。

### 3.2 本机实验数据（同一 headless probe）

```
=== T5: TextBlock LineHeight=22 + 混排 run + InlineUIContainer(Border 60x30) ===
  TextLayout Height=44  lines=2
    line[0] Height=22 Baseline=30  Extent=32
      run ... EmbeddedControlRun len=1 size=60,30 baseline=30
    line[1] Height=22 Baseline=17.6
  embedded Border Bounds=(81,0,60x30)      <-- 行盒 0..22，控件画到 y=30（向下溢出 8px）
=== T6: 同样内容、LineHeight=NaN ===
  TextLayout Height=34.07  lines=1
    line[0] Height=34.07 Baseline=30 Extent=32
```

可读出三点（均已测量，非推测）：
- 行盒是 22（固定），但该行基线被推到 30：该行文字实际画在行盒下半部之外；
- 内嵌控件 `Bounds=(81,0,60x30)` 从行盒顶部开始、**越过 22 的行盒底边 8 px**；
- 同一 `TextBlock` 的下一行基线是 17.6 ≠ 30 → **同一段落内逐行基线不一致**。

这些正是历史上 DSHRemote 里「行高塌陷 / 基线上移 / 链接截断 / 下一行上边缘压住链接」的机理来源：
Arc 把行内链接实现成 `InlineUIContainer(HyperlinkButton)`，`HyperlinkButton` 设了
`Padding(2,2,2,2)`、`MinHeight=24`（`src/Components/Arc.Avalonia.Markdown/Rendering/Inlines/ArcMarkdownDefaultInlineRenderer.cs:274-334`），
其内容 `TextBlock` 又是 `TextWrapping.NoWrap`（同文件 `:256-261`），比 22 的行盒更高且不参与换行，
再叠加 `TextBlock.ClipToBounds` 默认 `true` 而内嵌控件被重新加入 `VisualChildren` 以避免被裁
（`TextBlock.cs` `ArrangeOverride` 内注释 `//Add again to prevent clipping  //Fixes: #17194`）。

> 但这条与本轮截图的**水平叠压**无关：它解释的是竖向溢出/基线跳变/链接不换行。
> 本轮根因是集成层模板（§1–§2），与 `LineHeight` 取值无关。

### 3.3 混排 run 的行高与基线规则（顺带确认）

- 行高按**行内所有 run 的并集**（max ascent / max descent / max lineGap，含内嵌控件），
  不是「首个 run」决定 → `TextLineImpl.CreateLineMetrics()` 的 `foreach (var run in _textRuns)`。
- 基线是**整行统一**的一条：不同 `FontFamily` / 字号 / 内嵌控件都对齐到同一条 `textLine.Baseline`，
  单 run 通过 `BaselineOffset`/`BaselineAlignment` 偏移（`TextLineImpl.GetBaselineOffset`）。
  因此「混排不同字体导致同行水平叠压」在 Avalonia 文本引擎里没有对应机制。

---

## 4. 判定边界：Avalonia 层能解释什么、不能解释什么

| 症状 | Avalonia 12.1.4 能否解释 | 依据 |
|---|---|---|
| 同一基线上两段文字/两个块水平叠压（本轮 7417「最新 APK：+列表首行」） | **不能**由文本引擎解释；**能**由「裸 `Panel` 宿主」这个布局语义解释 —— 但这是集成层模板用错容器，Avalonia 的 `Panel` 行为本身符合文档语义 | §1、§2 |
| 相邻行竖向压字 / 行高塌陷（历史症状） | **能**：显式 `LineHeight` ≤ 自然行高时行盒固定、基线顶到 `-ascent`、字形向下溢出 | §3.1、§3.2 |
| 段落内基线不齐（同一块里某行基线偏高/偏低） | **能**：含高内嵌控件的行自然行高被撑大、被 `LineHeight` 截断后基线 = `-ascent`，与普通行不同 | §3.2 T5 |
| 行内链接不换行 / 被裁切（历史症状） | **能**：`HyperlinkButton` 内容 `TextWrapping.NoWrap` + `MinHeight=24` + `TextBlock.ClipToBounds=true` | §3.2 |
| 链接/文本被「水平挤在一起」但仍然按字距排到行尾 | **不能**：`TextLayout` 的 run 推进是 `currentX += drawable.Size.Width`（`TextLineImpl.Draw`），同一行不会出现两个绘制起点 | §3.1、§2.1 |
| 真机 arm64 坏、x86_64 模拟器好 | **不能**：布局语义与 ABI 无关，`Panel` 行为确定性；差异只能来自两边构建/模板状态或消息结构不同 | §0 表 #7 |

---

## 5. 背景：Android 字体与 `fontScale`（与本次根因无关）

- Arc 的字体 token 直接落到系统默认字体：`ArcPalette.cs:255-256`
  `dictionary[ArcTokens.FontDisplay] = FontFamily.Default; dictionary[ArcTokens.FontBody] = FontFamily.Default;`
  （`src/Foundation/Arc.Avalonia/Theme/ArcPalette.cs`）。
- 真机截图（`.recon/overlap/7417.jpg`、`DSHRemote/docs/evidence/phone-m6-arc-markdown-v122.png`）
  里**键盘按键字母、状态栏、正文全是同一种手写体** → 该机系统字体被 ROM 主题整体替换，
  所以 `FontFamily.Default` 解析到的是手写体；模拟器是 AOSP 默认字体，外观差异巨大。
  这解释了「真机观感与模拟器完全不同」，但**不解释水平叠压**（`Panel` 重叠是确定性的）。— **推论**
- `fontScale`：对 `Avalonia.Android 12.1.4` 的 `Avalonia.Android.dll` 做字符串扫描，
  未发现 `fontScale` / `FontScale` / `scaledDensity` / `ScaledDensity` 任一标识，只有 `DisplayMetrics`
  （用于密度/RenderScaling）。即 Avalonia 只按 density 换算 DIP，**不乘系统字体缩放**。
  — **推论**（字符串扫描是强证据但非完整证明；如需定论应反编译 `AvaloniaView`/`AndroidPlatform` 的密度读取路径）。
- 代码托管点（非 CJK 字体）如 `ArcMarkdownStyle.MonoFont` 的
  `"Cascadia Mono, Consolas, DejaVu Sans Mono, monospace"`（`ArcMarkdownStyle.cs:15`）在 Android 上一个都不存在，
  会整体回退到系统默认字体 → 代码/行内代码在真机上也是手写体。— **推论**

> **无法证实**：真机那次渲染究竟用的是 ROM 系统字体还是 DSHRemote 曾内置的 Noto 子集
> （当前 `DSHRemote/app/src` 里既没有字体资源，也没有 `ArcMarkdownStyle` 的显式设置代码），
> 因此这里只保留「真机显示的是系统替换字体」这一可从截图（键盘）直接观察到的结论。

---

## 6. 复现与验证

```powershell
# Panel vs StackPanel 语义（headless，无需真机）
cd .recon\PanelProbe
dotnet run -c Debug
# 产物：.recon/overlap/probe_panel.png / probe_stackpanel.png
#       .recon/overlap/probe_panel_host.png / probe_stackpanel_host.png
#       .recon/overlap/probe_inline_ui.png / probe_inline_ui_natural.png
```

Avalonia 源码引用一律使用 tag `12.1.4`：
`https://github.com/AvaloniaUI/Avalonia/tree/12.1.4`。

---

## 7. 引用清单

| 用途 | 位置 |
|---|---|
| `Panel` 无布局覆写 | `src/Avalonia.Controls/Panel.cs` <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Controls/Panel.cs> |
| 默认 measure/arrange（所有子控件同一 `Rect(finalSize)`） | `src/Avalonia.Base/Layout/Layoutable.cs` <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Base/Layout/Layoutable.cs> |
| `LineHeight` 属性定义、传入段落属性、内嵌控件 arrange | `src/Avalonia.Controls/TextBlock.cs` <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Controls/TextBlock.cs> |
| 行盒/基线夹取公式、run 并集 | `src/Avalonia.Base/Media/TextFormatting/TextLineImpl.cs` <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Base/Media/TextFormatting/TextLineImpl.cs> |
| `InlineUIContainer` 测量（用整块宽度、`+∞` 高度） | `src/Avalonia.Controls/Documents/InlineUIContainer.cs` <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Controls/Documents/InlineUIContainer.cs> |
| Fluent `HyperlinkButton` 模板（`Padding=ButtonPadding`、`VerticalAlignment=Center`、无 MinHeight） | `src/Avalonia.Themes.Fluent/Controls/HyperlinkButton.xaml` <https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Avalonia.Themes.Fluent/Controls/HyperlinkButton.xaml> |
| Arc 上游模板要求堆叠宿主 | `src/Components/Arc.Avalonia.Markdown/Themes/ArcMarkdownView.axaml:22` |
| Arc 往宿主加多个块 | `src/Components/Arc.Avalonia.Markdown/Controls/ArcMarkdownView.cs:499,529,597` |
| Arc 行内链接 = `InlineUIContainer(HyperlinkButton)` | `src/Components/Arc.Avalonia.Markdown/Rendering/Inlines/ArcMarkdownDefaultInlineRenderer.cs:274-334` |
| Arc 字体 token = `FontFamily.Default` | `src/Foundation/Arc.Avalonia/Theme/ArcPalette.cs:255-256` |
| DSHRemote 空行切块 + 每块一个 view | `D:\Code\VibeCoding\DSHRemote\app\src\DshRemote.App\Controls\MarkdownBodyView.cs:110-130,210-248` |
| DSHRemote 修复后模板（`StackPanel`） | `D:\Code\VibeCoding\DSHRemote\app\src\DshRemote.App\Theme\AppStyles.axaml:17-29` |
