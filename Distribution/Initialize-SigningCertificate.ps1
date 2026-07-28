[CmdletBinding()]
param(
    [string] $Subject = 'CN=yoqzii',
    [ValidateRange(1, 10)]
    [int] $ValidYears = 5,
    [string] $BackupPath = (
        Join-Path $env:LOCALAPPDATA `
            'SensorHUD\Signing\SensorHUD.pfx')
)

$ErrorActionPreference = 'Stop'

$existingCertificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $Subject -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date).AddDays(30) -and
        $_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3'
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($existingCertificate) {
    Write-Host (
        "A usable release certificate already exists: {0}" -f
        $existingCertificate.Thumbprint)
    exit 0
}

$backupDirectory = Split-Path -Parent $BackupPath
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null

Write-Host 'Creating the private release-signing certificate...'
$certificate = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -FriendlyName 'SensorHUD release signing' `
    -CertStoreLocation Cert:\CurrentUser\My `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears($ValidYears) `
    -TextExtension @(
        '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
        '2.5.29.19={text}ca=0')

$password = Read-Host `
    'Choose a strong password for the private PFX backup' `
    -AsSecureString

Export-PfxCertificate `
    -Cert $certificate `
    -FilePath $BackupPath `
    -Password $password `
    -CryptoAlgorithmOption AES256_SHA256 |
    Out-Null

Write-Host ''
Write-Host "Certificate thumbprint: $($certificate.Thumbprint)"
Write-Host "Private backup: $BackupPath"
Write-Host (
    'Keep the PFX and its password private. They are required to publish ' +
    'updates that Windows recognizes as the same publisher.')
