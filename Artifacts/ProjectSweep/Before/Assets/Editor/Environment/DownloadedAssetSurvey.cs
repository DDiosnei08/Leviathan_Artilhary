using System.IO;
using System.Text;
using ProjectLeviathan.Base;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class DownloadedAssetSurvey
{
    public static readonly string[] Candidates={
        "Assets/Darth_Artisan/Free_Trees/Prefabs/Oak_Tree.prefab",
        "Assets/Darth_Artisan/Free_Trees/Prefabs/Fir_Tree.prefab",
        "Assets/Darth_Artisan/Free_Trees/Prefabs/Poplar_Tree.prefab",
        "Assets/Darth_Artisan/Free_Trees/Prefabs/Palm_Tree.prefab",
        "Assets/Free/Stones/Mesh/Stone_1.fbx", "Assets/Free/Stones/Mesh/Stone_3.fbx",
        "Assets/RockFREE/LODGroups/rock1_LOD0.prefab",
        "Assets/Industrial Props/Prefabs/LightPole.prefab",
        "Assets/Industrial Props/Prefabs/RoadDivider.prefab",
        "Assets/Industrial Props/Prefabs/Fence.prefab",
        "Assets/Sector Warzone/Concrete Barriers/Prefabs/PRE_Concrete Barrier_1.prefab",
        "Assets/RSG/URP/Prefabs/P_rsg_military_sandbox_01.prefab",
        "Assets/RSG/URP/Prefabs/P_rsg_military_antitank_hedgehog_01.prefab",
        "Assets/Defensive_props/Prefabs/fortifications/sandbag barricade.prefab",
        "Assets/Lowpoly_Crates/Meshes/Crate_01.fbx"
    };
    public static void InspectBatch()
    {
        Directory.CreateDirectory("Artifacts/AssetCuration/Survey");AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        var rig=Object.FindAnyObjectByType<BaseGameRig>();var spawner=rig.spawner;
        var text=new StringBuilder();
        text.AppendLine("Base root="+spawner.previewInstance.transform.position+" site="+spawner.previewSite.center);
        foreach(var renderer in spawner.previewInstance.GetComponentsInChildren<Renderer>())
            if(renderer.name.Contains("Fosso") || renderer.name.Contains("Disco")) text.AppendLine(renderer.name+" "+renderer.bounds);
        foreach(var chunk in rig.terrain.TerrainChunks.Values)
            if(chunk.GeneratedData!=null) text.AppendLine("Chunk "+chunk.Coordinate+" position="+chunk.GameObject.transform.position+" origin="+chunk.GeneratedData.WorldOrigin+" site="+chunk.GeneratedData.BaseSite.center);
        var utility=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var light=new GameObject("Survey light").AddComponent<Light>();SceneManagementMove(light.gameObject,utility);
        light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(35,-35,0);
        var camera=new GameObject("Survey camera").AddComponent<Camera>();SceneManagementMove(camera.gameObject,utility);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.25f,.28f);camera.cullingMask=1<<30;
        for(int i=0;i<Candidates.Length;i++)
        {
            string path=Candidates[i];var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null) {text.AppendLine("MISSING "+path);continue;}
            var model=Object.Instantiate(asset);SceneManagementMove(model,utility);model.transform.position=Vector3.zero;
            var renderers=model.GetComponentsInChildren<Renderer>();Bounds bounds=new Bounds();bool first=true;int vertices=0;
            foreach(var renderer in renderers)
            {
                renderer.gameObject.layer=30;if(first) {bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
                foreach(var material in renderer.sharedMaterials) if(material!=null) text.AppendLine("  material="+material.name+" shader="+material.shader.name);
            }
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>()) if(filter.sharedMesh!=null)
            {vertices+=filter.sharedMesh.vertexCount;text.AppendLine("  mesh="+filter.sharedMesh.name+" vertices="+filter.sharedMesh.vertexCount+" submeshes="+filter.sharedMesh.subMeshCount+" colors="+filter.sharedMesh.colors.Length);}
            text.AppendLine(path+" bounds="+bounds+" vertices="+vertices);
            camera.transform.position=bounds.center+new Vector3(-.8f,.35f,-1.2f)*Mathf.Max(1,bounds.size.magnitude);
            camera.transform.LookAt(bounds.center);camera.nearClipPlane=.01f;camera.farClipPlane=10000;
            Capture(camera,"Artifacts/AssetCuration/Survey/"+i.ToString("00")+"_"+asset.name.Replace(' ','_')+".png",400,400);
            Object.DestroyImmediate(model);
        }
        EditorSceneManager.CloseScene(utility,true);File.WriteAllText("Artifacts/AssetCuration/Survey/Survey.txt",text.ToString());
        Debug.Log("DOWNLOADED_ASSET_SURVEY_OK");
    }
    static void SceneManagementMove(GameObject go,UnityEngine.SceneManagement.Scene scene) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
    public static void DiagnoseBaseBatch()
    {
        Directory.CreateDirectory("Artifacts/AssetCuration/Survey");EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        var rig=Object.FindAnyObjectByType<BaseGameRig>();rig.terrain.terrainSettings.RuntimeBaseSite=rig.spawner.previewSite;rig.terrain.RegenerateTerrain();
        var camera=rig.overviewCamera;var site=rig.spawner.previewSite;camera.orthographic=true;camera.orthographicSize=55;
        camera.transform.position=new Vector3(site.center.x,site.groundHeight+120,site.center.y);camera.transform.rotation=Quaternion.Euler(90,0,0);
        Capture(camera,"Artifacts/AssetCuration/Survey/Base_Top_Lit.png");
        var light=Object.FindAnyObjectByType<Light>();light.shadows=LightShadows.None;
        Capture(camera,"Artifacts/AssetCuration/Survey/Base_Top_NoShadows.png");
        var text=new StringBuilder();int missingOutside=0,coveredInside=0;
        for(int z=-40;z<=40;z++) for(int x=-40;x<=40;x++)
        {
            float radius=Mathf.Sqrt(x*x+z*z);bool hit=false;
            var ray=new Ray(new Vector3(site.center.x+x,site.groundHeight+40,site.center.y+z),Vector3.down);
            foreach(var chunk in rig.terrain.TerrainChunks.Values) if(chunk.MeshCollider.Raycast(ray,out var contact,100)) hit=true;
            if(radius>site.carveRadius+1 && !hit) missingOutside++;
            if(radius<site.carveRadius-1 && hit) coveredInside++;
        }
        text.AppendLine("Missing terrain outside intended opening="+missingOutside+" terrain covering inside="+coveredInside);
        foreach(var chunk in rig.terrain.TerrainChunks.Values) text.AppendLine(chunk.Coordinate+" origin="+chunk.GeneratedData.WorldOrigin+" site="+chunk.GeneratedData.BaseSite.center);
        File.WriteAllText("Artifacts/AssetCuration/Survey/BaseDiagnosis.txt",text.ToString());
    }
    public static void Capture(Camera camera,string path,int width=1600,int height=900)
    {
        var rt=new RenderTexture(width,height,24);rt.Create();var previous=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        File.WriteAllBytes(path,image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;
        rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);
    }
}
