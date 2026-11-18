# Technical overview

TYPR is a small Windows Forms application targeting .NET 8 for Windows. The window, its custom-drawn controls and the typing engine live in `TYPR.cs`; tests are in `tests/TYPR.Tests`.

## Input generation

The program P/Invokes `user32.dll!SendInput`.

For normal characters it sends keyboard events using `KEYEVENTF_UNICODE`. This avoids dependence on the local keyboard layout for most text and does not use the Windows clipboard. Surrogate pairs (emoji) are sent together.

Special characters handled as virtual keys, with their scan codes:

- newline (CRLF is one Enter) → `Enter`
- tab → `Tab`

Text containing other control characters is not typed as is: TYPR offers to remove them first.

## Global shortcuts

Start and stop default to `F8` and `F9`. The user can change them (a key with modifiers; Pause, Scroll Lock and function keys may stand alone). TYPR registers them with `RegisterHotKey` and handles `WM_HOTKEY` in the main window procedure. The start shortcut is ignored while one of TYPR's own dialogs is open; Stop always works.

## Timing

Typing delay comes from the selected characters-per-minute value:

```text
milliseconds per character = 60000 / CPM
```

Optional variation changes each delay randomly by up to the chosen percentage. Keys are scheduled against a running `Stopwatch` deadline (`TypingSchedule`), so a late key is not made up with a burst. While typing, TYPR asks Windows for 1 ms timer resolution and opts out of power throttling (`TimerResolution`), so the speed holds when the window is minimized.

## Safety checks while typing

- Before the first key TYPR waits (up to 2 s) for Ctrl, Alt, Shift and Win to be released, and refuses to start if one is still held.
- It remembers the target window after the countdown and stops if another window takes focus, or if the target runs as administrator and TYPR does not.
- Stop, the stop shortcut and closing the window cancel the run through a `CancellationTokenSource`.

## Settings

`%LOCALAPPDATA%\TYPR Kimi\settings.json` holds the texts, shortcuts, theme and window size (`SettingsStore`). A save writes a temporary file, flushes it, swaps it in and keeps the previous file as `.bak`. An unreadable file is copied to `.bad` and the load falls back to the `.bak`. Speed, delay and variation are not saved.

## Interface

The UI is built in code with owner-drawn controls and one token set: `Theme` (colours, both themes), `UiSpacing` (4 px scale), `UiSize` (control heights), `UiRadius`, `UiFont`. Only one TYPR window can run at a time (a named mutex).

## Security design

The application does not require elevated privileges and intentionally contains no persistence or networking mechanism.
