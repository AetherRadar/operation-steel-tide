<#
.SYNOPSIS
    Local assistant for the Tripo Studio + Godot Bridge workflow.

.DESCRIPTION
    This helper deliberately does not call Tripo's private web endpoints, read
    browser cookies, or store account credentials. It prepares a local task
    manifest, opens the Studio page, and archives models delivered by the
    official Godot Bridge into the project.

    Supported commands: prepare, open, import, scan, list, validate.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet("prepare", "open", "import", "scan", "list", "validate")]
    [string]$CommandName,

    [string]$Prompt,
    [string]$ReferenceImage,
    [string]$AssetName,
    [string]$Source,
    [string]$TaskId,
    [string]$Notes,
    [ValidateSet("shape", "textured_pbr", "textured", "multiview")]
    [string]$Pipeline = "textured_pbr",
    [string]$StudioUrl = "https://tripo3d.uk/workspace/generate",
    [switch]$OpenAfterPrepare
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot
$taskRoot = Join-Path $projectRoot "build\tripo_studio\tasks"
$inboxRoot = Join-Path $projectRoot "assets\tripo_inbox"
$modelRoot = Join-Path $projectRoot "assets\models\tripo"

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $parentPath = Split-Path -Parent $Path
    if ($parentPath -and -not (Test-Path -LiteralPath $parentPath)) {
        New-Item -ItemType Directory -Path $parentPath -Force | Out-Null
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Ensure-Directory {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Resolve-ExistingFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $candidate = $Path
    if (-not [IO.Path]::IsPathRooted($candidate)) {
        $candidate = Join-Path $projectRoot $candidate
    }
    $resolved = Resolve-Path -LiteralPath $candidate -ErrorAction Stop
    if (-not (Test-Path -LiteralPath $resolved.Path -PathType Leaf)) {
        throw "File not found: $Path"
    }
    return $resolved.Path
}

function ConvertTo-Slug {
    param([Parameter(Mandatory = $true)][string]$Value)

    $slug = $Value.Trim().ToLowerInvariant()
    $slug = [regex]::Replace($slug, "[^a-z0-9]+", "-")
    $slug = $slug.Trim("-")
    if ([string]::IsNullOrWhiteSpace($slug)) {
        return "asset"
    }
    return $slug.Substring(0, [Math]::Min($slug.Length, 64))
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-JsonText {
    param([Parameter(Mandatory = $true)][object]$Value)
    return ($Value | ConvertTo-Json -Depth 8)
}

function Get-TaskManifests {
    if (-not (Test-Path -LiteralPath $taskRoot)) {
        return @()
    }
    return @(Get-ChildItem -LiteralPath $taskRoot -Filter "task.json" -File -Recurse | Sort-Object LastWriteTimeUtc -Descending)
}

function Invoke-Prepare {
    if ([string]::IsNullOrWhiteSpace($Prompt)) {
        throw "-Prompt is required for prepare."
    }
    if ([string]::IsNullOrWhiteSpace($ReferenceImage)) {
        throw "-ReferenceImage is required for prepare."
    }

    $referencePath = Resolve-ExistingFile -Path $ReferenceImage
    $extension = [IO.Path]::GetExtension($referencePath).ToLowerInvariant()
    if ($extension -notin @(".png", ".jpg", ".jpeg", ".webp")) {
        throw "Reference image must be PNG, JPG, JPEG, or WEBP: $referencePath"
    }

    $safeAssetName = if ([string]::IsNullOrWhiteSpace($AssetName)) {
        [IO.Path]::GetFileNameWithoutExtension($referencePath)
    } else {
        $AssetName
    }
    $slug = ConvertTo-Slug -Value $safeAssetName
    $stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
    $taskKey = "$stamp-$slug"
    $taskDirectory = Join-Path $taskRoot $taskKey
    Ensure-Directory -Path $taskDirectory

    $referenceCopy = Join-Path $taskDirectory ("reference" + $extension)
    Copy-Item -LiteralPath $referencePath -Destination $referenceCopy -Force

    $manifest = [ordered]@{
        schema = "steel-tide.tripo-studio-task.v1"
        id = $taskKey
        asset_name = $safeAssetName
        asset_slug = $slug
        pipeline = $Pipeline
        prompt = $Prompt.Trim()
        notes = if ($null -eq $Notes) { "" } else { $Notes.Trim() }
        reference_image = $referenceCopy
        reference_sha256 = Get-Sha256 -Path $referenceCopy
        studio_url = $StudioUrl
        godot_inbox = $inboxRoot
        created_utc = (Get-Date).ToUniversalTime().ToString("o")
        usage = "Generate in Tripo Studio, export with the official Godot Bridge, then run import with the delivered GLB."
    }
    $manifestPath = Join-Path $taskDirectory "task.json"
    Write-Utf8NoBom -Path $manifestPath -Content (ConvertTo-JsonText -Value $manifest)

    Write-Output ("TRIPO_STUDIO_TASK id={0} manifest={1} reference={2}" -f $taskKey, $manifestPath, $referenceCopy)
    Write-Output "TRIPO_STUDIO_NEXT Open Studio, upload the reference image, use the task prompt, and export through the official Godot Bridge."

    if ($OpenAfterPrepare) {
        Start-Process -FilePath $StudioUrl | Out-Null
        Write-Output ("TRIPO_STUDIO_OPEN url={0}" -f $StudioUrl)
    }
}

function Invoke-Open {
    Start-Process -FilePath $StudioUrl | Out-Null
    Write-Output ("TRIPO_STUDIO_OPEN url={0}" -f $StudioUrl)
}

function Invoke-Import {
    if ([string]::IsNullOrWhiteSpace($Source)) {
        throw "-Source is required for import."
    }

    $sourcePath = Resolve-ExistingFile -Path $Source
    $extension = [IO.Path]::GetExtension($sourcePath).ToLowerInvariant()
    if ($extension -notin @(".glb", ".gltf", ".fbx", ".obj")) {
        throw "Supported model formats are GLB, GLTF, FBX, and OBJ: $sourcePath"
    }

    $safeAssetName = if ([string]::IsNullOrWhiteSpace($AssetName)) {
        [IO.Path]::GetFileNameWithoutExtension($sourcePath)
    } else {
        $AssetName
    }
    $slug = ConvertTo-Slug -Value $safeAssetName
    $destinationDirectory = Join-Path $modelRoot $slug
    Ensure-Directory -Path $destinationDirectory
    $destinationPath = Join-Path $destinationDirectory ("model" + $extension)
    if (Test-Path -LiteralPath $destinationPath) {
        throw "Destination already exists; choose a new -AssetName or archive it first: $destinationPath"
    }

    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath

    $metadata = [ordered]@{
        schema = "steel-tide.tripo-studio-asset.v1"
        asset_name = $safeAssetName
        asset_slug = $slug
        model = $destinationPath
        source = $sourcePath
        source_sha256 = Get-Sha256 -Path $sourcePath
        imported_utc = (Get-Date).ToUniversalTime().ToString("o")
        task_id = if ($null -eq $TaskId) { "" } else { $TaskId }
        pipeline = $Pipeline
        license_note = "Verify Tripo plan rights and input-image rights before distribution."
        godot_note = "Keep the generated source file immutable; make project-specific changes in an inherited scene or extracted material."
    }
    $metadataPath = Join-Path $destinationDirectory "tripo_asset.json"
    Write-Utf8NoBom -Path $metadataPath -Content (ConvertTo-JsonText -Value $metadata)

    Write-Output ("TRIPO_STUDIO_IMPORT asset={0} model={1} metadata={2}" -f $slug, $destinationPath, $metadataPath)
    Write-Output "TRIPO_STUDIO_NEXT Godot will import the model from res://assets/models/tripo/. Inspect scale, materials, collision, and LOD before use."
}

function Invoke-Scan {
    Ensure-Directory -Path $inboxRoot
    $models = @(Get-ChildItem -LiteralPath $inboxRoot -File -Recurse | Where-Object {
        $_.Extension.ToLowerInvariant() -in @(".glb", ".gltf", ".fbx", ".obj")
    } | Sort-Object FullName)

    Write-Output ("TRIPO_STUDIO_SCAN inbox={0} count={1}" -f $inboxRoot, $models.Count)
    foreach ($model in $models) {
        Write-Output ("TRIPO_STUDIO_CANDIDATE file={0} bytes={1} sha256={2}" -f $model.FullName, $model.Length, (Get-Sha256 -Path $model.FullName))
    }
}

function Invoke-List {
    $manifests = @(Get-TaskManifests)
    Write-Output ("TRIPO_STUDIO_LIST count={0}" -f $manifests.Count)
    foreach ($manifestFile in $manifests) {
        $data = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
        Write-Output ("TRIPO_STUDIO_TASK_SUMMARY id={0} asset={1} pipeline={2} manifest={3}" -f $data.id, $data.asset_name, $data.pipeline, $manifestFile.FullName)
    }
}

function Invoke-Validate {
    $valid = $true
    $reasons = New-Object System.Collections.Generic.List[string]
    if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) {
        $valid = $false
        $reasons.Add("project root missing")
    }

    $manifests = @(Get-TaskManifests)
    foreach ($manifestFile in $manifests) {
        try {
            $data = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
            if (-not (Test-Path -LiteralPath $data.reference_image -PathType Leaf)) {
                $valid = $false
                $reasons.Add("missing reference for $($data.id)")
            } elseif ((Get-Sha256 -Path $data.reference_image) -ne $data.reference_sha256) {
                $valid = $false
                $reasons.Add("reference hash changed for $($data.id)")
            }
        } catch {
            $valid = $false
            $reasons.Add("invalid manifest $($manifestFile.FullName)")
        }
    }

    Write-Output ("TRIPO_STUDIO_CHECK tasks={0} reasons={1}" -f $manifests.Count, $reasons.Count)
    foreach ($reason in $reasons) {
        Write-Output ("TRIPO_STUDIO_REASON {0}" -f $reason)
    }
    Write-Output ("TRIPO_STUDIO_PASS valid={0}" -f $valid.ToString().ToLowerInvariant())
    if (-not $valid) {
        exit 2
    }
}

switch ($CommandName) {
    "prepare" { Invoke-Prepare }
    "open" { Invoke-Open }
    "import" { Invoke-Import }
    "scan" { Invoke-Scan }
    "list" { Invoke-List }
    "validate" { Invoke-Validate }
}
