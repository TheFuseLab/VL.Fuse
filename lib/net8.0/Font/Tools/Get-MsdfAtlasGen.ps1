param(
    [Parameter(Mandatory = $true)]
    [string]$GitHubReleaseZipUrl,

    [string]$OutputFolder = "$PSScriptRoot\msdf-atlas-gen"
)

$ErrorActionPreference = "Stop"

$tmpZip = Join-Path $env:TEMP ("msdf-atlas-gen-" + [Guid]::NewGuid().ToString("N") + ".zip")
$tmpDir = Join-Path $env:TEMP ("msdf-atlas-gen-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $tmpDir | Out-Null
    Invoke-WebRequest -Uri $GitHubReleaseZipUrl -OutFile $tmpZip
    Expand-Archive -Path $tmpZip -DestinationPath $tmpDir -Force

    $exe = Get-ChildItem -Path $tmpDir -Filter "msdf-atlas-gen.exe" -Recurse | Select-Object -First 1
    if (-not $exe) {
        throw "msdf-atlas-gen.exe not found in downloaded archive."
    }

    New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
    Get-ChildItem -Path $exe.Directory.FullName -File | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination (Join-Path $OutputFolder $_.Name) -Force
    }

    Write-Host "Installed msdf-atlas-gen to $OutputFolder"
}
finally {
    if (Test-Path $tmpZip) { Remove-Item $tmpZip -Force -ErrorAction SilentlyContinue }
    if (Test-Path $tmpDir) { Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue }
}
