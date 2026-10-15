using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class CuratedMeshBaker
{
    public static Mesh Bake(GameObject source,bool tree,bool coarse)
    {
        var model=Object.Instantiate(source);var vertices=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
        var textures=new Dictionary<Texture,Texture2D>();
        try
        {
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();if(mesh==null || renderer==null) continue;
                var positions=mesh.vertices;var uv=mesh.uv;var materials=renderer.sharedMaterials;
                Matrix4x4 matrix=model.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var material=materials.Length>0 ? materials[Mathf.Min(sub,materials.Length-1)] : null;
                    Color tint=material!=null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").linear : material!=null && material.HasProperty("_Color") ? material.GetColor("_Color").linear : Color.gray.linear;
                    if(material!=null && material.name.StartsWith("Wood_") && tint.r>.9f && tint.g>.9f && tint.b>.9f)
                        tint=(material.name.Contains("dark") ? new Color(.25f,.14f,.06f) : new Color(.48f,.3f,.13f)).linear;
                    string property=material!=null && material.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                    Texture texture=material!=null && material.HasProperty(property) ? material.GetTexture(property) : null;
                    Texture2D readable=null;
                    if(texture!=null && !textures.TryGetValue(texture,out readable))
                    {
                        int w=Mathf.Min(texture.width,1024),h=Mathf.Min(texture.height,1024);var active=RenderTexture.active;
                        var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                        Graphics.Blit(texture,rt);RenderTexture.active=rt;
                        readable=new Texture2D(w,h,TextureFormat.RGBA32,false,true);readable.ReadPixels(new Rect(0,0,w,h),0,0);readable.Apply();
                        RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);textures.Add(texture,readable);
                    }
                    Vector2 scale=texture!=null ? material.GetTextureScale(property) : Vector2.one,offset=texture!=null ? material.GetTextureOffset(property) : Vector2.zero;
                    var lookup=new Dictionary<int,int>();
                    foreach(int index in mesh.GetTriangles(sub))
                    {
                        if(!lookup.TryGetValue(index,out int target))
                        {
                            Color color=tint;
                            if(readable!=null && uv.Length==positions.Length) {Vector2 p=Vector2.Scale(uv[index],scale)+offset;color*=readable.GetPixelBilinear(Mathf.Repeat(p.x,1),Mathf.Repeat(p.y,1));}
                            if(source.name.StartsWith("Stone_")) color*=new Color(.28f,.31f,.34f,1);
                            color.a=tree && color.g>color.r*1.1f && color.g>color.b*1.05f ? 1 : 0;
                            target=vertices.Count;lookup.Add(index,target);vertices.Add(matrix.MultiplyPoint3x4(positions[index]));colors.Add(color);
                        }
                        indices.Add(target);
                    }
                }
            }
            Bounds bounds=new Bounds(vertices[0],Vector3.zero);foreach(var vertex in vertices) bounds.Encapsulate(vertex);
            float dimension=tree ? bounds.size.y : Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            Vector3 origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            for(int i=0;i<vertices.Count;i++) vertices[i]=(vertices[i]-origin)/Mathf.Max(.001f,dimension);
            if(coarse) Cluster(vertices,colors,indices,tree ? .035f : .06f);
            var result=new Mesh {name=source.name+(coarse ? " LOD1" : " Curated"),indexFormat=IndexFormat.UInt32};
            result.SetVertices(vertices);result.SetColors(colors);result.SetTriangles(indices,0);result.RecalculateNormals();result.RecalculateBounds();return result;
        }
        finally {foreach(var texture in textures.Values) Object.DestroyImmediate(texture);Object.DestroyImmediate(model);}
    }
    static void Cluster(List<Vector3> vertices,List<Color> colors,List<int> indices,float cell)
    {
        var lookup=new Dictionary<Vector4Int,int>();var positions=new List<Vector3>();var sums=new List<Color>();var counts=new List<int>();var remap=new int[vertices.Count];
        for(int i=0;i<vertices.Count;i++)
        {
            Color32 color=colors[i];int packed=(color.r/32)<<9 | (color.g/32)<<6 | (color.b/32)<<3 | (color.a>0 ? 1 : 0);
            Vector3 p=vertices[i];var key=new Vector4Int(Mathf.RoundToInt(p.x/cell),Mathf.RoundToInt(p.y/cell),Mathf.RoundToInt(p.z/cell),packed);
            if(!lookup.TryGetValue(key,out int slot)) {slot=positions.Count;lookup.Add(key,slot);positions.Add(Vector3.zero);sums.Add(Color.clear);counts.Add(0);}
            positions[slot]+=p;sums[slot]+=colors[i];counts[slot]++;remap[i]=slot;
        }
        float minimum=float.MaxValue;for(int i=0;i<positions.Count;i++) {positions[i]/=counts[i];sums[i]/=counts[i];minimum=Mathf.Min(minimum,positions[i].y);}
        for(int i=0;i<positions.Count;i++) positions[i]-=Vector3.up*minimum;
        var triangles=new List<int>();for(int i=0;i<indices.Count;i+=3)
        {int a=remap[indices[i]],b=remap[indices[i+1]],c=remap[indices[i+2]];if(a==b || b==c || c==a) continue;triangles.Add(a);triangles.Add(b);triangles.Add(c);}
        vertices.Clear();vertices.AddRange(positions);colors.Clear();colors.AddRange(sums);indices.Clear();indices.AddRange(triangles);
    }
    readonly struct Vector4Int : System.IEquatable<Vector4Int>
    {
        readonly int x,y,z,w;
        public Vector4Int(int x,int y,int z,int w) {this.x=x;this.y=y;this.z=z;this.w=w;}
        public bool Equals(Vector4Int other) => x==other.x && y==other.y && z==other.z && w==other.w;
        public override bool Equals(object other) => other is Vector4Int key && Equals(key);
        public override int GetHashCode() {unchecked{return ((x*73856093)^(y*19349663)^(z*83492791))^w;}}
    }
}
