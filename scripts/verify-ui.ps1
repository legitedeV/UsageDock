[CmdletBinding()]
param([string]$Executable)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$Executable) { $Executable = Join-Path $root 'src/UsageDock.App/bin/Release/net8.0-windows/UsageDock.exe' }
if (!(Test-Path -LiteralPath $Executable)) { throw 'Build the application first or supply -Executable.' }
$output = Join-Path $root ('artifacts/ui/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$report = Join-Path $output 'smoke.txt'
foreach ($arguments in @(@('--demo', '--smoke-test', ('"' + $report + '"')), @('--demo', '--screenshot', ('"' + $output + '"')))) {
    $process = Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(30000)) {
        Stop-Process -Id $process.Id -Force
        throw 'UI verification timed out.'
    }
    if ($process.ExitCode -ne 0) { if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }; throw 'UI verification process failed.' }
}
if (!(Test-Path -LiteralPath $report)) { throw 'Smoke report missing.' }
$reportText = Get-Content -LiteralPath $report -Raw
if ([string]::IsNullOrWhiteSpace($reportText) -or $reportText -match 'FAIL:') { throw 'Smoke report is empty or contains a failure.' }
$checks = @([regex]::Matches($reportText, '(?m)^PASS: (?![0-9]+ checks;).+'))
$summary = [regex]::Match($reportText, '(?m)^PASS: ([0-9]+) checks;')
if (!$summary.Success -or [int]$summary.Groups[1].Value -lt 100 -or $checks.Count -ne [int]$summary.Groups[1].Value) { throw 'Smoke report lacks the expected passing checks.' }
foreach ($image in @('dashboard-dark.png','widget-dark.png','widget-light.png','connection-editor.png','dashboard-light.png','dashboard-minimum.png','main-client.png','widget-client.png','statistics-dark.png','history-dark.png','settings-dark.png','statistics-light.png','history-light.png','settings-light.png','statistics-minimum.png','history-minimum.png','settings-minimum.png','statistics-unavailable.png','statistics-empty.png','history-empty.png','settings-invalid.png','accounts-longnames.png','reset-inventory.png','reset-inventory-light.png','reset-inventory-unavailable.png')) {
    $path = Join-Path $output $image
    if (!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -lt 100) { throw "Missing render: $image" }
}
Add-Type -AssemblyName System.Drawing
foreach ($target in @(@{ Name = 'main-client.png'; Width = 1128; Height = 756 }, @{ Name = 'widget-client.png'; Width = 268; Height = 548 }, @{ Name = 'widget-light.png'; Width = 268; Height = 548 }, @{ Name = 'statistics-dark.png'; Width = 1128; Height = 756 }, @{ Name = 'settings-light.png'; Width = 1128; Height = 756 }, @{ Name = 'statistics-minimum.png'; Width = 960; Height = 580 }, @{ Name = 'settings-minimum.png'; Width = 960; Height = 580 })) {
    $render = [System.Drawing.Image]::FromFile((Join-Path $output $target.Name))
    try {
        if ($render.Width -ne $target.Width -or $render.Height -ne $target.Height) {
            throw "Unexpected client render dimensions: $($target.Name) is $($render.Width)x$($render.Height)."
        }
    } finally { $render.Dispose() }
}
Get-Content -LiteralPath $report
Write-Host "UI artifacts: $output"
