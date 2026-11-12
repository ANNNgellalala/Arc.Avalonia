# Arc Markdown 真机渲染取证（Arc 侧）

> 任务：`task-1`。范围：**只读**分析工作区仓库 `D:\Code\VibeCoding\Arc.Avalonia`（HEAD `32f2d8d`，Avalonia 12.1.4 / net10.0），不改 `src`。为定位 DSHRemote 真机（arm64）会话 Markdown 的「文字叠压 / 行内链接截断 / 基线不齐」，逐文件核对 `Arc.Avalonia.Markdown` 的控件树、间距来源、高度测量归属、行内链接实现与 LineHeight 来源。跨仓库证据（DSHRemote、`D:\Code\VibeCoding\DSHRemote`）仅作旁证引用，详细集成分析见同目录 [`dsh-remote-integration.md`](dsh-remote-integration.md)。
>
> 证据等级标注：**[已证实]** = 代码/文档/截图直接可核对；**[推论]** = 多条已证实事实推出的唯一合理解释，但未做运行时验证；**[待验证]** = 需要真机或 headless 实验才能定性。

---

## 0. 摘要（先给判定）

**用户问题的答案：本次真机「同一行文字水平叠压」的直接根因不是 Arc 的测量/布局代码，而是宿主重新模板化把 `PART_Host` 从 `StackPanel` 换成了裸 `Panel`；但 Arc 对这个坑负有不可推卸的设计责任（隐式模板契约 + 无官方嵌入模式）。另有 2 处真实的库级设计缺陷（行内链接、绝对 LineHeight 令牌），它们解释「链接截断 / 基线不齐 / 行高突变」，与本次截图叠压无直接因果。**

| # | 结论 | 分类 | 严重度 | 状态 |
|---|---|---|---|---|
| C1 | 本次叠压根因：DSHRemote `AppStyles.axaml:17-26` 把 `PART_Host` 改为裸 `Panel`（层叠容器），Arc 的多个顶层块全部叠在 (0,0) | **集成配合**（宿主侧） | 决定性 | **[已证实]** 代码 + 截图 + 宿主注释自证 |
| C2 | `[TemplatePart("PART_Host", typeof(Panel))]` 把契约声明成基类 `Panel`，而代码语义要求「堆叠面板」；Arc 从未在任何文档写明这条契约 | **库级设计缺陷**（契约） | 中高 | **[已证实]** |
| C3 | 无官方「无滚动 / 嵌入文档流」模式，`ArcMarkdownDocumentView` 仍包一层带 `ScrollViewer` 的 `ArcMarkdownView`，宿主想嵌进消息列表只能重模板 | **库级 API 缺口** | 中 | **[已证实]** |
| C4 | 行内链接 = `InlineUIContainer(HyperlinkButton)`：`MinHeight=24`、`Padding=2`、内容 `TextWrapping.NoWrap`、字号 14 与正文 16 不一致、无 Arc 主题 | **库级设计缺陷**（链接） | 高（库级） | **[已证实]** 代码；真机症状部分有截图 |
| C5 | 绝对 `LineHeight`（22/25/20/31…）与 FontSize 解耦；但 **Markdown 渲染出的 TextBlock 并不套 `arc-body` 类**，Markdown 行高来自字体度量 —— DSHRemote 说的「Markdown 行高塌陷」不能归因于该令牌。另：`TextBlock.arc-*` 是**精确类型**选择器，宿主开文本选择后 Markdown 段落是 `SelectableTextBlock`，这些排版类（以及 `arc-body`）根本打不到它 | **库级风险**（令牌/样式作用域）／对本次无贡献 | 中 | **[已证实]**（代码）；真机触发条件 **[待验证]** |
| C6 | 行内代码用 `Run.Background` 实现底色，Avalonia 背景矩形取自 `GlyphRun.Bounds`（非行盒），Android 混排字体下会跨行遮字 | 库级实现方式 + Avalonia 运行时 | 中 | **[推论]**，s2 截图有迹象 |

关键负面结论（避免误归因）：
- **Markdown 段落 TextBlock 没有套 `arc-body`**（Markdown 组件内 `Classes` 出现 0 次，`CreateText` 只设 FontSize/FontFamily/FontWeight，从不设 LineHeight），所以 `arc-body` 的 `LineHeight=22` 对 Markdown 正文不生效（见 §6）。
- **Arc 默认模板下不存在「子控件高度未计入父布局」的路径**：块高度全部由块自身 Measure 得出，`ArcMarkdownView` 只负责 `PART_Host.Children.Add`（见 §2）。
- 模拟器「正常」不等于 Arc 在真机有 bug：模拟器镜像缺 CJK 字体（`docs/25-mobile-usage-guidance.zh.md:143-156`，截图 `_scratch/emu-overlap-plain.png` 正文中文为豆腐块），与真机不是同一渲染条件（见 §8）。

---

## 1. 本次叠压的独立取证（C1）

### 1.1 两处模板的直接对照

Arc 自带模板（**唯一官方模板**）：

`src/Components/Arc.Avalonia.Markdown/Themes/ArcMarkdownView.axaml:14-25`
```xml
<Setter Property="Template">
  <ControlTemplate>
    <ScrollViewer x:Name="PART_Scroll" ...>
      <StackPanel x:Name="PART_Host" Spacing="0" Margin="0" />
    </ScrollViewer>
  </ControlTemplate>
</Setter>
```

DSHRemote 的覆盖（**把宿主换成层叠面板**）：

`D:\Code\VibeCoding\DSHRemote\app\src\DshRemote.App\Theme\AppStyles.axaml:12-26`
```xml
<!--
  The Arc Markdown view ships as a scroll host (its own ScrollViewer). Inside the transcript it must
  grow with the outer scroll viewer instead of scrolling itself, so re-template it as a simple panel;
  the view code only requires the PART_Host panel (PART_Scroll is optional).
-->
<ControlTheme x:Key="{x:Type md:ArcMarkdownView}" TargetType="md:ArcMarkdownView">
  ...
  <ControlTemplate>
    <Panel x:Name="PART_Host" Background="{TemplateBinding Background}" />
  </ControlTemplate>
```

注释最后一句「the view code only requires the PART_Host panel」正是误判来源：Arc 的 `[TemplatePart("PART_Host", typeof(Panel))]`（`Controls/ArcMarkdownView.cs:23`）字面上只承诺「是个 Panel」，宿主据此把 `StackPanel` 降级成 `Panel`。

### 1.2 Avalonia `Panel` 的语义是层叠，不是堆叠

`D:\.nuget\packages\avalonia\12.1.4\lib\net10.0\Avalonia.Controls.xml:15472-15480`（`T:Avalonia.Controls.Panel` 的 `<remarks>`）：

> Controls can be added to a Panel by adding them to its Children collection. **All children are layed out to fill the panel.**

即所有子控件被安排进同一个矩形（同原点），是「层叠容器」。本仓库内的实际用法也证明这一点：DSHRemote 自己就用 `Panel` 做图片/占位文字/加载文字的叠层（`Views/ChatView.axaml:24-36` 三个子控件叠在一起）。

### 1.3 Arc 侧写入路径不提供任何坐标

`src/Components/Arc.Avalonia.Markdown/Controls/ArcMarkdownView.cs:490-531`
```csharp
_host.Children.Clear();
...
foreach (var node in document.Markdown)
{
    var control = context.RenderBlock(node);
    AddBlockControl(node, control);   // -> _host!.Children.Add(control);       // :529
    ...
}
```
流式尾部同样只是 `_host.Children.Add(control)`（`:597`）。**Arc 从不设置 `Canvas.Top`/`Margin` 之外的位置信息，纵向堆叠完全由 `PART_Host` 的容器类型决定。**

### 1.4 因果链（与截图一一对应）

```
Markdown 文档 → RenderDocument：每个顶层块一个 Control → PART_Host.Children.Add
                                                     │
              PART_Host = StackPanel（Arc 模板） ─────┘→ 子块纵向排列（正常）
              PART_Host = Panel（DSHRemote 模板） ────┘→ 所有子块同一矩形 (0,0)
                                                          → 同 y、不同 x 的文字互相叠压
```

真机截图 `_dev/s1-ctx.png` / `_dev/s1-overlap-zoom.png`：段落 `**最新 APK**：` 与紧随其后的列表首行 `<https://69.8.132.141:9443/__gateway/download>` 被画在同一基线上，正是「同一 ArcMarkdownView 内 paragraph + list 两个顶层块」被 `Panel` 叠放的结果。DSHRemote 注释里「表格行盖住代码块」（`Controls/MarkdownBodyView.cs:13-16`）同理（多个顶层块落在同一 view 内时）。

为什么「按空行拆块」的 workaround 只能缓解：它把「N 块文档」拆成「1 块文档」，但只要 Markdown 里出现「段落紧跟列表/标题紧跟段落」这类**空行内多顶层块**的结构（如上例），同一个 `ArcMarkdownView` 里仍有 ≥2 个块 → 叠压复发；而且拆分后每个 view 都带 `ClipToBounds=true`（`MarkdownBodyView.cs:43,52,136`），溢出会从「叠压」变成「裁切」。

> 该结论与 Lead 的独立判断一致；本报告另行确认了 Arc 侧的契约责任（C2/C3），见 §3、§4。

---

## 2. 真实控件树、间距来源与高度测量归属

默认模板下的真实控件树（`Span`/`Run` 省略）：

```
ArcMarkdownView : TemplatedControl                     ArcMarkdownView.cs:24
└─ ScrollViewer PART_Scroll                            ArcMarkdownView.axaml:16
   └─ StackPanel PART_Host  Spacing=0 Margin=0          ArcMarkdownView.axaml:22
      ├─ (每顶层块) ── ParagraphRenderer → TextBlock/SelectableTextBlock   BlockRenderers.cs:26-33
      ├─ HeadingRenderer → TextBlock（DisplayFont + 字阶）                  :55-73
      ├─ QuoteRenderer  → Border(3px 左轨, Padding 左 16) → StackPanel(4)   :114-140
      ├─ ListRenderer   → Grid(Margin 底 12, Auto 行)
      │                    ├─ Col0: marker TextBlock / task CheckBox
      │                    └─ Col1: StackPanel(Spacing 4) ← item 子块        :151-221
      ├─ TableRenderer  → ScrollViewer(仅横向)
      │                    └─ ArcMarkdownTableFit → Border(ClipToBounds=true)
      │                       └─ Grid(Auto 行列) → 单元格 Border(Padding 12,8) :23-153
      ├─ CodeBlockRenderer → ArcMarkdownCodeBlock : TemplatedControl
      │                    └─ Border(模板 Padding 16) → Panel PART_Host
      │                       └─ StackPanel(0)[ header Grid + ScrollViewer(横向) + 折叠按钮 ]  CodeBlock.cs:191-304
      ├─ ThematicBreakRenderer → Border Height=1 Margin 12/12               BlockRenderers.cs:97-106
      ├─ 行内链接/图片：InlineUIContainer(Control) 嵌在段落 TextBlock 里      InlineRenderer.cs:334,388,392
      └─ 流式尾部：同样是 PART_Host.Children.Add                            ArcMarkdownView.cs:597
```

**间距来源**（全部是块自身 `Margin`，`PART_Host.Spacing=0` 不参与）：

| 间距 | 值 | 来源 |
|---|---|---|
| 段落块底 12 | `ArcTokens.Space3` | `ArcMarkdownStyle.cs:89`；`BlockRenderers.cs:30` |
| 标题上 24 / 24×0.6 | `Space6` | `ArcMarkdownStyle.cs:92`；`BlockRenderers.cs:65-69` |
| 标题底 8 | `Space2` | `ArcMarkdownStyle.cs:95` |
| 列表缩进 20 / 项间距 4 | `Space5` / `Space1` | `ArcMarkdownStyle.cs:98,101` |
| 引用左轨 3px + 左缩进 16 | `QuoteIndent=Space4` | `BlockRenderers.cs:132-139` |
| 代码块底 12 | `BlockSpacing` | `CodeRenderers.cs:37` |
| 表格底 12 | `BlockSpacing` | `TableRenderer.cs:96` |

**高度测量归属**（关键：没有任何父级写死块高度）：

| 控件 | 高度由谁决定 | 证据 |
|---|---|---|
| 段落/标题/表格单元格文本 | `TextBlock.MeasureOverride` → `TextLayout.Height`（自动行高，见 §6） | Avalonia `TextBlock.cs`（12.1.4）；`ArcMarkdownDefaults.cs:64-74` 未设 LineHeight |
| 列表 | `Grid` Auto 行 = 该行 marker 与内容 `StackPanel` 的最大 DesiredSize | `BlockRenderers.cs:174,184-213` |
| 表格 | 单元格 `Border`/`Grid` Auto 行；`ArcMarkdownTableFit` 只重写宽高转发，不截断高度 | `TableRenderer.cs:229-254` |
| 代码块 | 模板 `Border` Padding 16 + 内部 `StackPanel`（header + 横向 ScrollViewer + 可选折叠按钮） | `ArcMarkdownCodeBlock.cs:263-304`；`ArcMarkdownCodeBlock.axaml:10-15` |
| 隔离元素 | 唯一固定高 `Height=1` | `BlockRenderers.cs:102` |
| **ArcMarkdownView** | **无 `MeasureOverride`/`ArrangeOverride`**（组件内 0 处），DesiredSize 完全来自模板根 `ScrollViewer` | 全组件 grep 结果；`ArcMarkdownView.cs:281-299` 仅取模板部件 |

结论：**在默认模板下，Arc 没有「子控件高度丢失」的代码路径**；块高度逐级由自身测量上报。反过来说，`ArcMarkdownView` 的高度语义 = 「ScrollViewer 的 DesiredSize」，这既带来内层滚动，也把「高度是否与内容一致」交给 ScrollViewer/内容协商——这是后面 §4 讨论「嵌入模式缺失」的技术根。

> 备注：表格与代码块各自内嵌一个独立 `ScrollViewer`（`TableRenderer.cs:88-100`、`ArcMarkdownCodeBlock.cs:265-274`），嵌套滚动容器在手机上是已知易错点；但它们只影响手势，不改变高度。唯一显式 `ClipToBounds` 在表格圆角框（`TableRenderer.cs:84`），用于裁圆角，不参与高度。

---

## 3. `PART_Host` 契约：Arc 的库级设计责任（C2）

### 3.1 已证实事实

1. **声明类型过宽**：`src/Components/Arc.Avalonia.Markdown/Controls/ArcMarkdownView.cs:22-23`
   ```csharp
   [TemplatePart("PART_Scroll", typeof(ScrollViewer))]
   [TemplatePart("PART_Host", typeof(Panel))]
   public partial class ArcMarkdownView : TemplatedControl, IDisposable
   ```
   声明为 `Panel`，代码实际要求「会纵向堆叠」的 `Panel` 子类（模板给的是 `StackPanel`）。`Panel` 本身恰恰是最**不**该被塞多子块的实现（§1.2）。
2. **契约没有任何文字记录**：
   - 模板注释只说「the renderer builds Arc controls into PART_Host」（`ArcMarkdownView.axaml:6`），未说必须堆叠；
   - 类 XML doc（`ArcMarkdownView.cs:17-21`）只描述功能；
   - `docs/21-markdown-components.zh.md` §7「已知限制」（`:114-121`）只提「块级虚拟化不在本包范围」，**无任何重模板说明**；
   - `docs/25-mobile-usage-guidance.zh.md` 全篇（已逐行读）**无**「重新模板化 ArcMarkdownView」的条目；
   - 全仓 grep `PART_Host`：Markdown 组件只在 `ArcMarkdownView.cs:23,290` 与模板出现，README/文档均无契约句。
3. **契约门禁不覆盖容器语义**：`docs/18-control-contract-spec.zh.md:15-19` 只要求「有 OnApplyTemplate ⇒ 声明 ≥1 个 TemplatePart」「名字 PART_*」「查找与声明一致」「Themes 中 x:Name 与声明一一对应」；`tools/check-contracts.ps1:4-17`（R10）规则 a/b/e 同样只管名字/存在性。**没有任何检查「模板元素类型是否满足代码的布局假设」**。
4. **多子块堆叠需求是 Markdown 独有的**：grep `_host.Children.Add` 全仓，只有 `ArcMarkdownView`（`:529,597`）会向 `PART_Host` 添加任意数量的子块；其余 PART_Host 控件最多 add 1 个子控件/容器（如 `ArcChatBubble.cs:282-377`、`ArcMarkdownCodeBlock.cs:304`、`ArcTimeline.cs:183-196` 二选一）。所以「PART_Host=裸 Panel」在其他组件里恰好不会炸，问题被掩盖到 Markdown 才暴露。

### 3.2 判定

- **这是 Arc 的契约设计缺陷，不是「宿主不会用」**：声明 `typeof(Panel)` 是公开 API 的一部分，`[TemplatePart]` 的 `typeof` 语义就是「部件在这个控件树里的角色类型」；把一个「必须堆叠」的宿主声明成最泛化的层叠基类，等于主动邀请错误的重模板。**[已证实]**
- 严重度 **中高**：任何想「去掉内层滚动、嵌入外层列表」的宿主都会做同样的重模板动作（这正是 C3 指出的真实需求），一次误改就是全篇重叠，且现象看起来像「渲染器坏了」。

---

## 4. 嵌入 / 无滚动模式缺失（C3）

- `ArcMarkdownOptions` 只有 9 个配置项（`Rendering/ArcMarkdownPipeline.cs:15-46`：Pipeline/ImageLoader/LinkHandler/Clipboard/Localizer/Limits/UrlPolicy/InteractionEnabled/EnableTextSelection），**没有 ScrollEnabled/Embedded/PART 暴露之类的开关**。
- `Controls/ArcMarkdownDocumentView.cs:10-22`：`ContentControl` 内部 `private readonly ArcMarkdownView _inner = new();`，`Content = _inner` —— 它是「已解析文档」的便利包装，**仍然带 ScrollViewer**，无法作为无滚动嵌入体。
- `docs/21` §7（`:114-121`）给出的建议是「超长内容请拆页，或使用 `ArcChatView` 的消息级虚拟化」，即**没有**「嵌入到自定义外层滚动容器」的官方路径。

判定：宿主（DSHRemote）的真实需求合法且常见——消息列表由外层统一滚动，消息体必须「自然增长、不内滚」。Arc 不提供该模式，宿主只能重模板；重模板又踩中 C2 的隐式契约。**这是库级 API 缺口（中）**，也是 C1 的「必要条件」：不修 C2/C3，同类事故会在下一个宿主重演。

---

## 5. 行内链接实现（C4，真实库级缺陷，但不是本次叠压根因）

### 5.1 决定性证据：链接是 `InlineUIContainer(HyperlinkButton)`

`src/Components/Arc.Avalonia.Markdown/Rendering/Inlines/ArcMarkdownDefaultInlineRenderer.cs:247-335`
```csharp
var content = new TextBlock
{
    TextWrapping = TextWrapping.NoWrap,          // :258  链接内容永不换行
    Foreground = style.Accent,
    TextDecorations = TextDecorations.Underline,
};
...
var button = new HyperlinkButton
{
    Content = content,
    // A little vertical padding grows the touch target of an inline link without breaking the line box.
    Padding = new Thickness(2, 2, 2, 2),          // :278
    MinHeight = 24,                               // :279
    Background = null,
    BorderThickness = new Thickness(0),
    CornerRadius = new CornerRadius(0),
    VerticalAlignment = VerticalAlignment.Center, // :283
};
...
target.Add(new InlineUIContainer(button));        // :334
```
同一 renderer 的图片也用 `InlineUIContainer`（`:377-392`），同样风险。

### 5.2 为什么这确实会坏（Avalonia 语义 + 代码细节）

1. **行盒被嵌入控件撑开**：`InlineUIContainer.BuildTextRun` 产出 `EmbeddedControlRun`（Avalonia 12.1.4 `src/Avalonia.Controls/Documents/InlineUIContainer.cs`），而 `TextLineImpl.CreateLineMetrics` 会把所有 `DrawableTextRun` 的 `Baseline`/`Size.Height` 合并进行的 ascent/descent（Avalonia `src/Avalonia.Base/Media/TextFormatting/TextLineImpl.cs`，`CreateLineMetrics` 的 `case DrawableTextRun` 分支）。因此 `MinHeight=24` + 上下各 2px 的按钮会**改变该行行高**，第 277 行注释「without breaking the line box」与实现相反。
2. **基线由控件高度决定**：`TextBlock.ArrangeOverride`（Avalonia `src/Avalonia.Controls/TextBlock.cs`）用 `TextLineImpl.GetBaselineOffset(line, drawable)` 计算 `offsetY`，即嵌入控件按自身基线相对行基线摆放 → 链接行的基线/视觉中心与纯文本行不同 = 同一段内「基线不齐」。
3. **窄宽度必截断**：链接内容 `NoWrap`，`InlineUIContainer` 也不提供断点；外层 `TextBlock` 默认 `ClipToBounds=true`（Avalonia `TextBlock` 静态构造 `ClipToBoundsProperty.OverrideDefaultValue<TextBlock>(true)`）→ 超宽链接被裁而不是换行 = 「行内链接截断」。
4. **字号与正文不一致**：链接内容 `TextBlock` 只设 `Foreground`/`TextDecorations`，不设 `FontSize`/`FontFamily` → 落到全局隐式 `TextBlock` 样式（它正好是精确 `TextBlock`，能命中）：`src/Foundation/Arc.Avalonia/Theme/ArcBaseStyles.axaml:20-25`（FontSize=`ArcTextSm`）+ `Theme/ArcPalette.cs:258`（`TextSm=14`）；而正文 `ArcMarkdownStyle.BodySize`=`TextBase`=16（`ArcMarkdownStyle.cs:30`、`ArcPalette.cs:259`）。⇒ 链接文字 14px 嵌在 16px 正文里，基线偏差固定存在。
5. **没有 Arc 主题接管**：全仓 `*.axaml` grep `HyperlinkButton` **0 匹配**；实际样式来自 Fluent 默认 `HyperlinkButton`（额外模板 Padding/最小尺寸不受控），与 `docs/21:103`「链接…`:focus-visible` 由 ArcButton/Hyperlink 主题接管」的说法不符。
6. **触摸区与可选中的冲突**：`ArcMarkdownTapGuard`（`Controls/ArcMarkdownTapGuard.cs:27-33`）在链接上挂 Tunneling 指针处理器以抑制滑动误点，而 `EnableTextSelection=true` 时段落是 `SelectableTextBlock`（`Rendering/ArcMarkdownRenderContext.cs:104-105`），后者在 `OnPointerPressed` 里 `Pointer.Capture(this); e.Handled = true`（Avalonia `SelectableTextBlock.cs`）→ 两套指针状态机叠加，手势行为依赖具体设备（本次不展开，列为 **[待验证]**）。

### 5.3 与本次截图的关系（重要）

DSHRemote 已把行内链接预处理成纯文本（`MarkdownBodyView.cs:155-203` 的 `Preprocess`：`[label](url)` → `label（url）`，长 URL 插 ZWSP），真机截图 `_dev/s1-ctx.png` 中的 URL 是**普通 `Run`**，没有 `HyperlinkButton`。⇒ **C4 不是本次截图中水平叠压的根因**；它解释的是另一组症状（直接用 ArcMarkdownView 的宿主 / 未预处理时出现的链接截断、基线不齐、链接行行高突变）。DSHRemote 注释（`MarkdownBodyView.cs:155-160`）对 C4 的现象描述（「different baseline / do not wrap / top edge of next line overlaps the link」）与上述代码分析一致。

---

## 6. LineHeight 令牌与 Markdown 行高（C5）：给出 file:line 结论

### 6.1 令牌侧：绝对值 + 与字号解耦（[已证实]）

`src/Foundation/Arc.Avalonia/Theme/ArcBaseStyles.axaml:71-135`（节选）：

| 类 | FontSize 令牌 | 实际 px | LineHeight | 比值 |
|---|---|---|---|---|
| `arc-display` | `ArcText5xl` | 72 | 79 | 1.097 |
| `arc-title` | `ArcText4xl` | 52 | 57 | 1.096 |
| `arc-title-sm` | `ArcText3xl` | 36 | 40 | 1.111 |
| `arc-subtitle` | `ArcText2xl` | 28 | 31 | 1.107 |
| `arc-heading` | `ArcTextXl` | 22 | 31 | 1.409 |
| `arc-body-lg` | `ArcTextLg` | 18 | 25 | 1.389 |
| **`arc-body`** | `ArcTextBase` | **16** | **22** | **1.375** |
| `arc-body-sm` | `ArcTextSm` | 14 | 20 | 1.429 |
| `arc-caption` | `ArcTextXs` | 12 | 17 | 1.417 |
| `arc-label` | `ArcTextSm` | 14 | 20 | 1.429 |

字号/字距/行高 token 见 `Theme/ArcPalette.cs:255-269`；`ArcTokens.cs:84-85` 定义了 `ArcLeadingDisplay/ArcLeadingBody`（1.1 / 1.4）——**全仓 grep 除 `ArcPalette.cs:268-269` 的赋值外 0 处使用**：行高是硬编码 px，不是「leading × fontSize」。`arc-body` 的 22px 对 16px 正文只有 1.375，而 CJK 系统字体（如 Noto Sans CJK，ascent≈1.16em/descent≈0.288em）自然行高约 1.4–1.5em = **22.4–24px**，即 22 < 自然行高。

### 6.2 Markdown 侧：**不套 `arc-body`，不设 LineHeight**（[已证实]）

- `Arc.Avalonia.Markdown` 全目录 grep `Classes` / `arc-`：**0 匹配**（渲染器从不给 TextBlock 加样式类）。
- `Rendering/Defaults/ArcMarkdownDefaults.cs:64-74`：
  ```csharp
  internal static TextBlock CreateText(ArcMarkdownRenderContext context, bool wrap = true)
  {
      var text = context.CreateTextBlock();
      text.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
      text.Foreground = style.Foreground;
      text.FontSize = style.BodySize;      // 16
      text.FontFamily = style.BodyFont;
      text.FontWeight = FontWeight.Normal;
      return text;                          // 无 LineHeight、无 Classes
  }
  ```
- 段落（`Rendering/Blocks/ArcMarkdownBlockRenderers.cs:26-33`）、标题（`:55-73`）都只是 `CreateText` + 改字号/边距。
- 全组件 `*.cs` grep `LineHeight`：**0 匹配**（`ArcMarkdownCodeBlock.cs:229-250` 的 code/gutter TextBlock 同样不设行高）。

⇒ **Markdown 正文行高来自 Avalonia 自动行高**：`TextLineImpl.CreateLineMetrics` 用字体度量算 `naturalHeight = descent - ascent + lineGap`，与字体/字号强相关。**DSHRemote 所说「Arc 链接内联样式造成的行高塌陷」只能来自 C4 的链接控件或 C1 的块叠放，不能来自 `arc-body` 的 22px。**

### 6.3 但绝对 LineHeight 仍是真实风险（机制已证实，触发条件待验证）

Avalonia 的 `TextBlock.LineHeight` 是**附加属性且 `inherits: true`**：
```csharp
public static readonly AttachedProperty<double> LineHeightProperty =
    AvaloniaProperty.RegisterAttached<TextBlock, Control, double>(nameof(LineHeight), double.NaN, ..., inherits: true);
```
（Avalonia 12.1.4 `src/Avalonia.Controls/TextBlock.cs`；`SelectableTextBlock.CreateTextLayout` 也透传 `LineHeight`。）

因此**只要宿主在任一祖先控件上设 `LineHeight`**（或将来 Arc 给 Markdown 加 `arc-body` 类 / 主题里加 `TextBlock` 行高 setter），所有 Markdown TextBlock 都会继承绝对行高。而 Avalonia 对「显式 LineHeight < 自然行高」的处理是**压缩行盒、基线贴 ascent**（`TextLineImpl.CreateLineMetrics`：`if (lineHeight <= naturalHeight) { height = lineHeight; baseline = -ascent; }`）→ 字形高度超出 22px 行盒的部分会侵入相邻行 = **行间叠压**。这就是「CJK/系统替换字体 ⇒ 自然行高变大 ⇒ 越过 22 阈值」的真机触发机制。

现实检查（本次 DSHRemote）：
- DSHRemote 当前**没有**给 Markdown 设 LineHeight（跨仓库 grep `LineHeight` 只有 `WrapPanel.LineSpacing`，无 `TextBlock.LineHeight`）→ Markdown 正文不触发。
- **一个容易踩的样式作用域事实**：Avalonia 的裸类型选择器是 `OfType`（只匹配精确类型；派生类型必须写 `:is(...)`——`Selectors.OfType` 文档「Returns a selector which matches a type」vs `Selectors.Is`「matches a type **or a derived type**」，`Avalonia.Base.xml`；实现见 `TypeNameAndClassSelector` 的 `IsConcreteType` 精确比较）。因此 `TextBlock.arc-body` **不会**命中 `SelectableTextBlock`（Arc 仓库里也没有任何 `SelectableTextBlock` 选择器，grep 0 处）。DSHRemote 的流式纯文本 `_plain` 正是 `SelectableTextBlock` 且只加了 `arc-body` 类（`D:\Code\VibeCoding\DSHRemote\app\src\DshRemote.App\Controls\MarkdownBodyView.cs:53-54`）→ 它预期的 22px 行高**实际没有生效**（这属于宿主侧问题，需其自行确认；他们的 `timeline-text` 样式就写成了 `SelectableTextBlock.timeline-text`，说明这条语义他们只对了一半）。**[推论，需 30 秒验证]**：`TextBlock.FontSizeProperty` 默认值 12，若宿主未另设字号，纯文本模式可能比预期小。
- 于是 C5 的现实触发面变成：**任何把 `arc-body` / `arc-body-sm` / `arc-label` / `arc-caption` 类加在精确 `TextBlock` 上的地方**（Arc 自身 src 里 9 处，如 `ArcTimeline.cs:167` `arc-label`、`ArcAnnouncementBar.cs:654` `arc-caption`；Gallery/docs 页面更多），以及**任何在祖先上设 `LineHeight` 的宿主**。这些 `TextBlock` 在真机 CJK 下会走 §6.3 的 clamp。

判定：C5 属**库级令牌/样式作用域风险（中）**：行高写死 px、与字号解耦、`arc-body` 以 22 服务 16px 正文、leading token 定义后未使用；同时排版类只能作用于精确 `TextBlock`，派生文本控件（如选择模式下的 `SelectableTextBlock`）无法复用 —— 这会让宿主「要么用不了 Arc 排版、要么自己复制一份 px」。对本次截图叠压无贡献。

---

## 7. 其它 inline 级风险（C6，[推论]）

- 行内代码：`Rendering/Inlines/ArcMarkdownDefaultInlineRenderer.cs:143-150`
  ```csharp
  target.Add(new Run(content) { FontFamily = context.Style.MonoFont, Background = context.Style.InlineCodeBackground });
  ```
  `==marked==` 同样用 `Span.Background`（`:169-171`）。
- Avalonia 绘制该背景时用的是 **`GlyphRun.Bounds`**（`src/Avalonia.Base/Media/TextFormatting/ShapedTextRun.cs` 的 `Draw`：`drawingContext.DrawRectangle(Properties.BackgroundBrush, null, GlyphRun.Bounds);`），不是行盒。`Size => GlyphRun.Bounds.Size`、`Baseline => -TextMetrics.Ascent + LineGap*0.5` 也来自 run 自身字体度量。
- 真机证据迹象：`_dev/s2-clip.png` 中行内代码 `label（url）`（跨行拆分的后半段）的灰色底块高度约为 2 个行高，并压住了下一行文字（「消除…行高塌陷」下方的第三行被遮半截）。
- 判定：Arc 选了一个依赖 `GlyphRun.Bounds` 精确性的实现方式；在 Android 混排 fallback 字体下该 bounds 一旦偏大就会跨行遮字。**库级实现方式风险（中）**；是否为 Avalonia 运行时缺陷 **[待验证]**（需真机 dump `GlyphRun.Bounds`）。

---

## 8. 真机 vs 模拟器的差异（避免错误对照）

- `docs/25-mobile-usage-guidance.zh.md:143-156`（§5.4）已给出结论：真机 PLQ110 中文完全正常，**模拟器镜像缺 CJK 字体**（`FontFamily.Default` 在模拟器上正文混排变豆腐），并明确「不是库缺陷」；§5.1（`:112-118`）记录「标题正常、混排正文豆腐」是字体回退路径问题。
- 现场截图 `D:\Code\VibeCoding\DSHRemote\_scratch\emu-overlap-plain.png`（1080×2400）中正文中文即为豆腐块（标题类 CJK 正常）。
- ⇒ 模拟器上的「正常」是「CJK 文本根本没被真正整形」+ 拉丁字体自然行高更小，**不能作为 Arc 在模拟器无此 bug 的证据**；真机上 CJK 字形更宽、自然行高更大，C1 的「同原点叠放」在视觉上更明显（同一起点、更宽的字更容易水平交叉），C4/C6 的链接截断、行内 code 底块遮字也更容易看到。C5 的 22px clamp **不参与** Markdown 正文（§6.2），只影响带 `arc-*` 类的精确 `TextBlock`。

---

## 9. 问题清单（含分类 / 严重度 / 可复现条件）

| # | 问题 | 分类 | 严重度 | 可复现条件 | 状态 |
|---|---|---|---|---|---|
| 1 | 宿主把 `PART_Host` 换成裸 `Panel` → 多块同原点叠压 | **集成配合**（决定性根因） | 致命（本次） | 任一场合：一个 ArcMarkdownView 内 ≥2 个顶层块（段落+列表/标题+段落/表格+代码）；改回 StackPanel 即消失 | [已证实] |
| 2 | `[TemplatePart("PART_Host", typeof(Panel))]` 与实际「必须堆叠」语义不符，且无文档/无 XML doc 契约 | 库级设计（契约） | 中高 | 宿主按声明类型重模板为 `Panel`/`Grid` 即触发 | [已证实] |
| 3 | 无「无滚动/嵌入文档流」模式（`ArcMarkdownDocumentView` 仍带 ScrollViewer） | 库级 API 缺口 | 中 | 消息列表等需要外层统一滚动的场景 | [已证实] |
| 4 | 行内链接 = InlineUIContainer(HyperlinkButton)：MinHeight24/Padding2/NoWrap/14px/无 Arc 主题 | 库级设计（链接） | 高（对库使用者） | 任何未预处理链接的 ArcMarkdownView；窄宽度必截断、含链接的段落行高/基线必变 | [已证实]（代码）；真机像素表现部分有截图 |
| 5 | 绝对 LineHeight 令牌（22@16 等）与字号解耦；leading token 未使用；`LineHeight` 是 inherits 属性；`TextBlock.arc-*` 只命中精确类型（`SelectableTextBlock` 用不了） | 库级令牌/样式风险 | 中 | 宿主/主题在祖先设 `LineHeight`；或 `arc-*` 类加在精确 `TextBlock` 上且字形来自 CJK 系统字体（Arc src 内 9 处） | 机制[已证实]；真机逐行[待验证] |
| 6 | 行内代码/高亮用 `Run.Background`，背景取 `GlyphRun.Bounds` | 库级实现风险 + Avalonia 运行时 | 中 | Android 混排 fallback 字体 + 行内 code 跨行 | [推论]（有截图迹象） |
| 7 | 契约门禁（R10）只校验部件名/存在性，不校验容器语义 | 工程/测试缺口 | 中 | — | [已证实] |
| 8 | Markdown 无自动化布局断言（`tests/.../Program.cs:961+` 只断言渲染器产出的控件类型/文本，不测 `ArcMarkdownView` 模板下的实际排列） | 测试缺口 | 中 | 重模板类回归不会被测试抓到 | [已证实] |
| 9 | 表格/代码块内嵌横向 ScrollViewer + 表格 `ClipToBounds` | 设计权衡 | 低 | 窄屏横向手势/圆角裁切 | [已证实]（代码） |
| 10 | `SelectableTextBlock` 与链接 TapGuard 两套指针状态机叠加 | Avalonia 运行时/交互 | 低 | `EnableTextSelection=true` + 链接 + 滑动 | [待验证] |

---

## 10. 建议

### 10.1 Arc 侧（按优先级）

1. **P0 契约显式化（修 C2）**
   - 把声明改为 `[TemplatePart("PART_Host", typeof(StackPanel))]`（或新增公开的 `ArcMarkdownHost : StackPanel` 类型并声明它），让 `typeof` 真正表达「会堆叠」；
   - 类 XML doc 增补一句：「`PART_Host` 必须是一个纵向堆叠面板（`StackPanel`）；`RenderDocument` 只向其中 `Children.Add`，不写坐标」；
   - `docs/18-control-contract-spec.zh.md` 增加「容器语义」列/条目；`tools/check-contracts.ps1` 可加一条（可选）静态检查：`[TemplatePart(...,typeof(Panel))]` 且模板元素为 `StackPanel` 时提示声明过宽。
2. **P0/P1 提供官方嵌入模式（修 C3）**：`ArcMarkdownView.ScrollEnabled`（或 `Mode=Document/Embedded`）为 false 时模板根直接是 `StackPanel PART_Host`，`PART_Scroll` 可空；或公开 `ArcMarkdownContent`（无滚动、只渲染块）。这样宿主不必重模板，C1 类事故自然消失。
3. **P1 重做行内链接（修 C4）**：优先用真正的 `Inline`（自定义 `Run` + `Foreground/TextDecorations` + 命中测试）而不是 `InlineUIContainer(HyperlinkButton)`；若必须用控件，则去掉 `MinHeight`/`Padding`、显式设置 `FontSize=BodySize`、允许换行（或按段拆分链接）并给 `HyperlinkButton` 一个 Arc ControlTheme。图片的 `InlineUIContainer` 同理（`:377-392`）。
4. **P1 行高策略（修 C5）**：Markdown TextBlock 显式设 `LineHeight = max(自然行高, fontSize × ArcLeadingBody)`；把 `ArcLeadingBody=1.4` 真正用起来；`arc-body` 22px 需按 16px 正文 + CJK 度量重新校准（至少 ≥ 1.45em，或干脆留 Auto）；排版类的选择器改为 `:is(TextBlock)` 或同时提供 `SelectableTextBlock` 变体，让派生文本控件复用同一套字号/行高，而不是宿主自己复制 px。
5. **P2 行内代码底色**：改为 `Border`/`Decorator` 或按行盒高度绘制，避免依赖 `GlyphRun.Bounds`（C6）。
6. **P2 测试**：headless 里用「默认 ArcTheme 模板」实例化 `ArcMarkdownView`，断言多顶层块时相邻块 `Bounds.Y` 递增；再加一条「自定义主题把 PART_Host 换成 Panel 时行为应报错/警告」的守护测试（可选）。

### 10.2 宿主侧（DSHRemote，一句话结论，细节见 `dsh-remote-integration.md`）

把 `AppStyles.axaml:23` 的 `<Panel x:Name="PART_Host" .../>` 换回 `<StackPanel x:Name="PART_Host" Spacing="0" Background="{TemplateBinding Background}"/>`（Arc 默认模板一致，`StackPanel` 继承 `Panel`，`PART_Scroll` 本来就是可选的），即可消除 C1；C4/C5 的 workaround（链接纯文本、ZWSP）可保留但应在 Arc 修复后回收。

### 10.3 建议的验证清单（供后续）**[待验证]**

1. **C1 对照实验（最关键，5 分钟）**：真机上仅把 `PART_Host` 改回 `StackPanel`，同一会话/同一条消息截图对比；预期两处叠压消失、表格不再盖代码块。
2. **C5 实验**：真机渲染一张全 CJK 长段落（不经过 ArcMarkdown，纯 `TextBlock.Classes="arc-body"`），量行距是否为 22px、字形是否互相侵入；再设 `LineHeight=Auto` 对比。
3. **C4 实验**：真机直接用 `ArcMarkdownView` 渲染含长 URL 链接的段落（不做 Preprocess），测量链接行行高、链接是否被裁、基线偏移量。
4. **C6 实验**：真机 dump 行内 code `Run` 的 `GlyphRun.Bounds`（可在 headless/Android 打日志）对比行盒高度。
5. **模拟器对照**：装 Noto CJK（或用真机镜像）后再复跑 1–4，确认模拟器「正常」只是缺字体。

---

## 11. 方法与依据

- **只读**：未修改 `src`，未运行构建/测试；所有结论来自源码、仓库文档、NuGet 内 Avalonia 12.1.4 官方 XML 文档与 GitHub tag `12.1.4` 源码，以及 DSHRemote 仓库的只读取证（截图/代码/注释）。
- Arc 侧文件：`src/Components/Arc.Avalonia.Markdown/**`（全部 17 个源文件逐读）、`src/Foundation/Arc.Avalonia/Theme/{ArcBaseStyles.axaml,ArcPalette.cs,ArcTokens.cs,ArcThemeManager.cs}`、`src/Theme/Arc.Avalonia.Theme/ArcTheme.axaml`、`tests/Arc.Avalonia.Tests/Program.cs`（MarkdownChecks）、`tools/check-contracts.ps1`、`docs/{18,21,25}`。
- git log（`git log --oneline -40`）与 Android/CJK 相关提交：`11b978d`（首次 CJK 字体回退）、`bccf0ff`（ArcFontBody/Display 改 `FontFamily.Default`）、`6843883`（系统 font fallback 配置）、`ebe6863`（CJK 豆腐结论：模拟器缺字体、真机正常）。**Markdown 组件族只有 `6b4619d` 一次实现提交，之后无任何 Android/字体相关改动**——即 Arc 侧没有针对 Android 的专门分支或已知 hack（grep `OSPlatform|Android` in Markdown = 0）。
- Avalonia 12.1.4 关键语义引用（GitHub tag `12.1.4`）：
  - `src/Avalonia.Controls/Documents/InlineUIContainer.cs`（InlineUIContainer→EmbeddedControlRun、MeasureEmbeddedControls）
  - `src/Avalonia.Base/Media/TextFormatting/TextLineImpl.cs`（`CreateLineMetrics`：EmbeddedControl 参与行度量；显式 LineHeight 的 clamp 语义；`GetBaselineOffset`）
  - `src/Avalonia.Base/Media/TextFormatting/ShapedTextRun.cs`（`Baseline`/`Size`/背景=GlyphRun.Bounds）
  - `src/Avalonia.Controls/TextBlock.cs`（`LineHeightProperty inherits:true`、`ClipToBounds` 默认 true、Measure/Arrange、行度量快照注释）
  - `src/Avalonia.Controls/Panel.cs` 与 NuGet `Avalonia.Controls.xml`（Panel = 子控件铺满同一矩形）
  - `src/Avalonia.Base/Styling/TypeNameAndClassSelector.cs`（`TextBlock.arc-body` 能匹配 `SelectableTextBlock` 派生类型）
- **未决**：见 §10.3；另需 DSHRemote 侧确认其构建使用的 Arc 版本与本文 HEAD 一致（本报告按工作区 HEAD `32f2d8d` 分析）。
