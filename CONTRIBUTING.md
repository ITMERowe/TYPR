# Contributing to TYPR

Thanks for helping. TYPR types text into other windows where paste doesn't work, so changes are judged on one thing above all: **every character must arrive as written, Stop must always work, and saved texts must never be lost.**

Security problems go through the [security policy](SECURITY.md), not public issues.

## Before you start

- **Bugs:** open an [issue](../../issues/new) with steps to reproduce, the TYPR version, your Windows version and display scaling (e.g. 125%), and the app you were typing into.
- **Ideas and bigger changes:** open an issue first, so we can agree on the approach before you write code.
- **Small fixes** (typos, wording, obvious bugs) can go straight to a pull request.

## Set up

Prerequisites: Windows 10 or 11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet build -c Release
dotnet run --project TYPR.csproj
```

`.\publish.ps1` builds the self-contained `publish\win-x64\TYPR.exe`.

Your real saved texts live in `%LOCALAPPDATA%\TYPR Kimi\settings.json`, and a development build uses the same file. Back it up before working on settings code.

## Making a change

1. Fork the repo and create a branch from **`main`** (e.g. `fix/editor-scrolling`); open your pull request against `main`.
2. Keep the change focused: one fix or feature per pull request.
3. Follow the existing conventions in `TYPR.cs`:
   - the UI is built in code (no designer files); new controls follow the custom controls already there;
   - colours from `Theme`, spacing from `UiSpacing`, font sizes from `UiTypography`; check both light and dark;
   - every control works from the keyboard and has an accessible name;
   - ask before anything destructive (deleting a text tab with text in it).
4. Typing and data safety:
   - keep pacing even: don't send keys in bursts to catch up;
   - Stop, the stop shortcut and closing the window must end typing at once, with no key left held down;
   - don't change the settings file format without handling old files and a test;
   - nothing may need the network, the clipboard or administrator rights.
5. Add or update tests for what you changed.
6. Add a line to [CHANGELOG.md](CHANGELOG.md) for anything a user would notice.

Keep TYPR focused on transparent keyboard-input automation. Features that add hidden persistence, privilege escalation, credential collection, covert networking or similar behaviour are out of scope.

## Checks before you open a pull request

```powershell
dotnet build -c Release
```

CI builds the app on every push and pull request. Then check the flows you touched in the running app:

- light and dark theme, and switching between them;
- a long text (10,000+ characters) and several text tabs;
- 100% and 150% Windows display scaling;
- typing into Notepad at the default speed, and stopping halfway with the stop shortcut.

## Pull requests

- Explain *why*, not just *what*; link the issue it closes.
- Add screenshots for visible changes, in light and dark theme.
- Keep commits readable; they're squashed or merged as the maintainer sees fit.

## License

By contributing, you agree that your contributions are licensed under the project's [MIT License](LICENSE).
