param([string]$GameDir = 'E:\SteamLibrary\steamapps\common\Aviassembly')
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameDir 'Aviassembly_Data\Managed'
$compiler = Get-ChildItem 'C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll' | Sort-Object FullName -Descending | Select-Object -First 1
if (!$compiler) { throw 'The .NET SDK C# compiler is required.' }
$arguments = @('/nologo','/target:library','/nostdlib+','/langversion:latest',('/out:' + (Join-Path $PSScriptRoot 'AviassemblyFlaps.dll')))
foreach ($assembly in Get-ChildItem -LiteralPath $managed -Filter '*.dll') { $arguments += '/reference:' + $assembly.FullName }
foreach ($name in @('BepInEx.dll','0Harmony.dll')) { $arguments += '/reference:' + (Join-Path $GameDir ('BepInEx\core\' + $name)) }
$arguments += (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs').FullName
& dotnet $compiler.FullName @arguments
if ($LASTEXITCODE -ne 0) { throw 'Flaps compilation failed.' }
Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'AviassemblyFlaps.dll') -Algorithm SHA256
