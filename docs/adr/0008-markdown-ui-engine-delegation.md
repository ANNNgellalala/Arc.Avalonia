# ADR 0008：Markdown 组件族委托 PuraMarkdown.UI 渲染引擎（有范围包例外）

- 状态：Accepted（PuraMarkdown 0.2 迁移，2026-11-13）
- 关联：[ADR 0006](0006-markdown-puramarkdown-dependency.md)、[ADR 0007](0007-agent-chat-puramarkdown-bridge.md)、[docs/21 Markdown 组件族](../21-markdown-components.zh.md)、[docs/29 Agent/Chat 迁移](../29-puramarkdown-agent-migration.zh.md)
- 工具：`Directory.Packages.props`、`tools/deps-policy.json`、`tools/check-deps.ps1`、`tools/check-package.ps1`、`THIRD-PARTY-NOTICES.md`

## 背景

PuraMarkdown 0.1 只有一个 Avalonia 渲染包 `PuraMarkdown.Agent`（渲染控件 + Chat + Agent 组件 + Testing 全在一起）。
Arc 在 ADR 0006 中只获准使用无 Avalonia 的 `PuraMarkdown.Core`，`Arc.Avalonia.Markdown` 的渲染全部自研；
在 ADR 0007 中 Chat/Agent 才获准直接引用整个 `PuraMarkdown.Agent`。

PuraMarkdown **0.2** 把这两层拆开（版本 `0.2.0-alpha.1`）：

* **`PuraMarkdown.UI`**：Avalonia Markdown 控件/渲染引擎 —— `PuraMarkdownView`、`PuraDocumentView`、
  `PuraMarkdownOptions`、`PuraTheme`/`PuraColors`/`PuraTypography`、增量流式会话（`PuraStreamingSession`）、
  渲染调度/帧预算、块级虚拟化，以及供组件库换肤的 UI 扩展契约（`IPuraPlugin`、renderer 装饰/覆盖、
  `PuraParts`/`PuraClasses`）；依赖 `PuraMarkdown.Core`、Avalonia、`Avalonia.Controls.ItemsRepeater`、Markdig。
* **`PuraMarkdown.Agent`**：只保留 pura-agent 组件（choice/confirm/tool-call）、`PuraChatView` 与 Testing，
  依赖 `PuraMarkdown.UI`。

命名空间同步迁移：原 `PuraMarkdown.Agent`（根）与 `PuraMarkdown.Agent.Rendering/Localization/Platform`
中的通用 Markdown 类型改为 `PuraMarkdown.UI` / `PuraMarkdown.UI.Rendering` / `PuraMarkdown.UI.Localization` /
`PuraMarkdown.UI.Platform`；Agent 专属类型仍在 `PuraMarkdown.Agent`、Chat 视图在 `PuraMarkdown.Agent.Chat`。

Arc 的项目目标随之调整：Markdown 组件族不再自研渲染层，改为**委托上游渲染引擎**，Arc 保留 token skin
与 renderer pipeline；同时 Chat/Agent 源码需要适配类型命名空间迁移。

## 决策

1. **Markdown 包获得 Core + UI 的引用权**：`Arc.Avalonia.Markdown` 直接引用
   `PuraMarkdown.Core`、`PuraMarkdown.UI`（+ `Markdig`）。`ArcMarkdownView` / `ArcMarkdownDocumentView` /
   `ArcMarkdownCodeBlock` 的宿主 API 不变，内部把正文/节点渲染委托 `PuraMarkdown.UI` 的 `PuraMarkdownView`
   与渲染引擎；Arc 保留：
   * `ArcMarkdownPipeline`/renderer pipeline 入口与扩展点；
   * Arc token skin（`Themes/ArcMarkdownTheme.axaml`）、`ArcResources` token 读取（light/dark + 8 accent）；
   * 控件壳、链接/图片/剪贴板平台契约、本地化、warning 降级与无障碍包装；
   * 流式会话的外部契约（`ArcMarkdownStreamingSession`）保持向后兼容。
2. **有范围例外（`markdownUiEngine`）**：`PuraMarkdown.UI` 只允许以下三个工程 **直接 `PackageReference`**：
   * `src/Components/Arc.Avalonia.Markdown`（委托渲染）；
   * `src/Components/Arc.Avalonia.Chat` 与 `src/Agent/Arc.Avalonia.Agent`（0.2 命名空间迁移后直接使用
     `PuraMarkdownOptions`/`PuraTheme`/`PuraStreamingSession`/`PuraInteractionEventArgs`/`ILinkHandler` 等类型，
     因此显式引用以便审计，而不是依赖偶然的传递引用）。
   Theme/Meta/All 等聚合包经 Markdown/Chat/Agent 链条**间接**获得 `PuraMarkdown.UI` 属正常传递闭包；
   Foundation 与其它 L2/L3/L4 包不得直接引用。deny 列表、fail-closed 默认与 `unknownAvalonia: deny` 均不变。
3. **版本锁定**：`PuraMarkdown.Core`/`PuraMarkdown.UI`/`PuraMarkdown.Agent` 统一 `0.2.0-alpha.1`，集中在
   `Directory.Packages.props`；Avalonia 固定 12.1.5（与上游一致），`Avalonia.Controls.ItemsRepeater` 12.0.0
   由上游 nuspec 引入。升级三个包必须同步（版本矩阵由上游锁定）。
4. **工具落地**：
   * `tools/deps-policy.json`：新增 `PuraMarkdown.UI` 的 `scope: markdownUiEngine` + `allowedProjects`；
     `allowedDirect` 保留 `PuraMarkdown.Core`/`Markdig`；`allowedTransitive` 无需新增（UI 的依赖
     Avalonia/ItemsRepeater/Markdig/Core 均已在白名单）；
   * `tools/check-deps.ps1`：新增 `markdownUiEngine` 分支，只对 csproj 的**直接 PackageReference**判定，
     越界报 `markdown-ui-engine-outside-scope`；传递闭包放行并单独归类；
   * `tools/check-package.ps1`：`$allowedExternalByPackage` 中 Markdown 允许 `PuraMarkdown.UI`，
     Chat/Agent 允许 `PuraMarkdown.Agent` + `PuraMarkdown.UI`；
   * `THIRD-PARTY-NOTICES.md` 登记 `PuraMarkdown.UI`（MIT：Markdown 控件/渲染引擎/UI 扩展契约）。
5. **文档**：ADR 0006 追加 0.2 变更记录；`docs/21` 组件族边界、README 组件表/依赖说明同步更新；
   `docs/adr/README.md` 建立 ADR 索引。

## 替代方案与否决理由

| 方案 | 结论 | 理由 |
|---|---|---|
| 继续自研 Markdown 渲染（维持 ADR 0006 的实体） | ❌ 否决 | 上游 0.2 已把控件/渲染引擎/流式/虚拟化做成一等包，继续自研等于长期维护两套渲染与流式实现 |
| Fork/vendor `PuraMarkdown.UI` 源码 | ❌ 否决 | 上游迭代快，fork 后升级/安全修复成本高；许可证允许但不经济 |
| 用上游默认主题整体替换 Arc 设计系统 | ❌ 否决 | 破坏 Arc token/accent/无障碍一致性；本次只委托渲染引擎，skin 仍归 Arc |
| 不登记策略、靠传递引用“顺手”使用 UI | ❌ 否决 | Chat/Agent 直接使用 UI 类型却无直接引用，升级/裁剪时容易断；策略不可审计 |
| A（本 ADR）：Core+UI 有范围例外 + 渲染委托 + Arc 保留 skin/pipeline | ✅ 采用 | 与上游演进一致，边界静态可审计（`markdownUiEngine` 白名单），Arc 设计系统与宿主 API 不变 |

## 后果

* **依赖**：`Arc.Avalonia.Markdown` 包 nuspec 将声明 `PuraMarkdown.Core`、`PuraMarkdown.UI`、`Markdig`；
  Chat/Agent 声明 `PuraMarkdown.Agent` + `PuraMarkdown.UI`（+ 传递的 ItemsRepeater/JsonSchema.Net 链）；
* **包体积/构建**：Markdown 包与闭包增大（ItemsRepeater、上游控件 DLL），构建需要 sibling 本地 feed 提供
  三个 0.2.0-alpha.1 包；
* **API**：Arc 侧控件/选项类型不变（`ArcMarkdownView`/`ArcMarkdownDocumentView`/`ArcMarkdownOptions` 等保持
  Arc 命名空间）；上游命名空间迁移只影响 Arc 内部 using 与 `ArcChatThemeAdapter`/`ArcAgentConversation` 等
  适配文件；包版本仍 lockstep `0.9.0-alpha`；
* **许可证**：新增 MIT（`PuraMarkdown.UI`，上游控件/渲染引擎/UI 扩展契约），传递依赖许可证与 ADR 0007 相同；
  `THIRD-PARTY-NOTICES.md` 随发布物打包（R5 校验）；
* **治理**：若上游 UI 引擎引入不受控依赖/变更许可，R3/R5 失败并阻止发布；撤销本 ADR 即回退到 ADR 0006
  的自研渲染边界。

## 验证

```powershell
pwsh -NoProfile tools/check-deps.ps1             # R3：markdownUiEngine 只放行 Markdown/Chat/Agent 直接引用
pwsh -NoProfile tools/check-deps.ps1 -SelfTest   # 负例夹具仍全绿（门禁不会永远绿）
pwsh -NoProfile tools/check-package.ps1          # R5：Markdown 允许 UI；Chat/Agent 允许 Agent+UI
dotnet run --project tests/Arc.Avalonia.Tests -c Debug   # VERIFY: checks=190 failures=0（基线不回归）
```

反例（人工/CI 可复现）：在 `src/Components/Arc.Avalonia.Foundation/*.csproj` 临时加
`<PackageReference Include="PuraMarkdown.UI" />`，`check-deps` 必须报 `markdown-ui-engine-outside-scope`
并 exit 1；在 `check-package` 的 Foundation/其它包 nupkg 中出现 `PuraMarkdown.UI` 依赖必须报
`package.dependency`。

## 变更记录

* **2026-11-13**：创建。记录 PuraMarkdown 0.2 的 Core/UI/Agent 拆分与 Arc Markdown 组件族的委托渲染决策；
  新增 `markdownUiEngine` 策略 scope、`check-deps` 直接引用校验、`check-package` 包级放行与第三方声明。
