using UnityEngine;

namespace ProjectLeviathan.ProceduralTerrain
{
    // Generated child meshes share their chunk's lifetime; imported meshes are never assigned here.
    [ExecuteAlways]
    public sealed class GeneratedSurfaceMeshOwner : MonoBehaviour
    {
        [System.NonSerialized] public Mesh mesh;
        void OnDestroy()
        { if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);mesh=null;} }
    }
}
