# Changelog

All notable changes to MBBSLauncher are documented here.

File-level change tracking uses the format `YY.MM.DD.X` in each source file header.

---

## [v2.0] — unreleased (beta)

### Added
- **Faster launcher restore after a BBS shutdown (beta22)** — The launcher now reappears in about 2 seconds instead of 6–8 (measured: 7.2–7.5 s before, 1.7–2.6 s after). The BBS is polled on its own 500 ms timer instead of the 2 s program-list timer, and the "both processes gone" debounce is measured in wall-clock milliseconds rather than tick counts — configurable via `[Settings] BBSDownConfirmMs` (default 1500, range 500–30000).
- **BBS-stop retract watch (beta22)** — Because the debounce is now short, a restore that turns out to be a cleanup is undone: if `wgserver` or `wgsappgo` reappears within `[Settings] BBSRetractWindowSec` (default 15, 0 disables), the launcher puts itself away again and logs the exact elapsed time under `BBS.Retract` — which is the `BBSDownConfirmMs` value needed to prevent it on that board. Cancelled by any key press or click on the launcher.
- **Audit log build tag (beta18)** — Every `audit.log` line is now prefixed with a short build tag (e.g. `[v2b18]`), and a `Startup` line at launch records the running version and the initial BBS state (monitoring on/off, `wgserver`/`wgsappgo`, TICKLER.RUN). Makes it clear which build produced any log entry.
- **Hidden features (beta13)** — Two retro easter eggs on the main launcher screen, off until triggered and governed by an **Extras** toggle on the F12 Advanced tab (`[Settings] EasterEggsEnabled`, default on): the Konami code rolls a scrolling "greetz" credits screen, and typing `C-G-A` toggles a cycling CGA color wash with CRT scanlines. Neither affects BBS operation.
  - **beta14** — Slowed the CGA overlay to a gentle drift and added a periodic split-second "teaser" flicker (every 1–2 minutes, launcher visible only). Same Extras toggle.
- **Check for Updates (beta12)** — A manual **Check for Updates** button on the F12 Advanced tab queries the GitHub Releases API and reports whether a newer version is available, with the download-page URL. On-demand only — it never contacts GitHub on its own and never downloads or installs anything.
- **App Manager popup option (beta11)** — A checkbox on the General tab controls whether the App Manager window opens automatically when the BBS starts (`[AppManager] AutoShow`, default on). Manual open via the tray menu is unaffected.
- **BBS Monitoring & Auto-Restart** — Watches The Major BBS and automatically recovers it from a crash, detected via the [TICKLER module](https://github.com/SysopNetwork/TICKLER) heartbeat file (present while running, deleted on a clean shutdown/cleanup). Two sysop-definable stages, each with a cancelable countdown:
  - **Stage 1** — restart the BBS software, 1–3 attempts, with a startup-grace window to confirm it came back up.
  - **Stage 2** — if Stage 1 is exhausted, reboot the computer once, guarded against reboot loops by a flag that clears after a healthy uptime period.
  - New **BBS Monitoring** tab in the F12 Configuration Editor with all settings and a link to the required TICKLER module. All actions are written to `audit.log`.
  - Idea suggested by **Steve Hartsock**.

### Fixed
- **Pressing Go! during a cleanup started a second BBS (beta22)** — `LaunchOption`'s "already running" check only looked at `wgserver`, which is *down* mid-cleanup while the `wgsappgo` supervisor is still alive, so the guard passed and a second BBS launched. It now refuses while the supervisor is running. The new `silent` parameter keeps that message box off unattended callers (monitor recovery, auto-start countdown), which must never block on a dialog.
- **"Unrecognized errorlevel" box blocking crash recovery (beta21)** — When `wgserver` dies hard, the `wgsappgo` supervisor can raise a modal "Unrecognized errorlevel = ... returned from WGSERVER.EXE!" box and stay alive behind it, which crash detection reads as a cleanup — nothing recovered until someone clicked OK. The crash-dialog watcher now recognizes that box (only while `wgserver` is not running) and clicks OK automatically, so the supervisor exits and crash recovery / launcher restore proceeds unattended.
- **Launcher popping up over a program opened after BBS shutdown (beta19)** — The delayed BBS-stop restore no longer brings the launcher to the front when a launcher-managed program (a CNF editor, Module Editor, Offline Utilities, etc.) is already open. It holds the launcher in the background and lets that program's own close restore it, so it never lands on top of what you're working in.
- **Cleanup mistaken for a crash, restarting the BBS (beta15)** — A crash is now only declared when the server (`wgserver`) and the "Go!" supervisor (`wgsappgo`) are both gone and the TICKLER.RUN heartbeat is present, so a nightly cleanup (server cycling, supervisor alive) no longer triggers a second BBS launch and the database reindex that followed.
- **App Manager briefly showing "Crashed" during a cleanup (beta16)** — The status detector now checks the `wgsappgo` supervisor before the TICKLER.RUN heartbeat: while the supervisor is alive it shows **"Cleanup..."**, never "Crashed."
- **Launcher restoring itself during a cleanup with monitoring off (beta17)** — The "server and supervisor both gone" decision is now confirmed over several consecutive checks (~6 s) before the launcher restores, holding at "Cleanup..." meanwhile; every restore records the signal state to `audit.log` under `AppManager.BBSDown`.

---

## [v1.90] — 2026-06-22

### Removed
- **Manual "BBS Stop Delay (Cleanup)" setting** — The Config Editor control and its `[Settings] BBSStopDelay` INI key, the `_bbsStopDelayTimer` and `StartBBSStopDelay`/`CancelBBSStopDelay` machinery in `AppManagerForm`, and the default in `ConfigManager` were all removed. The cleanup-aware BBS stop detection (below) replaces it, so a genuine shutdown now restores the launcher immediately with no delay to tune. Existing `BBSStopDelay` values left in an INI are simply ignored.

### Changed
- **"Ghost3" renamed to "3rd Party App Launcher"** — User-facing wording in the F1 Help screen, the post-BBS launch countdown banner, and the "app not found" dialog now reads "3rd Party App Launcher" instead of "Ghost3". The legacy `Ghost3Enabled` / `Ghost3Path` / `Ghost3Delay` INI keys are unchanged so existing configurations keep working.

### Fixed
- **Auto-start BBS countdown re-triggering after the BBS closed** — `CheckAutoStartBBS()` ran from the `Shown` event, which can re-fire when the window handle is recreated or the launcher is restored from the tray. After the BBS was closed the launcher would restart the auto-start countdown as if freshly loaded. The check now runs only once per session via an `_autoStartChecked` guard.
- **White box with red X where the launcher used to be** — A `WM_PAINT` dispatched during shutdown could draw `_backgroundImage` after `Dispose()` had released it, throwing inside the paint handler and triggering WinForms' red-X paint-failure rendering. `MainForm_Paint` now bails out if the form is disposing and never lets a paint exception escape; the disposed image reference is also nulled.
- **Launcher not returning to the foreground after a program closes** — Non-BBS option programs (CNF, Menu Editor, Offline Utilities, Report Viewer, WGSDMOD) previously only cleared the tray status on exit and left the launcher minimized. They now restore the launcher and bring it to the front. `RestoreFromTray()` performs a brief `TopMost` toggle so the launcher reliably becomes the foreground window. The BBS still restores through the delayed `BBSCrashed` path, so the launcher does not pop up during BBS cleanup.
- **Launcher popping up during a BBS cleanup** — The App Manager treated the BBS as stopped whenever `wgserver.exe` was not running, but during a cleanup `wgserver` exits and restarts (5 seconds on a fast/minimal box, often much longer on production systems). The `wgsappgo.exe` "Go!" supervisor, however, stays alive across the entire cleanup and only exits on a genuine shutdown. BBS stop detection is now cleanup-aware: while `wgserver` is cycling and the supervisor is alive, the BBS shows **"Cleanup..."** and the launcher stays hidden; it only restores once the supervisor is gone too. This is duration-independent, so it works regardless of how long a cleanup takes.

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
