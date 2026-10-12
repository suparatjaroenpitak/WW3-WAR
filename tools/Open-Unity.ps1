[CmdletBinding()]
param([string]$UnityPath)

$ErrorActionPreference = 'Stop'
$projectDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$versionFile = Join-Path $projectDirectory 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
    throw "ProjectVersion.txt is missing. Run this script from the tools folder of the ww3 war project."
}
$versionMatch = [regex]::Match((Get-Content -LiteralPath $versionFile -Raw), 'm_EditorVersion:\s*(\S+)')
if (-not $versionMatch.Success) { throw "Cannot read the required Unity version from $versionFile" }
$requiredVersion = $versionMatch.Groups[1].Value

if ($UnityPath) {
    if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
        throw "Unity Editor executable was not found: $UnityPath. Supply -UnityPath with the full path to Editor\Unity.exe. No installation was attempted."
    }
    $editorExecutable = (Resolve-Path -LiteralPath $UnityPath).Path
    if ([IO.Path]::GetFileName($editorExecutable) -ine 'Unity.exe') {
        throw 'UnityPath must point to the Unity Editor executable named Unity.exe, not Unity Hub or the Unity CLI.'
    }
} else {
    $candidatePaths = @()
    if ($env:ProgramFiles) {
        $candidatePaths += Join-Path $env:ProgramFiles "Unity\Hub\Editor\$requiredVersion\Editor\Unity.exe"
    }
    if ($env:LOCALAPPDATA) {
        $candidatePaths += Join-Path $env:LOCALAPPDATA "Programs\Unity\Hub\Editor\$requiredVersion\Editor\Unity.exe"
    }
    $editorExecutable = $candidatePaths | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $editorExecutable) {
        throw "Unity Editor $requiredVersion is not installed in a standard location. Unity Hub/CLI alone cannot open this project. When an Editor is available, pass -UnityPath 'D:\YourUnityFolder\Editor\Unity.exe', or add this project in Unity Hub. This script does not install or update anything."
    }
}

Write-Host "Opening ww3 war with $editorExecutable"
Write-Host "Project: $projectDirectory (target version: $requiredVersion)"
# Opening an interactive Editor is the explicit purpose of this launcher.
Start-Process -FilePath $editorExecutable -ArgumentList @('-projectPath', ('"' + $projectDirectory + '"')) -WindowStyle Normal
