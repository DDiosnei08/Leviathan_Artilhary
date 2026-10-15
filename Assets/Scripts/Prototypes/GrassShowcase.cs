using UnityEngine;

public sealed class GrassShowcase : MonoBehaviour
{
    public IntegratedBattlefieldRig rig;
    public Vector3 grassCameraPosition, grassCameraTarget;
    public Vector3 baseCameraPosition;
    public Quaternion baseCameraRotation;
    [ContextMenu("Show Grass Close Up")]
    public void ShowGrass()
    {
        rig.SetView(IntegratedBattlefieldRig.ViewMode.Overview);
        rig.overviewCamera.transform.position = grassCameraPosition;
        rig.overviewCamera.transform.LookAt(grassCameraTarget);
    }
    [ContextMenu("Show Base And Cannon")]
    public void ShowBase()
    {
        rig.SetView(IntegratedBattlefieldRig.ViewMode.Overview);
        rig.overviewCamera.transform.SetPositionAndRotation(baseCameraPosition, baseCameraRotation);
    }
}
