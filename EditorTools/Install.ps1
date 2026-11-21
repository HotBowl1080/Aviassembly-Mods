param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Aviassembly')
$ErrorActionPreference = 'Stop'
$gameRoot = (Get-Item -LiteralPath $GameDir).FullName
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
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
$installId = [Guid]::NewGuid().ToString('N')
$pending = Join-Path $targetDir ("Aviassembly.EditorTools.$installId.pending")
$lockPath = Join-Path $targetDir 'Aviassembly.EditorTools.installing'
$lock = [IO.FileStream]::new($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite,
    [IO.FileShare]::None, 4096, [IO.FileOptions]::DeleteOnClose)
try {
    $expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $source -Destination $pending
    if ((Get-FileHash -LiteralPath $pending -Algorithm SHA256).Hash -ne $expected) { throw 'Staged DLL checksum mismatch.' }
    $assembly = [Reflection.AssemblyName]::GetAssemblyName($pending)
    if ($assembly.Name -ne 'Aviassembly.EditorTools') { throw 'The staged file is not the Editor Tools plugin.' }
    if (Test-Path -LiteralPath $target) {
        $backupDir = Join-Path $PSScriptRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $installId)
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backupDir
        [IO.File]::Replace($pending, $target, [System.Management.Automation.Language.NullString]::Value)
    } else {
        [IO.File]::Move($pending, $target)
    }
    $installed = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($installed -ne $expected) { throw 'Installed DLL checksum mismatch.' }
    [pscustomobject]@{ Installed = $target; SHA256 = $installed; Version = $assembly.Version.ToString() }
} finally {
    try { if (Test-Path -LiteralPath $pending) { Remove-Item -LiteralPath $pending -Force } }
    finally { $lock.Dispose() }
}
