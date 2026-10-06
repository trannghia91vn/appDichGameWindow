[CmdletBinding()]
param(
    [switch]$SkipZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solution = Join-Path $repoRoot "GameTranslator.sln"
$project = Join-Path $repoRoot "src\GameTranslator.App\GameTranslator.App.csproj"
$artifactsDirectory = Join-Path $repoRoot "artifacts"
$publishDirectory = Join-Path $artifactsDirectory "publish\win-x64"
$zipPath = Join-Path $artifactsDirectory "GameTranslator-win-x64.zip"
$localDotnet = Join-Path $repoRoot ".dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { "dotnet" }

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string]$Step,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host "`n== $Step ==" -ForegroundColor Cyan
    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

function Assert-ReleaseFile {
    param([Parameter(Mandatory)][string]$RelativePath)

    $path = Join-Path $publishDirectory $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required release file is missing: $RelativePath"
    }
}

$resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsDirectory)
$resolvedPublish = [System.IO.Path]::GetFullPath($publishDirectory)
$artifactsPrefix = $resolvedArtifacts.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $resolvedPublish.StartsWith(
        $artifactsPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a publish directory outside artifacts: $resolvedPublish"
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

Invoke-DotNet -Step "Restore" -Arguments @(
    "restore",
    $solution
)

Invoke-DotNet -Step "Release build" -Arguments @(
    "build",
    $solution,
    "-c", "Release",
    "--no-restore"
)

Invoke-DotNet -Step "Release tests" -Arguments @(
    "test",
    $solution,
    "-c", "Release",
    "--no-build"
)

Invoke-DotNet -Step "Self-contained win-x64 publish" -Arguments @(
    "publish",
    $project,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "--no-restore",
    "-o", $publishDirectory
)

$requiredFiles = @(
    "GameTranslator.exe",
    "GameTranslator.dll",
    "GameTranslator.deps.json",
    "GameTranslator.runtimeconfig.json",
    "GameTranslator.Core.dll",
    "RapidOcrNet.dll",
    "Microsoft.ML.OnnxRuntime.dll",
    "onnxruntime.dll",
    "onnxruntime_providers_shared.dll",
    "SkiaSharp.dll",
    "libSkiaSharp.dll",
    "coreclr.dll",
    "hostfxr.dll",
    "hostpolicy.dll",
    "PresentationFramework.dll",
    "models\v5\ch_PP-OCRv5_mobile_det.onnx",
    "models\v5\ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx",
    "models\v5\latin_PP-OCRv5_rec_mobile_infer.onnx",
    "models\v5\ppocrv5_latin_dict.txt",
    "RELEASE-README.md"
)

foreach ($relativePath in $requiredFiles) {
    Assert-ReleaseFile -RelativePath $relativePath
}

$exePath = Join-Path $publishDirectory "GameTranslator.exe"
$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
if ($versionInfo.FileVersion -ne "1.1.0.0" -or
    $versionInfo.ProductVersion -notlike "1.1.0*") {
    throw "Unexpected executable version: file=$($versionInfo.FileVersion), product=$($versionInfo.ProductVersion)"
}

$metadataFiles = Get-ChildItem -LiteralPath $publishDirectory -File |
    Where-Object { $_.Extension -in ".json", ".config", ".md" }
$forbiddenPatterns = @(
    [regex]::Escape($repoRoot),
    "\\.nuget[\\/]",
    "bin[\\/]Debug"
)

foreach ($file in $metadataFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($pattern in $forbiddenPatterns) {
        if ($content -match $pattern) {
            throw "Developer path reference found in $($file.Name): $pattern"
        }
    }
}

if (-not $SkipZip) {
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object FullName)
        if ($entryNames -notcontains "GameTranslator.exe" -or
            $entryNames -notcontains "RELEASE-README.md") {
            throw "Release ZIP does not contain the executable and README at its root."
        }

        $forbiddenEntries = @($entryNames | Where-Object {
            $_ -match "(^|/)(obj|tests)(/|$)" -or
            $_ -match "(^|/)bin/Debug(/|$)" -or
            $_ -match "\.(cs|csproj|sln)$"
        })
        if ($forbiddenEntries.Count -gt 0) {
            throw "Release ZIP contains developer files: $($forbiddenEntries -join ', ')"
        }
    }
    finally {
        $archive.Dispose()
    }
}

$publishBytes = (Get-ChildItem -LiteralPath $publishDirectory -Recurse -File |
    Measure-Object -Property Length -Sum).Sum
$publishSizeMb = $publishBytes / 1MB
$zipSizeMb = if (Test-Path -LiteralPath $zipPath) {
    (Get-Item -LiteralPath $zipPath).Length / 1MB
} else {
    0
}

Write-Host "`nRelease verified." -ForegroundColor Green
Write-Host "Publish: $publishDirectory"
Write-Host ("Publish size: {0:N1} MB" -f $publishSizeMb)
if (-not $SkipZip) {
    Write-Host "ZIP: $zipPath"
    Write-Host ("ZIP size: {0:N1} MB" -f $zipSizeMb)
}
