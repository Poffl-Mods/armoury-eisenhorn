# Eisenhorn Quest System Prototype 0.1.0: test walkthrough

Built and installed through the Mac-ModInstaller UI on 22 October 2026. The installer recognized all three bundles as macOS, reported successful installation/activation, and retained its backup. All 31 installed files match the built ZIP byte-for-byte. The game was not launched by the agent; the behavior below is the intended acceptance test, not a report of an in-game pass.

## First test: new game or early prologue save

1. Start a new game, or load an early prologue save before the first servitor fight on the officers' deck.
2. Play normally through the objective **“Destroy the out-of-control servitor.”** Finish the conversation that follows and wait until you can move freely. Unpause and allow a few seconds for reconciliation and the native Mechanics Scene reload.
3. Look for the journal quest **“Eisenhorn: Quest System Test”**, with objective **“Read the Eisenhorn test document.”**
4. A small gold/parchment rectangular marker should appear at the protagonist's feet. Move a step away so it is easy to click. Its interaction name is **“Eisenhorn Test Document.”** The position is deliberately relative to your character when the scene loads, rather than a fixed table location. It does not follow you afterward.
5. Click the marker. A native Book dialogue should display **“Test Page 1 – Eisenhorn Quest System.”** Select **“Read the next page.”** to reach **“Test Page 2 – Story Progression”** and **“Test Page 3 – Quest Complete.”**
6. Select **“Acknowledge and complete the quest.”** The journal objective and parent quest should finish. Selecting Page 3 alone must not finish it.
7. After the Book dialogue closes and normal play resumes, the controller requests native etude completion. Its additional scene should be removed through the engine's mechanics reload. The interaction condition rejects further use as soon as the objective is completed, even before teardown finishes.

The old internal asset name `02_KillTheodorasPet` and dialogue folder `AfterKillingBeast` are legacy names. The exported English localization identifies the encounter as the out-of-control servitor; there is no separate beast you need to find.

## Cancellation and persistence checks

- On Page 1 or Page 2, choose **“Close the document. I will return later.”** The quest must remain active. Reopen the document: it should restart at Page 1. Page-by-page bookmarks are outside this prototype.
- Save while the quest is active but outside dialogue. Reload, unpause, and check that the same objective is active and only one document is present. Its placement is reset to your protagonist's position when its scene loads.
- Complete the reading, create a separate test save, and reload. The quest must remain completed; the document must not return.
- Load another campaign before the servitor milestone in the same game session. No Eisenhorn quest/document should activate there until that campaign reaches the milestone.
- Revisit a supported area or reload repeatedly. There must be no duplicate objective or additional document copies.

## Existing-save test

Load a save after completing the servitor milestone. The runtime reads the native objective's saved state and starts the custom quest retroactively. It does not modify the vanilla quest or rewrite save JSON.

The document scene is attached to these supported areas:

- The prologue officers' deck.
- The prologue Rogue Trader cabin / bridge area.
- The post-prologue officers' deck, including the quarters visit.

If you load elsewhere, the journal quest can become available immediately, but visit one of those areas to find the document. For a later campaign, enter the post-prologue officers' deck/quarters and look at your character's feet after normal control returns. It is deliberately available anywhere in that area for this proof of concept.

Retroactive activation depends on the original servitor objective still being recorded as Completed. A save with that objective None/Failed does not qualify. No chapter-number guess or SaveUpgrader is used. A read-only check of the latest local save found this objective serialized with state None; that particular save was not evidence of post-milestone behavior. No completed-save runtime test has yet been performed.

## Logs and troubleshooting

The game log is:

`/Users/poffl/Library/Logs/Owlcat Games/Warhammer 40000 Rogue Trader/Player.log`

Search for `[EisenhornPrototype]`. Expected messages include initialization, observation of the completed servitor objective, opening of the native test objective, the native document's area/position, and the completion/teardown request. Errors are prefixed `ERROR` and repeated identical errors are suppressed.

- **No initialization message:** check that Mac-ModInstaller shows the prototype enabled and inspect preceding Owlmod/assembly-loading errors.
- **Quest absent:** make sure the native servitor objective is completed, play is unpaused, no dialogue/cutscene/loading is active, and a few seconds have elapsed. Do not force vanilla quest completion to make the test pass.
- **Quest active, document absent:** visit a supported area. Look for the document-ready area/position message and for mechanics-scene, assembly, shader or missing-reference errors. Save/reload outside dialogue is a useful separate test, not proof of a fix.
- **Marker visible but not clickable:** capture the interaction, map-object and conditions errors. Native selection/collider/approach behavior still needs the first gameplay test.
- **Completion recorded but marker remains:** wait until normal unpaused play resumes. Check for the teardown message. Reload a test save to distinguish live teardown from completed-state restoration. Report either failure rather than treating installation as proof of scene removal.

Build log: `build/unity-build.log` in this repository. Installation proof: `docs/prototype-install-verification.json`. Do not overwrite a valued campaign save solely for this test; use a separate test save when you test persistence.

## Current limits

No weapons, rewards, level/story toggle, final lore, custom model/VFX, multiplayer support, or SaveUpgraders are included. All six supplied locale packs use English placeholder text. The document is a scene-authored native object with a small placement component; fixed furniture placement is a later iteration.

Native blueprint deserialization, scene wiring, compile checks, metadata checks, package contents and installation are verified. Journal rendering, actual interaction, Book UI, live scene teardown, retroactive activation, duplicate prevention across reloads, and save/load restoration still require your in-game tests.
