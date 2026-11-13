# 21 · Markdown 组件族（PuraMarkdown 语法/渲染引擎 + Arc token skin）

> 状态：M3 波次实现记录 + PuraMarkdown 0.2 迁移（2026-11-13）。目标：把 PuraMarkdown 的解析/流式/渲染引擎
> 接到 Arc 设计系统里，由 Arc 提供**生产级**的 Markdown 渲染控件与 token skin（ADR 0008）。

---

## 1. 边界与依赖

```text
PuraMarkdown.Core (独立包，Markdig 1.5.1，无 Avalonia，ADR 0006)
  PuraSyntaxBuilder / PuraSyntaxPipeline
  PuraDocument = MarkdownDocument + Version + Diagnostics + Outline
  ParseIncremental / StreamingBlockScanner
        │ Markdig AST + 诊断/大纲
        ▼
PuraMarkdown.UI (Avalonia 控件/渲染引擎，ADR 0008)
  PuraMarkdownView / PuraDocumentView / PuraMarkdownOptions / PuraTheme
  增量流式（PuraStreamingSession）+ 渲染调度/帧预算 + 块级虚拟化
  换肤扩展契约：IPuraPlugin、renderer 装饰/覆盖、PuraParts / PuraClasses
        │
        ▼
Arc.Avalonia.Markdown (L2 组件族)
  ArcMarkdownView / ArcMarkdownDocumentView / ArcMarkdownCodeBlock（Arc 宿主控件壳）
  ArcMarkdownPipeline（Arc renderer pipeline）+ Arc token skin（ArcMarkdownTheme.axaml）
  链接/图片/剪贴板平台契约 + 本地化 + warning/无障碍包装
        │
        ▼
Arc.Avalonia.Chat (L2)  /  Arc.Avalonia.Agent (L4)
  Chat 由 PuraChatView（PuraMarkdown.Agent）渲染气泡正文；Agent 用 pura-agent 围栏渲染 choice/confirm，
  并按 Timeline 组合气泡与事件卡（ADR 0007）
```

* **解析/诊断/大纲/流式边界**归 PuraMarkdown.Core；Arc 不复制 Markdig 节点，也不重新实现解析。
* **Markdown 渲染引擎**（控件、节点渲染、增量流式、虚拟化、调度）归 PuraMarkdown.UI；**Arc token skin、
  renderer pipeline、宿主控件壳、平台契约与本地化**归 Arc（ADR 0008）。
* 依赖例外：`PuraMarkdown.Core`/`PuraMarkdown.UI`/`Markdig` 只允许在 `Arc.Avalonia.Markdown`、`Chat`、`Agent`
  的有范围白名单内出现；其它 Arc 包的依赖白名单不变。
* 本地构建：`PuraMarkdown.Core`/`PuraMarkdown.UI`/`PuraMarkdown.Agent` 均来自 sibling checkout 的本地包源
  （`NuGet.config` 里的 `../PuraMarkdown/artifacts/nuget`），首次构建前在 PuraMarkdown 仓库执行
  `dotnet pack ArcUI 相关工程 -c Release -o artifacts/nuget`（至少含 Core/UI/Agent 三个包）。

## 1.1 上游包改名（2026-11-10，PuraMarkdown ADR-0028；0.1 时代映射）

PuraMarkdown 把 3 个包改名为职责对齐的 id，并同步对齐命名空间（**不保留旧包 id / 旧命名空间**）：

| 旧 | 新（0.1 形态） | Arc 是否使用（0.1） |
| --- | --- | --- |
| `PuraMarkdown.Syntax` | **`PuraMarkdown.Core`**（Markdig 解析、`PuraDocument`、大纲、诊断、流式扫描；无 Avalonia） | ✅ 唯一使用 |
| `PuraMarkdown`（UI 控件） | `PuraMarkdown.Agent`（Avalonia 渲染控件 + Agent/Chat/Testing）；0.2 起拆为 `PuraMarkdown.UI` + `PuraMarkdown.Agent`，见 §1.3 | ⚠️ 部分使用：仅 Chat/Agent 与 Agent 迁移委托（ADR 0007）；Markdown 包 0.2 起获准使用 UI |
| `PuraMarkdown.Plugins` | `PuraMarkdown.Extension`（ColorCode/Mermaid，渲染引擎依赖只在此包） | ❌ 不使用 |

命名空间映射：`PuraMarkdown.Syntax` / 根 `PuraMarkdown` → **`PuraMarkdown.Core`**；`PuraMarkdown.Security` → **`PuraMarkdown.Core.Security`**。版本仍为 `0.1.0-alpha.1`。
Arc 侧同步修改：`Directory.Packages.props`、`Arc.Avalonia.Markdown.csproj`、Markdown 包内 using、测试、`tools/deps-policy.json`、`tools/check-package.ps1`、README/ADR 0006/第三方声明。

## 1.2 Chat/Agent 渲染改由 PuraMarkdown.Agent 承担（2026-11-11，ADR 0007）

按 [docs/29](29-puramarkdown-agent-migration.zh.md) §4 的决策，`Arc.Avalonia.Chat` 与 `Arc.Avalonia.Agent`
的聊天/Agent 渲染（消息气泡、流式正文、工具卡片、choice/confirm、Agent fence）委托上游
**`PuraMarkdown.Agent`**；Arc 不再自研同层渲染器，保留 Agent 表现运行时、布局壳、composer 输入框、
审批/注意力 UI 与主题适配（`ArcChatThemeAdapter` → `PuraTheme`）。

依赖边界（ADR 0007 有范围例外）：

* `PuraMarkdown.Agent` 只允许 `Arc.Avalonia.Chat` / `Arc.Avalonia.Agent` **直接引用**；
* 0.2 起本包（`Arc.Avalonia.Markdown`）获准直接引用 `PuraMarkdown.Core` + `PuraMarkdown.UI`（+ `Markdig`），
  渲染委托上游引擎（ADR 0008）；Foundation/其它 L2/L3 包仍不得直接引用 PuraMarkdown 的 Avalonia 包；
* 传递依赖 `Avalonia.Controls.ItemsRepeater`、`JsonSchema.Net`（→ `JsonPointer.Net` / `Json.More.Net` /
  `Humanizer.Core`）只在经这些例外路径进入闭包，`tools/check-deps.ps1` 的 `agentChatBridge` /
  `markdownUiEngine` scope 会拒绝越界包；
* 许可证与用途登记在 [`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md)。

Chat/Agent 的目标 API、删除清单与兼容性见 [docs/29](29-puramarkdown-agent-migration.zh.md) §2–§5。

## 1.3 PuraMarkdown 0.2 拆分与渲染委托（2026-11-13，ADR 0008）

PuraMarkdown 0.2 把 0.1 的单个 `PuraMarkdown.Agent` 拆成两个包（版本统一 `0.2.0-alpha.1`）：

| 包 | 职责 | Arc 直接引用范围 |
| --- | --- | --- |
| **`PuraMarkdown.UI`** | Avalonia Markdown 控件/渲染引擎：`PuraMarkdownView`/`PuraDocumentView`、`PuraMarkdownOptions`/`PuraTheme`、增量流式（`PuraStreamingSession`）、渲染调度/帧预算、块级虚拟化、换肤扩展契约（`IPuraPlugin`、renderer 装饰、`PuraParts`/`PuraClasses`） | `Arc.Avalonia.Markdown`（委托渲染）、`Arc.Avalonia.Chat` / `Arc.Avalonia.Agent`（直接消费类型） |
| **`PuraMarkdown.Agent`** | pura-agent 组件（choice/confirm/tool-call）、`PuraChatView`、Testing；依赖 `PuraMarkdown.UI` | `Arc.Avalonia.Chat` / `Arc.Avalonia.Agent`（ADR 0007） |

命名空间迁移：通用 Markdown/主题/流式/平台类型从 `PuraMarkdown.Agent[...]` 迁到
`PuraMarkdown.UI[...]`（`Rendering`/`Localization`/`Platform` 相应变为 `PuraMarkdown.UI.*`）；
Agent 专属类型仍在 `PuraMarkdown.Agent`，Chat 视图在 `PuraMarkdown.Agent.Chat`。

Arc 侧边界（`markdownUiEngine` scope）：

* `PuraMarkdown.UI` 只允许上述三个工程**直接 `PackageReference`**；Theme/Meta/All 等经 Markdown/Chat/Agent
  链条传递获得属正常闭包，Foundation/其它包直接引用即 `markdown-ui-engine-outside-scope` 红项；
* Markdown 包保留 Arc 的 renderer pipeline（`ArcMarkdownPipeline`）、Arc token skin
  （`Themes/ArcMarkdownTheme.axaml`）、宿主控件壳与平台契约；渲染引擎（节点渲染、流式、虚拟化、调度）交给上游；
* `Directory.Packages.props` 统一锁定 Core/UI/Agent = `0.2.0-alpha.1`，Avalonia 12.1.5 与上游一致；
  `tools/check-package.ps1` 按包放行（Markdown → UI；Chat/Agent → Agent + UI）。

## 2. 包内容

| 组件 | 作用 |
|---|---|
| `ArcMarkdownView` | 文档渲染宿主：Markdown → PuraMarkdown.Core 解析 → PuraMarkdown.UI 渲染引擎 → Arc token skin；流式、大纲、锚点、选择/复制、交互状态仍由 Arc 暴露 |
| `ArcMarkdownDocumentView` | 已解析 `PuraDocument` 的渲染宿主（宿主自己解析/缓存 AST 时使用） |
| `ArcMarkdownCodeBlock` | 围栏代码：语言标签、行号、token 高亮、复制、长代码折叠（上游围栏渲染 + Arc 主题） |
| 节点渲染/流式引擎 | CommonMark + GFM 全节点矩阵与增量流式、渲染调度、块级虚拟化由 `PuraMarkdown.UI` 提供（ADR 0008）；Arc 的 `ArcMarkdownPipeline` 负责注册/装饰渲染器并套 token skin |
| `pura-agent` 组件 | Chat/Agent 侧改由 `PuraMarkdown.Agent` 的 `AgentPlugin`/`PuraToolCallCard`/choice/confirm 承担（ADR 0007）；本包只消费 Core 的 AST/诊断 + UI 的渲染引擎，不直接依赖 Agent 包 |
| 平台契约 | 图片加载、链接打开、剪贴板、本地化都可注入；默认离线安全（`PuraMarkdown.UI.Platform` 类型经 Arc 包装） |
| 主题 | `<arc:ArcTheme/>` 已聚合 `ArcMarkdownTheme.axaml`；所有颜色/字号/圆角走 token（Arc skin 覆盖上游渲染） |

## 3. 公共 API（宿主视角）

```csharp
using Arc.Avalonia.Markdown;

// 启动时构建一次
var pipeline = new ArcMarkdownPipelineBuilder()
    .UseCommonMark()
    .UseGfm()
    .UseCodeHighlighter(ArcMarkdownHighlighter.Default)
    .UsePlugin(new MyFencePlugin())     // 可选
    .Build();

var options = new ArcMarkdownOptions
{
    Pipeline = pipeline,
    ImageLoader = ArcMarkdownPlatform.ImageLoader((url, ct) => LoadAsync(url, ct)),
    // 可选：自定义链接处理（应用内导航、深链、白名单校验）。菜单「在浏览器中打开」优先走它；
    // 未注入时回退 ArcMarkdownPlatform.OpenInBrowserAsync → TopLevel.Launcher → 系统浏览器。
    LinkHandler = ArcMarkdownPlatform.LinkHandler((url, ct) => OpenAsync(url, ct)),
    Localizer = ArcMarkdownLocalizer.Default,   // 中文默认
    EnableTextSelection = true,
};

var view = new ArcMarkdownView { Options = options };
// 点击链接先抛 LinkClicked；宿主置 HandledByHost = true 即接管该次点击（不再弹内置菜单）。
view.LinkClicked += (_, e) => { /* e.HandledByHost = true; */ };

// 历史消息
view.Markdown = message.Text;

// 流式
var session = view.BeginStreaming();
session.Append(delta);            // 任意线程
await session.CompleteAsync();    // 或 AbortAsync 保留部分内容
```

Agent 交互：

```csharp
view.InteractionOccurred += (_, e) =>
{
    // e.ComponentType / e.ComponentId / e.SelectedIds / e.Confirmed
};
view.SetInteractionState(...);    // 持久化/虚拟化恢复
```

### 3.1 链接交互（点击 → 上下文菜单）

行内链接（`[文本](url)` 与自动链接）渲染为**正文文本流里的 accent 色下划线 `Span`**（`ArcMarkdownLinkInline`），
不是内嵌控件。链接与正文共享同一行盒/基线，按段落宽度自然换行，可被选择/复制；点击命中由
`ArcMarkdownLinkInteraction` 对宿主 `TextBlock` 的字符矩形做命中测试（`TextLayout.HitTestTextPosition`），
命中后先抛 `LinkClicked`（宿主可置 `HandledByHost = true` 接管），未被接管时展开 Arc 浮层菜单：

| 菜单项 | 行为 |
|---|---|
| 复制链接 | 把原始 URL 写入 `ArcMarkdownOptions.Clipboard`；默认 no-op 剪贴板时回退 `TopLevel.Clipboard`，成功后行内显示「链接已复制」并在约 0.9 s 后关闭 |
| 在浏览器中打开 | 有宿主 `LinkHandler` 时调用它的 `OpenAsync`（应用内导航等）；否则 `ArcMarkdownPlatform.OpenInBrowserAsync` → `TopLevel.Launcher.LaunchUriAsync`，即系统浏览器/mailto 客户端 |

* 点击/轻触、桌面右键都会先抛 `LinkClicked`：宿主置 `HandledByHost = true` 对该次激活生效，不会旁路宿主的
  链接策略；滚动拖拽（>8 px）由 `ArcMarkdownTapGuard` 取消，滑动不会误开链接。
* 悬停显示手型光标，`[text](url "title")` 的 title 以 tooltip 呈现。
* 链接本身是文本 run，不再持有可聚焦控件：本组件不为每个链接提供键盘焦点/Enter 激活；需要键盘可达性的宿主
  应通过 `LinkClicked` + 自己的 UI 入口承载（这是为修复「链接被抬高/行盒被撑高」而做的权衡，见 docs/30）。
* 纯锚点链接（`#heading`）由 `ArcMarkdownView` 直接 `ScrollToAnchor` 滚动，不弹菜单（浏览器无法解析裸片段）。
* Chat / Agent 消息内的链接（渲染由 `PuraMarkdown.Agent` 负责）沿用同一 scheme 策略与 launcher 行为，但不弹本组件
  的浮层菜单（上游 chat 视图没有可锚定的链接控件/事件）：默认直接打开，宿主用 `ArcChatView.LinkClicked` /
  `LinkHandler` 接管，见 [docs/29](29-puramarkdown-agent-migration.zh.md) §1/§2。Chat 主题还会去掉上游
  `HyperlinkButton` 的按钮 chrome 并把链接对齐到正文基线（外观与本组件的 accent 下划线链接一致）。
* 自动链接除 CommonMark/GFM 的 `<https://…>` 与空白后的 URL 外，还识别**中文/全角标点后紧跟的裸 URL**
  （如 `下载页：https://…`、`详见（https://…）`）：Arc 在默认 pipeline 里注册了 CJK 友好的 autolink 解析器
  （Markdig 的 GFM autolinker 只在空白/行首触发，中文正文会漏链）。尾部标点（。，；：！？等）不会被吞进链接。
* 相对路径链接、以及宿主未注入 `LinkHandler` 时的非 http/https/mailto/tel scheme：「在浏览器中打开」置灰
  （`ToolTip` 提示，`ShowOnDisabled` 已启用）；launcher 不可用或启动失败时菜单不关闭，行内显示「打开链接失败」。
* 菜单文案走 `IArcMarkdownLocalizer`：`markdown.copyLink` / `markdown.linkCopied` / `markdown.openInBrowser` /
  `markdown.openLinkFailed` / `markdown.openLinkUnavailable` / `markdown.linkMenu`，默认中文、`ArcMarkdownEnglishLocalizer` 为英文。
* 失败降级为 warning，不抛出：`ARC-MD-LINK-001`（打开异常）、`002`（宿主点击回调异常）、`003`（菜单创建异常）、
  `004`（复制失败）、`005`（launcher 未能打开）。
* 主题：`Themes/ArcMarkdownTheme.axaml` 为 `FlyoutPresenter.arc-markdown-link-menu` 单独上 Arc 浮层样式
 （`ArcSurfaceRaised` + `ArcBorder` + `ArcRadiusPanel`），不改变宿主其它 Flyout。菜单是 popup 窗口，阴影由
  平台 popup chrome 提供（`FlyoutPresenter` 在 Avalonia 12 没有 `BoxShadow` 属性，内容会被 popup 边界裁剪，
  所以不叠加 `ArcShadowFloating`）。

## 4. 流式策略

* 只提交**稳定边界**（顶层空行、且前一块不是列表/引用/表格/缩进代码等可续接结构）之前的文本；
* 稳定窗口解析一次并增量追加控件，尾部窗口每次 flush 重渲染；
* 未闭合围栏、未完成表格留在尾部窗口，不闪烁、不重复；
* `CompleteAsync` 做一次完整解析并替换文档，保证最终结果与一次性解析一致；
* 自动滚动在用户向上滚动后暂停（`NearBottom` 语义）。

## 5. 设计规则（与 Arc 一致）

* 颜色/字号/圆角/间距全部来自 `ArcResources` token，light/dark + 8 accent 自适应；
* 正文字号 14、标题 28/22/18/16，字重 400/500，不用 Bold 700；
* 代码块使用 `ArcSurfaceMuted` + `ArcBorder` + panel 圆角；表格 1px hairline、表头 muted 背景；
* 链接为 accent 色下划线文本 run（`ArcMarkdownLinkInline`），与正文同一行盒/基线、可换行可选中；点击命中后
  展开「复制链接 / 在浏览器中打开」Arc 浮层菜单（见 §3.1）；纯锚点链接在文档内滚动；
* 无障碍：标题 `AutomationProperties.HeadingLevel`、流式完成播报、图片 alt 占位、代码复制按钮有名称；
* 所有渲染器失败降级为 warning 控件，不允许抛出到宿主。

## 6. 验收

* `dotnet build ArcUI.slnx` 0 error；
* gallery `--audit` / `--smoke` 通过，新增 Markdown 页面在 light/dark 下可渲染；
* `Arc.Avalonia.Verify` 覆盖：全节点矩阵、流式完成=全量、边界切分、Agent 组件交互；链接断言包括「行内链接渲染为
  文本 run（不再产生 `HyperlinkButton`/`InlineUIContainer`）」「CJK 全角标点后的裸 URL 自动链接」；Chat 断言见 docs/22；
* 视觉回归：`.recon/link-baseline-probe`（真实桌面窗口）对 360 dp 手机宽度验证链接字号 16、行盒 = 普通行盒、
  基线偏移 0、长 URL 自然换行；`.recon/link-verify` 保留 before/after 证据（见 [docs/30](30-inline-link-inline-flow.zh.md)）；
* 11 个仓库门禁（`tools/check-all.ps1`）绿；`check-package` 中本包依赖闭包为 `PuraMarkdown.Core` /
  `PuraMarkdown.UI` / `Markdig`（ADR 0008）；`PuraMarkdown.Agent` 仅允许出现在 Chat/Agent 包（ADR 0007）。

## 7. 已知限制

* HTML 不执行（Markdig `DisableHtml`）：按纯文本回退；
* 行内链接是文本 run，不提供「每个链接可 Tab 聚焦、Enter 激活」的键盘交互（这是为修复链接被抬高/行盒被撑高
  所做的权衡，见 [docs/30](30-inline-link-inline-flow.zh.md)）；需要键盘可达性的宿主用 `LinkClicked` + 自己的
  入口承载；
* 行内链接没有逐链接的读屏语义（旧 `HyperlinkButton` 的 `AutomationProperties.Name/HelpText` 随之移除）：读屏按
  普通文本朗读；需要无障碍链接语义的宿主应在 `LinkClicked` 里提供可聚焦入口；
* 启用 `EnableTextSelection` 时可以整体选择/复制、也可以从正文拖选跨过链接，但按下点落在链接上会被当作点击
  （不能从链接文字上起选或双击选词）；默认关闭选择时按普通文本处理；
* 链接菜单锚定在承载链接的 `TextBlock`（整段）下方，而不是链接字符处：Avalonia 12 的 `Flyout` 没有按点定位 API，
  长段落里菜单会出现在段落下沿（弹层会自动保证不出屏）。链接仍以字符矩形命中，不会点错；
* CJK 自动链接按「左边界 = 非 ASCII 字母数字」+ 尾标点裁剪的启发式解析：`mailto:`/`ftp:` 等其它 scheme 不自动
  链接（仍可用 `[text](url)`）；URL 紧贴 ASCII 单词（如 `myhttps://…`）不视为链接；URL **后面紧贴中文**（无标点/
  空白）时中文会被并入 URL（与 GFM autolink 行为一致，需宿主保证 `URL + 空格/标点`）；无点且非 `localhost` 的主机
  （如 `http://intranet/`）不自动链接；
* 未安装自定义围栏插件时，`pura-agent` / 图表等围栏回退为代码块（与 PuraMarkdown 行为一致）；
* 图片默认离线拒绝，宿主必须注入 `ImageLoader` 才会加载；
* 链接打开默认走系统浏览器（`TopLevel.Launcher`）：Android/iOS 由平台 launcher 接管；无 launcher 的宿主
  （离屏/无窗口）只会发 warning `ARC-MD-LINK-005`。相对路径链接必须由宿主 `LinkHandler` 处理，否则
  「在浏览器中打开」禁用；
* **块级虚拟化**：PuraMarkdown.UI 提供块级虚拟化能力（`PuraMarkdownOptions.BlockVirtualization`），是否开启由
  Arc 侧 `ArcMarkdownOptions`/`ArcMarkdownView` 的迁移结果决定；未开启时 `ArcMarkdownView` 仍一次性实例化整篇
  文档的块，超长内容请拆页，或使用 `Arc.Avalonia.Chat.ArcChatView` 的消息级虚拟化（1000 条消息只实例化约 10 行）。



