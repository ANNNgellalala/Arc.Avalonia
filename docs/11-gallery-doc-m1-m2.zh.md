# Gallery 文档化改造：M1 平台骨架 + M2 样板记录

> 对应规格：[`10-gallery-doc-platform.zh.md`](10-gallery-doc-platform.zh.md)。
> 状态：M1 完成；M2 的 12 页样板已产出，等待确认后进入 M3（全量 294 页）。

## 1. M1：平台骨架

### 1.1 数据层（`Arc.Avalonia.Demo`）

| 文件 | 作用 |
| --- | --- |
| `Docs/ArcDoc.cs` | 文档模型：摘要、用途、何时用/不用、**变体声明**、关系、对照、示例、API 声明；容错 JSON 解析 |
| `Docs/ArcDocStore.cs` | 从各 `Arc.Avalonia*.dll` 的嵌入资源 `*.Docs.<slug>.json` 自动发现文档；重复 slug、文件名不一致、解析失败都记录为审计错误 |
| `Docs/ArcDocValidator.cs` | 数据校验：必填区块、CJK 检查、关系悬空/自指/重复、对照 target 与关系一致性、示例完整性、API 类型可解析、`ImplementedStates` 与 `variants` 双向比对 |
| `ArcItemPage.cs` | 条目页契约：`ItemSlug`、`ImplementedStates`、`BuildPreview`；旧的五个英文文档字段保留为迁移期兼容成员，新布局不再读取 |

文档随组件项目分发：项目 csproj 加 `<EmbeddedResource Include="Docs\*.json" />`，新增组件不需要改中央注册表。

### 1.2 展示层（`Arc.Avalonia.Gallery`）

| 文件 | 作用 |
| --- | --- |
| `Framework/DocLayout.cs` | 中文文档布局：摘要 → 用途 → 何时使用/不用 → 预览 → 关系（含自动反向关系）→ 相似组件对照 → 用法示例 → API；缺章节时显示审计可见的缺口提示 |
| `Framework/DocApiTable.cs` | 从控件类型反射生成属性/事件表（名称、类型、默认值、中文说明）；默认值经运行时元数据读取，0 手写属性行 |
| `Framework/GalleryWindow.cs` | 三栏文档站外壳：侧栏（搜索、tier、分类树、指南、迁移中展示页）、正文、右侧 TOC；分类懒展开、选中增量更新、页面视图 LRU(16)、页脚上一页/下一页 |
| `Framework/DocSearch.cs` | 中文子串检索，覆盖标题、中文名、摘要、正文、关系说明；按标题 > 中文名 > 摘要 > 正文排序 |
| `Framework/DocNavigation.cs` | 页面内导航钩子（首页/状态页的关系卡片、快捷键入口） |
| `Pages/HomePage.cs` | 文档首页：定位、阅读方式、统计（294 条目 / 已写文档 / 条目页 / 阻断项）、快速入口 |
| `Pages/DocStatusPage.cs` | 覆盖率页：294 行状态与问题摘要，点击直达条目 |
| `Framework/CatalogAudit.cs` | `--audit`：文档校验 + 预览构建 + 加载错误；阻断项非 0 时退出码非 0 |
| `Framework/NavigationSmoke.cs` | `--smoke`：全部 294 条目、全部页面、主题/强调色、搜索跳转 |
| `Framework/ShotRunner.cs` | 截图：支持长文档自然高度测量与 `--offset` 视口切片、`--shell --page <slug>` 截图外壳 |

### 1.3 M1 验证

- 构建：`dotnet build src/Apps/Arc.Avalonia.Gallery` 通过（单节点，0 错误）。
- `--smoke`：`itemShows=294 pageShows=243 failures=0`。
- `--audit`：结构与加载正常；当前按预期报 282 个缺失文档 + 119 个缺失条目页（M3 范围），无关系/对照/变体错误。
- 外壳截图：`shots-m2/shell-light.png`、`shell-dark.png`（三栏、TOC 高亮、主题切换、中文渲染均正常）。

## 2. M2：12 页样板

| 条目 | 项目 | 文档 | 条目页 | 类型 |
| --- | --- | --- | --- | --- |
| Button | Actions | `Docs/button.json` | `Pages/ButtonPage.cs` | 组件 |
| Input | Inputs | `Docs/input.json` | `Pages/InputPage.cs` | 组件 |
| Select | Inputs | `Docs/select.json` | `Pages/SelectPage.cs` | 组件 |
| Switch | Inputs | `Docs/switch.json` | `Pages/SwitchPage.cs` | 组件 |
| Tabs | Navigation | `Docs/tabs.json` | `Pages/TabsPage.cs` | 组件 |
| Dialog | Overlays | `Docs/dialog.json` | `Pages/DialogPage.cs` | 组件 |
| Date picker | Pickers | `Docs/date-picker.json` | `Pages/DatePickerPage.cs` | 组件 |
| Line chart | Charts | `Docs/line-chart.json` | `Pages/LineChartPage.cs` | 组件 |
| Data grid | DataDisplay | `Docs/data-grid.json` | 原 `DataGridPage` 展示页改写为条目页 | 组件（Pro） |
| Glass menu | Special.Glass | `Docs/glass-menu.json` | 现有页面加 `ImplementedStates` | 特殊组件（Pro） |
| Site header | Blocks.Marketing | `Docs/site-header.json` | 现有页面加 `ImplementedStates` | 区块 |
| Agent status | Special.AIWidgets | `Docs/agent-status.json` | 现有页面加 `ImplementedStates` | 特殊组件（Pro） |

### 2.1 M2 验证

- 构建通过；`--audit` 对 12 篇文档 **0 错误 0 警告**（其余为缺失项）。
- `--smoke`：294 条目 + 243 页面，0 失败。
- 截图：`shots-m2/light`、`shots-m2/dark`，含每页 3 段切片与 4 页深色版本；外壳截图 `shots-m2/shell-*.png`。

### 2.2 过程中形成的约定

1. **变体标签用英文**（指向 API/变体名，如 `Primary`、`Small`、`Selected`），说明用中文；12 页已统一。
2. **API 表默认值**从运行时元数据读取；未写 `descriptions` 的成员显示 `—`，不阻断。
3. **区块不生成 API 表**：`api` 段留空，第 8 区块由 `uses` 关系生成"组成依赖"。
4. **预览与文档解耦**：`ImplementedStates` 的 key 与 `variants[].key` 严格对齐；预览是否静态铺开各状态由页面决定。
5. **关系单边声明**：`uses`/`extends` 只写在具体一侧，反向关系（被使用/基础版本）由布局自动生成。

### 2.3 已知限制（不影响 M2 验收，M3 前需定夺）

| 项 | 情况 | 建议 |
| --- | --- | --- |
| `select` 没有 `state-open` | `ArcSelect` 未挂模板时设 `IsOpen` 不展开 Popup，内联展开会压住文档 | 保持现状，说明"展开态由交互演示" |
| ~~Dialog 预览同时打开 3 个对话框~~ | **已修**：改为 1 个打开态 + 2 个静态底栏示例（无焦点竞争） | — |
| Glass menu / Agent status / Site header 的部分状态靠交互触发 | 预览未静态铺开全部状态（如 agent-status 的运行态由按钮切换） | **已确认规则**：M3 起每个声明状态必须静态可见；M2 这 3 页在 M3 一并补齐 |
| `data-grid` 的 catalog 分类是 Advanced charts | 沿用 uiarc.dev 分类，不在 Tables | 保持 catalog 不变；页面 Group 跟随分类 |
| 旧 67 个展示页仍在侧栏"展示页（迁移中）" | 为迁移期保留入口，避免 `--shot` 覆盖断层 | M4 随旧页退役删除 |
| `DataGridDemos.cs` 已无引用 | DataGridPage 改为条目页后遗留 | M4 清理 |

## 3. 下一步（M3，已完成）

> M3 全量生产与 M4 收尾结果见 [`12-gallery-doc-m3-m4.zh.md`](12-gallery-doc-m3-m4.zh.md)。

1. ~~按 12 页样板的口吻与结构，产出剩余 282 篇中文文档与 119 个条目页~~（已完成：294/294）。
2. ~~把旧展示页的演示拆分为条目预览，准备退役旧页~~（已完成：退役 46 页）。
3. ~~每个里程碑跑 `--audit` / `--smoke` / 截图~~（已完成：audit 0 错误、smoke 315 页 0 失败）。
