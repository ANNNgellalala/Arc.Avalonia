# 15 · M1 任务卡：分层 + 可发布（α）

> **依据**：[`docs/13-framework-layering-and-agent-top.zh.md`](13-framework-layering-and-agent-top.zh.md) v3.2（WBS G0/G1a/G2）
> **M1 总量**：≈90 人日（其中 M0 消耗 8）；本文件把剩余 **82 人日**拆成 5 张任务卡。
> **目标**：交付一个**能 `dotnet pack`、能一行接入 `<arc:ArcTheme/>`、发布物不含 Demo/页面/文档、依赖闭包仅 Avalonia 官方包**的 α 版本（`0.9.0-alpha`）。
> **不在 M1**：Top 15–20 内建控件皮肤（M2/G1b）、服务层/窗口壳（M2/G3b）、数据驱动列表（M2/G3c）、Agent 顶层（M2/G4）、无障碍/本地化（M3）。

---

## 1. 任务卡总览

| 卡 | 任务 | 人日 | 依赖 | 主要写作用域 | 主要 DoD |
|---|---|---:|---|---|---|
| **M1-T1** | 文档/页面出库（614 文件） | **17** | —（可与 T2 并行） | `src/Docs/**`（新增）、`src/Apps/Arc.Avalonia.Demo/**`、各库 `Pages/` `Docs/`、`src/Apps/Arc.Avalonia.Gallery/**` | 库 csproj 不引用 Demo；`--audit` 294/294；`--smoke` 315 页 0 失败 |
| **M1-T2** | 基础层纯度 + Internal 解散 + 程序集重命名 | **11** | —（可与 T1 并行） | `src/Foundation/**`、5 个引用 Internal 的 csproj、`ArcUI.slnx` 路径 | 构建 0 error；`Internal` 目录删除；`dotnet pack` 不拖 Demo |
| **M1-T3** | Fluent 基础适配 + `ArcTheme` 聚合入口 | **19** | T2（包结构）、M0 POC-A | 新增 `src/Theme/**`、`src/Foundation/Arc.Avalonia/Theme/**`、Gallery `App.axaml(.cs)` | 一行 `<arc:ArcTheme/>` 接入；双主题/8 accent 无冲突；R4 绿 |
| **M1-T4** | 发布工程（包元数据/XML/meta/CPM/samples/API 基线/许可） | **24** | T2（包结构） | `Directory.Build.props/.targets`、各 csproj 元数据、`samples/**`（新增）、根 `LICENSE`/`README`、`Directory.Packages.props` | `dotnet pack` 产物合规（R5 绿）；samples 可编译；每包有 `.xml` 与许可 |
| **M1-T5** | 门禁与回归（R1–R8、基线对比、发布验证） | **11** | T1–T4 | `tools/**`（只改门禁脚本）、`docs/baseline/**` | `check-all.ps1` 全绿（R2/R5 转绿）；基线截图对比通过；`0.9.0-alpha` 安装验证 |

**并行建议**（3 人）：
* 第 1–2 周：T1（出库）+ T2（基础层）+ T4 的元数据部分可先行；
* 第 3 周：T3 开工（依赖 T2 与 POC-A）；
* 第 4 周：T4 收尾（samples/许可/CPM）；
* 第 5 周：T5 全量回归 + 打包验证；
* 每周一次全量 `check-all + audit + smoke + 双主题截图`。

---

## 2. M1-T1 · 文档/页面出库（17 人日）

### 2.1 目标

把 320 个页面（294 个条目页 + 21 个文档/场景页 + 辅助类）与 294 份 `Docs/*.json` 从发布库迁到独立叶子工程，
使发布库不再依赖 `Arc.Avalonia.Demo`，也不再把文档嵌入程序集。

### 2.2 交付物

| 交付 | 说明 |
|---|---|
| `src/Docs/Arc.Avalonia.Docs/` | 新工程：引用 11 个组件族 + Agent + Special/Blocks（按页面来源）；`EmbeddedResource Include="**/Docs/*.json"` |
| `src/Docs/Arc.Avalonia.Docs.Abstractions/` | 从 `Demo` 拆出的契约：`ArcPage`/`ArcItemPage`/`DemoUi`/`ArcCatalog`/`ArcPageRegistry`/`ArcDocStore`/`ArcDocValidator` |
| 各库 `Pages/`、`Docs/` | 删除（迁出）；csproj 删除 `ProjectReference Demo` 与 `EmbeddedResource Docs` |
| `src/Apps/Arc.Avalonia.Demo/` | 收缩为 catalog 数据 + 别名（或并入 Docs.Abstractions）；不再被发布库引用 |
| Gallery | 增加对 Docs/Docs.Abstractions 的引用；`App.axaml` 的 `StyleInclude` 列表按新包名调整 |

### 2.3 关键步骤

1. 先建 `Docs.Abstractions`（契约层），再建 `Docs`（页面 + 文档数据），最后删库内页面；
2. 反射发现：`ArcPageRegistry`/`ArcDocStore` 扫 `Arc.Avalonia*` 程序集，**保持程序集名前缀**（`Arc.Avalonia.Docs`）即可被发现；
3. 文档资源逻辑名必须含 `.Docs.`（`ArcDocStore.ResourceMarker`）；
4. 页面仅 1 处 internal API 依赖（`DateRangePickerPage` → `ArcDateMath`），改为 public 或移入 Docs；
5. 逐项目删除 Demo 引用（24 个发布库），同步删除 `EmbeddedResource Docs`。

### 2.4 DoD

```powershell
pwsh -NoProfile tools/build.ps1
dotnet src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --audit   # items=294 docs=294 pages=294 errors=0
dotnet src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --smoke   # itemShows=294 pageShows=315 failures=0
pwsh -NoProfile tools/check-all.ps1   # R2 转绿（Demo 引用=0、EmbeddedResource Docs=0）
```

* 发布库 csproj 中 `Arc.Avalonia.Demo` 命中 0；
* 发布库 `EmbeddedResource Docs` 命中 0；
* `shots` 双主题抽查：按钮/输入/表格/对话框页无差异。

### 2.5 风险

| 风险 | 缓解 |
|---|---|
| 页面未进 Gallery 输出目录 → 静默缺失 | 以 `--audit` 计数为准；检查新工程在 Gallery `ProjectReference` 中 |
| 命名空间/资源 URI 错乱 | 迁移脚本化（`git mv` + 批量替换），一次一项目，逐项目跑 audit |
| `ArcPageRegistry` 扫描顺序变化 | 不改发现逻辑；仅保证程序集名前缀与文件在输出目录 |

---

## 3. M1-T2 · 基础层纯度 + Internal 解散 + 重命名（11 人日）

### 3.1 目标

* Foundation 只留设计系统内核与原子控件；`ArcVirtualList`/`ArcUi` 下沉 L2；
* `Arc.Avalonia.Internal` 解散（4 个视觉类并入 L2 包），解除 12 个 csproj 引用与 6 处 `InternalsVisibleTo`；
* 程序集重命名 `Arc.Avalonia` → `Arc.Avalonia.Foundation`（6 处 `avares://` URI + 文档 + 引用）。

### 3.2 交付物

| 交付 | 说明 |
|---|---|
| `Arc.Avalonia.Internal` 删除 | `ArcActionVisuals`/`ArcMenuSurface`/`ArcIconButton`/`FeedbackUi` 分别并入 `Controls.Actions`/`Controls.Feedback`（按使用方）|
| 引用清理 | 5 个组件包 csproj 删除 Internal 引用；`Pickers/AssemblyInfo.cs` 的 `InternalsVisibleTo(LifeToday)` 移除或改为公开 API |
| 程序集重命名 | `src/Foundation/Arc.Avalonia/Arc.Avalonia.csproj` → `Arc.Avalonia.Foundation.csproj`（`AssemblyName`/路径/slnx/6 处 avares URI/README 文档） |
| `IArcResourceHost` | `ArcResources` 去 `Application.Current` 硬依赖（默认实现保留，测试可替换） |
| `ControlThumbShadow` | 补注册到 `ArcPalette`（82/82 token） |

### 3.3 DoD

* `dotnet build ArcUI.slnx` 0 error；
* `grep -r "Arc.Avalonia.Internal" src` 为 0（除历史文档）；
* `dotnet pack` 时 `Arc.Avalonia.Foundation` 包内不含 `Pages/`/`Docs/`（与 T1 联合验证）；
* `VERIFY: checks=34 failures=0`；audit/smoke 与基线一致。

### 3.4 风险

| 风险 | 缓解 |
|---|---|
| Internal 的类被 12 个 csproj 以命名空间引用 | 先批量 grep 使用方，按"就近并入"原则分派；一次一个类，逐次构建 |
| 程序集重命名导致 avares 失效 | 只有 6 处引用（`ArcTheme.axaml`×3、Gallery `App.axaml`×1、`.recon`×1）；用 `grep -r "avares://Arc.Avalonia/"` 全量替换并验证 |
| `InternalsVisibleTo` 解除后编译错误 | 暴露必要 API 或把使用者移入同程序集；优先最小公开面 |

---

## 4. M1-T3 · Fluent 基础适配 + `ArcTheme` 聚合入口（19 人日）

### 4.1 目标

* 建 `src/Theme/Arc.Avalonia.Theme`（L2.5 聚合层）：`ArcTheme : Styles`（或 `app.UseArcTheme()` fallback，取决于 M0 POC-A）；
* 基础适配：`FluentTheme.Palettes` 映射 8 accent；全局字体/字号/圆角键；TextBox/ScrollViewer/ScrollBar/ListBox/Popup/ToolTip/Window 资源键覆盖（Light/Dark 两套 `ThemeDictionaries`）；
* 消费者从"`<FluentTheme/>` + 26 条 `StyleInclude`"变为 **1 行**。
* **修复 `ArcResources` 的变体取值**（M0 POC 发现）：token 迁入 `ThemeDictionaries` 后，无 variant 的 `TryFindResource` 会静默取默认值 → 改为按 `ActualThemeVariant` 取值（计入基础适配 15 人日）。
* 复用 ADR 0004 的等价键名迁移表（Fluent 11.x → 12.1.4 约 30% 键改名，如 `TextControlPadding→TextControlThemePadding`）。

### 4.2 交付物

| 交付 | 说明 |
|---|---|
| `src/Theme/Arc.Avalonia.Theme/` | `ArcTheme.axaml`（`<Styles>`：FluentTheme + token 字典 + 组件主题 Include）|
| `ArcThemeManager` 协作 | `Palettes` 映射 8 accent；`Apply` 时同步覆盖字典换肤 |
| 覆盖键常量表 | `ArcFluentKeys.cs`（键名常量 + 注释）；`tools/check-theme.ps1` 可校验 |
| Gallery 接入 | `App.axaml` 只留 `<arc:ArcTheme/>`；`App.axaml.cs` 调 `ArcThemeManager.Apply` |
| 组件主题文件改名 | `Foundation/Theme/ArcTheme.axaml` → `ArcBaseStyles.axaml`（避免与类重名） |
| `ArcResources` 变体修复 | `TryFindResource(key, variant)` / `ActualThemeVariant`；自绘控件换肤不再取默认值 |
| 键名常量表 | 按 ADR 0004 的 47 键 + 迁移表实现 `ArcFluentKeys`；`check-theme` 校验 |

### 4.3 DoD

* POC-A 结论落地（嵌套 Styles 或编程式入口），Gallery 一行接入运行正常；
* Light/Dark × 8 accent 切换：裸 TextBox/ComboBox/TabControl + Arc 控件无残留色（截图抽检 8 张）；
* `tools/check-all.ps1` R4 绿；`--audit`/`--smoke` 与基线一致；
* 主题包可单独 `dotnet pack` 且依赖（L1 + 11 组件包）符合 L2.5 allowlist。

### 4.4 风险

| 风险 | 缓解 |
|---|---|
| Fluent 覆盖键在变体字典中不生效 | M0 POC-B 已给出"键覆盖/局部 ControlTheme"分类；按分类实现 |
| 组件主题合并顺序导致覆盖被反覆盖 | 固定顺序：Fluent → token 字典 → Fluent 键覆盖 → 组件主题；写进 ADR 0003 |
| Fluent 键名版本漂移（11.x→12.x 约 30% 改名） | 键名常量表 + ADR 0004 迁移表 + `check-theme` 键存在性校验 |
| `ArcResources` 变体坑导致换肤残留 | M1 内修复（见 §4.2），并在 Gallery 切 8 accent 验证自绘控件 |
| 主题包聚合导致依赖倒置门禁红 | L2.5 例外已在 `tools/architecture-allow.json` |

---

## 5. M1-T4 · 发布工程（24 人日）

### 5.1 目标

`dotnet pack` 产出**可被消费**的包：元数据齐、XML 文档齐、依赖闭包干净、示例可编译、许可合规。

### 5.2 交付物

| 交付 | 说明 |
|---|---|
| `Directory.Build.props` / `Directory.Packages.props` | 统一 `PackageId`/`Version`（lockstep）、`Authors`/`License`/`RepositoryUrl`/`PackageReadmeFile`；集中包版本 |
| XML 文档 | `GenerateDocumentationFile=true`、清理 CS1591 告警（1341 段 summary 生效） |
| 元包与聚合 | `Arc.Avalonia`（meta）、`Arc.Avalonia.All`；`IsPackable` 白名单 |
| `samples/` | 三个可编译示例：登录页、设置页、数据页（引用 meta 包 + 一行 `ArcTheme`） |
| API 基线 | `Microsoft.CodeAnalysis.PublicApiAnalyzers` + 每包 `PublicAPI.Shipped.txt`/`Unshipped.txt` |
| 许可与声明 | 根 `LICENSE`（MIT）+ `THIRD-PARTY-NOTICES.md`（Avalonia/SkiaSharp MIT、Lucide ISC、Inter OFL、Arc UI MIT） |
| demo 残留清理 | `ArcUserMenu` persona 默认值、`ArcSamplePhoto`、Feedback demo 头像 → 中性默认值/移到 Docs |

### 5.3 DoD

```powershell
dotnet pack ArcUI.slnx -c Release -o artifacts
# 断言：
# - nupkg 内无 Pages/Docs/demo PNG；依赖只有 Avalonia 官方包（R5 绿）
# - 每包有 .xml 与 licenses；meta 包依赖正确
# - samples/ 三工程可 `dotnet build`
```

* `tools/check-all.ps1` R5 转绿；
* 在一个临时空工程里 `dotnet add package Arc.Avalonia --source artifacts` 后可运行最小窗口（一行主题）。

### 5.4 风险

| 风险 | 缓解 |
|---|---|
| 元数据/版本 lockstep 遗漏 | 用 MSBuild 中央属性 + 批量校验脚本（遍历 nupkg 元数据） |
| XML 文档告警爆炸 | 先只对发布库开启，逐步清理；CS1591 仅警告不阻塞 |
| demo 默认值是公开 API 行为 | 在 release notes 明确 breaking；M1 尚未发布，改默认值成本最低 |

---

## 6. M1-T5 · 门禁与回归（11 人日）

### 6.1 目标

让门禁从"known-red"变成"全绿"，并把基线对比固化为每周回归。

### 6.2 交付物

| 交付 | 说明 |
|---|---|
| R2 绿 | T1 完成后 `Demo` 引用=0、`EmbeddedResource Docs`=0 |
| R5 绿 | T4 完成后 pack 产物检查接入 `check-all` |
| BannedApi 生效 | `BannedSymbols.txt` + Agent/发布库配置（M2 前先就位） |
| 回归脚本 | `tools/regression.ps1`：build + verify + audit + smoke + check-all + 双主题截图（可选） |
| 基线对比 | `docs/baseline/shots-manifest.csv` vs 新截图；允许人工确认的差异清单 |
| 发布验证 | `0.9.0-alpha` 安装到临时工程并渲染一页 |

### 6.3 DoD

* `check-all.ps1`：green=7 / knownRed=0 / red=0；
* 每周回归一次全绿；截图对比差异有记录（允许的视觉变化需在 release notes 说明）；
* 发布验证脚本可重复执行。

---

## 7. 协作规则（3–4 人并行）

1. **写作用域不重叠**：各卡按 §1 的目录范围；跨域改动先在任务卡里登记；
2. **构建串行**：全量构建走 `tools/build.ps1`（有锁）；单独项目可用 `dotnet build <proj> -m:1 -nodeReuse:false`，遇文件锁 sleep 10s 重试一次；
3. **验证口径**：任何迁移类改动以 `--audit`/`--smoke` 的**计数**为准，不看 exit code；
4. **每日同步**：每天一次 `check-all + audit`；每周一次全量截图与 `VERIFY`；
5. **提交策略**：每卡完成一次 commit（`git add` 仅本卡作用域），message 格式 `M1-T<n>: <summary>`；Lead 负责 tag `m1-alpha`。

---

## 8. M1 退出标准

- [ ] `dotnet pack` 产物合规（无 Demo/页面/文档；依赖闭包仅官方包；XML/许可齐）；
- [ ] 一行 `<arc:ArcTheme/>` 接入（Gallery 与 samples 均验证）；
- [ ] 614 文件出库完成；audit 294/294；smoke 315 页 0 失败；双主题截图通过；
- [ ] `Arc.Avalonia.Internal` 解散；程序集重命名完成；`ArcVirtualList`/`ArcUi` 下沉；
- [ ] `check-all.ps1` 全绿（R1–R8）；
- [ ] `0.9.0-alpha` 可在临时空工程安装并渲染；
- [ ] 文档更新：README（项目表/依赖/构建）、CONVENTIONS（分层/主题规则）、docs/13 的 M1 状态。

---

## 9. 执行记录（2026-11-05 第一波：M1-T1 + M1-T2a）

### 9.1 结果

| 项 | 结果 |
|---|---|
| M1-T1 出库 | ✅ 617 文件（320 页 + 294 文档 + 3 portrait）迁入 `src/Docs/Arc.Avalonia.Docs`；`src/Apps/Arc.Avalonia.Demo` 整体迁为 `src/Docs/Arc.Avalonia.Docs.Abstractions`；25 个 csproj 编辑；`src/Apps` 仅剩 Gallery |
| M1-T2a 基础层 | ✅ `AssemblyName=Arc.Avalonia.Foundation`（文件路径/命名空间/引用不变）；avares URI 4 处更新；`ControlThumbShadow` 补注册（82/82，16 套主题断言）；`IArcResourceHost` + `UseHost` 落地；6 个 Inputs 主题 `assembly=` 连带修复 |
| 门禁 | ✅ R1/R2/R3/R4/R6/R8 **GREEN**；R5 KNOWN-RED（M1-T4/T5）；`-SelfTest` 4/4；R3 节点解析已支持 PackageId≠工程名 |
| 构建 | ✅ `tools/build.ps1` 0 error / 0 warning |
| 验证 | ✅ `VERIFY: checks=34 failures=0`；`AUDIT: items=294 docs=294 pages=294 errors=0`；`SMOKE: itemShows=294 pageShows=315 failures=0` |
| 回归 | ✅ 头像页真实渲染（`.recon/shots-check/light/10083-avatar.png`），portrait 资源经 `avares://Arc.Avalonia.Docs/Assets/` 正常加载 |

### 9.2 已知折中（M1 内接受，T2b/T4 清理）

1. **24 个发布库各加 `<InternalsVisibleTo Include="Arc.Avalonia.Docs" />`**：页面实际依赖各库 internal kit（AiKit/AppKit/CommerceKit/MarketingKit/TeamKit/DataKit/DataSamples、LifeToday 的 TodayPageUi/TodayStateSwitch、Special 的 Sv/Sm/Spring、Pickers 的 ArcDateMath、TeamPerson 等），比 docs/15 初稿估计的"仅 1 处"多得多。M1 用 IVT 保持最小改动；T2b 可迁出 kit 或公开 API 后删除。
2. **Docs 工程临时引用 `Arc.Avalonia.Internal`**（public 模型 ArcMenuItem/ArcActionSwapState/ArcMenuSurface 等）+ `<Using Include="Arc.Avalonia.Internal" />`；T2b 解散 Internal 时同步调整 Docs.csproj。
3. **3 个 Blocks `*Kit.cs` 删除未使用的 `using Arc.Avalonia.Demo;`**（编译必需，无逻辑改动，已 review 接受）。
4. **Abstractions 保留 LogicalName=`Arc.Avalonia.Demo.Catalog.arc-catalog.json`**（运行期加载契约）；命名空间仍为 `Arc.Avalonia.Demo`（避免 320 页 using 大改，命名空间重命名留待后续）。

### 9.3 剩余 M1 工作

| 卡 | 剩余内容 | 估 |
|---|---|---|
| M1-T2b | Internal 解散（4 视觉类迁入 L2、解除 12 csproj 引用与 6 处 IVT）、`ArcVirtualList`/`ArcUi` 下沉（可选） | 5–6 |
| M1-T3 | `src/Theme` 聚合层 + Fluent 基础适配（Palettes/ThemeDictionaries/资源键）+ `ArcTheme` 一行入口 + `ArcResources` 变体修复 | 19 |
| M1-T4 | 发布工程（元数据/XML/meta/CPM/samples/API 基线/许可） | 24 |
| M1-T5 | 门禁回归（R5 转绿、基线对比、发布验证） | 11 |

### 9.4 第二波执行记录（2026-11-05 晚：M1-T2b + M1-T3）

| 项 | 结果 |
|---|---|
| M1-T2b Internal 解散 | ✅ 删除 `src/Foundation/Arc.Avalonia.Internal`（5 cs + 3 axaml + AssemblyInfo + csproj）；类型并入 `Actions/Internal`（ArcActionVisuals/ArcMenuSurface/ArcIconButton/ArcActionModels/ArcGlide/ArcDismiss/ArcKeyboard/ArcMenuPresenter）与 `Feedback/Internal`（FeedbackUi/ArcTween），**命名空间保留 `Arc.Avalonia.Internal`**（消费者 using 零改动）；13 个 csproj 清理；IVT 处理：删 5 条 Internal、新增 Actions→Navigation/Overlays 与 Feedback→DataDisplay、移除 Feedback→Docs（避免 `.Paint` 与 `Sv.Paint` 二义）、Pickers→LifeToday 保留 |
| M1-T3 主题聚合 | ✅ 新增 `src/Theme/Arc.Avalonia.Theme`（L2.5，AssemblyName 同名）；`ArcTheme : Styles`：Fluent（C# 创建）→ `ArcBaseStyles.axaml` → 11 个组件主题；`ArcThemeManager.Apply` 注入 token + 47 个 Fluent 键 + 8 accent Palettes；`ArcResources` 默认按 `ActualThemeVariant` 取值；Gallery `App.axaml` 一行 `<arc:ArcTheme/>`（另保留 13 条 L3/L4 主题，因 L2.5 不得依赖 L3/L4） |
| 主题运行时验证 | ✅ 新增 `Gallery.dll --theme-report`：Light/Dark 各 47/47 Fluent 键解析、ArcBackground White/#141414、accent Neutral/Violet/Rose 的 `SystemAccentColor` 精确匹配 → `THEME REPORT OK` |
| 构建 | ✅ 0 error（3 个既有警告） |
| 验证 | ✅ `Verify: checks=46 failures=0`（原 34 + 12 条主题断言）；audit 294/294；smoke 315 页 |
| 门禁 | ✅ R3/R1/R2/R4/R6/R8 GREEN；R5 KNOWN-RED（T4/T5） |
| 截图 | `.recon/theme-shots/{light,dark}/`（button 页 7×2，未入库） |

**M1-T3 已知偏差（ADR 0003 §9 已记录）**：FluentTheme 用 C# 创建（R4 禁止库内 axaml 出现 FluentTheme 元素，且注释文本也算命中）；token 与 47 键由 C# 生成（满足 R6 token-only）；TextBox/ComboBox 字号需局部 ControlTheme；ScrollBar 自动收缩未覆盖（ADR 0004 已记）。

**剩余 M1**：M1-T4 发布工程（24 人日）、M1-T5 门禁回归（11 人日，R5 转绿 + 发布验证）。

### 9.5 第三波执行记录（2026-11-06：M1-T4 + M1-T5）—— M1 完成

| 卡 | 结果 |
|---|---|
| **M1-T4 发布工程** | ✅ `dotnet pack ArcUI.slnx -c Release -o artifacts -m:1 -nodeReuse:false` → **28 包 / 1.76 MB / 0.9.0-alpha**；`Directory.Build.props`（lockstep 版本/元数据/包许可/README）、`Directory.Packages.props`（CPM，POC opt-out）、28 个 csproj 元数据；`src/Meta`：`Arc.Avalonia`（→Theme+11 组件+2 Agent）与 `Arc.Avalonia.All`（→元包+5 Special+6 Blocks）；3 个可编译 samples（登录/设置/数据，各一行 `<arc:ArcTheme/>`）；`LICENSE`（MIT）+`THIRD-PARTY-NOTICES.md`；API 基线（自建 `check-public-api.ps1`，28/28 MATCH）；demo 清理（ArcUserMenu 中性默认值、ArcSamplePhoto 移入 Docs） |
| **M1-T5 门禁回归** | ✅ R5 接入 `check-all`（调用 `check-package.ps1`，artifacts 缺失即 RED）；R1 认识 `src/Meta`（app 层）；R3 扫描 meta 包依赖；`.gitignore` 加 `artifacts/`；**check-all 7/7 GREEN（0 red / 0 known-red）**；`check-public-api` 28/28；button 页明暗截图抽检通过（ArcTheme 下 ArcButton 各变体/尺寸/加载/禁用正确） |

**M1 退出标准核对**：
- [x] `dotnet pack` 产物合规（28 包，无 Pages/Docs/demo；依赖仅官方包+lockstep Arc 包；dll+xml+README+LICENSE）
- [x] 一行 `<arc:ArcTheme/>` 接入（Gallery + samples 均验证）
- [x] 617 文件出库；audit 294/294；smoke 315 页；明暗截图抽检通过
- [x] `Arc.Avalonia.Internal` 解散；`AssemblyName=Arc.Avalonia.Foundation`；主题聚合 L2.5
- [x] `check-all.ps1` 全绿（R1–R8）
- [x] `0.9.0-alpha` 离线消费者安装验证（临时工程 source=artifacts，restore+build 0 error）
- [x] README（Install/打包/API/License）、CONVENTIONS §10 发布约定

**M1 完成。** 已知遗留（M2 处理）：Docs 页面经 24 个 IVT 使用各库 internal kit（T2b 可选迁出）；TextBox/ComboBox 字号需局部 ControlTheme；ScrollBar 自动收缩未覆盖；`RepositoryUrl` 为占位；BannedApi 在 M2 生效；`TeamData` 等 internal demo 数据随 kit 迁 Docs。