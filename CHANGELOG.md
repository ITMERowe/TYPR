# Changelog

All notable changes to TYPR are documented in this file.

## [Unreleased] - 2026-10-06

### Added
- VS Code-inspired dark and light interface with rounded panels, subtle borders, and compact settings controls
- Subtle windowed-mode outline matching VS Code's frame
- Updated dark chrome and settings backgrounds to `#191A1B` and editor surface to `#121314`
- Set the settings panel inset to 8px on the left, top, and bottom, and 4px on the right
- Set editor action-container margins to 4px left/top/right and 8px bottom
- Increased the editor action-container footer row from 48px to 53px
- Set editor action-container vertical padding to 4px while retaining 2px horizontal padding
- Increased the editor action-container bottom margin to 10px
- Disabled status-label auto-sizing so editor footer text can center within its full-height cell
- Configurable global start and stop shortcuts, with modifier combinations
- Theme preference that follows Windows until the user selects a theme

### Changed
- Promoted the editor, activity bar, settings sidebar, and action bar to the only TYPR interface
- Retained existing theme and shortcut preference storage across the app consolidation
- Made the root project and `publish.ps1` the single build and publish path for `TYPR.exe`
- Updated the README to describe the single interface and its settings

### Fixed
- Corrected rounded card borders and equalized editor padding
- Added a subtle outer frame around the window when it is not maximized
- Centered editor footer status, metrics, and buttons with consistent spacing
- Improved settings-field alignment, shortcut keycaps, and input validation
- Hardened shortcut registration cleanup and publish failure handling

### Build and packaging
- The self-contained Windows x64 executable is published to `publish/win-x64/TYPR.exe`
