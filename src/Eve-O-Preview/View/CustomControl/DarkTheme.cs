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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EveOPreview.View.CustomControl;

public static class DarkTheme
{
    public static readonly Color EditorBackground = Color.FromArgb(0x1E, 0x1E, 0x1E);
    public static readonly Color SideBarBackground = Color.FromArgb(0x25, 0x25, 0x26);
    public static readonly Color SelectedTabBackground = Color.FromArgb(0x37, 0x37, 0x3D);
    public static readonly Color InputBackground = Color.FromArgb(0x3C, 0x3C, 0x3C);
    public static readonly Color Border = Color.FromArgb(0x45, 0x45, 0x45);
    public static readonly Color Foreground = Color.FromArgb(0xCC, 0xCC, 0xCC);
    public static readonly Color BrightForeground = Color.White;
    public static readonly Color DimForeground = Color.FromArgb(0x9D, 0x9D, 0x9D);
    public static readonly Color Accent = Color.FromArgb(0xB1, 0x80, 0xD7);
    public static readonly Color ButtonBackground = Color.FromArgb(0x68, 0x21, 0x7A);
    public static readonly Color ButtonHoverBackground = Color.FromArgb(0x7D, 0x2E, 0x90);
    public static readonly Color ButtonPressedBackground = Color.FromArgb(0x55, 0x1A, 0x64);
    public static readonly Color ActiveToggleBackground = Color.FromArgb(0x5A, 0x1D, 0x1D);
    public static readonly Color MenuSelection = Color.FromArgb(0x68, 0x21, 0x7A);
    public static readonly Color Link = Color.FromArgb(0xC5, 0x86, 0xC0);

    public static readonly Font MenuFont = new Font("Segoe UI", 9F, FontStyle.Regular);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Form form)
    {
        form.BackColor = EditorBackground;
        form.ForeColor = Foreground;
        ApplyDarkTitleBar(form);
        ApplyToChildren(form);

        if (form.ContextMenuStrip != null)
        {
            Apply(form.ContextMenuStrip);
        }
    }

    public static void Apply(ContextMenuStrip menu)
    {
        menu.Renderer = new DarkMenuRenderer();
        menu.BackColor = SideBarBackground;
        menu.ForeColor = Foreground;
        menu.Font = MenuFont;
    }

    private static void ApplyToChildren(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            ApplyToControl(control);

            if (control.ContextMenuStrip != null)
            {
                Apply(control.ContextMenuStrip);
            }

            ApplyToChildren(control);
        }
    }

    private static void ApplyToControl(Control control)
    {
        switch (control)
        {
            case Button button:
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = ButtonHoverBackground;
                button.FlatAppearance.MouseDownBackColor = ButtonPressedBackground;
                button.BackColor = ButtonBackground;
                button.ForeColor = BrightForeground;
                break;

            case ComboBox comboBox:
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = InputBackground;
                comboBox.ForeColor = Foreground;
                break;

            case TextBoxBase or ListBox or NumericUpDown:
                control.BackColor = InputBackground;
                control.ForeColor = Foreground;
                break;

            case LinkLabel link:
                link.ResetBackColor();
                link.ForeColor = Foreground;
                link.LinkColor = Link;
                link.ActiveLinkColor = Link;
                link.VisitedLinkColor = Link;
                break;

            case OutlinedLabel:
                control.ResetBackColor();
                break;

            case Label:
                control.ResetBackColor();
                control.ForeColor = Foreground;
                break;

            case TabPage page:
                page.UseVisualStyleBackColor = false;
                page.BackColor = EditorBackground;
                page.ForeColor = Foreground;
                break;

            case TrackBar:
                control.BackColor = EditorBackground;
                break;

            case Panel panel when panel.BorderStyle == BorderStyle.FixedSingle && panel.Controls.Count > 0:
                panel.BorderStyle = BorderStyle.None;
                panel.ResetBackColor();
                panel.ForeColor = Foreground;
                break;

            default:
                control.ResetBackColor();
                control.ForeColor = Foreground;
                break;
        }
    }

    private static void ApplyDarkTitleBar(Form form)
    {
        void SetDarkTitleBar()
        {
            try
            {
                int enabled = 1;
                if (DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref enabled, sizeof(int));
                }
            }
            catch (Exception)
            {
            }
        }

        if (form.IsHandleCreated)
        {
            SetDarkTitleBar();
        }
        else
        {
            form.HandleCreated += (_, _) => SetDarkTitleBar();
        }
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors())
        {
            this.RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = !e.Item.Enabled ? DimForeground : e.Item.Selected ? BrightForeground : Foreground;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Foreground;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => SideBarBackground;
        public override Color ImageMarginGradientBegin => SideBarBackground;
        public override Color ImageMarginGradientMiddle => SideBarBackground;
        public override Color ImageMarginGradientEnd => SideBarBackground;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => MenuSelection;
        public override Color MenuItemSelected => MenuSelection;
        public override Color MenuItemSelectedGradientBegin => MenuSelection;
        public override Color MenuItemSelectedGradientEnd => MenuSelection;
        public override Color MenuItemPressedGradientBegin => MenuSelection;
        public override Color MenuItemPressedGradientEnd => MenuSelection;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
        public override Color CheckBackground => MenuSelection;
        public override Color CheckSelectedBackground => MenuSelection;
        public override Color CheckPressedBackground => MenuSelection;
    }
}
