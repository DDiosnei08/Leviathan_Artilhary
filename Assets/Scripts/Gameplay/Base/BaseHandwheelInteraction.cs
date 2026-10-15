namespace ProjectLeviathan.Base
{
    public sealed class BaseHandwheelInteraction : RotaryInstrumentInteraction
    {
        public PlatformRotator platform;
        public CannonController cannon;
        public bool elevation;
        public float gearRatio = .04f;
        protected override float ResolveRotation(float degrees)
        {
            if (gearRatio <= 0) return 0;
            if (elevation) return cannon!=null ? cannon.AdjustElevation(degrees*gearRatio)/gearRatio : 0;
            if (platform==null || (cannon!=null && cannon.IsBusy)) return 0;
            platform.QueueRotation(degrees*gearRatio); return degrees;
        }
    }
}
