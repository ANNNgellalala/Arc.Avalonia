#Requires -Version 7.0
<#
.SYNOPSIS
    一键门禁：依次运行 R3 依赖、R1/R2 架构、R4 主题、R6 token-only、R8 无渲染层自研、R9 Agent 边界、
    R10 控件契约，以及 R5 打包产物检查，输出红/绿/known-red 三态汇总表。只有“新增违规”才 exit 1。

.DESCRIPTION
    各子门禁先写 .recon/gates/<name>-latest.json，再由本脚本汇总；子门禁的 exit code 只用于兜底。
    三态定义：
      GREEN      无违规（可有已登记豁免，如 R6 的 28 条存量 token 例外、R10 的过渡 allowlist）
      KNOWN-RED  仅基线内已知红（tools/gates-baseline.json；M1 目标）
      RED        新增违规（或子门禁自身 ERROR）
    R5（dotnet pack 产物）由 check-package.ps1 实现（artifacts/ 未生成时为 RED，先 dotnet pack）。
    R9（Agent 边界）主门禁是 BannedApiAnalyzers（RS0030 → error），check-agent-boundary.ps1 是离线兜底；
    其报告是简化 schema（gate/status/issues），本脚本单独兼容，不进入 gates-baseline 的 known-red。
    R10（控件契约）的过渡 allowlist 在 tools/contracts-baseline.json（切片改造中途由 -UpdateBaseline 刷新），
    未完成项降级为 exempt；新增契约违规照常 RED，不进入 gates-baseline。

.PARAMETER KnownRed
    严格模式：把 KNOWN-RED 也视为失败（exit 1）。默认 known-red 不阻塞（docs/14 §9 的 M0 预期状态）。

.PARAMETER SelfTest
    额外运行 tools/check-deps.ps1 -SelfTest（黑名单直接/传递负例注入），结果作为 R3-ST 行展示；
    自检失败同样 exit 1。

.PARAMETER Gate
    只跑指定门禁：R1 / R2 / R3 / R4 / R5 / R6 / R8 / R9 / R10 / selftest（大小写不敏感，前缀匹配）。

.PARAMETER Quiet
    不打印汇总表（仅用于脚本化调用；仍写报告）。

.PARAMETER OutDir
    报告输出目录，默认 <repo>/.recon/gates。

.EXITCODES
    0  全部为 GREEN 或已知红（且未开 -KnownRed）
    1  存在 RED/ERROR，或 -KnownRed 下存在 KNOWN-RED
    2  汇总自身失败（报告不可读等）

.EXAMPLE
    pwsh -NoProfile tools/check-all.ps1
    pwsh -NoProfile tools/check-all.ps1 -SelfTest
    pwsh -NoProfile tools/check-all.ps1 -KnownRed   # M1 严格门禁：known-red 也会失败
#>
[CmdletBinding()]
param(
    [switch]$KnownRed,
    [switch]$SelfTest,
    [string]$Gate = '',
    [switch]$Quiet,
    [string]$OutDir = ''
)

. (Join-Path $PSScriptRoot 'gates-common.ps1')

$root = Get-GatesRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }
if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }
$baseline = Read-GatesJson (Join-Path $PSScriptRoot 'gates-baseline.json')

$gateDefs = @(
    [pscustomobject]@{ Rule = 'R3'; Name = 'deps'; Script = 'check-deps.ps1'; Args = @() },
    [pscustomobject]@{ Rule = 'R1/R2'; Name = 'architecture'; Script = 'check-architecture.ps1'; Args = @() },
    [pscustomobject]@{ Rule = 'R4'; Name = 'theme'; Script = 'check-theme.ps1'; Args = @() },
    [pscustomobject]@{ Rule = 'R6'; Name = 'token-only'; Script = 'check-token-only.ps1'; Args = @() },
    [pscustomobject]@{ Rule = 'R8'; Name = 'no-render'; Script = 'check-no-render.ps1'; Args = @() },
    [pscustomobject]@{ Rule = 'R9'; Name = 'agent-boundary'; Script = 'check-agent-boundary.ps1'; Args = @() },
    [pscustomobject]@{ Rule = 'R10'; Name = 'contracts'; Script = 'check-contracts.ps1'; Args = @() }
)
if ($SelfTest) {
    $gateDefs += [pscustomobject]@{ Rule = 'R3-ST'; Name = 'deps-selftest'; Script = 'check-deps.ps1'; Args = @('-SelfTest') }
}

function Test-GateFilter {
    param([string]$Rule, [string]$Name)
    if ([string]::IsNullOrWhiteSpace($Gate)) { return $true }
    $filter = $Gate.Trim()
    if ($Rule.StartsWith($filter, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($Name.StartsWith($filter, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    if ($filter -eq 'selftest' -and $Name -eq 'deps-selftest') { return $true }
    return $false
}

$rows = [System.Collections.Generic.List[object]]::new()
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($def in $gateDefs) {
    if (-not (Test-GateFilter -Rule $def.Rule -Name $def.Name)) { continue }
    $scriptPath = Join-Path $PSScriptRoot $def.Script
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        $errors.Add("缺少脚本: $($def.Script)")
        $rows.Add([pscustomobject]@{ Rule = $def.Rule; Status = 'ERROR'; New = 0; KnownRed = 0; Exempt = 0; Checked = 0; Report = '' })
        continue
    }
    $childArgs = @('-NoProfile', '-File', $scriptPath, '-Quiet') + @($def.Args)
    & pwsh @childArgs | Out-Host
    $childExit = $LASTEXITCODE
    $latestPath = Join-Path $OutDir "$($def.Name)-latest.json"
    if (-not (Test-Path -LiteralPath $latestPath)) {
        $errors.Add("$($def.Name): 未生成报告 $latestPath（子门禁 exit=$childExit）")
        $rows.Add([pscustomobject]@{ Rule = $def.Rule; Status = 'ERROR'; New = 0; KnownRed = 0; Exempt = 0; Checked = 0; Report = '' })
        continue
    }
    try {
        $childReport = Get-Content -LiteralPath $latestPath -Raw -Encoding utf8 | ConvertFrom-Json
    }
    catch {
        $errors.Add("$($def.Name): 报告解析失败: $($_.Exception.Message)")
        $rows.Add([pscustomobject]@{ Rule = $def.Rule; Status = 'ERROR'; New = 0; KnownRed = 0; Exempt = 0; Checked = 0; Report = '' })
        continue
    }
    $ruleResults = @()
    if ($null -ne $childReport.PSObject.Properties['rules']) { $ruleResults = @($childReport.rules) }
    if ($ruleResults.Count -gt 0) {
        foreach ($ruleResult in $ruleResults) {
            $rows.Add([pscustomobject]@{
                    Rule     = [string]$ruleResult.id
                    Status   = [string]$ruleResult.status
                    New      = [int]$ruleResult.newViolations
                    KnownRed = [int]$ruleResult.knownRed
                    Exempt   = [int]$ruleResult.exemptions
                    Checked  = [int]$ruleResult.checked
                    Report   = ConvertTo-GatesRelPath -Path $latestPath -Base $root
                })
        }
    }
    elseif ($null -ne $childReport.PSObject.Properties['status']) {
        # 兼容 check-agent-boundary.ps1 的简化 schema（gate/status/issues，无 rules 数组）。
        $fallbackStatus = [string]$childReport.status
        if ($fallbackStatus -ne 'GREEN' -and $fallbackStatus -ne 'KNOWN-RED') { $fallbackStatus = 'RED' }
        $issueCount = if ($null -ne $childReport.PSObject.Properties['issues']) { @($childReport.issues).Count } else { 0 }
        $rows.Add([pscustomobject]@{
                Rule     = [string]$def.Rule
                Status   = $fallbackStatus
                New      = $issueCount
                KnownRed = 0
                Exempt   = 0
                Checked  = 0
                Report   = ConvertTo-GatesRelPath -Path $latestPath -Base $root
            })
    }
    else {
        $errors.Add("$($def.Name): 报告格式不可识别（既无 rules 也无 status）")
        $rows.Add([pscustomobject]@{ Rule = $def.Rule; Status = 'ERROR'; New = 0; KnownRed = 0; Exempt = 0; Checked = 0; Report = '' })
    }
}

# R5：dotnet pack 产物检查（M1-T4/T5 实现）。check-package.ps1 产出 gates 兼容的 package-latest.json。
if (Test-GateFilter -Rule 'R5' -Name 'pack') {
    $packageScript = Join-Path $PSScriptRoot 'check-package.ps1'
    $packageJson = Join-Path $OutDir 'package-latest.json'
    if (Test-Path $packageScript) {
        & $packageScript -Quiet *> $null
    }
    if (Test-Path $packageJson) {
        $packageResult = Read-GatesJson $packageJson
        $r5 = @($packageResult.rules | Where-Object { $_.id -eq 'R5' }) | Select-Object -First 1
        $rows.Add([pscustomobject]@{
                Rule     = 'R5'
                Status   = if ($r5 -and $r5.status -eq 'GREEN') { 'GREEN' } else { 'RED' }
                New      = if ($r5) { [int]$r5.newViolations } else { 1 }
                KnownRed = 0
                Exempt   = 0
                Checked  = if ($r5) { [int]$r5.checked } else { 0 }
                Report   = ConvertTo-GatesRelPath -Path $packageJson -Base $root
            })
    }
    else {
        $rows.Add([pscustomobject]@{
                Rule     = 'R5'
                Status   = 'RED'
                New      = 1
                KnownRed = 0
                Exempt   = 0
                Checked  = 0
                Report   = '(artifacts/ 未生成：先 dotnet pack ArcUI.slnx -c Release -o artifacts -m:1 -nodeReuse:false)'
            })
    }
}

$redRows = @($rows | Where-Object { $_.Status -eq 'RED' -or $_.Status -eq 'ERROR' })
$knownRows = @($rows | Where-Object { $_.Status -eq 'KNOWN-RED' })
$overall = if ($errors.Count -gt 0) { 'ERROR' } elseif ($redRows.Count -gt 0) { 'RED' } elseif ($knownRows.Count -gt 0) { 'KNOWN-RED' } else { 'GREEN' }

# ---------------------------------------------------------------------------
# 报告（Markdown + JSON）与汇总表
# ---------------------------------------------------------------------------
$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine('# M0 门禁汇总（check-all）')
[void]$md.AppendLine('')
[void]$md.AppendLine("- 生成时间: $((Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz'))")
[void]$md.AppendLine("- 总体状态: **$overall**")
[void]$md.AppendLine('- 基线: tools/gates-baseline.json（R2 已清零；R5 已由 check-package 实现）')
[void]$md.AppendLine('')
[void]$md.AppendLine('| 规则 | 状态 | 新增 | known-red | 已登记豁免 | 检查数 | 报告 |')
[void]$md.AppendLine('|---|---|---:|---:|---:|---:|---|')
foreach ($row in $rows) {
    [void]$md.AppendLine("| $($row.Rule) | $($row.Status) | $($row.New) | $($row.KnownRed) | $($row.Exempt) | $($row.Checked) | $($row.Report) |")
}
[void]$md.AppendLine('')
if ($knownRows.Count -gt 0) {
    [void]$md.AppendLine('## known-red 说明（M1 目标，不阻塞 M0）')
    [void]$md.AppendLine('')
    foreach ($row in $knownRows) {
        if ($row.Rule -eq 'R2') {
            [void]$md.AppendLine("- **R2**: $($baseline.knownRed.R2.reason)")
            [void]$md.AppendLine("  - 基线计数: demo 引用 $($baseline.knownRed.R2.counts.'r2-demo-ref')、EmbeddedResource Docs $($baseline.knownRed.R2.counts.'r2-embedded-docs')")
        }
        elseif ($row.Rule -eq 'R5') {
            [void]$md.AppendLine("- **R5**: 见 $($row.Report)")
        }
        else {
            [void]$md.AppendLine("- **$($row.Rule)**: 见 $($row.Report)")
        }
    }
    [void]$md.AppendLine('')
}
if ($errors.Count -gt 0) {
    [void]$md.AppendLine('## 汇总错误')
    [void]$md.AppendLine('')
    foreach ($e in $errors) { [void]$md.AppendLine("- $e") }
    [void]$md.AppendLine('')
}
[void]$md.AppendLine('## 用法')
[void]$md.AppendLine('')
[void]$md.AppendLine('- 常规: `pwsh -NoProfile tools/check-all.ps1`')
[void]$md.AppendLine('- 负例自检: `pwsh -NoProfile tools/check-deps.ps1 -SelfTest`')
[void]$md.AppendLine('- 严格模式（M2 起 known-red 也失败）: `pwsh -NoProfile tools/check-all.ps1 -KnownRed`')

$mdPath = Join-Path $OutDir "check-all-$stamp.md"
Set-Content -LiteralPath $mdPath -Value $md.ToString() -Encoding utf8
$summary = [ordered]@{
    generatedAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
    overall     = $overall
    knownRedMode = [bool]$KnownRed
    rows        = @($rows)
    errors      = @($errors)
}
$jsonPath = Join-Path $OutDir "check-all-$stamp.json"
Set-Content -LiteralPath $jsonPath -Value ($summary | ConvertTo-Json -Depth 8) -Encoding utf8
Set-Content -LiteralPath (Join-Path $OutDir 'check-all-latest.json') -Value ($summary | ConvertTo-Json -Depth 8) -Encoding utf8

if (-not $Quiet) {
    Write-Output ''
    Write-Output "M0 GATES - $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))"
    Write-Output ('{0,-8} {1,-12} {2,4} {3,9} {4,7} {5,8}  {6}' -f 'Rule', 'Status', 'New', 'KnownRed', 'Exempt', 'Checked', 'Report')
    Write-Output ('{0,-8} {1,-12} {2,4} {3,9} {4,7} {5,8}  {6}' -f ('-' * 8), ('-' * 12), ('-' * 4), ('-' * 9), ('-' * 7), ('-' * 8), ('-' * 40))
    foreach ($row in $rows) {
        Write-Output ('{0,-8} {1,-12} {2,4} {3,9} {4,7} {5,8}  {6}' -f $row.Rule, $row.Status, $row.New, $row.KnownRed, $row.Exempt, $row.Checked, $row.Report)
    }
    $greenCount = @($rows | Where-Object { $_.Status -eq 'GREEN' }).Count
    Write-Output ''
    Write-Output "GATES: green=$greenCount/$($rows.Count) knownRed=$($knownRows.Count) red=$($redRows.Count) errors=$($errors.Count) => $overall"
    if ($knownRows.Count -gt 0) {
        Write-Output 'KNOWN-RED（M1 目标，不阻塞 M0）:'
        foreach ($row in $knownRows) {
            if ($row.Rule -eq 'R2') {
                Write-Output "  - R2: 发布库仍引用 Demo（$($baseline.knownRed.R2.counts.'r2-demo-ref')）+ EmbeddedResource Docs（$($baseline.knownRed.R2.counts.'r2-embedded-docs')）；M1-T1 出库后清零"
            }
            elseif ($row.Rule -eq 'R5') {
                Write-Output '  - R5: 见 check-package 报告（artifacts/ 未生成时会 RED，先 dotnet pack）'
            }
            else {
                Write-Output "  - $($row.Rule): 见 $($row.Report)"
            }
        }
    }
    Write-Output "report: $(ConvertTo-GatesRelPath -Path $mdPath -Base $root)"
}

$exitCode = 0
if ($errors.Count -gt 0 -or $redRows.Count -gt 0) { $exitCode = 1 }
elseif ($KnownRed -and $knownRows.Count -gt 0) { $exitCode = 1 }
exit $exitCode
