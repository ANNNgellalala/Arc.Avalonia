param(
    [string]$Project = '',
    [switch]$NoRestore,
    [switch]$NoLock,
    [string[]]$ExtraArgs = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$lock = Join-Path $root '.build.lock'
$deadline = (Get-Date).AddMinutes(15)
$acquired = $false
while (-not $NoLock -and ((Get-Date) -lt $deadline)) {
    try {
        New-Item -ItemType Directory -Path $lock -ErrorAction Stop | Out-Null
        $acquired = $true; $NoLock = $true
        break
    }
    catch {
        Start-Sleep -Milliseconds 700
    }
}
if (-not $acquired -and -not $NoLock) { Write-Output 'BUILD LOCK TIMEOUT'; exit 3 }

try {
    $target = if ([string]::IsNullOrWhiteSpace($Project)) { Join-Path $root 'ArcUI.slnx' } else { $Project }
    $buildArgs = @('build', $target, '--nologo', '-m:1', '-v', 'q', '-nodeReuse:false')
    if ($NoRestore) { $buildArgs += '--no-restore' }
    $buildArgs += $ExtraArgs

    # Keep this a direct call: PowerShell closes the pipeline when dotnet exits. The infinite-hang bug
    # seen earlier came from MSBuild node reuse leaving worker processes that inherited the build's
    # stdout pipe, so the pipeline waited for EOF forever; -nodeReuse:false removes that cause.
    & dotnet @buildArgs
    exit $LASTEXITCODE
}
finally {
    Remove-Item -Recurse -Force $lock -ErrorAction SilentlyContinue
}
