# Quest prototype 0.1.0: implementation and verification

22 October 2026. This is a local proof of concept, built and installed through the native Mac-ModInstaller UI. The game was deliberately not launched. See `prototype-testing.md` for the acceptance walkthrough and `prototype-install-verification.json` for installed file hashes.

## Architecture and rationale

The prototype is a single Owlmod, `EisenhornQuestPrototype`. A UMM adapter would add no useful behavior at this stage because there are no configurable progression modes or weapon rewards. The approved MicroPatches 1.21.1 runtime/editor integration remains the baseline. No Harmony patch, vanilla blueprint patch, custom save type, SaveUpgrader, or original-game-file edit is used.

Native content owns quest persistence, dialogue actions and object registration. A small C# controller reconciles existing campaign state, starts the mod-owned etude, and defers etude completion until the Book dialogue has closed. A separate component positions the native scene-authored document at the protagonist's ground position when that scene loads. This is intentionally dynamic placement of an existing scene entity, not a runtime object-spawning fallback.

The mod's etude has no vanilla parent. ADDB's observation about parent lifecycle is correct: installed `EtudesSystem.StartEtude` checks the parent state and can refuse activation when a parent is completed. Making this test a child of a short-lived prologue etude would jeopardize retroactive activation. Instead, both reconciliation and the custom etude's native `ObjectiveStatus` activation condition read a durable quest objective. No vanilla etude needs modification.

`EtudeStatus` remains a valid alternative for a different milestone if its saved lifecycle proves the event. Started/playing/completed cannot be substituted indiscriminately. This prototype uses the concrete objective transition that the exported conversation already performs.

## Exact milestone

`World/Quests/Chapter 0/02_KillTheodorasPet.jbp`, GUID `b653839065a647a08b5721e077da23e1`.

The internal filename is historical. Its exported English title is **“Destroy the out-of-control servitor.”** `World/Dialogs/Ch0_Prologue/AfterKillingBeast/Cue_0002.jbp` completes it in `OnShow` and starts the subsequent native conversation objective. Reconciliation waits for normal play, so activation does not interrupt that conversation.

The runtime does not listen only for a new death/dialogue event: it calls `QuestBook.GetObjectiveState` on the current loaded player. Therefore a completed milestone can be recognized after installation or reload. A read-only inspection of the latest local archive confirmed this objective is represented under the saved quest's `PersistentObjectives`; its state in that particular save was None. This was not a completed-save integration test.

## Native flow and safety

1. `OwlcatModificationEnterPoint` initializes one persistent controller. It stores no player, item or campaign progression references.
2. Once per second, only in normal unpaused play, outside combat/loading, it resolves the milestone, mod objective and mod etude.
3. If the milestone is Completed and the etude has not started, it calls `Player.EtudesSystem.StartEtude`. The native etude also has that Completed condition.
4. `EtudePlayTrigger` gives the custom objective, guarded by its state being None. The controller provides the same None-state repair for load/activation ordering.
5. The etude's `m_AddedAreaMechanics` references three mod-owned mechanics blueprints, one for each supported area. All point at the one authored mechanics scene.
6. `EisenhornDocumentView` inherits native `MapObjectView`; `EisenhornDocumentInteraction` inherits native `InteractionAction`. The Selectable-layer trigger collider, highlight, approach radius, display string, conditions reference and action reference are authored into the Unity scene.
7. `InteractionActionSettings.Condition` references an `ObjectiveStatus(Started)` conditions holder. Its action holder executes native `StartDialog` for a `BlueprintDialog` of Type Book.
8. Three `BlueprintBookPage` assets each display a cue and answers. Next-page answers advance the graph. A reusable cancel answer ends the dialogue without actions.
9. Only the final acknowledgement's `OnSelect` calls `SetObjectiveStatus(Complete)` on the owned objective. The objective finishes its owned parent quest.
10. The condition now rejects further document interaction. After normal play resumes, the controller requests `MarkEtudeCompleted`, excluding already completed/precompleted etudes. Native etude updating handles the mechanics change.

Installed IL verifies that `EtudesSystem.UpdateEtudes` compares the active additional-mechanics sets and calls `Game.ReloadAreaMechanics(false, false)` when they differ. This is stronger evidence than assuming an object disappears on an eventual area reload, but actual live removal remains an in-game acceptance test.

Native quest/objective and etude states are serialized by the game. Stable blueprint GUIDs, scene GUID and entity ID live in source and survive regeneration. Completion is not stored in a static C# field or MonoBehaviour field. Loading another campaign reads that campaign's quest book and etudes anew.

The object is placed once per scene instance at the protagonist's ground position, with a raised visual marker. It stays there until scene unload; loading that scene again places it by the newly loaded protagonist. No fixed coordinates, walkability guess or vanilla locator dependency is required. Supported area GUIDs are recorded in the three mechanics blueprints: original prologue officers' deck, prologue cabin/bridge, and post-prologue officers' deck. Behavior when area parts change without the scene reloading still needs gameplay verification.

## Implemented files

| Location | Purpose |
| --- | --- |
| `src/Prototype/Scripts/PrototypeRuntime.cs` | Startup, safe-state checks, current-save milestone reconciliation and deferred native etude completion. |
| `src/Prototype/Scripts/EisenhornDocumentView.cs` | Mod-defined native map-object subclass for scene serialization. |
| `src/Prototype/Scripts/EisenhornDocumentInteraction.cs` | Mod-defined native action-interaction subclass. |
| `src/Prototype/Scripts/EisenhornDocumentAnchor.cs` | Moves the scene-authored native entity to the protagonist once per scene load. |
| `src/Prototype/Blueprints` | 19 authored native blueprints: quest, objective, etude, three mechanics, dialogue, three pages, three cues, four answers, actions holder and conditions holder. |
| `src/Prototype/Content` | Authored Unity scene, parchment material, localized interaction-name asset, and stable Unity metadata. |
| `src/Prototype/Localization` | Six locale packs with English placeholder text. |
| `src/Prototype/StableIds.json` | Fixed identities, including scene/entity IDs. |
| `src/Prototype/NativeShaderEvidence.json` | Installed native shader bundle hash and independently inspected shader-name evidence; build-time only. |
| `src/Editor` | Prototype-only editor assembly and build/scene-validation helper. |
| `tools/generate_prototype_content.py` | Reproducible authoring from native exported structures; writes only project source. |
| `tools/check_prototype.py` | Graph, reference, localization, package and dependency checks. |
| `tools/compile_prototype.py` | Compile-only validation against installed assemblies. |
| `tools/inspect_native_shader_bundle.py` | Read-only bounded UnityFS/LZ4 audit of native shader names on macOS. |
| `src/TechnicalChecks/PrototypeMetadataCheck.cs` | Installed TypeId, native interaction, mechanics reload and etude serialization checks. |

No custom weapon or upgrade is delivered by this prototype.

## Build process

The existing project is `/Users/poffl/poffl-forge/mods/mac-modding/RogueTrader-Modding`, using Unity `6000.0.64f1` and its existing packages/editor integration.

Source is staged under `Assets/Modifications/EisenhornQuestPrototype`; the separate editor helper assembly is under `Assets/EisenhornPrototypeEditor`. Its references include the prototype runtime, `Mods.Editor`, and the installed Scriptable Build Pipeline assemblies. The shared template's files are unchanged.

The build uses native `OwlcatModification.Editor.Build.Builder.Build`, normal script compilation, localization preparation, blueprint copying and bundle generation. Output is isolated in `EisenhornPrototypeBuildOutput` rather than the shared default Build directory, which other mod builds replace.

The helper additionally normalizes the actual bundle dependency graph after the standard build. The installed template's `CreateManifestAndSettings` recognizes only a `Bundles\\` prefix, so its ordinary macOS output had an empty dependency list. The local helper reads `BundleBuildResults`, handles either separator, checks the dependency files, writes the completed native settings and repacks with the same ZIP writer as the template's `PackArtifacts`. This does not bypass installation or alter the shared build integration.

The resulting scene bundle depends on `EisenhornQuestPrototype_content` and native `shaders`; the content bundle depends on native `shaders`. The shader library is supplied by the game and must not be replaced with a stripped shader bundle produced by the template. Its native UnityFS data was read/decompressed without changing it: `Owlcat/Unlit` and `Owlcat/Lit` names were found. `PF/Unlit` was absent, so the parchment uses current `Owlcat/Unlit`. This native shared library has no addressable root shader assets; root `LoadAllAssets<Shader>()` enumeration was not a valid absence test. The build checks the exact audited bundle hash.

Example rebuild once sources are staged and no other instance holds the project:

```sh
/Applications/Unity/Hub/Editor/6000.0.64f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -buildTarget osx \
  -projectPath /Users/poffl/poffl-forge/mods/mac-modding/RogueTrader-Modding \
  -executeMethod EisenhornPrototypeBuild.Build \
  -logFile /tmp/eisenhorn-unity-build.log
```

The helper's interactive menu requires a saved editor scene because it creates the authored scene additively; the batch path handles Unity's empty startup scene separately. It does not close or overwrite a user's dirty scene.

The prototype build succeeded despite shared-environment Burst hashing diagnostics referencing the unrelated Ork editor/Harmony assembly, and a shared build-log disposal exception. No dependency was updated to suppress those diagnostics. The native build returned Success, the resulting ZIP and dependency graph passed checks, and the installer accepted all three bundles as macOS. The full build log is retained locally in `build/unity-build.log`; these diagnostics are not presented as an in-game pass.

## Installation and validation results

The native Mac-ModInstaller UI opened `build/EisenhornQuestPrototype.zip`, previewed the destination and three macOS bundle targets, and installed/enabled it. No manual copying into the game or mod directories, new installer script, or ModCore fallback was used.

Destination:

`/Users/poffl/Library/Application Support/Owlcat Games/Warhammer 40000 Rogue Trader/Modifications/EisenhornQuestPrototype`

Validation completed:

- Prototype C# compiles against the installed game assemblies.
- Existing API probes compile; all eleven existing quest/persistence metadata checks pass.
- Fifteen serialized native TypeId/name pairs match installed assembly metadata; five further native interaction/etude contracts pass.
- Owlcat's own editor `BlueprintJsonWrapper.Load` deserializes all 19 authored blueprints without a `BlueprintBroken` result.
- Native scene entity identity, component scripts, action/condition references, layer and interaction wiring are checked by the build helper.
- Authored graph checks confirm cancellation, final-acknowledgement-only completion, localization coverage and reference closure.
- Package CRC, all authored blueprint bytes, one runtime assembly, three bundles, and the complete native dependency graph pass.
- The installer reports success and activation; all 31 installed files match the ZIP hashes; activation and installer backup are verified separately.

The generated installation record contains the exact ZIP hash and backup path. This turn never launched Rogue Trader and never wrote a save archive. Native journal rendering, Book presentation, selection/approach behavior, scene teardown, completed-save retrofit and save/reload roundtrips are still untested.

## ADDB suggestions and deferred decisions

**SaveUpgraders:** not needed for this prototype. Normal saved objective/etude inspection supplies the required state without introducing a JSON migration dependency. Add one later only for a specific incompatible saved representation that ordinary reconciliation cannot repair.

**Weapon actions:** installed metadata confirms both `Kingmaker.Designers.EventConditionActionSystem.Actions.AddItemToPlayer` and `RemoveItemFromPlayer`. The add action includes equip/owner/preferred-set options, so native actions are more capable than a plain inventory grant. However, their inputs are blueprint/quantity/equip policy rather than an explicit source-instance snapshot. A naive remove/add sequence still does not establish preservation of modifications, exact prior slot, ammunition, duplicates or rollback. No weapon action or replacement service is needed in this quest-only slice; compare and test those choices when adding the first pistol reward.

**Radiance:** no verified WotR game data was available in this workspace. No Pathfinder-only API or assumed Radiance implementation was imported. Rogue Trader's own quest, Book and area-mechanics structures were sufficient for this prototype.

Next work should respond to the first in-game results: verify native scene loading and interaction, then cancellation, completion/teardown, and save/load on separate test saves. Only after those pass should fixed placement, a pistol reward, and configurable progression modes be introduced.
