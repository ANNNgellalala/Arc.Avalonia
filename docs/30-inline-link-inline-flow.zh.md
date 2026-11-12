# 30 · 行内链接进入正文文本流：修复「链接文字被抬高」与「裸 https 不成链」

> 日期：2026-11-12
> 现象（真机截图 `7482.jpg`）：DSHRemote 会话里的 Markdown 消息中，① `下载页：https://…`、`直链：https://…` 这类
> URL 是普通正文（不可点、无链接样式）；② `[127 修复与验收记录](…)` 等行内链接的文字比正文**高约 7 px**、
> 字号偏小且行距被撑开。
> 对象：`Arc.Avalonia.Markdown`（本仓库）与 `DSHRemote`（外部宿主，通过 ProjectReference 消费 Arc 源码）；
> 证据：`.recon/link-baseline-probe/`（复现探针）、`.recon/link-verify/`（before/after 量化与图）、
> `.recon/phone-audit/REPORT.md`（真机截图时间线/APK 取证，工作区临时）。
> 相关：[docs/21 Markdown 组件族](21-markdown-components.zh.md) §3.1/§7、[docs/26 Markdown 叠压 RCA](26-markdown-overlap-rca.zh.md) §5.3。

---

## 0. 结论（先给答案）

两个现象是两个独立根因，都落在 **Arc 组件层**，与 Android/字体/GPU 无关：

| # | 现象 | 根因 | 修复 |
|---|---|---|---|
| 1 | `https://…` 不渲染成链接 | 默认 pipeline 用 GFM autolink（Markdig `UseAutoLinks`），它**只在行首/空白后触发**；中文正文里 URL 前通常是全角标点（`下载页：https://…`），于是保持普通文本。宿主 DSHRemote 曾用 `Preprocess` 把 URL 包成 `<…>` 绕行，但这属于宿主补丁，且 1.4.2 的旧版 `Preprocess` 反而把尖括号 autolink **压平成纯文本**（用户截图就是 1.4.2 的行为）。 | `Rendering/Autolinks/BareUrlAutolinks.cs`：新增 CJK 友好的 Markdig inline parser，在 `ArcMarkdownPipelineBuilder.UseGfm()` 注册；`[text](url)` 路径不变。 |
| 2 | 链接文字被抬高、行距被撑开 | `AddHyperlink` 把链接渲染成 `InlineUIContainer(HyperlinkButton{ Padding=2, MinHeight=24, VerticalAlignment=Center })`；内嵌控件高 24 px > 正文行盒 21.34 px，把行盒撑到 28.07 px，且控件内容（字号 14、非正文 16）贴控件顶部 → 链接文字比正文基线**高 6.89 px**。 | `AddHyperlink` 改为正文文本流里的 `ArcMarkdownLinkInline : Span`（accent + 下划线、字号/字体全部继承正文），点击命中改由 `Controls/ArcMarkdownLinkInteraction.cs` 对宿主 `TextBlock` 做字符矩形命中测试。 |

修复后（360 dp 手机宽度、真实桌面窗口实测，见 §4）：链接字号 16、行盒 21.336（= 普通行盒）、基线偏移 **0**；
`下载页：https://…` 变 accent 下划线链接，长 URL 自然换行不裁剪。**注意**：DSHRemote 需要重新构建（Arc 以
ProjectReference 参与其构建）才能把修复带上手机；宿主侧「裸 URL 包尖括号」的 workaround 可保留（幂等），
也可在下一个版本删除。

---

## 1. 现场与复现

截图消息原文（smartbill 会话，22:06:44 生成；见 `.recon/phone-audit/REPORT.md`）：

```markdown
- 下载页：<https://69.8.132.141:9443/__gateway/download>
- 直链：<https://69.8.132.141:9443/__gateway/resources/apk/smartbill-1.0.1-2-universal-debugsign.apk>
详细记录见 [127 修复与验收记录](docs/…)，本地 APK 在 [com.smartbill.app-Signed.apk](src/…)
```

- 时间线：截图完成于 2026-11-11 23:56（附件库写入时间），发进 DSHRemote 排查会话 23:57；当时手机上是
  **DSHRemote 1.4.2 (27)**。1.4.2 的 `MarkdownBodyView.Preprocess` 生效逻辑与 1.4.3 不同：旧版把
  `<https://…>` 尖括号 autolink **压平为纯文本**（为规避 Arc 当时 `HyperlinkButton` 的 NoWrap 裁切），
  行内链接 `[label](path)` 未被压平，所以只有 `127 修复与验收记录`/`com.smartbill.app-Signed.apk` 是链接、
  URL 全是正文——与截图逐项吻合。
- 「链接文字抬高」与宿主版本无关：`AddHyperlink` 在 1.4.2 与 1.4.3 都是同一实现（1.4.3 只多了 `NoWrap→Wrap`
  和链接菜单），所以 1.4.3 仍复现（模拟器 x86_64 1.4.3 截图亦可见，见 phone-audit 报告）。

复现探针：`.recon/link-baseline-probe`（真实 Avalonia 桌面窗口 + `ArcMarkdownView` + 360 dp 宽度），
同一段文本在修复前后各跑一次即可复现/验收；`.recon/link-verify` 用 DLL 快照冻结了 before 证据。

---

## 2. 根因 1：GFM autolink 的边界条件缺少中文场景

`ArcMarkdownPipelineBuilder.UseGfm()` → PuraMarkdown.Core `UseGfm()` → Markdig `UseAutoLinks()`。Markdig 的
`AutoLinkParser` 要求 URL 前是行首或空白；全角标点（`：`、`，`、`（`、`《`…）不是空白，因此：

```
下载页：https://example.com/x     → Literal("下载页：https://example.com/x")   （普通文本）
下载页： https://example.com/x    → Literal("下载页： ") + Autolink(...)     （只有这种情况成链）
```

截图的 URL 恰好全部紧跟在 `：` 后，于是整段失效。Arc 的默认 pipeline 因此对中文内容不友好；每个宿主都要
自己预处理（DSHRemote 的 `Preprocess` 就是这种补丁，且不同版本行为还不一致）。

**修复**：新增 `Rendering/Autolinks/BareUrlAutolinks.cs`（internal）：

- 实现 Markdig `IMarkdownExtension` + `InlineParser`（`OpeningCharacters = ['h','H']`），在 `ArcMarkdownPipelineBuilder.UseGfm()`
  里通过 PuraMarkdown 的 `UseMarkdownExtension` 注册；GFM 原有 parser 先跑（行首/空白场景行为不变），
  新 parser 只补「非 ASCII 字母数字左边界」的场景（CJK 标点、CJK 文字、`(`、`《` 等）。
- 产出与 GFM 相同的 `AutolinkInline`（带 `SourceSpan`），因此 sanitizer/诊断/渲染/链接菜单全部复用现有路径。
- URL 截止于空白与 `< > " ' \``、CJK 标点；尾部标点（`。，；：！？`、`.`/`,`/`;`/`:`/`!`/`?`/`…`）与不成对
  右括号裁掉，URL 内部成对括号（Wikipedia 风格）保留；扫描时跳过未闭合的强调定界符（`_见https://…_`）。
- 只处理 `http(s)://`；`ftp://`/`mailto:` 不自动链接（避免误链，仍可用 `[text](url)`）。
- 已知边界（与 GFM 接近，docs/21 §7 有记录）：URL 后**紧贴中文且无标点/空白**时中文会被并入 URL；无点且非
  `localhost` 的主机（如 `http://intranet/`）不自动链接；含 `* [ ]` 的 URL 与 IPv6 字面量不自动链接（避免吞掉
  强调/链接语法）。

验收矩阵（`.recon/autolink-scratch/autolink-matrix.log`）：CJK 标点/中文紧邻/行首/空白后 → 链接；代码 span、
围栏、`[text](https://…)`、`![alt](https://…)`、`<https://…>` 不重复链接/不被破坏；`ftp://`/`mailto:` 不动；
尾部全角/半角标点与括号平衡；同段多 URL；大写 `HTTPS://`。

---

## 3. 根因 2：行内链接是内嵌控件，不是文本

旧实现（`Rendering/Inlines/ArcMarkdownDefaultInlineRenderer.AddHyperlink`）：

```csharp
var content = new TextBlock { TextWrapping = Wrap, Foreground = Accent, TextDecorations = Underline };
var button = new HyperlinkButton { Content = content, Padding = 2, MinHeight = 24, VerticalAlignment = Center };
target.Add(new InlineUIContainer(button));
```

Avalonia 的 `InlineUIContainer` 把控件当作**原子的内嵌对象**：行盒高度取控件高度（24 px + padding → 行盒 28.07 px，
正文行盒 21.34 px），控件内容（未显式设置字号 → 继承 Fluent 默认 14，正文是 16）贴控件顶部，而控件底部对齐
文本基线 → 链接文字比正文基线高 `descent ≈ 6.89 px`。连带后果：行距被撑开、链接不可被文本选择、长 URL 作为
原子内联不参与段落换行（旧版只能靠 `NoWrap→Wrap` + 固定宽度硬撑，窄屏观感僵硬）。

**修复**：

- 新增 `Rendering/Inlines/ArcMarkdownLinkInline.cs`：`internal sealed class ArcMarkdownLinkInline : Span`，携带
  `Url`/`DisplayText`/`Title`，`Foreground = ArcAccent`、`TextDecorations = Underline`；标签里的强调会作为
  Span 子节点保留。字号/字体/行高全部继承宿主正文（16 px、FontBody、字体度量行高）。
- `AddHyperlink` 直接把 span 加进宿主 `TextBlock.Inlines`，不再产生任何 `HyperlinkButton`/`InlineUIContainer`
  （图片仍用 `InlineUIContainer`，不受影响）。
- 新增 `Controls/ArcMarkdownLinkInteraction.cs`：在 `CreateTextBlock()` 时挂到宿主 `TextBlock` 上，
  - 命中：遍历 inline 计算链接的字符区间（`Run.Text.Length`；`LineBreak` 按 `Environment.NewLine.Length`
    ——Avalonia 的换行 run 在 Windows 上占 2 个 text source 位置；`InlineUIContainer` 记 1 个字符，与 TextLayout
    一致），用 `TextLayout.HitTestTextPosition(offset)` 取字符矩形（按「Inlines 集合变更 + 布局尺寸/行数签名」失效
    缓存；流式 tail 每次 flush 会 `Inlines.Clear()` 重建，集合变更必须让映射一起失效），指针落在矩形内即命中；
  - 交互：轻触/左键 release（`ArcMarkdownTapGuard` 8 px 拖拽取消）→ 先 `LinkClickSink`（宿主 `LinkClicked` +
    `HandledByHost` 接管）→ 否则 `ArcMarkdownLinkMenu`（复制/浏览器菜单，逻辑不变；关闭时把焦点还给打开菜单前的
    元素）；桌面右键直接开菜单；悬停手型光标 + title tooltip；链接是普通文本 run，启用选择时随段落可整体选择/
    复制、可跨链接拖选（但不能从链接上起选），并随段落自然换行。
  - 触屏/无障碍取舍：链接不再各自是可聚焦控件 → 不支持「Tab 到链接 + Enter 激活」，读屏也没有逐链接语义；
    需要键盘/读屏入口的宿主用 `LinkClicked` 承载（docs/21 §7 已记录）。菜单锚定在承载链接的 `TextBlock` 下方
    （Avalonia 12 `Flyout` 无按点定位 API），点击命中仍按字符矩形精确判定。

---

## 4. 验证（真实桌面窗口，360 dp 手机宽度 + 680 px 桌面宽度）

`.recon/link-verify`（DLL 快照探针，before = 修复前构建、after = 修复后构建；指标定义见该目录 README）：

| 指标 | before | after | 判定 |
|---|---|---|---|
| 链接文字字号 | 14（正文 16） | **16**（继承正文） | PASS |
| 链接所在行盒高 | 28.070 px（普通行 21.336） | **21.336 px**（= 普通行） | PASS（Δ 0） |
| 链接文字基线偏移 | **−6.893 px**（抬高；max 6.963） | **0** | PASS |
| `下载页：https://…` | 普通 Run（无 accent/下划线，不可点） | **链接**（accent 下划线，可点） | PASS |
| 长 URL（直链） | 文本换行 4 行、未裁剪 | 链接换行 4 行、未裁剪 | PASS |
| `<https://…>` 角度 autolink | 链接（HyperlinkButton） | 链接（文本 run，基线一致） | PASS |
| `HyperlinkButton` 数量 | 3 | **0** | PASS |

图：

- [`docs/images/30-link-baseline-before-after.png`](images/30-link-baseline-before-after.png)：同段落 before/after 对比
  （before 链接抬高 + 裸 URL 是正文；after 链接落回基线 + URL 变链接）。
- [`docs/images/30-link-baseline-after.png`](images/30-link-baseline-after.png)：after 手机宽度整块渲染。

**真机/模拟器端到端（Android 16 模拟器，DSHRemote 1.5.x Debug，x86_64，连接真实网关 69.8.132.141:9443，
打开截图同一条会话消息）**：

- [`docs/images/30-link-android-real-session.png`](images/30-link-android-real-session.png) /
  [8x 局部](images/30-link-android-real-session-zoom.png)：`下载页：https://…`、`直链：https://…` 已是 accent 下划线
  链接并自然换行；`[I27 修复与验收记录]`、`[com.smartbill.app-Signed.apk]` 与正文同一基线（不再抬高）；
  `文件：…`/`sha256` 保持普通文本。模拟器缺 CJK 字体（中文显示为豆腐块，见 docs/25），Latin URL 与下划线清晰可见，
  基线可直接对照。
- 点击验证：在模拟器上点「演示会话」里的链接会走 Arc 的链接激活路径（本机实测系统浏览器被拉起，打开
  `https://69.8.132.141:9443/…`，与修复前一致）。
- 修复后的安装包已发布到网关下载页（arm64 Release `1.5.1 (30)`，sha256
  `4832a8951fc565b0175ffd0fb698e74048151d824f7cc879e5311b065c21f3d9`，
  `https://69.8.132.141:9443/__gateway/download`）：手机安装 1.5.1 后无需再做任何设置，两类链接问题同时消失
  （1.5.1 额外包含审查发现的硬换行字符映射与流式 tail 缓存失效两个修复，渲染像素与 1.5.0 一致）。

其它回归：

- `Arc.Avalonia.Verify`：**190 checks / 0 failures**（新增/更新断言：行内链接是文本 run、无 `HyperlinkButton`、
  链接带下划线/URL、CJK 冒号后裸 URL 自动链接、硬换行在字符映射中按 `Environment.NewLine.Length` 计数）；
  链接命中交互（tapGuard/右键/菜单/缓存失效）没有 headless 断言，由 `.recon/link-baseline-probe` 真实窗口点击与
  本节的自动化指标覆盖；
- `dotnet build ArcUI.slnx`：0 error；
- gallery `--audit` / `--smoke`、`tools/check-all.ps1` 门禁（终端输出见 `.recon/build-final.log`、`.recon/gates-final.log`）；
- `.recon/link-baseline-probe` 修复后实测：链接 run 与前一字符同 Y、同行高（`deltaY=0 deltaH=0`），
  `HyperlinkButton count=0`，5 个链接中 2 个来自 CJK 裸 URL 自动链接。

---

## 5. 兼容性与宿主动作

- **公开 API 无新增**：`ArcMarkdownLinkInline` / `ArcMarkdownLinkInteraction` 均为 internal；`AddHyperlink` 是
  private；`UseGfm()` 行为只增不减（行首/空白 autolink 仍由 GFM parser 处理）。工作区里既有的未提交链接菜单
  改动新增了 `ArcMarkdownPlatform.OpenInBrowserAsync`，`tools/api-baselines/Arc.Avalonia.Markdown.txt` 已包含它，
  `check-public-api` MATCH，本次不再新增基线。`check-public-api` 无需更新基线。
- **宿主 DSHRemote**：以 ProjectReference 消费 Arc 源码，重新构建 APK 即获得修复（`LinkClicked` / `LinkHandler` /
  菜单文案 / `ArcMarkdownOptions` 用法全部不变）。本次已构建并发布 `DSHRemote 1.5.1 (30)` arm64 Release 到网关下载页
  （`https://69.8.132.141:9443/__gateway/download`，sha256 见 §4）；1.4.3 的 `Preprocess`（裸 URL 包 `<…>`）可以保留：
  URL 已是 autolink 时结果幂等；建议下个版本删掉这段 workaround，避免宿主预处理误伤代码块/图片语法。
- **Chat/Agent（`PuraMarkdown.Agent` 路径）**：不经过本组件的行内渲染器，仍由上游 `HyperlinkButton` 渲染 +
  `ArcChatTheme` 的「去 chrome + 下移 4 px」主题修正（docs/29 §8 记录）；本修复不影响 Chat 路径。
- **已知限制**：见 docs/21 §7（无逐链接键盘焦点；CJK autolink 的边界/尾标点启发式；`ftp`/`mailto` 不自动链接）。

---

## 6. 证据索引

| 证据 | 位置 |
|---|---|
| 用户真机截图（两现象） | `7482.jpg`（会话附件）；时间线/版本取证见 `.recon/phone-audit/REPORT.md` |
| before/after 量化与图 | `.recon/link-verify/`（`before-*`、`after-*`、`before-metrics.json`、`before-after.png`） |
| 复现探针（真实窗口、360dp/680px） | `.recon/link-baseline-probe/`（`probe-phone.png`、`probe-phone-links-zoom.png`、`probe-phone-bare-zoom.png`） |
| autolink 解析矩阵 | `.recon/autolink-scratch/`（`autolink-matrix.log`、`Program.cs`） |
| 行内链接实现（新） | `src/Components/Arc.Avalonia.Markdown/Rendering/Inlines/ArcMarkdownLinkInline.cs`、`Controls/ArcMarkdownLinkInteraction.cs` |
| autolink 实现（新） | `src/Components/Arc.Avalonia.Markdown/Rendering/Autolinks/BareUrlAutolinks.cs`、`Rendering/ArcMarkdownPipeline.cs` |
| 旧内嵌控件实现（被替换） | `Rendering/Inlines/ArcMarkdownDefaultInlineRenderer.cs`（`AddHyperlink`） |
| 构建/门禁日志 | `.recon/build-final.log`、`.recon/gates-final.log` |
