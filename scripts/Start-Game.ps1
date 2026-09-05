Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$forwardedGameArguments = [string[]]$args

function Get-CanonicalProjectPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][byte[]]$Bytes)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString($sha256.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-ProjectPathHash {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)

    $canonicalRoot = (Get-CanonicalProjectPath -Path $ProjectRoot).ToUpperInvariant()
    return Get-Sha256Hex -Bytes ([System.Text.Encoding]::UTF8.GetBytes($canonicalRoot))
}

function Get-BackendSourceFingerprintFromEntries {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Entries)

    $records = @{}
    foreach ($entry in $Entries) {
        $relativePath = ([string]$entry.Path).Replace('\', '/')
        $contentHash = ([string]$entry.ContentHash).ToLowerInvariant()
        if ([string]::IsNullOrWhiteSpace($relativePath) -or
            $contentHash -notmatch '^[0-9a-f]{64}$') {
            throw "Invalid backend source fingerprint entry '$relativePath'."
        }
        if ($records.ContainsKey($relativePath)) {
            throw "Duplicate backend source fingerprint path '$relativePath'."
        }
        $records.Add($relativePath, $contentHash)
    }

    $orderedPaths = [string[]]@($records.Keys)
    [System.Array]::Sort($orderedPaths, [System.StringComparer]::Ordinal)
    $manifestLines = foreach ($relativePath in $orderedPaths) {
        "$relativePath`0$($records[$relativePath])"
    }
    $manifest = ($manifestLines -join "`n") + "`n"
    return Get-Sha256Hex -Bytes ([System.Text.Encoding]::UTF8.GetBytes($manifest))
}

function Get-BackendSourceFingerprint {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)

    $backendRoot = Get-CanonicalProjectPath -Path (Join-Path $ProjectRoot 'backend')
    $backendPrefix = $backendRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $sourceFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
    foreach ($sourceFile in (Get-ChildItem -LiteralPath $backendRoot -Filter '*.go' -File -Recurse)) {
        $sourceFiles.Add($sourceFile)
    }
    foreach ($moduleFileName in @('go.mod', 'go.sum')) {
        $modulePath = Join-Path $backendRoot $moduleFileName
        if (Test-Path -LiteralPath $modulePath -PathType Leaf) {
            $sourceFiles.Add((Get-Item -LiteralPath $modulePath))
        }
    }

    $entries = foreach ($sourceFile in $sourceFiles) {
        $fullPath = [System.IO.Path]::GetFullPath($sourceFile.FullName)
        if (-not $fullPath.StartsWith($backendPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Backend source escaped its expected root: '$fullPath'."
        }
        [PSCustomObject]@{
            Path = 'backend/' + $fullPath.Substring($backendPrefix.Length).Replace('\', '/')
            ContentHash = Get-Sha256Hex -Bytes ([System.IO.File]::ReadAllBytes($fullPath))
        }
    }

    return Get-BackendSourceFingerprintFromEntries -Entries @($entries)
}

function Get-BackendInstanceId {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)

    $pathHash = Get-ProjectPathHash -ProjectRoot $ProjectRoot
    $sourceFingerprint = Get-BackendSourceFingerprint -ProjectRoot $ProjectRoot
    return New-BackendInstanceId -ProjectPathHash $pathHash -SourceFingerprint $sourceFingerprint
}

function New-BackendInstanceId {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPathHash,
        [Parameter(Mandatory = $true)][string]$SourceFingerprint
    )

    if ($ProjectPathHash -notmatch '^[0-9a-f]{64}$' -or
        $SourceFingerprint -notmatch '^[0-9a-f]{64}$') {
        throw 'Backend instance ID components must be lowercase SHA-256 hashes.'
    }
    return "$ProjectPathHash-$SourceFingerprint"
}

function Get-ProjectMutexName {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][ValidateSet('Preparation', 'Runtime')][string]$Scope
    )

    $pathHash = Get-ProjectPathHash -ProjectRoot $ProjectRoot
    return "Local\OperationSteelTide-$Scope-$($pathHash.Substring(0, 24))"
}

function Get-GameRuntimePolicy {
    param(
        [Parameter(Mandatory = $true)][bool]$OwnsRuntimeMutex,
        [ValidateRange(0, [int]::MaxValue)][int]$RunningProjectInstanceCount
    )

    $parallel = -not $OwnsRuntimeMutex -or $RunningProjectInstanceCount -gt 0
    return [PSCustomObject]@{
        Parallel = $parallel
        BackendAllowed = -not $parallel
    }
}

function Test-IsFullyQualifiedWindowsPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return $Path -match '^(?i:[a-z]:[\\/])' -or
        $Path.StartsWith('\\') -or
        $Path.StartsWith('//')
}

function Test-GodotRuntimeCommandLine {
    param([Parameter(Mandatory = $true)][string]$CommandLine)

    return -not [System.Text.RegularExpressions.Regex]::IsMatch(
        $CommandLine,
        '(?i)(?:^|\s)(?:--editor|-e|--project-manager|-p|--import)(?=\s|$)')
}

function Get-RunningGodotProcessesForProject {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [AllowEmptyCollection()][AllowNull()][object[]]$Processes,
        [switch]$RuntimeOnly
    )

    $canonicalRoot = Get-CanonicalProjectPath -Path $ProjectRoot
    $matches = [System.Collections.Generic.List[object]]::new()
    $godotProcesses = if ($PSBoundParameters.ContainsKey('Processes')) {
        @($Processes)
    }
    else {
        @(Get-CimInstance -ClassName Win32_Process -Filter "Name LIKE 'Godot%.exe'")
    }
    foreach ($process in $godotProcesses) {
        $commandLine = [string]$process.CommandLine
        if ([string]::IsNullOrWhiteSpace($commandLine)) {
            continue
        }
        if ($RuntimeOnly -and -not (Test-GodotRuntimeCommandLine -CommandLine $commandLine)) {
            continue
        }

        $pathMatch = [System.Text.RegularExpressions.Regex]::Match(
            $commandLine,
            '(?i)(?:^|\s)--path(?:=|\s+)(?:"(?<quoted>[^"]+)"|(?<plain>\S+))')
        if (-not $pathMatch.Success) {
            continue
        }

        $candidatePath = if ($pathMatch.Groups['quoted'].Success) {
            $pathMatch.Groups['quoted'].Value
        }
        else {
            $pathMatch.Groups['plain'].Value
        }
        if (-not (Test-IsFullyQualifiedWindowsPath -Path $candidatePath)) {
            continue
        }

        try {
            $candidateRoot = Get-CanonicalProjectPath -Path $candidatePath
        }
        catch {
            continue
        }

        if ([string]::Equals($candidateRoot, $canonicalRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $matches.Add([PSCustomObject]@{
                ProcessId = [int]$process.ProcessId
                CommandLine = $commandLine
            })
        }
    }

    return $matches.ToArray()
}

function Invoke-GodotVersionProbe {
    param([Parameter(Mandatory = $true)][string]$Executable)

    $exitCode = -1
    try {
        $outputLines = @(& $Executable --version 2>&1)
        if ($null -ne $LASTEXITCODE) {
            $exitCode = [int]$LASTEXITCODE
        }
        $output = ($outputLines | ForEach-Object { [string]$_ }) -join "`n"
    }
    catch {
        $output = $_.Exception.Message
    }

    return [PSCustomObject]@{
        ExitCode = $exitCode
        Output = $output.Trim()
    }
}

function Test-CompatibleGodotVersion {
    param([Parameter(Mandatory = $true)]$ProbeResult)

    $output = [string]$ProbeResult.Output
    return [int]$ProbeResult.ExitCode -eq 0 -and
        $output -match '(?m)^\s*4\.6\.\d+[^\r\n]*\.mono\.'
}

function Select-CompatibleGodotExecutable {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Candidates,
        [scriptblock]$VersionProbe,
        [switch]$Quiet
    )

    if ($null -eq $VersionProbe) {
        $VersionProbe = { param($candidatePath) Invoke-GodotVersionProbe -Executable $candidatePath }
    }

    foreach ($candidate in $Candidates) {
        $probeResult = & $VersionProbe ([string]$candidate.Path)
        if (Test-CompatibleGodotVersion -ProbeResult $probeResult) {
            return [string]$candidate.Path
        }

        if (-not $Quiet) {
            $reportedVersion = ([string]$probeResult.Output).Replace("`r", ' ').Replace("`n", ' ').Trim()
            if ([string]::IsNullOrWhiteSpace($reportedVersion)) {
                $reportedVersion = '<no version output>'
            }
            elseif ($reportedVersion.Length -gt 200) {
                $reportedVersion = $reportedVersion.Substring(0, 200) + '...'
            }
            Write-Warning (
                "Rejected Godot candidate from $($candidate.Origin) at '$($candidate.Path)'. " +
                "Godot 4.6.x Mono is required; exit=$($probeResult.ExitCode), reported='$reportedVersion'.")
        }
    }

    return $null
}

function Resolve-GodotExecutable {
    $candidates = [System.Collections.Generic.List[object]]::new()
    $seenPaths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)

    if (-not [string]::IsNullOrWhiteSpace($env:GODOT_MONO)) {
        $configuredValue = $env:GODOT_MONO.Trim().Trim('"')
        $configuredPath = if (Test-Path -LiteralPath $configuredValue -PathType Leaf) {
            [System.IO.Path]::GetFullPath($configuredValue)
        }
        else {
            $configuredCommand = Get-Command $configuredValue -CommandType Application -ErrorAction SilentlyContinue |
                Select-Object -First 1
            if ($null -ne $configuredCommand) {
                $configuredCommand.Source
            }
        }
        if (-not [string]::IsNullOrWhiteSpace($configuredPath)) {
            if ($seenPaths.Add($configuredPath)) {
                $candidates.Add([PSCustomObject]@{ Path = $configuredPath; Origin = 'GODOT_MONO' })
            }
        }
        else {
            Write-Warning "GODOT_MONO does not resolve to an executable: '$configuredValue'."
        }
    }

    $commandNames = @(
        'Godot_v4.6.3-stable_mono_win64_console.exe',
        'Godot_v4.6.3-stable_mono_win64.exe',
        'godot4.exe',
        'godot.exe'
    )
    foreach ($commandName in $commandNames) {
        $command = Get-Command $commandName -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $command -and $seenPaths.Add($command.Source)) {
            $candidates.Add([PSCustomObject]@{ Path = $command.Source; Origin = "PATH ($commandName)" })
        }
    }

    $downloadRoot = Join-Path $env:USERPROFILE 'Downloads\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64'
    $defaultPaths = @(
        (Join-Path $downloadRoot 'Godot_v4.6.3-stable_mono_win64_console.exe'),
        (Join-Path $downloadRoot 'Godot_v4.6.3-stable_mono_win64.exe')
    )
    foreach ($defaultPath in $defaultPaths) {
        if ((Test-Path -LiteralPath $defaultPath -PathType Leaf) -and $seenPaths.Add($defaultPath)) {
            $candidates.Add([PSCustomObject]@{ Path = $defaultPath; Origin = 'default Downloads location' })
        }
    }

    return Select-CompatibleGodotExecutable -Candidates $candidates.ToArray()
}

function ConvertTo-SteelTideBackendHealth {
    param(
        [AllowNull()]$Response,
        [Parameter(Mandatory = $true)][string]$ExpectedInstanceId,
        [bool]$Reachable = $true
    )

    $status = ''
    $service = ''
    $instance = ''
    if ($Reachable -and $null -ne $Response) {
        $statusProperty = $Response.PSObject.Properties['status']
        $serviceProperty = $Response.PSObject.Properties['service']
        $instanceProperty = $Response.PSObject.Properties['instance']
        if ($null -ne $statusProperty) {
            $status = [string]$statusProperty.Value
        }
        if ($null -ne $serviceProperty) {
            $service = [string]$serviceProperty.Value
        }
        if ($null -ne $instanceProperty) {
            $instance = [string]$instanceProperty.Value
        }
    }

    $isSteelTide = $service -ceq 'steel-tide-backend'
    return [PSCustomObject]@{
        Reachable = $Reachable
        Status = $status
        Service = $service
        Instance = $instance
        IsSteelTide = $isSteelTide
        CanReuse = $Reachable -and
            $status -ceq 'ok' -and
            $isSteelTide -and
            $instance -ceq $ExpectedInstanceId
    }
}

function Get-SteelTideBackendHealth {
    param(
        [Parameter(Mandatory = $true)][string]$ExpectedInstanceId,
        [scriptblock]$HealthRequest
    )

    try {
        $health = if ($null -ne $HealthRequest) {
            & $HealthRequest
        }
        else {
            Invoke-RestMethod `
                -Uri 'http://127.0.0.1:8787/api/v1/health' `
                -Method Get `
                -UseBasicParsing `
                -TimeoutSec 1 `
                -ErrorAction Stop
        }
        return ConvertTo-SteelTideBackendHealth `
            -Response $health `
            -ExpectedInstanceId $ExpectedInstanceId
    }
    catch {
        return ConvertTo-SteelTideBackendHealth `
            -Response $null `
            -ExpectedInstanceId $ExpectedInstanceId `
            -Reachable $false
    }
}

function Wait-SteelTideBackendHealth {
    param(
        [Parameter(Mandatory = $true)][System.Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][string]$ExpectedInstanceId,
        [int]$TimeoutSeconds = 10
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $health = Get-SteelTideBackendHealth -ExpectedInstanceId $ExpectedInstanceId
        if ($health.CanReuse) {
            return $true
        }

        $Process.Refresh()
        if ($Process.HasExited) {
            return $false
        }

        Start-Sleep -Milliseconds 200
    }

    $finalHealth = Get-SteelTideBackendHealth -ExpectedInstanceId $ExpectedInstanceId
    return $finalHealth.CanReuse
}

function Get-GodotTopLevelLogErrors {
    param([Parameter(Mandatory = $true)][string]$LogPath)

    return @(Select-String `
        -LiteralPath $LogPath `
        -Pattern '^(?:ERROR|SCRIPT ERROR):' `
        -CaseSensitive)
}

function ConvertTo-WindowsProcessArgument {
    param([AllowEmptyString()][AllowNull()][string]$Argument)

    if ($null -eq $Argument) {
        $Argument = ''
    }
    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') {
        return $Argument
    }

    $backslash = [char]92
    $quote = [char]34
    $builder = [System.Text.StringBuilder]::new()
    $null = $builder.Append($quote)
    $backslashCount = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq $backslash) {
            $backslashCount++
            continue
        }
        if ($character -eq $quote) {
            $null = $builder.Append($backslash, ($backslashCount * 2) + 1)
            $null = $builder.Append($quote)
            $backslashCount = 0
            continue
        }
        if ($backslashCount -gt 0) {
            $null = $builder.Append($backslash, $backslashCount)
            $backslashCount = 0
        }
        $null = $builder.Append($character)
    }
    if ($backslashCount -gt 0) {
        $null = $builder.Append($backslash, $backslashCount * 2)
    }
    $null = $builder.Append($quote)
    return $builder.ToString()
}

function Join-WindowsProcessArguments {
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Arguments)

    return (@($Arguments | ForEach-Object { ConvertTo-WindowsProcessArgument -Argument $_ }) -join ' ')
}

function Wait-GodotRuntimeReady {
    param(
        [Parameter(Mandatory = $true)][System.Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [ValidateRange(1000, 120000)][int]$TimeoutMilliseconds = 120000
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $LogPath -PathType Leaf) {
            try {
                if (Select-String `
                    -LiteralPath $LogPath `
                    -SimpleMatch 'STEEL_TIDE_RUNTIME_READY' `
                    -Quiet) {
                    return $true
                }
            }
            catch {
                # Godot may briefly hold the new log exclusively while opening it.
            }
        }

        $Process.Refresh()
        if ($Process.HasExited) {
            return $false
        }
        Start-Sleep -Milliseconds 100
    }
    return $false
}

function Set-GameBackendEnvironment {
    param(
        [Parameter(Mandatory = $true)][string]$InstanceId,
        [Parameter(Mandatory = $true)][bool]$Offline,
        [AllowEmptyString()][string]$ParallelInstanceId = ''
    )

    $snapshot = [PSCustomObject]@{
        Instance = [Environment]::GetEnvironmentVariable('STEEL_TIDE_BACKEND_INSTANCE', 'Process')
        Offline = [Environment]::GetEnvironmentVariable('STEEL_TIDE_BACKEND_OFFLINE', 'Process')
        ParallelInstance = [Environment]::GetEnvironmentVariable(
            'STEEL_TIDE_PARALLEL_INSTANCE',
            'Process')
    }
    [Environment]::SetEnvironmentVariable('STEEL_TIDE_BACKEND_INSTANCE', $InstanceId, 'Process')
    [Environment]::SetEnvironmentVariable(
        'STEEL_TIDE_BACKEND_OFFLINE',
        $(if ($Offline) { '1' } else { '0' }),
        'Process')
    [Environment]::SetEnvironmentVariable(
        'STEEL_TIDE_PARALLEL_INSTANCE',
        $(if ([string]::IsNullOrWhiteSpace($ParallelInstanceId)) { $null } else { $ParallelInstanceId }),
        'Process')
    return $snapshot
}

function Restore-GameBackendEnvironment {
    param([Parameter(Mandatory = $true)]$Snapshot)

    [Environment]::SetEnvironmentVariable(
        'STEEL_TIDE_BACKEND_INSTANCE',
        $Snapshot.Instance,
        'Process')
    [Environment]::SetEnvironmentVariable(
        'STEEL_TIDE_BACKEND_OFFLINE',
        $Snapshot.Offline,
        'Process')
    [Environment]::SetEnvironmentVariable(
        'STEEL_TIDE_PARALLEL_INSTANCE',
        $Snapshot.ParallelInstance,
        'Process')
}

function Test-StartupRunDirectoryName {
    param([Parameter(Mandatory = $true)][string]$Name)

    $nameMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Name,
        '^(?<timestamp>\d{8}-\d{6}-\d{3})-(?<pid>\d+)-(?<nonce>[0-9a-f]{8})$',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $nameMatch.Success) {
        return $false
    }

    $timestamp = [DateTime]::MinValue
    return [DateTime]::TryParseExact(
        $nameMatch.Groups['timestamp'].Value,
        'yyyyMMdd-HHmmss-fff',
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::None,
        [ref]$timestamp)
}

function Get-StartupRunDirectoryProcessId {
    param([Parameter(Mandatory = $true)][string]$Name)

    $nameMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Name,
        '^\d{8}-\d{6}-\d{3}-(?<pid>\d+)-[0-9a-f]{8}$',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $nameMatch.Success) {
        return $null
    }

    $processId = 0
    if (-not [int]::TryParse($nameMatch.Groups['pid'].Value, [ref]$processId) -or $processId -le 0) {
        return $null
    }
    return $processId
}

function Remove-OldStartupRunLogs {
    param(
        [Parameter(Mandatory = $true)][string]$StartupLogRoot,
        [ValidateRange(1, 1000)][int]$Keep = 20
    )

    try {
        $rootItem = Get-Item -LiteralPath $StartupLogRoot -ErrorAction Stop
        if (-not $rootItem.PSIsContainer -or
            ($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            Write-Warning "Startup log retention skipped an unsafe root: '$StartupLogRoot'."
            return
        }
        $canonicalRoot = Get-CanonicalProjectPath -Path $rootItem.FullName
        $eligibleDirectories = [System.Collections.Generic.List[object]]::new()
        foreach ($directory in (Get-ChildItem -LiteralPath $canonicalRoot -Directory -Force)) {
            if (-not (Test-StartupRunDirectoryName -Name $directory.Name) -or
                ($directory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                continue
            }

            $launcherProcessId = Get-StartupRunDirectoryProcessId -Name $directory.Name
            if ($null -ne $launcherProcessId -and
                $null -ne (Get-Process -Id $launcherProcessId -ErrorAction SilentlyContinue)) {
                continue
            }

            try {
                $resolvedPath = Get-CanonicalProjectPath -Path (
                    (Resolve-Path -LiteralPath $directory.FullName -ErrorAction Stop).ProviderPath)
                $resolvedParent = Get-CanonicalProjectPath -Path (
                    [System.IO.Directory]::GetParent($resolvedPath).FullName)
                if (-not [string]::Equals(
                    $resolvedParent,
                    $canonicalRoot,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                    continue
                }
                $eligibleDirectories.Add([PSCustomObject]@{
                    Name = $directory.Name
                    Path = $resolvedPath
                })
            }
            catch {
                Write-Warning "Startup log retention skipped '$($directory.FullName)': $($_.Exception.Message)"
            }
        }

        $expiredDirectories = @($eligibleDirectories.ToArray() |
            Sort-Object -Property Name -Descending |
            Select-Object -Skip $Keep)
        foreach ($expiredDirectory in $expiredDirectories) {
            try {
                $verifiedItem = Get-Item -LiteralPath $expiredDirectory.Path -Force -ErrorAction Stop
                if (-not $verifiedItem.PSIsContainer -or
                    ($verifiedItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                    -not (Test-StartupRunDirectoryName -Name $verifiedItem.Name)) {
                    continue
                }
                $verifiedPath = Get-CanonicalProjectPath -Path (
                    (Resolve-Path -LiteralPath $verifiedItem.FullName -ErrorAction Stop).ProviderPath)
                $verifiedParent = Get-CanonicalProjectPath -Path (
                    [System.IO.Directory]::GetParent($verifiedPath).FullName)
                if (-not [string]::Equals(
                    $verifiedParent,
                    $canonicalRoot,
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                    continue
                }

                Remove-Item -LiteralPath $verifiedPath -Recurse -Force -ErrorAction Stop
            }
            catch {
                Write-Warning "Could not prune startup log '$($expiredDirectory.Path)': $($_.Exception.Message)"
            }
        }
    }
    catch {
        Write-Warning "Startup log retention failed safely: $($_.Exception.Message)"
    }
}

function Stop-OwnedBackend {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) {
        return
    }

    try {
        $Process.Refresh()
        if (-not $Process.HasExited) {
            Write-Host "Stopping launcher-owned mission service (PID $($Process.Id))..."
            Stop-Process -InputObject $Process -Force -ErrorAction Stop
            $null = $Process.WaitForExit(5000)
        }
    }
    catch {
        Write-Warning "Could not stop launcher-owned mission service PID $($Process.Id): $($_.Exception.Message)"
    }
    finally {
        $Process.Dispose()
    }
}

function Remove-ParallelRuntimeProfile {
    param([Parameter(Mandatory = $true)][string]$RunLogDirectory)

    try {
        $canonicalRunDirectory = Get-CanonicalProjectPath -Path $RunLogDirectory
        foreach ($fileName in @('operator_profile_parallel.json', 'operator_profile_parallel.json.tmp')) {
            $candidatePath = [System.IO.Path]::GetFullPath((Join-Path $canonicalRunDirectory $fileName))
            $candidateParent = Get-CanonicalProjectPath -Path (
                [System.IO.Directory]::GetParent($candidatePath).FullName)
            if (-not [string]::Equals(
                $candidateParent,
                $canonicalRunDirectory,
                [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Parallel profile escaped its run-log directory: '$candidatePath'."
            }
            if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
                Remove-Item -LiteralPath $candidatePath -Force -ErrorAction Stop
            }
        }
    }
    catch {
        Write-Warning "Could not clean the parallel runtime profile: $($_.Exception.Message)"
    }
}

function Invoke-LauncherSelfTest {
    $failures = [System.Collections.Generic.List[string]]::new()
    $testProjectRoot = 'C:\OperationSteelTideLauncherSelfTest'

    $relativeProcess = [PSCustomObject]@{
        ProcessId = 101
        CommandLine = '"C:\Godot.exe" --path .'
    }
    $absoluteProcess = [PSCustomObject]@{
        ProcessId = 202
        CommandLine = '"C:\Godot.exe" --path="C:\OperationSteelTideLauncherSelfTest\" --editor'
    }
    $runtimeProcess = [PSCustomObject]@{
        ProcessId = 303
        CommandLine = '"C:\Godot.exe" --path "C:\OperationSteelTideLauncherSelfTest" --log-file "C:\runtime.log" --'
    }
    $relativeMatches = @(Get-RunningGodotProcessesForProject `
        -ProjectRoot $testProjectRoot `
        -Processes @($relativeProcess))
    $absoluteMatches = @(Get-RunningGodotProcessesForProject `
        -ProjectRoot $testProjectRoot `
        -Processes @($absoluteProcess))
    $editorRuntimeMatches = @(Get-RunningGodotProcessesForProject `
        -ProjectRoot $testProjectRoot `
        -Processes @($absoluteProcess) `
        -RuntimeOnly)
    $runtimeMatches = @(Get-RunningGodotProcessesForProject `
        -ProjectRoot $testProjectRoot `
        -Processes @($runtimeProcess) `
        -RuntimeOnly)
    if ($relativeMatches.Count -ne 0) {
        $failures.Add('Relative Godot --path was incorrectly resolved against the launcher process.')
    }
    if ($absoluteMatches.Count -ne 1 -or $absoluteMatches[0].ProcessId -ne 202) {
        $failures.Add('Absolute matching Godot --path was not detected.')
    }
    if ($editorRuntimeMatches.Count -ne 0 -or
        $runtimeMatches.Count -ne 1 -or
        $runtimeMatches[0].ProcessId -ne 303) {
        $failures.Add('Godot editor/import filtering did not preserve actual runtime detection.')
    }

    $quotedPlain = ConvertTo-WindowsProcessArgument -Argument 'plain'
    $quotedEmpty = ConvertTo-WindowsProcessArgument -Argument ''
    $quotedTrailingSlash = ConvertTo-WindowsProcessArgument -Argument 'C:\Path With Space\'
    $quotedEmbeddedQuote = ConvertTo-WindowsProcessArgument -Argument 'say"hi'
    if ($quotedPlain -cne 'plain' -or
        $quotedEmpty -cne '""' -or
        $quotedTrailingSlash -cne '"C:\Path With Space\\"' -or
        $quotedEmbeddedQuote -cne '"say\"hi"') {
        $failures.Add('Windows child-process argument quoting is not command-line safe.')
    }

    $preparationMutexName = Get-ProjectMutexName `
        -ProjectRoot ($testProjectRoot + '\') `
        -Scope Preparation
    $caseVariantPreparationMutexName = Get-ProjectMutexName `
        -ProjectRoot $testProjectRoot.ToLowerInvariant() `
        -Scope Preparation
    $runtimeMutexName = Get-ProjectMutexName -ProjectRoot $testProjectRoot -Scope Runtime
    $otherPreparationMutexName = Get-ProjectMutexName `
        -ProjectRoot 'C:\OperationSteelTideLauncherSelfTestSibling' `
        -Scope Preparation
    if ($preparationMutexName -cne $caseVariantPreparationMutexName -or
        $preparationMutexName -ceq $runtimeMutexName -or
        $preparationMutexName -ceq $otherPreparationMutexName) {
        $failures.Add('Preparation/runtime mutex names are not stable and project-scoped.')
    }

    $primaryPolicy = Get-GameRuntimePolicy `
        -OwnsRuntimeMutex $true `
        -RunningProjectInstanceCount 0
    $parallelByMutexPolicy = Get-GameRuntimePolicy `
        -OwnsRuntimeMutex $false `
        -RunningProjectInstanceCount 0
    $parallelByProcessPolicy = Get-GameRuntimePolicy `
        -OwnsRuntimeMutex $true `
        -RunningProjectInstanceCount 1
    if (-not $primaryPolicy.BackendAllowed -or $primaryPolicy.Parallel -or
        $parallelByMutexPolicy.BackendAllowed -or -not $parallelByMutexPolicy.Parallel -or
        $parallelByProcessPolicy.BackendAllowed -or -not $parallelByProcessPolicy.Parallel) {
        $failures.Add('Parallel runtime policy did not reserve the backend for one primary game.')
    }

    $pathHash = Get-ProjectPathHash -ProjectRoot $testProjectRoot
    $emptyContentHash = Get-Sha256Hex -Bytes ([byte[]]@())
    $contentHashA = Get-Sha256Hex -Bytes ([System.Text.Encoding]::UTF8.GetBytes('package main'))
    $contentHashB = Get-Sha256Hex -Bytes ([System.Text.Encoding]::UTF8.GetBytes('package changed'))
    $moduleHash = Get-Sha256Hex -Bytes ([System.Text.Encoding]::UTF8.GetBytes('module example'))
    $sourceEntriesA = @(
        [PSCustomObject]@{ Path = 'backend/cmd/server/main.go'; ContentHash = $contentHashA },
        [PSCustomObject]@{ Path = 'backend/go.mod'; ContentHash = $moduleHash }
    )
    $sourceEntriesReordered = @($sourceEntriesA[1], $sourceEntriesA[0])
    $sourceEntriesChanged = @(
        [PSCustomObject]@{ Path = 'backend/cmd/server/main.go'; ContentHash = $contentHashB },
        [PSCustomObject]@{ Path = 'backend/go.mod'; ContentHash = $moduleHash }
    )
    $fingerprintA = Get-BackendSourceFingerprintFromEntries -Entries $sourceEntriesA
    $fingerprintReordered = Get-BackendSourceFingerprintFromEntries -Entries $sourceEntriesReordered
    $fingerprintChanged = Get-BackendSourceFingerprintFromEntries -Entries $sourceEntriesChanged
    $instanceA = New-BackendInstanceId -ProjectPathHash $pathHash -SourceFingerprint $fingerprintA
    $instanceChanged = New-BackendInstanceId -ProjectPathHash $pathHash -SourceFingerprint $fingerprintChanged
    if ($emptyContentHash -cne 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855') {
        $failures.Add('SHA-256 helper did not accept or hash empty file content correctly.')
    }
    if ($fingerprintA -cne $fingerprintReordered) {
        $failures.Add('Backend source fingerprint depends on input enumeration order.')
    }
    if ($fingerprintA -ceq $fingerprintChanged -or $instanceA -ceq $instanceChanged) {
        $failures.Add('A backend source content change did not change the backend instance identity.')
    }
    try {
        $actualProjectRoot = Get-CanonicalProjectPath -Path (Join-Path $PSScriptRoot '..')
        $filesystemFingerprint = Get-BackendSourceFingerprint -ProjectRoot $actualProjectRoot
        if ($filesystemFingerprint -notmatch '^[0-9a-f]{64}$') {
            $failures.Add('Backend filesystem fingerprint did not produce a lowercase SHA-256 value.')
        }
    }
    catch {
        $failures.Add("Backend filesystem fingerprint failed: $($_.Exception.Message)")
    }

    $matchingHealth = ConvertTo-SteelTideBackendHealth `
        -ExpectedInstanceId $instanceA `
        -Response ([PSCustomObject]@{
            status = 'ok'
            service = 'steel-tide-backend'
            instance = $instanceA
        })
    $mismatchedHealth = ConvertTo-SteelTideBackendHealth `
        -ExpectedInstanceId $instanceA `
        -Response ([PSCustomObject]@{
            status = 'ok'
            service = 'steel-tide-backend'
            instance = $instanceChanged
        })
    $legacyHealth = ConvertTo-SteelTideBackendHealth `
        -ExpectedInstanceId $instanceA `
        -Response ([PSCustomObject]@{
            status = 'ok'
            service = 'steel-tide-backend'
        })
    if (-not $matchingHealth.CanReuse -or $mismatchedHealth.CanReuse -or $legacyHealth.CanReuse) {
        $failures.Add('Backend health identity matching accepted an incompatible service or rejected a match.')
    }

    $originalEnvironmentInstance = [Environment]::GetEnvironmentVariable(
        'STEEL_TIDE_BACKEND_INSTANCE',
        'Process')
    $originalEnvironmentOffline = [Environment]::GetEnvironmentVariable(
        'STEEL_TIDE_BACKEND_OFFLINE',
        'Process')
    $originalParallelInstance = [Environment]::GetEnvironmentVariable(
        'STEEL_TIDE_PARALLEL_INSTANCE',
        'Process')
    $backendEnvironmentSnapshot = $null
    try {
        [Environment]::SetEnvironmentVariable(
            'STEEL_TIDE_BACKEND_INSTANCE',
            'self-test-sentinel-instance',
            'Process')
        [Environment]::SetEnvironmentVariable(
            'STEEL_TIDE_BACKEND_OFFLINE',
            'self-test-sentinel-offline',
            'Process')
        [Environment]::SetEnvironmentVariable(
            'STEEL_TIDE_PARALLEL_INSTANCE',
            'self-test-sentinel-parallel',
            'Process')
        $backendEnvironmentSnapshot = Set-GameBackendEnvironment `
            -InstanceId $instanceA `
            -Offline $true `
            -ParallelInstanceId '20260827-221530-123-4567-a1b2c3d4'
        $environmentApplied =
            [Environment]::GetEnvironmentVariable('STEEL_TIDE_BACKEND_INSTANCE', 'Process') -ceq $instanceA -and
            [Environment]::GetEnvironmentVariable('STEEL_TIDE_BACKEND_OFFLINE', 'Process') -ceq '1' -and
            [Environment]::GetEnvironmentVariable('STEEL_TIDE_PARALLEL_INSTANCE', 'Process') -ceq '20260827-221530-123-4567-a1b2c3d4'
        Restore-GameBackendEnvironment -Snapshot $backendEnvironmentSnapshot
        $backendEnvironmentSnapshot = $null
        $environmentRestored =
            [Environment]::GetEnvironmentVariable('STEEL_TIDE_BACKEND_INSTANCE', 'Process') -ceq 'self-test-sentinel-instance' -and
            [Environment]::GetEnvironmentVariable('STEEL_TIDE_BACKEND_OFFLINE', 'Process') -ceq 'self-test-sentinel-offline' -and
            [Environment]::GetEnvironmentVariable('STEEL_TIDE_PARALLEL_INSTANCE', 'Process') -ceq 'self-test-sentinel-parallel'
        if (-not $environmentApplied -or -not $environmentRestored) {
            $failures.Add('Game backend environment was not applied or restored exactly.')
        }
    }
    finally {
        if ($null -ne $backendEnvironmentSnapshot) {
            Restore-GameBackendEnvironment -Snapshot $backendEnvironmentSnapshot
        }
        [Environment]::SetEnvironmentVariable(
            'STEEL_TIDE_BACKEND_INSTANCE',
            $originalEnvironmentInstance,
            'Process')
        [Environment]::SetEnvironmentVariable(
            'STEEL_TIDE_BACKEND_OFFLINE',
            $originalEnvironmentOffline,
            'Process')
        [Environment]::SetEnvironmentVariable(
            'STEEL_TIDE_PARALLEL_INSTANCE',
            $originalParallelInstance,
            'Process')
    }

    $versionCandidates = @(
        [PSCustomObject]@{ Path = 'C:\WrongGodot.exe'; Origin = 'self-test wrong candidate' },
        [PSCustomObject]@{ Path = 'C:\RightGodot.exe'; Origin = 'self-test right candidate' }
    )
    $selectedVersion = Select-CompatibleGodotExecutable `
        -Candidates $versionCandidates `
        -Quiet `
        -VersionProbe {
            param($candidatePath)
            if ($candidatePath -like '*WrongGodot.exe') {
                return [PSCustomObject]@{ ExitCode = 0; Output = '4.6.3.stable.official' }
            }
            return [PSCustomObject]@{ ExitCode = 0; Output = '4.6.3.stable.mono.official.selftest' }
        }
    if ($selectedVersion -cne 'C:\RightGodot.exe') {
        $failures.Add('Godot version selection did not reject non-Mono 4.6 or continue to a valid candidate.')
    }
    $wrongMajorVersion = [PSCustomObject]@{ ExitCode = 0; Output = '4.7.0.stable.mono.official.selftest' }
    if (Test-CompatibleGodotVersion -ProbeResult $wrongMajorVersion) {
        $failures.Add('Godot version selection accepted a non-4.6 Mono build.')
    }

    $validRunDirectoryName = '20260827-221530-123-4567-a1b2c3d4'
    $runDirectoryProcessId = Get-StartupRunDirectoryProcessId -Name $validRunDirectoryName
    if (-not (Test-StartupRunDirectoryName -Name $validRunDirectoryName) -or
        $runDirectoryProcessId -ne 4567 -or
        (Test-StartupRunDirectoryName -Name 'startup-validation') -or
        $null -ne (Get-StartupRunDirectoryProcessId -Name 'startup-validation')) {
        $failures.Add('Startup log directory name safety filter is incorrect.')
    }

    if ($failures.Count -gt 0) {
        foreach ($failure in $failures) {
            [Console]::Error.WriteLine("SELF_TEST_FAIL $failure")
        }
        return 1
    }

    Write-Host 'SELF_TEST_PASS relative_path=True absolute_path=True editor_filter=True argument_quote=True preparation_mutex=True runtime_parallel=True backend_identity=True source_change=True filesystem_hash=True backend_environment=True godot_version=True active_log_filter=True'
    return 0
}

function Invoke-OperationSteelTideLauncher {
    param([string[]]$GameArguments)

    $projectRoot = Get-CanonicalProjectPath -Path (Join-Path $PSScriptRoot '..')
    $preparationMutex = New-Object System.Threading.Mutex(
        $false,
        (Get-ProjectMutexName -ProjectRoot $projectRoot -Scope Preparation))
    $runtimeMutex = New-Object System.Threading.Mutex(
        $false,
        (Get-ProjectMutexName -ProjectRoot $projectRoot -Scope Runtime))
    $ownsPreparationMutex = $false
    $ownsRuntimeMutex = $false
    $ownedBackendProcess = $null
    $gameProcess = $null
    $gameProcessStarted = $false
    $gameProcessExited = $false
    $gameEnvironmentSnapshot = $null
    $parallelProfileRunDirectory = $null

    try {
        try {
            $ownsPreparationMutex = $preparationMutex.WaitOne(0, $false)
        }
        catch [System.Threading.AbandonedMutexException] {
            $ownsPreparationMutex = $true
        }

        if (-not $ownsPreparationMutex) {
            Write-Host (
                'Another launcher is building or importing this project. ' +
                'Waiting for the shared preparation step to finish...')
            try {
                $ownsPreparationMutex = $preparationMutex.WaitOne(900000, $false)
            }
            catch [System.Threading.AbandonedMutexException] {
                $ownsPreparationMutex = $true
            }
            if (-not $ownsPreparationMutex) {
                [Console]::Error.WriteLine(
                    'Timed out after 15 minutes while waiting for project build/import preparation.')
                return 3
            }
        }

        $privateAssetSync = Join-Path $PSScriptRoot 'Sync-PrivateOperatorAssets.ps1'
        if (Test-Path -LiteralPath $privateAssetSync -PathType Leaf) {
            & $privateAssetSync -ProjectRoot $projectRoot
            if ($LASTEXITCODE -ne 0) {
                return $LASTEXITCODE
            }
        }

        $godot = Resolve-GodotExecutable
        if ([string]::IsNullOrWhiteSpace($godot)) {
            [Console]::Error.WriteLine(
                'Compatible Godot 4.6.x Mono was not found. Add it to PATH or set GODOT_MONO to its executable path.')
            return 1
        }

        $dotnet = Get-Command 'dotnet.exe' -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -eq $dotnet) {
            [Console]::Error.WriteLine(
                '.NET SDK was not found. Install .NET 8 SDK or add dotnet.exe to PATH.')
            return 1
        }

        $runId = '{0}-{1}-{2}' -f `
            (Get-Date -Format 'yyyyMMdd-HHmmss-fff'),
            $PID,
            ([Guid]::NewGuid().ToString('N').Substring(0, 8))
        $startupLogRoot = Join-Path $projectRoot 'logs\startup'
        $null = New-Item -ItemType Directory -Path $startupLogRoot -Force
        Remove-OldStartupRunLogs -StartupLogRoot $startupLogRoot -Keep 19
        $runLogDirectory = Join-Path $startupLogRoot $runId
        $importLog = Join-Path $runLogDirectory 'import.log'
        $runtimeLog = Join-Path $runLogDirectory 'runtime.log'
        $backendLog = Join-Path $runLogDirectory 'backend.log'
        $backendErrorLog = Join-Path $runLogDirectory 'backend-error.log'
        $null = New-Item -ItemType Directory -Path $runLogDirectory -Force

        Write-Host 'Operation Steel Tide startup'
        Write-Host "  Project:     $projectRoot"
        Write-Host "  Godot:       $godot"
        Write-Host "  Run logs:    $runLogDirectory"
        Write-Host "  Import log:  $importLog"
        Write-Host "  Runtime log: $runtimeLog"

        Write-Host 'Updating the C# game assembly...'
        $git = Get-Command 'git.exe' -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $git) {
            $commit = & $git.Source -C $projectRoot log --oneline -1 2>$null
            if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commit)) {
                Write-Host "  Commit: $commit"
            }
        }
        & $dotnet.Source build (Join-Path $projectRoot 'OperationSteelTide.csproj') --nologo --verbosity minimal |
            Out-Host
        $buildExitCode = $LASTEXITCODE
        if ($buildExitCode -ne 0) {
            [Console]::Error.WriteLine(
                "The C# game assembly could not be updated (exit code $buildExitCode).")
            return $buildExitCode
        }

        Write-Host 'Importing Godot resources...'
        & $godot --headless --path $projectRoot --import --log-file $importLog | Out-Host
        $importExitCode = $LASTEXITCODE
        if ($importExitCode -ne 0) {
            [Console]::Error.WriteLine(
                "Godot resource import failed with exit code $importExitCode. See '$importLog'.")
            return $importExitCode
        }
        if (-not (Test-Path -LiteralPath $importLog -PathType Leaf)) {
            [Console]::Error.WriteLine(
                "Godot resource import did not create its expected log. See '$runLogDirectory'.")
            return 1
        }
        $importErrors = @(Get-GodotTopLevelLogErrors -LogPath $importLog)
        if ($importErrors.Count -gt 0) {
            [Console]::Error.WriteLine(
                "Godot reported $($importErrors.Count) resource import error(s). See '$importLog'.")
            foreach ($importError in ($importErrors | Select-Object -First 10)) {
                [Console]::Error.WriteLine("  $($importError.Line)")
            }
            return 1
        }

        $runningProjectInstances = @(Get-RunningGodotProcessesForProject `
            -ProjectRoot $projectRoot `
            -RuntimeOnly)
        try {
            $ownsRuntimeMutex = $runtimeMutex.WaitOne(0, $false)
        }
        catch [System.Threading.AbandonedMutexException] {
            $ownsRuntimeMutex = $true
        }
        $runtimePolicy = Get-GameRuntimePolicy `
            -OwnsRuntimeMutex $ownsRuntimeMutex `
            -RunningProjectInstanceCount $runningProjectInstances.Count
        $parallelInstanceId = ''
        if ($runtimePolicy.Parallel) {
            $parallelInstanceId = $runId
            $parallelProfileRunDirectory = $runLogDirectory
            $runningPids = if ($runningProjectInstances.Count -gt 0) {
                ($runningProjectInstances.ProcessId | Sort-Object -Unique) -join ', '
            }
            else {
                'coordinated launcher instance'
            }
            Write-Host (
                "Parallel runtime mode enabled (existing: $runningPids). " +
                'This game will use isolated temporary progression and the offline mission fallback.')
        }

        $serverPath = Join-Path $projectRoot 'steel-tide-server.exe'
        $backendDataPath = Join-Path $projectRoot 'backend\data\state.json'
        $backendInstanceId = Get-BackendInstanceId -ProjectRoot $projectRoot
        $backendOffline = $true
        Write-Host "Backend instance: $backendInstanceId"
        if (-not $runtimePolicy.BackendAllowed) {
            Write-Host 'Parallel instances do not start, reuse, or stop the shared mission service.'
        }
        else {
            $backendHealth = Get-SteelTideBackendHealth -ExpectedInstanceId $backendInstanceId
            if ($backendHealth.CanReuse) {
                $backendOffline = $false
                Write-Host 'Using the matching healthy Steel Tide mission service on 127.0.0.1:8787.'
            }
            elseif ($backendHealth.Reachable) {
                if ($backendHealth.IsSteelTide) {
                    $reportedInstance = if ([string]::IsNullOrWhiteSpace($backendHealth.Instance)) {
                        '<missing>'
                    }
                    else {
                        $backendHealth.Instance
                    }
                    Write-Warning (
                        'A Steel Tide mission service from another checkout or an incompatible launcher ' +
                        "is already listening on 127.0.0.1:8787 (status='$($backendHealth.Status)', " +
                        "instance='$reportedInstance', expected='$backendInstanceId'). " +
                        'This launch will use the offline fallback without binding to or stopping that service.')
                }
                else {
                    Write-Warning (
                        "Another service answered the backend health URL (service='$($backendHealth.Service)'). " +
                        'This launch will use the offline fallback without binding to or stopping it.')
                }
            }
            else {
                $serverUsable = $false
                $go = Get-Command 'go.exe' -CommandType Application -ErrorAction SilentlyContinue |
                    Select-Object -First 1
                if ($null -ne $go) {
                    Write-Host 'Building the Go mission service...'
                    $identityBeforeBuild = Get-BackendInstanceId -ProjectRoot $projectRoot
                    $goBuildExitCode = 1
                    Push-Location (Join-Path $projectRoot 'backend')
                    try {
                        & $go.Source build -o $serverPath ./cmd/server | Out-Host
                        $goBuildExitCode = $LASTEXITCODE
                    }
                    finally {
                        Pop-Location
                    }
                    $identityAfterBuild = Get-BackendInstanceId -ProjectRoot $projectRoot
                    $sourceStableDuringBuild = $identityBeforeBuild -ceq $identityAfterBuild
                    $serverUsable = $goBuildExitCode -eq 0 -and
                        $sourceStableDuringBuild -and
                        (Test-Path -LiteralPath $serverPath -PathType Leaf)
                    if ($goBuildExitCode -ne 0) {
                        Write-Warning (
                            "The Go mission service build failed with exit code $goBuildExitCode. " +
                            'A pre-existing executable will not be used.')
                    }
                    elseif (-not $sourceStableDuringBuild) {
                        Write-Warning (
                            'Backend sources changed during the Go build. The resulting executable will not be started.')
                    }
                    elseif (-not $serverUsable) {
                        Write-Warning 'The Go build did not produce the expected mission service executable.'
                    }
                    else {
                        if ($backendInstanceId -cne $identityAfterBuild) {
                            Write-Host "Backend instance after source refresh: $identityAfterBuild"
                        }
                        $backendInstanceId = $identityAfterBuild
                    }
                }
                else {
                    Write-Warning (
                        'Go is unavailable, so no local mission service can be built. ' +
                        'A pre-existing on-disk executable will not be started.')
                }

                if ($serverUsable) {
                    try {
                        Write-Host 'Starting the Steel Tide mission service...'
                        Write-Host "  Backend log:       $backendLog"
                        Write-Host "  Backend error log: $backendErrorLog"
                        $backendArguments = @(
                            '-addr',
                            '127.0.0.1:8787',
                            '-data',
                            ('"{0}"' -f $backendDataPath),
                            '-instance',
                            $backendInstanceId
                        )
                        $ownedBackendProcess = Start-Process `
                            -FilePath $serverPath `
                            -WorkingDirectory $projectRoot `
                            -ArgumentList $backendArguments `
                            -RedirectStandardOutput $backendLog `
                            -RedirectStandardError $backendErrorLog `
                            -WindowStyle Hidden `
                            -PassThru

                        if (Wait-SteelTideBackendHealth `
                            -Process $ownedBackendProcess `
                            -ExpectedInstanceId $backendInstanceId) {
                            $backendOffline = $false
                            Write-Host "  Mission service ready (PID $($ownedBackendProcess.Id))."
                        }
                        else {
                            $ownedBackendProcess.Refresh()
                            $backendStatus = if ($ownedBackendProcess.HasExited) {
                                "exited with code $($ownedBackendProcess.ExitCode)"
                            }
                            else {
                                'did not become healthy within 10 seconds'
                            }
                            Write-Warning (
                                "The mission service $backendStatus. " +
                                'Starting with the built-in offline mission fallback.')
                            Stop-OwnedBackend -Process $ownedBackendProcess
                            $ownedBackendProcess = $null
                        }
                    }
                    catch {
                        Write-Warning (
                            "The mission service could not be started: $($_.Exception.Message). " +
                            'Starting with the built-in offline mission fallback.')
                        Stop-OwnedBackend -Process $ownedBackendProcess
                        $ownedBackendProcess = $null
                    }
                }
                else {
                    Write-Host 'Mission service unavailable; starting with the built-in offline mission fallback.'
                }
            }
        }

        Write-Host 'Starting Operation Steel Tide...'
        Write-Host '  Tip: In the Operations Office lobby use TIME to cycle Day/Dusk/Night/Dawn.'
        $gameEnvironmentSnapshot = Set-GameBackendEnvironment `
            -InstanceId $backendInstanceId `
            -Offline $backendOffline `
            -ParallelInstanceId $parallelInstanceId
        Write-Host (
            '  Backend environment: STEEL_TIDE_BACKEND_OFFLINE=' +
            $(if ($backendOffline) { '1 (offline fallback)' } else { '0 (matching instance)' }))
        $godotArguments = @('--path', $projectRoot, '--log-file', $runtimeLog, '--') + $GameArguments
        $godotCommandLine = Join-WindowsProcessArguments -Arguments $godotArguments
        $gameProcess = Start-Process `
            -FilePath $godot `
            -WorkingDirectory $projectRoot `
            -ArgumentList $godotCommandLine `
            -NoNewWindow `
            -PassThru
        $gameProcessStarted = $true
        $runtimeReady = Wait-GodotRuntimeReady -Process $gameProcess -LogPath $runtimeLog
        if (-not $runtimeReady) {
            $gameProcess.Refresh()
            if ($gameProcess.HasExited) {
                Write-Warning 'Godot exited before reporting that the C# runtime was ready.'
            }
            else {
                Write-Warning (
                    'Godot did not report C# runtime readiness within 120 seconds. ' +
                    'Releasing shared preparation so other launchers are not blocked indefinitely.')
            }
        }
        if ($ownsPreparationMutex) {
            $preparationMutex.ReleaseMutex()
            $ownsPreparationMutex = $false
        }
        if ($runtimeReady) {
            Write-Host '  C# runtime loaded; other launchers may now build and import in parallel.'
        }
        else {
            Write-Host '  Shared preparation released; other launchers may now continue.'
        }
        $gameProcess.WaitForExit()
        $gameProcessExited = $true
        $gameExitCode = $gameProcess.ExitCode
        if ($gameExitCode -ne 0) {
            [Console]::Error.WriteLine(
                "The game exited with code $gameExitCode. See '$runtimeLog'.")
            return $gameExitCode
        }
        if (-not (Test-Path -LiteralPath $runtimeLog -PathType Leaf)) {
            [Console]::Error.WriteLine(
                "Godot exited without creating its expected runtime log. See '$runLogDirectory'.")
            return 1
        }
        $runtimeErrors = @(Get-GodotTopLevelLogErrors -LogPath $runtimeLog)
        if ($runtimeErrors.Count -gt 0) {
            [Console]::Error.WriteLine(
                "Godot reported $($runtimeErrors.Count) runtime error(s). See '$runtimeLog'.")
            foreach ($runtimeError in ($runtimeErrors | Select-Object -First 10)) {
                [Console]::Error.WriteLine("  $($runtimeError.Line)")
            }
            return 1
        }

        return 0
    }
    catch {
        [Console]::Error.WriteLine("Launcher failure: $($_.Exception.Message)")
        return 1
    }
    finally {
        if ($null -ne $gameEnvironmentSnapshot) {
            Restore-GameBackendEnvironment -Snapshot $gameEnvironmentSnapshot
        }
        if ($null -ne $parallelProfileRunDirectory) {
            if (-not $gameProcessStarted -or $gameProcessExited) {
                Remove-ParallelRuntimeProfile -RunLogDirectory $parallelProfileRunDirectory
            }
            else {
                Write-Warning (
                    "Parallel profile cleanup deferred because Godot is still running: '$parallelProfileRunDirectory'.")
            }
        }
        Stop-OwnedBackend -Process $ownedBackendProcess
        if ($ownsRuntimeMutex) {
            $runtimeMutex.ReleaseMutex()
        }
        if ($ownsPreparationMutex) {
            $preparationMutex.ReleaseMutex()
        }
        if ($null -ne $gameProcess) {
            $gameProcess.Dispose()
        }
        $runtimeMutex.Dispose()
        $preparationMutex.Dispose()
    }
}

if ($env:STEEL_TIDE_LAUNCHER_SELF_TEST -ceq '1') {
    $selfTestExitCode = Invoke-LauncherSelfTest
    exit $selfTestExitCode
}

$launcherExitCode = Invoke-OperationSteelTideLauncher -GameArguments $forwardedGameArguments
exit $launcherExitCode
