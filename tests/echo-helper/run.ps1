param([Parameter(Mandatory)][string]$JavaPath,[Parameter(Mandatory)][string]$EngineDirectory,[Parameter(Mandatory)][string]$CompilerJar,[Parameter(Mandatory)][string]$HelperClasses,[Parameter(Mandatory)][string]$WorkDirectory)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $WorkDirectory){throw 'Choose a new isolated work directory.'}
New-Item -ItemType Directory -Path $WorkDirectory | Out-Null
$classpath=(([IO.Path]::GetFullPath($HelperClasses)),((Get-ChildItem -LiteralPath $EngineDirectory -Filter '*.jar').FullName -join ';')) -join ';'
& $JavaPath -jar $CompilerJar -17 -cp $classpath -d $WorkDirectory (Join-Path $PSScriptRoot 'LoaderRegression.java')
if($LASTEXITCODE -ne 0){throw 'Regression compilation failed.'}
& $JavaPath -cp (([IO.Path]::GetFullPath($WorkDirectory))+';'+$classpath) LoaderRegression (Join-Path $WorkDirectory 'fixture')
if($LASTEXITCODE -ne 0){throw 'Loader regression failed.'}
