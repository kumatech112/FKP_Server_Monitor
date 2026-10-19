[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$developmentRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent $developmentRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw 'The Windows .NET Framework C# compiler is unavailable. Install .NET Framework 4.8 before building.'
}
$buildCacheRoot = Join-Path $projectRoot 'Archive\builds'
$buildRoot = Join-Path $buildCacheRoot ('build-' + [guid]::NewGuid().ToString('N'))
$programRoot = Join-Path $projectRoot 'Program'
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
try {
    $sources = Get-ChildItem -LiteralPath (Join-Path $developmentRoot 'src') -Filter '*.cs' -File
    foreach ($source in $sources) {
        Copy-Item -LiteralPath $source.FullName -Destination $buildRoot
    }
    Copy-Item -LiteralPath (Join-Path $developmentRoot 'src\app.manifest') -Destination $buildRoot
    Copy-Item -LiteralPath (Join-Path $developmentRoot 'assets\branding\server-tracker.ico') -Destination $buildRoot
    Copy-Item -LiteralPath (Join-Path $developmentRoot 'assets\branding\server-tracker.png') -Destination $buildRoot
    $executablePath = Join-Path $buildRoot 'FKPServerMonitor.exe'
    $arguments = @(
        '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warn:4', '/warnaserror+',
        '/utf8output', '/codepage:65001', ('/out:' + $executablePath),
        ('/win32manifest:' + (Join-Path $buildRoot 'app.manifest')),
        ('/win32icon:' + (Join-Path $buildRoot 'server-tracker.ico')),
        ('/resource:' + (Join-Path $buildRoot 'server-tracker.ico') + ',ServerBotTracker.Branding.Icon'),
        ('/resource:' + (Join-Path $buildRoot 'server-tracker.png') + ',ServerBotTracker.Branding.Logo'),
        '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll', '/reference:System.Runtime.Serialization.dll',
        '/reference:System.Net.Http.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll'
    )
    $arguments += @(Get-ChildItem -LiteralPath $buildRoot -Filter '*.cs' -File | ForEach-Object { $_.FullName })
    & $compilerPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }
    New-Item -ItemType Directory -Path $programRoot -Force | Out-Null
    Copy-Item -LiteralPath $executablePath -Destination (Join-Path $programRoot 'FKPServerMonitor.exe') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FKPServerMonitor.exe.config') -Destination $programRoot -Force
    Copy-Item -LiteralPath (Join-Path $developmentRoot 'docs\USAGE.th.md') -Destination (Join-Path $programRoot 'USER_GUIDE_TH.md') -Force
    $shortcutWriter = Join-Path $buildRoot 'CreateShortcut.exe'
    & $compilerPath /nologo /target:winexe /warn:4 /warnaserror+ /utf8output /codepage:65001 ('/out:' + $shortcutWriter) (Join-Path $PSScriptRoot 'launcher\CreateShortcut.cs') (Join-Path $PSScriptRoot 'setup\DesktopShortcut.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Shortcut writer compilation failed.' }
    $shortcutPath = Join-Path $projectRoot 'FKP Server Monitor.lnk'
    $shortcutArguments = '"' + $shortcutPath + '" "' + (Join-Path $programRoot 'FKPServerMonitor.exe') + '" "FKP Server Monitor"'
    $shortcutProcess = Start-Process -FilePath $shortcutWriter -ArgumentList $shortcutArguments -WindowStyle Hidden -PassThru -Wait
    if ($shortcutProcess.ExitCode -ne 0) { throw 'Root launcher creation failed.' }
    Write-Output ('Built: ' + (Join-Path $programRoot 'FKPServerMonitor.exe'))
    Get-FileHash -LiteralPath (Join-Path $programRoot 'FKPServerMonitor.exe') -Algorithm SHA256 | Select-Object Algorithm, Hash, Path
}
finally {
    $resolvedBuild = [IO.Path]::GetFullPath($buildRoot)
    $resolvedCache = [IO.Path]::GetFullPath($buildCacheRoot).TrimEnd('\') + '\'
    if (-not $resolvedBuild.StartsWith($resolvedCache, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing cleanup outside the development directory.'
    }
    if (Test-Path -LiteralPath $resolvedBuild) {
        Remove-Item -LiteralPath $resolvedBuild -Recurse -Force
    }
}
