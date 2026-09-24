$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sdk = & dotnet --version
if ($sdk -ne '10.0.400') { throw "Expected .NET SDK 10.0.400, found $sdk" }
$source = Get-Content -LiteralPath "$root/assets/source/hashes.json" -Raw | ConvertFrom-Json
foreach ($entry in $source.PSObject.Properties) {
    if ((Get-FileHash -LiteralPath "$root/assets/source/$($entry.Name)").Hash -ne $entry.Value) { throw "Source changed: $($entry.Name)" }
}
foreach ($entry in (Get-Content -LiteralPath "$root/dependencies.lock.json" -Raw | ConvertFrom-Json)) {
    if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Sha256) { throw "Pinned dependency changed: $($entry.Path). Revalidate the new installation before updating the lock." }
}
Write-Output 'SDK, original source copies and installed dependency hashes match.'
