//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using EveOPreview.Helper;
using EveOPreview.Services.Interop;
using Serilog;

namespace EveOPreview.Services.Implementation
{
    public static class DebuggerSidecar
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct DEBUG_EVENT
        {
            [FieldOffset(0)] public uint dwDebugEventCode;
            [FieldOffset(4)] public uint dwProcessId;
            [FieldOffset(8)] public uint dwThreadId;
            // The union 'u' starts at offset 16 in 64-bit
            [FieldOffset(16)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 160)]
            public byte[] u;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OUTPUT_DEBUG_STRING_INFO
        {
            public IntPtr lpDebugStringData;
            public ushort fUnicode;
            public ushort nDebugStringLength;
        }

        private const uint DBG_CONTINUE = 0x00010002;
        private const uint DBG_EXCEPTION_NOT_HANDLED = 0x80010001;
        private const uint BreakpointHasBeenReachedErrorCode = 0x80000003;
        private const int PROCESS_VM_READ = 0x0010;

        public static void RunAsTheSideCar(string[] args)
        {
            uint targetPid = uint.Parse(args[1]);
            StartDebuggingLoop(targetPid);
            Environment.Exit(0);
        }

        public static void LaunchTheSideCar()
        {
            bool isDebuggerPresent = false;
            KernelNativeMethods.CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isDebuggerPresent);

            if (!isDebuggerPresent && !Debugger.IsAttached)
            {
                Log.Logger.WithCallerInfo().Information("No debugger present. Launching debugger sidecar.");
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                int myPid = Process.GetCurrentProcess().Id;

                if (!LaunchWithoutStartupCursor(currentExe, $"--attach-debug-sidecar {myPid}", out int win32Error))
                {
                    Log.Logger.WithCallerInfo().Warning("Launching the sidecar without startup cursor failed (Win32 error {Error}). Falling back to Process.Start.", win32Error);

                    ProcessStartInfo newProcess = new ProcessStartInfo();
                    newProcess.FileName = currentExe;
                    newProcess.Arguments = $"--attach-debug-sidecar {myPid}";
                    newProcess.CreateNoWindow = true;
                    newProcess.WindowStyle = ProcessWindowStyle.Hidden;
                    newProcess.UseShellExecute = false;

                    Process.Start(newProcess);
                }
            }
            else
            {
                Log.Logger.WithCallerInfo().Information("A debugger is already present. Will not launch debugger sidecar.");
            }
        }

        private static void StartDebuggingLoop(uint pid)
        {
            KernelNativeMethods.OutputDebugString($"[Eve-O Sidecar] Attempting attach to PID: {pid}");
            
            if (KernelNativeMethods.DebugActiveProcess(pid))
            {
                KernelNativeMethods.DebugSetProcessKillOnExit(false);

                KernelNativeMethods.OutputDebugString($"[Eve-O Sidecar] Successfully attach to PID: {pid}");

                DEBUG_EVENT dbgEvent;
                while (KernelNativeMethods.WaitForDebugEvent(out dbgEvent, uint.MaxValue))
                {
                    uint continueStatus = DBG_CONTINUE;

                    switch (dbgEvent.dwDebugEventCode)
                    {
                        case 1:
                            uint exceptionCode = BitConverter.ToUInt32(dbgEvent.u, 0);

                            try
                            {
                                IntPtr exceptionAddr = (IntPtr)BitConverter.ToInt64(dbgEvent.u, 16);
                                uint isFirstChance = BitConverter.ToUInt32(dbgEvent.u, 152); // after the x64 EXCEPTION_RECORD

                                string chance = (isFirstChance == 1) ? "First-Chance" : "Unhandled/Second-Chance";

                                KernelNativeMethods.OutputDebugString(
                                    $"[Eve-O Sidecar] EXCEPTION: 0x{exceptionCode:X8} at 0x{exceptionAddr.ToInt64():X16} ({chance})"
                                );
                            }
                            catch
                            {
                            }

                            continueStatus = (exceptionCode == BreakpointHasBeenReachedErrorCode) ? DBG_CONTINUE : DBG_EXCEPTION_NOT_HANDLED;
                            break;
                        
                        case 3:
                            CloseHandleAtOffset(dbgEvent.u, 0);
                            // DO NOT close hProcess or hThread (at offsets 8 and 16)
                            break;
                        case 6:
                            CloseHandleAtOffset(dbgEvent.u, 0);
                            break;
                        case 5:
                            KernelNativeMethods.OutputDebugString($"[Eve-O Sidecar] EXIT_PROCESS_DEBUG_EVENT received, closing debugger.");
                            KernelNativeMethods.ContinueDebugEvent(dbgEvent.dwProcessId, dbgEvent.dwThreadId, DBG_CONTINUE);
                            return;
                        case 8:
                            try
                            {
                                KernelNativeMethods.OutputDebugString("[Eve-O Sidecar] OUTPUT_DEBUG_STRING_EVENT received, forwarding through.");
                                IntPtr stringAddress = (IntPtr)BitConverter.ToInt64(dbgEvent.u, 0);
                                ushort isUnicode = BitConverter.ToUInt16(dbgEvent.u, 8);
                                ushort stringLen = BitConverter.ToUInt16(dbgEvent.u, 10);

                                if (stringLen > 0)
                                {
                                    IntPtr hProcess = KernelNativeMethods.OpenProcess(PROCESS_VM_READ, false,
                                        (int)dbgEvent.dwProcessId);

                                    if (hProcess != IntPtr.Zero)
                                    {
                                        int byteCount = isUnicode != 0 ? stringLen * 2 : stringLen;
                                        byte[] buffer = new byte[byteCount];

                                        if (KernelNativeMethods.ReadProcessMemory(hProcess, stringAddress, buffer,
                                                buffer.Length, out _))
                                        {
                                            string message = (isUnicode != 0)
                                                ? System.Text.Encoding.Unicode.GetString(buffer)
                                                : System.Text.Encoding.ASCII.GetString(buffer);

                                            KernelNativeMethods.OutputDebugString(message.TrimEnd('\0'));
                                        }

                                        KernelNativeMethods.CloseHandle(hProcess);
                                    }
                                }
                            }
                            catch
                            {
                            }

                            continueStatus = DBG_CONTINUE;
                            break;
                        case 2:
                        case 4:
                        case 7:
                        case 9:
                            break;
                    }

                    KernelNativeMethods.ContinueDebugEvent(dbgEvent.dwProcessId, dbgEvent.dwThreadId, continueStatus);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        private const int STARTF_USESHOWWINDOW = 0x00000001;
        private const int STARTF_FORCEOFFFEEDBACK = 0x00000080;
        private const short SW_HIDE = 0;
        private const uint CREATE_NO_WINDOW = 0x08000000;

        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(string lpApplicationName, IntPtr lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        private static bool LaunchWithoutStartupCursor(string exePath, string arguments, out int win32Error)
        {
            win32Error = 0;
            STARTUPINFO startupInfo = new STARTUPINFO();
            startupInfo.cb = Marshal.SizeOf<STARTUPINFO>();
            startupInfo.dwFlags = STARTF_USESHOWWINDOW | STARTF_FORCEOFFFEEDBACK;
            startupInfo.wShowWindow = SW_HIDE;

            // CreateProcessW may write to the command line buffer, so it must be writable memory
            IntPtr commandLine = Marshal.StringToHGlobalUni($"\"{exePath}\" {arguments}");
            try
            {
                if (!CreateProcess(exePath, commandLine, IntPtr.Zero, IntPtr.Zero, false, CREATE_NO_WINDOW,
                        IntPtr.Zero, null, ref startupInfo, out PROCESS_INFORMATION processInfo))
                {
                    win32Error = Marshal.GetLastWin32Error();
                    return false;
                }

                KernelNativeMethods.CloseHandle(processInfo.hThread);
                KernelNativeMethods.CloseHandle(processInfo.hProcess);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(commandLine);
            }
        }

        private static void CloseHandleAtOffset(byte[] u, int offset)
        {
            long val = BitConverter.ToInt64(u, offset);
            IntPtr h = new IntPtr(val);
            if (h != IntPtr.Zero && h != new IntPtr(-1))
            {
                KernelNativeMethods.CloseHandle(h);
            }
        }
    }
}