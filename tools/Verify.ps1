[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$executablePath = Join-Path $projectRoot 'release\ServerBotTracker.exe'
if (-not (Test-Path -LiteralPath $executablePath)) { throw 'Build the release before verifying it.' }
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$verificationRoot = Join-Path $env:LOCALAPPDATA ('Kuma\ServerBotTrackerDevelopment\verification-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
Copy-Item -LiteralPath $executablePath -Destination $verificationRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'release\ServerBotTracker.exe.config') -Destination $verificationRoot
$runnerPath = Join-Path $verificationRoot 'Verification.exe'
$arguments = @(
    '/nologo', '/target:exe', '/platform:anycpu', '/warn:4', '/warnaserror+', '/utf8output',
    ('/out:' + $runnerPath), ('/reference:' + (Join-Path $verificationRoot 'ServerBotTracker.exe')),
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Runtime.Serialization.dll',
    (Join-Path $PSScriptRoot 'checks\Verification.cs')
)
& $compilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Verification runner compilation failed.' }
& $runnerPath $verificationRoot
if ($LASTEXITCODE -ne 0) { throw "Verification failed. Evidence: $verificationRoot" }
$sourceHash = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash
$verifiedHash = (Get-FileHash -LiteralPath (Join-Path $verificationRoot 'ServerBotTracker.exe') -Algorithm SHA256).Hash
if ($sourceHash -ne $verifiedHash) { throw 'The tested executable differs from the release.' }
Write-Output ('Verified release SHA256: ' + $sourceHash)
Write-Output ('Evidence outside project: ' + $verificationRoot)
