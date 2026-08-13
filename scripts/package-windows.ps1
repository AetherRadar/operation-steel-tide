param(
    [string]$Godot = $env:GODOT_MONO,
    [string]$Version = "1.2.0",
    [string]$OutputRoot = "dist"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Godot)) {
    $Godot = Join-Path $env:USERPROFILE "Downloads\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe"
}
if (-not (Test-Path -LiteralPath $Godot)) {
    throw "Godot 4.6.3 Mono console executable not found. Set GODOT_MONO or pass -Godot."
}

$releaseName = "OperationSteelTide-$Version-windows-x64"
$outputRootPath = Join-Path $projectRoot $OutputRoot
$stagePath = Join-Path $outputRootPath $releaseName
$zipPath = Join-Path $outputRootPath "$releaseName.zip"
$exportPath = Join-Path $stagePath "OperationSteelTide.exe"

New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null
if (Test-Path -LiteralPath $stagePath) {
    Remove-Item -LiteralPath $stagePath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stagePath | Out-Null

Push-Location $projectRoot
try {
    dotnet build OperationSteelTide.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

    & $Godot --headless --path . --export-release "Windows Desktop" $exportPath
    if ($LASTEXITCODE -ne 0) { throw "Godot export failed." }

    go -C backend build -trimpath -ldflags "-s -w" -o (Join-Path $stagePath "steel-tide-server.exe") ./cmd/server
    if ($LASTEXITCODE -ne 0) { throw "Go backend build failed." }
}
finally {
    Pop-Location
}

Copy-Item -LiteralPath (Join-Path $projectRoot "scripts\PLAY_RELEASE.bat") -Destination (Join-Path $stagePath "PLAY.bat")
Copy-Item -LiteralPath (Join-Path $projectRoot "scripts\RELEASE_README.txt") -Destination (Join-Path $stagePath "README.txt")
Copy-Item -LiteralPath (Join-Path $projectRoot "ONLINE_PLAY.md") -Destination (Join-Path $stagePath "ONLINE_PLAY.md")
Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination (Join-Path $stagePath "LICENSE.txt")
New-Item -ItemType Directory -Force -Path (Join-Path $stagePath "backend\data") | Out-Null

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -LiteralPath $stagePath -DestinationPath $zipPath -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
Set-Content -LiteralPath "$zipPath.sha256" -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zipPath))" -Encoding ascii
Write-Host "WINDOWS_PACKAGE path=$zipPath sha256=$($hash.Hash.ToLowerInvariant())"
