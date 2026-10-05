# TYPR

A small Windows utility that types text into the currently focused application by generating normal keyboard input.

It is useful for Remote Desktop (RDP), virtual machines, KVM sessions, browser consoles, engineering workstations, and other environments where clipboard redirection or copy/paste is disabled but keyboard input is permitted.

> TYPR does **not** enable, modify, or bypass clipboard policy. It simply sends keyboard input, the same way physical typing would.

## Features

- Multiline text/code input
- Adjustable typing speed in characters per minute (CPM)
- Configurable start delay
- Optional typing jitter
- Global **F8** hotkey to start
- Global **F9** hotkey to stop
- Optional Enter key after completion
- Sends Enter and Tab as keyboard keys
- Unicode input through Windows `SendInput`
- No clipboard dependency
- No administrator rights required
- No network access
- No service, persistence, registry modification, or background installation

## Screenshot

A screenshot can be added later under `docs/screenshot.png`.

## Requirements

### To run a release build

No .NET installation is required if you use the self-contained release build.

### To build from source

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build

Clone the repository:

```powershell
git clone https://github.com/ITMERowe/TYPR.git
cd TYPR
```

Then either run:

```cmd
build.bat
```

or:

```powershell
dotnet publish TYPR.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false
```

The executable will be created at:

```text
bin\Release\net8.0-windows\win-x64\publish\TYPR.exe
```

## Usage

1. Open **TYPR** on the local Windows computer.
2. Paste or type your code/text into its editor.
3. Choose a typing speed. For RDP, **600–1200 CPM** is a good starting range.
4. Set the start delay, for example 3 seconds.
5. Click **Start (F8)**.
6. During the countdown, focus the target editor, terminal, or application inside the remote session.
7. The text is typed as keyboard input.
8. Press **F9** at any time to stop.

## RDP notes

Very high typing rates can overwhelm applications or remote sessions and cause dropped characters. Reduce CPM if this happens.

Tabs are sent as real `Tab` keys. In an application where Tab changes keyboard focus rather than inserting indentation, convert indentation to spaces before typing.

Newlines are sent as `Enter`.

## Security / privacy

TYPR intentionally has a minimal design:

- no network functionality
- no telemetry
- no auto-update mechanism
- no installer
- no Windows service
- no startup persistence
- no registry writes
- no encrypted payloads
- no administrator/elevation request
- no clipboard reading while typing

The source is intentionally compact so the behavior can be audited easily.

## How it works

The application uses the Windows `SendInput` API. Printable characters are sent with `KEYEVENTF_UNICODE`, while special keys such as Enter and Tab are generated as virtual-key input.

Global F8/F9 shortcuts use the Windows `RegisterHotKey` API.

See [`docs/TECHNICAL.md`](docs/TECHNICAL.md) for a short implementation overview.

## GitHub releases

The included GitHub Actions workflow builds a self-contained Windows x64 executable whenever a version tag such as `v1.0.0` is pushed.

Example:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow uploads the compiled executable as a GitHub Actions artifact and creates a GitHub Release containing `TYPR.exe`.

## Responsible use

Use this utility only on systems you are authorized to operate. Respect your organization's security controls and remote-access policies.

## License

MIT License. See [`LICENSE`](LICENSE).
