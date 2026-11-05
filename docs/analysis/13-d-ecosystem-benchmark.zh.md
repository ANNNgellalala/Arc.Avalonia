# D. 生态对标：自包含 Avalonia UI 框架的包形态与能力清单

> **任务**：task-4（13-D，生态对标）｜**调研员**：ecosystem-benchmark｜**日期**：2026-10-31
> **仓库**：`D:\Code\VibeCoding\Arc.Avalonia`（Avalonia 12.1.4 / .NET 10 / `net10.0`，26 个库项目 + 2 个应用 + 1 个测试控制台，见 [R1][R2]）
> **方法**：① 只读本地 NuGet 缓存实测（`D:\.nuget\packages`，nuspec/lib/资源字符串）；② 仓库静态检查（read/grep/glob + 只读 pwsh 统计）；③ 联网核实 Avalonia 官方文档与生态仓库 README。
> **约束**：未运行 `dotnet build` / `dotnet run` / `tools/*.ps1`；未修改任何源码/csproj/其它文档；唯一产出为本文件。
> **证据等级**：`A` = 本地实物或官方原文；`B` = 由 A 推导（标注推导规则）；`C` = 未验证/推断，不得作为决策硬依据。全文用 `[L*]`、`[R*]`、`[W*]` 引用 §6 证据索引；正文中标 **未验证** 的即 C 级。

---

## 0. 一页结论

**① 包形态结论（一句话）**：业界"自包含 Avalonia 框架"的通行形态是 **内核（Avalonia 本体）＋ 单列主题包 ＋ 可选字体/附加控件包**；Arc 目前**库层已经满足"零外部 UI 依赖"**（26 个库项目中 25 个只引 Avalonia，`Arc.Avalonia` 额外引 Inter，[R3][R9]），缺的是**内建控件的基础主题、字体内置、打包元数据**三件事，而不是再拆主题包。

**② 边界判定（一句话）**：`Avalonia` / `Avalonia.Skia` / `Avalonia.Desktop`（Win32/X11/Native 平台后端）= **框架内核，不算"其它 UI 库"**；`Avalonia.Themes.Fluent` / `Simple` = **主题层，必须替换**；`Avalonia.Fonts.Inter` / `Avalonia.Controls.ColorPicker` / `Avalonia.Controls.DataGrid` / `SkiaSharp` = **可选增值（字体资产 / 官方附加控件 / 渲染引擎），核心包不应依赖**（见 §4）。

**③ 最关键的 3 个外部数字**
- 官方 Fluent 12.1.4 的 `Controls/` 目录有 **80 个主题 XAML**（含 2 个共享/聚合文件；含 `Window.xaml` 1,835 B 与 v12 新增的 `WindowDrawnDecorations.xaml` 11,521 B），Simple 12.1.4 为 **79 个**；主题 DLL 分别 **716.0 KB / 405.0 KB**。[W8][W9][L1][L2]
- Semi.Avalonia 主主题包 **1,696 KB**，其附加控件主题 `Semi.Avalonia.DataGrid` 仅 **68.5 KB**，且 **不依赖** Semi 主包（只依赖 `Avalonia.Controls.DataGrid`）——"主题 × 附加控件"是独立包矩阵。[L3][L4]
- 官方附加控件包**自带两套内建主题**：`Avalonia.Controls.ColorPicker.dll` 内嵌 10 个 `avares://Avalonia.Controls.ColorPicker/Themes/{Fluent,Simple}/*.xaml`；`Avalonia.Controls.DataGrid.dll` 内也有 `Themes/Fluent`、`Themes/Simple` 字符串。**任何第三方主题都必须为附加控件单独出主题包**。[L7][L8]

**④ 最关键的 3 个仓库数字**
- Arc 现有 **137 个 ControlTheme**（分布在 116 个 `Themes/*.axaml` 中，含各项目主题入口文件），但全部面向自有控件；`TargetType` 为 Avalonia 内建控件的只有 **6 类**（`Button`/`ContentControl`/`ListBox`/`ListBoxItem`/`RadioButton`/`TextBox`，且多为内部辅助键）。[R10]
- 全仓库唯一使用 `FluentTheme` 的是 Gallery：`src/Apps/Arc.Avalonia.Gallery/App.axaml:7`（其后 25 个 `StyleInclude`，行 8–32）。[R7]
- 文档/验收资产：**294 个 Docs JSON、318 个 `*Page.cs`、294/294 条目文档、233×2 张截图**；但测试只有一个控制台 `Verify`（无 xUnit/Headless）。[R16][R19][R13]

**⑤ 建议包拓扑（详见 §5）**

```text
Avalonia 12.1.4（内核：Avalonia + Skia/HarfBuzz + Desktop 平台包）
└─ Arc.Avalonia                     核心：82 token + 主题引擎 + 动效/图标 + 基础控件
   │                                ＋ 内建控件基础主题（含 Window/WindowDrawnDecorations）
   │                                ＋ 内置 Inter 字体（去掉 Avalonia.Fonts.Inter 依赖）
   ├─ Arc.Avalonia.{Inputs,Actions,Navigation,Overlays,Feedback,DataDisplay,
   │                  Charts,Pickers,Effects,Content,Media}   11 个组件族（仅依赖核心）
   │   └─ Arc.Avalonia.{Blocks.*}     6 个产品块（依赖对应组件族）
   ├─ Arc.Avalonia.{Special.*}       5 个特效包（依赖核心；AI 两包可被 Agent 依赖）
   ├─ Arc.Avalonia.Agent            顶层组合（吸收 AgentUI + LifeToday + Blocks.AI）
   └─ Arc.Avalonia.All              元包（一键全量；不含 Demo/Gallery）
```

**⑥ 最大风险**：基础主题是**量级最大的一项**——外部锚点是 ~78 个内建控件（80 个 XAML 去掉 2 个共享/辅助文件），不是 5–10 个；Arc 现有自绘控件主题不能平移到内建控件上（见 §4.3.1）。其次是本地化/RTL（294 条目文案 + 多处硬编码 `FlowDirection.LeftToRight`，[R11]）与测试护栏（无 Headless）。

---

## 1. 本地证据：包形态实测（`D:\.nuget\packages`）

### 1.1 方法与口径

- 只读列目录、读 `.nuspec`、读 `.dll` 内可读字符串（不反编译、不加载程序集）；临时文件未写入工作区。
- 所有大小为 `net10.0` TFM 的 `lib` 产物（`net8.0` 同尺寸，见证据索引）；KB 为 1 KB = 1024 B 的四舍五入。
- "主题层是否单列成包"以 **包 ID / DLL 内容 / nuspec 依赖** 三者交叉判断，不依赖作者口头说明。

### 1.2 主题 / 附加控件 / 字体 / 平台包实测

| 包 | 缓存版本 | lib TFM | 主 DLL 与大小 | nuspec 直接依赖 | 形态判定 | 等级 |
|---|---|---|---|---|---|---|
| Avalonia.Themes.Fluent | 12.1.4 | net8.0, net10.0 | `Avalonia.Themes.Fluent.dll` **716.0 KB**（xml 6.9 KB） | `Avalonia 12.1.4` | 官方主题，单列单包 | A [L1] |
| Avalonia.Themes.Simple | 12.1.0 | net8.0, net10.0 | `Avalonia.Themes.Simple.dll` **405.0 KB**（xml 0.5 KB） | `Avalonia 12.1.0` | 官方主题，单列单包 | A [L2] |
| Semi.Avalonia | 12.1.0.1 | net8.0, net10.0 | `Semi.Avalonia.dll` **1696 KB** | `Avalonia 12.1.0` + `Irihi.Avalonia.Shared 0.5.0` | 第三方主题主包（不附控件） | A [L3][L5] |
| Semi.Avalonia.DataGrid | 12.1.0.1 | net8.0, net10.0 | `Semi.Avalonia.DataGrid.dll` **68.5 KB** | `Avalonia.Controls.DataGrid 12.1.0`（**不依赖 Semi 主包**） | 附加控件主题独立包 | A [L4] |
| FluentAvaloniaUI | 3.1.0 | net10.0 | `FluentAvalonia.dll` **3587.5 KB**（+pdb/xml） | `Avalonia` + `ColorPicker` + `DataGrid` + `Avalonia.Skia` + `MicroCom.Runtime` | 主题 + WinUI 控件单体包 | A [L6] |
| Avalonia.Controls.ColorPicker | 12.1.0 | net8.0, net10.0 | `Avalonia.Controls.ColorPicker.dll` **472.0 KB** | `Avalonia.Remote.Protocol` + `Avalonia` | 官方附加控件（自带两套主题） | A [L7] |
| Avalonia.Controls.DataGrid | 12.1.2 | net8.0, net10.0 | `Avalonia.Controls.DataGrid.dll` **358.5 KB** | `Avalonia 12.1.0` | 官方附加控件（自带两套主题） | A [L8] |
| Avalonia.Fonts.Inter | 12.1.4 | net8.0, net10.0 | `Avalonia.Fonts.Inter.dll` **1848.5 KB** | `Avalonia 12.1.4` | 字体资产包（预构建 `EmbeddedFontCollection`） | A [L9] |
| Avalonia.Desktop | 12.1.4 | net8.0, net10.0 | `Avalonia.Desktop.dll` **15.0 KB** | `Avalonia.Native`、`Avalonia.X11`、`Avalonia.HarfBuzz`、`Avalonia.Skia`、`Avalonia.Win32`（均 12.1.4） | 平台引导 **meta 包** | A [L10] |
| SukiUI | 7.0.1 | net8.0 | `SukiUI.dll` **1830.5 KB** | `Avalonia 12.0.3` + `ColorPicker` + `DataGrid` + `Avalonia.Skia` + **`Avalonia.Themes.Simple 12.0.3`** + `System.Linq.Dynamic.Core` | 单体 UI 库，**依赖内建 Simple 主题** | A [L12] |
| SkiaSharp | 3.119.4（缓存另有 4.153.1） | 多 TFM | 缓存 4.153.1 的 `lib\net10.0\SkiaSharp.dll` 547.3 KB；3.119.4 尺寸未测 | 由 `Avalonia.Skia 12.1.4` 传递引用 **3.119.4** | 渲染引擎依赖（非 UI 库） | A [L11] |
| ShimSkiaSharp | 4.1.0 | net461/net6/net8/net10/netstandard2.0 | `ShimSkiaSharp.dll` **190.5 KB** | 无依赖（nuspec 各 TFM 组为空） | 第三方 Svg.Skia 的 Skia 对象模型 shim；**Arc 未引用** | A [L13] |

**观察**：同一 DLL 在 net8.0 / net10.0 下大小一致（如 Fluent 均 716 KB），说明主题资源以资源形式内嵌、TFM 差异不改变体积；这印证"主题包 = DLL 里塞 XAML 资源"的形态。

### 1.2.1 `lib` 目录文件清单（原样列出，便于复核）

| 包 | `lib\` 下文件（相对路径；`net8.0\` 与 `net10.0\` 同名同尺寸时只写一次） | 备注 |
|---|---|---|
| Avalonia.Themes.Fluent 12.1.4 | `net10.0\Avalonia.Themes.Fluent.dll` 716.0 KB、`net10.0\Avalonia.Themes.Fluent.xml` 6.9 KB；`net8.0\` 同 | 无 `runtimes\`、无附属资源文件 |
| Avalonia.Themes.Simple 12.1.0 | `net10.0\Avalonia.Themes.Simple.dll` 405.0 KB、xml 0.5 KB；`net8.0\` 同 | 同上 |
| Avalonia.Fonts.Inter 12.1.4 | `net10.0\Avalonia.Fonts.Inter.dll` 1848.5 KB、xml 0.1 KB；`net8.0\` 同 | 字体数据内嵌在 DLL |
| Avalonia.Desktop 12.1.4 | `net10.0\Avalonia.Desktop.dll` 15.0 KB、xml 0.1 KB；`net8.0\` 同 | 纯 meta 壳 |
| Semi.Avalonia 12.1.0.1 | `net10.0\Semi.Avalonia.dll` 1696 KB；`net8.0\` 同 | 无 xml/pdb |
| FluentAvaloniaUI 3.1.0 | 仅 `net10.0\`：dll 3587.5 KB、pdb 829.2 KB、xml 473.5 KB | 无 net8.0 目标 |
| SukiUI 7.0.1 | 仅 `net8.0\`：dll 1830.5 KB、xml 92.5 KB | 无 net10.0 目标（net10 应用经兼容解析） |
| Avalonia.Controls.ColorPicker 12.1.0 | `net10.0\...dll` 472.0 KB、xml 91.5 KB；`net8.0\` 同 | — |
| Avalonia.Controls.DataGrid 12.1.2 | `net10.0\...dll` 358.5 KB、xml 266.5 KB；`net8.0\` 同 | — |
| Semi.Avalonia.DataGrid 12.1.0.1 | `net10.0\...dll` 68.5 KB；`net8.0\` 同 | 无 xml |

> 补充判据：对 Fluent / Simple 两个 DLL 做字符串扫描**未命中任何 `avares://` URI**（0 次），说明其 XAML 以编译资源内嵌、不以 URI 文本出现。因此"主题层是否单列成包"的判据以**包边界 + nuspec 依赖 + 体积 + 是否含控件 API** 为主，资源命名只作辅助。

### 1.3 nuspec 依赖树：主题包只依赖 Avalonia，附加主题包只依赖附加控件

```text
Avalonia 12.1.4 (meta) ──> Avalonia.Base/Controls/Markup.Xaml + Remote.Protocol + MicroCom.Runtime
Avalonia.Skia 12.1.4 ────> SkiaSharp 3.119.4 + HarfBuzzSharp 8.3.1.3 + Avalonia
Avalonia.Desktop 12.1.4 ─> Avalonia.Native + Avalonia.X11 + Avalonia.HarfBuzz + Avalonia.Skia + Avalonia.Win32

Avalonia.Themes.Fluent 12.1.4 ──> Avalonia 12.1.4                    （无其它主题依赖）
Avalonia.Themes.Simple 12.1.0 ──> Avalonia 12.1.0
Semi.Avalonia 12.1.0.1 ────────> Avalonia 12.1.0 + Irihi.Avalonia.Shared 0.5.0
Semi.Avalonia.DataGrid 12.1.0.1 ─> Avalonia.Controls.DataGrid 12.1.0  （不依赖 Semi.Avalonia）
FluentAvaloniaUI 3.1.0 ────────> Avalonia + ColorPicker + DataGrid + Avalonia.Skia + MicroCom.Runtime
Avalonia.Controls.ColorPicker 12.1.0 ─> Avalonia.Remote.Protocol + Avalonia
Avalonia.Controls.DataGrid 12.1.2 ────> Avalonia 12.1.0
Avalonia.Fonts.Inter 12.1.4 ──────────> Avalonia 12.1.4
SukiUI 7.0.1 ─────────────────────────> Avalonia + ColorPicker + DataGrid + Avalonia.Skia + Avalonia.Themes.Simple + System.Linq.Dynamic.Core
```

**结论（B 级推导）**：
1. **主题层在官方框架里是单列包**：`Themes.Fluent` / `Themes.Simple` 的 nuspec 只挂 `Avalonia`，DLL 内容只有主题类 + XAML 资源（xml 文档 6.9 KB / 0.5 KB，无控件 API）[L1][L2]。
2. **主题包之间不互相依赖**：Fluent 与 Simple 互不引用；Semi 不依赖内建主题；SukiUI 反过来**依赖** `Themes.Simple`（把 Simple 当底座）[L12]。
3. **附加控件 = 控件包 + 主题包两层，且没有 NuGet 硬连接**：`Semi.Avalonia.DataGrid` 只依赖 `Avalonia.Controls.DataGrid`，不依赖 `Semi.Avalonia` [L4]；这意味着"主主题 + 附加主题"要靠**用户同时安装 + 资源键约定**，不是编译期约束——业界已知的脆弱点。
4. **字体是独立资产包**：Inter 以 1.85 MB DLL 形态发布，只依赖 Avalonia [L9]。

### 1.4 官方内建主题的覆盖面：Fluent 80 / Simple 79 个主题 XAML

从 Avalonia 官方仓库 `release/12.1.4` 的目录清单统计（GitHub Contents API）：[W8][W9]

| 主题 | `src/Avalonia.Themes.*/Controls` XAML 数 | 是否含 Window | 是否含 WindowDrawnDecorations | DLL 大小 |
|---|---|---|---|---|
| Fluent 12.1.4 | **80**（含 `FluentControls.xaml`、`DateTimePickerShared.xaml` 等共享文件） | 是（`Window.xaml` 1,835 B） | 是（`WindowDrawnDecorations.xaml` 11,521 B） | 716.0 KB |
| Simple 12.1.4 | **79**（含共享文件） | 是（`Window.xaml` 1,760 B） | 是（`WindowDrawnDecorations.xaml` 11,747 B） | 405.0 KB（缓存为 12.1.0） |

**独立验证**：对两个主题 DLL 做字符串扫描，均命中 `WindowDrawnDecorations`（3 次）与 `TitleBar`（2 次）、`ControlTheme`（5 次）；两者都**不**命中 `DataGrid` / `ColorPicker`——即内建主题**不负责**附加控件外观。[L1][L2]

**推导（B 级）**：一个"能替换 Fluent 的头部主题"至少要为 **~78 个控件**（80 个 XAML 去掉 2 个非控件/共享文件，具体去重未逐个核对，属推断）提供 `ControlTheme`，且必须包含 `Window` 与 `WindowDrawnDecorations` 两个窗口装饰主题。这是 §4.3.1 工作量锚点的依据。

### 1.5 关键发现：官方附加控件包自带 Fluent + Simple 两套主题

| 包 | 实测字符串证据 | 含义 |
|---|---|---|
| `Avalonia.Controls.ColorPicker.dll` 12.1.0 | 10 个 `avares://Avalonia.Controls.ColorPicker/Themes/{Fluent,Simple}/{ColorPicker,ColorPreviewer,ColorSlider,ColorSpectrum,ColorView}.xaml` | 控件包内嵌**两套**内建主题资源 [L7] |
| `Avalonia.Controls.DataGrid.dll` 12.1.2 | `Themes/Fluent` ×3、`Themes/Simple` ×3、`DataGrid` ×134、`ControlTheme` ×5 | 同上（具体 XAML 清单**未验证**）[L8] |

**含义（B 级）**：附加控件的"主题适配"责任在**控件包自己**，但只认 Fluent/Simple；第三方主题（Semi/Ursa/Suki/Material）若要支持 DataGrid，必须另发 `*.*.DataGrid` 主题包——这解释了为什么 Semi 需要 6 个附加主题包（ColorPicker/DataGrid/TreeDataGrid/Dock/Tabalonia/AvaloniaEdit，[W10]）。

### 1.6 第三方框架包划分对照

| 框架 | 主包 | 附加/主题包 | 包数与依赖特征 | 证据 |
|---|---|---|---|---|
| Avalonia 官方 | `Avalonia`（核心控件）+ `Avalonia.Themes.Fluent`/`Simple`（主题单列） | `Avalonia.Controls.ColorPicker`/`DataGrid`（自带两套主题） | 主题与控件分层；附加控件主题内嵌 | [L1][L2][L7][L8] |
| Semi.Avalonia | `Semi.Avalonia`（主题，依赖 `Irihi.Avalonia.Shared`） | `Semi.Avalonia.ColorPicker`/`DataGrid`/`TreeDataGrid`/`Dock`/`Tabalonia`/`AvaloniaEdit` | **1 主 + N 附加主题**；`SemiTheme Locale="zh-CN"` 内建本地化 | [L3][L4][W10] |
| Ursa.Avalonia | `Irihi.Ursa`（企业级控件） | `Irihi.Ursa.Themes.Semi`（主题包）+ `Irihi.Ursa.PrismExtension`/`ReactiveUIExtension` | **控件与主题显式分离**（控件本身不带设计语言） | [W11] |
| Material.Avalonia | `Material.Avalonia`（主题 + 附加控件） | `Material.Avalonia.DataGrid` / `Material.Avalonia.Dialogs`；图标 `Material.Icons` 单独安装 | 1 主 + 2 附加；图标外置以减小体积 | [W12] |
| SukiUI | `SukiUI`（主题 + 控件 + Toast/Dialog 服务，单体） | —（无附加包） | **单体 1.83 MB**，依赖 `Avalonia.Themes.Simple` 当底座 | [L12][W13] |
| FluentAvalonia | `FluentAvaloniaUI`（主题 + WinUI 控件，单体） | —（内部依赖 ColorPicker/DataGrid） | **单体 3.59 MB**，依赖 5 个包 | [L6][W14] |

### 1.7 结论：业界"自包含主题框架"的包拆分惯例（5 条）

1. **主题与控件解耦、主题单列成包**——Avalonia / Semi / Ursa 都如此；单体（SukiUI / FluentAvaloniaUI）是例外，代价是 1.8–3.6 MB 单体 DLL 与更粗的版本耦合。
2. **主主题包只依赖 Avalonia**（Semi 额外依赖一个共享 UI 基础设施 `Irihi.Avalonia.Shared`），不依赖其它主题或控件库。
3. **附加控件主题跟随附加控件独立成包**，且与主主题**无 NuGet 硬依赖**（`Semi.Avalonia.DataGrid` 只依赖 `Avalonia.Controls.DataGrid`）。
4. **字体/图标等重资产独立成包**（`Avalonia.Fonts.Inter` 1.85 MB；`Material.Icons` 要求单装），避免核心包膨胀。
5. **按 Avalonia 版本对齐**：Semi 用四段版本 `12.1.0.1` 对齐 Avalonia 12.1.0，并维护兼容表（[W10]）；Avalonia 全家桶 lockstep `12.1.4`。

---

## 2. 联网核实：官方主题契约与生态（引用见 §6.3）

### 2.1 主题的官方定义与"必须提供哪些 ControlTheme"

- Avalonia 文档原文：**"themes are complete sets of control themes and theme resources for built-in controls"**（主题 = 内建控件 ControlTheme 的完整集合 + 主题资源）[W2]。
- **没有官方枚举的"必须实现的控件清单"**：官方文档只给出机制（`ControlTheme`、`TargetType`、`StyleKey`、`{x:Type}` 键）与两个内置主题的用法，不列出清单 [W1][W2]。工程上可用的权威清单就是主题源码里的 `Controls/*.xaml`（Fluent 80 / Simple 79）[W8][W9]。
- 控件主题的解析规则：优先用控件 `Theme` 属性；否则沿逻辑树向上查找 **key == 控件 StyleKey** 的 `ControlTheme` 资源；`{x:Type}` 键 → 对所有实例生效，字符串键 → 仅 `StaticResource` 显式指定的实例生效；**一个控件同一时刻只能应用一个 ControlTheme**。[W1]
- **对 Arc 的直接含义（B）**：Arc 若要"无 Fluent"，必须在 `Application.Resources` 或 `ArcTheme.axaml` 中为 ~78 个内建控件提供 `x:Key="{x:Type ...}"` 的 `ControlTheme`，并让自绘控件的 StyleKey 覆盖关系保持唯一。

### 2.2 ThemeVariant 机制与 `DynamicResource` 强约束

- 变体资源用 `ResourceDictionary.ThemeDictionaries`，键常用 `Light`/`Dark`，`Default` 为兜底；通过 `Application.RequestedThemeVariant`、`Window`/`TopLevel`、`ThemeVariantScope` 控制；`ActualThemeVariant` 只读继承 [W3]。
- **关键陷阱（官方 caution）**：`ThemeDictionaries` 里的资源**只能被 `DynamicResource` 解析**；`StaticResource` 找不到会抛异常（除非非 ThemeDictionaries 部分有同名资源）[W3]。
- **对 Arc 的直接含义**：Arc 现有实现是"`Application.Resources.MergedDictionaries` 合并 palette 字典 + 全部 `DynamicResource`"（`ArcTheme.axaml:15-23`、`ArcThemeManager.Apply`，[R5][R6]），与官方机制**天然兼容**；若改用 `ThemeDictionaries` 表达 Light/Dark，需要注意 `ArcTheme.axaml` 里没有任何 `StaticResource` 引用（现状核对：主题入口仅用 `DynamicResource`，见 [R6]）。

### 2.3 窗口装饰：Avalonia 12 的 `WindowDrawnDecorations`

- `Window` 是桌面 TopLevel，提供 `WindowDecorations`、`ExtendClientAreaToDecorationsHint` 等属性；`ExtendClientAreaToDecorationsHint=true` 时内容可延伸进标题栏，做自定义 chrome [W4]。
- **Avalonia 12 破坏性变更**：新增 `WindowDrawnDecorations`（命名空间 `Avalonia.Controls.Chrome`），**移除** `TitleBar`、`CaptionButtons`、`ChromeOverlayLayer`；`ExtendClientAreaChromeHints` 移除，改用 `WindowDecorations` + `ExtendClientAreaToDecorationsHint` [W5][W6]。
- `WindowDrawnDecorations` 是**逻辑元素**，不是视觉控件；模板分 underlay / overlay / fullscreen popover 三层，模板部件 `PART_CloseButton`/`PART_MinimizeButton`/`PART_MaximizeButton`/`PART_FullScreenButton` 可选，伪类 `:normal/:maximized/:minimized/:fullscreen/:has-shadow/:has-border/:has-titlebar`，并用 `WindowDecorationProperties.ElementRole`（TitleBar/Resize*/CloseButton…）做跨平台命中测试 [W5]。
- 平台差异：macOS 自行处理 resize grips 等（文档明言"可用部件随平台变化"）[W5]。
- **原生菜单/托盘是平台特性，不是主题特性**：`NativeMenu` 只在 macOS 与部分 Linux 发行版可用（v11 文档；v12 未找到等价页面，机制未验证）[W18]；`TrayIcon` 同属平台能力。Arc 全仓库 0 引用（`NativeMenu`/`TrayIcon`，[R17]）。这类能力的可用面由平台后端（`Avalonia.Desktop` 的 Win32/X11/Native，[L10]）决定，归入 §4.1 的①内核档，不构成主题层依赖。
- **对 Arc 的直接含义（B）**：Arc 的"自绘窗口壳"必须提供一个 `ControlTheme x:Key="{x:Type WindowDrawnDecorations}"`（不是简单 `Style Selector="Window"`），且按平台裁剪部件；Fluent/Simple 都内置了它（[L1][L2] 字符串证据 + [W8][W9] 文件证据）。

### 2.4 字体包形态与"内置 Inter"的可行路径

- Avalonia 支持三种自定义字体：静态资源（`FontFamily` 指向 `avares://.../Fonts#Family`，要求 `<AvaloniaResource Include="..."/>`）、`EmbeddedFontCollection`（注册自定义 scheme，如 `fonts:MyFonts#Family`）、**预构建字体包**（`Avalonia.Fonts.Inter` 就是此模式的范例，配 `.WithInterFont()`）[W7]。
- 字体 URI 必须带 `#字体家族名`（字体内部名，不是文件名），否则静默回退；WASM 下必须用完整 collection URI；变量字体目前不支持 [W7]。
- Avalonia 12 自带字体解析器，仅支持 TTF/OTF（Type1 被移除）[W6]。
- **对 Arc 的直接含义（A/B）**：Arc 现在是"Foundation 硬编码 `avares://Avalonia.Fonts.Inter/Assets#Inter`（`ArcPalette.cs:33`）+ Gallery `Program.cs:13 .WithInterFont()`" [R4][R8]。由于静态资源模式**不需要** `ConfigureFonts` 注册，替换成本极低：把 Inter 的 `.ttf` 作为 `AvaloniaResource` 放进 `Arc.Avalonia`，改常量即可（§4.3.2）。

### 2.5 生态包结构（引用）

- Semi：`dotnet add package Semi.Avalonia` + 6 个附加主题包；`SemiTheme Locale="zh-CN"` 表明**本地化是主题层职责** [W10]。
- Ursa：`Irihi.Ursa` 控件 + `Irihi.Ursa.Themes.Semi` 主题，另有 Prism/ReactiveUI 扩展；README 标注兼容 Avalonia 11.1.x/11.2.x（**对 Avalonia 12 的支持未核实**，C 级）[W11]。
- Material：`Material.Avalonia` 主题 + `Material.Avalonia.DataGrid` + `Material.Avalonia.Dialogs`，图标 `Material.Icons` 单装；"almost all Avalonia controls"有 Material 样式 [W12]。
- SukiUI：文档明确要求**移除 `Avalonia.Themes.Fluent`**，NuGet 只装 `SukiUI`；但本地 nuspec 显示它依赖 `Avalonia.Themes.Simple`（底座）[W13][L12]。
- FluentAvalonia：单包 `FluentAvaloniaUI`，面向 WinUI 风格，依赖 ColorPicker/DataGrid/Skia/MicroCom [W14]。

### 2.6 AG-UI：协议与 .NET SDK 包划分（Agent 层外部参照）

- AG-UI（Agent User Interaction Protocol）是**流式事件协议**：生命周期（Run/Step）、文本消息、工具调用、状态管理（Snapshot/Delta）、活动（Activity）、子 Agent、推理、自定义事件等类别 [W15]。
- .NET SDK **已按小包拆分**：`AGUI.Abstractions`（协议类型，net10/net9/net8/netstandard2.0/net472, AOT 兼容）、`AGUI.Formatting`（SSE/事件流）、`AGUI.Protobuf`、`AGUI.Client`（HTTP 客户端 + `IChatClient`）、`AGUI.Server` [W16]。
- **对 Arc 的直接含义（B/C）**：仓库内 **0 处** AG-UI 引用（[R20] grep 无命中），Agent 层目前是纯 UI 契约。若未来接入 AG-UI，建议做成**可选叶子包**（如 `Arc.Avalonia.Agent.AgUi`，依赖 `AGUI.Abstractions` 做事件→视图模型映射），**不让核心框架依赖 Agent 协议 SDK**；协议枚举/事件模型属宿主运行时，不属于 UI 契约层。

---

## 3. 通用 UI 框架能力清单对标（26 行）

> 难度档：`S` ≤ 5 人日、`M` 5–15 人日、`L` > 15 人日；**人日为粗估**，锚点是外部包规模（§4.3.1）与 Arc 现状，不作为排期承诺（C 级）。"Avalonia 自带"以 12.1.4 为准，不含社区包。

| # | 能力项 | Avalonia 12 自带 | Arc 现状（可查仓库） | 缺口 / 判定 | 难度 |
|---|---|---|---|---|---|
| 1 | 基础主题：内建控件 ControlTheme 全集 | Fluent 80 / Simple 79 个 XAML，含 Window、WindowDrawnDecorations [W8][W9] | 0 个内建控件全局主题；137 个 ControlTheme 均面向自有控件（内建 TargetType 仅 6 类且为内部辅助键）[R10] | **核心缺口**：~78 个内建控件基础主题 | L |
| 2 | Light/Dark 变体 | `ThemeDictionaries` + `RequestedThemeVariant` + `ThemeVariantScope` [W3] | `ArcThemeManager` 设 `RequestedThemeVariant` + 交换 palette 字典；8 accent × 2 scheme [R5] | 自定义变体（高对比）未做；系统跟随被覆盖 | S |
| 3 | 系统主题/高对比跟随 | 默认继承系统偏好；可被应用覆盖 [W3] | Gallery 固定 `RequestedThemeVariant="Light"`，由 manager 切换 [R7] | 缺系统跟随 + 高对比 | S–M |
| 4 | Window 壳与标题栏 | `Window` + `WindowDecorations`/`ExtendClientAreaToDecorationsHint` [W4]；v12 `WindowDrawnDecorations` [W5][W6] | 无 `ArcWindow`/标题栏；仅 `Window{Background,Foreground,FontFamily}` 样式（`ArcTheme.axaml:14-18`）；`GalleryWindow : Window` 用默认 chrome [R6][R17] | **缺口**：窗口壳 + 自绘标题栏 + caption 按钮 + 高 DPI 拖拽区 | L |
| 5 | 布局容器 | Panel/Grid/StackPanel/DockPanel/WrapPanel/GridSplitter/SplitView 等（均有主题）[W8][W9] | 有 `ArcResizablePanels`/`ArcScrollArea` 等自绘容器；全仓库 `SplitView`/`Splitter` 使用 **0** 处 [R11] | 缺 SplitView/Splitter/自适应布局壳 | M |
| 6 | 导航壳 | TabControl/TabStrip/CommandBar/NavigationPage/TabbedPage 有主题；**无导航服务** | Navigation 项目 4 控件（Breadcrumb/MegaMenu/Pagination/Tabs）；路由在 Demo/Gallery [R18] | 缺 NavigationService + Shell 骨架 | M |
| 7 | 数据展示与虚拟化 | ItemsControl/ListView/TreeView + VirtualizingStackPanel；DataGrid 在附加包 | `ArcVirtualList`（ListBox + VirtualizingStackPanel，[R12]）；自绘 `ArcDataGrid`/`ArcTreeTable`/`ArcSortableDataTable` | 网格编辑、列虚拟化、树虚拟化未验证/缺 | L |
| 8 | 输入控件 | TextBox/ComboBox/NumericUpDown/DatePicker/AutoCompleteBox 等 + 主题 | 30+ 自有输入控件；内建 TextBox 作 PART 主机；内建控件仅 6 类 ControlTheme [R10] | 内建输入控件外观仍依赖 Fluent | M |
| 9 | 浮层 | Popup/Flyout/ToolTip/ContextMenu（默认外观在主题包） | `ArcPopover`/`ArcTooltip`/`ArcDialog`/`ArcDrawer`/`ArcBottomSheet`/`ArcToastHost` | Popup/Flyout 默认主题来自 Fluent；缺 PopupHost 主题 | S–M |
| 10 | 服务层：对话框 | 无内置服务；`Window.ShowDialog` 原生 [W4] | `ArcDialog` 控件，页面内自管 | 缺 `IDialogService`（结果回传/focus trap） | M |
| 11 | 服务层：通知 | `WindowNotificationManager`（需主题） | `ArcToastHost`（控件级） | 缺 Window 级通知宿主 + 服务 | S |
| 12 | 服务层：主题/强调色 | 无（应用自管） | `ArcThemeManager.Apply/SetScheme/SetAccent/ToggleScheme` + `ArcResources` 类型化取值 [R5] | 事实标准已有，缺接口化/持久化 | S |
| 13 | 服务层：导航/窗口 | 无 | 无 | 缺 `INavigationService`/`IWindowService` | M |
| 14 | 本地化 / RTL | FlowDirection、AccessKey、culture-aware 格式化；无 L10n 框架 | 0 个 `.resx`；英文 UI 文案硬编码；`FormattedText` 多处硬编码 `FlowDirection.LeftToRight` [R11] | 本地化 + RTL 双缺口（294 条目文案） | L |
| 15 | 无障碍 | AutomationProperties / AutomationPeer / UIA | 744 处 `AutomationProperties.*`；**0** 个 `AutomationPeer` [R11] | 有基础语义，缺自定义 peer / 结构性语义 / 测试门禁 | M |
| 16 | 焦点与键盘导航 | `FocusManager`（v12 重做）、TabNavigation [W6] | 遍布 `:focus-visible /template/ Border#Ring` 自绘焦点环；无统一焦点陷阱库 | 缺 dialog/行内编辑焦点陷阱与 Tab 序列契约 | M |
| 17 | 主题扩展点 | ControlTheme + StyleKey + `{x:Type}` 键 + 资源覆盖 [W1] | 137 个 ControlTheme，token 全走 `DynamicResource` [R10] | 缺对外覆盖文档 + StyleKey 规范 + 示例 | S–M |
| 18 | MVVM 辅助 | 不提供（v12 编译绑定默认开）[W6] | 0 MVVM 库；全仓库 1 处 `INotifyPropertyChanged` [R11] | 可选：保持 UI-only；若要，独立包 | S |
| 19 | 拖放/剪贴板/文件对话框 | DragDrop/Clipboard 服务（v12 API 变更）；`IStorageProvider`；`ManagedFileChooser` 需主题 [W6][W8] | `ArcFileDropzone` 控件；无系统 DnD/文件对话框服务 | 缺宿主服务封装 | M |
| 20 | 平台集成（Tray/原生菜单/DPI） | TrayIcon、原生菜单、DPI scaling | 无 | 属宿主/平台层，框架可不做 | M（可选） |
| 21 | 打包与版本 | 官方按 Avalonia/Themes/Controls/Desktop/平台分包 + lockstep 版本 [L1][L10] | 28 个 csproj；**无** `PackageId`/`IsPackable`/`Version`/icon/README；`NuGet.config` 源被清空 [R14][R15] | **发布阻断** | S–M |
| 22 | 文档与示例 | 官方全站文档 + API reference | 294/294 条目文档 + 294 Docs JSON + 318 页面 + 233×2 截图 + audit/smoke [R16][R19] | 强于业界；缺 XML/API reference（`GenerateDocumentationFile=false`）[R14] | S–M |
| 23 | 测试护栏 | `Avalonia.Headless` + xUnit v3 / NUnit 4（v12 更新）[W6] | 仅 1 个控制台 `Verify`（`OutputType=Exe`，引用 Foundation+Demo，无测试框架）[R13] | 缺 Headless 渲染/快照/主题一致性测试 | M |
| 24 | 字体 | 预构建字体包模式（`Avalonia.Fonts.Inter`）[W7] | 硬编码 `avares://Avalonia.Fonts.Inter/Assets#Inter` + `.WithInterFont()` [R4][R8] | 需内置字体（1.85 MB）替换 | S |
| 25 | Token/资源语义 | 大量主题私有资源键 | 82 个 `ArcTokens` 常量（颜色/字号/间距/圆角/阴影/动效）[R4] | 缺公开 token 契约/稳定性承诺文档 | S–M |
| 26 | 高 DPI/缩放/渲染 | Skia 后端 + DPI scaling；v12 移除 Direct2D [W6] | 自绘控件按像素计算（Custom Drawing 大量 Render/Pen 逻辑），未做 DPI 冒烟 | 需 DPI/缩放回归（未验证） | S–M |

**缺口分布**：主题/窗口/虚拟化/本地化 4 项为 `L`；服务层、无障碍、测试、导航、输入等 10 项为 `M`；其余为 `S/M`。**Arc 的强项集中在"组件广度 + 文档/截图验收"**，弱项集中在"框架基础设施（主题底座、服务层、窗口壳、L10n、测试）"——与 README 自述"设计系统与组件/块集合，不是 1:1 API clone"一致 [R1]。

---

## 4. "完全无其他 UI 依赖"的可辩护边界

### 4.1 三档判定标准

| 档位 | 定义 | 判定问句 | 是否计入"其它 UI 库" | 依赖规则 |
|---|---|---|---|---|
| **① 框架内核** | Avalonia 项目发布的运行时/渲染/排版/平台后端包；MIT；不含具体设计语言 | 去掉它，框架还能存在吗？ | **不计入**（它是框架本体/底座） | 核心库可依赖 |
| **② 主题层** | 任何提供内建控件外观（ControlTheme/资源）的包 | 换掉它，UI 外观/控件语义会变吗？ | **计入** | 核心库**禁止**依赖；示例应用在迁移期可临时使用 |
| **③ 可选增值** | 字体/图标资产、官方附加控件、第三方渲染扩展 | 不装它，框架核心功能是否完整？ | **不计入**，但须声明为 optional | 核心包**不依赖**；可由应用或 feature 包按需引入 |

**一句化规则**：外观会变 → 主题层（必须替换）；提供控件但非必需 → 增值（不依赖）；只提供运行平台/渲染 → 内核（必须保留）。

### 4.2 逐包判定矩阵

| 包 | 许可证 | 档位 | 理由 | 替代成本 | 对 Arc 的结论 |
|---|---|---|---|---|---|
| `Avalonia`（Base/Controls/Markup.Xaml） | MIT | ① 内核 | 控件与样式系统本体；无设计语言 | 不可替代 | **必需** |
| `Avalonia.Skia` / `HarfBuzzSharp` | MIT | ① 内核 | 渲染/排版后端；v12 只保留 Skia 路线 [W6] | 不可替代 | **必需**（传递） |
| `Avalonia.Desktop`（+ Win32/X11/Native） | MIT | ① 内核 | 平台后端 meta 包（15 KB 壳 + 5 个平台包）[L10] | 不可替代 | **仅示例应用/宿主**引用，库不引 |
| `Avalonia.Themes.Fluent` | MIT | **② 主题层** | 716 KB / 80 个内建控件 ControlTheme；提供窗口装饰与全部内建控件外观 [L1][W8] | 自建基础主题（§4.3.1，L 级） | **必须替换**；现状仅 Gallery 使用 |
| `Avalonia.Themes.Simple` | MIT | **② 主题层** | 405 KB / 79 个；同层替代品（SukiUI 拿它当底座）[L2][L12] | 同上 | **不得作为发布依赖**；可作临时脚手架参考 |
| `Avalonia.Fonts.Inter` | MIT（字体 OFL） | ③ 增值（字体资产） | 1.85 MB 纯字体数据，无控件/主题 [L9] | 内置 TTF + `AvaloniaResource`（S，§4.3.2） | **改为内置**；README 依赖声明同步更新 |
| `Avalonia.Controls.ColorPicker` | MIT | ③ 增值（附加控件） | 472 KB；内嵌 Fluent/Simple 两套主题 → 与自包含主题冲突 [L7] | Arc 已有自绘 `ArcColorPicker`（1 个 ControlTheme，[R10]）+ 自研维护 | **不依赖**；自研替代 |
| `Avalonia.Controls.DataGrid` | MIT | ③ 增值（附加控件） | 358.5 KB；内嵌两套主题；Arc 已有 `ArcDataGrid`/`ArcTreeTable` [L8][R10] | 自研网格编辑/虚拟化成本高（L，未验证质量持平） | **不依赖**；自研替代，边界见 §4.3.3 |
| `SkiaSharp` | MIT | ① 内核（渲染引擎） | Avalonia.Skia 的依赖（3.119.4，[L11]）；Arc 未直接引用 | 不可替代 | **不直接依赖**（传递即可） |
| `ShimSkiaSharp` | MIT | ③ 增值（第三方渲染扩展） | Svg.Skia 的 Skia 对象模型 shim；Arc 未引用 [L13] | 仅在需要 SVG 渲染时引入 | **不引入**；若引入走 feature 包 |
| `MicroCom.Runtime` | MIT | ① 内核（传递） | Avalonia 依赖（COM 互操作） | 不可替代 | 传递即可 |

### 4.3 替代成本

#### 4.3.1 Fluent/Simple → 自有基础主题（最大单项）

**规模锚点（A 级）**：Fluent 80 个 XAML（含 2 个共享文件）、Simple 79 个；两个主题都覆盖 `Window` 与 `WindowDrawnDecorations`；DLL 体积 716/405 KB。抽样文件大小可作工作量体感：`TextBox.xaml` 18.3 KB、`CheckBox.xaml` 15.6 KB、`SplitView.xaml` 21.5 KB、`TimePicker.xaml` 17.0 KB、`DatePicker.xaml` 14.1 KB（Fluent 12.1.4，[W8]）。

**Arc 现状（A 级）**：137 个 ControlTheme 全为自有控件；内建控件只有 6 个 TargetType，且多为内部辅助键（`ArcTimeOptionItem`、`ArcEmojiIconButton`、`ArcPickerFieldTheme` 等）；`ArcTheme.axaml` 只对 `Window`/`TextBlock` 做样式级设置，**没有 WindowDrawnDecorations**。[R6][R10]

**建议分三波（粗估，C 级，需 task-1 交叉验证）**：

| 波次 | 范围 | 控件数（约） | 难度 | 外部锚点 |
|---|---|---|---|---|
| B1 输入与基础 | Button/Repeat/ToggleButton/CheckBox/RadioButton/TextBox/ComboBox(+Item)/NumericUpDown/ButtonSpinner/Slider/ProgressBar/ToggleSwitch/ScrollViewer/ScrollBar/ListBox(+Item)/Label/Separator/PathIcon/SelectableTextBlock/DataValidationErrors | ~20 | M | Fluent 对应文件平均 5–18 KB |
| B2 容器与导航 | Window/**WindowDrawnDecorations**/TabControl(+Item/TabStrip)/Menu/MenuItem/ContextMenu/MenuFlyout(ToolTip/Flyout)/PopupRoot/OverlayPopupHost/GridSplitter/SplitView/Expander/GroupBox/HeaderedContentControl/ItemsControl/ContentControl/HyperlinkButton/AdornerLayer/ThemeVariantScope/Carousel(+Page) | ~20 | M–L | WindowDrawnDecorations 单文件 11.5 KB |
| B3 选择器与其余 | Calendar 家族(4)/DatePicker/TimePicker/DateTimePickerShared/AutoCompleteBox/CommandBar/NotificationCard/WindowNotificationManager/ManagedFileChooser/PipsPager/RefreshContainer(+Visualizer)/TransitioningContentControl/TextSelectionHandle/EmbeddableControlRoot/NavigationPage/TabbedPage/DrawerPage/ContentPage/TableView 家族(4) | ~35 | M | Fluent `Controls` 总 80 文件 |

**MVP 建议**：先做 B1 + B2 的 `Window`/`WindowDrawnDecorations`/`PopupRoot`/`OverlayPopupHost`/`ToolTip`/`FlyoutPresenter`/`ScrollViewer`/`ScrollBar`/`ListBox`/`TextBox`/`ComboBox`（约 35–40 个），即可让 Gallery 去掉 `FluentTheme` 并跑 294 页截图回归；B3 在发布前补齐或文档标注"未主题化的内建控件不保证外观"（次优，但可用于 v0.x）。

**难度依据（B）**：主题 XAML 是"模板 + 伪类 + 资源键"的重复劳动，单控件 0.3–1 人日（含双主题/焦点/禁用/验证态），窗口装饰与 Popup 类 1–2 人日；~78 个控件落在 **25–45 人日**区间（与外部"一名熟悉 Avalonia 主题系统的工程师 3–6 周"的体感一致，C 级）。

#### 4.3.2 Inter → 内置字体（S 级，最小替代）

**现状（A）**：`ArcPalette.cs:33` 硬编码 `avares://Avalonia.Fonts.Inter/Assets#Inter`；字体 token 在 `ArcPalette.cs:203-204`；Gallery `Program.cs:13` 调 `.WithInterFont()`；Inter 包 1,848.5 KB 仅含字体 [R4][R8][L9]。

**替代方案（官方文档支持，A）**：把 Inter 静态 TTF 作为 `AvaloniaResource` 放入 `Arc.Avalonia`，`FontFamily` 改为 `avares://Arc.Avalonia/Assets/Fonts#Inter`（静态资源模式**无需** `ConfigureFonts` 注册）；或实现 `ArcFontCollection : EmbeddedFontCollection`（`fonts:arc#Inter`）并在宿主 `ConfigureFonts` 注册 [W7]。

| 成本项 | 说明 |
|---|---|
| 代码改动 | 1 个常量 + 1 个 csproj `AvaloniaResource`（+ 可选 collection 类）|
| 体积 | ≈ +1.85 MB 到 `Arc.Avalonia.dll`（与现在依赖外部包的总包体积相同，**不增加部署体积**）|
| 许可 | Inter 为 SIL OFL，需随包附带 OFL 文本；README 字体声明从"via Avalonia.Fonts.Inter"改为"内置" [R1] |
| 风险 | 变量字体不支持（需静态字重）、CJK/emoji 仍走系统回退、WASM 需完整 collection URI [W7] |

#### 4.3.3 ColorPicker/DataGrid → 自研（Arc 已选择）

**现状（A）**：Arc 未引用这两个包，自绘 `ArcColorPicker`/`ArcColorEditor`/`ArcDataGrid`/`ArcTreeTable`/`ArcSortableDataTable`，并各有自己的 ControlTheme [R10]；`ArcDataGrid` 主题文件 60 行、`ArcTreeTable` 61 行、`ArcSortableDataTable` 22 行（模板行数，[R10]）。

**判定**：不引入官方附加控件。理由：
1. 它们自带 Fluent/Simple 主题（[L7][L8]），引入后要么保留内建主题（违背自包含），要么为它们再写 Arc 主题（额外 S–M/控件）。
2. Arc 已有等价自研物；引入会形成"两套数据网格 API"。
3. 若未来需要"网格编辑/列虚拟化/剪贴板填充"等高级能力，官方包的这部分能力**不能**通过主题剥离——要么整体引入并接受两套主题，要么继续自研（L 级）。

#### 4.3.4 Desktop / Skia / HarfBuzz 的处理

- `Avalonia.Desktop` 是**应用宿主**的启动包（`.UsePlatformDetect()` 需要平台后端），不是 UI 库；Arc 的库项目不应引用它，Gallery/Demo 引用即可（现状正确：仅 Gallery 引用 [R9]）。
- `SkiaSharp` / `HarfBuzzSharp` 是 `Avalonia.Skia` 的传递依赖（3.119.4 / 8.3.1.3，[L11]），Arc 不直接引用，符合"不绑定渲染后端细节"。

### 4.4 判定规则与 CI 检查清单

**发布前必须成立的 6 条**（可脚本化）：

1. `src/` 下所有**发布库** `.csproj` 的 `PackageReference` 只允许 `Avalonia`（`ProjectReference` 之内不限）；仅应用（Demo/Gallery）允许 `Avalonia.Desktop`。
2. 全仓库 `grep` 不得命中 `FluentTheme|Themes.Fluent|SimpleTheme|Themes.Simple`（示例应用在迁移期豁免，迁移完成即删）。
3. 任何 `ControlTheme`/`Style` 不得引用 `FluentTheme` 的资源键（现状 Arc 使用自有 `Arc*` 键，[R6]）。
4. 字体不出现 `Avalonia.Fonts.Inter` 的 `avares` 路径（替换后）。
5. 新增附加控件（DataGrid/ColorPicker 等）必须走独立 feature 包，并在其内自建 Arc ControlTheme。
6. 每个发布包在 nuspec/`.csproj` 中显式声明 `PackageLicenseExpression=MIT` 与依赖白名单，CI 校验 `project.assets.json` 的直接依赖集合。

---

## 5. 结论：Arc 自包含框架的 NuGet 包拓扑

### 5.1 依赖方向（唯一允许的方向）

```text
Avalonia（内核，MIT）
  └─ Arc.Avalonia                  ← 核心：token/主题引擎/动效/图标/基础控件/内建控件基础主题/内置字体
       ├─ Arc.Avalonia.{Inputs, Actions, Navigation, Overlays, Feedback,
       │                  DataDisplay, Charts, Pickers, Effects, Content, Media}
       │       ├─ Arc.Avalonia.{Blocks.Marketing, Blocks.Commerce, Blocks.App,
       │       │                  Blocks.Team, Blocks.Data, Blocks.AI}
       │       └─ Arc.Avalonia.Agent        ← 顶层：吸收 AgentUI + LifeToday + Blocks.AI
       └─ Arc.Avalonia.{Special.Glass, Special.Motion, Special.Media,
                         Special.AIWidgets, Special.AIExplainers}   （可选）
  └─ Arc.Avalonia.All                ← 元包：聚合以上稳定包（不含 Demo/Gallery）
```

- 依赖只能"向下"：Components → 核心；Blocks → Components；Agent → Components + Special.AI*/Blocks.AI；All → 所有。
- **禁止横向依赖**：`Inputs → DataDisplay` 等（现状需核；task-2 负责）。禁止任何包依赖 `Avalonia.Themes.Fluent/Simple`。
- `Arc.Avalonia.Internal` 建议**不发布**（保持 `internal`/`InternalsVisibleTo`），或并入核心（减少一个公开包与版本面）。

### 5.2 建议包清单

| NuGet ID | 职责 | 直接依赖 | 发布 | 版本 |
|---|---|---|---|---|
| `Arc.Avalonia` | 82 token、`ArcThemeManager`/`ArcResources`、动效、图标、7 个基础控件、**内建控件基础主题（B1–B3）**、**Window/WindowDrawnDecorations 主题**、内置 Inter 字体 | `Avalonia` | ✅ 核心 | lockstep |
| `Arc.Avalonia.Inputs` … `Arc.Avalonia.Media`（11 个） | 组件族 + 各自的 `Themes/*.axaml`（现状已具备，116 个主题文件中大部分在此） | `Arc.Avalonia` | ✅（可单发，也可靠元包） | lockstep |
| `Arc.Avalonia.Special.*`（5 个） | 特效/图解类，可独立；AIWidgets/AIExplainers 可被 Agent 依赖 | `Arc.Avalonia`（+按需组件族） | ✅ 可选 | lockstep |
| `Arc.Avalonia.Blocks.*`（6 个） | 产品页面块（组合组件，带示例数据） | 对应组件族 | ✅ 可选 | lockstep |
| `Arc.Avalonia.Agent` | 顶层 Agent 组合（对话/审批/计划/收件箱/上下文窗口等），纯 UI 契约，不含运行时 | Components + Special.AI* + Blocks.AI | ✅ | lockstep |
| `Arc.Avalonia.All` | 元包（`<dependency>` 全部稳定包），供"一键引用" | 以上全部 | ✅ | lockstep |
| `Arc.Avalonia.Testing`（可选） | Headless 渲染/截图/审计辅助（从 Gallery 的 `ShotRunner`/`CatalogAudit` 抽公共部分） | 核心 + Avalonia.Headless | ✅ 可选 | lockstep |
| `Arc.Avalonia.Docs`（可选） | 294 个 Docs JSON + 元数据（内容包，非代码） | 无（或核心） | ✅ 可选 | lockstep |
| `Arc.Avalonia.Demo` / `.Gallery` | 示例与文档站宿主 | 核心 + 全部 + Avalonia.Desktop | ❌ 不发布（或仅示例） | — |

**版本策略**：所有 `Arc.Avalonia.*` **同一版本号**（lockstep），避免"组件 0.3 + 核心 0.5"的组合爆炸；兼容表按 Avalonia 主次版本声明（参照 Semi 的四段版本 `12.1.0.1` 对 Avalonia 12.1.0，[W10]）。建议先用 `0.1.0-preview.N`，稳定后 `1.0.0`；README 提供 "Arc 版本 ↔ Avalonia 版本" 表。

**包元数据清单（发布阻断项）**：`PackageId`、`Version`、`PackageLicenseExpression=MIT`、`PackageReadmeFile`、`PackageIcon`、`RepositoryUrl`、`Authors`、`Description`、`PackageTags`、`GenerateDocumentationFile=true`（当前 `false` + CS1591 抑制，[R14]）、SourceLink、`.snupkg` 符号包。NuGet ID 可用性：查询 `nuget.azure.cn` 镜像，`arc.avalonia` 与 `arc.avalonia.agent` 均返回 404（**推断未注册**；发布前须在 nuget.org 复核，C 级）。

### 5.3 与业界做法的对照

| 维度 | Avalonia 官方 | Semi.Avalonia | Ursa | Material.Avalonia | SukiUI / FluentAvalonia | **Arc（建议）** |
|---|---|---|---|---|---|---|
| 主题是否单列 | ✅ Themes.Fluent/Simple | ✅ 主主题包 | ✅ `*.Themes.Semi` | ✅（含控件） | ❌ 单体 | **✅ 但并入核心 `Arc.Avalonia`**（单设计语言，见 §5.4） |
| 附加控件主题 | 内嵌于控件包（Fluent+Simple） | 每控件一包（6 个） | 由主题包覆盖 | DataGrid/Dialogs 分列 | 内嵌 | **不引入附加控件；自研并随组件族带主题** |
| 字体/图标 | 字体独立包 | 主题内含，`Locale` 参数 | — | 图标独立包 | — | **内置到核心**（1.85 MB，去外部依赖） |
| 文档 | 官方站点 | 独立文档站 | wiki | wiki | 文档站 | **294 条目 docs 平台（内容包可选）** |
| 元包 | 无（按需组合） | `Semi.Avalonia` 即入口 | 无 | `Material.Avalonia` 即入口 | 单包即入口 | **`Arc.Avalonia.All` + 各族可单发** |
| 版本对齐 | lockstep 12.1.4 | 四段对齐 | 与 Avalonia 主次对齐 | 跟随 | 跟随 | **lockstep + 兼容表** |

### 5.4 三条"不要照抄业界"的理由（本仓库特殊性）

1. **全自绘 + token 驱动**：Arc 的 137 个 ControlTheme 全部绑定自有 token、用 `DynamicResource` 解析（[R6][R10]）。业界主题包（Fluent/Simple/Semi）是"给内建控件换皮"的产品，照抄"主主题包 + 每附加控件一包"的矩阵，会把 Arc 变成"又一套换皮主题"，与"token 约束一切"的设计系统定位冲突。Arc 需要的是**一次性的内建控件基础主题**（token 化的 78 个 ControlTheme），而不是可插拔的多主题市场。
2. **文档/截图/审计就是交付物**：294 条目 ×（文档 + 预览 + 双截图 + audit/smoke）是 Arc 最有价值的资产 [R16][R19]；业界包以 DLL 为交付单元。若照抄"极简包拆分"，文档仍留在 Demo 里，发布形态与验收形态脱节。应保留 `Demo`/`Gallery` 为**开发宿主**，并用可选 `Arc.Avalonia.Docs` 内容包把 294 个 Docs JSON 与内容以 lockstep 版本发布。
3. **单一设计语言 + 运行时换肤已就绪**：业界多包结构服务于"N 主题 × M 控件"的市场（Semi/Ursa/Material 都是可插拔主题生态）；Arc 只有 1 套设计语言，8 accent × 2 scheme 由 palette 字典运行时切换（[R5]），不需要 Theme/Skin 抽象层与多主题版本矩阵——拆得越细，版本矩阵与用户心智负担越大，收益越低。

### 5.5 迁移路线（从现状到可发布，粗估 C 级）

| 步骤 | 内容 | 产出/验收 | 难度 |
|---|---|---|---|
| M0 | 打包基线：PackageId/Version/README/icon/XML docs/SourceLink/`.snupkg`；依赖白名单 CI（§4.4） | `dotnet pack` 可产出、`project.assets.json` 校验通过 | S–M |
| M1 | 字体内置：Inter TTF 入 `AvaloniaResource`、改 `ArcPalette.cs:33`、删 `.WithInterFont()`、更新 README/OFL | `grep Avalonia.Fonts.Inter` 归零；294 页截图 diff 无回归 | S |
| M2 | 基础主题 MVP（B1+B2 关键控件 + Window/WindowDrawnDecorations/Popup/Flyout/ScrollViewer/TextBox/ComboBox/ListBox），Gallery 去 `FluentTheme` | 无 Fluent 下 294 条目页 audit/smoke/截图通过（双主题） | L |
| M3 | 基础主题补齐 B3，或明确"未主题化控件"清单 | 覆盖清单与 Fluent 80 文件对齐表 | L |
| M4 | 服务层最小集：`IThemeService`/`IDialogService`/`INotificationService` + Window 级通知宿主 | 三个 Demo 场景可用 | M |
| M5 | 顶层化：AgentUI + LifeToday + Blocks.AI → `Arc.Avalonia.Agent`（吸收，不新建重复组件） | Agent 场景页在无宿主依赖下可渲染 | M |
| M6 | 元包与发布：`Arc.Avalonia.All`、Docs 内容包、`Arc.Avalonia.Testing` | 一次 `dotnet add package Arc.Avalonia.All` 可跑 Gallery | S–M |

> 外部工作量锚点（C 级，供 Lead 与 task-1 交叉）：基础主题 25–45 人日；本地化/RTL 15–25 人日（294 条目文案）；Headless 测试 8–15 人日；打包工程 3–8 人日。总体与文档 §0 的"最大风险"一致：**主题底座是第一大项**。

### 5.6 风险与未验证项

| 项 | 说明 | 等级 |
|---|---|---|
| ~78 个控件数 | 由 Fluent 80 / Simple 79 文件数推导，含 2 个共享文件；去重后精确数未逐个核对 | C |
| DataGrid 内嵌主题细节 | 仅字符串证据（`Themes/Fluent` ×3 等），具体 XAML 清单未列出 | C |
| Arc 控件/类计数 | "236 控件 / 411 公共类"引自 `docs/agent-life-tools/04-qa-review.zh.md:35`，本轮未重算 | C |
| 页面/截图口径 | `shots/light` 与 `shots/dark` 各 233 张（目录实测），而 `docs/12` 记录 smoke 315 页；截图是否覆盖 315 页未验证 | C |
| NuGet ID 可用性 | 镜像 404 推断未注册；未在 nuget.org 直接复核 | C |
| 主题质量/性能 | 自有基础主题的渲染性能、双主题一致性无外部同类基线可比 | C |
| Ursa 对 Avalonia 12 的支持 | README 只写 11.1/11.2 兼容，未核实 12.x | C |

---

## 6. 证据索引

### 6.1 本地 NuGet 包证据 `[L*]`（`D:\.nuget\packages\`，只读实测）

| ID | 位置（文件:行） | 证明内容 |
|---|---|---|
| L1 | `avalonia.themes.fluent\12.1.4\avalonia.themes.fluent.nuspec:16-23`；`lib\net10.0\Avalonia.Themes.Fluent.dll`（716.0 KB）、xml 6.9 KB；DLL 字符串命中 `WindowDrawnDecorations`×3、`TitleBar`×2、`ControlTheme`×5、`DataGrid`/`ColorPicker`×0 | Fluent 主题包形态、依赖、体积、窗口装饰与"不含附加控件主题" |
| L2 | `avalonia.themes.simple\12.1.0\avalonia.themes.simple.nuspec:16-23`；`lib\net10.0\Avalonia.Themes.Simple.dll` 405.0 KB；字符串同上 | Simple 主题包形态 |
| L3 | `semi.avalonia\12.1.0.1\semi.avalonia.nuspec:16-25`；`lib\net10.0\Semi.Avalonia.dll` 1696 KB | 第三方主主题包依赖（Avalonia + Irihi.Avalonia.Shared） |
| L4 | `semi.avalonia.datagrid\12.1.0.1\semi.avalonia.datagrid.nuspec:16-22`；`Semi.Avalonia.DataGrid.dll` 68.5 KB | 附加控件主题包仅依赖 `Avalonia.Controls.DataGrid`，不依赖主主题包 |
| L5 | `irihi.avalonia.shared\0.5.0\irihi.avalonia.shared.nuspec:13-20` | Semi 的共享基础设施包只依赖 Avalonia |
| L6 | `fluentavaloniaui\3.1.0\fluentavaloniaui.nuspec:13-21`；`FluentAvalonia.dll` 3587.5 KB | WinUI 风格单体包依赖树（Avalonia+ColorPicker+DataGrid+Skia+MicroCom） |
| L7 | `avalonia.controls.colorpicker\12.1.0\avalonia.controls.colorpicker.nuspec:16-25`；`Avalonia.Controls.ColorPicker.dll` 472 KB；字符串含 10 个 `avares://.../Themes/{Fluent,Simple}/*.xaml` | 附加控件自带两套内建主题（ColorPicker 5+5 文件清单） |
| L8 | `avalonia.controls.datagrid\12.1.2\avalonia.controls.datagrid.nuspec:17-24`；`Avalonia.Controls.DataGrid.dll` 358.5 KB；字符串 `Themes/Fluent`×3、`Themes/Simple`×3、`ControlTheme`×5 | DataGrid 附加控件自带主题（清单未验证） |
| L9 | `avalonia.fonts.inter\12.1.4\avalonia.fonts.inter.nuspec:16-23`；`Avalonia.Fonts.Inter.dll` 1848.5 KB | 字体资产包形态与体积 |
| L10 | `avalonia.desktop\12.1.4\avalonia.desktop.nuspec:16-33`；`Avalonia.Desktop.dll` 15 KB | 平台 meta 包（Native/X11/HarfBuzz/Skia/Win32） |
| L11 | `avalonia.skia\12.1.4\avalonia.skia.nuspec:16-35`；`skiasharp\3.119.4\lib\net10.0\SkiaSharp.dll` 547.3 KB（缓存另有 4.153.1） | SkiaSharp 3.119.4 为 Avalonia.Skia 传递依赖；HarfBuzz 8.3.1.3 |
| L12 | `sukiui\7.0.1\sukiui.nuspec:12-21`；`SukiUI.dll` 1830.5 KB | 单体 UI 库依赖 `Avalonia.Themes.Simple` 等 6 包 |
| L13 | `shimskiasharp\4.1.0\shimskiasharp.nuspec:15-21`；`ShimSkiaSharp.dll` 190.5 KB（net10） | Svg.Skia 的 shim，无依赖，Arc 未引用 |
| L14 | `avalonia\12.1.4\avalonia.nuspec:16-27` | Avalonia meta 依赖（BuildServices/Remote.Protocol/MicroCom） |
| L15 | `avalonia\12.1.4\lib\net10.0` 文件清单（Avalonia.Base 2306 KB、Controls 1323.5 KB、Markup.Xaml 73 KB 等） | 内核包体积构成 |

### 6.2 仓库证据 `[R*]`（`D:\Code\VibeCoding\Arc.Avalonia\`）

| ID | 位置 | 证明内容 |
|---|---|---|
| R1 | `README.md:257-262`、`README.md:264-270` | 依赖声明（Avalonia/Desktop/Fluent/Inter + SkiaSharp 措辞）、字体与图标许可 |
| R2 | `CONVENTIONS.md:204-207` | 团队政策：只用 Avalonia/Avalonia.Themes.Fluent/Avalonia.Fonts.Inter/SkiaSharp |
| R3 | `src/Foundation/Arc.Avalonia/Arc.Avalonia.csproj:7-10` | 核心库只引 `Avalonia` + `Avalonia.Fonts.Inter` |
| R4 | `src/Foundation/Arc.Avalonia/Theme/ArcPalette.cs:33`、`:203-204`；`Theme/ArcTokens.cs`（82 个 `public const string`） | Inter 字体 URI 硬编码；字体 token；82 个 token 常量 |
| R5 | `src/Foundation/Arc.Avalonia/Theme/ArcThemeManager.cs:24-46`、`:15-22` | 运行时换肤（RequestedThemeVariant + palette 字典合并）；`ReducedMotion` |
| R6 | `src/Foundation/Arc.Avalonia/Theme/ArcTheme.axaml:1-31`（Window/TextBlock 样式、`DynamicResource`） | 无内建控件 ControlTheme；无 WindowDrawnDecorations |
| R7 | `src/Apps/Arc.Avalonia.Gallery/App.axaml:1-33`（`<FluentTheme />` 行 7；25 个 `StyleInclude` 行 8–32） | 唯一 FluentTheme 使用点；主题入口装配 |
| R8 | `src/Apps/Arc.Avalonia.Gallery/Program.cs:9-13`（`.UsePlatformDetect().WithInterFont()`） | 字体注册与平台启动 |
| R9 | 全仓库 `.csproj` PackageReference 抽取（见 §1.1 方法）：26 个库项目中 25 个仅 `Avalonia`，`Arc.Avalonia`（Foundation）额外 `Avalonia.Fonts.Inter`；仅 Gallery 引 `Avalonia.Desktop`/`Themes.Fluent`/`Fonts.Inter` | 依赖面现状（库层无 Fluent） |
| R10 | `src/**/Themes/*.axaml` 统计：`<ControlTheme` 137 个、主题入口 116 个文件；TargetType 为内建控件的仅 Button/ContentControl/ListBox/ListBoxItem/RadioButton/TextBox；`ArcDataGrid.axaml`/`ArcColorPicker.axaml` 等 | Arc 主题资产全貌；内建控件主题缺口 |
| R11 | 全仓库 `*.cs/*.axaml` 统计：`AutomationProperties` 744 行、`AutomationPeer / IAccessible` **0**、`.resx` **0**、`SplitView / GridSplitter / Splitter` **0**、`AG-UI / AGUI` **0**、`INotifyPropertyChanged` **1**、`FormattedText(..., FlowDirection.LeftToRight, ...)` 多处（如 `Special.Motion/Controls/SpecialMotionUi.cs:84`、`Special.Media/Controls/SpecialUi.cs:62`、`Special.AIWidgets/Controls/SpecialVisuals.cs:396`、`Special.Glass/Controls/ArcDurationDial.cs:174`） | 无障碍/本地化/RTL/布局/测试缺口 |
| R12 | `src/Foundation/Arc.Avalonia/Controls/ArcVirtualList.cs:27-40` | 虚拟化实现方式（ListBox + VirtualizingStackPanel） |
| R13 | `tests/Arc.Avalonia.Tests/Arc.Avalonia.Tests.csproj:1-13`；`Program.cs:17-28` | 无测试框架；仅控制台 `VERIFY: checks=…` |
| R14 | `Directory.Build.props:7`（AvaloniaVersion 12.1.4）、`:11-12`（`CS1591` 抑制、`GenerateDocumentationFile=false`）、`:3`（net10.0）；全部 csproj 无 `PackageId/IsPackable/Version`（grep 无命中） | 打包/文档元数据缺口 |
| R15 | `NuGet.config:4-8`（`<clear />`，离线还原） | 本机无 nuget.org；包依赖分析可确定性还原 |
| R16 | `docs/06-agent-life-tools-extension.zh.md:26-32`（294/128/55/111/236/219 表）、`docs/agent-life-tools/04-qa-review.zh.md:35`（411 公共类/236 控件）、`docs/03-implementation.zh.md:76-81`（294/294） | 组件与文档规模 |
| R17 | `src/Apps/Arc.Avalonia.Gallery/Framework/GalleryWindow.cs:23`（`public sealed class GalleryWindow : Window`）；全仓库无 `TitleBar/WindowDecorations/ExtendClientArea/TrayIcon/NativeMenu` 引用 | 窗口壳缺口 |
| R18 | `src/Components/Arc.Avalonia.Navigation/Controls/`（ArcBreadcrumb/ArcMegaMenu/ArcPagination/ArcTabs）；`src/Components/Arc.Avalonia.Overlays/Controls/`（ArcDialog/ArcDrawer/ArcPopover/ArcShareAccess/ArcTooltip） | 导航/浮层现状 |
| R19 | `docs/12-gallery-doc-m3-m4.zh.md:34-39`（294 docs / pages=294 / smoke 315）；`src/**/Docs/*.json` 294 个；`*Page.cs` 318 个；`docs/**/*.md` 26 篇；`shots/light` 与 `shots/dark` 各 233 张 PNG | 文档/截图/页面规模 |
| R20 | 全仓库 grep `AG-UI` / `AGUI` / `AgUi` / `ag-ui` 无命中 | Agent 层未接协议 |

### 6.3 外部证据 `[W*]`（联网核实；页面获取时间 2026-10-31）

| ID | URL | 证明内容 |
|---|---|---|
| W1 | https://docs.avaloniaui.net/docs/styling/control-themes | ControlTheme 机制：无选择器、`TargetType`、资源字典、StyleKey/`{x:Type}` 查找、单控件单主题 |
| W2 | https://docs.avaloniaui.net/docs/styling/themes | 官方定义："themes are complete sets of control themes and theme resources for built-in controls"；Fluent/Simple 用法；无官方控件清单 |
| W3 | https://docs.avaloniaui.net/docs/styling/theme-variants | ThemeVariant：`ThemeDictionaries`（Light/Dark/Default）、`RequestedThemeVariant`、`ThemeVariantScope`；`ThemeDictionaries` 资源仅能被 `DynamicResource` 解析 |
| W4 | https://docs.avaloniaui.net/controls/primitives/window | `Window` 属性（`WindowDecorations`、`ExtendClientAreaToDecorationsHint`）、ShowDialog 语义 |
| W5 | https://docs.avaloniaui.net/controls/primitives/windowdrawndecorations | v12 `WindowDrawnDecorations`：三层模板、PART_* 部件、伪类、`ElementRole`、平台差异 |
| W6 | https://docs.avaloniaui.net/docs/avalonia12-breaking-changes | v12 移除 TitleBar/CaptionButtons/ChromeOverlayLayer；Skia-only；Headless 测试框架升级；字体解析器；FocusManager 重做；多 Dispatcher |
| W7 | https://docs.avaloniaui.net/docs/styling/custom-fonts | 静态资源字体 / EmbeddedFontCollection / 预构建字体包（`Avalonia.Fonts.Inter`）；URI 必须带 `#Family`；变量字体不支持 |
| W8 | https://api.github.com/repos/AvaloniaUI/Avalonia/contents/src/Avalonia.Themes.Fluent/Controls?ref=release/12.1.4 | Fluent 80 个 ControlTheme XAML 清单（含 Window.xaml 1,835 B、WindowDrawnDecorations.xaml 11,521 B） |
| W9 | https://api.github.com/repos/AvaloniaUI/Avalonia/contents/src/Avalonia.Themes.Simple/Controls?ref=release/12.1.4 | Simple 79 个 XAML（含 Window/WindowDrawnDecorations） |
| W10 | https://github.com/irihitech/Semi.Avalonia（README：https://raw.githubusercontent.com/irihitech/Semi.Avalonia/main/README.md） | Semi 主包 + 6 个附加主题包；`SemiTheme Locale="zh-CN"`；版本兼容表 |
| W11 | https://github.com/irihitech/Ursa.Avalonia（README：https://raw.githubusercontent.com/irihitech/Ursa.Avalonia/main/README.md） | `Irihi.Ursa` 控件 + `Irihi.Ursa.Themes.Semi` 主题分离；Prism/ReactiveUI 扩展 |
| W12 | https://github.com/AvaloniaCommunity/Material.Avalonia（README：https://raw.githubusercontent.com/AvaloniaCommunity/Material.Avalonia/ea3ce1282dc22431e965d104c7049a67abae8e00/README.md） | Material.Avalonia + DataGrid + Dialogs；`Material.Icons` 单独安装 |
| W13 | https://kikipoulet.github.io/SukiUI/（安装页原文：https://raw.githubusercontent.com/kikipoulet/sukiui/2f4225125894724fb01389e342a5b5f6979fd697/docs/docs/documentation/getting-started/installation.md） | 安装即移除 `Avalonia.Themes.Fluent`；单包形态 |
| W14 | https://github.com/amwx/FluentAvalonia（README：https://raw.githubusercontent.com/amwx/FluentAvalonia/FA3.1/README.md） | WinUI 风格单包 `FluentAvaloniaUI`；MIT |
| W15 | https://docs.ag-ui.com/concepts/events | AG-UI 事件分类：Lifecycle/Text/Tool/State/Activity/Subagent/Special 等 |
| W16 | https://docs.ag-ui.com/sdk/dotnet/abstractions/overview | AG-UI .NET SDK 包划分：`AGUI.Abstractions`/`Formatting`/`Protobuf`/`Client`/`Server`；net10 等 TFM；AOT 兼容 |
| W17 | https://nuget.azure.cn/v3-flatcontainer/semi.avalonia/index.json（200）与 `arc.avalonia/index.json`、`arc.avalonia.agent/index.json`（404） | 镜像可用性验证；Arc 两个候选 ID 推断未注册（**未在 nuget.org 直接复核**） |
| W18 | https://v11.docs.avaloniaui.net/docs/reference/controls/nativemenu/ | `NativeMenu` 只在 macOS 与部分 Linux 发行版可用（v11 文档，2025-07 更新；v12 等价页未找到，机制未验证） |

### 6.4 未验证 / 存疑清单

1. Fluent/Simple 的精确"控件种类数"（~78）由文件数 80/79 去重推导，未逐个核对 `FluentControls.xaml` 等共享文件归属。
2. `Avalonia.Controls.DataGrid.dll` 内嵌主题的具体 XAML 文件清单（仅字符串证据）。
3. "236 控件 / 411 公共类"来自 `docs/agent-life-tools/04-qa-review.zh.md:35`，本轮未重算。
4. `shots/light|dark` 各 233 张 vs `docs/12` 记录 smoke 315 页：截图覆盖口径未验证。
5. NuGet ID 可用性仅用区域镜像验证。
6. Ursa 对 Avalonia 12 的支持情况（README 显示 11.x）。
7. 基础主题的质量/性能/双主题一致性缺少外部可对标基线，本文件的人日为粗估（C 级）。

---

## 附录 A：Avalonia 12.1.4 内置主题 `Controls` 目录主题 XAML 清单（Fluent 80 / Simple 79）

> 来源 [W8][W9]（GitHub Contents API，`release/12.1.4`）。用途：作为"基础主题覆盖清单"的**权威底稿**；Arc 的 B1–B3 波次可按此裁剪/勾选。

**Fluent（80）**：AdornerLayer、AutoCompleteBox、Button、ButtonSpinner、Calendar、CalendarButton、CalendarDatePicker、CalendarDayButton、CalendarItem、Carousel、CarouselPage、CheckBox、ComboBox、ComboBoxItem、CommandBar、ContentPage、ContextMenu、DataValidationErrors、DatePicker、DateTimePickerShared、DrawerPage、DropDownButton、EmbeddableControlRoot、Expander、FluentControls、FlyoutPresenter、GridSplitter、GroupBox、HeaderedContentControl、HyperlinkButton、ItemsControl、Label、ListBox、ListBoxItem、ManagedFileChooser、Menu、MenuFlyoutPresenter、MenuItem、MenuScrollViewer、NavigationPage、NotificationCard、NumericUpDown、OverlayPopupHost、PathIcon、PipsPager、PopupRoot、ProgressBar、RadioButton、RefreshContainer、RefreshVisualizer、RepeatButton、ScrollBar、ScrollViewer、SelectableTextBlock、Separator、Slider、SplitButton、SplitView、TabbedPage、TabControl、TabItem、TableView、TableViewCell、TableViewColumnHeader、TableViewRow、TabStrip、TabStripItem、TextBox、TextSelectionHandle、ThemeVariantScope、TimePicker、ToggleButton、ToggleSwitch、ToolTip、TransitioningContentControl、TreeView、TreeViewItem、Window、WindowDrawnDecorations、WindowNotificationManager。（共 80；其中 `FluentControls`、`DateTimePickerShared` 为共享/聚合文件，推断实际控件目标 ~78。）

**Simple（79）**：与 Fluent 基本同构，差异主要在少一个共享/聚合文件（如 `FluentControls` 对应项），同样包含 `Window.xaml` 与 `WindowDrawnDecorations.xaml`；完整清单见 [W9]。

---

*文件结束。本文件仅用于架构对标与工作量参照；所有"人日/难度档"为粗估（C 级），不构成排期承诺。*
