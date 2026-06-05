// MBBSLauncher - Help Form
// Created by Mark Laudenbach with Love in Iowa, USA.
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Forms/HelpForm.cs
// Version: v1.80
//
// Change History:
// 26.01.12.1 - Initial creation
// 26.01.23.1 - Updated for v1.20 - Added Ghost3 info, increased font size
// 26.02.19.1 - Dynamic version string in help content header (no more hardcoded version)

using System;
using System.Drawing;
using System.Windows.Forms;

namespace MBBSLauncher.Forms
{
    public class HelpForm : Form
    {
        private RichTextBox _helpText = null!;
        private Button _closeButton = null!;

        public HelpForm()
        {
            InitializeComponent();
            LoadHelpContent();
        }

        private void InitializeComponent()
        {
            this.Text = $"{Program.APP_NAME} {Program.APP_VERSION} - Help";
            this.Size = new Size(820, 600);
            this.MinimumSize = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(0, 0, 128); // DOS blue

            // Help text area
            _helpText = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(0, 0, 128),
                ForeColor = Color.White,
                Font = new Font("Consolas", 11f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = false
            };

            // Close button
            _closeButton = new Button
            {
                Text = "Close",
                Size = new Size(100, 30),
                Dock = DockStyle.Bottom,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 128, 128),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10f, FontStyle.Bold)
            };
            _closeButton.Click += (s, e) => this.Close();
            _closeButton.FlatAppearance.BorderColor = Color.Cyan;

            // Panel for padding
            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15)
            };
            contentPanel.Controls.Add(_helpText);

            this.Controls.Add(contentPanel);
            this.Controls.Add(_closeButton);

            // Handle Escape key to close
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.F1)
                {
                    this.Close();
                    e.Handled = true;
                }
            };
        }

        private void LoadHelpContent()
        {
            string helpContent = $@"
================================================================================
                    MBBS LAUNCHER {Program.APP_VERSION} - HELP & DOCUMENTATION
================================================================================

  Created by Mark Laudenbach with Love in Iowa, USA.
  https://github.com/SysopNetwork/MBBSLauncher

--------------------------------------------------------------------------------
                              QUICK START
--------------------------------------------------------------------------------

  MBBSLauncher provides a convenient graphical interface for managing and
  launching The Major BBS Version 10 system.

  Simply press the number key (0-9) corresponding to the option you want,
  or click on the option buttons with your mouse.

--------------------------------------------------------------------------------
                            KEYBOARD SHORTCUTS
--------------------------------------------------------------------------------

  NUMBER KEYS (0-9)     Launch the corresponding menu option
  99                    Launch CNF 99 (type 9 twice quickly)
  ENTER                 Launch the currently selected option (default: Go!)
  ESCAPE                Exit the launcher

  F1                    Open this Help dialog
  F2                    Enable/Disable Modules (WGSDMOD.exe)
  F12                   Open Configuration Editor

--------------------------------------------------------------------------------
                              MENU OPTIONS
--------------------------------------------------------------------------------

  1 - Hardware Setup        Configure hardware settings (WGSCNF -L1)
  2 - Design Menu Tree      Design and edit menu trees (wgsrunmt.exe)
  3 - Security & Accounting Security and accounting options (WGSCNF -L3)
  4 - Configuration Options General configuration (WGSCNF -L4)
  5 - Go!                   Start the BBS server (wgsappgo.exe)
  6 - Edit Text Blocks      Edit system text blocks (WGSCNF -L6)
  7 - Basic Utilities       Access basic utilities (WGSUMENU.exe)
  8 - Reports               Generate system reports (WGSRPT.exe)
  99 - CNF 99               Special configuration (WGSCNF -L99)
  0 - Exit                  Close the launcher

--------------------------------------------------------------------------------
                             MOUSE NAVIGATION
--------------------------------------------------------------------------------

  - Hover over any option to see a visual highlight
  - Click on any option number to launch it
  - Click on any option number to launch it

--------------------------------------------------------------------------------
                              SYSTEM TRAY
--------------------------------------------------------------------------------

  The launcher minimizes to the system tray when you launch a program.

  TRAY ICON FEATURES:
  - Shows ""Running: [Program Name]"" when a program is active
  - Right-click for quick access menu:
    * Show Launcher - Restore the launcher window
    * Start BBS (Go!) - Quick launch the BBS
    * Bring [Program] to Front - Focus the running program
    * Configuration (F12) - Open settings
    * Exit - Close the launcher

  TRAY BEHAVIORS:
  - Double-click the tray icon to restore the launcher
  - Balloon notification appears on first minimize

--------------------------------------------------------------------------------
                           AUTO-START BBS
--------------------------------------------------------------------------------

  The launcher can automatically start the BBS when it opens.

  To enable, press F12 and configure:
    AutoStartBBS=true
    AutoStartDelay=5      (seconds to wait, allows cancellation)
    QuietMode=false       (set true to minimize to tray after start)

  During countdown:
  - Press ANY KEY or CLICK anywhere to cancel auto-start
  - Countdown displays at bottom of screen (blue/cyan theme)

--------------------------------------------------------------------------------
                          GHOST3 AUTO-LAUNCH
--------------------------------------------------------------------------------

  Ghost3 is a program that allows The Major BBS to connect to old school
  BBS doors. The launcher can automatically start Ghost3 after the BBS
  launches.

  To enable, press F12 and configure:
    Ghost3Enabled=true
    Ghost3Path=C:\Ghost3\Ghost3.exe  (adjust to your installation)
    Ghost3Delay=60        (seconds to wait after BBS starts)

  How it works:
  - Launch the BBS (Option 5 - Go!)
  - Countdown begins after BBS starts successfully
  - Press ANY KEY or CLICK anywhere to cancel Ghost3 launch
  - Countdown displays at bottom of screen (green theme)
  - Ghost3 launches independently after delay expires

  Ghost3 runs separately from the launcher - it does not need to be
  monitored or managed. You can close the launcher while Ghost3 runs.

--------------------------------------------------------------------------------
                           CONFIGURATION
--------------------------------------------------------------------------------

  Press F12 to open the Configuration Editor, where you can:
  - Set the BBS installation path
  - Configure program paths and names
  - Enable/disable Windows startup
  - Customize all menu options

  Configuration is stored in: MBBSLauncher.ini

--------------------------------------------------------------------------------
                              RESOURCES
--------------------------------------------------------------------------------

  Sysop Network Discord:  https://discord.gg/dpzquuNVmb
  GitHub:                 https://github.com/SysopNetwork/MBBSLauncher
  Module SDK:             https://github.com/TheMajorBBS/MBBS-v10-module-SDK

--------------------------------------------------------------------------------
                           TROUBLESHOOTING
--------------------------------------------------------------------------------

  PROGRAM NOT FOUND:
  - Press F12 and verify the BBS Path is correct
  - Ensure all program paths in configuration are valid

  BBS ALREADY RUNNING:
  - The launcher detects if wgserver.exe is already running
  - Choose to bring the existing instance to the foreground

  LAUNCHER DISAPPEARED:
  - Check the system tray (near the clock)
  - Double-click the tray icon to restore

  CONFIGURATION ISSUES:
  - Delete MBBSLauncher.ini to reset to defaults
  - The launcher will recreate it on next start

--------------------------------------------------------------------------------

  Thank you for using MBBSLauncher!

  For support, questions, or feedback, join us on the Sysop Network Discord!

================================================================================
";
            _helpText.Text = helpContent.TrimStart();
            _helpText.SelectionStart = 0;
            _helpText.ScrollToCaret();
        }
    }
}
