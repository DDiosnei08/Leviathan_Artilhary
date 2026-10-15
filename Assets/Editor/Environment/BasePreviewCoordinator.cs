using System.Collections.Generic;
using ProjectLeviathan.Base;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BasePreviewCoordinator
{
    static readonly Dictionary<BaseSpawner,string> signatures=new Dictionary<BaseSpawner,string>();
    static double nextCheck;
    static BasePreviewCoordinator()
    {EditorApplication.update+=Update;UnityEditor.SceneManagement.EditorSceneManager.sceneOpened+=(_,__)=>signatures.Clear();}
    static void Update()
    {
        if(Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<nextCheck) return;
        nextCheck=EditorApplication.timeSinceStartup+2;
        foreach(var spawner in Object.FindObjectsByType<BaseSpawner>(FindObjectsSortMode.None))
        {
            if(spawner.heightSource==null || spawner.heightSource.terrain==null || spawner.previewInstance==null || spawner.basePrefab==null) continue;
            var settings=spawner.heightSource.terrain.terrainSettings;if(settings==null || !EditorUtility.IsPersistent(settings)) continue;
            string signature=EditorJsonUtility.ToJson(settings)+JsonUtility.ToJson(spawner.settings)+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(spawner.basePrefab));
            if(signatures.TryGetValue(spawner,out string previous) && previous==signature) continue;
            signatures[spawner]=signature;spawner.RebuildEditorPreview();
            signatures[spawner]=EditorJsonUtility.ToJson(settings)+JsonUtility.ToJson(spawner.settings)+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(spawner.basePrefab));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        }
    }
}
