# Aviassembly Performance 240 1.0.1

[Download Aviassembly.Performance240.dll](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/Performance240/Aviassembly.Performance240.dll) · [SHA256](SHA256.txt)

With the game closed, copy the DLL to `BepInEx/plugins/Aviassembly.Performance240/`. It targets a 4.17 ms frame budget. Frame rate depends on the aircraft, scenery and hardware. Configuration is in `BepInEx/config/aviassembly.performance240.cfg` after first launch.

## PowerShell installation

Close Aviassembly, download [Install-Performance240.ps1](Install-Performance240.ps1), and run `.\Install-Performance240.ps1` from its folder. For a custom Steam library, pass `-GameDir 'D:\SteamLibrary\steamapps\common\Aviassembly'`. The installer downloads the [tested v1.0.1 ZIP](Aviassembly_Performance_240_1.0.1.zip) automatically, or uses a copy beside the script. It verifies the archive and DLL and backs up the installed plugin before replacing it.

The legacy fog sorting fix activates with Atmosphere 0.7.0. The ZIP and standalone download contain the same DLL used for the fog tests.
