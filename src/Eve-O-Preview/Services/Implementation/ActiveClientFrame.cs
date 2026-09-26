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

namespace EveOPreview.Services
{
    internal sealed class ActiveClientFrame : IDisposable
    {
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const int OBJID_WINDOW = 0;
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const int SW_HIDE = 0;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private readonly Func<IntPtr, bool> _isClientWindow;
        private readonly Func<IntPtr, Color> _getFrameColor;
        private readonly Func<IntPtr, int> _getFrameThickness;
        private readonly WinEventDelegate _winEventCallback;
        private readonly EdgeWindow[] _edges = new EdgeWindow[4];
        private FrameOwnerWindow _owner;

        private IntPtr _foregroundHook;
        private IntPtr _locationHook;
        private uint _locationHookProcessId;
        private IntPtr _target;
        private bool _enabled;
        private bool _isShown;
        private Rectangle _lastBounds;
        private Color _lastColor;
        private int _lastThickness;

        public ActiveClientFrame(Func<IntPtr, bool> isClientWindow, Func<IntPtr, Color> getFrameColor, Func<IntPtr, int> getFrameThickness)
        {
            this._isClientWindow = isClientWindow;
            this._getFrameColor = getFrameColor;
            this._getFrameThickness = getFrameThickness;
            // Keep a reference: the native hook calls this delegate
            this._winEventCallback = this.OnWinEvent;
        }

        public bool Enabled
        {
            get => this._enabled;
            set
            {
                if (this._enabled == value)
                {
                    return;
                }

                this._enabled = value;
                if (value)
                {
                    this._foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
                        this._winEventCallback, 0, 0, WINEVENT_OUTOFCONTEXT);
                    this.Update(GetForegroundWindow(), forceReposition: true);
                }
                else
                {
                    Unhook(ref this._foregroundHook);
                    this.SetTarget(IntPtr.Zero);
                    this.HideEdges();
                }
            }
        }

        public void Refresh()
        {
            if (this._enabled)
            {
                this.Update(GetForegroundWindow(), forceReposition: false);
            }
        }

        public void Dispose()
        {
            this.Enabled = false;
            Unhook(ref this._locationHook);

            foreach (EdgeWindow edge in this._edges)
            {
                edge?.Dispose();
            }

            this._owner?.DestroyHandle();
            this._owner = null;
        }

        private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
        {
            if (!this._enabled)
            {
                return;
            }

            if (eventType == EVENT_SYSTEM_FOREGROUND)
            {
                this.Update(hwnd, forceReposition: true);
            }
            else if (eventType == EVENT_OBJECT_LOCATIONCHANGE && idObject == OBJID_WINDOW && hwnd == this._target)
            {
                this.Position(forceReposition: false);
            }
        }

        private void Update(IntPtr foregroundWindow, bool forceReposition)
        {
            bool isClient = foregroundWindow != IntPtr.Zero && this._isClientWindow(foregroundWindow);
            this.SetTarget(isClient ? foregroundWindow : IntPtr.Zero);
            this.Position(forceReposition);
        }

        private void SetTarget(IntPtr window)
        {
            if (window == this._target)
            {
                return;
            }

            this._target = window;

            uint processId = 0;
            if (window != IntPtr.Zero)
            {
                GetWindowThreadProcessId(window, out processId);
            }

            if (processId != this._locationHookProcessId)
            {
                Unhook(ref this._locationHook);
                this._locationHookProcessId = 0;
                if (processId != 0)
                {
                    this._locationHook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero,
                        this._winEventCallback, processId, 0, WINEVENT_OUTOFCONTEXT);
                    this._locationHookProcessId = processId;
                }
            }
        }

        private void Position(bool forceReposition)
        {
            if (this._target == IntPtr.Zero || !IsWindow(this._target) || IsIconic(this._target)
                || !TryGetVisibleBounds(this._target, out Rectangle bounds) || bounds.Width <= 0 || bounds.Height <= 0)
            {
                this.HideEdges();
                return;
            }

            uint dpi = GetDpiForWindow(this._target);
            int thickness = Math.Max(1, (int)Math.Round(this._getFrameThickness(this._target) * (dpi == 0 ? 96 : dpi) / 96.0));
            Color color = this._getFrameColor(this._target);

            if (!forceReposition && this._isShown && bounds == this._lastBounds && color == this._lastColor && thickness == this._lastThickness)
            {
                return;
            }

            this._lastBounds = bounds;
            this._lastColor = color;
            this._lastThickness = thickness;

            this.ShowEdge(0, bounds.Left, bounds.Top, bounds.Width, thickness, color);
            this.ShowEdge(1, bounds.Left, bounds.Bottom - thickness, bounds.Width, thickness, color);
            this.ShowEdge(2, bounds.Left, bounds.Top, thickness, bounds.Height, color);
            this.ShowEdge(3, bounds.Right - thickness, bounds.Top, thickness, bounds.Height, color);
            this._isShown = true;
        }

        private void ShowEdge(int index, int x, int y, int width, int height, Color color)
        {
            if (this._owner == null)
            {
                this._owner = new FrameOwnerWindow();
            }

            EdgeWindow edge = this._edges[index] ??= new EdgeWindow(this._owner.Handle);
            if (edge.BackColor != color)
            {
                edge.BackColor = color;
                edge.Invalidate();
            }

            SetWindowPos(edge.Handle, HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private void HideEdges()
        {
            if (!this._isShown)
            {
                return;
            }

            foreach (EdgeWindow edge in this._edges)
            {
                if (edge != null && edge.IsHandleCreated)
                {
                    ShowWindow(edge.Handle, SW_HIDE);
                }
            }

            this._isShown = false;
        }

        private static bool TryGetVisibleBounds(IntPtr window, out Rectangle bounds)
        {
            if (DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rect, Marshal.SizeOf<RECT>()) != 0
                && !GetWindowRect(window, out rect))
            {
                bounds = Rectangle.Empty;
                return false;
            }

            bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return true;
        }

        private static void Unhook(ref IntPtr hook)
        {
            if (hook != IntPtr.Zero)
            {
                UnhookWinEvent(hook);
                hook = IntPtr.Zero;
            }
        }

        // Hidden owner: owned windows are not treated as this program's main window
        // and are not hidden when the settings window is minimised
        private sealed class FrameOwnerWindow : NativeWindow
        {
            public FrameOwnerWindow()
            {
                this.CreateHandle(new CreateParams { Caption = "Elite-O Preview frame owner" });
            }
        }

        private sealed class EdgeWindow : Form
        {
            private const int WS_EX_TOPMOST = 0x00000008;
            private const int WS_EX_TRANSPARENT = 0x00000020;
            private const int WS_EX_TOOLWINDOW = 0x00000080;
            private const int WS_EX_LAYERED = 0x00080000;
            private const int WS_EX_NOACTIVATE = 0x08000000;
            private const int WM_NCHITTEST = 0x0084;
            private const int WM_MOUSEACTIVATE = 0x0021;
            private const int HTTRANSPARENT = -1;
            private const int MA_NOACTIVATE = 3;
            private const uint LWA_ALPHA = 0x2;

            private readonly IntPtr _ownerHandle;

            public EdgeWindow(IntPtr ownerHandle)
            {
                this._ownerHandle = ownerHandle;
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.StartPosition = FormStartPosition.Manual;
                this.ControlBox = false;
                this.MinimumSize = Size.Empty;
                this.Text = string.Empty;
            }

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams createParams = base.CreateParams;
                    createParams.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
                    createParams.Parent = this._ownerHandle;
                    return createParams;
                }
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                SetLayeredWindowAttributes(this.Handle, 0, 255, LWA_ALPHA);
            }

            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_NCHITTEST:
                        m.Result = new IntPtr(HTTRANSPARENT);
                        return;
                    case WM_MOUSEACTIVATE:
                        m.Result = new IntPtr(MA_NOACTIVATE);
                        return;
                }

                base.WndProc(ref m);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint eventThread, uint eventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate callback, uint idProcess, uint idThread, uint flags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr window, out RECT rect);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out RECT value, int size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);
    }
}
