[CmdletBinding()]
param(
    [string]$ZipPath,
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Aviassembly'
)

$ErrorActionPreference = 'Stop'
$zipName = 'Flap_Mod_0.23.1.zip'
$dllName = 'AviassemblyFlaps.dll'
$dllHash = '06A29490E4A2825927418DBE03B633BC65087A9A9FBEB509EC38458E9EB4FD73'
$entryName = "BepInEx/plugins/AviassemblyFlaps/$dllName"

if (!$ZipPath) {
    $candidates = @(
        (Join-Path $PSScriptRoot $zipName)
        (Join-Path (Join-Path $env:USERPROFILE 'Downloads') $zipName)
    )
    $ZipPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (!$ZipPath) { throw "Put $zipName beside this script or pass its full path with -ZipPath." }
}
$zipFile = (Resolve-Path -LiteralPath $ZipPath).ProviderPath
$game = (Resolve-Path -LiteralPath $GameDir).ProviderPath
$bepInEx = Join-Path $game 'BepInEx'
if (!(Test-Path -LiteralPath (Join-Path $game 'Aviassembly.exe') -PathType Leaf) -or
    !(Test-Path -LiteralPath (Join-Path $bepInEx 'core\BepInEx.dll') -PathType Leaf)) {
    throw 'Pass an Aviassembly game folder with BepInEx installed using -GameDir.'
}
if (Get-Process -Name 'Aviassembly' -ErrorAction SilentlyContinue) { throw 'Close Aviassembly before installing.' }

$plugins = Join-Path $bepInEx 'plugins'
$installed = @()
if (Test-Path -LiteralPath $plugins -PathType Container) {
    $installed = @(Get-ChildItem -LiteralPath $plugins -Filter $dllName -Recurse -File)
}
if ($installed.Count -gt 1) {
    throw ("More than one Flap Mod DLL is installed. Keep one copy and rerun the installer.`n" + ($installed.FullName -join "`n"))
}
$target = if ($installed.Count -eq 1) { $installed[0].FullName } else { Join-Path $plugins "AviassemblyFlaps\$dllName" }
if ($installed.Count -eq 1 -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $dllHash) {
    Write-Output "Flap Mod 0.23.1 is already installed at $target"
    return
}

$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N')
$pending = $target + '.' + $runId + '.pending'
$backup = $null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zipFile)
try {
    $entry = $archive.GetEntry($entryName)
    if (!$entry) { throw "This ZIP does not contain $entryName." }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $pending)
    if ((Get-FileHash -LiteralPath $pending -Algorithm SHA256).Hash -ne $dllHash) {
        throw 'The DLL checksum does not match Flap Mod 0.23.1. Download the release again.'
    }
    if (Get-Process -Name 'Aviassembly' -ErrorAction SilentlyContinue) { throw 'Close Aviassembly before installing.' }

    if ($installed.Count -eq 1) {
        $backupDir = Join-Path $bepInEx "mod-backups\FlapMod\$runId"
        [void][IO.Directory]::CreateDirectory($backupDir)
        $backup = Join-Path $backupDir $dllName
        [IO.File]::Replace($pending, $target, $backup)
    } else {
        [IO.File]::Move($pending, $target)
    }
} finally {
    $archive.Dispose()
    if (Test-Path -LiteralPath $pending -PathType Leaf) { Remove-Item -LiteralPath $pending -Force }
}

Write-Output "Installed Flap Mod 0.23.1 at $target"
if ($backup) { Write-Output "Previous DLL backed up to $backup" }
