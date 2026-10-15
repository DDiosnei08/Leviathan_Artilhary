using ProjectLeviathan.ProceduralTerrain;
using UnityEngine;

namespace ProjectLeviathan.Base
{
    /// <summary>A physical, seed-linked overview chart. It is not an aerial photograph or an aiming computer.</summary>
    public sealed class BasePhysicalMap : MonoBehaviour
    {
        public Renderer surface;
        public Transform baseMarker;
        public float paperSize = 3.8f;
        Texture2D texture;
        MaterialPropertyBlock properties;
        public void SetWorld(TerrainSettings world, BaseSiteData site)
        {
            if (surface==null) return;
            float extent=world.FiniteMapWorldSize;
            float minimum=-(world.finiteMapChunksPerSide/2)*world.chunkSize;
            var coarse=Instantiate(world); coarse.chunkSize=extent; coarse.chunkResolution=205;
            // Keep world-space noise and seed, but sample a coarse chart rather than loading the whole world.
            HeightMap map=HeightMapGenerator.GenerateHeightMap(coarse.CaptureGenerationSettings(false),205,205,new Vector2(minimum,minimum));
            const int resolution=1024;
            var colors=new Color32[resolution*resolution];
            for(int y=0;y<resolution;y++) for(int x=0;x<resolution;x++)
            {
                float u=x/(float)(resolution-1),v=y/(float)(resolution-1);
                int mx=Mathf.Min(204,(int)(u*204)),my=Mathf.Min(204,(int)(v*204));
                float h=map.Values[mx,my];
                float shade=map.Water[mx,my]>.5f ? .08f : Mathf.Lerp(.25f,.7f,h);
                float wx=u*extent,wz=v*extent;
                float major=Mathf.Min(Mathf.Repeat(wx,1000),Mathf.Repeat(wz,1000));
                float minor=Mathf.Min(Mathf.Repeat(wx,100),Mathf.Repeat(wz,100));
                if(major<extent/resolution*1.3f) shade=.88f;
                else if(minor<extent/resolution*.6f) shade=Mathf.Lerp(shade,.65f,.3f);
                colors[y*resolution+x]=new Color(shade,shade,shade,1);
            }
            ReleaseTexture();
            texture=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false) { name="Seed chart "+site.seed,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear };
            texture.SetPixels32(colors); texture.Apply(false,true);
            if(properties==null) properties=new MaterialPropertyBlock();
            properties.SetTexture("_BaseMap",texture); surface.SetPropertyBlock(properties);
            if(baseMarker!=null) baseMarker.localPosition=new Vector3(((site.center.x-minimum)/extent-.5f)*paperSize,.035f,((site.center.y-minimum)/extent-.5f)*paperSize);
            if(Application.isPlaying) Destroy(coarse); else DestroyImmediate(coarse);
        }
        void ReleaseTexture()
        {
            if(texture==null) return;
            if(Application.IsPlaying(gameObject)) Destroy(texture);else DestroyImmediate(texture);
            texture=null;
        }
        void OnDestroy() => ReleaseTexture();
    }
}
