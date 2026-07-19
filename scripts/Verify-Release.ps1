param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $projectRoot 'artifacts'
$setup = Join-Path $artifacts "PetDesktop-Setup-$Version.exe"
$portable = Join-Path $artifacts "PetDesktop-Portable-$Version.zip"
$checksums = Join-Path $artifacts "SHA256SUMS-$Version.txt"

foreach ($path in @($setup, $portable, $checksums)) {
    if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Missing or empty release artifact: $path"
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($portable)
try {
    foreach ($entryName in @('.portable', 'PetDesktop.App.exe')) {
        if ($null -eq $archive.GetEntry($entryName)) {
            throw "Portable archive is missing $entryName"
        }
    }
    $petDirectoryEntries = @($archive.Entries | Where-Object { $_.FullName -match '^(?i:pets[/\\])$' })
    if ($petDirectoryEntries.Count -eq 0) {
        throw 'Portable archive is missing the empty pets folder.'
    }
    $petFileEntries = @($archive.Entries | Where-Object { $_.FullName -match '^(?i:pets[/\\]).+' })
    if ($petFileEntries.Count -ne 0) {
        throw 'Portable archive contains pet resources. Pets must be user-supplied.'
    }
}
finally {
    $archive.Dispose()
}

$expected = Get-Content -LiteralPath $checksums
foreach ($artifact in @($setup, $portable)) {
    $actual = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
    if ($null -eq ($expected | Where-Object { $_ -like "$actual *" })) {
        throw "Checksum manifest does not contain the current hash for $artifact"
    }
}

Write-Output "Release $Version verification passed."
