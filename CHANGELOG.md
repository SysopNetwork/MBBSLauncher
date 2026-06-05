# Changelog

All notable changes to MBBSLauncher are documented here.

File-level change tracking uses the format `YY.MM.DD.X` in each source file header.

---

## [v1.85] — 2026-06-05

### Fixed
- **Single-instance restore with tray icon** — `FindExistingWindow` previously used hardcoded v1.5/v1.20 title strings that no longer matched. Replaced with `EnumWindows` prefix scan, which is version-agnostic and finds windows even when hidden to the system tray.
- **App Manager close not cancelling BBS stop delay** — Clicking X on App Manager hid the form but left the `_bbsStopDelayTimer` running. When the timer fired it would raise `BBSCrashed` and restore the launcher window even though the user had deliberately closed App Manager. `CancelBBSStopDelay()` is now called before the form hides.
- **Ghost3 and Auto-Start countdown banners overlapping** — Both banners received the same `bottomOffset` value when active simultaneously, causing them to render at the same Y coordinate. `DrawGhost3Countdown` now returns its consumed pixel height so `DrawAutoStartCountdown` stacks correctly above it.
- **Launcher not restoring when App Manager is hidden** — The v1.80 double-restore fix removed `RestoreFromTray()` from the `Task.Run` watcher and relied entirely on App Manager's `BBSCrashed` event. But `_updateTimer` was paused whenever App Manager was hidden, so a stopped BBS would never be detected with App Manager closed. The timer now runs continuously regardless of form visibility.
- **Module Editor background task crashing on form close** — `LaunchModulesEditor` spun up a `Task.Run` with no cancellation token and no `IsDisposed` guard. Closing the main form while the editor was running would cause an `ObjectDisposedException` on `this.Invoke`. The task now uses `_launchMonitorCts` and checks `IsDisposed` before invoking.
- **Cancel button GDI font leak in App Manager** — The v1.80 font-leak fix moved name/status label fonts to class-level fields but missed the cancel button, which was still creating `new Font(...)` on every `UpdateCancelButton` call. Added `_cancelButtonFont` as a class-level field.
- **Section divider lines invisible in F12 Config Editor** — `CreateSectionLabel` created a separator `Label` as a local variable that was immediately dropped without being added to the parent tab. The separator is now correctly added alongside the heading label.
- **Removed dead code** — `LaunchURL()` in `MainForm` had no callers after the URL click zones were removed in v1.80.

---

## [v1.80] — 2026-06-04

### Added
- **BBS Stop Delay** — New `[Settings] BBSStopDelay` INI option (seconds). When set, the launcher waits this many seconds after the BBS stops before restoring its window. Useful for sysops who run cleanup or restart scripts after shutdown. Set to `0` for the original immediate-restore behavior. Configurable via F12 → Auto-Start tab.
- **Sysop Network Discord link** — Added to the Support section in the Config Editor (F12 → Advanced) and the F1 Help screen.

### Changed
- **Updated background image** — Removed outdated Galacticomm/website text from the main launcher screen.
- **"Iowa, USA."** — Added to the window title, About screen, and Help screen.
- **Removed external links** — Removed links to themajorbbs.com, bbs.themajorbbs.com, and the old Discord. GitHub URL updated to SysopNetwork/MBBSLauncher.

### Fixed
- **Double-restore on BBS stop** — Both `AppManager.BBSCrashed` and the `Task.Run` watcher were calling `RestoreFromTray()`, causing the launcher to flash or appear twice. `Task.Run` no longer calls restore; `BBSCrashed` is the sole restore path.
- **Ghost3/auto-launch countdown overlap** — Ghost3 and auto-launch countdowns painted at the same Y position. Countdowns now stack from the bottom up using a shared offset mechanism.
- **GDI font leak in App Manager** — `UpdateDisplay` was creating `new Font(...)` per label on every tick. Moved to class-level `_boldFont` / `_regularFont` fields.
- **Dead field removed** — `_bbsWasRunning` was written every tick but never read.

---

## [v1.70] — 2026-02-19

### Added
- **App Manager opacity slider** — TrackBar control (20–100%) with INI persistence, defaulting to 60%.
- **Resizable App Manager** — Bottom-edge drag resizes the form; height persists in `[AppManager] LastHeight`.

### Fixed
- Paint crash on close when App Manager was open (`ArgumentException` from disposed stream).
- BBS stop always reported as "Crashed" instead of "Stopped".
- Countdown label truncated at 125%+ DPI (`"Launch 0:30"` clipped to `"Launch"`).
- `SaveSettings` writing `Opacity=0` during form initialization.

---

## [v1.60] — 2026-02-19

### Changed
- Neutral BBS stop messaging ("BBS has stopped" instead of "BBS Crashed") throughout tray notifications and App Manager.
- Auto-Launch tab column widths proportional — no horizontal scroll at standard size.
- Auto-Start info label no longer clips at 125% DPI.
- Improved defaults for new installs: auto-launch at startup enabled by default.

### Fixed
- Duplicate GitHub URL removed from Advanced tab About section.

---

## [v1.55] — 2026-02-18

### Added
- Auto-Launch now checks if a process is already running before launching. Prevents duplicate instances of Ghost3, Telnet servers, etc. on BBS restart.

---

## [v1.6] — 2026-02-11

### Added
- **Administrator privileges required** — `app.manifest` with `requireAdministrator`. UAC elevation prompt on every launch.

---

## [v1.5] — 2026-02-07

### Added
- Self-contained deployment — .NET 8.0 runtime bundled in the exe. No installation required.
- Single instance enforcement with named mutex + window restore.
- 5-tab configuration editor.
- Multiple auto-launch programs (up to 20) with independent delay timers.
- Launch minimized option per auto-launch program.
- Automatic migration from v1.20 INI format.
- Audit log with 500 KB rotation.

---

## [v1.20] — 2026-01-23

### Added
- Ghost3 auto-launch support with configurable delay (0–300 seconds) and countdown UI.
- New background image.
- Resizable, maximizable configuration editor.

---

## [v1.10] — 2026-01-13

### Added
- System tray integration — minimize to tray, double-click to restore, context menu.
- Auto-start with Windows option.

### Fixed
- File version properties showing v1.0.0.0 instead of actual version.

---

## [v1.00] — 2026-01-07

- Initial release. Classic retro DOS-style interface for The Major BBS v10 sysops.
