// Read-only checks of the native contracts used by the scene and JSON graph.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
class PrototypeMetadataCheck
{
    static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> types)
    { foreach (var t in types) { yield return t; foreach (var n in All(t.NestedTypes)) yield return n; } }
    static void Check(bool value, string label)
    { if (!value) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); }
    static bool Calls(TypeDefinition type, string method, string target)
        => type.Methods.Any(m => m.Name == method && m.HasBody && m.Body.Instructions.Any(i => i.Operand != null && i.Operand.ToString().Contains(target)));
    static void Main(string[] args)
    {
        var resolver = new DefaultAssemblyResolver(); resolver.AddSearchDirectory(args[0]);
        var modules = new List<ModuleDefinition>();
        try
        {
            foreach (var name in new[] { "Code", "RogueTrader.GameCore", "Kingmaker.ElementsSystem.Interfaces", "Kingmaker.Blueprints.Base" })
                modules.Add(ModuleDefinition.ReadModule(Path.Combine(args[0], name + ".dll"), new ReaderParameters { AssemblyResolver = resolver }));
            var types = modules.SelectMany(m => All(m.Types)).Where(t => t.Name != "<Module>").GroupBy(t => t.FullName).ToDictionary(g => g.Key, g => g.First());
            var ids = new Dictionary<string, string>();
            foreach (var type in types.Values)
                foreach (var attr in type.CustomAttributes.Where(a => a.AttributeType.Name == "TypeIdAttribute"))
                    if (attr.ConstructorArguments.Count > 0)
                        ids[attr.ConstructorArguments[0].Value.ToString() + ", " + type.Name] = type.FullName;
            var pattern = new Regex("\\\"\\$type\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
            var serializedTypes = Directory.GetFiles(args[1], "*.jbp").SelectMany(p => pattern.Matches(File.ReadAllText(p)).Cast<Match>().Select(m => m.Groups[1].Value)).Distinct().ToArray();
            foreach (var id in serializedTypes) Check(ids.ContainsKey(id), "Installed TypeId matches " + id);
            var etudes = types["Kingmaker.AreaLogic.Etudes.EtudesSystem"];
            Check(Calls(etudes, "UpdateEtudes", "ReloadAreaMechanics"), "Native etude update requests mechanics reload when its scene set changes");
            var interaction = types["Kingmaker.View.MapObjects.InteractionActionPart"];
            Check(Calls(interaction, "CanInteract", "ConditionsReference") || Calls(interaction, "CanInteract", "ConditionsHolder"), "Native action interaction evaluates a conditions holder");
            Check(Calls(interaction, "OnInteract", "ActionsHolder::Run"), "Native action interaction runs an ActionsHolder");
            Check(types["Kingmaker.DialogSystem.Blueprints.BlueprintAnswer"].Fields.Any(f => f.Name == "OnSelect"), "Final answer supports native selection actions");
            Check(types["Kingmaker.AreaLogic.Etudes.EtudesSystem"].Fields.Any(f => f.Name == "m_EtudesData" && f.CustomAttributes.Any(a => a.AttributeType.Name == "JsonPropertyAttribute")), "Native etude states are serialized");
        }
        finally { foreach (var module in modules) module.Dispose(); }
    }
}
