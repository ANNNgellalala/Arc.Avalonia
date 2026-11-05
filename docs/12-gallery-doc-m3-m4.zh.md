# Gallery 文档化改造：M3 全量内容 + M4 收尾记录

> 对应规格：[`10-gallery-doc-platform.zh.md`](10-gallery-doc-platform.zh.md)；
> 前两个里程碑见 [`11-gallery-doc-m1-m2.zh.md`](11-gallery-doc-m1-m2.zh.md)。
> 状态：**M1–M4 完成**；`--audit` 294/294 文档、0 错误 0 警告；`--smoke` 315 页 0 失败。

## 1. M3：294 页全量内容

### 1.1 覆盖

| 类别 | 数量 | 文档 | 条目页 |
| --- | --- | --- | --- |
| 组件（23 个分类） | 128 | 128 | 128（M2 样板 9 个 + M3 新建 119 个） |
| 特殊组件（5 个分组） | 55 | 55 | 55（已有页面，补 `ImplementedStates`，必要处最小静态增补） |
| 区块（6 个分组） | 111 | 111 | 111（已有页面，补 `ImplementedStates`） |
| **合计** | **294** | **294** | **294** |

分批方式：按项目/分类切包，每个包 10–21 个条目，由多个作者并行完成；Lead 统一构建、审计与截图。
文档 JSON 随项目嵌入（每个项目 csproj 增加 `<EmbeddedResource Include="Docs\*.json" />`），
页面只声明 `ImplementedStates` 与预览，正文全部在数据里。

### 1.2 内容约定（在 M2 基础上固化）

1. **变体标签英文、说明中文**：标签对应 API/变体名（`Primary`、`Small`、`Selected`），note 用中文解释。
2. **声明即静态可见**：`variants` 里列出的每个状态都必须在预览里直接看到，不依赖点击/悬停；
   `ImplementedStates` 与 `variants[].key` 严格双向一致，审计阻断。
3. **关系单边声明**：`uses`/`extends` 只写在具体一侧，反向关系由布局自动生成。
4. **区块不写 API**：`api.type` 留空，第 8 区块改为由 `uses` 生成的"组成依赖"表；每个区块 ≥3 条 `uses`。
5. **对照表**：每页 1–3 组，每组 ≥3 个维度，对照目标必须出现在本页关系中。
6. **示例不做编译校验**（已确认）：只校验存在、非空与语言标注。

### 1.3 验证

```text
CATALOG AUDIT: items=294 docs=294 pages=294 errors=0 warnings=0 pageFailures=0
AUDIT OK

GALLERY SMOKE: itemShows=294 pageShows=315 failures=0   (~21s)
```

构建：`dotnet build src/Apps/Arc.Avalonia.Gallery` 单节点 0 错误。
截图：`shots-m2/`（M2 样板，含 12 页 × 浅色切片 + 深色抽查）与 `docs/images/docs/`（README 用 18 张）。

### 1.4 M3 期间的取舍与已知限制

| 项 | 情况 |
| --- | --- |
| AI Explainers 11 页 | 纯绘制插画，公开 API 只有 `SetPaused/TogglePlay`，减弱动效下固定渲染海报帧；`variants` 描述该帧内同时可见的部件，purpose 中注明定格行为 |
| 部分特殊组件 API 表为 0 行 | 控件没有公开属性/事件（自绘类），API 区块显示"0 个属性、0 个事件"，属实现现状，不阻断 |
| 区块的 `uses` 语义 | 少数区块没有第三类真实共享控件，使用最接近的语义依赖（如 progress、card、slider）并在 note 说明 |
| 动效类页面的静态帧 | `--shot` 全局开启减弱动效，按"curated settled frame"评审；正常浏览时仍有动画 |
| 区块预览宽度 | 区块按 gallery 宽度构图，文档列较窄；预览区改为可横向平移，避免右侧被裁切 |

## 2. M4：旧页退役与收尾

### 2.1 退役

- 删除了 **46 个** 组件项目的分类合并展示页（Inputs/Actions/Charts/Overlays/Pickers/… 的 "part two"
  系列）。这些页面的演示已由 294 个条目页各自承担。
- 保留：5 个指南页（Foundations/Tokens/Icons/Motion/Kit proof）、首页、文档状态页，以及 14 个
  Agent/LifeToday/Virtualized list **场景示例**页（这些控件不在 Arc 294 条目目录内，作为场景演示保留，
  侧栏分区已从"展示页（迁移中）"改名为"场景示例"）。
- 页面注册表从 361 页缩减到 315 页；`--audit` 仍为 294/294。

### 2.2 平台收尾

- 文档页宽度：正文 860 px；区块预览在文档列内可横向平移（`ScrollViewer`），不再被 `ClipToBounds` 裁切。
- 截图工具：自然高度测量改为"挂载后测量内容"，修复长文档切片把标题样式测丢的问题；新增
  `--offset` 切片、`--max-height`、`--shell --page <slug>`。
- 外壳：三栏布局、右侧 TOC 滚动高亮、搜索、tier 过滤、主题/强调色、覆盖率页、上一页/下一页均按规格工作。
- README 重写为文档平台视角：新的截图、命令、验收数字与已知限制。

### 2.3 验证

```text
--audit   items=294 docs=294 pages=294 errors=0 warnings=0 pageFailures=0  -> AUDIT OK
--smoke   itemShows=294 pageShows=315 failures=0
--shot    全量 315 页 × 2 主题渲染成功（退出码 0）；docs/images/docs/ 保留 17 张精选截图（浅色/深色、外壳、关系区、API 表）
```

### 2.4 后续可选清理（不影响验收）

1. 旧条目页上的英文 `WhenToUse` / `KeyboardAndAccessibility` 等字段已不再渲染；可在一次机械清理中
   统一删除，连同 `ArcItemPage` 的五个迁移期兼容成员。
2. 旧展示页删除后遗留的演示助手（如 `DataGridDemos.cs`）已无引用，可随下次重构清理。
3. `shots-m2/` 是 M2 评审素材，可移入文档或删除；仓库正式截图以 `docs/images/docs/` 为准。
4. 中文字体仍走系统回退（按 M0 决策）；若后续出现缺字再加 CJK 字体包。

## 3. 生成物与版本控制

- `shots/` 是历史展示页的截图基线（旧页已退役，图片保留作对照）；新的 `--shot` 输出属于生成物，
  已由 `.gitignore` 忽略。README 使用的精选截图保存在 `docs/images/docs/`。
- 旧条目页上的英文文档字段（`WhenToUse`、`KeyboardAndAccessibility` 等）仍保留为编译兼容成员，
  渲染层已不再读取，列入后续机械清理。
