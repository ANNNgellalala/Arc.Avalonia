# 生活内容域：场景、缺口与扩展提案（02）

> 任务：`task-2`（生活内容域分析，与 Agent 交互域分离）。
> 输入：`docs/arc-catalog.json`（294 项）、`README.md`、`docs/01-arc-analysis.zh.md`、`docs/03-implementation.zh.md`、`docs/04-design-and-performance-review.zh.md`，以及 `src/*/Pages`、`src/*/Controls` 中与生活场景相关的页面与控件。
> 产物：本文件 + [`02-life-proposals.json`](02-life-proposals.json)（24 个提案，schema `arc-extension-proposals/v1`）。
> 边界：Agent 交互域（计划/审批/记忆/语音/运行时）由 `docs/agent-life-tools/03-*.md` 与 `03-agent-proposals.json` 负责；本文件只在“生活内容依赖 Agent 运行时”的地方引用它，不重复提 Agent 组件。
>
> **范围说明（v2，2026-10-28）**：本库定位为**纯 UI 组件库**。本文的 24 项提案全部是 UI 组件/页面；文中提到的“Agent 运行时”仅作为**宿主应用**依赖说明，不属于本库交付物。

---

## 0. 摘要

**一句话结论：294 项组件已经把“生活工具的界面观感”补齐了，但没有补齐“生活工具的时间语义与数据闭环”。** 真正卡住可用性的不是缺卡片、缺图表，而是缺“到期/重复/提醒/台账”这一层，以及它背后的持久化、调度、通知与本地隐私能力。

| 指标 | 结果 |
| --- | --- |
| 场景 | 24 个真实日常场景（≥ 任务要求的 15 个） |
| 提案 | 24 个（20 个 block + 4 个 component），均在 20–30 区间 |
| 优先级 | P0 7 / P1 13 / P2 4 |
| slug 冲突 | 与现有 294 个 slug 冲突 0；与 `03-agent-proposals.json` 冲突 0 |
| `composes` 校验 | 152 个引用全部有效（136 个现有 slug + 16 个本文件 slug），0 非法 |
| 工作量（UI 口径） | P0 ≈ 36 人日、P1 ≈ 72 人日、P2 ≈ 12 人日，合计 ≈ 120 人日（中位估算） |
| 最大空白 | 提醒与重复、个人财务台账、饮食、出行、家居/设备、宠物、持久化/调度/通知/隐私 |

三条最值得带走的判断：

1. **日历与时间控件是最大的“有但不适配”。** `ArcCalendar` 只有日期、区间、`MinDate/MaxDate/DisabledDates`，没有任何事件/标记能力（`src/Arc.Avalonia.Pickers/Controls/ArcCalendar.cs:20-45`）；`week-calendar` 的事件是源码里写死的 8 条示例（`src/Arc.Avalonia.Blocks.Team/Pages/WeekCalendarPage.cs:35-45`），拖拽新建/移动/缩放明确未复刻（同文件 `:74-77`）。而时间模型是 naive `DateTime`/`TimeSpan` 且固定 `en-US`（`src/Arc.Avalonia.Pickers/Controls/ArcDateMath.cs:3-7`），跨时区、夏令时、重复规则都无从表达。
2. **111 个 block 是“可看的样例”，不是“可运营的数据层”。** 所有 block 的 `ImplementationNotes` 都写着 sample data / local state；全仓没有 SQLite、没有 HTTP 客户端、没有调度器（`Sqlite` 0 处、`HttpClient` 0 处、`Cron` 0 处、`Recurrence` 相关 9 处全是文案）。生活工具一旦有真实流水（账单、交易、健康记录），`docs/04` 的 R4 结论就会立刻生效：整库几乎零虚拟化（仅 EmojiPicker 行级），DataGrid/TreeTable 选中/排序走整表重建（`docs/04` §0.2、§2.6、§4 R4、G-P0-5）。因此生活域扩展必须同时给出“数据契约 + 虚拟化/持久化路径”，否则只是又一批截图。
3. **最划算的扩展是“提醒–议程–到期”三件套，而不是再造 20 个卡片。** P0 的 7 项把现有 `calendar`、`notification-center`、`file-dropzone`、`sortable-data-table`、`checkbox`、`badge` 串成可用的日常闭环；其中排期与通知投递复用 Agent 域提案（`routine-schedule-editor`、`notification-routing`、`background-run-dock`），生活域不重复造 RRULE 与托盘。

---

## 1. 产品假设、范围与方法

### 1.1 产品假设

用本仓库的 UI 基座构建一个**桌面端、本地优先、Agent 主动参与**的个人日常生活助手：

- Agent 能主动规划（今天做什么、账单何时到期、药什么时候吃）、记忆（上次联系谁、上次体检哪项异常）、执行（建提醒、归档文件、生成清单），并在有副作用前让用户审批；
- 覆盖日程任务提醒、健康、财务、家庭、饮食、出行、记录、社交、专注、智能家居等日常域；
- 用户主要在自己的电脑上使用，数据默认留在本地，通知要能在窗口关闭后仍触达。

### 1.2 本文件的范围与边界

| 包含 | 不包含（归 03 号 Agent 交互域） |
| --- | --- |
| 生活内容的场景、数据结构需求、页面组合、领域组件 | Agent 计划/审批/记忆/工具调用/转录/语音/成本/审计的交互组件 |
| 提醒/账单/用药/证件/清单等生活对象如何被看见与操作 | 多步工作流可视化、失败恢复、权限沙箱、MCP、路由策略 |
| 生活域对持久化、调度、通知、隐私的**依赖清单** | 这些能力的 Agent 侧 UI（由 03 负责），本文件不重复提案 |
| 生活域新组件/新 block 的解剖与验收 | 已有 Agent 组件的修改建议（除非生活场景直接复用） |

本文件对 03 号提案的依赖（合并时必须一起看）：

- `routine-schedule-editor`（P0，Pickers）：提供 RRULE/时区/下次运行/错过策略。生活域的提醒、账单、用药、家务、订阅都依赖它；**本文件不重复提排期组件**。
- `notification-routing`（P1，BlocksApp）：提供事件 × 通道（应用内/系统/托盘/摘要/静默）。生活域的提醒中心依赖它，**本文件不重复提通知策略**。
- `background-run-dock` / `desktop-agent-settings`（P0/P1）：托盘、关窗常驻、自启、休眠策略。生活提醒“窗口关了还能响”依赖它们。
- `agent-attention-inbox`（P0）：Agent 的“等你处理”队列。生活域的 `reminder-center` 只处理生活提醒，**不与审批队列合并**；两者可以互相计数，但不能共用一个对象模型。

### 1.3 方法与证据口径

- **catalog 层**：脚本解析 `docs/arc-catalog.json` 的 294 项（128 components / 55 special / 111 blocks），按 category 与 slug 建索引；所有提案的 `composes` 只引用这 294 项或本文件内的 slug（见 §12 校验）。
- **页面层**：读 `src/*/Pages` 中与生活场景相关的 40+ 个页面，判断“这个页面演示了什么、数据从哪来、有没有写操作的闭环”。本文件把 `ImplementationNotes` 当作**作者自陈的能力边界**，这是最可靠的“支持到什么程度”的证据。
- **控件层**：读 `src/*/Controls` 的公开 API（StyledProperty/事件），判断能否通过组合/变体复用，而不是只看页面截图。
- **性能与完成定义**：引用 `docs/04` 的 R1/R4/R5/R7 与 G-P0-3/4/5/6/7；完成定义按 `CONVENTIONS.md:55-58`（构建通过、上页面、明暗两套渲染、键盘可用、只用已文档化属性）与团队口径“控件 + ControlTheme + 页面 + 明暗截图”。
- **授权**：所有新增项都是独立设计/独立实现，参考公开行为与视觉描述，不抓取、不照抄任何商业或 Pro 源码；这一点与仓库既有 Pro 条目的处理方式一致（`README.md:204-209`、`docs/03-implementation.zh.md:180-181`）。

---

## 2. 结论先行：生活域的五个判断

### 判断 1：现有组件覆盖“界面”，不覆盖“生活对象”

294 项里有一流的按钮、日历、图表、聊天、文件拖放、账单页、看板、专注控制中心，但没有一个对象能表达：

- 一条“每天 8:00 提醒我吃药，吃到 11 月 30 日，漏服要标记”的提醒；
- 一笔“每月 5 日自动扣、下次 12 月 5 日、可标记已付”的账单；
- 一份“2031 年 3 月到期、归属人是我、续办要提前 90 天提醒”的护照；
- 一条“每周三、周六，轮到爸爸做”的家务。

这些不是“再画一个组件”能解决的，它们需要 **提醒对象 + 排期规则 + 状态机 + 持久化** 四件套。

### 判断 2：日历/时间是最大的“有但不适配”

| 现有项 | 已支持 | 生活场景缺什么 |
| --- | --- | --- |
| `calendar` | 单选日期、区间高亮、`MinDate/MaxDate/DisabledDates`、键盘 | 事件/提醒标记、每日数量/密度、点击某天新建、多日事件的跨格渲染 |
| `date-picker` / `date-range-picker` | 日期/区间与预设、草稿 Apply | 区间与“重复规则”的组合、自然语言输入（“下周三”） |
| `time-picker` | 24h 值、12/24 显示、`MinuteStep` | 时区、提醒提前量、跨午夜 |
| `week-calendar` | 七天网格、全天行、议程视图、彩色日历 | 事件数据模型、拖拽新建/移动/缩放、重复事件展开、提醒关联 |
| `date-reel` / `time-dial` | 滚轮式选日期/时间 | 同样的时区与提醒语义缺口 |

证据：`ArcCalendar.cs:20-45` 的属性表里没有任何事件集合；`ArcCalendarSurface.cs:208-332` 只画星期、区间、今天点、焦点环；`WeekCalendarPage.cs:35-45` 的事件是静态数组，`:74-77` 明说拖拽未复刻。

### 判断 3：提醒/重复/到期/台账是最大空白，也是 Agent 主动性的载体

Agent 的“主动”在生活域不是聊天，而是**在该提醒的时候提醒、在该准备的时候准备**。没有提醒对象与到期状态，Agent 只能被动问答。全仓的“提醒”只出现在两处文案：`RangesAndTimePage.cs:144` 的 `"Reminder (24 h)"` 标签、`DateReelPage.cs:16` 的“send, reminder or post”说明；没有提醒数据、没有触发、没有 snooze 语义。

### 判断 4：生活域扩展应以 block 为主、组件为辅

24 个提案里 20 个 block、4 个 component。原因：生活场景的复杂度在“把多个域拼成一屏并保持四态”，而不是“再造一个按钮”。4 个组件都瞄准真正缺失的原子能力：

- `reminder-row`：生活行动行（到期 + 重复 + 完成/延后）；
- `weather-forecast`：天气展示（全仓 0）；
- `habit-streak-strip`：7/30 天连续条（`activity-heatmap` 是年视图，`activity-rings` 是三目标环）；
- `reference-band-chart`：参考范围带/目标线（`line-chart` 无 Band，`gauge` 只能单值）。

### 判断 5：非 UI 能力是隐藏前置，工作量不比 UI 小

生活工具的“最后一公里”在 UI 之外：持久化、重复调度（含休眠唤醒、错过补跑、DST）、系统通知/托盘、本地文件保险箱与加密、隐私同意、天气/账单/健康数据接入。现有仓库对这些的支持是 0：

- `TrayIcon` 0 处、`HttpClient` 0 处、`Sqlite` 0 处、`Cryptography` 0 处、`FileSystemWatcher` 0 处；
- 文件选择有（`ArcFileDropzone.cs:308-317` 用 `StorageProvider.OpenFilePickerAsync`），但只记录名称/大小，不落盘（`FileUploadPage.cs:58`）；
- 设置页只改本地状态（`SettingsPage.cs:51`），没有持久化；
- 所有 block 的示例数据都是内存里的 sample（各页 `ImplementationNotes`）。

因此本文件的 effort 只覆盖“控件 + 主题 + 页面 + 明暗截图”的 UI 口径；真正落地还要为 P0 叠加数据层、调度、通知与加密的非 UI 工作（§10、§11）。

---

## 3. 场景矩阵（24 个）

每个场景按同一结构写：**用户目标 / Agent 参与方式 / 界面元素 / 可复用 slug / 缺口与提案**。可复用 slug 以现有 294 项为主；属于本文件新提案的项标“（新）”，并同时出现在“缺口与提案”一栏。

### 3.1 `morning-brief` 晨间简报

- **用户目标**：起床后 5 分钟内知道今天要做什么、什么快到期、身体和钱有没有异常，并决定第一件事。
- **Agent 参与**：夜间汇总日程、账单、包裹、健康与天气，生成 3 条以内的建议；用户确认后再建提醒或改日程。
- **界面元素**：问候与日期、天气、今天议程（事件/提醒/任务/服药）、健康环、预算/账单摘要、包裹、一条主建议。
- **可复用 slug**：`metric-card`、`activity-rings`、`card`、`sparkline`、`timeline`、`empty-state`、`agent-status`（仅状态，不展开 Agent 交互）。
- **缺口与提案**：没有“今天”的聚合视图与生活建议卡 → `day-agenda`、`daily-brief`、`reminder-row`、`weather-forecast`。

### 3.2 `medication-plan` 服药计划

- **用户目标**：按医嘱在正确时间吃药，漏服能补记，快吃完能续方，家人能确认。
- **Agent 参与**：到点提醒；根据旅行时区调整；发现漏服时询问补服还是跳过；库存不足时生成续方提醒。
- **界面元素**：今日剂量表（药名/剂量/餐前餐后/时间）、已服/跳过/稍后、课程进度、续方库存、历史记录、照护人备注。
- **可复用 slug**：`reminder-row`（新）、`usage-meter`、`progress`、`timeline`、`badge`、`empty-state`。
- **缺口与提案**：没有药物/剂量/服药记录结构，也没有“已服/漏服”的状态机 → `medication-schedule`、`reminder-row`；排期依赖 03 号 `routine-schedule-editor`，通知依赖 03 号 `notification-routing`。

### 3.3 `bill-due` 账单到期

- **用户目标**：不错过任何一笔到期账单，知道本期要花多少，已付的能确认。
- **Agent 参与**：提前 7/3/1 天分层提醒；识别“本期总额异常”；对自动扣款失败给出处理建议；把账单与预算关联。
- **界面元素**：本期合计、未来 30 天合计、按到期日分组的账单行、自动扣款标记、已付确认、账单详情抽屉。
- **可复用 slug**：`sortable-data-table`、`filter-toolbar`、`countdown`、`money-input`、`badge`、`toast`。
- **缺口与提案**：`billing-overview` 是 SaaS 套餐/发票，`usage-billing` 是计量计费，`budget-variance` 是部门预算，都不是个人账单台账 → `bill-due-list`、`reminder-center`、`reference-band-chart`（预算线）、`subscription-tracker`。

### 3.4 `weekly-meal-plan` 每周备餐

- **用户目标**：周日花 20 分钟排好一周三餐，买菜不遗漏、不浪费，尽量兼顾营养与家人口味。
- **Agent 参与**：根据冰箱库存/常做菜谱/营养目标给出候选；按人数缩放份量；自动生成按货架分区的采买清单。
- **界面元素**：周网格（早/午/晚 × 7 天）、菜谱卡（时间/份数/标签）、营养概览、生成清单动作、常做菜谱抽屉。
- **可复用 slug**：`week-calendar`、`calendar`、`chip-group`、`card`、`donut-chart`、`empty-state`。
- **缺口与提案**：全仓没有菜谱/备餐/营养结构 → `meal-planner`、`grocery-list`；这属于真正的空白域（`Recipe`/`Meal`/`Grocer` 在 `src` 中 0 匹配）。

### 3.5 `trip-itinerary` 旅行行程

- **用户目标**：出行前把交通、住宿、预约、证件集中好；行程中按天看下一步，临时变更能快速调整。
- **Agent 参与**：监测航班/天气变化并给出改签或改期建议；提醒值机与证件；把预订确认号归档。
- **界面元素**：目的地/日期范围/倒计时/同行人、逐日时间线、交通/住宿/预约条目、证件与预订号、天气、每日下一步。
- **可复用 slug**：`date-range-picker`、`timeline`、`stepper`、`booking-pill`、`document-extract`（可选抽取预订确认）、`empty-state`。
- **缺口与提案**：`delivery-tracker` 只跟单个订单，`choropleth-explorer` 是分析地图，没有行程结构 → `trip-itinerary`、`weather-forecast`、`document-vault`；地图不做承诺（离线无瓦片），用地址文本 + 外部打开。

### 3.6 `home-repair` 家庭维修

- **用户目标**：家电坏了能报修、约上门、查保修、留好单据，别在保修期内自费。
- **Agent 参与**：从购买日期/发票推断保修；到保养周期主动提醒；整理服务商与历史维修记录。
- **界面元素**：资产清单（设备/位置/购买日/保修到期）、报修单、预约时间、服务商、维修时间线、费用与单据。
- **可复用 slug**：`document-vault`（新）、`calendar`、`timeline`、`sortable-data-table`、`badge`、`reminder-row`（新）、`empty-state`。
- **缺口与提案**：没有家庭资产/保修/服务记录语义；`project-board` 是软件任务 → `home-maintenance`、`document-vault`。

### 3.7 `mood-journal` 情绪日记

- **用户目标**：每天一分钟记录心情、事件与一句话，月回顾时能看出情绪与睡眠/社交的关系。
- **Agent 参与**：在合适的时间轻问一句；从记录中提取主题（工作/家庭/健康）；发现连续低落的趋势时建议求助，而不是下诊断。
- **界面元素**：日期、5 档心情 + 文字标签、一句话总结、标签、照片、当天事件、历史日历/热力图。
- **可复用 slug**：`rich-text-editor`、`tag-input`、`carousel`、`activity-heatmap`、`card-to-article`、`date-picker`。
- **缺口与提案**：没有“心情 + 日记”结构 → `daily-journal`；评分可复用 `rating` 但必须配文字标签（不能只有星星）；隐私要求最高（本地加密、可删除、不用于训练）。

### 3.8 `focus-block` 专注时段

- **用户目标**：进入 50 分钟专注，明确本次目标，压掉非紧急打扰，结束后知道完成度。
- **Agent 参与**：根据日程找空档；开始前确认目标；进行中把非紧急事项收进“稍后处理”；结束后生成下一步。
- **界面元素**：目标输入、时长拨盘、进行中的剩余时间/当前任务、环境音、允许打扰的人、中断记录、结束复盘。
- **可复用 slug**：`control-center`（专注拨盘）、`countdown`、`media-player`、`now-playing`、`switch`、`timeline`、`progress`。
- **缺口与提案**：`control-center` 只有工作区快捷设置（频道/音量/字号），没有完整会话 → `focus-session`；计时器必须单帧时钟 + 可见性门控（`docs/04` R1/R5/G-P0-2），不要为倒计时再造逐帧 timer。

### 3.9 `parcel-tracking` 包裹快递

- **用户目标**：几个包裹分别到哪了、什么时候到、需要代收或改约时提前处理。
- **Agent 参与**：汇总多承运商状态；异常件（延误/退回）主动提示；根据在家时间建议改约。
- **界面元素**：包裹列表（运单/承运商/状态/ETA/代收点）、异常标记、详情时间线、改约/代收动作。
- **可复用 slug**：`delivery-tracker`、`timeline`、`badge`、`filter-toolbar`、`empty-state`。
- **缺口与提案**：`delivery-tracker` 是单订单且明说没有承运商 webhook（`DeliveryTrackerPage.cs:16,50`）→ `package-inbox`；MVP 只做手工单号，文案不能暗示自动刷新。

### 3.10 `subscription-audit` 订阅管理

- **用户目标**：知道每月/每年在订阅上花多少，哪些快续费、哪些涨价、哪些已经不用。
- **Agent 参与**：续费前提醒；识别“连续三个月未使用”的候选；对比涨价历史；生成取消步骤与保留理由。
- **界面元素**：月度/年度总成本、续费倒计时、订阅列表（周期/折算月成本/支付方式）、取消流程、涨价标记。
- **可复用 slug**：`countdown`、`sortable-data-table`、`cancel-flow`、`confirm-morph`、`money-input`、`filter-toolbar`、`badge`。
- **缺口与提案**：`billing-overview` 是单个 SaaS 账户，`cancel-flow` 只是挽留文案流 → `subscription-tracker`；涨价检测需要邮件/银行导入（非 UI），MVP 手工维护。

### 3.11 `id-expiry` 证件到期

- **用户目标**：身份证/护照/驾照/保险/签证到期前有足够时间续办，材料清单一次备齐。
- **Agent 参与**：90/30/7 天分层提醒；列出续办材料；把旧证件扫描件归档；旅行前检查有效期是否满足入境要求。
- **界面元素**：证件卡（类型/号码后四位/到期日/归属人）、状态徽标、材料清单、扫描件预览、续办入口。
- **可复用 slug**：`file-dropzone`、`file-upload`、`filter-toolbar`、`sortable-data-table`、`drawer`、`empty-state`、`badge`。
- **缺口与提案**：文件上传只记录名称（`FileUploadPage.cs:58`），没有到期/归属/类型/搜索 → `document-vault`、`reminder-center`。

### 3.12 `report-archive` 体检报告归档

- **用户目标**：体检/化验/影像报告不丢，指标能看趋势，异常值能单独提示并带去就诊。
- **Agent 参与**：抽取报告中的指标；与历史值对比；把异常项与就诊问题清单关联；提醒复查。
- **界面元素**：报告列表（类型/日期/机构）、指标趋势图（带参考范围）、异常徽标、报告预览、复查提醒。
- **可复用 slug**：`document-vault`（新）、`line-chart`（需扩展）、`gauge`、`badge`、`timeline`、`document-extract`（仅抽取审阅，不重复实现）。
- **缺口与提案**：`line-chart` 没有参考范围带（`ArcLineChart` 只有 Series/Labels/ShowGrid/ShowLegend/ShowArea/Smooth），`gauge` 只能单值 → `reference-band-chart`、`document-vault`、`medication-schedule`（用药与报告关联）。

### 3.13 `relationship-upkeep` 生日与关系维护

- **用户目标**：记住重要的人的生日/纪念日与上次联系，按合适的节奏问候，不靠群发。
- **Agent 参与**：生日前一周提醒并给礼物/问候建议；发现“三个月没联系”时轻提醒；记录对方近况（孩子、工作、健康）。
- **界面元素**：人物卡（头像/关系/生日/上次联系/下次提醒）、关系节奏设置、问候草稿、重要日期日历。
- **可复用 slug**：`team-directory`、`avatar`、`hover-card`、`badge`、`calendar`、`chat-thread`（仅草稿）。
- **缺口与提案**：`team-directory` 是同事目录（`TeamDirectoryPage.cs:29`），没有生日/联系节奏/私人备注 → 不单列组件：用 `card` + `avatar` + `badge` + `reminder-row` 组合；生日/纪念日作为 `reminder-center` 的一类提醒，人物近况用 `rich-text-editor` 做私人备注；避免为凑数造“又一个联系人卡”。

> 说明：原本准备提 `relationship-card` 组件，评审后降级为现有积木组合，理由见 §9 去重表。同理，本文件对 `expense-split`、`appointment`、`packing`、`family-calendar` 等“看起来可以再开一个 block”的方向做了收敛：能由现有 + 本文件提案组合出来的，不单列。

### 3.14 `home-energy` 家庭能耗

- **用户目标**：看懂本月电水气为什么高，找到异常设备，知道下个月大概多少钱。
- **Agent 参与**：结合天气与账单解释变化；发现异常日并建议检查设备；对比去年同期与预算线。
- **界面元素**：本月费用/用量、同比环比、日趋势、按设备/全屋、异常日标记、预算参考线、节省建议。
- **可复用 slug**：`bar-chart`、`line-chart`、`metric-card`、`usage-meter`、`filter-toolbar`、`empty-state`。
- **缺口与提案**：没有能耗/设备/费用对照结构 → `energy-usage`、`reference-band-chart`、`smart-home-panel`（可选设备联动）。

### 3.15 `pet-care` 宠物照护

- **用户目标**：喂食、遛弯、驱虫、疫苗、体重、兽医预约不漏；出门时能给照看人一份说明。
- **Agent 参与**：按周期提醒；记录体重与用药；出发前生成交接摘要。
- **界面元素**：宠物档案、今日任务、疫苗/驱虫周期、体重曲线、兽医联系、就诊记录、交接说明。
- **可复用 slug**：`reminder-row`（新）、`line-chart`、`document-vault`（新）、`usage-meter`、`timeline`、`avatar`、`empty-state`。
- **缺口与提案**：宠物域全空白（`pet` 相关代码只出现在示例文案）→ `pet-care`（P2）。

### 3.16 `grocery-run` 采买清单

- **用户目标**：按货架顺序买齐，边买边勾，缺货能替换，家人能同时补充。
- **Agent 参与**：从备餐计划生成清单；按历史购买习惯排序；缺货时建议替代品。
- **界面元素**：分区清单（蔬果/乳品/冷冻…）、勾选、数量+单位、备注、添加人、已购进度、缺货替换、分享。
- **可复用 slug**：`checkbox`、`input`、`number-field`、`chip-group`、`swipe-actions`、`progress`、`empty-state`。
- **缺口与提案**：`reorderable-list` 只有标题/详情/禁用（`ArcReorderableList.cs:24-31`），没有勾选/数量/分区 → `grocery-list`；共享需要同步（非 UI）。

### 3.17 `chore-rota` 家务轮值

- **用户目标**：重复家务有人做、轮换公平、完成可见，月底不吵架。
- **Agent 参与**：按周期生成待办并分配；到期提醒；本周完成情况汇总；临时换班。
- **界面元素**：按人/按周的卡片、家务（频率/轮值人/下次到期/完成历史）、本月每人完成数、换班。
- **可复用 slug**：`kanban-board`、`project-board`、`reminder-row`（新）、`avatar-group`、`calendar`、`empty-state`。
- **缺口与提案**：`kanban-board`/`project-board` 是团队软件任务语义（阶段、WIP、负责人），不表达轮换与公平性 → `chore-board`（P2）。

### 3.18 `sleep-trend` 睡眠与健康趋势

- **用户目标**：知道最近睡得好不好，和日间精力/咖啡/运动的关系，异常时该不该就医。
- **Agent 参与**：汇总可穿戴/手记数据；标记异常；建议调整；在就诊前生成摘要。
- **界面元素**：睡眠时长/入睡时间时间轴、参考范围、日间精力、关联因素、异常标记、导出/就诊摘要。
- **可复用 slug**：`line-chart`（需 Band）、`bar-chart`、`activity-heatmap`、`metric-card`、`reference-band-chart`（新）、`timeline`。
- **缺口与提案**：没有参考范围带与“数据 vs 范围”的读法 → `reference-band-chart`；不做医学建议，只做数据呈现。

### 3.19 `expense-split` 聚餐分摊

- **用户目标**：聚餐/合租后清楚谁付了多少、谁欠谁、结算后归档。
- **Agent 参与**：按人/按项分摊；计算最小转账次数；提醒未结算；关联收据。
- **界面元素**：参与者、费用项、分摊方式（均分/按项/按比例）、谁垫付、结算状态、收据。
- **可复用 slug**：`split-bar`、`avatar-group`、`money-input`、`sortable-data-table`、`confirm-morph`。
- **缺口与提案**：`split-bar` 只是 100% 占比图，不承载“人 × 金额 × 结算”的台账 → 可在 `expense-ledger`（P1）内做分摊子视图，或后续补 `expense-split` block（P2 候选）；本次不单列，避免与 `expense-ledger` 重复。

### 3.20 `smart-home-scene` 回家场景

- **用户目标**：回家/离家/睡眠一键调整灯光、空调与安防，设备离线时知道怎么办。
- **Agent 参与**：按时间/位置建议场景；异常（门锁未关、空调忘关）提醒；离家前检查。
- **界面元素**：房间分组设备卡、开关/亮度/温度、场景切换、危险设备二次确认、离线状态、最后已知状态。
- **可复用 slug**：`control-center`、`switch`、`slider`、`segmented-control`、`dialog`、`badge`、`card`。
- **缺口与提案**：`control-center` 是工作区快捷设置（专注模式/频道/音量/字号，`ControlCenterPage.cs:44-63`），没有设备/房间/场景模型 → `smart-home-panel`；无统一协议，只定义 UI 适配契约，真实连接由应用层插件完成。

### 3.21 `spend-capture` 随手记账

- **用户目标**：花完钱顺手记一笔，月底看分类占比与超支项。
- **Agent 参与**：从收据/短信抽取金额与分类；发现重复订阅；超支预警；生成月度摘要。
- **界面元素**：快速记一笔（金额/分类/日期/备注/收据）、月份切换、搜索筛选、分类占比、超支项。
- **可复用 slug**：`money-input`、`sortable-data-table`、`filter-toolbar`、`donut-chart`、`drawer`、`empty-state`。
- **缺口与提案**：`wallet-card` 只有示例余额与活动（`WalletCardPage.cs:52`），`budget-variance` 是部门预算 → `expense-ledger`、`budget-board`。

### 3.22 `appointment-prep` 就诊准备

- **用户目标**：就诊前把症状、用药、近期报告与要问的问题准备好；就诊后记录结论、处方与复查。
- **Agent 参与**：汇总近 30 天症状/用药/指标；生成问题清单；记录医生结论与下次复查。
- **界面元素**：症状与时间线、当前用药、近期报告、问题清单、就诊记录、处方、复查提醒。
- **可复用 slug**：`timeline`、`medication-schedule`（新）、`document-vault`（新）、`rich-text-editor`、`reminder-row`（新）、`empty-state`。
- **缺口与提案**：没有“就诊前准备/就诊后记录”的结构 → 由 `medication-schedule` + `document-vault` + `daily-journal` 组合；本次不单列 `appointment` block，避免把“日程 + 表单”再包一层。

### 3.23 `outdoor-plan` 周末户外

- **用户目标**：周末找个天气合适的窗口出门，确认地点、同行人、装备与集合时间。
- **Agent 参与**：对比周六/周日天气与同行人日程；建议地点与出发时间；生成装备清单；出发前确认。
- **界面元素**：天气窗口对比、候选地点、同行人可用时间、装备清单、集合时间/地点、出发前检查。
- **可复用 slug**：`weather-forecast`（新）、`availability-picker`、`date-picker`、`checkbox`、`reorderable-list`、`empty-state`。
- **缺口与提案**：天气展示缺失、户外装备清单是通用 checklist → `weather-forecast`；外出计划可作为 `day-agenda` + `trip-itinerary` 的轻量组合，本次不单列。

### 3.24 `family-logistics` 家庭日程协同

- **用户目标**：家人之间的接送、聚会、家务、外出安排不冲突，谁负责什么一目了然。
- **Agent 参与**：汇总每个人的日程；发现冲突并给替代方案；按人推送提醒；生成周家庭简报。
- **界面元素**：家庭周视图、成员筛选、冲突标记、接送/家务/聚会条目、负责人、周简报。
- **可复用 slug**：`week-calendar`、`avatar-group`、`segmented-control`、`reminder-row`（新）、`empty-state`、`badge`。
- **缺口与提案**：`week-calendar` 的事件是静态示例、不能多人/多日历合并 → `day-agenda`、`chore-board`、`reminder-center`；家庭数据模型（成员/角色）需要非 UI 能力。

---

## 4. 能力域覆盖矩阵

状态口径：**覆盖** = 现有项可直接支撑该域的主要界面；**部分** = 有视觉/容器但缺生活语义或写操作闭环；**空白** = 没有可复用的对象模型或展示结构。证据列优先给源码路径与行号，其次给 `docs/04` 章节。

| # | 能力域 | 状态 | 现有覆盖与证据 | 关键缺口 |
| --- | --- | --- | --- | --- |
| 1 | 日程与事件 | **部分** | `week-calendar` 七天网格/议程（`WeekCalendarPage.cs:35-45` 静态数据）；`calendar` 日期/区间（`ArcCalendar.cs:20-45`）；`date-picker`/`date-range-picker`/`time-picker`/`date-reel`/`time-dial` | 事件模型、日历事件标记、按天议程、重复事件展开；`calendar` 没有 Markers 属性 |
| 2 | 提醒与重复 | **空白** | `notification-center` 只有已读/忽略（`NotificationCenterPage.cs:51`）；`inbox-triage` 的 snooze 时间不可编辑（`InboxTriagePage.cs:51`）；`Recurrence` 仅文案出现 | 提醒对象、到期状态、snooze/改期、RRULE、提前量、错过补跑；需 `reminder-row`、`reminder-center` |
| 3 | 待办·家务·习惯 | **部分** | `kanban-board`/`project-board`（团队任务）、`onboarding-checklist`（仅上手流程，`ContentFlowsPage.cs:63`）、`reorderable-list`（无勾选/到期，`ArcReorderableList.cs:24-31`）、`checkbox`、`activity-heatmap`（年节奏） | 个人/家庭任务与轮值、重复待办、连续天数；需 `chore-board`、`habit-streak-strip` |
| 4 | 健康与用药 | **部分** | `activity-rings`（三目标日环）、`line-chart`/`bar-chart`/`gauge`（`Thresholds`/`Target`）、`activity-heatmap`、`metric-card` | 药物/剂量/服药记录、参考范围带、就诊准备、报告归档；需 `medication-schedule`、`reference-band-chart`、`document-vault` |
| 5 | 财务与消费 | **部分** | `billing-overview`（SaaS 套餐/发票，`BillingOverviewPage.cs:18,52`）、`usage-billing`/`usage-pricing`（计量计费）、`invoice-studio`（开票）、`wallet-card`（余额示例）、`budget-variance`（部门预算）、`usage-forecast`、`money-input`、`split-bar` | 个人流水、周期账单、订阅清单、分类预算/信封、分摊、收据归档；需 `expense-ledger`、`budget-board`、`bill-due-list`、`subscription-tracker` |
| 6 | 饮食与采购 | **空白** | `Recipe`/`Meal`/`Grocer`/`Nutrition` 在 `src` 中 0 匹配；只有 `emoji-picker` 的食物 emoji | 菜谱、备餐、采买清单、库存/过期；需 `meal-planner`、`grocery-list` |
| 7 | 出行与地点 | **空白** | `delivery-tracker` 只跟单订单（`DeliveryTrackerPage.cs:16,50`）；`choropleth-explorer` 是分析地图；`booking-pill` 只是订位胶囊；无天气/行程/地图控件 | 行程、预订、证件、天气、跨时区；需 `trip-itinerary`、`weather-forecast` |
| 8 | 家居与设备 | **空白** | `control-center` 是工作区快捷设置（`ControlCenterPage.cs:44-63`）；`kanban-board`/`project-board` 是软件任务；无设备/房间/能耗/保修 | 智能家居面板、家庭维护、能耗；需 `smart-home-panel`、`home-maintenance`、`energy-usage` |
| 9 | 记录·文档·日记 | **部分** | `rich-text-editor`（Markdown 编辑）、`card-to-article`/`page-curl`（展示）、`file-dropzone`/`file-upload`（上传流程，`FileUploadPage.cs:58` 从不落盘）、`document-extract`（抽取审阅）、`json-viewer` | 文档库（类型/归属/到期/搜索）、日记结构、心情、导出/加密；需 `document-vault`、`daily-journal` |
| 10 | 人情与社交 | **部分** | `team-directory`（同事目录，`TeamDirectoryPage.cs:29`）、`availability-picker`（会议时段）、`invite-people`、`chat-thread`/`comment-thread`/`reactions`、`presence-stack`、`hover-card` | 生日/纪念日、联系节奏、私人备注、家庭角色；需 `relationship-cadence`（组合实现，本文件不单列）与 `reminder-center` |
| 11 | 专注与休息 | **部分** | `control-center`（专注时长拨盘 + 频道开关）、`countdown`、`media-player`/`now-playing`（无音频）、`activity-rings` | 完整专注会话（目标/中断/复盘）、环境音、DND；需 `focus-session` |
| 12 | 宠物 | **空白** | `pet` 相关代码只在示例文案中出现 | 宠物档案、喂食/遛弯/驱虫/疫苗/体重/兽医；需 `pet-care` |
| 13 | 通知触达与打扰 | **部分** | `notification-center`（页内已读）、`toast`/`toast-stack`、`announcement-bar`、`agent-routines`（定时但模拟） | 系统通知、托盘红点、静默时段、行动型通知、休眠补偿；属 03 号 `notification-routing` + `background-run-dock`，生活域只消费 |
| 14 | 数据·持久化·离线 | **空白** | 无 `Sqlite`、无 `HttpClient`、无数据层；所有 block sample data（各页 ImplementationNotes） | 本地数据库、迁移、备份、同步/冲突、离线队列；非 UI 前置 |
| 15 | 文件·隐私·本地权限 | **部分** | `ArcFileDropzone.cs:308-317` 用 `StorageProvider.OpenFilePickerAsync`；`security-settings`/`env-variables`（样例掩码） | 文件保险箱、加密、PII 脱敏、权限说明、数据导出/删除；非 UI 前置 |
| 16 | 天气与环境 | **空白** | `Weather` 在 `src` 中仅 1 处 emoji 数据；无天气控件/数据 | 今日/未来天气、单位/时区、过期状态；需 `weather-forecast` |

---

## 5. 缺口分析

### 5.1 缺组件（4 个真正的新原子）

| 提案 | 为什么必须新组件而不是组合 | 最接近的现有项 | 证据 |
| --- | --- | --- | --- |
| `reminder-row` | 现有行没有“到期时间 + 重复标记 + 完成/延后动作”的组合；`notification-center` 行只有展开/已读/忽略，`inbox-triage` 行是消息语义 | `timeline` 行、`notification-center` 行 | `NotificationCenterPage.cs:246-259`；`InboxTriagePage.cs:51` |
| `weather-forecast` | 没有任何天气展示；`metric-card` 只能放一个数字，不能表达天气图标/降水/五天条 | `metric-card`、`sparkline` | `src` 中 `Weather` 仅 emoji 数据 1 处 |
| `habit-streak-strip` | `activity-heatmap` 是整年一格一天（`DataDemos.cs:302-330`），`activity-rings` 是当日三目标；缺 7/30 天可点击的连续条与“当前连续天数” | `activity-heatmap`、`activity-rings` | `ArcActivityHeatmap` 的 `Days` 为一天一值；`ActivityRingsPage.cs:54-62` |
| `reference-band-chart` | `line-chart` 没有 Band/Target/Annotation；`gauge` 只能单值；健康参考范围与预算线需要“值 vs 范围” | `line-chart`、`gauge` | `ArcLineChart.cs` 属性无 Band；`ArcGauge` 有 `Thresholds`/`Target` 但只单值 |

> 设计上优先做成**现有组件的变体/扩展**：`habit-streak-strip` 先作为 `activity-heatmap` 的 `window=week/month` 变体，`reference-band-chart` 作为 `ArcLineChart` 的 `Band/Target/Annotation` 扩展；只有当页面确实需要独立标题/交互时才升为独立 catalog 条目。这样不会把“已有能力”包装成“新组件”。

### 5.2 有但不适配（不比新增少的重要缺口）

| 现有项 | 支持到什么程度（证据） | 生活场景里哪里不适配 | 处理建议 |
| --- | --- | --- | --- |
| `calendar` | 日期/区间/最小最大/禁用日；无事件集合（`ArcCalendar.cs:20-45`） | 看不到“这天有 3 件事”，不能点日期新建提醒 | 扩展 `Markers`/`DayBadge` 属性（可并入 `day-agenda` 的实现，不单列提案） |
| `week-calendar` | 静态 8 条事件；拖拽新建/移动/缩放未复刻（`WeekCalendarPage.cs:35-45,74-77`） | 不能作为真实的家庭/个人周历 | 由 `day-agenda`/`family-logistics` block 承载；后续可把拖拽作为 `week-calendar` 扩展，不新造第二个周历 |
| `notification-center` | 已读/忽略/清空；样例数据（`NotificationCenterPage.cs:51`） | 不能延后、改期、完成；没有“逾期”概念 | 新增 `reminder-center`（行动队列），`notification-center` 保留为知会历史 |
| `inbox-triage` | archive/snooze/restore；snooze 时间不可编辑（`InboxTriagePage.cs:51`） | 消息语义，不是时间提醒 | 不要改造成提醒；提醒用 `reminder-center` |
| `billing-overview` | SaaS 套餐/发票/支付方式（`BillingOverviewPage.cs:18,52`） | 不是个人/家庭账单台账 | 新增 `bill-due-list`；账单流程不复制 SaaS 的套餐选择 |
| `budget-variance` | 按部门预算 vs 实际（`BudgetVariancePage.cs:15,51`） | 不是家庭分类预算 | 新增 `budget-board`，复用其“预算 vs 实际”的视觉语言 |
| `usage-meter` | 配额/分段/`WarnAt`（`FeedbackDemos.cs:141-186`） | 可以表达预算信封与药量库存，但不能表达“到期日/周期” | 作为 `budget-board`/`medication-schedule` 的内部积木复用 |
| `wallet-card` | 余额/活动/转账样例；无账本（`WalletCardPage.cs:52`） | 不是个人流水 | 新增 `expense-ledger`；`wallet-card` 继续做账户总览 |
| `reorderable-list` | 标题/详情/禁用，拖放排序（`ArcReorderableList.cs:24-31`） | 不能勾选、没有数量单位、没有到期 | `grocery-list`/`packing` 用 `checkbox`+`input`+`number-field` 组合；不强行扩展拖放列表 |
| `onboarding-checklist` | 上手流程语义（`ContentFlowsPage.cs:63-101`） | 不能每日重置、没有重复/到期 | 不复用为通用待办；`chore-board`/`grocery-list` 用行级组合 |
| `activity-heatmap` | 一年一格一天，`Days/Thresholds/SelectedDate`（`DataDemos.cs:302-330`） | 不能做 7 天紧凑条与点击补记 | `habit-streak-strip` 作为其周/月变体 |
| `control-center` | 工作区快捷设置：专注模式/频道/人/音量/字号（`ControlCenterPage.cs:44-63`） | 不是设备/房间/场景 | `smart-home-panel` 复用其玻璃面板与拨盘视觉，但设备模型另建 |
| `delivery-tracker` | 单订单、无 webhook（`DeliveryTrackerPage.cs:16,50`） | 不能汇总多包裹 | `package-inbox` 包裹列表复用其详情页 |
| `file-upload` / `file-dropzone` | 只记录名称/大小，不落盘（`FileUploadPage.cs:58`；`ArcFileDropzone.cs:308-317`） | 不是文档库，没有类型/到期/归属 | `document-vault` 负责落盘与元数据；上传控件继续复用 |
| `document-extract` | 抽取审阅，不归档（`DocumentExtractPage.cs:30`） | 只解决“读出字段”，不解决“存哪/何时到期” | `document-vault` 复用其抽取能力做字段回填，不重复实现 |
| `team-directory` | 同事目录/搜索/复制联系方式（`TeamDirectoryPage.cs:29`） | 不是私人关系与生日/联系节奏 | 用 `card`+`avatar`+`reminder-row` 组合；不新造联系人组件 |
| `availability-picker` | 会议时段（`AvailabilityPickerPage.cs:47`） | 可以借用，但偏团队会议 | 户外/聚会场景直接复用，不扩展 |
| `media-player` / `now-playing` | 播放界面；明说没有音频（`MediaPlayerPage.cs:53`） | 专注环境音需要真实音频 | `focus-session` 只定义 UI，音频由应用层接入；不承诺播放 |
| `rating` | 数值评价、支持半星/清除 | 可以作为心情 5 档，但必须加文字标签，不能只用星星 | `daily-journal` 内组合使用；若评审认为需要独立心情尺度，可作为 `rating` 变体而非新组件 |

### 5.3 缺 block（20 个页面级组合）

生活域的复杂度在“把多个域拼成一屏并保持空/加载/错误/成功四态”，因此 20 个新 block 是主要投入：

`day-agenda`、`reminder-center`、`document-vault`、`medication-schedule`、`bill-due-list`、`grocery-list`、`daily-brief`、`expense-ledger`、`budget-board`、`subscription-tracker`、`meal-planner`、`trip-itinerary`、`daily-journal`、`home-maintenance`、`smart-home-panel`、`focus-session`、`pet-care`、`chore-board`、`energy-usage`、`package-inbox`。

分类学处理：

- 现有 block 组是 6 个（Marketing and site 18 / Commerce 18 / App shell and settings 21 / Team and collaboration 16 / Data and charts 20 / AI 18）。生活域 20 个 block 不适合硬塞进任何一组；
- 建议新增 **block 组 “Daily life”**，并在 `docs/arc-catalog.json` 的 `blockGroups` 中增加一项；组件分类保持现有 23 类不变；
- 建议新建 **`Arc.Avalonia.BlocksLife`** 项目承载这 20 个 block（与 6 个 Blocks* 项目平级）。JSON 的 `project` 字段为了“合入后可直接构建”，暂时指向最接近的现有项目（BlocksApp/BlocksCommerce/BlocksData），合并时由 Lead 决定是否拆项目；若拆，需要同步更新 `ArcUI.slnx`、主题入口与 Gallery 引用。

### 5.4 缺非 UI 能力（真正的“隐藏工作量”）

| 能力 | 为什么生活域必须有 | 仓库现状（证据） | 建议归属 |
| --- | --- | --- | --- |
| 本地数据层/持久化 | 提醒、账单、用药、文档、日记都要跨会话存在 | 无 SQLite/无数据层；所有 block sample data；`SettingsPage.cs:51` 只改本地状态 | 应用层（推荐 SQLite/JSON + 迁移）；不要塞进 UI 控件项目 |
| 重复调度器 | 每日/每周/每月/每年、提前量、错过补跑、DST、休眠唤醒 | `Cron` 0、`DueDate` 0；`Recurrence` 9 处均为文案；`Remind` 2 处为标签 | 应用层调度服务 + 03 号 `routine-schedule-editor` 的 `ScheduleSpec` |
| 通知投递 | 窗口关闭后仍能提醒；系统通知/托盘红点 | `TrayIcon` 0；无 `NotificationManager`；`notification-center` 只在页内 | 应用层平台适配（Windows toast、macOS UNUserNotification、Linux DBus）；03 号 `notification-routing` 定义策略 |
| 后台常驻/自启/单实例 | 定时提醒与 Agent routine 要求应用活着 | `Mutex` 0、`TrayIcon` 0 | 应用层启动器；03 号 `desktop-agent-settings` |
| 文件保险箱/加密 | 证件、体检、财务是高敏感数据 | `Cryptography`/`ProtectedData` 0；file dropzone 不落盘 | 应用层加密存储 + 权限说明；`document-vault` 只展示“已加密/可导出/可删除” |
| 隐私与同意 | 健康/财务/证件不能默认上传或训练 | 无隐私 UI；`env-variables` 有掩码但仅示例 | `security-settings` 扩展 + 应用层策略；新增数据域需明示用途 |
| 天气/账单/银行/健康/物流接入 | 生活数据不能全靠手输 | `HttpClient` 0；无 OAuth/API 客户端 | 应用层 connector（MCP/插件）；MVP 手工/CSV 导入，文案不得假装已同步 |
| 本地化/时区/单位/货币 | 跨时区旅行、DST、非 USD | 日期固定 `en-US`（`ArcDateMath.cs:3-7`）；`money-input` 默认 USD（`ArcMoneyInput.cs:48`） | foundation 级 `ArcLocale`/`ArcTimeZone` 抽象 + 应用层设置；UI 不用系统隐式 locale |
| 离线/同步/冲突 | 家庭共享清单/日程需多端 | 无网络/无同步 | 应用层；UI 需要 `OfflinePending`/`SyncError` 状态 |
| 可访问性与打扰 | 提醒要能被屏幕阅读器播报，且能静默 | 703 处 Automation 是强项；但无提醒/闹钟语义 | 复用现有自动化属性；提醒中心用 live region；静默时段见 03 号 |

> 引用：`docs/04` §0.2（虚拟化仅 EmojiPicker）、§2.6（重建型 UI）、§4 R4（零虚拟化）、G-P0-5（DataGrid/TreeTable 整表重建）、R5（可见性/生命周期门控缺失）、R7（截图 harness 开 reduced-motion，动画路径从未在 CI 执行）。生活域的流水/记录列表一旦上量，必然触发这些结论；扩展不能假设“现有表格能直接吃真实数据”。

---

## 6. 提案总览（24 项）

### 6.1 设计原则

1. **先组合，后新建**：能用现有 block/组件组合出来的场景不单列组件；4 个组件都是“确实没有等价能力”的原子（提醒行、天气、连续条、参考范围）。
2. **block 负责四态与安全**：每个生活 block 必须定义空/加载/错误/成功，且危险动作（跳过服药、删除证件、取消订阅）有确认与可撤销。
3. **数据契约先行**：每个 block 在 `notes` 里写清楚需要的时间/金额/状态字段；没有数据契约的 block 不进入实现。
4. **不与 Agent 交互域重复**：排期、通知策略、托盘、审批队列由 03 号负责；生活域只消费，不重复造。
5. **Pro 合规**：新增项是原创 UI 设计，参考公开的交互与视觉描述，不抓取/照抄任何商业实现；无需复刻 Pro 组件的私有 API。

### 6.2 总表

| # | slug | kind | category | project | P | effort | 设计意图 | 与现有关系 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | `reminder-row` | component | Activity | Arc.Avalonia.Feedback | P0 | M | 到期/重复/完成/延后的生活行动行 | 新原子；`timeline` 行/`notification-center` 行都不够 |
| 2 | `day-agenda` | block | Daily life | Arc.Avalonia.Blocks.App | P0 | L | 事件+提醒+任务+服药的同轴日常视图 | 组合 `week-calendar`/`calendar`/`timeline`/`reminder-row` |
| 3 | `reminder-center` | block | Daily life | Arc.Avalonia.Blocks.App | P0 | L | 逾期/今天/即将/已延后的行动中心 | 行动队列；与 `notification-center`/`inbox-triage` 分工 |
| 4 | `document-vault` | block | Daily life | Arc.Avalonia.Blocks.App | P0 | L | 证件/保险/保修/报告/票据的本地库 | 复用 `file-dropzone`/`file-upload`/`sortable-data-table` |
| 5 | `medication-schedule` | block | Daily life | Arc.Avalonia.Blocks.App | P0 | M | 服药时间表、已服/漏服、续方库存 | 复用 `reminder-row`/`usage-meter`/`progress` |
| 6 | `bill-due-list` | block | Daily life | Arc.Avalonia.Blocks.Commerce | P0 | M | 个人周期账单到期与已付台账 | 不是 `billing-overview`（SaaS） |
| 7 | `grocery-list` | block | Daily life | Arc.Avalonia.Blocks.Commerce | P0 | M | 分区采买清单，勾选/数量/共享 | 不是 `reorderable-list`（无勾选/数量） |
| 8 | `daily-brief` | block | Daily life | Arc.Avalonia.Blocks.App | P1 | M | 今天一屏：天气/议程/到期/健康/钱 | 聚合；不替代 dashboard |
| 9 | `weather-forecast` | component | Cards | Arc.Avalonia.Feedback | P1 | M | 今日 + 5 天天气条 | 全仓无天气 |
| 10 | `habit-streak-strip` | component | Charts | Arc.Avalonia.Charts | P1 | S | 7/30 天连续条 + 当前连续天数 | `activity-heatmap` 的周/月变体 |
| 11 | `reference-band-chart` | component | Charts | Arc.Avalonia.Charts | P1 | M | 参考范围/目标线/阈值注释图 | `line-chart` 的扩展，不新绘制引擎 |
| 12 | `expense-ledger` | block | Daily life | Arc.Avalonia.Blocks.Commerce | P1 | L | 个人流水、分类占比、快速记账 | 不是 `wallet-card`/`budget-variance` |
| 13 | `budget-board` | block | Daily life | Arc.Avalonia.Blocks.Data | P1 | L | 分类信封预算、结转、月底预测 | 复用 `usage-meter`/`budget-variance` |
| 14 | `subscription-tracker` | block | Daily life | Arc.Avalonia.Blocks.Commerce | P1 | L | 跨服务订阅续费/涨价/取消 | 不是 `billing-overview` |
| 15 | `meal-planner` | block | Daily life | Arc.Avalonia.Blocks.App | P1 | L | 周三餐计划 + 生成采买 | 复用 `week-calendar`/`grocery-list` |
| 16 | `trip-itinerary` | block | Daily life | Arc.Avalonia.Blocks.App | P1 | L | 逐日行程、交通/住宿/预约/证件 | 不是 `delivery-tracker` |
| 17 | `daily-journal` | block | Daily life | Arc.Avalonia.Blocks.App | P1 | M | 心情+标签+文字+照片+回顾 | 复用 `rich-text-editor`/`rating` |
| 18 | `home-maintenance` | block | Daily life | Arc.Avalonia.Blocks.App | P1 | L | 家庭资产/保修/保养/维修 | 复用 `document-vault`/`calendar` |
| 19 | `smart-home-panel` | block | Daily life | Arc.Avalonia.Blocks.App | P1 | L | 房间/设备/场景控制面 | 复用 `control-center` 视觉，不复制工作区语义 |
| 20 | `focus-session` | block | Daily life | Arc.Avalonia.Blocks.App | P1 | M | 目标/计时/环境音/中断/复盘 | 复用 `control-center`/`countdown`/`media-player` |
| 21 | `pet-care` | block | Daily life | Arc.Avalonia.Blocks.App | P2 | M | 宠物照护时间表与体重/兽医 | 复用 `reminder-row`/`line-chart` |
| 22 | `chore-board` | block | Daily life | Arc.Avalonia.Blocks.App | P2 | M | 家务轮值与完成公平性 | 复用 `kanban-board`/`project-board` |
| 23 | `energy-usage` | block | Daily life | Arc.Avalonia.Blocks.Data | P2 | M | 家庭能耗/费用/异常 | 复用 `bar-chart`/`line-chart`/`reference-band-chart` |
| 24 | `package-inbox` | block | Daily life | Arc.Avalonia.Blocks.Commerce | P2 | M | 多包裹汇总与异常件 | 复用 `delivery-tracker` |

> `reference-band-chart` 的 `asset` 设为 `foundation`（图表基础能力，被生活/健康/财务共用）；其余为 `life`。

---

## 7. P0 设计说明（7 项）

> **合并后优先级以 `docs/arc-extension-catalog.json` 的 `reviewNote` 为准**：`document-vault`（§7.4）、`bill-due-list`（§7.6）、`grocery-list`（§7.7）在合并评审中降为 P1；`daily-brief` 由 P1 升为 P0。本节的 Anatomy/状态/验收仍按原设计保留，落地顺序请以目录为准。

> P0 定义：不补就无法做出可用的生活工具。每项给设计意图、Anatomy、状态枚举、验收要点、依赖与去重。

### 7.1 `reminder-row`（component，Activity，M ≈ 3 人日）

**设计意图**：生活行动的最小单元。提醒、账单、服药、家务、证件续办、订阅续费都用同一种行表达“什么时候该做什么、做了没有、能不能晚点”。它是 `day-agenda`、`reminder-center`、`medication-schedule`、`bill-due-list` 的共同原子。

**Anatomy**

```
┌──────────────────────────────────────────────────────────────┐
│ [○ 完成]  标题                                    [⋯ 动作]   │
│           [今天 18:00] · [每周三] · [来源：账单/用药/家务]    │
│           展开：备注 / 来源链接 / 延后选项 / 改期 / 跳过      │
└──────────────────────────────────────────────────────────────┘
```

- **前导完成控件**：复选框/圆形完成；完成即写时间戳并支持撤销。
- **标题 + 元信息行**：到期 chip（`今天`/`2 小时后`/`逾期 1 天`）、重复标记（图标 + 文本）、来源（账单/用药/家务/证件）。
- **尾部动作**：默认只放一个 `⋯` 菜单，避免行内按钮过多；菜单含“延后 10 分钟 / 1 小时 / 明天 / 自定义”“改期”“完成”“跳过”。
- **展开态**：备注、来源、附件、操作日志；不默认展开。

**状态枚举**：`Pending`（未到）→ `Due`（到期）→ `Snoozed`（已延后）/ `Done`（已完成）/ `Missed`（已错过）/ `Paused`（暂停，如停药/暂停订阅）；`Disabled` 表示只读或来源失效。

**验收要点**

- 键盘：Tab 到行，Enter/Space 完成，`M` 打开菜单；菜单可用方向键；延后选项可自定义
- 文案：due chip 用相对时间并标明基准（“今天 18:00”“逾期 1 天”），不自己写逐帧 timer 刷新
- 颜色 + 文字双编码；暗色下 due/overdue 对比度达标
- 完成后可撤销（toast + undo），并写入操作日志
- 屏幕阅读器播报“已逾期”“明天到期”；`Missed` 不只靠红色
- 行不含 Agent 审批语义；审批仍用 `agent-approvals`（03 域）

**依赖与去重**：重复规则不内建，依赖 03 号 `routine-schedule-editor` 的 `ScheduleSpec`；提醒投递依赖 03 号 `notification-routing` 与托盘方案；本项只负责“看见与操作”。

### 7.2 `day-agenda`（block，Daily life，L ≈ 8 人日）

**设计意图**：把“今天”放在一屏：事件（有开始/结束）、提醒（有到期时刻）、任务（有截止）、服药（有剂量时刻）在同一条时间轴上，解决 `week-calendar` 只读示例与 `calendar` 无事件标记的问题。

**Anatomy**

```
┌──────────────────────────────────────────────────────────────┐
│ [‹] 11月12日 周三 [›]   [今天]      [日|3日|周]   [+ 新建]   │
├──────────────────────────────────────────────────────────────┤
│ 全天： [出差] [妈妈生日]                                      │
├──────────┬───────────────────────────────────────────────────┤
│ 08:00    │  ● 08:00 服药（早餐后）                            │
│ 09:00    │  ┃ 09:30–10:30 设计评审                            │
│ 10:00    │  ┃                                                │
│ 12:00    │  ● 12:30 账单到期（信用卡）                        │
│          │  ─────── 现在 14:05 ───────                        │
│ 15:00    │  ○ 15:00 接孩子                                    │
└──────────┴───────────────────────────────────────────────────┘
```

**状态枚举**：`Loading` / `Empty` / `Populated` / `Conflict`（时间重叠，只提示不自动重排）/ `Stale`（数据过期）/ `Error`。

**验收要点**

- 四种对象同轴且视觉可区分，但只用语义 token（不引入新的类别色）
- 日期导航、今天、视图切换、新建都可用键盘；事件块可聚焦并读出类型/时间
- 冲突用标记 + 文字，不静默移动用户数据
- 时区：条目显示本地时间并注明时区；跨时区条目显示出发地/当地时间
- 空状态给“添加第一件事”的下一步
- 页面加载不启动 60fps 动画；当前时间线用静态线 + 分钟级刷新（可见性门控）
- 列表 > 50 条时必须走虚拟化路径（`docs/04` R4）；页面截图用固定 now

**依赖**：事件/提醒数据模型（非 UI）；`routine-schedule-editor` 的重复展开；`ArcCalendar` 可选扩展 `Markers` 用于“有事的日期”提示（建议作为本 block 的一部分实现，不单列提案）。

### 7.3 `reminder-center`（block，Daily life，L ≈ 9 人日）

**设计意图**：生活的“行动队列”。`notification-center` 是知会历史（读过就完了），`inbox-triage` 是消息归档；提醒中心面对的是“必须处理或明确延后”的到期项，按逾期/今天/即将/已延后分组。

**Anatomy**

```
┌──────────────────────────────────────────────────────────────┐
│ 提醒            [逾期 2 · 今天 3 · 即将 8]      [静默时段 ⓘ] │
│ [待处理] [即将] [已延后] [已完成]       [批量延后 ▾]         │
├──────────────────────────────────────────────────────────────┤
│ 逾期                                                         │
│  ● 信用卡还款（逾期 2 天）            [延后 ▾] [改期] [完成] │
│ 今天                                                         │
│  ○ 服药（18:00）                      [延后 ▾] [改期] [完成] │
│  ○ 取快递（20:00 前）                 ...                    │
│ 即将（7 天内）                                               │
│  ○ 护照续办（还剩 30 天）                                   │
├──────────────────────────────────────────────────────────────┤
│ 已选 2 项： [延后到明天] [标记完成] [取消选择]               │
└──────────────────────────────────────────────────────────────┘
```

**状态枚举**：`Empty` / `Normal` / `Editing`（展开改期）/ `BulkSelected` / `SnoozedAll` / `Error`；行状态沿用 `reminder-row` 的 `Pending/Due/Snoozed/Done/Missed/Paused`。

**验收要点**

- 逾期/今天/即将分组按本地日界划分；跨时区与 DST 用数据层时区，不用 `DateTime.Now`
- 延后选项可编辑（与 `inbox-triage` 的固定 snooze 区别），并显示“延后到什么时候”
- 批量延后/完成/取消选择；操作可撤销；操作日志可查
- 静默时段入口（策略本身在 03 号 `notification-routing`），文案说明“窗口关闭时是否仍提醒”
- 空状态区分“真的没有”与“全部完成”，给下一步
- 不为每条提醒起一个 60fps timer；倒计时文案按分钟级更新并做可见性门控（`docs/04` R1/R5）
- 与 `agent-attention-inbox` 不合并：生活提醒的完成/延后不等于 Agent 审批

**依赖**：应用层调度器 + 通知投递 + 托盘（03 号）；本地存储；`notification-center` 保留为知会历史。

### 7.4 `document-vault`（block，Daily life，L ≈ 9 人日）

**设计意图**：生活里真正需要“存起来、找得到、到期会提醒”的文件：身份证/护照/驾照/保险/保修/体检报告/票据。现有 `file-upload` 只演示上传进度，`document-extract` 只做抽取审阅，二者都不解决“存哪、属于谁、何时到期、如何搜”。

**Anatomy**

```
┌──────────────────────────────────────────────────────────────┐
│ 文档   [搜索…] [类型 ▾] [归属人 ▾] [到期排序 ▾]   [+ 添加]   │
│ [全部 24] [证件 6] [保险 4] [保修 5] [报告 6] [票据 3]       │
├──────────────────────────────────────────────────────────────┤
│ 名称                 类型    归属   到期        状态         │
│ 护照.pdf             证件    我     2031-03-12  [90 天后]    │
│ 房屋保险.pdf         保险    家庭   2026-12-01  [30 天后]    │
│ 洗衣机保修单.pdf     保修    家庭   2027-06-30  [有效]       │
├──────────────────────────────────────────────────────────────┤
│ 预览抽屉：元数据 / 文件预览 / 续办材料 / 导出 / 删除（确认） │
└──────────────────────────────────────────────────────────────┘
```

**状态枚举**：`Empty` / `Uploading` / `Scanning`（抽取/缩略图）/ `Ready` / `Expiring`（30 天内）/ `Expired` / `Unavailable`（源文件丢失/移动盘离线）/ `Locked`（未解锁保险箱）。

**验收要点**

- 文件复制到应用数据目录，保留原始文件名与哈希；源文件删除不影响库内副本
- 到期状态与提醒入口（30/7/1 天）；到期日可空（票据类）
- 类型/归属人/标签可编辑；搜索覆盖名称、类型、归属、备注
- 权限错误（只读目录、路径过长、移动盘）给出可读恢复文案；不吞异常
- 删除需确认并可撤销；导出支持原文件与元数据 JSON
- 加密与访问审计由应用层负责，UI 明示“已加密/仅本机/可导出删除”
- `document-extract` 只用于回填字段，不在本 block 重复实现 OCR/抽取
- 大列表用虚拟化或分页（`docs/04` R4）

**依赖**：应用层文件存储/加密/权限；可选 `document-extract`（03 域相邻能力）做字段回填；`routine-schedule-editor` 做到期规则。

### 7.5 `medication-schedule`（block，Daily life，M ≈ 4 人日）

**设计意图**：把“吃药”从一句提醒升级为可核对的时间表：药名、剂量、餐前/餐后、时间、已服/跳过、课程进度、续方库存、历史。健康域现有 `activity-rings`/`gauge` 只能展示运动与单值，不能承载服药安全。

**Anatomy**

```
┌──────────────────────────────────────────────────────────────┐
│ 用药        今天 · 第 12/30 天       [暂停用药] [编辑方案]   │
│ 08:00  二甲双胍 500mg（早餐后）        [已服 ✓] [跳过]        │
│ 12:30  维生素 D 1000IU                 [已服 ✓]              │
│ 18:00  二甲双胍 500mg（晚餐后）        [稍后 ▾] [跳过]        │
├──────────────────────────────────────────────────────────────┤
│ 本周依从：[● ● ○ ● ● ● ●]   连续 5 天                       │
│ 续方库存：████████░░ 8/10 天      [申请续方]                 │
│ 历史：11-11 08:00 已服 · 11-10 18:00 漏服（补记）           │
└──────────────────────────────────────────────────────────────┘
```

**状态枚举**：`Due` / `Taken` / `Skipped` / `Missed` / `Snoozed` / `Paused` / `LowStock` / `CourseComplete`。

**验收要点**

- 已服/跳过都写时间戳，可撤销；漏服补记必须写清实际时间与“补记”标记，避免误服
- 依从条/连续天数复用 `habit-streak-strip` 或 `activity-heatmap` 周视图
- 续方库存按“剩余天数”与“提醒阈值”表达，不虚构药品库存
- 旅行跨时区时，显示“当地时间”并保留原时区备注
- 健康数据默认本地、可导出/删除；不做医学建议文案
- 与就诊准备联动：导出近 30 天用药与症状
- 提醒投递复用 `notification-routing`；本 block 不自己发系统通知

### 7.6 `bill-due-list`（block，Daily life，M ≈ 4 人日）

**设计意图**：个人/家庭账单台账。它要与 SaaS 的 `billing-overview`（套餐、发票、支付方式）区分：这里只有“哪笔钱、什么时候到期、付了没有”。

**Anatomy**

```
┌──────────────────────────────────────────────────────────────┐
│ 账单        本期合计 ¥3,240   未来 30 天 ¥5,120   [月|列表]  │
│ [全部] [待付 4] [已付 6] [自动扣款 3]                         │
├──────────────────────────────────────────────────────────────┤
│ 11-15  房租            ¥2,800   [自动扣款]        [标记已付] │
│ 11-20  信用卡          ¥1,240   [还有 5 天]       [标记已付] │
│ 11-28  水电燃气        ¥ 320    [待付]            [标记已付] │
│ 12-05  保险            ¥ 760    [即将]            [标记已付] │
└──────────────────────────────────────────────────────────────┘
```

**状态枚举**：`Upcoming` / `Due` / `Paid` / `Overdue` / `AutoPay` / `Skipped`。

**验收要点**

- 标记已付需记录日期与金额（可编辑）；撤销；已付项默认折叠
- 金额用 minor units + 币种；不混币种；多币种显示原币与折算说明
- 重复账单只显示“下一次到期”，不从 UI 重新计算 RRULE（排期在数据层）
- 无银行接口时只支持手工/CSV；文案不得说“已自动扣款”除非用户明确标记
- 逾期分层：到期前 7/3/1 天、逾期 1/7/30 天；与 `reminder-center` 联动
- 历史流水多时用分页/虚拟化（`docs/04` R4/G-P0-5）

### 7.7 `grocery-list`（block，Daily life，M ≈ 3 人日）

**设计意图**：最日常的“共同编辑清单”。现有 `reorderable-list` 只排序、`checkbox` 只是单个勾选，没有“分区 + 数量单位 + 添加人 + 已购进度 + 共享”。

**Anatomy**

```
┌──────────────────────────────────────────────────────────────┐
│ 采买清单   已购 7/18  ████░░░░  [分享] [清空已购]  [+ 添加]  │
│ 蔬果                                                         │
│  ☑ 西红柿      × 6 个      备注：要熟一点的                  │
│  ☐ 菠菜        × 1 把                                         │
│ 乳品                                                         │
│  ☐ 牛奶        × 2 L       由妈妈添加                        │
│  ☐ 酸奶        × 4 杯                                         │
└──────────────────────────────────────────────────────────────┘
```

**状态枚举**：`Empty` / `Active` / `Shopping`（购物中，已购折叠）/ `Completed` / `Archived` / `OfflinePending`（离线待同步）。

**验收要点**

- 数量 + 单位结构化（quantity + unit），不做自由文本拼接；支持“适量”这类非数值项
- 勾选即写入时间与勾选人；可撤销；已购可折叠/清空
- 分区顺序可调；缺货时可标记“缺货/替换”
- 共享清单需要同步与冲突合并（非 UI）；离线勾选排队，恢复后合并
- 从 `meal-planner` 加入时列出将加入的条目，不静默覆盖手工项
- 大清单虚拟化；键盘可勾选与编辑
- 不显示虚构价格；价格/预算属于 `expense-ledger`

---

## 8. P1/P2 设计说明

### 8.1 P1（13 项）

| 提案 | 设计意图与关键 Anatomy | 状态枚举 | 验收要点 |
| --- | --- | --- | --- |
| `daily-brief` | 今天一屏：问候/日期、天气、议程、到期提醒、健康环、预算/账单、包裹、一条建议；分节可折叠 | Loading/Empty/Ready/Stale/Error | 只给一条主建议；数据按需刷新；建议可拒绝；固定 now 截图；不启动常驻 60fps |
| `weather-forecast` | 今日主态（温度/体感/降水/风）+ 5 天条；单位/时区/数据时间戳为属性 | Ready/Stale/Unavailable/Error | 不内置 API/密钥；过期数据明确标注；图标来自现有 Lucide 子集；暗色可读 |
| `habit-streak-strip` | 7/30 天可点击连续条 + 当前连续天数 + 今天状态；建议先做 `activity-heatmap` 变体 | Future/Today/Done/Missed/Partial | 颜色+文字双编码；方向键移动；补记写时间；不与 `activity-rings` 争“三目标”语义 |
| `reference-band-chart` | `ArcLineChart` 扩展 Band/Target/Annotation；悬停读出值与范围关系 | —（图表，非交互状态） | 复用 ChartCore/绘制缓存；范围外点用状态色+文字；不做医学/投资建议；暗色对比 |
| `expense-ledger` | 月份切换、分类筛选、搜索、流水表、分类 donut、快速记一笔 drawer、收据附件 | Empty/Ready/Editing/Filtered/Error | 金额 minor units+币种；快速记账 2 步内完成；收据可预览；真实数据前必须虚拟化/分页（R4/G-P0-5） |
| `budget-board` | 分类信封卡（已用/预算/剩余/结转）、总预算、超支分类、月底预测 | Healthy/Warning/Over/Unbudgeted | 预测标“估算”并给依据；结转规则可编辑；复用 `usage-meter` 的 WarnAt；不虚构数据 |
| `subscription-tracker` | 续费倒计时、周期与折算月成本、月度/年度总成本、取消流程、涨价标记 | Active/DueSoon/PriceChanged/Cancelled/Paused | 取消文案诚实（不伪造挽留优惠）；无银行接口时手工；跨币种分开展示 |
| `meal-planner` | 周网格（早/午/晚 × 7 天）、菜谱卡、营养概览、生成采买清单 | Empty/Planned/Generated/Error | 份数按人数缩放；生成前列出条目；不推断库存；不做营养医学建议 |
| `trip-itinerary` | 目的地/日期/倒计时/同行人、逐日 timeline、交通/住宿/预约、证件/预订号、天气 | Draft/Upcoming/InProgress/Completed/Changed | 跨时区显示“当地时间”；地图不承诺；证照只显示后四位/掩码；变更需确认 |
| `daily-journal` | 日期、5 档心情+文字标签、一句话、标签、照片、当天事件、历史日历/热力图 | Empty/Draft/Saved/Error | 富文本增量修复前限制长文（G-P0-6）；本地加密/导出/删除；不做心理诊断；心情必须文字标签 |
| `home-maintenance` | 资产行（设备/位置/购买/保修/服务商/上次保养）、报修、维修 timeline、单据 | Active/DueSoon/Expired/Retired | 保修从购买日/发票推断需用户确认；服务商信息本地；费用与单据关联 vault；周期提醒复用排期 |
| `smart-home-panel` | 房间分组设备卡（开关/亮度/温度/在线）、三场景、危险设备二次确认 | Online/Offline/Unknown/Pending/Error | 只定义 UI 适配契约（ID/能力/状态/命令）；离线显示最后已知状态；命令失败可撤销；不做协议实现 |
| `focus-session` | 目标/时长拨盘/剩余时间/当前任务/环境音/允许打扰的人/中断/复盘 | Idle/Running/Paused/Break/Completed/Abandoned | 单帧时钟+可见性门控（R1/R5/G-P0-2）；桌面无法真屏蔽系统通知要诚实说明；结束写专注记录 |

### 8.2 P2（4 项）

| 提案 | 设计意图 | 验收要点 |
| --- | --- | --- |
| `pet-care` | 宠物档案、今日任务、疫苗/驱虫周期、体重曲线、兽医/就诊记录、交接说明 | 健康数据本地；周期复用排期；交接说明只读导出；不做医疗建议 |
| `chore-board` | 家务轮值、频率、完成历史、本月公平性回顾 | 轮值算法简单可解释；成员用头像组；不宣称“智能均衡”；完成可撤销 |
| `energy-usage` | 日/月能耗与费用、同比/预算参考、异常日、节省建议 | 双轴慎用（必要时两张图）；标注“相关≠因果”；数据来源（手工/导入）明示；不虚构实时 |
| `package-inbox` | 多包裹汇总、异常件、代收/改约 | 无物流 API 时手工单号；ETA 显示数据更新时刻；不假装自动刷新；详情复用 `delivery-tracker` |

---

## 9. 与现有能力的关系（去重说明）

这张表用于回答 QA 的“是否把已有能力包装成新组件”。原则：**能组合的不新建；能扩展的不另起；只有数据/交互语义确实不同才新建**。

| 提案 | 最相近的现有项 | 关系 | 为什么不直接复用 |
| --- | --- | --- | --- |
| `reminder-row` | `timeline` 行 / `notification-center` 行 | 新原子 | 现有行没有“完成/延后/改期”动作与到期状态；`timeline` 是历史记录，`notification-center` 是知会 |
| `day-agenda` | `week-calendar` / `calendar` | 新 block（组合） | `week-calendar` 数据是静态示例且无新建；`calendar` 无事件标记；需要四类对象同轴 |
| `reminder-center` | `notification-center` / `inbox-triage` | 新 block（不同对象） | 前两者分别是知会历史与消息归档；提醒需要逾期/延后/改期/批量完成 |
| `document-vault` | `file-upload` / `file-dropzone` / `document-extract` | 新 block, 复用上传与抽取 | 上传演示不落盘；抽取只做字段；缺类型/归属/到期/搜索/删除 |
| `medication-schedule` | `activity-rings` / `usage-meter` / `progress` | 新 block（复用积木） | 没有药物/剂量/服药记录/续方语义；环与进度只能表达数值 |
| `bill-due-list` | `billing-overview` / `usage-billing` / `invoice-studio` | 新 block（不同对象） | 三者都是 SaaS 套餐/计量/开票；个人账单只需要到期与已付 |
| `grocery-list` | `reorderable-list` / `checkbox` | 新 block（组合） | 拖放列表没有勾选/数量/分区；需要共享与离线状态 |
| `daily-brief` | `metrics-dashboard` / `kpi-drilldown` | 新 block（聚合） | 现有 dashboard 是工作/营收视角；简报是生活一屏 + 一条建议 |
| `weather-forecast` | 无 | 新组件 | 全仓没有天气展示；`metric-card` 不能表达天气条 |
| `habit-streak-strip` | `activity-heatmap` / `activity-rings` | **变体优先** | 建议先做 heatmap 的周/月窗口；独立成项只当页面需要独立标题/点击回填 |
| `reference-band-chart` | `line-chart` / `gauge` | **扩展优先** | 作为 `ArcLineChart` 的 Band/Target 属性；不新增绘制引擎 |
| `expense-ledger` | `wallet-card` / `budget-variance` / `sortable-data-table` | 新 block（组合） | `wallet-card` 无账本；`budget-variance` 是部门预算；表格可以作为行渲染积木 |
| `budget-board` | `usage-meter` / `budget-variance` | 新 block（组合） | 信封预算/结转/预测是新的数据语义；进度条复用 usage-meter |
| `subscription-tracker` | `billing-overview` / `cancel-flow` | 新 block（不同对象） | 单个 SaaS 账户 vs 跨服务订阅清单；取消流程复用 cancel-flow |
| `meal-planner` | `week-calendar` | 新 block（组合） | 日历是时间语义；三餐槽位/菜谱/营养/生成清单是新的数据语义 |
| `trip-itinerary` | `delivery-tracker` / `timeline` / `stepper` | 新 block（组合） | 单订单跟踪 vs 多日行程；timeline/stepper 复用 |
| `daily-journal` | `rich-text-editor` / `card-to-article` / `page-curl` | 新 block（组合） | 写作/展示形态已有；缺心情/标签/当日事件/回顾结构 |
| `home-maintenance` | `document-vault` / `calendar` | 新 block（组合） | 资产/保修/保养周期是新的数据语义 |
| `smart-home-panel` | `control-center` | 新 block（不同对象） | 工作区快捷设置 vs 设备/房间/场景；复用玻璃面板视觉 |
| `focus-session` | `control-center` / `countdown` / `media-player` | 新 block（组合） | 现有只有拨盘/倒计时/播放器；缺会话目标、中断、复盘 |
| `pet-care` | `reminder-row` / `line-chart` | 新 block（组合） | 宠物档案/疫苗/兽医是新的数据语义 |
| `chore-board` | `kanban-board` / `project-board` | 新 block（不同对象） | 团队任务阶段 vs 家庭轮值与公平性 |
| `energy-usage` | `bar-chart` / `line-chart` | 新 block（组合） | 能耗/费用/异常是新的数据语义 |
| `package-inbox` | `delivery-tracker` | 新 block（组合） | 单包裹详情 vs 多包裹汇总；详情复用 |

**关于“新分类”的说明**：组件的 4 个新项分别落在现有 `Activity`/`Cards`/`Charts` 三类，不新增组件分类；20 个 block 落在新增的 block 组 **“Daily life”**。这样既保持 23 个组件分类的稳定，又给生活域一个可导航的页面组。`reference-band-chart` 的 `asset` 标为 `foundation`，因为它是图表基础能力，不专属生活域。

---

## 10. 桌面端约束与宿主前置（非本库交付）

> 本节能力均属于宿主应用；本库只提供对应的 UI 展示位与配置界面（边界见 `docs/06-agent-life-tools-extension.zh.md` §7）。

这一节是给 Lead 合并时的“风险清单”，也是给实现者的“不要低估”提醒。

| 约束 | 具体影响 | 当前仓库证据 | 生活域提案的应对 |
| --- | --- | --- | --- |
| **无浏览器通知** | 不能靠 Web Notification；窗口关闭后提醒必须走系统通知 | 无 `TrayIcon`/`NotificationManager` | 生活域只定义“该提醒什么”；通知偏好界面复用 03 号 `notification-routing`，**投递与系统权限由宿主**；UI 文案不得承诺“一定会响” |
| **系统托盘** | 常驻、红点、快捷开关都依赖托盘；Avalonia 的 `TrayIcon` 只支持原生菜单，富面板要自建窗口 | `TrayIcon` 0 处 | 托盘由宿主；UI 复用 catalog 的 `tray-menu` 与 03 号 `background-run-dock` 的视觉，`reminder-center` 只展示“红点计数”状态 |
| **后台常驻** | 关窗后是否继续跑、开机自启、单实例、休眠唤醒 | `Mutex` 0、无启动器 | 配置界面复用 `desktop-agent-settings`；常驻与调度由宿主 |
| **休眠与错过** | 笔记本合盖后错过 3 个提醒，醒来不能静默补发一串 | 无调度器 | UI 展示“已错过”并要求逐条处理；记录与补发策略由宿主 |
| **本地文件权限** | 文档库要复制文件、加密、备份；不同平台路径/权限不同 | `ArcFileDropzone.cs:308-317` 只用文件选择器；不落盘 | `document-vault` 的实现说明要求应用层存储；UI 对权限错误给可恢复文案 |
| **无网络依赖** | 天气/账单/健康/物流数据需要 connector；不能默认上传 | `HttpClient` 0；无 OAuth | MVP 手工/CSV/MCP；UI 显示数据来源与更新时间 |
| **隐私与安全** | 健康、财务、证件属高敏感；本地优先、可导出、可删除 | 无加密/无审计；`env-variables` 只掩码示例 | 所有生活 block 在 `notes` 明示本地/加密/导出；数据分类分级由应用层做 |
| **本地化** | 日期固定 `en-US`、金额默认 USD；跨时区旅行会错 | `ArcDateMath.cs:3-7`；`ArcMoneyInput.cs:48` | UI 按**宿主传入的** locale/timezone 显示；时区/地区数据抽象不在本库 |
| **性能** | 交易/记录/日志列表上量后：零虚拟化 + 重建型 UI 会崩 | `docs/04` R4、G-P0-5/7、§2.6 | `expense-ledger`/`document-vault`/`bill-due-list` 必须分页或等 `ArcVirtualList`；不要用 100×8 的 DataGrid 直接吃真实数据 |
| **截图验收的盲区** | `ShotRunner` 默认 reduced-motion，动画/提醒路径从未在 CI 执行 | `docs/04` R7 | 生活域 P0 需补非 reduced-motion 的**状态截图**（延后/漏服/到期红点等 UI 状态） |
| **Pro 合规** | 不得抓取/照抄商业实现；新增项不能宣称与 Pro 一致 | `README.md:204-209`；`docs/03:180-187` | 4 个组件与 20 个 block 都是原创设计；只参考公开的交互意图与视觉语言，不复制源码/私有 API |

---

## 11. 工作量、依赖与路线图

### 11.1 工作量（UI 口径，按完成定义）

估算口径：S ≤ 1 人日、M = 2–4 人日、L = 1–2 周；按本仓库“控件 + ControlTheme + 页面 + 明暗截图”的完成定义，包含页面、文档结构与两套截图，不含非 UI。

| 优先级 | 项目数 | 中位估算 | 说明 |
| --- | --- | --- | --- |
| P0 | 7 | ≈ 36 人日 | 5 个 block（2 L + 3 M）+ 1 个 M 组件；另有非 UI 数据/调度/通知/加密 |
| P1 | 13 | ≈ 72 人日 | 10 个 block + 3 个组件；多为 L |
| P2 | 4 | ≈ 12 人日 | 4 个 M block |
| **合计** | **24** | **≈ 120 人日** | 单人约 24 周；2–3 人并行约 10–14 周（不含非 UI 与联调） |

> 非 UI 的粗估：本地数据层 + 调度器 + 通知投递 + 托盘/常驻 + 文件保险箱/加密 + 隐私设置，至少与 P0 的 UI 工作量同量级（保守再 +30–50% 总量）。本文件不把这些算进 120 人日，避免误导。

### 11.2 路线图（建议按依赖排序）

| 波次 | 内容 | 依赖 | 产出 |
| --- | --- | --- | --- |
| **W0 前置** | 数据模型（提醒/账单/用药/文档/清单/日记）、本地存储、时区/本地化、调度器、通知/托盘契约 | 03 号 `routine-schedule-editor`、`notification-routing`、`background-run-dock`；应用层平台适配 | 能存、能算下次触发、能投递 |
| **W1 P0 核心** | `reminder-row` → `day-agenda` → `reminder-center`；`document-vault`；`medication-schedule`；`bill-due-list`；`grocery-list` | W0；`file-dropzone`/`file-upload`/`sortable-data-table`/`checkbox`/`badge` | 可用的“提醒–议程–到期”闭环 |
| **W2 P1 基础** | `reference-band-chart`、`weather-forecast`、`habit-streak-strip`、`daily-brief`、`expense-ledger`、`budget-board`、`subscription-tracker` | W1 的列表/数据层；虚拟化（R4） | 生活一屏 + 财务/健康基础 |
| **W3 P1 场景** | `meal-planner`、`trip-itinerary`、`daily-journal`、`home-maintenance`、`smart-home-panel`、`focus-session` | W1/W2；天气/文档/提醒 | 六大生活域页面 |
| **W4 P2** | `pet-care`、`chore-board`、`energy-usage`、`package-inbox` | W1/W2/W3 | 长尾场景 |

### 11.3 跨域依赖表（合并时必须一起看）

| 生活域提案 | 依赖 03 号 Agent 域提案 | 不合并的后果 |
| --- | --- | --- |
| `reminder-row` / `reminder-center` / `bill-due-list` / `medication-schedule` / `chore-board` / `subscription-tracker` / `home-maintenance` / `pet-care` | `routine-schedule-editor`（RRULE/时区/错过策略） | 只能做固定频率预设，无法表达“每月最后一个周五”“每年生日” |
| `reminder-center` / `focus-session` / `daily-brief` | `notification-routing`（通道/静默/升级） | 只能页内提示；关窗后不提醒 |
| 所有“窗口关闭仍要提醒”的场景 | `background-run-dock` / `desktop-agent-settings`（托盘/常驻/自启） | 桌面助手退化为“打开才工作”的网页 |
| `reminder-center` vs `agent-attention-inbox` | 两者分开（生活提醒 vs Agent 审批） | 混在一起会让“完成医保提醒”变成“批准 Agent 发邮件” |

---

## 12. 校验方法与结果

### 12.1 校验脚本

校验脚本写在仓库外的临时目录（本文件只允许写两个交付文件，不往仓库加脚本），逻辑如下：

- 解析 `docs/arc-catalog.json` 的 `items`/`specialItems`/`blockItems`，得到 294 个现有 slug；
- 校验 JSON：schema、domain、24 个场景 id 唯一、item 数量 20–30、slug kebab-case 且不与 294 冲突、文件内不重复；
- 校验 `kind`/`priority`/`effort`/`asset` 枚举、`project` 必须在 `src/` 下存在、组件分类必须在现有 23 类内、block 分类为新增 `Daily life`；
- 校验每个 item 至少 1 个场景、场景 id 必须存在、所有场景至少被 1 个 item 使用；
- 校验 `composes` 只能引用现有 slug 或本文件 slug，且不能自引用；
- 统计优先级/kind/effort。

复现方式（临时脚本 `validate-life-proposals.ps1`，路径在本次运行的 `%TEMP%` 下）：

```powershell
pwsh -NoProfile -File <临时目录>\validate-life-proposals.ps1
```

### 12.2 本次运行结果

```text
=== arc-extension proposals validation ===
catalog entries: 294 (components 128 / special 55 / blocks 111)
items: 24 (P0 7 / P1 13 / P2 4)
kinds: component 4 / block 20 / special 0
scenarios: 24 (all covered by items: True)
compose refs: existing 136 / in-file 16
slug collisions with 294 existing: 0
duplicate slugs in file: 0
errors: 0
warnings: 0
effort P0: 7 items, ~36 person-days (midpoint)
effort P1: 13 items, ~72 person-days (midpoint)
effort P2: 4 items, ~12 person-days (midpoint)
effort total: ~120 person-days (midpoint; UI only per repo DoD)
```

### 12.3 与 03 号 Agent 提案的交叉校验

```text
life items: 24; agent items: 28; slug overlap: 0
scenario id overlap: 0
life composes referencing agent proposals: 0
```

说明：

- 生活域与 Agent 域的 slug、场景 id 完全不重叠；
- 按任务规则，`composes` 不能引用 03 号文件里的 slug，因此生活域提案没有把 `routine-schedule-editor`/`notification-routing`/`background-run-dock` 写进 `composes`，而是在 `notes` 与 §11.3 里声明依赖。**合并后建议由 Lead 把这三项作为共享前置，允许跨域引用**（或在 `arc-extension-catalog.json` 中加 `dependsOn` 字段），否则生活域的 `notes` 依赖无法被机器校验。
- 建议合并时统一 `asset`：`routine-schedule-editor` 是 agent asset 但被 life 依赖；`reference-band-chart` 是 foundation asset 但被 life 使用。不要因为 asset 不同就删除依赖关系。

---

## 13. 风险、待决与建议

### 13.1 风险清单（按严重度）

| # | 风险 | 影响 | 缓解 |
| --- | --- | --- | --- |
| R1 | **时区/DST/本地化**：现有 pickers 固定 `en-US`、naive `DateTime`/`TimeSpan` | 旅行/服药/账单在跨时区时错点；DST 当天重复/跳过 | 数据层用 `DateTimeOffset` + IANA 时区；foundation 增加 locale/timezone 抽象；UI 显示时区 |
| R2 | **跨域依赖**：生活提醒依赖 03 号的排期/通知/托盘 | 只合并生活目录时，提醒无法重复/无法系统触达 | W0 先合并三项 03 号提案；或把 `ScheduleSpec` 作为 foundation 共享类型 |
| R3 | **零虚拟化/重建型 UI**（`docs/04` R4、G-P0-5） | 真实流水/文档/日志上量后卡顿、焦点丢失 | P0 的列表先分页；W2 前完成 `ArcVirtualList` 或等价方案 |
| R4 | **隐私/安全**：健康/财务/证件高敏感，仓库无加密/无审计 | 数据泄露风险；用户不信任 | 本地加密、可导出/删除、最小权限、明示数据用途；敏感域不默认进搜索/摘要 |
| R5 | **桌面触达**：无 tray/系统通知/后台常驻 | 关窗后提醒失效；错过补发混乱 | 依赖 03 号 + 应用层；休眠唤醒重算 `missed`，逐条处理 |
| R6 | **性能热点**：提醒/倒计时若每条起 timer，会重演 `docs/04` R1/G-P0-2 | 空闲 CPU 高、电量差 | 单帧时钟 + 可见性门控；相对时间分钟级刷新 |
| R7 | **截图验收盲区**：`ShotRunner` 默认 reduced-motion（`docs/04` R7） | 提醒触发/动画路径没在 CI 被跑过 | P0 加非 reduced-motion smoke：到点、延后、漏服、红点 |
| R8 | **范围过大**：24 项 ≈ 120 人日 UI + 非 UI | 长期铺开、迟迟不可用 | 先做 7 个 P0；P1/P2 按场景验证后再排 |
| R9 | **Pro 合规/表达**：新增项不得暗示复刻商业实现 | 授权与表述风险 | 独立实现；文档写“原创设计，参考公开交互意图”；不复制 Pro API |
| R10 | **分类学变更**：新增 block 组 “Daily life”、可能新建 `Arc.Avalonia.BlocksLife` | Gallery/审计/构建需要同步改 | 合并时一次改 `arc-catalog.json`、`ArcCatalog`、`ArcUI.slnx`、主题入口与审计；先加分类再加页面 |

### 13.2 待决问题（建议 Lead 拍板）

1. **是否把 `routine-schedule-editor` / `notification-routing` / `background-run-dock` 作为 life 扩展的前置依赖**，还是允许生活目录自带一套简化排期？（建议：共享，不重复造）
2. **是否新建 `Arc.Avalonia.BlocksLife`**，还是把 20 个 block 分散进现有 6 个 Blocks 项目？（建议：新建，否则 BlocksApp 会膨胀到 41 个页面，且生活域没有独立导航组）
3. **`reference-band-chart` 的归属**：foundation 扩展 vs Charts 新条目？（建议：先做 `LineChart.Band` 扩展；只有需要独立页面时再升条目）
4. **`habit-streak-strip` 是否独立**：先做 `activity-heatmap` 的周/月窗口，还是直接独立组件？（建议：先变体，P1 后看页面需要）
5. **数据层选型**：SQLite / LiteDB / JSON + 文件？（建议：应用层 SQLite + 迁移；UI 项目不依赖数据库）
6. **通知平台范围**：Windows only / 三平台？Avalonia `TrayIcon` 与系统通知在三平台行为不同，需要先定支持矩阵。
7. **地图/天气/银行/健康/物流的接入边界**：MVP 手工/CSV/MCP，还是一开始就接第三方 API？（建议：MVP 手工/CSV + 明确“数据来源与更新时间”）

### 13.3 给 Lead 的三条落地建议

1. **先合并“提醒–议程–到期”链路**：`reminder-row` → `day-agenda` → `reminder-center` → `document-vault`/`bill-due-list`/`medication-schedule`。这 7 项决定“是不是一个能过日子的工具”，其余都是增强。
2. **把排期/通知/托盘作为跨域底座**：不要让生活域和 Agent 域各造一套；把 `ScheduleSpec`、通知通道枚举、托盘契约放到 foundation/共享层，两个目录都引用。
3. **在合并目录里加“非 UI 前置”章节**：生活域 40% 的工作量不在 UI；如果主报告只列组件，会严重低估路线图。

---

## 附录 A：场景 → 提案映射

| 场景 | 主要提案 |
| --- | --- |
| `morning-brief` | `day-agenda`、`daily-brief`、`reminder-row`、`weather-forecast` |
| `medication-plan` | `medication-schedule`、`reminder-row` |
| `bill-due` | `bill-due-list`、`reminder-center`、`reference-band-chart`、`subscription-tracker` |
| `weekly-meal-plan` | `meal-planner`、`grocery-list` |
| `trip-itinerary` | `trip-itinerary`、`weather-forecast`、`document-vault` |
| `home-repair` | `home-maintenance`、`document-vault` |
| `mood-journal` | `daily-journal`、`habit-streak-strip` |
| `focus-block` | `focus-session`、`daily-brief` |
| `parcel-tracking` | `package-inbox`、`reminder-center` |
| `subscription-audit` | `subscription-tracker`、`expense-ledger` |
| `id-expiry` | `document-vault`、`reminder-center` |
| `report-archive` | `document-vault`、`reference-band-chart`、`medication-schedule` |
| `relationship-upkeep` | `daily-journal`、`reminder-center`、`team-directory`（现有） |
| `home-energy` | `energy-usage`、`reference-band-chart`、`smart-home-panel` |
| `pet-care` | `pet-care`、`reminder-row` |
| `grocery-run` | `grocery-list`、`meal-planner` |
| `chore-rota` | `chore-board`、`reminder-row` |
| `sleep-trend` | `reference-band-chart`、`daily-journal` |
| `expense-split` | `expense-ledger`（分摊子视图） |
| `smart-home-scene` | `smart-home-panel` |
| `spend-capture` | `expense-ledger`、`budget-board` |
| `appointment-prep` | `medication-schedule`、`document-vault`、`reminder-row` |
| `outdoor-plan` | `weather-forecast`、`day-agenda`、`trip-itinerary` |
| `family-logistics` | `day-agenda`、`chore-board`、`reminder-center` |

## 附录 B：引用文件与行号索引

- `docs/arc-catalog.json`、`README.md`（294 项、219 页/438 截图）、`docs/01-arc-analysis.zh.md`（设计原则）、`docs/03-implementation.zh.md`（范围与完成定义）、`docs/04-design-and-performance-review.zh.md`（R1/R4/R5/R7、G-P0-3/4/5/6/7）。
- `src/Arc.Avalonia.Pickers/Controls/ArcCalendar.cs:20-45,135`（属性与事件）。
- `src/Arc.Avalonia.Pickers/Controls/ArcCalendarSurface.cs:208-332`（只画日期/区间/今天点）。
- `src/Arc.Avalonia.Blocks.Team/Pages/WeekCalendarPage.cs:35-45,74-77`（静态事件、拖拽未复刻）。
- `src/Arc.Avalonia.Pickers/Controls/ArcDateMath.cs:3-7`（固定 en-US）。
- `src/Arc.Avalonia.Pickers/Controls/ArcTimePicker.cs:26`（`TimeSpan?` 时间值）。
- `src/Arc.Avalonia.Inputs/Controls/ArcMoneyInput.cs:48`（默认 USD）。
- `src/Arc.Avalonia.Blocks.App/Pages/NotificationCenterPage.cs:29,51,246-259`（只读/忽略、样例）。
- `src/Arc.Avalonia.Blocks.App/Pages/InboxTriagePage.cs:18,51`（消息级 snooze，时间不可编辑）。
- `src/Arc.Avalonia.Blocks.Commerce/Pages/BillingOverviewPage.cs:18,52`（SaaS 账单）。
- `src/Arc.Avalonia.Blocks.Data/Pages/BudgetVariancePage.cs:15,51`（部门预算）。
- `src/Arc.Avalonia.Blocks.Commerce/Pages/UsageBillingPage.cs:53`、`WalletCardPage.cs:52`、`InvoiceStudioPage.cs:53`（样例/无账本）。
- `src/Arc.Avalonia.Special.Glass/Pages/ControlCenterPage.cs:44-63`（工作区快捷设置）。
- `src/Arc.Avalonia.Special.AIWidgets/Pages/ActivityRingsPage.cs:54-62`（三目标/年热力图建议）。
- `src/Arc.Avalonia.Feedback/Pages/DataDemos.cs:302-330`（heatmap 一天一值）。
- `src/Arc.Avalonia.Blocks.Commerce/Pages/DeliveryTrackerPage.cs:16,50`（单订单、无 webhook）。
- `src/Arc.Avalonia.Blocks.App/Pages/FileUploadPage.cs:58`（从不落盘）。
- `src/Arc.Avalonia.Pickers/Controls/ArcFileDropzone.cs:308-317`（文件选择器）。
- `src/Arc.Avalonia.Blocks.App/Pages/MediaPlayerPage.cs:53`（无音频）。
- `src/Arc.Avalonia.Blocks.AI/Pages/DocumentExtractPage.cs:30`（抽取审阅）。
- `src/Arc.Avalonia.Blocks.Team/Pages/TeamDirectoryPage.cs:29`、`AvailabilityPickerPage.cs:47`、`KanbanBoardPage.cs:41`、`ProjectBoardPage.cs:39`（团队语义）。
- `src/Arc.Avalonia.Content/Pages/ContentFlowsPage.cs:63-101`、`ContentPage.cs:56`、`src/Arc.Avalonia.Content/Controls/ArcReorderableList.cs:24-31`（上手清单/排序列表语义）。
- `CONVENTIONS.md:55-58`（完成定义）、`README.md:204-209`、`docs/03-implementation.zh.md:180-181`（Pro 处理方式）。
- 全仓 grep（排除 obj/bin）：`Cron` 0、`DueDate` 0、`Medicat` 0、`Dosage` 0、`Recipe` 0、`Meal` 0、`Grocer` 0、`Itinerar` 0、`Thermostat` 0、`Symptom` 0、`Workout` 0、`Energy` 0、`TrayIcon` 0、`HttpClient` 0、`Sqlite` 0、`Cryptography` 0；`Recurr` 9（均为文案）、`Remind` 2（标签）、`Habit` 1（说明文字）。




