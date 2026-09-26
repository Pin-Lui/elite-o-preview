//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//Modified for Elite Dangerous (Elite-O Preview), 2026.
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

public class DarkTabControl : TabControl
{
    private const int DesignTabWidth = 120;
    private const float TabFontPixelSize = 13F;
    private const int TextPaddingLeft = 12;
    private const int TextPaddingRight = 10;
    private const int AccentWidth = 3;
    private const TextFormatFlags TextFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter
        | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

    private Font _tabFont;
    private bool _formWidened;

    public DarkTabControl()
    {
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    private float DpiScale => this.DeviceDpi / 96F;

    private Font TabFont => this._tabFont ??= new Font("Segoe UI", TabFontPixelSize * this.DpiScale, FontStyle.Regular, GraphicsUnit.Pixel);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        this.UpdateTabWidth(widenForm: true);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        this._tabFont?.Dispose();
        this._tabFont = null;
        this.UpdateTabWidth(widenForm: false);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        this.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this._tabFont?.Dispose();
            this._tabFont = null;
        }

        base.Dispose(disposing);
    }

    private void UpdateTabWidth(bool widenForm)
    {
        if (this.Alignment != TabAlignment.Left && this.Alignment != TabAlignment.Right)
        {
            return;
        }

        int widestText = 0;
        foreach (TabPage page in this.TabPages)
        {
            widestText = Math.Max(widestText, TextRenderer.MeasureText(page.Text, this.TabFont, Size.Empty, TextFlags).Width);
        }

        int designWidth = (int)Math.Round(DesignTabWidth * this.DpiScale);
        int requiredWidth = widestText + (int)Math.Ceiling((TextPaddingLeft + TextPaddingRight) * this.DpiScale);
        int tabWidth = Math.Max(designWidth, requiredWidth);

        if (this.ItemSize.Height != tabWidth)
        {
            this.ItemSize = new Size(this.ItemSize.Width, tabWidth);
        }

        Form form = this.FindForm();
        if (widenForm && !this._formWidened && form != null && tabWidth > designWidth)
        {
            this._formWidened = true;
            int extraWidth = tabWidth - designWidth;
            form.MinimumSize = new Size(form.MinimumSize.Width + extraWidth, form.MinimumSize.Height);
            form.Width += extraWidth;
        }

        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;

        using (var editorBrush = new SolidBrush(DarkTheme.EditorBackground))
        {
            graphics.FillRectangle(editorBrush, this.ClientRectangle);
        }

        if (this.TabCount == 0)
        {
            return;
        }

        using (var sideBarBrush = new SolidBrush(DarkTheme.SideBarBackground))
        {
            graphics.FillRectangle(sideBarBrush, this.GetTabStripBounds());
        }

        for (int index = 0; index < this.TabCount; index++)
        {
            this.DrawTab(graphics, index);
        }
    }

    private Rectangle GetTabStripBounds()
    {
        Rectangle first = this.GetTabRect(0);
        switch (this.Alignment)
        {
            case TabAlignment.Left:
                return new Rectangle(0, 0, first.Right + 2, this.ClientSize.Height);
            case TabAlignment.Right:
                return new Rectangle(first.Left - 2, 0, this.ClientSize.Width - first.Left + 2, this.ClientSize.Height);
            case TabAlignment.Bottom:
                return new Rectangle(0, first.Top - 2, this.ClientSize.Width, this.ClientSize.Height - first.Top + 2);
            default:
                return new Rectangle(0, 0, this.ClientSize.Width, first.Bottom + 2);
        }
    }

    private void DrawTab(Graphics graphics, int index)
    {
        Rectangle bounds = this.GetTabRect(index);
        bool isSelected = index == this.SelectedIndex;

        using (var backgroundBrush = new SolidBrush(isSelected ? DarkTheme.SelectedTabBackground : DarkTheme.SideBarBackground))
        {
            graphics.FillRectangle(backgroundBrush, bounds);
        }

        if (isSelected)
        {
            using var accentBrush = new SolidBrush(DarkTheme.Accent);
            graphics.FillRectangle(accentBrush, bounds.Left, bounds.Top, (int)Math.Ceiling(AccentWidth * this.DpiScale), bounds.Height);
        }

        int paddingLeft = (int)Math.Round(TextPaddingLeft * this.DpiScale);
        int paddingRight = (int)Math.Round(TextPaddingRight * this.DpiScale);
        Rectangle textBounds = new Rectangle(bounds.Left + paddingLeft, bounds.Top,
            Math.Max(0, bounds.Width - paddingLeft - paddingRight), bounds.Height);

        TextRenderer.DrawText(graphics, this.TabPages[index].Text, this.TabFont, textBounds,
            isSelected ? DarkTheme.BrightForeground : DarkTheme.Foreground, TextFlags);
    }
}
