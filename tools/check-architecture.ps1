#Requires -Version 7.0
<#
.SYNOPSIS
    R1 + R2 架构门禁：解析所有 csproj 的 ProjectReference 映射到分层，检查“只能向下依赖”；
    并检查发布库不得引用 Demo/Docs/Samples、不得把 Docs 打进 EmbeddedResource。

.DESCRIPTION
    层映射与允许的目标层读取自 tools/architecture-allow.json（可评审 diff）：
      L1 Foundation → 仅 L0（例外：allowedEdges 里登记的 Internal→Foundation）
      L2 Components → L1/L2
      L2f Feature  → L1/L2
      L2.5 Theme   → L1/L2（docs/13 §2.3 的聚合例外）
      L3 Patterns/Special/Blocks → L1/L2（下行方向不变；见 ADR 0001 例外 E2）
      L4 Agent     → L1/L2/L3
      app Apps/Docs/Tests → 任意
    对 leaf 项目（Demo/Docs/Samples/Tests）的引用由 R2 判定；R2 的当前存量记为
    tools/gates-baseline.json 的 known-red，只有超出基线的引用才失败。

.PARAMETER Quiet
    只输出一行摘要（tools/check-all.ps1 使用）。

.PARAMETER OutDir
    报告输出目录，默认 <repo>/.recon/gates。

.PARAMETER BaselinePath
    known-red 基线文件，默认 tools/gates-baseline.json。CI/自测可用它验证“基线清空后 R2 立即变 RED”。

.EXITCODES
    0  无新增违规（R2 的 24+24 条 known-red 不阻塞）
    1  存在新增违规（分层反向依赖 / 新的 Demo 引用 / 新的 EmbeddedResource Docs）
    2  无法完成检查（csproj 解析失败等）

.EXAMPLE
    pwsh -NoProfile tools/check-architecture.ps1
#>
[CmdletBinding()]
param(
    [switch]$Quiet,
    [string]$OutDir = '',
    [string]$BaselinePath = ''
)

. (Join-Path $PSScriptRoot 'gates-common.ps1')

$root = Get-GatesRoot
$allow = Read-GatesJson (Join-Path $PSScriptRoot 'architecture-allow.json')
if ([string]::IsNullOrWhiteSpace($BaselinePath)) {
    $BaselinePath = Join-Path $PSScriptRoot 'gates-baseline.json'
}
elseif (-not [System.IO.Path]::IsPathRooted($BaselinePath)) {
    $BaselinePath = Join-Path $root $BaselinePath
}
$baseline = Read-GatesJson $BaselinePath
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }

$report = New-GatesReport -Gate 'R1/R2' -Name 'architecture' -Title '分层与发布库边界' `
    -Scope 'src/**/*.csproj + tests/**/*.csproj（ProjectReference 边 + EmbeddedResource）'
$r1 = New-GatesRule -Id 'R1' -Title 'ProjectReference 只能向下'
$r2 = New-GatesRule -Id 'R2' -Title '发布库不引用 Demo/Docs/Samples'
[void]$report.rules.Add($r1)
[void]$report.rules.Add($r2)
Add-GatesNote -Report $report -Note '层映射/允许边: tools/architecture-allow.json；known-red 基线: tools/gates-baseline.json'
Add-GatesNote -Report $report -Note 'leaf 项目（Demo/Docs/Samples/Tests）的引用只在 R2 判；R1 跳过这些边，避免同一问题双报。'

function Get-ConfigLayerTargets {
    param([Parameter(Mandatory)][string]$Layer)
    $prop = $allow.allowedTargetLayers.PSObject.Properties[$Layer]
    if ($null -eq $prop) { return @() }
    return @($prop.Value)
}

function Test-EdgeSide {
    # 一侧的匹配支持三种标识：AssemblyName/工程名（Name）、csproj 文件名（FileBase）、仓库相对路径（Rel）。
    # M1-T2a 后 Foundation 的 AssemblyName/PackageId 是 Arc.Avalonia.Foundation，而 csproj 仍是 Arc.Avalonia，
    # 因此例外必须能按路径或任一名字匹配（见 architecture-allow.json 的 fromProject/toProject）。
    param(
        [Parameter(Mandatory)]$Project,
        [string]$NamePattern = '',
        [string]$PathPattern = ''
    )
    $specified = $false
    if (-not [string]::IsNullOrWhiteSpace($NamePattern)) {
        $specified = $true
        foreach ($candidate in @($Project.Name, $Project.FileBase, $Project.Rel)) {
            if ([string]::Equals([string]$candidate, $NamePattern, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($PathPattern)) {
        $specified = $true
        $suffix = $PathPattern.TrimStart('/')
        if ([string]::Equals($Project.Rel, $suffix, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        if ($Project.Rel.EndsWith($suffix, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return (-not $specified)
}

function Test-ConfigAllowedEdge {
    param([Parameter(Mandatory)]$FromProject, [Parameter(Mandatory)]$ToProject)
    foreach ($edge in @($allow.allowedEdges)) {
        $props = $edge.PSObject.Properties
        $fromName = if ($null -ne $props['from']) { [string]$props['from'].Value } else { '' }
        $toName = if ($null -ne $props['to']) { [string]$props['to'].Value } else { '' }
        $fromPath = if ($null -ne $props['fromProject']) { [string]$props['fromProject'].Value } else { '' }
        $toPath = if ($null -ne $props['toProject']) { [string]$props['toProject'].Value } else { '' }
        if ((Test-EdgeSide -Project $FromProject -NamePattern $fromName -PathPattern $fromPath) -and
            (Test-EdgeSide -Project $ToProject -NamePattern $toName -PathPattern $toPath)) {
            return $true
        }
    }
    return $false
}

function Get-ProjectLayer {
    param([Parameter(Mandatory)][string]$RelPath)
    foreach ($map in @($allow.layerPrefixes)) {
        if ($RelPath.StartsWith([string]$map.prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return [string]$map.layer
        }
    }
    return 'unknown'
}

function Test-LeafProjectName {
    param([string]$Name)
    foreach ($leaf in @($allow.leafProjects)) {
        if ([string]::Equals($Name, [string]$leaf, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Get-R2Kind {
    param([string]$TargetName)
    if ([string]::Equals($TargetName, 'Arc.Avalonia.Demo', [System.StringComparison]::OrdinalIgnoreCase)) { return 'r2-demo-ref' }
    if ($TargetName -like 'Arc.Avalonia.Samples*') { return 'r2-samples-ref' }
    if ($TargetName -like 'Arc.Avalonia.Docs*') { return 'r2-docs-ref' }
    if ($TargetName -like 'Arc.Avalonia.Tests*') { return 'r2-demo-ref' }
    return 'r2-leaf-ref'
}

# ---------------------------------------------------------------------------
# 1) 收集项目与 ProjectReference 边
# ---------------------------------------------------------------------------
$projects = [System.Collections.Generic.List[object]]::new()
$scanRoots = @((Join-Path $root 'src'), (Join-Path $root 'tests'))
foreach ($scanRoot in $scanRoots) {
    if (-not (Test-Path -LiteralPath $scanRoot)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Filter '*.csproj') {
        if ($file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
        $rel = ConvertTo-GatesRelPath -Path $file.FullName -Base $root
        $layer = Get-ProjectLayer -RelPath $rel
        $name = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $refs = [System.Collections.Generic.List[object]]::new()
        $embedded = [System.Collections.Generic.List[string]]::new()
        $parseError = $null
        try {
            [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8
            $assemblyNode = $xml.SelectSingleNode('//Project/PropertyGroup/AssemblyName')
            if ($null -ne $assemblyNode -and -not [string]::IsNullOrWhiteSpace($assemblyNode.InnerText)) {
                $name = $assemblyNode.InnerText.Trim()
            }
            foreach ($ref in @($xml.SelectNodes('//ProjectReference'))) {
                $include = [string]$ref.GetAttribute('Include')
                if ([string]::IsNullOrWhiteSpace($include)) { continue }
                $resolved = $null
                try { $resolved = [System.IO.Path]::GetFullPath((Join-Path $file.DirectoryName $include)) } catch { $resolved = $null }
                $refs.Add([pscustomobject]@{ Include = $include; Resolved = $resolved })
            }
            foreach ($er in @($xml.SelectNodes('//EmbeddedResource'))) {
                $inc = [string]$er.GetAttribute('Include')
                if ([string]::IsNullOrWhiteSpace($inc)) { continue }
                $embedded.Add($inc)
            }
        }
        catch {
            $parseError = $_.Exception.Message
        }
        $projects.Add([pscustomobject]@{
                Path       = $file.FullName
                Rel        = $rel
                Name       = $name
                FileBase   = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
                Layer      = $layer
                Refs       = $refs
                Embedded   = $embedded
                ParseError = $parseError
            })
    }
}
$projects = @($projects | Sort-Object Rel)
$byPath = @{}
foreach ($p in $projects) {
    $byPath[[System.IO.Path]::GetFullPath($p.Path)] = $p
    if ($null -ne $p.ParseError) {
        Add-GatesItem -Rule $r1 -Severity 'new' -Kind 'r1-csproj-parse-error' -Project $p.Rel -Detail $p.ParseError
    }
}

# ---------------------------------------------------------------------------
# 2) R1：逐条 ProjectReference 判层
# ---------------------------------------------------------------------------
$r1Edges = 0
$layerUnknown = 0
foreach ($project in $projects) {
    if ($null -ne $project.ParseError) { continue }
    foreach ($ref in $project.Refs) {
        $target = if ($null -ne $ref.Resolved -and $byPath.ContainsKey($ref.Resolved)) { $byPath[$ref.Resolved] } else { $null }
        if ($null -eq $target) {
            if (Test-LeafProjectName -Name ([System.IO.Path]::GetFileNameWithoutExtension($ref.Include))) { continue }
            Add-GatesItem -Rule $r1 -Severity 'new' -Kind 'r1-unresolved-reference' -Project $project.Rel `
                -Detail "ProjectReference 目标不在扫描集合内: $($ref.Include)"
            continue
        }
        if (Test-LeafProjectName -Name $target.Name) { continue }   # R2 负责
        $r1Edges++
        if ($project.Layer -eq 'unknown' -or $target.Layer -eq 'unknown') {
            $layerUnknown++
            Add-GatesItem -Rule $r1 -Severity 'new' -Kind 'r1-unknown-layer' -Project $project.Rel `
                -Detail "无法映射层: $($project.Name)[$($project.Layer)] -> $($target.Name)[$($target.Layer)]"
            continue
        }
        if ($project.Layer -eq 'app') { continue }
        if (Test-ConfigAllowedEdge -FromProject $project -ToProject $target) { continue }
        $allowedTargets = @(Get-ConfigLayerTargets -Layer $project.Layer)
        if ($allowedTargets -contains '*') { continue }
        if ($allowedTargets -contains $target.Layer) { continue }
        Add-GatesItem -Rule $r1 -Severity 'new' -Kind 'r1-layer-violation' -Project $project.Rel `
            -Detail "反向依赖: $($project.Name) [$($project.Layer)] -> $($target.Name) [$($target.Layer)]"
    }
}
$r1.checked = $r1Edges
$r1.notes.Add("检查 ProjectReference 边: $r1Edges；层映射未知: $layerUnknown")
$r1.notes.Add('L3→L1 直连是 docs/13 §2.1 的显式放宽（见 architecture-allow.json notes 与 ADR 0001 例外 E2），不算违规。')

# ---------------------------------------------------------------------------
# 3) R2：发布库引用 Demo/Docs/Samples + EmbeddedResource Docs
# ---------------------------------------------------------------------------
$publishProjects = @($projects | Where-Object { $_.Layer -ne 'app' -and $null -eq $_.ParseError })
$r2.checked = $publishProjects.Count
$candidates = [System.Collections.Generic.List[object]]::new()
foreach ($project in $publishProjects) {
    foreach ($ref in $project.Refs) {
        $targetName = [System.IO.Path]::GetFileNameWithoutExtension($ref.Include)
        if ($null -ne $ref.Resolved -and $byPath.ContainsKey($ref.Resolved)) { $targetName = $byPath[$ref.Resolved].Name }
        if (-not (Test-LeafProjectName -Name $targetName)) { continue }
        $candidates.Add([pscustomobject]@{
                Project = $project.Rel
                Kind    = (Get-R2Kind -TargetName $targetName)
                Detail  = "ProjectReference -> $targetName"
            })
    }
    foreach ($inc in $project.Embedded) {
        if ($inc -match '(?i)(^|[\\/])(docs|pages)([\\/]|$)') {
            $candidates.Add([pscustomobject]@{
                    Project = $project.Rel
                    Kind    = 'r2-embedded-docs'
                    Detail  = "EmbeddedResource Include=`"$inc`""
                })
        }
    }
}

# 与 known-red 基线比对：项目在 permittedProjects 内且同类计数不超过基线 → knownred，否则 new。
$r2Baseline = $baseline.knownRed.R2
$permitted = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($p in @($r2Baseline.permittedProjects)) { [void]$permitted.Add([string]$p) }
$baselineCounts = @{}
foreach ($prop in $r2Baseline.counts.PSObject.Properties) { $baselineCounts[$prop.Name] = [int]$prop.Value }
$seenCounts = @{}
$sortedCandidates = @($candidates | Sort-Object Project, Kind, Detail)
foreach ($candidate in $sortedCandidates) {
    $kind = $candidate.Kind
    $limit = if ($baselineCounts.ContainsKey($kind)) { $baselineCounts[$kind] } else { 0 }
    if (-not $seenCounts.ContainsKey($kind)) { $seenCounts[$kind] = 0 }
    $seenCounts[$kind]++
    $severity = if ($permitted.Contains($candidate.Project) -and $seenCounts[$kind] -le $limit) { 'knownred' } else { 'new' }
    Add-GatesItem -Rule $r2 -Severity $severity -Kind $kind -Project $candidate.Project -Detail $candidate.Detail
}
$r2.notes.Add("发布库项目数: $($publishProjects.Count)；R2 命中: $($candidates.Count)")
if ($r2Baseline.expected) {
    $r2.notes.Add("known-red 基线: demo 引用 $($baselineCounts['r2-demo-ref']) 个、EmbeddedResource Docs $($baselineCounts['r2-embedded-docs']) 个（M1-T1 出库目标为 0）")
    $r2.notes.Add('仓库口径：引用 Demo 的 src 项目共 25 个（含 Gallery）+ tests 1 个；Gallery/tests 属 app，不属于发布库，故 R2 只报 24 个。')
}
else {
    $r2.notes.Add('R2 known-red 已清零（M1-T1 出库完成）：基线 counts=0、permittedProjects 为空，任何 leaf 引用/EmbeddedResource Docs 都是新增违规，直接 RED。')
    $r2.notes.Add('历史基线：M0 起点为 24 demo 引用 + 24 EmbeddedResource Docs（仓库口径 25/26 含 Gallery/tests，属 app 不计入 R2）。')
}
$cleared = 0
foreach ($kind in $baselineCounts.Keys) {
    $now = if ($seenCounts.ContainsKey($kind)) { $seenCounts[$kind] } else { 0 }
    if ($now -lt $baselineCounts[$kind]) { $cleared += ($baselineCounts[$kind] - $now) }
}
if ($cleared -gt 0) {
    $r2.notes.Add("known-red 已清理 $cleared 项：请同步下调 tools/gates-baseline.json（M1 清完后 R2 应为 GREEN）。")
}

Update-GatesRuleStatus -Rule $r1 | Out-Null
Update-GatesRuleStatus -Rule $r2 | Out-Null
exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
