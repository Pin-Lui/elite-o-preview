// Eve-O-Preview.Robin is a companion program / sidekick (think Batman and Robin) to provide a native runtime for communicating with the client, such as permitting AllowSetForegroundWindow and limiting DirectX frames per minute.
// Copyright (C) 2026  Aura Asuna
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.using System.Diagnostics;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static EveOPreview.Robin.DebugLogger;

namespace EveOPreview.Robin;

internal static unsafe class WinEventHook
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const int OBJID_WINDOW = 0;

    private static Action<IntPtr>? _onForegroundAction;
    private static IntPtr _lastHandleChecked = IntPtr.Zero;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != OBJID_WINDOW)
        {
            return;
        }

        if (_lastHandleChecked == hwnd)
        {
            return;
        }

        _lastHandleChecked = hwnd;

        _onForegroundAction?.Invoke(hwnd);
    }

    public static void StartListening(Action<IntPtr> callback)
    {
        try
        {
            _onForegroundAction = callback;

            Thread listenerThread = new Thread(RunHookListener)
            {
                IsBackground = true
            };
            listenerThread.Start();
        }
        catch (Exception ex)
        {
            Error(ex, $"Error in {nameof(StartListening)}");
        }
    }

    private static unsafe void RunHookListener()
    {
        const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        IntPtr hook = NativeMethods.SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            &OnForegroundChanged,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);

        if (hook == IntPtr.Zero) return;

        NativeMethods.MSG msg;
        while (NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        NativeMethods.UnhookWinEvent(hook);
    }
}