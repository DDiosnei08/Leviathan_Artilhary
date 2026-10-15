using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectLeviathan.World
{
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-150)]
    public sealed class LayeredSkyController : MonoBehaviour
    {
        public Material skyMaterial;
        public Light sun, moon;
        public WorldWeatherProvider weather;
        [Header("Clock")]
        [Range(0,24)] public float timeOfDay = 9.5f;
        public bool cycleTime = true;
        [Min(1)] public float minutesPerDay = 30;
        public bool animateAtmosphere = true;
        [Range(-70,70)] public float orbitTilt = 30;
        [Range(0,360)] public float sunriseBearing = 90;
        [Header("Lighting")]
        public float sunIntensity = 1.35f, moonIntensity = 0.28f;
        public Color sunDayColor = new Color(1,.95f,.85f), moonColor = new Color(.58f,.72f,1);
        public Color dayAmbientSky = new Color(.51f,.61f,.66f), dayAmbientHorizon = new Color(.43f,.47f,.45f);
        public Color nightAmbientSky = new Color(.13f,.18f,.28f), nightAmbientHorizon = new Color(.07f,.09f,.14f);
        public Color dayFog = new Color(.65f,.71f,.71f), nightFog = new Color(.025f,.04f,.075f);
        [Tooltip("Refresh reflection/ambient sky environment occasionally, never every frame.")]
        public bool updateEnvironment = true;
        [Min(10)] public float environmentUpdateInterval = 30;
        public float CurrentHour => Application.IsPlaying(gameObject) ? runtimeHour : timeOfDay;

        static readonly int SunId = Shader.PropertyToID("_LeviathanSkySun");
        static readonly int StateId = Shader.PropertyToID("_LeviathanSkyState");
        static readonly int MotionId = Shader.PropertyToID("_LeviathanSkyMotion");
        static LayeredSkyController owner;
        bool initialized, playing;
        float runtimeHour, configuredHour, clock, nextEnvironment;
        Vector2 drift;
        Material previousSky;
        Light previousMain;
        AmbientMode previousAmbientMode;
        Color previousAmbientSky, previousAmbientHorizon, previousAmbientGround, previousFog;
        float previousReflection;
        Vector4 previousSunGlobal, previousStateGlobal, previousMotionGlobal;
        LightState oldSun, oldMoon;
        struct LightState
        {
            public Quaternion rotation; public Color color; public float intensity; public bool enabled;
            public static LightState Read(Light light) => new LightState { rotation=light.transform.rotation,color=light.color,intensity=light.intensity,enabled=light.enabled };
            public void Restore(Light light) { if(light==null)return;light.transform.rotation=rotation;light.color=color;light.intensity=intensity;light.enabled=enabled; }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void PreparePlay()
        {
            // ExecuteAlways must explicitly cross the Editor/Play boundary when scene reload is disabled.
            foreach (var sky in FindObjectsByType<LayeredSkyController>()) if (sky.isActiveAndEnabled && Application.IsPlaying(sky.gameObject)) sky.EnsureInitialized();
        }
        void OnEnable() { EnsureInitialized(); Apply(); }
        void EnsureInitialized()
        {
            bool nowPlaying = Application.IsPlaying(gameObject);
            if (initialized && playing == nowPlaying) return;
            Restore();
            if (skyMaterial == null || sun == null || moon == null) return;
            if (owner != null && owner != this) return;
            owner = this; playing = nowPlaying; initialized = true;
            previousSky=RenderSettings.skybox;previousMain=RenderSettings.sun;previousAmbientMode=RenderSettings.ambientMode;
            previousAmbientSky=RenderSettings.ambientSkyColor;previousAmbientHorizon=RenderSettings.ambientEquatorColor;
            previousAmbientGround=RenderSettings.ambientGroundColor;previousFog=RenderSettings.fogColor;previousReflection=RenderSettings.reflectionIntensity;
            previousSunGlobal=Shader.GetGlobalVector(SunId);previousStateGlobal=Shader.GetGlobalVector(StateId);previousMotionGlobal=Shader.GetGlobalVector(MotionId);
            oldSun=LightState.Read(sun);oldMoon=LightState.Read(moon);
            runtimeHour=configuredHour=timeOfDay;clock=0;drift=Vector2.zero;nextEnvironment=0;
        }
        void Update()
        {
            EnsureInitialized(); if (!initialized || owner != this) return;
            if (configuredHour != timeOfDay) {runtimeHour=timeOfDay;configuredHour=timeOfDay;}
            if (playing)
            {
                float delta=Time.deltaTime;
                if(cycleTime) runtimeHour=Mathf.Repeat(runtimeHour+delta*24/(Mathf.Max(1,minutesPerDay)*60),24);
                if(animateAtmosphere)
                {
                    clock+=delta;
                    var sample=weather!=null ? weather.SampleAt(transform.position) : new WorldWeatherSample(Vector3.right,4,12000,0);
                    drift+=new Vector2(sample.WindDirection.x,sample.WindDirection.z)*sample.WindSpeedMetersPerSecond*delta;
                }
            }
            Apply();
        }
        public void SetTimeOfDay(float hour)
        {
            EnsureInitialized();
            if(Application.IsPlaying(gameObject)) runtimeHour=Mathf.Repeat(hour,24);
            else
            {
#if UNITY_EDITOR
                UnityEditor.Undo.RecordObject(this,"Definir horario do ceu");
#endif
                timeOfDay=Mathf.Repeat(hour,24);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                if(gameObject.scene.IsValid())UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            }
            Apply();
        }
        [ContextMenu("Horario/Dia (12:00)")] void Day() => SetTimeOfDay(12);
        [ContextMenu("Horario/Amanhecer (06:00)")] void Dawn() => SetTimeOfDay(6);
        [ContextMenu("Horario/Entardecer (18:00)")] void Dusk() => SetTimeOfDay(18);
        [ContextMenu("Horario/Noite (00:00)")] void Night() => SetTimeOfDay(0);
        void Apply()
        {
            if (!initialized || owner != this) return;
            float phase=CurrentHour*Mathf.PI/12;
            Vector3 direction=Quaternion.Euler(0,sunriseBearing,0)*Quaternion.Euler(0,0,-orbitTilt)*new Vector3(0,-Mathf.Cos(phase),Mathf.Sin(phase));
            float day=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,.12f,direction.y));
            float twilight=1-Mathf.SmoothStep(0,1,Mathf.Abs(direction.y)/.32f);
            float dawn=Mathf.Sin(phase)>=0 ? 1 : 0;
            Shader.SetGlobalVector(SunId,direction);
            Shader.SetGlobalVector(StateId,new Vector4(day,twilight,dawn,1));
            Shader.SetGlobalVector(MotionId,new Vector4(clock,drift.x,drift.y,0));
            RenderSettings.skybox=skyMaterial;
            sun.transform.rotation=Quaternion.LookRotation(-direction);
            moon.transform.rotation=Quaternion.LookRotation(direction);
            sun.color=Color.Lerp(new Color(1,.56f,.32f),sunDayColor,Mathf.InverseLerp(0,.4f,direction.y));
            sun.intensity=Mathf.Max(0,sunIntensity)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.03f,.35f,direction.y));sun.enabled=true;
            moon.color=moonColor;moon.intensity=Mathf.Max(0,moonIntensity)*(1-day);moon.enabled=moon.intensity>.001f;
            RenderSettings.sun=sun.intensity>=moon.intensity ? sun : moon;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(nightAmbientSky,dayAmbientSky,day);
            RenderSettings.ambientEquatorColor=Color.Lerp(nightAmbientHorizon,dayAmbientHorizon,day);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.025f,.035f,.05f),new Color(.25f,.27f,.26f),day);
            Color warm=dawn>0.5f ? new Color(.8f,.52f,.35f) : new Color(.63f,.38f,.48f);
            RenderSettings.fogColor=Color.Lerp(Color.Lerp(nightFog,dayFog,day),warm,twilight*.5f);
            RenderSettings.reflectionIntensity=Mathf.Lerp(.2f,1,day);
            if(playing && updateEnvironment && Time.unscaledTime>=nextEnvironment)
            {
                nextEnvironment=Time.unscaledTime+Mathf.Max(10,environmentUpdateInterval);
                DynamicGI.UpdateEnvironment();
            }
        }
        void OnValidate()
        {
            timeOfDay=Mathf.Repeat(timeOfDay,24);minutesPerDay=Mathf.Max(1,minutesPerDay);
            sunIntensity=Mathf.Max(0,sunIntensity);moonIntensity=Mathf.Max(0,moonIntensity);
            environmentUpdateInterval=Mathf.Max(10,environmentUpdateInterval);
        }
        void Restore()
        {
            if(!initialized)return;
            if(owner==this)
            {
                RenderSettings.skybox=previousSky;RenderSettings.sun=previousMain;RenderSettings.ambientMode=previousAmbientMode;
                RenderSettings.ambientSkyColor=previousAmbientSky;RenderSettings.ambientEquatorColor=previousAmbientHorizon;
                RenderSettings.ambientGroundColor=previousAmbientGround;RenderSettings.fogColor=previousFog;RenderSettings.reflectionIntensity=previousReflection;
                Shader.SetGlobalVector(SunId,previousSunGlobal);Shader.SetGlobalVector(StateId,previousStateGlobal);Shader.SetGlobalVector(MotionId,previousMotionGlobal);
                oldSun.Restore(sun);oldMoon.Restore(moon);owner=null;
            }
            initialized=false;
        }
        void OnDisable() => Restore();
        void OnDestroy() => Restore();
    }
}
