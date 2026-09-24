param([switch]$SkipBlender)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$hashes = Get-Content -LiteralPath "$root/assets/source/hashes.json" -Raw | ConvertFrom-Json
foreach ($property in $hashes.PSObject.Properties) {
    if ((Get-FileHash -LiteralPath "$root/assets/source/$($property.Name)").Hash -ne $property.Value) { throw "Source hash mismatch: $($property.Name)" }
}
New-Item -ItemType Directory -Force "$root/artifacts", "$root/assets/prepared", "$root/unity/Assets/Generated" | Out-Null
if (!$SkipBlender) {
    $blender = Join-Path $root '../forestcrawler/tools/blender-4.5.3-windows-x64/blender.exe'
    & $blender --background --python-exit-code 1 --python "$root/scripts/Prepare-Assets.py"
    if ($LASTEXITCODE -ne 0) { throw 'Blender preparation failed' }
    & $blender --background --python-exit-code 1 --python "$root/scripts/Prepare-Animation.py"
    if ($LASTEXITCODE -ne 0) { throw 'Blender animation preparation failed' }
}
Copy-Item -LiteralPath "$root/assets/source/throw_objekt.fbx" -Destination "$root/unity/Assets/Generated/ThrowSource.fbx"
$unity = 'C:/Program Files/Unity 6000.0.75f1/Editor/Unity.exe'
$log = "$root/artifacts/unity-build.log"
$arguments = @('-batchmode','-nographics','-quit','-projectPath',('"' + "$root/unity" + '"'),'-executeMethod','BolaBuild.Build','-logFile',('"' + $log + '"'))
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern 'BOLA_ASSETS_OK' -Quiet)) { throw "Asset validation failed: $log" }
Write-Output 'Bola prototype asset import and bundle validation passed.'
