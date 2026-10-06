# Changelog

All notable changes to TYPR are documented in this file.

## [Unreleased] - 2026-10-06

### Added
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
- Promoted the editor, activity bar, settings sidebar, and action bar to the only TYPR interface
- Made typing use only the active text tab and disabled tab actions during typing
- Matched the editor tabs to VS Code: the active tab uses a distinct outlined surface, while inactive tabs sit on the darker strip
- Retained existing theme and shortcut preference storage across the app consolidation
- Made the root project and `publish.ps1` the single build and publish path for `TYPR.exe`
- Updated the README to describe the single interface and its settings

### Fixed
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
