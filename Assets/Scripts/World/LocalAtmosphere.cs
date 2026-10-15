using UnityEngine;

namespace ProjectLeviathan.World
{
    /// <summary>Low-frequency distance gating for bounded smoke, mist and service lighting.</summary>
    public sealed class LocalAtmosphere : MonoBehaviour
    {
        public ParticleSystem[] particles;
        public Light[] lights;
        [Min(20)] public float effectsDistance=650;
        [Min(10)] public float lightDistance=120;
        float nextCheck;
        void Update()
        {
            if(Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.5f;
            Camera camera=Camera.main;if(camera==null)return;
            float distance=(camera.transform.position-transform.position).sqrMagnitude;
            bool dark=Shader.GetGlobalVector("_LeviathanSkyState").x<.4f;
            if(lights!=null)foreach(var light in lights)if(light!=null)light.enabled=dark && distance<lightDistance*lightDistance;
            if(particles!=null)foreach(var particle in particles)if(particle!=null)
            {
                bool near=distance<effectsDistance*effectsDistance;
                if(near && !particle.isPlaying)particle.Play();
                else if(!near && !particle.isStopped)particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
