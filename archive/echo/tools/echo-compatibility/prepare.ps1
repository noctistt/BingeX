param(
 [Parameter(Mandatory)][string]$BaseRuntimeProfile,
 [Parameter(Mandatory)][string]$BridgeSourceRoot,
 [Parameter(Mandatory)][string]$KotlinCompilerClasspath,
 [Parameter(Mandatory)][string]$CompilerJar,
 [Parameter(Mandatory)][string]$ConverterDirectory,
 [Parameter(Mandatory)][string]$IcuJar,
 [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
# Local preparation only. No downloads, license acceptance, automatic installation, or app-profile changes.
$BaseRuntimeProfile=(Resolve-Path -LiteralPath $BaseRuntimeProfile).Path
$base=Get-Content -LiteralPath $BaseRuntimeProfile -Raw|ConvertFrom-Json
$baseDirectory=Split-Path -Parent $BaseRuntimeProfile
function Resolve-Base([string]$value){[IO.Path]::GetFullPath($value,$baseDirectory)}
$java=Resolve-Base $base.Java;$engine=Resolve-Base $base.Engine;$sdk=Resolve-Base $base.Sdk
$files=@{}
foreach($entry in $base.Files.PSObject.Properties){$path=Resolve-Base $entry.Name;if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value){throw 'Base runtime integrity check failed.'};$files[$path]=$entry.Value}
$lock=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'components.json') -Raw|ConvertFrom-Json
$BridgeSourceRoot=(Resolve-Path -LiteralPath $BridgeSourceRoot).Path
$revision=& git -C $BridgeSourceRoot rev-parse HEAD
if($LASTEXITCODE -ne 0 -or $revision.Trim() -ne $lock.revision){throw 'Choose the reviewed Dartotsu bridge snapshot.'}
foreach($entry in $lock.sources.PSObject.Properties){$text=[IO.File]::ReadAllText((Join-Path $BridgeSourceRoot $entry.Name)).Replace("`r`n","`n");$digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)));if($digest -ne $entry.Value){throw 'Upstream source differs from the reviewed snapshot.'}}
if((Get-FileHash -LiteralPath $IcuJar -Algorithm SHA256).Hash -ne $lock.icuSha256){throw 'Unexpected ICU dependency.'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new output directory.'}
$source=Join-Path $OutputDirectory 'sources';$classes=Join-Path $OutputDirectory 'classes';$libs=Join-Path $OutputDirectory 'lib'
New-Item -ItemType Directory -Path $source,$classes,$libs -Force|Out-Null
$util=Join-Path $BridgeSourceRoot 'runtimeManager/libraries/commonDesktopLib/src/main/kotlin/com/aayush262/dartotsu_extension_bridge/util'
Copy-Item -LiteralPath (Join-Path $util 'BytecodeEditor.kt'),(Join-Path $util 'DexNewInstance.kt') -Destination $source
$javaSource=Join-Path $BridgeSourceRoot 'runtimeManager/libraries/commonDesktopLib/src/main/java/xyz/nulldev/androidcompat/replace'
$javaFiles=Get-ChildItem -LiteralPath $javaSource -Filter '*.java' -Recurse
foreach($file in $javaFiles){$relative=[IO.Path]::GetRelativePath($javaSource,$file.FullName);$destination=Join-Path $source $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force|Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $destination}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Logger.kt'),(Join-Path $PSScriptRoot 'BingeXEchoCompatibility.java') -Destination $source
Copy-Item -LiteralPath (Join-Path $BridgeSourceRoot 'LICENSE') -Destination (Join-Path $source 'UPSTREAM-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../LICENSE') -Destination (Join-Path $source 'BINGEX-LICENSE.txt')
$converterFiles=Get-ChildItem -LiteralPath $ConverterDirectory -Filter '*-2.4.38.jar'|Sort-Object Name
if($converterFiles.Count -ne 10){throw 'Supply the reviewed ten dex2jar 2.4.38 dependency JARs.'}
foreach($file in $converterFiles){if($lock.jars.($file.Name) -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash){throw 'Unexpected converter dependency.'}}
foreach($file in @($converterFiles.FullName)+@((Resolve-Path -LiteralPath $IcuJar).Path)){Copy-Item -LiteralPath $file -Destination $libs}
$extras=(Get-ChildItem -LiteralPath $libs -Filter '*.jar'|Sort-Object Name).FullName
$classpath=$sdk+';'+((Get-ChildItem -LiteralPath $engine -Filter '*.jar').FullName -join ';')+';'+($extras -join ';')
& $java -Xmx1536m -cp $KotlinCompilerClasspath org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -jvm-target 17 -Xskip-metadata-version-check -classpath $classpath -d $classes (Join-Path $source 'BytecodeEditor.kt') (Join-Path $source 'DexNewInstance.kt') (Join-Path $source 'Logger.kt')
if($LASTEXITCODE -ne 0){throw 'Optional repair compilation failed.'}
$localJava=(Get-ChildItem -LiteralPath $source -Filter '*.java' -Recurse).FullName
& $java -jar $CompilerJar -17 -cp ($classes+';'+$classpath) -d $classes @localJava
if($LASTEXITCODE -ne 0){throw 'Optional adapter compilation failed.'}
$adapter=Join-Path $libs 'echo-compatibility.jar'
[IO.Compression.ZipFile]::CreateFromDirectory($classes,$adapter)
$compatibility=@($adapter)+@($extras)
foreach($file in $compatibility){$files[$file]=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}
@{Version=2;Java=$java;Classes=(Resolve-Base $base.Classes);Sdk=$sdk;Engine=$engine;Files=$files;Compatibility=@{Jars=$compatibility}}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'echo-runtime.json') -Encoding utf8
$sourceHashes=@{};foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File){$sourceHashes[[IO.Path]::GetRelativePath($source,$file.FullName)]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
@{UpstreamRevision=$revision.Trim();Sources=$sourceHashes;Description='Locally prepared optional component; custom upstream and per-file notices retained. Not approved for redistribution.'}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutputDirectory 'SOURCE-INFO.json') -Encoding utf8
Write-Output 'Prepared optional echo-runtime.json and retained source/notices. Select this profile in BingeX and explicitly enable Enhanced music compatibility.'
