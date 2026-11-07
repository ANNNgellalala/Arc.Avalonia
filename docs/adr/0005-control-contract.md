# ADR 0005 · 控件契约：TemplatePart 命名与严格 additive RoutedEvent

- 状态：**Accepted**（M2-G3a-7，2026-11-07）
- 范围：`src/{Foundation,Components,Agent}` 的 TemplatedControl 契约 + Top 交互事件的 RoutedEvent 化
- 对应：[docs/13 §2.4 / D7](../13-framework-layering-and-agent-top.zh.md)、[docs/18 契约规范](../18-control-contract-spec.zh.md)
- 门禁：`tools/check-contracts.ps1`（R10，已接入 `tools/check-all.ps1`）

## 1. 背景

M1 前基线：143 个公开 CLR 事件、**0** 个 `RoutedEvent.Register`、**0** 个 `[TemplatePart]`、约 200 处
`OnApplyTemplate`。后果是：宿主/容器无法在祖先层统一监听控件交互；第三方改主题时没有部件名契约可依；
Doc 页面与控件实现之间只有“能跑”的耦合。M2-G3a 对约 76 个 TemplatedControl 与 Top 交互事件做契约化。

契约必须在**不破坏现有消费者**的前提下建立：仓库已有 320 个 Docs 页面、Gallery 页面与第三方样例直接订阅
当前 CLR 事件。因此本 ADR 采用 **A：严格 additive + 双通知**，明确拒绝 **B：同名转换**。

## 2. 决策

### 2.1 A（采用）：严格 additive + 双通知

1. **旧 CLR 事件零变更**：事件名、`EventHandler<T>` 签名、触发时机、`add/remove` 语义全部保留；不删除、
   不改名、不把旧事件改成 RoutedEvent 包装。
2. **新增 RoutedEvent 面**：字段统一命名为 `<Name>Event`，声明形式固定：
   ```csharp
   public static readonly RoutedEvent<MyEventArgs> ValueChangedEvent =
       RoutedEvent.Register<MyControl, MyEventArgs>("ValueChanged", RoutingStrategies.Bubble);
   ```
   `RoutingStrategies.Bubble` 是交互结果事件的默认策略；生命周期事件（Loaded/Attached/Detached）保持 CLR。
3. **CLR 包装名约定**（M2 修正）：若类中**存在**同名旧 CLR 事件（`event EventHandler<...> Foo`），包装必须叫
   `<Name>Routed`（冲突时 `<Name>RoutedEvent`），避免与新/旧 member 命名混淆；若**不存在**同名旧事件，包装可直接用
   同名 `<Name>`（Avalonia 惯例），也可用 `<Name>Routed`，两者都合规。自定义 `add/remove` 必须转发
   `AddHandler(<Name>Event, value)` / `RemoveHandler(<Name>Event, value)`：
   ```csharp
   public event EventHandler<MyEventArgs>? ValueChangedRouted
   {
       add => AddHandler(ValueChangedEvent, value);
       remove => RemoveHandler(ValueChangedEvent, value);
   }
   ```
4. **args 基类**：新 `*EventArgs` 必须派生自 `Avalonia.Interactivity.RoutedEventArgs`，提供无参构造 +
   `RoutedEvent` 构造（示例见 §6）。旧 CLR 事件继续使用原参数（`EventArgs` 或既有 args）。
5. **双通知是设计**：一次交互在设置 `args.RoutedEvent = <Name>Event` 后 `RaiseEvent(args)`，**同时**调用
   旧 CLR 事件 `Xxx?.Invoke(...)` 是 A 的预期行为，不是“双触发 bug”。固定顺序：先旧 CLR（保持既有同步
   语义），后 `RaiseEvent`；新代码应成对出现，避免只加一半。
6. **兼容边界**：旧事件与 handler 签名零变更（source & binary 语义不变）；`<Name>Routed` 是新可选面，
   第三方可继续用旧事件，也可在容器上 `AddHandler` 冒泡事件。迁移不要求改 Docs 页面。

### 2.2 B（拒绝）：同名转换

把旧 `ValueChanged` 直接改成同名 RoutedEvent 包装（或改 handler 签名）会让 320 个 Docs 页面、Gallery
页面与外部消费者同时 breaking；且无法在一个 minor 内提供兼容 shim。M2 是框架契约建设期，破坏面不可接受，
故拒绝。若未来要收敛，需新增 major 版本并在 ADR 中重开。

### 2.3 TemplatePart 契约

- 声明必须使用构造式并显式 `using Avalonia.Controls.Metadata;`：
  `[TemplatePart("PART_X", typeof(Border))]`（不要用字符串键/反射式注册）。
- 部件名统一 `PART_*`；`OnApplyTemplate` 中通过 `e.NameScope.Find<T>("PART_X")`（或 `GetTemplateChild("PART_X")`）
  取得的名称，必须在该类上声明同名 `[TemplatePart]`。
- `Themes/*.axaml` 模板内的 `x:Name="PART_X"` 必须与代码声明一致；改名属于公开契约变更：
  保留旧名别名/至少一个 minor 的 Obsolete 提示 + CHANGELOG 迁移说明，禁止静默改名。
- 自绘 `Control`（如 22 个 ArcChart）没有模板部件，明确不适用 `[TemplatePart]`；未来若模板化，
  预留 `PART_Plot` / `PART_Legend` / `PART_Tooltip`。

### 2.4 键盘 / 焦点 / 自动化契约

- 可交互控件须 `Focusable=true`，并通过 `:focus-visible` 提供可见焦点（不能用 hover 代替）。
- `Enter`/`Space` 激活 Button 类控件；`Escape` 关闭 Popup/Dialog/Overlay；方向键/Home/End 用于列表/标签组。
- 焦点顺序遵循布局顺序；容器不得吞掉 `Tab`；禁用态用 `IsEnabled=false` 而不是只隐藏。
- 自动化：`AutomationProperties.Name/HelpText` 必填（图标按钮至少 Name），Toast/Alert 用
  `AutomationLiveSetting`（危险态 Assertive）；装饰性图标不进入可访问性树（`IsVisible=false` 或
  `AutomationProperties.IsOffscreenBehavior` 约定）。
- 动效遵循 `ArcThemeManager.ReducedMotion`；不得用自研帧调度（R8）。

## 3. R10 门禁规则（可执行）

| 规则 | 检查内容 | 失败 kind |
|---|---|---|
| R10.1 | 有 `OnApplyTemplate` 覆写的 TemplatedControl 必须有 ≥1 个 `[TemplatePart(` | `missing-templatepart` |
| R10.2 | 部件查找（`GetTemplateChild` / `FindControl` / `NameScope.Find`）的 `PART_*` 名必须在声明集合内 | `undeclared-part` |
| R10.3 | TemplatePart 名必须 `PART_*`；构造式声明 | `invalid-templatepart-name`（+ PR 检查） |
| R10.4 | RoutedEvent 字段形式 `public static readonly RoutedEvent<TArgs> <Name>Event = RoutedEvent.Register<…>` | 解析告警（人工复核） |
| R10.5 | 包装名规则：存在同名旧 CLR 事件 → `<Name>Routed`/`<Name>RoutedEvent`；不存在 → 同名 `<Name>` 也合规；add/remove 走 AddHandler/RemoveHandler | `routedevent-no-wrapper` |
| R10.6 | 新 `*EventArgs` 若被 `RoutedEvent<…>` 引用，必须派生自 `RoutedEventArgs` | `routedevent-args-base` |
| R10.7 | 旧 CLR 事件允许暂时没有路由对应物（additive 迁移中） | `legacy-event-missing-routed`（exempt/warning，不计 red） |
| R10.8 | 双通知：旧事件 `Invoke` + `RaiseEvent` 成对出现（不做“只加一半”） | 人工复核 / PR checklist |
| R10.9 | 模板 `x:Name` 与 `[TemplatePart]` 一致 | 人工/主题 owner 复核（`grep PART_`） |
| R10.10 | 键盘/焦点/自动化契约 | PR checklist + `--audit/--smoke`（Lead 统一跑） |
| R10.11 | C# 取 token 必须用 `ArcResources.TryFind/Font`；axaml 用 `DynamicResource`；禁止无变体 `Application.FindResource`/`.FindResource`（token 在 ThemeDictionaries 中，会退化为透明/默认字体） | `untargeted-resource-lookup` |

门禁做**可静态判定**的 1/2/3/5/6/11；4/8/9/10 属评审项，避免误报。

## 4. 例外与过渡基线

- `tools/contracts-baseline.json` 是**过渡 allowlist**（按 `kind+file+symbol`）：切片只完成一半时，
  未完成项降级为 `exempt`，不阻塞 `check-all`；新增文件/新控件不在 allow 内仍 RED。
  `pwsh -NoProfile tools/check-contracts.ps1 -UpdateBaseline` 刷新；切片完成后应收敛到 `allow=[]`。
- **包装命名规则（M2 决策）**：存在同名旧 CLR 事件时加 `Routed` 后缀以免冲突；不存在时直接同名更贴合 Avalonia
  惯例（`Charts` 的 `PointClick/ItemClick/LegendClick/RangeSelect` 4 个即属后者，不再是例外，已从 baseline 移除）。
- **可路由类型边界**：`RoutedEvent` 只适用于派生自 `Avalonia.Interactivity.Interactive` 的类型（Control /
  TemplatedControl / ContentControl / …）。服务、模型、协调器类（如 `ArcDeltaCoalescer`、`ArcAgentWorkspace`、
  `ArcAgentSessionModel`、`ArcNavigationService`、`ArcNotificationService`）没有 UI 继承链、无法 `RaiseEvent`/冒泡，
  它们的 CLR 事件是合理设计，计入 `nonRoutableEvents` 指标，不做路由迁移。
- 旧 CLR 事件缺路由对应物（`legacy-event-missing-routed`）只统计，不判红；本波只承诺 Top 5–10 控件。

## 5. 验证

```powershell
pwsh -NoProfile tools/check-contracts.ps1                    # R10 单独跑（无基线时全量报红）
pwsh -NoProfile tools/check-contracts.ps1 -UpdateBaseline    # 切片中途刷新 allowlist
pwsh -NoProfile tools/check-contracts.ps1 -NoBaseline        # 审计模式：所有命中都算 red
pwsh -NoProfile tools/check-all.ps1                          # 9 行门禁，R10 GREEN
```

2026-11-07 03:15 快照（切片仍在收尾，数字会随改造变化，以最近一次脚本输出为准）：

```text
TemplatedControls=108 / WithTemplatePart=108 / RoutedEventFields=139 / WithWrapper=139 /
LegacyEventsWithRouted=99 / LegacyEventsMissingRouted=0 / NonRoutableEvents=6 / UntargetedLookups=0
R10 GREEN new=0 exempt=0（过渡 allowlist/legacy warning 均已清空；Lead 收尾后统一 -UpdateBaseline）
check-all: green=9/9 knownRed=0 red=0 => GREEN（exit 0）
```

`nonRoutableEvents=6` 为服务/模型类的 CLR 事件（`ArcDeltaCoalescer.Flush`、`ArcAgentWorkspace.Changed`、
`ArcAgentSessionModel.Changed`、`ArcNavigationService.Changed`、`ArcNotificationService.Shown/Dismissed`），
只统计、不进入 legacy 待补、不计 exempt、不计 red。

## 6. 迁移指引

**加模板部件**：

```csharp
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

[TemplatePart("PART_Input", typeof(TextBox))]
[TemplatePart("PART_Clear", typeof(ArcButton))]
public class ArcXxx : TemplatedControl
{
    private TextBox? _input;
    private ArcButton? _clear;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _input = e.NameScope.Find<TextBox>("PART_Input");
        _clear = e.NameScope.Find<ArcButton>("PART_Clear");
    }
}
```

模板侧：`<TextBox x:Name="PART_Input" />`；改名前先加旧名 `[TemplatePart]` + Obsolete 说明，一个 minor 后删除。

**加交互 RoutedEvent（additive）**：

```csharp
public sealed class ArcXxxValueChangedEventArgs : RoutedEventArgs
{
    public ArcXxxValueChangedEventArgs() { }
    public ArcXxxValueChangedEventArgs(RoutedEvent routedEvent) : base(routedEvent) { }
    public string? Value { get; init; }
}

public static readonly RoutedEvent<ArcXxxValueChangedEventArgs> ValueChangedEvent =
    RoutedEvent.Register<ArcXxx, ArcXxxValueChangedEventArgs>("ValueChanged", RoutingStrategies.Bubble);

public event EventHandler<ArcXxxValueChangedEventArgs>? ValueChangedRouted
{
    add => AddHandler(ValueChangedEvent, value);
    remove => RemoveHandler(ValueChangedEvent, value);
}

private void RaiseValueChanged(string? value)
{
    ValueChanged?.Invoke(this, new ArcXxxValueChangedEventArgs { Value = value });   // 旧 CLR：零变更
    RaiseEvent(new ArcXxxValueChangedEventArgs(ValueChangedEvent) { Value = value }); // 新增路由面
}
```

提交前跑：`check-contracts`（R10）+ `check-all`（R1–R10）；PR 描述附计数变化与新增部件/事件清单。
