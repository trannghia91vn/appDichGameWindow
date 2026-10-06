[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$source = [System.IO.Path]::GetFullPath(
    (Join-Path $repoRoot "artifacts\publish\win-x64"))
$programsRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $env:LOCALAPPDATA "Programs"))
$installDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $programsRoot "GameTranslator"))
$programsPrefix = $programsRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

if (-not $installDirectory.StartsWith(
        $programsPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to install outside the per-user Programs directory: $installDirectory"
}

$sourceExecutable = Join-Path $source "GameTranslator.exe"
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
    throw "Release output is missing. Run .\scripts\publish-win-x64.ps1 first."
}

Get-Process GameTranslator -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $source "*") `
    -Destination $installDirectory `
    -Recurse `
    -Force

$installedExecutable = Join-Path $installDirectory "GameTranslator.exe"
$shell = New-Object -ComObject WScript.Shell

function Set-GameTranslatorShortcut {
    param([Parameter(Mandatory)][string]$Path)

    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = $installedExecutable
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$installedExecutable,0"
    $shortcut.Description = "GameTranslator - Dich game Anh Viet"
    $shortcut.Save()
}

$desktopShortcut = Join-Path `
    ([Environment]::GetFolderPath("Desktop")) `
    "GameTranslator.lnk"
Set-GameTranslatorShortcut -Path $desktopShortcut

$startMenuDirectory = Join-Path `
    ([Environment]::GetFolderPath("Programs")) `
    "GameTranslator"
New-Item -ItemType Directory -Path $startMenuDirectory -Force | Out-Null
$startMenuShortcut = Join-Path $startMenuDirectory "GameTranslator.lnk"
Set-GameTranslatorShortcut -Path $startMenuShortcut

$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExecutable)
Write-Host "GameTranslator installed." -ForegroundColor Green
Write-Host "Executable: $installedExecutable"
Write-Host "Version: $($version.FileVersion)"
Write-Host "Desktop shortcut: $desktopShortcut"
Write-Host "Start Menu shortcut: $startMenuShortcut"
