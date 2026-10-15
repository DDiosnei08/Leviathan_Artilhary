using System.Collections.Generic;
using System.IO;
using ProjectLeviathan.Base;
using ProjectLeviathan.Grass;
using ProjectLeviathan.ProceduralTerrain;
using ProjectLeviathan.Vegetation;
using ProjectLeviathan.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class MapCompositionSetup
{
    [MenuItem("Leviathan/Environment/Aplicar composicao do mapa")]
    static void Menu()
    {if(!EditorApplication.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())ApplyBatch();}
    public static void ApplyBatch()
    {
        Directory.CreateDirectory("Assets/Prefabs/Environment");Directory.CreateDirectory("Assets/Models/Environment/Understory");AssetDatabase.Refresh();
        var scene=EditorSceneManager.OpenScene(BaseIntegrationBuilder.ScenePath);
        var rig=Object.FindAnyObjectByType<BaseGameRig>();var settings=rig.terrain.terrainSettings;
        var scenery=Object.FindAnyObjectByType<WorldSceneryManager>();var trees=Object.FindAnyObjectByType<ProceduralTreeManager>();
        scenery.enabled=false;trees.enabled=false;rig.grass.enabled=false;
        var grass=rig.grass.configuration;grass.meadowPatchSize=38;grass.meadowVariation=.85f;grass.meadowMinimumDensity=.14f;
        grass.heightRange=new Vector2(.45f,1.05f);EditorUtility.SetDirty(grass);
        trees.configuration.groveThreshold=.48f;trees.configuration.groveEdgeBlend=.1f;
        trees.configuration.scatteredDensity=.065f;trees.configuration.heights=new Vector2(6,16);EditorUtility.SetDirty(trees.configuration);
        settings.erosion.channelDepth=13;settings.erosion.channelWidth=.1f;settings.erosion.channelScale=105;
        settings.surfacePalette.climateBlendStrength=.46f;settings.surfacePalette.rockySlopeThreshold=24;
        var terrainMaterial=settings.terrainMaterial;terrainMaterial.SetFloat("_PatchStrength",.29f);terrainMaterial.SetFloat("_SlopeStrength",.43f);
        terrainMaterial.SetFloat("_DetailStrength",.055f);terrainMaterial.SetFloat("_PatchScale",.018f);EditorUtility.SetDirty(terrainMaterial);
        settings.roadMaterial=SurfaceSplineValidation.MaterialAsset("SplineTrack",Shader.Find("Leviathan/Spline Track"),new Color(.38f,.4f,.34f));
        settings.roadSurfaceLift=.1f;
        var paint=SurfaceSplineValidation.MaterialAsset("IndustrialPaint",Shader.Find("Leviathan/Dieselpunk Surface"),new Color(.24f,.34f,.34f));
        var steel=SurfaceSplineValidation.MaterialAsset("IndustrialSteel",Shader.Find("Leviathan/Dieselpunk Surface"),new Color(.18f,.21f,.22f));steel.SetFloat("_Metallic",.65f);
        var concrete=SurfaceSplineValidation.MaterialAsset("IndustrialConcrete",Shader.Find("Leviathan/Dieselpunk Surface"),new Color(.48f,.47f,.42f));concrete.SetFloat("_WearAmount",0);
        var signal=SurfaceSplineValidation.MaterialAsset("IndustrialLamp",Shader.Find("Leviathan/Dieselpunk Surface"),new Color(.86f,.54f,.22f));signal.SetColor("_EmissionColor",new Color(2,1.1f,.35f));
        var smoke=SurfaceSplineValidation.MaterialAsset("IndustrialMist",Shader.Find("Leviathan/Industrial Mist"),Color.white);
        for(int i=0;i<3;i++)
        {
            string id=i==0?"field-outpost":i==1?"supply-depot":"industrial-works";
            var definition=settings.poiDefinitions.Find(p=>p.id==id);
            if(definition==null){definition=new TerrainPOIDefinition{id=id,displayName="Usina de bombeamento"};settings.poiDefinitions.Add(definition);}
            definition.visualPrefab=IndustrialPrefab(i,paint,steel,concrete,signal,smoke);
            definition.minimumFlatAreaRadiusMeters=30;definition.maximumSlopeDegrees=2;
            definition.infiniteSpawnChancePerChunk=i==2?.28f:.38f;definition.minimumSpacingMeters=420;
            definition.visualSurfaceOffsetMeters=0;definition.useFallbackVisual=false;
        }
        foreach(var p in scenery.configuration.prototypes)if(p.role==SceneryRole.Lamp){settings.roadsidePoleMesh=p.mesh;settings.roadsidePoleMaterial=p.material;break;}
        settings.roadsideCableMaterial=steel;scenery.configuration.roadsideFurniture=false;
        var prototypes=new List<SceneryPrototype>(scenery.configuration.prototypes);
        prototypes.RemoveAll(p=>p.role==SceneryRole.Shrub || p.role==SceneryRole.FallenLog);
        Mesh shrub=SaveMesh(TreeMeshFactory.Create(false,true),"Shrub");
        var leaf=SurfaceSplineValidation.MaterialAsset("UnderstoryLeaves",Shader.Find("Leviathan/Cartoon Trees"),new Color(.7f,.85f,.6f));leaf.enableInstancing=true;
        prototypes.Add(new SceneryPrototype{label="Understory shrub",role=SceneryRole.Shrub,mesh=shrub,material=leaf,sizeRange=new Vector2(.9f,2.1f)});
        var cylinder=GameObject.CreatePrimitive(PrimitiveType.Cylinder);var log=new Mesh{name="Fallen timber"};
        log.CombineMeshes(new[]{new CombineInstance{mesh=cylinder.GetComponent<MeshFilter>().sharedMesh,transform=Matrix4x4.TRS(new Vector3(0,.13f,0),Quaternion.Euler(90,0,0),new Vector3(.24f,.5f,.24f))}});
        Object.DestroyImmediate(cylinder);log=SaveMesh(log,"FallenLog");
        var wood=SurfaceSplineValidation.MaterialAsset("FallenWood",Shader.Find("Leviathan/Dieselpunk Surface"),new Color(.24f,.22f,.17f));wood.enableInstancing=true;wood.SetFloat("_WearAmount",0);
        prototypes.Add(new SceneryPrototype{label="Fallen timber",role=SceneryRole.FallenLog,mesh=log,material=wood,sizeRange=new Vector2(3,6)});
        scenery.configuration.prototypes=prototypes.ToArray();scenery.configuration.maximumInstancesPerChunk=950;scenery.configuration.rockDensity=.42f;
        var sky=Object.FindAnyObjectByType<LayeredSkyController>();
        if(sky!=null){sky.nightAmbientSky=new Color(.18f,.24f,.31f);sky.nightAmbientHorizon=new Color(.12f,.16f,.2f);sky.moonIntensity=.36f;EditorUtility.SetDirty(sky);}
        var prefabPath=AssetDatabase.GetAssetPath(rig.spawner.basePrefab);
        var baseRoot=PrefabUtility.LoadPrefabContents(prefabPath);
        var previous=baseRoot.transform.Find("Luzes de Servico");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
        var lamps=new GameObject("Luzes de Servico");lamps.transform.SetParent(baseRoot.transform,false);
        var baseAssembly=baseRoot.GetComponent<BaseAssembly>();var lights=new List<Light>();
        foreach(var station in new[]{baseAssembly.leftStation,baseAssembly.rightStation,baseAssembly.mapStation})if(station!=null)
            lights.Add(ServiceLamp(lamps.transform,baseRoot.transform.InverseTransformPoint(station.position)+Vector3.up*2.3f,steel,signal));
        lamps.AddComponent<LocalAtmosphere>().lights=lights.ToArray();
        PrefabUtility.SaveAsPrefabAsset(baseRoot,prefabPath);PrefabUtility.UnloadPrefabContents(baseRoot);
        settings.Validate();EditorUtility.SetDirty(settings);EditorUtility.SetDirty(scenery.configuration);
        foreach(var m in new[]{paint,steel,concrete,signal,leaf,wood})EditorUtility.SetDirty(m);
        trees.enabled=true;scenery.enabled=true;rig.grass.enabled=true;
        if(!rig.spawner.RebuildEditorPreview())throw new System.InvalidOperationException("No naturally supported base site after composition setup.");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("MAP_COMPOSITION_SETUP_OK");
    }
    static Mesh SaveMesh(Mesh mesh,string name)
    {
        string path="Assets/Models/Environment/Understory/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);return old;
    }
    static GameObject IndustrialPrefab(int kind,Material paint,Material steel,Material concrete,Material signal,Material smoke)
    {
        string name=kind==0?"RelayOutpost":kind==1?"SupplyDepot":"PumpingWorks";
        var root=new GameObject(name);var parts=new List<Renderer>();
        void Part(string label,PrimitiveType type,Vector3 p,Vector3 scale,Material mat)
        {var go=GameObject.CreatePrimitive(type);go.name=label;go.transform.SetParent(root.transform,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;parts.Add(go.GetComponent<Renderer>());}
        Part("Buried footing",PrimitiveType.Cube,new Vector3(0,-.5f,0),new Vector3(26,3,20),concrete);
        Part("Service hall",PrimitiveType.Cube,new Vector3(-3,4,0),new Vector3(16,6,12),paint);
        Part("Roof",PrimitiveType.Cube,new Vector3(-3,7.2f,0),new Vector3(17,.6f,13),steel);
        for(int i=0;i<5;i++)Part("Roof ribs",PrimitiveType.Cube,new Vector3(-9+i*3,7.7f,0),new Vector3(.25f,.65f,13),steel);
        Part("Door",PrimitiveType.Cube,new Vector3(-3,3.2f,-6.08f),new Vector3(3.2f,4.4f,.2f),steel);
        for(int i=0;i<3;i++)Part("Window",PrimitiveType.Cube,new Vector3(-8+i*5,5.7f,-6.15f),new Vector3(2.2f,1.2f,.15f),signal);
        var particles=new List<ParticleSystem>();
        if(kind==0)
        {
            for(int i=0;i<4;i++)Part("Tower leg",PrimitiveType.Cube,new Vector3(8+(i%2)*3,9,-3+(i/2)*3),new Vector3(.55f,16,.55f),steel);
            Part("Watch cabin",PrimitiveType.Cube,new Vector3(9.5f,17,-1.5f),new Vector3(5,3,5),paint);
            Part("Antenna",PrimitiveType.Cylinder,new Vector3(9.5f,24,-1.5f),new Vector3(.25f,6,.25f),steel);
            Part("Antenna cross",PrimitiveType.Cube,new Vector3(9.5f,28,-1.5f),new Vector3(6,.2f,.2f),steel);
        }
        else
        {
            for(int i=0;i<2;i++){Part("Reservoir",PrimitiveType.Cylinder,new Vector3(8,5,-4+i*8),new Vector3(5,4,5),paint);Part("Tank ring",PrimitiveType.Cylinder,new Vector3(8,8.4f,-4+i*8),new Vector3(5.2f,.18f,5.2f),steel);}
            if(kind==2)
            {
                Part("Chimney",PrimitiveType.Cylinder,new Vector3(-8,17,4),new Vector3(2.2f,12,2.2f),concrete);
                Part("Chimney cap",PrimitiveType.Cylinder,new Vector3(-8,29,4),new Vector3(2.6f,.6f,2.6f),steel);
                particles.Add(Particle(root.transform,"Fumaca",new Vector3(-8,30,4),smoke,false));
                particles.Add(Particle(root.transform,"Bruma rasa",new Vector3(0,1,28),smoke,true));
            }
            else for(int i=0;i<5;i++)Part("Stored supplies",PrimitiveType.Cube,new Vector3(-10+i*3,2.2f,-8.2f),new Vector3(2,2.4f,2),steel);
        }
        Light light=ServiceLamp(root.transform,new Vector3(-3,7,-7),steel,signal);
        var fx=root.AddComponent<LocalAtmosphere>();fx.lights=new[]{light};fx.particles=particles.ToArray();
        var lod=root.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.015f,parts.ToArray())});lod.RecalculateBounds();
        string path="Assets/Prefabs/Environment/"+name+".prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return prefab;
    }
    static Light ServiceLamp(Transform parent,Vector3 position,Material steel,Material emissive)
    {
        var housing=GameObject.CreatePrimitive(PrimitiveType.Cube);housing.name="Service lamp";housing.transform.SetParent(parent,false);housing.transform.localPosition=position;housing.transform.localScale=new Vector3(.5f,.3f,.4f);housing.GetComponent<Renderer>().sharedMaterial=steel;
        var lens=GameObject.CreatePrimitive(PrimitiveType.Cube);lens.name="Warm lens";lens.transform.SetParent(parent,false);lens.transform.localPosition=position+Vector3.down*.17f;lens.transform.localScale=new Vector3(.4f,.06f,.3f);lens.GetComponent<Renderer>().sharedMaterial=emissive;
        var go=new GameObject("Service light");go.transform.SetParent(parent,false);go.transform.localPosition=position+Vector3.down*.4f;
        var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.67f,.35f);light.intensity=3;light.range=14;light.shadows=LightShadows.None;return light;
    }
    public static ParticleSystem Particle(Transform parent,string name,Vector3 p,Material material,bool mist)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(-90,0,0);
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.maxParticles=mist?12:40;main.startLifetime=mist?25:18;main.startSpeed=mist?.1f:1.5f;main.startSize=mist?25:4;
        main.startColor=mist?new Color(.58f,.66f,.66f,.08f):new Color(.22f,.24f,.25f,.3f);main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission;emission.rateOverTime=mist?.3f:1.5f;
        var shape=ps.shape;shape.shapeType=mist?ParticleSystemShapeType.Box:ParticleSystemShapeType.Cone;shape.radius=.8f;shape.angle=8;shape.scale=mist?new Vector3(80,80,1):Vector3.one;
        var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.4f,1,1.5f));
        var alpha=ps.colorOverLifetime;alpha.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});alpha.color=gradient;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;ps.Play();return ps;
    }
}
