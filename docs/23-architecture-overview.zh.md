# 23 · 组件库整体设计（as-built，0.9.0-alpha / 0.10 波次）

> 本文回答"当前这套组件库是怎么设计的"，是**已落地实现**的架构说明；规划与决策演进见 [docs/13](13-framework-layering-and-agent-top.zh.md)（v3.2 计划 + D1–D10）、[docs/20](20-component-inventory.zh.md)（组件清单，M2 快照 + 0.10 增量）、[docs/18](18-control-contract-spec.zh.md)（控件契约规范），逐项依据见 `docs/adr/0001–0006`。
> 数字取自当前 HEAD：33 个发布包、**261 个公开 UI 控件**、965 个公开类型、≈1244 个属性注册、**144 个路由事件**、**165 个 ControlTheme**、294 篇目录文档 / 323 个页面。

---

## 1. 定位与边界

**是什么**：基于官方 Avalonia 12.1.4 的**自包含 UI 组件库 / 设计系统**——对标 AtomUI、Semi.Avalonia、Ursa 的生态位，提供令牌、主题、通用控件、组合块、文档站点和 Agent 顶层 UI。

**不是什么**（每一轮评审都收敛过这些边界）：

| 边界 | 结论 | 原因 |
|---|---|---|
| 不替代 Avalonia | 渲染、平台窗口、输入管线、动画时钟仍是 Avalonia | 自研渲染层收益低、风险高；见 docs/13 §2.0 |
| 不做 drop-in 替代 AtomUI | 生态位对齐，不做 API 兼容承诺 | 用户决策 D-1；能力面不需要 100% 对齐 Ant Design |
| 不做自研帧调度/绘制缓存/主题版本失效协议 | 流式合并走 `Dispatcher`，不自建帧时钟 | 用户决策：渲染层不做（R8 门禁固化） |
| 不做 Agent 运行时 | 顶层只做**表现运行时**（reducer + 视图），网络/模型/工具/定时器全在宿主 | 边界由 BannedApiAnalyzers + R9 门禁固化 |
| 不引入第三方 UI 组件库 | 依赖白名单 fail-closed（仅官方 Avalonia 包） | R3 门禁 + 传递闭包校验 |
| 唯一例外：Markdown 解析层 | `PuraMarkdown.Core` + `Markdig` 只允许出现在 `Arc.Avalonia.Markdown` | ADR 0006：禁止的是第三方 **UI** 库，不是所有非 UI 依赖；UI 仍全部 Arc 实现 |

## 2. 分层架构

```text
L0  官方平台      Avalonia 12.1.4 / Avalonia.Themes.Fluent / Avalonia.Fonts.Inter
                 (Avalonia.Controls.DataGrid 仅 L2f Feature 包；PuraMarkdown.Core 仅 Markdown 包)
 │
L1  Foundation    Arc.Avalonia.Foundation（程序集名；csproj 路径仍 Arc.Avalonia）
     OKLCH 颜色引擎 / 82 token / 主题管理器 / 264 图标 / 动效 / 5 个原子控件 + ArcVirtualList
 │
L2  通用 UI       13 个组件族（Inputs / Actions / Navigation / Overlays / Feedback / DataDisplay /
                 Charts / Pickers / Effects / Content / Media / Markdown / Chat）+ Services + Feature.DataGrid
 │
L2.5 Theme        Arc.Avalonia.Theme：唯一聚合例外层，一行 <arc:ArcTheme/> 装配全系统
 │
L3  组合层        Special.*（5 包，55 个特殊组件）+ Blocks.*（6 包，111 个页面级 block）
 │
L4  Agent 顶层    Agent（表现运行时 + 组合控件）/ AgentUI（叶子控件）/ LifeToday
 │
L5  宿主/叶子     Docs（文档站点，320+ 页）/ Docs.Abstractions（页面契约）/ Gallery / samples / tests
```

**依赖规则（R1 强约束）**：L1→仅 L0；L2/L2f/L2.5/L3→L1+L2；L4→L1/L2/L3 + 同层兄弟（Agent→AgentUI 显式登记）；app→任意；层级白名单写在 `tools/architecture-allow.json`，例外必须逐条登记并写理由。

**关键不变量**：

* **下行依赖**：不存在反向边（L2 不引用 Docs/Theme/L3/L4）；
* **叶子工程**：`src/Docs/**`、`src/Apps/**`、`tests/**` 是叶子（R2），发布库不得引用 Demo/页面/EmbeddedResource 文档；
* **渲染层零代码**：`FrameClock|RenderTimer|DrawingCache|ThemeVersion|CompositionTarget.Rendering` 不得出现在 `src`（R8，allowlist 为空）；
* **token-only**：不得硬编码颜色（R6，28 条历史豁免在 `tools/token-allow.json`）。

## 3. 包拓扑（33 个，lockstep `0.9.0-alpha`，2.22 MB）

| 层 | 包 | 数量 |
|---|---|---:|
| L1 | `Foundation` | 1 |
| L2.5 | `Theme` | 1 |
| L2 | `Inputs` `Actions` `Navigation` `Overlays` `Feedback` `DataDisplay` `Charts` `Pickers` `Effects` `Content` `Media` `Markdown` `Chat` `Services` | 14 |
| L2f | `Feature.DataGrid` | 1 |
| L3 | `Special.{Glass,Motion,Media,AIWidgets,AIExplainers}`、`Blocks.{Marketing,Commerce,App,Team,Data,AI}` | 11 |
| L4 | `Agent` `AgentUI` `LifeToday` | 3 |
| 元包 | `Arc.Avalonia`（Theme + 全部 L2 组件族 + Agent 三件套）、`Arc.Avalonia.All`（+ Special + Blocks） | 2 |

* 版本 lockstep：`VersionPrefix 0.9.0` + `VersionSuffix alpha`，所有包同版本发布（可用 `-p:Version=` 覆盖）。
* 每个包：`IsPackable=true` + XML 文档 + `LICENSE`/`README`/`THIRD-PARTY-NOTICES` + `Description`；`check-package` 解包校验（无 Pages/Docs/演示资源、依赖闭包白名单、33/33）。
* 元包不含 Feature 包（大数据能力按需安装），也不含 Docs/Gallery/samples。

## 4. 主题与令牌系统

```text
<Application.Styles>
  <arc:ArcTheme/>     ← 唯一接入点（M1-T3 起）
    ├─ FluentTheme（代码创建，R4 禁止 axaml 里出现 <FluentTheme/>）
    ├─ Arc token ThemeDictionaries（Light + Dark，82 token）
    ├─ 47 个 Fluent 资源键覆盖（ADR 0004，46/47 纯键覆盖，0 模板替换）
    ├─ 13 个组件族主题 StyleInclude（165 个 ControlTheme）
    └─ 8 个 accent 的 ColorPaletteResources → FluentTheme.Palettes
```

* **令牌**：82 个 token（颜色/字号/间距/圆角/阴影/时长/字体），全部由 OKLCH 定义、sRGB 落地；axaml 用 `DynamicResource`，C# 用 `ArcResources.Brush/Color/Double/Radius/Shadows/Duration/Font`。**禁止无变体 `Application.FindResource`**（R10.11，见 §10 的透明窗口事故）。
* **主题切换**：`ArcThemeManager.Apply(app, ArcThemeOptions)`；token 只换变体不重建；accent/density/radius 变化才重建字典。
* **自定义品牌色**：`ArcThemeOptions.AccentColor = 种子色` → OKLCH 派生整套 palette（保 hue、chroma 夹 0–0.30、前景按 WCAG≥4.5 选黑白），同时驱动 Fluent `SystemAccentColor`。
* **密度**：`ArcDensity.Compact/Default/Comfortable` 缩放尺寸 token（`ControlHeightMd` 44→39/50）与 Fluent 输入内边距；**圆角**：`CornerRadiusScale`（pill 除外）。
* **图标**：264 个 Lucide path（stroke 1.75 / 24 网格），`ArcIcon.Register/IArcIconProvider` 运行时可注册，几何缓存按 key 失效。
* **护栏**：`--theme-report` 断言 47/47 Fluent 键、accent→SystemAccentColor 一致、极值种子对比度、density、以及 `renderPath` 的 alpha/Inter 字体（防止"取不到 token 回退 Transparent"回归）。

## 5. 控件契约体系

10+1 条契约（R10 静态门禁 + [docs/18](18-control-contract-spec.zh.md) 对外规范，[ADR 0005](docs/adr/0005-control-contract.md) 记录决策）：

| 契约 | 现状 |
|---|---|
| 模板部件 `[TemplatePart("PART_X", typeof(T))]` + `PART_*` 命名 | **114/114** 模板控件 100% 覆盖；`x:Name`/`#选择器` 同步；读取一律 `NameScope.Find`（含视觉树兜底） |
| 交互事件：保留 CLR 事件 + 新增可冒泡 `RoutedEvent`（严格 additive 双通知） | **144 个** RoutedEvent（R10 在 260 个契约源文件内统计 141 个字段，全部带 CLR 包装）；可路由 legacy 事件 102/122 已覆盖（余 20 个集中在新 Chat/Markdown/Conversation，见 §11） |
| 非可路由类（服务/模型/协调器）不强行路由 | 6 个 `nonRoutableEvents` 单列统计，不计缺陷 |
| 主题：每个控件有 ControlTheme、只用 token、支持明暗/8 accent | 165 个 ControlTheme；Charts 20 个自绘控件也已主题化 |
| 尺寸：`*Size`/`*Height` token 化 | 部分组件内部仍有硬编码尺寸（缺口） |
| 焦点/键盘 | `:focus-visible` 50 处、`IsTabStop` 66、`FocusAdorner` 19；无 `AutomationPeer`/`AutomationId`/`TabIndex`（缺口，M3） |

## 6. 服务层与窗口壳（L2 `Services`）

* **无 DI 容器**：`ArcServiceProvider`（静态注册/查找）+ `ArcWindow.Services`（窗口级门面，`EnsureRegistered` 不覆盖应用先注册的实现）。
* `IDialogService`（串行队列 + 焦点陷阱 + Esc/backdrop + 焦点恢复）、`INotificationService`（按 Key 去重 + 生命周期）、`INavigationService`（最小栈）、`IFilePickerService`（默认 `TopLevel.StorageProvider`，可注入 fake）、`IArcThemeService`。
* `ArcWindow : Window`：shell root + `PageHost`（页面入口）+ `OverlayHost`（Dialog/Toast/Drawer 覆盖层）；`TitleBarMode=Custom` 可自绘标题栏（System 为默认，Custom 仅编译验证，未做 Win32 交互点验）。
* 三个控件（FileDropzone/AvatarUpload/ChatThread→ChatView）从"直连 StorageProvider"改为"服务优先、未注册回退旧行为"。

## 7. 数据与虚拟化策略

| 场景 | 方案 | 说明 |
|---|---|---|
| 长列表（通用） | L1 `ArcVirtualList` | `ItemsSource` + 模板 + 选中/键盘；被 Chat、DataDisplay 复用 |
| 树/表（10k 节点） | `ArcTreeTable` 展平 + 视口窗口化 | 保留 `TemplatedControl` 自绘，只实例化视口 ±6 overscan、80 行缓存；10k 节点 20 个行控件 |
| 小数据网格 | 自有 `ArcDataGrid` | 与设计系统一致 |
| 大数据/分页网格 | `Feature.DataGrid`（唯一允许 `Avalonia.Controls.DataGrid` 的 L2f 包） | `ArcDataGridHost` + Arc 皮肤 + 分页 |
| 消息流（Chat/Agent） | `ArcChatView` 复用 `ArcVirtualList` | 1000 条只实例化约 10 行；自动滚动 Never/NearBottom/Always + 上滚暂停 |
| Markdown 长文档 | **不做块级虚拟化**（显式限制） | 一次实例化整篇；超长内容用 Chat 消息级虚拟化或拆页 |

## 8. 内容能力：Markdown 族 + Chat 族

* **`Arc.Avalonia.Markdown`（L2）**：解析层用 `PuraMarkdown.Core`（AST/诊断/大纲/流式稳定边界，无 Avalonia），Arc 负责全部渲染（块/行内/围栏、代码高亮、表格、图片/链接平台契约、本地化）。公共面：`ArcMarkdownView`（流式/选择/锚点）、`ArcMarkdownDocumentView`（宿主自管 AST）、`ArcMarkdownCodeBlock`，以及 `IArcMarkdownFenceRenderer` 扩展点。
* **流式策略**：只提交"稳定边界"之前的文本，尾部窗口每次 flush 重渲染；`CompleteAsync` 做一次完整解析保证与一次性解析一致。
* **`Arc.Avalonia.Chat`（L2）**：唯一消息模型 `ArcChatMessage`（Id/Role/Author/Markdown/Status/Delivery/Attachments/Metadata/Error + Append/Complete/Changed），供人类聊天、AI 回答、Agent 运行时共用；控件 `ArcChatBubble`（正文=ArcMarkdownView）、`ArcChatView`（虚拟化消息列表 + typing/空状态 + 内嵌 composer）、`ArcChatComposer`；`ArcChatThread` 为 `[Obsolete]` 别名。
* **去重**：全仓库唯一 Markdown 渲染实现 = `ArcMarkdownView`；Agent 的 `pura-agent` 围栏组件作为插件注册（choice/confirm），Blocks.AI 的渲染子集改为薄包装。

## 9. Agent 顶层（L4）

```text
宿主（网络/模型/工具/存储）
   │ 实现 IArcAgentTransport（推事件）/ IArcAgentCommandSink（收命令）/ IArcAgentServices（外链/剪贴板/确认）
   ▼
Arc.Avalonia.Agent —— 表现运行时（纯投影，无 I/O、无 Timer、无帧时钟；BannedApi RS0030→error 把关）
   ├─ 事件模型 ArcAgentEvent(ArcEventStamp{RunId,Sequence,EventId}, At)
   ├─ reducer：IArcAgentWorkspace / IArcAgentSession（重放/幂等/乱序缓冲/orphan drain）
   ├─ ArcDeltaCoalescer（Dispatcher 批次合并，非帧时钟）、审批串行队列、注意力队列、artifact 索引
   ├─ ArcAgentItemViewRegistry（值 → DataTemplate）
   └─ 控件：ArcAgentSurface（布局壳）+ ArcAgentConversation（时间线：气泡/工具卡/推理卡/审批卡/choice/confirm/artifact）
Arc.Avalonia.AgentUI（L4 叶子）：RunControlBar / ToolApprovalSheet / AttentionInbox / BackgroundRunDock
Arc.Avalonia.Chat（L2）：气泡与 composer；Arc.Avalonia.Markdown：正文与 pura-agent 围栏渲染
```

* **统一模型**：`ArcAgentSession.Messages` 元素类型 = `ArcChatMessage`；推理移出 Messages，作为 Timeline 事件由 Conversation 渲染成推理卡。
* **边界 CI**：`tools/BannedSymbols.txt`（HttpClient/File/Process/Timer/DispatcherTimer/TrayIcon/IStorageProvider…）+ R9 离线扫描 + ProjectReference/PackageReference 白名单。

## 10. 工程化与质量门禁（9 道，`tools/check-all.ps1`）

| 门禁 | 规则 | 当前 |
|---|---|---|
| R1 | 分层依赖（`architecture-allow.json`） | GREEN（209 条边） |
| R2 | 发布库不得引用叶子工程/演示文档 | GREEN（31） |
| R3 | 外部依赖白名单 + 传递闭包（`deps-policy.json`，fail-closed） | GREEN（33 包） |
| R4 | 主题来源只允许 `avares://Arc.Avalonia*` + 官方 Fluent | GREEN（124 文件） |
| R5 | nupkg 解包校验（id/依赖/XML/许可/无演示资源） | GREEN（33/33） |
| R6 | token-only（无硬编码色，28 条历史豁免） | GREEN（855 文件） |
| R8 | 无渲染层信号 | GREEN（855） |
| R9 | Agent 边界（禁用符号 + 依赖白名单） | GREEN |
| R10 | 控件契约（TemplatePart/部件引用/路由包装/args 基类/legacy 统计/无变体资源查找） | GREEN（260 文件，exempt=20 见 §11） |

配套：`--audit`（294/294 目录一致性）、`--smoke`（323 页导航 0 失败，含真实窗口壳）、`--shot`（双主题截图）、`--theme-report`（运行时主题断言）、`check-public-api`（33 包 API 基线）、`tools/build.ps1`（串行锁，避免多节点 MSBuild 冲突）。

**一次真实事故的教训（已固化）**：token 迁入 `ThemeDictionaries` 后，无变体 `FindResource` 对每个 token 返回 `UnsetValue`，`as IBrush ?? Brushes.Transparent` 让 Gallery 整窗透明，而 `--shot` 因预先重涂背景把它掩盖了。修复：`ArcResources.TryFind/Font` 统一查找路径 + `--theme-report renderPath` 断言 + R10.11 规则（见 [ADR 0003 §11](docs/adr/0003-theme-strategy.md)）。

## 11. 现状与缺口

* **已完成**：M0 基线/门禁/主题 POC；M1 出库（Docs 叶工程）+ Foundation 纯度 + 一行主题 + 发布工程（28 包）；M2 服务层/窗口壳、数据驱动列表/Feature DataGrid、Agent MVP + 主题 API + 图标扩展、控件契约化（TemplatePart/RoutedEvent/Charts 主题）；0.10 波次 Markdown/Chat 族 + Chat/Agent 统一消息模型。
* **缺口（下一阶段）**：
  1. 20 个可路由 legacy 事件未加 Routing（集中在 Chat/Markdown/AgentConversation；R10 exempt 计数）；
  2. 无 `AutomationPeer`/`AutomationId`/`TabIndex`（M3 a11y 15 人日）；
  3. 本地化：0 `.resx`（最小集清单已就绪：9 文件/63 串，`docs/analysis/18`）；RTL 178 个敏感点；
  4. 路由事件**无运行时冒泡断言**（当前只有静态门禁；M3 测试基线）；
  5. `*Size` 枚举未统一、`ItemsSource` 迁移未完成、组件内部尺寸未全部随 density；
  6. `ArcServiceProvider` 进程级静态（多窗口隔离）、`ArcWindow` Custom 标题栏未做 Win32 点验；
  7. `RepositoryUrl` 仍是占位符；截图为历史基线（未全量重拍）。

## 12. 设计原则（为什么这样设计）

1. **依赖方向是硬约束，能力可以软演进**：白色名单 + fail-closed + 传递闭包；新依赖先改 policy 和 ADR，再改代码。
2. **一行接入**：消费者只需要 `<arc:ArcTheme/>` + 一个 meta 包；系统内部再复杂，入口必须是一个元素。
3. **令牌是唯一颜色来源**：C# 走 `ArcResources.*`，axaml 走 `DynamicResource`；任何"取不到就回退"的静默行为都要有运行时断言兜底。
4. **additive 契约**：给控件加可冒泡事件时不改旧 CLR 事件（否则 320 个文档页 breaking）；双通知是有意设计。
5. **模板契约显式化**：`PART_*` + `[TemplatePart]` 让第三方主题可替换而不失焦点/交互。
6. **虚拟化按场景分工**：通用列表用 `ArcVirtualList`，树/表保留自绘窗口化，大数据用官方 DataGrid 封装——不为统一而重写。
7. **Agent 只做表现运行时**：UI 层理解事件与呈现，不理解网络/模型；宿主接缝显式、有编译器级禁令。
8. **文档即测试**：294 篇文档 × 323 个页面由 audit/smoke/shots 三件套持续验证，文档站是产品的一部分。
9. **决策留痕**：每个结构性决定都有 ADR（分层/依赖/主题/控件键/契约/Markdown 依赖），评审记录在 `docs/analysis/`。
10. **静态门禁 + 运行时探针**：静态规则防回退，`--theme-report`/`Verify` 防"编译通过但运行时失效"。

