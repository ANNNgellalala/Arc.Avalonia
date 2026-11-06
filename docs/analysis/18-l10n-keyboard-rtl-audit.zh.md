# 18 · 本地化最小集 / RTL / 键盘焦点审计（G3d 前置）

> **性质**：只读审计 + 可执行清单（为 G3d 本地化最小集 6 人日、M3 本地化全量 9 人日、无障碍 15 人日做准备）
> **日期**：2026-11-07｜**审计范围**：`src/Components` / `src/Foundation` / `src/Agent`（34 个项目中的 800 个 `.cs`/`.axaml`，排除 `obj/bin`）
> **约束**：未改源码/其它文档、未运行 `dotnet build`；行号为 **2026-11-07 快照**，M2 第三波正在并行改契约（TemplatePart/RoutedEvent），±2–5 行漂移属正常，引用时用"文件:行（≈）"。
> **关联**：`docs/13-framework-layering-and-agent-top.zh.md` v3.2 的 C5（本地化/RTL 15 人日 = M2 6 + M3 9）与 C4（无障碍 15 人日）、G3d=30；`docs/16`/`docs/17` 的 M2 第一/二波交付（服务层、Theme API、图标扩展点）。
> **结论先行**：**6 人日最小集没有技术阻断，但有三类必须先处理的"隐性成本"**：(1) 日期文化被写死为 en-US（`ArcDateMath`，功能级）；(2) 140 个字符串 DP 默认值是公开 API 行为，不能直接换资源查找，必须"默认保留 + 渲染期按 `IsSet` 回退"；(3) 主题 XAML 里另有 48 处硬编码文案（`Today`/`Browse files` 等），不在 C# resx 的扫描视野内。RTL 与键盘/无障碍不在本轮 6 人日范围，但审计给出 9/15 人日的可执行拆分。

---

## 0. 一页结论

### 0.1 规模（本次快照实测）

| 维度 | 数量 | 说明 |
|---|---:|---|
| 英文串候选（用户可见） | **≈384–388** | DP 默认值 136–140 / `Text=...` 类赋值 119 / `AutomationProperties.SetName`、`HelpText`、`ItemStatus` 字面量 81 / 主题 XAML 48；另有 minified 单行 axaml 被跳过（少算约 20–40） |
| 字符串 DP 默认值 | **≈140** | 分布在 13 个项目；是公开 API 行为，改值 = breaking（见 §3） |
| 插值/格式串（`$"..."`） | 415 处总插值串 | 其中用户可见的需人工筛（如 `$"Page {n}, current page"`、`$"Up to {n} files"`） |
| 固定 `en-US` 文化 | **7 处** | `ArcDateMath.cs:12` 是根因，日历/日期/范围/签名日期全部使用它 |
| `InvariantCulture` vs `CurrentCulture` | 69 vs 5 | 日期/数字格式化大多仍是 invariant，属 l10n 风险面 |
| RTL 敏感点 | **178** | 非对称水平 Margin 86 / `HorizontalAlignment=Left` 或 `Right` 62 / 方向性图标 21 / `FormattedText` 固定 LTR 9；`RightToLeft`/真正 RTL 分支 **0** |
| 键盘/无障碍现状 | — | `OnKeyDown` 258、`IsTabStop` 66、`:focus-visible` 50、`FocusAdorner` 19、`AutomationProperties` 752；**`AutomationPeer` 0、`AutomationId` 0、`TabIndex` 0**；`AccessibleRole` 仅 6 处（Pickers 基类/子类） |
| 组件契约（M2 三波成果） | — | `[TemplatePart]` 367、`RoutedEvent` 31——a11y 的"骨架"已有，缺 peer/值模式 |

### 0.2 三条判定

1. **G3d 6 人日最小集可行**：范围 = `.resx` 骨架 + `ArcLocalization` + 分页/日历/对话框/Toast/FileDropzone/EmptyState 的 **≈63 个唯一字符串** + 主题 XAML 4 处 + 日期文化切换；不改任何 DP 默认值。见 §1.4 的 6 天拆分。
2. **本地化机制推荐 `ArcStrings.resx` + `ResourceManager`（内部访问器）+ `ArcLocalization.Override`，不用 `DynamicResource` 字典**（理由见 §2.1）：现有 200 处文案是 **C# 构造**的（`Text =`/`SetName(...)`），不是 XAML 绑定；`DynamicResource` 覆盖不到，且失去编译期 key 校验/翻译工具链/测试可覆盖性。
3. **阻断性问题：无**。唯一的"技术性阻断"是 `ArcDateMath.Culture = en-US`（不改则 zh 日历仍显示英文月份/星期），但它是一个 internal 静态类，改造成本 ≈0.5 人日，已包含在 6 人日内。**风险项**：DP 默认值 140 个（不能让渲染回退污染 `GetValue`）、主题 XAML 48 处硬编码、`AutomationId` 0 处（M3 a11y 需先立约定）。

### 0.3 建议给 Lead 的决策点

| 决策 | 建议 |
|---|---|
| 6 人日最小集范围 | 只做 §1.4 清单（9 个源文件 + 4 个主题 + `ArcLocalization`/resx/测试），**不动** 140 个 DP 默认值、不翻译 294 页文档 |
| 机制 | `.resx`（en + zh-Hans）+ 内部 `ArcStrings` + 公开 `ArcLocalization`；语言切换用"控制级订阅 + 重新应用文案"，不做全局 `DynamicResource` |
| DP 兼容 | 保留所有 DP 默认值（公开 API 基线归零），渲染期用 `IsSet(prop)` 回退到当前语言；新增 read-only `Effective*` **不**进公开 API（内部用） |
| 日期 | `ArcDateMath.Culture` 改为 `ArcLocalization.Culture`；`DateFormat` DP 默认值保留，未显式设置时渲染用当前文化的短日期模式 |
| RTL（M3 9 人日） | 先落 §4.3 脚本 + "RTL smoke 页"；修复顺序 Pickers/Inputs（94 处）→ Actions/Effects/Overlays（42）→ 其余（42），最后 FormattedText 9 处 |
| 键盘/a11y（M3 15 人日） | 按 §5.2 的 15 控件清单逐项补 peer/值模式/AutomationId；先 DataGrid/Calendar/Dialog 三个重控件 |

---

## 1. 英文串清点（用户可见字面量）

### 1.1 口径

- 扫描 `src/Components|Foundation|Agent` 的 `.cs`/`.axaml`；**排除** `obj/bin`、注释/XML doc（`///`）、`{TemplateBinding ...}`、`{x:...}`、纯图标名/文件扩展名/文化标签。
- 分四类：`DP`（`AvaloniaProperty.Register<...,string>(..., "默认值")`）、`Assign`（`Text = "..."` / `Content` / `Title` / `Placeholder` / `Label` / `Message` / `Close` 等）、`Auto`（`AutomationProperties.SetName/HelpText/ItemStatus` 的第二参字面量）、`Xaml`（主题 `.axaml` 的 `Text=`/`Content=`/`Header=` 字面量）。
- **未计入**：minified 单行 axaml（如 `ArcColorPicker.axaml` 整文件一行，跳过以避免误报）、代码注释、日志/异常文案（非用户可见）、`ArcUi.Button("Send")` 这类组合助手参数（无法用统一规则判定，见 §1.4 的"人工补漏"）。

### 1.2 按项目 × 类别（本次快照）

类别定义：`Pagination`（分页）、`Calendar`（日历/日期/时间）、`Dialog`（对话框）、`Toast`（Toast/通知）、`FileDrop/Empty`（文件拖放/空态）、`Other`（其余控件）。

| 项目 | Pagination | Calendar | Dialog | Toast | FileDrop/Empty | Other | 合计 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Arc.Avalonia.Pickers | — | 16 | — | — | 6 | 42 | 64 |
| Arc.Avalonia.Actions | — | — | — | 2 | — | 35 | 37 |
| Arc.Avalonia.Media | — | — | — | — | — | 34 | 34 |
| Arc.Avalonia.Inputs | — | — | — | — | — | 32 | 32 |
| Arc.Avalonia.AgentUI | — | — | — | — | — | 30 | 30 |
| Arc.Avalonia.DataDisplay | — | — | — | — | — | 28 | 28 |
| Arc.Avalonia.Feedback | — | — | — | — | 1 | 23 | 24 |
| Arc.Avalonia.Overlays | — | — | — | — | — | 23 | 23 |
| Arc.Avalonia.Content | — | — | — | — | — | 22 | 22 |
| Arc.Avalonia.Effects | — | — | — | — | — | 16 | 16 |
| Arc.Avalonia.Navigation | 2 | — | — | — | — | 7 | 9 |
| Arc.Avalonia.Agent | — | — | — | — | — | 6 | 6 |
| Arc.Avalonia.Charts | — | — | — | — | — | 6 | 6 |
| Arc.Avalonia.LifeToday（Agent 下） | — | — | — | — | — | 53 | 53 |
| **合计** | **2** | **16** | **0** | **2** | **7** | **357** | **≈384** |

> 分类计数偏低的两个原因：① `Pagination` 的 "Previous page" 等是作为方法参数传入（`Arrow("chevron-left", "Previous page", ...)`），不匹配 `Assign` 规则；② `Dialog` 的默认按钮文案在 `Services/IDialogService.cs` 的 record 初始化器里，不匹配 DP/Assign 规则。这两部分已由 §1.3 人工补漏（因此下表优先文件的实际字符串数高于自动分类）。

### 1.3 优先文件逐条清单（人工补漏，文件:行≈）

> 标注：`DP` = DP 默认值（公开 API 行为）；`Fmt` = 格式/插值串；`Auto` = 无障碍名；`Vis` = 可见文案；`Xaml` = 主题内硬编码。`安全外置` = 可直接迁入 resx 且不改变公开 API。

#### A. 分页 — `src/Components/Arc.Avalonia.Navigation/Controls/ArcPagination.cs`

| 行≈ | 字符串 | 类型 | DP 默认 | Fmt | 安全外置 | 备注 |
|---|---|---|---:|---:|---:|---|
| 31 | `"Pagination"` | DP | ✅ | — | ⚠️ 保留默认 | `Label` DP 默认值；渲染回退用本地化 |
| 71 | `"Pagination"` | Auto fallback | — | — | ✅ | `Label ?? "Pagination"` |
| 98 | `"Previous page"` | Auto | — | — | ✅ | `Arrow(...)` 参数 |
| 117 | `$"Page {number}, current page"` / `$"Page {number}"` | Auto+Fmt | — | ✅ | ✅ | 需 `Pagination_PageCurrent` / `Pagination_Page` 两条格式资源 |
| 120 | `"Current page"` | Auto | — | — | ✅ | help text |
| 140 | `"Next page"` | Auto | — | — | ✅ | `Arrow(...)` 参数 |

#### B. 日历/日期/时间 — `ArcDateMath.cs`、`ArcCalendar.cs`、`ArcDateRangePicker.cs`、主题 XAML

| 文件:行≈ | 字符串 | 类型 | DP 默认 | Fmt | 安全外置 | 备注 |
|---|---|---|---:|---:|---:|---|
| `Pickers/Controls/ArcDateMath.cs:12` | `CultureInfo.GetCultureInfo("en-US")` | 文化 | — | — | ✅（internal） | **根因**：日历月份/星期/日期格式全部走 en-US |
| `ArcDateMath.cs:34` | `"MMMM yyyy"` | 格式 | — | ✅ | ✅ | 月标题 |
| `ArcDateMath.cs:36` | `"MMM"` | 格式 | — | ✅ | ✅ | 月短名 |
| `ArcDateMath.cs:38` | `ShortestDayNames[...]` | 文化数据 | — | — | ✅ | 星期名 |
| `ArcDateMath.cs:48/52/54` | `"MMM d, yyyy"` / `"MMM d"` | 格式 | — | ✅ | ✅ | 日期短格式 |
| `ArcDateMath.cs:61/66/69` | `$"{...} – {...}"` | 格式 | — | ✅ | ✅ | 范围串；需 `Range_SameMonth/SameYear/Other` |
| `ArcCalendar.cs:185/191` | `"Previous month"` / `"Next month"` | Auto | — | — | ✅ | 上/下月按钮 |
| `ArcCalendar.cs:507` | `$"{MonthTitle(month)} calendar"` | Auto+Fmt | — | ✅ | ✅ | `Calendar_Label` |
| `Pickers/Themes/ArcCalendar.axaml:45` | `"Today"` | Xaml | — | — | ✅ | `PART_Today` 的 Content |
| `ArcDateRangePicker.cs:31–34` | `"Today"` / `"Last 7 days"` / `"Last 30 days"` / `"This month"` | Vis（预设） | — | — | ✅ | 4 条预设标签 |
| `Pickers/Themes/ArcDateRangePicker.axaml:25/47/66/72` | `"Select dates"` / `"Presets"` / `"Cancel"` / `"Apply"` | Xaml | — | — | ✅ | 主题硬编码 |
| `Pickers/Controls/ArcDatePicker.cs:39` | `"MMM d, yyyy"` | DP | ✅ | ✅ | ⚠️ 保留默认 | `DateFormat`；未显式设置时按文化短日期 |
| `Pickers/Themes/ArcDatePicker.axaml:21` | `"Select a date"` | Xaml | — | — | ✅ | 占位文案 |
| `Pickers/Themes/ArcTimePicker.axaml:57` | `"9:00 AM"` | Xaml | — | — | ✅ | 12 小时制示例；中文应为 24h/`09:00`（可先用资源替换） |
| `Pickers/Themes/ArcTimePicker.axaml` 等 | `AccessibleRole` "Date"/"Time"/"Date range" | Role | — | — | ✅ | `AccessibleRole` 也可本地化（见 §5） |

#### C. 对话框 — `ArcDialog.cs`、`Services/IDialogService.cs`

| 文件:行≈ | 字符串 | 类型 | DP 默认 | Fmt | 安全外置 | 备注 |
|---|---|---|---:|---:|---:|---|
| `Overlays/Controls/ArcDialog.cs:168` | `"Dialog"` | Auto fallback | — | — | ✅ | `Title ?? "Dialog"` |
| `Services/IDialogService.cs:22` | `"OK"` | record 默认 | —（非 DP） | — | ⚠️ 行为变更风险 | `ArcDialogRequest.PrimaryText` |
| `Services/IDialogService.cs:28` | `"Cancel"` | record 默认 | — | — | ⚠️ | `CancelText` |
| `Services/IDialogService.cs:56` | `"Confirm"` | record 默认 | — | — | ⚠️ | `ArcConfirmRequest.ConfirmText` |
| `Services/IDialogService.cs:59` | `"Cancel"` | record 默认 | — | — | ⚠️ | `CancelText` |

> 说明：ArcDialog 本身不画默认按钮（Footer 由调用者给），所以"对话框默认按钮"的 l10n 点在 **服务层的 record 默认值**，处理方案见 §3.3（保持默认 + 提供本地化工厂，或 1.0 breaking 改 `null`）。

#### D. Toast — `ArcToast.cs`、`ArcToastHost.cs`

| 文件:行≈ | 字符串 | 类型 | DP 默认 | Fmt | 安全外置 | 备注 |
|---|---|---|---:|---:|---:|---|
| `Actions/Controls/ArcToast.cs:130` | `"Dismiss notification"` | Auto | — | — | ✅ | 关闭按钮 |
| `Actions/Controls/ArcToast.cs:163` | `"Notification"` | Auto fallback | — | — | ✅ | `Title ?? "Notification"` |
| `Actions/Controls/ArcToastHost.cs:31` | `"Notifications"` | Auto | — | — | ✅ | 区域名 |

> Toast 的用户可见标题/描述由调用者给，组件默认文案只有 3 条无障碍名；Toast 的 l10n 工作量主要不在字符串，而在**通知服务的默认文案约定**（`INotificationService` 不内置英文标题，保持即可）。

#### E. 文件拖放 — `ArcFileDropzone.cs`（本次最小集中字符串最多）

| 文件:行≈ | 字符串 | 类型 | DP 默认 | Fmt | 安全外置 | 备注 |
|---|---|---|---:|---:|---:|---|
| `Pickers/Controls/ArcFileDropzone.cs:74` | `"Add files"` | DP | ✅ | — | ⚠️ 保留默认 | `Label` |
| `Pickers/Controls/ArcFileDropzone.cs:77` | `"Drop files here or choose from your device"` | DP | ✅ | — | ⚠️ 保留默认 | `Description` |
| `ArcFileDropzone.cs:216/251/269/311` | `"Add files"`（多处 fallback/Title） | Auto/Vis | — | — | ✅ | 统一键 `FileDropzone_AddFiles` |
| `ArcFileDropzone.cs:241` | `"KB"`/`"MB"`/`"GB"`/`"TB"` | 单位 | — | — | ❌ 不外置 | 通用单位，保留 |
| `ArcFileDropzone.cs:283` | `$"Up to {size} each"` | Fmt | — | ✅ | ✅ | `FileDropzone_UpToEach` |
| `ArcFileDropzone.cs:288` | `"1 file"` / `$"Up to {n} files"` | Fmt | — | ✅ | ✅ | 单复数两条键 |
| `ArcFileDropzone.cs:366` | `"Accepted files"` | Vis（文件类型名） | — | — | ✅ | 选择器过滤器名 |
| `ArcFileDropzone.cs:414` | `$"Larger than {size}"` | Fmt | — | ✅ | ✅ | 行内错误 |
| `ArcFileDropzone.cs:421` | `"One file is too large."` / `$"{n} files are too large."` | Fmt | — | ✅ | ✅ | 单复数 |
| `ArcFileDropzone.cs:425` | `"That file type is not accepted."` / `$"{n} files were not accepted."` | Fmt | — | ✅ | ✅ | 单复数 |
| `ArcFileDropzone.cs:429` | `"The list holds one file."` / `$"The list holds up to {n} files."` | Fmt | — | ✅ | ✅ | 单复数 |
| `ArcFileDropzone.cs:542/543` | `$"... Uploading {p}%"` / `"Upload failed"` | Fmt | — | ✅ | ✅ | 状态行 |
| `ArcFileDropzone.cs:587/625` | `$"Remove {name}"` / `$"{name}, {size}"` | Fmt/Auto | — | ✅ | ✅ | 删除按钮/行名 |
| `Pickers/Themes/ArcFileDropzone.axaml:69/73/83` | `"Add files"` / `"Drop files here or choose from your device"` / `"Browse files"` | Xaml | — | — | ✅ | 与 DP 默认重复，需去重（XAML 用 code-behind 填或统一资源） |

#### F. 空态 — `ArcEmptyState.cs`（最小集中唯一"零文案"控件）

| 文件:行≈ | 字符串 | 类型 | DP 默认 | Fmt | 安全外置 | 备注 |
|---|---|---|---:|---:|---:|---|
| `Feedback/Controls/ArcEmptyState.cs:28` | `"folder"` | 图标名 | ✅ | — | ❌ 不翻译 | `Icon` DP 默认值，属资产 |
| 其余 | `Title`/`Description` 默认 `string.Empty` | DP | ✅ | — | ✅ 无需外置 | 文案由调用者给；仅 `AutomationProperties.SetName(this, Title/Label)` 用动态值 |

> 结论：**EmptyState 的 l10n 工作量≈0**，只需在 `Title` 为空时补一个本地化兜底（可选），不要把它算进 6 人日的主体。

### 1.4 最小集（6 人日）具体文件清单与拆分

**范围**：`ArcStrings.resx` + `ArcStrings.zh-Hans.resx` + `ArcLocalization` + 下列 9 个源文件 / 4 个主题文件；共 **≈63 个唯一字符串**（不含 DP 默认值本身；DP 默认值保留，见 §3）。**不包含** 140 个 DP 默认值的迁移、不含 294 页文档文案、不含 Blocks/Special（不在 3 个根目录内）。

| 天 | 文件 | 内容 | 字符串数（约） | 完成标准 |
|---|---|---:|---:|---|
| D1 | `src/Foundation/Arc.Avalonia/Resources/ArcStrings.resx` + `.zh-Hans.resx`；`Localization/ArcStrings.cs`；`Localization/ArcLocalization.cs`；测试 | 资源骨架、内部访问器、`Override/Reset/SetLanguage/LanguageChanged`、en/zh 初始键、单测（override/fallback/格式/切换） | 键框架 | `ArcLocalization` 可切 zh，`Override` 生效，公开 API 基线只多 1 个类 |
| D2 | `Pickers/Controls/ArcDateMath.cs`；`ArcCalendar.cs`；`Themes/ArcCalendar.axaml`；`ArcDatePicker.cs`；`Themes/ArcDatePicker.axaml`；`ArcDateRangePicker.cs`；`Themes/ArcDateRangePicker.axaml`；`Themes/ArcTimePicker.axaml` | 文化切换（`ArcDateMath.Culture`）、月份/星期/日期格式、Today/Select dates/Presets/Cancel/Apply、预设 4 条、`DateFormat` 渲染回退 | ≈28 | zh 下日历显示"2026年11月"、星期一…、今天/上月/下月；en 截图不变 |
| D3 | `Overlays/Controls/ArcDialog.cs`；`Services/IDialogService.cs` | `Dialog` 兜底名；OK/Cancel/Confirm 的**本地化工厂**（不改 record 默认，见 §3.3） | ≈5 | zh 下 `ArcLocalization.DialogDefaults()` 返回"确定/取消" |
| D4 | `Actions/Controls/ArcToast.cs`；`ArcToastHost.cs`；`Pickers/Controls/ArcFileDropzone.cs`；`Themes/ArcFileDropzone.axaml` | Toast 3 条无障碍名；FileDropzone 全部可见文案 + 单复数 + XAML 3 处去重 | ≈22 | zh 下拖放区/上传错误/文件行文案全中文；en 不变 |
| D5 | `Feedback/Controls/ArcEmptyState.cs`（可选兜底）；`docs/` 使用说明；`--audit/--smoke/截图` 回归 | EmptyState 兜底、资源键命名文档、en 截图 diff 归零、zh 抽检 3 页 | ≈2 | 双主题 + 双语截图抽检；Verify 新增断言 |
| D6 | 缓冲/联调 | zh 译文审校、`ArcLocalization` 在多窗口/设计器/Culture 变化下的行为、`check-public-api`/`check-package` 同步 | — | 6/6 关闸绿；无 DP 默认值 diff |

**验收命令（沿用现有门禁，不新增脚本也可）**：`tools/build.ps1`、`dotnet .../Arc.Avalonia.Verify`、`--audit`（294/294）、`--smoke`（319/320 页）、`tools/shots.ps1`（en 基线不变）、`check-public-api`（只新增 `ArcLocalization` 与内部类型不出现）。

---

## 2. 机制建议：`resx + ResourceManager + ArcLocalization`（推荐） vs `DynamicResource` 字典

### 2.1 方案对比

| 维度 | A. `.resx` + `ResourceManager`（推荐） | B. `DynamicResource` 语言字典 |
|---|---|---|
| 覆盖现有文案 | ✅ 覆盖 **C# 构造文案**（119 赋值 + 81 无障碍调用 + 格式串） | ⚠️ 只覆盖 XAML `DynamicResource`；C# 里 `Text=`/`SetName(...)` 无法用 `DynamicResource`，需改成 XAML 或手工订阅 |
| 编译期 key 校验 | ✅ 强类型访问器 + 资源键生成/检查（可加 analyzer/CI 扫描） | ❌ 字符串 key，无编译期校验 |
| 翻译工具链/审校 | ✅ `.resx` 是标准工具链（VS/Rider/ResXManager；可导出 XLIFF） | ❌ 需自维护字典 |
| 运行时切换 | ✅ 显式 `LanguageChanged` + 控件重应用（可控、可测） | ✅ 换字典即刷新（但需要语言字典是 `DynamicResource` 的同一层） |
| Host 覆盖 | ✅ `ArcLocalization.Override(key, value)`（测试/品牌文案） | ⚠️ 需覆盖整个资源键，且与主题字典同层，作用域难控 |
| 与主题/Fluent 的关系 | ✅ 语言与主题解耦，不碰 `ArcTheme` 资源链 | ❌ 语言字典需并入 `Application.Resources`/`ArcTheme`，换语言可能与主题合并顺序冲突 |
| 现有 48 处 XAML 硬编码 | ✅ 支持（少量 theme 文案可改为 code-behind 填或统一资源） | ✅ 但需要为每处建 `x:Key`，且失去"同一 key 双语"的工具链 |
| 依赖 | 0 新 NuGet（`System.Resources.ResourceManager` 在 BCL） | 0 |
| 结论 | **推荐** | 仅作为"XAML 纯资源文案"的补充，不作为主机制 |

### 2.2 API 概形（建议）

```csharp
// src/Foundation/Arc.Avalonia/Localization/ArcLocalization.cs（公开，新基线 +1 类）
public static class ArcLocalization
{
    /// <summary>Current UI culture; defaults to CultureInfo.CurrentUICulture.</summary>
    public static CultureInfo Culture { get; }
    public static event EventHandler? LanguageChanged;

    /// <summary>Sets UI culture and raises LanguageChanged; no-op when identical.</summary>
    public static void SetLanguage(CultureInfo culture);

    /// <summary>Per-key override (host/test), checked before resx. Case-sensitive, ordinal.</summary>
    public static void Override(string key, string value);
    public static void ResetOverrides();

    /// <summary>Lookup for host-facing scenario code (e.g. dialog factories).</summary>
    public static string Get(string key);                 // falls back to neutral resx, then key
    public static string Format(string key, params object?[] args);
}

// src/Foundation/Arc.Avalonia/Localization/ArcStrings.cs（internal）
internal static class ArcStrings
{
    private static readonly ResourceManager Manager =
        new("Arc.Avalonia.Resources.ArcStrings", typeof(ArcStrings).Assembly);
    internal static string Get(string key);              // override -> zh/en resx -> key
    internal static string Format(string key, params object?[] args);
}
```

- 资源文件：`src/Foundation/Arc.Avalonia/Resources/ArcStrings.resx`（en 中性）、`ArcStrings.zh-Hans.resx`；SDK 自动嵌入，资源逻辑名 `Arc.Avalonia.Resources.ArcStrings`（RootNamespace=`Arc.Avalonia`、AssemblyName=`Arc.Avalonia.Foundation`，已核对 csproj）。
- 键命名：`<Area>_<Semantic>`，如 `Pagination_PreviousPage`、`Calendar_NextMonth`、`Dialog_Ok`、`Dialog_Cancel`、`Toast_Dismiss`、`FileDropzone_AddFiles`、`FileDropzone_TooLarge_Plural`；格式键以 `_Fmt` 结尾（`Pagination_PageCurrent_Fmt` = "Page {0}, current page"）。
- 覆盖顺序：`Override` → 当前文化 resx → 中性 resx → key 本身（**永不抛异常**，便于缺翻译时回退）。
- 格式：`Format` 用 `string.Format(Culture, ...)`；单复数用独立键（`_One`/`_Plural`），避免在 resx 里写复数逻辑。
- 内部类型不进入 PublicApi 基线；只新增 `ArcLocalization`（与可选的 `ILocalizer`，见 §2.4）。

### 2.3 切换语言后的刷新

三种可选，推荐 **①+③ 组合**：

1. **控件级订阅（主路径）**：控件在 `OnAttachedToVisualTree` 订阅 `ArcLocalization.LanguageChanged`，在 `OnDetachedFromVisualTree` 退订；事件里调用各控件的 `UpdateLocalization()`（重新计算其构造文案：`TextBlock.Text`、`AutomationProperties`、`Button.Content`）。
   - 理由：现有 200 处文案在 **C# 构造**（`Build()`/`OnApplyTemplate`/`UpdateCopy()`）里，控件已有"重新应用"路径（如 `ArcPagination.Build`、`ArcDialog.UpdateCopy`、`ArcToast.UpdateCopy`），加一次调用即可。
   - 防泄漏：静态事件 + 未退订会保活控件；M2 的 Verify 增加"attach/detach 后 WeakReference 可回收"断言（与 Agent 的租约测试同风格）。
2. **`INotifyPropertyChanged` 代理（可选，仅 XAML 绑定文案）**：若某些主题文案改为 `Text="{Binding ...}"`，用 `ArcLocalization.Current` 视图模型 + `INPC`；但不要为全部 200 处做代理。
3. **"重新赋值 DP"**：仅用于 `Label`/`Description` 这类 **DP 默认值保留**的控件——**不要** set 默认值本身（会破坏 API），而是控制内部渲染：`if (!IsSet(LabelProperty)) _title.Text = ArcStrings.Get(...); else _title.Text = Label;`（见 §3）。语言切换时重跑该逻辑。

### 2.4 不破坏现有公开 API 的边界

| 边界 | 做法 |
|---|---|
| 140 个字符串 DP 默认值 | **不改**（不改 `AvaloniaProperty.Register(..., "English")` 的默认值）；渲染期用 `IsSet` 回退；`GetValue`/公开 API 行为不变，`check-public-api` 与既有测试不受影响 |
| record 初始化默认值（Dialog OK/Cancel/Confirm） | 保留现有默认（见 §3.3）；新增 `ArcLocalization` 工厂/辅助（如 `ArcDialogDefaults.Localized()`）供新代码使用；若要改默认，只在 1.0 breaking 窗口进行 |
| 现有 XAML 主题 | 不改外部形状；仅把 4 处硬编码文案改为 code-behind 填值或 `ArcStrings` 绑定（主题资源键可保留英文默认） |
| `ArcThemeManager`/`ArcThemeOptions` | 不混入语言（主题服务只管外观）；`ILocalizer` 可选注册进 `ArcServiceProvider`，但不强制 |
| 依赖边界 | 0 新 NuGet；`.resx` 编进 `Arc.Avalonia.Foundation`（L1），符合 R3 白名单 |
| 可选 `ILocalizer` 抽象 | 仅为宿主自定义字串源（如服务端下发）：`public interface ILocalizer { string Get(string key); }`，默认实现走 `ArcStrings`；不改变现有 API |

---

## 3. DP 默认值风险清单与兼容做法

### 3.1 为什么不能直接把 DP 默认值改成资源查找

1. **默认值是公开 API 行为**：`GetValue` 返回它、XAML 预览器/设计器依赖它、测试断言它、文档写它；`check-public-api` 对"默认值"不敏感，但行为变化会被下游消费者感知。
2. **静态初始化时机不可控**：`AvaloniaProperty.Register` 在类型静态构造时执行；此时 `CurrentUICulture` 可能还是进程默认（设计器/测试/多窗口语言不同），把 `ArcStrings.Get(...)` 放进默认值会导致 **首个访问者的语言被固化**，后续 `SetLanguage` 无法纠正。
3. **多窗口/多语言**（M3 可能支持）：默认值是全局单例，无法 per-window 本地化。
4. **XAML/Style 回退语义**：`BasedOn`/`Style Setter` 未命中时取默认值，若默认值随语言变化，样式优先级行为会变得不可预测。

### 3.2 兼容做法："保留 DP 默认值 + 渲染期按 `IsSet` 回退"

```csharp
// 以 ArcPagination.Label 为例（不改默认值 "Pagination"）
private void UpdateLocalizedText()
{
    var label = IsSet(LabelProperty) && !string.IsNullOrWhiteSpace(Label)
        ? Label!
        : ArcStrings.Get("Pagination_Label");          // zh: "分页"
    AutomationProperties.SetName(this, label);

    // 按钮/页号等其它文案同理：内部 TextBlock/Button 内容用 ArcStrings 重填
}

protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnAttachedToVisualTree(e);
    ArcLocalization.LanguageChanged += OnLanguageChanged; // 对称退订
    UpdateLocalizedText();
}

protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
    ArcLocalization.LanguageChanged -= OnLanguageChanged;
    base.OnDetachedFromVisualTree(e);
}
private void OnLanguageChanged(object? s, EventArgs e) => UpdateLocalizedText();
```

要点：
- `IsSet(prop)` 判定"显式设置"（local/style/binding 都算），未设置 → 当前语言串；显式设置 → 用户值（含英文/自定义）。
- `Label` 的**默认值仍是 "Pagination"**（API 兼容）；未显式设置时界面显示本地化"分页"——**`GetValue` 与界面显示可以不同**，这是有意的兼容缝。
- 不用新增公开 DP；若需给宿主读取"实际显示文案"，用 internal 计算属性，或 provider 侧返回（不进公开 API）。
- 对 `DateFormat` 这类 **格式串 DP**：保留默认 `"MMM d, yyyy"`；未显式设置时用 `ArcLocalization.Culture.DateTimeFormat.ShortDatePattern`（或 `d`），显式设置时按用户格式。

### 3.3 需要人工决策的 DP/record 默认值（高风险清单）

| # | 位置（文件:行≈） | 默认值 | 类型 | 风险 | 建议 |
|---|---|---|---|---|---|
| 1 | `Navigation/Controls/ArcPagination.cs:31` | `Pagination` | DP `Label` | 中（无障碍名/可见标签） | 保留默认；渲染回退 |
| 2 | `Pickers/Controls/ArcFileDropzone.cs:74/77` | `Add files` / `Drop files here or choose from your device` | DP `Label`/`Description` | 中（可见主文案） | 保留默认；渲染回退 |
| 3 | `Pickers/Controls/ArcDatePicker.cs:39` | `MMM d, yyyy` | DP `DateFormat` | 中（日期显示） | 保留默认；未设置时文化短格式 |
| 4 | `Inputs/Controls/ArcCombobox.cs:53/56` | `Search or select…` / `No matches found` | DP | 中 | 保留；内部 TextBlock 回退 |
| 5 | `Inputs/Controls/ArcSelect.cs:53` | `Select an option` | DP | 中 | 保留；回退 |
| 6 | `Inputs/Controls/ArcMultiSelect.cs:31` | `Select options` | DP | 中 | 保留；回退 |
| 7 | `Inputs/Controls/ArcTagInput.cs:29` | `Add a tag` | DP | 中 | 保留；回退 |
| 8 | `Inputs/Controls/ArcMorphSelect.cs:97` | `Select` | DP | 低 | 保留；回退 |
| 9 | `Content/Controls/ArcRichTextEditor.cs:49/52/61` | `Start writing` / `Type / for blocks` / `Editor` | DP | 中 | 保留；回退 |
| 10 | `DataDisplay/Controls/ArcDataGrid.cs:144/162` | `Data grid` / `No rows yet` | DP | 中 | 保留；回退 |
| 11 | `DataDisplay/Controls/ArcTreeTable.cs:140/171` | `Tree table` / `Nothing to show` | DP | 中 | 保留；回退 |
| 12 | `DataDisplay/Controls/ArcSortableDataTable.cs:78/81` | `Data table` / `No rows to show` | DP | 中 | 保留；回退 |
| 13 | `Actions/Controls/ArcActionButton.cs:40/43/46` | `Save` / `Saving` / `Saved` | DP | 中 | 保留；回退（状态文案） |
| 14 | `Actions/Controls/ArcCopyButton.cs:31/40/43` | `Copy` / `Copied` / `Could not copy` | DP | 中 | 保留；回退 |
| 15 | `Actions/Controls/ArcExpandableText.cs:38/41` | `Read more` / `Show less` | DP | 低 | 保留；回退 |
| 16 | `Effects/Controls/ArcBottomSheet.cs:58` | `Close` | DP | 低 | 保留；回退 |
| 17 | `Feedback/Controls/ArcSkeleton.cs:31` | `Loading content` | DP | 低 | 保留；回退 |
| 18 | `Services/IDialogService.cs:22/28/56/59` | `OK` / `Cancel` / `Confirm` / `Cancel` | record 初始化器 | **高**（服务默认按钮直接可见） | 保留默认 + 新增 `ArcDialogDefaults.Localized()`/本地化工厂；若必须改默认，走 1.0 breaking 记录 |
| 19 | `Pickers/Controls/ArcDateRangePicker.cs:31–34` | `Today` / `Last 7 days` / `Last 30 days` / `This month` | 静态预设（非 DP） | 中（可见） | 改为 `LocalizedPresets()`（运行时按 `ArcLocalization` 建列表） |
| 20 | `Pickers/Themes/ArcCalendar.axaml:45`、`ArcFileDropzone.axaml:69/73/83` 等 4–12 处 | `Today` / `Add files` / `Browse files` … | XAML | 中 | 迁到 code-behind/`ArcStrings`；保留 XAML 英文兜底 |

> 其余 ≈120 个 DP 默认值（LifeToday 21、Actions 其余 15、Feedback 15、Media 15、DataDisplay 其余 10、Pickers 其余 6、Content 其余 6、Charts 6、AgentUI 4、Overlays 4、Effects 3、Navigation 2）同属"保留默认 + 渲染回退"，**不在 6 人日范围**，M3 全量 9 人日按清单批量处理。

**新增公开 API 的边界**：6 人日只新增 `ArcLocalization`（+可选的 `ILocalizer`），**不**给 140 个 DP 加"本地化版本"属性——否则公开面爆炸。若确实需要宿主读"当前实际文案"，用 `internal` 方法或 `ILocalizer.Get(key)` 在宿主侧计算。

---

## 4. RTL / FlowDirection（M3 9 人日）

### 4.1 现状（本次快照）

| 项目 | 敏感点 | 说明 |
|---|---:|---|
| Arc.Avalonia.Inputs | 63 | 输入族最重：非对称 Margin + 前后缀图标方向 |
| Arc.Avalonia.Pickers | 31 | 日历/日期/时间/颜色：`chevron-left/right`、月份布局 |
| Arc.Avalonia.Effects | 19 | 横幅/底片/滚动 marquee 方向 |
| Arc.Avalonia.Actions | 14 | 菜单/Toast 图标方向、按钮图标位 |
| Arc.Avalonia.Feedback | 10 | 头像组/评论/计数器的左右偏移 |
| Arc.Avalonia.Media | 9 | 轮播/卡片栈/滑动操作方向 |
| Arc.Avalonia.Overlays | 9 | Drawer 方向、Share 面板 |
| Arc.Avalonia.Navigation | 8 | 面包屑分隔/分页箭头 |
| Arc.Avalonia.DataDisplay | 6 | 树/表格展开箭头 |
| Foundation | 5 | 基础图标/布局 |
| Agent（两项目 + LifeToday） | 3 | Agent 输入/工具卡方向 |
| Charts | 1 | 图表坐标轴/标注方向 |
| **合计** | **178** | 其中：非对称水平 Margin **86**、`HorizontalAlignment="Left"` 或 `"Right"` **62**、方向性图标 **21**、`FormattedText` 固定 LTR **9** |

补充事实：
- `RightToLeft` 出现 **0** 次；`FlowDirection` 全仓 17 次，其中 9 次是 `FlowDirection.LeftToRight` 硬编码（见下）。
- 固定 LTR 的 9 处：`Charts/Core/ChartCore.cs:146`、`Feedback/Controls/ArcActivityHeatmap.cs:567/613`、`ArcAnimatedCounter.cs:215`、`Inputs/Controls/ArcAdvancedInternals.cs:116`、`ArcTextarea.cs:153`、`Media/Controls/ArcMediaUi.cs:165`、`Pickers/Controls/ArcCalendarSurface.cs:328`、`Pickers/Controls/ArcSignaturePad.cs:206`。
- 固定 en-US 的另外 6 处：`Pickers/Controls/ArcSignaturePad.cs:206`、`Feedback/Controls/ArcAnimatedCounter.cs`（Locale DP 默认 en-US）等；`CultureInfo.InvariantCulture` 69 处（数字/图表/日期格式化），`CurrentCulture` 仅 5 处——**数字/日期在 zh 下不会自动按文化格式化**。

### 4.2 典型问题样本（file:line）

| 类型 | 样本 | 风险 |
|---|---|---|
| `FormattedText` 固定 LTR | `Pickers/Controls/ArcCalendarSurface.cs:328`、`Inputs/Controls/ArcTextarea.cs:153`、`Media/Controls/ArcMediaUi.cs:165` | 文字方向/省略号/对齐在 RTL 下错误 |
| 固定 en-US 文化格式化 | `Pickers/Controls/ArcDateMath.cs:12`（根因）、`SignaturePad.cs:206` | zh 日期/月份仍英文 |
| `HorizontalAlignment="Left/Right"` | `Actions/Themes/ArcActions.Buttons.axaml:89`（Left）、`ArcActions.Messages.axaml:59`（Right）、`ArcUserMenu.axaml:20/32` | RTL 下应镜像为 Start/End |
| 非对称水平 Margin | `ArcActionButton.axaml:56`（`8,0,0,0`）、`ArcActions.Messages.axaml:30`（`12,0,0,0`）、`ArcUserMenu.axaml:70`（`10,0,0,0`） | 图标与文字间距在 RTL 下应在另一侧 |
| 方向性图标 | `ArcActionButton.axaml:60`（`arrow-right`）、`ArcIconMorph.cs:165`、`ArcFilterToolbar.cs:443`、`ArcTreeTable.cs:1703`、`ArcJsonViewer.cs:540`、`ArcTreeView.cs:193` | 展开箭头/前进箭头应镜像 |
| 代码构造中的左右偏移 | `Margin = new Thickness(12, 0, 0, 0)` 等（本次扫描的非对称 Margin 大多来自 C# 构造，不只在 XAML） | 需统一 helper |

### 4.3 M3 9 人日的扫描方法（可脚本化）

**S1 扫描脚本（1 人日）** — 不新增依赖，用 PowerShell 正则落一份 RTL 清单（输出 CSV 到 `.recon/rtl/`，不入库）：

```powershell
$roots = 'src\Components','src\Foundation','src\Agent'
$files = Get-ChildItem $roots -Recurse -Include *.cs,*.axaml -File | ? { $_.FullName -notmatch '\\(obj|bin)\\' }
$rules = @(
  @{ Id='LTR-text';    Pat='FlowDirection\.LeftToRight' },
  @{ Id='HAlign';      Pat='HorizontalAlignment="(Left|Right)"' },
  @{ Id='DirIcon';     Pat='"(chevron|arrow|caret)-(left|right)"' },
  @{ Id='AsymMargin';  Pat='Margin="([0-9]+),([0-9]+),([0-9]+),([0-9]+)"' },   # 仅保留 $1 -ne $3
  @{ Id='AsymPadding'; Pat='Padding="([0-9]+),([0-9]+),([0-9]+),([0-9]+)"' },
  @{ Id='TranslateX';  Pat='translateX|TranslateTransform\((-?[0-9.]+)\s*,' },
  @{ Id='Dock';        Pat='Dock="(Left|Right)"' },
  @{ Id='Culture';     Pat='CultureInfo\.InvariantCulture|GetCultureInfo\("en-US"\)' }
)
# 逐规则统计/输出 file:line，人工复核后生成 fix list
```

**S2 布局 helper（2 人日）**：在 Foundation 增加 `ArcLayout`（internal/public 视 API 决策）：
```csharp
internal static class ArcLayout
{
    // 按控件当前 FlowDirection 镜像水平厚度
    public static Thickness Mirror(Thickness t, FlowDirection dir) =>
        dir == FlowDirection.RightToLeft ? new Thickness(t.Right, t.Top, t.Left, t.Bottom) : t;
    public static HorizontalAlignment Mirror(HorizontalAlignment a, FlowDirection dir) => ...;
    public static string MirrorIcon(string name, FlowDirection dir); // chevron-left <-> chevron-right
}
```
替换规则：`HorizontalAlignment="Left"` → `Start`（Avalonia 无 Start/End 枚举时用代码 helper 或附加属性 `ArcAlignment.Start`）；非对称 Margin → `ArcLayout.Mirror(...)`；方向性图标 → `MirrorIcon`。**不**假设 Avalonia 的 `FlowDirection` 会自动镜像 StackPanel/Grid 的 Margin（需在 RTL smoke 页逐控件验证）。

**S3 分区修复（4 人日）**：Pickers+Inputs（94 处，2 人日）→ Actions/Effects/Overlays（42 处，1 人日）→ 其余（42 处，1 人日）。
**S4 FormattedText/文化（1 人日）**：9 处 LTR → 传 `this.FlowDirection`（或 `GetFlowDirection`）；日期/数字改用 `ArcLocalization.Culture`；`ArcDateMath.Culture` 已在 6 人日改。
**S5 验收（1 人日）**：新增 Gallery `RtlSmokePage`（15–20 个代表控件，`FlowDirection=RightToLeft` 根），mirrored 截图（`shots/rtl/`），断言：无重叠/无截断/图标方向正确/焦点环可见/文本方向正确；再跑 en 回归（截图与基线一致）。

**M3 RTL 完成标准**：扫描清单 178 → 0（除白名单：通用单位/不镜像的纵向 Margin）；RTL smoke 页通过；`FlowDirection` 在 15 个关键控件上继承并影响文本与布局；不引入新依赖。

---

## 5. 键盘 / 焦点 / 无障碍（M3 15 人日）

### 5.1 现状统计（本次快照）

| 指标 | 数量 | 说明 |
|---|---:|---|
| `OnKeyDown` | 258 行 | 键盘处理广泛存在，但分布不均 |
| `IsTabStop` | 66 | 关键控件大多可聚焦；但 `TabIndex` **0**（顺序依赖树序） |
| `:focus-visible` 样式 | 50 | 主要在 Button/Select/Input 族；Pagination/Calendar/Dialog/Toast/EmptyState/DataGrid 部分缺失 |
| `FocusAdorner` | 19 | 少数控件显式关闭/自定义 |
| `AutomationProperties.*` | 752 | SetName/HelpText/ItemStatus 广泛使用 |
| `AutomationPeer` | **0** | 无自定义 peer；依赖基类（Button/ListBox 有默认 peer，自绘 TemplatedControl 无） |
| `AutomationId` | **0** | 无自动化测试坐标 |
| `AccessibleRole` | 6 | 仅 Pickers（`Picker`/`Date`/`Time`/`Date range`/`Color`） |
| `[TemplatePart]` / `RoutedEvent` | 367 / 31 | M2 第三波已提供的契约骨架，a11y peer 可复用 |
| 测试基线 | `Verify` 140 断言 | 无读屏/自动化树测试 |

### 5.2 15 个关键控件 → peer/键盘清单（M3 用）

> `peer` 列：`继承` = 基类已有 AutomationPeer（Avalonia 自带），`缺` = 自绘 `TemplatedControl`，需自定义 peer；`键盘` 列为本文件 `OnKeyDown` 命中的键（`Left/Right/Up/Down/Home/End/Tab/Escape/Enter/Space`），`基类` 表示由基类处理。

| # | 控件 | 基类 → peer 判定 | 键盘（OnKeyDown 实测） | 焦点环 | SetName | 缺口（M3 要补） |
|---:|---|---|---|---|---:|---|
| 1 | `ArcButton` | Button → **继承** `ButtonAutomationPeer` | 基类（Space/Enter） | ✅ 7 处 `:focus-visible` | 0（Content 自动名） | `AutomationId` 约定；Loading/Disabled 状态值 |
| 2 | `ArcActionButton` | Button → 继承 | 基类 | ✅ 1 | 1 | 状态机（Saving/Saved）用 `LiveSetting`/值模式 |
| 3 | `ArcInput` | `ArcTextFieldBase : ArcFieldBase : TemplatedControl` → **缺** | 基类 TextBox 内部处理 | FocusAdorner 4（内层） | 0 | 自定义 peer（Edit/Value），宿主名/描述；CapsLock 提示（XAML 已有） |
| 4 | `ArcCombobox` | `ArcFieldBase : TemplatedControl` → **缺** | Up/Down/Escape/Enter | ✅ 2 | 2 | peer（ComboBox/ExpandCollapse），Home/End/typeahead；展开状态值 |
| 5 | `ArcSelect` | `ArcFieldBase` → **缺** | Up/Down/Escape/Enter/Space | ✅ 8 | 1 | peer（ComboBox），Home/End；选中项 Value |
| 6 | `ArcCheckbox` | TemplatedControl → **缺** | Space | ✅ 2 | 1 | peer（Toggle 模式），三态 Indeterminate 值；role |
| 7 | `ArcRadioGroup` | TemplatedControl → **缺** | 四向/Home/End | ✅ 2 | 2 | peer（Selection/SelectionItem），roving tabindex，组名 |
| 8 | `ArcSwitch` | TemplatedControl → **缺** | Enter/Space | ✅ 2 | 1 | peer（Toggle 模式），On/Off 值 |
| 9 | `ArcSlider` | TemplatedControl → **缺** | 四向/Home/End | ✅ 2 | 1 | peer（RangeValue/Value），PageUp/PageDown（需新增），数值朗读 |
| 10 | `ArcTabs` | TemplatedControl → **缺** | Left/Right/Home/End | **❌ 0** | 1 | peer（Tab/Selection），`:focus-visible` 主题、Tab 键进入/离开策略 |
| 11 | `ArcDialog` | ContentControl → **缺** | Tab（陷阱），Escape 由 `ArcDismiss` | **❌ 0** | 1 | peer（Dialog/Window 角色），默认按钮/Escape 提示；焦点恢复已实现（好） |
| 12 | `ArcPagination` | TemplatedControl → **缺** | 无（内层 ArcButton 处理） | **❌ 0** | 3 | peer（Navigation），每页按钮 role/current 值；页码 `Page`/`PageCount` 值模式 |
| 13 | `ArcDataGrid` | TemplatedControl → **缺** | 四向/Home/End/Tab/Escape/Enter/Space（覆盖全） | ✅ 1 | 10 | peer（Table/Grid/Row/Cell），行/列/排序值模式；键盘与模板契约（已有 TemplatePart 6） |
| 14 | `ArcCalendar` | TemplatedControl → **缺** | 四向/Home/End/Escape/Enter/Space（无 Tab） | **❌ 0** | 3 | peer（Calendar/Grid），日期单元格 peer（当前自绘无子控件），焦点日期朗读；`Tab` 进入/离开 |
| 15 | `ArcVirtualList` | ListBox → **继承** `ListBoxAutomationPeer` | 基类（四向/Home/End 等） | 主题 **❌ 0** | 0 | 项模板的 role/名（`ListBoxItem` 自带 peer，但 Arc item 常是自绘 Border → 需 review）；`StickToBottom` 状态朗读 |

**注**：`ArcVirtualList` 已是 `ListBox`，其容器 `ListBoxItem` 由 Avalonia 提供 peer；但 Arc 的 item 模板常把内容画成单个 Border（无子控件），会丢 item 语义——M3 需逐模板检查。

### 5.3 15 人日拆分（M3）

| 天 | 工作 | 产出 |
|---|---|---|
| D1 | `AutomationId` 约定 + 测试 helper（`AutomationProperties.SetAutomationId` 统一入口）+ a11y 测试页骨架 | 1 个约定文档 + Verify 断言 |
| D2–D4 | 重控件 3 个：`ArcDataGrid`（peer + 行/列/排序值）、`ArcCalendar`（单元格 peer）、`ArcDialog`（Dialog role + 默认按钮） | 3 peers + keyboard 修复 |
| D5–D8 | 输入族 5 个：`ArcInput`/`ArcCombobox`/`ArcSelect`/`ArcCheckbox`/`ArcRadioGroup`（peer + 值模式 + roving） | 5 peers |
| D9–D11 | 其余 5 个：`ArcSwitch`/`ArcSlider`/`ArcTabs`/`ArcPagination`/`ArcActionButton` + `ArcVirtualList` 项语义 review | 5–6 peers/修复 |
| D12–D13 | 键盘契约补齐：`ArcSlider` PageUp/Down、`ArcTabs`/`ArcCalendar` Tab 策略、`ArcDialog` Escape 提示、焦点环缺失主题（Tabs/Dialog/Pagination/Calendar） | 键盘清单全绿 |
| D14 | 读屏/自动化树测试（headless `AutomationPeer` 树断言 + 手动读屏抽检 15 控件） | Verify/headless 断言 |
| D15 | 文档 + `check-*` 门禁（AutomationId 扫描、peer 覆盖表） + 回归 | M3 验收 |

**M3 a11y 完成标准**：15/15 控件有明确 peer（继承或自定义）与值模式；`AutomationId` 全量可寻址；15 控件键盘 Tab 顺序可预期；缺失的 `:focus-visible` 主题补齐；读屏抽检通过；不新增依赖。

---

## 6. 证据索引与未验证项

### 6.1 关键证据（本次快照）

| 证据 | 位置 |
|---|---|
| v3.2 的 C5（15 = M2 6 + M3 9）、C4 15、G3d 30 | `docs/13-framework-layering-and-agent-top.zh.md` §D6 表（C4/C5 行）、§4.2 WBS（G3d 行） |
| M2 服务层与列表（24 + 20） | `docs/16-m2-wave1-services-and-lists.zh.md` |
| M2 Agent/图标/Theme API（模板与 RoutedEvent 同批推进） | `docs/17-m2-wave2-agent-and-theme-api.zh.md` |
| 无 `.resx` / 无 Localizer | 全仓 `*.resx`/`*.xlf` 0 个；`Localizer` / `ArcStrings` / `ResourceManager` 0 命中（本轮 grep） |
| `ArcDateMath` 固定 en-US | `src/Components/Arc.Avalonia.Pickers/Controls/ArcDateMath.cs:12` |
| DP 默认值 ≈140 | 全仓 `AvaloniaProperty.Register<...,string>` 扫描（LifeToday 21、Actions 19、Inputs 17、Feedback 16、Media 15、DataDisplay 13、Pickers 10、Content 9、Charts 6、AgentUI 4、Overlays 4、Effects 3、Navigation 3） |
| Dialog 默认按钮 | `src/Components/Arc.Avalonia.Services/IDialogService.cs:22/28/56/59` |
| Pagination 文案 | `ArcPagination.cs:31/71/98/117/120/140` |
| Calendar 文案/文化 | `ArcCalendar.cs:185/191/507`；`ArcCalendar.axaml:45`；`ArcDateRangePicker.cs:31–34`；`ArcDateRangePicker.axaml:25/47/66/72`；`ArcDatePicker.axaml:21` |
| FileDropzone 文案 | `ArcFileDropzone.cs:74/77/216/251/269/283/288/311/366/414/421/425/429/542/543/587/625`；`ArcFileDropzone.axaml:69/73/83` |
| Toast 文案 | `ArcToast.cs:130/163`；`ArcToastHost.cs:31` |
| 空态无硬编码 | `ArcEmptyState.cs`（Title/Description 默认 `string.Empty`，Icon 默认 `"folder"`） |
| RTL 规则/计数 | `HorizontalAlignment="Left"` 或 `"Right"` 62、方向图标 21、非对称水平 Margin 86、`FlowDirection.LeftToRight` 9（脚本统计） |
| 键盘/a11y 计数 | `OnKeyDown` 258、`IsTabStop` 66、`:focus-visible` 50、`FocusAdorner` 19、`AutomationProperties` 752、`AutomationPeer` 0、`AutomationId` 0、`TabIndex` 0、`AccessibleRole` 6、`TemplatePart` 367、`RoutedEvent` 31 |
| 15 控件基类 | Button→继承 peer；`ArcTextFieldBase/ArcFieldBase : TemplatedControl`→缺；ListBox→继承；其余 TemplatedControl→缺 |

### 6.2 未验证 / 存疑

1. 字符串总数随 M2 第三波并行提交在 384–388 间波动；minified 单行 axaml 被跳过，真实总数可能高 20–40。
2. `ArcSelect/ArcCombobox` 的键盘行为可能部分由内部 `TextBox`/`ListBox` 处理，`OnKeyDown` 计数低不代表键盘缺失，但**焦点/展开状态**需实测（M3 用 headless 断言补）。
3. Avalonia 的 `FlowDirection` 对 StackPanel/Grid/Margin 的自动镜像范围未在本机验证；RTL 修复必须逐控件在 smoke 页确认，不可假设。
4. `ArcDialog` 的默认按钮由 `IDialogService` record 提供（而非控件 DP）；若要"服务默认按钮也本地化"，涉及公开行为决策（§3.3 #18）。
5. `IDialogService`/`INotificationService` 是否已在其它宿主/测试里依赖默认英文值：`Verify` 只断言结果值、未断言按钮文案（grep），但外部消费者可能在 0.9-alpha 已使用——按"保留默认 + 工厂"最安全。
6. `Thread.CurrentThread.CurrentUICulture` 在 Avalonia 设计器/预览器中的行为未在本机验证（实现时需单测）。

---

## 附录 A：建议的 resx 键（最小集，≈63 条中的主键）

| 域 | 键（示例） | 英文 | 中文（建议） |
|---|---|---|---|
| Pagination | `Pagination_Label` / `Pagination_PreviousPage` / `Pagination_NextPage` / `Pagination_Page_Fmt` / `Pagination_PageCurrent_Fmt` / `Pagination_CurrentPage` | Pagination / Previous page / Next page / Page {0} / Page {0}, current page / Current page | 分页 / 上一页 / 下一页 / 第 {0} 页 / 第 {0} 页，当前页 / 当前页 |
| Calendar | `Calendar_PreviousMonth` / `Calendar_NextMonth` / `Calendar_Today` / `Calendar_Label_Fmt` / `Calendar_SelectDate` / `Calendar_SelectDates` / `Calendar_Presets` / `Calendar_Apply` / `Calendar_Cancel` | Previous month / Next month / Today / {0} calendar / Select a date / Select dates / Presets / Apply / Cancel | 上个月 / 下个月 / 今天 / {0} 日历 / 选择日期 / 选择日期范围 / 预设 / 应用 / 取消 |
| DateRange | `DateRange_Today` / `DateRange_Last7Days` / `DateRange_Last30Days` / `DateRange_ThisMonth` | Today / Last 7 days / Last 30 days / This month | 今天 / 最近 7 天 / 最近 30 天 / 本月 |
| Dialog | `Dialog_Ok` / `Dialog_Cancel` / `Dialog_Confirm` / `Dialog_Label` | OK / Cancel / Confirm / Dialog | 确定 / 取消 / 确认 / 对话框 |
| Toast | `Toast_Dismiss` / `Toast_Label` / `Toast_Region` | Dismiss notification / Notification / Notifications | 关闭通知 / 通知 / 通知 |
| FileDropzone | `FileDropzone_AddFiles` / `FileDropzone_Description` / `FileDropzone_Browse` / `FileDropzone_UpToEach_Fmt` / `FileDropzone_UpToFiles_Fmt` / `FileDropzone_TooLarge` / `FileDropzone_TooLargePlural_Fmt` / `FileDropzone_TypeRejected` / `FileDropzone_TypeRejectedPlural_Fmt` / `FileDropzone_OneFileLimit` / `FileDropzone_LimitPlural_Fmt` / `FileDropzone_Uploading_Fmt` / `FileDropzone_UploadFailed` / `FileDropzone_Remove_Fmt` / `FileDropzone_ItemLabel_Fmt` | Add files / Drop files here or choose from your device / Browse files / Up to {0} each / Up to {0} files / One file is too large. / {0} files are too large. / That file type is not accepted. / {0} files were not accepted. / The list holds one file. / The list holds up to {0} files. / Uploading {0}% / Upload failed / Remove {0} / {0}, {1} | 添加文件 / 拖放文件到此处或从设备选择 / 浏览文件 / 每个最大 {0} / 最多 {0} 个文件 / 有一个文件过大。 / {0} 个文件过大。 / 不支持该文件类型。 / {0} 个文件未被接受。 / 只能保留一个文件。 / 最多保留 {0} 个文件。 / 上传中 {0}% / 上传失败 / 移除 {0} / {0}，{1} |
| EmptyState | `EmptyState_Label`（可选兜底） | Empty | 空 |

> 键命名统一 `<Area>_<Semantic>`；`_Fmt` 结尾的键用 `ArcLocalization.Format`，参数顺序与英文一致；中文翻译需人工审校（禁止机器直译进入发布版本）。

---

## 附录 B：M3 全量（9 人日本地化）与 15 人日 a11y 的边界

- **M3 全量 9 人日（本地化）**：P0 最小集（6）+ 其余 ≈320 条（140 DP 默认值迁移到"渲染回退"、约 119 赋值/81 无障碍名/48 XAML 中的其余）+ 单复数/日期/数字文化统一 + 294 页文档的示例文案（文档不译，仅确保不出现英文 UI 默认值）+ zh 截图基线。
- **15 人日（无障碍）**：§5.3 的 15 控件 peer/值模式/AutomationId/焦点环/读屏测试；不含 151 控件全覆盖（那是 M4 以后）。
- **与 RTL 的关系**：RTL 修复（9 人日）与本地化共享 `ArcLocalization.Culture` 与 `ArcLayout.Mirror`，建议同一批次实现，避免两次改控件。
