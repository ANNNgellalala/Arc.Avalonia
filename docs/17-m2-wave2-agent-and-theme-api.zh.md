# 17 · M2 第二波：Agent 顶层 MVP + 主题 API + 图标扩展点

> **依据**：[`docs/13-framework-layering-and-agent-top.zh.md`](13-framework-layering-and-agent-top.zh.md) v3.2 的 M2 计划（D8 的 G4、D6 C6 的 G3g/G3h）
> **范围**：G4 Agent 顶层 MVP（≈54 人日，M 档）、G3g 图标扩展点 + 高频图标（≈4 人日）、G3h `ArcThemeOptions`（≈4 人日）
> **基线**：M2 第一波完成（30 包 `0.9.0-alpha`、门禁 7/7 GREEN、build 0 error、audit 294/294、smoke 319、Verify 60）

---

## 1. 交付总览

| 卡 | 交付 | 关键数字 |
|---|---|---|
| **G4 Agent 顶层 MVP** | 新工程 `src/Agent/Arc.Avalonia.Agent`（L4，`Arc.Avalonia.Agent`）：`IArcAgentWorkspace` / `IArcAgentSession` 纯 reducer、审批/注意力/artifact 队列、`ArcDeltaCoalescer`（Dispatcher 批次，无 Timer/帧时钟）、宿主接缝（`IArcAgentTransport` / `IArcAgentCommandSink` / `IArcAgentServices`）、`ArcAgentItemViewRegistry`（值 → DataTemplate）、组合控件 `ArcAgentSurface` + Composer/StreamingText/ToolCallCard/ApprovalTray；AgentUI transcript 增量化；边界 CI（BannedApiAnalyzers + 离线脚本） | 新增 1 包（73 KB）；源码 2,627 行；公共 API 878 行基线；Verify +38 条断言；Docs 新增 `AgentSurfacePage` |
| **G3g 图标扩展点** | `ArcIcon.Register/Unregister/Register(IArcIconProvider)/TryGetPath` + 线程安全 `ArcIconRegistry`；内置图标 161 → **264**（+103，Lucide 0.545.0，ISC） | Verify +11；图标页 264 个双主题渲染 |
| **G3h Theme API** | `ArcThemeOptions{ Scheme, Accent, AccentColor?, Density, CornerRadiusScale }` + `ArcDensity`；`Apply(app, options)` / `SetAccent(app, Color)`；种子色 → OKLCH 派生整套 palette（含 Fluent `SystemAccentColor`） | Verify +31；`--theme-report` 47/47 键、极值种子对比度 7.90–10.86 |

## 2. 实测结果

| 检查 | 结果 |
|---|---|
| 构建 | **0 error / 0 warning** |
| `--audit` | items=294 docs=294 pages=294 errors=0 warnings=0 pageFailures=0 |
| `--smoke` | itemShows=294 **pageShows=320** failures=0（319 + Agent surface 1 页） |
| `VERIFY` | **checks=140 failures=0**（60 + Agent 38 + 图标 11 + ThemeOptions 31） |
| `check-all` | **8/8 GREEN**（R1/R2/R3/R4/R5/R6/R8 + 新增 R9 agent-boundary） |
| `check-package` | **31/31 GREEN**（新增 `Arc.Avalonia.Agent`；元包 `Arc.Avalonia` 依赖形状含 Agent） |
| `check-public-api` | **31/31 MATCH**（Agent 新建基线 878 行；AgentUI +10 / 0 removed） |
| `pack` | **31 包 / 1.96 MB / `0.9.0-alpha`** |
| `--theme-report` | `THEME REPORT OK`（Light/Dark 47/47 键；Neutral/Violet/Rose + 4 组自定义种子；density 44→39/50） |
| 截图 | `shots/{light,dark}/20100-agent-surface.png`（1180×1358，无 SHOT WARNING）；`.recon/theme-shots/{light,dark}/002-icons.png`（264 图标） |

## 3. 设计要点

### 3.1 Agent 顶层是"表现运行时"，不是 Agent 运行时

* 事件模型 `ArcAgentEvent(ArcEventStamp{RunId, Sequence, EventId}, At)`：RunStart/RunPhase/Step*/Message*/Reasoning/Tool*/Approval*/Question/Attention/Artifact/StateSnapshot/Delta/Custom。
* `IArcAgentWorkspace`：Sessions/Active/Runs/BackgroundRuns/PendingApprovals/Approvals/Attention/Artifacts/GetSession/Activate/Apply/ApplyRange/Reset/Changed；`IArcAgentSession`：Messages/ToolCalls/Steps/Timeline/Snapshot/Changed。
* reducer 是**纯 fold**：重复 `EventId` → Duplicate；序列缺口 → 缓冲（≤256）补洞；未知 run → orphan（≤256），`RunStarted` 时 drain；审批全局串行 + `RequestId` 去重；注意力去重 + Blocking 优先；artifact 去重。
* 宿主边界：`IArcAgentTransport`（拉/推事件）、`IArcAgentCommandSink`（stop/pause/resume/steer/takeover/approve/answer/retry/open）、`IArcAgentServices`（打开外链/复制/确认）。顶层**不做**网络、文件、进程、定时器。

### 3.2 流式合并走 Dispatcher，不自建帧时钟

* `ArcDeltaCoalescer`：`IArcUiDispatcher`（生产实现包 Avalonia `Dispatcher.UIThread`，`DispatcherPriority.Background`；测试用 `QueuedUiDispatcher`），每回调 flush 一个批次；无 16 ms 常量、无 `FrameClock`、无 `RenderTimer`；队列有 `maxBatchSize=2048` 上界与 `PendingHighWatermark` 观测。
* `ArcAgentTranscript` 增量 API 全部**新增**：`Append/AppendRange/AppendOrUpdate/ResetIncremental` + 计数（`FullRebuildCount`/`RenderedRowCount`/`TotalEventCount`/`DroppedRowCount`/`ViewportRows`/`MaxRenderedRows`）；同 id delta 直改 `TextBlock`，超出有界窗口（默认 2× 视口 = 80 行）丢头行。
* 10k 事件实测：**0 次全量重建**、`rendered=80`、`dropped=9920`、`total=10000`、分配 < 256 MB、耗时 ≈1.0 s（headless）。原 transcript API 签名/语义不变（PublicApi +10 / 0 removed）。

### 3.3 视图注册表（不是"渲染器"）

* `ArcAgentItemViewRegistry`：值类型 → `DataTemplate`，默认内置消息/工具/审批/artifact 视图，`+CreateTemplate`；目录命名从 `Rendering/` 改为 `Views/`，避免被误读为渲染层（评审 R3 第 3 条）。

### 3.4 边界 CI（第 8 道门禁）

* 主门禁：`Microsoft.CodeAnalysis.BannedApiAnalyzers` 3.3.2（`PrivateAssets=all`，`RS0030 → error`，`AdditionalFiles=tools/BannedSymbols.txt`；实测能拦 `HttpClient`）。
* 兜底：`tools/check-agent-boundary.ps1`（离线扫源码禁用符号 + ProjectReference/PackageReference 白名单），已接入 `check-all` 为 **R9**。
* 禁用清单：`HttpClient`/`HttpRequestMessage`/`File`/`Directory`/`FileStream`/`Process`/`System.Threading.Timer`/`System.Timers.Timer`/`DispatcherTimer`/`TrayIcon`/`IStorageProvider`/`IStorageFile`。

### 3.5 图标扩展点（G3g）

* 查找顺序：**运行时注册 → 已注册 provider → 内置**；内置不可被 provider 覆盖、可被 `Register` 覆盖；注册/注销精准失效几何缓存；`ArcIconData.Paths` 保持只读（内置 + 高频合并，内置优先）。
* 新增 103 个高频图标来自 Lucide 0.545.0（jsDelivr 批量取 SVG，ISC 许可），circle/rect/line 已机械转 path 命令，无重名；191 → 264。
* path data 属数据不属颜色，R6 token-only 不适用（门禁已验证不误报）。

### 3.6 主题 API（G3h）

* `ArcPalette.AccentColor(scheme, options)`：种子色 → OKLCH（保 hue、chroma 夹 0–0.30）→ 浅色 `l∈[0.36,0.72]` / 深色 `l+0.28` → strong 再偏移一档；前景按 WCAG ≥ 4.5 选黑/白；**无任何硬编码 hex**（R6 GREEN）。
* `Density`：缩放尺寸 token 与 Fluent 输入内边距（`default 44 / compact 39 / comfortable 50`）；`CornerRadiusScale` 缩放圆角（pill 除外）。
* 自定义品牌色同时驱动 Arc token 与 Fluent `SystemAccentColor`（`--theme-report` 断言二者完全一致）。

### 3.7 包拓扑

* `Arc.Avalonia` 元包按 docs/13 §2.3 的"`Theme` + 全部 L2 + Agent"补齐 `Arc.Avalonia.Agent` 依赖（31 包总数不变，`check-package` 的 `$agents` 期望同步为 3 个包）；`Arc.Avalonia.All` 仍只加 Special/Blocks。
* L4 → L4 同层兄弟边（Agent → AgentUI）已登记在 `tools/architecture-allow.json`（`"L4": ["L1","L2","L3","L4"]` + note）。

## 4. 遗留（进 M3 / M2 收尾）

1. **API 基线已更新**（`tools/api-baselines/` 31 个包）；`Applied` 事件签名变更只影响 Theme 内部消费（M2 未发布）。
2. **`check-agent-boundary` 已作 R9 接入 check-all**；分析的 `buildOnlyPackages` 已从 check-deps 发布闭包剔除。
3. **transcript 是有界窗口 MVP**：完整 scrollback 虚拟化（无丢弃、可回滚）留 M3；`DroppedRowCount` 是显式设计而非缺陷。
4. **density 覆盖不完整**：目前只作用于尺寸 token 与 Fluent 输入内边距，组件内部硬编码尺寸待 M2 后续统一；TextBox/ComboBox 字号仍需局部 `ControlTheme`（M1 遗留）。
5. **`ArcServiceProvider` 为进程级静态**（多窗口隔离留 M3）。
6. **`ArcWindow` Custom 标题栏**仅编译验证，未做 Win32 交互点验。
7. 门禁报告与截图在 `.recon/`（不入库）；ADR 增量见 `docs/adr/0002`（R9 门禁清单）、`0003`（§10 ThemeOptions）、`0004`（§9 图标扩展点）。
