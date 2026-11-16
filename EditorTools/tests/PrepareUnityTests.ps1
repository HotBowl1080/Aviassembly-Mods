param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Aviassembly')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
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
# The fixture uses the original BuildingCamera implementation. Suppress unrelated game
# startup and callbacks so no Steam session, save system, flight, or menu logic runs.
$callbacks = @('Awake','Start','Update','LateUpdate','FixedUpdate','OnEnable','OnDisable','OnDestroy','OnApplicationQuit','OnApplicationFocus','OnApplicationPause','OnGUI','OnLevelWasLoaded')
$count = 0
foreach ($type in $assembly.MainModule.Types) {
    if ($type.Name -eq 'BuildingCamera' -or $type.Name -eq 'Singleton`1') { continue }
    foreach ($method in $type.Methods) {
        $runtimeInit = @($method.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'RuntimeInitializeOnLoadMethodAttribute' }).Count -gt 0
        if (!$method.HasBody -or ($method.Name -notin $callbacks -and !$runtimeInit)) { continue }
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
Set-Content -LiteralPath (Join-Path $testRoot 'FOCUS_TEST_HOST_ONLY') -Value 'Disposable automated camera fixture. Not for gameplay.'
New-Item -ItemType Directory -Path (Join-Path $testRoot 'BepInEx\plugins') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'bin\Release\netstandard2.0\Aviassembly.EditorTools.dll') -Destination (Join-Path $testRoot 'BepInEx\plugins') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\netstandard2.0\Aviassembly.FocusRuntimeTests.dll') -Destination (Join-Path $testRoot 'BepInEx\plugins') -Force
"Prepared isolated Unity camera fixture; $count unrelated callbacks suppressed"
$testRoot
