using System;
using System.Runtime.InteropServices;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>Reads the platform caps-lock state for the optional password hint.</summary>
internal static class CapsLockState
{
    private const int CapsLockVirtualKey = 0x14;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    public static bool IsOn()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return (GetKeyState(CapsLockVirtualKey) & 1) != 0;
        }
        catch
        {
            return false;
        }
    }
}
