[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Id,
    [Parameter(Mandatory = $true)][string]$Title,
    [Parameter(Mandatory = $true)][string]$Message,
    [ValidateRange(1, 365)][int]$Days = 14
)

$ErrorActionPreference = 'Stop'
if ($Id.Length -gt 100 -or $Title.Length -gt 160 -or $Message.Length -gt 4000) { throw 'Announcement text exceeds the supported length.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $projectRoot 'release\publish\announcements'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$now = [DateTime]::UtcNow
$feed = @{ Schema = 1; Items = @(@{ Id = $Id; Title = $Title; Message = $Message; PublishedUtc = $now.ToString('o'); ExpiresUtc = $now.AddDays($Days).ToString('o') }) }
$feed | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'announcements.json') -Encoding UTF8
Write-Output ('Prepared only; no announcement sent: ' + (Join-Path $output 'announcements.json'))
