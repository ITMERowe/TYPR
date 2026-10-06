# Changelog

All notable changes to TYPR are documented in this file.

## [Unreleased] - 2026-10-06

### Added
- VS Code-inspired dark and light interface with rounded panels, subtle borders, and compact settings controls
- Configurable global start and stop shortcuts, with modifier combinations
- Theme preference that follows Windows until the user selects a theme

### Changed
- Promoted the editor, activity bar, settings sidebar, and action bar to the only TYPR interface
- Retained existing theme and shortcut preference storage across the app consolidation
- Made the root project and `publish.ps1` the single build and publish path for `TYPR.exe`
- Updated the README to describe the single interface and its settings

### Fixed
- Corrected rounded card borders and equalized editor padding
- Centered editor footer status, metrics, and buttons with consistent spacing
- Improved settings-field alignment, shortcut keycaps, and input validation
- Hardened shortcut registration cleanup and publish failure handling

### Build and packaging
- The self-contained Windows x64 executable is published to `publish/win-x64/TYPR.exe`
