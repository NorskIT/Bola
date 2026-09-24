param([string]$Name = ('runtime-' + (Get-Date -Format 'yyyyMMdd-HHmmss')), [switch]$NoRanged, [switch]$Gameplay, [switch]$Interactive)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Name -notmatch '^[a-zA-Z0-9-]+$') { throw 'Invalid fixture name' }
if (Get-Process valheim -ErrorAction SilentlyContinue) { throw 'Valheim is already running; fixture must be isolated.' }
$run = Join-Path $root "artifacts/$Name"
if (Test-Path -LiteralPath $run) { throw 'Use a new fixture name' }
& dotnet build "$root/tests/Bola.RuntimeSmoke/Bola.RuntimeSmoke.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Fixture build failed' }
if ($NoRanged) {
    & dotnet build "$root/../no-ranged-public/src/NoRanged/NoRanged.csproj" -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'No Ranged build failed' }
}
$bep = Join-Path $env:APPDATA 'com.kesomannen.gale/valheim/profiles/Prod-v1/BepInEx'
New-Item -ItemType Directory -Force "$run/BepInEx/core", "$run/BepInEx/plugins/Bola", "$run/BepInEx/plugins/Jotunn", "$run/saves" | Out-Null
Copy-Item -Path "$bep/core/*" -Destination "$run/BepInEx/core"
Copy-Item -LiteralPath "$bep/plugins/ValheimModding-Jotunn/Jotunn.dll" -Destination "$run/BepInEx/plugins/Jotunn"
Copy-Item -LiteralPath "$root/src/Bola/bin/Release/net481/Bola.dll", "$root/src/Bola.Core/bin/Release/net481/Bola.Core.dll", "$root/artifacts/bundle/bola.prototype.assets", "$root/tests/Bola.RuntimeSmoke/bin/Release/net481/Bola.RuntimeSmoke.dll" -Destination "$run/BepInEx/plugins/Bola"
if ($NoRanged) { Copy-Item -LiteralPath "$root/../no-ranged-public/src/NoRanged/bin/Release/net481/NoRanged.dll" -Destination "$run/BepInEx/plugins" }
$priorPrototype = $env:BOLA_PROTOTYPE
$priorGameplay = $env:BOLA_SMOKE_GAMEPLAY
$priorInteractive = $env:BOLA_INTERACTIVE
$priorOutput = $env:BOLA_SMOKE_OUTPUT
$priorDoorstop = $env:DOORSTOP_TARGET_ASSEMBLY
try {
    $env:BOLA_PROTOTYPE = '1'
    $env:BOLA_SMOKE_GAMEPLAY = if ($Gameplay) { '1' } else { '0' }
    $env:BOLA_INTERACTIVE = if ($Interactive) { '1' } else { '0' }
    $env:BOLA_SMOKE_OUTPUT = $run
    $env:DOORSTOP_TARGET_ASSEMBLY = "$run/BepInEx/core/BepInEx.Preloader.dll"
    $game = 'C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim.exe'
    $arguments = @('-batchmode','-screen-fullscreen','0','-screen-width','1000','-screen-height','1000','-savedir',('"' + "$run/saves" + '"'),'-logFile',('"' + "$run/unity.log" + '"'),'--doorstop-enabled','true','--doorstop-target-assembly',('"' + "$run/BepInEx/core/BepInEx.Preloader.dll" + '"'))
    if ($Interactive) {
        $arguments = @($arguments | Where-Object { $_ -ne '-batchmode' }) + @('-console')
        $process = Start-Process -FilePath $game -ArgumentList $arguments -WorkingDirectory $run -WindowStyle Normal -PassThru
    } else {
        $process = Start-Process -FilePath $game -ArgumentList $arguments -WorkingDirectory $run -WindowStyle Hidden -PassThru
    }
    $process.Id | Set-Content "$run/process-id.txt"
    Write-Output "Isolated Bola fixture PID $($process.Id): $run"
} finally {
    $env:BOLA_PROTOTYPE = $priorPrototype
    $env:BOLA_SMOKE_GAMEPLAY = $priorGameplay
    $env:BOLA_INTERACTIVE = $priorInteractive
    $env:BOLA_SMOKE_OUTPUT = $priorOutput
    $env:DOORSTOP_TARGET_ASSEMBLY = $priorDoorstop
}
