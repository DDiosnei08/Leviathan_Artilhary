using UnityEngine;

namespace ProjectLeviathan.Base
{
    public sealed class BaseGaugeDriver : MonoBehaviour
    {
        public CannonController cannon;
        public PlatformRotator platform;
        public MechanicalDialIndicator elevation, heading;
        public Transform readyFlag, emptyFlag, busyFlag;
        public TextMesh ammoPlate, chargesPlate;
        int previousAmmo=-1, previousCharges=-1, previousStock=-1;
        void Update()
        {
            if (cannon==null || platform==null) return;
            if (elevation!=null) elevation.SetReading(Mathf.Lerp(-135,135,Mathf.InverseLerp(cannon.minElevation,cannon.maxElevation,cannon.Elevation)));
            if (heading!=null) heading.SetReading(platform.Heading);
            Show(readyFlag,cannon.State==CannonState.Loaded); Show(emptyFlag,cannon.State==CannonState.Empty); Show(busyFlag,cannon.IsBusy);
            int stock=cannon.CurrentAmmo!=null ? cannon.CurrentAmmo.stock : 0;
            if (ammoPlate!=null && (previousAmmo!=cannon.selectedAmmo || previousStock!=stock))
            { previousAmmo=cannon.selectedAmmo; previousStock=stock; ammoPlate.text=(cannon.CurrentAmmo?.name ?? "--")+" / "+stock; }
            if (chargesPlate!=null && previousCharges!=cannon.propellantCharges)
            { previousCharges=cannon.propellantCharges; chargesPlate.text="CARGAS "+previousCharges; }
        }
        static void Show(Transform flag,bool visible) { if (flag!=null && flag.gameObject.activeSelf!=visible) flag.gameObject.SetActive(visible); }
    }
}
