//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//Modified for Elite Dangerous (Elite-O-Preview), 2026.
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View.CustomControl;

public enum AlertKind
{
    Information,
    Warning,
    Error
}

public sealed class DarkAlertOverlay : UserControl
{
    private static readonly Color WarningAccent = Color.FromArgb(0xCC, 0xA7, 0x00);
    private static readonly Color ErrorAccent = Color.FromArgb(0xF1, 0x4C, 0x4C);
    private static readonly Color DangerBackground = Color.FromArgb(0xA1, 0x26, 0x0D);
    private static readonly Color DangerHoverBackground = Color.FromArgb(0xC4, 0x31, 0x13);
    private static readonly Color DangerPressedBackground = Color.FromArgb(0x85, 0x1F, 0x0A);

    private readonly Form _host;
    private readonly Control _previousFocus;
    private readonly Action _onConfirm;
    private readonly Color _accent;
    private readonly Panel _card;
    private readonly PictureBox _icon;
    private readonly Font _titleFont;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;
    private bool _closing;

    private DarkAlertOverlay(Form host, AlertKind kind, string title, string text, string confirmText, string cancelText, bool danger, Action onConfirm)
    {
        _host = host;
        _previousFocus = host.ActiveControl;
        _onConfirm = onConfirm;
        _accent = kind switch
        {
            AlertKind.Error => ErrorAccent,
            AlertKind.Warning => WarningAccent,
            _ => DarkTheme.Accent
        };

        this.SuspendLayout();
        this.Font = host.Font;
        this.Bounds = host.ClientRectangle;
        this.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        int padding = host.LogicalToDeviceUnits(18);
        Size buttonSize = host.LogicalToDeviceUnits(new Size(88, 27));

        _card = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(padding)
        };
        _card.Paint += Card_Paint;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0),
            Location = new Point(padding, padding)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        StockIconId iconId = kind switch
        {
            AlertKind.Error => StockIconId.Error,
            AlertKind.Warning => StockIconId.Warning,
            _ => StockIconId.Info
        };

        int iconSize = host.LogicalToDeviceUnits(32);
        using (Icon icon = SystemIcons.GetStockIcon(iconId, 64))
        {
            _icon = new PictureBox
            {
                Image = icon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(iconSize, iconSize),
                Margin = new Padding(0, 0, host.LogicalToDeviceUnits(14), 0)
            };
        }

        _titleFont = new Font(host.Font.FontFamily, host.Font.Size * 1.15f, FontStyle.Bold);
        var titleLabel = new Label
        {
            AutoSize = true,
            Text = title,
            Font = _titleFont,
            Margin = new Padding(0, host.LogicalToDeviceUnits(4), 0, host.LogicalToDeviceUnits(8))
        };

        var messageLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(host.LogicalToDeviceUnits(320), 0),
            Text = text,
            Margin = new Padding(0, 0, 0, host.LogicalToDeviceUnits(18))
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0)
        };

        if (cancelText != null)
        {
            _cancelButton = new Button
            {
                Text = cancelText,
                AutoSize = true,
                MinimumSize = buttonSize,
                Margin = new Padding(host.LogicalToDeviceUnits(8), 0, 0, 0)
            };
            _cancelButton.Click += (_, _) => this.Close(false);
            buttons.Controls.Add(_cancelButton);
        }

        _confirmButton = new Button
        {
            Text = confirmText,
            AutoSize = true,
            MinimumSize = buttonSize,
            Margin = new Padding(host.LogicalToDeviceUnits(8), 0, 0, 0)
        };
        _confirmButton.Click += (_, _) => this.Close(true);
        buttons.Controls.Add(_confirmButton);

        layout.Controls.Add(_icon, 0, 0);
        layout.SetRowSpan(_icon, 2);
        layout.Controls.Add(titleLabel, 1, 0);
        layout.Controls.Add(messageLabel, 1, 1);
        layout.Controls.Add(buttons, 0, 2);
        layout.SetColumnSpan(buttons, 2);

        _card.Controls.Add(layout);
        this.Controls.Add(_card);

        DarkTheme.ApplyTo(this);
        this.BackColor = DarkTheme.EditorBackground;
        _card.BackColor = DarkTheme.SideBarBackground;
        titleLabel.ForeColor = DarkTheme.BrightForeground;

        if (danger)
        {
            _confirmButton.BackColor = DangerBackground;
            _confirmButton.FlatAppearance.MouseOverBackColor = DangerHoverBackground;
            _confirmButton.FlatAppearance.MouseDownBackColor = DangerPressedBackground;
        }

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    public static void ShowConfirm(Form host, string title, string text, string confirmText, Action onConfirm)
    {
        Show(new DarkAlertOverlay(host, AlertKind.Warning, title, text, confirmText, "Cancel", true, onConfirm));
    }

    public static void ShowMessage(Form host, string title, string text, AlertKind kind = AlertKind.Warning, string buttonText = "OK", Action onClose = null)
    {
        Show(new DarkAlertOverlay(host, kind, title, text, buttonText, null, false, onClose));
    }

    public static bool IsShowing(Form host)
    {
        foreach (Control control in host.Controls)
        {
            if (control is DarkAlertOverlay)
            {
                return true;
            }
        }

        return false;
    }

    private static void Show(DarkAlertOverlay overlay)
    {
        Form host = overlay._host;
        if (IsShowing(host))
        {
            overlay.Dispose();
            return;
        }

        host.Controls.Add(overlay);
        overlay.BringToFront();
        overlay.CenterCard();
        (overlay._cancelButton ?? overlay._confirmButton).Focus();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        this.CenterCard();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            this.Close(_cancelButton == null);
            return true;
        }

        if (keyData == Keys.Enter)
        {
            this.Close(_confirmButton.Focused);
            return true;
        }

        if (keyData == Keys.Tab || keyData == (Keys.Tab | Keys.Shift) || keyData == Keys.Left || keyData == Keys.Right)
        {
            if (_cancelButton != null)
            {
                (_confirmButton.Focused ? _cancelButton : _confirmButton).Focus();
            }

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void CenterCard()
    {
        if (_card == null)
        {
            return;
        }

        _card.Location = new Point(
            Math.Max(0, (this.ClientSize.Width - _card.Width) / 2),
            Math.Max(0, (this.ClientSize.Height - _card.Height) / 2));
    }

    private void Card_Paint(object sender, PaintEventArgs e)
    {
        int stripe = Math.Max(3, _host.LogicalToDeviceUnits(4));
        using (var border = new Pen(DarkTheme.Border))
        {
            e.Graphics.DrawRectangle(border, 0, 0, _card.Width - 1, _card.Height - 1);
        }

        using (var accent = new SolidBrush(_accent))
        {
            e.Graphics.FillRectangle(accent, 0, 0, _card.Width, stripe);
        }
    }

    private void Close(bool confirmed)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _host.BeginInvoke(() =>
        {
            _host.Controls.Remove(this);

            if (_previousFocus != null && !_previousFocus.IsDisposed && _previousFocus.CanFocus)
            {
                _previousFocus.Focus();
            }

            this.Dispose();

            if (confirmed)
            {
                _onConfirm?.Invoke();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon?.Image?.Dispose();
            _titleFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}
