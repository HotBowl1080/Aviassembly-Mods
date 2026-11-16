param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Aviassembly')
$ErrorActionPreference = 'Stop'
$gameRoot = [IO.Path]::GetFullPath($GameDir).TrimEnd('\')
$source = Join-Path $PSScriptRoot 'dist\BepInEx\plugins\Aviassembly.EditorTools\Aviassembly.EditorTools.dll'
if (!(Test-Path -LiteralPath $source -PathType Leaf)) { $source = Join-Path $PSScriptRoot 'Aviassembly.EditorTools.dll' }
if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Build the mod with build.ps1 first.' }
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'Aviassembly.exe') -PathType Leaf)) { throw 'GameDir does not contain Aviassembly.exe.' }
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'BepInEx\core\BepInEx.dll') -PathType Leaf)) { throw 'Install and run BepInEx 5 first.' }
if (Get-Process -Name Aviassembly -ErrorAction SilentlyContinue) { throw 'Close Aviassembly before installing the mod.' }
$plugins = Join-Path $gameRoot 'BepInEx\plugins'
$targetDir = Join-Path $plugins 'Aviassembly.EditorTools'
$target = Join-Path $targetDir 'Aviassembly.EditorTools.dll'
$duplicates = @(Get-ChildItem -LiteralPath $plugins -Filter 'Aviassembly.EditorTools.dll' -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $target })
if ($duplicates.Count -gt 0) { throw "A second copy exists at $($duplicates[0].FullName). Remove that copy before installing." }
if (Test-Path -LiteralPath $target) {
    $backupDir = Join-Path $PSScriptRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination $backupDir
}
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
$pending = Join-Path $targetDir 'Aviassembly.EditorTools.pending'
Copy-Item -LiteralPath $source -Destination $pending -Force
$expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
if ((Get-FileHash -LiteralPath $pending -Algorithm SHA256).Hash -ne $expected) { throw 'Staged DLL checksum mismatch.' }
Move-Item -LiteralPath $pending -Destination $target -Force
$installed = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if ($installed -ne $expected) { throw 'Installed DLL checksum mismatch.' }
[pscustomobject]@{ Installed = $target; SHA256 = $installed; Version = [Reflection.AssemblyName]::GetAssemblyName($target).Version.ToString() }
