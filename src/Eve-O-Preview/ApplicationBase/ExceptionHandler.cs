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
using System.Threading;
using System.Windows.Forms;
using EveOPreview.Helper;
using Serilog;

namespace EveOPreview
{
    sealed class ExceptionHandler
    {
        private const string EXCEPTION_MESSAGE = "Elite-O Preview has encountered a problem and needs to close. Additional information has been saved in the log file.";

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

                MessageBox.Show(ExceptionHandler.EXCEPTION_MESSAGE, @"Elite-O Preview", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }

            System.Environment.Exit(1);
        }
    }
}