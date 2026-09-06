[CmdletBinding()]
param([ValidateRange(0, 100)][double]$MinimumCoreCoverage = 80)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$results = Join-Path $root ('artifacts/tests/' + [Guid]::NewGuid().ToString('N'))
& dotnet test (Join-Path $root 'UsageDock.sln') -c Release --collect:'XPlat Code Coverage' --results-directory $results --logger 'trx' -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include='[UsageDock.Core]*' DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
$reports = @(Get-ChildItem -LiteralPath $results -Filter coverage.cobertura.xml -Recurse)
if ($reports.Count -eq 0) { throw 'No coverage report was produced.' }
$covered = 0L
$valid = 0L
foreach ($report in $reports) {
    [xml]$xml = Get-Content -LiteralPath $report.FullName -Raw
    foreach ($package in @($xml.coverage.packages.package)) {
        if ($package.name -ne 'UsageDock.Core') { continue }
        foreach ($line in @($package.classes.class.lines.line)) {
            $valid++
            if ([int]$line.hits -gt 0) { $covered++ }
        }
    }
}
if ($valid -eq 0) { throw 'UsageDock.Core coverage has no executable lines.' }
$percentage = 100.0 * $covered / $valid
Write-Host ('Core line coverage: {0:F2}% ({1}/{2}). Reports: {3}' -f $percentage, $covered, $valid, $results)
if ($percentage -lt $MinimumCoreCoverage) { throw "Core coverage is below $MinimumCoreCoverage%." }
