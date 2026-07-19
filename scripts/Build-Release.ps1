param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) {
    $localDotnet
}
else {
    (Get-Command dotnet -ErrorAction Stop).Source
}

$programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
$isccCandidates = @(
    (Join-Path $projectRoot '.tools\inno-7.0.2\ISCC.exe'),
    (Join-Path $programFilesX86 'Inno Setup 7\ISCC.exe'),
    (Join-Path $programFilesX86 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($null -eq $iscc) {
    throw 'Inno Setup 6 or 7 was not found. Install it, or provide the local .tools cache.'
}
$artifactsDir = Join-Path $projectRoot 'artifacts'
$publishDir = Join-Path $artifactsDir "publish-$Version"

New-Item -ItemType Directory -Path $publishDir, $artifactsDir -Force | Out-Null
& $dotnet publish (Join-Path $projectRoot 'src\PetDesktop.App\PetDesktop.App.csproj') -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:Version=$Version -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
New-Item -ItemType File -Path (Join-Path $publishDir '.portable') -Force | Out-Null
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath (Join-Path $artifactsDir "PetDesktop-Portable-$Version.zip") -Force
Remove-Item -LiteralPath (Join-Path $publishDir '.portable') -Force
& $iscc "/DMyAppVersion=$Version" "/DPublishDir=$publishDir" (Join-Path $projectRoot 'installer\PetDesktop.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
$checksumPath = Join-Path $artifactsDir "SHA256SUMS-$Version.txt"
Get-ChildItem -LiteralPath $artifactsDir -File | Where-Object { $_.FullName -ne $checksumPath } | Get-FileHash -Algorithm SHA256 | ForEach-Object {
    "{0}  {1}" -f $_.Hash, $_.Path
} | Set-Content -LiteralPath $checksumPath -Encoding utf8
