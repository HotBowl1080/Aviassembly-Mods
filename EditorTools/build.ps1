param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Aviassembly')
$ErrorActionPreference = 'Stop'
$env:MSBuildEnableWorkloadResolver = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.work\dotnet'
$GameDir = (Get-Item -LiteralPath $GameDir).FullName
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'))) { throw 'BepInEx 5 is required in GameDir.' }
Push-Location $PSScriptRoot
try {
    dotnet build .\Aviassembly.EditorTools.csproj -c Release "-p:GameDir=$GameDir" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $output = Join-Path $PSScriptRoot 'dist\BepInEx\plugins\Aviassembly.EditorTools'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\netstandard2.0\Aviassembly.EditorTools.dll') -Destination $output -Force
    Get-FileHash -LiteralPath (Join-Path $output 'Aviassembly.EditorTools.dll') -Algorithm SHA256
} finally { Pop-Location }
