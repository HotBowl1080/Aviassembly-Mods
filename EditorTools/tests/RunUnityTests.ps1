param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Aviassembly')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $projectRoot '.work\UnityTestHost'
$env:MSBuildEnableWorkloadResolver = 'false'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.work\dotnet'
& (Join-Path $projectRoot 'build.ps1') -GameDir $GameDir
dotnet build (Join-Path $PSScriptRoot 'UnityFocusTests.csproj') -c Release "-p:GameDir=$GameDir" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Runtime test build failed.' }
& (Join-Path $PSScriptRoot 'PrepareUnityTests.ps1') -GameDir $GameDir
$report = Join-Path $testRoot 'focus-test-results.txt'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$testArgs = '-batchmode -nographics -logFile "' + (Join-Path $testRoot 'unity-tests.log') + '"'
$testProcess = Start-Process -FilePath (Join-Path $testRoot 'Aviassembly.exe') -WorkingDirectory $testRoot -ArgumentList $testArgs -WindowStyle Hidden -PassThru
if (!$testProcess.WaitForExit(45000)) {
    $running = Get-Process -Id $testProcess.Id -ErrorAction SilentlyContinue
    $expectedExecutable = [IO.Path]::GetFullPath((Join-Path $testRoot 'Aviassembly.exe'))
    if ($running -and [IO.Path]::GetFullPath($running.Path) -eq $expectedExecutable) { Stop-Process -Id $running.Id -Force }
    throw 'The isolated runtime test timed out.'
}
if (!(Test-Path -LiteralPath $report)) { throw 'Unity did not produce a runtime test report; inspect .work\UnityTestHost\unity-tests.log.' }
Get-Content -LiteralPath $report
if ($testProcess.ExitCode -ne 0) { throw "Unity tests failed with exit code $($testProcess.ExitCode)." }
