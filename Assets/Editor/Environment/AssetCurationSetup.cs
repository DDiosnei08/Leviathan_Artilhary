using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProjectLeviathan.Base;
using ProjectLeviathan.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class AssetCurationSetup
{
    public const string ConfigPath="Assets/Settings/Vegetation/WorldScenery.asset";
    static readonly string[] Selected={
        "Assets/Darth_Artisan/Free_Trees/Prefabs/Oak_Tree.prefab",
        "Assets/Darth_Artisan/Free_Trees/Prefabs/Fir_Tree.prefab",
        "Assets/Free/Stones/Mesh/Stone_1.fbx","Assets/Free/Stones/Mesh/Stone_3.fbx","Assets/Free/Stones/Mesh/Stone_5.fbx",
        "Assets/Industrial Props/Prefabs/LightPole.prefab",
        "Assets/Sector Warzone/Concrete Barriers/Prefabs/PRE_Concrete Barrier_1.prefab",
        "Assets/Defensive_props/Prefabs/fortifications/sandbag barricade.prefab",
        "Assets/Lowpoly_Crates/Meshes/Crate_01.fbx"
    };
    static readonly string[] PackageRoots={"Darth_Artisan","Free","Industrial Props","Sector Warzone","Defensive_props","Lowpoly_Crates","RSG","RockFREE","PBR_Rock_Cliffs_Pack","Fences","StreetLightsPack","NOT_Lonely","Viverna_Assets","AK STUDIO ART","PHOSdigital","AssetsStore","Vefects","Announcer","Military WW2 Music Pack","_TerrainAutoUpgrade"};

    public static void ApplyBatch()
    {
        AssetDatabase.Refresh();var scene=EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);var rig=Object.FindAnyObjectByType<BaseGameRig>();
        Directory.CreateDirectory("Artifacts/AssetCuration/Captures");
        EnsureFolder("Assets/Models/Environment/Curated");EnsureFolder("Assets/Materials/Environment");
        var keep=new HashSet<string>(StringComparer.Ordinal);
        foreach(string path in Selected) foreach(string dep in AssetDatabase.GetDependencies(Resolve(path),true)) keep.Add(dep);
        var report=new StringBuilder();var meshes=new Mesh[Selected.Length];
        for(int i=0;i<Selected.Length;i++)
        {
            string path=Resolve(Selected[i]);var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null) throw new InvalidOperationException("Missing selected source: "+path);
            meshes[i]=MeshAsset(asset.name,asset,i<2,false);report.AppendLine(path+" -> "+AssetDatabase.GetAssetPath(meshes[i])+" vertices="+meshes[i].vertexCount);
        }
        var trees=AssetDatabase.LoadAssetAtPath<TreeConfiguration>(EnvironmentEnrichmentSetup.TreesPath);
        trees.broadleafNear=meshes[0];trees.broadleafFar=MeshAsset("Oak_Tree_LOD1",AssetDatabase.LoadAssetAtPath<GameObject>(Resolve(Selected[0])),true,true);
        trees.pineNear=meshes[1];trees.pineFar=MeshAsset("Fir_Tree_LOD1",AssetDatabase.LoadAssetAtPath<GameObject>(Resolve(Selected[1])),true,true);
        trees.spacing=14;trees.density=.68f;trees.groveThreshold=.49f;trees.groveScale=220;trees.maximumTreesPerChunk=1400;
        trees.heights=new Vector2(7,13);trees.nearDistance=125;trees.farDistance=700;trees.shadowDistance=75;EditorUtility.SetDirty(trees);
        var treeManager=Object.FindAnyObjectByType<ProceduralTreeManager>();treeManager.enabled=false;treeManager.configuration=trees;treeManager.enabled=true;
        var configuration=AssetDatabase.LoadAssetAtPath<WorldSceneryConfiguration>(ConfigPath);
        if(configuration==null) {configuration=ScriptableObject.CreateInstance<WorldSceneryConfiguration>();AssetDatabase.CreateAsset(configuration,ConfigPath);}
        const string materialPath="Assets/Materials/Environment/CuratedProps.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null) {material=new Material(Shader.Find("Leviathan/Cartoon Trees")) {enableInstancing=true};AssetDatabase.CreateAsset(material,materialPath);}
        material.SetVector("_TreeWind",Vector4.zero);EditorUtility.SetDirty(material);configuration.material=material;
        configuration.prototypes=new[] {
            Prototype("Stone A",SceneryRole.Rock,meshes[2],1.3f,4),Prototype("Stone B",SceneryRole.Rock,meshes[3],1.5f,5),Prototype("Stone C",SceneryRole.Rock,meshes[4],1.2f,3.5f),
            Prototype("Utility pole",SceneryRole.Lamp,meshes[5],8,10),Prototype("Concrete barrier",SceneryRole.Barrier,meshes[6],3.5f,4),
            Prototype("Sandbags",SceneryRole.Barrier,meshes[7],2.7f,3),Prototype("Supply crate",SceneryRole.Crate,meshes[8],1.1f,1.7f)};
        EditorUtility.SetDirty(configuration);var manager=Object.FindAnyObjectByType<WorldSceneryManager>();
        if(manager==null) manager=new GameObject("Cenario Procedural").AddComponent<WorldSceneryManager>();
        manager.enabled=false;manager.terrain=rig.terrain;manager.configuration=configuration;manager.player=rig.player.transform;manager.enabled=true;EditorUtility.SetDirty(manager);
        rig.spawner.settings.maxHeightRange=.25f;rig.spawner.settings.maxSlopeDeg=2;rig.spawner.settings.gridSteps=15;rig.spawner.settings.candidates=1800;
        if(!rig.spawner.RebuildEditorPreview()) throw new InvalidOperationException("No fully supported base site in search area.");
        var preview=rig.spawner.previewInstance;var assembly=preview.GetComponent<BaseAssembly>();
        rig.player.transform.SetPositionAndRotation(assembly.playerSpawn.position,assembly.playerSpawn.rotation);
        rig.overviewCamera.transform.position=preview.transform.TransformPoint(new Vector3(-47,32,-45));rig.overviewCamera.transform.LookAt(preview.transform.position+Vector3.up*3);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        foreach(string root in new[]{"Assets/Scenes","Assets/Prefabs","Assets/Settings","Assets/Materials","Assets/Models"})
            foreach(string guid in AssetDatabase.FindAssets("",new[]{root})) foreach(string dep in AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(guid),true)) keep.Add(dep);
        UpgradeSourceMaterials(keep);OrganizePackages(keep);
        AssetDatabase.Refresh();AssetDatabase.SaveAssets();
        report.AppendLine("Base="+rig.spawner.previewSite.center+" ground="+rig.spawner.previewSite.groundHeight+" opening="+rig.spawner.previewSite.carveRadius);
        File.WriteAllText("Artifacts/AssetCuration/Selection.txt",report.ToString());Debug.Log("ASSET_CURATION_SETUP_OK");
    }
    static SceneryPrototype Prototype(string name,SceneryRole role,Mesh mesh,float low,float high) => new SceneryPrototype {label=name,role=role,mesh=mesh,sizeRange=new Vector2(low,high)};
    static void UpgradeSourceMaterials(HashSet<string> paths)
    {
        foreach(string path in paths)
        {
            if(!path.EndsWith(".mat",StringComparison.Ordinal)) continue;
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null || material.shader.name!="Standard") continue;
            Texture texture=material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Color color=material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            material.shader=Shader.Find("Universal Render Pipeline/Lit");material.SetColor("_BaseColor",color);material.SetTexture("_BaseMap",texture);material.SetFloat("_Smoothness",.1f);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }
    static Mesh MeshAsset(string label,GameObject asset,bool tree,bool coarse)
    {
        string path="Assets/Models/Environment/Curated/"+label.Replace(' ','_')+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var mesh=CuratedMeshBaker.Bake(asset,tree,coarse);
        if(existing!=null) {EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static string Resolve(string path)
    {if(AssetDatabase.LoadMainAssetAtPath(path)!=null) return path;return "Assets/ThirdParty/Environment/"+path.Substring(7);}
    static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
    }
    static void OrganizePackages(HashSet<string> keep)
    {
        var manifest=new StringBuilder();long archivedBytes=0;int retained=0,archived=0;
        foreach(string package in PackageRoots)
        {
            string root="Assets/"+package;if(!Directory.Exists(root)) continue;
            foreach(string source in Directory.GetFiles(root,"*",SearchOption.AllDirectories))
            {
                string path=source.Replace('\\','/');if(path.EndsWith(".meta",StringComparison.Ordinal)) continue;
                bool license=Path.GetFileName(path).ToLowerInvariant().Contains("license");
                if(keep.Contains(path) || license)
                {
                    string target="Assets/ThirdParty/Environment/"+path.Substring(7);EnsureFolder(Path.GetDirectoryName(target).Replace('\\','/'));
                    string error=AssetDatabase.MoveAsset(path,target);if(!string.IsNullOrEmpty(error)) throw new IOException(error);
                    manifest.AppendLine("MOVE\t"+path+"\t"+target);retained++;
                }
                else
                {
                    string target="Artifacts/AssetCuration/Unused/"+path.Substring(7);Directory.CreateDirectory(Path.GetDirectoryName(target));
                    archivedBytes+=new FileInfo(path).Length;File.Move(path,target);
                    if(File.Exists(path+".meta")) File.Move(path+".meta",target+".meta");
                    manifest.AppendLine("ARCHIVE\t"+path+"\t"+target);archived++;
                }
            }
            // Remaining files are folder metadata; preserve those as part of the original package.
            foreach(string meta in Directory.GetFiles(root,"*.meta",SearchOption.AllDirectories))
            {string target="Artifacts/AssetCuration/Unused/"+meta.Substring(7);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Move(meta,target);}
            if(File.Exists(root+".meta")) {string target="Artifacts/AssetCuration/Unused/"+package+".meta";Directory.CreateDirectory(Path.GetDirectoryName(target));File.Move(root+".meta",target);}
            string absolute=Path.GetFullPath(root),assets=Path.GetFullPath("Assets")+Path.DirectorySeparatorChar;
            if(!absolute.StartsWith(assets,StringComparison.OrdinalIgnoreCase)) throw new IOException("Unsafe package root");Directory.Delete(root,true);
        }
        if(retained+archived>0)
        {
            File.WriteAllText("Artifacts/AssetCuration/Moves.tsv",manifest.ToString());
            File.WriteAllText("Artifacts/AssetCuration/Organization.txt","Retained source files="+retained+"\nArchived files="+archived+"\nArchived bytes="+archivedBytes+"\nNo downloaded content was permanently deleted.");
        }
    }
}
