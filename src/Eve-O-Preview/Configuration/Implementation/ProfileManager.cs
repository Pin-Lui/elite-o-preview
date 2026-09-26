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

using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EveOPreview.Configuration.Interface;
using EveOPreview.Configuration.Model;
using EveOPreview.Helper;
using EveOPreview.Mediator.Messages;
using MediatR;

namespace EveOPreview.Configuration.Implementation;

public class ProfileManager : IProfileManager
{
    private const string BASE_FILENAME = "Elite-O-Preview.json";
    private const string PROFILES_DIR = "Profiles";
    private const string DEFAULT_PROFILE_DIR = "Default";
    private const string APP_FOLDER_NAME = "Elite-O-Preview";
    private const string LEGACY_BASE_FILENAME = "Elite-O Preview.json";
    private const string LEGACY_APP_FOLDER_NAME = "Elite-O Preview";

    private readonly ILogger _logger;
    private readonly IMediator _mediator;

    public string ProfileRootDirectory { get; }
    public List<ProfileLocation> ProfileLocations { get; private set; }

    public ProfileManager(ILogger logger, IMediator mediator) : this(logger, mediator, null) { }

    internal ProfileManager(ILogger logger, IMediator mediator, string profileRootDirectory)
    {
        _logger = logger;
        this._mediator = mediator;
        ProfileRootDirectory = profileRootDirectory ?? FindOrCreateProfileRootDirectory();
        Directory.CreateDirectory(Path.Combine(ProfileRootDirectory, DEFAULT_PROFILE_DIR));
        _logger.WithCallerInfo().Information($"Profiles Root Directory located at {ProfileRootDirectory}");

        if (profileRootDirectory == null) MigrateLegacySingleProfile();
        MigrateLegacyProfileFileNames();

        ProfileLocations = RefreshProfileLocations();
    }

    private string FindOrCreateProfileRootDirectory()
    {
        string exePath = System.IO.Path.GetDirectoryName(System.Environment.ProcessPath);
        string localProfilesPath = Path.Combine(exePath, PROFILES_DIR);

        if (Directory.Exists(localProfilesPath))
        {
            return localProfilesPath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDataPath = Path.Combine(localAppData, APP_FOLDER_NAME);
        string appDataProfilesPath = Path.Combine(appDataPath, PROFILES_DIR);
        MigrateLegacyAppDataFolder(Path.Combine(localAppData, LEGACY_APP_FOLDER_NAME), appDataPath);

        if (Directory.Exists(appDataProfilesPath))
        {
            return appDataProfilesPath;
        }

        try
        {
            Directory.CreateDirectory(localProfilesPath);
            Directory.CreateDirectory(Path.Combine(localProfilesPath, DEFAULT_PROFILE_DIR));

            return localProfilesPath;
        }
        catch (UnauthorizedAccessException)
        {
            Directory.CreateDirectory(appDataProfilesPath);
            Directory.CreateDirectory(Path.Combine(appDataProfilesPath, DEFAULT_PROFILE_DIR));

            return appDataProfilesPath;
        }
    }

    private void MigrateLegacyAppDataFolder(string legacyPath, string newPath)
    {
        if (Directory.Exists(newPath) || !Directory.Exists(legacyPath))
        {
            return;
        }

        try
        {
            Directory.Move(legacyPath, newPath);
            _logger.WithCallerInfo().Information("Renamed settings folder {LegacyPath} to {NewPath}", legacyPath, newPath);
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Warning(ex, "Could not rename settings folder {LegacyPath}", legacyPath);
        }
    }

    private void MigrateLegacyProfileFileNames()
    {
        foreach (string dirPath in Directory.GetDirectories(this.ProfileRootDirectory))
        {
            string legacyFile = Path.Combine(dirPath, LEGACY_BASE_FILENAME);
            string newFile = Path.Combine(dirPath, BASE_FILENAME);
            if (File.Exists(newFile) || !File.Exists(legacyFile))
            {
                continue;
            }

            try
            {
                File.Move(legacyFile, newFile);
                _logger.WithCallerInfo().Information("Renamed profile file {LegacyFile} to {NewFile}", legacyFile, newFile);
            }
            catch (Exception ex)
            {
                _logger.WithCallerInfo().Warning(ex, "Could not rename profile file {LegacyFile}", legacyFile);
            }
        }
    }

    private void MigrateLegacySingleProfile()
    {
        string exePath = System.IO.Path.GetDirectoryName(System.Environment.ProcessPath);
        string sourceFile = Path.Combine(exePath, BASE_FILENAME);

        string destDir = Path.Combine(ProfileRootDirectory, DEFAULT_PROFILE_DIR);
        string destFile = Path.Combine(destDir, BASE_FILENAME);

        if (!File.Exists(sourceFile) || File.Exists(destFile))
        {
            return;
        }

        _logger.WithCallerInfo().Information($"Located a legacy profile to be migrated");

        try
        {
            _logger.WithCallerInfo().Information($"Copying profile from {sourceFile} to {destFile}");
            File.Copy(sourceFile, destFile, overwrite: false);

            string backupPath = sourceFile + ".bak";
            int counter = 1;

            while (File.Exists(backupPath))
            {
                backupPath = $"{sourceFile}.bak({counter})";
                counter++;
            }

            _logger.WithCallerInfo().Information($"Moving old profile from {sourceFile} to {backupPath}");
            File.Move(sourceFile, backupPath);
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Error($"Error while moving the old profile", ex);
        }
    }

    public List<ProfileLocation> RefreshProfileLocations()
    {
        var locations = new List<ProfileLocation>();

        if (!Directory.Exists(this.ProfileRootDirectory))
        {
            _logger.WithCallerInfo().Error($"{nameof(ProfileRootDirectory)} does not exist!");
            return locations;
        }

        string[] profileDirs = Directory.GetDirectories(this.ProfileRootDirectory);

        foreach (string dirPath in profileDirs)
        {
            string baseJsonPath = Path.Combine(dirPath, BASE_FILENAME);
            string profileName = Path.GetFileName(dirPath);

            if (File.Exists(baseJsonPath) || profileName.Equals(DEFAULT_PROFILE_DIR, StringComparison.OrdinalIgnoreCase))
            {
                locations.Add(new ProfileLocation
                {
                    FriendlyName = profileName,
                    FolderPath = dirPath,
                    FullPath = baseJsonPath
                });
            }
        }

        ProfileLocations = locations;
        _mediator.Publish(new ProfileListChangedNotification(locations));

        return locations;
    }

    public ProfileLocation GetDefaultProfileLocation()
    {
        var defaultProfile = ProfileLocations.FirstOrDefault(x => x.FriendlyName.Equals(DEFAULT_PROFILE_DIR, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(defaultProfile?.FullPath))
        {
            _logger.WithCallerInfo()
                .Information($"Unable to locate any profiles at default location {defaultProfile?.FullPath}");
            return null;
        }

        return defaultProfile;
    }

    public void CloneCurrentProfile()
    {
        var currentProfile = _mediator.Send(new GetCurrentProfileLocation()).Result;
        string newProfileName = GenerateNextProfileName(currentProfile.FriendlyName);
        string destDir = Path.Combine(ProfileRootDirectory, newProfileName);

        try
        {
            _mediator.Send(new SaveConfiguration()).GetAwaiter().GetResult();
            CopyDirectory(currentProfile.FolderPath, destDir);

            RefreshProfileLocations();
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Error(ex, $"Failed to clone profile {currentProfile.FriendlyName}");
        }
    }

    public void DeleteCurrentProfile()
    {
        var currentProfile = _mediator.Send(new GetCurrentProfileLocation()).Result;
        if (currentProfile.FriendlyName.Equals(DEFAULT_PROFILE_DIR, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(currentProfile.FolderPath))
        {
            _logger.WithCallerInfo().Warning($"Failed to delete non existing path {currentProfile.FolderPath}");
            return;
        }

        try
        {
            Directory.Delete(currentProfile.FolderPath, true);
            _logger.WithCallerInfo().Information($"Deleted profile {currentProfile.FriendlyName} directory: {currentProfile.FolderPath}");

            this._mediator.Send(new ChangeSelectedProfile(GetDefaultProfileLocation()));
            RefreshProfileLocations();
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Error(ex, $"Failed to delete profile {currentProfile.FriendlyName}");
        }
    }

    public void RenameCurrentProfile(RenameCurrentProfile request)
    {
        var currentProfile = _mediator.Send(new GetCurrentProfileLocation()).Result;

        if (currentProfile.FriendlyName.Equals(DEFAULT_PROFILE_DIR, StringComparison.OrdinalIgnoreCase) ||
            !IsValidProfileName(request.NewProfileName) ||
            currentProfile.FriendlyName.Equals(request.NewProfileName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string newProfileName = GenerateNextProfileName(request.NewProfileName);
        string destDir = Path.Combine(ProfileRootDirectory, newProfileName);

        try
        {
            Directory.Move(currentProfile.FolderPath, destDir);
            currentProfile.FolderPath = destDir;
            currentProfile.FullPath = Path.Combine(destDir, BASE_FILENAME);
            currentProfile.FriendlyName = newProfileName;
            RefreshProfileLocations();
            _mediator.Publish(new SelectedProfileChangedNotification(currentProfile));
        }
        catch (Exception ex)
        {
            _logger.WithCallerInfo().Error(ex, $"Failed to rename profile {currentProfile.FriendlyName}");
        }
    }

    public static bool IsValidProfileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.EndsWith('.') ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        string stem = name.Split('.')[0].ToUpperInvariant();
        return stem != "CON" && stem != "PRN" && stem != "AUX" && stem != "NUL" &&
            stem != "CONIN$" && stem != "CONOUT$" &&
            !(stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
              "123456789¹²³".Contains(stem[3]));
    }

    private string GenerateNextProfileName(string baseName)
    {
        int i = 1;
        string candidateName = baseName;

        while (Directory.Exists(Path.Combine(ProfileRootDirectory, candidateName)))
        {
            i++;
            candidateName = $"{baseName} ({i})";
        }

        return candidateName;
    }

    private void CopyDirectory(string sourceDir, string destDir)
    { 
        bool sourceExists = Directory.Exists(sourceDir);
        bool destDirExists = Directory.Exists(destDir);
        if (!sourceExists || destDirExists)
        {
            _logger.WithCallerInfo().Warning($"Unable to copy folder. Source [{sourceDir}] Exists = {sourceExists}.  Destination [{destDir}] Exists = {destDirExists}.");
            return;
        }

        Directory.CreateDirectory(destDir);

        foreach (string filePath in Directory.GetFiles(sourceDir))
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(destDir, fileName);
                File.Copy(filePath, destPath, true);
            }
            catch (Exception ex)
            {
                _logger.WithCallerInfo().Error(ex, $"Failed to copy file {filePath}");
            }
        }
    }

}