# B. 组件中间层审计：src/Components 11 个项目

> **范围**：`src/Components` 下 11 个项目（Inputs / Actions / Navigation / Overlays / Feedback / DataDisplay / Charts / Pickers / Effects / Content / Media）。
> **基线**：工作区 `D:\Code\VibeCoding\Arc.Avalonia`，Avalonia 12.1.4 / net10.0（`Directory.Build.props:7`）。
> **方法**：只读 PowerShell + `read`/`grep`/`glob` 静态清点；解析全部 `.csproj` 的 `ProjectReference`；用基类链判定控件；解析 128 份 `Docs/*.json`；抽样 19 个控件源码逐行核对。
> **约束**：未运行 `dotnet build` / `dotnet run` / `tools/*.ps1`（由 Lead 统一验证）。凡涉及运行时渲染行为的判断均标注「未验证」。
> **本报告不改任何源码，只写这一个文件。**
>
> **基线状态说明（重要）**：仓库最新提交为 `5c2513f`（2026-10-29），但**工作树包含未提交的「文档平台迁移 M1–M4」改动**：旧的总览式页面被删除（如 `Actions/Pages/ActionsPage.cs`）、新的「一条目一页」页面与 `Docs/*.json` 为未跟踪新增、部分 `.csproj` 已修改。本报告的**所有计数以工作树当前状态为准**（即迁移后状态），而非 HEAD；在 HEAD 上复算会得到不同的页面/文档数字。审计期间（2026-10-31 03:25–03:45）未运行构建、未改动任何源码。

---

## 0. 一页结论

| 维度 | 实测数字 | 判定 |
|---|---|---|
| 项目数 | 11（`src/Components/*`） | 组件族完整，命名一致 |
| 公开类 | 260 个（147 `sealed`、103 普通 `class`、5 `abstract`、5 `static`；其中 2 个非 Arc 前缀：`PresenceMember`、`ScrubExpression`） | 公开面偏大，含内部部件 |
| **公开控件** | **151 个**（基类链可达 Avalonia 控件）；其中仅 **106 个**有独立 ControlTheme，另有 16 个实为内部视觉部件 | 数量达标，质量分层 |
| 非控件公开类型 | 109 类 + 86 enum + 14 record | 模型层未收敛 |
| 依赖属性 | 913 个 `StyledProperty`，仅 1 个 `DirectProperty` | DP 密度 6.0/控件 |
| 公开事件 | 143 个 **CLR event**，`RoutedEvent.Register` = **0** | 事件模型未框架化 |
| 库代码 LOC | 77,567（163 个 .cs）+ 主题 8,660（97 个 .axaml） | 体量够「框架」 |
| 页面/文档 LOC+字节 | 页面 16,103 LOC（140 文件）+ 文档 662,145 B（128 JSON） | 占文件数 **49.4%**、字节 **29.9%** |
| **库 → Demo 依赖** | **11/11** 个项目 `ProjectReference` `src/Apps/Arc.Avalonia.Demo` | **第一道门槛未过** |
| 打包元数据 | PackageId/Version/Description/Authors/License/RepositoryUrl/PackageReadmeFile = **全仓库 0 处** | **不能发布 NuGet** |
| XML 文档产出 | `GenerateDocumentationFile=false` + 抑制 CS1591（`Directory.Build.props:11-12`） | 写了 1341 段 summary 但不产出 .xml |
| 主题覆盖 | 131 个 `ControlTheme`（106 个 Arc 目标 + 25 个内建控件目标）；**Charts 22 控件 0 主题** | 主题层基本成型，Charts 例外 |
| 通用框架能力差集 | 窗口壳 / 服务层 / 虚拟化 / 无障碍 peer / 本地化 RTL / DnD / MVVM 辅助 = 缺或近似 | 见 §7 |
| 剥离页面+文档的机械量 | 移动 268 个文件、改 22 行 csproj、建 1 个工程、改 1 处内部 API 访问、**必需命名空间改动 0** | **1 人日可做完，2–3 人日可回验** |
| 中间层成为「可发布通用框架」缺口 | 23 项 · **≈156–251 人日（中位 ~200）** | 见 §9 |

**一句话结论**：这层的 **控件密度与主题完成度已经达到框架级（151 控件 / 131 ControlTheme / 913 DP）**，但**发布形态仍是「画廊的伴生库」而不是框架**：库工程直接编译 140 个演示页面、嵌入 128 份文档、依赖 Demo 项目、公开面混入内部部件、没有包元数据与聚合主题入口，跨 6 项基础框架能力（窗口壳/服务层/虚拟化/无障碍/本地化/RTL/MVVM）为空。**页面与文档剥离是最容易、最便宜的一步（不是瓶颈）；真正的瓶颈是发布工程化 + 框架能力补齐。**

---

## 1. 判定：两道门槛

任务提出的问题是「这层能否在去掉 Demo 与 Fluent 依赖后，作为对标通用 UI 框架的中间层对外发布」。审计给出**两道独立门槛**，当前都未过：

| 门槛 | 内容 | 现状 | 结论 |
|---|---|---|---|
| **门槛 1：可打包性** | 库工程不含演示代码/文档/示例数据，有包元数据与版本策略，消费者能一键接入主题 | 11/11 引用 Demo；140 页 + 128 文档在库内；0 包元数据；消费者需手写 `FluentTheme` + 13 条 `StyleInclude`（`src/Apps/Arc.Avalonia.Gallery/App.axaml:7-20`） | **未过** |
| **门槛 2：框架能力** | 能做「一般 UI 开发」：窗口壳、容器/布局、数据展示、浮层、服务层、MVVM、本地化/RTL、无障碍、虚拟化、样式扩展点、DPI | 控件级很强（151 控件、913 DP、131 主题、186 处键盘处理），**框架级基础设施几乎为空**（0 `Window` 类、0 服务接口、0 `AutomationPeer`、0 虚拟化、0 本地化资源、`ICommand` 仅 1 处） | **未过** |

**两道门槛的关系**：门槛 1 是「能不能发」，门槛 2 是「发了有没有人用」。任务里把「剥离页面/文档」称作第一道门槛是准确的——它**便宜**，所以适合先做；但它**不解决**门槛 2。

---

## 2. 范围与方法

### 2.1 计数口径（避免歧义）

| 术语 | 定义 | 备注 |
|---|---|---|
| 控件 | `public` 类的基类链经过本仓库补全后，能到达 Avalonia 控件根（TemplatedControl / ContentControl / Button / Control / Panel） | 脚本对 547 个类声明建依赖图后向上解析 |
| 非控件类 | 其余 `public class`（模型 / Item / Option / Helper / EventArgs） | 含 `record`、`enum` 单独统计 |
| 库代码 | `*.cs`（排除 `Pages/`）+ `*.axaml` | 发布物应该只含这些 |
| 页面 | `Pages/` 下 `*.cs` | 含 12 个演示数据 helper（见 §3.4） |
| 文档 | `Docs/*.json` | 128 份，全部是 `EmbeddedResource` |
| LOC | 物理行数（含空行/注释，`Get-Content` 计数） | 与仓库既有文档口径一致 |

### 2.2 排除项

- `bin/`、`obj/` 全部排除。
- `.recon/`（侦察目录）、`shots/`（截图产物）不在审计范围。
- Agent / Special / Blocks 项目不在本次范围（见 C 报告）。

---

## 3. 逐项目清点

### 3.1 总表

说明：`库 LOC` = 非 Pages 的 `.cs` 行数；`主题` 仅统计 `Themes/*.axaml`；`页/文档占比` = (Pages cs 字节 + Docs json 字节) / 项目内 cs+axaml+json+csproj+assets 总字节。

| 项目 | 公开控件 | 公开类合计 | 库 LOC | 主题文件 | 主题 LOC | ControlTheme | 页面文件 | 页面 LOC | 文档 JSON | 文档字节 | 项目总 KB | 页+文档占比 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Arc.Avalonia.Inputs | **32** | 65 | 15,012 | 20 | 3,260 | 40（27 Arc + 13 内建） | 31 | 4,197 | 29 | 159,027 | 877.4 | 33.2% |
| Arc.Avalonia.Charts | **22** | 23 | 10,765 | 1 | **10** | **0** | 22 | 2,088 | 20 | 101,832 | 506.7 | 32.8% |
| Arc.Avalonia.Pickers | **17** | 28 | 9,277 | 14 | 1,625 | 22（10 Arc + 12 内建） | 10 | 970 | 9 | 48,260 | 443.3 | 18.3% |
| Arc.Avalonia.Actions | **19** | 22 | 6,826 | 10 | 947 | 16 | 20 | 1,923 | 19 | 94,012 | 409.1 | 38.6% |
| Arc.Avalonia.Feedback | **17** | 26 | 6,213 | 14 | 355 | 13 | 17 | 2,048 | 14 | 63,137 | 351.6 | 37.4% |
| Arc.Avalonia.DataDisplay | **7** | 22 | 7,461 | 8 | 251 | 7 | 8 | 1,234 | 7 | 39,132 | 328.7 | 24.7% |
| Arc.Avalonia.Media | **15** | 25 | 5,966 | 9 | 710 | 11 | 9 | 1,449 | 8 | 43,971 | 306.3 | 30.0% |
| Arc.Avalonia.Effects | **9** | 20 | 4,900 | 10 | 536 | 9 | 10 | 893 | 9 | 45,892 | 244.1 | 30.8% |
| Arc.Avalonia.Content | **4** | 9 | 4,901 | 5 | 106 | 4 | 4 | 345 | 4 | 21,203 | 200.1 | 16.2% |
| Arc.Avalonia.Overlays | **5** | 9 | 3,110 | 3 | 526 | 5 | 5 | 588 | 5 | 27,804 | 166.1 | 28.4% |
| Arc.Avalonia.Navigation | **4** | 11 | 3,136 | 3 | 334 | 4 | 4 | 368 | 4 | 17,875 | 139.5 | 21.6% |
| **合计** | **151** | **260** | **77,567** | **97** | **8,660** | **131** | **140** | **16,103** | **128** | **662,145** | **3,972.9** | **29.9%** |

> 公开类合计 = 该项目**非 `Pages/`** 的 `.cs` 中 `public class`（含控件/模型/静态表/`Core/` 辅助类）；不含 `enum`/`record`。
> 主题 LOC 与库 LOC 分开；`ControlTheme` 列含内建控件目标（Button 13 / ListBoxItem 6 / RadioButton 3 / ContentControl 1 / TextBox 1 / ListBox 1 = 25）。

### 3.2 「151 个控件」的准确分解

151 这个数字容易被误读为「151 个可发布控件」。按「有无自己的 ControlTheme + 是否被 catalog 单列」拆分（合计正好 151）：

| 分类 | 数量 | 判定依据 | 例子 |
|---|---:|---|---|
| 有自己 ControlTheme 的模板控件 | **106** | 106 个不同 Arc 类型出现在 `Themes/*.axaml` 的 `ControlTheme TargetType` 中 | ArcInput、ArcDialog、ArcDataGrid、ArcCalendar |
| 自绘图表控件（无 ControlTheme，渲染期解析 token） | **20** | 基类 `ArcChartBase : Control`；`ArcChartsTheme.axaml` 仅 10 行空壳（`:1-10`） | ArcLineChart、ArcDonutChart、ArcSankeyFlow |
| 继承父主题的子类 | **5** | `StyleKeyOverride` 指向父控件 | ArcActionSwap/CopyButton/ThemeSwitcher→ArcButton；ArcChartCard/ArcMetricCard→ArcCard（`StyleKeyOverride` 共 5 处） |
| 抽象基类 | **4** | 无法直接实例化 | ArcFieldBase、ArcTextFieldBase、ArcPickerBase、ArcChartBase |
| **公开的内部视觉部件** | **16** | 无 `ControlTheme`，其中 **14 个**无 catalog 文档（slug 不存在；`elastic-slider`、`range-histogram-slider` 两个有文档） | ArcProgressTrack、ArcUsageBar、ArcPanePanel、ArcColorArea、ArcAvatarRing、ArcSignatureSurface、ArcCarouselPanel、ArcCompareSurface、ArcCalendarSurface、ArcScrollOverlay、ArcAvatarCropSurface、ArcColorRamp、ArcHeatmapGrid、ArcBillingPrice |

证据（内部部件「无主题 + 无文档」的代表）：
- `Arc.Avalonia.Feedback/Controls/ArcProgress.cs:32` `public class ArcProgress`（有主题）与 `:340` `public sealed class ArcProgressTrack : Control`（无主题）；`Themes/ArcProgress.axaml` 只把 `TargetType="ArcProgress"` 作为 ControlTheme 目标。
- `Arc.Avalonia.Media/Controls/ArcResizablePanels.cs:41` `ArcPanePanel : Panel`（布局部件），目录中无 `pane-panel.json`。
- `Arc.Avalonia.Pickers/Controls/ArcColorSurfaces.cs:16` `ArcColorArea` / `:246` `ArcColorRamp` 为 `Control` 子类，无主题、无文档。
- 128 份 `Docs/*.json` 的 slug 与 128 个 `ItemSlug` **一一相等**（见 §4.3.2 校验），所以「没有 slug」是硬证据。

**结论**：对外可承诺的控件面应按 **106 模板控件 + 20 图表 + 5 子类 = 131** 计，而不是 151；另外 16 个公开类型应改为 `internal`（属 API 面污染，见 §5.2.5）。

### 3.3 主题覆盖的一个例外：Charts

```xml
<!-- src/Components/Arc.Avalonia.Charts/Themes/ArcChartsTheme.axaml:1-10 -->
<Styles ...>
  <Styles.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <!-- Component ControlThemes for this project are merged here. -->
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Styles.Resources>
</Styles>
```

- Charts 22 个公开控件、10,765 LOC，**0 个 ControlTheme**，主题入口是 10 行空文件。
- 颜色走 `ChartPalette` 在**渲染期**解析 token（`src/Components/Arc.Avalonia.Charts/Core/ChartCore.cs:64-112`，640 处 `ArcTokens.` 引用的主要来源之一），而不是走 `ControlTheme` 的 `DynamicResource`。
- 后果：**图表无法用 Avalonia 常规方式（改 ControlTheme setter）改样式**，只能改 token 或继承重写 `Render`；`ChartText` 还硬编码 `CultureInfo.InvariantCulture`（`:117`），数字格式不可本地化。

这一点是「框架 vs 组件集」的典型差别：组件集可以自绘，框架必须给出可覆盖的样式契约。

### 3.4 页面文件：128 个条目页 + 12 个演示数据 helper

`Pages/` 共 140 个 `.cs`，其中 **只有 128 个是条目页**（`class X : ArcItemPage`，128/128，无 `: ArcPage` 直系页）：

| 项目 | Pages 文件 | 其中条目页 | 演示 helper（无页面类） |
|---|---:|---:|---|
| Inputs | 31 | 29 | `InputsSections.cs` 355 行、`AdvancedInputsSections.cs` 758 行 |
| Charts | 22 | 20 | `ChartsData.cs` 130 行、`AdvancedChartsData.cs` 110 行 |
| Actions | 20 | 19 | `ActionDemos.cs` 133 行 |
| Feedback | 17 | 14 | `FeedbackDemos.cs` 477、`ProDemos.cs` 325、`PageLayout.cs` 66 |
| Pickers | 10 | 9 | `PickerDemo.cs` 98 行 |
| Effects | 10 | 9 | `EffectDemos.cs` 111 行 |
| Media | 9 | 8 | `MediaDemos.cs` 498 行 |
| DataDisplay | 8 | 7 | `DataDemos.cs` 340 行 |
| Content | 4 | 4 | — |
| Navigation | 4 | 4 | — |
| Overlays | 5 | 5 | — |
| **合计** | **140** | **128** | **12 个文件、3,322 LOC** |

12 个 helper 是纯演示数据构造器（`ActionDemos`、`ChartsData` 等），它们和页面一样被编译进**库程序集**；这是比「页面在库里」更隐蔽的一层污染。

---

## 4. Demo / 页面 / 文档耦合

### 4.1 现状：一个条目 = 三件工件，全部在库工程

```
src/Components/Arc.Avalonia.<X>/
├── Controls/     ← 真正要发布的控件（151 个公开类型）
├── Themes/       ← 要发布的主题（97 文件 / 131 ControlTheme）
├── Pages/        ← 128 条目页 + 12 演示 helper（库工程编译）
├── Docs/         ← 128 份中文文档 JSON（库工程 EmbeddedResource）
└── Assets/       ← Feedback 有 3 张演示头像 PNG（库工程 AvaloniaResource）
```

这是 `docs/10-gallery-doc-platform.zh.md:98` 与 `docs/11-gallery-doc-m1-m2.zh.md:17` 的**有意设计**（「文档随组件项目分发」「页面随项目走」），目的是新增组件不改中央注册表。从画廊角度这是优点；从**框架发布**角度这是根本性缺陷。

### 4.2 体积占比（精确）

全 11 项目合计（542 文件 / 4,068,223 B）：

| 类别 | 文件数 | 字节 | 占比 |
|---|---:|---:|---:|
| 库代码（.cs 非 Pages + .axaml） | 260 | 2,839,885 | 69.8% |
| **演示页面（Pages/*.cs）** | **140** | **552,763** | **13.6%** |
| **文档（Docs/*.json）** | **128** | **662,145** | **16.3%** |
| csproj + 演示 PNG | 14 | 13,430 | 0.3% |
| **页面 + 文档合计** | **268** | **1,214,908** | **29.9%** |

- **按文件数：268 / 542 = 49.4%** 的库工程文件不是发布物。
- 按 LOC：页面 16,103 / 93,670 全部 C# = **17.2%**。
- 文档平均 **5,173 B/份**；结构完整：128/128 都有 `variants`（合计 **669 个变体**）、`api`、`examples`、`relations`、`contrasts`、`whenToUse`（410 条）等 11 个固定键。

> 文档本身是资产，不是垃圾：669 个变体声明同时是「完整性预览」的校验基准（`ArcDocValidator` 用 `ImplementedStates` 双向比对，`docs/11:14`）。所以正确做法是**搬到独立文档工程**，不是删。

### 4.3 剥离影响面：机械工作量评估

这是任务里「库能否作为框架发布」的第一道门槛，结论：**机械上非常轻，因为架构恰好已经准备好了**。

#### 4.3.1 已经准备好的三个条件（关键）

| 机制 | 证据 | 对剥离的意义 |
|---|---|---|
| 页面按反射发现 | `src/Apps/Arc.Avalonia.Demo/ArcPageRegistry.cs:21-32`：扫描 `AppContext.BaseDirectory` 下 `Arc.Avalonia*.dll` | 页面放到任意 `Arc.Avalonia.*` 程序集都能被发现 |
| 文档按嵌入资源发现 | `src/Apps/Arc.Avalonia.Demo/Docs/ArcDocStore.cs:97-167`：扫所有 `Arc.Avalonia*` 程序集的资源名含 `.Docs.` 的 `.json` | 文档搬到新程序集只需逻辑名含 `.Docs.` |
| 审计同样走反射 | `Docs/ArcDocValidator.cs:105-110` 调 `ArcPageRegistry.Discover()` | 不依赖页面所在程序集 |

#### 4.3.2 精确改动清单

| 改动点 | 数量 | 证据 / 说明 |
|---|---:|---|
| 移动页面 `.cs` | **140** | `Pages/**/*.cs`；保持 `namespace Arc.Avalonia.<X>.Pages` 不变即可（.NET 命名空间 ≠ 程序集） |
| 移动文档 `.json` | **128** | `Docs/**/*.json` |
| 从组件 csproj 删除 `ProjectReference` Demo | **11 行** | 11/11 项目各 1 行，如 `Arc.Avalonia.Inputs.csproj:10`、`Arc.Avalonia.Feedback.csproj:11` |
| 从组件 csproj 删除 `EmbeddedResource Docs\*.json` | **11 行** | 如 `Arc.Avalonia.Inputs.csproj:14` |
| 新建文档工程 | 1 | 建议 `src/Apps/Arc.Avalonia.Docs`（或 `Arc.Avalonia.Components.Docs`），`ProjectReference` 11 个组件 + Demo，`EmbeddedResource Include="**/Docs/*.json"` |
| Gallery 增加引用 | 1 行 | `src/Apps/Arc.Avalonia.Gallery/Arc.Avalonia.Gallery.csproj`（现有 27 个引用后追加） |
| **必需的命名空间/using 改动** | **0** | 保持原命名空间；`using Arc.Avalonia.Demo`（135 个页面）继续有效，因为 Demo 契约项目仍是引用目标 |
| 页面访问内部 API 的修复 | **2 处** | ① `Pickers/Pages/DateRangePickerPage.cs:81` 用 `internal static ArcDateMath`（`Controls/ArcDateMath.cs:10`）；② `Pickers/Pages/AvatarUploadPage.cs:31` 用 `ArcSamplePhoto`（本身是 `public`，无需改）。真正要改的只有 **1 处** |
| 页面用 `DemoUi` 帮助方法 | 11 个成员、211 次调用 | Brush 43 / Card 42 / Field 28 / Row 21 / Caption 20 / Section 20 / Column 11 / Body 9 / FixedColumn 8 / Label 6 / Canvas 3（`DemoUi.cs:15-144` 全部 `public static`） |
| 页面用 `ArcItemPage` 受保护成员 | 4 个 override | `ItemSlug` 128、`ImplementedStates` 128、`BuildDemo` 128、`DemoOnCanvas` 8；全部 `public/protected`（`ArcItemPage.cs:20,39,61,64`），跨程序集可用 |
| 页面直接 new 控件 | 大量（预期内） | 这是演示代码的正常写法，不构成耦合；已确认页面**没有**访问控件私有字段（`._` 模式 0 处） |
| 工具链改动 | 0 | `tools/build.ps1` 按解决方案构建；`tools/shots.ps1` 走 Gallery 反射发现，无需改路径 |

#### 4.3.3 结论与估算

- **纯机械剥离 1 人日**：268 个文件移动 + 22 行 csproj 删除 + 1 个新工程 + 1 处内部 API。
- **含回验 2–3 人日**：`dotnet build ArcUI.slnx` + `--audit`（应为 items=294 / docs=294 / pages=294 / errors=0，见 `Gallery/Framework/CatalogAudit.cs:37-40`）+ `--smoke`（315 页）+ `shots.ps1 -Theme both` 双主题基线对比。
- **风险点不是编译，而是发现顺序**：`ArcDocStore` 与 `ArcPageRegistry` 都会 `Assembly.LoadFrom` 目录内 `Arc.Avalonia*.dll`；新文档程序集必须在 Gallery 的 `ProjectReference` 里，否则文件不会出现在输出目录 → 页面与文档「静默消失」，`--audit` 会报 `page.missing`/doc 缺失而不是构建失败。回验必须看 audit 计数，不能只看 exit code。

### 4.4 剥离之后仍然存在的问题（不代表可以发布）

剥离只解决「发布物不含演示代码」。以下 6 项与剥离无关，仍会在源库里：

| # | 问题 | 证据 | 归属 |
|---|---|---|---|
| 1 | 演示人物数据成为**公开 API 默认值** | `Actions/Controls/ArcUserMenu.cs:70` `nameof(UserName), "Maya Chen"`；`:73` `nameof(Email), "maya@acme.com"` | §5.2.4 |
| 2 | 库内公开「示例照片生成器」 | `Pickers/Controls/ArcSamplePhoto.cs:14`（`public static class`，注释自述 "for the demos"） | §5.2.4 |
| 3 | 演示头像 PNG 作为库资源 | `Arc.Avalonia.Feedback.csproj:16` `<AvaloniaResource Include="Assets\**" />`；`Assets/portrait-{leo,maya,sara}.png`，合计 6,475 B | §8-3 |
| 4 | 116 个英文 DP 默认文案 | 见 §5.2.3 | §7-11 |
| 5 | 无 XML 文档产出 | `Directory.Build.props:11-12` | §8-6 |
| 6 | 无包元数据 / 无聚合主题入口 | §8-1、§8-8 | §8 |

---

## 5. 库级 API 质量抽样（19 个控件）

### 5.1 样本总表

| # | 控件 | 文件 | 行数 | 公开面 | 评价（证据） |
|---|---|---|---:|---|---|
| 1 | ArcTextFieldBase | `Inputs/Controls/ArcTextFieldBase.cs` | 370 | 5 DP + `ValueChanged` | **好样本**：token-only（`ArcResources.Brush(ArcTokens.Danger)`，`ArcFieldBase.cs:110`）、状态用 class、`protected virtual OnMessageChanged` 扩展点（`:95`） |
| 2 | ArcInput | `Inputs/Controls/ArcInput.cs` | **9** | 0 | 空壳子类，仅为 API 命名存在；可接受但属冗余类型 |
| 3 | ArcSelect | `Inputs/Controls/ArcSelect.cs` | 414 | 4 DP + 1 event | 依赖内建 `ListBox/ListBoxItem`（`:67,132`）与 `ArcMenuListTheme`（`Themes/ArcPrimitives.axaml:245`）；`IsOpen` 暴露内部弹层状态 |
| 4 | ArcScrubInput | `Inputs/Controls/ArcScrubInput.cs` | 1,179 | 10+ DP + event | **职责过载**：文件底部另有 `public static class ScrubExpression`（`:940`，含表达式解析器与英文错误串 "Enter a number" `:959`） |
| 5 | ArcPhoneInput | `Inputs/Controls/ArcPhoneInput.cs` | 1,182 | 11 DP + 2 event | **数据内嵌**：`public static class ArcPhoneCountries`（`:69`）+ `Example` 英文字段（`:62`），国家表硬编码在控件文件 |
| 6 | ArcDataGrid | `DataDisplay/Controls/ArcDataGrid.cs` | 1,856 | 多 DP + 16 event（项目级） | **自述无虚拟化**：`:115-125` "Deliberately omitted ... row virtualisation. There is no row-number gutter and no horizontal scroll"；列菜单用 `Popup`（`:178,319`） |
| 7 | ArcTreeTable | `DataDisplay/Controls/ArcTreeTable.cs` | 2,355 | 多 DP | **自述无虚拟化**：`:106-108` "no row virtualisation, no height-spring layout"；键盘契约靠注释描述（`:112-114`） |
| 8 | ArcButtonGroup | `Actions/Controls/ArcButtonGroup.cs` | 449 | 7 DP + 0 event | 组合控件用 `Items`(#IList) 而非 `ItemsSource`；无事件，选中靠回写集合 |
| 9 | ArcToastHost | `Actions/Controls/ArcToastHost.cs` | **112** | 0 DP；`Show()/Clear()` | **面板级 host**，不是全局通知服务（`:15-19` "A page drops one host into a panel"） |
| 10 | ArcDialog | `Overlays/Controls/ArcDialog.cs` | 298 | 5 DP + `Closed` | 质量高：Escape + 焦点陷阱 + 关闭后还原焦点（`:177,283`）；但 scrim 来自 Internal（`:110` `ArcActionVisuals.Scrim(0.42)`），且「fills the nearest panel」非窗口级模态（`:22-23`） |
| 11 | ArcTooltip | `Overlays/Controls/ArcTooltip.cs` | 238 | 4 DP + 0 event | **自绘 Canvas 气泡**（`:42-58`），非 `ToolTip`/`Popup`；hover 延迟 + focus 打开 + Escape 关闭（`:23-27`） |
| 12 | ArcAvatar | `Feedback/Controls/ArcAvatar.cs` | 261 | 5 DP + 0 event | `Src` 支持 `avares://`（`:228`）；`ArcAvatarShape` 与 Pickers 重名（见 §5.2.6） |
| 13 | ArcColorPicker | `Pickers/Controls/ArcColorPicker.cs` | 223 | 5 DP + 2 event | `Swatches` 为可变集合 DP；2 个事件（`ValueChanged/SwatchesChanged`）；继承 `ArcPickerBase` |
| 14 | ArcRichTextEditor | `Content/Controls/ArcRichTextEditor.cs` | 1,426 | 9 DP + 1 event | `Html/Markdown` 双格式 API；**slash 菜单 8+ 条英文命令硬编码**（`:65-72` 起），无法本地化/裁剪 |
| 15 | ArcMegaMenu | `Navigation/Controls/ArcMegaMenu.cs` | **2,454** | 13 DP + 2 event，**8 个公开类型挤一个文件** | 最大单文件；导航壳级复合控件混在组件层 |
| 16 | ArcShareAccess | `Overlays/Controls/ArcShareAccess.cs` | 2,040 | 11 DP + 7 event，5 个公开类型 | **页面级 surface**（`:112-113` 自述 "page-hosted surface … not a popup"） |
| 17 | ArcScrollArea | `Media/Controls/ArcScrollArea.cs` | 777 | 14 DP + 1 event | **自绘滚动条** `ArcScrollOverlay : Control`（`:23`），绕过 `ScrollViewer`；无平台滚动语义/惯性/无障碍滚动 API |
| 18 | ArcLineChart | `Charts/Controls/ArcLineChart.cs` | 502 | 8 DP + 0 event | 渲染期解析 token（好）；`ChartText` 硬编码 invariant culture（`ChartCore.cs:117`） |
| 19 | ArcUserMenu | `Actions/Controls/ArcUserMenu.cs` | 1,176 | 12 DP + 1 event | **演示人物默认值**（`:70,73`）；面板 page-hosted（`:61-62`） |

### 5.2 系统性问题（按严重度）

#### 5.2.1 事件模型未框架化：143 个 CLR event，0 个 RoutedEvent

- `public static readonly RoutedEvent` / `RoutedEvent.Register` 全 11 项目 **0 处**；`public event` 143 处（Inputs 39、Pickers 25、DataDisplay 16、Media 13、Actions 12…）。
- 后果：没有冒泡/隧道、没有 class handler、宿主无法在容器级统一监听（对比 `Button.Click` 是 RoutedEvent）。作为框架，用户会期望 `ValueChanged` 能冒泡到表单容器——目前不能。
- 现有 42 处 `RoutedEventArgs` 只是**消费**内建事件（如 `ArcCombobox.cs:422`），不是定义。

#### 5.2.2 模板契约缺失：200 处 `OnApplyTemplate`，0 个 `[TemplatePart]`

- `[TemplatePart]` = 0；`OnApplyTemplate` = 200（含 override 与 base 调用）。
- 模板部件靠 `e.NameScope.Find<T>("Backdrop")` 字符串约定（`ArcDialog.cs:102-106`），没有声明式契约、没有 `PART_` 命名一致性检查（同文件内 `Backdrop/Panel/Title/FooterHost`，DataGrid 用 `PART_ColumnsPopup`，Inputs 用 `Menu`/`MenuPopup` —— 三种风格）。
- 后果：第三方做自定义主题时没有任何可查契约，只能读源码。

#### 5.2.3 本地化/RTL：默认值即英文，无法覆盖

| 证据 | 数量 |
|---|---:|
| `StyledProperty` 的字符串默认值含英文文案（`nameof(X), "Save"` 形式） | **116**（Actions/Charts/Content/Inputs/Feedback 均有） |
| C# 内英文 UI 串启发式计数 | 452 |
| `CultureInfo.InvariantCulture` / `CurrentCulture` | **59 / 5** |
| `II18n`、`.resx`、`ResourceManager`、`Language.` 等本地化设施 | **1** 处（且非本地化） |
| `FlowDirection` / `TextDirection` | 9 处 |
| RTL 专项（`Rtl`/`RightToLeft`） | **0** |

典型样本：`ArcButtonGroup`/`ArcConfirmMorph` 的 `"Delete"/"Cancel"/"Retry"`（`ArcConfirmMorph.cs:57-78`）；`ArcUserMenu` 的 `"Maya Chen"`（`:70`）；`ArcRichTextEditor` 的 8+ 条 slash 命令（`:65-72`）。

#### 5.2.4 演示数据泄漏进发布面

- `ArcUserMenu.cs:70,73`：公开 DP 默认值就是演示人物姓名/邮箱。
- `Pickers/Controls/ArcSamplePhoto.cs:14`：`public static class`，注释明说 "for the demos"。
- `Feedback` 的 3 张 demo 头像 PNG 作为库 `AvaloniaResource`（`Arc.Avalonia.Feedback.csproj:16`）。
- 页面对内部 API 的依赖只有 1 处需要改（`DateRangePickerPage.cs:81` → `ArcDateMath`），说明演示代码大多只用 public 面——这是剥离容易的又一证据。

#### 5.2.5 公开面污染：16 个内部部件是 public

见 §3.2。另有两类不一致：

| 不一致 | 证据 |
|---|---|
| 10 个 `*Size` 枚举语义不统一 | `ArcFieldSize`（Sm/Md/Lg，`ArcFieldBase.cs:9`）、`ArcAvatarSize`（`ArcAvatar.cs:16`）、`ArcPresenceSize`、`ArcAccordionSize`、`ArcButtonGroupSize`、`ArcExpandingButtonSize`、`ArcFloatingButtonSize`、`ArcProgressSize`、`ArcBillingToggleSize`、`ArcScrubInputSize` —— 共 **10 个 `*Size` 枚举**，无共享尺寸词汇表 |
| 属性集合类型不统一 | `IList<>` 147 / `IReadOnlyList<>` 273 / `IEnumerable<>` 104 / `IList<string>` 15 / `IReadOnlyList<string>` 51 |
| 命名不一致 | 公开类型 `PresenceMember`（无 Arc 前缀，`ArcPresenceStack.cs:44`）、`ChartSeries/ChartValue/ChartTone/SlopeItem/GaugeThreshold`（Charts 无前缀，`ChartCore.cs:17-39`）、`ScrubExpression`（`ArcScrubInput.cs:940`） |
| 重名枚举 | `ArcAvatarShape` 在 `Feedback/Controls/ArcAvatar.cs:32` 与 `Pickers/Controls/ArcAvatarUpload.cs:24` 各定义一次 |
| `public` 字段 | 仅 5 处，且都在 `private sealed class Face` 内（`ArcPresenceStack.cs:98-110`）→ **公开类型上 0 个 public 字段**（好） |

#### 5.2.6 主题 key 硬编码在 C#

9 处控件代码用字符串查找 ControlTheme：`TryFindResource("ArcSegmentOptionTheme")`（`ArcBillingToggle.cs:161`）、`"ArcChipTheme"`（`ArcMoneyInput.cs:423`）、`"ArcIconButtonTheme"`（`ArcTagInput.cs:288`）、`"ArcPickerSwatchTheme"/"ArcPickerIconTheme"`（`ArcColorEditor.cs:419-420`、`ArcFileDropzone.cs:567`、`ArcSignaturePad.cs:746`）、`"ArcEmojiRecentTheme"`（`ArcEmojiPicker.cs:1374`）、`"ArcCardOptionTheme"`（`ArcRadioCards.cs:163`）。

这些 key 既没导出为常量，也没写进任何文档；改主题 key 会静默退化为无主题控件。框架应给出 `ArcThemeKeys` 之类常量表 + 文档。

#### 5.2.7 依赖属性默认值硬编码（token 旁路）

- 64 处 `AvaloniaProperty.Register<T, V>(nameof(X), <数字字面量>)`，如 `Size = 24`（`ArcIconMorph.cs:60`）、`Duration = 4500`（`ArcToast.cs:32`）、`RingThickness = 22`（`ArcDonutChart.cs:29`）。
- 尺寸/栅格常量同样硬编码：`RowHeight = 40`（`ArcDataGrid.cs:128`）、`RowHeight = 44 / RowContentHeight = 40 / Indent = 20 / CheckColumnWidth = 44`（`ArcTreeTable.cs:118-122`）。
- 颜色本身基本 token 化（`Brushes.Transparent` 是中性值，不算违规）；真正硬编码颜色只有 Charts 的 alpha 派生（`ChartCore.cs:90`）与 2 处 `Color.FromArgb`（`ArcAdvancedChartModels.cs:81`、`ArcBrushChart.cs:1326`）。

#### 5.2.8 无障碍：有 Name，缺语义

| 指标 | 数量 |
|---|---:|
| `AutomationProperties.SetName` | 312 |
| `AutomationProperties.SetHelpText` | 103 |
| `AutomationProperties.SetLiveSetting` | 18 |
| `AutomationProperties.SetAutomationId` | **0** |
| `AutomationProperties.SetControlType` | **0** |
| `AutomationPeer` / `ICustomAutomationPeer` 实现 | **0** |
| `KeyboardNavigation.*` 显式配置 | **0** |
| 键盘处理（`OnKeyDown`/`KeyDown +=`） | 186 |
| `TabIndex`/`IsTabStop` | 53 |
| `:focus-visible` 选择器（axaml） | 52 |
| `FocusAdorner` | 19 |

即：**能命名、能键盘操作，但没有角色/值模式/网格语义**。`ArcDataGrid`/`ArcTreeTable`/`ArcTabs`/`ArcSlider`/`ArcSelect` 在屏幕阅读器里不会以 grid/tablist/slider/combobox 角色出现（未验证具体朗读结果，但 peer 层为空是静态事实）。

#### 5.2.9 XML 文档：写了，但不产出

- 1341 个 `/// <summary>`；163 个非 Pages `.cs` 中只有 7 个完全没有 doc。
- 但 `GenerateDocumentationFile=false`、`NoWarn ... CS1591`（`Directory.Build.props:11-12`）→ 发布包不会带 `.xml`，消费者 IntelliSense 拿不到这些注释。
- 结论：这是**打包配置问题，不是写作问题**，修起来几乎零成本（改 1 行 + 清理 CS1591 告警）。

---

## 6. 跨项目依赖与「组件族」层次

### 6.1 实际依赖图（解析 csproj）

```
Arc.Avalonia (Foundation) ← 全部 11 个
Arc.Avalonia.Internal      ← Actions / Navigation / Overlays / Feedback / DataDisplay（5）
Arc.Avalonia.Demo          ← 全部 11 个（11/11）          ← 违规
Arc.Avalonia.Actions       ← Navigation / Overlays（2）
Arc.Avalonia.Feedback      ← DataDisplay（1）
```

独立项目（只依赖 Foundation + Demo）：**Inputs / Charts / Content / Effects / Media / Pickers** 6 个 → 分层干净。

### 6.2 跨项目引用里只有 1 个是真的

| 引用 | 库代码实际使用 | 结论 |
|---|---|---|
| `Navigation → Actions` | 只有 `Pages/MegaMenuMorphPage.cs:5` `using Arc.Avalonia.Actions.Pages` | **纯演示依赖**，剥离后应删除 |
| `Overlays → Actions` | 只有 4 个页面 `using Arc.Avalonia.Actions.Pages`（`DialogPage:6`、`DrawerPage:5`、`PopoverPage:6`、`TooltipPage:5`） | **纯演示依赖**，剥离后应删除 |
| `DataDisplay → Feedback` | `GlobalUsings.cs:2` + `ArcDataGrid.cs:1036`、`ArcSortableDataTable.cs:531`、`ArcTreeTable.cs:1276` 的 `ArcEmptyState` | **真实依赖**，保留 |

即：当前 `Navigation/Overlays → Actions` 的 csproj 引用是页面带出来的假依赖；剥离页面后组件族的真实依赖只剩 `DataDisplay → Feedback` 与 5 个 → `Internal`。

### 6.3 Internal 是隐藏的第二层，且公开面混在一起

`Foundation/Arc.Avalonia.Internal` 被 5 个项目引用，同时通过 `InternalsVisibleTo` 打开内部：

```csharp
// src/Foundation/Arc.Avalonia.Internal/AssemblyInfo.cs:3-7
[assembly: InternalsVisibleTo("Arc.Avalonia.Actions")]
[assembly: InternalsVisibleTo("Arc.Avalonia.Navigation")]
[assembly: InternalsVisibleTo("Arc.Avalonia.Overlays")]
[assembly: InternalsVisibleTo("Arc.Avalonia.Feedback")]
[assembly: InternalsVisibleTo("Arc.Avalonia.DataDisplay")]
// src/Components/Arc.Avalonia.Pickers/AssemblyInfo.cs:3
[assembly: InternalsVisibleTo("Arc.Avalonia.LifeToday")]   // 下游产品
```

共享代码的调用量（非 Pages 代码）：

| 内部/共享类型 | 引用次数 | 分布 |
|---|---:|---|
| `FeedbackUi.*` | 150 | Feedback 76 / DataDisplay 74 |
| `ArcActionVisuals.*` | 69 | Actions 28 / Navigation 23 / Overlays 18 |
| `ArcIconButton` | 12 | DataDisplay 9 / Feedback 2 / Inputs 1 |
| `ArcMenuSurface` | 4 | Actions 2 / Overlays 2 |

问题：`ArcIconButton`、`ArcMenuSurface` 在 `Arc.Avalonia.Internal` 里是 **public**（`Internal/Controls/ArcIconButton.cs`、`ArcMenuSurface.cs:22`）——一个叫 `.Internal` 的程序集对外暴露 public API，且 5 个组件通过 `InternalsVisibleTo` 依赖它们的内部实现。**发布时要么把 Internal 也发成包（名字与语义都别扭），要么把共享代码下沉到 Foundation**。这会在 §9 计为「发布工程化」的一部分。

---

## 7. 对标通用 UI 框架的能力差集

判定基准：不是「Arc catalog 覆盖了多少」，而是「一个团队拿它写一般业务 UI 时会不会立刻撞墙」。档位：**S = ≤3 人日，M = 4–10，L = 11–25**（不含控件本身的既有开发量）。

| # | 框架能力 | 现状 | 已有近似物（file:line） | 缺失点 | 档位 |
|---|---|---|---|---|---:|
| 1 | 窗口壳 / 标题栏 / 自绘 chrome / 系统菜单 | **完全缺失** | 无（Components + Foundation 内 0 个 `Window/TitleBar/Chrome` 类；`GalleryWindow.cs` 属应用层） | 拖动区、最小化/最大化/关闭、系统菜单、Win/mac/Linux 差异、`ExtendClientArea` | L（8–14） |
| 2 | SplitView / Splitter / 可拖分栏 | **近似** | `Media/Controls/ArcResizablePanels.cs`（`ArcResizablePanels` + `ArcPanePanel`，833 行） | 折叠状态、嵌套分栏、键盘调整、记忆布局 | S–M（3–5） |
| 3 | 布局原语包（Stack/Wrap/Grid presets、间距标度） | **缺失（依赖 Avalonia 内建）** | 各控件直接用 Avalonia panel；`DemoUi` 提供演示布局但属 Demo | Arc 间距/栅格标度、响应式断点容器 | S–M（3–6） |
| 4 | TabControl 级内容容器 | **近似** | `Navigation/Controls/ArcTabs.cs:20-44`（触发条 + 就地面板切换 + 方向淡入） | `ItemsSource`/DataTemplate、懒加载、内容虚拟化、可访问 tablist 语义 | S–M（3–6） |
| 5 | 数据网格：编辑 + 虚拟化 + 列固定 + 剪贴板 | **编辑有、虚拟化无** | `DataDisplay/Controls/ArcDataGrid.cs`（行内编辑、Shift 选区、列宽拖拽、列显隐、footer 汇总）；自述缺失清单 `:115-125` | 行虚拟化、水平滚动、列固定、剪贴板矩阵、undo、分组、导出、服务端数据 | L（15–25） |
| 6 | 树 / 树表虚拟化 + 异步加载 | **无虚拟化** | `DataDisplay/Controls/ArcTreeTable.cs:97-115`（层级排序/三态选择/懒加载子节点，2,355 行）；`ArcTreeView` | 行虚拟化、大节点集、搜索过滤、拖拽重排 | M（8–12） |
| 7 | 拖放（通用 DnD 行为/指示器/处理器） | **仅文件拖入** | 仅 Pickers：`ArcFileDropzone.cs`、`ArcAvatarUpload.cs` 共 32 处 `DragDrop/DragEventArgs` | 通用拖拽源/目标、插入指示线、重排（`ArcReorderableList` 仅内部按钮）、跨控件 DnD 契约 | M（6–10） |
| 8 | 文件对话框 / 存储服务抽象 | **控件直连平台** | `ArcFileDropzone.cs:307-318` 直接 `TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync(...)`；`ArcAvatarUpload.cs:304-309` 同 | `IFilePickerService` 注入、无头测试替身、最近路径/过滤器模板 | S–M（2–4） |
| 9 | 对话框 / 通知 / 导航服务层 | **完全缺失** | 无（0 个 `IDialogService/INotificationService/INavigationService`）；`ArcToastHost.cs:15-19` 是面板级 host；`ArcDialog.cs:22` 自述 fills nearest panel | 窗口级 Overlay host、服务接口 + DI、排队/去重/生命周期、无头可测 | M–L（10–15） |
| 10 | 命令与 MVVM 辅助 | **几乎缺失** | `ICommand` 全 11 项目仅 **1** 处（`ArcFloatingButtonGroup.cs:77`）；0 `CommandProperty`；全项目仅 1 个 `DirectProperty`（`ArcScrollArea`），无 ViewModelBase/集合视图/验证适配 | `ICommand` 属性、`INotifyDataErrorInfo` 校验桥、`CollectionView`（排序/过滤/分页） | M（6–10） |
| 11 | 本地化 / RTL | **完全缺失** | 116 个英文 DP 默认值、452 处英文串、59 处 invariant culture、0 RTL 专项（§5.2.3） | 资源字典/`ILocalizer`、字符串外置、RTL 镜像审查（151 控件）、`FlowDirection` 契约 | L（12–18） |
| 12 | 无障碍基础设施 | **半成品** | 312 SetName / 103 SetHelpText / 18 LiveSetting / 52 `:focus-visible` | 0 AutomationPeer、0 ControlType、0 AutomationId、grid/tree/tablist/slider 角色与值模式、读屏回归 | L（15–20） |
| 13 | 焦点 / tab 序列 / 焦点作用域 | **弱** | `TabIndex/IsTabStop` 53 处；Dialog/Drawer 有焦点陷阱与还原（`ArcDialog.cs:177,283`） | `KeyboardNavigation` 0 处显式配置；无 `FocusScope` 策略；无“Tab 走出浮层”的统一契约 | M（6–10） |
| 14 | 控件级键盘导航契约 | **逐控件手写** | 186 处键盘处理；TreeTable 自述支持 arrows/Home/End/PageUp/PageDown/`*`/Space/Enter/Ctrl+A/typeahead（`:112-114`） | 无共享键盘行为基类/约定文档；各控件重复实现，行为一致性无法保证 | M（6–10） |
| 15 | 样式扩展点（StyleKey/ControlTheme 覆盖文档） | **有主题、无契约** | 131 个 ControlTheme；`StyleKeyOverride` 仅 5 处（子类）；25 个主题目标是内建控件（Button 13/ListBoxItem 6/RadioButton 3/TextBox 1/ListBox 1/ContentControl 1） | 主题 key 常量表、部件名契约（`[TemplatePart]`=0）、Charts 不可主题化、无“如何覆盖”文档 | M（5–8） |
| 16 | DPI / 缩放 | **未处理** | `RenderScaling/Dpi` 0 处；像素常量硬编码（`ArcDataGrid.cs:128`、`ArcTreeTable.cs:118-122`） | 1.25/1.5/2x 下的线宽/圆角/图标/行高适配、`Crisp` 逻辑只覆盖图表（`ChartCore.cs:60`） | M（6–10） |
| 17 | 平台窗口/系统服务 | **几乎没有** | `CapsLockState.cs:11` 是唯一 P/Invoke（user32，已 `OperatingSystem.IsWindows` 守卫）；`ArcThemeManager.ReducedMotion` 已读系统动效 | 窗口装饰、原生菜单、系统强调色/主题监听、平台能力探测层 | M（4–8） |
| 18 | 聚合主题入口（消费者一键接入） | **缺失** | 消费者需手写 `FluentTheme` + **13 条** `StyleInclude`（`Gallery/App.axaml:7-20`） | `ArcTheme` 聚合（单文件/单包），或 `UseArcTheme()` 扩展方法 | S（1–3） |
| 19 | 组件测试与契约测试 | **0** | `tests/Arc.Avalonia.Tests/Arc.Avalonia.Tests.csproj` 只引用 Foundation + Demo；151 控件 0 测试 | 单元/布局/主题矩阵/读屏/回归基线 | L（15–25） |
| 20 | 响应式 / 断点 / 容器查询 | **缺失** | 无（仅 `ArcTreeTable` 注释自述没有 hideBelow，见 `:106-107`） | 断点系统、列隐藏策略、窗口宽度驱动布局 | M（5–8） |
| 21 | 图标系统 | **有（160 个 Lucide）** | `Foundation/Arc.Avalonia/Icons/ArcIconData.cs`（160 条路径）；`ArcIcon` 自绘（`ArcIcon.cs:15-35`） | 全 Lucide 覆盖（~1600）、图标搜索/命名规范 | S（2–3，可选） |

---

## 8. 发布形态评估：现在能打成 NuGet 吗？

**不能。** 以下 10 项每一项都足以让 `dotnet pack` 产物不可用（或产生误导性包）。

| # | 阻断项 | 证据（file:line） | 影响 |
|---|---|---|---|
| 1 | **零包元数据** | 全仓库 `.csproj` 中 `PackageId/Version/VersionPrefix/Description/Authors/PackageLicenseExpression/RepositoryUrl/PackageTags/PackageReadmeFile/IsPackable/GeneratePackageOnBuild` 命中 **0** 处；`Directory.Build.props:1-15` 只有 TFM/Nullable/Avalonia 版本 | 打包得到默认 `Arc.Avalonia.Inputs.1.0.0.nupkg`，无描述/作者/许可证/仓库；11 个包无统一版本策略 |
| 2 | **库依赖 Demo 应用工程** | 11/11 csproj 有 `ProjectReference ..\..\Apps\Arc.Avalonia.Demo`：`Inputs:10`、`Feedback:11`、`Actions`/`Charts`/`Content`/`DataDisplay`/`Effects`/`Media`/`Navigation`/`Overlays`/`Pickers` 同型 | pack 会把 Demo 变成包依赖（一个 WinExe 画廊辅助项目），或直接失败；包依赖树不可发布 |
| 3 | **演示页面 + 演示数据编译进库** | 140 个 `Pages/*.cs`（16,103 LOC）；`ArcUserMenu.cs:70,73` demo persona；`ArcSamplePhoto.cs:14`；`Feedback.csproj:16` 打包 3 张 demo 头像 | 消费者下载的 DLL 里带画廊页面与示例数据；反射发现逻辑会把它们当正式类型 |
| 4 | **文档作为嵌入资源进 DLL** | 11/11 csproj `<EmbeddedResource Include="Docs\*.json" />`（如 `Inputs:14`）；662,145 B / 128 份 | 每个包多背 10–160 KB 文档资源，与程序集版本耦合，无法单独升版文档 |
| 5 | **InternalsVisibleTo 交叉** | `Internal/AssemblyInfo.cs:3-7`（→5 个组件）+ `Pickers/AssemblyInfo.cs:3`（→下游 `Arc.Avalonia.LifeToday`） | 内部实现契约被 5 个包 + 1 个产品锁死；任何内部重构都是跨包破坏性变更 |
| 6 | **不产出 XML 文档** | `Directory.Build.props:11` `NoWarn ... CS1591`；`:12` `GenerateDocumentationFile=false` | 1341 段 summary 白写，消费者无 IntelliSense；框架包的门面缺失 |
| 7 | **无 LICENSE / NOTICE / 第三方许可声明** | 根目录无 `LICENSE`（`Test-Path LICENSE` = False）；`README.md:257-270` 提到 Avalonia(MIT)/Lucide(ISC)/Inter(OFL)/Arc UI(MIT) 但无随包声明 | NuGet 合规缺失；再分发 Lucide/Inter 需要随附许可文本 |
| 8 | **无聚合主题入口 / 无 meta 包** | `Gallery/App.axaml:7-20` 需手写 `FluentTheme` + 13 条 `StyleInclude`（1 基础主题 + 1 Internal + 11 组件） | 消费者接入成本高、易漏；没有「装一个包就能用」的路径 |
| 9 | **无 README（包级）/ 无发布流水线** | `src/**` 下 0 个 `README.md`、0 个 `LICENSE*`；无 `.github/`；`NuGet.config:5-8` `<clear/>` 离线源 | 无 CI、无 push 目标、无 `PackageReadmeFile`；无法持续发版 |
| 10 | **无组件测试工程** | `tests/Arc.Avalonia.Tests` 仅引用 Foundation + Demo；0 个组件/主题/无障碍测试 | 发布后回归全靠 `--audit`/`--smoke`/截图（应用层），包级契约无保障 |

> 补充（不是阻断项但需注意）：`ArcUI.slnx` 把 11 个组件与 6 Special、6 Blocks 平铺，没有「框架包集合」的解决方案过滤（`ArcUI.slnx:6-17`），发布脚本需自己挑项目。

---

## 9. 缺口表：中间层成为「可发布通用框架」

档位同上：**S ≤3 / M 4–10 / L 11–25 人日**。人日为**增量工作量**（不含已完成的 151 控件开发）。

### 9.1 P0 发布门槛（不发不能叫框架）

| ID | 缺口 | 内容 | 档位 | 人日 |
|---|---|---|---|---:|
| P0-1 | 包元数据与版本策略 | 统一 `Directory.Build.props` 元数据、11 个 `PackageId`、`IsPackable`、版本号、`PackageReadmeFile`、License/SourceLink/符号 | M | 5–8 |
| P0-2 | 页面/文档剥离 | 268 文件移动 + 22 行 csproj + 1 新工程 + 1 处内部 API（§4.3） | M | 3–5 |
| P0-3 | Demo 残留清理 | 移除 persona 默认值、`ArcSamplePhoto`、demo 头像、`Pages` helper 类；补中性默认值 | S | 2–3 |
| P0-4 | API 面收敛 | 16 个部件 internal 化；`ArcAvatarShape` 去重；10 个 Size 枚举收敛为 `ArcSize`；集合类型统一；命名规范 | M | 8–12 |
| P0-5 | 输出 XML 文档 + 主题 key 常量表 | 打开 `GenerateDocumentationFile`；`ArcThemeKeys` 常量替换 9 处字符串查找 | S–M | 3–5 |
| P0-6 | 聚合主题入口 | `ArcTheme.axaml`（合并 11 个组件主题 + Internal 主题）+ `UseArcTheme()`；`ArcAvalonia` meta 包 | S | 1–3 |
| | **P0 小计** | | | **22–36** |

### 9.2 P1 框架能力（决定「像不像框架」）

| ID | 缺口 | 内容 | 档位 | 人日 |
|---|---|---|---|---:|
| P1-1 | 窗口壳 / 标题栏 | 自绘 chrome、平台差异、DPI、系统菜单 | L | 8–14 |
| P1-2 | 服务层（对话框/通知/导航/文件） | 接口 + 窗口级 overlay host + DI + 无头替身；改造 3 个直连 `StorageProvider` 的控件 | M–L | 10–15 |
| P1-3 | 数据网格虚拟化与编辑完善 | 行虚拟化 + 水平滚动/列固定 + 剪贴板 + 撤销/导出（按 `ArcDataGrid.cs:115-125` 清单） | L | 15–25 |
| P1-4 | 树/树表虚拟化 | `ArcTreeTable`/`ArcTreeView` 大节点集、异步子节点 | M | 8–12 |
| P1-5 | 无障碍基础设施 | 15–20 个关键控件的 AutomationPeer + 角色/值模式 + AutomationId + 读屏回归 | L | 15–20 |
| P1-6 | 本地化 / RTL | 116 默认值 + 452 字符串外置；`ILocalizer`；RTL 镜像审查 151 控件 | L | 12–18 |
| P1-7 | 事件与模板契约 | 关键控件引入 RoutedEvent；`[TemplatePart]` + 部件命名规范 + class handler | M | 6–10 |
| P1-8 | 焦点 / tab / 键盘契约 | FocusScope 策略、浮层 Tab 走位、共享键盘行为基类 + 文档 | M | 6–10 |
| P1-9 | 容器与布局补全 | SplitView 硬化、Tab 容器 DataTemplate/懒加载、响应式断点容器 | M | 6–10 |
| P1-10 | 拖放基础设施 | 通用 DnD 契约 + 插入指示器 + 重排 | M | 6–10 |
| P1-11 | MVVM/命令/集合视图 | `ICommand` DP、校验桥（`INotifyDataErrorInfo`）、CollectionView（排序/过滤/分页） | M | 6–10 |
| P1-12 | Charts 主题化 | Charts 项目 22 个公开控件（20 个自绘图表 + 卡片子类 + 基类）改为可覆盖样式（ControlTheme 或 StyleKey + 主题 token 映射） | M | 6–10 |
| P1-13 | DPI/像素常量审计 | 行高/图标/线宽/圆角在 1.25–2x 下验证；抽出标度常量 | M | 6–10 |
| P1-14 | 平台服务抽象 | 窗口装饰、系统菜单、强调色/动效监听；`CapsLockState` 抽象 | M | 4–8 |
| | **P1 小计** | | | **114–182** |

### 9.3 P2 工程质量与可选

| ID | 缺口 | 内容 | 档位 | 人日 |
|---|---|---|---|---:|
| P2-1 | 组件测试与契约测试 | 151 控件 × 双主题 × 3 尺寸的布局/属性/键位/读屏基线 | L | 15–25 |
| P2-2 | 示例工程（Samples） | 可运行的 `samples/`（非画廊）：登录页/设置页/数据页各一 | S | 3–5 |
| P2-3 | 图标覆盖扩展 | 160 → 全 Lucide（可选） | S | 2–3 |
| | **P2 小计** | | | **20–33** |

### 9.4 分档合计

| 档 | 项数 | 人日 |
|---|---:|---:|
| P0 发布门槛 | 6 | **22–36** |
| P1 框架能力 | 14 | **114–182** |
| P2 工程质量/可选 | 3 | **20–33** |
| **合计** | **23** | **≈156–251（中位 ~200）** |

**建议切分**：
- **里程碑 1（P0，~1 个月/2 人）**：先让库「能发且可信」——包元数据、剥离、API 收敛、XML 文档、聚合主题。这一步做完就有一个可发布但能力偏「组件集」的框架。
- **里程碑 2（P1 前半，~2 个月/2–3 人）**：窗口壳 + 服务层 + 数据网格/树虚拟化（这三项决定「能不能写业务应用」）。
- **里程碑 3（P1 后半 + P2，~3 个月/2–3 人）**：无障碍、本地化/RTL、事件/模板契约、测试基线。

> 说明：人日是**净增量**粗估，按「熟悉 Avalonia 12 的 1 名工程师」为 1 人日基准；无障碍与本地化的下界前提是有审计清单（本报告 §5.2.3/§5.2.8 可直接当清单）。

---

## 10. 三个最容易被低估的项

1. **无障碍的「自动化对等」被 312 处 `SetName` 误导。**
   看起来命名很全（312 SetName + 103 HelpText + 18 LiveSetting），但 **0 个 AutomationPeer、0 个 ControlType、0 个 AutomationId**（§5.2.8）。网格/树/标签页/滑块/下拉在无障碍树里没有角色与值模式，不是「补几个 Name」而是**逐控件写 peer + 值模式 + 回归**，还要和 §7-13 的焦点契约一起做；15–20 人日只是下界。

2. **本地化/RTL 被当成「替换字符串」。**
   116 个英文 DP 默认值就是**公开 API 的默认值**（改默认值是行为变更），452 处英文串分散在 151 个控件，59 处 invariant culture 影响数字/日期/货币显示，`ArcPhoneCountries`/`ArcEmojiData` 还内嵌了带英文名的大数据表（§5.2.3）。真正的成本在**RTL 镜像审查（151 控件 × 双主题截图基线全部要重拍）**与**默认值语义迁移**，而不是翻译。

3. **主题「可覆盖性」的契约化。**
   131 个 ControlTheme 看着很完整，但：Charts 22 控件 0 主题且渲染期解析 token（`ChartCore.cs:64-112`）；9 处 C# 硬编码主题 key（§5.2.6）；`[TemplatePart]`=0、三种部件命名风格（§5.2.2）。一旦对外承诺「主题可覆盖」，这些都会变成**破坏性变更的来源**——用户改不动图表、改了 key 静默失效、自定义主题没有契约可依。把 131 个主题从「能跑」变成「能承诺」比新增 20 个控件更耗时。

> **反过来，最容易被高估的是「剥离页面/文档」**：268 个文件移动 + 22 行 csproj，架构（反射发现）已经支持，1 人日机械完成、2–3 人日回验（§4.3）。它是「必要但不难」的第一步，不应成为路线图里的重量级项。

---

## 11. 建议的执行顺序（并入路线图）

1. **第 1 周**：P0-2 剥离 + P0-3 残留清理（可并行、互不冲突），立刻让 11 个库工程「只含库」。
2. **第 2–3 周**：P0-1 包元数据 + P0-5 XML 文档 + P0-6 聚合主题，产出第一批可 `dotnet pack` 的包（内部 feed）。
3. **第 4–6 周**：P0-4 API 收敛（internal 化 / Size 统一 / 命名），这会改公开面，**必须在发第一个 beta 之前**做。
4. **第 7 周起**：P1-1/P1-2/P1-3（窗口壳 + 服务层 + 数据网格虚拟化）并行立项——这是「组件集 → 框架」的分水岭。
5. 无障碍与本地化（P1-5/P1-6）安排在公开 beta 之后、1.0 之前，配合 P2-1 测试基线一起做。

---

## 12. 证据索引

### 12.1 结构与配置

| 证据 | 位置 |
|---|---|
| 11 个组件项目清单 | `ArcUI.slnx:6-17`；`src/Components/` |
| 统一 TFM / Avalonia 版本 | `Directory.Build.props:3,7` |
| 无包元数据 | 全仓库 csproj `PackageId/Version/Description/Authors/License/RepositoryUrl` 命中 0 |
| 不产出 XML 文档 | `Directory.Build.props:11`（NoWarn CS1591）、`:12`（GenerateDocumentationFile=false） |
| 离线 NuGet 源 | `NuGet.config:5-9` |
| 无 LICENSE / 无 .github | 根目录 `Test-Path LICENSE`=False、`.github` 不存在 |
| 依赖声明（MIT/Avalonia/Lucide/Inter） | `README.md:257-270` |
| 消费者需手写 12 条主题 include | `src/Apps/Arc.Avalonia.Gallery/App.axaml:7-20` |

### 12.2 耦合与剥离

| 证据 | 位置 |
|---|---|
| 11 个项目都引用 Demo | `Inputs.csproj:10`、`Feedback.csproj:11`（其余 9 个同型第 10/11 行） |
| 11 个项目都嵌入 Docs | `Inputs.csproj:14`、`Feedback.csproj:20`（其余同型） |
| 演示头像作为库资源 | `Feedback.csproj:16` + `Feedback/Assets/portrait-{leo,maya,sara}.png`（6,475 B） |
| 页面契约 | `Demo/ArcPage.cs:16-34`、`Demo/ArcItemPage.cs:14-89`、`Demo/DemoUi.cs:15-144` |
| 反射发现页面 | `Demo/ArcPageRegistry.cs:21-32` |
| 反射发现文档 | `Demo/Docs/ArcDocStore.cs:97-167`（扫描 `.Docs.` 资源） |
| 审计依赖同一发现机制 | `Demo/Docs/ArcDocValidator.cs:105-110` |
| 设计意图（文档随组件走） | `docs/10-gallery-doc-platform.zh.md:98`、`docs/11-gallery-doc-m1-m2.zh.md:17` |
| 128 页 ↔ 128 文档 slug 一一对应 | `ItemSlug` 提取 128 个；`Docs/*.json` 128 个；差集为空 |
| 页面只碰 1 处 internal | `Pickers/Pages/DateRangePickerPage.cs:81` → `Controls/ArcDateMath.cs:10` |
| 页面无私有字段访问 | `._` 模式在 140 个页面中 0 次 |
| DemoUi 调用分布 | Brush 43 / Card 42 / Field 28 / Row 21 / Caption 20 / Section 20 / Column 11 / Body 9 / FixedColumn 8 / Label 6 / Canvas 3 |

### 12.3 API 质量

| 证据 | 位置 |
|---|---|
| 913 StyledProperty / 1 DirectProperty / 143 event | 全项目 grep 计数 |
| 0 RoutedEvent.Register | 全项目 grep 计数（42 处 `RoutedEventArgs` 均为消费） |
| 0 `[TemplatePart]` / 200 `OnApplyTemplate` | 全项目 grep 计数 |
| 部件查找字符串风格不一致 | `ArcDialog.cs:102-106`（Backdrop/Panel）、`ArcDataGrid.cs:319`（PART_ColumnsPopup）、`ArcCombobox.cs:143`（MenuPopup） |
| 116 个英文 DP 默认值 | 如 `ArcConfirmMorph.cs:57-78`、`ArcUserMenu.cs:70,73`、`ArcCopyButton.cs:29` |
| 452 英文串 / 59 InvariantCulture / 5 CurrentCulture | 全项目 grep 计数 |
| 0 AutomationPeer / 0 ControlType / 0 AutomationId | 全项目 grep 计数（对照 312 SetName、103 SetHelpText、18 LiveSetting） |
| 0 KeyboardNavigation | 全项目 grep 计数（对照 186 键盘处理、53 TabIndex） |
| 0 虚拟化关键词；DataGrid/TreeTable 自述 | `ArcDataGrid.cs:115-125`、`ArcTreeTable.cs:106-108` |
| 0 服务接口；1 处 ICommand | 全项目 grep；`ArcFloatingButtonGroup.cs:77` |
| 9 处主题 key 字符串查找 | `ArcBillingToggle.cs:161`、`ArcMoneyInput.cs:423`、`ArcRadioCards.cs:163`、`ArcTagInput.cs:288`、`ArcColorEditor.cs:419-420`、`ArcEmojiPicker.cs:1374`、`ArcFileDropzone.cs:567`、`ArcSignaturePad.cs:746` |
| 64 个 DP 数字默认值 | 如 `ArcIconMorph.cs:60`、`ArcToast.cs:32`、`ArcDonutChart.cs:29` |
| 10 个 \*Size 枚举 | `ArcFieldBase.cs:9`、`ArcAvatar.cs:16`、`ArcPresenceStack.cs:33`、`ArcProgress.cs:13`、`ArcAccordion.cs:19`、`ArcButtonGroup.cs:27`、`ArcExpandingButtonGroup.cs:25`、`ArcFloatingButtonGroup.cs:28`、`ArcBillingToggle.cs:17`、`ArcScrubInput.cs:16` |
| 重名 enum | `ArcAvatarShape`：`ArcAvatar.cs:32` 与 `ArcAvatarUpload.cs:24` |
| 集合类型不一致 | IList 147 / IReadOnlyList 273 / IEnumerable 104 |
| 公开字段仅 5 处且都在 private 嵌套类 | `ArcPresenceStack.cs:96-111` |
| 演示数据泄漏 | `ArcUserMenu.cs:70,73`、`ArcSamplePhoto.cs:14`、`ArcPhoneInput.cs:69`（国家表）、`ArcRichTextEditor.cs:65-72`（英文命令） |
| XML 文档写作情况 | 1341 `/// <summary>`；163 个非 Pages 文件中仅 7 个无 doc |

### 12.4 依赖与主题

| 证据 | 位置 |
|---|---|
| 11/11 → Demo | 各组件 csproj |
| 5 个 → Internal | `Actions/Feedback/Navigation/Overlays/DataDisplay` csproj |
| `InternalsVisibleTo` 交叉 | `Internal/AssemblyInfo.cs:3-7`、`Pickers/AssemblyInfo.cs:3` |
| 共享类型引用量 | `FeedbackUi` 150、`ArcActionVisuals` 69、`ArcIconButton` 12、`ArcMenuSurface` 4（非 Pages 代码 grep） |
| 假依赖（仅页面使用） | `Navigation/Pages/MegaMenuMorphPage.cs:5`；`Overlays/Pages/{Dialog,Drawer,Popover,Tooltip}Page.cs:5-6` |
| 真依赖 | `DataDisplay/GlobalUsings.cs:2` + `ArcDataGrid.cs:1036`/`ArcSortableDataTable.cs:531`/`ArcTreeTable.cs:1276` |
| 131 ControlTheme（106 Arc + 25 内建） | 97 个 `Themes/*.axaml` 解析 |
| Charts 0 主题 | `Charts/Themes/ArcChartsTheme.axaml:1-10`；`ChartCore.cs:64-112`（token 渲染期解析） |
| ListBox 主题无 Template（仍依赖当前主题模板） | `Inputs/Themes/ArcPrimitives.axaml:245-253` |
| 内建控件使用面 | 非 Pages 代码 260 个文件中有 **104 个**提及内建控件（cs+axaml 提及次数）：Button 231 / TextBox 102 / ScrollViewer 56 / Popup 40 / ToolTip 36 / ListBox 35 / Menu 19 |
| 页面级浮层（非 Popup） | `ArcDialog.cs:22-23`、`ArcTooltip.cs:42-58`、`ArcToastHost.cs:15-19`、`ArcDropdownMenu.cs:15`、`ArcUserMenu.cs:61-62`、`ArcHoverCard.cs:24`、`ArcMegaMenu.cs:132-133`、`ArcShareAccess.cs:112-113` |
| 控件直连 StorageProvider | `ArcFileDropzone.cs:307-318`、`ArcAvatarUpload.cs:304-309`、`ArcChatThread.cs:977-982` |

### 12.5 验证门槛（供 Lead 执行，本报告未运行）

| 命令 | 期望 |
|---|---|
| `dotnet build ArcUI.slnx` | 0 error |
| `... Arc.Avalonia.Gallery.dll --audit` | `CATALOG AUDIT: items=294 docs=294 pages=294 errors=0`（`Gallery/Framework/CatalogAudit.cs:37-40`） |
| `... Arc.Avalonia.Gallery.dll --smoke` | 315 pages, 0 failures（`README.md:18`） |
| `tools/shots.ps1 -Theme both` | light/dark 基线与现库一致（剥离前后各跑一次对比） |
| `dotnet pack`（P0 完成后） | 11 个包 + meta 包，含 .xml/.pdb/README/license |

---

## 13. 未验证与边界说明

1. **未运行构建/渲染**：本报告所有判断来自静态代码/工程文件。以下结论需 Lead 构建后复核：
   - 剥离后 `--audit`/`--smoke` 是否仍 294/315 全绿（机制上应成立：反射发现 + 保持命名空间）。
   - 页面在**没有** FluentTheme 时是否可渲染（本报告只统计依赖面，判定属 A 报告）。
   - `ArcDataGrid.cs:115-125` 与 `ArcTreeTable.cs:106-108` 的「无虚拟化」自述是否仍与实现一致（作者注释可能滞后）。
2. **「无文档」判定**依赖 slug 命名假设（`ProgressTrack` → `progress-track`）；16 个部件中有 2 个（`ArcElasticSlider`、`ArcRangeHistogramSlider`）实际有独立文档，故写为「16 个部件、其中 14 个无文档」而非单一数字。
3. **人日为粗估**：以 1 名熟悉 Avalonia 12 的工程师为基准，未计入设计/评审/截图基线重拍（RTL/DPI 会额外增加截图工作量）。
4. **未覆盖**：Fluent 主题去除（A 报告）、Agent 顶层组合（C 报告）、生态包形态对标（D 报告）。本报告只回答「中间层自身能否作为通用框架发布」。

---

### 附：本报告关键数字速查

```
项目 11       公开类 260      公开控件 151     有主题控件 106
DP 913        CLR event 143   RoutedEvent 0    TemplatePart 0
库 LOC 77,567  主题 LOC 8,660   ControlTheme 131
页面 140/16,103 LOC            文档 128/662,145 B
页面+文档 = 49.4% 文件 / 29.9% 字节
库→Demo 11/11                包元数据 0
AutomationPeer 0              KeyboardNavigation 0
虚拟化 0                      本地化资源 0
缺口 23 项 ≈156–251 人日（中位 ~200）
```
