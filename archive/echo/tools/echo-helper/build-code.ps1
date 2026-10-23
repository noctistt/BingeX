param([Parameter(Mandatory)][string]$JavaPath,[Parameter(Mandatory)][string]$EngineDirectory,[Parameter(Mandatory)][string]$CompilerJar,[Parameter(Mandatory)][string]$WorkDirectory,[Parameter(Mandatory)][string]$OutputZip)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $WorkDirectory){throw 'Choose a new work directory.'}
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
$classpath=(Get-ChildItem -LiteralPath $EngineDirectory -Filter '*.jar').FullName -join ';'
& $JavaPath -jar $CompilerJar -17 -cp $classpath -d $WorkDirectory (Join-Path $PSScriptRoot 'EchoMusicHelper.java')
if($LASTEXITCODE -ne 0){throw 'Original helper compilation failed.'}
$stream=[IO.File]::Create([IO.Path]::GetFullPath($OutputZip))
$zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
try{
    foreach($file in Get-ChildItem -LiteralPath $WorkDirectory -Filter 'EchoMusicHelper*.class' | Sort-Object Name){
        $entry=$zip.CreateEntry($file.Name,[IO.Compression.CompressionLevel]::NoCompression)
        $entry.LastWriteTime=[DateTimeOffset]::new(1980,1,1,0,0,0,[TimeSpan]::Zero)
        $output=$entry.Open();try{$bytes=[IO.File]::ReadAllBytes($file.FullName);$output.Write($bytes,0,$bytes.Length)}finally{$output.Dispose()}
    }
}finally{$zip.Dispose();$stream.Dispose()}
# Contains only compiled original BingeX code. No SDK, engine or provider classes are copied.
