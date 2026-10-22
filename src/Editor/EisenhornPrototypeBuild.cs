using System;
using System.IO;
using System.Linq;
using ArmouryEisenhorn.Prototype;
using Kingmaker.Localization;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints;
using Kingmaker.ElementsSystem;
using Kingmaker.View.MapObjects;
using Newtonsoft.Json.Linq;
using OwlcatModification.Editor;
using OwlcatModification.Editor.Build;
using OwlcatModification.Editor.Build.Context;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using RogueTrader.SharedTypes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds only. Installation must always go through Mac-ModInstaller.
public static class EisenhornPrototypeBuild
{
    const string Root = "Assets/Modifications/EisenhornQuestPrototype";
    const string ScenePath = Root + "/Content/EisenhornPrototype_Mechanics.unity";

    [MenuItem("Eisenhorn Prototype/Build macOS prototype")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Cannot build the prototype in Play mode.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
            throw new InvalidOperationException("The established project must target StandaloneOSX; no target/dependency changes were made.");
        var ids = JObject.Parse(File.ReadAllText(Root + "/StableIds.json"));
        Directory.CreateDirectory(Root + "/Content");
        AssetDatabase.Refresh();
        InspectNativeContracts();
        BuildScene(ids);
        var mod = AssetDatabase.LoadAssetAtPath<Modification>(Root + "/EisenhornQuestPrototype.asset");
        if (!mod)
        {
            mod = ScriptableObject.CreateInstance<Modification>();
            AssetDatabase.CreateAsset(mod, Root + "/EisenhornQuestPrototype.asset");
        }
        mod.Manifest.UniqueName = "EisenhornQuestPrototype";
        mod.Manifest.Version = "0.1.0";
        mod.Manifest.DisplayName = "Eisenhorn: Quest System Prototype";
        mod.Manifest.Author = "Poffl";
        mod.Manifest.Description = "Native tutorial quest, additional mechanics scene and three-page Book dialogue. No weapons or vanilla patches.";
        EditorUtility.SetDirty(mod);
        AssetDatabase.SaveAssets();
        ValidateScene(ids);
        var results = new BundleBuildResults();
        var runtimeSettings = new DefaultModificationRuntimeSettings();
        const string output = "EisenhornPrototypeBuildOutput";
        var result = Builder.Build(mod.Manifest, mod.Settings, Root, output, results, runtimeSettings);
        if ((int)result < 0) throw new Exception("Owlcat build failed: " + result);
        FinalizeDependencies(output, results, runtimeSettings);
        File.WriteAllText("EisenhornPrototypeBuildResult.txt", DateTime.UtcNow.ToString("O") + "\n" + result);
        Debug.Log("[EisenhornPrototypeBuild] SUCCESS " + Path.GetFullPath(output + "/EisenhornQuestPrototype.zip"));
    }

    public static void InspectNativeContracts()
    {
        foreach (var file in Directory.GetFiles(Root + "/Blueprints", "*.jbp"))
        {
            var wrapper = BlueprintJsonWrapper.Load(file, null);
            if (wrapper?.Data == null || wrapper.Data is BlueprintBroken)
                throw new Exception("Native JSON deserialization failed: " + file);
        }
        Debug.Log("[EisenhornPrototypeBuild] All 19 native blueprint JSON documents deserialized successfully.");
    }

    // The shared template's settings task recognizes only "Bundles\\" paths.
    // Read the real build graph and normalize both separators locally; leave the
    // template intact. Repack with the same writer used by its PackArtifacts task.
    static void FinalizeDependencies(string output, BundleBuildResults results, DefaultModificationRuntimeSettings settings)
    {
        // Native shared shader libraries have no addressable root assets. Verify
        // the hash of the independently audited serialized data instead of treating
        // LoadAllAssets<Shader>() returning zero as evidence of missing shaders.
        var evidence = JObject.Parse(File.ReadAllText(Root + "/NativeShaderEvidence.json"));
        var nativeShaders = (string)evidence["bundlePath"];
        if (!File.Exists(nativeShaders)) throw new Exception("Required native shaders bundle is absent.");
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var stream = File.OpenRead(nativeShaders))
        {
            var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (hash != (string)evidence["sha256"])
                throw new Exception("Native shaders bundle changed; rerun the read-only shader audit.");
        }
        if (!evidence["serializedShaderNamesFound"].Values<string>().Contains("Owlcat/Unlit"))
            throw new Exception("Required native shader name has not been audited.");
        var folder = Path.Combine(output, "EisenhornQuestPrototype");
        string Normalize(string p) => p.Replace('\\', '/').StartsWith("Bundles/") ? p.Replace('\\', '/').Substring(8) : p.Replace('\\', '/');
        foreach (var pair in results.BundleInfos)
        {
            var key = Normalize(pair.Key);
            if (!File.Exists(Path.Combine(folder, "Bundles", key))) continue;
            var dependencies = pair.Value.Dependencies.Select(Normalize).Distinct().ToList();
            foreach (var dependency in dependencies)
                if (dependency != "shaders" && !File.Exists(Path.Combine(folder, "Bundles", dependency)))
                    throw new Exception("Missing built dependency: " + key + " -> " + dependency);
            settings.Settings.BundleDependencies.BundleToDependencies[key] = dependencies;
        }
        File.WriteAllText(Path.Combine(folder, "OwlcatModificationSettings.json"), JsonUtility.ToJson(settings.Settings, true));
        using (var zip = new Pathfinding.Ionic.Zip.ZipFile())
        {
            zip.AddDirectory(folder, "./");
            zip.Save(Path.Combine(output, "EisenhornQuestPrototype.zip"));
        }
        Debug.Log("[EisenhornPrototypeBuild] Verified native shader asset and normalized actual bundle dependencies.");
    }

    static void BuildScene(JObject ids)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root = new GameObject("Eisenhorn Test Document");
            root.layer = LayerMask.NameToLayer("Selectable");
            if (root.layer < 0) throw new Exception("Native Selectable layer unavailable.");
            var view = root.AddComponent<EisenhornDocumentView>();
            view.UniqueId = (string)ids["entity"];
            var interaction = root.AddComponent<EisenhornDocumentInteraction>();
            interaction.Settings = new InteractionActionSettings
            {
                Actions = new ActionsReference(),
                Condition = new ConditionsReference()
            };
            interaction.Settings.DisableAfterUse = false;
            interaction.Settings.NotInCombat = true;
            interaction.Settings.ShowOvertip = true;
            interaction.Settings.ShowHighlight = true;
            interaction.Settings.Type = Kingmaker.View.MapObjects.InteractionComponentBase.InteractionType.Approach;
            interaction.Settings.ProximityRadius = 3;
            interaction.Settings.UIType = Kingmaker.View.MapObjects.InteractionComponentBase.UIInteractionType.Action;
            interaction.Settings.UseAnimationState = Kingmaker.Visual.Animation.Kingmaker.UnitAnimationInteractionType.None;
            var serialized = new SerializedObject(interaction);
            serialized.FindProperty("Settings.Actions.guid").stringValue = (string)ids["start-actions"];
            serialized.FindProperty("Settings.Condition.guid").stringValue = (string)ids["available"];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var namePath = Root + "/Content/DocumentName.asset";
            var name = AssetDatabase.LoadAssetAtPath<SharedStringAsset>(namePath);
            if (!name)
            {
                name = ScriptableObject.CreateInstance<SharedStringAsset>();
                name.String = new LocalizedString();
                AssetDatabase.CreateAsset(name, namePath);
            }
            var nameData = new SerializedObject(name);
            nameData.FindProperty("String.m_Key").stringValue = "eisenhorn-prototype-speaker";
            nameData.ApplyModifiedPropertiesWithoutUndo();
            interaction.Settings.DisplayName = name;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.6f, 0);
            collider.size = new Vector3(1.2f, 1.0f, 1.2f);
            collider.isTrigger = true;
            var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "Placeholder parchment";
            paper.layer = root.layer;
            paper.transform.SetParent(root.transform, false);
            paper.transform.localPosition = new Vector3(0, 0.65f, 0);
            paper.transform.localScale = new Vector3(0.9f, 0.04f, 0.65f);
            UnityEngine.Object.DestroyImmediate(paper.GetComponent<Collider>());
            var matPath = Root + "/Content/PrototypeParchment.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!material)
            {
                var shader = Shader.Find("Owlcat/Unlit");
                if (!shader) throw new Exception("Existing Owlcat/Unlit shader unavailable.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.shader = Shader.Find("Owlcat/Unlit");
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.9f, 0.76f, 0.42f, 1));
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.9f, 0.76f, 0.42f, 1));
            paper.GetComponent<Renderer>().sharedMaterial = material;
            root.AddComponent<EisenhornDocumentAnchor>();
            root.AddComponent<OwlcatModificationMaterialsInSceneFixer>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            if (!Application.isBatchMode)
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        // Preserve the authored scene identity referenced from .jbp assets.
        var meta = ScenePath + ".meta";
        var text = File.ReadAllText(meta);
        var wanted = (string)ids["scene"];
        if (!text.Contains("guid: " + wanted))
        {
            text = System.Text.RegularExpressions.Regex.Replace(text, "guid: [a-f0-9]{32}", "guid: " + wanted);
            File.WriteAllText(meta, text);
            AssetDatabase.Refresh();
        }
        AssetDatabase.SaveAssets();
    }

    static void ValidateScene(JObject ids)
    {
        if (AssetDatabase.AssetPathToGUID(ScenePath) != (string)ids["scene"])
            throw new Exception("Mechanics scene GUID mismatch.");
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool alreadyOpen = scene.IsValid() && scene.isLoaded;
        if (!alreadyOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var objects = scene.GetRootGameObjects();
            if (objects.Length != 1) throw new Exception("Expected one native document.");
            var root = objects[0];
            var view = root.GetComponent<EisenhornDocumentView>();
            var interaction = root.GetComponent<EisenhornDocumentInteraction>();
            if (!view || view.UniqueId != (string)ids["entity"] || !interaction || interaction.Settings.DisableAfterUse)
                throw new Exception("Native map-object wiring invalid.");
            var so = new SerializedObject(interaction);
            if (so.FindProperty("Settings.Actions.guid").stringValue != (string)ids["start-actions"]
                || so.FindProperty("Settings.Condition.guid").stringValue != (string)ids["available"])
                throw new Exception("Native action/condition references invalid.");
            foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!script) throw new Exception("Missing scene script.");
            Debug.Log("[EisenhornPrototypeBuild] Native scene identity, action, condition and scripts validated.");
        }
        finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
    }
}
