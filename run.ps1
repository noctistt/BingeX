param([string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
& $DotnetPath run --project (Join-Path $PSScriptRoot 'src/BingeX/BingeX.csproj')
if ($LASTEXITCODE -ne 0) { throw 'BingeX could not start. Check the build output above.' }
