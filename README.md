# TYPR

**100% free. No subscriptions, paid features, or fees.**

TYPR types your text into the currently focused Windows app using normal keyboard input. It’s useful for remote desktops, virtual machines, and consoles where copy and paste aren’t available.

[Download the latest release](https://github.com/ITMERowe/TYPR/releases/latest)

## Features

- Multiline text, including Unicode
- Adjustable typing speed, start delay, and typing variation
- **F8** to start; **F9** to stop
- Optional Enter key after typing
- No clipboard access, network connection, telemetry, or administrator rights

> TYPR sends keyboard input. It does not bypass or change clipboard or other system policies.

## Use

1. Download and run `TYPR.exe` from [Releases](https://github.com/ITMERowe/TYPR/releases/latest). No .NET installation is needed.
2. Enter your text and set the speed and countdown delay.
3. Press **F8** or click **Start typing**, then focus the target app before the countdown ends.
4. Press **F9** or click **Stop** to stop typing.

For remote sessions, start around **600–1200 CPM** and lower the speed if characters are dropped. Tabs are sent as Tab keys, so use spaces if the target app uses Tab to change focus.

## Build from source

Requires Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/ITMERowe/TYPR.git
cd TYPR
.\publish.ps1
```

The self-contained Windows x64 build is written to `publish\win-x64`.

## Privacy and license

TYPR has no network or telemetry features, auto-updater, installer, background service, or startup persistence. It sends keyboard input through the Windows `SendInput` API.

Licensed under the [MIT License](LICENSE).

Use TYPR only on systems you’re authorized to operate, and follow your organization’s policies.
