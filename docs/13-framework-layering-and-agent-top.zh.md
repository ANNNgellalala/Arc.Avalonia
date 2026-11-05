# Arc.Avalonia 框架化：分层架构设计与工作量评估（v3.2）

> **目标（用户原始需求）**：① 把 AI Agent 组件提升为**顶层组合 UI 组件**；② 把基础模块拆出来作为**最底层**；
> ③ 中间层**对标通用 UI 框架**，提供一般 UI 开发所需的其余组件；④ 最终形成**无其他 UI 依赖**的框架。
>
> **口径演变（v2 → v3 → v3.1）**：
> * **v2**（2026-10-31）："无其他 UI 依赖"= 不依赖 AtomUI/Semi/Ursa 等第三方 UI 组件库；Avalonia 官方 UI（Fluent/Fonts/附加控件/Desktop）允许。
> * **v3**（2026-11-05）：Arc 定位 = **UI 组件库（对标 AtomUI/Semi/Ursa），不是 Avalonia 替代品**；渲染层（帧调度/绘制缓存/主题失效协议/布局/文本整形/虚拟化机制）全部归 Avalonia，Arc 不做。
> * **v3.1**（2026-11-05，四份对抗式评审后）：**修订估算与三处架构缺陷**——
>   ① 若要"替代 AtomUI"，必须把内建控件主题覆盖从"10 人日定向"提升到"Top 15–20 个控件"（+20 人日），并补图标扩展点与 Theme API；
>   ② 主题聚合包从 L1 改为 **L2.5 聚合层**（修正依赖方向倒置）；
>   ③ `Arc.Avalonia.Internal` 解散、614 个文档/页面文件出库、Agent 多会话 Workspace API 补入 WBS。
>   总量 **357 → 442 人日（288–594）**。评审明细见 §10 与 [`analysis/13-review-*.zh.md`](analysis/)。
> * **v3.2.1**（2026-11-05，M0 POC 结论）：`<arc:ArcTheme/>` 单行入口**可行**（51 断言通过，无需 fallback）；Top 内建控件皮肤 **47 键覆盖、46/47 纯键、0 模板替换**（仅字号需局部 ControlTheme、ScrollBar 自动收缩需样式）。
>   据此 M2 主题覆盖 15 → **18 人日**，总量 **442 → 445（291–597）**；M1 不变（90）。证据见 [`docs/adr/0003`](adr/0003-theme-strategy.md)/[`0004`](adr/0004-top-control-keys.md)。
>
> * **v3.2**（2026-11-05，四项决策确认）：**Arc 与 AtomUI/Semi/Ursa 处于同一生态位，但不承诺"完全 drop-in 替代"**；
>   Agent 采用 C 报告 **M 档（124 人日）**；网格采用"**自有网格（小数据）+ 官方 DataGrid Feature（大数据）**"分工；
>   Top 15–20 内建控件皮肤清单**认可**。因此：删除"drop-in 替代"表述与网格重写可选项，WBS 维持 **442（288–594）**，
>   但验收口径从"可替代 AtomUI"改为"**生态位对齐 + 设计一致性达标**"。决策记录见 §10。
>
> **方法**：4 份并行静态审计（A/B/C/D）+ 4 份对抗式评审（R1–R4）+ Lead 复算与实测（构建/verify/audit/smoke）。
> 本文是评估与设计，不含实现；数字均可在仓库内复算（附录 A）。

---

## 0. 摘要

### 0.0 口径演变与版本差异

| 项 | v1（过严） | v2 | v3 | **v3.1 → v3.2（本版）** |
|---|---|---|---|---|
| 依赖边界 | 连 Fluent 也要替换 | 官方允许、第三方禁止 | 同 v2 + 渲染层不做 | 同 v3；**门禁升级为传递依赖检查** |
| 主题 | 自建 43–78 个内建主题（48 人日） | 取消自建，10 人日适配 | 同 v2 | **Top 15–20 内建控件覆盖，33 人日**（POC 后 30→33） |
| 主题包位置 | L2 独立层 | L1 独立包 | L1 独立包 | **L2.5 聚合层**（依赖 L1+L2，修正倒置） |
| 中间层 | 以帧调度/绘制缓存为支柱 | 以通用 UI 能力为主体 | 渲染层不做 | 同 v3 + **契约/服务/网格方案按评审重估** |
| 层数 | 6 层 | 5 层 | 5 层 | 5 层 + 1 个聚合例外 |
| 总量 | ≈435（290–580） | ≈357（280–470） | ≈357（280–470） | **≈445（291–597）** |
| **M0 结论**（2026-11-05） | — | — | — | POC-A 单行入口可行；POC-B 47 键 / 46 纯键 / 0 模板替换 → 主题 +3 |
| 定位表述 | — | 第三方 UI 库替代 | UI 组件库（对标 AtomUI） | **同一生态位、不承诺完全 drop-in 替代**；主题覆盖 = 设计一致性，不是替代承诺 |
| 关键增量 | — | 官方 UI 允许 | 渲染层不做 | 主题覆盖 +20、Internal 解散 +5、614 文件出库 +5、契约 +9、网格方案 +5、Agent Workspace/压测 +20、图标/Theme API +8、门禁 +4，去重 −10 |

### 0.1 一句话结论

当前仓库**已不依赖任何第三方 UI 组件库**（26 个库项目只引 Avalonia 官方包），目标 ④ 只需守门；Arc 与 AtomUI/Semi/Ursa **同一生态位**（设计系统 + 组件库 + 组合），差异化在 Agent 顶层与 294 条目文档，**不以"完全替代"为承诺**；
真正的工作量在 **目标 ③ 的通用 UI 能力（≈190 人日）**、**"替代 AtomUI"所需的内建控件皮肤与图标/Theme API（≈34 人日）**
与 **目标 ① 的 Agent 顶层（54–155 人日）**。经评审与 M0 POC 修正后总工程量 **≈445 人日（291–597）**，3–4 人约 **6.5–8 个月**。

### 0.2 四项需求的可行性判断（v3.1）

| # | 需求 | 现状 | 判断 | 主要代价 |
|---|---|---|---|---|
| ① | Agent 组件 → 顶层组合层 | AgentUI 6 控件只有叶子视图；缺多会话 Workspace、流式、审批队列、视图注册 | **可行且最高价值** | 54（MVP）–124（完整）人日 |
| ② | 基础模块 → 最底层 | Foundation 18 文件 / 2,294 行，但混入 `ArcVirtualList`/`ArcUi`，`ArcResources` 静态耦合，`Internal` 被 12 个 csproj 引用 | **可行，代价低** | 10 人日（纯度 + Internal 解散） |
| ③ | 中间层 → 对标通用 UI 框架 | 151 控件/913 DP/131 ControlTheme，但 0 窗口壳/0 服务层/0 数据驱动列表/0 AutomationPeer/0 本地化 | **可行，是工作量主体** | ≈190 人日（契约/服务/列表/无障碍/本地化/测试/图标/Theme API） |
| ④ | 无其他 UI 依赖 | **已满足**：无第三方 UI 库；官方 Fluent/Fonts 属允许范围 | **只需门禁 + 主题适配** | 门禁 1 人日；主题适配归 ③ |

> 评审新增结论（R4）：**按 v3 原范围只能成为"可发布的组件库"，不足以"替代 AtomUI"**——差在内建控件皮肤、
> 图标数量（161 vs Ant Design 843）、本地化、服务层/窗口壳。v3.1 已把前两项与 Theme API 纳入 M2。

### 0.3 关键数字（基线，工作树 2026-11-05；口径已按评审修正）

| 维度 | 数值 | 口径 |
|---|---|---|
| 解决方案项目 | 29（28 源 + 1 测试控制台） | `ArcUI.slnx` |
| 源码 | **764 个 `.cs`/`.axaml`，181,358 行** | src+tests，不含 `.recon`/bin/obj；物理行 |
| 控件实现范式 | 76 `TemplatedControl` + 14 `ContentControl` + 83 裸 `Control`（多数自绘） | 类声明统计 |
| Components 公开控件 | **151** 个（106 有自有 ControlTheme，20 个自绘图表 0 主题，16 个内部部件误 public） | B 报告 |
| ControlTheme | **112 个 `x:Key` 定义 / 137 个 `<ControlTheme>` 元素**（含内建控件 keyed 主题） | 两种口径分别标注 |
| 页面/文档 | **328 个 `Pages/*.cs`**（294 个条目页 + 21 个文档/场景页类 + 辅助类）＋ **294 份 `Docs/*.json`** | 均编译进库工程；库内迁移面 = 320 页 + 294 文档 = **614 文件** |
| 库→Demo 反向依赖 | **25/29** 个项目 `ProjectReference` `Arc.Avalonia.Demo` | Lead 复算 |
| 第三方 UI 库依赖 | **0**（仅 Avalonia 官方包） | A/D |
| 包元数据 | `PackageId`/`Version`/`License`/`README`/`LICENSE`/`samples/`/`.github` 全仓库 **0 处** | B 报告 |
| 构建基线 | 全量构建 → **0 error / 9 warning**（既有 analyzer 提示） | Lead 实测 |
| 验证基线 | `VERIFY: checks=34 failures=0`；`CATALOG AUDIT: items=294 docs=294 pages=294 errors=0`；`GALLERY SMOKE: itemShows=294 pageShows=315 failures=0` | Lead 实测 |

### 0.4 工作量结论（v3.1）

**净增量人日**（1 名熟悉 Avalonia 12 的工程师；含自测；不含产品设计与新增业务组件）。

| 里程碑 | 范围 | 人日（中性，区间） | 3–4 人日历 |
|---|---|---|---|
| **M1 分层 + 可发布（α）** | 基础层纯度、Internal 解散、Fluent 主题基础适配、`ArcTheme` 聚合入口、614 文件出库、包元数据/samples/许可/门禁 | **≈90（62–118）** | 6–8 周 |
| **M2 通用 UI 能力 + 内建控件皮肤 + Agent MVP（β，与 AtomUI 同生态位）** | 契约/API、服务层+窗口壳、数据驱动列表、Top 15–20 内建控件覆盖、图标扩展、Theme API、本地化最小集、Agent Workspace/投影/组合根、迁移收尾 | **≈301（228–387）**（累计） | 18–25 周（累计） |
| **M3 全量成熟（1.0）** | 无障碍、本地化全量、DnD/MVVM/容器、测试基线、Agent 完整顶层 | **≈445（291–597）**（累计） | 27–36 周（累计，约 6.5–8 个月） |

**分档结论**：
* 只做"分层 + 可发布"（组件库形态，不承诺 drop-in）：**≈90 人日**（3 人 6–8 周）。
* 做到"**能日常写业务应用 + 与 AtomUI 同生态位**"：**≈301 人日**（3 人约 25 周 / 4 人约 18 周）。
* 做到"通用框架能力完整 + Agent 完整顶层"：**≈445 人日**（3–4 人约 6.5–8 个月）。
* 若砍掉 Top 皮肤 / 图标扩展 / Theme API（不追求与 AtomUI 同级的设计一致性）：**≈419 人日**；本版**不采用**该裁剪。
* 估算不确定性 **±30%**；评审已把 4 份审计的乐观项（主题 10、虚拟化 20、C1 44、G4 104）全部上调。

### 0.5 推荐架构（v3.1）

```text
L5  宿主应用（模型/工具执行/MCP/语音/调度/持久化/托盘）          ← 不在库内
L4  Agent 顶层组合层   Arc.Avalonia.Agent（Workspace + 投影运行时 + 组合根 + 视图注册）
L3  组合/模式层        Patterns（Special 55 + Blocks 111）        ← 可选，示例/场景
L2  通用 UI 组件层     Arc.Avalonia.Controls.*（151 控件 + 服务/容器/窗口壳/数据驱动列表）
L2.5 主题聚合层（例外）Arc.Avalonia.Theme（Fluent + token + 11 组件主题 + Top 内建覆盖）
L1  基础层             Arc.Avalonia.Foundation（token/主题引擎/动效/图标/原子控件）
L0  官方平台           Avalonia 12：渲染/布局/虚拟化机制/主题系统/平台窗口（Arc 不重做）
```

一句话：**"基础模块"= L1，"通用 UI 框架"= L2，"Agent 顶层"= L4；"无第三方 UI 依赖"= L0 白名单（已满足，靠 CI 守住）；
"主题聚合"是唯一的跨层例外（L2.5），只做官方主题与组件主题的组装，不做平台渲染。**

### 0.6 明确不做（详见 §7）

自研渲染后端/文本整形/IME/布局/动画时钟/合成器；**绘制缓存与主题版本失效协议；自建虚拟化机制**；
自建 78 个内建控件的全量主题（只做 Top 15–20）；Agent 执行运行时；重写 111 个 Blocks；
在 v1 就追求 151 控件无障碍全覆盖。

---

## 1. 现状基线：为什么"再加组件"到不了目标

### 1.1 当前依赖链与两个结构问题

```text
Avalonia 官方（内核 + Fluent 主题 + Inter 字体）                    ← 允许范围，已满足目标 ④
  └─ Arc.Avalonia（Foundation）
       └─ Arc.Avalonia.Demo（页面契约 + 294 项目录 + DemoUi）      ← 应用级工程
            ├─ Components ×11 / Agent ×2 / Special ×5              ← 库反向依赖 Demo
            │    └─ Blocks ×6（组合层，方向正确）
            └─ Gallery（应用壳）
```

两个结构问题（与"是否自建主题"无关，v3.1 中依然存在）：

1. **库 → 应用的方向错误**：可发布的 UI 库不该依赖"页面契约 + 目录数据 + 演示布局助手"。
   后果：`dotnet pack` 拖进 WinExe、140 个页面类 + 128 份文档 JSON 进 DLL、演示数据成为公开 API 默认值（`ArcUserMenu` 默认 `"Maya Chen"`）。
2. **文档/页面必须寄居在库程序集里**：反射发现机制（`ArcPageRegistry`/`ArcDocStore` 扫 `Arc.Avalonia*.dll`）**已经支持**跨程序集，
   但页面与文档从未搬出。库内迁移面是 **320 页 + 294 份文档 = 614 文件**（评审修正：先前只算了 Components 的 268）。

> **"无其他 UI 依赖"在 v3.1 下不是问题**：26 个库项目只引 Avalonia 官方包；`Avalonia.Themes.Fluent` 只在 Gallery 出现。
> 目标 ④ 的剩余工作只有两件：① 一行 `ArcTheme` 聚合入口；② CI 白名单（含传递依赖），防止未来误引入第三方 UI 库。

### 1.2 框架级缺口（v3.1）

| # | 缺口 | 证据（量化） | 目标 |
|---|---|---|---|
| 1 | **内建控件皮肤覆盖不足**（评审新增） | 现有 25 个内建 keyed 主题只覆盖 6 类；裸 `ComboBox`/`Menu`/`TabControl`/`CheckBox`/`Slider`/`ProgressBar`/`Expander`/`TreeView` 仍是 Fluent 外观 | ③④ |
| 2 | 没有窗口壳 | 0 个 `ArcWindow`/标题栏；Gallery 用系统 chrome | ③ |
| 3 | 没有服务层 | 0 个 `IDialogService/INotificationService/INavigationService/IFilePickerService`；`ArcDialog` "fills nearest panel" | ③ |
| 4 | 没有数据驱动列表 | `ItemsControl`/`DataTemplate` 使用 0 处；18 处手写 `Children.Clear()`；DataGrid/TreeTable 自述无虚拟化 | ③（也是 ① 的性能前提） |
| 5 | 没有无障碍语义 | 312 处 `SetName`，但 **0** 个 `AutomationPeer`/`ControlType`/`AutomationId` | ③ |
| 6 | 没有本地化/RTL | 116 个英文 DP 默认值、452 处英文串、0 个 `.resx`、0 处 RTL 专项 | ③ |
| 7 | 契约未框架化 | 143 个 CLR event、**0** 个 `RoutedEvent`；200 处 `OnApplyTemplate`、**0** 个 `[TemplatePart]` | ③ |
| 8 | 没有 Agent 主干 | 会话模型/多会话 Workspace/增量流式/审批队列/视图注册均为 0 | ① |
| 9 | 图标与 Theme API 不足（评审新增） | 161 个 Lucide 图标、`ArcIconData.Paths` 只读无注册；固定 8 accent、无自定义 accent/token 覆盖 API | ③④ |
| 10 | 没有发布工程 | 包元数据/版本/许可证/README/samples/CI 全缺 | ④ |

另有四个基础层具体缺陷（G1 一并修）：`ArcPalette` 声明 82 token 但只注册 81（`ControlThumbShadow` 漏注册）；
`ArcResources` 5 处 `Application.Current` 静态兜底；`ArcVirtualList`/`ArcUi` 混在 Foundation；测试仅 23 条非 UI 断言。

### 1.3 一个反直觉但重要的判断：现状离目标是"近"的

- **48% 的控件是自绘**（112 个 `Control` 链、79 个直接 `Render`）：不依赖主题系统。
- **已有 112 个 `x:Key` ControlTheme** 覆盖 112 个 Arc 类型；`ArcTheme.axaml` 只做 `Window`/`TextBlock` 全局样式——
  说明 Arc 本来就在"官方主题之上叠加 token"，v3.1 只是把叠加范围扩到 Top 15–20 个内建控件。
- **页面/文档出库是纯机械动作**：反射发现已就绪，614 个文件移动 + csproj 调整 + **0 处必需命名空间改动**。
- **验证护栏已经存在**：`--audit`（294/294、0 错误）/ `--smoke`（315 页）/ 截图 harness（本机实测见 §0.3）。

**所以工作量集中在三处**：L2 的通用 UI 能力（≈190）、"替代 AtomUI"的皮肤/图标/Theme API（≈34）、L4 Agent（124）。

---

## 2. 目标架构

### 2.0 职责边界：Arc 替代的是组件库，不是 Avalonia

> 判定规则：**AtomUI/Semi/Ursa 会提供的、应用开发者直接使用的能力，Arc 做；Avalonia 提供给所有 UI 库的基础设施，Arc 不做。**

| 能力 | 归属 | Arc 是否做 | 说明 |
|---|---|---|---|
| 渲染后端（Skia）、文本整形、输入栈、布局引擎、动画时钟/合成器 | Avalonia 本体 | ❌ | 重做等于重做 Avalonia |
| 内建控件**模板机制**与 Fluent 主题（80 个 ControlTheme） | Avalonia 官方 | ❌ | 但**要在其上做 Top 15–20 的皮肤覆盖**（组件库职责） |
| 内建控件**皮肤覆盖**（Window/TextBox/ComboBox/ScrollBar/Button/CheckBox/…） | Arc L2.5 | ✅ | 这是"同生态位"的核心，33 人日（§D3） |
| 列表/树虚拟化**机制**（`VirtualizingStackPanel`/容器回收） | Avalonia 本体 | ❌ | Arc 只把控件改成 `ItemsSource`+`DataTemplate`，用上该机制 |
| 绘制缓存、帧调度、主题资源失效协议 | Avalonia 本体 | ❌ | v3 起明确不做；CI 禁止新增 |
| 平台窗口、托盘、原生菜单、系统文件对话框 | Avalonia 官方 | ❌ | Arc 只做外观与服务包装 |
| 设计 token / 色板 / 明暗 / 8 accent + 自定义 accent | Arc L1 | ✅ | 设计语言本体 + Theme API |
| 151 个组件 + 各自 `ControlTheme` + 图标体系 | Arc L2 | ✅ | 对标 AtomUI 的组件集合；补图标扩展点 |
| 窗口壳 / 服务层 / 容器 / 数据驱动列表 / 控件契约 | Arc L2 | ✅ | 对标 Semi/Ursa/Material 的"框架能力" |
| 页面级组合（Special 55 + Blocks 111） | Arc L3（可选） | ✅ | 示例与组合配方 |
| Agent Workspace + 表现层运行时 + 组合根 + 治理 UI | Arc L4 | ✅ | 差异化能力 |
| Agent 执行、模型、工具、MCP、持久化、托盘/热键 | 宿主 | ❌ | 库的边界之外 |

**推论**：Arc 与 AtomUI/Semi/Ursa 同生态位；主战场是"组件覆盖、设计一致性、API 质量、接入成本、文档"，
不是"渲染性能、主题引擎、虚拟化机制"。**生态位对齐 ≠ 完全替代**：不承诺 drop-in，不追求 78 个内建控件全量换肤与 843 个图标。

### 2.1 分层模型（L0 官方平台 + 4 层自有 + 1 个聚合例外）

| 层 | 包/工程 | 职责 | 允许依赖 | 禁止 |
|---|---|---|---|---|
| **L0 官方平台（允许）** | `Avalonia` 12、`Avalonia.Themes.Fluent`、`Avalonia.Fonts.Inter`、`Avalonia.Controls.DataGrid/ColorPicker`（按需）、`Avalonia.Desktop`（仅应用）、Skia/HarfBuzz（传递） | 渲染/排版/输入/基础控件模板/字体/官方附加控件；并提供渲染后端、布局引擎、虚拟化机制、主题系统、平台窗口——**Arc 全部不重做** | — | 第三方 UI 组件库（见 §2.2） |
| **L1 基础层** | `Arc.Avalonia.Foundation` | 82 token、OKLCH 色彩引擎、`ArcThemeManager`、动效、161 图标、`ArcIconProvider` 扩展点、主题引擎、契约接口、原子控件（Button/Card/Badge/Icon/Spinner） | L0 | 依赖 L2 及以上；含页面/文档 |
| **L2 通用 UI 组件层** | `Arc.Avalonia.Controls.{Inputs,Actions,Navigation,Overlays,Feedback,DataDisplay,Charts,Pickers,Effects,Content,Media}` | 151 控件 + 通用 UI 能力：服务层实现、窗口壳（可选）、容器、数据驱动列表、焦点/键盘/无障碍/本地化/尺寸 token 契约 | L1（+L2 内 DAG） | 依赖 L2.5/L3/L4；依赖 Demo/Docs；同层成环 |
| **L2.5 主题聚合层（例外）** | `Arc.Avalonia.Theme` | 聚合：`FluentTheme` + token 字典 + Fluent 资源键覆盖（含 Top 15–20 内建控件）+ 合并 11 个组件主题字典；`ArcTheme` 入口 | L1 + 11 个 L2 包（**显式允许的聚合例外**） | 被 L2 反向依赖；含控件实现 |
| **L3 组合/模式层（可选）** | `Arc.Avalonia.Patterns.{Special.*,Blocks.*}` | 55 Special + 111 Blocks：页面级组合配方与演示 | L2 | 被 L2 依赖 |
| **L4 Agent 顶层** | `Arc.Avalonia.Agent` | `IArcAgentWorkspace`（多会话/多 run）+ 投影 reducer + 增量视图 + 审批/注意力队列 + 组合控件 + 条目视图注册 + 治理 UI | L1+L2（+可选 L3） | I/O、传输、持久化、模型调用、渲染 |
| **L5 宿主** | 应用 | Agent 执行、工具、MCP、语音、调度、存储、托盘 | 全部 | — |

**L2 内部两个子层**（B 报告实测跨族依赖只有 `DataDisplay→Feedback` 是真实依赖）：

```text
L2a 基础控件族   Feedback（进度/头像/卡片/空态）· Actions（按钮/菜单）
L2b 复合控件族   Inputs · Navigation · Overlays · DataDisplay · Charts · Pickers · Effects · Content · Media
                 （可依赖 L2a；L2a 不得反向依赖 L2b）
```

### 2.2 依赖规则：官方白名单 + 第三方黑名单 + 8 条硬规则

**外部依赖判定（v3.1，按 R4 补漏）**：

| 类别 | 允许 | 不允许 |
|---|---|---|
| Avalonia 官方（直接） | `Avalonia`、`Avalonia.Base/Controls/Markup.Xaml`、`Avalonia.Themes.Fluent`、`Avalonia.Fonts.Inter`、`Avalonia.Controls.DataGrid/ColorPicker`（按需，走 Feature 包）、`Avalonia.Desktop`（仅应用） | — |
| Avalonia 官方（传递/平台） | `Avalonia.Skia`、`SkiaSharp`+native assets、`HarfBuzzSharp`、`Avalonia.Win32/X11/Native/Wayland`、`Avalonia.Remote.Protocol`、`Avalonia.BuildServices`、`MicroCom.Runtime` | — |
| 第三方 UI/组件/图标库 | — | **AtomUI.\*（全族）、Semi.Avalonia\*、Ursa.\*/Irihi.\*、FluentAvaloniaUI、SukiUI、Material.Avalonia\*、Material.Icons\*、AntDesign.Avalonia\*、IconPacks.\*、Lucide.Avalonia、Dock.Avalonia、Tabalonia、AvaloniaEdit、Markdown.Avalonia、DialogHost.Avalonia、Svg.\*、ShimSkiaSharp、ColorDocument.Avalonia** |
| 一般库 | `System.*`、诊断/日志/序列化等非 UI 库（按需评审） | 与 UI 外观/控件语义相关的库 |

**8 条硬规则**：

| 规则 | 内容 | 门禁方式 |
|---|---|---|
| **R1 只能向下** | 包依赖沿 L0→L1→L2→L4 单向（L2.5 聚合例外显式 allowlist） | 架构测试解析 `project.assets.json` |
| **R2 文档/示例只能反向依赖库** | Pages/Docs/DemoUi/samples 是**叶子**；发布库禁止引用 | grep 库 csproj |
| **R3 外部依赖白名单 + 黑名单（含传递）** | 直接与**传递**依赖都必须落在允许集合；黑名单命中即失败 | CI 解析 `project.assets.json` 传递闭包并 fail-closed |
| **R4 主题只做叠加** | 主题资源只引用 `Arc*` token 与官方 Fluent 资源；不得 `StyleInclude` 第三方主题 | grep `avares://` 来源 |
| **R5 发布物不含演示物** | 无 `EmbeddedResource Docs`、无 `Pages/`、无 demo PNG、无 persona 默认值 | `dotnet pack` 后检查 |
| **R6 token-only** | 控件 XAML/C# 不得出现 hex/rgb；自绘控件取色走 `ArcResources`/`DynamicResource` | 扫描脚本 |
| **R7 官方附加控件走 Feature 包** | `Avalonia.Controls.DataGrid/ColorPicker` 只能出现在 `Arc.Avalonia.Feature.*`，并配 Arc 皮肤 | 包结构检查 |
| **R8 无渲染层自研** | `Arc.Avalonia.*` 不得新增帧时钟/绘制缓存/主题失效协议类代码 | analyzer + 命名扫描 |

> **为什么 R2 是分水岭**：它决定"框架"与"画廊伴生库"的区别。614 个文件仍在仓库、仍可被 audit/smoke/截图验证，只是搬到叶子工程。
> **R3 的升级点（R4 评审）**：只看直接依赖会被"官方附加控件间接带进来的包"绕过，必须查传递闭包。

### 2.3 NuGet 包拓扑（v3.1）

```text
Avalonia 12 官方：Avalonia + Avalonia.Themes.Fluent + Avalonia.Fonts.Inter      ← L0，允许
└─ Arc.Avalonia.Foundation                      L1  设计系统内核 + 原子控件 + 图标扩展点
   ├─ Arc.Avalonia.Controls.Inputs … .Media      L2  11 个组件族（各自只带自己的 ControlTheme）
   │   ├─ Arc.Avalonia.Patterns.{Special.*,Blocks.*}    L3（可选）
   │   └─ Arc.Avalonia.Agent                     L4  顶层（依赖 L1+L2）
   └─ Arc.Avalonia.Theme                         L2.5 聚合（依赖 L1 + 11 个 L2 包）
         └─ Arc.Avalonia（元包，推荐安装）        = Theme + 全部 L2 + Agent
               └─ Arc.Avalonia.All                = 上一项 + Patterns
```

**命名与边界决策**：
* 元包 `Arc.Avalonia` 依赖 `Arc.Avalonia.Theme`（聚合完整主题），消费者一行接入即可；
* **`Arc.Avalonia.Theme` 是 L2.5 聚合层**（评审修正 P0-1）：它必须依赖 L1 + 11 个组件包，才能把组件主题字典合并进一个入口。
  架构测试对它有显式 allowlist；L2 包**不得**反向依赖 Theme；单独安装 Theme（不装组件包）只在"只用基础控件"场景有效，文档需说明；
* 程序集重命名：`src/Foundation/Arc.Avalonia` → `Arc.Avalonia.Foundation`（程序集名同步改，`avares://Arc.Avalonia/` 仅 6 处引用，
  成本 1 人日）；元包 ID 用 `Arc.Avalonia`，与程序集名解耦；
* 命名冲突（评审 P1）：现有 `Theme/ArcTheme.axaml` 改名为 `Theme/ArcBaseStyles.axaml`；新入口类保留 `ArcTheme`（`<arc:ArcTheme/>`），
  避免"类/资源文件/包名/命名空间"四重同名；
* `Arc.Avalonia.Internal` **解散**（评审 P0-3）：4 个视觉类分别并入 `Controls.Feedback`/`Controls.Actions` 等 L2 包；
  它被 **12 个 csproj** 直接引用，不处理会卡 M1 的 `dotnet pack`（+5 人日，计入 G1）。

**接入形态（目标：1 行替代 27 行）**：

```xml
<Application.Styles>
  <arc:ArcTheme />   <!-- 内部 = FluentTheme + token + Top 内建覆盖 + 11 个组件主题 -->
</Application.Styles>
```

现状：消费者手写 `<FluentTheme/>` + **26 条** `StyleInclude`（`Gallery/App.axaml:7-32`；评审修正：不是 13 条）。

### 2.4 每层的"准入标准"

| 层 | 可以进 | 不可以进 |
|---|---|---|
| L1 | 无状态设计系统资产（token/色引擎/动效/图标/主题引擎/助手）、契约接口、原子控件 | 依赖 DOM/页面/数据集合的控件；业务语义 |
| L2 | 通用 UI 控件、框架服务实现、窗口壳、数据驱动列表、控件级契约 | 页面、文档、demo 数据、Agent 执行逻辑、主题聚合 |
| L2.5 | 官方主题装配、token 与资源键覆盖、组件主题合并、`ArcTheme` 入口 | 任何控件实现；任何业务逻辑 |
| L3 | 页面级组合配方、示例数据、特效演示 | 被 L2 引用的公共 API |
| L4 | Workspace 路由、事件模型、reducer、组合控件、条目视图注册、治理 UI | 网络/文件/模型/系统调用；任何"会自动做事"的代码；渲染 |

### 2.5 主题策略与"中间层应该有多高"

**① 为什么不做"自建内建主题"，但要"覆盖 Top 15–20"**：
* 全量自建 78 个内建控件主题 = 48 人日 + 每次 Avalonia 升级跟进，且 Arc 的差异化不在重写官方模板；
* 同生态位的 AtomUI/Semi/Ursa 都会换肤；Arc 不承诺 drop-in 完全替代，但若不覆盖裸控件，会在"设计一致性"这一生态位指标上明显落后。评审结论（R1/R4）：
  只做"Arc 表面 6 类定向覆盖"（10 人日）**不够**，消费者用裸 `ComboBox/Menu/TabControl/Button/CheckBox` 会露 Fluent；
* 折中方案：**Top 15–20 个内建控件做"资源键 + 少量模板"覆盖**（33 人日，清单已于 2026-11-05 认可；M0 POC 实测 47 键、46/47 纯键、0 模板替换），其余接受 Fluent 并文档说明。
  覆盖清单：Window 背景、TextBox、ComboBox(+Item)、ListBox(+Item)、ScrollBar/ScrollViewer、Button、CheckBox、RadioButton、
  ToggleSwitch、Slider、ProgressBar、TabControl(+Item)、Menu/MenuItem/ContextMenu、ToolTip/Flyout/Popup、Expander、TreeView(+Item)、Label/Separator。
* 机制（评审 P1）：必须用 `ResourceDictionary.ThemeDictionaries`（Light/Dark 两套）与 `FluentTheme.Palettes`
  （官方公共 API 映射 8 accent），不能用裸 `SystemAccentColor`；先做 1 个 POC 验证"两套覆盖 + Palette 换肤 + 组件主题共存"。

**② 中间层的高度：用"能不能写一个普通业务应用"衡量，而不是"有没有自研底层基建"**：

| 判定问题 | v1 的错误答案 | v3.1 的正确答案 |
|---|---|---|
| 中间层是否包含帧调度器/绘制缓存/主题失效协议？ | 是（作为支柱） | **否**——Avalonia 渲染层职责，CI 禁止 |
| 中间层是否包含自研主题引擎？ | 是 | **否**；但包含 Top 15–20 皮肤覆盖（L2.5） |
| 中间层是否包含窗口壳/服务层/容器/数据驱动列表/表单体系？ | 部分 | **是，这才是"一般 UI 开发"撞墙的地方** |
| 中间层验收标准 | 底层指标（timer 数、缓存命中） | **一条端到端路径**：登录 → 列表 → 表单 → 对话框 → 文件上传 → 设置页 |

**③ 若未来要做"连官方主题也接管"**：才评估 v1 的全量自建方案（48 人日 + Tier3 25 人日）；本版不做。

---

## 3. 关键设计决策

### D1 分层轴：按"依赖方向 + 主题所有权"切，而不是按 catalog 类别

* **背景**：现有 `Components/` 的 11 个项目是按上游 Arc 的 23 个类别折叠出来的（`docs/08` §5 已指出）。
* **决策**：**不重新切组件族**，只在族之下立 L1、之上加"通用 UI 能力"，再加 L2.5 聚合层与 L4 Agent。
* **理由**：重切组件族是"零用户收益的 200+ 文件搬迁"；分层才决定能否发布、能否独立升级。
* **代价**：接受 `Inputs` 里文本/选择/开关/滑杆"混装"，按目录内子命名空间整理。

### D2 文档与示例出库：`Pages/` + `Docs/` 迁到独立叶子工程

* **背景**：**614 个文件**（320 页 + 294 文档）在库内；反射发现机制已支持跨程序集。
* **决策**：新建 `src/Docs/Arc.Avalonia.Docs`（引用 11 个组件族 + Agent/Patterns）；`Demo` 拆为 `Arc.Avalonia.Docs.Abstractions`
  ——发布库不再引用，只有 Docs 与 Gallery 引用。
* **理由**：可发布的第一道门槛，机械成本低（B：1 人日机械 + 2–3 人日回验；v3.1 按 614 文件计 9 人日含回验）。
* **风险**：发现顺序导致静默缺失（`--audit` 报 missing 而不是构建失败）；回验必须看计数。
* **代价**：`Navigation`/`Overlays` 对 `Actions` 的两个"假依赖"随页面迁出消失。

### D3 主题：官方 Fluent 之上的"适配 + Top 内建覆盖 + 聚合入口"（33 人日）

* **背景**：v1 假设连 Fluent 都要替换；v2 澄清官方 UI 允许；v3.1 按同生态位对标补足皮肤覆盖；v3.2 明确"不承诺 drop-in、但设计一致性要对齐"。
* **决策**（三块，共约 33 人日）：
  1. **基础适配（15）**：`FluentTheme.Palettes` 映射 8 accent；全局字体/字号/圆角键；TextBox/ScrollViewer/ScrollBar/ListBox/Popup/ToolTip/Window 的资源键覆盖；
     双主题（Light/Dark × 8 accent）核对 + 截图回归；覆盖键常量表 + 升级失效检测。
  2. **Top 15–20 内建控件覆盖（18）**：见 §2.5 清单；以资源键 + 局部 `ControlTheme`（BasedOn Fluent）为主（POC：46/47 键纯键覆盖、0 模板替换）；
     焦点视觉决策（接受 Fluent 边框 or 补 Arc 焦点环）。
  3. **聚合入口（计入 G1，4 人日）**：`ArcTheme`（L2.5）装配 Fluent + token + 覆盖 + 11 个组件主题；POC 验证 ThemeDictionaries 与组件主题共存。
* **理由**：33 人日 < 全量 48 人日 + 长期跟进；以"Top 清单 + 文档写明未覆盖控件"达成生态位对标，不承诺 drop-in。
* **代价/风险**：Fluent 升级会改键 → 键常量表 + grep 门禁 + 升级检查清单；模板替换的控件需双主题回归。

### D4 依赖边界：官方允许 / 第三方禁止（含传递闭包）

* **决策**：按 §2.2 的四档判定与 8 条规则执行；**CI 必须解析 `project.assets.json` 的传递依赖**（R4 评审），
  并对黑名单 fail-closed；官方附加控件只能出现在 `Feature.*` 包。
* **理由**：用户口径明确；当前仓库已满足，门禁防回归；传递依赖检查堵住"官方包间接引入第三方"的漏洞。
* **代价**：门禁脚本 +1 人日；Feature 包结构需维护。

### D5 基础层纯度：Foundation 只留内核；Internal 解散

* **现状问题**（A §2.3）：`ArcVirtualList`/`ArcUi` 混在 Foundation；`ArcResources` 5 处 `Application.Current`；`ControlThumbShadow` 未注册。
* **决策**：
  * Foundation 保留：token/色引擎/动效/图标（+`ArcIconProvider` 扩展点）/主题引擎/契约接口/原子控件；
  * `ArcResources` → `IArcResourceHost`（默认实现走 `Application.Current`，测试可替换）；
  * `ArcVirtualList` 移到 L2；`ArcUi` 移到 L2 共享内部；
  * **`Arc.Avalonia.Internal` 解散**：4 个视觉类并入对应 L2 包，解除 12 个 csproj 引用与 6 处 `InternalsVisibleTo`；
  * 程序集重命名 `Arc.Avalonia` → `Arc.Avalonia.Foundation`（6 处 avares URI + 文档，1 人日）。
* **理由**：基础层要能被无 UI 单元测试直接引用；Internal 解散是 M1 `dotnet pack` 的前置条件（评审 P0-3）。
* **代价**：10 人日（纯度 5 + Internal 5）+ 重命名 1（含在 G1）。

### D6 中间层能力：C1–C7（按评审重估）

| ID | 能力 | 解决什么 | 必要性 | 估 |
|---|---|---|---|---|
| C1 | **组件契约与 API 收敛**：Top-40 交互事件 → `RoutedEvent`、106 个模板控件补 `[TemplatePart]`、`ItemsSource` 迁移、10 个 `*Size` 枚举统一、公开面收敛、Charts 22 控件主题化、键盘/焦点契约文档 | 143 CLR event/0 RoutedEvent、0 TemplatePart、API 不统一 | **必需** | 53 |
| C2 | **服务层 + 窗口壳**：Dialog/Notification/Navigation/FilePicker 服务 + 窗口级 Overlay host + `ArcWindow`（自绘标题栏可选） | 0 服务接口；`ArcDialog` 只能在面板内；文件控件直连 `StorageProvider` | **必需** | 24 |
| C3 | **数据驱动列表（降级方案）**：列表复用 `ArcVirtualList`（ListBox+VSP）；树改为展平 + 虚拟化；**网格用官方 `Avalonia.Controls.DataGrid` 走 `Feature.DataGrid` 包 + 分页**；替换高频 `Children.Clear` | 18 处 `Children.Clear()`；DataGrid/TreeTable 自述无虚拟化 | **必需** | 25 |
| C4 | **无障碍基础设施**：15 个关键控件 `AutomationPeer` + 角色/值模式 + `AutomationId` 约定 | 312 处 `SetName` 但 0 peer | 发布必需 | 15 |
| C5 | **本地化 / RTL**：`.resx` 骨架 + 116 个英文 DP 默认值迁移 + `FlowDirection` 契约 + RTL 镜像审查 | 0 `.resx`、0 RTL | 发布必需（M2 最小集 6 / M3 全量 9） | 15 |
| C6 | **DnD / MVVM / 容器 / 尺寸 token**：通用拖放、`ICommand` 属性、`CollectionView`、SplitView/GridSplitter/Tab 容器、**尺寸 token 复核（不是 DPI 渲染）** | 0 DnD、`ICommand` 仅 1 处、尺寸常量硬编码 | 可选 | 26 |
| C7 | **测试基线**：Headless 工程、151 控件 × 双主题矩阵、键盘/焦点契约检查、控件级（非渲染）性能断言 | 仅 1 个控制台 `Verify` | 必需（发布前） | 24 |

* **理由**：C1–C3 + C7 是"能写业务应用"的分界；C4/C5 是"能对外发布"的分界；C6 决定"像不像通用框架"。
* **C3 的评审结论**：三控件是 `TemplatedControl` 手工全量建行（4,635 行），"改 ItemsSource 白拿虚拟化"不成立；
  字面做真数据驱动需 40–60 人日。v3.1 采用降级方案（25 人日），**真网格重写列为 M3 可选 +20–30**。

### D7 控件契约标准：10 条"进 L2 的通行证"

1. 有 `ControlTheme`（`x:Key="{x:Type ...}"`），主题只引用 `Arc*` token 与官方 Fluent 资源；自绘图表也可被 StyleKey 覆盖；
2. 模板部件用 `[TemplatePart]` 声明，命名统一 `PART_*`；
3. 交互结果用 `RoutedEvent`（可冒泡）或明确的 `ICommand`；
4. 集合型数据用 `ItemsSource` + `DataTemplate`，不提供"只能 new 出来"的 `Items`；
5. 视觉状态用枚举/伪类表达（Loading/Empty/Error/Disabled/Hover/Focus）；
6. 键盘路径完整（Tab/方向/Home/End/Esc），`:focus-visible` 自绘焦点环；
7. 提供 `AutomationPeer` 或至少 ControlType/Value 语义 + `AutomationId` 约定；
8. 双主题（Light/Dark × 8 accent）渲染；reduced-motion 有短路分支；
9. `FlowDirection`/RTL 可用；尺寸使用 Arc 尺寸 token；
10. 不出现英文默认文案（走 `ILocalizer`），不出现 demo 人物数据。

> 现状对照：第 1 条 106/151、第 2 条 0/151、第 3 条 0/151、第 7 条 0 peers、第 10 条 116 处英文默认值。

### D8 Agent 顶层：Workspace + 表现层运行时，不做 Agent 运行时（按评审修订）

* **背景**：`docs/06` §7 已确立"UI 只做展示 + 意图事件"；但纯契约不够；且现有 6 个控件里有 3 个天然跨 run
  （后台 dock / 注意力收件箱 / run 控制条），单会话 API 驱动不了（评审 P0）。
* **决策**：

| 顶层自建（UI 职责） | 留给宿主（不碰） |
|---|---|
| `IArcAgentWorkspace`：多会话/多 run 路由、`ArcAgentRunRef`（前台/后台/子 agent + parentRunId）、跨 run 审批/注意力/artifact 队列 | 产生事件、保证序号、传输（HTTP/WS/IPC） |
| `IArcAgentSession`（单 run 投影）+ 纯 reducer（可重放）+ `EventId`/乱序策略 | 模型调用、工具执行、重试、并发调度 |
| 增量视图：`Dispatcher` 批次合并（**不自建帧时钟**）+ 增量集合（Insert/Update/Remove）+ 虚拟化 | token 生成与背压策略 |
| 审批队列 / 注意力队列 / artifact 索引 | 策略判定、权限校验、审计落库 |
| **条目视图注册**（`IArcAgentItemViewRegistry`，DataTemplate 选择器；不用"渲染器"字样） | 业务自定义控件的业务逻辑 |
| 组合根 `ArcAgentSurface` + 组合控件 + 治理 UI | 持久化、同步、系统能力（托盘/通知/热键/调度） |

* **关键 API 形状**（签名级，详见 C 报告 §4.3 与 R3 评审 §3）：

```csharp
public interface IArcAgentWorkspace {                 // 多会话/多 run 容器
    IReadOnlyList<ArcAgentRunRef> Runs { get; }        // 前台/后台/子 agent
    IArcAgentSession GetSession(string runId);         // 单 run 投影
    IReadOnlyList<ArcApprovalRequest> PendingApprovals { get; }   // 跨 run 聚合
    IReadOnlyList<ArcAttentionItem> Attention { get; }            // 跨 run 聚合
    void Apply(ArcAgentEvent e);                       // 纯投影，无副作用
    event EventHandler<ArcWorkspaceChangedEventArgs>? Changed;
}
public interface IArcAgentSession { /* 单 run：Messages/ToolCalls/Run + Changed */ }
public interface IArcAgentTransport { ... }            // 宿主：推事件
public interface IArcAgentCommandSink { ... }          // 宿主：stop/steer/approve/takeover
public interface IArcAgentItemViewRegistry { ... }     // 值/条目 → DataTemplate
public interface IArcAgentServices { ... }             // 宿主注入的 UI 服务（对话框/通知）
```

* **边界 CI 化（评审 P0）**：`BannedApiAnalyzers` + 引用白名单，禁止 `HttpClient`/`File.Write`/`System.Threading.Timer` 出现在 Agent 程序集的业务路径；
  增加"无宿主渲染 smoke"（只注入 fake transport 即可渲染 Surface）。
* **理由**：对齐业界（AG-UI 事件语义、assistant-ui `ExternalStoreRuntime`、Vercel AI SDK UI、CopilotKit 三层），但只借语义不引 SDK。
* **代价/风险**：reducer 内出现 I/O 会退化成"迷你 Agent SDK"；评审已把多会话/压测/迁移/边界 CI 计入 G4（+20）。

### D9 组合层与场景层降级：Patterns 可选、LifeToday 横向

* `Special.*`（55）与 `Blocks.*`（111）定位为 **L3 Patterns**，不被 L2 依赖；`Arc.Avalonia.All` 包含，元包不包含。
* `Blocks.AI` 不整体吸收：18 页是目录与截图验收基线；只把 6 个关键配方重写为顶层示例（重写目标工程 = Docs/samples，归 G5）。
* `LifeToday` 移到 `src/Scenarios/`：7 个控件是提醒/用药/议程/捕获，无 run/approval/streaming 语义；可依赖 L4（life → agent）。
* 13 个 Agent/Life 场景页随 D2 一起出库（评审修正：G2 的 614 文件已含）。

### D10 版本、发布与兼容

* lockstep 版本（`0.9.0` → `1.0.0`），与 Avalonia `12.x` 对齐；
* 打开 `GenerateDocumentationFile`（1,341 段 summary 目前白写），产出 XML + SourceLink + 符号包；引入 PublicApiAnalyzers 基线；
* 每包 `PackageId/Description/Authors/License=MIT/PackageReadmeFile`；根补 `LICENSE` + 第三方声明
  （Avalonia/SkiaSharp MIT、Lucide ISC、Inter OFL、Arc UI MIT、AG-UI 仅语义参考不引 SDK）；
* 提供可编译 `samples/`（登录/设置/数据页各一）+ `.editorconfig` + `global.json` + CI 脚本；
* **口径**：M3 的"Agent 完整"= C 报告 **M 档（124 人日）**，不含 06 长尾 7 项（23 人日，列 post-1.0）；若要 L 档需 +31。

---

## 4. 工作量估算

### 4.1 口径与假设

| 项 | 定义 |
|---|---|
| 1 人日 | 8 小时，含自测；1 名熟悉 Avalonia 12 的工程师 |
| 范围 | **净增量**：现有 151 控件、294 页面、55 Special、111 Blocks 的功能开发量不计入 |
| 不含 | 产品设计、新增业务组件（`docs/06` 57 项另计约 235 人日）、Agent 执行运行时、文档站运营、渲染层优化 |
| 区间 | 乐观 / 悲观；正文取中性 |
| 基准 | A/B/C/D 审计 + R1–R4 评审 + Lead 交叉去重（评审增量已按 §10 去重） |

### 4.2 工作包（WBS，v3.1）

| ID | 工作包 | 内容 | 人日 | 区间 | 依据 |
|---|---|---|---:|---:|---|
| **G0** | **治理与护栏** | 依赖白/黑名单（含传递）脚本、分层架构测试、边界 CI（BannedApi）、CI（build+verify+audit+smoke+shots+pack）、版本/发布脚本、`.editorconfig`/`global.json` | **20** | 15–27 | R3/R4/Lead |
| **G1** | **基础层、主题与聚合入口** | 纯度+Internal 解散 10；Fluent 基础适配 15；Top 15–20 内建控件覆盖 18；`ArcTheme` 聚合入口（L2.5）4；程序集重命名 1（含在纯度内） | **47** | 34–61 | R1/R4/A + M0 POC |
| **G2** | **文档/示例解耦与发布** | 614 文件出库 9；Docs.Abstractions 4；Gallery/audit/smoke/shots 适配 4；包元数据/XML/meta/CPM 12；samples 4；API 基线 3；LICENSE/NOTICE 2；demo 残留清理 3 | **41** | 32–52 | B/R1/R2 |
| **G3a** | **组件契约与 API 收敛**（C1） | 见 D6 C1 | **53** | 40–70 | R2/B |
| **G3b** | **服务层 + 窗口壳**（C2） | 4 服务 + Overlay host + `ArcWindow` + 无头替身 | **24** | 16–35 | B |
| **G3c** | **数据驱动列表（降级）**（C3） | 列表复用 `ArcVirtualList`；树展平虚拟化；网格官方 DataGrid Feature+分页 | **25** | 18–36 | R2 |
| **G3d** | **无障碍 + 本地化/RTL**（C4/C5） | 15 关键控件 peer；`.resx`；116 默认值；RTL 审查（M2 6 / M3 24） | **30** | 22–45 | B/R4 |
| **G3e** | **DnD / MVVM / 容器 / 尺寸 token**（C6） | 拖放、`ICommand`、`CollectionView`、SplitView/GridSplitter/Tab、尺寸 token 复核 | **26** | 18–36 | R2 |
| **G3f** | **测试基线**（C7） | Headless、151×双主题矩阵、键盘/焦点契约、控件级性能断言 | **24** | 18–34 | R2/D |
| **G3g** | **图标体系** | `ArcIconProvider` 扩展点 + 自定义 path 注册 + 100 个高频图标补齐（不追 843） | **4** | 3–6 | R4 |
| **G3h** | **Theme API** | `ArcThemeOptions`：自定义 accent（品牌色）、token 覆盖、密度/圆角 | **4** | 3–6 | R4 |
| **G4** | **Agent 顶层** | MVP 54（Workspace 8 + 投影/事件 8 + 增量 transcript 14 + 审批/工具 8 + 视图注册/队列 5 + Gallery/护栏 6 + 迁移 5）；完整 124（+子 agent/artifact/diff 10 + 治理 UI 35 + 迁移/验收 15） | **124** | 54–160 | C/R3 |
| **G5** | **迁移收尾** | 旧 API 兼容/弃用 5；Gallery 自举与回归 8；README/CONVENTIONS/01–13 更新 4；首版发布 3；LifeToday/Blocks 目标与许可 3 | **23** | 17–29 | R3/Lead |
| | **合计（M3 全量）** | | **445** | **291–597** | |
| （不做） | 网格真数据驱动重写 | 已决策采用"自有网格 + 官方 DataGrid Feature"分工，不重写 | 0 | — | 决策 D-3 |
| （post-1.0） | Agent L 档 | 06 长尾 7 项（23 人日）+ 相关验收；1.0 不纳入 | +31 | — | 决策 D-2 |

> 去重说明（评审）：G2 原"API 收敛 13"拆分——demo 残留 3 留 G2、API 收敛 10 并入 G3a；C1 原含 ItemsSource 与 C3 重复约 6，已剔除；
> R1 的 samples/API/LICENSE 与 R2 的 samples 已合并计入 G2；主题适配不再按 v1 的 48+Tier3 双计。

### 4.3 里程碑与情景

| 阶段 | 包含 | 人日 | 里程碑验收（硬门槛） |
|---|---|---|---|
| **M0 准备**（1 周） | 冻结基线（commit + 截图基线 + audit/smoke 计数）、落 R1–R8 门禁、`docs/13` 评审定稿、主题 POC（ThemeDictionaries+Palettes）、`ArcTheme` 聚合 POC | 8（含在 G0） | 门禁脚本能在当前树"红/绿"正确判定；两个 POC 通过 |
| **M1 分层 + 可发布（α）** | G0+G1a+G2 | **≈90（62–118）** | ① `dotnet pack` 产出不含 Demo/页面/文档的包，依赖闭包只含官方包；② 一行 `<arc:ArcTheme/>` 可接入；③ Internal 解散、614 文件出库后 audit/smoke/双主题截图通过；④ XML/许可证/samples 齐备 |
| **M2 通用 UI 能力 + 内建皮肤 + Agent MVP（β）** | +G1b+G3a+G3b+G3c+G3g+G3h+G3d(6)+G4 MVP+G5 | **≈301（228–387）**（累计） | ① 示例应用跑通"登录→列表→表单→对话框→文件上传→设置页"；② **设计一致性抽检**：Top 清单中 ≥12 个控件呈现 Arc 皮肤（生态位对齐；不作为 drop-in 承诺）；③ 服务层可无头测试；④ Agent：fake transport 下 Workspace 驱动 6 个叶子控件；headless 断言 0 全量重建、可见行 ≤2×视口、分配/GC 阈值；桌面 p95 ≤16.7ms |
| **M3 全量成熟（1.0）** | +G3d(24)+G3e+G3f+G4 完整 | **≈445（291–597）**（累计） | ① 15 关键控件有 peer 且读屏抽检；② RTL 与 1.25–2x 缩放抽检；③ 公共 API 基线 + 每包 XML；④ Agent 完整顶层（Workspace/子 agent/artifact/治理 UI 清单见 R3 §4） |

**情景对照**：

| 情景 | 内容 | 人日 | 3 人 | 4 人 |
|---|---|---:|---:|---:|
| S1 | 分层 + 可发布（组件库形态，不承诺 drop-in） | 90 | 8 周 | 6 周 |
| S2 | S1 + 通用 UI 能力 + Top 皮肤 + Agent MVP（**与 AtomUI 同生态位**） | 301 | 24 周 | 18 周 |
| S3 | 全量（M3） | 445 | 36 周 | 27 周 |
| S3′ | 不追求同级设计一致性（不做 Top 覆盖/图标/Theme API；本版不采用） | 422 | 34 周 | 26 周 |

> 日历按 5 人日/周 + 20% 评审/联调/回归损耗。**2 人团队**：S3 ≈ 53 周（12 个月）；**1 人**：S3 ≈ 106 周（约 2 年）。

### 4.4 敏感性分析

| 变量 | 影响 | 说明 |
|---|---|---|
| 主题覆盖深度 | **±10 人日** | 若多数控件只能靠模板替换（而非资源键），Top 15–20 会超支；POC 先验证 3 个代表控件 |
| 无障碍/本地化 | **±20 人日** | 116 个英文 DP 默认值是公开 API 行为；151 控件 RTL 镜像审查 + 截图基线重拍 |
| 网格方案 | **+20–30 人日** | 若坚持自研网格真虚拟化（评审结论：字面 40–60），M3 上限抬升 |
| Agent 流式性能 | **±20 人日** | 批处理 + 增量集合 + 虚拟化；若 O(n²) 不解决，需重做 transcript |
| Fluent 升级 | **±5 人日/次** | 覆盖键常量表 + grep 门禁 + 升级检查清单 |
| 团队实现方式 | 不可量化 | 多 agent 并行可压缩编码，但评审/回归成本不降反升 |

### 4.5 最容易被低估 / 高估的项

| 被低估 | 依据 |
|---|---|
| "裸控件皮肤"（评审新增） | 只做 Arc 表面 6 类 ≠ 替代 AtomUI；消费者会用裸 ComboBox/Menu/Tab，必须 Top 15–20（+20） |
| 无障碍"自动化对等" | 312 处 `SetName` 掩盖 0 peer/0 ControlType；逐控件写 peer + 值模式（15 人日下界） |
| 本地化/RTL | 改公开默认值 + 151 控件 RTL 审查 + 全部截图基线重拍 |
| 数据驱动列表 | 三控件 4,635 行手工建行；"白拿虚拟化"不成立（R2：字面 40–60） |
| Agent 多会话 | 单会话 API 驱动不了 3 个跨 run 控件；Workspace 是容器级缺口（+8） |
| 发布工程 | 包元数据/许可证/XML/samples/CI 全缺（G2 41 人日） |
| **被高估** | |
| "无 UI 依赖" | 当前已满足；只需门禁 1 人日（+传递依赖检查） |
| 自研底层性能基建 | v3 起明确不做（v1 的 21 人日已删除） |
| 页面/文档出库 | 614 文件但反射发现已就绪：9 人日含回验（不是 20+） |

### 4.6 官方附加控件：网格采用官方 DataGrid（评审修订）

| 选项 | 收益 | 代价 | 结论 |
|---|---|---|---|
| **网格：`Feature.DataGrid` 包装官方 `Avalonia.Controls.DataGrid`** | 免费获得行/列虚拟化、编辑、剪贴板；C3 从 40–60 降到 25 | 需 Arc 皮肤覆盖（列入 Top 覆盖）；与自有 `ArcDataGrid` 并存 | **采用**（R2 推荐）：小数据用 `ArcDataGrid`，大数据用 Feature 包；文档写明 |
| ColorPicker：不采用（自研 `ArcColorPicker` 已够用） | 设计一致 | — | 不采用 |
| 自研网格真虚拟化 | 单套 API | +20–30 人日 | **不做**（决策 D-3） |
| 用官方 DataGrid 替换自有网格 | 少一套 API | 设计语言与 API 断层 | 不做 |

> **决策（2026-11-05，D-3）**：采用"自有 `ArcDataGrid`（小数据/设计一致）+ `Arc.Avalonia.Feature.DataGrid` 包装官方 DataGrid（大数据/虚拟化）"分工；
> 不重写自有网格，不整体替换；未覆盖的官方 DataGrid 外观在 Feature 包内做 Arc 皮肤或明确标注。

---

## 5. 分阶段路线图（执行版）

### M0 · 冻结与门禁（1 周，8 人日含在 G0；执行清单见 [`docs/14-m0-baseline-and-gates.zh.md`](14-m0-baseline-and-gates.zh.md)）

1. `git tag pre-framework` + 记录 audit/smoke 计数与截图基线；
2. 落地 R1–R8 门禁脚本（含传递依赖白名单/黑名单、无渲染层扫描、BannedApi）；
3. 两个 POC：`ArcTheme` 聚合（FluentTheme + ThemeDictionaries + 组件主题共存）、Top 3 内建控件资源键覆盖；
4. 评审本文 §2–§3（架构决策），后续变更走 ADR。

### M1 · 分层 + 可发布（6–8 周 / 3 人，约 90 人日）

| 周 | 动作 | 产出 |
|---|---|---|
| 1–2 | D2 出库（614 文件）、Docs.Abstractions、R2/R5 门禁 | 库 csproj 不再引用 Demo；audit 计数不降 |
| 2–3 | D5 纯度 + Internal 解散 + 程序集重命名 + Inter 保留 | `dotnet pack` 不会拖入 Demo；12 处引用解除 |
| 3–5 | D3 基础适配（Palettes/ThemeDictionaries/资源键）+ `ArcTheme` 聚合入口 | 一行接入；双主题无冲突 |
| 5–6 | G2 包元数据/XML/meta/CPM/samples/API 基线/LICENSE | 可安装的 `0.9.0-alpha`；依赖闭包仅官方包 |
| 6–7 | 双主题截图 + audit + smoke 回归；R1–R8 全绿 | M1 验收通过 |

**M1 回滚点**：`ArcTheme` 内部可退化为"FluentTheme + token"（不做覆盖），至少与现状等价。

### M2 · 通用 UI 能力 + 内建皮肤 + Agent MVP（+8–12 周 / 3–4 人，累计约 301 人日）

| 顺序 | 动作 | 理由 |
|---|---|---|
| 1 | G3b 服务层 + 窗口壳 | "能写业务应用"的分界线；示例应用先跑起来 |
| 2 | G3c 数据驱动列表（列表/树/网格 Feature） | 长列表/数据页与 Agent transcript 的共同前提 |
| 3 | G3a 契约收敛（Top-40 事件 + 106 控件 TemplatePart + Charts） | 让第三方能扩主题、容器能统一监听事件 |
| 4 | G1b Top 15–20 内建控件覆盖 + G3g 图标 + G3h Theme API | "替代 AtomUI"的可见面 |
| 5 | G3d 本地化最小集（en/zh，分页/日历/对话框/Toast） | 迁移项目的硬需求 |
| 6 | G4 MVP（Workspace + 投影 + 增量 transcript + 审批 + 组合根） | 顶层价值兑现；与 G3a 并行 |
| 7 | G5 迁移收尾 + 示例应用验收 | 防止文档漂移与旧 API 残留 |

**M2 回滚点**：Agent 独立包；Top 覆盖以"按控件开关"的方式分片发布，未覆盖控件文档标注 Fluent 外观。

### M3 · 全量成熟（+8–12 周 / 3–4 人，累计约 445 人日）

1. G3d 全量（无障碍 peer + 本地化/RTL 全量）；
2. G3e DnD/MVVM/容器/尺寸 token；
3. G3f 测试基线（151 控件 × 双主题矩阵 + 键盘/焦点契约 + 控件级性能断言）；
4. G4 完整（Workspace 子 agent/artifact/diff + 治理 UI + 验收清单）；
5. Agent L 档（06 长尾 7 项）列 post-1.0；网格不重写（决策 D-3）。

> 注：**不做**帧调度器、绘制缓存、主题版本失效协议（Avalonia 渲染层职责，见 §2.0）；CI 用 R8 禁止新增。

---

## 6. 风险登记册

| # | 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|---|
| R1 | 误引入第三方 UI 库（直接或传递） | 低 | 目标 ④ 失守 | R3 传递依赖门禁 + 黑名单 fail-closed |
| R2 | UI 库内生 Agent 运行时 → 宿主锁定 | 中 | 目标 ① 走偏 | D8 边界 + BannedApi CI + 无宿主渲染 smoke |
| R3 | Agent 流式性能不达标（O(n²) 重建） | 中 | Agent 顶层不可用 | Dispatcher 批处理 + 增量集合 + 虚拟化；M2 门槛量化 |
| R4 | 审批/中断并发（前后台 run 交错） | 中 | 审批状态错乱 | Workspace 串行审批队列 + 请求 id 幂等 + 交错场景测试（R3 评审新增） |
| R5 | 大重构期间 audit/smoke 静默退化 | 中 | 目录/页面悄悄丢 | M0 冻结基线；看计数而非 exit code |
| R6 | 主题覆盖超支 / Fluent 升级失效 | 中 | M2 延期、皮肤漂移 | 先 POC 3 个控件；键常量表 + 升级清单 |
| R7 | 无障碍/本地化被低估 | 高 | M3 超期 | 清单驱动；先 15 个关键控件 peer |
| R8 | 公开 API 面收敛引发内部破坏 | 中 | 编译错误潮 | M1 一次性做（未发布成本最低）+ PublicApiAnalyzers |
| R9 | 生态位/能力表述过度承诺 | 中 | 声誉 | 全文统一"同一生态位、不承诺 drop-in"；README 写覆盖清单与未覆盖控件；M2 通过后再提"对标" |
| R10 | 许可合规（Arc UI MIT / Lucide ISC / Inter OFL / AG-UI 语义参考） | 低 | 法律风险 | 随包 LICENSE/NOTICE + README 声明 |
| R11 | 估算偏差（±30%） | 高 | 排期失信 | 每里程碑重估；先做 S1 验证团队速度 |

---

## 7. 明确不做（避免范围爆炸）

1. **不自研渲染后端/文本整形/IME/布局/动画时钟/合成器**：Avalonia 本体职责。
2. **不做绘制缓存、帧调度器、主题版本失效协议**（v3 起删除；R8 禁止新增）。
3. **不自建虚拟化机制**：只用 Avalonia 的 `VirtualizingStackPanel`；网格走官方 DataGrid Feature。
4. **不做 78 个内建控件全量主题**：只做 Top 15–20（其余接受 Fluent 并文档说明）。
5. **不做 Agent 执行运行时**：模型/工具/MCP/语音/调度/持久化归宿主；不做多会话持久化/事件日志存储。
6. **不重写 111 个 Blocks / 55 Special 为控件**：它们是 L3 示例与目录验收基线。
7. **不追求"全覆盖无障碍"**：先 15 个关键控件 peer，后续按反馈推进。
8. **不在 v1 做完整 MVVM 框架**：只提供命令/集合视图最小辅助（8 人日）。
9. **不整体替换自研网格为官方 DataGrid**：并存（小数据/大数据分工），避免 API 与设计断层。
10. **不承诺"完全 drop-in 替代 AtomUI"**：只做同一生态位的对标准备（Top 皮肤/图标/Theme API/服务层），不追求 78 全量换肤与 843 图标。

---

## 8. 验收与度量（可直接执行）

```powershell
# 1) 构建与验证（现状：0 error / 9 warning；VERIFY checks=34 failures=0）
pwsh -NoProfile tools/build.ps1
dotnet tests\Arc.Avalonia.Tests\bin\Debug\net10.0\Arc.Avalonia.Verify.dll

# 2) 文档目录与页面覆盖（现状 docs=294 pages=294 errors=0）
dotnet src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --audit

# 3) 导航回归（现状 315 页 0 失败）
dotnet src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --smoke

# 4) 双主题截图基线（M1 起每次迁移后跑）
pwsh -NoProfile tools/shots.ps1

# 5) 打包与依赖闭包检查（M1 起）
dotnet pack ArcUI.slnx -c Release -o artifacts
#   断言：不含 Pages/Docs/demo；直接+传递依赖只含 Avalonia 官方包；每包有 .xml 与 LICENSE

# 6) 门禁（M0 起纳入本地 CI）
#   - 外部依赖白名单/黑名单（含传递闭包，R3）
#   - 分层/聚合例外 allowlist（R1）
#   - 发布库不得引用 Demo（R2）
#   - 主题只引用 Arc token/官方 Fluent 资源（R4）
#   - token-only 扫描（R6）
#   - 无渲染层自研扫描（R8）
#   - BannedApi：Agent 程序集禁止 HttpClient/File.Write/Timer 业务逻辑
#   - 公共 API diff（D10）
```

**度量（"算不算成了"）**：

| 指标 | 目标 |
|---|---|
| 第三方 UI 组件库依赖（含传递） | **0**（当前已是 0） |
| 内建控件皮肤覆盖 | ≥15 个（Top 清单见 §2.5），抽检通过（生态位对齐，非 drop-in 承诺） |
| 图标 | 161 + 新增 ≥100，且支持自定义 path/Provider |
| Theme API | 支持自定义 accent/token 覆盖/密度 |
| 发布物 | 不含 Demo/页面/文档；依赖仅官方；有 XML/LICENSE |
| 接入成本 | 1 个元包 + 1 行 `<arc:ArcTheme/>`（当前 1 + 26 行） |
| 通用 UI 能力 | 示例应用跑通六页路径 |
| Agent 接入 | 宿主只实现 3 个接口（Transport/CommandSink/Services）+ Workspace 路由 |
| 渲染层自研代码 | **0**（R8 扫描） |
| 生态位对标自评 | 组件覆盖 / 设计一致性 / 图标 / Theme API / 服务层 五项，对照 R4 矩阵逐项打分（不承诺完全替代） |

---

## 9. 结论与建议

1. **定位确认**：Arc 是 UI 组件库（对标 AtomUI/Semi/Ursa），不是 Avalonia 替代品；渲染/布局/虚拟化机制/主题引擎/平台全部归 Avalonia。
2. **评审修正后的判断**：v3 的 357 人日只能做出"可发布的组件库"；要做到"与 AtomUI 同生态位、设计一致性达标"，需补 **Top 15–20 内建控件皮肤 + 图标扩展 + Theme API + 服务层**，
   总量 **445 人日（291–597）**。注意：这是"**生态位对齐**"，不是 drop-in 替代承诺（不追求 78 全量换肤/843 图标）。
3. **架构推荐**：L0 官方平台 → L1 Foundation → L2 通用 UI → L2.5 主题聚合（例外）→ L3 Patterns（可选）→ L4 Agent；8 条硬规则 + 传递依赖门禁。
4. **里程碑**：M1 ≈90（可发布 α）；M2 ≈301（**与 AtomUI 同生态位的 β**）；M3 ≈445（1.0）。
   3–4 人团队 6.5–8 个月；2 人约 12 个月。
5. **四项决策已确认（2026-11-05，见 §10）**：同生态位不承诺 drop-in、Agent M 档、网格"自有 + 官方 Feature"分工、Top 清单认可。下一步按 M0 启动（冻结基线 + 门禁 + 两个 POC）。

---

## 10. 评审记录与决策（v3.1 → v3.2）

四份对抗式评审（2026-11-05）与采纳情况：

| 评审 | 文件 | 核心结论 | 采纳情况 |
|---|---|---|---|
| R1 基础层/依赖 | [`analysis/13-review-a-foundation-deps.zh.md`](analysis/13-review-a-foundation-deps.zh.md) | Theme 包 L1 聚合 L2 方向倒置；10 人日主题适配是乐观值（中性 15）；Internal 解散未立项且被 12 个 csproj 引用；26 条 StyleInclude；命名四重冲突 | **全部采纳**：Theme 改 L2.5、主题 10→30（含 Top 覆盖）、Internal 解散 +5、入口 POC、改名/样式文件重命名 |
| R2 组件/WBS | [`analysis/13-review-b-components-wbs.zh.md`](analysis/13-review-b-components-wbs.zh.md) | C3 虚拟化"白拿"不成立（三控件 4,635 行，字面 40–60）；G2/G3a 重复计价；C1 44 低估（→53）；DPI 属渲染层；614 文件出库；验收不可判定 | **全部采纳**：C3 降级 25 + 可选重写、去重 −10、C1 53、DPI→尺寸 token、614 文件、可执行验收清单 |
| R3 Agent/里程碑 | [`analysis/13-review-c-agent-gates.zh.md`](analysis/13-review-c-agent-gates.zh.md) | 缺多会话 Workspace（+8）；"帧级 16ms"违反口径；M2/M3 门槛不可判定；边界未 CI 化；迁移/许可漏项；G4 104=M 档非 L 档 | **采纳**：Workspace API、Dispatcher 批处理、量化门槛、BannedApi、迁移 +4、口径写明 M 档 |
| R4 竞品可替代性 | [`analysis/13-review-d-competitor-parity.zh.md`](analysis/13-review-d-competitor-parity.zh.md) | v3 与 AtomUI 生态位差距（主题 6 类 vs 全量、图标 161 vs 843、本地化/服务层缺失）；建议 +21–33；白/黑名单补漏 | **采纳**：Top 15–20 皮肤、图标扩展 +100、Theme API、本地化最小集前移、名单升级为传递依赖；按 D-1 收敛为"生态位对齐"，不做 drop-in 验收 |

**决策记录（2026-11-05 已确认，v3.2）**：

| # | 问题 | 决策 | 对文档的影响 |
|---|---|---|---|
| **D-1** | 是否承诺 drop-in 替代 AtomUI | **不承诺**：Arc 与 AtomUI/Semi/Ursa **同一生态位**，以设计一致性/组件覆盖/文档对标 | 全文删除"替代"表述（改为"生态位对齐"）；不追求 78 全量换肤与 843 图标；验收改为一致性抽检 |
| **D-2** | Agent "完整顶层"口径 | **C 报告 M 档（124 人日，含 R3 评审 +20）** | L 档（+31，06 长尾 7 项）列 post-1.0；§4.2 可选项标注 |
| **D-3** | 网格策略 | **自有 `ArcDataGrid`（小数据/设计一致）+ `Arc.Avalonia.Feature.DataGrid`（大数据/虚拟化）分工** | 取消"自研真虚拟化 +20–30"可选项；§4.6 写明分工与皮肤责任 |
| **D-4** | Top 15–20 皮肤清单 | **认可** §2.5 清单 | 按清单实施；未覆盖控件在文档标注 Fluent 外观 |

**残留可选（不计入 1.0）**：图标继续向 843 靠拢（按需）；Agent L 档；若未来小数据场景也需要虚拟化，再评估网格重写。

> **M0 执行结果（2026-11-05）**：基线 tag `pre-framework`（commit `bfdcffd`）；门禁 R1/R3/R4/R6/R8 绿、R2/R5 known-red、依赖 self-test 4/4；POC-A/POC-B 均通过；M1 任务卡见 [`docs/15-m1-task-cards.zh.md`](15-m1-task-cards.zh.md)；执行记录见 [`docs/14`](14-m0-baseline-and-gates.zh.md) §12。

---

## 附录 A：证据索引

| 报告 | 路径 | 覆盖 |
|---|---|---|
| A. 基础层与去 Fluent | [`analysis/13-a-foundation-and-no-external-ui-dependency.zh.md`](analysis/13-a-foundation-and-no-external-ui-dependency.zh.md) | Foundation/Internal 构成、12 项 Fluent 隐式契约、三档清单、ControlTheme 人日分档、自绘比例 |
| B. 组件中间层 | [`analysis/13-b-component-mid-layer-audit.zh.md`](analysis/13-b-component-mid-layer-audit.zh.md) | 151 控件清点、Demo/页面/文档耦合、19 个 API 样本、21 行能力差集、发布阻断 10 项 |
| C. Agent 顶层 | [`analysis/13-c-agent-top-layer-audit.zh.md`](analysis/13-c-agent-top-layer-audit.zh.md) | 40 项 AI 资产、表现层运行时判断、API 概形、复用率、S/M/L 人日 |
| D. 生态对标 | [`analysis/13-d-ecosystem-benchmark.zh.md`](analysis/13-d-ecosystem-benchmark.zh.md) | 包形态实测、Fluent 80/Simple 79、三档边界、包拓扑 |
| R1–R4 评审 | [`analysis/13-review-*.md`](analysis/) | 依赖/主题、组件/WBS、Agent/里程碑、竞品可替代性 |

本次 Lead 复算（可复现）：29 项目 / 764 文件 / 181,358 行（src+tests，不含 .recon）/ 112 个 `x:Key` ControlTheme（137 元素）/
328 个 Pages 文件（294 条目页 + 21 文档/场景页类）/ **294 份 Docs JSON**（不含目录 JSON）/ 25 个 csproj 引用 Demo /
0 个第三方 UI 库 / 全量构建 0 error / 9 warning；`VERIFY: checks=34`；`AUDIT: items=294 docs=294 pages=294`；`SMOKE: 315 页 0 失败`。

## 附录 B：需要同批修订的文档

| 文档 | 需修订处 |
|---|---|
| `README.md` | 项目表（Theme/Agent/Docs 工程）、依赖声明（仅 Avalonia 官方；第三方 UI 禁止）、构建/打包命令、覆盖清单与"未覆盖控件"说明 |
| `CONVENTIONS.md` | §1 所有权表、§4 主题规则（D7 十条 + Top 覆盖清单）、§8 技术栈声明 |
| `docs/03-implementation.zh.md` | 技术栈与分层描述 |
| `docs/07-p0-ui-implementation.zh.md` | 文件索引过期（AgentUI/LifeToday 路径） |
| `docs/arc-extension-catalog.json` | `project` 字段旧值；缺 `status` |
| `docs/09-project-restructure-phase1.zh.md` | 项目数与分层描述（本文取代其"后续可选项"） |

## 附录 C：术语

| 术语 | 含义 |
|---|---|
| L1 基础层 / Foundation | 设计系统内核、原子控件、主题引擎、图标与扩展点 |
| L2 通用 UI 层 | 面向一般 UI 开发的组件与能力（服务/容器/窗口壳/数据驱动列表） |
| L2.5 主题聚合层 | 聚合官方 Fluent + token + Top 内建覆盖 + 组件主题的唯一例外层 |
| Top 15–20 内建控件覆盖 | 在 Fluent 之上对最常用内建控件做资源键/局部模板覆盖，使裸控件呈 Arc 外观 |
| 数据驱动列表 | 把控件改为 `ItemsSource`+`DataTemplate` 以用上 Avalonia 自带虚拟化；不自研虚拟化 |
| 表现层运行时 | Agent 事件 → 视图状态的纯投影（reducer）+ Workspace 多会话路由，无 I/O |
| 生态位对齐 | 与 AtomUI/Semi/Ursa 在同一生态位可比（组件覆盖、设计一致性、图标、Theme API、服务层）；**不承诺完全 drop-in 替代** |

## 附录 D：修订记录

| 版本 | 日期 | 变更 |
|---|---|---|
| v1 | 2026-10-31 | 严格口径（连 Fluent 也替换）：L0–L6 六层含独立主题层；≈435 人日 |
| v2 | 2026-10-31 | 官方 UI 允许、仅禁第三方 UI 库；取消自建主题层（48→10）；层数 6→5；357 人日 |
| v3 | 2026-11-05 | 定位修正：Arc=UI 组件库，渲染层不做；删除 P1 性能专项；"虚拟化基座"改"数据驱动改造"；M3 357 |
| **v3.1** | 2026-11-05 | **4 份对抗式评审后修订**：主题 10→30（Top 15–20 覆盖）；Theme 改 L2.5 聚合层；Internal 解散 +5；614 文件出库 +5；C1 44→53；C3 20→25（降级+可选重写）；Agent Workspace/压测/迁移 +20；图标 +4、Theme API +4；门禁 +4；去重 −10；**M1 71→90、M2 225→298、M3 357→442**；新增 §10 评审记录与 4 项待决策 |
| **v3.2** | 2026-11-05 | **四项决策确认**：D-1 同生态位、不承诺 drop-in（删除"替代"表述与替代验收）；D-2 Agent 采用 M 档 124（L 档 post-1.0）；D-3 网格"自有 + 官方 Feature"分工（取消重写可选项）；D-4 Top 15–20 清单认可；WBS 维持 **442（288–594）** |
| **v3.2.2** | 2026-11-06 | **M1 完成（α）**：出库 617 文件、Foundation 更名、Internal 解散、ArcTheme 一行接入（47/47 Fluent 键 + 8 accent Palettes）、28 包 `0.9.0-alpha` 可打包、门禁 7/7 GREEN；证据见 docs/15 §9.4–§9.5 |
| **v3.2.1** | 2026-11-05 | **M0 POC 结论落地**：`<arc:ArcTheme/>` 单行入口可行（51 断言，无需 fallback）；Top 皮肤 47 键 / 46 纯键 / 0 模板替换（字号需局部 ControlTheme、ScrollBar 自动收缩需样式）→ Top 覆盖 15→18；G1 44→47、M2 298→301、M3 442→445（291–597）；M1 不变 90；证据 ADR 0003/0004 |
