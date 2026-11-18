# Changelog

All notable changes to TYPR are documented in this file. Each version starts with **Highlights**: a short list for users, shown first on the GitHub Release. The sections after it are the full list; put the biggest changes first in each.

## Unreleased

### Highlights
- New VS Code-style interface with light and dark themes; follows Windows until you pick one
- Save several texts in tabs: add, rename and delete them
- Choose your own start and stop shortcuts, or reset them to F8 / F9
- Download a portable exe or an installer; no separate .NET install needed

### Added
- Optional line numbers beside the editor text (**Line numbers** under Editor, or Ctrl+Shift+L); off by default
- **Reset to F8 / F9** link under Shortcuts
- TYPR remembers the window size (and maximized state) between launches
- Global shortcuts no longer start or stop typing while one of TYPR's own dialogs (rename, delete) is open
- VS Code-inspired dark and light interface with rounded panels, subtle borders, and compact settings controls
- Subtle windowed-mode outline matching VS Code's frame
- Set tab-container fill/border and inactive tabs to `#191A1B`; set active tabs to `#111111`
- Set the settings panel inset to 8px on the left, top, and bottom, and 4px on the right
- Set editor action-container margins to 4px left/top/right and 8px bottom
- Increased the editor action-container footer row from 48px to 53px
- Set editor action-container vertical padding to 4px while retaining 2px horizontal padding
- Increased the editor action-container bottom margin to 10px
- Disabled status-label auto-sizing so editor footer text can center within its full-height cell
- Configurable global start and stop shortcuts, with modifier combinations
- Theme preference that follows Windows until the user selects a theme
- Persistent text tabs with controls to add, delete, and rename saved text

### Changed
- Release exe is one compressed file (72 MB instead of 153 MB plus five loose DLLs) and carries the release version; build settings live in one publish profile
- Release notes come from this changelog; a tag without a matching section fails before building
- Window edges resize again, without flicker; footer buttons and character count are taller
- Promoted the editor, activity bar, settings sidebar, and action bar to the only TYPR interface
- Made typing use only the active text tab and disabled tab actions during typing
- Matched the editor tabs to VS Code: the active tab uses a distinct outlined surface, while inactive tabs sit on the darker strip
- Retained existing theme and shortcut preference storage across the app consolidation
- Made the root project and `publish.ps1` the single build and publish path for `TYPR.exe`
- Updated the README to describe the single interface and its settings
- Spacing follows one 4 px scale: sidebar values, keycaps and switch share one right edge, footer and dialog buttons sit 8 px apart and line up with the editor card, the first sidebar title shares the tab labels' baseline, the drawer matches the docked sidebar, dialog errors use the body text size, and the editor hint starts where typed text does

### Fixed
- Accessibility: the switch, tabs and slider report their state to screen readers; dialogs and inline errors are announced; stronger contrast for the editor placeholder, switch, slider rail and scroll thumb
- Footer status puts the changing part first and the character chip shortens in narrow windows or during a run
- Speed and Start delay show their units ("cpm", "s"); out-of-range values are explained in the status line; Page Up/Down step by 100 (speed) or 5 (delay)
- Tabs scroll without a native scroll bar, only the active tab is a Tab stop (arrow keys move between tabs), and new tabs get the next free "Text N" name
- Text with control characters can be cleaned from a dialog instead of being refused
- Fixed: the maximized window growing at each launch; typing starting while Ctrl/Alt/Shift is stuck; dialogs opening behind other windows; F8 starting a run while a dialog is open; the narrow-window sidebar letting keystrokes edit hidden text
- The pacing slider's thumb no longer hangs off the left end of its rail at low values
- The title-bar logo uses the theme accent instead of a separate blue
- Every button now has the key look of the shortcut keys (edge plus bottom lip), one 6 px corner radius and a minimum height (buttons 40, shortcut keys 32, text boxes 30), so none reads as a pill; the character chip is a rounded rectangle too
- Focused text boxes show a 1 px accent edge instead of a thick 2 px white ring
- New accent colour: watermelon neon #ff2e63 in both themes, with dark ink on it; accent-coloured text (the Reset link) uses a darker #d4144a on light surfaces so it stays readable
- The "End with Enter" switch no longer has jagged edges when on
- The editor no longer stops accepting text at 32,767 characters (longer pastes were silently cut)
- Labels, buttons and dialog titles use Segoe UI Semibold instead of Bold, and dialogs have a real 10 px padding on every side (the left padding was missing) and the buttons line up with the text box edge
- Inactive tabs no longer show a short floating divider; it now runs the full tab height
- The "settings could not be loaded/saved" warnings and the unexpected-error message now use TYPR's own dialog instead of the Windows message box
- Sidebar group titles ("Typing", "Shortcuts") use the stronger secondary text colour
- Corrected rounded card borders and equalized editor padding
- Added a subtle outer frame around the window when it is not maximized
- Constrained maximized window bounds to the monitor work area so the taskbar remains visible
- Centered editor footer status, metrics, and buttons with consistent spacing
- Improved settings-field alignment, shortcut keycaps, and input validation
- Hardened shortcut registration cleanup and publish failure handling
- Added a rounded editor outline along the sides and bottom while keeping the top edge open to blend into the active tab
- Added a rounded-top outline around the tab container, including its bottom edge
- Matched the active tab fill to the editor and left its bottom edge open; inactive tabs retain a bottom edge
- Exposed the tab container's rounded top corners around the tab strip content
- Clipped the tab container fill and contents to its rounded top corners
- Removed per-resize tab-container clipping to prevent repaint jitter while preserving rounded fill through transparent child backgrounds
- Composited tab-strip child painting to prevent rounded-corner repaint flicker
- Removed the tab-container and tab-flow padding so tabs meet the container border
- Set the tab container margin to 4px left, 8px top and right, and 0px bottom
- Set the tab container fill and border to `#191A1B`
- Set the tab-container border to the shared pane-border color and removed tab-panel padding
- Keep the tab-container outline synchronized with the shared pane-border color when switching themes
- Paint the shared pane-border outline on the tab-flow surface so it remains visible above the container fill
- Drew template captions directly in tab panels to prevent ghosted/overlapping labels
- Added a short fade-in/fade-out circular hover highlight to the add-text button, matching the tab close buttons
- Matched the add (+) and close (×) tab controls with consistent hover-circle size and opacity and rounded glyph strokes
- Moved the add (+) glyph and its hover circle down by 3px for consistent alignment
- Increased the tab-strip row height to fit its top margin and tabs without a vertical scrollbar
- Removed duplicate tab-layer border painting and let tab panels paint a single rounded outline
- Aligned the tab strip and tabs flush to the editor group edges
- Rounded the editor outline at the bottom corners while keeping its top edge open

### Build and packaging
- The self-contained Windows x64 executable is published to `publish/win-x64/TYPR.exe`
- Added a tag-triggered GitHub Actions release that publishes a portable Windows x64 executable and an optional Inno Setup installer
- Added SHA-256 checksums to GitHub release assets
