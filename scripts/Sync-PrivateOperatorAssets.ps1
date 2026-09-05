[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectRoot,
    [string]$PrivateRoot = $env:STEEL_TIDE_PRIVATE_ASSET_ROOT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$destination = Join-Path $ProjectRoot 'assets\models\hy3d_operators'
$requiredAssets = @('viper.glb', 'heron.glb', 'lynx.glb', 'magpie.glb', 'jackal.glb')

if ([string]::IsNullOrWhiteSpace($PrivateRoot)) {
    $PrivateRoot = Join-Path $env:LOCALAPPDATA 'OperationSteelTide\private-assets'
}

$source = Join-Path $PrivateRoot 'hy3d_operators'
$availableSourceAssets = @($requiredAssets |
    Where-Object { Test-Path -LiteralPath (Join-Path $source $_) -PathType Leaf })
$projectAssetsPresent = @($requiredAssets |
    Where-Object { Test-Path -LiteralPath (Join-Path $destination $_) -PathType Leaf }).Count

if ($availableSourceAssets.Count -eq 0) {
    if ($projectAssetsPresent -eq $requiredAssets.Count) {
        Write-Host 'Private HY3D operator assets already present in the project.'
    }
    else {
        Write-Warning (
            "Private HY3D operator assets were not found at '$source'. " +
            'Set STEEL_TIDE_PRIVATE_ASSET_ROOT or place the approved local assets there; ' +
            'the game will use its documented fallback operators until then.')
    }
    exit 0
}

$null = New-Item -ItemType Directory -Path $destination -Force
$copied = 0
foreach ($assetName in $availableSourceAssets) {
    $sourcePath = Join-Path $source $assetName
    $destinationPath = Join-Path $destination $assetName
    $needsCopy = -not (Test-Path -LiteralPath $destinationPath -PathType Leaf)
    if (-not $needsCopy) {
        $needsCopy = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
    }
    if ($needsCopy) {
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force
        $copied++
    }
}

Write-Host "Private HY3D operator assets ready (updated=$copied, total=$($availableSourceAssets.Count))."
exit 0
