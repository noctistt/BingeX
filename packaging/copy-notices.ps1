param([Parameter(Mandatory)][string]$PublishDirectory)
$ErrorActionPreference = 'Stop'
$assets = Get-Content (Join-Path $PSScriptRoot '../src/BingeX/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
$licenseDirectory = Join-Path $PublishDirectory 'licenses'
New-Item -ItemType Directory -Force -Path $licenseDirectory | Out-Null
foreach ($entry in $assets.libraries.GetEnumerator()) {
    if ($entry.Value.type -ne 'package') { continue }
    $packageDirectory = $null
    foreach ($packageRoot in $assets.packageFolders.Keys) {
        $candidate = Join-Path $packageRoot $entry.Value.path
        if (Test-Path -LiteralPath $candidate) { $packageDirectory = $candidate; break }
    }
    if (!$packageDirectory) { throw "Missing resolved package: $($entry.Key)" }
    $destination = Join-Path $licenseDirectory ($entry.Key -replace '/', '-')
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object { $_.Name -match 'LICENSE|COPYING|NOTICE|\.nuspec$' } | Copy-Item -Destination $destination
}
Copy-Item (Join-Path $PSScriptRoot '../THIRD_PARTY_NOTICES.md') $PublishDirectory
# Include the upstream license texts checked into this packaging directory.

Copy-Item -Path (Join-Path $PSScriptRoot 'licenses/*') -Destination $licenseDirectory

# Include the project grant and distribution guidance with every future package.
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach ($name in @('LICENSE', 'COPYRIGHT.md', 'docs/licensing.md', 'docs/dependency-licenses.json')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $name) -Destination $PublishDirectory
}
# Capture tracked working files, rather than assuming HEAD matches the binary being built.
$tracked = @(& git -C $repositoryRoot -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0 -or !$tracked.Count) { throw 'Cannot capture matching tracked source.' }
foreach ($required in @('LICENSE', 'COPYRIGHT.md', 'docs/licensing.md', 'tools/cloudstream-patch/AudioPatch.java')) {
    if ($tracked -notcontains $required) { throw "Stage the required source file before packaging: $required" }
}
$sourceZip = Join-Path $PublishDirectory 'BingeX-source.zip'
if (Test-Path -LiteralPath $sourceZip) { Remove-Item -LiteralPath $sourceZip }
$archive = [IO.Compression.ZipFile]::Open($sourceZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($relative in $tracked) {
        $source = Join-Path $repositoryRoot $relative
        if (!(Test-Path -LiteralPath $source -PathType Leaf)) { continue }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, $relative.Replace('\','/'), [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$revision = & git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine source revision.' }
@"
BingeX original work: GPL-3.0-or-later. See LICENSE and COPYRIGHT.md.
Repository: https://github.com/noctistt/BingeX
Base revision: $revision
BingeX-source.zip contains tracked working files captured at packaging time; local edits may differ from the base revision.
Build: .NET SDK version from global.json; dotnet build src/BingeX/BingeX.csproj -c Release.
Windows package: packaging/build-windows.ps1. Java patch: tools/cloudstream-patch/README.md.
Third-party licenses remain separate. See THIRD_PARTY_NOTICES.md and licensing.md.
This snapshot does not include complete native dependency or optional-runtime corresponding source.
"@ | Set-Content -LiteralPath (Join-Path $PublishDirectory 'SOURCE-INFO.txt') -Encoding utf8
