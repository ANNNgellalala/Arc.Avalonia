# AI Agent 界面的组件库：Gallery 现状盘点与 UI 扩展分析

> 分析对象：`Arc.Avalonia`（Arc UI 的 Avalonia 12 / .NET 10 移植，294 项目录项、219 个展示页、438 张明暗截图）
> 分析目的：回答两个问题——**当前 gallery 已经包含哪些组件？** **面向 AI Agent 界面（可嵌入多种前端壳层、对接多种 Agent 后端），还可以扩展哪些 UI 组件？**
> **范围**：只做 UI（组件、页面、状态、交互、渲染性能）；Agent 逻辑、数据、调度、系统通知、权限、模型/工具调用由宿主应用负责（§2.1、§7）。
> 数据来源：`docs/arc-catalog.json`、`docs/01~05`、`README.md`、`src/` 源码；统计口径见[附录 A](#附录-a数据来源与统计口径)。
> 配套文件：[`agent-life-tools/01-inventory.zh.md`](agent-life-tools/01-inventory.zh.md)（全量 294 项盘点）、[`agent-life-tools/02-life-gaps.zh.md`](agent-life-tools/02-life-gaps.zh.md)（生活域场景与缺口）、[`agent-life-tools/03-agent-gaps.zh.md`](agent-life-tools/03-agent-gaps.zh.md)（Agent 状态与交互 UI 缺口）、[`arc-extension-catalog.json`](arc-extension-catalog.json)（可执行的 UI 扩展目录）。
> **实现状态（2026-10-28）**：P0 14 项 UI 组件已编码并通过构建/审计，实现说明见 [`07-p0-ui-implementation.zh.md`](07-p0-ui-implementation.zh.md)；P1 34 项与 P2 9 项仍为扩展目录中的待实现项。P0 落地后 gallery 当前为 **233 页 / 466 张明暗截图**（本报告其余部分的 219/438 为分析基线）。

---

## 0. 结论摘要

### 0.1 一句话结论

**这是一套面向 AI Agent 界面的 UI 组件库：它的职责是"把状态展示好、把意图表达清楚"，不负责 Agent 逻辑、数据与运行时。**
294 项现有组件已覆盖绝大多数"展示"需求；面向 Agent 生活工具的扩展只应做三件 UI 层的事：
**① 生活域的展示型组件**（日程、任务、提醒、健康、账单、餐食、行程等"数据长什么样、怎么排、怎么操作"），
**② Agent 状态与审批的 UI 闭环**（流式/停止/预演/审批/撤销/记忆/审计的*展示与交互*，执行逻辑在宿主），
**③ 页面级组合与渲染性能**（能拼出"今天/本周"这类页面，长列表不卡、动效不烧 CPU）。

Agent 运行时、持久化、调度、系统通知、托盘/热键、权限执行、模型与 MCP 调用、语音管线**全部属于宿主应用**，不在本库范围（边界见 §2.1、§7）。

### 0.2 关键数字

| 指标 | 数值 | 含义 |
| --- | --- | --- |
| 目录项 | **294**（128 components + 55 Special + 111 blocks） | Arc 公开目录 100% 覆盖，`--audit` 294/294 |
| 分类 | 23 类常规组件 / 5 组 Special / 6 组 Blocks | 组件谱系完整，但分类以"网页/产品营销"为导向 |
| 工程 | 24 个项目、516 个 `.cs`、约 17.7 万行、112 个 `.axaml` | 分层清晰：基础层 → 特性层 → 组合层 |
| 控件类 | **236 个控件类**（411 个公共类声明 / 407 个唯一名，另 100 个 internal 类） | 控件实现充足，但 Blocks 六项目仅 7 个公共控件——block 是页面产物，不是组件库 |
| 展示 | **219 个页面 / 438 张截图**（166 个独立条目页 + 53 个共享页；明暗各 219） | 设计验证基础设施完备，可直接复用做新组件验收；P0 落地后为 233 页 / 466 图（见 §0 状态与 07 报告） |
| 设计 token | 82 个常量 / 81 个主题 key / 8 种 accent / 明暗双主题 | 语义化 token，无硬编码色，主题切换零重建 |
| 可访问性 | 703 处 `AutomationProperties`（`docs/04` 审查时点；当前实测 722）、完整键盘路径、焦点陷阱 | 强项，扩展组件必须继承 |
| 虚拟化 | 全库仅 EmojiPicker 1 处生效的行级虚拟化（`docs/04` §2.6） | **真实产品化的第一道坎**：长列表/聊天/日志会重建整个子树 |
| 动效 | 11 套独立动画驱动、0 处 `CompositionTarget.Rendering`（`docs/04` §R1） | 桌面常驻场景需要共享帧时钟与可见性门控 |

### 0.3 面向 Agent 生活工具的 5 条判断

1. **不从零造 UI 基座。** Button/Input/Select/Dialog/Toast/Chart/Table/Avatar 等 128 个常规组件可以直接复用，扩展工作应集中在"域组件 + 页面组合 + 状态覆盖"。
2. **缺的不是"AI 聊天"，而是"AI 干活过程的可视化与可操作性"。** 现有 `agent-status`/`tool-call`/`agent-plan`/`agent-approvals`/`agent-memory` 是很好的*视觉素材*，但缺少执行前预演、可撤销、审计、成本与权限等**状态与交互的 UI 表达**；执行本身由宿主完成。
3. **生活工具的主界面不是聊天窗口，而是"今天"。** 日程、任务、提醒、习惯、账单这些"日视图/周视图"组合（block 层）是当前 catalog 最大的空白：294 项里没有 agenda（议程）、habit tracker（习惯追踪）、meal planner（备餐计划）、trip planner（行程规划）、personal finance（个人财务）这类生活域页面。
4. **组件契约要能承接宿主状态，但不去实现宿主能力。** 系统通知、托盘、调度、持久化、权限执行、模型调用等由宿主应用负责；组件只定义"这些状态长什么样、用户点了发出什么意图事件"，并把平台差异作为可配置的展示分支（见 §7）。
5. **成功标准是"界面可组合、状态齐全、可复用"，不是"业务能跑通"。** 每个扩展项挂到真实界面场景上验收（§2.3、§9），但验收对象是视觉/交互状态，而不是数据落库或 Agent 执行。

### 0.4 建议的优先级（详见 §6）

| 优先级 | 数量 | 内容 | 工作量（人日） |
| --- | --- | --- | --- |
| **P0** | 14 项 | Agent 状态/审批 UI 闭环（7 项）+ "今天"主界面（5 项）+ 捕获/虚拟化基础件（2 项） | **72** |
| **P1** | 34 项 | 健康、财务、家庭、餐食、出行、记录、关系、专注、阅读等生活域组件与 block，以及 Agent 的权限/审计/成本/语音的**展示界面** | **130** |
| **P2** | 9 项 | 锦上添花的可视化、特殊效果、垂直场景 block | **33** |
| **合计** | **57 项** | 16 components + 9 specials + 32 blocks（全部为 UI 交付物） | **235** |

> 人日按本仓库完成定义估算（S ≤1、M = 2–4、L = 1–2 周；汇总时取 S=1 / M=3 / L=8），**只包含 UI 交付物**：组件、ControlTheme、页面、明暗截图、状态走查。宿主应用的数据层/调度/通知/权限/模型调用不在此估算内。对应 §6 的 **3–4 人团队（2.8–3.4 FTE）14–17 周**；若固定 2–3 人，则约需 **16–24 周**（2 人约 24 周、3 人约 16 周）。扩展目录逐项数据见 [`arc-extension-catalog.json`](arc-extension-catalog.json)。

---

## 1. 现状：Gallery 里到底有什么

### 1.1 五层结构

| 层 | 内容 | 规模 | 对生活工具的价值 |
| --- | --- | --- | --- |
| **基础层** `Arc.Avalonia` | OKLCH 色彩引擎、明暗主题、8 accent、排版、动效（SpringSmooth/Snappy/Morph）、161 个 Lucide 图标、`ArcButton`/`ArcCard`/`ArcBadge`/`ArcIcon`/`ArcSpinner` | 82 个 token 常量 | ★★★★★ 直接继承，是"看起来统一"的根基 |
| **常规组件层** | 文本输入、特殊输入、选择、开关、滑块、日期/颜色/文件选择器、导航、展开、浮层、消息、进度、头像、卡片、图表、表格、活动、媒体、文字效果 | 128 项 / 23 类 | ★★★★☆ 交互基座齐全，但都是"短时、无状态"的展示型控件 |
| **Special 层** | 玻璃质感、动效、媒体输入、AI 小组件、AI 解释器；大量自绘/动画控件 | 55 项 / 5 组 | ★★★☆☆ 强项是 Agent 状态可视化与语音/媒体，弱项是真实数据绑定 |
| **Blocks 层** | 营销站、电商、应用壳层/设置、团队协作、数据可视化、AI 产品面 | 111 项 / 6 组 | ★★★★☆ 有 `command-palette`、`notification-center`、`week-calendar`、`agent-*` 等可改造的高价值组合 |
| **展示/验收层** `Gallery` + `Demo` | catalog 驱动侧栏、`ArcItemPage` 统一文档页、`--audit`（294/294）、`--smoke`、明暗离屏截图 | 219 页 / 438 图 | ★★★★★ 扩展组件的"零注册 + 自动审计 + 双主题截图"流程现成 |

补充：仓库共有 **24 个项目**（基础层 1、Demo 1、Gallery 1、特性项目 10、Special 项目 5、Blocks 项目 6）。每个特性项目自带 `Controls/`、`Themes/`、`Pages/`，gallery 通过反射发现 `ArcPage`，**新增组件不需要中心注册表**，这是后续扩展最省力的机制。

源码级盘点（详见 [`agent-life-tools/01-inventory.zh.md`](agent-life-tools/01-inventory.zh.md)）：

- **294 项的路由方式**：166 个独立条目页（Special 55 + Blocks 111）+ 88 个章节标题匹配 + 40 个别名（`ArcCatalogAliases`）= 294，missing 0。
- **页面结构**：166 个独立页 + 53 个共享页 = 219 页；53 个共享页中含 5 个 Gallery 自身页面（Foundations / Tokens / Icons / Motion / Kit proof）。9 个独立页的 `ItemSlug` 与截图文件名不一致（如 `glass-tabbar` → `glass-tab-bar`），迁移/重命名时需注意。
- **控件规模**：411 个公共类声明（407 个唯一名），其中 **236 个控件类**（234 个唯一名），另 100 个 internal 类；Blocks 六个项目合计只有 7 个公共控件，**Blocks 是"页面产物"而不是控件库**——生活工具扩展应复用组件 + 参考 block 组合，而不是在 block 上二次封装。
- **运行时能力为零**：全仓 0 处 `TrayIcon` / 平台系统通知 / `HttpClient` / 任务调度器 / 音频采集 / **显式列表虚拟化**（仅 EmojiPicker 的 `ListBox` 行级虚拟化生效；另有 92 处动画 `DispatcherTimer`）；写文件仅截图与签名导出 2 处。按本库"纯 UI"定位，这些属于宿主应用能力；组件库只需提供展示位与设置 UI（见 §7）。

### 1.2 23 类常规组件：有什么、对生活工具多有用

| 分类 | 数量 | 代表项 | 生活工具相关性 | 备注（作为生活工具积木时的缺口） |
| --- | --- | --- | --- | --- |
| Buttons | 11 | button, action-button, split-button, copy-button, theme-switch | 高 | 缺"长任务进行中/可取消"的异步动作态 |
| Gestures | 3 | hold-to-confirm, swipe-actions, reactions | 低–中 | 桌面端用得少，但"滑动完成"适合移动伴生端 |
| Menus | 3 | dropdown-menu, context-menu, user-menu | 中 | 需要"条目级 Agent 建议/解释"的菜单 variant |
| Text fields | 7 | input, textarea, search-field, inline-edit | 高 | 缺"自然语言输入 + 解析结果 chips"的捕获框 |
| Special inputs | 7 | number-field, money-input, tag-input, mention-input, shortcut-recorder | 中–高 | `money-input`/`tag-input`/`shortcut-recorder` 可直接用于记账、标签、热键设置 |
| Selects | 5 | select, combobox, multi-select, chip-group | 中 | 需支持分组、异步加载（MCP/模型/联系人） |
| Toggles | 7 | checkbox, switch, segmented-control, rating, radio-cards | 中 | 设置页主力；缺"权限/自动化的三态（允许/询问/禁止）" |
| Sliders | 3 | slider, elastic-slider, range-histogram-slider | 低 | 健康目标/预算阈值可借用 |
| Pickers | 6 | calendar, date-range-picker, time-picker, color-picker, emoji-picker | **高** | **日历没有事件模型**（仅选中日期/区间）；缺重复规则、提醒偏移、时长选择 |
| Editors | 4 | rich-text-editor, signature-pad, file-dropzone, avatar-upload | 高 | 日记/备忘/文件归档可用；缺"附件/图片/语音"统一的内容模型 |
| Navigation | 4 | tabs, breadcrumb, pagination, mega-menu-morph | 中 | 生活工具需要"日/周/月/年"视图切换，tabs 可复用 |
| Expand | 5 | scroll-area, accordion, expandable-card, resizable-panels | 中 | 详情面板可复用 |
| Overlays | 7 | dialog, drawer, popover, hover-card, tooltip, share-access | **高** | Agent 审批、确认、解释的主战场；缺"风险分级 + 后果预览"对话框 |
| Messages | 5 | alert, toast, toast-stack, announcement-bar, cookie-consent | **高** | 缺带动作/进度/撤销的 toast；缺系统通知的展示位/偏好界面（投递由宿主） |
| Progress | 6 | progress, skeleton, stepper, countdown, usage-meter, onboarding-checklist | **高** | `stepper` 适合多步 Agent 执行；`usage-meter` 可做预算/额度 |
| Avatars | 4 | avatar, avatar-group, badge, presence-stack | 中 | 个人 CRM/家庭共享需要 |
| Cards | 3 | card, metric-card, empty-state | **高** | 生活条目（任务/账单/习惯）的容器；缺条目级组件 |
| Charts | 14 | line/bar/donut/sparkline/gauge/activity-heatmap/waffle/treemap | **高** | 健康、财务、能耗、习惯的核心可视化；缺睡眠阶段/预算进度等域图表 |
| Advanced charts | 7 | sunburst, sankey, funnel, radar, realtime-stream, data-grid | 中 | 数据探索可复用；`realtime-stream` 适合桌面实时指标 |
| Tables | 7 | sortable-data-table, tree-table, tree-view, filter-toolbar, json-viewer, code-block | **高** | 交易/日志/任务列表；**无虚拟化、无行内编辑** |
| Activity | 3 | timeline, comment-thread, chat-thread | **高** | Agent 执行轨迹/日记/消息；**无流式追加、无工具消息** |
| Media | 3 | image-compare, carousel, card-stack | 中 | 照片日记/食谱图；缺"图片 + 元数据"条目 |
| Text effects | 4 | text-reveal, in-view-title, text-morph, text-shimmer | 低–中 | 启动/空态点缀 |

### 1.3 5 组 Special：Agent 视觉素材最集中的地方

| 组 | 数量 | 代表项 | 与 Agent 生活工具的关系 |
| --- | --- | --- | --- |
| Glass and surfaces | 11 | glass-menu, glass-card, control-center, wallet-stack, sheet-stack, share-sheet, card-to-article | `control-center` 是智能家居面板原型；`wallet-stack` 适合钱包/卡片；`sheet-stack` 适合移动伴生端 |
| Motion and interaction | 11 | dock, liquid-tab-bar, orbit-menu, action-morph, keycap-keyboard, typewriter-terminal | Mac 风格 dock/菜单可直接做桌面助手入口；`typewriter-terminal` 适合日志 |
| Media and input | 11 | voice-orb, voice-recorder, now-playing, product-gallery, photo-grid, cover-flow | **语音是生活工具的主入口之一**；`voice-orb`/`voice-recorder` 是现成素材 |
| AI widgets | 11 | **agent-status, tool-call, agent-plan, agent-timeline, context-window**, ink-fluid, activity-rings | Agent 执行可视化的核心 5 件套，但都是"演示态"，缺真实数据与交互 |
| AI explainers | 11 | **agent-loop, reasoning-tree, prompt-cache, vector-search**, data-flow, payment-flow, time-dial, date-reel | 适合"解释 Agent 为什么这么做"；`time-dial`/`date-reel` 可做时间选择器的高级变体 |

### 1.4 6 组 Blocks：能直接改造的"页面原型"

| 组 | 数量 | 对生活工具最重要的项 | 缺口 |
| --- | --- | --- | --- |
| Marketing and site | 18 | page-header, faq-section, contact-section | 与生活工具关系弱，可作"帮助/关于"页 |
| Commerce | 18 | **wallet-card, invoice-studio, usage-billing, billing-overview, delivery-tracker, cancel-flow** | 电商/计费语义，需改造成"个人账单/订阅/包裹" |
| App shell and settings | 21 | **command-palette, notification-center, inbox-triage, settings-page, security-settings, search-results, otp-input, media-player** | 最接近桌面助手壳层；缺桌面设置页（托盘/热键/自启的**配置界面**，系统能力由宿主） |
| Team and collaboration | 16 | **week-calendar, kanban-board, availability-picker, onboarding-illustration** | 团队协作语义，需改造成"个人周计划/家务看板" |
| Data and charts | 20 | **metrics-dashboard, kpi-drilldown, journey-flow, budget-variance, activity-terrain** | 商业指标语义，需改造成健康/财务/能耗仪表盘 |
| AI | 18 | **ai-composer, ai-chat, ai-side-panel, agent-memory, agent-tasks, agent-approvals, agent-routines, agent-orchestrator, mcp-servers, agent-run** | 已有完整"Agent 产品面"骨架，但都是静态演示数据；缺可交互的审批/撤销/编辑 |

### 1.5 对生活工具最有价值的 12 个现有项

| # | 现有项 | 为什么重要 | 直接使用前必须补的 |
| --- | --- | --- | --- |
| 1 | `calendar` + `date-range-picker` + `time-picker` | 一切"日程/提醒/账单到期"的入口 | 事件模型、重复规则、提醒偏移、议程视图 |
| 2 | `command-palette`（block） | 桌面助手的全局入口（快速捕捉/执行） | 可配置的唤起入口/快捷键设置 UI（全局注册由宿主）、意图解析结果展示、结果分组 |
| 3 | `notification-center`（block） | 提醒/主动建议的收件箱 | 通知列表/分组/勿扰/摘要的 UI（投递与系统权限由宿主） |
| 4 | `ai-composer` + `ai-chat`（block） | 对话式 Agent 主界面 | 流式输出、停止/重试、工具消息、引用来源 |
| 5 | `agent-approvals`（block） | 信任闭环的雏形 | 风险分级、参数预演、记住选择、审计 |
| 6 | `agent-memory`（block） | 长期记忆的浏览界面 | 记忆条目级编辑/遗忘/来源追溯 |
| 7 | `agent-routines`（block） | 例行任务（"每天 8 点简报"） | 触发器/重复/动作序列的可交互编辑器 |
| 8 | `agent-status` + `tool-call` + `agent-plan` + `agent-timeline`（Special） | Agent 执行可视化素材 | 真实状态机、可展开详情、失败重试 |
| 9 | `agent-loop` + `reasoning-tree`（Special） | 可解释性 | 从演示动画变成可查询的执行记录 |
| 10 | `week-calendar` + `kanban-board`（block） | 日/周视图与任务板的起点 | 去团队化、支持个人数据、拖拽与虚拟化 |
| 11 | `settings-page` + `security-settings`（block） | 权限/隐私/自动化设置骨架 | 权限三态、数据与隐私说明、按域分组 |
| 12 | `voice-orb` + `voice-recorder`（Special） | 语音入口素材 | 双向对话、打断、转写与确认 |

### 1.6 工程与验收基础设施（扩展工作的放大器）

- **目录驱动导航**：`ArcCatalog` 读取内嵌的 catalog 副本（`src/Apps/Arc.Avalonia.Demo/Catalog/arc-catalog.json`，与 `docs/arc-catalog.json` 字节级一致）；`ArcItemPage` 自动从 catalog 推导标题、分类、层级、排序；新增项只需加页面类，无需注册。
- **统一文档页**：`ArcDocLayout` 固定输出 breadcrumb / 标题 / tier / 描述 / When to use / Preview / 键盘与可访问性 / Motion / Implementation notes，扩展组件的文档形态可以直接继承。
- **审计与截图**：`--audit` 校验 294 项目录可达性（当前 294/294、missing=0），`--smoke` 做导航回归，`tools/shots.ps1` 产出明暗两套 PNG；新组件验收标准可以完全复用。按 `CatalogAudit` 同一算法静态复算：166 个独立条目页 + 88 个章节标题 + 40 个别名 = 294；截图文件实测 219×2 = 438。本次分析在 2026-10-26 对现有构建重新运行了审计模式（日志：`.recon/audit-2026-10-26.log`），输出为：

  ```text
  CATALOG AUDIT: pages=219 items=294 itemPages=166 matchedBySection=88 matchedByAlias=40 missing=0 pageFailures=0
  ```

  这与 01 号盘点文档的静态复算完全一致，可作为"扩展前基线"。
- **设计约束**：语义 token、同心圆角、4px 网格、只允许 400/500 字重、reduced-motion 分支、`{DynamicResource}`；新组件必须继承这些规则，否则会破坏"统一感"。
- **已知工程债（扩展前必须正视）**：零虚拟化、11 套动画驱动、77 个 `Render` 每帧分配、14+ 处布局属性动画（来源：`docs/04` §2.5/§2.6/§R1–R4）。生活工具的列表长度和常驻时间都远超 gallery 演示，这些债会先爆。

### 1.7 现状总评

| 维度 | 评价 |
| --- | --- |
| 视觉一致性 | ★★★★★（token 单一来源，8 accent × 明暗全部派生） |
| 组件广度 | ★★★★★（294 项，覆盖 Web 产品绝大多数界面） |
| 交互深度 | ★★★☆☆（演示级：选中/展开/输入都有，但无持久化、无长任务、无并发状态） |
| 数据能力 | ★★☆☆☆（全部为确定性示例数据，`Children.Clear()` 重建；无 ItemsSource/数据契约） |
| Agent 交互 | ★★★☆☆（可视化素材丰富，真实执行/审批/撤销/记忆编辑缺失） |
| 生活域覆盖 | ★★☆☆☆（只有通用展示组件，缺日程/任务/健康/财务/餐食/出行/日记/关系等域组件） |
| 宿主/运行时 | —（不在本库范围：组件只提供展示位与配置界面，见 §7） |
| 工程可扩展性 | ★★★★☆（项目分层 + 反射发现 + 目录审计，新增成本低；性能护栏缺失） |

**一句话：它现在是一套"很好的设计系统 + 展示型组件库"，还不是"生活工具 SDK"。下面的扩展建议就是为了把它推进到后者。**

---

## 2. 目标界面与范围：Agent 应用需要什么样的 UI

### 2.1 目标界面与范围声明

本库服务的是 **Agent 应用的前端设计**：同一套组件可以嵌入聊天式助手、常驻侧栏、独立桌面应用、Web/桌面/移动壳层，提供一致的视觉与交互语言。"桌面常驻的个人 Agent 助手"只是典型载体：

- **首页是"今天"**：日程、提醒、任务、健康/预算摘要与 Agent 建议。
- **执行过程可读**：计划、工具调用、审批、结果、失败与恢复都有对应的展示状态。
- **生活域有专门组件**：健康、财务、家庭、餐食、出行、记录、关系、专注。
- **自然交互**：命令面板、通知中心、语音状态条、键盘可达。

**组件库做什么 / 不做什么**：

| 组件库负责（本报告扩展范围） | 宿主应用负责（不在本库范围） |
| --- | --- |
| 视觉样式、布局、状态展示、主题/明暗/密度 | Agent 规划、工具调用、模型选择与编排 |
| 交互原语：按钮、输入、菜单、审批、撤销入口 | 真正执行动作、写入数据、发消息、支付 |
| 数据展示契约：`ItemsSource`、状态枚举、意图事件 | 数据持久化、同步、迁移、备份 |
| 页面组合：今天、本周、健康、财务、餐食、行程… | 业务规则、定时/触发、后台任务 |
| 渲染性能：列表虚拟化、共享帧时钟、绘制缓存 | 系统通知投放、托盘常驻、全局热键注册 |
| 可访问性与键盘路径 | 权限校验与沙箱、密钥管理 |
| 状态齐全的示例页与文档页 | 语音 ASR/TTS、MCP/插件运行时 |

组件只"展示状态 + 发出意图事件"：不替宿主做决定，也不把 Agent 逻辑写进控件。

### 2.2 四层 UI 能力模型与现有对应

| 层 | 职责 | 现有对应 | 结论 |
| --- | --- | --- | --- |
| L1 基础层 | token、主题、图标、动效、基础控件、可访问性 | `Arc.Avalonia` + 128 常规组件 | **可直接复用**，仅需补生活/Agent 展示相关基础件 |
| L2 生活域组件层 | 日程、任务、提醒、习惯、账单、健康指标、餐食、行程、日记、联系人等**展示组件** | 几乎没有（只有 calendar/timeline/card 等通用件） | **主要扩展区** |
| L3 页面组合层 | 今天、本周、健康、财务、家务、餐食、旅行、日记、关系、专注、简报 | 111 个 block 里没有生活域页面 | **第二大扩展区** |
| L4 Agent 状态与交互层 | 流式、停止/改向、预演、审批、撤销、记忆、例行任务、成本、审计、语音**界面** | 22 个 AI/Agent 项（演示态） | **补状态与交互，不补执行** |

> **L5 运行时与数据层（存储、调度、通知、权限、MCP、语音管线）不在本库范围**：由宿主应用负责，组件只为这些状态提供展示位与操作入口（边界见 §7）。

### 2.3 39 个目标界面场景（UI 覆盖的验收锚点）

> 场景用于驱动 **页面组合、组件覆盖与状态展示**，不代表组件库实现场景业务逻辑：每个新增项挂到至少一个场景，场景 id 写入 [`arc-extension-catalog.json`](arc-extension-catalog.json) 的 `scenarios` 字段；生活域 24 个场景的完整描述（用户目标、界面元素、可复用项、展示缺口）见 [`02-life-gaps`](agent-life-tools/02-life-gaps.zh.md) §3；Agent 域 15 个场景的 `id/title/summary` 见 [`03-agent-proposals.json`](agent-life-tools/03-agent-proposals.json) 的 `scenarios`，对应的状态阶段 / 展示约束 / UI 缺口分析见 [`03-agent-gaps`](agent-life-tools/03-agent-gaps.zh.md) §3、§4、§5（03 正文暂未展开逐场景的"界面元素 / 可复用项"）。
> 生活域 24 个 + Agent 域 15 个，共 39 个；全部至少被 1 个提案覆盖（提案数见下表）。

#### 2.3.1 生活内容域（24）

| id | 场景 | 提案数 |
| --- | --- | --- |
| `morning-brief` | 晨间简报 | 9 |
| `medication-plan` | 服药计划 | 4 |
| `bill-due` | 账单到期 | 9 |
| `weekly-meal-plan` | 每周备餐 | 2 |
| `trip-itinerary` | 旅行行程 | 4 |
| `home-repair` | 家庭维修 | 3 |
| `mood-journal` | 情绪日记 | 4 |
| `focus-block` | 专注时段 | 5 |
| `parcel-tracking` | 包裹快递 | 3 |
| `subscription-audit` | 订阅管理 | 4 |
| `id-expiry` | 证件到期 | 3 |
| `report-archive` | 体检报告归档 | 3 |
| `relationship-upkeep` | 生日与关系维护 | 1 |
| `home-energy` | 家庭能耗 | 7 |
| `pet-care` | 宠物照护 | 1 |
| `grocery-run` | 采买清单 | 3 |
| `chore-rota` | 家务轮值 | 6 |
| `sleep-trend` | 睡眠与健康趋势 | 3 |
| `expense-split` | 聚餐分摊 | 1 |
| `smart-home-scene` | 回家场景 | 2 |
| `spend-capture` | 随手记账 | 5 |
| `appointment-prep` | 就诊准备 | 4 |
| `outdoor-plan` | 周末户外 | 3 |
| `family-logistics` | 家庭日程协同 | 9 |

#### 2.3.2 Agent 交互域（15）

| id | 场景 | 提案数 |
| --- | --- | --- |
| `daily-plan-approval` | 每日计划审批 | 3 |
| `first-run-permissions` | 首次能力授权 | 9 |
| `errand-preflight` | 代办前预演 | 7 |
| `routine-fire-review` | 例行任务触发与复核 | 9 |
| `interrupted-run` | 打断与改向 | 4 |
| `failure-recovery` | 失败恢复 | 5 |
| `budget-guard` | 成本护栏 | 3 |
| `memory-review` | 记忆周检 | 2 |
| `focus-hours` | 专注时段打扰控制 | 6 |
| `offline-travel` | 离线与本地模型 | 4 |
| `voice-morning-checkin` | 语音晨间问询 | 3 |
| `audit-after-fact` | 事后审计 | 8 |
| `unattended-background` | 后台常驻 | 9 |
| `handoff-takeover` | 人工接管 | 2 |
| `sensitive-confirm` | 敏感动作确认 | 3 |

> 交叉覆盖说明：`morning-brief`（9）、`bill-due`（9）、`family-logistics`（9）、`first-run-permissions`（9）、`routine-fire-review`（9）、`unattended-background`（9）是"组合型"场景，天然需要多个组件协同，也是 §9.2 UI 状态走查的重点。

### 2.4 扩展设计原则（UI 层，在现有规则之上追加）

1. **状态优先**：Agent 的每个阶段都要有可展示状态（运行/等待输入/需审批/暂停/失败/完成/已停止），不能只有"转圈"。
2. **可打断的 UI**：停止/暂停/改向/接管等控件必须存在并可键盘操作；控件只发意图事件，执行由宿主完成。
3. **可预演/可审批的展示**：写操作类 UI 先展示"将发生什么"（diff、影响范围、风险等级），再让用户决策。
4. **可撤销/可审计的展示**：检查点、操作历史、审计列表要有专门组件与空态；回滚动作由宿主执行。
5. **可控打扰的 UI**：通知偏好、勿扰/摘要、专注条等界面要覆盖"现在/摘要/静默"三态。
6. **可组合**：新组件默认可嵌入任意容器（侧栏、浮层、整页、移动壳层），不假设全局单例或特定路由。
7. **继承现有设计纪律**：语义 token、同心圆角、reduced-motion、键盘可达、`{DynamicResource}`；新组件不引入硬编码色/字号。
8. **无副作用**：组件不读写宿主数据、不发起网络/模型调用、不注册系统能力；所有副作用通过事件交由宿主。

---

## 3. 差距分析

### 3.1 生活域覆盖矩阵

图例：**● 可直接复用**（现有项基本满足）／**◐ 部分覆盖**（有近似项，需扩展）／**○ 空白**（需新增）。

| 能力域 | 现状 | 现有可用项（catalog slug） | 主要缺口 |
| --- | --- | --- | --- |
| 时间与日程 | ◐ | calendar, date-range-picker, time-picker, countdown, week-calendar | 事件模型、重复规则、议程/日视图、提醒偏移、时区 |
| 任务与待办 | ◐ | checkbox, kanban-board, reorderable-list, onboarding-checklist, inbox-triage | 任务实体、优先级/截止/重复、快速捕获、子任务 |
| 提醒与通知 | ◐ | toast, toast-stack, notification-center, announcement-bar | 通知条目/分组/勿扰/摘要的 UI；投递与系统权限由宿主 |
| 习惯与追踪 | ◐ | activity-heatmap, waffle-chart, activity-rings | 习惯网格、连续天数、打卡、周目标 |
| 健康与身心 | ◐ | line-chart, gauge, activity-rings, activity-heatmap | 睡眠阶段、用药计划、症状/情绪、体检归档 |
| 财务与账单 | ◐ | wallet-card, invoice-studio, usage-billing, metric-card | 个人预算、账单到期、订阅、交易分类、分摊 |
| 家庭与家务 | ◐ | control-center, kanban-board, reorderable-list, settings-page | 家务轮换、家电/保修档案、维护计划、能耗、房间场景 |
| 餐食与采购 | ○ | （无专用项） | 食谱卡、周菜单、营养、购物清单、库存 |
| 出行与旅行 | ○ | （无专用项） | 行程时间轴、票卡、打包清单、交通衔接、天气 |
| 记录与日记 | ◐ | rich-text-editor, card-stack, timeline, photo-grid | 日记模板、情绪、语音备忘、每日回顾、照片日记 |
| 社交与关系 | ◐ | avatar, comment-thread, share-sheet, team-directory | 个人联系人卡、生日/纪念日、关系时间轴、问候草稿 |
| 专注与效率 | ○ | progress, countdown（借用） | 专注计时器、勿扰条、分心记录、屏幕时间摘要 |
| 资讯与阅读 | ◐ | blog-grid, newsletter-signup, code-block | 阅读队列、摘要卡、来源/可信度、天气条 |
| Agent 状态与交互 | ◐ | agent-status, tool-call, agent-plan, agent-timeline, context-window, agent-loop, reasoning-tree, ai-composer, ai-chat, ai-side-panel, agent-memory, agent-tasks, agent-approvals, agent-routines, agent-orchestrator, mcp-servers, model-picker | 流式状态、停止/改向控件、预演/审批卡、撤销与检查点展示、记忆编辑、成本/审计展示、语音会话 UI（执行由宿主） |
| 自动化与集成 | ◐ | mcp-servers, env-variables, webhooks, api-keys, settings-page | 规则编辑器、连接状态展示、权限范围选择器、失败重试状态 |
| 数据/隐私/平台 | —（宿主） | security-settings（单页） | 库内只需隐私/权限的**设置界面**；存储、同步、导出、托盘、热键、自启由宿主 |
| 桌面系统集成 | —（宿主） | （无专用项） | 组件只提供托盘菜单/热键设置的**视觉稿与设置项**；系统能力由宿主 |

### 3.2 三类差距，三种修法

| 差距类型 | 表现 | 修法 | 数量（建议） |
| --- | --- | --- | --- |
| **A. 已有组件能力不足** | calendar 只能选日期、toast 不能带动作、chat 不能流式、dialog 不能预演后果 | 扩展现有控件（新增 variant/property/子部件），不新增 catalog 项 | 14 组（§3.3） |
| **B. 缺域组件/页面组合** | 没有"今天"、习惯、用药、预算、菜单、行程、日记、联系人等 | 新增 `component` / `special` / `block` 目录项 | 57 项（§4） |
| **C. 宿主运行时能力（不在本库）** | 持久化、调度、系统通知投放、托盘/热键、权限执行、模型调用 | 不在组件库实现；组件只提供展示位与设置 UI，接缝用 `ItemsSource`/状态/事件约定（§7） | 0 项（边界说明） |

### 3.3 需要"扩展现有组件"的清单（不新增目录项）

| 现有项 | 扩展内容 | 服务场景 |
| --- | --- | --- |
| `calendar` | 事件集合（点/条/密度）、当前时间线、议程侧栏、点击事件回调、`EventTemplate` | morning-brief, daily-plan, medication |
| `date-range-picker` | 生活化预设（今天/本周末/本月/发薪日）、时区提示 | budget, travel |
| `time-picker` | 时长选择模式（15/30/60 分钟）、提醒偏移组合 | remind, focus |
| `toast` / `toast-stack` | 动作按钮（撤销/查看）、进度态、静默分组、去重 | 所有 Agent 长任务 |
| `dialog` | 风险分级头、后果预览、影响范围、"记住此选择" | 所有写操作审批 |
| `chat-thread` | 流式追加、工具消息、引用来源、失败重试、消息操作条 | ai-chat, journal |
| `command-palette` | 容器内快捷唤起、意图分组、最近使用、Agent 建议区（全局热键由宿主） | capture, focus |
| `notification-center` | 通知列表/分组/勿扰/摘要的 UI（投递与系统权限由宿主） | remind, proactive |
| `settings-page` | 权限三态（允许/询问/禁止）、按数据域分组、危险区 | 隐私与自动化设置 |
| `usage-meter` | 预算阈值/超支预警、周期重置、成本维度（token/金额） | budget, agent-cost |
| `data-grid` / `sortable-data-table` | 行内编辑、行操作、虚拟化、空态动作 | budget, inbox |
| `empty-state` | 带 Agent 建议动作的空态（"要不要我帮你建一个？"） | 所有列表 |
| `timeline` | 分组（今天/本周）、可折叠、来源标记 | journal, relationship |
| `progress` / `stepper` | Agent 多步执行的状态（等待/执行/失败/跳过）、可展开详情 | daily-plan, automation |

### 3.4 空白域的判定依据（UI 层）

- **餐食、出行、专注、天气**：`docs/arc-catalog.json` 的 294 个 slug 与 description 中没有任何对应条目；现有 block 也没有"餐食 / 行程 / 专注 / 天气"语义（系统集成类能力属于宿主，不在 UI 缺口之列）。
- **日历无事件模型**：`src/Components/Arc.Avalonia.Pickers/Controls/ArcCalendar.cs` 的公开属性包括 `SelectedDate/RangeStart/RangeEnd/DisplayMonth/MinDate/MaxDate/ShowToday/WeekStartsOn/DisabledDates`，没有事件集合或事件模板。
- **零虚拟化**：`docs/04` §2.6 实测 **C# 代码里** `ItemsControl/DataTemplate/ItemsRepeater/VirtualizingStackPanel` 使用次数为 0；XAML 侧仅 7 处 `ListBox`（其中 EmojiPicker 行级虚拟化生效）。而生活工具的聊天、日志、交易、收件箱都是长列表。
- **无流式/工具消息的展示模型**：`chat-thread`/`ai-chat` 为静态消息列表；`tool-call`/`agent-timeline` 是自绘演示，不接收"事件流"这类展示数据。

### 3.5 边界：这些不是组件库的扩展项（宿主责任）

> 沿用"UI 组件库"的定位，以下能力**不进入**扩展目录与路线图；组件库只为它们提供展示位、设置项和状态样式。这样同一套 UI 可以对接不同 Agent 后端与不同前端壳层。

| # | 宿主应用负责 | 组件库只负责（UI 展示位） |
| --- | --- | --- |
| 1 | 本地持久化/同步/迁移 | 列表/详情/空态的展示（`ItemsSource` + 状态） |
| 2 | 调度与后台任务 | 提醒/例行任务的**配置界面**与状态展示 |
| 3 | 系统通知投放与权限 | 通知中心/勿扰/摘要的页面与设置项 |
| 4 | 托盘/全局热键/开机启动 | 托盘菜单视觉稿、热键设置控件（注册由宿主） |
| 5 | 权限校验与沙箱 | 权限范围选择器、授权说明页 |
| 6 | Agent 规划/工具执行/模型调用 | 计划、工具调用、流式、审批、失败等**状态 UI** |
| 7 | MCP/插件连接 | 连接状态、工具清单、健康度页面 |
| 8 | 语音 ASR/TTS/打断 | 语音会话条、波形、转写与确认的视觉状态 |
| 9 | 成本计量与日志采集 | 成本条、审计列表、trace 页面的展示 |
| 10 | 文案翻译与地区数据 | RTL/长文案适配、日期/数字格式的**排版**（数据由宿主） |

---

## 4. 扩展方案总览

> 本节数据源为 [`arc-extension-catalog.json`](arc-extension-catalog.json)（v2：**57 个 UI 扩展项 + 39 个界面场景**）。每一项的 `why` / `notes` / `composes` / `scenarios` / `priority` / `effort` 在 JSON 中完整可查；三个提案来源（02 生活域、03 Agent 域、Lead 评审补充）已合并，Lead 的 5 项补充与 4 项优先级调整在 JSON 中以 `source: "lead"` 与 `reviewNote` 标明。**目录不包含任何运行时/数据层交付物**；组件只定义展示状态与意图事件。

### 4.1 统计与分类

| 维度 | 分布 |
| --- | --- |
| 范围 | **纯 UI 交付**：组件 + ControlTheme + 页面 + 状态/明暗截图；不含宿主运行时、数据层、Agent 逻辑 |
| 类型 | component **16** / special **9** / block **32** |
| 优先级 | P0 **14** / P1 **34** / P2 **9** |
| 资产域 | life **25** / agent **28** / foundation **4** |
| 工作量 | S **8** / M **33** / L **16** = **235 人日**（P0 72 / P1 130 / P2 33） |
| 新增分类 | block 组 `Daily life`（22 项）；component 分类 `Foundation`（1 项）；其余全部复用现有 23 类 / 5 组 / 6 组 |
| 主要落地项目 | Blocks.App 18、Special.AIWidgets 8、Blocks.AI 7、Blocks.Commerce 5、AdvancedInputs 3、Feedback 3、Actions 2、Charts 2、Content 2、Blocks.Data 2，其余各 1 |
| 复用关系 | 每一项都声明 `composes`（引用现有 294 项或目录内新项），确保"组合现有积木"而非从零画控件 |

**扩展的三种形态**（与 §3.2 对应，均为 UI 层）：

1. **扩展现有组件**（不新增目录项，14 组见 §3.3）：给 calendar 加事件展示模型、给 toast 加动作、给 chat 加流式状态、给 dialog 加风险预演等。成本低、收益直接，应优先于新增。
2. **新增 57 个 UI 目录项**：16 个组件 + 9 个 Special + 32 个 block。block 占比最高（32/57）符合"Agent 应用的主界面是页面组合，而不是更多原子控件"的判断。
3. **页面组合与渲染性能**：长列表虚拟化、共享帧时钟、绘制缓存（`docs/04` §R1/R2/R4）——这是 UI 展示优化的地基，不涉及宿主逻辑。

### 4.2 P0（14 项，MVP 阻塞项）

> 完整设计见 §5；每项都列出主要服务场景。

| # | slug | 类型 | 工作量 | 主要场景 | 一句话 |
| --- | --- | --- | --- | --- | --- |
| 1 | `virtualized-list` | component | L | mood-journal, audit-after-fact, memory-review | 长列表虚拟化基础件（ItemsSource + 增量更新 + 粘底滚动） |
| 2 | `quick-capture-bar` | component | M | spend-capture, appointment-prep, morning-brief | 快速捕获条：输入 → 解析 chips → 确认（解析与落库由宿主，热键注册由宿主） |
| 3 | `agent-transcript` | component | M | interrupted-run, routine-fire-review, audit-after-fact | 可增量追加、可虚拟化的 Agent 事件流（message/tool/plan/approval/ask） |
| 4 | `run-control-bar` | special | M | interrupted-run, handoff-takeover, unattended-background | 停止（立即/安全点）/暂停/改向/接管 |
| 5 | `tool-approval-sheet` | special | L | errand-preflight, daily-plan-approval, sensitive-confirm | 预演副作用 + 改参 + 审批范围 + 过期 + 撤销入口 |
| 6 | `agent-ask-card` | special | M | daily-plan-approval, errand-preflight, first-run-permissions | 结构化追问（选日期/选账户/传文件/二选一/延后回答） |
| 7 | `routine-schedule-editor` | component | L | routine-fire-review, first-run-permissions | 例行任务排期：RRULE、时区、下次运行预览、misfire/overlap 策略 |
| 8 | `background-run-dock` | special | L | unattended-background, focus-hours, routine-fire-review | 后台运行状态的常驻坞（状态/进度/暂停入口；托盘由宿主） |
| 9 | `agent-attention-inbox` | block | M | routine-fire-review, failure-recovery, focus-hours, unattended-background | 「等你处理」统一队列：审批/追问/失败/到期 routine |
| 10 | `daily-brief` | block | M | morning-brief, home-energy, bill-due, parcel-tracking | 「今天」简报卡：天气 + 日程 + 到期 + 健康 + 预算 + 一条可执行建议 |
| 11 | `day-agenda` | block | L | morning-brief, family-logistics, focus-block, appointment-prep, outdoor-plan | 日议程：事件/提醒/任务/服药同一时间轴 + 冲突标记 |
| 12 | `reminder-row` | component | M | morning-brief, medication-plan, bill-due, chore-rota, id-expiry, subscription-audit | 「到期 + 重复 + 完成/延后」原子行 |
| 13 | `reminder-center` | block | L | morning-brief, bill-due, medication-plan, chore-rota, id-expiry, parcel-tracking | 行动中心：逾期/今天/即将/已延后分组 + 批量延后与改期 |
| 14 | `medication-schedule` | block | M | medication-plan, report-archive, appointment-prep | 用药计划/剂量/打卡/库存与续方提醒 |

**P0 的组合逻辑**：`virtualized-list` + `agent-transcript` + `run-control-bar` + `tool-approval-sheet` + `agent-ask-card` + `agent-attention-inbox` + `background-run-dock` 构成"**Agent 干活过程可见、可控、可审批**"的 UI 闭环（执行由宿主完成）；`quick-capture-bar` + `day-agenda` + `reminder-row` + `reminder-center` + `daily-brief` + `medication-schedule` 构成"生活工具每天会被打开"的最小界面。两者缺一不可：只有前者是"AI 控制台"，只有后者是"又一个日历 App"。

### 4.3 P1（34 项，显著提升）

#### 4.3.1 Agent 状态与审批界面（16 项）

| slug | 类型 | 工作量 | 一句话 |
| --- | --- | --- | --- |
| `permission-scope-picker` | component | S | 审批范围选择 UI（一次/本任务/1 小时/永久 + 文件夹/金额/账户/时段约束；执行由宿主） |
| `autonomy-dial` | component | S | 按数据域设置自主级别的选择 UI（建议/询问/执行/全自动；策略执行由宿主） |
| `diff-review` | component | M | 通用 diff 展示控件（文件/日历/金额变更的批准视图） |
| `agent-handoff-banner` | component | S | 人工接管状态条（谁在控制、接管多久、何时交还） |
| `guardrail-alert` | component | S | 规则/风险/覆盖请求的提示 UI（拦截执行由宿主） |
| `agent-budget-bar` | component | S | 运行中的 token/时长/金额预算条与阈值状态（降级动作由宿主） |
| `voice-session-bar` | special | M | 常驻语音条：按住说话/打断/静音/转写纠错的状态与操作入口（音频链路由宿主） |
| `checkpoint-restore` | special | M | 检查点列表、恢复范围预览、回滚确认 UI（回滚由宿主） |
| `local-model-status` | special | M | 本地模型状态展示（加载/推理/离线/切换入口；切换由宿主） |
| `memory-review-queue` | special | M | 记忆周检（来源/置信度/编辑/合并/遗忘） |
| `desktop-agent-settings` | block | M | 桌面 Agent 设置页（热键/自启/通知/后台/隐私的配置 UI） |
| `notification-routing` | block | M | 通知偏好与打扰控制的设置 UI（现在/摘要/静默；投递与升级由宿主） |
| `agent-audit-log` | block | M | 执行审计展示页（时间/工具/参数/结果/审批人/成本；数据由宿主） |
| `connector-health` | block | M | MCP/集成连接状态页（scope 展示与失败状态；重试由宿主） |
| `offline-action-queue` | block | M | 离线动作队列的展示与冲突提示（重放由宿主） |
| `trust-rules-manager` | block | M | 信任规则管理 UI（Always-allow 范围台账；执行由宿主） |

#### 4.3.2 生活域（16 项）

| slug | 类型 | 工作量 | 一句话 |
| --- | --- | --- | --- |
| `weather-forecast` | component | M | 今天/未来几天天气窗口（简报、出行、户外共用） |
| `habit-streak-strip` | component | S | 7/30 天习惯连续条 + 当前连续天数 |
| `task-board` | block | M | 任务主页面：今天/本周/逾期/无日期 + 列表/看板双视图 |
| `habit-tracker` | block | M | 习惯打卡/连续天数/月热力图/目标建议 |
| `bill-due-list` | block | M | 账单到期列表（分层提醒/已付/未来 30 天） |
| `budget-board` | block | L | 预算看板（分类预算/进度/超支解释） |
| `expense-ledger` | block | L | 交易台账（分类/备注/收据/分摊） |
| `subscription-tracker` | block | L | 订阅清单（续费日/涨价/闲置/总成本/取消） |
| `document-vault` | block | L | 证件与文档保险库（到期/分类/搜索/导出） |
| `daily-journal` | block | M | 每日记录（情绪/一句话/照片/语音 + 月回看） |
| `focus-session` | block | M | 专注时段（声明目标/勿扰/中断记录/结束复盘） |
| `grocery-list` | block | M | 按货架分区的采买清单（多人协作/缺货替换） |
| `meal-planner` | block | L | 周菜单（偏好/库存/预算 + 自动汇总购物清单） |
| `home-maintenance` | block | L | 家电/房屋维护（故障/保修/预约/单据） |
| `smart-home-panel` | block | L | 回家/离家/睡眠场景与设备状态（含离线降级） |
| `trip-itinerary` | block | L | 行程（交通/住宿/预约/证件 + 逐日安排） |

#### 4.3.3 基础件（2 项）

| slug | 类型 | 工作量 | 一句话 |
| --- | --- | --- | --- |
| `tray-menu` | block | M | 托盘菜单与托盘状态的视觉稿（今天/暂停提醒/专注/退出）——宿主常驻入口的 UI |
| `reference-band-chart` | component | M | （生活域共用的范围对比图表，归入 foundation 资产） |

### 4.4 P2（9 项，锦上添花）

| slug | 类型 | 工作量 | 一句话 |
| --- | --- | --- | --- |
| `spoken-confirmation` | component | S | 免手场景的语音复述确认（敏感动作） |
| `global-hotkey-field` | component | S | 全局热键录制与注册状态/冲突/平台限制的**展示**（注册由宿主） |
| `run-trace-viewer` | special | L | 调试/复盘用 span 瀑布（模型/工具/等待 + 成本 + 对比） |
| `model-routing-policy` | block | L | 按任务类型路由本地/小模型/强模型的规则与成本预估 |
| `run-history-list` | block | M | 可搜索的历史运行列表与重放入口 |
| `chore-board` | block | M | 家务轮换看板（重复家务/公平性回顾） |
| `energy-usage` | block | M | 家庭能耗趋势（日/月/设备 + 费用对照） |
| `package-inbox` | block | M | 多包裹汇总（异常件/代收/改约） |
| `pet-care` | block | M | 宠物照护（喂食/遛弯/疫苗/体重/兽医预约） |

### 4.5 与现有组件的边界（去重说明）

> 这是本轮评审最容易被质疑的地方：**新增项不能把已有能力换个名字再卖一遍**。下表列 **30 个最容易与现有能力重叠的项**（含 Lead 补充的 5 项）；其余 27 项的去重依据见 [`02-life-gaps`](agent-life-tools/02-life-gaps.zh.md) §9 的生活提案复用矩阵、[`03-agent-gaps`](agent-life-tools/03-agent-gaps.zh.md) §12.2 的 Agent 提案复用矩阵，以及 03 §9 对 `autonomy-dial` / `guardrail-alert` / `connector-health` 的补充对照（24 + 25 + 3 = 52 个提案项）。只有"数据模型或交互模式确实不同"才保留为新增。

| 新项 | 最接近的现有项 | 关系 |
| --- | --- | --- |
| `agent-transcript` | `chat-thread` / `ai-chat` | 组合：复用气泡与 markdown 子集；新在**事件类型 + 增量更新 + 可虚拟化** |
| `tool-approval-sheet` | `agent-approvals` | 替换：后者是只读样例页；新在**预演副作用/改参/范围/过期** |
| `run-control-bar` | `agent-status` / `ai-composer` | 组合：新在**向宿主发出停止/改向/接管意图** |
| `background-run-dock` | `agent-status` | 新场景：常驻坞/托盘入口的运行状态展示，不是页面内状态的重复 |
| `agent-attention-inbox` | `notification-center` / `inbox-triage` / `agent-tasks` | 组合三者；语义从"消息/通知"变为**"等你处理"** |
| `agent-ask-card` | `ai-chat` | 新在**结构化追问**（选择器/文件/二选一）与延后回答 |
| `reminder-center` | `notification-center` | 组合：新在**提醒语义**（分组/延后/改期） |
| `day-agenda` | `week-calendar` / `calendar` | 替换静态示例；新在**事件模型 + 时间轴 + 冲突** |
| `daily-brief` | `metrics-dashboard` / `kpi-drilldown` | 复用卡片与图表；新在**"今天"的内容编排与建议动作** |
| `quick-capture-bar` | `command-palette` | 复用交互；新在**可复用控件 + 解析 chips + 全局热键** |
| `virtualized-list` | `data-grid` / `sortable-data-table` / `chat-thread` | 底层原语：不是替代，而是被这些列表复用 |
| `tray-menu` | `settings-page` | 新在**系统级入口的 UI**（托盘菜单视觉） |
| `weather-forecast` | `metric-card` | 新在气象数据形态与时间窗口 |
| `habit-tracker` | `activity-heatmap` / `activity-rings` | 组合 + **打卡、连续天数、目标** |
| `document-vault` | `file-dropzone` | 组合：拖放负责导入，保险库负责**分类/到期/搜索** |
| `expense-ledger` | `sortable-data-table` | 组合 + 记账语义（分类/分摊/收据） |
| `task-board` | `kanban-board` / `project-board` | 组合；去团队化，加"今天/逾期/无日期"维度 |
| `routine-schedule-editor` | `agent-routines` | 替换展示字符串为**真实 RRULE/时区/预览** |
| `medication-schedule` | `activity-rings` / 图表 | 新在**用药数据模型与安全确认** |
| `habit-streak-strip` | `activity-heatmap` / `activity-rings` | **变体优先**：先给 heatmap 加 7/30 天窗口；独立成项仅因需要独立标题/点击回填契约（02 §9） |
| `reference-band-chart` | `line-chart` / `gauge` | **扩展优先**：作为 `ArcLineChart` 的 Band/Target 属性；不新增绘制引擎，目录项只对应公开 API 文档页（02 §9） |
| `global-hotkey-field` | `shortcut-recorder` | 包裹：全局注册/冲突/平台能力探测，普通组合键录制已由 `shortcut-recorder` 覆盖（03 §12.2） |
| `desktop-agent-settings` | `settings-page` | 包裹：桌面设置分区（热键/自启/通知偏好的 UI），复用 `settings-page` 骨架（03 §12.2） |
| `agent-budget-bar` | `usage-meter` / `progress` | 组合：预算阈值与降级状态的展示；视觉复用 `usage-meter`，新增的是 run 级预算语义（03 §12.2） |
| `notification-routing` | `notification-center` / `settings-page` | 不同对象：投递策略/打扰控制 vs 收件箱/设置页（03 §12.2） |
| `connector-health` | `mcp-servers` | 互补：MCP 管工具，本项管账号/同步/重授权（03 §12.2） |
| `voice-session-bar` | `voice-orb` / `voice-recorder` | 组合：会话控制条；不是全屏 orb、也不是录音控件（03 §12.2） |
| `run-history-list` | `agent-run` / `agent-tasks` | 补"历史层"：单次 run 卡与任务队列都不提供可搜索/重放（03 §12.2） |
| `model-routing-policy` | `model-picker` | 不同对象：单次模型选择 vs 条件→档位路由策略（03 §12.2） |
| `trust-rules-manager` | `security-settings` / `agent-approvals` | 不同对象：账号安全设置 vs Always-allow 规则台账（03 §12.2） |

未列入上表的其余 27 项，其"最接近现有项 / 不重复理由"见 `02-life-gaps` §9 与 `03-agent-gaps` §12.2+§9（`autonomy-dial` / `guardrail-alert` 见 03 §9）；其中 `habit-streak-strip`、`reference-band-chart` 按"变体/扩展优先"处理（见上表新增行），`arc-extension-catalog.json` 中对应项的 `dedupeNote` 记录了该判断。"不存在换名重复风险"这一绝对结论不成立，本报告改为逐项对照。

### 4.6 跨域依赖（组件之间的复用关系）

> 生活域的提醒/排期/待处理展示复用 Agent 域的**UI 组件**，两域不重复造组件。宿主的数据与执行不在此依赖图中。

| 从 | 依赖 | 原因 |
| --- | --- | --- |
| `reminder-row` | `routine-schedule-editor`, `notification-routing` | 重复规则编辑与通知偏好页面复用 Agent 域 UI |
| `reminder-center` | `routine-schedule-editor`, `notification-routing`, `background-run-dock` | 分组/延后需要排期编辑、通知偏好与运行状态坞 |
| `day-agenda` | `routine-schedule-editor` | 跨天/重复事件的展示依赖重复规则编辑器 |
| `medication-schedule` | `notification-routing` | 服药提醒的渠道偏好复用通知路由 UI |
| `daily-brief` | `agent-attention-inbox` | 简报里的"待你处理"来自统一队列 UI |
| `task-board` | `quick-capture-bar` | 快速添加复用捕获条 |
| `agent-transcript` | `virtualized-list` | 长会话事件流必须建立在虚拟化之上 |
| `run-trace-viewer` | `virtualized-list` | 轨迹条目多，必须虚拟化 |
| `expense-ledger` | `virtualized-list` | 交易明细上量前必须虚拟化 |
| `tool-approval-sheet` | `permission-scope-picker`, `trust-rules-manager` | "记住范围/规则"落到权限选择器与信任规则 UI |
| `background-run-dock` | `tray-menu` | 后台运行状态与托盘入口（视觉）互补 |
| `agent-attention-inbox` | `notification-routing`, `tray-menu` | 统一待处理队列需要通知偏好与桌面入口 UI |

### 4.7 新分类与新项目建议

| 建议 | 说明 |
| --- | --- |
| 新增 block 组 `Daily life` | 22 个生活域 block。建议新建 `Arc.Avalonia.BlocksLife`；扩展目录暂把 `project` 指向现有 `Blocks.App` / `Blocks.Commerce` / `Blocks.Data` 以保证可构建，落地时一次性迁移 |
| 新增组件分类 `Foundation` | `virtualized-list` 落在基础层 `Arc.Avalonia`，与 `ArcButton` / `ArcCard` 同级，供所有长列表复用 |
| Agent 新 Special 落位 | `run-control-bar` / `tool-approval-sheet` / `agent-ask-card` / `background-run-dock` 等 4 个 P0 落 `Arc.Avalonia.Special.AIWidgets`，与现有 11 项并列 |
| 不新增其他分类 | 其余扩展全部归入现有 23 类 / 5 组 / 6 组，降低目录复杂度 |

---

## 5. P0 重点设计

> 完整设计说明（含更细的 Anatomy、状态枚举、验收清单）见 [`02-life-gaps`](agent-life-tools/02-life-gaps.zh.md) §7 与 [`03-agent-gaps`](agent-life-tools/03-agent-gaps.zh.md) §7。本节给出统一约定与 11 个旗舰项的紧凑设计。

### 5.1 统一约定

- **数据契约**：组件接收 `ItemsSource` / `IReadOnlyList<T>`，通过事件（`ActionRequested`、`DecisionChanged`、`Dismissed`…）向上报告意图；**不读写宿主数据、不发起 Agent/网络/模型调用**（现状反例：`ArcToolCall.Approve` 直接改自身模型）。
- **状态机**：Agent 类统一 `Idle → Running → Paused → NeedsInput → Done / Failed / Stopped`；列表类统一 `Loading / Empty / Populated / Error`；写操作类统一 `Preview → AwaitingApproval → Executing → Done / Undone / Failed`。
- **可访问性**：每个新控件补齐 `AutomationProperties`（名称/角色/值）、键盘主路径、焦点陷阱（浮层）、reduced-motion 分支。
- **主题与动效**：只使用语义 token；状态变化用 `SpringSmooth`，断言类数值（金额/进度/倒计时）不过冲。
- **验收**：每项在 gallery 页面或最小示例宿主壳层中呈现场景所需状态（含空/加载/错误/完成），并在 `arc-extension-catalog.json` 中可检索。

### 5.2 旗舰项设计

#### 5.2.1 `virtualized-list`（基础件）

- **目的**：给聊天、日志、交易、收件箱、长表格提供统一的高性能列表底座，替代全库"`Children.Clear()` + 全量重建"。
- **Anatomy**：`ItemsSource` → `ItemTemplate` → 滚动容器（虚拟化）→ 粘底/跳转最新 → 空态/加载态 → 增量更新入口（Insert/Update/Remove/Move）。
- **状态**：`Loading / Empty / Populated / Error`；粘底开关 `StickToBottom`；新条目提示 pill。
- **数据契约**：`IReadOnlyList<T>` + `INotifyCollectionChanged`；暴露 `ScrollToIndex`、`AtBottom`。
- **复用**：`scroll-area`（滚动条视觉）、`skeleton`（加载）。
- **验收**：10,000 行可滚动、内存稳定、选中不重建；对话场景粘底与"跳转最新"正确；新增/更新单条只重绘该行。

#### 5.2.2 `quick-capture-bar`（捕获入口）

- **目的**：快捷唤起的一句话捕获（热键注册由宿主），把"想到一件事"到"进入系统"的路径压到 3 秒内。
- **Anatomy**：输入框（多行自适应）→ 解析结果 chips（类型/时间/人物/金额/清单）→ 确认/编辑/取消 → 键盘提示条。
- **状态**：`Idle / Parsing / Parsed / Ambiguous / Saving / Error`。
- **数据契约**：`SubmitRequested(text)`；宿主返回 `ParsedCapture`（或由宿主直接给出解析状态），控件只负责展示与编辑；确认动作以事件交宿主落库，组件不写数据。
- **复用**：`expanding-search`、`tag-input`、`mention-input`、`shortcut-recorder`、`command-palette`（页内交互参考）。
- **验收**："明天下午 3 点和张医生复诊"这类解析结果能以 chips 展示并可编辑；解析失败/歧义有明确态；Esc 关闭不丢草稿（草稿由宿主或本地状态持有）。

#### 5.2.3 `agent-transcript`（事件流）

- **目的**：Agent 运行的唯一可信记录视图：消息、思考、工具调用、计划、审批、追问、结果、错误都在同一条可增量更新的流里。
- **Anatomy**：事件行（类型图标 + 摘要 + 状态）→ 可展开详情（参数/结果/引用）→ 行级操作（重试/复制/查看来源）→ 粘底/新输出 pill。
- **状态**：事件级 `Pending / Streaming / Done / Failed / Skipped`；整体 `Running / AwaitingInput / Done / Failed / Stopped`。
- **数据契约**：事件模型 `AgentEvent { kind, id, status, payload, citations, startedAt, endedAt }`；增量 `Insert/Update`，禁止全量重建；事件不可由控件静默删除。
- **复用**：`chat-thread`（气泡）、`tool-call`（工具摘要）、`agent-plan`、`virtualized-list`。
- **验收**：长会话（≥500 事件）滚动流畅；流式状态与停止按钮正确（停止请求由宿主执行）；失败事件可展开错误与重试入口；审计视图可展示事件来源。

#### 5.2.4 `run-control-bar`（控制条）

- **目的**：把"Agent 正在干活"从展示状态变成可控状态。
- **Anatomy**：状态图标 + 当前步骤/耗时 → 停止（立即 / 安全点）/ 暂停 / 改向（插入指令）/ 接管 → 更多。
- **状态**：`Running / Pausing / Paused / Stopping / Stopped / TakenOver`。
- **数据契约**：事件 `StopRequested(scope)`、`PauseRequested`、`SteerRequested(text)`、`TakeoverRequested(step)`；控件不自行改变运行状态。
- **复用**：`agent-status`、`hold-to-confirm`（立即停止）、`toast`（确认反馈）。
- **验收**：停止有明确作用域（当前步/整个 run）；改向文本进入 transcript；接管后交还可继续。

#### 5.2.5 `tool-approval-sheet`（审批单）

- **目的**：高风险动作（付款、发邮件、改日历、删数据）在**执行前**被看见、被修改、被限制范围。
- **Anatomy**：工具与意图 → 参数表（可编辑）→ 副作用预览（diff/日历/金额/收件人）→ 风险与不可逆提示 → 有效期倒计时 → 决策（批准 / 拒绝 / 改参批准 / 委托）→ 范围（一次 / 本任务 / 1 小时 / 永久）。
- **状态**：`Checkout / Editing / Submitting / Decided / Expired / Cancelled`。
- **数据契约**：`ApprovalRequest { tool, args, risk, preview, expiresAt, scopes }`；事件 `Decision { kind, modifiedArgs, scope }`；审计由宿主写入。
- **复用**：`drawer`/`bottom-sheet`、`hold-to-confirm`、`tool-call`、`diff-review`、`permission-scope-picker`。
- **验收**：金额/日期可预览；"永久允许"必须落 scope；过期后不可批准；拒绝有理由输入；决定以事件交宿主写入审计。

#### 5.2.6 `routine-schedule-editor`（例行任务排期）

- **目的**：让"每天早上 8 点简报""每周日 20 点备餐提醒"可被普通用户编辑，而不是改一行 cron。
- **Anatomy**：重复选择（每天/工作日/每周几/每月几/自定义）→ 时间与时区 → 开始/结束 → 错过策略（补跑/跳过/询问）→ 重叠策略 → 下次运行预览。
- **状态**：`Viewing / Editing / Validating / Saved / Error`。
- **数据契约**：RRULE 子集 + 时区字段；输出 `ScheduleSpec { rrule, timezone, startAt, endAt, misfirePolicy, overlapPolicy }` 交宿主；组件只编辑与预览，不调度。
- **复用**：`calendar`（日期）、`time-picker`、`select`、`checkbox`。
- **验收**：时区/夏令时的**展示**正确（数据由宿主）；下次运行时间实时预览；错过与重叠策略可解释；非法组合即时校验。

#### 5.2.7 `day-agenda`（日议程）

- **目的**：生活工具的主界面——把事件、提醒、任务、服药放在一条时间轴上。
- **Anatomy**：日期头（前一天/今天/日历跳转）→ 全天条 → 小时网格（事件块/提醒行/任务）→ 当前时间线 → 冲突提示。
- **状态**：`Loading / Empty / Populated / Conflict / Error`。
- **数据契约**：`DateTimeOffset` + IANA 时区；事件/提醒/任务/用药统一 `AgendaItem { kind, start, end, status }`；冲突只标记不自动重排。
- **复用**：`week-calendar`（布局参考）、`calendar`、`timeline`、`reminder-row`、`segmented-control`。
- **验收**：跨天事件正确断行；当前时间线准确；冲突可见；≤50 条手工布局、>50 条走虚拟化；时区切换不丢事件。

#### 5.2.8 `daily-brief`（今天的简报）

- **目的**：打开应用 3 秒内知道"今天要注意什么"，并给出**一个可执行建议**。
- **Anatomy**：问候 + 日期/天气 → 时间轴摘要（下一个日程）→ 到期提醒（最多 3 条）→ 健康/预算小卡 → Agent 建议（采纳/忽略/稍后）→ 跳转各域。
- **状态**：`Loading / Ready / PartiallyLoading / Error`；建议级 `Pending / Accepted / Dismissed / Snoozed`。
- **数据契约**：只读聚合 `DailyBrief { date, weather, nextEvents, dueReminders, health, budget, suggestion }`；采纳动作走 `agent-attention-inbox` 或对应域。
- **复用**：`card`、`metric-card`、`activity-rings`、`weather-forecast`、`reminder-row`、`empty-state`。
- **验收**：无数据时显示可执行空态（"要不要我帮你建一个？"）；建议的采纳/忽略/稍后状态正确（动作事件交宿主）；加载分块不闪烁。

#### 5.2.9 `reminder-center`（提醒行动中心）

- **目的**：把散落在通知里的提醒变成"可以处理的工作台"。
- **Anatomy**：分组（逾期 / 今天 / 即将 / 已延后）→ 行（标题/到期/来源/重复标记）→ 行操作（完成/延后 10 分钟/1 小时/明天/改期/跳过）→ 批量操作 → 筛选。
- **状态**：`Loading / Empty / Populated / Error`；行级 `Pending / Due / Snoozed / Done / Missed / Paused`。
- **数据契约**：`Reminder { id, title, dueAt, recurrence, source, status }`；延后/完成事件回宿主；不内建定时器。
- **复用**：`reminder-row`、`badge`、`empty-state`、`dropdown-menu`、`toast`。
- **验收**：延后与改期动作的菜单/状态立即反映（动作交宿主）；分组计数正确；批量完成可撤销；空态/加载/错误态齐全。

#### 5.2.10 `agent-attention-inbox`（等你处理）

- **目的**：桌面助手的常驻首页——所有需要人决策的事项只有一个入口。
- **Anatomy**：分类（审批 / 追问 / 失败 / 到期 routine / 建议）→ 卡片（摘要/风险/来源/时间）→ 行内主操作（批准/回答/重试/查看）→ 批量处理 → 空态。
- **状态**：`Loading / Empty / Populated / Error`；条目级 `New / Seen / InProgress / Resolved / Expired`。
- **数据契约**：`AttentionItem { kind, refId, title, risk, createdAt, deadline }`；处理动作路由到对应组件（审批单/追问卡/恢复选项）。
- **复用**：`notification-center`、`inbox-triage`、`agent-approvals`、`guardrail-alert`。
- **验收**：可嵌入侧栏/浮层/整页三种容器；处理完成与空态正确；计数与分组随宿主数据更新。

#### 5.2.11 `background-run-dock`（后台运行坞）

- **目的**：表达"Agent 仍在后台干活"，并提供一键暂停/呼回。
- **Anatomy**：常驻小坞（状态点 + 当前任务 + 进度 + 停止/暂停 + 展开 transcript）。
- **状态**：`Idle / Running / NeedsAttention / Paused / Offline`。
- **数据契约**：订阅宿主提供的运行状态；不自行执行动作；同一状态可在窗口/侧栏/浮动坞复用。
- **复用**：`agent-status`、`tray-menu`（视觉参考）、`progress`。
- **验收**：宿主状态变化时 UI 同步；需审批态有醒目标记（是否发系统通知由宿主）；暂停态视觉正确。

### 5.3 其余 P0 摘要

| slug | 关键点 | 验收要点 |
| --- | --- | --- |
| `agent-ask-card` | 结构化问询：日期/账户/文件/二选一/自由文本；可延后回答 | 回答以事件交宿主写回 run；超时可"稍后"；敏感问题二次确认 |
| `reminder-row` | 到期 chip + 重复标记 + 完成/延后/改期动作 | 状态六态（Pending/Due/Snoozed/Done/Missed/Paused）齐全；键盘可操作 |
| `medication-schedule` | 药物/剂量/时间/库存/打卡/续方；异常与漏服提示 | 打卡可撤销；库存不足提醒；家属代确认状态由宿主记录并展示（含审计） |

---

## 6. 路线图

### 6.1 里程碑

> 人日只包含 UI 交付物（组件 + 主题 + 页面 + 状态/截图），**不含宿主应用的数据层、调度、通知、权限、模型调用**；周期按 **3–4 人团队（2.8–3.4 FTE）** 估算，总计 **14–17 周**；若固定 2–3 人，约需 **16–24 周**（2 人约 24 周、3 人约 16 周）。

| 阶段 | 周期 | 目标 | UI 交付项（数量） | 人日 | 退出标准（UI 走查） |
| --- | --- | --- | --- | --- | --- |
| **M0 基础与契约** | 1 周 | 组件状态契约与渲染底座 | `virtualized-list`；`ItemsSource`/状态/事件契约；扩展目录接入 gallery 流程；静态示例数据 | 8 | 10k 行列表原型流畅；3 个 P0 页面用同一组静态示例数据 |
| **M1 Agent 状态与审批 UI** | 2.5 周 | 过程可见、可控、可审批 | Agent 域 P0 7 项 + P1 8 项（权限范围/diff/接管/护栏/预算/检查点/托盘视觉等） | 50 | `interrupted-run` / `errand-preflight` / `sensitive-confirm` 的 UI 状态走查 |
| **M2「今天」** | 2 周 | 生活工具每天会打开的界面 | 生活 P0 5 项 + `quick-capture-bar` + `notification-routing` | 31 | `morning-brief` / `medication-plan` / `daily-plan-approval` 的 UI 走查 |
| **M3 生活域批次一** | 4 周 | 高频生活域组件与页面 | 生活 P1 16 项 + `reference-band-chart`（任务/习惯/账单/预算/台账/订阅/文档/购物/餐食/维护/专注/家居/天气/行程/日记） | 89 | `bill-due` / `weekly-meal-plan` / `chore-rota` / `focus-block` 的 UI 走查 |
| **M4 生活域批次二 + Agent 扩展** | 3 周 | 补齐长尾与自动化界面 | 生活 P2 4 项 + Agent P1 8 项 + P2 5 项（语音/记忆/审计/连接器/离线/信任规则/桌面设置…） | 57 | `trip-itinerary` / `audit-after-fact` / `offline-travel` 的 UI 走查 |
| **M5 打磨与验收** | 1.5 周 | 可交付 | RTL/长文案适配、a11y 走查、性能护栏、文档/截图/目录审计（`--audit`）、5 条 UI 状态走查 | —（质量活动） | §9.2 五条 UI 走查全通过；`--audit` 无 missing |

### 6.2 关键路径（组件之间的依赖）

```text
virtualized-list ──> agent-transcript ──> run-trace / audit / 长列表组件
calendar 事件展示 ──> day-agenda ──> daily-brief / medication-schedule
reminder-row ──> reminder-center ──> daily-brief
审批 UI 原语（sheet + scope picker + diff）──> trust-rules / 自动化页面
quick-capture-bar ──> task-board / capture 相关页面
「ItemsSource + 状态 + 事件」契约 ──> 所有生活域组件与 block
```

**软依赖（先占位、后替换，不阻塞里程碑）**：

- `daily-brief`（M2）复用 `weather-forecast`（M3）：M2 阶段天气卡用"暂无天气数据"占位，M3 接入展示组件。
- `tool-approval-sheet`（M1）依赖 `trust-rules-manager`（M4）：M1 先做"一次 / 本任务"两个 scope 的 UI，Always-allow 规则台在 M4 补齐。
- `tray-menu`（M1）composes `desktop-agent-settings`（M4）：M1 的设置入口先跳 `settings-page`，M4 再补桌面设置分区（热键/自启/通知偏好 UI）。

### 6.3 团队与排期假设

- **3–4 人（2.8–3.4 FTE）**：1 人基础/Agent 状态 UI（M0/M1），2 人生活域（M2–M4），0.5 人设计/验证按阶段共享（不计入上面的 FTE 分母）。
- **每周固定动作**：新增项进扩展目录 → 控件+主题+页面 → 明暗截图 → `--audit`/压测 → UI 状态走查。
- **风险缓冲**：只给性能底座（§7.3）预留 1 周；系统平台差异不在范围，不做预留。

### 6.4 最小可行集（预算/时间不足时）

只做 **P0 14 项**（不含任何宿主动能）：

- 工作量约 **72 人日**，**2 人约 7 周 / 3 人约 5 周**；
- 可完成 §9.2 的 **5 条 UI 状态走查**（晨间简报、快速捕捉、提醒中心、审批单、计划审批）；均在静态示例数据上完成，不依赖宿主运行时。
- 结论：**这是 UI 层最小可交付集**，宿主应用可在此之上接入自己的数据与 Agent 运行时。

---


## 7. 边界：组件库负责 UI，宿主应用负责逻辑与运行时

> 这一节不是"以后要补的非 UI 工作"，而是**明确不做什么**。保持组件库只做展示与交互，才能让同一套 UI 复用到不同 Agent 后端、不同前端壳层与不同产品形态。

### 7.1 一句话边界

组件库交付"看起来对、用起来顺、状态齐全"的界面；宿主应用交付"Agent 真的能干活"的能力。两者通过 **props / `ItemsSource` / 状态枚举 / 意图事件** 对接，不通过共享数据库或全局单例耦合。

### 7.2 责任矩阵

| 能力 | 组件库（本报告范围） | 宿主应用（不在范围） |
| --- | --- | --- |
| Agent 运行 | 状态、计划、工具调用、流式、失败、审批的**展示组件** | 规划、推理、模型调用、工具执行、重试 |
| 数据 | 列表/详情/空态的展示契约（`ItemsSource`） | 持久化、同步、迁移、备份、加密 |
| 时间/提醒 | 日历事件展示、议程、提醒行、排期编辑界面 | 调度、触发、时区换算、系统通知投放 |
| 权限/信任 | 权限范围选择、审批单、信任规则页、审计列表 | 真正的权限校验、沙箱、策略执行 |
| 桌面集成 | 托盘菜单视觉稿、热键设置控件、通知偏好界面 | 托盘常驻、热键注册、开机启动、系统通知 |
| 语音 | 语音会话条、波形、转写与确认状态 | VAD、ASR、TTS、打断、音频设备 |
| 集成 | MCP 连接状态、工具清单、健康度页面 | MCP 客户端、认证、协议、重连 |
| 成本/可观测 | 预算条、成本卡、审计/trace 页面 | 计量、日志采集、上报、脱敏 |
| 本地化 | RTL / 长文案 / 密度适配的**排版** | 文案翻译、地区数据、货币换算 |

### 7.3 组件与宿主的接缝（UI 契约）

1. **输入**：展示型组件以 `ItemsSource` / 只读模型 + 状态枚举接收数据；不主动拉取数据。
2. **输出**：交互以事件（`ActionRequested`、`DecisionChanged`、`Dismissed`…）上报意图；不自作主张执行。
3. **状态机**：组件内置完整视觉状态（Loading / Empty / Error / Partial / Running / Paused / NeedsInput / Done / Failed），宿主只负责切换。
4. **无副作用**：不写文件/数据库、不发网络/模型请求、不注册系统能力、不持有全局单例。
5. **可嵌入**：不假设路由、全局快捷键或固定窗口尺寸；在侧栏、浮层、整页、移动壳层中都可用。
6. **示例数据**：gallery/文档页使用确定性示例数据，展示"数据长什么样"即可，不模拟业务逻辑。

### 7.4 为什么不做（三个理由）

- **复用性**：UI 与 Agent 后端解耦，才能一套组件对接多家模型/工具生态与多种前端栈。
- **稳定性**：组件库的版本节奏与运行时/数据层解耦，避免"UI 升级带来行为变化"。
- **边界清晰**：把"能看/能点"和"能做/能做对"分开验收，UI 质量可度量（截图、状态覆盖、性能），不被业务缺陷拖累。

---

## 8. 风险与取舍

| 风险 | 表现 | 缓解 |
| --- | --- | --- |
| **范围爆炸** | 57 个 UI 组件 ≈ 235 人日，做不完 | 场景驱动：每个里程碑只承诺 3–5 条 UI 状态走查；P0 之外可裁剪 |
| **重复造轮子** | 把已有 `notification-center`/`agent-approvals` 又造一遍 | 先扩展现有（§3.3），只有"展示模型/交互模式"确实不同才新增；扩展目录与 294 项目录分开维护 |
| **范围外溢（最重要）** | 把 Agent 逻辑/数据/系统能力写进组件：组件内直接调模型、写数据库、注册热键、发通知 | 强制 UI 契约（§7.3）：无副作用、只发意图事件；PR/代码评审设"边界检查"，出现 `HttpClient`/`File.Write`/`Dispatcher` 业务逻辑即拒绝 |
| **性能债反噬** | 数据驱动组件叠加在"重建型 UI"上，长列表直接卡死 | 虚拟化/共享帧时钟先于长列表组件；新组件带性能护栏与压力页 |
| **设计漂移** | 新组件另起一套颜色/圆角/字号 | 强制 token + ControlTheme + 明暗截图 + 文档模板；PR 检查清单 |
| **组件与宿主脱节** | 组件只在 gallery 好看，宿主接不进去 | 每个组件交付"状态矩阵 + 接入示例（静态数据）+ 事件清单"；gallery 页至少展示空/加载/错误/内容四态 |
| **Pro/版权政策** | 参考了 Arc Pro 的实现 | 坚持公开文档 + 独立实现，ImplementationNotes 记录近似；不抓取 Pro 源码（沿用 `CONVENTIONS.md` §8） |
| **估工偏差** | 组件"控件+主题+页面"成本被低估 | 以 §3.3 的 14 组扩展现有项为基准校准（如 `activity-heatmap`≈S、`data-grid`≈L）；每里程碑复盘 |

**取舍原则**：宁可少做，也不要做"没有状态覆盖"的组件；宁可扩展现有，也不要新增重复项；**宁可把数据、调度、系统能力留给宿主，也不要把 Agent 逻辑写进控件**。

---

## 9. 验收与度量

### 9.1 单个扩展项的完成定义（在 `CONVENTIONS.md` §2 之上追加）

1. 控件 + `ControlTheme` + 页面（`ArcItemPage` 或 block 页）+ 明暗截图；
2. 出现在 `arc-extension-catalog.json`，slug 与现有 294 项不冲突；
3. 至少挂 1 个场景 id，并在文档页写清 When to use / When not to use / Keyboard / Motion / Implementation notes；
4. 键盘可达、`AutomationProperties` 完整、reduced-motion 分支；
5. 列表型组件必须虚拟化或明确说明数据规模上限；
6. 无硬编码色/字号，全部走语义 token；
7. 涉及 Agent 状态/写操作的组件必须覆盖"预演/审批/撤销/审计"等**展示状态**，并以事件上报意图，不实现执行；
8. **无副作用**：不读写宿主数据、不发网络/模型/系统调用、不注册系统能力；
9. 组件在明暗、8 种 accent、紧凑/舒适密度下都不破版。

### 9.2 UI 状态走查（里程碑门槛）

> 用静态示例数据在 gallery 页/最小宿主壳层中展示下列状态；**验收对象是 UI 状态与交互，不涉及数据落库、系统通知或 Agent 执行**。

| # | 界面 | 必须展示的状态 |
| --- | --- | --- |
| 1 | 晨间简报 | 加载 / 部分加载 / 空 / 错误 / 有数据；建议的采纳/忽略/稍后态 |
| 2 | 快速捕捉 | 输入 / 解析中 / 解析成功（chips 可编辑）/ 歧义 / 解析失败 |
| 3 | 提醒中心 | 逾期 / 今天 / 即将 / 已延后分组；空态；延后菜单与批量操作态 |
| 4 | 审批单 | 预演 / 改参 / 高风险 / 过期 / 已拒绝 / 已批准 |
| 5 | 计划审批 | 步骤的等待/执行/失败/跳过状态；审批范围（一次/本任务）；撤销入口 |

### 9.3 质量与性能护栏

| 指标 | 目标 | 测法 |
| --- | --- | --- |
| 页面切换 | ≤ 20 ms/项（`docs/05` 已把 366 ms 降到 15.8 ms，新页面不得回退） | 现有 probe 脚本 |
| 长列表 | 10,000 行可滚动、内存稳定、无每次选中全量重建 | 新增压力页 + 帧时间采样 |
| 常驻动画 | 隐藏/不可见时 timer 停止；空闲 CPU ≈ 0 | timer 计数探针（`docs/04` §7 实验 1） |
| 状态覆盖 | 每个新组件 ≥4 个视觉状态（空/加载/错误/内容），Agent 组件覆盖 §2.4 的状态机 | 页面清单逐项勾选 |
| 目录审计 | 现有 294 项 + 新增项全部可达，`missing=0`、`pageFailures=0` | `--audit` 扩展 |
| 截图 | 每个新页面明暗各 1 张，无 `SHOT WARNING` 裁切 | `tools/shots.ps1` |
| 可访问性 | 新控件 100% 有 Automation 名称/角色；键盘可完成主路径 | 人工走查 + AutomationProperties 统计 |
| 主题/密度 | 8 accent × 明暗 + 紧凑/舒适不破版 | 截图矩阵抽查 |

### 9.4 怎样算"扩展成功"

不是"新增了 N 个组件"，而是：

1. §9.2 的 5 条 UI 状态走查全部通过，且不需要临时私有控件补丁；
2. 新增组件中 ≥ 60% 是"生活域组件/block"（而非又一批通用展示件）；
3. 长列表/常驻场景的性能不劣于 `docs/05` 的 P0 修复后基线；
4. 同一组件只替换静态示例数据即可在不同宿主壳层复用（无业务逻辑耦合）；
5. 新组件能被第三方（不读源码）仅凭文档页在 30 分钟内用起来。

---

## 附录 A：数据来源与统计口径

| 数据 | 来源 | 口径 |
| --- | --- | --- |
| 294 项目录 | `docs/arc-catalog.json` | `componentCategories` 128 + `specialGroups` 55 + `blockGroups` 111；与 `src/Apps/Arc.Avalonia.Demo/Catalog/arc-catalog.json` 同源 |
| 294 项路由 | `agent-life-tools/01-inventory.zh.md` §2/附录 B | 166 个独立条目页 + 88 个章节标题匹配 + 40 个别名 = 294（按 `CatalogAudit` 同一算法静态复算，未运行构建） |
| 24 个项目 | `src/` 目录 | 基础层 1 + Demo 1 + Gallery 1 + 特性项目 10 + Special 5 + Blocks 6 |
| 219 页 / 438 图 | `shots/light`、`shots/dark` 实测文件数；`README.md`、`docs/03` §4 | 分析基线：166 个独立条目页 + 53 个共享页 = 219；P0 落地后实测 233 页 / 466 图（见 `07-p0-ui-implementation.zh.md` §4），294 项目录审计不受影响 |
| 控件类 236 | `agent-life-tools/01-inventory.zh.md` §3 | 411 个公共类声明（407 唯一名）中按继承链判定 236 个控件类（234 唯一名）；另 100 个 internal 类 |
| 516 个 `.cs` / 17.7 万行 / 112 个 `.axaml` | `docs/04` §0.2（不含 `obj`） | 审查时点数据，扩展后需重新统计 |
| 703 处 AutomationProperties | `docs/04` §2.7 | 审查时点数据；当前工作树实测 722 处，扩展后需重新统计 |
| 零虚拟化 / 11 套动画驱动 / 77 个 Render | `docs/04` §2.6、§0.2、§R1–R2 | 同上 |
| 57 项 UI 扩展 / 39 场景 | `arc-extension-catalog.json` | 由 02/03 两份提案 JSON + Lead 评审（5 项补充、4 项优先级调整）合并；**目录只含 UI 交付物**（`scope.uiOnly = true`），v1 的宿主侧字段已移除（变更记录见附录 D）。合并脚本 `.recon/merge_ext_catalog.py` 可复跑 |
| 人日 | 上述目录 + §0.4 / §6 | S=1 / M=3 / L=8；**只计 UI 完成定义**，不含宿主应用的数据/调度/通知/权限/模型调用，也不含设计/测试人力 |

> 历史文档中出现的 "441 张截图"（`docs/04` §2.8）是审查时点数字；当前工作树实测为 438（219×2）。`docs/01-arc-analysis.zh.md` 的 "127 组件 / 110 区块" 早于最终范围，当前以 catalog 的 128 / 55 / 111 为准。新增页面后这些数字都会变化：正文与附录对关键数字标注"审查时点"与"当前实测"；其余数字以 `docs/04` 快照为准，扩展后重新统计。

## 附录 B：配套文件索引

| 文件 | 内容 | 维护者 |
| --- | --- | --- |
| [`agent-life-tools/01-inventory.zh.md`](agent-life-tools/01-inventory.zh.md) | 294 项全量盘点、源码级控件统计、25 组关键能力深读 | inventory-auditor |
| [`agent-life-tools/02-life-gaps.zh.md`](agent-life-tools/02-life-gaps.zh.md) | 生活域场景矩阵、缺口分析、提案设计说明 | life-domain-analyst |
| [`agent-life-tools/02-life-proposals.json`](agent-life-tools/02-life-proposals.json) | 生活域扩展提案（原始，待合并） | life-domain-analyst |
| [`agent-life-tools/03-agent-gaps.zh.md`](agent-life-tools/03-agent-gaps.zh.md) | Agent 生命周期覆盖、缺口专题、提案设计说明 | agent-ux-analyst |
| [`agent-life-tools/03-agent-proposals.json`](agent-life-tools/03-agent-proposals.json) | Agent 交互扩展提案（原始，待合并） | agent-ux-analyst |
| [`agent-life-tools/04-qa-review.zh.md`](agent-life-tools/04-qa-review.zh.md) | 独立事实核查报告（**审查 v1 "UI + 运行时" 版**，范围修订前的历史基线；v2 复核见 05） | qa-reviewer |
| [`agent-life-tools/05-ui-scope-review.zh.md`](agent-life-tools/05-ui-scope-review.zh.md) | v2 复核：**纯 UI 范围**合规、运行时内容移除、数字/引用/路线图一致性 | qa-reviewer |
| [`07-p0-ui-implementation.zh.md`](07-p0-ui-implementation.zh.md) | P0 14 项 UI 组件的实现报告：控件/页面/API/状态/事件、构建与审计结果、已知限制 | Lead + 3 implementers |
| [`agent-life-tools/06-p0-implementation-review.zh.md`](agent-life-tools/06-p0-implementation-review.zh.md) | P0 实现独立复核：构建/审计/smoke、纯 UI 边界、token/无副作用、页面可达性 | qa-reviewer |
| [`arc-extension-catalog.json`](arc-extension-catalog.json) | 合并后的可执行 UI 扩展目录（本报告 §4–§5 的数据源，`scope.uiOnly = true`） | Lead |

## 附录 C：术语表

| 术语 | 含义 |
| --- | --- |
| **目录项 / catalog item** | Arc 目录中的 294 个条目之一；gallery 侧栏按此渲染 |
| **常规组件 / component** | 可复用交互控件（Button、Calendar、Toast…） |
| **Special component** | 自绘/动画型特殊组件（agent-status、voice-orb、glass-card…） |
| **Block** | 页面级组合（command-palette、notification-center…），有示例数据但无真实数据层 |
| **P0/P1/P2** | P0 = 不补就无法做出可用的 Agent **界面**；P1 = 显著提升；P2 = 锦上添花 |
| **S/M/L 工作量** | S ≤ 1 人日；M = 2–4 人日；L = 1–2 周（按"控件 + 主题 + 页面 + 明暗截图"完成定义） |
| **预演（dry-run）** | 执行写操作前展示将发生的变化与影响范围（**展示**，执行由宿主） |
| **状态 UI 闭环** | 预演 → 审批 → 执行中 → 撤销 → 审计 的**界面状态覆盖**；后端执行链路由宿主实现 |
| **宿主应用 / host** | 接入本组件库的产品应用；负责 Agent 逻辑、数据、调度、通知、权限、模型/工具调用 |

---

## 附录 D：修订记录

| 版本 | 日期 | 变更 |
| --- | --- | --- |
| v1 | 2026-10-26 | 初版：按"UI + 运行时"分析，57 项扩展 + 10 个运行时服务，290 人日、3–5 人 14–17 周；QA 报告见 [`04-qa-review`](agent-life-tools/04-qa-review.zh.md) |
| **v2** | **2026-10-28** | **按"AI Agent UI 组件库"定位收敛为纯 UI**：① 移除 §7 运行时服务建设，改为 §7 "UI 边界 / 宿主责任"；② 扩展目录删除 `runtimeServices` 与指向服务的跨域依赖，新增 `scope.uiOnly` 与 `hostResponsibilities`；③ 工作量 290→**235 人日**（仅 UI），团队 3–4 人 / 14–17 周；④ §3.5/§8/§9 改为"宿主责任 / UI 状态走查"；⑤ 路线图 M0/M5 去运行时化，最小集改为 P0 14 项 72 人日；⑥ 新增本修订记录与 [`05-ui-scope-review`](agent-life-tools/05-ui-scope-review.zh.md) 复核 |
| **v2.1** | **2026-10-28** | **P0 14 项 UI 组件落地**：新增 26 个源文件（12 个控件文件，含共享 `ArcUi`；共 14 个控件类 + 14 个 Gallery 页面文件），全量构建 **0 error**（9 个既有 warning，新增文件 0 warning），`--audit` pages=233 / items=294 / missing=0；实现说明见 [`07-p0-ui-implementation`](07-p0-ui-implementation.zh.md)，复核见 [`06-p0-implementation-review`](agent-life-tools/06-p0-implementation-review.zh.md) |
