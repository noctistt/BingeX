param(
    [string]$DotnetPath = 'dotnet',
    [string]$IsccPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts'),
    [switch]$PortableOnly
)
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$publishDirectory = Join-Path $OutputDirectory 'win-x64'
& $DotnetPath publish (Join-Path $PSScriptRoot '../src/BingeX/BingeX.csproj') -c Release -r win-x64 --self-contained true -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'BingeX publish failed.' }
& (Join-Path $PSScriptRoot 'copy-notices.ps1') -PublishDirectory $publishDirectory
Copy-Item (Join-Path $PSScriptRoot '../README.md') $publishDirectory
if (!$PortableOnly) {
    if (!(Test-Path -LiteralPath $IsccPath)) { throw 'Install Inno Setup 6 or pass -IsccPath to ISCC.exe.' }
    & $IsccPath "/DSourceDir=$publishDirectory" "/DOutputDir=$OutputDirectory" (Join-Path $PSScriptRoot 'BingeX.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
}
