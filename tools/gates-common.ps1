#Requires -Version 7.0
<#
.SYNOPSIS
    M0 门禁脚本共享工具（R1–R8）。本文件不是入口，由 tools/check-*.ps1 dot-source。

.DESCRIPTION
    提供：仓库根定位、相对路径、JSON 读取、规则/报告对象、Markdown/JSON 报告落盘、
    控制台摘要与退出码约定。所有门禁脚本共用同一套报告 schema，供 tools/check-all.ps1 汇总。

    报告 schema（<name>-latest.json）：
      gate / name / title / status / generatedAt / scope / notes / summary / rules[]
      rules[]: id / title / status / checked / newViolations / knownRed / exemptions / items[] / notes[]
      items[]: severity(new|knownred|exempt) / kind / project / detail / path

    状态三态（+ ERROR）：
      GREEN           无违规（可有已登记豁免）
      KNOWN-RED       只有基线内已知红（tools/gates-baseline.json），不阻塞
      RED             存在新增违规 → 调用脚本 exit 1
      ERROR           无法完成检查（缺文件/解析失败/restore 失败）→ exit 2

    注意：不要用 Set-Content 写带 BOM 的 UTF-8；PowerShell 7 的 -Encoding utf8 无 BOM。
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-GatesRoot {
    return (Split-Path -Parent $PSScriptRoot)
}

function ConvertTo-GatesRelPath {
    param(
        [Parameter(Mandatory)][string]$Path,
        [string]$Base = ''
    )
    if ([string]::IsNullOrWhiteSpace($Base)) { $Base = Get-GatesRoot }
    $full = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $Base $Path }
    $full = [System.IO.Path]::GetFullPath($full)
    return ([System.IO.Path]::GetRelativePath($Base, $full) -replace '\\', '/')
}

function Read-GatesJson {
    param([Parameter(Mandatory)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "gates: missing file $Path" }
    return (Get-Content -LiteralPath $Path -Raw -Encoding utf8 | ConvertFrom-Json)
}

function New-GatesRule {
    param(
        [Parameter(Mandatory)][string]$Id,
        [string]$Title = '',
        [int]$Checked = 0
    )
    return [pscustomobject]@{
        id            = $Id
        title         = $Title
        status        = 'GREEN'
        checked       = $Checked
        newViolations = 0
        knownRed      = 0
        exemptions    = 0
        items         = [System.Collections.Generic.List[object]]::new()
        notes         = [System.Collections.Generic.List[string]]::new()
    }
}

function Add-GatesItem {
    param(
        [Parameter(Mandatory)]$Rule,
        [Parameter(Mandatory)][ValidateSet('new', 'knownred', 'exempt', 'pass')][string]$Severity,
        [Parameter(Mandatory)][string]$Kind,
        [string]$Project = '',
        [string]$Detail = '',
        [string]$Path = '',
        [string]$Tfm = ''
    )
    [void]$Rule.items.Add([pscustomobject]@{
            severity = $Severity
            kind     = $Kind
            project  = $Project
            detail   = $Detail
            path     = $Path
            tfm      = $Tfm
        })
}

function Update-GatesRuleStatus {
    param([Parameter(Mandatory)]$Rule)
    $new = @($Rule.items | Where-Object { $_.severity -eq 'new' })
    $known = @($Rule.items | Where-Object { $_.severity -eq 'knownred' })
    $exempt = @($Rule.items | Where-Object { $_.severity -eq 'exempt' })
    $Rule.newViolations = $new.Count
    $Rule.knownRed = $known.Count
    $Rule.exemptions = $exempt.Count
    $Rule.status = if ($new.Count -gt 0) { 'RED' } elseif ($known.Count -gt 0) { 'KNOWN-RED' } else { 'GREEN' }
    return $Rule
}

function New-GatesReport {
    param(
        [Parameter(Mandatory)][string]$Gate,
        [Parameter(Mandatory)][string]$Name,
        [string]$Title = '',
        [string]$Scope = ''
    )
    return [pscustomobject]@{
        gate        = $Gate
        name        = $Name
        title       = $Title
        scope       = $Scope
        status      = 'GREEN'
        generatedAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
        notes       = [System.Collections.Generic.List[string]]::new()
        rules       = [System.Collections.Generic.List[object]]::new()
        summary     = [pscustomobject]@{
            checked       = 0
            newViolations = 0
            knownRed      = 0
            exemptions    = 0
            errors        = 0
        }
    }
}

function Add-GatesNote {
    param(
        [Parameter(Mandatory)]$Report,
        [Parameter(Mandatory)][string]$Note
    )
    [void]$Report.notes.Add($Note)
}

function Get-GatesExitCode {
    param([Parameter(Mandatory)][string]$Status)
    switch ($Status) {
        'RED' { return 1 }
        'ERROR' { return 2 }
        default { return 0 }
    }
}

function Format-GatesMarkdown {
    param([Parameter(Mandatory)]$Report)
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("# $($Report.gate) $($Report.title) 门禁报告")
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine("- 门禁: ``$($Report.name)``")
    [void]$sb.AppendLine("- 生成时间: $($Report.generatedAt)")
    [void]$sb.AppendLine("- 状态: **$($Report.status)**")
    if (-not [string]::IsNullOrWhiteSpace($Report.scope)) {
        [void]$sb.AppendLine("- 范围: $($Report.scope)")
    }
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('## 汇总')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('| 规则 | 状态 | 检查数 | 新增违规 | known-red | 已登记豁免 |')
    [void]$sb.AppendLine('|---|---|---:|---:|---:|---:|')
    foreach ($rule in $Report.rules) {
        [void]$sb.AppendLine("| $($rule.id) $($rule.title) | $($rule.status) | $($rule.checked) | $($rule.newViolations) | $($rule.knownRed) | $($rule.exemptions) |")
    }
    [void]$sb.AppendLine('')
    foreach ($rule in $Report.rules) {
        [void]$sb.AppendLine("## $($rule.id) 明细")
        [void]$sb.AppendLine('')
        if ($rule.items.Count -eq 0) {
            [void]$sb.AppendLine('（无）')
        }
        else {
            foreach ($item in $rule.items) {
                $parts = @("[$($item.severity)]", $item.kind)
                if (-not [string]::IsNullOrWhiteSpace($item.project)) { $parts += $item.project }
                if (-not [string]::IsNullOrWhiteSpace($item.detail)) { $parts += "— $($item.detail)" }
                if (-not [string]::IsNullOrWhiteSpace($item.path)) { $parts += "`n  path: $($item.path)" }
                [void]$sb.AppendLine(('- ' + ($parts -join ' ')))
            }
        }
        [void]$sb.AppendLine('')
        if ($rule.notes.Count -gt 0) {
            foreach ($n in $rule.notes) { [void]$sb.AppendLine("> $n") }
            [void]$sb.AppendLine('')
        }
    }
    if ($Report.notes.Count -gt 0) {
        [void]$sb.AppendLine('## 说明')
        [void]$sb.AppendLine('')
        foreach ($n in $Report.notes) { [void]$sb.AppendLine("- $n") }
        [void]$sb.AppendLine('')
    }
    return $sb.ToString()
}

function Complete-GatesReport {
    param(
        [Parameter(Mandatory)]$Report,
        [Parameter(Mandatory)][string]$OutDir,
        [Parameter(Mandatory)][string]$Stamp,
        [switch]$Quiet
    )
    $hasError = @($Report.rules | Where-Object { $_.status -eq 'ERROR' }).Count -gt 0
    $hasRed = @($Report.rules | Where-Object { $_.status -eq 'RED' }).Count -gt 0
    $hasKnown = @($Report.rules | Where-Object { $_.status -eq 'KNOWN-RED' }).Count -gt 0
    $hasSkip = @($Report.rules | Where-Object { $_.status -eq 'SKIP' }).Count -gt 0
    $Report.status = if ($hasError) { 'ERROR' } elseif ($hasRed) { 'RED' } elseif ($hasKnown) { 'KNOWN-RED' } elseif ($hasSkip) { 'SKIP' } else { 'GREEN' }

    $Report.summary.checked = [int](@($Report.rules | Measure-Object -Property checked -Sum).Sum)
    $Report.summary.newViolations = [int](@($Report.rules | Measure-Object -Property newViolations -Sum).Sum)
    $Report.summary.knownRed = [int](@($Report.rules | Measure-Object -Property knownRed -Sum).Sum)
    $Report.summary.exemptions = [int](@($Report.rules | Measure-Object -Property exemptions -Sum).Sum)

    if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }
    $jsonName = "$($Report.name)-$Stamp.json"
    $latestName = "$($Report.name)-latest.json"
    $mdName = "$($Report.name)-$Stamp.md"
    $json = $Report | ConvertTo-Json -Depth 12
    Set-Content -LiteralPath (Join-Path $OutDir $jsonName) -Value $json -Encoding utf8
    Set-Content -LiteralPath (Join-Path $OutDir $latestName) -Value $json -Encoding utf8
    $md = Format-GatesMarkdown -Report $Report
    Set-Content -LiteralPath (Join-Path $OutDir $mdName) -Value $md -Encoding utf8

    if (-not $Quiet) {
        $line = "[$($Report.gate) $($Report.name)] $($Report.status) checked=$($Report.summary.checked)" +
            " new=$($Report.summary.newViolations) knownRed=$($Report.summary.knownRed)" +
            " exempt=$($Report.summary.exemptions)"
        Write-Host $line
        foreach ($rule in $Report.rules) {
            if ($rule.status -eq 'RED' -or $rule.status -eq 'ERROR') {
                foreach ($item in @($rule.items | Where-Object { $_.severity -eq 'new' })) {
                    $detail = if ([string]::IsNullOrWhiteSpace($item.detail)) { '' } else { " — $($item.detail)" }
                    Write-Host "  NEW [$($rule.id)] $($item.kind): $($item.project)$detail"
                }
            }
        }
        Write-Host "  report: $([System.IO.Path]::GetRelativePath((Get-GatesRoot), (Join-Path $OutDir $mdName)) -replace '\\', '/')"
    }
    return (Get-GatesExitCode -Status $Report.status)
}

function Get-GatesSourceFiles {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string[]]$Extensions
    )
    if (-not (Test-Path -LiteralPath $Root)) { return @() }
    return @(Get-ChildItem -LiteralPath $Root -Recurse -File |
            Where-Object {
                $_.Extension -in $Extensions -and
                $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
            })
}

function Test-GatesGlobMatch {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Pattern
    )
    $regex = '^' + ([regex]::Escape($Pattern) -replace '\\\*', '.*') + '$'
    return [bool]([regex]::IsMatch($Name, $regex, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase))
}

function Test-GatesAnyGlob {
    param(
        [Parameter(Mandatory)][string]$Name,
        [string[]]$Patterns = @()
    )
    foreach ($p in $Patterns) {
        if (Test-GatesGlobMatch -Name $Name -Pattern $p) { return $true }
    }
    return $false
}
