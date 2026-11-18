# Changelog

All notable changes to TYPR are documented in this file. Each version starts with **Highlights**: a short list for users, shown first on the GitHub Release. The sections after it are the full list; put the biggest changes first in each.

## 0.1.0 (2026-11-18)

First public release.

### Highlights
- Types your saved text into the window in front, one key at a time, for places where paste doesn't work: remote desktops, virtual machines, consoles and web forms
- Several saved texts in tabs: add, rename, delete, drag to reorder
- Your own start and stop shortcuts (F8 / F9 by default); Stop always works
- Stops by itself if the target window loses focus, and refuses to type while a modifier key is stuck
- Light and dark themes with a watermelon accent; optional line numbers; remembers the window size
- Free and private: no network, no telemetry, no clipboard. One portable exe or an installer; no separate .NET install

### Added
- Typing: speed 30–20,000 characters per minute, start delay, human-like pacing, optional Enter at the end; multiline and Unicode text including emoji
- Text tabs that are saved between runs, with a confirmation before deleting a tab that has text; Ctrl+Tab, Ctrl+Shift+Tab, Ctrl+1–9 to switch
- Global start and stop shortcuts you can change (including Win and Ctrl+Alt combinations, Pause and Scroll Lock), with a **Reset to F8 / F9** link
- Optional line numbers beside the editor (**Line numbers** under Editor, or Ctrl+Shift+L); off by default
- The window reopens at the size you closed it, and maximized if you closed it that way
- A narrow-window layout: under 780 px the sidebar becomes a drawer (button in the title bar, or Ctrl+B)
- A dialog to remove control characters from a text instead of refusing to type it
- Light and dark themes; follows Windows until you pick one
- Screen-reader support: switch, tab and slider state; announced dialogs and errors; a visible focus ring on every control

### Changed
- New look: a VS Code-style window with a compact settings sidebar, text tabs, and a footer showing status, character count with a time estimate, Stop and Start typing
- One button style, one corner radius and minimum control heights; one 4 px spacing scale; Segoe UI Semibold labels
- Speed and Start delay show their units; out-of-range values are explained in the status line
- Your own dialogs replace the Windows message boxes for settings warnings and unexpected errors
- Release exe is one compressed file carrying the release version; release notes come from this changelog

### Fixed
- Typing no longer starts while Ctrl, Alt or Shift is held down, and stops cleanly when focus leaves the target
- Only one TYPR window can run at a time, so two windows can't overwrite each other's saved texts
- A damaged `settings.json` is kept as a `.bad` copy and recovered from the previous save, never overwritten
- First launch no longer crashes when there is no settings file
- Even typing speed at any rate and when minimized (timer-resolution and deadline scheduling)
- The editor accepts texts longer than 32,767 characters and keeps pasted text whole
- Edits made after switching the theme are saved like any other
- Dialogs open over TYPR even when another app is in front, and the start shortcut is ignored while a dialog is open
- The narrow-window sidebar no longer lets keystrokes edit hidden text
- A maximized window no longer grows at each launch
- Contrast: editor placeholder, switch, slider rail and scroll thumb are readable in both themes

### Build and packaging
- Self-contained Windows x64 `TYPR.exe` from `publish.ps1`, plus an Inno Setup installer
- A tag-triggered GitHub Actions release publishes the portable exe, the installer and `SHA256SUMS.txt`
- Tests run in CI before every release
