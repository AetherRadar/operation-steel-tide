param(
    [string]$Godot = $env:GODOT_MONO,
    [string]$Version = "1.4.1",
    [string]$OutputRoot = "dist"
)

$ErrorActionPreference = "Stop"
$env:NuGetAudit = "false"
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
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

$releaseName = "OperationSteelTide-$Version-macos-universal"
$outputRootPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputRoot))
$buildRootPath = [IO.Path]::GetFullPath((Join-Path $projectRoot "build"))
$zipPath = Join-Path $outputRootPath "$releaseName.zip"
if (-not $outputRootPath.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Output root must remain inside the project checkout."
}

foreach ($generatedRoot in @($outputRootPath, $buildRootPath)) {
    New-Item -ItemType Directory -Force -Path $generatedRoot | Out-Null
    Set-Content -LiteralPath (Join-Path $generatedRoot ".gdignore") -Value "" -Encoding ascii
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Push-Location $projectRoot
try {
    dotnet build OperationSteelTide.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

    & $Godot --headless --path . --export-release "macOS" $zipPath
    if ($LASTEXITCODE -ne 0) { throw "Godot export failed." }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Godot did not create the macOS archive: $zipPath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName })
    $appRoot = "Operation Steel Tide.app"
    $executablePath = "$appRoot/Contents/MacOS/Operation Steel Tide"
    $requiredEntries = @(
        "$appRoot/Contents/Info.plist",
        $executablePath,
        "$appRoot/Contents/Resources/Operation Steel Tide.pck"
    )
    foreach ($entry in $requiredEntries) {
        if ($entries -notcontains $entry) {
            throw "macOS archive is incomplete: $entry"
        }
    }

    $managedAssemblies = @($entries | Where-Object { $_ -like "*/OperationSteelTide.dll" })
    $coreRuntimes = @($entries | Where-Object { $_ -like "*/libcoreclr.dylib" })
    if ($managedAssemblies.Count -ne 2 -or $coreRuntimes.Count -ne 2) {
        throw "macOS archive must contain managed assemblies and .NET runtimes for both architectures."
    }

    $executable = $archive.GetEntry($executablePath)
    $stream = $executable.Open()
    try {
        $header = New-Object byte[] 8
        if ($stream.Read($header, 0, $header.Length) -ne $header.Length) {
            throw "Unable to read the macOS executable header."
        }
    }
    finally {
        $stream.Dispose()
    }
    $magic = ($header[0..3] | ForEach-Object { $_.ToString("X2") }) -join ""
    $architectureCount = [BitConverter]::ToUInt32(@($header[7], $header[6], $header[5], $header[4]), 0)
    if ($magic -notin @("CAFEBABE", "CAFEBABF") -or $architectureCount -ne 2) {
        throw "macOS executable is not a two-architecture universal Mach-O binary."
    }
    $entryCount = $entries.Count
}
finally {
    $archive.Dispose()
}

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
Set-Content -LiteralPath "$zipPath.sha256" -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zipPath))" -Encoding ascii
Write-Host "MACOS_PACKAGE path=$zipPath entries=$entryCount architectures=$architectureCount sha256=$($hash.Hash.ToLowerInvariant())"
