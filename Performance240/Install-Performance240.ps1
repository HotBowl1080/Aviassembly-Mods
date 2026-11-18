param(
    [string]$GameDir = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Aviassembly",
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$packageName = 'Aviassembly_Performance_240_1.0.1.zip'
$packageHash = '34EF05C615084EB9AFFAB63E72D8B61EEF3958FDD4E486717BCF179BEF1CDBB0'
$dllName = 'Aviassembly.Performance240.dll'
$dllHash = '6C03118CC6206D9E27694CBCDF3BA98A43EAF1B30D33EB5DFD2A02021CED040C'
$plugins = Join-Path $GameDir 'BepInEx\plugins'
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'Aviassembly.exe') -PathType Leaf) -or
    !(Test-Path -LiteralPath $plugins -PathType Container)) {
    throw 'Choose an Aviassembly installation with BepInEx 5 using -GameDir.'
}
if (Get-Process -Name Aviassembly -ErrorAction SilentlyContinue) {
    throw 'Close Aviassembly and run the installer again.'
}

$installed = @(Get-ChildItem -LiteralPath $plugins -Recurse -File -Filter $dllName)
if ($installed.Count -gt 1) { throw 'Multiple Performance 240 DLLs are installed. Keep one copy and retry.' }
$folder = if ($installed.Count) { $installed[0].DirectoryName } else { Join-Path $plugins 'Aviassembly.Performance240' }
$target = Join-Path $folder $dllName
if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -eq $dllHash) {
    Write-Output 'Performance 240 v1.0.1 is already installed and verified.'
    return
}

$download = $null
$staged = $null
try {
    if (!$PackagePath) {
        $PackagePath = Join-Path $PSScriptRoot $packageName
        if (!(Test-Path -LiteralPath $PackagePath)) {
            $download = [IO.Path]::GetTempFileName()
            $PackagePath = $download
            $url = "https://raw.githubusercontent.com/HotBowl1080/Aviassembly-Mods/main/Performance240/$packageName"
            Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $download
        }
    }
    if ((Get-FileHash -LiteralPath $PackagePath).Hash -ne $packageHash) { throw 'Package checksum mismatch.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $staged = Join-Path $folder ([IO.Path]::GetRandomFileName())
    $zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
    try {
        $entry = $zip.GetEntry("BepInEx/plugins/Aviassembly.Performance240/$dllName")
        if (!$entry) { throw 'The package is missing the plugin DLL.' }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $staged)
    } finally { $zip.Dispose() }
    if ((Get-FileHash -LiteralPath $staged).Hash -ne $dllHash) { throw 'Plugin checksum mismatch.' }
    if (Test-Path -LiteralPath $target) {
        [IO.File]::Replace($staged, $target, "$target.bak")
    } else {
        [IO.File]::Move($staged, $target)
    }
    Write-Output "Installed and verified Performance 240 v1.0.1 at $target"
} finally {
    if ($staged -and (Test-Path -LiteralPath $staged)) { Remove-Item -LiteralPath $staged }
    if ($download) { Remove-Item -LiteralPath $download }
}
