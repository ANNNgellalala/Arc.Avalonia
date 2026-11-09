#Requires -Version 7.0
<#
.SYNOPSIS
    R12 手机端 smoke：调用 Gallery 的 `--smoke --mobile`，解析 `pageShows`/`failures`，
    期望 `pageShows >= 320 && failures == 0`；若 Gallery 尚无 `--mobile` 参数或二进制未构建，则 SKIP（exit 0）。

.DESCRIPTION
    参数探测：先扫描 src/Apps/Arc.Avalonia.Gallery/**/*.cs 是否存在 `--mobile` 字面量。
    不存在（T1 尚未落地）或 dll 不存在时，报告 R12 SKIP 并 exit 0，check-all 记为 10/11 + 1 skip。
    存在时启动 `dotnet <Gallery.dll> --smoke --mobile`（默认 300s 超时），解析：
      GALLERY SMOKE: itemShows=… pageShows=… failures=…
    失败（非 0 / failures>0 / pageShows<MinPages / 解析不到 / 超时）→ RED，exit 1。

.PARAMETER DllPath
    Gallery dll 路径；缺省优先 Debug，其次 Release（src/Apps/Arc.Avalonia.Gallery/bin/{Debug,Release}/net10.0/Arc.Avalonia.Gallery.dll）。

.PARAMETER MinPages
    期望最小 pageShows，缺省 320。

.PARAMETER TimeoutSec
    smoke 超时提示秒数（Gallery 自身内置 300s watchdog；本脚本此参数保留用于报告/未来包装）。

.PARAMETER Quiet
    只输出摘要（check-all 使用）。

.PARAMETER OutDir
    报告目录，缺省 <repo>/.recon/gates。

.EXITCODES
    0  GREEN 或 SKIP
    1  RED（mobile smoke 失败/超时/输出不可解析）
    2  无法完成检查

.EXAMPLE
    pwsh -NoProfile tools/mobile-smoke.ps1
    pwsh -NoProfile tools/check-all.ps1   # 通过 R12 行调用
#>
[CmdletBinding()]
param(
    [string]$DllPath = '',
    [int]$MinPages = 320,
    [int]$TimeoutSec = 300,
    [switch]$Quiet,
    [string]$OutDir = ''
)

. (Join-Path $PSScriptRoot 'gates-common.ps1')

$root = Get-GatesRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }
if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }
if ([string]::IsNullOrWhiteSpace($DllPath)) {
    $debugDll = Join-Path $root 'src/Apps/Arc.Avalonia.Gallery/bin/Debug/net10.0/Arc.Avalonia.Gallery.dll'
    $releaseDll = Join-Path $root 'src/Apps/Arc.Avalonia.Gallery/bin/Release/net10.0/Arc.Avalonia.Gallery.dll'
    if (Test-Path -LiteralPath $debugDll) { $DllPath = $debugDll }
    elseif (Test-Path -LiteralPath $releaseDll) { $DllPath = $releaseDll }
    else { $DllPath = $debugDll }
}
elseif (-not [System.IO.Path]::IsPathRooted($DllPath)) {
    $DllPath = Join-Path $root $DllPath
}

$report = New-GatesReport -Gate 'R12' -Name 'mobile-smoke' -Title '手机端 Gallery smoke' `
    -Scope 'src/Apps/Arc.Avalonia.Gallery --smoke --mobile（pageShows >= 320, failures = 0）'
$rule = New-GatesRule -Id 'R12' -Title 'mobile smoke' -Checked 0
[void]$report.rules.Add($rule)
Add-GatesNote -Report $report -Note '参数探测：Gallery 源码出现 --mobile 才实际运行；否则 SKIP（T1 尚未落地时 check-all 显示 10/11 + 1 skip）。'
Add-GatesNote -Report $report -Note 'R12 SKIP 不算红；若已支持 --mobile 但 smoke 失败/超时/输出不可解析 → RED。'

function Set-MobileSkip {
    param([string]$Reason)
    $rule.status = 'SKIP'
    $rule.notes.Add("SKIP: $Reason")
    Add-GatesNote -Report $report -Note "R12 SKIP 原因: $Reason"
    $json = $report | ConvertTo-Json -Depth 12
    Set-Content -LiteralPath (Join-Path $OutDir "mobile-smoke-$stamp.json") -Value $json -Encoding utf8
    Set-Content -LiteralPath (Join-Path $OutDir 'mobile-smoke-latest.json') -Value $json -Encoding utf8
    $md = Format-GatesMarkdown -Report $report
    Set-Content -LiteralPath (Join-Path $OutDir "mobile-smoke-$stamp.md") -Value $md -Encoding utf8
    if (-not $Quiet) { Write-Host "  [R12 mobile-smoke] SKIP: $Reason" }
    exit 0
}

# 1) 参数探测：T1 给 Gallery 加 --mobile 后本脚本自动生效
$gallerySource = Join-Path $root 'src/Apps/Arc.Avalonia.Gallery'
$mobileSupported = $false
if (Test-Path -LiteralPath $gallerySource) {
    foreach ($cs in Get-ChildItem -LiteralPath $gallerySource -Recurse -File -Filter '*.cs') {
        if ((Get-Content -LiteralPath $cs.FullName -Raw -Encoding utf8) -match '--mobile') { $mobileSupported = $true; break }
    }
}
if (-not (Test-Path -LiteralPath $DllPath)) {
    Set-MobileSkip "Gallery dll 不存在（先构建）: $([System.IO.Path]::GetRelativePath($root, $DllPath) -replace '\\', '/')"
}
if (-not $mobileSupported) {
    Set-MobileSkip 'Gallery 源码无 --mobile（T1 尚未落地；参数探测未命中）'
}
# --mobile 在源码里但二进制可能是旧的（并行开发时常见）：dll 早于 Gallery 源码 → SKIP，避免假红。
$dllTime = (Get-Item -LiteralPath $DllPath).LastWriteTime
$newestSource = Get-ChildItem -LiteralPath $gallerySource -Recurse -File -Include '*.cs', '*.axaml' |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -ne $newestSource -and $dllTime -lt $newestSource.LastWriteTime) {
    Set-MobileSkip ("Gallery binary 早于源码（dll={0:HH:mm:ss} < {1}={2:HH:mm:ss}），--mobile 可能未编译；先重建" -f $dllTime, $newestSource.Name, $newestSource.LastWriteTime)
}

# 2) 实跑：dotnet Gallery.dll --smoke --mobile（Gallery 内置 300s smoke 超时；-TimeoutSec 用于提示）
$combined = ''
$timedOut = $false
$exitCode = -1
try {
    $combined = (& dotnet $DllPath --smoke --mobile 2>&1 | Out-String)
    $exitCode = $LASTEXITCODE
}
catch {
    $rule.status = 'ERROR'
    $rule.notes.Add("启动失败: $($_.Exception.Message)")
}

if ($rule.status -ne 'ERROR') {
    $pageMatch = [regex]::Match($combined, 'pageShows=(\d+)')
    $failMatch = [regex]::Match($combined, 'failures=(\d+)')
    $pageShows = if ($pageMatch.Success) { [int]$pageMatch.Groups[1].Value } else { -1 }
    $failures = if ($failMatch.Success) { [int]$failMatch.Groups[1].Value } else { -1 }
    $rule.checked = if ($pageShows -ge 0) { $pageShows } else { 0 }
    $tail = ($combined.Trim() -split "`n" | Select-Object -Last 12) -join "`n"
    $fail = $false
    if ($timedOut) { $fail = $true; $rule.items.Add([pscustomobject]@{ severity = 'new'; kind = 'mobile-smoke-timeout'; project = 'src/Apps/Arc.Avalonia.Gallery'; detail = "mobile smoke 超过 $TimeoutSec s 未退出"; path = '' }) }
    if ($exitCode -ne 0) { $fail = $true; $rule.items.Add([pscustomobject]@{ severity = 'new'; kind = 'mobile-smoke-exit'; project = 'src/Apps/Arc.Avalonia.Gallery'; detail = "exit=$exitCode"; path = '' }) }
    if ($failures -lt 0) { $fail = $true; $rule.items.Add([pscustomobject]@{ severity = 'new'; kind = 'mobile-smoke-unparsable'; project = 'src/Apps/Arc.Avalonia.Gallery'; detail = "输出中无 failures= 计数；tail: $tail"; path = '' }) }
    elseif ($failures -gt 0) { $fail = $true; $rule.items.Add([pscustomobject]@{ severity = 'new'; kind = 'mobile-smoke-failures'; project = 'src/Apps/Arc.Avalonia.Gallery'; detail = "failures=$failures；tail: $tail"; path = '' }) }
    if ($pageShows -lt $MinPages) { $fail = $true; $rule.items.Add([pscustomobject]@{ severity = 'new'; kind = 'mobile-smoke-pages'; project = 'src/Apps/Arc.Avalonia.Gallery'; detail = "pageShows=$pageShows < $MinPages"; path = '' }) }
    $rule.notes.Add("exit=$exitCode pageShows=$pageShows failures=$failures timedOut=$timedOut")
    if ($fail) { $rule.status = 'RED' } else { $rule.status = 'GREEN' }
    if (-not $Quiet) {
        Write-Host "  [R12 mobile-smoke] exit=$exitCode pageShows=$pageShows failures=$failures status=$($rule.status)"
        if ($fail) { Write-Host "  tail: $tail" }
    }
}

if ($rule.status -eq 'GREEN' -or $rule.status -eq 'RED') { Update-GatesRuleStatus -Rule $rule | Out-Null }
exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
