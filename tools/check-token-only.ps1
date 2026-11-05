#Requires -Version 7.0
<#
.SYNOPSIS
    R6 token-only 门禁：扫描 src/**/*.cs|*.axaml 中的 #RRGGBB(#RRGGBBAA)、rgb()/rgba() 字面量、Color.FromArgb；
    对照 tools/token-allow.json 的存量豁免，新增命中或超出该文件登记行数即失败。

.DESCRIPTION
    判定粒度是“文件 × 规则 × 命中行数”：
      - 未登记的文件/规则命中 → 新增违规；
      - 已登记但命中行数 > max → 新增违规（防止在老文件里继续堆硬编码）；
      - 命中行数 ≤ max → 已登记豁免（不阻塞）。
    token 定义源头（ArcPalette）、颜色引擎（ArcColor）、取色器颜色数学、自绘 alpha 派生
    属于 docs/14 §4 允许的既有例外，详见 tools/token-allow.json 的 reason。

.PARAMETER Quiet
    只输出一行摘要（tools/check-all.ps1 使用）。

.PARAMETER OutDir
    报告输出目录，默认 <repo>/.recon/gates。

.EXITCODES
    0  无新增违规（可能带有已登记豁免）
    1  新增违规
    2  无法完成检查

.EXAMPLE
    pwsh -NoProfile tools/check-token-only.ps1
#>
[CmdletBinding()]
param(
    [switch]$Quiet,
    [string]$OutDir = ''
)

. (Join-Path $PSScriptRoot 'gates-common.ps1')

$root = Get-GatesRoot
$allowConfig = Read-GatesJson (Join-Path $PSScriptRoot 'token-allow.json')
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }

$report = New-GatesReport -Gate 'R6' -Name 'token-only' -Title 'token-only 取色' `
    -Scope 'src/**/*.cs, src/**/*.axaml（排除 bin/obj）'
$rule = New-GatesRule -Id 'R6' -Title '不得出现 hex/rgb/Color.FromArgb'
[void]$report.rules.Add($rule)
Add-GatesNote -Report $report -Note '存量豁免: tools/token-allow.json（file+rule+max 行数）；新文件/超量命中 fail-closed。'
Add-GatesNote -Report $report -Note '允许的取色方式: ArcResources.Brush(...)/ArcResources.Color(...)、{DynamicResource ...}、ArcTokens 语义 token。'

$patterns = [ordered]@{
    hex      = '#[0-9A-Fa-f]{6,8}\b'
    rgb      = '(?<![A-Za-z0-9_])rgba?\s*\('
    fromargb = 'Color\.FromArgb'
}

# allowMap: "file|rule" → @{ max; reason }
$allowMap = @{}
$allowEntries = [System.Collections.Generic.List[object]]::new()
foreach ($entry in @($allowConfig.allow)) {
    $file = ([string]$entry.file) -replace '\\', '/'
    $ruleName = [string]$entry.rule
    $key = "$file|$ruleName"
    $allowMap[$key] = @{ max = [int]$entry.max; reason = [string]$entry.reason }
    $allowEntries.Add([pscustomobject]@{ File = $file; Rule = $ruleName; Max = [int]$entry.max; Reason = [string]$entry.reason })
}

$files = @(Get-GatesSourceFiles -Root (Join-Path $root 'src') -Extensions @('.cs', '.axaml'))
$hits = @{}   # "file|rule" → List[int] 行号
foreach ($file in $files) {
    $rel = ConvertTo-GatesRelPath -Path $file.FullName -Base $root
    $lineNo = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding utf8) {
        $lineNo++
        foreach ($ruleName in $patterns.Keys) {
            $pattern = $patterns[$ruleName]
            $matched = if ($ruleName -eq 'rgb') { $line -cmatch $pattern } else { $line -match $pattern }
            if (-not $matched) { continue }
            $key = "$rel|$ruleName"
            if (-not $hits.ContainsKey($key)) { $hits[$key] = [System.Collections.Generic.List[int]]::new() }
            $hits[$key].Add($lineNo)
        }
    }
}

$newCount = 0
$exemptCount = 0
foreach ($key in @($hits.Keys | Sort-Object)) {
    $parts = $key -split '\|', 2
    $file = $parts[0]
    $ruleName = $parts[1]
    $lineNumbers = @($hits[$key])
    $found = $lineNumbers.Count
    $firstLines = ($lineNumbers | Select-Object -First 5) -join ', '
    if (-not $allowMap.ContainsKey($key)) {
        $newCount++
        Add-GatesItem -Rule $rule -Severity 'new' -Kind 'r6-unguarded-color' -Project $file `
            -Detail "规则 $ruleName 命中 $found 行（行 $firstLines）；新文件/新规则必须走 token 或登记 token-allow.json"
        continue
    }
    $allowed = $allowMap[$key]
    if ($found -gt $allowed.max) {
        $newCount++
        Add-GatesItem -Rule $rule -Severity 'new' -Kind 'r6-exceeds-baseline' -Project $file `
            -Detail "规则 $ruleName 命中 $found 行 > 基线 $($allowed.max)（行 $firstLines）"
    }
    else {
        $exemptCount++
        Add-GatesItem -Rule $rule -Severity 'exempt' -Kind 'r6-baseline-exempt' -Project $file `
            -Detail "规则 $ruleName 命中 $found/$($allowed.max) 行（已登记）"
    }
}

# 基线里登记但当前 0 命中的条目：提示清理，不失败。
$stale = 0
foreach ($entry in $allowEntries) {
    $key = "$($entry.File)|$($entry.Rule)"
    if (-not $hits.ContainsKey($key)) {
        $stale++
        $rule.notes.Add("基线条目已无命中，建议清理: $($entry.File) [$($entry.Rule)]")
    }
}

$rule.checked = $files.Count
$rule.notes.Add("扫描文件: $($files.Count)；规则: $($patterns.Keys -join ', ')；登记豁免: $($allowEntries.Count) 条")
$rule.notes.Add("本次命中：新增 $newCount 条、已登记豁免 $exemptCount 条、基线零命中 $stale 条")
$rule.notes.Add('M1-T1 出库后 Pages 豁免已迁移到 src/Docs/Arc.Avalonia.Docs；规则收紧（如禁用 FromArgb）由 M1-T5 评审决定。')
Update-GatesRuleStatus -Rule $rule | Out-Null
exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
