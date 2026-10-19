[CmdletBinding()]
param([string]$Notes = 'Renamed to FKP Server Monitor, with a renamed installer, executable and default program folder. Existing server lists are preserved. Run the new Setup once to migrate older installations.')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = Join-Path $projectRoot 'release'
$executablePath = Join-Path $releaseRoot 'FKPServerMonitor.exe'
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executablePath).FileVersion
$versionValue = [version]$version
$tag = 'v' + $(if ($versionValue.Revision -gt 0) { $versionValue.ToString(4) } else { $versionValue.ToString(3) })
$outputRoot = Join-Path $releaseRoot ('publish\' + $tag)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$package = Join-Path $outputRoot 'FKPServerMonitor-portable.zip'
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Force }
Compress-Archive -LiteralPath @($executablePath, (Join-Path $releaseRoot 'FKPServerMonitor.exe.config')) -DestinationPath $package
$manifest = [ordered]@{ Schema = 1; Version = $version; Tag = $tag; Sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash; SizeBytes = (Get-Item -LiteralPath $package).Length; Notes = $Notes }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'update-fkp.json') -Encoding UTF8
# Older clients require the old filenames and product metadata. Keep their feed on
# the last compatible release instead of advertising a package they cannot load.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'legacy-update.json') -Destination (Join-Path $outputRoot 'update.json') -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$setup = Join-Path $outputRoot 'FKPServerMonitor-Setup.exe'
$brandingRoot = Join-Path $projectRoot 'assets\branding'
$arguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warn:4', '/warnaserror+',
    '/utf8output', '/codepage:65001', ('/out:' + $setup),
    ('/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')),
    ('/win32icon:' + (Join-Path $brandingRoot 'server-tracker.ico')),
    ('/resource:' + $package + ',payload.zip'),
    ('/resource:' + (Join-Path $brandingRoot 'server-tracker.ico') + ',ServerBotTracker.Branding.Icon'),
    ('/resource:' + (Join-Path $brandingRoot 'server-tracker.png') + ',ServerBotTracker.Branding.Logo'),
    '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll',
    (Join-Path $PSScriptRoot 'setup\Setup.cs'),
    (Join-Path $PSScriptRoot 'setup\SetupForm.cs'),
    (Join-Path $PSScriptRoot 'setup\InstallLocation.cs'),
    (Join-Path $PSScriptRoot 'setup\DesktopShortcut.cs'),
    (Join-Path $PSScriptRoot 'setup\UninstallTransaction.cs'),
    (Join-Path $PSScriptRoot 'setup\InstallationScope.cs'),
    (Join-Path $PSScriptRoot 'setup\RegistryBackup.cs'),
    (Join-Path $PSScriptRoot 'setup\SetupFiles.cs'),
    (Join-Path $projectRoot 'src\AssemblyInfo.cs'),
    (Join-Path $projectRoot 'src\ProductIdentity.cs'),
    (Join-Path $projectRoot 'src\Branding.cs'),
    (Join-Path $projectRoot 'src\ElevationBroker.cs'),
    (Join-Path $projectRoot 'src\Theme.cs')
)
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
Copy-Item -LiteralPath $setup -Destination (Join-Path $releaseRoot 'FKPServerMonitor-Setup.exe') -Force
Get-FileHash -LiteralPath $setup,$package -Algorithm SHA256 | Select-Object Hash,Path
Write-Output ('Prepared release only; nothing uploaded: ' + $outputRoot)
