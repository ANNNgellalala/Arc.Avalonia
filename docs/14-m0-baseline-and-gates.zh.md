# 14 · M0 执行清单：冻结基线、门禁与两个 POC

> **依据**：[`13-framework-layering-and-agent-top.zh.md`](13-framework-layering-and-agent-top.zh.md)（v3.2）
> **决策基线**（2026-11-05 已确认）：D-1 与 AtomUI 同生态位、**不承诺 drop-in**；D-2 Agent 采用 **M 档（124 人日）**；
> D-3 网格 = **自有 `ArcDataGrid`（小数据）+ `Arc.Avalonia.Feature.DataGrid`（大数据）分工**；D-4 Top 15–20 皮肤清单**认可**。
> **范围**：M0 = 1 周 / **8 人日**（含在 G0 的 20 人日内）。产出四件：**冻结基线、R1–R8 门禁脚本、两个 POC、M1 任务拆分**。
> **前置条件**：v3.2 文档评审通过；工作树可提交/打 tag；构建环境与基线一致（Avalonia 12.1.4 / .NET 10 / 离线 NuGet）。
> **不做**：M0 不改任何产品源码；POC 代码只放在 `poc/`（不进 `ArcUI.slnx`、不进发布物），M0 末可删或留作参考。

---

## 1. M0 任务总表

| ID | 任务 | 产出 | 人日 | 依赖 | 负责人建议 |
|---|---|---|---:|---|---|
| M0-1 | 冻结基线 | git tag + 基线记录 + 截图 manifest | 1.0 | — | Lead |
| M0-2 | 依赖门禁（R3，含传递闭包） | `tools/check-deps.ps1` + 报告 + CI 入口 | 2.0 | M0-1 | 基础设施 |
| M0-3 | 其余门禁（R1/R2/R4/R5/R6/R8 + BannedApi 配置） | `tools/check-*.ps1` + 规则表 | 1.0 | M0-1 | 基础设施 |
| M0-4 | POC-A：`ArcTheme` 聚合入口 | POC 工程 + 1 页验证 + 结论（可行/回退方案） | 1.5 | M0-2 | 主题 |
| M0-5 | POC-B：Top 内建控件资源键覆盖 | POC 工程 + 3 控件双主题截图 + 覆盖键清单 | 1.5 | M0-2 | 主题 |
| M0-6 | M1 任务拆分与并行计划 | 任务卡（写作用域/DoD/依赖图） | 1.0 | M0-3/4/5 | Lead |
| | **合计** | | **8.0** | | |

---

## 2. M0-1 冻结基线（1 人日）

### 2.1 步骤

1. 确认工作树状态：`git status --short`（当前有未提交改动，先记录或提交到 `pre-framework` 分支）；
2. 打 tag：`git tag -a pre-framework -m "baseline before framework restructure"`；
3. 记录基线数字到 `docs/baseline/2026-11-baseline.md`：

| 项 | 命令 | 期望（2026-11-05 实测） |
|---|---|---|
| 构建 | `pwsh -NoProfile tools/build.ps1 -ExtraArgs '--no-incremental'` | 0 error / 9 warning |
| 自检 | `dotnet tests\Arc.Avalonia.Tests\bin\Debug\net10.0\Arc.Avalonia.Verify.dll` | `VERIFY: checks=34 failures=0` |
| 目录审计 | `dotnet src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --audit` | `items=294 docs=294 pages=294 errors=0 warnings=0 pageFailures=0` |
| 导航 smoke | 同上 `--smoke` | `itemShows=294 pageShows=315 failures=0` |
| 截图 | `pwsh -NoProfile tools/shots.ps1 -Theme both` | `shots/light`、`shots/dark` 全量 |
| 依赖快照 | 收集所有 `src/**/obj/project.assets.json` | 归档到 `docs/baseline/assets-pre-framework/`（或记录哈希） |
| 环境 | `dotnet --info`、Avalonia 版本、NuGet 源状态 | 写入基线文件 |

4. 生成截图 manifest（用于 M1 前后对比）：
   `Get-ChildItem shots -Recurse -Filter *.png | Get-FileHash -Algorithm SHA256 | Export-Csv docs/baseline/shots-manifest.csv`

### 2.2 验收

* tag 存在、基线文件已提交；
* 三条验证命令输出与上表一致（如有差异，以实测为准并记录原因）；
* 截图 manifest 可复算（同一命令输出相同行数）。

### 2.3 回滚点

`git checkout pre-framework`（M0–M1 期间任何失败可回到此点）。

---

## 3. M0-2 依赖门禁（R3，含传递闭包，2 人日）

### 3.1 目标

用脚本回答两个问题，且 **fail-closed**：

1. 每个**发布项目**的直接 + 传递 NuGet 依赖，是否都在允许集合内？
2. 是否有黑名单包被直接/间接引入（包括"官方附加控件间接带来第三方包"的情况）？

### 3.2 规则

**允许（直接）**：`Avalonia`、`Avalonia.Base`、`Avalonia.Controls`、`Avalonia.Markup.Xaml`、`Avalonia.Themes.Fluent`、
`Avalonia.Fonts.Inter`、`Avalonia.Desktop`（仅 `src/Apps/**`）、`Avalonia.Controls.DataGrid/ColorPicker`（仅 `Feature.*` 包）。

**允许（传递/平台）**：`Avalonia.Skia`、`SkiaSharp`（+native）、`HarfBuzzSharp`、`Avalonia.Win32`、`Avalonia.X11`、
`Avalonia.Native`、`Avalonia.Wayland`、`Avalonia.HarfBuzz`、`Avalonia.Remote.Protocol`、`Avalonia.BuildServices`、
`MicroCom.Runtime`、`System.*`、`Microsoft.*`（仅构建/SDK 相关，需显式列举）。

**禁止（直接或传递命中即失败）**：`AtomUI.*`、`Semi.Avalonia*`、`Ursa*`/`Irihi.*`、`FluentAvalonia*`、`SukiUI*`、
`Material.Avalonia*`、`Material.Icons*`、`AntDesign.Avalonia*`、`IconPacks.*`、`Lucide.Avalonia*`、
`Dock.Avalonia*`、`Tabalonia*`、`AvaloniaEdit*`、`Markdown.Avalonia*`、`DialogHost.Avalonia*`、`Svg.*`、`ShimSkiaSharp`、
`ColorDocument.Avalonia*`，以及任何 `Avalonia.*` 但不在白名单内的包。

### 3.3 实现要点

```powershell
# tools/check-deps.ps1（M0 内新增）
# 1) 枚举发布项目：src/**/*.csproj 中排除 Apps/、Tests/、Patterns（可选发布）、Feature（白名单单独处理）
# 2) 对每个项目读 obj/project.assets.json，取 targets.*.<tfm> 的完整依赖闭包
# 3) 与 allowlist/denylist 比对；输出 Markdown/JSON 报告；有命中即 exit 1
```

* 判定用 **package id（小写比较）**，不要用文件名；
* `project.assets.json` 不存在时先 `dotnet restore`（离线源已配置）；
* 报告写入 `artifacts/gates/deps-<date>.md`，CI 与本地共用；
* 白名单/黑名单集中在一个 `tools/deps-policy.json`，便于评审 diff。

### 3.4 验收

* 当前树运行 → **0 违规**（当前仓库确实只引官方包）；
* 人为在 POC 项目加一个黑名单包 → 脚本必须 exit 1 且报出包名与引入路径；
* 人为加一个"间接引入"用例（例如引用一个会带 `Svg.*` 的官方包模拟）→ 必须命中。

---

## 4. M0-3 其余门禁（1 人日）

| 规则 | 检查内容 | 实现方式 | 豁免 |
|---|---|---|---|
| **R1 分层** | L1 只依赖 L0；L2 → L1；L2.5 → L1+L2（allowlist）；L4 → L1+L2；Apps → 任意；Patterns → L2 | 解析 `ProjectReference` + `project.assets.json`，映射项目→层 | L2.5 的 11 个组件包依赖；Apps/Tests |
| **R2 库不引用文档/示例** | 发布库 csproj 不得含 `Arc.Avalonia.Demo`/`Docs`/`Samples` 引用，不得 `EmbeddedResource Docs` | grep csproj | 无（M1 完成后必须 0） |
| **R4 主题来源** | `Themes/*.axaml` 的 `StyleInclude`/`ResourceInclude` 只允许 `avares://Arc.Avalonia*` 与官方 Avalonia 主题 | grep + 路径白名单 | — |
| **R5 发布物内容** | `dotnet pack` 产物不含 `Pages/`、`Docs/`、demo PNG、persona 字符串 | 解包 nupkg 检查 | 过渡期（M1 前）允许失败并记录 |
| **R6 token-only** | axaml/cs 中不得出现 `#RRGGBB`/`rgb(`/`Color.FromArgb`（自绘图表 alpha 派生除外，需 allowlist 注释） | 正则扫描 + 注释豁免 | 已有 CONVENTIONS §4 的既有例外 |
| **R8 无渲染层自研** | 新增文件不得含 `FrameClock`/`RenderTimer`/`DrawingCache`/`ThemeVersion` 等命名；不得实现帧调度/绘制缓存 | 文件级正则 + 人工复核 | 现有历史代码不追溯（仅 M0 后新增） |
| **BannedApi** | Agent 程序集禁止 `System.Net.Http.HttpClient`、`System.IO.File`、`System.Threading.Timer` 出现在业务路径 | `BannedApiAnalyzers` + `BannedSymbols.txt`（M1 配好，M2 生效） | 测试/工具代码 |

验收：一条命令 `pwsh -NoProfile tools/check-all.ps1` 一次跑完 → 输出汇总表（红/绿），当前树除 R2/R5（M1 前已知为红）外全绿。

---

## 5. M0-4 POC-A：`ArcTheme` 聚合入口（1.5 人日）

### 5.1 要验证的三个假设

1. 一个自定义 `Styles` 子类（`ArcTheme`）能否在 XAML 中**嵌套** `<FluentTheme/>` + `<StyleInclude>` + 资源字典；
2. Light/Dark 与 8 accent 能否通过 `FluentTheme.Palettes`（`ColorPaletteResources`）映射，而非裸 `SystemAccentColor`；
3. 组件主题（如 `ArcButton.axaml`）与 Fluent 覆盖能否**共存**且 `ThemeDictionaries` 的 `DynamicResource` 正常解析。

### 5.2 步骤

```text
poc/ArcThemePoc/
├── ArcThemePoc.csproj      引用 Foundation + Inputs + Feedback + Avalonia.Themes.Fluent
├── App.axaml               <arc:ArcTheme/>（唯一 StyleInclude）
├── App.axaml.cs            ArcThemeManager.Apply(...)
└── MainWindow.axaml        ArcButton + ArcInput + 裸 TextBox/ComboBox + 切换 accent 按钮
```

1. 新建 POC 工程（不进 `ArcUI.slnx`）；
2. `ArcTheme : Styles`，XAML 内容：`<FluentTheme/>`、token 资源字典（含 `ThemeDictionaries` 两套）、
   11 个组件主题的 `StyleInclude`（POC 只取 2 个，验证机制）；
3. 在 `App.axaml` 只写 `<arc:ArcTheme/>`；
4. 运行：切 Light/Dark、切 8 accent，检查裸 TextBox 与 Arc 控件均正常；
5. 记录结论：`docs/adr/0003-theme-strategy.md`（含失败时的 fallback）。

### 5.3 Fallback（若嵌套 `<FluentTheme/>` 不被 XAML 接受）

改为**编程式入口**：`Application.UseArcTheme(ArcThemeOptions)` 扩展方法，内部
`app.Styles.Add(new FluentTheme()); app.Styles.Add(new StyleInclude(...)); app.Resources.MergedDictionaries.Add(tokenDict)`。
消费者从 `<arc:ArcTheme/>` 改为 `app.UseArcTheme()`（一行 C#），其余不变。

### 5.4 验收

* POC 工程可 `dotnet run`（或 headless 截图），无 XAML 解析异常、无 `StaticResource` 缺失；
* 双主题 + 8 accent 切换无残留色（裸 TextBox + Arc 控件）；
* ADR 记录"可行/回退"与证据截图。

---

## 6. M0-5 POC-B：Top 内建控件资源键覆盖（1.5 人日）

### 6.1 要验证的假设

Fluent 12.1.4 的内建控件外观由**资源键**驱动（R1 已列 TextBox 约 20 键、ScrollBar 约 20 键）；
在 Arc 的 `ThemeDictionaries` 中覆盖这些键，能否在**不改模板**的前提下让裸控件呈 Arc 外观（颜色/尺寸/圆角/滚动条粗细）。

### 6.2 覆盖样本（3 个代表控件）

| 控件 | 关键键（示例，以 Fluent 12.1.4 源码为准） | 验收点 |
|---|---|---|
| `TextBox` | `TextControlBackground*`、`TextControlBorderBrush*`、`TextControlForeground*`、`PlaceholderForeground`、`TextControlThemeMinHeight`、`TextControlPadding`、`ControlCornerRadius` | 浅/深色下背景、边框、占位符、圆角为 Arc token；获得焦点时边框可接受（或补焦点环样式） |
| `ScrollBar` | `ScrollBarSize`、`ScrollBarThumbFill*`、`ScrollBarTrackFill*`、`ScrollBarButton*` | 细滚动条、thumb 圆角、hover 色 |
| `TabControl`/`TabItem` | `TabItemHeaderBackground*`、`TabItemHeaderForeground*`、`TabItemHeaderSelectedBackground*`、间距/圆角键 | 选中态用 accent、圆角与 Arc 一致 |

### 6.3 步骤

1. 在 POC 页面放裸控件 + Arc 控件对照；
2. 写 `ThemeDictionaries`（`Light`/`Dark` 各一套覆盖），逐键验证；
3. 截图（浅色/深色）存入 `docs/images/poc/`；
4. 记录覆盖键清单到 `docs/adr/0004-top-control-keys.md`（后续 M2 直接复用）；
5. 结论：哪些控件可"纯资源键"，哪些必须"局部 ControlTheme 或模板替换"（后者会抬高 M2 的 15 人日估算）。

### 6.4 Fallback

若 `ThemeDictionaries` 无法覆盖 Fluent 的某些键（例如被 `BasedOn` 模板内联），改用：
`ControlTheme TargetType="{x:Type TextBox}" BasedOn="{StaticResource {x:Type TextBox}}"` 的局部覆盖，或 `ThemeVariantScope` 包裹。
若仍不可行 → 该控件列入"必须模板替换"清单，M2 排期按 R1 的悲观值（23 人日）预留。

### 6.5 验收

* 3 个控件在浅/深色 + 至少 2 个 accent 下与 Arc token 一致（截图对比）；
* 覆盖键清单可被 M2 直接引用；
* 给出"纯键覆盖 / 需模板替换"分类与占比（决定 M2 主题 15 人日的置信度）。

---

## 7. M0-6 M1 任务拆分（1 人日）

### 7.1 任务卡（并行 3–4 人；写作用域不重叠）

| 任务 | 内容 | 写作用域 | 依赖 | DoD |
|---|---|---|---|---|
| **M1-T1 出库** | 614 文件（320 页 + 294 文档）迁到 `src/Docs/Arc.Avalonia.Docs`；`Demo` 拆 `Docs.Abstractions`；Gallery/audit/smoke/shots 适配 | `src/Docs/**`、`src/Apps/Arc.Avalonia.Demo/**`、`src/Apps/Arc.Avalonia.Gallery/**`、各库 `Pages/` `Docs/` | — | audit 计数不降；smoke 315 页 0 失败；双主题截图通过 |
| **M1-T2 基础层** | Foundation 纯度化、`Internal` 解散（12 处引用）、程序集重命名、`ArcVirtualList`/`ArcUi` 下沉 | `src/Foundation/**`、5 个引用包的 csproj | — | 构建 0 error；`dotnet pack` 不拖 Demo |
| **M1-T3 主题** | Fluent 基础适配（Palettes/ThemeDictionaries/资源键）+ `ArcTheme` 聚合入口 + 样式文件改名 | `src/Theme/**`（新增）、`src/Foundation/Arc.Avalonia/Theme/**`、`src/Apps/.../App.axaml` | M1-T2、POC | 一行接入；双主题无冲突 |
| **M1-T4 发布工程** | 包元数据/XML/meta/CPM/samples/API 基线/LICENSE | `Directory.Build.props`、各 csproj、`samples/**`、根文件 | M1-T2 | `dotnet pack` 产物合规；samples 可编译 |
| **M1-T5 门禁与回归** | 跑 R1–R8、修误报、维护基线对比 | `tools/**`、`docs/baseline/**` | 全部 | 门禁全绿；基线截图对比通过 |

### 7.2 依赖图

```text
M0-1 ──> M0-2 ──> M0-3 ──> M0-4 ─┐
                     └────> M0-5 ─┴──> M0-6 ──> M1-T1 ∥ M1-T2 ∥ M1-T4
                                                      └──> M1-T3（依赖 T2 + POC）
                                                           └──> M1-T5（最后）
```

### 7.3 并行与协调规则

* 每个任务一个 owner（人或 agent），**写作用域不重叠**；跨域改动先在 M0-6 拆出来单独立项；
* 每完成一个任务：`dotnet build` + `--audit` + `--smoke`（快速）+ 该任务相关截图；
* 每日一次全量回归（T5 负责）；
* 出库任务的风险是"静默缺失"：以 `--audit` 计数为唯一判据，不看 exit code。

---

## 8. 一周日程（3 人）

| 日 | 任务 | 产出 |
|---|---|---|
| D1 | M0-1 冻结基线；M0-2 依赖门禁脚本开写 | tag + 基线文件；`check-deps.ps1` 初版 |
| D2 | M0-2 完成 + 负例验证；M0-3 门禁开写 | 依赖报告（0 违规）；R1/R2/R4/R6/R8 脚本 |
| D3 | M0-3 完成 + `check-all.ps1`；M0-4 POC-A | 门禁一键跑；POC-A 结论 |
| D4 | M0-5 POC-B；M0-4 fallback 决策（如需要） | 覆盖键清单 + 截图 |
| D5 | M0-6 任务拆分 + M0 复盘；评审 M1 任务卡 | `docs/14` 定稿 + M1 任务卡 |

---

## 9. M0 退出标准（全部满足才进 M1）

- [ ] `pre-framework` tag 与基线文件已提交；
- [ ] `tools/check-all.ps1` 可一键运行，R1/R3/R4/R6/R8 全绿，R2/R5 为已知红（M1 目标）；
- [ ] 依赖门禁可复现地抓到至少 1 个黑名单负例（证明不是"永远绿"）；
- [ ] POC-A 结论明确：`<arc:ArcTheme/>` 可行，或回退方案 `app.UseArcTheme()` 可行；
- [ ] POC-B 结论明确：3 个控件"纯键覆盖 / 需模板替换"分类 + 覆盖键清单；
- [ ] M1 任务卡（5 张）已评审：写作用域、依赖、DoD 明确；
- [ ] M1 排期与 v3.2 的 90 人日一致（偏差 < 10% 或已重估）。

---

## 10. 风险与回退

| 风险 | 触发 | 回退 |
|---|---|---|
| POC-A 嵌套 `<FluentTheme/>` 不可行 | XAML 解析异常 | 改 `app.UseArcTheme()` 编程式入口（消费者仍是一行） |
| POC-B 资源键覆盖不生效 | 键被模板内联/BasedOn | 局部 `ControlTheme` 覆盖；再不行列入"模板替换"清单（M2 按悲观 23 人日预留） |
| 出库后 audit 静默缺失 | 新程序集未进 Gallery 引用/命名不匹配 | 检查 `Arc.Avalonia.Docs` 是否在 Gallery `ProjectReference`；以 audit 计数为准 |
| 门禁误报太多导致"关掉门禁" | 历史代码大量命中 | 对存量设 allowlist（文件级），只对新增 fail-closed；M1 后逐步收紧 |
| 工作树已有未提交改动 | 基线不干净 | 先提交到 `pre-framework` 分支，或 stash 后打 tag |

---

## 11. 附：M0 交付物清单

| 类型 | 路径 |
|---|---|
| 基线记录 | `docs/baseline/2026-11-baseline.md`、`docs/baseline/shots-manifest.csv`、`docs/baseline/assets-pre-framework/` |
| 门禁脚本 | `tools/check-deps.ps1`、`tools/check-architecture.ps1`、`tools/check-theme.ps1`、`tools/check-no-render.ps1`、`tools/check-all.ps1`、`tools/deps-policy.json` |
| POC | `poc/ArcThemePoc/**`、`poc/TopControlSkinPoc/**`（不进 slnx） |
| ADR | `docs/adr/0001-layering.md`、`0002-dependency-policy.md`、`0003-theme-strategy.md`、`0004-top-control-keys.md` |
| 计划 | 本文 `docs/14-m0-baseline-and-gates.zh.md` + M1 任务卡 |
