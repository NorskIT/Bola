param([switch]$Assets)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
try {
    & "$PSScriptRoot/Verify-Inputs.ps1"
    if ($Assets) { & "$PSScriptRoot/Build-Assets.ps1" }
    & dotnet build "$root/Bola.sln" -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    & dotnet test "$root/tests/Bola.Core.Tests/Bola.Core.Tests.csproj" -c Release --no-build --logger 'trx;LogFileName=core.trx' --results-directory "$root/artifacts/tests"
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
} finally { Pop-Location }
