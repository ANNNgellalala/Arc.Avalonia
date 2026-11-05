# ADR 0003 · 主题聚合入口 `ArcTheme`：单行接入可行，采用 XAML 嵌套方案

> 状态：**Accepted**（M0-4 结论，2026-11-05）
> 对应：`docs/14-m0-baseline-and-gates.zh.md` §5（M0-4 POC-A）、`docs/13-framework-layering-and-agent-top.zh.md` v3.2 §2.3 / §2.5 / D3
> 证据工程：`poc/ArcThemePoc/`（不进 `ArcUI.slnx`）

## 1. 问题

v3.2 的接入形态是"一行替代 27 行"：

```xml
<Application.Styles>
  <arc:ArcTheme />   <!-- 内部 = FluentTheme + token + Top 内建覆盖 + 11 个组件主题 -->
</Application.Styles>
```

需要验证三个假设（docs/14 §5.1）：

1. 一个自定义 `Styles` 子类能否在 XAML 里**嵌套** `<FluentTheme/>` + 资源字典 + `<StyleInclude>`；
2. Light/Dark × 8 accent 能否通过 `FluentTheme.Palettes`（`ColorPaletteResources`）映射，而不是裸 `SystemAccentColor`；
3. 组件主题（`ArcButton.axaml` 等）与 Fluent 覆盖能否**共存**，且 `ThemeDictionaries` 的 `DynamicResource` 正常解析。

## 2. 决策

**采用单行 `<arc:ArcTheme/>`（XAML 嵌套 `<FluentTheme/>`）方案；不需要回退到 `app.UseArcTheme()`。**

`ArcTheme : Styles` 的实现形态（POC 实测可编译、可运行）：

```xml
<Styles xmlns="https://github.com/avaloniaui" xmlns:x="..." x:Class="ArcThemePoc.ArcTheme">
  <Styles.Resources>
    <ResourceDictionary>
      <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Light"> … token 覆盖 … </ResourceDictionary>
        <ResourceDictionary x:Key="Dark"> … token 覆盖 … </ResourceDictionary>
      </ResourceDictionary.ThemeDictionaries>
    </ResourceDictionary>
  </Styles.Resources>

  <FluentTheme />
  <StyleInclude Source="avares://Arc.Avalonia/Theme/ArcTheme.axaml" />
  <StyleInclude Source="avares://Arc.Avalonia.Inputs/Themes/ArcInputsTheme.axaml" />
</Styles>
```

```csharp
public partial class ArcTheme : Styles
{
    public ArcTheme() => AvaloniaXamlLoader.Load(this); // 可行：Styles 支持 x:Class + AvaloniaXamlLoader
}
```

## 3. 证据

运行：`dotnet poc/ArcThemePoc/bin/Debug/net10.0/ArcThemePoc.dll`（Avalonia 12.1.4 + Avalonia.Desktop 12.1.4 + Fluent 12.1.4，离线缓存）。
退出码 **0**；每个主题 17 条断言、共 **51 条全 PASS**。

### 3.1 假设 1：嵌套 `<FluentTheme/>` 可行（未被 XAML 拒绝）

* `App.axaml` 里只有 `<poc:ArcTheme />`；`ArcTheme.axaml` 内 `<FluentTheme />` 编译通过并注册为样式；
* 裸 `TextBox` / `ComboBox` / `TabControl` 均获得 `ControlTheme`（`Template != null`）、`Background` 由 Fluent 键解析出 `#66ffffff`（Light）/`#66000000`（Dark）；
* 没有任何 XAML 解析异常、没有 `StaticResource` 缺失异常；
* **没有触发** docs/14 §5.3 的 fallback 路径。

### 3.2 优先级：聚合自身的 token 覆盖**赢过**嵌套的 FluentTheme

| key | Fluent 12.1.4 默认 | 聚合覆盖（Light / Dark） | 结论 |
|---|---|---|---|
| `ControlCornerRadius` | `3,3,3,3` | `14,14,14,14` / `10,10,10,10` | 覆盖生效 |
| `ControlContentThemeFontSize` | `14` | `15` | 覆盖生效（真实键名见 §4.1） |
| `TextControlThemePadding` | `10,6,6,5` | 见 ADR 0004 | 覆盖生效 |

裸 `TextBox` 模板内的 `Border.CornerRadius` 回读为 `14/10`（不是 Fluent 的 3），说明 `DynamicResource` 真的走了聚合字典。

### 3.3 假设 2：`FluentTheme.Palettes` 可映射 8 accent

* `FluentTheme.Palettes` 在 12.1.4 存在（运行时反射：`DensityStyle, Palettes, …`）；`ColorPaletteResources` 有 34 个颜色属性（`Accent / AltHigh / BaseHigh / Chrome* / ErrorText / ListLow / RegionColor …`）；
* 设置 `Palettes[Light|Dark] = new ColorPaletteResources { Accent = 颜色 }` 后，`SystemAccentColor` **立即**变为该颜色（Neutral `#353535`、Violet `#7747FF`），不需要重建窗口；
* 视觉：`theme-light-violet.png` 中裸 `TabControl` 的选中下划线、以及 Fluent 强调色控件随 accent 变紫，说明 accent 真的进入了 Fluent 的键。

> 结论：8 accent 走 `Palettes`（每条 theme variant 一份）是可行路线，不必改用裸 `SystemAccentColor` 覆盖。

### 3.4 假设 3：组件主题与 Fluent 覆盖共存

同一 `Styles` 聚合内：`ArcButton`（Primary/Secondary/Ghost）、`ArcInput`×2 共 5 个 Arc 控件的 `Template` 全部解析成功；同时 3 个裸 Fluent 控件的 `Template` 也全部解析成功。两套 `ControlTheme` 无冲突。

### 3.5 截图（`docs/images/poc/`，均 ≤1200 长边、≤300KB）

| 文件 | 内容 |
|---|---|
| `theme-light-neutral.png` | Light / Neutral：Arc 组件 + 裸 Fluent 控件 |
| `theme-dark-neutral.png` | Dark / Neutral |
| `theme-light-violet.png` | Light / Violet：accent 经 `Palettes` 进入 Fluent 控件 |

## 4. 必须写进 M1/M2 的坑（POC 实测）

### 4.1 键名必须以 12.1.4 为准

`ContentControlThemeFontSize` **不是** Fluent 12.1.4 的键（实测 `<missing>`；真实键是 `ControlContentThemeFontSize`=14）。用错名字的表现是"覆盖成功（自定义 key 可解析）但控件无变化"。ADR 0004 有完整清单。

### 4.2 `Application.TryFindResource(key, out value)` **看不到** `ThemeDictionaries`

POC 实测（同一时刻、同一 key）：

| 调用 | Light | Dark |
|---|---|---|
| `app.TryFindResource("ControlCornerRadius")`（无 variant） | `3,3,3,3`（Fluent 默认） | `3,3,3,3` |
| `app.TryFindResource("ControlCornerRadius", app.ActualThemeVariant)` | **14** | **10** |
| `app.TryFindResource("PocTokenBrush")`（无 variant） | `<missing>` | `<missing>` |
| `app.TryFindResource("PocTokenBrush", ActualThemeVariant)` | `#3366FF` | `#8FA8FF` |

**影响**：`src/Foundation/Arc.Avalonia/Theme/ArcThemeManager.cs` 的 `ArcResources.Brush/Color/Double/Radius/Shadows/Duration` 全部走无 variant 的 `Application.Current.TryFindResource`。现在 token 是"普通字典"（无 `ThemeDictionaries`）所以没事；**一旦 M1/M2 把 token 迁进 `ThemeDictionaries`，所有代码侧取 token 的组件（Charts 等）会静默拿到默认值**。

**修复（列入 M1-T3）**：`ArcResources` 改成 `TryFindResource(key, Application.Current.ActualThemeVariant, out value)`；并加一条回归断言（双主题下同一 key 取到不同值）。

### 4.3 `FluentTheme` 在 12.1.4 只有 `FluentTheme(IServiceProvider)` 构造

`Avalonia.Themes.Fluent.dll` 的 XML doc 只暴露 `M:Avalonia.Themes.Fluent.FluentTheme.#ctor(System.IServiceProvider)`。XAML 嵌套时由 XAML 加载器注入 App 的 `IServiceProvider`（`Application : IServiceProvider`），因此 `<FluentTheme />` 可用。

若将来真要走 fallback 的 `app.UseArcTheme()`，**必须** `app.Styles.Add(new FluentTheme(app))`（`Application` 自身就是 `IServiceProvider`）；`new FluentTheme()` 无参构造不存在。POC 不需要该路径，记录备查。

### 4.4 嵌套 `Styles` 的 `x:Class` 可用

`Styles` 子类 + `AvaloniaXamlLoader.Load(this)` 在 12.1.4 正常工作（POC 即此形态），无需用 `StyleInclude` + 无 `x:Class` 的替代写法。

## 5. Fallback（未采用，保留设计）

docs/14 §5.3 的 `app.UseArcTheme(ArcThemeOptions)` 编程式入口仍然成立，形态：

```csharp
public static void UseArcTheme(this Application app, ArcThemeOptions? options = null)
{
    app.Styles.Insert(0, new FluentTheme(app));               // 12.1.4 必须传 IServiceProvider
    app.Styles.Add(new StyleInclude(new Uri("avares://Arc.Avalonia.Theme/"))
        { Source = new Uri("avares://Arc.Avalonia.Theme/Theme/ArcTheme.axaml") });
    app.Resources.MergedDictionaries.Add(tokenDictionary);     // 含 ThemeDictionaries
}
```

消费者仍是一行（`app.UseArcTheme()`），但**不推荐**：它把"主题装配"从声明式变成命令式，且资源优先级依赖 `Styles.Insert(0)`，比 XAML 嵌套更难验证。本次 POC 证明不需要它。

## 6. 与任务书的偏差（重要）

task-10 原文要求用 **Avalonia.Headless**。实测本地缓存只有 `Avalonia.Headless 12.1.5`，而仓库主版本是 **12.1.4**，混用会引入 `Avalonia.*` 版本上移（12.1.5）的依赖冲突风险。经与 Lead 确认，**两个 POC 均改用 Avalonia 12.1.4 + Avalonia.Desktop 12.1.4 + Fluent 12.1.4，用 `Arc.Avalonia.Gallery/Framework/ShotRunner.cs` 同款离屏渲染**（`AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime` → 建 Window、`Show()`、`RenderTargetBitmap.Render()`、`Save()`、`Environment.Exit()`）。

该方案在本机已验证（Gallery 的 shots 流程即如此），且完全离线。

## 7. 复现

```powershell
dotnet build poc\ArcThemePoc\ArcThemePoc.csproj -m:1 -nodeReuse:false
dotnet poc\ArcThemePoc\bin\Debug\net10.0\ArcThemePoc.dll            # 3 张 PNG + 断言，exit 0
dotnet poc\ArcThemePoc\bin\Debug\net10.0\ArcThemePoc.dll --probe    # 打印 Fluent/FluentTheme 资源与解析差异
```

## 8. 后续动作

1. M1-T3 落地 `src/Theme/Arc.Avalonia.Theme`：`ArcTheme : Styles`（XAML）+ `ArcThemeOptions` + `Palettes` 的 8 accent 映射表；
2. 修 §4.2 的 `ArcResources` variant 问题（否则主题迁移会静默回归）；
3. 组件主题 11 个 `StyleInclude` 全部并入 `ArcTheme`，`Gallery/App.axaml` 从 27 行降到 1 行；
4. 加"键常量表 + Fluent 升级检测"（见 ADR 0004）。
