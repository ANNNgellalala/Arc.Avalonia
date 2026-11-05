# 基线记录：pre-framework（2026-11-05）

> 用途：M0-1 冻结基线。重构（M1）开始前的可复现快照，用于前后对比与回滚。
> 对应计划：[`docs/14-m0-baseline-and-gates.zh.md`](../14-m0-baseline-and-gates.zh.md) §2；架构依据：[`docs/13-framework-layering-and-agent-top.zh.md`](../13-framework-layering-and-agent-top.zh.md) v3.2。

## 1. 环境

| 项 | 值 |
|---|---|
| OS / Shell | Windows / PowerShell 7 |
| .NET SDK | 10.0.114 |
| Avalonia | 12.1.4（`Directory.Build.props`） |
| NuGet | 离线；全局缓存 `D:\.nuget\packages`；`NuGet.config` 源清空 |
| 解决方案 | `ArcUI.slnx`（29 个项目：28 源 + 1 测试控制台） |
| 仓库 HEAD（基线前） | `5c2513f docs: correct P2a project count` |
| 基线 tag | `pre-framework`（指向本文件所在的基线提交） |

## 2. 构建与验证（实测，2026-11-05）

| 检查 | 命令 | 结果 | 判定 |
|---|---|---|---|
| 全量构建 | `pwsh -NoProfile tools/build.ps1 -ExtraArgs '--no-incremental'` | **0 error / 9 warning**（既有 analyzer 提示） | ✅ |
| 自检 | `dotnet tests\Arc.Avalonia.Tests\bin\Debug\net10.0\Arc.Avalonia.Verify.dll` | `VERIFY: checks=34 failures=0` | ✅ |
| 目录审计 | `dotnet src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll --audit` | `CATALOG AUDIT: items=294 docs=294 pages=294 errors=0 warnings=0 pageFailures=0` | ✅ |
| 导航回归 | 同上 `--smoke` | `GALLERY SMOKE: itemShows=294 pageShows=315 failures=0` | ✅ |
| 截图基线 | `shots/`（历史产物，本机 466 张 PNG，浅色 + 深色） | manifest 见 `shots-manifest.csv` | ✅（未重跑全量截图；M1 起按需重跑对比） |

> 说明：`shots/` 在 `.gitignore` 中，不随基线提交；其 SHA256 manifest 已提交，用于 M1 后逐图对比。

## 3. 依赖快照

| 文件 | 内容 |
|---|---|
| `assets-manifest.csv` | 29 个 `obj/project.assets.json` 的路径 + SHA256（重构前依赖闭包快照） |
| `shots-manifest.csv` | 466 张截图的路径 + SHA256 |

复算命令：

```powershell
# 依赖快照
Get-ChildItem src,tests -Recurse -Filter project.assets.json -File |
  Get-FileHash -Algorithm SHA256 | Export-Csv docs/baseline/assets-manifest.csv -NoTypeInformation
# 截图快照
Get-ChildItem shots -Recurse -Filter *.png | Get-FileHash -Algorithm SHA256 |
  Export-Csv docs/baseline/shots-manifest.csv -NoTypeInformation
```

## 4. 基线时的已知状态（M1 要改的）

| 项 | 基线值 | M1 目标 |
|---|---|---|
| 发布库引用 `Arc.Avalonia.Demo` | 25/29 项目 | 0 |
| 库内 `Pages/` + `Docs/` | 320 页 + 294 文档（614 文件） | 全部出库到 `src/Docs` |
| `Arc.Avalonia.Internal` | 12 个 csproj 引用 + 6 处 `InternalsVisibleTo` | 解散 |
| 第三方 UI 库依赖 | 0 | 保持 0（门禁） |
| `PackageId`/`Version`/`License`/`README`/`samples`/`.github` | 0 | 齐备 |
| 内建控件皮肤 | 6 类表面；无 Top 覆盖 | 基础适配 15 + Top 15–20 覆盖 15（M2） |
| 渲染层自研 | 历史代码不追溯 | 新增为 0（R8 门禁） |

## 5. 回滚

```powershell
git checkout pre-framework            # 回到基线提交（工作树干净时）
git switch -c recover pre-framework   # 或从基线开分支
```

M0 期间新增的门禁脚本、POC 与 ADR 不改变产品行为；若 M0 失败，删除 `tools/` 新增文件、`poc/`、`docs/adr/` 即可回到基线语义。
