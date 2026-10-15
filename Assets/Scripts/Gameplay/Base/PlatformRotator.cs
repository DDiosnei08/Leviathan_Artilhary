using UnityEngine;

namespace ProjectLeviathan.Base
{
    /// <summary>Attach to an upright Unity pivot, not the axis-converted FBX empty.</summary>
    public sealed class PlatformRotator : MonoBehaviour
    {
        public float maxSpeed = 6f, acceleration = 3f, braking = 5f;
        [SerializeField] float currentSpeed;
        float input, remaining;
        Rigidbody body;
        public float CurrentSpeed => currentSpeed;
        public float Heading => transform.eulerAngles.y;
        public bool IsMoving => Mathf.Abs(currentSpeed) > .01f || Mathf.Abs(remaining) > .01f;
        void Awake() { body = GetComponent<Rigidbody>(); if (body != null) body.isKinematic = true; }
        public void SetInput(float value) { input = Mathf.Clamp(value,-1,1); remaining = 0; }
        public void QueueRotation(float degrees) { input = 0; remaining = Mathf.Clamp(remaining + degrees,-180,180); }
        public void Stop() { input = remaining = currentSpeed = 0; }
        void OnDisable() => Stop();
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float target = input * Mathf.Max(0,maxSpeed);
            if (Mathf.Abs(remaining) > .001f)
                target = Mathf.Sign(remaining) * Mathf.Min(Mathf.Max(0,maxSpeed),Mathf.Sqrt(2*Mathf.Max(.01f,braking)*Mathf.Abs(remaining)));
            currentSpeed = Mathf.MoveTowards(currentSpeed,target,Mathf.Max(.01f,Mathf.Abs(target)<Mathf.Abs(currentSpeed) ? braking : acceleration)*dt);
            float step = currentSpeed * dt;
            if (Mathf.Abs(remaining) > .001f)
            {
                if (Mathf.Abs(step) >= Mathf.Abs(remaining) && Mathf.Sign(step)==Mathf.Sign(remaining))
                { step = remaining; remaining = currentSpeed = 0; }
                else remaining -= step;
            }
            Quaternion rotation = Quaternion.AngleAxis(step,Vector3.up) * transform.rotation;
            if (body != null) body.MoveRotation(rotation); else transform.rotation = rotation;
        }
    }
}
