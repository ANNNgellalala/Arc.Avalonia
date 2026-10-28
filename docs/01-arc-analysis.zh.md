# Arc UI 设计系统分析

本文研究 Arc UI（[uiarc.dev](https://uiarc.dev)）的设计系统，目标是在 Avalonia 中复刻它的设计语言与组件行为。依据全部来自站点公开资料：概览页 [docs/introduction](https://uiarc.dev/docs/introduction)、机器可读索引 [llms.txt](https://uiarc.dev/llms.txt) 与 [llms-full.txt](https://uiarc.dev/llms-full.txt)、技能文件 [design.md](https://uiarc.dev/r/skills/arc/design.md)、[motion.md](https://uiarc.dev/r/skills/arc/motion.md)、[composition.md](https://uiarc.dev/r/skills/arc/composition.md)、[accessibility.md](https://uiarc.dev/r/skills/arc/accessibility.md)、[copy.md](https://uiarc.dev/r/skills/arc/copy.md)、[responsive.md](https://uiarc.dev/r/skills/arc/responsive.md)、[SKILL.md](https://uiarc.dev/r/skills/arc/SKILL.md)、[INSTRUCTIONS.md](https://uiarc.dev/r/skills/arc/INSTRUCTIONS.md)、[checklist.md](https://uiarc.dev/r/skills/arc/checklist.md)、基础 token 注册项 [arc-foundation.json](https://uiarc.dev/r/arc-foundation.json)，以及 25 个组件的 markdown 页面（见文末来源清单）。

站点的三处计数并不一致，本文按下面的口径处理，避免把不同时间的快照混为一谈：

- [llms.txt](https://uiarc.dev/llms.txt) 概览：185 个组件、110 个区块；其中 118 个组件与 22 个区块为 MIT 免费，67 个组件与 88 个区块为 Pro。
- [docs/introduction](https://uiarc.dev/docs/introduction) 的侧栏导航：23 个分组共 127 个常规组件，另有 55 个 Special 组件，区块标为 103。
- 差集来自列表生成时间不同。第 2 节按导航的 23 个分组列全 127 个常规组件，Special 与区块按用途归纳。

## 1 概览

### 1.1 定位与形态

Arc 自述为“面向 AI 辅助前端开发的 React 组件与区块库”（[llms.txt](https://uiarc.dev/llms.txt)）。它的产品形态不是常规 npm 运行时依赖，而是 shadcn 兼容的注册表条目：每个组件以源码形式复制进项目，文件落在 `components/arc/<id>/` 下，可以自由阅读与修改。这一点决定了它的文档写法与分发方式，也决定了复刻时的心理模型：源码即文档，文档即规格。

### 1.2 技术栈

| 层 | 选型 | 说明 |
| --- | --- | --- |
| 视图 | React | 组件即函数组件，交互状态由调用方控制或组件内部维护 |
| 样式 | CSS Modules + CSS 变量 | 明确不使用 Tailwind 类，样式与结构一一对应 |
| 主题 | `data-theme` 与 `data-accent` | 挂在 `<html>` 上，token 自动切换明暗与强调色 |
| 动效 | Motion（`motion/react`） | 弹簧预设在 `components/arc/lib/motion-tokens.ts`，另配 CSS 过渡变量 |
| 交互原语 | Radix | select、checkbox、switch、tabs、accordion、dialog、popover、tooltip、dropdown-menu 等 |
| 图标 | lucide-react | 16、20、24px，`strokeWidth={1.75}` |

注册项会声明自己的依赖闭包。例如 [select](https://uiarc.dev/components/select/markdown) 的依赖是 `@radix-ui/react-select`、`motion`、`lucide-react`，安装时同时拉入动效预设与它组合使用的 Arc 组件；设计 token 由 `arc-foundation` 一次性安装，任何组件都不会重复安装它。

### 1.3 分发模型

安装有两条等价路径：完整 URL，或先在 `components.json` 注册命名空间后按名安装。

```bash
npx shadcn@latest add https://uiarc.dev/r/arc-foundation.json
npx shadcn@latest add @uiarc/button @uiarc/dialog
```

Pro 条目走另一套需要 token 的注册表 `@uiarc-pro`，请求头带 `Authorization: Bearer ${ARC_PRO_TOKEN}`。MCP 服务器对通过 OAuth 登录的 Pro 账户，或对带同一 token 的请求，会在 `get_component` 返回 Pro 源码，并让 `get_install_command` 返回 `@uiarc-pro` 命令。官方规则明确要求：没有 Pro 权限时不得重建 Pro 条目，只能建议免费替代品（[llms.txt](https://uiarc.dev/llms.txt)）。

真正让这套分发对代理友好的是几组机器可读接口，它们对应三种上下文预算：

| 接口 | 内容 | 适用场景 |
| --- | --- | --- |
| [llms.txt](https://uiarc.dev/llms.txt) | 概览、安装、规则、全部条目一行描述 | 中等上下文，做选型与清单 |
| [llms-full.txt](https://uiarc.dev/llms-full.txt) | 同文件加每个组件 markdown 内联 | 大上下文，一次读全 |
| [llms-small.txt](https://uiarc.dev/llms-small.txt) | 概览、安装、每项一行 | 小上下文窗口 |
| [r/catalog.json](https://uiarc.dev/r/catalog.json) 与 [r/registry.json](https://uiarc.dev/r/registry.json) | 含 Pro 的机器可读元数据、可安装条目索引 | 程序化筛选与安装计划 |

MCP 端点在 `https://uiarc.dev/api/mcp`，提供 `search_components`、`list_components`、`get_component`、`get_install_command`、`get_skill` 五个工具（[docs/ai](https://uiarc.dev/docs/ai)）。技能文件是可读的规则集：入口 [SKILL.md](https://uiarc.dev/r/skills/arc/SKILL.md) 给工作流与原则，八个参考文件分别覆盖组件选择、页面组织、文案、设计、动效、无障碍、响应式与审查清单，另有三个完整示例页面。

### 1.4 免费与 Pro

免费部分为 MIT，Pro 为付费授权：$149/年、$249 终身，团队版 $799 一次性或 $449/年（[llms.txt](https://uiarc.dev/llms.txt)）。Pro 覆盖 67 个组件与 88 个区块，集中在 Advanced charts 整组、Special 组件的大部分、以及数据网格、表情选择器、头像上传、分享面板等复杂交互项。对复刻工作来说，边界很清楚：Pro 条目的源码不公开，本文只把它的公开文档当作行为参考，不复刻实现。

## 2 组件清单

站点把 127 个常规组件分成 23 组，下表按组给出中文名、一句用途与授权。授权列只标 Pro 的 13 项，其余为 MIT 免费。

### 2.1 按钮、手势、菜单

| 分组 | 组件 | 用途 | 授权 |
| --- | --- | --- | --- |
| Buttons | Button 按钮 | 单一主行动，次级态克制，按压缩放后回弹 | 免费 |
| Buttons | Action button 操作按钮 | 工具栏中的紧凑行动，异步结束后按钮自身显示成功 | 免费 |
| Buttons | Split button 分裂按钮 | 一个默认行动加 2 到 5 个近邻备选 | 免费 |
| Buttons | Button group 按钮组 | 相关行动合成一个表面，发丝线分隔，悬停高亮在段间滑动 | 免费 |
| Buttons | Floating button group 悬浮按钮组 | 安静托盘里的软按钮，一块共享高亮在按钮间变形 | 免费 |
| Buttons | Expanding button group 展开按钮组 | 指向或聚焦的图标按钮长开露出标签，邻居让位 | 免费 |
| Buttons | Copy button 复制按钮 | 复制值并就地确认 | 免费 |
| Buttons | Action swap 操作切换 | 在固定位置切换行动文案、图标与语气 | 免费 |
| Buttons | Icon morph 图标变形 | 菜单到关闭、播放到暂停、复制到对勾等八种双态图标 | 免费 |
| Buttons | Confirm morph 确认变形 | 破坏性按钮就地变成确认、加载、结果与撤销 | 免费 |
| Buttons | Theme switcher 主题切换器 | 四种过渡方式在明暗外观间切换 | 免费 |
| Gestures | Hold to confirm 长按确认 | 用按住而非点击确认危险操作 | 免费 |
| Gestures | Swipe actions 滑动操作 | 滑动露出行内动作，菜单路径同样可达 | 免费 |
| Gestures | Reactions 表情反馈 | 一键回应，计数滚动 | 免费 |
| Menus | Dropdown menu 下拉菜单 | 锚定触发器的动作列表 | 免费 |
| Menus | Context menu 右键菜单 | 贴近选中对象的次级动作 | 免费 |
| Menus | User menu 用户菜单 | 头像后的账户、设置、主题与退出，手机上开为底部面板 | 免费 |

### 2.2 文本输入与特殊输入

| 分组 | 组件 | 用途 | 授权 |
| --- | --- | --- | --- |
| Text fields | Input 输入框 | 单行文本，标签、帮助文案与错误就地改写 | 免费 |
| Text fields | Textarea 多行文本 | 备注、描述等长文本 | 免费 |
| Text fields | Password field 密码框 | 敏感文本，带显示与隐藏 | 免费 |
| Text fields | Password strength 密码强度 | 输入时显示密码强度 | 免费 |
| Text fields | Search field 搜索框 | 可识别的搜索入口 | 免费 |
| Text fields | Expanding search 展开搜索 | 图标变形为搜索框，结果落在下方 | 免费 |
| Text fields | Inline edit 就地编辑 | 页面文字原位变成输入框 | 免费 |
| Special inputs | Number field 数字输入 | 有界数字，带增减控件 | 免费 |
| Special inputs | Money input 金额输入 | 货币格式、千分位、等宽数字、最小单位输出 | 免费 |
| Special inputs | Phone input 电话输入 | 国家选择、输入即格式化、E.164 输出 | 免费 |
| Special inputs | Scrub input 拖拽数字输入 | 拖动标签擦洗数值，可输入单位与算式 | 免费 |
| Special inputs | Tag input 标签输入 | 短文本转成可移除的标签 | 免费 |
| Special inputs | Mention input 提及输入 | 支持 @人 与 #频道 的单一 token 输入 | 免费 |
| Special inputs | Shortcut recorder 快捷键录制 | 组合键录成键帽，带冲突提示与速查表 | 免费 |

### 2.3 选择、开关、滑块

| 分组 | 组件 | 用途 | 授权 |
| --- | --- | --- | --- |
| Selects | Select 选择器 | 短固定列表，键盘友好的菜单 | 免费 |
| Selects | Combobox 组合框 | 长列表搜索选择，焦点不离开字段 | 免费 |
| Selects | Multi-select 多选 | 多值选择且字段保持可读 | 免费 |
| Selects | Morph select 变形选择器 | 触发器长成列表，高亮滑动，支持 typeahead | 免费 |
| Selects | Chip group 筛选标签组 | 少量分面筛选，选中时 chip 变形 | 免费 |
| Toggles | Radio cards 单选卡片 | 可选选项卡，滑动选中环，含价格与描述槽 | 免费 |
| Toggles | Billing toggle 计费切换 | 月与年切换，省额徽标，价格滚动到新值 | 免费 |
| Toggles | Checkbox 复选框 | 二值选择，含半选状态 | 免费 |
| Toggles | Radio group 单选组 | 从可见集合中选一个 | 免费 |
| Toggles | Switch 开关 | 立即生效的设置，按压时拇指拉伸 | 免费 |
| Toggles | Segmented control 分段控件 | 2 到 5 个相关视图切换，选中块滑动 | 免费 |
| Toggles | Rating 评分 | 数值评价，支持半星、擦洗、清除 | 免费 |
| Sliders | Slider 滑块 | 单值或区间，跟随手指，带刻度与读数 | 免费 |
| Sliders | Elastic slider 弹性滑块 | 越界像橡皮拉伸，数值牌摇晃，拇指卡入挡位 | 免费 |
| Sliders | Range histogram slider 直方图区间滑块 | 在数据直方图上选择价格区间 | 免费 |

### 2.4 选择器与编辑器

| 分组 | 组件 | 用途 | 授权 |
| --- | --- | --- | --- |
| Pickers | Calendar 日历 | 紧凑月视图浏览日期 | 免费 |
| Pickers | Date picker 日期选择 | 不丢失上下文的日期选择 | 免费 |
| Pickers | Date range picker 日期区间选择 | 触发器长成双月，带预设与拉伸区间高亮 | 免费 |
| Pickers | Time picker 时间选择 | 键盘行为合理的时间选择 | 免费 |
| Pickers | Color picker 取色器 | 色块长成取色器，格式变形、吸管、色板、对比度读数 | 免费 |
| Pickers | Emoji picker 表情选择 | 搜索、分类同步、肤色、最近使用、虚拟化网格 | Pro |
| Editors | Rich text editor 富文本编辑器 | Markdown 快捷输入、浮动工具栏、斜杠菜单、HTML 与 Markdown 输出 | 免费 |
| Editors | Signature pad 签名板 | 随速度变细的墨迹，撤销、回放、PNG 或 SVG 导出 | 免费 |
| Editors | File dropzone 文件拖放区 | 宽裕的拖放目标，支持多文件 | 免费 |
| Editors | Avatar upload 头像上传 | 在头像上裁剪、拖拽缩放，进度环上传 | Pro |

### 2.5 导航与展开

| 分组 | 组件 | 用途 | 授权 |
| --- | --- | --- | --- |
| Navigation | Tabs 标签页 | 同一对象的相关视图，指示器滑动，面板高度过渡 | 免费 |
| Navigation | Breadcrumb 面包屑 | 展示页面在层级中的位置 | 免费 |
| Navigation | Pagination 分页 | 长集合分页，边界清晰 | 免费 |
| Navigation | Mega menu morph 变形大菜单 | 站点头部各菜单共用一块浮动面板，尺寸与内容一起变形 | Pro |
| Expand | Scroll area 滚动区域 | 原生滚动容器，细滚动条与按需边缘渐隐 | 免费 |
| Expand | Accordion 手风琴 | 单开手风琴，就地展开支持信息 | 免费 |
| Expand | Expandable card 可展开卡片 | 密集卡片按需获得更多空间 | 免费 |
| Expand | Resizable panels 可调分栏 | 拖动分隔条交换两栏空间 | 免费 |
| Expand | Expandable text 展开文本 | 多行截断加渐隐，仅在溢出时显示展开 | 免费 |

### 2.6 浮层与消息

| 分组 | 组件 | 用途 | 授权 |
| --- | --- | --- | --- |
| Overlays | Dialog 对话框 | 需要打断的决策与短表单 | 免费 |
| Overlays | Drawer 抽屉 | 侧面临时表面，保留页面上下文 | 免费 |
| Overlays | Bottom sheet 底部面板 | 停在半高或全高，跟手拖动 | 免费 |
| Overlays | Popover 浮层 | 锚定的小型非模态面板 | 免费 |
| Overlays | Hover card 悬停卡片 | 悬停或聚焦预览人物与链接 | 免费 |
| Overlays | Tooltip 工具提示 | 不熟悉控件的单行说明 | 免费 |
| Overlays | Share access 分享权限 | 邀请成员、角色菜单、链接权限与就地复制 | Pro |
| Messages | Alert 提示条 | 持续在流内的消息，帮助恢复或继续 | 免费 |
| Messages | Toast 轻提示 | 后台动作完成的简短确认 | 免费 |
| Messages | Toast stack 轻提示堆栈 | 边缘堆叠的结果，可伸手取用 | 免费 |
| Messages | Announcement bar 公告栏 | 顶部横幅，轮播、倒计时、关闭时平滑收起 | 免费 |
| Messages | Cookie consent Cookie 同意条 | 同意条变形为偏好设置，折叠成重开按钮 | 免费 |

### 2.7 进度与身份

| 分组 | 组件 | 用途 | 授权 |
| --- | --- | --- | --- |
| Progress | Progress 进度条 | 已知任务的完成度，填充与数字同一弹簧 | 免费 |
| Progress | Skeleton 骨架屏 | 加载中的占位 | 免费 |
| Progress | Stepper 步骤条 | 多步流程中的位置与已完成项 | 免费 |
| Progress | Countdown 倒计时 | 滚动数字，到零变形为进行状态 | 免费 |
| Progress | Usage meter 用量计量 | 配额的填充与接近上限 | 免费 |
| Progress | Onboarding checklist 上手清单 | 引导新工作区逐步完成设置 | 免费 |
| Avatars | Avatar 头像 | 人物与账户的紧凑身份标记 | 免费 |
| Avatars | Avatar group 头像组 | 小空间展示一组人，含溢出计数 | 免费 |
| Avatars | Badge 徽标 | 状态、类别或元数据的小标签 | 免费 |
| Avatars | Presence stack 在线状态堆栈 | 当前在线的人，加入时重排，悬停展开姓名 | Pro |
| Cards | Card 卡片 | 相关内容与动作的容器，可长成快速查看对话框 | 免费 |
| Cards | Metric card 指标卡 | 需要上下文的数字，里程表式滚动 | 免费 |
| Cards | Empty state 空状态 | 无内容时给出一个下一步 | 免费 |

### 2.8 图表、表格、活动、媒体、文字效果

| 分组 | 组件 | 用途 | 授权 |
| --- | --- | --- | --- |
| Charts | Line chart 折线图 | 多序列折线，滑动十字线，切换区间时路径变形 | 免费 |
| Charts | Bar chart 柱状图 | 跨天比较单一度量，可擦洗读值 | 免费 |
| Charts | Donut chart 环形图 | 弧段在数据集间变形，中心数字滚动 | 免费 |
| Charts | Streamgraph 流图 | 摆动基线上的层叠流，可隔离单层 | 免费 |
| Charts | Brush chart 刷选图 | 密集时间序列加概览条，拖窗缩放 | 免费 |
| Charts | Waffle chart 华夫图 | 10×10 单元图，每格 1%，数据变化时单元飞向新组 | 免费 |
| Charts | Slope chart 斜率图 | 前后对比，两轴之间划线，换数据时整体倾斜 | 免费 |
| Charts | Sparkline 迷你折线 | 数值旁的紧凑趋势 | 免费 |
| Charts | Gauge 仪表 | 值对已知区间 | 免费 |
| Charts | Activity heatmap 活动热力图 | 一年活动，一格一天 | 免费 |
| Charts | Animated counter 数字滚动 | 变化的总量有方向感 | 免费 |
| Charts | Ridgeline 山脊图 | 重叠分布，一组一脊，悬停读四分位 | 免费 |
| Charts | Treemap 矩形树图 | squarified 矩形树，点击下钻 | 免费 |
| Charts | Split bar 占比条 | 100% 条切分为份额，图例可开关 | Pro |
| Advanced charts | Sunburst 旭日图 | 环形层级，点击段落换中心 | Pro |
| Advanced charts | Sankey flow 桑基图 | 阶段间流带与漂移粒子 | Pro |
| Advanced charts | Funnel chart 漏斗图 | 逐步流失的转化漏斗 | Pro |
| Advanced charts | Radar chart 雷达图 | 2 到 3 个画像叠加比较 | Pro |
| Advanced charts | Realtime stream 实时流线 | 60fps 滚动的实时折线，异常打点 | Pro |
| Advanced charts | Race bar chart 竞速条形图 | 随时间变化的排名，条形互相超车 | Pro |
| Advanced charts | Data grid 数据网格 | 电子表格式网格，区域选择、就地编辑、填充柄 | Pro |
| Tables | Sortable data table 可排序数据表 | 结构化记录的排序比较 | 免费 |
| Tables | Tree view 树视图 | 嵌套文件夹与结构化内容 | 免费 |
| Tables | Reorderable list 可排序列表 | 直接拖放排序 | 免费 |
| Tables | Filter toolbar 筛选工具栏 | 集合筛选靠近手边且易重置 | 免费 |
| Tables | Code block 代码块 | 代码的层级与复制入口 | 免费 |
| Tables | JSON viewer JSON 查看器 | 可折叠 JSON 树，搜索、长数组分页、复制路径 | 免费 |
| Activity | Timeline 时间线 | 按天分组、最新在上 | 免费 |
| Activity | Comment thread 评论串 | 嵌套回复、表情、提及、就地编辑、解决 | 免费 |
| Activity | Chat thread 聊天串 | 分组消息、回执、正在输入、带附件的输入框 | 免费 |
| Media | Image compare 图片对比 | 拖动分隔线对比两张图 | 免费 |
| Media | Carousel 轮播 | 拖动、甩动或方向键浏览 | 免费 |
| Media | Card stack 卡片堆 | 一次一张审阅，可甩出与撤销 | 免费 |
| Text effects | Text reveal 文本揭示 | 克制的入场揭示 | 免费 |
| Text effects | In-view title 滚动入视图标题 | 滚动进入视口时标题进入 | 免费 |
| Text effects | Text morph 文本变形 | 标签逐字母变成下一状态 | 免费 |
| Text effects | Text shimmer 文本微光 | 用平静的光带表示进行中 | 免费 |

### 2.9 Special 组件 55 个

Special 是展示型与 AI 场景组件，绝大多数为 Pro，本文只归纳，不复刻实现。

| 主题 | 数量 | 代表组件 |
| --- | --- | --- |
| 玻璃与导航 | 15 | Dock、Glass menu、Glass segmented、Glass card、Glass tab bar、Liquid tab bar、Morph nav、Sheet stack、Holo card、Card to article |
| 媒体与播放 | 8 | Product gallery、Lightbox gallery、Photo grid、Cover flow、Now playing、Voice orb、Voice recorder |
| 时间与按键 | 8 | Keycap keyboard、Date reel、Time dial、Activity rings、Link unfurl、Slot text、Sand text、Typewriter terminal |
| 加载与转场 | 6 | Stretch refresh、Morph loader、Skeleton morph、Black hole、Sticker peel、Page curl |
| 环境氛围 | 4 | Orbit logos、Dot grid、Gravity well、Ink fluid |
| 代理状态 | 5 | Agent status、Tool call、Agent plan、Agent timeline、Context window |
| 解释性动画 | 9 | Data flow、Payment flow、Multi-region failover、Vector search、Agent loop、Mixture of experts、Speculative decoding、Prompt cache、Reasoning tree |

### 2.10 区块 110 个

[llms.txt](https://uiarc.dev/llms.txt) 的区块是一张平表，共 110 项，站点未给分类。下面按用途重新归纳，数量为本文字段统计，免费区块 22 个散布在多个组里。

| 用途组 | 数量 | 典型区块与说明 |
| --- | --- | --- |
| 认证与入门 | 9 | Sign up form、Sign in、Centered login、OTP input、Security settings、Cancel flow、Onboarding illustration |
| 应用外壳与导航 | 10 | Command palette、Workspace sidebar、Sidebar rail、Docs sidebar、Settings page、Page header、Site header、Site footer |
| 表单与设置 | 10 | Multi-step form、File upload、Invite people、Team members、API keys、Environment variables、Webhooks、Roles and permissions、Integrations |
| 计费与商业 | 14 | Plan comparison、Pricing calculator、Usage pricing、Billing overview、Checkout summary、Live checkout、Invoice studio、MRR waterfall、Cart drawer、Product listing |
| 数据与分析 | 19 | Metrics dashboard、Metric explorer、KPI drilldown、Cohort retention、Small multiples、Horizon chart、Choropleth explorer、Market terminal、Revenue globe、Customer galaxy |
| 内容与营销 | 16 | Hero section、Hero signup、FAQ section、Contact section、CTA section、Newsletter signup、Blog grid、Changelog feed、Feature bento、Scroll story、Stats band |
| 协作与效率 | 15 | Team directory、Kanban board、Project board、Week calendar、Inbox triage、Support widget、Selection toolbar、Media player、Search results |
| AI 产品界面 | 17 | AI composer、AI chat、AI answer、AI side panel、Model picker、Prompt studio、Agent run、Agent tasks、Agent orchestrator、MCP servers、Eval suite |

对 Avalonia 复刻而言，区块的价值不在逐个照搬，而在于它们示范了页面级的组合约束：一个页面容器、一条主行动、每个数据区都要有加载、空、错误、成功四态。

## 3 设计思路分析

### 3.1 语义 token 而非原始色值

Arc 的 token 以角色命名，组件与页面只允许引用角色，禁止直接使用 `--neutral-*` 阶梯、十六进制或 Tailwind 色类（[design.md](https://uiarc.dev/r/skills/arc/design.md)）。角色分七族：

| 族 | token |
| --- | --- |
| 页面与表面 | `--background`、`--surface`、`--surface-raised`、`--surface-muted` |
| 文本 | `--foreground`、`--text-secondary`、`--text-muted` |
| 线条 | `--border`、`--border-subtle`（表面内部）、`--border-strong`（输入框与强调） |
| 强调 | `--accent`、`--accent-strong`、`--accent-subtle`、`--accent-foreground` |
| 状态 | `--success`、`--warning`、`--danger`，信息态用 `--accent` |
| 选择控件 | `--control-on`、`--control-glyph`、`--control-track`、`--control-thumb`、`--control-thumb-on` |
| 图表序列 | `--series-1` 到 `--series-4` |

这套划分的关键在于把“值”与“意图”解耦：暗色主题不是把亮色取反，而是换一组同名角色；强调色也不是换一个主色，而是换一组已经调校好的角色。复刻时如果保留角色名，主题与强调色切换几乎不需要组件配合。

### 3.2 颜色：oklch、八种强调色与暗色

基础值用 oklch 表达，中性阶梯零彩度，从 `oklch(100% 0 0)` 到 `oklch(15% 0 0)`。亮色下 `--border: oklch(93.5% 0 0)`、`--border-subtle: oklch(96.5% 0 0)`、`--border-strong: oklch(82% 0 0)`；暗色下背景 `oklch(19% 0 0)`、表面 `21.5%`、抬升表面 `24%`、静音表面 `26%`、前景 `96%`、次文本 `77%`、弱文本 `64%`、边框 `29%`。状态色亮暗成对：成功 `oklch(49% .19 150)` 与 `oklch(78% .18 150)`，警告 `oklch(58% .18 75)` 与 `oklch(83% .17 80)`，危险 `oklch(54% .21 25)` 与 `oklch(76% .2 25)`。用 oklch 的收益是可预测的明度：同一 L 值在明暗两侧观感一致，替品牌换色时不必逐色试。

八种强调色通过 `data-accent` 选择：neutral、violet、blue、green、amber、orange、coral、rose。neutral 走 oklch 灰阶，其余七色亮色值分别为 `#7747ff`、`#0562ef`、`#0db879`、`#f3ad20`、`#f48120`、`#f15f55`、`#ed4e9d`，暗色改为 `#a78bff`、`#6ba3ff`、`#53dca6`、`#ffcb62`、`#ffad5e`、`#ff9386`、`#ff8bc5`。三处细节值得照搬：

- 选择控件单独一套 `--control-on`，按强调色调校，保证白色拇指在任何色相上都不糊；琥珀色在亮色下改用深色勾。
- 中性强调色在暗色下反相：近白轨道配深色拇指，开与关不会读成一样。
- 图表序列由 `oklch(from var(--accent) …)` 相对颜色推导，序列 1 即强调色本身，2 到 4 按色相旋转与明度步进；中性强调回退到蓝色组，并用数据可视化脚本验证过任意两条序列在色觉障碍下的 ΔE 不小于 9、正常视觉不小于 17。

### 3.3 字体与排版

| 用途 | 字面 | 设置 |
| --- | --- | --- |
| 30px 以上标题 | Geist，`--font-display` | 400 或 500，字距 `-0.03em`，行高 1.1 |
| 正文、标签、控件 | Inter，`--font-body` | 400 或 500，字距 `-0.01em`，行高 1.4 |

字号只从阶梯取值：12、14、16、18、22、28、36、52、72。字重只有 400 与 500，层级靠字号、颜色（前景压次文本）与间距建立，不靠加粗。规则里还有两条容易被忽略的约束：Geist 不进控件与小标签，Inter 不进主标题；会变化或需要竖向对齐的数字使用 `tabular-nums`。

### 3.4 同心圆角

圆角四档：控件 18px、面板 26px、表面 34px、胶囊形。嵌套时内圆角等于外圆角减内边距，让两段弧线平行：

```css
.card  { border-radius: var(--radius-surface); padding: 8px; }
.inner { border-radius: calc(var(--radius-surface) - 8px); } /* 26px */
```

内边距很大时用 `max(6px, calc(...))` 兜底，避免算出负值。反过来，内外同半径会做出厚薄不均的角，这是文档明确点名的错误做法。

### 3.5 4px 网格与三种控件高度

间距是 4 的倍数，token 覆盖 4 到 24：`--space-1` 到 `--space-6`，再加 8、10、12、16、20、24（对应 32 到 96），刻意不提供 `--space-7`、`--space-9` 这类档位，逼使用者回到网格上。控件高度三档：36、44、50px，44px 是默认可点尺寸。

节奏按语境分配：产品页组内 16px、区块间 24px、长表单段落间 32 到 48px、页面内边距 24 到 32px；营销区块纵向 64 到 120px。视口变宽时只放大外侧留白，控件内部间距保持固定。

### 3.6 表面、边框与阴影

先靠邻近分组，其次用 1px 边框，只有真实边界才用卡片。阴影只给浮动层：菜单、浮层、对话框、面板用 `--shadow-floating`（`0 20px 48px oklch(0% 0 0 / .105)` 加 `0 3px 10px / .045`）；卡片靠边框站立，不用阴影；`--shadow-resting` 与 `--shadow-raised` 留给极少数的抬升元素，且不允许叠加。

并排卡片要共享边缘：同顶、同高（`align-items: stretch`），外缘与内容列齐平；要么留 16 到 24px 的真间隙，要么合成一个带发丝分隔的组，不允许出现两条边框几乎相碰的 1 到 4px 缝。文档还禁止嵌套圆角卡片、装饰性胶囊、光晕与散落的符号。

### 3.7 图标

图标统一 lucide，16、20、24px，线宽 1.75，纯装饰加 `aria-hidden`。图标与标签平铺，不放进圆角方块或圆形里，除非那个形状本身就是控件或承载状态。破坏性与不熟悉的动作优先用文字标签，而不是只留图标。

### 3.8 动效系统

动效的职责被限定为一句话：说明因果，谁打开了、什么变了、从哪来、接下来是什么；解释不了就删掉（[motion.md](https://uiarc.dev/r/skills/arc/motion.md)）。预设如下：

| 预设 | 值 | 用于 |
| --- | --- | --- |
| `spring.smooth` | 0.4s，bounce 0 | 面板、高度、抽屉、进度、布局位移，永不冲过头 |
| `spring.snappy` | 0.26s，bounce 0.12 | 按压反馈、小指示器、图标切换 |
| `spring.morph` | 0.42s，bounce 0.16 | 共享高亮、跟随内容的宽度、形状变形 |
| `duration.*` | 120、160、180、240、480ms | 颜色、透明度、小淡入淡出的补间 |
| `stagger.*` | 0.035、0.08、0.04、0.016s | 列表与文字，总时长控制在 0.4s 内 |
| `blur.*` | 2、4、8px | 仅短暂交叉淡化 |

CSS 侧另有 `--duration-spring: 580ms` 与 37 段 `linear()` 弹簧曲线 `--ease-spring`，缓动为 `cubic-bezier(.16,1,.3,1)`（进）、`(.22,1,.36,1)`（标准）、`(.7,0,.84,0)`（出）、`(.65,0,.35,1)`（进出）。

九条规则构成了可执行的边界：只动 transform 与 opacity（尺寸变化本身是信息时才动宽高，且走弹簧）；一次交互只有一段连续手势，不许淡入接滑动再接缩放；一切可中断，用户能反向的操作必须用弹簧；没有一帧完成的尺寸变化；数字用等宽字体滚到新值；按压缩到约 0.97，但作为浮层锚点的元素改用颜色回应，因为弹层按按压后的尺寸定位；退出快于进入；一页最多一次编排式入场；动画元素保持挂载，不靠改 key 重启。

### 3.9 无障碍

焦点策略是这套系统里最有辨识度的一条：指针聚焦不画环，键盘聚焦（`:focus-visible`）统一画 2px 描边、偏移 2px，颜色默认取强调强色与透明的 72% 混合。描边不画在文本框上（文本框用边框与光标示意），也不画在菜单项与列表选项上（它们用高亮填充），`tabindex="-1"` 的脚本焦点目标同样排除。基座里 `--focus-ring: transparent` 明确了这个决定，并把描边颜色做成可调 token。

其余规则：按钮做事、链接去地方，绝不在 `div` 上绑点击；每个字段都有可见标签，仅图标控件要有具体名字（“Delete invoice” 而不是 “Delete”）；组合控件只有一个 Tab 停靠点，内部用方向键；浮层移入焦点、困住焦点、Escape 关闭、关闭后归还焦点；手势一律有按钮或菜单路径；加载用 `aria-busy` 而不是 `disabled`，避免焦点丢失；异步状态用 `role="status"` 或 `aria-live="polite"`，阻断错误用 `role="alert"`；对比度正文 4.5:1、图标与控件边框 3:1，明暗两套都要查。

### 3.10 文案

规则短而硬：句首大写（sentence case），永不全大写；标题上方不放小字眉标；不用长破折号，改用句号、逗号、冒号或括号，区间写 “9 to 17”；标题末尾不加句号；按钮是动词加宾语（“Save changes” 而不是 “Submit”）；最多一句副标题，且只在标题说不清时出现；不许编造数字、客户与奖项；同一个概念全站用同一个词。错误文案要说发生了什么、怎么修，紧贴字段；空状态要说为什么空并给一个下一步；破坏性确认要点名对象与后果。

### 3.11 响应式

必测宽度 390、768、1024、1440，带长标签或表格时加测 320。检查项只有三类：没有横向页面滚动（只有代码块、宽表格、分段行在自己的容器里横滚）、没有裁切重叠与断词、主行动不用找。布局上手机沟槽 16px 并用 `clamp(16px, 4vw, 32px)` 随宽度生长，多列在约 800px 以下塌成一列，设置行在约 560px 以下标签叠到控件上方，网格与弹性子项一律 `min-width: 0`，正文测量宽度约 65 字符，触控目标至少 44px 且相邻至少 8px。悬停永远是增强，悬停才出现的内容必须也能通过点击或聚焦到达。图表跟随容器宽度，用各自的 `height` 或 `stackBelow` 一类属性处理窄屏，而不是在外面套一层媒体查询。

### 3.12 底层原则

把上面的规则压成几条可迁移的判断标准，比记住具体数值更有用。

动效解释因果。动画不是装饰，而是空间关系的陈述：面板从触发它的方向来，高亮在选项之间滑行表示“同一个选中态换了位置”，退出更快是因为结果已经发生。凡是需要三段时间才能讲清的变化，都应该改成一段。

安静的界面，强调色只在有意图时出现。大部分屏幕保持中性，强调色只给当前导航项、选中的行或选项、真正重要的那条数据序列、进度，以及少量交互细节。状态色只表示状态，且必须配文字，绿色不表示“好看”，红色不表示“重要”。

就地确认替代弹窗。保存的结果落在按钮或行上，破坏性动作让按钮自己变成一个问题，撤销留在结果旁。弹窗只留给必须打断、且一句话说不清的决策。这条原则同时降低了模态的滥用与通知的噪音。

可中断的弹簧。所有用户可能反向的操作都用弹簧，反向时从当前位置接续，不重播。状态类指示器（开关拇指、抽屉边缘、面板高度、进度填充）必须零过冲，因为过冲会短暂显示一个不真实的状态；可见回弹只留给庆祝与玩具式的瞬间。

面向代理的文档结构。每个条目都有固定的文档骨架与“何时不用”一节，事实只写一遍，属性表就是契约；选型先读文档再写代码，禁止发明属性。这套结构让代理无需读源码也能正确使用组件，也让人读起来更省力。

## 4 架构启示

### 4.1 Arc 为什么对代理友好

它的可读性不是文案写得好，而是结构上做了五件事。

注册项即安装闭包。每个条目声明依赖与 registryDependencies，一条命令就能把组件、它依赖的 Arc 组件、动效预设一起装好；基座只装一次。代理不需要推断“还缺什么”。

每个组件一篇固定骨架的 markdown。包含何时用、何时不用、安装、用法、示例、API 表、键盘、无障碍、动效、响应式、性能、给 AI 的备注、相关条目。骨架稳定意味着代理可以按段定位，也意味着遗漏会显形：没有“何时不用”就说明文档没写完。

三种上下文预算。`llms-small.txt`、`llms.txt`、`llms-full.txt` 覆盖小、中、大窗口；`catalog.json` 与 `registry.json` 给程序化筛选。同一事实只有一处权威来源，不同粒度只是投影。

MCP 工具面窄而准。搜索、列表、取详情、取安装命令、取技能文件，五个工具覆盖“找到、读清、装上”的完整链路，权限按账户分层。

规则以技能文件形式随库分发。技能里既有正向清单，也有“永不”清单与可执行的 grep 巡检，把设计评审变成可重复的机械步骤。

### 4.2 映射到 Avalonia

| Arc 概念 | Avalonia 对应实现 | 备注 |
| --- | --- | --- |
| `foundation.css` 变量 | `ArcPalette.Build(scheme, accent)` 生成 `ResourceDictionary`，键集中在 `ArcTokens` | 本仓库已有此实现，oklch 在构建时转 sRGB |
| `data-theme="dark"` | `ThemeVariant` 或应用层主题开关，切换时替换字典 | 角色名不变，只换值 |
| `data-accent` | `ArcAccent` 枚举，八色各带一套 control-on | 强调色切换应热生效 |
| 每组件 CSS Module | `Themes/<Control>.axaml` 里的 `ControlTheme` | 一个控件一个主题文件，键控样式 |
| Radix 原语 | 内置控件加 `Popup`、`Flyout`、`OverlayLayer`、`AdornerLayer` | 焦点陷阱与归还需自己实现 |
| Motion 弹簧与 `layoutId` | `Transitions`、`KeyFrameAnimation`、`RenderTransform` | 没有共享元素动画，用位移加淡化近似 |
| `:focus-visible` 焦点环 | 控件模板内的焦点视觉，绑定 `ArcFocusOutline` | 只在键盘聚焦时显示，指针聚焦不显示 |
| `aria-busy` 与 `aria-live` | `AutomationProperties` 的 Name、HelpText、LiveSetting | 状态播报靠自动化属性 |
| `ResizeObserver` 高度弹动 | 布局系统加高度动画或裁剪 | 能力弱于 Web，需减少依赖高度的动效 |
| 注册表与 MCP | `Controls/` 源码、每组件一篇 `docs/`、Demo 画廊页 | 建议再生成一份机器可读清单 |

如果要让这套复刻也具备代理友好性，最小可行动作是：组件目录与文档目录同名同序；每个组件一篇 markdown，沿用 Arc 的骨架并补一列“Avalonia 属性名与 CLR 类型”；token 键名与 `ArcTokens` 常量一一对应，文档里直接引用常量名；再用反射从控件生成一份 JSON 清单，让代理与画廊读同一份事实。

## 5 复刻范围与取舍

### 5.1 范围

本仓库的目标是忠实子集，不是完整移植：基础 token 与主题（已落地为 `ArcPalette` 与 `ArcTokens`，覆盖明暗两套与八种强调色）、约 60 到 80 个组件并覆盖全部 23 个语义分组、以及一个演示画廊。对照 Arc 的 127 个常规组件、55 个 Special 与 110 个区块，这个范围约为组件总量的三分之一到二分之一，区块则以少量页面级示例代替。

选择依据是覆盖面而非数量：每个分组至少有一个代表，让 token、控件主题、焦点、动效、无障碍这些横切关注点在真实组件上被验证一遍；同一分组内的近似变体（例如多种主题切换动画、多种按钮组合）不复刻。

### 5.2 取舍理由

授权边界。Pro 条目是付费授权内容，源码不公开，复刻时既不参考也不重建；Advanced charts 整组、数据网格、表情选择器、头像上传、分享面板，以及 Special 中绝大多数展示型组件都在此列。第 2 节与第 02 号文档中的 Pro 条目只记录公开文档里的行为与属性，用于理解设计取向，不进入实现清单。

平台差异。React 的函数组件加 Radix 原语，在 Avalonia 里要拆成“控件加控件主题加行为”三部分：弹层定位、焦点陷阱、点击外部关闭、typeahead 这些 Radix 已经处理好的事情需要自己写；Motion 的 `layoutId` 共享元素动画没有等价物，只能用同一区域内的位移与淡化表达“同一个东西换了位置”；`ResizeObserver` 驱动的高度弹动在桌面布局里收益有限，改为固定槽位加透明度过渡；`color-mix` 与相对颜色在构建期算好，避免运行时依赖。

字体。Geist 无法离线获取，标题与正文统一用 Inter，`-0.03em` 的展示字距与 1.1 的行高照旧应用。代价是超大标题的几何感略弱于 Geist，收益是字体栈只有一个来源、离线可用、不会出现回退闪烁。若后续引入 Geist，只需换 `ArcFontDisplay` 的 `FontFamily`。

触控与指针。桌面应用以鼠标与键盘为主，44px 的可点高度与 8px 的最小间距仍然保留，因为触屏笔记本与高 DPI 场景同样受益；悬停态映射到指针伪类，按压反馈用缩放加颜色，与 Web 端一致。

区块与页面。110 个区块不复刻，改为在画廊里做少量页面级示范（仪表盘、设置页、登录页），用来验证“一个页面容器、一条主行动、四态齐全”的组合约束。

### 5.3 验收要点

复刻是否忠实，可以按下面几条自查：八种强调色与明暗两套主题下，强调、状态、控件三类颜色都可用且对比度达标；焦点环只在键盘操作时出现，且每个可聚焦控件都有；状态类动效零过冲，退出快于进入；数字使用等宽字形，切换时不抖动；圆角嵌套符合内等于外减内边距；每个组件都有加载、空、错误、成功四态的处理方式。

## 来源

- [docs/introduction](https://uiarc.dev/docs/introduction)
- [llms.txt](https://uiarc.dev/llms.txt) 与 [llms-full.txt](https://uiarc.dev/llms-full.txt)
- [design.md](https://uiarc.dev/r/skills/arc/design.md)、[motion.md](https://uiarc.dev/r/skills/arc/motion.md)、[composition.md](https://uiarc.dev/r/skills/arc/composition.md)、[accessibility.md](https://uiarc.dev/r/skills/arc/accessibility.md)、[copy.md](https://uiarc.dev/r/skills/arc/copy.md)、[responsive.md](https://uiarc.dev/r/skills/arc/responsive.md)
- [SKILL.md](https://uiarc.dev/r/skills/arc/SKILL.md)、[INSTRUCTIONS.md](https://uiarc.dev/r/skills/arc/INSTRUCTIONS.md)、[checklist.md](https://uiarc.dev/r/skills/arc/checklist.md)
- [arc-foundation.json](https://uiarc.dev/r/arc-foundation.json)
- [docs/ai](https://uiarc.dev/docs/ai)
- 组件 markdown：[/components/button/markdown](https://uiarc.dev/components/button/markdown)、[/components/select/markdown](https://uiarc.dev/components/select/markdown)、[/components/dialog/markdown](https://uiarc.dev/components/dialog/markdown)、[/components/toast/markdown](https://uiarc.dev/components/toast/markdown)、[/components/line-chart/markdown](https://uiarc.dev/components/line-chart/markdown)、[/components/data-grid/markdown](https://uiarc.dev/components/data-grid/markdown)、[/components/segmented-control/markdown](https://uiarc.dev/components/segmented-control/markdown)，以及第 02 号文档列出的 25 个页面。Toast stack 只按 llms.txt 的一行描述归纳，未单独读取其页面
- 未能取得正文：[/docs/theming](https://uiarc.dev/docs/theming)、[/docs/motion](https://uiarc.dev/docs/motion)、[/docs/installation](https://uiarc.dev/docs/installation)。这三页返回的 HTML 被站点导航占满并在正文前截断；相关事实改由 `foundation.json`、`docs/ai` 与技能文件互相印证
