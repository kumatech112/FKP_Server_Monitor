[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CertificateThumbprint,
    [Parameter(Mandatory = $true)][string[]]$Files
)

$ErrorActionPreference = 'Stop'
$certificate = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $CertificateThumbprint)
if (-not $certificate.HasPrivateKey) { throw 'The code-signing certificate has no private key.' }
foreach ($file in $Files) {
    $result = Set-AuthenticodeSignature -LiteralPath $file -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com'
    if ($result.Status -ne 'Valid') { throw ('Signing failed: ' + $result.StatusMessage) }
}
