# Security policy

## Supported versions

TYPR has not had its first release yet. Once it has, only the **latest release** on GitHub Releases gets security fixes.

## Reporting a vulnerability

**Please don't open a public issue for security problems.**

Report it privately through GitHub: open the repository's **Security** tab and choose **Report a vulnerability**. Include:

- what the problem is and what an attacker could do with it;
- steps to reproduce, or a proof of concept;
- the TYPR version and your Windows version.

You can expect an acknowledgement within 7 days and an assessment within 30 days. Once a fix is released, you'll be credited in the changelog unless you'd rather not be.

## What's in scope

- The app: keyboard input it sends, its global shortcuts, the settings file and how it's read and written.
- The portable executable and the installer, and the code, build and release workflows in this repository.

## Security model, for context

- TYPR is offline by design: no network code, no server, no account, no telemetry, no auto-updater.
- It runs with normal user rights and never asks for elevation. Windows does not let it type into windows running as administrator.
- It sends keystrokes only while typing, only to the focused window, and only the text in the active tab. It doesn't use the clipboard and doesn't record keystrokes; the global shortcuts listen only for the start and stop combinations.
- Saved texts are in `%LOCALAPPDATA%\TYPR Kimi\settings.json`, **plain unencrypted JSON** readable by anything running as your Windows user. Writes go through a temporary file, so a crash can't leave it half-written. If the file can't be read, TYPR warns and starts with default settings.
- The release executables are **not code-signed** (Windows SmartScreen warns on first run). Each GitHub Release includes SHA-256 checksums of its files.

TYPR is intentionally designed without privileged services, persistence mechanisms, remote control, credential collection or hidden payloads. Changes should keep it that way unless the maintainer documents a different decision.

Reports about these known properties (e.g. "the settings file is readable") are still welcome if you see a practical attack.
