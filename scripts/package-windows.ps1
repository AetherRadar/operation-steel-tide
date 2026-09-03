param(
    [string]$Godot = $env:GODOT_MONO,
    [string]$Version = "1.4.1",
    [string]$OutputRoot = "dist"
)

$ErrorActionPreference = "Stop"
$env:NuGetAudit = "false"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Godot)) {
    $Godot = Join-Path $env:USERPROFILE "Downloads\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe"
}
if (-not (Test-Path -LiteralPath $Godot)) {
    throw "Godot 4.6.3 Mono console executable not found. Set GODOT_MONO or pass -Godot."
}

$projectFile = Join-Path $projectRoot "project.godot"
$projectVersionMatch = Select-String -LiteralPath $projectFile -Pattern '^config/version="([^"]+)"$'
if ($projectVersionMatch.Matches.Count -ne 1) {
    throw "Unable to read a single config/version from project.godot."
}
$projectVersion = $projectVersionMatch.Matches[0].Groups[1].Value
if ($Version -ne $projectVersion) {
    throw "Package version '$Version' does not match project version '$projectVersion'."
}

$releaseName = "OperationSteelTide-$Version-windows-x64"
$outputRootPath = Join-Path $projectRoot $OutputRoot
$buildRootPath = Join-Path $projectRoot "build"
$stagePath = Join-Path $outputRootPath $releaseName
$zipPath = Join-Path $outputRootPath "$releaseName.zip"
$exportPath = Join-Path $stagePath "OperationSteelTide.exe"

foreach ($generatedRoot in @($outputRootPath, $buildRootPath)) {
    New-Item -ItemType Directory -Force -Path $generatedRoot | Out-Null
    Set-Content -LiteralPath (Join-Path $generatedRoot ".gdignore") -Value "" -Encoding ascii
}
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

$requiredReleasePaths = @(
    $exportPath,
    (Join-Path $stagePath "OperationSteelTide.pck"),
    (Join-Path $stagePath "data_OperationSteelTide_windows_x86_64"),
    (Join-Path $stagePath "steel-tide-server.exe"),
    (Join-Path $stagePath "PLAY.bat"),
    (Join-Path $stagePath "README.txt"),
    (Join-Path $stagePath "ONLINE_PLAY.md"),
    (Join-Path $stagePath "LICENSE.txt")
)
foreach ($requiredPath in $requiredReleasePaths) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Release output is incomplete: $requiredPath"
    }
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -LiteralPath $stagePath -DestinationPath $zipPath -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
Set-Content -LiteralPath "$zipPath.sha256" -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zipPath))" -Encoding ascii
Write-Host "WINDOWS_PACKAGE path=$zipPath sha256=$($hash.Hash.ToLowerInvariant())"
