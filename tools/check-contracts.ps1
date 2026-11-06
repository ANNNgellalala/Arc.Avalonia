#Requires -Version 7.0
<#
.SYNOPSIS
    R10 控件契约门禁：静态校验 [TemplatePart] / PART_* 命名、GetTemplateChild/FindControl 引用、
    RoutedEvent 的 CLR 包装与“双触发”反模式。支持过渡基线（M2-G3a 切片并行改造时用 allowlist 降级为 exempt）。

.DESCRIPTION
    扫描 src/{Foundation,Components,Agent}/**/*.cs（排除 Pages/、Docs/、bin/、obj/），按类范围做以下检查：
      a. 有 OnApplyTemplate 覆写且派生自 TemplatedControl 的类必须声明 ≥1 个 [TemplatePart(…)] → missing-templatepart；
      b. GetTemplateChild("X")/FindControl("X") 的 X 必须在该类声明的 TemplatePart 集合内 → undeclared-part；
      c. 每个 public static readonly RoutedEvent<…> <Name>Event 必须有合规 CLR 包装：
         类中存在同名旧 CLR 事件（event EventHandler<…> <Name>）→ 必须 `<Name>Routed`（冲突时 `<Name>RoutedEvent`）；
         不存在同名旧事件 → 允许 `<Name>Routed`/`<Name>RoutedEvent`，也允许直接同名 `<Name>`（Avalonia 惯例）；
         且 accessor 走 AddHandler(<Name>Event)/RemoveHandler(<Name>Event) → routedevent-no-wrapper；
      d. 新 *EventArgs 类型若被 RoutedEvent<…> 引用，必须派生自 RoutedEventArgs → routedevent-args-base；
      e. TemplatePart 名必须是 PART_* 前缀 → invalid-templatepart-name（命名规范，便于主题侧对应）；
      f. 旧 `EventHandler<T>` 公开事件暂时没有 RoutedEvent 对应物 → legacy-event-missing-routed（**warning/exempt，只统计不判红**）。

    说明：本门禁采用 **严格 additive + 双通知** 口径（见 ADR 0005）——同一事件既 RaiseEvent 又 Invoke
    是设计要求，不属于违规；旧 CLR 事件与 handler 签名零变更。

    过渡基线 tools/contracts-baseline.json（由 -UpdateBaseline 生成）按 kind+file+symbol 记录 allowlist：
    命中 allowlist 的项降级为 exempt（不阻塞 check-all），未登记的新违规才是 RED。

.PARAMETER Baseline
    过渡基线路径，缺省 tools/contracts-baseline.json。文件不存在时视为无 allowlist。

.PARAMETER NoBaseline
    忽略过渡基线，所有命中都按新增违规处理。

.PARAMETER UpdateBaseline
    把当前全部命中写入基线文件（覆盖），用于切片改造中途刷新 allowlist；写完 exit 0。

.PARAMETER Quiet
    只输出一行摘要（tools/check-all.ps1 使用）。

.PARAMETER OutDir
    报告目录，缺省 <repo>/.recon/gates（.recon/ 已 gitignore）。

.EXITCODES
    0  无新增违规（可能有已登记 exempt）
    1  存在新增违规
    2  无法完成检查

.EXAMPLE
    pwsh -NoProfile tools/check-contracts.ps1
    pwsh -NoProfile tools/check-contracts.ps1 -UpdateBaseline
    pwsh -NoProfile tools/check-contracts.ps1 -NoBaseline
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
if ([string]::IsNullOrWhiteSpace($Baseline)) { $Baseline = Join-Path $PSScriptRoot 'contracts-baseline.json' }
elseif (-not [System.IO.Path]::IsPathRooted($Baseline)) { $Baseline = Join-Path $root $Baseline }

# ---------------------------------------------------------------------------
# 轻量 C# 静态解析工具
# ---------------------------------------------------------------------------

# 把注释/字符串/字符字面量替换成空格（保留换行与长度），使 class/大括号匹配不受内容干扰。
function Get-CodeMaskedText {
    param([Parameter(Mandatory)][string]$Text)
    $pattern = '(?s)@"(?:""|[^"])*"|"(?:\\.|[^"\\\r\n])*"|''(?:\\.|[^''\\\r\n])*''|//[^\r\n]*|/\*.*?\*/'
    return [regex]::Replace($Text, $pattern, {
            param($m)
            return [regex]::Replace($m.Value, '[^\r\n]', ' ')
        })
}

function Get-LineNumberAt {
    param([Parameter(Mandatory)][int]$Index, [Parameter(Mandatory)][int[]]$LineStarts)
    $lo = 0
    $hi = $LineStarts.Length - 1
    while ($lo -le $hi) {
        $mid = [int](($lo + $hi) / 2)
        if ($LineStarts[$mid] -le $Index) { $lo = $mid + 1 } else { $hi = $mid - 1 }
    }
    return $hi + 1
}

function Get-CSharpClassesInFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$RelPath
    )
    $raw = Get-Content -LiteralPath $Path -Raw -Encoding utf8
    if ([string]::IsNullOrWhiteSpace($raw)) { return @() }
    $masked = Get-CodeMaskedText -Text $raw
    $lineStarts = [System.Collections.Generic.List[int]]::new()
    $lineStarts.Add(0)
    for ($i = 0; $i -lt $masked.Length; $i++) {
        if ($masked[$i] -eq "`n") { $lineStarts.Add($i + 1) }
    }
    $declRegex = [regex]'(?m)^[ \t]*(?:(?:public|internal|private|protected|sealed|abstract|partial|static|unsafe|file|new|readonly)\s+)*class\s+([A-Za-z_]\w*)'
    $classes = [System.Collections.Generic.List[object]]::new()
    foreach ($m in $declRegex.Matches($masked)) {
        $name = $m.Groups[1].Value
        $braceStart = $masked.IndexOf('{', $m.Index + $m.Length)
        if ($braceStart -lt 0) { continue }
        $depth = 0
        $end = -1
        for ($i = $braceStart; $i -lt $masked.Length; $i++) {
            $ch = $masked[$i]
            if ($ch -eq '{') { $depth++ }
            elseif ($ch -eq '}') {
                $depth--
                if ($depth -eq 0) { $end = $i; break }
            }
        }
        if ($end -lt 0) { continue }
        $header = $raw.Substring($m.Index, $braceStart - $m.Index)
        $bases = [System.Collections.Generic.List[string]]::new()
        $colon = $header.IndexOf(':')
        if ($colon -ge 0) {
            $baseText = $header.Substring($colon + 1)
            $baseText = [regex]::Replace($baseText, '(?i)\bwhere\b.*$', '')
            foreach ($bm in [regex]::Matches($baseText, '[A-Za-z_][A-Za-z0-9_.]*')) {
                $token = $bm.Value
                if ($token -in @('where', 'class', 'new', 'record', 'struct')) { continue }
                if ($token -match '^T\d*$') { continue }
                $simple = ($token -split '\.')[-1]
                if (-not $bases.Contains($simple)) { $bases.Add($simple) }
            }
        }
        $startLine = Get-LineNumberAt -Index $m.Index -LineStarts $lineStarts.ToArray()
        $endLine = Get-LineNumberAt -Index $end -LineStarts $lineStarts.ToArray()
        $body = $raw.Substring($braceStart, $end - $braceStart + 1)
        $maskedBody = $masked.Substring($braceStart, $end - $braceStart + 1)
        # 特性写在 class 声明之前（类体外），需要把声明前的连续 [TemplatePart] 块纳入扫描范围；
        # 用最近一个 }/; 作为切口，避免把上一个类的特性算进来。
        $prefixStart = [Math]::Max(0, $m.Index - 4000)
        $prefix = $raw.Substring($prefixStart, $m.Index - $prefixStart)
        $cut = $prefix.LastIndexOfAny([char[]]@('}', ';'))
        $attributeTail = if ($cut -ge 0) { $prefix.Substring($cut + 1) } else { $prefix }
        $attributeScope = $attributeTail + $body
        $parts = [System.Collections.Generic.List[string]]::new()
        foreach ($pm in [regex]::Matches($attributeScope, '\[TemplatePart\(\s*(?:"([^"]+)"|(?:Name|name)\s*=\s*"([^"]+)")')) {
            $partName = if ($pm.Groups[1].Success) { $pm.Groups[1].Value } else { $pm.Groups[2].Value }
            if (-not $parts.Contains($partName)) { $parts.Add($partName) }
        }
        $partCalls = [System.Collections.Generic.List[string]]::new()
        foreach ($cm in [regex]::Matches($body, '(?:GetTemplateChild|FindControl|NameScope\s*\.\s*Find)\s*(?:<[^<>()]*>)?\s*\(\s*"([^"]+)"')) {
            if (-not $partCalls.Contains($cm.Groups[1].Value)) { $partCalls.Add($cm.Groups[1].Value) }
        }
        $classes.Add([pscustomobject]@{
                Name            = $name
                File            = $RelPath
                StartLine       = $startLine
                EndLine         = $endLine
                Bases           = @($bases)
                TemplateParts   = @($parts)
                PartCalls       = @($partCalls)
                HasApplyTemplate = [regex]::IsMatch($maskedBody, '\boverride\s+void\s+OnApplyTemplate\s*\(')
                Body            = $body
            })
    }
    return @($classes)
}

function Test-ClassDerivesFrom {
    param(
        [Parameter(Mandatory)][object]$Class,
        [Parameter(Mandatory)][hashtable]$BaseMap,
        [Parameter(Mandatory)][string]$TargetBase,
        [System.Collections.Generic.HashSet[string]]$Visited = $null,
        [int]$Depth = 0
    )
    if ($Depth -gt 12) { return $false }
    if ($null -eq $Visited) { $Visited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase) }
    foreach ($base in @($Class.Bases)) {
        if ([string]::Equals($base, $TargetBase, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        if (-not $BaseMap.ContainsKey($base)) { continue }
        if (-not $Visited.Add($base)) { continue }
        foreach ($candidate in @($BaseMap[$base])) {
            if (Test-ClassDerivesFrom -Class $candidate -BaseMap $BaseMap -TargetBase $TargetBase -Visited $Visited -Depth ($Depth + 1)) { return $true }
        }
    }
    return $false
}

# ---------------------------------------------------------------------------
# 收集类与 RoutedEvent
# ---------------------------------------------------------------------------
$scanRoots = @('src/Foundation', 'src/Components', 'src/Agent')
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

$allClasses = [System.Collections.Generic.List[object]]::new()
$scanErrors = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    try {
        foreach ($c in Get-CSharpClassesInFile -Path $file.Path -RelPath $file.Rel) { $allClasses.Add($c) }
    }
    catch {
        $scanErrors.Add("$($file.Rel): $($_.Exception.Message)")
    }
}
# 继承图：基类名 → 声明（同名多处时合并）
$baseMap = @{}
foreach ($c in $allClasses) {
    if (-not $baseMap.ContainsKey($c.Name)) { $baseMap[$c.Name] = [System.Collections.Generic.List[object]]::new() }
    $baseMap[$c.Name].Add($c)
}

$templated = [System.Collections.Generic.List[object]]::new()
foreach ($c in $allClasses) {
    # 静态近似：OnApplyTemplate 只存在于 TemplatedControl；TemplatePart / 部件查找同样只对模板控件有意义。
    # 外部 Avalonia 基类（ContentControl/Button/TextBox…）无法在源码里解析继承链，所以用这三类证据判定，
    # 避免把 ContentControl 派生的控件误排除（或维护一份易漂移的外部基类白名单）。
    if ($c.HasApplyTemplate -or $c.TemplateParts.Count -gt 0 -or $c.PartCalls.Count -gt 0) {
        $templated.Add($c)
    }
}

function Test-ContractLegacyEvent {
    # 判断“类中是否存在同名旧 CLR 事件”：排除了自定义 add/remove 且转发 AddHandler/RemoveHandler 的路由包装，
    # 因为那种声明是 RoutedEvent 的 CLR 面（不是旧事件）。
    param(
        [Parameter(Mandatory)][object[]]$Scopes,
        [Parameter(Mandatory)][string]$Name
    )
    $pattern = [regex]('(?s)\bevent\s+[^;{]+?\b' + [regex]::Escape($Name) + '\b\s*(?:;|\{([^}]*)\})')
    foreach ($scope in $Scopes) {
        foreach ($m in $pattern.Matches($scope)) {
            $block = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { '' }
            if ($block -match 'AddHandler\s*\(' -or $block -match 'RemoveHandler\s*\(') { continue }
            return $true
        }
    }
    return $false
}

$routedEvents = [System.Collections.Generic.List[object]]::new()
$reRegex = [regex]'(?s)public\s+static\s+readonly\s+RoutedEvent<([^>]+)>\s+([A-Za-z_]\w*)\s*=\s*RoutedEvent\.Register\s*<\s*([^>]+)\s*>\s*\(\s*"([^"]+)"\s*(?:,\s*([^)]*))?\)\s*;'
foreach ($file in $files) {
    $raw = Get-Content -LiteralPath $file.Path -Raw -Encoding utf8
    foreach ($m in $reRegex.Matches($raw)) {
        $varName = $m.Groups[2].Value
        $eventName = $m.Groups[4].Value
        $argsType = $m.Groups[1].Value.Trim()
        $ownerType = $m.Groups[3].Value.Trim()
        # 包装命名规则（M2 修正）：
        #   类中存在同名旧 CLR 事件（event EventHandler<...> Foo）→ 必须 FooRouted（冲突时 FooRoutedEvent）；
        #   不存在同名旧事件 → 允许 FooRouted/FooRoutedEvent，也允许直接同名 Foo（Avalonia 惯例）。
        $baseName = $varName -replace 'Event$', ''
        $line = ($raw.Substring(0, $m.Index) -split "`n").Count
        $owner = $null
        foreach ($c in $allClasses) {
            if ($c.File -ne $file.Rel) { continue }
            if ($line -ge $c.StartLine -and $line -le $c.EndLine) { $owner = $c; break }
        }
        # 包装可能在另一个 partial 文件里：先查整份文件，再查同名 partial 类体。
        $scopeTexts = [System.Collections.Generic.List[string]]::new()
        $scopeTexts.Add($raw)
        $ownerName = if ($null -ne $owner) { $owner.Name } else { '' }
        if (-not [string]::IsNullOrWhiteSpace($ownerName)) {
            foreach ($c in $allClasses) {
                if ([string]::Equals($c.Name, $ownerName, [System.StringComparison]::OrdinalIgnoreCase) -and $c.File -ne $file.Rel) {
                    $scopeTexts.Add($c.Body)
                }
            }
        }
        $hasLegacySameName = Test-ContractLegacyEvent -Scopes $scopeTexts.ToArray() -Name $baseName
        $candidateNames = if ($hasLegacySameName) {
            @("${baseName}Routed", "${baseName}RoutedEvent")
        }
        else {
            @("${baseName}Routed", "${baseName}RoutedEvent", $baseName)
        }
        $matchedWrapper = $null
        $wrapperOk = $false
        foreach ($scopeText in $scopeTexts) {
            foreach ($candidate in $candidateNames) {
                $wrapperRegex = [regex]('(?s)\bevent\s+[^;{]+?\b' + [regex]::Escape($candidate) + '\b\s*\{([^}]*)\}')
                $wrapperMatch = $wrapperRegex.Match($scopeText)
                if (-not $wrapperMatch.Success) { continue }
                $block = $wrapperMatch.Groups[1].Value
                $hasAdd = [regex]::IsMatch($block, 'AddHandler\s*\(\s*' + [regex]::Escape($varName) + '\b')
                $hasRemove = [regex]::IsMatch($block, 'RemoveHandler\s*\(\s*' + [regex]::Escape($varName) + '\b')
                if ($hasAdd -and $hasRemove) {
                    $matchedWrapper = $candidate
                    $wrapperOk = $true
                    break
                }
            }
            if ($wrapperOk) { break }
        }
        $routedEvents.Add([pscustomobject]@{
                File        = $file.Rel
                Line        = $line
                VarName     = $varName
                EventName   = $eventName
                BaseName    = $baseName
                WrapperName = if ($null -ne $matchedWrapper) { $matchedWrapper } else { $candidateNames[0] }
                ArgsType    = $argsType
                OwnerType   = $ownerType
                OwnerClass  = if ($null -ne $owner) { $owner.Name } else { '' }
                HasLegacySameName = $hasLegacySameName
                WrapperOk   = $wrapperOk
            })
    }
}

# 旧 CLR 事件（严格 additive 策略下允许暂时没有 RoutedEvent 对应物；本波只覆盖 Top 5–10）。
$legacyEvents = [System.Collections.Generic.List[object]]::new()
$legacyEventRegex = [regex]'public\s+event\s+EventHandler\s*<[^>]+>\s*\??\s*([A-Za-z_]\w*)'
foreach ($c in $allClasses) {
    foreach ($m in $legacyEventRegex.Matches($c.Body)) {
        $name = $m.Groups[1].Value
        if ($name -match '(?i)Routed(Event)?$') { continue }   # 新包装事件不算 legacy
        $hasRouted = $false
        foreach ($ev in $routedEvents) {
            if ($ev.File -ne $c.File) { continue }
            if ([string]::Equals($ev.BaseName, $name, [System.StringComparison]::OrdinalIgnoreCase) -or
                [string]::Equals($ev.WrapperName, $name, [System.StringComparison]::OrdinalIgnoreCase)) { $hasRouted = $true; break }
        }
        $legacyEvents.Add([pscustomobject]@{ File = $c.File; Symbol = $c.Name; Name = $name; HasRouted = $hasRouted })
    }
}
$legacyWithRouted = @($legacyEvents | Where-Object { $_.HasRouted }).Count

# ---------------------------------------------------------------------------
# 规则判定
# ---------------------------------------------------------------------------
$violations = [System.Collections.Generic.List[object]]::new()
$nonPartLookups = 0
foreach ($c in $templated) {
    if ($c.HasApplyTemplate -and $c.TemplateParts.Count -eq 0) {
        $violations.Add([pscustomobject]@{ Kind = 'missing-templatepart'; File = $c.File; Symbol = $c.Name; Detail = "$($c.Name) 有 OnApplyTemplate 但缺少 [TemplatePart]"; Line = $c.StartLine })
    }
    foreach ($part in @($c.TemplateParts)) {
        if ($part -notmatch '^PART_') {
            $violations.Add([pscustomobject]@{ Kind = 'invalid-templatepart-name'; File = $c.File; Symbol = $c.Name; Detail = "$($c.Name) 的 TemplatePart '$part' 不是 PART_* 命名"; Line = $c.StartLine })
        }
    }
    foreach ($call in @($c.PartCalls)) {
        if ($call -notmatch '^PART_') { $nonPartLookups++; continue }   # 旧部件名/non-PART 查询：命名债，单列统计
        if (@($c.TemplateParts) -notcontains $call) {
            $violations.Add([pscustomobject]@{ Kind = 'undeclared-part'; File = $c.File; Symbol = $c.Name; Detail = "$($c.Name) 查找 '$call' 但未声明同名 [TemplatePart]"; Line = $c.StartLine })
        }
    }
}
foreach ($ev in $routedEvents) {
    if (-not $ev.WrapperOk) {
        $expectedText = if ($ev.HasLegacySameName) {
            "'$($ev.BaseName)Routed'（或 '$($ev.BaseName)RoutedEvent'；类中存在同名旧 CLR 事件，必须加 Routed 后缀）"
        }
        else {
            "'$($ev.BaseName)Routed'、'$($ev.BaseName)RoutedEvent' 或直接同名 '$($ev.BaseName)'"
        }
        $violations.Add([pscustomobject]@{ Kind = 'routedevent-no-wrapper'; File = $ev.File; Symbol = $ev.VarName; Detail = "$($ev.VarName) 缺少合规 CLR 包装 $expectedText：add/remove → AddHandler/RemoveHandler"; Line = $ev.Line })
    }
    # args 基类：仓库内声明的新 *EventArgs 必须派生自 RoutedEventArgs；外部类型（Avalonia 内建）静态不可判定，跳过。
    $argsSimple = ($ev.ArgsType -split '[<>,]')[0].Trim()
    if ($argsSimple -match '\.') { $argsSimple = ($argsSimple -split '\.')[-1] }
    if ($baseMap.ContainsKey($argsSimple)) {
        $argsClass = @($baseMap[$argsSimple])[0]
        if (-not (Test-ClassDerivesFrom -Class $argsClass -BaseMap $baseMap -TargetBase 'RoutedEventArgs')) {
            $violations.Add([pscustomobject]@{ Kind = 'routedevent-args-base'; File = $ev.File; Symbol = $ev.VarName; Detail = "$($ev.ArgsType) 未派生自 RoutedEventArgs（被 $($ev.VarName) 引用）"; Line = $ev.Line })
        }
    }
}
$violations = @($violations | Sort-Object File, Kind, Symbol, Detail)

$metrics = [ordered]@{
    scannedFiles            = $files.Count
    templatedControls       = $templated.Count
    withTemplatePart        = @($templated | Where-Object { $_.TemplateParts.Count -gt 0 }).Count
    routedEventFields       = $routedEvents.Count
    withWrapper             = @($routedEvents | Where-Object { $_.WrapperOk }).Count
    legacyEventsTotal       = $legacyEvents.Count
    legacyEventsWithRouted  = $legacyWithRouted
    legacyEventsMissingRouted = $legacyEvents.Count - $legacyWithRouted
    nonPartLookups          = $nonPartLookups
    missingTemplatePart     = @($violations | Where-Object { $_.Kind -eq 'missing-templatepart' }).Count
    violations              = $violations.Count
}
foreach ($kind in @('missing-templatepart', 'undeclared-part', 'invalid-templatepart-name', 'routedevent-no-wrapper', 'routedevent-args-base')) {
    $metrics[$kind] = @($violations | Where-Object { $_.Kind -eq $kind }).Count
}

# ---------------------------------------------------------------------------
# 过渡基线
# ---------------------------------------------------------------------------
if ($UpdateBaseline) {
    $allow = @()
    foreach ($v in $violations) {
        $allow += [ordered]@{
            kind   = $v.Kind
            file   = $v.File
            symbol = $v.Symbol
            reason = 'M2-G3a 契约改造进行中（-UpdateBaseline 生成的过渡 allowlist；切片完成后应清空）'
        }
    }
    $baselineObject = [ordered]@{
        '$comment'  = 'R10 契约门禁的过渡基线：按 kind+file+symbol 把“改造进行中”的命中降级为 exempt，避免堵死 check-all。切片完成后用 -UpdateBaseline 刷新，理想状态 allow=[]。'
        version     = 1
        generatedAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
        counters    = $metrics
        allow       = $allow
        notes       = @(
            '本文件不是架构豁免：R10 的“新文件/新控件”违规不在 allow 内时会照常 RED。',
            '刷新命令: pwsh -NoProfile tools/check-contracts.ps1 -UpdateBaseline',
            'ADR: docs/adr/0005-control-contract.md'
        )
    }
    Set-Content -LiteralPath $Baseline -Value ($baselineObject | ConvertTo-Json -Depth 8) -Encoding utf8
    Write-Host "CONTRACTS BASELINE UPDATED: allow=$($allow.Count) file=$([System.IO.Path]::GetRelativePath($root, $Baseline) -replace '\\', '/')"
    Write-Host "  TemplatedControls=$($metrics.templatedControls) WithTemplatePart=$($metrics.withTemplatePart) RoutedEventFields=$($metrics.routedEventFields) WithWrapper=$($metrics.withWrapper) LegacyEventsWithRouted=$($metrics.legacyEventsWithRouted) MissingTemplatePart=$($metrics.missingTemplatePart)"
    exit 0
}

$allowKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$baselineLoaded = $false
$staleAllow = 0
$baselineCounter = @{}
if (-not $NoBaseline -and (Test-Path -LiteralPath $Baseline)) {
    try {
        $baselineObject = Read-GatesJson -Path $Baseline
        foreach ($entry in @($baselineObject.allow)) {
            $key = "$([string]$entry.kind)|$([string]$entry.file)|$([string]$entry.symbol)"
            [void]$allowKeys.Add($key)
        }
        if ($null -ne $baselineObject.PSObject.Properties['counters']) {
            foreach ($p in $baselineObject.counters.PSObject.Properties) { $baselineCounter[$p.Name] = $p.Value }
        }
        $baselineLoaded = $true
    }
    catch {
        $scanErrors.Add("基线解析失败: $($_.Exception.Message)")
    }
}

# ---------------------------------------------------------------------------
# 报告
# ---------------------------------------------------------------------------
$report = New-GatesReport -Gate 'R10' -Name 'contracts' -Title '控件契约（TemplatePart / RoutedEvent）' `
    -Scope 'src/{Foundation,Components,Agent}/**/*.cs（排除 Pages/、Docs/、bin/、obj/）'
$rule = New-GatesRule -Id 'R10' -Title '控件契约静态校验' -Checked $files.Count
[void]$report.rules.Add($rule)
Add-GatesNote -Report $report -Note "过渡基线: $(if ($baselineLoaded) { '已加载 tools/contracts-baseline.json' } elseif ($NoBaseline) { '已禁用（-NoBaseline）' } else { '不存在（无 allowlist）' })"
Add-GatesNote -Report $report -Note '规则来源: docs/adr/0005-control-contract.md；对外规范: docs/18-control-contract-spec.zh.md'
if ($baselineLoaded) {
    $rule.notes.Add("allowlist 条目: $($allowKeys.Count)；命中 exempt 的改造中项不阻塞 check-all。")
}

$newCount = 0
$exemptCount = 0
$seenAllow = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($v in $violations) {
    $key = "$($v.Kind)|$($v.File)|$($v.Symbol)"
    $severity = if ($allowKeys.Contains($key)) { 'exempt' } else { 'new' }
    if ($severity -eq 'exempt') {
        $exemptCount++
        [void]$seenAllow.Add($key)
    }
    else { $newCount++ }
    $rule.items.Add([pscustomobject]@{
            severity = $severity
            kind     = $v.Kind
            project  = $v.File
            detail   = "$($v.Detail)（L$($v.Line)，$($v.Symbol)）"
            path     = $v.File
            line     = $v.Line
            symbol   = $v.Symbol
        })
}
foreach ($key in @($allowKeys)) {
    if (-not $seenAllow.Contains($key)) { $staleAllow++ }
}
# 旧 CLR 事件暂时没有 RoutedEvent 对应物：严格 additive 策略下是 warning，不计 red（只统计）。
$legacyExempt = 0
foreach ($legacy in @($legacyEvents | Where-Object { -not $_.HasRouted })) {
    $legacyExempt++
    $rule.items.Add([pscustomobject]@{
            severity = 'exempt'
            kind     = 'legacy-event-missing-routed'
            project  = $legacy.File
            detail   = "$($legacy.Symbol).$($legacy.Name) 仍为旧 CLR event（additive 迁移下波补齐，不计 red）"
            path     = $legacy.File
            line     = 0
            symbol   = $legacy.Symbol
        })
}
$rule.checked = $files.Count
$rule.notes.Add("TemplatedControls=$($metrics.templatedControls) / WithTemplatePart=$($metrics.withTemplatePart) / RoutedEventFields=$($metrics.routedEventFields) / WithWrapper=$($metrics.withWrapper) / LegacyEventsWithRouted=$($metrics.legacyEventsWithRouted) / MissingTemplatePart=$($metrics.missingTemplatePart)")
$rule.notes.Add("LegacyEvents=$($metrics.legacyEventsTotal)（withRouted=$($metrics.legacyEventsWithRouted), missingRouted=$($metrics.legacyEventsMissingRouted)，后者只统计/不阻塞）；non-PART 部件查询=$($metrics.nonPartLookups)（命名债，单列统计，不计 red）")
$rule.notes.Add("violations=$($violations.Count)（missing-templatepart=$($metrics['missing-templatepart']), undeclared-part=$($metrics['undeclared-part']), routedevent-no-wrapper=$($metrics['routedevent-no-wrapper']), routedevent-args-base=$($metrics['routedevent-args-base'])）；baseline-exempt=$exemptCount, new=$newCount")
if ($baselineLoaded) {
    $rule.notes.Add("基线已清/失效项: $staleAllow（切片完成后用 -UpdateBaseline 刷新可缩小 allowlist）")
}
if ($scanErrors.Count -gt 0) {
    foreach ($e in $scanErrors) { $rule.notes.Add("scan-warning: $e") }
}
Update-GatesRuleStatus -Rule $rule | Out-Null
$report | Add-Member -NotePropertyName metrics -NotePropertyValue $metrics
if ($scanErrors.Count -gt 0) { $rule.status = 'ERROR' }
exit (Complete-GatesReport -Report $report -OutDir $OutDir -Stamp $stamp -Quiet:$Quiet)
