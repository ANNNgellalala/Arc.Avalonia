# 01 · Gallery 现有组件全量盘点（面向"Agent 日常生活工具"基座评估）

> 本文是 `docs/agent-life-tools/` 系列的**现状盘点**，不做扩展提案（扩展见 02/03）。
> 目标：把 294 个 catalog 项、219 个 gallery 页面、24 个 `src` 项目的实现现状，整理成后续缺口分析可直接引用的索引。
>
> **数据截止**：2026-10-26，仓库工作区（未构建、未改代码）。所有数量均由脚本从 `docs/arc-catalog.json`、
> `src/*/Pages/*.cs`、`src/*/Controls/*.cs` 只读统计得出，统计命令见 §1.3。
> 引用路径一律为仓库相对路径。
>
> **范围说明（v2，2026-10-28）**：本库定位为**纯 UI 组件库**。本文记录的持久化/调度/系统通知/托盘/网络/音频/显式虚拟化缺失，只作为宿主应用的集成边界参考，**不是本库的待办**；第 6.5 节总评请按此口径阅读。

---

## 0. 摘要（给主报告直接取用）

| 维度 | 数字 | 口径 |
| --- | --- | --- |
| catalog 总项 | **294** | `docs/arc-catalog.json`：`items` 128 + `specialItems` 55 + `blockItems` 111 |
| 组件分类 | **23 类 / 128 项** | `componentCategories.length = 23`，各项合计 128 |
| Special 分组 | **5 组 × 11 = 55 项** | `specialGroups`：Glass/surfaces、Motion、Media、AI widgets、AI explainers |
| Blocks 分组 | **6 组 / 111 项** | `blockGroups`：18+18+21+16+20+18 |
| 免费 / Pro | **138 / 156** | 按 `tier` 字段 |
| Gallery 页面 | **219** | 从 24 个程序集反射发现的 `ArcPage` 具体子类（`ArcPageRegistry.Discover`） |
| 其中独立条目页 | **166** | `ArcItemPage`（Special 55 + Blocks 111，即 55+111=166） |
| 其中分组/共享页 | **53** | 48 个组件演示页 + 5 个 Gallery 自身页面（Foundations/Tokens/Icons/Motion/Kit proof） |
| 截图 | **438** | `shots/light` 219 + `shots/dark` 219（各 219 个 PNG，已核对） |
| src 工程数 | **24** | `src` 下 24 个目录与 24 个 `.csproj` 一一对应，与 `docs/04` 的"24 工程"一致 |
| 非 obj 的 `.cs` 源文件 | **516** | 与 `docs/04-design-and-performance-review.zh.md` §0.2 的 516 一致；其中 222 个是 `*Page.cs` 或页面契约文件 |
| 公共类声明（不含 Pages） | **411** | `grep "public … class"`，去 Pages/bin/obj；407 个唯一类名 |
| 公共**控件**类（不含 Pages） | **236**（234 个唯一类名） | 按继承链判定基类可达 `Avalonia.Control` 系；方法见 §3.1 |
| 内部（internal）类 | **100** | 各 Blocks 项目的私有绘制/数据助手，不构成公共 API |
| `.axaml`（ControlTheme） | **112** | 与 `docs/04` 的 112 一致 |
| 目录可达性审计 | **294/294，missing=0** | `--audit` 的实现见 `src/Arc.Avalonia.Gallery/Framework/CatalogAudit.cs`；本次静态复核结果为 0 遗漏 |

**一句话结论**：这是"**展示层几乎完整、运行时基础设施几乎为零**"的组件库——294 项目录 100% 可达，
控件主题/动效/无障碍纪律完整，但**没有任何持久化、系统通知、调度、网络、音频采集与虚拟化**；
作为"Agent 生活工具"的 UI 基座，它提供的是**高保真积木与页面范式**，而不是可运行的日常事务系统。

---

## 1. 数据源与核对方法

### 1.1 权威数据源

| 文件 | 作用 | 核对结果 |
| --- | --- | --- |
| `docs/arc-catalog.json` | catalog 单一事实来源（113,204 B）：`componentCategories / specialGroups / blockGroups / items / specialItems / blockItems` | 294 项、0 个重复 slug |
| `src/Arc.Avalonia.Demo/Catalog/arc-catalog.json` | 内嵌资源副本（csproj `EmbeddedResource`，资源名 `Arc.Avalonia.Demo.Catalog.arc-catalog.json`） | SHA-256 与 docs 版**完全一致** |
| `src/Arc.Avalonia.Demo/ArcCatalog.cs` | 加载 catalog、按 slug 建索引、给侧栏排序（`OrderOf`） | 294 项全部可 `Get(slug)` |
| `src/Arc.Avalonia.Demo/ArcItemPage.cs` | 单条目页契约：`ItemSlug` → `ArcCatalog.Get` → 统一文档骨架 | 166 个实现 |
| `src/Arc.Avalonia.Demo/ArcPageRegistry.cs` | 反射扫描 `Arc.Avalonia*` 程序集里非抽象、有默认构造的 `ArcPage` | 发现 219 页 |
| `src/Arc.Avalonia.Gallery/Framework/CatalogAudit.cs` | `--audit`：独立页 / 章节标题 / 别名三条路由判定可达 | 294/294，missing=0 |
| `src/Arc.Avalonia.Gallery/Framework/ShotRunner.cs` | 离屏 `RenderTargetBitmap` 截图，页高上限 1400px | `{order:000}-{pageSlug}.png` |
| `docs/01~05` | 设计分析、组件规格、实现说明、性能审查、页面切换卡顿 | 与本文数字交叉核对，发现的文档漂移见 §6.4 |

### 1.2 catalog 的 section → category 结构（全部 294 项的归属骨架）

| section | 分组数 | 项数 | 分组明细（项数） |
| --- | --- | --- | --- |
| Components | 23 | 128 | Buttons 11、Gestures 3、Menus 3、Text fields 7、Special inputs 7、Selects 5、Toggles 7、Sliders 3、Pickers 6、Editors 4、Navigation 4、Expand 5、Overlays 7、Messages 5、Progress 6、Avatars 4、Cards 3、Charts 14、Advanced charts 7、Tables 7、Activity 3、Media 3、Text effects 4 |
| Special components | 5 | 55 | Glass and surfaces 11、Motion and interaction 11、Media and input 11、AI widgets 11、AI explainers 11 |
| Blocks | 6 | 111 | Marketing and site 18、Commerce 18、App shell and settings 21、Team and collaboration 16、Data and charts 20、AI 18 |

### 1.3 页面归属判定方法（每条 catalog 项如何落到某个页面）

catalog 的 294 项并不等于 294 个页面：实际是 **166 个独立条目页 + 53 个分组共享页**。
归属按 `CatalogAudit.cs` 的三级顺序静态复算（本次用 PowerShell 只读脚本，与运行时逻辑一致）：

1. **独立页**：页面源码里 `public override string ItemSlug => "…"` 且 slug 命中，共 **166** 项（special 55 + block 111 全部在此）。
2. **章节标题匹配**：共享页生成 `<helper>.Section("Title", …)` 或 `DemoUi.Section("Title", …)`
   的 `arc-heading`；其标题与 catalog `title` 规范化后相等（去非字母数字、小写），共 **88** 项。
   例：`src/Arc.Avalonia.Actions/Pages/ActionsPage.cs` 的 `ActionDemos.Section("Split button", …)` ⇒ `split-button`。
3. **别名映射**：`src/Arc.Avalonia.Demo/ArcCatalogAliases.cs` 手工声明"某项由哪个共享页演示"，共 **40** 项。
   例：`["textarea"] = new[] { "Inputs" }`、`["data-grid"] = new[] { "Data grid" }`。
4. **兜底**：0 项（若出现即说明侧栏不可达）。

> 统计口径说明：一个共享页可能同时是多项的"归属页"（如 `Inputs, part three` 覆盖 5 项）。
> 本文表格中"共享页"列给出的是**该条目的演示页标题**；标题→文件映射见 §5.1。
> 9 个独立条目页的 `ItemSlug` 与"catalog 标题的 kebab 形式"不同（截图文件名取页面 Title 的 kebab 形式），清单见附录 A。

### 1.4 复核结果

| 检查项 | 结果 |
| --- | --- |
| 294 项全部映射到某个页面/分组页 | **294/294**（独立页 166 + 标题匹配 88 + 别名 40 + 未找到 0） |
| slug 是否重复 | 0 个重复 |
| 分类项数合计 | 23 类合计 128；5 组合计 55；6 组合计 111 |
| 独立页 slug 是否都是 catalog slug | 166/166 命中 |
| 页面总数 | 166 + 53 = 219，与 README / `docs/03` §4 的 219 一致 |
| 截图数量 | `shots/light` = 219、`shots/dark` = 219，合计 438 |
| `src` 内嵌 catalog 与 docs 版 | 字节级一致 |

---

## 2. 全量清单（294 项，按 section → category）

> **表头说明**：`slug` 为 catalog 唯一键；「项目」省略 `Arc.Avalonia.` 前缀（`Core` = `src/Arc.Avalonia`，
> `Gallery` = `src/Arc.Avalonia.Gallery`）；「页面归属」对 Components 给**共享演示页标题**（均无独立页），
> 对 Special / Blocks 一律为**独立 `ArcItemPage`**（页面 Title 即标题）。
> 每类标题括注 `项数 / free·pro / 独立页数·共享页数`，均按 catalog `tier` 与 §1.3 路由统计。

### 2.1 Components（23 类 / 128 项；115 free / 13 pro；独立页 0、共享页 49）

#### Buttons（11 项 / 11 free / 0 独立页 · 5 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `button` | Button | 主/次/幽灵/危险四态的主行动按钮 | Gallery | Kit proof |
| `action-button` | Action button | 工具栏高频操作用的紧凑动作按钮 | AdvancedActions | Advanced actions |
| `split-button` | Split button | 主行动 + 备选菜单的组合按钮 | Actions | Actions |
| `button-group` | Button group | 相关操作合并为同一表面、悬停高亮滑动的按钮组 | Actions | Actions |
| `floating-button-group` | Floating button group | 软性按钮放入托盘、共享高亮在按钮间形变 | AdvancedActions | Advanced actions |
| `expanding-button-group` | Expanding button group | 悬停/聚焦时图标按钮展开标签、相邻按钮让位 | AdvancedActions | Advanced actions, part two |
| `copy-button` | Copy button | 复制值并在原位给出确认 | Actions | Actions |
| `action-swap` | Action swap | 保持位置与意图地切换动作标签（含失败重试） | Actions | Actions |
| `icon-morph` | Icon morph | 菜单↔关闭、播放↔暂停等图标形变 | AdvancedActions | Advanced actions |
| `confirm-morph` | Confirm morph | 破坏性按钮变形为内联确认、加载、可撤销结果 | Actions | Actions, part two |
| `theme-switch` | Theme switcher | 明暗主题切换的四种动效方案 | Actions | Actions, part two |

#### Gestures（3 项 / 3 free / 0 独立页 · 1 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `hold-to-confirm` | Hold to confirm | 长按而非点击来确认破坏性操作 | Media | Gestures |
| `swipe-actions` | Swipe actions | 滑动揭示行操作，也可从菜单触发同一组动作 | Media | Gestures |
| `reactions` | Reactions | 一键表态，计数滚动更新 | Media | Gestures |

#### Menus（3 项 / 3 free / 0 独立页 · 2 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `dropdown-menu` | Dropdown menu | 锚定触发点的聚焦动作菜单 | Actions | Actions, part two |
| `context-menu` | Context menu | 贴近选中对象的次要操作 | Actions | Actions, part two |
| `user-menu` | User menu | 头像后的账号、设置、主题与退出 | AdvancedActions | Advanced actions, part two |

#### Text fields（7 项 / 7 free / 0 独立页 · 3 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `input` | Input | 带标签、帮助与错误态的单行输入框 | Inputs | Inputs |
| `textarea` | Textarea | 备注/描述类多行输入 | Inputs | Inputs |
| `password-field` | Password field | 可切换明文展示的敏感输入 | Inputs | Inputs |
| `password-strength` | Password strength | 边输入边评估新密码强度 | Inputs | Inputs |
| `search-field` | Search field | 有明确提示的标准搜索入口 | Inputs | Inputs |
| `expanding-search` | Expanding search | 图标形变为搜索框并在下方给出结果 | AdvancedInputs | Advanced inputs, part three |
| `inline-edit` | Inline edit | 文本就地变为输入框完成改名 | AdvancedInputs | Advanced inputs |

#### Special inputs（7 项 / 7 free / 0 独立页 · 4 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `number-field` | Number field | 带步进控件与上下界的数字输入 | Inputs | Inputs, part two |
| `money-input` | Money input | 千分位、稳定宽度、滚动数字与最小单位的金额输入 | Inputs | Inputs, part two |
| `phone-input` | Phone input | 国家选择 + 输入格式化 + E.164 输出 | AdvancedInputs | Advanced inputs, part two |
| `scrub-input` | Scrub input | Figma 式拖动标签改值，支持单位与算术表达式 | AdvancedInputs | Advanced inputs |
| `tag-input` | Tag input | 短文本转为可删除标签 | Inputs | Inputs, part two |
| `mention-input` | Mention input | 支持 @人 / #频道 的单令牌提及输入，光标处给建议 | AdvancedInputs | Advanced inputs, part three |
| `shortcut-recorder` | Shortcut recorder | 把按键组合录成键帽，带冲突警告与速查表 | AdvancedInputs | Advanced inputs, part two |

#### Selects（5 项 / 5 free / 0 独立页 · 3 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `select` | Select | 键盘友好的紧凑下拉选择 | Inputs | Inputs, part two |
| `combobox` | Combobox | 不离开字段即可搜索并选择 | Inputs | Inputs, part two |
| `multi-select` | Multi-select | 多值选择同时保持字段可读 | Inputs | Inputs, part three |
| `morph-select` | Morph select | 触发器长成列表，带滑动高亮与 type-ahead | AdvancedInputs | Advanced inputs, part four |
| `chip-group` | Chip group | 少量分面的芯片式筛选，选中时芯片形变 | Inputs | Inputs, part three |

#### Toggles（7 项 / 7 free / 0 独立页 · 3 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `radio-cards` | Radio cards | 带滑动选中环、价格与描述槽的选项卡 | AdvancedInputs | Advanced inputs, part five |
| `billing-toggle` | Billing toggle | 月/年切换，带节省徽标与价格滚动 | AdvancedInputs | Advanced inputs, part five |
| `checkbox` | Checkbox | 清晰可读的二元勾选 | Inputs | Inputs, part three |
| `radio-group` | Radio group | 可见选项中的单选组 | Inputs | Inputs, part three |
| `switch` | Switch | 即时生效设置的触感开关 | Inputs | Inputs, part three |
| `segmented-control` | Segmented control | 少量相关视图间的分段切换 | Inputs | Inputs, part four |
| `rating` | Rating | 无障碍的数字评价采集 | Inputs | Inputs, part four |

#### Sliders（3 项 / 3 free / 0 独立页 · 2 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `slider` | Slider | 单值或区间滑杆，轨道跟随指针 | Inputs | Inputs, part four |
| `elastic-slider` | Elastic slider | 越界橡皮筋回弹、数值标签摇摆、带档位吸附的滑杆 | AdvancedInputs | Advanced inputs, part four |
| `range-histogram-slider` | Range histogram slider | 直方图数据之上的价格区间滑杆，吸附柱边 | AdvancedInputs | Advanced inputs, part four |

#### Pickers（6 项 / 5 free · 1 pro / 0 独立页 · 4 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `calendar` | Calendar | 紧凑月视图浏览与选择日期 | Pickers | Pickers |
| `date-picker` | Date picker | 不丢上下文的日期选择 | Pickers | Pickers |
| `date-range-picker` | Date range picker | 触发器长成双月区间的范围选择，带预设 | Pickers | Ranges and time |
| `time-picker` | Time picker | 按步长列表选择时间，键盘友好 | Pickers | Ranges and time |
| `color-picker` | Color picker | 色板长成取色器：格式切换、吸管、收藏、对比度 | Pickers | Color |
| `emoji-picker` | Emoji picker | 完整表情选择器：418 个离线表情、分类、肤色、最近、虚拟化网格 | Pickers | Emoji picker |

#### Editors（4 项 / 3 free · 1 pro / 0 独立页 · 3 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `rich-text-editor` | Rich text editor | Markdown 快捷键 + 浮动工具条 + slash 菜单 + HTML/Markdown 输出 | Content | Content |
| `signature-pad` | Signature pad | 速度感墨迹签名，支持撤销、回放、PNG/SVG 导出 | Pickers | Files and ink |
| `file-dropzone` | File dropzone | 多文件拖放的宽目标区 | Pickers | Files and ink |
| `avatar-upload` | Avatar upload | 点击/拖入照片后就地裁剪（拖动、缩放、进度环） | Pickers | Avatar upload |

#### Navigation（4 项 / 3 free · 1 pro / 0 独立页 · 2 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `tabs` | Tabs | 同一上下文内切换相关内容 | Actions | Navigation |
| `breadcrumb` | Breadcrumb | 表达页面在层级中的位置 | Actions | Navigation |
| `pagination` | Pagination | 带明确边界地翻越长集合 | Actions | Navigation |
| `mega-menu-morph` | Mega menu morph | 站点头部菜单共享同一浮层面板并变形 | Actions | Mega menu |

#### Expand（5 项 / 5 free / 0 独立页 · 4 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `scroll-area` | Scroll area | 细滚动条 + 溢出时才出现的边缘渐隐滚动容器 | Media | Media layout |
| `accordion` | Accordion | 就地逐步展开补充信息 | Actions | Navigation |
| `expandable-card` | Expandable card | 密集卡片按请求扩大 | Effects | Effects |
| `resizable-panels` | Resizable panels | 拖动分隔条在面板间分配空间 | Media | Media layout |
| `expandable-text` | Expandable text | 长文本截断 + 渐隐 + 展开/收起（仅溢出时出现） | Actions | Disclosure |

#### Overlays（7 项 / 6 free · 1 pro / 0 独立页 · 4 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `dialog` | Dialog | 需要专注决策的模态面 | Actions | Overlays |
| `drawer` | Drawer | 用于专注工作的侧向临时面板 | Actions | Overlays |
| `bottom-sheet` | Bottom sheet | peek/full 两档、跟随手指的底部抽屉 | Effects | Effects |
| `popover` | Popover | 锚定的小型上下文浮层 | Actions | Overlays, part two |
| `hover-card` | Hover card | 悬停/聚焦预览人物或链接 | Effects | Effects |
| `tooltip` | Tooltip | 陌生控件的短说明 | Actions | Overlays |
| `share-access` | Share access | 分享面板：邮箱邀请芯片、角色菜单、链接权限、撤销 | Actions | Share access |

#### Messages（5 项 / 5 free / 0 独立页 · 3 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `alert` | Alert | 帮助恢复或继续的持久提示 | Actions | Overlays, part two |
| `toast` | Toast | 后台动作完成后的短暂确认 | Actions | Overlays, part two |
| `toast-stack` | Toast stack | 边缘堆叠的 Toast 容器，到期自动移除 | Actions | Actions, part two |
| `announcement-bar` | Announcement bar | 轮播消息 + 倒计时 + 关闭折叠的顶部通告 | Effects | Effects |
| `cookie-consent` | Cookie consent | 同意条变形为偏好设置，再折成重开按钮 | Effects | Effects |

#### Progress（6 项 / 6 free / 0 独立页 · 5 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `progress` | Progress | 已知任务完成度 | Feedback | Feedback |
| `skeleton` | Skeleton | 加载中占位，保持布局 | Feedback | Feedback |
| `stepper` | Stepper | 多步流程中的位置与已完成项 | Feedback | Steps |
| `countdown` | Countdown | 发布倒计时，归零时变为直播态 | Feedback | Time |
| `usage-meter` | Usage meter | 配额用量与上限、剩余/超额文案 | Feedback | Identity |
| `onboarding-checklist` | Onboarding checklist | 新工作区逐步设置引导 | Content | Flows |

#### Avatars（4 项 / 3 free · 1 pro / 0 独立页 · 3 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `avatar` | Avatar | 人物/账号的紧凑身份标记 | Feedback | Identity |
| `avatar-group` | Avatar group | 小空间内表达团队/贡献者集合 | Feedback | Identity |
| `badge` | Badge | 状态、分类或元数据的小标签 | Gallery | Kit proof |
| `presence-stack` | Presence stack | 在线成员头像堆叠，带计数、输入中/空闲点、悬停展开 | Feedback | Presence |

#### Cards（3 项 / 3 free / 0 独立页 · 3 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `card` | Card | 相关内容与动作的容器 | Gallery | Kit proof |
| `metric-card` | Metric card | 需要上下文的数字摘要卡 | Feedback | Identity |
| `empty-state` | Empty state | 无内容时给出有用下一步 | Feedback | Data grid |

#### Charts（14 项 / 13 free · 1 pro / 0 独立页 · 5 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `line-chart` | Line chart | 多系列折线 + 滑动十字准线 + 图例开关 | Charts | Charts |
| `bar-chart` | Bar chart | 分类柱状对比，可刷读单柱 | Charts | Charts |
| `donut-chart` | Donut chart | 弧段形变的环图，中心值滚动 | Charts | Charts |
| `streamgraph` | Streamgraph | wiggle 基线的层叠流图，可隔离单层 | Charts | Charts, part two |
| `brush-chart` | Brush chart | 密集时序 + 概览条缩放窗口 + 事件标记 | Charts | Brush chart |
| `waffle-chart` | Waffle chart | 10×10 百分比格，单元格飞向新分组 | Charts | Charts, part two |
| `slope-chart` | Slope chart | 前后排名斜率图，切换数据时整体滑动 | Charts | Charts |
| `sparkline` | Sparkline | 数值旁的迷你趋势 | Charts | Charts |
| `gauge` | Gauge | 已知区间内的数值仪表 | Charts | Charts |
| `activity-heatmap` | Activity heatmap | 一年每日活跃度热力图 | Feedback | Explorer |
| `animated-counter` | Animated counter | 变化总量获得运动感 | Feedback | Time |
| `ridgeline` | Ridgeline | 分组分布山脊图，悬停抬升读四分位 | Charts | Charts, part two |
| `treemap` | Treemap | 可点选钻取的方形树图，带面包屑 | Charts | Charts, part two |
| `split-bar` | Split bar | 单条 100% 份额条，悬停加宽读精确值 | Charts | Charts, part two |

#### Advanced charts（7 项 / 0 free · 7 pro / 0 独立页 · 3 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `sunburst` | Sunburst | 同心环层级 + 钻取 + 面包屑 + 滚动合计 | Charts | Advanced charts |
| `sankey-flow` | Sankey flow | 阶段间流向丝带 + 粒子 + 悬浮追踪 | Charts | Advanced charts |
| `funnel-chart` | Funnel chart | 转化漏斗 + 每步流失 + 设备拆分 | Charts | Advanced charts |
| `radar-chart` | Radar chart | 多轴轮廓对比，切换档案时形变 | Charts | Advanced charts, part two |
| `realtime-stream` | Realtime stream | 60fps 滚动的实时流线，悬停冻结读数 | Charts | Advanced charts, part two |
| `race-bar-chart` | Race bar chart | 排名随时间竞速，可播放/拖动时间轴 | Charts | Advanced charts, part two |
| `data-grid` | Data grid | 电子表格：排序、列宽、多选、行内编辑、汇总 | Feedback | Data grid |

#### Tables（7 项 / 7 free / 0 独立页 · 5 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `sortable-data-table` | Sortable data table | 可排序列的结构化记录对比表 | Feedback | Data |
| `tree-table` | Tree table | 嵌套行表格：三态选择、层级排序、懒加载 | Feedback | Tree table |
| `tree-view` | Tree view | 嵌套文件夹/结构化内容导航 | Feedback | Explorer |
| `reorderable-list` | Reorderable list | 直接拖放排序的列表 | Content | Content |
| `filter-toolbar` | Filter toolbar | 集合筛选靠近内容且易重置 | Feedback | Data |
| `code-block` | Code block | 带层级与复制入口的代码展示 | Feedback | Data views |
| `json-viewer` | JSON viewer | 可折叠 JSON 树，支持搜索、长数组分页、复制路径 | Feedback | Data views |

#### Activity（3 项 / 3 free / 0 独立页 · 2 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `timeline` | Timeline | 按天分组、最新在前的活动时间线 | Feedback | Activity |
| `comment-thread` | Comment thread | 回复、表情、@、行内编辑与解决的评论串 | Feedback | Activity |
| `chat-thread` | Chat thread | 分组消息、表情、回执、输入中与附件输入框 | Content | Flows |

#### Media（3 项 / 3 free / 0 独立页 · 1 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `image-compare` | Image compare | 拖动分隔条对比两张图 | Media | Media |
| `carousel` | Carousel | 拖动/滑动/方向键浏览幻灯片 | Media | Media |
| `card-stack` | Card stack | 逐张审阅卡组，支持抛出与撤销 | Media | Media |

#### Text effects（4 项 / 4 free / 0 独立页 · 1 共享页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属（共享页） |
| --- | --- | --- | --- | --- |
| `text-reveal` | Text reveal | 克制地揭示一小段内容 | Effects | Text effects |
| `in-view-title` | In-view title | 滚动入视口时引入分节标题 | Effects | Text effects |
| `text-morph` | Text morph | 逐字把标签形变为下一状态 | Effects | Text effects |
| `text-shimmer` | Text shimmer | 用微光表示持续进行的工作 | Effects | Text effects |

---

### 2.2 Special components（5 组 / 55 项；1 free · 54 pro；**全部 55 项各有独立 `ArcItemPage`**）

> 55 项页面 Title = catalog 标题；其中 5 项的页面 slug 与 catalog slug 不同（如 `glass-tabbar` → `glass-tab-bar`），
> 完整清单见附录 A。Special 是实现独立、动效自绘的展示型控件，多数只暴露"演示状态"，不是通用业务控件。

#### Glass and surfaces（11 项 / 11 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `glass-menu` | Glass menu | 玻璃动作菜单：触发器形变为面板，高亮胶囊滑动、一层子菜单 | SpecialGlass | 独立页 |
| `glass-segmented` | Glass segmented | 玻璃分段控件：液体指示器拉伸、可拖动甩出 | SpecialGlass | 独立页 |
| `glass-card` | Glass card | 指针跟随高光、可倾斜、点击变形为详情面板的玻璃卡 | SpecialGlass | 独立页 |
| `glass-tabbar` | Glass tab bar | 折射下层内容的浮动玻璃标签栏，玻璃透镜可拖动/放大 | SpecialGlass | 独立页 |
| `holo-card` | Holo card | 随视角倾斜的全息箔会员卡，可翻面 | SpecialGlass | 独立页 |
| `wallet-stack` | Wallet stack | 扇形卡片堆，抽出一张查看其流水 | SpecialGlass | 独立页 |
| `sheet-stack` | Sheet stack | 带层深的嵌套面板栈，拖拽关闭，宽屏变堆叠对话框 | SpecialGlass | 独立页 |
| `control-center` | Control center | 工作区快捷设置：磁贴展开详情、时长表盘、回弹滑杆 | SpecialGlass | 独立页 |
| `card-to-article` | Card to article | 故事卡无缝展开为全文，下拉缩回原槽位 | SpecialGlass | 独立页 |
| `link-unfurl` | Link unfurl | 粘贴 URL 后微光加载，再展开为富预览卡 | SpecialGlass | 独立页 |
| `share-sheet` | Share sheet | 分享按钮展开为复制链接/权限/渠道/联系人面板 | SpecialGlass | 独立页 |

#### Motion and interaction（11 项 / 10 pro · 1 free / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `action-morph` | Action morph | 浮动按钮 → 快捷菜单 → 内联表单 → 完成态的连续形变 | SpecialMotion | 独立页 |
| `morph-nav` | Morph nav | 导航栏作为一个表面形变为菜单/搜索/紧凑态 | SpecialMotion | 独立页 |
| `dock` | Dock | 浮动工具坞：滑动标签、弹簧选中、托盘生长 | SpecialMotion | 独立页 |
| `liquid-tab-bar` | Liquid tab bar | 透镜在标签间滑动的液体会话标签栏 | SpecialMotion | 独立页 |
| `orbit-menu` | Orbit menu | 长按后操作环绕飞出，拖动选取 | SpecialMotion | 独立页 |
| `orbit-logos` | Orbit logos | 品牌 logo 在倾斜轨道上环绕产品标记 | SpecialMotion | 独立页 |
| `booking-pill` | Booking pill | 单个药丸形变走完人数/日期/时间/出票预订流 | SpecialMotion | 独立页 |
| `keycap-keyboard` | Keycap keyboard | 机械键帽下沉发光的打字反馈键盘 | SpecialMotion | 独立页 |
| `slot-text` | Slot text | 文字/数字像老虎机滚轮错峰滚入新值 | SpecialMotion | 独立页 |
| `sand-text` | Sand text | 沙粒浇铸成标题、光标可挖散并自愈 | SpecialMotion | 独立页 |
| `typewriter-terminal` | Typewriter terminal | 打字机式回放 CLI 会话，带语法着色与进度行 | SpecialMotion | 独立页 |

#### Media and input（11 项 / 11 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `product-gallery` | Product gallery | 悬停放大、缩略图滑动、颜色/尺码变体交叉淡入 | SpecialMedia | 独立页 |
| `lightbox-gallery` | Lightbox gallery | 瀑布流照片从格子缩放到可滑动/捏合的查看器 | SpecialMedia | 独立页 |
| `photo-grid` | Photo grid | 捏合缩放级别、从单元格直接开图 | SpecialMedia | 独立页 |
| `cover-flow` | Cover flow | 有真实地面阴影与倒影的深度封面流 | SpecialMedia | 独立页 |
| `voice-orb` | Voice orb | 聆听/思考/说话的语音模式球 + 实时转写 + 通话控制 | SpecialMedia | 独立页 |
| `now-playing` | Now playing | 迷你播放器连续形变为全屏播放器 | SpecialMedia | 独立页 |
| `voice-recorder` | Voice recorder | 实时波形录制、回听拖动、发送进线程 | SpecialMedia | 独立页 |
| `page-curl` | Page curl | 拖页角翻页的数字杂志，带真实卷边与光照 | SpecialMedia | 独立页 |
| `sticker-peel` | Sticker peel | 撕贴纸的卷边效果，可移动并贴回 | SpecialMedia | 独立页 |
| `dot-grid` | Dot grid | 点阵在光标处透镜凸起、点击涟漪 | SpecialMedia | 独立页 |
| `gravity-well` | Gravity well | 网格向光标弯曲、点击发出冲击波 | SpecialMedia | 独立页 |

#### AI widgets（11 项 / 11 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `agent-status` | Agent status | 运行中 Agent 的状态药丸：实时环、当前活动、已用时、可展开步骤 | SpecialAIWidgets | 独立页 |
| `tool-call` | Tool call | 工具调用行：工具标记、运行态、输入/输出展开、审批与重试 | SpecialAIWidgets | 独立页 |
| `agent-plan` | Agent plan | Agent 计划：草稿可增删排序，运行时变实时清单 | SpecialAIWidgets | 独立页 |
| `agent-timeline` | Agent timeline | 单次编排运行的多代理共享时间轴、移交连线、回放播放头 | SpecialAIWidgets | 独立页 |
| `context-window` | Context window | 200 格上下文占用图，含压缩阈值与模型容量 | SpecialAIWidgets | 独立页 |
| `ink-fluid` | Ink fluid | 光标搅动、静置自愈的实时流体背景 | SpecialAIWidgets | 独立页 |
| `activity-rings` | Activity rings | 每日目标活动环，超 100% 走第二圈 | SpecialAIWidgets | 独立页 |
| `stretch-refresh` | Stretch refresh | 下拉时拉伸提示"何时松手"刷新 | SpecialAIWidgets | 独立页 |
| `morph-loader` | Morph loader | 形状互相形变的加载器，完成折成对勾/叉 | SpecialAIWidgets | 独立页 |
| `skeleton-morph` | Skeleton morph | 骨架块逐块长成真实内容而非直接替换 | SpecialAIWidgets | 独立页 |
| `black-hole` | Black hole | 删除动画：条目被吸入奇点，撤销时再转出 | SpecialAIWidgets | 独立页 |

#### AI explainers（11 项 / 11 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `data-flow` | Data flow | 泛化的数据流示意插画 | SpecialAIExplainers | 独立页 |
| `payment-flow` | Payment flow | 卡支付→处理器→网络→银行→清算的分步动画 | SpecialAIExplainers | 独立页 |
| `multi-region-failover` | Multi-region failover | 主区降级、健康检查失败、流量切换到健康区再恢复 | SpecialAIExplainers | 独立页 |
| `vector-search` | Vector search | 文档嵌入、聚类、查询近邻与相似度排序的示意 | SpecialAIExplainers | 独立页 |
| `agent-loop` | Agent loop | "取上下文→行动→验证→重复"的代理循环示意 | SpecialAIExplainers | 独立页 |
| `mixture-of-experts` | Mixture of experts | token 被路由到 Top-k 专家并合并输出的示意 | SpecialAIExplainers | 独立页 |
| `speculative-decoding` | Speculative decoding | 草稿模型猜 token、目标模型一次验证的示意 | SpecialAIExplainers | 独立页 |
| `prompt-cache` | Prompt cache | 共享前缀缓存一次、后续请求命中省时省钱的示意 | SpecialAIExplainers | 独立页 |
| `reasoning-tree` | Reasoning tree | 思维树搜索：候选打分、弱枝淡出、最优路径绘制 | SpecialAIExplainers | 独立页 |
| `time-dial` | Time dial | 转动表盘选择小时，查看各城市上班时间 | SpecialAIExplainers | 独立页 |
| `date-reel` | Date reel | iOS 式 3D 滚轮日期时间选择，带惯性与吸附 | SpecialAIExplainers | 独立页 |

---

### 2.3 Blocks（6 组 / 111 项；22 free · 89 pro；**全部 111 项各有独立 `ArcItemPage`**）

> Blocks 是"页面级组合"：每页由 128 组件 + 项目内私有绘制控件拼成，数据为代码内示例。
> 交互可操作但省略网络、路由与后端（见 `docs/03-implementation.zh.md` §5）。

#### Marketing and site（18 项 / 13 free · 5 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `site-header` | Site header | 滚动变实、链接滑动、巨型菜单、移动端抽屉的站点头部 | BlocksMarketing | 独立页 |
| `site-footer` | Site footer | 链接列 + 订阅 + 大字渐隐 Arc 标记的页脚 | BlocksMarketing | 独立页 |
| `hero-section` | Hero section | 三种整屏 SaaS Hero（仪表盘升起 / 工作流图 / 编辑式大字） | BlocksMarketing | 独立页 |
| `faq-section` | FAQ section | 手风琴 / 主题侧栏 / 可搜索高亮三种 FAQ | BlocksMarketing | 独立页 |
| `contact-section` | Contact section | 校验表单变形确认 + 支持渠道 + 带当地时间办公室卡 | BlocksMarketing | 独立页 |
| `blog-grid` | Blog grid | 博客索引：精选、分类筛选、卡片、分页、就地阅读 | BlocksMarketing | 独立页 |
| `comparison-table` | Comparison table | 我方 vs 对方的对比表，粘性表头与手机堆叠视图 | BlocksMarketing | 独立页 |
| `stats-band` | Stats band | 进入视口时计数上升的数字带 | BlocksMarketing | 独立页 |
| `cta-section` | CTA section | 居中 / 分栏 / 可关闭横幅三种收尾 CTA | BlocksMarketing | 独立页 |
| `newsletter-signup` | Newsletter signup | 往期期刊堆叠框住的订阅表单 | BlocksMarketing | 独立页 |
| `logo-marquee` | Logo marquee | 持续滚动、可暂停的品牌 logo 带 | BlocksMarketing | 独立页 |
| `page-header` | Page header | 滚动折叠成紧凑栏、标签计数滚动的项目页头 | BlocksMarketing | 独立页 |
| `hero-signup` | Hero signup | 全屏等待名单 Hero，邮箱校验后按钮长成确认 | BlocksMarketing | 独立页 |
| `spotlight-grid` | Spotlight grid | 光斑跟随指针、卡片内播放真实 UI 演示的功能网格 | BlocksMarketing | 独立页 |
| `scroll-story` | Scroll story | 滚动文案驱动固定产品视图走完各步骤 | BlocksMarketing | 独立页 |
| `feature-bento` | Feature bento | 六格功能 Bento，悬停时小 UI 演示该功能 | BlocksMarketing | 独立页 |
| `plan-comparison` | Plan comparison | 套餐与计费周期之间可读的差异对比 | BlocksMarketing | 独立页 |
| `pricing-calculator` | Pricing calculator | 席位滑杆 + 月年切换驱动价格与推荐徽标 | BlocksMarketing | 独立页 |

#### Commerce（18 项 / 2 free · 16 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `product-listing` | Product listing | 价格/颜色/尺码筛选、排序、悬停换图、快速加购的商店网格 | BlocksCommerce | 独立页 |
| `product-detail` | Product detail | 可滑动画廊、变体、加购确认、配送、评价摘要的商品页 | BlocksCommerce | 独立页 |
| `cart-drawer` | Cart drawer | 商品飞入购物车、数量滚动、抽屉内结算 | BlocksCommerce | 独立页 |
| `checkout-summary` | Checkout summary | 订单摘要，优惠码变芯片，折扣/税与支付按钮状态 | BlocksCommerce | 独立页 |
| `usage-pricing` | Usage pricing | 拖动席位与流量自动匹配套餐的计算器 | BlocksCommerce | 独立页 |
| `usage-billing` | Usage billing | 用量账单：预测花费线、限额计量、告警、升级确认 | BlocksCommerce | 独立页 |
| `wallet-card` | Wallet card | 余额、近期流水、聚焦资金动作的账户卡 | BlocksCommerce | 独立页 |
| `invoice-studio` | Invoice studio | 可编辑发票，实时合计与清晰保存流 | BlocksCommerce | 独立页 |
| `delivery-tracker` | Delivery tracker | 标记在站点间滑动、到达时间滚动的物流跟踪 | BlocksCommerce | 独立页 |
| `cancel-flow` | Cancel flow | 按原因给出针对性挽留，最后长按取消 | BlocksCommerce | 独立页 |
| `integrations` | Integrations | 集成目录：Connect 形变为 Connected，筛选实时重计数 | BlocksCommerce | 独立页 |
| `help-center` | Help center | 即时搜索、主题卡、文章阅读与大纲的帮助中心 | BlocksCommerce | 独立页 |
| `support-widget` | Support widget | 应用内帮助入口，长成文章搜索、阅读与聊天 | BlocksCommerce | 独立页 |
| `support-conversation` | Support conversation | 带快捷回复与反馈的支持会话线程 | BlocksCommerce | 独立页 |
| `changelog-feed` | Changelog feed | 可按月筛选、就地展开的更新日志 | BlocksCommerce | 独立页 |
| `empty-states` | Empty states | 一份插画在四个空态场景间形变 | BlocksCommerce | 独立页 |
| `billing-overview` | Billing overview | 计费页：计划卡变形为选择器，用量/发票/卡紧随 | BlocksCommerce | 独立页 |
| `checkout-live` | Live checkout | 编辑即重算的订阅结账，再滑入中心展开为收据 | BlocksCommerce | 独立页 |

#### App shell and settings（21 项 / 7 free · 14 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `workspace-sidebar` | Workspace sidebar | 工作区切换、实时搜索、可拖收藏、内联项目的产品侧栏 | BlocksApp | 独立页 |
| `sidebar-rail` | Sidebar rail | 侧栏折叠成图标栏、手机变滑入抽屉的应用外壳 | BlocksApp | 独立页 |
| `settings-page` | Settings page | 账户设置：滑动分节导航 + 编辑即出现的保存条 | BlocksApp | 独立页 |
| `settings-command` | Settings command | 设置页搜索框长成可飞往任意设置项的调色板 | BlocksApp | 独立页 |
| `docs-sidebar` | Docs sidebar | 实时过滤的文档导航树、版本切换、本页大纲 | BlocksApp | 独立页 |
| `inbox-sidebar` | Inbox sidebar | 邮件侧栏：写信按钮长成编辑器，文件夹接收拖放 | BlocksApp | 独立页 |
| `inbox-triage` | Inbox triage | 收件箱分诊：归档、稍后、恢复与单步撤销 | BlocksApp | 独立页 |
| `command-palette` | Command palette | 完整键盘驱动的全局命令面板（搜索/分组/快捷键） | BlocksApp | 独立页 |
| `notification-center` | Notification center | 通知中心：未读徽标、All/Unread、展开、已读、批量 | BlocksApp | 独立页 |
| `selection-toolbar` | Selection toolbar | 选中条目后出现的上下文批量操作条 | BlocksApp | 独立页 |
| `file-upload` | File upload | 文件选择全流程：约束、进度、错误反馈 | BlocksApp | 独立页 |
| `otp-input` | OTP input | 六位验证码输入，支持粘贴与键盘导航 | BlocksApp | 独立页 |
| `multi-step-form` | Multi-step form | 一次只呈现一个决策、保留进度的分步表单 | BlocksApp | 独立页 |
| `signup-form` | Sign up form | 注册流程：字段校验、密码强度、完成态 | BlocksApp | 独立页 |
| `sign-in` | Sign in | 登录卡从邮箱形变到六位码再到账户 | BlocksApp | 独立页 |
| `login-centered` | Centered login | passkey 优先的居中登录卡，经过邮箱与验证码 | BlocksApp | 独立页 |
| `login-immersive` | Immersive login | 暮色照片上的玻璃魔法链接登录，发送后信封折起 | BlocksApp | 独立页 |
| `login-split` | Login split | 全屏分栏登录：邮箱优先，形变为密码步骤 | BlocksApp | 独立页 |
| `security-settings` | Security settings | 账号安全：两步验证引导、改密、活跃会话 | BlocksApp | 独立页 |
| `search-results` | Search results | 分面工作区搜索：建议、计数、过滤芯片、预览面板 | BlocksApp | 独立页 |
| `media-player` | Media player | 带动态播放列表与完整播放控制的音频播放器 | BlocksApp | 独立页 |

#### Team and collaboration（16 项 / 0 free · 16 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `team-directory` | Team directory | 可搜索的人员目录 + 团队筛选 + 聚焦资料面板 | BlocksTeam | 独立页 |
| `team-showcase` | Team showcase | 图像主导的团队介绍，资料就地展开 | BlocksTeam | 独立页 |
| `testimonial-stage` | Studio perspectives | 人物肖像驱动的编辑式故事与阅读态 | BlocksTeam | 独立页 |
| `team-members` | Team members | 成员列表：输入邮箱变芯片并飞入待邀请，席位滚动 | BlocksTeam | 独立页 |
| `invite-people` | Invite people | 邀请页：角色、批量粘贴校验、席位计量、头像飞入 | BlocksTeam | 独立页 |
| `availability-picker` | Availability picker | 跨多人寻找会议时间并汇成选择 | BlocksTeam | 独立页 |
| `project-board` | Project board | 在阶段间移动工作的活动工作流看板 | BlocksTeam | 独立页 |
| `kanban-board` | Kanban board | 泳道 + 抬卡倾斜 + WIP 限制的看板 | BlocksTeam | 独立页 |
| `week-calendar` | Week calendar | 周历：彩色日历、全天行、日/议程视图 | BlocksTeam | 独立页 |
| `api-keys` | API keys | API 密钥管理：创建展开为表单，密钥只显示一次 | BlocksTeam | 独立页 |
| `webhooks` | Webhooks | Webhook 控制台：投递历史、载荷检查、重试 | BlocksTeam | 独立页 |
| `roles-permissions` | Roles and permissions | 权限矩阵：组开关涟漪、角色复制、评审条 | BlocksTeam | 独立页 |
| `env-variables` | Environment variables | 环境变量/密钥管理：掩码、作用域、.env 导入、指纹 diff | BlocksTeam | 独立页 |
| `release-readiness` | Release readiness | 发布检查清单：前置、进度与最终动作同屏 | BlocksTeam | 独立页 |
| `onboarding-illustration` | Onboarding illustration | 引导卡插画在文件夹→文件→团队→徽章间形变 | BlocksTeam | 独立页 |
| `cohort-retention` | Cohort retention | 留存三角与留存曲线，可随分群重绘 | BlocksTeam | 独立页 |

#### Data and charts（20 项 / 0 free · 20 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `metrics-dashboard` | Metrics dashboard | 密集分析视图：KPI 标签、可检查图表、来源列表 | BlocksData | 独立页 |
| `revenue-explorer` | Revenue explorer | 刷选缩放的收入图，路径/坐标/数字一起滑动 | BlocksData | 独立页 |
| `journey-flow` | Journey flow | 用户旅程桑基图，按渠道形变并可追踪分群 | BlocksData | 独立页 |
| `usage-forecast` | Usage forecast | 计费周期用量 + 预测锥 + 可拖动预算线 | BlocksData | 独立页 |
| `mrr-waterfall` | MRR waterfall | MRR 桥接瀑布，浮动条按周期形变并可下钻账户 | BlocksData | 独立页 |
| `metric-explorer` | Metric explorer | KPI 卡展开为可刷图，指标与区间间形变 | BlocksData | 独立页 |
| `activity-terrain` | Activity terrain | 每小时活动的 3D 地形，可环绕/缩放/压平成热图 | BlocksData | 独立页 |
| `revenue-globe` | Revenue globe | 点阵地球，实时支付弧线回家，可回放一天 | BlocksData | 独立页 |
| `customer-galaxy` | Customer galaxy | 2400 账户的星系，按套餐/地区/健康度聚簇 | BlocksData | 独立页 |
| `semantic-zoom` | Semantic zoom | 公司语义地图，飞入时揭示团队/项目/任务/人 | BlocksData | 独立页 |
| `layout-morph` | Layout morph | 24 卡在网格/瀑布/列表/扇形/轮播/3D 螺旋间飞行 | BlocksData | 独立页 |
| `budget-variance` | Budget variance | 部门预算 vs 实际的子弹图，YTD 切换弹簧重排 | BlocksData | 独立页 |
| `choropleth-explorer` | Choropleth explorer | 分级填色地图 + 国家切换 + 图例/排名/缩放 | BlocksData | 独立页 |
| `small-multiples` | Small multiples | 小倍数折线阵列：同步准线、共享/独立 y 轴 | BlocksData | 独立页 |
| `connected-scatter` | Connected scatter | CAC×LTV 连通散点，路径自绘 + 刷子游标 | BlocksData | 独立页 |
| `horizon-chart` | Horizon chart | 14 天序列折成 horizon 行，可切换层数/排序 | BlocksData | 独立页 |
| `line-replay` | Line replay | 时间回放折线：播放头、速度控制、关键时刻标注 | BlocksData | 独立页 |
| `exploded-view` | Exploded view | 滚动驱动的手机分层爆炸视图，再重新合拢 | BlocksData | 独立页 |
| `kpi-drilldown` | KPI drilldown | KPI 卡展开为含周期对比与明细表的详图 | BlocksData | 独立页 |
| `market-terminal` | Market terminal | 交易终端：K 线/成交量/均线/十字读数/自选/下单 | BlocksData | 独立页 |

#### AI（18 项 / 0 free · 18 pro / 独立页）

| slug | 标题 | 中文用途一句话 | 项目 | 页面归属 |
| --- | --- | --- | --- | --- |
| `ai-composer` | AI composer | AI 输入框：附件、/ 命令、@ 提及、模型/思考菜单、停止键 | BlocksAI | 独立页 |
| `ai-chat` | AI chat | 全屏 AI 聊天：markdown 流式、可折叠推理、工具芯片、引用 | BlocksAI | 独立页 |
| `ai-side-panel` | AI side panel | 停靠在页面旁的助手：读页面、按词 diff 审阅、一键撤销 | BlocksAI | 独立页 |
| `research-run` | Research run | 深度研究：规划搜索、阅读来源、带引用流式作答 | BlocksAI | 独立页 |
| `ai-answer` | AI answer | 单条答案排成文章：思考、来源卡、引用高亮 | BlocksAI | 独立页 |
| `model-picker` | Model picker | 模型选择药丸：推荐模型、思考强度、按供应商浏览 | BlocksAI | 独立页 |
| `model-arena` | Model arena | 盲测竞技：同一提示双模型同速流式，投票后揭晓 | BlocksAI | 独立页 |
| `prompt-studio` | Prompt studio | 提示词模板：变量芯片、试跑、版本对比与上线 | BlocksAI | 独立页 |
| `document-extract` | Document extraction | 文档转结构化字段：字段随阅读填充，悬停定位原文 | BlocksAI | 独立页 |
| `agent-memory` | Agent memory | 助手记忆管理：筛选/搜索/编辑/遗忘，开关记忆来源 | BlocksAI | 独立页 |
| `agent-tasks` | Agent tasks | 后台代理任务队列：仓库/分支、排队→运行→待审 | BlocksAI | 独立页 |
| `agent-approvals` | Agent approvals | 审批卡：等待的工具调用、理由、参数、批准/拒绝/总是允许 | BlocksAI | 独立页 |
| `agent-routines` | Agent routines | 定时代理：句式化日程、下次运行倒计时、连接器与去向 | BlocksAI | 独立页 |
| `agent-orchestrator` | Agent orchestrator | 主代理拆计划、并行工作泳道、汇总结果 | BlocksAI | 独立页 |
| `mcp-servers` | MCP servers | MCP 服务器管理：工具上下文成本、连接/暂停、单工具开关 | BlocksAI | 独立页 |
| `knowledge-base` | Knowledge base | 知识库：来源解析→切块→嵌入→索引，检索回显片段 | BlocksAI | 独立页 |
| `eval-suite` | Eval suite | 评估套件：逐例流式结果、与基线对比、标出回归 | BlocksAI | 独立页 |
| `agent-run` | Agent run | 单次代理运行：步骤流式、工具展开、停在双文件 diff 待审 | BlocksAI | 独立页 |

---

## 3. 源码级控件统计（按项目）

### 3.1 统计方法（可复现）

| 指标 | 方法 | 命令/模式 |
| --- | --- | --- |
| `.cs` 文件数 | 排除 `bin/`、`obj/`、`bin-alt/` | `Get-ChildItem src -Recurse -Filter *.cs` + 路径过滤 |
| 公共类 | 非 `Pages/` 源码中的 `public [sealed/abstract/static/partial/record] class X` | `grep "public\s+(...)class\s+(\w+)"` |
| **控件类** | 在公共类基础上，**沿 `class X : Base` 的继承链递归**，基类名命中 Avalonia 控件基类集合（`Control`、`TemplatedControl`、`ContentControl`、`Panel`、`Border`、`Button`、`Window`、`Slider`… 共 50 个）即计为控件；基类不可解析时按"非控件"处理 | 见 §1.3 脚本思路；`Arc*` 前缀不是必要条件（如 `PresenceMember` 是模型） |
| internal 类 | `internal … class X`，含绘制 stage/数据助手，**不计入公共 API** | 同上 |
| 页面数 | `ItemSlug` 有值 = 独立条目页；`class XPage : ArcPage` 且有 `Title` = 分组共享页 | 反射契约见 `src/Arc.Avalonia.Demo/ArcPageRegistry.cs` |
| ControlTheme | `*.axaml` 主题文件数（不含 bin/obj/Assets） | 与 `docs/04` 的 112 一致 |

> 口径提醒：**"公共类"≠"控件"**。例如 `Arc.Avalonia.AdvancedInputs` 36 个公共类里只有 12 个是控件，
> 其余 24 个是选项/事件参数/模型（`ArcBillingToggleOption`、`ArcShortcutChangedEventArgs`…）。
> 若按 `grep "public.*class"` 粗算会高估控件数，本文以继承链判定为准。

### 3.2 项目 → 类数量 → 代表类名

| 项目 | 职责 | `.cs` | Controls `.cs` | 公共类 | **控件类** | internal | 页面（独立/共享） | `.axaml` | 代表控件类（节选） |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- |
| `Arc.Avalonia`（Core） | token/主题/动效/图标 + 5 个基础控件 | 11 | 5 | 13 | **5** | 0 | 0 / 0 | 4 | `ArcButton` `ArcCard` `ArcBadge` `ArcIcon` `ArcSpinner` |
| `Arc.Avalonia.Demo` | `ArcPage` 契约、catalog 加载、文档布局 | 7 | 0 | 10 | 0 | 0 | 0 / 0（2 个抽象基类文件） | 0 | `ArcCatalog` `ArcItemPage` `ArcPageRegistry` `ArcDocLayout` `DemoUi` |
| `Arc.Avalonia.Inputs` | 文本/选择/开关/滑杆 | 28 | 23 | 30 | **20** | 3 | 0 / 4 | 10 | `ArcInput` `ArcTextarea` `ArcSelect` `ArcCombobox` `ArcMultiSelect` `ArcSwitch` `ArcSlider` `ArcSegmentedControl` |
| `Arc.Avalonia.Actions` | 按钮组/菜单/导航/浮层/消息 | 34 | 25 | 42 | **24** | 6 | 0 / 8 | 8 | `ArcDialog` `ArcPopover` `ArcToastHost` `ArcDropdownMenu` `ArcTabs` `ArcAccordion` `ArcShareAccess` `ArcMegaMenu` |
| `Arc.Avalonia.Feedback` | 反馈/进度/头像/表格/活动 | 38 | 22 | 50 | **26** | 2 | 0 / 11 | 22 | `ArcDataGrid` `ArcSortableDataTable` `ArcTreeTable` `ArcTreeView` `ArcUsageMeter` `ArcTimeline` `ArcCommentThread` `ArcProgress` |
| `Arc.Avalonia.Charts` | 自绘图表（Arc 系列色板） | 30 | 22 | 23 | **22** | 7 | 0 / 5 | 1 | `ArcLineChart` `ArcBarChart` `ArcBrushChart` `ArcSunburstChart` `ArcSankeyFlow` `ArcRealtimeStream` `ArcActivityHeatmap` `ArcChartBase` |
| `Arc.Avalonia.Pickers` | 日历/日期/时间/颜色/文件/签名 | 26 | 19 | 28 | **17** | 2 | 0 / 6 | 14 | `ArcCalendar` `ArcDatePicker` `ArcDateRangePicker` `ArcTimePicker` `ArcColorPicker` `ArcEmojiPicker` `ArcFileDropzone` `ArcSignaturePad` |
| `Arc.Avalonia.AdvancedInputs` | 行内编辑/特殊输入/高级选择 | 20 | 14 | 36 | **12** | 5 | 0 / 5 | 11 | `ArcInlineEdit` `ArcScrubInput` `ArcPhoneInput` `ArcMentionInput` `ArcShortcutRecorder` `ArcMorphSelect` `ArcElasticSlider` `ArcRadioCards` |
| `Arc.Avalonia.AdvancedActions` | 高级按钮族 | 8 | 6 | 9 | **6** | 2 | 0 / 2 | 6 | `ArcActionButton` `ArcFloatingButtonGroup` `ArcExpandingButtonGroup` `ArcIconMorph` `ArcUserMenu` |
| `Arc.Avalonia.Effects` | 文字动效/通告/浮层特效 | 14 | 11 | 20 | **9** | 5 | 0 / 2 | 10 | `ArcTextReveal` `ArcInViewTitle` `ArcTextMorph` `ArcTextShimmer` `ArcAnnouncementBar` `ArcCookieConsent` `ArcBottomSheet` |
| `Arc.Avalonia.Content` | 富文本/排序列表/聊天/清单 | 8 | 6 | 9 | **4** | 4 | 0 / 2 | 5 | `ArcRichTextEditor` `ArcReorderableList` `ArcChatThread` `ArcOnboardingChecklist` |
| `Arc.Avalonia.Media` | 手势/媒体布局 | 14 | 10 | 25 | **15** | 3 | 0 / 3 | 9 | `ArcHoldToConfirm` `ArcSwipeActions` `ArcReactions` `ArcImageCompare` `ArcCarousel` `ArcCardStack` `ArcScrollArea` `ArcResizablePanels` |
| `Arc.Avalonia.Special.Glass` | 玻璃与表面 | 28 | 17 | 35 | **14** | 2 | 11 / 0 | 1 | `ArcGlassMenu` `ArcGlassTabBar` `ArcGlassSegmented` `ArcHoloCard` `ArcWalletStack` `ArcControlCenter` `ArcShareSheet` |
| `Arc.Avalonia.Special.Motion` | 动效与交互 | 24 | 12 | 12 | **12** | 2 | 11 / 0 | 1 | `ArcActionMorph` `ArcMorphNav` `ArcDock` `ArcLiquidTabBar` `ArcOrbitMenu` `ArcSlotText` `ArcTypewriterTerminal` |
| `Arc.Avalonia.Special.Media` | 媒体与输入 | 25 | 14 | 12 | **12** | 3 | 11 / 0 | 1 | `ArcProductGallery` `ArcLightboxGallery` `ArcCoverFlow` `ArcVoiceOrb` `ArcVoiceRecorder` `ArcNowPlaying` `ArcPageCurl` |
| `Arc.Avalonia.Special.AIWidgets` | AI 运行时组件 | 23 | 12 | 32 | **18** | 9 | 11 / 0 | 1 | `ArcAgentStatus` `ArcToolCall` `ArcAgentPlan` `ArcAgentTimeline` `ArcContextWindow` `ArcActivityRings` `ArcSkeletonMorph` |
| `Arc.Avalonia.Special.AIExplainers` | AI 原理示意 | 24 | 13 | 12 | **12** | 1 | 11 / 0 | 1 | `ArcAgentLoop` `ArcDataFlow` `ArcPaymentFlow` `ArcVectorSearch` `ArcReasoningTree` `ArcPromptCache` `ArcDateReel` |
| `Arc.Avalonia.Blocks.Marketing` | 营销/站点区块 | 23 | 5 | 4 | **4** | 1 | 18 / 0 | 1 | `ArcMarquee` `ArcMeshWash` `ArcSpotlightCard` `ArcStatNumber` |
| `Arc.Avalonia.Blocks.Commerce` | 电商/计费区块 | 20 | 2 | 1 | **1** | 2 | 18 / 0 | 1 | `ArcEmptyScene` |
| `Arc.Avalonia.Blocks.App` | 应用壳层/设置 | 23 | 2 | 0 | **0** | 3 | 21 / 0 | 1 | （无公共控件；`AppKit`/`OtpField` 均为 internal） |
| `Arc.Avalonia.Blocks.Team` | 团队协作 | 20 | 4 | 3 | **2** | 2 | 16 / 0 | 1 | `ArcCohortChart` `ArcMorphIllustration` |
| `Arc.Avalonia.Blocks.Data` | 数据可视化区块 | 38 | 18 | 0 | **0** | 33 | 20 / 0 | 1 | （16 个 internal 图表 stage，如 `ArcCandleChart`/`ArcDotGlobe`/`ArcHorizonChart`） |
| `Arc.Avalonia.Blocks.AI` | AI 产品界面 | 19 | 1 | 0 | **0** | 2 | 18 / 0 | 1 | （无公共控件；`AiKit`/`ModelChip` 为 internal） |
| `Arc.Avalonia.Gallery` | 展示应用 + 截图/审计 | 11 | 0 | 5 | **1** | 1 | 0 / 5 | 1 | `GalleryWindow`（另 `CatalogAudit`/`ShotRunner`/`NavigationSmoke` 为工具类） |
| **合计** | **24 工程** | **516** | **263** | **411**（407 唯一名） | **236**（234 唯一名） | **100** | **166 / 53 = 219** | **112** | — |

### 3.3 解读与注意点

1. **公共控件集中在特征层工程**：10 个组件特征工程（Inputs/Actions/Feedback/Charts/Pickers/AdvancedInputs/AdvancedActions/Effects/Content/Media）
   合计 **155** 个控件类，加上 Core 的 5 个基础控件共 **160** 个通用控件；Special 五项目合计 **68** 个（以自绘/动效为主）；
   Blocks 六项目合计仅 **7** 个公共控件、Gallery 1 个（`GalleryWindow`）——**Blocks 是页面产物，不是可复用控件**，
   扩展生活工具时应复用组件 + 参考 block 的组合方式，而不是二次封装 block。
2. **同名类跨项目重复**：`ArcActivityHeatmap` 与 `ArcAnimatedCounter` 在 `Arc.Avalonia.Charts` 与 `Arc.Avalonia.Feedback`
   各有一份实现（两条独立代码路径）。Charts 版被 `src/Arc.Avalonia.Charts/Pages/ChartsMorePage.cs` 使用；
   Feedback 版被 `src/Arc.Avalonia.Feedback/Pages/DataDemos.cs`、`FeedbackDemos.cs` 使用（对应 Explorer / Time 共享页）。
   扩展时需明确用哪个命名空间，避免重复维护。
3. **Core 只有 5 个控件**（`ArcButton`/`ArcCard`/`ArcBadge`/`ArcIcon`/`ArcSpinner`），但它是唯一提供 token、
   161 个 Lucide 图标、弹簧动效与主题切换的层（`src/Arc.Avalonia/Theme/ArcPalette.cs`、`src/Arc.Avalonia/Motion/ArcMotion.cs`、
   `src/Arc.Avalonia/Icons/ArcIconData.cs`）。生活工具的所有新控件都必须建在这一层上。
4. **与 `docs/04` 的谱系统计互证**：该文档给出 `TemplatedControl` 76、裸 `Control` 33、`Panel` 24、`ContentControl` 14、
   `Button` 系 12、`ArcChartBase` 20、Special 基类 33——说明大多数控件走 **ControlTheme 模板**，
   自绘集中在图表与 Special；这也意味着"控件可换肤、可访问性由模板保证"是既有优势。
5. **没有的东西同样是统计**：`src` 内 **0 处** `TrayIcon`/`WindowNotificationManager`、**0 处** `HttpClient/WebSocket`、
   `File.*` 写入仅截图与签名导出 2 处（`src/Arc.Avalonia.Gallery/Framework/ShotRunner.cs`、
   `src/Arc.Avalonia.Pickers/Controls/ArcSignaturePad.cs`）；`Schedul/Recurr/Reminder` 命中 30 处全部是
   `src/Arc.Avalonia.Blocks.AI/Pages/AgentRoutinesPage.cs` 等页面的示例文案。这些空白决定了生活工具必须自带"非 UI 能力"。

---

## 4. 关键能力深读（25 组 / 36 个 catalog 项，与生活工具最相关）

> 每节统一四段：**证据**（源码/页面相对路径）/ **已支持的行为与状态** / **数据形态** / **作为生活工具积木的缺口**。
> "缺口"只描述现状事实（缺什么能力），不提方案。

### 4.1 `calendar` — 月历（Pickers）

- **证据**：`src/Arc.Avalonia.Pickers/Controls/ArcCalendar.cs`；`src/Arc.Avalonia.Pickers/Pages/PickersPage.cs`；`src/Arc.Avalonia.Pickers/Themes/ArcCalendar.axaml`。
- **支持**：`TemplatedControl`；属性 `SelectedDate`、`RangeStart`、`RangeEnd`（范围仅高亮渲染）、`DisplayMonth`、`MinDate`、`MaxDate`、
  `DisabledDates`（`Func<DateTime,bool>` 回调）、`ShowToday`、`WeekStartsOn`；事件 `SelectedDateChanged`、`DisplayMonthChanged`；
  单一 Tab 停靠点，方向键/Home/End/PageUp/PageDown 移动日期游标，月份滑动与选择胶囊为动效。
- **数据形态**：`DateTime?` 标量 + 禁用日期谓词；**没有任何"事件/日程"集合**，不承载标题、时间、地点、参与者。
- **生活工具缺口**：无"事件模型"（日程项/提醒/重复规则/参与人）；无多事件渲染（日期下圆点/计数）；无时区与全天语义；
  无数据源与持久化；无"今天+相对日期"导航 API；范围高亮是展示属性而非业务区间选择器。

### 4.2 `date-picker` / `date-range-picker` — 日期与区间选择（Pickers）

- **证据**：`src/Arc.Avalonia.Pickers/Controls/ArcDatePicker.cs`、`ArcDateRangePicker.cs`；`src/Arc.Avalonia.Pickers/Pages/PickersPage.cs`、`RangesAndTimePage.cs`。
- **支持**：`ArcDatePicker`：`SelectedDate`/`MinDate`/`MaxDate`/`DisabledDates`/`ShowToday`/`Format` + `SelectedDateChanged`；
  `ArcDateRangePicker`：**`ArcDateRange`（含 `Days` 计算）**、**`ArcDateRangePreset`（`Defaults` = Today / Last 7 days / Last 30 days / This month）**、
  可注入 `Presets`、`Months`（`ArcRangeMonths` 单/双月）、`RangeChanged`；触发器形变为双月面板，预设左栏。
- **数据形态**：`DateTime?` 与 `ArcDateRange(Start,End)` 值对象；预设是 `Func<DateTime, ArcDateRange>`，**无到期日/重复周期概念**。
- **生活工具缺口**：预设是"向后看"的报表式区间，缺"未来 N 天/本周/下周/工作日"等生活语义；无时区；
  无"区间→事件查询"衔接；无自然语言日期输入（`scrub-input` 只处理数字）。

### 4.3 `time-picker` — 时间选择（Pickers）

- **证据**：`src/Arc.Avalonia.Pickers/Controls/ArcTimePicker.cs`；`src/Arc.Avalonia.Pickers/Pages/RangesAndTimePage.cs`。
- **支持**：值 `TimeSpan?`（默认 09:00）、`MinuteStep`（默认 15）、`Format`（`ArcTimeFormat.TwelveHour/TwentyFourHour`）；
  弹出按步长居中的时间列表，方向键循环、Enter 选中、Esc 关闭、`TimeChanged` 事件。
- **数据形态**：一天内的 `TimeSpan?`；无日期、无时区、无时长。
- **生活工具缺口**：无"日期+时间"组合值类型（需与 date-picker 手工拼）；无时区/DST；无时长选择器
  （SpecialGlass 有公共 `ArcDurationDial`，但属于玻璃视觉组件，未与时间字段联动）；无重复提醒时间列表（如"每天 8:00/20:00 两次"）。

### 4.4 `week-calendar` — 周历（BlocksTeam）

- **证据**：`src/Arc.Avalonia.Blocks.Team/Pages/WeekCalendarPage.cs`。
- **支持**：7 天 × 8 小时网格、全天行、彩色日历、事件选中与详情、周切换/今天、周视图↔议程视图切换；
  页内 `private sealed record CalEvent(...)` 与 `static readonly IReadOnlyList<CalEvent> Events` 种子数据；
  Implementation notes 明确：**拖拽创建/移动/缩放未复刻**，改用事件选择 + 显式周切换保持键盘可用。
- **数据形态**：页面私有 record + 静态示例，固定在"2026 年 11 月 9 日"那一周；其他周显示空态。
- **生活工具缺口**：**没有可复用的日历控件**（逻辑全在页面里）；无事件 CRUD API、无重复规则、无提醒/通知联动、
  无时区、无多日历源、无"新建事件"表单；数据不持久化；无法承载"每天的用药/接送/会议"这类真实日程。

### 4.5 `notification-center` — 通知中心（BlocksApp）

- **证据**：`src/Arc.Avalonia.Blocks.App/Pages/NotificationCenterPage.cs`；相邻能力 `src/Arc.Avalonia.Actions/Controls/ArcToast.cs`、`ArcToastHost.cs`。
- **支持**：铃铛触发器 + 未读徽标（数字滚动）、All/Unread 分段、行展开、`Mark read/unread`、`Dismiss`、
  `Mark all read`、`Clear read`、空态"你已追平"、`ToolTip` 带未读数；页内 `record Update(Id, Title, Description, Time, Tone, Read, Actor)`；
  toast 为窗口内浮层容器（`ArcToastHost.Show`），到期自动消失、hover 不暂停、每个 toast 至多一个 action。
- **数据形态**：内存 `List<Update>`，`Read` 布尔本地翻转；`Time` 是展示字符串（"2m"/"1h"），不是时间戳。
- **生活工具缺口**：**无通知投递通道**（全仓 0 处 `WindowNotificationManager`/`TrayIcon`，见 §3.3-5）——不能弹出系统通知、
  不能托盘常驻；无优先级/分组/静默时段/免打扰；无"按天分组/未读跨会话持久化"；无深链（点通知跳到事项）；
  无服务端推送/定时触发；`Time` 字符串无法排序或触发"到期提醒"。

### 4.6 `inbox-triage` — 收件箱分诊（BlocksApp）

- **证据**：`src/Arc.Avalonia.Blocks.App/Pages/InboxTriagePage.cs`。
- **支持**：Inbox/Snoozed/Archive 三个文件夹（Tab 列表 + 方向键）、行展开、Archive/Snooze/Restore + 字母快捷键、
  单步撤销、状态区播报；`record Mail(Id, Sender, Subject, Preview, Body, Time, Unread)` 种子数据。
- **数据形态**：文件夹是页内 `List<Mail>` 字典，动作即本地移除/加入；`Time` 为展示字符串；snooze 时间不可编辑。
- **生活工具缺口**：可作"每日待处理队列"范式（晨间简报/账单/包裹的 triage），但缺：真实"稍后到某时刻自动回到 Inbox"
  （无调度）；无批量选择；无分类/标签；无与提醒/日历的联动；无持久化；无来源（邮件/短信/IM）接入。

### 4.7 `command-palette` — 命令面板（BlocksApp）

- **证据**：`src/Arc.Avalonia.Blocks.App/Pages/CommandPalettePage.cs`；同族 `src/Arc.Avalonia.Blocks.App/Pages/SettingsCommandPage.cs`、`src/Arc.Avalonia.Blocks.App/Pages/SearchResultsPage.cs`。
- **支持**：全键盘驱动的应用级动作面板：搜索过滤、分组结果、快捷键提示、单一滑动高亮；页内
  `record Command(Id, Label, Description, Group, Shortcut, Icon, Keywords)`；`SettingsCommandPage` 展示"搜索框长成设置面板"的变体。
- **数据形态**：命令是内存列表，`handler` 仅关闭面板并在状态区播报，**不执行真实路由/动作**。
- **生活工具缺口**：天然适合"用一句话调起生活动作"（记一笔、加提醒、开始专注），但缺：命令注册/执行框架
  （无命令总线、无参数化命令、无权限）、无异步/流式命令（如"帮我规划今天"）、无最近使用/学习排序、
  无全局快捷键注册（`ShortcutRecorder` 只录制不注册）。

### 4.8 `agent-status` — Agent 运行状态（SpecialAIWidgets）

- **证据**：`src/Arc.Avalonia.Special.AIWidgets/Controls/ArcAgentStatus.cs`；`Pages/AgentStatusPage.cs`。
- **支持**：`ArcAgentStatus : Panel`；**状态枚举 `ArcAgentRunStatus` = Idle / Working / NeedsInput / Paused / Done / Failed / Stopped**；
  **步骤枚举 `ArcAgentStepState` = Pending / Active / Waiting / Done / Failed / Skipped**；属性 `AgentName`、`ActivityLabel/Icon/Count`、
  `ElapsedSeconds`、`Steps`、`IsExpanded`；`ArcAgentStep` 含 `Id/Label/Icon/State/DurationSeconds`；
  展开后见步骤与进度线；计时器仅在 Working 走动。
- **数据形态**：控件属性直接吃 `IEnumerable<ArcAgentStep>`，**没有事件/订阅接口**；页面用按钮推进演示状态。
- **生活工具缺口**：作为"后台生活 Agent 的状态胶囊"视觉完备，但缺：运行时绑定（无 `IObservable`/命令/取消/暂停 API）、
  **NeedsInput 不会主动触达**（无系统通知/托盘闪烁）、无 token/成本字段（`agent-timeline` 有，status 没有）、
  无跨会话的运行记录、无"任务名称/生活域标签"。

### 4.9 `tool-call` — 工具调用行（SpecialAIWidgets）

- **证据**：`src/Arc.Avalonia.Special.AIWidgets/Controls/ArcToolCall.cs`；`Pages/ToolCallPage.cs`。
- **支持**：`ArcToolCall : Panel`；**状态 `ArcToolCallStatus` = Running / Success / Error / Approval / Denied**；
  `ArcToolCallData` 含 `Id`、`Tool`（默认 `"web"`）、`Title`、`RunningTitle`、`ApprovalTitle`、`Status`、
  `Input`（`ArcToolArgument{Key,Value}` 列表）、`Output`（`ArcToolListItem{Lead,Title,Tag}` 列表）、`Summary`、`Message`、`DurationSeconds`；
  控件属性 `Calls`、`ShowHeader`、`IsOpen`、`DefaultOpenId`；行可展开看输入/输出，复制按钮原位确认。
- **数据形态**：`IEnumerable<ArcToolCallData>` 只读集合；审批/重试/拒绝是**本地模拟**（Implementation notes：
  "approval, retry and deny are simulated locally because the demo has no agent loop behind it"）。
- **生活工具缺口**：适合展示"Agent 查天气/下单/发消息"的调用记录，但缺：真实审批回调与超时、参数可编辑
  （`agent-approvals` 同样只是只读行）、工具结果的结构化渲染协议（只支持 Key/Value 与列表）、
  失败重试策略、调用与提醒/通知的联动、成本计量。

### 4.10 `agent-plan` — 计划与清单（SpecialAIWidgets）

- **证据**：`src/Arc.Avalonia.Special.AIWidgets/Controls/ArcAgentPlan.cs`；`Pages/AgentPlanPage.cs`。
- **支持**：`ArcAgentPlan : Panel`；**计划状态 `ArcAgentPlanStatus` = Draft / Running / Paused / Done**；
  **步骤状态 `ArcAgentPlanStepState` = Pending / Active / Done / Failed / Skipped**；
  `ArcAgentPlanStep{Id,Title,State,SubSteps,Note,Error,DurationSeconds}`；属性 `Steps`、`Status`、`Goal`、`ModelName`、`Summary`；
  草稿态可改名/增删/排序（页面实现），运行态变实时清单；`agent-plan` 页并排展示"可编辑草稿 vs 运行清单"。
- **数据形态**：页面局部可变列表；`Keep planning` 追加硬编码建议，运行用固定时钟推进（Implementation notes 明示）。
- **生活工具缺口**：天然映射"今天的计划/待办/购物清单"，但缺：**持久化与跨天延续**、重复/到期/优先级字段、
  拖拽排序复用到真实列表（页面内实现未抽取成控件）、计划→提醒/日历的落地、"Paused 后如何恢复"的运行时语义。

### 4.11 `agent-timeline` — 多代理时间轴（SpecialAIWidgets）

- **证据**：`src/Arc.Avalonia.Special.AIWidgets/Controls/ArcAgentTimeline.cs`；`Pages/AgentTimelinePage.cs`；绘图助手 `Controls` 内 `ArcTimelinePlot`（internal）。
- **支持**：`ArcAgentTimeline : Panel`；模型 **`ArcTimelineAgent{Id,Name,Model,PricePerMillion}`**、
  **`ArcTimelineSpan{Id,AgentId,Kind,Start,End,Label,Tool,Tokens,Failed,Retry}`**、
  **`ArcTimelineHandoff{From,To,At,Label}`**；属性 `Title`、`Agents`、`Spans`、`Handoffs`、`Duration`、`IsPlaying`；
  play/restart/速度控制回放，点击 span 固定详情；表头统计时间/token/成本（页内）。
- **数据形态**：三个只读集合 + `Duration`，行与 span 由代码绘制并命中测试；单 Tab 停靠 + 方向键（非 roving tabindex）。
- **生活工具缺口**：可用于"一天做了什么/Agent 何时介入"的回放，但缺：真实事件流录制/持久化、时间轴缩放与筛选、
  与日历/时间线的互转、"等待用户"的时长统计入口（span 有 Kind，但没有聚合报表）。

### 4.12 `context-window` — 上下文占用（SpecialAIWidgets）

- **证据**：`src/Arc.Avalonia.Special.AIWidgets/Controls/ArcContextWindow.cs`；`Pages/ContextWindowPage.cs`。
- **支持**：`ArcContextWindow : Panel`；**分类 `ArcContextCategory` = System / Tools / Files / Summary / Messages**；
  `ArcContextItem{Id,Category,Label,Tokens}`、`ArcContextModel{Id,Name,Window,Reserve}`；属性 `Items`、`Models`、`ModelId`、`Threshold`；
  200 格网格一次绘制，发送消息/附件/压缩时展示"还剩多少"，阈值行标记压缩起点。
- **数据形态**：token 计数为示例数字；网格是绘制结果（非 200 个节点），填充波为单次脉冲。
- **生活工具缺口**：这是"Agent 记忆预算"的透明化范式，但缺：真实 tokenizer/计量、与 `agent-memory` 的联动
  （记忆项不会落到这里）、用户可删除单项上下文（只有整体 compaction）、本地模型上下文窗口探测。

### 4.13 `agent-memory` — 助手记忆（BlocksAI）

- **证据**：`src/Arc.Avalonia.Blocks.AI/Pages/AgentMemoryPage.cs`。
- **支持**：`record Memory(Id, Text, Group, Source)`；分组 All/About/Preferences/Projects；筛选、搜索、就地编辑、
  `Forget`（立即删除，无 5 秒撤销——Implementation notes）、`Capture` 药丸塞入一条示例记忆、暂停记忆开关；页内状态文案"3 memories, 62% of the budget"。
- **数据形态**：内存 `List<Memory>`；`Source` 是展示字符串（"From a chat · 4 Oct"）；预算百分比是硬编码文案。
- **生活工具缺口**：**这是"个人生活偏好/事实"的唯一现成容器**，但缺：持久化与加密、记忆类型与过期/遗忘策略、
  来源可追溯（没有消息 id）、与 `context-window`/`ai-chat` 的真实读写链路、用户可见的"记忆预算"实算、批量导出/删除（隐私要求）。

### 4.14 `agent-tasks` — 后台任务队列（BlocksAI）

- **证据**：`src/Arc.Avalonia.Blocks.AI/Pages/AgentTasksPage.cs`。
- **支持**：`record Task(Title, Repo, Branch, Model, State, Step, Progress, Meta, CanOpen)`；过滤 All/Running/Ready/Merged、
  启动任务插入排队卡、卡片进度与状态色、完成后 `Create pull request`；Implementation notes：任务/diff/check 均为示例，
  Start 插入示例任务，PR 号是编造的。
- **数据形态**：内存列表 + 展示字符串（`Meta` "Started 2m ago"）；`Progress` 是 int 百分比。
- **生活工具缺口**：可作"Agent 替你办事"的任务队列 UI，但缺：任务持久化与重试、依赖/优先级/截止时间、
  进度事件推送（当前静态 int）、取消/暂停/重排、任务结果落地为文档或提醒、失败通知。

### 4.15 `agent-approvals` — 审批（BlocksAI）

- **证据**：`src/Arc.Avalonia.Blocks.AI/Pages/AgentApprovalsPage.cs`。
- **支持**：`record Request(Id, Agent, Model, Tool, Action, Title, Summary, Reason, Risk, string[] Args)`；
  一次一个审批焦点：工具标记、Agent 理由、风险等级（High/Medium/Low）、参数行、批准/拒绝/总是允许；
  决策后移出队列并追加到 resolved 日志。Implementation notes：**"nothing reaches an agent"**，参数为只读行，
  Always allow 只加规则芯片、不匹配历史请求，键盘快捷键仅有按钮接通。
- **数据形态**：`Request[]` 队列 + 解析日志 `List<string>`；参数是 `string[]` 的 "key: value" 文本。
- **生活工具缺口**：生活 Agent 的"授权 UI"骨架齐全（如"允许自动下单/发消息/改日程"），但缺：**真实授权执行链**、
  权限范围与时效（一次性/本会话/永久）、敏感操作二次确认（`hold-to-confirm` 可组合）、审批超时与默认动作、
  审计日志持久化（合规必需）。

### 4.16 `agent-routines` — 定时代理（BlocksAI）

- **证据**：`src/Arc.Avalonia.Blocks.AI/Pages/AgentRoutinesPage.cs`。
- **支持**：`class Routine{Name, Instruction, Schedule, Countdown, Sources[], Destination, Model, Enabled, LastRun, Ticks[], Report}`；
  每行"句式化日程"（如 `Weekdays at 09:00`）、下次运行倒计时、连接器、运行 tick 历史、就地展开编辑/立即运行；
  示例含"Morning triage/Weekly digest/Stale PR nudge/Incident summary"。
- **数据形态**：内存对象；`Schedule`/`Countdown` 是**展示字符串**；`Run now` 立即完成并加一个 tick。
- **生活工具缺口**：**这是与"提醒/重复日程"最接近的现成范式**，但缺：调度引擎（cron/RRULE/自然语言→规则）、
  真实倒计时计算与时区、失败重试/补偿、通知投递（"Destination" 无实际通道）、提醒型 routine（当前均为日报型）、
  与日历/任务的数据互转。

### 4.17 `agent-orchestrator` / `agent-run` — 编排与单次运行（BlocksAI）

- **证据**：`src/Arc.Avalonia.Blocks.AI/Pages/AgentOrchestratorPage.cs`、`AgentRunPage.cs`；相关控件 `agent-timeline`/`agent-plan`/`tool-call`。
- **支持**：orchestrator：`record Lane(Model, Subtask, Tool, Output, Tokens, Time, …)`，主代理计划 + 三条并行工作泳道 + 汇总；
  agent-run：步骤流式、工具就地展开、停在双文件 diff 的审批门（Approve/Reject），批准后写入示例改动并打开结果。
- **数据形态**：静态示例数据；`Tokens` 是字符串；diff 用着色 token 而非语法高亮器；PR 号为编造值。
- **生活工具缺口**：这是"Agent 自主+人类在环"的完整叙事模板，但缺：真实运行状态机接入（`agent-status` 有枚举但无事件源）、
  并行任务的资源/成本配额、拒绝后的回流与重试、运行产物（文档/账单/日程）落地、跨运行历史索引。

### 4.18 `kanban-board` / `project-board` — 任务看板（BlocksTeam）

- **证据**：`src/Arc.Avalonia.Blocks.Team/Pages/KanbanBoardPage.cs`、`ProjectBoardPage.cs`。
- **支持**：三列 `To do / Doing / Done` + `Limits = { -1, 2, -1 }`（Doing 限 2）；泳道、实时 WIP 校验（超限有文本+颜色）、
  快速新增、详情条；Implementation notes：**指针拖拽抬卡/倾斜/自动滚动未复刻**，改用带标签的左右箭头移动，保持键盘可用。
- **数据形态**：页内示例卡与人名（内存）；列/限值均为静态数组。
- **生活工具缺口**：可直接做"家务/维修/待办"看板，但缺：持久化、截止日/优先级/负责人字段、重复任务、
  子任务与检查清单联动（`onboarding-checklist`/`agent-plan` 各自独立）、提醒触达、拖拽（键盘替代已实现）。

### 4.19 `chat-thread` / `ai-chat` / `ai-composer` — 对话表面（Content / BlocksAI）

- **证据**：`src/Arc.Avalonia.Content/Controls/ArcChatThread.cs`；`src/Arc.Avalonia.Blocks.AI/Pages/AiChatPage.cs`、`AiComposerPage.cs`。
- **支持**：`ArcChatThread : TemplatedControl`；**`ArcChatMessageStatus` = Sending / Sent / Delivered / Read / Failed**；
  `ArcChatParticipant{Name,Avatar}`、`ArcChatAttachment{Name,Size,Url,IsImage}`、
  `ArcChatMessage{Id,AuthorId,Text,CreatedAt,Attachments,Status}`、`ArcChatDraft{Text,Attachments}`；
  属性 `Participants`、`CurrentUserId`、`Messages`、`Typing`、`Placeholder`、`ShowComposer`、`AllowAttachments`、`GroupWindow`；事件 `Send`。
  ai-chat：markdown 子集渲染、可折叠推理、工具芯片、引用来源、切换模型/评分/展开；ai-composer：附件芯片、Web 搜索/扩展思考开关、模型芯片、发送。
- **数据形态**：消息集合由调用方传入；AI 页用脚本化回复追加（"Nothing is sent to a model"）；附件无真实文件流。
- **生活工具缺口**：是"日常对话式入口"的最佳起点（记录、问账单、让 Agent 计划一天），但缺：
  多会话存储与检索、消息持久化、真实流式与打断（文档有描述但未实现）、语音输入（`voice-orb` 不连通）、
  消息加密与隐私控制、通知/未读跨页面、附件落盘（仅有 `file-dropzone` UI）。

### 4.20 `settings-page` — 设置页（BlocksApp）

- **证据**：`src/Arc.Avalonia.Blocks.App/Pages/SettingsPage.cs`；相邻 `SettingsCommandPage.cs`、`SecuritySettingsPage.cs`、`src/Arc.Avalonia.Blocks.Team/Pages/EnvVariablesPage.cs`。
- **支持**：`record Section(Id, Label, Icon, Description)`；滑动分节导航、编辑即出现的保存条（save/discard）、
  账户字段与账单摘要、照片上传；Implementation notes：保存/丢弃/上传只改本地状态，未做服务端校验。
- **数据形态**：页内示例值 + 局部布尔 dirty 状态；无配置 schema。
- **生活工具缺口**：生活工具需要"我的偏好/家庭/隐私/通知/连接器"等设置，但缺：偏好持久化与版本迁移、
  设置项 schema 化（便于 Agent 读写）、按作用域（个人/家庭/设备）覆盖、导入导出与重置、权限门控。

### 4.21 `usage-meter` / `usage-billing` — 配额与账单（Feedback / BlocksCommerce）

- **证据**：`src/Arc.Avalonia.Feedback/Controls/ArcUsageMeter.cs`；`src/Arc.Avalonia.Blocks.Commerce/Pages/UsageBillingPage.cs`。
- **支持**：`ArcUsageMeter : TemplatedControl`；`ArcUsageSegment{Id,Label,Value}`；属性 `Label`、`Segments`、`Limit`、`Unit`、
  `Decimals`、`FreeLabel`、`OverLabel`、**`WarnAt`**（视觉告警阈值）；usage-billing 页：期间花费、7 日均值预测线、
  限额计量、告警规则、发票历史、升级确认（示例数据，预测为虚线而非参考实现的阴影锥）。
- **数据形态**：段值集合 + 限额 double；历史/发票为页内示例数组；无账本/流水模型。
- **生活工具缺口**：适合"本月预算/订阅额度/水电用量"的仪表，但缺：真实账本（账户/分类/交易录入/对账）、
  阈值触达（`WarnAt` 只改颜色，不发通知）、周期切换与历史持久化、货币/税率/时区、多成员共享预算。

### 4.22 `activity-heatmap` / `activity-rings` / `activity-terrain` — 活动可视化（Charts / SpecialAIWidgets / BlocksData）

- **证据**：`src/Arc.Avalonia.Charts/Controls/ArcActivityHeatmap.cs`（另有同名 `src/Arc.Avalonia.Feedback/Controls/ArcActivityHeatmap.cs`）；
  `src/Arc.Avalonia.Special.AIWidgets/Pages/ActivityRingsPage.cs`；`src/Arc.Avalonia.Blocks.Data/Pages/ActivityTerrainPage.cs`。
- **支持**：heatmap 属性 `Values`（`IReadOnlyList<int>`）、`StartDate`、`Unit`，一年一日的方格；
  activity-rings：每日目标的 tick 环，超 100% 走第二圈；activity-terrain：周 × 小时 3D 等距地形（`DrawingContext` 绘制、Shift+方向键环绕、可压平成热图）。
- **数据形态**：纯数值数组 + 起始日期/单位；rings/terrain 为页内种子数据；**没有任何"习惯/目标/打卡"实体**。
- **生活工具缺口**：这是"习惯追踪"最接近的视觉资产，但缺：习惯定义（名称、目标、频率、提醒时间）、
  打卡写入与持久化（无数据录入控件）、streak/连续天数语义、目标达成通知、跨年/时区/补卡。

### 4.23 `data-grid` / `sortable-data-table` / `tree-table` — 数据表（Feedback）

- **证据**：`src/Arc.Avalonia.Feedback/Controls/ArcDataGrid.cs`、`ArcSortableDataTable.cs`、`ArcTreeTable.cs`；`src/Arc.Avalonia.Feedback/Pages/DataGridPage.cs`。
- **支持**：`ArcDataGrid`：`ArcGridColumn{Key,Header,Width,MinWidth,Align,Sortable,Resizable,Editable,Hidden,ShowTotal,Format,ValueGetter}`、
  `ArcGridRow{Id,Values}`、`ArcGridSort`；属性 `Columns/Rows/Label/DefaultSort/SelectedIds/Selectable/Zebra/MaxBodyHeight/EmptyMessage/EmptyDescription/ShowTotals/Editable`；
  事件 `SortChanged`、`SelectionChanged`、`CellEdited`；吸顶表头、列宽拖动、多选与 Shift 区间、单元格键盘导航、行内编辑、汇总行、空态。
- **数据形态**：行是 `Dictionary<string, object?>`，编辑事件回传 `ArcGridCellEdit{Row,Column,OldValue,NewValue}`；页内数据为示例。
- **生活工具缺口**：可用于"账单流水/药品清单/家庭资产"表格，但缺：**虚拟化与增量更新**（`DataGridPage` 自述
  "leaves out clipboard ranges, the fill handle, grouping, export, and virtualisation"；`docs/04` G-P0-5 指出
  100×8 ≈ 800 控件、选择/排序整表重建）、导出、日期/货币专用列类型、行分组、持久化。

### 4.24 `voice-orb` / `voice-recorder` — 语音（SpecialMedia）

- **证据**：`src/Arc.Avalonia.Special.Media/Controls/ArcVoiceOrb.cs`、`ArcVoiceRecorder.cs`；`Pages/VoiceOrbPage.cs`、`VoiceRecorderPage.cs`。
- **支持**：orb：`ArcVoiceOrbState` = Idle / Listening / Thinking / Speaking，转写文本、通话控制（开始/静音/挂断/时钟），
  键盘 Space/Enter/M/Escape；recorder：录制态实时波形、回听拖动、发送进线程；两者均声明为**模拟**。
- **数据形态**：orb 的麦克风电平是模拟值；recorder 的峰值来自确定性噪声函数（保证截图稳定），
  "真实采集会把 MediaRecorder 数据推进同一发送路径"仅为注释。
- **生活工具缺口**：**语音是生活工具的核心入口，但当前只有 UI 外壳**：全仓 0 处音频采集/播放/ASR/TTS
  （无 `SpeechSynthesizer`/`Wasapi`/音频库引用），也没有"按住说话→转写→意图→执行"的链路；无权限提示。

### 4.25 `control-center` — 快捷设置/专注（SpecialGlass）

- **证据**：`src/Arc.Avalonia.Special.Glass/Controls/ArcControlCenter.cs`；`Pages/ControlCenterPage.cs`。
- **支持**：`ArcFocusMode{Id,Label,Icon,Description,DefaultMinutes}`、`ArcChannelOption{Id,Label,Description,DefaultOn}`、People 列表、
  `DefaultVolume`、`DefaultTextSize`；磁贴展开详情、时长盘（`ArcDurationDial`）、回弹滑杆（`ArcRubberMeter`）、
  `StateChanged` 事件；键盘可调滑杆与表盘。
- **数据形态**：选项集合 + 默认值；状态在控件内，事件只告知变更，无持久化与系统调用。
- **生活工具缺口**：可作"专注模式/免打扰/家庭场景"的控制面板，但缺：**系统级联动**（无法真正静音通知、无法启动
  系统专注、无托盘）；无场景/自动化规则（"到家自动…"）；无设备/房间模型（智能家居需要设备清单与状态回读）；
  无持久化与跨设备同步。

---

## 5. Gallery 自身页面与工具链专章

### 5.1 53 个分组/共享页（catalog 项的"页面归属"索引）

> 统计口径：`ArcPage` 具体子类中非 `ArcItemPage` 的页面，共 **53** 个；其中 **49 个**承担 catalog 项演示，
> 4 个（Foundations / Tokens / Icons / Motion）不承载 catalog 项，见 §5.2。"覆盖项数"为 §1.3 路由结果。

| 页面标题 | 项目 | 文件 | 覆盖 catalog 项数 |
| --- | --- | --- | ---: |
| Actions | Actions | `src/Arc.Avalonia.Actions/Pages/ActionsPage.cs` | 4 |
| Actions, part two | Actions | `src/Arc.Avalonia.Actions/Pages/ActionsPartTwoPage.cs` | 5 |
| Disclosure | Actions | `src/Arc.Avalonia.Actions/Pages/DisclosurePage.cs` | 1 |
| Mega menu | Actions | `src/Arc.Avalonia.Actions/Pages/MegaMenuPage.cs` | 1 |
| Navigation | Actions | `src/Arc.Avalonia.Actions/Pages/NavigationPage.cs` | 4 |
| Overlays | Actions | `src/Arc.Avalonia.Actions/Pages/OverlaysPage.cs` | 3 |
| Overlays, part two | Actions | `src/Arc.Avalonia.Actions/Pages/OverlaysPartTwoPage.cs` | 3 |
| Share access | Actions | `src/Arc.Avalonia.Actions/Pages/ShareAccessPage.cs` | 1 |
| Advanced actions | AdvancedActions | `src/Arc.Avalonia.AdvancedActions/Pages/AdvancedActionsPage.cs` | 3 |
| Advanced actions, part two | AdvancedActions | `src/Arc.Avalonia.AdvancedActions/Pages/AdvancedActionsPartTwoPage.cs` | 2 |
| Advanced inputs | AdvancedInputs | `src/Arc.Avalonia.AdvancedInputs/Pages/AdvancedInputsPage.cs` | 2 |
| Advanced inputs, part two | AdvancedInputs | `src/Arc.Avalonia.AdvancedInputs/Pages/AdvancedInputsPartTwoPage.cs` | 2 |
| Advanced inputs, part three | AdvancedInputs | `src/Arc.Avalonia.AdvancedInputs/Pages/AdvancedInputsPartThreePage.cs` | 2 |
| Advanced inputs, part four | AdvancedInputs | `src/Arc.Avalonia.AdvancedInputs/Pages/AdvancedInputsPartFourPage.cs` | 3 |
| Advanced inputs, part five | AdvancedInputs | `src/Arc.Avalonia.AdvancedInputs/Pages/AdvancedInputsPartFivePage.cs` | 2 |
| Charts | Charts | `src/Arc.Avalonia.Charts/Pages/ChartsPage.cs` | 6 |
| Charts, part two | Charts | `src/Arc.Avalonia.Charts/Pages/ChartsMorePage.cs` | 5 |
| Brush chart | Charts | `src/Arc.Avalonia.Charts/Pages/ChartsBrushPage.cs` | 1 |
| Advanced charts | Charts | `src/Arc.Avalonia.Charts/Pages/AdvancedChartsPage.cs` | 3 |
| Advanced charts, part two | Charts | `src/Arc.Avalonia.Charts/Pages/AdvancedChartsPartTwoPage.cs` | 3 |
| Content | Content | `src/Arc.Avalonia.Content/Pages/ContentPage.cs` | 2 |
| Flows | Content | `src/Arc.Avalonia.Content/Pages/ContentFlowsPage.cs` | 2 |
| Effects | Effects | `src/Arc.Avalonia.Effects/Pages/EffectsPage.cs` | 5 |
| Text effects | Effects | `src/Arc.Avalonia.Effects/Pages/EffectsMotionPage.cs` | 4 |
| Activity | Feedback | `src/Arc.Avalonia.Feedback/Pages/ActivityPage.cs` | 2 |
| Data | Feedback | `src/Arc.Avalonia.Feedback/Pages/DataPage.cs` | 2 |
| Data views | Feedback | `src/Arc.Avalonia.Feedback/Pages/DataViewsPage.cs` | 2 |
| Data grid | Feedback | `src/Arc.Avalonia.Feedback/Pages/DataGridPage.cs` | 2 |
| Explorer | Feedback | `src/Arc.Avalonia.Feedback/Pages/ExplorerPage.cs` | 2 |
| Feedback | Feedback | `src/Arc.Avalonia.Feedback/Pages/FeedbackPage.cs` | 2 |
| Identity | Feedback | `src/Arc.Avalonia.Feedback/Pages/IdentityPage.cs` | 4 |
| Presence | Feedback | `src/Arc.Avalonia.Feedback/Pages/PresencePage.cs` | 1 |
| Steps | Feedback | `src/Arc.Avalonia.Feedback/Pages/StepsPage.cs` | 1 |
| Time | Feedback | `src/Arc.Avalonia.Feedback/Pages/TimePage.cs` | 2 |
| Tree table | Feedback | `src/Arc.Avalonia.Feedback/Pages/TreeTablePage.cs` | 1 |
| Inputs | Inputs | `src/Arc.Avalonia.Inputs/Pages/InputsPage.cs` | 5 |
| Inputs, part two | Inputs | `src/Arc.Avalonia.Inputs/Pages/InputsPartTwoPage.cs` | 5 |
| Inputs, part three | Inputs | `src/Arc.Avalonia.Inputs/Pages/InputsPartThreePage.cs` | 5 |
| Inputs, part four | Inputs | `src/Arc.Avalonia.Inputs/Pages/InputsPartFourPage.cs` | 3 |
| Gestures | Media | `src/Arc.Avalonia.Media/Pages/GesturesPage.cs` | 3 |
| Media | Media | `src/Arc.Avalonia.Media/Pages/MediaPage.cs` | 3 |
| Media layout | Media | `src/Arc.Avalonia.Media/Pages/MediaLayoutPage.cs` | 2 |
| Pickers | Pickers | `src/Arc.Avalonia.Pickers/Pages/PickersPage.cs` | 2 |
| Ranges and time | Pickers | `src/Arc.Avalonia.Pickers/Pages/RangesAndTimePage.cs` | 2 |
| Color | Pickers | `src/Arc.Avalonia.Pickers/Pages/ColorPage.cs` | 1 |
| Emoji picker | Pickers | `src/Arc.Avalonia.Pickers/Pages/EmojiPickerPage.cs` | 1 |
| Files and ink | Pickers | `src/Arc.Avalonia.Pickers/Pages/FilesAndInkPage.cs` | 2 |
| Avatar upload | Pickers | `src/Arc.Avalonia.Pickers/Pages/AvatarUploadPage.cs` | 1 |
| Kit proof | Gallery | `src/Arc.Avalonia.Gallery/Pages/ProofPage.cs` | 3 |

合计：49 页覆盖 128 个 Components 项（其中 Kit proof 覆盖 3 项）。

### 5.2 非 catalog 页面（Foundations / Tokens / Icons / Motion / Kit proof）

这 5 页是 Gallery 自己的"设计系统说明页"，不来自 catalog，但**是扩展新组件时必须对齐的规范来源**：

| 页面 | 文件 | 内容 | 对扩展工作的意义 |
| --- | --- | --- | --- |
| Foundations | `src/Arc.Avalonia.Gallery/Pages/FoundationsPage.cs`（102 行） | 语义色板（background/surface/…/accent）与字号阶 | 新生活组件只能引用这些语义角色，不能写死颜色/字号 |
| Tokens | `src/Arc.Avalonia.Gallery/Pages/TokensPage.cs`（164 行） | 同心圆角 18/26/34/pill、4px 间距网格、阴影、弹簧预设、8 个 accent | 圆角嵌套公式（内 = 外 − padding）、控件高度 36/44/50 |
| Icons | `src/Arc.Avalonia.Gallery/Pages/IconsPage.cs`（83 行） | 161 个 Lucide 图标，24 单位网格、stroke 1.75 | 生活域图标需先确认是否已有（如 `pill`/`wallet`/`calendar` 等），缺的按同规格补 `ArcIconData` |
| Motion | `src/Arc.Avalonia.Gallery/Pages/MotionPage.cs`（94 行） | SpringSmooth/Snappy/Morph 与 tween 规则、reduced-motion 分支 | 新交互的时长/回弹/降级必须符合这些预设与"状态类零过冲"规则 |
| Kit proof | `src/Arc.Avalonia.Gallery/Pages/ProofPage.cs`（148 行） | 基础控件验证：按钮三尺寸四权重、Badge、Card、Icon、Spinner | 新增基础控件后应在这里补验证，再接具体生活组件 |

### 5.3 审计 / 截图 / 目录工具链（扩展工作的"验收基础设施"）

| 工具 | 入口 | 行为 | 对扩展的意义 |
| --- | --- | --- | --- |
| 目录审计 | `--audit` → `src/Arc.Avalonia.Gallery/Framework/CatalogAudit.cs` | 遍历 219 页，收集 `ArcItemPage.ItemSlug` 与 `arc-heading` 标题，按"独立页→标题→别名"判定 294 项是否可达；输出 `CATALOG AUDIT: pages=… items=… missing=…`，非零退出码表示失败 | **新增 catalog 项/页面后必须仍 294/294**；新增项要选一条路由（独立页/标题/别名），别名表在 `src/Arc.Avalonia.Demo/ArcCatalogAliases.cs` |
| 页面清单 | `--list` → `App.axaml.cs` | 打印 `Order / Group / Title (slug)`，用于核对 219 页与侧栏顺序 | 新页的 `Order`/`Group` 决定侧栏位置 |
| 导航回归 | `--smoke` → `src/Arc.Avalonia.Gallery/Framework/NavigationSmoke.cs` | 驱动真实侧栏：点遍按钮 3 轮、每页"切走再切回"、每个 catalog 项 `ShowItem` 两次、主题/accent 切换、搜索过滤，任何重挂载异常即失败 | 新增页面若引入"控件已有父节点"类问题会在这里暴露 |
| 截图 | `--shot` → `src/Arc.Avalonia.Gallery/Framework/ShotRunner.cs`；`tools/shots.ps1` | 离屏窗口渲染每页明暗两套，自然高度上限 **1400px**，超出会打印 `SHOT WARNING … split it into two pages`；文件名为 `{order:000}-{pageSlug}.png` | 生活工具新页面的视觉评审沿用同一流程；页面过高必须拆页 |
| 目录生成 | `tools/build-catalog.ps1` | 从 `.recon/catalog-parsed.json` 生成 `docs/arc-catalog.json`；内含 23/5/6 的分组清单并校验 special/block 只出现一次 | 扩展目录应以同样的"分组 + 校验"方式生成，便于与上游 taxonomy 对齐 |
| 构建 | `tools/build.ps1` | 单节点构建（本机并行 MSBuild 不稳定）；`shots.ps1` 以 `-p:BuildProjectReferences=false` 只重建 Gallery | 新增项目时需进 `ArcUI.slnx` 与依赖链 |

### 5.4 截图与页面规模的现状约束

- `shots/light` 与 `shots/dark` 各 **219** 个 PNG（已用 `Get-ChildItem` 计数核对）；文件名是 `{Order:000}-{pageSlug}.png`。
- 页面高度上限 1400px 是**硬约束**：`ShotRunner.MaxHeight = 1400`，超限即输出警告并裁剪，因此展示页会被拆成
  `part two/four/five` 这类多页（输入、图表、高级图表都因此拆分）。
- 页面宽度默认 1180px（`shots.ps1 -Width` 可调），截图使用 `ArcThemeManager.ReducedMotion = true`，
  所以动效在截图中是稳定静帧——这解释了为什么大量演示依赖"状态按钮"而不是连续动画。
- 该工具链只覆盖"渲染 + 可达性 + 导航"，**没有**单元测试、性能基准或视觉 diff（`docs/04` §0.4 的"工程护栏 ★☆☆☆☆"）。

---

## 6. 现有能力总评（供主报告引用）

### 6.1 分级定义

- **已覆盖**：有可复用控件 + 有页面范式 + 所需数据形态基本齐全（只差接入数据）。
- **半覆盖**：有 UI/页面范式，但缺少让它在生活中"跑起来"的关键能力（持久化、调度、触达、数据模型之一）。
- **空白**：仓库内没有对应组件/页面，或只有不相关的近似物。

### 6.2 已覆盖域

| 能力域 | 现有资产（可复用） | 证据 |
| --- | --- | --- |
| 应用外壳与导航 | `workspace-sidebar`、`sidebar-rail`、`docs-sidebar`、`tabs`、`breadcrumb`、`command-palette`、`settings-command` | `src/Arc.Avalonia.Blocks.App/Pages/`、`src/Arc.Avalonia.Actions/Controls/` |
| 搜索与命令 | `command-palette`、`search-results`、`expanding-search`、`combobox`、`filter-toolbar` | `CommandPalettePage.cs`、`SearchResultsPage.cs` |
| 输入与表单 | 18 项 Inputs 目录组件（20 个控件类）+ 11 项 AdvancedInputs（12 个控件类）+ `file-dropzone`、`otp-input`、`multi-step-form`、`signup/sign-in` | `src/Arc.Avalonia.Inputs/Controls/`、`src/Arc.Avalonia.AdvancedInputs/Controls/` |
| 数据浏览与编辑 | `data-grid`、`sortable-data-table`、`tree-table`、`tree-view`、`json-viewer`、`code-block` | `src/Arc.Avalonia.Feedback/Controls/ArcDataGrid.cs` 等 |
| 数据可视化 | 14 个 Charts + 7 个 Advanced charts + 20 个 Data blocks（仪表盘/瀑布/预测/地图/回放…） | `src/Arc.Avalonia.Charts/Controls/`、`src/Arc.Avalonia.Blocks.Data/Pages/` |
| 对话与消息 | `chat-thread`、`ai-chat`、`ai-composer`、`comment-thread`、`support-conversation`、`inbox-triage` | `src/Arc.Avalonia.Content/Controls/ArcChatThread.cs`、`BlocksAI/AiChatPage.cs` |
| Agent 运行时可视化 | `agent-status`、`tool-call`、`agent-plan`、`agent-timeline`、`context-window`、`agent-run`、`agent-orchestrator`、`eval-suite`、`mcp-servers` | `src/Arc.Avalonia.Special.AIWidgets/Controls/`、`src/Arc.Avalonia.Blocks.AI/Pages/` |
| 计划/任务/看板 | `kanban-board`、`project-board`、`agent-tasks`、`stepper`、`release-readiness`、`onboarding-checklist` | `src/Arc.Avalonia.Blocks.Team/Pages/`、`src/Arc.Avalonia.Feedback/Controls/` |
| 通知/消息呈现 | `notification-center`、`toast`、`toast-stack`、`alert`、`announcement-bar`、`inbox-sidebar` | `src/Arc.Avalonia.Blocks.App/Pages/NotificationCenterPage.cs`、`src/Arc.Avalonia.Actions/Controls/ArcToast*.cs` |
| 设置与账户 | `settings-page`、`security-settings`、`env-variables`、`api-keys`、登录三件套 | `src/Arc.Avalonia.Blocks.App/Pages/SettingsPage.cs` 等 |
| 媒体与上传 | `file-upload`、`avatar-upload`、`photo-grid`、`lightbox-gallery`、`media-player`、`signature-pad`、`voice-recorder`（UI） | `src/Arc.Avalonia.Pickers/Controls/`、`src/Arc.Avalonia.Special.Media/Controls/` |
| 快捷控制/专注 | `control-center`、`switch`、`slider`、`segmented-control`、`hold-to-confirm` | `src/Arc.Avalonia.Special.Glass/Controls/ArcControlCenter.cs` |
| 活动/习惯可视化 | `activity-heatmap`、`activity-rings`、`activity-terrain`、`animated-counter`、`metric-card` | `src/Arc.Avalonia.Charts/Controls/ArcActivityHeatmap.cs` 等 |
| 提醒/例行任务范式 | `agent-routines`（句式日程 + 倒计时 + 运行历史） | `src/Arc.Avalonia.Blocks.AI/Pages/AgentRoutinesPage.cs` |

### 6.3 半覆盖域（有 UI，缺落地能力）

| 生活域 | 现有可复用项 | 缺的关键能力 |
| --- | --- | --- |
| 日程/日历 | `calendar`、`date-picker`、`date-range-picker`、`time-picker`、`date-reel`、`week-calendar`、`availability-picker` | 事件模型（标题/时间/地点/人）、重复规则、提醒触达、时区、跨日历源、持久化；`week-calendar` 为页面级实现而非控件 |
| 提醒/通知 | `notification-center`、`toast`、`toast-stack`、`alert`、`agent-routines` | 系统通知/托盘、调度引擎、优先级/静默时段、去重与合并、点击深链、跨会话未读 |
| 任务/待办 | `kanban-board`、`project-board`、`agent-tasks`、`onboarding-checklist`、`agent-plan` | 持久化、截止日/优先级/负责人、重复任务、子任务、提醒、收件箱式录入 |
| 健康/习惯 | `activity-heatmap`、`activity-rings`、`activity-terrain`、`metric-card`、`countdown` | 习惯/目标/打卡实体、数据录入与历史、服药/体征记录、提醒、隐私保护 |
| 财务/账单 | `usage-meter`、`usage-billing`、`invoice-studio`、`billing-overview`、`wallet-card`、`budget-variance` | 账本/交易/分类录入、对账、周期预算、到期提醒、货币/税务、导出 |
| 订阅/续费 | `usage-pricing`、`billing-overview`、`cancel-flow`、`plan-comparison` | 订阅清单与续费日、扣款提醒、价格变更跟踪（无网络/邮件接入） |
| 记录/归档 | `rich-text-editor`、`reorderable-list`、`document-extract`、`knowledge-base`、`code-block` | 文档库/全文检索/标签、附件落盘、加密、导入导出 |
| 出行/物流 | `delivery-tracker`、`journey-flow`、`week-calendar`、`market-terminal`（地图能力） | 行程/itinerary 模型、地点/路线组件、实时状态源、出发提醒 |
| 社交/关系 | `team-directory`、`presence-stack`、`comment-thread`、`invite-people`、`contact-section` | 联系人/关系实体、生日与上次联系、消息通道、提醒生成 |
| 专注/打扰 | `control-center`、`focus` 相关（`ArcFocusMode`）、`notification-center` | 系统级免打扰/专注联动、场景自动化、托盘状态、时长统计 |
| 智能家居 | `control-center`（开关/滑杆隐喻）、`switch`、`slider`、`control-center` 的 People/Channels | 设备/房间/场景模型、状态回读、协议接入（Matter/HomeKit 等）、离线队列 |
| 购物/家庭物资 | `product-listing`、`cart-drawer`、`checkout-summary`、`reorderable-list`、`kanban-board` | 清单模型（共享清单/勾选/价格）、常购/补货提醒、家庭共享 |
| 语音入口 | `voice-orb`、`voice-recorder`、`waveform` 视觉 | 真实音频采集/播放、ASR/TTS、唤醒与打断、权限与隐私提示 |
| 家庭协作 | `roles-permissions`、`team-members`、`share-access`、`share-sheet` | 多成员/家庭账户、共享数据与冲突合并、权限模型落地 |
| 数据导出/备份 | `data-grid`、`json-viewer`、`signature-pad` PNG 导出 | 通用导出（CSV/JSON/PDF）、备份/恢复、迁移 |

### 6.4 空白域（仓库内基本没有对应能力）

| 空白域 | 说明 | 现有"最接近"的东西（不足以替代） |
| --- | --- | --- |
| 持久化/本地存储 | 无数据库、无设置文件、无迁移；除截图与签名导出外无文件写入 | 页面内 `List<T>` 内存状态 |
| 系统集成 | 0 处 `TrayIcon` / `WindowNotificationManager`；无开机自启、后台常驻、系统通知 | `toast`/`notification-center` 都是窗口内 UI |
| 调度/重复规则 | 无 cron/RRULE/自然语言解析；`agent-routines` 的 schedule 是展示字符串 | `countdown`（纯视觉倒计时）、`date-range-picker` 预设 |
| 网络/同步 | 0 处 `HttpClient`/`WebSocket`；所有 Blocks 数据为代码内示例 | `webhooks`/`api-keys` 页面（只有 UI） |
| 本地 AI/模型运行 | 无本地模型管理；`model-picker` 只是选择器 | `mcp-servers`、`eval-suite`（演示） |
| 权限/沙箱/审批执行 | `agent-approvals` 明确"nothing reaches an agent"；无权限模型、无沙箱提示 | `hold-to-confirm`（确认交互） |
| 加密/安全存储 | 无凭据/密钥存储；`env-variables` 是掩码展示 | `security-settings` 页面（引导 UI） |
| 时区/本地化日期 | 日期控件均为无时区 `DateTime`；无 i18n | `date-reel`/`time-dial`（展示型） |
| 通知去重/摘要/静默 | 无优先级、无分组订阅、无免打扰时段 | `notification-center` 的 All/Unread 两种视图 |
| 文件/媒体真实 IO | `file-upload`/`voice-recorder`/`avatar-upload` 均为模拟 | `file-dropzone` UI |
| 多用户/家庭账户 | `CurrentUserId` 是字符串；无会话/账户体系 | `team-*` blocks（示例数据） |
| 应用级状态与恢复 | 无"上次状态"、无草稿保存、无崩溃恢复 | 页面内局部变量 |

### 6.5 给主报告的 8 条结论

1. **目录层面 100% 完成、无重复项**：294/294 可达，219 页 438 截图；主报告可直接引用"全量覆盖"这一事实，无需再核。
2. **UI 基座强、运行时为零**：控件/主题/动效/无障碍成熟（703 处 `AutomationProperties`，见 `docs/04` §0.2），但
   持久化/通知/调度/网络/音频/虚拟化全部缺失——生活工具必须在 UI 之外自带"引擎层"。
3. **"日程"是最大的半覆盖域**：日历选择器齐全，但**没有事件模型**；`week-calendar` 是最接近的页面范式
   （`src/Arc.Avalonia.Blocks.Team/Pages/WeekCalendarPage.cs`），可复用其布局与交互约定，但需重写为数据驱动控件。
4. **"提醒"没有触达**：`notification-center`/`toast` 只是窗口内呈现；系统通知、托盘、静默时段为空白，
   这决定了"提醒"类生活工具的核心体验无法用现有组件交付。
5. **Agent 侧视觉资产异常丰富**：`agent-status/plan/timeline/tool-call/context-window/approvals/routines/run/orchestrator`
   覆盖"理解→计划→执行→审批→记忆→复盘"的全部展示阶段，缺的是事件流、持久化、权限执行与通知桥（见 03 号分析）。
6. **"专注/免打扰/快捷控制"有强范式**：`control-center` + `ArcFocusMode`/`ArcRubberMeter`/`ArcDurationDial`
   可直接作为生活场景控制面板，但全部停留在 UI 状态。
7. **语音是外壳**：`voice-orb`/`voice-recorder` 的视觉完成度高，但无真实音频链路，生活工具的语音入口需从零补。
8. **性能护栏是硬约束**：无虚拟化（`docs/04` R4/G-P0-5/7/12）、表格/聊天/列表整树重建；任何"每天几百条记录"的
   生活数据视图在现状下都会遇到规模墙——扩展分析必须把"数据量/虚拟化"作为 P0 验收项。

### 6.6 文档漂移与不确定项（诚实标注）

| 项 | 说明 |
| --- | --- |
| `docs/01-arc-analysis.zh.md` §2.10/§5.1 写"110 个区块""127 个常规组件" | 早于最终范围；当前 catalog/README/`docs/03` 均为 **128 + 55 + 111**。本文以 catalog 为准。 |
| `docs/03-implementation.zh.md` §4 "常规组件 53 页（含 Tree table）" | 53 个共享页中有 5 个是 Gallery 自身页面（Foundations/Tokens/Icons/Motion/Kit proof），其中 Kit proof 覆盖 3 个 catalog 项；因此"承载 catalog 的共享页"实为 **49**。 |
| `docs/04` "非 obj `.cs` 516" | 本文复算 516（含 `Pages/` 目录 232 个文件）；公共类扫描覆盖其中非 `Pages/` 目录的 284 个源码文件（另有 12 个 `Pages/` 下的 helper 文件如 `InputsSections.cs` 未计入类统计）；两个口径都与该文档不冲突。 |
| 页面归属（标题匹配的 88 项） | 静态解析 `Section("…")` 与 `InputsSections`/`AdvancedInputsSections` 调用得到；运行时 `CatalogAudit` 用的是"全页面 heading 集合"，只在 `Empty state`（同时出现在 Data grid 与 Feedback 页）存在多页同名的歧义，不影响可达性结论。 |
| `--audit` 实际输出 | 本次盘点未构建/未运行（任务约束）；294/294 是**按同一算法静态复算**的结果，且与 README/`docs/03` 记录的 `missing=0` 一致。补充：Lead 在 2026-10-26 23:09 对现有构建重跑了一次 `--audit`，输出 `pages=219 items=294 itemPages=166 matchedBySection=88 matchedByAlias=40 missing=0 pageFailures=0`（日志 `.recon/audit-2026-10-26.log`），与本表静态复算一致。 |
| 控件类数 236 | 继承链启发式判定；`ArcGlide`/`ArcAdvancedGlide`/`ArcSpecialCanvas`/`ArcMotionCanvas` 等偏"视觉助手"的类也被计入控件；若按"可直接实例化的业务控件"口径会更少。 |
| `Internal` 类 100 | 按项目去重统计，`src/Arc.Avalonia`（Core）没有 internal 类；`src/Arc.Avalonia.Blocks.Data` 的 33 个 internal 中含 16 个图表 stage。 |
| 跨项目同名类 | `ArcActivityHeatmap`、`ArcAnimatedCounter` 在 Charts 与 Feedback 各一份（不同命名空间）；引用时需确认来源。 |
| 截图统计 | 按目录文件数（219/219）核对，未逐张打开校验内容。 |

---

## 附录 A：页面 slug 与 catalog slug 不一致的 9 个独立页

这 9 项的 `ItemSlug` 与其 catalog 标题的 kebab 形式不同（页面 `Title` 仍由 `Item.Title` 派生，未覆盖）；
截图文件名取 **页面 Title 的 kebab 形式**，因此与 catalog slug 不同：

| catalog slug | 页面 Title | 截图文件名中的 slug | 页面文件 |
| --- | --- | --- | --- |
| `glass-tabbar` | Glass tab bar | `glass-tab-bar` | `src/Arc.Avalonia.Special.Glass/Pages/GlassTabBarPage.cs` |
| `checkout-live` | Live checkout | `live-checkout` | `src/Arc.Avalonia.Blocks.Commerce/Pages/CheckoutLivePage.cs` |
| `signup-form` | Sign up form | `sign-up-form` | `src/Arc.Avalonia.Blocks.App/Pages/SignupFormPage.cs` |
| `login-centered` | Centered login | `centered-login` | `src/Arc.Avalonia.Blocks.App/Pages/LoginCenteredPage.cs` |
| `login-immersive` | Immersive login | `immersive-login` | `src/Arc.Avalonia.Blocks.App/Pages/LoginImmersivePage.cs` |
| `testimonial-stage` | Studio perspectives | `studio-perspectives` | `src/Arc.Avalonia.Blocks.Team/Pages/TestimonialStagePage.cs` |
| `roles-permissions` | Roles and permissions | `roles-and-permissions` | `src/Arc.Avalonia.Blocks.Team/Pages/RolesPermissionsPage.cs` |
| `env-variables` | Environment variables | `environment-variables` | `src/Arc.Avalonia.Blocks.Team/Pages/EnvVariablesPage.cs` |
| `document-extract` | Document extraction | `document-extraction` | `src/Arc.Avalonia.Blocks.AI/Pages/DocumentExtractPage.cs` |

其余 157 个独立页的页面 slug 与 catalog slug 一致。

## 附录 B：逐项核对清单（294 项覆盖证明）

- 独立页路由：166 项 = Special 55（`glass-menu`…`date-reel`）+ Blocks 111（`site-header`…`agent-run`）。
- 标题匹配路由：88 项，全部落 §5.1 的 49 个共享页；49 页合计覆盖 128 个 Components 项（其中 `button`/`badge`/`card` 3 项走 Kit proof 的别名路由）。
- 别名路由：40 项，全部由 `src/Arc.Avalonia.Demo/ArcCatalogAliases.cs` 的 `Map` 覆盖（Inputs 系 17、Charts 系 18、`toast-stack`、`data-grid`、按钮三件套 3）。
- 交叉验证：`items` 128 = 88（标题） + 40（别名） = 128；`specialItems` + `blockItems` = 166 = 独立页数。**总数 294，遗漏 0。**

> 维护提示：任何新增 catalog 项都应同时更新 `docs/arc-catalog.json`（与 `src/Arc.Avalonia.Demo/Catalog/arc-catalog.json` 副本）、
> 分组数组、以及（若走共享页）`ArcCatalogAliases` 或页面内 `Section` 标题；随后跑 `--audit` 与 `--smoke`，
> 再用 `tools/shots.ps1` 生成明暗两套截图。这是本仓库既有的"完成定义"。


