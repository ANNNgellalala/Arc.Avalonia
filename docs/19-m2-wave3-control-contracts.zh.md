# 19 · M2 第三波：组件契约化（TemplatePart + RoutedEvent）+ Charts 主题化

> **依据**：[`docs/13-framework-layering-and-agent-top.zh.md`](13-framework-layering-and-agent-top.zh.md) v3.2 的 D6 **C1（组件契约与 API 收敛，53 人日）** 第一刀
> **范围**：Top 交互事件 → `RoutedEvent`、全部模板控件补 `[TemplatePart]`、`PART_*` 命名统一、Charts 22 控件主题化、契约门禁 R10 + 契约规范
> **基线**：M2 第二波完成（31 包 `0.9.0-alpha`、门禁 8/8 GREEN、Verify 140、audit 294/294、smoke 320）
> **并行方式**：7 个写域互斥的切片（按项目族切分）+ 1 份 l10n 前瞻审计，8 名队员同时推进；写域见 §1 表

---

## 1. 交付总览

| 切片 | 写域 | 交付 | 数字 |
|---|---|---|---|
| **A** Foundation + Inputs | `src/Foundation`、`Components/Inputs` | `ArcButton` 焦点部件契约；Inputs 27 个取件控件全量 `[TemplatePart]`；Top 10 事件路由化 | 160 条属性、147 处取件改名、152 处主题改名、10 个 RoutedEvent |
| **B** Actions + Navigation + Overlays | 三个组件项目 | 27 个类契约化；主题 `x:Name`/`#选择器` 同步 | 119 条属性、127 处改名、15 处选择器、24 个 RoutedEvent |
| **C** Feedback + DataDisplay | 两个组件项目 | 排序/选择/展开/行激活/工具栏全部路由化；窗口化控件零改动 | 48 条属性、17 处改名、23 个 RoutedEvent |
| **D** Pickers + Effects | 两个组件项目 | 选择/范围/提交/输入 + Open/Index/Detent 路由化 | 137 条属性、45 处改名、16 个 RoutedEvent |
| **E** Media + Content + AgentUI + LifeToday | 4 个项目 | 卡片/轮播/滑动 + 聊天/清单 + 6 个 AgentUI + 7 个 LifeToday 事件路由化 | 45 条属性、37 个 RoutedEvent |
| **F** Charts | `Components/Charts` | 20 个 ControlTheme（全 token）；画笔/字号字面量清零；`ArcChartBase` 上 4 个可冒泡交互事件 | 20 主题、24→0 字面笔厚、88→0 字号字面、5 个 RoutedEvent |
| **G** 契约门禁 + 规范 | `tools/`、`docs/adr`、`docs/18` | `tools/check-contracts.ps1`（**R10**）+ `contracts-baseline.json`；ADR 0005；契约规范文档 | R10 228 文件、9/9 门禁 |
| **H** l10n/键盘/RTL 前瞻 | `docs/analysis/18` | G3d 最小集可执行清单（63 串 / 9 文件）、`.resx` 机制建议、140 个 DP 默认值风险、178 个 RTL 敏感点、15 控件 a11y 清单 | 518 行 |

## 2. 实测结果

| 检查 | 结果 |
|---|---|
| 构建 | **0 error**（余 3–9 条既有警告：Docs/Blocks/Gallery 的 CS0105/CS0219/CS0649/CS0618/CS0169 + `ArcThemeOptions` CS1580；本轮修掉 2 条 CS0419 cref 歧义） |
| `VERIFY` | checks=140 failures=0（本波未加断言，见 §4-2） |
| `--audit` | items=294 docs=294 pages=294 errors=0 warnings=0 pageFailures=0 |
| `--smoke` | itemShows=294 pageShows=320 failures=0 |
| 渲染抽检 | Calendar（Pickers）、Dialog（Overlays）双主题截图正常：选中胶囊/区间淡底、模态遮罩/危险主行动/Danger 复选均正确 |
| `check-all` | **9/9 GREEN**（R1/R2/R3/R4/R5/R6/R8/R9 + 新增 **R10**），knownRed=0 red=0 |
| `check-contracts` | TemplatedControls=**108** / WithTemplatePart=**108**（100%）/ RoutedEventFields=**115** / WithWrapper=**115** / LegacyEventsWithRouted=75 / MissingTemplatePart=**0** / **allow=0** |
| `check-public-api` | **31/31 MATCH**；基线 +574 / −15 行 |
| `pack` | **31 包 / 2.02 MB / `0.9.0-alpha`**；`PACKAGE GATE 31/31 GREEN` |

> 全仓 `[TemplatePart]` 属性 **511 条**、`RoutedEvent.Register` **115 个**、`x:Name`/`#选择器` 改名约 **340 处 C# + 175 处 axaml**。

## 3. 设计要点

### 3.1 契约形态：TemplatePart

* API：`Avalonia.Controls.Metadata.TemplatePartAttribute`，**构造式**用法 `[TemplatePart("PART_X", typeof(Border))]`，属性必须挂在**控件类声明正上方**（放在类前、被 args 类隔断会被门禁判为缺失——本波真实踩坑）。
* 命名统一 `PART_*`；`OnApplyTemplate` 里 `GetTemplateChild`/`NameScope.Find` 的字符串与主题 `x:Name` 必须同步（含 `#PART_*` 选择器）。基类声明的部件由派生类继承（`Inherited=true`）。
* 覆盖策略：**凡按名取件必声明**，包括运行时兜底走视觉树的控件（如 `ArcVirtualList` 声明宿主 ListBox 的 `PART_ScrollViewer` 并优先按名取、视觉树兜底，保持第三方主题兼容）。

### 3.2 契约形态：RoutedEvent（严格 additive + 双通知）

* 决策 **A**（ADR 0005）：**旧 CLR 事件一字不改、继续 `Invoke`**；新增 `RoutedEvent.Register<TOwner, TArgs>("Name", RoutingStrategies.Bubble)` 静态字段 + CLR 包装，触发点一次旧 `Invoke` + 一次 `RaiseEvent`。拒绝方案 B（同名转换）的理由：会 breaking 320 个 Docs 页面与外部消费者。
* 包装命名两分支（已入 R10 规则，不是例外）：类中存在同名旧 CLR 事件 → 必须 `<Name>Routed`；不存在同名旧事件 → 同名 `<Name>` 或 `<Name>Routed` 均合规（贴合 Avalonia 惯例）。
* args：新 `*EventArgs : RoutedEventArgs`（含 `(RoutedEvent)` ctor + `init` payload）；既有 args 类改为继承 `RoutedEventArgs` 并保留原 ctor（源兼容、二进制 breaking，alpha 阶段已在 ADR 记录）。
* 覆盖：115 个 RoutedEvent，`WithWrapper=115`（100%），优先"驱动业务"的语义（选择/提交/关闭/展开/排序/审批/停止）。

### 3.3 Charts 主题化

* 20 个 `ControlTheme`（`{x:Type charts:Arc*}`），setter 只引 Arc token；`ArcChartCard` 经 `StyleKeyOverride` 复用 `ArcCard` 主题。
* 新增 `Core/ChartMetrics.cs` 把字号/圆角/间距/笔厚集中为 token 派生：`new Pen(x, 1)` 字面 24→0，`ChartText` 字号字面 88→0；颜色本就全走 `ChartPalette`/`ArcResources`（hex=0）。
* 自绘控件无模板，故 `TemplatePart` 不适用（`ArcChartBase.Contract.cs` 说明并预留 `PART_Plot/PART_Legend/PART_Tooltip`）。

### 3.4 契约门禁 R10

* `tools/check-contracts.ps1`（228 文件）：`missing-templatepart`、`undeclared-part`、`routedevent-no-wrapper`（两分支命名）、`routedevent-args-base`；`legacy-event-missing-routed` 仅统计（exempt=30，见 §4-1）。
* `-Baseline`/`-UpdateBaseline` + `tools/contracts-baseline.json`：本波结束时 **allow=[]**（过渡基线清空，不留"为绿而绿"的豁免）。
* 已接入 `check-all` 为第 9 道门禁，输出 `green=9/9`。

## 4. 遗留（进后续波次）

1. **30 个 legacy 事件仍无路由对应物**（R10 以 exempt 统计）：Pickers 14（cropper/upload/color swatches/signature 等长尾）、Effects 4、AgentUI dock/attention 长尾、其余分散在各库；下波按"业务驱动优先"补齐。
2. **无运行时断言**：本轮只做编译/静态门禁 + 渲染抽检，`Verify` 仍 140（未加路由事件冒泡断言）。M3 的测试基线（G3f，24 人日）里补"`RaiseEvent` 冒泡到父容器"的头less 断言。
3. **`*Size` 枚举统一、`ItemsSource` 迁移、公开面收敛**（C1 剩余部分）未做，仍是 M2 收尾工作。
4. **键盘/焦点契约文档**已产出（docs/18 + ADR 0005），但无 `AutomationPeer`/`AutomationId`/`TabIndex`（M3 G3d 15 人日）。
5. **Charts**：Gauge/Sparkline/Ridgeline/Slope/AnimatedCounter 暂无 raise（绘制无可点区域）、键盘激活未发路由事件。
6. **l10n 最小集（G3d 6 人日）** 已有可执行清单（`docs/analysis/18-l10n-keyboard-rtl-audit.zh.md`：9 文件 / 63 串 / `ArcDateMath` en-US 写死 ≈0.5 人日），可下一波直接开工。
