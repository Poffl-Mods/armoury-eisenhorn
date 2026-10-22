# V1–V6 progression: technical feasibility

Checked locally on 2026-10-21. Result: the required APIs exist and the compile-only probe passes against the installed game assemblies. This is not a working mod or an in-game/save-load validation.

## Evidence

- Existing Ork implementation: `../armoury-orks/Assets/Modifications/ArmouryOrks/Scripts/ChoppaProgressionController.cs`, especially `HandleItemsAdded`, `TryUpgradeAll`, and `TryUpgradeItemInSeries`. It already replaces inventory/equipped weapons and defers changes during combat. Reuse the approach, but add transaction/error handling and stricter player-ownership checks. Do not reuse the level requirement.
- Installed `Code.dll`: `IUnitDeathHandler.HandleUnitDeath(AbstractUnitEntity)` permits detecting a death and checking `unit.Blueprint.AssetGuid`. Named encounters may additionally require area/instance identity, since a blueprint can be reused.
- Installed `Code.dll`: `IItemsCollectionHandler` exposes additions/removals. `ItemsCollection.Contains(BlueprintItem, int)`, `Remove(BlueprintItem, int)`, and `Add(BlueprintItem)` support material checks, consumption and replacement creation. `ItemSlot.RemoveItem` and `InsertItem` support re-equipping.
- Installed `RogueTrader.GameCore.dll`: `BlueprintUnlockableFlag` exposes Unlock/Value; the backing UnlockableFlagsManager stores integer flag values. Use mod-owned flag blueprints for milestones/reward claims; save/load behavior of the completed integration remains to be tested.
- Vanilla blueprint example: `Equipment/Rings/Unique/Cogni-Simulacrum_Ability.jbp` runs actions and removes an item on successful use. This supports the general ability-driven approach, but does not prove a custom upgrade consumable's UI, restrictions or charge timing.
- Installed and editor `Code.dll` SHA-256 differ. The final compile check therefore uses the installed game's assemblies, not only the editor copies.

## Recommended implementation

A named encounter records a milestone. After combat, grant a dedicated upgrade token exactly once. Once token and matching weapon tier are present in permitted player-owned inventories, perform one Vn -> Vn+1 upgrade. Automatic acquisition-triggered upgrades are the simplest initial path. Explicit item use is also a candidate, but its use restrictions and charge timing need a separate runtime check.

Never process arbitrary vendor/loot collections from the global item-added event. Ignore cargo initially: it uses different storage semantics. Defer during combat and avoid reentrant upgrade events. Define whether a token upgrades one specific weapon copy or unlocks a tier globally before implementing.

Create and validate the replacement before discarding the old weapon; retain enough state for rollback if slot insertion or material removal fails. Preserve relevant per-instance state deliberately (e.g. ammunition or custom enchantments); blueprint replacement does not guarantee that. Native add/remove operations do not by themselves provide an atomic transaction.

Past kills cannot be recovered from a newly installed death-event listener. For each selected encounter, identify an existing quest/etude/flag proving completion or provide an alternate token source. No general retrospective kill check has been established.

## Validation performed

`src/TechnicalChecks/ProgressionApiProbe.cs` compiles without warnings against the installed game using `compile_probe.py`. It checks signatures for death/item events, enemy blueprint identity, material operations, weapon slot operations and milestone values. It performs no mutations and is not installed into the game.

## Remaining runtime checks

Prototype V1 + one token -> V2; test inventory and equipped weapon, weapon sets, stash policy, token stacks, duplicate weapon copies, wrong/missing material, combat deferral, failure recovery, enemy identity and duplicate events. Save/reload and area transitions must retain tier and claimed reward state. Confirm how item use consumes charges before choosing automatic acquisition versus explicit activation. Multiplayer synchronization has not been evaluated.

## Follow-up: native weapon quests

See `weapon-quest-feasibility.md` for the native journal, chained objective, reward, and save/load architecture audit. Native quest integration is supported by inspected code and compile checks; the custom in-game roundtrip remains untested.
