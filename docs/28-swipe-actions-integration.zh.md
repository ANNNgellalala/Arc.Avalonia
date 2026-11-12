# 28 · 滑动操作集成指南：收起态动作不得参与绘制

> 日期：2026-11-10
> 适用：所有使用 `ArcSwipeActions` 的宿主，以及「可点击行 + 横向滑动开合动作」需要自绘手势的宿主
> 背景：[docs/27 会话列表隐藏动作透色根因分析](27-swipe-action-color-leak-rca.zh.md)
> 相关：[docs/25 手机端使用建议](25-mobile-usage-guidance.zh.md) §3/§5、[docs/24 触控滑动审计](analysis/24-touch-scroll-audit.zh.md)、`Docs/swipe-actions.json`
> 说明：文中 `DSHRemote/...` 是外部仓库路径（相对该仓库根），其余为 Arc.Avalonia 仓库相对路径。

---

## 0. 一句话

**收起态的动作必须「不参与绘制」，而不是「被卡片盖住」。**

`ArcSwipeActions` 已经这样做了（两侧动作 host 在 `|offset| ≤ 0.5` 时 `IsVisible=false`）；如果你绕过组件自绘滑动，这条不变式要自己实现——否则 Danger 动作会从圆角卡片的抗锯齿边缘渗出来，形成 1–2 px 的红色细边（完整案例见 [docs/27](27-swipe-action-color-leak-rca.zh.md)）。

---

## 1. 核心不变式：收起态动作不得参与绘制

### 1.1 规则

行处于**收起态**（未滑动、未停靠打开）时，动作层必须满足下面任意一条：

| 做法 | 效果 | 适用 |
|---|---|---|
| `IsVisible = false` | 不测量、不绘制、不参与命中与 Tab 焦点；最彻底 | **默认推荐** |
| `Opacity = 0` | 不绘制但仍参与命中/焦点；不触发布局 | 需要保留布局、做淡入淡出时；建议同时 `IsHitTestVisible=false`、`IsTabStop=false` |
| 不加入视觉树（延迟创建，收起时移除） | 不绘制也不占内存；创建有成本 | 动作数量多、按行懒加载时 |

**禁止**：把动作层常驻绘制在卡片下层，靠 `Border` 的圆角 + 不透明背景「盖住」它。

### 1.2 为什么遮挡不成立

- 卡片的圆角轮廓在渲染时是**抗锯齿**的：弧段上每个像素的覆盖率 `c ∈ (0,1)`，最终颜色是 `c × 卡片色 + (1−c) × 下层色`。`1−c` 的部分会把下层颜色按比例带上来。
- 动作按钮与卡片通常共享同一轮廓（同宽、同高、右对齐、同圆角）。于是卡片边缘的「半覆盖像素」正好落在动作按钮上，Danger 色就从右边缘的上下圆角渗出来（实测覆盖率 7%–38%）。
- 如果卡片背景/描边本身带 alpha（半透明表面、玻璃/毛玻璃效果、`#AARRGGBB` 描边），`(1−c)` 会沿**整条边**存在，渗色就不再局限于圆角。
- 高 DPI 不会让问题消失：2x/3x 下弧段覆盖的**设备像素更多**，只是每像素的色差更小；缩放截图反而会把接缝平均掉，让问题更难被看见（见 §5 与 docs/27 §3）。

一句话：**抗锯齿边缘永远「半透明」，把颜色放在它下面就是让颜色参与绘制。**

### 1.3 验收条件

- 收起态：行外圈条带的 Danger 色像素数必须为 **0**；
- 拖动中/停靠打开：同一判据必须能扫到动作颜色（正控制，证明扫描有效）；
- 松手回位：重新回到 0。

判据与脚本见 [docs/27 §6](27-swipe-action-color-leak-rca.zh.md) 与 `tools/verify-swipe-leak.ps1`。

---

## 2. 何时用 `ArcSwipeActions`

### 2.1 适用与不适用（与 `Docs/swipe-actions.json` 对齐）

`whenToUse`（`swipe-actions.json:10-14`）：

1. 触屏或触控板优先的列表（邮件、通知、审批）需要每行一两个快速动作；
2. 希望在保持行高不变的前提下，让归档、删除这类高频动作随时可达；
3. 桌面端也需要一条右键菜单之外的、键盘可触达的快捷路径。

`whenNotToUse`（`swipe-actions.json:16-20`）：

1. 动作需要文字说明或存在多个分支时，改用 dropdown-menu 或 context-menu，滑出的按钮放不下解释；
2. **「行本身要响应点击（打开详情）时，把可点击元素（Button 或带 Tapped 的表面）放进 Content：轻点会到达内容，横向拖动才会被滑动层接管；不要再把整个滑动行包进外层 Button，那会让按钮先消费按压与点击语义。」**
3. 需要拖拽调整顺序或搬运内容时，用专门的拖拽排序组件，方向与语义都不同。

> 第 2 条在 2026-11-10 的组件增强后从「不要用」变成了「有标准写法」：可点击元素放进 `Content`，轻点与横滑共存，见 §2.7。

### 2.2 API 速查

| 成员 | 说明 | 位置 |
|---|---|---|
| `Leading` / `Trailing` | 左右动作列表（`IList<ArcSwipeAction>`）；右滑露出 Leading，左滑露出 Trailing | `ArcSwipeActions.cs:71-75,122-134` |
| `Label` | 行的无障碍名称（邮件主题、会话标题），会写入 `AutomationProperties.Name` | `:77-78,136-140,181-186` |
| `FullSwipe` | 默认 `true`；大滑越过阈值直接执行该侧最外侧动作 | `:80-81,143-147` |
| `IsOpen` | 只读，任一侧是否正在露出（`\|offset\| > 1`） | `:162-163` |
| `ActionInvoked` | 动作被点选或由满滑执行时触发，携带 `ArcSwipeAction` | `:149-160,269-275` |
| `ActionInvokedRouted` / `ActionInvokedEvent` | 同一事件的路由（冒泡）版本 | `:150-158` |
| `Open(int side)` | 代码打开一侧：`-1` Trailing，`+1` Leading | `:278-287` |
| `Close()` | 收起；若焦点在行内则把焦点还给行 | `:290-297` |
| `ArcSwipeAction` | `Label`、`Icon`（Lucide 名）、`Tone`（`Neutral`/`Accent`/`Danger`）、`Action` | `ArcMediaModels.cs:14-49` |
| 动作宽度 | 固定 `ActionWidth = 84` px/个（`ArcSwipeActions.ActionWidth`） | `ArcSwipeActions.cs:67,223-267` |

注意两点：

- `ArcSwipeAction.KeepRow`（`ArcMediaModels.cs:44-45`）已在模型上声明，但 `ArcSwipeActions.Invoke` 目前总是 `Close()`（`ArcSwipeActions.cs:269-275`）——**不要依赖「执行后保持展开」**。
- 动作按钮的 Tab 停靠只在露出时打开（`ApplyOffset:339-347`、`AfterSettle:304-319`），收起态它们不是 Tab 焦点。

### 2.3 正确用法示例

```xml
<!-- XAML：行内容放在 Content，动作放 Leading/Trailing -->
<media:ArcSwipeActions Label="Invoice 2041">
  <media:ArcSwipeActions.Trailing>
    <media:ArcSwipeAction Label="Archive" Icon="archive" />
    <media:ArcSwipeAction Label="Delete" Icon="trash-2" Tone="Danger" />
  </media:ArcSwipeActions.Trailing>
  <Grid ColumnDefinitions="*,Auto">
    <!-- 行内容：注意这里不要再挂 PointerPressed 打开详情（§2.1 第 2 条） -->
  </Grid>
</media:ArcSwipeActions>
```

```csharp
var row = new ArcSwipeActions
{
    Label = "Design review",
    Content = BuildMailRow("Maya Chen", "Design review", "9:41"),
    Leading = new List<ArcSwipeAction>
    {
        new("Unread", "mail", ArcSwipeActionTone.Accent) { Action = MarkUnread }
    },
    Trailing = new List<ArcSwipeAction>
    {
        new("Archive", "archive") { Action = Archive },
        new("Delete", "trash-2", ArcSwipeActionTone.Danger) { Action = Delete }
    }
};

row.ActionInvoked += (_, action) => ShowUndoToast($"{action.Label} 已执行");
// 需要程序化打开某一侧时：row.Open(-1); row.Close();
```

### 2.4 Danger tone、满滑与撤销

- 破坏性动作放**该侧最外侧**：Trailing 的最后一个、Leading 的第一个，因为满滑执行的就是它（`CommitTrailing:587-596`、`CommitLeading:598-607`）。
- 满滑阈值：`max(动作总宽 + 48, 行宽 × 0.56)`（`ArcSwipeActions.cs:543,562`）；未达到就按「露出宽度的一半」决定停靠还是回位（`:551-558,570-577`）。
- 删除/归档执行后用带「撤销」的 toast（`Docs/swipe-actions.json` 的 relations 已把 toast 标为 complement），因为满滑是不可逆手势。

### 2.5 键盘与读屏路径（不要只做触摸）

| 操作 | 行为 | 位置 |
|---|---|---|
| 聚焦行后按 `Right` | 露出 Leading；若已开则把焦点移入第一个动作 | `ArcSwipeActions.cs:652-665` |
| 聚焦行后按 `Left` | 露出 Trailing；若已开则把焦点移入第一个动作 | `:637-650` |
| `Tab` | 露出后走进动作按钮（按钮此时才 `IsTabStop=true`） | `:339-347` |
| `Escape` | 收起，焦点回到行 | `:667-671`、`Close():290-297` |
| 读屏 | `Label` → 行名；`PART_Status` live region 播报 `Actions: …` / `Actions hidden` | `:181-186,374-380`、主题 `ArcSwipeActions.axaml:109-113` |

### 2.6 使用 `ArcSwipeActions` 时的「不要」

- 不要重模板把 `PART_LeadingHost/PART_TrailingHost` 画到 `PART_Surface` 上面或做常驻绘制；要改模板就保留「`|offset| ≤ 0.5` 时两侧 host `IsVisible=false`」这一条。
- 不要给 `ArcSwipeActions` 的背景做成半透明/玻璃，再指望动作 host 在下面也看不见（道理同 §1.2）。
- 不要在行内容上再挂第二套「起拖」的指针处理器或自行 `CapturePointer`，与组件的延迟捕获抢指针；长按多选、拖拽排序等冲突手势见 §3。
- **不要把整个 `ArcSwipeActions` 再包进外层 `Button`**：外层按钮**拦不住横向拖动**（行在横向锁定后仍会抢到指针并打开），却会让一次轻点触发多次激活——纯内容行 + 外层 Button 实测 `outer.Click=1` 且 `row.Tapped=1`（2 次），Content=Button + 外层 Button 实测 `inner.Click=1 + outer.Click=1 + row.Tapped=1`（3 次），同时造成嵌套按钮语义重复（T7 探针，见 [`analysis/swipe-leak/interaction-probe.md`](analysis/swipe-leak/interaction-probe.md)）。可点击行请按 §2.7 把 Button 放进 `Content`，或给 `Content` 的表面接 `Tapped`。

> 真机手感结论（起判、方向锁、回弹、纵向滚动不误开）见 [docs/25 §3 与 §5.5](25-mobile-usage-guidance.zh.md)。

### 2.7 可点击 / 可交互行（2026-11-10 增强）

**行内容本身可以是可点击控件。** 从 2026-11-10 的组件增强起，`ArcSwipeActions` 把按压改为**隧道阶段观察**、横向锁定后才夺取指针，因此「轻点内容」与「横滑露出动作」可以共存（`ArcSwipeActions.cs:107-119,382-585`）。`Docs/swipe-actions.json` 的 `purpose` 第 4 段与 `variant-tappable-row`（`:27`）描述的就是这个用法。

#### 正确写法：把可点击元素放进 `Content`

```xml
<!-- 整行可点击：Button 作为 Content；横向拖动仍由 ArcSwipeActions 接管 -->
<media:ArcSwipeActions Label="Invoice 2041">
  <media:ArcSwipeActions.Trailing>
    <media:ArcSwipeAction Label="Archive" Icon="archive" />
  </media:ArcSwipeActions.Trailing>

  <Button Command="{Binding OpenInvoiceCommand}"
          Background="Transparent" BorderThickness="0" Padding="0"
          HorizontalAlignment="Stretch"
          HorizontalContentAlignment="Stretch">
    <!-- 原来的行内容（头像 / 标题 / 时间……） -->
  </Button>
</media:ArcSwipeActions>
```

```csharp
var row = new ArcSwipeActions
{
    Label = "Invoice 2041",
    Trailing = new List<ArcSwipeAction> { new("Archive", "archive") },
    Content = new Button
    {
        Command = openInvoice,
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0),
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Content = BuildMailRow("Accounts", "Invoice 2041", "Yesterday")
    }
};
```

也可以用「行表面 `Tapped`」：`Content` 仍是普通布局，把点击处理挂在表面（或它的子元素）上：

```xml
<media:ArcSwipeActions Label="Invoice 2041">
  <media:ArcSwipeActions.Trailing>
    <media:ArcSwipeAction Label="Archive" Icon="archive" />
  </media:ArcSwipeActions.Trailing>

  <Border Background="Transparent" Tapped="OnRowTapped">
    <!-- 行内容 -->
  </Border>
</media:ArcSwipeActions>
```

```csharp
private void OnRowTapped(object? sender, TappedEventArgs e) => openInvoice();
```

两种写法都经过 T1/T2（纯内容行）与 T3/T4（Content=Button）实测：轻点触发一次、横拖只开行不触发点击。区别只是「点击语义挂在哪一层」——Button 自带按下/悬停/键盘视觉与 `Command`，`Tapped` 更轻、适合整行表面已是自定义视觉的情况。样式上通常把内层 Button 做成透明背景、无边框、内容 Stretch，或让表面 `Background="Transparent"`，看起来仍然是原来的行表面。

#### 互斥仲裁：为什么轻点和横滑不会互相吞掉

| 阶段 | 组件做什么 | 内层可点击元素的结果 |
|---|---|---|
| `PointerPressed`（隧道） | 记录起点 / 起始偏移 / 按下时是否已展开；**不 Handled、不 CapturePointer**（行收起时，见 `:419-425`）；行已展开时 `e.Handled = true` 消费按压（`:411-417`） | 收起态：收到按下，进入 pressed 视觉；展开态：收不到（点行只会收起） |
| `PointerMoved` | 位移 < `DragLock = 6` px（`:68,461`）不动；纵向主导则放弃本次按压（`:466-471`）；**横向锁定后**才 `Capture(this)`（`:478-482`） | 横向锁定 → 内层 Button 收到 `PointerCaptureLost` → **Click 被取消**；纵向滚动/轻点不受影响 |
| `PointerReleased` | 拖动结束按阈值停靠/回位（`:506-585`）；轻点且行原本展开 → `Close()` + `Handled`（`:519-524`） | 收起态轻点：事件不 Handled，Button.Click / Tapped 正常触发；展开态轻点：只收起，不触发内容 |
| `PointerCaptureLost` | 复位按下/拖动状态并 `Close()`（`:609-621`） | 保证手势中断时不会留下半开行或卡住的 Click |

一句话记忆：**按下先观察、横向锁定才夺权**。轻点时组件从不捕获指针，点击自然生效；横拖时组件夺取指针，内层按钮的 Click 被系统取消，于是「滑动」不会被误判成「点击」。

#### 实测保证（headless 交互探针 T1–T6，全部 PASS）

`poc/SwipeInteractionProbe/`（Avalonia Headless，走真实 routed event / pointer capture / 控件模板管线）对上述契约逐条断言，报告见 [`analysis/swipe-leak/interaction-probe.md`](analysis/swipe-leak/interaction-probe.md)：

| 用例 | 手势 | 实测 |
|---|---|---|
| T1 | 纯内容行轻点 | `row.Tapped=1`，行不展开 |
| T2 | 纯内容行横拖 dx=-120 | 行展开，`Tapped=0` |
| T3 | `Content=Button` 行轻点 | `Button.Click=1`，行不展开 |
| T4 | 从 `Content` 的 Button 上起手横拖 | 行展开，`Button.Click=0`（最关键的一条） |
| T5 | 已展开行轻点 | 收起，`Button.Click=0`（不会误触内容） |
| T6 | 纵向拖动 dy=+80 | 行保持收起（纵向滚动不被吃掉） |

T4/T5 就是本章的核心保证：**从可点击元素上起手横拖会取消点击并打开行；展开态轻点只收起、不触发内容。**

#### 注意事项

- **不要把整个 `ArcSwipeActions` 再包进外层 `Button`。** T7 实测：外层 Button **拦不住横向拖动**（行仍会打开、`outer.Click=0`），但轻点会叠加触发——纯内容行 2 次激活（`outer.Click=1` + `row.Tapped=1`）、Content=Button 行 3 次激活（`inner.Click=1` + `outer.Click=1` + `row.Tapped=1`）。正确写法是把 Button 放进 `Content`（T3/T4 验证）或给 `Content` 表面接 `Tapped`（T1 验证）。证据：[`analysis/swipe-leak/interaction-probe.md`](analysis/swipe-leak/interaction-probe.md)、`poc/SwipeInteractionProbe/`。
- **点击逻辑放 `Click`/`Tapped`，不要放 `PointerPressed`。** 按下即执行会在横拖开始前误触发；组件的设计正是把「执行」留给内容的 Click。
- **不要在 Content 内自己 `CapturePointer` 或实现第二套横滑**，那会与组件的延迟捕获抢指针；长按多选、拖拽排序这类手势见 §3。
- **键盘/焦点**：内容 Button 可以 Tab 聚焦并用 Enter/Space 触发；行获得焦点时 `Left`/`Right` 会露出动作（`OnKeyDown:633+`）。如果内容控件需要独占方向键，在自己的 `KeyDown` 里处理并 `e.Handled = true`。
- 行已展开时，轻点行表面 = 收起（组件在按下阶段就消费事件），不会漏到内容按钮上。

#### 验证方式

文档页第 0 行就是「Button 作为 Content」的示例（`SwipeActionsPage.cs:39-46,88-104`），并带一个点击计数文案：轻点计数 +1、横向拖动露出动作且计数不变、松手回位后再轻点仍正常。`Docs/swipe-actions.json` 的 `variant-tappable-row`（`:27`）与示例「行内容可点击：轻点与滑动共存」（`:66`）与代码保持一致。

> 与 §3 的分工：本节覆盖「行内容可点击」这一最常见的可交互行；行内还有长按多选/拖拽排序等**第二个水平手势**时，仍属于 §3 需要统一仲裁的情况。

---

## 3. 何时仍建议自绘滑动

「行本身要响应点击」**不再是** `ArcSwipeActions` 的限制：把可点击元素放进 `Content` 即可（§2.7）。下列情况仍建议自绘，或与组件做更细的手势协同：

1. **行内已有与横滑冲突的手势**：长按多选、拖拽排序、自定义滑动面板等，需要统一仲裁指针（组件只负责「水平拖拽露出动作」，不理解其他手势）；
2. **动作层需要完全自定义**：非 84 px 动作格、自定义动作动画/布局，或动作内嵌复杂状态；
3. **整行要作为单一可拖拽对象**（拖拽排序、搬运）：方向与语义都不同，按 `swipe-actions.json:19` 的 `whenNotToUse` 第 3 条用专门的拖拽排序组件。

自绘 ≠ 重写组件：它只替换「手势 + 动作显隐」这一层，动作按钮仍然建议用 `ArcButton`/普通 `Button` + Arc token 绘制；自绘时仍必须遵守 docs/27 的「收起态动作不参与绘制」不变式。

> 历史注记：DSHRemote 的会话列表曾因「行可点击」而自绘滑动（2026-11-10 之前的结论，见 docs/27 §2.5）；组件增强后，这类场景可以直接用 `ArcSwipeActions` + `Button` 作为 `Content`，不必再自绘。

---

## 4. 自绘手势的正确骨架

### 4.1 状态机

```
Idle
 └─(PointerPressed：记录起点；不 Handled、不 Capture)
     Pressed
      ├─(纵向主导 |dy| > |dx|) ──────────────→ Idle / 交给 ScrollViewer
      ├─(位移未超阈值) ────────────────────→ Pressed
      └─(横向主导且 |dx| > 起判阈值)
          Dragging：CapturePointer；Transform.X = clamp(...)
           ├─(X < 0，行确实被拖开) ────────→ 动作 paint=true
           ├─(PointerReleased：X ≤ -停靠阈值) → 停靠 -动作宽；paint=true → Idle
           ├─(PointerReleased：未过阈值) ────→ 回位 0；paint=false → Idle
           └─(PointerCaptureLost / 窗口失焦) → 回位 0；paint=false → Idle
```

关键点：**动作的 `paint=true` 发生在「横向锁定且行确实被拖开（X < 0）」之后**，不是等到位置到停靠点；`paint=false` 发生在回位、收起、切换行、捕获丢失、行首次创建。DSHRemote 的实现是在横向锁定时就 `paint=true`（向右拖动、不产生露出时会短暂多画一层，但此时行没被拖开，不影响结果）；本文骨架按位移判断更稳。

### 4.2 参数（按 Arc 组件实际值，不要拍脑袋）

| 参数 | Arc 组件实际值 | DSHRemote 自绘值 | 建议 |
|---|---|---|---|
| 轴锁 / 起判 | `DragLock = 6 px`（`ArcSwipeActions.cs:68,461`） | `SwipeThreshold = 10 px`（`SessionsView.axaml.cs:14`） | 6–10 px；小于 6 会误触发点击，大于 10 手感发滞 |
| 纵向放弃 | `\|dy\| > \|dx\| + 2` 时放弃（`:466-471`） | `\|dx\| <= \|dy\|` 时返回（`:119-124`） | 必须在 `Handled` 之前判断，否则吃掉纵向滚动 |
| 停靠阈值 | 露出宽度的 **50%**（`:551-558,570-577`） | `OpenThreshold = 48`（= 96 × 50%，`:16`） | 35–48 px（单动作）；本质是目标宽度的一半，按动作宽推导，不要拍一个绝对值 |
| 动作宽 | `ActionWidth = 84`（`:67`） | `ArchiveWidth = 96`（`:15`） | 84–96 px；与卡片圆角、行宽一起定 |
| 越界阻尼 | `0.24`（`:489-504`） | 直接 `Math.Clamp`（`:130-132`） | 有阻尼更像组件，钳制更简单；二者都可，但不要两者都不做 |
| 满滑执行阈值 | `max(动作总宽 + 48, 行宽 × 0.56)`（`:543,562`） | 无（自绘未做 FullSwipe） | 做满滑就照此公式，不要只按固定 px |
| 停靠动画 | `ArcMediaTween` + `ArcTokens.DurationStandard`（`:299-302`） | 直接赋值（无动画） | 至少回位要有过渡，否则像卡住 |

### 4.3 最小片段（XAML + C#）

XAML（行模板：动作在下、卡片在上，但**动作收起态 `Opacity=0`**）：

```xml
<Grid Margin="0,0,0,8" ClipToBounds="True">
  <!-- 动作：收起态不绘制；显隐由手势状态机统一切换。类名/配色由宿主定义，Danger 填充便于暴露问题 -->
  <Button x:Name="ArchiveAction"
          Classes="row-action" Content="归档" Width="96"
          IsVisible="{Binding CanArchive}"
          Opacity="0"
          HorizontalAlignment="Right" VerticalAlignment="Stretch" />

  <!-- 行表面：点击打开 + 横向拖动 -->
  <Border Classes="row-card"
          PointerPressed="OnRowPressed"
          PointerMoved="OnRowMoved"
          PointerReleased="OnRowReleased"
          PointerCaptureLost="OnRowCaptureLost">
    <!-- 行内容 -->
  </Border>
</Grid>
```

C#（状态机 + 动作显隐；简化自 DSHRemote 修复后实现，完整 diff 见 [docs/27 §5](27-swipe-action-color-leak-rca.zh.md)）：

```csharp
private const double SwipeThreshold = 10;   // 起判/轴锁
private const double OpenThreshold = 48;    // 停靠阈值（≈ 动作宽的一半）
private const double ActionWidth = 96;

private sealed class RowState
{
    public TranslateTransform Transform { get; } = new();
    public Button? Action { get; set; }
    public bool Open { get; set; }

    // 收起态不参与绘制：卡片的圆角抗锯齿边缘盖不住下层 Danger 填充
    public void SetActionPainted(bool painted)
    {
        if (Action is null) return;

        Action.Opacity = painted ? 1 : 0;
        Action.IsTabStop = painted;        // 隐藏时不要占 Tab 焦点
        Action.IsHitTestVisible = painted; // 隐藏时不要吃命中测试
    }
}

private Border? _gestureRow;
private Point _start;
private bool _dragging, _moved;

private RowState State(Border row)
{
    if (row.Tag is RowState existing) return existing;

    var state = new RowState { Action = FindAction(row) };   // 模板里的 x:Name="ArchiveAction"
    row.Tag = state;
    row.RenderTransform = state.Transform;
    state.SetActionPainted(false);                           // 行首次创建：收起态
    return state;
}

private void OnRowPressed(object? sender, PointerPressedEventArgs e)
{
    if (sender is not Border row) return;
    State(row);
    _gestureRow = row;
    _start = e.GetPosition(row);
    _dragging = _moved = false;
    // 不设置 e.Handled、不 CapturePointer：点击与纵向滚动先走正常路径
}

private void OnRowMoved(object? sender, PointerEventArgs e)
{
    if (sender is not Border row || row != _gestureRow) return;
    var state = State(row);
    var p = e.GetPosition(row);
    var dx = p.X - _start.X;
    var dy = p.Y - _start.Y;

    if (!_dragging)
    {
        if (Math.Abs(dx) > SwipeThreshold || Math.Abs(dy) > SwipeThreshold) _moved = true;

        // 未超阈值，或纵向主导 → 不接管，让外层 ScrollViewer 继续滚
        if (Math.Abs(dx) <= SwipeThreshold || Math.Abs(dx) <= Math.Abs(dy)) return;

        _dragging = true;
        e.Pointer.Capture(row);
    }

    // 只允许向左露出右侧动作；已展开的行从 -ActionWidth 继续拖
    var baseOffset = state.Open ? -ActionWidth : 0;
    state.Transform.X = Math.Clamp(baseOffset + dx, -ActionWidth, 0);
    state.SetActionPainted(state.Transform.X < -0.5);   // 真的拖开了才绘制
    e.Handled = true;
}

private void OnRowReleased(object? sender, PointerReleasedEventArgs e)
{
    if (sender is not Border row || row != _gestureRow) return;
    var state = State(row);
    _gestureRow = null;               // 先清引用：随后 Capture(null) 会触发捕获丢失

    if (_dragging)
    {
        _dragging = false;
        e.Pointer.Capture(null);

        state.Open = state.Transform.X <= -OpenThreshold;
        state.Transform.X = state.Open ? -ActionWidth : 0;
        state.SetActionPainted(state.Open);   // 打开保持绘制；回位立即隐藏
        e.Handled = true;
        return;
    }

    if (_moved) { _moved = false; e.Handled = true; return; }   // 滑动不被当成点击

    if (state.Open)                                          // 已展开 → 点击先收起
    {
        state.Open = false;
        state.Transform.X = 0;
        state.SetActionPainted(false);
        e.Handled = true;
        return;
    }

    // 未移动且未展开 → 点击打开详情（行可点击的场景）
}

private void OnRowCaptureLost(object? sender, PointerCaptureLostEventArgs e)
{
    if (sender is not Border row || row != _gestureRow) return;
    var state = State(row);
    _gestureRow = null;
    _dragging = false;
    state.Open = false;
    state.Transform.X = 0;
    state.SetActionPainted(false);    // 复位：不留「动作可见但行已回位」的残局
}
```

要点注释：

- **动作显隐与位移分离**：`Opacity` 只跟「是否露出」走；`Transform.X` 只跟位移走。不要把两者绑在同一个值上，也不要在 XAML 里把 `Opacity` 绑成 `1`。
- **先清 `_gestureRow` 再 `Capture(null)`**：正常释放也会触发 `PointerCaptureLost`，顺序反了会把刚停靠好的行复位掉。
- **`PointerCaptureLost` 必须复位**：窗口失焦、行被移出视觉树、滚动容器抢捕获都会触发；不复位会留下「动作显示着但行回位了」的残局。
- **点击与滑动互斥**：按下不 `Handled`；只有横向锁定后才 `Handled`；释放时用 `_moved` 抑制误点击。
- **生产实现**：`TranslateTransform`、动作引用、`Open` 放进行状态（`row.Tag`），不要每次 `new`，也不要在 `Moved` 里反复查找控件。
- **不要用「卡片遮挡」代替 `Opacity=0`**：卡片圆角弧段会漏（§1.2）。

### 4.4 圆角与裁剪注意

- 行容器 `ClipToBounds=true` 可以防止拖动时动作画出列表边界，但**它裁的是矩形**，不能替代圆角遮挡；
- 动作按钮与卡片的圆角半径保持一致是**视觉**要求，不是遮蔽手段；
- 如果卡片背景/描边带 alpha，收起态的渗色会从圆角扩展到整条边——只能靠「不绘制」解决；
- 不要给动作层加 `Opacity=1` 的父容器「兜底」，父级不会改变子元素的绘制事实。

### 4.5 自绘必须补的键盘/无障碍

`ArcSwipeActions` 免费提供的键盘路径，自绘要自己补：

- `Escape` 收起并归还焦点；行获得焦点时 `Left/Right` 能露出对应动作；
- 动作按钮露出时才 `IsTabStop=true`，收起时 `IsTabStop=false`（否则 Tab 会停在看不见的按钮上）；
- 行的 `AutomationProperties.Name`、动作按钮的 `AutomationProperties.Name`；
- 用 `Opacity=0` 隐藏时，同时把 `IsHitTestVisible`、`IsTabStop` 设为 `false`（本次 DSHRemote 修复即如此，见 [docs/27 §5.2–5.3](27-swipe-action-color-leak-rca.zh.md)）；否则透明按钮仍可能被命中或 Tab 到。

---

## 5. 排查清单

| # | 检查项 | 方法与期望 |
|---|---|---|
| 1 | 收起态右边缘与四个角 | 截原始分辨率图，放大 **4–8 倍最近邻**看弧段；红边通常就藏在右侧上/下圆角 |
| 2 | 2x / 3x DPI | 高分屏下弧段覆盖更多设备像素；只在 1x 桌面看容易漏；2x/3x 必看 |
| 3 | 高对比隔离 | 临时把宿主背景换成品红/绿色，或把动作填充换成 `Danger`/高饱和色，渗色立刻显眼 |
| 4 | 像素断言 | 用 `R≥60 && R−G≥25 && R−B≥25` 之类判据扫**外圈条带**；同时留一个「动作已露出」的正控制，避免判据失效 |
| 5 | 别只看缩略图 | 聊天软件/文档预览里的截图会被缩放重编码，1–2 px 接缝被平均（docs/27 §3 的教训） |
| 6 | 正/负对照 | 同一场景抓「收起 / 拖动中 / 松手回位」三态；收起 0、拖动非 0、回位 0 才算修好 |
| 7 | 重模板自查 | 动作 host 是否在 `\|offset\| ≤ 0.5` 时 `IsVisible=false`？是否被放在 surface 下层常驻绘制？ |
| 8 | 文字误报 | 整行矩形扫描会把白字的 LCD 子像素抗锯齿当危险色（如 `(160,25,25)`），只扫条带或排除文字区 |

脚本与证据：`tools/verify-swipe-leak.ps1`（Arc 文档页回归）、`tools/capture-window.ps1`（宿主窗口原始像素）、`docs/analysis/swipe-leak/`（本次案例的 before/after/mid-drag/after-release 截图与 `pixel-assert.md`）。

> 与 [docs/25 §3](25-mobile-usage-guidance.zh.md) 的分工：§3 给的是「触摸手感」结论（起判、方向锁、回弹、纵向滚动不误开）；本文补的是「视觉/绘制」不变式（收起态零危险色像素）。两者都要过才算滑动行验收通过。

---

## 6. 验收清单

- [ ] 收起态：外圈条带危险色像素 **0**（`verify-swipe-leak.ps1` 或宿主自建断言）
- [ ] 拖动中：同一判据能扫到动作颜色（正控制）
- [ ] 松手回位 / 点击收起 / 关闭其他行：回到 0
- [ ] 2x/3x DPI 下重复上述检查
- [ ] `Escape` 收起、`Left/Right` 露出、`Tab` 进动作、焦点归还
- [ ] 行可点击与滑动开合互不误触（点击不触发滑动、滑动不触发打开）
- [ ] 纵向滚动不被横向手势吃掉
- [ ] 无「收起态动作仍可被 Tab 命中」的隐形焦点

---

## 7. 相关文件

| 内容 | 路径 |
|---|---|
| 组件实现 | `src/Components/Arc.Avalonia.Media/Controls/ArcSwipeActions.cs` |
| 组件主题（圆角/裁剪/surface/host） | `src/Components/Arc.Avalonia.Media/Themes/ArcSwipeActions.axaml` |
| 动作模型（Tone/Label/Icon/Action） | `src/Components/Arc.Avalonia.Media/Controls/ArcMediaModels.cs:14-49` |
| 文档数据（whenToUse/whenNotToUse/示例/API） | `src/Docs/Arc.Avalonia.Docs/Components/Arc.Avalonia.Media/Docs/swipe-actions.json` |
| 文档页（含收起态 + Danger 回归行） | `src/Docs/Arc.Avalonia.Docs/Components/Arc.Avalonia.Media/Pages/SwipeActionsPage.cs:56-68` |
| 像素断言脚本 | `tools/verify-swipe-leak.ps1` |
| 本次透色案例的完整证据 | [docs/27](27-swipe-action-color-leak-rca.zh.md)、`docs/analysis/swipe-leak/` |
