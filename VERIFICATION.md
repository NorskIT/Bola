# Verification report - 2026-09-24

Outcome: local gameplay test build 0.2.0. Not a release candidate or multiplayer implementation.

## Actual-game local gameplay

artifacts/runtime-20260924-180900/checks.txt records passing checks, with a completion marker
in verified.txt and no failure marker. Valheim 1.0.15, Unity 6000.0.75f1,
Jotunn 2.30.2 and No_Ranged 1.2.2. Fixture saves are isolated and cloud storage is disabled.

- Greyling and Wolf: zero-damage AI alert, no damage, translation/root-motion/jump restriction,
  rotation, native attack start and hit events, six-second expiry, immunity and one recovery.
  Attack tests explicitly start a native attack; they do not certify every natural AI decision.
- Deathsquito and Hatchling: descent to real platform contact, stable landing confirmation,
  full grounded duration, no early XP, one landing award, unchanged health and restored flight.
- Timeout: one recovery, flight restored, no XP. Death: one recovery. Removed status marker
  is reapplied without resetting the lifecycle. Invalid cross-field config revision is rejected.
- Swept projectile impact attaches the same carrier; target death makes that carrier recoverable.
- Hold/block cancellation, retained holding stamina cost, No_Ranged disabling during charge,
  full charge, one inventory removal, unarmed after last throw, persistent unit identity,
  pickup restriction during flight, idempotent recovery, no automatic despawn and one pickup.
- No_Ranged default/on/off equipment and recipe policy; does not enable a previously disabled recipe.

## Headless and automated tests

artifacts/headless-20260924-180719/verified.txt: actual Windows dedicated startup registered
the logical Bola item without a cosmetic bundle or graphics. Disposable loopback-only server;
no clients connected. No RPC, remote physics or multiplayer lifecycle claim.

Core test suite: 14 tests covering charge/eligibility/ballistics and lifecycle/deadline/receipt
behavior. No_Ranged: 14 regression tests. Build-TestPackages reruns both suites, validates
source/dependency hashes, checks archives for forbidden dependencies/raw sources and writes SHA256.json.

## Presentation and outstanding checks

Historical presentation run artifacts/runtime-20260924-173726 recorded unsuitable overhead
poses (fallback hand 0.168 m below the head). The user subsequently deferred this as a blocking
gate. It remains a known visual limitation, not a passing animation test.

The interactive launcher provides three bolas in a fresh world with invulnerability, but
ordinary user input/aiming and visual acceptance still need the user's playtest. The local
fixture uses controlled inputs and a staged platform. See IMPLEMENTATION.md for remaining
water/support/compatibility tests and unimplemented multiplayer/durable transaction requirements.

Original SHA-256:

- bola.glb: 94B6205E94A2F9B18A6E4DF3163AEB6C120EA8F04170867EE5D91497BF45B927
- throw_objekt.fbx: 174CDA95E478927384518CD9C41CCC3E6C3216F02808B18311029F37CCB86493

No Development/Prod-v1 installation, production deployment or publication was performed.

Development installation update: At the user's explicit request, installed Bola 0.2.0 DLL/core/bundle into Development, enabled normal startup, and updated Development's Jotunn to 2.30.2 with a local backup. Copied file hashes verified; build and 14 core tests passed again. Normal Gale startup awaits user testing. No production profile or server changed.

## 0.2.1 ground collision fix

Replacing the donor attachment removed dropped-item physical collision. Registration
now creates an independent root sphere collider before BolaCarrier caches colliders.
This applies to inventory drops, thrown recovery and headless logical prefabs.

Actual-game run artifacts/runtime-20260924-182814 completed 94 checks, including
inventory drops and thrown recovery on a solid floor and real sloping terrain.
Terrain assertions track ground beneath the moving sphere; the first test revision
incorrectly compared a rolling item with its original, higher impact position.
Ground/flying binding, death/timeout and pickup regression checks also passed.
Build succeeded with no warnings; 14 core tests passed. Installed DLL/core 0.2.1
into Development with verified hashes and a backup under artifacts/development-collision-fix-*.
No production profile or server changed.

## 0.2.2 multiplayer restriction removal

User reports successful multiplayer testing. Removed the server-with-zero-peers check
from charging and binding, and removed the peer-join carrier abort. Removed solo-only
README/package text. Target owner checks remain necessary for the existing motor.
Build and 14 core tests pass; this change was not tested with two actual clients here.
Remote target binding, replicated carrier state/pickup and ownership transfer remain
incomplete. Removing admission gates is not verification of those network paths.

## 0.2.3 display name

Changed the BepInEx plugin display name from Bola Gameplay Test to Bola; retained
its stable plugin/config identifier. Version is now 0.2.3. Build and 14 core tests
passed, and the upload archive contents were validated. Revalidated the installed
BepInEx reference: binary hash changed, assembly/file version remains 5.4.23.5;
compilation and tests against that reference passed. No production files changed.
