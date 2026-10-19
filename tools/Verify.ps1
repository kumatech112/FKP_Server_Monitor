[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$executablePath = Join-Path $projectRoot 'release\FKPServerMonitor.exe'
if (-not (Test-Path -LiteralPath $executablePath)) { throw 'Build the release before verifying it.' }
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$verificationRoot = Join-Path $env:LOCALAPPDATA ('Kuma\ServerBotTrackerDevelopment\verification-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $verificationRoot -Force | Out-Null
Copy-Item -LiteralPath $executablePath -Destination $verificationRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'release\FKPServerMonitor.exe.config') -Destination $verificationRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'release\FKPServerMonitor-Setup.exe') -Destination $verificationRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\branding\server-tracker.ico') -Destination (Join-Path $verificationRoot 'branding.ico')
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\branding\server-tracker.png') -Destination (Join-Path $verificationRoot 'branding.png')
$releaseVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath).FileVersion
$versionValue = [version]$releaseVersion
$versionTag = if ($versionValue.Revision -gt 0) { $versionValue.ToString(4) } else { $versionValue.ToString(3) }
$publishedRoot = Join-Path $projectRoot ('release\publish\v' + $versionTag)
if (Test-Path -LiteralPath (Join-Path $publishedRoot 'update-fkp.json')) {
    Copy-Item -LiteralPath (Join-Path $publishedRoot 'update-fkp.json') -Destination (Join-Path $verificationRoot 'published-update.json')
    Copy-Item -LiteralPath (Join-Path $publishedRoot 'update.json') -Destination (Join-Path $verificationRoot 'legacy-update.json')
    Copy-Item -LiteralPath (Join-Path $publishedRoot 'FKPServerMonitor-portable.zip') -Destination (Join-Path $verificationRoot 'published-package.zip')
}
$runnerPath = Join-Path $verificationRoot 'Verification.exe'
Copy-Item -LiteralPath (Join-Path $projectRoot 'release\FKPServerMonitor.exe.config') -Destination ($runnerPath + '.config')
$arguments = @(
    '/nologo', '/target:exe', '/platform:anycpu', '/warn:4', '/warnaserror+', '/utf8output', '/codepage:65001',
    ('/out:' + $runnerPath), ('/reference:' + (Join-Path $verificationRoot 'FKPServerMonitor.exe')),
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll', '/reference:System.Runtime.Serialization.dll',
    '/reference:System.Net.Http.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll', '/reference:System.Management.dll',
    (Join-Path $PSScriptRoot 'checks\Verification.cs'),
    (Join-Path $PSScriptRoot 'checks\SetupVerification.cs'),
    (Join-Path $PSScriptRoot 'checks\InstallationScopeVerification.cs'),
    (Join-Path $PSScriptRoot 'checks\RenameVerification.cs'),
    (Join-Path $PSScriptRoot 'setup\DesktopShortcut.cs'),
    (Join-Path $PSScriptRoot 'checks\UpdatesVerification.cs')
)
$fixtureSource = Join-Path $verificationRoot 'OldVersion.cs'
@'
using System.Reflection;
[assembly: AssemblyProduct("FKP Server Monitor")]
[assembly: AssemblyFileVersion("0.9.0.0")]
internal static class OldVersion { private static void Main() { System.Threading.Thread.Sleep(1000); } }
'@ | Set-Content -LiteralPath $fixtureSource -Encoding UTF8
& $compilerPath /nologo /target:winexe ('/out:' + (Join-Path $verificationRoot 'OldVersion.exe')) $fixtureSource
if ($LASTEXITCODE -ne 0) { throw 'Old-version fixture compilation failed.' }
$legacyFixtureSource = Join-Path $verificationRoot 'LegacyVersion.cs'
@'
using System.Reflection;
[assembly: AssemblyProduct("Server Bot Tracker")]
[assembly: AssemblyFileVersion("1.6.0.0")]
internal static class LegacyVersion { private static void Main() { } }
'@ | Set-Content -LiteralPath $legacyFixtureSource -Encoding UTF8
& $compilerPath /nologo /target:winexe ('/out:' + (Join-Path $verificationRoot 'LegacyVersion.exe')) $legacyFixtureSource
if ($LASTEXITCODE -ne 0) { throw 'Legacy-name fixture compilation failed.' }
& $compilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw 'Verification runner compilation failed.' }
& $runnerPath $verificationRoot
if ($LASTEXITCODE -ne 0) { throw "Verification failed. Evidence: $verificationRoot" }
$sourceHash = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash
$verifiedHash = (Get-FileHash -LiteralPath (Join-Path $verificationRoot 'FKPServerMonitor.exe') -Algorithm SHA256).Hash
if ($sourceHash -ne $verifiedHash) { throw 'The tested executable differs from the release.' }
$setupHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot 'release\FKPServerMonitor-Setup.exe') -Algorithm SHA256).Hash
$verifiedSetupHash = (Get-FileHash -LiteralPath (Join-Path $verificationRoot 'FKPServerMonitor-Setup.exe') -Algorithm SHA256).Hash
if ($setupHash -ne $verifiedSetupHash) { throw 'The tested installer differs from the release.' }
Write-Output ('Verified release SHA256: ' + $sourceHash)
Write-Output ('Verified installer SHA256: ' + $setupHash)
Write-Output ('Evidence outside project: ' + $verificationRoot)
