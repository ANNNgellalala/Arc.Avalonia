# 31 · Arc Markdown 迁移到 PuraMarkdown 0.2（设计系统复用上游引擎的参考实现）

> 日期：2026-11-13。状态：已完成并合入工作树。
> 关联：[ADR 0006](adr/0006-markdown-puramarkdown-dependency.md)、[ADR 0008](adr/0008-markdown-ui-engine-delegation.md)、[docs/21](21-markdown-components.zh.md)、[docs/29](29-puramarkdown-agent-migration.zh.md)。
> 上游迁移手册（宿主/组件库通用）：PuraMarkdown 仓根目录 `MIGRATION.md` 与 `docs/39-0.2.0-UI重构迁移指南.md`。

## 1. 为什么要迁移

PuraMarkdown 0.2 把架构定为"**上游承载解析 + 控件 + 渲染引擎，组件库只做 Skin**"，并拆出
`PuraMarkdown.UI`。迁移后 Arc 不再自研 Markdown 渲染循环/流式会话/布局调度：

- 上游引擎提供：帧预算调度器（`RenderAsync`/`PuraRenderScheduler`）、自动块级虚拟化、流式窗口/预算、增量嫁接、观测；
- Arc 保留：**Arc renderer pipeline**（视觉/交互/插件契约）、**Arc token skin**、公开 API、外层 `ScrollViewer` 与宿主行为。

这样 PuraMarkdown 的性能修复（docs/38 的 13s UI-HANG 方案）对 Arc 自动生效，不再需要双份实现。

## 2. 目标结构

```
ArcMarkdownView (TemplatedControl, PART_Scroll + PART_Host, 公开 API 不变)
  └─ PuraMarkdownView                    ← PuraMarkdown.UI 引擎
       └─ PuraPipeline (ArcMarkdownPipeline.Build() 注入)
            └─ ArcNodeRendererAdapter × N
                 └─ ArcMarkdownRendererRegistry（原有 renderer/fence/fallback）
```

- `ArcMarkdownPipeline` 公开面（`Syntax`/`Renderers`/`Highlighter`/`PluginNames`）保持；`Build()` 把 Arc 全部 node renderer 以高优先级适配为 `IPuraNodeRenderer` 注册进 Pura 引擎；Arc fallback 仍先于 Pura 默认渲染器，因此未知节点行为不变。
- 解析仍是 `PuraMarkdown.Core`（Arc `Syntax` 不变）；Arc renderer 仍消费 Markdig AST。

## 3. 文件与职责

| 文件 | 职责 |
| --- | --- |
| `Rendering/ArcMarkdownPipeline.cs` | 双管道镜像（Arc `PuraSyntaxBuilder` + Pura `PuraPipelineBuilder`）；`Build()` 注册 `ArcNodeRendererAdapter`（优先级 `10000-index`，高于 Pura 默认 0）；暴露 internal `PuraPipeline` |
| `Rendering/ArcMarkdownPuraBridge.cs` | `ArcMarkdownHostState`（ArcMarkdownOptions/Pipeline/View）；`ConditionalWeakTable<PuraMarkdownOptions, host>` 注册；`ContextFor(Pura RenderContext)` → Arc `ArcMarkdownRenderContext`（Sanitized/Dispatcher/IsStreaming/IsTail/sinks）；`ArcNodeRendererAdapter` |
| `Rendering/ArcMarkdownPuraOptions.cs` | `ArcMarkdownOptions` → `PuraMarkdownOptions`（Pipeline/Theme/Clipboard/LinkHandler/Localizer/Limits/UrlPolicy/InteractionEnabled/EnableTextSelection；`BlockVirtualization=Never`；`RenderOptions.Mode=Auto`）；三个平台适配器 |
| `Rendering/ArcMarkdownPuraTheme.cs` | `ArcMarkdownStyle` → `PuraTheme`（Colors/Typography/Spacing/Radii/CodeTheme；非 Solid brush fallback），供 Pura 默认 renderer/fallback 路径保持 Arc 外观 |
| `Controls/ArcMarkdownView.cs` | 包装 `PuraMarkdownView`：Markdown/Options→引擎；Blocks/Outline/Diagnostics/GetPlainText/GetOutline 走引擎或存储文档；LinkClicked/Warning/InteractionOccurred 回传；BeginStreaming 返回 Arc 包装会话；外层 ScrollViewer/Prewarm/CopyAllAsync/InteractionStates/Announce/Dispose |
| `Streaming/ArcMarkdownStreamingSession.cs` | 包装 `PuraStreamingSession`：State/CurrentText/PendingChars/Statistics/Append/AppendAsync/FlushAsync/CompleteAsync/AbortAsync/DisposeAsync 映射；完成/取消时回写 Arc 文档与滚动 |
| `ArcMarkdownDocumentView.cs` | 不变（继续委托 ArcMarkdownView） |

## 4. 关键适配代码（示意）

```csharp
// ArcMarkdownPipeline.Build()
var registry = new ArcMarkdownRendererRegistry(nodes, fences, fallback);
var pipeline = new ArcMarkdownPipeline(_syntax.Build(), registry, _highlighter, pluginNames);
var ordered = nodes.OrderByDescending(entry => entry.Item2).ToList();
for (var i = 0; i < ordered.Count; i++)
    _pura.UseRenderer(new ArcNodeRendererAdapter(registry, ordered[i].Item1, pipeline), 10_000 - i);
pipeline.SetPuraPipeline(_pura.Build());

// ArcMarkdownView.Options 变化
var state = new ArcMarkdownHostState { Options = arc, Pipeline = arc.Pipeline, View = this };
_engine.Options = ArcMarkdownPuraOptions.Create(arc, arc.Pipeline, state);

// 渲染上下文
ArcNodeRendererAdapter.Render(node, puraCtx)
    => inner.Render(node, ArcMarkdownPuraBridge.ContextFor(puraCtx, pipeline, registry));
```

## 5. 依赖与门禁

- `Directory.Packages.props`：Core/Agent → `0.2.0-alpha.1`，新增 `PuraMarkdown.UI 0.2.0-alpha.1`。
- `Arc.Avalonia.Markdown` 依赖：Foundation + Avalonia + Markdig + `PuraMarkdown.Core` + `PuraMarkdown.UI`。
- `tools/deps-policy.json`：`PuraMarkdown.UI` 为 `markdownUiEngine` 有范围例外（Markdown/Chat/Agent 直接可引；其它包走传递、fail-closed）。
- `tools/check-deps.ps1` 新增 scope 分支；`check-agent-boundary.ps1` 放行 `PuraMarkdown.UI`；`check-package.ps1`/`THIRD-PARTY-NOTICES.md` 同步。
- Public API baseline：`Arc.Avalonia.Chat`/`Arc.Avalonia.Agent` 因公共签名中的 upstream 类型从 `PuraMarkdown.Agent.*` → `PuraMarkdown.UI.*` 而更新（**对消费者是 breaking**，已在 ADR-0008 记录）。

## 6. 行为差异/限制（迁移后）

| 项 | 说明 |
| --- | --- |
| 外层滚动 | ArcMarkdownView 保留自己的 `ScrollViewer`，因此 Pura 引擎 `BlockVirtualization=Never`；块级虚拟化收益要在 Pura 上游的"External 宿主滚动虚拟化"落地后接入 |
| 渲染模式 | ArcMarkdownView 使用 Pura 默认 `Auto`：小文档同步、大文档分帧；需要旧同步语义的场景显式 `Render()` |
| Inline renderer | `IArcMarkdownInlineRenderer` 未逐一适配为 `IPuraInlineRenderer`；Arc 顶层块 renderer 自带行内渲染，正常文档不受影响 |
| Streaming `Updated` | Arc 包装的 `Updated` 事件变为 best-effort（上游没有逐窗口回调）；`Completed/Aborted` 行为保留 |
| Chat 完成路径 | 上游仍未提供"流式控件晋升"，Arc Chat 完成时仍会重新渲染一次消息（进度见上游 docs/38 D4） |

## 7. 迁移/回归清单（其他设计系统可复用）

1. 旧 renderer pipeline 保持不动，新增"适配器 → Pura `IPuraNodeRenderer`"层（优先于 Pura 默认 renderer）。
2. 为 `PuraMarkdownOptions` ↔ 宿主 options 建立 `ConditionalWeakTable` 桥，提供 ArcRenderContext（sanitized/dispatcher/streaming/tail/sinks）。
3. 把宿主 token → `PuraTheme` 做完整映射（fallback/默认 renderer 看起来仍像宿主）。
4. 控件包装而非重写：宿主公开 API 不变，内部宿主 `PuraMarkdownView`；流式会话做包装类。
5. 更新依赖策略（有范围例外 + fail-closed 校验）与第三方声明。
6. 验收：编译 0 warning/0 error、原有 verify/gates 全绿、Gallery audit/smoke、pack + package gate、真实长文档/流式冒烟。

## 8. 验证结果（2026-11-13）

```
dotnet build ArcUI.slnx -c Debug -m:1 -nodeReuse:false   → 0 warning / 0 error
dotnet run tests/Arc.Avalonia.Tests -c Debug            → VERIFY: checks=190 failures=0
Gallery --audit                                          → items=294 docs=294 pages=294 errors=0
Gallery --smoke                                          → itemShows=294 pageShows=323 failures=0
tools/check-all.ps1                                      → GATES: green=11/11 red=0
dotnet pack ArcUI.slnx -c Release -o artifacts           → 33 packages；check-package GREEN
```
