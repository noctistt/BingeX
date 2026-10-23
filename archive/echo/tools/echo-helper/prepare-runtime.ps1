param(
    [Parameter(Mandatory)][string]$JavaPath,
    [Parameter(Mandatory)][string]$EngineDirectory,
    [Parameter(Mandatory)][string]$SdkJar,
    [Parameter(Mandatory)][string]$CompilerJar,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
# Creates a local profile referring to explicitly supplied dependencies. Nothing is downloaded or redistributed.
$JavaPath=(Resolve-Path -LiteralPath $JavaPath).Path
$EngineDirectory=(Resolve-Path -LiteralPath $EngineDirectory).Path
$SdkJar=(Resolve-Path -LiteralPath $SdkJar).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new output folder.'}
$classes=Join-Path $OutputDirectory 'classes'
New-Item -ItemType Directory -Path $classes -Force | Out-Null
$jars=Get-ChildItem -LiteralPath $EngineDirectory -Filter '*.jar'
$classpath=$SdkJar+';'+($jars.FullName -join ';')
& $JavaPath -jar $CompilerJar -17 -cp $classpath -d $classes (Join-Path $PSScriptRoot 'EchoMusicHelper.java')
if($LASTEXITCODE -ne 0){throw 'Music helper compilation failed.'}
$files=@{}
foreach($file in @($JavaPath,$SdkJar)+@($jars.FullName)+@((Get-ChildItem -LiteralPath $classes -Filter 'EchoMusicHelper*.class').FullName)){
    $files[$file]=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
}
@{Version=1;Java=$JavaPath;Classes=$classes;Sdk=$SdkJar;Engine=$EngineDirectory;Files=$files} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'echo-runtime.json') -Encoding utf8
Write-Output 'Prepared local echo-runtime.json. Keep the referenced dependency folders in place.'
