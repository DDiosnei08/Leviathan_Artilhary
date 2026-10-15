using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectOrganizationValidator
{
    [MenuItem("Tools/Leviathan/Validation/Check Organized Project")]
    public static void ValidateMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try { ValidateAssets(); }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }

    public static void ValidateAndPlayBatch()
    {
        try
        {
            ValidateAssets();
            BaseIntegrationValidation.ValidateBatch();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static void RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
            throw new InvalidOperationException("Missing or unimported asset: " + path);
    }

    public static void ValidateAssets()
    {
        AssetDatabase.Refresh();
        RequireAsset<SceneAsset>(BaseIntegrationBuilder.ScenePath);
        RequireAsset<SceneAsset>("Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity");
        RequireAsset<GameObject>(BaseIntegrationBuilder.PrefabPath);
        RequireAsset<GameObject>("Assets/Models/Artillery/Gauges/ArtilleryGaugeSet.fbx");
        RequireAsset<Material>("Assets/Materials/Terrain/TerrainCartoon.mat");
        RequireAsset<Material>("Assets/Materials/Water/WaterCartoon.mat");
        foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                throw new InvalidOperationException("Missing shader: " + path);
        }
        foreach (string path in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            // BaseSite groups serializable data, an interface, a solver and a registry; it is not a component.
            if (script.GetClass() == null && !path.EndsWith("/BaseSite.cs",StringComparison.Ordinal))
                throw new InvalidOperationException("Script type could not be resolved: " + path);
        }
        string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
        foreach (string path in scenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int missing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject));
            if (missing != 0) throw new InvalidOperationException($"{path}: {missing} missing components.");
            Debug.Log("ORGANIZATION_SCENE_OK: " + path);
        }
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!File.Exists(scene.path) || AssetDatabase.AssetPathToGUID(scene.path) != scene.guid.ToString())
                throw new InvalidOperationException("Build Settings scene reference is invalid: " + scene.path);
        }
        Debug.Log($"ORGANIZATION_ASSETS_OK: {scenes.Length} scenes, runtime script types, materials, model and Build Settings.");
    }
}
