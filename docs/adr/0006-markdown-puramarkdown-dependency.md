# ADR 0006：Markdown 组件族允许依赖 PuraMarkdown 语法层

* 状态：Accepted（M3 Markdown 波次）
* 关联：[docs/13 分层](../13-framework-layering-and-agent-top.zh.md)、[docs/21 Markdown 组件族](../21-markdown-components.zh.md)、`tools/deps-policy.json`、`tools/check-package.ps1`

## 背景

PuraMarkdown 已经把 Markdown 解析与 Avalonia 渲染解耦：`PuraMarkdown.Syntax` 只依赖 Markdig，
输出 Markdig AST + `PuraDocument`（版本/诊断/大纲）+ 流式稳定边界扫描。它自带的 Avalonia 渲染包
定位为 Demo；生产 UI 由 Arc 实现。Arc 需要完整的 Markdown 渲染组件（文档、代码、表格、图片/链接、
流式、AI Chat、Agent 组件），并保持 Arc 设计系统的一致性。

按 docs/13 的外置依赖策略，发布库只允许 Avalonia 官方包；这条策略的意图是**禁止第三方 UI 组件库**，
而不是禁止所有非 UI 依赖。

## 决策

1. 新增 `Arc.Avalonia.Markdown`（L2 组件族），允许它直接依赖：
   * `PuraMarkdown.Syntax`（MIT，解析/诊断/大纲/流式边界；无 Avalonia 依赖）；
   * `Markdig`（BSD-2-Clause，PuraMarkdown.Syntax 的解析依赖；Arc 渲染器需要 AST 类型）。
2. 其它 Arc 发布库的依赖白名单保持不变；不得引用 PuraMarkdown 的 Avalonia 渲染包 / Agent / Chat 包。
3. Arc 不复制 Markdig 节点、不实现 Markdown 解析；所有控件、主题、交互仍由 Arc 自己实现。
4. `tools/deps-policy.json` 的 `allowedDirect` 增加两个包；`tools/check-package.ps1` 的
   `$allowedExternal` 同步增加；meta 包依赖形状把 `Arc.Avalonia.Markdown` 计入 L2 组件族。
5. 包产物会声明 `PuraMarkdown.Syntax`/`Markdig` 依赖；第三方清单（`THIRD-PARTY-NOTICES.md`）同步登记。

## 理由

* 方向符合 PuraMarkdown 的技术演进（B+：Markdig AST 作为语法契约，Arc 只消费）；
* 避免维护第二套 Markdown 解析器/流式扫描，性能基线由 PuraMarkdown 承担；
* UI 依赖边界仍然干净：渲染、主题、交互、无障碍、本地化全部是 Arc 代码。

## 后果

* `Arc.Avalonia.Markdown` 的消费者会间接获得 Markdig；这是文档协议自身的要求；
* 升级 Markdig 需跟随 PuraMarkdown 的兼容矩阵（`MarkdigCompatibility`，锁定 1.5.1）；
* 如果 PuraMarkdown 后续发布 1.0 稳定版，Arc 侧只需改 `Directory.Packages.props` 的版本号；
* 若未来需要脱离 Markdig（C 阶段语义模型），`ArcMarkdownOptions` 的渲染器契约已经是 Arc 自有类型，
  替换解析层只影响 `ArcMarkdownPipeline` 与 `ArcMarkdownStreamingSession`。
