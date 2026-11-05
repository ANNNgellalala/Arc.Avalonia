# ADR 0002 · 外部依赖策略与依赖门禁（R3，含传递闭包）

- 状态：Accepted
- 日期：2026-11-05
- 范围：M0-2/M0-3（docs/14 §3/§4 R3）；实现 `tools/check-deps.ps1` + `tools/deps-policy.json`
- 相关：[docs/13 §2.2](../13-framework-layering-and-agent-top.zh.md)、[ADR 0001](0001-layering.md)

## 背景

Arc 的目标是"自包含 Avalonia UI 框架"：只允许官方 Avalonia 平台包，禁止第三方 UI/组件/图标库。
docs/14 §3.4 的验收要求：当前树 0 违规；人为加黑名单包必须 exit 1 且报出包名与引入路径；
"间接引入"也必须命中。只看直接 `PackageReference` 会被"官方附加控件/项目引用间接带进来的包"绕过，
因此判定必须基于 `obj/project.assets.json` 的 **targets 依赖图**（完整传递闭包），而不是只读 csproj。

## 决策

### 1) 单一策略文件 `tools/deps-policy.json`（fail-closed）

| 类别 | 内容 | 判定 |
|---|---|---|
| 直接官方 | `Avalonia`、`Avalonia.Base`、`Avalonia.Controls`、`Avalonia.Markup.Xaml`、`Avalonia.Themes.Fluent`、`Avalonia.Fonts.Inter`；`Avalonia.Desktop` 仅 apps；`Avalonia.Controls.DataGrid/ColorPicker` 仅 Feature | 允许（越权 → 违规） |
| 传递/平台 | `Avalonia.Skia/HarfBuzz/Win32/X11/Native/Wayland/FreeDesktop/Remote.Protocol/BuildServices/Angle.Windows.Natives`、`SkiaSharp`、`HarfBuzzSharp`、`MicroCom.Runtime`、`Tmds.DBus.Protocol`、`SkiaSharp.NativeAssets.*`、`HarfBuzzSharp.NativeAssets.*`、`runtime.*` | 允许 |
| 构建/SDK | `System.*`、`Microsoft.*`（docs/14 要求显式枚举；出现新条目评审需确认非 UI 运行时库） | 允许但记录 |
| 黑名单 | `AtomUI.*`、`Semi.Avalonia*`、`Ursa*`/`Irihi.*`、`FluentAvalonia*`、`SukiUI*`、`Material.*`、`AntDesign*`、`IconPacks.*`、`Lucide.*`、`Dock.*`、`Tabalonia*`、`AvaloniaEdit*`、`Markdown.Avalonia*`、`DialogHost*`、`Svg.*`、`ShimSkiaSharp`、`ColorDocument*` | **deny 优先，直接或传递命中即失败** |
| 未列名 `Avalonia.*` | 任何不在白名单的 Avalonia 官方族包（如 `Avalonia.Themes.Simple`） | 拒绝（`unknown-avalonia-package`） |
| 其它未知包 | 默认拒绝（fail-closed），不允许"运行时才发现" | 拒绝（`package-not-allowed`） |

判定单位是 **NuGet package id**（小写比较/大小写不敏感），不是 dll 文件名。

### 2) 扫描范围

- 发布库：`src/Foundation/**`、`src/Components/**`、`src/Agent/**`、`src/Special/**`、`src/Blocks/**`
  （以及 M1 后出现的 `src/Theme/**`、`src/Feature/**`、`src/Patterns/**`）；
- 默认排除 `src/Apps/**`、`tests/**`、`poc/**`、`.recon/**`；`-IncludeApps` 可把 `src/Apps/**` 纳入
  （应用允许 `Avalonia.Desktop`）；
- `Feature.*` 工程（路径 `src/Feature/` 或程序集名含 `Arc.Avalonia.Feature.`）额外允许
  `Avalonia.Controls.DataGrid/ColorPicker`（R7 的落地方式）。

### 3) 闭包算法（必须覆盖"间接引入"）

对每个项目的 `obj/project.assets.json`：

1. 取 `targets.<tfm>`；把每项拆成 `package` / `project` 节点，`dependencies` 为出边，id 大小写不敏感；
2. NuGet 不把根项目写进 targets，因此合成根节点：
   - 直接包依赖 ← `project.frameworks.<tfm>.dependencies`（`target=Package`）；
   - 直接工程引用 ← `project.restore.frameworks.<tfm>.projectReferences`，经 `libraries[].path` 解析回工程 id；
3. BFS 求完整闭包并记录最短引入路径；**project 节点继续下钻**（其它库间接带来的包也会被看到）；
4. 只对 `package` 节点做策略判定，记录 `root → … → 违规包` 的引入路径与 tfm；
5. assets 缺失时只对**单个 csproj** 串行 `dotnet restore`；文件锁/解析失败 sleep 10s 重试一次，
   仍失败记为 ERROR（exit 2），不静默放过；
6. **build-only 排除**：`deps-policy.buildOnlyPackages`（当前 `Microsoft.CodeAnalysis.BannedApiAnalyzers`）是
   编译期 analyzer/tooling（`PrivateAssets=all`），会出现在 assets 的 build 节点但**不应进入发布包依赖闭包**；
   check-deps 把它归为 `build-only` 并从发布闭包统计中剔除（报告 notes 会列出被剔除项），发布物侧由 R5 复核。

### 节点 id 与工程名解耦（M1-T2a 修正，2026-11-05）

NuGet 的 targets 节点键是 **PackageId**，MSBuild 的默认 PackageId = AssemblyName；它与 csproj 文件路径 /
工程文件名可以不同。M1-T2a 把 `src/Foundation/Arc.Avalonia/Arc.Avalonia.csproj` 的 AssemblyName 改为
`Arc.Avalonia.Foundation` 后，targets 键变成 `Arc.Avalonia.Foundation/1.0.0`，而旧脚本用工程文件名
`Arc.Avalonia` 查节点 → 25 条 `scan-error: 依赖图中的节点在 targets 中不存在`。修正为两层解析：

1. **构建 PackageId 映射**：扫描 `src/**` + `tests/**` 的 csproj，取 `PackageId` → 缺省 `AssemblyName`
   → 再缺省工程文件名；建立"绝对 csproj 路径 → PackageId"与"工程文件名 → PackageId"两级大小写不敏感映射；
2. **libraries[].path 基准修正**：NuGet 里 `libraries[].path` 的 `type=project` 路径是**相对工程目录**
   （不是相对 `obj/`）；解析出的绝对 csproj 路径经上面的映射取 PackageId（并保留 `obj/` 作为历史兼容候选）；
3. **兜底别名**：若依赖字典里出现 `X` 而 targets 无 `X` 节点，依次尝试 csproj PackageId 映射与
   `X.Foundation` 候选（覆盖"依赖字典写工程名、节点键写 PackageId"的形态）。

修复后 R3 回到 GREEN（0 违规、0 scan-error）。**注意**：以后任何 AssemblyName/PackageId 改名都由该映射自动跟随；
不需要在脚本里硬编码包名。

## 失败判定与三态

- 任何 `denied` / `unknown-avalonia` / `package-not-allowed` / Feature 包越界 → **新增违规，exit 1**；
- R3 当前**没有** known-red 基线：docs/14 实测当前树确实只引官方包，任何命中都必须修；
- 报告：`.recon/gates/deps-<timestamp>.md`、`deps-<timestamp>.json`、`deps-latest.json`
  （`.recon/` 已 gitignore；docs/14 原写 `artifacts/gates/`，M0 统一到 `.recon/gates/`）；
- 退出码：`0` 无违规 / `1` 违规 / `2` 无法完成检查。

## 门禁清单（check-all 8 行）

| 规则 | 脚本 | 说明 |
|---|---|---|
| R1/R2 | `check-architecture.ps1` | 分层方向 + 发布库不引用 Demo/Docs（ADR 0001） |
| R3 | `check-deps.ps1` | 外部依赖白/黑名单 + 传递闭包（本 ADR） |
| R4 | `check-theme.ps1` | 主题来源只允许 Arc/官方 Avalonia 主题 |
| R5 | `check-package.ps1` | `dotnet pack` 产物：无 Pages/Docs、依赖闭包、许可/XML |
| R6 | `check-token-only.ps1` | 禁 hex/rgb/FromArgb（存量豁免在 `token-allow.json`） |
| R8 | `check-no-render.ps1` | 禁自研帧时钟/绘制缓存/主题失效协议 |
| **R9 边界** | `check-agent-boundary.ps1` | Agent 程序集禁止 `HttpClient/HttpRequestMessage/File/Directory/FileStream/Process/Timer/DispatcherTimer/TrayIcon/IStorageProvider/IStorageFile`；主门禁是 **BannedApiAnalyzers（RS0030 → error）**，脚本是离线兜底（报告为简化 schema：`gate/status/issues`，由 `check-all` 兼容），不设 known-red。 |

`check-all.ps1` 现输出 8 行（R1/R2 拆行、R5/R9 各自提供），**全部 GREEN 才通过**；R5 在 `artifacts/` 未生成/过期时 RED。`-KnownRed` 严格模式仍只作用于 known-red 行（当前为 0）。

## `-SelfTest`：证明门禁不是"永远绿"

`tools/fixtures/self-test/**` 用**伪造的 `project.assets.json`**（不编译、不联网）覆盖四类断言：

| fixture | 类型 | 断言 |
|---|---|---|
| `blacklisted-direct` | 负例 | 根项目直接引用 `Semi.Avalonia` → 必须命中，路径含根与包 |
| `transitive-svg` | 负例 | 白名单包 `Avalonia.Themes.Fluent` 合成依赖 `Svg.Skia` → 经传递闭包命中，路径含中间包 |
| `case-insensitive-direct` | 负例 | 依赖字典写 `semi.avalonia`、节点写 `Semi.Avalonia` → 大小写不敏感命中 |
| `clean` | 正例对照 | 只有官方白名单包 → 0 违规（防止门禁误报/永远红） |

任一断言失败 → `check-deps.ps1 -SelfTest` exit 1。

## 验证结果（2026-11-05 M1 / 2026-11-06 M2）

命令与输出（本机 pwsh 7.6.7 / dotnet 10.0.114 / Avalonia 12.1.4）：

```text
> pwsh -NoProfile tools/check-deps.ps1 -SelfTest
SELFTEST PASS blacklisted-direct: Semi.Avalonia
SELFTEST PASS case-insensitive-direct: Semi.Avalonia
SELFTEST PASS clean: 干净项目未被误报
SELFTEST PASS transitive-svg: Svg.Skia
[R3-SelfTest deps-selftest] GREEN checked=4 new=0 knownRed=0 exempt=0
# exit 0（负例均被抓住、正例不误报）

> pwsh -NoProfile tools/check-deps.ps1
[R3 deps] GREEN checked=26 new=0 knownRed=0 exempt=0
# 26 个发布库项目、130 个包节点（直接 52 / 传递 78），0 违规、0 scan-error

> pwsh -NoProfile tools/check-all.ps1
M0 GATES - 2026-11-05 23:40:23
Rule     Status        New  KnownRed  Exempt  Checked  Report
R3       GREEN           0         0       0       26  .recon/gates/deps-latest.json
R1       GREEN           0         0       0      140  .recon/gates/architecture-latest.json
R2       GREEN           0         0       0       26  .recon/gates/architecture-latest.json
R4       GREEN           0         0       0      117  .recon/gates/theme-latest.json
R6       GREEN           0         0      28      764  .recon/gates/token-only-latest.json
R8       GREEN           0         0       0      764  .recon/gates/no-render-latest.json
R5       KNOWN-RED       0         1       0        0  (M0 不实现，见 gates-baseline.json)
GATES: green=6 knownRed=1 red=0 errors=0 => KNOWN-RED
# exit 0（除 R5 外全 GREEN，红=0）
```

另外实测：`check-all.ps1 -KnownRed` 严格模式 exit 1（R5 已知红）；`check-deps.ps1 -IncludeApps`
扫描 29 个项目（26 发布库 + Gallery + Docs 叶子工程）仍为 GREEN；三个负例 fixture 的检出内容与修复前一致，
证明节点解析改动没有削弱黑名单检测。

> 以上为 M1 历史快照。2026-11-06 起 R5 已可执行、R9 已接入、known-red 为空：`check-all -KnownRed` 为 exit 0，
> 见下方「M2 增量复验」。

### 本次修复对照（门禁自身误报）

| 现象 | 根因 | 修复 |
|---|---|---|
| R3 ERROR 25：`targets 中不存在 net10.0\|Arc.Avalonia` | 节点 id 随 AssemblyName 改名，脚本按工程名查 | PackageId 两级映射 + `libraries[].path` 相对工程目录 + 别名兜底（见 §3） |
| R1 RED 1：`Arc.Avalonia.Internal [L1] -> Arc.Avalonia.Foundation [L1]` | `allowedEdges.to` 写死旧名，改名后失效 | 例外支持 `from/to` + `fromProject/toProject` 多字段匹配（ADR 0001 E1），`until` 改为 M1-T2b |
| R6 RED 2：`src/Docs/.../Pickers/Pages/ColorPickerPage.cs` | Pages 迁入 Docs 工程，旧路径豁免失效 | `token-allow.json` 两条 Page 豁免迁移到新路径并清理旧条目（当前豁免 28 条、零命中 0 条） |
| R2 KNOWN-RED 48 | M1-T1 前发布库仍引 Demo / 嵌 Docs | 已被 M1-T1 清零：`gates-baseline.json` R2 counts=0、permittedProjects=[]、expected=false |

### M2 增量复验（2026-11-06，8 行门禁）

R9 接入后 check-all 由 7 行变 8 行，全部 GREEN（本机；artifacts 已按当前 csproj 重新 pack）：

```text
M0 GATES - 2026-11-06 02:33:15
Rule     Status        New  KnownRed  Exempt  Checked  Report
R3       GREEN           0         0       0       31  .recon/gates/deps-latest.json
R1       GREEN           0         0       0      190  .recon/gates/architecture-latest.json
R2       GREEN           0         0       0       29  .recon/gates/architecture-latest.json
R4       GREEN           0         0       0      118  .recon/gates/theme-latest.json
R6       GREEN           0         0      28      800  .recon/gates/token-only-latest.json
R8       GREEN           0         0       0      800  .recon/gates/no-render-latest.json
R9       GREEN           0         0       0        0  .recon/gates/agent-boundary-latest.json
R5       GREEN           0         0       0       31  .recon/gates/package-latest.json
GATES: green=8 knownRed=0 red=0 errors=0 => GREEN   # 8/8，exit 0
```

- `check-deps.ps1`：GREEN checked=31（发布库含 M2 新增 `src/Meta` 等），发布闭包 159 个包节点；build-only 剔除
  `Microsoft.CodeAnalysis.BannedApiAnalyzers`（1 个），0 违规、0 scan-error；
- `check-agent-boundary.ps1`：GREEN issues=0；禁止符号与主/兜底关系见「门禁清单」R9；其报告是简化 schema
  （`gate/status/issues`），`check-all` 单独兼容成一行 R9，不进入 known-red；
- 复验注意：R5 曾因 `src/Meta/Arc.Avalonia.Meta` 新增 Agent 依赖但 `artifacts/` 仍是旧包而短暂 RED；重新 pack 后 GREEN —
  这是产物同步问题，不是门禁误报（check-package 的 fail-closed 行为正确）。

## 偏离与遗留

1. 报告目录用 `.recon/gates/`（与 docs/14 §3.3 的 `artifacts/gates/` 不同），原因是本工作树 `.gitignore`
   已忽略 `.recon/`，避免误提交门禁产物；如 M1 需要归档报告，再改目录并同步 docs/14。
2. R5（`dotnet pack` 产物内容）已由 `tools/check-package.ps1` 实现并接入 check-all（`artifacts/` 缺失/过期时 RED，
   需先 `dotnet pack`）；BannedApiAnalyzers 已作为 **R9 Agent 边界**接入（主门禁 analyzer，脚本离线兜底）。
3. R6/R8 的存量例外是**已登记豁免**而非 known-red：`token-allow.json` 按 `file+rule+max 行数` 记录
   （当前 28 条、全部有 reason，Pages 两条已随 M1-T1 迁到 `src/Docs/...`），`render-allow.json` 当前为空；
   新增命中一律 fail-closed。
4. L3→L1 的直连属 ADR 0001 的例外 E2，不在 R3 依赖策略里（R3 只管外部 NuGet 包）。
5. M1-T1 出库后 `src/Docs` 是 app 叶子工程：`deps-policy.json` 的 `appsRoots` 已列入，默认扫描不包含，
   `-IncludeApps` 时按应用规则检查；发布库扫描包含 M2 新增的 `src/Meta`、`src/Theme`、`src/Service` 等（见 policy）。
6. R2 的 known-red 已清零（M1-T1）；R5 已转为可执行门禁，当前 `gates-baseline.json` 无 known-red 条目。
7. **build-only 不变量**：`Microsoft.CodeAnalysis.BannedApiAnalyzers` 只在编译期生效（`PrivateAssets=all`），
   不出现在任何 `*.nupkg` 的依赖组里；R3 把它从发布闭包剔除，R5 复核 nuspec 依赖，两者互补。

## 维护

- 改白/黑名单：改 `tools/deps-policy.json`，在本 ADR 记一行理由（评审 diff 可见）；
- 改 known-red 基线：改 `tools/gates-baseline.json`；R2 已于 M1-T1 清零，R5 已转为可执行门禁（无 known-red）；
- 新增 build-only analyzer/tooling：加入 `deps-policy.buildOnlyPackages` 并在本 ADR §3 记录（R3 自动剔除、R5 复核产物）；
- 新增自绘 alpha 例外：改 `tools/token-allow.json`（见 R6）；新增历史渲染信号：改 `tools/render-allow.json`（见 R8）；
- **改 AssemblyName/PackageId 或移动 csproj**：targets 节点键会随之变化。R3 已改为读 `PackageId`（缺省
  AssemblyName）并缓存路径映射，一般无需改脚本；但 `architecture-allow.json` 的例外与
  `token-allow.json` 的文件路径需同步（本次 M1-T2a/T1 各出现一次，见上方修复对照）。
