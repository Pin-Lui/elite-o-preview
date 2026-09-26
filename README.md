# Elite-O Preview

Live thumbnails of every running **Elite Dangerous** client, with click or hotkey switching between them. Made for multiboxing several commanders on one PC.

Elite-O Preview is adapted from [EVE-O Preview](https://github.com/EveOPlus/eve-o-preview) by Aura Asuna and is licensed under the GPLv3, like the original. It is not affiliated with or endorsed by Frontier Developments.

## License
Original work Copyright © 2026 Aura Asuna. Elite Dangerous adaptation © 2026 Pin-Lui.

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.

## What it does and does not do

It is a task switcher. It shows a live preview of each game window and brings the one you pick to the front.

The program does NOT:
* modify the Elite Dangerous interface
* inject code into Elite Dangerous
* broadcast any keyboard or mouse events
* interact with Elite Dangerous except to resize it, bring it to the foreground, and read its journal files to find out which commander is playing.

## Build

1. Install the **.NET 10 SDK** (Windows x64): https://dotnet.microsoft.com/download/dotnet/10.0
2. Double-click `build.cmd`. The app is placed in the `Elite-O Preview` folder next to it.

The build is a single, self-contained `Elite-O Preview.exe` with .NET built in. To use it on another PC, copying that one file is enough; nothing needs to be installed there. The other files in the folder (licence, verbose-logging shortcut) are optional.

## Install & Use

1. Start `Elite-O Preview\Elite-O Preview.exe`. It asks for **administrator rights**, which it needs to see which Windows user runs each game and to read that user's journals.
2. Launch your games as usual, for example with min-ed-launcher. The start order does not matter.
3. Set Elite's display mode to **Windowed** or **Borderless**. Exclusive fullscreen cannot be previewed.

Settings are stored in the `Profiles` folder next to the exe. `Launch Elite-O Preview with Verbose Logging.cmd` writes a detailed log to the `logs` folder, which helps when something goes wrong.

## How clients are named

Every Elite window has the same caption (`Elite - Dangerous (CLIENT)`), so Elite-O Preview works out each client's name from the game's journal:

game process → Windows user that runs it → that user's `Saved Games\Frontier Developments\Elite Dangerous` folder → newest journal written since the game started → the commander named in it.

| State | Client name |
| --- | --- |
| Game started, commander not logged in yet | `Elite - <Windows user> (not logged in)` |
| Commander logged in | `Elite - <COMMANDER NAME>` |

The name appears within a couple of seconds of logging in. Thumbnail positions, border colours, cycle groups and window layouts are all saved under this full name, for example `Elite - DIRTYRODRIGUEZ`.

If two games run under the **same** Windows user, Windows is asked which journal file each game has open. The recommended setup is still one Windows user per commander.

## Application Options

### **General** Tab
| Option | Description |
| --- | --- |
| Minimize to System Tray | Minimize the main window to the Windows tray when it is closed |
| Track client locations | Restore each client's window position when it is started or activated |
| Hide preview of active Elite client | Don't show the thumbnail of the client you are currently playing |
| Minimize inactive Elite clients | Minimize clients automatically when you switch away from them |
| Previews always on top | Keep thumbnails above all other windows |
| Hide previews when no Elite client is active | Only show thumbnails while an Elite client (or a thumbnail) has focus |
| Unique layout for each commander | Remember separate thumbnail positions depending on which commander is active |

### **Thumbnail** Tab
| Option | Description |
| --- | --- |
| Opacity | Opacity of inactive thumbnails (from 20% to 100%) |
| Thumbnail Width | **100** to **640** points |
| Thumbnail Height | **80** to **400** points |
| Reset position and size | Asks for confirmation, then sets all previews back to the default size (384 × 216) and lines them up side by side in the top-left corner of the main screen |

### **Zoom** Tab
| Option | Description |
| --- | --- |
| Zoom on hover | Enlarge a thumbnail while the mouse is over it |
| Zoom factor | **2** to **10** |
| Zoom anchor | The corner or edge the zoomed thumbnail grows from |

### **Overlay** Tab
| Option | Description |
| --- | --- |
| Show overlay | Show the commander name on each thumbnail |
| Show frames | Show thumbnails with window caption and borders |
| Highlight active client | Draw a coloured border around the thumbnail of the active client |
| Color | Colour of that border |
| Title Font | Font, colours, outline and offset of the name shown on thumbnails |

### **Active Clients** Tab
| Option | Description |
| --- | --- |
| Thumbnails list | Running clients. Checking one hides its thumbnail until the client or the app restarts |
| Hide Thumbnails | Hide all thumbnails until toggled again. Double-click the box next to "Hotkey" to set a hotkey |
| Minimize | Minimize all Elite clients. Double-click the box next to "Hotkey" to set a hotkey |

### **Cycle Groups** Tab
| Option | Description |
| --- | --- |
| Select Cycle Group | Pick the group to view or edit. `+` creates a group, `-` deletes it |
| Group Name | Name of the group. `+` selects the new group with its name highlighted: type a name and press **Enter** (or click elsewhere) to save it, **Esc** to undo. Names must be unique |
| Forward Key / Backward Key | Hotkeys to cycle through the group. Double-click a box to set a primary or secondary key. With a single client, only set the forward key |
| Clients and Order `+` / `-` / Up | Add a running client, remove the selected one, or move it up in the cycle order |

### **Profiles** Tab
Profiles are complete, independent copies of all settings that can be switched while the app is running. The app always starts with the Default profile.

| Option | Description |
| --- | --- |
| Clone Current Profile | Create a new profile as a copy of the current one |
| Delete Current Profile | Delete the selected profile (Default cannot be deleted) |
| Current Profile | Name of the profile, which is also its folder name |

### Mouse Gestures and Actions
| Action | Gesture |
| --- | --- |
| Bring the Elite client to the front | Click its thumbnail |
| Minimize the Elite client | Ctrl + click the thumbnail, or right-click → Minimize |
| Minimize all Elite clients | Right-click any thumbnail → Minimize All |
| Switch to the last used window that is not an Elite client | Ctrl + Shift + click any thumbnail |
| Move a thumbnail | Hold the right mouse button for a moment, or right-click → Move, then click when done |
| Resize one thumbnail | Right-click → Resize, then click when done. Only that commander's preview changes, and its size is remembered. Hold Shift to keep the aspect ratio |
| Resize all thumbnails | Right-click → Resize All, then click when done. Every preview gets the same size (this also replaces individual sizes). Hold Shift to keep the aspect ratio |

### Configuration File-Only Options

Edit `Profiles\<profile>\Elite-O Preview.json` only while Elite-O Preview is closed, and keep a backup.

| Option | Description |
| --- | --- |
| **ActiveClientHighlightThickness** | Border thickness of the active client highlight, **1**...**6** (default **3**) |
| **CompatibilityMode** | Screenshot-based thumbnails instead of live DWM previews (default **false**). Works over remote desktop, but uses more memory and refreshes at 1 FPS |
| **EnableThumbnailSnap** | Snap thumbnails to each other when moved (default **true**) |
| **HideThumbnailsDelay** | Delay, in refresh periods, before thumbnails hide when no Elite client is active (default **2**, about 1 second) |
| **PriorityClients** | Clients that are never auto-minimized, e.g. `"PriorityClients": [ "Elite - DIRTYRODRIGUEZ" ]` |
| **ThumbnailMinimumSize** / **ThumbnailMaximumSize** | Size limits, e.g. `"100, 80"` and `"640, 400"` |
| **ThumbnailRefreshPeriod** | Refresh period in milliseconds, **300**...**1000** (default **500**) |

### Per Client Border Color

To give one commander its own highlight colour, edit **PerClientActiveClientHighlightColor** in the profile file:

    "PerClientActiveClientHighlightColor": {
      "Elite - DIRTYRODRIGUEZ": "Red",
      "Elite - PIN-LUI": "Green"
    }

Clients not listed use the global highlight colour. Supported colour names: https://docs.microsoft.com/en-us/dotnet/api/system.drawing.color#properties

### Hotkey hints
* Hotkeys are global. Unusual keys such as F13–F24, bound to a mouse or game pad button, avoid clashes with Elite's own bindings.
* Supported keys: https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys

## Differences from EVE-O Preview
* Watches `EliteDangerous64.exe` and names clients by commander (see above).
* The **FPS / Audio** features (FPS limiter, audio muting, focus prediction) are removed. They depend on a DLL injected into the game client.
* **Dynamic CPU affinity** is removed. It limits the active client to two CPU threads, which suits EVE but slows Elite down.
* Uses its own settings, log and single-instance names, so it can run next to EVE-O Preview.

## Credits

Elite-O Preview is made by **Pin-Lui** and is based on EVE-O Preview.

* Elite-O Preview: **Pin-Lui**. Source: https://github.com/Pin-Lui/elite-o-preview
* EVE-O Preview maintained by **Aura Asuna**. Source: https://github.com/EveOPlus/eve-o-preview
* Created by **StinkRay**
* Previous maintainers: **Phrynohyas Tig-Rah**, **Makari Aeron**, **StinkRay**
* With contributions from **CCP FoxFour**
* Original repository: https://bitbucket.org/ulph/eve-o-preview-git

Elite Dangerous is a trademark of Frontier Developments plc. This program is not affiliated with or endorsed by Frontier Developments. EVE Online is a trademark of CCP hf.
