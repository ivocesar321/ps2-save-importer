<#
  Compila o PS2 Save Importer.

  Uso:
    .\build.ps1             -> compila em Release (App\bin\Release\net8.0-windows)
    .\build.ps1 -Publish    -> gera a pasta "dist" pronta para distribuir (executável único, não exige .NET instalado)
    .\build.ps1 -Test       -> compila e roda os testes automatizados (tools\SelfTest)
#>
param(
    [switch]$Publish,
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'App\Ps2SaveImporter.csproj'

dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Publish) {
    $dist = Join-Path $root 'dist'
    if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
    dotnet publish $project -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $dist
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "`nPronto: $dist\PS2SaveImporter.exe (MyMC embutido; arquivo único para distribuir)"
}

if ($Test) {
    $selfTest = Join-Path $root 'tools\SelfTest\SelfTest.csproj'
    $work = Join-Path $env:TEMP 'ps2si-selftest'
    dotnet build $selfTest -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $exe = Join-Path $root 'tools\SelfTest\bin\Release\net8.0-windows\Ps2SaveImporter.SelfTest.exe'
    & $exe logic --mymc (Join-Path $root 'App\mymc.exe') --work $work
    exit $LASTEXITCODE
}
