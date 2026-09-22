# Changelog

All notable changes to MBBSLauncher are documented here.

File-level change tracking uses the format `YY.MM.DD.X` in each source file header.

---

## [v2.0.1] — 2026-09-22

### Fixed

- **Minimize to tray did not minimize to tray** — `MinimizeToTray()` only set `WindowState=Minimized`, leaving a taskbar icon behind instead of hiding to the system tray. It now also hides the window and clears `ShowInTaskbar`, matching what `RestoreFromTray()` already undoes.

---

## [v2.0] — 2026-07-30

### Added

- **BBS Monitoring & Auto-Restart** — Watches The Major BBS and automatically recovers it from a crash. A crash is detected via the [TICKLER module](https://github.com/SysopNetwork/TICKLER) heartbeat file (`TICKLER.RUN`, present while the BBS runs and deleted on a clean shutdown or cleanup), so a genuine crash is reliably told apart from a normal shutdown. Recovery runs in two sysop-definable stages, each with a cancelable countdown:
  - **Stage 1** — restart the BBS software, 1–3 attempts, with a startup-grace window to confirm it came back up.
  - **Stage 2** — reboot the computer once, guarded against reboot loops by a flag that clears after a healthy uptime period.
  - A **BBS Monitoring** tab in the F12 Configuration Editor holds all settings. Every action is written to `audit.log`.
  - Idea suggested by **Steve Hartsock**.
- **Automatic crash-dialog clearing** — Two error boxes used to block crash recovery until someone clicked OK. Both are now recognized and cleared automatically: the Windows memory-error box that holds a faulted `wgserver.exe` open, and the `wgsappgo` supervisor's "Unrecognized errorlevel … returned from WGSERVER.EXE!" box (only cleared while `wgserver` is not running). Controlled by `[Settings] AutoDismissCrashDialog`.
- **Check for Updates** — A manual **Check for Updates** button on the F12 Advanced tab queries the GitHub Releases API and reports whether a newer version is available, with the download-page URL. On-demand only — it never contacts GitHub on its own and never downloads or installs anything.
- **App Manager popup option** — A checkbox on the General tab controls whether the App Manager window opens automatically when the BBS starts (`[AppManager] AutoShow`, default on). Manual open via the tray menu is unaffected.
- **Audit log build tag** — Every `audit.log` line is prefixed with a short build tag (e.g. `[v2]`), and a `Startup` line at launch records the running version and the initial BBS state (monitoring on/off, `wgserver`/`wgsappgo`, `TICKLER.RUN`). Any log entry now identifies the build that produced it.
- **Tunable BBS-down confirmation** — `[Settings] BBSDownConfirmMs` (default 1500, range 500–30000) sets how long `wgserver` and `wgsappgo` must both be gone before the launcher treats it as a shutdown, so the safety margin can be matched to a board's real cleanup without a rebuild.
- **BBS-stop retract watch** — If `wgserver` or `wgsappgo` reappears within `[Settings] BBSRetractWindowSec` (default 15, 0 disables), the restore is undone: the launcher puts itself away again and logs the exact elapsed time under `BBS.Retract` — which is the `BBSDownConfirmMs` value needed to prevent it on that board. Cancelled by any key press or click on the launcher.
- **Hidden features** — Two retro easter eggs on the main launcher screen, off until triggered and governed by an **Extras** toggle on the F12 Advanced tab (`[Settings] EasterEggsEnabled`, default on). Neither affects BBS operation.

### Changed

- **Faster launcher restore after a BBS shutdown** — The launcher reappears in about 2 seconds instead of 6–8. The BBS is polled on its own 500 ms timer rather than the 2 s program-list timer, and the "both processes gone" debounce is measured in wall-clock milliseconds instead of tick counts.

### Fixed

- **Cleanup mistaken for a crash, restarting the BBS** — A crash is now only declared when the server (`wgserver`) and the "Go!" supervisor (`wgsappgo`) are both gone and the `TICKLER.RUN` heartbeat is present, so a nightly cleanup (server cycling, supervisor alive) no longer triggers a second BBS launch and the database reindex that followed.
- **App Manager briefly showing "Crashed" during a cleanup** — The status detector checks the `wgsappgo` supervisor before the `TICKLER.RUN` heartbeat: while the supervisor is alive it shows **"Cleanup…"**, never "Crashed."
- **Launcher restoring itself during a cleanup with monitoring off** — The "server and supervisor both gone" decision is confirmed over a configurable wall-clock window before the launcher restores, holding at "Cleanup…" meanwhile; every restore records the signal state to `audit.log` under `AppManager.BBSDown`.
- **Launcher popping up over a program opened after BBS shutdown** — The delayed BBS-stop restore no longer brings the launcher to the front when a launcher-managed program (a CNF editor, Module Editor, Offline Utilities, etc.) is already open. It holds the launcher in the background and lets that program's own close restore it.
- **Pressing Go! during a cleanup started a second BBS** — The "already running" check only looked at `wgserver`, which is down mid-cleanup while the `wgsappgo` supervisor is still alive, so the guard passed and a second BBS launched. It now refuses while the supervisor is running, and that message box is suppressed for unattended callers (monitor recovery, auto-start countdown) which must never block on a dialog.

---

Release history for v1.x is archived with the [v1.85 release](https://github.com/SysopNetwork/MBBSLauncher/releases/tag/v1.85).
