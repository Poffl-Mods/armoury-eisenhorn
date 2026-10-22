// Read-only inspection of installed assemblies. Requires Mono.Cecil, supplied externally.
using System;
using System.Linq;
using Mono.Cecil;
class QuestMetadataCheck
{
    static System.Collections.Generic.IEnumerable<TypeDefinition> All(System.Collections.Generic.IEnumerable<TypeDefinition> types)
    {
        foreach (var t in types) { yield return t; foreach (var n in All(t.NestedTypes)) yield return n; }
    }
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }
    static bool Calls(TypeDefinition type, string method, string target)
        => type.Methods.Any(m => m.Name == method && m.HasBody && m.Body.Instructions.Any(i => i.Operand != null && i.Operand.ToString().Contains(target)));
    static bool SavedField(TypeDefinition type, string field)
        => type.Fields.Any(f => f.Name == field && f.CustomAttributes.Any(a => a.AttributeType.FullName == "Newtonsoft.Json.JsonPropertyAttribute"));
    static void Main(string[] args)
    {
        using (var code = ModuleDefinition.ReadModule(args[0]))
        {
            var types = All(code.Types).ToDictionary(t => t.FullName);
            var player = types["Kingmaker.Player"];
            var entry = types["Kingmaker.QuestBookEntityEntry"];
            var quest = types["Kingmaker.AreaLogic.QuestSystem.Quest"];
            var book = types["Kingmaker.AreaLogic.QuestSystem.QuestBook"];
            Check(SavedField(player, "m_QuestBook"), "Player quest book is serialized");
            Check(SavedField(player, "m_UnlockableFlags"), "Player milestone/claim flags are serialized");
            Check(SavedField(entry, "m_State") && SavedField(entry, "m_IsVisible"), "Objective state and visibility are serialized");
            Check(SavedField(quest, "m_State"), "Quest state is serialized");
            Check(quest.Properties.Any(p => p.CustomAttributes.Any(a => a.AttributeType.FullName == "Newtonsoft.Json.JsonPropertyAttribute") && p.Name == "PersistentObjectives"), "Persistent objectives are serialized");
            Check(entry.Methods.Any(m => m.Name == "OnPostLoad") && quest.Methods.Any(m => m.Name == "OnPostLoad"), "Quest and objective have post-load handling");
            Check(Calls(quest, "OnObjectiveFinished", "get_NextObjectives") && Calls(quest, "OnObjectiveFinished", "OnObjectiveOpened"), "Completed objectives advance native chained stages");
            Check(Calls(book, "GiveObjective", "::Start("), "GiveObjective starts native objective entries");
            Check(Calls(types["Kingmaker.Designers.GameHelper/Quests"], "GetList", "QuestBook::get_Quests"), "Journal helper reads player quest book");
            Check(Calls(types["Kingmaker.Code.UI.MVVM.VM.ServiceWindows.Journal.JournalVM"], ".ctor", "GameHelper/Quests::GetList"), "Normal journal uses native quest list");
            var trigger = types["Kingmaker.Designers.EventConditionActionSystem.ObjectiveEvents.ObjectiveStatusTrigger"];
            Check(trigger.Methods.Any(m => m.Name.EndsWith("IQuestObjectiveLogic.OnCompleted") && m.HasBody && m.Body.Instructions.Any(i => i.Operand != null && i.Operand.ToString().Contains("ActionList::Run"))), "Objective completion can execute reward actions");
        }
    }
}
