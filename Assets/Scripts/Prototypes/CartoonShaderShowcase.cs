using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectLeviathan.ProceduralTerrain
{
    public class CartoonShaderShowcase : MonoBehaviour
    {
        public IntegratedBattlefieldRig rig;
        public Vector3 waterCameraPosition;
        public Vector3 waterFocusPosition;
        Vector3 baseCameraPosition;
        Quaternion baseCameraRotation;
        bool showingWater;
        bool initialized;

        void Initialize()
        {
            if (initialized || rig == null || rig.overviewCamera == null) return;
            baseCameraPosition = rig.overviewCamera.transform.position;
            baseCameraRotation = rig.overviewCamera.transform.rotation;
            initialized = true;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame)
            {
                if (showingWater) ShowBase();
                else ShowWater();
            }
        }

        [ContextMenu("Show Water View")]
        public void ShowWater()
        {
            Initialize();
            if (!initialized) return;
            rig.SetView(IntegratedBattlefieldRig.ViewMode.Overview);
            rig.overviewCamera.transform.position = waterCameraPosition;
            rig.overviewCamera.transform.LookAt(waterFocusPosition);
            showingWater = true;
            rig.terrain.RegenerateTerrain();
        }

        [ContextMenu("Show Base View")]
        public void ShowBase()
        {
            Initialize();
            if (!initialized) return;
            rig.SetView(IntegratedBattlefieldRig.ViewMode.Overview);
            rig.overviewCamera.transform.SetPositionAndRotation(baseCameraPosition, baseCameraRotation);
            showingWater = false;
            rig.terrain.RegenerateTerrain();
        }
    }
}
