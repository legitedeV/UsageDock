[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& dotnet restore (Join-Path $root 'UsageDock.sln')
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
& dotnet build (Join-Path $root 'UsageDock.sln') -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
