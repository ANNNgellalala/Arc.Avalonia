param(
    [string]$Page = '',
    [string]$Theme = 'both',
    [string]$Accent = 'neutral',
    [int]$Width = 1180,
    [int]$TimeoutSec = 240,
    [switch]$Shell,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) {
    # Build the gallery without rebuilding the component projects: several agents build those in
    # parallel, and the gallery only needs their last good assembly to render pages.
    & (Join-Path $PSScriptRoot 'build.ps1') -Project (Join-Path $root 'src\Apps\Arc.Avalonia.Gallery\Arc.Avalonia.Gallery.csproj') -ExtraArgs '-p:BuildProjectReferences=false' -NoLock
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$dll = Join-Path $root 'src\Apps\Arc.Avalonia.Gallery\bin\Debug\net10.0\Arc.Avalonia.Gallery.dll'
$arguments = @($dll, '--shot', (Join-Path $root 'shots'), '--theme', $Theme, '--width', $Width, '--timeout', $TimeoutSec)
if ($Accent -ne 'neutral') { $arguments += @('--accent', $Accent) }
if (-not [string]::IsNullOrWhiteSpace($Page)) { $arguments += @('--page', $Page) }
if ($Shell) { $arguments += '--shell' }
& dotnet @arguments
exit $LASTEXITCODE



