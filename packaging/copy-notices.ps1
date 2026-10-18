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
