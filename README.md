# Floating Stopwatch for PowerPoint

An always-on-top stopwatch / countdown for Windows 10 and 11 that stays visible over a full-screen
PowerPoint slideshow without taking keyboard focus from it.

> **Status:** the source was written without access to a Windows machine or the .NET SDK, so it has
> **not been compiled or run yet**. Build it with `build.bat`, then run `tests\Run-Test.bat`
> (an automated F5 slideshow test) before relying on it in a live presentation.

## 1. Build and launch (about 2 minutes)

1. Install the **.NET 10 SDK** (https://dotnet.microsoft.com/download). Only needed to build.
2. Double-click **`build.bat`**. It creates `dist\FloatingStopwatch.exe`, a single standalone file
   that runs on any Windows 10/11 x64 PC with nothing else installed.
   - `build.bat small` makes a ~1 MB exe instead, but that PC then needs the .NET 10 Desktop Runtime.
   - If you only have the .NET 8 SDK, change `net10.0-windows` to `net8.0-windows` in
     `FloatingStopwatch\FloatingStopwatch.csproj` (and the "10" to "8" in the text above).
3. Double-click `dist\FloatingStopwatch.exe` to run it. No installation is needed; copy the exe anywhere.

**Optional installer:** install Inno Setup 6.3+, open `installer\FloatingStopwatch.iss`, press Compile.
You get `installer-output\FloatingStopwatch-Setup.exe` with Start-menu, desktop and start-with-Windows options.

## 2. Verify it over a real full-screen slideshow

Run **`tests\Run-Test.bat`** on a PC with desktop PowerPoint. It builds a 6-slide deck, starts the
slideshow full screen (like F5), changes slides, and after each change checks that the stopwatch is above the
slideshow *and* that PowerPoint still has keyboard focus. It then sends the global hotkeys while the slideshow
is in front and checks the saved timer and laps. Output: PASS/FAIL per check, a screenshot per slide, and
`tests\test-results\report.txt`. Your own saved lap list is backed up and restored.

## 3. Using it during a presentation

1. Start `FloatingStopwatch.exe` **before or after** pressing F5. Either order works.
2. Drag it where you want it (grab the timer or the top strip). Tick **Lock position** in Settings so you
   cannot move it by accident. Use the size slider and **Compact mode** (timer only) to make it unobtrusive.
3. During the show, **control it with the global hotkeys** (they work while PowerPoint has focus):

| Hotkey | Action |
|---|---|
| Ctrl+Alt+Space | Start / Stop (Stop pauses, Start again continues) |
| Ctrl+Alt+L | Lap / mark |
| Ctrl+Alt+R | Reset to 00:00:00 |
| Ctrl+Alt+H | Hide / show the stopwatch |

You can also click its buttons with the mouse. Clicks do not take focus from the slideshow, so arrow keys and
Space keep advancing slides.

**About plain Space / L / R / Esc:** a window that never takes focus cannot receive keystrokes, and plain Space
cannot be a global hotkey because PowerPoint uses it to advance slides. So the plain keys work only when the
stopwatch has focus: tick *Take keyboard focus when clicked* in Settings (then click the stopwatch, and click the
slide again afterwards to give PowerPoint focus back). Esc minimizes the stopwatch to the taskbar.

## 4. Controls

- **START / CONTINUE**: starts from zero, or resumes after STOP. **STOP** pauses. **LAP** records the current
  time without stopping. **RESET** returns to 00:00:00 and stops; the next START begins from zero.
- **RESET + START**: back to zero and running immediately.
- **NEW SESSION**: resets the clock and starts "Session n+1". The lap list is kept, with a Session column.
- **COUNTDOWN MODE**: set the length with the -1 min / -10 s / +10 s / +1 min buttons or in Settings. START,
  STOP/CONTINUE and RESET work as above. At zero the window flashes red ("TIME'S UP") with an optional sound.
  LAP is for the stopwatch only.
- **Lap list** (the list icon or right-click menu): No., Session, Activity (double-click to rename), Time
  (this lap), Total Elapsed. Delete selected, Clear all, Export CSV (opens directly in Excel).
  Times are shown to 0.01 s. The list is saved automatically and kept until you clear it.
- **Right-click** the timer for every option, including Compact mode, which hides everything but the digits.
- **Pin indicator** (top left): green "ON TOP" or grey "NOT ON TOP"; click it to toggle.
- **Settings** (gear icon): size, font size, text colour, background colour, background opacity, show/hide
  buttons, lock, compact, always on top, hotkeys, sounds (master switch plus per-sound), countdown length.

## 5. Where data is stored

`%AppData%\FloatingStopwatch\settings.json` and `session.json` (timer value, lap list, session number).
The previous session is restored when you reopen the app: the lap list as it was, and the clock at the value it
had, paused. The clock does not run while the app is closed. Delete the folder to reset everything.

## 6. How the always-on-top behaviour works

- The window is a normal WPF window with the topmost flag, plus `WS_EX_NOACTIVATE` and a `WM_MOUSEACTIVATE`
  handler that returns `MA_NOACTIVATE`, so it never becomes the active window.
- PowerPoint's slideshow window is also topmost, and among topmost windows the most recently raised one wins.
  So the app re-raises itself (`SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)`) on every foreground-window change
  (`SetWinEventHook`) and every 250 ms. Starting or restarting a slideshow therefore cannot bury it for more
  than a fraction of a second.
- The clock is `System.Diagnostics.Stopwatch` (QueryPerformanceCounter), so it is monotonic and unaffected by
  focus, UI load or system clock changes. The display timer ticks only while a clock is running.

## 7. Known limits

- It cannot appear over a true exclusive-fullscreen DirectX program (some games). PowerPoint is not one.
- Windows with the special "UIAccess" privilege (for example the on-screen keyboard) can still sit above it.
- Right-click menus and the Settings / Lap windows are ordinary windows and may take focus; click the slide
  afterwards. Use the buttons and hotkeys during a show.
- Windows 10/11 x64. Not built for ARM64 or 32-bit.
- Export is CSV (Excel opens it directly); there is no native .xlsx writer.
- If another program already owns a Ctrl+Alt hotkey, that one hotkey is silently skipped; the buttons still work.
