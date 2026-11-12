# DSHRemote 集成侧取证：真机 Markdown 水平叠压的触发条件

> 分析对象：`D:\Code\VibeCoding\DSHRemote`（只读分析；本报告未修改其中任何文件）与工作区 `D:\Code\VibeCoding\Arc.Avalonia`
> 证据截图：`.recon/overlap/7416.jpg`、`.recon/overlap/7417.jpg` 及裁剪图
> 路径约定：`DSHRemote/…` = `D:\Code\VibeCoding\DSHRemote\…`；`Arc/…` = `D:\Code\VibeCoding\Arc.Avalonia\…`

---

## 0. 结论（先给结果）

1. **截图里那一行「最新最APK版//69.8.132.141:9443/__gateway/download」不是字体问题，也不是 Arc 渲染缺陷，而是 DSHRemote 自己的宿主模板缺陷**：`DSHRemote/app/src/DshRemote.App/Theme/AppStyles.axaml` 把 `ArcMarkdownView` 的 `ControlTheme` 整体覆盖，`PART_Host` 从 Arc 官方的 `StackPanel`（`Arc/src/Components/Arc.Avalonia.Markdown/Themes/ArcMarkdownView.axaml:22`）换成了**裸 `Panel`**。Avalonia 的 `Panel` 是**层叠容器**——所有子控件都被 arrange 到同一个 `Rect(finalSize)`、即同一 origin (0,0)（源码级证据见 §0.1-2），所以**同一个 `ArcMarkdownView` 里有多个顶层块时，它们全部画在同一位置**，后画的盖在先画的上面：同 y、不同 x → 「一行内多段文字水平叠压」。
2. **为什么只有个别行出问题**：第二处 workaround「按顶层块拆成多个 `ArcMarkdownView`」（`DSHRemote/…/Controls/MarkdownBodyView.cs:110-125`）只在**每个 group 恰好解析出 1 个 Markdown 节点**时才安全。它的切分依据是**空行**（`MarkdownBodyView.cs:211-248`）而不是 AST 节点边界；`段落 + 紧随其后的列表`（中间没有空行）会留在同一个 group 里，解析出 `ParagraphBlock + ListBlock` 两个节点 → 两个控件同 y 叠放。截图里的两处叠压**恰好都是这个模式**，原始 Markdown 可在 `DSHRemote/_dev/final-msg.md:7-8` 和 `:47-48` 找到（即这条会话消息的原文留档）。
3. **同源后果还有"高度塌陷"**：`Panel` 的 `MeasureOverride` 返回子控件的**最大值**而不是求和（§0.1-2），于是 `ArcMarkdownView` 向父级上报的高度 = 最高块的高度，父 `StackPanel` 预留空间不足 → 后续控件被上提、超出部分被 `ClipToBounds` 裁掉。这正是"行高塌陷 / 链接截断"观感的第二来源，与字体无关。
4. **真机 vs 模拟器的差异不解释这次叠压**：该缺陷与 GPU、ABI、字体无关，桌面版同样会复现。真机之所以"看起来"更糟，是另一个独立因素叠加：`ArcFontBody = FontFamily.Default`（`Arc/src/Foundation/Arc.Avalonia/Theme/ArcPalette.cs:255-256`）→ 真机用了**主题/用户安装的手写体**（截图里连 App 壳层「已连接」「空闲」「DSHRemote」都是手写体），字宽/换行点/字形墨迹与模拟器（Roboto / 部分镜像还是 `□` 豆腐块）完全不同。
5. **"模拟器正常"不构成反证**：DSHRemote 现有模拟器证据截图（`docs/evidence/task22-a-markdown-emulator.png`、`emulator-m6-arc-markdown.png`、`emulator-m6-arc-long-markdown.png`）里，**没有任何一个 block group 含 ≥2 个 Markdown 节点**（块之间都有空行；或消息很短），因此从未触发该组合；且其中至少两张 CJK 是豆腐块、一张 CJK 正常，说明模拟器镜像并不统一。同一条 `final-msg.md` 在模拟器上必然同样叠压。
6. **修复已落地（13:59:10，非本报告所为）**：`app/src/.../AppStyles.axaml:29` 已改为 `<StackPanel x:Name="PART_Host" Spacing="0" …/>`，并在 `:12-22` 补了说明。**但 `_dev/validate/…/AppStyles.axaml` 与 `_dev/backup-app-src/…/AppStyles.axaml` 仍是旧的 `Panel` 版本**（见 §0.1-1），而 `DSHRemote/_dev/settings-notes.md:60` 记载编译校验正是走 `_dev/validate` 副本——**这条校验路径现在编的是带 bug 的旧模板**。
7. **DSHRemote 侧能解释**：该行两段文字同 y 叠压、"链接从 `//` 开始像被截断"、"基线不齐/行高塌陷"的观感、以及此前 README 记载的「表格行压代码块」。**不能解释**（属 Arc/Avalonia 文字栈）：单个 TextBlock 内部字形互相挤压（若确实存在）、行内链接控件不可断行导致的溢出（Arc 用 `InlineUIContainer` 实现链接）、以及任何字体回退度量问题（详见 §6）。

---

## 0.1 决定性复核（回应 Lead 线索）

### 0.1-1 `AppStyles.axaml` 就是 `PART_Host` 覆盖；没有别处换回；但两份影子副本已陈旧

| 版本 | 文件 | sha256 | 行数 | `PART_Host` |
|---|---|---|---|---|
| **旧版（本报告判定的状态）** | `app/src/.../Theme/AppStyles.axaml`（分析快照） | `05E22142C2DB…` | 386 | `:23` `<Panel x:Name="PART_Host" Background="{TemplateBinding Background}" />`；注释 `:12-16`（"re-template it as a simple panel"） |
| **当前 `app/src`（13:59:10 被并发修复）** | `app/src/.../Theme/AppStyles.axaml` | `E86876B51625…` | 392 | `:29` `<StackPanel x:Name="PART_Host" Spacing="0" Background="{TemplateBinding Background}" />`；注释 `:12-22`（已写明 "PART_Host MUST be a StackPanel, not a Panel…"） |
| **陈旧影子副本** | `_dev/validate/DshRemote.App/Theme/AppStyles.axaml` | `05E22142C2DB…` | 386 | 仍是 `:23` `<Panel …/>` |
| **陈旧备份** | `_dev/backup-app-src/DshRemote.App/Theme/AppStyles.axaml` | `05E22142C2DB…` | 386 | 仍是 `:23` `<Panel …/>` |

其它查证（旧版状态）：

- 全仓库 `PART_Host` **只**出现在上述 3 份 `AppStyles.axaml` 里（`app/src`、`_dev/backup-app-src`、`_dev/validate`），**没有第二处 `ControlTheme`、也没有任何代码把 `Panel` 换回 `StackPanel`**；`MarkdownBodyView.CreateMarkdownView` 只设 `Options/Markdown/ClipToBounds`（`MarkdownBodyView.cs:132-137`），不碰模板。
- 模板覆盖生效：`App.axaml:7-10` 先 `<arc:ArcTheme/>` 再 `StyleInclude AppStyles.axaml`，后者的 `Styles.Resources` 里同名 `ControlTheme` 生效（截图现象本身就是"生效"的证明）。
- **风险点**：`_dev/validate` 是编译校验用的隔离镜像（`DSHRemote/_dev/settings-notes.md:60`："Compile: 0 warnings / 0 errors, but through an isolated copy (`_dev/validate/DshRemote.App`)"）。它和 `_dev/backup-app-src` 都还停在 `05E22142…`，即**旧 bug 模板**。任何走 `_dev/validate` 的编译/复核都会验证到错误的行为。

### 0.1-2 Avalonia 12 的 `Panel` 语义：所有子控件从 (0,0) 开始、铺满同一矩形

证据来自**与项目实际使用完全一致的 12.1.4 tag 源码**（工作区 `Arc/Directory.Build.props:7` 与 `DSHRemote/…/DshRemote.App.csproj:10` 均为 `12.1.4`）：

1. `src/Avalonia.Controls/Panel.cs`（12.1.4）**没有覆写** `MeasureOverride` / `ArrangeOverride` —— 因此完全继承 `Layoutable` 的默认实现；
2. `src/Avalonia.Base/Layout/Layoutable.cs`（12.1.4）默认 `MeasureOverride`：对每个子 visual 用**同一个** `availableSize` 测量，返回**各子尺寸的最大值**（不是求和）：

   ```csharp
   protected virtual Size MeasureOverride(Size availableSize) {
       double width = 0, height = 0;
       foreach (child) { child.Measure(availableSize); width = Math.Max(width, child.DesiredSize.Width); height = Math.Max(...); }
       return new Size(width, height);
   }
   ```

3. 默认 `ArrangeOverride`：**把每一个子控件都 arrange 到同一个矩形**：

   ```csharp
   protected virtual Size ArrangeOverride(Size finalSize) {
       var arrangeRect = new Rect(finalSize);            // origin = (0,0)，尺寸 = 面板尺寸
       foreach (child) layoutable.Arrange(arrangeRect);  // 每个子控件拿到同一个矩形
       return finalSize;
   }
   ```

4. `Panel.cs` 的官方注释同样写明（安装包 `D:\.nuget\packages\avalonia\12.1.4\lib\net10.0\Avalonia.Controls.xml`，`T:Avalonia.Controls.Panel`）："All children are layed out to fill the panel."
5. 对照 `StackPanel`（同 tag `src/Avalonia.Controls/StackPanel.cs`）：它**覆写**了两个方法——`MeasureOverride` 竖直方向累加 `height += spacing + child.Height`（并取 `width = max`），`ArrangeOverride` 每行 `rcChild = rcChild.WithY(rcChild.Y + previousChildSize)` 逐行推进。**所以 `Panel`→`StackPanel` 恰好就是"叠放"与"堆叠"的差别。**
6. 本地用法佐证（Avalonia `Panel` 就是当叠层容器用的）：`DSHRemote/…/Views/ChatView.axaml:24-36` 用 `Panel` 把「图片」占位文字、`Image`、「加载中…」三者叠在同一个格子里。

> 实验说明：本来可以用 DSHRemote 自带的 `_dev/MdProbe`（`ProbeApp.cs:81-95` 就有 `panel` / `stackpanel` 两个对照 mode，正是为这个问题写的）跑一张对照图；但 DSHRemote 为只读分析对象、构建会写它的 `bin/obj`，故未执行。**12.1.4 tag 的源码已经把语义定死**（Panel 无 Arrange/Measure 覆写 + Layoutable 默认实现），不需要运行时实验；需要出图时直接跑 `MdProbe panel` / `MdProbe stackpanel` 即可。

### 0.1-3 为什么真机炸、模拟器"看起来正常"

模拟器/桌面证据逐张审阅（DSHRemote 自带）：

| 证据 | 内容 | 是否含"段落紧跟列表/多节点 group" |
|---|---|---|
| `docs/evidence/task22-a-markdown-emulator.png` | CJK **全豆腐块**；`## 用法`、段落、`- 自动允许审批 ✅`、松散列表 | 否：块间都有空行，每个 group 单节点 |
| `docs/evidence/emulator-m6-arc-markdown.png` | 豆腐块；短消息（`你好，这是中文渲染测试。`） | 否 |
| `docs/evidence/emulator-m6-arc-long-markdown.png` | CJK **正常**（说明并非所有模拟器镜像都缺 CJK）；code fence + 表格 | 否：代码块与表格之间有间距，各自独立 group |
| `docs/evidence/task22-a-markdown-table-desktop.png` | 桌面；表格右列被裁（表格比气泡宽，Arc 表格宽度问题的独立现象） | 否 |
| `docs/evidence/desktop-chat-real.png` | 桌面；错误气泡 + 单段长文本 | 否 |

结论：**"模拟器正常"只说明当时那些测试消息没有命中"一个 group 里 ≥2 个 Markdown 节点"这一组合**，不存在任何"模拟器天然不触发"的机制（`Panel` 的布局是纯托管、平台无关的；同一条消息在模拟器上会同样叠压）。

真机额外叠加的**非主因**差异（只影响换行点与观感，不改变是否叠压）：

| 维度 | 真机（arm64 手写体） | 模拟器 | 是否解释本次叠压 |
|---|---|---|---|
| 字体族 | `ArcFontBody = FontFamily.Default`（`ArcPalette.cs:255-256`）→ 系统默认族被换成主题手写体 | Roboto / 部分镜像豆腐块 | 否 |
| CJK 可用性 | 系统完整 CJK | 镜像不一致（有豆腐块也有正常） | 否 |
| fontScale | 无相关代码 | 同 | 否 |
| density | 仅用于 insets 换算（`DSHRemote/…/MainActivity.cs:858-885`） | 同 | 否 |
| Skia/硬件加速 | `RenderingMode=Egl,Software`（Avalonia.Android 12.1.4 包文档） | SwiftShader 软 GL | 否（只影响栅格化） |
| ABI/JIT | arm64，`RunAOTCompilation=false`、`AndroidLinkMode=None` | x86_64，同 | 否（同一份 IL） |

### 0.1-4 DSHRemote 注释的归因复核：一半站得住、一半站不住

| DSHRemote 位置 | 原话 | 复核结论 |
|---|---|---|
| `MarkdownBodyView.cs:13-16` | "Arc's Markdown surface still loses block heights on **Android** when one document mixes tables and code blocks (rows from the table overlap the code block)" | **站不住**：现象（表格/段落盖住另一块）对，归因（Arc、Android）错。真因是自己覆盖的 `Panel` 宿主，与平台无关；Arc 官方模板就是 `StackPanel`（`ArcMarkdownView.axaml:22`）。该注释当前**仍未更新**（文件 mtime 未变）。 |
| `MarkdownBodyView.cs:155-160` | "Arc renders inline links as HyperlinkButton controls, which sit on a different baseline … and do not wrap inside narrow bubbles (the top edge of the next line then overlaps the link)" | **站得住**：Arc 把行内链接渲染成 `InlineUIContainer` 包 `HyperlinkButton`，内部 `TextBlock{TextWrapping=NoWrap}`、`Padding=2,2,2,2`、`MinHeight=24`、`VerticalAlignment=Center`（`Arc/…/Rendering/Inlines/ArcMarkdownDefaultInlineRenderer.cs:256-284`、`:334`）。长 URL 不可断行 + 行盒被抬高 → 溢出被裁 + 基线错位。**这是"链接截断/基线不齐"的真因，和本行叠压是两回事。** |
| `README.md:250`、`:295`、`:302`、`:321` | "ArcMarkdownView 在 Android/Skia 上对长段落渲染会叠层"、"不是字体问题，是 Arc 文档级测量缺陷" | **需要修正**：「表格压代码块」「多段文字叠在同一 block」= `Panel` 宿主；「行内链接的行高/基线」= Arc。混在一起归因 Arc 不成立。 |
| `AppStyles.axaml`（当前 `:17-21`） | "PART_Host MUST be a StackPanel, not a Panel … A plain Panel lays every child out at the same origin" | **正确**（13:59:10 之后补写的修复说明，与本报告结论一致）。 |

### 0.1-5 修复判断

- **最小且正确**：`PART_Host` 用 `StackPanel`（并 `Spacing="0"`），与 Arc 官方模板一致 —— **这正是 13:59:10 已落地的改法**（`AppStyles.axaml:29`）。
- **兼容性已核实**：`ArcMarkdownView` 声明 `[TemplatePart("PART_Host", typeof(Panel))]`（`Arc/…/Controls/ArcMarkdownView.cs:23`）并按 `Find<Panel>` 取（`:290`）——`StackPanel : Panel`，可接受；`PART_Scroll` 可选，全部 `_scroll` 用法都有 null 守卫（`:242-248`、`:284-296`、`:392-405`）。
- **Arc 没有"无滚动"入口**（Lead 的猜测正确）：`ArcMarkdownDocumentView` 只是 `ContentControl`，内部 `private readonly ArcMarkdownView _inner = new()`（`Arc/…/Controls/ArcMarkdownDocumentView.cs:10-22`），仍然走 `ArcMarkdownView` 的滚动模板；`ArcMarkdownView` 公开面只有 `Markdown`/`Options`/事件/`Blocks`（`:26-140`），没有 `ScrollEnabled`/`Overflow` 之类属性。→ 当前唯一正确做法就是"重模板 + StackPanel"。
- **仍需收尾**（见 §7）：同步 `_dev/validate` / `_dev/backup-app-src`；更新 `MarkdownBodyView.cs` 注释与 README 归因；重评"按空行拆块"是否还需要；真机回归同一条消息。

---

## 1. 证据对齐：截图那一行到底是什么

### 1.1 原始 Markdown（这条消息的留档原文）

`DSHRemote/_dev/final-msg.md`（与截图内容逐句对应）：

```
 7: 修复：
 8: - 重新 **clean 构建** v1.2.5，程序集存储恢复完整；
 9: - `scripts/build-release-apk.ps1` 现在每次发布前强制删除 Android `bin/Release`、`obj/Release`，避免再次产出坏包；
10: - 用 x86_64 Release APK 在模拟器实测可正常启动；VPS 上已删除错误的 1.2.3/1.2.4，只保留可用的 1.2.5。
...
47: **最新 APK**：
48: - <https://69.8.132.141:9443/__gateway/download>
49: - 直链：<https://69.8.132.141:9443/__gateway/download/dshremote-1.2.5-18-arm64-release.apk>
50: - 1.2.5 (18)，34.12 MB，sha256 `4f3f6ad7…`
```

### 1.2 截图与原文对位

| 截图现象 | 原始 Markdown | 结论 |
|---|---|---|
| `7417.jpg` 中「最新最APK版//69.8.132.141:9443/__gateway/download」 | `:47` 段落 `**最新 APK**：` **压在第 48 行列表项** `<https://69.8.132.141:9443/__gateway/download>` 之上 | 段落（粗体 `最新 APK` + 全角冒号）与列表第 1 行的 URL 同 y 叠压：`APK` 盖住 URL 的 `https`，全角 `：` 盖住 `s:`，于是 URL 只剩 `//69.8.132.141:9443/__gateway/download` 可读 |
| 同图下方「· 直链：https://…/download/」干净显示 | `:49` 列表第 2 项 | 同一个 `ListBlock` 的第 2 行不受影响（同一控件内部正常排布） |
| `7416.jpg` 中「修复重新 clean 构建 v1.2.5，程序集存储恢复完整；」 | `:7` 段落 `修复：` **压在第 8 行列表项** `- 重新 **clean 构建** v1.2.5…` 之上 | 同一模式的第二次复现，`修复` 与 `· 重新` 叠在一起（截图上「修」字里能看到列表 `·` 的点） |
| 「行内链接截断」观感 | 同 §1.2 第 1 行 | URL 开头的 `https:` 被段落盖住 → 用户读到的是从 `//` 开始，误判为「链接被截断」；后半段 `…/dshremote-…apk` 在列表第 2 项里是完整的，说明数据没丢 |

> 注意：`Preprocess` 会把 `<url>` 尖括号形式改写成裸 URL 并插入 `\u200B`（`MarkdownBodyView.cs:191-192`、`205-208`）。这不影响本判断——被压住的 `https:` 与 ZWSP 无关，是 y 相同造成的覆盖。

### 1.3 为什么恰好"只叠一行"

- 段落节点渲染成 1 个 `TextBlock`（`Arc/…/Rendering/Blocks/ArcMarkdownBlockRenderers.cs:26-33`），高度 ≈ 1 行 + 下边距。
- 列表节点渲染成 1 个 `Grid`（`ArcMarkdownBlockRenderers.cs:147-221`），第 1 行从 Grid 的 y=0 开始。
- `Panel` 把两个子控件都 arrange 到同一个 `Rect(finalSize)` → 段落的第 1 行（y=0）与列表第 1 行（y=0）**完全重合**；列表第 2、3 行在 y=行高、2×行高，因此正常。
- `ArcMarkdownView` 高度 = `Panel` 的 DesiredSize = 最高子控件（列表）的高度，所以列表不会被裁；但**段落的行高没有计入**（§0.1-2 的"最大值"语义）→ 父级少预留段落的行高，表现为"行高塌陷"。
- 与截图完全一致：只有 1 行坏掉，列表第 2/3 项、后续引用块都正常。

---

## 2. 一条最终助手消息的完整渲染链与测量链

### 2.1 数据 → 视图

| 步骤 | 位置 |
|---|---|
| 最终文本落在 `ChatMessageViewModel.Text`（reveal 泵走完后） | `DSHRemote/…/ViewModels/ChatMessageViewModel.cs:116-126`、`:471-481` |
| 该轮被选为该 turn 的 `final` | `DSHRemote/…/ViewModels/SessionViewModel.cs:1645-1653` |
| turn 折叠块 + `Final` 集合进入 `Timeline` | `SessionViewModel.cs:1655-1673`、`:138` |
| `Timeline` 的 `ItemsControl` 用 `ChatTurnViewModel` 模板 | `DSHRemote/…/Views/ChatView.axaml:348-418` |
| `Final` 子 `ItemsControl` → `MarkdownBodyView` | `ChatView.axaml:377-393` |
| `MarkdownBodyView` 模式：`UseMarkdown = IsMarkdown`（仅 assistant 且非 streaming） | `ChatView.axaml:389-393`、`ChatMessageViewModel.cs:149` |

### 2.2 `MarkdownBodyView` 内部（两处 workaround 的所在）

```
MarkdownBodyView (ContentControl, ClipToBounds=true)         MarkdownBodyView.cs:41-56
└─ Content = StackPanel { Spacing = 8 }                      MarkdownBodyView.cs:118
   └─ ArcMarkdownView × N（每个"空行 group"一个）              MarkdownBodyView.cs:119-125
      ├─ Options = { EnableTextSelection = true }            MarkdownBodyView.cs:132-137
      ├─ ClipToBounds = true                                 MarkdownBodyView.cs:136
      └─ PART_Host
         ├─ 旧版 = Panel        → 全部 arrange 到 (0,0)（本 bug） AppStyles.axaml:23（sha 05E2…）
         └─ 新版 = StackPanel  → 逐块纵向堆叠                    AppStyles.axaml:29（sha E868…）
```

`ArcMarkdownView.RenderDocument` 只做 `_host.Children.Add(control)`（`Arc/…/Controls/ArcMarkdownView.cs:490-531`），**它依赖宿主是堆叠面板**；Arc 自己的模板注释也写明"renderer builds Arc controls into PART_Host"并给的是 `StackPanel`。

### 2.3 宽度 / 裁剪 / 虚拟化

- 气泡：`Border.bubble` `MaxWidth=560`（`AppStyles.axaml:106-113`）；assistant 覆盖成 `MaxWidth=760`（`AppStyles.axaml:196-198`）。手机上实际可用宽度由屏幕决定（≈ 300–330 DIP），`MaxWidth` 在真机上不起约束作用。
- 裁剪：`MarkdownBodyView` 静态构造 + 实例构造 + 每个 `ArcMarkdownView` 三处 `ClipToBounds=true`（`MarkdownBodyView.cs:43`、`:52`、`:136`）。任何溢出（叠放后的越界、链接控件超宽）都会被**硬裁**，这是"截断"观感的来源。
- **列表没有虚拟化**：`ScrollViewer → StackPanel → ItemsControl`（`ChatView.axaml:192-197`、`:233`），`ItemsControl` 默认 `ItemsPanel` 是 `StackPanel`，**没有任何 `VirtualizingStackPanel`**；`ChatView.axaml.cs` 只有 IME/滚动/分页逻辑（`:126-171`、`:310-353`）。→ **"虚拟化回收导致同 y 复用"直接排除**。
- 没有任何 `LineHeight` / `LetterSpacing` / `FontSize` 覆盖：全工程 grep 无命中（唯一字体相关设置是 `AppStyles.axaml:24` 的 `FontFamily={DynamicResource ArcFontBody}`）。→ **"DSHRemote 覆写了行高"排除**。

---

## 3. 两处 workaround 逐条评估

### 3.1 Workaround A：按顶层块拆成多个 `ArcMarkdownView`

**做法**：`MarkdownBodyView.cs:101-130`（`ShowMarkdown`）→ `Preprocess` 清洗 → `SplitTopLevelBlocks`（空行切分、``` 围栏内不切，`:211-248`）→ 若 group 数 ≤ 1 直接一个 `ArcMarkdownView`，否则 `StackPanel{Spacing=8}` 逐块放。

**有效性**：**部分有效，且只对"一个 group = 一个 AST 节点"的行有效**——在 `Panel` 宿主时代它把"全文档叠压"降级为"个别行叠压"；宿主修回 `StackPanel` 之后，它在**正确性**上已不再必要。

| 场景 | group 数 | 每个 group 的 AST 节点数 | 旧版(Panel)是否叠压 |
|---|---|---|---|
| 段落间都有空行（示例里绝大多数） | N | 1 | 否（Panel 只放 1 个控件） |
| 标题 + 空行 + 段落 | N | 1 | 否 |
| **段落 + 紧随的列表（无空行）** | 1 | **2（Paragraph + List）** | **是** ← 本次截图 |
| 段落 + 紧随的 ATX 标题 / 段落 + 表格（GFM 表格可打断段落） | 1 | 2 | 是（同类隐患） |
| 表格 + 代码块（同一 group） | 1 | 2 | 是（README:295 的旧现象） |

**副作用（逐项，与宿主修不修无关）**：

1. **块间距被改**：Arc 自己的宿主是 `Spacing=0`，间距由块自身 `Margin.Bottom`（正文 12 DIP，`ArcMarkdownStyle.cs:89`、`ArcMarkdownBlockRenderers.cs:30`）提供；拆块后又叠加 `StackPanel Spacing=8`（`MarkdownBodyView.cs:118`）→ 顶级块间距 12→20，且**单块与多块消息间距不一致**（单块走 `:112-115`）。
2. **`~~~` 围栏判据两处不一致**：`Preprocess` 认 ``` 和 ~~~（`:174-175`），`SplitTopLevelBlocks` 只认 ```（`:230`）。含 `~~~` 代码块且块内有空行的文档会被**从代码块中间切开**。
3. **缩进代码块（4 空格）不受保护**：`Preprocess` 只跳过围栏（`:174-186`），缩进代码块里的 URL 会被插 ZWSP、`[x](y)` 会被改写。
4. **引用式链接定义被拆散**：`[label]: url` 独立成块（Arc 侧 `SkipRenderer` 不渲染），其它块拿不到定义 → 退化成字面文本。
5. **块级上下文丢失**：多段引用块、松散列表被打断，rail/缩进连续性断裂。
6. **测量与解析成本 ×N**：每个 group 一个 `ArcMarkdownView`（各自 `ParseDocument`，`ArcMarkdownView.cs:141`），且每个实例订阅 `ActualThemeVariantChanged`（`:302-312`）；`MarkdownBodyView` 在 `Text` 变化时整体重建（`:258-271`）。
7. **跨块选择丢失**：`EnableTextSelection=true` 是**逐块**的（`MarkdownBodyView.cs:134`），跨块选择/复制被切断。

### 3.2 Workaround B：行内链接改写成纯文本 + 裸 URL 插零宽空格

**做法**：`MarkdownBodyView.cs:139-208`。`[label](url)` → `label（url）`（`:188-190`）；`<url>` → 裸 url（`:191`）；裸 URL 在 `/ . _ - ? = & #` 后插 `\u200B`（`:147-153`、`:205-208`）；围栏内跳过（`:174-186`）。

**要解决的问题真实存在（Arc 侧证据，§0.1-4 已核）**：`HyperlinkButton` + `NoWrap` 内层 TextBlock + `MinHeight=24` + `Padding`（`ArcMarkdownDefaultInlineRenderer.cs:256-284`）→ 窄气泡里长 URL 不可断行、行盒被抬高 → 溢出被裁 + 基线错位。

**副作用（逐项）**：

1. **行内图片被破坏**：`InlineLinkPattern`（`:139-141`）会命中 `![alt](img.png)` 里的 `[alt](img.png)`，输出 `!alt（img.png）` → 图片变字面文本（功能性回归，应加负向前瞻排除 `!`）。
2. **行内代码内容被改写**：`` `[a](b)` ``、`` `https://x/y` `` 会被改写（`:188-192` 不检查反引号），复制出的代码语义被污染。
3. **复制文本含零宽空格**：粘贴到浏览器/终端可能失败（消息里"复制文本不受影响"的说法不成立）。
4. **强依赖"链接不可点"现状**：DSHRemote 从未订阅 `LinkClicked`、也未实现 `IArcMarkdownLinkHandler`（默认 no-op，`Arc/…/Rendering/ArcMarkdownContracts.cs:130-135`；DSHRemote 侧 grep 只命中 `MarkdownBodyView.cs:134`）。以后要恢复可点击链接必须整体回滚。
5. **丢弃 `title`**：`[a](url "title")` 的 title 被吞（`:190`）。
6. **尖括号 autolink 语义丢失**：`<url>` 变裸文本（`:191`）。
7. **ZWSP 只覆盖 `/ . _ - ? = & #`**：`;` `%` `,` 或超长无分隔段仍不可断；`__gateway` 会被断在 `_` 后。

---

## 4. 真机 vs 模拟器差异总表（结论：均不解释叠压）

| 维度 | 真机（arm64，手写体主题字体） | 模拟器（x86_64 / Roboto 或豆腐块） | 与叠压的关系 |
|---|---|---|---|
| 字体族 | `ArcFontBody/ArcFontDisplay = FontFamily.Default`（`ArcPalette.cs:255-256`）→ 系统默认族被主题字体替换 | 默认 Roboto | 否；只影响换行点/观感 |
| CJK 可用性 | 完整 | 镜像不一致（`task22-a-markdown-emulator.png` 豆腐块；`emulator-m6-arc-long-markdown.png` 正常） | 否 |
| fontScale | 无相关代码 | 同 | 否 |
| density | 仅 insets 换算（`MainActivity.cs:858-885`） | 同 | 否 |
| Skia/硬件加速 | `RenderingMode=Egl,Software` | SwiftShader | 否 |
| ABI/JIT | arm64，未 AOT（`DshRemote.App.Android.csproj:13-15`） | x86_64，同 | 否 |
| Avalonia/Arc 版本 | 12.1.4 + Arc ProjectReference（`DshRemote.App.csproj:10,18`） | 同 | 否 |

Android 侧与字体/缩放相关的**全部**代码（均无字体配置）：`MainActivity.cs:1074-1141`（`AvaloniaAndroidApplication<App>`，`OnCreate` 只注入状态目录/配对路径，无 `CustomizeAppBuilder`/`FontManagerOptions`/`WithInterFont`）、`MainActivity.cs:25-39`（Activity 特性含 `Density`）、`Resources/values/styles.xml`、`AppStyles.axaml:24`（唯一 `FontFamily`，值仍是 Arc token）。

Arc 侧字体结论（判断"字体回退风险"必读）：`Arc/docs/25-mobile-usage-guidance.zh.md:51-54`（默认字体策略）、`:112-118`（"Android 上不要依赖 `FontFamily.Default` 的逐字回退；Avalonia 12 + Skia 在混排文本里不会逐字替换字体"）、`:145-156`（真机正常/模拟器缺字）；git `bccf0ff`/`6843883`/`f64ccad`/`faa443c`/`ebe6863`（当日 01:59–03:38 的 CJK 字体实验链）。**风险**：DSHRemote 与 Arc Gallery 同处"纯系统字体"策略，任何主题字体（尤其手写体）都会改变 Markdown 版面，从而放大任何布局缺陷。

---

## 5. 四个假设的裁决

针对「最新版本」+「APK…https」在同一行水平叠压：

### (a) 同一 `TextBlock` 内 run 推进错误
**否**。叠压的两段文字**分属两个 Markdown 块**、在 Arc 里是两个独立控件（`ParagraphRenderer`→`TextBlock`、`ListRenderer`→`Grid`）；且两者各自从自己的起点开始（段落 x=0，列表内容列 x≈24 DIP = marker 列 16 + margin 8），正是"两个控件都被 arrange 到 (0,0)"的特征，而不是单行内 run 推进错。同一消息里列表第 2/3 项排版正常。

### (b) 两个控件同 y 叠放
**是（直接原因，源码级已闭环）**。`Panel` 无 Arrange/Measure 覆写 → `Layoutable` 默认把每个子控件 arrange 到同一个 `Rect(finalSize)`（§0.1-2）；块切分粒度是"空行"（`MarkdownBodyView.cs:211-248`），而 `final-msg.md:7-8`、`:47-48` 的"段落+列表无空行"合成一个 group → 一个 `ArcMarkdownView` 里 2 个块控件 → 同 y 叠压；列表第 2 行起正常、只有 1 行坏掉，与该模型精确一致。修复（`Panel`→`StackPanel`）已于 13:59:10 落地。

### (c) 行高塌陷导致换行重叠
**否（对本次行而言），但"高度塌陷"确实是同源后果**。本行不是"同一段落第 2 行回卷到第 1 行"（URL 起点在列表内容列、不是 x=0）；相邻正文行也没有真正交叠（`7416.jpg` 段 5 行、heading 2 行只是墨迹贴边，行距≈正常行高）。真正存在的"塌陷"是 `Panel` 只上报 max 高度导致父级少预留（§0.1-2、§1.3），以及旧版行内链接 `MinHeight=24` 抬高行盒（§0.1-4）。

### (d) 字体回退度量不一致
**不能解释本次水平叠压；能解释"英文中文挤在一起/大小不一"的观感**。字体回退只会造成"同一行内字符间距/字形宽度不匹配"或"缺字形"，不会把两个不同块的文字放到同一 y 并各自从自己的 x 起点开始。真机手写体把 CJK 画得比 Latin 小（截图对比 `1.2.5 (18)，34.12 MB` 与 `最新 APK` 的墨迹大小）属于字体设计差异。若后续在"单块单 TextBlock、ASCII+CJK 混排"段落上仍测到**同一行内 ink 互相重叠**，那才是 (d) 的真实命中，需要 Arc/Avalonia 侧按 `docs/25:112-118` 的思路验证。

---

## 6. DSHRemote 侧能解释 / 不能解释

### 能解释（集成侧，已闭环）

| 现象 | 机制 | 证据 |
|---|---|---|
| 一行内两段文字水平叠压、同 y | `PART_Host` 被换成 `Panel`（层叠容器） | `AppStyles.axaml:23`（旧）/`:29`（新）vs `ArcMarkdownView.axaml:22`；Avalonia 12.1.4 `Panel.cs` + `Layoutable.cs` |
| 只炸"段落紧跟列表/标题/表格"的那些行 | 拆块按空行切，一个 group 可含多个 AST 节点 | `MarkdownBodyView.cs:211-248` + `final-msg.md:7-8`、`:47-48` |
| URL 看起来"从 `//` 开始、被截断" | 段落盖住 `https:` + 三层 `ClipToBounds` | `MarkdownBodyView.cs:43,52,136` |
| "行高塌陷"、下方内容贴太近 | `Panel` 只上报 max 高度（不求和） | Avalonia 12.1.4 `Layoutable.MeasureOverride` |
| 单块 / 多块块间距不一致 | 拆块后额外 `Spacing=8` | `MarkdownBodyView.cs:118` vs `ArcMarkdownStyle.cs:89` |
| README 里"表格+代码块叠层"的旧结论 | 同一 Panel 叠放 | `README.md:295`、`:302` |

### 不能解释（属 Arc / Avalonia / 平台文字栈）

| 现象 | 归属 |
|---|---|
| 行内链接 `HyperlinkButton` 不可断行 / 抬高行盒（"链接截断/基线不齐"） | Arc inline 渲染选择（`InlineUIContainer` + `NoWrap` + `MinHeight=24`） |
| 单个 TextBlock 内部字形 ink 互相挤压（若确有，需实测） | Avalonia `TextLayout` + Skia 字体回退/度量 |
| 文档内表格宽度溢出气泡（`task22-a-markdown-table-desktop.png` 右列被裁） | Arc 表格渲染器测量 |
| 手写体下 CJK 墨迹小于 Latin、行距观感紧 | 系统主题字体 |
| 模拟器豆腐块 | 模拟器镜像缺 CJK（环境） |

---

## 7. 修复与验证（截至本报告）

**已落地（13:59:10，非本报告所为）**

- `app/src/.../AppStyles.axaml:29`：`<Panel>` → `<StackPanel x:Name="PART_Host" Spacing="0" …/>`；`:12-22` 补了成因说明。改法与 Arc 官方模板一致，兼容性见 §0.1-5。

**必须补的收尾**

1. **同步影子副本**：`_dev/validate/DshRemote.App/Theme/AppStyles.axaml` 与 `_dev/backup-app-src/.../AppStyles.axaml` 仍是 `05E22142…`（旧 `Panel`）。`_dev/settings-notes.md:60` 表明编译校验走 `_dev/validate` 副本 → **不同步就会验证到旧行为**。
2. **真机/模拟器回归同一条消息**：贴 `_dev/final-msg.md` 全文，看 `7416/7417` 那两行是否恢复；模拟器也跑同一份，确认 x86_64 同样不再叠压（按本报告推断旧版在模拟器上也会叠压）。
3. **重评拆块 workaround**：宿主修好后，"按空行拆块"不再是正确性必需；若移除，可同时恢复跨块选择、统一块间距（12 DIP）、减少 N 次解析。若要保留，至少把 `Spacing=8` 去掉或把 `~~~` 围栏判据补齐（`MarkdownBodyView.cs:230`）。
4. **修正归因文案**：`MarkdownBodyView.cs:13-16` 与 `README.md:250/295/302/321` 仍把"块叠层"归给 Arc/Android；应改为"宿主模板覆盖 + 空行拆块粒度"。（链接/基线那一段（`MarkdownBodyView.cs:155-160`）保持原样，归因正确。）
5. **负向修复**：`Preprocess` 的 `InlineLinkPattern` 应排除 `![...](...)` 与反引号内内容（§3.2 第 1、2 条）。

**若要在改代码前先取证（可选，最便宜）**

- 桌面版贴 `final-msg.md` 全文：按本报告推断，**旧模板下桌面同样叠压**（Panel 布局平台无关）；这能一次性排除"Android/字体/GPU"。
- DSHRemote 自带 `_dev/MdProbe`（`ProbeApp.cs:81-95` 的 `panel` / `stackpanel` 两 mode）可直接出对照图。

**Arc 侧次要/长期建议（与本次叠压无关）**

1. 行内链接：给宿主一个"链接渲染策略"（`InlineUIContainer` vs `Span+TextDecorations`），或让链接内部可断行；否则窄屏宿主只能像 DSHRemote 一样降级为纯文本。
2. 提供 `ScrollEnabled`/`OverflowMode` 之类属性，省掉宿主"重模板"（现在唯一可行手段，且极易踩 Panel/StackPanel 的坑）。
3. 文档：在 `docs/21-markdown-components.zh.md` / `docs/25-mobile-usage-guidance.zh.md` 写明"**重模板 `ArcMarkdownView` 时 `PART_Host` 必须是 `StackPanel`（或其它堆叠面板），不能是 `Panel`/`Grid`**"的宿主契约。

---

## 8. 证据文件索引

| 证据 | 位置 |
|---|---|
| 真机截图（叠压两处） | `.recon/overlap/7416.jpg`（`修复：`+列表）、`.recon/overlap/7417.jpg`（`最新 APK：`+列表） |
| 裁剪/放大 | `.recon/overlap/line_wide.png`、`line_zoom.png`、`topline_left.png`、`prevline.png`、`img2_s2.png`、`G_latest.png`、`D_link.png`、`B_rootcause.png` |
| 该消息的原始 Markdown | `DSHRemote/_dev/final-msg.md:7-8`、`:47-50` |
| 模板覆盖（根因） | `DSHRemote/app/src/DshRemote.App/Theme/AppStyles.axaml`：旧 sha `05E22142…` `:12-16/:23`（Panel）；新 sha `E86876B5…` `:12-22/:29`（StackPanel）；影子副本 `_dev/validate`、`_dev/backup-app-src` 仍旧 sha `05E22142…` |
| Arc 官方模板 | `Arc/src/Components/Arc.Avalonia.Markdown/Themes/ArcMarkdownView.axaml:14-25`（`:22` StackPanel） |
| Avalonia 12.1.4 布局语义 | 源码 tag `12.1.4`：`src/Avalonia.Controls/Panel.cs`（无 Measure/Arrange 覆写）、`src/Avalonia.Base/Layout/Layoutable.cs`（默认 `MeasureOverride`=max、`ArrangeOverride`=所有子控件同一 `Rect(finalSize)`）、`src/Avalonia.Controls/StackPanel.cs`（逐行推进）；安装包 XML：`D:\.nuget\packages\avalonia\12.1.4\lib\net10.0\Avalonia.Controls.xml`（`T:Avalonia.Controls.Panel`） |
| 本地 `Panel` 叠层用法 | `DSHRemote/…/Views/ChatView.axaml:24-36` |
| 拆块 / 预处理 | `DSHRemote/app/src/DshRemote.App/Controls/MarkdownBodyView.cs:101-248` |
| 行内链接渲染（Arc） | `Arc/src/Components/Arc.Avalonia.Markdown/Rendering/Inlines/ArcMarkdownDefaultInlineRenderer.cs:247-335` |
| 块渲染器（Arc） | `Arc/…/Rendering/Blocks/ArcMarkdownBlockRenderers.cs:22-48`（段落）、`:147-279`（列表） |
| 字体 token | `Arc/src/Foundation/Arc.Avalonia/Theme/ArcPalette.cs:250-262`；`Arc/…/Rendering/ArcMarkdownStyle.cs:15-47`；`Arc/src/Theme/Arc.Avalonia.Theme/ArcFluentOverrides.cs:136-142` |
| Arc 移动端字体结论 | `Arc/docs/25-mobile-usage-guidance.zh.md:51-54`、`:112-118`、`:132-156`；git `bccf0ff`、`6843883`、`f64ccad`、`faa443c`、`ebe6863` |
| 模拟器/桌面证据 | `DSHRemote/docs/evidence/task22-a-markdown-emulator.png`、`emulator-m6-arc-markdown.png`、`emulator-m6-arc-long-markdown.png`、`task22-a-markdown-table-desktop.png`、`desktop-chat-real.png` |
| 自带对照探针 | `DSHRemote/_dev/MdProbe/ProbeApp.cs:81-95`（`panel` / `stackpanel`） |
| 编译校验副本说明 | `DSHRemote/_dev/settings-notes.md:60` |
| 历史结论（待修正） | `DSHRemote/README.md:250`、`:295-297`、`:302`、`:321`；`DSHRemote/docs/research/arc-agent-integration.md:99-111` |

---

### 附：一句话给 DSHRemote 维护者

> `ArcMarkdownView` 的 `PART_Host` 必须是**堆叠面板**，你把它换成了 `Panel`；`Panel` 在 Avalonia 里是层叠容器（所有子控件都 arrange 到同一个矩形、只上报最大高度），所以你看到的"多段文字画在同一基线"+"行高塌陷"是**你的模板**造成的，不是 Arc 的测量缺陷。按空行拆块只是把 N 块文档拆成 1 块，遇到"段落紧跟列表"（无空行）就回到同一个 `ArcMarkdownView` 里复发——真机截图 `7416/7417` 的两处都是这个组合。改成 `StackPanel`（已改）即解决；记得同步 `_dev/validate`、`_dev/backup-app-src` 两份旧副本，并修正 `MarkdownBodyView.cs:13-16` 与 README 的归因。链接那一路（HyperlinkButton 不可断行）是另一回事，现在的降级方案可以留，但别让它把 `![alt](url)` 和行内代码一起改坏。
