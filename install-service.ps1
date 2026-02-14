#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Installs ProPresenter-OBS Bridge as a Windows Service.
.DESCRIPTION
    Creates the Windows Service, sets automatic startup and failure recovery,
    registers the Event Log source, and starts the service.
.PARAMETER ExePath
    Path to the published ProPresenterObsBridge.exe. Defaults to .\publish\ProPresenterObsBridge.exe.
.PARAMETER ServiceName
    Name of the Windows Service. Defaults to ProPresenterObsBridge.
#>
param(
    [string]$ExePath = ".\publish\ProPresenterObsBridge.exe",
    [string]$ServiceName = "ProPresenterObsBridge"
)

$ErrorActionPreference = 'Stop'

$resolvedPath = Resolve-Path $ExePath -ErrorAction SilentlyContinue
if (-not $resolvedPath) {
    Write-Error "Executable not found at '$ExePath'. Run publish.ps1 first."
    exit 1
}

# Register Event Log source
$logSource = "ProPresenterObsBridge"
if (-not [System.Diagnostics.EventLog]::SourceExists($logSource)) {
    New-EventLog -LogName Application -Source $logSource
    Write-Host "Event Log source '$logSource' registered."
} else {
    Write-Host "Event Log source '$logSource' already exists."
}

# Create the service
Write-Host "Creating service '$ServiceName'..."
sc.exe create $ServiceName binPath= "$resolvedPath" start= auto displayname= "ProPresenter-OBS Bridge"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service. It may already exist. Use uninstall-service.ps1 first."
    exit 1
}

# Configure recovery: restart on first, second, and third failure (60s delay each)
Write-Host "Configuring failure recovery..."
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to set recovery options."
}

# Start the service
Write-Host "Starting service..."
sc.exe start $ServiceName
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Service did not start. Check Event Log for details."
} else {
    Write-Host "Service '$ServiceName' installed and started successfully."
}
