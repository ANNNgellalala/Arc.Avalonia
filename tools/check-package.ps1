#Requires -Version 7.0
<#
.SYNOPSIS
    R5 包产物门禁：解包 dotnet pack 产物，断言每个 nupkg 的 ID/版本/依赖/内容/许可。

.DESCRIPTION
    对 artifacts/ 下的 *.nupkg 逐包检查（docs/15 §5 / docs/13 §2.2 R5）：
      1. 包集合：恰好是「所有 IsPackable=true 的 csproj」推导出的包（含 2 个 meta：Arc.Avalonia / Arc.Avalonia.All），
         不得出现 Docs/Abstractions/Gallery/samples/POC 包；
      2. 依赖：只允许 Avalonia 官方包（Avalonia/Avalonia.Themes.Fluent/Avalonia.Fonts.Inter）
         与 Arc.Avalonia.* 包；Arc 依赖版本必须是同一 lockstep 版本；禁止 third-party 与 Docs 依赖；
      3. 内容：非 meta 包必须有 lib/net10.0/<AssemblyName>.dll 与同名 .xml；
         任何包都不得含 Pages/、Docs/、demo PNG 或其他页面资源；
      4. 许可：LICENSE、README.md、THIRD-PARTY-NOTICES.md 位于包根，nuspec 声明 license=file LICENSE、
         readme=README.md；元包无 lib 且依赖树符合预期。
    报告写入 .recon/gates/package-<stamp>.md|json（并写 package-latest.json 供 check-all/T5 接入）。

.PARAMETER Artifacts
    nupkg 所在目录，默认 <repo>/artifacts。

.PARAMETER ExpectedVersion
    期望的 lockstep 版本，默认 0.9.0-alpha。

.PARAMETER OutDir
    报告目录，默认 <repo>/.recon/gates。

.PARAMETER Quiet
    不打印逐包明细（仍写报告并输出最终一行结论）。

.EXAMPLE
    dotnet pack ArcUI.slnx -c Release -o artifacts -m:1 -nodeReuse:false
    pwsh -NoProfile tools/check-package.ps1

.EXITCODES
    0  全绿；1  存在违规；2  脚本自身错误（artifacts 缺失、nupkg 损坏等）。
#>
[CmdletBinding()]
param(
    [string]$Artifacts = '',
    [string]$ExpectedVersion = '0.9.0-alpha',
    [string]$OutDir = '',
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Artifacts)) { $Artifacts = Join-Path $root 'artifacts' }
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$utf8 = New-Object System.Text.UTF8Encoding($false)

# ---------------------------------------------------------------------------
# Expectations. The package set is derived from the repository: every src/**/*.csproj with
# <IsPackable>true</IsPackable> contributes its <PackageId> (falling back to <AssemblyName>). Adding a
# package therefore never requires editing this script. The two meta packages keep an explicit dependency
# shape check below; they are not expected to aggregate the Feature packages.
# ---------------------------------------------------------------------------
$allowedExternal = @('Avalonia', 'Avalonia.Themes.Fluent', 'Avalonia.Fonts.Inter', 'Avalonia.Controls.DataGrid', 'PuraMarkdown.Syntax', 'Markdig')

# Layer families the meta packages must aggregate (dependency-shape contract for Arc.Avalonia / .All).
$components = @(
    'Arc.Avalonia.Inputs', 'Arc.Avalonia.Actions', 'Arc.Avalonia.Navigation', 'Arc.Avalonia.Overlays',
    'Arc.Avalonia.Feedback', 'Arc.Avalonia.DataDisplay', 'Arc.Avalonia.Charts', 'Arc.Avalonia.Pickers',
    'Arc.Avalonia.Effects', 'Arc.Avalonia.Content', 'Arc.Avalonia.Media', 'Arc.Avalonia.Markdown'
)
# L4 agent layer (top composition) + the two leaf agent surface packages.
$agents = @('Arc.Avalonia.Agent', 'Arc.Avalonia.AgentUI', 'Arc.Avalonia.LifeToday')
$specials = @(
    'Arc.Avalonia.Special.Glass', 'Arc.Avalonia.Special.Motion', 'Arc.Avalonia.Special.Media',
    'Arc.Avalonia.Special.AIWidgets', 'Arc.Avalonia.Special.AIExplainers'
)
$blocks = @(
    'Arc.Avalonia.Blocks.Marketing', 'Arc.Avalonia.Blocks.Commerce', 'Arc.Avalonia.Blocks.App',
    'Arc.Avalonia.Blocks.Team', 'Arc.Avalonia.Blocks.Data', 'Arc.Avalonia.Blocks.AI'
)
$metaExpectedDeps = @{
    'Arc.Avalonia'     = @('Arc.Avalonia.Theme') + $components + $agents
    'Arc.Avalonia.All' = @('Arc.Avalonia') + $specials + $blocks
}
$metaPackages = @('Arc.Avalonia', 'Arc.Avalonia.All')

function Get-PublishedProjects {
    param([string]$RepoRoot)
    $projects = [System.Collections.Generic.List[object]]::new()
    foreach ($file in Get-ChildItem -Recurse -File -Filter *.csproj (Join-Path $RepoRoot 'src')) {
        if ($file.FullName -match '\\(bin|obj)\\') { continue }
        $text = [IO.File]::ReadAllText($file.FullName)
        if ($text -notmatch '<IsPackable>true</IsPackable>') { continue }
        $assembly = if ($text -match '<AssemblyName>([^<]+)</AssemblyName>') { $Matches[1] } else { $file.BaseName }
        $packageId = if ($text -match '<PackageId>([^<]+)</PackageId>') { $Matches[1] } else { $assembly }
        $projects.Add([pscustomobject]@{ PackageId = $packageId; AssemblyName = $assembly; Project = $file.FullName })
    }

    return @($projects | Sort-Object PackageId)
}

$publishedProjects = @(Get-PublishedProjects -RepoRoot $root)
if ($publishedProjects.Count -eq 0) {
    Write-Output 'PACKAGE GATE: ERROR - 未找到任何 IsPackable=true 的工程'
    exit 2
}

$expectedPackages = @($publishedProjects | ForEach-Object { $_.PackageId })
foreach ($meta in $metaPackages) {
    if ($expectedPackages -notcontains $meta) { $expectedPackages += $meta }
}

# Assembly file name inside lib/ per package (PackageId can differ from AssemblyName, e.g. meta packages).
$assemblyNameByPackage = @{}
foreach ($project in $publishedProjects) { $assemblyNameByPackage[$project.PackageId] = $project.AssemblyName }

$forbiddenPackagePatterns = @(
    'Arc.Avalonia.Docs*', 'Arc.Avalonia.Gallery*', '*.Sample.*', '*Poc*', '*POC*'
)
$forbiddenPathPatterns = @(
    '(^|/)Pages/', '(^|/)Docs/', '\.png$', '\.jpg$', '\.jpeg$', '\.webp$',
    'portrait-', 'arc-catalog', 'demo'
)

function New-Issue {
    param([string]$Package, [string]$Code, [string]$Message)
    [pscustomobject]@{ package = $Package; code = $Code; message = $Message }
}

$issues = [System.Collections.Generic.List[object]]::new()
$rows = [System.Collections.Generic.List[object]]::new()

function Read-Nupkg {
    param([string]$Path)
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($zip.Entries | ForEach-Object { $_.FullName })
        $nuspecEntry = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
        if ($null -eq $nuspecEntry) { throw "没有 nuspec：$Path" }
        $reader = New-Object System.IO.StreamReader($nuspecEntry.Open())
        try { $nuspecText = $reader.ReadToEnd() } finally { $reader.Close() }
        $xml = [xml]$nuspecText
        $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
        $ns.AddNamespace('n', 'http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd')
        $metadata = $xml.package.metadata
        $deps = @()
        foreach ($group in @($metadata.dependencies.group)) {
            if ($null -eq $group) { continue }
            foreach ($dep in @($group.dependency)) {
                if ($null -eq $dep) { continue }
                $deps += [pscustomobject]@{ id = [string]$dep.id; version = [string]$dep.version }
            }
        }
        $license = $metadata.license
        $licenseType = if ($null -ne $license) { [string]$license.type } else { '' }
        $licenseValue = if ($null -ne $license) { [string]$license.'#text' } else { '' }
        if ([string]::IsNullOrWhiteSpace($licenseValue) -and $null -ne $license) { $licenseValue = [string]$license.InnerText }
        $readme = if ($null -ne $metadata.readme) { [string]$metadata.readme } else { '' }
        return [pscustomobject]@{
            Entries      = $entries
            Id           = [string]$metadata.id
            Version      = [string]$metadata.version
            Authors      = [string]$metadata.authors
            Description  = [string]$metadata.description
            Dependencies = @($deps)
            LicenseType  = $licenseType
            LicenseValue = $licenseValue.Trim()
            Readme       = $readme
        }
    }
    finally { $zip.Dispose() }
}

if (-not (Test-Path -LiteralPath $Artifacts)) {
    Write-Output "PACKAGE GATE: ERROR - artifacts 目录不存在：$Artifacts"
    Write-Output "  先运行: dotnet pack ArcUI.slnx -c Release -o artifacts -m:1 -nodeReuse:false"
    exit 2
}

$nupkgs = @(Get-ChildItem -LiteralPath $Artifacts -Filter *.nupkg -File | Sort-Object Name)
if ($nupkgs.Count -eq 0) {
    Write-Output "PACKAGE GATE: ERROR - $Artifacts 下没有 nupkg"
    exit 2
}

foreach ($file in $nupkgs) {
    try {
        $pkg = Read-Nupkg -Path $file.FullName
    }
    catch {
        $issues.Add((New-Issue -Package $file.Name -Code 'package.unreadable' -Message $_.Exception.Message))
        continue
    }

    $row = [pscustomobject]@{
        File             = $file.Name
        Id               = $pkg.Id
        Version          = $pkg.Version
        SizeBytes        = $file.Length
        HasXml           = $false
        HasLicense       = $false
        HasReadme        = $false
        HasNotices       = $false
        HasLib           = $false
        DependencyCount  = $pkg.Dependencies.Count
        Dependencies     = ($pkg.Dependencies | ForEach-Object { "$($_.id) $($_.version)" }) -join '; '
        DemoFree         = $true
    }
    $rows.Add($row)

    # --- identity -----------------------------------------------------------
    if ($expectedPackages -notcontains $pkg.Id) {
        $issues.Add((New-Issue -Package $pkg.Id -Code 'package.unexpected' -Message "不在可发布包清单内（$($file.Name)）"))
    }
    if ($pkg.Version -ne $ExpectedVersion) {
        $issues.Add((New-Issue -Package $pkg.Id -Code 'package.version' -Message "版本 $($pkg.Version) != 期望 $ExpectedVersion"))
    }
    if ([string]::IsNullOrWhiteSpace($pkg.Authors)) {
        $issues.Add((New-Issue -Package $pkg.Id -Code 'package.authors' -Message 'nuspec 缺少 authors'))
    }
    if ([string]::IsNullOrWhiteSpace($pkg.Description)) {
        $issues.Add((New-Issue -Package $pkg.Id -Code 'package.description' -Message 'nuspec 缺少 description'))
    }

    # --- content: never ship pages/docs/demo assets -------------------------
    foreach ($entry in $pkg.Entries) {
        foreach ($pattern in $forbiddenPathPatterns) {
            if ($entry -match $pattern) {
                $row.DemoFree = $false
                $issues.Add((New-Issue -Package $pkg.Id -Code 'package.demo-content' -Message "包含演示/页面资源：$entry"))
            }
        }
    }
    $libEntries = @($pkg.Entries | Where-Object { $_ -like 'lib/*' })
    $row.HasLib = $libEntries.Count -gt 0
    $hasLicenseFile = @($pkg.Entries | Where-Object { $_ -eq 'LICENSE' }).Count -gt 0
    $hasReadmeFile = @($pkg.Entries | Where-Object { $_ -eq 'README.md' }).Count -gt 0
    $hasNoticesFile = @($pkg.Entries | Where-Object { $_ -eq 'THIRD-PARTY-NOTICES.md' }).Count -gt 0
    $row.HasLicense = $hasLicenseFile
    $row.HasReadme = $hasReadmeFile
    $row.HasNotices = $hasNoticesFile

    if (-not $hasLicenseFile) { $issues.Add((New-Issue -Package $pkg.Id -Code 'package.license-file' -Message '包根缺少 LICENSE')) }
    if ($pkg.LicenseType -ne 'file' -or $pkg.LicenseValue -ne 'LICENSE') {
        $issues.Add((New-Issue -Package $pkg.Id -Code 'package.license-meta' -Message "nuspec license 应为 file/LICENSE，实际 type=$($pkg.LicenseType) value=$($pkg.LicenseValue)"))
    }
    if (-not $hasReadmeFile) { $issues.Add((New-Issue -Package $pkg.Id -Code 'package.readme-file' -Message '包根缺少 README.md')) }
    if ($pkg.Readme -ne 'README.md') {
        $issues.Add((New-Issue -Package $pkg.Id -Code 'package.readme-meta' -Message "nuspec readme 应为 README.md，实际 '$($pkg.Readme)'"))
    }
    if (-not $hasNoticesFile) { $issues.Add((New-Issue -Package $pkg.Id -Code 'package.notices' -Message '包根缺少 THIRD-PARTY-NOTICES.md')) }

    # --- dependencies -------------------------------------------------------
    foreach ($dep in $pkg.Dependencies) {
        $isArc = $dep.id -like 'Arc.Avalonia*'
        $isAllowedExternal = $allowedExternal -contains $dep.id
        if (-not $isArc -and -not $isAllowedExternal) {
            $issues.Add((New-Issue -Package $pkg.Id -Code 'package.dependency' -Message "非白名单依赖：$($dep.id) $($dep.version)"))
        }
        if ($isArc) {
            if ($expectedPackages -notcontains $dep.id) {
                $issues.Add((New-Issue -Package $pkg.Id -Code 'package.dependency' -Message "依赖了不可发布/未知的 Arc 包：$($dep.id)"))
            }
            if ($dep.version -ne $ExpectedVersion) {
                $issues.Add((New-Issue -Package $pkg.Id -Code 'package.dependency-version' -Message "Arc 依赖 $($dep.id) 版本 $($dep.version) != $ExpectedVersion"))
            }
        }
    }
    foreach ($pattern in $forbiddenPackagePatterns) {
        if ($pkg.Id -like $pattern) {
            $issues.Add((New-Issue -Package $pkg.Id -Code 'package.leaf' -Message '演示/应用工程不应被打包'))
        }
    }

    # --- library vs meta shape ---------------------------------------------
    if ($metaPackages -contains $pkg.Id) {
        if ($libEntries.Count -gt 0) {
            $issues.Add((New-Issue -Package $pkg.Id -Code 'meta.lib' -Message "元包不应包含 lib/ 内容：$($libEntries -join ', ')"))
        }
        $expectedDeps = @($metaExpectedDeps[$pkg.Id])
        $actualDeps = @($pkg.Dependencies | Where-Object { $_.id -like 'Arc.Avalonia*' } | ForEach-Object { $_.id })
        foreach ($required in $expectedDeps) {
            if ($actualDeps -notcontains $required) {
                $issues.Add((New-Issue -Package $pkg.Id -Code 'meta.dependency-missing' -Message "元包缺少依赖 $required"))
            }
        }
        foreach ($actual in $actualDeps) {
            if ($expectedDeps -notcontains $actual) {
                $issues.Add((New-Issue -Package $pkg.Id -Code 'meta.dependency-extra' -Message "元包多出依赖 $actual"))
            }
        }
    }
    else {
        $asm = $assemblyNameByPackage[$pkg.Id]
        if ($null -eq $asm) { $asm = $pkg.Id }
        $dll = "lib/net10.0/$asm.dll"
        $xml = "lib/net10.0/$asm.xml"
        if ($pkg.Entries -notcontains $dll) {
            $issues.Add((New-Issue -Package $pkg.Id -Code 'package.dll' -Message "缺少 $dll"))
        }
        if ($pkg.Entries -notcontains $xml) {
            $issues.Add((New-Issue -Package $pkg.Id -Code 'package.xml' -Message "缺少 $xml（GenerateDocumentationFile）"))
        }
        $row.HasXml = ($pkg.Entries -contains $xml)
    }
}

# --- missing expected packages --------------------------------------------
$actualIds = @($rows | ForEach-Object { $_.Id })
foreach ($expected in $expectedPackages) {
    if ($actualIds -notcontains $expected) {
        $issues.Add((New-Issue -Package $expected -Code 'package.missing' -Message '缺失可发布包（未 pack 或 IsPackable 未开）'))
    }
}

# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------
if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }
$status = if ($issues.Count -eq 0) { 'GREEN' } else { 'RED' }

$md = [System.Text.StringBuilder]::new()
[void]$md.AppendLine('# R5 包产物门禁（check-package）')
[void]$md.AppendLine('')
[void]$md.AppendLine("- 生成时间: $((Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz'))")
[void]$md.AppendLine("- 产物目录: ``$Artifacts``")
[void]$md.AppendLine("- 期望版本: $ExpectedVersion")
[void]$md.AppendLine("- 状态: **$status**（包数 $($rows.Count)/$($expectedPackages.Count)，违规 $($issues.Count)）")
[void]$md.AppendLine('')
[void]$md.AppendLine('| 包 | 版本 | KB | lib | xml | license | readme | notices | demo-free | 依赖数 |')
[void]$md.AppendLine('|---|---|---:|---|---|---|---|---|---|---:|')
foreach ($row in $rows) {
    [void]$md.AppendLine("| $($row.Id) | $($row.Version) | $([math]::Round($row.SizeBytes / 1KB)) | $($row.HasLib) | $($row.HasXml) | $($row.HasLicense) | $($row.HasReadme) | $($row.HasNotices) | $($row.DemoFree) | $($row.DependencyCount) |")
}
[void]$md.AppendLine('')
[void]$md.AppendLine('## 每包依赖清单')
[void]$md.AppendLine('')
foreach ($row in $rows) {
    [void]$md.AppendLine("- **$($row.Id)**：$(if ([string]::IsNullOrWhiteSpace($row.Dependencies)) { '(无)' } else { $row.Dependencies })")
}
[void]$md.AppendLine('')
if ($issues.Count -gt 0) {
    [void]$md.AppendLine('## 违规')
    [void]$md.AppendLine('')
    foreach ($issue in $issues) { [void]$md.AppendLine("- [$($issue.code)] $($issue.package)：$($issue.message)") }
    [void]$md.AppendLine('')
}
$mdPath = Join-Path $OutDir "package-$stamp.md"
Set-Content -LiteralPath $mdPath -Value $md.ToString() -Encoding utf8

$report = [ordered]@{
    gate        = 'R5'
    name        = 'package'
    title       = 'dotnet pack 产物合规（ID/版本/依赖/内容/许可）'
    scope       = "artifacts/*.nupkg"
    status      = $status
    generatedAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
    artifacts   = $Artifacts
    expectedVersion = $ExpectedVersion
    rules       = @([ordered]@{
            id            = 'R5'
            title         = '包产物不含 Pages/Docs/demo，依赖仅 Avalonia 官方包与 lockstep Arc 包'
            status        = $status
            checked       = $rows.Count
            newViolations = $issues.Count
            items         = @($issues)
            notes         = @(
                "包数: $($rows.Count) / 期望 $($expectedPackages.Count)（从 IsPackable=true 的 csproj 推导 + 2 meta 显式校验）",
                "报告: $(Convert-Path $mdPath)"
            )
        })
    summary     = [ordered]@{ checked = $rows.Count; newViolations = $issues.Count; packages = @($rows) }
}
$jsonPath = Join-Path $OutDir "package-$stamp.json"
$reportJson = $report | ConvertTo-Json -Depth 10
Set-Content -LiteralPath $jsonPath -Value $reportJson -Encoding utf8
Set-Content -LiteralPath (Join-Path $OutDir 'package-latest.json') -Value $reportJson -Encoding utf8

if (-not $Quiet) {
    Write-Output ''
    Write-Output "PACKAGE GATE - $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))"
    Write-Output ('{0,-34} {1,-12} {2,8} {3,5} {4,5} {5,8} {6,8} {7,8} {8,10} {9,5}' -f 'Package', 'Version', 'KB', 'lib', 'xml', 'license', 'readme', 'notices', 'demo-free', 'deps')
    Write-Output ('{0,-34} {1,-12} {2,8} {3,5} {4,5} {5,8} {6,8} {7,8} {8,10} {9,5}' -f ('-' * 34), ('-' * 12), ('-' * 8), '-----', '-----', '--------', '------', '-------', '----------', '-----')
    foreach ($row in $rows) {
        Write-Output ('{0,-34} {1,-12} {2,8} {3,5} {4,5} {5,8} {6,8} {7,8} {8,10} {9,5}' -f $row.Id, $row.Version, [math]::Round($row.SizeBytes / 1KB), $row.HasLib, $row.HasXml, $row.HasLicense, $row.HasReadme, $row.HasNotices, $row.DemoFree, $row.DependencyCount)
    }
    Write-Output ''
    if ($issues.Count -gt 0) {
        Write-Output 'PACKAGE VIOLATIONS:'
        foreach ($issue in $issues) { Write-Output "  - [$($issue.code)] $($issue.package): $($issue.message)" }
    }
    Write-Output "PACKAGE GATE: status=$status packages=$($rows.Count)/$($expectedPackages.Count) violations=$($issues.Count) version=$ExpectedVersion"
    Write-Output "report: $mdPath"
}

if ($issues.Count -gt 0) { exit 1 }
exit 0
