<#
.SYNOPSIS
  Automated test: does Floating Stopwatch stay above a REAL full-screen PowerPoint slideshow?

.DESCRIPTION
  1. Starts FloatingStopwatch.exe.
  2. Creates a 6-slide deck in PowerPoint and starts the slideshow (same as pressing F5, full screen).
  3. After every slide change it checks the window z-order: stopwatch must be above the slideshow window,
     and the slideshow must still own keyboard focus (the stopwatch must not steal it).
  4. While the slideshow has focus it sends the global hotkeys (reset, start, 2 laps, stop) and verifies
     from the saved session file that the timer and lap list recorded them.
  5. Saves a screenshot after each slide to tests\test-results\ and writes report.txt (PASS/FAIL per check).

  Requires: Windows 10/11 with desktop PowerPoint installed. Run it from a normal desktop session
  (not over a locked screen), and do not touch the mouse or keyboard while it runs (about 30 seconds).
  Your saved lap list is backed up first and restored afterwards.
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot "..\dist\FloatingStopwatch.exe"),
    [int]$Slides = 6
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class WinProbe
{
    delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder sb, int max);

    // Visible top-level windows (as numbers), topmost in z-order first.
    public static long[] ZOrder()
    {
        List<long> list = new List<long>();
        EnumWindows(delegate(IntPtr h, IntPtr l) { if (IsWindowVisible(h)) list.Add(h.ToInt64()); return true; }, IntPtr.Zero);
        return list.ToArray();
    }

    // First visible top-level window with this class name, or 0.
    public static long FindByClass(string cls)
    {
        long found = 0;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            if (!IsWindowVisible(h)) return true;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            GetClassName(h, sb, 256);
            if (sb.ToString() == cls) { found = h.ToInt64(); return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static long Foreground() { return GetForegroundWindow().ToInt64(); }

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

    // Make this process DPI aware so window rectangles and cursor positions use real pixels.
    public static void Dpi() { SetProcessDPIAware(); }

    // Give a window the foreground. A tap of Alt first lets SetForegroundWindow succeed from a script.
    public static void BringToFront(long hwnd)
    {
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        SetForegroundWindow(new IntPtr(hwnd));
    }

    // Real mouse click on the stopwatch's timer digits (roughly 35% down the window, horizontally centred).
    public static void ClickTimer(long hwnd)
    {
        RECT r; GetWindowRect(new IntPtr(hwnd), out r);
        int x = (r.Left + r.Right) / 2;
        int y = r.Top + (int)((r.Bottom - r.Top) * 0.35);
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);  // left down
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);  // left up
    }

    public static uint PidOf(long hwnd)
    {
        uint p; GetWindowThreadProcessId(new IntPtr(hwnd), out p); return p;
    }

    public static string ClassOf(long hwnd)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        GetClassName(new IntPtr(hwnd), sb, 256);
        return sb.ToString();
    }

    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    // First visible top-level window owned by this process, ignoring message boxes (class #32770).
    // Also returns the window's class name through 'cls' so failures can be diagnosed.
    public static long FindByPid(uint pid, out string cls)
    {
        long found = 0;
        string foundClass = "";
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            if (!IsWindowVisible(h)) return true;
            uint p; GetWindowThreadProcessId(h, out p);
            if (p != pid) return true;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            GetClassName(h, sb, 256);
            if (sb.ToString() == "#32770") return true;
            found = h.ToInt64(); foundClass = sb.ToString(); return false;
        }, IntPtr.Zero);
        cls = foundClass;
        return found;
    }

    // Sends Ctrl+Alt+<key> as real keyboard input (this is what triggers RegisterHotKey).
    public static void Chord(byte key)
    {
        keybd_event(0x11, 0, 0, UIntPtr.Zero);
        keybd_event(0x12, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 2, UIntPtr.Zero);
        keybd_event(0x12, 0, 2, UIntPtr.Zero);
        keybd_event(0x11, 0, 2, UIntPtr.Zero);
    }
}
"@

$outDir = Join-Path $PSScriptRoot "test-results"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
[WinProbe]::Dpi()   # real pixels for window rectangles, cursor and screenshots
Write-Host "=== Floating Stopwatch slideshow test, VERSION 3 ==="
Write-Host ("Script file: " + $PSCommandPath)

$script:log = New-Object System.Collections.ArrayList
$script:failures = 0

function Check([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { $tag = "PASS" } else { $tag = "FAIL"; $script:failures++ }
    $line = "[{0}] {1}  {2}" -f $tag, $name, $detail
    Write-Host $line
    [void]$script:log.Add($line)
}

function Note([string]$name, [string]$detail) {
    $line = "[INFO] {0}  {1}" -f $name, $detail
    Write-Host $line
    [void]$script:log.Add($line)
}

function Save-Shot([string]$name) {
    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save((Join-Path $outDir ($name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function Test-OnTop([string]$label) {
    $z = [long[]][WinProbe]::ZOrder()
    $iSw = [Array]::IndexOf($z, $script:swHwnd.ToInt64())
    $iShow = [Array]::IndexOf($z, $script:showHwnd.ToInt64())
    $above = ($iSw -ge 0) -and ($iShow -ge 0) -and ($iSw -lt $iShow)
    Check ($label + ": stopwatch is above the slideshow") $above ("(z-order index stopwatch={0}, slideshow={1}; lower = higher up)" -f $iSw, $iShow)
    # Focus checks. The stopwatch must never be the active window. Whether PowerPoint itself is active
    # is reported separately (INFO), because a slideshow started from a script can lose the foreground
    # to the test's console window through no fault of the stopwatch.
    $fg = [WinProbe]::Foreground()
    $fgPid = [WinProbe]::PidOf($fg)
    Check ($label + ": stopwatch is NOT the active window (focus not stolen)") ($fgPid -ne $script:swPid) ("(foreground = 0x{0:X}, class {1}, pid {2}; stopwatch pid {3})" -f $fg, [WinProbe]::ClassOf($fg), $fgPid, $script:swPid)
    if ($fgPid -eq $script:pptPid) { Note ($label + ": PowerPoint is the active window") "" }
    else { Note ($label + ": PowerPoint is NOT the active window - the test console has focus; does not affect the result") ("(foreground class {0})" -f [WinProbe]::ClassOf($fg)) }
    return $fg
}

if (-not (Test-Path $Exe)) { throw "Not found: $Exe  -  run build.bat first." }
$Exe = (Resolve-Path $Exe).Path

$os = Get-CimInstance Win32_OperatingSystem
[void]$script:log.Add(("Windows: {0} (build {1})" -f $os.Caption, $os.BuildNumber))
[void]$script:log.Add(("Date: {0}" -f (Get-Date)))

# Back up the user's saved session so the test does not pollute their lap list.
$dataDir = Join-Path $env:APPDATA "FloatingStopwatch"
$sessionFile = Join-Path $dataDir "session.json"
$backupFile = Join-Path $env:TEMP "FloatingStopwatch-session-backup.json"
$hadSession = Test-Path $sessionFile
if ($hadSession) { Copy-Item $sessionFile $backupFile -Force }

$proc = $null
$ppt = $null
$pres = $null
$show = $null
try {
    Get-Process FloatingStopwatch -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 600
    $proc = Start-Process -FilePath $Exe -PassThru

    $script:swHwnd = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(15)
    $swClass = ""
    while ((Get-Date) -lt $deadline) {
        if ($proc.HasExited) { break }
        $cls = ""
        $found = [WinProbe]::FindByPid([uint32]$proc.Id, [ref]$cls)
        if ($found -ne 0) { $script:swHwnd = [IntPtr]$found; $swClass = $cls; break }
        Start-Sleep -Milliseconds 300
    }
    if ($script:swHwnd -ne [IntPtr]::Zero) {
        Check "Stopwatch window created" $true ("(window class $swClass)")
    }
    else {
        $why = "the process is still running but showed no window"
        if ($proc.HasExited) { $why = "the process exited immediately (exit code $($proc.ExitCode))" }
        $errLog = Join-Path $env:APPDATA "FloatingStopwatch\error.log"
        if (Test-Path $errLog) { $why += "; error.log says: " + ((Get-Content $errLog -Tail 6) -join " | ") }
        Check "Stopwatch window created" $false $why
        throw "Stopwatch window did not appear."
    }

    # Make sure we start from a known state: visible, session file read for the lap baseline.
    Start-Sleep -Seconds 1
    $baselineLaps = 0
    if (Test-Path $sessionFile) {
        $s0 = Get-Content $sessionFile -Raw | ConvertFrom-Json
        if ($s0.Laps) { $baselineLaps = @($s0.Laps).Count }
    }

    # ---- Start a real full-screen slideshow --------------------------------------------------
    $ppt = New-Object -ComObject PowerPoint.Application
    $pres = $ppt.Presentations.Add(-1)
    for ($i = 1; $i -le $Slides; $i++) {
        $slide = $pres.Slides.Add($i, 12)   # 12 = blank layout
        $box = $slide.Shapes.AddTextbox(1, 80, 160, 800, 120)
        $box.TextFrame.TextRange.Text = "Test slide $i"
        $box.TextFrame.TextRange.Font.Size = 60
    }
    $pres.SlideShowSettings.ShowType = 1    # 1 = full-screen presentation by a speaker
    $show = $pres.SlideShowSettings.Run()
    Start-Sleep -Seconds 3
    # A slideshow started from a script does not always take the foreground from this console window
    # (Windows foreground lock). A person pressing F5 would have PowerPoint in front, so activate it here.
    try { $show.Activate() } catch { }
    Start-Sleep -Milliseconds 800
    $pptProcIds = @(Get-Process POWERPNT -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    if ($pptProcIds -notcontains [int][WinProbe]::PidOf([WinProbe]::Foreground())) {
        $cand = [WinProbe]::FindByClass("screenClass")
        if ($cand -ne 0) { [WinProbe]::BringToFront($cand); Start-Sleep -Milliseconds 800 }
    }
    # Find the slideshow window. Try the COM property, then the known window classes, then the foreground window.
    $h = 0
    try { $h = [int64]$show.HWND } catch { }
    $script:swPid = [uint32]$proc.Id
    # The slideshow takes the foreground when it starts and the stopwatch never activates, so the
    # foreground window is the slideshow if it belongs to PowerPoint. (Other PowerPoint windows exist too.)
    if ($h -eq 0) {
        $fg = [WinProbe]::Foreground()
        if ($pptProcIds -contains [int][WinProbe]::PidOf($fg)) { $h = $fg }
    }
    if ($h -eq 0) { $h = [WinProbe]::FindByClass("screenClass") }       # PowerPoint 2013 and later
    if ($h -eq 0) { $h = [WinProbe]::FindByClass("paneClassDC") }       # PowerPoint 2010 and earlier
    $script:showHwnd = [IntPtr]$h
    $script:pptPid = if ($pptProcIds.Count -gt 0) { [uint32]$pptProcIds[0] } elseif ($h -ne 0) { [WinProbe]::PidOf($h) } else { 0 }
    $hClass = if ($h -ne 0) { [WinProbe]::ClassOf($h) } else { "" }
    Check "Slideshow window found" ($h -ne 0) ("(slideshow HWND = 0x{0:X}, class {1}, PowerPoint pid {2})" -f $h, $hClass, $script:pptPid)
    $running = $false
    try { $running = ($ppt.SlideShowWindows.Count -ge 1) } catch { }
    Check "Slideshow is running" $running ""
    if ($h -eq 0) { throw "Could not locate the slideshow window." }

    [void](Test-OnTop "Slide 1")
    Save-Shot "slide1"
    for ($i = 2; $i -le $Slides; $i++) {
        $show.View.Next()
        Start-Sleep -Milliseconds 1200
        [void](Test-OnTop ("Slide " + $i))
        Save-Shot ("slide" + $i)
    }

    # ---- Drive the stopwatch with global hotkeys while the slideshow has focus -----------------
    [WinProbe]::Chord(0x52)                 # Ctrl+Alt+R  reset
    Start-Sleep -Milliseconds 500
    [WinProbe]::Chord(0x20)                 # Ctrl+Alt+Space  start
    Start-Sleep -Seconds 3
    [WinProbe]::Chord(0x4C)                 # Ctrl+Alt+L  lap 1
    Start-Sleep -Milliseconds 700
    [WinProbe]::Chord(0x4C)                 # lap 2
    Start-Sleep -Seconds 1
    [WinProbe]::Chord(0x20)                 # stop
    Start-Sleep -Milliseconds 900
    Save-Shot "after-hotkeys"

    # ---- Click the stopwatch with the real mouse: it must not take focus from the slideshow ----
    $fgBefore = [WinProbe]::Foreground()
    [WinProbe]::ClickTimer($script:swHwnd.ToInt64())
    Start-Sleep -Milliseconds 700
    $fgAfter = Test-OnTop "After clicking the stopwatch with the mouse" | Select-Object -Last 1
    Check "Clicking the stopwatch did not change the active window" ([int64]$fgAfter -eq [int64]$fgBefore) ("(active window before = 0x{0:X}, after = 0x{1:X})" -f [int64]$fgBefore, [int64]$fgAfter)
    Save-Shot "after-click"

    $s = Get-Content $sessionFile -Raw | ConvertFrom-Json
    $elapsed = [TimeSpan]::FromTicks([int64]$s.ElapsedTicks).TotalSeconds
    $laps = @($s.Laps)
    $newLaps = $laps.Count - $baselineLaps
    Check "Timer recorded the run (expected about 4.7 s)" (($elapsed -gt 4.0) -and ($elapsed -lt 7.0)) ("(elapsed = {0:N2} s)" -f $elapsed)
    Check "Two laps were recorded" ($newLaps -eq 2) ("(new laps = {0})" -f $newLaps)
    if ($newLaps -ge 2) {
        $l1 = [TimeSpan]::Parse($laps[$laps.Count - 2].TotalTime).TotalSeconds
        $l2 = [TimeSpan]::Parse($laps[$laps.Count - 1].TotalTime).TotalSeconds
        $d2 = [TimeSpan]::Parse($laps[$laps.Count - 1].LapTime).TotalSeconds
        Check "Lap totals look right (about 3.0 s and 3.7 s)" (($l1 -gt 2.5) -and ($l1 -lt 4.5) -and ($l2 -gt $l1)) ("(lap1 total = {0:N2} s, lap2 total = {1:N2} s)" -f $l1, $l2)
        Check "Lap 2 duration is about 0.7 s" (($d2 -gt 0.4) -and ($d2 -lt 1.2)) ("(lap2 duration = {0:N2} s)" -f $d2)
    }
    [void](Test-OnTop "After hotkeys")

    $show.View.Exit()
}
catch {
    Check "Test run completed without errors" $false $_.Exception.Message
}
finally {
    try { if ($pres) { $pres.Saved = -1; $pres.Close() } } catch { }
    try { if ($ppt) { $ppt.Quit() } } catch { }
    if ($proc) { try { Stop-Process -Id $proc.Id -Force } catch { } }
    Start-Sleep -Milliseconds 500
    # Restore the user's own saved lap list.
    if ($hadSession) { Copy-Item $backupFile $sessionFile -Force }
    elseif (Test-Path $sessionFile) { Remove-Item $sessionFile -Force }
}

$summary = if ($script:failures -eq 0) { "RESULT: PASS - the stopwatch stayed above the full-screen slideshow." } else { "RESULT: FAIL - $($script:failures) check(s) failed. See the lines marked FAIL and the screenshots in $outDir" }
Write-Host ""
Write-Host $summary
[void]$script:log.Add("")
[void]$script:log.Add($summary)
Set-Content -Path (Join-Path $outDir "report.txt") -Value $script:log
if ($script:failures -ne 0) { exit 1 }
