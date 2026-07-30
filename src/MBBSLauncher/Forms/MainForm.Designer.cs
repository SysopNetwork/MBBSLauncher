// MBBSLauncher - Main Form Designer
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/laudenbachm/MBBS-Launcher
//
// File: Forms/MainForm.Designer.cs
// Version: v1.90
//
// Change History:
// 26.01.07.1 - 06:00PM - Initial creation
// 26.01.12.1 - Added NotifyIcon cleanup in Dispose
// 26.06.22.1 - v1.90 - Null _backgroundImage after disposing so a late paint can't draw a
//                      disposed image (paired with the MainForm_Paint shutdown guard)

namespace MBBSLauncher.Forms
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
                // Clean up background image, then drop the reference so a queued WM_PAINT
                // arriving during shutdown can't try to draw a now-disposed image.
                _backgroundImage?.Dispose();
                _backgroundImage = null;

                // Clean up tray icon (important to avoid orphaned tray icons)
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }

                // Clean up tray context menu
                _trayMenu?.Dispose();

                // Clean up hidden-feature timers, fonts, and pens (see MainForm.EasterEggs.cs)
                DisposeEasterEggs();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(960, 540);
            this.Name = "MainForm";
            this.Text = "MBBSLauncher";
            this.ResumeLayout(false);
        }

        #endregion
    }
}
