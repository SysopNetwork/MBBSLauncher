// MBBSLauncher - BBS Monitoring Incident Window
// Created by Mark Laudenbach with Love in Iowa
// https://github.com/SysopNetwork/MBBSLauncher
//
// File: Forms/MonitorIncidentForm.cs
// Version: v2.0
//
// Change History:
// 26.06.22.1 - v2.0 - Initial creation. Always-on-top window shown during a BBS crash-recovery
//                     incident. Displays the current status / countdown and a Cancel button so the
//                     sysop can abort a restart attempt or a pending reboot from the console.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace MBBSLauncher.Forms
{
    /// <summary>
    /// Small top-most window that shows BBS Monitoring incident status and lets the sysop cancel.
    /// Driven by the RestartManager via SetStatus(); raises CancelRequested when the sysop cancels.
    /// </summary>
    public class MonitorIncidentForm : Form
    {
        private readonly Label _messageLabel;
        private readonly Button _cancelButton;

        /// <summary>Raised when the sysop clicks Cancel (or presses Esc) while cancellation is allowed.</summary>
        public event EventHandler? CancelRequested;

        public MonitorIncidentForm()
        {
            this.Text = "BBS Monitoring";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = true;
            this.TopMost = true;
            this.ClientSize = new Size(560, 150);
            this.BackColor = Color.FromArgb(64, 0, 0); // dark red — this is an alert
            this.KeyPreview = true;

            _messageLabel = new Label
            {
                AutoSize = false,
                Location = new Point(20, 20),
                Size = new Size(520, 70),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = ""
            };
            this.Controls.Add(_messageLabel);

            _cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(160, 34),
                Location = new Point((560 - 160) / 2, 100),
                BackColor = Color.Gainsboro,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                FlatStyle = FlatStyle.Standard
            };
            _cancelButton.Click += (s, e) => RaiseCancel();
            this.Controls.Add(_cancelButton);

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && _cancelButton.Enabled)
                {
                    RaiseCancel();
                    e.Handled = true;
                }
            };
        }

        private void RaiseCancel() => CancelRequested?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Updates the displayed status. <paramref name="cancelable"/> controls whether the Cancel
        /// button is enabled (e.g. it is disabled while a launch is actually in progress).
        /// </summary>
        public void SetStatus(string message, bool cancelable)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetStatus(message, cancelable)));
                return;
            }

            _messageLabel.Text = message;
            _cancelButton.Enabled = cancelable;
            _cancelButton.Visible = cancelable;

            if (!this.Visible)
            {
                this.Show();
            }
            this.BringToFront();
            this.Activate();
        }

        /// <summary>Hides the incident window (incident resolved).</summary>
        public void HideIncident()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(HideIncident));
                return;
            }
            this.Hide();
        }

        // Don't let the user X-close this destroy the singleton instance — just hide it (that acts
        // like a cancel is not implied; the launcher owns the lifecycle). The owner disposes it.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
