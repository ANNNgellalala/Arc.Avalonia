#Requires -Version 7.0
<#
.SYNOPSIS
    R4 主题来源门禁：库内 Themes/**（含 Foundation 的 Theme/）axaml 只能 Include 本地 Arc 资源或官方 Avalonia 主题资源；
    库内任何 axaml 不得出现 <FluentTheme/>、<SimpleTheme/>（主题装配只发生在应用层）。

.DESCRIPTION
    允许的 Source 前缀（硬编码，改动需改本脚本并同步 ADR 0001）：
      avares://Arc.Avalonia…        Arc 自己的程序集资源（Arc.Avalonia / Arc.Avalonia.*）
      avares://Avalonia.Themes…     官方 Avalonia 主题资源
    应用（src/Apps）不参与本检查：Gallery 的 App.axaml 允许并必须写 <FluentTheme/>（M1-T3 后由 ArcTheme 入口接管）。

.PARAMETER Quiet
    只输出一行摘要（tools/check-all.ps1 使用）。

.PARAMETER OutDir
    报告输出目录，默认 <repo>/.recon/gates。

.EXITCODES
    0  合规
    1  新增违规（Include 第三方主题/相对路径，或库内出现 FluentTheme/SimpleTheme）
    2  无法完成检查

.EXAMPLE
    pwsh -NoProfile tools/check-theme.ps1
#>
[CmdletBinding()]
param(
    [switch]$Quiet,
    [string]$OutDir = ''
)

. (Join-Path $PSScriptRoot 'gates-common.ps1')

$root = Get-GatesRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }

$report = New-GatesReport -Gate 'R4' -Name 'theme' -Title '主题来源' `
    -Scope '发布库 src/**/Themes|Theme/*.axaml 的 StyleInclude/ResourceInclude + 库内 FluentTheme/SimpleTheme'
$rule = New-GatesRule -Id 'R4' -Title '主题只做叠加'
[void]$report.rules.Add($rule)
Add-GatesNote -Report $report -Note '允许: avares://Arc.Avalonia… 与 avares://Avalonia.Themes…；应用层（src/Apps）不检查。'
Add-GatesNote -Report $report -Note 'M1-T3 新增 src/Theme/Arc.Avalonia.Theme 后本门禁继续生效（前缀按层目录扫描，不依赖具体项目名）。'

$scanRoots = @('src/Foundation', 'src/Components', 'src/Agent', 'src/Special', 'src/Blocks', 'src/Theme', 'src/Feature', 'src/Patterns')
$axamlFiles = [System.Collections.Generic.List[object]]::new()
foreach ($r in $scanRoots) {
    $dir = Join-Path $root $r
    if (-not (Test-Path -LiteralPath $dir)) { continue }
    foreach ($f in Get-GatesSourceFiles -Root $dir -Extensions @('.axaml')) {
        $rel = ConvertTo-GatesRelPath -Path $f.FullName -Base $root
        $axamlFiles.Add([pscustomobject]@{ Path = $f.FullName; Rel = $rel })
    }
}

$allowedUriPatterns = @(
    '^avares://Arc\.Avalonia',
    '^avares://Avalonia\.Themes\.'
)
$includeRegex = [regex]'(?is)<(?:StyleInclude|ResourceInclude)\b[^>]*?\bSource\s*=\s*(?:"([^"]+)"|''([^'']+)'')'
$themeElementRegex = [regex]'(?i)<\s*(FluentTheme|SimpleTheme)\b'

$themeFileCount = 0
$includeCount = 0
foreach ($file in $axamlFiles) {
    $segments = $file.Rel -split '/'
    $isThemeFile = ($segments -contains 'Themes') -or ($segments -contains 'Theme')
    if ($isThemeFile) {
        $themeFileCount++
        $text = Get-Content -LiteralPath $file.Path -Raw -Encoding utf8
        if ($null -eq $text) { $text = '' }
        foreach ($m in $includeRegex.Matches($text)) {
            $includeCount++
            $source = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
            $ok = $false
            foreach ($pattern in $allowedUriPatterns) {
                if ($source -match $pattern) { $ok = $true; break }
            }
            if (-not $ok) {
                Add-GatesItem -Rule $rule -Severity 'new' -Kind 'r4-theme-include-source' -Project $file.Rel `
                    -Detail "不允许的 Include 来源: $source"
            }
        }
    }
    $textAll = Get-Content -LiteralPath $file.Path -Raw -Encoding utf8
    if ($null -eq $textAll) { $textAll = '' }
    foreach ($m in $themeElementRegex.Matches($textAll)) {
        Add-GatesItem -Rule $rule -Severity 'new' -Kind 'r4-fluent-theme-in-library' -Project $file.Rel `
            -Detail "库内不得出现 <$($m.Groups[1].Value)/>（主题装配属于应用入口/M1 的 ArcTheme）"
    }
}

$rule.checked = $axamlFiles.Count
$rule.notes.Add("扫描库内 axaml: $($axamlFiles.Count) 个；其中主题文件: $themeFileCount 个；Include 来源: $includeCount 条")
$rule.notes.Add('官方主题资源的正确用法：M1 的 ArcTheme 入口在 src/Theme 内聚合 <FluentTheme/>；组件库自己的主题文件只 Include avares://Arc.Avalonia*。')
Update-GatesRuleStatus -Rule $rule | Out-Null
exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
