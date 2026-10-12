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
        throw "Unity Editor $requiredVersion is not installed in a standard location. Unity Hub/CLI alone cannot build the game. When an Editor is available, pass -UnityPath 'D:\YourUnityFolder\Editor\Unity.exe'. This script does not install or update anything."
    }
}

$logDirectory = Join-Path $projectDirectory 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory 'build-windows.log'
Write-Host "Building ww3 war for Windows x64 with $editorExecutable"
Write-Host "Close this project in the interactive Editor before running the batch build. Log: $logPath"
$arguments = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', ('"' + $projectDirectory + '"'),
    '-buildTarget', 'Win64',
    '-executeMethod', 'WW3War.Editor.BuildCommands.BuildWindows',
    '-logFile', ('"' + $logPath + '"')
)
$buildProcess = Start-Process -FilePath $editorExecutable -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($buildProcess.ExitCode -ne 0) {
    throw "Unity build failed with exit code $($buildProcess.ExitCode). Read $logPath."
}
$executablePath = Join-Path $projectDirectory 'Builds\Windows\ww3 war.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Unity exited without an error, but the expected build was not found: $executablePath. Read $logPath."
}
Write-Host "Build completed: $executablePath"
Write-Host 'Distribute the complete Builds\Windows folder, including the executable, data folder, and runtime files.'
