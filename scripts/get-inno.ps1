[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tools = Join-Path $root 'artifacts/tools'
New-Item -ItemType Directory -Path $tools -Force | Out-Null
$installer = Join-Path $tools 'innosetup-6.7.3.exe'
$compilerDir = Join-Path $tools 'inno'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $installer
$expected = '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732'
if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $expected) { throw 'Inno Setup download hash mismatch.' }
$signature = Get-AuthenticodeSignature -LiteralPath $installer
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )O=Pyrsys B\.V\.(,|$)') { throw 'Inno Setup publisher signature is invalid.' }
$process = Start-Process -FilePath $installer -ArgumentList @('/PORTABLE=1','/VERYSILENT','/CURRENTUSER','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="' + $compilerDir + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw 'Portable compiler extraction failed.' }
$compiler = Join-Path $compilerDir 'ISCC.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Compiler was not extracted.' }
Write-Output $compiler
