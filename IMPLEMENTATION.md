> Update 0.2.2: The explicit solo-only gates were removed at the user's request
> following their multiplayer test. This does not implement the planned coordinator:
> binding still requires local ownership of the target. Remote carrier phase/pickup
> synchronization and ownership-transfer durability remain unverified/incomplete.
> Older descriptions below refer to earlier builds.
# Implementation ledger

The user explicitly deferred animation quality as a blocking gate on 2026-09-24,
then requested local testing first because two actual clients are unavailable.
Current delivery: an isolated, playable solo prototype, not completion of the full plan.

| Area | Current status |
| --- | --- |
| Assets | Preserved source hashes; rebuilt ropes, reduced geometry, humanoid import and Windows bundle. Both-model fit and final materials pending. |
| Presentation | Held rope motion and charge circle/zoom implemented. Experimental animation remains unsuitable; polished upper-body transitions, observer presentation and anatomy-specific binding visuals pending. |
| Ground binding | Owner motor suppresses translation, root motion and jumps; preserves rotation and native attack events. Greyling and Wolf tested in the actual game. |
| Flight | Controlled descent, actual collision/foot contact confirmation, grounded timer, timeout, flight restoration and recovery. Deathsquito and Hatchling tested. |
| Throws | Custom charge/stamina, release delay, ballistic swept sphere, item identity, single carrier through impact/binding/recovery, pickup and interruption paths implemented locally. |
| Configuration | Validated immutable revisions, charge/binding/flight/projectile/eligibility settings and local zoom. Crafting/item settings, external-knockback option and rope quality remain incomplete; recipe and item use planned defaults. |
| No_Ranged | Separate 1.2.2 test build with exact identity exception and live cancellation. No required cross-dependency. |
| Dedicated startup | Windows dedicated registers logical item without cosmetic bundle or graphics. This does not validate multiplayer gameplay. |
| Multiplayer | New actions deliberately blocked. Server adjudication, ownership transfer, durable journal, reservation/receipt reconciliation, snapshot replication and actual two-client suite remain unimplemented. |

## Remaining validation and limitations

- The normal local throw/recovery/pickup path conserves one identity in recorded tests.
  Crash/restart atomicity and exactly-once transactions are NOT guaranteed. Use disposable saves.
- Deep water, slopes, roofs, moving supports, removed supports and all input/menu interruption
  combinations need further actual-game tests. Damage-break and refresh are nondefault paths.
- Full installed creature inventory, modded motors, bosses, both player models and the production
  compatibility profile are not certified. Unknown/custom motor support is limited.
- Stack size is fixed at one. Recipe is 3 Flint + 2 LeatherScraps, no station. No upgrades/durability.
- No Linux dedicated or non-Windows client claim. No multiplayer, save-restart lifecycle or observer animation claim.
- The interactive launcher itself still needs user playtesting; automation exercises controlled fixtures.

Next development work after local feedback: fix local interaction issues, then implement the
durable server coordinator and transaction fault tests. Actual two-client validation remains
a later acceptance requirement. Animation improvements can proceed independently.

No release, production profile replacement, server deployment or publication is authorized here.
