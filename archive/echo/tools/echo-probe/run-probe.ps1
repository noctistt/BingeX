param(
    [Parameter(Mandatory)][string]$JavaPath,
    [Parameter(Mandatory)][string]$EngineDirectory,
    [Parameter(Mandatory)][string]$SdkJar,
    [Parameter(Mandatory)][string]$CompilerJar,
    [Parameter(Mandatory)][string]$ExtensionPath,
    [Parameter(Mandatory)][string]$EntryClass,
    [Parameter(Mandatory)][string]$WorkDirectory,
    [string]$DotnetPath = 'dotnet',
    [string]$PythonPath = 'python'
)
$ErrorActionPreference='Stop'
# Explicitly supplied trusted code only. This helper is not a sandbox or app installer.
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$WorkDirectory=[IO.Path]::GetFullPath($WorkDirectory)
New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null
$classes=Join-Path $WorkDirectory 'classes'
New-Item -ItemType Directory -Force -Path $classes | Out-Null
$classpath=(Resolve-Path -LiteralPath $SdkJar).Path+';'+((Get-ChildItem -LiteralPath $EngineDirectory -Filter '*.jar').FullName -join ';')
& $JavaPath -jar $CompilerJar -17 -cp $classpath -d $classes (Join-Path $PSScriptRoot 'EchoProbe.java')
if($LASTEXITCODE -ne 0){throw 'Probe compilation failed.'}
$project=Join-Path $root 'tests/BingeX.EchoProbe'
& $DotnetPath build $project -c Release
if($LASTEXITCODE -ne 0){throw 'Audio probe build failed.'}
& $PythonPath (Join-Path $PSScriptRoot 'fixture-probe.py') --java $JavaPath --classpath "$classes;$classpath" --extension $ExtensionPath --converted (Join-Path $WorkDirectory 'converted.jar') --entry $EntryClass --output (Join-Path $WorkDirectory 'resolved-fixture.txt') --dotnet $DotnetPath --project $project
if($LASTEXITCODE -ne 0){throw 'Extension fixture probe failed.'}
