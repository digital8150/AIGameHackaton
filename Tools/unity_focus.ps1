# Bring the running Unity Editor window to the foreground (the Editor throttles its player loop / game view
# while unfocused, so captures from `unity command game_view` can stall). Usage:
#   powershell -ExecutionPolicy Bypass -File Tools/unity_focus.ps1 [-ProjectName AIGameHackaton]
param([string]$ProjectName = "AIGameHackaton")

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
"@

$p = Get-Process Unity -ErrorAction SilentlyContinue |
     Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -like "*$ProjectName*" } |
     Select-Object -First 1
if (-not $p) { Write-Output "Unity window for '$ProjectName' not found"; exit 1 }

$h = $p.MainWindowHandle
if ([Win]::IsIconic($h)) { [Win]::ShowWindow($h, 9) | Out-Null }   # SW_RESTORE
# Windows only lets the foreground process hand focus away; a synthetic Alt tap satisfies that rule.
[Win]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
[Win]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
$ok = [Win]::SetForegroundWindow($h)
Write-Output "focused=$ok  $($p.MainWindowTitle)"
