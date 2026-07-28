[CmdletBinding()]
param(
    [switch] $Uninstall,
    [ValidateSet('Install', 'Remove')]
    [string] $CertificateAction,
    [string] $CertificatePath,
    [string] $ExpectedThumbprint
)

$ErrorActionPreference = 'Stop'
$packageName = 'yoqzii.SensorHUD'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-PublicCertificate {
    param([Parameter(Mandatory)][string] $Path)

    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "The certificate was not found at '$Path'."
    }

    return [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $Path)
}

function Invoke-ElevatedCertificateAction {
    param(
        [Parameter(Mandatory)][ValidateSet('Install', 'Remove')]
        [string] $Action,
        [Parameter(Mandatory)][string] $Path,
        [Parameter(Mandatory)][string] $Thumbprint
    )

    $windowsPowerShell = Join-Path $env:SystemRoot `
        'System32\WindowsPowerShell\v1.0\powershell.exe'
    $arguments = (
        '-NoLogo -NoProfile -ExecutionPolicy Bypass ' +
        "-File `"$PSCommandPath`" " +
        "-CertificateAction $Action " +
        "-CertificatePath `"$Path`" " +
        "-ExpectedThumbprint $Thumbprint")

    $process = Start-Process `
        -FilePath $windowsPowerShell `
        -ArgumentList $arguments `
        -Verb RunAs `
        -Wait `
        -PassThru

    if ($process.ExitCode -ne 0) {
        throw "Administrator certificate action failed ($($process.ExitCode))."
    }
}

if ($CertificateAction) {
    if (!(Test-IsAdministrator)) {
        throw 'Administrator privileges are required to manage trust.'
    }

    $certificate = Get-PublicCertificate -Path $CertificatePath
    if ($certificate.Thumbprint -ne $ExpectedThumbprint) {
        throw 'The certificate thumbprint does not match the expected value.'
    }

    $trustedPath = "Cert:\LocalMachine\TrustedPeople\$ExpectedThumbprint"
    if ($CertificateAction -eq 'Install') {
        if (!(Test-Path -LiteralPath $trustedPath)) {
            Import-Certificate `
                -FilePath $CertificatePath `
                -CertStoreLocation Cert:\LocalMachine\TrustedPeople |
                Out-Null
        }
    }
    elseif (Test-Path -LiteralPath $trustedPath) {
        Remove-Item -LiteralPath $trustedPath -Force
    }

    exit 0
}

$releaseDirectory = Split-Path -Parent $PSCommandPath
$certificatePath = Join-Path $releaseDirectory 'SensorHUD.cer'
$certificate = Get-PublicCertificate -Path $certificatePath

if ($certificate.Subject -ne 'CN=yoqzii') {
    throw "Unexpected certificate publisher '$($certificate.Subject)'."
}

if ($Uninstall) {
    Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue |
        Remove-AppxPackage

    Invoke-ElevatedCertificateAction `
        -Action Remove `
        -Path $certificatePath `
        -Thumbprint $certificate.Thumbprint

    Write-Host (
        'SensorHUD and its trusted certificate were removed. ' +
        'PawnIO was left installed because other applications may use it.')
    exit 0
}

if (![Environment]::Is64BitOperatingSystem) {
    throw 'SensorHUD 0.0.1 requires 64-bit Windows.'
}

$packages = @(
    Get-ChildItem `
        -LiteralPath $releaseDirectory `
        -File |
        Where-Object { $_.Extension -in @('.msix', '.appx') })

if ($packages.Count -ne 1) {
    throw 'The release must contain exactly one MSIX or APPX package.'
}

$signature = Get-AuthenticodeSignature -LiteralPath $packages[0].FullName
if (!$signature.SignerCertificate -or
    $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'The application package and certificate do not match.'
}

$trustedPath = "Cert:\LocalMachine\TrustedPeople\$($certificate.Thumbprint)"
if (!(Test-Path -LiteralPath $trustedPath)) {
    Write-Host 'Windows will request permission to trust the yoqzii certificate.'
    Invoke-ElevatedCertificateAction `
        -Action Install `
        -Path $certificatePath `
        -Thumbprint $certificate.Thumbprint
}

$installParameters = @{
    Path = $packages[0].FullName
    ForceApplicationShutdown = $true
}
$dependencyDirectory = Join-Path $releaseDirectory 'Dependencies'
if (Test-Path -LiteralPath $dependencyDirectory -PathType Container) {
    $dependencyPaths = @(
        Get-ChildItem `
            -LiteralPath $dependencyDirectory `
            -File `
            -Filter '*.appx' |
            Select-Object -ExpandProperty FullName)
    if ($dependencyPaths.Count -gt 0) {
        $installParameters.DependencyPath = $dependencyPaths
    }
}

Add-AppxPackage @installParameters

$installedPackage = Get-AppxPackage -Name $packageName
if (!$installedPackage) {
    throw 'Windows did not report the application as installed.'
}

if (!(Get-AppxPackage -Name 'Microsoft.XboxGamingOverlay')) {
    Write-Warning (
        'Xbox Game Bar is not installed for this user. Install it before ' +
        'trying to open the widget.')
}

Write-Host ''
Write-Host "Installed SensorHUD $($installedPackage.Version)."
Write-Host 'Press Win+G and select SensorHUD from the widget menu.'
