param(
    [Parameter(Mandatory)][string]$JavaPath,
    [Parameter(Mandatory)][string]$EngineDirectory,
    [Parameter(Mandatory)][string]$SdkJar,
    [Parameter(Mandatory)][string]$CompilerJar,
    [Parameter(Mandatory)][string]$ExtensionPath,
    [Parameter(Mandatory)][string]$WorkDirectory,
    [string]$DotnetPath='dotnet',
    [string]$PythonPath='python'
)
$ErrorActionPreference='Stop'
# Explicitly selected trusted runtime and the documented OpenSubsonic fixture candidate only.
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$WorkDirectory=[IO.Path]::GetFullPath($WorkDirectory)
New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null
$classes=Join-Path $WorkDirectory 'classes'
New-Item -ItemType Directory -Force -Path $classes | Out-Null
$classpath=(Resolve-Path -LiteralPath $SdkJar).Path+';'+((Get-ChildItem -LiteralPath $EngineDirectory -Filter '*.jar').FullName -join ';')
& $JavaPath -jar $CompilerJar -17 -cp $classpath -d $classes (Join-Path $PSScriptRoot 'EchoMusicHelper.java')
if($LASTEXITCODE -ne 0){throw 'Music helper compilation failed.'}
$project=Join-Path $root 'tests/BingeX.EchoProbe'
& $DotnetPath build $project -c Release
if($LASTEXITCODE -ne 0){throw 'Music helper integration build failed.'}
& $PythonPath (Join-Path $root 'tools/echo-probe/fixture-probe.py') --bridge --java $JavaPath --classpath "$classes;$classpath" --extension $ExtensionPath --converted (Join-Path $WorkDirectory 'unused-probe.jar') --entry dev.brahmkshatriya.echo.extension.OpenSubsonicExtension --output (Join-Path $WorkDirectory 'resolved-fixture.txt') --dotnet $DotnetPath --project $project --classes $classes --sdk (Resolve-Path -LiteralPath $SdkJar).Path --engine $EngineDirectory --cache (Join-Path $WorkDirectory 'cache')
if($LASTEXITCODE -ne 0){throw 'Music helper integration checks failed.'}
