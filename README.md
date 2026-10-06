# TYPR

<p align="center">
  <strong>Free text typing for Windows.</strong><br>
</p>

<p align="center">
  <a href="https://github.com/ITMERowe/TYPR/releases/latest">Download TYPR</a>
  ·
  <a href="https://github.com/ITMERowe/TYPR/issues">Report a problem</a>
</p>

![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/license-MIT-green)
![Price](https://img.shields.io/badge/price-free-brightgreen)

## What is TYPR?

TYPR is a small Windows utility for typing text into the currently focused app. It can help with repetitive text entry, remote desktops, virtual machines, and consoles where copy and paste may not be available.

TYPR is completely free: no paid tier, subscriptions, or ads. It has no telemetry, trackers, or network features.

## Features

- Type multiline and Unicode text using normal Windows keyboard input
- Adjust typing speed, start delay, and typing variation
- Start and stop typing with configurable global shortcuts
- Save multiple text entries; add, rename, and delete them in the editor
- Choose Light, Dark, or system-matched theme
- Optionally press Enter after typing
- Choose a portable executable or an optional installer; no separate .NET installation is required

> TYPR sends keystrokes to whichever app is focused. Check the target window before typing, and follow that app's rules. TYPR does not bypass clipboard or other system policies.

## Get started

1. From [the latest GitHub release](https://github.com/ITMERowe/TYPR/releases/latest), download either `TYPR-Portable-win-x64.exe` or `TYPR-Setup-<version>-win-x64.exe`.
2. Run the portable executable directly, or run the installer and follow its prompts. No separate .NET installation is needed.
3. Enter or select a text entry, then set the speed and start delay.
4. Start typing with **Start typing** or your configured shortcut. Focus the target app before the countdown finishes.
5. Stop with **Stop** or your configured stop shortcut.

Use the `+` beside the editor tabs to add a text entry. Select a tab to edit it, click its `x` to delete it, or double-click its name to rename it. At least one text entry must remain; only the active entry is typed.

In **Settings**, click a shortcut to assign a different key combination. TYPR follows your Windows theme until you select Light or Dark. For remote sessions, try **600–1200 CPM** and reduce the speed if characters are missed. Tabs are sent as Tab keys, so use spaces if the target app uses Tab to change focus.

## Privacy

TYPR is designed to work offline. It does not include telemetry, analytics, an auto-updater, or a background service. It does not need clipboard access to type text; it sends keyboard input through the Windows `SendInput` API.

Your saved text, shortcuts, and theme preference are stored locally in:

```text
%LOCALAPPDATA%\TYPR Kimi\settings.json
```

TYPR does not transmit that settings file. The source code is available in this repository so you can inspect how the app works or build it yourself.

## Requirements

- Windows 10 or 11, 64-bit
- No separate .NET installation for the published release

## Build from source

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then run PowerShell in the repository folder:

```powershell
.\publish.ps1
```

The self-contained Windows x64 build is written to `publish\win-x64`.

## Releases

Pushing a version tag in `vX.Y.Z` format (for example, `v1.2.3`) runs the GitHub Actions release workflow. It builds the portable executable and Inno Setup installer, creates SHA-256 checksums, and attaches the files to a GitHub Release.

## Contributing

Bug reports and practical feature requests are welcome. Please include your Windows version, steps to reproduce, and what you expected to happen. For code changes, open a pull request with a short description of the change.

## License

TYPR is licensed under the [MIT License](LICENSE).

## Responsible use

Use TYPR only on systems and apps you are authorized to use, and follow applicable rules and policies. Some applications may not accept simulated keyboard input.
