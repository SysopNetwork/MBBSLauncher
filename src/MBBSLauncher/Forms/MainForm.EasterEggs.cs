// MBBSLauncher - Main Form Easter Eggs
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Forms/MainForm.EasterEggs.cs
// Version: v2.0-beta14
//
// Change History:
// 26.07.03.1 - v2.0-beta13 - Initial creation. Two hidden features, both off unless the sysop
//                     triggers them and both silenced when [Settings] EasterEggsEnabled is false:
//                       1. Konami code (Up Up Down Down Left Right Left Right B A) rolls a scrolling
//                          "greetz" credits screen. Any key or click closes it.
//                       2. Typing C-G-A toggles a retro CGA color-cycle + CRT scanline overlay over
//                          the launcher background. Typing it again, or pressing ESC, turns it off.
//                     Kept in a partial class so the main form file stays focused; MainForm wires
//                     five small hook points (setup, KeyDown, MouseClick, Paint, Dispose).
// 26.07.03.2 - v2.0-beta14 - Slowed the manual CGA overlay a lot (color drifts every ~2s, scanlines
//                     roll gently). Added a periodic "teaser": every 1-2 minutes, while the launcher
//                     is visible and no egg is already active, the overlay fades in and out over a
//                     fraction of a second — enough to notice, not enough to give it away. The teaser
//                     obeys the same EasterEggsEnabled toggle.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace MBBSLauncher.Forms
{
    public partial class MainForm
    {
        // ---- Konami code ---------------------------------------------------------------------
        private static readonly Keys[] KonamiSequence =
        {
            Keys.Up, Keys.Up, Keys.Down, Keys.Down,
            Keys.Left, Keys.Right, Keys.Left, Keys.Right,
            Keys.B, Keys.A
        };
        private int _konamiIndex;

        // Rolling buffer of recent letter keys, used to spot the "CGA" trigger word.
        private string _secretWord = "";

        // ---- Greetz screen -------------------------------------------------------------------
        private bool _showGreetz;
        private float _greetzScroll;
        private System.Windows.Forms.Timer? _greetzTimer;
        private Font? _greetzFont;      // monospace so the name/handle columns line up
        private Font? _greetzHintFont;

        // The credits roll. Edit here to change who is thanked — columns assume a monospace font.
        private readonly string[] _greetzLines =
        {
            "",
            "= = =   G R E E T Z   = = =",
            "",
            "MBBSLauncher",
            "",
            "Special thanks to the sysops who",
            "shaped this release:",
            "",
            "Steve Hartsock      (Baldilocks)",
            "Gregory McGill      (araceshopper)",
            "Jacque Steyn        (Ragtop)",
            "",
            "...and the rest of The Major BBS v10",
            "community that provided feedback.",
            "",
            "Thank you!  <3",
            "",
        };

        // ---- CGA / CRT overlay ---------------------------------------------------------------
        private bool _crtMode;
        private int _crtPhase;
        private System.Windows.Forms.Timer? _crtTimer;
        private Pen? _scanlinePen;

        // ---- Periodic teaser -----------------------------------------------------------------
        // Every 1-2 minutes the overlay blips on for a fraction of a second, then fades out, so a
        // sysop notices "something happened" without being handed the trick.
        private System.Windows.Forms.Timer? _teaserTimer;       // schedules the next blip
        private System.Windows.Forms.Timer? _teaserFlashTimer;  // drives the blip's fade frames
        private bool _teaserActive;
        private int _teaserFrame;
        private int _teaserColorIndex;
        private readonly Random _rng = new Random();
        private const int TeaserFrames = 9;                     // ~360ms blip at 40ms/frame

        // Classic CGA high-intensity colors, cycled to wash the background with a retro glow.
        private static readonly Color[] CgaPalette =
        {
            Color.FromArgb(85, 255, 255),  // cyan
            Color.FromArgb(255, 85, 255),  // magenta
            Color.FromArgb(255, 255, 255), // white
            Color.FromArgb(85, 255, 85),   // green
            Color.FromArgb(255, 85, 85),   // red
            Color.FromArgb(255, 255, 85),  // yellow
        };

        /// <summary>
        /// Creates the easter-egg timers, fonts, and pens. Called once from the form setup.
        /// </summary>
        private void InitializeEasterEggs()
        {
            _greetzTimer = new System.Windows.Forms.Timer { Interval = 40 }; // ~25 fps scroll
            _greetzTimer.Tick += GreetzTimer_Tick;

            _crtTimer = new System.Windows.Forms.Timer { Interval = 100 };   // color-cycle / roll
            _crtTimer.Tick += CrtTimer_Tick;

            // Periodic teaser. The blip frames run on a short timer; the schedule timer re-randomizes
            // its own interval to 1-2 minutes after each tick so the blips don't fall on a predictable
            // beat. Started here; each tick no-ops unless the feature is on and the launcher is visible.
            _teaserFlashTimer = new System.Windows.Forms.Timer { Interval = 40 };
            _teaserFlashTimer.Tick += TeaserFlashTimer_Tick;

            _teaserTimer = new System.Windows.Forms.Timer { Interval = _rng.Next(60000, 120001) };
            _teaserTimer.Tick += TeaserTimer_Tick;
            _teaserTimer.Start();

            // Fonts and pens live in the paint/animation loop, so they are cached (not per-frame).
            _greetzFont = new Font("Consolas", 13f, FontStyle.Bold);
            _greetzHintFont = new Font("Consolas", 9f, FontStyle.Regular);
            _scanlinePen = new Pen(Color.FromArgb(120, 0, 0, 0));
        }

        /// <summary>Disposes easter-egg resources. Called from MainForm.Dispose.</summary>
        private void DisposeEasterEggs()
        {
            _greetzTimer?.Dispose();
            _crtTimer?.Dispose();
            _teaserTimer?.Dispose();
            _teaserFlashTimer?.Dispose();
            _greetzFont?.Dispose();
            _greetzHintFont?.Dispose();
            _scanlinePen?.Dispose();
        }

        private bool EasterEggsEnabled => _config.GetBool("Settings", "EasterEggsEnabled", true);

        /// <summary>
        /// Feeds a key press to the easter-egg handlers. Returns true if the key was consumed
        /// (an egg was open or just triggered), so the caller should stop processing it.
        /// </summary>
        private bool HandleEasterEggKey(Keys key)
        {
            // An open greetz screen swallows the next key to close itself.
            if (_showGreetz)
            {
                HideGreetz();
                return true;
            }

            // ESC turns CRT mode off (so it doesn't also minimize the launcher).
            if (_crtMode && key == Keys.Escape)
            {
                SetCrtMode(false);
                return true;
            }

            // No new triggers while the feature is disabled.
            if (!EasterEggsEnabled)
                return false;

            bool fired = false;

            // Konami progress: advance on a match, otherwise restart (allowing this key to be the
            // start of a fresh attempt).
            if (key == KonamiSequence[_konamiIndex])
            {
                _konamiIndex++;
                if (_konamiIndex >= KonamiSequence.Length)
                {
                    _konamiIndex = 0;
                    ShowGreetz();
                    fired = true;
                }
            }
            else
            {
                _konamiIndex = (key == KonamiSequence[0]) ? 1 : 0;
            }

            // Secret word buffer for the "CGA" toggle (letters only).
            char letter = LetterForKey(key);
            if (letter != '\0')
            {
                if (_secretWord.Length >= 8)
                    _secretWord = _secretWord.Substring(_secretWord.Length - 7);
                _secretWord += letter;

                if (_secretWord.EndsWith("CGA", StringComparison.Ordinal))
                {
                    _secretWord = "";
                    SetCrtMode(!_crtMode);
                    fired = true;
                }
            }

            return fired;
        }

        /// <summary>Lets a mouse click close an open greetz screen. Returns true if consumed.</summary>
        private bool HandleEasterEggClick()
        {
            if (_showGreetz)
            {
                HideGreetz();
                return true;
            }
            return false;
        }

        private static char LetterForKey(Keys key)
        {
            return (key >= Keys.A && key <= Keys.Z) ? (char)('A' + (key - Keys.A)) : '\0';
        }

        private void ShowGreetz()
        {
            _showGreetz = true;
            _greetzScroll = 0;
            _greetzTimer?.Start();
            this.Invalidate();
        }

        private void HideGreetz()
        {
            _showGreetz = false;
            _greetzTimer?.Stop();
            this.Invalidate();
        }

        private void SetCrtMode(bool on)
        {
            _crtMode = on;
            if (on)
            {
                _crtPhase = 0;
                _crtTimer?.Start();
            }
            else
            {
                _crtTimer?.Stop();
            }
            // Diagnostic breadcrumb so the toggle can be confirmed in audit.log if the effect is
            // ever reported as "not showing".
            Program.LogInfo("EasterEgg", on ? "CGA/CRT overlay ON" : "CGA/CRT overlay OFF");
            this.Invalidate();
        }

        private void GreetzTimer_Tick(object? sender, EventArgs e)
        {
            _greetzScroll += 1.2f;
            this.Invalidate();
        }

        private void CrtTimer_Tick(object? sender, EventArgs e)
        {
            _crtPhase++;
            this.Invalidate();
        }

        private void TeaserTimer_Tick(object? sender, EventArgs e)
        {
            // Only blip when the feature is on, nothing else is already showing, and the launcher is
            // actually on screen (not hidden to tray or minimized while the BBS runs).
            bool canTease = EasterEggsEnabled
                            && !_crtMode
                            && !_showGreetz
                            && !_teaserActive
                            && this.Visible
                            && this.WindowState != FormWindowState.Minimized;

            if (canTease)
            {
                _teaserFrame = 0;
                _teaserColorIndex++;
                _teaserActive = true;
                _teaserFlashTimer?.Start();
                this.Invalidate();
            }

            // Re-randomize the next gap to 1-2 minutes so the blips aren't on a predictable beat.
            if (_teaserTimer != null)
                _teaserTimer.Interval = _rng.Next(60000, 120001);
        }

        private void TeaserFlashTimer_Tick(object? sender, EventArgs e)
        {
            _teaserFrame++;
            if (_teaserFrame >= TeaserFrames)
            {
                _teaserActive = false;
                _teaserFlashTimer?.Stop();
            }
            this.Invalidate();
        }

        /// <summary>
        /// Draws the active easter-egg overlays. Called at the end of MainForm_Paint, so it renders
        /// on top of the background and any countdown banners.
        /// </summary>
        private void DrawEasterEggs(Graphics g)
        {
            if (_crtMode)
                DrawCrtOverlay(g);
            else if (_teaserActive)
                DrawTeaser(g);

            if (_showGreetz)
                DrawGreetzScreen(g);
        }

        /// <summary>
        /// Draws the brief teaser blip: a single CGA color wash + scanlines that fade in and back out
        /// across the blip so it reads as a soft flicker rather than a hard flash.
        /// </summary>
        private void DrawTeaser(Graphics g)
        {
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            // Smooth 0 -> 1 -> 0 envelope over the blip's frames.
            double p = TeaserFrames <= 1 ? 1.0 : (double)_teaserFrame / (TeaserFrames - 1);
            double fade = Math.Sin(Math.Clamp(p, 0.0, 1.0) * Math.PI);
            if (fade <= 0.01) return;

            Color c = CgaPalette[_teaserColorIndex % CgaPalette.Length];

            int washA = (int)(70 * fade);
            if (washA > 0)
                using (var washBrush = new SolidBrush(Color.FromArgb(washA, c)))
                    g.FillRectangle(washBrush, 0, 0, w, h);

            int scanA = (int)(90 * fade);
            if (scanA > 0)
                using (var scanPen = new Pen(Color.FromArgb(scanA, 0, 0, 0)))
                    for (int y = 0; y < h; y += 2)
                        g.DrawLine(scanPen, 0, y, w, y);
        }

        private void DrawCrtOverlay(Graphics g)
        {
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            // Color-cycle wash: drift slowly, changing tint about every ~2 seconds (timer is 100ms,
            // so /20). Alpha is kept high enough to read clearly over the (colorful) background image.
            Color wash = CgaPalette[(_crtPhase / 20) % CgaPalette.Length];
            using (var washBrush = new SolidBrush(Color.FromArgb(90, wash)))
                g.FillRectangle(washBrush, 0, 0, w, h);

            // Dense scanlines with a gentle roll (shifts one row about every ~500ms) for an
            // unmistakable but calm CRT feel.
            if (_scanlinePen != null)
            {
                int offset = (_crtPhase / 5) % 2;
                for (int y = offset; y < h; y += 2)
                    g.DrawLine(_scanlinePen, 0, y, w, y);
            }
        }

        private void DrawGreetzScreen(Graphics g)
        {
            if (_greetzFont == null || _greetzHintFont == null) return;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            // Dim the whole screen behind the panel.
            using (var dim = new SolidBrush(Color.FromArgb(205, 0, 0, 0)))
                g.FillRectangle(dim, 0, 0, w, h);

            int boxW = Math.Min(600, w - 40);
            int boxH = Math.Min(400, h - 40);
            int boxX = (w - boxW) / 2;
            int boxY = (h - boxH) / 2;

            // Panel background (DOS navy) and cyan border, matching the countdown banner style.
            using (var boxBrush = new SolidBrush(Color.FromArgb(235, 0, 0, 100)))
                g.FillRectangle(boxBrush, boxX, boxY, boxW, boxH);
            using (var borderPen = new Pen(Color.FromArgb(255, 0, 255, 255), 2))
                g.DrawRectangle(borderPen, boxX, boxY, boxW, boxH);

            // Reserve a strip at the bottom for the static hint; scroll credits above it.
            int hintH = 34;
            var scrollRect = new Rectangle(boxX + 10, boxY + 10, boxW - 20, boxH - 20 - hintH);

            float lineH = g.MeasureString("X", _greetzFont).Height;
            float contentH = _greetzLines.Length * lineH;

            var savedClip = g.Clip;
            g.SetClip(scrollRect);

            using (var textBrush = new SolidBrush(Color.FromArgb(255, 170, 255, 170)))
            {
                // Credits rise from the bottom of the scroll area and loop continuously.
                float startY = scrollRect.Bottom - _greetzScroll;
                for (int i = 0; i < _greetzLines.Length; i++)
                {
                    string line = _greetzLines[i];
                    float lineY = startY + i * lineH;
                    if (lineY < scrollRect.Top - lineH || lineY > scrollRect.Bottom)
                        continue;
                    if (line.Length == 0)
                        continue;

                    SizeF size = g.MeasureString(line, _greetzFont);
                    float lineX = boxX + (boxW - size.Width) / 2f;
                    g.DrawString(line, _greetzFont, textBrush, lineX, lineY);
                }
            }

            g.Clip = savedClip;

            if (_greetzScroll > contentH + scrollRect.Height)
                _greetzScroll = 0;

            // Static hint at the bottom of the panel.
            const string hint = "Press any key or click to close";
            SizeF hintSize = g.MeasureString(hint, _greetzHintFont);
            float hintX = boxX + (boxW - hintSize.Width) / 2f;
            float hintY = boxY + boxH - hintH + (hintH - hintSize.Height) / 2f;
            using (var hintBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 120)))
                g.DrawString(hint, _greetzHintFont, hintBrush, hintX, hintY);
        }
    }
}
