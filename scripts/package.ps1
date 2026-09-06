[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.4.0',
    [string]$InnoSetupCompiler,
    [switch]$RequireInstaller
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root "artifacts/release/$Version"
$publish = Join-Path $output ('publish-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $publish -Force | Out-Null
& dotnet publish (Join-Path $root 'src/UsageDock.App/UsageDock.App.csproj') -c Release --artifacts-path (Join-Path $output 'build') -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$exe = Join-Path $publish 'UsageDock.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Published executable is missing.' }
Copy-Item -LiteralPath (Join-Path $root 'LICENSE'), (Join-Path $root 'README.md'), (Join-Path $root 'CONTRIBUTING.md'), (Join-Path $root 'SECURITY.md') -Destination $publish
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $publish 'docs') -Recurse
$zip = Join-Path $output "UsageDock-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -Force
$files = @($zip)
if (!$InnoSetupCompiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $InnoSetupCompiler = $command.Source }
    elseif (Test-Path -LiteralPath "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") {
        $InnoSetupCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    }
}
if ($InnoSetupCompiler) {
    & $InnoSetupCompiler "/DAppVersion=$Version" "/DPublishDir=$publish" "/DOutputDir=$output" (Join-Path $root 'packaging/UsageDock.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $installer = Join-Path $output "UsageDock-$Version-win-x64-setup.exe"
    if (!(Test-Path -LiteralPath $installer)) { throw 'Installer was not produced.' }
    $files += $installer
} elseif ($RequireInstaller) { throw 'Inno Setup 6 compiler is required; pass -InnoSetupCompiler <ISCC.exe>.' }
else { Write-Warning 'Portable ZIP created. Inno Setup compiler unavailable; installer was not built.' }
$hashes = foreach ($file in $files) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $file -Leaf)
}
$hashes | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Artifacts: $output"
