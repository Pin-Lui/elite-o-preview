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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EveOPreview.Helper;
using Serilog;

namespace EveOPreview.Services.Implementation
{
    sealed class ProcessMonitor : IProcessMonitor, IDisposable
    {
        #region Private constants
        // Elite Dangerous (Horizons/Odyssey) game client, e.g. started by min-ed-launcher
        private const string DEFAULT_PROCESS_NAME = "EliteDangerous64";
        private const string CURRENT_PROCESS_NAME = "Elite-O Preview";
        #endregion

        #region Private fields
        private readonly object _lockObj = new object();
        public IDictionary<IntPtr, IProcessInfo> ProcessCache { get; }
        private IProcessInfo _currentProcessInfo;
        private readonly ILogger _logger;
        private readonly Func<Process[]> _enumerateProcesses;
        // All Elite windows share one caption, so the client title comes from the commander in the journal.
        private readonly Func<Process, string> _titleOf;
        private readonly EliteCommanderResolver _commanderResolver;
        #endregion

        public ProcessMonitor(ILogger logger) : this(logger, () => Process.GetProcessesByName(DEFAULT_PROCESS_NAME), new EliteCommanderResolver()) { }

        internal ProcessMonitor(ILogger logger, Func<Process[]> enumerateProcesses) : this(logger, enumerateProcesses, null) { }

        private ProcessMonitor(ILogger logger, Func<Process[]> enumerateProcesses, EliteCommanderResolver commanderResolver)
        {
            _logger = logger;
            _enumerateProcesses = enumerateProcesses;
            _commanderResolver = commanderResolver;
            _titleOf = commanderResolver != null ? new Func<Process, string>(commanderResolver.GetClientTitle) : p => p.MainWindowTitle;
            this.ProcessCache = new Dictionary<IntPtr, IProcessInfo>(512);
            
            // This field cannot be initialized properly in constructor
            // At the moment this code is executed the main application window is not yet initialized
            this._currentProcessInfo = new ProcessInfo(IntPtr.Zero, IntPtr.Zero, 0, "");
            
            _logger.Verbose("ProcessMonitor initialized");
        }

        private bool IsMonitoredProcess(string processName)
        {
            // This is a possible extension point
            return String.Equals(processName, ProcessMonitor.DEFAULT_PROCESS_NAME, StringComparison.OrdinalIgnoreCase);
        }

        private IProcessInfo GetCurrentProcessInfo()
        {
            using var currentProcess = Process.GetCurrentProcess();
            // The host's HWND is only used for visibility checks, not affinity.
            return new ProcessInfo(currentProcess.MainWindowHandle, IntPtr.Zero, currentProcess.Id, currentProcess.MainWindowTitle);
        }

        public IProcessInfo GetMainProcess()
        {
            if (this._currentProcessInfo.MainWindowHandle == IntPtr.Zero)
            {
                var processInfo = this.GetCurrentProcessInfo();

                // Are we initialized yet?
                if (processInfo.Title != "")
                {
                    _logger.Verbose("Main application window initialized: {Title} (Handle: 0x{Handle:X})", processInfo.Title, processInfo.MainWindowHandle);
                    this._currentProcessInfo = processInfo;
                }
            }

            return this._currentProcessInfo;
        }

        public ICollection<IProcessInfo> GetAllProcesses()
        {
            lock (_lockObj)
            {
                return this.ProcessCache.Values.ToList();
            }
            
            //ICollection<IProcessInfo> result = new List<IProcessInfo>(this._processCache.Count);
            //
            //// TODO Lock list here just in case
            //foreach (KeyValuePair<IntPtr, IProcessInfo> entry in this._processCache)
            //{
            //    result.Add(new ProcessInfo(entry.Key, entry.Value.ProcessId, entry.Value.Title));
            //}

            //return result;
        }

        public void GetUpdatedProcesses(out ICollection<IProcessInfo> addedProcesses, out ICollection<IProcessInfo> updatedProcesses, out ICollection<IProcessInfo> removedProcesses)
        {
            addedProcesses = new List<IProcessInfo>(16);
            updatedProcesses = new List<IProcessInfo>(16);
            removedProcesses = new List<IProcessInfo>(16);

            lock (_lockObj)
            {
            var knownProcesses = new HashSet<IntPtr>(ProcessCache.Keys);
            var usedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var runningPids = new HashSet<int>();
            // Stable order so duplicate titles always get the same suffix.
            foreach (Process process in _enumerateProcesses().OrderBy(p => p.Id))
            using (process)
            {
                try
                {
                IntPtr mainWindowHandle = process.MainWindowHandle;
                if (mainWindowHandle == IntPtr.Zero) continue;
                runningPids.Add(process.Id);
                string title = _titleOf(process);
                // Two clients must never share a title (e.g. two games of one Windows user before login).
                if (!usedTitles.Add(title))
                {
                    title = $"{title} [{process.Id}]";
                    usedTitles.Add(title);
                }
                ProcessCache.TryGetValue(mainWindowHandle, out IProcessInfo cachedProcess);
                knownProcesses.Remove(mainWindowHandle);

                if (cachedProcess != null && cachedProcess.ProcessId != process.Id)
                {
                    removedProcesses.Add(cachedProcess);
                    cachedProcess.CloseKernelHandle();
                    ProcessCache.Remove(mainWindowHandle);
                    cachedProcess = null;
                }
                if (cachedProcess == null)
                {
                    var processInfo = new ProcessInfo(mainWindowHandle, process.OpenKernelHandle(), process.Id, title);
                    ProcessCache.Add(mainWindowHandle, processInfo);
                    addedProcesses.Add(processInfo);
                }
                else if (cachedProcess.Title != title)
                {
                    // A character/login rename still owns the same process handle.
                    var renamed = ((ProcessInfo)cachedProcess).WithTitle(title);
                    ProcessCache[mainWindowHandle] = renamed;
                    updatedProcesses.Add(renamed);
                }
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
                {
                    _logger.Debug(ex, "Client exited or became inaccessible during discovery");
                }
            }

            _commanderResolver?.Retain(runningPids);

            foreach (IntPtr index in knownProcesses)
            {
                var cachedProcess = this.ProcessCache[index];
                removedProcesses.Add(cachedProcess);
                this.ProcessCache.Remove(index);
                _logger.Verbose("Elite client process removed: {Title} (Handle: 0x{Handle:X}, PID: {ProcessId})", 
                    cachedProcess.Title, index, cachedProcess.ProcessId);
            }

            foreach (var removedProcess in removedProcesses)
            {
                removedProcess.CloseKernelHandle();
            }

            if (_logger.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            {
                _logger.Verbose("Process update cycle complete: Added={AddedCount}, Updated={UpdatedCount}, Removed={RemovedCount}, Total={TotalCount}",
                    addedProcesses.Count, updatedProcesses.Count, removedProcesses.Count, this.ProcessCache.Count);
            }
            }
        }

        public IProcessInfo LookupCachedProcessByWindowHandle(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                _logger.Verbose("Lookup called with null window handle");
                return null;
            }

            lock (_lockObj)
            {
            ProcessCache.TryGetValue(windowHandle, out var procInfo);

            if (procInfo == null)
            {
                _logger.Verbose("Window handle 0x{Handle:X} not found in process cache", windowHandle);
            }

            return procInfo;
            }
        }

        public void Dispose()
        {
            lock (_lockObj)
            {
                foreach (var process in ProcessCache.Values) process.CloseKernelHandle();
                ProcessCache.Clear();
            }
        }
    }
}
