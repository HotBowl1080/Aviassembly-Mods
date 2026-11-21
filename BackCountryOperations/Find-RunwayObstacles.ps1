[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]] $Capture,

    [Parameter(Mandatory)]
    [string] $Runway
)

$ErrorActionPreference = 'Stop'
$site = Get-Content -LiteralPath $Runway -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($site.worldAsset) -or $null -eq $site.worldVersionStartingFrom) {
    throw 'The runway file must identify the world asset and its starting save version.'
}

$dimensions = @($site.thresholdA.x, $site.thresholdA.z, $site.thresholdB.x, $site.thresholdB.z, $site.width)
foreach ($value in $dimensions) {
    if ($null -eq $value -or $value -is [string] -or $value -is [bool] -or
        [double]::IsNaN($value) -or [double]::IsInfinity($value)) {
        throw 'Runway thresholds and width must be finite numbers.'
    }
}

$dx = [double]$site.thresholdB.x - $site.thresholdA.x
$dz = [double]$site.thresholdB.z - $site.thresholdA.z
$length = [Math]::Sqrt($dx * $dx + $dz * $dz)
if ($length -le 0 -or [double]::IsInfinity($length) -or $site.width -le 0) {
    throw 'Runway needs distinct thresholds and a positive width.'
}

$axisX = $dx / $length
$axisZ = $dz / $length
$halfLength = $length / 2
$halfWidth = [double]$site.width / 2
$centerX = [double]$site.thresholdA.x + $dx / 2
$centerZ = [double]$site.thresholdA.z + $dz / 2
$worldHalfX = [Math]::Abs($axisX) * $halfLength + [Math]::Abs($axisZ) * $halfWidth
$worldHalfZ = [Math]::Abs($axisZ) * $halfLength + [Math]::Abs($axisX) * $halfWidth

function Test-Overlap($Obstacle) {
    $min = $Obstacle.colliderMin
    $max = $Obstacle.colliderMax
    $halfX = ($max.x - $min.x) / 2
    $halfZ = ($max.z - $min.z) / 2
    $offsetX = $min.x + $halfX - $centerX
    $offsetZ = $min.z + $halfZ - $centerZ

    # Test the axes of both rectangles. Touching the edge counts as overlap.
    if ([Math]::Abs($offsetX) -gt ($worldHalfX + $halfX) -or
        [Math]::Abs($offsetZ) -gt ($worldHalfZ + $halfZ)) {
        return $false
    }

    $along = $offsetX * $axisX + $offsetZ * $axisZ
    $across = -$offsetX * $axisZ + $offsetZ * $axisX
    $boxAlong = $halfX * [Math]::Abs($axisX) + $halfZ * [Math]::Abs($axisZ)
    $boxAcross = $halfX * [Math]::Abs($axisZ) + $halfZ * [Math]::Abs($axisX)
    return [Math]::Abs($along) -le ($halfLength + $boxAlong) -and
        [Math]::Abs($across) -le ($halfWidth + $boxAcross)
}

$results = @(foreach ($path in $Capture) {
    $snapshot = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($snapshot.schema -ne 2) {
        throw "Runway screening requires a schema 2 capture from probe 0.2.2 or later: $path"
    }
    if (!$snapshot.hasWorldAsset -or
        $snapshot.worldAsset -cne $site.worldAsset -or
        $snapshot.worldVersionStartingFrom -ne $site.worldVersionStartingFrom) {
        throw "Capture does not match the runway world and save version: $path"
    }
    if ($null -eq $snapshot.decorations -or $snapshot.decorationCount -ne @($snapshot.decorations).Count) {
        throw "Invalid decoration count in capture: $path"
    }

    $obstacles = @(foreach ($decoration in $snapshot.decorations) {
        if ($decoration.hasCollider -and $decoration.colliderActive -and (Test-Overlap $decoration)) {
            $decoration | Select-Object decorationType, position, mesh, colliderMin, colliderMax
        }
    })

    [pscustomobject]@{
        Capture = $path
        ChunkOrigin = $snapshot.chunkOrigin
        OverlapCount = $obstacles.Count
        Obstacles = $obstacles
    }
})

ConvertTo-Json -InputObject $results -Depth 6
