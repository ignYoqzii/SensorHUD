[CmdletBinding()]
param(
    [ValidateSet('Release')]
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..'))
$project = Join-Path `
    $repositoryRoot 'SensorHUD\SensorHUD.csproj'
$manifestPath = Join-Path `
    $repositoryRoot 'SensorHUD\Package.appxmanifest'
$buildPropertiesPath = Join-Path $repositoryRoot 'Directory.Build.props'
$collectorManifestPath = Join-Path `
    $repositoryRoot 'SensorHUD.Collector\app.manifest'
$installerSource = Join-Path $PSScriptRoot 'Installer'
$artifactsRoot = [IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot 'artifacts'))

if (!$artifactsRoot.StartsWith(
        $repositoryRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The artifacts directory resolved outside the repository.'
}

[xml] $manifest = Get-Content -LiteralPath $manifestPath
$packageVersion = [version] $manifest.Package.Identity.Version
$releaseVersion = (
    '{0}.{1}.{2}' -f
    $packageVersion.Major,
    $packageVersion.Minor,
    $packageVersion.Build)
$publisher = $manifest.Package.Identity.Publisher

[xml] $buildProperties = Get-Content -LiteralPath $buildPropertiesPath
$sourceVersion = $buildProperties.SelectSingleNode(
    '/Project/PropertyGroup/Version'
).InnerText
[xml] $collectorManifest = Get-Content -LiteralPath $collectorManifestPath
$collectorVersion = [string] $collectorManifest.SelectSingleNode(
    "/*[local-name()='assembly']/*[local-name()='assemblyIdentity']"
).version

if ($sourceVersion -ne $releaseVersion -or
    $collectorVersion -ne $packageVersion.ToString()) {
    throw (
        'Version metadata is inconsistent. Directory.Build.props, ' +
        'Package.appxmanifest, and the collector app.manifest must describe ' +
        'the same release.')
}

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $publisher -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date).AddDays(30) -and
        $_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3'
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (!$certificate) {
    throw (
        "No usable '$publisher' code-signing certificate was found in " +
        'Cert:\CurrentUser\My. Run ' +
        '.\Distribution\Initialize-SigningCertificate.ps1 first.')
}

$vsWhere = Join-Path ${env:ProgramFiles(x86)} `
    'Microsoft Visual Studio\Installer\vswhere.exe'
if (!(Test-Path -LiteralPath $vsWhere)) {
    throw 'Visual Studio Installer (vswhere.exe) was not found.'
}

$installations = & $vsWhere `
    -all `
    -products '*' `
    -requires Microsoft.Component.MSBuild `
    -format json |
    ConvertFrom-Json

if (!$installations) {
    throw 'A Visual Studio installation containing MSBuild was not found.'
}

$installation = $installations |
    Sort-Object `
        @{ Expression = {
            if ($_.productId -eq 'Microsoft.VisualStudio.Product.BuildTools') {
                1
            }
            else {
                0
            }
        } },
        @{ Expression = { [version]$_.installationVersion };
            Descending = $true } |
    Select-Object -First 1

$msBuild = Join-Path $installation.installationPath `
    'MSBuild\Current\Bin\amd64\MSBuild.exe'
if (!(Test-Path -LiteralPath $msBuild)) {
    throw "64-bit MSBuild was not found at '$msBuild'."
}

$windowsKitBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$signTool = Get-ChildItem -LiteralPath $windowsKitBin -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object {
        Join-Path $_.FullName 'x64\signtool.exe'
    } |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if (!$signTool) {
    throw 'The Windows SDK x64 signtool.exe was not found.'
}

$packageWorkDirectory = Join-Path $artifactsRoot 'package-work'
$releaseDirectory = Join-Path `
    $artifactsRoot "SensorHUD-$releaseVersion-x64"
$archivePath = "$releaseDirectory.zip"

foreach ($path in @(
        $packageWorkDirectory,
        $releaseDirectory,
        $archivePath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $packageWorkDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

Write-Host "Building SensorHUD $releaseVersion for x64..."
& $msBuild `
    $project `
    /restore `
    /t:_GenerateAppxPackage `
    "/p:Configuration=$Configuration" `
    /p:Platform=x64 `
    /p:AppxBundle=Never `
    /p:UapAppxPackageBuildMode=SideloadOnly `
    /p:AppxPackageSigningEnabled=true `
    "/p:PackageCertificateThumbprint=$($certificate.Thumbprint)" `
    "/p:CollectorSigningThumbprint=$($certificate.Thumbprint)" `
    "/p:CollectorSignToolPath=$signTool" `
    "/p:AppxPackageDir=$packageWorkDirectory" `
    /nologo `
    /v:minimal

if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE."
}

$builtPackage = Get-ChildItem `
    -LiteralPath $packageWorkDirectory `
    -Recurse `
    -File |
    Where-Object {
        $_.Name -like 'SensorHUD_*.msix' -and
        $_.FullName -notmatch '\\Dependencies\\'
    } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (!$builtPackage) {
    throw 'MSBuild completed without producing an MSIX or APPX package.'
}

$releasePackage = Join-Path `
    $releaseDirectory "SensorHUD-$releaseVersion-x64.msix"
Copy-Item -LiteralPath $builtPackage.FullName -Destination $releasePackage

$generatedDependencies = Join-Path `
    $builtPackage.Directory.FullName 'Dependencies\x64'
if (Test-Path -LiteralPath $generatedDependencies -PathType Container) {
    $releaseDependencies = Join-Path $releaseDirectory 'Dependencies'
    New-Item `
        -ItemType Directory `
        -Path $releaseDependencies `
        -Force |
        Out-Null
    Get-ChildItem `
        -LiteralPath $generatedDependencies `
        -File `
        -Filter '*.appx' |
        Copy-Item -Destination $releaseDependencies
}

$publicCertificate = Join-Path `
    $releaseDirectory 'SensorHUD.cer'
Export-Certificate `
    -Cert $certificate `
    -FilePath $publicCertificate `
    -Type CERT |
    Out-Null

Copy-Item `
    -LiteralPath (Join-Path $installerSource 'Setup.ps1') `
    -Destination $releaseDirectory
Copy-Item `
    -LiteralPath (Join-Path $installerSource 'Install.cmd') `
    -Destination $releaseDirectory
Copy-Item `
    -LiteralPath (Join-Path $installerSource 'Uninstall.cmd') `
    -Destination $releaseDirectory

$packageSignature = Get-AuthenticodeSignature -LiteralPath $releasePackage
if (!$packageSignature.SignerCertificate -or
    $packageSignature.SignerCertificate.Thumbprint -ne
        $certificate.Thumbprint) {
    throw 'The generated package was not signed by the release certificate.'
}

Compress-Archive `
    -Path (Join-Path $releaseDirectory '*') `
    -DestinationPath $archivePath `
    -CompressionLevel Optimal

Write-Host ''
Write-Host "Release archive: $archivePath"
Write-Host "Certificate: $($certificate.Thumbprint)"
Write-Host "SHA-256: $((Get-FileHash $archivePath -Algorithm SHA256).Hash)"
