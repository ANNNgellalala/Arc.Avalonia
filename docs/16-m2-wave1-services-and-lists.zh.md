# 16 · M2 第一波：服务层/窗口壳 + 数据驱动列表

> **依据**：[`docs/13-framework-layering-and-agent-top.zh.md`](13-framework-layering-and-agent-top.zh.md) v3.2 的 M2 计划（D6 C2/C3）
> **范围**：G3b 服务层 + `ArcWindow`（24 人日）、G3c 数据驱动列表 + Feature DataGrid（25 人日）
> **基线**：M1 完成（28 包 `0.9.0-alpha`、门禁 7/7 GREEN、build 0 error、audit 294/294、smoke 315、Verify 46）

---

## 1. 交付总览

| 卡 | 交付 | 关键数字 |
|---|---|---|
| **G3b 服务层 + 窗口壳** | 新工程 `src/Components/Arc.Avalonia.Services`（L2，`Arc.Avalonia.Services`）；`IDialogService` / `INotificationService` / `INavigationService` / `IFilePickerService` / `IArcThemeService`；`ArcServiceProvider`（静态注册/查找，无 DI 容器）；`ArcWindow : Window`（PageHost + OverlayHost + 可选 Custom 标题栏 + Services 注册门面）；3 个控件改走服务（未注册时回退旧行为） | 新增 1 包（37 KB）；Gallery `GalleryWindow : ArcWindow`；新增 Services 演示页 |
| **G3c 数据驱动列表** | `ArcTreeTable` 窗口化（展平 + 视口窗口 ±6 overscan + 80 行缓存上限，10k 节点只实例化视口级行）；`ArcVirtualList` 作为可复用列表原语接入 DataDisplay；新增 `src/Feature/Arc.Avalonia.Feature.DataGrid`（唯一允许 `Avalonia.Controls.DataGrid` 的 L2f 包，`ArcDataGridHost` + Arc 皮肤 + 分页） | 10k 节点实测 `10,000 flattened rows · 20 realised row controls`；新增 3 个演示页；Verify +14 断言 |

## 2. 实测结果

| 检查 | 结果 |
|---|---|
| 构建 | **0 error**（7 个既有 analyzer 警告） |
| `--audit` | items=294 docs=294 pages=294 errors=0 |
| `--smoke` | itemShows=294 **pageShows=319** failures=0（315 + G3c 3 页 + Services 1 页） |
| `VERIFY` | **checks=60 failures=0**（M1 46 + 服务 7 + 列表 7/窗口数学/行复用/分页） |
| `check-all` | **7/7 GREEN**（R1/R2/R3/R4/R5/R6/R8） |
| `check-package` | **30/30 GREEN**（新增 Services、Feature.DataGrid；期望包改为从 IsPackable 推导） |
| `check-public-api` | **30/30 MATCH**（新增 2 包基线） |
| `pack` | 30 包 / 1.88 MB / `0.9.0-alpha` |
| 截图 | `--shot --page services`、DataDisplay 3 页双主题通过（≤1400px） |

## 3. 设计要点

### 3.1 服务层（无 DI 容器）

* `ArcServiceProvider`：进程级静态注册/查找；`ArcWindow.Services` 提供窗口级门面（`EnsureRegistered` 默认实现，不覆盖应用先注册的实现）。
* `IDialogService`：`ShowAsync<TResult>` / `ConfirmAsync`；`ArcDialogService` 用 `SemaphoreSlim(1,1)` 串行 + `IArcDialogPresenter`，默认 `OverlayDialogPresenter` 复用 `ArcDialog` 的焦点陷阱、Esc、backdrop、焦点恢复。
* `INotificationService`：按 `Key` 去重 + 生命周期 + 窗口级 `ArcToastHost` sink。
* `INavigationService`：最小 frame（栈/替换/回退/Changed）。
* `IFilePickerService`：默认 `StorageProviderFilePickerService`（走 `TopLevel.StorageProvider`，无窗口返回空），可注入 fake 供测试。
* 3 个控件（`ArcFileDropzone`/`ArcAvatarUpload`/`ArcChatThread`）改为"服务优先、未注册回退旧路径"，公开 API 与语义零变更。

### 3.2 ArcWindow

```text
Grid (Auto,*)
 ├─ TitleBar   (Auto，默认隐藏；TitleBarMode=Custom 时 ExtendClientArea 自绘，含拖动/双击最大化/最小化/最大化/关闭)
 └─ PageHost   (*)  ← 页面内容入口（Page 属性 / PageHost.Content）
 └─ OverlayHost(RowSpan=2) ← Dialog / Toast / Drawer
```

* Content 只在构造时设为 shell root（修复了 M2 初次集成时的 `AttachedToLogicalTreeCore` 崩溃：曾误在 `OnPropertyChanged(Content)` 里二次回写）。
* Custom 标题栏仅编译验证，未做 Win32 交互点验（已知限制）。

### 3.3 树窗口化（不重写 TemplatedControl）

* 保留 `ArcTreeTable : TemplatedControl`；新增 `ArcVirtualRowsPanel`（extent = 行数 × 44，按扁平索引 arrange）+ 窗口数学（±6 overscan）+ 有界行缓存（80）。
* `BuildBody` 只实例化视口窗口；`ScrollChanged/SizeChanged/Loaded` 驱动窗口更新；键盘焦点走 `EnsureVisible`；排序/展开/三态选择仍经 Flatten + RebuildContent。
* 实测诊断：10k 节点 → `10,000 flattened rows · 20 realised row controls · window [0..19]`；滚动后行实例进缓存复用（Verify 断言同实例复用 + 容量淘汰）。

### 3.4 Feature DataGrid（大数据与自有网格分工）

* `Arc.Avalonia.Feature.DataGrid`（L2f）：唯一允许 `Avalonia.Controls.DataGrid` 的包；`ArcDataGridHost` 提供 `ItemsSource` + `PageSize/PageIndex/TotalItems/AutoPage/PageRequested` + First/Prev/Next/Last/GoToPage。
* 皮肤：host ControlTheme + 对官方 DataGrid 的表头/行/选中/网格线/行高做 token 覆盖。
* 分工：小数据用自有 `ArcDataGrid`（设计一致）；大数据/虚拟化/分页用 Feature 包（官方实现）。

## 4. 遗留（进 M2 后续）

1. `ArcServiceProvider` 为进程级静态：多窗口时后注册覆盖（XML 文档已注明）；若需要多窗口隔离，后续改为窗口作用域（`ArcWindow.Services` 已有门面，可演进）。
2. `ArcWindow` Custom 标题栏未做 Win32 交互点验（拖动区/系统菜单/高 DPI）。
3. 服务 UI 复用既有组件主题，未新增 Services 主题入口（无需）。
4. 树窗口化的行高为常量 44（与现有 `ArcTreeTable` 一致）；可变行高不在本波范围。
5. BannedApi（Agent 边界）仍在 M2 后续 Agent 波次接入。
