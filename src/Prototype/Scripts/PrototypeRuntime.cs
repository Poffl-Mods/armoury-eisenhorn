using System;
using Kingmaker;
using Kingmaker.AreaLogic.Etudes;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Quests;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.GameModes;
using Kingmaker.Modding;
using UnityEngine;

namespace ArmouryEisenhorn.Prototype
{
    // All campaign state belongs to the player's native quest book / etudes system.
    // This host retains no Player, collection, completed flag or current-save reference.
    public sealed class PrototypeRuntime : MonoBehaviour
    {
        public const string MilestoneId = "b653839065a647a08b5721e077da23e1";
        public const string ObjectiveId = "b72f12cf229b501ca5fef51a60373d6b";
        public const string EtudeId = "c9d6c0e2392251cb89eadb2e908b2f41";
        private float nextCheck;
        private string lastError;
        private static OwlcatModification owner;

        [OwlcatModificationEnterPoint]
        public static void Initialize(OwlcatModification modification)
        {
            owner = modification;
            if (FindFirstObjectByType<PrototypeRuntime>() != null) return;
            var host = new GameObject("EisenhornQuestPrototype_Runtime");
            DontDestroyOnLoad(host);
            host.AddComponent<PrototypeRuntime>();
            Log("Initialized 0.1.0. Native mechanics scene prototype; no Harmony patches.");
        }

        public static bool IsSafeWorld()
        {
            var game = Game.Instance;
            return game != null && game.Player != null && game.CurrentlyLoadedArea != null
                && game.CurrentMode == GameModeType.Default
                && !(LoadingProcess.Instance?.IsLoadingInProcess ?? true)
                && !game.Player.IsInCombat;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            if (!IsSafeWorld()) return;
            try
            {
                var player = Game.Instance.Player;
                var milestone = ResourcesLibrary.TryGetBlueprint(MilestoneId) as BlueprintQuestObjective;
                var objective = ResourcesLibrary.TryGetBlueprint(ObjectiveId) as BlueprintQuestObjective;
                var etude = ResourcesLibrary.TryGetBlueprint(EtudeId) as BlueprintEtude;
                if (milestone == null || objective == null || etude == null)
                    throw new InvalidOperationException("Required milestone/custom blueprint is unavailable; no state changed.");
                var state = player.QuestBook.GetObjectiveState(objective);
                if (state == QuestObjectiveState.Completed)
                {
                    // Completing the objective happens inside the Book dialogue. Removing
                    // mechanics is deferred until dialogue/cutscene/loading has finished.
                    if (!player.EtudesSystem.EtudeIsNotStarted(etude)
                        && !player.EtudesSystem.EtudeIsCompleted(etude)
                        && !player.EtudesSystem.EtudeIsPreCompleted(etude))
                    {
                        player.EtudesSystem.MarkEtudeCompleted(etude, "Eisenhorn prototype acknowledged");
                        Log("Reading complete; requested native etude/mechanics teardown.");
                    }
                    return;
                }
                if (state == QuestObjectiveState.Failed) return;
                if (player.QuestBook.GetObjectiveState(milestone) != QuestObjectiveState.Completed) return;
                if (player.EtudesSystem.EtudeIsNotStarted(etude))
                {
                    player.EtudesSystem.StartEtude(etude, "Eisenhorn prototype milestone reconciliation");
                    Log("Observed completed servitor objective; started mod-owned etude.");
                }
                // Native EtudePlayTrigger gives the objective. Its saved once-only action
                // is supplemented by this idempotent repair for late installs/load timing.
                if (state == QuestObjectiveState.None && !player.EtudesSystem.EtudeIsCompleted(etude))
                {
                    player.QuestBook.GiveObjective(objective, false);
                    Log("Opened native Eisenhorn test objective.");
                }
                lastError = null;
            }
            catch (Exception ex)
            {
                if (lastError == ex.Message) return;
                lastError = ex.Message;
                Log("ERROR: " + ex);
            }
        }

        public static void Log(string message)
        {
            owner?.Logger.Log("[EisenhornPrototype] " + message);
            Debug.Log("[EisenhornPrototype] " + message);
        }
    }
}
