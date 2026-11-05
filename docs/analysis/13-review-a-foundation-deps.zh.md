# R1 评审：基础层 / 主题适配 / 依赖边界与包拓扑（对 v3 主文档的对抗式评审）

> **评审对象**：`docs/13-framework-layering-and-agent-top.zh.md`（v3，717 行，2026-11-05 基线）。
> **评审基准**：`docs/analysis/13-a-foundation-and-no-external-ui-dependency.zh.md`（A 报告）+ 当前工作树复算（2026-11-05）+ Avalonia 12.1.4 官方源码/主题源码交叉核对。
> **评审方式**：只读静态评审；**未运行 dotnet build / dotnet run / tools/*.ps1**。凡运行后才可见的结论标 `未验证`。
> **范围**：§2.0 职责边界、§2.1 分层、§2.2 依赖规则、§2.3 包拓扑、D3（主题）、D4（依赖边界）、D5（基础层纯度）、WBS 的 G0/G1/G2、以及附录 A 数字。

---

## 0. 评审结论（TL;DR）

**v3 的方向（Arc=组件库、渲染层归 Avalonia、Fluent 允许、只做主题适配）我认同；问题集中在"适配"被当成 10 人日的确定值、以及主题/包拓扑存在一处层级倒置和一处 M1 阻塞项。**

| 等级 | 数量 | 一句话 |
|---|---:|---|
| **P0** | 3 | ① `Arc.Avalonia.Theme` 在 L1 却要聚合 11 个 L2 组件主题，依赖方向倒置，"一行 `arc:ArcTheme`"只在元包安装时成立；② 10 人日主题适配是**乐观下界**而非中性值（中性 15，悲观 22；裸内建控件仍是 Fluent 外观，与"替代 AtomUI"存在口径张力）；③ `Arc.Avalonia.Internal` 解散未进 WBS，而它是 **12 个 csproj 的 ProjectReference**，不处理会直接卡 M1 的 `dotnet pack` |
| **P1** | 7 | 命名冲突（`ArcTheme` 类 vs `Theme/ArcTheme.axaml` vs 命名空间/包名）、一行接入覆盖数（当前 26 条 StyleInclude 而非 13 条）、PackageId≠AssemblyName 的打包机制细节、Fluent 资源键覆盖需要 ThemeDictionaries + `FluentTheme.Palettes`（而非裸 `SystemAccentColor`）、samples/PublicApiAnalyzers/LICENSE 三项遗漏、人日口径（48+Tier3 双计）等 |
| **P2** | 8 | 基线数字口径混用（774/182,621 混入 `.recon` 与非空行口径；328 页 vs 316；298 Docs vs 294；131 vs 137 ControlTheme；80 个 ControlTheme vs 80 个 xaml）、"23 条断言" vs "checks=34"未标口径、`ArcTheme.axaml 只做 Window/TextBlock` 表述不实、`ItemsControl/DataTemplate 0 处` 不准确等 |

**人日结论（建议修订）**：

| 里程碑 | v3 | 建议（中性） | 主要增量 |
|---|---:|---:|---|
| **M1 分层 + 可发布** | ≈71（52–92） | **≈94（72–116）** | 主题适配 +5、ArcTheme 入口 +1、Internal 解散 +5、G2 补 samples/API/LICENSE +9、G0 门禁/POC +2；若 samples 推迟到 M2 则 ≈89 |
| **M2（累计）** | ≈225 | ≈248 | M1 增量 + G3/G4 不变 |
| **M3 全量** | ≈357（280–470） | **≈380（300–496）** | 上述增量累计 |
| 若采用"覆盖 Top 15–20 个内建控件"策略（推荐用于支撑"替代 AtomUI"叙事） | — | **M3 ≈406** | +26（M2/M3 实施） |

> **不在本评审范围**：Agent 顶层（G4）、无障碍/本地化（G3d）、文档站运营、B/C/D 报告的完整复核；这些只做数字交叉核对，不重新审计。

---

## 1. P0 级发现（必须在 M0/M1 定稿前处理）

### P0-1｜`Arc.Avalonia.Theme` 位于 L1 却依赖 L2：包依赖方向倒置，"一行接入"只在元包安装时成立

**问题**
v3 把 `Arc.Avalonia.Theme` 画在 `Arc.Avalonia.Foundation`（L1）之下（§2.3，`13-framework-layering-and-agent-top.zh.md:239-246`），但同一个包要"最后合并各组件包的主题字典"（同文件 `:250-252`）。而 §2.1 对 L1 的硬性要求是"允许依赖 L0、**禁止依赖 L2 及以上**"（`:193-196`），R1 规则也要求"包依赖沿 L0→L1→L2→L4 单向"（`:223-224`）。两者直接冲突。

**证据**
- 当前需要聚合的组件主题文件（每个 L2 项目一个 `<Styles>` 包装 + `ControlTheme` 资源）：`src/Components/*/Themes/*Theme.axaml` 共 **11 个**；加上 Foundation/Internal/Agent 的入口文件，Gallery 现在共 **26 个 `<StyleInclude>` 元素**（`src/Apps/Arc.Avalonia.Gallery/App.axaml:8-32`，共 25 行、26 个元素；`:7` 是 `<FluentTheme />`）。
- 如果 `Arc.Avalonia.Theme` 在编译期 `<StyleInclude>` 这些组件主题，它必须 `ProjectReference`/`PackageReference` 11 个 L2 包 → **L1→L2**，R1 架构测试会红。
- 如果它不依赖 11 个包、只写 `avares://Arc.Avalonia.Inputs/...` 这样的 URI，则单独 `dotnet add package Arc.Avalonia.Theme` 时 Inputs 程序集不存在，`StyleInclude` 在运行时解析失败；只有装元包 `Arc.Avalonia` 才可用。这与"包拓扑图把它当独立 L1 包"的表述矛盾。
- 当前 `Arc.Avalonia.Theme` 工程并不存在（28 个 src 项目里无 Theme 项目）；文档只在拓扑中画出。

**影响**
- R1 门禁无法同时满足"Theme 聚合组件主题"和"L1 只依赖 L0"。
- 消费者若按文档只装 Theme 包（而非元包），主题静默缺组件字典（ControlTheme 找不到 → 组件无模板）。
- 一旦把 Theme 改成依赖 11 个包，`Arc.Avalonia` 元包与 Theme 包的关系、版本 lockstep、发布顺序都要改。

**建议修复（三选一）**
1. **推荐：把 Theme 明确为 L2.5 聚合层**（依赖 L1 + 11 个 L2 包；L2 不反向依赖 Theme；R1 增加一条"聚合包例外"并在架构测试中显式 allowlist）。拓扑图改为 `Foundation → Controls.* → Theme（聚合）→ Agent`，元包 `Arc.Avalonia` 依赖 Theme。
2. **Theme 作为纯内容包**：不含聚合逻辑代码，只发布一个 `ArcTheme.axaml`（`<Styles>`），当且仅当通过元包安装（所有组件程序集存在）时可用；文档明确"Theme 包不可单独使用"。需 POC 验证：Avalonia 的 `avares://` 解析是否要求目标程序集已加载（`未验证`）。
3. **取消独立 Theme 包**，把聚合入口放进元包（元包内放一个极小的 `Arc.Avalonia.Entry` 程序集或 content 文件 + `targets`）。包数少一个，但需要一个额外的极小工程。

**人日增量**：**+2**（1 天方案定稿 + 1 天 POC/文档/门禁调整；不含 P0-2 的主题实现）。

---

### P0-2｜"10 人日主题适配"是乐观下界，不是中性值；且裸内建控件仍是 Fluent 外观

**问题**
v3 把"Fluent 主题适配与定向覆盖"固定为 10 人日（G1，`:465`），并在 §2.5 `:281` 声称"只对 Arc 表面实际出现的内建控件做资源键覆盖与局部 ControlTheme，不做模板重写"。A 报告没有"10 人日"这个数字（A 报告给的是"保现状 23 / 框架级 48"，且 23 是模板重写口径）；10 是 v3 新引入的时，没有任何来源或分解。

**关键机制核对（决定 10 是否可能）**
- Fluent 12.1.4 的 TextBox 主题（`TextBox.xaml`，18,307 B）通过**资源键**控制外观，而不是全部写死：`TextControlBackground* / BorderBrush* / Foreground* / PlaceholderForeground / SelectionHighlightColor / TextControlThemeMinHeight / MinWidth / Padding / BorderThemeThickness / ControlCornerRadius` 等约 20 个键；ScrollBar 同理（`ScrollBarSize`、`ScrollBarThumbFill*`、`ScrollBarTrack*`、`ScrollBarButton*` 约 20 个键）。**因此"资源键覆盖"确实能在不改模板的前提下改变颜色、字号、最小高度、内边距、圆角与滚动条粗细**——10 人日不是完全不可能。
- 但存在 3 个被 v3 忽略的成本：
  1. **变体字典**：Fluent 的键值在 `ThemeDictionaries` 里按 Light/Dark 分套；平铺的 `ResourceDictionary` 覆盖只对默认变体生效。必须用 `ResourceDictionary.ThemeDictionaries` 为两套主题各写一份覆盖（Arc 目前 `ThemeDictionaries` 使用 **0 处**，A 报告 §2.4）。这是新机制 + 双主题验证。
  2. **覆盖面**：当前 Arc 表面不是只有"5 个控件"：`TextBox` 18 个实例/12 个文件（**17 个无 Theme**）、`ScrollViewer` 10 个 axaml + 28 个 cs、`Popup` 7 个 axaml、`ToolTip` 29 个文件/42 处、`ListBox` 3 个 axaml/7 个元素、`Window` 背景全局样式；Gallery 自己还有一个裸 `TextBox`（`GalleryWindow.cs:195`）。这些都要逐项按键覆盖并双主题核对。
  3. **"不做模板重写"的边界**：Fluent TextBox 用 `FocusAdorner="{x:Null}"` + 边框变色表达焦点；Arc 的设计是 2px 焦点环（`ArcButton.axaml:122-125`、`ArcTextField.axaml:42` 等地方 Arc 自己把 `FocusAdorner` 置空并自绘环）。资源键无法把 Fluent 的"焦点边框"变成"外环"；若消费者直接 new TextBox，焦点视觉仍是 Fluent 的边框色（不崩，但视觉不是 Arc）。同理 ScrollBar 的 auto-hide/line button 行为、ToolTip 的圆角/阴影形状。
- **裸控件问题**：v3 的竞品是 AtomUI/Semi/Ursa，这些库都会覆盖全部内建控件主题。若消费者使用裸 `TextBox`/`Window`/`ComboBox`，在"10 人日适配"后仍是 Fluent 的几何与交互细节（颜色/字体可被 Arc token 覆盖）。这与"替代 AtomUI 类组件库"的对外叙事有张力（v3 §2.0 `:188-189` 把"设计一致性、API 质量"列为主战场）。**P0-2 的实质不是"颜色覆盖做不到"，而是"10 人日买不到与 AtomUI 同级的裸控件视觉一致性"。**

**按"定向覆盖"重新估算（覆盖清单 × 逐项人日）**

| # | 覆盖对象 | 当前证据 | 覆盖手段 | 乐观 | 中性 | 悲观 |
|---|---|---|---:|---:|---:|
| 1 | Fluent 调色板/8 accent | 8 accent × Light/Dark（`ArcPalette.cs:276-424`）；官方 `FluentTheme.Palettes` + `ColorPaletteResources.Accent`（Avalonia 12.1.4 源码） | `FluentTheme.Palettes` 公共 API 映射，不使用裸 `SystemAccentColor` | 1.0 | 1.5 | 2.5 |
| 2 | 全局字体/字号/圆角 | Fluent 键 `ContentControlThemeFontFamily`、`ControlContentThemeFontSize`、`ControlCornerRadius`（`Window.xaml` 等官方源） | 覆盖 3 个全局键 + Inter 字体 | 0.5 | 1.0 | 1.5 |
| 3 | **TextBox** | 18 实例/12 文件，17 个无 Theme；`GalleryWindow.cs:195` 裸 TextBox；Fluent 约 20 个键 | ThemeDictionaries 覆盖颜色/尺寸键 + `FocusAdorner` 决策 | 1.5 | 2.0 | 3.5 |
| 4 | **ScrollViewer / ScrollBar** | 10 axaml / 28 cs；ScrollBar 被 ScrollViewer 模板间接使用；Fluent 约 20 个键 | 覆盖 `ScrollBarSize`、Thumb/Track/Button 色与圆角 | 1.0 | 1.5 | 2.5 |
| 5 | **ListBox / ListBoxItem** | 3 axaml/7 元素；已有 6 个 keyed ListBoxItem 主题（`ArcPrimitives.axaml:255,296` 等） | 隐式 ControlTheme（BasedOn Fluent）+ token 色；ArcVirtualList 复用 | 0.75 | 1.25 | 2.0 |
| 6 | **Popup / Flyout / ToolTip** | Popup 7 axaml；ToolTip 29 文件/42 处；Fluent `PopupRoot.xaml`/`ToolTip.xaml` | PopupRoot/ToolTip/FlyoutPresenter 的背景/圆角/阴影/字体键 | 1.0 | 1.5 | 2.5 |
| 7 | **Window 背景/字体** | 已有 `ArcTheme.axaml:14-18` 全局 Window 样式 | 迁移进 ArcTheme + token；OS 装饰不动 | 0.25 | 0.5 | 1.0 |
| 8 | 焦点/验证状态 | 16 处 `FocusAdorner="{x:Null}"`；Fluent `DataValidationErrors.xaml` | 决定"接受 Fluent 焦点边框"或补 2–3 个局部样式 | 0.5 | 0.75 | 1.5 |
| 9 | 双主题 × 8 accent 核对 + 截图回归 | 现有 `docs/images` 100+ PNG；无像素 diff | 逐键 Light/Dark 核对、关键页截图 | 1.5 | 2.5 | 4.0 |
| 10 | 键注册表 + 升级失效检测（CI） | 无覆盖键清单；Fluent 升级会改键 | 键名常量/清单 + grep 门禁 + 升级清单 | 1.0 | 1.0 | 1.5 |
| 11 | ThemeDictionaries/attach-order POC | v3 未列；Fluent 资源在变体字典中 | 1 个 POC：两套覆盖 + Palette 换肤 + 组件主题共存 | 1.0 | 1.0 | 2.0 |
| | **小计** | | | **10.0** | **15.0** | **23.0** |

**结论**
- **10 人日 = 乐观下界**（要求：覆盖键齐全、不做模板重写、接受 Fluent 焦点边框/几何、不做裸控件全覆盖、不做 Top15）。
- **中性 15 人日**，悲观 23 人日。建议 G1 的"主题适配"从 10 改为 **15（10–23）**，G1 总从 18 改为 **25**。
- 如果要在对外叙事上支撑"替代 AtomUI"，还需 **P1 的策略选择**（Top 15–20 覆盖，+26）或明确文档化"裸内建控件为 Fluent 外观"——二者必须选一个，不能默认省略。

---

### P0-3｜`Arc.Avalonia.Internal` 解散未进 WBS，且它是 12 个 csproj 的 ProjectReference：会卡 M1 打包

**问题**
v3 §2.3 只是"建议不发布：4 个视觉类并入 `Arc.Avalonia.Controls.Feedback`（或 Foundation 的 internal 部分），减少一个公开包与 `InternalsVisibleTo` 的版本面"（`:253-254`），但 WBS G1（`:465`）只有"Foundation 纯度化拆分（5）+ 主题适配（10）+ ArcTheme 入口（3）"，**没有 Internal 解散的任何一行**。

**证据（当前工作树复算）**
- `Arc.Avalonia.Internal` 被 **12 个 csproj** 直接 `ProjectReference`：Gallery、6 个 Blocks、5 个 Components（Actions/DataDisplay/Feedback/Navigation/Overlays）。v3 说的"5 处 InternalsVisibleTo"只是 friend 名单（`AssemblyInfo.cs:3-7`），**不是引用面**。
- 内部实际有 **7 个 internal 类型**（`ArcActionVisuals`、`ArcMenuPresenter`、`ArcDismiss`、`ArcKeyboard`、`FeedbackUi`、`ArcTween`、`ArcText`）+ 4 个公开视觉类（`ArcGlide`/`ArcIconButton`/`ArcFlatButton`/`ArcMenuSurface`），被 20/23/11/11/11/8/6/7 个文件使用（见附录证据）。
- `ArcInternalTheme.axaml` 在 `Gallery/App.axaml:9` 被 `StyleInclude`；Internal 还提供 `ArcGlide` 给 Navigation/Actions 主题使用。
- 若 Internal 不发布，**5 个 L2 组件包 + 6 个 L3 Blocks 包（+Gallery）**的打包依赖会指向未发布的 `Arc.Avalonia.Internal`（`dotnet pack` 的 ProjectReference 会变成包依赖），M1 的 pack 门槛直接失败；这不是"建议不发布"能绕过的。

**关于"减少 InternalsVisibleTo"的目标**：不可达。
- 5 个 L2 项目消费 Internal 的 internal 助手是**真实依赖**（`ArcActionVisuals` 20 文件、`FeedbackUi` 23 文件）。把类并入 Feedback 后，Actions/Navigation/Overlays/DataDisplay 仍需要访问这些助手 → 要么变成 public（扩大 L2 公开面），要么继续给 5 个 friend（只是把 friend 从 Internal 移到 Feedback/Foundation）。**"减少一个公开包"可以做到；"减少 InternalsVisibleTo 版本面"做不到，除非扩 API。**

**建议**
- 新增 **G1b「Internal 解散」（5 人日）**：
  - 把 4 个公开视觉类 + 7 个 internal 助手迁到明确归属（推荐：Foundation 的 `Arc.Avalonia.Controls.Internal` 命名空间 + 保持 5 个 friend；或 L2 的共享 `Arc.Avalonia.Controls.Primitives` 包）；
  - 删除 **12 个 csproj 的 ProjectReference**（改为经 Foundation/共享包传递）；
  - 合并/迁移 3 个主题字典，更新 `Gallery/App.axaml:9`；
  - 更新 40+ 处 using/命名空间，跑 audit/smoke/双主题截图。
- 若 Lead 确认这 5 人日原本隐含在 G1 的"5"里，则 G1 的 5 需要拆成 Foundation 5 + Internal 5 = 10（否则 G1 仍少 5）。

**人日增量**：**+5**（若并入 G1，则 G1 从 18→30；建议单列 G1b 更清晰）。

---

## 2. P1 级发现

### P1-1｜`ArcTheme` 命名冲突：类名 / 资源文件 / 命名空间 / 包名四重同名，且"ResourceDictionary 嵌套 FluentTheme"的表述不成立

**问题**
v3 新增 `<arc:ArcTheme/>`（`:256-262`），同时现有：
- 资源文件 `src/Foundation/Arc.Avalonia/Theme/ArcTheme.axaml`（`<Styles>`，117 行）——A 报告 §2.1；
- 命名空间 `Arc.Avalonia.Theme`（4 个文件：`ArcColor.cs`/`ArcPalette.cs`/`ArcThemeManager.cs`/`ArcTokens.cs`，`namespace Arc.Avalonia.Theme`）；
- 拟新增包 `Arc.Avalonia.Theme`。
如果新类也叫 `ArcTheme`，则完整类型名 `Arc.Avalonia.Theme.ArcTheme`、资源 URI `avares://Arc.Avalonia/Theme/ArcTheme.axaml`、包 `Arc.Avalonia.Theme` 三者互相混淆；且 `Styles` 的校验器明确禁止把 `ControlTheme` 直接放进 `Styles`（Avalonia `Styles.cs` 校验逻辑），v3 说的"自定义 Styles/ResourceDictionary 里嵌套 `<FluentTheme/>`"只对 `Styles` 成立，`ResourceDictionary` 不能承载 `IStyle`。

**建议**
- 类名改为 `ArcThemeBundle`（或 `ArcDesignSystem`），命名空间用 `Arc.Avalonia.Theming`，包名仍可用 `Arc.Avalonia.Theme`（或 `Arc.Avalonia.DesignSystem`）；`Theme/ArcTheme.axaml` 保留为资源，或改名为 `ArcThemeBundle.axaml` 消除同名。
- 文档把"Styles/ResourceDictionary"改为"`Styles`（`IStyle` 容器）"。

**人日增量**：**+0.5**（命名/文档/引用更新）。

---

### P1-2｜"一行替代 13 条 StyleInclude"与当前树不一致：当前是 26 条；元包至少 14 条，All 至少 25 条

**证据**
- `Gallery/App.axaml` 当前：`<FluentTheme />` 在 `:7`（1 条），`<StyleInclude>` 元素 **26 条**（`:8-32`，分布在 25 行，`:22` 一行两条）。
- v3 引用的 `Gallery/App.axaml:7-20` 只包含前 13 条（ArcTheme + Internal + 11 Components），**没有** AgentUI/LifeToday（`:21-22`）、Special ×5（`:22-26`）、Blocks ×6（`:27-32`）。
- v3 的元包 `Arc.Avalonia` 含 Agent（`Arc.Avalonia.Agent`，`:244`），因此元包消费者至少要 14 条（13 + AgentUI）；`Arc.Avalonia.All` 若含 Patterns，则是 13 + Agent + 5 Special + 6 Blocks = **25 条**。
- v3 的"现状 1 + 14 行"与"13 条 StyleInclude"（`:264`、`:646`）在分母上也不一致（14 = Fluent + 13；但元包是 15 行）。

**影响**：M1 验收标准"消费者 1 行接入"对 `Arc.Avalonia.All` 不成立；CI/文档若按 13 条写会漏测 L3/L4 主题。

**建议**
- 明确 **两个入口**：`ArcTheme`（L1+L2+Agent）与 `ArcTheme.All`（+Patterns）；或让 `All` 元包额外放一个 `ArcTheme.All.axaml`；更新 `:256-264` 与 `:646` 的计数与验收语句。
- 验收改为："消费者安装元包后，`<Application.Styles>` 只写 1 行（两个入口二选一）"。

**人日增量**：**+0.5**（文档与验收修订；实现约 0.5 已含在 ArcTheme 入口）。

---

### P1-3｜PackageId ≠ AssemblyName 方案可行，但必须显式处理，否则 `dotnet pack` 会与元包重名/依赖断裂

**结论**：**技术可行**，但 v3 没写机制，且会与现有项目默认值冲突。

**证据**
- Foundation 项目 `<AssemblyName>Arc.Avalonia</AssemblyName>`（`Arc.Avalonia.csproj:4`），当前 **0 个 PackageId**（全仓 csproj 无 PackageId/License 元数据）。
- `avares://Arc.Avalonia/` 全仓仅 **4 处、2 个文件**（`Gallery/App.axaml:8` 1 处；`Foundation/Arc.Avalonia/Theme/ArcTheme.axaml:7-9` 3 处）；`avares://Arc.Avalonia.Internal/` 3 处、2 个文件。也就是说"保留程序集名"节省的修改量只有 **4 处 URI + 3 处 Internal**（Internal 本来就要解散），并不是一个强约束。
- 若不设 `<PackageId>`，Foundation 打包默认 PackageId = `Arc.Avalonia`，会与 v3 的元包 `Arc.Avalonia` 撞名，`dotnet pack`/发布直接冲突。
- 元包工程还要处理"不产生程序集"（`IncludeBuildOutput=false`、`SuppressDependenciesWhenPacking` 等）以及依赖列表。

**更稳妥方案（推荐 C 或 B）**

| 方案 | PackageId / AssemblyName | avares 改动 | 优点 | 风险 |
|---|---|---|---:|---|
| A（v3） | Foundation 包=`Arc.Avalonia.Foundation`，程序集=`Arc.Avalonia`；元包=`Arc.Avalonia`；Theme 包 | 0（4 处保留） | 品牌安装名不变 | PackageId≠AssemblyName 造成长期解释成本；必须显式 PackageId；Theme 层倒置（P0-1） |
| **B** | L1 包=程序集=`Arc.Avalonia`（不变）；Theme 独立（L2.5）；元包=`Arc.Avalonia.All`（或 `Arc.Avalonia.Framework`） | 0 | 最稳、零重命名、包/程序集一致 | 失去"安装 `Arc.Avalonia` 就是全家桶"的品牌直觉 |
| **C** | L1 包=程序集=`Arc.Avalonia.Foundation`；元包=`Arc.Avalonia`；Theme 独立 | **+4 处 Foundation URI + 3 处 Internal + Gallery/Probe 引用**（约 1 天） | 包/程序集一致，元包 ID 达成 | 改名成本小但需同步 docs/示例（当前发布前，影响低） |

**建议**：若坚持元包 ID=`Arc.Avalonia`，选 **C**（一次性 7 处 URI + 程序集重命名）优于 A 的"包 ID≠程序集名"长期歧义；若不想改名，选 **B** 并把元包叫 `Arc.Avalonia.All`。无论选哪个，M1 都要加：
- `<PackageId>` 显式声明；
- CI 断言"包 ID 唯一、程序集名稳定、元包无 lib"；
- 发布顺序：L1 → L2 → Theme → Agent → 元包。

**人日增量**：**+1**（打包机制 + CI 断言 + 文档）。

---

### P1-4｜主题覆盖机制：必须用 `ThemeDictionaries` + `FluentTheme.Palettes`，v3 举例的裸 `SystemAccentColor`/`TextControl*` 覆盖需要 POC

**机制核对（Avalonia 12.1.4 源码）**
- `FluentTheme : Styles, IResourceNode`（官方 `FluentTheme.xaml.cs`），构造函数加载 XAML 并暴露 `Palettes: IDictionary<ThemeVariant, ColorPaletteResources>`；`ColorPaletteResources` 提供 `Accent` 及 `System*Color` 属性（`ColorPaletteResources.Properties.cs`）。**这是官方支持的 accent 定制入口**，比裸 `SystemAccentColor` 键更可靠。
- `Styles.TryGetResource` 的顺序是：**自身 `Resources` 优先**，然后**逆序遍历子 Styles**（`Styles.cs`）。因此 `ArcTheme` 把 token + Fluent 键覆盖放在自己的 `Resources` 中，会压过作为子项的 `FluentTheme` 资源——机制成立。
- 但 Fluent 的控件资源值在 `ThemeDictionaries`（Light/Dark）里；平铺覆盖只对默认变体有效；必须用 `ResourceDictionary.ThemeDictionaries` 分别覆盖（Arc 目前 0 处）。`Styles` 校验器还禁止 `ControlTheme` 直接作为 Styles 子项（必须放 Resources）——v3 的"局部 ControlTheme 覆盖"需要写成 `ResourceDictionary` 中的 `ControlTheme x:Key="{x:Type ...}"`，POC 验证。

**建议**：在 ArcTheme POC 中先验证 4 件事：
1. `<Styles>` 子项里嵌套 `<FluentTheme/>` 可行，且双变体资源解析正确；
2. `FluentTheme.Palettes[ThemeVariant.Light/Dark].Accent = ARC_ACCENT` 能驱动内建控件 accent；
3. `Styles.Resources`（含 ThemeDictionaries）覆盖 `TextControlBackground`/`ControlCornerRadius`/`ScrollBarSize` 等键，Light/Dark 各自生效；
4. `ControlTheme BasedOn="{StaticResource {x:Type TextBox}}"`（或隐式 `x:Key={x:Type TextBox}`）能在保留 Fluent 模板的前提下改度量/焦点，且不与组件包既有主题冲突。

**人日增量**：**+2**（1 天 POC + 1 天键注册表/覆盖测试门禁；与 P0-2 的 15 人日不重复——P0-2 是覆盖实现，这里是机制验证与门禁）。

---

### P1-5｜samples / PublicApiAnalyzers / LICENSE/NOTICE 三项承诺未进 WBS

| 项 | v3 承诺 | WBS 现状 | 复算证据 | 建议人日 |
|---|---|---|---|---:|
| 可编译 `samples/` | D10 `:441` | G2（`:466`）无 samples 行；G5 只有文档更新 | `Test-Path samples` = **False**；仓库无 samples 工程/CI 构建 | **+5**（3 个示例 + 本地 feed + CI） |
| PublicApiAnalyzers / 公共 API 基线 | D10 `:440`；§8 `:634` 门禁列出 | G0（`:464`）只有"依赖白名单/CI/build/verify/audit/smoke/shots/pack"，未写分析器与基线生成 | 全仓 `PublicApiAnalyzers` 引用 **0**；无 `PublicAPI.Shipped.txt` | **+3**（引入 + 26 包基线生成 + 现有公开面 triage；分析器本身需进 D4 白名单） |
| LICENSE / NOTICE | D10 `:439` | G2 `:466` "包元数据+XML+meta 包（12）"可能含，但未显式 | 根目录 `LICENSE` = False；`THIRD-PARTY-NOTICES` = False；Lucide ISC/Inter OFL 需第三方声明 | **+1**（NOTICE 生成 + 打包校验；`PackageLicenseExpression=MIT` 本身 0.25） |

> 如不补 samples，D10 的"可编译示例"就是空承诺；如不建 API 基线，R6/API diff 门禁无法执行。

---

### P1-6｜人日口径混乱：A 报告"48"是框架级（≈43 控件），不是"Fluent 等价"；"48 + Tier3 25"是双计

**证据**
- v3 §2.5 `:279`："自研等价主题需要 **48 人日**"；`:293`："A 报告：保现状 23 人日、框架级 48 人日 **+ Tier3 25 人日**"；D3 `:324` 又写"Tier1+2 = 23 人日、Tier3 = 48 人日"。
- A 报告的真实口径：**A = 保现状 23（Tier1 15.5 + Tier2 3.5 + Tier4 4）**；**B = 框架级 48 = A + Tier3 25**（覆盖 ≈43 个内建控件，不是 Fluent 的 80 个 xaml / ≈78 控件）。因此：
  - "48 人日自研**等价**主题"是错配：等价 Fluent（≈78 控件）应高于 48；
  - "48 + Tier3 25"把已经包含的 25 再加一次，正确的是 **23 + 25 = 48**（或 48 本身）。
- v3 §0.0 的"43–78 个内建控件 ControlTheme"也把 A 的 43（Tier3 覆盖）与 Fluent 的 78（控件数）混在一起。

**建议**：统一表述为 — "保现状（当前 Arc 表面）23；框架级（≈43 个内建控件）48；Fluent 等价全量（≈78）≥65（按 D 报告/S 策略估，未验证）"；删除 `48 + 25`。

**人日增量**：0（纯口径；但影响"是否要做自建主题"的决策依据）。

---

### P1-7｜一键接入的"换肤"仍需要代码：`ArcThemeManager.Apply` 现由 App 调用，入口设计需一并改

**证据**
- Gallery 当前在 `App.axaml.cs:24` 显式 `ArcThemeManager.Apply(this, Light, Neutral)`；`ArcThemeManager.Apply(Application app, …)` 要求外部传入 Application，并替换 `app.Resources.MergedDictionaries`（A 报告 §2.3）。
- 如果 `<arc:ArcTheme/>` 只负责"装 Fluent + 字典"，则**默认 palette 必须在入口内部完成**；运行期换 accent/明暗仍需要 API（不能"零代码"）。
- 需要设计：`ArcTheme.ApplyScheme/ApplyAccent`（或静态 `ArcThemeManager` 保持现有签名），并明确与 `Style.Resources`/`Application.Resources` 的所有权；否则会出现"token 在 Application.Resources、主题覆盖在 Styles.Resources、两个宿主分别通知"的同步问题。

**建议**：在 ArcTheme 入口里默认调用一次 `Apply(Light, Neutral)`（构造或 attach 时），把换肤 API 保留为显式调用；文档把"一行接入"定义为"默认主题 + 组件主题聚合"，不承诺"零代码换肤"。

**人日增量**：**+1**（入口生命周期 + 换肤 API + POC，已部分计入 P1-2 的 0.5）。

---

## 3. P2 级发现（表述/口径/引用，不直接加人日但会误导决策）

| # | v3 表述 | 复算/事实 | 建议 |
|---|---|---|---|
| P2-1 | §0.3 "源码 **774 个** `.cs`/`.axaml`，**182,621 行**" | 774/182,621 = **`src` + `tests` + `.recon`（Probe/NavPerf 试验工程）** 的"非空行"口径；`src`-only = **763 文件 / 181,231 非空行 / 206,968 物理行** | 基线表标注口径；试验工程不计入"源码" |
| P2-2 | §0.2/§D1 "**131** ControlTheme" | 复算 **137**（与 §0.3 的 137 一致）；131 为旧数/B 口径 | 全篇统一为 137，并注明 112 Arc 类型 + 25 内建 keyed |
| P2-3 | §2.5 "Fluent **80 个 ControlTheme** XAML / 78 个内建控件" | 我复算：Fluent `Controls/` = **80 个 xaml 文件**（540,657 B）；其中 `FluentControls.xaml` 是聚合 Styles，`DateTimePickerShared` 等是共享文件，**不等于 80 个 ControlTheme**；"78 个控件"来自 D 报告，我未离线复算（`未验证`） | 改为"80 个主题 xaml（约 76–78 个 ControlTheme）"，并注明 78 未验证 |
| P2-4 | §0.3 "328 个页面类（294 ArcItemPage）" | 当前树：`ArcItemPage` 294 + `ArcPage` 22 = **316** 个页面类；`Docs/*.json` = **294**（v3 写 298） | 用页面注册数或注明"含 328 页注册/298 文档"的来源；重建基线 |
| P2-5 | §0.3 "25/29 个项目 ProjectReference Demo" | 当前：**25 个 src 项目**（含 Gallery）+ **tests 1 个** = 26 个 csproj；总项目 29 | 写"25 个 src 项目（26 含测试）/29"或分母改为 28 |
| P2-6 | §1.2 "23 条非 UI 断言" | 23 = `Check(` **调用点**；Lead 的 `VERIFY checks=34` 是**执行数**（循环展开，如 7 条 easing × 2） | 改为"23 个断言调用点 / 34 次执行" |
| P2-7 | §1.2 "312 处 SetName / 18 处 `Children.Clear` / 200 处 `OnApplyTemplate` / 143 个 CLR event" | 当前树复算：`AutomationProperties.SetName` **495**、`.Children.Clear()` **193**、`OnApplyTemplate` **210**、`public event` **240**（范围不同：含/不含 Pages） | 标注统计范围（Controls only vs 全 src）；不要作为验收基线 |
| P2-8 | §1.3 "`ArcTheme.axaml` 只做 Window/TextBlock 全局样式" | 实际 117 行：Window/TextBlock/Button TextBlock 三个全局样式 + 10 个类型尺度 class + 合并 3 个组件 ControlTheme 字典（`ArcTheme.axaml:4-12,14-114`） | 改为"全局样式 + 类型尺度 + Foundation 组件主题字典" |

> 补充（不单列 P 级）：v3 §1.2 说"`ItemsControl`/`DataTemplate` 使用 0 处"，当前实际 `ItemsSource` 出现在 5 个文件、`ItemsControl` 元素 1 个（EmojiPicker）；建议改为"仅 ArcVirtualList/Emoji/TimePicker 使用 ItemsSource，绝大多数列表仍是代码组合"。

---

## 4. 回答 Lead 的 5 个必查项（结论+证据，不复述文档）

### Q1：把"保现状 23 / 框架级 48"压成"10 人日主题适配"，是否低估？

**结论：低估。** 10 人日只能算"乐观下界"；按 v3 自己定义的"定向覆盖 + 不重写模板"，中性应为 **15 人日（10–23）**；若要求与"替代 AtomUI"匹配的裸控件视觉一致性，则需 **Top 15–20 覆盖 +26** 或明确文档化例外。

- 逐项清单与估算见 **P0-2 表**（合计 10/15/23）。
- 覆盖清单：Fluent accent 8×2（Palettes）、全局 font/size/radius（3 键）、TextBox（17 个无 Theme 实例 + Gallery 裸框）、ScrollViewer/ScrollBar（10 axaml + 28 cs）、ListBox/ListBoxItem（3 axaml/7 元素）、Popup/Flyout/ToolTip（7 axaml/29 文件）、Window 背景、焦点/验证、双主题核对、键注册表/CI、ThemeDictionaries POC。
- 依据：Fluent TextBox/ScrollBar 主题的键可覆盖颜色/尺寸（官方源码），但 Light/Dark 需要 `ThemeDictionaries`；Fluent 的焦点边框 vs Arc 焦点环无法靠键覆盖；裸控件的外观仍是 Fluent 几何。
- **建议**：G1 的"主题适配 10"→**15**（区间 10–23），G1 总 18→**25**；M1 71→**94**（含 P0-3 与 G2 增量，见 §5）。

### Q2：`<arc:ArcTheme/>` 一行接入是否可行？最小 POC 步骤？

**结论：可行（在 `Styles` 上），但 v3 的"Styles/ResourceDictionary 嵌套"表述需修正；不同变体/资源优先级需 POC 验证。**

机制依据（Avalonia 12.1.4 源码）：
- `FluentTheme` 本身是 `public class FluentTheme : Styles, IResourceNode`（官方 `FluentTheme.xaml.cs`）；`Styles` 实现 `IAvaloniaList<IStyle>, IStyle, IResourceProvider`（`Avalonia.Base/Styling/Styles.cs`）。因此 **`<Styles>` 的子项可以是另一个 `Styles`（含 FluentTheme）；`<Application.Styles><arc:ArcTheme/></Application.Styles>` 在 `ArcTheme : Styles` 时合法**。
- `ResourceDictionary` 不能承载 `FluentTheme`（它不是 `Resource`/`IStyle`）；且 `Styles` 的校验器**明确禁止 ControlTheme 直接作为 Styles 子项**（`Styles.cs` 的 `IAvaloniaListItemValidator`），ControlTheme 必须放 `Resources`/MergedDictionaries。→ v3 的"自定义 Styles/ResourceDictionary 里嵌套 `<FluentTheme/>`"统一改为"自定义 `Styles` 内嵌套 `<FluentTheme/>`"。
- 覆盖优先级：`Styles.TryGetResource` 先查自身 `Resources`，再逆序查子 Styles；因此 **Arc token 覆盖放在 ArcTheme 自己的 `Resources` 就会压过子 FluentTheme**。
- `FluentTheme.Palettes`（`IDictionary<ThemeVariant, ColorPaletteResources>`）+ `ColorPaletteResources.Accent` 是官方支持的 accent 定制入口，优于裸 `SystemAccentColor` 键。

**最小 POC（不写进仓库）**
1. 在临时目录新建 `ArcThemeProbe`（引用 `Avalonia`、`Avalonia.Themes.Fluent`、`Avalonia.Fonts.Inter`）；不提交仓库。
2. 写两个变体：
   - A：`class ArcTheme : Styles`，构造函数依次 `Resources.MergedDictionaries.Add(覆盖字典)` → `Add(new FluentTheme())` → `Add(new StyleInclude(...){Source=avares://Arc.Avalonia/Theme/ArcTheme.axaml})` → 11 个组件主题 StyleInclude；
   - B：`ArcTheme.axaml` 根 `<Styles>`：`<Styles.Resources>`（token + Fluent 覆盖）→ `<FluentTheme/>` → `<StyleInclude>` ×N；消费者用 `<StyleInclude Source="avares://Arc.Avalonia.Theme/Themes/ArcTheme.axaml"/>` 一行。
3. App.axaml 改 `<Application.Styles><arc:ArcTheme/></Application.Styles>`；跑一个包含 `Window`、裸 `TextBox`、`ArcButton`、`ArcSelect`（Popup）、ToolTip 的测试页。
4. 断言（逐条记录结果）：
   - 裸 `TextBox`/`Button`/`ScrollBar` 用 Arc token 颜色、字体、圆角、高度；
   - Light/Dark 切换后覆盖仍生效（ThemeDictionaries）；
   - 8 个 accent 通过 `FluentTheme.Palettes` 生效；
   - `ArcButton` 等组件仍能找到自己的 ControlTheme；
   - `Application.RequestedThemeVariant` 与 `ActualThemeVariant` 一致；
   - 组件主题 StyleInclude 在"只装 Theme 包、不装 Inputs 包"时失败（证明 P0-1）；
   - 设计器/编译绑定/热重载无异常。
5. 失败模式验证：同一 `FluentTheme` 实例不能挂两个 App（单 Owner）；`Styles.Resources` 动态换肤后通知是否刷新；`ArcThemeManager.Apply` 与入口的默认 palette 是否重复/覆盖。

**风险**：`avares://` 解析是否要求程序集已加载（`未验证`，决定 P0-1 方案 2 是否可行）；`FluentTheme` 构造依赖 `AvaloniaXamlLoader.Load(sp, this)`，由 XAML 实例化 vs `new` 创建的 SP 行为需验证。

### Q3：包拓扑（L1 包 ID=`Arc.Avalonia.Foundation`、程序集=`Arc.Avalonia`、元包 ID=`Arc.Avalonia`）是否可行？更稳妥替代？

**结论：技术上可行（NuGet 允许 PackageId≠AssemblyName），但不推荐作为唯一方案；v3 缺三项机制（显式 PackageId、元包无 lib、Theme 层归属）。**

- `avares://Arc.Avalonia/` 仅 **4 处/2 文件**（Gallery `:8`；`ArcTheme.axaml:7-9`），`avares://Arc.Avalonia.Internal/` 3 处/2 文件；因此"为保住 avares URI 而保留程序集名"的收益只有 4 处编辑，**不足以支撑一个长期歧义**。
- 若 Foundation 不显式设 `<PackageId>`，默认会产出 `Arc.Avalonia` 包，与元包撞名；必须显式设置。
- 推荐 **C**（L1 包/程序集都叫 `Arc.Avalonia.Foundation`，元包 `Arc.Avalonia`，Theme 独立 L2.5；一次改 4+3 处 URI，成本约 1 天、发布前无外部兼容负担），或 **B**（L1 保持 `Arc.Avalonia`，元包叫 `Arc.Avalonia.All`，零改名，最稳）。
- 三个方案对比表见 **P1-3**；无论哪个方案都要加：包 ID/程序集名 CI 断言、元包 `IncludeBuildOutput=false`、发布顺序（L1→L2→Theme→Agent→meta）。
- 如果保留 v3 的 A 方案，**必须**在包 README/发布说明里解释"安装 `Arc.Avalonia` 得到的是元包，程序集 `Arc.Avalonia` 来自 Foundation 包"，否则用户/工具链会长期困惑。

### Q4：裸内建控件外观是否与"替代 AtomUI 类组件库"矛盾？三策略与代价

**结论：矛盾确实存在。** AtomUI/Semi/Ursa 的卖点之一是"安装后所有内建控件都换成该设计体系"；v3 的 10 人日方案只覆盖 Arc 表面，消费者裸用 TextBox/ComboBox/Window 会得到 Fluent 外观（颜色可被 token 覆盖，但几何/焦点/弹层形态仍是 Fluent）。三个可选策略：

| 策略 | 内容 | 人日（中性） | 风险 | 建议 |
|---|---|---:|---|---|
| **S1 接受并文档化** | 只覆盖 Arc 表面；README/文档明确"裸内建控件为 Fluent 外观，请使用 Arc 控件以获得统一视觉"；M1 验收不含裸控件 | 0（在 15 内） | 与 AtomUI 叙事不一致；用户首屏可能放大裸控件而"不像 Arc" | 若定位收窄为"Arc 设计体系组件库（Fluent 底座）"可接受 |
| **S2 覆盖 Top 15–20** | Window、TextBox、Button、ComboBox、CheckBox、RadioButton、ToggleSwitch、Slider、ProgressBar、ListBox/ListBoxItem、ScrollViewer/ScrollBar、TabControl/TabItem、TreeView/TreeViewItem、Menu/MenuItem、ToolTip、Popup/FlyoutPresenter、Expander、GroupBox、Label/Separator | **+26**（每个 1–2 d，含模板局部覆盖 + 双主题验证） | Fluent 内部键/模板升级跟随；设计与 Fluent 交互细节冲突；需 2 人并行 | **推荐 M2/M3 实施**，作为"框架可信度"投资 |
| **S3 全量覆盖** | 等价 Fluent 的 ~78 控件（A 报告框架级 48 只覆盖 ≈43；全量 ≥65，区间 48–90） | **+65**（中性；A 报告 48 是 43 控件口径，不能直接引用） | 长期维护成本、与 v3 "不自建主题"口径冲突 | 仅在推出 `SelfContained` 独立产品线时做 |

**说明**：S2 的 26 人日与 P0-2 的 15 不重复（15 覆盖 Arc 表面；S2 增补裸控件）。若选 S1，必须在 v3 §0.2/§9 的"替代 AtomUI"叙事中加一句限定，否则评审/用户会按 AtomUI 的预期验收。

### Q5：A 报告事实是否被正确引用？数字不一致清单

**结论：核心事实引用基本正确，但存在 8 处口径/数字问题，其中 2 处会实质影响估算。**

正确引用（核对通过）：
- Foundation **18 文件 / 2,294 行（不含 csproj；含 csproj 为 2,305）**（§0.2 `:50`）；
- **82 token，81 注册，`ControlThumbShadow` 漏注册**（§1.2 `:152`；A 报告 §2.3）；
- `ArcResources` **5 处 `Application.Current`**、`ArcVirtualList` **296 行**、`ArcUi` **191 行**、**23 个测试断言调用点**（§1.2 `:151-155`）；
- **137 ControlTheme（112 Arc + 25 内建 keyed）**（§0.3 `:65`）；
- **2 处 `<FluentTheme />`**（§0.3 `:676`；A 报告一致）；
- Fluent **716 KB DLL**（§2.5 `:278`；A 报告 716.0 KB）。

不一致/需修正：

| # | v3 | 复算 | 级别 |
|---|---|---|---|
| 1 | §2.5 `:279` "自研等价主题 48 人日" | A 报告 48 = 框架级 ≈43 控件；Fluent 等价 ≈78 控件应更高 | P1（影响是否自建决策） |
| 2 | §2.5 `:293` "框架级 48 + Tier3 25" | 48 已含 Tier3 25；应写 23 + 25 = 48 | P1（双计） |
| 3 | §0.2/§D1 "131 ControlTheme" | 137（§0.3 自己也是 137） | P2（同文矛盾） |
| 4 | §0.3 "774 文件 / 182,621 行" | src-only 763 / 181,231 非空 / 206,968 物理；774/182,621 含 `.recon` + tests，且为非空行 | P2（基线口径） |
| 5 | §0.3 "328 页面类 / 298 Docs" | 316（294 + 22）；Docs 294 | P2（基线不准） |
| 6 | §0.3 "25/29 Demo 引用" | 25 个 src 项目 + tests = 26 个 csproj / 29 | P2 |
| 7 | §1.2 "23 条断言" vs §0.3/§8 "checks=34" | 两者都真：23 是调用点、34 是执行数；需标口径 | P2 |
| 8 | §2.3/§8 "13 条 StyleInclude / 1+14 行" | 当前 26 个 StyleInclude 元素；L1+L2 元包 14、All 25 | P1（验收标准） |
| 9 | §1.3 "ArcTheme.axaml 只做 Window/TextBlock" | 117 行，含类型尺度 + Button TextBlock + 3 个组件字典 | P2 |
| 10 | §2.5 "80 个 ControlTheme / 78 控件" | 80 个 xaml 文件（含聚合/共享文件）；78 未验证 | P2 |

> 另有 §1.2 的 UI 契约数字（312 SetName/18 Children.Clear/200 OnApplyTemplate/143 event）我复算为 495/193/210/240（范围未注明），建议由 B 报告给出统计范围后再引用，不作为 v3 的基线数字。

---

## 5. 修订后的人日建议与 WBS 增量

### 5.1 增量表

| WBS | v3 | 建议 | Δ | 说明 |
|---|---:|---:|---:|---|
| G0 治理与护栏 | 16 | **18** | +2 | Theme POC + API/包 ID 门禁 + Fluent 键覆盖测试 |
| G1 基础层与主题适配 | 18 | **25** | +7 | 基础拆分 5 + 主题覆盖 15 + ArcTheme 入口 4 + 门禁 1 |
| **G1b Internal 解散（新增）** | — | **5** | +5 | 12 个 ProjectReference、4 公开类 + 7 internal 助手、3 个主题字典、Gallery 入口 |
| G2 文档/发布工程 | 37 | **46** | +9 | 原 37 + samples 5 + PublicAPI 基线 3 + LICENSE/NOTICE 1 |
| G3a–G3f | 162 | 162 | 0 | 不在本评审范围；若选 S2，另在 G3 增 26 |
| G4 Agent | 104 | 104 | 0 | 不在本评审范围 |
| G5 迁移收尾 | 20 | 20 | 0 | — |
| **M3 合计** | **357** | **380** | **+23** | 区间建议 300–496 |
| M1 合计（G0+G1+G1b+G2） | 71 | **94** | +23 | 若 samples 推迟到 M2：M1 ≈ 89 |
| M2 累计 | 225 | ≈248 | +23 | 同上 |
| 若采纳 S2（Top15-20 内建控件覆盖） | — | **M3 ≈ 406** | +26 | M2/M3 实施，不影响 M1 |

### 5.2 建议的修订优先级

1. **M0 定稿前**：P0-1（Theme 层级）、P0-3（Internal 归属）、P1-1（命名）、P1-6（人日口径）——都是"写文档/拍决策"零成本或低成本项。
2. **M1 前**：P0-2 的 ThemeDictionaries/Palette POC、P1-2（入口数量）、P1-3（打包机制）、P1-4（覆盖键注册表）、P1-5（samples/API/LICENSE 立项）。
3. **M1 内**：G1b Internal 解散必须在第一次 `dotnet pack` 前完成，否则 pack 依赖不完整。
4. **M2/M3**：决定 S1/S2/S3 的裸控件策略；若 S2，把 26 人日写进 G3。

---

## 6. 附录 A：证据索引

### 6.1 当前工作树复算（2026-11-05）

| 指标 | 值 | 命令/口径 |
|---|---|---|
| 项目数 | 29（28 src + 1 tests；`.recon/Probe`/`NavPerf` 不在 slnx） | `ArcUI.slnx` |
| 源文件/行（src-only） | 763 文件 / **181,231 非空行** / **206,968 物理行** | `Get-ChildItem src -Include *.cs,*.axaml`，排除 bin/obj |
| src+tests+.recon | **774 文件 / 182,621 非空行 / 208,558 物理行** | 与 v3 的 774/182,621 完全吻合 → v3 基线含 `.recon` 试验工程 |
| Foundation | **18 文件 / 2,294 行**（不含 csproj；含 csproj 2,305）/ 82 token / 161 图标 / 6 控件 + ArcUi | `src/Foundation/Arc.Avalonia` |
| Internal | 10 文件 / 1,662 行；7 个 internal 类型 + 4 个公开视觉类 | `src/Foundation/Arc.Avalonia.Internal` |
| ControlTheme | **137**（复算）；v3 §0.3 137、§0.2/D1 131 | grep `<ControlTheme` |
| StyleInclude | **26 元素 / 25 行**；v3 的 13 = `Gallery/App.axaml:8-20` | `src/Apps/Arc.Avalonia.Gallery/App.axaml:6-33` |
| `avares://Arc.Avalonia/` | **4 处 / 2 文件**（`Gallery/App.axaml:8`；`ArcTheme.axaml:7-9`） | grep |
| `avares://Arc.Avalonia.Internal/` | 3 处 / 2 文件（`Gallery/App.axaml:9`；`ArcInternalTheme.axaml:6-7`） | grep |
| 测试断言 | 23 个 `Check(` 调用点；Lead 实测执行 34 checks | `tests/Arc.Avalonia.Tests/Program.cs` |
| 页面类 | `ArcItemPage` 294 + `ArcPage` 22 = **316** | grep |
| Docs JSON | **294**（`src/**/Docs/*.json`） | 复算 |
| Demo 引用 | 25 个 src 项目 + tests = 26 个 csproj | grep csproj |
| PublicApiAnalyzers / samples / LICENSE | 0 / 不存在 / 不存在 | 复算 |
| Internal 的 ProjectReference | **12 个 csproj** | Gallery + 6 Blocks + 5 Components |
| Fluent 主题 | 80 个 xaml / 540,657 B / DLL 716.0 KB | 本地 NuGet + GitHub 12.1.4 |
| Fluent 定制 API | `FluentTheme.Palettes`、`ColorPaletteResources.Accent`、`DensityStyle` | 官方 `FluentTheme.xaml.cs` / `ColorPaletteResources.Properties.cs` |

### 6.2 v3 原文行号

| 位置 | 内容 |
|---|---|
| `:57-71` | §0.3 基线数字（774/182,621、328 页、298 Docs、25/29、137 ControlTheme、2 FluentTheme） |
| `:133-135` | "26 个库项目只引 Avalonia 官方包；Fluent 只在 Gallery" |
| `:137-155` | §1.2 缺口（8 项）+ 4 个基础层缺陷（82/81 token、5 Application.Current、296/191、23 断言） |
| `:160` | "ArcTheme.axaml 只做 Window/TextBlock 全局样式" |
| `:177` | "内建控件模板与 Fluent 主题（80 个 ControlTheme）" |
| `:193-199` | §2.1 分层（L1 允许 L0、禁止 L2+） |
| `:220-230` | §2.2 七条硬规则（R1 单向、R4 主题只叠加） |
| `:235-264` | §2.3 包拓扑（Theme 在 L1、聚合组件主题、一行接入、13 StyleInclude） |
| `:278-281` | §2.5 主题不单列理由（80 个 ControlTheme、78 控件、48 人日、10 人日适配范围） |
| `:293-294` | "回到 v1 方案（保现状 23、框架级 48 + Tier3 25）" |
| `:317-327` | D3 主题决策（保留 Fluent、定向覆盖 5 类控件、不重写模板） |
| `:343-353` | D5 基础层纯度（ArcVirtualList/ArcUi/ArcResources/Inter、5 人日） |
| `:439-442` | D10 承诺（LICENSE/NOTICE、PublicApiAnalyzers、samples） |
| `:462-475` | WBS（G0 16 / G1 18 / G2 37 / G3 162 / G4 104 / G5 20 = 357） |
| `:478-480` | 与四份审计对齐（A 的 23/48 只取 10） |
| `:524-533` | §4.6 官方附加控件取舍 |

### 6.3 官方源码（Avalonia 12.1.4）

- `FluentTheme : Styles, IResourceNode` + `Palettes`：`https://raw.githubusercontent.com/AvaloniaUI/Avalonia/refs/heads/release/12.1.4/src/Avalonia.Themes.Fluent/FluentTheme.xaml.cs`
- `FlUENTTheme.xaml`（`<Styles>` 聚合、MergeResourceInclude、StyleInclude）：`.../FluentTheme.xaml`
- `Styles : AvaloniaObject, IAvaloniaList<IStyle>, IStyle, IResourceProvider`；`TryGetResource` 先 Resources 后逆序子项；校验器禁止 ControlTheme 作为子项：`https://raw.githubusercontent.com/AvaloniaUI/Avalonia/refs/heads/release/12.1.4/src/Avalonia.Base/Styling/Styles.cs`
- `ColorPaletteResources`（`Accent`/系统色）：`.../Accents/ColorPaletteResources.Properties.cs`
- `TextBox.xaml`（约 20 个 `TextControl*` 键、`FocusAdorner={x:Null}`、模板部件）：`.../Controls/TextBox.xaml`
- `ScrollBar.xaml`（`ScrollBarSize`/Thumb/Track/Button 键、模板结构）：`.../Controls/ScrollBar.xaml`

---

## 7. 附录 B：POC / 门禁 checklist（供 Lead 执行，均不写入仓库）

| # | 检查 | 通过标准 |
|---|---|---|
| 1 | `ArcTheme : Styles` 一行接入 | `<Application.Styles><arc:ArcTheme/></Application.Styles>` 可启动；裸 TextBox/Button/ScrollViewer 渲染 |
| 2 | `<Styles>` 文件嵌套 `<FluentTheme/>` + StyleInclude 版本 | StyleInclude 一行接入可启动；组件 ControlTheme 全部命中 |
| 3 | ThemeDictionaries 双主题覆盖 | Light/Dark 下 5 个目标控件的背景/边框/文字/圆角/尺寸都来自 Arc token |
| 4 | `FluentTheme.Palettes` 8 accent | 切换 8 accent，内建控件（TextBox 焦点、Button、ScrollBar）与 Arc 组件同步变化 |
| 5 | 组件主题分离 | 只装 Theme 包、不装 Inputs 包时，Inputs 主题缺失应显式报错（验证 P0-1 结论） |
| 6 | `dotnet pack` 包图 | 包 ID 唯一；元包无 lib；Foundation 的 PackageId 正确；组件包不依赖未发布 Internal |
| 7 | 覆盖键升级检测 | 键清单 + `grep` 门禁在 Fluent 升级后可发现缺失键 |
| 8 | 裸控件策略 | S1 文档化 / S2 Top15 / S3 全量，三者之一写进 v3 验收 |

---

## 8. 结论

1. **v3 的总体方向正确**：官方允许、不自建主题、基础层薄、Agent 顶层；这些我不反对。
2. **但有 3 个 P0 必须在 M1 前解决**：Theme 包与 L1/L2 的层级倒置；10 人日主题适配的乐观口径（中性 15，且裸控件仍是 Fluent）；Internal 解散未立项且会卡打包。
3. **2 个 P1 会直接改变验收**：一行接入的覆盖数（26 条 vs 13/14/25）与 PackageId/AssemblyName 的打包机制。
4. **数字层面**：A 报告的核心事实引用基本正确；`48 + Tier3 25` 的双计、131 vs 137、`774/182,621` 的 `.recon`/非空行口径、328/298/23 等需要统一基线表。
5. **建议最小修订**：M1 **71→94**（+23），M3 **357→380**（+23）；若采纳 Top 15–20 裸控件覆盖，M3 **≈406**（+26）。若只修 P0 不补 samples/API/LICENSE，M1 ≈85、M3 ≈371，但仍存在 P1 的验收失真。

> 本评审所有结论均可按附录 A 复算；`未验证` 项仅涉及运行时行为，未在本轮构建/运行。
