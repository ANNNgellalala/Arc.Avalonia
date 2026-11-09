# 25 · 手机端使用与开发建议（真机验证结论）

> 来源：在 `PLQ110`（OnePlus，Android 16 / API 36，1272×2800）与 `sdk_gphone64_x86_64` 模拟器上，用 `Arc.Avalonia.Gallery.Android` 对 Gallery 全量文档站做真机安装/启动/滚动/手势验证。
> 目的：把"在桌面能跑、上手机就崩/难看/手感差"的真实坑，固化成使用本库的开发建议。逐条给：现象 → 根因 → 建议。
> 相关：[docs/24 触控审计](analysis/24-touch-scroll-audit.zh.md)、[docs/23 架构总览](23-architecture-overview.zh.md)、[ADR 0003 主题策略](adr/0003-theme-strategy.md)。

---

## 1. Android 宿主接入（这 5 条按顺序做，缺一条就白屏/闪退）

### 1.1 用可用的 Android SDK（API 36）

* 现象：`error XA5207: 找不到适用于 API 级别 36 的 android.jar`；`dotnet workload` 又不可用时无法自动补装。
* 建议：`net10.0-android` 只接受 `TargetPlatformVersion 36.0/36.1`，宿主机器必须有 `platforms/android-36` + `build-tools` + JDK 21；构建时显式传 `-p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...`。同机其它 App 若能装，优先复用它的 SDK 目录。

### 1.2 Activity 主题必须是 AppCompat 后代（**最常见的"打开即崩溃"**）

* 现象：装上后启动即退出，`logcat -b crash` 为空，只在 `logcat` 里能看到
  `java.lang.IllegalStateException: You need to use a Theme.AppCompat theme (or descendant) with this activity`
  栈顶是 `AvaloniaActivity.set_Content → AvaloniaMainActivity.InitializeAvaloniaView`。
* 根因：Avalonia 的 Android 宿主内部是 `AppCompatActivity`，`setContentView` 要求应用主题继承 `Theme.AppCompat`。
* 建议（`Resources/values/styles.xml` + `[Activity(Theme = "@style/YourTheme")]`）：
  ```xml
  <style name="ArcGalleryTheme" parent="Theme.AppCompat.Light.NoActionBar">
    <item name="android:windowBackground">@android:color/white</item>
  </style>
  ```
  不要用 `@android:style/Theme.Material.Light.NoActionBar`。

### 1.3 `adb install` 的 APK 必须内嵌程序集

* 现象：启动即 `SIGABRT`，tombstone：
  `No assemblies found in '/data/user/0/<pkg>/files/.__override__/arm64-v8a' ... Assuming this is part of Fast Deployment. Exiting...`
* 根因：`dotnet build`（Debug 默认）走 Fast Deployment，程序集不在 APK 里，靠 `-t:Run` 单独推送。
* 建议（与仓库里可用的两个 Android 宿主一致）：
  ```xml
  <AndroidLinkMode>None</AndroidLinkMode>
  <EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>
  <RunAOTCompilation>false</RunAOTCompilation>
  <PublishTrimmed>false</PublishTrimmed>
  <AndroidPackageFormat>apk</AndroidPackageFormat>
  ```
  需要"手动 `adb install` 就能启动"时必须显式加 `EmbedAssembliesIntoApk=true`。

### 1.4 包内程序集是可枚举的，但**没有散落的 .dll 文件**

* 现象：页面/文档数量为 0（首页统计 `0 中文文档 / 0 条目页 / 477 阻断项`）。
* 根因：`ArcPageRegistry` / `ArcDocStore` 按 `AppContext.BaseDirectory + Directory.GetFiles("Arc.Avalonia*.dll") + Assembly.LoadFrom` 发现程序集；APK 里没有 .dll 文件，且依赖程序集是懒加载。
* 建议：宿主启动时（`OnFrameworkInitializationCompleted`）先遍历入口程序集的引用，把 `Arc.Avalonia*` 显式 `Assembly.Load(...)`，再让发现逻辑枚举 `AppDomain.GetAssemblies()`。本仓库已在 `ArcPageRegistry` / `ArcDocStore` 内实现该兜底，自定义文档/页面程序集同样适用。

### 1.5 字体：跟随系统，不要钉死字体

* 现象：中文全为 `□□□` 豆腐块；或固定某款字体后与系统/其它 App 不一致。
* 建议：`ArcFontBody` / `ArcFontDisplay` 用 `FontFamily.Default`（跟随平台），**不要**在 Android 宿主里 `.WithInterFont()` 或写死 `FontFallbacks = ["sans-serif"]`；系统字体本身覆盖 CJK。若产品确需品牌字体，再单独加字体包与回退链。

### 1.6 状态栏安全区（edge-to-edge）

* 现象：顶栏与系统状态栏/时间重叠（Android 15+ 强制 edge-to-edge）。
* 建议：宿主在 `MainView` 挂载后读 `TopLevel.InsetsManager.SafeAreaPadding` 给根视图加 top/bottom 间距（无值时用 28/12 DIP 兜底）。Gallery 的 `MobileApp` 已按此实现。

## 2. 布局与响应式建议

1. **compact 断点**：窗口宽度 `< 900 DIP` 切换单列；侧栏变覆盖式抽屉（宽 320）+ backdrop + `Tapped` 关闭；TOC 隐藏；内容 `Padding=16`、正文 `MaxWidth=720` 且**水平 Stretch**。
2. **禁止固定像素宽度**：文档页/示例里的 `Width=720/600` 之类在 412 视口会溢出被裁切（真机实测：Swipe actions 预览行文字被切、API 表第三列只显示到 `0,(`）。窄屏一律 `Stretch` + 换行；确需宽内容时放进**独立横向滚动容器**，并设 `IsScrollChainingEnabled=false`。
3. **表格类**（API 表、数据表、对比表）窄屏方案：≤600 宽时改为"每条属性一行（键值折行）"或独立横滚；不要整页横滚。
4. **滚动条**：手机上不要常驻，窄屏用 `Hidden`/`Auto`；需要视觉提示时用 `ArcMobileStyles.axaml`（6px、thumb ≥44、空闲 0.45 透明度）。
5. **命中区**：交互目标最小 44×44（`ArcHitTargetMin`）；图标按钮视觉可以小，命中区用 `MinWidth/MinHeight` 撑满。

## 3. 滑动/手势组件使用建议

| 组件 | 真机结论 | 开发建议 |
|---|---|---|
| `ArcSwipeActions` | 演示行被裁切时"滑不到位/露出不全"；阈值与回弹需要真机手感确认 | 起判 8–10px 且横向主导（`|dx| > |dy|`）才接管；`PointerCaptureLost/Canceled` 复位；松手按 35% 停靠或回位（阻尼 0.24）；滑开后点空白收起；FullSwipe 只在超越阈值后执行 |
| `ArcChatView` / Markdown 流式 | 流式追加会抢用户上滑 | 拖拽 >8px 即暂停自动跟随，释放后近底（~56px）才恢复；不要用 `PointerWheelChanged` 当唯一滚动信号 |
| 嵌套滚动（代码块/表格/聊天） | 横滑会带动页面纵滚 | 内层 `ScrollViewer` 设 `IsScrollChainingEnabled=false`，横向 `Auto`、纵向 `Disabled`（反之亦然） |
| 轮播/卡片栈 | 与页面纵向滚动冲突 | 方向锁定后再接管手势；未锁定前不 `CapturePointer` |
| 抽屉/底部弹层 | 下拉关闭的手感依赖阈值与捕获复位 | 跟手位移 + 35% 阈值 + capture 丢失复位；backdrop 用 `Tapped` 关闭 |

## 4. 真机验证方法（可直接复用）

```powershell
# 1) 无线调试
adb connect <ip>:<port>                     # 或用 adb mdns services 发现
adb -s <serial> install -r <apk>
adb -s <serial> shell monkey -p <pkg> -c android.intent.category.LAUNCHER 1

# 2) 直达某一页（Avalonia 在 Activity 之前创建 App，intent extra 读不到）
adb -s <serial> shell run-as <pkg> sh -c "echo swipe-actions > files/slug.txt"
#   Gallery 的 MobileApp 支持 files/slug.txt 指定初始 slug（调试钩子）

# 3) 截图 / 手势
adb -s <serial> exec-out screencap -p > shot.png
adb -s <serial> shell input swipe 1000 1100 350 1100 300   # 左滑
adb -s <serial> shell input tap 60 110                     # 点汉堡菜单

# 4) 崩溃定位
adb -s <serial> shell dumpsys activity exit-info <pkg>     # reason=4 Exception / 5 Native crash
adb -s <serial> logcat -d | Select-String 'UNHANDLED EXCEPTION|Caused by|IllegalState'
adb -s <serial> shell dumpsys dropbox --print data_app_native_crash
```

**教训**：`logcat -b crash` 为空 ≠ 没崩；Avalonia 的托管异常打在 `MonoDroid` tag（`UNHANDLED EXCEPTION`）；退出原因看 `exit-info`。

## 5. 本库当前的真机已知问题（待修）

1. **文档页固定宽度导致横向裁切**（API 表、Swipe actions 预览行）——本轮真机截图直接可见，下一轮修 `DocLayout`/`DocApiTable`/页面示例。
2. Android **硬件返回键**未接入（仅 backdrop/Esc 关闭抽屉）；返回键应关闭抽屉 → 无抽屉时退出。
3. compact 下 TOC 直接隐藏，未提供"折叠到正文顶部"的替代。
4. `ArcSwipeActions` 的阈值/停靠手感尚未在真机逐项标定（下一轮 4 组手势 + 截图对比）。
5. `ArcChatView` 消息行点选（`ArcVirtualList`）拖拽后仍可能选中——需 8px 抑制。

## 5.1 Android 中英混排 CJK 豆腐块（未解决，实测结论）

真机/模拟器同一页面里出现"**纯中文标题正常、中英混排正文全是 □□□**"：
- 已试无效：`ArcFontBody = FontFamily.Default`；`ArcFontBody = "sans-serif"`；`ArcFontBody = "Noto Sans CJK SC"`（该族在测试机不存在，反而全豆腐）；`ArcFluentOverrides` 里覆写 `ContentControlThemeFontFamily = Default`。
- 结论：正文文本并非走 `ArcFontBody` 的样式路径（ArcBaseStyles 的 `TextBlock` setter 未生效或优先级低于 Fluent 的隐式 ControlTheme），实际继承的是 Fluent/Inter（Inter 无 CJK 字形），而标题类元素显式用了 `ArcFontDisplay` 才正常。
- 下一轮方案：① 用 `ControlTheme`/`Style` 在 `TextBlock` 上强制 `FontFamily`（提高优先级），或② 删掉 `Avalonia.Fonts.Inter` 依赖、让 Fluent 的默认字体回落到平台默认，或③ 内嵌 CJK 字体子集作为 body 字体（最稳，体积 2–3 MB）。
- 给使用者的建议：**在 Android 上不要依赖 `FontFamily.Default` 的逐字回退**——Avalonia 12 + Skia 在混排文本里不会逐字替换字体；要么把正文字体显式设成系统 CJK 族，要么内嵌 CJK 字体。

## 6. 给使用者的 10 条检查清单

- [ ] Activity 主题继承 `Theme.AppCompat.*`（否则启动即崩）
- [ ] APK 内嵌程序集（`EmbedAssembliesIntoApk=true`），`adb install` 可直接启动
- [ ] 启动时预加载 `Arc.Avalonia*` 依赖程序集（页面/文档计数不为 0）
- [ ] 字体用 `FontFamily.Default`，不钉死品牌字体；CJK 正常
- [ ] edge-to-edge 安全区已应用（顶栏不压状态栏）
- [ ] compact 断点 <900，单列 + 抽屉 + 16px 边距
- [ ] 页面无固定像素宽度；宽内容用独立滚动容器
- [ ] 命中区 ≥44×44，滚动条不常驻
- [ ] 滑动组件：8–10px 起判、方向锁、capture 丢失复位、35% 停靠
- [ ] 流式/自动滚动不抢手指，回底才恢复跟随
### 5.2 内嵌 CJK 字体的第一次尝试（失败，已回滚）

- 做法：把 `NotoSansSC` 子集（1.5 MB，OFL）复制进 Android 工程，`<AvaloniaResource Include="Assets\Fonts\*.otf" />`，并在 `FontManagerOptions.DefaultFamilyName` 里用 `avares://Arc.Avalonia.Gallery.Android/Assets/Fonts#Noto Sans SC`。
- 结果：应用**启动即退出**（`pidof` 为空、前台回桌面），说明字体资源/族名解析在 AppBuilder 阶段抛异常；已回滚，应用恢复正常（pid 11659 级别）。
- 下次做法：① 先用 `Avalonia.Media.FontManager.Current.AddFontCollection(...)`/`OpenTypeCollection` 验证资源路径可加载；② 再设 `DefaultFamilyName = "Noto Sans SC"`（不带 URI，靠已注册集合）；③ 失败时把异常打进 `ArcAppInit` 日志再迭代，不要直接在 `CustomizeAppBuilder` 里赌。
### 5.3 内嵌 CJK 字体第二次尝试（XAML 资源方式，同样失败）

- 做法：`<AvaloniaResource Include="Assets\Fonts\*.otf" />` + `MobileApp.axaml` 里 `<FontFamily x:Key="ArcCjkBody">avares://Arc.Avalonia.Gallery.Android/Assets/Fonts#Noto Sans SC</FontFamily>` + `Style Selector="TextBlock"` 强制使用。
- 结果：**启动即退出**（`pidof` 空），已回滚，应用恢复（pid 11825）。
- 结论：Avalonia 12.1.4 + Android 下，直接以 `avares://…*.otf` 作为 `FontFamily`（甚至只是在资源字典里声明）会让初始化阶段抛异常。正确路径应是 `Avalonia.Media.Fonts.EmbeddedFontCollection` + `FontManager.Current.AddFontCollection(...)`（在 `App.Initialize` 里 try/catch 注册），并用**族名**引用，而不是直接给 `FontFamily` 一个 otf 的 avares URI。
- 给使用者的建议（当前结论）：**Android 上暂时不要依赖库内嵌字体**；要么接受系统字体的 CJK 限制（混排正文可能缺字），要么按上面的 `EmbeddedFontCollection` 方案在宿主自行注册并用族名覆盖。
### 5.4 最终结论（真机验证，2026-11-10）

在真机 `PLQ110`（Android 16 / API 36）上，当前版本（系统默认字体、无任何内嵌字体）**中文完全正常**：

- 首页标题、正文段落、统计卡说明、列表项里的**中英混排**都正确渲染；
- 顶栏目录按钮、安全区、紧凑布局也都正常（截图 `.recon/phone-cjk.png`）。

因此 §5.1–5.3 的豆腐块是**模拟器系统镜像缺少 CJK 字体**导致的环境问题，不是库缺陷：

- 真机（含中文字体的系统）：`FontFamily.Default` 完全够用；
- 模拟器 / 无 CJK 字体的设备：才会出现混排豆腐，此时按 §5.3 用 `EmbeddedFontCollection` + 族名在宿主补字体；
- 库侧**不需要**内嵌字体，保持"跟随系统字体、不钉死字体"的既定策略。

**给使用者的最终建议**：真机验收以真实设备为准；CI/模拟器上若出现中文豆腐块，先检查镜像是否有 Noto CJK，而不是改库。
