# R4 评审：AtomUI/Semi 竞品能力面与"可替代性"差距

> **任务**：task-8（R4 评审）｜**评审人**：ecosystem-benchmark｜**日期**：2026-11-05
> **评审对象**：`docs/13-framework-layering-and-agent-top.zh.md`（v3，2026-11-05）§2.0/§2.2/D3/D4、WBS 的 G1/G3，以及 D 报告（`docs/analysis/13-d-ecosystem-benchmark.zh.md`）的生态结论。
> **核心问题**：**Arc 能否真正替代 AtomUI 这类 UI 组件库**（用户明确这是目标）。
> **方法**：① 本地 NuGet 缓存核实（`D:\.nuget\packages`，仅 Semi/SukiUI/FluentAvalonia 等有缓存，AtomUI/Ursa/Material 不在本机缓存）；② 联网核实（`nuget.azure.cn` 镜像的 nuspec/版本、GitHub Contents API、官方 README/架构文档）。**联网状态：可用（2026-11-05 实测获取成功）**。
> **约束**：只写本文件；未修改主文档/源码；未运行 `dotnet build`。证据编号见 §6：`[W*]` 外部、`[R*]` 仓库、`[D]` 上一份 D 报告。`C` = 未验证/推断，`B` = 由 A 推导。
> **一句话判定**：**v3 能把 Arc 做成"一个可发布的 UI 组件库"，但按 v3 当前 M1/M2 范围，还不能"替代 AtomUI"**——差在 4 个消费者可见面：**内建控件主题覆盖（10 人日只覆盖 6 类表面）、图标（161 vs 843 且无扩展点）、本地化（0 vs 内置 3 语言）、服务层/窗口壳（v3 放在 M2）**。建议加 **21–33 人日**（其中主题从 10→20–25），把"替代 AtomUI"变成可验收的声明；否则应把定位降级为"Arc 组件库 + 迁移指南"。

---

## 0. 一页结论

### 0.1 竞品事实（只列对"可替代性"有决定性影响的）

| 事实 | AtomUI 6.2.12 | Semi.Avalonia 12.1.0.1 | Ursa 2.3.0 | Material.Avalonia 3.21.0 | Arc v3 计划 |
|---|---|---|---|---|---|
| 定位 | Ant Design 6 组件系统 + 深度 token 主题系统 [W1][W2][W6] | 纯主题（换肤）[W11] | 控件库 + 独立主题包 [W13] | 主题 + 少量附加控件 [W16] | UI 组件库（v3 自述）[R1] |
| Avalonia 12 支持 | 12.1.4 / net10+net8 [W1][W3] | 12.1.0 [W12] | 12.0.2 [W14] | 12.1.1（net8）[W17] | 12.1.4（net10）[R1] |
| 内建控件主题覆盖 | **全量**（81 个控制/样式目录含 Input/ListBox/ScrollViewer/TextBlock/Window/Menu/TabControl…）[W8] | 全量（主题库）[W11] | 由 `Irihi.Ursa.Themes.Semi` 主题包提供 [W13] | "almost all Avalonia controls" [W16] | **10 人日定向覆盖 6 类表面**（TextBox/ScrollBar/ListBoxItem/Popup/ToolTip/Window 背景），不重写模板 [R1] |
| 附带控件 | **~76 组**（Message/MessageBox/Notifications/Dialog/Drawer/QRCode/Tour/Transfer/Cascader/TreeSelect/Form/Descriptions/Statistic…）+ DataGrid/ColorPicker/Extras 独立包 [W8] | 无新控件，仅 6 个附加**主题**包 [W11] | 大量控件（Dialog/MessageBox/Notification/Toast/TitleBar/ThemeSelector/Form/QRCode/PathPicker/PinCode/TagInput…）[W15] | Snackbar/侧板/浮动按钮/卡片/对话框等附加控件 [W16] | 151 个 `Arc*` 控件 [R4]；缺 Form/Transfer/Cascader/TreeSelect/Descriptions/Statistic/QRCode/Tour/FloatButton 等 [R8] |
| 图标 | **843 个**（Ant Design，Outlined/Filled/TwoTone 生成目录）[W9][W10] | 无独立图标包（主题库，C）[W11] | `Controls/Icons` 目录存在，规模未核实（C）[W15] | 图标**单独安装** `Material.Icons`（C：数量未核）[W16] | **161 个** Lucide 图标（`ArcIconData` 实测 161 条）[R5]；`Paths` 只读、无注册扩展点 [R5] |
| 本地化 | **内置 en-US / zh-CN / zh-TW** + 官方 `AtomUI.I18n.*` 附加语言包机制 [W7] | `SemiTheme Locale="zh-CN"`（主题内置本地化）[W11] | 未核实（C） | 未宣传（C） | **0**：无 `.resx`、英文 DP 默认值 116 处、v3 把 C5 放 M3（15 人日）[R1][R7] |
| 对话框/通知服务 | Dialog/Drawer/Message/MessageBox/Notifications 控件 + 服务 API（主题文档中静态 Dialog/Notification API）[W6][W8] | 不提供（主题库，C）[W11] | Dialog/MessageBox/Notification/Toast 控件 [W15] | `Material.Avalonia.Dialogs` 包 + DialogHost.Avalonia（第三方）[W16] | **0 服务接口**；C2（4 服务 + Overlay host + ArcWindow）**24 人日放 M2** [R1][R3][R7] |
| 主题切换/定制 API | `IThemeManager` + `ThemeConfig`（Seed/Map/Alias/Control token、算法 Default/Dark/Compact、局部 ThemeConfigProvider、XML 主题、用户主题目录、运行时事务切换）[W6] | `SemiTheme`（Locale/主题变体）[W11] | `ThemeSelector`/`ThemeVariantMapper` 控件 + 主题包 [W15] | `MaterialTheme`（BaseTheme/PrimaryColor/SecondaryColor，运行时调色板）[W16] | `ArcThemeManager`（Light/Dark + **固定 8 accent** + ReducedMotion）+ `ArcTheme` 入口；**无自定义 accent / 无 token 覆盖 API** [R6] |
| Token 暴露 | 强类型 token 资源扩展 `{atom:XxxTokenResource}` + `ThemeConfig.Tokens` 覆盖 [W6] | 主题资源键（C） | 主题资源键（C） | 主题资源键（C） | 82 个 `ArcTokens` **public const** + `ArcResources` 类型化读取 [R14]；无 token 覆盖 API |
| 窗口壳 | `Window` + `WindowTitleBar` + `AtomUI.Native` 原生窗口能力 [W3][W8] | 不提供（主题） | `TitleBar`/`UrsaWindow` 类控件 [W15] | 不提供（主题） | 0 `ArcWindow`/标题栏；v3 C2 里"可选自绘标题栏" [R1] |
| 许可证 | **LGPLv3**（链接发布二进制可免费；改源码需开源或商业授权）[W1] | MIT [W12] | MIT [W14] | MIT [W17] | MIT（计划）[R1] |
| 文档/示例 | Gallery 应用 + 文档站 + Samples 仓库 [W1][W2] | 文档站 + 浏览器预览 [W11] | wiki + demo | wiki + demo 截图 [W16] | 294 条目文档 + 328 页面 + 截图审计（强项）[R4][R10] |

### 0.2 三个"最关键的断层"

1. **主题覆盖断层（阻断"替代"声明）**：AtomUI 是"所有控件默认被主题化"；Arc v3 的 10 人日只覆盖 **6 类**内建控件表面（且明确"不重写模板"），裸 `ComboBox`/`Menu`/`TabControl`/`Button`/`CheckBox`/`RadioButton`/`ToggleSwitch`/`Slider`/`NumericUpDown`/`ProgressBar`/`Expander`/`TreeView` 等仍是 Fluent 外观 [R1]。迁移业务应用不可能一次性全部改用 `Arc*` 控件——**这是"替代 AtomUI"与"Arc 自有组件库"的分水岭**。
2. **图标断层（近阻断）**：AtomUI 843 个图标有成套命名与强类型 catalog；Arc 161 个 Lucide 描边图标且 `ArcIconData.Paths` 只读、无注册 API [R5]——缺的图标**画不出来**（可换用自绘控件绕过，但迁移成本陡增）。
3. **本地化 + Theme API 断层（需补）**：AtomUI 内置 zh-CN/zh-TW；Arc 0 `.resx`，分页/日历/对话框等内建文案为英文（v3 C5 放 M3）[R1][R7]。AtomUI/Semi/Material 都允许消费者改品牌色/种子 token；Arc 只有固定 8 accent、无自定义 accent 与 token 覆盖 API [R6]——**B 端品牌定制场景直接被卡**。

### 0.3 建议（给 Lead 的决策点）

| 决策 | 建议 | 增量 |
|---|---|---|
| 内建控件主题从 10 人日定向 → Top 15–20 控件 20–25 人日 | **做**（ROI 高，见 §4.2）；但 scope 为"资源键覆盖为主 + 少数模板替换"，不追求 78 个全量 | **+10–15 人日**（M1） |
| 图标扩展点 + 常用图标补齐（不追 843） | **做**：`ArcIcon` 支持自定义 path/`Register`、图标 provider 接口 + 100 个高频补齐 | **+3–5 人日**（M2） |
| 本地化最小集（en-US/zh-CN，覆盖分页/日历/时间/对话框/Toast 等内建文案） | **前移**到 M2（从 C5 中切片） | **+5–8 人日**（M2） |
| Theme API：公开 token 覆盖 + 自定义 accent（品牌色） | **做**：`ArcThemeOptions`（accent Color / 密度 / 圆角） | **+3–5 人日**（M2） |
| 服务层 + 窗口壳（v3 已计划 C2） | 维持 M2；**不要**把"替代 AtomUI"的对外声明放在 M1 | 0（已在 24 人日内） |
| 9 个缺失控件（Form/Transfer/Cascader/TreeSelect/Descriptions/Statistic/QRCode/Tour/FloatButton） | **P1/P2 可选**；其中 Form(+验证) 建议 P1 | +6–10（Form）～+18–30（全量） |
| **对 v3 总盘的影响** | P0 最小替代集把 357 → **378–390 人日**（仍在 v3 的 280–470 区间内）；若加 Form 与 9 控件 → 402–430 | **+21–33（P0）/ +45–73（含 P1/P2）** |

> **结论（对用户目标）**：如果"替代 AtomUI"是发布级目标，**必须采用 P0 最小替代集**；如果坚持 357 人日不增，请把对外表述改为"Arc 组件库（推荐新项目）+ 迁移指南（非 drop-in）"，并在 README 明示"只用 Arc 组件"的边界——但这条边界在消费端**不可强制**（见 §4.5），因此不能替代主题覆盖。

---

## 1. 方法与边界

- **本地缓存**：`D:\.nuget\packages` 现只有 `semi.avalonia`（12.1.0.1）、`semi.avalonia.datagrid`、`fluentavaloniaui`、`sukiui` 等，**无 AtomUI/Ursa/Material**；因此三家竞品以联网证据为主，Semi 有本地 nuspec 实证 [W12]。
- **联网证据**：NuGet 镜像 `nuget.azure.cn`（nuspec/版本清单）、GitHub Contents API（目录/生成文件）、官方 README 与架构文档。所有 URL 列入 §6。
- **不做的**：不反编译竞品 DLL、不运行竞品、不评估竞品性能；"能力面"只按包/文档/生成物的事实列。
- **与 D 报告的关系**：D 报告已确立"主题单列成包 / 附加控件主题矩阵 / 官方主题 80 个 ControlTheme"等生态结论 [D]；本报告不重复，只在"AtomUI 可替代性"上做增量判断。

---

## 2. 能力面对照

### 2.1 竞品能力面矩阵（消费者视角）

| 能力项 | AtomUI 6.2.12 | Semi.Avalonia 12.1.0.1 | Ursa 2.3.0 | Material.Avalonia 3.21.0 | 证据 |
|---|---|---|---|---|---|
| 内建控件是否全量换肤 | ✅ 全量（Ant Design 6 token 化） | ✅ 全量（主题库） | ✅ 由主题包提供 | ✅ "almost all" | [W8][W11][W13][W16] |
| 附加控件（超出内建） | ✅ ~76 组 + DataGrid/ColorPicker/Extras 包 | ❌（仅附加**主题**包，不是控件） | ✅ 大量（Dialog/Message/Notification/TitleBar/Form/QRCode…） | ✅ 少量（Snackbar/侧板/FAB/卡片/对话框） | [W8][W11][W15][W16] |
| 图标体系 | ✅ 843（生成 catalog，843 项） | ❌ 无独立图标包（C） | ⚠️ `Controls/Icons`（规模 C） | ⚠️ 图标单装 `Material.Icons`（数量 C） | [W9][W10][W11][W15][W16] |
| 本地化 | ✅ en-US/zh-CN/zh-TW 内置 + 语言包机制 | ✅ `Locale="zh-CN"` | ⚠️ 未核实 | ⚠️ 未宣传 | [W7][W11] |
| 对话框/通知服务 | ✅ Dialog/Message/MessageBox/Notifications | ❌（主题库） | ✅ Dialog/MessageBox/Notification/Toast | ✅ Dialogs 包 + DialogHost.Avalonia | [W6][W8][W11][W15][W16] |
| 主题切换 API | ✅ ThemeManager + ThemeConfig（算法/作用域/运行时） | ✅ SemiTheme（Locale/变体） | ✅ ThemeSelector/ThemeVariantMapper | ✅ MaterialTheme（运行时调色板） | [W6][W11][W15][W16] |
| Token 定制 | ✅ Seed/Map/Alias/Control 四层 + 局部覆盖 | ⚠️ 主题资源（C） | ⚠️ 主题资源（C） | ✅ Primary/Secondary 色 | [W6][W11][W15][W16] |
| 窗口壳/标题栏 | ✅ Window + WindowTitleBar + Native | ❌ | ✅ TitleBar/UrsaWindow | ❌ | [W3][W8][W15] |
| 字体 | ✅ AlibabaSans/AlibabaPuHuiTi 字体包 | ❌（跟随系统） | ❌ | ❌ | [W4][W11] |
| 文档/示例 | ✅ Gallery + 文档 + Samples | ✅ 文档站 + 浏览器预览 | ✅ wiki + demo | ✅ wiki + demo | [W1][W11][W13][W16] |
| 许可证 | ⚠️ LGPLv3 | ✅ MIT | ✅ MIT | ✅ MIT | [W1][W12][W14][W17] |
| Avalonia 12 | ✅ 12.1.4 | ✅ 12.1.0 | ✅ 12.0.2 | ✅ 12.1.1 | [W1][W12][W14][W17] |

> 注：AtomUI 的"全量换肤"由其 `Desktop.Controls` 目录（含 Input/ListBox/ScrollViewer/TextBlock/Window 等内建控件样式目录）与"Ant Design 6 组件系统"定位推断（B 级），本轮未逐控件截图核对；其余竞品同理，除本文明确标注 A 级的包/版本/依赖事实外，能力描述可能有 ±1 档误差。

### 2.2 Arc v3 计划 / 未提供 对照（对 AtomUI）

| 能力项 | AtomUI 现状 | Arc v3 计划 | Arc 未提供 | 判定 |
|---|---|---|---|---|
| 内建控件主题覆盖 | 全量 ~76 组 | 10 人日定向覆盖 **6 类**：TextBox/ScrollBar/ListBoxItem/Popup/ToolTip/Window 背景；不重写模板 [R1] | **~70 组**内建控件无 Arc 皮肤（ComboBox/Menu/TabControl/Button/CheckBox/RadioButton/ToggleSwitch/Slider/NumericUpDown/ProgressBar/Expander/TreeView/DatePicker…） | **阻断**（对"替代"声明）；需补 20–25 人日 |
| 附加控件 | ~76 组 | 151 个 Arc 控件 [R4]，覆盖大多数类别 | Form(+验证)/Transfer/Cascader/TreeSelect/Descriptions/Statistic/QRCode/Tour/FloatButton 等 9 组 [R8] | **需补**（P1/P2；可用组合替代，但迁移成本上升） |
| 图标 | 843 | 161 Lucide（v3 已列）[R4][R5] | 无扩展点（`Paths` 只读、`ArcIcon` 只按名查表）[R5]；无图标 provider/别名映射 | **阻断**（图标密集应用），先补扩展点可降为"需补" |
| 本地化 | en-US/zh-CN/zh-TW 内置 | C5（15 人日）放 **M3** [R1][R3] | 内建文案（分页/日历/时间/对话框/Toast）无 zh-CN；0 `.resx` [R7] | **需补**；建议切 5–8 人到 M2 |
| 对话框/通知服务 | Dialog/Message/MessageBox/Notifications | C2（24 人日）放 **M2** [R1][R3] | M1 交付物无服务接口；`ArcDialog` 仍"填满最近宿主面板" | **需补**（M2 可解决）；M1 阶段 **阻断** |
| 主题切换 API | ThemeConfig + 算法 + 局部作用域 | `ArcThemeManager`（2 scheme × 8 accent）+ `ArcTheme` 入口（3 人日）[R6] | 自定义 accent/品牌色；公开 token 覆盖；密度/圆角配置 | **需补**（+3–5 人日）；深度 token 系统**不必对标** |
| Token 暴露 | 强类型 token 资源扩展 | 82 个 public `ArcTokens` + `ArcResources` [R14] | token 覆盖 API、token 文档/稳定性承诺 | **需补**（小） |
| 窗口壳 | Window + WindowTitleBar + Native | C2 中"ArcWindow（自绘标题栏可选）" [R1] | M1 无窗口壳；依赖 Fluent 的 `WindowDrawnDecorations` | **需补**（M2）；迁移初期可接受系统 chrome |
| 表单/校验 | Form 控件 + 校验 | 未列（C1 契约、"表单与数据展示模式"泛述）[R1] | 无 `DataValidationErrors`/`INotifyDataErrorInfo` 使用 [R7] | **需补**（+6–10 人日，P1） |
| 表格虚拟化/编辑 | DataGrid 独立包（行/列虚拟化、编辑） | C3（20 人日）只做 `ItemsSource` + Avalonia 自带虚拟化；**不整体采用官方 DataGrid** [R1] | DataGrid/TreeTable 行虚拟化、单元格编辑、列虚拟化 | **需补**（P1/P2，8–15 人日或接受为限制） |
| 文档/示例 | Gallery + Samples | 294 条目文档 + 328 页面 + 截图审计 [R4][R10] | **消费端**快速开始/迁移指南（当前文档面向 gallery 内部） | **可接受**（强项），补迁移文档即可 |

---

## 3. 迁移断层清单（AtomUI → Arc）

> 严重度定义：**可接受** = 不影响业务应用落地/有等价绕法；**需补** = 必须在某里程碑补齐才能对外宣称可替代；**阻断** = 不补则"替代 AtomUI"不成立（或迁移成本接近重写）。

| # | 断层 | AtomUI 侧事实 | Arc v3 侧事实 | 严重度 | 补齐成本 | 归属 |
|---|---|---|---|---|---|---|
| F1 | 裸 `Window`/标题栏外观 | Window + WindowTitleBar + Native 窗口能力 [W3][W8] | 无 `ArcWindow`/标题栏；v3 C2 才做（M2）；v12 依赖 Fluent 的 `WindowDrawnDecorations` | **需补** | 含在 C2（24） | 组件库 |
| F2 | 裸 `TextBox`/`ComboBox`/`ScrollBar` 皮肤 | 全量主题化 [W8] | 10 人日只覆盖 TextBox/ScrollBar/ListBoxItem/Popup/ToolTip/Window 背景；ComboBox 未列 [R1] | **阻断**（ComboBox 是最高频输入控件） | 主题 uplift 内 | 组件库 |
| F3 | 裸 `Menu`/`MenuItem`/`ContextMenu` | 全量主题化 [W8] | 未覆盖；Arc 有 `ArcDropdownMenu`/`ArcContextMenu` 但宿主菜单/右键菜单通常是裸的 | **需补** | 主题 uplift 内 | 组件库 |
| F4 | 裸 `TabControl`/`TabItem` | 全量主题化 [W8] | 未覆盖；ArcTabs 是自绘控件，不能替换任意 `TabControl` 用法 | **需补** | 主题 uplift 内 | 组件库 |
| F5 | 裸 `Button`/`CheckBox`/`RadioButton`/`ToggleSwitch`/`Slider`/`NumericUpDown`/`ProgressBar`/`Expander`/`TreeView` | 全量主题化 [W8] | 未覆盖（Arc 有对应自绘控件，但消费端裸控件仍 Fluent） | **需补** | 主题 uplift 内 | 组件库 |
| F6 | 图标数量/命名 | 843 个 Ant Design 图标（强类型 catalog）[W9] | 161 Lucide；`Paths` 只读、无注册/别名/provider [R5] | **阻断**→加扩展点后**需补** | 3–5 人日（扩展 API + 100 高频） | 组件库 |
| F7 | 本地化（zh-CN/zh-TW/en-US） | 内置 3 语言；语言包机制 [W7] | 0 `.resx`；v3 C5 放 M3；分页/日历/对话框内建文案英文 [R1][R7] | **需补**（M2 前阻断中文企业应用） | 5–8 人日（切片） | 组件库 |
| F8 | 品牌色/token 定制 | ThemeConfig 可覆盖任意 Global/Control token；Semi/Material 也可改色 [W6][W11][W16] | 固定 8 accent 枚举；无自定义 accent；无 token 覆盖 API [R6] | **需补**（品牌定制应用阻断） | 3–5 人日 | 组件库 |
| F9 | 对话框/通知服务 | Dialog/Message/MessageBox/Notifications 服务 API [W6][W8] | 0 服务接口；`ArcDialog` 靠宿主面板；C2 在 M2 [R1][R3][R7] | **需补**（M1 期阻断，M2 解决） | 24（C2，已计划） | 组件库 |
| F10 | 表单与校验 | Form 控件 + 校验 | 无 Form/校验基础设施 [R7][R8] | **需补** | 6–10 人日 | 组件库 |
| F11 | 数据网格高级能力 | DataGrid 独立包（编辑/虚拟化/筛选）[W8] | C3 只做数据驱动/自带虚拟化，不自研网格虚拟化 [R1] | **需补**（大数据表格场景） | 8–15 人日 或 文档限制 | 组件库 |
| F12 | 缺失组合控件 | Transfer/Cascader/TreeSelect/Descriptions/Statistic/QRCode/Tour/FloatButton | 无对应类 [R8]（部分可用现有组件组合） | **可接受**（P2；用组合替代，需文档） | 18–30 人日（若全补） | 组件库 |
| F13 | 视觉设计语言 | Ant Design | Arc 自有 token（OKLCH/8 accent） | **可接受**（迁移必然重画；这是 Arc 身份） | 0 | — |
| F14 | MVVM 集成 | 间接依赖 ReactiveUI.Avalonia [W3] | 不依赖任何 MVVM 库 [R1] | **可接受**（消费者自选 CommunityToolkit 等） | 0 | — |
| F15 | 字体（中文设计字体） | AlibabaSans/AlibabaPuHuiTi 字体包 [W4] | Inter + 系统回退 [R1] | **可接受**（CJK 走系统字体；观感差异） | 0–2 | — |
| F16 | 许可证 | LGPLv3（改源码需开源/商业授权）[W1] | MIT（计划）[R1] | **可接受/优势**（Arc 对商业闭源更友好） | 0 | — |

**断层总结**：**阻断 1 项（主题覆盖，F2）、近阻断 2 项（图标 F6、本地化 F7）、需补 6 项（F1/F3/F4/F5/F8/F9/F10/F11）**。其中 F1/F9 已在 v3 M2 计划内；**F2–F8/F10/F11 是 v3 计划未覆盖或严重不足的部分**。

---

## 4. "最小可替代集"与主题覆盖决策

### 4.1 判定框架：什么才算"能替代 AtomUI"

一个业务应用从 AtomUI 迁到 Arc，需要同时满足 3 类条件：

1. **外观一致**：应用里**裸用的**内建控件（Window/输入/菜单/标签页/滚动条/对话框宿主）与 Arc 自绘控件不产生明显视觉断层。
2. **能力可替**：AtomUI 提供的图标/本地化/对话框通知/主题定制/表单校验能用 Arc 的等价物完成。
3. **迁移可行**：不是"全部页面重写"；允许先替换壳与关键页，其余裸控件逐步迁移（这正是 AtomUI 用户切换库时的真实路径）。

v3 现状：3 只满足一半（Arc 控件覆盖大部分业务控件），1 不满足（10 人日/6 类表面），2 部分（服务层 M2、本地化 M3、图标无扩展）。

### 4.2 核心决策：10 人日定向 vs 20–25 人日 Top 15–20，值不值？

**结论：值，建议采用 20–25 人日；但必须 scope 为"资源键覆盖为主、模板替换为辅"，且把"不重写模板"从绝对原则改为"默认不重写"。**

| 选项 | 覆盖 | 人日 | 收益 | 代价/风险 | 结论 |
|---|---|---|---|---|---|
| A（v3 现状） | 6 类表面（TextBox/ScrollBar/ListBoxItem/Popup/ToolTip/Window 背景） | 10 | 仅 Arc 自有页面视觉一致 | 消费端裸控件断层；"替代 AtomUI"不成立 | 不够 |
| B（建议） | **Top 15–20**：Window/WindowDrawnDecorations、TextBox、ComboBox(+Item)、ScrollViewer/ScrollBar、ListBox(+Item)、Menu/MenuItem/ContextMenu、TabControl/TabItem、Button/Repeat/ToggleButton、CheckBox/RadioButton、ToggleSwitch、Slider、NumericUpDown、ProgressBar、Expander、TreeView(+Item)、ToolTip、Popup/Flyout | **20–25** | 迁移应用"裸控件"可用；截图级一致性可验收；覆盖 AtomUI 消费端 80% 场景 | 需部分模板替换 → 与 v3 D3"不重写模板"冲突（需 ADR 小改）；Fluent 升级需复核 | **推荐** |
| C（仅文档承诺"只用 Arc 组件"） | 0 | 0 | 省 10–15 人日 | 消费端不可强制（见 §4.5）；迁移成本=全量重写；替代声明失真 | 不推荐 |

**为什么 B 的 ROI 高**：
- 成本仅占 v3 总量 3–4%（+10–15 人日 / 357），却把"可替代"从"口号"变成"可截图验收"；
- 20–25 人日 ≈ 1.2–1.7 人日/控件，与 D 报告的"单控件 0.3–1 人日（简单）/1–2 人日（窗口、Popup 类）"锚点一致（[D] §4.3.1）；
- 其中约 12 个控件**只需资源键覆盖**（颜色/圆角/字号/边框/Focus 环/滚动条厚度），只有 **6–8 个**需要模板替换（ComboBox/Menu/TabControl/ScrollBar/ListBoxItem/Window 装饰），因此 20–25 人日可行；若全模板重写 78 个则是 43–78 人日（D 报告 v1 口径），不在此列。

**与 v3 决策的冲突点（必须显式处理）**：v3 D3/§2.5 写"不重写模板"、R4 写"主题只做叠加"。B 方案需要把这两处修订为："**默认只做资源键覆盖；对 Top 15–20 中资源键无法表达的控件（ComboBox/Menu/TabControl/ScrollBar/Window 装饰）允许 Arc 版 ControlTheme 模板，且这些模板必须只引用 Arc token + 官方 Fluent 资源键**"。建议作为 ADR 记录，并保留 Fluent 作为 fallback。

### 4.3 最小可替代集（P0/P1/P2）

| 优先级 | 项 | 是否组件库职责（而非渲染层） | 人日 | v3 映射 | 说明 |
|---|---|---|---|---|---|
| **P0** | 内建控件主题 uplift（Top 15–20） | ✅ 组件库 | **20–25**（增量 +10–15） | 扩 G1 的"主题适配" | §4.2 选项 B；M1 交付 |
| **P0** | 图标扩展点 + 100 高频补齐 | ✅ 组件库 | **3–5** | 新增（L1 Icons） | `ArcIcon` 支持自定义 path/provider/别名；不追 843 |
| **P0** | 本地化最小集（en-US/zh-CN：分页/日历/时间/对话框/Toast/空态） | ✅ 组件库 | **5–8** | 从 C5（15）切片前移 M2 | 只做内建文案，不做 151 控件全量 |
| **P0** | Theme API：自定义 accent/品牌色 + token 覆盖入口 | ✅ 组件库 | **3–5** | 扩 `ArcThemeManager`/`ArcTheme` | `ArcThemeOptions`；与 8 accent 并存 |
| P0（已计划） | 4 服务 + Overlay host + ArcWindow | ✅ 组件库 | 24（C2） | G3b，M2 | 不要提前保证"M1 可替代" |
| P1 | Form + 校验基础设施 | ✅ 组件库 | 6–10 | 未列 | 迁移企业表单应用需要 |
| P1 | DataGrid 行/列虚拟化或官方 DataGrid feature 包 | ✅ 组件库（可选 feature） | 8–15 | C3 延伸 / R7 | 大数据场景；或文档标注限制 |
| P2 | 9 个缺失组合控件 | ✅ 组件库 | 18–30 | 未列 | 可用 Arc 现有组件组合替代，需示例 |
| P2 | 密度/圆角/字号主题算法（Compact） | ✅ 组件库 | 8–12 | 未列 | 对标 AtomUI Compact/Semi 尺寸 |
| P2 | 无障碍 peer（v3 已计划 15） | ✅ 组件库 | 15 | G3d/C4，M3 | 企业采购常问 |

### 4.4 人日与里程碑建议（相对 v3）

| 项 | v3 | 建议 | 差值 |
|---|---|---|---|
| M1（分层+可发布） | 71 | **81–86**（加主题 uplift +10–15） | +10–15 |
| M2（通用 UI + Agent MVP） | 225 | **236–243**（加图标 3–5、本地化切片 5–8、Theme API 3–5；不含 Form/DataGrid） | +11–18 |
| M3（全量） | 357 | **378–390**（P0 最小替代集） | +21–33 |
| 可选扩展 | — | **≈400–430**（加 Form 6–10 + 9 控件 18–30） | +45–73 |

> 注：以上人日为"中性"增量，沿用 v3 的 ±30% 不确定性；M1 加主题 uplift 后，M1 验收需增加"**裸控件迁移页**（20 个内建控件同屏，双主题截图）"作为硬门槛。

### 4.5 为什么"文档承诺只用 Arc 组件"不能替代主题覆盖

1. **消费端不可强制**：宿主窗口 chrome、`ScrollViewer/ScrollBar`、`ContextMenu`、`ToolTip`、第三方 Avalonia 控件内部、DataTemplate 内的裸控件都不在"只用 Arc 组件"的控制范围内——这些正是 10 人日覆盖不到的 70 类控件。
2. **迁移路径是渐进的**：AtomUI 用户不会一口气重写所有页面；最现实的迁移是"先换壳/导航/关键页，其余保留"，没有基础主题就会出现"半 Fluent 半 Arc"的视觉撕裂。
3. **"替代"是产品声明，不是编码规范**：AtomUI/Semi/Ursa/Material 的默认预期是"装上就有统一外观"；把一致性责任推给消费者，会让 Arc 在选型对比中直接输给"默认全主题化"的竞品。
4. **文档承诺可作为 v0.x 的诚实边界**：在 M1 发布 `0.9.0` 时，可以写"当前仅保证 Arc 控件外观一致；裸内建控件将在 M2 主题 uplift 后覆盖"，但**不能在对外宣称"替代 AtomUI"的同时保留该限制**。

### 4.6 对 v3 的具体修订建议（可执行清单）

| # | 修订点 | 具体改动 |
|---|---|---|
| 1 | G1 扩容 | `G1 18` → **28–33**（Foundation 5 + 主题 uplift 20–25 + ArcTheme 3）；M1 从 71 → 81–86 |
| 2 | 新增工作包 | M2 新增"图标扩展点 + 100 高频"3–5、"本地化最小集"5–8、"Theme API（自定义 accent/token 覆盖）"3–5 |
| 3 | 修订 D3/R4 | 把"不重写模板"改为"默认只做资源键覆盖；Top 15–20 中资源键不可达的控件允许 Arc ControlTheme 模板（只引用 Arc token + Fluent 资源键），并保留 Fluent fallback" |
| 4 | M1 验收加门槛 | 新增"裸控件迁移页"：20 个内建控件同屏 + 双主题截图，作为"M1 可发布"与"M2 可替代"的分界 |
| 5 | R3 门禁升级 | 解析 `project.assets.json` 传递依赖；白名单补 §5.2；黑名单补 §5.1；未知传递包 fail-closed |
| 6 | 对外表述门禁 | "替代 AtomUI"只允许在"M2 完成（含 C2 服务 + P0 主题/图标/本地化/Theme API）"后使用；M1 阶段写"Arc 组件库 + 渐进迁移" |

---

## 5. 依赖白名单/黑名单补漏

### 5.1 黑名单补充（v3 §2.2/D4 当前清单之外的必加项）

| 类别 | 包族/示例 | 为什么必须拉黑 | 级别 |
|---|---|---|---|
| AtomUI 全家桶 | `AtomUI.*`（`Controls`、`Controls.Shared`、`Desktop.Controls`、`Desktop.Controls.DataGrid/ColorPicker/Extras`、`Core`、`Native`、`Localization`、`Icons.*`、`Fonts.*`、`Generator`、`Toolkits.*`、`Toolchain`） | 直接竞品；LGPLv3 [W1]；且会拖入 `ReactiveUI.Avalonia`/`Svg.*`/`SkiaSharp.QrCode`/`System.Reactive` [W3][W4][W5] | 直接+传递 |
| 其他 Ant Design 组件库 | `AntDesign.Avalonia*`（`AntDesign.Style`/`ColorPicker`/`DataGrid`） | 另一套 Ant Design 主题/控件；且停留在 net5/6/7/netstandard2.0，**不适配 Avalonia 12** [W19] | 直接+传递 |
| 主题/控件库（v3 已列，补精确包名） | `Semi.Avalonia*`、`Irihi.Ursa*`、`Irihi.Avalonia.Shared*`、`FluentAvaloniaUI`、`SukiUI`、`Material.Avalonia*`、`Dock.Avalonia*`、`Tabalonia`、`AvaloniaEdit`/`Avalonia.AvaloniaEdit`、`Markdown.Avalonia*`、`ColorDocument.Avalonia`、`DialogHost.Avalonia` | v3 已列大部分；补上"`Irihi.Avalonia.Shared`（Semi/Ursa 依赖）"与"`DialogHost.Avalonia`（Material 生态对话框）" | 直接+传递 |
| 图标资产库（新增，防"缺图标就装库"） | `Material.Icons`、`Material.Icons.Avalonia`、`IconPacks.Avalonia*`、`Lucide.Avalonia`、`AntDesign.Avalonia` 图标相关、任意 `*Icons*`/`*IconPacks*` | Arc 的图标应是自有资产（Lucide path data，ISC [R5]）；引入图标库会瞬间破坏"设计系统自包含"，且与新图标扩展点方案冲突 | 直接+传递 |
| SVG/渲染扩展 | `Svg.Skia`、`Svg.Controls.Avalonia`、`Svg.Custom`、`Svg.Model`、`ShimSkiaSharp` | v3 已列 `Svg.Skia`；补齐 `Svg.Controls.Avalonia`/`Svg.Custom`/`Svg.Model`/`ShimSkiaSharp`（AtomUI 的实际传递依赖 [W4]） | 直接+传递 |
| 需评审（不自动拉黑） | `ReactiveUI.Avalonia`、`System.Reactive`、`CommunityToolkit.Mvvm` | 不是"视觉 UI 库"，但 `ReactiveUI.Avalonia` 是 AtomUI 的传递依赖 [W3]；建议"**Arc 核心禁止；消费应用可选**"，在 README 说明 | 仅直接（核心） |

### 5.2 白名单补充（官方包，CI 需显式放行，否则 ColorPicker/Desktop 一用就红）

v3 D4 的①只列了 `Avalonia`/`Skia`/`HarfBuzz`/`Themes.Fluent`/`Fonts.Inter`/`Desktop`，但实际依赖图会带出下列官方包：

| 包 | 来源 | 建议 |
|---|---|---|
| `Avalonia.Remote.Protocol` | `Avalonia.Controls.ColorPicker` 的**直接依赖** [D §1.2]；`Avalonia` 的直接依赖 [D §1.2] | 加入白名单（直接/传递均允许） |
| `Avalonia.BuildServices` | `Avalonia` 的直接依赖 [D §1.2] | 加入白名单（传递；`exclude=Build,Analyzers` 建议统一） |
| `Avalonia.Win32`/`Avalonia.X11`/`Avalonia.Native`/`Avalonia.Wayland` | `Avalonia.Desktop` 的平台后端 [D §1.2] | 加入白名单（**仅应用层**；`Wayland` 是 AtomUI 额外引入的，Arc 不主动引） |
| `Avalonia.Skia`/`SkiaSharp`/`SkiaSharp.NativeAssets.*`/`HarfBuzzSharp`/`HarfBuzzSharp.NativeAssets.*` | 渲染传递依赖 [D §1.2] | 加入白名单（传递） |
| `MicroCom.Runtime` | `Avalonia` 的传递依赖 [D §1.2] | 加入白名单（传递） |
| `Avalonia.Generators`/`Avalonia.Analyzers.*` | `Avalonia` 包的 analyzers 目录 [D §1.2] | 加入白名单（analyzer） |

### 5.3 R3 门禁升级：从"直接依赖白名单"到"传递依赖白名单 + 黑名单"

v3 R3 只校验"发布库的 `PackageReference` 是否落在 Avalonia 官方集合 + 包名黑名单"——**这会漏掉传递依赖**。建议：

1. **解析 `project.assets.json` 的完整 `libraries`（直接+传递）**，而不是只读 csproj；
2. **两段门禁**：
   - 直接依赖：必须在"官方白名单"精确集合内；
   - 传递依赖：命中"黑名单正则"即失败（如 `^AtomUI\.`、`^Semi\.Avalonia`、`^Irihi\.Ursa`、`^Material\.Icons`、`^IconPacks\.`、`^Lucide\.Avalonia`、`^Svg\.`、`^AntDesign\.`、`^FluentAvaloniaUI$`、`^SukiUI$`、`^Material\.Avalonia`、`^DialogHost\.Avalonia`、`^ShimSkiaSharp$`）；
   - **未在白名单也未在黑名单的传递包 → 失败并要求人工评审**（fail-closed），防止"未来某包悄悄拉入图标库"；
3. **白名单纳入 5.2 的官方传递闭包**（从 Avalonia 12.1.4 的 `project.assets.json` 生成一次基线，随 Avalonia 版本更新）；
4. **ColorPicker 间接依赖结论**：`Avalonia.Controls.ColorPicker` 的直接依赖是 `Avalonia` + `Avalonia.Remote.Protocol`，二者都是 Avalonia 官方；因此**间接依赖在白名单补入 `Avalonia.Remote.Protocol` 后是足够的**，不需要为 ColorPicker 单独开例外。DataGrid 同理（直接依赖仅 `Avalonia`）。

### 5.4 一个容易忽略的"反向依赖"：控制包依赖平台包

AtomUI 的 `AtomUI.Desktop.Controls` **直接依赖 `Avalonia.Desktop`/`Avalonia.Wayland`/`Avalonia.X11`**（控制库拖平台包）[W3]；Arc 的库项目只依赖 `Avalonia`，`Desktop` 只在 Gallery [D][R1]。建议把这条写成显式规则并进 CI：**发布库禁止引用 `Avalonia.Desktop`/平台后端**（当前已满足，防回归）。

---

## 6. 证据索引

### 6.1 外部证据 `[W*]`（联网核实，2026-11-05）

| ID | URL | 证明内容 |
|---|---|---|
| W1 | https://github.com/AtomUI/AtomUI（README.nuget.md：https://raw.githubusercontent.com/atomui/atomui/master/README.nuget.md） | AtomUI 6.x = Ant Design 6 组件系统；`.NET 10` + `Avalonia 12.1.x`；包划分（Desktop.Controls/DataGrid/ColorPicker/Extras/Icons.AntDesign）；**LGPLv3** |
| W2 | https://raw.githubusercontent.com/AtomUI/AtomUI/128994e3958e84f1a832a327641eadc9d4d5b7c2/docs/architecture/overview.md | 源码包架构（Native/Localization/Core/Controls.Shared/Controls/Desktop/DataGrid/ColorPicker/Extras/Generator/Fonts/Icons）；启动注册（`WithAtomUIDefaultOptions` + `UseAtomUI`）；主题/本地化链路 |
| W3 | https://nuget.azure.cn/v3-flatcontainer/atomui.desktop.controls/6.2.12/atomui.desktop.controls.nuspec | `AtomUI.Desktop.Controls` 依赖 `AtomUI.Controls`/`AtomUI.Native`/`Avalonia.Desktop 12.1.4`/`Avalonia.Wayland`/`Avalonia.X11`/`ReactiveUI.Avalonia 12.0.3`；contentFiles 含 en-US.xlf（Calendar/DatePicker/Dialog/Pagination/…） |
| W4 | https://nuget.azure.cn/v3-flatcontainer/atomui.controls/6.2.12/atomui.controls.nuspec | `AtomUI.Controls` 依赖 `AtomUI.Core`/`AtomUI.Fonts.AlibabaSans`/`AtomUI.Icons.AntDesign`/`Avalonia 12.1.4`/`SkiaSharp.QrCode 0.12.0`/`Svg.Controls.Avalonia 12.0.0.15`/`Svg.Custom`/`Svg.Model` |
| W5 | https://nuget.azure.cn/v3-flatcontainer/atomui.core/6.2.12/atomui.core.nuspec | `AtomUI.Core` 依赖 `AtomUI.Localization`/`Avalonia`/`Avalonia.Native/Win32/X11`/`System.Reactive 6.1.0` |
| W6 | https://raw.githubusercontent.com/AtomUI/AtomUI/4513cfd2898c66d90e2d137f8ac8fc2d171f5598/docs/architecture/systems/theming/runtime.md | 主题系统：ThemeConfig/Token 四层/算法 Default-Dark-Compact/局部 ThemeConfigProvider/事务切换/XML 主题/用户主题目录；`IThemeManager` 与静态 Dialog/Notification 的根主题语义 |
| W7 | https://raw.githubusercontent.com/AtomUI/AtomUI/4513cfd2898c66d90e2d137f8ac8fc2d171f5598/docs/architecture/systems/localization/language-packs.md | **内置 en-US / zh-CN / zh-TW**；官方附加语言包命名 `AtomUI.I18n.{Tag}`、模块包 `AtomUI.*.I18n.{Tag}`；`UseLanguages(...)` |
| W8 | https://api.github.com/repos/AtomUI/AtomUI/contents/src/AtomUI.Desktop.Controls?ref=main | 81 个目录：AdornerLayer/Alert/AutoComplete/Avatar/Badge/BorderBeam/Breadcrumb/Buttons/ButtonSpinner/Calendar/Card/Carousel/Cascader/CheckBox/Collapse/ComboBox/DatePicker/DateViewer/Descriptions/Dialog/Drawer/DropdownButton/Empty/Expander/FloatButton/Flyouts/Form/GroupBox/HeaderedContentControl/ImagePreviewer/Input/ListBox/ListView/MarqueeLabel/Masonry/Mentions/Menu/**Message/MessageBox/NavMenu/Notifications**/NumericUpDown/OptionButtonGroup/OtpLineEdit/Pagination/Popup/PopupConfirm/Primitives/ProgressBar/Properties/QRCode/RadioButton/Rate/Result/ScrollViewer/Segmented/Select/Separator/Skeleton/Slider/Space/Spin/SplitButton/Splitter/SplitView/Statistic/Steps/Switch/TabControl/Tag/TextBlock/Timeline/TimePicker/Tooltip/Tour/Transfer/TreeSelect/TreeView/Upload/**Window/WindowTitleBar** |
| W9 | https://raw.githubusercontent.com/AtomUI/AtomUI/main/src/AtomUI.Icons.AntDesign/AntDesignIconCatalog.g.cs | 生成目录声明 **`IconCount = 843`**；`AntDesignIconInfo(Name, ThemeType, Kind, IconType, Creator)`；按 64 分块 |
| W10 | https://api.github.com/repos/AtomUI/AtomUI/contents/src/AtomUI.Icons.AntDesign?ref=main | 生成文件（`AntDesignIconCatalog.g.cs` 136,955 B、`AntDesignIconProvider.Factory.g.cs` 146,059 B）+ `GeneratedIcons`；图标为强类型 `Icon` 控件（Outlined/Filled/TwoTone） |
| W11 | https://raw.githubusercontent.com/irihitech/Semi.Avalonia/main/README.md | Semi 主主题包 + 6 个附加**主题**包（ColorPicker/DataGrid/TreeDataGrid/Dock/Tabalonia/AvaloniaEdit）；`SemiTheme Locale="zh-CN"`；版本兼容表 |
| W12 | 本地：`D:\.nuget\packages\semi.avalonia\12.1.0.1\semi.avalonia.nuspec` | Semi 12.1.0.1 依赖 `Avalonia 12.1.0` + `Irihi.Avalonia.Shared 0.5.0`；`Semi.Avalonia.dll` 1696 KB |
| W13 | https://raw.githubusercontent.com/irihitech/Ursa.Avalonia/main/README.md + https://api.github.com/repos/irihitech/Ursa.Avalonia/contents/src?ref=main | `Irihi.Ursa` 控件 + `Irihi.Ursa.Themes.Semi` 主题包 + Prism/ReactiveUI 扩展；Ursa 提供 Dialog 相关服务（Prism 扩展） |
| W14 | https://nuget.azure.cn/v3-flatcontainer/irihi.ursa/2.3.0/irihi.ursa.nuspec | Irihi.Ursa 2.3.0 依赖 `Avalonia 12.0.2`（net10/net8）+ `Irihi.Avalonia.Shared(.Contracts) 0.5.0` |
| W15 | https://api.github.com/repos/irihitech/Ursa.Avalonia/contents/src/Ursa/Controls?ref=main | Ursa 控件目录含 Dialog/MessageBox/Notification/NotificationShared/Toast/TitleBar/ThemeSelector/ThemeVariantMapper/Form/QRCode/PathPicker/PinCode/TagInput/ImageViewer 等 |
| W16 | https://raw.githubusercontent.com/AvaloniaCommunity/Material.Avalonia/ea3ce1282dc22431e965d104c7049a67abae8e00/README.md | Material 主题（"almost all Avalonia controls"）、附加控件（Snackbar/侧板/FAB/卡片/对话框）、`Material.Avalonia.DataGrid`/`Dialogs` 包、图标需单装 `Material.Icons` |
| W17 | https://nuget.azure.cn/v3-flatcontainer/material.avalonia/3.21.0/material.avalonia.nuspec | Material.Avalonia 3.21.0 依赖 `Avalonia 12.1.1`（net8） |
| W18 | https://nuget.azure.cn/v3-flatcontainer/material.icons.avalonia/index.json | `Material.Icons.Avalonia` 3.x 支持 Avalonia 12（`3.0.0-avalonia12` 起） |
| W19 | https://nuget.azure.cn/v3-flatcontainer/antdesign.avalonia/11.0.0/antdesign.avalonia.nuspec | `AntDesign.Avalonia` 11.0.0：MIT，`AntDesign.Style/ColorPicker/DataGrid`，目标 net5/net6/net7/netstandard2.0（**不适配 Avalonia 12**） |
| W20 | https://nuget.azure.cn/v3-flatcontainer/iconpacks.avalonia/index.json；https://nuget.azure.cn/v3-flatcontainer/lucide.avalonia/index.json | 图标库族确实存在（IconPacks.Avalonia 2.0.0；Lucide.Avalonia 0.2.29），应列入黑名单 |
| W21 | https://nuget.azure.cn/v3-flatcontainer/atomui.icons.antdesign/index.json；`atomui.desktop.controls/index.json` | AtomUI 最新版本 6.2.12（本报告采用） |

### 6.2 仓库证据 `[R*]`（`D:\Code\VibeCoding\Arc.Avalonia\`）

| ID | 位置 | 证明内容 |
|---|---|---|
| R1 | `docs/13-framework-layering-and-agent-top.zh.md` §2.5、§2.2、D3/D4、§0.3、§7 | v3 口径：不重写模板、10 人日定向覆盖 6 类表面、官方白名单（Fluent/Fonts/DataGrid/ColorPicker/Desktop）、151 控件/137 ControlTheme/161 图标、C2 24 人日（M2）、C5 15 人日（M3）、明确不做自建主题 |
| R2 | 同上 §2.2（行 212–218）/D4 | 黑名单当前清单：AtomUI、Semi、Ursa、FluentAvalonia、SukiUI、Material、Dock、Tabalonia、AvaloniaEdit、Markdown.Avalonia、Svg.Skia、ColorDocument.Avalonia（**缺图标库与 AtomUI 传递族**） |
| R3 | 同上 §4.2（WBS 行 464–475） | G1=18（Foundation 5 + 主题适配 10 + ArcTheme 3）；G3b=24；G3d=30；G3f=20；总 357 |
| R4 | 同上 §0.3（行 57–71）、L1 行 196 | 151 控件 / 137 ControlTheme / 328 页面 / 298 Docs JSON / 161 图标 / 294 条目审计 |
| R5 | `src/Foundation/Arc.Avalonia/Icons/ArcIconData.cs:7-11`（`public static readonly IReadOnlyDictionary<string,string> Paths`）；`src/Foundation/Arc.Avalonia/Controls/ArcIcon.cs:69-77` | 161 个 Lucide path（实测 161 条）；`Paths` 只读、无 `Register`/provider；`ArcIcon` 只按名查表，未命中即空 |
| R6 | `src/Foundation/Arc.Avalonia/Theme/ArcThemeManager.cs:15-46`；`Theme/ArcPalette.cs:9-25` | `ArcThemeManager.Apply/SetScheme/SetAccent/ToggleScheme` + `ReducedMotion`；`ArcAccent` 固定 8 值；无自定义 color/token 覆盖 API |
| R7 | 全仓库 grep（`*.cs`）：`DataValidationErrors` / `INotifyDataErrorInfo` / `FormField` / `IDialogService` / `INotificationService` / `INavigationService` / `ThemeSelector` / `WindowNotificationManager` 均 **0**；`.resx` 0 | 无服务层/表单校验/主题选择器/本地化资源 |
| R8 | 全仓库 grep：`class Arc(Form / Transfer / Cascader / TreeSelect / Descriptions / Statistic / QRCode / Tour / FloatButton / Splitter / Window / TitleBar)\b` **0 命中** | 9 组 AtomUI 控件无 Arc 对应类（部分可由现有组件组合：`ArcResizablePanels`≈Splitter、`ArcEmptyState`≈Empty/Result、`ArcToast`≈Message） |
| R9 | `src/Foundation/Arc.Avalonia/Theme/ArcTokens.cs`（82 个 `public const string`）；同目录 `ArcResources`（`ArcThemeManager.cs:49-70`） | 82 个 public token key；`ArcResources` 提供 Brush/Color/Double/Radius/Shadows/Duration 类型化读取；无写入/覆盖 API |
| R10 | `docs/13...` §0.3 + 既有 `docs/12-gallery-doc-m3-m4.zh.md` | 294 条目文档/328 页面/audit 与截图验收基线（Arc 的强项） |
| R11 | `src/Apps/Arc.Avalonia.Gallery/App.axaml:7`；`src/Foundation/Arc.Avalonia/Theme/ArcTheme.axaml` | 当前 `FluentTheme` + 25 个 `StyleInclude`；Arc 主题只做 Window/TextBlock 样式（无内建 ControlTheme） |

### 6.3 上一份 D 报告 `[D]`

- `docs/analysis/13-d-ecosystem-benchmark.zh.md`：§1.2（本地包实测：Fluent/Simple/Semi/Suki/FluentAvalonia/Inter/Desktop/ColorPicker/DataGrid）、§1.5（附加控件自带 Fluent+Simple 两套主题）、§1.7（包拆分惯例）、§4（三档边界与替代成本）、附录 A（Fluent 80 个主题 XAML 清单）。
- 本报告用到的关键锚点：ColorPicker/DataGrid 的 nuspec 依赖与 DLL 资源、Fluent 80 个 ControlTheme、`Avalonia.Remote.Protocol`/`BuildServices`/平台包属于 Avalonia 官方传递依赖。

### 6.4 未验证 / 存疑清单（C 级）

1. Semi/Ursa/Material 的**图标数量**（Semi 未宣传图标包；Ursa `Controls/Icons` 规模未清点；Material 依赖 `Material.Icons`，其图标数未核实）。
2. Semi/Ursa/Material 的**本地化**细节（Semi 仅 `Locale` 参数实证；Ursa/Material 未在文档中确认多语言）。
3. AtomUI 的 `~76 组控件`为目录数（81 个目录含 Buttons/Flyouts/Primitives/Properties 等基类/聚合目录），**精确控件类数未逐个数**。
4. Arc 的 `151 控件`沿用 v3 的 Lead 复算口径，本轮未重算（本地粗查 public Arc 控件类 109，因基类/目录过滤不同，不作反证）。
5. AtomUI 许可证的商用边界按 README 文字理解（LGPLv3 + 商业授权选项），具体法务解释未做。
6. 20–25 人日的主题 uplift 为估算（沿用 D 报告"单控件 0.3–1 / 窗口类 1–2 人日"锚点），未做原型验证。

---

## 附录 A：AtomUI `Desktop.Controls` 的 81 个目录（[W8] 原文，按字母序）

AdornerLayer、Alert、AutoComplete、Avatar、Badge、BorderBeam、Breadcrumb、Buttons、ButtonSpinner、Calendar、Card、Carousel、Cascader、CheckBox、Collapse、ComboBox、DatePicker、DateViewer、Descriptions、Dialog、Drawer、DropdownButton、Empty、Expander、FloatButton、Flyouts、Form、GroupBox、HeaderedContentControl、ImagePreviewer、Input、ListBox、ListView、MarqueeLabel、Masonry、Mentions、Menu、Message、MessageBox、NavMenu、Notifications、NumericUpDown、OptionButtonGroup、OtpLineEdit、Pagination、Popup、PopupConfirm、Primitives、ProgressBar、Properties、QRCode、RadioButton、Rate、Result、ScrollViewer、Segmented、Select、Separator、Skeleton、Slider、Space、Spin、SplitButton、Splitter、SplitView、Statistic、Steps、Switch、TabControl、Tag、TextBlock、Timeline、TimePicker、Tooltip、Tour、Transfer、TreeSelect、TreeView、Upload、Window、WindowTitleBar。

> 说明：`Flyouts`/`Primitives`/`Properties`/`Buttons` 为聚合/基类目录；去掉这 4 类后约 76 个"控件/样式组"。

## 附录 B：AtomUI 本地化与主题系统（对 Arc 的可借鉴/不可借鉴点）

| AtomUI 能力 | 事实 | 对 Arc 的取舍 |
|---|---|---|
| 内置 en-US/zh-CN/zh-TW | 随拥有 Catalog 的组件程序集发布，非运行时插件 [W7] | **借鉴**：Arc 不需要 3 语言，但至少要 en-US + zh-CN 内建文案 |
| 语言包 = 静态 XLIFF + buildTransitive props + 编译期校验 | 复杂但可维护 [W7] | **不照抄**：Arc 用 `.resx` + `ILocalizer` 即可（v3 C5 方向正确） |
| ThemeConfig / Seed-Map-Alias-Control 四层 token / 算法 / 局部作用域 / 事务切换 | 重运行时主题引擎 [W6] | **不照抄**：Arc 的 82 token + palette 字典 + 8 accent 已满足设计系统；只需补"自定义 accent + token 覆盖 API" |
| 强类型 token 资源扩展 `{atom:XxxTokenResource}` | 编译期校验、零反射 [W6] | **选择性借鉴**：Arc 的 `ArcResources` 已是类型化读取；可加 token key 文档/校验，不必做扩展 |
| 用户主题目录 / XML 主题定义 | 允许终端用户换主题 [W6] | **不借鉴**：非 Arc 目标；会引入安全/兼容负担 |
| 窗口原生能力 `AtomUI.Native` | 自绘/原生 chrome [W2][W3] | **借鉴**：Arc 的 C2 里 ArcWindow 应覆盖窗口装饰 |
