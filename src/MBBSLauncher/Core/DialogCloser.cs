// MBBSLauncher - Dialog Closer
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Core/DialogCloser.cs
// Version: v2.0-beta21
//
// Change History:
// 26.02.07.1 - Initial creation (disabled stub in v1.5).
// 26.06.22.1 - v2.0 - Added ClickBtrieveContinueIfPresent. After a crash, a BBS restart (whether by
//                     Worldgroup's own recovery, our monitor, or a manual relaunch) stalls on
//                     wgsappgo's "The Btrieve database engine is already running... continue loading
//                     the BBS?" prompt until someone clicks Yes. This finds that dialog (a #32770
//                     owned by wgsappgo whose body mentions Btrieve) and clicks Yes (control id 6 =
//                     IDYES), but only while wgserver is NOT running so we honor the dialog's own
//                     "no other instance" warning. The legacy CloseAllBBSDialogs /
//                     CloseDialogsForProcess stubs are unchanged.
// 26.07.15.1 - v2.0-beta20 - Added DismissWgserverCrashDialogIfPresent. When wgserver.exe faults
//                     (a memory/"Application Error" box with two hex addresses), Windows keeps the
//                     faulted process suspended-but-listed behind the modal box, so
//                     IsProcessRunning("wgserver") stays true and neither crash detector
//                     (RestartManager.CrashCondition / AppManagerForm.UpdateBBSStatus) fires until a
//                     human closes the box. This finds that box (a visible #32770 owned by wgserver
//                     with crash-indicative text — a 0x/segment:offset address or a known fault
//                     phrase — or a WerFault dialog naming wgserver), logs its text to audit.log,
//                     then force-terminates the stuck wgserver so it truly exits and normal recovery
//                     (or launcher restore) proceeds unattended. Fails safe: a wgserver dialog with
//                     no crash indicator is logged once but never acted on, and a kill only follows
//                     two consecutive confirmations of the same box.
// 26.07.29.1 - v2.0-beta21 - Added DismissWgsappgoErrorlevelDialogIfPresent. When wgserver dies hard
//                     (e.g. exit code -1073740940 / 0xC0000374 heap corruption), the wgsappgo "Go!"
//                     supervisor can raise its own modal "Unrecognized errorlevel = ... returned from
//                     WGSERVER.EXE!" box and sit alive behind it. Since the beta15 cleanup-safety rule
//                     (a crash requires wgsappgo gone too), that alive supervisor reads as "Cleanup..."
//                     and BOTH crash detectors stand down until a human clicks OK. This finds that box
//                     (a #32770 owned by a wgs* process whose text mentions an errorlevel returned
//                     from something, only while wgserver is NOT running — the box by definition
//                     postdates wgserver's exit) and clicks OK, exactly what the human does today, so
//                     wgsappgo exits and normal crash recovery / launcher restore proceeds unattended.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MBBSLauncher.Core
{
    public static class DialogCloser
    {
        #region Win32 API Imports

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassNameW(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr hDlg, int controlId);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const string DialogClassName = "#32770"; // standard Windows dialog class
        private const uint BM_CLICK = 0x00F5;
        private const uint WM_CLOSE = 0x0010;
        private const int IDOK = 1;
        private const int IDCANCEL = 2;
        private const int IDYES = 6;

        #endregion

        // --- Legacy stubs (disabled in v1.5, kept for ProcessHelper compatibility) ---

        public static int CloseAllBBSDialogs(bool aggressive = false)
        {
            // Feature disabled in v1.5
            return 0;
        }

        public static int CloseDialogsForProcess(int processId)
        {
            // Feature disabled in v1.5
            return 0;
        }

        // --- Btrieve "already running" auto-continue (v2.0) ---

        /// <summary>
        /// Looks for wgsappgo's "The Btrieve database engine is already running... continue loading
        /// the BBS?" prompt and clicks Yes so a restart can finish unattended. Only acts while
        /// wgserver is not running (honoring the dialog's own "no other instance" warning).
        /// Returns true if a Yes click was sent.
        /// </summary>
        public static bool ClickBtrieveContinueIfPresent()
        {
            // Gate: never click while a BBS server is already up — that's the unsafe case the
            // dialog warns about. This also makes the common (BBS healthy) path a cheap no-op.
            if (ProcessHelper.IsWGServerRunning())
                return false;

            IntPtr target = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (GetWindowClass(hWnd) != DialogClassName) return true;
                if (!IsOwnedByProcess(hWnd, "wgsappgo")) return true;
                if (!HasBtrieveText(hWnd)) return true;

                target = hWnd;
                return false; // found it — stop enumerating
            }, IntPtr.Zero);

            if (target == IntPtr.Zero) return false;

            IntPtr yesButton = GetDlgItem(target, IDYES);
            if (yesButton == IntPtr.Zero) return false;

            SendMessageW(yesButton, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            Program.LogInfo("BtrieveWatcher", "Clicked Yes on the wgsappgo Btrieve 'already running' prompt to continue BBS startup.");
            return true;
        }

        // --- wgserver crash-dialog auto-dismiss (v2.0-beta20) ---

        private const string WGSERVER_PROCESS = "wgserver";

        // Text that reliably marks a crash box (case-insensitive). Kept conservative: matching one of
        // these on a wgserver-owned dialog is what authorizes force-killing the process, so it must
        // never fire on a benign dialog. The standard Windows "Application Error" / memory box always
        // contains "0x" (e.g. "The instruction at 0x... referenced memory at 0x...").
        private static readonly string[] CrashPhrases =
        {
            "application error", "has stopped working", "stopped responding", "not responding",
            "general protection", "access violation", "unhandled exception", "runtime error",
            "referenced memory", "the memory could not be", "at address", "0x"
        };

        // A segment:offset address (XXXX:XXXX) or a 0x-prefixed address — the "two numbers" a
        // faulted wgserver reports. Compiled once; conservative enough not to match ordinary text.
        private static readonly Regex AddressPattern =
            new Regex(@"[0-9A-Fa-f]{4}:[0-9A-Fa-f]{4}|0x[0-9A-Fa-f]{4,}", RegexOptions.Compiled);

        private static IntPtr _lastCrashDialog = IntPtr.Zero;   // handle of the crash box we're tracking
        private static int _crashDialogStreak;                  // consecutive polls we've seen the same box
        private static IntPtr _lastUnhandledWgserverDialog = IntPtr.Zero; // for one-shot diagnostic logging

        /// <summary>
        /// Detects a wgserver crash / "Application Error" box that is keeping the faulted wgserver.exe
        /// process suspended-but-listed — so <c>IsProcessRunning("wgserver")</c> still returns true and
        /// neither crash detector fires until a human dismisses the box. Logs the box text for
        /// diagnostics and force-terminates the stuck wgserver so the process truly exits and normal
        /// crash recovery / launcher restore can proceed unattended.
        ///
        /// Safety: a wgserver-owned dialog must carry a crash indicator (an address or a known fault
        /// phrase) before it is acted on — a benign wgserver dialog is logged once and left alone — and
        /// the kill only follows two consecutive confirmations of the same box.
        /// Returns true when it force-killed a stuck wgserver this call.
        /// </summary>
        public static bool DismissWgserverCrashDialogIfPresent()
        {
            IntPtr found = IntPtr.Zero;
            string capturedText = string.Empty;
            bool ownerIsWer = false;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (GetWindowClass(hWnd) != DialogClassName) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return true;

                string owner = TryGetProcessName((int)pid);
                bool isWgserver = string.Equals(owner, WGSERVER_PROCESS, StringComparison.OrdinalIgnoreCase);
                bool isWer = owner.StartsWith("WerFault", StringComparison.OrdinalIgnoreCase);
                if (!isWgserver && !isWer) return true;

                string text = (GetWindowTextValue(hWnd) + " " + GetAllChildText(hWnd)).Trim();

                // A WerFault dialog is a crash by definition, but only if it is about wgserver.
                if (isWer && text.IndexOf(WGSERVER_PROCESS, StringComparison.OrdinalIgnoreCase) < 0)
                    return true;

                // A wgserver-owned dialog must look like a crash before we touch the process.
                if (isWgserver && !LooksLikeCrash(text))
                {
                    NoteUnhandledWgserverDialog(hWnd, text);
                    return true;
                }

                found = hWnd;
                capturedText = text;
                ownerIsWer = isWer;
                return false; // found it — stop enumerating
            }, IntPtr.Zero);

            if (found == IntPtr.Zero)
            {
                _lastCrashDialog = IntPtr.Zero;
                _crashDialogStreak = 0;
                return false;
            }

            // Debounce: confirm the same box across two consecutive polls before killing, so a one-off
            // mismatch can never take down a healthy BBS.
            if (found == _lastCrashDialog)
            {
                _crashDialogStreak++;
            }
            else
            {
                _lastCrashDialog = found;
                _crashDialogStreak = 1;
                Program.LogInfo("CrashDialog",
                    $"wgserver crash box detected ({(ownerIsWer ? "WerFault" : "wgserver")}): \"{Truncate(capturedText, 300)}\" — confirming before recovery.");
            }

            if (_crashDialogStreak < 2) return false; // wait one more poll to be certain

            int killed = ProcessHelper.ForceKillProcess(WGSERVER_PROCESS);
            Program.LogInfo("CrashDialog",
                $"Cleared the stuck wgserver crash box (processes terminated={killed}) so crash recovery can proceed.");

            _lastCrashDialog = IntPtr.Zero;
            _crashDialogStreak = 0;
            return true;
        }

        // --- wgsappgo "Unrecognized errorlevel" auto-dismiss (v2.0-beta21) ---

        private static IntPtr _lastErrorlevelDialog = IntPtr.Zero; // for one-shot logging per box

        /// <summary>
        /// Detects the wgsappgo supervisor's modal "Unrecognized errorlevel = ... returned from
        /// WGSERVER.EXE!" box, raised when wgserver dies hard (e.g. 0xC0000374 heap corruption).
        /// While that box is up wgsappgo stays alive, so the cleanup-safety rule (crash requires
        /// wgsappgo gone) reads the outage as "Cleanup..." and neither crash detector fires until a
        /// human clicks OK. This clicks OK — the same action the human takes — so wgsappgo exits and
        /// normal crash recovery / launcher restore proceeds unattended.
        ///
        /// Safety: only acts while wgserver is NOT running (the box reports an exit code, so it
        /// postdates wgserver's death by definition), and only on a dialog owned by a wgs* process
        /// whose text mentions an errorlevel being returned. Returns true when an OK/close was sent.
        /// </summary>
        public static bool DismissWgsappgoErrorlevelDialogIfPresent()
        {
            if (ProcessHelper.IsWGServerRunning())
                return false;

            IntPtr target = IntPtr.Zero;
            string ownerName = string.Empty;
            string capturedText = string.Empty;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (GetWindowClass(hWnd) != DialogClassName) return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return true;

                // Expected owner is wgsappgo; accept any wgs* sibling in case another Worldgroup
                // component raises the same box. (wgserver itself is dead — gated above.)
                string owner = TryGetProcessName((int)pid);
                if (!owner.StartsWith("wgs", StringComparison.OrdinalIgnoreCase)) return true;

                string text = (GetWindowTextValue(hWnd) + " " + GetAllChildText(hWnd)).Trim();
                if (text.IndexOf("errorlevel", StringComparison.OrdinalIgnoreCase) < 0) return true;
                if (text.IndexOf("returned from", StringComparison.OrdinalIgnoreCase) < 0) return true;

                target = hWnd;
                ownerName = owner;
                capturedText = text;
                return false; // found it — stop enumerating
            }, IntPtr.Zero);

            if (target == IntPtr.Zero)
            {
                _lastErrorlevelDialog = IntPtr.Zero;
                return false;
            }

            if (target != _lastErrorlevelDialog)
            {
                _lastErrorlevelDialog = target;
                Program.LogInfo("CrashDialog",
                    $"{ownerName} errorlevel box detected: \"{Truncate(capturedText, 300)}\" — clicking OK so crash recovery can proceed.");
            }

            // A plain MB_OK message box gives its lone OK button control id IDCANCEL (2), not IDOK,
            // so try both; a bare WM_CLOSE dismisses an OK-only box as a last resort.
            IntPtr okButton = GetDlgItem(target, IDOK);
            if (okButton == IntPtr.Zero) okButton = GetDlgItem(target, IDCANCEL);
            if (okButton != IntPtr.Zero)
                SendMessageW(okButton, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            else
                SendMessageW(target, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        private static bool LooksLikeCrash(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string lower = text.ToLowerInvariant();
            foreach (string phrase in CrashPhrases)
                if (lower.Contains(phrase)) return true;
            return AddressPattern.IsMatch(text);
        }

        // Log an unrecognized wgserver-owned dialog once (per distinct window) so a board whose fault
        // box uses different wording surfaces its exact text in audit.log — we can then widen matching
        // without ever having guessed and killed a healthy BBS.
        private static void NoteUnhandledWgserverDialog(IntPtr hWnd, string text)
        {
            if (hWnd == _lastUnhandledWgserverDialog) return;
            _lastUnhandledWgserverDialog = hWnd;
            Program.LogInfo("CrashDialog",
                $"Saw a wgserver-owned dialog with no crash indicator; taking no action. Text: \"{Truncate(text, 300)}\"");
        }

        private static string GetAllChildText(IntPtr dialog)
        {
            var sb = new StringBuilder();
            EnumChildWindows(dialog, (child, lParam) =>
            {
                string t = GetWindowTextValue(child);
                if (!string.IsNullOrWhiteSpace(t)) { sb.Append(t); sb.Append(' '); }
                return true;
            }, IntPtr.Zero);
            return sb.ToString();
        }

        private static string TryGetProcessName(int pid)
        {
            try
            {
                using Process p = Process.GetProcessById(pid);
                return p.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        private static bool IsOwnedByProcess(IntPtr hWnd, string processName)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return false;
            try
            {
                using Process p = Process.GetProcessById((int)pid);
                return string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasBtrieveText(IntPtr dialog)
        {
            bool found = false;
            EnumChildWindows(dialog, (child, lParam) =>
            {
                if (GetWindowTextValue(child).IndexOf("Btrieve", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = true;
                    return false; // stop scanning children
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static string GetWindowClass(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            GetClassNameW(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static string GetWindowTextValue(IntPtr hWnd)
        {
            var sb = new StringBuilder(2048);
            GetWindowTextW(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
