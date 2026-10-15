using System.Collections.Generic;
using UnityEngine;

namespace ProjectLeviathan.Vegetation
{
    public static class TreeMeshFactory
    {
        public static Mesh Create(bool pine,bool lowDetail)
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
            int sides=lowDetail ? 5 : 8;
            AddShape(vertices,colors,indices,new Vector3(0,.29f,0),new Vector3(.035f,.29f,.035f),sides,2,new Color(.31f,.23f,.15f,0));
            if(pine)
            {
                for(int i=0;i<(lowDetail ? 2 : 3);i++)
                    AddShape(vertices,colors,indices,new Vector3(0,.54f+i*.18f,0),new Vector3(.32f-i*.06f,.27f,.32f-i*.06f),sides,2,new Color(.2f,.41f+i*.035f,.28f,1),true);
            }
            else
            {
                AddShape(vertices,colors,indices,new Vector3(0,.77f,0),new Vector3(.39f,.31f,.35f),sides,lowDetail ? 3 : 5,new Color(.37f,.57f,.28f,1));
                if(!lowDetail)
                {
                    AddShape(vertices,colors,indices,new Vector3(-.2f,.67f,.06f),new Vector3(.26f,.23f,.29f),sides,4,new Color(.25f,.45f,.23f,1));
                    AddShape(vertices,colors,indices,new Vector3(.19f,.72f,-.1f),new Vector3(.25f,.25f,.27f),sides,4,new Color(.43f,.61f,.3f,1));
                }
            }
            var mesh=new Mesh {name=(pine ? "Pine" : "Broadleaf")+(lowDetail ? " LOD1" : " LOD0")};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void AddShape(List<Vector3> v,List<Color> colors,List<int> triangles,Vector3 center,Vector3 scale,int sides,int rings,Color color,bool cone=false)
        {
            int start=v.Count;
            for(int y=0;y<=rings;y++)
            {
                float t=y/(float)rings;
                float radius=color.a==0 ? 1-t*.3f : cone ? 1-t : Mathf.Max(.02f,Mathf.Sin(t*Mathf.PI));
                for(int x=0;x<sides;x++)
                {
                    float angle=x*Mathf.PI*2/sides;
                    v.Add(center+Vector3.Scale(new Vector3(Mathf.Cos(angle)*radius,t*2-1,Mathf.Sin(angle)*radius),scale));
                    colors.Add(color);
                    if(y==rings) continue;
                    int a=start+y*sides+x,b=start+y*sides+(x+1)%sides,c=a+sides,d=b+sides;
                    triangles.Add(a);triangles.Add(c);triangles.Add(b);triangles.Add(b);triangles.Add(c);triangles.Add(d);
                }
            }
        }
    }
}
