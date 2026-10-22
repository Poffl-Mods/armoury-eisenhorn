# Eisenhorn weapon quests: implementation research and prototype plan

Research date: 22 October 2026. This document proposes implementation; it does not implement a mod.

## Recommendation and evidence boundary

Use an **Owlmod content package plus a small UMM configuration/runtime adapter**. Author the weapons, native quests, lore dialogues, mod-owned flags, and a quarters mechanics scene through the existing Owlcat editor integration. Put reconciliation and safe item replacement in one runtime service, with one owner for initialization. UMM exposes the requested mode selection. Use Harmony only where inspection proves that a necessary lifecycle hook has no suitable public event.

Preserve the directory's approved MicroPatches 1.21.1 baseline, its blueprint dependency setting, and Mac-ModInstaller workflow. Nothing in this research authorizes updating those dependencies.

Three evidence levels apply throughout:

- **Installed API/IL verified:** method signatures or implementation structure inspected in installed assemblies; existing probes rerun successfully today.
- **Export verified:** concrete serialized game data inspected in the local editor blueprint export. The export must still be compared with the installed build before locking production GUIDs and predicates.
- **Proposed / runtime untested:** architecture inferred from those mechanisms. No custom quest, book scene, or reward roundtrip has been run in-game.

Installed `Code.dll` SHA-256: `446ebca34e768cb157e1353be60a27957288f6422b3c91a3be708e9e13036660`. The editor project records Unity `6000.0.64f1`. Do not follow the upstream template's older Unity version blindly. The installed and editor assemblies were already found to differ in the earlier audit.

The existing compile-only API probes pass against the installed assemblies. All eleven assertions in `QuestMetadataCheck.cs` pass again: quest/flag serialization, objective visibility/state, quest state/objectives, post-load support, native stage advancement, objective activation, journal source, and completion actions. These checks cannot certify scene registration, localization, notifications, or gameplay compatibility.

## 1. Campaign detection: the actual Aurora chain

Rogue Trader tracks narrative state across several cooperating systems:

| System / verified type | Purpose and integration |
| --- | --- |
| `Kingmaker.AreaLogic.QuestSystem.QuestBook` | Player quest/objective runtime state; native journal reads this data. Query with `GetObjectiveState(BlueprintQuestObjective)` rather than searching UI text. |
| `Kingmaker.Blueprints.Quests.BlueprintQuest`, `BlueprintQuestObjective` | Static journal definitions and objective relationships. |
| `Kingmaker.AreaLogic.Etudes.BlueprintEtude`, `EtudesSystem` | Narrative state graph, conditions, triggers, conflicting groups, area links, additional mechanics. Started, playing and completed are distinct concepts. |
| `Kingmaker.Blueprints.BlueprintUnlockableFlag` | Persistent mod-owned booleans/integer values; suitable for entitlements and migration versions. |
| `Conditions.ObjectiveStatus`, `QuestStatus`, `EtudeStatus` under `Kingmaker.Designers.EventConditionActionSystem` | Data-driven checks of those systems. Installed metadata confirms the types and their state/reference fields. |
| `EvaluatedUnitDeathTrigger`, `SetObjectiveStatus`, `GiveObjective` | Exported vanilla chain connecting a particular scene entity's death to objective transitions. |

**The useful discovery is that searching for “Aurora” alone selects the wrong encounter.**

The export contains `FlashOfTheLastSunrise/Obj5_StopAurora.jbp`, GUID `49bd197947624e59881fc51f923f2d70`. Its references lead to the Archeotech Reactor encounter. It is not the starport Chaos Marine completion signal.

For the starport encounter, inspect:

`World/Etudes/RogueTraderEtude/Chapter01_Extra/StarPort_SecondState.jbp`

GUID `675793ba6ab345acb0beb81247a6ca24`.

Its `EvaluatedUnitDeathTrigger` evaluates `UnitFromSpawner` with:

- Editor name: `ChaosMarine`.
- Entity ID: `0dbc3e35-a100-498e-b79c-120880ed2e59`.
- Scene asset GUID: `1ab66bc255847fa47bd3940e178a2955`.

The death action list unhides an exit, completes objective `c5438eab19b849e19a79ace23c3ddc5d`, and gives objective `53ffb25c02e74d82bed8250cdd781fde`.

Those resolve to:

| Asset | GUID | Meaning established by export |
| --- | --- | --- |
| `World/Quests/Chapter 1/RaucadRun_c1/Obj3_SafeShuttle .jbp` | `c5438eab19b849e19a79ace23c3ddc5d` | Completed by the Chaos Marine death trigger. Its developer comment explicitly identifies that death. |
| `World/Quests/Chapter 1/RaucadRun_c1/Obj4_Escape .jbp` | `53ffb25c02e74d82bed8250cdd781fde` | Started by the same trigger; comment says completed by selecting departure on the shuttle. |
| Parent quest `RaucadRun_c1_quest.jbp` | `895a33166cc34bd88a934e3178e64a93` | Parent referenced by both objectives. |

Some filenames have a space before `.jbp`; preserve that when locating them. GUIDs above are read from files, not guessed.

**Proposed primary predicate:** the SafeShuttle objective is `QuestObjectiveState.Completed`. Subscribe to its completion for prompt response, but query its persistent state after load and during reconciliation. Do not start or complete any vanilla objective yourself.

**Candidate fallback evidence:** Escape is already started/completed. Its documented start chain makes it a useful corroborating signal, but first check every writer. Do not turn “chapter >= 2” or a parent quest completion into a fallback until alternate routes, failures, and migration behavior have been inspected. A failed objective is not completion evidence.

`AuroraMurdered` and `AuroraTrophy` also exist, but their names alone do not establish equivalence with this encounter. Neither is needed for the first prototype.

### Reproduce and strengthen the identification

1. Resolve the objective and etude in the live game's blueprint catalog; compare fields with the export.
2. Search the complete export for each GUID, not only filenames. Inspect every writer and reader of SafeShuttle/Escape and their parent quest.
3. Follow the scene entity reference into the starport mechanics scene and confirm its unit blueprint/encounter identity.
4. Inspect the strings referenced by Title/Description to correlate internal names with the visible journal.
5. On separate saves before and after the battle, log objective states. Repeat after departure and in a later chapter. Do not manipulate vanilla state during this check.
6. Record the validated game build and hashes alongside the milestone manifest. Unknown or missing assets should disable only that milestone and produce diagnostics.

Installed `EtudesSystem` exposes `EtudeIsNotStarted`, `EtudeIsCompleted`, `EtudeIsPreCompleted`, `GetSavedState`, `GetStartedEtudes`, and `GetCompletedEtudes`. It also exposes `GetActiveAdditionalMechanics(BlueprintArea)`. For another milestone, inspect `EtudeStatus` and the etude's full lifecycle before deciding whether started, playing, or completed proves the event. An area-linked etude no longer playing does not mean its event never happened.

## 2. Native quests and progression flow

Use one quest per weapon, containing its lore episodes. A shared manifest coordinates which fragments reveal the larger Eisenhorn story. Stable episode IDs should identify content independently of weapon tier numbers.

Verified native relationships:

- `BlueprintQuestObjective.SetQuest`, `BlueprintQuest.LinkObjective`, `AddNextObjective`, and `SetIsFinishParent` compile in the existing probe.
- `QuestBook.GiveObjective(objective, false)` starts a native objective.
- `QuestBook.CompleteObjective(objective)` completes it.
- `Quest.OnObjectiveFinished` enumerates `NextObjectives` and opens the next entries.
- `ObjectiveStatusTrigger` implements completion logic that runs an `ActionList`.
- `IQuestObjectiveHandler` exposes started, visible, completed and failed notifications.

Use packaged blueprints with permanent GUIDs; constructing a C# object does not register a save-resolvable asset. Supply localized title/description, valid quest grouping, visibility, parent references, and chapter/type settings. Use a vanilla non-DLC grouping initially. Do not copy XP/reward components accidentally from a template quest.

**Do not directly chain every reading objective if later stages need separate campaign milestones.** Native chaining opens its successors immediately. Either start each episode explicitly when eligible, or chain a reading objective into a visible waiting objective and only open the next reading objective once its gate passes. Keep the overall quest unfinished until its final intended episode; only the appropriate final objective should finish its parent.

Proposed first-episode sequence:

1. Blueprints resolve and a player save is ready; read/migrate mod-owned state.
2. Read vanilla SafeShuttle completion; latch `AuroraMilestoneObserved` in this save.
3. If the first pistol lore episode has not been acknowledged, give its quarters reading objective once.
4. Enable the book presentation when the appropriate quarters area is loaded and interaction is safe.
5. Open a short Book dialogue. Intermediate pages do not grant rewards.
6. An explicit final “Close the account” answer records `PistolEpisode1Read` and the corresponding story entitlement.
7. Complete the reading objective, then reconcile delivery of the now-entitled pistol tier.
8. Hide/disable this episode's book presentation. If the pistol is unavailable, retain a separate “recover/bring the pistol” delivery objective or equivalent visible pending status.

This deliberately separates **reading completed** from **item delivered**. Otherwise a missing pistol can trap the player in an endlessly repeated reading objective. Keep a delivery objective until replacement succeeds if native journal feedback is desired.

In Level mode, reading and journal completion behave exactly the same, but weapon entitlement comes from the chosen level thresholds. Reading must never downgrade a weapon or replay a level-delivered reward.

## 3. Adding the quarters book

### Preferred experiment: a mod-owned additional mechanics scene

The export verifies a native path:

`BlueprintEtude.m_AddedAreaMechanics` → `BlueprintAreaMechanics` → `Area` + `Scene`.

`StarPort_SecondState` attaches additional mechanics this way. For the likely quarters area, exported assets include:

- `World/Areas/Prologue/VoidshipOfficerDeck_AfterPrologue/VoidshipOfficerDeck_AfterPrologue.jbp`, area GUID `66b9351d457f4363afebcbca96a506da`.
- Its `..._Quarters.jbp` enter point, GUID `0530a068283540eb9b51cc66061a3a96`, points to that area/area part.
- `Addons/VoidshipOfficerDeck_AfterPrologue_KibellahRendezvous.jbp`, GUID `e224913e8c84416f935884f109b00f4b`, is a `BlueprintAreaMechanics` targeting that same area and a separate scene.

The DLC example establishes the attachment format; it should not become a runtime dependency. Confirm the area actually used for ordinary player visits, all relevant chapter variants, and usable table coordinates in the live game. No coordinates were established in this research.

Proposed implementation:

1. Create an Eisenhorn mechanics scene with only the new book, its native map-object interaction, and required supporting content.
2. Create a mod-owned `BlueprintAreaMechanics` pointing to the verified target area and scene.
3. Create a mod-owned etude linking that area and including the new mechanics blueprint. Start it through the runtime service after content registration.
4. Initially keep the scene attached while the book's availability is controlled by episode state. This avoids making scene unload/reload itself part of reward delivery.
5. Give the map object a permanent entity identity. Derive its visibility from save state on every relevant scene load.

This is **supported by vanilla data and installed API structure, but not yet a working mod-scene example**. The first prototype gate must prove a newly registered etude/mechanics scene is included when entering an existing area and restored after save/load. Check immediate attachment when installing while already inside the quarters separately; defer to the next normal area load if necessary.

### Interaction and lore presentation

Installed types include:

- `Kingmaker.View.MapObjects.MapObjectView`: native map-object view/entity path; exposes `CreateEntityData(bool)` and blueprint application.
- `Kingmaker.View.MapObjects.InteractionAction`: native action interaction.
- `InteractionActionSettings`: `Condition`, `Actions`, `DisableAfterUse`, and `DisplayName` fields.
- `InteractionComponentBase.InteractionSettings`: includes combat restriction, highlight/overtip, interaction type, and a dialogue reference.
- `Kingmaker.DialogSystem.Blueprints.BlueprintDialog`, `BlueprintBookPage`, `BlueprintCue`, `BlueprintAnswer`.

An existing `ActionsHolder` at `World/Encounters/DestroyedShipBESystem/ActionsHolders/StartBookEvent.jbp` contains a `StartDialog` action. `World/Dialogs/Ch2_VS/ForgeWorld/Scrapcode_bookevent/Scrapcode_bookevent.jbp` is a `BlueprintDialog` whose serialized `Type` is `Book`. This verifies that book presentation uses the dialogue graph; a separate invented “OpenLoreBook” API is unnecessary.

Wire an interaction action reference to an `ActionsHolder` that runs `StartDialog` on the mod's Book dialogue. Use a final `BlueprintAnswer.OnSelect` action for the explicit acknowledgement; that field exists in installed metadata. `BlueprintBookPage.OnShow` and `BlueprintCue.OnShow/OnStop` also exist, but simply showing a page is not reliable proof of finishing the sequence. Do not assume dialogue `FinishActions` distinguishes successful reading from every exit/cancellation path.

Exported `HideMapObject` plus `MapObjectFromScene` shows how native content hides/unhides a specific scene object. It does not create one. Use the mod's own entity/scene references, not vanilla IDs. Do not enable `DisableAfterUse` indiscriminately: an aborted reading must remain retryable. Check conditions again at interaction time to reject stale or repeated clicks.

The upstream scene guidance includes mod-defined subclasses when using Owlcat MonoBehaviours, and a material-fixer component for renderer-containing mod scenes. Verify that guidance against the current editor/build integration; do not use a bare decorative Unity GameObject and assume it gains native interactions or persistence.

### Alternatives

| Approach | Assessment |
| --- | --- |
| Additional mechanics scene | First choice: follows observed area/etude design, keeps new entity identity under mod ownership. Registration/load timing still needs proof. |
| Runtime spawn of native map object | Fallback if scene attachment cannot work. Inspect entity registration, scene ownership, disposal, save restoration and multiplayer handling before using `Instantiate`; no complete verified spawn recipe was established here. |
| Attach an action to an existing quarters prop | Useful diagnostic shortcut, but conflicts with vanilla/DLC interactions and does not prove adding a new object. |
| Replace the whole vanilla area/scene | Excessive conflict surface; unnecessary for one book. |

## 4. Weapon replacement and ownership policy

Installed compile-verified operations include:

```csharp
// Verified API fragments. These are not a safe replacement transaction by themselves.
ItemEntity replacement = inventory.Add(targetBlueprint);
bool removedFromSlot = slot.RemoveItem(false, false);
slot.InsertItem(replacement, false);
inventory.Remove(oldItem);
```

Types are `Kingmaker.Items.ItemsCollection`, `Kingmaker.Items.ItemEntity`, `Kingmaker.Items.Slots.ItemSlot`, and `Kingmaker.Blueprints.Items.BlueprintItem`. `InsertItem` returns void; returning from the call does not by itself establish successful final equipment state.

The local Ork `ChoppaProgressionController.TryUpgradeItemInSeries` is a concrete precedent for blueprint replacement and using `HoldingSlot`/`Collection`. It removes the old weapon before adding the replacement and gathers globally observed collections. **Do not copy those choices:** they risk loss on failure and processing non-player collections. Its reflection-based companion discovery is also a research shortcut, not the preferred production ownership contract.

Recommended policy: each tier is a permanent entitlement for a weapon family. Normalize eligible player-owned copies to that tier; never grant an extra copy merely because an upgrade was earned. This makes duplicates deterministic and reacquisition easy without depending on a custom item lineage serializer. It also means genuine duplicated copies all benefit; if the design demands exactly one heirloom, instance lineage and a verified per-item persistence mechanism become additional work.

Explicit cases:

| Case | Required behavior |
| --- | --- |
| Equipped main/alternate set | Capture exact holding slot, owner and weapon set. Reinsert there if legal. Account for two-handed/offhand changes; defer incompatible equipment changes rather than displacing unrelated gear silently. |
| Inventory | Replace within the authorized collection. Deduplicate scans because equipment may also be visible through collection traversal. |
| Companion | Include active and reserve player-owned companions through verified party ownership APIs. A current-area unit scan misses absent companions. |
| Shared stash | Include after its enumeration and ownership are verified. Reconcile again when accessible. |
| Cargo/vendor/ground | Exclude for prototype. Cargo has different semantics; a global item event is not authorization to mutate its collection. |
| Enchantments/modifications | Separate target-blueprint built-ins from dynamic instance additions. Transfer only compatible persistent additions through inspected APIs; never copy temporary buffs or blindly duplicate built-in enchantments. |
| Ammo/charges/state | Establish a field-by-field transfer policy. Preserve valid values or clamp to new capacity; do not accidentally refill on every reconciliation. |
| Duplicates | Upgrade eligible copies once each, keyed by their current blueprint tier. Do not let one global reward claim stop other copies from being normalized. |
| Missing/sold/destroyed | Keep entitlement pending. Show delivery status. No automatic replacement item; an explicit recovery feature can be designed later. |
| Already higher tier | Keep it. Mode switches, respecs and reconciliation never downgrade. |

Proposed replacement procedure: guard against reentrancy; defer during combat/dialogue/load; validate source, target and ownership; snapshot instance and slot state; create and validate a replacement while retaining the original; transfer approved state; swap and verify the final owner/slot; remove the original only after successful delivery; update delivery evidence. On failure, remove the candidate and restore the original slot/state. If inventory capacity prevents temporary coexistence, handle that explicitly rather than discarding the original first.

These calls do not form an engine transaction. Do not allow saving/yielding in the critical swap, and verify save/autosave scheduling before claiming atomicity. If interruptions can persist partial state, a transaction identifier plus verified per-item identity is required for deterministic recovery. Detect ambiguous “both original and replacement” states conservatively; never delete an unrelated matching item as cleanup.

Native add/remove actions are appropriate for a simple fixed reward; `ObjectiveStatusTrigger` can invoke custom actions. No native Rogue Trader action was verified here to preserve owner, slot, unique state and modifications across blueprint replacement. Custom C# is therefore the justified choice for this requirement. Inspect installed `ItemEntity`, weapon subclasses, enchantment ownership and slot insertion implementation before writing transfer code.

## 5. Saves, retroactivity and mode switching

The installed IL checks establish that `Player.m_QuestBook` and `m_UnlockableFlags` are serialized, as are objective state/visibility and quest state/persistent objectives. Quest entries implement native post-load handling. These support native persistence but do not prove resolution of newly introduced Eisenhorn references.

Use mod-owned `BlueprintUnlockableFlag` assets for a small, explicit first schema:

| Logical state (proposed mod data, not engine member names) | Reason |
| --- | --- |
| Schema version | Ordered idempotent migrations. |
| Per-save mode | Avoid changing another campaign through a global settings file. |
| Observed campaign milestones | Latch verified historical evidence without editing vanilla state. |
| Lore episodes read | Narrative completion independent of weapon strength. |
| Story tier entitlement per weapon | Rewards earned by lore. |
| Level tier entitlement per weapon | Highest tier earned under level mode. |
| Delivery status | Distinguish entitled, pending and actually observed delivery. |
| Tutorial grant recorded per weapon | Prevent repeated initial grants. |

`BlueprintUnlockableFlag.Unlock()` and its integer `Value` property compile against installed assemblies. Do not read value zero as “absent” when zero is a valid stored choice; define an unlocked/schema convention. The blueprint flag manager is per player/save, not a replacement for global UI preferences.

The upstream template mentions `EntityPartKeyValueStorage` for per-save data. **That exact type was not found in the installed managed-assembly metadata scan.** Do not copy that legacy sample into this project. Use the verified flags for the first schema. A richer transaction store requires inspection of current player/entity-part serialization and explicit type registration before implementation.

### Install after Rykad

Once the loaded player and blueprints are ready, query the existing SafeShuttle objective. If completed, latch the milestone and activate the first unread lore episode. Past death events are unnecessary for this case. Test whether finished quest objectives remain queryable on actual later-chapter saves; the native serializer evidence alone does not prove retention for every lifecycle.

If evidence is absent or the blueprint cannot resolve, leave the milestone “unknown,” log why, and offer a clearly identified manual repair action affecting only mod-owned state if required. Never silently complete vanilla quests to repair a custom one.

Backlog handling should unlock episodes chronologically while allowing players to read them one at a time. Do not auto-mark lore read merely because its campaign milestone happened before installation. Catch up the three weapons independently.

### Switching modes

Recommended non-destructive entitlement rule:

```text
PSEUDOCODE — all names below are mod policy abstractions.
if mode == Level:
    levelEarned[weapon] = max(levelEarned[weapon], tierForChosenLevelMetric())
if mode == Story:
    storyEarned[weapon] = max(storyEarned[weapon], tierFromReadEpisodes())
permanentEntitlement = max(storyEarned[weapon], levelEarned[weapon])
target = max(permanentEntitlement, highestRecognizedOwnedTier)
normalizeEligibleOwnedCopies(weapon, target)
```

Use party level initially to match the existing Ork design, if that is the intended user experience; `Player.PartyLevel` is used in that local source. Confirm it against current assemblies before adding a level API sample. Protagonist level and wielder level are different design choices and should not be mixed silently.

Story → Level immediately catches up entitlement to current level at a safe point. Level → Story preserves all earned tiers, and subsequent story reading continues. Previously earned level tiers may therefore keep applying to reacquired lower-tier copies in Story mode. Explain this permanent-entitlement behavior in the setting. Respecs cannot revoke progression. This also means switching briefly to Level mode permanently earns its catch-up rewards by design.

Persist a per-save selected mode, with UMM global settings providing only the default for new saves. Disable current-save mutation until a save is ready; apply a mode change once outside IMGUI drawing and reconcile afterwards.

For the actual UMM adapter, inspect the **installed bridge**, not only an external UMM distribution: the game bundles a modified integration. Common UMM concepts are a mod entry, GUI callback and settings-save callback, but their exact current signatures were not compiled here. Inspect `UnityModManagerBridge.dll`, its referenced manager assembly, and the current ToyBox entry implementation before writing that adapter. A fake compilable-looking settings class would overstate the evidence.

### Migrations and recovery

Load order: content/reference availability → native player restore → schema migration → evidence reconciliation → presentation. Register all persistent blueprint identities before native saved references need them.

Each migration checks old and new state, applies only monotonic additions, and commits the schema version last. Keep obsolete referenced blueprints as compatibility stubs. Do not regenerate GUIDs from display names. Never resurrect explicitly completed mod objectives or erase read flags on migration.

Run reconciliation after a loaded save is ready, after safe area loading, milestone notifications, relevant acquisitions, combat exit, level changes, and mode changes. Events request a rescan; they are not the only source of truth. Clear caches on player/save changes; a persistent controller must never retain another save's inventory references. Avoid heavy inventory scans each frame.

Removing the mod, disabling its content package, or loading a save while only half the hybrid is active is outside the compatibility guarantee. Detect partial installation early. Multiplayer synchronization and authoritative ownership are unverified; keep the first prototype single-player until tested.

## 6. Architecture comparison and project layout

| Approach | Strength | Limitation / decision |
| --- | --- | --- |
| Official modification framework | Packages blueprints, localization, scenes/assets and runtime assemblies. | Use for authored content; inspect current registration and build behavior. |
| Pure UMM C# | Settings and lifecycle logic are convenient. | Runtime-generated content still needs stable registration and bundle handling; avoid reimplementing these without need. |
| Blueprint-only | Native quests, conditions and action graphs compose well. | Safe slot/state-preserving replacement and mode reconciliation exceed the verified action recipe. |
| Harmony-heavy | Can intercept otherwise unavailable lifecycle points. | Fragile across updates and conflicts; choose only a demonstrated missing hook. |
| Hybrid | Native content plus focused C# policy and UMM setting. | Requires dependency/readiness handshake and one controller owner; recommended. |

Proposed structure, not files implemented by this research:

```text
src/
  Content/Blueprints/{Weapons,Quests,Dialogues,Etudes,Flags}/
  Content/Localization/
  Content/Scenes/EisenhornQuartersMechanics.unity
  Runtime/{Bootstrap,MilestoneReader,ProgressionReconciler,WeaponReplacement}/
  Runtime/{OwnershipResolver,SaveSchema,BookAvailability}/
  UMM/{Entry,SettingsView}/
  Manifests/{StableIds,Milestones,WeaponStages}/
  TechnicalChecks/                    # existing read-only probes
assets/{Models,Textures,VFX}/
docs/{Research,Compatibility,PrototypeAcceptance}/
```

Map this conceptual layout into the existing editor's required modification folder/assembly layout when building. Avoid embedding runtime controller copies in both packages. The UMM adapter should call a shared service only after the Owlmod reports content readiness; if one side is missing, present a clear disabled state.

Custom weapon VFX belong in the content package. Validate target-tier visual references, attached abilities, holster/drawn behavior, materials/shaders and bundle dependencies independently from quest correctness. Blueprint replacement changes the weapon identity but is not proof that its VFX initialize properly.

## 7. Verified API example and reconciliation pseudocode

This narrow fragment is consistent with the compile-verified calls and installed enum. It assumes a loaded player and already resolved blueprint arguments:

```csharp
using Kingmaker;
using Kingmaker.Blueprints.Quests;

static bool AuroraObjectiveCompleted(Player player, BlueprintQuestObjective safeShuttle)
    => player.QuestBook.GetObjectiveState(safeShuttle) == QuestObjectiveState.Completed;

static void StartLoreObjective(Player player, BlueprintQuestObjective reading)
    => player.QuestBook.GiveObjective(reading, false);
```

The state guard belongs in the caller; repeatedly starting an objective should not be used as a reconciliation strategy.

```text
PSEUDOCODE — functions below are proposed services, not Owlcat APIs.
reconcile(reason):
    require contentResolved && loadedPlayerReady && !reconciling
    migrateModOwnedState()
    observeSupportedVanillaMilestones()
    openFirstEligibleUnreadEpisodePerWeapon()
    updatePermanentEntitlementsForCurrentMode()
    if safeForInventoryChanges:
        normalizeAuthorizedWeaponsWithoutGrantingExtraCopies()
    deriveBookAvailabilityFromCurrentEpisodeAndArea()
    recordDiagnostics(reason)
```

`IQuestObjectiveHandler` is verified for prompt milestone observation. `Kingmaker.PubSubSystem.Core.IAreaLoadingStagesHandler` exposes `OnAreaScenesLoaded()` and `OnAreaLoadingComplete()` in installed metadata. The latter is a candidate safe reconciliation point, but confirm whether it fires after player restore on ordinary save load. Do not infer the entire save lifecycle from its name. The old public template's `EventBus.Subscribe` example also needs checking against current event routing; the local Ork mod uses reflected global subscription. Verify the supported current route rather than adopting reflection automatically.

## 8. Implementation roadmap and minimal prototype

1. **Freeze evidence.** Validate GUIDs against installed assets, inspect every SafeShuttle/Escape writer, and compare two actual saves. Record game/editor versions and dependency hashes.
2. **Prove content loading.** Register a disposable quest, objective, flag and simple dialogue with stable test IDs. Show the native journal entry and verify save/reload. No campaign trigger yet.
3. **Prove quarters placement.** Build only the additional mechanics scene and clickable native book. Enter, exit, save/reload in place, and repeat. Resolve entity identity and area variants before adding rewards.
4. **Connect milestone.** Observe SafeShuttle completion and reconcile existing saves. Never alter its vanilla graph. Test fresh and post-Rykad installation.
5. **Connect lore.** Three short pages and an explicit final acknowledgement. Save/close halfway, reopen, acknowledge twice, and verify exactly one read state and no duplicate journal entries.
6. **Connect pistol.** V1/V2 only. Implement guarded replacement with a documented initial instance-state policy. Test inventory, equipped slot and companion. Leave missing pistol pending.
7. **Connect mode selection.** UMM default plus per-save mode, monotonic entitlements and level catch-up. Test both switch directions and respec behavior.
8. **Harden.** Stash, reserve companions, duplicates, modifications, weapon sets, full inventory, partial failure and mode changes during unsafe states.
9. **Expand content.** Only after the prototype passes, add all three tutorial grants, later campaign milestones, intertwined lore and custom VFX.

The **minimal prototype** is one starport milestone, one pistol quest with reading/delivery state, one quarters book, one three-page Book dialogue, pistol V1→V2, and stable flags. It should include the reconciliation path from the beginning. UMM switching can be the next prototype increment, before full content production.

Acceptance matrix:

| Test | Required result |
| --- | --- |
| Install before starport battle | No episode before milestone; objective after completion. |
| Install after battle/later chapter | Episode offered retroactively from persistent state. |
| Kill unrelated/reused unit blueprint | No false milestone. |
| Load already inside quarters | One book or a clearly documented next-entry activation; never duplicate entities. |
| Enter/leave/reload repeatedly | One presentation, no repeated reward or state reset. |
| Abort lore / select final answer twice | Abort remains retryable; acknowledgement grants entitlement once. |
| Pistol on protagonist/companion/inventory | Correct target blueprint and owner/slot; no extra copy. |
| Pistol missing, then reacquired | Reading preserved; delivery remains pending then reconciles. |
| Duplicate lower-tier pistols | Each authorized copy normalizes; no new pistol is created as reward. |
| Incompatible modification or failed insertion | Original retained/restored; explicit diagnostic, no false delivery claim. |
| Save immediately before/after delivery | Reload shows a consistent item and entitlement state. |
| Story↔Level, high level, respec | No downgrade, repeated reading reward or progression loss. |
| Load a second campaign in same session | No state/inventory references leak across saves. |

Major conflicts to test are other mods replacing the same area/quest assets, threaded/early blueprint loading, two runtime instances, changed game GUIDs or schemas, competing weapon-normalization mods, missing bundles, stale map-object visibility and incompatible dynamic item modifications. Avoid vanilla replacements where read-only observation and new assets suffice.

## 9. Sources and verification locations

Public primary sources consulted:

- [Owlcat RTModificationTemplate](https://github.com/OwlcatOpenSource/RTModificationTemplate): official blueprint/content packaging, built-in UMM, scenes, events and persistence examples. Treat legacy snippets as leads; current installed assemblies take precedence.
- [ToyBox Rogue Trader source tree](https://github.com/xADDBx/ToyBox-RogueTrader/tree/main/ToyBox/Classes): independent mod implementation to inspect for current lifecycle/configuration and quest/etude tools.
- [ToyBox project documentation and changelog](https://github.com/xADDBx/ToyBox-RogueTrader/blob/main/ToyBox/ReadMe.md): identifies native quest/etude tooling and documents historical loading/per-save timing conflicts. It is not proof of this mod's compatibility.

Local primary evidence:

- Export root: `/Users/poffl/poffl-forge/mods/mac-modding/RogueTrader-Modding/Blueprints`. Exact asset paths are given above; `research-evidence-2026-10-22.json` records absolute locations, hashes, GUIDs and relevant action nodes.
- Installed assemblies: `/Users/poffl/Library/Application Support/Steam/steamapps/common/Warhammer 40,000 Rogue Trader/WH40KRT.app/Contents/Resources/Data/Managed/`. Inspect `Code.dll` for quests, etudes, conditions, interactions, dialogue and item APIs; inspect `RogueTrader.GameCore.dll` and referenced blueprint/entity assemblies for moved types and storage behavior. Search by fully qualified type, not by assumptions from Pathfinder.
- Editor interaction inspectors: `Assets/Code/GameCore/Editor/UIElements/Custom/InteractionActionInspectorLayout.cs` references the actual `InteractionAction.Settings` object; condition factory `.../Elements/SmartElementPopulation/Factories/Conditions/EtudeStatusFromEtudeBlueprintFactory.cs` demonstrates the etude condition namespace and playing-state setup.
- Local replacement precedent: `/Users/poffl/poffl-forge/mods/armoury-orks/Assets/Modifications/ArmouryOrks/Scripts/ChoppaProgressionController.cs`, particularly `TryUpgradeItemInSeries` and collection discovery. The shortcomings described above are visible in that source.
- Existing audits/probes: `docs/weapon-quest-feasibility.md`, `docs/progression-feasibility.md`, `src/TechnicalChecks/QuestApiProbe.cs`, `ProgressionApiProbe.cs`, `QuestMetadataCheck.cs`, and `compile_probe.py`. The earlier save archive inspection is recorded in the audit; this turn reran assembly checks, not that archive inspection.

Radiance is a useful conceptual precedent for story-dependent tiers, but no Pathfinder-only replacement API is assumed applicable here. Rogue Trader's own objective, area mechanics and Book dialogue data provide the stronger foundation. A Radiance implementation was not independently verified in this research and is not used as technical evidence.
