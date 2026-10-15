using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectLeviathan.Base
{
    public enum CannonState { Empty, Preparing, Loading, Loaded, Recoiling }
    [Serializable] public sealed class AmmoType
    {
        public string name = "HE";
        public GameObject projectilePrefab;
        public float muzzleVelocity = 250f;
        public int stock = 10;
    }
    /// <summary>Independent breech, elevation, ammunition and recoil. Axes use the imported pivot space.</summary>
    public sealed class CannonController : MonoBehaviour
    {
        public Transform elevationPivot, recoilPivot, doorHinge, muzzle;
        public PlatformRotator platform;
        public Vector3 elevationAxis = Vector3.left, recoilAxis = Vector3.up, doorAxis = Vector3.forward;
        public Vector3 muzzleLocalDirection = Vector3.up;
        public bool invertElevation;
        public float minElevation = 0, maxElevation = 40, loadElevation = 5, elevationSpeed = 6;
        public float doorOpenAngle = 100, doorTime = 1.2f, loadTime = 2.5f;
        [Tooltip("Recoil travel in world metres, independent of the FBX scale.")]
        public float recoilDistance = 1, recoilTime = .12f, recoverTime = 1.6f;
        public AmmoType[] ammo = { new AmmoType(), new AmmoType { name = "AP", stock = 10 } };
        public int selectedAmmo;
        [Range(1,5)] public int propellantCharges = 1;
        public UnityEvent onLoadStart = new UnityEvent(), onLoadFinished = new UnityEvent(), onFire = new UnityEvent();
        public UnityEvent<CannonState> onStateChanged = new UnityEvent<CannonState>();
        public CannonState State { get; private set; }
        public float Elevation { get; private set; }
        public bool IsBusy => State == CannonState.Preparing || State == CannonState.Loading || State == CannonState.Recoiling;
        public bool IsConfigured => elevationPivot != null && recoilPivot != null && doorHinge != null && muzzle != null;
        public AmmoType CurrentAmmo => ammo != null && selectedAmmo >= 0 && selectedAmmo < ammo.Length ? ammo[selectedAmmo] : null;
        public Vector3 ShotDirection => muzzle != null ? muzzle.TransformDirection(muzzleLocalDirection).normalized : transform.forward;
        Quaternion elevationRest, doorRest;
        Vector3 recoilRest;
        float elevationInput;
        AmmoType loadedAmmo, reservedAmmo;
        bool initialized;
        void Awake() => Initialize();
        public void Initialize()
        {
            if (initialized || !IsConfigured) return;
            elevationRest = elevationPivot.localRotation; doorRest = doorHinge.localRotation; recoilRest = recoilPivot.localPosition;
            initialized = true; Elevation = Mathf.Clamp(loadElevation,minElevation,maxElevation); ApplyElevation();
        }
        public void SetElevationInput(float value) => elevationInput = Mathf.Clamp(value,-1,1);
        public float AdjustElevation(float degrees)
        {
            Initialize(); if (!initialized || IsBusy) return 0;
            float old = Elevation; Elevation = Mathf.Clamp(old+degrees,minElevation,maxElevation); ApplyElevation(); return Elevation-old;
        }
        public void SelectAmmo(int index)
        { if (State == CannonState.Empty && ammo != null && index >= 0 && index < ammo.Length) selectedAmmo=index; }
        public void CycleAmmo() { if (ammo != null && ammo.Length>0) SelectAmmo((selectedAmmo+1)%ammo.Length); }
        public void CycleCharges() { if (State==CannonState.Empty) propellantCharges=propellantCharges%5+1; }
        public void LoadCommand() => RequestLoad();
        public void FireCommand() => Fire();
        public bool RequestLoad()
        {
            Initialize(); var a=CurrentAmmo;
            if (!isActiveAndEnabled || !initialized || State!=CannonState.Empty || a==null || a.stock<=0) return false;
            reservedAmmo=a; a.stock--; SetState(CannonState.Preparing); StartCoroutine(LoadRoutine()); return true;
        }
        public bool Fire()
        {
            if (!isActiveAndEnabled || !initialized || State!=CannonState.Loaded || loadedAmmo==null || (platform!=null && platform.IsMoving)) return false;
            SetState(CannonState.Recoiling); StartCoroutine(FireRoutine()); return true;
        }
        void Update() { if (!IsBusy && initialized) AdjustElevation(elevationInput*Mathf.Max(.01f,elevationSpeed)*Time.deltaTime); }
        void ApplyElevation() => elevationPivot.localRotation=elevationRest*Quaternion.AngleAxis((invertElevation?-1:1)*Elevation,elevationAxis.normalized);
        void SetState(CannonState state) { State=state; onStateChanged.Invoke(state); }
        IEnumerator LoadRoutine()
        {
            float target=Mathf.Clamp(loadElevation,minElevation,maxElevation);
            while (!Mathf.Approximately(Elevation,target)) { Elevation=Mathf.MoveTowards(Elevation,target,Mathf.Max(.01f,elevationSpeed)*1.5f*Time.deltaTime); ApplyElevation(); yield return null; }
            yield return RotateDoor(0,doorOpenAngle,doorTime);
            SetState(CannonState.Loading); onLoadStart.Invoke();
            float elapsed=0; while (elapsed<Mathf.Max(0,loadTime)) { elapsed+=Time.deltaTime; yield return null; }
            yield return RotateDoor(doorOpenAngle,0,doorTime);
            loadedAmmo=reservedAmmo; reservedAmmo=null; SetState(CannonState.Loaded); onLoadFinished.Invoke();
        }
        IEnumerator RotateDoor(float from,float to,float duration)
        {
            for(float t=0;t<duration;t+=Time.deltaTime) { doorHinge.localRotation=doorRest*Quaternion.AngleAxis(Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,t/duration)),doorAxis.normalized); yield return null; }
            doorHinge.localRotation=doorRest*Quaternion.AngleAxis(to,doorAxis.normalized);
        }
        IEnumerator FireRoutine()
        {
            var a=loadedAmmo; loadedAmmo=null;
            if (a.projectilePrefab != null)
            {
                var go=Instantiate(a.projectilePrefab,muzzle.position,Quaternion.LookRotation(ShotDirection));
                if (go.TryGetComponent(out Rigidbody rb)) rb.AddForce(ShotDirection*a.muzzleVelocity,ForceMode.VelocityChange);
            }
            onFire.Invoke();
            Vector3 worldTravel = recoilPivot.parent.TransformDirection(recoilAxis.normalized)*Mathf.Max(0,recoilDistance);
            Vector3 back=recoilRest-recoilPivot.parent.InverseTransformVector(worldTravel);
            for(float t=0;t<recoilTime;t+=Time.deltaTime) { recoilPivot.localPosition=Vector3.Lerp(recoilRest,back,1-Mathf.Pow(1-t/recoilTime,3)); yield return null; }
            recoilPivot.localPosition=back;
            for(float t=0;t<recoverTime;t+=Time.deltaTime) { recoilPivot.localPosition=Vector3.Lerp(back,recoilRest,Mathf.SmoothStep(0,1,t/recoverTime)); yield return null; }
            recoilPivot.localPosition=recoilRest; SetState(CannonState.Empty);
        }
        void OnDisable()
        {
            StopAllCoroutines(); elevationInput=0;
            if (reservedAmmo!=null) { reservedAmmo.stock++; reservedAmmo=null; }
            if (initialized) { recoilPivot.localPosition=recoilRest; doorHinge.localRotation=doorRest; }
            if (IsBusy) { loadedAmmo=null; SetState(CannonState.Empty); }
        }
    }
}
