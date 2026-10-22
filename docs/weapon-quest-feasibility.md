# Native multi-stage weapon quests: technical audit

Date: 2026-10-21. Scope: native journal integration, chained weapon objectives, upgrade rewards, save/load architecture.

## Result and evidence level

The installed game exposes all required systems. A native quest per weapon with five upgrade stages (V1 -> V2 through V5 -> V6) is technically supported by the inspected implementation. Quest APIs compile against installed game assemblies. Eleven automated read-only metadata/IL checks pass. An existing save archive contains the native quest book and objective state.

This is an architecture/API verification, not an end-to-end in-game demonstration. No Eisenhorn quest blueprints have been built, registered, installed or loaded. No custom-quest save/reload roundtrip has been performed. No existing save, game installation or enabled-mod configuration was changed. A full integration test remains necessary before claiming runtime compatibility.

## Native journal

`JournalVM` constructs its navigation using `GameHelper.Quests.GetList()`. That helper returns `Game.Instance.Player.QuestBook.Quests`. Therefore the normal journal consumes runtime quest instances, rather than only a fixed list of vanilla quest GUIDs.

`QuestBook.GiveObjective(BlueprintQuestObjective, bool)` ensures and starts a native objective. Use this API after the weapon is acquired; a custom menu is not needed for the basic design. Provide a registered `BlueprintQuest`, objective blueprints, localized strings, a valid existing quest group, visible objectives, and appropriate chapter/type/DLC settings. Journal grouping reads the game's quest group definitions, so use a standard group initially rather than inventing an unregistered group ID. This supports the native UI path but does not visually verify notifications, sorting, or the rendered entries.

## Multiple stages

`BlueprintQuest` has an objective list. Each `BlueprintQuestObjective` references its parent quest and can contain `m_NextObjectives`. `Quest.OnObjectiveFinished` enumerates the next objectives after successful completion and opens them. `m_FinishParent` controls finishing the parent; enable it only on the final stage of the linear weapon quest.

The compile probe checks `SetQuest`, `LinkObjective`, `AddNextObjective`, `SetIsFinishParent`, `GiveObjective`, `CompleteObjective`, and `GetObjectiveState`. Calls used to construct the graph demonstrate API availability, not that runtime-created blueprints are registered or persisted. Final content should be packaged as normal mod blueprints with stable GUIDs.

Vanilla reference: `DLC/DLC2/Narrative/Quests/ArbitesNotebook/ArbitesNotebook_quest.jbp` and its objective files show native quest/objective serialization fields. This is a format reference, not a DLC dependency for Eisenhorn.

## Weapon upgrade rewards

Native `ObjectiveStatusTrigger` implements `IQuestObjectiveLogic.OnCompleted` and runs its `ActionList`. The vanilla blueprint `GlobalMaps/Colonization/Projects/DLC2_DargonusTithe/DLC2_DargonusTithe_1_Objective.jbp` uses this completion trigger. There is also the global `IQuestObjectiveHandler.HandleQuestObjectiveCompleted` event.

A custom slot-preserving upgrade still requires our runtime code. Native quest completion does not automatically implement weapon replacement. Reuse the inspected Ork replacement approach with stricter ownership checks and rollback. The API probe compiles inventory material checks/consumption and weapon slot replacement.

Recommended order: detect fulfilled requirements, defer until outside combat, validate the exact weapon and materials, apply and validate the upgrade, record its claim, then complete the objective. Native chaining opens the next stage. If the weapon is absent or replacement fails, keep a visible unfinished/ready objective and allow retry. If an OnCompleted callback is used instead, persist a pending reward and reconcile on load; an event listener alone cannot recover a missed reward after loading.

This sequence is a design requirement, not a proven atomic engine transaction. Define behavior for duplicate copies, custom enchantments, ammo, weapon sets, stash, and a lost/sold weapon. A separate claim flag can help reconciliation, but must not be set before successful delivery.

## Save/load evidence

Installed `Code.dll` metadata confirms:

- `Player.m_QuestBook` and `Player.m_UnlockableFlags` carry `JsonProperty`.
- `Quest.m_State` and `Quest.PersistentObjectives` carry `JsonProperty`.
- `QuestBookEntityEntry.m_State` and `m_IsVisible` carry `JsonProperty`.
- `Quest.OnPostLoad` and `QuestBookEntityEntry.OnPostLoad` perform native load handling. The entry rebuilds cached relationships from blueprint references.

A read-only inspection of the newest existing local `.zks` archive also found `player.json` containing `m_QuestBook/Facts/m_Facts`, quest `m_State`, `PersistentObjectives` with objective `m_State`, and `m_UnlockableFlags`. Only structure was inspected; no save was modified. This verifies real save storage for native quests, not successful restoration of yet-unbuilt Eisenhorn assets.

Use permanent blueprint GUIDs and load the mod's quest/objective assets before the saved references are resolved. Avoid keeping required progression only in MonoBehaviour fields. Mod loading, blueprint reference resolution, and custom reward reconciliation still require a runtime roundtrip test. Removing the mod or changing GUIDs is outside the claimed compatibility.

## Checks and reproduction

- `src/TechnicalChecks/QuestApiProbe.cs` and the existing progression probe compile together without warnings against the installed assemblies using `compile_probe.py`.
- `src/TechnicalChecks/QuestMetadataCheck.cs`, compiled with the locally supplied Mono.Cecil, passes 11 assertions: serialized quest book, serialized flags, serialized objective state/visibility, serialized quest state, serialized persistent objectives, post-load methods, native next-stage advancement, native objective start, journal helper source, normal journal source, and completion actions.
- These checks read/compile code only. Neither probe is installed or executed as a game mod.

Example compile-only command:

```sh
python3 src/TechnicalChecks/compile_probe.py '/path/to/game/Managed' '/path/to/MonoBleedingEdge'
```

## Required runtime acceptance test

Build a disposable test package with a separate test quest and stable test GUIDs; use a separate test save. Install through Mac-ModInstaller under the approved MicroPatches baseline. Verify:

1. Acquiring a test V1 weapon starts a visible journal quest with title and description.
2. Gathering a test token upgrades an equipped or carried weapon to V2 exactly once and opens the next objective.
3. A selected enemy encounter completes a subsequent requirement, with a distinguishable encounter identity.
4. Save at an active stage, reload, and verify the same objective, weapon, and materials; continue progression.
5. Save after reward delivery, reload and revisit the area; verify no duplicate reward or reset.
6. Test a deferred combat reward, missing weapon, multiple copies, weapon sets, stash policy, and rejected replacement.
7. Verify quest completion and completed journal history survive another save/reload.

No runtime blockers have been demonstrated, but these tests remain open. The current evidence supports proceeding with native quest implementation; it does not certify a completed playable quest system.
