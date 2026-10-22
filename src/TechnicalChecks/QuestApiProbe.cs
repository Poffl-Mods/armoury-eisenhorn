// Compile-only evidence for the native journal APIs; not installed or executed in-game.
using System;
using Kingmaker;
using Kingmaker.AreaLogic.QuestSystem;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Quests;
using Kingmaker.Designers.EventConditionActionSystem.ObjectiveEvents;
using Kingmaker.ElementsSystem;
using Kingmaker.PubSubSystem;

namespace ArmouryEisenhorn.TechnicalChecks
{
    public sealed class QuestApiProbe : IQuestObjectiveHandler
    {
        // An actual controller must filter for this mod's GUIDs, handle deferred rewards
        // and reconcile completed-but-unclaimed upgrades after loading a save.
        public Action<QuestBookEntityEntry> OnCompletion;
        public void HandleQuestObjectiveStarted(QuestBookEntityEntry entry, bool autoStart) { }
        public void HandleQuestObjectiveBecameVisible(QuestBookEntityEntry entry, bool autoStart) { }
        public void HandleQuestObjectiveCompleted(QuestBookEntityEntry entry)
            => OnCompletion?.Invoke(entry);
        public void HandleQuestObjectiveFailed(QuestBookEntityEntry entry) { }

        public static void LinkStages(BlueprintQuest quest, BlueprintQuestObjective[] stages)
        {
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i].SetQuest(quest);
                quest.LinkObjective(stages[i]);
                stages[i].IsHidden = false;
                stages[i].SetIsFinishParent(i == stages.Length - 1);
                if (i + 1 < stages.Length) stages[i].AddNextObjective(stages[i + 1]);
            }
        }
        public static void Start(Player player, BlueprintQuestObjective first)
            => player.QuestBook.GiveObjective(first, false);
        public static void Complete(Player player, BlueprintQuestObjective stage)
            => player.QuestBook.CompleteObjective(stage);
        public static QuestObjectiveState State(Player player, BlueprintQuestObjective stage)
            => player.QuestBook.GetObjectiveState(stage);
        public static Quest GetQuest(Player player, BlueprintQuest quest)
            => player.QuestBook.GetQuest(quest);
        public static ObjectiveStatusTrigger CompletionActions(ActionList actions)
            => new ObjectiveStatusTrigger { objectiveState = QuestObjectiveState.Completed, Actions = actions };
        public static void RecordClaim(BlueprintUnlockableFlag claim)
            => claim.Unlock();
    }
}
