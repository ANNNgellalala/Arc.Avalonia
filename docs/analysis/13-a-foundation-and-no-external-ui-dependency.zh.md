# A. 基础层与"去 Fluent 依赖"审计

> 审计对象：`src/Foundation/Arc.Avalonia`、`src/Foundation/Arc.Avalonia.Internal`，以及全仓库对内建 Avalonia 控件主题（FluentTheme）的实际依赖面。
> 审计方式：只读静态审计（read/grep/glob + 只读 PowerShell 统计 + 本地 NuGet 包与 Avalonia 12.1.4 官方源交叉核对）。**未运行 dotnet build / dotnet run / tools/*.ps1**，因此凡"运行后才可见"的行为均标 `未验证`。
> 统计口径：`src/` 下 28 个项目、排除 `bin/`、`obj/`；行数为物理行数（含空行）；"文件数"按包含该类型标识符的文件计。

---

## 0. 结论摘要（一页）

### 0.1 五个最关键的量化事实

| # | 事实 | 数字 | 证据 |
|---|---|---|---|
| 1 | 基础层体量 | **18 个源文件 / 101.5 KB / 2,294 行**（其中 C# 1,945 行、XAML 349 行），占全仓库 206,968 行的 **1.1%** | `src/Foundation/Arc.Avalonia/**`；全仓统计见 §2.1 |
| 2 | token/图标/动效密度 | **82 个设计 token**、**161 个 Lucide 图标**、**8 种强调色**、**7 条动效曲线**；`ArcPalette` 实际注册 **81/82** 个键（`ControlThumbShadow` 计算后漏注册，见 §2.3） | `Theme/ArcTokens.cs:7-114`、`Icons/ArcIconData.cs`、`Theme/ArcPalette.cs:66-74,142-246`、`Motion/ArcMotion.cs:14-42` |
| 3 | FluentTheme 的标记引用面 | 全仓库只有 **2 处** `<FluentTheme />`（`Gallery/App.axaml:7`、`.recon/Probe/App.axaml:8`）+ **2 处** PackageReference（`Gallery.csproj:12`、`Probe.csproj`），与任务前提一致 | grep 全仓（含 .recon） |
| 4 | 但"去 Fluent"的真实契约面 | **12 项隐式模板/资源契约**（Window、PopupRoot、OverlayPopupHost、ToolTip、TextBox、ScrollViewer、ScrollBar、ListBox、ListBoxItem、FlyoutPresenter、AdornerLayer/FocusAdorner、ThemeVariant 字典）；缺失即"无模板→控件不渲染" | §4；Avalonia 12.1.4 Fluent 主题 80 个 xaml 源文件，540,657 字节，编译后 DLL 716.0 KB |
| 5 | Arc 自身主题化程度 | 已有 **137 个 ControlTheme**（112 个 Arc 自定义类型 + 25 个内建类型 keyed 主题）、**597 个 Style**、118 个 axaml；1189 处 `DynamicResource` vs 19 处 `StaticResource`；**0 处 `ThemeDictionaries`** | grep + 统计；§3.4、§6 |

### 0.2 四个判断

1. **Foundation 足够薄，但不够纯**：82 token + 161 图标 + 6 个控件 + 1 个组合助手的体量说明它确实是"薄基础层"；但（a）硬依赖 `Avalonia.Fonts.Inter`（`Arc.Avalonia.csproj:9`、`ArcPalette.cs:33`）；（b）`ArcResources` 有 5 处 `Application.Current` 静态耦合（`ArcThemeManager.cs:52,60,63,66,69`）；（c）混入了 `ArcVirtualList`（296 行 ListBox 数据原语，且 **明确依赖 Fluent ListBox 模板**，`ArcVirtualList.cs:71-72`）和组件语义的组合工具 `ArcUi`（191 行）。
2. **删掉 `<FluentTheme />` 本身是 0 行改动，代价 100% 在"补模板 + 补宿主契约"**。当前 294 个 catalog 条目、P0 控件与 Gallery 外壳都直接或间接使用了 Fluent 提供的 12 项模板；删除后不是"样式变丑"，而是 **Window 内容不显示 / 弹层不显示 / 文本框与滚动容器不渲染 / 列表空白 / ToolTip 失效**。
3. **自绘比例高（约 48% 的 lookless 控件不走模板）是最大利好**：112 个 `Control` 链控件（79 个直接 override `Render`）已经自己画，112 个 Arc 类型已有自己的 ControlTheme；真正必须重写的内建模板只有 12 项、且大多可以只做"最小模板"（先例：`ArcEmojiPicker.axaml:191-221` 用约 30 行写了一个最小 TextBox ControlTheme）。
4. **"去 Fluent"≠"无其他 UI 依赖"**：即使完成 Fluent 替换，Foundation 仍依赖 `Avalonia.Fonts.Inter`（1,848.5 KB DLL），Gallery 依赖 `Avalonia.Desktop`，SkiaSharp 3.119.4 / HarfBuzzSharp 8.3.1.3 作为渲染与文本整形后端传递依赖。按"主题/控件/平台"三档判定，只有 Fonts.Inter 属于可以也应被消除的"外部 UI 资产"，Desktop/Skia 属于平台实现、应保留（见 §5.4）。

### 0.3 人日结论（详见 §5）

| 口径 | 乐观 | 中性 | 悲观 |
|---|---|---|---|
| **A. 去 Fluent 后保住现状**（294 个条目页 + P0 控件 + Gallery 外壳正确渲染，内建控件只覆盖"当前用到的"） | **12 人日** | **23 人日** | **37 人日** |
| **B. 框架级"无其他 UI 依赖"**（再补齐 Avalonia 12 内建控件主题全集 ≈43 个控件，可对外宣称自带主题） | **30 人日** | **48 人日** | **72 人日** |

> 一句话：**"去 Fluent 依赖 = 23 人日（中性，保现状）；要做到框架级无其他 UI 依赖 = 48 人日（中性）"**。若接受"降级 MVP"（暂不做 ToolTip/默认 Button/非当前控件，仅保证自绘控件与 294 页可看），可压到 **10–12 人日**。

---

## 1. 审计范围、方法与前提修正

### 1.1 范围

| 层 | 项目 | 本次处理 |
|---|---|---|
| 基础层 | `src/Foundation/Arc.Avalonia` | 全量清点 + 纯度判定 |
| 基础层（内部） | `src/Foundation/Arc.Avalonia.Internal` | 全量清点（4 个视觉类 + 模型） |
| 主题装配 | `Gallery/App.axaml` + 各项目 `*Theme.axaml` + `ArcThemeManager` | 装配链与资源解析路径 |
| 依赖面 | 全仓库 118 个 axaml + 645 个 cs | 内建控件使用、`new`、自绘、Fluent 契约 |
| 包依赖 | 28 个 csproj + Gallery/Probe/Tests | 直接/传递依赖 |

### 1.2 任务前提的修正（3 条）

| 任务描述 | 审计结果 | 影响 |
|---|---|---|
| "Foundation/Arc.Avalonia 只有 20 个文件、约 103 KB" | 精确为 **18 个源文件 / 101.5 KB**（含 csproj；不含 bin/obj 生成物）。若把 `bin/obj` 生成物计入是 42 个文件 / 568.8 KB。按 101.5 KB≈103.9 KB 四舍五入，与"约 103 KB"一致 | 体量结论不变，按 18 文件引用 |
| "5 个基础控件" | 实际 **6 个可视化控件**（`ArcBadge`、`ArcButton`、`ArcCard`、`ArcIcon`、`ArcSpinner`、`ArcVirtualList`）+ 组合助手 `ArcUi` + 3 个 ControlTheme | 控件数按 6+1 计 |
| "只有 Gallery App.axaml 与 .recon/Probe/App.axaml 引用 FluentTheme" | **标记层属实**（正好 2 处）。但 Fluent 的运行时依赖远大于标记引用：12 项模板契约是隐式获取的；另外 Linux 上 `StorageProvider` 文件对话框会落到 Fluent 的 `ManagedFileChooser` 主题（3 个文件使用 StorageProvider） | "去 Fluent"工作量不能被 2 处引用低估，见 §4 |

> 补充：`CONVENTIONS.md:207` 声称技术栈是 "Avalonia.Themes.Fluent, Avalonia.Fonts.Inter and SkiaSharp"；`README.md:260`、`docs/03-implementation.zh.md:186` 也把 Fluent 写入"无外部商业 UI 组件库"的依赖清单。SkiaSharp 并无直接 PackageReference，它是 `Avalonia.Desktop → Avalonia.Skia` 的传递依赖（3.119.4；HarfBuzzSharp 8.3.1.3）。

---

## 2. 基础层构成

### 2.1 Foundation/Arc.Avalonia 文件清单（18 文件 / 101.5 KB / 2,294 行）

| 文件 | 行数 | 职责 | 层归属 |
|---|---:|---|---|
| `Theme/ArcTokens.cs` | 116 | 82 个资源键常量（颜色/线/状态/选择/焦点/阴影/数据系列/品牌渐变/字体/字阶/圆角/尺寸/动效） | token 层 |
| `Theme/ArcPalette.cs` | 429 | 由 OKLCH 构建 Light/Dark × 8 accent 的 ResourceDictionary（82 键）；Inter 字体族硬编码 | token 层 |
| `Theme/ArcColor.cs` | 138 | OKLCH/OKLab ↔ sRGB 转换、mix、hex 解析（纯函数，可测试） | token 层 |
| `Theme/ArcTheme.axaml` | 117 | 全局样式：Window、TextBlock、Button TextBlock、10 个类型尺度 class；合并 3 个组件 ControlTheme 字典 | 主题壳 + 组件主题混合 |
| `Theme/ArcThemeManager.cs` | 70 | `Apply(app, scheme, accent)` 运行时换肤；`ArcResources` 静态读取 token | 主题装配 |
| `Themes/ArcButton.axaml` | 139 | `ArcButton` 完整 ControlTheme（变体/尺寸/焦点环/loading/禁用） | 组件主题 |
| `Themes/ArcBadge.axaml` | 54 | `ArcBadge` ControlTheme（5 种 tone） | 组件主题 |
| `Themes/ArcCard.axaml` | 39 | `ArcCard` ControlTheme（muted/interactive） | 组件主题 |
| `Controls/ArcButton.cs` | 129 | `Button` 子类：Variant/Size/Loading/Compact，类名驱动主题 | 基础控件 |
| `Controls/ArcBadge.cs` | 57 | `ContentControl` 子类：Tone/Dot | 基础控件 |
| `Controls/ArcCard.cs` | 38 | `ContentControl` 子类：Interactive/Muted | 基础控件 |
| `Controls/ArcIcon.cs` | 105 | `Control` 自绘：Lucide 24 网格 stroke 图标 + geometry 缓存 | 基础控件（自绘） |
| `Controls/ArcSpinner.cs` | 107 | `Control` 自绘：270° 弧 + DispatcherTimer，ReducedMotion 时静止 | 基础控件（自绘） |
| `Controls/ArcVirtualList.cs` | 296 | `ListBox` 子类：虚拟化、StickToBottom、ScrollState；**依赖 Fluent ListBox 模板** | 基础控件（数据原语） |
| `Controls/ArcUi.cs` | 191 | 代码组合工具：Text/Caption/Body/Heading/Stack/HStack/Wrap/Icon/Surface/Divider/Badge/Button/Pill/Dot/Name | 组合助手 |
| `Icons/ArcIconData.cs` | 178 | 161 条 Lucide path data（20.6 KB） | 资产 |
| `Motion/ArcMotion.cs` | 91 | 7 条 easing（含自研 `ArcSpringEasing` 阻尼振子）+ 5 个时长 | 动效 |
| `Arc.Avalonia.csproj` | 11 | 仅 `Avalonia` + `Avalonia.Fonts.Inter`；**无任何 ProjectReference** | 工程 |

**分层占比**：token/主题/图标/动效 = 1,371 行（59.8%）；控件与组合助手 = 923 行（40.2%），其中 `ArcVirtualList` 一家占 12.9%、三个模板控件（Badge/Button/Card 及其 3 个主题）合计 456 行（19.9%）。

### 2.2 Arc.Avalonia.Internal（10 文件 / 52.0 KB / 1,662 行）

| 文件 | 行数 | 说明 |
|---|---:|---|
| `Composition/ArcActionVisuals.cs` | 476 | 含 `ArcGlide`（`Border` 子类，共享高亮滑块）+ 动作类视觉工具 |
| `Composition/FeedbackUi.cs` | 309 | 反馈层 UI 工具；`:129` 注释"local values keep Fluent chrome out of the Arc surface"；含 `new TextBox` 工厂 |
| `Controls/ArcActionModels.cs` | 223 | `ArcMenuItem` 等模型/枚举 |
| `Controls/ArcIconButton.cs` | 185 | `ArcIconButton` / `ArcFlatButton`（`Button` 子类，自有 ControlTheme）；`:112` 使用 `ToolTip.SetTip` |
| `Controls/ArcMenuSurface.cs` | 302 | `TemplatedControl` 菜单面板；**刻意不自建 Popup**，"hosts itself inside its owner's overlay canvas, so it renders in the gallery without a popup window"（`:16-20`） |
| `Themes/ArcIconButton.axaml` | 119 | 两个按钮主题（含焦点环） |
| `Themes/ArcMenuSurface.axaml` | 30 | 菜单面板主题（自带 ArcGlide + StackPanel） |
| `Themes/ArcInternalTheme.axaml` | 11 | 合并上述 2 个字典 |
| `AssemblyInfo.cs` | 7 | `InternalsVisibleTo` 5 个组件项目（Actions/Navigation/Overlays/Feedback/DataDisplay） |
| csproj | 11 | Avalonia + ProjectReference(`Arc.Avalonia`) |

### 2.3 "够薄、够纯"判定

| 维度 | 结论 | 证据 |
|---|---|---|
| 够薄 | ✅ 是。18 文件 / 101.5 KB / 2,294 行只占全仓 1.1%；无页面、无 Demo、无业务代码；无 ProjectReference | §2.1；`Arc.Avalonia.csproj:7-10` |
| 依赖纯净（只 Avalonia） | ❌ 否。硬依赖 `Avalonia.Fonts.Inter`（DLL 1,848.5 KB） | `Arc.Avalonia.csproj:9`；`ArcPalette.cs:33,203-204`；Gallery `Program.cs:13` `.WithInterFont()` |
| Application 耦合 | ⚠️ 部分。`ArcThemeManager.Apply(Application app, …)` 显式注入（可测）；但 `ArcResources` 用 5 处 `Application.Current` 兜底（不可测/多窗口/设计器失效） | `ArcThemeManager.cs:24,29,34,37`；`:52,60,63,66,69` |
| 主题与组件分层 | ⚠️ 混。`ArcTheme.axaml` 同时含全局 Window/TextBlock 样式与 3 个组件 ControlTheme 的 ResourceInclude | `ArcTheme.axaml:7-12,14-30` |
| 是否混入组件语义 | ⚠️ 是。`ArcVirtualList` 是数据展示原语（ListBox 子类）；`ArcUi` 是卡片/胶囊/按钮级组合工具；3 个 ControlTheme 属组件层 | `ArcVirtualList.cs:40`、`ArcUi.cs:17` |
| 是否已 Fluent-free | ❌ 否。`ArcVirtualList` 通过 `StyleKeyOverride => typeof(ListBox)` 显式复用 Fluent ListBox 主题 | `ArcVirtualList.cs:71-72`；`ArcVirtualList.cs:65` 注释"virtualisation never depends on the active theme's ListBox template" |
| 无障碍基础 | ⚠️ 部分。自绘控件有 `AutomationProperties.Name` 与焦点环（`:focus-visible`），但没有统一 FocusAdorner/Validation 基础设施 | `ArcVirtualList.cs:68,148-151,288-293`；`ArcButton.axaml:122-125` |
| 可换肤性 | ⚠️ 运行时调色板替换可行，但不是 Avalonia 的 ThemeDictionaries 机制；`ThemeDictionaries` 全仓 0 处 | `ArcThemeManager.cs:31-37`；grep `ThemeDictionaries` = 0 |
| token 一致性 | ❌ 有 1 个缺口：`ArcTokens.ControlThumbShadow` 已声明（`ArcTokens.cs:45`）、`controlThumbShadow` 也计算了（`ArcPalette.cs:66-74`），但**从未写入资源字典**（palette 共 81 个写入）；同时它被 3 个 Glass 控件 + `ArcToggle.axaml` 通过资源键消费（无 producer） | `ArcTokens.cs:45`；`ArcPalette.cs:66-74`（变量未使用）；消费点：`ArcToggle.axaml`、`ArcControlCenter.cs`、`ArcRubberMeter.cs`、`ArcSheetStack.cs` |

**判定：薄 ✅ / 纯 ❌。** 它已经是"tokens + 动效 + 图标 + 少量基础控件"的薄基础层，但要成为"无外部 UI 依赖"的框架底座，需解决 3 件事：去掉 Fonts.Inter 硬依赖、去掉 `Application.Current` 静态兜底、把 `ArcVirtualList`（及 `ArcUi` 的组件语义）从 foundation 中解耦或明确降级为"依赖主题契约的基础控件"。

### 2.4 主题装配链

```
Gallery/App.axaml:6-33
  ├─ :7  <FluentTheme />                     ← 唯一内建主题来源（被审计目标）
  ├─ :8  ArcTheme.axaml                      ← Foundation 全局样式 + Badge/Button/Card 3 个字典
  ├─ :9  ArcInternalTheme.axaml              ← Internal 2 个字典
  └─ :10-32  25 个 StyleInclude              ← 各项目 *Theme.axaml（26 个主题文件，其中 13 个是 ≤15 行的空壳）

运行时：App.OnFrameworkInitializationCompleted (App.axaml.cs)
  → ArcThemeManager.Apply(this, Light, Neutral)          ArcThemeManager.cs:24-38
      → app.RequestedThemeVariant = Light/Dark            :29
      → ArcPalette.Build(scheme, accent) → 82 个资源      ArcPalette.cs:35-248
      → app.Resources.MergedDictionaries.Add(palette)     :31-37
  → 组件通过 DynamicResource / ArcResources 读取 token    （1189 处 DynamicResource）
```

关键观察：

- 资源解析路径是 **Arc 自己的资源字典 + DynamicResource**，不是 Fluent 的资源键。全仓 grep Fluent 资源键（`SystemControl*`、`TextControl*`、`SystemAccentColor` 等）**命中 0**。这意味着：删掉 Fluent 不会造成"Arc 控件引用不到资源"，只会造成"内建控件没有模板"。
- 变体机制：`RequestedThemeVariant` 仅由 `ArcThemeManager.cs:29` 设置；`ActualThemeVariant` 只在 2 个自绘控件里用于判断暗色（`ArcScrollArea.cs`、`ArcImageCompare.cs`）。`ThemeDictionaries` 全仓 0 处，说明 Arc 的颜色切换靠"运行时替换整个字典"，而非 Fluent/Simple 的变体字典。自建主题必须补上 variants，否则内建控件无法跟随浅/深色。

---

## 3. 全仓库对内建控件与 Fluent 的依赖面

### 3.1 编译期依赖（28 个项目）

| 依赖 | 直接引用项目 | 性质 | 去留判断 |
|---|---|---|---|
| `Avalonia` 12.1.4 | 28/28 | 框架本体（MIT） | **必须保留**（任务明确） |
| `Avalonia.Fonts.Inter` 12.1.4 | Foundation(`:9`)、Gallery(`:13`) | 字体资产（DLL 1,848.5 KB） | **消除或资产化**（自带 Inter 资源 / 改系统字体） |
| `Avalonia.Themes.Fluent` 12.1.4 | Gallery(`:12`)、`.recon/Probe` | 内建主题（80 个 xaml、716 KB DLL） | **目标删除** |
| `Avalonia.Desktop` 12.1.4 | Gallery(`:11`) | 平台实现（Win32/X11/macOS） | 保留（app 层，非主题） |
| `SkiaSharp` 3.119.4 | 传递（`Avalonia.Skia`） | 2D 渲染后端 | 保留（不是 UI 框架） |
| `HarfBuzzSharp` 8.3.1.3 | 传递（`Avalonia.Skia`） | 文本整形 | 保留 |
| 测试 | 仅 `Foundation` + `Demo` ProjectReference | 控制台断言（23 个 Check），无渲染测试 | 见 §7 |

> 结论：库侧（26 个非 app 项目）除 Avalonia 外只有 1 个直接外部依赖——Foundation 的 Fonts.Inter。Fluent 只存在于 app 层。这是一次"低风险裁剪"的结构前提。

### 3.2 内建控件使用统计（按类型）

统计口径：axaml 用元素匹配（`<Type`），cs 用单词边界匹配（排除 `Arc*` 前缀），`new` 为构造调用次数。

| 内建类型 | axaml 文件/元素 | cs 文件/引用 | `new` | 是否依赖 Fluent 模板 | 判定 |
|---|---:|---:|---:|---|---|
| `TextBox` | 12 / 18 | 31 / 93 | 0 | **是**（Fluent `TextBox.xaml` 18,307 B；仅 1 个实例有自定义 Theme，17 个没有） | **必然坏** |
| `ScrollViewer` | 10 / 10 | 28 / 55 | 0 | **是**（`ScrollViewer.xaml` 3,858 B） | **必然坏** |
| `ScrollBar` | 0 / 0 | 2 / 4 | 0 | **是**（经 ScrollViewer 模板；`ScrollBar.xaml` 18,384 B） | **必然坏** |
| `ListBox` | 3 / 7 | 10 / 20 | 0 | **是**（`ListBox.xaml` 2,972 B；`ArcVirtualList.cs:71-72` 明示） | **必然坏** |
| `ListBoxItem` | 0 / 0 | 4 / 12 | 0 | **是**（经 ListBox；`ListBoxItem.xaml` 4,014 B） | **必然坏** |
| `Popup` | 7 / 9 | 28 / — | 0 | **是**（`PopupRoot.xaml` 1,639 B 提供 VisualLayerManager + ContentPresenter） | **必然坏** |
| `ToolTip` | 0 / 0 | 29 / 42 | 0 | **是**（`ToolTip.xaml` 2,749 B） | **必然坏** |
| `Flyout` | 0 / 0 | 1 处代码 | 1（`ArcContextWindow.cs:214`） | **是**（`FlyoutPresenter.xaml` 2,755 B） | **必然坏** |
| `Window` | 0 / 0 | 1 个类 | 0（GalleryWindow 继承；ShotRunner `new Window`） | **是**（`Window.xaml` 1,835 B，含 VisualLayerManager；`Window.cs` 无默认模板） | **必然坏** |
| `ItemsControl` | 1 / 1 | 1 / 1 | 0 | 是（ListBox 的基类链/模板） | **必然坏（间接）** |
| `Button` | 18 / 30 | 67 / 265 | 0 | 否——30/30 元素都带 `Theme=`（13 个 keyed Button 主题） | 当前不受影响 |
| `RadioButton` | 0 / 0 | 3 / 6 | 3（均 `Theme=ArcRadioButtonTheme`） | 否（当前） | 当前不受影响 |
| `ContentControl` | 2 / 2 | 95 / 182 | **149** | 否——`ContentControl.cs` 静态构造函数给 Template 提供默认 `ContentPresenter` | 不受影响（重要反例） |
| `CheckBox` / `ProgressBar` / `Expander` / `TreeView` / `TabControl` / `SplitView` / `ComboBox` / `ComboBoxItem` / `Slider` / `ToggleSwitch` / `Calendar` / `AutoCompleteBox` / `NumericUpDown` / `DatePicker` / `TimePicker` / `ContextMenu` / `MenuItem` / `Menu` / `DataGrid` / `ColorPicker` | 0 | 仅词形/类名误命中（如 `ArcSlider`、`ArcCalendar`、`ArcContextMenu`），无 `new`、无 axaml 元素 | 0 | —（当前未使用） | **不受影响** |

补充说明：

- **`ContentControl` 不需要主题**（Avalonia 内置默认模板），所以 149 处 `new ContentControl` 的代码组合模式是安全的；这也解释了为什么 13 个项目的 `*Theme.axaml` 是空壳——这些项目的控件完全由代码组合。
- **`Button` 当前全部显式主题化**：`<Button` 30 处全部带 `Theme=`，`new Button` 为 0；默认 Fluent Button 主题在当前仓库不构成"必然坏"，但作为框架应补（约 0.5–1 人日）。
- **`TextBox` 是最大单点**：18 个实例中只有 1 个有自定义 Theme（EmojiPicker 搜索框），其余 17 个（分布 10 个主题文件）靠 Fluent 模板提供行内编辑、水印、滚动、IME、选择/光标。`ArcTextField.axaml:42,144,233,357` 只是把外框/焦点环剥掉，内部仍是 Fluent。
- 大量内建类型"被引用"只是类名/注释误命中（如 `Calendar` 32 个文件其实都是 `ArcCalendar` 系列），因此本表只把有 axaml 元素、构造调用或继承的当真实依赖。

### 3.3 C# 构造与自绘

| 指标 | 数值 | 说明 |
|---|---:|---|
| `new Button/TextBox/ListBox/Popup/Slider/Window/ScrollViewer/ListBoxItem` | 0 | 组件库里没有直接 `new` 内建交互控件（除 `new Window` 在截图夹具、`new Flyout` 1 处、`new RadioButton` 3 处） |
| `new ContentControl(...)` | 149 | 代码组合容器，安全（内置默认模板） |
| `override void Render(DrawingContext)` 的文件 | 78 | 全部位于自绘控件/图表/特殊组件 |
| 使用 `DrawingContext` 的文件 | 122 | 自绘是主要渲染手段 |
| `AffectsRender` 的文件 | 47 | 自绘控件标准做法 |
| `StyleKeyOverride` 的文件 | 6 | 复用内建主题的只有 `ArcVirtualList`（ListBox）等少数 |
| `new Flyout` | 1（`ArcContextWindow.cs:214`） | 唯一的 Flyout 依赖 |

### 3.4 Fluent 主题包量化（用于估算"要补什么"）

本地 NuGet：`D:\.nuget\packages\avalonia.themes.fluent\12.1.4`

| 指标 | Fluent | Simple（参照） |
|---|---|---|
| nupkg | 266.9 KB | 178.8 KB（12.1.0） |
| DLL | 716.0 KB | 405.0 KB（12.1.0） |
| 依赖 | 仅 `Avalonia` 12.1.4 | 仅 `Avalonia` 12.1.0 |
| 主题源文件 | **80 个 xaml / 540,657 B**（GitHub 12.1.4 `src/Avalonia.Themes.Fluent/Controls`） | — |

这 80 个 xaml 中与 Arc 当前渲染相关的关键文件（字节数即"重写工作量"的粗略基准）：

| 契约 | Fluent 源文件 | 字节 | Arc 侧现状 |
|---|---|---:|---|
| Window | `Window.xaml` | 1,835 | 0 自定义 Window 主题；依赖 Fluent 模板提供 `VisualLayerManager` + `ContentPresenter` |
| Popup 宿主 | `PopupRoot.xaml` | 1,639 | 0；7 个 axaml 模板内嵌 `<Popup>` |
| 弹层备用宿主 | `OverlayPopupHost.xaml` | 1,393 | 0 |
| 焦点层 | `AdornerLayer.xaml` | 1,097 | 0（Arc 自绘控件自带焦点环，16 处 `FocusAdorner="{x:Null}"`） |
| ToolTip | `ToolTip.xaml` | 2,749 | 0；29 个文件用 ToolTip API |
| TextBox | `TextBox.xaml` | 18,307 | 1 个自定义 TextBox 主题（30 行最小版，`ArcEmojiPicker.axaml:191-221`） |
| ScrollViewer | `ScrollViewer.xaml` | 3,858 | 0 |
| ScrollBar | `ScrollBar.xaml` | 18,384 | 0 |
| ListBox | `ListBox.xaml` | 2,972 | 0 默认主题；`ArcVirtualList` 复用 Fluent |
| ListBoxItem | `ListBoxItem.xaml` | 4,014 | 有 6 个 keyed ListBoxItem 主题（Popups/Emoji/Time），默认仍靠 Fluent |
| Flyout | `FlyoutPresenter.xaml` | 2,755 | 0 |
| 验证 | `DataValidationErrors.xaml` | 5,171 | 未使用（0 处），但框架应补 |
| 变体 | `ThemeVariantScope.xaml` | 431 | 未使用；Arc 依赖运行时换字典 |
| 平台窗口装饰 | `WindowDrawnDecorations.xaml` | 11,521 | 未使用（Gallery 用 OS 原生装饰） |
| 文件对话框 | `ManagedFileChooser.xaml` | 22,678 | 3 个文件用 `StorageProvider`（Linux 会命中；Windows 原生） |

---

## 4. 删掉 `<FluentTheme />` 会发生什么（三档清单）

### 4.1 必然坏（无模板 → 控件不渲染 / 宿主层缺失）

| # | 控件/契约 | Arc 侧使用证据 | 缺失后的具体后果 | 自建复杂度 |
|---|---|---|---|---|
| 1 | **Window** | `GalleryWindow.cs:23` 唯一 `: Window`；截图夹具 `ShotRunner.cs` `new Window` | Window 无 Template → 内容不显示；且作为宿主，其 `VisualLayerManager` 缺失会连带 ToolTip/Popup/Adorner 层不可用 | 中（1 个模板 + token 映射） |
| 2 | **PopupRoot** | 7 个 axaml 内嵌 `<Popup>`（`ArcSelect.axaml:72,223,348`、`ArcDataGrid`、`ArcReactions`、`ArcColorPicker`、`ArcDatePicker`、`ArcDateRangePicker`、`ArcTimePicker`） | 所有下拉/选择器/颜色弹层内容不可见（不是样式坏，是空白） | 中 |
| 3 | **OverlayPopupHost** | 同上（轻量弹层宿主，取决于平台） | 部分平台的 Popup 走 overlay host，同样空白 | 低-中 |
| 4 | **ToolTip** | 29 个文件 / 42 处 `ToolTip.Tip` / `ToolTip.SetTip`（如 `ArcIconButton.cs:112`、`GalleryWindow.cs:580`） | 悬浮提示全部消失（无模板）；`ArcIconButton` 的可访问性退化 | 低 |
| 5 | **TextBox** | 18 个 axaml 实例（17 个无 Theme）+ 31 个 cs 文件引用；`ArcTextField.axaml:42,144,233,357`、`ArcSelect.axaml:183`、`GalleryWindow.cs:195` | 文本输入框不渲染/不可输入；Gallery 搜索框消失；所有字段控件（ArcTextField/Password/Phone/Mention/InlineEdit…）内部编辑区崩掉 | **高**（IME、水印、选择、滚动、双向绑定） |
| 6 | **ScrollViewer** | 10 个 axaml + 28 个 cs；Gallery 主内容区 `GalleryWindow.cs:120,134`、侧栏 `:236` | 内容不可见/不可滚动；Gallery 文档正文区消失 | 中 |
| 7 | **ScrollBar** | ScrollViewer 模板内（2 个 cs 文件显式提及） | 滚动条消失（若自己画 overlay 尚可用滚轮，但可拖拽/可访问性丢失） | 中（含悬停/拖拽/触屏） |
| 8 | **ListBox** | 3 个 axaml / 7 个元素（`ArcSelect.axaml:107,232,356` 等）；`ArcVirtualList.cs:40,71-72` | 虚拟列表/弹层列表内容不可见（无 ItemsPresenter/ScrollViewer） | 中 |
| 9 | **ListBoxItem** | 经 ListBox（4 个 cs 文件操作容器） | 列表行无模板（Arc 已有 6 个 keyed 行主题可复用，但默认容器仍空） | 低-中 |
| 10 | **FlyoutPresenter** | `ArcContextWindow.cs:214-215` `new Flyout` | 该模型菜单无法显示 | 低 |
| 11 | **AdornerLayer / FocusAdorner** | 16 处显式 `FocusAdorner="{x:Null}"`（Arc 自绘环），但内建控件仍有依赖 | 键盘焦点可视化退化/消失（无障碍问题），验证错误装饰无处绘制 | 低-中 |
| 12 | **ThemeVariant 系统色字典** | Arc 0 处 `ThemeDictionaries`；`RequestedThemeVariant` 仅 `ArcThemeManager.cs:29` | 内建控件无 Light/Dark 颜色；即使补了模板，暗色主题会错色 | 中 |

> 注意：**"有自定义 ControlTheme"≠"不依赖 Fluent"**。例如 `ArcSelect`/`ArcEmojiPicker`/`ArcDataGrid` 都有自己的 ControlTheme，但模板内部嵌入了 `<TextBox>`/`<ScrollViewer>`/`<Popup>`，这些内建部件的模板仍来自 Fluent。受此牵连的自定义控件至少覆盖 12 个 axaml（TextBox） + 10 个（ScrollViewer） + 7 个（Popup）。

### 4.2 可能坏（当前少用/条件触发/无障碍降级）

| # | 项 | 证据 | 条件 | 估算 |
|---|---|---|---|---|
| 1 | `ItemsControl` 默认主题 | 1 个 axaml；ListBox 基类 | ListBox 主题若直接内联 `ItemsPresenter` 可不单独做 | 0.5 d |
| 2 | `RadioButton` 默认主题 | 3 个 cs 文件 `new RadioButton`，当前全部 `Theme=` | 未来出现无 Theme 的 RadioButton 即坏 | 0.5 d |
| 3 | `TextSelectionHandle` | TextBox 触屏选择 | 触屏/笔输入场景 | 0.5 d |
| 4 | `DataValidationErrors` / Validation | 全仓 0 处 | 引入 `INotifyDataErrorInfo` 后 | 0.5–1 d |
| 5 | `ThemeVariantScope` | 0 处 | 局部主题切换/嵌套 | 0.25 d |
| 6 | `ManagedFileChooser` | 3 个文件用 `StorageProvider`（`ArcAvatarUpload.cs`、`ArcFileDropzone.cs`、`ArcChatThread.cs`） | **Linux** 上打开文件对话框；Windows 原生不涉及 | 1 d（或判为"平台能力"不自建） |

### 4.3 不受影响（自绘/代码组合/非模板元素）

| 类别 | 数量 | 说明 |
|---|---:|---|
| 非模板控件（`Control` 链，自绘或以代码组合子元素） | **112 类** | `ArcChartBase` 20、`DataStage` 15、`ArcExplainer` 11、`ArcMotionCanvas` 11、`ArcSpecialCanvas` 11、直接 `: Control` 44；79 个类的文件直接 override `Render` |
| 已有完全自有 ControlTheme 的 Arc 控件 | **112 个自定义类型** | 137 个 ControlTheme − 25 个内建类型 keyed 主题 |
| 非模板基础元素 | 324+ 处 axaml 使用 | `Border` 223、`TextBlock` 162、`Panel` 130、`Grid` 82、`StackPanel` 139、`WrapPanel` 6、`Image` 2、`Path` 4、`Ellipse` 2（这些不依赖 ControlTheme） |
| 页面 | **294 个 ArcItemPage** | 全部用 Arc 控件/基础元素搭建 |
| 当前未使用的内建控件 | ≈ 40 类 | CheckBox/ComboBox/Slider/ToggleSwitch/ProgressBar/Expander/TreeView/TabControl/SplitView/Menu/ContextMenu/DatePicker/Calendar/DataGrid/ColorPicker 等（0 axaml 元素、0 构造） |

---

## 5. 自建 ControlTheme 工作量估算

### 5.1 分档明细（控件数 × 难度 × 人日）

**Tier 1 必然坏（12 项）**

| 项 | 难度 | 乐观 | 中性 | 悲观 |
|---|---|---:|---:|---:|
| Window（含 VisualLayerManager/ContentPresenter/token 映射） | 中 | 0.5 | 1.0 | 2.0 |
| PopupRoot | 中 | 0.5 | 1.0 | 1.5 |
| OverlayPopupHost | 低-中 | 0.25 | 0.5 | 1.0 |
| ToolTip | 低 | 0.25 | 0.5 | 1.0 |
| AdornerLayer + FocusAdorner | 中 | 0.5 | 1.0 | 1.5 |
| **TextBox**（IME/水印/选择/滚动/TextPresenter 契约） | 高 | 2.0 | 3.0 | 4.5 |
| ScrollViewer | 中 | 1.0 | 2.0 | 3.0 |
| ScrollBar（悬停/拖拽/触屏） | 中 | 0.75 | 1.25 | 2.0 |
| ListBox | 中 | 1.0 | 1.5 | 2.5 |
| ListBoxItem | 低-中 | 0.5 | 1.0 | 1.5 |
| FlyoutPresenter | 低 | 0.25 | 0.75 | 1.5 |
| ThemeVariant 字典/系统色（Light+Dark 两套） | 中 | 1.0 | 1.5 | 2.5 |
| **小计** | | **8.5** | **15.5** | **24.0** |

**Tier 2 可能坏（6 项）**

| 项 | 乐观 | 中性 | 悲观 |
|---|---:|---:|---:|
| ItemsControl 默认主题 | 0.25 | 0.5 | 1.0 |
| RadioButton 默认主题 | 0.25 | 0.5 | 1.0 |
| TextSelectionHandle | 0.25 | 0.5 | 1.0 |
| Validation/DataValidationErrors | 0.5 | 0.75 | 1.5 |
| ThemeVariantScope | 0.1 | 0.25 | 0.5 |
| ManagedFileChooser（Linux，可选） | 0.5 | 1.0 | 2.0 |
| **小计** | **1.85** | **3.5** | **7.0** |

**Tier 3 框架完整性（当前不受影响，但"通用 UI 框架"必须覆盖；≈43 个内建控件）**

| 族 | 控件数（约） | 主要项 | 乐观 | 中性 | 悲观 |
|---|---:|---|---:|---:|---:|
| 选择/状态 | 6 | CheckBox、ToggleButton、ToggleSwitch、ProgressBar、Slider、RepeatButton | 3.0 | 4.5 | 6.0 |
| 容器/导航 | 12 | Expander、TreeView(+Item)、TabControl(+TabItem)、TabStrip(+Item)、SplitView、GridSplitter、Carousel、RefreshContainer/Visualizer、TransitioningContentControl | 6.0 | 8.0 | 11.0 |
| 输入/选择器 | 12 | ComboBox(+Item)、AutoCompleteBox、NumericUpDown、Calendar(+Button/Day/Item)、DatePicker、TimePicker、ButtonSpinner | 5.0 | 7.0 | 10.0 |
| 命令/菜单 | 8 | Menu、MenuItem、ContextMenu、MenuFlyoutPresenter、SplitButton、DropDownButton、ToggleSplitButton、HyperlinkButton | 2.5 | 3.5 | 5.0 |
| 文本/杂项 | 5 | SelectableTextBlock、PathIcon、Label、Separator、GroupBox/HeaderedContentControl | 1.5 | 2.0 | 3.0 |
| **小计** | **≈43** | | **18.0** | **25.0** | **35.0** |

**Tier 4 跨切面（回归基线/无障碍/文档）**：乐观 2 / 中性 4 / 悲观 6 人日（截图基线扩充、双主题逐页核对、焦点/Tab 顺序抽查、README/CONVENTIONS 更新）。

### 5.2 "去 Fluent 依赖"结论表

| 口径 | 乐观 | 中性 | 悲观 |
|---|---:|---:|---:|
| **A. 保现状** = Tier1 + Tier2 + Tier4 | **12** | **23** | **37** |
| **B. 框架级** = A + Tier3（含双主题/无障碍打磨已计入 Tier4） | **30** | **48** | **72** |

**建议的落地顺序（按 ROI）**：

1. **1–2 天**：补 Window + PopupRoot + OverlayPopupHost（否则 Gallery 内容与所有弹层不可见），临时用最小模板验证"能渲染"。
2. **3–4 天**：ScrollViewer + ScrollBar + ListBox/ListBoxItem（复用 `ArcVirtualList` 与 6 个已有 ListBoxItem keyed 主题）。
3. **3–5 天**：TextBox（复用 `ArcEmojiSearchBox` 30 行最小模板；补 PART_TextPresenter/PART_ScrollViewer/水印/选择）。
4. **2–3 天**：ToolTip + AdornerLayer/FocusAdorner + ThemeVariant 字典。
5. **2–3 天**：FlyoutPresenter、RadioButton/ItemsControl 默认、跨页截图回归。
6. **1–2 周**：Tier3 内建控件全集（只在要对外宣称"框架"时做）。

### 5.3 可复用资产（把估算压低的依据）

| 资产 | 数量/位置 | 价值 |
|---|---|---|
| 已有自定义 ControlTheme | 137 个（112 个 Arc 类型 + 25 个内建 keyed） | 大部分组件不用重做；只需重做其模板内嵌的内建部件 |
| 已有最小 TextBox 先例 | `ArcEmojiPicker.axaml:191-221`（约 30 行） | TextBox 估算 2–4.5 d 的上限可信 |
| 已有 ListBoxItem 主题 | 6 个（`ArcPrimitives.axaml:255,296`、`ArcEmojiPicker.axaml:96,114,149`、`ArcTimePicker.axaml:6`） | ListBoxItem 基本可复用 |
| 已有 Button 主题 | 13 个 keyed Button（`ArcPrimitives.axaml:7,65,131,187`；`ArcAdvancedShared.axaml:7,62`；Pickers 7 个） | 默认 Button 主题可从这些抽取 |
| 已有焦点环模式 | 35+ 处 `:focus-visible`（`ArcButton.axaml:122-125` 等） | FocusAdorner 只需做系统层 |
| 第三方参照 | 本地 `Avalonia.Themes.Simple` 12.1.0（405 KB DLL，覆盖全部内建控件） | 可直接对照其"最小主题"规模；比 Fluent 小 311 KB，说明精简主题可做到 400 KB 级 |
| 截图基线 | `docs/images/` 100+ PNG；`shots/` 目录 | 视觉回归有起点，但无自动 diff |

### 5.4 不能 / 不建议自建清单

| 项 | 理由 | 建议 |
|---|---|---|
| `Avalonia.Desktop` / Win32/X11/macOS 平台实现、SkiaSharp、HarfBuzz | 这是 UI 框架的"平台 + 渲染 + 文本整形"底座，不属于"其他 UI 依赖"；重写等于重做 Avalonia | 保留，按"平台/渲染三档"排除在计数外（见本表后两行及 §3.1） |
| 原生菜单/托盘/窗口装饰（NativeMenu、TrayIcon、Win32 标题栏、Mica/圆角） | 平台能力，主题无法替代；当前 Gallery 依赖 OS 原生装饰（`GalleryWindow` 未使用 `ExtendClientArea`） | 保留平台实现；将来要自绘标题栏再评估 `WindowDrawnDecorations.xaml`（11,521 B） |
| 文件对话框（ManagedFileChooser） | Windows 走原生；Linux 的托管对话框属于 Avalonia 内置平台 UI | 定义"平台对话框"豁免；否则把 1 人日列进 Tier2 |
| 完整 DataGrid / ColorPicker / RichText 排版 | 当前 0 使用；且 DataGrid/ColorPicker 是独立 NuGet 包（`Avalonia.Controls.DataGrid/ColorPicker`） | 不纳入基础主题；需要时作为可选包并明确标注"外部控件包" |
| 字体渲染/文本整形 | 重写不现实 | 把 Inter 字体文件作为 Arc 自有 avares 资产打包（替换 `ArcPalette.cs:33` 的 `avares://Avalonia.Fonts.Inter/...`），或改用系统字体并接受字阶变化（0.5–1 d） |
| 重做 Fluent/Simple 整套视觉 | 与"Arc 设计语言"目标冲突，且成本 > 手工 40 个控件 | 只做"最小可渲染模板 + Arc token 映射"，视觉细化靠既有 112 个自绘控件 |

---

## 6. Arc 自绘 / 模板比例（中间层能否替代通用框架的关键数据）

### 6.1 控件分类（`Controls/` 目录内 301 个类）

| 类别 | 类数 | 判定依据 | 是否依赖 ControlTheme 模板 |
|---|---:|---|---|
| `TemplatedControl` 链 | **92** | 直接 `: TemplatedControl` 76 + 经 `ArcFieldBase` 8 + `ArcTextFieldBase` 4 + `ArcPickerBase` 4 | 是（已有 112 个自定义 ControlTheme 覆盖） |
| `ContentControl` 链 | **18** | 直接 14 + `ArcBadge` 2 + `ArcCard` 2 | 是（ContentControl 有内置默认模板，但这些有自有主题） |
| `Button` 链 | **12** | 直接 `: Button` 9 + `ArcButton` 3 | 是（全部有自有/keyed 主题） |
| `ListBox` 链 | **1** | `ArcVirtualList` | **是，且复用 Fluent ListBox 主题** |
| `Control` 链（自绘/代码组合） | **112** | `ArcChartBase` 20 + `DataStage` 15 + `ArcExplainer` 11 + `ArcMotionCanvas` 11 + `ArcSpecialCanvas` 11 + 直接 `: Control` 44；其中 79 个类所在文件 override `Render` | 否（自己绘制/组合子元素） |
| `Panel` 组合类 | 37 | 布局辅助 | 否 |
| `Border`/`Decorator`/`StackPanel`/`TextBlock` 子类 | 5 | 辅助 | 否 |
| 非视觉类（EventArgs/IDisposable/INotifyPropertyChanged/`ArcMorphSelectItem`） | 25 | 模型/事件 | — |
| **合计** | **301** | | |

### 6.2 比例

- **非模板控件 : 模板控件 = 112 : 123 ≈ 47.7% : 52.3%**（不含 37 个 Panel 组合类与 5 个装饰辅助类）。
- 也就是说，**约一半的"真正控件"不经过 ControlTheme 模板**，而是直接 `Render` 或在代码里组合 `Border/StackPanel/Grid/ContentControl`。这是 Arc 比"通用 UI 框架"更容易去 Fluent 的根本原因：它本来就不太依赖主题系统。
- 但这也是它对标通用框架的**短板**：通用框架的用户期望"能换主题、能改 ControlTheme、能用 Style 扩展"，而 Arc 的 112 个自绘控件把外观写进了 C#（`DrawingContext`），只有约 123 个模板化组件提供 Style/ControlTheme 扩展点（其中 112 个已有自定义主题）；且全仓 13 个主题文件是空壳（10 行），说明"主题层"目前更像"资源壳"而非"可覆盖的样式系统"。
- 模板化程度较高的项目：`Inputs`（20 个 axaml / 40 ControlTheme）、`Pickers`（14/22）、`Feedback`（14/13）、`Actions`（10/16）、`Effects`（10/9）、`Media`（9/11）、`DataDisplay`（8/7）。这 7 个项目是"去 Fluent"的主战场；其余 17 个项目（Agent 2 + Special 5 + Blocks 6 + Charts/Content/Navigation/Overlays 4）大量使用自绘/代码组合，去 Fluent 对它们的影响主要是"宿主/基础控件"层面。

---

## 7. 最大不确定性来源（5 个）

| # | 不确定性 | 为什么不可静态消除 | 影响 |
|---|---|---|---|
| 1 | **Popup/Overlay/Adorner 宿主契约** | `PopupRoot.xaml`、`OverlayPopupHost.xaml`、`AdornerLayer.xaml` 提供的是控件树宿主；弹层的 light-dismiss、焦点陷阱、顶层重定向、DPI 缩放、`TopLevelHost` 行为只在运行时可见。Avalonia 12 新引入 `TopLevelHost`/`WindowDrawnDecorations`，版本行为可能变化 | 直接影响 7 个弹层模板与 29 个 ToolTip 文件；若宿主层出错，可能是"弹层能显示但定位/关闭/键盘异常" |
| 2 | **视觉回归无自动化** | `tests/Arc.Avalonia.Tests/Program.cs` 只有 23 个 Check，覆盖 OKLCH/动效/目录，无任何渲染断言；`docs/images/` 与 `shots/` 是人工基线，无像素 diff。1180px 宽度、双主题、字阶与阴影都靠肉眼 | 估时中 Tier4 的 2–6 人日可能低估；暗色主题下 112 个自绘控件的颜色回归无法用测试兜底 |
| 3 | **TextBox/IME/触屏选择** | 17 个无 Theme 的 TextBox 依赖 Fluent 的 `PART_TextPresenter`/`PART_ScrollViewer`/水印/IME 组合；Windows 11 输入法组合窗口、触摸选择手柄、`TextSelectionHandle` 都需要实机 | 是 Tier1 中单项最大的 2–4.5 人日，也是最可能超支的一项 |
| 4 | **Window/平台装饰差异** | 当前未使用 `ExtendClientArea`，依赖 OS 原生标题栏（Windows 11 圆角/Mica、Linux WM、macOS traffic lights 行为不同）；自绘标题栏的 `WindowDrawnDecorations.xaml` 11.5 KB 未评估 | 若目标平台只有 Windows，风险低；若要对标跨平台框架，窗口装饰是隐藏的 2–5 人日 |
| 5 | **ThemeVariant 与既有 token 语义** | Arc 用"运行时整字典替换 + `RequestedThemeVariant`"（`ThemeDictionaries` 0 处），而内建控件主题需要 Light/Dark 两套资源字典；把 82 个 Arc token 映射到内建控件的 `SystemControl*` 语义时，暗色下的对比度/边框/禁用态需要设计确认 | 估时中的 ThemeVariant 1–2.5 人日可能翻倍；且可能引出设计侧 1–2 轮调色 |

> 额外的"过程不确定性"：本仓库当前处于多任务并行修改状态（`git status` 显示大量未提交改动），且禁止构建；上述所有结论都可能在 Lead 统一构建后被修正。

---

## 8. 证据索引（关键 file:line）

### 8.1 基础层与主题装配

| 文件:行 | 内容 |
|---|---|
| `src/Foundation/Arc.Avalonia/Arc.Avalonia.csproj:8-9` | 仅 Avalonia + Avalonia.Fonts.Inter |
| `src/Foundation/Arc.Avalonia/Theme/ArcTokens.cs:7-114` | 82 个 token 常量 |
| `src/Foundation/Arc.Avalonia/Theme/ArcPalette.cs:33` | `InterFont = "avares://Avalonia.Fonts.Inter/Assets#Inter"` |
| `src/Foundation/Arc.Avalonia/Theme/ArcPalette.cs:66-74,142-246` | `controlThumbShadow` 计算后未写入；palette 实际注册 81 个资源键 |
| `src/Foundation/Arc.Avalonia/Theme/ArcPalette.cs:203-204` | 字体 token 指向 Inter |
| `src/Foundation/Arc.Avalonia/Theme/ArcThemeManager.cs:24-38` | `Apply(app, scheme, accent)` + RequestedThemeVariant + MergedDictionaries 替换 |
| `src/Foundation/Arc.Avalonia/Theme/ArcThemeManager.cs:52,60,63,66,69` | `ArcResources` 5 处 `Application.Current` |
| `src/Foundation/Arc.Avalonia/Theme/ArcTheme.axaml:7-12` | 合并 Badge/Button/Card 3 个主题字典 |
| `src/Foundation/Arc.Avalonia/Theme/ArcTheme.axaml:14-30` | 全局 Window/TextBlock/Button TextBlock 样式 |
| `src/Foundation/Arc.Avalonia/Themes/ArcButton.axaml:5-137` | ArcButton 完整 ControlTheme（含 `:focus-visible` 焦点环 :122-125） |
| `src/Foundation/Arc.Avalonia/Controls/ArcVirtualList.cs:40` | `: ListBox` |
| `src/Foundation/Arc.Avalonia/Controls/ArcVirtualList.cs:65` | 注释：虚拟化不依赖"active theme's ListBox template" |
| `src/Foundation/Arc.Avalonia/Controls/ArcVirtualList.cs:71-72` | **明确复用 Fluent ListBox 主题**（`StyleKeyOverride => typeof(ListBox)`） |
| `src/Foundation/Arc.Avalonia/Controls/ArcVirtualList.cs:213,250` | 依赖模板内 `ScrollViewer` 视觉后代 |
| `src/Foundation/Arc.Avalonia/Controls/ArcIcon.cs:65-103` | 自绘 Render（DrawGeometry） |
| `src/Foundation/Arc.Avalonia/Controls/ArcSpinner.cs:78-105` | 自绘 Render（ArcTo） |
| `src/Foundation/Arc.Avalonia/Controls/ArcUi.cs:20,96,106-127,173-180` | 代码组合工具（Border/Pill/Surface 等） |

### 8.2 内建 Fluent 依赖的直接证据

| 文件:行 | 内容 |
|---|---|
| `src/Apps/Arc.Avalonia.Gallery/App.axaml:7` | `<FluentTheme />`（全仓库 1/2） |
| `src/Apps/Arc.Avalonia.Gallery/App.axaml:8-32` | 25 个 `StyleInclude`（项目主题） |
| `.recon/Probe/App.axaml:8` | `<FluentTheme />`（全仓库 2/2） |
| `src/Apps/Arc.Avalonia.Gallery/Arc.Avalonia.Gallery.csproj:12` | `Avalonia.Themes.Fluent` 包引用 |
| `src/Apps/Arc.Avalonia.Gallery/Arc.Avalonia.Gallery.csproj:13` | `Avalonia.Fonts.Inter` 包引用 |
| `src/Apps/Arc.Avalonia.Gallery/Program.cs:12-13` | `UsePlatformDetect()` + `WithInterFont()` |
| `src/Foundation/Arc.Avalonia.Internal/Composition/FeedbackUi.cs:129` | "local values keep Fluent chrome out of the Arc surface" |
| `src/Foundation/Arc.Avalonia.Internal/Controls/ArcIconButton.cs:112` | `ToolTip.SetTip(...)` |
| `src/Components/Arc.Avalonia.Content/Controls/ArcContentUi.cs:121` | "local values keep Fluent chrome off the Arc surface" |
| `src/Components/Arc.Avalonia.Content/Controls/ArcContentUi.cs:153-176` | 剥离 Fluent TextBox 焦点矩形 / 给 Fluent 创建的 TextPresenter 上色 |
| `src/Components/Arc.Avalonia.Media/Controls/ArcScrollArea.cs:195-199` | "platform chrome hidden"，自绘 overlay 滚动条 |
| `src/Components/Arc.Avalonia.Inputs/Themes/ArcPrimitives.axaml:7,65,131,187` | 4 个 Button 自有主题 |
| `src/Components/Arc.Avalonia.Inputs/Themes/ArcPrimitives.axaml:245,255,296,331` | ListBox/ListBoxItem×2/RadioButton 自有主题（**ArcMenuListTheme 无 Template、无 BasedOn**，见 §9） |
| `src/Components/Arc.Avalonia.Inputs/Themes/ArcAdvancedShared.axaml:7,62,109,139,190` | 内建 Button/ContentControl/RadioButton keyed 主题 |
| `src/Components/Arc.Avalonia.Pickers/Themes/ArcEmojiPicker.axaml:191-221` | **已有最小 TextBox ControlTheme**（ScrollViewer + TextPresenter） |
| `src/Components/Arc.Avalonia.Pickers/Themes/ArcEmojiPicker.axaml:192,203,205,208` | TextBox TargetType / Template / PART_ScrollViewer / PART_TextPresenter |
| `src/Components/Arc.Avalonia.Inputs/Themes/ArcSelect.axaml:72,107-110,223,232,348,356` | `<Popup>` + `ListBox Theme=ArcMenuListTheme` |
| `src/Components/Arc.Avalonia.Inputs/Themes/ArcSelect.axaml:183-196` | TextBox 无 Theme、`FocusAdorner="{x:Null}"` |
| `src/Components/Arc.Avalonia.Inputs/Themes/ArcTextField.axaml:42,144,233,357` | 4 个无 Theme 的 TextBox |
| `src/Components/Arc.Avalonia.Pickers/Themes/ArcEmojiPicker.axaml:262-265` | 唯一带 Theme 的 TextBox 实例 |
| `src/Special/Arc.Avalonia.Special.AIWidgets/Controls/ArcContextWindow.cs:94,214-215,313` | 唯一 `new Flyout`（依赖 FlyoutPresenter 主题） |
| `src/Apps/Arc.Avalonia.Gallery/Framework/GalleryWindow.cs:23` | 唯一 `: Window` 子类 |
| `src/Apps/Arc.Avalonia.Gallery/Framework/GalleryWindow.cs:120,134,236` | 3 个 `new ScrollViewer`（Gallery 外壳依赖） |
| `src/Apps/Arc.Avalonia.Gallery/Framework/GalleryWindow.cs:195` | Gallery 搜索框 `new TextBox`（依赖 Fluent 外观） |
| `src/Apps/Arc.Avalonia.Gallery/Framework/GalleryWindow.cs:580` | `ToolTip.SetTip` |
| `src/Apps/Arc.Avalonia.Gallery/Framework/GalleryWindow.cs:548,577` | 两处 `Application.Current` |
| `src/Foundation/Arc.Avalonia.Internal/AssemblyInfo.cs:3-7` | `InternalsVisibleTo` 5 个组件项目 |
| `.recon/Probe/Probe.csproj` | Fluent/Fonts.Inter/Desktop 引用（第二个 Fluent 消费点） |
| `CONVENTIONS.md:207` | 将 Fluent/Fonts.Inter/SkiaSharp 列为技术栈 |
| `README.md:260`、`docs/03-implementation.zh.md:186` | 依赖声明中出现 Fluent |

### 8.3 外部官方源码（Avalonia 12.1.4，用于契约判定）

- Fluent 主题 80 个 xaml 清单（GitHub API）：`https://api.github.com/repos/AvaloniaUI/Avalonia/contents/src/Avalonia.Themes.Fluent/Controls?ref=refs/heads/release/12.1.4`
- Fluent 汇总：`https://raw.githubusercontent.com/AvaloniaUI/Avalonia/refs/heads/release/12.1.4/src/Avalonia.Themes.Fluent/Controls/FluentControls.xaml`（汇总引入这 80 个 xaml 字典）
- Window 主题模板：`https://raw.githubusercontent.com/AvaloniaUI/Avalonia/refs/heads/release/12.1.4/src/Avalonia.Themes.Fluent/Controls/Window.xaml`（含 `VisualLayerManager` + `PART_ContentPresenter`）
- PopupRoot 主题：`https://raw.githubusercontent.com/AvaloniaUI/Avalonia/refs/heads/release/12.1.4/src/Avalonia.Themes.Fluent/Controls/PopupRoot.xaml`（含 `PART_VisualLayerManager`）
- `Window.cs`（无默认 Template）：`https://raw.githubusercontent.com/AvaloniaUI/Avalonia/refs/heads/release/12.1.4/src/Avalonia.Controls/Window.cs`
- `ContentControl.cs`（有默认 Template，反例）：`https://raw.githubusercontent.com/AvaloniaUI/Avalonia/refs/heads/release/12.1.4/src/Avalonia.Controls/ContentControl.cs`
- Avalonia 官方文档「一个控件同时只能应用一个 ControlTheme」：https://docs.avaloniaui.net/docs/styling/control-themes

### 8.4 统计脚本口径（可复现）

```powershell
# 基础层
Get-ChildItem -Recurse -File src\Foundation\Arc.Avalonia | ? { $_.FullName -notmatch '\\(bin|obj)\\' }
# 内建控件使用：axaml 元素 + cs 词边界（排除 Arc* 前缀）
[regex]::Matches($text,'(?<![A-Za-z0-9_])TextBox(?![A-Za-z0-9_])')
# 自绘
[regex]::Matches($text,'override\s+void\s+Render\s*\(\s*DrawingContext')
# ControlTheme
[regex]::Matches($text,'<ControlTheme\b')
```

---

## 9. 未验证事项清单（必须由 Lead 构建/运行后确认）

| # | 事项 | 原因 | 建议验证方式 |
|---|---|---|---|
| 1 | 删除 `<FluentTheme />` 后的实际崩溃/空白清单 | 本次禁止构建与运行；"无模板→不渲染"由 Avalonia 源码契约推得，但各控件是否有默认模板需逐个确认（已确认 Window 无、ContentControl 有） | 建分支删 FluentTheme，跑 `--shot` 与 `--smoke`，逐页对比 `docs/images` |
| 2 | `ArcMenuListTheme`（`ArcPrimitives.axaml:245`）未定义 Template 且**全仓无 `BasedOn`** | 官方文档称一个控件同时只应用一个 ControlTheme；若如此，显式 `Theme` 会替换隐式主题，ListBox 将无模板（选择/组合框弹层可能本来就空白，因截图里弹层默认关闭而未暴露）。也可能 Avalonia 对未设置的 Template 有隐式回退 | 打开任一 Select/Combobox 弹层截图或写最小复现；无论结论如何，去 Fluent 时都必须补 ListBox 模板 |
| 3 | `Popup`/`PopupRoot` 在 Avalonia 12 `TopLevelHost` 机制下的宿主行为 | 12.x 新增 `TopLevelHost`、`WindowDrawnDecorations`、`WindowDecorationsTheme`，静态无法判断 | 运行弹层/焦点/DPI 手工用例 |
| 4 | TextBox 的 IME/触屏选择/双向绑定细节 | 需要 Windows 11 实机与输入法 | 手工测试 IME 组合、触摸选择、水印、`Text` 双向 |
| 5 | Linux/macOS 的文件对话框（ManagedFileChooser）与窗口装饰 | 当前只在 Windows 环境审计，未运行其他平台 | CI/虚拟机双平台 smoke |
| 6 | 视觉回归是否可接受 | 无自动像素 diff；1180px 基线只覆盖双主题截图 | 建 screenshot diff（可选 `Avalonia.Headless` + RenderTargetBitmap） |
| 7 | 精确的"内建控件全集"数量（Tier3 ≈43 个） | 由 Avalonia.Controls.xml 公开类型 + Fluent 80 个 xaml 人工归类，未逐一验证每个都有 ControlTheme | 以 Fluent 主题的 `TargetType` 清单为准做 checklist |

---

## 10. 附：关键数字速查

| 指标 | 值 |
|---|---|
| 源项目 | 28（src）+ 1 tests + 1 Probe |
| 全仓 C# / axaml | 645 文件 / 197,572 行；118 文件 / 9,396 行（合计 206,968 行） |
| 公开类 / catalog 条目 | 765 / 294 |
| 自定义 ControlTheme / Style | 137 / 597 |
| `DynamicResource` / `StaticResource` | 1,189 / 19 |
| `ThemeDictionaries` | 0 |
| Foundation | 18 文件 / 101.5 KB / 2,294 行；82 token（palette 注册 81）/ 161 图标 / 6 控件 + ArcUi |
| Internal | 10 文件 / 52.0 KB / 1,662 行 |
| 控件分类 | 模板链 123 : 非模板 112（≈52% : 48%）；Panel 37；非视觉 25 |
| FluentTheme 标记引用 | 2 处 axaml + 2 处 csproj |
| Fluent 主题规模 | 80 xaml / 540,657 B / DLL 716.0 KB |
| Simple 主题规模（参照） | DLL 405.0 KB（12.1.0） |
| Fonts.Inter | DLL 1,848.5 KB / nupkg 1,915.5 KB |
| 平台/渲染传递依赖 | SkiaSharp 3.119.4、HarfBuzzSharp 8.3.1.3、Avalonia.Win32 12.1.4 |
| 去 Fluent 人日（中性） | 保现状 **23**；框架级 **48** |
