# Elite-O Preview

A fork of [EVE-O Preview](https://github.com/EveOPlus/eve-o-preview) (GPLv3) for **Elite Dangerous** multiboxing:
live thumbnails of every running Elite client, click or hotkey to switch between them.

## Build (once)
1. Install the **.NET 10 SDK** (Windows x64): https://dotnet.microsoft.com/download/dotnet/10.0
2. Double-click `build.cmd`. The app lands in the `Elite-O Preview` folder next to it.

## Use
- Start `Elite-O Preview\Elite-O Preview.exe`. It asks for **admin rights**. It needs them to see which
  Windows user runs each game and to read that user's journals.
- Run Elite in **Windowed** or **Borderless** mode (exclusive fullscreen can't be previewed).
- Launch your games as usual (min-ed-launcher, one Windows user per commander).

## How clients are named
Every Elite window has the same caption, so each client gets its name from the journal instead:

game process → Windows user that runs it → that user's
`Saved Games\Frontier Developments\Elite Dangerous` → newest journal written since the game started →
last `Commander` / `LoadGame` event.

- Before login: `Elite - <Windows user> (not logged in)`
- After login: `Elite - <CMDR NAME>`. Thumbnail positions, colours, hotkeys and cycle groups are saved
  under this name. Use the full name (`Elite - DIRTYRODRIGUEZ`) in cycle groups.
- If two games run under the same Windows user, Windows is asked which journal file each game has open.

## Differences from EVE-O Preview
- Watches `EliteDangerous64.exe` instead of EVE's `ExeFile.exe`.
- **No DLL injection into the game.** The FPS limiter, audio mute and focus prediction live in an injected
  DLL, so they are switched off (their settings do nothing).
- Automatic CPU affinity is **off by default**. It would limit the active game to 2 CPU threads.
- Own settings, log and single-instance names ("Elite-O Preview"), so it can run next to EVE-O Preview.

Changed files: `Services/Implementation/EliteCommanderResolver.cs` (new), `ProcessMonitor.cs`, `HookService.cs`,
`ThumbnailView.cs`, `ThumbnailConfiguration.cs`, `ProfileManager.cs`, `Program.cs`, `ExceptionHandler.cs`,
`MainForm.Designer.cs`, `Eve-O-Preview.csproj`, `app.manifest`, plus `tests/.../EliteCommanderResolverTests.cs`.
