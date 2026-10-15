using UnityEngine;

namespace ProjectLeviathan.Base
{
    public sealed class BaseAssembly : MonoBehaviour
    {
        public PlatformRotator platform;
        public CannonController left, right;
        public Transform leftStation, rightStation, mapStation, playerSpawn;
        public void LoadBoth() { left.RequestLoad(); right.RequestLoad(); }
        public void FireBoth() { if (!platform.IsMoving) { left.Fire(); right.Fire(); } }
        void Awake()
        {
            // A shot cannot be prepared while the massive platform is moving.
            left.onFire.AddListener(platform.Stop); right.onFire.AddListener(platform.Stop);
        }
    }
}
