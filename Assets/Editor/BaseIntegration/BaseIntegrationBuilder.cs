using System;
using System.Collections.Generic;
using System.IO;
using ProjectLeviathan.Base;
using ProjectLeviathan.Grass;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.World;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class BaseIntegrationBuilder
{
    public const string ScenePath="Assets/Scenes/Leviathan_BaseGiratoria.unity";
    public const string PrefabPath="Assets/Prefabs/BaseGiratoria.prefab";
    public const string SettingsPath="Assets/Settings/Terrain/BaseGiratoriaTerrain.asset";
    const string Materials="Assets/Materials/BaseGiratoria/";
    [MenuItem("Leviathan/Base Giratoria/Criar ou reconstruir cena")]
    static void MenuBuild()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildBatch();
    }
    public static void BuildBatch()
    {
        Directory.CreateDirectory(Materials); Directory.CreateDirectory("Assets/Prefabs"); AssetDatabase.Refresh();
        BaseModelInspection.InspectBatch();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GameObject root=BuildBase();
        GameObject prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); Object.DestroyImmediate(root);
        TerrainSettings settings=AssetDatabase.LoadAssetAtPath<TerrainSettings>(SettingsPath);
        if(settings==null)
        {
            settings=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TerrainSettings>("Assets/Settings/Terrain/CartoonShaderDemo.asset"));
            settings.name="BaseGiratoriaTerrain"; AssetDatabase.CreateAsset(settings,SettingsPath);
        }
        settings.finiteMapChunksPerSide=17; settings.finiteMapStreamingRadiusChunks=1; settings.finiteMapEditorPreviewRadiusChunks=1;
        settings.terrainMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Terrain/TerrainCartoon.mat");
        settings.waterMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Water/WaterCartoon.mat");
        settings.generateWaterSurfaces=true; settings.Validate(); EditorUtility.SetDirty(settings);

        GameObject world=new GameObject("Mundo Procedural"); world.SetActive(false);
        TerrainWorldController terrain=world.AddComponent<TerrainWorldController>(); terrain.terrainSettings=settings;
        terrain.terrainMaterial=settings.terrainMaterial; terrain.previewInEditor=false; terrain.useFiniteMap=true;
        terrain.maxChunkGenerationsPerFrame=2; terrain.useBackgroundGeneration=false;
        terrain.weatherProvider=world.AddComponent<WorldWeatherProvider>();
        var spawnRoot=new GameObject("Base Unica por Semente");
        var source=spawnRoot.AddComponent<ProceduralHeightSource>(); source.terrain=terrain;
        var spawner=spawnRoot.AddComponent<BaseSpawner>(); spawner.heightSource=source; spawner.basePrefab=prefab;
        spawner.settings.searchCenter=new Vector2(600,600); spawner.settings.maxDistance=650;
        spawner.settings.seed=settings.noiseSettings.seed;
        var natural=settings.CaptureGenerationSettings(false);
        spawner.settings.waterLevel=natural.EvaluateHeight(natural.lakeThreshold)*natural.heightMultiplier;
        spawner.settings.maxAltitude=natural.heightMultiplier*2;
        source.ResetCache(); BaseSiteData site=BaseSiteSolver.Solve(source,spawner.settings);
        if(!site.valid) throw new InvalidOperationException("No dry base site for scene seed.");
        settings.RuntimeBaseSite=site; spawner.previewSite=site;
        var preview=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        preview.transform.SetPositionAndRotation(new Vector3(site.center.x,site.groundHeight,site.center.y),Quaternion.Euler(0,site.yaw,0));
        preview.name="BaseGiratoria_Preview"; spawner.previewInstance=preview;
        BaseAssembly assembly=preview.GetComponent<BaseAssembly>();
        preview.GetComponentInChildren<BasePhysicalMap>().SetWorld(settings,site);

        GameObject playerObject=new GameObject("Operador");
        playerObject.transform.SetPositionAndRotation(assembly.playerSpawn.position,assembly.playerSpawn.rotation);
        var character=playerObject.AddComponent<CharacterController>(); character.height=1.8f; character.radius=.32f;
        character.center=new Vector3(0,.9f,0); character.stepOffset=.32f;
        var player=playerObject.AddComponent<LeviathanPlayerController>(); player.enabled=false;
        Camera playerCamera=CameraAt("Camera Jogador",playerObject.transform,new Vector3(0,1.65f,0));
        player.cameraPivot=playerCamera.transform;
        var interactor=playerObject.AddComponent<CommandRoomInteractor>(); interactor.playerController=player; interactor.viewCamera=playerCamera; interactor.enabled=false;
        Camera overview=CameraAt("Camera Geral",null,preview.transform.TransformPoint(new Vector3(-43,31,-42))); overview.transform.LookAt(preview.transform.position+Vector3.up*4);
        Camera station=CameraAt("Camera Instrumentos",null,assembly.leftStation.position+Vector3.up*1.65f);
        station.transform.rotation=assembly.leftStation.rotation*Quaternion.Euler(35,0,0);
        var stationInteractor=station.gameObject.AddComponent<CommandRoomInteractor>(); stationInteractor.viewCamera=station;
        stationInteractor.playerController=player; stationInteractor.useScreenCenter=false; stationInteractor.interactDistance=5;
        stationInteractor.enabled=false;
        SelectCamera(overview,true); SelectCamera(playerCamera,false); SelectCamera(station,false);
        terrain.viewer=overview.transform;
        var grassObject=new GameObject("Grama GPU"); grassObject.SetActive(false);
        var grass=grassObject.AddComponent<GrassManager>(); grass.terrain=terrain;
        grass.configuration=AssetDatabase.LoadAssetAtPath<GrassConfiguration>(GrassValidation.ConfigPath);
        grass.renderCamera=overview; grass.player=playerObject.transform;
        var rig=new GameObject("Operacao da Base").AddComponent<BaseGameRig>(); rig.spawner=spawner; rig.terrain=terrain;
        rig.grass=grass; rig.player=player; rig.playerCamera=playerCamera; rig.overviewCamera=overview; rig.stationCamera=station;
        rig.playerInteractor=interactor; rig.stationInteractor=stationInteractor; rig.initialView=BaseGameRig.View.Overview;
        Light sun=new GameObject("Sol").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.4f;
        sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(48,-35,0);
        RenderSettings.ambientMode=AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.54f,.67f,.8f);
        RenderSettings.ambientEquatorColor=new Color(.4f,.47f,.43f); RenderSettings.ambientGroundColor=new Color(.23f,.26f,.22f);
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Exponential; RenderSettings.fogColor=new Color(.6f,.73f,.79f); RenderSettings.fogDensity=.00008f;
        terrain.previewInEditor=true; world.SetActive(true); terrain.RegenerateTerrain(); grassObject.SetActive(true);
        Directory.CreateDirectory("Assets/Scenes");
        if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new InvalidOperationException("Scene save failed.");
        EditorBuildSettings.scenes=new[] {new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/Scenes/Tests/Test_ArtilleryControls/Test_ArtilleryControls.unity",false)};
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Artifacts/BaseIntegration");
        File.WriteAllText("Artifacts/BaseIntegration/Build.txt","Seed="+site.seed+"\nSite="+site.center+"\nGround="+site.groundHeight+"\nYaw="+site.yaw+"\nVertices="+terrain.GeneratedVertexCount);
        Debug.Log("BASE_INTEGRATION_BUILD_OK "+site.center+" seed="+site.seed);
    }
    static GameObject BuildBase()
    {
        GameObject root=new GameObject("BaseGiratoria");
        GameObject model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BaseModelInspection.ModelPath));
        PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        model.transform.SetParent(root.transform,false);
        Transform importedPivot=Find(model.transform,"PIVO_Plataforma_Giratoria");
        // An upright wrapper prevents yaw around the Blender-local axis (which points along the barrel).
        Transform pivot=Empty("Rotacao_Y_Unity",root.transform,Vector3.zero);
        importedPivot.SetParent(pivot,true); importedPivot.localRotation=Quaternion.Euler(90,0,0);
        var rotator=pivot.gameObject.AddComponent<PlatformRotator>();
        var body=pivot.gameObject.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
        var materials=new Dictionary<Material,Material>();
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(renderer.name.StartsWith("REF_",StringComparison.Ordinal)) { renderer.gameObject.SetActive(false); continue; }
            var slots=renderer.sharedMaterials;
            for(int i=0;i<slots.Length;i++)
            {
                Material old=slots[i]; if(old==null) continue;
                if(!materials.TryGetValue(old,out Material replacement))
                {
                    Color color=old.HasProperty("_Color") ? old.color : Color.gray;
                    replacement=Material("FBX_"+old.name.Replace(' ','_'),color); materials.Add(old,replacement);
                }
                slots[i]=replacement;
            }
            renderer.sharedMaterials=slots;
            // Non-convex triangle colliders are supported on static and kinematic assemblies.
            var filter=renderer.GetComponent<MeshFilter>();
            if(filter!=null && filter.sharedMesh!=null)
            { var collider=renderer.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh=filter.sharedMesh; }
        }
        var assembly=root.AddComponent<BaseAssembly>(); assembly.platform=rotator;
        foreach(string tag in new[]{"E","D"})
        {
            Transform elevation=Find(root.transform,"Canhao_"+tag+"_PIVO_Elevacao"); elevation.localRotation=Quaternion.identity;
            Transform recoil=Find(root.transform,"Canhao_"+tag+"_PIVO_Recuo"); recoil.localPosition=Vector3.zero;
            Transform door=Find(root.transform,"Canhao_"+tag+"_PIVO_Ferrolho"); door.localRotation=Quaternion.identity;
            Transform muzzle=Empty("Canhao_"+tag+"_BOCA",recoil,new Vector3(0,.246f,0));
            var controller=Empty("Controle_Canhao_"+tag,pivot,Vector3.zero).gameObject.AddComponent<CannonController>();
            controller.elevationPivot=elevation; controller.recoilPivot=recoil; controller.doorHinge=door; controller.muzzle=muzzle;
            controller.platform=rotator; controller.elevationAxis=Vector3.left; controller.recoilAxis=Vector3.up;
            controller.doorAxis=Vector3.forward; controller.doorOpenAngle=tag=="E" ? 100 : -100;
            controller.muzzleLocalDirection=Vector3.up;
            float side=tag=="E" ? -1 : 1;
            Transform station=Empty("Canhao_"+tag+"_POSTO_OPERADOR",pivot,new Vector3(side*8.7f,1.7f,-.7f));
            Console(pivot,controller,rotator,new Vector3(side*8.7f,2.55f,.55f));
            if(tag=="E") {assembly.left=controller; assembly.leftStation=station;} else {assembly.right=controller;assembly.rightStation=station;}
        }
        float deck=Find(root.transform,"Plataforma_Disco").GetComponent<Renderer>().bounds.max.y;
        Transform map=Empty("Mesa de Operacao",pivot,new Vector3(0,deck,-10));
        Map(map,assembly);
        assembly.mapStation=Empty("Posto_Mapa",pivot,new Vector3(0,deck+.05f,-12.4f));
        assembly.playerSpawn=Empty("Nascimento_Operador",pivot,new Vector3(0,deck+.1f,-14.3f));
        Empty("BASE_CAMERA_CONVES",pivot,new Vector3(0,6,-9));
        File.WriteAllText("Artifacts/BaseIntegration/PrefabBounds.txt",BoundsReport(root));
        return root;
    }
    static string BoundsReport(GameObject root)
    {
        var report=new System.Text.StringBuilder();
        foreach(var renderer in root.GetComponentsInChildren<Renderer>())
            if(renderer.name.Contains("Passarela") || renderer.name.Contains("Disco") || renderer.name.Contains("Fosso") || renderer.name.Contains("Painel"))
                report.AppendLine(renderer.name+" "+renderer.bounds);
        return report.ToString();
    }
    static void Console(Transform parent,CannonController cannon,PlatformRotator platform,Vector3 position)
    {
        Transform console=Empty("Console_"+cannon.name,parent,position); console.localRotation=Quaternion.Euler(-55,0,0);
        Part("Chapa",console,new Vector3(0,-.045f,0),new Vector3(1.65f,.07f,1.5f),Material("Instrumentos",new Color(.12f,.19f,.18f)),true);
        BaseGaugeDriver driver=console.gameObject.AddComponent<BaseGaugeDriver>(); driver.cannon=cannon; driver.platform=platform;
        driver.heading=Dial(console,new Vector3(-.4f,0,.16f),false);
        driver.elevation=Dial(console,new Vector3(.4f,0,.16f),true);
        Wheel(console,new Vector3(-.42f,.12f,-.46f),cannon,platform,false);
        Wheel(console,new Vector3(.42f,.12f,-.46f),cannon,platform,true);
        var load=Button(console,new Vector3(-.64f,0,.64f),"CARGA",new Color(.25f,.58f,.39f));
        UnityEventTools.AddPersistentListener(load.onPressed,cannon.LoadCommand);
        var fire=Button(console,new Vector3(.64f,0,.64f),"FOGO",new Color(.78f,.17f,.1f));
        UnityEventTools.AddPersistentListener(fire.onPressed,cannon.FireCommand);
        var ammo=Button(console,new Vector3(-.18f,0,.64f),"MUNICAO",new Color(.7f,.64f,.32f));
        UnityEventTools.AddPersistentListener(ammo.onPressed,cannon.CycleAmmo);
        var charges=Button(console,new Vector3(.18f,0,.64f),"PROPELENTE",new Color(.7f,.64f,.32f));
        UnityEventTools.AddPersistentListener(charges.onPressed,cannon.CycleCharges);
        driver.ammoPlate=Print("HE / 10",console,new Vector3(-.4f,.025f,-.2f),.021f);
        driver.chargesPlate=Print("CARGAS 1",console,new Vector3(.4f,.025f,-.2f),.021f);
        driver.readyFlag=Flag(console,"CARREGADO",new Vector3(0,.025f,.09f),new Color(.22f,.64f,.32f));
        driver.emptyFlag=Flag(console,"VAZIO",new Vector3(0,.026f,.09f),new Color(.6f,.22f,.16f));
        driver.busyFlag=Flag(console,"EM CARGA",new Vector3(0,.027f,.09f),new Color(.83f,.64f,.16f));
    }
    static Transform Flag(Transform parent,string caption,Vector3 position,Color color)
    {
        Transform flag=Empty("Bandeira_"+caption,parent,position);
        Part("Placa",flag,Vector3.zero,new Vector3(.19f,.018f,.23f),Material(caption,color));
        Print(caption,flag,Vector3.up*.013f,.009f); return flag;
    }
    static MechanicalDialIndicator Dial(Transform parent,Vector3 position,bool elevation)
    {
        Transform dial=Empty(elevation ? "Elevacao" : "Azimute",parent,position);
        Part("Aro",dial,Vector3.zero,new Vector3(.65f,.022f,.65f),Material("Aro",new Color(.2f,.25f,.24f)),false,PrimitiveType.Cylinder);
        Part("Mostrador",dial,Vector3.up*.025f,new Vector3(.59f,.008f,.59f),Material("Mostrador",new Color(.81f,.85f,.79f)),false,PrimitiveType.Cylinder);
        int ticks=elevation ? 9 : 36;
        for(int i=0;i<ticks;i++)
        {
            float a=elevation ? -135+i*33.75f : i*10; Vector3 radial=Quaternion.Euler(0,a,0)*Vector3.forward;
            Transform tick=Part("Marca",dial,radial*.267f+Vector3.up*.042f,new Vector3(.008f,.006f,.03f),Material("Tinta",new Color(.09f,.15f,.13f))).transform;
            tick.localRotation=Quaternion.Euler(0,a,0);
            if(elevation ? i%2==0 : i%9==0) Print(elevation ? (i*5).ToString("0") : new[]{"N","E","S","O"}[i/9],dial,radial*.21f+Vector3.up*.045f,.012f,Color.black);
        }
        Transform pointer=Empty("Ponteiro",dial,Vector3.up*.056f);
        Part("Agulha",pointer,Vector3.forward*.1f,new Vector3(.018f,.012f,.23f),Material("Agulha",new Color(.74f,.15f,.1f)));
        MechanicalDialIndicator indicator=dial.gameObject.AddComponent<MechanicalDialIndicator>(); indicator.needle=pointer;
        Print(elevation ? "ELEVACAO" : "AZIMUTE",dial,new Vector3(0,.04f,-.09f),.009f,Color.black); return indicator;
    }
    static void Wheel(Transform parent,Vector3 position,CannonController cannon,PlatformRotator platform,bool elevation)
    {
        Transform wheel=Empty(elevation ? "Volante_Elevacao" : "Volante_Base",parent,position);
        wheel.localRotation=Quaternion.Euler(-90,0,0);
        Transform rotor=Empty("Rotor",wheel,Vector3.zero);
        var metal=Material("Volantes",new Color(.32f,.39f,.36f));
        for(int i=0;i<20;i++) { float a=i*18; Vector3 radial=Quaternion.Euler(0,0,a)*Vector3.right;
            var rim=Part("Aro",rotor,radial*.18f,new Vector3(.065f,.025f,.025f),metal);
            rim.transform.localRotation=Quaternion.Euler(0,0,a+90); }
        Part("RaioX",rotor,Vector3.zero,new Vector3(.33f,.025f,.025f),metal);
        Part("RaioY",rotor,Vector3.zero,new Vector3(.025f,.33f,.025f),metal);
        Part("Manopla",rotor,new Vector3(.18f,0,-.05f),new Vector3(.045f,.045f,.11f),metal);
        var collider=wheel.gameObject.AddComponent<BoxCollider>(); collider.size=new Vector3(.43f,.43f,.2f);
        var interaction=wheel.gameObject.AddComponent<BaseHandwheelInteraction>(); interaction.cannon=cannon; interaction.platform=platform;
        interaction.elevation=elevation; interaction.wheelVisual=rotor; interaction.wheelCenter=wheel;
        interaction.displayName=elevation ? "Elevacao" : "Rotacao da base"; interaction.interactionDistance=5;
    }
    static void Map(Transform root,BaseAssembly assembly)
    {
        Part("Mesa",root,new Vector3(0,.87f,0),new Vector3(4.1f,.16f,4.1f),Material("Mesa",new Color(.25f,.31f,.28f)),true);
        for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
            Part("Pe",root,new Vector3(x*1.65f,.4f,z*1.65f),new Vector3(.12f,.8f,.12f),Material("Mesa",Color.gray),true);
        var surface=Part("MapSurface",root,new Vector3(0,.96f,0),new Vector3(3.8f,.02f,3.8f),Material("Mapa",Color.white,true),true);
        var chart=surface.AddComponent<BasePhysicalMap>(); chart.surface=surface.GetComponent<Renderer>();
        // Use a dedicated top quad with explicit UVs (north at +Z).
        Mesh quad=new Mesh(); quad.vertices=new[]{new Vector3(-1.9f,0,-1.9f),new Vector3(1.9f,0,-1.9f),new Vector3(-1.9f,0,1.9f),new Vector3(1.9f,0,1.9f)};
        quad.uv=new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(0,0),new Vector2(1,0)}; quad.triangles=new[]{0,2,1,1,2,3}; quad.RecalculateNormals();
        const string meshPath="Assets/Models/World/BaseGiratoria/MapQuad.asset";
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath); if(old==null) { AssetDatabase.CreateAsset(quad,meshPath); old=quad; } else { EditorUtility.CopySerialized(quad,old);Object.DestroyImmediate(quad); }
        Transform paper=Empty("Carta",root,new Vector3(0,.978f,0)); paper.gameObject.AddComponent<MeshFilter>().sharedMesh=old;
        paper.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Material("Mapa",Color.white,true); chart.surface=paper.GetComponent<Renderer>();
        chart.baseMarker=Part("Posicao_Base",paper,Vector3.up*.035f,new Vector3(.065f,.02f,.065f),Material("Marcador",new Color(.8f,.2f,.13f))).transform;
        for(int i=0;i<21;i++)
        {
            Print(((char)('A'+i)).ToString(),root,new Vector3(-1.82f+i*.1863f,1,-1.96f),.021f);
            Print((i+1).ToString(),root,new Vector3(-1.96f,1,-1.82f+i*.1863f),.021f);
        }
        PhysicalInstrumentBuilder.CreateMapControls(surface.transform,null);
        var load=Button(root,new Vector3(1.35f,1.01f,-1.78f),"CARREGAR AMBOS",new Color(.2f,.5f,.32f)); UnityEventTools.AddPersistentListener(load.onPressed,assembly.LoadBoth);
        var fire=Button(root,new Vector3(1.75f,1.01f,-1.78f),"FOGO AMBOS",new Color(.72f,.15f,.1f)); UnityEventTools.AddPersistentListener(fire.onPressed,assembly.FireBoth);
    }
    static PhysicalPushButton Button(Transform parent,Vector3 position,string caption,Color color)
    {
        Transform root=Empty("Botao_"+caption,parent,position);
        Part("Suporte",root,Vector3.zero,new Vector3(.2f,.025f,.2f),Material("Botoes",new Color(.15f,.2f,.18f)));
        Transform cap=Part("Tampa",root,Vector3.up*.04f,new Vector3(.13f,.022f,.13f),Material(caption,color),false,PrimitiveType.Cylinder).transform;
        var collider=root.gameObject.AddComponent<BoxCollider>(); collider.center=Vector3.up*.025f; collider.size=new Vector3(.2f,.12f,.2f);
        var button=root.gameObject.AddComponent<PhysicalPushButton>(); button.buttonVisual=cap; button.displayName=caption; button.interactionDistance=5;
        Print(caption,root,new Vector3(0,.006f,-.13f),.008f); return button;
    }
    static TextMesh Print(string value,Transform parent,Vector3 position,float size,Color? color=null)
    {
        Transform root=Empty("Gravacao_"+value,parent,position); root.localRotation=Quaternion.Euler(90,0,0);
        var text=root.gameObject.AddComponent<TextMesh>(); text.text=value; text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize=64; text.characterSize=size; text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center;
        text.color=color ?? new Color(.92f,.95f,.91f); text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material; return text;
    }
    static Material Material(string name,Color color,bool unlit=false)
    {
        string path=Materials+name.Replace('/','_')+".mat"; var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor",color); if(!unlit) { material.SetFloat("_Smoothness",.32f);material.SetFloat("_Metallic",.12f); } AssetDatabase.CreateAsset(material,path); }
        return material;
    }
    static Transform Find(Transform root,string name)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(t.name==name) return t;
        throw new InvalidOperationException("FBX node missing: "+name);
    }
    static Transform Empty(string name,Transform parent,Vector3 position)
    { var t=new GameObject(name).transform; t.SetParent(parent,false);t.localPosition=position;return t; }
    static GameObject Part(string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool collider=false,PrimitiveType type=PrimitiveType.Cube)
    {
        var part=GameObject.CreatePrimitive(type); part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;
        part.GetComponent<Renderer>().sharedMaterial=material; if(!collider) Object.DestroyImmediate(part.GetComponent<Collider>());return part;
    }
    static Camera CameraAt(string name,Transform parent,Vector3 position)
    {
        var camera=Empty(name,parent,position).gameObject.AddComponent<Camera>(); camera.nearClipPlane=.05f;camera.farClipPlane=6000;
        camera.fieldOfView=65;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.57f,.74f,.82f);camera.gameObject.AddComponent<AudioListener>();return camera;
    }
    static void SelectCamera(Camera camera,bool selected)
    { camera.enabled=selected;camera.tag=selected ? "MainCamera" : "Untagged";camera.GetComponent<AudioListener>().enabled=selected; }
}
