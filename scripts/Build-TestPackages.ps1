# Produces local review artifacts only. Never installs, publishes or deploys.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& "$PSScriptRoot/Build.ps1"
& dotnet build "$root/../no-ranged-public/src/NoRanged/NoRanged.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'No Ranged compilation failed' }
& dotnet test "$root/../no-ranged-public/tests/NoRanged.Tests/NoRanged.Tests.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'No Ranged regression failed' }
$stage = Join-Path $root ('artifacts/packages-' + [Guid]::NewGuid().ToString('N'))
$bola = Join-Path $stage 'Bola-0.2.1-LOCAL-TEST'
$ranged = Join-Path $stage 'No_Ranged-1.2.2-TEST'
New-Item -ItemType Directory -Force "$bola/plugins/Bola", "$ranged/plugins/NoRanged" | Out-Null
Copy-Item -LiteralPath "$root/src/Bola/bin/Release/net481/Bola.dll", "$root/src/Bola.Core/bin/Release/net481/Bola.Core.dll", "$root/artifacts/bundle/bola.prototype.assets" -Destination "$bola/plugins/Bola"
Copy-Item -LiteralPath "$root/README.md", "$root/IMPLEMENTATION.md", "$root/VERIFICATION.md" -Destination $bola
Copy-Item -LiteralPath "$root/icon.png" -Destination "$bola/icon.png"
Copy-Item -LiteralPath "$root/images" -Destination "$bola/images" -Recurse
Copy-Item -LiteralPath "$root/../no-ranged-public/src/NoRanged/bin/Release/net481/NoRanged.dll" -Destination "$ranged/plugins/NoRanged"
foreach ($name in @('README.md','CHANGELOG.md','LICENSE','BOLA-VERIFICATION.md')) {
    Copy-Item -LiteralPath "$root/../no-ranged-public/$name" -Destination $ranged
}
Copy-Item -LiteralPath "$root/../no-ranged-public/src/NoRanged/Assets/icon.png" -Destination "$ranged/icon.png"
@{
    name = 'No_Ranged'; version_number = '1.2.2'; website_url = 'https://github.com/NorskIT/No-ranged';
    description = 'Unpublished test build: optional server-controlled Bola exception.';
    dependencies = @('denikson-BepInExPack_Valheim-5.4.2350','ValheimModding-Jotunn-2.30.2')
} | ConvertTo-Json | Set-Content -LiteralPath "$ranged/manifest.json" -Encoding UTF8
foreach ($folder in @($bola,$ranged)) {
    $files = Get-ChildItem -LiteralPath $folder -Recurse -File
    $forbidden = $files | Where-Object { $_.Name -match '^(assembly_|UnityEngine|BepInEx|0Harmony|Jotunn)' -or $_.Extension -in @('.fbx','.glb') }
    if ($forbidden) { throw 'Dependency or raw source asset leaked into test package' }
    $hashes = $files | ForEach-Object { [PSCustomObject]@{Path=$_.FullName.Substring($folder.Length+1);SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash} }
    $hashes | ConvertTo-Json | Set-Content -LiteralPath "$folder/SHA256.json" -Encoding UTF8
    Compress-Archive -Path "$folder/*" -DestinationPath "$folder.zip"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead("$folder.zip")
    try {
        if ($archive.Entries.Count -ne ($files.Count+1)) { throw 'Archive entry count mismatch' }
    } finally { $archive.Dispose() }
}
Write-Output "Local test archives: $stage"


