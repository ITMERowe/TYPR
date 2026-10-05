# Technical overview

TYPR is a small Windows Forms application targeting .NET 8 for Windows.

## Input generation

The program P/Invokes `user32.dll!SendInput`.

For normal characters it sends keyboard events using `KEYEVENTF_UNICODE`. This avoids dependence on the local keyboard layout for most text and does not use the Windows clipboard.

Special characters handled as virtual keys:

- newline → `Enter`
- tab → `Tab`

## Global hotkeys

The application registers:

- `F8` — start typing
- `F9` — stop typing

using `RegisterHotKey` and handles the resulting `WM_HOTKEY` messages in the main window procedure.

## Timing

Typing delay is calculated from the selected characters-per-minute value:

```text
milliseconds per character = 60000 / CPM
```

Optional jitter varies this delay randomly around the configured rate.

## Cancellation

Typing runs asynchronously and uses a `CancellationTokenSource`, allowing F9 or the Stop button to interrupt the operation.

## Security design

The application does not require elevated privileges and intentionally contains no persistence or networking mechanism.
