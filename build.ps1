<#
.SYNOPSIS
  Builds, tests and packages DRIVELY.
.EXAMPLE
  .\build.ps1                 # build + test
  .\build.ps1 -Publish        # also create a self-contained x64 release in .\artifacts
#>
param([switch]$Publish, [string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet build Drively.sln -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test Drively.sln -c $Configuration --no-build -nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Publish) {
    $out = Join-Path $PSScriptRoot "artifacts\DRIVELY-win-x64"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish src\Drively.App\Drively.App.csproj -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o $out -nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Copy-Item LICENSE, THIRD-PARTY-NOTICES.md, README.md $out
    $zip = Join-Path $PSScriptRoot "artifacts\DRIVELY-win-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path "$out\*" -DestinationPath $zip
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash
    "$hash  DRIVELY-win-x64.zip" | Set-Content (Join-Path $PSScriptRoot "artifacts\SHA256SUMS.txt")
    Write-Host "Release package: $zip"
    Write-Host "SHA-256: $hash"
}
