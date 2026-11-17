# Wing Inspector

Source-only development helper for inspecting wing geometry in Aviassembly. It is not required to use Wing Flaps or Editor Tools.

[WingInspector.cs](WingInspector.cs) defines a BepInEx plugin. When built against the game's assemblies and loaded, F6 writes loaded scene-wing properties, child transforms and mesh names to the BepInEx log. It also exports a readable active Big Wing mesh as base64.

This helper inspects loaded scene objects; it does not establish the game's complete wing catalog. For catalog capture, use [the native asset capture workflow](../Flaps/MODEL-UPDATES.md).

No compiled Wing Inspector download or standalone build project is provided here.
