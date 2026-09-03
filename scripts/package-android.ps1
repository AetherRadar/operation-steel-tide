param(
    [string]$Godot = $env:GODOT_MONO,
    [string]$Version = "1.4.2",
    [string]$OutputRoot = "dist",
    [ValidateSet("Debug", "Release")]
    [string]$Mode = "Debug",
    [string]$AndroidSdk = "",
    [string]$JavaHome = "",
    [string]$DebugKeystore = ""
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

if ([string]::IsNullOrWhiteSpace($AndroidSdk)) {
    $AndroidSdk = if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
        $env:ANDROID_HOME
    } elseif (-not [string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT)) {
        $env:ANDROID_SDK_ROOT
    } else {
        Join-Path $env:LOCALAPPDATA "Android\Sdk"
    }
}
$AndroidSdk = [IO.Path]::GetFullPath($AndroidSdk)
if (-not (Test-Path -LiteralPath $AndroidSdk)) {
    throw "Android SDK not found at '$AndroidSdk'. Install the Godot 4.6 Android SDK requirements or pass -AndroidSdk."
}
$requiredSdkPaths = @(
    (Join-Path $AndroidSdk "platform-tools"),
    (Join-Path $AndroidSdk "platforms\android-36"),
    (Join-Path $AndroidSdk "build-tools\36.1.0")
)
foreach ($requiredSdkPath in $requiredSdkPaths) {
    if (-not (Test-Path -LiteralPath $requiredSdkPath)) {
        throw "Android SDK component is missing: $requiredSdkPath"
    }
}
$env:ANDROID_HOME = $AndroidSdk
$env:ANDROID_SDK_ROOT = $AndroidSdk

$androidBuildRoot = Join-Path $projectRoot "android\build"
$androidBuildGradle = Join-Path $androidBuildRoot "build.gradle"
if (-not (Test-Path -LiteralPath $androidBuildGradle)) {
    $templateZip = Join-Path $env:APPDATA "Godot\export_templates\4.6.3.stable.mono\android_source.zip"
    if (-not (Test-Path -LiteralPath $templateZip)) {
        throw "Godot Android Gradle template not found at '$templateZip'. Install the 4.6.3 Mono export templates first."
    }
    New-Item -ItemType Directory -Force -Path $androidBuildRoot | Out-Null
    Expand-Archive -LiteralPath $templateZip -DestinationPath $androidBuildRoot -Force
}

if ([string]::IsNullOrWhiteSpace($JavaHome)) {
    $JavaHome = $env:JAVA_HOME
}
if ([string]::IsNullOrWhiteSpace($JavaHome)) {
    $JavaHome = "C:\Program Files\Eclipse Adoptium\jdk-21.0.12.8-hotspot"
}
$JavaHome = [IO.Path]::GetFullPath($JavaHome)
$javaExecutable = Join-Path $JavaHome "bin\java.exe"
$keytool = Join-Path $JavaHome "bin\keytool.exe"
if (-not (Test-Path -LiteralPath $javaExecutable) -or -not (Test-Path -LiteralPath $keytool)) {
    throw "A JDK with java.exe and keytool.exe is required at '$JavaHome'. Pass -JavaHome."
}
$env:JAVA_HOME = $JavaHome

$keystorePassword = "android"
$keystoreUser = "androiddebugkey"
if ($Mode -eq "Debug") {
    if ([string]::IsNullOrWhiteSpace($DebugKeystore)) {
        $DebugKeystore = Join-Path $env:APPDATA "Godot\keystores\debug.keystore"
    }
    $DebugKeystore = [IO.Path]::GetFullPath($DebugKeystore)
    $keystoreDirectory = Split-Path -Parent $DebugKeystore
    New-Item -ItemType Directory -Force -Path $keystoreDirectory | Out-Null
    if (-not (Test-Path -LiteralPath $DebugKeystore)) {
        & $keytool -genkeypair -v -keystore $DebugKeystore -alias $keystoreUser -storepass $keystorePassword -keypass $keystorePassword -dname "CN=Android Debug,O=Android,C=US" -keyalg RSA -keysize 2048 -validity 10000
        if ($LASTEXITCODE -ne 0) { throw "Unable to create the Android debug keystore." }
    }
    $env:GODOT_ANDROID_KEYSTORE_DEBUG_PATH = $DebugKeystore
    $env:GODOT_ANDROID_KEYSTORE_DEBUG_USER = $keystoreUser
    $env:GODOT_ANDROID_KEYSTORE_DEBUG_PASSWORD = $keystorePassword
} else {
    foreach ($releaseVariable in @("GODOT_ANDROID_KEYSTORE_RELEASE_PATH", "GODOT_ANDROID_KEYSTORE_RELEASE_USER", "GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD")) {
        if ([string]::IsNullOrWhiteSpace((Get-Item -Path "Env:$releaseVariable" -ErrorAction SilentlyContinue).Value)) {
            throw "Release Android export requires $releaseVariable to be set."
        }
    }
    if (-not (Test-Path -LiteralPath $env:GODOT_ANDROID_KEYSTORE_RELEASE_PATH)) {
        throw "Release Android keystore not found at '$env:GODOT_ANDROID_KEYSTORE_RELEASE_PATH'."
    }
}

$releaseName = "OperationSteelTide-$Version-android-arm64-$($Mode.ToLowerInvariant())"
$outputRootPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputRoot))
$buildRootPath = [IO.Path]::GetFullPath((Join-Path $projectRoot "build"))
$buildAndroidPath = Join-Path $buildRootPath "android"
$buildOutputPath = Join-Path $buildAndroidPath "$releaseName.apk"
$distOutputPath = Join-Path $outputRootPath "$releaseName.apk"
if (-not $outputRootPath.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Output root must remain inside the project checkout."
}
foreach ($generatedRoot in @($outputRootPath, $buildRootPath, $buildAndroidPath)) {
    New-Item -ItemType Directory -Force -Path $generatedRoot | Out-Null
    Set-Content -LiteralPath (Join-Path $generatedRoot ".gdignore") -Value "" -Encoding ascii
}
foreach ($generatedFile in @($buildOutputPath, $distOutputPath, "$distOutputPath.sha256")) {
    if (Test-Path -LiteralPath $generatedFile) {
        Remove-Item -LiteralPath $generatedFile -Force
    }
}

Push-Location $projectRoot
try {
    dotnet build OperationSteelTide.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

    $exportVerb = if ($Mode -eq "Debug") { "--export-debug" } else { "--export-release" }
    $exportLogPath = Join-Path $buildAndroidPath "$releaseName.godot.log"
    $exportErrorLogPath = Join-Path $buildAndroidPath "$releaseName.godot.error.log"
    $godotArguments = @("--headless", "--path", $projectRoot, $exportVerb, "Android", $buildOutputPath)
    $godotProcess = Start-Process -FilePath $Godot -ArgumentList $godotArguments -WorkingDirectory $projectRoot -NoNewWindow -PassThru -RedirectStandardOutput $exportLogPath -RedirectStandardError $exportErrorLogPath
    $exportDeadline = (Get-Date).AddMinutes(10)
    $artifactGraceDeadline = $null
    while (-not $godotProcess.HasExited -and (Get-Date) -lt $exportDeadline) {
        if (Test-Path -LiteralPath $buildOutputPath) {
            if ($null -eq $artifactGraceDeadline) {
                $artifactGraceDeadline = (Get-Date).AddSeconds(30)
            } elseif ((Get-Date) -ge $artifactGraceDeadline) {
                break
            }
        }
        Start-Sleep -Seconds 1
    }
    if (-not $godotProcess.HasExited) {
        if (Test-Path -LiteralPath $buildOutputPath) {
            Write-Warning "Godot did not exit after creating the Android package; stopping the exporter process and continuing with the verified APK."
            Stop-Process -Id $godotProcess.Id -Force
        } else {
            Stop-Process -Id $godotProcess.Id -Force
            throw "Godot Android export timed out without creating an APK."
        }
    }
    if ($godotProcess.ExitCode -ne 0 -and -not (Test-Path -LiteralPath $buildOutputPath)) {
        throw "Godot Android export failed with exit code $($godotProcess.ExitCode)."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $buildOutputPath)) {
    throw "Godot did not create the Android package: $buildOutputPath"
}
Copy-Item -LiteralPath $buildOutputPath -Destination $distOutputPath
$hash = Get-FileHash -LiteralPath $distOutputPath -Algorithm SHA256
Set-Content -LiteralPath "$distOutputPath.sha256" -Value "$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($distOutputPath))" -Encoding ascii
Write-Host "ANDROID_PACKAGE path=$distOutputPath mode=$Mode sha256=$($hash.Hash.ToLowerInvariant())"
