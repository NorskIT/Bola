$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$server = Join-Path $root 'artifacts/dedicated'
if (!(Test-Path -LiteralPath "$server/valheim_server.exe")) { throw 'Install Steam app 896660 into bola/artifacts/dedicated first.' }
$run = Join-Path $root ('artifacts/headless-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force "$run/BepInEx/core", "$run/BepInEx/plugins/Bola", "$run/BepInEx/plugins/Jotunn", "$run/saves" | Out-Null
$bep = Join-Path $env:APPDATA 'com.kesomannen.gale/valheim/profiles/Prod-v1/BepInEx'
& dotnet build "$root/tests/Bola.RuntimeSmoke/Bola.RuntimeSmoke.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Copy-Item -Path "$bep/core/*" -Destination "$run/BepInEx/core"
Copy-Item -LiteralPath "$bep/plugins/ValheimModding-Jotunn/Jotunn.dll" -Destination "$run/BepInEx/plugins/Jotunn"
Copy-Item -LiteralPath "$root/src/Bola/bin/Release/net481/Bola.dll", "$root/src/Bola.Core/bin/Release/net481/Bola.Core.dll", "$root/tests/Bola.RuntimeSmoke/bin/Release/net481/Bola.RuntimeSmoke.dll" -Destination "$run/BepInEx/plugins/Bola"
# Intentionally no cosmetic AssetBundle in this fixture.
Copy-Item -LiteralPath 'C:/Program Files (x86)/Steam/steamapps/common/Valheim/winhttp.dll' -Destination "$server/winhttp.dll"
$priorPrototype=$env:BOLA_PROTOTYPE
$priorOutput=$env:BOLA_HEADLESS_OUTPUT
$priorDoorstop=$env:DOORSTOP_TARGET_ASSEMBLY
try {
    $env:BOLA_PROTOTYPE='1'; $env:BOLA_HEADLESS_OUTPUT=$run
    $env:DOORSTOP_TARGET_ASSEMBLY="$run/BepInEx/core/BepInEx.Preloader.dll"
    $arguments=@('-batchmode','-nographics','-name','BolaLocalFixture','-world','BolaLocalFixture','-password','BolaTestOnly','-public','0','-ip','127.0.0.1','-port','24761','-savedir',('"'+"$run/saves"+'"'),'-logFile',('"'+"$run/unity.log"+'"'),'--doorstop-enabled','true','--doorstop-target-assembly',('"'+$env:DOORSTOP_TARGET_ASSEMBLY+'"'))
    $process=Start-Process -FilePath "$server/valheim_server.exe" -ArgumentList $arguments -WorkingDirectory $run -WindowStyle Hidden -PassThru
    $process.Id | Set-Content "$run/process-id.txt"
    Write-Output "Isolated headless fixture PID $($process.Id): $run"
} finally { $env:BOLA_PROTOTYPE=$priorPrototype; $env:BOLA_HEADLESS_OUTPUT=$priorOutput; $env:DOORSTOP_TARGET_ASSEMBLY=$priorDoorstop }
