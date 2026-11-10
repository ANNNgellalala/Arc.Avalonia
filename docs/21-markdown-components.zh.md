# 21 · Markdown 组件族（PuraMarkdown 语法 + Arc 渲染）

> 状态：M3 波次实现记录。目标：把 PuraMarkdown 的解析/流式/Agent 语法能力接到 Arc 设计系统里，
> 由 Arc 提供**生产级**的 Markdown 渲染控件（PuraMarkdown 自带的 Avalonia 渲染只作为 Demo）。

---

## 1. 边界与依赖

```text
PuraMarkdown.Core (独立包，Markdig 1.5.1，无 Avalonia)
  PuraSyntaxBuilder / PuraSyntaxPipeline
  PuraDocument = MarkdownDocument + Version + Diagnostics + Outline
  ParseIncremental / StreamingBlockScanner
        │ Markdig AST + 诊断/大纲
        ▼
Arc.Avalonia.Markdown (L2 组件族)
  ArcMarkdownView / ArcMarkdownDocumentView / ArcMarkdownCodeBlock
  Arc 节点渲染器（块/行内/围栏）+ 代码高亮 + 流式会话 + 本地化 + 图片/链接平台契约
        │
        ▼
Arc.Avalonia.Chat (L2)  /  Arc.Avalonia.Agent (L4)
  Chat 用 ArcMarkdownView 渲染气泡正文；Agent 用 pura-agent 围栏渲染 choice/confirm，并按 Timeline 组合气泡与事件卡
```

* **解析/诊断/大纲/流式边界**归 PuraMarkdown.Core；Arc 不复制 Markdig 节点，也不重新实现解析。
* **所有 UI**（控件、主题、token、交互、无障碍、本地化）归 Arc。没有引用 PuraMarkdown 的 Avalonia 渲染包。
* 依赖例外：`PuraMarkdown.Core` 与 `Markdig` 只允许出现在 `Arc.Avalonia.Markdown`（ADR 0006）；
  其它 Arc 包的依赖白名单不变。渲染层没有第三方 UI 库。
* 本地构建：`PuraMarkdown.Core` 来自 sibling checkout 的本地包源（`NuGet.config` 里的
  `../PuraMarkdown/artifacts/nuget`），首次构建前在 PuraMarkdown 仓库执行
  `dotnet pack src/PuraMarkdown.Core -c Release -o artifacts/nuget`。

## 1.1 上游包改名（2026-11-10，PuraMarkdown ADR-0028）

PuraMarkdown 把 3 个包改名为职责对齐的 id，并同步对齐命名空间（**不保留旧包 id / 旧命名空间**）：

| 旧 | 新 | Arc 是否使用 |
| --- | --- | --- |
| `PuraMarkdown.Syntax` | **`PuraMarkdown.Core`**（Markdig 解析、`PuraDocument`、大纲、诊断、流式扫描；无 Avalonia） | ✅ 唯一使用 |
| `PuraMarkdown`（UI 控件） | `PuraMarkdown.Agent`（Avalonia 渲染控件 + Agent/Chat/Testing） | ⚠️ 部分使用：仅 Chat/Agent 与 Agent 迁移委托（ADR 0007）；Markdown 包仍不使用 |
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
* 本包（`Arc.Avalonia.Markdown`）与 Foundation/其它 L2/L3 包仍只允许 `PuraMarkdown.Core`（+ `Markdig`），
  不随 Chat/Agent 迁移改变渲染实现；
* 传递依赖 `Avalonia.Controls.ItemsRepeater`、`JsonSchema.Net`（→ `JsonPointer.Net` / `Json.More.Net` /
  `Humanizer.Core`）只在经此例外路径进入闭包，`tools/check-deps.ps1` 的 `agentChatBridge` scope 会拒绝越界包；
* 许可证与用途登记在 [`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md)。

Chat/Agent 的目标 API、删除清单与兼容性见 [docs/29](29-puramarkdown-agent-migration.zh.md) §2–§5。

## 2. 包内容

| 组件 | 作用 |
|---|---|
| `ArcMarkdownView` | 文档渲染宿主：Markdown → 解析 → Arc 控件树；流式、大纲、锚点、选择/复制、交互状态 |
| `ArcMarkdownDocumentView` | 已解析 `PuraDocument` 的渲染宿主（宿主自己解析/缓存 AST 时使用） |
| `ArcMarkdownCodeBlock` | 围栏代码：语言标签、行号、token 高亮、复制、长代码折叠 |
| 节点渲染器 | CommonMark + GFM 全节点矩阵：heading/paragraph/quote/list/task list/table/code/thematic break/html 回退/强调/行内代码/链接/图片/自动链接/换行 |
| `pura-agent` 组件 | Chat/Agent 侧改由 `PuraMarkdown.Agent` 的 `AgentPlugin`/`PuraToolCallCard`/choice/confirm 承担（ADR 0007）；本包只消费 Core 的 AST/诊断，不依赖该 UI 库 |
| 平台契约 | 图片加载、链接打开、剪贴板、本地化都可注入；默认离线安全 |
| 主题 | `<arc:ArcTheme/>` 已聚合 `ArcMarkdownTheme.axaml`；所有颜色/字号/圆角走 token |

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

行内链接（`[文本](url)` 与自动链接）渲染为 accent 色下划线的 `HyperlinkButton`。点击后先抛
`LinkClicked`（宿主可置 `HandledByHost = true` 接管），未被接管时在链接下方展开 Arc 浮动菜单：

| 菜单项 | 行为 |
|---|---|
| 复制链接 | 把原始 URL 写入 `ArcMarkdownOptions.Clipboard`；默认 no-op 剪贴板时回退 `TopLevel.Clipboard`，成功后行内显示「链接已复制」并在约 0.9 s 后关闭 |
| 在浏览器中打开 | 有宿主 `LinkHandler` 时调用它的 `OpenAsync`（应用内导航等）；否则 `ArcMarkdownPlatform.OpenInBrowserAsync` → `TopLevel.Launcher.LaunchUriAsync`，即系统浏览器/mailto 客户端 |

* 桌面右键与键盘（链接聚焦后 Enter/Space）打开同一菜单，且都先抛 `LinkClicked`：宿主置
  `HandledByHost = true` 对该次激活生效（含右键），不会旁路宿主的链接策略。
* 菜单内 ↑/↓ 移动、Enter/Space 选择、Esc 关闭；关闭后焦点回到链接；触屏目标取 `ArcHitTargetMin`（≥44 px）。
* 纯锚点链接（`#heading`）由 `ArcMarkdownView` 直接 `ScrollToAnchor` 滚动，不弹菜单（浏览器无法解析裸片段）。
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
* 链接为 accent 色下划线，点击展开「复制链接 / 在浏览器中打开」Arc 浮层菜单（见 §3.1），`:focus-visible`
  由 ArcButton/Hyperlink 主题接管；纯锚点链接在文档内滚动；
* 无障碍：标题 `AutomationProperties.HeadingLevel`、流式完成播报、图片 alt 占位、代码复制按钮有名称；
* 所有渲染器失败降级为 warning 控件，不允许抛出到宿主。

## 6. 验收

* `dotnet build ArcUI.slnx` 0 error；
* gallery `--audit` / `--smoke` 通过，新增 Markdown 页面在 light/dark 下可渲染；
* `Arc.Avalonia.Verify` 覆盖：全节点矩阵、流式完成=全量、边界切分、Agent 组件交互；Chat 断言见 docs/22；
* 11 个仓库门禁（`tools/check-all.ps1`）绿；`check-package` 中本包依赖闭包只含 `PuraMarkdown.Core` / `Markdig`，`PuraMarkdown.Agent` 仅允许出现在 Chat/Agent 包（ADR 0007）。

## 7. 已知限制

* HTML 不执行（Markdig `DisableHtml`）：按纯文本回退；
* 未安装自定义围栏插件时，`pura-agent` / 图表等围栏回退为代码块（与 PuraMarkdown 行为一致）；
* 图片默认离线拒绝，宿主必须注入 `ImageLoader` 才会加载；
* 链接打开默认走系统浏览器（`TopLevel.Launcher`）：Android/iOS 由平台 launcher 接管；无 launcher 的宿主
  （离屏/无窗口）只会发 warning `ARC-MD-LINK-005`。相对路径链接必须由宿主 `LinkHandler` 处理，否则
  「在浏览器中打开」禁用；
* **块级虚拟化不在本包范围**：`ArcMarkdownView` 一次性实例化整篇文档的块；超长内容请拆页，或使用
  `Arc.Avalonia.Chat.ArcChatView` 的消息级虚拟化（L1 `ArcVirtualList`，1000 条消息只实例化约 10 行）。



