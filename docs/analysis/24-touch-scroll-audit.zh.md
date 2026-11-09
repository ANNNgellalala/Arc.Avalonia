# 24 · 触控 / 滑动审计（手机端）

> 触发：用户反馈"手机上部分组件体验差，主要是滑动"。本文件给出**已核验**的问题、修复状态与验收清单。
> 扫描口径：`src/{Foundation,Components,Agent}` 73 个含指针/滚动代码的文件（`.recon/touch-scan.json`）+ `tools/check-touch.ps1`（R11）+ 各修复切片实测。

## 1. 总体结论

| 类别 | 结论 |
|---|---|
| 滚动条常驻 | **0** 处 `ScrollBarVisibility.Visible`（基线健康） |
| 触屏滚动被拦截 | 修复前 `ArcMarkdownView` 只处理 `PointerWheelChanged`（触屏滚轮事件不触发）；已补触摸路径 |
| 嵌套滚动链 | 修复前全仓 **0** 处 `IsScrollChainingEnabled` → 横向代码块/表格/聊天内滚动会链到页面；已对 Chat/Markdown/Agent 关键滚动宿主显式 `=false` |
| 拖拽手势 | 20 个自绘拖拽控件（Swipe/轮播/卡栈/长按/滑块/图表刷选）均有 `PointerCaptureLost` 兜底；缺兜底的是 `ArcColorArea/ColorRamp`（2 处，P1）与 `ArcResizablePanels/ImageCompare`（P1） |
| 点击时机 | 5 处 `PointerPressed` 直接执行动作（warning：`ArcChoroplethMap/ArcSemanticMap/ArcSpotlightCard/ArcTextFieldBase/ArcBlackHole`）→ 触屏滑过会误触，P2 |
| 命中区 | 2 处 informational（`ArcRichTextEditor`、`ArcTreeTable` 内部小按钮）；Chat/Markdown/Agent 的按钮已统一 44×44 |
| 键盘遮挡 | `ArcChatView/ArcChatComposer` 已支持 `KeyboardInsets/SafeArea` 注入；其它输入控件未做（M3） |

## 2. 已修复（本轮）

| 组件 | 问题 | 修复 |
|---|---|---|
| `ArcChatView` | 流式追加时 `ScrollToEnd` 把用户手指拽回底部 | 指针拖拽锁存（>8px 即 `_follow=false`），释放且在 56px 内才恢复；`ScrollChanged` 上移暂停；`ScrollToEnd()` 显式恢复 |
| `ArcChatView` / `ArcChatComposer` | 滚动条常驻、键盘遮挡输入区 | `<480px` 隐藏纵向滚动条；`IsScrollChainingEnabled=false`；新增 `SendOnEnter`、`KeyboardInsets`、`SafeArea`，按钮 34→44 |
| `ArcMarkdownView` | 代码块/表格横滚链到页面纵滚；流式抢滚 | 内部 `ScrollViewer` 横滚 `Auto` + 禁链；流式上滚后不强制回底；`NotifyUserScrolledToEnd()` 恢复 |
| `ArcMarkdownView` | 链接/复制按钮触屏难命中 | 链接 Padding+MinHeight 24、按钮 44；`ArcMarkdownTapGuard` 8px 拖拽取消 click |
| `ArcAgentConversation` | 事件卡追加时抢滚；按钮小 | 拖拽锁存 + 释放复位；审批/choice/confirm 按钮 44×44 + `ArcAgentTapGuard` |
| Gallery 移动壳 | 桌面三栏在手机上不可用 | 新增 `--mobile`（412×915）+ 宽度 <900 自动 compact：顶部 44px 菜单/标题/主题栏、侧栏变 320px 覆盖抽屉（backdrop Tapped 关闭、44px 目标）、TOC 隐藏、内容 16px 边距 |

## 3. 待修（下一波，按优先级）

1. **P1** `ArcColorSurface/ArcColorRamp`：拖动无 `PointerCaptureLost` 复位（拖动中弹层关闭会卡住状态）。
2. **P1** `ArcResizablePanels` / `ArcImageCompare`：同上；且把手命中区偏小（视觉细可以，命中要 44）。
3. **P1** `ArcSwipeActions`/`ArcCarousel`：确认拖拽阈值 ≥8px 且方向锁定后才拦截（否则纵向滚动被横向手势吃掉）——需要真机复测。
4. **P1** `ArcVirtualList` 行点选：拖拽滚动后可能仍触发选中（Chat 消息行点选走这里），需 8px 抑制。
5. **P2** `ArcBottomSheet`/`ArcDrawer`：下拉关闭阈值、backdrop 点击、safe-area 底部留白。
6. **P2** 弹层尺寸：`ArcDialog` 窄屏宽度、`ArcPopover/Tooltip` 贴边翻转、Picker 弹层在 412 视口不溢出。
7. **P2** 5 处 `PointerPressed` 直接执行动作改为 `Tapped` + 拖拽守卫。
8. **M3** 输入控件键盘 insets、滚动惯性参数、手势返回（Android back）。

## 4. 手机端验收清单（412×915，人工/脚本）

- [ ] 纵向滚动长页时不误触任何按钮/链接（拖 20px 后松手不触发）
- [ ] 聊天流式输出时手指上滑不被拽回底部；回到底部后恢复自动跟随
- [ ] 代码块/表格可独立横向滑动，且不会带动页面纵向滚动
- [ ] 横向卡片（Carousel/Swipe）滑动只动卡片；纵向滑动页面时不触发卡片
- [ ] 抽屉打开后可点 backdrop / 菜单按钮关闭；关闭后内容回到顶部
- [ ] 所有按钮命中区 ≥44×44（Chat/Markdown/Agent 已达标）
- [ ] 滚动条不常驻（滚动时短暂出现）
- [ ] 输入框聚焦时键盘不遮挡发送按钮（宿主注入 KeyboardInsets）
- [ ] 弹层（Dialog/Popover/日期选择）宽度不超出视口，可关闭
- [ ] 主题切换后移动壳配色仍为 token（无透明背景）

## 5. 复现与门禁

* 静态：`pwsh tools/check-touch.ps1`（R11）——`wheel-only-scroll` / `missing-capture-lost` 为红项，其余为 warning/informational。
* 运行时：`pwsh tools/mobile-smoke.ps1`（R12）——跑 `Gallery.dll --smoke --mobile` 并校验 `pageShows>=320 && failures=0`。
* 手测：`dotnet src/Apps/Arc.Avalonia.Gallery/bin/Debug/net10.0/Arc.Avalonia.Gallery.dll --mobile`

> 真机验证后的使用者建议（Android 宿主接入、布局、手势）见 [docs/25 手机端使用与开发建议](../25-mobile-usage-guidance.zh.md)。

