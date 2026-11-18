param([string]$GameDir = 'E:\SteamLibrary\steamapps\common\Aviassembly')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$testRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '.work\UnityTestHost'))
if (!$testRoot.StartsWith([IO.Path]::GetFullPath((Join-Path $projectRoot '.work')) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid test location.' }
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
# Only copy required player files; no existing plugins, configs, saves, or binaries are modified in the real installation.
foreach ($name in @('Aviassembly.exe','UnityPlayer.dll','winhttp.dll','doorstop_config.ini')) {
    Copy-Item -LiteralPath (Join-Path $GameDir $name) -Destination $testRoot -Force
}
foreach ($name in @('Aviassembly_Data','MonoBleedingEdge')) {
    if (!(Test-Path -LiteralPath (Join-Path $testRoot $name))) { Copy-Item -LiteralPath (Join-Path $GameDir $name) -Destination $testRoot -Recurse }
}
New-Item -ItemType Directory -Path (Join-Path $testRoot 'BepInEx') -Force | Out-Null
if (!(Test-Path -LiteralPath (Join-Path $testRoot 'BepInEx\core'))) {
    Copy-Item -LiteralPath (Join-Path $GameDir 'BepInEx\core') -Destination (Join-Path $testRoot 'BepInEx') -Recurse
}
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$original = Join-Path $GameDir 'Aviassembly_Data\Managed\Assembly-CSharp.dll'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($original)
# Preserve native design loading, pricing, undo and tooltip behavior in the disposable player.
$callbacks = @('Awake','Start','Update','LateUpdate','FixedUpdate','OnEnable','OnDisable','OnDestroy','OnApplicationQuit','OnApplicationFocus','OnApplicationPause','OnGUI','OnLevelWasLoaded')
$count = 0
foreach ($type in $assembly.MainModule.Types) {
    if ($type.Name -eq 'Singleton`1') { continue }
    foreach ($method in $type.Methods) {
        if ($method.Name -eq 'Awake' -and $type.Name -in @('BuildingPart','PaintableRenderer','PartPrefabs','Tooltip','TooltipLayout','TooltipSystem','TooltipArrow')) { continue }
        $runtimeInit = @($method.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'RuntimeInitializeOnLoadMethodAttribute' }).Count -gt 0
        if (!$method.HasBody -or ($method.Name -notin $callbacks -and !$runtimeInit -and !($type.Name -eq 'BuildingCamera' -and $method.Name -eq 'UpdateOrigin'))) { continue }
        $method.Body.Instructions.Clear()
        $method.Body.ExceptionHandlers.Clear()
        $method.Body.Variables.Clear()
        $il = $method.Body.GetILProcessor()
        if ($method.ReturnType.FullName -ne 'System.Void') {
            if ($method.ReturnType.IsValueType) { throw "Unsupported callback return type: $($method.FullName)" }
            $il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldnull))
        }
        $il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
        $count++
    }
}
$assembly.Write((Join-Path $testRoot 'Aviassembly_Data\Managed\Assembly-CSharp.dll'))
$assembly.Dispose()
Set-Content -LiteralPath (Join-Path $testRoot 'FLAP_TEST_HOST_ONLY') -Value 'Disposable wing upgrade fixture'
$plugins = Join-Path $testRoot 'BepInEx\plugins'
New-Item -ItemType Directory -Force -Path $plugins | Out-Null
$release = Join-Path $projectRoot 'AviassemblyFlaps.dll'
Copy-Item -LiteralPath $release -Destination $plugins -Force
$compiler = Get-ChildItem 'C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll' | Sort-Object FullName -Descending | Select-Object -First 1
$arguments = @('/nologo','/target:library','/nostdlib+','/langversion:latest',('/out:' + (Join-Path $plugins 'WingRuntimeFixture.dll')),('/reference:' + $release))
foreach ($lib in Get-ChildItem (Join-Path $GameDir 'Aviassembly_Data\Managed') -Filter '*.dll') { $arguments += '/reference:' + $lib.FullName }
foreach ($name in @('BepInEx.dll','0Harmony.dll')) { $arguments += '/reference:' + (Join-Path $GameDir ('BepInEx\core\' + $name)) }
$arguments += (Join-Path $PSScriptRoot 'WingRuntimeFixture.cs')
& dotnet $compiler.FullName @arguments
if ($LASTEXITCODE -ne 0) { throw 'Runtime fixture did not compile.' }
$report = Join-Path $testRoot 'flap-results.txt'
if (Test-Path $report) { Remove-Item -LiteralPath $report }
$player=Start-Process -FilePath (Join-Path $testRoot 'Aviassembly.exe') -WorkingDirectory $testRoot -WindowStyle Hidden -ArgumentList ('-batchmode -nographics -logFile "' + (Join-Path $testRoot 'unity.log') + '"') -PassThru
if (!$player.WaitForExit(45000)) { $player.Kill();throw 'Isolated player timed out.' }
if (!(Test-Path $report)) { throw 'No runtime report. See unity.log.' }
Get-Content -LiteralPath $report
if ($player.ExitCode -ne 0) { throw 'Runtime fixture failed.' }
