#Requires -Version 7.0
<#
.SYNOPSIS
    R8 无渲染层自研门禁：扫描 src/**/*.cs|*.axaml 中的渲染层实现信号
    （FrameClock / RenderTimer / DrawingCache / ThemeVersion / CompositionTarget.Rendering）；
    新增命中即失败，历史代码需在 tools/render-allow.json 按 file+signal 登记。

.DESCRIPTION
    docs/13 §2.0 / R8：帧调度、绘制缓存、主题资源失效协议属于 Avalonia 渲染层职责，Arc 不做。
    动画用 Avalonia Animation/Transitions，时间用 Dispatcher，取色用 token/资源。
    判定：命中文件的信号若在 render-allow.json 的 historical（file+signal）或 filePatterns 中 → 已登记豁免；
    否则一律新增违规。

.PARAMETER Quiet
    只输出一行摘要（tools/check-all.ps1 使用）。

.PARAMETER OutDir
    报告输出目录，默认 <repo>/.recon/gates。

.EXITCODES
    0  无新增命中（历史豁免不计）
    1  新增命中
    2  无法完成检查

.EXAMPLE
    pwsh -NoProfile tools/check-no-render.ps1
#>
[CmdletBinding()]
param(
    [switch]$Quiet,
    [string]$OutDir = ''
)

. (Join-Path $PSScriptRoot 'gates-common.ps1')

$root = Get-GatesRoot
$allowConfig = Read-GatesJson (Join-Path $PSScriptRoot 'render-allow.json')
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }

$report = New-GatesReport -Gate 'R8' -Name 'no-render' -Title '无渲染层自研' `
    -Scope 'src/**/*.cs, src/**/*.axaml（排除 bin/obj）'
$rule = New-GatesRule -Id 'R8' -Title '不得自研帧时钟/绘制缓存/主题失效协议'
[void]$report.rules.Add($rule)
Add-GatesNote -Report $report -Note '历史豁免: tools/render-allow.json（当前为空数组，M0 实测 0 命中）。'
Add-GatesNote -Report $report -Note '信号: FrameClock / RenderTimer / DrawingCache / ThemeVersion / CompositionTarget.Rendering。'

$signalRegex = [regex]'\b(FrameClock|RenderTimer|DrawingCache|ThemeVersion)\b|CompositionTarget\s*\.\s*Rendering'
$historical = [System.Collections.Generic.List[object]]::new()
foreach ($h in @($allowConfig.historical)) {
    $historical.Add([pscustomobject]@{
            File   = (([string]$h.file) -replace '\\', '/')
            Signal = [string]$h.signal
            Reason = [string]$h.reason
        })
}
$filePatterns = @($allowConfig.filePatterns | ForEach-Object { [string]$_ })

$files = @(Get-GatesSourceFiles -Root (Join-Path $root 'src') -Extensions @('.cs', '.axaml'))
$newCount = 0
$exemptCount = 0
foreach ($file in $files) {
    $rel = ConvertTo-GatesRelPath -Path $file.FullName -Base $root
    $lineNo = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding utf8) {
        $lineNo++
        foreach ($m in $signalRegex.Matches($line)) {
            $signal = $m.Groups[1].Value
            if ([string]::IsNullOrWhiteSpace($signal)) { $signal = 'CompositionTarget.Rendering' }
            $isExempt = $false
            foreach ($h in $historical) {
                if ([string]::Equals($h.File, $rel, [System.StringComparison]::OrdinalIgnoreCase) -and
                    [string]::Equals($h.Signal, $signal, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $isExempt = $true
                    break
                }
            }
            if (-not $isExempt) {
                foreach ($p in $filePatterns) {
                    if (Test-GatesGlobMatch -Name $rel -Pattern $p) { $isExempt = $true; break }
                }
            }
            if ($isExempt) {
                $exemptCount++
                Add-GatesItem -Rule $rule -Severity 'exempt' -Kind 'r8-historical-exempt' -Project $rel -Detail "信号 $signal 已登记豁免（行 $lineNo）"
            }
            else {
                $newCount++
                Add-GatesItem -Rule $rule -Severity 'new' -Kind 'r8-render-signal' -Project $rel -Detail "新增渲染层信号 $signal（行 $lineNo）"
            }
        }
    }
}

$rule.checked = $files.Count
$rule.notes.Add("扫描文件: $($files.Count)；新增命中: $newCount；历史豁免: $exemptCount")
$rule.notes.Add('若确需时间循环，请使用 Avalonia.Threading.DispatcherTimer/Dispatcher，而不是自建 RenderTimer/FrameClock。')
Update-GatesRuleStatus -Rule $rule | Out-Null
exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
