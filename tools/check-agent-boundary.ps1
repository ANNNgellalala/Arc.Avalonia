#Requires -Version 7.0
<#
.SYNOPSIS
    Offline fallback for the M2-G4 Agent boundary gate. The primary gate is Microsoft's
    BannedApiAnalyzers wired into src/Agent/Arc.Avalonia.Agent/Arc.Avalonia.Agent.csproj
    (tools/BannedSymbols.txt as AdditionalFiles, RS0030 -> error). This script exists for environments
    where the analyzer package cannot be restored and for a quick pre-build review.

.DESCRIPTION
    Scans src/Agent/Arc.Avalonia.Agent/**/*.cs for the forbidden symbol names listed in
    tools/BannedSymbols.txt, and checks the project's ProjectReference allow-list (Foundation,
    Components, sibling Agent projects, Avalonia packages only). Writes a gates-compatible JSON report.

.PARAMETER Quiet
    Only print the one-line summary.

.EXAMPLE
    pwsh -NoProfile tools/check-agent-boundary.ps1
#>
[CmdletBinding()]
param(
    [switch]$Quiet,
    [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }
if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

$project = Join-Path $root 'src/Agent/Arc.Avalonia.Agent/Arc.Avalonia.Agent.csproj'
$bannedFile = Join-Path $PSScriptRoot 'BannedSymbols.txt'
$issues = [System.Collections.Generic.List[object]]::new()

if (-not (Test-Path -LiteralPath $project)) {
    $issues.Add([pscustomobject]@{ code = 'missing-project'; file = $project; detail = 'Agent 工程不存在' })
}
if (-not (Test-Path -LiteralPath $bannedFile)) {
    $issues.Add([pscustomobject]@{ code = 'missing-banned-symbols'; file = $bannedFile; detail = '缺少 BannedSymbols.txt' })
}

# Symbol -> short token used in a source regex. Keep this mapping in sync with BannedSymbols.txt.
$tokens = @(
    @{ id = 'HttpClient'; regex = '\bHttpClient\b'; detail = 'network client' },
    @{ id = 'HttpRequestMessage'; regex = '\bHttpRequestMessage\b'; detail = 'network request' },
    @{ id = 'File'; regex = '\bFile\s*\.'; detail = 'file-system access' },
    @{ id = 'Directory'; regex = '\bDirectory\s*\.'; detail = 'directory access' },
    @{ id = 'FileStream'; regex = '\bFileStream\b'; detail = 'file stream' },
    @{ id = 'Process'; regex = '\bProcess\b'; detail = 'process execution' },
    @{ id = 'System.Threading.Timer'; regex = '\bnew\s+Timer\s*\('; detail = 'timer' },
    @{ id = 'System.Timers.Timer'; regex = '\bSystem\.Timers\.Timer\b'; detail = 'timer' },
    @{ id = 'DispatcherTimer'; regex = '\bDispatcherTimer\b'; detail = 'timer (use Dispatcher batching / host ticks)' },
    @{ id = 'TrayIcon'; regex = '\bTrayIcon\b'; detail = 'system tray (host capability)' },
    @{ id = 'IStorageProvider'; regex = '\bIStorageProvider\b'; detail = 'file picker (host service)' },
    @{ id = 'IStorageFile'; regex = '\bIStorageFile\b'; detail = 'file access (host service)' }
)

$agentDir = Join-Path $root 'src/Agent/Arc.Avalonia.Agent'
if (Test-Path -LiteralPath $agentDir) {
    $sources = @(Get-ChildItem -Recurse -File -Filter *.cs $agentDir | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
    foreach ($source in $sources) {
        $relative = $source.FullName.Substring($root.Length + 1).Replace('\', '/')
        $lineNo = 0
        foreach ($line in Get-Content -LiteralPath $source.FullName -Encoding utf8) {
            $lineNo++
            foreach ($token in $tokens) {
                if ($line -cmatch $token.regex) {
                    $issues.Add([pscustomobject]@{
                            code   = 'banned-symbol'
                            file   = $relative
                            line   = $lineNo
                            detail = "$($token.id): $($token.detail)"
                        })
                }
            }
        }
    }
}

# ProjectReference allow-list: L1 Foundation, L2 Components, sibling Agent (L4), Avalonia packages.
$allowedReferencePatterns = @(
    '\\Foundation\\Arc\.Avalonia\\Arc\.Avalonia\.csproj$',
    '\\Components\\Arc\.Avalonia\.[^\\]+\\[^\\]+\.csproj$',
    '\\Agent\\Arc\.Avalonia\.AgentUI\\Arc\.Avalonia\.AgentUI\.csproj$',
    'Arc\.Avalonia\.AgentUI\.csproj$'
)
if (Test-Path -LiteralPath $project) {
    [xml]$xml = Get-Content -LiteralPath $project -Raw
    foreach ($reference in @($xml.SelectNodes('//ProjectReference'))) {
        $include = [string]$reference.Include
        $matched = $false
        foreach ($pattern in $allowedReferencePatterns) {
            if ($include -match $pattern) { $matched = $true; break }
        }

        if (-not $matched) {
            $issues.Add([pscustomobject]@{
                    code   = 'reference-outside-allowlist'
                    file   = 'src/Agent/Arc.Avalonia.Agent/Arc.Avalonia.Agent.csproj'
                    detail = "ProjectReference $include"
                })
        }
    }

    foreach ($reference in @($xml.SelectNodes('//PackageReference'))) {
        $id = [string]$reference.Include
        if ($id -ne 'Avalonia' -and $id -ne 'Microsoft.CodeAnalysis.BannedApiAnalyzers') {
            $issues.Add([pscustomobject]@{
                    code   = 'package-outside-allowlist'
                    file   = 'src/Agent/Arc.Avalonia.Agent/Arc.Avalonia.Agent.csproj'
                    detail = "PackageReference $id"
                })
        }
    }
}

$status = if ($issues.Count -eq 0) { 'GREEN' } else { 'RED' }
$report = [ordered]@{
    generatedAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
    gate        = 'AGENT-BOUNDARY'
    status      = $status
    issues      = @($issues)
    notes       = @(
        '主门禁: Microsoft.CodeAnalysis.BannedApiAnalyzers（RS0030 -> error）',
        '本脚本是离线兜底；不替代 analyzer。',
        '允许的依赖: L1 Foundation, L2 Components, L4 AgentUI, Avalonia 官方包。'
    )
}
$reportPath = Join-Path $OutDir 'agent-boundary-latest.json'
Set-Content -LiteralPath $reportPath -Value ($report | ConvertTo-Json -Depth 8) -Encoding utf8

foreach ($issue in $issues) {
    Write-Output ("AGENT BOUNDARY {0}: {1} {2} {3}" -f $issue.code, $issue.file, $issue.line, $issue.detail)
}
Write-Output "AGENT BOUNDARY: status=$status issues=$($issues.Count) report=$(Resolve-Path -Relative $reportPath)"
if ($status -eq 'GREEN') { exit 0 }
exit 1


