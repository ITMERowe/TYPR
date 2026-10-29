# TYPR privacy policy

_Last updated: 30 October 2026_

TYPR is a Windows app that types saved text into the window you choose. This policy explains what happens to the information you put into it.

## The short version

TYPR does not collect, send or sell any data. Everything you enter stays on your computer.

## What the app stores, and where

TYPR stores your saved texts (each tab's name and text), your start and stop shortcuts, and your theme choice, in one file:

```text
%LOCALAPPDATA%\TYPR Kimi\settings.json
```

- **No network:** TYPR has no network features. There is no server, no account, no sign-in, no analytics, no advertising, no tracking and no automatic updates.
- **Plain text:** the settings file is ordinary, unencrypted JSON. Anyone or any program with access to your Windows account can read it. Don't keep passwords or other secrets in TYPR's tabs if that matters to you.
- **Not saved:** typing speed, start delay, typing variation and the "Press Enter when finished" option reset each time TYPR starts.

## What TYPR does with your text

- When you start typing, TYPR sends the active tab's text as keystrokes, through the Windows `SendInput` function, to **whichever window has focus**. What happens to the text there is up to that app and its own privacy policy.
- TYPR doesn't read or change the clipboard, doesn't read what's on screen, and doesn't record your own keystrokes. Its global shortcuts (F8 and F9 by default) only listen for the two key combinations you set.

## Deleting your data

- Delete a text tab inside TYPR to remove its text from the settings file.
- To remove everything, close TYPR and delete the `%LOCALAPPDATA%\TYPR Kimi` folder.
- Uninstalling (installer version) or deleting the portable `.exe` does **not** delete that folder, so your texts are still there if you reinstall.

## Children

TYPR is a general utility and is not directed at children. It collects no information from anyone.

## Changes to this policy

If TYPR ever starts handling data differently, this policy will be updated first, and the date at the top will change. Past versions are in this file's history on GitHub.

## Contact

Questions about this policy: open an issue at <https://github.com/ITMERowe/TYPR/issues> or contact the developer through GitHub ([ITMERowe](https://github.com/ITMERowe)).
