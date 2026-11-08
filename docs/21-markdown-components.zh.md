# 21 · Markdown 组件族（PuraMarkdown 语法 + Arc 渲染）

> 状态：M3 波次实现记录。目标：把 PuraMarkdown 的解析/流式/Agent 语法能力接到 Arc 设计系统里，
> 由 Arc 提供**生产级**的 Markdown 渲染控件（PuraMarkdown 自带的 Avalonia 渲染只作为 Demo）。

---

## 1. 边界与依赖

```text
PuraMarkdown.Syntax (独立包，Markdig 1.5.1，无 Avalonia)
  PuraSyntaxBuilder / PuraSyntaxPipeline
  PuraDocument = MarkdownDocument + Version + Diagnostics + Outline
  ParseIncremental / StreamingBlockScanner
        │ Markdig AST + 诊断/大纲
        ▼
Arc.Avalonia.Markdown (L2 组件族)
  ArcMarkdownView / ArcMarkdownDocumentView / ArcMarkdownChatView / ArcMarkdownCodeBlock
  Arc 节点渲染器（块/行内/围栏）+ 代码高亮 + 流式会话 + 本地化 + 图片/链接平台契约
        │
        ▼
Arc.Avalonia.Agent (L4)
  pura-agent 围栏的 choice / confirm 组件 → Arc 控件；顺带把 Agent transcript 接上 Markdown
```

* **解析/诊断/大纲/流式边界**归 PuraMarkdown.Syntax；Arc 不复制 Markdig 节点，也不重新实现解析。
* **所有 UI**（控件、主题、token、交互、无障碍、本地化）归 Arc。没有引用 PuraMarkdown 的 Avalonia 渲染包。
* 依赖例外：`PuraMarkdown.Syntax` 与 `Markdig` 只允许出现在 `Arc.Avalonia.Markdown`（ADR 0006）；
  其它 Arc 包的依赖白名单不变。渲染层没有第三方 UI 库。
* 本地构建：`PuraMarkdown.Syntax` 来自 sibling checkout 的本地包源（`NuGet.config` 里的
  `../PuraMarkdown/artifacts/nuget`），首次构建前在 PuraMarkdown 仓库执行
  `dotnet pack src/PuraMarkdown.Syntax -c Release -o artifacts/nuget`。

## 2. 包内容

| 组件 | 作用 |
|---|---|
| `ArcMarkdownView` | 文档渲染宿主：Markdown → 解析 → Arc 控件树；流式、大纲、锚点、选择/复制、交互状态 |
| `ArcMarkdownDocumentView` | 已解析 `PuraDocument` 的渲染宿主（宿主自己解析/缓存 AST 时使用） |
| `ArcMarkdownChatView` | AI Chat 消息列表：消息级虚拟化、流式槽、自动滚动、状态/错误 |
| `ArcMarkdownCodeBlock` | 围栏代码：语言标签、行号、token 高亮、复制、长代码折叠 |
| 节点渲染器 | CommonMark + GFM 全节点矩阵：heading/paragraph/quote/list/task list/table/code/thematic break/html 回退/强调/行内代码/链接/图片/自动链接/换行 |
| `pura-agent` 组件 | `Arc.Avalonia.Agent` 提供 choice/confirm 控件与 JSON 解析，作为 `IArcMarkdownFenceRenderer` 注册 |
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
    LinkHandler = ArcMarkdownPlatform.LinkHandler((url, ct) => OpenAsync(url, ct)),
    Localizer = ArcMarkdownLocalizer.Default,   // 中文默认
    EnableTextSelection = true,
};

var view = new ArcMarkdownView { Options = options };
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
* 链接为 accent 色下划线，`:focus-visible` 由 ArcButton/Hyperlink 主题接管；
* 无障碍：标题 `AutomationProperties.HeadingLevel`、流式完成播报、图片 alt 占位、代码复制按钮有名称；
* 所有渲染器失败降级为 warning 控件，不允许抛出到宿主。

## 6. 验收

* `dotnet build ArcUI.slnx` 0 error；
* gallery `--audit` / `--smoke` 通过，新增 Markdown 页面在 light/dark 下可渲染；
* `Arc.Avalonia.Verify` 覆盖：全节点矩阵、流式完成=全量、边界切分、Agent 组件交互、Chat 增删改流式；
* 9 个仓库门禁（`tools/check-all.ps1`）绿，`check-package` 依赖闭包包含 `PuraMarkdown.Syntax` / `Markdig` 且无其它第三方 UI 库。

## 7. 已知限制

* HTML 不执行（Markdig `DisableHtml`）：按纯文本回退；
* 未安装自定义围栏插件时，`pura-agent` / 图表等围栏回退为代码块（与 PuraMarkdown 行为一致）；
* 图片默认离线拒绝，宿主必须注入 `ImageLoader` 才会加载；
* **块级虚拟化不在本包范围**：`ArcMarkdownView` 一次性实例化整篇文档的块；超长内容请拆页或使用
  `ArcMarkdownChatView` 的消息级虚拟化（L1 `ArcVirtualList`，1000 条消息只实例化约 12 行）。
