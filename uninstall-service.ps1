#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Uninstalls the ProPresenter-OBS Bridge Windows Service.
.DESCRIPTION
    Stops the service if running, deletes it, and removes the Event Log source.
.PARAMETER ServiceName
    Name of the Windows Service. Defaults to ProPresenterObsBridge.
#>
param(
    [string]$ServiceName = "ProPresenterObsBridge"
)

$ErrorActionPreference = 'Stop'

# Stop the service if running
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq 'Running') {
    Write-Host "Stopping service '$ServiceName'..."
    sc.exe stop $ServiceName | Out-Null
    Start-Sleep -Seconds 3
}

# Delete the service
Write-Host "Deleting service '$ServiceName'..."
sc.exe delete $ServiceName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to delete service. It may not exist."
    exit 1
}
Write-Host "Service '$ServiceName' deleted."

# Remove Event Log source
$logSource = "ProPresenterObsBridge"
if ([System.Diagnostics.EventLog]::SourceExists($logSource)) {
    Remove-EventLog -Source $logSource
    Write-Host "Event Log source '$logSource' removed."
} else {
    Write-Host "Event Log source '$logSource' not found (already removed)."
}

Write-Host "Uninstall complete."
