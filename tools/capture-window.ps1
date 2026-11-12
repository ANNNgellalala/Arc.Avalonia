# Captures a top-level window of a process to a PNG, optionally resizing it first.
# Usage: pwsh -File capture-window.ps1 -ProcessName DshRemote.App.Desktop -Out shots/out.png [-Width 412 -Height 915] [-WaitSeconds 8]
param(
    [Parameter(Mandatory = $true)][string]$ProcessName,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Width = 0,
    [int]$Height = 0,
    [int]$WaitSeconds = 6
)

Add-Type -AssemblyName System.Drawing

$src = @'
using System;
using System.Runtime.InteropServices;
public static class WinCapture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
}
'@
Add-Type -TypeDefinition $src

$deadline = (Get-Date).AddSeconds(30)
$proc = $null
while ((Get-Date) -lt $deadline) {
    $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($proc) { break }
    Start-Sleep -Milliseconds 400
}
if (-not $proc) { throw "No window found for process '$ProcessName'" }

Start-Sleep -Seconds $WaitSeconds
$hwnd = $proc.MainWindowHandle
if ($hwnd -eq 0) { throw "MainWindowHandle is 0" }

if ($Width -gt 0 -and $Height -gt 0) {
    [WinCapture]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE
    [WinCapture]::SetWindowPos($hwnd, [IntPtr]::Zero, 40, 40, $Width, $Height, 0x0040) | Out-Null
    Start-Sleep -Milliseconds 900
}

$rect = New-Object WinCapture+RECT
[WinCapture]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
if ($w -le 0 -or $h -le 0) { throw "Bad window rect $w x $h" }

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $gfx.GetHdc()
$ok = [WinCapture]::PrintWindow($hwnd, $hdc, 2)   # PW_RENDERFULLCONTENT
$gfx.ReleaseHdc($hdc)
$gfx.Dispose()

$full = [System.IO.Path]::GetFullPath($Out)
$dir = [System.IO.Path]::GetDirectoryName($full)
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$bmp.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "saved $full ($w x $h) printwindow=$ok"
