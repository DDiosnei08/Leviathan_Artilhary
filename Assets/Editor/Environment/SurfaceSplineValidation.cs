using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProjectLeviathan.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class SurfaceSplineValidation
{
    public const string ScenePath = "Assets/Scenes/Tests/SurfaceSplineDemo.unity";
    const string Output = "Artifacts/MapComposition/";
    static readonly StringBuilder report = new StringBuilder();
    [MenuItem("Leviathan/Environment/Criar demonstracao de spline")]
    static void Menu()
    { if (!EditorApplication.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) CreateDemo(); }

    public static void ValidateBatch()
    {
        try
        {
            ShaderUtil.allowAsyncCompilation=false;
            report.Clear(); CreateDemo();
            foreach (var spline in Object.FindObjectsByType<SurfaceSplineMesh>()) Validate(spline);
            var test = GameObject.Find("Spline Esfera").GetComponent<SurfaceSplineMesh>();
            var previous = test.GeneratedMesh; test.Rebuild();
            Check(previous == null && test.GeneratedMesh != null, "Rebuild releases the previous owned mesh");
            test.enabled = false; Check(test.GeneratedMesh == null, "Disable releases generated mesh");
            test.enabled = true; test.Rebuild();
            var surface = test.surfaces[0]; surface.enabled = false; test.Rebuild();
            Check(test.GeneratedMesh.GetIndexCount(0) == 0 && test.MissedSamples > 0, "Missing surface produces no floating ribbon");
            surface.enabled = true; test.Rebuild();
            var direction = GameObject.Find("Spline Paisagem").GetComponent<SurfaceSplineMesh>();
            direction.direction = Vector3.zero; direction.Rebuild();
            Check(direction.LastError != null && direction.GeneratedMesh == null,"Invalid direction reported without stale output");
            direction.direction = Vector3.down; direction.Rebuild();
            direction.generateCollider = true; direction.Rebuild(); direction.Rebuild(); Validate(direction);
            Check(direction.GetComponent<MeshCollider>().sharedMesh == direction.GeneratedMesh,"Generated collider is updated and excluded from projection");
            direction.generateCollider = false; direction.Rebuild();
            var mask=direction.surfaceLayers;direction.surfaceLayers=0;direction.Rebuild();
            Check(direction.GeneratedMesh.GetIndexCount(0)==0,"Layer mask is honored even for explicit targets");
            direction.surfaceLayers=mask;
            var worldKnots=new List<SurfaceSplineKnot>(direction.knots);
            direction.transform.SetPositionAndRotation(new Vector3(4,2,7),Quaternion.Euler(0,20,0));direction.transform.localScale=new Vector3(1.4f,.7f,-.9f);
            direction.knots.Clear();foreach(var knot in worldKnots)direction.knots.Add(new SurfaceSplineKnot(direction.transform.InverseTransformPoint(knot.position),direction.transform.localToWorldMatrix.transpose.MultiplyVector(knot.normal).normalized,knot.width));
            direction.Rebuild();Validate(direction);
            var hole=new SurfaceSplineBuilder();
            hole.Append(new[]{new SurfaceSplineKnot(new Vector3(-5,0,0),Vector3.up),new SurfaceSplineKnot(new Vector3(5,0,0),Vector3.up)},false,1,.25f,4,0,.02f,1,.08f,HoleProjection);
            Check(hole.MissedSamples>0 && hole.triangles.Count>0,"Gap cuts unsupported triangles while retaining supported segments");
            for(int i=0;i<hole.triangles.Count;i+=3)
            {
                float a=hole.vertices[hole.triangles[i]].x,b=hole.vertices[hole.triangles[i+1]].x,c=hole.vertices[hole.triangles[i+2]].x;
                if(Mathf.Min(a,Mathf.Min(b,c))< -1 && Mathf.Max(a,Mathf.Max(b,c))>1)throw new InvalidOperationException("Triangle bridges the missing surface");
            }
            Check(true,"No triangle bridges the missing surface");
            var shader = Shader.Find("Leviathan/Spline Track");
            foreach (var error in ShaderUtil.GetShaderMessages(shader)) Check(error.severity.ToString() != "Error",error.message);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            captureFrames=0;EditorApplication.update+=CaptureWhenReady;
        }
        catch(Exception e)
        { Directory.CreateDirectory(Output); File.WriteAllText(Output+"SplineValidation.txt",report+"\nFAIL "+e); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static bool HoleProjection(Vector3 seed,Vector3 hint,out Vector3 p,out Vector3 n)
    {p=new Vector3(seed.x,0,seed.z);n=Vector3.up;return Mathf.Abs(seed.x)>1;}
    static int captureFrames;
    static void CaptureWhenReady()
    {
        if(++captureFrames<12)return;
        EditorApplication.update-=CaptureWhenReady;
        try
        {
            string path=Output+"Captures/Spline_Demo.png";
            DownloadedAssetSurvey.Capture(Camera.main,path,1600,900);
            var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes(path));int yellow=0;
            foreach(var p in image.GetPixels32())if(p.r>80 && p.g>50 && p.b<p.g*.75f && p.g<p.r)yellow++;
            Object.DestroyImmediate(image);
            foreach(var message in ShaderUtil.GetShaderMessages(Shader.Find("Leviathan/Spline Track")))Check(message.severity.ToString()!="Error",message.message);
            Check(yellow>1000,"GPU capture contains the intended yellow ribbon, pixels="+yellow);
            playStarted=EditorApplication.timeSinceStartup;EditorApplication.update+=VerifyPlay;
            EditorApplication.EnterPlaymode();EditorApplication.isPaused=false;
        }
        catch(Exception e){File.WriteAllText(Output+"SplineValidation.txt",report+"\nFAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static double playStarted;
    static void VerifyPlay()
    {
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup-playStarted<3)return;
        EditorApplication.update-=VerifyPlay;
        try
        {
            Check(Object.FindAnyObjectByType<ProjectLeviathan.ProceduralTerrain.TerrainWorldController>()==null,"Demo Play does not spawn the fallback game terrain");
            var splines=Object.FindObjectsByType<SurfaceSplineMesh>();Check(splines.Length==3,"Three independent spline surfaces remain in Play");
            foreach(var spline in splines)Check(spline.GeneratedMesh!=null && spline.GeneratedMesh.GetIndexCount(0)>0,spline.name+": survives entering Play");
            File.WriteAllText(Output+"SplineValidation.txt",report.ToString());Debug.Log("SURFACE_SPLINE_VALIDATION_OK\n"+report);EditorApplication.Exit(0);
        }
        catch(Exception e){File.WriteAllText(Output+"SplineValidation.txt",report+"\nFAIL "+e);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Check(bool pass,string message)
    { if(!pass) throw new InvalidOperationException(message); report.AppendLine("PASS "+message); }
    static void Validate(SurfaceSplineMesh spline)
    {
        spline.Rebuild(); Check(spline.LastError == null && spline.GeneratedMesh.vertexCount > 100, spline.name+": generated mesh");
        Check(spline.MissedSamples == 0,spline.name+": every vertex has supporting surface");
        Check(spline.RejectedTriangles == 0,spline.name+": continuous ribbon without rejected triangles (actual "+spline.RejectedTriangles+")");
        var mesh = spline.GeneratedMesh; var vertices = mesh.vertices; var normals = mesh.normals;
        float maximumError = 0;
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 n=spline.transform.worldToLocalMatrix.transpose.MultiplyVector(normals[i]).normalized;
            Vector3 p=spline.transform.TransformPoint(vertices[i])-n*spline.surfaceOffset;
            CheckFinite(p);
            if(!spline.Project(p,n,out Vector3 hit,out _)) throw new InvalidOperationException("Contact probe failed");
            maximumError=Mathf.Max(maximumError,Vector3.Distance(p,hit));
        }
        Check(maximumError < .002f,spline.name+": surface contact error below 2mm; max="+maximumError);
        if(spline.closed)
        {
            int stride=spline.widthSegments+1;
            for(int i=0;i<stride;i++) Check(Vector3.Distance(vertices[i],vertices[vertices.Length-stride+i])<.0001f,"Closed seam vertex "+i);
        }
    }
    static void CheckFinite(Vector3 p)
    { if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.sqrMagnitude)) throw new InvalidOperationException("Non-finite vertex"); }
    public static void CreateDemo()
    {
        Directory.CreateDirectory("Assets/Models/Environment/SplineDemo");
        Directory.CreateDirectory("Assets/Materials/Environment"); AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var track=MaterialAsset("SplineTrack",Shader.Find("Leviathan/Spline Track"),new Color(.52f,.55f,.46f));
        var highlight=MaterialAsset("SplineDemoRibbon",Shader.Find("Leviathan/Spline Track"),new Color(.92f,.61f,.13f));
        highlight.SetFloat("_EdgeWear",0);highlight.SetFloat("_TrackStrength",.25f);EditorUtility.SetDirty(highlight);
        var ground=MaterialAsset("SplineDemoSurface",Shader.Find("Universal Render Pipeline/Lit"),new Color(.26f,.42f,.43f));
        var terrain=MeshObject("Paisagem",Landscape(),new Vector3(-15,0,0),ground);
        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name="Esfera";
        sphere.transform.position=new Vector3(0,4,0);sphere.transform.localScale=Vector3.one*8;sphere.GetComponent<Renderer>().sharedMaterial=ground;
        var cone=MeshObject("Cone",Cone(),new Vector3(14,0,0),ground);
        var path=NewSpline("Spline Paisagem",terrain,highlight);path.width=1.6f;
        path.knots=new List<SurfaceSplineKnot>();
        foreach(var p in new[]{new Vector3(-21,4,-4),new Vector3(-19,4,1),new Vector3(-14,4,2),new Vector3(-9,4,-3)})path.knots.Add(new SurfaceSplineKnot(p,Vector3.up));
        var ring=NewSpline("Spline Esfera",sphere,highlight);ring.projection=SurfaceProjectionMode.Radial;ring.radialCenter=sphere.transform;ring.closed=true;ring.width=1.2f;
        ring.knots=new List<SurfaceSplineKnot>();
        for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;var n=new Vector3(Mathf.Cos(a),.25f,Mathf.Sin(a)).normalized;ring.knots.Add(new SurfaceSplineKnot(sphere.transform.position+n*4,n));}
        var helix=NewSpline("Spline Cone",cone,highlight);helix.projection=SurfaceProjectionMode.KnotNormals;helix.width=.8f;helix.knots=new List<SurfaceSplineKnot>();
        for(int i=0;i<16;i++)
        {float a=i/15f*Mathf.PI*2.8f,y=.8f+i/15f*5.8f,r=4*(1-y/8);var n=new Vector3(Mathf.Cos(a),.5f,Mathf.Sin(a)).normalized;helix.knots.Add(new SurfaceSplineKnot(new Vector3(14+r*Mathf.Cos(a),y,r*Mathf.Sin(a)),n));}
        foreach(var spline in new[]{path,ring,helix}){spline.sampleSpacing=.12f;spline.widthSegments=12;spline.surfaceOffset=.035f;spline.maximumSurfaceError=.08f;spline.probeAbove=12;spline.probeBelow=20;spline.Rebuild();}
        var sun=new GameObject("Sol").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.4f;sun.transform.rotation=Quaternion.Euler(45,-30,0);sun.shadows=LightShadows.Soft;
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.45f,.5f);RenderSettings.fog=false;
        var camera=new GameObject("Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.09f,.1f);
        camera.transform.position=new Vector3(8,22,-36);camera.transform.LookAt(new Vector3(-2,3,0));camera.orthographic=true;camera.orthographicSize=12;
        camera.gameObject.AddComponent<ProjectLeviathan.ProceduralTerrain.TerrainBootstrapOptOut>();
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
    }
    static SurfaceSplineMesh NewSpline(string name,GameObject surface,Material material)
    {var spline=new GameObject(name).AddComponent<SurfaceSplineMesh>();spline.surfaces=new[]{surface.GetComponent<Collider>()};spline.material=material;return spline;}
    public static Material MaterialAsset(string name,Shader shader,Color color)
    {
        string path="Assets/Materials/Environment/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.SetColor("_BaseColor",color);EditorUtility.SetDirty(material);return material;
    }
    static GameObject MeshObject(string name,Mesh mesh,Vector3 position,Material material)
    {
        string path="Assets/Models/Environment/SplineDemo/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
        var go=new GameObject(name);go.transform.position=position;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
    }
    static Mesh Landscape()
    {
        var v=new List<Vector3>();var t=new List<int>();const int n=40;
        for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){float px=x*.4f-8,pz=z*.4f-8;v.Add(new Vector3(px,1+Mathf.Sin(px*.45f)*Mathf.Cos(pz*.32f)*1.4f,pz));}
        for(int z=0;z<n;z++)for(int x=0;x<n;x++){int a=z*(n+1)+x;t.AddRange(new[]{a,a+n+1,a+1,a+1,a+n+1,a+n+2});}
        var m=new Mesh{name="Spline Demo Landscape"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static Mesh Cone()
    {
        var v=new List<Vector3>();var t=new List<int>();const int n=128;
        for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;int k=v.Count;v.Add(new Vector3(4*Mathf.Cos(a),0,4*Mathf.Sin(a)));v.Add(Vector3.up*8);v.Add(new Vector3(4*Mathf.Cos(b),0,4*Mathf.Sin(b)));t.AddRange(new[]{k,k+1,k+2});}
        var m=new Mesh{name="Spline Demo Cone"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
}
