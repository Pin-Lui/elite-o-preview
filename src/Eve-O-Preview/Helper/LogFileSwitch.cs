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

namespace EveOPreview.Helper
{
    // Log files are only written when "Write log file" is enabled in the settings,
    // or when the app is started with --verbose / -v (the Verbose Logging shortcut).
    public static class LogFileSwitch
    {
        private static volatile bool _enabledBySetting;
        private static volatile bool _forcedByCommandLine;

        public static bool IsEnabled => _forcedByCommandLine || _enabledBySetting;

        public static void ForceByCommandLine(bool forced) => _forcedByCommandLine = forced;

        public static void SetEnabledBySetting(bool enabled) => _enabledBySetting = enabled;
    }
}
