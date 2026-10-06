# Changelog

All notable changes to TYPR are documented in this file.

## [Unreleased] - 2026-10-06

### Added
- VS Code-inspired dark theme with muted surfaces, subtle borders, and compact controls
- Rounded card panels with consistent spacing and layout rhythm
- Improved custom frameless window styling to remove the inactive border artifact

### Changed
- tightened card padding and button spacing for a more polished visual balance
- reduced the right-side settings card width to prevent clipping and improve alignment
- streamlined the README to emphasize that TYPR is 100% free and quick to use
- hardened the publish script so failed builds stop immediately instead of reporting false success

### Fixed
- fixed uneven gaps between cards and action buttons
- fixed internal card clipping by correcting panel sizing and spacing
- fixed restore/publish reliability by forcing NuGet to use nuget.org

### Build and packaging
- cleaned up stale publish output and kept the current release build in `publish/win-x64`
