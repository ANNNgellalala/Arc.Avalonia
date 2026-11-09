#Requires -Version 7.0
<#
.SYNOPSIS
    R11 手机/触控门禁（静态）：检查滚动路径、指针捕获兜底、点击执行时机、滚动条常驻与命中区尺寸。
    目标是把“手机上好不好用”变成可回归的静态断言；不替代真机/模拟器验收。

.DESCRIPTION
    扫描 src/{Foundation,Components,Agent,Special,Blocks}/**/*.cs（排除 Pages/、Docs/、bin/、obj/）：
      a. wheel-only-scroll（红）：出现 PointerWheelChanged 但同文件没有 PointerPressed/PointerMoved/Gestures/GestureRecognizers；
      b. missing-capture-lost（红）：出现 CapturePointer( 但同文件没有 PointerCaptureLost/PointerCanceled；
      c. press-executes-action（P2 warning，exempt）：PointerPressed 后 25 行内直接 RaiseEvent/.Invoke/Execute，
         且同文件没有 Tapped；
      d. scrollbar-always-visible（P2 warning，exempt）：ScrollBarVisibility.Visible 且文件无 desktop-only 标记；
      e. small-hit-target（informational，exempt）：Width/Height 常量 < 40 且同文件有 Tapped/Command。

    红项（a/b）可通过 tools/touch-baseline.json 的过渡 allowlist 降级为 exempt；warning/informational 只统计、
    不计 red、不需要基线。有 new 违规 exit 1。

.PARAMETER Baseline
    过渡基线路径，缺省 tools/touch-baseline.json。

.PARAMETER NoBaseline
    忽略过渡基线（审计模式）。

.PARAMETER UpdateBaseline
    把当前红项写入基线（覆盖），写完 exit 0。

.PARAMETER Quiet
    只输出一行摘要（tools/check-all.ps1 使用）。

.PARAMETER OutDir
    报告目录，缺省 <repo>/.recon/gates。

.EXITCODES
    0  无新增红项（warning/informational 不计）
    1  存在新增红项
    2  无法完成检查

.EXAMPLE
    pwsh -NoProfile tools/check-touch.ps1
    pwsh -NoProfile tools/check-touch.ps1 -UpdateBaseline
#>
[CmdletBinding()]
param(
    [string]$Baseline = '',
    [switch]$NoBaseline,
    [switch]$UpdateBaseline,
    [switch]$Quiet,
    [string]$OutDir = ''
)

. (Join-Path $PSScriptRoot 'gates-common.ps1')

$root = Get-GatesRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }
if ([string]::IsNullOrWhiteSpace($Baseline)) { $Baseline = Join-Path $PSScriptRoot 'touch-baseline.json' }
elseif (-not [System.IO.Path]::IsPathRooted($Baseline)) { $Baseline = Join-Path $root $Baseline }

function Get-CodeMaskedText {
    param([Parameter(Mandatory)][string]$Text)
    $pattern = '(?s)@"(?:""|[^"])*"|"(?:\\.|[^"\\\r\n])*"|''(?:\\.|[^''\\\r\n])*''|//[^\r\n]*|/\*.*?\*/'
    return [regex]::Replace($Text, $pattern, { param($m) [regex]::Replace($m.Value, '[^\r\n]', ' ') })
}

function Get-TouchSymbol {
    param([Parameter(Mandatory)][string]$Text, [Parameter(Mandatory)][string]$RelPath)
    $m = [regex]::Match($Text, '(?m)^[ \t]*(?:(?:public|internal|private|protected|sealed|abstract|partial|static|unsafe|file|new|readonly)\s+)*class\s+([A-Za-z_]\w*)')
    if ($m.Success) { return $m.Groups[1].Value }
    return [System.IO.Path]::GetFileNameWithoutExtension($RelPath)
}

$scanRoots = @('src/Foundation', 'src/Components', 'src/Agent', 'src/Special', 'src/Blocks')
$files = [System.Collections.Generic.List[object]]::new()
foreach ($scanRoot in $scanRoots) {
    $dir = Join-Path $root $scanRoot
    if (-not (Test-Path -LiteralPath $dir)) { continue }
    foreach ($f in Get-GatesSourceFiles -Root $dir -Extensions @('.cs')) {
        $rel = ConvertTo-GatesRelPath -Path $f.FullName -Base $root
        if ($rel -match '(?i)/(Pages|Docs)/') { continue }
        $files.Add([pscustomobject]@{ Path = $f.FullName; Rel = $rel })
    }
}
$files = @($files | Sort-Object Rel)

$redViolations = [System.Collections.Generic.List[object]]::new()
$warnings = [System.Collections.Generic.List[object]]::new()
$metrics = [ordered]@{ scannedFiles = $files.Count; wheelOnlyScroll = 0; missingCaptureLost = 0; pressExecutesAction = 0; scrollbarAlwaysVisible = 0; smallHitTargets = 0; violations = 0 }

foreach ($file in $files) {
    $raw = Get-Content -LiteralPath $file.Path -Raw -Encoding utf8
    if ([string]::IsNullOrWhiteSpace($raw)) { continue }
    $masked = Get-CodeMaskedText -Text $raw
    $symbol = Get-TouchSymbol -Text $masked -RelPath $file.Rel

    # a. 滚动容器必须是“滚轮 + 触摸/手势”双路径，避免手机上没有滚轮就滚不动
    if ($masked -match 'PointerWheelChanged') {
        if ($masked -notmatch 'PointerPressed|PointerMoved|GestureRecognizers|Gestures') {
            $metrics.wheelOnlyScroll++
            $redViolations.Add([pscustomobject]@{ Kind = 'wheel-only-scroll'; File = $file.Rel; Symbol = $symbol; Detail = "$symbol 处理 PointerWheelChanged 但同文件无 PointerPressed/PointerMoved/Gestures 触屏路径" })
        }
    }

    # b. 指针捕获必须有 CaptureLost/Canceled 兜底（手机来电/切后台/手势竞争会丢 capture）
    if ($masked -match 'CapturePointer\s*\(') {
        if ($masked -notmatch 'PointerCaptureLost|PointerCanceled') {
            $metrics.missingCaptureLost++
            $redViolations.Add([pscustomobject]@{ Kind = 'missing-capture-lost'; File = $file.Rel; Symbol = $symbol; Detail = "$symbol 调用 CapturePointer 但无 PointerCaptureLost/PointerCanceled 兜底" })
        }
    }

    # c. PointerPressed 后直接 RaiseEvent/业务 Action：手机上会与滚动/长按冲突（P2 warning）
    if ($masked -match 'PointerPressed' -and $masked -notmatch '\bTapped\b') {
        $lines = $raw -split "`n"
        $pressExecutes = $false
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -notmatch 'PointerPressed') { continue }
            $end = [Math]::Min($i + 25, $lines.Count - 1)
            $window = ($lines[$i..$end] -join "`n")
            if ($window -match 'RaiseEvent\s*\(|\.Invoke\s*\(|Execute\s*\(') { $pressExecutes = $true; break }
        }
        if ($pressExecutes) {
            $metrics.pressExecutesAction++
            $warnings.Add([pscustomobject]@{ Kind = 'press-executes-action'; File = $file.Rel; Symbol = $symbol; Detail = "$symbol 在 PointerPressed 后直接执行动作且无 Tapped（手机滚动/长按易误触发）" })
        }
    }

    # d. 滚动条常驻（P2 warning）：移动端默认应 Overlay/Auto，desktop-only 文件可豁免
    if ($masked -match 'ScrollBarVisibility\.Visible') {
        $isDesktopOnly = ($raw -match '(?i)desktop-only|DesktopOnly') -or ($file.Rel -match '(?i)Desktop')
        if (-not $isDesktopOnly) {
            $metrics.scrollbarAlwaysVisible++
            $warnings.Add([pscustomobject]@{ Kind = 'scrollbar-always-visible'; File = $file.Rel; Symbol = $symbol; Detail = "$symbol 使用 ScrollBarVisibility.Visible（移动端应 Auto/Overlay；如确属桌面专用请标注 desktop-only）" })
        }
    }

    # e. 命中区 < 40px（informational）：供人工复核 44px 触控目标
    if ($masked -match '\bTapped\b|\bCommand\b') {
        $smallValues = [System.Collections.Generic.List[double]]::new()
        foreach ($m in [regex]::Matches($masked, '\b(?:Width|Height)\s*=\s*([0-9]+(?:\.[0-9]+)?)\b')) {
            $v = [double]$m.Groups[1].Value
            if ($v -lt 40) { $smallValues.Add($v) }
        }
        if ($smallValues.Count -gt 0) {
            $metrics.smallHitTargets++
            $unique = @($smallValues | Sort-Object -Unique)
            $warnings.Add([pscustomobject]@{ Kind = 'small-hit-target'; File = $file.Rel; Symbol = $symbol; Detail = "$symbol 存在 Width/Height<40 的常量（$($unique -join ', ')）且含 Tapped/Command；复核 44px 命中区" })
        }
    }
}

$redViolations = @($redViolations | Sort-Object File, Kind, Symbol)
$warnings = @($warnings | Sort-Object File, Kind, Symbol)
$metrics.violations = $redViolations.Count

if ($UpdateBaseline) {
    $allow = @()
    foreach ($v in $redViolations) {
        $allow += [ordered]@{
            kind   = $v.Kind
            file   = $v.File
            symbol = $v.Symbol
            reason = 'T6 手机/触控门禁过渡 allowlist（首跑真实命中；修复后应清空）'
        }
    }
    $baselineObject = [ordered]@{
        '$comment'  = 'R11 触控门禁的过渡基线：只记录红项（wheel-only-scroll / missing-capture-lost），warning/informational 不计入。'
        version     = 1
        generatedAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
        counters    = $metrics
        allow       = $allow
        notes       = @(
            '刷新命令: pwsh -NoProfile tools/check-touch.ps1 -UpdateBaseline',
            '目标是 allow=[]：手机端路径修复后清空。',
            'warning/informational 项（press-executes-action/scrollbar-always-visible/small-hit-target）不入基线。'
        )
    }
    Set-Content -LiteralPath $Baseline -Value ($baselineObject | ConvertTo-Json -Depth 8) -Encoding utf8
    Write-Host "TOUCH BASELINE UPDATED: allow=$($allow.Count) file=$([System.IO.Path]::GetRelativePath($root, $Baseline) -replace '\\', '/')"
    Write-Host "  scanned=$($metrics.scannedFiles) wheelOnly=$($metrics.wheelOnlyScroll) missingCaptureLost=$($metrics.missingCaptureLost) pressExecutes=$($metrics.pressExecutesAction) scrollbarVisible=$($metrics.scrollbarAlwaysVisible) smallHit=$($metrics.smallHitTargets)"
    exit 0
}

$allowKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$baselineLoaded = $false
$staleAllow = 0
if (-not $NoBaseline -and (Test-Path -LiteralPath $Baseline)) {
    try {
        $baselineObject = Read-GatesJson -Path $Baseline
        foreach ($entry in @($baselineObject.allow)) {
            [void]$allowKeys.Add("$([string]$entry.kind)|$([string]$entry.file)|$([string]$entry.symbol)")
        }
        $baselineLoaded = $true
    }
    catch { }
}

$report = New-GatesReport -Gate 'R11' -Name 'touch' -Title '手机/触控静态门禁' `
    -Scope 'src/{Foundation,Components,Agent,Special,Blocks}/**/*.cs（排除 Pages/、Docs/、bin/、obj/）'
$rule = New-GatesRule -Id 'R11' -Title '触控路径/捕获兜底/命中区' -Checked $files.Count
[void]$report.rules.Add($rule)
Add-GatesNote -Report $report -Note "过渡基线: $(if ($baselineLoaded) { '已加载 tools/touch-baseline.json' } elseif ($NoBaseline) { '已禁用（-NoBaseline）' } else { '不存在（无 allowlist）' })"
Add-GatesNote -Report $report -Note 'R11 是静态启发式：a/b 红项可由基线过渡；c/d/e 为 P2 warning/informational，只统计。'
Add-GatesNote -Report $report -Note '移动端最终验收仍以 Gallery --smoke --mobile（R12）与真机/模拟器为准。'

$newCount = 0
$exemptCount = 0
$seenAllow = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($v in $redViolations) {
    $key = "$($v.Kind)|$($v.File)|$($v.Symbol)"
    $severity = if ($allowKeys.Contains($key)) { 'exempt' } else { 'new' }
    if ($severity -eq 'exempt') { $exemptCount++; [void]$seenAllow.Add($key) } else { $newCount++ }
    $rule.items.Add([pscustomobject]@{ severity = $severity; kind = $v.Kind; project = $v.File; detail = $v.Detail; path = $v.File; symbol = $v.Symbol })
}
foreach ($key in @($allowKeys)) { if (-not $seenAllow.Contains($key)) { $staleAllow++ } }
foreach ($w in $warnings) {
    $rule.items.Add([pscustomobject]@{ severity = 'exempt'; kind = $w.Kind; project = $w.File; detail = $w.Detail; path = $w.File; symbol = $w.Symbol })
}
$rule.notes.Add("scannedFiles=$($metrics.scannedFiles) / wheel-only-scroll=$($metrics.wheelOnlyScroll) / missing-capture-lost=$($metrics.missingCaptureLost) / press-executes-action=$($metrics.pressExecutesAction) / scrollbar-always-visible=$($metrics.scrollbarAlwaysVisible) / small-hit-target=$($metrics.smallHitTargets)")
$rule.notes.Add("红项 new=$newCount, exempt=$exemptCount；warning/informational=$($warnings.Count)（不计 red）")
if ($baselineLoaded -and $staleAllow -gt 0) { $rule.notes.Add("基线条目已无命中: $staleAllow（修复后可 -UpdateBaseline 收敛）") }
Update-GatesRuleStatus -Rule $rule | Out-Null
$report | Add-Member -NotePropertyName metrics -NotePropertyValue $metrics
if (-not $Quiet) {
    Write-Host "  metrics: scanned=$($metrics.scannedFiles) wheelOnly=$($metrics.wheelOnlyScroll) missingCaptureLost=$($metrics.missingCaptureLost) pressExecutes=$($metrics.pressExecutesAction) scrollbarVisible=$($metrics.scrollbarAlwaysVisible) smallHit=$($metrics.smallHitTargets)"
}
exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
