<#
.SYNOPSIS
    Builds the release binaries and the MSI installer without Visual Studio.
.DESCRIPTION
    Needs the .NET SDK. The WiX 3 toolset is taken from the WIX environment variable
    or downloaded from nuget.org into .tools on the first run.
#>
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'artifacts'

function Get-WixTools {
    if ($env:WIX -and (Test-Path (Join-Path $env:WIX 'bin\candle.exe'))) { return (Join-Path $env:WIX 'bin') }
    $tools = Join-Path $root '.tools\wix'
    if (-not (Test-Path (Join-Path $tools 'tools\candle.exe'))) {
        Write-Host 'Downloading WiX 3.14 toolset...'
        New-Item -ItemType Directory -Force $tools | Out-Null
        $zip = Join-Path $tools 'wix.zip'
        Invoke-WebRequest 'https://www.nuget.org/api/v2/package/wix/3.14.1' -OutFile $zip
        Expand-Archive $zip $tools -Force
        Remove-Item $zip
    }
    return (Join-Path $tools 'tools')
}

foreach ($project in 'PlexService\PlexService.csproj', 'PlexServiceTray\PlexServiceTray.csproj') {
    dotnet build (Join-Path $root $project) -c $Configuration -nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Build of $project failed" }
}

$version = ([xml](Get-Content (Join-Path $root 'PlexServiceInstaller\Product.wxs'))).Wix.Product.Version
$wix = Get-WixTools
$installer = Join-Path $root 'PlexServiceInstaller'
$obj = Join-Path $installer "obj\$Configuration"
New-Item -ItemType Directory -Force $obj, $out | Out-Null

$defines = @(
    "-dSolutionDir=$root\",
    "-dPlexService.TargetPath=$root\PlexService\bin\$Configuration\PlexService.exe",
    "-dPlexService.TargetDir=$root\PlexService\bin\$Configuration\",
    "-dPlexServiceWCF.TargetPath=$root\PlexServiceWCF\bin\$Configuration\PlexServiceWCF.dll",
    "-dPlexServiceCommon.TargetPath=$root\PlexServiceCommon\bin\$Configuration\PlexServiceCommon.dll",
    "-dPlexServiceCommon.TargetDir=$root\PlexServiceCommon\bin\$Configuration\",
    "-dPlexServiceTray.TargetPath=$root\PlexServiceTray\bin\$Configuration\PlexServiceTray.exe",
    "-dPlexServiceTray.TargetDir=$root\PlexServiceTray\bin\$Configuration\"
)
$extensions = @(
    '-ext', (Join-Path $installer 'WixCommonUiExtension.dll'),
    '-ext', (Join-Path $wix 'WixNetFxExtension.dll'),
    '-ext', (Join-Path $wix 'WixUIExtension.dll'),
    '-ext', (Join-Path $wix 'WixUtilExtension.dll')
)

& (Join-Path $wix 'candle.exe') -nologo -arch x86 -out "$obj\" @defines @extensions (Join-Path $installer 'Product.wxs')
if ($LASTEXITCODE -ne 0) { throw 'candle failed' }

$msi = Join-Path $out "PlexService_$version.msi"
& (Join-Path $wix 'light.exe') -nologo -spdb -sice:ICE61 -out $msi @extensions (Join-Path $obj 'Product.wixobj')
if ($LASTEXITCODE -ne 0) { throw 'light failed' }

Write-Host "Installer: $msi"
