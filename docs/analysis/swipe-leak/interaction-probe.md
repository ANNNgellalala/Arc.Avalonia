# ArcSwipeActions 可点击行：headless 交互探针证据（task-3）

日期：2026-11-10（Asia/Shanghai）
探针项目：`poc/SwipeInteractionProbe/`（不入 `ArcUI.slnx`）
被测控件：`src/Components/Arc.Avalonia.Media/Controls/ArcSwipeActions.cs`（隧道监听版本：`OnPreviewPointerPressed/Moved/Released` + 构造函数 `AddHandler(..., RoutingStrategies.Tunnel)`，横向锁定后才 `Capture`，展开态按压被消费，`OnPointerCaptureLost` 复位）

## 1. 环境与命令

- Avalonia **12.1.5**（probe 显式覆盖 `AvaloniaVersion`；本机离线缓存只有 12.1.5 的 `Avalonia.Headless`）。被引用的 Arc 程序集仍按仓库 12.1.4 构建，12.1.x 补丁行二进制兼容，运行时统一到 12.1.5。
- Avalonia.Headless + `UseHeadlessDrawing = true`：不创建桌面窗口，但走真实输入管线（routed events / pointer capture / control template）。
- `ArcThemeManager.ReducedMotion = true`：行 tween 立即落到目标值，`Open`/`Close` 同步完成，断言确定。
- 指针注入：`window.MouseDown / MouseMove / MouseUp`，每次事件后 `Dispatcher.UIThread.RunJobs()` + `AvaloniaHeadlessPlatform.ForceRenderTimerTick(1)`。

```powershell
# 构建（单节点）
dotnet build poc/SwipeInteractionProbe/SwipeInteractionProbe.csproj -m:1

# 运行（不要直接启动 exe，走 dotnet host）
dotnet poc/SwipeInteractionProbe/bin/Debug/net10.0/SwipeInteractionProbe.dll
```

退出码：`0` = T1–T6 全部符合契约；`1` = 有强制用例失败；`2` = 探针崩溃。两次运行的输出除换行符外逐字节一致（确定性已验证）。

## 2. 原始输出（`interaction-probe-output.txt`）

```text
Swipe interaction probe | Avalonia 12.1.5.0 | headless drawing | reduced motion
window 760x820, row width 640

T1 PASS | taps=1 isOpen=False | tap on a pure-content row (expect taps=1, isOpen=False)
T2 PASS | isOpen=True taps=0 | horizontal drag dx=-120 on a pure-content row (expect isOpen=True, taps=0)
T3 PASS | clicks=1 isOpen=False | tap on the content Button (expect clicks=1, isOpen=False)
T4 PASS | isOpen=True clicks=0 | horizontal drag dx=-120 starting on the content Button (expect isOpen=True, clicks=0)
T5 PASS | openedBefore=True isOpenAfter=False clicks=0 | tap on an already open row (expect openedBefore=True, isOpen=False, clicks=0)
T6 PASS | isOpen=False | vertical drag dy=80 (expect isOpen=False)
T7 INFO tap inside a pure-content row wrapped in a Button: outer.Click=1 row.Tapped=1 row.IsOpen=False
T7 INFO drag inside a pure-content row wrapped in a Button: row.IsOpen=True outer.Click=0 row.Tapped=0
T7 INFO tap inside a Button-content row wrapped in a Button: inner.Click=1 outer.Click=1 row.Tapped=1 row.IsOpen=False
T7 INFO drag inside a Button-content row wrapped in a Button: row.IsOpen=True inner.Click=0 outer.Click=0 row.Tapped=0

PROBE PASS: T1-T6 all matched the expected interaction contract (T7 exploratory, see T7 INFO above).
```

## 3. 用例与实测值

| 用例 | 手势 | 实测 | 期望 | 结果 |
| --- | --- | --- | --- | --- |
| T1 | 纯内容行轻点 | `row.Tapped=1`，`IsOpen=False` | taps=1，IsOpen=False | PASS |
| T2 | 纯内容行横拖 dx=-120（8 步） | `IsOpen=True`，`Tapped=0` | IsOpen=True，taps=0 | PASS |
| T3 | Content=Button 行轻点 | `Button.Click=1`，`IsOpen=False` | Click=1，IsOpen=False | PASS |
| T4 | 从 Content 的 Button 上起手横拖 dx=-120 | `IsOpen=True`，`Button.Click=0` | IsOpen=True，Click=0 | PASS |
| T5 | 已展开行（`Open(-1)` 后）轻点 | 展开前=True，轻点后 `IsOpen=False`，`Button.Click=0` | 收起且 Click=0 | PASS |
| T6 | 纵向拖动 dy=+80 | `IsOpen=False` | 不误开 | PASS |

补充：

- T1 的 `row.Tapped` 在 headless 下**正常触发**（不需要退回纯 Pointer 断言）；说明 `Tapped` 手势识别器并不依赖真实设备。
- T4 是最关键的一条：从 Content 的 `Button` 上起手横拖，横向锁定（DragLock=6px）后行 `e.Pointer.Capture(this)`，Button 收到 pointer capture lost，点击被取消（Click=0），行照常打开（IsOpen=True）。
- T5 的轻点落在被 `TranslateTransform(-84*2)` 平移后的 Button 可见区域上，按压在隧道阶段被行消费（`e.Handled=true`），所以内容不会收到 press/release，Click=0；松手时行 `Close()`。
- T6 纵向手势在首次移动（|dy|>|dx|+2）时就放弃本次 press，不抢捕获，行保持收起。

## 4. T7（探索）：把整行包进外层 Button 的实测结论

| 场景 | 轻点 | 横拖 |
| --- | --- | --- |
| 纯内容行 + 外层 Button | `outer.Click=1` **且** `row.Tapped=1`（一次轻点两次激活），`IsOpen=False` | `row.IsOpen=True`，`outer.Click=0`（拖动**未被**外层 Button 阻断） |
| Content=Button 行 + 外层 Button | `inner.Click=1` + `outer.Click=1` + `row.Tapped=1`（一次轻点三次激活） | `row.IsOpen=True`，`inner.Click=0`，`outer.Click=0` |

结论：

1. **不要把整个 `ArcSwipeActions` 再包进外层 `Button`。** 外层按钮并不能拦住横向拖动（行在锁定后仍会抢到指针并打开），却会让一次轻点产生 2–3 次激活（内容 Click / 行 Tapped / 外层 Click 叠加）。
2. 正确的「整行可点」写法是：把 `Button` 放进 `Content`（T3/T4 已验证），或给 `Content` 的表面接 `Tapped`（T1 已验证）。两者都不影响横向拖动，且展开态轻点只会收起、不会误触内容（T5）。
3. 顺带结论：外层 `Button` 的按压/点击语义会被内层消费一部分，但无法形成可控的单一激活路径，所以属于「能跑但语义不可取」的用法。

## 5. 文档措辞建议

当前页面（`SwipeActionsPage`）里已有「不要把整个滑动行包进外层 Button，那会让按钮先浪费按压与点击语义」的意思，建议按实测改成更精确的版本：

> 行内容本身可以是一个可点击控件（Button，或带 Tapped 的表面）：按压只在隧道阶段被观察，横向锁定后行才夺取指针，所以轻点会触发内容自己的 Click / Tapped，横向拖动仍会露出动作，两者不会互相吞掉；行已展开时轻点只会收起行，不会误触内容。
>
> 不要把整个 ArcSwipeActions 再包进外层 Button：外层按钮拦不住横向拖动（行仍会打开），但一次轻点会同时触发内容 Click / 行 Tapped 与外层 Button.Click（实测 2–3 次激活）。需要整行可点时，把 Button 放进 Content，或给 Content 的表面上接 Tapped。

## 6. 回归：危险色渗漏验证仍 PASS

`pwsh -NoProfile tools/verify-swipe-leak.ps1`（含 gallery 重建，页面已含「Content = Button」的第 0 行，页面高 5395px）：

```text
dark : ROW 3 closed row with hidden danger actions (ASSERTION) rect=(77,1152,646,75) page_y=1452 closed=True ring_danger=0 ...
       RESULT dark: PASS (assertion outer band: 0 danger px; sanity row outer band: 0 danger px; control danger fill: 5251 px)
light: ROW 3 ... rect=(77,1152,646,75) page_y=1452 closed=True ring_danger=0 ...
       RESULT light: PASS (assertion outer band: 0 danger px; sanity row outer band: 0 danger px; control danger fill: 5246 px)
PASS: swipe-actions closed danger row leaks 0 danger pixels in its outer band in dark + light; the positive control saw the red Delete fill in every theme.
```

页面加高到 5395px 后脚本的动态定位（画布 → 4 个行 band → 共享 x 范围）仍然正确；未修改 `tools/verify-swipe-leak.ps1`。证据更新见 `REPORT.md` / `report.json` / `swipe-leak-{dark,light}-annotated.png`。

## 7. 探针实现备忘（踩坑）

- **headless 的拖拽 Move 必须带 `RawInputModifiers.LeftMouseButton`**：headless 平台用 raw modifiers 推导 `PointerPointProperties.IsLeftButtonPressed`；`MouseMove(point, RawInputModifiers.None)` 会让控件看到「按键已松」，`ArcSwipeActions` 于是正确中止手势、拖动永远不开始（探针第一版 T2/T4 失败就是这个原因，不是控件缺陷）。
- `Tapped` 手势在 headless 下可用（T1 实测 1 次），无需真实桌面。
- 坐标统一用 `TranslatePoint` 从控件局部坐标映射到窗口坐标，避免依赖硬编码位置。
- 探针自带最小主题宿主 `ProbeTheme`：Fluent 基线 + `avares://Arc.Avalonia.Foundation/Theme/ArcBaseStyles.axaml` + `avares://Arc.Avalonia.Media/Themes/ArcMediaTheme.axaml`，避开会拉入 Markdown 家族的聚合主题。
- 探针只读引用 `src/**` 程序集，不修改 `src/**`、`tools/**` 与其他 docs。

## 8. 相关文件

- 探针项目与说明：`poc/SwipeInteractionProbe/SwipeInteractionProbe.csproj`、`Program.cs`、`App.axaml(.cs)`、`ProbeTheme.axaml(.cs)`、`README.md`
- 原始输出：`docs/analysis/swipe-leak/interaction-probe-output.txt`
- 危险色回归证据：`docs/analysis/swipe-leak/REPORT.md`、`report.json`、`swipe-leak-dark-annotated.png`、`swipe-leak-light-annotated.png`
