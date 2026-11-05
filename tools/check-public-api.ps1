#Requires -Version 7.0
<#
.SYNOPSIS
    轻量 public API 基线门禁：反射导出每个 Arc 发布程序集的 public API 文本，并与基线 diff。

.DESCRIPTION
    M1-T4 的 API 基线方案。本地离线 NuGet 缓存没有 Microsoft.CodeAnalysis.PublicApiAnalyzers
    （已核实），因此用 PowerShell 反射 + 纯文本基线替代它：

      * 基线文件位于 tools/api-baselines/<PackageId>.txt，随仓库提交；
      * 默认模式比较当前程序集与基线，输出 added/removed，任何差异 exit 1；
      * -Update 用当前程序集覆盖基线（只在有意改 API 时运行，并在 PR/commit 里 review 基线 diff）；
      * 反射使用 Avalonia 与 Arc 的已构建程序集（默认 Debug，可用 -Configuration 切换），无需 pack。

    覆盖范围：public 类型及其方法/构造函数/属性/字段/事件/运算符的名称与签名（含泛型与 Access）。
    不覆盖（刻意，PublicApiAnalyzers 覆盖，待包可用后替换）：参数默认值、可空注解、特性、
    参数名以外的元数据。见 README「API 基线」一节。

    为什么自己写反射脚本：机器无 nuget.org 访问，不能新增分析器包；而 Roslyn 包虽在缓存，
    解析程序集并重建符号需要额外工具工程，反射方案对 M1 的 26 个包足够且零依赖。

.PARAMETER Configuration
    读取 bin/<Configuration>/net10.0 下的程序集，默认 Debug。

.PARAMETER Update
    重写基线文件（有意变更 API 时使用）。

.PARAMETER Package
    只检查指定 PackageId（可重复传或用逗号分隔）。

.PARAMETER Quiet
    只在有差异/错误时输出明细。

.EXAMPLE
    pwsh -NoProfile tools/build.ps1
    pwsh -NoProfile tools/check-public-api.ps1            # 校验
    pwsh -NoProfile tools/check-public-api.ps1 -Update   # 重建基线

.EXITCODES
    0  与基线一致；1  有差异或缺少基线/程序集；2  脚本自身错误。
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$Update,
    [string[]]$Package = @(),
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$baselineDir = Join-Path $PSScriptRoot 'api-baselines'
$outDir = Join-Path $root '.recon/gates'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$utf8 = New-Object System.Text.UTF8Encoding($false)

# ---------------------------------------------------------------------------
# Discover publishable projects: <IsPackable>true</IsPackable> in csproj.
# ---------------------------------------------------------------------------
function Get-PublishProjects {
    $projects = @()
    foreach ($file in Get-ChildItem -Recurse -File -Filter *.csproj (Join-Path $root 'src')) {
        if ($file.FullName -match '\\(bin|obj)\\') { continue }
        $text = [IO.File]::ReadAllText($file.FullName)
        if ($text -notmatch '<IsPackable>true</IsPackable>') { continue }
        $asm = if ($text -match '<AssemblyName>([^<]+)</AssemblyName>') { $Matches[1] } else { $file.BaseName }
        $pkgId = if ($text -match '<PackageId>([^<]+)</PackageId>') { $Matches[1] } else { $asm }
        $projects += [pscustomobject]@{
            Path         = $file.FullName
            Directory    = $file.DirectoryName
            AssemblyName = $asm
            PackageId    = $pkgId
        }
    }
    return $projects | Sort-Object PackageId
}

$projects = @(Get-PublishProjects)
if ($projects.Count -eq 0) {
    Write-Output 'PUBLIC API: ERROR - 未找到 IsPackable=true 的工程'
    exit 2
}
if ($Package.Count -gt 0) {
    $wanted = @($Package | ForEach-Object { $_.Split(',') } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $projects = @($projects | Where-Object { $wanted -contains $_.PackageId })
    if ($projects.Count -eq 0) {
        Write-Output "PUBLIC API: ERROR - 没有匹配的 PackageId: $($wanted -join ', ')"
        exit 2
    }
}

# Assembly resolution: Arc peers from every project's output, Avalonia from the Gallery output
# (exe builds copy the full runtime closure) and finally the offline NuGet cache.
$searchDirs = [System.Collections.Generic.List[string]]::new()
foreach ($proj in $projects) {
    $bin = Join-Path $proj.Directory "bin/$Configuration/net10.0"
    if (Test-Path -LiteralPath $bin) { $searchDirs.Add($bin) }
}
$galleryBin = Join-Path $root "src/Apps/Arc.Avalonia.Gallery/bin/$Configuration/net10.0"
if (Test-Path -LiteralPath $galleryBin) { $searchDirs.Add($galleryBin) }
$nugetRoots = @(
    (Join-Path $env:USERPROFILE '.nuget/packages'),
    'D:\.nuget\packages',
    (Join-Path $HOME '.nuget/packages')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique

$resolverCache = @{}
$resolverHandler = [System.Func[System.Runtime.Loader.AssemblyLoadContext, System.Reflection.AssemblyName, System.Reflection.Assembly]]{
    param($context, $name)
    $simple = $name.Name
    foreach ($dir in $searchDirs) {
        $candidate = Join-Path $dir "$simple.dll"
        if (Test-Path -LiteralPath $candidate) { return [System.Reflection.Assembly]::LoadFrom($candidate) }
    }
    foreach ($nugetRoot in $nugetRoots) {
        $hits = Get-ChildItem -Path $nugetRoot -Filter "$simple.dll" -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\lib\\net(10\.0|standard2\.0)\\' } | Select-Object -First 1
        if ($hits) { return [System.Reflection.Assembly]::LoadFrom($hits.FullName) }
    }
    return $null
}
[void][System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($resolverHandler)

# ---------------------------------------------------------------------------
# Reflection-based API text
# ---------------------------------------------------------------------------
$bindingFlags = [System.Reflection.BindingFlags]'Public,Instance,Static,DeclaredOnly'

function Format-Type([Type]$type) {
    if ($null -eq $type) { return 'void' }
    if ($type.IsGenericParameter) { return $type.Name }
    if ($type.IsByRef) { return 'ref ' + (Format-Type $type.GetElementType()) }
    if ($type.IsPointer) { return (Format-Type $type.GetElementType()) + '*' }
    if ($type.IsArray) {
        $rank = $type.GetArrayRank()
        $suffix = if ($rank -gt 1) { '[' + (',' * ($rank - 1)) + ']' } else { '[]' }
        return (Format-Type $type.GetElementType()) + $suffix
    }
    if ($type.IsConstructedGenericType) {
        $definition = $type.GetGenericTypeDefinition()
        $baseName = Format-TypeName $definition
        $baseName = [regex]::Replace($baseName, '`\d+$', '')
        $args = @($type.GetGenericArguments() | ForEach-Object { Format-Type $_ })
        return "$baseName<$($args -join ', ')>"
    }
    return (Format-TypeName $type)
}

function Format-TypeName([Type]$type) {
    $name = if ($type.FullName) { $type.FullName.Replace('+', '.') } else { $type.Name }
    $tick = $name.IndexOf('`')
    if ($tick -ge 0 -and -not $type.IsGenericTypeDefinition) {
        # Nested constructed names keep their arity marker only on the definition path.
        $name = [regex]::Replace($name, '`\d+', '')
    }
    return $name
}

function Format-Parameters([System.Reflection.ParameterInfo[]]$parameters) {
    $parts = foreach ($parameter in $parameters) {
        $prefix = if ($parameter.IsOut) { 'out ' } elseif ($parameter.ParameterType.IsByRef) { 'ref ' } else { '' }
        $parameterType = if ($parameter.ParameterType.IsByRef) { $parameter.ParameterType.GetElementType() } else { $parameter.ParameterType }
        $prefix + (Format-Type $parameterType) + ' ' + $parameter.Name
    }
    return '[' + (@($parts) -join ', ') + ']'
}

function Get-TypeModifiers([Type]$type) {
    $mods = @()
    if ($type.IsAbstract -and $type.IsSealed) { $mods += 'static' }
    elseif ($type.IsAbstract -and -not $type.IsInterface) { $mods += 'abstract' }
    elseif ($type.IsSealed -and -not $type.IsValueType) { $mods += 'sealed' }
    if ($type.IsValueType -and -not $type.IsEnum) { $mods += 'struct' }
    if ($type.IsEnum) { $mods += 'enum' }
    if ($type.IsInterface) { $mods += 'interface' }
    if ($mods.Count -eq 0) { $mods += 'class' }
    return (@($mods) -join ' ')
}

function Get-ApiText([System.Reflection.Assembly]$assembly) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $types = @()
    try { $types = @($assembly.GetTypes()) }
    catch [System.Reflection.ReflectionTypeLoadException] { $types = @($_.Exception.Types | Where-Object { $_ }) }

    foreach ($type in $types) {
        if (-not ($type.IsPublic -or $type.IsNestedPublic)) { continue }
        if ($type.IsSpecialName) { continue }
        $typeName = Format-Type $type
        $kind = Get-TypeModifiers $type
        $base = ''
        if ($type.BaseType -and $type.BaseType -ne [object] -and -not $type.IsEnum) {
            $base = ' : ' + (Format-Type $type.BaseType)
        }
        $interfaces = @($type.GetInterfaces() | ForEach-Object { Format-Type $_ } | Sort-Object)
        if ($interfaces.Count -gt 0) {
            if ($base) { $base += ', ' + ($interfaces -join ', ') } else { $base = ' : ' + ($interfaces -join ', ') }
        }
        $lines.Add("type $kind $typeName$base")

        foreach ($ctor in @($type.GetConstructors($bindingFlags) | Sort-Object { $_.ToString() })) {
            $lines.Add("  ctor " + $typeName + (Format-Parameters $ctor.GetParameters()))
        }
        foreach ($method in @($type.GetMethods($bindingFlags) | Sort-Object { $_.Name + '(' + (($_.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ',') + ')' })) {
            # Property/event accessors are represented by the property/event lines; operators are API.
            if ($method.IsSpecialName -and -not $method.Name.StartsWith('op_')) { continue }
            $generic = if ($method.IsGenericMethodDefinition) { '<' + (($method.GetGenericArguments() | ForEach-Object { $_.Name }) -join ',') + '>' } else { '' }
            $lines.Add("  method " + (Format-Type $method.ReturnType) + ' ' + $method.Name + $generic + (Format-Parameters $method.GetParameters()))
        }
        foreach ($property in @($type.GetProperties($bindingFlags) | Sort-Object Name)) {
            $accessors = @()
            if ($property.GetMethod -and $property.GetMethod.IsPublic) { $accessors += 'get' }
            if ($property.SetMethod -and $property.SetMethod.IsPublic) { $accessors += 'set' }
            if ($accessors.Count -eq 0) { continue }
            $lines.Add("  property " + (Format-Type $property.PropertyType) + ' ' + $property.Name + ' {' + ($accessors -join ';') + '}')
        }
        foreach ($field in @($type.GetFields($bindingFlags) | Sort-Object Name)) {
            if ($field.IsSpecialName) { continue }
            $static = if ($field.IsStatic) { 'static ' } else { '' }
            $lines.Add("  field $static" + (Format-Type $field.FieldType) + ' ' + $field.Name)
        }
        foreach ($event in @($type.GetEvents($bindingFlags) | Sort-Object Name)) {
            $lines.Add("  event " + (Format-Type $event.EventHandlerType) + ' ' + $event.Name)
        }
    }

    return @($lines | Sort-Object -CaseSensitive)
}

# ---------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------
if (-not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
if (-not (Test-Path -LiteralPath $baselineDir)) { New-Item -ItemType Directory -Force -Path $baselineDir | Out-Null }

$results = @()
$failures = 0

foreach ($proj in $projects) {
    $assemblyPath = Join-Path $proj.Directory "bin/$Configuration/net10.0/$($proj.AssemblyName).dll"
    if (-not (Test-Path -LiteralPath $assemblyPath)) {
        Write-Output "PUBLIC API: 缺少程序集 $assemblyPath（先 pwsh tools/build.ps1）"
        $failures++
        continue
    }

    $assembly = $null
    try { $assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath) }
    catch {
        Write-Output "PUBLIC API: 加载失败 $($proj.PackageId): $($_.Exception.Message)"
        $failures++
        continue
    }

    $current = @(Get-ApiText $assembly)
    $baselinePath = Join-Path $baselineDir "$($proj.PackageId).txt"
    $status = 'MATCH'
    $added = @()
    $removed = @()

    if ($Update) {
        $header = @(
            "# Arc for Avalonia public API baseline - $($proj.PackageId)",
            "# Generated by tools/check-public-api.ps1 -Update. Review this diff like any other source change.",
            "# One line per public type/member; do not edit by hand.",
            ''
        )
        Set-Content -LiteralPath $baselinePath -Value (@($header) + $current) -Encoding utf8
        $status = 'UPDATED'
    }
    elseif (-not (Test-Path -LiteralPath $baselinePath)) {
        $status = 'MISSING-BASELINE'
        $failures++
    }
    else {
        $baseline = @(Get-Content -LiteralPath $baselinePath | Where-Object { $_ -and -not $_.StartsWith('#') })
        $added = @($current | Where-Object { $baseline -notcontains $_ })
        $removed = @($baseline | Where-Object { $current -notcontains $_ })
        if ($added.Count -gt 0 -or $removed.Count -gt 0) {
            $status = 'DRIFT'
            $failures++
        }
    }

    $results += [pscustomobject]@{
        PackageId  = $proj.PackageId
        Assembly   = $proj.AssemblyName
        ApiLines   = $current.Count
        Status     = $status
        Added      = $added.Count
        Removed    = $removed.Count
        AddedLines = @($added | Select-Object -First 20)
        RemovedLines = @($removed | Select-Object -First 20)
    }

    if (-not $Quiet -or $status -in 'DRIFT', 'MISSING-BASELINE') {
        Write-Output ('{0,-34} {1,-14} api={2,5} added={3,3} removed={4,3}' -f $proj.PackageId, $status, $current.Count, $added.Count, $removed.Count)
        if ($status -eq 'DRIFT') {
            foreach ($line in ($added | Select-Object -First 12)) { Write-Output "  + $line" }
            foreach ($line in ($removed | Select-Object -First 12)) { Write-Output "  - $line" }
        }
    }
}

$status = if ($failures -eq 0) { 'GREEN' } else { 'RED' }
$report = [ordered]@{
    gate        = 'API'
    name        = 'public-api'
    title       = 'public API 与基线一致（轻量替代 PublicApiAnalyzers）'
    status      = $status
    generatedAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
    configuration = $Configuration
    baselineDir = (Join-Path 'tools' 'api-baselines')
    packages    = @($results)
    summary     = [ordered]@{ checked = $projects.Count; failing = $failures }
}
$reportJson = $report | ConvertTo-Json -Depth 8
Set-Content -LiteralPath (Join-Path $outDir "public-api-$stamp.json") -Value $reportJson -Encoding utf8
Set-Content -LiteralPath (Join-Path $outDir 'public-api-latest.json') -Value $reportJson -Encoding utf8

if (-not $Quiet) {
    Write-Output ''
    Write-Output "PUBLIC API: status=$status packages=$($projects.Count) failing=$failures configuration=$Configuration"
    if ($Update) { Write-Output "基线已写入 tools/api-baselines/（$($projects.Count) 个包）" }
    Write-Output "report: .recon/gates/public-api-$stamp.json"
}

exit $(if ($failures -eq 0) { 0 } else { 1 })
