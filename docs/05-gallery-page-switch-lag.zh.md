# Gallery 页面切换卡顿：原因分析与测量报告

> 症状：在 Gallery 里点侧栏切换页面时明显卡顿（点击后界面冻结一下才切换）。
> 方法：在本仓库中运行**离屏测量探针**（`.recon/NavPerf`，不修改 `src/**`），用真实 `GalleryWindow` + 真实点击路径逐项计时，并用反射读取缓存、控件数、`DispatcherTimer` 数量和分配量。
> 结论：卡顿不是单一渲染问题，而是**三条成本叠加在同一个 UI-thread 帧里**，其中最大的一条是"每次点击都重建整个侧栏"。
> **状态：P0（选中态增量更新 + 分类懒展开）已于 2026-10-25 实施，实测 366 → 15.8 ms / 点击，见 [§5](#5-p0-修复实施与实测结果)。**

---

## 0. 结论摘要（按贡献排序）

| # | 原因 | 每次点击的实测成本 | 为什么 |
| --- | --- | --- | --- |
| **1** | **每次索引点击都全量重建侧栏**：`ItemButton.Click` → `RefreshNavigation()`，`Children.Clear()` 后重新创建 96～390 个 `ArcButton` | **270–572 ms**，并分配 **28–108 MB**，触发约 10 次 Gen0 + 1–2 次 Gen2 | 侧栏没有虚拟化；`ArcButton` 模板 + `ControlTheme` + 隐藏 `ArcSpinner` 的附加成本约 **0.5–1 ms / 63 KB 每实例** |
| **2** | **首次访问某个页面**：`page.Build` + 模板/样式应用 + 整页布局 + 首帧渲染 | 平均 **68 ms**，p95 **168 ms**，最坏 **300 ms**（宽页面 200–300 ms） | 页面是 120–650 个节点、8–10 层嵌套的非虚拟化树；布局/样式应用是主要部分（平均布局 38 ms，共享页平均 85 ms） |
| **3** | **88 个"无独立页/无别名"组件首次点击**：`FindComponentSection` 同步逐页构建共享页直到找到标题 | **3–473 ms**（实测构建 9–52 个共享页；action-button 45 页/473 ms） | 标题索引没有预计算，在点击回调里做"构建 + 递归查找" |
| **4** | **后台常驻定时器与内存放大**：侧栏 386 个按钮各带一个隐藏 `ArcSpinner` 的 16 ms timer；页面缓存无上限 | 空闲时 **~24% 单核**（386 timer）vs 11%（96 timer）；访问 219 页后 **368–435 MB** 托管内存、44,874 个缓存控件、25–30 次 Gen2 | 隐藏 spinner 仍在 60 fps tick；`_builtViews` 永不淘汰；每次重建 108 MB 分配加剧 GC 暂停 |
| **5** | **即使全部缓存，切换仍需重挂载 + 重新布局** | 平均 **21 ms**，p95 **51 ms**，最坏 **99 ms**（超过 16.7 ms 帧预算） | `_contentScroller.Content = view` 触发整页 detach/attach；页面里的按钮重新创建 spinner timer，重新走一遍样式/布局 |

> 一句话：**卡顿的主因不是"画得慢"，而是"每次点击都把侧栏整棵树重建一遍"**——实测把"只改选中状态"的成本从 270–572 ms 降到 **0.7 ms**，这就是最直接的修复点（已实施，见 §5）。

---

## 1. 测量方法

### 1.1 探针

`.recon/NavPerf`（`net10.0`，引用 `Arc.Avalonia.Gallery`，`gitignore` 已忽略 `.recon/`）：

* 用 `AppBuilder.SetupWithoutStarting()` 初始化真实 App（加载 `App.axaml` 里全部 22 个样式字典：FluentTheme + 21 个 `StyleInclude`、主题、字体）；
* 创建真实 `GalleryWindow`（1180×900）并 `Show()`，通过 `Dispatcher.UIThread.RunJobs()` 驱动布局；
* 用反射调用 `GalleryWindow` 的真实私有路径：`RefreshNavigation`/`RebuildSidebar`/`ShowPage`/`ShowItem`/`FindComponentSection`/`ExpandFor`，并读取 `_builtContent`/`_builtViews`/`_expanded`/`_sidebarHost`/`_contentScroller`；
* 渲染用 `RenderTargetBitmap`（与 `ShotRunner` 相同方式）；分配用 `GC.GetAllocatedBytesForCurrentThread()` / `GC.GetTotalAllocatedBytes()`。

复现：

```powershell
pwsh -NoProfile -File tools/build.ps1 -Project .recon\NavPerf\NavPerf.csproj
dotnet .recon\NavPerf\bin\Debug\net10.0\NavPerf.dll      # 全量：219 页冷/热切换 + 294 个目录项点击
dotnet .recon\NavPerf\bin\Debug\net10.0\NavPerf.dll 2    # 侧栏 rebuild/refresh 拆分 + 标题搜索 + 内存
dotnet .recon\NavPerf\bin\Debug\net10.0\NavPerf.dll 3    # ArcButton/Spinner/TextBlock 单行成本微基准
dotnet .recon\NavPerf\bin\Debug\net10.0\NavPerf.dll 4    # 每次操作的分配/GC
dotnet .recon\NavPerf\bin\Debug\net10.0\NavPerf.dll 5    # 侧栏 build vs layout 的分配拆分
```

### 1.2 测量口径与偏差

* 所有时间为 **wall-clock**，包含同步构建 + 强制布局；真实应用里这些工作发生在同一帧，数值可直接对应"界面冻结多久"。
* 环境为离屏窗口 + 软件光栅（`RenderTargetBitmap`）。**CPU 部分（侧栏构建、布局、样式、GC）与平台无关**，可迁移；`render_ms` 在真实 GPU 下会更小（本报告里 render 只占冷切换的 5 ms 左右，不影响结论）。
* 侧栏大小取决于展开的分类数。探针额外做了"全部展开"（386 按钮）来测最坏状态；真实使用中 `ExpandFor` 只会**越点越多**（没有自动折叠），因此会逐步逼近该状态。

---

## 2. 测量结果

### 2.1 每次点击的侧栏成本（主因）

`GalleryWindow.RefreshNavigation()` 会清空并重建除前 3 个固定节点外的所有子项（`GalleryWindow.cs:202-285`），而每个目录项/页面按钮都是 `ArcButton`。

| 侧栏状态 | 按钮 / 节点 | RefreshNavigation 构建 | 布局 | **合计** | 分配 |
| --- | --- | --- | --- | --- | --- |
| 初始（只展开默认页） | 96 / 926 | 96 ms | 174 ms | **270 ms** | 28 MB（10 MB 构建 + 18 MB 布局） |
| 全部分类展开 | 386 / 3,278 | 148 ms | 424 ms | **572 ms** | 108 MB（30 MB + 78 MB） |
| **只改选中状态（修复方案的上界）** | 386 / 3,278 | 0.3 ms | 0.4 ms | **0.7 ms** | 1 KB |
| 整体替换侧栏（`RebuildSidebar`，换肤/过滤走这里） | 96 / 926 | 99 ms | 279 ms | **378 ms** | — |

按侧栏规模分桶（294 次真实 item 点击统计）：

| 侧栏按钮数 | 样本 | Refresh 构建 | Refresh 布局 | 合计 |
| --- | --- | --- | --- | --- |
| ~100 | 90 | 54.8 ms | 126.1 ms | **181 ms** |
| ~200 | 111 | 85.0 ms | 251.3 ms | **336 ms** |
| ~300+ | 93 | 128.7 ms | 371.7 ms | **500 ms** |

**为什么这么贵**：`RefreshNavigation` 每次 `Children.Clear()` + 重新 `new ArcButton`。微基准（同一 App 样式环境，300 个控件 attach+layout）：

| 控件 | 数量 | Attach + Layout | 每实例 | 分配 |
| --- | --- | --- | --- | --- |
| `ArcButton`（Ghost/Sm） | 100 | 79–191 ms | ~0.8–1.9 ms | — |
| `ArcButton` | 300 | 342–642 ms | ~1.1–2.1 ms | — |
| `ArcButton`（放在裸 StackPanel） | 386 | **377 ms** | ~1.0 ms | **24.5 MB**（~63 KB/按钮） |
| 原生 `Button`（Fluent） | 300 | 203 ms | ~0.7 ms | — |
| `TextBlock` | 300 | 13–56 ms | ~0.04–0.19 ms | — |

即：**`ArcButton` 的模板 + `ControlTheme` + 隐藏 spinner + 22 个样式字典的匹配，每实例约 1 ms / 63 KB**。侧栏 386 行就是 377 ms / 24.5 MB；页面里的按钮同样按这个价格计费。

### 2.2 一次真实 item 点击的总耗时（缓存已热、侧栏已展开）

| 目录项类型 | 样本 | 侧栏 Refresh（构建+布局） | 页面 ShowItem+布局 | **合计平均** | 最坏 |
| --- | --- | --- | --- | --- | --- |
| component（128） | 128 | 60.5 + 146.2 | 16.7 + 4.6 | **231 ms** | 485 ms |
| special（55） | 55 | 84.6 + 274.8 | 12.3 + 5.7 | **385 ms** | 524 ms |
| block（111） | 111 | 125.7 + 360.2 | 27.2 + 6.7 | **522 ms** | 705 ms |

例：`agent-run` 705 ms（refresh 128 + 布局 563 + 页面 14）、`layout-morph` 664 ms、`ai-chat` 659 ms。
**侧栏占一次点击总时间的 90–93%**；页面本身（已缓存时）只占 10–40 ms。

### 2.3 页面切换本身（清空缓存 = 首次访问）

| 指标 | 值 |
| --- | --- |
| 冷切换 total（构建+布局+渲染） | avg **68.4 ms**，p50 50.2，p95 **167.6**，max **299.9** |
| 其中 | 构建 25.1 ms + 布局 38.2 ms + 渲染 5.1 ms |
| 热切换（全部缓存） | avg **21.3 ms**，p50 17.2，p95 **50.9**，max 98.6 |
| 其中 | show 13.4 ms + 布局 4.4 ms + 渲染 3.4 ms |

最慢的 10 个冷切换页面：

| 页面 | 构建 | 布局 | 渲染 | 节点 |
| --- | --- | --- | --- | --- |
| Emoji picker | 20.9 | 276.2 | 6.8 | 585 |
| Share access | 47.1 | 209.7 | 7.7 | 337 |
| Inputs | 33.7 | 184.4 | 3.8 | 505 |
| Advanced inputs, part two | 36.7 | 180.6 | 3.9 | 461 |
| Inputs, part two | 43.7 | 167.4 | 2.8 | 441 |
| Avatar upload | 40.1 | 157.8 | 4.0 | 523 |
| Mega menu | 16.7 | 164.9 | 10.1 | 428 |
| Data grid | 19.3 | 150.9 | 3.0 | 492 |
| Icons | **132.7** | 25.3 | 9.3 | 653 |
| Actions | 48.8 | 109.3 | 4.2 | 370 |

共享页（组件演示页）平均布局 84.7 ms，item 页（Special/Block 文档页）平均 23.3 ms——因为共享页本身由大量模板化控件（按钮、输入框）组成，同样按上面的 ~1 ms/实例付费。

### 2.4 88 个组件的"首次点击"额外尖峰

128 个组件里只有 40 个配了别名（`ArcCatalogAliases`），其余 88 个走 `FindComponentSection`：**在点击回调里逐页 `page.Build` 直到匹配到标题**（`GalleryWindow.cs:632-666`）。

| 组件（首次点击，空缓存） | `FindComponentSection` 耗时 | 构建的共享页数 |
| --- | --- | --- |
| action-button | **473 ms** | 45 |
| hold-to-confirm | 108 ms | 52 |
| scrub-input | 11 ms | 40 |
| date-range-picker | 30 ms | 35 |
| pagination | 41 ms | 12 |
| chat-thread | 12 ms | 50 |
| 其余样本 | 3–30 ms | 20–50 |

缓存建好之后，同样的查找平均 **0.3 ms**（max 15 ms，88 个全部能找到标题、无 fallback）——所以这只是"第一次点击某个组件的尖峰"，但足以被感知为"卡了一大下"。

### 2.5 后台成本：隐藏 timer、内存与 GC

| 指标 | 数值 |
| --- | --- |
| 侧栏 386 按钮时的 enabled `DispatcherTimer` | **386**（每个 `ArcButton` 模板里一个隐藏 `ArcSpinner`，`ArcButton.axaml:43-51`） |
| 空闲 CPU（只泵 dispatcher，2 秒计时） | 386 按钮：**484 ms / 2 s ≈ 24% 单核**；96 按钮：219 ms / 2 s ≈ 11%；差额 ≈ 130 ms/s 全部来自隐藏 spinner 的 60 fps tick |
| 页面内按钮带来的 timer | Actions 页 31 个、Mega menu 29 个、Share access 22 个…… |
| 访问 219 页后的缓存 | 219 个视图 / 44,874 个控件 / **368–435 MB** 托管内存；期间 25–30 次 Gen2 |
| 每次侧栏重建的分配 | 28 MB（96 行）～**108 MB**（386 行）；约 10 次 Gen0 + 1–2 次 Gen2 / 点击 |
| 每次页面切换的分配 | 冷 21 MB；热 16 MB（两次切换 32 MB） |
| 切走页面后的残留 timer | 6 个（`Agent run` 页），其余控件基本在 detach 时停表 |

---

## 3. 因果链（一次点击发生了什么）

```
鼠标点击侧栏条目
└─ ItemButton.Click（GalleryWindow.cs:406-412）
   ├─ _selected = item; ExpandFor(item);        // 展开的分类只增不减
   ├─ RefreshNavigation()                        // ★ 主因：清空重建整个侧栏
   │  ├─ Children.Clear()（96–390 个 ArcButton 全部 detach）
   │  ├─ 重新 new ArcButton × N
   │  └─ 布局 pass：每个 ArcButton 模板实例化 + 样式匹配 + 隐藏 ArcSpinner 建 16ms timer
   │     ≈ 0.5–1 ms / 63 KB 每行 → 270–572 ms + 28–108 MB
   └─ ShowItem(item)
      ├─ 有 item page / 别名 → ShowPage
      ├─ 否则 FindComponentSection：同步 Build 9–52 个共享页找标题（首次 3–473 ms）
      └─ ShowPage
         ├─ 首次：page.Build（25 ms 均值）+ 模板/样式应用 + 整页布局（38 ms 均值）+ 首帧渲染（5 ms）
         └─ 已缓存：Content 换挂（13 ms）+ 重新布局（4 ms）+ 渲染（3 ms）
            且页面里的 ArcButton 重新创建隐藏 spinner timer
```

三层叠加后的用户感受：**一次点击冻结 0.2–0.7 秒**；首访宽页面/无别名组件再叠加 0.1–0.5 秒；随着浏览的页面与展开的分类变多，侧栏越来越大、缓存内存越来越高，卡顿会**逐渐加重**（Gen2 GC 暂停 + 390 个 timer 的后台 CPU）。

> 一个重要的区分：**只有"目录项"会触发侧栏重建**。侧栏里的 294 个 Component/Block/Special 条目（`ItemButton`）和分类展开按钮（`CategoryRow`）都调用 `RefreshNavigation()`；而顶部 Overview / 底部 "Demo pages" 的页面按钮（`PageButton`，`GalleryWindow.cs:318-333`）只调用 `ShowPage`，没有侧栏成本。所以"点组件/块条目卡得明显、点 demo 页相对轻"正是这套代码路径的直接表现。

---

## 4. 建议修复（按收益/成本排序）

### P0：选中态不再重建侧栏（1 小时级改动，收益最大）

`ItemButton.Click` 里把 `RefreshNavigation()` 换成"只更新选中视觉 + 按需插入新展开的分类行"：

* 维护 `Dictionary<slug, ArcButton>`，切换时只对旧/新两个按钮 `Classes.Set("selected", ...)`；
* `ExpandFor` 命中新分类时，只 `Insert` 该分类的 item 行（一次），不重建全部；
* `BringIntoView()` 滚到当前项。

实测对照：**0.7 ms / 1 KB vs 270–572 ms / 28–108 MB**。
预期：component 点击 231 → **~25 ms**（页面已缓存）或 **~90 ms**（首次），block 点击 522 → **~40 ms**。

### P0/P1：侧栏虚拟化或换轻量行

* 用 `ItemsControl` + `VirtualizingStackPanel`（或只把"已展开分类"的 item 行缓存、用 `IsVisible` 切换），只保留 ~25 个可见行；
* 或给侧栏用轻量行控件（`Border` + `TextBlock` + pointer handler），避免 `ArcButton` 的模板/样式/spinner 成本。

实测依据：386 个 `ArcButton` attach+layout = 377 ms/24.5 MB；同数量 `TextBlock` = 13–56 ms。虚拟化后侧栏任何重建都应 **< 20 ms**。

### P1：`ArcSpinner` 按需创建，只在可见/加载时计时

* `ArcSpinner.OnAttachedToVisualTree` 只在 `IsVisible == true` 时建 timer，`IsVisibleChanged` 时启停；
* `ArcButton` 模板只在 `Loading` 时实例化 spinner（或使用不含 spinner 的轻量模板变体）。

预期：386 按钮时空闲 CPU 从 **24% → 11%**（回到没有隐藏 spinner 的基线），页面 attach 也少建 20–30 个 timer/页。

### P1：首次访问页面的成本

* 空闲时（`DispatcherPriority.Background`）预热可能的下一页（`Build` + `Measure`），让点击时只做挂载；
* 缓存切换改为"保留最近 N 页在面板里，用 `IsVisible` 切换"，避免 detach/attach 后的样式与布局重做（热切换 21 ms 的主要来源）；
* 对超宽页面（Icons 653 节点、Emoji picker 276 ms 布局）做列表虚拟化/分段渲染。

### P1：预计算"组件 → 共享页"索引

* 启动时后台构建一次 `slug → (page, heading)`（或直接从目录数据生成静态映射），替代点击时的 `FindComponentSection`；
* 实测缓存后查找 0.3 ms，说明索引本身很便宜，贵的是"在点击路径里构建页面"。

### P2：给页面缓存加 LRU 上限

* `_builtViews` 当前无淘汰（访问 219 页 → 44,874 控件 / 368–435 MB）；保留最近 8–12 页即可在"切回速度"与内存/GC 之间平衡；
* 页面 detach 时确保所有动画 timer 停止（目前只有 `Agent run` 页有 6 个残留）。

### 预期总效果（基于实测）

| 场景 | 现在 | 仅做 P0（选中态增量） | P0 + 侧栏虚拟化 + spinner 修复 |
| --- | --- | --- | --- |
| 切到已访问页面 | 231–522 ms | **~20–40 ms** | **~20–30 ms** |
| 切到首次访问的普通页 | +68 ms | **~90–110 ms** | **~70–90 ms** |
| 切到首次访问的宽页面 | +200–300 ms | **~220–330 ms** | **~100–200 ms** |
| 首次点击无别名组件 | +3–473 ms | 仍有一次构建尖峰 | 预计算索引后消失 |
| 空闲 CPU（386 按钮） | ~24% 单核 | 同左 | **~11%** |
| 侧栏重建内存 | 108 MB/点击 | ~0 | ~0 |

---

## 5. P0 修复实施与实测结果

> 已实施（2026-10-25）。只改了一个文件：`src/Arc.Avalonia.Gallery/Framework/GalleryWindow.cs`；`src/**` 其余部分未动。

### 5.1 改动内容

1. **选中态不再重建侧栏**：`ItemButton.Click` 不再调用 `RefreshNavigation()`（原来 `GalleryWindow.cs:406-412`），改为：
   * `_selected = item`；
   * `ExpandGroupsFor(item)`：只确保该条目所属分类展开（首次展开时插入该分类的行）；
   * `SelectItemButton(button)`：对旧/新两个按钮切换背景、前景与字重（`ClearValue` 恢复主题默认值）；
   * `ShowItem(item)` + `BringIntoView()`（侧栏滚动到当前项，顺带修掉了原来每次点击侧栏跳回顶部的问题）。
2. **分类行按需物化、之后只切 `IsVisible`**：`CategoryRow` 首次展开时调用 `EnsureGroupItems` 插入该分类的行；再次展开/折叠只是 `host.IsVisible = true/false`，不再重建整棵侧栏。
3. **新增增量状态**：`_groupContainers`（分类外层容器）、`_groupItems`（分类行 host）、`_itemButtons`（slug → 按钮）、`_selectedItemButton`；`RefreshNavigation()` 在整树重建前清空它们（搜索、tier 过滤、换肤仍走完整重建，语义不变）。
4. 搜索/过滤/换肤路径、页面构建与缓存策略均未改动；`--smoke` 的 5 条导航路径全部通过（见 §5.3）。

### 5.2 修复前后实测（同一探针、同一机器）

| 指标 | 修复前 | 修复后 | 变化 |
| --- | --- | --- | --- |
| 目录项点击 avg（294 项，侧栏 390 按钮） | **366.2 ms** | **15.8 ms** | **−96%** |
| 目录项点击 p50 | 345.2 ms | 13.3 ms | −96% |
| 目录项点击 p95 | 638.5 ms | **33.4 ms** | **−95%** |
| 目录项点击 max | 673.9 ms | 72.7 ms | −89% |
| component 点击 avg | 231 ms | **14.2 ms** | −94% |
| special 点击 avg | 385 ms | **15.3 ms** | −96% |
| block 点击 avg | 522 ms | **17.9 ms** | −97% |
| 每次点击分配 | 28–108 MB（侧栏重建） | **~5.2 MB**（只剩页面挂载） | −95% |
| 只换选中高亮 | 270–572 ms（重建） | **0.2–0.7 ms，~0 分配** | −99.9% |
| 分类首次展开 | 270–572 ms（整栏重建） | **1.0 ms 构建 + 10.7 ms 布局**（最大 48.7 ms） | −95% |
| 冷页面切换 avg / p95 | 68.4 / 167.6 ms | **57.2 / 143.1 ms** | 略降（GC 压力减小） |
| 热页面切换 avg / p95 | 21.3 / 50.9 ms | **16.7 / 42.1 ms** | −22% / −17% |
| 无别名组件首次点击 total | 175–256 ms | 51–163 ms（仍含标题索引构建） | −40% 左右 |
| 侧栏完整重建（搜索/tier 过滤，未改） | 270–572 ms | 不变 | 仍是 P1 |
| 隐藏 spinner timer / 空闲 CPU | 386 timer，~16–24% 单核 | 不变 | 仍是 P1 |
| 219 页缓存内存 | 368–435 MB | 不变 | 仍是 P2 |

> 说明：`SUMMARY_ITEM` 的修复后数值来自完整 294 次"真实按钮点击"（每次通过 `RaiseEvent(ClickEvent)` 走真实 handler，含 `ShowItem` + 布局 + 渲染）；`ITEM_SWEEP` 的分配测量为 25 ms / 5.2 MB 每次（包含不同页面的挂载分配），侧栏部分已接近 0。

### 5.3 回归验证

* `pwsh -NoProfile -File tools/build.ps1 -Project src/Arc.Avalonia.Gallery/Arc.Avalonia.Gallery.csproj`：0 error（仅原有 warning）。
* `dotnet .../Arc.Avalonia.Gallery.dll --smoke`：`GALLERY SMOKE: clicks=836 pageShows=657 itemShows=588 failures=0`，26.3 s（覆盖页面按钮、分类展开、tier 过滤、全部目录项、换肤/换 accent、搜索过滤 + 结果点击）。
* 探针复跑（`.recon/NavPerf`）：`SUMMARY_ITEM`、`SUMMARY_COLD/WARM`、`EXPAND_FIRST`、`ITEM_SWEEP` 均按上表变化，`errors=0`。

### 5.4 仍然存在（下一步 P1，不在本次 P0 范围）

1. **搜索框每敲一键仍走完整 `RefreshNavigation`**（侧栏 390 行时 ~450–580 ms）——建议加 150–250 ms 防抖，或搜索结果也做增量/虚拟化；
2. **侧栏本身仍是 386 个 `ArcButton` 的普通 `StackPanel`**：完整重建仍要 ~300–580 ms，386 个隐藏 spinner timer 仍以 60 fps tick（空闲 ~16–24% 单核）——建议虚拟化/轻量行 + `ArcSpinner` 按需计时（P1）；
3. **页面切换本身**：冷切换 p95 仍 143 ms、热切换 16.7 ms（p95 42 ms），且每次挂载仍分配 ~5 MB——页面构建/挂载策略、预取、保留挂载页面是下一步；
4. **无别名组件的首次点击**仍会同步构建 9–52 个共享页找标题（本次实测 total 51–163 ms）——预计算 `slug → section` 索引即可消除；
5. **页面缓存无上限**（219 页 ≈ 44,874 控件 / 440 MB）——LRU 上限（P2）。

---

## 6. 附：为什么"只改页面内容"不够

只优化页面渲染（缓存几何、减少每帧分配）对**本症状**帮助有限：实测页面本身已缓存时只占点击成本的 7–20%，而且首访页面最贵的部分也是"模板/样式应用 + 布局"（与侧栏同源）。真正的杠杆是：

1. **不要把"选中"实现成"重建"**；
2. **不要让 386 个隐藏 spinner 以 60 fps 空转**；
3. **不要让模板化按钮承担静态列表行**（或让其虚拟化）；
4. **不要在点击回调里构建页面**（标题索引/预构建应提前到空闲时）。

这四件事都不需要改渲染架构，属于"改对生命周期/更新策略"就能拿到的收益。
