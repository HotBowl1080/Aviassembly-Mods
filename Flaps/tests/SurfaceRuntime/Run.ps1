param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Aviassembly')
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
& (Join-Path $project 'build.ps1') -GameDir $GameDir
$hostPath=[IO.Path]::GetFullPath((Join-Path $project '.work\TestHost'))
New-Item -ItemType Directory -Path $hostPath -Force | Out-Null
foreach($n in @('Aviassembly.exe','UnityPlayer.dll','winhttp.dll','doorstop_config.ini')){Copy-Item -LiteralPath (Join-Path $GameDir $n) -Destination $hostPath -Force}
foreach($n in @('Aviassembly_Data','MonoBleedingEdge')){if(!(Test-Path -LiteralPath (Join-Path $hostPath $n))){Copy-Item -LiteralPath (Join-Path $GameDir $n) -Destination $hostPath -Recurse}}
New-Item -ItemType Directory -Path (Join-Path $hostPath 'BepInEx\plugins') -Force | Out-Null
if(!(Test-Path -LiteralPath (Join-Path $hostPath 'BepInEx\core'))){Copy-Item -LiteralPath (Join-Path $GameDir 'BepInEx\core') -Destination (Join-Path $hostPath 'BepInEx') -Recurse}
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Aviassembly_Data\Managed\Assembly-CSharp.dll'))
$callbacks=@('Awake','Start','Update','LateUpdate','FixedUpdate','OnEnable','OnDisable','OnDestroy','OnApplicationQuit','OnApplicationFocus','OnApplicationPause','OnGUI','OnLevelWasLoaded')
# Retain actual flight/render patch targets. Scene behaviours are disabled before Start/FixedUpdate.
$retain=@('PlaneContainer.FixedUpdate','PlaneController.FixedUpdate','SpeedDial.Update','HeightDial.Update','CloudSpawner.Start','CloudSpawner.Update','CustomFog.Update','AirTurbine.Update','Airbreak.Update','WingVisual.Update','WingVisual.Start')
foreach($type in $assembly.MainModule.Types){
 if($type.Name -eq 'Singleton`1'){continue}
 foreach($method in $type.Methods){
  $runtimeInit=@($method.CustomAttributes | Where-Object {$_.AttributeType.Name -eq 'RuntimeInitializeOnLoadMethodAttribute'}).Count -gt 0
  if(!$method.HasBody -or ($method.Name -notin $callbacks -and !$runtimeInit) -or (($type.Name+'.'+$method.Name) -in $retain)){continue}
  $method.Body.Instructions.Clear(); $method.Body.ExceptionHandlers.Clear(); $method.Body.Variables.Clear()
  $il=$method.Body.GetILProcessor()
  if($method.ReturnType.FullName -ne 'System.Void'){
   if($method.ReturnType.IsValueType){throw 'Unexpected test callback type'}
   $il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldnull))
  }
  $il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
 }
}
$assembly.Write((Join-Path $hostPath 'Aviassembly_Data\Managed\Assembly-CSharp.dll')); $assembly.Dispose()

Set-Content -LiteralPath (Join-Path $hostPath 'PAID_FLAPS_TEST_ONLY') -Value 'Private fixture, never install.'
$plugin = Join-Path $project 'AviassemblyFlaps.dll'
$testPlugin = Join-Path $hostPath 'BepInEx\plugins\Flaps.PaidUpgradeRuntimeTests.dll'
Copy-Item -LiteralPath $plugin -Destination (Join-Path $hostPath 'BepInEx\plugins') -Force
$compiler = Get-ChildItem 'C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll' | Sort-Object FullName -Descending | Select-Object -First 1
$arguments = @('/nologo','/target:library','/nostdlib+','/langversion:latest',('/out:' + $testPlugin),('/reference:' + $plugin))
foreach ($assembly in Get-ChildItem -LiteralPath (Join-Path $GameDir 'Aviassembly_Data\Managed') -Filter '*.dll') { $arguments += '/reference:' + $assembly.FullName }
foreach ($name in @('BepInEx.dll','0Harmony.dll')) { $arguments += '/reference:' + (Join-Path $GameDir ('BepInEx\core\' + $name)) }
$arguments += (Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs').FullName
& dotnet $compiler.FullName @arguments
if ($LASTEXITCODE -ne 0) { throw 'Runtime test compilation failed.' }
$report = Join-Path $hostPath 'paid-flaps-results.txt'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$log = Join-Path $hostPath 'paid-flaps-unity.log'
$process = Start-Process -FilePath (Join-Path $hostPath 'Aviassembly.exe') -WorkingDirectory $hostPath -WindowStyle Hidden -PassThru -ArgumentList ('-batchmode -nographics -logFile "' + $log + '"')
if (!$process.WaitForExit(45000)) {
    if (!$process.HasExited) { $process.Kill() }
    throw "Isolated runtime test timed out. See $log"
}
if (!(Test-Path -LiteralPath $report)) { throw "No runtime test report. See $log" }
Get-Content -LiteralPath $report -Tail 18
if ($process.ExitCode -ne 0 -or (Select-String -LiteralPath $log -Pattern 'Exception:|\[Error\s*:' -Quiet)) { throw "Runtime tests failed. See $report and $log" }
