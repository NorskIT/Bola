> Update 0.2.2: The explicit solo-only gates were removed at the user's request
> following their multiplayer test. This does not implement the planned coordinator:
> binding still requires local ownership of the target. Remote carrier phase/pickup
> synchronization and ownership-transfer durability remain unverified/incomplete.
> Older descriptions below refer to earlier builds.
# Bola 0.2.1 local gameplay test

Playable local prototype for Valheim 1.0.15. Multiplayer is disabled pending the
server coordinator and durable inventory transactions. This is not a release candidate.
Animation polish is deferred by the user's instruction; it no longer blocks gameplay work.

## Try it locally

Close Valheim, then double-click `Start-Local-Test.cmd` in this directory.
The launcher builds the current code and opens an isolated disposable world with
three bolas, invulnerability and the No_Ranged compatibility test build. Initial
loading can take about a minute. It does not install into Development or Prod-v1.
Each launch creates fresh saves and logs under `artifacts/runtime-<timestamp>/`.

Hold primary attack to charge, release to throw, and press block to cancel.
Pick up the thrown bola in the world. The last throw leaves you unarmed.
Greyling and Wolf are useful ground targets; Deathsquito and Hatchling exercise descent.
For quick spawning, open the console with F5, enter `devcommands`, then for example
`spawn Greyling 1`. This launcher enables the console for this isolated game.

Please check aiming, charge/cancel feel, recovery, movement while charging, and binding
while enemies attack. Ground binding lasts six seconds, followed by eight seconds of
immunity. Flyers start their six seconds after confirmed supporting collision.
The hand/overhead animation and bound visual are provisional.

## Build and verification

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1 -Assets
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Runtime.ps1 -Gameplay -NoRanged
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Headless.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-TestPackages.ps1
```

Build inputs pin .NET SDK 10.0.400, Blender 4.5.3, Unity 6000.0.75f1 and installed
game dependencies. Environment.props gives installation defaults and
dependencies.lock.json records hashes. Environment.local.props can override paths.
Headless verification additionally requires the isolated dedicated installation under
artifacts/dedicated. Runtime scripts return after launching; inspect verified.txt
or failure.txt in the printed directory for the result.

The plugin loads normally, including in the user-authorized Development profile. Archives are review
artifacts, not ordinary Gale installation packages. The launcher reads installed
dependencies but writes only its own test directory; cloud saves are disabled in
that process. See [verification](VERIFICATION.md) and [remaining work](IMPLEMENTATION.md).

Source asset hashes are preserved under assets/source. Bundles contain adapted
geometry and gameplay animation, without reference robot meshes or extracted game
textures/shaders. Asset redistribution rights still need confirmation before publication.
Publication and deployment follow the workspace Hexium > Gale > new profile code workflow.



## Source assets

The original bola.glb and throw_objekt.fbx are not included in Git while their
redistribution rights remain unconfirmed. Asset builds require local copies in
assets/source matching hashes.json. Generated bundles and installed game assemblies
are also excluded. The provided mod/item icons and README screenshot are included.
