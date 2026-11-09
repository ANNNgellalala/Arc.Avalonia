# 18 · 控件契约规范（第三方可执行版）

> 版本：1.0（M2-G3a，2026-11-07）
> 状态：Accepted；对应 [ADR 0005](adr/0005-control-contract.md)、[docs/13 §2.4/D7](13-framework-layering-and-agent-top.zh.md)
> 门禁：`tools/check-contracts.ps1`（R10）、`tools/check-touch.ps1`（R11）、`tools/mobile-smoke.ps1`（R12）→ `tools/check-all.ps1`（11 行全绿才通过；R12 可在 `--mobile` 未落地时 SKIP）
> 适用范围：`src/{Foundation,Components,Agent}` 的 TemplatedControl 与公开交互事件；`Charts` 自绘 `Control` 见 §1 规则 1 例外。

本规范写给**第三方控件作者与主题作者**：它把 Arc 的模板/事件/键盘契约写成可执行规则，并给出可直接复制的
示例。工具只静态检查可判定项（R10.1/2/3/5/6/11）；其余通过 PR checklist 与运行时验证补齐。

## 1. 十一条规则（可执行版）

| # | 规则 | 要求 | 如何验证 |
|---|---|---|---|
| 1 | TemplatePart 声明 | 有 `OnApplyTemplate` 覆写的 TemplatedControl（及派生类）必须声明 ≥1 个 `[TemplatePart("PART_X", typeof(T))]`；自绘 `Control`（如 22 个 Charts）没有模板，不适用 | R10 `missing-templatepart`（check-contracts） |
| 2 | 声明方式 | 必须 `using Avalonia.Controls.Metadata;` + 构造式特性；禁止字符串键/反射注册/仅注释 | R10 解析 + PR 复核 |
| 3 | 命名 | 部件名统一 `PART_*`（全大写 `PART_` 前缀 + PascalCase 后缀） | R10 `invalid-templatepart-name` |
| 4 | 查找一致性 | `OnApplyTemplate` 中 `GetTemplateChild("X")` / `e.NameScope.Find<T>("X")` / `FindControl("X")` 的 `PART_*` 名必须在该类声明集合内 | R10 `undeclared-part` |
| 5 | 模板一致 | `Themes/*.axaml` 的 `x:Name="PART_X"` 与 `[TemplatePart]` 一一对应；`ControlTheme` 的 `:focus-visible`/状态样式也按部件名选择 | `grep -R "PART_" Themes/` + 主题 owner 复核 |
| 6 | RoutedEvent 声明 | `public static readonly RoutedEvent<TArgs> <Name>Event = RoutedEvent.Register<TOwner, TArgs>("<Name>", RoutingStrategies.Bubble);`；生命周期事件可保持 CLR | R10 解析（结构/命名） |
| 7 | args 基类 | 新 `*EventArgs` 必须派生自 `Avalonia.Interactivity.RoutedEventArgs`，提供无参 + `RoutedEvent` 构造 | R10 `routedevent-args-base` |
| 8 | CLR 包装 | 存在同名旧 CLR 事件（`event EventHandler<...> Foo`）→ 包装必须 `<Name>Routed`（冲突时 `<Name>RoutedEvent`）；不存在同名旧事件 → 直接同名 `<Name>` 或 `<Name>Routed` 都合规；自定义 `add/remove` 转发 `AddHandler(<Name>Event, value)` / `RemoveHandler(<Name>Event, value)` | R10 `routedevent-no-wrapper` |
| 9 | 双通知（additive） | 旧 CLR 事件名/签名零变更：一次交互先 `OldEvent?.Invoke(...)`，再 `args.RoutedEvent = <Name>Event; RaiseEvent(args)`；不允许只加一半 | 人工复核 + `legacy-event-missing-routed` 统计（warning，仅可路由类型） |
| 10 | 键盘/焦点/自动化 | `Focusable`、`:focus-visible`、Enter/Space、Escape、方向键/Home/End、`AutomationProperties.Name/HelpText/LiveSetting`、禁用态可见 | PR checklist + `--audit/--smoke`（Lead 统一跑） |
| 11 | 资源查找（token） | C# 取 token 必须用 `ArcResources.TryFind(...)` / `ArcResources.Font(...)`；axaml 用 `DynamicResource`；**禁止无变体 `Application.FindResource` / `.FindResource`**（token 在 `ThemeDictionaries`，无变体查找退化为透明/默认字体） | R10 `untargeted-resource-lookup` |

## 2. Top 40 控件抽样清单

> 快照：2026-11-07 02:47（M2-G3a 切片进行中）；`—` 表示该控件当前没有路由事件/待补。
> 最终以 `pwsh -NoProfile tools/check-contracts.ps1` 输出为准；本表用于第三方抽查“部件名 + 事件面”。

| # | 控件 | 家族 | TemplatePart（当前/目标） | RoutedEvent（当前/目标） |
|---:|---|---|---|---|
| 1 | `ArcButton` | Actions | `PART_Spinner` | — |
| 2 | `ArcIconButton` | Actions | `PART_Icon` | — |
| 3 | `ArcSplitButton` | Actions | `PART_Main, PART_Chevron, PART_Divider, PART_Overlay` | `ClickEvent` → `ClickRouted` |
| 4 | `ArcButtonGroup` | Actions | `PART_Segments, PART_HighlightLayer, PART_Highlight, PART_Overlay` | — |
| 5 | `ArcDropdownMenu` | Actions | `PART_Trigger, PART_Overlay` | — |
| 6 | `ArcContextMenu` | Actions | `PART_Target, PART_Ring, PART_Overlay` | — |
| 7 | `ArcAlert` | Actions | `PART_Icon, PART_Title, PART_Details, PART_Action, PART_Close` | `DismissedEvent` → `DismissedRouted` |
| 8 | `ArcToast` | Actions | `PART_Surface, PART_Title, PART_Description, PART_Action, PART_Close` | `ClosedEvent` → `ClosedRouted` |
| 9 | `ArcAccordion` | Actions | `PART_Rows` | — |
| 10 | `ArcExpandableText` | Actions | `PART_Body, PART_Clip, PART_Fade, PART_Toggle` | `ExpandedChangedEvent` → `ExpandedChangedRouted` |
| 11 | `ArcTabs` | Navigation | `PART_Triggers, PART_GlideLayer, PART_Glide, PART_PanelHost` | `SelectionChangedEvent` → `SelectionChangedRouted` |
| 12 | `ArcBreadcrumb` | Navigation | `PART_Crumbs` | — |
| 13 | `ArcPagination` | Navigation | `PART_Buttons` | `PageChangedEvent` → `PageChangedRouted` |
| 14 | `ArcMegaMenu` | Navigation | `PART_Bar, PART_BrandHost, PART_TriggerZone, PART_Triggers, PART_ActionsHost, PART_MenuButton, PART_Overlay, PART_BarHighlight, PART_Caret` | `SectionChangedEvent/ItemInvokedEvent` → `…Routed` |
| 15 | `ArcTextFieldBase` | Inputs | `PART_Input, PART_Message, PART_LeadingIcon, PART_TrailingIcon, PART_ClearAction` | — |
| 16 | `ArcNumberField` | Inputs | `PART_Input, PART_Message, PART_LimitHint, PART_PrefixText, PART_SuffixText, PART_IncreaseAction, PART_DecreaseAction` | — |
| 17 | `ArcTagInput` | Inputs | `PART_Input, PART_Message, PART_TagPanel, PART_Shell` | — |
| 18 | `ArcMultiSelect` | Inputs | `PART_Trigger, PART_Ring, PART_ChipPanel, PART_PlaceholderText, PART_Overflow, PART_Chevron, PART_Message, PART_MenuPopup, PART_MenuSurface, PART_Menu` | — |
| 19 | `ArcSlider` | Inputs | `PART_TrackArea, PART_Track, PART_Fill, PART_Thumb, PART_Bubble, PART_BubbleText, PART_Readout, PART_LabelText, PART_Description, PART_Marks` | — |
| 20 | `ArcSwitch` | Inputs | `PART_Track, PART_Thumb` | — |
| 21 | `ArcCheckbox` | Inputs | `PART_CheckGlyph, PART_DashGlyph` | — |
| 22 | `ArcRadioGroup` | Inputs | `PART_Rows` | — |
| 23 | `ArcRating` | Inputs | `PART_Stars, PART_ValueText, PART_LabelText, PART_Description` | — |
| 24 | `ArcCombobox` | Inputs | `PART_Input, PART_Message, PART_EmptyText, PART_Trigger, PART_ClearAction, PART_MenuPopup, PART_MenuSurface, PART_Menu` | — |
| 25 | `ArcDialog` | Overlays | `PART_Backdrop, PART_Panel, PART_Title, PART_Description, PART_FooterHost` | `ClosedEvent` → `ClosedRouted` |
| 26 | `ArcDrawer` | Overlays | `PART_Backdrop, PART_Panel, PART_Title, PART_Description, PART_FooterHost, PART_CloseButton` | `ClosedEvent` → `ClosedRouted` |
| 27 | `ArcBottomSheet` | Overlays | `PART_Layer, PART_Scrim, PART_Sheet, PART_Header, PART_Grabber, PART_CloseButton, PART_Title, PART_Description, PART_Body` | `OpenChangedEvent/DetentChangedEvent` → `…Routed` |
| 28 | `ArcPopover` | Overlays | `PART_TriggerHost, PART_Overlay` | — |
| 29 | `ArcTooltip` | Overlays | `PART_Overlay` | — |
| 30 | `ArcProgress` | Feedback | `PART_Bar, PART_LabelText, PART_ValueText, PART_Check, PART_Header, PART_ValuePanel` | — |
| 31 | `ArcStepper` | Feedback | `PART_Host` | `StepSelectedEvent` → `StepSelectedRouted` |
| 32 | `ArcTimeline` | Feedback | `PART_Host` | — |
| 33 | `ArcAvatar` | Feedback | `PART_Surface, PART_Initials, PART_Dot` | — |
| 34 | `ArcDataGrid` | DataDisplay | `PART_Toolbar, PART_HeaderHost, PART_Scroll, PART_Body, PART_FooterHost, PART_ColumnsPopup` | `SortChangedEvent/CellEditedEvent` → `…Routed` |
| 35 | `ArcTreeTable` | DataDisplay | `PART_Toolbar, PART_HeaderHost, PART_Scroll, PART_Body, PART_FooterHost, PART_Live` | `SortChangedEvent/RowActivatedEvent` → `…Routed` |
| 36 | `ArcTreeView` | DataDisplay | `PART_Host` | `SelectedEvent` → `SelectedRouted` |
| 37 | `ArcCalendar` | Pickers | `PART_Surface, PART_Title, PART_Previous, PART_Next, PART_Today` | `SelectedDateChangedEvent` → `SelectedDateChangedRouted` |
| 38 | `ArcDatePicker` | Pickers | `PART_Value, PART_Label, PART_Description, PART_Clear, PART_Calendar` | `SelectedDateChangedEvent` → `SelectedDateChangedRouted` |
| 39 | `ArcColorPicker` | Pickers | `PART_Swatch, PART_Name, PART_Hex, PART_Editor` | `ValueChangedEvent` → `ValueChangedRouted` |
| 40 | `ArcCarousel` | Media | `PART_Stage, PART_Dots, PART_Prev, PART_Next, PART_Play, PART_Status` | `IndexChangedEvent` → `IndexChangedRouted` |

### 2.1 当前覆盖率快照（M2 第三波，2026-11-07）

> 这是 **M2 第三波（G3a）** 的中间快照；第四波及后续波次继续补齐剩余控件/事件，最终以
> `pwsh -NoProfile tools/check-contracts.ps1` 输出为准。
> **计数口径**：`LegacyEvents*` 只统计可路由类型（继承链含 `Interactive/Control/TemplatedControl/...`）；
> 服务/模型/协调器类的 CLR 事件（无 UI 继承链、无法 RaiseEvent）计入 `NonRoutableEvents`，只统计、不计 exempt/red。

| 指标 | 数值 | 说明 |
|---|---:|---|
| TemplatedControls | 108 | 有 OnApplyTemplate / TemplatePart / 部件查找证据的控件（静态近似） |
| WithTemplatePart | 108 | 已声明 ≥1 个 `[TemplatePart]` |
| RoutedEventFields | 139 | `<Name>Event` 字段总数 |
| WithWrapper | 139 | 通过包装规则（`<Name>Routed` 或合法同名）的 CLR 包装 |
| LegacyEventsWithRouted | 99 | 可路由类型的旧 CLR 事件已有路由对应物 |
| LegacyEventsMissingRouted | 0 | 可路由类型仍缺路由对应物（warning/exempt，不阻塞） |
| NonRoutableEvents | 6 | 服务/模型类 CLR 事件：`ArcDeltaCoalescer.Flush`、`ArcAgentWorkspace.Changed`、`ArcAgentSessionModel.Changed`、`ArcNavigationService.Changed`、`ArcNotificationService.Shown/Dismissed`；不需要 RoutedEvent |
| MissingTemplatePart | 0 | 仍缺 `[TemplatePart]`（过渡 baseline 中；当前波次已完成） |
| UntargetedLookups | 0 | 无变体 `FindResource` 命中数（必须为 0；R10.11 红项） |

## 3. 如何给控件加模板部件

```csharp
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

[TemplatePart("PART_Input", typeof(TextBox))]
[TemplatePart("PART_Clear", typeof(ArcButton))]
public class ArcSearchBox : TemplatedControl
{
    private TextBox? _input;
    private ArcButton? _clear;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);                       // 必须调用 base
        _input = e.NameScope.Find<TextBox>("PART_Input");
        _clear = e.NameScope.Find<ArcButton>("PART_Clear");
        if (_clear is not null)
        {
            _clear.Click += (_, _) => _input?.Clear();
        }
    }
}
```

```xml
<ControlTheme x:Key="{x:Type local:ArcSearchBox}" TargetType="local:ArcSearchBox">
  <Setter Property="Template">
    <ControlTemplate>
      <Border x:Name="PART_Surface">
        <Grid ColumnDefinitions="Auto,*">
          <TextBox x:Name="PART_Input" />
          <ArcButton x:Name="PART_Clear" Content="×" />
        </Grid>
      </Border>
    </ControlTemplate>
  </Setter>
</ControlTheme>
```

检查清单：`PART_` 名一致、`base.OnApplyTemplate` 已调、每个查找都有 `[TemplatePart]`、模板改名有迁移说明。

## 4. 如何给控件加交互 RoutedEvent（严格 additive）

```csharp
using Avalonia.Interactivity;

// 1) 新 args 派生 RoutedEventArgs：无参 + RoutedEvent 构造
public sealed class ArcSearchBoxSubmittedEventArgs : RoutedEventArgs
{
    public ArcSearchBoxSubmittedEventArgs() { }
    public ArcSearchBoxSubmittedEventArgs(RoutedEvent routedEvent) : base(routedEvent) { }
    public string Text { get; init; } = string.Empty;
}

// 2) RoutedEvent 字段：<Name>Event + Bubble
public static readonly RoutedEvent<ArcSearchBoxSubmittedEventArgs> SubmittedEvent =
    RoutedEvent.Register<ArcSearchBox, ArcSearchBoxSubmittedEventArgs>("Submitted", RoutingStrategies.Bubble);

// 3) CLR 包装：<Name>Routed（冲突时 <Name>RoutedEvent）
public event EventHandler<ArcSearchBoxSubmittedEventArgs>? SubmittedRouted
{
    add => AddHandler(SubmittedEvent, value);
    remove => RemoveHandler(SubmittedEvent, value);
}

// 4) 双通知：旧 CLR 事件（若有）零变更 + 新路由面
private void RaiseSubmitted(string text)
{
    Submitted?.Invoke(this, new ArcSearchBoxSubmittedEventArgs { Text = text });       // 旧 CLR
    RaiseEvent(new ArcSearchBoxSubmittedEventArgs(SubmittedEvent) { Text = text });    // 新路由
}
```

注意事项：旧事件名/签名不得变；`SubmittedRouted` 是**新增**成员而不是替换；`RoutingStrategies.Bubble` 是默认策略；
不要在同一路径里重复 `RaiseEvent`（双通知 ≠ 双 RaiseEvent）。包装命名：上面的例子存在同名旧事件 `Submitted`，
所以必须用 `SubmittedRouted`；若类中没有同名旧 CLR 事件，包装可直接叫 `Submitted`（Avalonia 惯例）。

## 5. 键盘 / 焦点 / 自动化契约

| 能力 | 要求 |
|---|---|
| 焦点 | 可交互控件 `Focusable=true`；用 `:focus-visible` 提供可见焦点环（不能只靠 hover）；`IsTabStop`/`TabIndex` 遵循视觉顺序 |
| 键盘 | Button/IconButton：Enter/Space 激活；Popup/Dialog/Overlay：Escape 关闭、焦点环留在面板内；列表/标签：方向键、Home/End、PageUp/PageDown |
| 禁用 | `IsEnabled=false` + 可见禁用态；不得只隐藏事件或只改透明度 |
| 可访问性 | `AutomationProperties.Name`（图标按钮必填）、`HelpText`（非自明操作）、`AutomationProperties.LiveSetting`（Toast/Alert：Danger=Assertive） |
| 装饰图标 | 不进入可访问性树（`IsVisible=false` 或 `AutomationProperties.IsOffscreenBehavior=FromPoint` 约定） |
| 动效 | 遵循 `ArcThemeManager.ReducedMotion`；不自研帧时钟/绘制缓存（R8） |

### 5.1 移动 / 触控契约（T6）

手机端与桌面端的差异是“没有 hover、命中区更小、手势会竞争”，以下 7 条为 T6 的可回归口径（R11 静态检查 + R12 真机 smoke）：

1. **44px 命中区**：可点击/可拖拽目标至少 44×44 CSS px；视觉尺寸小时用 `Padding`/透明底扩大命中区（R11 `small-hit-target` 只做 informational 提示）。
2. **滚动条不常驻**：移动端默认 `Auto`/`Overlay`，不得用 `ScrollBarVisibility.Visible` 常驻滚动条；桌面专用控件需在文件标注 `desktop-only` 并在 R11 豁免。
3. **滚动/拖拽方向消歧**：首次移动超过阈值后锁定主轴（水平/垂直），横向手势不得触发纵向滚动；自定义滚动必须同时支持 `PointerPressed`/`PointerMoved`/`Gestures`，不能只处理 `PointerWheelChanged`（R11 `wheel-only-scroll`）。
4. **capture 兜底**：任何 `CapturePointer` 必须处理 `PointerCaptureLost`/`PointerCanceled`，用于来电、切后台、系统手势抢占等中断场景（R11 `missing-capture-lost`）。
5. **嵌套滚动 chaining**：内层滚动到边界后才把滚动手势交给父容器；横向轮播/纵向列表嵌套时优先消费主轴，避免“滑不动/滑过头”。
6. **hover-only 替代**：悬停才出现的信息/操作（tooltip、hover 操作条）必须有触摸等价路径（点击展开、长按、常显按钮），不能让触摸用户失去功能。
7. **点击时机**：`PointerPressed` 只做视觉/捕获状态，实际执行放到 `Tapped`/`Released`；避免手指滑动时误触发（R11 `press-executes-action`）。

## 6. 门禁与 CI

```powershell
pwsh -NoProfile tools/check-contracts.ps1                 # R10：契约统计 + 违规
pwsh -NoProfile tools/check-touch.ps1                     # R11：手机/触控静态检查
pwsh -NoProfile tools/mobile-smoke.ps1                    # R12：Gallery --smoke --mobile（无 --mobile/旧二进制则 SKIP）
pwsh -NoProfile tools/check-all.ps1                       # 11 行门禁；R10/R11 计入 Exempt/New，R12 SKIP 不算红
```

- `tools/contracts-baseline.json` 只保存**改造进行中**的 allowlist（kind+file+symbol），不是架构豁免；
  新控件/新文件不在 allow 内仍 RED；切片完成后 `allow=[]`。
- 报告：`.recon/gates/contracts-<timestamp>.md|json`；计数：
  `TemplatedControls / WithTemplatePart / RoutedEventFields / WithWrapper / LegacyEventsWithRouted / MissingTemplatePart / NonRoutableEvents / UntargetedLookups`。
  `check-all` 汇总表里 R10 行的 `Exempt` 列 = 过渡 allowlist + legacy warning 数；`NonRoutableEvents` 只写报告、不计入 Exempt/red。
- 当前例外：Charts 自绘 `Control` 的 4 个同名旧包装（`PointClick` 等）由 baseline 放行，待 ADR 0005 §4 决策；
  旧 CLR 事件缺路由对应物只统计（warning），不算 red。

## 7. 版本与兼容

- 新增契约是 **additive**：旧 CLR event / handler 签名 / `PART_` 名在同一 minor 内保持不变。
- `PART_*` 在 M2 起视为公开契约；改名必须保留旧名 `[TemplatePart]` 至少一个 minor + CHANGELOG 迁移说明。
- 删除/重命名 `<Name>Routed` 或改变 args 基类属于 breaking，需要新 major + ADR。
- 第三方控件建议：先加 `[TemplatePart]`/RoutedEvent，再用 `check-contracts -NoBaseline` 自检，最后在 CI 跑 `check-all`。
