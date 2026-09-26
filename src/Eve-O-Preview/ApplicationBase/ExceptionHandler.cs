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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using EveOPreview.Helper;
using EveOPreview.View;
using EveOPreview.View.CustomControl;
using Serilog;

namespace EveOPreview
{
    sealed class ExceptionHandler
    {
        private const string EXCEPTION_MESSAGE = "Elite-O-Preview has encountered a problem and needs to close. Additional information has been saved in the log file.";
        private const string EXCEPTION_NOTICE = "Elite-O-Preview has encountered a problem and needs to close.";
        private const string LOG_ON_NOTICE = "The details were saved in the logs folder.";
        private const string LOG_OFF_NOTICE = "To get the details next time, turn on \"Write log file\" in the General tab.";

        private int _isHandlingException;

        public void SetupExceptionHandlers()
        {
            
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                return;
            }
#endif
            
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (Object sender, ThreadExceptionEventArgs e)
            {
                this.ExceptionEventHandler(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += delegate (Object sender, UnhandledExceptionEventArgs e)
            {
                this.ExceptionEventHandler(e.ExceptionObject as Exception);
            };
        }

        private void ExceptionEventHandler(Exception exception)
        {
            try
            {
                Log.Logger.WithCallerInfo().Error(exception, EXCEPTION_MESSAGE);
            }
            catch
            {
            }

            if (Interlocked.Exchange(ref this._isHandlingException, 1) == 0 && TryShowNotice())
            {
                return;
            }

            System.Environment.Exit(1);
        }

        private static bool TryShowNotice()
        {
            try
            {
                Form host = Application.OpenForms.OfType<MainForm>()
                    .FirstOrDefault(form => !form.IsDisposed && form.Visible && form.WindowState != FormWindowState.Minimized);

                if (host == null || host.InvokeRequired)
                {
                    return false;
                }

                string notice = EXCEPTION_NOTICE + "\n\n" + (LogFileSwitch.IsEnabled ? LOG_ON_NOTICE : LOG_OFF_NOTICE);
                DarkAlertOverlay.ShowMessage(host, "Elite-O-Preview has to close", notice, AlertKind.Error, "Close",
                    () => System.Environment.Exit(1));
                return DarkAlertOverlay.IsShowing(host);
            }
            catch
            {
                return false;
            }
        }
    }
}