[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Before,

    [Parameter(Mandatory)]
    [string] $After
)

$ErrorActionPreference = 'Stop'

function Read-Capture([string] $Path) {
    $capture = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($capture.schema -notin 1, 2 -or $null -eq $capture.chunkOrigin -or
        $null -eq $capture.decorations -or $capture.decorationCount -ne @($capture.decorations).Count) {
        throw "Invalid or unsupported scenery capture: $Path"
    }
    $capture
}

function Compare-Samples([string] $Category, [object[]] $Fields) {
    $oldRows = @(foreach ($sample in $old.decorations) {
        $sample | Select-Object -Property $Fields | ConvertTo-Json -Compress -Depth 4
    })
    $newRows = @(foreach ($sample in $new.decorations) {
        $sample | Select-Object -Property $Fields | ConvertTo-Json -Compress -Depth 4
    })

    # Include duplicate rows so changes in repeated placements are visible.
    $changes = @(Compare-Object -ReferenceObject $oldRows -DifferenceObject $newRows -CaseSensitive)
    [pscustomobject]@{
        Category = $Category
        Removed = @($changes | Where-Object SideIndicator -eq '<=' | ForEach-Object { $_.InputObject | ConvertFrom-Json })
        Added = @($changes | Where-Object SideIndicator -eq '=>' | ForEach-Object { $_.InputObject | ConvertFrom-Json })
    }
}

$old = Read-Capture $Before
$new = Read-Capture $After
if ($old.chunkOrigin.x -ne $new.chunkOrigin.x -or $old.chunkOrigin.y -ne $new.chunkOrigin.y -or
    $old.chunkOrigin.z -ne $new.chunkOrigin.z) {
    throw 'Select captures from the same chunk origin.'
}
if (!$old.hasWorldAsset -or !$new.hasWorldAsset -or $old.worldAsset -cne $new.worldAsset -or
    $old.worldVersionStartingFrom -ne $new.worldVersionStartingFrom) {
    throw 'Select captures with the same known world asset and world version.'
}
if ($old.gameVersion -cne $new.gameVersion) {
    throw 'Select captures from the same Aviassembly version.'
}

[pscustomobject]@{
    World = $old.worldAsset
    ChunkOrigin = $old.chunkOrigin
    BeforeCount = $old.decorationCount
    AfterCount = $new.decorationCount
    Changes = @(
        Compare-Samples 'Positions' @('position')
        Compare-Samples 'Models' @('position', 'decorationType', 'mesh', 'distantMesh')
        Compare-Samples 'Transforms' @('position', 'rotation', 'scale')
        if ($old.schema -eq 2 -and $new.schema -eq 2) {
            Compare-Samples 'Collider bounds' @('position', 'hasCollider', 'colliderActive', 'colliderMin', 'colliderMax')
        }
    )
} | ConvertTo-Json -Depth 8
