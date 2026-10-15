[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw 'The Windows .NET Framework C# compiler is unavailable. Install .NET Framework 4.8 before building.'
}
$developmentRoot = Join-Path $env:LOCALAPPDATA 'Kuma\ServerBotTrackerDevelopment'
$buildRoot = Join-Path $developmentRoot ('build-' + [guid]::NewGuid().ToString('N'))
$releaseRoot = Join-Path $projectRoot 'release'
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
try {
    $sources = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File
    foreach ($source in $sources) {
        Copy-Item -LiteralPath $source.FullName -Destination $buildRoot
    }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\app.manifest') -Destination $buildRoot
    $executablePath = Join-Path $buildRoot 'ServerBotTracker.exe'
    $arguments = @(
        '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warn:4', '/warnaserror+',
        '/utf8output', '/codepage:65001', ('/out:' + $executablePath),
        ('/win32manifest:' + (Join-Path $buildRoot 'app.manifest')),
        '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll', '/reference:System.Runtime.Serialization.dll',
        '/reference:System.Net.Http.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll'
    )
    $arguments += @(Get-ChildItem -LiteralPath $buildRoot -Filter '*.cs' -File | ForEach-Object { $_.FullName })
    & $compilerPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
    Copy-Item -LiteralPath $executablePath -Destination (Join-Path $releaseRoot 'ServerBotTracker.exe') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ServerBotTracker.exe.config') -Destination $releaseRoot -Force
    Write-Output ('Built: ' + (Join-Path $releaseRoot 'ServerBotTracker.exe'))
    Get-FileHash -LiteralPath (Join-Path $releaseRoot 'ServerBotTracker.exe') -Algorithm SHA256 | Select-Object Algorithm, Hash, Path
}
finally {
    $resolvedBuild = [IO.Path]::GetFullPath($buildRoot)
    $resolvedDevelopment = [IO.Path]::GetFullPath($developmentRoot).TrimEnd('\') + '\'
    if (-not $resolvedBuild.StartsWith($resolvedDevelopment, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing cleanup outside the development directory.'
    }
    if (Test-Path -LiteralPath $resolvedBuild) {
        Remove-Item -LiteralPath $resolvedBuild -Recurse -Force
    }
}
