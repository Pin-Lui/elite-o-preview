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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace EveOPreview.Services.Implementation
{
    /// <summary>
    /// Works out which Elite Dangerous commander is playing in a game process, so every game
    /// window gets its own stable client title ("Elite - CMDR NAME") even though all Elite
    /// windows share the same caption.
    ///
    /// Chain: game process -> Windows user that owns it -> that user's Saved Games journal folder
    /// -> newest journal written since the process started -> last Commander / LoadGame event.
    /// When several games write into the same journal folder (same Windows user), the Windows
    /// Restart Manager is asked which journal file each process has open.
    ///
    /// Reading other users' processes and folders needs the app to run as administrator.
    /// All members are called from ProcessMonitor under its lock, so no extra locking is needed.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class EliteCommanderResolver
    {
        public const string TitlePrefix = "Elite - ";
        public const string NotLoggedInSuffix = " (not logged in)";

        private static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan StartTimeSlack = TimeSpan.FromMinutes(1);
        private const int MaxReadBytes = 32 * 1024 * 1024;

        private sealed class Entry
        {
            public int Pid;
            public DateTime StartUtc;
            public string UserName;
            public string JournalFolder;
            public string JournalPath;
            public long Offset;
            public string Commander;
            public DateTime NextScanUtc;
        }

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Func<int, (string Sid, string UserName)> _ownerLookup;
        private readonly Func<string, string> _journalFolderLookup;
        private readonly Func<string, int[]> _filePidsLookup;
        private readonly Func<DateTime> _utcNow;

        public EliteCommanderResolver()
            : this(GetProcessOwner, GetJournalFolder, RestartManager.GetProcessIdsUsingFile, () => DateTime.UtcNow)
        {
        }

        /// <summary>Test seam: replaces the Windows lookups.</summary>
        public EliteCommanderResolver(
            Func<int, (string Sid, string UserName)> ownerLookup,
            Func<string, string> journalFolderLookup,
            Func<string, int[]> filePidsLookup,
            Func<DateTime> utcNow)
        {
            _ownerLookup = ownerLookup;
            _journalFolderLookup = journalFolderLookup;
            _filePidsLookup = filePidsLookup;
            _utcNow = utcNow;
        }

        /// <summary>Client title for a running Elite process. Never throws.</summary>
        public string GetClientTitle(Process process)
        {
            try
            {
                return GetClientTitle(process.Id, SafeStartTimeUtc(process));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                return TitlePrefix + "Unknown" + NotLoggedInSuffix;
            }
        }

        public string GetClientTitle(int pid, DateTime startUtc)
        {
            if (!_entries.TryGetValue(pid, out var entry) || entry.StartUtc != startUtc)
            {
                // New process (or a recycled PID): look up owner and journal folder once.
                entry = new Entry { Pid = pid, StartUtc = startUtc };
                var owner = SafeCall(() => _ownerLookup(pid), default((string Sid, string UserName)));
                entry.UserName = owner.UserName;
                entry.JournalFolder = owner.Sid == null ? null : SafeCall(() => _journalFolderLookup(owner.Sid), (string)null);
                _entries[pid] = entry;
            }

            var now = _utcNow();
            if (now >= entry.NextScanUtc)
            {
                entry.NextScanUtc = now + RescanInterval;
                SafeCall(() => { Scan(entry); return true; }, false);
            }

            return BuildTitle(entry);
        }

        /// <summary>Forget processes that are no longer running.</summary>
        public void Retain(ICollection<int> runningPids)
        {
            foreach (var pid in _entries.Keys.Where(p => !runningPids.Contains(p)).ToList())
            {
                _entries.Remove(pid);
            }
        }

        private static string BuildTitle(Entry entry)
        {
            if (!string.IsNullOrEmpty(entry.Commander))
            {
                return TitlePrefix + entry.Commander;
            }

            return TitlePrefix + (string.IsNullOrEmpty(entry.UserName) ? "Unknown" : entry.UserName) + NotLoggedInSuffix;
        }

        private void Scan(Entry entry)
        {
            if (entry.JournalFolder == null || !Directory.Exists(entry.JournalFolder)) return;

            string journal = PickJournal(entry);
            if (journal == null) return;

            if (!string.Equals(journal, entry.JournalPath, StringComparison.OrdinalIgnoreCase))
            {
                // A continuation part of the same session (Journal.<stamp>.02.log) does not repeat the
                // Commander event, so keep the name; any other file starts from scratch.
                if (!IsSameSession(entry.JournalPath, journal)) entry.Commander = null;
                entry.JournalPath = journal;
                entry.Offset = 0;
            }

            ReadNewLines(entry);
        }

        /// <summary>True when both files are parts of one journal session: Journal.2026-09-25T202149.01.log / .02.log.</summary>
        public static bool IsSameSession(string previousPath, string newPath)
        {
            if (previousPath == null) return false;
            string SessionOf(string path)
            {
                var name = Path.GetFileNameWithoutExtension(path); // Journal.<stamp>.<part>
                int lastDot = name.LastIndexOf('.');
                return lastDot > 0 ? name.Substring(0, lastDot) : name;
            }

            return string.Equals(SessionOf(previousPath), SessionOf(newPath), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Path.GetDirectoryName(previousPath), Path.GetDirectoryName(newPath), StringComparison.OrdinalIgnoreCase);
        }

        private string PickJournal(Entry entry)
        {
            var earliest = entry.StartUtc == DateTime.MinValue ? DateTime.MinValue : entry.StartUtc - StartTimeSlack;
            var candidates = new DirectoryInfo(entry.JournalFolder)
                .GetFiles("Journal.*.log")
                .Where(f => f.LastWriteTimeUtc >= earliest)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            if (candidates.Count == 0) return null;

            bool folderShared = _entries.Values.Any(o => o != entry &&
                string.Equals(o.JournalFolder, entry.JournalFolder, StringComparison.OrdinalIgnoreCase));

            if (!folderShared) return candidates[0].FullName;

            // Several games write into the same folder: ask Windows which file this process holds open.
            foreach (var file in candidates)
            {
                var pids = SafeCall(() => _filePidsLookup(file.FullName), Array.Empty<int>());
                if (pids.Contains(entry.Pid)) return file.FullName;
            }

            // Could not tell; keep the previous choice rather than guessing another commander's file.
            return entry.JournalPath;
        }

        private static void ReadNewLines(Entry entry)
        {
            using var stream = new FileStream(entry.JournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length < entry.Offset) entry.Offset = 0; // file was replaced
            long available = stream.Length - entry.Offset;
            if (available <= 0) return;

            int toRead = (int)Math.Min(available, MaxReadBytes);
            var buffer = new byte[toRead];
            stream.Seek(entry.Offset, SeekOrigin.Begin);
            int read = 0;
            while (read < toRead)
            {
                int n = stream.Read(buffer, read, toRead - read);
                if (n == 0) break;
                read += n;
            }

            // Only consume complete lines; a half-written line is read again next time.
            int lastNewLine = read == 0 ? -1 : Array.LastIndexOf(buffer, (byte)'\n', read - 1);
            if (lastNewLine < 0) return;
            entry.Offset += lastNewLine + 1;

            var text = Encoding.UTF8.GetString(buffer, 0, lastNewLine + 1);
            foreach (var line in text.Split('\n'))
            {
                var name = ExtractCommander(line);
                if (name != null) entry.Commander = name;
            }
        }

        /// <summary>Commander name from a journal line, or null if the line does not name one.</summary>
        public static string ExtractCommander(string line)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                (line.IndexOf("Commander", StringComparison.Ordinal) < 0 && line.IndexOf("LoadGame", StringComparison.Ordinal) < 0))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("event", out var ev) || ev.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                switch (ev.GetString())
                {
                    case "Commander": return ReadString(root, "Name");
                    case "LoadGame": return ReadString(root, "Commander");
                    default: return null;
                }
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string ReadString(JsonElement root, string property)
        {
            if (root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()?.Trim();
                return string.IsNullOrEmpty(text) ? null : text;
            }

            return null;
        }

        private static T SafeCall<T>(Func<T> call, T fallback)
        {
            try
            {
                return call();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException ||
                                       ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException ||
                                       ex is ArgumentException || ex is SystemException)
            {
                return fallback;
            }
        }

        private static DateTime SafeStartTimeUtc(Process process)
        {
            try
            {
                return process.StartTime.ToUniversalTime();
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is NotSupportedException)
            {
                return DateTime.MinValue;
            }
        }

        #region Windows lookups

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint TOKEN_QUERY = 0x0008;
        private const int TokenUser = 1;
        private static readonly Guid FOLDERID_SavedGames = new Guid("4C5C32FF-BB9D-43b0-B5B4-2D72E54EAAA4");

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr tokenHandle, int infoClass, IntPtr info, int infoLength, out int returnLength);

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid folderId, uint flags, IntPtr token, out IntPtr path);

        /// <summary>SID and short user name of the account running a process.</summary>
        private static (string Sid, string UserName) GetProcessOwner(int pid)
        {
            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (process == IntPtr.Zero) return (null, null);
            try
            {
                if (!OpenProcessToken(process, TOKEN_QUERY, out IntPtr token)) return (null, null);
                try
                {
                    GetTokenInformation(token, TokenUser, IntPtr.Zero, 0, out int length);
                    if (length <= 0) return (null, null);
                    IntPtr buffer = Marshal.AllocHGlobal(length);
                    try
                    {
                        if (!GetTokenInformation(token, TokenUser, buffer, length, out _)) return (null, null);
                        var sid = new SecurityIdentifier(Marshal.ReadIntPtr(buffer)); // TOKEN_USER.User.Sid
                        string name = sid.Value;
                        try
                        {
                            name = ((NTAccount)sid.Translate(typeof(NTAccount))).Value;
                            int slash = name.LastIndexOf('\\');
                            if (slash >= 0) name = name.Substring(slash + 1);
                        }
                        catch (IdentityNotMappedException)
                        {
                        }

                        return (sid.Value, name);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
                finally
                {
                    CloseHandle(token);
                }
            }
            finally
            {
                CloseHandle(process);
            }
        }

        /// <summary>Elite journal folder of a Windows user, e.g. C:\Users\Name\Saved Games\Frontier Developments\Elite Dangerous.</summary>
        private static string GetJournalFolder(string sid)
        {
            string savedGames = null;

            string ownSid;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                ownSid = identity.User?.Value;
            }

            if (string.Equals(sid, ownSid, StringComparison.OrdinalIgnoreCase))
            {
                savedGames = GetOwnSavedGamesFolder();
            }

            savedGames ??= GetSavedGamesFolderFromRegistry(sid);

            return savedGames == null ? null : Path.Combine(savedGames, "Frontier Developments", "Elite Dangerous");
        }

        private static string GetOwnSavedGamesFolder()
        {
            if (SHGetKnownFolderPath(FOLDERID_SavedGames, 0, IntPtr.Zero, out IntPtr path) != 0) return null;
            try
            {
                return Marshal.PtrToStringUni(path);
            }
            finally
            {
                Marshal.FreeCoTaskMem(path);
            }
        }

        private static string GetSavedGamesFolderFromRegistry(string sid)
        {
            string profile;
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\" + sid))
            {
                profile = key?.GetValue("ProfileImagePath") as string;
            }

            if (!string.IsNullOrEmpty(profile)) profile = Environment.ExpandEnvironmentVariables(profile);

            // The user's registry hive is loaded while one of their processes (the game) is running.
            try
            {
                using var key = Registry.Users.OpenSubKey(sid + @"\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
                var raw = key?.GetValue(FOLDERID_SavedGames.ToString("B"), null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                if (!string.IsNullOrEmpty(raw))
                {
                    // %USERPROFILE% must point at that user's profile, not ours.
                    if (!string.IsNullOrEmpty(profile)) raw = raw.Replace("%USERPROFILE%", profile, StringComparison.OrdinalIgnoreCase);
                    if (raw.IndexOf("%USERPROFILE%", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return Environment.ExpandEnvironmentVariables(raw);
                    }
                }
            }
            catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException)
            {
            }

            return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, "Saved Games");
        }

        #endregion
    }

    /// <summary>Asks Windows which processes have a file open (Restart Manager API; no admin needed for own files).</summary>
    [SupportedOSPlatform("windows")]
    internal static class RestartManager
    {
        private const int ERROR_MORE_DATA = 234;

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string strServiceShortName;
            public int ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint sessionHandle, int flags, StringBuilder sessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint sessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint sessionHandle, uint fileCount, string[] fileNames, uint applicationCount, IntPtr applications, uint serviceCount, string[] serviceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint sessionHandle, out uint needed, ref uint count, [In, Out] RM_PROCESS_INFO[] processInfo, ref uint rebootReasons);

        public static int[] GetProcessIdsUsingFile(string path)
        {
            if (RmStartSession(out uint session, 0, new StringBuilder(64)) != 0) return Array.Empty<int>();
            try
            {
                if (RmRegisterResources(session, 1, new[] { path }, 0, IntPtr.Zero, 0, null) != 0) return Array.Empty<int>();

                uint needed = 0, count = 0, reasons = 0;
                int result = RmGetList(session, out needed, ref count, null, ref reasons);
                if (result != ERROR_MORE_DATA || needed == 0) return Array.Empty<int>();

                var info = new RM_PROCESS_INFO[needed];
                count = needed;
                if (RmGetList(session, out needed, ref count, info, ref reasons) != 0) return Array.Empty<int>();

                var pids = new int[count];
                for (int i = 0; i < count; i++) pids[i] = info[i].Process.dwProcessId;
                return pids;
            }
            finally
            {
                RmEndSession(session);
            }
        }
    }
}
