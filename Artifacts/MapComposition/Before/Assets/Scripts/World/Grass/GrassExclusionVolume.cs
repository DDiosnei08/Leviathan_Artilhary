using UnityEngine;

namespace ProjectLeviathan.Grass
{
    // XZ footprint only: useful for foundations, paths and hand-placed props.
    public sealed class GrassExclusionVolume : MonoBehaviour
    {
        public Vector3 center;
        public Vector3 size = new Vector3(20, 5, 20);
        public Bounds WorldBounds
        {
            get
            {
                Matrix4x4 m = transform.localToWorldMatrix;
                Vector3 half = size * 0.5f;
                Vector3 x = m.MultiplyVector(Vector3.right * half.x);
                Vector3 y = m.MultiplyVector(Vector3.up * half.y);
                Vector3 z = m.MultiplyVector(Vector3.forward * half.z);
                return new Bounds(m.MultiplyPoint3x4(center), new Vector3(
                    Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                    Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                    Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)) * 2);
            }
        }
        void OnDrawGizmosSelected() { Gizmos.color = Color.red; Gizmos.DrawWireCube(WorldBounds.center, WorldBounds.size); }
    }
}
