# Arc 组件规格摘要（25 个代表组件）

本文从官方组件 markdown 页面提取事实，供 Avalonia 复刻时对照。口径说明三点：只列关键属性，不是 API 全集；属性名与默认值保持原文，方便回查；Data grid 属 Pro 条目，这里只记录公开文档中的 API 与行为，不进入复刻范围。每条按用途、何时用与何时不用、关键 props、键盘与无障碍、动效要点组织，动效数值取自各页面的 Motion 段，统一预设见 [motion.md](https://uiarc.dev/r/skills/arc/motion.md)。

## 1. Button 按钮

- **用途**：页面、表单与对话框里的单一行动。默认 `variant="primary"`，同一表面只应有一个 primary。
- **何时用·何时不用**：用于任何单次行动，尤其是标签会就地变化（Save 到 Saved）而宽度应弹性过渡的场合，以及短异步只需按钮内 spinner 的场合。需要按钮自己展示 pending 与 success 的异步提交改用 action-button；一个主行动带少数备选用 split-button；需要深思的删除用 hold-to-confirm。
- **关键 props**：`variant`（primary、secondary、ghost、danger，默认 primary）、`size`（sm、md、lg，默认 md）、`loading`（显示 spinner、设 aria-busy、吞掉点击但保留焦点）；其余原生 button 属性透传。
- **键盘与无障碍**：Enter 与 Space 触发；渲染原生 button，角色与焦点原生可得；`loading` 用 aria-busy 与 aria-disabled 代替 disabled，键盘焦点不会中途丢失；纯图标按钮必须给 aria-label；禁用态按 variant 区分，次级与危险按钮保留边框改静音填充，幽灵按钮只改文字色。
- **动效要点**：按压缩到约 0.97，走 snappy 弹簧，图标尺寸按钮压得更深；标签变化时宽度弹簧到新尺寸，新旧文字带轻微 blur 交叉淡化；作为浮层锚点时按压缩放自动关闭；减少动效时去掉缩放，只做普通淡化。

## 2. Input 输入框

- **用途**：单行文本，标签、帮助文案与错误文案随输入就地改写。
- **何时用·何时不用**：用于姓名、邮箱、URL 等单行值，也用于错误与帮助文案需要逐字改写的字段，并可直接参与原生表单提交。多行用 textarea；有特定形状的值用 password-field、search-field 或 number-field；展示为页面文字再编辑用 inline-edit。
- **关键 props**：`label`（必填，可见标签并通过 htmlFor 绑定）、`description`（帮助文案，接入 aria-describedby）、`error`（错误文案，设 aria-invalid 并以 role="alert" 行渲染）；其余原生 input 属性透传，含 name、type、value、onChange。
- **键盘与无障碍**：原生 input 加真实 label，表单行为原生；帮助与错误的 id 会与调用方传入的 aria-describedby 合并；错误同时设 aria-invalid 并播报，动画文字对读屏隐藏、另留纯文本副本。
- **动效要点**：帮助行与错误行的高度用 smooth 弹簧展开，随后变化的词句升起去模糊，数字逐位滚动；只有变化的词参与动画；减少动效时整行以全高直接挂载，词句瞬时淡化。

## 3. Select 选择器

- **用途**：短固定列表的单选，触发器内的值按列表方向滚动进出。
- **何时用·何时不用**：用于地区、排序等不需要打字的短列表，以及需要原生表单提交的字段（Radix 提供隐藏 select）。长列表或需要搜索用 combobox；多值用 multi-select；2 到 4 个应常驻可见的选项用 segmented-control。
- **关键 props**：`label`（必填）、`options`（必填，`{ value, label, disabled? }`）、`placeholder`（默认 Select an option）、`description`、`id`、`className`；其余为 Radix Select 根属性，含 value、defaultValue、onValueChange、name、required、disabled，ref 落在触发按钮。
- **键盘与无障碍**：Enter、Space、ArrowDown 打开；上下键在可用项间移动；Enter 或 Space 选中并关闭；Escape 关闭且不改值；输入字母跳到下一个以该字母开头的项（Radix typeahead）。真实值放在视觉隐藏的 Radix Value 里，动画副本对读屏隐藏。
- **动效要点**：后一项从下方升起、前一项向上落下，都带柔和模糊；减少动效时值只做瞬时交叉淡化；菜单宽度跟随触发器，上限 min(24rem, 100vw 减 20px)，超高后滚动；触发器按压缩放关闭，保证 Radix 量到稳定锚点。

## 4. Combobox 组合框

- **用途**：可搜索的单选字段，打字过滤，列表高度跟随结果变化。
- **何时用·何时不用**：列表很长且用户知道名字（时区、国家），或需要别名匹配（keywords）时用。短列表且打字无收益用 select；多值用 multi-select；搜索用于跳转而非设值用 expanding-search。
- **关键 props**：`label`（必填，同时作为列表名）、`options`（必填，含 `keywords` 参与匹配）、`value`、`defaultValue`（默认空串）、`onValueChange`、`description`、`placeholder`（默认 Search or select…）、`emptyMessage`（默认 No matches found）。
- **键盘与无障碍**：上下键打开并移动、到端环绕；Enter 选中当前项；Escape 关闭并丢弃查询。输入框带 role="combobox"、aria-expanded、aria-controls、aria-autocomplete="list"、aria-activedescendant；选项为 role="option" 并带 aria-selected 与 aria-disabled；空结果是一行 role="status"；清除按钮名为 Clear selection。
- **动效要点**：浮层从略上方弹入，列表高度随过滤走 smooth 弹簧；选中标签带轻微模糊升入字段，清除按钮缩放进入；减少动效时只保留透明度淡入与瞬时高度变化。列表上限 min(300px, 40vh)，越界滚动收敛，点击外部关闭。

## 5. Checkbox 复选框

- **用途**：需要提交才生效的二值选择，支持半选。
- **何时用·何时不用**：用于同意条款这类独立开关，或父级行显示部分选中。立即生效的设置改用 switch；只能选一个用 radio-group；频繁切换的筛选分面用 chip-group。
- **关键 props**：`label`、`description`、`checked`（boolean 或 "indeterminate"）、`defaultChecked`（默认 false）、`onCheckedChange`；其余为 Radix Checkbox 根属性，含 name、value、required、disabled。
- **键盘与无障碍**：Space 切换。Radix 渲染带 role="checkbox" 的按钮，aria-checked 包含 mixed；label 是真实元素并按 id 绑定，description 走 aria-describedby；绘制的勾对读屏隐藏。
- **动效要点**：填充以 snappy 弹簧缩放进入，同时对勾做路径描绘；切到半选时同一路径变形为横杠；减少动效时所有状态变化瞬时完成。命中区域是整个控件高度的方块，绘制出的方框更小但易点。

## 6. Switch 开关

- **用途**：立即生效的设置项，拇指按压时拉伸、释放后弹到位。
- **何时用·何时不用**：用于通知开关这类翻转即生效的设置，也用于每行一个偏好的设置列表。等待提交的选择用 checkbox；在具名选项间切换用 segmented-control；明暗外观用 theme-switch。
- **关键 props**：`label`（显示在开关内，未传 aria-label 时兼作可访问名）、`checked`、`defaultChecked`（默认 false）、`onCheckedChange`；其余为 Radix Switch 根属性，含 name、disabled、aria-label，指针与键盘处理器是链式而非替换。
- **键盘与无障碍**：Space 与 Enter 切换；按住 Space 时拇指持续拉伸到松开。Radix 渲染 role="switch" 与 aria-checked；只有图标时必须给 aria-label。
- **动效要点**：按下时拇指像被手指按住一样朝另一侧拉伸，释放后走 snappy 弹簧越过；减少动效时移除拉伸，拇指瞬移，同时关闭 CSS 过渡。动效全部是 CSS 过渡，无观察者、无逐帧计算；整体保持控件高度，整段标签都是点击区。

## 7. Segmented control 分段控件

- **用途**：2 到 5 个互斥短视图的切换，选中块在段间滑动。
- **何时用·何时不用**：用于 Day、Week、Month 这类时间范围，或立即生效的布局与模式切换。每个选项要换整块面板时用 tabs；表单里需要描述的单选用 radio-group；单个开关用 switch。
- **关键 props**：`options`（必填，含 `accessory` 可放徽标）、`value`（必填，受控）、`onValueChange`（必填）、`label`（作为组名）、`onOptionIntent`（指针或焦点提前到达时预加载）。默认导出。
- **键盘与无障碍**：Tab 进入控件并落在选中段，再 Tab 离开；方向键选中并聚焦前后段，到端环绕；Home 与 End 跳到首尾。根为 role="group" 并由 label 命名，段是原生按钮加 aria-pressed，只有选中段是 Tab 停靠点；滑动块对读屏隐藏。
- **动效要点**：共享 layoutId 的选中块走 morph 弹簧滑到目标段，每个实例用 LayoutGroup 限定作用域；减少动效时瞬时移动并关闭按钮过渡。行内布局、最大宽度 100%，段放不下时横向滚动且隐藏滚动条，段本身不缩不换行。

## 8. Slider 滑块

- **用途**：单值或双拇指区间的近似取值，带刻度、数值气泡与滚动读数。
- **何时用·何时不用**：用于音量、透明度、价格区间这类近似值，以及需要双拇指、带标签刻度与格式化读数的场景。精确数字用 number-field；展示不可改的进度用 progress。
- **关键 props**：`label`（必填）、`value`（数字或二元组，二元组即区间）、`onValueChange`（每步触发）、`onValueCommit`（松开或按键后一次）、`min`、`max`、`step`、`largeStep`（默认区间十分之一）、`marks`、`minStepsBetweenThumbs`、`format`、`showValue`、`thumbLabels`、`start`、`end`、`name`、`disabled`。
- **键盘与无障碍**：左右上下键按 step 增减；Shift 加方向键或 PageUp、PageDown 走 largeStep；Home 与 End 到该拇指允许的最值。每个拇指是 role="slider"，带 aria-valuemin、aria-valuemax、aria-valuenow、aria-valuetext（来自 format）与 aria-orientation；读数、气泡与刻度对读屏隐藏，值由拇指承载。
- **动效要点**：松手后带一点动量再吸附；气泡在活动拇指上方弹入，读数数字滚动；到边界时拇指朝按键方向“较劲”再弹回；减少动效时去掉动量、较劲与滚动并关闭 CSS 过渡。触控用 pan-y，横向拖动改值、纵向滑动仍滚页面；粗指针下气泡抬高避免挡手。

## 9. Rating 评分

- **用途**：1 到 N 的星级评价，支持半星、擦洗、清除与滚动数值标签。
- **何时用·何时不用**：用于评论与反馈表单，或展示平均分（readOnly 加 allowHalf），也可通过 name 参与原生表单。连续值用 slider；内容上的表情反馈用 reactions；带标签的选项而非刻度用 radio-group。
- **关键 props**：`label`（必填，组名）、`value`、`defaultValue`（默认 0）、`onChange`、`max`（默认 5）、`readOnly`、`disabled`、`description`、`size`、`allowHalf`、`allowClear`（默认 true）、`showValue`、`valueLabels`（五整星默认 Poor 到 Excellent）、`emptyLabel`（默认 Not rated）、`name`。
- **键盘与无障碍**：方向键增减一步（allowHalf 时为半星）；Home 与 End 到最低最高；数字 1 到 9 直接设为该星数；Backspace 与 Delete 在 allowClear 时清除。交互态是 role="radiogroup" 加每步一个 role="radio"，用巡游 Tab 停在已选值；每个单选读作 “Great, 4 of 5 stars”，提交经 polite 区域播报；readOnly 渲染 role="img" 并给 aria-label。
- **动效要点**：填充沿值变化方向逐星扫过并带小错峰，提交时星星成波地弹一下；按下星压到 0.86，数值词按变化方向滚动；减少动效时填充瞬时、去掉弹跳、按压缩放与词滚动。星星落在 36px 单元里，窄屏时数值标签折到星星下方。

## 10. Tabs 标签页

- **用途**：同一对象的相关视图切换，选中块滑动、面板高度过渡。
- **何时用·何时不用**：用于 Overview、Activity、Settings 这类同级视图，或面板高度不同、容器应在两者间弹动，以及需要横向滚动与边缘按钮的长标签集。与面板无关的紧凑取值切换用 segmented-control；按顺序阅读的堆叠段落用 accordion；路由级导航用 liquid-tab-bar 或 morph-nav。
- **关键 props**：根 `value`、`defaultValue`、`onValueChange`；TabsList 支持 aria-label 与 loop；TabsTrigger 的 `value` 必填；TabsContent 的 `value` 必填，`forceMount` 为 true 时保持挂载并跳过进出动画。
- **键盘与无障碍**：左右键在标签间移动并激活；Home 与 End 到首尾；Tab 从标签列进入当前面板。Radix 提供 tablist、tab、tabpanel 与 aria-selected、aria-controls；退出的面板在淡出期间设为 inert，无法取焦；溢出滚动按钮名为 Scroll tabs left 与 Scroll tabs right。
- **动效要点**：选中块在触发器之间走 morph 弹簧滑动，作用域限于一个 Tabs 实例；面板沿行进方向平移 8px，同时高度从上一面板弹到新高度；减少动效时选中块瞬移、面板原地交叉淡化且不对高度做动画。触发器最小宽度 5.5rem，所以标签多时手机上更早进入溢出。

## 11. Accordion 手风琴

- **用途**：单开的问题与答案列表，就地展开支持信息。
- **何时用·何时不用**：用于 FAQ（同一时间只开一个答案）、把长内容收在短问题下的设置或帮助页，`size="lg"` 用于页面级 FAQ。单个独立展开（套餐或订单摘要）用 expandable-card；同级视图切换用 tabs；行是待办任务用 onboarding-checklist。只支持单开模式。
- **关键 props**：`items`（必填，`{ title, content }`，title 即触发器标签）、`defaultOpen`（默认 0，传 -1 全部关闭）、`size`（md 或 lg）。content 是普通数据，可直接放富 ReactNode。
- **键盘与无障碍**：Enter 与 Space 切换当前行；上下键在触发器间移动；Home 与 End 到首尾。Radix 接好 aria-expanded、aria-controls 与面板区域标注；关闭的面板保持挂载，收起后置为 visibility hidden 从而退出无障碍树；触发器位于标题元素内，箭头对读屏隐藏。
- **动效要点**：面板高度零过冲地弹动，答案从轻微模糊下滑 6px 进入，箭头走 snappy 弹簧旋转；飞行中再次切换会从当前高度重定向而不是重播；减少动效时一步到位。关闭面板仍留在 DOM，内容很重时要在面板内懒加载；lg 的答案上限 62ch，520px 以下行高降到 68px。

## 12. Dialog 对话框

- **用途**：必须打断的决策与短表单，遮罩加居中面板。
- **何时用·何时不用**：用于删除确认这类必须打断的决策、重命名或邀请这类能放进一块面板的短表单，以及标题在步骤间变化应就地交叉淡化的流程。长表单或要保持页面上下文的详情用 drawer；手机优先的任务用 bottom-sheet；轻量非模态内容用 popover。
- **关键 props**：根 `open`、`defaultOpen`、`onOpenChange`；DialogTrigger 用 asChild 包裹自己的按钮；DialogContent 的 `title` 必填（打开状态下变化会交叉淡化）、`description`、`children` 必填；DialogClose 用于自定义取消或确认。
- **键盘与无障碍**：Escape 关闭并把焦点还给触发器；Tab 与 Shift+Tab 在对话框内循环。Radix 渲染 role="dialog" 与 aria-modal，困住焦点并在关闭时归还；title 与 description 接到 aria-labelledby 与 aria-describedby；关闭按钮名为 Close dialog。
- **动效要点**：遮罩淡入，面板上升 8px 并从 0.96 缩放，走 smooth 弹簧；关闭更短且从当前状态重定向；标题与描述变化带轻微模糊升起；减少动效时退化为纯透明度淡出。面板宽度 min(100vw 减 32px 沟槽, 440px)，高度超出视口时内部滚动；遮罩 7px 背景模糊在低端设备上可能掉帧。

## 13. Drawer 抽屉

- **用途**：侧面临时面板，承载筛选、设置或记录详情，保持页面上下文。
- **何时用·何时不用**：用于放不进对话框的长表单、筛选与详情面板，可从任意边进入（side），标题栏兼作拖拽手柄。短决策与确认用 dialog；手机优先且带吸附高度的用 bottom-sheet；不需要模态遮罩的小内容用 popover。
- **关键 props**：根 `open`、`defaultOpen`、`onOpenChange`；DrawerTrigger 用 asChild；DrawerContent 的 `title` 必填、`description`、`side`（left、right、top、bottom，默认 right）、`children` 必填；DrawerClose 用于额外关闭按钮。
- **键盘与无障碍**：Escape 关闭并归还焦点；Tab 与 Shift+Tab 在面板内循环。Radix Dialog 提供 role="dialog"、aria-modal、焦点陷阱与归还；标题与描述始终接好，面板永远有名字；标题栏关闭按钮名为 Close drawer，拖拽只是可选路径，绝不是唯一关闭方式。
- **动效要点**：面板从所贴边弹入，离开用更快的补间；拖过面板三分之一或快速甩动即关闭，并保留松手速度；向远离边缘拖动有橡皮筋，遮罩同步淡入淡出；减少动效时禁用拖拽，用短淡入代替滑入。左右面板宽度 min(30rem, 100vw 减沟槽)，40rem 以下接近全宽；遮罩 4px 背景模糊。

## 14. Popover 浮层

- **用途**：点击打开、锚定触发器的小型非模态面板，可放交互内容。
- **何时用·何时不用**：用于分享设置、小型筛选表单等点击打开且可与页面其余部分同时操作的场景，或自建选择器。短悬停标签用 tooltip；只读预览用 hover-card；必须阻断页面的选择用 dialog；命令列表用 dropdown-menu。
- **关键 props**：根 `open`、`defaultOpen`（默认 false）、`onOpenChange`、`modal`（默认 false，为 true 时困住焦点）；PopoverTrigger 自动退出按压缩放，避免面板位移；PopoverContent 的 `align`（默认 start）、`sideOffset`（默认 6）、`collisionPadding`（默认 10）；另有 PopoverClose。
- **键盘与无障碍**：Enter 与 Space 从触发器打开；Escape 关闭并把焦点还给触发器。Radix 在触发器上设 aria-expanded、aria-controls、aria-haspopup="dialog"；打开时焦点移入内容，关闭时归位；默认非模态，内部有控件时应给标题或 aria-label。
- **动效要点**：面板淡入并从朝触发器方向的 5px、0.97 缩放落定，离开 140ms；用过渡而非关键帧，所以关闭中途重开会从当前位置反向；减少动效时去掉位移只留短淡入。面板最小 12rem、最大 min(22rem, 100vw 减 20px)，碰撞处理默认距视口 10px 并自动翻边。

## 15. Tooltip 工具提示

- **用途**：不熟悉控件的一行补充说明，悬停或键盘聚焦出现。
- **何时用·何时不用**：用于工具栏纯图标按钮的标签、被截断文本的全文，以及图标密集时从一个移到另一个应立刻显示。带链接或控件的内容用 popover；人物与链接的富预览用 hover-card；触屏上必须看到的信息不要放进 tooltip。
- **关键 props**：`content`（必填，字符串或数字变化时会交叉淡化并调整尺寸）、`children`（必填，必须接受 ref 且可聚焦）、`side`（top 或 bottom，默认 top）。每个 Tooltip 自带 provider，不需要应用级包裹。
- **键盘与无障碍**：Tab 聚焦触发器即打开；Escape 关闭。Radix 用 aria-describedby 关联内容并渲染 role="tooltip"；内容只是补充，纯图标触发器仍需自己的 aria-label；内部不得放交互元素。
- **动效要点**：250ms 后出现，从 0.97 缩放上升 3px；与另一个 tooltip 相隔 300ms 内则立即出现且只淡入；已打开时字符串变化带模糊升起，气泡弹到新尺寸；减少动效时去掉位移保留 90ms 淡入。气泡上限 15rem，靠近视口边缘由 Radix 翻边。

## 16. Toast 轻提示

- **用途**：后台动作完成后的简短确认，可滑动关闭。
- **何时用·何时不用**：用于单条成功确认（Changes saved），由本地状态控制，或不需要队列的演示与小应用。应用级、带类型、操作、更新与堆叠的通知用 toast-stack；需要留在页面流里的持续消息用 alert。前台动作不要只靠 toast 确认。
- **关键 props**：`title`（必填，显示中变化会交叉淡化）、`description`、`open`（默认 true，关闭后再次置 true 可重新升起）、`onOpenChange`（关闭、滑动或超时回调 false，只有提供它才自动消失）、`duration`（默认 4500ms，小于等于 0 关闭计时）。默认导出。
- **键盘与无障碍**：Tab 到关闭按钮后 Enter 激活。渲染 role="status" 与 aria-live="polite"、aria-atomic，新内容会被播报但不抢焦点；关闭按钮名为 Dismiss notification，对勾图标对读屏隐藏；定位由调用方负责，自动消失计时不因悬停暂停，所以文案要短。
- **动效要点**：从 0.96 缩放上升 16px 进场，走 morph 弹簧，同时对勾描绘；横向滑动超过 80px 或速度超过 480px/s 即按松手速度甩出；关闭时下沉 8px 并淡出；文案变化时升起且高度弹到合适；减少动效时只做透明度淡入并禁用拖拽。宽度 min(100%, 26rem)，标题单行省略。

## 17. Alert 提示条

- **用途**：页面流内的持续消息，帮助恢复或继续。
- **何时用·何时不用**：用于过期卡片这类页面或表单相关且需要常驻的消息、随时间变化应在同一实例上变色的状态，以及可关闭且关闭后下方内容收拢的提示。动作的短暂结果用 toast 或 toast-stack；无内容可展示用 empty-state；必须先回应才能继续用 dialog。
- **关键 props**：`title`（必填，新标题会覆盖旧标题升起）、`tone`（info、success、warning、danger，默认 info）、`children`（标题下的细节，字符串变化时交叉淡化）、`open`（控制存在，隐藏时收高度并淡出）、`onDismiss`（提供即显示关闭按钮）。
- **键盘与无障碍**：关闭按钮存在时用 Enter 或 Space 激活。danger 用 role="alert" 打断播报，其余 tone 用 role="status" 礼貌播报；淡出中的旧文案对读屏隐藏，live region 只读当前文本；关闭按钮名为 Dismiss: 标题，tone 图标对读屏隐藏。
- **动效要点**：高度用 smooth 弹簧收起或展开并配合淡出，下方内容自然合拢；tone 变化时图标经过小幅缩放与模糊变形；文案变化就地升起同时高度弹到合适；减少动效时只做短淡入且不动高度。填充容器宽度、文案换行、图标与关闭按钮尺寸固定；每页只保留少量 alert。

## 18. Progress 进度条

- **用途**：已知任务的确定进度，填充与百分比数字一致。
- **何时用·何时不用**：用于上传、导入、长任务等可报告百分比的场景，`showValue` 可显示计数百分比与完成对勾。内容加载且没有进度可报用 skeleton；配额对上限用 usage-meter；仪表盘指标用 gauge；多步流程位置用 stepper。
- **关键 props**：`value`（默认 0，夹在 0 与 max 之间）、`max`（默认 100）、`label`（可见标签兼 aria-label，缺省时辅助技术读作 Progress）、`showValue`（默认 false）。
- **键盘与无障碍**：无键盘交互。渲染 role="progressbar" 并带 aria-valuemin、aria-valuemax、aria-valuenow 与百分比 aria-valuetext；label 变化交叉淡化，淡出中的副本对读屏隐藏；它不是 live region，完成状态重要时需另行播报。
- **动效要点**：同一个 smooth 弹簧同时驱动填充与数字，两者永远一致；填充从左滑入以保住圆角端；100% 时填充转为成功色，数字旁落定一个对勾；减少动效时填充与计数直接跳到新值。条形填满容器宽度，卡片、行与全宽布局通用。

## 19. Stepper 步骤条

- **用途**：多步流程中的位置与已完成状态，横向或纵向。
- **何时用·何时不用**：用于结账、账户设置这类严格多步流程，可通过 onStepSelect 允许跳回已完成步骤，也支持带描述与错误态的纵向时间线。还需要步骤内容、导航与成功态时用 multi-step-form；松散任务用 onboarding-checklist；单一百分比够用时用 progress。
- **关键 props**：`steps`（必填，`{ id, label, description?, error? }`，error 会把标记变成警告并替换描述）、`current`（必填，`steps.length` 表示全部完成）、`orientation`（默认 horizontal）、`onStepSelect`、`details`（all 或 current，默认 all）、`compact`（默认 false）、`label`（默认 Progress）、`completeLabel`（默认 All steps complete）。
- **键盘与无障碍**：有 onStepSelect 时方向键在可达步骤间移动焦点（跟随 RTL）；Home 与 End 到首步或当前步；Enter 与 Space 选择已完成步骤。可交互时渲染带名字的 nav，否则 role="group"；当前步带 aria-current="step"；每步追加读屏专用状态（Completed、Not started、Error）；步骤变化经 polite 区域播报为 “Step 2 of 4: Shipping”；未到的步骤 aria-disabled 并移出 Tab 序列。
- **动效要点**：进度一次跳多步时连接线依次填充，随后新当前标记外环生长；数字变形为描绘出的对勾或警告，描述升起且槽位弹到新高度；减少动效时全部瞬时生效。容器宽度低于 30rem 时横向步骤条自动切成只留标记加一行说明。

## 20. Avatar 头像

- **用途**：人物与账户的紧凑身份标记，缺图时回退到首字母，可带在线点。
- **何时用·何时不用**：用于姓名、评论、记录旁的单个人，或在页头与列表里表示在线状态，以及照片可能缺失而应以首字母代替的场合。一组人加溢出计数用 avatar-group；头像是账户菜单触发器时用 user-menu。
- **关键 props**：`name`（必填，用于可访问名与前两个首字母）、`src`（图片地址，失败回退首字母，可通过 ArcProvider 换成 next/image 一类组件）、`size`（sm、md、lg、xl，直径 28 到 88px，默认 md）、`status`（online 或 offline，附在可访问名后）。
- **键盘与无障碍**：无键盘交互。根为 role="img"，aria-label 是姓名加状态，例如 “Maya Chen, online”；图片与首字母是装饰，姓名只读一次；状态点对辅助技术隐藏，状态由标签承载。
- **动效要点**：仍在加载的照片带轻微模糊淡入，已缓存则立即显示；状态点以 snappy 弹簧弹入弹出；减少动效时状态点瞬时切换并去掉图片淡入。尺寸只由 size 决定，不随断点变化，并按尺寸给 sizes 提示避免手机下载过大照片。

## 21. Badge 徽标

- **用途**：状态、类别或元数据的小标签，文案与图标就地变形。
- **何时用·何时不用**：用于标题旁或表格单元里的 Live、Draft、Failed 这类短状态、需要就地变形的计数，以及给一行打一个 tone 加可选图标。需要整句的消息用 alert 或 toast；用户可切换的取值用 chip-group；带趋势的主数字用 stat-card。
- **关键 props**：`tone`（neutral、success、info、warning、danger，默认 neutral）、`size`（sm 或 md，默认 md）、`icon`（前置图标，换图标时交叉淡化）、`children`（字符串或数字获得滚动换字，其他节点原样渲染）。
- **键盘与无障碍**：无键盘交互。渲染普通 span，随周围文本内联朗读；图标对读屏隐藏，标签必须自己说明状态而不能只靠颜色；淡出中的旧标签对辅助技术隐藏，只读当前文本；它不播报变化，状态更新需要被听见时应放进 live region。
- **动效要点**：新标签带短模糊升起、旧标签抬走，胶囊宽度弹到合适；字体替换这类被动重排瞬时改变，只有内容变化才走弹簧；减少动效时快速淡化并直接改宽度。胶囊按标签自适应且不换行，窄单元格里控制在一两个词；每行一个徽标尚可，一张表里成千上万个不合适。

## 22. Card 卡片

- **用途**：相关内容与动作的容器，可长成快速查看对话框。
- **何时用·何时不用**：用于网格里的项目、商品、文章等可浏览条目，也用于应打开为快速查看而不离开页面的条目（details），或带媒体、作者署名与就地更新状态行的内容。卡片内就地展开用 expandable-card；数字用 stat-card 或 metric-card；没有卡片可生长时用 dialog。
- **关键 props**：`title`（必填，设 details 后成为对话框触发器）、`description`、`media`（顶部图片，悬停轻微放大）、`action`、`avatar`、`meta`、`status`（变化时逐词滚入并被礼貌播报）、`details`（快速查看内容，设置后卡片打开为 Radix 对话框）、`open`、`defaultOpen`、`onOpenChange`、`children`。
- **键盘与无障碍**：标题上 Enter 与 Space 打开快速查看；Escape 关闭并把焦点还给标题；打开期间 Tab 困在内部。渲染 article 加 h3，快速查看用 Radix Dialog 并把标题与描述接为标签；只有标题变成按钮，嵌套动作仍可分别聚焦；状态变化经 role="status" 区域读出；媒体图要有 alt 或空 alt，纯图标动作要有 aria-label。
- **动效要点**：悬停抬升 2px 并缓慢放大媒体；快速查看与卡片共享 layout id，表面、图片、标题与署名在同一个弹簧上飞行，细节随后淡入；减少动效时去掉抬升、放大与变形，对话框只做淡入。快速查看面板宽度 min(30rem, 100vw 减沟槽)，高度封顶视口；抬升与媒体放大只对鼠标生效。

## 23. Metric card 指标卡

- **用途**：需要一个数字加上下文的紧凑摘要，数字像里程表一样滚动。
- **何时用·何时不用**：用于单一纯数字（正常运行时间、订单数），也用于实时更新且应朝变化方向滚动的数字。需要预格式化字符串、趋势箭头、tone 与图标时用 stat-card；趋势形状比最新值更重要时用 sparkline；不带卡片的数字用 animated-counter。
- **关键 props**：`label`（必填，度量名称）、`value`（必填，数字，首次进入从零滚上来，之后在值之间滚动）、`suffix`（如 % 或 ms）、`context`（必填，数字下一行，如 vs last week）、`change`（右上角小片，如 +12%，省略即隐藏）。
- **键盘与无障碍**：无键盘交互。渲染 article，数字通过视觉隐藏副本按纯文本读出；变化小片本身是纯文本，方向要写进文案（加号或减号），不能只靠颜色；它不播报更新，数值在被注视时变化应外包 live region。
- **动效要点**：数字走 animated-counter，进入视口时滚动，并按数值移动方向决定滚动方向；标签、上下文与变化文案从数字移动的方向滚入，小片宽度弹到合适；减少动效时数字瞬跳、文案快速淡化。380px 以下内边距收紧，变化小片折到标签下方；文案行保持单行并裁切，所以标签与上下文要短。

## 24. Line chart 折线图

- **用途**：多序列折线，滑动十字线、图例开关，切换区间时路径变形而不是重绘。
- **何时用·何时不用**：用于仪表盘卡片里跨天或跨周的流量、收入、延迟，对比本期与上期或目标（虚线序列），以及切换 7d、30d、90d 时应平滑变形的时间序列。无轴无提示的极小趋势用 sparkline；离散类别或分桶计数用 bar-chart；份额问题用 donut-chart。
- **关键 props**：`data`（必填，`{ key, label, axisLabel?, values }`，缺失值按 0）、`series`（必填，`{ key, label, color?, dashed?, area? }`，首序列 area 默认 true）、`label`（必填，度量名）、`unit`、`height`（默认 220，宽度跟随容器）、`formatValue`、`formatTick`、`hiddenSeries`、`defaultHiddenSeries`、`onHiddenSeriesChange`、`onActiveChange`（十字线移动回调，可驱动标题数字）、`loading`、`emptyLabel`（默认 No data for this range）、`legend`、`categoryLabel`（默认 Date）、`curve`（smooth 或 linear，默认 smooth）。
- **键盘与无障碍**：左右与上下键把十字线移到前后点，从静止开始时落在最新点；PageUp 与 PageDown 跳约六分之一区间；Home 与 End 到首尾；Escape 放开十字线。figure 由 label 命名，绘图区是 role="slider"，aria-valuetext 读出日期与所有可见序列值；提供视觉隐藏的区间摘要与逐点数据表；图例开关是带 aria-pressed 的按钮；loading 为 true 时隐藏的 live region 读 Loading；SVG 与提示框对读屏隐藏，避免数值读两遍。
- **动效要点**：首次滚动进入视口时从左到右描绘；新数据时每条线从屏幕上的旧形状按共享 x 采样变形，数值刻度弹到新区间、网格线滑动；隐藏的序列压平到基线并淡出；十字线在点间滑行，提示框以自己的弹簧跟随并在边缘翻面；减少动效时跳过描绘、变形与滑行并停止加载脉冲。宽度由 ResizeObserver 跟随容器，轴标签稀化到每 76px 一个；触摸拖动用指针捕获、松手释放，touch-action 为 pan-y。

## 25. Data grid 数据网格

- **用途**：像电子表格一样就地编辑大量数据，支持区域选择、填充柄、剪贴板与动画排序。Pro 条目，源码不公开，以下只记录公开文档中的 API 面，用于理解交互规格。
- **何时用·何时不用**：用于预算、价目表、库存这类要成片改值的场景，需要与 Excel 或 Google Sheets 互相复制粘贴，需要区域选择、填充、撤销，需要过滤、排序、分组小计与合计的大表（可达数万行），或数据量大到必须按块从服务端取。只读记录加排序用 sortable-data-table；一次改一条记录用表单；每条记录需要图片或长文用卡片列表或网格。
- **关键 props**：`columns` 与 `label` 必填；行数据 `rows` 或 `defaultRows` 配 `onRowsChange`；视图状态都可受控或非受控，包括 `sort`、`filters`、`selectedRowIds`、`hiddenColumns`、`pinnedColumns`、`density`（compact、standard、comfortable 对应 32、40、48px 行高）、`groupBy`；另有 `maxHeight`（默认 400）、`rowSelection`、`rowNumbers`、`selectionStats`、`toolbar`、`totals`、`exportFormats`（默认 csv，可加 xlsx）、`dataSource`（按块取数，带 AbortSignal，最多缓存 40 块）、`bulkActions`、`canDeleteRows`、`exportFileName`、`loading`、`emptyMessage`（默认 No rows yet）。列定义里 `key` 与 `label` 必填，`type` 可取 text、number、currency、percent、select、date，另有 `width`、`minWidth`、`editable`、`options`、`aggregate`、`sortable`、`filterable`、`groupable`、`pinned`、`hidden`。
- **键盘与无障碍**：方向键移动活动单元，Shift 加方向键扩展区域，Cmd 或 Ctrl 加方向键跳到行列首尾，Home 与 End 到行首列，加修饰键到整表首尾，PageUp 与 PageDown 翻页，Cmd 或 Ctrl 加 A 全选，Ctrl 加 Space 扩到整列，Shift 加 Space 勾选区域内行；Enter 或 F2 进入编辑，直接打字则替换，编辑中 Enter 或 Tab 提交并按方向移动，Escape 取消；Alt 加下箭头在选择单元打开选项、在日期单元打开日历；Cmd 或 Ctrl 加 D 向下填充，加 C、X、V 复制剪切粘贴，加 Z 撤销，加重做；Shift 点击表头追加排序键，Alt 加左右箭头调整列宽。网格是一个可聚焦的 role="grid"，带 aria-multiselectable、aria-rowcount、aria-colcount 与 aria-busy，活动单元通过 aria-activedescendant 暴露而不移动焦点；行列带 aria-rowindex 与 aria-colindex；表头是 columnheader 加 aria-sort；编辑、排序、勾选、粘贴、填充、撤销与重做都进 polite live region；开启分组后变为 role="treegrid"，组行带 aria-level 与 aria-expanded。
- **动效要点**：选择矩形与活动单元在区域间走 snappy 弹簧，滚动时立即跟随，调整列宽时直接吸附；排序后行用 700ms 弹簧滑到新位置，从屏幕外进来的行淡入；粘贴、填充、撤销与重做过的单元闪一层洗色并在 0.9s 内淡出；工具栏切换为批量操作时带短位移、淡化与模糊；非法编辑抖一下；减少动效时选择瞬移，去掉行滑动、抖动、按压缩放、箭头旋转与骨架脉冲，只保留短淡化。行与列都虚拟化，筛选输入用 useDeferredValue，编辑不会立刻重排或重新过滤，避免行在光标下跳动。

## 来源与缺口

25 个组件的全部事实来自对应 markdown 页面，例如 [button](https://uiarc.dev/components/button/markdown)、[input](https://uiarc.dev/components/input/markdown)、[select](https://uiarc.dev/components/select/markdown)、[combobox](https://uiarc.dev/components/combobox/markdown)、[checkbox](https://uiarc.dev/components/checkbox/markdown)、[switch](https://uiarc.dev/components/switch/markdown)、[segmented-control](https://uiarc.dev/components/segmented-control/markdown)、[slider](https://uiarc.dev/components/slider/markdown)、[rating](https://uiarc.dev/components/rating/markdown)、[tabs](https://uiarc.dev/components/tabs/markdown)、[accordion](https://uiarc.dev/components/accordion/markdown)、[dialog](https://uiarc.dev/components/dialog/markdown)、[drawer](https://uiarc.dev/components/drawer/markdown)、[popover](https://uiarc.dev/components/popover/markdown)、[tooltip](https://uiarc.dev/components/tooltip/markdown)、[toast](https://uiarc.dev/components/toast/markdown)、[alert](https://uiarc.dev/components/alert/markdown)、[progress](https://uiarc.dev/components/progress/markdown)、[stepper](https://uiarc.dev/components/stepper/markdown)、[avatar](https://uiarc.dev/components/avatar/markdown)、[badge](https://uiarc.dev/components/badge/markdown)、[card](https://uiarc.dev/components/card/markdown)、[metric-card](https://uiarc.dev/components/metric-card/markdown)、[line-chart](https://uiarc.dev/components/line-chart/markdown)、[data-grid](https://uiarc.dev/components/data-grid/markdown)。这 25 页全部读取成功，没有凭推测补写的条目。
