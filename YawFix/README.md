# Yaw Fix 1.0.1

Clears residual rudder input when you release Q or E. The plane can still rotate from momentum. Requires BepInEx 5. The DLL was built successfully, but in-game flight behavior has yet to be verified.

[Download YawFix.dll](https://github.com/HotBowl1080/Aviassembly-Mods/raw/refs/heads/main/YawFix/YawFix.dll), copy it into `Aviassembly/BepInEx/plugins/`, and restart the game. The BepInEx log should say `Yaw Fix enabled immediate rudder release`.

Build from source with `dotnet build YawFix.csproj -c Release -p:AviassemblyDir="path to Aviassembly"`.
