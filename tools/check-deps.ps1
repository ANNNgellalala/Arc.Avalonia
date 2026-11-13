#Requires -Version 7.0
<#
.SYNOPSIS
    R3 依赖门禁：解析发布库项目 obj/project.assets.json 的 targets 段，取完整传递闭包，
    对照 tools/deps-policy.json 做 fail-closed 判定（默认拒绝、deny 优先、未列名的 Avalonia.* 一律拒绝）。

.PARAMETER SelfTest
    不扫描仓库，改用 tools/fixtures/self-test/** 的伪造 assets 验证：
    ① 黑名单包被直接引用时能抓到；② 黑名单包被传递引入时能抓到（并给出引入路径）；
    ③ 干净项目不会误报。全部通过 exit 0，任一断言失败 exit 1。

.PARAMETER IncludeApps
    额外扫描 src/Apps/**（应用允许直接引用 Avalonia.Desktop）。默认只扫发布库。

.PARAMETER NoRestore
    obj/project.assets.json 缺失时不做单项目 restore，直接记为 ERROR（默认会串行 restore 单个 csproj）。

.PARAMETER Quiet
    只输出一行摘要（tools/check-all.ps1 使用）。

.PARAMETER OutDir
    报告输出目录，默认 <repo>/.recon/gates（已被 .gitignore 忽略）。

.EXITCODES
    0  无违规（含仅 known-red；R3 当前没有 known-red）
    1  存在新增违规（黑名单命中 / 未白名单包 / 未知 Avalonia.* / Feature 包越界）
    2  无法完成检查（assets 缺失且未 restore、解析失败、restore 失败）

.EXAMPLE
    pwsh -NoProfile tools/check-deps.ps1
    pwsh -NoProfile tools/check-deps.ps1 -SelfTest
    pwsh -NoProfile tools/check-deps.ps1 -IncludeApps -Quiet
#>
[CmdletBinding()]
param(
    [switch]$SelfTest,
    [switch]$IncludeApps,
    [switch]$NoRestore,
    [switch]$Quiet,
    [string]$OutDir = ''
)

. (Join-Path $PSScriptRoot 'gates-common.ps1')

$root = Get-GatesRoot
$policy = Read-GatesJson (Join-Path $PSScriptRoot 'deps-policy.json')
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root '.recon/gates' }

function Get-PackageIdFromKey {
    param([Parameter(Mandatory)][string]$Key)
    $slash = $Key.IndexOf('/')
    if ($slash -lt 0) { return $Key }
    return $Key.Substring(0, $slash)
}

function Get-PackageVersionFromKey {
    param([Parameter(Mandatory)][string]$Key)
    $slash = $Key.IndexOf('/')
    if ($slash -lt 0) { return '' }
    return $Key.Substring($slash + 1)
}

function Test-PolicyExactId {
    param([string]$Id, [string[]]$Ids = @())
    foreach ($candidate in $Ids) {
        if ([string]::Equals($Id, $candidate, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Get-DepsPolicyVerdict {
    param(
        [Parameter(Mandatory)][string]$PackageId,
        [bool]$IsApp,
        [bool]$IsFeature,
        [string]$ProjectRel = '',
        [bool]$IsDirectPackage = $false
    )
    # 1) deny 优先（直接或传递命中都拒绝）
    if (Test-PolicyExactId -Id $PackageId -Ids @($policy.denied.exact)) {
        return [pscustomobject]@{ allowed = $false; category = 'denied'; kind = 'denied-package'; reason = '在 deps-policy.denied.exact 中' }
    }
    if (Test-GatesAnyGlob -Name $PackageId -Patterns @($policy.denied.patterns)) {
        return [pscustomobject]@{ allowed = $false; category = 'denied'; kind = 'denied-package'; reason = '匹配 deps-policy.denied.patterns' }
    }
    # 2) build-only analyzer/tooling：允许存在于编译期 assets，但不计入发布依赖闭包。
    if ($null -ne $policy.PSObject.Properties['buildOnlyPackages'] -and
        (Test-PolicyExactId -Id $PackageId -Ids @($policy.buildOnlyPackages.packages))) {
        return [pscustomobject]@{ allowed = $true; category = 'build-only'; kind = ''; reason = 'build-only analyzer/tooling，不进发布包依赖闭包' }
    }
    # 3) 直接官方包
    foreach ($entry in $policy.allowedDirect) {
        if (-not [string]::Equals($PackageId, [string]$entry.id, [System.StringComparison]::OrdinalIgnoreCase)) { continue }
        switch ([string]$entry.scope) {
            'appsOnly' {
                if ($IsApp) { return [pscustomobject]@{ allowed = $true; category = 'direct-apps'; kind = ''; reason = '应用专用官方包' } }
                return [pscustomobject]@{ allowed = $false; category = 'scope'; kind = 'apps-only-package'; reason = '官方包只允许 src/Apps 直接引用' }
            }
            'featureOnly' {
                if ($IsFeature) { return [pscustomobject]@{ allowed = $true; category = 'feature'; kind = ''; reason = 'Feature 专用官方包' } }
                return [pscustomobject]@{ allowed = $false; category = 'scope'; kind = 'feature-package-outside-feature'; reason = '只允许 Arc.Avalonia.Feature.* 发布包引用（R7）' }
            }
            'agentChatBridge' {
                # ADR 0007 有范围例外：只校验“直接 PackageReference”（Chat/Agent）。
                # 经 Chat/Agent 链条传递到 Theme/Meta/All 的 PuraMarkdown.Agent 属正常闭包，交给 allowedTransitive。
                if (-not $IsDirectPackage) {
                    return [pscustomobject]@{ allowed = $true; category = 'agent-chat-bridge-transitive'; kind = ''; reason = 'PuraMarkdown.Agent 传递依赖；直接引用范围由 agentChatBridge 校验' }
                }
                $allowed = $false
                if ($null -ne $entry.PSObject.Properties['allowedProjects']) {
                    foreach ($pattern in @($entry.allowedProjects)) {
                        if (Test-GatesGlobMatch -Name $ProjectRel -Pattern ([string]$pattern)) { $allowed = $true; break }
                    }
                }
                if ($allowed) {
                    return [pscustomobject]@{ allowed = $true; category = 'agent-chat-bridge'; kind = ''; reason = 'ADR 0007 有范围例外：Chat/Agent 直接引用 PuraMarkdown.Agent' }
                }
                return [pscustomobject]@{ allowed = $false; category = 'scope'; kind = 'agent-chat-bridge-outside-scope'; reason = 'PuraMarkdown.Agent 只允许 Arc.Avalonia.Chat / Arc.Avalonia.Agent 直接 PackageReference（ADR 0007）' }
            }
            'markdownUiEngine' {
                # ADR 0008 有范围例外：只校验“直接 PackageReference”（Markdown/Chat/Agent）。
                # 经 Markdown/Chat/Agent 链条传递到 Theme/Meta/All 的 PuraMarkdown.UI 属正常闭包。
                if (-not $IsDirectPackage) {
                    return [pscustomobject]@{ allowed = $true; category = 'markdown-ui-engine-transitive'; kind = ''; reason = 'PuraMarkdown.UI 传递依赖；直接引用范围由 markdownUiEngine 校验' }
                }
                $allowed = $false
                if ($null -ne $entry.PSObject.Properties['allowedProjects']) {
                    foreach ($pattern in @($entry.allowedProjects)) {
                        if (Test-GatesGlobMatch -Name $ProjectRel -Pattern ([string]$pattern)) { $allowed = $true; break }
                    }
                }
                if ($allowed) {
                    return [pscustomobject]@{ allowed = $true; category = 'markdown-ui-engine'; kind = ''; reason = 'ADR 0008 有范围例外：Markdown/Chat/Agent 直接引用 PuraMarkdown.UI' }
                }
                return [pscustomobject]@{ allowed = $false; category = 'scope'; kind = 'markdown-ui-engine-outside-scope'; reason = 'PuraMarkdown.UI 只允许 Arc.Avalonia.Markdown / Arc.Avalonia.Chat / Arc.Avalonia.Agent 直接 PackageReference（ADR 0008）' }
            }
            default {
                return [pscustomobject]@{ allowed = $true; category = 'direct'; kind = ''; reason = '官方直接依赖' }
            }
        }
    }
    # 3) 官方传递/平台包
    if (Test-PolicyExactId -Id $PackageId -Ids @($policy.allowedTransitive)) {
        return [pscustomobject]@{ allowed = $true; category = 'transitive'; kind = ''; reason = '官方传递/平台包' }
    }
    if (Test-GatesAnyGlob -Name $PackageId -Patterns @($policy.allowedTransitivePatterns)) {
        return [pscustomobject]@{ allowed = $true; category = 'transitive'; kind = ''; reason = '官方传递/平台包（native assets）' }
    }
    # 4) 构建/SDK 级 System.* / Microsoft.*
    if (Test-GatesAnyGlob -Name $PackageId -Patterns @($policy.systemBuild.patterns)) {
        return [pscustomobject]@{ allowed = $true; category = 'system'; kind = ''; reason = '构建/SDK 级依赖' }
    }
    # 5) Avalonia.* 不在白名单 → 单独归类，便于定位“官方附加控件间接带入”
    if ($PackageId -match '^Avalonia(\.|$)') {
        return [pscustomobject]@{ allowed = $false; category = 'unknown-avalonia'; kind = 'unknown-avalonia-package'; reason = 'Avalonia.* 不在白名单内' }
    }
    # 6) 默认拒绝
    return [pscustomobject]@{ allowed = $false; category = 'not-allowed'; kind = 'package-not-allowed'; reason = '不在白名单（fail-closed 默认拒绝）' }
}

function Read-AssetsHashtable {
    param([Parameter(Mandatory)][string]$Path)
    $attempt = 0
    while ($attempt -lt 3) {
        $attempt++
        try {
            $raw = Get-Content -LiteralPath $Path -Raw -Encoding utf8
            if ([string]::IsNullOrWhiteSpace($raw)) { throw 'empty assets file' }
            return ($raw | ConvertFrom-Json -AsHashtable)
        }
        catch {
            if ($attempt -ge 3) { throw }
            # assets 可能正被并行的 restore/build 写入；等待后重试，不并发 restore。
            Start-Sleep -Seconds 2
        }
    }
    throw "unreachable: $Path"
}

function New-DepsScanResult {
    return [pscustomobject]@{
        ProjectName    = ''
        Targets        = 0
        PackageCount   = 0
        ProjectRefCount = 0
        DirectCount    = 0
        TransitiveCount = 0
        Violations     = [System.Collections.Generic.List[object]]::new()
        BuildOnlyIds   = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        UnknownNodes   = [System.Collections.Generic.List[string]]::new()
        Errors         = [System.Collections.Generic.List[string]]::new()
        Categories     = @{}
    }
}

function Get-CsprojPackageIdLookup {
    # NuGet 的 targets 节点键是 PackageId（MSBuild 默认取 AssemblyName），而工程文件名/工程名可能不同
    # （M1-T2a 后 src/Foundation/Arc.Avalonia/Arc.Avalonia.csproj 的 PackageId = Arc.Avalonia.Foundation）。
    # 建两级映射：绝对 csproj 路径 → PackageId；工程文件名（及 PackageId 自身）→ PackageId。
    $byPath = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $byName = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($scanRoot in @((Join-Path $root 'src'), (Join-Path $root 'tests'))) {
        if (-not (Test-Path -LiteralPath $scanRoot)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $scanRoot -Recurse -File -Filter '*.csproj') {
            if ($file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
            $fileBase = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
            $packageId = ''
            try {
                [xml]$xml = Get-Content -LiteralPath $file.FullName -Raw -Encoding utf8
                foreach ($propName in @('PackageId', 'AssemblyName')) {
                    $node = $xml.SelectSingleNode("//Project/PropertyGroup/$propName")
                    if ($null -ne $node -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
                        $packageId = $node.InnerText.Trim()
                        break
                    }
                }
            }
            catch { $packageId = '' }
            if ([string]::IsNullOrWhiteSpace($packageId)) { $packageId = $fileBase }
            $byPath[[System.IO.Path]::GetFullPath($file.FullName)] = $packageId
            $byName[$fileBase] = $packageId
            $byName[$packageId] = $packageId
        }
    }
    return @{ ByPath = $byPath; ByName = $byName }
}

function Invoke-DepsClosureScan {
    param(
        [Parameter(Mandatory)][string]$AssetsPath,
        [Parameter(Mandatory)][string]$ProjectLabel,
        [bool]$IsApp = $false,
        [bool]$IsFeature = $false,
        [object]$PackageIdLookup = $null,
        [System.Collections.Generic.HashSet[string]]$DirectPackages = $null
    )
    $result = New-DepsScanResult
    $assets = $null
    try { $assets = Read-AssetsHashtable -Path $AssetsPath }
    catch {
        [void]$result.Errors.Add("assets 解析失败: $($_.Exception.Message)")
        return $result
    }
    if (-not $assets.ContainsKey('targets') -or $assets['targets'].Count -eq 0) {
        [void]$result.Errors.Add('assets 缺少 targets 段')
        return $result
    }
    $projectName = ''
    if ($assets.ContainsKey('project')) {
        $project = $assets['project']
        if ($project.ContainsKey('restore') -and $project['restore'].ContainsKey('projectName')) {
            $projectName = [string]$project['restore']['projectName']
        }
    }
    if ([string]::IsNullOrWhiteSpace($projectName)) {
        # 退化：取第一个 project 类型节点
        foreach ($tfmKey in $assets['targets'].Keys) {
            foreach ($entryKey in $assets['targets'][$tfmKey].Keys) {
                $entry = $assets['targets'][$tfmKey][$entryKey]
                if ($entry.ContainsKey('type') -and $entry['type'] -eq 'project') {
                    $projectName = Get-PackageIdFromKey $entryKey
                    break
                }
            }
            if ($projectName) { break }
        }
    }
    if ([string]::IsNullOrWhiteSpace($projectName)) {
        [void]$result.Errors.Add('无法确定 assets 的根项目名（project.restore.projectName 缺失）')
        return $result
    }
    $result.ProjectName = $projectName

    $seenViolationKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $packageIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $directIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $transitiveIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $projectRefIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $categories = @{}

    foreach ($tfm in @($assets['targets'].Keys | Sort-Object)) {
        $entries = $assets['targets'][$tfm]
        if ($null -eq $entries -or $entries.Count -eq 0) { continue }
        $result.Targets++
        # 同一 tfm 内 id→节点；id 大小写不敏感。
        $nodeCmps = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($entryKey in @($entries.Keys)) {
            $entry = $entries[$entryKey]
            $id = Get-PackageIdFromKey $entryKey
            $type = if ($entry.ContainsKey('type')) { [string]$entry['type'] } else { 'package' }
            $deps = @()
            if ($entry.ContainsKey('dependencies') -and $null -ne $entry['dependencies']) {
                $deps = @($entry['dependencies'].Keys)
            }
            if (-not $nodeCmps.ContainsKey($id)) {
                $nodeCmps[$id] = [pscustomobject]@{
                    Id   = $id
                    Type = $type
                    Deps = [System.Collections.Generic.List[string]]::new()
                }
            }
            $node = $nodeCmps[$id]
            foreach ($d in $deps) {
                $exists = $false
                foreach ($existing in $node.Deps) {
                    if ([string]::Equals($existing, $d, [System.StringComparison]::OrdinalIgnoreCase)) { $exists = $true; break }
                }
                if (-not $exists) { $node.Deps.Add($d) }
            }
        }
        if (-not $nodeCmps.ContainsKey($projectName)) {
            # 根项目自身不在 targets 段（NuGet 只写依赖），需要合成根节点：
            #   直接 PackageReference ← project.frameworks.<tfm>.dependencies（target=Package）
            #   直接 ProjectReference ← project.restore.frameworks.<tfm>.projectReferences → libraries.path
            $rootDeps = [System.Collections.Generic.List[string]]::new()
            $projectFrameworks = $null
            if ($assets['project'].ContainsKey('frameworks') -and $assets['project']['frameworks'].ContainsKey($tfm)) {
                $projectFrameworks = $assets['project']['frameworks'][$tfm]
            }
            if ($null -ne $projectFrameworks -and $projectFrameworks.ContainsKey('dependencies')) {
                foreach ($depKey in @($projectFrameworks['dependencies'].Keys)) {
                    $target = ''
                    try { $target = [string]$projectFrameworks['dependencies'][$depKey]['target'] } catch { $target = '' }
                    if ($target -eq 'Package' -or $target -eq 'Project') { $rootDeps.Add($depKey) }
                }
            }
            $restoreFrameworks = $null
            if ($assets['project'].ContainsKey('restore') -and $assets['project']['restore'].ContainsKey('frameworks') -and
                $assets['project']['restore']['frameworks'].ContainsKey($tfm)) {
                $restoreFrameworks = $assets['project']['restore']['frameworks'][$tfm]
            }
            if ($null -ne $restoreFrameworks -and $restoreFrameworks.ContainsKey('projectReferences')) {
                $projectDir = Split-Path -Parent (Split-Path -Parent $AssetsPath)
                $objDir = Join-Path $projectDir 'obj'
                # libraries 里 type=project 的 path 相对【工程目录】（NuGet 语义），不是相对 obj/。
                # 同时保留 obj/ 作为候选，兼容历史 assets；包 id 优先取 csproj 的 PackageId/AssemblyName。
                $libraryRefMap = @{}
                if ($assets.ContainsKey('libraries')) {
                    foreach ($libKey in @($assets['libraries'].Keys)) {
                        $lib = $assets['libraries'][$libKey]
                        if (-not $lib.ContainsKey('type') -or [string]$lib['type'] -ne 'project') { continue }
                        $libPath = [string]$lib['path']
                        if ([string]::IsNullOrWhiteSpace($libPath)) { continue }
                        $libId = Get-PackageIdFromKey $libKey
                        foreach ($baseDir in @($projectDir, $objDir)) {
                            try {
                                $abs = [System.IO.Path]::GetFullPath((Join-Path $baseDir $libPath))
                                $candidateId = $libId
                                if ($null -ne $PackageIdLookup -and $PackageIdLookup['ByPath'].ContainsKey($abs)) {
                                    $candidateId = [string]$PackageIdLookup['ByPath'][$abs]
                                }
                                $libraryRefMap[$abs] = $candidateId
                            }
                            catch { }
                        }
                    }
                }
                foreach ($refPath in @($restoreFrameworks['projectReferences'].Keys)) {
                    $id = $null
                    try {
                        $absRef = [System.IO.Path]::GetFullPath([string]$refPath)
                        if ($null -ne $PackageIdLookup -and $PackageIdLookup['ByPath'].ContainsKey($absRef)) {
                            $id = [string]$PackageIdLookup['ByPath'][$absRef]
                        }
                        elseif ($libraryRefMap.ContainsKey($absRef)) { $id = [string]$libraryRefMap[$absRef] }
                    }
                    catch { }
                    if ($null -eq $id) {
                        # 最后兜底：工程文件名 → csproj PackageId（改名后工程名与包 id 不一致时也能对上）。
                        $refBase = [System.IO.Path]::GetFileNameWithoutExtension([string]$refPath)
                        if ($null -ne $PackageIdLookup -and $PackageIdLookup['ByName'].ContainsKey($refBase)) {
                            $id = [string]$PackageIdLookup['ByName'][$refBase]
                        }
                        else { $id = $refBase }
                    }
                    if (-not [string]::IsNullOrWhiteSpace($id) -and -not $rootDeps.Contains($id)) { [void]$rootDeps.Add($id) }
                }
            }
            $nodeCmps[$projectName] = [pscustomobject]@{
                Id   = $projectName
                Type = 'project'
                Deps = [System.Collections.Generic.List[string]]::new()
            }
            foreach ($d in $rootDeps) { $nodeCmps[$projectName].Deps.Add($d) }
        }
        # BFS：记录最短引入路径与“包深度”（project 节点不增加包深度）。
        $paths = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $depths = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $visited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $queue = [System.Collections.Generic.Queue[string]]::new()
        $paths[$projectName] = @($projectName)
        $depths[$projectName] = 0
        [void]$visited.Add($projectName)
        $queue.Enqueue($projectName)
        while ($queue.Count -gt 0) {
            $current = $queue.Dequeue()
            $currentNode = $nodeCmps[$current]
            $currentPath = @($paths[$current])
            foreach ($dep in @($currentNode.Deps)) {
                $resolvedDep = $dep
                if (-not $nodeCmps.ContainsKey($resolvedDep)) {
                    # 依赖字典可能用工程名（Arc.Avalonia），而 targets 节点键是 PackageId（Arc.Avalonia.Foundation）：
                    # 先查 csproj PackageId 映射，再退回 X.Foundation 候选（M1-T2a 的改名形态）。
                    $candidates = [System.Collections.Generic.List[string]]::new()
                    if ($null -ne $PackageIdLookup -and $PackageIdLookup['ByName'].ContainsKey($dep)) {
                        $mapped = [string]$PackageIdLookup['ByName'][$dep]
                        if (-not [string]::IsNullOrWhiteSpace($mapped)) { $candidates.Add($mapped) }
                    }
                    $candidates.Add("$dep.Foundation")
                    foreach ($cand in $candidates) {
                        if ($nodeCmps.ContainsKey($cand)) { $resolvedDep = $cand; break }
                    }
                }
                if (-not $nodeCmps.ContainsKey($resolvedDep)) {
                    $unknownKey = "$tfm|$dep"
                    if (-not $result.UnknownNodes.Contains($unknownKey)) { $result.UnknownNodes.Add($unknownKey) }
                    continue
                }
                if ($visited.Add($resolvedDep)) {
                    $depNode = $nodeCmps[$resolvedDep]
                    $paths[$resolvedDep] = @($currentPath + $depNode.Id)
                    $depths[$resolvedDep] = $depths[$current] + $(if ($depNode.Type -eq 'package') { 1 } else { 0 })
                    $queue.Enqueue($resolvedDep)
                }
            }
        }
        foreach ($id in @($paths.Keys)) {
            if ($id -ieq $projectName) { continue }
            $node = $nodeCmps[$id]
            $canonicalId = $node.Id
            # 路径按 nodes 的规范大小写重写，避免依赖键大小写影响报告可读性。
            $path = @($paths[$id] | ForEach-Object {
                    if ($nodeCmps.ContainsKey($_)) { $nodeCmps[$_].Id } else { $_ }
                })
            if ($node.Type -eq 'project') { [void]$projectRefIds.Add($canonicalId); continue }
            $isDirectPackage = ($null -ne $DirectPackages -and $DirectPackages.Contains($canonicalId))
            $verdict = Get-DepsPolicyVerdict -PackageId $canonicalId -IsApp $IsApp -IsFeature $IsFeature -ProjectRel $ProjectLabel -IsDirectPackage $isDirectPackage
            if (-not $categories.ContainsKey($verdict.category)) { $categories[$verdict.category] = 0 }
            $categories[$verdict.category]++
            if ($verdict.category -eq 'build-only') {
                # 编译期 analyzer/tooling：不算发布闭包（PrivateAssets=all 不会出现在 nuspec 依赖里）。
                [void]$result.BuildOnlyIds.Add($canonicalId)
                continue
            }
            [void]$packageIds.Add($canonicalId)
            if ($depths[$id] -le 1) { [void]$directIds.Add($canonicalId) } else { [void]$transitiveIds.Add($canonicalId) }
            if (-not $verdict.allowed) {
                $violationKey = "$($ProjectLabel)|$($canonicalId.ToLowerInvariant())"
                if ($seenViolationKeys.Add($violationKey)) {
                    [void]$result.Violations.Add([pscustomobject]@{
                            Package  = $canonicalId
                            Kind     = $verdict.kind
                            Category = $verdict.category
                            Reason   = $verdict.reason
                            Path     = $path
                            Tfm      = $tfm
                        })
                }
            }
        }
    }
    $result.PackageCount = $packageIds.Count
    $result.ProjectRefCount = $projectRefIds.Count
    $result.DirectCount = $directIds.Count
    $result.TransitiveCount = $transitiveIds.Count
    $result.Categories = $categories
    return $result
}

function Invoke-SingleProjectRestore {
    param([Parameter(Mandatory)][string]$CsprojPath)
    $restoreArgs = @('restore', $CsprojPath, '--nologo', '-v', 'q', '-p:RestoreDisableParallel=true')
    & dotnet @restoreArgs *> $null
    if ($LASTEXITCODE -eq 0) { return $true }
    # 遇到文件锁（Lead 的全量构建在跑）时 sleep 10s 重试一次。
    Start-Sleep -Seconds 10
    & dotnet @restoreArgs *> $null
    return ($LASTEXITCODE -eq 0)
}

function Get-PublishProjects {
    $roots = [System.Collections.Generic.List[string]]::new()
    foreach ($r in @($policy.scan.publishRoots)) { [void]$roots.Add($r) }
    if ($IncludeApps) { foreach ($r in @($policy.scan.appsRoots)) { [void]$roots.Add($r) } }
    $projects = [System.Collections.Generic.List[object]]::new()
    foreach ($r in $roots) {
        $dir = Join-Path $root $r
        if (-not (Test-Path -LiteralPath $dir)) { continue }
        foreach ($p in Get-ChildItem -LiteralPath $dir -Recurse -File -Filter '*.csproj') {
            $rel = ConvertTo-GatesRelPath -Path $p.FullName -Base $root
            $relSegments = $rel -split '/'
            $excluded = $false
            foreach ($seg in @($policy.scan.excludePathSegments)) {
                $name = ([string]$seg).Trim('/')
                if ([string]::IsNullOrWhiteSpace($name)) { continue }
                if ($relSegments -contains $name) { $excluded = $true; break }
            }
            if ($excluded) { continue }
            [void]$projects.Add([pscustomobject]@{ Path = $p.FullName; Rel = $rel })
        }
    }
    return @($projects | Sort-Object Rel -Unique)
}

# ---------------------------------------------------------------------------
# -SelfTest：用 tools/fixtures/self-test/** 的伪造 assets 验证门禁不是“永远绿”。
# ---------------------------------------------------------------------------
if ($SelfTest) {
    $report = New-GatesReport -Gate 'R3-SelfTest' -Name 'deps-selftest' -Title '依赖门禁负例自检' `
        -Scope 'tools/fixtures/self-test/**（伪造 project.assets.json）'
    $rule = New-GatesRule -Id 'R3-SelfTest' -Title '负例注入断言'
    [void]$report.rules.Add($rule)
    $fixtureRoot = Join-Path $PSScriptRoot 'fixtures/self-test'
    if (-not (Test-Path -LiteralPath $fixtureRoot)) {
        [void]$rule.notes.Add("缺少 fixture 目录: $fixtureRoot")
        $rule.status = 'ERROR'
        $report.status = 'ERROR'
        exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
    }
    $cases = @(Get-ChildItem -LiteralPath $fixtureRoot -Directory | Sort-Object Name)
    if ($cases.Count -eq 0) {
        [void]$rule.notes.Add('fixture 目录为空')
        $rule.status = 'ERROR'
        $report.status = 'ERROR'
        exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
    }
    $rule.checked = $cases.Count
    $negativeCaught = 0
    $negativeTotal = 0
    foreach ($case in $cases) {
        $expectPath = Join-Path $case.FullName 'expect.json'
        $assetsPath = Join-Path $case.FullName 'project.assets.json'
        if (-not (Test-Path -LiteralPath $expectPath) -or -not (Test-Path -LiteralPath $assetsPath)) {
            Add-GatesItem -Rule $rule -Severity 'new' -Kind 'selftest-failure' -Project $case.Name -Detail '缺少 expect.json 或 project.assets.json'
            continue
        }
        $expect = Read-GatesJson -Path $expectPath
        $scan = Invoke-DepsClosureScan -AssetsPath $assetsPath -ProjectLabel "fixture:$($case.Name)"
        $found = @($scan.Violations)
        $expectedPackages = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($e in @($expect.expect)) { [void]$expectedPackages.Add([string]$e.package) }
        if ($expect.expectClean) {
            $negativeTotal++
            if ($found.Count -eq 0 -and $scan.Errors.Count -eq 0) {
                $negativeCaught++
                Add-GatesItem -Rule $rule -Severity 'pass' -Kind 'selftest-pass' -Project $case.Name -Detail '干净项目未被误报'
                if (-not $Quiet) { Write-Output "SELFTEST PASS $($case.Name): 干净项目未被误报" }
            }
            else {
                $detail = "干净项目被误报: " + (($found | ForEach-Object { $_.Package }) -join ', ')
                Add-GatesItem -Rule $rule -Severity 'new' -Kind 'selftest-failure' -Project $case.Name -Detail $detail
            }
            continue
        }
        $negativeTotal++
        $caseFailures = [System.Collections.Generic.List[string]]::new()
        foreach ($expectation in @($expect.expect)) {
            $package = [string]$expectation.package
            $match = @($found | Where-Object { [string]::Equals($_.Package, $package, [System.StringComparison]::OrdinalIgnoreCase) })
            if ($match.Count -eq 0) {
                [void]$caseFailures.Add("期望命中 $package，实际未命中")
                continue
            }
            foreach ($seg in @($expectation.pathContains)) {
                $hit = @($match | Where-Object { @($_.Path) -contains $seg })
                if ($hit.Count -eq 0) {
                    [void]$caseFailures.Add("$package 命中但引入路径缺少 ''$seg''（实际: $($match[0].Path -join ' > ')）")
                }
            }
        }
        # 未在 expect 中声明的违规也算失败：防止误报被吞掉。
        foreach ($v in $found) {
            if (-not $expectedPackages.Contains($v.Package)) {
                [void]$caseFailures.Add("额外违规 $($v.Package)（未在 expect.json 中声明）")
            }
        }
        if ($scan.Errors.Count -gt 0) {
            foreach ($err in $scan.Errors) { [void]$caseFailures.Add("扫描错误: $err") }
        }
        if ($caseFailures.Count -eq 0) {
            $negativeCaught++
            $caught = ($found | ForEach-Object { $_.Package }) -join ', '
            Add-GatesItem -Rule $rule -Severity 'pass' -Kind 'selftest-pass' -Project $case.Name -Detail "已抓到: $caught"
            if (-not $Quiet) { Write-Output "SELFTEST PASS $($case.Name): $caught" }
        }
        else {
            foreach ($f in $caseFailures) {
                Add-GatesItem -Rule $rule -Severity 'new' -Kind 'selftest-failure' -Project $case.Name -Detail $f
                if (-not $Quiet) { Write-Output "SELFTEST FAIL $($case.Name): $f" }
            }
        }
    }
    $rule.notes.Add("负例均被抓住: $negativeCaught/$negativeTotal（含干净项目不误报的对照）")
    $rule.notes.Add('fixture 说明见 tools/fixtures/self-test/*/expect.json 与 docs/adr/0002-dependency-policy.md')
    $report.notes.Add('本自检证明 R3 门禁对“黑名单直接命中”和“传递引入命中”都会失败，而不是永远绿。')
    Update-GatesRuleStatus -Rule $rule | Out-Null
    if ($negativeCaught -lt $negativeTotal) { $rule.status = 'RED' }
    exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
}

# ---------------------------------------------------------------------------
# 正常扫描：发布库（+可选 Apps）逐个项目解析 assets 传递闭包。
# ---------------------------------------------------------------------------
$scopeText = '发布库: ' + (@($policy.scan.publishRoots) -join ', ')
if ($IncludeApps) { $scopeText += ' + 应用/叶子: ' + (@($policy.scan.appsRoots) -join ', ') }
$report = New-GatesReport -Gate 'R3' -Name 'deps' -Title '依赖（直接 + 传递闭包）' -Scope $scopeText
$rule = New-GatesRule -Id 'R3' -Title '外部依赖白名单/黑名单'
[void]$report.rules.Add($rule)
Add-GatesNote -Report $report -Note '策略文件: tools/deps-policy.json（deny 优先；默认拒绝；未知 Avalonia.* 拒绝）'
Add-GatesNote -Report $report -Note '闭包来源: obj/project.assets.json 的 targets.<tfm> 依赖图（project 节点继续下钻）'

$projects = @(Get-PublishProjects)
# R3 节点键 = PackageId（缺省 AssemblyName），不是工程文件名：M1-T2a 把 Foundation 的程序集改名为
# Arc.Avalonia.Foundation 后，targets 里的键与 csproj 路径/工程名不再相同。
$packageIdLookup = Get-CsprojPackageIdLookup
$rule.checked = $projects.Count
$scanErrors = 0
$totalPackages = 0
$totalDirect = 0
$totalTransitive = 0
$totalProjectRefs = 0
$violationCount = 0
$buildOnlySeen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$skipped = [System.Collections.Generic.List[string]]::new()

foreach ($project in $projects) {
    $isApp = $project.Rel -match '^src/Apps/'
    $isFeature = $false
    foreach ($pattern in @($policy.rules.featureProjects.pathPatterns)) {
        if (Test-GatesGlobMatch -Name $project.Rel -Pattern ([string]$pattern)) { $isFeature = $true; break }
    }
    $projectDir = Split-Path -Parent $project.Path
    $assetsPath = Join-Path $projectDir 'obj/project.assets.json'
    if (-not (Test-Path -LiteralPath $assetsPath)) {
        if ($NoRestore) {
            $scanErrors++
            Add-GatesItem -Rule $rule -Severity 'new' -Kind 'scan-error' -Project $project.Rel -Detail '缺少 obj/project.assets.json（-NoRestore 下不自动 restore）'
            continue
        }
        if (-not (Invoke-SingleProjectRestore -CsprojPath $project.Path)) {
            $scanErrors++
            Add-GatesItem -Rule $rule -Severity 'new' -Kind 'scan-error' -Project $project.Rel -Detail '单项目 restore 失败（文件锁重试后仍失败）'
            continue
        }
        if (-not (Test-Path -LiteralPath $assetsPath)) {
            $scanErrors++
            Add-GatesItem -Rule $rule -Severity 'new' -Kind 'scan-error' -Project $project.Rel -Detail 'restore 后仍无 obj/project.assets.json'
            continue
        }
    }
    # 直接 PackageReference 集合（用于 agentChatBridge 等“只限直接引用”的 scope；传递依赖交给 allowedTransitive）。
    $directPackages = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    if (Test-Path -LiteralPath $project.Path) {
        try {
            [xml]$csprojXml = Get-Content -LiteralPath $project.Path -Raw -Encoding utf8
            foreach ($prNode in @($csprojXml.SelectNodes('//PackageReference'))) {
                $include = [string]$prNode.GetAttribute('Include')
                if (-not [string]::IsNullOrWhiteSpace($include)) { [void]$directPackages.Add($include.Trim()) }
            }
        }
        catch {
            $scanErrors++
            Add-GatesItem -Rule $rule -Severity 'new' -Kind 'scan-error' -Project $project.Rel -Detail "csproj 解析失败（无法判断直接 PackageReference）: $($_.Exception.Message)"
        }
    }
    $scan = Invoke-DepsClosureScan -AssetsPath $assetsPath -ProjectLabel $project.Rel -IsApp $isApp -IsFeature $isFeature -PackageIdLookup $packageIdLookup -DirectPackages $directPackages
    foreach ($err in $scan.Errors) {
        $scanErrors++
        Add-GatesItem -Rule $rule -Severity 'new' -Kind 'scan-error' -Project $project.Rel -Detail $err
    }
    foreach ($unknown in $scan.UnknownNodes) {
        $scanErrors++
        Add-GatesItem -Rule $rule -Severity 'new' -Kind 'scan-error' -Project $project.Rel -Detail "依赖图中的节点在 targets 中不存在（assets 不完整？）: $unknown"
    }
    $totalPackages += $scan.PackageCount
    $totalDirect += $scan.DirectCount
    $totalTransitive += $scan.TransitiveCount
    $totalProjectRefs += $scan.ProjectRefCount
    foreach ($buildOnlyId in @($scan.BuildOnlyIds)) { [void]$buildOnlySeen.Add($buildOnlyId) }
    foreach ($v in $scan.Violations) {
        $violationCount++
        Add-GatesItem -Rule $rule -Severity 'new' -Kind $v.Kind -Project $project.Rel `
            -Detail "包 $($v.Package) [$($v.Reason)] 引入路径: $($v.Path -join ' > ')" -Tfm $v.Tfm
    }
}

$rule.checked = $projects.Count
$rule.notes.Add("扫描项目数: $($projects.Count)；发布闭包包节点: $totalPackages（直接 $totalDirect / 传递 $totalTransitive）；项目引用节点: $totalProjectRefs")
if ($buildOnlySeen.Count -gt 0) {
    $rule.notes.Add("build-only 已从发布闭包剔除 $($buildOnlySeen.Count) 个: $(@($buildOnlySeen) -join ', ')（编译期 analyzer/tooling，见 deps-policy.buildOnlyPackages）")
}
$rule.notes.Add('节点键 = csproj PackageId（缺省 AssemblyName，再缺省工程名）；libraries[].path 按 NuGet 语义相对工程目录解析。')
$rule.notes.Add('agentChatBridge（PuraMarkdown.Agent）只校验 csproj 的直接 PackageReference；经 Chat/Agent 传递到 Theme/Meta/All 的依赖走 allowedTransitive 白名单。')
$rule.notes.Add('markdownUiEngine（PuraMarkdown.UI）同样只校验直接 PackageReference：Markdown/Chat/Agent 可直接引用，其它包经它们传递获得；越界即 markdown-ui-engine-outside-scope 红项（ADR 0008）。')
$rule.notes.Add("R3 当前无 known-red 基线：任何命中都是新增违规，必须修。")
$report.notes.Add('Apps 与发布库共用同一策略；Avalonia.Desktop 只在 -IncludeApps 扫描 src/Apps 时判定为应用专用合法。')
$report.notes.Add('SelfTest: pwsh -NoProfile tools/check-deps.ps1 -SelfTest')
Update-GatesRuleStatus -Rule $rule | Out-Null
if ($scanErrors -gt 0) { $rule.status = 'ERROR' }
exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
