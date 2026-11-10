# ADR 0007：Agent/Chat 渲染委托 PuraMarkdown.Agent（有范围第三方 UI 例外）

- 状态：Accepted（M3 Agent/Chat 迁移，2026-11-11）
- 关联：[docs/29 §4](../29-puramarkdown-agent-migration.zh.md)、[docs/21](../21-markdown-components.zh.md)、[docs/22](../22-chat-agent-refactor.zh.md)、[ADR 0006](0006-markdown-puramarkdown-dependency.md)
- 工具：`tools/deps-policy.json`、`tools/check-deps.ps1`、`tools/check-package.ps1`、`THIRD-PARTY-NOTICES.md`

## 背景

`docs/13` 的 R3 原则禁止第三方 UI 组件库，只允许 Avalonia 官方包；Markdown 组件族在 ADR 0006 中已获准
使用 `PuraMarkdown.Core`（解析层，无 Avalonia 依赖）。用户随后明确要求 **Agent/Chat 的渲染不再自研**，
改用上游 `PuraMarkdown.Agent`（Avalonia 渲染控件 + Chat + Agent 组件 + Testing，`0.1.0-alpha.1`）。
这与“禁止第三方 UI 组件库”的字面原则冲突，因此需要一条**有范围、可审计**的例外，而不是放松全局策略。

## 决策

1. **渲染委托**：`Arc.Avalonia.Chat` 与 `Arc.Avalonia.Agent` 的聊天/Agent 正文、消息气泡、工具调用卡片、
   choice/confirm 组件、流式渲染统一委托 `PuraMarkdown.Agent`；Arc 不再自研同层渲染器。
2. **Arc 保留的能力**：Agent 表现运行时（事件 reducer/workspace/session/审批队列/artifact）、布局壳、
   composer 输入框、审批托盘、注意力收件箱、主题 token 映射（`ArcChatThemeAdapter` → `PuraTheme`）、
   本地化/无障碍包装。迁移后的边界与目标 API 见 docs/29 §1–§2。
3. **范围例外（唯一允许点）**：
   * `PuraMarkdown.Agent` 只允许 `Arc.Avalonia.Chat`、`Arc.Avalonia.Agent` **直接引用**；
   * `Arc.Avalonia.Markdown` 仍只允许 `PuraMarkdown.Core`（+ `Markdig`），不得引用 `PuraMarkdown.Agent`；
   * Foundation、其它 L2/L3/L4 包与 meta 包不得**直接**引用该 UI 库；Theme/Meta/All 等聚合包经
     Chat/Agent 链条间接获得它属正常传递闭包（`allowedTransitive` 放行）；
   * 传递依赖（仅经此例外路径）白名单：`Avalonia.Controls.ItemsRepeater`、`JsonSchema.Net`、
     `JsonPointer.Net`、`Json.More.Net`、`Humanizer.Core`；deny 列表与 fail-closed 默认不变。
4. **落地方式**：
   * `tools/deps-policy.json`：`PuraMarkdown.Agent` 以 `scope: agentChatBridge` + `allowedProjects`
     登记（不是全局 `libraries` 白名单）；`check-deps.ps1` 只对 csproj 的**直接 PackageReference**判定，
     越界即 `agent-chat-bridge-outside-scope` 红项；传递依赖走 `allowedTransitive`；
   * `tools/check-package.ps1`：`PuraMarkdown.Agent` 只通过 `$allowedExternalByPackage` 放行 Chat/Agent
     两个包；Markdown 包仍只允许 Core/Markdig；
   * `THIRD-PARTY-NOTICES.md` 登记许可证/用途；`docs/21` §1.2 记录组件族边界的变化。
5. 版本：Avalonia 固定 12.1.5（与上游一致）；`PuraMarkdown.Agent` 版本由
   `Directory.Packages.props` 统一锁定，升级需同步核对上游依赖矩阵。

## 替代方案与否决理由

| 方案 | 结论 | 理由 |
|---|---|---|
| 继续自研 Chat/Agent 渲染 | ❌ 否决 | 与上游重复维护；流式、工具卡片、JSON 协议、Testing 均需长期追平，功能与稳定性落后 |
| Fork / vendor 上游源码进 Arc | ❌ 否决 | 许可证虽允许，但 fork 后升级/安全修复成本高，且仍要维护 UI 渲染；违背“不做渲染层自研”的边界 |
| 直接引用整个 PuraMarkdown 的产品 UI 包并替换 Arc 设计系统 | ❌ 否决 | 会破坏 Arc 的主题/无障碍/设计一致性；本次只委托 Chat/Agent 渲染，Arc 保留设计系统与宿主壳 |
| A（本 ADR）：有范围例外 + 工具级强制 | ✅ 采用 | 用户明确要求、边界可静态审计（Chat/Agent 白名单）、deny 默认不变、第三方清单透明 |

## 后果

* **依赖**：Chat/Agent 包将声明 `PuraMarkdown.Agent`；消费者经 Chat/Agent 间接获得
  `Avalonia.Controls.ItemsRepeater`、`JsonSchema.Net`（→ `JsonPointer.Net`/`Json.More.Net`/`Humanizer.Core`）；
  Markdown 包仍只有 `PuraMarkdown.Core`/`Markdig`；
* **包体积/构建**：Chat/Agent 包与依赖闭包增大；Avalonia 需与上游 12.1.5 对齐；
* **API**：`ArcChatMessage` → `PuraChatMessage` 等为 **breaking**（docs/29 §5）；`ArcChatThread` 移除；
* **许可证**：新增 MIT（PuraMarkdown.Agent）、MIT（ItemsRepeater/JsonSchema.Net 系）与 Humanizer 的
  MIT/Apache（以 nuspec 为准）；`THIRD-PARTY-NOTICES.md` 必须随发布物打包（R5 校验）；
* **治理**：若未来上游 UI 库引入不受控依赖或变更许可，R3/R5 会失败并阻止发布；撤销本 ADR 即回退到自研边界。

## 验证

```powershell
pwsh -NoProfile tools/check-deps.ps1 -SelfTest    # 4/4 fixtures PASS
pwsh -NoProfile tools/check-deps.ps1             # R3：Chat/Agent 允许 PuraMarkdown.Agent；其它包越界即红
pwsh -NoProfile tools/check-package.ps1          # R5：Chat/Agent nuspec 可声明；Markdown 仅 Core/Markdig
pwsh -NoProfile tools/check-all.ps1              # R1–R12 汇总
```

反例（人工/CI 可复现）：在 `src/Components/Arc.Avalonia.Markdown/*.csproj` 临时加
`PuraMarkdown.Agent` 引用（或让其它包传递引入），`check-deps` 必须报
`agent-chat-bridge-outside-scope` 并 exit 1；在 `check-package` 的 Markdown nupkg 中出现
`PuraMarkdown.Agent` 依赖必须报 `package.dependency`。

## 变更记录

* **2026-11-11**：创建；记录用户要求的 Chat/Agent 渲染迁移与有范围例外，新增
  `agentChatBridge` 策略 scope、Chat/Agent 包级 `check-package` 例外与第三方声明。
