# Architecture Decision Records

本目录按时间顺序记录 Arc for Avalonia 的架构决策。每条 ADR 都绑定可执行的仓库门禁
（`tools/check-*.ps1`），决策与工具行为不一致时以门禁为准并回写 ADR。

| ADR | 标题 | 状态 |
|---|---|---|
| [0001](0001-layering.md) | 分层与依赖方向门禁（R1/R2） | Accepted |
| [0002](0002-dependency-policy.md) | 外部依赖策略与依赖门禁（R3，含传递闭包） | Accepted |
| [0003](0003-theme-strategy.md) | 主题聚合入口 `ArcTheme`：XAML 嵌套方案 | Accepted |
| [0004](0004-top-control-keys.md) | Top 内建控件皮肤：Fluent 资源键覆盖 | Accepted |
| [0005](0005-control-contract.md) | 控件契约：TemplatePart 命名与 additive RoutedEvent | Accepted |
| [0006](0006-markdown-puramarkdown-dependency.md) | Markdown 组件族允许依赖 PuraMarkdown 语法层（Core + Markdig） | Accepted（0.2 变更见记录） |
| [0007](0007-agent-chat-puramarkdown-bridge.md) | Agent/Chat 渲染委托 PuraMarkdown.Agent（有范围第三方 UI 例外） | Accepted |
| [0008](0008-markdown-ui-engine-delegation.md) | Markdown 组件族委托 PuraMarkdown.UI 渲染引擎（有范围包例外） | Accepted |

## 约定

* ADR 一旦 Accepted 不再改写决策正文；变更追加到文末「变更记录」，破坏性调整新建 ADR 互链。
* 每条 ADR 的「验证」一节必须列出可执行的 `pwsh -NoProfile tools/*.ps1` 命令或反例注入步骤。
* 依赖类决策必须同步登记 `tools/deps-policy.json`、`tools/check-package.ps1` 与
  [`THIRD-PARTY-NOTICES.md`](../../THIRD-PARTY-NOTICES.md)。
