# Creates a local upload archive. Does not publish, install or deploy.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& "$PSScriptRoot/Build.ps1"
[xml]$props = Get-Content "$root/Directory.Build.props"
$version = [string]$props.Project.PropertyGroup.Version
$stage = Join-Path $root ('artifacts/upload-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force "$stage/BepInEx/plugins/Bola" | Out-Null
Copy-Item -LiteralPath "$root/src/Bola/bin/Release/net481/Bola.dll", "$root/src/Bola.Core/bin/Release/net481/Bola.Core.dll", "$root/artifacts/bundle/bola.prototype.assets" -Destination "$stage/BepInEx/plugins/Bola"
# Package pages need an absolute image URL; the repository README keeps its relative path.
$readme = [IO.File]::ReadAllText("$root/README.md").Replace('(images/bola-in-game.png)', '(https://raw.githubusercontent.com/NorskIT/Bola/main/images/bola-in-game.png)')
[IO.File]::WriteAllText("$stage/README.md", $readme)
@{
    name = 'Bola'; version_number = $version; website_url = 'https://github.com/NorskIT/Bola';
    description = 'Reusable bolas to bind creatures and ground flying enemies.';
    dependencies = @('denikson-BepInExPack_Valheim-5.4.2350','ValheimModding-Jotunn-2.30.2')
} | ConvertTo-Json | Set-Content -LiteralPath "$stage/manifest.json" -Encoding UTF8
# Mechanical package-thumbnail conversion; preserve the full-resolution source.
Add-Type -AssemblyName System.Drawing
$source = [Drawing.Image]::FromFile("$root/icon.png")
$thumbnail = New-Object Drawing.Bitmap 256,256
$graphics = [Drawing.Graphics]::FromImage($thumbnail)
try {
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($source,0,0,256,256)
    $thumbnail.Save("$stage/icon.png",[Drawing.Imaging.ImageFormat]::Png)
} finally { $graphics.Dispose(); $thumbnail.Dispose(); $source.Dispose() }
$zip = Join-Path $root "artifacts/Bola-$version.zip"
Compress-Archive -Path "$stage/*" -DestinationPath $zip -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $actual = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\','/') } | Where-Object { ! $_.EndsWith('/') } | Sort-Object)
    $expected = @('BepInEx/plugins/Bola/Bola.dll','BepInEx/plugins/Bola/Bola.Core.dll','BepInEx/plugins/Bola/bola.prototype.assets','README.md','manifest.json','icon.png') | Sort-Object
    if (Compare-Object $actual $expected) { throw 'Unexpected archive contents' }
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
"$hash  Bola-$version.zip" | Set-Content "$zip.sha256" -Encoding ASCII
Write-Output "Upload artifact: $zip"
Write-Output "SHA256: $hash"
